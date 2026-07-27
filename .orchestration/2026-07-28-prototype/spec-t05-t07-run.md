# 목표

T-05(초과 전력 분배) + T-07(10층 미니 런)을 구현한다.
완료 = 한 번 시작해 10층까지 플레이하고 성공 또는 실패로 끝난 뒤 즉시 재시작할 수 있으며,
재시작 시 이전 런 데이터가 완전히 초기화된다. 컴파일 에러 0.

---

# 프로젝트 배경

- Unity 6000.5.5f1 / URP. 네임스페이스 `Ascend.Prototype`. asmdef 없음.
- 앞선 티켓에서 효과 파이프라인(T-03)과 승객·과적(T-04/T-06)이 구현되었다.
- **먼저 읽어라:**
  - `Assets/Prototype_Elevator/Scripts/Core/RunController.cs`
  - `Assets/Prototype_Elevator/Scripts/Core/FloorMath.cs`
  - `Assets/Prototype_Elevator/Scripts/Core/FloorController.cs`
  - `Assets/Prototype_Elevator/Scripts/Core/ElevatorState.cs`
  - `Assets/Prototype_Elevator/Scripts/Core/PassengerManager.cs`
  - `Assets/Prototype_Elevator/Scripts/Data/PrototypeConfig.cs`

현재 `RunController.ApplyOverchargeAllocation()`은 임시 구현이다.
초과 전력을 `BankedPower`에 그대로 넣거나 돈으로 바꾸기만 한다.
이걸 제대로 된 선택지로 바꾸는 게 T-05다.

---

# 변경 대상

## 수정 — `PrototypeConfig.cs` (기존 필드 삭제 금지)

```csharp
[Header("Run (T-07)")]
[Tooltip("이 층에 도달하면 런 성공.")]
public int targetFloor = 10;

[Tooltip("한 층에서 허용되는 발전 재시도 횟수. 초과하면 런 실패.")]
public int maxRetriesPerFloor = 2;

[Header("Overcharge (T-05)")]
[Tooltip("추가 상승 1개 층에 필요한 초과 전력.")]
public float powerPerExtraFloor = 60f;

[Tooltip("추가 상승으로 한 번에 오를 수 있는 최대 층수.")]
public int maxExtraFloorsPerAllocation = 3;
```

## 신규 — `Assets/Prototype_Elevator/Scripts/Core/RunOutcome.cs`

```csharp
public enum RunOutcome { InProgress, Success, Failure }
```

## 신규 — `Assets/Prototype_Elevator/Scripts/Core/OverchargeOption.cs`

**순수 C# 구조체.** 두 선택지를 값으로 표현해 UI가 비교해 보여줄 수 있게 한다.
"같은 상황에서도 빌드·자원 상태에 따라 선택 가치가 달라진다"는 요구를 만족시키는 자료구조다.

```csharp
public enum OverchargeMode { Money, Ascend }

[Serializable]
public struct OverchargeOption
{
    public OverchargeMode Mode;
    public float SurplusUsed;    // 이 선택이 소비하는 초과 전력
    public int   FloorsGained;   // 추가로 오르는 층수 (Money면 0)
    public float MoneyGained;    // 얻는 돈 (Ascend면 0)
    public float PowerCarried;   // 다음 층으로 이월되는 전력
    public string Label;         // UI 한 줄 표기. 예: "추가 상승 +2층 (전력 120 소비)"
}
```

## 수정 — `FloorMath.cs`

두 선택지를 계산하는 함수를 구현/보강한다.

```csharp
public static OverchargeOption BuildMoneyOption(PrototypeConfig cfg, float surplus);
public static OverchargeOption BuildAscendOption(PrototypeConfig cfg, float surplus);
```

공식 — 이대로:
```
Money 선택:
    FloorsGained  = 0            (다음 층까지만 상승)
    MoneyGained   = surplus * cfg.powerToMoneyRatio
    PowerCarried  = 0
    SurplusUsed   = surplus

Ascend 선택:
    FloorsGained  = clamp(floor(surplus / cfg.powerPerExtraFloor),
                          0, cfg.maxExtraFloorsPerAllocation)
    SurplusUsed   = FloorsGained * cfg.powerPerExtraFloor
    PowerCarried  = surplus - SurplusUsed      // 남은 건 다음 층 시작 전력으로 이월
    MoneyGained   = 0
```
`cfg`가 null이거나 `powerPerExtraFloor <= 0`이면 0층/0원 옵션을 반환한다(0으로 나누기 금지).

`Label`은 한국어로 채운다:
- Money: `$"돈 +{MoneyGained:F0}  (초과 전력 {SurplusUsed:F0} 전량 변환)"`
- Ascend: `$"추가 상승 +{FloorsGained}층  (전력 {SurplusUsed:F0} 소비, {PowerCarried:F0} 이월)"`

## 수정 — `ElevatorState.cs`

추가 필드 (`Initialize()`에서 전부 초기화):
```csharp
public int   RetriesThisFloor;
public int   TotalRetries;
public int   HighestFloorReached;
public float TotalMoneyEarned;
public int   TotalAccidents;
```

## 수정 — `RunController.cs`

### 런 결과 상태
```csharp
private RunOutcome _outcome = RunOutcome.InProgress;
public  RunOutcome Outcome => _outcome;

// UI가 두 선택지를 나란히 보여줄 수 있게 노출
public OverchargeOption MoneyOption  { get; private set; }
public OverchargeOption AscendOption { get; private set; }
```

### `ResetRun()` — 완전 초기화
다음을 **전부** 되돌려야 한다. 하나라도 빠지면 재시작이 오염된다:
- `_outcome = RunOutcome.InProgress`
- `_state.Initialize(_config)` (신규 필드 포함)
- `_passengers.ResetPassengers()` + `InitializeSeed`
- `_roulette.InitializeSeed` + `ResetTubes`
- `_effects.InitializeSeed`
- `_accidentRng` 재생성
- `_surplus`, `_lastShortfall`, `_lastResolutionSuccess`, `_overchargeChoice`,
  `_lastCombination`, `_turnResolved`, `_floorStartPower`
- `MoneyOption` / `AscendOption` 기본값
- `_floor.EnterFloor(0)` + `RecalculateLoad()`

### `PerformPowerResolution()` — 실패 시 재시도 횟수 관리
실패 분기에서:
```csharp
_state.RetriesThisFloor++;
_state.TotalRetries++;
if (_state.RetriesThisFloor > _config.maxRetriesPerFloor)
{
    FailRun($"{_floor.CurrentFloor}층에서 요구 전력 미달 (재시도 {_config.maxRetriesPerFloor}회 초과)");
    return;
}
RetryFloor();
```

성공 분기에서 두 옵션을 미리 계산한다:
```csharp
MoneyOption  = FloorMath.BuildMoneyOption(_config, _surplus);
AscendOption = FloorMath.BuildAscendOption(_config, _surplus);
```
사고가 났다면 `_state.TotalAccidents++`.

### `ApplyOverchargeAllocation()` — 재작성
```csharp
OverchargeOption chosen = (_overchargeChoice == 1) ? AscendOption : MoneyOption;

if (chosen.Mode == OverchargeMode.Ascend)
{
    _pendingExtraFloors = chosen.FloorsGained;
    _state.BankedPower  = chosen.PowerCarried;
}
else
{
    _pendingExtraFloors = 0;
    _state.Money            += chosen.MoneyGained;
    _state.TotalMoneyEarned += chosen.MoneyGained;
    _state.BankedPower       = 0f;
}
Debug.Log($"[상승] 초과 전력 분배: {chosen.Label}");
_overchargeChoice = 0;
```
`_pendingExtraFloors`는 새 private int 필드.

### `Ascending` 전이 — 층 이동 + 성공 판정
```csharp
case RunState.Ascending:
    int climb = 1 + _pendingExtraFloors;      // 기본 1층 + 추가 상승
    _pendingExtraFloors = 0;

    _floorStartPower   = _config.startingPower + _state.BankedPower;
    _state.Power       = _floorStartPower;
    _state.BankedPower = 0f;
    _state.CurrentTurn = 0;
    _state.RetriesThisFloor = 0;              // 층이 바뀌면 재시도 초기화

    int next = _floor.CurrentFloor + climb;
    _floor.EnterFloor(next);
    _state.HighestFloorReached = Mathf.Max(_state.HighestFloorReached, next);

    if (next >= _config.targetFloor) { SucceedRun(); return; }

    _passengers.GenerateCandidates(next);
    RecalculateLoad();
    to = RunState.FloorArrival;
    break;
```

### 신규 메서드
```csharp
private void SucceedRun()
{
    _outcome = RunOutcome.Success;
    Debug.Log($"[상승] === 런 성공 === {_floor.CurrentFloor}층 도달 / 돈 {_state.Money:F0} / 사고 {_state.TotalAccidents}회 / 재시도 {_state.TotalRetries}회");
}

private void FailRun(string reason)
{
    _outcome = RunOutcome.Failure;
    _state.LastFailureReason = reason;         // ElevatorState에 string 필드 추가
    Debug.Log($"[상승] === 런 실패 === {reason} / 최고 도달 {_state.HighestFloorReached}층");
}
```
`ElevatorState`에 `public string LastFailureReason;` 추가하고 `Initialize()`에서 비운다.

### `Update()` 입력 게이트
- `_outcome != RunOutcome.InProgress` 이면 **Space와 통관 정지 입력을 전부 무시**한다.
  `[R]` 재시작만 받는다. 종료 화면에서 상태가 더 진행되면 안 된다.
- `OverchargeAllocation` 상태의 `[1]`/`[2]`는 그대로 두되, 로그에 선택된 옵션의
  `Label`을 출력하도록 바꾼다.

### `AdvanceState()` 가드
맨 앞에 `if (_outcome != RunOutcome.InProgress) return;` 추가.

---

# 설계 결정 (이대로 따를 것)

- **성공/실패 판정은 `RunController` 안에서만** 한다. UI는 `Outcome`을 읽기만 한다.
- 재시작은 `ResetRun()` 하나로 끝나야 한다. 별도 경로를 만들지 마라.
- 밸런스 수치는 전부 `PrototypeConfig`. 하드코딩 금지.
- 난수는 시드 기반. `UnityEngine.Random` 금지.
- 추가 상승으로 `targetFloor`를 건너뛰어도 성공으로 친다 (`>=` 비교).
- 참조 null 방어. NullReferenceException 금지.
- `Debug.Log` 접두사 `[상승]`.

# Unity 제약 (위반 금지)

- `.meta` 직접 생성/수정 금지
- `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/` 수정 금지
- `ProjectSettings/`, `*.unity`, `*.prefab`, `*.asset` 직접 편집 금지
- `dotnet build` / `csc` 실행 금지 — 컴파일 확인은 다른 쪽에서 한다

# 완료 조건

- [ ] `RunOutcome`, `OverchargeOption`, `OverchargeMode` 생성
- [ ] `FloorMath.BuildMoneyOption/BuildAscendOption` 구현, 0으로 나누기 방어
- [ ] 두 선택지가 `RunController.MoneyOption/AscendOption`으로 노출됨
- [ ] Ascend 선택이 실제로 여러 층을 올리고 남은 전력을 이월함
- [ ] Money 선택이 돈을 늘리고 1층만 올림
- [ ] `targetFloor` 도달 시 Success, 재시도 초과 시 Failure
- [ ] 종료 후 Space/통관 입력이 무시되고 [R]만 동작
- [ ] `ResetRun()`이 신규 필드를 포함해 전부 초기화
- [ ] C# 문법 오류 없음

# 범위 밖 (건드리지 마라)

- `PrototypeUI.cs` — **절대 손대지 마라.** 다음 티켓에서 한 번에 갱신한다
- `TubeController` 스크롤 수학
- 효과 파이프라인 내부 로직 (T-03)
- 승객 데이터·매니저 내부 로직 (T-04)
- 씬, 프리팹, 카메라, 조명, 머티리얼
- 자동 시뮬레이션 하네스 (T-08) — 다음 티켓
