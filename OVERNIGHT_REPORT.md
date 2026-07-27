# OVERNIGHT_REPORT

1차 프로토타입 자율 구현 작업 보고
작업일: 2026-07-28 / 브랜치: `prototype/overnight` / 기준: `main`

---

## 1. 한 줄 요약

**T-00 ~ T-08의 완료 조건을 충족했고, 10층 미니 런을 처음부터 끝까지 플레이할 수 있다.**
T-09는 요청대로 임시 권고안까지만 작성했다.
작업 도중 Codex 사용 한도가 소진되어 후반부는 직접 구현했다.

## 2. 티켓별 상태

| 티켓 | 상태 | 근거 |
|---|---|---|
| T-00 프로젝트 골격 | 완료 (기존) | 씬·컨트롤러·상태 전환·config 노출 모두 존재. Play Mode 동작 확인 |
| T-01 한 층 발전 루프 | 완료 (기존) | 6단계 상태 흐름이 순서대로 동작하고 다음 층에서 재시작됨 |
| T-02 3통관 구슬 정지 장치 | 완료 (기존) | 확률 합계 100.00%, 60,000회 분포 수렴, 시드 재현성, 독립 정지 실증 |
| **T-03 범용 연쇄 효과 처리기** | **완료 (이번)** | 7종 효과·고정 순서·3중 루프 방어·로그. 결함 3건 교정 |
| **T-04 승객 5종과 무게** | **완료 (이번)** | 5종 생성, 탑승 즉시 무게·요구전력·효과 갱신 실증 |
| **T-05 초과 전력 분배** | **완료 (이번)** | 두 선택지 값 노출, 전력 보존, 선택 가치 역전 확인 |
| **T-06 과적 사고** | **완료 (이번)** | 확률 사전 공개, 원인·손실 표시, 시드 재현. 허용중량 재조정 |
| **T-07 10층 미니 런** | **완료 (이번)** | 성공/실패 판정, 종료 화면, 재시작 완전 초기화 |
| **T-08 플레이테스트 로그** | **완료 (이번)** | 12런 JSON+CSV, 자동 검증 14항목 전부 통과 |
| T-09 콘텐츠 확장 여부 | **임시 권고안만** | 최종 확정은 사람 플레이테스트 이후 (요청대로) |

## 3. 실행 방법

1. Unity **6000.5.5f1** 로 `B:\Projects\Upandup_DDD` 열기
2. `Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity` 열기
3. Play

### 조작 (상태별로 게이트됨)

| 상태 | 키 |
|---|---|
| 승객 선택 | `[1]` `[2]` 후보 탑승 · `[0]` 건너뛰기 · `[Space]` 진행 |
| 발전 턴 | `[1]` `[2]` `[3]` 통관 A·B·C 정지 · 전부 정지 후 `[Space]` |
| 초과 전력 | `[1]` 돈 · `[2]` 추가 상승 · `[Space]` 확정 |
| 상시 | `[R]` 재시작 · `[P]` 구슬 확률표 토글 |

## 4. 테스트 방법

Unity 에디터 메뉴:

- `Ascend/Run Self Tests` — 자동 검증 14항목
- `Ascend/Run Playtest Simulation` — 12런 시뮬레이션, `PlaytestLogs/`에 JSON + CSV 2종
- `Ascend/Generate Effect Assets` / `Ascend/Generate Passenger Assets` — 에셋 재생성 (기존 파일은 건너뜀)

## 5. 테스트 결과

### 자동 검증 — 14 PASS / 0 FAIL
구슬 확률 합계, 등급 분포, 요구 전력 공식(정상·과적·무게영향), 초과 전력 보존과 상한,
효과 적용 순서, 무한 Repeat 방지(발동 횟수·재귀 깊이), 재시작 초기화,
같은 시드 재현성 및 다른 시드 상이.

### 자동 런 12회
성공 6/12(50%), 평균 최고층 8.2, 평균 돈 721, 평균 사고 0.25회.
정책별 성공률은 경량 2/4, 균형 2/4, 과적 2/4로 동일했다.
**9층이 병목**(통과율 25%)이며 실패 원인은 전부 요구 전력 미달이었다.
상세는 `PLAYTEST_SUMMARY.md`.

### Play Mode 실플레이
층 사이클 전체, 승객 탑승 반영(무게 0→3 → 요구 100→106), 통관 독립 정지,
완벽 정지 시 도박사 Repeat로 전력 67.2 → **134.4(정확히 2배)**,
실패 시 재시도 복귀, 층 전이, 재시작 초기화까지 전부 확인.
NullReferenceException 0건, 씬 참조 NULL 0건.

## 6. 핵심 아키텍처

```
RunController          상태 머신 소유. 성공/실패 판정. 입력 게이트
├ FloorController      현재 층과 요구 전력   ─┐
├ RouletteController   통관 3개 오케스트레이션 │
│ └ TubeController×3   스크롤·제동·스냅·수확  │
├ CombinationResolver  3구슬 조합 판정        │  전부 FloorMath /
├ EffectResolver       얇은 어댑터            │  CombinationEvaluator /
│ └ EffectPipeline     순수 C# 효과 연쇄      │  EffectPipeline 재사용
├ PassengerManager     후보 생성·탑승·효과 집계│
└ PrototypeUI          읽기 전용 HUD         ─┘

FloorMath              순수 static 공식 (요구전력·사고확률·초과전력)
RunSimulator           씬 없는 헤드리스 런. 위 공식을 그대로 호출
```

**설계상 가장 중요한 결정 두 가지:**

1. **계산 로직을 MonoBehaviour 밖으로 뺐다.**
   `FloorMath`·`EffectPipeline`·`CombinationEvaluator`가 순수 C#이라
   시뮬레이터가 씬 없이 같은 코드를 돌린다. 시뮬레이터가 자기 공식을
   갖는 순간 측정값이 실제 게임을 설명하지 못하게 된다.

2. **효과를 핸들러 레지스트리로 분리했다.**
   새 효과 추가 = 핸들러 파일 1개 + enum 값 1개. `EffectPipeline` 본문은
   건드리지 않는다.

## 7. 변경한 주요 파일

### 신규
```
Scripts/Effects/  EffectPipeline, GenerationContext, EffectDefinition,
                  EffectLogEntry, EffectTrigger, EffectCondition,
                  EffectResolverSettings, IEffectHandler, IEffectRandom,
                  Handlers/ (7종)
Scripts/Core/     FloorMath, PassengerManager, RunOutcome, OverchargeOption
Scripts/Data/     PassengerDefinition
Scripts/Sim/      RunSimulator, SimPolicy, SimRecords, BallDrawer
Editor/           EffectAssetGenerator, PassengerAssetGenerator,
                  PlaytestSimRunner, PrototypeSelfTest
Data/             Effects/ (5), Passengers/ (5), EffectResolverSettings.asset
```

### 수정
```
Scripts/Core/     RunController(대폭), ElevatorState, FloorController
Scripts/Data/     PrototypeConfig (신규 필드, 기존 보존)
Scripts/Roulette/ RouletteController(IsPerfectStop), TubeController(LastStopDistance만),
                  CombinationResolver(BuildContext, DetermineType 공개)
Scripts/UI/       PrototypeUI (전면 재작성)
Scenes/           Prototype_Elevator.unity (PassengerManager 추가 + 참조 배선)
```

**`TubeController`의 스크롤 수학은 손대지 않았다.** 정상 동작 중이었고
가장 깨지기 쉬운 부분이다.

## 8. 밸런스 변경 (근거 있는 것만)

| 항목 | 변경 | 이유 |
|---|---|---|
| `allowedWeight` | 100 → **8** | 승객 5종 총무게가 17이라 100에서는 과적이 **구조적으로 불가능**했고 T-06이 죽은 콘텐츠였다 |

그 외 수치는 바꾸지 않았다. 추측으로 조정하면 근거가 남지 않는다.

## 9. 남은 오류 / 미해결

**컴파일 에러 0, 런타임 예외 0.** 기능적으로 막힌 것은 없다.

미완성으로 남긴 것:

1. **Probability 효과가 구슬 추첨에 연결되지 않았다.**
   구조(`PendingProbabilityModifiers`)는 있고 소비처만 없다.
   7종 중 유일하게 게임플레이 영향이 없는 효과다.
   "구슬 확률을 빌드로 바꾼다"는 99번 문서의 핵심 기둥이므로 **다음 작업 1순위**다.

2. **로그 접두사 불일치** — 효과 파이프라인 일부가 `[상승]`이 아닌 `[EffectPipeline]`.
   기능 영향 없음.

## 10. 도구 관련 보고 (사람 조치 필요)

1. **Gemini CLI 사용 불가** — `IneligibleTierError`. 무료 티어 중단.
   Antigravity 마이그레이션 필요. 조사·독립검증·시각분석 역할이 비어 있다.
2. **Codex 사용 한도 소진** — `try again at Aug 27th`.
   T-08·UI부터는 직접 구현했다. 두 작업 모두 파일을 쓰기 전에 중단되어 손상은 없다.
3. **Unity가 Play 모드로 방치되어 있었다** — 이 상태에서는 스크립트가
   컴파일되지 않는다. 자동화 전 `isPlaying` 확인이 필요하다.

**구조적 약점:** Gemini와 Codex가 모두 빠지면서 후반부는 **구현자와 검토자가
같은 모델**이 되었다. 제3자 시선이 없다.

## 11. 사람이 직접 확인해야 하는 항목

`PLAYTEST_SUMMARY.md` 11절의 `HUMAN PLAYTEST REQUIRED` 목록을 볼 것.
요약하면 **재미·조작감·긴장감·재플레이 욕구는 전혀 측정되지 않았다.**

특히 다음 셋은 수치로 대체 불가:
- 제동 지연과 완벽 정지 허용 오차(0.12)의 조작감
- 낙하 중 구슬 9종·등급의 실제 판독성
- 조합 실패 59%가 답답한지 납득되는지

## 12. T-09 임시 권고안 (확정 아님)

자동 로그만을 근거로 한 **잠정** 판단이다. 사람 플레이테스트 전까지 확정하지 말 것.

### 유지 권고
- **3통관 독립 정지 구조** — 기술적으로 안정적이고 확장(통관·구멍 추가) 여지가 열려 있다
- **승객 무게 ↔ 요구 전력 연동** — 탑승 즉시 요구 전력이 오르는 것이 수치로 확인된다
- **초과 전력 2선택** — 정책별 최종 돈이 4배 이상 벌어졌다. 선택이 실제로 결과를 바꾼다
- **효과 파이프라인** — 데이터로 효과를 추가할 수 있고 폭주 방어가 검증되었다

### 수정 권고
- **조합표 재설계** (우선순위 1) — 조합 실패 59%, 성공의 절반이 2종에 집중.
  ThreeOfAKind·ThreeDifferentCommon·SpecificOrder는 합쳐 4.5%로 사실상 사문화.
  9종에서 3개 뽑는 구조에 맞는 조합 규칙이 필요하다
- **9층 난이도** (우선순위 2) — 통과율 25%로 유일하게 평균 달성이 요구를 밑돈다.
  마지막 층만 벽으로 느껴질 위험
- **빌드 차별화** (우선순위 3) — 정책 3종 성공률이 2/4로 동일했다.
  다만 n=4라 통계적으로 무의미하므로 **정책당 50런 이상 재측정이 선행되어야 한다**
- **Probability 효과 연결** — 미구현 상태로는 핵심 기둥 하나가 비어 있다

### 폐기 권고
**현재로선 없다.** 자동 로그만으로 기능 폐기를 판단할 근거가 부족하다.

## 13. 다음 작업 우선순위

1. **사람 플레이테스트 1회** — 위 모든 판단의 전제다. 이게 없으면 나머지는 추측이다
2. **Probability 효과를 `RouletteController` 추첨에 연결** — 핵심 기둥 완성
3. **조합표 재설계** 후 시뮬레이션 재측정
4. **정책당 50런 이상으로 재측정** — 빌드 차별화 판단의 통계적 근거 확보
5. 9층 난이도 조정 (`requiredPowerGrowthPerFloor` 또는 조합 배수)
6. T-09 최종 확정

---

## 부속 문서

- `OVERNIGHT_ASSUMPTIONS.md` — 문서에 없어 직접 결정한 항목 전체
- `KNOWN_ISSUES.md` — 남은 문제와 리스크
- `PLAYTEST_SUMMARY.md` — 자동 테스트 상세 결과
- `.orchestration/2026-07-28-prototype/` — Codex에 넘긴 설계 명세 원본
- `PlaytestLogs/` — 시뮬레이션 원본 데이터 (gitignore)
