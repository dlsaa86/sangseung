# 상승 — AI 개발 문서 인덱스

이 디렉터리는 Claude Code, Codex 및 기타 구현 에이전트가 사용하는 **저장소 내 동결 명세**다.

Notion은 계속 수정되는 기획 원본이다. 실제 구현 세션에서는 이 디렉터리의 문서를 기준으로 작업하며, Notion의 변경 사항은 검토 후 별도 커밋으로 동기화한다.

## 문서 우선순위

충돌이 발생하면 아래 순서가 우선한다.

1. [`MASTER_PRD.md`](./MASTER_PRD.md) — 제품 범위, 핵심 경험, 완료 조건
2. [`TECH_SPEC.md`](./TECH_SPEC.md) — Unity 구조, 상태 모델, 테스트 및 성능 기준
3. [`CURRENT_PHASE.md`](./CURRENT_PHASE.md) — 이번 개발 세션의 허용 범위
4. [`VISUAL_SPEC.md`](./VISUAL_SPEC.md) — 비주얼 방향과 시각 검증 기준
5. [`DECISION_LOG.md`](./DECISION_LOG.md) — 확정된 결정과 변경 이력
6. [`ASSUMPTION_LOG.md`](./ASSUMPTION_LOG.md) — 에이전트가 사용한 임시 기본값과 교체 위치
7. `Assets/Plans/` — 세부 작업 티켓
8. [`handoff/`](./handoff/) — 기기 이전·세션 인수인계
9. 아카이브 및 폐기된 초기 아이디어 — 구현 근거로 사용하지 않음

제품 범위는 `CURRENT_PHASE.md`가 아니라 `MASTER_PRD.md`가 정의한다. 단, 현재 세션에서 무엇을 구현할 수 있는지는 `CURRENT_PHASE.md`가 제한한다.

## 원본

- MASTER PRD Notion 원본: https://app.notion.com/p/3ada30cad9c58106b9a8c4ee03dd995c
- 기술 부록 Notion 원본: https://app.notion.com/p/3ada30cad9c58160a5f8ce347273d843
- 저장소 스냅샷 작성일: 2026-07-30

## 동기화 규칙

- Notion이 변경되었다는 이유만으로 구현 중인 명세가 자동 변경되지는 않는다.
- 제품 범위, 핵심 규칙, 데이터 계약 또는 완료 조건을 바꾸는 경우 `MASTER_PRD.md` 또는 `TECH_SPEC.md`를 수정하고 `DECISION_LOG.md`에 기록한다.
- 수치 밸런스, 캐릭터 최종 외형, 최종 모션, 최종 색·재질, 공포 강도는 사용자 승인 전까지 교체 가능한 데이터 또는 프리셋으로 유지한다.
- 불명확한 세부사항은 작업을 중단하는 이유가 아니다. 안전한 기본값을 사용하고 `ASSUMPTION_LOG.md`에 기록한다.

## 에이전트 시작 순서

1. 저장소의 `CLAUDE.md`를 읽는다.
2. 이 문서의 우선순위대로 명세를 읽는다.
3. 현재 코드와 에셋을 요구사항에 매핑한다.
4. 누락·충돌·기술 부채를 `docs/runtime/GapAnalysis.md`에 기록한다.
5. 실행 계획을 `docs/runtime/ImplementationPlan.md`에 기록한다.
6. `CURRENT_PHASE.md`에 명시된 범위만 구현한다.
7. 각 통과 조건마다 컴파일, 자동 테스트, 실제 플레이, 캡처, 시각 평가, 성능 측정을 수행한다.
