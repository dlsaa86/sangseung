# Gap Analysis — Phase 1 + 1층 Hero Slice

> 작성: 2026-07-30 / 브랜치 `agent/phase1-hero-slice`
> 기준 문서: `MASTER_PRD.md` > `TECH_SPEC.md` > `CURRENT_PHASE.md` > `VISUAL_SPEC.md`
> 기준 커밋: `c0dac27`

이 문서는 **현재 저장소가 무엇을 이미 하고 있고, `CURRENT_PHASE.md`의 통과 조건 대비
무엇이 비어 있는가**를 매핑한다. "구현되지 않음"과 "구현됐지만 연결되지 않음"을 구분한다.
후자가 이 저장소의 지배적인 상태다.

---

# 0. 감사 방법

- Unity 에디터 실시간 조사(MCP `Unity_RunCommand`)로 씬 하이라키·컴포넌트·활성 상태를 덤프.
- `Assets/**/*.cs` 전량 목록화 후 신규 설계(`Spin/`, `Run/`, `View/`, `Player/`)와
  폐기 설계(`Core/`, `Roulette/`, `Effects/`, `Data/Ball*`, `Sim/`, `UI/PrototypeUI`)로 분류.
- 기준선 확인: `Ascend/Run Spin Tests` → **10 PASS / 0 FAIL**, 컴파일 오류 없음.

## 0.1 감사 시작 시점의 환경 사고

**에디터가 이름 없는 빈 씬에서 Play 모드로 방치되어 있었다.** `KNOWN_ISSUES.md` A-2와
동일한 상황이다. 이 상태에서는 스크립트 변경이 반영되지 않고, 조사 결과가 전부 거짓이 된다.
Play 모드를 종료하고 `Prototype_Elevator.unity`를 연 뒤에 감사를 시작했다.

**작업 트리가 깨끗하지 않았다.** `Packages/manifest.json`과 `packages-lock.json`이
`com.unity.ai.assistant` 2.16.0-pre.1 → 2.17.0-pre.1로 수정된 상태였다. 이 변경은
에디터가 자체 수행한 것이고 에이전트가 만든 것이 아니다. 되돌리면 현재 실행 중인
에디터(및 MCP 경로)가 깨질 수 있어 그대로 가져간다. → `ASSUMPTION_LOG` A-20260730-05.

**기준 하드웨어가 문서의 가정과 다르다.** `ASSUMPTION_LOG` A-20260730-01은
Ryzen 7 5700 / RTX 3070 / Windows를 가정하지만, 실제 개발 기기는
**Apple M5 / Metal / macOS 26.5.2 / 24GB**다. 이 세션의 모든 성능 수치와 캡처
베이스라인은 Windows 기준값으로 쓸 수 없다. → `ASSUMPTION_LOG` A-20260730-06.

---

# 1. 요약 — Gate별 현재 위치

| Gate | 상태 | 한 줄 요약 |
|---|---|---|
| A — 코어 | **거의 통과** | 판정·캐스케이드·결정론이 전부 있다. 하드 캡 값과 시드 파생 규칙만 명세와 어긋난다. |
| B — 플레이 흐름 | **부분** | 흐름은 키보드 IMGUI로만 완주 가능. 1인칭 오브젝트 경로는 과수확 레버가 없어 끊긴다. |
| C — 판독성·위험 | **미구현** | 위험 상태 시스템 자체가 없다. 계기판은 폐기된 컨트롤러를 참조해 죽어 있다. |
| D — 기술 증거 | **부분** | 헤드리스 테스트 10종은 있다. PlayMode 검증·캡처·성능 측정·고정 시드 기록이 없다. |

핵심 판단: **이 저장소는 "코어가 없는" 상태가 아니라 "코어가 화면과 연결되지 않은" 상태다.**
따라서 이번 세션의 대부분은 신규 판정 로직 작성이 아니라 **연결·연출·상태 표현**이어야 한다.
`SpinEngine`을 재작성할 근거는 발견되지 않았다.

---

# 2. 이미 구현되어 정상 동작하는 것 (재작성 금지)

`MASTER_PRD.md` §15.2 "이미 정상 동작하는 시스템을 이유 없이 재작성하지 않는다"에 따라
아래는 **유지**한다.

| 영역 | 파일 | 검증 근거 |
|---|---|---|
| 3×3 보드 좌표계·직선 8종·직교/대각 인접 | `Spin/SpinBoard.cs` | 테스트 2·3·4 통과 |
| 심볼 3종 정의 | `Spin/SymbolKind.cs` | — |
| 개수 정화(3개 이상)·직선·연결 4+·풀보드 판정 | `Spin/SpinEngine.FindMatches` | 테스트 1·2·3·5·6 통과 |
| 제거 → 빈칸 → 재충전 → 재판정 캐스케이드 | `Spin/SpinEngine.ResolveBoardInternal` | 테스트 3·9·10 통과 |
| 시드 재현 | `SpinEngine.SpinWithSeed` | 테스트 7 통과 |
| 흡수체·증식체 잔류 | `SpinEngine.BuildResidual` | 테스트 8 통과 |
| 계약 3요소 동시 이동 | `Spin/ResistanceContract.cs`, `SpinRuleSet.Apply` | 코드 검토 |
| 전력 임계점 8구간 | `Spin/PowerThresholds.cs` | 코드 검토 |
| 층·런 상태 기계 (순수 C#) | `Run/FloorSession.cs`, `Run/RunSession.cs` | 코드 검토 |
| 1인칭 이동·조준·상호작용 | `Player/*` | 씬에 배선 확인됨 |
| 3×3 결과판 공간 표시 | `View/SpinBoardView.cs` + 씬 `Tube_0..2/Cell_0..2` | 씬 배선 확인됨 |
| 그레이박스 엘리베이터 지오메트리 | 씬 `GrayboxWorld/Car` | 씬 배선 확인됨 |

---

# 3. 갭 — 우선순위 순

## G-01 (BLOCKER, Gate A) 캐스케이드 하드 캡이 명세의 20이 아니라 8이다

- 명세: `MASTER_PRD.md` §6 "캐스케이드 하드 캡은 20회다", `TECH_SPEC.md` §9,
  `CURRENT_PHASE.md` §2.1.
- 현재: `SpinRuleSet.MaxCascadeDepth = 8`.
- 영향: 명세 위반. 20까지 도달하는 판을 한 번도 검증하지 못한다.
- 조치: 기본값 20. 8은 연출 상한이 아니라 **다른 개념**이므로 값만 올린다.

## G-02 (BLOCKER, Gate A) 하드 캡 도달이 조용히 끝난다

- 명세: `MASTER_PRD.md` §6 "캡 도달 시 오류로 멈추지 말고 **명확한 로그를 남긴 뒤** 정상 종료",
  `TECH_SPEC.md` §9 "캡 도달 시 시드, 초기 보드, 마지막 보드, 단계별 발동을 로그에 남긴다".
- 현재: `for (depth = 1; depth <= maxDepth; ...)`가 그냥 빠져나온다. `SpinResolution`에
  "캡에 걸렸다"를 나타내는 필드가 없어 UI·로그·테스트 어느 쪽도 구분할 수 없다.
- 영향: 무한 루프 방지가 **동작하는지 증명할 수 없다.** 완료 조건 "캐스케이드 무한 루프 없음"을
  증거로 뒷받침하지 못한다.
- 조치: `SpinResolution.CascadeCapReached` 추가 + 도달 시 경고 로그 + 전용 테스트.

## G-03 (HIGH, Gate A) 시드 파생 규칙이 명세와 다르다

- 명세: `TECH_SPEC.md` §7 "각 층과 스핀은 `RunSeed`, 층 번호, 스핀 인덱스에서 파생한 시드를
  사용한다. 시드 파생 규칙은 한 곳에 정의한다."
- 현재: `SpinEngine`이 런 시드로 만든 `System.Random`에서 스핀 시드를 **순차로** 뽑는다
  (`NextSpinSeed`). 결과적으로 결정론적이긴 하지만, 호출 **순서**에 의존한다.
- 영향: "3층 2번째 스핀"을 단독으로 재현할 수 없다. 앞선 층에서 스핀을 한 번만 더/덜 해도
  이후 모든 시드가 밀린다. 고정 시드 3개로 캡처를 재현해야 하는 이번 세션 요구와 충돌한다.
- 조치: `SpinSeed.Derive(runSeed, floor, spinIndex)` 단일 출처 신설, `FloorSession`이
  층·스핀 인덱스를 넘겨 호출.

## G-04 (BLOCKER, Gate B) 1층에 계약 선택이 존재하지 않는다

- 명세 충돌:
  - `CURRENT_PHASE.md` §1·§2.2: 1층 Hero Slice는 `계약 선택 → 실행 레버 → …` 흐름을
    개발자 개입 없이 경험할 수 있어야 하고, "계약 선택 인터페이스"가 구현 대상이다.
  - `Spin/FloorPlan.cs`의 `PrototypeCurriculum`: 1층은 교습 층이라
    `ContractChoices = Array.Empty<>()`. 계약은 6층에 처음 등장한다.
- 현재: `FloorSession` 생성자가 선택지 0개를 보고 `Phase`를 곧장 `Spinning`으로 넘긴다.
  Gate B의 "계약 미선택 상태에서는 스핀할 수 없다"를 **1층에서는 검증조차 할 수 없다.**
- 판단: 문서 우선순위상 `CURRENT_PHASE.md`가 이번 세션 범위를 정의하므로 Hero Slice가 이긴다.
  단 10층 커리큘럼은 Phase 2 이후의 자산이므로 **덮어쓰지 않고 별도 층 계획을 추가**한다.
- 조치: `PrototypeCurriculum.HeroSlice` 신설(계약 없음/흡수체/증식체 3종). 10층 표는 그대로 둔다.

## G-05 (BLOCKER, Gate B·C) 과수확 레버가 존재하지 않는다

- 명세: `MASTER_PRD.md` §7 전체, `VISUAL_SPEC.md` §7, `CURRENT_PHASE.md` §2.2
  "과수확 레버 잠금·해제와 선택", Gate C "과수확 레버가 일반 실행 레버와 혼동되지 않는다".
- 현재: 씬에 레버는 `ExecutionLever` **하나뿐**이다. `RouletteInteractionBridge`는
  Decision 단계에서 **같은 레버**를 당기면 판돈을 물고 추가 스핀하도록 되어 있다.
- 영향: PRD가 "대표 장면"으로 지정한 선택이 물리적으로 존재하지 않는다.
  `visual-criteria.md` B-4.12 "확정 대 추가 스핀의 무게가 대등한가"도 자동 실패다 —
  한쪽은 탱크, 다른 쪽은 이미 다른 용도로 쓰던 레버의 재탕이다.
- 조치: 별도 대형 레버 + 보호 덮개 + 잠금 상태. 요구 전력 100% 달성 시에만 덮개가 열린다.
  실행 레버에서 추가 스핀 기능을 **제거**해 역할 혼동을 없앤다.

## G-06 (BLOCKER, Gate C) 위험 상태 시스템이 전혀 없다

- 명세: `MASTER_PRD.md` §9, `TECH_SPEC.md` §4·§6.4(`RiskProfile`),
  `CURRENT_PHASE.md` §2.2 "Stable 상태 / Critical 상태", Gate C.
- 현재: `RiskStateController`, `RiskProfile`, 위험 단계 enum 모두 없음.
  `FloorState`에 "현재 위험 상태" 필드도 없다.
- 영향: Gate C의 절반, 필수 캡처 세트의 Stable/Critical 두 장을 만들 수 없다.
- 조치: 순수 C# `RiskState` 산출기 + 데이터 프리셋 + 조명·오디오·진동 구동기.
  최종 강도는 승인 대기 항목이므로 교체 가능한 프리셋으로 둔다.

## G-07 (HIGH, Gate C) 계기판이 폐기된 컨트롤러를 참조해 죽어 있다

- 현재: `View/ElevatorGrayboxView`의 `_run` 필드 타입이 **`RunController`**(폐기된 설계)다.
  그 컴포넌트는 씬의 `GameSystems`에 있는데 `GameSystems`는 **INACTIVE**다.
  따라서 `LateUpdate`가 첫 줄에서 `return`한다.
- 영향: 씬의 `FloorLabel` `PowerLabel` `PowerBarPivot` `WeightLabel` `OverloadLight`
  `ContractPlaque_0..2`가 전부 **정적인 장식**이다. 실제 전력은 IMGUI HUD에만 있다.
  `visual-criteria.md` B-3.8 "등을 돌려도 전력을 아는가"가 구조적으로 실패한다.
- 조치: `RunSession`을 읽는 신규 `InstrumentPanelView`로 계기판 구동. `ElevatorGrayboxView`는
  폐기 경로이므로 **건드리지 않고 씬 참조만 끊는다**(재작성 금지 원칙).

## G-08 (HIGH, Gate B·C) 스핀이 한 프레임에 끝나 판독 순서가 없다

- 명세: `MASTER_PRD.md` §6 "제거 → 빈칸 → 신규 심볼 유입 → 재판정 단계를 **시각적으로
  생략하지 않는다**", §6.1 판독 순서 8단계, `TECH_SPEC.md` §10 상태 전이
  `Revealing → Resolving → Cascading → ApplyingEffects`.
- 현재: `RunSession.Spin()`이 동기적으로 전부 계산하고, `SpinBoardView`는 **최종 보드만**
  보여준다. 캐스케이드 중간 단계는 데이터에는 있으나 화면에 없다.
- 영향: 이 게임의 핵심 체감(연쇄가 눈앞에서 무너지는 것)이 존재하지 않는다.
  `visual-criteria.md` B-2.7 "캐스케이드 단계가 따라가지는가" 자동 실패.
- 조치: `SpinPresenter` — 이미 확정된 `SpinResolution.Steps`를 시간축으로 재생만 한다.
  **판정을 다시 하지 않는다**(TECH_SPEC §5 "UI와 연출은 SpinResult를 소비").

## G-09 (MEDIUM, Gate D) 사고 기록기가 없다

- 명세: `MASTER_PRD.md` §10, `CURRENT_PHASE.md` §2.2 "최소 사고 기록 요약".
- 현재: 없음. `FloorResult`에 결과 밴드와 실패 사유는 있으나 **왜 그렇게 됐는지**의
  단계별 기록이 없다.
- 조치: 순수 C# `AccidentRecorder` — 시드·계약·초기 보드·단계별 발동·잔류·과수확 여부를
  한 덩어리로 출력.

## G-10 (MEDIUM, Gate D) 디버그 패널과 시드 입력이 없다

- 명세: `MASTER_PRD.md` §4.1 "디버그 패널, 결정론적 시드, 텔레메트리",
  `CURRENT_PHASE.md` §2.3 "고정 시드 최소 3개".
- 현재: `RunSessionBehaviour._seed`가 인스펙터 값(1337)으로 고정. 런타임에 시드를 바꿔
  재현할 방법이 없다.
- 조치: 디버그 패널(시드 입력·재시작·상태 전이 로그·현재 위험 상태) + 고정 시드 3개 기록.

## G-11 (MEDIUM, Gate D) TECH_SPEC §11 요구 테스트 중 9종이 없다

현재 10종 통과. 명세가 요구하는 항목 중 미구현:

| 요구 항목 (TECH_SPEC §11) | 상태 |
|---|---|
| 다른 시드에서 결과가 고정되지 않음 | 없음 |
| 가중치 합산과 선택 경계 정확성 | 없음 |
| 제거 대상 정확성 | 없음 |
| 캐스케이드 재충전 순서 | 없음 |
| 증식체 잔류 가중치 증가 | 없음 |
| 계약 보정 적용 | 없음 |
| 요구 전력 달성 판정 | 없음 |
| 과적과 위험 상태 계산 | 없음(위험 상태 자체가 없음) |
| `SpinResult` 직렬화 또는 로그 재현 | 없음 |

## G-12 (MEDIUM, Gate D) PlayMode 검증 경로가 폐기 설계용이다

- 현재: `Assets/Editor/PlayModeSmokeTest.cs`는 폐기된 `RunController` 흐름을 검증한다.
  새 `RunSession` 흐름을 Play 모드에서 검증하는 것은 아무것도 없다.
- 명세: `TECH_SPEC.md` §12 10항목.
- 조치: 새 루프용 PlayMode 하네스 신설.

## G-13 (LOW) 폐기 설계 코드·씬 잔재

- 코드: `Core/`(RunController·FloorController·PassengerManager·ElevatorState 등),
  `Roulette/`(RouletteController·TubeController·CombinationResolver),
  `Data/Ball*`, `Effects/`(7종 핸들러), `Sim/`, `UI/PrototypeUI`,
  `Assets/Editor/`(PrototypeSelfTest·PlaytestSimRunner·BalanceProbe) — 약 5,000줄.
  전부 타이밍 정지·구슬 등급 체계 기반으로, `MASTER_PRD.md` §4.2가 명시적으로 제외한 축이다.
- 씬: `Tube_0..2`에 `TubeController`가 **활성 상태로** 남아 있다(호출자가 없어 무해).
  `ButtonPivot_1..3`(정지 버튼), `HarvestMarker`, `TubeReadouts`는 이미 INACTIVE다.
- 판단: **이번 세션에서 삭제하지 않는다.** 삭제는 컴파일 위험만 크고 Gate에 기여하지 않는다.
  단 `visual-criteria.md` B-5(정지 버튼 잔재는 그 자체로 감점)에 걸리는 **화면 잔재**는
  이미 비활성이므로 캡처에 나오지 않는지만 확인한다.
- 후속: 별도 세션에서 폐기 코드 일괄 제거 티켓.

## G-14 (LOW, 정보) EditMode/PlayMode 테스트를 Unity Test Runner로 돌릴 수 없다

- 원인: 프로젝트에 asmdef이 없어 모든 코드가 `Assembly-CSharp`에 있다. Unity의 규칙상
  asmdef 어셈블리는 predefined `Assembly-CSharp`를 **참조할 수 없다.** 따라서 NUnit 테스트
  어셈블리를 추가해도 게임 코드를 볼 수 없다.
- 제약: `TECH_SPEC.md` §3 "asmdef 도입은 별도 결정 없이 진행하지 않는다."
- 결론: 테스트는 **NUnit에 의존하지 않는 헤드리스 러너**로 유지한다(기존 방식 계승).
  Gate A의 문구가 "Unity 씬 없이 핵심 판정 테스트가 통과한다"이므로 이 방식으로 충족된다.
  → `DECISION_LOG` D-20260730-06.

---

# 4. 이번 세션 범위 밖으로 남기는 것

`CURRENT_PHASE.md` §3에 따라 아래는 **의도적으로 구현하지 않는다.**

| 항목 | 이유 | 이번 세션의 처리 |
|---|---|---|
| 2~10층 커리큘럼 밸런싱 | Phase 밖 | `PrototypeCurriculum.TenFloors` 그대로 보존, 손대지 않음 |
| 승객 4종·부품 4종 | Phase 밖 | 무게 입력만 인터페이스로 남김(`RunSession.CarriedWeight` 기존 경로) |
| 최종 아트·모션·사운드·재질 | 승인 대기 | 교체 가능한 프리셋/플레이스홀더 |
| 최종 밸런스 수치 | 승인 대기 | 전부 데이터 필드, 코드 상수 금지 |
| 폐기 코드 5,000줄 제거 | 위험 대비 이득 없음 | G-13 후속 티켓 |
| Windows 빌드 | 개발 기기가 macOS | 빌드 차단 원인으로 보고 |

---

# 5. 착수 순서 결론

Gate 순서를 그대로 따른다. 상세는 `ImplementationPlan.md`.

1. **Gate A 정합**: G-01, G-02, G-03 → 코어 명세 위반 제거
2. **Gate B 연결**: G-04, G-05, G-07, G-08 → 1인칭 오브젝트만으로 1층 완주
3. **Gate C 표현**: G-06 → Stable/Critical 공간 반응
4. **Gate D 증거**: G-09, G-10, G-11, G-12 → 기록·테스트·캡처·성능

`CURRENT_PHASE.md` §6 중단 규칙에 따라 Gate A 통과 전 비주얼 폴리시를 시작하지 않는다.

---

# Phase 2 요구사항 매핑 (2026-07-31)

> 기준: `AUTONOMOUS_PROTOTYPE_GOAL.md` §3 / `CURRENT_PHASE.md` §2 / `MASTER_PRD.md`
> 분류: **완료** / **부분 완료** / **미완료** / **현재 Phase 제외** / **승인 필요** / **환경 제약**

## 게임 진행

| 요구 | 판정 | 근거 |
|---|---|---|
| 1인칭 이동과 상호작용 | 완료 | Phase 1 자산. `FirstPersonController`·`CrosshairInteractor` |
| 1층~10층 연속 진행 | 완료 | PlayMode 완주 [1,2,3,4,5,6,7,8,9,10] + 헤드리스 `고정 시드 3개 이상이 10층을 완주한다` |
| 층 시작 계약 또는 위험 선택 | 완료 | 계약 6·7·8·9·10층, 적재 2·5·8층 |
| 층당 최대 스핀 수와 상승 조건 | 완료 | `FloorPlan.Spins` 5, `PowerThresholds` 8구간 |
| 요구 전력 후 확정 또는 과수확 | 완료 | PlayMode 에서 잠금·덮개 개방·해제·추가 스핀 소비까지 관측. 원인은 덮개 연출 대기 누락이었다 |
| 실패·사고·완주 결과 | 완료 | Jettison/Crash 실패 경로, 완주 종료 확인 |
| 고정 시드로 층·스핀 단독 재현 | 완료 | `SpinSeed.Derive(runSeed, floor, spinIndex)`, 테스트 2건 |

## 룰렛 코어 (Phase 1 자산 — 보존)

| 요구 | 판정 |
|---|---|
| 결정론적 3×3 자동 룰렛 | 완료 (재작성 없음) |
| 정상 영혼·흡수체·증식체 | 완료 |
| 같은 저항체 3개 이상 정화 | 완료 |
| 가로·세로·대각선 직선 판정 | 완료 |
| 직교 연결 4개 이상 판정 | 완료 |
| 제거·재충전·캐스케이드 | 완료 |
| 최대 캐스케이드 20 | 완료 (`SpinEngineTests` 3건이 캡·플래그·자연종료 구분을 고정) |
| 무한 루프·진행 불가 방지 | 완료 (60시드 × 10층 헤드리스 + PlayMode 2회) |
| 정화·캐스케이드 원인의 시각적 판독 | 부분 완료 (`PurifyMarkerView` 존재. **5연쇄 캡처 미생성**) |

## 승객·부품·적재

| 요구 | 판정 | 근거 |
|---|---|---|
| 4종 이상 승객·부품 | 완료 | **11종** (승객 6 · 부품 5) `BuildCatalog` |
| 엘리베이터 안 실제 배치 | 완료 | `BuildFigureView` — PlayMode에서 오브젝트 수 검증 |
| 총중량·허용 중량·과적 | 완료 | `RunSession.CarriedWeight`/`WeightCapacity`/`IsOverloaded` |
| 적재량이 요구 전력·위험 변경 | 완료 | 2층 355→431(38kg), 과적 365→854 |
| 룰렛 규칙 실제 변경 | 완료 | `minA` 3→2, `diag`, `casc` 0.50→0.75, `res` 1.00→0.55 |
| 서로 다른 두 빌드 전략 | 완료 | 무적재 vs 전적재가 5시드 중 5개에서 결과 분기 |
| 승차·하차·목적지·보상 | 완료 | `DestinationFloor`/`DisembarkReward`, PlayMode에서 하차 관측 |
| 최대 적재에서도 플레이 가능 | 부분 완료 | 좌표상 통로 확보. **6개 적재 캡처 미생성** |

## 위험과 사고

| 요구 | 판정 | 근거 |
|---|---|---|
| Stable/Warning/Critical/Collapse | 부분 완료 | 로직·테스트 11건 통과. **4상태 동일 좌표 캡처 미생성** |
| 조명·음향·진동·승객 반응 동기화 | 부분 완료 | 조명·험·진동은 `RiskStateView`에 있음. **승객 반응 없음** — 승객이 이번에 처음 씬에 생겼다 |
| 과수확이 위험·보상 변경 | 완료 | `OverharvestWeight 3.2 ≥ WarningEnter 3.0` 불변식이 테스트로 고정 |
| 사고 기록기 정확도 | 완료 | `FloorRecord.Capture` — PlayMode에서 층당 기록 검증 |

## 비주얼

`docs/runtime/VisualGapAnalysis.md`에 18항목 판정. 요약: 일치 5 / 부분 일치 8 /
불일치 1 / 자료 부족 3 / 판정 보류 1.

## 현재 Phase 제외

최종 캐릭터 모델·애니메이션, 최종 사운드, 최종 재질·조명 마감, 추가 저항체,
정상 영혼 등급 체계, 메타 진행·세이브, 멀티 엔딩, asmdef 도입.

## 승인 필요

| 항목 | 근거 |
|---|---|
| 적재 단계를 계약보다 앞에 둔 것 | `MASTER_PRD.md` §5는 반대 순서 (`A-20260731-03`) |
| 밸런스 수치 전반 | `A-20260731-02` — 측정 전 임시값 |
| 공포 표현 강도 | `A-20260730-09` — 3종 프리셋 유지 |
| 과수확 손잡이의 물리적 잠금 형상 | 현재는 콜라이더 차단, §4는 형태상 접근 불가 요구 |
| Notion 에셋 프롬프트의 폐기된 정지 버튼 | `NotionSyncReport.md` A-3 — 그대로 3D 생성에 넣으면 폐기 장치가 만들어진다 |
| Notion 승객 4종 채택 여부 | 2종(측량사·과수확 변압기)만 동결, 2종은 엔진 변경 필요 |

## 환경 제약

| 항목 | 내용 |
|---|---|
| 기준 하드웨어 미확정 | `TECH_SPEC.md` §13의 Ryzen 7 5700 / RTX 3070 이 아님. 성능 완료 선언 불가 |
| 캡처 베이스라인 무효 | 직전 세션이 macOS/M5. `machineFingerprint`가 다르므로 비교 불가 |
| 에디터 Play 모드 측정 | 빌드 성능이 아니다. 게임 코드 비용과 에디터 비용을 구분해 기록한다 |
