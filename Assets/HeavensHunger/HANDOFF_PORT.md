# 해븐즈 헝거 — 상승 → 유니티 이식 핸드오프 (2026-08-25)

상승 HTML 프로토타입(coreB / RULESET 3.0)을 이 유니티 프로젝트로 옮긴 1차 이식본.
**규칙과 숫자는 하나도 바꾸지 않았다.** 3D로 표현하기 어려운 것(상점·확률표·판정표)만 화면 UI로 뺐다.

---

## 0. 실행

1. `Assets/HeavensHunger/Scenes/HeavensHunger.unity` 열고 재생
2. 조작
   - `Space` 레버 · `Enter` 출발
   - `Y` / `N` 문 앞의 사람·인터폰에 답하기
   - `Tab` 상점 · `F` 명부/설비 · `Q` 확률표 · `E` 줄표(판정 규칙) · `R` 새 판
3. 씬을 잃어버리면 메뉴 **HeavensHunger ▸ 씬 다시 짓기** 로 같은 씬이 다시 만들어진다(코드 생성).

---

## 1. 이식 검증 — 원본 엔진과 수치 완전 일치

원본 `__v2.simLever` 와 C# `HHSim.SimLever` 를 **같은 스펙·같은 시드·20,000 트라이얼**로 돌려 비교했다.

| 조건 | 원본 JS | C# 이식본 |
|---|---|---|
| 시작 풀 · 눈 0 | ev 7.4 / sd 15.4 / max 238 / 잭팟 2.3% | ev 7.4 / sd 15.4 / max 238 / 잭팟 2.3% |
| 시작 풀 · 눈 3 | ev 3.2 / sd 9.2 / max 473 / 잭팟 0.9% | ev 3.2 / sd 9.2 / max 473 / 잭팟 0.9% |
| 시작 풀 · 눈 5 | ev 1.6 / sd 5.0 / max 58 | ev 1.6 / sd 5.0 / max 58 |
| 모노 뼈13 · 눈 0 | ev 2044.8 / sd 0 / 잭팟 100% | ev 2044.8 / sd 0 / 잭팟 100% |
| 순도 60% | ev 134.8 / p30 25 / med 72 / 잭팟 40.6% | ev 134.8 / p30 25 / med 72 / 잭팟 40.6% |

> ⚠ **함정 하나 기록해 둔다.** 처음엔 C# 쪽이 계속 ~8% 낮게 나왔다. 원인은 판정 로직이 아니라 **원본 측정 LCG의 double 정밀도 손실**이었다.
> JS `seed*1103515245` 는 최대 ~2^61 이라 2^53 을 넘겨 정밀도가 깨진다. C# 에서 `long` 으로 정확히 계산하면 **다른 난수열**이 나온다.
> `HHSim.LcgRng` 은 일부러 double 산술 + ToUint32 로 JS 의미론을 흉내 낸다. 게임 본체는 mulberry32(`HHRng`)라 영향 없음.
>
> 재현: `HHSim.Report()` 를 실행하고, 원본은 `python3 -m http.server` 로 `sangseung_proto.html` 서빙 → **게임 시작 후**(`LINES.length===20` 확인) `__v2.simLever(...)`.
> 타이틀 화면에서 재면 8줄 유물이라 전부 무효다.

---

## 2. 옮긴 것

### 코어 (`Scripts/Core/`)
| 파일 | 내용 |
|---|---|
| `HHData.cs` | 문양 7종(어금니1/뼈2/귀3/혀4/심장6/뇌9/폐13) · 장치 7종 · 아이템 14종 · DIAL 전량 · 목표 사다리 `[12,18,26,55,100,120,150,160,175,200]` · 11정차+ `200×(층/186)^0.52` · 완주 7734 |
| `HHRng.cs` | xmur3 + mulberry32 (원본과 같은 결정론) |
| `HHResolver.cs` | 페이라인 9종 · 대각 6방향 · 직선 3연속 판정(4연속 ×2·5연속 ×4) · 꺾인 줄 5칸 완성 잭팟 ×4 · 하위 조각 흡수 · 동시 N줄 ×(1+0.2(N−1)) · 뱃지 35% 부착/레버당 1회(변압기만 줄마다) · 운 앉히기 · 눈 갈림 · 소각/감김 |
| `HHRun.cs` | 레버 탱크 5 · 당첨 레버 +1닢 · 출발 시 남긴 레버 누진(1·2·3·5·8) · 점프 = 상한(5+층×0.15) × 겹침(최대 4) · 종 게이지(벨 뱃지 8% 단일 경로) · 강설 0.08+0.02×(정차−4) · 상점/합성/제거/리롤 · 아이템 3계층(무한 스택 ×1.4^n · 공급 ×0.55^보유 · 유일 · 액티브 2칸 충전) |
| `HHSim.cs` | 원본 simLever 등가 MC 하네스 |

### 뷰 (`Scripts/View/`)
- `HHSlotView.cs` — 블렌더 앵커 `HH_Cell_00..14` 에 문양 타일 + 값 라벨. 당첨 칸은 금색 테두리 + 3D 페이라인.
- `HHHud.cs` — 화면 UI 전부.
- `HHGame.cs` — 입력/진행. **Input System 패키지 전용**(레거시 `Input` 쓰면 매 프레임 예외).

### 아트
- `Art/ELV_Cabin_5x3.fbx` — 블렌더 `SM_ElevCab_Panel_AD57.blend` 에서 반출. 81 오브젝트 / 약 97.5k 삼각형.
  - 5×3 슬롯 본체 = `TEST_H_ChamberArray` + `TEST_H_Cores` + `TEST_H_Glass`(유리 아일랜드 정확히 15개로 확인) + `TEST_H_PanelRecess`
  - 셀 격자: 피치 0.476 정사각, 유리면 Blender y=2.8497
  - 반출 시 `HH_Cell_00..14` · `HH_LeverPivot` · `HH_CamAnchor` 엠프티를 블렌더에 새로 만들어 같이 내보냈다(컬렉션 `HH_SlotAnchors`). **블렌더 파일은 저장하지 않았다** — 앵커를 남기려면 직접 저장할 것.
- `Art/Fonts/HH_KR SDF.asset` — 한글 TMP 폰트(AppleGothic 기반, Dynamic 아틀라스). 프로젝트에 한글 폰트가 없었다.

> ⚠ **블렌더 앵커에는 축변환 회전(−90° X)이 박혀 있다.** 그냥 자식으로 붙이면 쿼드가 바닥에 눕는다.
> `HHSlotView` 는 앵커 밑에 월드 무회전 `HH_CellFrame_XX` 를 하나 끼워 넣어 해결한다. z 순서는 테두리 0.108 < 타일 0.115 < 글자 0.14.

---

## 3. 화면 배치 (설계자 지시: "전력량·현재 층은 크게")

```
┌ 현재 층 (104pt) ─┬──── 전력량 (116pt) + 게이지 ────┬─ 레버/동전/눈/종/풀 ─┐
│  29층            │  14W   목표 55W · 정차 4        │  레버 4/5            │
│  출발 → +9층      │        · 문턱까지 41W           │  동전 2닢 …          │
└──────────────────┴─────────────────────────────────┴──────────────────────┘
                        [ 5 × 3  3D 슬롯 ]
   로그
                   판독기: 줄 2개 · 동시 ×1.2 · 기초 12W × 배율 1.00 = 14W
        [레버 Space] [출발 Enter] [상점 Tab] [확률표 Q] [줄표 E]
```

**UI로 뺀 것** (3D 공간에 안 넣음)
- **상점** — 릴 풀/장치/아이템 보유 현황 + 진열 구매 버튼 + 리롤
- **확률표** — 칸당 실제 추첨 확률(아이템 가중 반영). 막대 + %.
- **줄표** — 판정 규칙 전문(직선 3연속·완성형 잭팟·중복 정책·뱃지·눈)

---

## 4. 2차 이식 (2026-08-25 오후) — 승객·설비·거래·연쇄 + 2D 문양 + 크레센도

### 데이터 (손으로 안 적었다)
`Data/hh_roster.json` (승객 23) · `hh_parts.json` (설비 151) · `hh_deals.json` (거래 24)
— 원본 빌드를 띄워 `ROSTER`/`PARTS`/`DEALS` 를 그대로 JSON 으로 덤프한 것이 정본이다.
151개를 손으로 옮기면 오타가 반드시 난다 — 바꿀 때는 원본에서 다시 뜨는 편이 맞다.

- `HHContent.cs` — JSON 적재 (Newtonsoft, 런타임 사용 가능 확인됨)
- `HHModsCalc.cs` — 원본 `recomputeMods` 등가. 보유 설비 + 탑승 승객 + 융합분을 하나의 `ModBag` 으로 접는다. famPer(계열 N개당) 상한 6 반영.
- `HHDeals.cs` — 거래 24종의 실제 효과를 한 건씩 옮겼다. 이월분(`DealCarry`)은 다음 정차에 소진된다.

### 붙은 파이프
| 계통 | 어디에 먹는가 |
|---|---|
| `wcMul` · `reqMul` · 무게 | `HHRun.EffReq` — 문턱 = 목표 × (1+0.05×무게세×무게) × 거래이월 × 부품문턱 |
| `lv.h/v/d` | 줄값 보정 (가로/세로/대각) |
| `sv[]` · `svLo/svHi` · `svPerW` · `svHiX` | 칸별 심볼값 (장기=심장·뇌·폐에만 배수) |
| `luck` · `luckLow` | 운 — 가로 2연속의 세 번째 칸에 같은 문양을 앜힌다 |
| `eyeW` | 강설 확률 |
| `outAdd` · `outPerW` · `outMulX` | 전력 배율 |
| `spinCapD` | 레버 수 |
| `jumpCap` · `jumpCapMul` | 상승 한계 |
| `badgeAdd` · `gaugeNeedD` | 벨 뱃지 확률 · 종 문턱 |
| `brand` | 냙인 — 정차당 N회, 터진 살 중 가장 값진 문양에 영구 +1W |
| `purgeOnArrive` · `plantOnArrive` · `arriveTick` | 도착 처리 |
| `onDeliverTick` · `onBlankTick` · `onJackTick` | 경제 |
| `confirmMul` | 출발 복리 — used/deliv/weight/eyes/lines 를 각자 다른 재료로 쓴다 |
| `ch:{on,fx,v}` | 연쇄 — line/blank/eyeBorn/eyeGone/bell/deliv/fuse/full 에 반응 |

### 승객/거래 제안
- 승객: 3층부터 · 명부 4명 상한 · 기본 18% · 쿨다운 2정차 · 정차 3·9 보장 · 가뮄(8정차+) ×2.2 · 2기는 9층부터
- 인터폰: 매 정차 40% · 종 등급에 따라 맑은종(well)/종루(grand)/붉은종(red) 로 갈린다
- 융합: 승객이 기다리는 계열의 설비를 사면 그 자리에서 터진다 (설비 진열이 60% 확률로 그 계열을 밀어준다)
- 설비 진열: 4칸 · 등급 가중 [.32,.38,.22,.08] · 보유 계열 60% · 산탄총은 4층 이후 첫 상점에 보장 · 소지 8개 상한 · 가격 = `cost` 닯(원본 그대로)

### 2D 문양 (설계자 지시: 3D 메시 말고 2D)
`HHSymbolArt.cs` — 외부 이미지 0개. SDF 조합으로 14종 실루에을 굽고 스프라이트로 만든다.
구격이 통일돼 있어서(정사각·알파 실루에·흰색) 나중에 그림으로 갈아끼기 쉽다 — `HHSymbolArt.Get(k)` 만 교체하면 된다.
당첨 칸은 금색 **테두리**(`Ring()`)로 표시한다 — 꽉 찬 사각형은 문양을 가려서 버렸다.

### 크레센도 + 사운드
- `HHSlotView.RevealCrescendo` — 작은 줄 → 큰 줄 → 완성형 순서. 완성형 직전 0.38초 침묵, 터질 때 화면 흔들림 + 배너.
- `HHSlotView.SpinAnim` — 열마다 순서대로 멈추고 그때마다 정지음이 난다.
- `HHAudio.cs` — 음원 에셋 0개. 절차적으로 만든다: 레버 클렁 · 릴 정지(열마다 피치 상승) · 줄 팝(순서마다 반음씩 올라간다) · 잔팟 아르페지오 · 종 · 꽝 · 동전 · 출발.

---

## 4-b. 아직 안 올긴 것

1. **눈 배수 빌드** — 설계자가 미뤄둔 건. 파이프는 깔아둔 상태다:
   `HHDial.EyeMultFromMods = false` 한 줄을 `true` 로 바꾸면 부품/승객의 `eyeMult` 가 즉시 먹는다.
   그 순간 `GrindGateAt 1.5` 분기(눈 배수 1.5+ 빌드는 줄이 눈을 갈지 않는다)가 살아난다.
2. **산탄총(`sgun`) 발동형** — 설비는 살 수 있고 진열 보장도 살아 있지만, 발동(명부의 이름 지우기)은 미배선.
3. **하강 2막** — 원본에서도 보류.
4. **경제 정밀 패스** — 원본 §3-④. 설비 가격은 원본 `cost` 그대로 썼지만, coreB 경제(당첨 레버 +1닯)와의 통합 검증은 안 했다.

## 5. 발열 설정

`HeavensHunger ▸ Thermal` 메뉴로 켜고 끈다. 현재 적용값(저발열 30fps):

| 항목 | 이전 | 지금 |
|---|---|---|
| 에디터 Interaction Mode | 모니터 주사율(16ms) | **Custom 33ms (≈30fps)** |
| URP MSAA | 4× | **2×** |
| 그림자 거리 / 캐스케이드 | 50m / 4단 | **18m / 1단** (실내 캐빈엔 캐스케이드가 무의미) |
| 그림자 해상도 | 2048 | **1024** |
| Opaque Texture | 켬 | **끔** (쓰는 셰이더 0개인데 매 프레임 전체화면 복사 중이었음) |
| Blender 뷰포트 AA | 8× MSAA | **FXAA** (2503×1667 레티나) |
| Blender 자동저장 | 2분 | **10분** (23MB 파일) |
| Blender 언두 메모리 | 무제한 | 512MB / 16스텝 |

더 낮추려면 `HeavensHunger ▸ Thermal ▸ 저발열 강 (20fps)`, 되돌리려면 `원복 (모니터 주사율)`.
**작업 중 가장 큰 발열원은 블렌더와 유니티를 동시에 띄워두는 것이다** — 모델 재반출이 끝났으면 블렌더는 닫는 편이 낫다.

---

## 6. ⚠ 이전 자산을 버렸던 건 — 되돌렸다 (2026-08-25 저녁)

설계자 지적: *"이전에 만들었던걸 아예 다 버렸구나? 텍스처나 무드나 조명이나 레버 내려가는 애니메이션이나 하단에 전력 몇 참는지 숫자나 … 다 버렸네?"*
맞다. 첫 판은 **빈 씬에서 새로 지었고**, 그래서 90%를 버렸다. 지금은 **이전 씬을 통째로 물려받고 3×3 패널만 5×3 으로 갈아끼운다.**

`HeavensHunger ▸ 씬 다시 짓기 (이전 씬 + 5x3 패널)` 가 하는 일:
1. `Prototype_Elevator.unity` 를 열어 그대로 복제
2. `CabinAD47` 의 옛 3×3 메시만 비활성(38개) → 내 5×3 FBX 를 그 자리에 자식으로 넣는다
   - 정렬 근거(실측): `CabinAD47` 가 rot Y=180 이므로 localRotation=identity 로 넣으면 월드에서 정확히 겹친다
     (SM_Cab_Wall_Back z: FBX −2.661 → 회전 후 +2.661 = 이전 씬과 일치)
3. 옛 **3×3 코어를 돌리던 드라이버만** 비활성(38개) — 소품·조명·포스트는 건들지 않는다

### 살린 것 (이전 씬 그대로)
| 분류 | 내용 |
|---|---|
| 텍스처·머티리얼 | `TEX_Iron_Rust` · `TEX_FloorPlate_Rust` · M_Cab_* / M_Elev_* 24종 (FBX 에 명시 리맵) |
| 무드 | PostVolume(AscendPostProfile: 블룸·비네트·필름그레인·노출+0.7·채도−12) · 안개 ExpSq 0.018 · 반사 0 · SMAA |
| 조명 | LT_CabBulb · LT_SoulSpill · ChamberFillLight · ShaftLamp · ShaftLampNear · ExecutionLeverKeyLight · GateLamp |
| 소품 | ReferenceRoom 전체 — 레버 베이스(하우징·피벗허브·구동로드·기어박스·잠금핀) · 경고등 · 상승칼럼 · 전력계 · 창고선반·공구·안전표지 · 가위문 · 샤프트 |
| 시스템 | AudioDirector · AmbientParticleDirector · AD47_DoorRig · PassengerStations · ShellCollision |
| 플레이 | 1인칭 Player(FirstPersonController · CrosshairInteractor) + PlayerHUD 크로스헤어 |

### 다시 물린 물리 계기 (`HHCabinRig.cs`)
새로 만들지 않고 **패널의 진짜 오브젝트를 이름으로 찾아 값만 흘린다.**
- `SM_Lever_Handle.003` / `.001` — **레버가 실제로 내려갔다 올라온다** (−42°, 내려갈 땐 빠르게 · 돌아올 땐 느리게)
- `SM_SpinGauge_Cell_0..4` — **남은 레버 5칸 파일러뻗** (남은 개수만큼 주황으로 발광)
- 계기 유리 안 — **물리 전력 막대 + `37 / 130 W` 3D 숫자** (화면 UI 말고 기계에서도 읽힌다)
- `SM_Siren_Bulb` — 잭팟·종에 맥동
- `SM_Harness_Fill` — 문턱 넘기면 발광
- `ChamberFillLight` — 전력이 챔3다 밝아진다
- `SM_Door_L/R` — 출발하면 문이 열렸다 닫힌다

### 방향 산출 (또 틀리지 않게)
씬마다 패널이 어느 쪽을 보는지 다르므로 하드코딩하지 않는다.
`HHSlotView.MachineOutward()` 가 **유리(TEST_H_Glass) − 챔버배열(TEST_H_ChamberArray)** 로 바깥방향을 재고,
칸 프레임·페이라인·플로터·계기 글자가 전부 그걸 기준으로 농인다.
**실측 규칙: TMP 3D 는 text.forward 가 카메라 시선과 같을 때 읽힌다.** (반대로 두면 거울상이 된다)

### 아직 안 물린 이전 자산
- `AudioDirector` 는 살아 있지만 **옛 RunSession 이벤트를 듣도록 짜여 있어 coreB 사건을 받지 못한다.**
  지금 소리는 `HHAudio`(절차 생성)가 낸다. 다음 패스에서 AudioDirector 에 coreB 브리지를 붙일 것.
- `InstrumentPanelView` · `AscentColumnView` · `PowerGaugeView` · `CellGlowView` 는 옛 3×3 코어 전용이라 꺼둔 상태.
  그 중 **상승칼럼(AscentColumn)과 전력계(PowerMeter)는 물체는 보이고 있다** — 숫자만 안 도는 상태.
- `PassengerStations`(Slot_0..3)는 살아 있지만 **승객 모델을 세우는 배선은 안 했다** — 명부는 UI 패널(F)로만 보인다.
- `TubesRoot` · `GrayboxWorld` · `GB_AscendControls` 는 AD47 캐빈 이전의 그레이박스라 끔 상태(화면을 가리는 유물 글자 포함).
