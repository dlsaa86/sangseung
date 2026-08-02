# 씬의 비어 있는 직렬화 참조 — 전수 조사

**측정: 2026-08-02 · `Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity`**
**방법: MonoBehaviour 블록을 훑어 `_필드: {fileID: 0}` 을 컴포넌트별로 집계 →
각 필드가 소유 스크립트에서 `FindAnyObjectByType` 등으로 자가 복구되는지 대조.**

## 왜 이 문서가 있나

같은 날 나는 계약 시너지 줄을 `InstrumentPanelView.ApplyContractPreview` 에 배선했다.
**컴파일도 되고 단정 2건도 통과했는데 화면에는 아무것도 안 나왔다** — 그 메서드는
`_contractLabel` 이 비어서 첫 줄에서 즉시 반환한다.

값 층위 테스트는 「함수가 옳은 문자열을 만드는가」를 증명한다. **「그 문자열이 화면에
닿는가」는 증명하지 못한다.** 뷰에 무언가를 붙이기 전에 **그 필드가 씬에서 비어 있는지
먼저 세야 한다.** 그 목록이 어디에도 없어서 이 문서를 만든다.

## 두 종류를 구분한다

| | 뜻 | 조치 |
|---|---|---|
| **자가 복구** | `Awake` 등에서 `FindAnyObjectByType` 으로 스스로 찾는다 | 비어 있어도 **동작한다.** 배선하면 탐색 비용만 준다 |
| **빈 채로 남음** | 폴백이 없다. 가드에서 반환하거나 그냥 아무 일도 안 한다 | **죽은 경로다.** 여기에 기능을 붙이면 화면에 안 나온다 |

## 빈 채로 남는 것 — 14 필드 / 6 컴포넌트

| 컴포넌트 | 필드 | 무엇이 죽는가 |
|---|---|---|
| `View.PaperTapePrinterView` | `_tape` · `_tapeText` · `_printHead` | **종이 테이프 전체.** `_tapeText == null` 이면 `:213` 에서 반환 — 층 기록이 한 줄도 안 찍힌다. `UP-REC-03`·`UP-REC-04` 가 여기 걸려 있다 |
| `Build.BuildFigureView` | `_carAnchor` · `_lobbyAnchor` · `_gazeCeiling` · `_accessibility` | 승객·화물 형상의 배치 기준과 접근성 |
| `UI.GameHudView` | `_auxGroup` · `_auxRatioText` · `_auxStateText` | 보조 HUD 세 줄 |
| `View.FloorIndicatorView` | `_display` · `_housing` | 층 표시기 |
| `View.InstrumentPanelView` | `_contractLabel` | 계기판의 계약 표시. `UP-DEVICE-06` 이 이미 「죽은 경로」로 기록했다. 계약 문구는 `_plaqueLabels`(살아 있음)로만 뜬다 |

## 자가 복구되는 것 — 4 필드

`AmbientParticleDirector._risk`·`_run` · `RiskStateView._audio` ·
`InstrumentPanelView._presenter` · `TelemetryRecorderBehaviour._riskView`.

**이것들을 「미배선」으로 읽지 말 것.** 비어 있어도 기능은 돈다.
자체 검증 로그의 「배선되지 않았다」 경고와 같은 종류의 오독을 부른다.

## 프로파일 슬롯 — **배선 완료 (2026-08-02 14:36)**

**6/6 꽂았고 씬을 저장했다.** 전수 조사가 24 → **18 필드**로 줄었다.
자체 검증 470 · EditMode 451 · 둘 다 0 FAIL — **배선 전과 같은 수다.**
프로파일 값이 전부 코드 프리셋과 같으므로 밸런스가 한 자리도 안 바뀌었다는 실측이다.

만든 에셋 다섯: `WeightProfile` · `SpinBalanceProfile` · `RiskThresholdProfile` ·
`FloorCurriculumProfile` · `ContractProfile`
(`PresentationProfile.asset` 은 이미 있었고 `SpinPresenter._presentation` 에 꽂기만 했다).
뒤의 둘은 `Reset()` 을 불러 현재 곡선·계약 값을 채웠다 — 편집자가 빈 칸이 아니라
현재 값을 보고 시작하도록.

아래는 그 작업 **이전** 기록이다.

## 프로파일 슬롯 — 6 필드 (설계상 비어 있는 것이 정상)

`RunSessionBehaviour._weightProfile`·`_spinBalanceProfile`·`_floorCurriculum`·
`_contractProfile` · `RiskStateView._thresholdProfile` · `SpinPresenter._presentation`.

전부 2026-08-02 `UP-TECH-09` 작업으로 생긴 슬롯이다. **비어 있으면 코드 프리셋으로
같은 밸런스로 돈다** — 배선 여부는 화면으로 구분되지 않고 `*Source` 프로퍼티가 말한다
(`WeightSource` · `SpinBalanceSource` · `FloorCurriculumSource` · `ContractSource` ·
`ThresholdSource` · `TempoSource`). 각 프로파일 파일 하단 트레일러에 확인법이 있다.

## 쓰는 법

1. **뷰에 기능을 붙이기 전에** 위 「빈 채로 남는 것」에 그 필드가 있는지 본다.
2. 있으면 붙이지 말고, 살아 있는 형제 경로를 찾는다(계약 문구 → `_plaqueLabels`).
3. 씬을 고칠 수 있으면 배선하고 이 표에서 지운다 — `.unity` 는 단일 소유 파일이라
   **씬 오너의 일**이다.

## 다시 세는 법

```bash
PYTHONIOENCODING=utf-8 python tools/dead-scene-wiring.py
```

씬이 바뀌면 수가 달라진다. **날짜 없이 이 표를 인용하지 말 것** (`백로그 §0.35`).
