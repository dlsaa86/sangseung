# 미사용 레거시 코드 — 확정 목록과 삭제 순서 (`UP-TEST-11`)

독립 조사자가 `Scripts/` 146파일 + `Assets/Editor/` 17파일을 전수 조사한 결과다.
방법: ① 모든 `.cs.meta` 의 `guid:` 를 씬·프리팹·`.asset` YAML 에서 역검색
② 씬 YAML 을 파싱해 `m_IsActive`·`m_Enabled`·부모 체인 산출
③ **주석과 문자열을 제거한 소스**로 타입 참조 그래프를 만들어 도달성 계산

> **주석 제거가 결정적이었다.** 주석만 뒤진 1차 결과는 `PlayerSetupValidator`·
> `PrototypeUI`·`ComponentPool`·`RunOutcome` 을 「살아 있음」으로 **오판**했다.

---

## A. 지금 지워도 안전한 것 — 3파일 / 191줄

| 파일 | 줄 | 근거 |
|---|---|---|
| `Scripts/Effects/IEffect.cs` | 13 | 구현체 0개(`: IEffect` 매치는 `IEffectHandler`/`IEffectRandom` 뿐), GUID 씬·에셋 0건. `EffectPipeline` 리플렉션 등록 대상은 `IEffectHandler` 라 안 걸린다. 본문이 스스로 `"stub … planned for T-02+"` 라 적었다 |
| `Scripts/Perf/ComponentPool.cs` | 137 | 참조 0곳. `PerfTests` 도 `ObjectPool<T>` 만 쓴다. ⚠ **아래 주의** |
| `Scripts/Player/InteractablePassenger.cs` | 41 | 선언 둘 다 참조 0곳, GUID 모든 씬·에셋 0건 → **인스턴스 자체가 없다.** `AddComponent` 하는 코드도 없다 |

> ⚠ **`ComponentPool` 은 레거시가 아니라 「아직 안 쓰이는 신규 인프라」다.**
> `UP-TECH-06` 의 유일한 Unity 어댑터다. 컴파일상 삭제는 안전하지만 **보류를 권한다** —
> 지우면 「풀링을 실제로 쓰게 한다」가 처음부터 다시 만들어야 한다.

**`IInteractable` 예외를 적용하지 않은 이유**: `CrosshairInteractor` 는 레이캐스트로
씬에 실재하는 컴포넌트를 찾는다. 인스턴스 0 + 런타임 생성자 0 이면 인터페이스로도 도달 불가다.
같은 역할은 `InteractableBuildCandidate` 가 맡고, 그쪽은 `BuildFigureView.cs:574` 가
`AddComponent` 로 런타임 생성하므로 **C 묶음**이다.

## B. 대체됐으나 남아 있는 것 — 43파일 / 4,380줄 (+부분 편집 5곳, `.asset` 23개)

근거가 소스에 명시된 대체 쌍:

| 옛 | 새 | 근거 |
|---|---|---|
| `Player/PlayerSetupValidator.cs` (101) | `Diagnostics/SceneWiringValidator.cs` | `SceneWiringValidator.cs:66-69` 가 「이전 판본」이라 명시 |
| `View/ElevatorGrayboxView.cs` (404) | `InstrumentPanelView`+`FloorIndicatorView`+`BuildFigureView` | `InstrumentPanelView.cs:17-18` |
| `Core/PassengerManager.cs` (140) | `Build/BuildLoadout.cs`+`BuildItem.cs` | `BuildLoadout.cs:11-12` |
| `UI/PrototypeUI.cs` (445) | `UI/GameHudView.cs`+`DebugPanelView.cs` | 씬 `m_IsActive: 0` |

스택 단위: `Core/` 8 + `Roulette/` 4 + `Effects/` 18 + `Data/` 레거시 6 = **36파일이 상호
참조라 쪼갤 수 없다** (`RunController ↔ ElevatorState`, `RouletteController → TubeController`,
`EffectPipeline ↔ Handlers`, `CombinationConfig ↔ CombinationType`).

**`TubeController` 특이사항** — 씬에서 **활성이고 배선까지 돼 있다.** 죽어 있는 이유는
구동자 `RouletteController` 가 비활성 `GameSystems` 위라 `_stream` 이 영원히 null 이고
`Update()` 가 `TubeController.cs:142` 에서 즉시 빠지기 때문이다.
**PRD §4.2 가 명시 제외한 「통관별 정지 버튼·타이밍 정지」가 한 번의 호출이면 되살아난다.**

## C. 죽어 보이지만 건드리면 안 되는 것 — 52파일 / 약 19,500줄

`[MenuItem]` 32개 · `[RuntimeInitializeOnLoadMethod]` · `.asset` 이 참조하는
`ScriptableObject` · 인터페이스 리플렉션 등록(`Effects/Handlers/` 7파일) ·
테스트 스위트 9파일 · `Scripts/Run/Tests/` 10파일.

특히:

- **`Assets/Editor/PrototypeSelfTest.cs` 는 커밋 게이트다.** `commit-gate.sh:51` 과
  `verify-topdown.ps1:308-320` 이 이 파일이 쓰는 `.claude/state/last-selftest.txt` 를 요구한다
- **`Scripts/Sim/` 5파일 899줄은 레거시가 아니다.** 전부 신 스택에 의존한다.
  백로그의 「`Scripts/Sim/`(일부)」는 **shim 2개만** 가리켜야 한다
- `PlayModeSmokeTest.cs` — `GapAnalysis.md:198` 의 「폐기된 `RunController` 흐름을 검증한다」는
  **낡은 서술**이다. 현재 파일은 신 스택만 쓴다

---

## 삭제 순서 — 이 순서가 아니면 커밋이 막힌다

**🚨 레거시 `.asset` 을 먼저 지우면 안 된다.** `PrototypeSelfTest.cs:38-43` 이

```csharp
if (config == null || balls == null || combo == null)
{ _fail++; _log.AppendLine("  FAIL  자동 검증 중단 — 필수 에셋 누락 …"); return …; }
```

로 **조기 반환**한다. 에셋을 먼저 지우면 신 스택 스위트 10종(188 PASS)이 통째로 실행되지
않고 `1 FAIL` 로 끝나며, `commit-gate.sh` 와 `verify-topdown.ps1` 이 **모든 커밋을 막는다.**

| 웨이브 | 작업 | 검증 |
|---|---|---|
| **0** | `PrototypeSelfTest.cs` 편집 — 에셋 로드 `:32-36`, 조기 반환 `:38-43`, `Test1~9` 호출 `:46-54`, 본문 `:133-379` 제거 | 자체 검증 fail=0 · PASS ≥188 |
| **1** | A 묶음 3파일 + `PlayerSetupValidator.cs` | 컴파일 |
| ↳ | **전제**: `FirstPersonController`·`CrosshairInteractor`·`CrosshairView` 에 `[RequiredReference]` 를 먼저 붙인다 — 안 그러면 검사 손실이다. 현재 이 셋에 속성이 하나도 없다 | |
| **2** | 레거시 에디터 도구 4개. `PlaytestSimRunner` 는 `LoadOne`/`LoadAll` 제공자라 **마지막** | 컴파일 |
| **3** | **씬 편집 — `unity-scene-owner` 단독.** `GameSystems` 서브트리·`PrototypeUI`·`ElevatorGrayboxView`·`Tube_0~2` 와 `.asset` 참조 해제 | 10층 완주 · 콘솔 0 |
| ↳ | ⚠ 에디터 실행 중 `.unity` 를 `Write`/`Edit` 로 고치는 것은 훅이 막는다. `Unity_RunCommand`+`SaveScene` 만. **`PD-13` 승인이 전제** | |
| **4** | shim 제거 — `RunSimulator.cs:20-27`, `BallDrawer.cs:45-60`. **파일 자체는 남긴다** | 밸런스 심 |
| **5** | 씬 빌더의 레거시 블록 — `GrayboxWorldBuilder.cs:362-393`, `HeroSliceSceneBuilder.cs:245-253` | 컴파일 |
| **6** | `PrototypeUI.cs` + `ElevatorGrayboxView.cs` — 활성 경로에서 레거시를 참조하던 마지막 둘 | 컴파일 |
| **7** | `Core/`+`Roulette/`+`Effects/`+`Data/` 레거시 **36파일 한 번에** (쪼갤 수 없다) | 전체 회귀 |
| **8** | `.asset` 23개 + `.meta` | missing-reference 0 |
| **9** | 문서 정정 — **`CLAUDE.md` 가 `Data/PrototypeConfig.asset` 을 「밸런스 값」으로 안내한다.** 실제 밸런스는 `Spin/FloorPlan.cs` 의 `PrototypeCurriculum` 이다 | — |

**되돌릴 수 없는 지점은 웨이브 3(씬)과 8(`.asset`)뿐이다.** 나머지는 전부 git 복구 가능하므로
**3 이전에 로컬 커밋을 하나 남긴다.**

## 규모 정정

| 묶음 | 파일 | 줄 |
|---|---|---|
| A | 3 | 191 |
| B | 43 | 4,380 |
| **삭제 총계** | **46 + 에셋 23** | **4,571** |
| C (보존) | 52 | ~19,500 |

`GapAnalysis.md:205-207` 과 `WINDOWS_SETUP.md:168` 의 「약 5,000줄」은
**`Scripts/Sim/` 899줄을 잘못 포함한 값**이다.
