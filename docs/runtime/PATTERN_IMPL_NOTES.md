# 상대 배치 패턴 Duo·Cross 구현 보고 — 2026-08-09

**범위** `PLAN_BUILD_DEPENDENCY.md` §C-7 「1단」만. Stripe·Frame·Cycle(2단), 4번째 심볼(3단)은
손대지 않았다.

**권한** `Assets/Prototype_Elevator/Scripts/Spin/` 아래 `.cs` 5개만 수정했다. 컴파일·자체
검증 실행은 팀장(코디네이터) 담당 — 여기 적힌 것은 내가 코드로 확인한 것과 별도 콘솔
스크래치로 실측한 것이다.

---

## 0. 사용자 원문 요구와 이 구현의 대응

> 「구슬 2종류가 어떤 모양으로 나와야 점수 더 주고, 그래야만 올라갈 수 있게」
> 「레버만 당겨서는 게임을 통과할 수 없게 해 줘. 적극적으로 승객과 아이템 빌드를 이용해야만」

- **Duo** — 한 종류의 연결 덩어리(2칸↑)가 다른 저항체와 인접하면 배수 2.5×. 실측 발생률
  **약 17.8%**(§4) — "레버만 당겨도" 심심찮게 걸리는 낮은 문턱의 상호작용 보상.
- **Cross** — 한 종류가 중심에서 다른 저항체에 완전히 둘러싸이면 배수 5.0×. 실측 발생률
  **약 0.02~0.04%**(§4) — 우연이 아니라 승객·부품으로 노려야 하는 자리. §C-4가 예고한
  `CrossPatternMode`(2단)가 아직 없으므로, **지금은 Cross가 사실상 장식에 가깝다**
  (§5에서 다시 짚는다).
- **NormalSoulValue 14.0→11.5** — 패턴 없는 기본산을 요구 전력 아래로 내려서, 패턴을
  잡아야 "그래야만 올라간다"가 실제로 성립하게 만드는 축. Duo·Cross 판정과 분리해서
  커밋하지 말라는 지시를 따라 한 커밋 단위로 묶는다.

---

## 1. 무엇을 어디에 추가했나

### `Assets/Prototype_Elevator/Scripts/Spin/PatternKind.cs`
- `PatternKind` 열거형에 `Duo = 3`, `Cross = 5` 추가. 기존 `Cluster`(3→4)·`FullBoard`(4→6)를
  배수 크기 순으로 다시 매겼다 — 근거는 §2.
- `DisplayName()`에 `"쌍"`/`"십자"` 케이스 추가.
- `TriggersRefill()`에 `PatternKind.Cross` 추가(`Duo`는 제외) — 근거는 §2.

### `Assets/Prototype_Elevator/Scripts/Spin/SpinRuleSet.cs`
- `NormalSoulValue`: `14f → 11.5f` (55행). 기존 20시드 튜닝 이력 주석은 지우지 않고
  2026-08-09 조정 근거를 이어 붙였다.
- `DuoMultiplier = 2.5f`(88행), `CrossMultiplier = 5.0f`(96행) 신규 필드.
- `PatternMultiplierFor()`의 `switch`에 `case PatternKind.Duo`(271행)·`case PatternKind.Cross`(273행)
  추가 — 안 넣으면 `default: return 0f;`로 떨어져 배수가 조용히 0이 된다.
- `Clone()`에 `DuoMultiplier`(367행)·`CrossMultiplier`(369행) 복제 추가 — 안 넣으면
  `SpinEngine.PrepareRules`가 매 스핀 `Clone()`을 부르는 통에 커스텀 값이 스핀마다
  기본값으로 되돌아간다(이 파일 기존 주석이 같은 함정을 경고하고 있어 재사용했다).

### `Assets/Prototype_Elevator/Scripts/Spin/SpinEngine.cs`
- 상수 3개: `DuoMinimumSize = 2`(481행), `CrossCenterIndex = 4`(484행),
  `CrossWheelIndices = {1,3,5,7}`(492행— 열 우선 인덱싱에서 중심의 직교 이웃은 항상
  이 네 칸이라 매번 `SpinBoard.OrthogonalNeighbours`로 다시 구하지 않는다).
- `FindMatches()`(497행) 재구성:
  - 조기 탈출 문턱을 `rules.MinimumCountFor(kind)` 단독에서
    `Math.Min(DuoMinimumSize, rules.MinimumCountFor(kind))`(505행)로 낮췄다. **이걸 안
    하면 Duo가 통째로 안 뜬다** — 아래 §2-3 참조.
  - `cross`(512행)·`duo`(521행) 판정을 계산하고, 단일 매치 경로(`!AllowMultiplePatternsPerKind`,
    기본값)와 중복 매치 경로(업그레이드 해금) 양쪽의 if/else-if 사슬에 우선순위대로 끼워
    넣었다.
- 신규 메서드 3개:
  - `TryFindCross`(667행, `static`) — 중심이 다른 저항체이고 바퀴 4칸이 같은 종류인지 판정.
  - `TryFindDuo`(693행, 인스턴스) — 연결 덩어리(2칸↑)가 다른 저항체와 인접하는지 판정.
    `FindConnectedComponent`를 그대로 재사용한다(새 인접 개념을 안 만들었다).
  - `TouchesOtherResistance`(707행, 인스턴스) — 칸 목록이 다른 저항체와 닿는지 검사하는
    공용 헬퍼. 기존 `_neighbourBuffer`를 재사용해 새 배열을 할당하지 않는다.

### 테스트 (아래 §3에서 상세)
- `Assets/Prototype_Elevator/Scripts/Spin/Tests/SpinEngineTests.cs` — Duo·Cross 판정 테스트
  9개 신설 + **기존 테스트 1개(`TestRefillPreservesSurvivors`) 수정** (§5-1).
- `Assets/Prototype_Elevator/Scripts/Spin/Tests/SpinRuleSetTests.cs` — 배선 누락 방지
  테스트 2개 신설.

`git diff --stat`: 5개 파일, +558/−14.

---

## 2. 중복 계상을 어떻게 막았나

### 2-1. 우선순위 사슬 — 배수 크기 순

`FindMatches`의 단일 매치 경로(기본값)는 한 종류당 패턴 하나만 고른다. 새 패턴 둘을
끼워 넣으면서 순서를 이렇게 잡았다:

```
FullBoard(10.0×) > Cross(5.0×) > Cluster(3.0×) > Duo(2.5×) > Line(2.0×) > Scattered(1.0×)
```

**판단 근거:** 같은 칸 배치가 여러 등급을 동시에 만족할 수 있다는 것을 콘솔 스크래치로
먼저 확인했다(§4의 검증 방법론). 예:

- 십자의 바퀴 4칸 중 일부가 **모서리를 통해 직교로** 다른 자기 종류 칸과 이어지면
  Cross와 Cluster가 동시에 성립한다 — 대각 연결 스위치가 꺼져 있어도 난다(처음에는
  대각 연결이 있어야만 충돌한다고 잘못 생각했는데, 손으로 짠 반례를 스크래치에 돌려보니
  모서리 칸이 중심의 대각 이웃일 뿐 아니라 바퀴 칸 두 개와는 **직교로** 붙어 있었다).
- 직선 3개가 마침 다른 종류와 인접하면 Line과 Duo가 동시에 성립한다.
- 4칸 연결(Cluster)이 다른 종류와 인접하면 Cluster와 Duo가 동시에 성립한다.

우선순위를 배수 크기 순으로 두면 "더 어렵고 더 큰 배수를 요구하는 패턴이 이긴다"는
일관된 규칙이 되고, 기존 `FullBoard > Cluster > Line > Scattered` 사슬의 사상(더 희귀한
등급이 이긴다)과도 맞는다. `SpinEngine.cs` 543~549행 주석에 같은 근거를 남겼다.

`PatternKind` 열거형 값도 이 순서에 맞춰 재배치했다(`Scattered=1 < Line=2 < Duo=3 <
Cluster=4 < Cross=5 < FullBoard=6`) — `SpinResolution.Summary()`의 "최고 패턴"이
`p.Pattern > best`라는 **정수 비교**로 뽑히는데, 배수 순서와 열거형 순서가 어긋나면 그
표시가 배수보다 약한 패턴을 "최고"라고 잘못 부를 수 있다. 재배치가 안전한 이유(직렬화
안 됨, 소비하는 3개 파일 전부 이름으로 switch)는 `PatternKind.cs` 상단 주석과 아래
§5-2에 적었다.

### 2-2. 중복 계상이 실제로 일어나지 않는다는 것을 테스트로 고정

우선순위만으로는 "설계 의도"이지 "보장"이 아니다. §3의 `TestCrossOutranksClusterWhenBothQualify`·
`TestDuoOutranksLineWhenBothQualify`·`TestClusterOutranksDuoWhenBothQualify` 세 개가
**두 조건이 실제로 동시에 성립하는 보드**를 만들어 놓고 `Purifies` 배열에 해당 종류
발동이 **정확히 1건**만 있는지 확인한다. 우선순위 사슬(if/else-if)이 원래 하나만
고르게 되어 있지만, 그 사실 자체를 반례 보드로 검증하지 않으면 "설계상 안 겹친다"가
그냥 주장으로 남는다.

### 2-3. Duo의 조기 탈출 문턱 — 발견한 함정

`FindMatches`의 원래 첫 줄은 `if (board.CountOf(kind) < rules.MinimumCountFor(kind)) continue;`
였다. 이 값은 기본 3이고, 기존 네 패턴(Scattered 3·Line 3·Cluster 4·FullBoard 9)은 전부
3 이상을 요구하므로 이 가드가 안전한 최적화였다. **Duo는 2부터 성립한다** — 그대로
두면 2칸짜리 Duo가 이 줄에서 통째로 걸러진다. `Math.Min(DuoMinimumSize, rules.MinimumCountFor(kind))`로
낮춰서 고쳤다(SpinEngine.cs:505).

이 수정 자체가 §5-1에서 다루는 기존 테스트 파손의 **원인**이다 — Duo가 새로 뜨기
시작하면서, 예전에는 "너무 적어서 무시됐던" 2칸짜리 저항체 잔여가 이제는 정화 대상이
될 수 있다. 고의로 낮춘 문턱이라 되돌리는 방법은 이 상수를 지우거나 999 같은 큰 값으로
바꾸는 것이지만, 그러면 Duo 자체가 죽는다.

---

## 3. 추가한 테스트와 각각이 보장하는 것

### `SpinEngineTests.cs` (9개 신설)

| 테스트 | 무엇을 보장하는가 |
|---|---|
| `TestCrossDetectsWheelAroundDifferentCenter` | 중심이 다른 저항체·바퀴 4칸이 같은 종류면 Cross가 뜨고, 배수가 `rules.CrossMultiplier`와 일치하며, 정화 칸이 **바퀴 4칸만**(중심 제외)인지 |
| `TestCrossRequiresResistantCenter` | 중심이 저항체가 아니면(정상 영혼) Cross가 안 뜬다 — **거짓 양성 방지**. 부수 효과로 "바퀴 4칸은 중심을 거치지 않으면 서로 고립된다"는 전제도 함께 검증(무엇도 안 뜸) |
| `TestCrossOutranksClusterWhenBothQualify` | Cross·Cluster가 동시에 성립하는 반례 보드에서 흡수체 발동이 **정확히 1건**, 패턴은 Cross — **중복 계상 방지** |
| `TestCrossIsDeterministic` | 같은 판·같은 규칙을 두 번 풀어 `Equivalent()`로 완전 동일 확인 — Cross는 희귀해서(§4) 무작위 스윕 대신 직접 만든 판으로 결정론을 본다 |
| `TestDuoDetectsPairAdjacentToOtherKind` | 연결 2칸이 다른 저항체와 인접하면 Duo가 뜨고, 배수가 `rules.DuoMultiplier`와 일치하며, 정화 칸이 **자기 종류 2칸만**(상대 종류 칸은 미포함)인지 |
| `TestDuoRequiresAdjacencyToOtherKind` | 연결 2칸이 있어도 판에 다른 저항체가 전혀 없으면 아무것도 안 뜬다 — **거짓 양성 방지** |
| `TestDuoOutranksLineWhenBothQualify` | 직선 3개가 마침 다른 종류와 인접한 반례 보드에서 흡수체 발동이 정확히 1건, 패턴은 Duo(Line이 아님) — **중복 계상 방지 + 의도한 역전 고정** |
| `TestClusterOutranksDuoWhenBothQualify` | 4칸 연결이 다른 종류와 인접한 반례 보드에서 흡수체 발동이 정확히 1건, 패턴은 Cluster(Duo가 아님) — **중복 계상 방지** |
| `TestDuoIsDeterministicAcrossRandomSpins` | 실전 커리큘럼 가중치(`PrototypeCurriculum.For(8)`)로 4시드×500스핀을 두 엔진에 각각 돌려 매 스핀 `Equivalent()` 일치 + Duo가 최소 1회 이상 나왔는지(공허한 통과 방지). 기존 `TestPurifiedCellsAreContiguous`와 같은 시드·스핀 수 |

### `SpinRuleSetTests.cs` (2개 신설)

| 테스트 | 무엇을 보장하는가 |
|---|---|
| `TestDuoCrossMultiplierWiring` | `PatternMultiplierFor(Duo/Cross, kind)`가 `default: return 0f;`로 안 떨어지고 실제 필드 값을 돌려주는지 |
| `TestDuoCrossMultiplierSurvivesClone` | `DuoMultiplier`/`CrossMultiplier`를 기본값이 아닌 값(9.25/9.75)으로 설정한 뒤 `Clone()`해도 살아남는지 — **기본값으로 검사하면 이 버그가 안 잡히므로** 일부러 기본값이 아닌 값을 쓴다 |

### 이미 있던 테스트가 Duo·Cross를 공짜로 검증하는 부분

`TestPurifiedCellsAreContiguous`(기존, 2000스핀 스윕)는 패턴 종류를 가리지 않고 모든
`PurifyEvent.Cells`가 상호 인접하는지 검사한다. Cross의 바퀴 4칸이 대각으로 고리 모양
연쇄 인접을 이룬다는 것, Duo의 연결 덩어리가 `FindConnectedComponent`로 나온 이상
정의상 상호 연결이라는 것을 손으로 검증했고(§4), 이 기존 테스트가 실전 스핀에서도
같은 사실을 재확인해 줄 것이다 — 내가 손으로 짠 반례 하나로는 못 잡는 종류의 회귀를
이 테스트가 대신 잡는다.

---

## 4. 검증 방법론 — 실제로 돌려서 확인했다

이 저장소에는 dotnet CLI(`~/.dotnet/dotnet`, v10.0.302)가 있다. Unity 없이도 순수 C#
판정 로직은 독립적으로 재현할 수 있어서, `/tmp`(저장소 밖)에 `SpinBoard`/`FindMatches`와
**동일한 인접 규칙·우선순위 사슬**을 옮겨 심은 콘솔 스크래치를 만들어:

1. 손으로 설계한 반례 보드 9개(§3의 각 테스트가 쓰는 보드와 동일)를 실제로 돌려
   기대한 패턴이 정확히 나오는지 확인했다. **이 과정에서 최소 한 번은 손 계산이
   틀렸다** — 처음에는 "모서리 칸은 중심과 대각으로만 닿는다"고 가정했는데, 실제로는
   모서리가 바퀴 칸 두 개와 **직교로** 붙어 있었다(예: 인덱스0은 인덱스1·3과 직교
   인접). 3×3 인접 기하를 눈대중으로 틀리기 쉽다는 것 자체가 이번 검증에서 얻은
   교훈이라 테스트 파일 상단 주석에도 남겼다.
2. 게임 근사 가중치(정상영혼 60%·흡수체 20%·증식체 20%, 직교 연결 기본값)로 10000판을
   뽑아 패턴별 발생률을 쟀다:

   | 패턴 | 발생률(직교) | 발생률(대각 연결 켬) |
   |---|---|---|
   | None | 75.81% | 63.85% |
   | Scattered | 1.58% | 1.66% |
   | Line | 1.51% | 0.94% |
   | **Duo** | **17.75%** | **27.20%** |
   | Cluster | 3.32% | 6.32% |
   | **Cross** | **0.02%** | 0.03% |
   | FullBoard | 0.00% | 0.00% |

   (표본은 "종류 하나당 한 번 판정"을 단위로 센다 — 보드 하나에 흡수체·증식체 판정이
   각각 있으니 10000판 = 20000 표본.)

**이 표가 이 보고서에서 가장 중요한 숫자다.** 문서(`PLAN_BUILD_DEPENDENCY.md` §C-2·C-7)의
추정치("Duo 15%", "Cross 3%")를 **사실로 받아들이지 말고 코드에서 다시 재라**는 지시를
따른 결과, Duo는 추정과 비슷했지만(15%→17.75%) **Cross는 추정보다 100배 넘게
희귀했다**(3%→0.02%). 이 격차의 의미는 §5-2에서 다룬다.

가중치 근사치는 실제 `SpinBalanceProfile`/`PrototypeCurriculum`의 층별 값과 다를 수
있다 — Unity 실행 없이는 그 값을 직접 못 읽어서 임의로 근사했다. **정확한 발생률은
팀장의 `Ascend/Run Self Tests` 뒤 실제 커리큘럼으로 다시 재는 것을 권한다.**

스크래치 프로젝트는 검증 후 삭제했다(저장소에 포함되지 않음, `/tmp` 밖에 있던 적도 없음).

---

## 5. 기존 610개 중 깨질 것으로 예상되는 것

**정직하게 적는다 — 다 안전하다고 뭉개지 않는다.**

### 5-1. `TestRefillPreservesSurvivors` — 실제로 깨질 뻔했다, 고쳤다

원래 보드는 흡수체 2×2 클러스터 옆에 증식체 2칸(인덱스 2·5)을 흩어 놓고 "문턱(3) 아래라
살아남는다"를 검증했다. **인덱스 2·5는 서로 직교 인접이라 Duo 추가 후 그 2칸도 Duo로
같이 정화된다** — 재충전 가중치가 "증식체 100%"로 세팅돼 있어서 정화 후 같은 칸이
같은 심볼(증식체)로 다시 채워지는 바람에 `after[2] != Proliferator` 비교는 **우연히
계속 통과**하지만, "정화도 수확도 되지 않았다"는 테스트의 실제 주장은 깨져 있었다.

값 비교만으로는 못 잡는 종류의 파손이라 손으로 찾아서 고쳤다(증식체 위치를 서로
비인접한 인덱스 6·8로 옮기고, 1단계 발동이 정확히 1건이라는 방어적 어서션을 추가했다).
**이게 이번 작업에서 발견한 유일한 실제 파손이다.**

### 5-2. Cross가 사실상 죽어 있는 패턴이라는 것 — 파손은 아니지만 목표 미달 위험

§4의 실측대로 Cross는 기본 가중치에서 **10000판에 2~4번**꼴로만 뜬다. 이건 테스트
파손이 아니라 **밸런스 목표 달성 여부에 대한 정직한 경고**다:

- 사용자 목표("패턴을 잡은 런은 10층을 완주한다")를 Duo가 대부분 짊어져야 한다 —
  Cross는 통계적으로 "만날 수 있으면 좋은 보너스"이지 "노려서 쓰는 전략"이 되려면
  §C-4가 예고한 `CrossPatternMode`(중심 고정, 2단) 같은 빌드 효과가 있어야 한다.
- **지금은 없다.** 이번 1단 범위 밖이라 손대지 않았지만, Cross의 5.0× 배수가 실전
  런의 완주율 계산에 유의미하게 기여할 확률은 현재 거의 0에 가깝다는 것을 팀장이
  알고 있어야 한다.

### 5-3. 그 외 — 확인했고 안전하다고 판단한 것들

- **`PatternKind` 값 재배치(Cluster 3→4, FullBoard 4→6)**: 이 값을 직렬화하는 `.asset`/
  `.unity`/`.prefab`이 저장소에 없음을 grep으로 확인했다. 이 열거형을 소비하는 3개 파일
  (`View/SpinPresenter.cs`·`View/PurifyMarkerLayout.cs`·`Run/FloorSession.cs`)은 전부
  이름으로 `switch`하므로 재배치에 영향받지 않는다. **다만 이 세 파일은 Duo·Cross
  케이스를 모른다** — 셋 다 `default:` 안전 분기가 있어 크래시는 안 나지만(직접 읽어서
  확인), Duo·Cross가 발동해도 전용 시각·이벤트 처리를 못 받고 `default` 취급(펄스 1회,
  마커 없음, `PurifyScattered` 텔레메트리)된다. UI 쪽 파일이라 이 티켓 권한 밖이다.
- **`NormalSoulValue` 14→11.5**: 이 값에 의존하던 과거 파손 이력을 두 건 찾았는데
  (`SpinEngineTests.TestMaxDepthHarvest`, `RunTests.TestAnteEscalation`) **둘 다 이미
  `rules.NormalSoulValue`/`session.Power`를 동적으로 읽도록 고쳐져 있었다**(각 파일
  자체 주석이 "예전에 상수를 박아 뒀다가 10→14 변경 때 깨졌다"고 기록). 같은 패턴을
  따르는 `BuildTests.TestEmptyLoadoutLeavesRulesAlone`도 상대 비교라 안전. 이 세 파일은
  Spin/ 밖이라 내가 고칠 권한이 없지만 읽어서 확인은 했다.
- **`Sim/RunSimulator.cs` 진단 카운터**: `basePurifyCount`/`lineCount`/`clusterCount` 세
  변수가 `PatternKind.Scattered`/`.Line`/`.Cluster`·`.FullBoard`만 세고 Duo·Cross는 안
  센다(302~305행). **테스트가 깨지지는 않는다** — 이 파일에 Duo·Cross 값과 대조하는
  어서션이 없다. 다만 `grossPower`/`netPower` 같은 실제 전력 합계는 `PurifyEvent.Power`를
  패턴 무관하게 합산하므로 Duo·Cross 기여분이 누락 없이 들어간다. 진단용 세부 분류만
  비는 것이라 밸런스 판단에는 영향 없지만, 이 파일을 보는 사람이 "Duo·Cross가 안
  잡혔다"로 오해하지 않도록 적어 둔다.

---

## 6. 못 한 것 · 확신 없는 것

- **2단·3단 범위(Stripe·Frame·Cycle, 4번째 심볼)는 전혀 손대지 않았다.** 지시대로다.
- **Cross의 실전 도달 가능성.** §5-2 그대로 — 지금 상태로는 "노려서 맞추는" 패턴이 아니다.
  빌드 효과 없이 Cross가 실제 완주율에 기여하는지는 Unity 시뮬레이터(`RunSimulator`)로
  다시 재야 확정적으로 알 수 있는데, 나는 Unity를 못 열었다.
- **실제 게임 가중치로는 재현하지 못했다.** §4 표는 근사 가중치다. `SpinBalanceProfile`/
  `PrototypeCurriculum`의 층별 실제 값은 Unity `ScriptableObject` 직렬화라 코드만 읽어서는
  정확한 수치를 못 끌어냈다(추정치를 만들 수는 있었지만 "다시 쟀다"고 부를 만큼
  신뢰하지 않는다).
- **목표 수치("무빌드/무패턴 런 4~5층 정체, 패턴 잡은 런 10층 완주") 달성 여부는
  모른다.** 이건 팀장이 `Ascend/Run Self Tests`의 4개 실패 지표를 다시 재야 나온다.
  내가 한 것은 "그 지표가 움직일 수 있는 손잡이(NormalSoulValue↓ + Duo·Cross 판정)"를
  다는 것까지다.
- **`Duo`의 연결 성분 탐색이 "판 전체에서 가장 큰 덩어리 하나"만 본다**(`FindConnectedComponent`
  재사용, `SpinEngine.cs:693` 주석에 적어 뒀다). 이론상 "더 큰 덩어리는 다른 종류와 안
  닿고, 더 작은 덩어리가 따로 있는데 그건 닿는" 보드에서 Duo를 놓칠 수 있다. 3×3처럼
  작은 판에서 실제로 몇 번이나 이런 경우가 나는지는 세어 보지 않았다 — Cluster·Scattered도
  이미 같은 단순화를 쓰고 있어서 이 판정만 예외로 만들지 않았지만, 확신은 없다.
- **`SpinPresenter`/`PurifyMarkerLayout`/`FloorSession`의 Duo·Cross 전용 처리**는 UI/Run
  권한이라 손대지 않았다 — §5-3에 크래시 안 남만 확인해 뒀다.
- **PatternKind 값 재배치가 다른 어느 파일에도 영향이 없다**는 확신은 grep 기반이다
  (직렬화 여부·switch 방식). Unity 컴파일러가 실제로 어떻게 처리하는지는 팀장의 컴파일
  결과로 최종 확인해야 한다.

---

## 7. 되돌리는 법 (문서 §「되돌리는 법」과 일치시킴)

⚠ **2026-08-09 라운드 2 갱신** — `NormalSoulValue`는 이미 14.0으로 되돌아갔다(§8).
아래는 **Duo·Cross 판정 자체**를 되돌리는 절차다.

```
1단 롤백:
  SpinEngine.FindMatches 의 cross/duo 판정과 조기 탈출 문턱 완화를 제거
  (PatternKind.Duo/.Cross 열거형 값은 남겨도 무해하다 — 아무도 안 만들면 그만이다)
  SpinRuleSet.NormalSoulValue 는 이미 14.0 — 되돌릴 것이 없다
```

---

## 8. 2026-08-09 라운드 2 — 자체 검증 피드백 대응

팀장이 자체 검증(622 PASS / 7 FAIL, 라운드 1 대비 실패 +3)을 돌려 받은 피드백에 대한
조치. 산출물은 그대로 이 파일이 이어서 담는다 — 별도 문서로 안 쪼갰다.

### 8-1. 결정론 회귀 — 버그 아님, 확인 후 재확정

**팀장 진단**: "패턴 판정이 난수를 소비하고 있다 — 판정은 순수 함수여야 한다."

**조사 결과**: 아니다. `TestExtraRerollZeroIsBitIdentical`이 잡은 것은 난수 소비가 아니라
**못 박은 절대값의 노후화**다. 근거:

1. 이 테스트는 `new SpinRuleSet { ... }`를 직접 만들어 `BoardRules()`(프로파일 경유)를
   피했다 — 그런데 `NormalSoulValue`는 프로파일이 아니라 **C# 필드 기본값**이라 그
   방어를 그냥 통과했다. 14→11.5 변경이 이 못을 흔든 것이 그 결과다.
2. 콘솔 스크래치로 실제 `SpinBoard`/`SpinEngine`/`SpinRuleSet`을 그대로 옮겨 3가지
   변형을 돌렸다(같은 40시드, `MaxCascadeDepth=2, ExtraCascadeRerollCells=0`):
   - **A**: 현재 코드 그대로(NSV=11.5, Duo·Cross 켜짐) → `GrossPower` 합 5221.8999
   - **B**: NSV만 14로 강제(Duo·Cross는 그대로 켜짐) → 5786.8999
   - **C**: NSV=14 + Duo·Cross 판정 자체를 코드에서 제거(옛 사슬 재현) → **4566.7999**
     — 옛 못값과 **소수점까지 정확히 일치**(차이 0.0000).
   - 세 변형 전부 `refilled`(재충전 걸린 스핀 수) = **6/40으로 동일** — 캐스케이드
     구조·재충전 여부가 안 바뀌었다는 직접 증거다.
3. Variant C가 옛 값을 정확히 재현한다는 것은 (a) 내 `SpinEngine.cs` 리팩터링이 판정
   로직 밖에서는 아무것도 안 건드렸다는 것과, (b) 이 dotnet 10 스크래치의 `Random(seed)`
   수열이 이 못값을 원래 낸 런타임(Unity/Mono로 추정)과 **일치한다는 것**을 동시에
   증명한다 — 안 그랬으면 C도 안 맞았을 것이다.

**결론**: 난수 소비 순서·횟수는 바뀌지 않았다. `GrossPower` 값이 바뀐 이유는 둘뿐이다 —
`NormalSoulValue`가 정상 영혼 전력에 직접 곱해지는 것(A vs B 차이, −565.00)과, Duo가
이 40시드에서 28번 새로 발동해 정화 전력이 늘어난 것(B vs C 차이, +1220.10) — **둘 다
의도한 변경**이다. Cross는 이 40시드에서 0건.

**조치**: `SpinEngineTests.cs`의 `TestExtraRerollZeroIsBitIdentical`을 고쳤다(파일:줄
441~490). `NormalSoulValue = 14f`를 테스트 안에서 **직접 못 박고**(원래 이 테스트가
`BoardRules()`를 피해서 지키려던 의도를 마저 지킨 것), 못값을 5786.8999로 갱신했다.
이제 `SpinRuleSet`의 필드 기본값이 앞으로 또 바뀌어도 이 테스트는 안 흔들린다.

### 8-2. 시드 271828 — 재현하고 원인을 특정했다

콘솔 스크래치에 실제 `PrototypeCurriculum`/`FloorPlan.cs`까지 옮겨 심어 "빌드·계약 없이
1층을 5스핀 도는" 시뮬레이션을 돌렸다(`FloorSession`/`RunSession`의 과수확·잔류 이월·
푸시유어럭 같은 상위 로직은 뺀, 순수 스핀 누적치 — 그래서 이 결과가 **하한**이다).

**시드 271828은 NormalSoulValue=11.5에서 1층 도달율 63%로 Crash(70% 미만) 문턱
아래였다.** NormalSoulValue=13.5에서 79%, 14.0에서 82%로 올라간다 — 딱 이 필드 하나가
경계를 갈랐다.

더 넓게 재서 **왜** 이 필드가 문제였는지 찾았다 — §8-3.

### 8-3. 왜 — Duo·Cross가 못 미치는 곳에 부담이 갔다

`PrototypeCurriculum`을 읽어보니 **1~5층의 저항 풀은 흡수체 하나뿐**이다
(`SymbolPool = SoulAndAbsorber`, `FloorPlan.cs`). 증식체는 6층부터 들어온다. Duo·Cross는
"저항체 하나가 **다른** 저항체와 어떤 모양을 이루는가"로 성립하는 패턴이라 —
**1~5층에서는 정의상 절대 뜰 수 없다.**

`NormalSoulValue`는 전 층 공통 필드다. 이걸 낮추면 1~5층도 6~10층과 똑같이
어두워지는데, 6~10층엔 그 어두움을 벌충할 Duo·Cross가 있고 1~5층엔 없다. 즉 이
필드 하나를 내리는 것은 "패턴 없이는 못 간다"를 만드는 게 아니라 **"패턴이 안 통하는
구간을 무차별로 더 어렵게 만드는" 것**이었다.

무빌드·계약없음·300시드 표본 (`docs/runtime/PATTERN_IMPL_NOTES.md`가 이 문서이므로
자기 참조 대신 수치만 다시 적는다):

| 층 | 요구 전력 | 저항 풀 | NSV=11.5 크래시율 | NSV=14.0 크래시율 |
|---|---|---|---|---|
| 1 | 330 | 흡수체만 | 5.7% | 1.0% |
| 2 | 430 | 흡수체만 | 5.0% | 2.0% |
| 3 | 550 | 흡수체만 | 7.7% | 5.7% |
| 4 | 680 | 흡수체만 | **43.3%** | **30.0%** |
| 5 | 820 | 흡수체만 | **56.7%** | **41.0%** |
| 6 | 970 | 3종(Duo·Cross 가능) | 42.0% | 34.3% |
| 7 | 1130 | 3종 | 47.3% | 40.7% |
| 8 | 1300 | 3종 | 56.3% | 50.7% |
| 9 | 1480 | 3종 | 58.7% | 54.7% |
| 10(계약 강제) | 1670 | 3종 | 22.0% | 18.7% |

핵심 관찰 셋:

1. **4~5층은 NSV=14.0(원래 값) 만으로도 이미 30~41%/41~57% 크래시율이다.** "무빌드
   런이 4~5층에서 막힌다"는 목표는 이 필드를 안 건드려도 요구 전력 곡선 자체가
   이미 만들고 있었다.
2. **6~9층은 Duo가 런당 5~7회씩 터지는데도(300시드에 1500~1800건) 크래시율이
   34~59%로 여전히 높다** — 패턴이 "터지기만 하면 이긴다"가 아니라 "터져도 빠듯하다"
   수준이라는 뜻이고, 이건 오히려 "빌드+패턴이 있어야 안정적으로 넘는다"는 목표에
   가깝다.
3. **10층은 계약이 강제라(§C-2 무관하게 이미 그런 설계) 크래시율이 오히려 6~9층보다
   낮다** — 계약 하나만으로도 상당한 보정이 있다는 뜻이라, 실제 빌드(승객·부품)까지
   더해지면 "3시드 이상 10층 완주"는 낙관적으로 보인다. 다만 이건 계약까지만 반영한
   수치고 승객·부품 적재는 시뮬레이션에 안 넣었다 — 확인 안 됨.

### 8-4. 조치 — NormalSoulValue만 원복, 패턴 판정과 배수는 그대로

`SpinRuleSet.NormalSoulValue`를 **11.5 → 14.0으로 되돌렸다**(파일:줄 SpinRuleSet.cs:82,
필드 자체는 :168 부근 — 파일 안 주석 참고). `DuoMultiplier`(2.5)·`CrossMultiplier`(5.0)는
**손대지 않았다** — 6~9층 크래시율이 Duo가 자주 터져도 여전히 30~59%대로 유지되는 걸
보면 이 두 배수가 "터지면 무조건 이긴다"가 아니라 적당히 빠듯한 수준으로 이미 작동하고
있다고 판단했다. 바꿀 근거보다는 그대로 둘 근거가 더 뚜렷했다.

**이 결정이 놓친 것**: 위 표는 `FloorSession`/`RunSession`의 과수확 게이트·잔류 이월·
푸시유어럭·실제 승객/부품 적재를 **전혀 반영하지 않은 하한**이다. 그 위 계층은 다른
에이전트가 같은 시점에 바꾸고 있었고(과수확이 "전력만"에서 "전력+열쇠"로), 그
상호작용까지 재려면 `Run/`·`Build/` 권한이 필요해 이번 조치 범위 밖에 뒀다. 팀장이
재검증했을 때 6~9층 완주율이 여전히 부족하면, 다음으로 볼 곳은 이 필드가 아니라
① 과수확 열쇠 획득 경로가 무빌드 런에서 실제로 얼마나 막혀 있는지, ② 승객·부품
적재가 6층 이후 Duo·Cross 발동률을 실제로 얼마나 끌어올리는지 — 둘 다 Spin/ 밖이다.

### 8-5. 확인하지 못한 것 (라운드 2)

- 위 표는 **1개 층을 독립적으로** 돈 결과다. 실제 런은 층을 연달아 넘으며 잔류·소지금·
  과수확 상태가 누적된다 — 그 누적 효과는 안 쟀다.
- "적재하고도 10층 완주 0개"·"계약 건 런도 10층 완주 1회" 두 실패는 `Build/Tests/`
  권한이라 직접 재현하지 못했다. 위 §8-3 표의 6~10층 크래시율이 이 두 실패와 같은
  방향(높음)이라는 것까지만 확인했다 — NormalSoulValue 원복이 이 둘도 개선할 것으로
  **기대**하지만 재실행 전까지는 추정이다.
- 6층 이상에서만 `NormalSoulValue`를 따로 내리는(1~5층은 그대로, 6~10층만 압박) 축은
  만들지 않았다 — 필드 하나로는 층별 분기가 안 되고, 새 메커니즘 추가는 이번 라운드의
  "원복해서 회귀부터 없앤다"는 목표를 벗어난다고 판단했다. 팀장이 "그래도 6층 이상은
  더 어려워야 한다"고 재확인하면 다음 단계로 검토할 항목이다.
