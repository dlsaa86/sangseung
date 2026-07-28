#!/usr/bin/env bash
# PreToolUse gate for Unity MCP calls.
#
# Why: a dead editor does not fail fast. The MCP call sits until the 120s tool timeout,
# and twice in one session the editor was gone (GPU crash) or the pipe was stale while
# every call kept "running". Detecting it here costs milliseconds instead of minutes.
#
# What it checks: whether an editor process actually owns this project right now.
# What it CANNOT check: isPlaying / isCompiling — Unity does not expose those to the
# filesystem, so those still have to be read through an MCP call.
#
# Exit 0 with no output = allow. Exit 0 with JSON = deny.

set -uo pipefail

ROOT="${CLAUDE_PROJECT_DIR:-$(pwd)}"
INSTANCE="$ROOT/Library/EditorInstance.json"

# jq is not installed on this machine; node is. Use it for correct JSON escaping —
# hand-rolled sed escaping breaks on quotes and backslashes in Windows paths.
deny() {
  MSG="$1" node -e '
    const msg = process.env.MSG;
    process.stdout.write(JSON.stringify({
      hookSpecificOutput: {
        hookEventName: "PreToolUse",
        permissionDecision: "deny",
        permissionDecisionReason: msg
      }
    }));
  '
  exit 0
}

VERSION="$(sed -n 's/^m_EditorVersion: *//p' "$ROOT/ProjectSettings/ProjectVersion.txt" 2>/dev/null | head -1)"

if [ ! -s "$INSTANCE" ]; then
  deny "Unity 에디터가 이 프로젝트를 열고 있지 않다 (Library/EditorInstance.json 없음/빈 파일). Unity ${VERSION:-6000.5.5f1} 로 이 프로젝트를 먼저 열 것: $ROOT"
fi

PID="$(grep -oE '"process_id"[[:space:]]*:[[:space:]]*[0-9]+' "$INSTANCE" | grep -oE '[0-9]+$' | head -1)"
if [ -z "${PID:-}" ]; then
  deny "EditorInstance.json 에서 process_id 를 읽지 못했다. 파일이 손상됐을 수 있다: $INSTANCE"
fi

# EditorInstance.json survives a crash, so its existence proves nothing. Check the PID.
ALIVE=0
if command -v tasklist >/dev/null 2>&1; then
  tasklist //FI "PID eq $PID" 2>/dev/null | grep -qE "[[:space:]]$PID[[:space:]]" && ALIVE=1
elif command -v ps >/dev/null 2>&1; then
  ps -p "$PID" >/dev/null 2>&1 && ALIVE=1
else
  ALIVE=1   # No way to check — do not block on a missing tool.
fi

if [ "$ALIVE" -ne 1 ]; then
  # EditorInstance.json records the editor binary, so the recovery hint stays correct on any OS.
  APP="$(sed -n 's/.*"app_path"[^"]*"\([^"]*\)".*/\1/p' "$INSTANCE" 2>/dev/null | head -1)"
  deny "Unity 프로세스(PID $PID)가 죽어 있다. EditorInstance.json 은 크래시 후에도 남으므로 파일 존재만으로는 생존을 알 수 없다. 에디터를 다시 실행한 뒤 재시도할 것${APP:+ ($APP)}: $ROOT"
fi

exit 0
