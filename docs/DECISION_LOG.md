# Decision Log

제품 범위, 핵심 규칙, 기술 계약 또는 승인 결과가 바뀔 때 기록한다.

## 기록 형식

```markdown
## D-YYYYMMDD-NN — 제목

- 상태: Proposed | Accepted | Rejected | Superseded
- 결정일:
- 결정자:
- 관련 문서/파일:
- 결정:
- 이유:
- 대안:
- 영향:
- 후속 작업:
```

---

## D-20260730-01 — 저장소 내 동결 명세 사용

- 상태: Accepted
- 결정일: 2026-07-30
- 결정자: 사용자
- 관련 문서/파일: `docs/`, `CLAUDE.md`
- 결정: Notion을 편집 가능한 기획 원본으로 유지하고, 실제 AI 구현은 저장소의 Markdown 스냅샷을 기준으로 한다.
- 이유: 인증, 문서 길이, 실행 중 변경으로 인한 명세 드리프트를 방지한다.
- 영향: Notion 변경은 자동 적용되지 않으며 검토 후 별도 커밋으로 동기화한다.

## D-20260730-02 — 첫 자율 세션 범위를 Hero Slice로 제한

- 상태: Accepted
- 결정일: 2026-07-30
- 결정자: 사용자
- 관련 문서/파일: `CURRENT_PHASE.md`
- 결정: 첫 장시간 자율 개발은 Phase 1 전체와 Phase 2의 1층 Hero Slice만 대상으로 한다.
- 이유: 전체 10층과 모든 콘텐츠를 동시에 구현하면 기능은 많지만 검증되지 않은 얕은 프로토타입이 될 위험이 높다.
- 영향: Gate A와 Gate B 통과 전 10층 확장과 대규모 비주얼 폴리시를 시작하지 않는다.

## D-20260730-03 — 구현 기준 Unity 버전 고정

- 상태: Accepted
- 결정일: 2026-07-30
- 결정자: 저장소 현행 설정
- 관련 문서/파일: `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`, `Packages/packages-lock.json`
- 결정: Unity 6000.5.5f1과 URP 17.5.0을 사용한다.
- 이유: 현재 프로젝트의 재현 가능한 개발 환경을 유지한다.
- 영향: 에이전트는 승인 없이 Unity 또는 렌더 파이프라인 버전을 변경하지 않는다.

## D-20260730-04 — 자동 룰렛 유지

- 상태: Accepted
- 결정일: 2026-07-30
- 결정자: MASTER PRD
- 관련 문서/파일: `MASTER_PRD.md`
- 결정: 통관별 정지 버튼, 타이밍 정지, 구슬 위치 이동·교환을 1차 프로토타입에서 제외한다.
- 이유: 플레이어 판타지는 반응 조작자가 아니라 위험과 확률 규칙을 설계하는 운영자다.
- 영향: 입력 정밀도보다 계약, 빌드, 정화, 캐스케이드, 과수확 의사결정을 검증한다.

## D-20260730-05 — 씬과 YAML 에셋은 단일 소유

- 상태: Accepted
- 결정일: 2026-07-30
- 결정자: 저장소 운영 규칙
- 관련 문서/파일: `CLAUDE.md`
- 결정: `.unity`, `.prefab`, `.mat`, `.asset`, URP 설정은 동시에 여러 에이전트가 수정하지 않는다.
- 이유: Unity YAML의 fileID 참조가 텍스트 병합 후 조용히 손상될 수 있다.
- 영향: 코드 디렉터리는 배타 소유로 병렬화할 수 있지만 씬과 아트 통합은 순차 처리한다.

## D-20260730-06 — 테스트를 Unity Test Runner(NUnit)로 옮기지 않는다

- 상태: Accepted
- 결정일: 2026-07-30
- 결정자: 구현 에이전트 (`agent/phase1-hero-slice`)
- 관련 문서/파일: `TECH_SPEC.md` §3·§11·§12, `Assets/Editor/AscendTestMenu.cs`
- 결정: EditMode/PlayMode 검증을 NUnit이 아니라 **NUnit에 의존하지 않는 자체 헤드리스 러너**로
  유지한다. 진입점은 `Ascend/Run All EditMode Tests`와 `Ascend/Run PlayMode Hero Slice Check` 두 개다.
- 이유: 이 프로젝트에는 asmdef이 없어 모든 게임 코드가 predefined `Assembly-CSharp`에 있다.
  Unity 규칙상 asmdef 어셈블리는 `Assembly-CSharp`를 **참조할 수 없으므로**, 테스트 어셈블리를
  추가해도 게임 코드를 볼 수 없다. 테스트를 위해 asmdef을 도입하는 것은 `TECH_SPEC.md` §3이
  금지한 "별도 결정 없는 asmdef 도입"에 해당한다.
- 대안: (1) 게임 코드 전체를 asmdef으로 분할 — 이번 Phase 범위 밖의 대규모 구조 변경.
  (2) 테스트를 포기 — Gate A·D가 성립하지 않는다.
- 영향: `CURRENT_PHASE.md`의 "핵심 EditMode 테스트 / 핵심 PlayMode 테스트"는 이 러너로 충족한다.
  Gate A 문구가 "Unity 씬 없이 핵심 판정 테스트가 통과한다"이므로 요구는 그대로 만족된다.
- 후속 작업: asmdef 도입을 별도 안건으로 검토할 때 이 결정을 재평가한다.

## D-20260730-07 — 1층 Hero Slice 층 계획을 10층 커리큘럼과 분리해 신설

- 상태: Accepted
- 결정일: 2026-07-30
- 결정자: 구현 에이전트 (`agent/phase1-hero-slice`)
- 관련 문서/파일: `CURRENT_PHASE.md` §1·§2.2, `Assets/Prototype_Elevator/Scripts/Spin/FloorPlan.cs`
- 결정: `PrototypeCurriculum.HeroSlice`(계약 3종·저항체 2종·5스핀·요구 전력 460)를 신설하고,
  `IFloorPlanSource`로 10층 커리큘럼과 나란히 둔다. 기본 런 모드는 `RunMode.HeroSlice`다.
- 이유: 명세 충돌이 있었다. `CURRENT_PHASE.md`는 1층에서 `계약 선택 → … → 확정 또는 과수확`
  전체 흐름을 요구하지만, 기존 10층 커리큘럼의 1층은 교습 층이라 계약이 없고(6층 첫 등장)
  증식체도 없다(7층 첫 등장). 그 층으로는 이번 Phase의 통과 조건을 **검증조차 할 수 없다.**
  문서 우선순위상 이번 세션 범위는 `CURRENT_PHASE.md`가 정한다.
- 대안: 10층 커리큘럼의 1층을 직접 수정 — Phase 2 이후의 자산인 교습 곡선이 파괴된다. 기각.
- 영향: 10층 커리큘럼은 손대지 않았고, 보존 여부를 테스트(`10층 커리큘럼이 보존됨`)로 고정했다.
- 후속 작업: Phase 2에서 10층으로 확장할 때 Hero Slice를 유지할지 흡수할지 결정한다.

## D-20260730-08 — 실행 레버에서 추가 스핀 기능을 제거하고 과수확 레버를 신설

- 상태: Accepted
- 결정일: 2026-07-30
- 결정자: 구현 에이전트 (`agent/phase1-hero-slice`)
- 관련 문서/파일: `MASTER_PRD.md` §7, `VISUAL_SPEC.md` §7, `.claude/visual-criteria.md` B-4.12
- 결정: 물체 하나가 뜻 하나를 갖는다. 실행 레버 = 계약 확정·일반 스핀, 전력 탱크 = 확정,
  과수확 레버(보호 덮개·잠금등) = 추가 스핀. Decision 단계에서 실행 레버는 비활성이 된다.
- 이유: 기존 구현은 Decision 단계에서 **같은 실행 레버**를 당기면 판돈을 물고 추가 스핀했다.
  그러면 확정과 과수확이 같은 물체의 다른 타이밍이 되어 플레이어가 무엇을 고르는지 모른 채
  당긴다. PRD가 "대표 장면"으로 지정한 선택이 물리적으로 존재하지 않았다.
- 영향: 요구 전력 100% 미만이면 과수확 손잡이 콜라이더가 꺼져 조준 자체가 걸리지 않는다
  (하우징은 조준 가능 — "왜 못 쓰는지"가 프롬프트로 읽혀야 한다).
- 후속 작업: 잠금 해제 순간의 조명·음향 집중 연출은 아직 최소 수준이다. Phase 4에서 강화.
