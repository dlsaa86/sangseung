# 현재 구현 실증 감사 — 2026-08-01

> **원격 main도, 과거 문서도 기준이 아니다.** 이 문서는 현재 로컬 브랜치의 실제 코드,
> Unity 씬, 테스트 산출물, 빌드, 캡처만 근거로 삼는다. 문서가 "미완료"라고 적었어도
> 증거가 있으면 증거를 따르고, "완료"라고 적었어도 증거가 없으면 완료로 보지 않는다.
>
> 게임 코드·씬·프리팹·머티리얼은 이 감사에서 **한 줄도 수정하지 않았다.**

---

# 1. 저장소 상태

| 항목 | 값 |
|---|---|
| 브랜치 | `agent/phase2-full-prototype` |
| main 대비 | **28 커밋 앞섬** · 103 파일 변경 · +26,112 / −8,174 |
| HEAD | `1992651` 진행 문서에 방금 만든 커밋을 적는다 |
| 미커밋 | 2건 (아래 §1.2) |

## 1.1 main 대비 앞선 커밋 28개 — 무엇이 들어왔는가

| 커밋 | 들어온 구현 |
|---|---|
| `c9a86e4` | 10층 건너뛰기 클램프 + 적재 시스템 연결 |
| `fea4cb1` | 폐기된 정지 버튼 제거, 비례 변경 결함 3건 되돌림 |
| `fc540a9` | 상호작용만으로 10층 완주 증명 (두 정책), 승객 불안 반응 |
| `75fd507` | 무게 전파 누락 수정 (과적인데 Stable 이던 결함) |
| `4499f5a` | 성능·GC 측정 — 게임 코드 비용과 에디터 비용 분리 |
| `0503a3f` | 독립 QA 감사가 Gate D 판정을 뒤집음 (빈 테스트 2개 적발) |
| `3405b82` | 사고 기록기에 적재 반영 |
| `2ca3638` | 필수 캡처 완성 |
| `2f9bc84` | 층수 표시등 회전 제거 |
| `aa3cc8d` | 빈 판 캡처 문제 — 독립 시각 평가 불채택 |
| `84c1e28` | 하차가 확정된 층의 숫자를 바꾸던 회귀 수정 |
| `dcce85b` | 위험 단계 캡처가 기다리지 않던 문제 |
| `33af65e` | 계약 조건을 선택자 옆에 배치 (계기판에 자리 없음을 3회 만에 인정) |
| `815b1f5` | 스파이크 귀속 순서 수정 |
| `db5d84e` | 반전 글자 — 회전이 아니라 재질 문제 |
| `9623475` | Unity 모달 자동 해제 훅, worktree 가드 |
| `6b2dc0c` | "층 실패"가 성공한 층을 설명하던 문제 |
| `4e238b1` | 5차 감사 — 모순은 화면이 아니라 규칙에 있었다 |
| `d97ed3e` | **정화 인접 요구** — 판정과 제거를 함께 수정 |
| `641bbb3` | 전문 서브에이전트 7종 + 오케스트레이션 규약 |
| `71e8c0f` | 입구 카메라가 하우징 안에 있던 문제 |
| `6dea2a1` | 어셈블리 미컴파일 오진 수정 |
| `36a383f` | **Windows 빌드 성공** + 씬의 유령 컴포넌트 발견 |
| `37aa5c3` | **커리큘럼 클램프** — 층을 건너뛰면 커리큘럼이 지워짐 |
| `5543214` | 캡처 파일 이름 문제 + 순손실이던 수정 되돌림 |
| `33f1b54` | 세션 기록 |
| `27dab79` | 탑다운 실행 구조 설치 |
| `1992651` | 진행 문서 갱신 |

## 1.2 미커밋 2건 — 사용자 변경이 아니다

| 파일 | 성격 | 판단 |
|---|---|---|
| `Assets/Prototype_Elevator/Fonts/NanumGothic SDF.asset` | +1,664 / −2,167 — **글리프 순손실** | 배치 모드 Unity가 동적 폰트 데이터를 초기화한 결과. `font-atlas-guard.sh`가 막으려는 바로 그 회귀다. 커밋하면 한글 렌더링이 깨진다 |
| `ProjectSettings/UnityConnectSettings.asset` | `m_Enabled: 0 → 1` | 에디터가 Unity Analytics를 켠 것. 의도한 변경이 아니다 |

**둘 다 사람이 만든 변경이 아니고, 둘 다 커밋 대상이 아니다.** 이번 감사에서는
지시대로 손대지 않았다. 복구는 `git checkout HEAD -- <경로>`이며 에디터가 살아 있는
동안에도 `.asset`은 worktree 가드를 통과한다.

---

# 2. Unity 씬과 Build Settings

| 항목 | 값 |
|---|---|
| Build Settings 씬 | `Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity` **1개, enabled** |
| 씬 규모 | 17,754줄 · GameObject 161 · MonoBehaviour 62 |
| 루트 활성 | `AscendRun`(활성), `GrayboxWorld`(활성), `GameSystems`(**비활성**), `Canvas`(**비활성**) |

## 2.1 활성 런타임 스택 — `AscendRun`

`RunSessionBehaviour` · `SpinBoardView` · `SpinPresenter` · `AccidentRecorder` ·
`RouletteInteractionBridge` · `GameHudView` · `DebugPanelView`

## 2.2 결정적 확인 — 통관과 결과판은 물리적으로 하나다

`SpinBoardView._cells` 9개의 부모를 씬에서 역추적한 결과:

```
Cell_0,1,2 → 부모 Tube_0
Cell_0,1,2 → 부모 Tube_1
Cell_0,1,2 → 부모 Tube_2
```

**세 통관이 각 3칸을 담는 구조가 씬에 실제로 구현돼 있다.** PRD §4.1의
"세 통관이 각 3개 결과를 만드는 3×3"은 화면 배치가 아니라 계층 구조로 성립한다.
`UP-DEVICE-01`을 `VERIFIED`로 올린 근거다.

---

# 3. 컴파일과 콘솔

| 검사 | 결과 |
|---|---|
| `Assembly-CSharp.dll` | 2026-08-01 04:20:22 |
| `Assembly-CSharp-Editor.dll` | 2026-08-01 04:20:23 |
| 최신 게임 소스 | `Run/Tests/TenFloorCaptureRig.cs` 04:20 |
| 최신 에디터 소스 | `Editor/AscendTestMenu.cs` 04:01 |
| 판정 | **소스가 어셈블리보다 새롭지 않다 → 마지막 변경이 컴파일을 통과했다** |

`Logs/Editor.log`에 `error CS`가 6건 있으나 **전부 과거 기록**이다.
마지막 오류는 35,830줄 중 **1,756줄**(전체의 5%) 지점의
`TenFloorSceneBuilder.cs(822,29): CS0111 중복 Paint`이고, 현재 파일에는 `Paint` 정의가
**1개뿐**(818줄)이다. 이후 34,000여 줄에 오류가 없다.

**현재 컴파일 오류 0 · 치명적 콘솔 오류 0.**

---

# 4. 테스트 실증

## 4.1 EditMode — 91 PASS / 0 FAIL (`Logs/editmode_tests.txt`)

| 스위트 | 건수 | 무엇을 증명하는가 |
|---|---|---|
| Spin Engine | 29 | 인접 정화·직선 3종·직교 연결 4개·대각 규칙·캐스케이드·하드캡 20·시드 파생·가중치·계약 4값·패턴 칸 보고 |
| Run | 16 | 계약 게이팅·스핀 소진·CanBank·앤티·무게→요구전력·결정론·10층 커리큘럼 보존 |
| Risk Evaluator | 11 | 4단계·히스테리시스·과적·과수확·층 실패 Collapse·원인 설명 |
| Build | 35 | 적재·슬롯·허용중량·과적 배수·승객/부품 규칙 변경·하차 보상·클램프·**고정 시드 3개 이상 10층 완주**·두 빌드 차이 |

## 4.2 자체 검증 — 110 PASS / 0 FAIL (`.claude/state/last-selftest.txt`)

`PrototypeSelfTest`가 위 4개 스위트(91)를 접고 **자체 검사 19건**을 더한 값이다.
자체 검사에는 `1. 구슬 확률 합계 == 100` 같은 **레거시 구슬 시스템 단정**이 섞여 있다.
커밋 게이트가 도는 숫자이므로 정리 시 함께 봐야 한다 (`UP-TEST-11`).

## 4.3 PlayMode — 394 PASS / 0 FAIL / 콘솔오류 0 (`Logs/tenfloor_playmode.txt`)

**8개 런 전부 상호작용만으로 진행했다.** 디버그 조작 없음.

| 런 | 방문 층 | 결말 |
|---|---|---|
| 보수·1337 (씬 시드) | [1..8] | 8층 Crash |
| 보수·4242 / 공격·4242 | **[1..10]** | 완주 |
| 보수·7 / 공격·7 | **[1..10]** | 완주 |
| 보수·271828 / 공격·271828 | **[1..10]** | 완주 |
| 재현·1337 | [1..8] | 첫 런과 동일 |

- 8런 전부 **방문 층 연속** — 건너뛴 층 없음
- **6런이 10층 전부 도달** · 서로 다른 완주 시드 **3개**
- 재현 검증: 같은 시드가 같은 방문 층·같은 소지금

---

# 5. 빌드 · 캡처 · 성능

## 5.1 Windows 빌드 (`Logs/build_report.txt`)

```
result: Succeeded · totalErrors: 0 · 134.9 MB · 328.8초
outputPath: Builds/Windows/Upandup_DDD.exe   ← 디스크에 실존 확인
```

## 5.2 고정 캡처 18장 (`Captures/TenFloor/manifest.txt`)

매니페스트가 **연출이 아니라 실제 게임 상태**임을 각 줄에 적고 있다.

| 캡처 | 실측된 상태 |
|---|---|
| `09_risk_strain` | 과적 218/130 → 실제 단계 Warning (06에서 무게만 +140kg) |
| `10_risk_critical` | 실제 단계 Critical / 점수 7.0 / 게이지 0.401m |
| `12_overharvest_unlocked` | unlocked=True 덮개열림=True / 게이지 0.632m |
| `13_overharvest_pulled` | 판돈 46 지불 / 추가 스핀 0→1 / 전력 421/350 |
| `15_cascade_deep` | 시드 12 / 8층 / **연쇄 8단계 중 5단계** / 게이지 100% |
| `16_risk_collapse` | 실제 단계 Collapse / 실패 True / 사유 Crash |

기기 지문: `Windows|Direct3D12|NVIDIA GeForce RTX 3070|6000.5.5f1`

## 5.3 성능 (`Logs/loaded_critical_perf.txt`)

| 조건 | 중앙 | 95% | 최악 | GC/프레임 |
|---|---|---|---|---|
| 무적재·Stable | 8.33 ms | 8.40 | 8.49 | 10,760 B |
| 최대적재·Critical | 8.33 ms | 8.62 | 8.83 | 9,128 B |

**중앙값 8.33ms는 정확히 1/120초 — 비용이 아니라 vSync 상한이다.**
상한에 걸린 값으로는 PRD §13.1의 90 FPS 목표를 판정할 수 없다. 읽을 수 있는 것은
꼬리뿐이고, 그 기준으로 최대적재+Critical이 0.2~0.3 ms 무겁다.
GC 9,000~11,000 B/프레임은 목표 0 B와 큰 격차다.

## 5.4 독립 시각 평가 (`docs/runtime/VISUAL_VERDICT.md`)

**VERDICT: REJECT.** 최우선 지적 — `01_entry`가 공간의 높이를 보여주지 못한다.
지적 6건은 백로그 §5 수정 백로그(`UP-FIX-01`~`06`)로 전환돼 있다.

---

# 6. 죽은 코드 — 씬에 살아 있는 레거시

`VISIBLE` 후보를 찾다가 확인한 두 가지다. **둘 다 Required 항목의 결함이 아니라
정리 대상**이므로 `UP-TEST-11`·`UP-APV-12`에 귀속한다.

## 6.1 `TubeController` ×3 — 활성이지만 구동자가 죽어 있다

| 사실 | 확인 방법 |
|---|---|
| `Tube_0/1/2`에 붙어 있고 **셋 다 활성** | 씬 파싱 |
| `_config`·`_ballContainer`·`_harvestMarker` **모두 배선됨** | 씬 직렬화 필드 |
| 구동자 `RouletteController`는 **비활성 `GameSystems`** 위에 있다 | 씬 파싱 |
| `Update()`는 `_stream == null` 에서 즉시 반환 | `TubeController.cs:142` |

이 컴포넌트는 "구슬 스트림 스크롤 · 브레이크 지연 · 수확창 정지"를 구현한다 —
**PRD §4.2가 명시적으로 제외한 통관별 정지 설계다.** 지금은 무해하지만 완전히 배선된
채 한 번의 호출이면 되살아난다.

## 6.2 `ElevatorGrayboxView` — 활성 오브젝트에서 죽은 모델을 읽는다

`GrayboxWorld`(활성)에 붙어 있고, `_run`이 **비활성 `GameSystems`의 `RunController`**를
가리킨다. 그러면서 살아 있는 오브젝트를 12개나 잡고 있다:

```
_doorLeft, _doorRight        ← InteractableDoorControl 이 쓰는 그 문
_passengerAnchor, _candidateAnchor  ← BuildFigureView 가 쓰는 그 앵커
_floorLabel, _powerLabel, _weightLabel, _powerBarPivot, _overloadLight
_buttons[3], _buttonRenderers[3], _tubeLabels[3]  ← 전부 null
```

**같은 트랜스폼에 기록자가 둘이고 한쪽은 죽은 모델을 먹고 있다.**
PlayMode 콘솔 오류가 0이므로 현재는 가드에 걸려 아무것도 쓰지 않는다. 그러나 이것이
`36a383f`에서 빌드가 드러낸 "씬의 유령"과 같은 종류의 부채다.

## 6.3 레거시 코드 목록 (정리 대상)

```
Scripts/Data/Ball{Database,Definition,Grade}.cs   ← BallGrade = PRD §4.2 제외한 9종 등급 체계 잔재
Scripts/Data/{CombinationConfig,PassengerDefinition}.cs
Scripts/Roulette/{RouletteController,CombinationResolver,TubeController}.cs
Scripts/Effects/*                                  (EffectResolver 등 18파일)
Scripts/Core/{RunController,FloorController,PassengerManager,ElevatorState}.cs
Scripts/UI/PrototypeUI.cs
Scripts/View/ElevatorGrayboxView.cs
Data/{Balls,Effects,Passengers}/*.asset
```

**활성 스택이 이들을 참조하는 곳은 정확히 두 파일뿐이다** — `UI/PrototypeUI.cs`와
`View/ElevatorGrayboxView.cs`. 둘 다 그 자체가 레거시다. 즉 **새 스택(`Spin`·`Run`·
`Build`·`Risk`)은 레거시에 전혀 의존하지 않으며, 정리는 순환 참조 없이 가능하다.**

---

# 7. 상태 분포

| 상태 | 개수 | 비율 |
|---|---|---|
| `VERIFIED` | **64** | 50% |
| `CONNECTED` | 26 | 20% |
| `VISIBLE` | 0 | — |
| `SKELETON` | 16 | 12% |
| `NOT_STARTED` | 23 | 18% |
| **Required 합계** | **129** | |

`UP-RUN-09`(돈)를 Required에서 **Deferred로 내렸다.** PRD §4.1 어디에도 없다.
이미 구현·테스트돼 있고 초과 전력 처분에 쓰이므로 **제거하지 않는다.**

---

# 8. 이 감사가 뒤집은 것

| 과거 기록 | 실증 |
|---|---|
| 백로그 "VERIFIED 0건" | 판정 기준이 달랐을 뿐. 실제로는 64건이 코드·씬·테스트를 모두 갖췄다 |
| `CurrentStateAudit` §9 "PlayMode에 완주 단정 없음 (blocker)" | **닫혔다.** "10층 완주가 최소 3회 — 실제 6/8회" PASS |
| `CurrentStateAudit` §9 "최대 적재+Critical 성능 측정 없음 (blocker)" | **닫혔다.** `LoadedCriticalPerfProbe` 산출물 존재 |
| `GapAnalysis` "개발 기기가 macOS라 빌드 불가" | **사라졌다.** Windows 빌드 Succeeded |
| Editor.log의 CS0111 오류 | 과거 기록. 현재 파일에 중복 정의 없음 |

# 9. 이 감사가 새로 연 것

| 발견 | 귀속 |
|---|---|
| `TubeController` ×3이 활성 + 완전 배선 상태로 남아 있다 (PRD §4.2 제외 설계) | `UP-TEST-11` |
| `ElevatorGrayboxView`가 살아 있는 문·앵커·라벨 12개를 죽은 모델로 잡고 있다 | `UP-TEST-11`, `UP-APV-12` |
| 커밋 게이트의 110건에 레거시 구슬 확률 단정이 섞여 있다 | `UP-TEST-11` |
| 폰트 아틀라스가 글리프 순손실 상태로 미커밋 방치돼 있다 | 이 문서 §1.2 |
| 성능 중앙값이 vSync 상한에 고정돼 90 FPS 목표를 판정할 수 없다 | `UP-TECH-04` |
</content>
