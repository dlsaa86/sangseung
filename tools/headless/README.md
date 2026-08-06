# tools/headless — 유니티 없이 판정 계층을 돌린다

## 왜 있는가

밸런스 판정 코드는 이미 있다 — `Assets/Editor/BalanceSweep.cs` 와
`Assets/Editor/CurriculumCoverageProbe.cs`. 문제는 **그것을 돌리는 비용**이었다.
에디터 왕복은 도메인 리로드만으로 수십 초라 표본을 시드 300 개로 묶어 두게 된다.

그런데 그 툴들은 `UnityEditor` 를 `[MenuItem]` 어트리뷰트로만 쓰고,
그것이 부르는 계층(`Spin` · `Run` · `Build` · `Risk` · `Data.Profiles`)은 전부
순수 C# 이다. `Shim/` 의 최소 대역만 있으면 **같은 소스가 그대로** .NET 8 에서 돈다.

| | 유니티 에디터 | 여기 |
|---|---|---|
| 시드 300 × 정책 3종 | 도메인 리로드 포함 수십 초 | **1.0초** |
| 시드 30000 × 정책 3종 | 돌린 적 없음 | **33초** |

그 차이가 만드는 것은 편의가 아니라 **분해능**이다. 시드 300 에서 「3연쇄 이상
스핀 비율」의 ✅/❌ 는 어느 시드 블록을 뽑았는가로 결정된다 —
경위와 결과는 `docs/runtime/HEADLESS_BALANCE_AUDIT.md`.

## 두 가지 규칙

**① 판정을 여기서 다시 구현하지 않는다.** `Headless.csproj` 는 게임 소스를
경로로 **참조만** 한다. 복사본을 두는 순간 두 갈래가 되고, 이 저장소가 반복해서
당한 실패가 정확히 그것이다 — 재는 도구가 다른 게임을 잰다.

**② 대역은 Unity 의 정의와 같은 의미여야 한다.** `Shim/` 에 뭔가를 더할 때는
게임 코드가 **실제로 부르는 것만**, Unity 와 **같은 값을 내도록** 넣는다.
여기서 값이 갈라지면 산출 전체가 조용히 틀린다.

## ⚠ 이 기기에는 .NET SDK 가 설치돼 있지 않다 (2026-08-07 확인)

`C:\Program Files\dotnet` 에는 **런타임 8.0.22 뿐**이고 `dotnet build` 가
「No .NET SDKs were found」로 죽는다. 설치할 필요 없다 — **Unity 가 SDK 를 번들로 들고 있다.**

```bash
DOTNET="B:/Unity/6000.5.5f1/Editor/Data/DotNetSdk/dotnet.exe"   # SDK 8.0.318
"$DOTNET" build -c Release
```

`PATH` 앞에 붙이거나 `DOTNET_ROOT` 를 세우는 것으로는 **안 된다** — 머서가
`C:\Program Files\dotnet` 를 먼저 잡는다. **절대 경로로 부른다.**

## 쓰는 법

```bash
cd tools/headless

dotnet run -c Release -- sweep     30000              # 밸런스 스윕
dotnet run -c Release -- coverage  20000              # 커리큘럼 커버리지
dotnet run -c Release -- loadcurve  8000              # 적재량 0~6개 축
dotnet run -c Release -- overload   4000              # 적재의 대가(과적) 실측
dotnet run -c Release -- strategy   3000              # 서로 다른 빌드 전략이 있는가
dotnet run -c Release -- contracts  3000              # 계약이 선택인가 정답인가
dotnet run -c Release -- replicate   300 out/r.md 24  # 표본 잡음 측정

dotnet run -c Release -- tests                        # 프로젝트 자신의 검사 (1초)
```

### `tests` — 유니티 없이 자체 검증을 돌린다

`CLAUDE.md` §7 은 「자체 검증을 코드 변경 뒤에 돌리고 커밋한다」를 요구하는데, 그
유일한 실행 경로가 **에디터 메뉴**였다. 즉 에디터가 없거나 프로젝트에 컴파일 오류가
있어 도메인이 리로드되지 않는 상태에서는 검사를 돌릴 방법이 아예 없었다 —
그리고 그때가 바로 검사가 가장 필요한 순간이다.

`RunTests` · `MercyHungerTests` · `SpinEngineTests` · `SpinRuleSetTests` ·
`SimulatorParityTests` 를 **그대로 부른다**(판정을 다시 구현하지 않는다).
실측 86 PASS / 1.0초. **에디터 자체 검증을 대체하지 않는다** — `MonoBehaviour` 를
실제로 실행하거나 씬을 잡는 검사는 여기 없다.

### `strategy` — 「1등 없이도 갈 길이 있는가」

`build` 모드는 「그 품목 **하나만** 싣는 플레이어」를 잰다. 그 측정에서는 조건부
효과가 **정의상 한 번도 발동하지 않는다** — 조건을 켜 줄 다른 품목이 없기 때문이다.
그래서 단독 표는 조건부 설계를 항상 과소평가한다.

설계 질문은 「1등이 몇 %p 앞서는가」가 아니라 **「1등을 못 뽑은 런도 갈 길이 있는가」**다.
이 모드는 축별 원형 빌드의 완주율과, **1등 품목을 금지한 플레이어 vs 전부 집는
플레이어**를 나란히 낸다.

### `contracts` — 격차가 아니라 **단조성**을 본다

계약이 선택인지 정답인지는 평균 격차로 판정할 수 없다. 격차가 커도 층마다 정답이
다르면 선택이고, 격차가 0이어도 언제나 같은 것이 1등이면 정답이다. 그래서 이 모드는
① 층마다 인덱스 순서대로 단조 증가하는가 ② **빌드를 바꾸면 1등이 뒤집히는가**
둘을 낸다. ②가 노션 §4 「현재 빌드에 가장 유리한 저항 계약은 무엇인가」에 직접 답한다.

산출은 `tools/headless/out/` 에 떨어진다. **`docs/runtime/` 을 직접 덮어쓰지 않는다** —
표본 수가 다른 보고서가 같은 이름으로 섞이면 어느 쪽이 무엇인지 판정할 수 없게 된다.
채택할 산출만 사람이 옮긴다.

### `replicate` 가 답하는 것

같은 스윕을 **겹치지 않는 시드 블록**으로 여러 번 돌려, 각 지표가 표본 잡음만으로
얼마나 흔들리는지 낸다. 게임은 하나도 바뀌지 않고 시드만 다르다.

한 번 돌린 값의 소수점 자리수는 정밀도에 대해 아무것도 말해 주지 않는다.
**「이 지표를 이 표본으로 판정해도 되는가」에 답하는 유일한 방법이다.**

## 이 하네스가 재지 못하는 것

- **연출 계층 전부.** 대역은 `MonoBehaviour` 를 컴파일하지만 실행하지 않는다.
  `RiskStateView` · `SpinPresenter` · 오디오는 한 프레임도 돌지 않는다.
- **씬에 의존하는 다리들.** `RouletteInteractionBridge` · `AccidentRecorder` 등은
  `Headless.csproj` 에서 **명시적으로 제외**돼 있다. 대역으로 흉내 낼 수는 있지만
  흉내 낸 것을 재면 안 된다. 이들이 필요한 측정은 PlayMode 에서 한다.
- **재미·긴장·손맛.** 봇은 감정을 만들지 않는다.

## 설치

.NET 8 SDK 만 있으면 된다. 유니티를 **띄울** 필요도 라이선스도 없다.

**이 기기에는 따로 설치할 필요가 없다** — 위 「이 기기에는 .NET SDK 가 없다」 절을 본다.
Unity 설치본이 SDK 8.0.318 을 들고 있으므로 그 절대 경로를 쓰면 된다.
다른 기기라면:

```bash
# Windows
winget install Microsoft.DotNet.SDK.8
```

## 위치를 옮기지 않는다

이 폴더는 `Assets/` **밖**에 있다. 유니티는 여기를 컴파일하지 않으므로 대역이
게임 빌드에 섞이지 않는다. `Assets/` 아래로 옮기면 `UnityEngine.Mathf` 가 두 번
정의되어 프로젝트 전체가 컴파일되지 않는다.

### 2026-08-07 추가된 모드

```bash
dotnet run -c Release -- sweepnoload 20000            # 적재 없는 옛 기준선
dotnet run -c Release -- sweepw  8000 out/w.md 85     # 허용 중량 후보
dotnet run -c Release -- sweeps  8000 out/s.md 0.25   # 정산율 후보 (출하 추정치로)
dotnet run -c Release -- solve    2000 out/c.md       # 요구 전력 곡선 역산 (평균)
dotnet run -c Release -- solvemed 2000 out/c.md 1.6   # 중앙값 + 목표 스핀 배율
```

`sweepnoload` 가 있는 이유는 `docs/runtime/HEADLESS_TEST_GAP.md` 에 있다 —
2026-08-07 이전의 스윕은 **적재를 하지 않았고** `FUN_CRITERIA` 의 모든 대역이
그 상태에서 정해졌다. 옛 값을 다시 뽑을 수 없으면 「대역이 왜 움직였나」를 판정할 수 없다.

`solvemed` 가 평균이 아니라 중앙값을 쓰는 이유도 같은 문서에 있다 —
적재를 실으면 산출 분포가 두꺼워져(8층 평균이 표본에 따라 2251→2990→3424)
평균으로 역산한 곡선은 **완주율 8.0%** 를 낸다.
