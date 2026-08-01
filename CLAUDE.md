# Upandup_DDD

Unity **6000.5.5f1** / URP 17.5.0. 엘리베이터 상승 + 룰렛 튜브 프로토타입.

```
docs/                              AI 개발 동결 명세
  MASTER_PRD.md                    최상위 제품 요구사항
  TECH_SPEC.md                     Unity 구조·상태·테스트 기준
  CURRENT_PHASE.md                 현재 세션 허용 범위
  VISUAL_SPEC.md                   비주얼 방향과 평가 기준
  DECISION_LOG.md                  확정 결정과 변경 이력
  ASSUMPTION_LOG.md                임시 기본값과 교체 위치
  TOPDOWN_MASTER_BACKLOG.md        탑다운 단일 작업 목록 (Required/Deferred/승인 대기)
  runtime/TOPDOWN_PROGRESS.md      현재 패스·다음 항목·차단 사항
  runtime/PENDING_DECISIONS.md     사용자 결정이 필요한 항목과 기본 프리셋
  runtime/VISUAL_VERDICT.md        독립 시각 평가 판정 (구현자가 쓰지 않는다)
tools/verify-topdown.ps1           탑다운 완료 검증기 (Stop hook이 실행)
Assets/Prototype_Elevator/
  Scenes/Prototype_Elevator.unity   유일한 작업 씬
  Scripts/{Core,Data,Effects,Roulette,UI}
  Data/PrototypeConfig.asset        밸런스 값
Assets/CaptureHarness/              결정론적 캡처 하네스 (README 참조)
Assets/Plans/                       T-00~ 작업 명세
.claude/visual-criteria.md          상세 시각 평가 기준
Captures/                           캡처 산출물 (gitignore, 기기 종속)
```

네임스페이스: 게임 코드 `Ascend.Prototype`, 하네스 `Ascend.CaptureHarness`.
**asmdef이 없다** — 모든 스크립트가 `Assembly-CSharp` 하나에 들어간다. 스크립트를
하나만 고쳐도 전체가 재컴파일되고 플레이 모드가 종료된다.

---

# 문서 권한과 시작 순서

에이전트는 작업을 시작하기 전에 반드시 다음 순서로 문서를 읽는다.

1. `docs/MASTER_PRD.md`
2. `docs/TECH_SPEC.md`
3. `docs/CURRENT_PHASE.md`
4. `docs/VISUAL_SPEC.md`
5. `docs/DECISION_LOG.md`
6. `docs/ASSUMPTION_LOG.md`
7. 관련 `Assets/Plans/` 티켓

충돌 시 앞선 문서가 우선한다. `MASTER_PRD.md`는 제품 범위를 정의하고,
`CURRENT_PHASE.md`는 이번 세션에서 구현할 수 있는 범위를 제한한다.

Notion은 편집 가능한 기획 원본이지만 실제 구현 중에는 `docs/`의 동결 스냅샷을 기준으로 한다.
Notion의 내용이 다르다고 판단되면 임의로 구현 기준을 바꾸지 말고 `DECISION_LOG.md`에 변경 제안을 기록한다.

작업 시작 시 다음을 수행한다.

1. 현재 코드·씬·에셋을 요구사항에 매핑한다.
2. 누락, 충돌, 기술 부채를 `docs/runtime/GapAnalysis.md`에 기록한다.
3. 작업 순서와 검증 계획을 `docs/runtime/ImplementationPlan.md`에 기록한다.
4. 불명확하지만 작업을 막지 않는 판단은 `docs/ASSUMPTION_LOG.md`에 기록한다.
5. `CURRENT_PHASE.md`의 Gate를 순서대로 통과한다.

---

# 탑다운 자율 실행 규칙

장시간 자율 개발은 `docs/TOPDOWN_MASTER_BACKLOG.md`의 **네 패스**를 순서대로 통과한다.
이 규칙들은 취향이 아니라 이 프로젝트가 실제로 겪은 실패에서 나왔다.

## 1. Pass 1·2가 끝나기 전에는 한 장면이나 기능을 두 번 이상 연속 미세 개선하지 않는다

전체 Coverage 이전의 폴리싱은 **아직 존재하지도 않는 것과 비교되지 않은 개선**이다.
같은 대상을 연속으로 두 번 손봤다면 멈추고 백로그의 다음 미구현 항목으로 간다.

## 2. 비주얼 평가 실패는 기록하고 다음 미구현 필수 범위로 이동한다

REJECT는 작업 종료 사유가 아니다. 지적을 백로그 §5 「수정 백로그」로 옮기고 계속한다.
**같은 층위에서 세 번 실패한 항목은 네 번째를 시도하지 않는다** — 필요한 것은 노력이
아니라 구조 변경이나 배치 결정이다 (`visual-verify` §6).

## 3. 전체 Coverage 이전에는 정밀 폴리싱보다 누락 시스템 구현을 우선한다

Pass 1의 차단 조건은 셋뿐이다 — 컴파일 오류, 데이터 손상, 진행 불가.
최종 모델링·최종 재질·정밀 밸런스·시각 평가 실패는 Pass 1을 막지 않는다.

## 4. 컴파일을 깨뜨린 상태로 다음 기능으로 이동하지 않는다

asmdef이 없어 스크립트 하나가 전체를 막는다. 깨진 채로 쌓으면 원인 분리가 불가능해진다.

## 5. 되돌릴 수 있는 결정은 질문하지 않고 기본값으로 진행한다

안전한 기본값을 고르고 `docs/ASSUMPTION_LOG.md`에 기록한다.
되돌리기 **어려운** 것만 `docs/runtime/PENDING_DECISIONS.md`로 올리고,
그 항목 때문에도 작업을 멈추지 않는다 — 교체 가능한 프리셋으로 진행한다.

## 6. 구현자는 자신의 작업을 VERIFIED로 최종 승인하지 않는다

`CONNECTED → VERIFIED`는 독립 검증을 거쳐야 한다. 시각 항목은
`docs/runtime/VISUAL_VERDICT.md`의 ACCEPT, 로직 항목은 독립 감사자.
승격 규칙 전문은 백로그 §0.4에 있다.

## 7. 20~30분 단위의 복구 가능한 마일스톤마다 저장 → 테스트 → 진행 로그 → 로컬 커밋

순서를 지킨다. 커밋 전에 `docs/runtime/TOPDOWN_PROGRESS.md`를 갱신한다 —
갱신하지 않은 커밋은 진행 기록이 없는 커밋이다.
커밋 게이트(`commit-gate.sh`)가 자체 검증 없는 커밋을 막는다.

## 8. 원격 push, PR 생성, main 병합은 금지한다

사용자가 명시적으로 요청할 때만 수행한다. 자율 실행 중에는 로컬 커밋까지다.

## 완료 판정 — 게이트는 **현재 패스에만** 적용된다

**완료는 선언이 아니라 `tools/verify-topdown.ps1`의 exit code 0이다.**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify-topdown.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify-topdown.ps1 -Stats   # 통계만
```

성공하면 `TOPDOWN_ALL_PASSES_COMPLETE` 한 줄과 exit 0. 그 외에는 남은 항목을 stderr로
내보내고 exit 2.

| 패스 | 완료 기준 | 이 패스가 막는 것 |
|---|---|---|
| **Pass 1** | 모든 Required 가 최소 `SKELETON`·`VISIBLE` | 상태 바뿐 |
| **Pass 2** | 모든 Required 가 최소 `CONNECTED` | 상태 바 + 끊긴 증거 경로 |
| **Pass 3** | 백로그 §1 `PASS3_GATED` 28항목 `VERIFIED` + 시각 `ACCEPT` | + 캡처 세트·독립 평가·콘솔 오류 |
| **Pass 4** | 모든 Required `VERIFIED` | + 전체 테스트·빌드·10층 증거·미커밋 0 |

**모든 Required의 `VERIFIED` 요구는 Pass 4에서만 적용한다.** 직전 판본은 패스와 무관하게
항상 최종 증거를 요구해 **Pass 1이 사실상 최종 QA처럼 작동했다** (2026-08-02 사용자 지시로 변경).

패스와 무관하게 항상 막는 것은 셋뿐이다 — 컴파일 통과, 분류 모순 없음, 진행 문서·브랜치.

검증기는 지금 막지 않는 요구를 **「지금은 막지 않는다」 절에 항상 함께 출력한다.**
게이트 완화가 곧 「사라진 요구사항」이 되지 않게 하기 위한 것이다.

> ⚠ `tools/verify-topdown.ps1`은 **UTF-8 BOM으로 저장해야 한다.** Windows PowerShell 5.1은
> BOM 없는 `.ps1`을 ANSI로 읽어 한글 문자열 리터럴이 깨지고, 그 깨진 바이트가 따옴표를
> 삼켜 **파일 전체가 파스 오류**가 된다 (2026-08-02에 겪었다).

## Pass 1 작업 리듬 (고속 Coverage)

- **항목마다** 전체 EditMode·PlayMode·빌드·캡처·독립 평가를 수행하지 **않는다.**
- 서로 연관된 **5~10개 항목을 한 배치**로 구현한 뒤 컴파일 + 최소 스모크 테스트를 한 번.
- 플레이스홀더·단순 형상·기본 데이터·임시 UI를 적극 쓴다.
- 진행 문서는 **배치 종료 시에만** 갱신한다. 커밋은 45~90분 단위 기능 묶음.
- 씬 오브젝트를 반복 수작업 배치하지 말고 **데이터 기반 런타임 생성기 또는 재실행 가능한
  Editor 조립 스크립트**를 쓴다 — 직렬화가 유일한 병목이기 때문이다.

Stop hook 두 개가 이를 강제한다 (`.claude/settings.json` — 추적되므로 다른 기기에서도 동일하게 동작한다).
① `command` — 위 스크립트. exit 2면 종료를 막고 남은 항목을 돌려준다.
② `agent` — 저장소를 직접 열어보는 독립 검증자. 대화 요약을 근거로 통과시키지 않는다.

## 게이트 끄기

탑다운과 무관한 세션(설정 정비, 문서 작업, 조사)에서는 `.claude/settings.local.json`의
`env`에 `"SKIP_TOPDOWN_GATE": "1"`을 넣는다. 이 파일은 gitignore 대상이라 기기 로컬에만 남는다.

**끈 상태를 잊는 것이 진짜 위험이다** — 꺼진 게이트는 아무도 막지 않으니 아무도 눈치채지
못한다. 그래서 `topdown-gate-notice.sh`가 세션 시작마다 꺼져 있다는 사실을 알린다.
자율 개발을 시작하기 전에 그 줄을 지운다.

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

다른 영역의 변경이 필요하면 직접 고치지 말고 통합 소유자에게 요청하거나 계획에 기록한다.

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

# 구현과 실패 대응

- 이미 정상 동작하는 시스템을 이유 없이 재작성하지 않는다.
- 핵심 규칙과 연출을 분리하고, 가변 값은 데이터 또는 프로파일로 둔다.
- 패키지, Unity 버전, URP 버전을 사용자 승인 없이 변경하지 않는다.
- 빌드 또는 핵심 테스트가 깨진 상태에서 새 기능을 추가하지 않는다.
- 동일 오류를 세 번 이상 같은 방식으로 수정하지 않는다.
- 반복 실패 시 최소 재현 테스트를 만들거나 비차단 기능을 인터페이스 뒤 플레이스홀더로 격리한다.
- 실패한 테스트를 삭제, 무시, 조건부 스킵으로 숨기지 않는다.
- 유료 에셋, 외부 유료 API, 라이선스가 불명확한 파일을 추가하지 않는다.
- 승인 대기 비주얼·모션·밸런스는 하나의 최종안으로 잠그지 않는다.

---

# 검증

각 마일스톤은 다음 루프를 수행한다.

`구현 → 컴파일·자동 테스트 → 실제 플레이 → 고정 캡처 → 시각 평가 → 성능 측정 → 수정 → 재검증`

화면에 보이는 것을 바꿨으면 `visual-verify` 스킬을 쓴다. 로직 전용 변경에는 쓰지 않는다
(캡처 1회에 수십 초 걸린다).

핵심 원칙:

- **구현자가 자기 결과를 최종 평가하지 않는다.** 평가자는 어느 쪽이 새 빌드인지 모른 채 본다.
- **직전 승인 빌드보다 나빠지면 채택하지 않는다.**
- 테스트, 캡처, 성능 기준을 통과하지 않은 기능은 완료로 선언하지 않는다.
- 실패와 미완료를 숨기지 않고 재현 절차와 원인을 기록한다.

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

**에디터가 열고 있는 씬 파일을 디스크에서 바꾸면 세션이 죽는다.** 다음 에셋 리프레시
(포커스가 에디터로 돌아오는 순간 — 즉 자동화가 제어를 넘기는 바로 그때)에 "외부에서
수정됨 / Reload·Ignore" 모달이 뜬다. 모달은 Unity 메인 스레드를 잡으므로 이후 MCP 호출은
전부 120초 타임아웃까지 매달리고, 사람이 클릭하기 전까지 아무것도 진행되지 않는다.
`unity-preflight.sh`로는 못 잡는다 — 프로세스는 멀쩡히 살아 있고 그냥 막혀 있을 뿐이다.

그래서 감지가 아니라 예방한다. `.claude/hooks/unity-worktree-guard.sh`가 에디터가
살아 있는 동안 다음을 막는다.

- 워킹 트리를 다시 쓰는 git 명령 (`checkout`/`restore`/`switch`/`stash`/`reset --hard`/
  `clean`/`revert`/`merge`/`rebase`/`pull`/`cherry-pick`/`apply`/`am`).
  `-- <경로>`로 범위를 좁혔고 그 경로가 `.unity`/`.prefab`/`.mat`에 닿지 않으면 통과한다
  (`font-atlas-guard.sh`가 안내하는 `.asset` 복구는 그대로 동작한다).
- `Write`/`Edit`로 `.unity`/`.prefab`/`.mat`을 텍스트로 고치는 것.

되돌릴 일이 있으면 경로를 명시하거나, `Unity_RunCommand`로 에디터 안에서 고치고
`SaveScene`한다. 에디터를 의도적으로 파킹해 뒀다면 `SKIP_UNITY_GUARD=1`을 붙인다.

그래도 뚫고 들어온 모달은 **자동으로 눌린다.** `unity-preflight.sh`가 Unity MCP 호출
직전에 `unity-modal-autoclick.ps1`을 돌린다. 사용자에게 묻지 않는다.

| 모달 제목 | 누르는 버튼 | 이유 |
|---|---|---|
| `The open scene(s) have been modified externally` | `Reload` | 디스크 쪽이 의도한 내용이다 |
| `Scene(s) Have Been Modified` | `Save` | 하네스가 곧 같은 씬을 다시 연다. `Don't Save`는 편집분을 조용히 버린다 |

**이 표에 없는 모달은 절대 누르지 않는다.** Unity는 "에셋을 삭제할까요?"에도 같은 모달
기구를 쓴다. 모르는 제목이면 버튼 목록만 `UNKNOWN`으로 보고하고 손대지 않는다 —
표를 늘리는 건 의도적인 결정이어야 한다. 클릭 기록은 `.claude/state/modal-autoclick.log`
에 남는다(gitignore 대상).

한계: 모달이 **호출 도중에** 뜨면 그 호출 하나는 120초 타임아웃까지 간다. 훅은 호출
직전에만 돌기 때문이다. 그 다음 호출이 치우고 정상화된다. 캡처 런처럼 오래 도는
구간에서 이 한 번도 아깝다면 감시 모드를 따로 띄운다.

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass \
  -File .claude/hooks/unity-modal-autoclick.ps1 -WatchSeconds 600
```

---

# 브랜치·커밋·푸시

- `main`에서 직접 장시간 자율 작업을 하지 않는다. 전용 `agent/<description>` 브랜치를 사용한다.
- 자율 작업 중 복구 가능한 마일스톤마다 로컬 커밋할 수 있다.
- 테스트가 실패한 상태를 저장해야 한다면 커밋 메시지와 완료 보고에 명시한다.
- 원격 푸시와 PR 생성은 사용자가 요청한 경우에만 수행한다.
- 사용자가 푸시를 요청했더라도 `main`에 직접 반영하지 않고 기본적으로 Draft PR을 만든다.
- `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Captures/`는 `.gitignore` 대상이다.
- `Packages/packages-lock.json`은 **반드시 커밋한다** — 다른 기기에서 패키지 버전이
  동일하게 재현되는 근거다.

---

# 완료 보고

완료 보고에는 최소 다음을 포함한다.

- 구현된 요구사항과 미구현 요구사항
- Definition of Done 대조표
- 변경 파일 목록
- 테스트 결과와 실패 항목
- 재현 시드
- 콘솔 오류
- 성능 측정 결과
- 고정 캡처 세트
- 실행 가능한 빌드 또는 빌드 차단 원인
- 남은 위험, 가정, 사용자 승인 항목
- 다음 세션의 구체적인 시작점

**PlayMode 자동화가 도는 동안 `AssetDatabase.Refresh()`를 부르지 않는다.**
스크립트가 바뀐 상태에서 Refresh 하면 도메인이 리로드되고, `Awake()`에서 잡은
비직렬화 필드(`MaterialPropertyBlock` 등)가 전부 null 이 된 채 오브젝트만 살아남는다.
런은 계속 도는 것처럼 보이지만 결과는 오염돼 있다. 컴파일 확인은 런이 끝난 뒤에 한다.
