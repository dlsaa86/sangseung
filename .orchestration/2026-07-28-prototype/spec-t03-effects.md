# 목표

Unity 프로젝트 `Upandup_DDD`의 T-03 티켓 구현: **범용 연쇄 효과 처리기**.
현재 `EffectResolver`는 아무것도 하지 않는 스텁이다. 이것을 데이터 중심(ScriptableObject)
연쇄 효과 파이프라인으로 재작성한다.

완료 = 7종 효과가 정해진 순서로 계산되고, 입력·중간·최종값이 로그로 남고,
무한 루프가 방지되며, 컴파일 에러 0.

---

# 프로젝트 배경 (반드시 읽을 것)

- Unity 6000.5.5f1 / URP. 네임스페이스는 `Ascend.Prototype`.
- **asmdef이 없다.** 모든 스크립트가 `Assembly-CSharp` 하나에 들어간다.
- 게임: 엘리베이터가 층을 오른다. 한 층마다 3번의 "발전 턴"이 있고, 매 턴
  세 개의 수직 통관에서 떨어지는 구슬을 각각 멈춰 3개를 수확한다.
  3구슬 조합이 전력으로 환산되고, 요구 전력을 넘기면 다음 층으로 상승한다.
- 구슬은 9종, 등급은 `Common / Advanced / Rare / Legendary` (`BallGrade`).
- 기존 파일은 이미 읽을 수 있다. 특히 다음을 먼저 읽어라:
  - `Assets/Prototype_Elevator/Scripts/Effects/*.cs`
  - `Assets/Prototype_Elevator/Scripts/Roulette/CombinationResolver.cs`
  - `Assets/Prototype_Elevator/Scripts/Core/RunController.cs`
  - `Assets/Prototype_Elevator/Scripts/Data/*.cs`

---

# 변경 대상

## 신규 파일

### `Assets/Prototype_Elevator/Scripts/Effects/GenerationContext.cs`
효과 파이프라인이 관통하는 가변 상태 컨테이너. **순수 C# 클래스** (MonoBehaviour 아님).

```csharp
public class GenerationContext
{
    // 입력
    public List<BallDefinition> Balls;      // 수확된 3구슬. Convert/Copy/Remove가 변형한다
    public CombinationType Combination;     // CombinationResolver가 판정한 타입
    public float BaseOutputSum;             // 구슬 baseOutput 합
    public float CombinationBaseScore;      // 조합 기본 점수
    public float CombinationMultiplier;     // 조합 배수

    // 누산기
    public float FlatBonus;                 // Add 효과 누적
    public float MultiplierBonus;           // Multiply 효과 누적 (초기 1)
    public float MoneyDelta;                // Convert 효과가 만든 돈

    // 조건 플래그
    public bool  IsOverloaded;
    public bool  PerfectStop;               // 세 통관 모두 완벽 정지했는가
    public int   TurnIndex;
    public int   FloorIndex;

    // 산출
    public float FinalPower;
    public readonly List<EffectLogEntry> Log;

    public float ComputeCurrentPower();     // (BaseOutputSum + CombinationBaseScore + FlatBonus) * CombinationMultiplier * MultiplierBonus
    public GenerationContext Clone();       // Repeat 처리에 쓸 얕은 복제 (Balls는 새 리스트)
}
```

### `Assets/Prototype_Elevator/Scripts/Effects/EffectLogEntry.cs`
```csharp
[Serializable]
public struct EffectLogEntry
{
    public int    Order;        // 처리 순번 (0부터)
    public int    Depth;        // Repeat 재귀 깊이
    public string EffectId;
    public string EffectName;
    public EffectType Type;
    public float  PowerBefore;
    public float  PowerAfter;
    public bool   Applied;      // 조건/확률 불충족으로 스킵됐으면 false
    public string Note;         // "확률 0.30 실패", "Ball_02 -> Ball_07" 등
    public string ToDisplayString();  // UI 한 줄 표기용
}
```

### `Assets/Prototype_Elevator/Scripts/Effects/EffectDefinition.cs`
`[CreateAssetMenu(menuName = "Ascend/EffectDefinition")]` ScriptableObject.

```csharp
public string     id;                  // 고유 식별자
public string     displayName;         // UI 표기명
public EffectType type;
[Range(0,100)] public int priority;    // 같은 type 내 처리 순서. 작을수록 먼저
public EffectTrigger  trigger;
public EffectCondition condition;
public BallGrade  conditionGrade;      // condition == ContainsGrade 일 때만 사용
[Range(0f,1f)] public float probability = 1f;  // 발동 확률. 1이면 항상
public float      value;               // Add=가산량, Multiply=배수, Convert=환산비, Probability=가중치배수
public string     targetBallId;        // Convert/Copy/Remove 대상
public string     resultBallId;        // Convert 결과
[Range(1,5)] public int repeatCount = 1;  // Repeat 전용
[TextArea] public string description;
```

### `Assets/Prototype_Elevator/Scripts/Effects/EffectTrigger.cs`
```csharp
public enum EffectTrigger { OnHarvest, OnCombination, OnGeneration, OnFinal, OnOverload }
```

### `Assets/Prototype_Elevator/Scripts/Effects/EffectCondition.cs`
```csharp
public enum EffectCondition { None, Overloaded, NotOverloaded, PerfectStop, ContainsGrade }
```

### `Assets/Prototype_Elevator/Scripts/Effects/IEffectHandler.cs`
```csharp
public interface IEffectHandler
{
    EffectType Type { get; }
    // 효과를 ctx에 적용한다. note에 사람이 읽을 설명을 채운다.
    // 반환값: 실제로 적용됐으면 true
    bool Apply(EffectDefinition def, GenerationContext ctx, IEffectRandom rng, out string note);
}
```

### `Assets/Prototype_Elevator/Scripts/Effects/IEffectRandom.cs`
```csharp
public interface IEffectRandom { double NextDouble(); }
```
`SystemEffectRandom`(System.Random 래퍼)과 `FixedEffectRandom`(항상 지정값 반환, 테스트용)
두 구현을 같은 파일에 넣어라.

### `Assets/Prototype_Elevator/Scripts/Effects/Handlers/` (신규 폴더)
`AddEffectHandler.cs`, `MultiplyEffectHandler.cs`, `ConvertEffectHandler.cs`,
`CopyEffectHandler.cs`, `RemoveEffectHandler.cs`, `ProbabilityEffectHandler.cs`,
`RepeatEffectHandler.cs` — 각각 `IEffectHandler` 구현. 파일 하나에 클래스 하나.

동작 규약:
- **Add**: `ctx.FlatBonus += def.value`
- **Multiply**: `ctx.MultiplierBonus *= def.value`
- **Convert**: `ctx.Balls` 안의 `targetBallId`와 일치하는 구슬을 `resultBallId` 구슬로 교체.
  `resultBallId`가 비어 있으면 대신 현재 전력의 `def.value` 비율만큼을 `ctx.MoneyDelta`로
  옮기고 그만큼 `FlatBonus`를 깎는다(전력→돈 변환).
- **Copy**: `ctx.Balls`에서 `targetBallId`와 일치하는 첫 구슬을 복제해 리스트에 추가.
  `targetBallId`가 비면 마지막 구슬을 복제. 복제 후 `BaseOutputSum`을 그 구슬 baseOutput만큼 증가.
- **Remove**: `targetBallId`와 일치하는 첫 구슬을 제거하고 `BaseOutputSum`을 그만큼 감소.
- **Probability**: 이 효과는 구슬 등장 확률을 바꾸는 용도라 파이프라인 안에서는
  전력을 직접 바꾸지 않는다. `ctx.Log`에만 기록하고 `PendingProbabilityModifiers`
  리스트(GenerationContext에 `List<EffectDefinition>`로 추가)에 담아 둔다.
  RouletteController가 나중에 소비할 수 있게 하는 것이 목적이다. Applied=true로 기록.
- **Repeat**: 파이프라인 전체를 다시 실행하도록 요청한다. 핸들러 자체는 ctx에
  `RepeatRequested += def.repeatCount` 만 표시하고, 실제 재실행은 `EffectPipeline`이 한다.

### `Assets/Prototype_Elevator/Scripts/Effects/EffectPipeline.cs`
**순수 C# 클래스.** MonoBehaviour 아님. 이게 T-03의 핵심이다.

```csharp
public class EffectPipeline
{
    public EffectPipeline(EffectResolverSettings settings, IEffectRandom rng);
    public GenerationContext Run(GenerationContext ctx, IReadOnlyList<EffectDefinition> effects);
}
```

처리 순서 — **반드시 이 순서를 지킬 것**:
1. `EffectType` 기준 1차 정렬. 고정 순서: `Probability → Remove → Convert → Copy → Add → Multiply → Repeat`
2. 같은 EffectType 안에서는 `priority` 오름차순, 동률이면 리스트 등장 순서(안정 정렬)
3. 각 효과마다:
   a. `trigger`가 현재 단계와 맞는지 확인 (맞지 않으면 스킵, 로그에 Applied=false)
   b. `condition` 평가 (불충족이면 스킵, 로그 기록)
   c. `probability` 롤 (`rng.NextDouble() < probability`) — 실패 시 스킵, 로그 기록
   d. 핸들러 `Apply` 호출
   e. `PowerBefore`/`PowerAfter`를 `ctx.ComputeCurrentPower()`로 채워 로그에 push
4. 전부 끝나면 `ctx.FinalPower = ctx.ComputeCurrentPower()`
5. `ctx.RepeatRequested > 0` 이면 깊이를 1 늘려 1~4를 다시 실행

**무한 루프 방지 — 세 가지를 모두 구현할 것:**
- `settings.maxRecursionDepth` (기본 3) 초과 시 중단하고 `Debug.LogWarning`
- `settings.maxTotalActivations` (기본 64) 총 발동 횟수 초과 시 중단
- 순환 감지: `(effectId, depth)` 쌍을 `HashSet<string>`에 넣어 같은 깊이에서 같은 효과가
  두 번 발동하려 하면 스킵하고 로그에 남긴다
중단할 때는 예외를 던지지 말고 로그를 남기고 정상 반환한다.

### `Assets/Prototype_Elevator/Scripts/Effects/EffectResolverSettings.cs`
`[CreateAssetMenu(menuName = "Ascend/EffectResolverSettings")]` ScriptableObject.
`maxRecursionDepth`(기본 3), `maxTotalActivations`(기본 64), `verboseLogging`(기본 true).

## 수정 파일

### `Assets/Prototype_Elevator/Scripts/Effects/EffectResolver.cs`
MonoBehaviour는 **얇은 어댑터로만** 남긴다. 계산은 전부 `EffectPipeline`에 위임.

```csharp
[SerializeField] private EffectResolverSettings _settings;
[SerializeField] private List<EffectDefinition> _globalEffects = new();

public void InitializeSeed(int seed);                       // 내부 IEffectRandom 생성
public void SetActiveEffects(IReadOnlyList<EffectDefinition> effects);  // 승객 효과 주입용(T-04)
public GenerationContext Resolve(GenerationContext ctx);    // 파이프라인 실행 후 ctx 반환
public IReadOnlyList<EffectLogEntry> LastLog { get; }
public string BuildLogText();                               // UI 표기용 여러 줄 문자열

// 기존 호출부 호환을 위해 남긴다. 내부적으로 아무것도 하지 않는다.
public void ResolveEffects();
```

`_settings`가 null이면 코드 기본값으로 동작하는 폴백을 넣어라 (씬 참조 누락 시 NullReference 금지).

### `Assets/Prototype_Elevator/Scripts/Roulette/CombinationResolver.cs`
`Resolve()`가 지금은 전력까지 계산한다. 다음을 **추가**한다 (기존 메서드는 남겨 둘 것):

```csharp
// 효과 파이프라인에 넘길 컨텍스트를 만든다. 전력은 아직 확정하지 않는다.
public GenerationContext BuildContext(IReadOnlyList<BallDefinition> balls,
                                      bool isOverloaded, bool perfectStop,
                                      int turnIndex, int floorIndex);
```
내부적으로 `DetermineType`을 재사용하고, `BaseOutputSum` / `CombinationBaseScore` /
`CombinationMultiplier`를 채워 반환한다.

### `Assets/Prototype_Elevator/Scripts/Core/RunController.cs`
`ResolveGenerationTurn()` 만 수정한다. 다른 메서드는 건드리지 마라.

```csharp
private void ResolveGenerationTurn()
{
    IReadOnlyList<BallDefinition> balls = _roulette.CollectResults();

    bool perfectStop = /* 지금은 항상 false로 두고 TODO 주석. T-04에서 채운다 */;
    var ctx = _resolver.BuildContext(balls, _state.IsOverloaded, perfectStop,
                                     _state.CurrentTurn, _floor.CurrentFloor);
    ctx = _effects.Resolve(ctx);

    _state.Power += ctx.FinalPower;
    _state.Money += ctx.MoneyDelta;
    _state.LastGenerationPower = ctx.FinalPower;
    _state.LastRollSummary = /* 기존 요약 + 조합 타입 */;
    _state.LastEffectLog = _effects.BuildLogText();   // ElevatorState에 필드 추가
    _turnResolved = true;
    // 기존 Debug.Log 형식 유지
}
```
`ResetRun()`에 `_effects.InitializeSeed(_config.randomSeed);` 한 줄을 추가한다.

### `Assets/Prototype_Elevator/Scripts/Core/ElevatorState.cs`
`public string LastEffectLog;` 필드를 추가하고 `Initialize()`에서 `string.Empty`로 초기화.

### 신규 `Assets/Editor/EffectAssetGenerator.cs`
`Assets/Editor/` 아래에 에디터 전용 스크립트. `#if UNITY_EDITOR` 불필요 (Editor 폴더).
메뉴 `Ascend/Generate Effect Assets` 를 만들고, 실행하면
`Assets/Prototype_Elevator/Data/Effects/` 폴더에 아래 EffectDefinition 에셋을 생성한다
(이미 있으면 덮어쓰지 말고 건너뛰고 로그를 남길 것):

| id | displayName | type | trigger | condition | value | 비고 |
|---|---|---|---|---|---|---|
| EFF_TECHNICIAN_ADD | 기술자 발전 보너스 | Add | OnGeneration | None | 2 | |
| EFF_TRANSFORMER_MUL | 변압기 기사 증폭 | Multiply | OnFinal | None | 2 | |
| EFF_GAMBLER_REPEAT | 도박사 재발동 | Repeat | OnFinal | PerfectStop | 0 | repeatCount 1 |
| EFF_ZEALOT_OVERLOAD_MUL | 과적 광신도 | Multiply | OnFinal | Overloaded | 2 | |
| EFF_TEST_LEGENDARY_ADD | 전설 가산(테스트) | Add | OnCombination | ContainsGrade | 15 | conditionGrade = Legendary |

또한 `EffectResolverSettings` 에셋을 `Assets/Prototype_Elevator/Data/EffectResolverSettings.asset`로 생성한다.
`AssetDatabase.CreateAsset` + `AssetDatabase.SaveAssets` + `Refresh`를 쓸 것.

---

# 설계 결정 (이대로 따를 것)

- **핸들러 레지스트리 구조를 반드시 쓸 것.** 새 효과 추가 = 새 핸들러 파일 1개 +
  `EffectType`에 값 1개. `EffectPipeline` 본문은 손대지 않아도 되어야 한다.
  `EffectPipeline` 생성자에서 `Dictionary<EffectType, IEffectHandler>`를 채운다.
- 밸런스 수치를 코드에 하드코딩하지 마라. 전부 `EffectDefinition` / `EffectResolverSettings`.
- `EffectPipeline`, `GenerationContext`, 모든 핸들러는 **MonoBehaviour가 아니어야 한다.**
  나중에 에디터에서 씬 없이 시뮬레이션을 돌릴 것이기 때문이다.
- 난수는 반드시 `IEffectRandom`을 통해서만 쓴다. `UnityEngine.Random` 직접 호출 금지.
  같은 시드 → 같은 결과가 보장되어야 한다.
- 기존 public API를 깨지 마라. `CombinationResolver.Resolve()`, `RouletteController`,
  `TubeController`의 시그니처는 그대로 둔다.
- `Debug.Log` 접두사는 기존과 동일하게 `[상승]`을 쓴다.
- XML 문서 주석은 기존 파일들과 같은 밀도로 영어로 작성한다. 인라인 주석은
  "왜"를 설명할 때만 쓴다.

# Unity 제약 (위반 금지)

- `.meta` 파일을 직접 만들거나 수정하지 마라. Unity가 생성한다.
- `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/` 를 수정하지 마라.
- `ProjectSettings/`, `*.unity` 씬, `*.prefab`, `*.asset` 을 **직접 편집하지 마라.**
  에셋이 필요하면 위의 `EffectAssetGenerator` 처럼 에디터 스크립트로 생성한다.
- 컴파일 확인은 네가 하지 않는다. Unity 에디터를 띄운 쪽에서 한다.
  `dotnet build`나 `csc`를 실행하려 하지 마라.

# 완료 조건

- [ ] 위 신규 파일이 전부 생성됨
- [ ] `EffectPipeline`이 7종 효과를 명시된 고정 순서로 처리
- [ ] 재귀 깊이 / 총 발동 횟수 / 순환 감지 3중 방어가 모두 구현됨
- [ ] `EffectLogEntry`에 입력·중간·최종 전력이 기록됨
- [ ] `EffectResolver.BuildLogText()`가 사람이 읽을 수 있는 여러 줄 문자열 반환
- [ ] `RunController.ResolveGenerationTurn()`이 새 파이프라인을 통과
- [ ] `EffectAssetGenerator` 메뉴 항목 존재
- [ ] 참조가 null이어도 NullReferenceException이 나지 않는 폴백 존재
- [ ] C# 문법 오류 없음 (직접 컴파일은 하지 않아도 된다)

# 범위 밖 (건드리지 마라)

- 승객 시스템 (T-04) — `PassengerDefinition` 등을 만들지 마라. 다음 티켓이다.
- 과적 사고 확률 (T-06)
- 10층 런 / 성공·실패 화면 (T-07)
- `PrototypeUI.cs` — UI는 별도 티켓에서 한 번에 갱신한다. 손대지 마라.
- 씬 배치, 카메라, 조명, 머티리얼
- 기존 `TubeController`의 스크롤 수학 — 정상 동작 중이다. 절대 손대지 마라.
