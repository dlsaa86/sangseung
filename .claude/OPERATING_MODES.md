# 운영 모드 — 협업 / 자율주행

이 문서는 **누가 지휘하고 누가 실행하는가**, 그리고 **사용자가 자리에 있을 때와 없을 때
무엇이 달라지는가**를 정의한다. `CLAUDE.md`에서 import 되어 항상 로드된다.

---

## 0. 지휘 체계

```
Claude Code Game Studios   판단 체계 — 비전, GDD, 아키텍처, 작업 분해, 완료 기준
        ↓
Ouroboros                  반복 실행 엔진 — 구현 → 평가 → 수정 → 재검증, 수렴까지
        ↓
Unity MCP                  손과 눈 — 씬·프리팹·플레이모드·콘솔·캡처
        ↓
Git                        복구 장치 — 브랜치, 체크포인트
```

**Ouroboros가 게임을 지휘하지 않는다.** Ouroboros는 Game Studios가 확정한 작업 명세를
실행하고 검증하는 하위 엔진으로만 쓴다. 무엇을 만들지, 어떤 모양이어야 하는지,
무엇이 완료인지는 Game Studios 계층에서 결정된다.

---

## 1. 이 문서와 기존 규칙의 우선순위

Game Studios 에이전트 39개는 이 프로젝트의 물리적 제약을 **모른다.** 충돌하면
아래 순서로 이긴다.

1. **`CLAUDE.md`의 「에이전트 소유권 규칙」과 「환경 주의사항」** — Unity YAML 손상,
   도메인 리로드, 모달 잠금은 Game Studios가 알지 못하는 이 저장소의 물리 법칙이다.
2. `docs/MASTER_PRD.md` → `TECH_SPEC.md` → `CURRENT_PHASE.md` → `VISUAL_SPEC.md`
3. 이 문서 (운영 모드)
4. `.claude/rules/*.md` (Game Studios 경로별 코딩 표준)
5. Game Studios 에이전트·스킬의 기본 절차

특히 **어떤 모드에서도** 다음은 뒤집히지 않는다.

- `.unity` / `.prefab` / `.mat` / `.asset`은 **`unity-scene-owner` 에이전트만** 수정한다.
  Game Studios의 `unity-specialist`, `technical-artist`, `level-designer` 등이
  씬을 직접 고치려 하면 막고 `unity-scene-owner`에게 넘긴다.
- **동시에 두 에이전트가 씬을 열지 않는다.** 에디터 인스턴스도 하나만 띄운다.
- `Write`/`Edit`로 직렬화 에셋을 텍스트 편집하지 않는다. `Unity_RunCommand`로
  에디터 안에서 고치고 `SaveScene` 한다.
- 에디터가 살아 있는 동안 워킹 트리를 다시 쓰는 git 명령을 쓰지 않는다.

---

## 2. MODE: COLLABORATIVE (기본값)

사용자가 컴퓨터 앞에 있을 때. **명시적으로 자율주행이 트리거되기 전까지 항상 이 모드다.**

```
review-mode: full
```

### 규칙

- **창작적 결정은 선택지를 제시하고 사용자가 고른다.** 분위기, 구조, 레버 모양,
  승객 시스템, 아트 방향, 밸런스 곡선이 여기 해당한다.
- 구조 변경 전 승인을 받는다.
- 레퍼런스·`VISUAL_SPEC.md`와 충돌하면 멈추고 보고한다.
- **Ouroboros의 지속 루프(`ooo ralph`, `ouroboros_start_ralph`)를 실행하지 않는다.**
  단발 `evaluate` / `qa` 호출은 허용한다.
- 필요하면 Game Studios 전문 에이전트를 호출한다.

### 이 모드에서 쓰는 워크플로

| 목적 | 스킬 |
|---|---|
| 시스템 설계 | `/design-system`, `/quick-design`, `/map-systems` |
| 레벨·공간 | `/team-level`, `/ux-design` |
| 아트 기준 | `/art-bible`, `/asset-spec` |
| 아키텍처 | `/create-architecture`, `/architecture-decision`, `/architecture-review` |
| 작업 분해 | `/create-epics`, `/create-stories`, `/story-readiness` |
| 구현 | `/dev-story` |
| 검토 | `/design-review`, `/gs-code-review`, `/ux-review` |
| QA | `/team-qa`, `/qa-plan`, `/smoke-check` |
| 폴리싱 | `/team-polish`, `/perf-profile` |
| 상황 파악 | `/sprint-status`, `/project-stage-detect`, `/help` |

> `/code-review`는 Claude Code 내장 명령이다. Game Studios의 것은 **`/gs-code-review`**로
> 개명해 두었다.

---

## 3. MODE: AFK_AUTONOMOUS

사용자가 자리를 비울 때만. **모드 전환은 사용자의 명시적 발화로만 일어난다.**

### 트리거 문구

```
나 이제 잘게 / 나 자러 갈게 / 나 출근할게 / 나 출근해야 해
자율주행 시작해 / 내가 없는 동안 진행해 / AFK
```

### 진입 조건 (셋 다 충족해야 한다)

1. 사용자가 취침·출근·외출·자율주행을 **명시**했다.
2. 현재 작업 목표가 존재한다.
3. 완료 기준(acceptance criteria)을 작성할 수 있다.

목표가 모호하면 — **진행 중인 Story가 있으면 그것을 이어간다.** 없으면 가장 최근에
명시된 작업을 대상으로 삼는다. 둘 다 없으면 자율주행에 들어가지 않고, 무엇을 할지
한 줄로 묻고 협업 모드에 머무른다.

### 진입 절차

```
1. 현재 작업 내용을 하나의 Story로 고정한다        → /create-stories 또는 수기 story 파일
2. 작업 범위와 금지 범위를 기록한다
3. acceptance criteria 를 작성한다                 → /story-readiness 로 READY 확인
4. git checkpoint 를 만든다                        → 커밋
5. agent/<주제> 브랜치에서만 작업한다
6. review-mode 를 solo 로 내린다                   → production/review-mode.txt
7. Game Studios 가 담당 에이전트를 고른다
8. Ouroboros 가 구현·평가·수정 루프를 돈다
9. Unity 컴파일 · 콘솔 · 테스트 · 플레이 화면을 확인한다
10. 완료 조건을 통과하면 멈춘다
11. 결과 보고서를 남긴다                           → production/session-logs/
```

`solo`로 내리는 것은 Game Studios의 디렉터 게이트를 끄는 것이고, 그 자리를
**Ouroboros의 검증 게이트가 대신한다** — 게이트를 없애는 게 아니다.

### 복귀 시

사용자가 돌아오면 **자동으로 COLLABORATIVE로 되돌리고** `review-mode`를 `full`로
복원한다. 보류 항목을 먼저 보고한다.

---

## 4. 자율주행 중 금지

```
- main 브랜치 직접 수정 또는 병합
- 원격 push, PR 생성
- 기존 핵심 에셋 삭제
- 대규모 폴더 이동
- Unity 버전 변경 (6000.5.5f1 고정)
- 렌더 파이프라인 변경 (URP 17.5.0 고정)
- 패키지 대량 설치 또는 업그레이드
- GDD 핵심 규칙 변경
- 게임의 아트 방향 변경
- 레퍼런스에 없는 신규 시스템 발명
- 유료 API 또는 유료 에셋 구매
- 환경변수·비밀키 접근
- 두 에이전트가 동시에 씬 열기
- Unity 에디터가 열려 있는 동안 워킹 트리를 다시 쓰는 git 명령
```

앞의 넷은 `.claude/settings.json`의 `deny`/`ask`로도 막혀 있다. 나머지는 규율이다.

---

## 5. 막혔을 때 — 질문하고 멈추지 않는다

사용자가 없는 동안 질문을 띄우고 대기하면 밤 전체가 낭비된다.

### 스스로 처리한다

```
컴파일 오류 · NullReferenceException · 잘못된 컴포넌트 연결
테스트 실패 · 좌표/크기/레이아웃 조정 · 성능 저하
명명 규칙 위반 · 기존 설계에 명확히 답이 있는 문제
```

되돌릴 수 있는 결정은 기본값으로 진행하고 `docs/ASSUMPTION_LOG.md`에 적는다.

### 해당 작업만 보류하고 **다른 독립 작업을 계속한다**

```
두 설계안이 모두 타당한 창작 결정
기존 GDD 와 사용자 지시가 충돌
핵심 구조를 다시 만들어야 하는 상황
저장 데이터가 손상될 가능성
대규모 에셋 삭제가 필요한 상황
외부 유료 서비스가 필요한 상황
```

보류 항목은 `docs/runtime/PENDING_DECISIONS.md`에 **교체 가능한 기본 프리셋과 함께**
적는다. 세션 전체를 멈추지 않는다.

---

## 6. 반복 종료 조건

`ooo ralph`는 지속 루프다. 아래 중 **하나라도** 걸리면 즉시 멈추고 보고한다.

```
- 모든 acceptance criteria 통과          ← 정상 종료
- 같은 실패가 3회 반복
- 같은 파일을 5회 이상 재작성
- 품질 점수가 3회 연속 개선되지 않음
- 범위 밖 변경이 필요함
- Unity 가 정상적으로 열리지 않음 / 모달에 잠김
- 컨텍스트 또는 사용량 한계 접근
```

"7시간 무조건 실행"이 아니라 **완료 기준을 만족할 때까지 돌리되 정체되면 중단**한다.

Unity 모달은 사람이 눌러야 한다 (`CLAUDE.md` 「환경 주의사항」의 표 참조).
모달에 잠기면 그 시점에서 루프를 끝내고 무엇이 떴는지 보고한다 — 자동으로 누르지 않는다.

---

## 7. 야간 파이프라인

```
 1. producer              현재 목표를 Story 로 정리
 2. creative-director     기존 게임 비전과 충돌 여부 확인
 3. art-director          PS1 로우폴리 · 픽셀 텍스처 기준 고정
 4. unity-specialist      Unity 구현 계획 수립       (씬은 만지지 않는다)
 5. technical-artist      모델 · 머티리얼 · 조명 · 텍스처 검사
 6. gameplay-programmer   상호작용 및 애니메이션 구현
 7. qa-lead               검증 항목 작성
 8. Ouroboros             구현 → 검사 → 수정 반복
 9. unity-scene-owner     실제 씬 반영 · 플레이모드 · 고정 캡처   ← 씬을 만지는 유일한 지점
10. visual-critic         블라인드 시각 평가                      ← 구현자와 분리
11. producer              변경 · 성공 · 실패 정리 후 보고서
```

9·10번이 이 프로젝트 고유의 삽입 지점이다. Game Studios 기본 파이프라인에는 없고,
**빼면 씬이 손상되거나 구현자가 자기 결과를 자기가 승인하게 된다.**

---

## 8. 탑다운 백로그와의 관계

`docs/TOPDOWN_MASTER_BACKLOG.md`의 네 패스는 **그대로 유효하다.**
Game Studios의 Story/Epic 체계는 그것을 대체하지 않고 **한 패스 안의 작업을
분해하는 도구**로 쓴다.

- Pass 1·2 (Coverage) → `solo`. 플레이스홀더 허용, 배치 단위 검증.
- Pass 3·4 (검증·최종) → `full`. 디렉터 게이트와 독립 평가가 다시 켜진다.
- 「구현자는 자신의 작업을 VERIFIED 로 최종 승인하지 않는다」는 자율주행 중에도 유효하다.
  `CONNECTED → VERIFIED` 승격은 사용자가 돌아온 뒤에 한다.
