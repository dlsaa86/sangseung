# Implementation Plan — Phase 1 + 1층 Hero Slice

> 작성: 2026-07-30 / 브랜치 `agent/phase1-hero-slice`
> 입력: `docs/runtime/GapAnalysis.md`
> 진행 기록: `docs/runtime/ProgressLog.md`

각 작업 묶음은 **변경 → 컴파일 → 자동 테스트 → 실제 플레이 → 캡처 → 평가 → 수정**을 한 바퀴
돌고 나서만 다음으로 넘어간다. 커밋 단위 = 작업 묶음 단위다.

---

# 0. 소유권과 안전 규칙

이 세션은 **단일 에이전트**가 순차로 진행한다. 따라서 `CLAUDE.md`의 배타 소유 규칙은
자동으로 만족되지만, 씬 손상 위험은 그대로 남으므로 아래를 지킨다.

- 씬(`.unity`)은 **에디터를 통해서만** 수정한다. YAML 직접 편집 금지.
- 씬을 바꾸는 작업 묶음은 시작 전에 직전 커밋이 깨끗한지 확인한다(손상 시 복구 지점).
- 씬 수정은 항상 `EditorSceneManager.MarkSceneDirty` → `SaveScene` 명시 호출.
- 스크립트 수정 후 Play 모드 진입 전에 컴파일 완료를 확인한다(asmdef이 없어 전체 재컴파일).
- Play 모드 진입은 도메인 리로드로 느리므로 폴링하지 않는다.

---

# 1. WP-A — Gate A 코어 정합 (G-01, G-02, G-03)

**목표**: 명세와 코드의 수치·규칙 불일치 제거. 화면 변경 없음 → `visual-verify` 쓰지 않는다.

| # | 변경 | 파일 |
|---|---|---|
| A-1 | `MaxCascadeDepth` 기본값 8 → **20** | `Spin/SpinRuleSet.cs` |
| A-2 | `SpinResolution.CascadeCapReached` 필드 + 캡 도달 시 진단 문자열 | `Spin/SpinResolution.cs`, `Spin/SpinEngine.cs` |
| A-3 | 시드 파생 단일 출처 `SpinSeed.Derive(runSeed, floor, spinIndex)` | **신규** `Spin/SpinSeed.cs` |
| A-4 | `FloorSession`이 층·스핀 인덱스로 시드를 파생해 `SpinWithSeed` 호출 | `Run/FloorSession.cs` |
| A-5 | `SpinResolution`에 `RunSeed` / `Floor` / `SpinIndex` 기록 + `ToLogLine()` | `Spin/SpinResolution.cs` |

**검증**
- `Ascend/Run Spin Tests` 기존 10종 유지 통과(회귀).
- 신규: 캡 도달 시 `CascadeCapReached == true` 이고 `Steps.Length == 20`.
- 신규: `SpinSeed.Derive`가 (런시드, 층, 스핀인덱스) 3튜플에 대해 안정·비충돌.
- 신규: 같은 (런시드, 층, 스핀인덱스) → 같은 보드. 층만 달라도 보드가 달라짐.

**완료 판정**: Gate A의 세 조건(씬 없이 통과 / 동일 시드 재현 / 20회 미초과)이 테스트로 증명됨.

---

# 2. WP-B — Hero Slice 층 계획 (G-04)

**목표**: 1층에서 계약 선택이 실제로 존재하게 만든다. 10층 커리큘럼은 보존한다.

| # | 변경 | 파일 |
|---|---|---|
| B-1 | `PrototypeCurriculum.HeroSlice` 신설 — 1층, 계약 3종(없음/흡수체/증식체), 스핀 5 | `Spin/FloorPlan.cs` |
| B-2 | `RunSessionBehaviour._mode` (HeroSlice / TenFloor) — 기본 HeroSlice | `Run/RunSessionBehaviour.cs` |
| B-3 | `RunSession`이 층 계획 공급자를 주입받도록 확장(기존 10층 경로 기본값 유지) | `Run/RunSession.cs` |

**요구 전력 산정 근거**: Hero Slice는 "5스핀 안에 요구 전력을 넘기되, 넘긴 뒤에도
스핀이 남아 과수확 선택이 실제 고민이 되는" 지점이어야 한다. 시뮬레이터로 100런 이상
돌려 **3~4스핀째 달성률**을 보고 값을 정한다. 임시값을 근거 없이 박지 않는다.

**검증**: 헤드리스 100런 — 요구 전력 달성률, 달성 시점 스핀 인덱스 분포, 5스핀 소진 실패율.

**완료 판정**: 1층 진입 시 `Phase == ContractSelection`, 계약 미선택 상태에서 `Spin()`이 거부됨.

---

# 3. WP-C — 실행 레버 / 과수확 레버 분리 (G-05)

**목표**: `MASTER_PRD.md` §7의 대표 장면을 물리적으로 존재하게 만든다.

| # | 변경 | 파일 |
|---|---|---|
| C-1 | `InteractableOverharvestLever` — 잠금 상태와 덮개를 스스로 표현 | **신규** `Player/InteractableOverharvestLever.cs` |
| C-2 | 씬: 콘솔 반대편에 대형 레버 + 보호 덮개 + 잠금 표시등 배치 | 씬 (에디터 스크립트) |
| C-3 | `RouletteInteractionBridge` 역할 재배분 | `Run/RouletteInteractionBridge.cs` |
| C-4 | 실행 레버에서 "추가 스핀" 기능 제거 | `Run/RouletteInteractionBridge.cs` |

**역할 배분 (확정)**

| 오브젝트 | 역할 | 활성 조건 |
|---|---|---|
| 계약 패널 | 계약 미리보기 순환 (확정 아님) | `ContractSelection` |
| 실행 레버 | 계약 확정 / 일반 스핀 | `ContractSelection` 또는 `Spinning` |
| 전력 탱크 | **확정** — 층 종료 | `Decision` && `CanBank` |
| 과수확 레버 | **추가 스핀** — 판돈 지불 | `Decision` && `CanBank` && 스핀 잔여 |

핵심: 확정과 과수확이 **서로 다른 두 물체**가 되고, 둘 다 `Decision`에서만 살아난다.
`visual-criteria.md` B-4.12(두 선택의 시각적 대등함)를 만족시키기 위한 구조다.

**잠금 규칙**: 요구 전력 100% 미만이면 덮개가 닫혀 있고 콜라이더가 비활성. 100% 달성 순간에
덮개가 열리는 것이 **사건으로** 보여야 한다(조명·소리 집중). `VISUAL_SPEC.md` §7.

**검증**: PlayMode — 달성 전 과수확 레버 `CanInteract == false`, 달성 즉시 `true`.
캡처: "과수확 레버 접근 순간".

---

# 4. WP-D — 계기판 구동 (G-07)

**목표**: 보드를 안 보고 있어도 전력·요구·위험이 읽힌다 (`visual-criteria.md` B-3.8).

| # | 변경 | 파일 |
|---|---|---|
| D-1 | `InstrumentPanelView` — `RunSession`을 읽어 층·전력·요구·무게·잔류 구동 | **신규** `View/InstrumentPanelView.cs` |
| D-2 | 전력 게이지에 임계점 눈금(100/130/170/220/300%) 표시 | 씬 + D-1 |
| D-3 | `ContractPlaque_0..2` 구동 — 출현률↑·보상↑·대가↑ **3요소 동시** 표시 | **신규** `View/ContractPlaqueView.cs` |
| D-4 | 씬에서 `ElevatorGrayboxView` 비활성화(삭제 아님) | 씬 |

**주의**: `ElevatorGrayboxView`는 폐기 설계(`RunController`)에 묶여 있다. 고치지 않고
비활성화만 한다 — 재작성 금지 원칙과 컴파일 안전 양쪽을 지키는 최소 조치다.

**검증**: 캡처 — 룰렛에 등을 돌린 위치에서도 전력 상태가 읽히는지.

---

# 5. WP-E — 연출 순서 / 캐스케이드 재생 (G-08)

**목표**: `MASTER_PRD.md` §6.1 판독 순서를 시간축에 올린다.

| # | 변경 | 파일 |
|---|---|---|
| E-1 | `SpinPresenter` — 확정된 `SpinResolution`을 단계별로 재생 | **신규** `View/SpinPresenter.cs` |
| E-2 | `SpinBoardView`에 단계 보드 표시 + 정화 칸 하이라이트 API | `View/SpinBoardView.cs` |
| E-3 | 하이라이트를 패턴별로 구분(개수 / 직선 / 연결) | E-1, E-2 |
| E-4 | 재생 중 입력 잠금 (중복 입력 차단) | `Run/RouletteInteractionBridge.cs` |

**불변식**: 프리젠터는 **판정을 다시 하지 않는다.** `SpinResolution`은 읽기 전용 입력이다
(`TECH_SPEC.md` §5). 연출을 전부 꺼도 판정 테스트가 통과해야 한다(§2 기술 원칙).

**연출 타이밍은 승인 대기 항목**이므로 전부 데이터 필드로 두고 프리셋 2종(빠름/느림)을 만든다.

**검증**: PlayMode — 재생 중 레버 재입력이 상태를 손상시키지 않음.
캡처: "5연쇄 이상" (해당 시드를 테스트로 고정).

---

# 6. WP-F — 위험 상태 (G-06)

**목표**: Gate C. Stable과 Critical이 무음 캡처에서 구분된다.

| # | 변경 | 파일 |
|---|---|---|
| F-1 | `RiskLevel` enum (Stable / Warning / Critical / Collapse) | **신규** `Core/Risk/RiskLevel.cs` |
| F-2 | `RiskEvaluator` — 순수 C#. 입력: 전력비·잔류 저항 수·과수확 횟수·과적 | **신규** `Core/Risk/RiskEvaluator.cs` |
| F-3 | `RiskProfile` — 조명·오디오·진동·파티클 강도 프리셋 (교체 가능) | **신규** `Core/Risk/RiskProfile.cs` |
| F-4 | `RiskStateView` — 조명·앰비언트·차체 진동·경고등 구동 | **신규** `View/RiskStateView.cs` |
| F-5 | 오디오: 위험 단계별 앰비언트 톤 (절차 생성, 외부 에셋 없음) | F-4 |

**히스테리시스 필수**: 진입·이탈 임계값을 분리한다(`TECH_SPEC.md` §6.4). 경계에서
단계가 떨리면 판독성이 무너진다.

**금지**: 무작위 전체 화면 흔들림, 과도한 섬광 (`VISUAL_SPEC.md` §6 Critical).
**승인 대기**: 공포 표현 강도 — 프리셋 2~3종으로 유지.

**검증**: 동일 카메라·해상도·FOV로 Stable / Critical 캡처 후 무음 비교.

---

# 7. WP-G — 사고 기록기 · 디버그 패널 (G-09, G-10)

| # | 변경 | 파일 |
|---|---|---|
| G-1 | `FloorRecord` / `AccidentRecorder` — 순수 C# | **신규** `Core/Telemetry/AccidentRecorder.cs` |
| G-2 | 층 종료 시 요약을 화면에 표시 | `View/` |
| G-3 | 디버그 패널 — 시드 입력·재시작·상태 전이·현재 위험 단계 | **신규** `UI/DebugPanel.cs` |
| G-4 | 고정 시드 3개 선정 및 문서화 | `ProgressLog.md` |

**기록 항목** (`MASTER_PRD.md` §10 중 이번 Phase 해당분): 런 시드·층·계약·초기 보드·
각 캐스케이드 단계·정화/패턴 목록·획득/요구 전력·잔류·위험 변화·과수확 여부·결과 원인.

---

# 8. WP-H — 테스트 보강 (G-11, G-12)

| # | 변경 | 파일 |
|---|---|---|
| H-1 | EditMode 9종 추가 (GapAnalysis G-11 표) | `Spin/Tests/SpinEngineTests.cs` |
| H-2 | 층·런 흐름 테스트 (요구 전력 달성 판정, 계약 게이트, 과적) | **신규** `Run/Tests/FloorSessionTests.cs` |
| H-3 | 위험 상태 계산 테스트(히스테리시스 포함) | **신규** `Core/Risk/Tests/RiskEvaluatorTests.cs` |
| H-4 | PlayMode 하네스 — 새 루프용 | **신규** `Assets/Editor/HeroSlicePlayModeTest.cs` |
| H-5 | 통합 러너 메뉴 `Ascend/Run All Tests` | `Assets/Editor/` |

**PlayMode 검증 항목** (`TECH_SPEC.md` §12 중 Phase 해당분):
레버 입력 후 스핀 완료 / 스핀 중 중복 입력 차단 / 계약 미선택 시 레버 비활성 /
결과 공개 후 입력 복구 / 요구 전력 달성 후 확정 UI 활성 / 과수확 레버 잠금·해제 /
위험 상태와 조명·UI 동기화 / 사고 기록기 출력.

**금지**: 실패한 테스트를 삭제·완화·조건부 스킵으로 통과시키지 않는다.

---

# 9. WP-I — 캡처 · 성능 · 보고

| # | 작업 |
|---|---|
| I-1 | 고정 캡처 세트: 내부 전경 / 룰렛 정면 / 계약 선택 / Stable / Critical / 과수확 접근 / 5연쇄 |
| I-2 | 캡처 조건 고정: 1920×1080, 고정 FOV·카메라 위치·품질 프리셋·시드 |
| I-3 | 시각 평가 — `visual-criteria.md` 항목별. 구현자 자기 평가 금지 → 별도 평가 단계 |
| I-4 | 성능: 프레임타임·GC Alloc·스핀/캐스케이드 중 스파이크 측정 |
| I-5 | Definition of Done 대조 보고 |

**성능 측정의 한계를 먼저 명시한다**: 개발 기기는 Apple M5 / Metal / macOS다.
`TECH_SPEC.md` §13의 기준 PC(Ryzen 7 5700 / RTX 3070 / Windows)가 아니다.
따라서 **성능 완료를 선언하지 않는다**(§13 "미지정 상태에서는 성능 완료를 선언하지 않는다").
측정값은 참고치로만 기록한다.

**Windows 빌드**: macOS 개발 기기에서는 Windows Build Support 모듈 없이 산출 불가.
빌드 차단 원인으로 보고한다.

---

# 10. 위험과 대응

| 위험 | 징후 | 대응 |
|---|---|---|
| 씬 YAML 손상 | 참조 끊김, 런타임 NRE | 작업 묶음마다 커밋. 손상 시 직전 커밋으로 복구 |
| 에디터 GPU 크래시 (`KNOWN_ISSUES` A-4) | 에디터 사망 | 커밋 간격을 짧게. 씬 저장을 미루지 않음 |
| 도메인 리로드로 Play 모드 검증 지연 | 수십 초 대기 | 폴링 금지, 헤드리스 테스트를 1차 방어선으로 |
| 같은 오류 3회 반복 | — | 최소 재현 테스트로 전환하거나 인터페이스 뒤로 격리 |
| 폐기 코드가 컴파일을 깨뜨림 | 컴파일 오류 | 폐기 코드를 건드리지 않는다. 참조만 끊는다 |

# 11. 순서 확정

`WP-A → WP-B → WP-C → WP-D → WP-E → WP-F → WP-G → WP-H → WP-I`

Gate 대응: A(Gate A) / B·C·D·E(Gate B) / F(Gate C) / G·H·I(Gate D).
`CURRENT_PHASE.md` §6에 따라 **Gate A 통과 전 비주얼 폴리시를 시작하지 않고,
Gate B 통과 전 10층 확장을 시작하지 않는다.**

---

# Phase 2 실행 계획 (2026-07-31)

> 기준: `AUTONOMOUS_PROTOTYPE_GOAL.md` (`P2-Gate A~H`), `CURRENT_PHASE.md`
> 감사: `GapAnalysis.md` Phase 2 절 · `VisualGapAnalysis.md` · `NotionSyncReport.md`

## 0. 원칙

1. **이미 도는 것을 다시 쓰지 않는다.** Phase 1 코어는 54개 테스트가 지킨다.
2. **씬과 직렬화 에셋은 한 명만 순차로.** 병렬 에이전트는 문서와 읽기 전용 감사에만.
3. **씬은 손으로 편집하지 않고 멱등 빌더로.** `.unity`는 fileID 상호 참조라 손 편집이
   조용히 깨지고, 좌표가 코드에 있어야 "왜 여기인가"를 설명할 수 있다.
4. **테스트를 약화시키지 않는다.** 같은 방식으로 세 번 고치지 않고 계측으로 전환한다.

## 1. 순서와 근거

| 단계 | 내용 | 상태 |
|---|---|---|
| 1 | 범위 승격 절차 (`CURRENT_PHASE`·`DECISION_LOG`·`README`) | 완료 |
| 2 | 감사 3인 병렬 (적재 / 하네스 / Notion) | 완료 |
| 3 | Gate B — 다층 상승 클램프, 종료 조건 | 완료 |
| 4 | Gate C — `Scripts/Build/` 신설, `SpinRuleSet` 주입 | 완료 |
| 5 | 씬 통합 — 모드·승객 배치·층수 표시·문 손잡이 | 완료 |
| 6 | Gate E — 비례·앞벽·팔레트·폐기 조작부 제거·계기판 | 부분 완료 |
| 7 | Gate D — `BuildTests` 26개, `TenFloorAutoPilot` | 완료 |
| 8 | Gate F·G — 캡처 세트, 성능 측정, 승객 반응 | 진행 중 |
| 9 | Gate H — 독립 재검토 | 예정 |

### 왜 이 순서인가

**Gate B를 먼저 한 이유**: 10층이 실제로 돌지 않으면 그 위에 얹는 모든 것(적재·위험·
캡처)이 검증 불가능한 상태로 쌓인다. 그리고 실제로 헤드리스 5시드 실측에서 층 건너뛰기와
도달 층 13이라는 결함이 즉시 나왔다.

**Gate C가 재작성이 아닌 이유**: `SpinRuleSet`이 이미 승객용으로 설계돼 있었다.
주석이 `MinimumCountToPurify`를 "승객이 특정 저항만 2로 낮출 수 있다"로,
`ResidualMitigation`을 "잔류 완화형 승객이 낮춘다"로 예약했고 발동 순서까지 적혀 있었다.
주입 지점 하나(`FloorSession.BuildRules`)만 이으면 됐다.

**비주얼을 캡처 이후에 고친 이유**: 비례가 잘못됐다는 사실은 숫자가 아니라 화면에서
나왔다. 폭 3.20 × 높이 2.50은 "좁고 높은"의 정반대인데, 씬 계층 덤프만 봐서는
그것이 문제인지 알기 어렵다. 캡처가 먼저였다.

**조명을 건드리지 않은 이유**: `RiskStateView`가 Stable/Warning/Critical을 조명으로
표현한다. Gate F를 검증하기 전에 기본값을 바꾸면 무엇이 깨졌는지 알 수 없다.

## 2. 하지 않기로 한 것

| 안 한 것 | 이유 |
|---|---|
| 레거시 `Effects/` 삭제 | `PrototypeSelfTest`·`BalanceProbe`가 물고 있다. 지우면 테스트가 깨진다 |
| Notion 승객 4종 전면 채택 | Notion은 우선순위 4순위. 저장소 카탈로그가 이미 `MASTER_PRD.md` §8을 만족. 겹치지 않는 2종만 동결 |
| Notion 밸런스 수치 채택 | 현재 값은 400시드 측정 결과(`A-20260730-07`). 교체하면 이번 세션 측정이 전부 무효 |
| 조명·셰이더 교체 | 위 참조 |
| asmdef 도입 | `TECH_SPEC.md` §3, `D-20260730-06` |
| 잉여 배분을 플레이어 선택으로 | 확정/과수확의 대등함이 흐려진다. `D-20260731-03` 후속 |

## 3. 검증 전략

**헤드리스로는 Gate B를 증명할 수 없다.** 씬 배선·상호작용 게이트·연출 잠금을 전부
건너뛰기 때문이고, 이번에 깨지기 쉬웠던 곳이 정확히 그쪽이었다.
그래서 `TenFloorAutoPilot`은 `IInteractable.Interact()`만 호출한다 —
`CrosshairInteractor`가 클릭 시 하는 것과 같은 경로다.

**한 번의 런으로는 부족하다.** 과수확 판돈이 런을 흔들어 계약이 처음 나오는 6층
전에 사고가 날 수 있다. 실제로 그렇게 됐고 "계약 단계를 거쳤다"가 도달 불가능한
검사가 되어 실패했다. 검사를 낮추는 대신 **같은 시드로 두 정책**을 돈다:
보수(과수확 없음)가 완주와 계약을, 공격(과수확 1회)이 대표 선택과 사고 경로를 증명한다.

## 4. 남은 차단 요인

1. 필수 캡처 세트 (Gate G) — `TenFloorCaptureRig` 작성 완료, 실행 대기
2. 승객 불안 반응 (Gate F) — 승객이 이번에 처음 씬에 생겨서 아직 없다
3. 심볼 3항목 차이 (Gate E §4) — 현재 실루엣 하나뿐
4. 성능·GC 측정 (Gate G)
5. 독립 재검토 (Gate H)
