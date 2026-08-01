# TOPDOWN PROGRESS

> **각 로컬 커밋 전에 이 파일을 갱신한다.** 갱신하지 않은 커밋은 진행 기록이 없는 커밋이다.
> 서술은 `ProgressLog.md`가 맡는다. 이 파일은 **지금 어디에 있는가**만 짧게 적는다.

---

## 현재 위치

| 항목 | 값 |
|---|---|
| 현재 패스 | **Pass 1 — Breadth First Coverage** |
| 브랜치 | `agent/phase2-full-prototype` |
| 마지막 정상 커밋 | `031ba8d` — VERIFIED 재판정 |
| 마지막 검증 통과 커밋 | **없음** — `verify-topdown.ps1`이 아직 통과한 적 없다 |
| 백로그 | `docs/TOPDOWN_MASTER_BACKLOG.md` |
| 갱신 시각 | 2026-08-01 (Pass 1 Wave A) |

## Required 상태 분포

`verify-topdown.ps1 -Stats` 출력과 일치해야 한다.

| 상태 | 개수 | 직전 |
|---|---|---|
| `VERIFIED` | **66** | 64 |
| `CONNECTED` | 25 | 26 |
| `VISIBLE` | 0 | 0 |
| `SKELETON` | **31** | 16 |
| `NOT_STARTED` | **7** | 23 |
| **Required 합계** | **129** | 129 |

## 테스트

| 산출물 | 값 | 직전 |
|---|---|---|
| `Logs/editmode_tests.txt` | **188 PASS / 0 FAIL** | 91 |
| `.claude/state/last-selftest.txt` | **207 PASS / 0 FAIL** | 110 |
| 컴파일 오류 | 0 | 0 |

`Logs/editmode_tests.txt`는 이제 **`AscendTestMenu.RunAll()`이 직접 쓴다.** 지금까지는
아무도 쓰지 않아 스위트가 늘어도 옛 숫자가 남아 있었고, 검증기는 그 옛 숫자를 읽고 있었다.

---

## Pass 1 Wave A — 이번에 들어온 것

| 영역 | 파일 | 채운 항목 |
|---|---|---|
| 공용 사건 버스 | `Scripts/Events/` | (기반) `D-20260801-04` |
| 텔레메트리 | `Scripts/Telemetry/` ×5 | `UP-TEST-05` |
| 데이터 프로파일 7종 | `Scripts/Data/Profiles/` ×7 | `UP-PLAT-04·05`, `UP-POWER-07`, `UP-RISK-07·08`, `UP-AUD-05`, `UP-TECH-09` |
| 승객 반응 | `Scripts/Npc/` ×5 | `UP-NPC-02·03·05` |
| 사운드 | `Scripts/Audio/` ×5 | `UP-AUD-02·03·04` |
| 풀링·성능 측정 | `Scripts/Perf/` ×4 | `UP-TECH-06·07·08` |
| 방어 테스트 | `Scripts/Spin/Tests/SpinRuleSetTests.cs` | `UP-CORE-14`, `UP-BUILD-09` |
| Collapse 연출 | `Scripts/Risk/CollapseSequence.cs` | `UP-RISK-06` |
| 위험 사건 발행 | `Scripts/Risk/RiskEventBridge.cs` | (기반) |
| 과수확 접근 판정 | `Scripts/Run/OverharvestApproachBridge.cs` | `UP-POWER-06` |
| 월드 사고 기록기 | `Scripts/View/PaperTapePrinterView.cs` | `UP-REC-04·05` |
| 증거 영상 | `Assets/CaptureHarness/GifEncoder.cs`, `Run/Tests/SequenceRecorder.cs` | `UP-TEST-08·09` |

**이 중 씬에 붙은 것은 하나도 없다.** 전부 `SKELETON`이며, 다음 마일스톤(Wave B)이
씬 오너 한 명으로 순차 배선한다. 코드가 있다는 것과 게임에서 일어난다는 것은 다르다.

---

## 다음 항목 — Pass 1 잔여 `NOT_STARTED` 7건

| 순 | ID | 항목 | 비고 |
|---|---|---|---|
| 1 | `UP-VIS-04` | URP 공통 스타일 셰이더 | 씬 오너. Pass 1에서는 존재만 |
| 2 | `UP-VIS-05` | 파티클 5종 | 씬 오너. 오버드로우 예산은 Pass 4 |
| 3 | `UP-TEST-11` | 레거시 정리 | `PD-13` 승인 필요. Pass 4 |
| 4 | `UP-VIS-07` | 시각 루브릭 통과 | Pass 3·4. 독립 평가 |
| 5 | `UP-VIS-09` | 축소 화면 판독 | Pass 3 |
| 6 | `UP-DOC-01` | Notion §6.1 정화 규칙 개정 | **차단됨 — 아래 참조** |
| 7 | `UP-DOC-02` | 위험 2단계 이름 `Strain` | `D-20260801-05`. 코드 개명 필요 |

## Wave B — 씬 배선 (한 명이 순차로)

Wave A가 만든 컴포넌트를 씬에 붙이고 `.asset` 7종을 만든다. 이것이 끝나야
`SKELETON` 31건이 실제로 내려간다.

---

## 차단 사항

| 종류 | 항목 | 누가 풀 수 있는가 |
|---|---|---|
| **권한 차단** | `UP-DOC-01` — Notion PRD §6.1을 인접 정화로 개정하려 했으나 **Notion 쓰기가 권한 계층에서 거부됐다.** 우회하지 않았다 | **사용자.** 직접 고치거나 Notion 쓰기를 허용한다 |
| 승인 대기 | 14건 (`PENDING_DECISIONS.md`) | 사용자. **작업을 멈추지 않는다** — 프리셋으로 진행 |
| 반복 실패 | `UP-FIX-02` (임계점 눈금 숫자) 3회 실패 | 배치 결정이 필요하다. 같은 층위의 4번째 시도 금지 |
| 측정 결함 | `UP-TECH-04` — 중앙값이 vSync 상한(8.33ms)에 걸려 90 FPS 목표를 판정할 수 없다 | 프로브를 상한 없는 측정으로 바꿔야 한다 |
| 미커밋 오염 | 폰트 아틀라스가 **글리프 순손실** 상태 (`NanumGothic SDF.asset`) | `git checkout HEAD -- <경로>`. 커밋하면 한글 렌더링이 깨진다 |

---

## 마일스톤 로그

형식: `YYYY-MM-DD · <커밋> · <패스> · <완료한 ID> · <테스트 결과>`

```
2026-08-01 · 27dab79 · Pass 1 · 탑다운 실행 구조 설치 (백로그 130 / 검증기 / Stop hook 2개) · 게임 코드 변경 없음, EditMode 91 PASS / PlayMode 394 PASS 유지
2026-08-01 · 1992651 · Pass 1 · 진행 문서에 마지막 정상 커밋 기록 · 변경 없음
2026-08-01 · 031ba8d · Pass 1 · 실증 감사와 130건 재분류 (VERIFIED 0 → 64) · 게임 코드·씬 무수정
2026-08-01 ·  (이번) · Pass 1 · Wave A — 사건 버스 + 텔레메트리·프로파일 7종·승객 반응·사운드·풀링·증거 영상·Collapse 연출·월드 기록기 · EditMode 91 → 188 PASS / 0 FAIL, 자체 검증 110 → 207 PASS / 0 FAIL, 컴파일 오류 0
```
