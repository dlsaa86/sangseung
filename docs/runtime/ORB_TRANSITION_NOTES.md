# SpinBoardView — 구슬 등장/퇴장 전환

## 무엇을 바꿨는가

원인: `SetCell()` 이 `child.gameObject.SetActive(on)` 으로 **즉시** 켜고 끄고 있었다.
그래서 구슬이 팝인/팝아웃했다.

바꾼 구조:

- `SymbolSlot` 구조체에 `TargetOn`(목표 표시 상태)과 `Progress`(0~1 전환 진행도)를 추가했다.
- `SetCell()` 은 이제 **목표만 정한다.**
  - 켜질 때: 즉시 `SetActive(true)` 하고 `Progress=0` 으로 되돌린다 — 실제로 자라나는 건
    다음 프레임부터 `ApplyTransitions` 가 한다.
  - 꺼질 때: `SetActive` 를 **여기서 부르지 않는다.** `Progress` 도 안 건드린다. 지금 값에서
    `ApplyTransitions` 가 0까지 줄이고, 0 에 닿는 순간 `SetActive(false)` 를 부른다.
- 새 메서드 `ApplyTransitions()` (매 프레임, `Update`) — `Time.deltaTime / _transitionDuration`
  만큼 `Progress` 를 `Mathf.MoveTowards` 로 목표(0 또는 1)에 수렴시킨다. `_transitionDuration`
  은 `[SerializeField, Min(0.01f)]`, 기본값 `0.12`초.
- `ApplyHighlights()` 의 스케일 대입을 `BaseScale * highlightScale` → `BaseScale * highlightScale
  * Progress` 로 바꿨다. 자세한 결합 방식은 아래 "하이라이트와 어떻게 합쳤나" 참조.

전환 곡선은 **선형**이다(`Progress` 를 그대로 스케일 배율로 쓴다). 이징이 필요하면 다음
폴리싱 패스에서 `Mathf.SmoothStep(0f, 1f, slot.Progress)` 정도로 얹으면 되고, 이번엔
지시받은 범위(0.12초 안팎, 그로우/슈링크)를 넘지 않으려고 넣지 않았다.

## 에디터 캡처 경로 — 왜 `Application.isPlaying` 분기가 있는가

`ShowBoard()` 의 기존 주석이 이미 못박아 뒀다: "에디터 캡처와 테스트가 Play 모드 없이
결과판을 세울 수 있어야 해서 공개한다 — Update는 에디트 모드에서 돌지 않는다." 실제로
`Assets/Editor/EyeLevelCapture.cs` 가 `EditorSceneManager.OpenScene` 으로 씬을 연 뒤
Play 모드 진입 없이 `view.ShowBoard(...)` 를 직접 부르고 바로 스크린샷을 찍는다
("Play 모드를 끌어들이면... 실패 지점만 늘어난다"는 게 그 파일 자신의 이유다).

`SpinBoardView` 는 `[ExecuteAlways]` 가 아니므로 이 경로에서는:

1. **`Awake()` 자체가 안 불린다** → `_slots` 가 null. → `SetCell` 맨 위에 `EnsureSlots()`
   를 추가해 필요하면 그 자리에서 `CacheSlots()` 를 지연 실행하도록 했다.
2. **`Update()` 가 영원히 안 돈다** → 전환을 "다음 프레임들에 걸쳐" 진행할 방법이 없다.
   그래서 `SetCell` 은 `Application.isPlaying == false` 면 전환을 건너뛰고 그 자리에서
   최종 모습(`Progress`, `SetActive`, `localScale` 전부)으로 즉시 스냅한다 —
   **기존 동작과 동일한 결과.**

이 분기가 없으면 에디터 캡처가 "구슬이 전부 스케일 0(투명)인 빈 판"을 찍게 된다 —
사용자가 보는 버그를 고치려다 채점용 도구를 하나 깨뜨리는 셈이라 반드시 넣었다.

## 0 B/프레임을 어떻게 지켰는가

- **`GetPropertyBlock` 을 안 부른다** — 기존 규칙 그대로 유지. `ApplyTransitions` 는
  `localScale`/`SetPropertyBlock` 어느 쪽도 만지지 않는다(아래 참고). `ApplyHighlights`
  의 `SetPropertyBlock` 호출부는 원래 코드에서 한 글자도 안 바꿨다.
- **새 루프는 전부 배열 인덱스 `for`.** `ApplyTransitions`/`SetCell` 모두 `SymbolSlot[]`
  를 인덱스로 돈다. `foreach` 도, `List<T>` 도, 인터페이스 타입 컬렉션도 안 썼다 — 열거자
  할당이 생길 자리가 구조적으로 없다.
- **`Mathf.MoveTowards`** 는 `float` 세 개를 받는 정적 함수다. 박싱도 힙 할당도 없다.
- **`SetCell` 은 여전히 매 프레임 호출되지 않는다.** 원래도 스핀/층이 바뀐 순간에만
  호출됐고(`Update` 의 "달라진 게 없으면 건너뛴다" 가드), 그 호출 빈도를 안 바꿨다.
  그 안에서 쓰는 `KindOf(slot.Child.name)`(문자열 할당의 출처였던 `Object.name`)도
  전과 똑같이 "판이 바뀔 때만" 발생한다 — 매 프레임이 아니다.
- **정말 매 프레임 도는 새 코드는 `ApplyTransitions` 하나뿐**이고, 그 안의 두 중첩
  `for` 는 최대 9×3=27 회 반복 — 슬롯이 전부 목표에 도달해 있으면 `slot.Progress ==
  target` 에서 `continue` 하고 끝난다(부동소수 비교, 할당 없음). 즉 유휴 상태(전환도
  하이라이트도 없음)에서는 `ApplyTransitions` 가 27번의 `continue` 만 돌고 `false` 를
  반환하며, `ApplyHighlights` 는 그 `false` 를 받아 즉시 `return` 한다 — 기존 유휴
  경로(0 B)를 그대로 보존한다.

**검증 방법**: 이 파일이 자기 입으로 쓴 방법을 그대로 따르면 된다 —
`Ascend.Prototype.View.SpinBoardView.DiagnosticSkip` 을 0→1→2 로 돌리며
`PlayerPerfProbe.cs` (`Assets/Prototype_Elevator/Scripts/Run/Tests/PlayerPerfProbe.cs`)
로 소거 측정. **주의**: 레벨 1의 의미가 "`ApplyHighlights` 만 건너뜀"에서 "전환+하이라이트
(`ApplyTransitions`+`ApplyHighlights`) 를 함께 건너뜀"으로 넓어졌다 — 클래스 안 주석에도
그렇게 적어 뒀다. `PlayerPerfProbe.cs` 코드 자체는 정수값만 대입하므로 고칠 필요 없지만,
결과 해석("레벨 1 대비 레벨 0 의 차이 = 이 컴포넌트의 시각 폴리싱 비용")이 이제 두 메서드의
합산이라는 점만 인지하면 된다.

## 기존 하이라이트와 어떻게 합쳤는가

`ApplyHighlights()` 한 곳에서만 `Transform.localScale` 을 쓴다는 불변식을 그대로 지켰다.
`ApplyTransitions()` 는 `Progress` 값만 갱신하고 **스케일에는 손대지 않는다** — 손대면
두 메서드가 같은 프레임에 서로 다른 값을 `localScale` 에 대입하게 되고, 나중에 실행되는
쪽이 앞선 쪽을 덮어써서 정화 맥동이나 전환 둘 중 하나가 사라진다(팀 리드가 지적한 바로
그 함정).

최종 대입은 한 줄이다:

```csharp
slot.Child.localScale = slot.BaseScale * scale * slot.Progress;
```

`scale` 은 기존 하이라이트 배율(`Mathf.Lerp(1f, _highlightScale, amount)`), `Progress`
는 새 전환 배율. 셋 다 **곱**이라 어느 한쪽이 1(효과 없음)이면 다른 쪽만 남는다 —
평소(하이라이트 0, 즉 `scale=1`)엔 전환만 보이고, 정화 연출 중(전환 다 끝나서
`Progress=1`)엔 하이라이트 맥동만 보이고, 둘이 겹치는 드문 순간(칸이 막 나타나는데
동시에 정화 점등도 걸리는 경우)엔 둘 다 반영된다.

`ApplyHighlights` 의 기존 "달라진 게 없으면 건너뛴다" 최적화(`_highlightApplied` +
`Quantize` 32단계)는 구조를 그대로 뒀고, 게이트 조건만 넓혔다:

```csharp
if (!changed && !transitioning) return;
```

`transitioning` 은 `ApplyTransitions()` 의 반환값(이번 프레임에 `Progress` 를 실제로
움직였는가)을 그 자리에서 인자로 넘긴 것이다 — 필드로 따로 안 두고
`ApplyHighlights(ApplyTransitions())` 한 줄로 끝냈다(평가 순서상 `ApplyTransitions()`
가 먼저 실행되고 그 결과가 `ApplyHighlights` 로 들어간다).

## 1프레임 튐을 막은 지점 (덜 뻔한 부분)

`SetCell` 이 슬롯을 켤 때 `Progress=0` 으로 되돌리는 것과 별개로,
`slot.Child.localScale = Vector3.zero;` 를 **그 자리에서 동기적으로** 대입한다.

이유: `Update()` 안에서 `ApplyTransitions`/`ApplyHighlights` 가 board-refresh(`ShowBoard`
호출)보다 **먼저** 실행된다(기존 순서를 그대로 뒀다 — `DrivenExternally` 일 때도
하이라이트는 계속 그려야 해서 순서를 못 바꾼다). 즉 이번 프레임에 새로 켜지는 슬롯은
`ApplyHighlights` 가 이미 지나간 뒤에야 활성화된다. 여기서 스케일을 안 정해 주면, 그
슬롯이 "한 번도 칠해진 적 없는" 자식일 경우(씬에 저작된 원래 스케일 그대로) 이번
프레임에 **원래 크기로 잠깐 튀었다가** 다음 프레임에 0으로 줄고 다시 자라는, 고치기 전
버그보다 더 눈에 띄는 결함이 생긴다. `Progress=0` 이면 스케일도 항상 0이어야 한다는
불변식을 `SetCell` 이 스스로 지켜서 이 튐을 막는다.

## 검증하려면 무엇을 봐야 하는가

1. **컴파일**: `.cs` 를 반영한 뒤 `grep -an "error CS" Logs/Editor.log | tail -5` —
   줄 번호가 파일 끝에 가까운지 확인(CLAUDE.md 의 표준 절차).
2. **육안 — Play 모드**: 룰렛을 실제로 돌려서 칸이 바뀔 때 구슬이 **자라나며 등장 /
   줄어들며 퇴장**하는지 확인. 특히 캐스케이드처럼 한 프레임에 여러 칸이 동시에
   바뀌는 경우.
3. **정화 맥동 회귀 확인**: 정화 연출이 걸리는 칸에서 여전히 커졌다 작아지는 맥동이
   보이는지 — 이번 변경이 죽이기 가장 쉬운 부분이라 팀 리드가 특히 짚었던 지점이다.
4. **0 B 재확인**: `PlayerPerfProbe.cs` 로 `DiagnosticSkip` 0/1/2 소거 측정, 워밍업 후
   유휴 상태에서 0 B 인지, 전환 애니메이션이 도는 동안에는 (기존에 하이라이트가 그랬듯)
   0 B 가 아니어도 정상이라는 점 확인.
5. **에디터 캡처 경로**: `Ascend ▸ Capture — 눈높이 뷰`(`EyeLevelCapture.cs`) 를 Play
   모드 없이 실행해서 결과판이 여전히 즉시 채워져 찍히는지 확인 — `Application.isPlaying`
   분기가 실제로 이 경로를 지키는지의 직접 증거다.
6. **⚠ 남은 위험 — 캡처 하네스 타이밍**: `TenFloorCaptureRig.cs` 의 여러 샷이
   `ShowBoard(...)` 직후 `yield return WaitFrames(3);` 만 기다리고 찍는다(예: `02_device_front`,
   `01_entry`). 60fps 기준 3프레임 ≈ 0.05초인데 `_transitionDuration` 기본값은 0.12초다
   — 즉 그 3프레임 시점에는 방금 나타난/사라지는 구슬이 **아직 전환 중**일 수 있다.
   `ShowBoard` 를 부르는 각 지점이 "이미 떠 있던 칸의 내용을 바꾸는지"(전환 없음, 안전)
   "빈 칸에서 새로 채우는지"(전환 발생, 위험)에 따라 실제 영향이 갈리므로 코드만 보고는
   확정할 수 없다 — Play 모드에서 그 캡처들을 실제로 돌려서 눈으로 확인이 필요하다.
   문제가 확인되면 두 가지 선택지가 있다: (a) 그 캡처들의 `WaitFrames` 를 늘린다
   (`_transitionDuration` 만큼, 여유 있게 8~10프레임), 또는 (b) `_transitionDuration`
   을 캡처 친화적으로 줄인다. 이건 `Assets/` 아래 파일이라 이 세션에서는 고치지 않고
   여기 기록만 한다.
