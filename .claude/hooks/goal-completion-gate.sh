#!/usr/bin/env bash
# Stop hook — 활성 목표가 있을 때만 도는 완주 게이트.
#
# ── 이 훅이 존재하는 이유 ─────────────────────────────────────────────────────
# 2026-08-07 에 로그를 셌더니 4일간 서브에이전트 호출 48회, 그중 8종만 돌았다.
# 46개 중 38개는 한 번도 안 돌았고, 거기엔 game-designer·systems-designer·
# economy-designer·qa-lead 가 전부 포함돼 있었다. 즉 "팀"은 파일로만 있었다.
#
# 돈 둘의 공통점이 답이었다 — unity-scene-owner(13회)는 CLAUDE.md 가 배타 소유권으로
# 강제했고, visual-critic(9회)은 visual-verify 스킬이 직접 불렀다. 나머지 38개는
# "필요하면 부르세요" 라고 적힌 텍스트였고 그래서 안 불렸다.
#
#   호출 장치가 없는 에이전트는 에이전트가 아니라 문서다.
#   완료 기준도 마찬가지다 — 대조하는 장치가 없으면 그냥 통과한다.
#
# ── 왜 autonomous-batch-gate 로 충분하지 않은가 ──────────────────────────────
# 그 게이트는 review-mode=solo 일 때만 돈다. CLAUDE.md 가 직접 인정하는 대로
# "협업 모드에서는 여전히 아무것도 강제하지 않는다". 사용자가 자리에 있는 동안
# 던진 목표가 끝까지 가는지는 아무도 확인하지 않았다. 이 훅이 그 자리를 메운다.
#
# ── 발동 조건을 좁게 잡은 이유 ────────────────────────────────────────────────
# 2026-08-03 에 하네스를 지우게 만든 것은 기준이 아니라 발동 조건이었다. Stop 훅이
# 작업 종류를 안 가려서 "블렌더 접속됐어?" 한 줄에도 저장소를 통째로 읽었다.
# 그래서 이 훅은 production/goals/ACTIVE.md 가 있고 그 status 가 ACTIVE 일 때만 돈다.
# 잡담·질문·단발 수정에는 첫 세 줄에서 끝난다 (실측 ~5ms).
#
# status: INTAKE 에서는 막지 않는다 — 그때 멈추는 것은 사용자에게 되묻기 위해서이고,
# 그 멈춤을 막으면 심문 게이트 자체가 성립하지 않는다.
#
# exit 0 = 종료 허용. exit 2 = 종료 차단 + stderr 를 모델에게 전달.
# 끄기: SKIP_GOAL_GATE=1

set -uo pipefail

# 훅 입력은 stdin 으로 온다. 한 번만 읽히므로 맨 앞에서 잡아 둔다.
HOOK_INPUT="$(cat 2>/dev/null || true)"

ROOT="${CLAUDE_PROJECT_DIR:-$(pwd)}"
cd "$ROOT" 2>/dev/null || exit 0

[ "${SKIP_GOAL_GATE:-}" = "1" ] && exit 0

GOAL="$ROOT/production/goals/ACTIVE.md"
[ -f "$GOAL" ] || exit 0

# ── ① 상태 확인 — ACTIVE 가 아니면 돌지 않는다 ───────────────────────────────
STATUS="$(sed -n 's/^[[:space:]]*-\{0,1\}[[:space:]]*status:[[:space:]]*//p' "$GOAL" \
          | head -1 | tr -d ' \t\r')"
# HANDOFF — 사용자가 「새 세션에서 이어가자」고 지시한 상태. 목표는 여전히 미완이고
# 체크박스도 그대로지만, 이 세션은 끝났으므로 막지 않는다.
#
# 2026-08-07 추가. 직전 판은 「완료 선언」과 「사용자 지시로 인한 종료」를 구분하지 못해,
# 사용자가 명시적으로 세션을 끝냈는데도 게이트가 붙잡고 3회 정체 해제까지 헛돌았다.
# 발화를 문자열로 맞히는 대신 **파일에 적힌 상태**로 판정한다 — 사람이 보고 되돌릴 수 있고,
# 다음 세션이 ACTIVE 로 되돌리기 전까지는 기록으로 남는다.
#
# 남용 방지는 은폐 불가로 한다: HANDOFF 는 조용히 통과하지 않고 매번 크게 보고된다.
# 모델이 임의로 이 값을 쓰면 사용자 눈에 그대로 띈다.
if [ "$STATUS" = "HANDOFF" ]; then
  AC_H="$(awk '/^##[[:space:]]*완료 기준/{f=1;next} /^##[[:space:]]/{f=0} f' "$GOAL")"
  LEFT="$(printf '%s\n' "$AC_H" | grep -c '^[[:space:]]*-[[:space:]]*\[[[:space:]]\]' || true)"
  DONE_H="$(printf '%s\n' "$AC_H" | grep -c '^[[:space:]]*-[[:space:]]*\[[xX]\]' || true)"
  echo "[완주 게이트] 목표가 HANDOFF 상태다 — 막지 않는다. **완료가 아니다**: ${DONE_H} 충족 / ${LEFT} 남음. 다음 세션은 production/goals/ACTIVE.md 를 읽고 status 를 ACTIVE 로 되돌린 뒤 이어간다." >&2
  exit 0
fi

[ "$STATUS" = "ACTIVE" ] || exit 0

TITLE="$(sed -n 's/^#[[:space:]]*GOAL:[[:space:]]*//p' "$GOAL" | head -1)"
[ -n "$TITLE" ] || TITLE="(제목 없음)"

# ── ② 완료 기준 대조 ─────────────────────────────────────────────────────────
AC_BLOCK="$(awk '/^##[[:space:]]*완료 기준/{f=1;next} /^##[[:space:]]/{f=0} f' "$GOAL")"

TODO_LINES="$(printf '%s\n' "$AC_BLOCK" | grep '^[[:space:]]*-[[:space:]]*\[[[:space:]]\]' || true)"
TODO="$(printf '%s' "$TODO_LINES" | grep -c . || true)"
DONE="$(printf '%s\n' "$AC_BLOCK" | grep -c '^[[:space:]]*-[[:space:]]*\[[xX]\]' || true)"

# 완료 기준 절이 비어 있으면 그것 자체가 실패다 — AC 없는 ACTIVE 목표는 있을 수 없다.
if [ "$TODO" -eq 0 ] && [ "$DONE" -eq 0 ]; then
  cat >&2 <<EOF
[완주 게이트] production/goals/ACTIVE.md 가 ACTIVE 인데 「## 완료 기준」 절에 항목이 없다.

AC 없는 활성 목표는 완료를 판정할 수 없으므로 존재할 수 없다. 둘 중 하나를 한다.
  · 완료 기준을 채운다 (/goal 의 심문 단계로 돌아간다)
  · 목표를 접는다 — status 를 DONE 으로 바꾸거나 파일을 production/goals/<slug>.md 로 옮긴다
EOF
  exit 2
fi

[ "$TODO" -eq 0 ] && exit 0   # 전부 체크됨 — 통과

# ── ③ 정체 판정 ──────────────────────────────────────────────────────────────
# autonomous-batch-gate 와 같은 방식. 멈추지 못하는 루프는 자율주행이 아니라 무한 루프다.
STATE_DIR="$ROOT/.claude/state"
STATE="$STATE_DIR/goal-gate.txt"
mkdir -p "$STATE_DIR"

MAX_SAME=3        # 같은 상태로 이만큼 막으면 정체로 보고 풀어 준다
MAX_SESSION=12    # 세션 전체 차단 상한

# ── ②-a 사용자에게 넘기려는 정지는 막지 않는다 ────────────────────────────────
# 2026-08-07 실측으로 추가. 이 훅은 「다 했다」는 정지와 「사용자에게 물어보려는」
# 정지를 구분하지 못했다. 협업 모드에서 그건 결함이다 — 모델이 창작 결정을 물으려는데
# 게이트가 붙잡아 두면, 사용자는 자리에 있는데도 답할 기회를 못 얻는다.
# 자율주행 게이트에는 이 문제가 없었다. 물어볼 사람이 없으니까.
#
# 판정은 목표 파일이 아니라 **모델의 마지막 발화**로 한다. 훅 입력의 transcript_path 를
# 읽어 마지막 assistant 메시지가 물음표로 끝나면 통과시킨다.
# 남용을 막는 장치 둘: ① 세션당 PASS_ASK_MAX 회까지만 ② 그 횟수도 상태 파일에 남는다.
PASS_ASK_MAX=4
ASK_STATE="$STATE_DIR/goal-gate-ask.txt"
ASKED="$(cat "$ASK_STATE" 2>/dev/null || echo 0)"
case "$ASKED" in ''|*[!0-9]*) ASKED=0 ;; esac

TRANSCRIPT="$(printf '%s' "$HOOK_INPUT" | sed -n 's/.*"transcript_path"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)"
TRANSCRIPT="$(printf '%s' "$TRANSCRIPT" | sed 's/\\\\/\//g')"
if [ -n "$TRANSCRIPT" ] && [ -f "$TRANSCRIPT" ] && [ "$ASKED" -lt "$PASS_ASK_MAX" ] \
   && command -v node >/dev/null 2>&1; then
  # 마지막 assistant 발화의 **마지막 줄**만 본다. 본문 아무 데나 물음표가 있다고
  # 통과시키면 게이트가 사실상 꺼진다 — 묻는 문장은 언제나 끝에 온다.
  LAST="$(node -e '
    const fs=require("fs");
    const ls=fs.readFileSync(process.argv[1],"utf8").trim().split("\n");
    for (let i=ls.length-1;i>=0;i--) {
      let m; try { m=JSON.parse(ls[i]); } catch { continue; }
      if (m.type!=="assistant") continue;
      const c=m.message?.content;
      if (!Array.isArray(c)) continue;
      const t=c.filter(b=>b.type==="text").map(b=>b.text).join("").trim();
      if (!t) continue;
      const lines=t.split("\n").map(s=>s.trim()).filter(Boolean);
      process.stdout.write(lines[lines.length-1].slice(-200));
      break;
    }
  ' "$TRANSCRIPT" 2>/dev/null)"
  case "$LAST" in
    *"?"|*"까요"*|*"습니까"*)
      echo $((ASKED + 1)) > "$ASK_STATE"
      echo "[완주 게이트] 사용자에게 묻는 정지로 판단해 통과시킨다 (${ASKED}/${PASS_ASK_MAX}회 사용). 목표는 여전히 미완이다." >&2
      exit 0 ;;
  esac
fi

SIG="$( { git rev-parse HEAD 2>/dev/null; git status --porcelain 2>/dev/null; \
          cat "$GOAL" 2>/dev/null; } | git hash-object --stdin 2>/dev/null )"
[ -n "$SIG" ] || SIG="nosig"

PREV_SIG=""; SAME=0; SESSION=0
if [ -f "$STATE" ]; then
  PREV_SIG="$(sed -n 's/^sig=//p'     "$STATE" | tail -1)"
  SAME="$(    sed -n 's/^same=//p'    "$STATE" | tail -1)"
  SESSION="$( sed -n 's/^session=//p' "$STATE" | tail -1)"
fi
[ -n "$SAME" ]    || SAME=0
[ -n "$SESSION" ] || SESSION=0

if [ "$SIG" = "$PREV_SIG" ]; then SAME=$((SAME + 1)); else SAME=1; fi
SESSION=$((SESSION + 1))

printf 'sig=%s\nsame=%s\nsession=%s\nts=%s\ngoal=%s\n' \
  "$SIG" "$SAME" "$SESSION" "$(date '+%Y-%m-%d %H:%M:%S')" "$TITLE" > "$STATE"

if [ "$SAME" -ge "$MAX_SAME" ]; then
  cat >&2 <<EOF
[완주 게이트 — 정체로 판단해 멈춘다]

목표: $TITLE
같은 상태(지문 ${SIG:0:8})로 ${SAME}회 막았고 그 사이 디스크도 목표 파일도 바뀌지 않았다.
OPERATING_MODES.md §6 「같은 실패가 3회 반복」에 해당한다. 더 시도하지 않는다.

남은 완료 기준:
$TODO_LINES

필요한 것은 반복이 아니라 구조 변경이거나 사용자 결정이다. 사용자에게
무엇이 막혔는지와 선택지를 제시하고, 되돌리기 어려운 항목이면
docs/runtime/PENDING_DECISIONS.md 에 교체 가능한 기본 프리셋과 함께 적는다.
EOF
  exit 0
fi

if [ "$SESSION" -gt "$MAX_SESSION" ]; then
  echo "[완주 게이트] 세션 차단 상한(${MAX_SESSION}회) 도달. 더 막지 않는다. 남은 기준을 사용자에게 보고할 것." >&2
  exit 0
fi

# ── ④ 되돌려보낸다 ───────────────────────────────────────────────────────────
cat >&2 <<EOF
[완주 게이트 — 이 목표는 아직 끝나지 않았다]  (${SAME}/${MAX_SAME} 회, 세션 ${SESSION}/${MAX_SESSION})

목표: $TITLE          (production/goals/ACTIVE.md)
진행: ${DONE} 충족 / ${TODO} 남음

남은 완료 기준 — 이것은 대화 요약이 아니라 목표 파일에 적힌 사실이다:
$TODO_LINES

── 이제 할 것 ────────────────────────────────────────────────────────────────
1. 위에서 **하나**를 고른다. 전부 한 번에 끝내려 하지 않는다.
2. 충족했으면 목표 파일의 해당 줄을 - [ ] 에서 - [x] 로 바꾸고 근거를 한 줄 덧붙인다.
   근거 없는 체크는 자기 승인이다. 어떤 파일·테스트·캡처가 그것을 보이는지 적는다.
3. VERIFY- 로 시작하는 기준은 **구현한 에이전트가 스스로 체크하지 않는다.**
   별도 에이전트를 부른다 — 요구사항 대조는 requirements-auditor,
   시각 판정은 visual-critic, 경계 조건은 test-adversary, 구조는 architecture-critic.
   (CLAUDE.md 탑다운 규칙 6)
4. 정말로 이 목표를 접어야 한다면 사용자에게 이유를 말하고 승인을 받는다.
   혼자 status 를 DONE 으로 바꾸지 않는다.

같은 상태로 ${MAX_SAME}회 막히면 자동으로 풀어 주고 정체로 보고한다.
EOF
exit 2
