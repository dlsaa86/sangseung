# 죽은 구현 감사 — 2026-08-01

독립 감사자(구현자와 분리)가 저장소를 직접 열어 55건(`SKELETON` 22 + `CONNECTED` 33)의
「지금 무엇이 없어서 VERIFIED 가 아닌가」를 확인한 결과다. **백로그의 「남은 문제」 서술을
믿지 않고 소스·씬 YAML·`.meta` GUID·로그 파일을 직접 대조했다.**

---

## 0. 이 문서가 따로 있는 이유

이 저장소가 실제로 겪은 실패는 언제나 같은 모양이다 — **코드는 있는데 아무도 부르지 않는다.**
승객 시스템의 무게 계산이 비활성 `GameObject` 위의 `PassengerManager` 에만 있어서
실제로 도는 런에서는 무게가 영원히 0이었던 것이 그 원형이다.

그 실패는 컴파일 오류를 내지 않는다. 테스트도 통과한다(그 코드를 직접 부르니까).
**증거 파일도 생긴다.** 그래서 「구현했다」와 「게임에서 일어난다」가 갈라져도 아무도 모른다.

아래는 지금 저장소에 남아 있는 같은 모양의 것들이다.

---

## 1. 프로파일 `.asset` 7종 — 만들어졌고 아무 데도 흐르지 않는다

`Data/Profiles/` 에 `.asset` 8개가 실재한다. 백로그는 이 항목들의 문제를
「`.asset` 이 없다」로 적고 있는데 **그건 이미 해결됐고 진짜 문제는 소비처다.**

`Scripts/Data/Profiles/`·`Tests/`·`Assets/Editor/` 를 제외한 런타임 소비처를 센 결과:

| 프로파일 | 런타임 소비처 |
|---|---|
| `TargetHardwareProfile` | **없음** |
| `OverharvestProfile` | **없음** |
| `DangerFeedbackProfile` | **없음** |
| `AudioMixProfile` | **없음** |
| `AccessibilityProfile` | **없음** |
| `RunSummaryTemplate` | **없음** |
| `VisualQualityProfile` | `Perf/RenderBudgetProbe.cs` — 필드만 있고 씬이 안 물렸다 |
| `PassengerReactionSet` | `Npc/` 4파일 ✅ |

교차 확인:

- `RiskStateView` 는 `DangerFeedbackProfile` 을 모른다. `RiskProfile.Preset(RiskIntensity)`
  라는 **코드 상수 표**를 읽는다.
- `AudioDirector` 는 `AudioMixProfile` 을 모른다. `[SerializeField] _masterVolume` 등을 직접 갖는다.
- `Logs/render_budget.txt` 마지막 줄이 스스로 적는다 — `예산이 주입되지 않았다. 값만 기록했고 판정은 하지 않았다.`
- 씬 YAML 의 GUID 를 `.meta` 로 역매핑하면 씬이 참조하는 프로파일은 `PassengerReactionSet.asset` 하나뿐이다.

**왜 이것이 그냥 미완료보다 나쁜가**: PRD §14.2 「승인 대기 항목을 교체 가능한 프리셋으로
유지」가 이 상태에서는 성립하지 않는다. 값을 바꿔도 화면에서 아무 일도 일어나지 않기 때문이다.
그리고 서술이 낡은 채로 두면 다음 세션이 `.asset` 을 **다시 만들고** 완료로 표시한다.

여는 항목: `UP-PLAT-04` `UP-POWER-07` `UP-RISK-07` `UP-RISK-08` `UP-AUD-05` `UP-TECH-09` `UP-TECH-07`
(부분: `UP-PLAT-05` 예산 쪽만 · `UP-REC-02` 요약 템플릿)

### 주입할 때의 함정 두 개 (미리 확인해 둔 것)

**① 채널 열거가 두 벌이고 값이 다르다.** 이름이 같아서 캐스트가 컴파일된다.

| 멤버 | `Audio/AudioDirector.cs` 의 `AudioCueChannel` | `Data/Profiles/AudioMixProfile.cs` 의 `AudioChannel` |
|---|---|---|
| `Machine` | 0 | 1 |
| `Event` | 1 | **2** |
| `Passenger` | 2 | **3** |
| `Warning` | 3 | **4** |

`(AudioChannel)cueChannel` 로 넘기면 전 채널이 한 칸씩 밀린 볼륨을 받는다.
컴파일도 되고 소리도 나므로 **아무도 눈치채지 못한다.** 명시적 매핑 함수를 쓴다.

**② 모든 프로파일에 `SnapshotOrDefault(profile, caller)` 가 이미 있다.**
설계는 주입을 예상하고 만들어졌고 마지막 한 걸음만 빠졌다. 새로 만들지 말고 그것을 부른다.
`AccessibilityProfile` 은 `ScaleShake`·`AllowFlickerAt`·`ClampFlickerRate` 까지 갖고 있어
`RiskStateView` 쪽 접근성 분리(`UP-RISK-08`)가 계산식을 새로 짤 필요가 없다.

## 2. 승객이 한 번도 탄 적이 없다 — 14건이 관측 불가

여덟 자동 런 전체의 적재 목록이 전부 `PRT_*`(부품)다. `BuildCatalog` 의 승객 6종은
**단 한 번도 탑승한 적이 없다.** 원인은 하네스가 후보를 번호 순으로만 집는 것이었다.

그래서 `[승객반응] 시작 0` 이 나오는데, 이것은 **배선 고장이 아니라 칸이 비어 있어서**다.
`PassengerReactionView → Router → Director → BuildFigureView.SetReaction` 사슬은 코드에
완결돼 있고 `GameEventBus` 도 `FloorSession` 에서 실제로 발행된다.

동시에 계측 프로브가 `Stable` 을 벗어나지 않고(`[위험사건] 마지막 알림 Stable`),
10층 로그는 **위험 단계를 적는 줄이 아예 없다.**

여는 항목: `UP-NPC-01~05` `UP-AUD-01~04` `UP-RISK-03` `UP-RISK-05` `UP-RISK-06` `UP-POWER-06` `UP-SPACE-06`

> **진행 상황**: `Scripts/Build/BuildLoadPolicy.cs` 와 `TenFloorAutoPilot` 의 `BoardStyle.MaxLoad`
> 로 적재 쪽을 열었다(`A-20260801-06`). 위험 단계 쪽은 아직이다.

## 3. 캡처가 화면 HUD 를 담지 않는다 — 판정 불가능한 증거 4건

`Captures/TenFloor/manifest.txt` 가 스스로 적고 있다.

> 주의: 전용 카메라의 RenderTexture 렌더다. 화면 UGUI HUD는 포함되지 않는다.

씬의 캔버스 6개가 전부 `ScreenSpaceOverlay` 다. 그런데 아래는 **HUD·프롬프트가 증거의
대상인데** 그 캡처를 증거로 걸고 있다.

- `UP-CORE-13` 「한 화면에 모든 숫자를 띄우지 않는다」 — 그 그림에는 HUD 가 없다
- `UP-SPACE-03` 「조준 대상 하이라이트와 행동 프롬프트」 — 둘 다 오버레이
- `UP-SPACE-09` — 증거 「없음」
- `UP-REC-05` / `UP-FIX-06` — 17번만 화면 캡처(816×714)인 이유가 정확히 이것이다

## 4. 「연출 잠금」을 잠긴 순간에 관측한 적이 없다 — 4건

`Logs/tenfloor_playmode.txt` 395건 중 `연출잠금=True` 는 **0회**다.
하네스가 `WaitWhileLocked` 로 잠금이 풀린 **뒤에** 상태를 찍기 때문이다.

즉 `UP-RUN-08`(중복 입력)·`UP-SPACE-08`(판정 중 이동)의 증거로 걸린 그 필드는
잠긴 순간을 한 번도 보지 않았다. 스핀 중 레버를 두 번 누르는 단정도,
잠금 중 이동을 시도하는 단정도 없다.

여는 항목: `UP-RUN-08` `UP-SPACE-08` `UP-CORE-11` `UP-CORE-12`

## 5. 성능 측정이 기준 해상도가 아니다 — 4건

| 산출물 | 해상도 | 중앙 프레임타임 |
|---|---|---|
| `Logs/render_budget.txt` | **816×714** | 8.45ms |
| `Logs/loaded_critical_perf.txt` | **816×714** | 네 조건 전부 정확히 **8.33ms** |

`TECH_SPEC.md` §13 의 기준은 1920×1080 이다. 지금은 화소 수 기준 약 28% 다.
게다가 `vSync 0` 이라 적힌 채 중앙값이 8.33ms 에 못 박혀 있다 — 상한에 걸린 값으로는
90 FPS 목표를 판정할 수 없다.

`UP-TECH-06` 의 풀(`ObjectPool`/`ComponentPool`)은 완성돼 있으나 **파티클·심볼·사운드
어느 것도 쓰지 않는다** — 소비처 0. 1절과 같은 모양이다.

## 6. 증거 영상이 0개다 — 2건

저장소 전체에 `.gif` 가 **0개**이고 `SequenceRecorder` 를 호출하는 코드가 자기 파일 말고
**0곳**이다. 인코더 왕복 검증(Pillow 대조)은 됐다. 만들어졌고 아무도 부르지 않는다.

여는 항목: `UP-TEST-08` `UP-TEST-09`

---

## 7. 「남은 문제: 없음」이라고 적힌 죽은 구현

가장 위험한 형태다. 미충족이라서가 아니라 **아무도 다시 안 볼 형태로 적혀 있어서**다.

**`UP-TECH-03`** — `Scripts/Player/PlayerSetupValidator.cs` 는 `#if UNITY_EDITOR` 안의
`static` 클래스에 `MenuItem` 하나다. **호출자 0곳**이고 빌드에 들어가지 않는다.
그런데 상태는 `CONNECTED`, 남은 문제는 「없음」이다. 증거로 걸린
`tenfloor_playmode.txt` 「씬 배선」 줄은 `TenFloorAutoPilot` **자신이** 찍는 것이지
이 코드의 산물이 아니다.

**출처 정정 —** 이 항목이 백로그와 이 문서에서 「PRD §13.5」로 인용돼 왔으나
동결된 `docs/MASTER_PRD.md` §13.5 는 **「증거 산출물」**(완료 보고에 무엇을 넣는가)이고
필수 참조와 무관하다. 함께 적힌 「N08 §3.3」은 `NOTION_GAP_MATRIX.md` 에 없어
로컬에서 확인되지 않는다. 로컬에서 검증 가능한 실제 출처는
**`docs/TECH_SPEC.md:35`** 다 — 「null 상태에서 조용히 실패하지 않는다.
개발 빌드와 에디터에서는 원인과 경로를 명확히 출력한다.」
**틀린 인용을 내가 이 문서로 옮겨 적었다** — 원문을 열지 않고 백로그의 출처 표기를
그대로 베낀 결과다. 구현은 위 TECH_SPEC 문장을 기준으로 했다.

같은 방식으로 다시 확인해야 할 것들:

- **`UP-POWER-02`** — 재확인 완료(2026-08-01). **8구간 중 죽은 것은 정확히 하나다** —
  앞서 이 줄이 항목 전체를 의심하게 적혀 있었으나, 전수 확인하면 일곱은 살아 있다.
  - `Crash` → `RunEnded` → `FloorResult.CanContinueRun` — **산다**
  - `Jettison` → `RequiresJettison` → `RunSession.cs:283`, `FloorRecord.cs:115` — **산다**
  - `MultiFloor`·`Overharvest`·`Runaway` → `AscendResult.cs:72` 층 보너스 — **산다**
  - `Normal`·`Rewarded` → 기본 보상 경로 — **산다**
  - `Damaged` → `AscendResult.DeviceDamaged` → `FloorResult.DeviceDamaged` → **읽는 곳 0곳.**
    대입 2곳(`AscendResult.cs:62`, `FloorResult.cs:40`)과 선언 2곳이 전부다

  즉 요구 전력의 **90~100% 구간에 들어가면 아무 일도 일어나지 않는다.** 게임은
  그 구간을 `Normal` 과 구별해 분류해 놓고 그 분류를 쓰지 않는다.

  **더 중요한 것: 이 구간이 무엇을 해야 하는지 동결 PRD에 없다.** `MASTER_PRD.md`
  전문에서 「손상」·「Damaged」·「파손」이 **0건**이다. 구간 이름과 경계값
  (`DamagedCeiling = 1.00f`)은 코드에만 있다. 따라서 이것은 「구현이 빠졌다」가 아니라
  **「사양이 없는 채로 분류만 만들어졌다」**이고, 효과를 지금 정하면 그것은 구현이
  아니라 **설계 결정**이다. `PENDING_DECISIONS.md` 로 올린다 — 다만 되돌릴 수 있으므로
  기본 프리셋(효과 없음 = 현 상태)으로 계속 진행하고 막지 않는다
- **`UP-TEST-06`** — 재확인 완료(2026-08-01). **주장이 맞다.**
  `Assets/Prototype_Elevator/Scripts/UI/DebugPanelView.cs` 전문에
  `UNITY_EDITOR`·`DEVELOPMENT_BUILD`·`Debug.isDebugBuild` 가 **0건**이다.
  릴리스 빌드에서 F1 이 그대로 디버그 패널을 연다 — 시드 재시작(R), 시드 입력(T),
  스핀 로그(L)까지 전부 노출된다. 고치는 비용은 작다(조건부 컴파일 한 겹)
- **`UP-REC-03`** — 반대 방향. 실제로는 충족으로 보인다(`GameHudView` 와 `PaperTapePrinterView`
  가 둘 다 `AccidentRecorder.FloorRecord` 를 읽는다). 판정만 남았다

---

## 8. 백로그 자체가 틀린 것

| 위치 | 적힌 것 | 실제 |
|---|---|---|
| §6 통계표 | VERIFIED 66 / CONNECTED 25 / SKELETON 31 | **67 / 33 / 22 / 7** (헤더 직접 집계) |
| `UP-VIS-06` 남은 문제 | 「§15.1 9종과 1:1 대조표가 없다」 | `NOTION_GAP_MATRIX.md` §6 에 있다 |
| `UP-REC-04` 검증 | 「`EyeLevelCapture` 09번 각도」 | `Captures/eyelevel/` 에 00~08 만 있다 |
| `UP-TECH-07` 남은 문제 | 드로우콜 최악 900 / 프레임 147.28ms | 최신값 464 / 153.81ms |

---

## 9. 감사자가 확인하지 못한 것 (추정으로 남긴다)

- `UP-AUD-02` 10종이 서로 다른 소리로 들리는가 — `AudioDirector` 가 큐 **종류별** 카운터를
  갖지 않아 로그로 셀 수 없다. 청취는 사람이 필요하다(PRD §13)
- `UP-VIS-*` 의 시각 판정 — 감사자는 캡처를 열어 채점하지 않았다. `VISUAL_VERDICT.md` 의
  `REJECT` 를 현재 판정으로 인용했을 뿐이다
- 1절을 실행하면 정확히 몇 건이 열리는가 — `UP-PLAT-05`·`UP-REC-02` 는 부분이라 7~9건 범위
