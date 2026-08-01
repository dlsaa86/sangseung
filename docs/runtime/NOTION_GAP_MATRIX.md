# NOTION ↔ 구현 GAP MATRIX — 2026-08-01

> Notion 11개 문서를 다시 읽고 현재 구현과 1:1 대조한 결과다.
> **MASTER PRD §4.1의 21항목만 Required로 취급한다** — 다른 문서에만 있는 아이디어는
> 아무리 매력적이어도 1차 프로토타입 범위가 아니다.
>
> Notion 재확인 시각: 2026-08-01. PRD 본문은 2026-07-30 스냅샷과 **동일하다**
> (§4.1 21항목 · §4.2 12항목 변동 없음).

---

# 1. PRD §4.1 「반드시 구현」 21항목 — 실증 대조

| # | PRD §4.1 항목 | 백로그 ID | 상태 | 근거 |
|---|---|---|---|---|
| 1 | Unity LTS · URP · Windows PC | `UP-PLAT-01` | **VERIFIED** | `build_report.txt` unity 6000.5.5f1 / Succeeded |
| 2 | 1인칭 이동과 마우스 시점 | `UP-SPACE-01·02` | **VERIFIED** | PlayMode 씬 배선 PASS · 8런 전부 상호작용만으로 진행 |
| 3 | 엘리베이터 내부 + 제한된 외부 층 공간 | `UP-SPACE-04·05` | **VERIFIED** | 씬 `Car`/`CarShell_Lobby*` · PlayMode "승강장에 후보가 서 있다" |
| 4 | 10층 플레이 구조 | `UP-RUN-01·02·03` | **VERIFIED** | PlayMode 6/8 런 완주 · "방문 층이 연속이다" · `curriculum_coverage.txt` |
| 5 | 한 층 최대 5회 스핀 | `UP-POWER-04` | **VERIFIED** | RunTests "스핀 소진 후 추가 Spin 거부" |
| 6 | 세 통관 × 3칸 = 3×3 자동 룰렛 | `UP-CORE-02`, `UP-DEVICE-01·02` | **VERIFIED** | `Cell_0/1/2`가 `Tube_0/1/2`의 자식 (씬 계층 실증) |
| 7 | 정상 영혼 1종 | `UP-CORE-03` | **VERIFIED** | SpinEngineTests |
| 8 | 저항체 2종 (흡수체·증식체) | `UP-CORE-04` | **VERIFIED** | SpinEngineTests |
| 9 | 계약 2종 | `UP-CONTRACT-03` | **VERIFIED** | capture 14 "선택지 2종" · BuildTests 계약 런 완주 |
| 10 | 같은 저항체 3개 이상 기본 정화 | `UP-CORE-05` | **VERIFIED** | "정화된 칸은 서로 붙어 있다" 외 3건 |
| 11 | 직선 3개 보너스 | `UP-CORE-06` | **VERIFIED** | "같은 저항 3개 직선 3종 → LineKind" |
| 12 | 4개 이상 직교 연결 + 캐스케이드 | `UP-CORE-07·08·09` | **VERIFIED** | "직교 연결 4개 → Cluster와 재충전" · 하드캡 4건 |
| 13 | 잔류 효과 | `UP-CORE-10` | **VERIFIED** | "흡수체 잔류 → NetPower 차감" · "증식체 잔류가 다음 스핀 가중치를 올림" |
| 14 | 전력 · 요구 전력 · 초과 임계점 | `UP-POWER-01·02·08` | **부분** | 01·08 VERIFIED / **02 CONNECTED** — `PowerBand.Damaged` 소비처 0곳 |
| 15 | 전력 확정과 과수확 레버 | `UP-POWER-03·05`, `UP-DEVICE-03·04` | **VERIFIED** | PlayMode "탱크로 층을 끝낼 수 있다" · capture 11/12/13 |
| 16 | 승객·부품 최소 4종 | `UP-BUILD-01·02` | **VERIFIED** | 카탈로그 11종 · "서로 다른 두 빌드가 결과를 바꾼다" |
| 17 | 과적과 요구 전력 증가 | `UP-BUILD-03·04` | **VERIFIED** | "과적이 요구 전력에 배수를 건다" · capture 09 과적 218/130 → Warning |
| 18 | 감각적 위험 상태 시스템 | `UP-RISK-01`~`09` | **부분** | 상태기계·동기화·과수확 VERIFIED / 조명·진동 CONNECTED / **음향·Collapse 연출·프로파일·접근성은 코드가 생겼으나 씬 배선 전(SKELETON)** |
| 19 | 승객 상황 반응 | `UP-NPC-01`~`05` | **부분** | 위험 반응 CONNECTED / **반응 11종 매핑·중재기(우선순위·쿨다운·동시 수)·자세 7종·시선 6종이 코드와 테스트로 존재한다. 씬에서 승객이 아직 움직이지 않는다** |
| 20 | 사고 기록기 | `UP-REC-01`~`05` | **부분** | 기록 생성 VERIFIED / **월드 기계식 프린터 코드가 생겼다(`PaperTapePrinterView`). 씬에 장치가 없어 여전히 오버레이만 보인다** |
| 21 | 디버그 패널 · 결정론적 시드 · 텔레메트리 | `UP-TEST-06`, `UP-CORE-01`·`UP-RUN-05`, `UP-TEST-05` | **부분** | 시드 VERIFIED · 패널 CONNECTED / **텔레메트리 SKELETON** — 코드와 17건의 테스트는 있고 씬에 붙지 않았다 |

**21항목 중 완전 충족 15 · 부분 충족 6 · 미착수 0.**

## 2026-08-01 정정 — "§16.2가 20개 필드를 지정한다"는 사실이 아니다

이 문서의 직전 판본은 텔레메트리를 두고 "PRD §16.2가 20개 필드를 지정하는데 코드에
존재하지 않는다"고 적었다. **Notion 원문을 직접 열어 보면 §16.2 「플레이 로그」의
항목은 11개다.** 20이라는 숫자는 어느 계획 문서의 해석이었고, 그것이 이 문서로,
다시 백로그 제목으로 옮겨 앉았다.

차이가 중요한 이유: 개수를 기준으로 삼으면 **"20개를 채웠으니 완료"** 가 성립해 버린다.
실제로 첫 구현은 20필드를 정확히 채우고도 §16.2의 열한 항목 중 **다섯을 빠뜨렸다** —
캐스케이드별 보드 · 정화/발동 순서 · 현재 위험 단계 · 승객·부품 발동 ·
프레임 타임과 GC Alloc. 런 종료 원인은 스핀의 속성이 아니므로 런 단위 레코드가 따로 필요하다.

검증 기준을 개수에서 **11항목 대조표**로 바꿨다 (`docs/DECISION_LOG.md` `D-20260801-06`).

---

# 2. PRD §4.2 「명시적 제외」 12항목 — 침범 여부

| # | 제외 항목 | 코드에 있는가 | 판단 |
|---|---|---|---|
| 1 | 통관별 정지 버튼·타이밍 정지 | **있다** — `Scripts/Roulette/TubeController.cs` (브레이크·수확창 정지). 씬에서 활성, 구동자는 비활성 | **정리 필요** (`UP-TEST-11`). 게임 규칙에는 연결되지 않았다 |
| 2 | 구슬 위치 이동·교환 | 없다 | 준수 |
| 3 | 연타·리듬·정밀 클릭 | 없다 | 준수 |
| 4 | L·T·십자·고리 특수 패턴 | 없다 | 준수 |
| 5 | 정상 영혼 9종 등급 체계 | **있다** — `Scripts/Data/BallGrade.cs` (Common/Advanced/Rare/Legendary), `Data/Balls/Ball_01~09.asset` | **정리 필요** (`UP-TEST-11`). 새 스택은 참조하지 않는다 |
| 6 | 추가 저항체 | 없다 (`SymbolKind`에 2종만) | 준수 |
| 7 | 완성형 멀티 엔딩 | 없다 | 준수 |
| 8 | 완성형 대화·관계도 | 없다 | 준수 |
| 9 | 온라인·멀티플레이·Twitch | 없다 | 준수 |
| 10 | 최종 캐릭터 아트·애니메이션 | 없다 (플레이스홀더 형상) | 준수 |
| 11 | 최종 수치 밸런스 | 임시값 유지 | 준수 (`UP-APV-05`) |
| 12 | 장기 메타 진행·세이브 슬롯 | 없다 | 준수 |

**12항목 중 10항목 준수, 2항목은 죽은 레거시 코드로만 존재한다.**
둘 다 실행되는 게임 규칙이 아니므로 "제외 위반"이 아니라 **미정리 부채**다.

---

# 3. Notion에는 있으나 PRD §4.1에 없는 것 — Deferred 유지

| Notion 출처 | 내용 | 백로그 |
|---|---|---|
| N03 「특수 숫자 층」, N04·N05 | 444·666·777 정확 정차 이벤트 | `UP-DEF-08` |
| N04 「괴담 콘텐츠 방향」 | 존재하지 않는 층 버튼, 인원수 불일치 등 | `UP-DEF-09` |
| N04 「몬스터의 기능」 | 심볼 풀 오염, 통관 잠금, 전력 탈취 | `UP-DEF-10` |
| N05 전체 | 세계 수확 장치 반전, 엔딩 5종 | `UP-DEF-11` |
| N02 「경제·정차 계열」, N04 「NPC의 경제 활동」 | 상인·거래·운송료·하강 | `UP-DEF-13` |
| N03 「테마 제작 원칙」 | 10·100·200·500층 테마 전환 | `UP-DEF-14` |
| N06 §1~11 | Clay/Material Turnaround 에셋 파이프라인 | `UP-DEF-20` |
| N01 「보호체 계약」, N07 「후속 저항체」 | 고정체·위장체·보호체·무게체 | `UP-DEF-06·07` |
| N08 §4.1 | Bootstrap / ElevatorPrototype 2씬 분리 | `UP-DEF-18` |
| N99 「핵심 재화: 전력·돈」 | 돈 자원 | `UP-RUN-09` — **이미 구현·테스트됨. 제거하지 말 것** |

`UP-RUN-09`만 예외적으로 "Deferred인데 구현돼 있는" 항목이다. 범위 이탈이 이미
일어났으나 초과 전력 처분에 쓰이고 `BuildTests`가 지키고 있으므로 되돌리지 않는다.

---

# 4. 최상위 문서와 코드가 어긋난 곳 2건

| # | Notion 원문 | 코드 | 조치 |
|---|---|---|---|
| 1 | PRD §6.1 "같은 종류 저항체가 3개 이상이면 **위치와 무관하게** 기본 정화한다" | **인접을 요구한다** (`d97ed3e`, 테스트 3건이 이를 강제) | `UP-DOC-01` — 사용자 지시로 `D-20260801-03`이 이미 내려졌고 저장소 스냅샷은 개정됐다. **Notion 원본만 남았다** |
| 2 | PRD §8.1 위험 2단계 = `Strain` | `RiskLevel.Warning` | `UP-DOC-02` — 되돌릴 수 있는 결정. 기본값은 코드를 PRD에 맞춘다 |

**둘 다 Notion 재확인(2026-08-01)에서 여전히 옛 문장임을 확인했다.**

---

# 5. PRD §14.3 권장 데이터 자산 13종 — 실재 여부

| 자산 | 실재 | 비고 |
|---|---|---|
| `GameBalanceProfile` | △ | `Data/PrototypeConfig.asset` + `SpinRuleSet`가 나눠 맡음 |
| `FloorProgressionProfile` | △ | `Scripts/Spin/FloorPlan.cs`에 코드로 있음 (에셋 아님) |
| `SymbolDefinition` | △ | `SymbolKind` enum + `SpinRuleSet` 가중치 |
| `ContractDefinition` | △ | `Scripts/Spin/ResistanceContract.cs` |
| `PassengerDefinition` | ✕ | 레거시 에셋만 존재. 새 스택은 `BuildCatalog` 코드 배열 |
| `PartDefinition` | ✕ | 같음 |
| `OverharvestProfile` | ✕ | `UP-POWER-07` NOT_STARTED |
| `DangerFeedbackProfile` | △ | `RiskProfile` 구조체 (에셋 아님) — `UP-RISK-07` |
| `PassengerReactionSet` | ✕ | `UP-NPC-03` NOT_STARTED |
| `VisualQualityProfile` | ✕ | `UP-PLAT-05` NOT_STARTED |
| `AudioMixProfile` | ✕ | `UP-AUD-05` NOT_STARTED |
| `AccessibilityProfile` | ✕ | `UP-RISK-08` NOT_STARTED |
| `RunSummaryTemplate` | ✕ | `FloorRecord` 코드에 문장 하드코딩 |

**13종 중 에셋으로 실재하는 것은 0종, 코드로 대체된 것 5종, 아예 없는 것 8종.**
PRD §1.2 "모든 가변 요소는 데이터 또는 프로파일로 분리한다. 하드코딩 후 전체 코드를
고쳐야 하는 구조는 실패다"에 정면으로 걸린다 — `UP-TECH-09`.

---

# 6. PRD §15.1 필수 캡처 9종 — 1:1 대조

| PRD §15.1 요구 | 현재 캡처 | 충족 |
|---|---|---|
| 엘리베이터 입구에서 본 전체 내부 | `01_entry` | △ — 존재하나 **높이가 읽히지 않음** (`UP-FIX-01`) |
| 룰렛 정면 | `02_device_front` | ○ |
| 계약 선택 상태 | `14_contract_select` | ○ |
| Stable 상태 | `06_risk_stable` | ○ |
| Critical 상태 | `10_risk_critical` | ○ |
| 과수확 레버 접근 순간 | `11`·`12`·`13` | ○ (3장으로 분해) |
| 5연쇄 이상 | `15_cascade_deep` (8단계 중 5단계) | ○ |
| 사고 직후와 사고 기록기 | `16_risk_collapse` + `17_accident_recorder` | △ — 17번만 해상도·방식 다름 (`UP-FIX-06`) |
| 승객 4명과 화물 최대 적재 | `07_cargo_full` + `08_passenger_and_device` | ○ |

**9종 중 7종 완전 충족, 2종 조건부.** 여분 캡처 4장(`03`·`04`·`05`·`18`)은 PRD 요구
외의 보조 프레임이다.

---

# 7. PRD §17 Definition of Done 대조

| DoD | 상태 | 근거 |
|---|---|---|
| §17.1 기능 완료 | **거의 충족** | 10층 완주 6/8런 · 계약~과수확 연결 · 위험 동기화 · 진행 불가 0 · 콘솔 오류 0 |
| §17.2 가독성 완료 | **미검증** | 실제 플레이테스터 관찰이 필요 (`UP-DEF-21`) |
| §17.3 감정·방송성 완료 | **미검증** | 같음 |
| §17.4 기술 완료 | **미충족** | 90 FPS 판정 불가(vSync 상한) · GC 9~11 KB/프레임 · 풀링·압축 Preset 없음 · 메모리 누적 미측정 |
| §17.5 비주얼 완료 | **미충족** | 루브릭 **REJECT** · Critical/Collapse 미구분 |
| §17.6 증거 산출물 | **부분** | 빌드·테스트·성능·캡처·시드 있음 / **영상 2종 없음** |
</content>
