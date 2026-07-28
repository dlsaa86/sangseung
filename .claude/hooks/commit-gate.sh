#!/usr/bin/env bash
# PreToolUse gate on `git commit`.
#
# Why: the self-tests only run because someone remembers to run them. That held all
# session, but "held because I remembered" is not a guarantee — and a commit message
# claiming verification that never happened is worse than no claim.
#
# Rule: block the commit if any tracked C# source is newer than the last self-test run,
# or if that run recorded a failure.
#
# Escape hatch: SKIP_SELFTEST_GATE=1 in the command, for genuinely non-code commits.
#
# Exit 0 with no output = allow. Exit 0 with JSON = deny.

set -uo pipefail

ROOT="${CLAUDE_PROJECT_DIR:-$(pwd)}"
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

if [ ! -f "$MARKER" ]; then
  deny "자체 검증 기록이 없다. Unity 메뉴 'Ascend/Run Self Tests' 를 실행한 뒤 커밋할 것. (코드와 무관한 커밋이면 명령 앞에 SKIP_SELFTEST_GATE=1 을 붙일 것)"
fi

# A recorded failure blocks regardless of timestamps.
if grep -qE 'fail=[1-9]' "$MARKER"; then
  deny "마지막 자체 검증이 실패로 기록되어 있다: $(tr -d '\r\n' < "$MARKER") — 고치고 다시 실행한 뒤 커밋할 것."
fi

# Any gameplay/editor C# newer than the marker means the tests predate the change.
NEWER="$(find "$ROOT/Assets/Prototype_Elevator" "$ROOT/Assets/Editor" \
           -name '*.cs' -newer "$MARKER" 2>/dev/null | head -5)"

if [ -n "$NEWER" ]; then
  LIST="$(printf '%s' "$NEWER" | sed "s|$ROOT/||" | tr '\n' ' ')"
  deny "자체 검증 이후 수정된 C# 파일이 있다: $LIST— 'Ascend/Run Self Tests' 를 다시 실행한 뒤 커밋할 것. (마지막 검증: $(tr -d '\r\n' < "$MARKER"))"
fi

exit 0
