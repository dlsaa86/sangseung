#!/usr/bin/env bash
# PreToolUse gate on `git commit`.
#
# Why: every batch-mode Unity run ("Clearing [NanumGothic SDF] dynamic font asset data")
# wipes the baked glyph table out of the TMP font assets. The deletion is invisible in a
# normal review — it looks like an ordinary asset touch — but committing it silently
# regresses Korean text rendering, which this repo already paid to fix once
# (commit d8a50d8 "3D 그레이박스 엘리베이터 월드와 한글 폰트 수정").
#
# This bit three times in one session. Each time the fix was the same one-line restore,
# and each time it was caught only because someone happened to read the diffstat.
#
# Rule: block the commit if a staged font asset loses glyph entries.
# The hook never edits anything — it reports and hands back the exact restore command.
#
# Escape hatch: SKIP_FONT_GUARD=1, for a commit that genuinely intends to rebake fonts.
#
# Exit 0 with no output = allow. Exit 0 with JSON = deny.

set -uo pipefail

ROOT="${CLAUDE_PROJECT_DIR:-$(pwd)}"
cd "$ROOT" 2>/dev/null || exit 0

INPUT="$(cat)"
CMD="$(MSG="$INPUT" node -e '
  try {
    const j = JSON.parse(process.env.MSG || "{}");
    process.stdout.write(String((j.tool_input && j.tool_input.command) || ""));
  } catch (e) { process.stdout.write(""); }
')"

case "$CMD" in
  *"git commit"*) ;;
  *) exit 0 ;;
esac
case "$CMD" in
  *--help*|*SKIP_FONT_GUARD=1*) exit 0 ;;
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

# Compare staged content against HEAD. Only SDF font assets matter here.
VICTIMS=""
while IFS= read -r f; do
  [ -n "$f" ] || continue
  case "$f" in
    *SDF*.asset|*"SDF - Fallback.asset") ;;
    *) continue ;;
  esac
  # m_Index lines are one per baked glyph. A net loss means the atlas was cleared.
  REMOVED="$(git diff --cached -U0 -- "$f" | grep -cE '^-[[:space:]]*- m_Index:' || true)"
  ADDED="$(git diff --cached -U0 -- "$f" | grep -cE '^\+[[:space:]]*- m_Index:' || true)"
  if [ "${REMOVED:-0}" -gt "${ADDED:-0}" ]; then
    LOST=$((REMOVED - ADDED))
    VICTIMS="${VICTIMS}
  ${f}  (글리프 ${LOST}개 삭제)"
  fi
done < <(git diff --cached --name-only 2>/dev/null)

[ -z "$VICTIMS" ] && exit 0

deny "폰트 아틀라스가 비워진 채로 커밋되려 한다. 배치 모드 Unity가 동적 폰트 데이터를 초기화한 결과이고, 이대로 커밋하면 한글 렌더링이 깨진다.
${VICTIMS}

되돌리고 다시 커밋할 것:
  git checkout HEAD -- <위 파일들>

폰트를 실제로 다시 굽는 커밋이라면 명령 앞에 SKIP_FONT_GUARD=1 을 붙일 것."
