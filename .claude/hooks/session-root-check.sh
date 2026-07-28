#!/usr/bin/env bash
# SessionStart briefing.
#
# Why: a session once started in a look-alike copy of this project
# (…/iCloudDrive/01_Projects/Unity/Upandup_DDD and a checkpoint worktree both mirror the
# tree). Edits there compile nowhere and every verification silently measures the wrong
# checkout. Cheap to detect, expensive to discover late.
#
# Also surfaces the two facts a Unity session always wants up front: is an editor running,
# and when did the self-tests last pass.
#
# Never blocks — SessionStart cannot deny anyway. It injects context.

set -uo pipefail

ROOT="${CLAUDE_PROJECT_DIR:-$(pwd)}"
NOTES=""
add() { NOTES="${NOTES}$1"$'\n'; }

# ── Is this the real checkout? ──
if [ ! -d "$ROOT/Assets" ] || [ ! -d "$ROOT/ProjectSettings" ]; then
  add "경고: '$ROOT' 는 Unity 프로젝트 루트가 아니다 (Assets/ 또는 ProjectSettings/ 없음)."
elif [ ! -f "$ROOT/Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity" ]; then
  add "경고: Prototype_Elevator 씬이 없다. 다른 프로젝트이거나 체크아웃이 불완전하다."
fi

# Mirror/sync/checkpoint locations, not a specific machine's layout — this has to stay true
# when the repo moves to another OS.
case "$ROOT" in
  *iCloudDrive*|*"Mobile Documents"*|*Dropbox*|*OneDrive*|*"Google Drive"*|*AssistantCheckpoints*|*orca/workspaces*)
    add "경고: '$ROOT' 는 동기화 폴더이거나 체크포인트 사본으로 보인다. 이런 경로에서 편집하면 실제로 열려 있는 Unity 에디터가 변경을 보지 못할 수 있다. 진짜 작업용 체크아웃이 맞는지 확인할 것."
    ;;
esac

# ── Editor status ──
INSTANCE="$ROOT/Library/EditorInstance.json"
if [ -s "$INSTANCE" ]; then
  PID="$(grep -oE '"process_id"[[:space:]]*:[[:space:]]*[0-9]+' "$INSTANCE" | grep -oE '[0-9]+$' | head -1)"
  if [ -n "${PID:-}" ] && command -v tasklist >/dev/null 2>&1 \
     && tasklist //FI "PID eq $PID" 2>/dev/null | grep -qE "[[:space:]]$PID[[:space:]]"; then
    add "Unity 에디터 실행 중 (PID $PID)."
  else
    add "Unity 에디터가 실행되어 있지 않다 (EditorInstance.json 은 있으나 프로세스 없음 — 크래시 흔적일 수 있다). Unity MCP 호출은 차단된다."
  fi
else
  add "Unity 에디터가 실행되어 있지 않다. Unity MCP 호출은 차단된다."
fi

# ── Last verification ──
MARKER="$ROOT/.claude/state/last-selftest.txt"
if [ -f "$MARKER" ]; then
  add "마지막 자체 검증: $(tr -d '\r\n' < "$MARKER")"
else
  add "자체 검증 기록 없음. 커밋 전에 Unity 메뉴 'Ascend/Run Self Tests' 를 실행할 것."
fi

MSG="$NOTES" node -e '
  const msg = (process.env.MSG || "").trim();
  process.stdout.write(JSON.stringify({
    hookSpecificOutput: {
      hookEventName: "SessionStart",
      additionalContext: "[프로젝트 사전 점검]\n" + msg
    }
  }));
'
exit 0
