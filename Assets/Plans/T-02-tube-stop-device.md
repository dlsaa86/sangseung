# T-02 — 세 통관 구슬 정지 장치

> 범위: **T-02 (세 통관 구슬 정지 장치)만** 수행. T-01의 즉시 랜덤 롤(`RouletteController.Generate()`)을 **실제 통관 이동 + 정지 버튼 타이밍 + 수확 선택**으로 교체한다. 범용 연쇄 효과 처리기(**T-03**), 승객/부품/과적/경제/층 콘텐츠(**T-04+**), 완성형 아트/VFX/사운드, 실제 Rigidbody 물리 충돌은 구현하지 않는다.

---

# Project Overview
- **Game Title:** 상승 (Ascend) — 가제
- **Target Platform / Orientation / Pipeline:** StandaloneWindows64 / Landscape 1920x1080 / URP (변경 없음)
- **기준 문서:** 사용자 사양(섹션 1~15) + 완료된 `T-00`, `T-01` 계획.

## 확정된 설계 결정 (사용자 확인 완료)
1. **표현:** **월드 3D Graybox** — 3개의 수직 통관과 그 안을 아래로 흐르는 구슬(스피어, `BallDefinition.debugColor`)을 씬에 배치하고 카메라로 비춘다. 최종 아트 아님.
2. **정지 입력:** 키 **`1` / `2` / `3`** = 통관 0 / 1 / 2 정지. **GenerationTurn 상태에서만** 활성. (초과배분의 `1/2`는 `OverchargeAllocation` 상태에서만 활성이므로 상태 게이팅으로 충돌 없음.)
3. **턴 흐름:** 세 통관이 **모두 정지되면 자동으로 조합·전력 판정**을 수행하고 결과를 표시. 이후 **`Space`로 다음 턴/정산 진행**.

## 결정론 (섹션 6 준수)
- 각 통관의 **구슬 스트림은 시드 기반**(`RouletteController.InitializeSeed`)으로 생성 → 같은 시드면 같은 구슬 순서.
- 통관 이동은 **코드 기반 결정론적 스크롤**(`ballMoveSpeed`로 누적 이동, Rigidbody 물리 없음).
- 따라서 **같은 시드 + 같은 정지 시점 = 같은 수확 결과**가 재현된다(섹션 6). 최종 조합은 플레이어 타이밍에 의존(그게 이 장치의 핵심 메커닉).

---

# Game Mechanics

## Core Gameplay Loop — GenerationTurn 재설계
T-01의 상태 기계는 유지. 단, `GenerationTurn` 내부 동작이 즉시 롤 → **타이밍 정지 미니게임**으로 바뀐다.

```
GenerationTurn 진입
  → RouletteController.StartSpin()      (3통관 구슬 하강 시작)
  → 플레이어가 [1]/[2]/[3]로 각 통관을 원하는 순간 정지
      → 각 통관: 짧은 제동 지연(brakeDelay) 후 정지
      → 정지 시 수확창(harvest window)에 가장 가까운 구슬을 결과로 확정
  → 세 통관 모두 정지 시:
      → CollectResults() → CombinationResolver.Resolve() → 전력 누적 (자동 판정)
      → 결과 표시, 턴 "resolved" 상태로 잠금
  → [Space]로 다음 GenerationTurn(다음 턴 자동 StartSpin) 또는 PowerResolution 진행
```

### 통관 이동/정지 모델 (TubeController, 결정론적)
- 각 통관은 **구슬 스트림**(`List<BallDefinition>`, 시드 기반으로 미리 생성)을 보유.
- `_scrollOffset += ballMoveSpeed * Time.deltaTime` — 구슬들이 `ballSpacing` 간격으로 아래로 이동, 하단 도달 시 상단으로 순환(스트림 인덱스 증가).
- **수확창**(고정 Y 마커) 기준으로 현재 가장 가까운 구슬을 추적.
- `RequestStop()`: 즉시 멈추지 않고 `brakeDelay`초 동안 계속 이동 후 정지 → 그 순간 수확창 최근접 구슬을 `StoppedBall`로 확정(섹션 6 "짧은 제동 지연 후 정지").
- 다른 통관은 독립적으로 계속 이동(섹션 6).
- 시각: 통관당 N개의 구슬 스피어를 풀링해 스크롤 오프셋에 따라 위치만 갱신, 각 슬롯이 스트림의 해당 구슬을 표시(debugColor 틴트).

## Controls and Input Methods (T-02)
| 키 | 활성 상태 | 동작 |
|----|------|------|
| `1` / `2` / `3` | GenerationTurn | 통관 0 / 1 / 2 정지 요청(제동 시작) |
| `Space` | 전체 | 상태 진행 (단, GenerationTurn에서는 **세 통관 정지+판정 완료 후에만** 진행) |
| `R` | 전체 | 런 초기화(모든 통관 리셋 포함) |
| `1` / `2` | OverchargeAllocation | 초과 전력 배분 선택(기존 T-01, 상태 게이팅) |

---

# UI / 씬 표현
- **월드 3D**: `TubesRoot` 아래 통관 0/1/2를 x = -2 / 0 / +2 위치에 배치. 각 통관은 반투명 프레임(스케일된 큐브, 투명 머티리얼)과 하단 근처 **수확창 마커**(얇은 밝은 큐브/쿼드), 구슬 풀(스피어).
- **Main Camera**(Prototype 씬): 세 통관을 정면에서 담도록 위치/각도 조정. `SampleScene` 카메라는 무관.
- **HUD(PrototypeUI)** 확장: 기존 값 + 통관 상태 표시.
  - `Tubes: [1:running] [2:STOP Ball_04(A)] [3:running]` 형태로 각 통관 상태/정지 구슬.
  - GenerationTurn 힌트: `[1][2][3] Stop tubes`.
  - 세 통관 정지 후: 기존 `Roll: ...` 결과 라인 + `[Space] Next`.
- 구슬 머티리얼은 런타임 생성(URP/Unlit, `debugColor`) — 프리팹 에셋 없이 되돌리기 쉽게.

```
(Game View)
   ┌───┐   ┌───┐   ┌───┐
   │ ● │   │ ● │   │ ● │      ← 구슬 하강
   │ ● │   │ ● │   │ ● │
  ═╪═●═╪═ ═╪═●═╪═ ═╪═●═╪═     ← 수확창(harvest window)
   │ ● │   │ ● │   │ ● │
   └───┘   └───┘   └───┘
    [1]     [2]     [3]        ← 정지 키

HUD(좌상단): State/Floor/Turn/Power/Required/Money/Weight/Tubes/Roll
```

---

# Key Asset & Context

## 신규 파일
```
Assets/Prototype_Elevator/
  Scripts/
    Roulette/
      TubeController.cs        (통관 1개: 구슬 스트림, 스크롤 이동, 제동/정지, 수확 선택, 시각 풀링)
```

## 수정 파일
| 파일 | 변경 요지 |
|------|-----------|
| `Roulette/RouletteController.cs` | 3개 `TubeController` 오케스트레이션으로 재작성. `StartSpin()`=각 통관에 시드 기반 스트림 배정+하강 시작, `StopTube(int)`=해당 통관 제동, `AllStopped`(bool), `CollectResults()`=정지 구슬 3개 반환(계약 유지). `InitializeSeed(int)` 유지. 기존 즉시 `Generate()`는 제거 또는 내부 스트림 생성 헬퍼로 대체. |
| `Core/RunController.cs` | GenerationTurn 진입 시 즉시 판정 대신 `_roulette.StartSpin()` 호출. Update에서 GenerationTurn일 때 `1/2/3` → `_roulette.StopTube(i)`. 매 프레임 `AllStopped && !턴resolved`면 `ResolveGenerationTurn()`(CollectResults→EffectResolver 훅→Resolve→전력 누적). `AdvanceState`의 GenerationTurn 진행을 **턴 resolved 이후에만** 허용(미완료 시 Space 무시). ResetRun에서 통관 리셋. 층 재시도/다음 턴 시 통관 재시작. |
| `Data/PrototypeConfig.cs` | 시각/이동 파라미터 추가: `visibleBallsPerTube`(예 7), `tubeHeight`(예 6), `harvestWindowOffset`(수확창 Y, 예 -1.5), `streamLength`(스트림 길이, 예 32). 기존 `ballMoveSpeed/ballSpacing/brakeDelay` 활용. |
| `UI/PrototypeUI.cs` | 통관 상태 라인 + GenerationTurn 정지 힌트 추가. RunController에서 통관 상태 읽기용 getter 사용. |

## RunController 신규 내부 상태
- `bool _turnResolved` — 이번 GenerationTurn의 3통관 정지+판정 완료 여부. GenerationTurn 진입 시 false, 판정 후 true.
- getter: 통관 상태/정지 구슬을 UI에 노출(예 `RouletteController` 참조를 통해).

## PrototypeConfig 추가 값 (섹션 11 — 모두 Inspector 조정)
| 필드 | 예시값 | 용도 |
|------|------|------|
| `ballMoveSpeed` | 5 | 구슬 하강 속도(기존) |
| `ballSpacing` | 1 | 구슬 간격(기존) |
| `brakeDelay` | 0.35 | 정지 버튼 후 제동 지연(기존) |
| `visibleBallsPerTube` | 7 | 통관당 표시 구슬 수 |
| `tubeHeight` | 6 | 통관 세로 길이(순환 범위) |
| `harvestWindowOffset` | -1.5 | 수확창 Y 위치 |
| `streamLength` | 32 | 통관 구슬 스트림 길이 |

---

# Implementation Steps

### Step 1 — PrototypeConfig 이동/시각 파라미터
- **Description:** `PrototypeConfig.cs`에 `visibleBallsPerTube/tubeHeight/harvestWindowOffset/streamLength` 추가(`[Header("Tube Visuals")]`). 기존 필드 유지.
- **Assigned role:** developer
- **Dependencies:** None
- **Parallelizable:** Yes

### Step 2 — TubeController 구현
- **Description:** `Scripts/Roulette/TubeController.cs`(MonoBehaviour) 작성. 시드 스트림 주입(`SetStream(IReadOnlyList<BallDefinition>)`), `StartScroll()`, `RequestStop()`, `IsStopped`/`IsBraking`/`StoppedBall`. `Update`에서 결정론적 스크롤 + 제동 타이머 처리, 정지 시 수확창 최근접 구슬 선택. 구슬 시각 풀(런타임 생성 스피어 + URP/Unlit 머티리얼 debugColor 틴트) 위치 갱신. config 참조.
- **Assigned role:** developer
- **Dependencies:** Step 1
- **Parallelizable:** No

### Step 3 — RouletteController 오케스트레이션 재작성
- **Description:** `RouletteController.cs`를 3 `TubeController` 관리로 재작성. `[SerializeField] TubeController[] _tubes`(3개), `_database`, `InitializeSeed`. `StartSpin()`=시드 RNG로 통관별 스트림 생성·배정·하강. `StopTube(int)`=범위 검사 후 제동. `AllStopped`. `CollectResults()`=정지 구슬 3개. 통관 상태 조회 getter.
- **Assigned role:** developer
- **Dependencies:** Step 2
- **Parallelizable:** No

### Step 4 — RunController GenerationTurn 통합
- **Description:** `RunController.cs` 수정. GenerationTurn 진입 훅을 `StartSpin()`으로 교체, Update에 `1/2/3` 정지 입력(GenerationTurn 한정), 매 프레임 `AllStopped`시 1회 `ResolveGenerationTurn()`(CollectResults→`_effects.ResolveEffects()` 훅→`_resolver.Resolve`→전력 누적, `_turnResolved=true`). `AdvanceState` GenerationTurn 케이스는 `_turnResolved`일 때만 진행(아니면 무시+안내 로그). 층 재시도/다음 턴 진입 시 통관 재시작·`_turnResolved` 초기화. ResetRun에서 통관 리셋.
- **Assigned role:** developer
- **Dependencies:** Step 3
- **Parallelizable:** No

### Step 5 — PrototypeUI 확장
- **Description:** `PrototypeUI.cs`에 통관 상태 라인 + GenerationTurn 정지 힌트 표시 추가.
- **Assigned role:** developer
- **Dependencies:** Step 4
- **Parallelizable:** No

### Step 6 — 씬 구성 & 배선
- **Description:** `Prototype_Elevator.unity`에 `TubesRoot` + 통관 0/1/2(각 반투명 프레임 + 수확창 마커 + 구슬 풀 컨테이너, `TubeController` 부착) 생성. `GameSystems`의 `RouletteController._tubes`에 3통관 배선, 각 `TubeController._config` 배선. Main Camera를 세 통관 정면으로 위치 조정. `SampleScene` 무수정. 씬 저장.
- **Assigned role:** developer
- **Dependencies:** Steps 2, 3, 4, 5
- **Parallelizable:** No

### Step 7 — 컴파일 & Play Mode 검증 (스크린샷 포함)
- **Description:** 콘솔 에러 0 확인. Play Mode에서 통관 하강·개별 정지·제동 지연·수확 선택·자동 판정·Space 진행 확인. Game View 스크린샷으로 3통관+구슬 가시성 시각 검증. 시드 고정 시 스트림 재현 확인.
- **Assigned role:** coordinator
- **Dependencies:** Step 6
- **Parallelizable:** No

---

# Verification & Testing (T-02 완료 기준)
1. Play Mode 진입 시 **Console Error 0**.
2. GenerationTurn 진입 시 세 통관에서 구슬이 아래로 흐르고 순환한다(시각 확인).
3. `1/2/3`로 각 통관을 **독립적으로** 정지할 수 있고, 정지되지 않은 통관은 계속 움직인다.
4. 정지 버튼 후 **짧은 제동 지연** 뒤 멈추며, 수확창 최근접 구슬이 결과로 확정된다.
5. 세 통관이 모두 정지되면 **자동으로** 조합·전력 판정이 수행되고 결과가 표시된다.
6. 판정 완료 후 `Space`로만 다음 턴/정산으로 진행된다(미완료 시 Space 무시).
7. 3턴 발전 → PowerResolution → (성공/실패·재시도) → 초과배분 → 상승 루프가 계속 반복된다(T-01 기능 유지).
8. **같은 `randomSeed`**로 리셋 시 통관 구슬 스트림이 재현된다(섹션 6).
9. 이동/제동/시각 파라미터를 `PrototypeConfig`에서 조정 가능(하드코딩 없음).
10. `EffectResolver` 훅이 판정 경로에 존재하되 T-02에서 no-op 통과(T-03 자리 유지).
11. 기존 씬·기능 무손상. 되돌리기: 신규 파일 삭제 + 수정 파일 원복 + 씬 통관 오브젝트 제거로 롤백.

---

# T-03 진입 조건 (참고)
- `RouletteController.CollectResults()`가 실제 정지 구슬 3개를 안정적으로 공급하고, `RunController.ResolveGenerationTurn()`에 `EffectResolver.ResolveEffects()` 훅 지점이 확보되어 있으면, T-03에서 확률/가산/배율/변환/복제/제거/재발동 연쇄 효과를 이 훅에 삽입할 수 있다.
