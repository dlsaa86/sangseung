# NEXT TOPDOWN PLAN — 2026-08-01 Pass 1 Wave A 이후

> 근거는 `CURRENT_IMPLEMENTATION_AUDIT.md`, `NOTION_GAP_MATRIX.md`,
> 그리고 이번 세션에 **Notion MASTER PRD 원문을 직접 다시 읽은 결과**다.
> **이미 VERIFIED 인 66건은 이 계획에 없다.** 재작성 금지 목록은 §5.

---

# 0. 이번 세션에 뒤집힌 것

| 직전 계획이 적은 것 | 원문을 읽고 확인한 것 |
|---|---|
| "PRD §16.2 가 텔레메트리 20개 필드를 지정한다" | **§16.2 「플레이 로그」의 항목은 11개다.** 20은 그 계획 문서의 해석이었다 (`D-20260801-06`) |
| "PRD §8.1 은 `Strain`, 구현은 `Warning`" | 맞다. 그리고 **저장소 동결 스냅샷 `docs/MASTER_PRD.md` §9 도 `Warning` 이다** — 스냅샷을 옮겨 적을 때 생긴 이탈이다 (`D-20260801-05`) |
| `NOT_STARTED` 23건 | **7건.** Wave A 가 16건을 코드로 채웠다 |

`NOT_STARTED` 가 줄어든 16건은 대부분 `SKELETON` 으로 갔다 — **코드와 테스트는 있고
씬에 붙지 않았다.** 이 구분을 흐리면 "구현했다"가 "동작한다"로 읽힌다.

---

# Pass 1 — 잔여 `NOT_STARTED` 7건

## 1-1. `UP-VIS-04` URP 공통 스타일 셰이더 · `UP-VIS-05` 파티클

PRD §12.4·§12.5. Pass 1 에서는 **존재만** 만든다. 품질은 Pass 3.
파티클 5종: 먼지 · 녹가루 · 스파크 · 정화 파편 · 캐스케이드 유입.
§12.5 의 "단계별 최대 동시 파티클 수와 오버드로우 예산"은 `VisualQualityProfile` 이
이미 필드를 갖고 있으므로 **거기서 읽게** 만든다.

## 1-2. `UP-DOC-02` 위험 2단계를 `Strain` 으로 개명

`D-20260801-05`. 실제로 바꿀 곳을 세어 보면 좁다 — `RiskLevel.Warning` 직접 참조 12곳,
표시 문자열 "경고" 1곳, 캡처 이름 `09_risk_warning` 1곳.

### 함께 바꾸는 것 (단계 이름에서 파생된 식별자)

`RiskEvaluator.WarningEnter` · `AudioMixProfile._humVolumeWarning` ·
`_humPitchWarning` · `RiskLevels.DisplayName` 의 "경고" ·
`TenFloorCaptureRig` 의 `09_risk_warning` · `docs/MASTER_PRD.md` §9.

### **절대 바꾸지 않는 것 — 이름이 같지만 다른 것이다**

`RiskProfile.WarningColor` · `WarningPulseRate` · `WarningEmission` 는
**경고등이라는 물리 장치**를 가리킨다. 단계 이름이 아니다.
일괄 치환하면 Collapse 단계의 "Strain등" 같은 말이 생기고,
그때부터 코드를 읽는 사람이 장치와 단계를 구분하지 못한다.
`sed` 로 한 번에 바꾸지 말고 위 목록만 손으로 짚는다.

캡처 이름 변경은 그 각도의 비교 이력을 한 번 끊는다 — Pass 3 에서 어차피 전량
재촬영하므로 **지금 끊는 것이 가장 싸다.**

## 1-3. `UP-DOC-01` Notion §6.1 정화 규칙 개정 — **차단됨**

Notion 원문은 여전히 "위치와 무관하게 기본 정화한다"이고 코드는 인접을 요구한다
(`D-20260801-03`, 사용자 요청). 이번 세션에 Notion 을 고치려 했으나
**쓰기가 권한 계층에서 거부됐다.** 우회하지 않았다.

**사용자 조치가 필요하다.** 둘 중 하나:
- Notion §6.1 을 직접 고친다 (문구는 `docs/MASTER_PRD.md` §6 128~129줄에 있다)
- 또는 이 에이전트에게 Notion 쓰기를 허용한다

## 1-4. `UP-TEST-11` 레거시 정리 · `UP-VIS-07` 루브릭 · `UP-VIS-09` 축소 판독

각각 Pass 4 · Pass 3·4 · Pass 3 소속이다. Pass 1 에서 손대지 않는다.

---

# Pass 2 — 씬에 붙이고 실제로 일어나게 한다

완료 조건: Required 중 `SKELETON`·`VISIBLE` 0건 (현재 **31건**).

## 2-1. Wave B — 씬 배선 (진행 중)

`.asset` 8종 생성 + `AscendRun` 에 컴포넌트 8종 배선 + 월드 사고 기록기 설치.
씬 오너 한 명이 순차로 한다. 이것이 끝나야 `SKELETON` 31건이 실제로 내려간다.

## 2-2. Wave C — 텔레메트리를 §16.2 11항목으로 완성

현재 스핀 레코드는 20필드지만 **§16.2 의 11항목 중 다섯이 빠져 있다**:

| §16.2 항목 | 현재 | 필요한 것 |
|---|---|---|
| 층·스핀·시드 | ✅ | |
| 초기 보드와 **캐스케이드별 보드** | 부분 | 단계별 보드 문자열 |
| 정화·패턴·**발동 순서** | 부분 | 순서를 보존하는 문자열 |
| 획득/요구 전력 | ✅ | |
| 선택 계약 | ✅ | |
| 잔류 저항 | 부분 | 흡수체·증식체 개수 |
| **현재 위험 단계** | ❌ | `RiskLevelChanged` 를 문맥에 누적 |
| 과수확 선택과 결과 | ✅ | |
| **승객·부품 발동** | ❌ | 적재 요약 |
| **프레임 타임과 GC Alloc 샘플** | ❌ | 스핀 경계 샘플 |
| **런 종료 원인** | ❌ | **런 단위 레코드가 따로 필요하다** |

런 단위 레코드는 PRD §10.2 의 출력 항목 9종과 정확히 겹친다 —
`RunSummaryTemplate` 이 이미 그 9종의 서식을 갖고 있으므로 둘을 같은 데이터로 잇는다.

## 2-3. 나머지 Pass 2 항목

| ID | 무엇이 끊겨 있는가 |
|---|---|
| `UP-SPACE-09` | 등을 돌렸을 때 HUD 하나에만 의존. **사운드가 생겼으므로 이제 연결할 수 있다** |
| `UP-TECH-02` | `FindAnyObjectByType` 폴백이 여러 곳에 남아 실행 순서 의존이 있다. 자동 검사가 없다 |
| `UP-VIS-01` | 스타일 락이 그레이박스 상태. 재질·실루엣 언어 없음 |
| `UP-TEST-11` | `TubeController` ×3 이 활성 + 완전 배선(PRD §4.2 제외 설계) · `PD-13` 승인 필요 |

**Pass 2 에서 `UP-TECH-04`·`UP-TECH-05`(성능·GC)는 손대지 않는다.** 측정 방법 자체가
깨져 있어(vSync 상한) Pass 4 에서 프로브를 고친 뒤 다룬다.

---

# Pass 3 — 경험·비주얼·피드백

완료 조건: PRD §15.2 루브릭 통과 + `VISUAL_VERDICT.md` 가 `ACCEPT`.

## 3-1. 수정 백로그 소진 (독립 평가 REJECT 6건)

| ID | 지적 | 주의 |
|---|---|---|
| `UP-FIX-01` | `01_entry` 가 공간의 높이를 보여주지 못한다 | **평가자 최우선** |
| `UP-FIX-02` | 임계점 눈금 숫자 라벨 없음 | **3회 실패. 네 번째 시도 금지** — 배치 결정이 필요하다 |
| `UP-FIX-03` | 과수확 레버가 당김을 형상으로 전달 못함 | 하우징 안 + 카메라 반대 방향 |
| `UP-FIX-04` | 좌측 벽 라벨 거울상 렌더 | |
| `UP-FIX-05` | Critical 과 Collapse 미구분 | **`CollapseSequence` 가 생겼다.** Wave B 배선 후 재촬영 |
| `UP-FIX-06` | 17번 캡처만 해상도·방식 다름 | **월드 프린터로 옮기면 같은 리그로 찍힌다** |

## 3-2. 판독성 마감

`UP-CORE-11·12·13`, `UP-DEVICE-06·09·10`, `UP-VIS-02·03·06·08·09·10`.

## 3-3. 위험 연출 완성

`UP-RISK-03·04` — 조명·진동은 연결돼 있으나 단계 간 감정 차이가 부족하다.
`AccessibilityProfile` 이 생겼으므로 셰이크·섬광을 그쪽에서 읽게 바꾼다.

---

# Pass 4 — 테스트·성능·빌드·회귀

완료 조건: `tools/verify-topdown.ps1` 이 `TOPDOWN_ALL_PASSES_COMPLETE` 출력.

| 순 | 작업 | 비고 |
|---|---|---|
| 1 | **성능 프로브 수정** `UP-TECH-04` | 중앙값이 vSync 상한 8.33ms 에 고정돼 90 FPS 를 **판정할 수 없다.** 상한 없는 측정으로 바꾸고 `TargetHardwareProfile` 을 인용해 재측정 |
| 2 | **GC 원인 분해** `UP-TECH-05` | 9,000~11,000 B/프레임. 목표 0 B |
| 3 | 풀링을 실제로 쓰게 한다 `UP-TECH-06` | 파티클·심볼·사운드. Alloc 감소를 측정으로 보인다 |
| 4 | 렌더 예산·메모리 추세 측정 `UP-TECH-07·08` | 프로브는 있다. 아직 한 번도 돌리지 않았다 |
| 5 | 증거 영상 2종 `UP-TEST-08·09` | 인코더는 왕복 검사로 검증됐다. 런을 그 구간으로 모는 대본이 없다 |
| 6 | 레거시 삭제 또는 격리 `UP-TEST-11` | `PD-13` 승인 후. 자체 검증의 레거시 구슬 단정 19건도 함께 |
| 7 | 폰트 아틀라스 복구 | 글리프 순손실 상태로 미커밋. worktree 가드 때문에 에디터를 끄고 해야 한다 |
| 8 | 전체 회귀 + Windows 빌드 재실행 | 경고 506건 점검 |
| 9 | 독립 시각 평가 재수행 | `VISUAL_VERDICT.md` ACCEPT |
| 10 | Required 129건 전부 `VERIFIED` 전환 | |

---

# 5. 재작성하면 안 되는 시스템 — 이미 VERIFIED

**이 목록의 어떤 것도 "없는 줄 알고" 다시 만들지 않는다.**
증거는 `CURRENT_IMPLEMENTATION_AUDIT.md` §4 에 있다.

| 시스템 | 구현 위치 | 증거 |
|---|---|---|
| 결정론적 자동 3×3 룰렛 | `Scripts/Spin/SpinEngine.cs`, `SpinSeed.cs`, `SpinBoard.cs` | SpinEngineTests 29건 |
| 정상 영혼·흡수체·증식체 | `Scripts/Spin/SymbolKind.cs`, `SpinRuleSet.cs` | 위 스위트 |
| 계약 2종과 4값 동시 변경 | `Scripts/Spin/ResistanceContract.cs` | capture 14 |
| 인접 정화·직선·연결·캐스케이드·하드캡 20 | `Scripts/Spin/SpinEngine.cs` | capture 15 (8단계) |
| 실행 레버 / 잠금식 과수확 레버 | `Scripts/Player/Interactable*.cs` | capture 11/12/13 (판돈 46 실측) |
| 전력·요구 전력·임계점·확정 | `Scripts/Core/FloorMath.cs`, `Spin/PowerThresholds.cs` | RunTests · PlayMode |
| 위험 4단계와 Collapse **판정** | `Scripts/Risk/RiskEvaluator.cs` | RiskEvaluatorTests 11건 · capture 16 |
| 사고 기록기 **데이터** | `Scripts/Run/AccidentRecorder.cs`, `FloorRecord.cs` | PlayMode · capture 17 |
| 승객·부품·무게·과적 | `Scripts/Build/BuildLoadout.cs` | BuildTests 35건 · capture 07/09 |
| 1~10층 커리큘럼과 클램프 | `Scripts/Spin/FloorPlan.cs`, `Core/FloorMath.ClampAscent` | `curriculum_coverage.txt` |
| Windows 빌드 파이프라인 | `Assets/Editor/WindowsBuildTask.cs` | `build_report.txt` Succeeded |
| 캡처·성능·테스트 하네스 | `Assets/CaptureHarness/`, `Editor/PrototypeSelfTest.cs` | manifest 18장 |
| 가중치 0 방어 · 발동 순서 | `Scripts/Spin/Tests/SpinRuleSetTests.cs` | **이번에 추가** |

**주의 — 위 목록에서 "판정"과 "연출"을 구분한다.**
위험 4단계의 *판정*은 VERIFIED 지만 Collapse 의 *연출*은 이번에 코드만 생겼고
씬 배선 전이다. 사고 기록기의 *데이터*는 VERIFIED 지만 *물리적 형태*도 같다.

---

# 부록 A — Wave A 적대적 검증이 남긴 수정 목록

**구현자와 분리된 검증자가 자기 모듈을 공격해 찾은 것들이다.** 전부 실재가 확인됐다
(`CONFIRMED` — 파일과 줄 번호를 짚었다). Wave C 에서 소진한다.

## A-1. 아무것도 검사하지 않는 단정 4건 — `TelemetryTests`

| 위치 | 왜 항상 통과하는가 |
|---|---|
| `:259` `IsNullOrEmpty(r.Contract)` | `ResolveContractLabel` 이 마지막에 무조건 `ResistanceContract.None.Label`("계약 없음")을 돌려준다. 빈 문자열이 될 수 없다 |
| `:260` `IsNullOrEmpty(r.BestPattern)` | `PatternKind.None.ToString()` 이 `"None"` 이다. 역시 빌 수 없다 |
| `TestPowerLedger` 의 추가 스핀 분기 | 그 테스트의 `Drive()` 가 `PushYourLuck` 을 부르지 않아 **도달하지 않는다** |
| `TestSeedActuallyUsed` | `Count` 만 비교한다. 길이가 같고 내용이 다른 경우를 잡지 못한다 |

**빈 테스트는 없는 테스트보다 나쁘다** — 통과 숫자를 올려 놓고 아무것도 지키지 않는다.
이 저장소는 `0503a3f` 에서 이미 같은 이유로 독립 감사가 Gate 판정을 뒤집은 적이 있다.

## A-2. 문자열 계약이 무보호다

`TelemetryRecorder.cs:115` 가 `e.Text == "과적"` 으로, 그리고 `isExtraSpin` 이
`SpinStarted` 의 `Text == "추가 스핀"` 으로 판정한다.
**`FloorSession` 이 그 문자열을 바꿔도 어떤 테스트도 빨개지지 않는다.**

고칠 방향: 문자열 대신 `GameEvent.IntValue` 에 플래그를 싣는다. 사건 계약을 넓히는
쪽이 문자열 비교보다 낫다 — 지금은 넓히지 않는 쪽을 골랐고, 그 선택이 이 구멍을 만들었다.

## A-3. 도달 불가능한 코드

`TelemetryRecorder` 의 `ContractSelected` 구독과 `_contractLabel` 폴백은 죽은 코드다.
`resolution.Contract.Label` 이 항상 먼저 반환되기 때문이다. 지우거나, 도달하는 경로를 만든다.

## A-4. 주석이 구현보다 넓게 말한다

`SpinTelemetryRecord.cs:10-15` 가 PRD §10 아홉 항목을 열거하며 "종료 원인"까지
포함해 "전부 레버를 한 번 당긴 순간에 값이 정해진다"고 적는다. **종료 원인 필드는 없다.**
런 종료는 스핀 속성이 아니므로 런 단위 레코드로 분리해야 한다 (`D-20260801-06`).

## A-5. 그 밖에 기록해 둘 것

- `TelemetryFileSink` 는 8건마다 민다. 프로세스가 강제 종료되면 **마지막 최대 7건이 사라진다.**
  조사하고 싶은 런은 대개 끝까지 가지 못한 런이므로 이 손실은 가장 아픈 곳에서 난다.
- 파일 sink 테스트 2건이 `Path.GetTempPath()` 아래에 실제로 쓴다. 임시 폴더 쓰기가
  막힌 환경에서 FAIL 로 잡힌다 — **조용히 스킵하지 않는 쪽을 골랐다.**
- 어떤 테스트도 `run.Events.ErrorCount == 0` 을 확인하지 않는다. 버스가 구독자 예외를
  삼키므로 기록기가 던지면 조용히 기록만 빠진다.

## A-6. 버스가 발행마다 배열을 할당한다 (내가 만든 것)

`GameEventBus.Publish` 는 구독자 예외를 격리하려고 `Published.GetInvocationList()` 를
부르는데, **이 호출은 매번 `Delegate[]` 를 새로 할당한다.**

깊은 캐스케이드 한 번이 40개 남짓의 사건을 낸다(열 공개 3 + 단계마다 영혼·정화·단계 +
잔류 + 임계점 + 종합). 스핀당 40회 할당이다. 매 프레임이 아니므로 PRD §13.2 의
"워밍업 후 매 프레임 0 B" 를 직접 깨지는 않지만, **스핀은 이 게임에서 가장 화려한
순간이고 그 순간에 GC 를 부르는 것은 정확히 피해야 할 배치다.**

고칠 방향: 구독자를 `List<Action<GameEvent>>` 로 직접 들고, 구독이 바뀔 때만
스냅샷을 갱신한다. 예외 격리는 그대로 유지된다.

**측정 없이 고치지 않는다** — `UP-TECH-05` 의 GC 원인 분해에서 이 항목이 실제로
얼마를 차지하는지 먼저 잰다. 지금은 원인 후보로 기록만 한다.
