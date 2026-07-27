# T-01 — 한 층 발전 루프 Graybox

> 범위: **T-01 (한 층 발전 루프 Graybox)만** 수행. 실제 통관 이동/정지 타이밍(**T-02**), 범용 연쇄 효과 처리기(**T-03**), 승객/부품/저주 콘텐츠, 완성형 아트/VFX/사운드는 구현하지 않는다. T-00에서 만든 상태 기계 위에 **실제 전력 생산·정산·초과 배분** 로직을 얹어 루프를 플레이 가능하게 만든다.

---

# Project Overview
- **Game Title:** 상승 (Ascend) — 가제
- **High-Level Concept:** 엘리베이터로 층을 오르는 로그라이트. 3구슬 조합으로 전력을 생산해 상승한다.
- **Players:** 싱글 플레이어
- **Target Platform / Orientation / Pipeline:** StandaloneWindows64 / Landscape 1920x1080 / URP (변경 없음)
- **기준 문서:** 사용자 제공 사양(섹션 1~15) + 승인·완료된 `Assets/Plans/T-00-prototype-skeleton.md`

## 확정된 설계 결정 (사용자 확인 완료)
1. **구슬 생성 모델:** 가중 랜덤 롤. 각 발전 턴에 `BallDatabase`의 `spawnProbability`로 3구슬을 즉시 추첨. `System.Random`을 **시드로 초기화**해 재현 가능(섹션 6·9). 통관 이동/정지 타이밍 없음(T-02 담당).
2. **요구 전력 미달 시:** **층 재시도** — 전력을 시작값으로 되돌리고 같은 층의 발전 3턴을 다시 수행. 루프가 끊기지 않음.
3. **초과 전력 배분:** `OverchargeAllocation` 상태에서 **[1]=돈 변환 / [2]=추가 상승** 선택 후 **Space로 확정·진행**. 현재 Space 진행 모델과 일관.

---

# Game Mechanics

## Core Gameplay Loop (T-01에서 실제 동작)
```
FloorArrival
  → PassengerSelection            (T-01: 통과. 승객 콘텐츠는 T-04+)
    → GenerationTurn (×3)         ← 각 턴마다 3구슬 롤 + 조합 판정 + 전력 누적
      → PowerResolution           ← 누적 전력 vs 요구 전력 판정 (성공/실패)
        → (성공) OverchargeAllocation  ← 초과 전력을 돈/추가상승에 배분
          → Ascending                  ← 층+1, 배분 반영
        → (실패) 층 재시도            ← 전력 리셋, GenerationTurn부터 다시
```

### 발전(Generation) 계산 흐름 — 한 턴
1. `RouletteController.Generate()` → 가중 랜덤으로 3구슬 추첨(시드 기반).
2. `CombinationResolver.Resolve(3구슬)` → `CombinationResult`(조합 종류, 획득 전력) 반환.
3. `EffectResolver.ResolveEffects()` 훅 호출(**T-01은 no-op 통과** — T-03 확장 자리 유지).
4. 획득 전력을 `ElevatorState.Power`에 누적. UI에 마지막 결과 표시.

### 조합 판정 규칙 (섹션 7)
`CombinationResolver`가 아래 **우선순위(위→아래, 첫 매칭 채택)**로 판정. 점수/배율은 전부 `CombinationConfig`(ScriptableObject)에서 조정.

| 우선순위 | 조합 (CombinationType) | 판정 조건 (graybox) |
|---|---|---|
| 1 | `ContainsLegendary` | 전설 구슬 1개 이상 포함 |
| 2 | `ThreeOfAKind` | 같은 `id` 3개 |
| 3 | `SpecificOrder` | 통관 순서상 등급 오름차순(tube0.grade < tube1 < tube2) — graybox 예시 규칙 |
| 4 | `CommonAdvancedRare` | 일반·고급·희귀 각 1개 |
| 5 | `ThreeSameGrade` | 같은 등급 3개 |
| 6 | `ThreeDifferentCommon` | 서로 다른 일반 구슬 3개 |
| 7 | `None` | 위 어느 것도 아님 → 최소 기본 출력 |

**전력 산식(graybox):** `power = (구슬 baseOutput 합계 + combo.baseScore) × combo.multiplier`
(각 combo의 `baseScore`, `multiplier`는 `CombinationConfig`에서 수정 가능 — 섹션 7·11)

### 정산 / 초과 배분
- **PowerResolution:** `surplus = Power - RequiredPower`.
  - `surplus >= 0` → 성공. surplus 저장 후 `OverchargeAllocation`으로.
  - `surplus < 0` → 실패. 로그 후 **층 재시도**(Power=시작값, CurrentTurn=0, `GenerationTurn` 재진입 준비 상태로 복귀 = `PassengerSelection`으로 되돌림).
- **OverchargeAllocation:** 초과분 배분 선택.
  - `[1]` 돈 변환: `Money += surplus × powerToMoneyRatio`.
  - `[2]` 추가 상승: surplus를 다음 층 **시작 전력으로 이월(banked)** → 다음 층이 쉬워짐(graybox 해석. 층 스킵이 아니라 전력 이월로 단순·되돌리기 쉽게 구현).
  - `Space` = 선택 확정 → `Ascending`.
  - 미선택 상태로 Space 시 기본값 **[1] 돈 변환** 적용(안전 기본값).

## Controls and Input Methods (T-01)
| 키 | 상태 | 동작 |
|----|------|------|
| `Space` | 전체 | 상태 진행(발전/정산/확정) — T-00과 동일 |
| `R` | 전체 | 런 초기화 |
| `1` | OverchargeAllocation | 초과 전력 → 돈 변환 선택 |
| `2` | OverchargeAllocation | 초과 전력 → 추가 상승(이월) 선택 |

---

# UI (디버그 HUD 확장)
기존 HUD(State/Floor/Turn/Power/Required/Money/Weight) 유지 + 아래 추가 표시:
- **마지막 발전 결과:** 3구슬 id/등급 + 조합 이름 + 이번 턴 획득 전력.
  예: `Roll: Ball_02(C) Ball_02(C) Ball_02(C) → ThreeOfAKind (+42.0)`
- **정산 결과:** PowerResolution 진입 시 `SUCCESS (surplus +30)` / `FAIL (short 15)` 표시.
- **초과 배분 힌트:** OverchargeAllocation 상태에서 `[1] Money  [2] Ascend  (selected: Money)` 표시.
- 조합 이름은 성공 색(초록)/실패 색(빨강)으로 구분(선택).

```
State:    GenerationTurn
Floor:    1   Turn: 2/3
Power:    68.0 / Required 125.0
Money:    0
Weight:   0 / 100
Roll:     Ball_04(A) Ball_02(C) Ball_07(R) → CommonAdvancedRare (+34.0)
─────────────────────────────
[Space] Advance  [R] Reset  [1]Money [2]Ascend
```

---

# Key Asset & Context

## 신규 파일
```
Assets/Prototype_Elevator/
  Scripts/
    Roulette/
      CombinationType.cs        (enum: ThreeOfAKind, ThreeSameGrade, ThreeDifferentCommon,
                                        CommonAdvancedRare, SpecificOrder, ContainsLegendary, None)
      CombinationResolver.cs     (MonoBehaviour: Resolve(IReadOnlyList<BallDefinition>) → CombinationResult)
    Data/
      CombinationConfig.cs       (ScriptableObject: 조합별 baseScore/multiplier + None 최소 출력)
  Data/
    CombinationConfig.asset      (기본값 에셋)
```
`CombinationResult`는 `CombinationResolver.cs` 안의 `[Serializable] struct`(필드: `CombinationType Type; float Power; string Summary;`).

## 수정 파일
| 파일 | 변경 요지 |
|------|-----------|
| `Roulette/RouletteController.cs` | 가중 랜덤 롤 구현. `[SerializeField] BallDatabase _database;` 추가. `Generate()`/`RollTubes(int)`로 3구슬 추첨, `LastResults` 보관, `CollectResults()`가 이를 반환. `System.Random` 시드 초기화(`InitializeSeed(int)`). `StartSpin/StopTube`는 T-02용 스텁 유지. |
| `Core/RunController.cs` | 참조 추가: `CombinationResolver _resolver`. 발전 훅(GenerationTurn 진입/증가 시 `Generate→Resolve→EffectResolver→Power 누적`). PowerResolution 성공/실패 분기 + 실패 시 층 재시도. OverchargeAllocation 키 1/2 선택·Space 확정. surplus/배분 이월 처리. ResetRun에서 시드/이월 초기화. |
| `Core/ElevatorState.cs` | 필드 추가: `float BankedPower`(추가 상승 이월), 마지막 결과 표시용 `string LastRollSummary`, `float LastGenerationPower`. `Initialize`에서 리셋. |
| `Data/PrototypeConfig.cs` | 필드 추가: `int randomSeed = 12345;`(재현용, 섹션 6). |
| `UI/PrototypeUI.cs` | 마지막 발전 결과/정산 결과/초과 배분 힌트 표시 로직 추가. |

## CombinationConfig 노출 값 (섹션 7·11)
| 조합 | baseScore(예) | multiplier(예) |
|------|------|------|
| ThreeOfAKind | 20 | 2.0 |
| ThreeSameGrade | 12 | 1.6 |
| ThreeDifferentCommon | 6 | 1.2 |
| CommonAdvancedRare | 10 | 1.5 |
| SpecificOrder | 8 | 1.4 |
| ContainsLegendary | 30 | 2.5 |
| None (최소 기본 출력) | `minimumBaseOutput = 5` | 1.0 |

> 모든 값은 Inspector에서 조정 가능. 구슬 `baseOutput`은 기존 `Ball_01~09.asset`에 이미 존재(임시값). 필요 시 T-01에서 임시값을 명확한 차등값으로 재설정(등급 높을수록 큰 output).

---

# Implementation Steps

### Step 1 — 조합 데이터 타입 & 설정 SO
- **Description:** `CombinationType.cs`(enum), `CombinationConfig.cs`(ScriptableObject, `[CreateAssetMenu]`, 위 표의 필드 + `minimumBaseOutput`) 작성. `Ascend.Prototype` 네임스페이스.
- **Assigned role:** developer
- **Dependencies:** None
- **Parallelizable:** Yes

### Step 2 — CombinationResolver
- **Description:** `CombinationResolver.cs`(MonoBehaviour) 작성. `[SerializeField] CombinationConfig _config;`. `CombinationResult Resolve(IReadOnlyList<BallDefinition> balls)` — 우선순위 판정표대로 첫 매칭 채택, 전력 산식 적용, `Summary` 문자열 생성. `CombinationResult` struct 포함.
- **Assigned role:** developer
- **Dependencies:** Step 1
- **Parallelizable:** No

### Step 3 — RouletteController 가중 랜덤 롤
- **Description:** `RouletteController.cs` 수정. `BallDatabase` 참조 + 시드 기반 `System.Random`. `InitializeSeed(int)`, `Generate()`(3구슬 롤), `CollectResults()`가 `LastResults` 반환. 누적 가중치 방식으로 `spawnProbability` 샘플링. `StartSpin/StopTube` 스텁 유지(주석에 T-02 명시).
- **Assigned role:** developer
- **Dependencies:** None (Step 1/2와 병행 가능)
- **Parallelizable:** Yes

### Step 4 — RunController 루프 통합 & ElevatorState/Config 확장
- **Description:** `PrototypeConfig.cs`에 `randomSeed` 추가. `ElevatorState.cs`에 `BankedPower/LastRollSummary/LastGenerationPower` 추가 + `Initialize` 리셋. `RunController.cs`에 `CombinationResolver` 참조, 발전 훅, PowerResolution 성공/실패(층 재시도) 분기, OverchargeAllocation 키1/2 선택·Space 확정·이월/돈 변환, ResetRun 시드/이월 초기화 구현. `EffectResolver.ResolveEffects()` 훅 호출 지점 삽입(no-op 통과).
- **Assigned role:** developer
- **Dependencies:** Steps 1, 2, 3
- **Parallelizable:** No

### Step 5 — PrototypeUI 확장
- **Description:** `PrototypeUI.cs`에 마지막 발전 결과/정산 결과/초과 배분 힌트 표시 추가. RunController의 최신 결과·surplus·선택 상태를 읽어 표시.
- **Assigned role:** developer
- **Dependencies:** Step 4
- **Parallelizable:** No

### Step 6 — 씬 배선 & 데이터 에셋
- **Description:** `CombinationConfig.asset` 생성(기본값). `Prototype_Elevator.unity`의 `GameSystems`에 `CombinationResolver` 컴포넌트 추가. 참조 배선: RunController→CombinationResolver, RouletteController→BallDatabase, CombinationResolver→CombinationConfig. `SampleScene` 무수정. (선택) `Ball_01~09.asset`의 `baseOutput`을 등급 차등값으로 정리.
- **Assigned role:** developer
- **Dependencies:** Steps 2, 3, 4, 5
- **Parallelizable:** No

### Step 7 — 컴파일 & Play Mode 검증
- **Description:** 콘솔 에러 0 확인. Play Mode에서 3턴 발전→전력 누적→정산(성공/실패)→초과 배분→상승 순환 검증. 시드 고정 시 동일 롤 재현 확인.
- **Assigned role:** coordinator
- **Dependencies:** Step 6
- **Parallelizable:** No

---

# Verification & Testing (T-01 완료 기준)
1. Play Mode 진입 시 **Console Error 0**.
2. 각 GenerationTurn에서 3구슬이 롤되고, 조합 종류와 획득 전력이 Console/화면에 표시된다.
3. 3턴 후 PowerResolution에서 누적 전력 vs 요구 전력이 판정된다(성공/실패 로그).
4. **실패 시** 같은 층에서 발전이 재시도된다(전력 리셋, 층 유지).
5. **성공 시** OverchargeAllocation에서 `[1]`돈/`[2]`추가상승을 선택하고 Space로 확정하면 배분이 반영된다(Money 증가 또는 다음 층 BankedPower 이월).
6. 상승 후 다음 층 요구 전력이 증가한 채 루프가 계속 반복된다.
7. **동일 `randomSeed`로 리셋 시 동일한 구슬 롤 시퀀스가 재현**된다(섹션 6).
8. 조합 점수/배율을 `CombinationConfig`에서, 구슬 확률/출력을 에셋에서 수정 가능(하드코딩 없음).
9. `EffectResolver` 훅이 루프에 존재하되 T-01에서는 no-op으로 통과(T-03 확장 자리 유지).
10. 기존 씬·기능 무손상. 되돌리기: 신규 파일 삭제 + 수정 파일 원복으로 롤백 가능.

---

# T-02 진입 조건 (참고)
- `RouletteController`가 결과 3구슬을 안정적으로 공급하고 `CombinationResolver`가 전력을 산출하면, T-02에서 `Generate()`의 즉시 랜덤 롤을 **실제 통관 이동 + 정지 버튼 타이밍**(TubeController)으로 교체하되 `CollectResults()` 계약은 유지한다.
- `EffectResolver` 훅 지점이 확보되어 T-03에서 연쇄 효과를 삽입할 수 있다.
