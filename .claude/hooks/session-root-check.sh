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

# ── 운영 모드와 배치 게이트 ──
# topdown-gate-notice.sh 가 하던 일을 여기로 합쳤다. 훅이 하나 줄고, 모드와 게이트가
# 같은 줄에 보인다 — 이 둘은 항상 함께 읽어야 하는 값이다.
# 꺼진 게이트는 켜진 게이트보다 위험하다. 아무도 막히지 않으니 아무도 눈치채지 못한다.
MODE="$(tr -d ' \t\r\n' < "$ROOT/production/review-mode.txt" 2>/dev/null || echo full)"
if [ "$MODE" = "solo" ]; then
  if [ "${SKIP_BATCH_GATE:-}" = "1" ]; then
    add "운영 모드: AFK_AUTONOMOUS(solo) — 그런데 SKIP_BATCH_GATE=1 이라 배치 완료 게이트가 꺼져 있다. 아무도 완료를 검사하지 않는다. .claude/settings.local.json 의 env 에서 지울 것."
  else
    add "운영 모드: AFK_AUTONOMOUS(solo). 배치 완료 게이트가 켜져 있다 — 완료 기준을 통과할 때까지 Stop 이 차단된다 (같은 상태 3회 반복 시 자동 해제)."
  fi
  [ -f "$ROOT/docs/TOPDOWN_MASTER_BACKLOG.md" ] || add "경고: docs/TOPDOWN_MASTER_BACKLOG.md 가 없다. 배치 게이트가 항상 실패한다."
else
  add "운영 모드: COLLABORATIVE(full). 배치 완료 게이트는 돌지 않는다 — 창작 결정은 사용자가 고른다. 자율주행 전환은 사용자의 명시적 발화로만 한다."
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
