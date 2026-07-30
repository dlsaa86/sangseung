# 상승 — AI 개발 문서 인덱스

이 디렉터리는 Claude Code, Codex 및 기타 구현 에이전트가 사용하는 **저장소 내 동결 명세**다.

Notion은 계속 수정되는 기획 원본이다. 실제 구현 세션에서는 이 디렉터리의 문서를 기준으로 작업하며, Notion의 변경 사항은 검토 후 별도 커밋으로 동기화한다.

## 문서 우선순위

우선순위는 **두 축**으로 나뉜다. 하나의 일렬 목록으로 합치면 "무엇을 만드는가"와
"어떻게 만들어야 유효한가"가 뒤섞여 매번 잘못된 결론이 나온다.

### 축 1 — 요구사항 ("무엇을 만드는가")

요구사항의 출처가 충돌하면 아래 순서가 우선한다.

1. [`DECISION_LOG.md`](./DECISION_LOG.md) — 확정된 결정. 뒤집으려면 새 결정 항목이 필요하다.
2. [`CURRENT_PHASE.md`](./CURRENT_PHASE.md) — 이번 세션의 허용 범위
3. [`MASTER_PRD.md`](./MASTER_PRD.md) — 제품 범위, 핵심 경험, 완료 조건
4. Notion MASTER PRD와 부록 — 상세 기획·비주얼 원본
5. 기존 코드의 현재 동작

제품 **범위 전체**는 `MASTER_PRD.md`가 정의한다. 단, 이번 세션에서 무엇을 구현할 수
있는지는 `CURRENT_PHASE.md`가 제한한다. `CURRENT_PHASE.md`가 `MASTER_PRD.md`보다
앞에 오는 것은 범위를 **좁히는 방향으로만** 유효하다 — 세션 범위 문서가 제품 범위를
넓히려면 `DECISION_LOG.md` 항목이 있어야 한다.

### 축 2 — 기술·시각 제약 ("어떻게 만들어야 유효한가")

아래 둘은 축 1의 어느 단계에도 종속되지 않는 **직교 제약**이다. 요구사항이 어디서
왔든 이 조건을 만족해야 완료로 인정한다.

- [`TECH_SPEC.md`](./TECH_SPEC.md) — Unity 구조, 상태 모델, 테스트 및 성능 기준
- [`VISUAL_SPEC.md`](./VISUAL_SPEC.md) — 비주얼 방향과 시각 검증 기준
  (상세 루브릭: `.claude/visual-criteria.md`)

- 축 1의 문서가 이 둘보다 **더 구체적이고 더 엄격하면** 그쪽을 따르고, 내용을
  `TECH_SPEC.md`/`VISUAL_SPEC.md` 또는 전용 설계 문서로 동결한다.
- 축 1의 문서가 이 둘의 제약을 **완화하는** 것으로 읽히면 `TECH_SPEC.md`/`VISUAL_SPEC.md`가
  우선한다. 완화는 `DECISION_LOG.md` 항목 없이 성립하지 않는다.

### 그 외

- [`AUTONOMOUS_PROTOTYPE_GOAL.md`](./AUTONOMOUS_PROTOTYPE_GOAL.md) — 장기 자율 실행의
  작업 명세와 완료 게이트. 범위는 `CURRENT_PHASE.md`에 반영해 사용한다.
- [`ASSUMPTION_LOG.md`](./ASSUMPTION_LOG.md) — 임시 기본값과 교체 위치
- `Assets/Plans/` — 세부 작업 티켓
- [`handoff/`](./handoff/) — 기기 이전·세션 인수인계
- `runtime/` — 세션 산출물(감사·계획·진행 로그). 명세가 아니다.
- 아카이브 및 폐기된 초기 아이디어 — 구현 근거로 사용하지 않음

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
