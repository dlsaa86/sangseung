# Upandup_DDD

Unity **6000.5.5f1** / URP 17.5.0. 엘리베이터 상승 + 룰렛 튜브 프로토타입.

```
Assets/Prototype_Elevator/
  Scenes/Prototype_Elevator.unity   유일한 작업 씬
  Scripts/{Core,Data,Effects,Roulette,UI}
  Data/PrototypeConfig.asset        밸런스 값
Assets/CaptureHarness/              결정론적 캡처 하네스 (README 참조)
Assets/Plans/                       T-00~ 작업 명세
.claude/visual-criteria.md          시각 평가 기준
Captures/                           캡처 산출물 (gitignore, 기기 종속)
```

네임스페이스: 게임 코드 `Ascend.Prototype`, 하네스 `Ascend.CaptureHarness`.
**asmdef이 없다** — 모든 스크립트가 `Assembly-CSharp` 하나에 들어간다. 스크립트를
하나만 고쳐도 전체가 재컴파일되고 플레이 모드가 종료된다.

---

# 에이전트 소유권 규칙

여러 에이전트를 병렬로 돌릴 때 **반드시** 따른다. 이 규칙은 취향이 아니라 Unity의
파일 포맷에서 나오는 제약이다.

## 왜 필요한가

`.unity`, `.prefab`, `.mat`, `.asset`은 전부 YAML이고 내부가 fileID로 상호 참조된다.
두 에이전트가 같은 씬을 동시에 고치면 머지가 깨지는 게 아니라 **조용히 손상된다** —
텍스트로는 병합되지만 참조가 어긋나 런타임에 터진다. 되돌리기도 어렵다.

## 병렬 가능 — 디렉터리 단위 배타 소유

각 에이전트는 **자기 경로만** 수정한다. 남의 경로는 읽기만 한다.

| 영역 | 소유 경로 |
|---|---|
| 게임플레이 로직 | `Scripts/Core`, `Scripts/Roulette`, `Scripts/Effects` |
| 데이터·밸런스 | `Scripts/Data`, `Data/*.asset` |
| UI | `Scripts/UI` |
| 툴·하네스 | `Assets/CaptureHarness` |

다른 영역의 변경이 필요하면 **직접 고치지 말고 요청한다.**

## 단일 소유자 — 순차 처리

아래는 서로 강하게 얽혀 있어 **동시에 여러 에이전트가 건드리지 않는다.** 한 명이
순서대로 처리한다.

- `*.unity` — 씬은 하나뿐이라 사실상 모든 씬 작업이 직렬화된다
- `*.prefab`
- `*.mat`, 조명, 포스트프로세싱 볼륨
- `Assets/Settings/` — URP 렌더러·RP 에셋·볼륨 프로파일
- 전체 아트 디렉션

**동시에 두 에이전트가 씬을 열지 않는다.** 에디터 인스턴스도 하나만 띄운다.

## 작업 전 확인

```bash
git status --porcelain    # 지저분하면 먼저 정리한다. 되돌릴 수 없는 상태에서 시작하지 않는다
```

---

# 검증

화면에 보이는 것을 바꿨으면 `visual-verify` 스킬을 쓴다. 로직 전용 변경에는 쓰지 않는다
(캡처 1회에 수십 초 걸린다).

핵심 원칙 두 가지만 기억하면 된다:
- **구현자가 자기 결과를 평가하지 않는다.** 평가자는 어느 쪽이 새 빌드인지 모른 채 본다.
- **직전 승인 빌드보다 나빠지면 채택하지 않는다.**

베이스라인(`Captures/baseline.txt`)은 **기기에 종속된다.** OS·GPU·그래픽 API가 다르면
렌더 결과가 비트 단위로 다르므로, 기기를 옮기면 그 기기에서 베이스라인을 새로 세운다.
manifest의 `machineFingerprint`가 이를 강제한다.

---

# 환경 주의사항

시간을 크게 낭비하게 만드는 것들이다.

**에디터 로그는 프로젝트 상대 경로에 있다** — `Logs/Editor.log`.
`%LOCALAPPDATA%\Unity\Editor\Editor.log`가 아니다(부팅 직후 그쪽으로 옮겨간다).
MCP의 콘솔 조회는 Clear-on-Play 이후 비어 보이므로, 플레이 모드 결과는 이 파일에서 확인한다.

**MCP `Unity_RunCommand`는 `System.Reflection`을 차단한다.** 타입을 직접 참조하거나
`AppDomain.CurrentDomain.GetAssemblies()`로 우회한다.

**에디터가 포커스를 잃으면 플레이 루프가 멈춘다** (`runInBackground=false`).
자동화 중 플레이 모드가 진행되지 않으면 이걸 먼저 의심한다.

**`OpenScene`은 참조되지 않은 ScriptableObject의 네이티브 객체를 파괴한다.**
씬을 열기 전에 필요한 값을 미리 읽어둔다.

**플레이 모드 진입은 도메인 리로드 때문에 수십 초 걸린다.** 폴링하지 말고 백그라운드로
돌린 뒤 알림을 받는다.

---

# 커밋

- 사용자가 요청할 때만 커밋·푸시한다
- `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Captures/`는 `.gitignore` 대상
- `Packages/packages-lock.json`은 **반드시 커밋한다** — 다른 기기에서 패키지 버전이
  동일하게 재현되는 근거다
