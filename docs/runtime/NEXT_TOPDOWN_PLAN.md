# NEXT TOPDOWN PLAN — 2026-08-01 실증 감사 이후

> 근거는 `CURRENT_IMPLEMENTATION_AUDIT.md`와 `NOTION_GAP_MATRIX.md`다.
> **이미 VERIFIED인 64건은 이 계획에 없다.** 재작성 금지 목록은 §5.

---

# Pass 1 — 실제로 없거나 골격뿐인 필수 범위

목표는 완성도가 아니라 **PRD §4.1 필수 범위가 한 번은 전부 존재하게** 만드는 것이다.
차단 조건은 셋뿐이다 — 컴파일 오류, 데이터 손상, 진행 불가.

완료 조건: Required 중 `NOT_STARTED` 0건 (현재 **23건**).

## 1-1. 텔레메트리 — `UP-TEST-05` ★ 첫 작업

**현재 가장 큰 범위 누락이다.** PRD §4.1이 명시적으로 요구하고 §16.2가 20개 필드를
지정하는데 **코드에 존재하지 않는다.** `Scripts/Sim/SimRecords.cs`는 시뮬레이터 전용이라
인게임 런을 기록하지 않는다.

이것을 첫 번째로 두는 이유는 크기 때문만이 아니다. **이후의 모든 밸런스 판단이
사후 추정이 된다.** 지금 `PENDING_DECISIONS.md`의 PD-09(밸런스)를 사용자가 결정하려 해도
근거로 낼 런 데이터가 없다.

- 기록 시점: 스핀 종료마다
- 필드: PRD §16.2 / N08 §18의 20항목
- 형식: JSON 또는 CSV, `Logs/telemetry/`
- 완료 판정: 런 1회 후 파일 존재 + 20필드 대조표

## 1-2. 승객 반응 시스템 — `UP-NPC-02` · `UP-NPC-03` · `UP-NPC-05`

PRD §4.1 #19가 **10종 중 1종**(Critical 진입)만 구현돼 있다.
§9.2가 지정한 나머지 9종 — 계약 선택 / 기본 정화 / 5연쇄 / 임계점 100·170·300% /
과수확 해금 / 과수확 접근 / 추가 스핀 / Collapse 직전 / 사고·성공.

- `PassengerReactionSet` ScriptableObject로 이벤트별 교체 가능하게 (§9.4)
- 우선순위·쿨다운·최대 동시 반응 수 (§9.4 마지막)
- 표현은 자세 + LookAt까지. 최종 모션은 `PD-02` 승인 대기

## 1-3. 사운드 채널 — `UP-AUD-02` · `UP-AUD-03` · `UP-AUD-04` · `UP-AUD-05`

PRD §4.1 #18의 감각 채널 넷(조명·소리·진동·승객) 중 **소리만 사실상 비어 있다**
(절차 생성 hum 1채널). §11의 "무음 관전자" 기준과 §18 Phase 4 통과 조건("오디오만 듣는
테스트에서 위험 단계 구분")이 여기에 걸린다.

- N08 §16.4의 10종 이벤트 사운드
- 과수확 0.3~0.7초 정적 (PRD §7.3)
- 승객 비언어 음성
- 유료 에셋·라이선스 불명확 파일 금지 → 절차 생성 유지 또는 자작

## 1-4. 데이터 프로파일 8종 — `UP-POWER-07` · `UP-RISK-07` · `UP-RISK-08` · `UP-PLAT-04` · `UP-PLAT-05` · `UP-AUD-05` · `UP-TECH-09`

PRD §14.3 권장 자산 13종 중 **에셋으로 실재하는 것이 0종**이다.
PRD §1.2 "하드코딩 후 전체 코드를 고쳐야 하는 구조는 실패다"에 정면으로 걸린다.

`UP-PLAT-04`(`TargetHardwareProfile`)는 특히 급하다 — **PRD §13.1이 미지정 상태에서
성능 완료 선언을 금지**하므로 Pass 4의 선행 조건이다.

## 1-5. 사고 기록기의 물리적 형태 — `UP-REC-04` · `UP-REC-05`

PRD §10.1은 "기계식 프린터, 종이 테이프 또는 펀치카드 장치"를 요구한다.
현재는 `ScreenSpaceOverlay` 텍스트다. `PD-14` 승인 대기지만 **기본 프리셋으로 진행**한다.

## 1-6. 비주얼 기반 — `UP-VIS-04` · `UP-VIS-05`

URP 공통 스타일 셰이더와 파티클. Pass 1에서는 **존재만** 만든다. 품질은 Pass 3.

## 1-7. 성능 측정 지점 — `UP-TECH-06` · `UP-TECH-07` · `UP-TECH-08`

풀링·렌더링 예산·메모리 누적. Pass 1에서는 측정 지점과 뼈대만.

## 1-8. 증거 영상 — `UP-TEST-08` · `UP-TEST-09`

PRD §17.6이 5연쇄 영상과 Critical→과수확→결과 영상을 증거로 요구한다.
캡처 하네스에 녹화가 없다.

## 1-9. 남은 방어 테스트 — `UP-CORE-14`

가중치 합이 0일 때 명시적 오류를 내는지 검증하는 테스트. 방어 코드는 있고 테스트가 없다.

## 1-10. 문서 정합 — `UP-DOC-01` · `UP-DOC-02`

Notion PRD §6.1(정화 인접)과 §8.1(`Strain` vs `Warning`).
**최상위 문서가 코드와 어긋난 상태를 오래 두지 않는다.**

---

# Pass 2 — 존재하지만 실제 플레이와 연결되지 않은 범위

완료 조건: Required 중 `SKELETON`·`VISIBLE` 0건 (현재 **16건**).

| 순 | ID | 무엇이 끊겨 있는가 |
|---|---|---|
| 1 | `UP-RISK-05` · `UP-RISK-06` | Collapse 단계가 **상태로만** 존재한다. PRD §8.2의 6개 연출 요소(암전→파열음→급강하→불규칙 재점등)가 없어 Critical과 구분되지 않는다 |
| 2 | `UP-POWER-06` | 과수확 상호작용 5단계 중 해금 연출만. 「기계음 감소 → 승객 응시 → 정적」이 없다 |
| 3 | `UP-NPC-04` | 표현 채널이 자세뿐. 시선·대사·비언어 음성 없음 |
| 4 | `UP-SPACE-09` | 등을 돌렸을 때 HUD 하나에만 의존. 사운드 채널이 생기면 연결한다 |
| 5 | `UP-TECH-02` | `FindAnyObjectByType` 폴백이 여러 곳에 남아 실행 순서 의존이 있다. 자동 검사가 없다 |
| 6 | `UP-VIS-01` | 스타일 락이 그레이박스 상태. 재질·실루엣 언어 없음 |
| 7 | **레거시 정리** `UP-TEST-11` | `TubeController` ×3이 **활성 + 완전 배선**으로 남아 있다(PRD §4.2 제외 설계). `ElevatorGrayboxView`가 살아 있는 문·앵커·라벨 12개를 죽은 모델로 잡고 있다 → `PD-13` 승인 필요 |

**Pass 2에서 `UP-TECH-04`·`UP-TECH-05`(성능·GC)는 손대지 않는다.** 측정 방법 자체가
깨져 있어(vSync 상한) Pass 4에서 프로브를 고친 뒤 다룬다.

---

# Pass 3 — 전체 게임 경험·비주얼·피드백 개선

완료 조건: PRD §15.2 루브릭 통과 + `VISUAL_VERDICT.md`가 `ACCEPT`.

## 3-1. 수정 백로그 소진 (독립 평가 REJECT에서 전환된 6건)

| ID | 지적 | 주의 |
|---|---|---|
| `UP-FIX-01` | `01_entry`가 공간의 높이를 보여주지 못한다 (높이 프레임 0장) | **평가자 최우선** |
| `UP-FIX-02` | 임계점 눈금 숫자 라벨 없음 | **3회 실패. 네 번째 시도 금지** — 필요한 것은 배치 결정(게이지 배면 각인 또는 상태 블록 상향) |
| `UP-FIX-03` | 과수확 레버가 당김을 형상으로 전달 못함 | 하우징 안 + 카메라 반대 방향 |
| `UP-FIX-04` | 좌측 벽 라벨 거울상 렌더 | |
| `UP-FIX-05` | Critical과 Collapse 미구분 | Pass 2의 `UP-RISK-06`이 선행 |
| `UP-FIX-06` | 17번 캡처만 해상도·방식 다름 | 캡처 리그의 기존 해법 사용 |

## 3-2. 판독성 마감

`UP-CORE-11`·`UP-CORE-12`·`UP-CORE-13` (순차 공개·판정 원인 점등·숫자 과밀 방지),
`UP-DEVICE-06`·`UP-DEVICE-09`·`UP-DEVICE-10`, `UP-VIS-02`·`03`·`06`·`08`·`10`,
`UP-VIS-09`(축소 화면 판독 — 한 번도 검사한 적 없다).

## 3-3. 위험 연출 완성

`UP-RISK-03`·`UP-RISK-04` — 조명·진동은 연결돼 있으나 단계 간 감정 차이가 부족하다.

---

# Pass 4 — 전체 테스트·성능·빌드·회귀·정리

완료 조건: `tools/verify-topdown.ps1`이 `TOPDOWN_ALL_PASSES_COMPLETE` 출력.

| 순 | 작업 | 비고 |
|---|---|---|
| 1 | **성능 프로브 수정** — `UP-TECH-04` | 중앙값이 vSync 상한 8.33ms에 고정돼 90 FPS 목표를 **판정할 수 없다.** 상한 없는 측정으로 바꾸고 `TargetHardwareProfile` 기준으로 재측정 |
| 2 | **GC 원인 분해** — `UP-TECH-05` | 9,000~11,000 B/프레임. 목표 0 B. 원인 미분해 |
| 3 | 풀링·렌더링 예산·메모리 누적 검증 | `UP-TECH-06`·`07`·`08` |
| 4 | 레거시 삭제 또는 `_Legacy/` 격리 | `UP-TEST-11` — `PD-13` 승인 후. 자체 검증 110건에 섞인 레거시 구슬 단정도 함께 정리 |
| 5 | 폰트 아틀라스 복구 | 미커밋 상태로 방치된 글리프 순손실 |
| 6 | 전체 회귀 | EditMode + PlayMode + 서로 다른 시드 3개 이상 |
| 7 | Windows 빌드 재실행 | 경고 506건 점검 |
| 8 | 독립 시각 평가 재수행 | `VISUAL_VERDICT.md` ACCEPT |
| 9 | Required 129건 전부 `VERIFIED` 전환 | |

---

# 5. 재작성하면 안 되는 시스템 — 이미 VERIFIED

**이 목록의 어떤 것도 "없는 줄 알고" 다시 만들지 않는다.**
증거는 `CURRENT_IMPLEMENTATION_AUDIT.md` §4에 있다.

| 시스템 | 구현 위치 | 증거 |
|---|---|---|
| 결정론적 자동 3×3 룰렛 | `Scripts/Spin/SpinEngine.cs`, `SpinSeed.cs`, `SpinBoard.cs` | SpinEngineTests 29건 · "같은 시드 → 완전 동일" |
| 정상 영혼·흡수체·증식체 | `Scripts/Spin/SymbolKind.cs`, `SpinRuleSet.cs` | 위 스위트 |
| 계약 2종과 4값 동시 변경 | `Scripts/Spin/ResistanceContract.cs` | "계약이 네 값을 함께 움직임" · capture 14 |
| 개수 정화(인접)·직선·연결·캐스케이드·하드캡 20 | `Scripts/Spin/SpinEngine.cs` | SpinEngineTests 다수 · capture 15 (8단계) |
| 실행 레버 / 잠금식 과수확 레버 | `Scripts/Player/InteractableLever.cs`, `InteractableOverharvestLever.cs`, `View/OverharvestUnlockEffect.cs` | PlayMode · capture 11/12/13 (판돈 46 실측) |
| 전력·요구 전력·임계점·확정 | `Scripts/Core/FloorMath.cs`, `Spin/PowerThresholds.cs`, `Player/InteractablePowerTank.cs` | RunTests · PlayMode "탱크로 층을 끝낼 수 있다" |
| 위험 4단계와 Collapse **판정** | `Scripts/Risk/RiskEvaluator.cs`, `RiskLevel.cs` | RiskEvaluatorTests 11건 · capture 16 (실제 Collapse 도달) |
| 사고 기록기 **데이터** | `Scripts/Run/AccidentRecorder.cs`, `FloorRecord.cs` | PlayMode "층마다 기록했다" · capture 17 |
| 승객·부품·무게·과적 | `Scripts/Build/BuildLoadout.cs`, `BuildItem.cs`, `BuildFigureView.cs` | BuildTests 35건 · capture 07/09 |
| 1~10층 커리큘럼과 건너뛰기 클램프 | `Scripts/Spin/FloorPlan.cs`, `Core/FloorMath.ClampAscent` | "방문 층이 연속이다" · `curriculum_coverage.txt` |
| Windows 빌드 파이프라인 | `Assets/Editor/WindowsBuildTask.cs` | `build_report.txt` Succeeded / 0 오류 |
| 캡처·성능·테스트 하네스 | `Assets/CaptureHarness/`, `Run/Tests/*`, `Editor/PrototypeSelfTest.cs` | manifest 18장 · 91/394 PASS |

**주의 — 위 목록에서 "판정"과 "연출"을 구분한다.**
위험 4단계의 *판정*은 VERIFIED지만 Collapse의 *연출*은 SKELETON이다(`UP-RISK-06`).
사고 기록기의 *데이터*는 VERIFIED지만 *물리적 형태*는 SKELETON이다(`UP-REC-04`).
Pass 1·2에서 손댈 것은 뒤쪽이지 앞쪽이 아니다.
</content>
