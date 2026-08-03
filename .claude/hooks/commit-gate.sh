#!/usr/bin/env bash
# PreToolUse gate on `git commit`.
#
# Why: the self-tests only run because someone remembers to run them. That held all
# session, but "held because I remembered" is not a guarantee — and a commit message
# claiming verification that never happened is worse than no claim.
#
# ── 2026-08-04 복원 시 좁힌 것 ────────────────────────────────────────────────
# 직전 판본은 `Assets/Prototype_Elevator` 와 `Assets/Editor` **전체**에서 마커보다 새로운
# .cs 를 찾았다. 그래서 **다른 세션이나 다른 에이전트가 고친 파일** 때문에 내 커밋이
# 막혔다. 내가 건드리지도 않은 파일을 위해 20분짜리 자체 검증을 다시 돌리거나, 막힌
# 이유를 파악하는 데 턴을 썼다 — 그리고 그게 반복되면 순환에 빠진다.
#
# 이제 **이 커밋에 스테이징된 것만** 본다. 문서만 고친 커밋은 자동으로 통과하고,
# 스테이징된 코드나 씬이 마커보다 새로울 때만 막는다.
#
# 씬·프리팹·데이터도 함께 보는 이유: 이 저장소의 자체 검증(556 케이스)은 코드뿐 아니라
# 씬 배선과 밸런스 값도 검사한다. 씬만 바뀐 커밋을 통과시키면 그 검사가 무의미해진다.
#
# Escape hatch: SKIP_SELFTEST_GATE=1 in the command.
#
# Exit 0 with no output = allow. Exit 0 with JSON = deny.

set -uo pipefail

ROOT="${CLAUDE_PROJECT_DIR:-$(pwd)}"
cd "$ROOT" 2>/dev/null || exit 0
MARKER="$ROOT/.claude/state/last-selftest.txt"

INPUT="$(cat)"
CMD="$(MSG="$INPUT" node -e '
  try {
    const j = JSON.parse(process.env.MSG || "{}");
    process.stdout.write(String((j.tool_input && j.tool_input.command) || ""));
  } catch (e) { process.stdout.write(""); }
')"

# Only gate real commits. `git log`, `git commit --help`, etc. must pass through.
case "$CMD" in
  *"git commit"*) ;;
  *) exit 0 ;;
esac
case "$CMD" in
  *--help*|*SKIP_SELFTEST_GATE=1*) exit 0 ;;
esac

deny() {
  MSG="$1" node -e '
    process.stdout.write(JSON.stringify({
      hookSpecificOutput: {
        hookEventName: "PreToolUse",
        permissionDecision: "deny",
        permissionDecisionReason: process.env.MSG
      }
    }));
  '
  exit 0
}

# --- 이 커밋이 실제로 담고 있는 검증 대상 -------------------------------------------
# -A 가 붙은 커밋이면 아직 스테이징되지 않았을 수 있으므로 워킹 트리 변경도 함께 본다.
STAGED="$(git diff --cached --name-only --diff-filter=ACMR 2>/dev/null)"
case "$CMD" in
  *" -a"*|*" -A"*|*"--all"*)
    STAGED="$STAGED
$(git diff --name-only --diff-filter=ACMR 2>/dev/null)"
    ;;
esac

SUBJECTS="$(printf '%s\n' "$STAGED" | grep -E '\.(cs|unity|prefab|mat|asset)$' | sort -u)"

# 검증 대상이 하나도 없으면 코드·씬 커밋이 아니다. 문서만 고친 커밋은 여기서 끝난다.
[ -n "$SUBJECTS" ] || exit 0

if [ ! -f "$MARKER" ]; then
  deny "자체 검증 기록이 없는데 코드·씬 파일이 커밋에 포함돼 있다. Unity 메뉴 'Ascend/Run Self Tests' 를 실행한 뒤 커밋할 것.
$(printf '%s\n' "$SUBJECTS" | head -5 | sed 's/^/  /')
(정말 검증이 불필요한 커밋이면 명령 앞에 SKIP_SELFTEST_GATE=1 을 붙일 것)"
fi

# A recorded failure blocks regardless of timestamps.
if grep -qE 'fail=[1-9]' "$MARKER"; then
  deny "마지막 자체 검증이 실패로 기록되어 있다: $(tr -d '\r\n' < "$MARKER") — 고치고 다시 실행한 뒤 커밋할 것."
fi

# 이 커밋에 담긴 파일 중 마커보다 새로운 것만 센다.
NEWER=""
while IFS= read -r f; do
  [ -n "$f" ] || continue
  [ -f "$ROOT/$f" ] || continue
  if [ "$ROOT/$f" -nt "$MARKER" ]; then
    NEWER="${NEWER}  ${f}"$'\n'
  fi
done <<< "$SUBJECTS"

[ -z "$NEWER" ] && exit 0

deny "자체 검증 이후에 수정된 파일이 이 커밋에 들어 있다:
$(printf '%s' "$NEWER" | head -5)
'Ascend/Run Self Tests' 를 다시 실행한 뒤 커밋할 것. (마지막 검증: $(tr -d '\r\n' < "$MARKER"))
검증이 불필요한 커밋이면 명령 앞에 SKIP_SELFTEST_GATE=1 을 붙일 것."
