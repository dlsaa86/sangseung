# 목표

프로토타입 HUD를 전면 개편해, 플레이어가 **화면만 보고** 아래 정보를 즉시 구분할 수 있게 한다.
완료 = 아래 "필수 표시 항목"이 전부 화면에 나타나고 컴파일 에러 0.

완성형 아트는 필요 없다. 텍스트와 색만으로 충분하다. 다만 **읽히지 않으면 실패다.**

---

# 프로젝트 배경

- Unity 6000.5.5f1 / URP. 네임스페이스 `Ascend.Prototype`. asmdef 없음.
- HUD는 TextMeshPro(`TMP_Text`) 한 덩어리로 그린다. 새 Canvas나 프리팹을 만들지 마라.
- 씬에는 이미 `HUDText`와 `HintText` 두 개의 TMP 오브젝트가 있고
  `PrototypeUI`에 연결되어 있다. **이 두 개만 쓴다.**
- **작업 전에 반드시 실제 파일을 읽어라. 시그니처를 추측하지 마라:**
  - `Assets/Prototype_Elevator/Scripts/UI/PrototypeUI.cs` (현재 구현)
  - `Assets/Prototype_Elevator/Scripts/Core/RunController.cs` (노출된 프로퍼티 전부)
  - `Assets/Prototype_Elevator/Scripts/Core/ElevatorState.cs`
  - `Assets/Prototype_Elevator/Scripts/Core/PassengerManager.cs`
  - `Assets/Prototype_Elevator/Scripts/Core/RunOutcome.cs`, `OverchargeOption.cs`
  - `Assets/Prototype_Elevator/Scripts/Roulette/RouletteController.cs`, `TubeController.cs`
  - `Assets/Prototype_Elevator/Scripts/Data/BallDatabase.cs`, `BallDefinition.cs`
  - `Assets/Prototype_Elevator/Scripts/Effects/EffectResolver.cs` (BuildLogText)

**`RunController.cs`를 수정하지 마라.** 필요한 읽기 전용 접근자는 이미 전부 있다:

```
CurrentState, State, Floor, Config, Outcome, Roulette, Passengers, Effects,
LastCombination, Surplus, OverchargeChoice, LastResolutionSuccess, LastShortfall,
MoneyOption, AscendOption
```

`ElevatorState`에는 `Power, Money, Weight, AllowedWeight, IsOverloaded, CurrentTurn,
BankedPower, LastRollSummary, LastGenerationPower, LastEffectLog, AccidentChance,
LastAccidentOccurred, LastAccidentLoss, LastAccidentCause, BoardedCount,
RetriesThisFloor, TotalRetries, HighestFloorReached, TotalMoneyEarned,
TotalAccidents, LastFailureReason` 가 있다.

이 목록으로 부족하면 **그 화면 요소를 생략하고 주석으로 남겨라.**
다른 파일을 수정하면 병렬 작업과 충돌한다.

**이 작업에서 수정해도 되는 파일은 `Assets/Prototype_Elevator/Scripts/UI/PrototypeUI.cs` 하나뿐이다.**

---

# 필수 표시 항목

## 상시 (좌상단 HUD)

1. **현재 층 / 목표 층** — `3 / 10층` 형태
2. **상태와 턴** — `상태: GenerationTurn   턴 2/3`
3. **전력 / 요구 전력** — 달성 시 초록, 미달 시 빨강.
   진행 막대를 아스키로 그려라: `[■■■■■■□□□□] 62%`
4. **돈**
5. **총무게 / 허용 중량** — 과적이면 빨강 + `⚠ 과적 +12.0` 표기
6. **과적 사고 확률** — `사고 확률: 24%`. 0%면 초록, 0 초과면 노랑, 40% 이상이면 빨강.
   **반드시 사고가 나기 전에 보여야 한다.** 이게 T-06의 핵심 요구다.
7. **탑승 승객** — `승객: 기술자, 도박사 (2/6)`. 없으면 `승객: 없음`

## 세 통관 (가장 중요 — 버튼 대응이 즉시 읽혀야 한다)

각 통관을 **한 줄씩 세 줄**로 그린다. 반드시 키 번호를 앞에 붙인다:

```
[1] 통관A  ●낙하중
[2] 통관B  ◆정지 Ball_04 고급 (완벽)
[3] 통관C  ●낙하중
```

- 키 번호 `[1][2][3]`이 통관 A/B/C와 1:1 대응함이 명확해야 한다
- 상태 구분: `낙하중` / `제동중` / `정지`
- 정지한 통관은 **구슬 id + 등급 한글명**을 함께 보여준다
- 등급별 색: 일반=회색, 고급=하늘색, 희귀=보라, 전설=주황
  (TMP `<color=#RRGGBB>` 태그 사용)
- 완벽 정지(`LastStopDistance <= config.perfectStopTolerance`)면 `(완벽)`을 붙이고 초록으로
- 아직 안 멈춘 통관과 멈춘 통관이 **한눈에 구분**되어야 한다

## 상태별 추가 표시

### `FloorArrival` / `PassengerSelection`
**승객 후보를 반드시 보여준다.** 무게와 효과를 함께:
```
승객 후보:
  [1] 기술자      무게 2   발전마다 전력 +2
  [2] 과적 광신도  무게 6   과적 상태에서 전력 ×2
  [0] 아무도 태우지 않음
```
후보를 태웠을 때의 **예상 요구 전력 변화**도 보여주면 좋다: `→ 요구 전력 152 (+4)`

### `GenerationTurn`
- 세 통관 줄 (위 참조)
- 직전 턴의 조합 결과: `조합: Ball_04(고급) Ball_02(일반) Ball_07(희귀) → CommonAdvancedRare`
- **연쇄 효과 처리 과정** — `EffectResolver.BuildLogText()`를 그대로 여러 줄로 출력한다.
  이게 T-03의 "효과 처리 순서가 로그와 UI에서 확인된다" 완료 조건이다.
  너무 길면 마지막 8줄만.

### `PowerResolution`
- 성공/실패
- **사고가 났으면 원인과 손실량을 빨강으로**: `⚠ 과적 12.0 초과 (확률 24%) — 전력 42.0 손실`

### `OverchargeAllocation`
두 선택지를 **나란히** 보여주고 현재 선택을 강조한다:
```
초과 전력 128.0 을 어떻게 쓸까?
  [1] 돈 +128   (초과 전력 128 전량 변환)      ← 선택됨
  [2] 추가 상승 +2층 (전력 120 소비, 8 이월)
```
`RunController.MoneyOption` / `AscendOption`의 `Label`을 쓴다.

### 런 종료 (`Outcome != InProgress`)
화면 중앙에 크게(HUD 텍스트 안에서 `<size=150%>` 등으로) 결과를 띄운다:
```
=== 런 성공 ===
도달 층 10 / 돈 340 / 사고 2회 / 재시도 1회
[R] 다시 시작
```
실패면 `=== 런 실패 ===` + `LastFailureReason`.
종료 화면에서는 통관 줄과 승객 후보를 숨긴다.

## 구슬 확률표 (플레이 전 공개 — 07번 문서 요구)

`[P]` 키로 토글되는 패널. 기본은 **켜짐**(플레이 전에 보여야 하므로).
`BallDatabase`를 읽어 9종 전부를 등급순으로:
```
구슬 등장 확률 (합계 100%)      [P] 토글
  일반  Ball_01 Common Alpha    18%   출력 5
  ...
  전설  Ball_09 Legendary        4%   출력 35
```
- `PrototypeUI`에 `[SerializeField] private BallDatabase _ballDatabase;` 추가
- null이면 이 패널만 조용히 생략 (예외 금지)

## 하단 힌트 (`HintText`)

상태별로 바뀐다. 지금 누를 수 있는 키만 보여준다:
- `PassengerSelection`: `[1][2] 승객 탑승   [0] 건너뛰기   [Space] 진행   [R] 재시작`
- `GenerationTurn` (미정지): `[1][2][3] 통관 정지   [R] 재시작`
- `GenerationTurn` (전부 정지): `[Space] 다음 턴   [R] 재시작`
- `OverchargeAllocation`: `[1] 돈   [2] 추가 상승   [Space] 확정   [R] 재시작`
- 런 종료: `[R] 다시 시작`
- 그 외: `[Space] 진행   [R] 재시작`

---

# 설계 결정 (이대로 따를 것)

- **`PrototypeUI`는 읽기 전용이다.** 게임 상태를 바꾸는 코드를 절대 넣지 마라.
  입력 처리도 하지 마라 — 단, `[P]` 확률표 토글은 UI 로컬 상태이므로 예외로 허용한다.
- 매 프레임 `string` 연결을 남발하지 마라. 기존처럼 `StringBuilder`를 재사용한다.
  `Update()`에서 `new` 할당을 만들지 마라.
- 모든 참조에 null 방어. 어떤 참조가 비어 있어도 HUD가 죽으면 안 된다.
  없는 정보는 그 줄만 생략한다.
- 등급 한글명 매핑은 UI 안의 private static 헬퍼로: 일반/고급/희귀/전설.
- 색상 상수는 파일 상단에 `private const string` 으로 모아 둔다. 흩뿌리지 마라.
- 숫자는 `F0` 또는 `F1`로 자릿수를 고정한다. 값이 흔들려 읽기 힘들면 실패다.

# Unity 제약 (위반 금지)

- `.meta` 직접 생성/수정 금지
- `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/` 수정 금지
- `ProjectSettings/`, `*.unity`, `*.prefab`, `*.asset` 직접 편집 금지 —
  **씬에 새 오브젝트를 만들지 마라.** 기존 `HUDText`/`HintText` 두 개만 쓴다
- `dotnet build` / `csc` 실행 금지

# 완료 조건

- [ ] 위 "필수 표시 항목"이 전부 구현됨
- [ ] `[1][2][3]` ↔ 통관 3개의 대응이 화면에서 명확함
- [ ] 구슬 등급이 색과 한글명으로 구분됨
- [ ] 낙하 중 / 정지 상태가 구분됨
- [ ] 사고 확률이 사고 발생 **이전에** 표시됨
- [ ] 연쇄 효과 로그가 화면에 나타남
- [ ] 초과 전력 두 선택지가 값과 함께 비교 표시됨
- [ ] 성공/실패 화면과 재시작 안내가 표시됨
- [ ] 구슬 9종 확률표가 `[P]`로 토글되고 기본 켜짐
- [ ] `Update()`에서 힙 할당을 만들지 않음 (StringBuilder 재사용)
- [ ] 참조 null이어도 예외 없음
- [ ] C# 문법 오류 없음

# 범위 밖 (건드리지 마라)

- 게임 로직 전부 (`RunController`의 읽기 전용 프로퍼티 추가만 허용)
- `TubeController` 스크롤 수학
- 씬, 프리팹, 카메라, 조명, 머티리얼
- 시뮬레이터 / 에디터 스크립트
