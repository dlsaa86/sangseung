# RunSession 이주 계획 — 층 진행에서 라운드로 (2026-08-09)

새 규칙은 **이미 만들어졌고 28건으로 잠겨 있다** (`ElevatorTravel`·`RoundGoal`·`RoundSession`).
남은 것은 `RunSession` 이 그것을 쓰게 만드는 일이고, 그 과정에서 **검사 88건이 흔들린다.**
이 문서는 그 이주를 순서대로 적는다. 한 번에 하지 말 것.

---

## 0. 왜 이 문서가 있나

배선을 시작하면 「층마다 요구 전력」을 전제한 검사들이 무더기로 깨진다. 그 상태에서
컨텍스트가 끊기면 **반쯤 이주된 저장소**가 남고, 다음 사람은 새 규칙이 틀린 건지 배선이
틀린 건지 옛 검사가 낡은 건지 구분할 수 없다. 그래서 시작 전에 이음매를 적어 둔다.

---

## 1. 확정된 이음매 (실측)

| 무엇 | 어디 |
|---|---|
| 층 진행의 **유일한** 지점 | `RunSession.cs:571` `CurrentFloor += ascended` (`CompleteFloor` 안) |
| 층 실패 → 런 종료 | `RunSession.cs:545-553` |
| 층 생성 / 건물 끝 판정 | `RunSession.CreateCurrentFloor()` (`:501`) |
| 요구 전력의 출처 | `FloorPlan.RequiredPower` (`Spin/FloorPlan.cs:23`) |
| 스핀 수의 출처 | `FloorPlan.Spins` (`:26`) |
| 잉여 → 돈·추가층 | `AscendResult.AllocateSurplus` · `CompleteFloor:577-596` |
| 남은 스핀 정산 | `RemainingSpinSettlementProfile` → `FloorResult.SettlementMoney` |

**핵심**: 층 위치를 바꾸는 곳이 한 군데뿐이다. 이주는 그 한 줄을
`RoundSession.Move()` 로 대체하는 데서 시작한다.

---

## 2. 의미가 바뀌는 것

| 개념 | 옛 뜻 | 새 뜻 |
|---|---|---|
| `FloorPlan.RequiredPower` | 층 통과 조건 | **층당 이동 비용** (`ElevatorTravel.PowerPerFloor`) |
| `FloorPlan.Floor` | 진행 단위 | 라운드의 **목표 층** (`RoundGoal.TargetFloor`) |
| `FloorPlan.Spins` | 층당 스핀 | 라운드당 스핀 (그대로) |
| 층 실패 | 요구 전력 미달 | **스핀 소진 시 목표 층 미달** |
| 돈 | 잉여 환산 + 정산 | **도달 시점의 남은 스핀 × 4** |

⚠ `AscendResult` 의 `SurplusUse`(Ascend/Money/Bank) 3분기와 `DefaultGoldPerSkippedFloor`
는 **새 모델에서 역할이 사라진다.** 지우기 전에 `PowerBand`(Crash/Jettison/Damaged)가
새 모델의 어디에 대응하는지 먼저 정해야 한다 — 그것까지 같이 지우면 위험 연출·화물 포기가
통째로 사라진다.

---

## 3. 순서 (한 단계씩, 매번 전체 자체검증)

### 3-1. `FloorPlan` 에 목표 층·이동 비용을 **추가만** 한다
기존 필드를 지우지 않는다. `TargetFloor`, `PowerPerFloor` 를 넣고 기본값을 기존 값에서
유도한다. **이 단계에서는 아무 검사도 깨지지 않아야 한다.** 깨지면 유도식이 틀린 것이다.

### 3-2. `RunSession` 이 `RoundSession` 을 **소유**한다
`CreateCurrentFloor` 에서 함께 만든다. 아직 권위는 주지 않는다 —
「두 모델이 같은 답을 내는가」를 검사로 먼저 확인한다.

> 이 단계의 검사가 이주 전체의 안전망이다. 옛 경로와 새 경로가 같은 입력에서 같은
> 층·같은 돈을 내는지 시드 여러 개로 대조한다. 어긋나면 그 자리에서 멈춘다.

### 3-3. 권위를 넘긴다
`CurrentFloor += ascended` 를 `_round.Move(delta)` 로 바꾼다.
**여기서 검사가 깨지기 시작한다.** 깨지는 것을 그룹 단위로 옮긴다:

```
[층 진행·계약·앤티]      25건   ← 요구 전력 전제가 가장 많다
[적재·빌드·10층 진행]    63건   ← 10층 완주 시나리오 전체
[텔레메트리]             28건   ← 층 전이 기록
[런 요약 9종]            14건
```

⚠ **검사를 지우거나 스킵하지 않는다.** 옛 규칙을 검증하던 것은 새 규칙에서 **무엇을
물어야 하는지** 다시 쓴다. 「10층까지 오른다」는 새 모델에서도 유효한 질문이다 —
가는 방법만 달라졌다.

### 3-4. 상승 버튼을 샌드박스에서 런으로 옮긴다
`RoundSandbox` 를 지우고 `InteractableRoundButton` 이 `RunSession` 을 보게 한다.
**샌드박스를 지우는 것이 이주 완료의 신호다.**

### 3-5. 표시 배선
- LED 디스플레이 ← 스핀당 획득 전력 (`RoundSandbox._gainDisplay` 슬롯 참조)
- 2D HUD 정중앙 하단 ← 전력 총량
- 상승 버튼 위 ← `RoundSession.PowerToGoal`

### 3-6. 추락 연출
판정(`RoundOutcome.Crashed`)은 이미 있다. 화면에서 벌어지는 일이 없다.

---

## 4. 이주 중 지켜야 할 것

- **한 단계 끝날 때마다 `Ascend/Run Self Tests`.** 기준선은 **655 PASS / 3 FAIL**
  (실패 3건은 밤 시작 전부터 있던 것 — 적재 정책 4/6칸 · 다층 상승 0회 · 무계약 6층 1/6).
- 컴파일이 깨진 채로 다음 단계로 가지 않는다. asmdef 이 없어 한 줄이 전체를 막는다.
- `.cs` 를 고친 뒤 첫 확인은 `grep -an "error CS" Logs/Editor.log | tail -5`.
- 되돌릴 지점: 커밋 `eb8ff84` (새 척추까지 · 배선 전).

---

## 5. 이주가 끝났다고 말할 수 있는 조건

1. `RoundSandbox` 가 저장소에 없다
2. 자체검증이 655 이상이고 새로 깨진 것이 0
3. 플레이 모드에서 5스핀 안에 목표 층 도달 → 골드, 미달 → 추락이 실제로 일어난다
4. 상승 버튼 위에 `PowerToGoal` 이 뜨고, 전력이 모자라면 버튼이 안 켜진다
5. 하강 버튼이 눌리고 전력이 그만큼 빠진다 (소비처는 아직 없어도 된다)
