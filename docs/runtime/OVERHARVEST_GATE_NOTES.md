# 과수확 해금 게이트 (2026-08-09)

**요청자:** 사용자 (팀리드 경유, 자율주행 세션)
**원문:** 「과수확도 그냥 가능하게 하는 게 아니라 아이템이나 승객 조건으로 해 두는 게
좋을 것 같아. 아니면 계약으로. 우선 과수확은 기본 옵션이 아니게 해 줘.」

**구현자 권한:** `Assets/Prototype_Elevator/Scripts/Build/`·`Scripts/Run/` 의 `.cs` 만.
`Scripts/Spin/`·`.unity`/`.prefab`/`.mat`/`.asset`·Unity MCP·git 은 전부 금지.

---

## 0. 결론 요약

- 이전: `IsOverharvestUnlocked = _overharvest.IsUnlocked(Power, RequiredPower)`
  — 전력 달성률만 보고 열린다. `BUILD_CONTENT_AUDIT.md` §5 가 "Option 1: `UnlockThreshold`
  1.0 → 1.15" 를 권했지만, **그건 여전히 전력만 보는 것**이라 사용자 지시(아이템·승객·계약
  조건)를 만족하지 않는다 — 채택하지 않았다. 이유는 팀리드 지시에도 명시돼 있다.
- 이후: `IsOverharvestUnlocked = _overharvest.IsUnlocked(Power, RequiredPower)
  && HasOverharvestKey` — **AND**. 전력 달성은 여전히 필요조건이지만 더 이상 충분조건이
  아니다. `UnlockThreshold`(1.0) 는 손대지 않았다.
- 열쇠는 **아이템·승객 두 갈래**로 열었다(계약 축은 못 열었다 — §2, §6 참고).
- `_loadout == null` 은 게이트를 적용하지 않는다(§3). 실제 플레이는 이 분기를 타지 않는다.
- 팀리드가 지목한 "과수확 5단계 연출 40 PASS" 는 **애초에 이 코드에 의존하지 않았다**
  (§4) — 대신 예상보다 훨씬 넓은 범위(위험 판정 결정론, PlayMode 하네스 2개, 그리고
  권한 밖의 Telemetry·UI 스위트 각 1건)가 실제로 걸렸다. §5·§6 이 정직한 목록이다.

---

## 1. 무엇을 어디에 (파일:줄)

### 1.1 게이트 배관 — `Scripts/Build/BuildItem.cs`

- `BuildEffectKind.OverharvestUnlock` 신규 (열거형 값, :129 부근) — 이 종류를 가진
  품목이 실려 있으면 열쇠 조건을 만족시킨다.
- `BuildEffect.ApplyTo(SpinRuleSet rules)` 의 `case OverharvestUnlock`(:481) —
  **의도적 no-op**. 이유는 §2.3.
- `BuildEffect.DescribeEffect()` 의 `case OverharvestUnlock`(:393) — "과수확 레버 해금"
  표시 문구.

### 1.2 조회 + 카탈로그 — `Scripts/Build/BuildLoadout.cs`

- `BuildLoadout.HasEffect(BuildEffectKind kind)` 신규(:74) — 적재 품목의 **조건 없는**
  효과만 스캔한다(이유는 §2.3 의 순환 의존 설명과 같다).
- `PRT_OVERHARVEST_TRANSFORMER`("과수확 변압기") 의 `Effects` 배열에
  `BuildEffect.Of(BuildEffectKind.OverharvestUnlock, 1f)` 추가(:306).
- `PSG_PORTER`("짐꾼") 의 `Effects` 를 `Array.Empty<BuildEffect>()` 에서 같은 효과
  하나짜리 배열로 교체(:365). 기존엔 효과가 하나도 없던 품목이라 이게 이 품목의
  **첫 효과**다.

### 1.3 판정식 — `Scripts/Run/FloorSession.cs`

- `IsOverharvestUnlocked`(:473) — `AND HasOverharvestKey` 로 확장.
- `HasOverharvestKey`(:501, 신규, `public`) — 열쇠 판정 프로퍼티. `_loadout == null` 이면
  참, 아니면 `_loadout.HasEffect(BuildEffectKind.OverharvestUnlock)`.
- `CanTakeExtraSpin`(:508) — **코드 변경 없음.** `IsOverharvestUnlocked` 를 그대로
  쓰므로 게이트가 자동으로 전파된다. `PushYourLuck`·`ExtraSpinLimit` 도 마찬가지다.
- `UnlockThreshold`(`OverharvestProfile.cs`) — **건드리지 않았다**(팀리드 지시).

### 1.4 소비처 재확인 — 손대지 않았지만 확인한 파일들

`RouletteInteractionBridge.cs`·`InteractableLever.cs`·`AscentColumnView.cs`·
`PowerGaugeView.cs` 전부 `FloorSession.CanTakeExtraSpin`/`IsOverharvestUnlocked` 를
**그대로 위임**하고 자체 판정식을 다시 쓰지 않는다(각 파일에 "판정식을 여기서 다시
쓰지 않는다" 주석이 이미 있었다). 그래서 이번 변경은 `FloorSession.cs` 한 곳만 고쳐도
레버·UI·게이지 전부에 전파된다 — 추가로 손댈 곳이 없었다.

---

## 2. 열쇠를 몇 갈래로 열었나와 각각의 근거

### 2.1 첫째 갈래 — 부품 「과수확 변압기」(`PRT_OVERHARVEST_TRANSFORMER`)

팀리드가 먼저 지목한 항목. 이름이 이미 "과수확"이라 가장 자연스러운 자리다. 부품이라
**런이 끝날 때까지 유지**된다 — 한 번 확보하면 끝까지 열쇠를 쥔다. 기존에도
`ResidualMitigation` 을 증폭시켜(`×1.25`) 광신자(`PSG_ZEALOT`)의 `ResidualAmplified`
조건과 이미 시너지를 이루고 있었다 — 위험을 늘리는 대신 보상을 올리는 "공격적" 계열의
중심 품목이었고, 과수확(위험을 감수하고 더 당긴다)이 그 계열에 붙는 것이 어색하지 않다.

### 2.2 둘째 갈래 — 승객 「짐꾼」(`PSG_PORTER`)

열쇠가 하나뿐이면 그것이 **지배적 선택**이 된다 — 이 저장소가 `BUILD_DIVERSITY_AUDIT.md`
에서 이미 겪은 실패 패턴이다(사선 결속기 하나가 전체 이득의 74~82%를 가져갔던 사건).
그래서 팀리드 지시대로 최소 두 갈래를 열었다.

짐꾼을 고른 근거는 **지어낸 것이 아니라 이미 코드에 적혀 있었다.** `BuildItem.cs` 의
`BuildAxis.Load` 정의:

```csharp
/// <summary>무게와 과수확 위험을 출력으로 바꾼다.</summary>
Load,
```

이 축에 실려 있는 품목은 **짐꾼 하나뿐**이다. "무게와 **과수확** 위험을 출력으로
바꾼다"는 문장이 축 설명에 이미 있었고, 그 축의 유일한 품목이 짐꾼이었다 — 새로운
테마 연결을 만든 게 아니라 이미 있던 것을 실체화했다. (참고: "과수확"과 "과적"은
다른 말이다 — 과적은 중량 초과, 과수확은 이 티켓의 대상인 추가 스핀 시스템이다.
이 축 설명은 "과수확"을 정확히 쓰고 있다, 혼동이 아니다.)

부수 효과로 "영구 열쇠"(부품) vs "한시적 열쇠"(승객, 8층 하차) 라는 차이가 생겼다 —
짐꾼만 들고 8층에서 내리면 그 시점부터 다시 태우기 전까지 과수확이 잠긴다. 의도적으로
설계한 것은 아니지만 승객/부품 축의 기존 성질(하차 여부)이 자연스럽게 만들어 낸
결과이고, 게임적으로도 말이 되는 결과라 그대로 뒀다.

### 2.3 계약 축은 열지 못했다 — 권한 제약

사용자 지시는 "아이템이나 승객 조건으로... **아니면 계약으로**"였다. 계약
(`ResistanceContract`) 은 `Assets/Prototype_Elevator/Scripts/Spin/ResistanceContract.cs`
에 있고, `Scripts/Spin/` 은 이번 작업에서 **읽기만 허용**됐다(다른 에이전트 소유
디렉터리). 그래서 계약이 열쇠를 주는 경로는 만들 수 없었다 — 이건 설계 판단이 아니라
권한 경계다. §6 에 재기재한다.

### 2.4 `SpinRuleSet` 을 왜 거치지 않았나 — 원래 계획과 실제 구현이 갈린 지점

팀리드 지시는 "`DiagonalConnects` 가 좋은 본보기다. 그 조회 경로를 그대로 따르라"
였다. `DiagonalConnects` 의 실제 경로는:

```
BuildItem 의 Effects → BuildEffect.ApplyTo(SpinRuleSet) → rules.DiagonalCountsAsConnected = true
→ (다른 코드가) rules.DiagonalCountsAsConnected 를 읽는다
```

즉 **`SpinRuleSet` 에 불리언 필드를 하나 두고 그 필드를 조회하는 것**이 "그대로 따라야
할 경로"였다. 그런데 `SpinRuleSet` 은 `Scripts/Spin/SpinRuleSet.cs` 에 있고, 이 파일도
`Scripts/Spin/` 소속이라 **새 필드를 추가할 수 없었다** — 확인해 보니 필드 목록이
전부 명시적으로 나열돼 있고(범용 플래그 딕셔너리 같은 우회로도 없다) `Clone()` 도 모든
필드를 일일이 나열해서 복사한다. 손대지 않고는 새 불리언을 끼울 방법이 없었다.

그래서 조회 경로를 한 층 옮겼다 — `SpinRuleSet` 대신 `BuildLoadout` 이 직접 스캔한다
(`HasEffect`, §1.2). 판정의 **성격**은 같다("이 종류의 효과가 실려 있는가"를 묻는다)
지만 **경로**는 다르다. 이 차이가 생긴 이유는 설계 취향이 아니라 권한 경계였다는 것을
분명히 남긴다 — 만약 `Scripts/Spin/` 에 손댈 수 있는 사람이 이어받는다면, 원래
지시대로 `SpinRuleSet.OverharvestKeyEquipped` 같은 필드를 추가하고 `BuildEffect.ApplyTo`
의 `OverharvestUnlock` case 를 no-op 대신 그 필드를 세우도록 바꾸는 것도 가능하다 —
다만 그럴 필요가 실제로 있는지는 의문이다. `BuildLoadout.HasEffect` 쪽이 오히려 장점이
있다: `SpinRuleSet`(`_rules`) 은 `Boarding`/`ContractSelection` 단계에서는 아직
`null` 일 수 있는데, `HasEffect` 는 `_loadout` 을 직접 보므로 **어느 단계에서
조회해도 안전**하다. `SpinRuleSet` 경로였다면 "규칙이 아직 안 만들어졌을 때 열쇠를
어떻게 판정하나"라는 별도 문제가 생겼을 것이다.

부수적으로 — 조건부 효과는 세지 않기로 했다(`HasEffect` 는 `IsUnconditional` 인
효과만 본다). 열쇠 자체가 조건부이면 "무엇이 열쇠를 여는가"가 다른 규칙 상태에
순환 의존하게 되고, 위에서 말한 "규칙이 아직 없는 단계" 문제도 다시 생긴다. 열쇠는
단순하게 "실려 있으면 무조건 켜진다"로 뒀다.

---

## 3. `_loadout == null` 을 어떻게 정했고 왜

**결정: null 이면 게이트를 적용하지 않는다** (`HasOverharvestKey => _loadout == null
|| _loadout.HasEffect(...)`).

### 왜 이게 "게이트를 느슨하게 하는 것"이 아닌가

`RunSession.cs:17`: `private readonly BuildLoadout _loadout = new BuildLoadout();`
— **필드 초기화 시점에 이미 빈 `BuildLoadout` 이 만들어진다.** `readonly` 이므로
재대입도 없다. 즉 **실제 게임(런)은 `_loadout` 에 절대 null 을 넘기지 않는다.**
`_loadout == null` 분기는 살아 있는 게임에서 **한 번도 타지 않는 코드**다.

null 이 실제로 지나가는 경로는 `FloorSession` 생성자 체인 중 `BuildLoadout` 인자가
없는 짧은 오버로드들이다. 그 생성자의 기존 주석("`loadout`는 런이 소유하며 층을
건너 살아남는다. null이면 적재 없는 층으로 동작한다(Phase 1 Hero Slice 경로가
이렇다)")이 이미 이 경로를 "적재 시스템 자체가 없는 옛 경로"로 규정하고 있었다.

### 두 대안을 저울질한 근거

- **null → 항상 잠금**을 골랐다면: 적재 시스템이 아예 없는 경로에 "열쇠가 없으니
  잠근다"를 적용하는 셈인데, 그 경로에는애초에 **열쇠를 실을 API 자체가 없다**(그
  경로로 만든 `FloorSession` 은 `TakeOffer`/`Loadout` 이 의미가 없다). 게이트가
  지키려는 것("적재가 있는데 열쇠가 없으면 잠긴다")과 무관한 곳까지 잠기는 셈이고,
  §5 에서 실측한 대로 관련 없는 테스트가 무더기로 깨진다.
- **null → 항상 해금(현재 선택)**: 실제 런에서는 이 분기가 절대 실행되지 않으므로
  "실제 게임의 게이트가 느슨해진다"는 우려가 성립하지 않는다. 옛 경로·테스트만
  옛 동작을 유지한다.

### 검증 — 실제로 null 이 아닌지 확인한 방법

`grep -rn "new FloorSession(" Scripts/` 로 전체 생성 지점을 나열하고, 각 호출이
`loadout` 인자를 명시적으로 넘기는지 육안으로 대조했다(§5.1 참고). 프로덕션 경로는
`RunSession.cs:515` 단 한 곳이고 항상 `_loadout`(비-null)을 넘긴다. `RunSimulator.cs`
(Sim/, 오프라인 밸런스 시뮬레이터)는 `FloorSession` 을 아예 만들지 않고 자체 로직으로
동작하므로 이 경로와 무관하다.

---

## 4. "과수확 5단계 연출 40 PASS" — 재확인 결과

팀리드가 위험 구간으로 지목한 항목이지만, **실제로 열어 보니 이 스위트는
`FloorSession` 을 아예 생성하지 않는다.** `Assets/Prototype_Elevator/Scripts/View/Tests/
OverharvestStageTests.cs` 는 `OverharvestStageTimeline`/`OverharvestSnapshot` 만
직접 다룬다(정적 구간 길이, 감쇠 배율, 응시 지연, 재개 페이드 등 §7.3 의 5단계
연출 타이밍) — 판정(`IsOverharvestUnlocked`)이 아니라 **연출 재생**을 검사하는
스위트다. `UnlockThreshold` 도 건드리지 않았으므로 이 40개는 **한 자리도 손대지
않았다.** 다시 실행해도 그대로 40 PASS 여야 한다(제가 직접 돌려 확인은 못 했다 —
Unity MCP 금지, 아래 §7 참고).

이 오판(?)이 왜 생겼는지는 짐작만 가능하다 — 이름이 "과수확"을 포함하는 스위트가
이것 하나뿐이라 가장 먼저 눈에 띄었을 것이다. 실제로 게이트에 의존하는 스위트는
이름에 "과수확"이 없는 것들(위험 판정, 텔레메트리, 런 요약)이었다 — §5.

---

## 5. 실제 블라스트 반경 — 확인·조치 내역

`Assets/Editor/PrototypeSelfTest.cs` (다른 에이전트 소유, 읽기만 함)를 읽고 **"610
PASS / 4 FAIL"의 실제 구성 스위트 전부**를 확인했다. `IsOverharvestUnlocked`/
`CanTakeExtraSpin`/`PushYourLuck`/`ContinueSpinning` 4개 API 이름을 저장소 전체에서
문자열 검색해, 이 API를 실제로 호출하는 파일만 추려 하나씩 대조했다.

### 5.1 안전 확인됨 — 손대지 않음

| 스위트 | 이유 |
|---|---|
| `RunTests.cs` (내 소유) | 과수확 관련 테스트 전부가 `BuildLoadout` 인자 없는 생성자(= null 적재)를 쓴다 — §3 의 완화가 그대로 적용돼 옛 동작 유지 |
| `BuildTests.cs` (내 소유) | 이 4개 API 를 아예 호출하지 않는다. 새 효과는 `SpinRuleSet` no-op 이라 무게·필수개수·결정론 등 기존 단정에도 영향 없다 |
| `SpinRuleSetTests.cs` (Spin/, 읽기전용) | 이 4개 API 를 호출하지 않는다(계약·규칙 다발만 검사) |
| `OverharvestStageTests.cs` (View/, 읽기전용) | §4 — `FloorSession` 자체를 안 씀 |
| `LeverStateMachineTests.cs` (View/, 읽기전용) | `FloorSession`/`RunSession` 참조가 아예 없다(grep 확인) |
| `SimulatorParityTests.cs` (Sim/, 읽기전용) | 유일한 `FloorSession` 생성이 null 적재 — §3 완화 적용 |
| `LoadedCriticalPerfProbe.cs` (내 소유, PlayMode) | `critical:true` 호출이 전부 `loaded:true` 와 짝지어져 있고, "적재" 는 카탈로그 순서대로 6칸을 채우는데 `PSG_PORTER` 가 배열 4번째라 항상 포함된다 — 우연이지만 안전하다. 코드는 그대로 뒀다 |
| `ProfileTests.cs`·`PassengerReactionTests.cs`·`AudioTests.cs`·`PerfTests.cs`·`WiringDiagnosticsTests.cs`·`PresentationBindingTests.cs`·`HoldInputTests.cs`·`CustomsLockViewTests.cs`·`InstrumentPanelLineTests.cs`·`SettlementTests.cs`·`MercyHungerTests.cs`·`DemoLoadoutTests.cs`·`RiskEvaluatorTests.cs`·`SpinEngineTests.cs` 등 | 4개 API 문자열이 파일에 아예 없다(전수 grep) |

### 5.2 내 소유 — 확인 후 직접 고침

| 파일 | 무엇이 깨질 뻔했나 | 조치 |
|---|---|---|
| `Scripts/Run/Tests/RiskDeterminismTests.cs` | `TestAntePeakSurvivesFrameSampling`(유일하게 이 API 를 쓰는 테스트) 이 `DriveToOverharvestDecision` 헬퍼로 "과수확 당길 수 있는 지점"까지 몰다가, `RunSession` 이 늘 비지 않은 적재를 갖고 있어(§3) 열쇠 없이는 그 지점에 **영원히 도달하지 못해** 3000시드를 전부 헛돌고 예외로 실패했을 것 | `DriveToOverharvestDecision` 시작부에서 과수확 변압기를 직접 실어 준다(:419-420). 같은 함수를 1차·2차 양쪽이 다 쓰므로 두 패스가 일관된다. **주의**: 이 테스트는 "위험 봉우리가 중간에 있는" 특정 궤적을 3000시드 안에서 찾는 존재증명형 검사라, +24kg 이 요구 전력을 살짝 밀어 올려 그 궤적을 찾을 확률을 낮출 가능성은 있다(0으로 만들 정도는 아닐 것으로 본다 — 근거는 낮은 확신, §7) |
| `Scripts/Run/Tests/TenFloorAutoPilot.cs` (PlayMode, 610 카운트 밖) | `"공격·시드{seed}(층당 2개 적재·과수확 1회)"` 시나리오가 "번호가 가장 작은 후보"를 집는 정책이라 열쇠가 우연히만 뽑힌다 — `Check("[정책] 요구 전력 달성 후 과수확을 당길 수 있다", ...)` 류가 실패할 것 | `DriveRun` 시작부에서 `useOverharvest` 가 참일 때만 과수확 변압기를 직접 보장(:636-637). 후보 선택 로직(하네스의 결정론 기준)은 손대지 않았다 — 6칸 중 1칸·24kg 만 추가되므로 "층당 boardCount개" 관측에 끼어들지 않는다 |
| `Scripts/Run/Tests/TenFloorCaptureRig.cs` (PlayMode, 610 카운트 밖) | `ForceCritical`("과적 위에 과수확을 얹어 Critical 을 강제한다") 이 열쇠 없이는 목적을 달성 못 함 — 캡처가 의도한 위험 단계를 못 찍는다 | 함수 시작부에서 열쇠를 직접 보장(:765-766) |

### 5.3 권한 밖 — 확인만 했고 고치지 못함

`Scripts/Telemetry/`·`Scripts/UI/` 는 이번 작업 쓰기 권한 밖이다. 아래 둘은 **읽어서
직접 확인한, 실제로 깨질 것으로 예상되는** 항목이다. 이건 예측이 아니라 코드를 읽고
"열쇠를 실을 경로가 없다"를 확인한 것이다.

| 파일 | 테스트 | 왜 깨지는가 | 최소 수정안(적용 못 함) |
|---|---|---|---|
| `Scripts/Telemetry/Tests/TelemetryTests.cs:931` | `TestExtraSpinIsMarked` ("과수확으로 산 스핀이 추가 스핀으로 표시된다") | seed 1337~1386(50개) 에서 `DrivePushingLuck(run)` 을 돌려 추가 스핀이 한 번이라도 나오길 기다린다. `run = new RunSession(seed)` 는 빈 적재로 시작하고 어디서도 열쇠를 싣지 않는다 — 50개 시드 전부 실패하고 `"50개 시드 안에 추가 스핀이 한 번도 일어나지 않았다"` 로 FAIL 할 것 | `DrivePushingLuck` 시작부(또는 `RunSession` 생성 직후)에서 `run.Loadout.Add(BuildCatalog.ById("PRT_OVERHARVEST_TRANSFORMER"))` 한 줄 |
| `Scripts/UI/Tests/RunSummaryBuilderTests.cs:~319` | `TestOverharvestReported` ("과수확한 런은 비율과 마지막 선택을 남긴다") | 헬퍼 `Play(seed, overharvest, out run)` 이 `overharvest && floor.CanTakeExtraSpin && run.PushYourLuck()` 로 시도하지만 400시드 전부 빈 적재라 `CanTakeExtraSpin` 이 항상 거짓 — `"400시드 안에 과수확이 일어난 런이 없다"` 로 FAIL 할 것 | `Play` 안에서 `overharvest == true` 일 때만 `run.Loadout.Add(BuildCatalog.ById("PRT_OVERHARVEST_TRANSFORMER"))` 를 보딩 진입 전에 한 줄 추가 — `TestNoOverharvestReported`(`overharvest=false`) 는 건드리지 않아야 대조군이 유지된다 |

**같은 파일의 다른 테스트는 안전한지도 확인했다:** `RunSummaryBuilderTests.cs` 의
`TestNineFieldsFilled`/`TestNoPlaceholderInRealRun` 은 `overharvest=true` 를 쓰지만
"과수확이 실제로 일어났는가"가 아니라 "런이 끝났는가"만 요구해서 안전하다(과수확
없이도 층은 끝난다) — `data.LastOverharvestChoice` 는 시도 자체가 없어도
"과수확 없음"이라는 유효한 비어있지-않은 문자열을 낸다(`TestNoOverharvestReported`
가 바로 이 문자열을 검증 대상으로 삼고 있다).

`Scripts/Sim/` (Sim/Tests/SimulatorParityTests.cs, Sim/RunSimulator.cs) 도 권한
밖이지만 §5.1 에서 이미 안전 확인됐다 — 조치 불필요.

---

## 6. 못 한 것 · 확신 없는 것 (정직하게)

1. **계약 축을 열지 못했다.** 사용자가 제시한 세 번째 옵션("아니면 계약으로")을
   구현하지 못했다 — `ResistanceContract` 가 `Scripts/Spin/` 소속이라 권한 밖이었다.
   지금은 아이템·승객 두 갈래만 있다. `Scripts/Spin/` 을 다룰 수 있는 에이전트가
   이어받으면: `ResistanceContract` 에 `GrantsOverharvestKey`(bool) 같은 필드를
   추가하고 `FloorSession.HasOverharvestKey` 를
   `_loadout == null || _loadout.HasEffect(...) || _contract.GrantsOverharvestKey`
   로 OR 확장하면 된다. `_contract` 는 이미 `FloorSession` 이 들고 있다.
2. **`SpinRuleSet` 경로를 못 따랐다.** 팀리드가 제시한 "`DiagonalConnects` 처럼"
   경로를 문자 그대로 구현하지 못하고 `BuildLoadout.HasEffect` 로 우회했다 — §2.4 에
   이유와 대안을 적었다. 되짚어보면 이 우회가 오히려 더 안전한 설계였다는 것도
   §2.4 에 적었지만, "원래 지시를 따르지 못했다"는 사실 자체는 남긴다.
3. **어떤 테스트도 실제로 돌려서 확인하지 못했다.** Unity MCP 가 금지돼 있어
   컴파일·PlayMode·`Ascend/Run Self Tests` 전부 **팀리드의 검증에 의존한다.**
   §5 의 "안전 확인됨"은 전부 정적 코드 읽기(grep + 로직 추적) 로만 판단한 것이고,
   컴파일 오류나 내가 놓친 실행 시점 동작(예: 이벤트 구독 순서, 널 참조)이 있을 수
   있다. 특히 `FloorSession.cs`·`BuildItem.cs`·`BuildLoadout.cs` 세 파일은 서로
   맞물려 있어 오타 하나가 셋 다 컴파일을 막을 수 있다 — **첫 검증은 반드시
   `grep -an "error CS" Logs/Editor.log | tail -5` 로 시작해 달라** (`CLAUDE.md`
   의 표준 절차).
4. **`RiskDeterminismTests.TestAntePeakSurvivesFrameSampling` 의 3000시드 탐색
   성공률을 실측하지 못했다.** +24kg 이 요구 전력을 밀어 올려 "봉우리가 중간에
   있는" 궤적을 찾을 확률이 낮아질 수 있다. 3000회 시도라는 여유가 있어 0이 될
   가능성은 낮다고 "생각"하지만 실측이 아니라 추정이다. 이 테스트가 새로 FAIL
   하면 가장 먼저 의심할 곳이다.
5. **`PSG_PORTER` 에 열쇠 효과를 얹은 것이 밸런스에 미치는 영향은 재지 않았다.**
   `OverharvestUnlock` 자체는 `SpinRuleSet` 을 안 건드리므로 스핀 수치에는 영향이
   없지만, "짐꾼을 태우면 과수확도 열린다"는 조합이 `BuildTests.cs` 의 10층 완주율
   계열 테스트(`TestSeedsCompleteTenFloors` 등)에 새로운 상호작용을 만들지는
   확인하지 못했다 — 다만 그 테스트들이 `CanTakeExtraSpin`/`PushYourLuck` 을
   호출하지 않는다는 것은 §5.1 에서 확인했으므로, 있어도 간접적일 것이다.
6. **`docs/runtime/BUILD_CONTENT_AUDIT.md` §5 를 수정하지 않았다.** 그 문서는
   Option 1(권장)을 적어 뒀는데 이번 구현은 Option 2 계열을 채택했다 — 문서와
   구현이 어긋난 상태로 남아 있다. 그 문서의 소유권이 불명확해 직접 고치지
   않았다(내 권한은 `Scripts/Build/`·`Scripts/Run/` 의 `.cs` 뿐, `docs/` 갱신은
   이 노트와 `ASSUMPTION_LOG.md` 로 대신했다). 다음에 그 문서를 여는 사람은 §5 를
   "Option 2 채택, 근거는 `OVERHARVEST_GATE_NOTES.md`" 로 갱신해야 한다.

---

## 7. 재현·검증 절차 (팀리드용)

```bash
grep -an "error CS" Logs/Editor.log | tail -5      # 컴파일 먼저
```

그다음 `Ascend/Run Self Tests` 를 돌려 610+4(새 테스트 4개 추가로 614) 근방에서
FAIL 이 몇 개인지 본다. 이 작업 전 4 FAIL 이 무엇이었는지는 확인하지 않았다 — 그
4개가 이 변경과 무관하다는 보장은 없다. 새로 FAIL 이 늘었다면 아래 순서로 의심한다.

1. `BuildTests.cs` 의 신규 4개(`TestOverharvestLockedWithoutKey` 등) — 게이트
   자체의 회귀 검증. 이게 실패하면 §1.3 의 판정식부터 재확인.
2. `RiskDeterminismTests.cs` 의 `TestAntePeakSurvivesFrameSampling` — §6.4.
3. `TelemetryTests.cs`/`RunSummaryBuilderTests.cs` — §5.3, 예상된 실패. 이 세션이
   고치지 못한 것이므로 팀리드나 해당 디렉터리 소유 에이전트가 §5.3 의 최소
   수정안을 적용해야 한다.
