# 자율주행 세션 범위 — 2026-08-06 「데모 플레이 차선」

> 진입 근거: 사용자 출근 선언 + 지시 「노션 GAMEPLAY CORE 보고 게임이 재밌어질 때까지
> 밸런스나 구조 작업 다 해줘 / 모든 빌드를 다 체험해볼 수 있게 데모플레이가 가능한
> 수준으로 / 모델링은 열심히 할 필요 없어」 (2026-08-06 09:5x)
> 기준 문서: Notion 「🎯 GAMEPLAY CORE — 재미 검증 단일 기준」

---

## 0. 이 세션이 좁혀진 이유 — 동시 세션

진입 시점에 **다른 Claude Code 세션이 같은 저장소를 쓰고 있었다.** 09:53~09:54에
`Spin/SpinEngine.cs` · `SpinRuleSet.cs` · `ResistanceContract.cs` · `FloorPlan.cs` ·
`Run/FloorSession.cs` · `Build/BuildLoadout.cs` · `tools/headless/Runner/ContractProbe.cs` ·
`docs/runtime/PENDING_DECISIONS.md` 를 수정했고 `docs/runtime/PD2930_REPORT_20260806.md`
(PD-29·PD-30 구현 보고, 93 PASS)를 남겼다.

그 세션이 밸런스 수치·계약 규칙을 이미 파고 있으므로 **같은 파일에 손대면 서로 덮어쓴다.**
`CLAUDE.md` 「에이전트 소유권 규칙」의 정신을 세션 단위로 적용해 차선을 나눈다.

| 소유 | 경로 |
|---|---|
| **다른 세션 (건드리지 않는다)** | `Scripts/Spin`, `Scripts/Build`, `Scripts/Run`, `tools/headless`, `docs/runtime/PENDING_DECISIONS.md` |
| **이 세션** | `Scripts/Demo` (신규), `Scripts/UI`, `Scripts/View`, 이 문서 |

읽기는 양쪽 다 자유. 쓰기만 배타.

---

## 1. 목표

플레이어(그리고 플레이테스터)가 **모든 빌드를 실제로 태워보고 결과를 눈으로 확인할 수
있는 상태**를 만든다. GAMEPLAY CORE §11 「반드시 검증」 중 이 세션이 여는 것:

- 승객·부품 선택이 결과를 바꾸는 체감
- 첫 플레이어가 결과 원인을 설명할 수 있는지 (§10 판독성)

### 왜 이것이 지금 값나가나

`docs/runtime/BUILD_DIVERSITY_AUDIT.md`(2026-08-05)의 결론은 **「명확히 다른 두 빌드
전략」이 성립하지 않는다**였다 — 한 품목이 이득의 83%, 11종 중 5종이 음의 기여,
55개 짝 중 시너지 ≥+1%p가 0개. 효과가 완전히 가산적이라 최적해가 하나뿐이다.

그 **설계 결함 자체의 수정은 `BuildLoadout.ApplyTo(SpinRuleSet)`에 있고 그 파일은 지금
다른 세션의 것이다.** 이 세션은 대신 **그 결함을 사람이 직접 만져 볼 수 있는 장치**를
만든다 — 임의 적재를 지정해 런을 돌리고 결과를 나란히 보는 수단. 수정이 실제로 효과가
있었는지 판정하려면 어차피 이 장치가 먼저 있어야 한다.

---

## 2. Acceptance Criteria

| # | 기준 | 검증 방법 |
|---|---|---|
| AC-1 | 카탈로그의 **모든** 빌드 품목을 임의 조합으로 적재해 런을 시작할 수 있다 | 에디터 메뉴에서 조합 지정 → 런 시작 |
| AC-2 | 지정 적재가 `BuildLoadPolicy` 자동 선택을 **덮어쓴다** (제시 운에 의존하지 않는다) | 고정 시드로 두 번 돌려 적재가 동일 |
| AC-3 | 품목 단독 / 짝 조합의 결과를 **표로 비교**할 수 있다 | 산출 문서에 표 생성 |
| AC-4 | 한 스핀의 인과 사슬(§10 여섯 단계)이 로그로 남는다 | 텍스트 산출물 확인 |
| AC-5 | 컴파일 오류 0 · 기존 테스트 회귀 0 | `Ascend/Run Self Tests` |

---

## 3. 금지 범위

- `Scripts/Spin` · `Scripts/Build` · `Scripts/Run` · `tools/headless` **쓰기**
- 모델링·3D 에셋 작업 (사용자가 블렌더로 직접 작업 중)
- 최종 비주얼 스타일, 머티리얼, 조명
- 원격 push · PR · main 병합
- 밸런스 **수치** 변경 — 다른 세션 소유

---

## 4. 진행 로그

(아래에 배치 단위로 append)
