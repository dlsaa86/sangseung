#!/usr/bin/env bash
# Stop hook — 자율주행 중에만 도는 배치 완료 게이트.
#
# ── 이 훅이 존재하는 이유 ─────────────────────────────────────────────────────
# 사용자가 없는 동안 "완료 기준을 만족할 때까지 스스로 돌린다"를 실제로 성립시키는
# 장치다. 모델이 스스로 "다 했다"고 판단해 멈추려 할 때, 디스크에 있는 사실로 그 판단을
# 반증하고 되돌려보낸다. 품질은 이 되돌려보내기에서 나왔다.
#
# ── 2026-08-03 에 지워진 이유, 그리고 이번에 무엇을 바꿨는가 ──────────────────
# 지운 이유는 기준이 틀려서가 아니라 **발동 조건이 없어서**였다. 직전 판본은 Stop 훅이
# 작업 종류도 모드도 가리지 않고 **매 턴** 돌았다. 그래서 "블렌더 접속됐어?" 한 줄에도
# 서브에이전트가 저장소를 통째로 읽었고, 한 세션에서 스무 번 넘게 돌았으며 대부분은
# 탑다운 작업조차 아니었다. 사용자와 대화하는 내내 Unity 재검증 지시가 나갔다.
#
# 이번 판본이 바꾼 것은 셋이다.
#
#   1. **모드 게이팅.** production/review-mode.txt 가 solo(자율주행)일 때만 돈다.
#      협업 모드에서는 첫 두 줄에서 조용히 끝난다 — 이것이 낭비의 실제 원인이었다.
#      사용자가 자리에 있을 때 모델을 붙잡아 두는 것은 도움이 아니라 방해다.
#
#   2. **서브에이전트를 쓰지 않는다.** 비쌌던 것은 판정 기준이 아니라 sonnet
#      서브에이전트(타임아웃 900초)였다. 결정론적 검증기는 실측 0.68초다. 같은 판정을
#      1/1000 비용으로 얻는다. 독립 감사가 필요한 시점은 이 훅이 **지목만** 하고,
#      호출은 메인 루프가 한다 — 그래야 무엇이 왜 도는지 사람이 볼 수 있다.
#
#   3. **정체를 스스로 인정하고 멈춘다.** 같은 상태로 3회 막으면 풀어 준다
#      (.claude/OPERATING_MODES.md §6 "같은 실패가 3회 반복"). 세션당 상한도 둔다.
#      멈추지 못하는 루프는 자율주행이 아니라 무한 루프다.
#
# 판정 자체는 tools/verify-topdown.ps1 -Batch 가 한다. 이 스크립트는 **언제 물어볼지**만
# 정한다. 기준과 발동 시점을 분리한 것이 이번 복원의 요점이다.
#
# exit 0 = 종료 허용. exit 2 = 종료 차단 + stderr 를 모델에게 전달.
# 끄기: SKIP_BATCH_GATE=1

set -uo pipefail

ROOT="${CLAUDE_PROJECT_DIR:-$(pwd)}"
cd "$ROOT" 2>/dev/null || exit 0

[ "${SKIP_BATCH_GATE:-}" = "1" ] && exit 0

# ── ① 모드 확인 — 협업 모드면 여기서 끝난다 (비용 ~1ms) ──────────────────────
MODE_FILE="$ROOT/production/review-mode.txt"
MODE="$(tr -d ' \t\r\n' < "$MODE_FILE" 2>/dev/null || echo full)"
[ "$MODE" = "solo" ] || exit 0

VERIFIER="$ROOT/tools/verify-topdown.ps1"
[ -f "$VERIFIER" ] || exit 0
command -v powershell.exe >/dev/null 2>&1 || exit 0

STATE_DIR="$ROOT/.claude/state"
STATE="$STATE_DIR/batch-gate.txt"
mkdir -p "$STATE_DIR"

MAX_SAME=3        # 같은 상태로 이만큼 막으면 정체로 보고 풀어 준다
MAX_SESSION=12    # 세션 전체 차단 상한

# ── ② 현재 상태의 지문 ────────────────────────────────────────────────────────
# HEAD + 워킹 트리. 모델이 무언가 바꿨으면 지문이 바뀐다.
SIG="$( { git rev-parse HEAD 2>/dev/null; git status --porcelain 2>/dev/null; } \
        | git hash-object --stdin 2>/dev/null )"
[ -n "$SIG" ] || SIG="nosig"

PREV_SIG=""; SAME=0; SESSION=0
if [ -f "$STATE" ]; then
  PREV_SIG="$(sed -n 's/^sig=//p'     "$STATE" | tail -1)"
  SAME="$(    sed -n 's/^same=//p'    "$STATE" | tail -1)"
  SESSION="$( sed -n 's/^session=//p' "$STATE" | tail -1)"
fi
[ -n "$SAME" ]    || SAME=0
[ -n "$SESSION" ] || SESSION=0

write_state() {
  printf 'sig=%s\nsame=%s\nsession=%s\nts=%s\nverdict=%s\n' \
    "$SIG" "$1" "$2" "$(date '+%Y-%m-%d %H:%M:%S')" "$3" > "$STATE"
}

# ── ③ 결정론적 판정 (실측 0.68초) ────────────────────────────────────────────
OUT="$(powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$VERIFIER" -Batch 2>&1)"
CODE=$?

if [ "$CODE" -eq 0 ]; then
  write_state 0 "$SESSION" PASS
  # 통과해도 "지금은 막지 않는다" 절은 남긴다 — 미룬 요구가 조용히 사라지지 않게.
  DEFERRED="$(printf '%s\n' "$OUT" | sed -n '/지금은 막지 않는다/,$p')"
  if [ -n "$DEFERRED" ]; then
    printf '%s' "$DEFERRED" | node -e '
      let s = ""; process.stdin.on("data", d => s += d).on("end", () => {
        process.stdout.write(JSON.stringify({
          systemMessage: "[배치 게이트 통과] 미룬 요구가 남아 있다:\n" + s.trim()
        }));
      });
    '
  fi
  exit 0
fi

# ── ④ 정체 판정 — 되돌려보내기 전에 멈출 이유가 있는지 먼저 본다 ──────────────
if [ "$SIG" = "$PREV_SIG" ]; then
  SAME=$((SAME + 1))
else
  SAME=1
fi
SESSION=$((SESSION + 1))

if [ "$SAME" -ge "$MAX_SAME" ]; then
  write_state "$SAME" "$SESSION" STALLED
  cat >&2 <<EOF
[배치 게이트 — 정체로 판단해 멈춘다]

같은 상태(지문 ${SIG:0:8})로 ${SAME}회 막았고 그 사이 디스크가 바뀌지 않았다.
OPERATING_MODES.md §6 의 "같은 실패가 3회 반복" 에 해당한다. 더 시도하지 않는다.

필요한 것은 반복이 아니라 구조 변경이거나 사용자 결정이다.
docs/runtime/PENDING_DECISIONS.md 에 교체 가능한 기본 프리셋과 함께 적고,
production/session-logs/ 에 보고서를 남긴 뒤 이 배치를 끝낼 것.

마지막 판정:
$OUT
EOF
  exit 0
fi

if [ "$SESSION" -gt "$MAX_SESSION" ]; then
  write_state "$SAME" "$SESSION" CAPPED
  echo "[배치 게이트] 세션 차단 상한(${MAX_SESSION}회)에 도달했다. 더 막지 않는다. 남은 항목은 보고서에 적을 것." >&2
  exit 0
fi

# ── ⑤ 되돌려보낸다 ────────────────────────────────────────────────────────────
write_state "$SAME" "$SESSION" FAIL
cat >&2 <<EOF
[배치 게이트 — 이 배치는 아직 끝나지 않았다]  (${SAME}/${MAX_SAME} 회, 세션 ${SESSION}/${MAX_SESSION})

자율주행 중이다 (review-mode=solo). 아래는 대화 요약이나 완료 선언이 아니라
디스크에 있는 파일로만 낸 판정이다.

$OUT

── 이제 할 것 ────────────────────────────────────────────────────────────────
1. 위 목록에서 **가장 이른 실패 하나**를 고른다. 전부 한 번에 고치려 하지 않는다.
2. 고친 뒤 저장 → 자체 검증(Ascend/Run Self Tests) → docs/runtime/TOPDOWN_PROGRESS.md
   갱신 → 로컬 커밋. 순서를 지킨다.
3. 판정에 독립 검증이 필요하면 **구현자가 스스로 올리지 말고** 별도 에이전트를 부른다
   — 요구사항 대조는 requirements-auditor, 시각 판정은 visual-critic.
   (CLAUDE.md 탑다운 규칙 6: 구현자는 자기 작업을 VERIFIED 로 승인하지 않는다)
4. 되돌릴 수 있는 판단은 묻지 말고 기본값으로 진행하고 docs/ASSUMPTION_LOG.md 에 적는다.
   되돌리기 어려운 것만 docs/runtime/PENDING_DECISIONS.md 로 올리고, 그것 때문에도
   멈추지 말고 다른 독립 작업을 계속한다.

같은 상태로 ${MAX_SAME}회 막히면 자동으로 풀어 주고 정체로 보고한다.
EOF
exit 2
