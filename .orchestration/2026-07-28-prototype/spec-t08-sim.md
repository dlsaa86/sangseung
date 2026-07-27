# 목표

T-08: **씬 없이 돌아가는 자동 플레이테스트 시뮬레이터**와 로그 산출을 구현한다.
완료 = 에디터 메뉴 한 번으로 10회 이상의 런을 시뮬레이션하고, 층별 상세 기록을
CSV와 JSON으로 저장하며, 요약 통계를 콘솔에 출력한다. 컴파일 에러 0.

또한 핵심 계산의 **자동 검증 테스트**를 포함한다.

---

# 프로젝트 배경

- Unity 6000.5.5f1 / URP. 네임스페이스 `Ascend.Prototype`. asmdef 없음(Assembly-CSharp 단일).
- 앞선 티켓들에서 다음이 구현되어 있다. **작업 전에 반드시 실제 파일을 읽어라.**
  시그니처를 추측하지 마라:
  - `Assets/Prototype_Elevator/Scripts/Effects/` (EffectPipeline, GenerationContext, EffectDefinition, IEffectRandom, EffectResolverSettings)
  - `Assets/Prototype_Elevator/Scripts/Core/` (FloorMath, RunController, ElevatorState, PassengerManager, RunOutcome, OverchargeOption)
  - `Assets/Prototype_Elevator/Scripts/Roulette/` (CombinationResolver, CombinationType)
  - `Assets/Prototype_Elevator/Scripts/Data/` (PrototypeConfig, BallDatabase, BallDefinition, PassengerDefinition, CombinationConfig)

**핵심 제약:** 시뮬레이터는 MonoBehaviour나 씬에 의존하면 안 된다.
`GameObject`를 만들지 말고, `FindObjectOfType`을 쓰지 마라.
ScriptableObject는 에디터에서 `AssetDatabase.LoadAssetAtPath`로 읽어 주입한다.

---

# 변경 대상

## 1. 순수 계산 로직 추출

### 신규 — `Assets/Prototype_Elevator/Scripts/Roulette/CombinationEvaluator.cs`

`CombinationResolver`(MonoBehaviour) 안의 판정 로직을 **순수 static 클래스로 추출**한다.

```csharp
public static class CombinationEvaluator
{
    public static CombinationType DetermineType(IReadOnlyList<BallDefinition> balls);
    public static float ComputePower(CombinationConfig cfg, IReadOnlyList<BallDefinition> balls, CombinationType type);
    public static string BuildSummary(IReadOnlyList<BallDefinition> balls, CombinationType type, float power);
}
```

- 판정 우선순위와 공식은 **기존 `CombinationResolver`와 완전히 동일해야 한다.**
  로직을 바꾸지 마라. 옮기기만 해라.
- `CombinationResolver`는 이 클래스에 **위임**하도록 수정한다.
  기존 public 시그니처(`Resolve`, `BuildContext`)는 그대로 유지한다.
- `balls`가 null이거나 3개 미만이면 `CombinationType.None` 반환.

### 신규 — `Assets/Prototype_Elevator/Scripts/Data/BallDrawer.cs`

**순수 클래스.** 가중치 기반 구슬 추첨. `RouletteController`의 `DrawWeighted`와
**동일한 알고리즘**이어야 한다(재현성 비교를 위해).

```csharp
public class BallDrawer
{
    public BallDrawer(BallDatabase db, System.Random rng);
    public BallDefinition Draw();
    public List<BallDefinition> DrawMany(int count);
    // 확률 합계 검증용
    public static float SumProbabilities(BallDatabase db);
}
```

## 2. 시뮬레이터 본체

### 신규 — `Assets/Prototype_Elevator/Scripts/Sim/SimPolicy.cs`

시뮬레이션 중 "플레이어가 어떻게 행동하는가"를 데이터로 표현한다.
같은 빌드에서도 정책을 바꿔 비교할 수 있어야 한다.

```csharp
[Serializable]
public class SimPolicy
{
    public string name = "Balanced";

    [Tooltip("후보 승객을 태울 확률 (0~1).")]
    public float boardChance = 0.6f;

    [Tooltip("총무게가 허용 중량의 이 비율을 넘으면 더 태우지 않는다.")]
    public float weightCeilingRatio = 1.0f;

    [Tooltip("세 통관을 완벽 정지시킬 확률. 사람의 조작 숙련도를 대신한다.")]
    public float perfectStopChance = 0.25f;

    [Tooltip("초과 전력을 추가 상승에 쓸 확률. 나머지는 돈으로.")]
    public float ascendChance = 0.5f;
}
```

### 신규 — `Assets/Prototype_Elevator/Scripts/Sim/SimRecords.cs`

기록 자료구조. 전부 `[Serializable]` (JsonUtility로 직렬화한다).

```csharp
[Serializable] public class SimTurnRecord {
    public int    turnIndex;
    public string ball0, ball1, ball2;      // id
    public string grade0, grade1, grade2;
    public bool   perfectStop;
    public string combination;
    public float  powerBeforeEffects;
    public float  powerAfterEffects;
    public float  moneyDelta;
    public string effectLog;                // 여러 줄을 ' | '로 이어붙인 것
}

[Serializable] public class SimFloorRecord {
    public int    floorIndex;
    public string candidatesOffered;        // "기술자(무게2), 도박사(무게3)"
    public string passengerBoarded;         // 없으면 "-"
    public float  totalWeight;
    public float  allowedWeight;
    public bool   overloaded;
    public float  accidentChance;
    public bool   accidentOccurred;
    public float  accidentLoss;
    public float  requiredPower;
    public float  finalPower;
    public bool   success;
    public int    retries;
    public float  surplus;
    public string overchargeChoice;         // "돈 +120" / "추가 상승 +2층"
    public int    floorsClimbed;
    public List<SimTurnRecord> turns = new();
}

[Serializable] public class SimRunRecord {
    public int    runIndex;
    public int    seed;
    public string policyName;
    public string outcome;                  // Success / Failure
    public string failureReason;
    public int    highestFloor;
    public float  finalMoney;
    public int    totalAccidents;
    public int    totalRetries;
    public List<SimFloorRecord> floors = new();
}

[Serializable] public class SimBatchResult {
    public string generatedAtUtc;
    public int    runCount;
    public int    successCount;
    public float  averageHighestFloor;
    public float  averageMoney;
    public float  averageAccidents;
    public List<SimRunRecord> runs = new();
}
```

### 신규 — `Assets/Prototype_Elevator/Scripts/Sim/RunSimulator.cs`

**순수 C# 클래스.** 이게 T-08의 핵심이다.

```csharp
public class RunSimulator
{
    public RunSimulator(PrototypeConfig config,
                        BallDatabase ballDb,
                        CombinationConfig comboConfig,
                        EffectResolverSettings effectSettings,
                        IReadOnlyList<PassengerDefinition> passengerPool);

    public SimRunRecord RunOnce(int seed, SimPolicy policy, int runIndex);
}
```

`RunOnce`가 재현해야 하는 흐름 — **실제 `RunController`와 같은 순서**여야 한다:

```
런 초기화 (전 상태 리셋, 유도 시드로 RNG 4종 생성:
    구슬용 seed, 효과용 unchecked(seed*397^0x5EED),
    승객용 unchecked(seed*31^0x9A55), 사고용 unchecked(seed*17^0x2ACC))

while (outcome == InProgress):
    [층 도착]
        후보 승객 생성 (승객 RNG)
    [승객 선택]
        정책에 따라 탑승 결정
        태우면 무게/허용중량/요구전력/사고확률 갱신 (FloorMath 사용)
    [발전 턴 × config.generationsPerFloor]
        구슬 3개 추첨 (BallDrawer)
        perfectStop = 정책 확률로 판정
        CombinationEvaluator로 조합 판정
        GenerationContext 구성 → EffectPipeline 실행
        전력·돈 누적, SimTurnRecord 기록
    [전력 판정]
        사고 굴림 (사고 RNG) → 발생 시 전력 손실 기록
        전력 >= 요구전력 ?
            성공 → surplus 계산
            실패 → 재시도 증가. maxRetriesPerFloor 초과면 런 실패 종료
    [초과 전력 분배]
        정책 ascendChance로 Money/Ascend 선택
        FloorMath.BuildMoneyOption / BuildAscendOption 사용
    [상승]
        층 += 1 + 추가상승층수
        층 >= config.targetFloor 면 런 성공 종료
```

- **모든 공식은 `FloorMath`를 호출해서 쓴다.** 공식을 다시 적지 마라.
  중복 구현하면 실제 게임과 시뮬레이션이 어긋난다. 이게 이 티켓에서 가장 중요한 규칙이다.
- 무한 루프 방지: 총 층 반복이 `config.targetFloor * (config.maxRetriesPerFloor + 2) + 20`을
  넘으면 강제 종료하고 `failureReason = "시뮬레이션 반복 상한 도달"`로 기록.

## 3. 에디터 진입점

### 신규 — `Assets/Editor/PlaytestSimWindow.cs`

메뉴 `Ascend/Run Playtest Simulation`.

- `AssetDatabase.LoadAssetAtPath`로 다음을 로드한다. 경로가 다르면
  `AssetDatabase.FindAssets("t:PrototypeConfig")` 같은 방식으로 찾아라:
  - `PrototypeConfig`, `BallDatabase`, `CombinationConfig`, `EffectResolverSettings`
  - `PassengerDefinition` 전부 (`AssetDatabase.FindAssets("t:PassengerDefinition")`)
- 하나라도 못 찾으면 `Debug.LogError`로 무엇이 없는지 정확히 알리고 중단한다.
- **12회** 런을 돌린다. 시드는 `baseSeed + i` (baseSeed는 `config.randomSeed`).
  정책은 3종을 섞어 쓴다:
  - `경량형`: boardChance 0.35, weightCeilingRatio 0.8, perfectStopChance 0.25, ascendChance 0.3
  - `균형형`: boardChance 0.60, weightCeilingRatio 1.0, perfectStopChance 0.25, ascendChance 0.5
  - `과적형`: boardChance 0.90, weightCeilingRatio 1.6, perfectStopChance 0.25, ascendChance 0.7
- 산출물을 `<프로젝트루트>/PlaytestLogs/` 에 쓴다 (없으면 생성):
  - `sim_<yyyyMMdd_HHmmss>.json` — `SimBatchResult`를 `JsonUtility.ToJson(_, true)`로
  - `sim_<yyyyMMdd_HHmmss>_floors.csv` — 층 단위 한 줄씩
  - `sim_<yyyyMMdd_HHmmss>_turns.csv` — 발전 턴 단위 한 줄씩
- CSV는 **UTF-8 BOM 포함**으로 쓴다 (`new UTF8Encoding(true)`).
  한글이 엑셀에서 깨지지 않게 하기 위함이다.
- CSV 필드에 쉼표/따옴표/개행이 들어갈 수 있으므로 반드시 escape 한다
  (`"`로 감싸고 내부 `"`는 `""`로).
- 콘솔에 요약을 출력한다: 정책별 성공률, 평균 최고 층, 평균 돈, 평균 사고 횟수,
  실패 원인 분포.

## 4. 자동 검증 테스트

### 신규 — `Assets/Editor/PrototypeSelfTest.cs`

메뉴 `Ascend/Run Self Tests`. **NUnit에 의존하지 마라.** 테스트 프레임워크 패키지가
없을 수 있다. 단순한 assert 헬퍼를 직접 만들고, 실패 건수를 콘솔에 출력한다.
성공은 `Debug.Log`, 실패는 `Debug.LogError`.

검증할 항목 — **전부 구현할 것**:
1. **구슬 확률 합계** — `BallDrawer.SumProbabilities(db)`가 100 ± 0.01
2. **구슬 종류·등급 분포** — 일반 3종/고급 3종/희귀 2종/전설 1종인지
3. **요구 전력 계산** — `FloorMath.ComputeRequiredPower`가
   (기본 + 층×증가 + 무게×계수) × 과적배수 와 일치 (직접 계산한 기대값과 비교)
4. **초과 전력 계산** — `BuildAscendOption`의 `FloorsGained`가 상한을 넘지 않고,
   `SurplusUsed + PowerCarried == surplus` (부동소수 허용오차 0.001)
5. **효과 적용 순서** — Add와 Multiply를 섞은 EffectDefinition 리스트를 만들어
   파이프라인에 넣고, 결과가 `(base + add) * mul` 이 되는지 확인
   (`(base * mul) + add`가 아니어야 한다)
6. **무한 Repeat 방지** — `repeatCount`를 크게 준 Repeat 효과를 넣어도
   파이프라인이 반환되고, 총 발동 횟수가 `maxTotalActivations` 이하인지
7. **재시작 후 상태 초기화** — `ElevatorState`를 오염시킨 뒤 `Initialize(config)`를
   호출하면 모든 수치 필드가 config 기본값으로, 문자열 필드가 빈 문자열로 돌아가는지
   (리플렉션 없이 필드를 직접 확인해도 된다)
8. **같은 시드 재현성** — 같은 시드로 `RunSimulator.RunOnce`를 두 번 돌렸을 때
   `outcome`, `highestFloor`, `finalMoney`, 층 수, 각 층의 `finalPower`가 모두 동일한지.
   **이 테스트가 실패하면 어디가 다른지 첫 불일치 지점을 로그로 찍어라.**

---

# 설계 결정 (이대로 따를 것)

- **공식 중복 구현 금지.** 요구 전력·사고 확률·초과 전력은 반드시 `FloorMath`,
  조합 판정은 반드시 `CombinationEvaluator`, 효과는 반드시 `EffectPipeline`을 통한다.
  시뮬레이터가 자기 버전의 공식을 갖는 순간 이 티켓은 무의미해진다.
- 시뮬레이터와 기록 클래스는 `Assets/Prototype_Elevator/Scripts/Sim/` 에 둔다
  (런타임 어셈블리 — 에디터 전용 API 사용 금지).
- 에디터 진입점만 `Assets/Editor/` 에 둔다.
- 난수는 전부 `System.Random`. `UnityEngine.Random` 금지.
- 밸런스 수치 하드코딩 금지. 정책 수치는 `SimPolicy`에.
- `Debug.Log` 접두사 `[상승]`.

# Unity 제약 (위반 금지)

- `.meta` 직접 생성/수정 금지
- `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/` 수정 금지
- `ProjectSettings/`, `*.unity`, `*.prefab`, `*.asset` 직접 편집 금지
- `dotnet build` / `csc` 실행 금지
- `Assets/Prototype_Elevator/Scripts/Sim/` 안에서 `UnityEditor` 네임스페이스를
  쓰지 마라 (빌드가 깨진다)

# 완료 조건

- [ ] `CombinationEvaluator` 추출, `CombinationResolver`가 위임, 기존 시그니처 유지
- [ ] `BallDrawer` 생성
- [ ] `RunSimulator`가 씬 없이 전체 런을 돌린다
- [ ] 시뮬레이터가 `FloorMath`/`CombinationEvaluator`/`EffectPipeline`을 재사용한다
- [ ] 반복 상한으로 무한 루프를 막는다
- [ ] `Ascend/Run Playtest Simulation` 메뉴가 12런을 돌리고 JSON + CSV 2종을 쓴다
- [ ] CSV가 UTF-8 BOM이고 필드 escape가 된다
- [ ] `Ascend/Run Self Tests` 메뉴가 위 8개 항목을 전부 검증한다
- [ ] C# 문법 오류 없음

# 범위 밖 (건드리지 마라)

- `PrototypeUI.cs`
- `TubeController` 스크롤 수학
- 씬, 프리팹, 카메라, 조명, 머티리얼
- 효과·승객·런 로직의 **동작 변경** — 추출과 위임은 하되 결과가 달라지면 안 된다
