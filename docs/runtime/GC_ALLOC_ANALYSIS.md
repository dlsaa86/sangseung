# GC Alloc 분해 — 2026-08-01

`UP-TECH-05`(워밍업 후 매 프레임 0 B)의 「9,000~11,000 B/프레임」이 무엇이었는지
분해한 기록이다. **결론: 그 숫자의 대부분은 잴 대상이 아니었다.**

이것은 `UP-TECH-08` 과 **같은 종류의 오판**이다. 그때는 `GC.GetTotalMemory(false)` 가
「붙잡힘」과 「아직 안 치움」을 구분하지 못해 미수거 쓰레기를 누수로 읽었다.
이번에는 측정 창 안에 하네스 자신의 할당이 들어가 있었다.

---

## 1. 바닥이 8,805 B/프레임이다

`Logs/heroslice_perf.txt` 에 **같은 기기·같은 카운터**(`ProfilerCategory.Memory /
"GC Allocated In Frame"`)로 잰 대조군이 이미 있었다.

```
[대조군 — 게임 코드 전부 끄고 60초] 7095프레임
  GC Alloc 중앙 8805 B / 95% 9616 B / 최대 402053 B / 평균 9273 B
```

`Logs/loaded_critical_perf.txt` 의 네 조건을 이 바닥과 대조하면:

| 조건 | 평균 B/프레임 | 대조군 평균(9,273) 대비 |
|---|---|---|
| 무적재·Stable p1 | 10,760 | +1,487 |
| 최대적재·Critical p1 | 9,128 | **−145** |
| 최대적재·Critical p2 | 9,127 | **−146** |
| 무적재·Stable p2 | 11,015 | +1,742 |

**네 조건 중 둘은 게임 코드를 끈 바닥보다 낮다.** 이 표를 만든
`LoadedCriticalPerfProbe` 에는 대조군 arm 이 없어서 그 사실을 볼 수 없었다.

## 2. 중앙값이 구성과 무관하게 같다

`heroslice_perf.txt` 의 일곱 구성 중 다섯이 중앙 **8,805 B**, 95% **9,616 B** 로
바이트까지 동일하다. 적재 0↔6, Stable↔Critical, 유휴↔재생 중, HUD 켬↔끔 —
어느 것도 이 값을 움직이지 못한다. 게임 상태에 반응하지 않는 값이다.

## 3. 보고서의 최대 할당 두 개는 하네스 자신의 표본 버퍼다

`HeroSlicePerfProbe.cs:357-358` — **recorder 를 켠 다음 줄에서** 버퍼를 만든다.

```csharp
var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
var frames = new List<FrameSample>(4096);   // ← 측정 창 안이다
```

`FrameSample` = `int`4 + `float`4 + `long`8 + `int`×3 12 + `int`4 + `int`4 + `int`4 +
`bool`1 = 41 B → `long` 정렬로 **48 B**.

| 보고된 값 | 계산 | 차이 |
|---|---|---|
| 「스핀·캐스케이드 재생 중」 최대 **205,437 B** | 4096 × 48 + 8,805 = 205,413 | **+24 B** |
| 「대조군」 최대 **402,053 B** | 8192 × 48 + 8,805 = 402,021 | **+32 B** |

차이 24 B·32 B 는 `List` 객체 헤더와 배열 헤더다. **저장소 전체 성능 보고서에서
가장 큰 두 개의 GC 할당은 측정 도구가 자기 자신을 잰 것이다.**

## 4. 그래서 실제로 남는 것

**1,638 B/프레임** — `heroslice_perf.txt` #1(10,443) − #3(8,805).
범위는 `HeroSlicePerfProbe.cs:100-113` 이 한 덩어리로 끄는 8개다:
`RiskStateView` · `InstrumentPanelView` · `PurifyMarkerView` · `SpinBoardView` ·
`BuildFigureView` · `CrosshairInteractor` · `RouletteInteractionBridge` · `SpinPresenter`.

**어느 것인지는 아직 모른다.** 하네스가 8개를 개별로 끄지 않았다.
그리고 이 값은 `ContractSelection` 단계에서만 나오고 `Decision` 단계에서는 0 이다.

### 다만 1,638 B 는 「게임 코드 전체」가 아니다 — 이 문서 초판이 그렇게 적었고 틀렸다

「대조군 — 게임 코드 전부 끔」은 **전부가 아니다.** `HeroSlicePerfProbe.cs:588-599` 는
`Disable<>()` 를 **정확히 12번** 부르는 손 열거이고, 씬에서 계속 도는 프레임 콜백이
남는다 — `AudioDirector` · `PaperTapePrinterView` · `FloorIndicatorView` ·
`PassengerReactionView` · `TubeController`×3 · `OverharvestApproachBridge` ·
`RiskEventBridge` · `OverharvestUnlockEffect` · `InteractableOverharvestLever` ·
`TelemetryRecorderBehaviour` · `RenderBudgetProbe` · `MemoryTrendProbe` · `RunSessionBehaviour`.

따라서 8,805 B 바닥 **안에도 게임 코드가 들어 있다.** 정확히 말하면:

- **1,638 B** = 저 **8개 컴포넌트의 비용**이지 게임 코드 전체 비용이 아니다
- 나머지 게임 코드(위 14개)의 비용은 **바닥에 섞여 있고 한 번도 분리된 적이 없다**
- 진짜 엔진 바닥은 8,805 B 보다 **낮다** — 얼마나 낮은지는 아무도 모른다

즉 목표 대비 격차는 **「10 KB」도 「1.6 KB」도 아니다.** 1.6 KB 는 확정된 하한이고,
상한은 미측정이다. **「1.6 KB 만 고치면 된다」로 읽으면 안 된다.**

---

## 5. 측정 자체의 결함 (고쳐야 할 것)

| # | 결함 | 위치 |
|---|---|---|
| F-1 | 표본 버퍼를 recorder 시작 **뒤에** 할당 | `HeroSlicePerfProbe.cs:358, 218-220, 503-505` |
| F-2 | 「게임 코드 전부 끔」이 손 열거 12개뿐 — 14개가 계속 돌았다 | `HeroSlicePerfProbe.cs:588-599` |
| F-3 | 할당 이분 탐색에 순서 반전 arm 이 없다 | `heroslice_perf.txt` §2b·2c |
| F-4 | A/B arm 이 이름 붙은 변수 말고 **층 단계**까지 다르다 | `LoadedCriticalPerfProbe.cs:94-134` |
| F-5 | 「스핀당 0 B」는 수거가 일어난 것이지 할당이 없는 게 아니다 | `heroslice_perf.txt:14` |
| F-7 | 816×714 에디터 Play 측정 — §13 기준은 1920×1080, 빌드에서 잰 적 없다 | 두 파일 전부 |

**F-2 는 `BuildFigureView` 누락 때와 같은 맹점이다** — 목록이 손으로 유지되는 열거인
한 계속 재발한다. `FindObjectsByType<MonoBehaviour>` 전수 비활성으로 바꿔야 한다.

**F-4 가 특히 중요하다.** heroslice 가 이 교락을 폭로한다 — 적재 0·Critical 강제 없이도
같은 두 값이 나온다(유휴 평균 10,790 ≈ A arm 10,760 / 스핀 이후 9,126 ≈ B arm 9,128).
1.6 KB 차이를 만드는 것은 **적재도 위험도도 아니고 층 단계**다. 게다가 부호가 반대다 —
오브젝트 6개를 더 싣고 Critical 로 만들면 할당이 **줄어든다.**

## 6. 판정

**측정된 값만으로 위반을 확정할 수 없다.** 동시에 **위반이 아니라고도 말할 수 없다** —
1,638 B/프레임은 두 파일에서 독립적으로 재현됐고 목표는 0 B다.

`UP-TECH-05` 를 SKELETON 으로 유지한다. 다만 「남은 문제」의 숫자를 정정한다 —
격차는 10 KB 가 아니라 1.6 KB 이고, **원인 분해가 없는 것이 아니라 이미 있는 분해를
`loaded_critical_perf.txt` 가 인용하지 않은 것**이다.

## 7. 다음에 할 일 (순서대로)

1. **측정을 고친다** — 버퍼를 recorder 앞으로, 대조군 arm 추가, A/B arm 의 층 단계 정렬,
   「전부 끔」을 전수 비활성으로. 고치기 전의 수치로는 아무것도 판정하지 않는다
2. **1,638 B 의 주인을 8개 중에서 가른다** — 하나씩만 끄는 8회 측정,
   전부 켠 arm 을 처음과 마지막에 두 번(A→b1…b8→A)
3. **빌드에서 1920×1080 으로 잰다** — 에디터 루프가 빠지면 바닥이 얼마나 내려가는지가
   곧 답이다. `Builds/Windows/Upandup_DDD.exe` 는 이미 있다

`ObjectPool`/`ComponentPool`(`UP-TECH-06`)이 답인 지점은 **하나뿐**이다 —
`CrosshairInteractor.ApplyHighlight`(렌더러당 `MaterialPropertyBlock` 2개 + `RendererState`).
나머지 후보는 전부 문자열이라 풀이 답이 아니다. **풀을 넣는다고 0 B 가 되지 않는다.**
