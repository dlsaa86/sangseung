#!/usr/bin/env bash
# SessionStart notice: 탑다운 Stop 게이트가 꺼져 있으면 매 세션 시작 때 알린다.
#
# Why: SKIP_TOPDOWN_GATE 는 "이번 세션만" 끄려고 넣는 값인데, 실제로는
# .claude/settings.local.json 의 env 에 남아 다음 세션까지 조용히 따라온다.
# 꺼진 게이트는 켜진 게이트보다 위험하다 — 아무도 막히지 않으니 아무도 눈치채지 못한다.
# 그래서 끄는 것 자체는 허용하되, 꺼져 있다는 사실은 매번 눈에 띄게 만든다.
#
# 출력이 없으면 게이트가 살아 있다는 뜻이다.

set -uo pipefail

ROOT="${CLAUDE_PROJECT_DIR:-$(pwd)}"

if [ "${SKIP_TOPDOWN_GATE:-}" = "1" ]; then
  cat <<'EOF'
[탑다운 게이트 꺼짐] SKIP_TOPDOWN_GATE=1 이 설정되어 있다.
  Stop hook 이 완료 여부를 검사하지 않는다. 자율 개발을 시작하기 전에
  .claude/settings.local.json 의 env 에서 SKIP_TOPDOWN_GATE 줄을 지울 것.
  현재 상태 확인: powershell -NoProfile -File tools/verify-topdown.ps1 -Stats
EOF
  exit 0
fi

# 게이트는 켜져 있다. 백로그가 없으면 그 사실만 알린다 — 게이트가 헛돌기 때문이다.
if [ ! -f "$ROOT/docs/TOPDOWN_MASTER_BACKLOG.md" ]; then
  echo "[탑다운 게이트] docs/TOPDOWN_MASTER_BACKLOG.md 가 없다. Stop hook 이 항상 실패한다."
fi

exit 0
