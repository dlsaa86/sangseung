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
