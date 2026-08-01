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

<!-- verify-topdown.ps1 이 아래 네 줄을 파싱한다. 형식을 바꾸지 말 것. -->

- PASS_1: IN_PROGRESS
- PASS_2: NOT_STARTED
- PASS_3: NOT_STARTED
- PASS_4: NOT_STARTED

## Pass 1 — Breadth First Coverage

**목표는 완성도가 아니라 필수 범위를 한 번 전부 존재하게 만드는 것이다.**

- 모든 Required 시스템·콘텐츠가 코드 또는 교체 가능한 플레이스홀더로 존재한다.
- 모든 핵심 오브젝트가 Unity 공간에 실제로 존재한다.
- 모든 Required 층·승객·부품·계약·위험·사고·진행이 최소 `SKELETON` 이상이다.
- **차단 조건은 셋뿐이다** — 컴파일 오류, 데이터 손상, 진행 자체 불가능.
- 최종 모델링·최종 재질·정밀 밸런스·시각 평가 실패는 Pass 1의 차단 조건이 **아니다.**

완료 조건: Required 항목 중 `NOT_STARTED`가 0개.

## Pass 2 — Full Integration

- 1층부터 10층까지 모든 Required 시스템이 하나의 플레이 흐름으로 연결된다.
- 승객·부품·적재·과적·계약·룰렛·캐스케이드·위험·과수확·사고가 서로 **실제 규칙을 바꾼다.**
- 플레이스홀더라도 플레이어가 공간에서 발견하고 조작할 수 있다.

완료 조건: Required 항목 중 `NOT_STARTED`·`SKELETON`이 0개.

## Pass 3 — Experience and Visual Pass

- 노션의 그래픽 방향(PRD §12, N06, N07)과 장치 디자인을 반영한다.
- 엘리베이터 구조, 수확 장치, 레버, 심볼, 층 공간, 승객 배치를 개선한다.
- 플레이 흐름·피드백·연출·판독성·공포 분위기를 개선한다.
- **비주얼 REJECT는 작업 종료 사유가 아니라 수정 백로그로 전환한다** — REJECT를 받으면
  §5 「수정 백로그」에 항목을 추가하고 다음 미구현 필수 범위로 이동한다.

완료 조건: PRD §15.2 루브릭 통과 + `docs/runtime/VISUAL_VERDICT.md`가 `ACCEPT`.

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
- 상태: SKELETON · 패스: P2
- 구현: `Scripts/Data/Profiles/TargetHardwareProfile.cs`(기준 해상도·목표 90 FPS·하드 플로어 60 · vSync 취급)
- 접근: 해당 없음 (개발 전용 데이터)
- 검증: `Ascend/Run All EditMode Tests` → 기본 스냅샷 값 대조
- 증거: `Logs/editmode_tests.txt`
- 의존: 없음
- 남은 문제: 클래스만 있고 `.asset` 이 없다. **성능 산출물이 아직 이 프로파일을 인용하지 않는다** — PRD §13.1 이 미지정 상태에서 성능 완료 선언을 금지하므로 Pass 4 의 선행 조건이다

### UP-PLAT-05 — 압축·임포트 Preset과 VisualQualityProfile
- 분류: Required · 출처: PRD §13.3, §13.4, §17.4
- 상태: SKELETON · 패스: P3
- 구현: `Scripts/Data/Profiles/VisualQualityProfile.cs`(광원 수·그림자 거리·파티클 상한·오버드로우 예산·렌더 스케일)
- 접근: 해당 없음
- 검증: `Ascend/Run All EditMode Tests` → 프리셋 값 대조
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-PLAT-03
- 남은 문제: `.asset` 이 없고 **텍스처·오디오 임포트 Preset 은 아직 하나도 없다**. 빌드 리포트의 상위 용량 기록도 미착수

### UP-PLAT-06 — 결정론적 캡처 하네스
- 분류: Required · 출처: PRD §15.1 「동일한 해상도·FOV·카메라 위치·시간대·품질 프리셋」
- 상태: VERIFIED · 패스: P1 P3 P4
- 구현: `Assets/CaptureHarness/`, `Assets/Prototype_Elevator/Scripts/Run/Tests/TenFloorCaptureRig.cs`
- 접근: 해당 없음 (개발 도구)
- 검증: 하네스 실행 → `Captures/TenFloor/manifest.txt` 생성
- 증거: `Captures/TenFloor/manifest.txt`
- 의존: 없음
- 남은 문제: 17번 캡처만 해상도·방식이 다르다 (리그의 기존 해법을 쓰지 않음)

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
- 상태: CONNECTED · 패스: P1 P2 P3
- 구현: `Scripts/Player/CrosshairView.cs`
- 접근: 조준점을 상호작용물에 올린다
- 검증: 고정 캡처 `03_device_side`
- 증거: `Captures/TenFloor/03_device_side.png`
- 의존: UP-SPACE-02
- 남은 문제: 외곽선 셰이더가 아니라 크로스헤어 상태 변화로만 표현. PRD §12.3의 "둘 이상" 기준 재검토 필요

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
- 남은 문제: 없음

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
- 상태: CONNECTED · 패스: P2
- 구현: `Scripts/View/SpinPresenter.cs`(연출 잠금은 입력만 잠근다), `FirstPersonController.cs`
- 접근: 레버를 당긴 직후 이동해 본다
- 검증: `Logs/tenfloor_playmode.txt` 「연출잠금」 상태 기록
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-SPACE-01, UP-CORE-11
- 남은 문제: 전용 단정이 없다. 현재는 상태 로그로만 확인된다

### UP-SPACE-09 — 등을 돌려도 결과와 전력 변화를 알 수 있다
- 분류: Required · 출처: PRD §11(무음 관전자 기준), N03 「등을 돌려도 사운드·점등·보조 UI로」, N08 §17
- 상태: SKELETON · 패스: P2 P3
- 구현: `Scripts/UI/GameHudView.cs`(화면 HUD만)
- 접근: 장치를 등지고 선 채 레버 결과를 기다린다
- 검증: 등진 시점 고정 캡처 + 사운드 채널 확인
- 증거: 없음
- 의존: UP-AUD-02, UP-DEVICE-06
- 남은 문제: 사운드 채널이 거의 없어 실질적으로 HUD 하나에만 의존한다

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
- 남은 문제: 계기판에 빈 띠가 없어 숫자 라벨을 넣을 자리가 없다 (배치 결정 필요)

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
- 남은 문제: 흑백 변환 대조 검사를 아직 하지 않았다

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
- 검증: 고정 캡처 `15_cascade_deep` + 연출 프리셋 3종
- 증거: `Captures/TenFloor/15_cascade_deep.png`
- 의존: UP-CORE-02
- 남은 문제: 없음

### UP-CORE-12 — 판정 원인 시각화 (정화·직선·연결 점등)
- 분류: Required · 출처: PRD §6.2, §15.2, N07 「패턴 시각화」, N08 §16.2
- 상태: CONNECTED · 패스: P2 P3
- 구현: `Scripts/View/PurifyMarkerView.cs`, `SpinPresenter.cs`
- 접근: 정화가 일어나는 순간 결과판을 본다
- 검증: 고정 캡처 `15_cascade_deep`
- 증거: `Captures/TenFloor/15_cascade_deep.png`
- 의존: UP-CORE-08
- 남은 문제: 개수 정화 / 직선 / 연결의 연출 강도 차이가 아직 대비 검사되지 않았다

### UP-CORE-13 — 한 화면에 모든 숫자를 띄우지 않는다
- 분류: Required · 출처: PRD §6.2 마지막 문단
- 상태: CONNECTED · 패스: P3
- 구현: `Scripts/UI/GameHudView.cs`, `Scripts/View/SpinPresenter.cs`
- 접근: 깊은 캐스케이드 중 화면을 본다
- 검증: 고정 캡처 `15_cascade_deep`
- 증거: `Captures/TenFloor/15_cascade_deep.png`
- 의존: UP-CORE-12
- 남은 문제: 없음

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
- 남은 문제: `PowerBand.Damaged`(90~99%)의 **소비처가 0곳** → UP-APV-10

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
- 상태: SKELETON · 패스: P2 P3
- 구현: `Scripts/Run/OverharvestApproachBridge.cs`(접근 판정), `Scripts/Audio/SilenceWindow.cs`(정적), `Scripts/Npc/`(승객 응시 반응)
- 접근: 과수확 레버에 손을 올린다
- 검증: 접근 순간 고정 캡처 + 정적 구간 측정
- 증거: `Captures/TenFloor/12_overharvest_unlocked.png`
- 의존: UP-DEVICE-03, UP-NPC-02, UP-AUD-03
- 남은 문제: 5단계의 부품이 전부 생겼으나 **씬에서 서로 이어지지 않았다.** 접근 다리·오디오·승객 반응이 한 오브젝트 트리에 붙어야 한다

### UP-POWER-07 — OverharvestProfile 데이터화 (9개 항목)
- 분류: Required · 출처: PRD §7.4
- 상태: SKELETON · 패스: P2
- 구현: `Scripts/Data/Profiles/OverharvestProfile.cs`(PRD §7.4 의 9항목)
- 접근: 해당 없음
- 검증: `Ascend/Run All EditMode Tests` → 정적 구간 범위 조임 등
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-POWER-05
- 남은 문제: 클래스만 있다. `.asset` 을 만들고 **`FloorSession`·`OverchargeOption` 의 흩어진 수치가 실제로 이 프로파일을 읽게** 해야 데이터화가 성립한다

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
- 상태: CONNECTED · 패스: P1 P2
- 구현: `Scripts/Run/RouletteInteractionBridge.cs`(연출 잠금)
- 접근: 스핀 중 레버를 연타한다
- 검증: `Logs/tenfloor_playmode.txt` 「연출잠금」
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-CORE-11
- 남은 문제: 없음

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
- 검증: 고정 캡처 `06_risk_stable`, `09_risk_warning`, `10_risk_critical`
- 증거: `Captures/TenFloor/06_risk_stable.png`, `Captures/TenFloor/10_risk_critical.png`
- 의존: UP-RISK-01
- 남은 문제: **Critical과 Collapse가 캡처에서 구분되지 않는다**

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
- 상태: SKELETON · 패스: P2 P3
- 구현: `Scripts/Audio/ProceduralClipFactory.cs`(금속 타격·저역 하강·Collapse 임펄스), `Scripts/Audio/AudioCueTable.cs`(사건 전용 발동)
- 접근: 위험이 오르면 소리가 달라진다
- 검증: 오디오 채널 목록 + 무영상 청취 테스트
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-AUD-01
- 남은 문제: 사이렌을 **지속 재생하지 않는다**는 §8.3 원칙은 구조로 지켜진다 — 위험 사운드가 전부 사건 큐이고 지속음은 hum 하나뿐이다. 다만 씬 배선 전이라 실제로 들리지 않는다

### UP-RISK-06 — Collapse 단계 (암전 → 파열음 → 급강하 → 재점등)
- 분류: Required · 출처: PRD §8.2 Collapse
- 상태: SKELETON · 패스: P2 P3
- 구현: `Scripts/Risk/CollapseSequence.cs`(암전 → 급강하 → 불규칙 재점등, `CollapseBegan` 사건 구독)
- 접근: 층을 실패한다
- 검증: 고정 캡처 `16_risk_collapse` + 시퀀스 진행 중 캡처
- 증거: `Captures/TenFloor/16_risk_collapse.png`
- 의존: UP-RISK-01, UP-AUD-01
- 남은 문제: 연출 코드는 생겼으나 **씬에 붙지 않아 여전히 Critical 과 구분되지 않는다.** 낙하 대상·카메라 리그 배선이 필요하다

### UP-RISK-07 — DangerFeedbackProfile 데이터화 (9개 항목)
- 분류: Required · 출처: PRD §8.4, §14.1
- 상태: SKELETON · 패스: P2
- 구현: `Scripts/Risk/RiskProfile.cs`(구조체), `Scripts/Data/Profiles/DangerFeedbackProfile.cs`(ScriptableObject)
- 접근: 해당 없음
- 검증: `Ascend/Run All EditMode Tests` → 데이터 프로파일
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-RISK-01
- 남은 문제: ScriptableObject 껍데기는 생겼다. `.asset` 을 만들고 `RiskStateView` 가 코드 프리셋 대신 이것을 읽게 해야 §14.2 「프리셋 비교 가능」이 성립한다

### UP-RISK-08 — 접근성 옵션 분리 (셰이크·사이렌·섬광)
- 분류: Required · 출처: PRD §8.3 마지막, §14.1
- 상태: SKELETON · 패스: P3
- 구현: `Scripts/Data/Profiles/AccessibilityProfile.cs`(셰이크 배율·섬광 허용·사이렌·저주파 감쇠·자막)
- 접근: 옵션 메뉴 (없음)
- 검증: `Ascend/Run All EditMode Tests` → 셰이크 0 배율이 실제 0을 낸다
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-RISK-07
- 남은 문제: `.asset` 이 없고 `RiskStateView`·`CollapseSequence` 가 아직 이 값을 읽지 않는다

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
- 상태: CONNECTED · 패스: P1 P2
- 구현: `Scripts/Build/BuildFigureView.cs`(`ReactToRisk`)
- 접근: 위험이 오르면 승객 자세가 바뀐다
- 검증: 고정 캡처 `09_risk_warning`, `10_risk_critical`
- 증거: `Captures/TenFloor/09_risk_warning.png`, `Captures/TenFloor/10_risk_critical.png`
- 의존: UP-BUILD-05, UP-RISK-01
- 남은 문제: 반응 진폭은 `A-20260731-04`대로 임시값

### UP-NPC-02 — 프로토타입 반응 이벤트 10종
- 분류: Required · 출처: PRD §9.2 (계약 선택 / 기본 정화 / 5연쇄 / 임계점 3개 / 과수확 해금 / 과수확 접근 / 추가 스핀 / Critical 진입 / Collapse 직전 / 사고·성공)
- 상태: SKELETON · 패스: P2
- 구현: `Scripts/Npc/PassengerReactionEvent.cs`(11종 + `TryMap`), `PassengerReactionSet.cs`(11종 기본값), `PassengerReactionView.cs`(씬 진입점)
- 접근: 각 사건이 일어날 때 승객을 본다
- 검증: `Ascend/Run All EditMode Tests` → 「11종 전부가 사건 목록에서 유도된다」·「5연쇄는 깊이 5부터」·「임계점 100·170·300 이 서로 갈린다」
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-NPC-01
- 남은 문제: 사건 → 반응 매핑 11종이 전부 테스트로 증명됐다. **아직 씬에서 승객이 움직이지 않는다** — `PassengerReactionView` 배선이 남았다

### UP-NPC-03 — PassengerReactionSet 데이터화
- 분류: Required · 출처: PRD §9.4 「반응은 `PassengerReactionSet` 데이터로 이벤트별 교체 가능」
- 상태: SKELETON · 패스: P2
- 구현: `Scripts/Npc/PassengerReactionSet.cs`(ScriptableObject · 11종 기본값 · 폴백)
- 접근: 해당 없음
- 검증: `Ascend/Run All EditMode Tests` → 승객 반응 14건
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-NPC-02
- 남은 문제: 클래스는 있으나 **`.asset` 인스턴스가 없다.** 씬 오너가 만들어 배선해야 데이터 교체가 실제로 가능해진다

### UP-NPC-04 — 표현 채널 (시선·자세·짧은 대사·비언어 음성)
- 분류: Required · 출처: PRD §9.3
- 상태: SKELETON · 패스: P2 P3
- 구현: `Scripts/Npc/PassengerReaction.cs`(자세 7종 · 시선 6종 · 음성 큐 ID), `Scripts/Build/BuildFigureView.cs`(`SetReaction`/`GazeRotation`)
- 접근: 승객을 바라본다
- 검증: `Ascend/Run All EditMode Tests` + 고정 캡처의 승객 자세 대비
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-NPC-02, UP-AUD-04
- 남은 문제: 자세와 시선 두 채널이 코드에 생겼다. **짧은 대사는 없고**(PRD §9.4 가 긴 대화 트리를 제외하므로 한 줄 이하), 비언어 음성은 큐 ID 만 있고 재생 배선이 없다. 시선 대상 넷은 씬에서 배선해야 동작한다

### UP-NPC-05 — 동시 반응 제한 (우선순위·쿨다운·최대 수)
- 분류: Required · 출처: PRD §9.4 「한 이벤트에서 모든 승객이 동시에 말하지 않는다」
- 상태: SKELETON · 패스: P2
- 구현: `Scripts/Npc/PassengerReactionDirector.cs`(우선순위·쿨다운·최대 동시 수·결정론적 라운드 로빈)
- 접근: 해당 없음
- 검증: `Ascend/Run All EditMode Tests` → 동시 반응 상한·쿨다운·우선순위 덮어쓰기·승객 0명
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-NPC-02
- 남은 문제: 중재기는 완성됐으나 **승객 오브젝트에 붙지 않았다.** `BuildFigureView`가 반응을 실제 자세·시선으로 옮겨야 한다

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
- 남은 문제: 없음

### UP-REC-04 — 기계식 프린터·종이 테이프 형태의 물리적 출력
- 분류: Required · 출처: PRD §10.1 「단순 결과창 대신 엘리베이터 내부의 기계식 프린터, 종이 테이프 또는 펀치카드」
- 상태: SKELETON · 패스: P3
- 구현: `Scripts/View/PaperTapePrinterView.cs`(월드 공간 기계식 프린터 · 줄 단위 인쇄)
- 접근: 층이 끝나면 화면에 뜬다
- 검증: 고정 캡처 `17_accident_recorder`
- 증거: `Captures/TenFloor/17_accident_recorder.png`
- 의존: UP-REC-02
- 남은 문제: 프린터 컴포넌트는 생겼고 `FloorRecord` 를 그대로 읽는다(§10.3). **씬에 장치가 없다** — 벽면 위치와 테이프 슬롯 배치가 필요하다

### UP-REC-05 — 기록과 사고 후 상태가 한 장에 함께 보인다
- 분류: Required · 출처: PRD §10.3 마지막
- 상태: SKELETON · 패스: P3
- 구현: `Scripts/Run/Tests/TenFloorCaptureRig.cs`
- 접근: 사고 직후 화면
- 검증: 고정 캡처 `17_accident_recorder`
- 증거: `Captures/TenFloor/17_accident_recorder.png`
- 의존: UP-REC-04
- 남은 문제: 17번만 해상도·방식이 달라 다른 캡처와 나란히 비교되지 않는다

## 2.12 VIS — 비주얼과 아트 디렉션

### UP-VIS-01 — 스타일 락 (low-poly industrial occult horror)
- 분류: Required · 출처: PRD §12.1, N06 §5 Style Lock
- 상태: SKELETON · 패스: P3
- 구현: 그레이박스 프리미티브 (`Assets/Editor/GrayboxWorldBuilder.cs`)
- 접근: 어디서든
- 검증: 독립 시각 평가
- 증거: 없음
- 의존: UP-SPACE-04
- 남은 문제: 아직 그레이박스다. 재질·실루엣 언어가 없다

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
- 상태: NOT_STARTED · 패스: P3
- 구현: 없음
- 접근: 해당 없음
- 검증: 셰이더 에셋 존재 + 머티리얼 적용
- 증거: 없음
- 의존: UP-VIS-01
- 남은 문제: 없음

### UP-VIS-05 — 파티클 (먼지·녹가루·스파크·정화 파편·캐스케이드 유입)
- 분류: Required · 출처: PRD §12.5
- 상태: NOT_STARTED · 패스: P3
- 구현: 없음
- 접근: 해당 없음
- 검증: 파티클 시스템 존재 + 오버드로우 예산 측정
- 증거: 없음
- 의존: UP-VIS-01
- 남은 문제: PRD §12.5의 「단계별 최대 동시 파티클 수와 오버드로우 예산」도 없다

### UP-VIS-06 — 필수 고정 캡처 세트 9종
- 분류: Required · 출처: PRD §15.1
- 상태: CONNECTED · 패스: P3 P4
- 구현: `Scripts/Run/Tests/TenFloorCaptureRig.cs`
- 접근: 해당 없음
- 검증: 캡처 실행 → `Captures/TenFloor/manifest.txt`
- 증거: `Captures/TenFloor/manifest.txt`
- 의존: UP-PLAT-06
- 남은 문제: 18장이 생성되나 PRD §15.1의 9종 요구와 1:1 대조표가 없다

### UP-VIS-07 — 시각 루브릭 통과 (판독성·스타일 평균 4.0 이상)
- 분류: Required · 출처: PRD §15.2 통과 조건
- 상태: NOT_STARTED · 패스: P3 P4
- 구현: `.claude/visual-criteria.md`, `.claude/skills/visual-verify/SKILL.md`
- 접근: 해당 없음
- 검증: 독립 평가자 → `docs/runtime/VISUAL_VERDICT.md`에 `VERDICT: ACCEPT`
- 증거: 없음
- 의존: UP-VIS-06
- 남은 문제: **직전 평가 결과는 REJECT.** 지적 4건이 §5 수정 백로그에 있다

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
- 상태: NOT_STARTED · 패스: P3
- 구현: 없음
- 접근: 해당 없음
- 검증: 캡처를 25% 축소해 독립 평가
- 증거: 없음
- 의존: UP-VIS-07
- 남은 문제: 축소 대조 검사를 한 번도 하지 않았다

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
- 상태: SKELETON · 패스: P2 P3
- 구현: `Scripts/Risk/RiskStateView.cs`(절차 생성 hum), `Scripts/Audio/AudioCueTable.cs`(`RiskLevelChanged`·`CollapseBegan` 큐), `ProceduralClipFactory.cs`(금속 응력음·저주파 임펄스)
- 접근: 위험 단계를 올린다
- 검증: `Ascend/Run All EditMode Tests` → 사건별 큐 매핑과 볼륨·피치 범위
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-RISK-01
- 남은 문제: 지속 hum 1채널에 **단계 전이 사건음이 더해졌다.** PRD §13 의 「오디오만 듣는 테스트에서 위험 단계 구분」은 `AudioDirector` 가 씬에 붙은 뒤에야 실제로 확인할 수 있다

### UP-AUD-02 — 룰렛 사운드 10종
- 분류: Required · 출처: N08 §16.4 (레버 / 칸 공개 / 영혼 수확 / 정화 / 직선 / 연결 / 캐스케이드 단계 / 임계점 / 잔류 피해 / 확정)
- 상태: SKELETON · 패스: P2 P3
- 구현: `Scripts/Audio/AudioCueKind.cs`(10종+4), `AudioCueTable.cs`(사건→큐 매핑), `ProceduralClipFactory.cs`(절차 생성), `AudioDirector.cs`
- 접근: 스핀을 돌린다
- 검증: `Ascend/Run All EditMode Tests` → 사운드 매핑 13건 (10종 전부 매핑·깊이별 피치 단조 증가)
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-CORE-11
- 남은 문제: `AudioDirector`가 **씬에 없어 소리가 나지 않는다.** 매핑과 합성은 테스트로 증명됐다

### UP-AUD-03 — 과수확 정적 구간 (0.3~0.7초)
- 분류: Required · 출처: PRD §7.3(4)
- 상태: SKELETON · 패스: P2 P3
- 구현: `Scripts/Audio/SilenceWindow.cs`(0.3~0.7초 조임 · 게인 타임라인), `Scripts/Run/OverharvestApproachBridge.cs`(접근 사건)
- 접근: 과수확 레버에 손을 올린다
- 검증: `Ascend/Run All EditMode Tests` → 정적 구간 경계·단조성·범위 조임
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-POWER-06, UP-AUD-01
- 남은 문제: 게인 곡선은 검증됐으나 **씬에 붙지 않아 실제로 음량이 줄지 않는다**

### UP-AUD-04 — 승객 비언어 음성
- 분류: Required · 출처: PRD §9.3
- 상태: SKELETON · 패스: P3
- 구현: `Scripts/Audio/ProceduralClipFactory.cs`(PassengerVoice — 포먼트 2개, 승객 인덱스로 피치 변화)
- 접근: 승객이 반응할 때
- 검증: `Ascend/Run All EditMode Tests` → 큐 종류 분기
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-NPC-04
- 남은 문제: 합성기는 있으나 승객 반응과 이어지지 않았다 (`UP-NPC-04` 선행)

### UP-AUD-05 — AudioMixProfile / 오디오 압축 구분
- 분류: Required · 출처: PRD §13.4, §14.3
- 상태: SKELETON · 패스: P3
- 구현: `Scripts/Data/Profiles/AudioMixProfile.cs`(채널 5종 · 덕킹 배율 · 위험 단계별 험)
- 접근: 해당 없음
- 검증: `Ascend/Run All EditMode Tests` → 데이터 프로파일 19건
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-AUD-01
- 남은 문제: 클래스만 있고 `.asset` 인스턴스가 없다. 오디오 임포트 Preset 도 아직 없다

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
- 상태: CONNECTED · 패스: P2
- 구현: `Scripts/Player/PlayerSetupValidator.cs`
- 접근: 해당 없음
- 검증: `Logs/tenfloor_playmode.txt` 「씬 배선」 검사
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-TECH-01
- 남은 문제: 없음

### UP-TECH-04 — 1080p 목표 90 FPS / 하드 플로어 60 FPS
- 분류: Required · 출처: PRD §13.1, §17.4
- 상태: SKELETON · 패스: P4
- 구현: `Scripts/Run/Tests/HeroSlicePerfProbe.cs`, `LoadedCriticalPerfProbe.cs`
- 접근: 해당 없음
- 검증: 프로브 실행 → `Logs/loaded_critical_perf.txt`
- 증거: `Logs/loaded_critical_perf.txt`
- 의존: UP-PLAT-04
- 남은 문제: **중앙값 8.33ms는 vSync 상한이지 비용이 아니다.** 상한에 걸린 값으로는 90 FPS 목표를 판정할 수 없다. `Logs/heroslice_perf.txt`는 HEAD를 설명하지 못한다

### UP-TECH-05 — 워밍업 후 매 프레임 0 B GC Alloc
- 분류: Required · 출처: PRD §13.2, §17.4
- 상태: SKELETON · 패스: P4
- 구현: `Scripts/Run/Tests/LoadedCriticalPerfProbe.cs`
- 접근: 해당 없음
- 검증: 프로브의 GC/프레임 항목
- 증거: `Logs/loaded_critical_perf.txt`
- 의존: UP-TECH-04
- 남은 문제: **9,000~11,000 B/프레임.** 목표 0 B와 큰 격차. 원인 분해가 없다

### UP-TECH-06 — 오브젝트 풀링 (파티클·심볼·사운드)
- 분류: Required · 출처: PRD §13.2, §17.4
- 상태: SKELETON · 패스: P4
- 구현: `Scripts/Perf/ObjectPool.cs`(이중 반환 감지 포함), `ComponentPool.cs`
- 접근: 해당 없음
- 검증: `Ascend/Run All EditMode Tests` → 풀링 20건 (prewarm·재사용·이중 반환·maxSize 초과)
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-TECH-05
- 남은 문제: 풀은 있으나 **파티클·심볼·사운드가 아직 쓰지 않는다.** Alloc 감소 측정도 없다

### UP-TECH-07 — 렌더링 예산 측정 (드로우콜·SetPass·오버드로우)
- 분류: Required · 출처: PRD §13.3
- 상태: SKELETON · 패스: P4
- 구현: `Scripts/Perf/RenderBudgetProbe.cs`(드로우콜·SetPass·삼각형 샘플링, 측정 불가 시 명시)
- 접근: 해당 없음
- 검증: 프로브 실행 → `Logs/render_budget.txt`
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-TECH-04
- 남은 문제: 프로브는 있으나 **아직 한 번도 돌리지 않았다.** 최악 시점 기준도 정하지 않았다

### UP-TECH-08 — 10층 연속 플레이에서 메모리 누적 없음
- 분류: Required · 출처: PRD §17.4
- 상태: SKELETON · 패스: P4
- 구현: `Scripts/Perf/MemoryTrendProbe.cs`(층 경계 샘플링), `MemoryTrend.Analyze`
- 접근: 해당 없음
- 검증: 10층 런 전후 스냅샷 → `Logs/memory_trend.txt`
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-RUN-10
- 남은 문제: 추세 판정 로직만 검증됐다. **10층 런을 실제로 재지 않았다**

### UP-TECH-09 — 가변 요소의 데이터 분리 (PRD §14.1 12항목)
- 분류: Required · 출처: PRD §14.1, §14.3
- 상태: SKELETON · 패스: P2 P3
- 구현: `Data/PrototypeConfig.asset`, `Scripts/Spin/SpinRuleSet.cs`, **`Scripts/Data/Profiles/` 7종**(TargetHardware · Overharvest · DangerFeedback · VisualQuality · AudioMix · Accessibility · RunSummaryTemplate), `Scripts/Npc/PassengerReactionSet.cs`
- 접근: 해당 없음
- 검증: `Ascend/Run All EditMode Tests` → 데이터 프로파일 19건 (기본 스냅샷 값 대조)
- 증거: `Logs/editmode_tests.txt`
- 의존: 없음
- 남은 문제: §14.3 권장 13종 중 **8종의 클래스가 생겼다**(직전 0종). 남은 것은 `.asset` 인스턴스 생성과 **소비 지점을 프로파일로 돌리는 일** — 클래스만 있고 코드가 여전히 하드코딩 값을 읽으면 데이터화가 아니다

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

### UP-TEST-05 — 텔레메트리 (스핀별 JSON/CSV 20항목)
- 분류: Required · 출처: PRD §4.1(텔레메트리), §16.2, N08 §18
- 상태: SKELETON · 패스: P2
- 구현: `Scripts/Telemetry/`(SpinTelemetryRecord 20필드 · TelemetryRecorder · TelemetryFileSink · ITelemetrySink)
- 접근: 해당 없음
- 검증: `Ascend/Run All EditMode Tests` → 텔레메트리 17건 (결정론·CSV 열 일치·JSONL 이스케이프)
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-RUN-01
- 남은 문제: 두 가지가 남았다. ① **씬에 붙지 않아 실제 인게임 런이 파일을 만들지 않는다** — 헤드리스 테스트만 기록한다. ② Notion §16.2 11항목 중 **다섯이 빠져 있다** — 캐스케이드별 보드 · 정화/발동 순서 · 현재 위험 단계 · 승객·부품 발동 · 프레임 타임과 GC Alloc. 런 종료 원인은 스핀 속성이 아니라 런 단위 레코드가 따로 필요하다 (`D-20260801-06`)

### UP-TEST-06 — 디버그 패널
- 분류: Required · 출처: PRD §4.1, N08 §17 「개발 빌드에서만 기본 활성화」
- 상태: CONNECTED · 패스: P1 P2
- 구현: `Scripts/UI/DebugPanelView.cs`
- 접근: F1
- 검증: `Logs/tenfloor_playmode.txt` 씬 배선 검사
- 증거: `Logs/tenfloor_playmode.txt`
- 의존: UP-RUN-05
- 남은 문제: 릴리스 빌드에서 비활성화되는지 확인되지 않았다

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
- 상태: SKELETON · 패스: P4
- 구현: `Assets/CaptureHarness/GifEncoder.cs`(자체 LZW), `Scripts/Run/Tests/SequenceRecorder.cs`
- 접근: 해당 없음
- 검증: 5연쇄 런 녹화 → GIF 파일 존재
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-CORE-08
- 남은 문제: 인코더는 **파이썬 이식본을 Pillow 로 되읽는 왕복 검사로 검증했다**(사전 포화·Clear 재시작 경로 포함, 480×270 5프레임 바이트 일치). 아직 **실제 런을 녹화하지 않았다**

### UP-TEST-09 — Critical → 과수확 → 결과 영상
- 분류: Required · 출처: PRD §17.6 증거 산출물
- 상태: SKELETON · 패스: P4
- 구현: `Scripts/Run/Tests/SequenceRecorder.cs`(RecordUntil 로 조건까지 녹화)
- 접근: 해당 없음
- 검증: Critical → 과수확 → 결과 구간 녹화 → GIF 파일 존재
- 증거: `Logs/editmode_tests.txt`
- 의존: UP-TEST-08
- 남은 문제: 녹화 장치만 있다. 그 구간으로 런을 몰아넣는 대본이 아직 없다

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
- 상태: NOT_STARTED · 패스: P4
- 구현: 정리 대상 — `Scripts/Data/Ball*.cs`, `CombinationConfig.cs`, `PassengerDefinition.cs`, `Scripts/Roulette/`, `Scripts/Effects/`, `Scripts/Sim/`(일부), `Core/PassengerManager.cs`, `Data/Balls/`, `Data/Effects/`, `Data/Passengers/`
- 접근: 해당 없음
- 검증: 삭제 또는 `Legacy/` 격리 후 컴파일·테스트 통과
- 증거: 없음
- 의존: UP-TEST-01
- 남은 문제: `BallGrade`는 PRD §4.2가 제외한 **9종 등급 체계의 잔재**다. 새 `Spin`/`Run`/`Build`/`Risk` 코드는 이 스택을 참조하지 않는다. 씬 잔존 여부는 UP-APV-12

## 2.16 DOC — 문서 정합성

### UP-DOC-01 — Notion PRD §6.1의 정화 규칙을 인접 요구로 개정
- 분류: Required · 출처: `D-20260801-03`, PRD §1.1(이 문서가 최상위)
- 상태: NOT_STARTED · 패스: P4
- 구현: Notion 페이지 `3ada30cad9c58106b9a8c4ee03dd995c` §6.1
- 접근: 해당 없음
- 검증: Notion 원문과 `docs/MASTER_PRD.md`가 일치
- 증거: 없음
- 의존: UP-CORE-05
- 남은 문제: 저장소 동결 스냅샷만 개정됐고 **Notion 원본은 아직 "위치와 무관하게"**다. 최상위 문서가 코드와 어긋난 상태다

### UP-DOC-02 — 위험 2단계 이름을 PRD와 일치시킨다 (`Strain` vs `Warning`)
- 분류: Required · 출처: PRD §8.1
- 상태: NOT_STARTED · 패스: P4
- 구현: `Scripts/Risk/RiskLevel.cs` 또는 Notion PRD §8.1
- 접근: 해당 없음
- 검증: 이름 일치
- 증거: 없음
- 의존: UP-RISK-01
- 남은 문제: 어느 쪽을 고칠지는 되돌릴 수 있는 결정이므로 기본값(코드를 PRD에 맞춤)으로 진행한다

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
| UP-APV-14 | 기준 하드웨어 프로파일 확정 | Ryzen 5 5600X / RTX 3070 (`A-20260731-01`) | PRD §13.1 |

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
| 수정 백로그 (§5) | 6 |

| 상태 | Required 중 개수 |
|---|---|
| `VERIFIED` | **66** |
| `CONNECTED` | 25 |
| `VISIBLE` | 0 |
| `SKELETON` | 31 |
| `NOT_STARTED` | **7** |
| `BLOCKED_EXTERNAL` | 0 |

**Required 129건 중 66건(51%)이 코드·씬·테스트 증거를 모두 갖췄다.**
직전 판본의 "VERIFIED 0"은 판정 기준이 달랐기 때문이지 구현이 없어서가 아니었다 (§0.4).

> **2026-08-01 Pass 1 Wave A.** `NOT_STARTED` 가 23 → 7 로 내려갔다. 옮겨간 16건은
> 대부분 `SKELETON` 이다 — **코드와 테스트는 생겼지만 씬에 붙지 않아 게임 안에서는
> 아직 아무 일도 일어나지 않는다.** 이 구분을 흐리면 "구현했다"가 "동작한다"로
> 읽히고, 그것이 이 백로그가 막으려는 바로 그 착시다.
> EditMode 91 → **188 PASS / 0 FAIL**, 자체 검증 110 → **207 PASS / 0 FAIL**.

`VISIBLE`이 0건인 것은 후보가 없어서가 아니라, 가장 유력한 두 후보가 Required 항목이
아니라 **레거시 정리 대상**이기 때문이다 — 상세는
`docs/runtime/CURRENT_IMPLEMENTATION_AUDIT.md` §6, 추적은 `UP-TEST-11`.
