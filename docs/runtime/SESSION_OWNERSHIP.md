# ⚠ 동시 세션 경고 — 2026-08-04 자율 주행 중

## 사실

이 저장소에 **두 개의 Claude Code 세션이 동시에 쓰고 있다.**
`CLAUDE.md` 「에이전트 소유권 규칙」이 금지하는 상태다.

- 이 세션(A): 사용자 지시 「레벨디자인 재미 판정 + PRD 미구현 작업 + 그다음 비주얼」
  (출근 전 자율주행 요청). 10:26 커밋 `b2e299d`.
- 다른 세션(B): `docs/PROJECT_VISION.md`·`GAMEPLAY_SPEC.md`·`ART_BIBLE.md`·
  `PASSENGER_SYSTEM.md`·`LEVEL_BRIEF.md`·`MACHINE_SPEC.md`·`QUALITY_GATES.md`·
  `CURRENT_STATE.md`·`ITERATION_LOG.md` 를 10:09~10:22 에 작성. Notion 전수 조사 기반.

B 쪽도 이 사실을 스스로 발견해 `ITERATION_LOG.md` 끝에 적어 두었다 —
「조사 중 다른 세션이 같은 워킹 트리에 동시에 쓰고 있었다. Unity 에디터도 열려 있다.」

## 지금까지 실제로 부딪힌 것

- **없음(코드 기준).** B 의 산출물은 전부 `docs/*.md` 이고, A 는 `Assets/**` 와
  `docs/runtime/**` 를 만졌다. 겹친 파일이 없다.
- A 가 `git add -A` 로 커밋하면서 B 의 미완성 문서 9개를 함께 커밋했다
  (`b2e299d`). **덮어쓴 것이 아니라 스테이징한 것**이라 내용 손실은 없다.
  A 는 이후 경로를 명시해 스테이징한다.
- Unity MCP 파이프가 세 번 끊겼다(`Named pipe socket file not found`).
  B 가 Unity 를 함께 몰았다면 그 증상일 수 있다 — **확증은 없다.**

## 이 세션(A)이 선언하는 소유권

사용자가 자리에 없어 조정할 수 없으므로, 부딪히면 손상이 큰 것부터 명시한다.

| 자원 | 소유 | 근거 |
|---|---|---|
| **Unity 에디터 인스턴스** | **A** | A 가 컴파일·자체검증·밸런스 스윕을 이 인스턴스로 돌리고 있다 |
| `Assets/**` (코드·에셋) | **A** | |
| `.unity` / `.prefab` / `.mat` | **아무도 안 만짐** | 이번 세션에서 A 는 씬을 열지 않았다 |
| `docs/runtime/**` | **A** | 밸런스·재미 판정 산출물 |
| `docs/*.md` (신규 9종) | **B** | A 는 읽지도 고치지도 않는다 |
| `docs/ASSUMPTION_LOG.md`·`PENDING_DECISIONS.md`·`DECISION_LOG.md` | **양쪽 추가만** | append 전용이라 충돌이 잘 안 난다. **기존 줄을 고치지 않는다** |

## 사용자가 돌아오면 할 것

1. **세션 하나를 종료한다.** 어느 쪽을 남길지는 사용자 판단이다.
2. B 의 문서 9종을 검토한다 — A 는 읽지 않았고 커밋만 했다.
   내용이 `docs/` 의 기존 동결 명세(`MASTER_PRD.md` 등)와 충돌하는지 확인이 필요하다.
3. 특히 `docs/GAMEPLAY_SPEC.md` 와 `docs/LEVEL_BRIEF.md` 는 A 가 이번에 바꾼
   밸런스(요구 전력 곡선·계약 수치·정산율)와 **어긋날 수 있다.** A 의 근거는
   `docs/runtime/BALANCE_SOLVE.md` 의 실측이다.
