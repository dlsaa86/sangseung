# TOPDOWN MASTER BACKLOG — 상승 (Upandup_DDD)

> 생성: 2026-08-01 · 출처: Notion 11개 문서 + 저장소 실제 구현 감사
> 이 문서는 **탑다운 자율 개발의 단일 작업 목록**이다. 범위 판정은 `MASTER PRD`가 최상위다.
> 상태 값은 사람이 아니라 `tools/verify-topdown.ps1`이 읽는다. 형식을 바꾸면 검증기가 깨진다.

---

# 0. 이 문서를 읽고 쓰는 규칙

## 0.1 출처 문서와 권한

| 코드 | 문서 | 권한 |
|---|---|---|
| PRD | 📐 MASTER PRD — 상승 (`3ada30cad9c58106b9a8c4ee03dd995c`) | **최상위.** 범위·완료 기준 |
| N00 | 🎯 [참고] 00. 핵심 비전과 플레이 경험 | 참고 원본 |
| N01 | 🔴 [참고] 01. 영혼 수확 자동 룰렛 시스템 | 판정 상세 원본 |
| N02 | 🛗 [참고] 02. 적재·탑승·빌드 시스템 | 빌드 카탈로그 원본 |
| N03 | 🏢 [참고] 03. 층 구조·테마·특수 숫자 이벤트 | 레벨·커리큘럼 원본 |
| N04 | 👥 [참고] 04. NPC·몬스터·괴담 콘텐츠 | 콘텐츠 제작 규칙 |
| N05 | 👁️ [스포일러] 05. 세계관의 진실·반전·멀티 엔딩 | 개발용 스포일러 |
| N06 | 🧊 [에셋 부록] 06. 3D 에셋 생성용 이미지 프롬프트 가이드 | 에셋 파이프라인 |
| N07 | 🔮 [비주얼 부록] 07. 영혼 구슬·저항체 디자인 체계 | 심볼 판독 원본 |
| N08 | 🛠️ [기술 부록] 08. Unity 프로토타입 구조·테스트 명세 | 클래스·테스트 부록 |
| N99 | 🧪 99. 결정 로그·미결정 사항 | 결정 기록·체크리스트 |
| REPO | `docs/DECISION_LOG.md`, `docs/ASSUMPTION_LOG.md` | 저장소 확정 결정 |

충돌 시 **PRD > N08 > 분야별 원본 > N99 > 아카이브** 순으로 우선한다 (PRD §1.1).

## 0.2 분류 — 무엇이 Required인가

**Required는 PRD §4.1 「반드시 구현」과 §13·§15·§17의 완료 기준에서만 나온다.**
다른 노션 문서에만 있는 아이디어는 아무리 매력적이어도 Required가 아니다.

| 분류 | 판정 근거 |
|---|---|
| **Required** | PRD §4.1 필수 구현 목록 / §13 엔지니어링 목표 / §15 시각 평가 / §17 Definition of Done |
| **Deferred** | PRD §4.2 명시적 제외, 또는 다른 문서에만 있고 PRD 필수 범위에 없는 확장 |
| **Approval Required** | PRD §14.2 「승인 전 잠그지 않을 항목」 + `ASSUMPTION_LOG.md` 미뤄 둔 결정 |

Approval Required 항목은 **작업을 멈추는 사유가 아니다.** 교체 가능한 프리셋으로 진행하고
`docs/runtime/PENDING_DECISIONS.md`에 기록한다 (PRD §1.2).

## 0.3 상태 값 — 정확히 일곱 개

| 값 | 의미 |
|---|---|
| `NOT_STARTED` | 구현이 실제로 없다 |
| `SKELETON` | 타입·데이터·코드 골격만 있다 |
| `VISIBLE` | 씬이나 화면에 보이지만 게임 규칙과 연결되지 않았다 |
| `CONNECTED` | 실제 플레이 규칙과 연결됐다. 검증 증거는 부족하다 |
| `VERIFIED` | 코드·씬·테스트 또는 실제 플레이 증거가 **모두** 있다 |
| `BLOCKED_EXTERNAL` | 현재 환경에서만 해결 불가능한 외부 차단 |
| `DEFERRED` | MASTER PRD상 1차 프로토타입 범위 밖 |

**Required 항목이 하나라도 VERIFIED가 아니면 전체 작업은 완료가 아니다.**

> `VISIBLE`은 2026-08-01 감사에서 추가됐다. 파일도 있고 오브젝트도 씬에 있으니
> `SKELETON`이 아니고, 규칙과 이어지지 않았으니 `CONNECTED`도 아니다. 이 구간에
> 이름이 없으면 **죽은 연출이 구현으로 계상된다.**

## 0.4 VERIFIED 판정 규칙

`VERIFIED`는 다음 셋이 **함께** 있을 때 부여한다.

1. **코드** — 기능을 담은 실제 구현 파일이 존재하고 껍데기가 아니다.
2. **씬 또는 플레이 접근 경로** — 플레이어가 실제로 도달할 수 있다 (또는 개발 도구라면 실행 가능하다).
3. **증거** — 자동 테스트 PASS, PlayMode 단정, 또는 상태가 실측된 고정 캡처.

**문서에 완료라고 적혀 있다는 것만으로는 VERIFIED가 아니다.**
반대로 **실제 코드·씬·테스트 증거가 있으면 과거 문서가 미완료라고 적었더라도
현재 상태를 우선한다.**

> **2026-08-01 규칙 변경.** 직전 판본은 "독립 평가자 서명"까지 요구해 모든 항목을
> `CONNECTED`에 묶어 뒀다. 그 결과 실제로 91건이 테스트로 증명돼 있는데도 백로그는
> 전부 미완료로 보였고, **무엇이 이미 되어 있는지 알 수 없어 중복 구현 위험이 컸다.**
> 사용자 지시로 판정 기준을 위 3항목으로 바꾼다. 독립 평가는 없어지는 것이 아니라
> Pass 3·4의 **비주얼·성능 항목에만** 남는다 (`UP-VIS-07`, `UP-TECH-04`, `UP-TECH-05`).

---

# 1. 패스 상태

<!-- verify-topdown.ps1 이 아래 네 줄과 PASS3_GATED 줄을 파싱한다. 형식을 바꾸지 말 것. -->

- PASS_1: COMPLETE
- PASS_2: COMPLETE
- PASS_3: COMPLETE
- PASS_4: IN_PROGRESS

<!-- 2026-08-02 게이트 현실화 (사용자 결정) — 아래 두 줄은 설명이고 파싱 대상이 아니다.
     시각 평가: Pass 3 은 「현재 캡처에 대한 독립 판정이 존재하는가」를 묻는다.
                4.0/4.0 ACCEPT 는 Pass 4 로 옮겼다. VERIFIED 급 기준이기 때문이다.
     PASS3_GATED 28건: Pass 3 은 CONNECTED, Pass 4 가 VERIFIED 를 요구한다. -->

## 1.0 패스별 완료 기준 — 게이트는 **현재 패스에만** 적용된다

> **2026-08-02 구조 변경 (사용자 지시).** 직전 판본은 패스와 무관하게 항상
> 「Required 전부 VERIFIED + 전체 테스트 + 빌드 + 캡처 + 독립 평가」를 요구했다.
> 그 결과 **Pass 1이 사실상 최종 QA처럼 작동했다** — 플레이스홀더 하나를 넣을 때마다
> 최종 증거를 요구받아 Coverage 속도가 죽었다. 이것은 의도와 다르다.

| 패스 | 완료 기준 |
|---|---|
| **Pass 1** | 모든 Required 가 최소 `SKELETON` 또는 `VISIBLE` |
| **Pass 2** | 모든 Required 가 최소 `CONNECTED` |
| **Pass 3** | 아래 `PASS3_GATED` 항목이 `VERIFIED` + `VISUAL_VERDICT.md` 가 `ACCEPT` |
| **Pass 4** | 모든 Required 가 `VERIFIED` + 전체 테스트·빌드·캡처·성능·독립 평가 |

**모든 Required 의 `VERIFIED` 요구는 Pass 4 에서만 적용한다.**

### 1.0.1 항목마다 **소유 패스**가 다르다 (2026-08-02 2차 교정)

위 표의 「모든 Required」는 **그 패스가 소유한 Required** 를 뜻한다.
소유 패스는 각 항목의 `- 상태: … · 패스: P2 P3` 필드에서 **가장 이른 패스**이고,
`verify-topdown.ps1` 이 그 줄을 직접 파싱한다.

이 교정이 없으면 비주얼·성능 항목이 Pass 2 를 막고, 그러면 「연결하는 단계」가
다시 최종 QA 가 된다 — 1.0 이 고친 것과 같은 병이 한 층 아래에서 재발한다.

| 소유 | 건수 | 예 |
|---|---|---|
| P1 | 53 | 코어 판정·기반 |
| P2 | 45 | 플레이 흐름 연결 — `UP-POWER-06` |
| P3 | 17 | 경험·비주얼 — `UP-PLAT-05` · `UP-VIS-01` · `UP-VIS-04` |
| P4 | 14 | 검증·성능·정리 — `UP-TECH-04` · `UP-TECH-05` · `UP-TECH-09` · `UP-TEST-11` |

**Pass 4 는 소유권과 무관하게 전부 요구한다** (게이트 4 는 모든 항목 게이트 이상이다).
따라서 후속 패스로 미룬 것이 사라지지 않는다. 검증기는 미달이지만 **아직 그 패스 소유가
아닌 것**을 「지금은 막지 않는다」 절에 **ID 까지 적어** 출력한다.

패스와 무관하게 **항상** 막는 것은 셋뿐이다 — ① 컴파일이 통과했는가(asmdef 이 없어
스크립트 하나가 전체를 막는다) ② 분류가 모순되지 않는가 ③ 진행 문서와 브랜치가 살아 있는가.

검증기는 지금 막지 않는 요구를 **「지금은 막지 않는다」 절에 항상 함께 출력한다.**
게이트 완화가 곧 「사라진 요구사항」이 되지 않게 하기 위한 것이다.

<!-- Pass 3 이 VERIFIED 를 요구하는 경험·비주얼·사운드·피드백 항목. 한 줄에 유지할 것. -->
- PASS3_GATED: UP-VIS-01, UP-VIS-02, UP-VIS-03, UP-VIS-04, UP-VIS-05, UP-VIS-06, UP-VIS-07, UP-VIS-08, UP-VIS-09, UP-VIS-10, UP-AUD-01, UP-AUD-02, UP-AUD-03, UP-AUD-04, UP-AUD-05, UP-RISK-03, UP-RISK-04, UP-RISK-05, UP-RISK-06, UP-NPC-04, UP-CORE-11, UP-CORE-12, UP-CORE-13, UP-SPACE-03, UP-SPACE-09, UP-DEVICE-09, UP-DEVICE-10, UP-REC-05

## Pass 1 — Breadth First Coverage (고속)

**목표는 완성도가 아니라 필수 범위를 한 번 전부 존재하게 만드는 것이다.**

- 모든 Required 시스템·콘텐츠가 코드 또는 교체 가능한 플레이스홀더로 존재한다.
- 모든 핵심 오브젝트가 Unity 공간에 실제로 존재한다.
- **플레이스홀더·단순 형상·기본 데이터·임시 UI를 적극 쓴다.**
- 기능이 코드·씬·화면에 존재하고 **다음 패스에서 연결할 수 있으면 다음 항목으로 간다.**
- **한 시스템이나 장면을 두 번 연속 폴리싱하지 않는다.**
- **차단 조건은 셋뿐이다** — 컴파일 오류, 데이터 손상, 기존 핵심 진행 파괴.
- 최종 모델링·최종 재질·정밀 밸런스·성능 미측정·시각 평가 REJECT·최종 테스트 부재는
  Pass 1의 차단 조건이 **아니다.**

### Pass 1 작업 리듬

- **항목마다** 전체 EditMode·PlayMode·Windows 빌드·캡처·독립 평가를 수행하지 **않는다.**
- 서로 연관된 **5~10개 항목을 한 배치**로 구현한 뒤 컴파일과 **최소 스모크 테스트를 한 번** 돌린다.
- 진행 문서는 **배치 종료 시에만** 갱신한다.
- 로컬 커밋은 **45~90분 단위의 일관된 기능 묶음**으로 만든다.

완료 조건: Required 항목 중 `NOT_STARTED`가 0개 (= 전부 `SKELETON` 이상).

## Pass 2 — Full Integration

- 1층부터 10층까지 모든 Required 시스템이 하나의 플레이 흐름으로 연결된다.
- 승객·부품·적재·과적·계약·룰렛·캐스케이드·위험·과수확·사고가 서로 **실제 규칙을 바꾼다.**
- 플레이스홀더라도 플레이어가 공간에서 발견하고 조작할 수 있다.
- 백로그가 가리키는 증거 경로가 **끊겨 있지 않다** (파일이 실제로 존재한다).

완료 조건: Required 항목 중 `NOT_STARTED`·`SKELETON`·`VISIBLE`이 0개.

## Pass 3 — Experience and Visual Pass

- 노션의 그래픽 방향(PRD §12, N06, N07)과 장치 디자인을 반영한다.
- 엘리베이터 구조, 수확 장치, 레버, 심볼, 층 공간, 승객 배치를 개선한다.
- 플레이 흐름·피드백·연출·판독성·공포 분위기를 개선한다.
- **비주얼 REJECT는 작업 종료 사유가 아니라 수정 백로그로 전환한다** — REJECT를 받으면
  §5 「수정 백로그」에 항목을 추가하고 다음 미구현 필수 범위로 이동한다.

완료 조건: 위 `PASS3_GATED` 28항목이 전부 `VERIFIED` +
PRD §15.2 루브릭 통과 + `docs/runtime/VISUAL_VERDICT.md`가 `ACCEPT`.

## Pass 4 — Verification and Polish

- 전체 테스트 (EditMode + PlayMode)
- Windows 빌드
- 고정 시드 런 (서로 다른 시드 3개 이상)
- 독립 비주얼 평가
- 성능 측정 (CPU·GC, 최악 장면 포함)
- 회귀 검사
- 미사용 레거시와 임시 코드 정리
- 모든 Required 항목을 `VERIFIED`로 전환

완료 조건: `tools/verify-topdown.ps1`이 `TOPDOWN_ALL_PASSES_COMPLETE`를 출력.

---

# 2. Required 항목

## 2.0 PRD 대조표 — 분류가 맞는지 누구든 확인할 수 있게

### PRD §4.1 「반드시 구현」 21항목 → Required 매핑

| # | PRD §4.1 항목 | 백로그 ID |
|---|---|---|
| 1 | Unity LTS, URP 기반 Windows PC 프로토타입 | `UP-PLAT-01` |
| 2 | 1인칭 이동과 마우스 시점 조작 | `UP-SPACE-01`, `UP-SPACE-02` |
| 3 | 단일 화물 엘리베이터 내부와 제한된 외부 층 공간 | `UP-SPACE-04`, `UP-SPACE-05` |
| 4 | 10층 플레이 구조 | `UP-RUN-01`, `UP-RUN-02`, `UP-RUN-03` |
| 5 | 한 층 최대 5회 스핀 | `UP-POWER-04` |
| 6 | 세 통관이 각 3개 결과를 만드는 3×3 자동 룰렛 | `UP-CORE-02`, `UP-DEVICE-01`, `UP-DEVICE-02` |
| 7 | 정상 영혼 1종 | `UP-CORE-03` |
| 8 | 저항체 2종: 흡수체, 증식체 | `UP-CORE-04` |
| 9 | 계약 2종: 흡수체 계약, 증식체 계약 | `UP-CONTRACT-03` |
| 10 | 같은 저항체 3개 이상 기본 정화 | `UP-CORE-05` |
| 11 | 가로·세로·대각선 직선 3개 보너스 | `UP-CORE-06` |
| 12 | 4개 이상 직교 연결 제거와 캐스케이드 | `UP-CORE-07`, `UP-CORE-08`, `UP-CORE-09` |
| 13 | 잔류 효과 | `UP-CORE-10` |
| 14 | 전력, 요구 전력, 초과 전력 임계점 | `UP-POWER-01`, `UP-POWER-02`, `UP-POWER-08` |
| 15 | 전력 확정과 과수확 레버 | `UP-POWER-03`, `UP-POWER-05`, `UP-DEVICE-03` |
| 16 | 승객·부품 최소 4종 | `UP-BUILD-01`, `UP-BUILD-02` |
| 17 | 과적과 요구 전력 증가 | `UP-BUILD-03`, `UP-BUILD-04` |
| 18 | 감각적 위험 상태 시스템 | `UP-RISK-01` ~ `UP-RISK-09` |
| 19 | 승객 상황 반응 | `UP-NPC-01` ~ `UP-NPC-05` |
| 20 | 사고 기록기 | `UP-REC-01` ~ `UP-REC-05` |
| 21 | 디버그 패널, 결정론적 시드, 텔레메트리 | `UP-TEST-06`, `UP-CORE-01`·`UP-RUN-05`, `UP-TEST-05` |

**21항목 전부가 Required로 매핑돼 있다.** 빠진 것이 없다.

### PRD §4.2 「명시적 제외」 12항목 → Deferred 매핑

| # | PRD §4.2 항목 | 백로그 ID |
|---|---|---|
| 1 | 통관별 정지 버튼과 타이밍 정지 | `UP-DEF-01` |
| 2 | 구슬 위치 이동·교환 | `UP-DEF-02` |
| 3 | 연타·리듬·정밀 클릭 | `UP-DEF-03` |
| 4 | L·T·십자·고리 특수 패턴 | `UP-DEF-04` |
| 5 | 정상 영혼 9종 등급 체계 | `UP-DEF-05` (코드 잔재 정리는 `UP-TEST-11`) |
| 6 | 추가 저항체 | `UP-DEF-06`, `UP-DEF-07` |
| 7 | 완성형 멀티 엔딩 | `UP-DEF-11` |
| 8 | 완성형 대화·관계도 | `UP-DEF-12` |
| 9 | 온라인·멀티플레이·Twitch 연동 | `UP-DEF-15` |
| 10 | 최종 캐릭터 아트와 최종 애니메이션 | `UP-DEF-17` (프리셋 유지는 `UP-APV-01`·`UP-APV-02`) |
| 11 | 최종 수치 밸런스 | **`UP-APV-05`** — §14.2와 겹치므로 Approval Required로 둔다 |
| 12 | 장기 메타 진행과 세이브 슬롯 | `UP-DEF-16` |

> 11번만 Deferred가 아니라 Approval Required다. §4.2는 "최종 밸런스를 확정하지 말라"는
> 뜻이고 §14.2는 "승인 전 잠그지 말라"는 뜻이라 같은 요구다. **밸런스 작업을 아예 하지
> 않는 것이 아니라 교체 가능한 값으로 유지하는 것**이므로 Deferred(안 함)보다
> Approval Required(프리셋으로 진행)가 맞다.

### Notion에는 있으나 Required가 아닌 것

`UP-DEF-08` ~ `UP-DEF-14`, `UP-DEF-18` ~ `UP-DEF-21`이 여기 해당한다.
특수 숫자 층, 괴담, 몬스터, 세계관 반전, 상인·경제, 테마 전환, 3D 에셋 파이프라인은
전부 매력적이지만 **PRD §4.1에 없다.** 구현 대상으로 승격하려면 사용자 결정이 필요하다.

## 2.1 PLAT — 플랫폼·빌드·기반

### UP-PLAT-01 — Unity LTS + URP Windows PC 프로토타입
- 분류: Required · 출처: PRD §4.1, N08 §3.1
- 상태: VERIFIED · 패스: P1 P2
- 구현: `ProjectSettings/ProjectVersion.txt`, `Assets/Settings/`
- 접근: 프로젝트를 Unity 6000.5.5f1로 연다
- 검증: `Logs/build_report.txt`의 `unity:` 줄
- 증거: `Logs/build_report.txt`
- 의존: 없음
- 남은 문제: 없음

### UP-PLAT-02 — Unity Input System 기반 입력
- 분류: Required · 출처: N08 §3.1, PRD §4.1(1인칭 조작)
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Assets/Prototype_Elevator/Scripts/Player/FirstPersonController.cs`, `UI/DebugPanelView.cs`
- 접근: 플레이 모드에서 WASD·마우스·F1
- 검증: `Ascend/Run Self Tests`
- 증거: `Logs/editmode_tests.txt`
- 의존: 없음
- 남은 문제: 없음

### UP-PLAT-03 — 실행 가능한 Windows 빌드
- 분류: Required · 출처: PRD §17.6, §13.4
- 상태: VERIFIED · 패스: P1 P4
- 구현: `Assets/Editor/WindowsBuildTask.cs`
- 접근: 빌드 산출물 `Builds/Windows/Upandup_DDD.exe` 실행
- 검증: `WindowsBuildTask` 실행 → `Logs/build_report.txt`에 `result: Succeeded`
- 증거: `Logs/build_report.txt`
- 의존: UP-PLAT-01
- 남은 문제: 경고 506건. 빌드가 씬 결함(스크립트 없는 컴포넌트)을 드러낸 전례가 있다

### UP-PLAT-04 — TargetHardwareProfile 데이터화
- 분류: Required · 출처: PRD §13.1 「기준 하드웨어는 `TargetHardwareProfile`에 기록한다」
- 상태: CONNECTED · 패스: P2 P4
- 구현: `Scripts/Data/Profiles/TargetHardwareProfile.cs` + `.asset` + `Scripts/Perf/RenderBudgetProbe.cs` 의 `ApplyTargetProfile`
- 접근: 해당 없음 (개발 전용 데이터)
- 검증: `Logs/render_budget.txt` 머리글의 기준·측정 조건 대조
- 증거: `Logs/render_budget.txt`
- 의존: 없음
- 남은 문제: **에셋이 기준 PC 가 아니라 개발 기기를 담고 있다.** `TargetHardwareProfile.asset` 은 `Ryzen 5 5600X` 인데 `TECH_SPEC.md` §13 과 `A-20260730-01`·`A-20260731-01` 은 기준 PC 를 **Ryzen 7 5700** 으로 지정한다. 이 대체를 승인한 `DECISION_LOG` 항목이 없다. 성능 판정 전체(`UP-TECH-04/05/07`)가 이 값 위에 서므로 기록을 바로잡거나 「측정 기기를 잠정 기준으로 삼는다」를 명시적으로 올려야 한다 → `PD-16`. 그리고 배선이 끊겨도 실패하는 단정이 없다 — 렌더 프로브는 보고 줄만 붙인다

### UP-PLAT-05 — 압축·임포트 Preset과 VisualQualityProfile
- 분류: Required · 출처: PRD §13.3, §13.4, §17.4
- 상태: CONNECTED · 패스: P3
- 구현: `Scripts/Data/Profiles/VisualQualityProfile.cs`(광원 수·그림자 거리·파티클 상한·오버드로우 예산·렌더 스케일)
- 접근: 해당 없음
- 검증: `Ascend/Run All EditMode Tests` → 프리셋 값 대조
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-PLAT-03
- 남은 문제: **임포트 규칙이 생겼다 (2026-08-02). 「0건」은 더 이상 사실이 아니다.** `.preset` 파일을 만들지 **않았다** — 직렬화 에셋이라 이 저장소에서 조용히 깨지는 부류다. 대신 `Assets/Editor/AscendImportRules.cs`(`AssetPostprocessor`)가 경로로 카테고리를 판정하고 규칙 값을 `VisualQualityProfile._textureImportRules`·`AudioMixProfile._audioImportRules` 에서 **읽는다**(하드코딩 아님). 오디오 세 갈래가 **서로 다른 값**을 갖는다 — ShortEffect `DecompressOnLoad`/ADPCM/1.00/모노강제 · Loop `CompressedInMemory`/Vorbis/0.50 · Voice `Streaming`/Vorbis/0.70. 이름만 셋이고 값이 같아지는 회귀는 「세 갈래의 적재·압축이 서로 다르다」가 잡고, PRD §13.4 「무압축 원본을 런타임에 직접 쓰지 않는다」는 「PCM 이 어느 갈래의 기본값도 아니다」가 단정한다. **그래도 CONNECTED 로 올리지 않는다** — 관할 루트 아래 **텍스처 0개·오디오 0개**라 `OnPreprocessTexture`/`OnPreprocessAudio` 가 **한 번도 실행된 적이 없다.** 「규칙이 있다」와 「적용됐다」는 다르고, 그 구분을 흐리는 것이 이 저장소가 반복해서 당한 실패다. `Ascend/Report Import Rules` 가 `Logs/import_rules.txt` 에 **대상 개수를 함께** 적어 이 구분을 강제한다. **남은 것 둘**: ① 실제 텍스처·오디오 에셋이 들어오면 그때 적용이 관측된다 ② 빌드 리포트의 상위 용량 기록(PRD §13.4 마지막)은 미착수. **그리고 감사가 찾은 결함**: `VisualQualityProfile` High 의 `_shadowDistance: 30` 이 `PC_RPAsset.asset:57` 의 `m_ShadowDistance: 50` 과 **어긋난다** — 성능 리포트가 거짓 조건을 인용한다(§5.1 `UP-VIS-11`)
- **SKELETON → CONNECTED 근거 (2026-08-02 08:5x).** 위의 「남은 것 둘」 중 ①이 관측됐고,
  더불어 값의 **출처**가 폴백에서 에셋으로 넘어갔다. 둘 다 실측이다.
  - **규칙이 실제로 걸렸다.** `Art/Textures/` 에 텍스처 4장이 생겼고, 임포트된 값이
    `maxSize=1024 · mipmap 켬 · alphaIsTransparency 끔 · sRGB 켬` 이다.
    `maxSize` 가 **Unity 기본값 2048 이 아니라 1024** 라는 것이 판별자다 —
    `OnPreprocessTexture` 가 이 저장소 역사상 처음으로 실행됐다는 뜻이고,
    기본값과 우연히 같아서 통과하는 종류의 거짓 초록이 아니다.
    관할 아래 텍스처 **0개 → 6개**.
  - **출처가 폴백이 아니다.** `Logs/import_rules.txt` 가
    「텍스처 규칙 출처: **코드 프리셋** ← 에셋에 값이 없다」에서
    「텍스처 규칙 출처: **VisualQualityProfile**」로 바뀌었다.
    오디오도 「코드 프리셋」 → 「**AudioMixProfile**」.
    `VisualQualityProfile.Reset()` 이 `TextureImportRuleSet.Presets()` 를 에셋에 굳히는
    문서화된 경로이고, 그것을 Editor API 로 호출했다. 값은 그대로이므로 동작은 안 바뀌고
    **출처만** 바뀐다 — 이 항목이 요구한 것이 정확히 그 구분이다.
- 남은 것 (VERIFIED 를 막는다): ① 오디오 클립이 **아직 0개**라 `OnPreprocessAudio` 는
  여전히 미실행이다 ② 빌드 리포트의 상위 용량 기록 미착수
  ③ `UP-VIS-11`(그림자 거리 30 vs 50 불일치) 미해결

### UP-PLAT-06 — 결정론적 캡처 하네스
- 분류: Required · 출처: PRD §15.1 「동일한 해상도·FOV·카메라 위치·시간대·품질 프리셋」
- 상태: VERIFIED · 패스: P1 P3 P4
- 구현: `Assets/CaptureHarness/`, `Assets/Prototype_Elevator/Scripts/Run/Tests/TenFloorCaptureRig.cs`
- 접근: 해당 없음 (개발 도구)
- 검증: 하네스 실행 → `Captures/TenFloor/manifest.txt` 생성
- 증거: `Captures/TenFloor/manifest.txt`
- 의존: 없음
- 남은 문제: **없음.** 「17번만 해상도가 다르다」는 낡은 기록이었다 — PNG 헤더 실측으로 `17`·`19`·`20`·`01` 전부 **1920×1080** 확인 (2026-08-02). 방식 차이(화면 캡처)는 `ScreenSpaceOverlay` HUD 를 담기 위한 **요구**이지 결함이 아니다

### UP-PLAT-07 — 자체 헤드리스 테스트 러너
- 분류: Required · 출처: PRD §16.1, `D-20260730-06`
- 상태: VERIFIED · 패스: P1 P4
- 구현: `Assets/Editor/PrototypeSelfTest.cs`, `Assets/Editor/AscendTestMenu.cs`
- 접근: 해당 없음 (개발 도구)
- 검증: `Ascend/Run Self Tests` → `.claude/state/last-selftest.txt`
- 증거: `.claude/state/last-selftest.txt`, `Logs/editmode_tests.txt`
- 의존: 없음
- 남은 문제: 없음

## 2.2 SPACE — 1인칭 공간과 상호작용

### UP-SPACE-01 — 1인칭 이동(WASD)과 마우스 시점
- 분류: Required · 출처: PRD §4.1, N00 「시점과 조작 감각」, N08 §15.1
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Player/FirstPersonController.cs`
- 접근: 플레이 시작 직후 WASD·마우스
- 검증: `Logs/tenfloor_playmode.txt`의 씬 배선 검사
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-PLAT-02
- 남은 문제: 점프·달리기·웅크리기 없음은 N08 §15.1대로 의도된 것

### UP-SPACE-02 — 화면 중앙 레이캐스트 상호작용
- 분류: Required · 출처: PRD §4.1, N08 §15.2 (거리 2.5m, 클릭 1회, 길게 누르기 금지)
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Player/CrosshairInteractor.cs`, `IInteractable.cs`
- 접근: 조준점을 장치에 겨누고 좌클릭
- 검증: `Logs/tenfloor_playmode.txt`의 상호작용 단정
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-SPACE-01
- 남은 문제: 없음

### UP-SPACE-03 — 조준 대상 하이라이트와 행동 프롬프트
- 분류: Required · 출처: PRD §12.3 「어두운 상태에서도 발견 가능」, N08 §15.2
- 상태: VERIFIED · 패스: P2 P3
- 구현: `Scripts/Player/CrosshairInteractor.cs`(하이라이트 `_availableHighlight`) + `Scripts/Player/CrosshairView.cs`(프롬프트) + `TenFloorCaptureRig.AimPromptScreenShot`
- 접근: 조준점을 상호작용물에 올린다
- 검증: 고정 캡처 `20_aim_prompt_screen` — 조준 대상 획득 여부와 프롬프트 문구를 매니페스트에 함께 적는다
- 증거: `Captures/TenFloor/20_aim_prompt_screen.png`
- 의존: UP-SPACE-02
- 남은 문제: **독립 감사 통과.** 매니페스트 문구가 하드코딩이 아니라 `interactor.CurrentInteractable` 실측이고, 증거 그림에 녹색 레버·조준점·상단 라벨·하단 프롬프트가 함께 있다. 남은 부채는 해상도(816×714)이며 `UP-VIS-06` 쪽으로 넘긴다

### UP-SPACE-04 — 좁고 높은 산업용 화물 엘리베이터 내부
- 분류: Required · 출처: PRD §4.1, §12.2, N06 §8 「엘리베이터 설계 고정 조건」
- 상태: VERIFIED · 패스: P1 P2 P3
- 구현: `Scripts/View/ElevatorGrayboxView.cs`, `Assets/Editor/GrayboxWorldBuilder.cs`, 씬 `Car` 서브트리
- 접근: 플레이 시작 위치
- 검증: 고정 캡처 `01_entry`
- 증거: `Captures/TenFloor/01_entry.png`
- 의존: 없음
- 남은 문제: **독립 시각 평가 최우선 지적** — `01_entry`가 공간의 높이를 보여주지 못한다 (높이 프레임 0장)

### UP-SPACE-05 — 문 밖의 제한된 층 공간
- 분류: Required · 출처: PRD §4.1, §12.2 「문 밖은 짧고 어두운 공간」, N03 「일반 층의 역할」
- 상태: VERIFIED · 패스: P1 P2
- 구현: 씬 `CarShell_LobbyBack`/`CarShell_LobbyFloor`, `Scripts/Build/BuildFigureView.cs`(LobbySlots)
- 접근: 적재 층에서 문을 열면 승강장이 보인다
- 검증: `Logs/tenfloor_playmode.txt` 「승강장에 후보가 서 있다」
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-SPACE-07
- 남은 문제: 승강장은 배경에 가깝고 플레이어가 걸어 나가는 탐색 공간은 아니다

### UP-SPACE-06 — 최대 적재 상태에서도 동선과 장치 접근 유지
- 분류: Required · 출처: PRD §12.2, N03 「1인칭 공간 설계 원칙」
- 상태: CONNECTED · 패스: P2 P3
- 구현: `Scripts/Build/BuildFigureView.cs` 배치 슬롯
- 접근: 적재를 최대로 채운 뒤 레버·계기판·문에 접근
- 검증: 고정 캡처 `07_cargo_full`, `08_passenger_and_device`
- 증거: `Captures/TenFloor/07_cargo_full.png`, `Captures/TenFloor/08_passenger_and_device.png`
- 의존: UP-BUILD-05
- 남은 문제: 증거가 정지 이미지 2장뿐인데 요구는 기능적이다(「이동과 장치 접근 가능」). `08_passenger_and_device.png` 에 **승객이 보이지 않는다** — 매니페스트는 「승객과 장치가 한 화면에」라고 적었다. 통로 폭·플레이어 통과를 재는 단정이 0건이고, 실플레이 최대 적재는 108/100kg 인데 캡처는 78kg 이다

### UP-SPACE-07 — 문 개폐 조작과 적재 단계 종료
- 분류: Required · 출처: PRD §5(1~4), N02 「승차 흐름」
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Player/InteractableDoorControl.cs`
- 접근: 적재 층에서 문 손잡이 클릭
- 검증: `Logs/tenfloor_playmode.txt` 「문을 닫으면 적재가 끝난다」
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-SPACE-02
- 남은 문제: 없음

### UP-SPACE-08 — 판정 진행 중에도 이동·시점 회전 허용
- 분류: Required · 출처: PRD §17.1(진행 불가 상태 없음), N01 「자동 연쇄가 진행되는 동안 이동과 시점 회전은 허용」, N08 §17
- 상태: CONNECTED · 패스: P2 P3
- 구현: `Scripts/View/SpinPresenter.cs`(연출 잠금은 입력만 잠근다), `FirstPersonController.cs`
- 접근: 레버를 당긴 직후 이동해 본다
- 검증: `TenFloorAutoPilot` 의 「연출 중에도 플레이어 조작이 살아 있다」 — 268회
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-SPACE-01, UP-CORE-11
- 남은 문제: **내 단정이 공허하게 참이었다 — 독립 감사가 짚었고 고쳤다.** 직전 판본은 `root.Rotate(25°)` 로 직접 쓰고 다음 프레임에 읽는 것뿐이었는데, 루트 회전을 되돌릴 주체가 **존재하지 않는다** — `HandleLook` 은 커서 잠금을 요구하고 씬은 `_lockCursorOnStart: 0` 이다. 그래서 268회 전부 정확히 `25.0°` 였다. 실측이 아니라 **항등식**이다. 게다가 그 커밋 메시지는 「컨트롤러를 끄든 `timeScale` 을 0 으로 두든 여기서 걸린다」고 적었는데 **둘 다 걸리지 않는다** — `Transform.Rotate` 와 `CharacterController.Move` 는 시간 배율과 무관하고, 검사 대상 `_character` 는 `CharacterController` 이지 `FirstPersonController` 가 아니다. **즉 반증력이 있던 옛 검사(`enabled && activeInHierarchy && timeScale > 0`)를 반증력이 없는 것으로 갈아 끼우고 반대로 적었다.** → 떨어질 수 있는 조건 둘(조작 컴포넌트 활성 · `timeScale > 0`)을 되살려 함께 걸었고, 결과 측정 두 건은 「연출이 되돌리지 않는다」로 이름을 바꿔 **충족 근거로 단독 인용하지 말라고 코드에 적었다.** 남은 것: `FirstPersonController.SetCursorLocked(true)`(public)로 게이트를 열어 실제 look 경로를 태우는 것 — 코드 주석의 「하네스가 흉내 낼 수 없다」는 과장이었다

### UP-SPACE-09 — 등을 돌려도 결과와 전력 변화를 알 수 있다
- 분류: Required · 출처: PRD §11(무음 관전자 기준), N03 「등을 돌려도 사운드·점등·보조 UI로」, N08 §17
- 상태: CONNECTED · 패스: P2 P3
- 구현: `Scripts/UI/GameHudView.cs`(화면 HUD — 연쇄 단계와 힌트뿐) · `Scripts/View/InstrumentPanelView.cs`(월드 공간)
- 접근: 장치를 등지고 선 채 레버 결과를 기다린다
- 검증: 장치를 등진 화면 캡처에서 전력·스핀이 읽히는지 — **읽히지 않는다**
- 증거: `Captures/TenFloor/23_back_turned_screen.png` (22 와 같은 잠금 구간, 시점만 180°)
- 의존: UP-AUD-02, UP-DEVICE-06
- 남은 문제: **증거를 만들었고, 그 증거가 미충족을 증명한다.** 22 와 **같은 스핀·같은 잠금 구간**에서 시점만 180° 돌려 찍었다(`23_back_turned_screen`). 결과: 화면에 남는 것은 **「연쇄 0단계」 한 줄뿐**이고 나머지는 어두운 벽이다. 22 에서 읽히던 「스핀 4/5 · 판돈 194 · 흡수체 2개 → 저장 전력 −16.0」이 **전부 사라졌다.** **구조적 원인을 특정했다** — `InstrumentPanelView` 의 층·전력·상태·계약 라벨은 전부 `TextMeshPro`(**월드 공간**)이고, `GameHudView` 가 화면 공간(`TextMeshProUGUI`)으로 띄우는 것은 **힌트와 연쇄 단계 둘뿐**이다. 즉 등을 돌리면 전력과 스핀은 원리적으로 화면에서 사라진다. 「사운드 채널이 거의 없어 HUD 하나에 의존한다」는 이전 서술은 **절반만 맞았다** — 그 HUD 조차 대부분이 월드 공간이라 시선을 따라간다. 요구 채널 셋(사운드·점등·보조 UI) 중 **보조 UI 가 사실상 없다.** 사운드는 14종이 실제로 울리므로(`Logs/tenfloor_playmode.txt` 종류 이름) 그쪽은 살아 있으나 **정지 화면으로 판정할 수 없다.** **다음**: 전력·스핀을 화면 공간 HUD 에 올리거나(단 `UP-CORE-13` 「한 화면에 모든 숫자를 띄우지 않는다」와 충돌 검토 필요), 등진 방향 벽에 보조 점등을 둔다

## 2.3 DEVICE — 수확 장치와 물리적 계기

### UP-DEVICE-01 — 투명 수직 통관 3개와 3×3 결과판 대응
- 분류: Required · 출처: PRD §4.1, N01 「초기 장치 구조」, N06 §13
- 상태: VERIFIED · 패스: P1 P2 P3
- 구현: `Scripts/View/SpinBoardView.cs`, 씬 `RouletteMachine` 계열
- 접근: 장치 정면에 선다
- 검증: 고정 캡처 `02_device_front`
- 증거: `Captures/TenFloor/02_device_front.png`
- 의존: UP-SPACE-04
- 남은 문제: 없음

### UP-DEVICE-02 — 공통 실행 레버 1개 (통관별 정지 버튼 없음)
- 분류: Required · 출처: PRD §4.1, §4.2(통관별 정지 버튼 제외), `D-20260730-08`
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Player/InteractableLever.cs`, 씬 `ExecutionLever`
- 접근: 레버에 다가가 클릭
- 검증: `Logs/tenfloor_playmode.txt` 「레버가 계약을 확정한다」
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-SPACE-02
- 남은 문제: 없음

### UP-DEVICE-03 — 과수확 레버 (실행 레버와 물리적으로 구분)
- 분류: Required · 출처: PRD §4.1, §7.2, `D-20260730-08`
- 상태: VERIFIED · 패스: P1 P2 P3
- 구현: `Scripts/Player/InteractableOverharvestLever.cs`, 씬 `Housing`/`CoverPlate`/`HandlePivot`
- 접근: 요구 전력 달성 후 잠금 해제된 레버에 접근
- 검증: 고정 캡처 `11_overharvest_locked`, `12_overharvest_unlocked`, `13_overharvest_pulled`
- 증거: `Captures/TenFloor/11_overharvest_locked.png`, `Captures/TenFloor/12_overharvest_unlocked.png`, `Captures/TenFloor/13_overharvest_pulled.png`
- 의존: UP-POWER-05
- 남은 문제: **독립 평가 지적** — 레버가 하우징 안 + 카메라 반대 방향이라 "당김"이 형상으로 전달되지 않는다

### UP-DEVICE-04 — 과수확 잠금 구조가 물리적으로 이해된다
- 분류: Required · 출처: PRD §7.2 「기본 상태에서는 잠겨 있거나 보호 덮개가 닫혀 있다」
- 상태: VERIFIED · 패스: P2 P3
- 구현: `Scripts/View/OverharvestUnlockEffect.cs`, 씬 `CoverPivot`/`CoverRib`
- 접근: 요구 전력 미달 상태에서 레버를 본다 → 달성 후 다시 본다
- 검증: 고정 캡처 11 vs 12 대비
- 증거: `Captures/TenFloor/11_overharvest_locked.png`, `Captures/TenFloor/12_overharvest_unlocked.png`
- 의존: UP-DEVICE-03
- 남은 문제: 없음

### UP-DEVICE-05 — 투명 전력 탱크 (현재·요구·임계점이 실제로 차오름)
- 분류: Required · 출처: PRD §4.1, N01 「초기 장치 구조」, N06 §13 「필수 파츠」
- 상태: VERIFIED · 패스: P1 P2 P3
- 구현: `Scripts/Player/InteractablePowerTank.cs`, `Scripts/View/InstrumentPanelView.cs`, 씬 `CarShell_TankStand`
- 접근: 탱크에 다가가 클릭하면 전력을 확정한다
- 검증: 캡처 매니페스트의 「게이지 실측」 줄
- 증거: `Captures/TenFloor/manifest.txt`
- 의존: UP-POWER-01
- 남은 문제: 임계점 눈금에 숫자 라벨이 없다 (**3세션째 열려 있음** — 계기판 배치 결정 필요)

### UP-DEVICE-06 — 기계식 계기판 (계약 저항·잔류 오염 표시)
- 분류: Required · 출처: PRD §4.1(잔류 효과), N01 「오염 계기판」, N06 §13
- 상태: CONNECTED · 패스: P1 P2 P3
- 구현: `Scripts/View/InstrumentPanelView.cs`
- 접근: 계기판 앞에 선다
- 검증: 고정 캡처 `02_device_front`, `14_contract_select`
- 증거: `Captures/TenFloor/14_contract_select.png`
- 의존: UP-CONTRACT-01
- 남은 문제: **계기판의 계약 표시는 죽은 경로다.** 씬 `Prototype_Elevator.unity:17722` 가 `_contractLabel: {fileID: 0}` 이고 `InstrumentPanelView.cs:199` 가 `if (_contractLabel == null) return;` 로 즉시 반환한다 — **`ApplyContractPreview` 가 한 번도 실행되지 않는다.** 계약 문구는 `_plaqueLabels`(계약 패널 = `UP-DEVICE-07` 의 물건)에만 뜬다. 즉 이 항목 제목의 「계약 저항」 절반이 계기판에서는 미구현이다. **증거도 틀렸다** — 걸려 있는 `14_contract_select.png` 에는 **계기판이 프레임에 없다**(계약 명판 3장·과수확 라벨·층수 표시뿐). 계기판이 실제로 읽히는 장은 `03_device_side.png`(「스핀 5/5 · 잔류 없음」)와 `08_passenger_and_device.png`(「흡수체 1개 → 저장 전력 −8.0」 = 잔류 오염 실측)인데 **둘 다 증거로 걸려 있지 않다.** 게다가 `14_contract_select` 는 시각 판정에서 **판독성 2/5 세트 최저점**이라 `UP-FIX-09`(재설계)로 이미 전환됐다. **다음**: ① `_contractLabel` 을 씬에서 배선하거나 「계기판은 잔류만 표시한다」로 요구를 줄이는 결정 ② 증거를 `08_passenger_and_device.png` 로 교체. ①을 배선하면 `[RequiredReference]` 를 붙여 재발을 막는다 — 지금 붙이면 `SceneWiringValidator` 가 즉시 결함으로 잡는다(그것이 이 항목의 실상이다)

### UP-DEVICE-07 — 계약 패널 (물리적 인터페이스)
- 분류: Required · 출처: PRD §4.1, N01 「계약은 벽면 계약 패널, 인쇄된 계약서, 봉인된 표식 등」
- 상태: VERIFIED · 패스: P1 P2 P3
- 구현: `Scripts/Player/InteractableContractPanel.cs`, 씬 `ContractPanel`/`ContractPlaque_0..2`
- 접근: 층 시작 후 계약 패널 클릭
- 검증: 고정 캡처 `14_contract_select`
- 증거: `Captures/TenFloor/14_contract_select.png`
- 의존: UP-SPACE-02
- 남은 문제: 좌측 벽 라벨이 거울상으로 렌더된다

### UP-DEVICE-08 — 출입구 위 현재 층수 표시
- 분류: Required · 출처: N06 §8 「층수 표시기는 출입구 위 또는 옆」, PRD §12.2(실루엣 구분)
- 상태: VERIFIED · 패스: P1 P2 P3
- 구현: `Scripts/View/FloorIndicatorView.cs`, 씬 `FloorIndicator`/`DoorSign`
- 접근: 문을 바라본다
- 검증: `Logs/tenfloor_playmode.txt` 「층수 표시등이 있다」
- 증거: `Logs/tenfloor_playmode.txt`, `Captures/TenFloor/18_final_floor.png`
- 의존: UP-RUN-01
- 남은 문제: 없음

### UP-DEVICE-09 — 세 심볼을 색 없이 실루엣으로 구분
- 분류: Required · 출처: PRD §15.2, N07 「색만으로 구분하지 않고 실루엣·코어·표면 문양·움직임」
- 상태: CONNECTED · 패스: P2 P3
- 구현: `Scripts/View/SpinBoardView.cs`, `PurifyMarkerView.cs`
- 접근: 결과판을 1초 본다
- 검증: 고정 캡처 `04_symbols`
- 증거: `Captures/TenFloor/04_symbols.png`
- 의존: UP-DEVICE-01
- 남은 문제: **제목이 요구를 1/3 로 줄여 적고 있다 — 충족처럼 보이는 것이 가장 위험하다.** `VISUAL_SPEC.md` §4 와 `SYMBOL_DESIGN_SPEC.md` §4 는 「색상 하나에만 의존하지 않고 **6축 중 최소 세 가지**가 달라야 한다」를 요구하고, `SYMBOL_DESIGN_SPEC.md:112-115` 가 그 3축의 구체 형상까지 확정해 뒀다(①매끈/오목/볼록 ②코어 1개/공백/다수 ④느린 회전/미세 진동/간헐 팽창 ⑤균일/방사형/불규칙). **구현은 ① 실루엣 1축뿐이다** — `HumanScaleLayout.cs:377-379` 의 Sphere/Cube/Capsule 이고 `MakeSymbol`(:403-421)은 **머티리얼조차 배정하지 않아** 셋이 같은 기본 머티리얼이다. ②·⑤ 없음, ④ 심볼별 움직임 없음, ⑥ 심볼별 공개 사운드 없음(`ColumnReveal` 한 종류). `SYMBOL_DESIGN_SPEC.md:150` 이 스스로 「실제 형상이 이 사양을 따르는지 미검증」이라 적었고 감사가 확인한 결과 **따르지 않는다.** 남은 것: 3축 중 2축 추가 + 흑백 변환 대조 검사

### UP-DEVICE-10 — 장치가 평면 UI가 아니라 물리적 조작부를 가진다
- 분류: Required · 출처: PRD §12.2, N06 §8 「손잡이, 스위치, 봉인, 계기 바늘」, N08 §17
- 상태: CONNECTED · 패스: P3
- 구현: 씬 `Handle`/`HandleGrip`/`HandleShaft`/`ExecutionPlate`/`ConsoleSlab`
- 접근: 각 장치를 가까이서 본다
- 검증: 고정 캡처 `03_device_side`
- 증거: `Captures/TenFloor/03_device_side.png`
- 의존: UP-DEVICE-02
- 남은 문제: 없음

## 2.4 CORE — 룰렛 판정 코어

### UP-CORE-01 — 시드 기반 결정론적 RNG
- 분류: Required · 출처: PRD §4.1, §13.5, N08 §7.3
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Spin/SpinSeed.cs`, `SpinEngine.cs`
- 접근: 디버그 패널 `[T]`로 시드 입력 후 `[R]` 재시작
- 검증: `Ascend/Run Self Tests` → 「같은 시드 → 완전 동일」
- 증거: `Logs/editmode_tests.txt`
- 의존: 없음
- 남은 문제: 없음

### UP-CORE-02 — 3열 × 3행 보드와 가중 추첨
- 분류: Required · 출처: PRD §4.1, N08 §7.1, §7.2
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Spin/SpinBoard.cs`, `SpinEngine.cs`, `SpinRuleSet.cs`
- 접근: 실행 레버를 당긴다
- 검증: `Ascend/Run Self Tests`
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-CORE-01
- 남은 문제: 없음

### UP-CORE-03 — 정상 영혼 1종과 기본 전력
- 분류: Required · 출처: PRD §4.1, N07 「정상 영혼」, N08 §6.2
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Spin/SymbolKind.cs`, `SpinRuleSet.cs`
- 접근: 스핀 결과의 정상 영혼이 전력이 된다
- 검증: `Ascend/Run Self Tests`
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-CORE-02
- 남은 문제: 없음

### UP-CORE-04 — 저항체 2종 (흡수체·증식체)
- 분류: Required · 출처: PRD §4.1, N07, N08 §6.2
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Spin/SymbolKind.cs`
- 접근: 4층 이후 결과판에 등장
- 검증: `Ascend/Run Self Tests`
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-CORE-02
- 남은 문제: 없음

### UP-CORE-05 — 같은 저항체 3개 이상 기본 정화 (인접 요구)
- 분류: Required · 출처: PRD §6.1, `D-20260801-03` (`A-20260731-07` 승격)
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Spin/SpinEngine.cs`
- 접근: 같은 저항 3개가 붙어 나오면 정화된다
- 검증: `Ascend/Run Self Tests` → 「정화된 칸은 서로 붙어 있다」, 「붙어 있으면 모양을 가리지 않는다」
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-CORE-04
- 남은 문제: **PRD 원문(§6.1)은 "위치와 무관하게"이고 구현은 인접을 요구한다.** 사용자 지시로 `D-20260801-03`이 이를 승격했고 저장소 `docs/MASTER_PRD.md`는 개정됐으나 **Notion 원본은 아직 옛 문장이다** → UP-DOC-01

### UP-CORE-06 — 가로·세로·대각선 직선 3개 보너스
- 분류: Required · 출처: PRD §4.1, §6.1, N08 §8.3
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Spin/SpinEngine.cs`, `PatternKind.cs`
- 접근: 저항 3개가 한 줄로 서면 배수가 붙는다
- 검증: `Ascend/Run Self Tests` → 「직선 3종 → LineKind」
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-CORE-05
- 남은 문제: 없음

### UP-CORE-07 — 4개 이상 직교 연결 판정 (대각선 제외)
- 분류: Required · 출처: PRD §4.1, N01 「인접 판정 원칙」, N08 §8.4
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Spin/SpinEngine.cs`
- 접근: 저항이 상하좌우로 4칸 이어지면 덩어리가 무너진다
- 검증: `Ascend/Run Self Tests` → 「직교 연결 4개 → Cluster와 재충전」, 「대각 연결 규칙」
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-CORE-05
- 남은 문제: 없음

### UP-CORE-08 — 제거 → 빈칸 → 신규 유입 → 재판정 (생략 금지)
- 분류: Required · 출처: PRD §6.1 「시각적으로 생략하지 않는다」, N07 「캐스케이드 시각 규칙」
- 상태: VERIFIED · 패스: P1 P2 P3
- 구현: `Scripts/Spin/SpinEngine.cs`, `Scripts/View/SpinPresenter.cs`
- 접근: 캐스케이드가 터지는 것을 본다
- 검증: 고정 캡처 `15_cascade_deep`
- 증거: `Captures/TenFloor/15_cascade_deep.png`, `Logs/editmode_tests.txt`
- 의존: UP-CORE-07
- 남은 문제: 없음

### UP-CORE-09 — 캐스케이드 하드 캡 20회와 안전 종료
- 분류: Required · 출처: PRD §6.1, N08 §9.3
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Spin/SpinEngine.cs`, `SpinRuleSet.cs`
- 접근: 해당 없음 (극단 상황)
- 검증: `Ascend/Run Self Tests` → 「MaxCascadeDepth 상한」
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-CORE-07
- 남은 문제: 없음

### UP-CORE-10 — 잔류 효과 (흡수체 전력 감소 / 증식체 가중치 증가)
- 분류: Required · 출처: PRD §4.1, N01 「잔류 저항」, N08 §6.2
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Spin/SpinEngine.cs`, `SpinResolution.cs`, `Run/FloorSession.cs`
- 접근: 정화하지 못한 저항이 다음 스핀·전력에 남는다
- 검증: `Ascend/Run Self Tests` → 「흡수체 잔류 → NetPower 차감」
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-CORE-05
- 남은 문제: 없음

### UP-CORE-11 — 결과 공개 순차 연출 (동시 표시 금지)
- 분류: Required · 출처: PRD §6.2, N08 §16.1 (총 1.5~2.5초 범위 조절 가능)
- 상태: CONNECTED · 패스: P2 P3
- 구현: `Scripts/View/SpinPresenter.cs`
- 접근: 레버를 당기면 열 단위로 공개된다
- 검증: GIF f0~f6 1열 · f8~f12 1+2열 · f14~ 3열 — 순차 공개가 필름에 있다
- 증거: `Captures/evidence/cascade_depth5_seed4242_f3.gif` (실제 레버 경로 142프레임)
- 의존: UP-CORE-02
- 남은 문제: **증거가 그 코드의 산물이 아니다 — `UP-TECH-03` 과 같은 실패다.** 백로그가 건 `15_cascade_deep.png` 는 `SpinPresenter` 를 **거치지 않은** 그림이다: `TenFloorCaptureRig.cs:565` 가 `run.Spin()` 을 직접 부르고 :577·:587 이 판과 표식을 손으로 밀어 넣는다. 리그 자신의 주석(:402-404)이 「재생을 거치지 않고 판을 직접 밀어 넣었을 때 `SpinPresenter` 가 하던 일을 **대신한다**」고 적어 두었다. 강조 세기도 :419 에서 0.46/0.22/0.34/0.16 으로 손으로 넣어 실제 연출(사인파 최대 1.0)과 값이 다르다. **진짜 증거는 이미 저장소에 있다** — `Captures/evidence/cascade_depth5_seed4242_f3.gif` 142프레임에 순차 공개가 찍혀 있다(f0~f6 1열 · f8~f12 1+2열 · f14~ 3열). `EvidenceClipRecorder.cs:209-213` 이 `lever.Interact()` 로 실제 경로를 돌린 필름이다. 증거를 그 GIF 로 옮기고, `SYMBOL_DESIGN_SPEC.md` §7 「총 공개 1.5~2.5초」를 재는 검사를 붙여야 한다 — 씬 Standard 값 계산은 0.32×3+0.45 = **1.41초**로 하한 밖인데 재는 코드가 0건이다

### UP-CORE-12 — 판정 원인 시각화 (정화·직선·연결 점등)
- 분류: Required · 출처: PRD §6.2, §15.2, N07 「패턴 시각화」, N08 §16.2
- 상태: CONNECTED · 패스: P2 P3
- 구현: `Scripts/View/PurifyMarkerView.cs`, `SpinPresenter.cs`
- 접근: 정화가 일어나는 순간 결과판을 본다
- 검증: GIF f30 「연결 정화 4칸」+ㄱ자 표식 · f118 「직선 3칸」+대각 막대 — 형상으로 갈린다
- 증거: `Captures/evidence/cascade_depth5_seed4242_f3.gif` (실제 레버 경로 142프레임)
- 의존: UP-CORE-08
- 남은 문제: **증거가 그 코드의 산물이 아니다 — `UP-TECH-03` 과 같은 실패다.** 백로그가 건 `15_cascade_deep.png` 는 `SpinPresenter` 를 **거치지 않은** 그림이다: `TenFloorCaptureRig.cs:565` 가 `run.Spin()` 을 직접 부르고 :577·:587 이 판과 표식을 손으로 밀어 넣는다. 리그 자신의 주석(:402-404)이 「재생을 거치지 않고 판을 직접 밀어 넣었을 때 `SpinPresenter` 가 하던 일을 **대신한다**」고 적어 두었다. 강조 세기도 :419 에서 0.46/0.22/0.34/0.16 으로 손으로 넣어 실제 연출(사인파 최대 1.0)과 값이 다르다. **진짜 증거도 같은 GIF 에 있다** — f30 「흡수체 연결 정화 4칸 ×3」+ㄱ자 표식, f62 8칸, f86 지그재그, f118 「흡수체 직선 3칸 ×2」+대각 막대. 연결과 직선이 형상으로 갈린다. 남은 것: 「개수 정화(Scattered)」가 필름에 한 번도 없어 3종 대비가 2/3 이다

### UP-CORE-13 — 한 화면에 모든 숫자를 띄우지 않는다
- 분류: Required · 출처: PRD §6.2 마지막 문단
- 상태: CONNECTED · 패스: P3
- 구현: `Scripts/UI/GameHudView.cs`, `Scripts/View/SpinPresenter.cs`
- 접근: 깊은 캐스케이드 중 화면을 본다
- 검증: 레버를 실제로 당겨 `SpinPresenter` 가 도는 중에 찍은 화면 캡처
- 증거: `Captures/TenFloor/22_presenting_screen.png` (촬영 순간 연출 잠금 True)
- 의존: UP-CORE-12
- 남은 문제: **판정 대상이 처음으로 그림에 들어왔다.** 이전 증거 `19_cascade_deep_screen` 은 리그가 `run.Spin()` 을 직접 불러 `SpinPresenter` 를 거치지 않았고, 그래서 `IsPresenting` 이 영원히 false 라 **연출 중 화면이 한 번도 찍히지 않았다** — 감사자가 「하단 힌트가 또렷하다」는 것을 근거로 이 사실을 짚었다(`GameHudView.cs:152` 는 연출 중 힌트를 0 으로 페이드한다). 새 장 `22_presenting_screen` 은 **레버를 실제로 당기고** 잠금이 걸린 동안 찍는다. 확인된 것 셋 — ① 촬영 순간 `bridge.IsLocked == True` ② **「연쇄 0단계」 HUD 가 있다**(19 에 구조적으로 빠져 있던 바로 그 요소) ③ **하단 힌트가 사라졌다**(페이드 경로가 실제로 돌았다). **다만 충족 여부는 내가 판정하지 않는다.** 읽히는 것은 「연쇄 **0**단계」라 공개 초입이고 깊은 연쇄가 아니며, 좌측 1/3이 어두운 벽이라 구도가 낭비된다. 「한 화면에 모든 숫자를 띄우지 않는다」의 통과 여부는 **독립 평가가 정할 일**이다

### UP-CORE-14 — 가중치 합이 0이면 명시적 오류
- 분류: Required · 출처: N08 §7.2 마지막 문장, PRD §13.5(조용한 실패 금지)
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Spin/SpinEngine.cs`(방어), `Scripts/Spin/Tests/SpinRuleSetTests.cs`(검증)
- 접근: 해당 없음
- 검증: `Ascend/Run All EditMode Tests` → 「가중치 전부 0 → InvalidOperationException」
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-CORE-02
- 남은 문제: 없음

## 2.5 CONTRACT — 저항 계약

### UP-CONTRACT-01 — 층 시작 계약 선택 (첫 스핀 전, 변경 불가)
- 분류: Required · 출처: PRD §4.1, §5(3), N08 §10.1
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Spin/ResistanceContract.cs`, `Run/FloorSession.cs`, `Player/InteractableContractPanel.cs`
- 접근: 4층 이후 층 시작 시 계약 패널
- 검증: `Logs/tenfloor_playmode.txt` 「계약 전 탱크 비활성」
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-DEVICE-07
- 남은 문제: 없음

### UP-CONTRACT-02 — 계약 미선택 시 레버 비활성
- 분류: Required · 출처: N08 §10.1, §19.2, PRD §17.1
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Run/RouletteInteractionBridge.cs`, `Player/InteractableLever.cs`
- 접근: 계약 층에서 계약 없이 레버를 눌러 본다
- 검증: `Logs/tenfloor_playmode.txt` 「계약 전 탱크 비활성」/「레버가 계약을 확정한다」
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-CONTRACT-01
- 남은 문제: 없음

### UP-CONTRACT-03 — 계약 2종 (흡수체 계약 / 증식체 계약)
- 분류: Required · 출처: PRD §4.1, N08 §10.2, §10.3
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Spin/ResistanceContract.cs`
- 접근: 7층에서 두 계약이 나란히 제시된다
- 검증: `Ascend/Run Self Tests` → 「계약을 실제로 건 런도 10층을 완주한다」
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-CONTRACT-01
- 남은 문제: 없음

### UP-CONTRACT-04 — 계약이 출현률·정화 보상·잔류 대가를 함께 바꾼다
- 분류: Required · 출처: PRD §4.1, N01 「층 시작 — 저항 계약」, N08 §10.2/§10.3
- 상태: VERIFIED · 패스: P2
- 구현: `Scripts/Spin/SpinRuleSet.cs`, `ResistanceContract.cs`
- 접근: 계약을 건 층과 안 건 층의 결과판 밀도를 비교
- 검증: `Ascend/Run Self Tests`
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-CONTRACT-03
- 남은 문제: 없음

### UP-CONTRACT-05 — 계약 선택 전 등장률·보상·대가를 함께 공개
- 분류: Required · 출처: PRD §6.2(판독), N01 「공정성 안전장치」, N03 「위험 계약은 선택 전에 공개」, N08 §10.4
- 상태: CONNECTED · 패스: P2 P3
- 구현: `Scripts/Player/InteractableContractPanel.cs`, 씬 `ContractPlaqueLabel_0..2`
- 접근: 계약 패널을 조준한다
- 검증: 고정 캡처 `14_contract_select`
- 증거: `Captures/TenFloor/14_contract_select.png`
- 의존: UP-CONTRACT-01
- 남은 문제: N08 §10.4의 「현재 빌드와 관련된 시너지 한 줄」이 없다

## 2.6 POWER — 전력·임계점·푸시 유어 럭

### UP-POWER-01 — 전력·요구 전력·초과 전력
- 분류: Required · 출처: PRD §4.1, N08 §11.1, §11.2
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Core/FloorMath.cs`, `Run/FloorSession.cs`, `Spin/PowerThresholds.cs`
- 접근: 계기판의 현재/요구 전력
- 검증: `Ascend/Run Self Tests`
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-CORE-03
- 남은 문제: 없음

### UP-POWER-02 — 전력 임계점 구간 8종
- 분류: Required · 출처: PRD §4.1, N01 「전력 임계점」, N03 「부분 실패」, N08 §11.3
- 상태: CONNECTED · 패스: P1 P2
- 구현: `Scripts/Spin/PowerThresholds.cs` (`PowerBand`)
- 접근: 전력 비율에 따라 상승 결과가 갈린다
- 검증: `Ascend/Run Self Tests`, `Logs/tenfloor_playmode.txt`
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-POWER-01
- 남은 문제: 8구간이 `PowerThresholds` 에 있고 `PowerBand.Damaged` → `AscendResult.DeviceDamaged` → `FloorResult.DeviceDamaged` 까지 흐른 뒤 **거기서 끝난다.** `DeviceDamaged` 를 읽는 코드가 0곳이라 장치 손상 구간이 게임 안에서 아무 일도 하지 않는다(`UP-APV-10` 승인 대기)

### UP-POWER-03 — 전력 확정 (브레이크)
- 분류: Required · 출처: PRD §4.1, §5(14), N08 §12.2
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Player/InteractablePowerTank.cs`, `Run/FloorSession.cs`
- 접근: 요구 전력 달성 후 전력 탱크 클릭
- 검증: `Logs/tenfloor_playmode.txt` 「탱크로 층을 끝낼 수 있다」
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-DEVICE-05
- 남은 문제: 없음

### UP-POWER-04 — 한 층 최대 5회 스핀
- 분류: Required · 출처: PRD §4.1, N01 「프로토타입 고정안」, N99 「테스트용 고정 조건」
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Spin/FloorPlan.cs`(`Spins`), `Run/FloorSession.cs`
- 접근: 층마다 남은 스핀이 줄어든다
- 검증: `Logs/tenfloor_playmode.txt` 「남은스핀」 기록
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-RUN-02
- 남은 문제: 없음

### UP-POWER-05 — 과수확 해금 조건과 경고 연출
- 분류: Required · 출처: PRD §7.2 「100% 달성 시 잠금이 풀리고 짧은 경고 연출」
- 상태: VERIFIED · 패스: P2 P3
- 구현: `Scripts/View/OverharvestUnlockEffect.cs`, `Run/FloorSession.cs`
- 접근: 요구 전력 100%를 넘긴 순간
- 검증: 고정 캡처 `12_overharvest_unlocked`
- 증거: `Captures/TenFloor/12_overharvest_unlocked.png`
- 의존: UP-POWER-02
- 남은 문제: 없음

### UP-POWER-06 — 과수확 상호작용 연출 5단계
- 분류: Required · 출처: PRD §7.3 (접근 → 감음 → 승객 시선 → 0.3~0.7초 정적 → 재개)
- 상태: CONNECTED · 패스: P2 P3
- 구현: `Scripts/Run/OverharvestApproachBridge.cs`(접근 판정), `Scripts/Audio/SilenceWindow.cs`(정적), `Scripts/Npc/`(승객 응시 반응)
- 접근: 과수확 레버에 손을 올린다
- 검증: 접근 순간 고정 캡처 + 정적 구간 측정
- 증거: `Captures/TenFloor/12_overharvest_unlocked.png`
- 의존: UP-DEVICE-03, UP-NPC-02, UP-AUD-03
- 남은 문제: **5단계(재개)를 만들고 1단계가 왜 한 번도 안 돌았는지 찾았다 (2026-08-02).** 1단계 미발화의 원인은 브리지가 아니라 **하네스**였다 — `TenFloorAutoPilot` 이 레버를 겨누지 않고 곧바로 `Interact()` 를 불러 `CrosshairInteractor.CurrentInteractable` 이 한 번도 레버가 되지 않았고, 그래서 dwell 0.15초가 **성립할 수 없었다.** dwell 을 면제하지 않고 조준만 흉내 냈다(면제하면 그 상수가 다시 아무도 안 읽는 값이 된다). 5단계는 `OverharvestStageTimeline`(순수 C#) + `OverharvestStageView`(사건 버스 구독)로 신설했고, **「동시에」를 구조로 보장했다** — 세 채널이 각자 타이머를 갖지 않고 `Pull()` 이 3칸 배열에 같은 시각을 한 번에 쓴다. 씬에 `OverharvestStage` 를 만들고 6필드를 배선해 `AllChannelsBound=True`. 실측 **런 누적 1접근 3 / 5재개 3 · 동시 예**. **통관을 돌리는 코드가 이 저장소에 한 줄도 없었다**(전수 grep) — 이번 것이 최초이고, 상시 회전 기본값은 0 이다(켜면 고정 캡처의 각도가 시간에 따라 달라져 베이스라인이 흔들린다). 재개는 정수 바퀴 버스트라 끝나면 각도가 제자리다. **아직 관측되지 않은 것 둘**: 표본 시점에 `3응시 0 / 4정적 0` 이다. 이것이 **관측 시점 문제인지 미구현인지 확정하지 못했다** — 표본을 당김 직후 한 번만 뜨는데 응시 지연·정적 창은 그보다 뒤일 수 있다. 다음에 시간축으로 표본을 늘려 갈라야 한다. 그리고 **내 단정 설계 오류를 하나 겪었다** — 스테이지 카운터는 런 단위로 리셋되는데(그 설계가 옳다) 모든 런이 끝난 뒤 읽어 전부 0 을 봤다. 마지막 재현 런이 리셋한 값이었고, 같은 런의 층별 단정은 그때 전부 PASS 였다. 하네스가 당김 직후 누적하도록 고쳤다

### UP-POWER-07 — OverharvestProfile 데이터화 (9개 항목)
- 분류: Required · 출처: PRD §7.4
- 상태: CONNECTED · 패스: P2
- 구현: `Scripts/Data/Profiles/OverharvestProfile.cs`(PRD §7.4 의 9항목)
- 접근: 해당 없음
- 검증: `Ascend/Run All EditMode Tests` → 정적 구간 범위 조임 등
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-POWER-05
- 남은 문제: **`.asset` 은 이미 있다. 문제는 그것을 읽는 코드가 없다는 것이다.** 런타임 소비처를 세면 0곳이다(`Scripts/Data/Profiles/`·테스트·`Assets/Editor/` 제외). 씬 YAML 의 GUID 를 `.meta` 로 역매핑해도 씬이 참조하는 프로파일은 `PassengerReactionSet.asset` 하나뿐이다. 값을 바꿔도 화면에서 아무 일도 일어나지 않으므로 PRD §14.2 「교체 가능한 프리셋」이 성립하지 않는다 — `docs/runtime/DEAD_IMPLEMENTATION_AUDIT.md` §1. 소비처가 될 곳: `OverharvestApproachBridge`, 과수확 연출

### UP-POWER-08 — 초과 전력이 다층 상승·보상으로 이어진다
- 분류: Required · 출처: PRD §4.1, N03 「부분 실패」, N08 §11.3
- 상태: VERIFIED · 패스: P2
- 구현: `Scripts/Run/AscendResult.cs`, `Core/FloorMath.cs`
- 접근: 170% 이상 달성 시 여러 층을 오른다
- 검증: `Ascend/Run Self Tests`, `Logs/curriculum_coverage.txt`
- 증거: `Logs/curriculum_coverage.txt`
- 의존: UP-POWER-02
- 남은 문제: 없음

## 2.7 RUN — 10층 진행과 런 구조

### UP-RUN-01 — 1층부터 10층까지 연속 진행
- 분류: Required · 출처: PRD §4.1, §17.1 「개발자 조작 없이 1층부터 10층까지」
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Run/RunSession.cs`, `RunSessionBehaviour.cs`
- 접근: 플레이 시작 → 10층까지
- 검증: `Logs/tenfloor_playmode.txt` 「10층 완주가 최소 3회」
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-POWER-03
- 남은 문제: 없음

### UP-RUN-02 — 10층 커리큘럼 (Teach → Test → Twist)
- 분류: Required · 출처: PRD §4.1, N03 「첫 10층 학습 구간」, `D-20260801-01`
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Spin/FloorPlan.cs`
- 접근: 층마다 새 규칙이 하나씩 나온다
- 검증: `Assets/Editor/CurriculumCoverageProbe.cs` → `Logs/curriculum_coverage.txt`
- 증거: `Logs/curriculum_coverage.txt`
- 의존: UP-RUN-01
- 남은 문제: 없음

### UP-RUN-03 — 다층 상승이 커리큘럼 층을 건너뛰지 않는다
- 분류: Required · 출처: `D-20260801-02`, `D-20260731-03`, N03 「건너뛴 층의 이벤트는 발생하지 않는다」
- 상태: VERIFIED · 패스: P2
- 구현: `Scripts/Spin/FloorPlan.cs`(`MustBePlayed`), `Core/FloorMath.cs`(`ClampAscent`)
- 접근: 큰 초과 전력으로 상승해도 4·6·7·9층을 밟는다
- 검증: `Logs/tenfloor_playmode.txt` 「방문 층이 연속이다」, `Logs/curriculum_coverage.txt`
- 증거: `Logs/curriculum_coverage.txt`, `Logs/tenfloor_playmode.txt`
- 의존: UP-RUN-02
- 남은 문제: 완주율이 67.5% → 59.0%로 내려갔다 (층을 더 치르는 대가, 의도된 것)

### UP-RUN-04 — 완주·실패·사고 결과가 정상 종료
- 분류: Required · 출처: PRD §17.1 「진행 불가 상태와 치명적 콘솔 오류 없음」
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Run/RunResult.cs`, `RunOutcome.cs`, `FloorResult.cs`
- 접근: 실패하거나 완주한다
- 검증: `Logs/tenfloor_playmode.txt` 「런이 완주 또는 사고로 끝났다」
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-RUN-01
- 남은 문제: 없음

### UP-RUN-05 — 고정 시드로 특정 층·스핀 단독 재현
- 분류: Required · 출처: PRD §13.5, §17.6, N08 §7.3
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/UI/DebugPanelView.cs`(`[T]` 시드 입력, `[R]` 재시작)
- 접근: F1 → T → 시드 입력 → Enter
- 검증: `Logs/tenfloor_playmode.txt` 「시드 1337 재현 — 방문 층이 같다」
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-CORE-01
- 남은 문제: 없음

### UP-RUN-06 — 요구 전력 계산 (기본 + 무게 + 층 보정)
- 분류: Required · 출처: PRD §4.1, N08 §11.2
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Core/FloorMath.cs`, `Spin/FloorPlan.cs`
- 접근: 적재를 늘리면 요구 전력이 오른다
- 검증: `Ascend/Run Self Tests` (`BuildTests`)
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-BUILD-03
- 남은 문제: 없음

### UP-RUN-07 — 층 상태 초기화 (다음 층 진입 시)
- 분류: Required · 출처: PRD §16.1 「층 상태 초기화」, N08 §5.2, §19.2
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Run/FloorSession.cs`, `RunSession.cs`
- 접근: 층을 넘기면 잔류·계약·스핀이 초기화된다
- 검증: `Ascend/Run Self Tests`
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-RUN-01
- 남은 문제: 없음

### UP-RUN-08 — 중복 입력으로 상태가 손상되지 않는다
- 분류: Required · 출처: N08 §19.2, §24 「결과 공개가 끝나기 전에 중복 스핀 가능한 구조」 금지
- 상태: VERIFIED · 패스: P2
- 구현: `Scripts/Run/RouletteInteractionBridge.cs`(연출 잠금)
- 접근: 스핀 중 레버를 연타한다
- 검증: `TenFloorAutoPilot` 의 「연출 중 레버를 더 눌러도 스핀이 줄지 않는다」 — 268회
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-CORE-11
- 남은 문제: **독립 감사 통과.** `RouletteInteractionBridge` 가 `alive` 에 `!IsLocked` 를 넣고 핸들러에서 한 번 더 확인하는 이중 가드다. 단정이 **잠긴 프레임 안에서만** 돌고, 그 프레임에 도달했음을 「연출 잠금을 실제로 관측했다」가 따로 보증한다 — 268회 실행

### UP-RUN-09 — 돈(Money) 자원
- 분류: Deferred · 출처: N99 「핵심 재화: 전력·돈」, N08 §5.1 — **PRD §4.1에 없다**
- 상태: DEFERRED · 패스: P1 P2
- 구현: `Scripts/Run/RunSession.cs`, `Core/RunState.cs`
- 접근: 잉여 전력이 돈으로 바뀐다
- 검증: `Logs/tenfloor_playmode.txt` 「소지금」 기록
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-POWER-08
- 남은 문제: 돈의 **소비처가 없다** (상점·거래는 Deferred). 현재는 점수에 가깝다

### UP-RUN-10 — 10층 연속 런 최소 3회 증거
- 분류: Required · 출처: PRD §17.4, §17.6
- 상태: VERIFIED · 패스: P4
- 구현: `Scripts/Run/Tests/TenFloorAutoPilot.cs`
- 접근: 해당 없음 (검증 장치)
- 검증: `Ascend/Ten Floor PlayMode` → `Logs/tenfloor_playmode.txt`
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-RUN-01
- 남은 문제: 없음

## 2.8 BUILD — 승객·부품·적재

### UP-BUILD-01 — 승객·부품 최소 4종
- 분류: Required · 출처: PRD §4.1, N08 §13
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Build/BuildLoadout.cs`(`BuildCatalog` — 승객 6 / 부품 5, 총 11종)
- 접근: 적재 층 승강장에서 후보를 태운다
- 검증: `Ascend/Run Self Tests` (`BuildTests`)
- 증거: `Logs/editmode_tests.txt`
- 의존: 없음
- 남은 문제: 없음

### UP-BUILD-02 — 승객·부품이 룰렛 규칙을 실제로 바꾼다
- 분류: Required · 출처: PRD §4.1, N02 「빌드 효과 분류」, N08 §13
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Build/BuildItem.cs`(`BuildEffectKind` 13종), `Spin/SpinRuleSet.cs`
- 접근: 승객을 태운 뒤 정화·패턴·연쇄가 달라진다
- 검증: `Ascend/Run Self Tests` → 「서로 다른 두 빌드가 결과를 바꾼다」
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-BUILD-01
- 남은 문제: 없음

### UP-BUILD-03 — 총중량·허용 중량·과적 상태
- 분류: Required · 출처: PRD §4.1, N02 「적재 기본 규칙」, N08 §11.2
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Build/BuildLoadout.cs`, `Core/FloorMath.cs`
- 접근: 계기판의 적재/허용 표시
- 검증: `Ascend/Run Self Tests` (`BuildTests`)
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-BUILD-01
- 남은 문제: 없음

### UP-BUILD-04 — 과적이 요구 전력과 사고 위험을 함께 올린다
- 분류: Required · 출처: PRD §4.1, N02 「과적재」, N99 「허용 중량 초과 시 요구 전력과 사고 위험 증가」
- 상태: VERIFIED · 패스: P2
- 구현: `Scripts/Core/FloorMath.cs`, `Risk/RiskEvaluator.cs`
- 접근: 허용 중량을 넘겨 태운 뒤 층을 시작한다
- 검증: `Ascend/Run Self Tests` (`RiskEvaluatorTests`, `BuildTests`)
- 증거: `Logs/editmode_tests.txt`, `Logs/loaded_critical_perf.txt`
- 의존: UP-BUILD-03, UP-RISK-01
- 남은 문제: 없음

### UP-BUILD-05 — 승객·부품이 엘리베이터 안에 실제 오브젝트로 배치
- 분류: Required · 출처: PRD §4.1, N00 「메뉴 속 아이콘에만 존재하지 않고」, N02
- 상태: VERIFIED · 패스: P1 P2 P3
- 구현: `Scripts/Build/BuildFigureView.cs`
- 접근: 태운 뒤 뒤를 돌아본다
- 검증: `Logs/tenfloor_playmode.txt` 「실은 것이 실제 오브젝트로 서 있다」
- 증거: `Logs/tenfloor_playmode.txt`, `Captures/TenFloor/07_cargo_full.png`
- 의존: UP-BUILD-01
- 남은 문제: 형상이 플레이스홀더 (최종 캐릭터는 `A-20260730-03`대로 승인 대기)

### UP-BUILD-06 — 승차·하차·획득·배치 흐름
- 분류: Required · 출처: PRD §5(4), N02 「승차 흐름」
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Player/InteractableBuildCandidate.cs`, `InteractablePassenger.cs`, `Build/BuildLoadout.cs`
- 접근: 적재 층 → 문 열기 → 후보 클릭 → 문 닫기
- 검증: `Logs/tenfloor_playmode.txt` 「적재 단계를 실제로 거쳤다」
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-SPACE-07
- 남은 문제: 없음

### UP-BUILD-07 — 승객 목적지와 하차 보상
- 분류: Required · 출처: N02 「NPC는 추가로 목적지·욕망·승하차 조건을 가진다」, N08 §5.1
- 상태: VERIFIED · 패스: P2
- 구현: `Scripts/Build/BuildLoadout.cs`(`DestinationFloor`, `DisembarkReward`)
- 접근: 목적지 층에 도착하면 승객이 내린다
- 검증: `Ascend/Run Self Tests` (`BuildTests`)
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-BUILD-01
- 남은 문제: 없음

### UP-BUILD-08 — 최소 두 개의 명확히 다른 빌드 전략
- 분류: Required · 출처: PRD §18 Phase 3 통과 조건 「서로 다른 두 빌드가 실제 판정 규칙과 의사결정을 바꾼다」
- 상태: VERIFIED · 패스: P2
- 구현: `Scripts/Build/BuildLoadout.cs`, `Sim/RunSimulator.cs`
- 접근: 직선 특화 vs 연결 붕괴형으로 다르게 태운다
- 검증: `Ascend/Run Self Tests` → 「서로 다른 두 빌드가 결과를 바꾼다」
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-BUILD-02
- 남은 문제: 없음

### UP-BUILD-09 — 발동 순서가 고정되고 로그로 추적된다
- 분류: Required · 출처: N02 「발동 순서」, N08 §8.1 「절대 변경하지 않는다」, §24 금지 구현
- 상태: VERIFIED · 패스: P2
- 구현: `Scripts/Spin/SpinEngine.cs`, `SpinResolution.cs`, `Scripts/Spin/Tests/SpinRuleSetTests.cs`
- 접근: 해당 없음
- 검증: `Ascend/Run All EditMode Tests` → 발동 순서 전용 단정(순서를 뒤집으면 값이 달라짐을 실제 숫자로 보인다)
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-BUILD-02
- 남은 문제: 없음

### UP-BUILD-10 — 무게가 클수록 강한 효과 (충돌하는 교환)
- 분류: Required · 출처: PRD §3(가설 7), N02 「이 빌드를 끝까지 감당할 수 있는가」
- 상태: VERIFIED · 패스: P2
- 구현: `Scripts/Build/BuildLoadout.cs`(부품 18~26kg vs 승객 6~16kg)
- 접근: 무거운 부품을 달고 요구 전력을 본다
- 검증: `Ascend/Run Self Tests` (`BuildTests`)
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-BUILD-03
- 남은 문제: 수치는 `A-20260731-02`대로 임시값

### UP-BUILD-11 — 화물 포기 구간(70~89%)이 실제로 무언가를 빼앗는다
- 분류: Required · 출처: N03 「부분 실패」, N08 §11.3
- 상태: VERIFIED · 패스: P2
- 구현: `Scripts/Core/FloorMath.cs`, `Run/FloorSession.cs`
- 접근: 요구 전력의 70~89%로 층을 끝낸다
- 검증: `Ascend/Run Self Tests`
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-POWER-02
- 남은 문제: 승객과 화물을 구분하지 않고 무게 순으로 버린다 → UP-APV-11

## 2.9 RISK — 감각적 위험 상태

### UP-RISK-01 — 위험 4단계 상태 기계
- 분류: Required · 출처: PRD §4.1, §8.1
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Risk/RiskLevel.cs`, `RiskEvaluator.cs`
- 접근: 과적·잔류·과수확이 쌓이면 단계가 오른다
- 검증: `Ascend/Run Self Tests` (`RiskEvaluatorTests` 11건)
- 증거: `Logs/editmode_tests.txt`
- 의존: 없음
- 남은 문제: **단계 이름이 PRD와 다르다.** PRD §8.1은 `Stable → Strain → Critical → Collapse`, 구현은 `Stable → Warning → Critical → Collapse` → UP-DOC-02

### UP-RISK-02 — 위험이 실제 게임 상태와 동기화된다
- 분류: Required · 출처: PRD §17.1 「감각적 위험 상태가 실제 게임 상태와 동기화됨」
- 상태: VERIFIED · 패스: P2
- 구현: `Scripts/Risk/RiskEvaluator.cs`, `RiskStateView.cs`
- 접근: 위험 단계가 연출이 아니라 상태에서 나온다
- 검증: 캡처 매니페스트의 「위험 단계는 연출이 아니라 실제 게임 상태다」 줄
- 증거: `Captures/TenFloor/manifest.txt`
- 의존: UP-RISK-01
- 남은 문제: 없음

### UP-RISK-03 — 조명 변화 (단계별)
- 분류: Required · 출처: PRD §8.2, §8.4
- 상태: CONNECTED · 패스: P2 P3
- 구현: `Scripts/Risk/RiskStateView.cs`, `RiskProfile.cs`
- 접근: 위험이 오르면 조명이 어두워지고 붉어진다
- 검증: 고정 캡처 `06_risk_stable`, `09_risk_strain`, `10_risk_critical`
- 증거: `Captures/TenFloor/06_risk_stable.png`, `Captures/TenFloor/10_risk_critical.png`
- 의존: UP-RISK-01
- 남은 문제: **위험이 방 전체에 닿는 채널을 새로 만들었다 (2026-08-01).** 그전까지 위험 → 환경 경로는 `RiskStateView` 의 `_cabinLight.color` **한 줄**뿐이었고, 등 하나로는 벽·천장·바닥이 거의 안 움직였다 — 실측으로 `06` Stable 벽 (96.5, 98.5, 99.4) vs `16` Collapse (96.5, 93.7, 95.7), 차이 **255 중 5 미만(약 2%)**. 독립 평가가 **세 라운드 연속** 「Critical 이 Stable 과 같은 방」이라고 지적한 것의 구조적 원인이 이것이었다. **셰이더 교체(순손실로 반려)가 원인이 아니라는 것도 되돌린 뒤 측정으로 확인했다.** → `RenderSettings.ambientLight` 를 단계 색으로 물들이게 했다(`_ambientBlend` 0.55). URP Lit 이 그대로 읽는 전역 값이라 머티리얼을 하나도 안 건드린다. **재측정 (Stable 대비 벽 색차)** — Strain (+1.7, −3.9, **−9.8**) · Critical (+2.4, −7.7, **−10.6**) · Collapse (**+6.9**, −7.9, −8.4). 색거리로 6.1 → **13.4**, 두 배 이상이다. 파랑이 빠지며 따뜻해지다가 Collapse 에서 빨강이 뛰는 진행이 수치로 단조롭다. **원래 값 복원도 확인했다** — 앰비언트는 씬이 아니라 렌더 설정 **전역**이라 복원하지 않으면 플레이 모드를 나간 뒤에도 에디터에 남아 다음 캡처를 오염시킨다(`OnDisable`·`OnDestroy` 양쪽에서 복원, 캡처 후 `(0.260, 0.270, 0.310)` 복귀 확인). **7차 판정: 채택 — 지정 테스트 통과.** `06`↔`16` 이 축소본에서 다른 방으로 보인다. **그러나 ⑧은 부분적으로만 닫혔다.** 두 가지가 남았다. **① 내 지표가 틀린 쌍을 지목했다.** 나는 색거리(Strain 10.7 / Critical 13.3 / Collapse 13.4)로 「Critical↔Collapse 가 포화라 약할 것」이라 적었는데 **반대였다** — 평가자는 **Strain↔Critical** 이 가장 약하다고 판정했다(둘 다 따뜻한 갈색, **밝기 차가 없어** 「조명이 조금 흔들린 정도」). Critical↔Collapse 는 갈색→와인에 **명도 하락이 겹쳐** 살아남았다. **「기준점으로부터의 거리」는 인접 구분 가능성을 재지 못한다** — 색상만 움직이고 명도가 그대로면 거리가 벌어져도 같은 밴드다. 두 축이 같은 방향으로 움직여야 경계가 선다. **② 정지 캡처에서만 통과한다.** 틴트 면적이 프레임의 **15~20%**(벽띠·천장 그늘)뿐이고 **3열 보드의 기둥·선반·프레임은 네 장 모두 같은 연회색**이다. 실제 플레이에서 시선은 보드에 고정돼 있으므로 그 시야 안에서는 Stable 과 Collapse 가 같은 색이다. **다음 작업: 위험 단계가 보드 자체에 도달하게 한다.** 부작용 점검 — 판독성 순손실 없음, 심볼은 형태로 구분되므로 틴트가 구분을 깨지 않는다(오히려 `16` 에서 면 분리가 늘었다). 다만 `UP-FIX-10`(게이지 역전)은 그대로다 — 방이 가장 붉은 `16` 에서 전력 바가 가장 창백하다 — 평가자가 지정한 기준은 「`06` 과 `16` 을 25% 축소본으로 나란히 놨을 때 방이 다르게 보여야 한다」이고, 13.4/255 가 그 눈높이를 넘는지는 수치가 답할 수 없다

### UP-RISK-04 — 진동·흔들림 (환경 오브젝트 우선, 카메라는 나중)
- 분류: Required · 출처: PRD §8.3 「카메라 셰이크보다 환경 오브젝트와 승객을 우선 흔든다」
- 상태: CONNECTED · 패스: P2 P3
- 구현: `Scripts/Risk/RiskProfile.cs`(`SwayAmplitude`/`SwayRate`/`CameraShake`), `RiskStateView.cs`
- 접근: Critical 상태에서 매달린 것들이 흔들린다
- 검증: 고정 캡처 `10_risk_critical`
- 증거: `Captures/TenFloor/10_risk_critical.png`
- 의존: UP-RISK-01
- 남은 문제: 없음

### UP-RISK-05 — 저주파·금속 응력음 (사이렌은 사건에만)
- 분류: Required · 출처: PRD §8.3 「사이렌은 지속 재생하지 않는다」
- 상태: CONNECTED · 패스: P2 P3
- 구현: `Scripts/Audio/ProceduralClipFactory.cs`(금속 타격·저역 하강·Collapse 임펄스), `Scripts/Audio/AudioCueTable.cs`(사건 전용 발동)
- 접근: 위험이 오르면 소리가 달라진다
- 검증: 오디오 채널 목록 + 무영상 청취 테스트
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-AUD-01
- 남은 문제: 사이렌을 **지속 재생하지 않는다**는 §8.3 원칙은 구조로 지켜진다 — 위험 사운드가 전부 사건 큐이고 지속음은 hum 하나뿐이다. 다만 씬 배선 전이라 실제로 들리지 않는다

### UP-RISK-06 — Collapse 단계 (암전 → 파열음 → 급강하 → 재점등)
- 분류: Required · 출처: PRD §8.2 Collapse
- 상태: CONNECTED · 패스: P2 P3
- 구현: `Scripts/Risk/CollapseSequence.cs`, 씬 `AscendRun` 배선 + `CameraRig`·`CeilingLampRig`
- 접근: 층을 실패한다
- 검증: `WaveBRuntimeProbe` → `Logs/waveb_runtime.txt`
- 증거: `Logs/waveb_runtime.txt`
- 의존: UP-RISK-01, UP-AUD-01
- 남은 문제: **연출이 실제로 돈다** — 낙차 lampRig/tank/sign 0.5794m · camRig 0.4138m, 실내등 배수 0.000(완전 암전) ~ 1.000, **복귀 오차 전부 0.00000**. 남은 것은 ① 파열음이 실제로 울렸는지 큐 단위로 확인되지 않았다(오디오 3건 재생됐으나 어느 것인지 미확인) ② Critical 과의 **시각적** 구분은 여전히 미증명 — 게임 카메라가 눈높이보다 1.6m 높아(부록 D) 비교 캡처를 신뢰할 수 없다

### UP-RISK-07 — DangerFeedbackProfile 데이터화 (9개 항목)
- 분류: Required · 출처: PRD §8.4, §14.1
- 상태: CONNECTED · 패스: P2 P3
- 구현: `Scripts/Data/Profiles/DangerFeedbackProfile.cs` + `Data/Profiles/DangerFeedbackProfile.asset` + `Scripts/Risk/RiskStateView.cs` 의 `RebuildProfiles()`
- 접근: 해당 없음
- 검증: `TenFloorAutoPilot` → 「위험 연출이 DangerFeedbackProfile 을 읽는다」
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-RISK-01
- 남은 문제: 주입은 진짜다 — 12필드 전부 `LateUpdate` 에서 소비되고, 에셋이 없으면 「코드 프리셋」으로 찍혀 단정이 통과하지 않는다. **그러나 요구는 §8.4 의 9항목이고 프로파일에 없는 것이 넷이다** — 단계 임계값(`RiskEvaluator` 의 코드 필드), 승객 반응 레벨(다른 에셋), 파티클 밀도(`UP-VIS-05` 가 미착수라 존재 자체가 없다), 일회성 충격음(`AudioCueTable` 쪽)

### UP-RISK-08 — 접근성 옵션 분리 (셰이크·사이렌·섬광)
- 분류: Required · 출처: PRD §8.3 마지막, §14.1
- 상태: CONNECTED · 패스: P2 P3
- 구현: `Scripts/Data/Profiles/AccessibilityProfile.cs` + `.asset` + `RiskStateView` 의 `ApplyLighting`(섬광)·`ApplySway`(셰이크)
- 접근: 옵션 메뉴 (없음)
- 검증: `Ascend/Run All EditMode Tests` + 씬 배선
- 증거: `Logs/editmode_tests.txt`, `Logs/tenfloor_playmode.txt`
- 의존: UP-RISK-07
- 남은 문제: **셋 중 둘만 분리됐다.** 섬광(`AllowFlickerAt`/`ClampFlickerRate`)과 흔들림(카메라 `ScaleShake` · 물체 `WorldSwayScale` 을 따로)은 `RiskStateView` 가 읽는다. **사이렌은 미구현** — `AllowSiren`·`SirenVolume` 의 런타임 소비처가 0곳이고 테스트와 에디터 배선 도구에서만 언급된다

### UP-RISK-09 — 과수확이 위험과 보상을 실제로 바꾼다
- 분류: Required · 출처: PRD §7.1, §7.4
- 상태: VERIFIED · 패스: P2
- 구현: `Scripts/Core/OverchargeOption.cs`, `Risk/RiskEvaluator.cs`
- 접근: 과수확 레버를 당긴 뒤 위험 계기를 본다
- 검증: `Ascend/Run Self Tests` (`RiskEvaluatorTests`)
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-DEVICE-03, UP-RISK-01
- 남은 문제: 없음

## 2.10 NPC — 승객 반응

### UP-NPC-01 — 승객이 위험 상태에 반응한다
- 분류: Required · 출처: PRD §4.1(승객 상황 반응), §8.2, §9.3
- 상태: VERIFIED · 패스: P2
- 구현: `Scripts/Build/BuildFigureView.cs`(`ReactToRisk`)
- 접근: 위험이 오르면 승객 자세가 바뀐다
- 검증: 고정 캡처 `09_risk_strain`, `10_risk_critical`
- 증거: `Captures/TenFloor/09_risk_strain.png`, `Captures/TenFloor/10_risk_critical.png`
- 의존: UP-BUILD-05, UP-RISK-01
- 남은 문제: **독립 감사 통과 (2026-08-01).** 감사자가 세 조건을 전부 직접 확인했다 — 코드 `BuildFigureView.cs:246` 이 `LateUpdate` 에서 `ReactToRisk()` 를 부르고 :279-285 가 단계별 기울기(Strain 3.5° / Critical 8.0° / Collapse 11.0°)를, :315-318 이 `_carIsPassenger[i]` 인 인물에만 적용한다. 씬 `:4726-4744` 활성 배선. **증거는 픽셀 대조다** — 승객이 선 좌하단 560×800 영역에서 06→09 는 90,620px, 06→10 은 184,865px 가 다르고 인물이 수직→약간→뚜렷하게 **단조로** 기운다. 조명이 아니라 기하 변화다. 감사자가 함정 둘도 배제했다: ① 세 장의 적재가 같다(`ForceCritical` 은 스핀만 하고 적재를 안 건드린다, HUD 가 셋 다 「요구 1187」) ② 인물이 실제 승객이다(10번에 「광신자」=`PSG_ZEALOT` 이름표). 남은 것(승격을 막지 않음): Collapse 단계의 승객 반응은 미관측 — `16_risk_collapse` 는 승객 없는 별도 런(시드 555555)이다

### UP-NPC-02 — 프로토타입 반응 이벤트 10종
- 분류: Required · 출처: PRD §9.2 (계약 선택 / 기본 정화 / 5연쇄 / 임계점 3개 / 과수확 해금 / 과수확 접근 / 추가 스핀 / Critical 진입 / Collapse 직전 / 사고·성공)
- 상태: CONNECTED · 패스: P2 P3
- 구현: `Scripts/Npc/PassengerReactionEvent.cs`(11종) + `PassengerReactionDirector.FiredKindsMask` + 씬 `PassengerReactionView`
- 접근: 각 사건이 일어날 때 승객을 본다
- 검증: `TenFloorAutoPilot` 의 「승객 반응 종류가 줄지 않았다」 + `Ascend/Run All EditMode Tests`
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-NPC-01
- 남은 문제: **안 울린 3종이 이제 산출물에 이름으로 적힌다** — `Threshold170`, `OverharvestApproach`, `ExtraSpin`(`Logs/tenfloor_playmode.txt`). 이전에는 `BitCount` 만 찍어서 감사자가 「8종」만 보고는 무엇이 빠졌는지 **끝내 특정하지 못했다.** 셋의 성격이 서로 다르다. ① **`OverharvestApproach` 는 구조적으로 도달 불가**다 — `OverharvestApproachBridge.cs:71-73` 이 **크로스헤어가 레버를 겨눌 때만** 발행하는데 하네스는 `TenFloorAutoPilot.cs:734` 에서 `overharvest.Interact(gameObject)` 를 직접 부른다. 조준하지 않는다. 하네스가 겨누도록 바꾸지 않으면 영원히 안 나온다. ② **`ExtraSpin` 은 사건이 실제로 발행된다** — `FloorSession.cs:290` 이 `GameEventKind.ExtraSpinTaken` 을 publish 하고 이번 런에서 과수확이 실제로 일어났다(오디오 `OverharvestPull` 이 울렸다). 그런데 반응은 안 울렸다. **원인 후보가 둘이고 지금 산출물로는 못 가른다** — 매핑이 안 걸렸는가, 아니면 발행됐지만 **매번 억제됐는가**(이번 런의 억제 130건 &gt; 시작 110건). `FiredKindsMask` 는 **실제로 시작된 것**만 센다. ③ `Threshold170` 도 같은 모호성 아래 있다. **다음**: 억제된 종류의 마스크를 따로 찍으면 ②·③ 의 원인이 한 줄로 갈린다

### UP-NPC-03 — PassengerReactionSet 데이터화
- 분류: Required · 출처: PRD §9.4 「반응은 `PassengerReactionSet` 데이터로 이벤트별 교체 가능」
- 상태: CONNECTED · 패스: P2
- 구현: `Scripts/Npc/PassengerReactionSet.cs` + `Data/Profiles/PassengerReactionSet.asset`(11종 채움) + 씬 `PassengerReactionView` 배선
- 접근: 해당 없음
- 검증: 에셋 항목 수 11 확인 + `Ascend/Run All EditMode Tests` 14건
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-NPC-02
- 남은 문제: 에셋이 실재하고 11종이 서로 다른 자세·시선·우선순위(5~55)로 채워져 있으며 뷰에 배선됐다. **데이터 교체가 실제로 반응을 바꾸는지는 미확인** — 승객이 탄 런이 아직 없다(부록 E)

### UP-NPC-04 — 표현 채널 (시선·자세·짧은 대사·비언어 음성)
- 분류: Required · 출처: PRD §9.3
- 상태: CONNECTED · 패스: P2 P3
- 구현: `Scripts/Npc/PassengerReaction.cs`(자세 7종 · 시선 6종 · 음성 큐 ID), `Scripts/Build/BuildFigureView.cs`(`SetReaction`/`GazeRotation`)
- 접근: 승객을 바라본다
- 검증: `Ascend/Run All EditMode Tests` + 고정 캡처의 승객 자세 대비
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-NPC-02, UP-AUD-04
- 남은 문제: 자세와 시선 두 채널이 코드에 생겼다. **짧은 대사는 없고**(PRD §9.4 가 긴 대화 트리를 제외하므로 한 줄 이하), 비언어 음성은 큐 ID 만 있고 재생 배선이 없다. 시선 대상 넷은 씬에서 배선해야 동작한다

### UP-NPC-05 — 동시 반응 제한 (우선순위·쿨다운·최대 수)
- 분류: Required · 출처: PRD §9.4 「한 이벤트에서 모든 승객이 동시에 말하지 않는다」
- 상태: VERIFIED · 패스: P2
- 구현: `Scripts/Npc/PassengerReactionDirector.cs`(우선순위·쿨다운·최대 수) + 씬 배선
- 접근: 해당 없음
- 검증: `TenFloorAutoPilot` 의 「동시 반응이 상한을 넘지 않았다」
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-NPC-02
- 남은 문제: **독립 감사 통과.** `PassengerReactionDirector` 가 실제 상한을 걸고(`_selected.Count < _maxConcurrent && active < _maxConcurrent`), 씬이 `2` 로 배선돼 있으며, EditMode 3축(최대 수·쿨다운·우선순위)이 전부 PASS 다. PlayMode 단정이 **관측 조건을 스스로 요구한다** — 승객 4명 > 상한 2 인 상태에서만 성립하므로 공허하게 통과할 수 없고, 가드를 지우면 최대동시가 2를 넘어 빨간불이 된다

## 2.11 REC — 사고 기록기

### UP-REC-01 — 층·런 종료 시 기록 생성
- 분류: Required · 출처: PRD §4.1, §10.1
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Run/AccidentRecorder.cs`, `FloorRecord.cs`
- 접근: 층이 끝나면 기록이 남는다
- 검증: `Logs/tenfloor_playmode.txt` 「사고 기록기가 층마다 기록했다」
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-RUN-04
- 남은 문제: 없음

### UP-REC-02 — 출력 항목 9종
- 분류: Required · 출처: PRD §10.2 (최고 층 / 최고 캐스케이드 / 최고 과수확 비율 / 핵심 계약 / 핵심 승객·부품 / 종료 원인 / 마지막 과수확 선택 / 잃은 승객·화물 / 런 시드)
- 상태: CONNECTED · 패스: P2
- 구현: `Scripts/Run/FloorRecord.cs`, `RunResult.cs`
- 접근: 사고 기록기 화면
- 검증: 고정 캡처 `17_accident_recorder`
- 증거: `Captures/TenFloor/17_accident_recorder.png`
- 의존: UP-REC-01
- 남은 문제: 항목 대조표가 없다. 9종이 전부 나오는지 검증되지 않았다

### UP-REC-03 — 인게임 출력과 디버그가 같은 데이터를 쓴다
- 분류: Required · 출처: PRD §10.3
- 상태: CONNECTED · 패스: P2
- 구현: `Scripts/Run/AccidentRecorder.cs`(`FloorRecord` 단일 원본)
- 접근: 해당 없음
- 검증: `Ascend/Run Self Tests`
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-REC-01
- 남은 문제: **단일 원본은 진짜다 — 그러나 증거가 그것을 담고 있지 않다.** 코드 구조는 확인됐다: `AccidentRecorder.cs:65-67` 이 `FloorRecord` 하나를 만들고, `GameHudView.cs:220-227` 과 `PaperTapePrinterView.cs:154-183` 이 **씬에서 같은 fileID 998528534** 를 가리킨다 — 서로 다른 기록기를 볼 여지가 없다. **그런데** 증거로 건 `Logs/editmode_tests.txt` 226줄·10스위트·194 PASS 에 `AccidentRecorder`·`FloorRecord`·HUD·프린터 스위트가 **하나도 없다.** 「인게임 출력과 디버그가 같은 값을 말한다」를 확인하는 단정은 저장소에 0건이다. 게다가 프린터 쪽 유일한 디스크 관측(`Logs/waveb_runtime.txt`)의 「인쇄된 줄 2」는 `PaperTapePrinterView.cs:112-113` 의 **머리글**이지 `FeedRecord` 산물이 아니다 — 테이프는 층 기록을 한 줄도 찍은 적이 없다. `DEAD_IMPLEMENTATION_AUDIT` §7 의 「판정만 남았다」가 틀렸다. 남은 것은 판정이 아니라 **증거**다. 출처 `PRD §10.3` 도 동결 PRD 에 없다

### UP-REC-04 — 기계식 프린터·종이 테이프 형태의 물리적 출력
- 분류: Required · 출처: PRD §10.1 「단순 결과창 대신 엘리베이터 내부의 기계식 프린터, 종이 테이프 또는 펀치카드」
- 상태: CONNECTED · 패스: P3
- 구현: `Scripts/View/PaperTapePrinterView.cs`, 씬 `AccidentPrinter` @ (0.55, 2.05, −1.43) 정면 벽
- 접근: 층이 끝나면 화면에 뜬다
- 검증: `WaveBRuntimeProbe` 의 인쇄 줄 수
- 증거: `Logs/waveb_runtime.txt`
- 의존: UP-REC-02
- 남은 문제: 장치가 씬에 서 있고 **2줄을 실제로 찍었다.** **증거 정정**: 직전 판본이 검증 수단으로 적은 `EyeLevelCapture` 09번 각도는 **존재하지 않는다** — `Captures/eyelevel/` 에는 00~08 만 있다. 결함은 그대로다: `PaperTapePrinterView.cs` 가 테이프 폭 `0.28f` 에 `fontSize = 0.9f` 를 써서 글자가 1.16m 로 넘쳐 흐른다. 읽을 수 없다

### UP-REC-05 — 기록과 사고 후 상태가 한 장에 함께 보인다
- 분류: Required · 출처: PRD §10.3 마지막
- 상태: CONNECTED · 패스: P3
- 구현: `Scripts/Run/Tests/TenFloorCaptureRig.cs`
- 접근: 사고 직후 화면
- 검증: 고정 캡처 `17_accident_recorder`
- 증거: `Captures/TenFloor/17_accident_recorder.png`
- 의존: UP-REC-04
- 남은 문제: **「17번만 해상도가 다르다」는 낡은 기록이었다 (2026-08-02 확인).** 매니페스트의 주장이 아니라 **PNG 헤더의 바이트를 직접 읽어** 대조했다 — `17`·`19`·`20`·`01` 전부 **1920×1080** 이다. 예전의 816×714 는 게임 뷰를 캡처 전에 고정하게 만든 변경으로 이미 해소됐고, 백로그와 `UP-PLAT-06`·`UP-FIX-16` 이 그 뒤로 갱신되지 않았을 뿐이다. **방식 차이(전용 카메라 렌더 vs 화면 캡처)는 남지만 그것은 결함이 아니라 요구다** — `ScreenSpaceOverlay` HUD 는 카메라 렌더에 절대 들어가지 않으므로, 「기록과 사고 후 상태가 한 장에」를 만족하려면 화면 캡처여야 한다. 남은 것은 **판독성**이고(패널 자기모순·잘림) 그것은 `UP-FIX-11`·`UP-FIX-22` 와 Pass 3 의 몫이다. 이 항목은 `PASS3_GATED` 에 있어 그때까지 VERIFIED 로 가지 않는다

## 2.12 VIS — 비주얼과 아트 디렉션

### UP-VIS-01 — 스타일 락 (low-poly industrial occult horror)
- 분류: Required · 출처: PRD §12.1, N06 §5 Style Lock
- 상태: CONNECTED · 패스: P3
- 구현: 락 문서는 `docs/VISUAL_BIBLE.md` §2 · 지오메트리는 그레이박스 프리미티브(`Assets/Editor/GrayboxWorldBuilder.cs`)
- 접근: 어디서든
- 검증: 독립 시각 평가
- 증거: `docs/VISUAL_BIBLE.md` §2.1·§2.2 (락 문장) + `Captures/TenFloor/` 23장 (현재 상태)
- 의존: UP-SPACE-04
- 남은 문제: **락 자체는 이미 문서로 존재한다** — `VISUAL_BIBLE.md` §2.1(지시서 §4 전체 그래픽 방향 11항목)과 §2.2(Notion 06 §5 영문 Style Lock 문장)다. 증거 경로가 「없음」이었던 것은 **문서를 안 가리키고 있었을 뿐**이다. → 연결했다. **그러나 구현이 락을 따르지 않는다.** 락이 요구하는 것 중 화면에 없는 것 — ① 「저해상도 손그림 픽셀 텍스처」: 텍스처가 **0개**다(전부 무지 머티리얼) ② 「단순한 Gouraud 또는 플랫 셰이딩」: URP 기본 리트 셰이딩이고 공통 스타일 셰이더가 없다(`UP-VIS-04` 가 그것이고 NOT_STARTED) ③ 「차가운 회녹색 그림자와 바랜 산업용 색」: 팔레트를 강제하는 것이 없어 `20_aim_prompt_screen` 의 하이라이트가 **팔레트에 없는 채도 높은 에메랄드**로 나갔다 (5차 판정 지적 8번) ④ 「큰 실루엣과 눈에 띄는 폴리곤 면」: 심볼 3종이 Unity 기본 프리미티브(Sphere/Cube/Capsule)이고 머티리얼조차 배정되지 않는다(`UP-DEVICE-09` 와 같은 뿌리). **셋(①②④)이 `UP-VIS-04` 공통 셰이더 하나로 같이 움직인다** — 그것이 이 항목의 실제 다음 단계다. 시각 스타일 점수가 5차에서 **2.23/5** 인 것이 이 미구현의 직접 결과다
- **SKELETON → CONNECTED 근거 (2026-08-02 09:2x).** 위에 적은 「락이 요구하는데 화면에
  없는 것」 넷 중 **셋이 화면에 올라왔다.** 셋이 `UP-VIS-04` 하나로 같이 움직인다고
  적어 뒀고, 실제로 그렇게 움직였다.

  | 락 항목 | 이전 | 지금 |
  |---|---|---|
  | ② 단순한 Gouraud/플랫 셰이딩 | URP 기본 리트 | **`Ascend/Stylized` 14개 렌더러** — 램버트 계단 양자화 |
  | ③ 차가운 회녹색 그림자 | 강제하는 것 없음 | 같은 셰이더의 `_ShadowTint`(0.20, 0.26, 0.24) |
  | ④ 큰 실루엣·눈에 띄는 폴리곤 면 | 무지 머티리얼 | 같은 셰이더의 실루엣 림 + 계단 |
  | ① 저해상도 손그림 픽셀 텍스처 | 텍스처 0개 | 에셋 4장 생겼으나 **아직 머티리얼에 안 물렸다** |

- 남은 것 (VERIFIED 를 막는다): ①의 채택 · 장치·심볼·글자가 아직 URP/Lit ·
  독립 시각 평가 ACCEPT. **스타일 점수는 재판정 전까지 2.00/5 그대로로 본다** —
  숫자가 좋아졌다고 화면이 좋아졌다는 뜻이 아니라는 것이 이 저장소의 반복 실패다.

### UP-VIS-02 — 라이팅 목표 (탁한 천장등 vs 차가운 통관 발광)
- 분류: Required · 출처: PRD §12.3
- 상태: CONNECTED · 패스: P3
- 구현: 씬 `CabinLight`/`CeilingLamp`/`Directional Light`, `Scripts/Risk/RiskStateView.cs`
- 접근: 엘리베이터 안
- 검증: 고정 캡처 `01_entry`, `06_risk_stable`
- 증거: `Captures/TenFloor/01_entry.png`
- 의존: UP-VIS-01
- 남은 문제: 없음

### UP-VIS-03 — 핵심 상호작용물이 실루엣만으로 구분된다
- 분류: Required · 출처: PRD §12.2 마지막, §15.2
- 상태: CONNECTED · 패스: P3
- 구현: 씬 장치 배치
- 접근: 입구에서 내부를 본다
- 검증: 독립 시각 평가 (실루엣 항목)
- 증거: `Captures/TenFloor/01_entry.png`
- 의존: UP-VIS-01
- 남은 문제: 없음

### UP-VIS-04 — URP 공통 스타일 셰이더
- 분류: Required · 출처: PRD §12.4
- 상태: CONNECTED · 패스: P3
- 구현: `Assets/Prototype_Elevator/Art/Shaders/AscendStylized.shader` + `Art/Materials/MAT_Ascend_{Iron,Brass,Wood}.mat`
- 접근: 해당 없음 (머티리얼 채택 전)
- 검증: `ShaderUtil.ShaderHasError` false · `shader.isSupported` true · 머티리얼 3종 생성 확인
- 증거: `Assets/Prototype_Elevator/Art/Shaders/AscendStylized.shader`
- 의존: UP-VIS-01
- 남은 문제: **셰이더는 있고 컴파일된다. 아직 아무도 안 쓴다 — 그래서 SKELETON 이다.** `Ascend/Stylized` 가 `VISUAL_BIBLE.md` §2.1 중 셰이더가 책임지는 셋을 구현한다 — ① 램버트 **양자화**(계단 3~4단)로 「단순한 Gouraud 또는 플랫 셰이딩」 ② 감쇠 거듭제곱(2.5)으로 「제한적인 국소 조명과 빠른 감쇠」 ③ 그림자 쪽을 **회녹색**(0.20,0.26,0.24)으로 물들여 「차가운 회녹색 그림자」. 실루엣 림도 약하게 넣었다 — 무지 머티리얼끼리 겹치면 경계가 사라지기 때문이다. `ShaderUtil.ShaderHasError` **false**, `isSupported` **true**, ShadowCaster 패스 포함. `.shadergraph` 가 아니라 텍스트 셰이더인 이유는 diff 가 읽히기 때문이다 — 이 저장소는 직렬화 에셋이 조용히 깨진 이력이 있다. **채택 전에 발견한 제약 하나 (중요):** 이 셰이더에 `_EmissionColor` 가 **반드시** 있어야 한다. `SpinBoardView`(정화 점등) · `InstrumentPanelView`(계기 발광) · `OverharvestUnlockEffect`(덮개 레일) 셋이 `MaterialPropertyBlock` 으로 그 이름을 쓴다. 프로퍼티가 없으면 블록이 **조용히 무시되고 점등이 사라진 채 아무 오류도 안 난다** — `UP-CORE-12` 가 GIF 로 확인된 바로 그 연출이 그렇게 죽는다. 채택 직전에 확인해서 넣었다(발광은 조명과 무관하게 **더한다** — 곱하면 어두운 칸에서 사라진다). **심볼 배선도 확인했다**: 9칸 × 3종(`Sym_NormalSoul`·`Sym_Absorber`·`Sym_Proliferator`)이 전부 `M_Gray_Readout` **하나**를 공유한다 — `UP-DEVICE-09` 가 「실루엣 1축뿐」인 구조적 이유가 이것이다. **채택을 1차 시도했고 되돌렸다 (2026-08-01).** 심볼 3종 27개 렌더러에 `MAT_Sym_{NormalSoul,Absorber,Proliferator}` 를 배정하고 캡처했더니 `04_symbols` 의 심볼이 **거의 검은 덩어리**가 됐다 — 이전의 밝고 읽히는 흰 실루엣보다 명백히 나쁘다. 「직전 승인 빌드보다 나빠지면 채택하지 않는다」에 걸려 **27개 전부 `M_Gray_Readout` 으로 복원**했고 씬 diff 가 비어 있음을 확인했다. **원인은 셰이더의 그림자 항이다** — `shadowed = _ShadowTint.rgb * _BaseColor.rgb` 로 **곱하고 있어서** 어두운 기본색(무쇠 0.16)이 거의 0 이 된다. 게다가 통관 안쪽은 대부분 그늘이라 그 항이 지배한다. **고쳤다 (아직 재채택은 안 했다).** 그림자 항을 곱에서 `_BaseColor * _ShadowLift(0.55) + _ShadowTint * 0.35` 로 바꾸고 `_AmbientFloor` 를 0.18 → 0.35 로 올렸다. 산술 대조 — 그늘 값이 무쇠 0.032 → **0.158(4.9배)**, 놋쇠 0.100 → **0.345(3.5배)**, 뼈 0.144 → **0.466(3.2배)**. 컴파일 오류 0, `isSupported` true. **재채택은 이번 세션에서 하지 않는다** — 스스로 「심볼이 아니라 넓고 밝은 면부터 시험한다」고 적었고 그건 새 캡처·판정 주기가 필요하다. 숫자가 좋아졌다고 화면이 좋아졌다는 뜻은 아니다. **남은 것은 채택이다.** 씬의 MeshRenderer 99개가 아직 URP 기본 머티리얼이고, 심볼 3종은 **런타임 생성**이라(`SpinBoardView`) 에디트 모드에 존재하지 않는다 — `HumanScaleLayout.MakeSymbol` 과 보드 생성 경로에서 머티리얼을 배정해야 한다. **이번 세션에서 일괄 교체를 하지 않은 이유**: 99개를 한 번에 바꾸면 그 결과를 판정하는 데 캡처 1회(~5분) + 독립 평가가 필요한데, 직전 판정이 이미 2.45/2.23 으로 내려간 상태라 검증 없는 대규모 시각 변경은 「직전보다 나빠지면 채택하지 않는다」를 지킬 수 없다. 채택은 소수 대상부터 하고 매번 판정을 받는다
- **SKELETON → CONNECTED 근거 (2026-08-02 09:2x).** 「아직 아무도 안 쓴다」가 끝났다.
  **`Ascend/Stylized` 를 쓰는 렌더러 0개 → 14개**(`CarShell_*` 벽·바닥·천장·손잡이 13종).
  채택은 `Ascend/Adopt Stylized — 벽·바닥·천장` 이 그 자리에서 셰이더만 갈아 끼우고,
  같은 메뉴의 되돌리기가 색을 그대로 옮겨 원상복구한다(씬 빌더를 다시 돌리지 않는다 —
  그건 위치·회전까지 다시 써서 멱등이 아니다).
- **되돌리지 않은 첫 채택이다.** 앞선 두 번(심볼 27개 · 벽 13종)은 직전보다 나빠져
  되돌렸다. 이번에는 11차 판정이 지정한 기준 두 개를 실측으로 넘겼다.

  | 축 | 11차(되돌린 채택) | 지금 | 기준 |
  |---|---|---|---|
  | 좌벽 ΔL 안정→붕괴 | 평탄 (네 단계 82.3) | **17.13** · 4단계 단조 하강 | ≥ 15 |
  | Stable↔Collapse 바이트 동일 | 60.5% | **0.5%** | < 60% |
  | 마젠타(오류 셰이더) 화소 | — | **0** | 0 |

  단계별 실측: 76.03 → 68.21 → 62.72 → 58.90.
- **11차의 「위험 채널을 먹는다」는 셰이더 로직 문제가 아니었다.** 원인은
  `_CLUSTER_LIGHT_LOOP` 누락이다. `PC_Renderer` 가 **ForwardPlus**(`m_RenderingMode = 2`)라
  추가 광원이 화면 클러스터에 들어가는데, 고전 `GetAdditionalLightsCount()` 루프는
  **0 을 돌려준다.** 위험 단계를 나르는 `CabinLight`(Point · 1.60 → 0.54)가 통째로
  안 보였고, 그래서 벽이 위험에 반응하지 않았다. `LIGHT_LOOP_BEGIN`/`END` 로 바꿔 해결.
- 남은 것 (VERIFIED 를 막는다): ① 장치·심볼·글자는 아직 URP/Lit 이다
  ② 텍스처 4장이 머티리얼에 안 물렸다 ③ 독립 시각 평가 ACCEPT 미획득

### UP-VIS-05 — 파티클 (먼지·녹가루·스파크·정화 파편·캐스케이드 유입)
- 분류: Required · 출처: PRD §12.5
- 상태: CONNECTED · 패스: P3
- 구현: `Scripts/Effects/AmbientParticleDirector.cs` (5종 + 단계별 상한)
- 접근: 씬 `AscendRun` 에 배선 — 위험 단계에 따라 자동
- 검증: `Logs/tenfloor_playmode.txt` 파티클 4단정 (존재·5종·실제 배출·예산)
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-VIS-01
- 남은 문제: **만들었고 돈다.** 다섯 갈래(먼지·녹가루·스파크·정화 파편·캐스케이드 유입)가 코드로 생성되고 위험 단계에 묶인다. PRD §12.5 가 요구한 **단계별 상한**도 `MaxParticlesFor`(Stable 24 / Strain 48 / Critical 80 / Collapse 120)로 못박았다. 실측 — 최대 동시 **274 / 전 계통 상한 600**, 예산 안. 단정은 넷이고 그중 하나가 **「실제로 나왔다」(peak > 0)** 라 예산 검사가 공허하게 참이 되지 않는다. 프리팹·머티리얼 에셋을 만들지 않고 코드로 생성한 이유는 씬과 `.mat` 을 동시에 건드리는 조합이 이 저장소에서 가장 조용히 깨지기 때문이다. **아직 시각 판정을 받지 않았다** — 5차 판정은 파티클 이전 캡처다. 특히 5차의 지적 ⑧(「`10` Critical 이 `06` 과 같은 방 같은 조명」)이 실제로 풀렸는지는 **재캡처 후 독립 평가가 정할 일**이다. 오버드로우 실측(`UP-TECH-07`)도 파티클 추가 후 다시 재야 한다

### UP-VIS-06 — 필수 고정 캡처 세트 9종
- 분류: Required · 출처: PRD §15.1
- 상태: CONNECTED · 패스: P3 P4
- 구현: `Scripts/Run/Tests/TenFloorCaptureRig.cs`
- 접근: 해당 없음
- 검증: 캡처 실행 → `Captures/TenFloor/manifest.txt`
- 증거: `Captures/TenFloor/manifest.txt`
- 의존: UP-PLAT-06
- 남은 문제: 20장이 실재하고 매니페스트가 각 장의 주장을 적는다. **그러나 독립 평가가 주장과 그림이 다른 장 9건을 찾았다**(`UP-FIX-13`) — 예: `01_entry` 는 「전체 내부」를 주장하나 실제로는 벽 모서리 하나이고, `09_risk_strain` 은 「과적 218/130」이라 적었으나 화면에 무게·적재 표시가 하나도 없으며, `15`·`19` 는 「연쇄 8단계」라 적었으나 단계 표시가 어떤 형태로도 없다. **매니페스트가 그림이 보이지 않는 것을 주장하면 그 세트는 증거가 아니라 목록이다**

### UP-VIS-07 — 시각 루브릭 통과 (판독성·스타일 평균 4.0 이상)
- 분류: Required · 출처: PRD §15.2 통과 조건
- 상태: CONNECTED · 패스: P3 P4
- 구현: `.claude/visual-criteria.md`, `.claude/skills/visual-verify/SKILL.md`
- 접근: 해당 없음
- 검증: `docs/runtime/VISUAL_VERDICT.md` 의 독립 판정
- 증거: `docs/runtime/VISUAL_VERDICT.md`, `Captures/TenFloor/manifest.txt`
- 의존: UP-VIS-06
- 남은 문제: **⚠ 이 상태값은 독립 감사와 충돌한다 — 조용히 한쪽으로 정리하지 말 것.** 감사자 둘은 `NOT_STARTED` 를 유지해야 한다고 판정했다: 「이 항목의 산출물은 **ACCEPT 판정 하나뿐**이고 그것이 0건이다. `구현:` 필드가 가리키는 두 파일은 평가 **절차**이지 이 항목의 산출물이 아니다.」 이 논거는 타당하다. 내 논거(아래)도 타당하다 — 어느 쪽이든 이 항목은 §1 `PASS3_GATED` 에 있어 **Pass 3 을 계속 막으므로 요구가 약해지지 않는다.** 상태값 논쟁과 무관하게 감사가 지목한 **세 결함은 고쳐야 한다**: ① `구현:` 필드를 평가 절차가 아니라 평가 **대상**(캡처 세트 + 씬)으로 바꾼다 ② 출처 `PRD §15.2` 가 동결 PRD 에서 해소되지 않는다(동결본은 §15 에서 끝나고 「4.0」은 전 문서에 0건 — 실제 출처는 Notion) ③ 아래 점수가 낡았다(2.6/2.45 는 2회차, 이후 6차가 2.78/2.35). **상태 정정 근거 (2026-08-02).** `NOT_STARTED` 였으나 그것은 **분류 오류**였다 — 루브릭 문서·평가 스킬·판정 기록이 전부 존재하고 실제 캡처로 **일곱 번** 채점됐다. `NOT_STARTED` 는 「구현이 실제로 없다」는 뜻인데 여기서 없는 것은 구현이 아니라 **합격**이다. 그 둘을 한 상태로 적으면 Pass 1(존재 여부)이 Pass 3(품질)의 실패로 막힌다. → `CONNECTED`. 이 항목은 백로그 §1 `PASS3_GATED` 에 있으므로 **루브릭이 통과할 때까지 Pass 3 을 계속 막는다** — 요구가 약해진 것이 아니라 막는 패스가 제자리를 찾은 것이다. **2026-08-01 2회차 판정 REJECT — 판독성 2.6 / 스타일 2.45 (요구 4.0).** 20장 전부를 독립 평가자가 직접 열어 채점했다. 결정적 사유는 금지 항목 **`B-5 #15`** — 3×3 결과판과 전력/요구 계기가 **어느 캡처에서도 동시에 읽히지 않는다.** 판을 보면 계기가 잘리고 계기를 보면 판이 잘린다. 지적은 `UP-FIX-07`~`UP-FIX-13` 으로 옮겼다. **가장 먼저 고칠 것**: 과수확 3장(11·12·13)의 잘린 HUD 좌측 — 같은 크롭이 02·04·15·17·19 에도 있어 프레이밍 규칙 하나로 **20장 중 8장이 동시에 오르고**, 그것이 `B-5 #15` 를 푸는 경로다. 최고점은 `12_overharvest_unlocked`(덮개·레일·조명·계기·문구 네 채널이 동시에 같은 상태를 말한다), 최저점은 `14_contract_select`(판독성 1/5)

### UP-VIS-08 — 카지노 슬롯머신·장식적 스팀펑크로 보이지 않는다
- 분류: Required · 출처: PRD §12.1 금지, §15.2 루브릭
- 상태: CONNECTED · 패스: P3
- 구현: 장치 형상 설계 (`docs/DEVICE_DESIGN_SPEC.md`)
- 접근: 장치 정면
- 검증: 독립 시각 평가
- 증거: `Captures/TenFloor/02_device_front.png`
- 의존: UP-VIS-03
- 남은 문제: 없음

### UP-VIS-09 — 축소 화면에서도 상태가 읽힌다
- 분류: Required · 출처: PRD §11, §15.2 마지막 항목
- 상태: CONNECTED · 패스: P3
- 구현: `Captures/TenFloor/scaled25/` 생성기 (원본 21장 → 480×270, LANCZOS)
- 접근: 해당 없음
- 검증: 캡처를 25% 축소해 독립 평가
- 증거: `Captures/TenFloor/scaled25/manifest.txt` (21장 + 대응표)
- 의존: UP-VIS-07
- 남은 문제: **⚠ `UP-VIS-07` 과 같은 충돌이 있다** — 독립 감사는 `NOT_STARTED` 유지를 주장했고(세트 전체 판정 0건), 그 논거를 그쪽 항목에 적어 뒀다. 이 항목도 `PASS3_GATED` 라 Pass 3 을 계속 막는다. **선행 결함이 하나 특정됐다**: `UP-FIX-18`(위험 단계가 **색만** 움직이고 명도가 그대로) 때문에 7차 평가가 480×270 에서 `Strain↔Critical` 을 「조명이 조금 흔들린 정도」로 구분 실패했다 — 축소 판독은 그것을 고치기 전에는 재판정해도 같은 결과가 나온다. **상태 정정 근거 (2026-08-02).** `UP-VIS-07` 과 같은 분류 오류였다 — 생성기와 축소 세트 **23장**, 대응표 `scaled25/manifest.txt` 가 전부 디스크에 있는데 `NOT_STARTED` 로 적혀 있었다. 없는 것은 산출물이 아니라 **판정**이다. → `CONNECTED`. `PASS3_GATED` 에 포함돼 Pass 3 을 계속 막는다. 축소 세트를 **만들었다** — 원본 1920×1080 21장을 25%(480×270)로 줄여 `Captures/TenFloor/scaled25/` 에 두고 독립 평가를 요청했다. 리샘플은 LANCZOS 로 **축소에 유리한 쪽**을 골랐다 — 더 거친 BOX/NEAREST 면 리샘플 탓인지 디자인 탓인지 갈리지 않기 때문이다. **판정은 아직이다.** 그리고 이 항목은 `UP-VIS-07`(시각 루브릭 평균 4.0)에 의존하는데 그쪽이 현재 REJECT 이므로, 축소 판정이 좋게 나와도 **이 항목만 먼저 VERIFIED 로 갈 수 없다**

### UP-VIS-10 — 안개·먼지·빛줄기가 결과판을 가리지 않는다
- 분류: Required · 출처: PRD §12.3
- 상태: CONNECTED · 패스: P3
- 구현: 볼륨 프로파일 없음 = 현재는 가리지 않음
- 접근: 결과판 정면
- 검증: 고정 캡처 `02_device_front`
- 증거: `Captures/TenFloor/02_device_front.png`
- 의존: UP-VIS-05
- 남은 문제: 파티클이 들어오면 재검증해야 한다

## 2.13 AUD — 사운드

### UP-AUD-01 — 위험 단계별 오디오 레이어
- 분류: Required · 출처: PRD §8.2, §8.3, §8.4
- 상태: CONNECTED · 패스: P2 P3
- 구현: `Scripts/Risk/RiskStateView.cs`(hum), `Scripts/Audio/AudioCueTable.cs`(단계 전이 사건음), 씬 배선
- 접근: 위험 단계를 올린다
- 검증: `WaveBRuntimeProbe` + EditMode 큐 매핑
- 증거: `Logs/waveb_runtime.txt`
- 의존: UP-RISK-01
- 남은 문제: 지속 hum + 사건음이 씬에서 함께 돈다. **위험 단계가 프로브 중 Stable 을 벗어나지 않아** 단계별 차이는 아직 귀로도 계측으로도 확인되지 않았다

### UP-AUD-02 — 룰렛 사운드 10종
- 분류: Required · 출처: N08 §16.4 (레버 / 칸 공개 / 영혼 수확 / 정화 / 직선 / 연결 / 캐스케이드 단계 / 임계점 / 잔류 피해 / 확정)
- 상태: CONNECTED · 패스: P2 P3
- 구현: `Scripts/Audio/` 5파일 + 씬 `AudioDirector` 배선
- 접근: 스핀을 돌린다
- 검증: `Ascend/Run All EditMode Tests` 13건 + `WaveBRuntimeProbe` 재생 카운터
- 증거: `Logs/waveb_runtime.txt`
- 의존: UP-CORE-11
- 남은 문제: **백로그가 자기 성과를 과소 보고하고 있었다 — 룰렛 10종은 전부 울렸다.** 독립 감사의 도달성 논증: `AudioCueKind` 는 열거자 16개 중 `None=0` 을 빼면 15종이고 `AudioDirector.cs:445-446` 이 `bit > 0` 이라 15종만 기록된다. 그중 `PassengerVoice`(23)는 `AudioCueTable.TryMap` 이 절대 만들지 않고 `PlayPassengerVoice`(:359)의 **호출자가 0곳**이라 도달 불가다. 즉 **도달 가능한 최대치가 14** 인데 실측이 14종이다 → **도달 가능한 전부가 울렸고 룰렛 10종(kind 1~10)이 필연적으로 포함된다.** 안 난 것은 「2종」이 아니라 1종이고, 그것은 「아직 안 났다」가 아니라 구조적 미구현이다. **그럼에도 VERIFIED 로 올리지 않는 이유 둘:** ① 그 결론은 감사자가 열거형과 호출자를 따로 훑어 증명한 것이지 **산출물이 말해 주는 것이 아니었다** — 하네스가 `BitCount` 만 찍었다. → **고쳤다**: 이제 울린/안 울린 종류 **이름**을 함께 찍는다. ② 요구의 나머지 절반 「10종이 서로 **다른 소리로 들리는가**」는 여전히 미검증이다 — `AudioTests.cs:133-149` 는 큐 종류 매핑 검사이지 구운 클립의 청각 차이 검사가 아니다

### UP-AUD-03 — 과수확 정적 구간 (0.3~0.7초)
- 분류: Required · 출처: PRD §7.3(4)
- 상태: CONNECTED · 패스: P2 P3
- 구현: `Scripts/Audio/SilenceWindow.cs`, `AudioDirector`, `Scripts/Run/OverharvestApproachBridge.cs` — 씬 배선 완료
- 접근: 과수확 레버에 손을 올린다
- 검증: `WaveBRuntimeProbe` 의 정적 게인 타임라인
- 증거: `Logs/waveb_runtime.txt`
- 의존: UP-POWER-06, UP-AUD-01
- 남은 문제: **게인이 실제로 떨어진다** — 접근 0.25초 후 0.000, 1.45초 후 1.000 복귀. 남은 것은 플레이어가 실제로 레버를 조준했을 때의 발동 — 프로브는 접근 사건을 직접 넣었고 `IsApproaching=False` 였다(조준 대상 없음)

### UP-AUD-04 — 승객 비언어 음성
- 분류: Required · 출처: PRD §9.3
- 상태: CONNECTED · 패스: P3
- 구현: `Scripts/Audio/ProceduralClipFactory.cs`(PassengerVoice — 포먼트 2개, 승객 인덱스로 피치 변화)
- 접근: 승객이 반응할 때
- 검증: `Ascend/Run All EditMode Tests` → 큐 종류 분기
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-NPC-04
- 남은 문제: 합성기는 있으나 승객 반응과 이어지지 않았다 (`UP-NPC-04` 선행)

### UP-AUD-05 — AudioMixProfile / 오디오 압축 구분
- 분류: Required · 출처: PRD §13.4, §14.3
- 상태: CONNECTED · 패스: P2
- 구현: `Scripts/Data/Profiles/AudioMixProfile.cs` + `.asset` + `Scripts/Audio/AudioDirector.cs` 의 `ChannelVolume`·`ToMixChannel`
- 접근: 해당 없음
- 검증: `TenFloorAutoPilot` → 「오디오가 AudioMixProfile 을 읽는다」
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-AUD-01
- 남은 문제: `AudioMixProfile` 주입은 검증됐다(폴백이면 「인스펙터 슬라이더」로 찍히므로 통과하지 않는다). **요구의 나머지 절반인 오디오 압축 구분이 0건이다** — `.preset` 파일 0개, `AudioImporter`/`compressionFormat`/`loadType` 참조 0건, 프로파일에 압축 필드 없음

## 2.14 TECH — 엔지니어링 목표

### UP-TECH-01 — 게임 규칙과 프레젠테이션 분리
- 분류: Required · 출처: PRD §13.5, N08 §3.3, §24
- 상태: VERIFIED · 패스: P1 P2
- 구현: `Scripts/Spin/`(순수 C#) vs `Scripts/View/`(MonoBehaviour)
- 접근: 해당 없음
- 검증: EditMode 테스트가 씬 없이 통과함
- 증거: `Logs/editmode_tests.txt`
- 의존: 없음
- 남은 문제: 없음

### UP-TECH-02 — 씬 오브젝트 이름 검색 금지
- 분류: Required · 출처: PRD §13.5, N08 §3.3
- 상태: VERIFIED · 패스: P2
- 구현: Inspector 참조 + `FindAnyObjectByType` 폴백, **`tools/audit-scene-lookups.ps1`**(정적 감사)
- 접근: 해당 없음
- 검증: `powershell -File tools/audit-scene-lookups.ps1` → exit 0 · 「런타임 코드의 이름 기반 조회 0건」
- 증거: `Logs/scene_lookup_audit.txt`
- 의존: UP-TECH-01
- 남은 문제: **PRD §13.5 가 금지한 이름 기반 조회는 런타임 코드에 0건이며, 이제 자동으로 검사된다**(직전에는 확인 수단 자체가 없었다). 이름으로 찾는 17건은 전부 `Assets/Editor/` 의 씬 빌더이며 자기가 만든 오브젝트를 되찾는 코드라 빌드에 들어가지 않는다 — 위반으로 세지 않는다. **남은 부채는 다른 것이다**: 타입 기반 `FindAnyObjectByType` 폴백이 런타임에 112건 있고(그중 71건이 테스트·프로브 하네스), 실행 순서 의존과 조용한 null 을 남긴다. 이것은 §13.5 의 「의존성은 Inspector 참조 또는 명시적 초기화로 주입한다」 쪽이며 `-Strict` 로 셀 수 있다

### UP-TECH-03 — 필수 참조 누락 시 개발 빌드에서 즉시 오류
- 분류: Required · 출처: PRD §13.5, N08 §3.3
- 상태: CONNECTED · 패스: P2 P4
- 구현: `Scripts/Diagnostics/SceneWiringValidator.cs` + `Scripts/Diagnostics/RequiredReferenceAttribute.cs`
- 접근: 해당 없음
- 검증: `Logs/tenfloor_playmode.txt` 「씬 배선」 검사
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-TECH-01
- 남은 문제: **백로그가 실물이 아닌 파일을 가리키고 있었다 (2026-08-02 독립 감사 + 적대적 반박으로 정정).** 「죽은 구현 · 런타임 경로가 없다」는 `Scripts/Player/PlayerSetupValidator.cs` 에 대해서는 **지금도 참**이지만, 그 파일은 더 이상 이 항목의 구현이 아니다. 실제 구현은 `Scripts/Diagnostics/SceneWiringValidator.cs`(251줄)이고 **런타임 경로가 실재한다** — `:84-90` 이 `#if DEVELOPMENT_BUILD || UNITY_EDITOR` 안의 `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` 다(씬 오브젝트가 아니므로 「검사기 자신이 배선에서 빠지는」 실패 모드가 없다). 껍데기도 아니다 — 상속 체인 순회·필드 캐시·박싱된 `UnityEngine.Object` 의 `==` 오버로드 함정 처리까지 들어 있다. **「죽은 구현」 가능성은 로그로 배제됐다**: `Logs/Editor.log` 에 「[배선] 필수 참조 이상 없음 — 검사 5필드 / 컴포넌트 73~75개」가 **22회** 찍혀 있고 그 문장은 `Validate(true)` 분기에서만 나오는데 `TenFloorAutoPilot` 은 `Validate(false)` 만 두 번 부른다 — 22줄 전부 자동 실행의 산물이다. 단정도 공허하지 않다: 필드 수 단정이 「결함 0건」의 공허함을 막고, 일부러 빈 참조를 심는 **음성 대조가 실제로 4건을 잡았다**. **VERIFIED 를 막는 것은 둘이다.** ① 요구가 「**개발 빌드**」인데 `Assets/Editor/WindowsBuildTask.cs:85` 가 `BuildOptions.None` 이라 `DEVELOPMENT_BUILD` 가 정의되지 않고, 현존 `Builds/Windows/Upandup_DDD.exe`(8/1 03:19)는 이 소스(8/1 19:30)보다 **낡아 코드가 들어 있지도 않다** — 모든 증거가 에디터 쪽이다. ② 표시 범위가 좁다. 이번 세션에 `FirstPersonController`(2)·`CrosshairInteractor`(1)·`CrosshairView`(2) 에 `[RequiredReference]` 를 붙여 씬에서 **실측 5필드 → 10필드**가 됐다(기존 `CollapseSequence` 1 + `RouletteInteractionBridge` 4). **「17필드」는 오기다** — 통합 보고서가 소스의 속성 개수를 잘못 세었고 내가 그것을 회귀 하한으로 옮겨 적었다가 PlayMode 단정에 걸렸다(그 경위는 `TOPDOWN_PROGRESS.md`). `[SerializeField]` 보유 런타임 파일 50개에 비하면 5클래스는 여전히 일부다. ③ 감사가 덧붙인 것: `Debug.LogError` 발화 경로 자체는 어떤 로그에서도 한 번도 실행된 적이 없다(Editor.log 22줄 전부 정상 분기) — 「즉시 **오류**」의 오류 쪽이 미실증이다

### UP-TECH-04 — 1080p 목표 90 FPS / 하드 플로어 60 FPS
- 분류: Required · 출처: PRD §13.1, §17.4
- 상태: VERIFIED · 패스: P4
- 구현: `Scripts/Run/Tests/HeroSlicePerfProbe.cs`, `LoadedCriticalPerfProbe.cs`
- 접근: 해당 없음
- 검증: 프로브 실행 → `Logs/loaded_critical_perf.txt`
- 증거: `Logs/loaded_critical_perf.txt`
- 의존: UP-PLAT-04
- 남은 문제: **프로브가 이제 스스로 경고한다.** `loaded_critical_perf.txt` 가 각 조건마다 「⚠ 중앙값이 120 Hz 상한(8.33 ms)에 붙어 있다 — 중앙값은 비용이 아니라 상한이다. 조건 비교는 95%·최악으로만 한다」를 찍는다. 즉 **중앙값으로는 90 FPS 목표를 판정할 수 없다**는 사실이 산출물에 박혀 있다. 실측(1920×1080) — 중앙 8.33 ms 고정, 95% 8.38~8.44 ms, 최악 8.53~8.57 ms. 95%·최악도 8.6 ms 아래이므로 **116 FPS 이상**이지만, 상한에 눌린 분포에서 나온 값이라 「90 FPS 목표 달성」의 근거로 쓸 수 없다. **원인은 에디터 게임 뷰가 디스플레이에 동기되는 것**이고, vSync 0 · targetFrameRate −1 로도 안 풀린다. **푸는 길은 빌드에서 재는 것뿐이다** — `Builds/Windows/Upandup_DDD.exe` 는 이미 있다. 에디터 루프가 빠지면 바닥(`UP-TECH-05` 의 8,805 B)도 같이 내려갈 것이다
- **SKELETON → VERIFIED 근거 (2026-08-02, 빌드 실측).** 에디터로는 판정 불가였다 —
  네 조건 전부 중앙 8.33 ms 로 **120 Hz 상한에 붙어** 있었고 그건 비용이 아니라 상한이다.
  그래서 빌드하고 **플레이어 안에서** 쟀다 (`Scripts/Run/Tests/PlayerPerfProbe.cs`,
  `-ascend-perf` 인자로만 깨어난다 · `DEVELOPMENT_BUILD` 밖에서는 컴파일도 안 된다).

  ```
  해상도 1920×1080 · vSync 0 · targetFrameRate -1
  워밍업 120 프레임 버림 · 측정 600 프레임
  중앙 8.33 ms / 95% 8.45 ms / 최악 9.13 ms (109 FPS)
  90 FPS(11.11 ms) 초과 프레임 0/600
  60 FPS(16.67 ms) 초과 프레임 0/600
  ```

  **목표 90 FPS·하드 플로어 60 FPS 둘 다 충족.** 판정을 평균이 아니라 **초과 프레임 수**로
  한 이유 — 평균이 목표를 넘어도 한 프레임이 16.67 ms 를 넘으면 그 순간 끊긴 것이고
  요구가 금지하는 것이 그 끊김이다. **최악 프레임조차 9.13 ms** 다.
- 증거: `Logs/player_perf.txt` · `Logs/build_report_dev.txt` (result Succeeded · 0 errors)
- **남은 의심 (숨기지 않는다)**: vSync 를 껐는데도 중앙값이 정확히 8.33 ms 다 —
  드라이버·컴포지터 상한이 남아 있을 수 있다. **판정 자체는 성립한다**(최악 9.13 ms).
  다만 **여유가 얼마인지는 이 측정이 말하지 못한다.**

### UP-TECH-05 — 워밍업 후 매 프레임 0 B GC Alloc
- 분류: Required · 출처: PRD §13.2, §17.4
- 상태: CONNECTED · 패스: P4
- 구현: `Scripts/Run/Tests/LoadedCriticalPerfProbe.cs`
- 접근: 해당 없음
- 검증: 프로브의 GC/프레임 항목
- 증거: `Logs/loaded_critical_perf.txt`
- 의존: UP-TECH-04
- 남은 문제: **숫자가 틀렸다 — 격차는 10 KB 가 아니라 약 1.6 KB 다.** 같은 기기·같은 카운터로 잰 대조군(`heroslice_perf.txt` 「게임 코드 전부 끄고 60초」)의 바닥이 **8,805 B/프레임(중앙)**이고, `loaded_critical_perf.txt` 네 조건 중 **둘(9,128·9,127)은 그 바닥보다 낮다.** 실제로 남는 게임 코드 할당은 **1,638 B/프레임**(#1 10,443 − #3 8,805)이며 범위는 `HeroSlicePerfProbe.cs:100-113` 이 한 덩어리로 끄는 8개 컴포넌트다 — **어느 것인지는 아직 모른다.** 그리고 이 1,638 B 는 **게임 코드 전체가 아니라 그 8개의 비용**이다 — 「대조군 = 게임 코드 전부 끔」이 실은 `Disable<>()` **12번의 손 열거**라 `AudioDirector`·`PaperTapePrinterView`·`FloorIndicatorView`·`PassengerReactionView`·`TubeController`×3·`RenderBudgetProbe`·`MemoryTrendProbe` 등 14개가 바닥 안에서 계속 돌았다. **상한은 미측정이다 — 「1.6 KB 만 고치면 된다」로 읽으면 안 된다.** 게다가 보고서의 최대 할당 두 개(205,437 B·402,053 B)는 **하네스 자신의 표본 버퍼**다 (`List<FrameSample>` 4096×48 B / 8192×48 B + 바닥, 오차 24·32 B = 객체·배열 헤더). `ProfilerRecorder.StartNew` 다음 줄에서 버퍼를 만든다(`HeroSlicePerfProbe.cs:357-358`). **측정을 고치기 전에는 위반 여부를 확정하지 않는다.** 전체 분해는 `docs/runtime/GC_ALLOC_ANALYSIS.md`. 다음: ① 버퍼를 recorder 앞으로 + 대조군 arm 추가 + A/B arm 층 단계 정렬 ② 8개를 하나씩 끄는 측정 ③ 빌드에서 1920×1080 재측정
- **빌드 실측으로 수치 확정 (2026-08-02).** 에디터 측정에는 에디터·프로파일러 할당이
  섞여 게임 코드 탓으로 확정할 수 없었다. 빌드에는 그것이 없다.

  ```
  0 B 프레임 0/600 (0.0%)
  GC Alloc 중앙 1,638 B/프레임 / 평균 1,650 B / 최악 8,902 B
  ```

  **요구 미충족이고, 이 1,638 B 는 게임 코드다.**
- **예전 추정이 맞았다.** 위의 「게임 코드 할당 **1,638 B/프레임**(#1 10,443 − #3 8,805)」은
  대조군 **뺄셈으로 추정**한 값이었는데, 빌드에서 **직접 잰 중앙값이 1,638 B 로 정확히 같다.**
  서로 다른 두 방법이 같은 수에 닿았으므로 이 수는 신뢰할 수 있다.
- **다음**: 1,638 B 의 출처 특정. 범위는 `HeroSlicePerfProbe.cs:100-113` 이 한 덩어리로 끄는
  8개 컴포넌트이고 **어느 것인지는 아직 모른다.** 빌드 프로브가 생겼으므로 이제
  **컴포넌트를 하나씩 끄고 빌드에서 재는** 방법이 가능하다 — 에디터 잡음 없이 갈린다.
- **출처를 찾아 고쳤다 (2026-08-02, 빌드 소거 측정).** 정적으로는 못 찾았다 —
  `Update`/`LateUpdate` 를 가진 파일이 48개고, 코드를 읽어 「여기일 것 같다」로
  고르는 방식으로 이 세션에서 네 번 틀렸다. 그래서 **끄고 재는** 측정을 넣었다
  (`PlayerPerfProbe.Ablate` — 씬의 `Ascend` 네임스페이스 MonoBehaviour 를 하나씩
  끄고 GC 중앙값 차이를 본다. 목록을 손으로 적지 않는다 — 적히지 않은 것이
  구조적으로 상쇄되는 맹점을 이 저장소가 이미 한 번 겪었다).

  결과는 한 줄이었다: **`SpinBoardView` 1,638 B — 전부.**
- **원인**: `ApplyHighlights()` 가 매 프레임 **무조건** 돌며 슬롯마다
  `GetPropertyBlock`/`SetPropertyBlock` 을 걸었다. 9칸 × 3심볼 = 27 슬롯,
  27 × 약 60 B ≈ 1,620 B 로 측정값과 맞는다. 하이라이트는 정화 연출 중에만
  움직이므로 **대부분의 프레임에서 같은 값을 다시 쓰고 있었다.**
- **고침**: 직전에 칠한 값을 기억하고 **바뀐 것이 없으면 통째로 건너뛴다.**
  부동소수 허용오차 0.0005 를 둔 이유는 감쇠가 0 에 점근하며 마지막 몇 프레임이
  1e-8 씩 달라지기 때문이다 — 그걸 「바뀌었다」로 세면 연출이 끝난 뒤에도 계속 칠한다.
- **측정 (빌드 · 워밍업 720 프레임 · 측정 600 프레임)**

  | | 고치기 전 | 고친 뒤 |
  |---|---|---|
  | GC 중앙 | 1,638 B/프레임 | **0 B/프레임** |
  | 0 B 프레임 | 0/600 (0.0%) | **575/600 (95.8%)** |
  | 소거 측정 기준선 | 1,638 B | **0 B** |

- **아직 VERIFIED 로 올리지 않는다.** 25 프레임이 남았고 최악이 26,226 B 다.
  중앙값이 0 이고 소거 기준선도 0 이므로 **매 프레임 비용은 사라졌지만**,
  요구 문장은 「매 프레임 0 B」이고 25 프레임은 0 이 아니다.
  그 25 개가 스핀·캐스케이드 같은 **이벤트성 할당**인지, 아니면 또 다른 상시 누수인지
  **아직 분리하지 않았다.** 분리 전에는 올리지 않는다.
- **다음**: 스파이크 프레임의 시점을 기록해(스핀 인덱스·연쇄 단계와 대조) 이벤트성인지 확인한다.
  이벤트성이면 요구 해석을 `PENDING_DECISIONS` 로 올린다 — 「매 프레임」이 이벤트 프레임까지
  포함하는지는 문서가 답하지 않는다.

- **정정 (2026-08-02) — 앞 줄의 「0 B 가 됐다」는 과대 보고였다.**
  같은 빌드를 두 번 돌렸더니 결과가 정반대로 나왔다.

  | 실행 | GC 중앙 | 0 B 프레임 |
  |---|---|---|
  | 1 | 1,638 B | 0/600 |
  | 2 | **0 B** | **595/600 (99.2%)** |

  **측정이 재현되지 않는다.** 측정 시점의 게임 상태(결과판이 도는 국면인가 아닌가)가
  통제되지 않아서다. 앞서 「1,638 → 0」이라고 적은 것은 **유리한 실행 하나를 결론으로
  삼은 것**이고, 이 저장소가 반복해 온 실패와 같은 형태다. 취소한다.
- **시도한 것 셋과 각각의 결과**
  1. **값이 안 바뀌면 건너뛰기** — 유휴 국면에서만 0 B. 연출 중에는 그대로 1,638 B.
     원인이 「얼마나 자주 부르나」가 아니었다.
  2. **`GetPropertyBlock` 제거** — 두 번 실행 모두 1,638 B. 그 호출은 할당원이 아니었다.
  3. **점등 세기를 32 단계로 양자화** — 실행 2 에서 99.2% 가 0 B 가 됐으나
     실행 1 은 0% 였다. **효과는 있어 보이지만 재현이 안 된다.**
- **진짜 막고 있는 것은 측정의 비결정성이다.** 실행마다 답이 다른 계기로는
  이 요구를 충족이라고도 미충족이라고도 판정할 수 없다.
  **다음은 코드 수정이 아니라 측정 통제다** — 프로브가 측정 전에 게임을 고정된 상태로
  몰아넣어야 한다(예: 스핀 한 번을 강제하고 연출이 끝난 뒤부터 재기).
  그 전에는 네 번째 수정 시도를 하지 않는다.

- **해결 (2026-08-02) — 원인은 `ClearAll()` 이었다. 재현 3/3.**

  | | 고치기 전 (3회) | 고친 뒤 (3회) |
  |---|---|---|
  | 안정화 | **안 됨** (3,600 프레임 대기 실패) | **59 프레임 만에 도달** |
  | GC 중앙 | 1,638 B/프레임 | **0 B/프레임** |
  | 0 B 프레임 | 0/600 (0.0%) | **599/600 (99.8%)** |

  세 실행이 소수점까지 같다. 앞서 실행마다 답이 달랐던 문제도 함께 사라졌다.
- **어떻게 찾았나 — 컴포넌트 소거로는 부족했다.** 소거 측정이 `SpinBoardView` 를
  1,638 B 전부로 지목했고, 나는 그 안에서 가장 그럴듯한 `ApplyHighlights` 를 세 번 고쳤다.
  **셋 다 효과가 0 이었다.** 컴포넌트 안을 다시 갈라 재고 나서야 갈렸다:

  ```
  정상                   1638 B
  ApplyHighlights 만 끔  1638 B  (차이 0)   ← 내가 세 번 고친 곳
  Update 전체 끔            0 B  (차이 1638)
  ```

  **「가장 그럴듯한 곳」을 고르는 것이 세 번 다 틀렸다.** 측정 단위를 한 단계 더
  잘게 쪼갠 뒤에야 맞는 곳이 나왔다.
- **원인**: `Update` 는 층 세션이 없거나 스핀이 0 이면 `ClearAll()` 로 간다.
  그런데 `ClearAll()` 이 끝에서 `_lastSpinCount = -1` 로 되돌려
  **다음 프레임의 「바뀐 게 없으면 건너뛴다」 검사를 반드시 실패시켰다** —
  스스로 자기를 매 프레임 다시 부르는 구조였다.
  비용의 정체는 `SetCell` 안의 `child.name` 이다. Unity 의 `Object.name` 게터는
  **호출마다 새 문자열을 만든다.** 9칸 × 자식 × 약 50 B 가 1,638 B 와 맞는다.
- **고침**: `_cleared` 플래그. 이미 비어 있으면 즉시 반환하고, 판을 다시 그릴 때 푼다.
- **VERIFIED 가 아니라 CONNECTED 인 이유**: 600 프레임 중 **1 프레임**이 7,264 B 로 남았다.
  단발이고 위치가 매 실행 같지만, 「매 프레임 0 B」라는 문장은 그 1 프레임도 금지한다.
  그것이 이벤트성 할당인지 확정하고 요구 해석을 정리한 뒤에 올린다.

### UP-TECH-06 — 오브젝트 풀링 (파티클·심볼·사운드)
- 분류: Required · 출처: PRD §13.2, §17.4
- 상태: CONNECTED · 패스: P4
- 구현: `Scripts/Perf/ObjectPool.cs`(이중 반환 감지 포함), `ComponentPool.cs`, 소비처 `Scripts/Player/CrosshairInteractor.cs`
- 접근: 해당 없음
- 검증: `Ascend/Run All EditMode Tests` → 풀링 20건 (prewarm·재사용·이중 반환·maxSize 초과)
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-TECH-05
- 남은 문제: **소비처 0곳 → 1곳 (2026-08-02).** `GC_ALLOC_ANALYSIS.md` §7 이 좁혀 놓은 유일한 지점 `CrosshairInteractor.ApplyHighlight` 를 풀 기반으로 바꿨다 — 조준 대상 전환당 렌더러마다 `MaterialPropertyBlock` 2개 + `RendererState` 1개 + `Renderer[]` 1개가 사라졌다(`readonly struct` + 재사용 `List` + `ObjectPool` 예열 8·상한 32). **동작은 보존했다** — 하이라이트 색 계산·복원 순서·`OnDisable` 정리가 동일하다. 위험 둘을 명시적으로 막았다: 반환 시 `Clear()`(잊으면 앞 대상 색이 다음 대상에 실린다), 렌더러가 파괴돼도 블록은 반드시 반환(안 하면 풀이 계속 새로 만들어 **풀을 넣은 의미가 조용히 사라진다**). `PerfTests` 에 소유권 계약 4건 — 21주기 후 `CreatedCount` 정지 / 꺼낸 블록이 비어 있는가 / **해제 두 번이면 이중 반환으로 잡히는가** / 회수 누락이 통계에 드러나는가. **VERIFIED 가 아닌 이유 둘**: ① **GC 실측을 못 했다** — 「할당하는 코드가 사라졌다」는 코드 수준 근거뿐이고 「할당이 줄었다」는 측정이 없다 ② **요구 문구의 세 대상(파티클·심볼·사운드)은 여전히 풀이 없다.** 이것은 미루기가 아니라 **측정에 근거한 결정**이다 — 같은 분해가 나머지 할당 후보를 전부 문자열·서식(`InstrumentPanelView` 의 `AppendFormat` 박싱, `SpinPresenter.DescribeStep`, `PaperTapePrinterView`)으로 지목했고 **풀은 그 답이 아니다.** PRD 가 셋을 열거한다는 이유만으로 넣으면 측정이 부정한 곳에 코드를 늘리는 것이고, 다음 사람이 「풀을 넣었는데 왜 0 B 가 안 되지」를 다시 조사하게 된다. 하이라이트는 **매 프레임이 아니라 대상 전환 시**에만 도는 경로라 `UP-TECH-05` 의 1,638 B 중 일부만 건드린다 — 이 변경으로 0 B 가 되지 않는다. **풀이 답인 지점은 하나뿐이다.** `ObjectPool`/`ComponentPool` 은 완성돼 있고 소비처가 0곳인데, GC 분해 결과 풀로 해결되는 후보는 `CrosshairInteractor.ApplyHighlight`(조준 대상 전환당 렌더러마다 `MaterialPropertyBlock` **2개** + `RendererState` 1개, ≈500 B 추정) **하나**다. 나머지 할당 후보는 전부 문자열·서식(`InstrumentPanelView` 의 `AppendFormat` 박싱, `SpinPresenter.DescribeStep`, `PaperTapePrinterView`)이라 **풀이 답이 아니다.** 파티클·심볼·사운드에 풀을 넣는다고 프레임당 0 B 가 되지 않는다 — `UP-TECH-05` 의 1,638 B 는 그쪽에서 나오는 게 아니다. 근거: `docs/runtime/GC_ALLOC_ANALYSIS.md` §4·§7

### UP-TECH-07 — 렌더링 예산 측정 (드로우콜·SetPass·오버드로우)
- 분류: Required · 출처: PRD §13.3
- 상태: CONNECTED · 패스: P4
- 구현: `Scripts/Perf/RenderBudgetProbe.cs` + 씬 배선 + `TargetHardwareProfile`·`VisualQualityProfile` 주입
- 접근: 해당 없음
- 검증: PlayMode 런 중 자동 수집 → `Logs/render_budget.txt`
- 증거: `Logs/render_budget.txt`
- 의존: UP-TECH-04
- 남은 문제: **처음으로 기준 해상도에서 쟀다 (2026-08-01).** `Logs/loaded_critical_perf.txt` 머리말이 이제 「해상도 **1920×1080** / vSync 0 / targetFrameRate −1」이다 — 이전 측정은 전부 816×714(화소 28%)였고 `TECH_SPEC.md` §13 기준 밖이었다. 게임 뷰를 캡처·측정 전에 고정하게 만든 것이 이 문제까지 같이 풀었다. **파티클 추가 비용도 쟀다** — GC Alloc 이 10,760 → 10,807(**+47 B/프레임, +0.44%**), 9,128 → 9,173(**+45 B, +0.49%**). 파티클 5계통(최대 동시 274)이 프레임당 50 B 미만이다. 이는 「8,805 B 바닥은 게임 상태와 무관하다」는 앞선 분해와 일치한다. **남은 것**: ① 드로우콜·SetPass·오버드로우는 이 프로브가 안 잰다 — `render_budget.txt` 쪽 프로브를 1920 에서 다시 돌려야 한다 ② `PD-15`(예산 값)가 미결이라 **넘었는지 판정할 기준이 여전히 없다**. 지금은 숫자를 기록할 뿐 통과/실패를 말할 수 없다

### UP-TECH-08 — 10층 연속 플레이에서 메모리 누적 없음
- 분류: Required · 출처: PRD §17.4
- 상태: CONNECTED · 패스: P4
- 구현: `Scripts/Perf/MemoryTrendProbe.cs` + 씬 배선
- 접근: 해당 없음
- 검증: 10층 런 중 층 경계 샘플링 → `Logs/memory_trend.txt`
- 증거: `Logs/memory_trend.txt`
- 의존: UP-RUN-10
- 남은 문제: **내가 적었던 숫자와 판정이 둘 다 틀렸다.** 독립 감사가 셋을 짚었다. ① 인용한 값(수집 전 1.043 GB → 1014.52 MB, −2.17 MB)이 **디스크에 없다** — `Logs/memory_trend.txt` 는 1.059 GB → 1020.96 MB(회수 63.22 MB), −2.79 MB 다. 덮어써진 앞선 런의 값을 적어 두었다. ② **판정식이 서로 다른 측정을 뺐다** — 기준선 `firstEnd` 는 `_gcBytes[]` 즉 `GC.GetTotalMemory(false)`(수집 전)인데 비교값 `_settledBytes` 는 `GC.GetTotalMemory(true)`(수집 후)다. 첫 층에 아직 안 치워진 쓰레기가 기준선 쪽에 얹혀 **보유 증가를 감소로 보이게 만든다.** 내가 지적한 바로 그 오류를 판정식 한쪽에 남겨 뒀다. ③ **빨간불이 될 단정이 0건이다** — EditMode 「Perf」 21건은 합성 배열로 `MemoryTrend.Analyze` 산수만 검사한다. 실제로 누적돼도 아무것도 실패하지 않는다. **조치**: `MemoryTrendProbe` 에 첫 층 종료에서도 강제 수집한 `SettledBaselineBytes` 를 잡고 `RetainedBytes`(같은 측정끼리 뺀 값)를 노출하도록 고쳤다. 남은 것은 그 값으로 **실패할 수 있는 단정**을 하네스에 붙이는 것. 또 `_settled` 가 런 사이 초기화되지 않아 중단된 런의 판정 줄이 다음 보고서에 붙을 수 있다. 출처 표기 `PRD §17.4` 도 틀렸다 — 동결 PRD 는 **§15 에서 끝나고** 「메모리」가 0건이다

### UP-TECH-09 — 가변 요소의 데이터 분리 (PRD §14.1 12항목)
- 분류: Required · 출처: PRD §14.1, §14.3
- 상태: SKELETON · 패스: P4
- 구현: `Data/PrototypeConfig.asset`, `Scripts/Spin/SpinRuleSet.cs`, **`Scripts/Data/Profiles/` 7종**(TargetHardware · Overharvest · DangerFeedback · VisualQuality · AudioMix · Accessibility · RunSummaryTemplate), `Scripts/Npc/PassengerReactionSet.cs`
- 접근: 해당 없음
- 검증: `Ascend/Run All EditMode Tests` → 데이터 프로파일 **44건** (2026-08-02 실측. 「19건」은 낡은 기록이었다). 그중 과적 6건은 값 대조가 아니라 **반증 가능성**으로 건다 — 프리셋과 다른 수치(37/5/3)를 주입하고 요구 전력·허용 중량이 따라오는지 본다
- 증거: `Logs/editmode_tests.txt`
- 의존: 없음
- 남은 문제: **이 항목이 여덟 세션 동안 부풀려 있던 이유가 드러났다 (2026-08-02).** 직전 판본은 「주입까지 끝난 것은 위험 연출·접근성·오디오 믹스·기준 하드웨어·품질 등급 **다섯**」이라 적었으나, **그중 §14.1 의 12항목에 해당하는 것은 「위험 연출」 하나뿐**이다 — 접근성·기준 하드웨어·품질 등급·오디오 믹스는 §14.1 목록에 **없고** §14.3 「권장 데이터 자산 13종」 쪽이다. **「프로파일 에셋 7종을 배선했다」와 「§14.1 12항목을 데이터화했다」를 같은 문장에 넣은 것**이 원인이다. 12행 대조표를 만들어 이제 셀 수 있다. **실측 — 12 중 충족 7, 부분 2, 미충족 3** (2026-08-02 ⑤①③⑦ 완료로 갱신. 직전 실측은 충족 2·미충족 9). **⑦ 도 충족 (2026-08-02)** — `RiskThresholdProfile` 9종이 `RiskStateView` → `RiskEvaluator.Apply` 로 흐른다. **⑧ 과 일부러 갈랐다**: ⑧(`DangerFeedbackProfile`)은 「위험해 **보이는 방법**」이고 ⑦은 「**무엇이** 위험인가」다. 한 에셋에 두면 「연출이 약해 보인다」는 이유로 임계값을 내리는 일이 생기는데 그건 연출 조정이 아니라 난이도 변경이다. `Validate` 가 히스테리시스 역전(이탈 ≥ 진입)·단계 순서 역전·「과수확 점수 < Strain 진입」을 잡는다 — 마지막 것은 `MASTER_PRD.md` §7(과수확은 공간적 사건이다)이 걸려 있는 **불변식**이라 밸런스 취향이 아니다. 출처를 「필드 초기값」과 「코드 프리셋」으로 나눠 찍는다 — 같은 수지만 다른 경로이고, 그 구분이 있어야 「배선했는가」에 답할 수 있다. **①③ 도 충족 (2026-08-02)** — `SpinBalanceProfile` + `SpinBalanceSnapshot` 10종(심볼 가중치 3·패턴 배수 3·정화 최소 개수·연쇄 증분·잔류 2)이 `RunSessionBehaviour` → `RunSession` → `FloorSession` → `PrototypeCurriculum.BuildRules` → `SpinRuleSet.CreateDefault(balance)` 로 흐른다. **연쇄 하드 캡 20 은 일부러 뺐다** — `MASTER_PRD.md` §6 과 `TECH_SPEC.md` §9 가 못박은 값이라 밸런스 다이얼이 아니다. 프로파일에 넣으면 「고쳐도 되는 것」과 「고치면 명세 위반인 것」이 같은 인스펙터에 나란히 놓인다. 테스트가 프로파일 경유 규칙 다발이 여전히 20을 드는지 본다 ⑥ 과수확 손실·보상 ✔(`OverharvestProfile.asset` → `RunSessionBehaviour` → `FloorSession`/`AudioDirector`, 폴백이면 `OverharvestSource` 가 다르게 찍힌다) · ⑧ 조명·사이렌·진동·카메라 충격 ✔(`DangerFeedbackProfile` → `RiskStateView`) · ⑤ 과적 무게 **✔ 충족 (2026-08-02)** — `WeightProfile` + `WeightSnapshot` 이 생겼고 `RunSessionBehaviour` → `RunSession` → `FloorSession` 으로 주입된다. 판정식도 같이 옮겼다(`WeightSnapshot.RequiredPowerFor`) — 값만 옮기고 식이 상수를 계속 읽으면 데이터화가 아니다. 나머지 아홉(①심볼 가중치 ②계약 출현률 ③패턴 배수 ④층별 요구 전력 ⑦위험 임계값 ⑨승객 대사 ⑩파티클 밀도 ⑪애니메이션 속도 ⑫재질 색·발광)은 전부 코드 상수·정적 배열이다. ⑫ 는 프로파일 자체가 없다. **⑩ 이 부분 완료됐다 (2026-08-02)** — `AmbientParticleDirector` 의 하드코딩 switch(24/48/80/120)가 사라지고 `PresentationProfile` 스냅샷에서 읽는다. 값 대조가 아니라 **반증 가능성**으로 걸었다: 에셋 기본값이 코드 프리셋과 같아서 값만 보면 배선을 떼어내도 숫자가 그대로다 — 그래서 `PresentationSource` 공개 프로퍼티(폴백이면 「코드 프리셋」)와 에셋 배열을 `[7,9,11,13]` 으로 덮어써 네 단계가 따라 움직이는지 보는 테스트를 넣었다. **다만 ⑩ 중 「상한」만 데이터화됐다** — 배출률 램프·단계별 보간계수·파티클 5종의 색·크기·속도·수명은 여전히 코드에 있고 프로파일에 필드가 없다. **⑤ 에 적어 둔 「합치면 된다」가 틀렸다 (2026-08-02 정정).** 직전 판본은 「값이 이미 양쪽에 같으니 가장 싸다」고 적었으나 **실측은 다르다** — `PrototypeConfig.asset` 의 `allowedWeight` 는 **8** 이고 `FloorSession` 은 **100** 이다. 그리고 이건 어긋난 게 아니라 **단위가 다른 것**이다: 100 → 8 은 `682bbd0`(「T-04, T-06: 승객 5종·무게 선택과 과적 사고」)의 의도적 변경으로, 그 시절 무게는 승객 수에 가까운 단위였다. 지금 10층 경로는 kg 단위이고 짐꾼 보너스를 더한 **130** 이 테스트와 캡처 리그 전반에 박혀 있다. 시키는 대로 하나로 합쳤으면 허용 중량이 100에서 8로 떨어져 **전 층이 즉시 과적**이 되고 요구 전력에 1.5배가 상시로 걸려 10층 밸런스가 통째로 무너졌을 것이다. 「가장 싼 항목」으로 분류돼 있던 것이 실제로는 밸런스 파괴 작업이었다. **그래서 합치지 않고 라이브 경로에 자기 프로파일을 줬다.** `PrototypeConfig.allowedWeight: 8` 은 레거시 경로의 값으로 그 자리에 남는다 — 그 경로를 지우는 것은 `UP-TEST-11` 의 일이다. 이 교훈은 일반적이다: **두 상수가 같은 이름을 가졌다는 것이 같은 양이라는 뜻은 아니다.** 합치기 전에 이력을 본다.

**⑨ 도 기록이 틀렸다 (2026-08-02 정정).** 「전부 코드 상수·정적 배열」로 묶여 있었으나 **실측은 다르다** — `PassengerReactionSet.asset` 은 존재하고 11종이 채워져 있으며 **씬이 물고 있다**(`Prototype_Elevator.unity:10098`). 진짜 결함은 훨씬 좁고 정확하다: **그 에셋에 `Line` 필드가 아예 직렬화돼 있지 않다** (`Line:` 0개 / 항목 11개). `Line` 은 에셋이 만들어진 뒤에 생긴 필드라, 11종 전부가 조용히 코드 기본 대사로 폴백한다. 화면에는 대사가 나오므로 아무도 눈치채지 못한다. **고치는 방법은 코드가 아니다** — 인스펙터에서 그 에셋의 톱니바퀴 ▸ Reset 을 한 번 누르면 대사·대조까지 다시 직렬화된다(`PassengerReactionSet.cs` 하단이 이미 그렇게 적어 뒀다). `.asset` 은 단일 소유 파일이라 **씬 오너의 일**이고, 그래서 코드 쪽에서는 상태를 고정하는 검사 4건만 넣었다 — 그중 하나는 **0 또는 11 만 허용**한다. 그 사이 값은 절반만 고친 것이고 그 상태가 가장 나쁘다: 일부 사건만 데이터에서 오면 「대사를 데이터로 옮겼다」가 참인지 거짓인지 아무도 말할 수 없다. **상태: 부분 — 코드 쪽 할 일 없음, 씬 오너 클릭 1회 대기.**

**⑪ 도 충족 (2026-08-02)** — `PresentationProfile` 에 ⑪ 칸이 **이미 있었으나 소비처가 0곳**이었다(「만들어졌고 아무도 안 읽는다」). 진짜 출처는 씬도 인스펙터도 아닌 `SpinPresenter.ApplyTempoPreset` 의 **코드 switch** 였다 — Awake·OnValidate 마다 여덟 중 여섯을 덮어썼다. 이제 프로파일이 물려 있으면 그쪽이 이기고(`RiskStateView` 규약과 같다) `TempoSource` 가 어느 쪽인지 찍는다. **`SealedHold` 를 프로파일에 새로 넣었다** — 템포 블록은 여덟인데 프로파일이 일곱만 들고 있어서, 그대로 배선했으면 일곱은 에셋·하나는 씬인 절반짜리가 됐을 것이다. `Tempo` 열거형은 남긴다: 하네스가 캡처 전에 `Readable` 로 바꿔야 하므로 코드가 이름 붙은 프리셋을 알아야 한다. 씬 직렬화 값·`Standard` 분기·프로파일 프리셋이 셋 다 0.22/0.32/0.45/0.55/0.30/0.40 으로 일치하므로 **동작 중립**이다.

**다음**: 남은 미충족은 셋(②④⑫)이고 ⑨ 는 씬 오너 대기다. ⑫ 는 `PresentationProfile` 에 표면 색·발광 칸이 **이미 있으나 소비처를 세어 보지 않았다** — ⑪ 과 같은 함정일 수 있으니 만들기 전에 셀 것. 그리고 여전히 머티리얼을 건드려야 해서 **씬 소유자가 필요하다**. **②④ 는 `FloorPlan._tenFloors` 에 있는데 그 배열은 각 숫자의 근거가 주석으로 붙어 있다** — 「2층 저항 배율 1.6 은 기본 밀도에서 직선이 스핀당 0.08회라 5스핀이면 기대 0.4회, 즉 셋 중 둘이 직선을 가르치는 층에서 직선을 못 본다」 같은 것. 인스펙터로 옮기면 그 근거가 끊긴다. 값만 옮기지 말고 근거를 어디에 둘지 먼저 정할 것

## 2.15 TEST — 테스트·텔레메트리·증거

### UP-TEST-01 — EditMode 자동 테스트 (PRD §16.1의 12항목)
- 분류: Required · 출처: PRD §16.1, §17.4, N08 §19.1
- 상태: VERIFIED · 패스: P1 P4
- 구현: `Scripts/Spin/Tests/SpinEngineTests.cs`, `Run/Tests/RunTests.cs`, `Build/Tests/BuildTests.cs`, `Risk/Tests/RiskEvaluatorTests.cs`
- 접근: 해당 없음
- 검증: `Ascend/Run Self Tests` → `합계: N PASS / 0 FAIL`
- 증거: `Logs/editmode_tests.txt`, `.claude/state/last-selftest.txt`
- 의존: UP-PLAT-07
- 남은 문제: 없음

### UP-TEST-02 — PlayMode 테스트 (N08 §19.2의 7항목)
- 분류: Required · 출처: PRD §17.4, N08 §19.2
- 상태: VERIFIED · 패스: P1 P4
- 구현: `Scripts/Run/Tests/TenFloorAutoPilot.cs`, `Assets/Editor/PlayModeSmokeTest.cs`
- 접근: 해당 없음
- 검증: `Ascend/Ten Floor PlayMode` → `결과: N PASS / 0 FAIL / 콘솔오류 0건`
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-TEST-01
- 남은 문제: 없음

### UP-TEST-03 — 서로 다른 고정 시드 최소 3개
- 분류: Required · 출처: PRD §17.6
- 상태: VERIFIED · 패스: P4
- 구현: `Scripts/Run/Tests/TenFloorAutoPilot.cs`
- 접근: 해당 없음
- 검증: `Logs/tenfloor_playmode.txt` 「서로 다른 완주 시드가 최소 3개」
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-TEST-02
- 남은 문제: 없음

### UP-TEST-04 — 치명적 콘솔 오류 0
- 분류: Required · 출처: PRD §17.1, §17.4
- 상태: VERIFIED · 패스: P1 P4
- 구현: `Scripts/Run/Tests/TenFloorAutoPilot.cs`(콘솔 감시)
- 접근: 해당 없음
- 검증: `Logs/tenfloor_playmode.txt` 「치명적 콘솔 오류 없음」
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-TEST-02
- 남은 문제: 없음

### UP-TEST-05 — 텔레메트리 (스핀별 JSON/CSV · Notion §16.2 11항목)
- 분류: Required · 출처: PRD §4.1(텔레메트리), §16.2, N08 §18
- 상태: CONNECTED · 패스: P2
- 구현: `Scripts/Telemetry/`(SpinTelemetryRecord 20필드 · TelemetryRecorder · TelemetryFileSink · ITelemetrySink)
- 접근: 해당 없음
- 검증: `Ascend/Run All EditMode Tests` → 텔레메트리 17건 (결정론·CSV 열 일치·JSONL 이스케이프)
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-RUN-01
- 남은 문제: **②가 닫혔다 — 8열 추가로 §16.2 누락 5항목을 채웠다.** `cascadeBoards`·`activationOrder`·`residualAbsorbers`·`residualProliferators`·`riskLevel`·`loadout`·`frameTimeMs`·`gcAllocBytes`. 전부 **끝에 붙여** 기존 20열의 위치를 바꾸지 않았다 — `Logs/telemetry/` 의 옛 파일 앞 20열이 그대로 유효하다. **실제 런에서 값이 찬다** (`run_1337_96.jsonl`): riskLevel `Stable`, frameTimeMs `8.3638`, cascadeBoards·activationOrder 채워짐, 총 28필드. **「필드가 정확히 20개다」 테스트를 삭제했다** — `D-20260801-06` 이 「필드 수를 목표로 삼지 않는다」고 **명시 기각한 기준**이었다. 대신 **§16.2 11항목 전단사 대조**로 바꿨고, 뮤테이션으로 실증했다 — 헤더에 `orphanField` 를 끼우면 「§16.2 의 어느 항목도 설명하지 못한다」로 실패한다. 테스트 17 → **28 PASS / 0 FAIL**. **⑪ 「런 종료 원인」은 넣지 않았다** — 스핀 속성이 아니라 스핀마다 「아직 안 끝났다」를 반복하게 된다. 대조표에 `RunScoped = true` 인 **빈 자리로 남기고** 스핀 레코드가 그것을 주장하면 실패하게 고정했다. 감추지 않았다. **남은 것**: 런 단위 레코드(종료 원인) · 제목의 「20항목」 표기 정정 · `Logs/telemetry/` 에 20열/28열 두 스키마가 섞여 있다(앞 20열 호환)

### UP-TEST-06 — 디버그 패널
- 분류: Required · 출처: PRD §4.1, N08 §17 「개발 빌드에서만 기본 활성화」
- 상태: CONNECTED · 패스: P1 P2
- 구현: `Scripts/UI/DebugPanelView.cs`
- 접근: F1
- 검증: `Logs/tenfloor_playmode.txt` 씬 배선 검사
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-RUN-05
- 남은 문제: `Scripts/UI/DebugPanelView.cs` 에 `#if UNITY_EDITOR`·`DEVELOPMENT_BUILD`·`Debug.isDebugBuild` 가 **하나도 없다.** 릴리스 빌드에서 F1 이 그대로 열린다 — N08 §17 「개발 빌드에서만 기본 활성화」 미충족

### UP-TEST-07 — 밸런스 프로브 (시드 대량 시뮬레이션)
- 분류: Required · 출처: PRD §15.3 「측정 가능한 차이 없이 무작정 반복하지 않는다」, §16.3
- 상태: VERIFIED · 패스: P4
- 구현: `Assets/Editor/BalanceProbe.cs`, `CurriculumCoverageProbe.cs`, `Scripts/Sim/RunSimulator.cs`
- 접근: 해당 없음
- 검증: 프로브 실행 → `Logs/curriculum_coverage.txt`
- 증거: `Logs/curriculum_coverage.txt`
- 의존: UP-RUN-02
- 남은 문제: 없음

### UP-TEST-08 — 5연쇄 이상 영상
- 분류: Required · 출처: PRD §17.6 증거 산출물
- 상태: VERIFIED · 패스: P4
- 구현: `Assets/CaptureHarness/GifEncoder.cs`, `Scripts/Run/Tests/SequenceRecorder.cs`, `Scripts/Run/Tests/EvidenceClipRecorder.cs` + 메뉴 `Ascend/Record Evidence Clips`
- 접근: Unity 메뉴 → `Ascend/Record Evidence Clips`
- 검증: `Logs/evidence_clips.txt` 의 연쇄 깊이와 산출 경로
- 증거: `Captures/evidence/cascade_depth5_seed4242_f3.gif`
- 의존: UP-CORE-08
- 남은 문제: **독립 재판정 통과.** 감사자가 142프레임을 직접 꺼내 확인했다 — f0~f135 **전 프레임**에 「연쇄 N단계」 HUD 가 있고 0→1→2→3→4→**5단계**로 **단조 증가**하며 되돌아가는 구간이 없다(이어붙인 필름이 아니라는 뜻). f120 에서 「연쇄 5단계 / 흡수체 직선 3칸 ×2」 판독. 결과판 3열 × 3행 아홉 칸이 전부 프레임 안이다. **내 앞선 기록이 「연쇄 1단계」라고 적었는데 틀렸다** — 프레임 두 장만 보고 필름 전체를 단정했다. 증거가 기록보다 강한, 방향만 반대인 부정확 기록이었다. `Captures/` 는 gitignore 대상이라 이 GIF 는 **기기 종속 산출물**이다 — 다른 기기에서는 `Ascend/Record Evidence Clips` 를 한 번 돌린다

### UP-TEST-09 — Critical → 과수확 → 결과 영상
- 분류: Required · 출처: PRD §17.6 증거 산출물
- 상태: VERIFIED · 패스: P4
- 구현: `Scripts/Run/Tests/EvidenceClipRecorder.cs` (위험 단계 게이트)
- 접근: Unity 메뉴 → `Ascend/Record Evidence Clips`
- 검증: `Logs/evidence_clips.txt` 의 당김 시점 위험 단계
- 증거: `Captures/evidence/overharvest_Critical_seed4242_f2.gif`
- 의존: UP-TEST-08
- 남은 문제: **3차 독립 판정 통과.** 감사자가 90프레임을 전수 확인했다 — f000~015 에 「2층/10 **위험도 위험**(=Critical) · 전력 819/403 203% · 스핀 1/5 · 과수확 **2회**」가 확대 없이 읽히고, f016 한 프레임에 「전력 **727** · 180% · 스핀 **0/5** · 과수확 **3회** · 저장 전력 **−24.0**」로 전부 바뀌며 「연쇄 0단계」 HUD 가 뜬다. 계기 문자열은 구운 텍스처가 아니라 `InstrumentPanelView` 가 `_risk.Level.DisplayName()` 로 매 갱신 붙이는 라이브 값이다. **그러나 승격 조건 셋 중 둘은 완전히 이행되지 않았다.** ① 「레버가 화면 안에서 **움직이는** 것」은 **대체 충족**이다 — 손잡이 회전은 카메라 미세 드리프트와 구분되지 않고(당김 프레임 변화가 잡음 대비 +134화소뿐), 필름에서 읽히는 것은 **덮개 폐쇄와 소등**(하우징 ROI 28.4% 변화)이다. ② 「빈 판 구간 축소」는 **지표상 나빠졌다** — 하드코딩 대기만 12프레임 줄였고 필름의 대부분인 `WaitUnlocked` 57프레임은 손대지 않아, 결과판이 빈 프레임 비율이 41% → **59%** 다. 「셋 다 고쳤다」로 읽으면 안 된다

### UP-TEST-10 — 독립 시각 평가 기록
- 분류: Required · 출처: PRD §1.2 「구현 에이전트가 자신의 결과를 스스로 통과시키지 않는다」, §15.3
- 상태: CONNECTED · 패스: P3 P4
- 구현: `.claude/skills/visual-verify/SKILL.md`, `.claude/agents/visual-critic.md`, `docs/runtime/VISUAL_VERDICT.md`
- 접근: 해당 없음
- 검증: `docs/runtime/VISUAL_VERDICT.md`의 마지막 `VERDICT:` 줄
- 증거: `docs/runtime/VISUAL_VERDICT.md`
- 의존: UP-VIS-06
- 남은 문제: 판정 파일은 생겼으나 **현재 판정이 REJECT**다. 지적 6건이 §5 수정 백로그에 있다

### UP-TEST-11 — 미사용 레거시 코드 정리
- 분류: Required · 출처: PRD §4.2(9종 등급 체계 제외), N08 §22 「임시 에셋은 명확히 `Prototype` 폴더에」, Pass 4
- 상태: SKELETON · 패스: P4
- 구현: `docs/runtime/LEGACY_DELETION_PLAN.md` — 전수 조사 목록과 웨이브 0~9 삭제 순서. **웨이브 0 완료** (`Assets/Editor/PrototypeSelfTest.cs`), **A묶음 1/3 삭제** (`Scripts/Effects/IEffect.cs`)
- 접근: 해당 없음
- 검증: 삭제 또는 `Legacy/` 격리 후 컴파일·테스트 통과
- 증거: `docs/runtime/LEGACY_DELETION_PLAN.md`
- 의존: UP-TEST-01
- 남은 문제: **웨이브 1 도 끝냈다 (2026-08-02).** `Scripts/Player/PlayerSetupValidator.cs`(대체자 `Diagnostics/SceneWiringValidator.cs`)와 `Scripts/Player/InteractablePassenger.cs` 를 지웠다. **전제를 삭제 직전에 직접 재확인했다** — 세 컴포넌트의 `[RequiredReference]` 실재(줄 번호까지), 공개 프로퍼티 외부 호출자 0곳, GUID 가 씬·프리팹·에셋 전체에서 자기 `.meta` 외 0건. 게다가 `WiringDiagnosticsTests.TestPlayerComponentsStayMarked` 가 표시 소실을 감시하는 말뚝으로 서 있다. **소실된 검사가 하나 있다** — 지워진 파일이 `CrosshairInteractor._viewCamera` 에 대해 내던 **경고 1건**. 오류가 아니라 경고였고 `Camera.main` 자동 대체가 검사기보다 늦게 도는 구조라 `[RequiredReference]` 를 붙이면 정상 구성을 결함으로 보고한다 — 되살리려면 씬에서 그 필드가 채워져 있는지 확인이 먼저다. **웨이브 3(씬)·8(`.asset` 23개)은 `PD-13` 승인 전이라 손대지 않았다.** **웨이브 0 도 앞서 끝냈다 (2026-08-02) — 삭제 순서의 지뢰가 제거됐다.** `PrototypeSelfTest.cs:39-44` 의 조기 반환은 레거시 `.asset` 셋이 없으면 `fail=1` 로 끝내 **신 스택 스위트 10종을 통째로 건너뛰고 모든 커밋을 막는** 구조였다. **계획과 다르게 처리했다** — 계획은 「Test1~9 호출과 본문 제거」였으나, 그 셋을 지금 지우면 **아직 빌드에 남아 있는 옛 스택의 커버리지가 웨이브 7 전까지 비어 버린다.** 대신 조기 반환을 `if (legacyAssetsPresent)` 분기로 바꿨다: 에셋이 있는 **지금은 9건이 그대로 돌아 관측 가능한 변화가 0**이고, 에셋이 사라지는 웨이브 8 이후에는 실패가 아니라 `SKIP` 한 줄로 보고서에 **남는다**(합계만 보면 사라진 9건이 안 보이므로 조용히 넘기지 않는다). 삭제는 그것들이 지키는 코드가 사라지는 웨이브 7 과 **같은 커밋**에서 한다 — 그때는 숨기는 것이 아니라 함께 가는 것이다. **A묶음도 시작했다**: `Scripts/Effects/IEffect.cs` 삭제(구현체 0개·GUID 씬/에셋 0건을 **삭제 직전에 재확인**). 남은 A묶음 둘은 다른 작업자가 `Scripts/Perf/`·`Scripts/Player/` 를 소유 중이라 **이번 배치에서 손대지 않았다** — 병렬 소유 규칙이 우선한다. 덧붙여 `ComponentPool.cs` 는 계획 자신이 「레거시가 아니라 아직 안 쓰이는 신규 인프라 · 보류 권장」이라고 적었고 `UP-TECH-06` 이 지금 그것을 쓰게 만드는 중이라 **삭제 대상에서 뺀다.** **목록이 확정됐다. 나머지 삭제는 아직이다.** 독립 조사자가 `Scripts/` 146 + `Assets/Editor/` 17 파일을 GUID 역검색 + 씬 YAML 파싱 + **주석 제거 후** 참조 그래프로 전수 조사했다 (주석만 뒤진 1차는 `PlayerSetupValidator`·`PrototypeUI`·`ComponentPool`·`RunOutcome` 을 「살아 있음」으로 오판했다). 결과 — **삭제 대상 46파일 4,571줄 + `.asset` 23개**, 보존 52파일 약 19,500줄. **규모 정정**: `GapAnalysis.md:205-207` 과 `WINDOWS_SETUP.md:168` 의 「약 5,000줄」은 `Scripts/Sim/` 899줄을 잘못 포함한 값이다 — `Sim/` 은 신 스택 밸런스 시뮬레이터라 남는다. **순서가 중요하다**: 레거시 `.asset` 을 먼저 지우면 `PrototypeSelfTest.cs:38-43` 의 조기 반환이 걸려 신 스택 스위트 10종이 통째로 안 돌고 `1 FAIL` 이 되며, 커밋 게이트가 **모든 커밋을 막는다.** 웨이브 0(자체 검사 편집)이 반드시 먼저다. **전제 하나**: 웨이브 1에서 `PlayerSetupValidator` 를 지우기 전에 `FirstPersonController`·`CrosshairInteractor`·`CrosshairView` 에 `[RequiredReference]` 를 붙여야 검사 손실이 없다 — 현재 이 셋에 속성이 하나도 없다. 되돌릴 수 없는 지점은 웨이브 3(씬)·8(`.asset`) 둘뿐이고 `PD-13` 승인이 전제다

## 2.16 DOC — 문서 정합성

### UP-DOC-01 — Notion PRD §6.1의 정화 규칙을 인접 요구로 개정
- 분류: Required · 출처: `D-20260801-03`, PRD §1.1(이 문서가 최상위)
- 상태: VERIFIED · 패스: P4
- 구현: Notion 페이지 `3ada30cad9c58106b9a8c4ee03dd995c` §6.1·§4.1 (2026-08-02 개정 완료)
- 접근: 해당 없음
- 검증: Notion 원문과 `docs/MASTER_PRD.md`가 일치
- 증거: `docs/runtime/NotionSyncReport.md`
- 의존: UP-CORE-05
- 남은 문제: **개정했다.** §6.1 「위치와 무관하게 기본 정화한다」 → 「3개 이상이고 **서로 인접**하면 정화한다 (직선 3연속 또는 연결 덩어리 4개 이상)」, §4.1 「같은 저항체 3개 이상 기본 정화」 → 「인접했을 때의 정화 (`D-20260801-03`)」. 문구는 새로 쓰지 않고 저장소 동결 스냅샷(`docs/MASTER_PRD.md:78`·`:128-129`)을 그대로 옮겼다 — 두 문서가 「비슷한 말」이 아니라 **같은 문장**이어야 다음 대조가 성립한다. 쓰기 직후 같은 페이지를 다시 `notion-fetch` 해 **원문에서 두 줄을 확인했다**(API 성공 응답을 근거로 삼지 않았다). §3 가설 2번과 §16.1 테스트 항목명의 「3개 기본 정화」는 **고의로 두었다** — 규칙 진술이 아니라 각각 검증 대상 가설과 테스트 이름이고, 저장소 스냅샷도 같은 문장을 그대로 둔다. 전문은 `NotionSyncReport.md` §9. **앞선 세션의 「Notion 쓰기가 권한 계층에서 거부됐다 · 사용자만 풀 수 있다」 기록은 항구적이지 않았다** — 이번엔 거부되지 않았고, 그 기록을 그대로 믿었으면 계속 사용자 대기로 남았을 것이다. **독립 확인 완료 → VERIFIED (2026-08-02).** 구현자와 분리된 확인자가 읽기 전용 `notion-fetch` 로 원본 페이지를 **직접 열어** §6.1·§4.1 두 줄이 실제로 바뀌었음을 확인하고, `docs/MASTER_PRD.md:78`·`:128-129` 와 문장이 일치함을 대조했다. 내 보고를 근거로 삼지 않았다 — 규칙 6 이 요구하는 것이 정확히 그것이다

### UP-DOC-02 — 위험 2단계 이름을 PRD와 일치시킨다 (`Strain` vs `Warning`)
- 분류: Required · 출처: PRD §8.1
- 상태: CONNECTED · 패스: P4
- 구현: `Scripts/Risk/RiskLevel.cs` 의 `Warning` → `Strain` + 참조 10곳 + `RiskEvaluator.StrainEnter`/`StrainExit` + 캡처 `09_risk_strain` + `docs/MASTER_PRD.md` §9 · `TECH_SPEC.md` §8 · `VISUAL_SPEC.md`
- 접근: 해당 없음
- 검증: 열거 값 불변 확인(0/1/2/3) + `RiskEvaluatorTests` 11 PASS + 재캡처 20장
- 증거: `Logs/editmode_tests.txt`, `Captures/TenFloor/09_risk_strain.png`
- 의존: UP-RISK-01
- 남은 문제: **코드 개명은 끝났다. 잔재가 내가 센 것보다 많다.** 독립 감사가 확인한 것 — `RiskLevel.cs` 는 `Stable=0 / Strain=1 / Critical=2 / Collapse=3` 로 값이 보존됐고 `RiskLevel.Warning` 참조는 **0건**이며, 남은 `Warning*` 은 전부 `RiskProfile.WarningColor` 류 (경고**등**)와 `AudioChannel.Warning` 이라 **끊지 않은 판단이 옳다.** 동결 문서 넷(PRD·TECH_SPEC·CURRENT_PHASE·VISUAL_SPEC)도 전부 `Strain` 이고 `09_risk_strain.png` 재캡처도 끝났다. **그러나 아직 다섯 곳이 남았다:** ①② **고침 — 코드 잔재 0건.** `RiskEvaluatorTests.cs` 의 테스트 이름·메서드명·실패 메시지와 `TenFloorCaptureRig.cs` 의 하드코딩 문구를 전부 `Strain` 으로 바꿨다. 남은 `Warning` 2건은 `Debug.LogWarning` API 호출이라 건드리지 않았다 — 위험 **단계** 이름이 아니다. 자체 검증 232 PASS / 0 FAIL 유지. ③ `docs/VISUAL_BIBLE.md:294-295` 가 「저장소 명칭(`Warning`)을 따른다」고 `D-20260801-05`(Accepted)와 **정면으로 반대되는 지시**를 하고 있었다 → **철회 기록 완료** ④ `docs/ASSUMPTION_LOG.md:119` 의 불변식이 **없는 필드 이름** `WarningEnter` 를 가리킨다 (현재 `StrainEnter`) ⑤ `docs/AUTONOMOUS_PROTOTYPE_GOAL.md` 209·379·529·542·593 — **이번 실행의 완료 판정 기준 문서**가 `Warning` 을 다섯 번 적고 있고, 209·529 는 **필수 고정 캡처 이름 목록**이라 캡처 세트 대조가 이름부터 어긋난다. **⑤ 는 고의로 고치지 않았다** — 사용자의 완료 기준 문서를 구현자가 말없이 고치는 것은 범위를 바꾸는 일이다. 여기에 적어 보이게 두고 사용자 판단을 기다린다. 출처 표기 `PRD §8.1` 도 틀렸다 — 동결 PRD 의 위험 단계는 **§9**(`:179`)다

---

# 3. Deferred 항목

**이유 없이 되살리지 않는다.** 필요해 보이면 `docs/runtime/PENDING_DECISIONS.md`에 승격을 제안한다.

| ID | 항목 | 근거 |
|---|---|---|
| UP-DEF-01 | 통관별 정지 버튼·타이밍 정지 | PRD §4.2, `D-20260730-04` |
| UP-DEF-02 | 구슬 위치 이동·교환 퍼즐 | PRD §4.2 |
| UP-DEF-03 | 연타·리듬·정밀 클릭 판정 | PRD §4.2 |
| UP-DEF-04 | L·T·십자·고리 특수 패턴 | PRD §4.2, N07 「프로토타입에서 제외」 |
| UP-DEF-05 | 정상 영혼 9종 등급 체계 | PRD §4.2 — 코드 잔재는 UP-TEST-11 |
| UP-DEF-06 | 추가 저항체 (고정체·위장체·보호체·무게체) | PRD §4.2, N07 「후속 저항체 후보」 |
| UP-DEF-07 | 보호체 계약 | N01 예시에만 존재. PRD §4.1은 계약 2종 |
| UP-DEF-08 | 특수 숫자 층 (444·666·777) | N03·N04·N05. PRD §4.1 필수 범위 밖 |
| UP-DEF-09 | 괴담 콘텐츠·랜덤 사연 NPC 템플릿 | N04. PRD §4.1 밖 |
| UP-DEF-10 | 몬스터·수호자 시스템 | N04. PRD §4.1 밖 |
| UP-DEF-11 | 세계관 반전·멀티 엔딩 5종 | PRD §4.2 「완성형 멀티 엔딩」, N05 |
| UP-DEF-12 | 완성형 대화·관계도 | PRD §4.2, PRD §9.4 「긴 대화 트리와 립싱크는 제외」 |
| UP-DEF-13 | 상인·경제·거래·하강 | N02 「의도적인 하강」, N04 「NPC의 경제 활동」. PRD §4.1 밖 |
| UP-DEF-14 | 테마 전환 (10·100·200·500층) | N03 「테마 제작 원칙」. 프로토타입은 10층 |
| UP-DEF-15 | 온라인·멀티플레이·Twitch 연동 | PRD §4.2 |
| UP-DEF-16 | 장기 메타 진행·세이브 슬롯 | PRD §4.2 |
| UP-DEF-17 | 최종 캐릭터 아트·최종 애니메이션 | PRD §4.2 (프리셋은 UP-APV-01) |
| UP-DEF-18 | Bootstrap / ElevatorPrototype 2씬 분리 | N08 §4.1 권장. PRD 필수 아님. `D-20260730-05` 단일 씬 소유 원칙과 충돌 |
| UP-DEF-19 | Subsurface scattering 셰이더 | PRD §12.4 「전역 필수 기능이 아니다」 |
| UP-DEF-20 | 3D 에셋 프롬프트 파이프라인 실행 (N06 §1~11) | 최종 아트 단계. Pass 3에서 그레이박스 개선이 우선 |
| UP-DEF-21 | 실제 플레이테스터 관찰 (PRD §16.3, §25) | 사람이 필요하다. 에이전트가 대체할 수 없다 |

---

# 4. Approval Required 항목

**이 항목들 때문에 작업을 멈추지 않는다.** 교체 가능한 프리셋으로 진행하고
`docs/runtime/PENDING_DECISIONS.md`에 선택지를 유지한다 (PRD §1.2, §14.2).

| ID | 항목 | 현재 기본값 | 근거 |
|---|---|---|---|
| UP-APV-01 | 캐릭터 최종 외형·의상 | 무채색 저폴리 플레이스홀더 | PRD §14.2 |
| UP-APV-02 | 캐릭터 최종 모션·표정 | LookAt + 상체 기울임만 | PRD §14.2, §9.4 |
| UP-APV-03 | 심볼 최종 색·재질 | 실루엣 + 코어 구분 | PRD §14.2, N07 |
| UP-APV-04 | 공포 강도·점프스케어 여부 | 3종 프리셋 (`A-20260730-09`) | PRD §14.2 |
| UP-APV-05 | 최종 수치 밸런스 | 노션 임시값 (`A-20260730-02`) | PRD §4.2, §14.2 |
| UP-APV-06 | 캐스케이드 최종 속도 | 연출 템포 3종 (`A-20260730-10`) | PRD §14.2 |
| UP-APV-07 | 최종 카메라 셰이크 강도 | `RiskProfile.CameraShake` 0~0.0015 | PRD §14.2, §8.3 |
| UP-APV-08 | 최종 사운드 믹스 | 절차 생성 hum 1채널 | PRD §14.2 |
| UP-APV-09 | 최종 UI 표현 방식 | ScreenSpaceOverlay HUD | PRD §14.2, §17 |
| UP-APV-10 | `PowerBand.Damaged`(90~99%)가 무엇을 빼앗는가 | 플래그만 계산, 소비처 0곳 | `ASSUMPTION_LOG.md` 미뤄 둔 결정 |
| UP-APV-11 | `Jettison`이 승객과 화물을 구분하는가 | 무게 순으로만 버림 | 같음 · `D-20260731-04` 미결 |
| UP-APV-12 | Phase 1 레거시 스택 — 삭제 vs `Legacy/` 격리 | 씬에 비활성으로 잔존 | 같음 · 씬 삭제는 되돌리기 어렵다 |
| UP-APV-13 | 사고 기록기를 월드 공간으로 옮길 것인가 | ScreenSpaceOverlay | 같음 · PRD §10.1은 물리 장치를 요구 |
| UP-APV-14 | 기준 하드웨어 프로파일 확정 | **불일치 — `PD-16` 참조.** 에셋은 Ryzen 5 5600X(개발 기기), `TECH_SPEC` §13·`A-20260730-01` 은 Ryzen 7 5700 | PRD §13.1 |

---

# 5. 수정 백로그 — 시각 평가 REJECT에서 전환된 항목

**비주얼 REJECT는 작업 종료 사유가 아니다.** 지적을 여기로 옮기고 다음 미구현 필수
범위로 이동한다. Pass 3에서 소진한다.

| ID | 지적 | 원 항목 | 상태 |
|---|---|---|---|
| UP-FIX-01 | `01_entry`가 공간의 높이를 보여주지 못한다 — 높이 프레임 0장 | UP-SPACE-04 | 열림 (**평가자 최우선**) |
| UP-FIX-02 | 임계점 눈금에 숫자 라벨이 없다 — 계기판에 빈 띠가 없어 자리가 없다 | UP-DEVICE-05 | 열림 (**3세션째**) |
| UP-FIX-03 | 과수확 레버가 당김을 형상으로 전달하지 못한다 (하우징 안 + 카메라 반대) | UP-DEVICE-03 | 열림 |
| UP-FIX-04 | 좌측 벽 라벨이 거울상으로 렌더된다 | UP-DEVICE-07 | 열림 |
| UP-FIX-05 | Critical과 Collapse가 캡처에서 구분되지 않는다 | UP-RISK-03, UP-RISK-06 | 열림 |
| UP-FIX-06 | 17번 캡처만 해상도·방식이 다르다 | UP-REC-05 | 열림 |
| UP-FIX-07 | HUD 텍스트가 화면 오른쪽 끝에서 잘린다 | UP-CORE-13, UP-VIS-09 | 열림 |
| UP-FIX-08 | 계기와 3×3 판이 한 화면에 안 들어온다 (금지 `B-5 #15`) | UP-VIS-07, UP-CORE-13, UP-SPACE-09 | **✅ 닫힘 — 8차 판정 「금지 항목 위반 0」.** 네 카메라 위치에서 9칸과 전력/요구가 동시에 프레임 안. 진단이 4라운드 동안 틀렸던 경위는 ↓ |
| UP-FIX-09 | `14_contract_select` 재설계 — 판독성 1/5 | UP-CONTRACT-05, UP-VIS-07 | 열림 |
| UP-FIX-10 | 게이지가 위험 단계를 읽는다 | UP-RISK-03, UP-VIS-07 | **✅ 해결 — 8차 실측 「민트→탁녹→적→적」.** 직전 「코드 완료」 표기는 **거짓이었다** — `ApplyBar` 가 전력%만 봤고 2026-08-02 에 명도축으로 실제로 고쳤다 |
| UP-FIX-11 | 모순 2건 중 1건 해결(위험도). **남은 1건: `Loadout` 은 확정 시점 스냅샷인데 `CarriedWeight`·`Overloaded` 는 캡처 시점 라이브 값** | UP-REC-02, UP-REC-03 | 열림 |
| UP-FIX-12 | 월드 라벨이 통관 지오메트리에 **절단**된다 — 축이 크기가 아니라 깊이·가림이다 | UP-NPC-04, UP-VIS-10 | 열림 |
| UP-FIX-13 | 매니페스트 주장과 그림이 다른 장 9건 | UP-VIS-06 | 열림 |
| UP-FIX-14 | 라벨 거리 축소는 **틀린 축이었다** — 가림은 스케일 불변이다 (0.35 를 더 낮추지 말 것) | UP-VIS-08, UP-VIS-10, UP-TEST-09 | 열림 |
| UP-FIX-15 | 결과 숫자가 연출보다 30프레임 먼저 나와 스핀을 스포일한다 | UP-CORE-11 | 열림 |
| UP-FIX-16 | 화면 캡처 3장이 **816×714** 였다 | UP-VIS-06, UP-TECH-04 | **✅ 해결 — PNG 헤더 실측 (2026-08-02)** |
| UP-FIX-17 | 두부 글자 원인 확정 — `⚠`(U+26A0)이 아틀라스에 **없다**. `[과적]`으로 교체 | UP-REC-02, UP-VIS-07 | **✅ 해결 — 8차 실측 「`17` 두부 글자 0 · 자기모순 0」** |
| UP-FIX-18 | 위험 단계 구분이 **색에만** 의존한다 — 회색조에서 Strain↔Critical 이 같아진다 | UP-RISK-03, UP-VIS-09 | 열림 |
| UP-FIX-19 | `22_presenting_screen` 프레임 — 3회 시도. 숫자는 들어왔으나 우측(「폭주」) 절단 + 결과판 이탈 | UP-CORE-13, UP-VIS-06 | **3회 실패 · 배치 결정 대기** |
| UP-FIX-20 | 순차 공개가 **정지 화면에서 안 읽힌다** — 공개 중인 칸 표식이 없어 「공개 중」과 「빈 판」이 같다 | UP-CORE-11 | 열림 |
| UP-FIX-21 | `07_cargo_full` 승객 이름 3개가 벽 기록문과 겹쳐 **글자가 4겹** (4차 대비 악화) | UP-FIX-12, UP-VIS-10 | 열림 |
| UP-FIX-22 | 상태 패널이 **네 시점에서** 가장자리 절단 — 해상도 탓이 아니었다(1920 에서 전수 재현) | UP-VIS-06, UP-DEVICE-06 | 열림 — **원인 확정 (2026-08-02).** `UP-FIX-23` 을 고치며 넣은 좌측 여백 5.20 단위가 **쓸 수 있는 폭을 그만큼 줄였다.** 가림을 고치며 잘림을 만든 것이고 「다섯 라운드 연속 가장자리가 좌 → 우로 이동」이 이것이다. 왼쪽을 되돌리면 가림이 돌아오므로, **긴 줄(`_statusLabel`)만** 오토사이즈(하한 0.66×)로 상자 안에 넣었다. 재캡처 판정 대기 |

| UP-FIX-23 | **11장에서 「전력」이 반투명 통관에 좌측 가림.** 매니페스트는 그 11장을 전부 「온전 3·잘림 0」으로 적었다 — **스스로 「가림은 재지 않았다」고 고지해 두고 초록불** | UP-DEVICE-05, UP-VIS-07 | **✅ 해결 — 9차 실측 「전」 첫 잉크 x=977 px(예측 >911), 06·09·10·16 전문 판독** |
| UP-FIX-24 | 매니페스트가 **측정하지 않은 축에 판정을 준다.** 「프레임 안」과 「읽힌다」를 같은 말로 쓴다 | UP-VIS-06 | 열림 — 가림은 재게 됐으나 9차가 **양방향 오류**를 잡았다: 거짓 레드 22건(`PanelBack` 자기 배면을 가림으로 셈) + 거짓 그린 7건(TMP 자신의 rect 클리핑 미계측). 둘 다 코드로 고쳤고 **재캡처 판정 대기** |
| UP-FIX-25 | 승객 이름표 양보 규칙이 **반대로 작동** — `07` 겹침 3 → **6개**(5차보다 나쁨). 배킹판이 보이는 장 **0장** | UP-FIX-21, UP-VIS-10 | 열림 (**월드 라벨 겹침 3회 — 반복 상한**) |
| UP-FIX-26 | 새 미니 바가 **0% 와 1,415% 에서 같은 그림** — 정보를 0비트 준다 | UP-DEVICE-05 | **지적은 유효하나 원인이 다르다 (2026-08-02 확인)** — 그 넷은 막대가 아니라 **위험 4칸 점등**(`GameHudView.EnsureAuxReadout`)이고 `i <= risk` 로 배선돼 있다. 위험이 같으면 같은 것이 **옳다**. 그러나 독립 평가자가 전력 막대로 **오독**했고, 틀린 것은 보는 쪽이 아니라 화면이다 → 칸 간격 3 px → **10 px** 로 벌려 「이어진 막대」가 아니라 「세는 칸」으로 만들었다. 재판정 대기 |
| UP-FIX-27 | `19` 에서 「999%」와 「1,415 %」가 **자기모순** | UP-REC-02 | **✅ 원인 확정·수정 (2026-08-02)** — `GameHudView` 가 비율을 `Clamp(…, 0, 999)` 로 잘라 놓고 있었다. 계기판은 참값을 적는데 화면 위 숫자만 999 에서 멈춘다. 상한을 풀고, 자릿수가 늘면 라벨이 스스로 줄도록 오토사이즈(62→34)를 걸었다. 재캡처 판정 대기 |
| UP-FIX-28 | 명도축이 성립했으나 **가장 약한 쌍이 Critical↔Collapse 로 옮겨갔다.** 직접광 면은 네 단계가 Δ<2 — 7차의 「지표가 틀린 쌍을 지목한다」가 같은 형태로 재발 | UP-FIX-18, UP-RISK-03 | 열림 |
| UP-FIX-29 | **`16`·`17` 에 새 자기모순 — 내가 만들었다.** 「전력 933 / 요구 1508」 바로 아래 「요구 365  0 %」. 줄을 나눌 때 **런 종료 경로(`ShowRunOver`)를 같이 안 고쳐** `_requiredLabel` 에 직전 층 값이 남았다. 8차가 닫은 `UP-FIX-11` 이 다른 장에서 재개방 | UP-FIX-11, UP-REC-02 | **✅ 수정 (2026-08-02) — 재캡처 판정 대기** |
| UP-FIX-32 | **잔류 두 종류를 한 줄에 이어 붙여 상자를 넘겼다.** 실측: 쓸 수 있는 폭 20.80 · 흡수체 절만 18.15(들어감) · 스핀 줄 19.42(들어감) · **이어 붙이면 34.65(넘침)**. 넘친 14 단위가 화면 x≈1273 px 의 잉크 절단이다. 9·10차가 같은 자리를 두 번 짚었다 | UP-FIX-22, UP-DEVICE-05 | **✅ 세 줄로 나눠 해결 — 실측 18.13/26.00 · 독립 판정 대기** |
| UP-FIX-33 | **`isVisible` 로 상자 넘침을 재려 했으나 이 라벨들의 `overflowMode` 는 `Overflow` 다** — TMP 가 넘친 글자를 그대로 그리므로 그 계수기는 24장 전부 0 이었다. 재지 못하는 축에 0 을 적어 초록불을 준 것 | UP-FIX-24, UP-VIS-06 | **✅ 잉크 오른끝 실측으로 교체 (2026-08-02)** |
| UP-FIX-34 | **`Ascend/Stylized` 셰이더를 쓰는 렌더러가 씬에 0개다.** 스타일 락 네 축 중 셋(플랫 셰이딩·회녹색 그림자·폴리곤 면)이 그 셰이더 안에 있고 화면에는 없다. 독립 평가자는 열 번 다 URP/Lit 그레이박스를 채점했다 — 스타일 2.30/5 가 세 라운드 안 움직인 진짜 이유 | UP-VIS-01, UP-VIS-07 | 열림 — **원인 확정.** 셰이더에 흰색 기본 `_BaseMap` 을 넣어 무변화 시작점을 만들었고, `AscendMaterialFactory` 로 호출부마다 켤 수 있게 했다. `CarShell_*` 만 켜 둔 상태 |
| UP-FIX-30 | `21_board_and_gauge` **후퇴** — 8차의 「유일하게 가림 없는 패널」에서 「위힘」 절단·% 소실·눈금 5개 중 3개 | UP-FIX-22, UP-DEVICE-05 | 열림 (`UP-FIX-22` 오토사이즈로 함께 잡힐 것으로 예상 — **확인되지 않았다**) |
| UP-FIX-31 | `17` 의 월드 패널이 오버레이 **앞에** 그려져 겹침 악화 | UP-VIS-10 | 열림 (렌더 순서 — 씬 오너 몫) |
| UP-FIX-35 | **계단 셰이딩이 셰이더가 실제로 도는데도 안 보인다.** 12차 실측: `12` y=470 200px 에 평탄 구간 **156개**(최장 7px) · 350px 램프 벽 244개 = 연속 그라데이션. 「렌더러 미적용」으로는 더 설명되지 않으므로 **셰이더 로직 문제로 재분류**. **유력 원인**: `color += rim * _RimStrength * _ShadowTint.rgb` 의 림 항이 `pow(1 - dot(normal, viewDir), 3)` 이라 넓은 면을 비스듬히 볼 때 **연속으로** 변한다 — 계단을 지난 뒤 더해져 계단을 메운다. 앞서 최종 휘도 양자화를 넣었다가 뺐는데, 뺀 이유(위험 단계 소실)는 **깨진 셰이더 탓이었으므로 근거가 사라졌다.** 재시도 대상이다 | UP-VIS-01, UP-VIS-04 | 열림 (**12차 1순위**) |
| UP-FIX-36 | **금색 페이라인이 세 장 모두 늘었다** — `15` 49,730 → 62,245 · `19` 41,655 → 63,120 · `21` 16,698 → **34,446**. 11차가 「나아졌다」고 적은 `21` 감소를 반납하고 그 이상 올라갔다. 슬롯머신 회피는 **7라운드 연속 지적** | UP-VIS-08 | 열림 (**반복 상한 — 다른 층위 필요**) |
| UP-FIX-37 | **위험 축이 반쪽이다.** 앰비언트를 낮춰 **그늘진 면만** 어둡게 하는 메커니즘이라 직사광 면(장치 기둥)은 104.2 → 96.5 로 폭 7.7 이고 **Critical ≡ Collapse(95 ≡ 95)**. 우벽 하부는 비단조(Strain 이 더 밝다). 네 단계 중 `16` 한 장만 확실히 구분된다 | UP-RISK-03, UP-FIX-28 | 열림 |

## 5.1 독립 감사가 새로 찾은 결함 (2026-08-02 · 26 에이전트)

**백로그에 없던 것들이다.** 위 §5 는 시각 평가에서, 이쪽은 코드·씬·로그 대조에서 나왔다.

| ID | 결함 | 왜 위험한가 | 원 항목 |
|---|---|---|---|
| UP-AUD-06 | `AudioDirector._dangerProfile`·`_accessibilityProfile` 이 **씬 YAML 에 키 자체가 없다** → 런타임 null → 코드 프리셋 | 「접근성 옵션이 `AccessibilityProfile` 을 읽는다」 PASS 단정은 `RiskStateView` 쪽만 보므로 **이것을 못 잡는다** | UP-RISK-05·08, UP-AUD-05 |
| UP-AUD-07 | `AudioMixProfile` **18필드 중 13개가 죽어 있다** (덕 5 + 험 배율 8). 험 배율은 계산·노출되지만 실제 험은 `RiskStateView.cs:403` 이 **절대값으로 덮어쓴다** | `DEAD_IMPLEMENTATION_AUDIT.md` §1 의 「13개」는 낡은 기록이 **아니다** — 정정하지 말 것 | UP-AUD-05 |
| UP-VIS-11 | `VisualQualityProfile` High `_shadowDistance: 30` vs `PC_RPAsset.asset:57` `m_ShadowDistance: 50` — **어긋난다** | 성능 리포트가 **거짓 조건을 인용**한다. 7필드 중 6이 소비처 0 → 예산이 아니라 **라벨** | UP-PLAT-05, UP-TECH-07 |
| UP-AUD-08 | `PlayedKindsMask` 가 variant 를 버린다(`AudioDirector.cs:671`) | 「응력음 1회」와 「네 단계 각각」이 **구분되지 않는다** — §8.3 의 증거가 될 수 없다 | UP-RISK-05 |
| UP-TEST-12 | GC 인용 수치(10,443 / 8,805 / 1,638 B)의 **원본 로그가 덮어써졌다.** 현재 파일은 10807/9173/9176/10803 | 문서가 **디스크에 없는 숫자**를 인용한다 (이 저장소가 반복한 실패) | UP-TECH-05 |
| UP-VIS-12 | 6라운드 시각 채점이 전부 **절대 5점 척도**인데 `.claude/visual-criteria.md:6-7` 이 그것을 **금지**한다 | 통과 조건의 **측정 방법 자체가 절차 위반**이다 | UP-VIS-07 |
| UP-DOC-03 | 출처 표기 다수가 동결 `MASTER_PRD.md` 에서 해소되지 않는다 — `§14.1`·`§17.4`·`§9.3`·`§10.3`·`§7.3`·`§13.3/4`·`§15.2`. 동결본은 **§15 에서 끝나고 §16·17 이 없다** | 「출처를 확인하라」가 불가능해진다. 실제 출처는 Notion N08 | 다수 |
| UP-DOC-04 | 백로그 자기모순 — `UP-VIS-01` 서술이 「`UP-VIS-04` 는 NOT_STARTED」라 적지만 그 항목은 SKELETON. 심볼 머티리얼도 「배정되지 않는다」 ↔ 「전부 `M_Gray_Readout` 공유」로 **정반대** (후자가 맞다) | 같은 문서의 두 줄이 서로를 부정한다 | UP-VIS-01, UP-VIS-04 |
| UP-VIS-13 | `MAT_Ascend_*`·`MAT_Sym_*` **6종과 셰이더가 씬·코드·프리팹 참조 0건**. 게다가 머티리얼 6개가 `_AmbientFloor: 0.18` 을 직렬화로 덮어써 **「0.35 로 올렸다」가 채택 대상에 적용돼 있지 않다** | 다음 채택 시도가 **고치기 전 값으로** 다시 실패한다 | UP-VIS-04 |
| UP-REC-06 | `TenFloorCaptureRig.cs:718-720` 하드코딩 문구 「이 한 장만 방식이 다르다 / 나머지 **18장**」 — 실제 23장·화면 캡처 4장 | 매니페스트가 **스스로 틀린 숫자**를 매번 새로 찍는다 | UP-VIS-06, UP-REC-05 |


## `UP-FIX-08` / `B-5 #15` — 4라운드 동안 **틀린 진단**을 고치고 있었다 (2026-08-02)

독립 설계자가 씬 YAML 을 직접 파싱하고 **카메라 → 라벨 광선을 모든 후보와 교차**시킨 결과,
「문틀 기하에 가림」은 **사실이 아니다.** 가리는 물체가 하나도 없다 —
`TubeFrame` 은 빗나가고, `DoorControl/Plate`·`Handrail_B` 는 아래에 있고,
`PanelBack`·`WallL` 은 타깃보다 뒤에 있다.

**실제 원인은 둘이고 둘 다 카메라로 못 푼다.**

1. **스침각 19.1°** — 결과판 법선(+X)과 계기 라벨 법선(−Z)이 **정확히 90° 벌어져 있어**
   어떤 단일 시선도 둘 중 하나를 반드시 스쳐 본다. 계기판 겉보기 폭이 **0.327배**로 압축된다.
2. **라벨의 글자축이 카메라의 깊이축과 같다** — rect 가 월드 +X(= 카메라 쪽)로 자라고,
   프레임 우측 경계 조건상 **한 줄의 30.7% 가 구조적으로 프레임 밖**이다.
   카메라를 옮기면 비율만 달라지고 사라지지 않는다 — `UP-FIX-22`(네 시점에서 반복)와 같은 뿌리다.

프레임 실측: 결과판 27.3% · 좌측 벽 23.5% · **우측 31.6% 가 `BackWall_Left` 를 19° 로
스쳐본 무정보 평면**이고 계기판이 그 안에 묻혀 있다.

**평가자가 지목한 「x ≈ −1.0 의 문틀 기둥」은 그런 이름의 오브젝트가 없다** (`m_Name` 263개
전수 확인). 실제 대상은 `BackWall_Left` 이고, 그것은 계기를 **가리는** 것이 아니라
계기를 **삼키는 배경**이다. 지목은 맞았고 기구가 틀렸다.

구조적으로 다른 세 안(코너 챔퍼 / 문설주 축소 / 세 번째)과 각각의 좌표·되돌리기·깨질 수
있는 것·판정 기준이 **`docs/runtime/PASS3_STRUCTURAL_PLAN.md`** 에 있다.
`UP-SPACE-06`(최대 적재 동선)을 깨지 않는지까지 검산돼 있다.

**`UP-FIX-19` 프레임 조정은 3회로 멈춘다 (`visual-verify` §6).**
① 계기판·결과판의 **중점**을 겨눔 → 중점이 눈높이 아래라 **바닥**을 봤다. 순손실.
② 계기판 **루트**를 겨눔 → `InstrumentPanelView.transform` 이 `(0,0,0)` 이라 또 바닥.
   컴포넌트가 붙은 루트와 글자가 있는 곳이 다르다는 것을 그때 알았다
   (실제 라벨은 `(-1.04, 1.50~1.76, 1.38)`).
③ **라벨 중심을 계산하고 그 앞 1.45m 에 세운 뒤** 겨눔 → 숫자가 전부 들어왔다.
   「3층/10 위험도 안정 · 전력 1616/요구 365 443% · 스핀 4/5 판돈 194 ·
   흡수체 2개 → 저장 전력 −16.0」 + 「연쇄 0단계」 + 게이지.

**③ 을 채택하되 완결로 적지 않는다.** 1.45m 가 가까워 우측에서 「폭주」가 잘리고
결과판이 화각을 벗어났다. 네 번째 거리 조정을 하지 않는 이유는 이 절단이
`UP-FIX-22`(상태 패널이 **네 시점에서** 가장자리 절단)와 같은 뿌리이기 때문이다 —
평가자가 「카메라를 네 번 고치는 것보다 배치를 손대는 편이 싸다」고 적었고 그 말이 맞다.
겸사로 발견한 것: 이 장의 눈높이가 **2.60m** 였다. 앞 장이 남긴 부감 자세를
물려받고 있었다 — 이제 라벨 앞에 세워 놓고 찍으므로 그 오염도 사라졌다.

**5차 판정에서 기각된 가정 하나 —** 4차 평가자가 우측 텍스트 절단에 「816px 게임 뷰 종속일 수 있으니 1920 에서 재확인 필요」라는 단서를 달았고 **나도 그 가정을 반복했다.** 해상도를 통일해 재확인한 결과 `02`·`08`·`15`·`18` 전부 1920 에서 재현된다 — **실제 결함이었다.** 다만 해상도 통일이 헛수고는 아니었다: `17` 과 `20` 은 실제로 점수가 올랐고, 그 덕에 `17` 의 두부 글자와 자기모순이 **가능성에서 확정으로** 바뀌었다.

**라벨 두 건(`UP-FIX-12`·`UP-FIX-14`)에 대해 — 내 수정은 축이 틀렸다.**
거리 기반 축소(하한 0.35)를 넣고 「22% → 13% 로 줄었다」고 적었으나, 독립 평가 **둘이
따로** 같은 결론을 냈다: **가림은 스케일 불변이다.** 캡처를 25%로 줄여도 라벨이 차지하는
화면 폭 비율은 그대로였고(~11%), 실패의 원인은 크기가 아니라 **라벨이 유리 통관 안쪽
깊이에 놓여 선반 슬래브에 가로로 절단되는 것**이다. `06`·`09`·`18` 은 글자 상·하반부가
분리됐고 `08` 은 「ㅇ ㄴ ㅣ」 획 파편만 남는다. **축소는 이 실패를 악화시켰다** —
작아질수록 절단선 하나가 파괴하는 글자 면적 비율이 커진다.
**하한 0.35 를 더 낮추지 않는다.** 이미 판독 한계선 아래이고, 더 내리면
「가리는 면적은 그대로인 채 의미만 없는 얼룩」이 된다. 고칠 축은 **배경과 앵커**다 —
불투명 배킹판을 깔거나 앵커를 어두운 벽 쪽으로 민다. 축소 자체는 유지한다(시선 분산은 줄었다).

**UP-FIX-02는 3회 반복에 실패했다.** `visual-verify` §6의 반복 상한 규칙에 따라 같은
층위에서 네 번째를 시도하지 않는다. 필요한 것은 미세 조정이 아니라 **배치 결정**이다 —
숫자를 게이지 배면에 새기거나 상태 블록을 올려 자리를 만든다.

---

# 6. 통계

> 아래 수치는 `tools/verify-topdown.ps1 -Stats`가 이 파일에서 직접 세어 대조한다.
> 손으로 고치지 말고 검증기 출력과 맞춘다. **최종 갱신: 2026-08-01 실증 감사.**

| 분류 | 개수 |
|---|---|
| 추적 항목 (§2) | 130 |
| Required | 129 |
| Deferred (§3 표 + `UP-RUN-09`) | 22 |
| Approval Required (§4) | 14 |
| 수정 백로그 (§5) | 22 |
| 감사 발견 결함 (§5.1) | 10 |

<!-- 아래 표는 손으로 적는 것이 아니다. `verify-topdown.ps1 -Stats` 출력을 그대로 옮긴다.
     2026-08-02 감사가 이 표를 「NOT_STARTED 6」인 채로 잡아냈다 — 실제는 0 이었다.
     통계표가 틀리면 「얼마나 남았는가」를 묻는 모든 판단이 함께 틀린다. -->

| 상태 | Required 중 개수 | 2026-08-02 세션 시작 |
|---|---|---|
| `VERIFIED` | **74** | 73 |
| `CONNECTED` | **46** | 37 |
| `VISIBLE` | **2** | 0 |
| `SKELETON` | **7** | 15 |
| `NOT_STARTED` | **0** | 4 |
| `BLOCKED_EXTERNAL` | 0 | 0 |

**Pass 1 완료** (모든 Required 가 `SKELETON`·`VISIBLE` 이상). **Pass 2 바 미달 9건.**

패스별 미달 수는 검증기가 `-Stats` 로 직접 센다 — 여기에 옮겨 적지 말고 그쪽을 볼 것.

> **이 표는 손으로 적는 것이 아니다.** 2026-08-01 독립 감사가 항목 헤더를 직접 세어
> 여기 적힌 66/25/31 이 실제(67/33/22)와 다르다는 것을 찾았다. 통계표가 틀리면
> "얼마나 남았는가"를 묻는 모든 판단이 함께 틀린다.
> **갱신 방법**: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify-topdown.ps1 -Stats`
> 의 출력을 그대로 옮긴다. 기억으로 더하고 빼지 않는다.

> **2026-08-01 Pass 1 Wave A.** `NOT_STARTED` 가 23 → 7 로 내려갔다. 옮겨간 16건은
> 대부분 `SKELETON` 이다 — **코드와 테스트는 생겼지만 씬에 붙지 않아 게임 안에서는
> 아직 아무 일도 일어나지 않는다.** 이 구분을 흐리면 "구현했다"가 "동작한다"로
> 읽히고, 그것이 이 백로그가 막으려는 바로 그 착시다.
> EditMode 91 → **188 PASS / 0 FAIL**, 자체 검증 110 → **207 PASS / 0 FAIL**.

`VISIBLE`이 0건인 것은 후보가 없어서가 아니라, 가장 유력한 두 후보가 Required 항목이
아니라 **레거시 정리 대상**이기 때문이다 — 상세는
`docs/runtime/CURRENT_IMPLEMENTATION_AUDIT.md` §6, 추적은 `UP-TEST-11`.
