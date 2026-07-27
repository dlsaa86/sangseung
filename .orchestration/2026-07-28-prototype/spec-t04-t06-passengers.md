# 목표

T-04(승객 5종과 무게 선택) + T-06(과적 사고)을 구현한다.
두 티켓은 "무게"라는 하나의 축을 공유하므로 함께 구현한다.

완료 = 승객을 태우면 무게·요구 전력·효과가 즉시 갱신되고, 과적 시 사고 확률이
플레이어에게 미리 공개되며, 사고가 나면 원인과 손실량이 표시된다. 컴파일 에러 0.

---

# 프로젝트 배경

- Unity 6000.5.5f1 / URP. 네임스페이스 `Ascend.Prototype`. asmdef 없음(Assembly-CSharp 단일).
- 직전 티켓 T-03에서 효과 파이프라인이 구현되었다. **먼저 읽어라:**
  - `Assets/Prototype_Elevator/Scripts/Effects/` 전체 (특히 `EffectDefinition`, `EffectPipeline`, `GenerationContext`, `EffectResolver`)
  - `Assets/Prototype_Elevator/Scripts/Core/RunController.cs`
  - `Assets/Prototype_Elevator/Scripts/Core/FloorController.cs`
  - `Assets/Prototype_Elevator/Scripts/Core/ElevatorState.cs`
  - `Assets/Prototype_Elevator/Scripts/Data/PrototypeConfig.cs`
  - `Assets/Prototype_Elevator/Scripts/Roulette/TubeController.cs`
- 승객은 **효과와 무게를 동시에** 가진다. 강해질수록 무거워지고 사고 위험이 오른다.
  이 트레이드오프가 이 티켓의 전부다.

---

# 변경 대상

## 신규 — `Assets/Prototype_Elevator/Scripts/Data/PassengerDefinition.cs`

`[CreateAssetMenu(menuName = "Ascend/PassengerDefinition")]` ScriptableObject.

```csharp
public string id;
public string displayName;
[TextArea] public string description;      // UI에 그대로 노출되는 한 줄 설명
public float weight;                       // 총무게에 더해진다
public float allowedWeightBonus;           // 허용 중량을 늘린다 (짐꾼)
public List<EffectDefinition> effects;     // T-03 효과를 그대로 재사용
public Color debugColor = Color.white;     // 그레이박스 표시용
```

## 신규 — `Assets/Prototype_Elevator/Scripts/Core/PassengerManager.cs`

MonoBehaviour. 승객 후보 제시 / 탑승 / 상태 집계를 담당한다.

```csharp
[SerializeField] private PrototypeConfig _config;
[SerializeField] private List<PassengerDefinition> _pool;   // 승객 5종 전부

public void InitializeSeed(int seed);          // 유도 시드 사용: unchecked(seed * 31 ^ 0x9A55)
public void ResetPassengers();                 // 탑승자 전원 하차, 후보 초기화
public void GenerateCandidates(int floorIndex);// 이 층의 후보를 시드 기반으로 뽑는다
public IReadOnlyList<PassengerDefinition> Candidates { get; }
public IReadOnlyList<PassengerDefinition> Boarded { get; }
public bool CanBoard(PassengerDefinition p);   // 슬롯 여유 확인
public bool Board(int candidateIndex);         // 성공 시 true. 후보에서 제거
public float TotalWeight { get; }              // Boarded 의 weight 합
public float TotalAllowedWeightBonus { get; }  // Boarded 의 allowedWeightBonus 합
public IReadOnlyList<EffectDefinition> ActiveEffects { get; }  // Boarded 의 effects 를 평탄화
```

- 후보 생성은 `_pool`에서 **중복 없이** `_config.passengerCandidatesPerFloor` 개를 뽑는다.
  풀이 후보 수보다 작으면 있는 만큼만.
- 난수는 반드시 내부 `System.Random`으로만. `UnityEngine.Random` 사용 금지 (재현성).
- `_pool`이 비어 있거나 `_config`가 null이어도 예외를 던지지 마라. 빈 결과 + 경고 로그.
- **탑승 슬롯 상한**은 `_config.maxPassengerSlots`.

## 신규 — `Assets/Prototype_Elevator/Scripts/Core/FloorMath.cs`

**순수 static 클래스.** MonoBehaviour 아님. 나중에 씬 없이 시뮬레이션을 돌릴 때
`FloorController`와 시뮬레이터가 **같은 공식**을 쓰게 하려는 것이다.

```csharp
public static float ComputeRequiredPower(PrototypeConfig cfg, int floor, float totalWeight, bool isOverloaded);
public static float ComputeAccidentChance(PrototypeConfig cfg, float weight, float allowedWeight);
public static float ComputeAccidentPowerLoss(PrototypeConfig cfg, float currentPower);
public static int   ComputeExtraFloors(PrototypeConfig cfg, float surplus);
public static float ComputeMoneyFromSurplus(PrototypeConfig cfg, float surplus);
```

공식 — **이대로 구현할 것**:
```
요구 전력 = (baseRequiredPower + floor * requiredPowerGrowthPerFloor
             + totalWeight * weightToPowerFactor)
            * (isOverloaded ? overloadRequiredPowerMultiplier : 1)

초과 중량   = max(0, weight - allowedWeight)
사고 확률   = clamp01(min(maxAccidentChance,
                          초과중량 * accidentChancePerOverweightUnit))
사고 손실   = currentPower * accidentPowerLossRatio
```

## 수정 — `Assets/Prototype_Elevator/Scripts/Data/PrototypeConfig.cs`

아래 필드를 추가한다. 기존 필드는 삭제하지 마라.

```csharp
[Header("Weight → Power")]
[Tooltip("요구 전력에 더해지는 총무게 계수. 요구전력 += 총무게 * 이 값")]
public float weightToPowerFactor = 2f;

[Header("Passengers")]
public int   maxPassengerSlots = 6;
public int   passengerCandidatesPerFloor = 2;

[Header("Overload Accident (T-06)")]
[Tooltip("초과 중량 1당 증가하는 사고 확률")]
public float accidentChancePerOverweightUnit = 0.06f;
[Tooltip("사고 확률 상한")]
[Range(0f,1f)] public float maxAccidentChance = 0.75f;
[Tooltip("사고 발생 시 잃는 현재 전력의 비율")]
[Range(0f,1f)] public float accidentPowerLossRatio = 0.35f;

[Header("Perfect Stop")]
[Tooltip("수확 구멍 중심으로부터 이 거리 안에서 멈추면 완벽 정지로 인정")]
public float perfectStopTolerance = 0.12f;
```

## 수정 — `Assets/Prototype_Elevator/Scripts/Core/FloorController.cs`

- 기존 `ComputeRequiredPower` / `UpdateRequiredPower`가 **`FloorMath`에 위임**하도록 바꾼다.
- 총무게를 인자로 받도록 시그니처를 확장한다:
  `public void UpdateRequiredPower(float totalWeight, bool isOverloaded)`
- 기존 호출부(`RunController`)도 함께 고친다.

## 수정 — `Assets/Prototype_Elevator/Scripts/Roulette/TubeController.cs`

**스크롤 수학은 절대 건드리지 마라.** 아래 한 가지만 추가한다.

- `FinalizeStop()`에서 이미 계산하는 `bestDist`를 필드에 저장하고 프로퍼티로 노출한다:
  ```csharp
  private float _lastStopDistance = float.MaxValue;
  public  float LastStopDistance => _lastStopDistance;
  ```
- `ResetTube()`/`StartScroll()`에서 `float.MaxValue`로 되돌린다.

## 수정 — `Assets/Prototype_Elevator/Scripts/Roulette/RouletteController.cs`

```csharp
/// 세 통관이 모두 perfectStopTolerance 안에서 멈췄는가.
public bool IsPerfectStop { get; }   // _config.perfectStopTolerance 사용
```
`_tubes` 중 하나라도 null이거나 미정지면 false.

## 수정 — `Assets/Prototype_Elevator/Scripts/Core/ElevatorState.cs`

추가 필드 (모두 `Initialize()`에서 초기화할 것):
```csharp
public float AccidentChance;      // 현재 사고 확률 (0~1). UI가 미리 보여준다
public bool  LastAccidentOccurred;
public float LastAccidentLoss;
public string LastAccidentCause;  // 예: "과적 12.0 초과 — 케이블 손상"
public int   BoardedCount;
```
`AllowedWeight`는 이제 `config.allowedWeight + PassengerManager.TotalAllowedWeightBonus`로
매번 갱신된다.

## 수정 — `Assets/Prototype_Elevator/Scripts/Core/RunController.cs`

### 참조 추가
`[SerializeField] private PassengerManager _passengers;`

### `ResetRun()`
- `_passengers.InitializeSeed(_config.randomSeed); _passengers.ResetPassengers();`
- 무게/허용중량/요구전력/사고확률을 `RecalculateLoad()`로 갱신 (아래 참조)

### 신규 private 메서드 `RecalculateLoad()`
승객이 타거나 내릴 때마다, 그리고 층 진입 시 호출한다. **이 한 곳에서만** 갱신한다.
```csharp
private void RecalculateLoad()
{
    _state.Weight        = _config.startingWeight + _passengers.TotalWeight;
    _state.AllowedWeight = _config.allowedWeight  + _passengers.TotalAllowedWeightBonus;
    _state.BoardedCount  = _passengers.Boarded.Count;
    _state.AccidentChance = FloorMath.ComputeAccidentChance(_config, _state.Weight, _state.AllowedWeight);
    _floor.UpdateRequiredPower(_state.Weight, _state.IsOverloaded);
    _effects.SetActiveEffects(_passengers.ActiveEffects);
}
```

### `FloorArrival` 진입 시
`_passengers.GenerateCandidates(_floor.CurrentFloor);` 호출.

### `PassengerSelection` 상태의 입력 처리
`Update()`의 상태 게이트에 분기를 추가한다:
```
[1] → _passengers.Board(0) 성공 시 RecalculateLoad()
[2] → _passengers.Board(1) 성공 시 RecalculateLoad()
[0] → 아무도 태우지 않고 넘어감 (로그만)
```
탑승 실패(슬롯 부족 등)는 `Debug.Log`로 이유를 남긴다.
**Space는 기존대로 상태 전진**이므로 손대지 마라.

### `ResolveGenerationTurn()`
T-03에서 `perfectStop`을 `false` TODO로 두었다. 이제 채운다:
```csharp
bool perfectStop = _roulette.IsPerfectStop;
```

### `PerformPowerResolution()` — 과적 사고 판정 추가
전력 비교 **직전에** 사고를 굴린다.
```csharp
_state.LastAccidentOccurred = false;
_state.LastAccidentLoss     = 0f;
_state.LastAccidentCause    = string.Empty;

float chance = FloorMath.ComputeAccidentChance(_config, _state.Weight, _state.AllowedWeight);
_state.AccidentChance = chance;

if (chance > 0f && _accidentRng.NextDouble() < chance)
{
    float loss = FloorMath.ComputeAccidentPowerLoss(_config, _state.Power);
    _state.Power -= loss;
    _state.LastAccidentOccurred = true;
    _state.LastAccidentLoss     = loss;
    float over = Mathf.Max(0f, _state.Weight - _state.AllowedWeight);
    _state.LastAccidentCause = $"과적 {over:F1} 초과 (확률 {chance:P0}) — 전력 {loss:F1} 손실";
    Debug.LogWarning($"[상승] 과적 사고! {_state.LastAccidentCause}");
}
// 이후 기존 surplus 비교 로직
```
- `_accidentRng`는 `RunController`가 소유하는 `System.Random`.
  `ResetRun()`에서 `new System.Random(unchecked(_config.randomSeed * 17 ^ 0x2ACC))`로 만든다.
  **재현성을 위해 반드시 시드 기반**이어야 한다.

## 신규 — `Assets/Editor/PassengerAssetGenerator.cs`

에디터 메뉴 `Ascend/Generate Passenger Assets`.
`Assets/Prototype_Elevator/Data/Passengers/` 에 아래 5종을 생성한다.
**이미 존재하면 덮어쓰지 말고 건너뛰고 로그를 남길 것.**
`effects` 슬롯에는 T-03에서 생성된 `Assets/Prototype_Elevator/Data/Effects/` 의
해당 EffectDefinition 에셋을 `AssetDatabase.LoadAssetAtPath`로 찾아 연결한다.

| id | displayName | weight | allowedWeightBonus | 연결할 효과 | description |
|---|---|---:|---:|---|---|
| PSG_TECHNICIAN | 기술자 | 2 | 0 | EFF_TECHNICIAN_ADD | 발전마다 전력 +2 |
| PSG_TRANSFORMER | 변압기 기사 | 4 | 0 | EFF_TRANSFORMER_MUL | 최종 전력 ×2 |
| PSG_GAMBLER | 도박사 | 3 | 0 | EFF_GAMBLER_REPEAT | 완벽 정지 시 효과 재발동 |
| PSG_PORTER | 짐꾼 | 2 | 5 | (없음) | 허용 중량 +5 |
| PSG_ZEALOT | 과적 광신도 | 6 | 0 | EFF_ZEALOT_OVERLOAD_MUL | 과적 상태에서 전력 ×2 |

효과 에셋을 못 찾으면 `Debug.LogWarning`을 남기고 빈 리스트로 둔다(예외 금지).

---

# 설계 결정 (이대로 따를 것)

- **승객 효과는 T-03 `EffectDefinition`을 재사용한다.** 승객 전용 효과 시스템을 새로 만들지 마라.
- **무게·허용중량·요구전력·사고확률 갱신은 `RecalculateLoad()` 한 곳에서만** 한다.
  여러 곳에 흩뿌리면 반드시 어긋난다.
- 모든 밸런스 수치는 `PrototypeConfig`에. 코드 하드코딩 금지.
- 모든 난수는 시드 기반 `System.Random`. `UnityEngine.Random` 금지.
  각 시스템은 서로 다른 유도 시드를 쓴다(명세에 적힌 상수 그대로).
- 사고 확률은 **발생 전에** `_state.AccidentChance`로 노출되어야 한다.
  숨겨진 랜덤 패널티가 되면 이 티켓은 실패다.
- 참조가 null이어도 NullReferenceException이 나지 않게 방어한다.
- `Debug.Log` 접두사는 `[상승]`.

# Unity 제약 (위반 금지)

- `.meta` 파일 직접 생성/수정 금지
- `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/` 수정 금지
- `ProjectSettings/`, `*.unity`, `*.prefab`, `*.asset` 직접 편집 금지 —
  에셋은 위 에디터 스크립트로 생성한다
- 컴파일 확인은 네가 하지 않는다. `dotnet build`/`csc` 실행하지 마라

# 완료 조건

- [ ] `PassengerDefinition`, `PassengerManager`, `FloorMath` 생성
- [ ] `PrototypeConfig`에 신규 필드 추가 (기존 필드 보존)
- [ ] 요구 전력 = (기본 + 층×증가 + 총무게×계수) × 과적배수
- [ ] 승객 탑승 즉시 무게·허용중량·요구전력·사고확률·활성효과가 갱신됨
- [ ] `RouletteController.IsPerfectStop` 동작, 도박사 Repeat이 이걸로 발동
- [ ] 사고 확률이 발생 이전에 `ElevatorState.AccidentChance`로 노출됨
- [ ] 사고 발생 시 원인 문자열과 손실량이 기록됨
- [ ] 사고 난수가 시드 기반이라 재현 가능
- [ ] `PassengerAssetGenerator` 메뉴 존재, 5종 생성
- [ ] C# 문법 오류 없음

# 범위 밖 (건드리지 마라)

- `PrototypeUI.cs` — UI는 뒤에서 한 번에 갱신한다. **절대 손대지 마라.**
- 10층 런 / 성공·실패 화면 / 재시작 (T-07)
- 초과 전력 분배 로직 변경 (T-05) — `FloorMath`에 함수 시그니처만 만들어 두고
  내부는 구현하되, `RunController`의 기존 `ApplyOverchargeAllocation()`은 손대지 마라
- `TubeController` 스크롤 수학
- 씬 배치, 카메라, 조명, 머티리얼
