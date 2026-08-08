# production/goals/

`/goal` 스킬이 쓰는 목표 파일 디렉터리.

- `ACTIVE.md` — 지금 진행 중인 목표. **동시에 하나만.** 이 파일이 있고
  `status: ACTIVE` 일 때만 완주 게이트(`.claude/hooks/goal-completion-gate.sh`)가 돈다.
- `<slug>-<날짜>.md` — 끝났거나 접은 목표의 보관본.
