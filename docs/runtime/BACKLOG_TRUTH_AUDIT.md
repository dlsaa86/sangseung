# 백로그 실상 감사 — 19항목 · 26 에이전트 (2026-08-02)

> 15 `SKELETON` + 4 `NOT_STARTED` 항목의 **실제** 상태를 코드·씬 YAML·로그에서 직접 확인했다.
> **판정마다 적대적 반박자를 붙였다** — 반박자의 기본 입장은 「이 판정은 너무 후하다」이고,
> 확신이 없으면 낮은 쪽으로 기울이도록 지시했다.
>
> 반박 성공 3건(`UP-DOC-01` ↑, `UP-POWER-06` ↓, `UP-POWER-07` ↓, `UP-SPACE-09` ↑).
> 이 문서는 통합 산출물이며, 정정 결과는 `TOPDOWN_MASTER_BACKLOG.md` 에 이미 반영돼 있다.

## 0. 전제 — 작업 트리가 감사보다 앞서 있다

감사 19건은 **HEAD 기준**이다. 현재 작업 트리에는 미커밋 변경 18파일(+989줄)과 신규 6파일이 있고, 이것이 판정 몇 건을 이미 바꿔 놓았다. **씬(`.unity`)만 손대지 않은 상태다.**

| 이미 들어온 것 (미커밋) | 감사 판정을 어떻게 바꾸는가 |
|---|---|
| `Run/FloorSession.cs`·`RunSessionBehaviour.cs` — `OverharvestProfile` 9필드 중 판돈·해금 임계·추가 스핀 상한 소비 + `RunTests` 단정 3건 | UP-POWER-07 의 「6필드 소비처 0곳」이 해소됨. **단 씬 배선이 없어 런타임은 여전히 코드 기본값** |
| `Player/{CrosshairInteractor,CrosshairView,FirstPersonController}.cs` — `[RequiredReference]` 5필드 추가(전체 5→17) | UP-TECH-03 표시 범위 확대, **UP-TEST-11 웨이브 1 전제 충족** |
| `UI/GameHudView.cs` — `_auxGroup·_auxRatioText·_auxStateText·_auxRiskLamps·_summaryTemplate·_risk` | UP-SPACE-09 화면공간 채널 신설. **씬 배선 0** |
| `Npc/PassengerReaction*.cs` — `_audio` 필드 + `VoiceCueCount` | UP-NPC-04 ④·UP-AUD-04 결선 준비. **씬 배선 0, `PlayPassengerVoice` 호출은 아직 없음** |
| 신규 `Audio/DangerBed.cs`·`Audio/PassengerVoiceKind.cs`·`Data/Profiles/PresentationProfile.cs`·`Data/Profiles/AssetImportRules.cs`·`Run/RunSummaryBuilder.cs` | **5파일 전부 자기 파일 밖 참조 0곳** — 지금 이 순간 죽은 구현이다 (`AssetImportRules` 만 `Assets/Editor/AscendImportRules.cs` 소비처 있음) |
| `Diagnostics/Tests/WiringDiagnosticsTests.cs` (신규) | `PrototypeSelfTest.FoldInSuite` 목록(:68-85)에 **미등록 → 한 번도 안 돈다** |

---

## 1. 백로그 정정표

`docs/TOPDOWN_MASTER_BACKLOG.md` 의 **현재 파일 값** 기준이다(감사 입력의 `backlogState`는 HEAD 값이라 일부 다르다).

| ID | 백로그(파일) | 실제 | 근거 한 줄 |
|---|---|---|---|
| UP-TECH-03 | SKELETON | **CONNECTED** | 구현이 `Player/PlayerSetupValidator.cs`(죽음)가 아니라 `Diagnostics/SceneWiringValidator.cs:84-90` 의 `RuntimeInitializeOnLoadMethod` 다. `Logs/Editor.log` 에 `[배선] 필수 참조 이상 없음` 22회 |
| UP-POWER-07 | SKELETON | **VISIBLE** | 「런타임 소비처 0곳·씬 참조 없음」이 거짓 — `.unity:9524` 배선 + `AudioDirector.cs:258` 소비. 다만 규칙 4값이 코드 const 라 VISIBLE (작업 트리 수정으로 CONNECTED 직전) |
| UP-POWER-06 | SKELETON | **VISIBLE** | 「씬에서 서로 이어지지 않았다」가 거짓 — 3컴포넌트 전부 `m_GameObject: 998528525`. 대신 5단계(재개) 구현 0 + 1단계 발화 0회 |
| UP-AUD-04 | SKELETON | **CONNECTED** | `AudioDirector.cs:495 TryPlayEventVoice` 가 실제 재생까지 감 — `PlayedKindsMask` 는 `PlayOneShot` 뒤에만 선다(:664-672), 로그 :2129 에 `PassengerVoice` |
| UP-RISK-05 | SKELETON | **CONNECTED** | 「씬 배선 전이라 들리지 않는다」가 거짓 — `AudioDirector` 씬 :9519, MetalStress·Siren·CollapseImpact·ResidualDamage 전부 재생됨 |
| UP-SPACE-09 | SKELETON | **CONNECTED** | 2D 사운드(`spatialBlend=0`, 16종 발화) + `RiskStateView.cs:340` 전역 앰비언트가 방향 무관 채널로 실동작. 로그 :2132 PASS |
| UP-REC-05 | SKELETON | **CONNECTED** | 「17번만 해상도가 다르다」가 거짓 — 23장 전부 PNG IHDR 1920×1080, 화면 캡처는 4장(17·20·22·23) |
| UP-DOC-01 | CONNECTED | **VERIFIED** | 독립 확인자가 `notion-fetch` 로 원본 §6.1·§4.1 개정 확인, `MASTER_PRD.md:78`·`:128-129` 와 문장 일치 |
| UP-VIS-07 ⚠ | CONNECTED | **NOT_STARTED (충돌)** | 판정 7회 전부 REJECT(최고 2.78/2.35, 요구 4.0), `B-5 #15` 4라운드 연속 열림. §3 참조 |
| UP-VIS-09 ⚠ | CONNECTED | **NOT_STARTED (충돌)** | 세트 전체 판정 0건 + 7차가 480×270 Strain↔Critical 판독 실패를 실측. §3 참조 |

⚠ **충돌 두 건은 조용히 되돌리지 말 것.** 백로그의 CONNECTED 승격 논거(「없는 것은 구현이 아니라 합격이다 — NOT_STARTED 로 두면 Pass 1 이 Pass 3 실패로 막힌다」)는 게이트 구조상 타당하고, 감사 논거(「구현 필드가 평가 절차를 가리킨다·ACCEPT 0건」)도 타당하다. 상태값 논쟁과 무관하게 **세 결함은 무조건 고친다**: ① `구현:` 필드를 평가 절차(`docs/VISUAL_CRITERIA.md`)가 아니라 평가 대상(캡처 세트+씬)으로 ② 출처 `PRD §15.2` → 동결 PRD 에 없다(§11 이 시각 항목, 「4.0」은 전 문서 0건) ③ 낡은 점수(2.6/2.45 → 6차 2.78/2.35)와 이미 해소된 「가장 먼저 고칠 것」(HUD 좌측 잘림, `VISUAL_VERDICT.md:139`).

**정정 불필요 확인**: UP-TEST-11 은 HEAD 기준 NOT_STARTED 였으나 작업 트리에서 웨이브 0 완료 + `Effects/IEffect.cs` 삭제가 실제로 들어와 **SKELETON 이 맞다.** UP-PLAT-05·UP-TECH-04·05·06·09·VIS-01·04·NPC-04·AUD-05 는 그대로.

---

## 2. 작업 순서 (여는 항목 ÷ 비용)

### ★ W1 — 씬·에셋 배선 한 번 (**씬 필요 · 단일 소유자 · 캡처 불필요**)

**혼자, 한 번에.** 작업 트리의 신규 `SerializeField` 가 전부 씬에서 null 이라, 이 한 번의 패스가 6항목을 동시에 움직인다. 에디터가 열려 있으므로 `.unity` 를 텍스트로 고치지 말고 `Unity_RunCommand` → `SaveScene` 으로 처리한다(워크트리 가드가 텍스트 편집을 막는다).

| 배선 대상 | 여는 항목 |
|---|---|
| `RunSessionBehaviour._overharvestProfile` ← `Data/Profiles/OverharvestProfile.asset` (현재 씬 내 이 필드명 1회는 AudioDirector 것이다) | UP-POWER-07 VISIBLE→CONNECTED |
| `GameHudView._auxGroup/_auxRatioText/_auxStateText/_auxRiskLamps` — Canvas(`m_RenderMode: 0`) 하위에 새 오브젝트 생성 후 배선 | UP-SPACE-09 |
| `GameHudView._summaryTemplate` ← `RunSummaryTemplate.asset` (현재 씬 참조 0건인 유일한 프로파일) | UP-REC-02, UP-TECH-09 |
| `PassengerReactionView._audio` ← `AudioDirector` (같은 오브젝트 998528525) | UP-NPC-04 ④, UP-AUD-04 |
| `AudioDirector._dangerProfile`·`_accessibilityProfile` — **씬 YAML 에 키 자체가 없다**(런타임 null → 코드 프리셋) | UP-RISK-05, UP-RISK-08, UP-AUD-05 |
| `PresentationProfile.asset` 신규 생성 + `AmbientParticleDirector` 에 배선 | UP-TECH-09 ⑩⑪⑫ |

### ★ W2 — 신규 5파일에 소비처 붙이기 (**코드만**)

지금 상태로 커밋하면 「완성돼 있으나 아무도 안 쓴다」가 5건 늘어난다.

| 파일 | 붙일 곳 |
|---|---|
| `Audio/PassengerVoiceKind.cs` | `AudioCueTable.PassengerVoice(int, PassengerVoiceKind, float)` 구현 → `AudioDirector.PlayPassengerVoice`(:584, 호출자 0) → `PassengerReactionView.OnReacted` 에서 `reaction.VoiceCue` 를 실제로 읽어 호출. `_externalVoiceDriver` 가 서면 `TryPlayEventVoice` 폴백이 물러난다(:580-588 이미 설계됨) |
| `Audio/DangerBed.cs` | `RiskStateView.cs:403,407`(현재 `_blended.HumVolume` 절대값 덮어쓰기)과 `AudioDirector._humVolumeScale`(:407-408, 노출만 되고 소비처 0) 사이를 잇는다 |
| `Run/RunSummaryBuilder.cs` | `GameHudView.UpdateResult`(199-235) / `RunSession` 종료 경로 |
| `Data/Profiles/PresentationProfile.cs` | `Effects/AmbientParticleDirector.cs:30-40` 의 하드코딩 switch(24/48/80/120) 교체 |
| `Data/Profiles/AssetImportRules.cs` | `Assets/Editor/AscendImportRules.cs` 소비처 이미 있음 → **대상 텍스처·오디오가 0개**라는 사실을 UP-PLAT-05·UP-AUD-05 「남은 문제」에 명시(「미착수」와 「대상 없음」은 다르다) |
| `Diagnostics/Tests/WiringDiagnosticsTests.cs` | `Assets/Editor/PrototypeSelfTest.cs` 의 `FoldInSuite` 목록(:68-85)에 등록 — 안 하면 안 돈다 |

### ★ W3 — 공허한 단정 교체 (**코드만 · 저비용 · 게이트 신뢰도 회복**)

| 파일:줄 | 지금 | 바꿀 것 |
|---|---|---|
| `Run/Tests/TenFloorAutoPilot.cs:1021-1023` | `CurrentBudget > 0` (switch `default: 24` 라 항상 참) | 프로파일 값에서 유도된 수인지 대조 |
| `TenFloorAutoPilot.cs:374` | `AudioKindFloor = 14` (실측 16 → `PassengerVoice` 사라져도 PASS) | 16 |
| `TenFloorAutoPilot.cs:1121-1130` | 「통과를 요구하지 않는다」 보고 줄 | `Check(p95 <= 16.67ms)` — 하드 플로어 |
| `TenFloorAutoPilot.cs:1042` | 필수 참조 `> 0` | 하한 수치(현재 17필드 → `≥ 15`) |
| `AudioDirector.cs:671` | `PlayedKindsMask` 가 variant 를 버림 | `MetalStress` variant(RiskLevel 0~3)별 재생 횟수 계수 → UP-RISK-05 「단계마다 다르게 울렸는가」 |
| `AudioDirector.cs:421-428` | `VolumeFor` 만 사용 | `DuckedVolumeFor` — 덕 배율 5필드가 현재 죽음 |
| 「과수확 정적 구간 0.50초」 | 씬 인스펙터 값 0.5 와 프로파일 중앙값 0.5 가 같아 반증 불가 | `.asset` 값을 코드 기본값과 **다르게** 만들고 차이를 단정 |

### W4 — 백로그·문서 정정 (**문서만**)

§1 표 + §4 의 staleClaim 을 반영. **주의**: UP-DOC-01 을 `BLOCKED_EXTERNAL` 로 적으면 `verify-topdown.ps1:649-659`(C11)이 「남은 문제」에 `외부 차단:` 문자열을 요구해 게이트가 새로 실패한다. VERIFIED 로 가면 해당 없음.

### W5 — 개발 빌드 측정 경로 (**코드만 · UP-TECH-03/04/05 공통 전제**)

`Assets/Editor/WindowsBuildTask.cs:85` 의 `options = BuildOptions.None` → `Development` 옵션 경로 추가. 이것 없이는 ① `DEVELOPMENT_BUILD` 미정의로 `SceneWiringValidator` 런타임 경로가 빌드에 없고 ② 플레이어에 `GC Allocated In Frame` 카운터가 없다. 현 `Builds/Windows/Upandup_DDD.exe`(8/1 03:19)는 모든 프로브 소스보다 낡았다.

### W6 — 레거시 웨이브 1~2 (**코드만 · 이제 전제 충족**)

`[RequiredReference]` 가 Player 3파일에 들어갔으므로 `Scripts/Player/PlayerSetupValidator.cs`(호출자 0, GUID 씬·에셋 0건) 삭제가 가능하다. A묶음 나머지(`Player/InteractablePassenger.cs`)도 참조 0. **`Perf/ComponentPool.cs` 는 삭제 대상에서 제외**(UP-TECH-06 이 소비처를 만드는 중). 웨이브 3·6·7·8 은 §3.

---

## 3. 이번 세션에 끝낼 수 없는 것

| 항목 | 이유 |
|---|---|
| UP-VIS-01 / 04 / 07 / 09 | 독립 시각 평가 주기가 필요하고, `B-5 #15`(3×3 판 + 전력 계기 동시 판독)는 **4라운드 연속 실패**다. CLAUDE.md 규칙 2 상 다섯 번째 카메라 조정은 금지 — 필요한 것은 **배치 결정 PD-17**(6차 평가자 제안: `x ≈ -1.0` 문틀 기둥 축소). 셰이더 재채택은 2회 되돌림 이력 + 6차 「순손실」 판정 |
| UP-VIS-09 | 선행 `UP-FIX-18`(위험 단계가 색만 움직이고 명도가 그대로 → 480×270 에서 Strain↔Critical 동일) 미해결. 재캡처 23장 + `scaled25/` 재생성 + 세트 전체 판정이 필요 |
| UP-POWER-06 5단계 | 1단계(조준 dwell 0.15s)가 **어떤 런에서도 발화한 적이 없다.** `TenFloorCaptureRig.AimPromptScreenShot` 을 복제해 `InteractableOverharvestLever` 를 겨누는 새 캡처 런이 필요(수 분) |
| UP-REC-05 | 캡처 17 이 코드 수정 2건(`FloorRecord.cs:30` 「[과적]」, b61a187 위험도 모순)보다 낡음 → 재촬영 + `VISUAL_VERDICT.md` 독립 ACCEPT |
| UP-TECH-04 | `render_budget.txt`(p95 52.43 ms, 「예산 초과」)와 `loaded_critical_perf.txt`(p95 8.4 ms)가 **6배 어긋난다.** 같은 창에서 재측정하기 전에는 어느 쪽도 근거가 아니다. + `TargetHardwareProfile.asset:26 _ratified: 0` → **PD-16 사용자 비준** |
| UP-TECH-05 | `heroslice_perf.txt` 가 1469×720 이고 무효 선언된 프로브(F-1·F-2 수정 전) 산출물. 1920×1080 재측정 필요. `LoadedCriticalPerfProbe` 는 0 B 프레임을 세지 않아 「매 프레임 0 B」를 영원히 판정 못 함 |
| UP-TEST-11 웨이브 3·6·7·8 | **PD-13 사용자 승인 전제**(①전부삭제/②`_Legacy/`이동/③코드만삭제). 웨이브 3 은 씬 재저장이라 유일한 비가역 지점 |
| UP-TECH-09 ①~⑦ | `SpinRuleSet`·`FloorPlan._tenFloors`·`RiskEvaluator` 임계 4값을 ScriptableObject 로 빼는 대공사. 12항목 중 주입 완료는 현재 2개(⑧⑨) |

---

## 4. 감사가 찾은 새 결함 (백로그에 없음)

**즉시 위험**
1. **신규 5파일 소비처 0** — `DangerBed`·`PassengerVoiceKind`·`PresentationProfile`·`RunSummaryBuilder`(+`AssetImportRules` 는 Editor 만). 이대로 커밋하면 죽은 구현 5건 추가.
2. **`WiringDiagnosticsTests.cs` 미등록** — `PrototypeSelfTest.FoldInSuite`(:68-85)에 없어 실행되지 않는다.
3. **`AudioDirector._dangerProfile`·`_accessibilityProfile` 이 씬 YAML 에 아예 없다** — 런타임 null. 「PASS 접근성 옵션이 AccessibilityProfile 을 읽는다」는 `RiskStateView` 쪽만 보므로 이를 못 잡는다.
4. **`AudioMixProfile` 18필드 중 13개가 죽어 있다** — 덕 5 + 험 배율 8. 험 배율은 계산·노출되지만 실제 험은 `RiskStateView.cs:403` 이 절대값으로 덮어쓴다. `DEAD_IMPLEMENTATION_AUDIT.md` §1 의 「13개」는 **낡지 않았다** — 정정하지 말 것.
5. **`VisualQualityProfile` 이 URP 에셋과 어긋난다** — 프로파일 High `_shadowDistance: 30` vs `PC_RPAsset.asset:57`·`Mobile_RPAsset.asset:57` `m_ShadowDistance: 50`. 성능 리포트가 거짓 조건을 인용한다. 7필드 중 6필드 소비처 0 — 예산이 아니라 **라벨**이다.

**증거 신뢰도**
6. `PlayedKindsMask` 가 variant 를 버려(:671) 「응력음 1회」와 「네 단계 각각」이 구분되지 않는다.
7. GC 인용 수치(10,443 / 8,805 / 1,638 B)의 **원본 로그가 덮어써졌다** — 현재 `loaded_critical_perf.txt` 는 10807/9173/9176/10803.
8. 6라운드 시각 채점이 전부 **절대 5점 척도**인데 `docs/VISUAL_CRITERIA.md:6-7` 이 「10점 만점 절대 점수는 쓰지 않는다」고 금지 — 통과 조건의 측정 방법 자체가 절차 위반.
9. `VISUAL_VERDICT.md` 의 4·5·6차가 `## VERDICT: REJECT` 헤딩 형태라 **`verify-topdown.ps1:587` 정규식이 못 읽는다.** 지금은 결과가 우연히 같지만 ACCEPT 가 헤딩으로 적히면 C10 이 통과시키지 않는다.
10. `TenFloorCaptureRig.cs:718-720` 하드코딩 문구 「이 한 장만 방식이 다르다 / 나머지 18장」 — 실제 23장·화면 캡처 4장. `DEAD_IMPLEMENTATION_AUDIT.md:105` 에도 같은 낡은 서술.

**출처 표기 (동결 PRD 는 §15 에서 끝나고 §16·17 이 없다)**
11. 다음 출처가 동결 `docs/MASTER_PRD.md` 에서 해소되지 않는다 — **`§14.1`(UP-TECH-09, 실제는 Notion N08, 사본 `NotionSyncReport.md:508`), `§17.4`(UP-PLAT-05·TECH-04·05·06), `§9.3`(UP-NPC-04·AUD-04), `§10.3`(UP-REC-05, 실제 §11:214), `§7.3`(UP-POWER-06, 실제 `DEVICE_DESIGN_SPEC.md §5.4` 「Phase 4 대상」), `§13.3/§13.4`(UP-PLAT-05 — PRD 전체에 「압축」·「임포트」 0건), `§15.2`(UP-VIS-07·09 — 「4.0」 전 문서 0건)**.

**감사자 인용 오류 정정 (다음 세션이 잘못된 줄을 열지 않도록)**
12. `verify-topdown.ps1` — VERDICT 정규식 **:587**(≠443), `fail=` 파싱 **:423**(≠320), C11 **:649-659**.
13. `PrototypeSelfTest.cs` 조기 반환 **:39-44**(≠38-43). 단 작업 트리에서 이미 `if (legacyAssetsPresent)` 분기로 교체됨.
14. 백로그 자체 모순: `:1195`(UP-VIS-01)이 「UP-VIS-04 는 NOT_STARTED」라 적지만 `:1219` 는 SKELETON. UP-VIS-01 은 심볼에 「머티리얼조차 배정되지 않는다」, UP-VIS-04 는 「전부 `M_Gray_Readout` 공유」로 정반대 서술 — 후자가 맞다(`.unity:5191`).
15. `MAT_Ascend_*`·`MAT_Sym_*` 6종과 `AscendStylized.shader` 는 씬·코드·프리팹 참조 **0건**. 머티리얼 6개가 `_AmbientFloor: 0.18` 을 직렬화로 덮어써 백로그의 「0.35 로 올렸다」가 채택 대상에 적용돼 있지 않다.
