# 목표

직전 작업(T-03 효과 파이프라인)의 **설계 결함 3건**을 수정한다.
코드 리뷰에서 실제 확인된 것이다. 지금 고치지 않으면 T-04 이후 밸런스 폭주와
재현성 버그로 되돌아온다. **기능 추가는 하지 마라. 아래 3건만 고친다.**

작업 전에 반드시 아래 파일들을 읽어라:
- `Assets/Prototype_Elevator/Scripts/Effects/EffectPipeline.cs`
- `Assets/Prototype_Elevator/Scripts/Effects/GenerationContext.cs`
- `Assets/Prototype_Elevator/Scripts/Effects/Handlers/` 전체
- `Assets/Prototype_Elevator/Scripts/Effects/EffectResolver.cs`
- `Assets/Prototype_Elevator/Scripts/Roulette/CombinationResolver.cs`

---

## 결함 1 — Repeat이 누산기를 중복 오염시킨다 (심각)

**확인된 현상:** `EffectPipeline.Run()`의 while 루프가 **같은 `ctx`에 대해**
`ProcessPass`를 반복 호출한다. 그래서 Repeat 1회에 Add가 `FlatBonus`에 다시 더해지고
Multiply가 `MultiplierBonus`에 다시 곱해진다.

예: 기술자(+2)와 변압기 기사(×2)를 태운 상태에서 도박사 Repeat이 발동하면
1패스: (base+2)×2 → 2패스: (base+4)×4. **의도는 "한 번 더 발전"인데 4배가 된다.**
`maxRecursionDepth` 3이면 ×16까지 간다. 플레이어가 예측할 수 없다.

또한 `GenerationContext.Clone()`이 구현만 되어 있고 **아무 데서도 쓰이지 않는다.**

**요구 동작:** "효과 재발동"은 그 턴의 발전이 **한 번 더 일어나는 것**이다.
누산기를 다시 오염시키는 게 아니라 결과 전력을 한 번 더 버는 것이어야 한다.
도박사 Repeat 1회 = 정확히 전력 2배.

**수정 방법 — `EffectPipeline.Run()`을 아래 구조로 바꾼다:**

```
1. ctx.Log.Clear(); ctx.PendingProbabilityModifiers.Clear();
   ctx.RepeatRequested = 0; ctx.RepeatBonusPower = 0;
   AddInputLog(ctx);

2. 기본 패스:
   ProcessPass(ctx, effects, depth: 0, allowRepeatHandler: true, ...)
   float basePassPower = ctx.ComputeCurrentPower();
   int repeats = ctx.RepeatRequested;

3. 반복 패스 (repeats 번, 단 maxRecursionDepth 이내):
   for (int r = 0; r < repeats && r < maxDepth; r++)
   {
       GenerationContext pass = ctx.CloneForRepeat();
       ProcessPass(pass, effects, depth: r + 1, allowRepeatHandler: false, ...)
       ctx.RepeatBonusPower += pass.ComputeCurrentPower();
       ctx.MoneyDelta       += pass.MoneyDelta;
       foreach (var e in pass.Log) { var c = e; c.Depth = r + 1; ctx.Log.Add(c); }
   }

4. ctx.FinalPower = basePassPower + ctx.RepeatBonusPower;
   AddFinalLog(ctx, ...);
```

- `ProcessPass`에 `bool allowRepeatHandler` 파라미터를 추가한다.
  false면 `EffectType.Repeat` 효과를 건너뛴다 (로그에 Applied=false, Note="반복 패스에서는 재발동 없음").
  **반복 패스 안에서 다시 Repeat이 발동하면 안 된다.**
- `GenerationContext`에 `public float RepeatBonusPower;` 를 추가한다.
- 기존 `Clone()`을 **`CloneForRepeat()`으로 바꾸거나 새로 추가**한다. 차이가 핵심이다:
  - `Balls`: 원본의 새 리스트 (내용 복사)
  - `Combination`, `CombinationBaseScore`, `CombinationMultiplier`,
    `IsOverloaded`, `PerfectStop`, `TurnIndex`, `FloorIndex`: 그대로 복사
  - `FlatBonus = 0`, `MultiplierBonus = 1f`, `MoneyDelta = 0`,
    `RepeatBonusPower = 0`, `RepeatRequested = 0`, `FinalPower = 0`
  - `Log`, `PendingProbabilityModifiers`: **비어 있는 새 리스트** (원본 것을 복사하지 마라)
- 총 발동 횟수(`activationCount`)와 `maxTotalActivations` 상한은 기본 패스와
  반복 패스를 **합산해서** 세야 한다. 지금처럼 `ref int`로 넘기면 된다.
- 순환 감지 `HashSet`은 패스마다 초기화하지 말고 `(effectId, depth)` 키로 계속 누적한다
  (깊이가 다르면 다시 발동 가능해야 하므로 현재 키 설계는 유지).

## 결함 2 — BaseOutputSum 수동 증감이 Balls와 어긋난다 (심각)

**확인된 현상:** `CopyEffectHandler`는 `ctx.BaseOutputSum += source.baseOutput`,
`RemoveEffectHandler`는 `ctx.BaseOutputSum -= ball.baseOutput`을 직접 한다.
`ConvertEffectHandler`도 마찬가지다. 세 효과가 겹치거나 Convert가 baseOutput이 다른
구슬로 교체하면 `BaseOutputSum`과 `Balls`가 반드시 어긋난다.

**수정 방법 — `BaseOutputSum`을 저장 필드에서 계산 프로퍼티로 바꾼다:**

```csharp
/// Always derived from Balls so ball-list mutations can never desync the sum.
public float BaseOutputSum
{
    get
    {
        float sum = 0f;
        if (Balls != null)
            for (int i = 0; i < Balls.Count; i++)
                if (Balls[i] != null) sum += Balls[i].baseOutput;
        return sum;
    }
}
```

그리고:
- **모든 핸들러에서 `ctx.BaseOutputSum`을 대입/증감하는 코드를 전부 제거한다.**
  핸들러는 `ctx.Balls` 리스트만 정확히 조작하면 된다.
  (note 문자열은 그대로 유지해도 좋다)
- `CombinationResolver.BuildContext()`에서 `BaseOutputSum`에 대입하는 코드를 제거한다.
- `CloneForRepeat()`에서 `BaseOutputSum` 복사 코드를 제거한다 (이제 계산값이다).
- 컴파일이 깨지는 곳이 있으면 전부 고쳐라.

## 결함 3 — 효과 RNG가 구슬 RNG와 같은 수열을 쓴다

**현상:** `EffectResolver.InitializeSeed(seed)`와 `RouletteController.InitializeSeed(seed)`가
같은 seed로 `System.Random`을 만들면 완전히 같은 난수 수열이 나온다.
구슬 뽑기와 효과 확률 판정이 상관관계를 갖게 되어 통계가 왜곡된다.

**수정 방법:** `EffectResolver.InitializeSeed`에서 시드를 유도한다.
```csharp
_rng = new SystemEffectRandom(unchecked(seed * 397 ^ 0x5EED));
```
재현성은 유지된다(같은 입력 시드 → 같은 유도 시드).

---

## 유지할 것 (건드리지 마라)

- `OrderEffects` / `InsertByPriority`의 정렬 방식은 **이미 올바르다.**
  고정 `_typeOrder` 리스트를 순회하고 같은 타입 안에서만 priority로 삽입하며,
  동률일 때 원본 순서를 보존한다(안정 정렬). **`List.Sort`로 바꾸지 마라** —
  `List.Sort`는 불안정 정렬이라 오히려 재현성이 깨진다.
- `IsTriggerActive` / `IsConditionMet` 의 현재 동작
- `EffectLogEntry` 구조
- 핸들러 등록 방식(Dictionary는 타입→핸들러 조회 용도이므로 문제없다)

# 완료 조건

- [ ] Repeat이 `CloneForRepeat()` 패스 방식으로 동작하고 원본 누산기를 재오염시키지 않는다
- [ ] 반복 패스 안에서 Repeat 효과가 다시 발동하지 않는다 (`allowRepeatHandler=false`)
- [ ] `GenerationContext.RepeatBonusPower` 존재
- [ ] `FinalPower == 기본패스 전력 + RepeatBonusPower`
- [ ] `CloneForRepeat()`이 누산기를 초기값으로 되돌리고 Log를 비운 채 복제한다
- [ ] `BaseOutputSum`이 `Balls`로부터 계산되는 **읽기 전용 프로퍼티**다
- [ ] 어떤 핸들러에도 `BaseOutputSum` 대입/증감 코드가 남아 있지 않다
- [ ] `EffectResolver.InitializeSeed`가 시드를 유도한다
- [ ] `List.Sort`를 도입하지 않았다
- [ ] C# 문법 오류 없음

# 범위 밖

- 새 효과 타입 추가 금지
- 승객 / 과적 / UI / 씬 / 시뮬레이터 — 전부 다른 티켓이다
- `TubeController` 스크롤 수학 — 절대 손대지 마라
- `PrototypeUI.cs` — 손대지 마라
