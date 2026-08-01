# TOPDOWN PROGRESS

> **각 로컬 커밋 전에 이 파일을 갱신한다.** 갱신하지 않은 커밋은 진행 기록이 없는 커밋이다.
> 서술은 `ProgressLog.md`가 맡는다. 이 파일은 **지금 어디에 있는가**만 짧게 적는다.

---

## 현재 위치

| 항목 | 값 |
|---|---|
| 현재 패스 | **Pass 1 — Breadth First Coverage** |
| 브랜치 | `agent/phase2-full-prototype` |
| 마지막 정상 커밋 | `27dab79` — 탑다운 실행 구조 설치 (게임 코드 변경 없음) |
| 마지막 검증 통과 커밋 | **없음** — `verify-topdown.ps1`이 아직 통과한 적 없다 |
| 백로그 | `docs/TOPDOWN_MASTER_BACKLOG.md` (Required 130 · VERIFIED 0) |
| 갱신 시각 | 2026-08-01 |

---

## 다음 항목 — 우선순위 순

Pass 1의 완료 조건은 **Required 항목 중 `NOT_STARTED`가 0개**다. 현재 23개가 남아 있다.
정밀 폴리싱보다 누락 시스템 구현이 먼저다 (`CLAUDE.md` 실행 규칙).

| 순 | ID | 항목 | 왜 먼저인가 |
|---|---|---|---|
| 1 | UP-TEST-05 | 텔레메트리 (스핀별 20항목) | **PRD §4.1 명시적 필수인데 코드에 없다.** 없으면 이후 밸런스 판단의 근거가 전부 사후 추정이 된다 |
| 2 | UP-NPC-02 · 03 · 05 | 승객 반응 이벤트 10종 · `PassengerReactionSet` · 동시 반응 제한 | PRD §9 전체가 SKELETON 하나로 대표되고 있다 |
| 3 | UP-AUD-02 | 룰렛 사운드 10종 | 무영상·무음 판독성(PRD §11)의 절반이 비어 있다 |
| 4 | UP-POWER-07 · UP-RISK-07 · UP-TECH-09 | 프로파일 에셋 (`OverharvestProfile` 등 8종) | PRD §14.1 위반 상태. 승인 대기 항목을 프리셋으로 유지하려면 먼저 있어야 한다 |
| 5 | UP-PLAT-04 | `TargetHardwareProfile` | PRD §13.1이 **미지정 상태에서 성능 완료 선언을 금지**한다. Pass 4의 선행 조건 |
| 6 | UP-RISK-08 | `AccessibilityProfile` | PRD §8.3이 셰이크·사이렌·섬광을 옵션으로 분리하라고 요구 |
| 7 | UP-VIS-04 · 05 | 스타일 셰이더 · 파티클 | Pass 3의 선행 조건. Pass 1에서는 존재만 만든다 |
| 8 | UP-TECH-06 · 07 · 08 | 풀링 · 렌더링 예산 · 메모리 누적 | Pass 4 증거. Pass 1에서는 측정 지점만 만든다 |
| 9 | UP-TEST-08 · 09 | 5연쇄 영상 · Critical→과수확 영상 | PRD §17.6 증거 산출물. 하네스에 녹화가 없다 |
| 10 | UP-DOC-01 · 02 | Notion PRD §6.1 개정 · 위험 단계 이름 정합 | 최상위 문서가 코드와 어긋난 상태를 오래 두지 않는다 |

---

## 차단 사항

| 종류 | 항목 | 누가 풀 수 있는가 |
|---|---|---|
| **게이트 꺼짐** | `.claude/settings.local.json`의 `env`에 `SKIP_TOPDOWN_GATE=1`이 있다 (2026-08-01 설치 세션에서 끔). **자율 개발을 시작하기 전에 지울 것** | 에이전트. 세션 시작 알림이 매번 상기시킨다 |
| 외부 차단 | 없음 | — |
| 승인 대기 | 14건 (`PENDING_DECISIONS.md`) | 사용자. **작업을 멈추지 않는다** — 프리셋으로 진행 |
| 반복 실패 | UP-FIX-02 (임계점 눈금 숫자) 3회 실패 | 배치 결정이 필요하다. 같은 층위의 4번째 시도를 금지 |
| 측정 결함 | UP-TECH-04 — 중앙값이 vSync 상한(8.33ms)에 걸려 90 FPS 목표를 판정할 수 없다 | 프로브를 vSync off + 상한 없는 측정으로 바꿔야 한다 |

---

## 완료 항목

아직 `VERIFIED`로 전환된 Required 항목이 없다.
`CONNECTED` 91건은 증거 파일이 있으나 독립 검증을 거치지 않았다 —
백로그 §0.4의 승격 규칙 참조.

현재 분포 (`verify-topdown.ps1 -Stats` 출력과 일치해야 한다):
`NOT_STARTED 23` · `SKELETON 16` · `CONNECTED 91` · `VERIFIED 0` — 합계 130.

---

## 마일스톤 로그

각 20~30분 단위 복구 가능 마일스톤마다 한 줄씩 추가한다.
형식: `YYYY-MM-DD HH:MM · <커밋> · <패스> · <완료한 ID> · <테스트 결과>`

```
2026-08-01 · 27dab79 · Pass 1 · 탑다운 실행 구조 설치 (백로그 130 / 검증기 / Stop hook 2개) · 게임 코드 변경 없음, EditMode 91 PASS / PlayMode 394 PASS 유지
```
</content>
