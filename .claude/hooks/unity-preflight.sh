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

# --- Clear a modal that is already blocking the main thread ---------------------------
# A live PID is not the same as a responsive editor. If a modal is up, Unity's main thread
# is parked and this call would hang to its 120s timeout with nobody there to click. The
# clicker only touches dialogs whose title it recognises (see the script) and reports the
# rest without pressing anything — an unrecognised modal is still a decision for a person.
# ~480ms, measured; MCP calls cost seconds anyway.
CLICKER="$ROOT/.claude/hooks/unity-modal-autoclick.ps1"
if [ -f "$CLICKER" ] && command -v powershell.exe >/dev/null 2>&1; then
  OUT="$(CLAUDE_PROJECT_DIR="$ROOT" powershell.exe -NoProfile -ExecutionPolicy Bypass \
          -File "$CLICKER" 2>/dev/null | tr -d '\r\000' | grep -a -vE '^NONE' || true)"
  if [ -n "$OUT" ]; then
    mkdir -p "$ROOT/.claude/state"
    printf '%s  %s\n' "$(date '+%Y-%m-%d %H:%M:%S')" "$OUT" >> "$ROOT/.claude/state/modal-autoclick.log"
    # stderr, not stdout: stdout here is the deny channel and must stay JSON-or-empty.
    printf '%s\n' "$OUT" >&2
  fi
fi

exit 0
