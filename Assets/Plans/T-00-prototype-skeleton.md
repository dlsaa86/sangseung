# T-00 — "상승(Ascend)" 프로토타입 골격

> 범위: **T-00 (프로토타입 프로젝트 골격)만** 수행. 구슬 이동, 실제 발전 계산, 승객/부품 콘텐츠, 완성형 아트, 실제 물리는 **구현하지 않는다.** 이후 T-01~ 확장을 위한 책임 분리와 데이터/인터페이스 자리만 마련한다.

---

# Project Overview
- **Game Title:** 상승 (Ascend) — 가제
- **High-Level Concept:** 정체불명의 엘리베이터를 타고 층을 올라가는 로그라이트. 3개의 투명 통관에서 떨어지는 구슬을 버튼으로 멈춰 수확하고, 3구슬 조합으로 전력을 생산해 상승한다.
- **Players:** 싱글 플레이어
- **Inspiration / Reference:** 슬롯/릴 정지 타이밍 게임 + 덱빌딩 로그라이트(발라트로류 조합 점수)
- **Tone / Art Direction:** 미정 (프로토타입은 Graybox, 임시 색/머티리얼)
- **Target Platform:** StandaloneWindows64 (PC)
- **Screen Orientation / Resolution:** Landscape 1920x1080
- **Render Pipeline:** URP (기존 PC_RPAsset 유지, 변경 없음)

## 현재 프로젝트 조사 결과 (기준선)
- Unity 6000.5.5f1, URP 17.5.0, New Input System 1.19.0, TMP(com.unity.ugui 2.5.0).
- 커스텀 스크립트/네임스페이스/asmdef 없음. 모두 `Assembly-CSharp`. 씬은 `SampleScene`뿐.
- `Prototype`/`Prototype_Elevator` 폴더 부재 → 신규 생성 안전, 충돌 없음.
- **기존 코드 충돌 가능성:** 전용 네임스페이스 `Ascend.Prototype`, 전용 폴더/씬 사용 → 사실상 없음. `SampleScene`·기존 파일은 건드리지 않음.

## 결정 사항 (사용자 확인 완료)
- **디버그 UI:** uGUI Canvas + TextMeshPro.
- **테스트 입력:** `Keyboard.current` 직접 폴링(에셋 생성 없이 가장 단순/되돌리기 쉬움).
- **네임스페이스:** `Ascend.Prototype` 로 격리. **asmdef는 만들지 않음**(단순 유지, 요청 없음). Editor 전용 코드가 없으므로 불필요.

---

# Game Mechanics

## Core Gameplay Loop (한 층의 상태 흐름)
```
FloorArrival
  → PassengerSelection
    → GenerationTurn (최대 3회 반복)
      → PowerResolution
        → OverchargeAllocation
          → Ascending
            → (다음 층) FloorArrival ...
```
T-00에서는 이 흐름이 **끝없이 반복**되는 상태 기계만 구현한다. 실제 발전/조합/승객 로직은 없고, 각 상태는 테스트 입력으로 다음 단계로 전이한다.

### T-00 전이 규칙
- `Space` = 다음 상태로 진행(Advance).
- `GenerationTurn` 진입 시 `currentTurn` 1 증가. `Space`를 누르면:
  - `currentTurn < generationsPerFloor` 이면 다시 `GenerationTurn`(턴 +1, 같은 상태 유지).
  - `currentTurn >= generationsPerFloor` 이면 `PowerResolution`으로.
- `Ascending`에서 `Space` → 층 +1, `currentTurn=0`, 요구 전력 재계산 후 `FloorArrival`.
- `R` = 런 리셋(층/턴/재화를 config 초기값으로).
- 모든 전이는 `Debug.Log`로 기록.

## Controls and Input Methods (T-00 테스트용)
| 키 | 동작 |
|----|------|
| `Space` | 현재 상태 → 다음 상태 진행 |
| `R` | 런 초기화 |

`UnityEngine.InputSystem.Keyboard.current`를 `Update`에서 폴링. `.inputactions` 에셋 생성/수정 없음.

---

# UI (임시 디버그 HUD)
- **Canvas** (Screen Space - Overlay) + **EventSystem**(New Input System용 `InputSystemUIInputModule`).
- 좌상단 **TMP 텍스트 1개**(멀티라인)로 다음을 매 프레임 표시:
  - `State: <RunState>`
  - `Floor: <n>`
  - `Turn: <cur>/<max>`
  - `Power: <power>`
  - `Required: <requiredPower>`
  - `Money: <money>`
  - `Weight: <weight>/<allowedWeight>` (초과 시 색 강조 - 선택)
- 하단 조작 힌트 텍스트: `[Space] Advance   [R] Reset`.

```
┌───────────────────────────────┐
│ State: GenerationTurn          │
│ Floor: 2                       │
│ Turn: 1/3                      │
│ Power: 0                       │
│ Required: 125                  │
│ Money: 0                       │
│ Weight: 0 / 100                │
│                                │
│                                │
│           [Space] Advance  [R] Reset │
└───────────────────────────────┘
```

---

# Key Asset & Context

## 폴더 구조 (신규, 전부 격리)
```
Assets/Prototype_Elevator/
  Scenes/
    Prototype_Elevator.unity
  Scripts/
    Core/
      RunState.cs
      RunController.cs
      FloorController.cs
      ElevatorState.cs        (= RunData, 런타임 데이터, 순수 C# 클래스)
    Roulette/
      RouletteController.cs    (골격/스텁)
    Effects/
      EffectResolver.cs        (골격 + 확장용 인터페이스 자리)
      EffectType.cs            (enum 자리)
      IEffect.cs               (인터페이스 자리)
    UI/
      PrototypeUI.cs
    Data/
      PrototypeConfig.cs       (ScriptableObject: 밸런스 값)
      BallGrade.cs             (enum: Common/Advanced/Rare/Legendary)
      BallDefinition.cs        (ScriptableObject: 구슬 데이터 자리)
      BallDatabase.cs          (ScriptableObject: 구슬 목록 + 확률합 검증)
  Data/                         (에셋 인스턴스)
    PrototypeConfig.asset
    BallDatabase.asset
    Balls/
      Ball_01 ~ Ball_09.asset  (데이터만, 동작 없음)
```

## 클래스 책임 요약
- **RunState** (enum): `FloorArrival, PassengerSelection, GenerationTurn, PowerResolution, OverchargeAllocation, Ascending`.
- **RunController** (MonoBehaviour): 전체 상태/전환 소유. `ElevatorState` 인스턴스 보유. `FloorController`, `RouletteController`, `EffectResolver` 참조. `AdvanceState()`, `ResetRun()` 제공. `Keyboard.current` 폴링. 전이 로그.
- **FloorController** (MonoBehaviour): `CurrentFloor`, `RequiredPower`. `EnterFloor(floor)`, `ComputeRequiredPower(floor)`(= config에서 base + floor*growth), 층 진입/종료 관리.
- **ElevatorState / RunData** (순수 C# `[Serializable]` 클래스): `Power, Money, Weight, AllowedWeight, CurrentTurn`. config 초기값으로 `Initialize()`. RunController가 소유, `[SerializeField]`로 Inspector 노출.
- **RouletteController** (MonoBehaviour, 스텁): `StartSpin()`, `StopTube(int)`, `CollectResults()` 시그니처만. T-00은 로그만. (TubeController/CombinationResolver는 **T-02**에서.)
- **EffectResolver** (MonoBehaviour, 스텁): `ResolveEffects(EffectContext)` → 입력 그대로 반환. 확장 대비.
- **IEffect / EffectType**: `Probability, Add, Multiply, Convert, Copy, Remove, Repeat` 자리만. **로직 없음**(섹션 8 준수).
- **PrototypeUI** (MonoBehaviour): RunController 참조, TMP 텍스트 갱신.
- **PrototypeConfig** (SO): 아래 밸런스 값.
- **BallGrade/BallDefinition/BallDatabase** (SO): 구슬 **데이터 정의만**. 이동/판정 로직 없음. `BallDatabase`는 확률 합계 100% 검증 유틸 제공(섹션 5). — *데이터 정의는 시스템 구현이 아니므로 T-00 범위와 충돌하지 않음.*

## PrototypeConfig 노출 값 (섹션 11 근거)
| 필드 | 초기값(예시) | 비고 |
|------|------|------|
| `generationsPerFloor` | 3 | 층당 발전 횟수(고정 조건) |
| `baseRequiredPower` | 100 | 기본 요구 전력 |
| `requiredPowerGrowthPerFloor` | 25 | 층 증가당 요구 전력 증가 |
| `allowedWeight` | 100 | 허용 무게 |
| `overloadRequiredPowerMultiplier` | 1.5 | 과적 시 요구 전력 배율 |
| `powerToMoneyRatio` | 1 | 초과 전력→돈 변환 비율 |
| `startingPower` / `startingMoney` / `startingWeight` | 0 / 0 / 0 | 런 초기값 |
| `ballMoveSpeed`, `ballSpacing`, `brakeDelay` | — | **T-02용 자리**(값만 노출, 미사용) |

> `BallDefinition`(확률/기본 출력/등급/식별자), 조합별 점수/배율은 각각 `BallDatabase`/향후 `CombinationConfig`에서 데이터로 관리 → **코드 하드코딩 금지**(섹션 11).

---

# Implementation Steps

### Step 1 — 폴더 & 데이터 타입 골격
- **Description:** `Assets/Prototype_Elevator/{Scenes,Scripts/...,Data}` 폴더 생성. `PrototypeConfig.cs`, `BallGrade.cs`, `BallDefinition.cs`, `BallDatabase.cs` 작성(모두 `Ascend.Prototype` 네임스페이스, `[CreateAssetMenu]` 포함). 로직 없이 필드/검증만.
- **Assigned role:** developer
- **Dependencies:** None
- **Parallelizable:** Yes (Step 2와 병행 가능)

### Step 2 — 코어 상태 기계 골격
- **Description:** `RunState.cs`, `ElevatorState.cs`, `FloorController.cs`, `RunController.cs`, `RouletteController.cs`, `EffectResolver.cs`(+`IEffect.cs`,`EffectType.cs`) 작성. RunController에 전이 그래프/로그/`Keyboard.current` 입력 구현. `EnumFlow` 규칙(위 T-00 전이 규칙) 반영.
- **Assigned role:** developer
- **Dependencies:** Step 1 (RunController가 PrototypeConfig 참조)
- **Parallelizable:** No

### Step 3 — 디버그 UI 스크립트
- **Description:** `PrototypeUI.cs` 작성. RunController/ElevatorState/FloorController에서 값을 읽어 TMP 텍스트 갱신. 조작 힌트 표시.
- **Assigned role:** developer
- **Dependencies:** Step 2
- **Parallelizable:** No

### Step 4 — 데이터 에셋 생성
- **Description:** `PrototypeConfig.asset`, `BallDatabase.asset`, `Balls/Ball_01~09.asset` 생성. 9종 확률 배정(일반3×18%, 고급3×10%, 희귀2×6%, 전설1×4% = 100%). 임시 debugColor 지정. `BallDatabase`에 9개 등록 후 합계 검증 통과 확인.
- **Assigned role:** developer
- **Dependencies:** Step 1
- **Parallelizable:** Yes (Step 2/3와 병행 가능)

### Step 5 — 씬 구성 & 배선
- **Description:** `Prototype_Elevator.unity` 생성(URP 카메라 + Directional Light). `GameSystems` 오브젝트에 RunController/FloorController/RouletteController/EffectResolver 부착·상호 참조. `PrototypeConfig.asset` 할당. Canvas+EventSystem(`InputSystemUIInputModule`)+TMP 텍스트 생성, `PrototypeUI` 부착·참조 배선. **`SampleScene`은 손대지 않음.** 빌드 세팅에 씬 추가는 하지 않음(선택).
- **Assigned role:** developer
- **Dependencies:** Steps 2, 3, 4
- **Parallelizable:** No

### Step 6 — 컴파일 & Play Mode 검증
- **Description:** 콘솔 에러 0 확인, Play Mode 진입 후 상태 표시/전이/로그 동작 확인.
- **Assigned role:** developer
- **Dependencies:** Step 5
- **Parallelizable:** No

---

# Verification & Testing (T-00 완료 기준)
1. `Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity` 존재.
2. Play Mode 진입 시 **Console Error 0**.
3. 현재 상태가 **Console + 화면**에 표시됨.
4. **층/턴/전력/요구전력/돈/무게**가 화면에 표시됨.
5. `Space` 입력으로 상태가 다음 단계로 전이(GenerationTurn 3회 반복 → 상승 → 층+1 → FloorArrival 순환).
6. 각 상태 전이가 Console에 기록됨.
7. `PrototypeConfig.asset`의 핵심 수치를 Inspector에서 수정 가능(요구 전력 계산에 반영 확인).
8. 특정 3D 아트 없이 이후 기능 추가 가능한 구조.
9. `SampleScene` 및 기존 파일 무손상(수정/삭제 없음).

**되돌리기:** 전체 작업은 `Assets/Prototype_Elevator/` 폴더 삭제만으로 완전 롤백 가능(기존 프로젝트 무영향).

---

# T-01 진입 조건 (참고)
- 상태 기계가 안정적으로 순환하고, `RouletteController.StartSpin/StopTube/CollectResults` 시그니처가 자리잡혀 있으면 T-01(한 층 발전 루프 Graybox)에서 실제 발전 턴 로직과 통관 스텁 결과를 연결할 수 있다.
