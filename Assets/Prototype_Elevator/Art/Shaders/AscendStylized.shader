// 공통 스타일 셰이더 (`UP-VIS-04`).
//
// `VISUAL_BIBLE.md` §2.1 이 요구하는 것 중 셰이더가 책임지는 셋을 구현한다 —
//   · 「단순한 Gouraud 또는 플랫 셰이딩」  → 램버트를 계단으로 양자화한다
//   · 「제한적인 국소 조명과 빠른 감쇠」    → 감쇠를 거듭제곱으로 가파르게 만든다
//   · 「차가운 회녹색 그림자」              → 그림자 쪽을 회녹색으로 물들인다
//
// 왜 URP Lit 을 안 쓰는가: Lit 은 물리 기반이라 부드러운 하이라이트와
// 완만한 감쇠를 만든다. 그건 PS1~초기 PS2 감각의 반대다. 5차 시각 평가가
// 스타일 2.23/5 를 준 이유의 하나가 「전부 같은 무지 머티리얼」이었다.
//
// 왜 ShaderGraph 가 아닌가: `.shadergraph` 는 바이너리에 가까운 대형 YAML 이라
// 리뷰가 안 되고, 이 저장소는 직렬화 에셋이 조용히 깨진 이력이 있다.
// 텍스트 셰이더는 diff 가 읽힌다.
//
// ─────────────────────────────────────────────────────────────────────────────
// **기본값 불변 규약** — 이 파일에 무엇을 더하든 지켜야 하는 단 하나의 규칙.
//
// 이 저장소는 셰이더·머티리얼 일괄 교체를 **두 번 되돌렸다**(6차 판정 「순손실」).
// 두 번 다 「바꾼 뒤에 비교」했기 때문이다. 그래서 이 파일의 모든 신규 기능은
// **꺼진 상태가 기본**이고, 기본 상태에서 프래그먼트 계산은 종전과 수식적으로
// 동일하다. 씬 소유자가 머티리얼 하나씩 켜며 매번 판정한다.
//
// 그 규약을 지키는 방법은 둘뿐이다.
//   ① 곱셈 중립값 (`_BaseMap` = "white")
//   ② `shader_feature_local` 로 **코드 자체를 컴파일하지 않는 것**
// 신규 기능은 전부 ②를 쓴다. ①은 「기본값이 중립이니 같다」는 논증이 필요하지만
// ②는 **그 코드가 변형에 존재하지 않는다**는 것이 논증이라 반박의 여지가 없다.
//
// ⚠ `multi_compile` 을 쓰지 않는다. 이 파스에는 이미 조명·그림자·안개 조합으로
// 64 변형이 있고, 신규 기능을 `multi_compile` 로 걸면 무조건 곱해진다.
// `shader_feature_local` 은 **머티리얼이 실제로 켠 조합만** 컴파일된다.
// ─────────────────────────────────────────────────────────────────────────────
Shader "Ascend/Stylized"
{
    Properties
    {
        _BaseColor      ("기본 색", Color) = (0.55, 0.52, 0.46, 1)
        _ShadowTint     ("그림자 색조 (회녹색)", Color) = (0.20, 0.26, 0.24, 1)
        _Steps          ("명암 계단 수", Range(2, 8)) = 3
        _FalloffPower   ("감쇠 가파름", Range(1, 6)) = 2.5

        // ⚠ **이 프로퍼티는 프래그먼트에서 한 번도 읽히지 않는다.** 죽은 값이다.
        // 씬 머티리얼 13장이 0.35, 고아 `.mat` 6장이 0.18 로 서로 다른 값을 들고
        // 있지만 어느 쪽도 화면에 영향이 없다. 첫 판본의
        // `lerp(shadowed, lit, saturate(lambert + _AmbientFloor))` 에서 쓰이던
        // 것이고, 그 누적 방식을 되돌릴 때 사용처만 사라지고 선언이 남았다.
        // **지우지 않는 이유**: 지우면 머티리얼 13장의 직렬화 프로퍼티가
        // 「알 수 없는 프로퍼티」가 되어 재직렬화 때 조용히 삭제된다. 씬 YAML 을
        // 건드리는 변경이 되므로 셰이더 소유자가 단독으로 할 일이 아니다.
        // 밝기를 조정하려면 `_ShadowLift` 를 쓴다.
        _AmbientFloor   ("주변광 바닥 (⚠ 미사용 — 죽은 값)", Range(0, 0.6)) = 0.35

        _ShadowLift     ("그림자에 남는 기본색 비율", Range(0, 1)) = 0.55

        // 계단 0 번 칸의 바닥. **0 = 종전과 수식적으로 동일**(기본값 불변 규약).
        // 실내 점광에서 첫 칸이 0 이 되어 직접광이 통째로 사라지는 것을 막는다.
        // 자세한 근거는 아래 `Quantize` 주석에 있다.
        _BandFloor      ("계단 0번 칸 바닥 (0 = 기존과 동일)", Range(0, 1)) = 0

        // ── 계단 경계 부드럽게 (2026-08-08 사용자 지적) ────────────────────────
        //
        // 「그림자가 계단식으로 나뉘는 게 좀 어색하다.」
        //
        // 계단 자체는 스타일 락이다 — 없애면 이 게임의 그림이 아니다. 어색한 것은
        // **경계선**이다. `floor` 는 한 화소 안에서 값이 통째로 튀므로 경사면에서
        // 톱니가 생기고, 그 톱니가 「의도된 계단」이 아니라 「깨진 렌더링」으로 읽힌다.
        //
        // 그래서 칸 수도 칸 높이도 건드리지 않고 **경계에서만** 다음 칸으로 넘어가는
        // 구간을 준다. 칸 폭의 몇 %만 쓰므로 평평한 면은 그대로 평평하다.
        //
        // ⚠ 키워드가 꺼진 변형에는 이 코드가 **컴파일되지 않는다.** 기존 머티리얼
        // 98 장의 그림은 비트 단위로 그대로다 — 이 파일의 「기본값 불변 규약」.
        [Toggle(_BANDSOFT_ON)] _BandSoftEnabled ("계단 경계 부드럽게 사용", Float) = 0
        _BandSoftness   ("계단 경계 폭 (칸 폭 대비)", Range(0.02, 1)) = 0.35

        // ── 최소 채우기 — 「기계 주변 테두리가 검정」 (2026-08-08 사용자가 두 번 지적) ──
        //
        // 원인은 셰이더가 아니라 **간접광이 없다는 것**이다. 이 씬에는 베이크 GI 라이트맵
        // 0 장 · ReflectionProbe 0 개 · 활성 조명 2 개뿐이다. 블렌더 EEVEE 는 간접광이
        // 있어 오목한 곳이 「어둡지만 읽힌다」. Unity 는 점광이 안 닿으면 그냥 0 이다.
        // 실측: `ambientTerm ≈ 0.00026` — 화면에서 순수 검정과 구분되지 않는다.
        //
        // 라이트맵을 굽는 것이 정공법이지만 되돌리기 어렵고 오래 걸린다. 그래서 우선
        // **알베도에 비례하는 아주 작은 상수**를 더한다. 알베도에 비례시키는 것이 핵심이다 —
        // 상수를 그냥 더하면 어두운 재질과 밝은 재질이 같은 회색으로 뭉쳐 재질 구분이 죽는다.
        //
        // ⚠ 이것은 간접광의 **대역**이지 간접광이 아니다. 방향도 가림도 없다.
        // 라이트맵이나 프로브가 들어오면 이 항은 꺼야 한다.
        [Toggle(_MINFILL_ON)] _MinFillEnabled ("최소 채우기 사용", Float) = 0
        _MinFill        ("최소 채우기 (검은 면의 바닥)", Range(0, 0.3)) = 0.06

        _RimStrength    ("실루엣 림", Range(0, 1)) = 0.25

        // ── `_NearAttenClamp` — 점광의 「크기」 대역 (2026-08-08) ────────────────
        //
        // URP 점광은 **반지름이 0** 이다. 그래서 광원에 가까운 면은 `distanceAttenuation`
        // 이 1 에 붙고, 그 뒤 어떤 계단 수를 써도 최상단 칸에 고정된다. 캐빈 램프는
        // 천장에서 0.36 m 아래라 바로 위 천장이 정확히 그 상태가 됐다 — 흰색으로 타고
        // 링 밴딩이 보였다. **광원 세기를 낮춰도 사라지지 않는다**: 넘치는 값을 정하는
        // 것은 세기가 아니라 거리 감쇠이기 때문이다.
        //
        // 블렌더 전구는 물리적 크기가 있어서 근거리에서 이렇게 타지 않는다. 여기서는
        // 감쇠의 **상한**을 두어 그 크기를 흉내 낸다. 먼 면(감쇠 « 상한)은 영향이 없고,
        // 가까운 면만 상단 칸에서 내려온다.
        //
        // ⚠ **기본값 1 에서 `min(a, 1) == a` 라 비트 단위로 동일하다** —
        // 이 파일의 「기본값 불변 규약」을 지킨다.
        _NearAttenClamp ("광원 크기 대역 (1 = 기존과 동일)", Range(0.05, 1)) = 1

        // **이 프로퍼티가 없으면 이 셰이더는 어디에도 채택할 수 없다.**
        // `SpinBoardView`(정화 점등) · `InstrumentPanelView`(계기 발광) ·
        // `OverharvestUnlockEffect`(덮개 레일) 셋이 `MaterialPropertyBlock` 으로
        // `_EmissionColor` 를 쓴다. 이름이 없으면 블록이 조용히 무시되고
        // **점등이 사라진 채 아무 오류도 안 난다** — `UP-CORE-12` 가 GIF 로 확인된
        // 바로 그 연출이 그렇게 죽는다.
        [HDR] _EmissionColor ("발광 (MaterialPropertyBlock 이 쓴다)", Color) = (0, 0, 0, 1)

        // **기본값이 흰색인 것이 이 프로퍼티의 안전 장치다.**
        //
        // 이 셰이더에는 텍스처 슬롯이 하나도 없었다. 그래서 `UP-VIS-01` 스타일 락의
        // 첫 항목(「저해상도 손그림 픽셀 텍스처」)이 에셋을 만들어도 화면에 올라갈 데가
        // 없었고, 머티리얼에 배정해도 **조용히 무시된 채 아무 오류도 안 났다.**
        //
        // 흰색을 곱하면 아무것도 달라지지 않는다. 그래서 기존 머티리얼 23장은
        // 이 변경 뒤에도 픽셀 단위로 같은 그림을 낸다 — 되돌릴 일이 생기지 않는다.
        // 이 저장소는 셰이더·머티리얼 일괄 교체를 두 번 되돌렸고(6차 판정 「순손실」),
        // 그 두 번 다 「바꾼 뒤에 비교」했다. 이번엔 **바꿔도 같은 상태**에서 시작해
        // 머티리얼 하나씩 텍스처를 물리며 매번 판정한다.
        _BaseMap ("표면 텍스처 (흰색이면 무지 — 기존과 동일)", 2D) = "white" {}

        // ── 진단 ────────────────────────────────────────────────────────────
        // 진단 전용. **0 = 정상이고 기본값이다.** 켜지 않는 한 그림에 영향이 없다.
        //
        //   0 정상
        //   1 직접광 항만 (`lit` — 주광 + 추가 광원)
        //   2 앰비언트 항만 (`ambientTerm`)
        //   3 림 항만
        //   4 추가 광원 항만
        //   5 주광 항만
        //   6 추가 광원 **양자화 입력** (연속값, 회색조)
        //   7 추가 광원 **양자화 출력** (계단값, 회색조)
        //   8 월드 법선 (색). 면 전체가 **한 가지 색이면 그 면의 법선은 상수**다
        //   9 화면 기준 계단 8단 (셰이더·재질과 무관한 **정답 계단**)
        //
        // 9 번이 이 목록의 핵심이다. 자세한 사용법은 아래 `Frag` 의 주석에 있다.
        _DebugOutput ("진단 출력 (0 정상 … 9 기준 계단)", Range(0, 9)) = 0

        // ── 계단 축 재분배 (`UP-FIX-35` / G-4) ─────────────────────────────
        // 기본 꺼짐. 켜도 `_ShadeGamma = 1` 이면 수식적으로 동일하다.
        [Toggle(_SHADEGAMMA_ON)] _ShadeGammaEnabled ("계단 축 재분배 사용", Float) = 0
        _ShadeGamma ("계단 축 감마 (1 = 기존과 동일)", Range(0.25, 4)) = 1

        // ── PS1 정점 왜곡 (기본 꺼짐) ──────────────────────────────────────
        [Toggle(_PS1DISTORT_ON)] _PS1DistortEnabled ("PS1 정점 왜곡 사용", Float) = 0
        _VertexSnap    ("정점 스냅 격자 (가로 화소 수 · 0 = 끔)", Range(0, 512)) = 0
        _AffineAmount  ("어파인 텍스처 매핑 비율 (0 = 정상 원근)", Range(0, 1)) = 0

        // ── 색 심도 양자화 + 4×4 베이어 디더 (기본 꺼짐) ───────────────────
        [Toggle(_COLORDEPTH_ON)] _ColorDepthEnabled ("색 심도 양자화 사용", Float) = 0
        _ColorDepth     ("색 심도 단계 수 (0 = 끔)", Range(0, 64)) = 0
        _DitherStrength ("오더드 디더 세기", Range(0, 1)) = 1

        // ── 발광 마스크 (기본 꺼짐) ────────────────────────────────────────
        // ⚠ **기본값이 "black" 이면 안 된다.** 검정을 곱하면 발광이 항상 0 이 되고,
        // `MaterialPropertyBlock` 이 런타임에 넣는 `_EmissionColor` 가 **조용히
        // 죽는다** — 정화 점등·계기 발광·과수확 레일 셋이 그렇게 사라진다.
        // 「머티리얼 기본 상태에서 `_EmissionColor` 도 검정이니 같다」는 논증은
        // 틀렸다. 기본 상태가 아니라 **런타임 상태**가 문제이기 때문이다.
        // 그래서 곱셈 중립인 흰색을 기본으로 두고, 그 위에 키워드로 한 번 더 막는다.
        [Toggle(_EMISSIONMAP_ON)] _EmissionMapEnabled ("발광 마스크 사용", Float) = 0
        _EmissionMap ("발광 마스크 (흰색 = 중립)", 2D) = "white" {}

        // ── 근거리 그레인 (기본 꺼짐) ──────────────────────────────────────
        // ⚠ 기본값이 `"grey"` 가 아니라 `"linearGrey"` 인 이유: `"grey"` 는 sRGB
        // 인코딩된 0.5 라 선형으로 0.216 이 되어 **2배 곱셈의 중립이 아니다.**
        // `"linearGrey"` 는 선형 0.5 이므로 ×2 = 1.0 에 가장 가깝다.
        // (그래도 8bit 128/255 = 0.50196 이라 정확히 1.0 은 아니다. 그래서
        //  중립성을 텍스처 값에 의존하지 않고 키워드로 확정한다.)
        [Toggle(_DETAILMAP_ON)] _DetailMapEnabled ("근거리 그레인 사용", Float) = 0
        _DetailMap      ("근거리 그레인 (선형 회색 = 중립)", 2D) = "linearGrey" {}
        _DetailScale    ("그레인 반복", Range(0.1, 64)) = 8
        _DetailStrength ("그레인 세기", Range(0, 1)) = 1

        // ── 삼중 평면 UV (기본 꺼짐) ───────────────────────────────────────
        // Unity 기본 프리미티브(Cube)는 면마다 0~1 UV 라 3.2 m 벽에 텍스처를
        // 물리면 통째로 늘어난다. 삼중 평면은 UV 대신 월드 좌표를 쓴다.
        // ⚠ 이 경로는 `_BaseMap_ST` 를 무시하고 `_TriplanarScale` 만 쓴다.
        [Toggle(_TRIPLANAR_ON)] _TriplanarEnabled ("삼중 평면 UV 사용", Float) = 0
        _TriplanarScale ("삼중 평면 크기 (m 당 반복)", Range(0.05, 8)) = 0.5
        _TriplanarBlend ("삼중 평면 혼합 날카로움", Range(1, 16)) = 4

        // ── 금속 하이라이트 (기본 꺼짐, 2026-08-08) ────────────────────────
        //
        // **이 셰이더에는 스페큘러 항이 하나도 없었다.** `spec`·`halfDir`·`reflect`·
        // GGX 어느 것도 0 건이다. 그래서 금속이 빛날 방법이 물리적으로 없었고,
        // 독립 평가가 기계를 「찰흙 같다」고 지적한 것이 이것이다 — 재질 문제가
        // 아니라 **없는 항을 조정하려 한 것**이었다.
        //
        // 왜 GGX 가 아닌가: 이 파일의 첫 문단이 URP/Lit 을 버린 이유가
        // 「부드러운 하이라이트」다. 물리 기반 스페큘러를 그대로 넣으면 그 이유를
        // 스스로 되돌린다. 그래서 블린-퐁을 구한 **뒤 계단으로 끊는다** —
        // 이 파일이 램버트·감쇠·림에 이미 쓰는 것과 같은 처리다.
        //
        // ⚠ **문턱 아래를 정확히 0 으로 만드는 것이 핵심이다.** `Quantize` 를
        // 재사용하지 않는 이유가 그것이다 — 저쪽은 `_BandFloor` 를 더하므로
        // 하이라이트가 없어야 할 면에도 상수 밝기가 남아 방 전체가 뿌예진다.
        [Toggle(_SPECULAR_ON)] _SpecularEnabled ("금속 하이라이트 사용", Float) = 0
        // ⚠ **중립(1,1,1)이 기본이다.** 처음엔 따뜻하게 (1, 0.97, 0.90) 줬는데
        // 그것이 EEVEE 대비 R/B 비를 **나쁜 쪽으로** 밀었다 (2.324 → 2.349, 허용 상한 2.288).
        // AC-7 은 스페큘러를 켜기 전부터 실패하고 있었으므로 이 항이 원인은 아니지만,
        // 「직전 승인 빌드보다 나빠지면 채택하지 않는다」에 걸린다.
        //
        //     warm (1, 0.97, 0.90)  R/B 2.3494   ← 되돌렸다
        //     neutral (1, 1, 1)     R/B 2.3329   ← 채택
        //     cool (0.90, 0.96, 1)  R/B 2.3222
        //
        // 따뜻한 색조가 나트륨등 캐빈에 맞다고 판단되면 머티리얼에서 올리면 된다 —
        // 셰이더 기본값을 임의의 색으로 두지는 않는다.
        _SpecTint       ("하이라이트 색 (중립이 기본)", Color) = (1, 1, 1, 1)
        _SpecPower      ("하이라이트 날카로움", Range(4, 256)) = 48
        _SpecStrength   ("하이라이트 세기", Range(0, 4)) = 0.8
        _SpecSteps      ("하이라이트 계단 수 (1 = 켜짐/꺼짐)", Range(1, 4)) = 2
        _SpecThreshold  ("하이라이트 문턱 (아래는 정확히 0)", Range(0, 1)) = 0.25
        _SpecSoftness   ("문턱 부드러움 (계단 경계 앤티에일리어싱)", Range(0.001, 0.3)) = 0.05

        // ── 구운 AO (기본 꺼짐, 2026-08-08) ────────────────────────────────
        //
        // 독립 평가의 1순위 지적이 「드리운 그림자가 없다」였다. 인계 문서는
        // `AD53_BAKEALL` 의 `use_pass_direct/indirect` 를 켜라고 제안했지만 **그건 못 쓴다** —
        // 빛을 알베도에 구우면 Unity 가 그 위에 런타임 조명을 또 곱한다. 이중 조명을
        // 피하려면 런타임 점광을 줄여야 하고, 그러면 아래 조명 누산부 주석이 세 번의
        // 실패로 기록한 그것이 재현된다(「추가 광원이 위험 단계를 나른다」).
        //
        // **AO 는 조명과 무관하다.** 곱해도 이중 조명이 되지 않고 점광이 그대로 산다.
        // 접촉 음영과 접지감만 얻고 위험 단계 연출은 손대지 않는다.
        // (2026-08-08 사용자 결정: 「AO 정도만 굽고 광원 그림자는 굽지 마라」)
        //
        // **직접광에도 곱한다 — 주변광에만 곱는 것은 이 씬에서 무효다.**
        //
        // 처음엔 주변광에만 곱었다. 「AO 가 가리는 것은 간접광이지 직접광이 아니다」는
        // 물리적으로 옳지만 **이 씬에서는 아무 일도 하지 않는다.** 실측(2026-08-08):
        //
        //     ambientMode = Flat · ambientLight linear = (0.001, 0.001, 0.002)
        //     주변광 실효 휘도 0.00146 · × _ShadowLift 0.18 → ambientTerm ≈ 0.00026
        //     AO 45장을 배선하고 켠 뒤 평균 휘도 0.1035 → 0.1035 (변화 0)
        //
        // 그리고 반대 논거도 틀렸다. 「직접광에 곱하면 램프를 정면으로 받는 면이
        // 크레바스라는 이유로 어두워져 광원 위치가 안 읽힌다」고 적었지만 —
        // **크레바스 깊숙한 면은 램프를 향해도 실제로 어두워야 한다.** 램프가 가려져
        // 있으니까. 이 셰이더에 쓸 만한 점광 그림자가 없으므로 AO 가 그 대역이다.
        // 게다가 곱셈은 `ndotl · 감쇠` 의 **형태를 보존**하므로 광원 위치는 계속 읽힌다.
        // 원래 걱정은 AO 가 형태를 *대체*할 때만 성립하는 것이었다.
        //
        // `_AODirect` 로 직접광 쪽 비율을 따로 둔다 — 1.0 은 물리적으로 과하고
        // (AO 는 반구 평균이라 방향광에 그대로 쓰면 이중 계산이다) 0 은 무효다.
        //
        // ⚠ 진단 6·7 (`addTerm = lit - mainTerm`) 이 이제 AO 를 포함한다.
        //   AO 는 픽셀당 **양의 상수 배율**이라 그 진단이 보는 위험 단계 단조성은
        //   보존된다. 절대값만 낮아진다.
        //
        // ⚠ 기본값 `"white"` 가 곱셈 중립이지만 그것에 의존하지 않는다 —
        //   키워드가 꺼지면 샘플링 코드 자체가 컴파일되지 않는다(규약의 ②).
        // ⚠ `uv` 는 구운 알베도(`BK_*`)가 쓰는 것과 **같은 변수**다. 둘 다 블렌더의
        //   `UVBake` 레이어로 구웠으므로 그래야 정렬이 맞는다.
        [Toggle(_AOMAP_ON)] _AOMapEnabled ("구운 AO 사용", Float) = 0
        _AOMap          ("AO 맵 (흰색 = 중립)", 2D) = "white" {}
        _AOStrength     ("AO 세기", Range(0, 1)) = 1
        _AODirect       ("직접광에 곱하는 비율 (0 = 주변광만 → 이 씬에선 무효)", Range(0, 1)) = 0.7
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            // ── 점광 그림자 (2026-08-08) ────────────────────────────────────────
            // 이 셰이더에는 `_ADDITIONAL_LIGHT_SHADOWS` 가 없었다. `ShadowCaster` 패스는
            // 있어서 그림자맵은 채워지고 있었지만, 키워드가 없으니 **아무도 그 맵을 읽지
            // 않았다.** 방향광이 있는 동안에는 티가 안 났다 — 주광 그림자는 따로 켜져
            // 있으니까. 방향광을 끄자(밀폐된 방에 필라이트가 부자연스러워서) 캐빈 램프가
            // **추가광**이 되었고, 그 순간 방 전체에서 드리운 그림자가 통째로 사라졌다.
            //
            // 증상은 톤 문제로 위장한다: 그림자가 없으니 어두운 화소가 생기지 않고,
            // 아무리 앰비언트·밴드 바닥을 낮춰도 p05 가 내려가지 않는다. 실제로 EEVEE
            // 기준선과의 마지막 차이가 전부 이것이었다.
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            // **Forward+ 를 선언하지 않으면 추가 광원이 통째로 안 보인다.**
            // 이 프로젝트의 `PC_Renderer` 는 `m_RenderingMode = 2`(ForwardPlus)다.
            // 그 경로에서는 추가 광원이 화면 클러스터에 들어가고,
            // 고전 `GetAdditionalLightsCount()` 루프는 **0 을 돌려준다.**
            // URP 17 은 `_FORWARD_PLUS` 를 폐기하고 `_CLUSTER_LIGHT_LOOP` 로 바꿨다.
            // 옛 이름을 쓰면 컴파일은 되지만 경고와 함께 변형 수가 불어난다.
            // (URP 17.5 실물 확인: `ShaderLibrary/Core.hlsl:13` 이
            //  `#if defined(_CLUSTER_LIGHT_LOOP)` 로 `USE_CLUSTER_LIGHT_LOOP` 를 정하고,
            //  `ForwardPlusKeyword.deprecated.hlsl` 이 옛 이름을 새 이름으로 넘긴다.)
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            // ── 신규 기능은 전부 `shader_feature_local` 이다 ──────────────
            // 머티리얼이 실제로 켠 조합만 컴파일된다. 아무 머티리얼도 켜지 않은
            // 지금은 **변형 수가 종전과 정확히 같다.**
            // 프래그먼트 전용인 것은 `_fragment` 를 붙여 정점 프로그램이
            // 불필요하게 늘어나지 않게 한다.
            #pragma shader_feature_local          _PS1DISTORT_ON
            #pragma shader_feature_local_fragment _SHADEGAMMA_ON
            #pragma shader_feature_local_fragment _COLORDEPTH_ON
            #pragma shader_feature_local_fragment _EMISSIONMAP_ON
            #pragma shader_feature_local_fragment _DETAILMAP_ON
            #pragma shader_feature_local_fragment _TRIPLANAR_ON
            #pragma shader_feature_local_fragment _SPECULAR_ON
            #pragma shader_feature_local_fragment _AOMAP_ON
            #pragma shader_feature_local_fragment _BANDSOFT_ON
            #pragma shader_feature_local_fragment _MINFILL_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            // `LinearToSRGB` / `SRGBToLinear` 를 쓴다. 인클루드 가드가 있어
            // 이미 들어와 있어도 안전하다.
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShadowTint;
                float  _Steps;
                float  _FalloffPower;
                float  _AmbientFloor;
                float  _ShadowLift;
                float  _BandFloor;
                float  _BandSoftness;
                float  _MinFill;
                float  _RimStrength;
                float  _NearAttenClamp;
                float4 _EmissionColor;
                float4 _BaseMap_ST;
                float  _DebugOutput;
                // ── 신규 ── SRP Batcher 는 머티리얼 유니폼이 전부 이 블록 안에
                // 있어야 하므로 토글 플로트와 미사용 `_ST` 도 함께 선언한다.
                float  _ShadeGammaEnabled;
                float  _ShadeGamma;
                float  _PS1DistortEnabled;
                float  _VertexSnap;
                float  _AffineAmount;
                float  _ColorDepthEnabled;
                float  _ColorDepth;
                float  _DitherStrength;
                float  _EmissionMapEnabled;
                float4 _EmissionMap_ST;
                float  _DetailMapEnabled;
                float4 _DetailMap_ST;
                float  _DetailScale;
                float  _DetailStrength;
                float  _TriplanarEnabled;
                float  _TriplanarScale;
                float  _TriplanarBlend;
                float  _SpecularEnabled;
                float4 _SpecTint;
                float  _SpecPower;
                float  _SpecStrength;
                float  _SpecSteps;
                float  _SpecThreshold;
                float  _SpecSoftness;
                float  _AOMapEnabled;
                float4 _AOMap_ST;
                float  _AOStrength;
                float  _AODirect;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_DetailMap);
            SAMPLER(sampler_DetailMap);
            TEXTURE2D(_AOMap);
            SAMPLER(sampler_AOMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float  fogCoord   : TEXCOORD2;
                float2 uv         : TEXCOORD3;
                #if defined(_PS1DISTORT_ON)
                    // 어파인(원근 보정 없는) UV 를 만들기 위한 보조 보간자.
                    // `noperspective` 를 쓰지 않는 이유는 아래 `Frag` 참조.
                    float3 uvAffine : TEXCOORD4;
                #endif
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs   nrm = GetVertexNormalInputs(input.normalOS);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS   = nrm.normalWS;
                output.fogCoord   = ComputeFogFactor(pos.positionCS.z);
                output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);

                #if defined(_PS1DISTORT_ON)
                    // ── 정점 스냅 ──────────────────────────────────────────
                    // PS1 은 정점 좌표를 정수 화소 격자에 고정소수점으로 저장했다.
                    // 그래서 화면에서 정점이 격자에 붙어 흔들린다. 클립 공간에서
                    // NDC 로 나눈 뒤 격자에 반올림하고 다시 w 를 곱해 되돌린다.
                    //
                    // `w > 0` 인 정점만 건드린다 — 카메라 뒤(`w <= 0`)의 정점을
                    // 나누면 부호가 뒤집혀 삼각형이 화면을 가로지른다.
                    if (_VertexSnap > 0.5 && output.positionCS.w > 1e-4)
                    {
                        // 세로 격자는 화면 비율을 따라가게 해서 화소를 정사각형으로 둔다.
                        float aspect = _ScreenParams.y / max(_ScreenParams.x, 1.0);
                        float2 gridPx = float2(_VertexSnap, _VertexSnap * aspect);
                        float2 grid   = max(gridPx * 0.5, 1.0);   // NDC 는 [-1,1] 이라 절반

                        float2 ndc = output.positionCS.xy / output.positionCS.w;
                        ndc = floor(ndc * grid + 0.5) / grid;
                        output.positionCS.xy = ndc * output.positionCS.w;
                    }

                    // ── 어파인 텍스처 매핑용 보조 보간자 ────────────────────
                    // `w` 는 스냅으로 바뀌지 않으므로 스냅 뒤에 읽어도 같다.
                    output.uvAffine = float3(output.uv * output.positionCS.w,
                                             output.positionCS.w);
                #endif

                return output;
            }

            /// 지각 휘도. 계단을 걸 축이고, 그림자가 환경을 따라가는지 재는 축이기도 하다.
            float Lum(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

            // 램버트를 계단으로 끊는다. 이것이 「플랫/Gouraud」의 핵심이고,
            // 부드러운 그라디언트를 없애 폴리곤 면이 드러나게 한다.
            //
            // ⚠ 알려진 경계 결함: `value == 1.0` 이면 `floor(1.0 * steps) == steps` 라
            // 결과가 `steps / (steps - 1)` 즉 `_Steps 3` 에서 **1.5** 가 된다.
            // 밝은 쪽으로 한 칸 넘친다. 고치면 그 조건의 화소가 실제로 어두워지므로
            // **기본값 불변 규약에 걸린다** — 여기서 고치지 않고 기록만 한다.
            // 발생 조건: `saturate(ndotl * atten) == 1`, 즉 광원을 정면으로 받고
            // 감쇠가 1 인 면. 현재 씬에서는 주광 `ndotl` 최대가 0.749(뒷벽)라 미발생.
            //
            // ── `_BandFloor` — 0 번 칸이 「빛 없음」이 되는 것을 막는다 (2026-08-02) ──
            //
            // **이 함수는 실내 점광을 화면에서 지우고 있었다.** 발견 경위:
            // 사용자 명세로 4.0 × 4.6 × 2.9 m 방을 세우고 천장 케이지 전구 하나를
            // 주광으로 두자 화면이 **p50 = 0, p75 = 0** 으로 나왔다. 통제 실험 5회 —
            // 감쇠 지수, 알베도, 광원 range, 광원 그림자, `_ShadeGamma` 를 각각 바꿔도
            // p50 이 0 에서 안 움직였고, **앰비언트만** 즉시 작동했다.
            //
            // 산술이 그대로 설명한다. URP 점광의 `distanceAttenuation` 은 역제곱이라
            // 거리 2.7 m 에서 약 **0.137** 이다. `_Steps 4` 의 첫 칸 경계는 0.25 이므로
            // `floor(0.137 * 4) = 0` → 결과 **정확히 0**. 즉 이 방의 거의 모든 표면에서
            // 직접광 항이 통째로 사라진다. `range` 를 22 로 늘려도 역제곱이 지배해서
            // 넘지 못한다 — 실측으로 확인했다.
            //
            // 「빠른 감쇠」는 스타일 락이지만 **「빛이 0」은 감쇠가 아니라 소등**이다.
            // 그래서 칸 값의 바닥만 들어 올린다. 계단의 개수도 간격도 그대로다.
            //
            // ⚠ **기본값 0 에서 수식이 종전과 완전히 동일하다** — `(band + 0) / (steps - 1 + 0)`.
            // 이 파일의 「기본값 불변 규약」이 요구하는 것이 그것이고, 그래서 기존
            // 머티리얼 23장의 그림은 비트 단위로 안 바뀐다. 새 방의 머티리얼만 켠다.
            float Quantize(float value, float steps)
            {
                steps = max(2.0, steps);
                // 2026-08-08 — 위에 「기록만 한다」고 적어 둔 그 경계 결함을 고친다.
                // 「현재 씬에서는 ndotl 최대 0.749 라 미발생」은 **그 램프가 들어오기 전**
                // 이야기였다. `LT_CabBulb` 는 천장에서 0.36 m 아래에 달려 있어 바로 위
                // 천장면이 `ndotl ≈ 1`, `distanceAttenuation ≈ 1` 이 되고, 그래서
                // `floor(1.0 * steps) == steps` → `steps / (steps - 1)` = **1.33** 이
                // 실제로 나왔다. 천장 광원 자리가 흰색으로 타고 링 모양 밴딩이 보인 것이
                // 이것이다. 광원 세기를 낮춰도 사라지지 않는다 — 넘치는 값은 세기가 아니라
                // `distanceAttenuation` 이 정하기 때문이다.
                //
                // 클램프는 **결함 조건인 화소에서만** 값을 바꾼다. 나머지 화소는 이미
                // `floor(v * steps) <= steps - 1` 이라 비트 단위로 동일하다 —
                // 「기본값 불변 규약」은 그대로 지켜진다.
                #if defined(_BANDSOFT_ON)
                    // 경계에서만 다음 칸으로 넘어간다. 칸의 개수·높이·바닥은 그대로다.
                    // `w` 는 **칸 폭 대비 비율**이라 `_Steps` 를 바꿔도 체감이 유지된다.
                    float scaled = saturate(value) * steps;
                    float band   = min(floor(scaled), steps - 1.0);
                    float w      = clamp(_BandSoftness, 0.02, 1.0);
                    // `scaled - band` 는 칸 안에서의 위치(0~1). 마지막 w 구간에서만 올라간다.
                    float blend  = smoothstep(1.0 - w, 1.0, scaled - band);
                    float soft   = min(band + blend, steps - 1.0);
                    return (soft + _BandFloor) / (steps - 1.0 + _BandFloor);
                #else
                    float band = min(floor(saturate(value) * steps), steps - 1.0);
                    return (band + _BandFloor) / (steps - 1.0 + _BandFloor);
                #endif
            }

            // **계단 축 재분배 — `UP-FIX-35` / G-4 의 손잡이.**
            //
            // 왜 필요한가. `Quantize` 는 **선형 구간을 균등 분할**한다. 그런데 이 셰이더가
            // 양자화하는 값 `ndotl * pow(distanceAttenuation, _FalloffPower)` 는
            // 실내 점광원에서 거의 전부 0.4 이하에 몰린다. `_Steps 3` 이면 첫 칸의
            // 경계가 1/3 이라 **벽 대부분이 0 번 칸 하나에 들어간다.** 칸이 하나면
            // 계단이 없다. 계단 수를 올려도 위쪽 칸들이 아무도 안 쓰는 구간에 배치될 뿐이다.
            //
            // 그래서 **칸을 늘리는 게 아니라 축을 늘린다.** 양자화 직전에 값을
            // `pow(v, 1/g)` 로 펴서 낮은 값들이 여러 칸에 흩어지게 하고, 양자화 뒤에
            // `pow(q, g)` 로 되돌려 **밝기 수준은 원래 척도로 돌려놓는다.**
            // 계단 「위치」만 바뀌고 「높이 범위」는 그대로다 — 위험 4단계가 나르는
            // 신호(광원 세기·앰비언트)는 이 곱셈 바깥에 있으므로 건드리지 않는다.
            //
            // `g = 1` 이면 `pow(v,1)`·`pow(q,1)` 이라 항등이지만, 그조차 컴파일하지
            // 않도록 키워드로 감싼다. 키워드가 꺼진 변형에는 이 코드가 없다.
            float QuantizeShade(float value, float steps)
            {
                #if defined(_SHADEGAMMA_ON)
                    // 축을 편 뒤 양자화한다. **복원하지 않는다.**
                    //
                    // 첫 판본은 `pow(band, g)` 로 되돌렸고 그것이 이 기능을 무용지물로
                    // 만들었다 — 실측에서 p99 가 47 → **1** 로 악화됐다. 이유는 산술이다:
                    // 복원이 낮은 칸을 원래 자리로 되돌리므로 `band 1/3` 이
                    // `(1/3)^3 = 0.037` 이 된다. **칸은 늘어나는데 전부 0 근처로 모인다.**
                    // 「계단 위치만 바꾸고 밝기 범위는 지킨다」는 의도였는데, 지키려던
                    // 그 범위가 애초에 이 문제의 원인이었다.
                    //
                    // 복원을 빼면 어두운 구간이 밝은 쪽으로 펴진다. 그것이 실내 점광에서
                    // 원하는 것이다 — `distanceAttenuation` 이 역제곱이라 0.05~0.3 에
                    // 몰려 있고, 그 구간에 **계조가 없으면 조명이 평평해 보인다.**
                    //
                    // `g = 1` 이면 `pow(v, 1)` 이라 `Quantize(value, steps)` 와 같다.
                    float g    = max(0.0001, _ShadeGamma);
                    float wide = PositivePow(saturate(value), rcp(g));
                    return Quantize(wide, steps);
                #else
                    return Quantize(value, steps);
                #endif
            }

            #if defined(_COLORDEPTH_ON)
                // 4×4 오더드(베이어) 행렬. 「손그림 픽셀 그레인」의 화면 쪽 절반이다.
                //
                // **배열로 두지 않는 이유**: 이 파일에는 `#pragma target` 이 없어
                // 하위 셰이더 모델로도 컴파일된다. 상수 배열을 런타임 인덱스로 읽는 것은
                // 그 경로에서 실패하거나 조용히 언롤되어 레지스터를 먹는다.
                // 아래 항등식은 배열 없이 같은 행렬을 만든다 —
                //   Bayer2 = [[0,2],[3,1]] / 4,  Bayer4 = Bayer2(a/2)/4 + Bayer2(a)
                // 검산: y=0 행이 0, 0.5, 0.125, 0.625 → ×16 = 0, 8, 2, 10 (표준 4×4 첫 행).
                float Bayer2(float2 a)
                {
                    a = floor(a);
                    return frac(a.x * 0.5 + a.y * a.y * 0.75);
                }
                float Bayer4(float2 a)
                {
                    return Bayer2(a * 0.5) * 0.25 + Bayer2(a);
                }

                // **표시 공간(sRGB)에서 끊는다.** PS1 의 15bit 프레임버퍼는 표시 색을
                // 끊었지 선형 광량을 끊지 않았다. 선형에서 끊으면 어두운 쪽 칸이
                // 전부 붙어 버려 계단이 밝은 쪽에만 생긴다.
                //
                // ⚠ 이 경로는 색을 `saturate` 한다 — 프레임버퍼 심도를 흉내 내는 것이므로
                // 1 을 넘는 HDR 발광은 여기서 잘린다. 블룸을 나중에 켜면 색 심도를 켠
                // 머티리얼의 발광이 덜 번진다. 그건 이 기능의 정의상 그렇다.
                float3 QuantizeColorDepth(float3 linearColor, float2 pixelCoord)
                {
                    float levels = max(2.0, _ColorDepth);
                    // 베이어 패턴의 주기는 x·y 모두 4 다. 먼저 4 로 접어야 1080p 이상에서
                    // `a.y * a.y` 가 float 정밀도를 잃고 디더 패턴이 무너지지 않는다.
                    float2 p = fmod(floor(pixelCoord), 4.0);
                    float t = Bayer4(p) - 0.46875;            // [0,1) → 대칭 [-0.469, 0.469]

                    float3 s = LinearToSRGB(saturate(linearColor));
                    s = floor(s * (levels - 1.0) + 0.5 + t * _DitherStrength) / (levels - 1.0);
                    return SRGBToLinear(saturate(s));
                }
            #endif

            #if defined(_TRIPLANAR_ON)
                // 삼중 평면. UV 가 없거나 면마다 0~1 로 반복되는 프리미티브에
                // 월드 좌표로 텍스처를 물린다. 큰 벽에서 늘어남을 막는 유일한 방법이다.
                float3 SampleTriplanar(TEXTURE2D_PARAM(tex, samp), float3 positionWS, float3 normalWS)
                {
                    // 스칼라를 그대로 넘기면 `PositivePow` 의 float/min16float 오버로드가
                    // 모호해질 수 있다. float3 로 명시한다.
                    float3 w = PositivePow(abs(normalWS),
                                           float3(_TriplanarBlend, _TriplanarBlend, _TriplanarBlend));
                    w /= max(w.x + w.y + w.z, 1e-5);

                    float s = _TriplanarScale;
                    float3 cx = SAMPLE_TEXTURE2D(tex, samp, positionWS.zy * s).rgb;
                    float3 cy = SAMPLE_TEXTURE2D(tex, samp, positionWS.xz * s).rgb;
                    float3 cz = SAMPLE_TEXTURE2D(tex, samp, positionWS.xy * s).rgb;
                    return cx * w.x + cy * w.y + cz * w.z;
                }
            #endif

            #if defined(_SPECULAR_ON)
                // 계단으로 끊은 블린-퐁. 광원 하나가 기여하는 하이라이트를 돌려준다.
                //
                // `atten` 은 호출부가 이미 `_FalloffPower` 까지 먹인 감쇠다. 하이라이트도
                // 같은 감쇠를 타야 「빠른 감쇠」라는 스타일 락이 하이라이트에서만 깨지지 않는다.
                //
                // **알베도를 곱하지 않는다.** 하이라이트는 표면에서 튄 빛이지 재질 색이
                // 아니다. 곱하면 어두운 무쇠에서 하이라이트가 사라져 「금속이 빛나지 않는다」는
                // 원래 증상이 그대로 남는다.
                float3 StylizedSpec(float3 normalWS, float3 lightDir, float3 viewDirWS,
                                    float3 lightColor, float atten)
                {
                    float3 halfDir = SafeNormalize(lightDir + viewDirWS);
                    float  raw     = PositivePow(saturate(dot(normalWS, halfDir)), _SpecPower);

                    // 문턱 아래는 **정확히 0**. `smoothstep` 은 하한 아래에서 0 을 보장하므로
                    // 이 성질이 부동소수점 오차 없이 성립한다.
                    float  t     = smoothstep(_SpecThreshold, _SpecThreshold + _SpecSoftness, raw);

                    // `ceil` 이라 t 가 0 을 넘는 순간 첫 칸(1/steps)으로 뛴다 — 그게 계단이다.
                    // `steps = 1` 이면 켜짐/꺼짐 두 값뿐이라 가장 거친 스탬프가 된다.
                    float  steps = max(1.0, floor(_SpecSteps));
                    float  band  = ceil(t * steps) / steps;

                    return lightColor * _SpecTint.rgb * band * atten * _SpecStrength;
                }
            #endif

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);

                // ── UV 결정 ────────────────────────────────────────────────
                // 기본(두 키워드 모두 꺼짐)에서는 `uv == input.uv` 로 **종전과 동일**하다.
                float2 uv = input.uv;

                #if defined(_PS1DISTORT_ON)
                    // **어파인 텍스처 매핑.** PS1 은 원근 보정 나눗셈을 하지 않아
                    // 큰 폴리곤의 텍스처가 삼각형 경계에서 꺾였다. 그게 이 룩의 서명이다.
                    //
                    // `noperspective` 보간자를 쓰지 않는 이유: 플랫폼·백엔드마다 지원이
                    // 갈리고(특히 GLES/일부 Metal 경로) 실패하면 조용히 무시된다.
                    // 대신 하드웨어 보간의 정의를 그대로 이용한다 —
                    // 하드웨어는 속성 A 를 `(Σ λᵢAᵢ/wᵢ) / (Σ λᵢ/wᵢ)` 로 보간한다.
                    //   Aᵢ = uvᵢ·wᵢ 를 넣으면 분자가 `Σ λᵢuvᵢ`
                    //   Bᵢ = wᵢ    를 넣으면 결과가 `1 / (Σ λᵢ/wᵢ)`
                    //   A/B = `Σ λᵢuvᵢ`  ← 화면 선형, 즉 정확히 어파인.
                    // 근사가 아니라 정확한 어파인이고 추가 문법도 필요 없다.
                    float2 uvAffine = input.uvAffine.xy / max(input.uvAffine.z, 1e-5);
                    uv = lerp(uv, uvAffine, saturate(_AffineAmount));
                #endif

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float ndotl = saturate(dot(normalWS, mainLight.direction));
                // 감쇠를 가파르게 — 「빠른 감쇠」. 거듭제곱이 가장 싸다.
                //
                // ⚠ 관측: 방향광은 `distanceAttenuation == 1` 이고 이 씬의 주광은
                // 그림자도 끄여 있어(`m_Shadows.m_Type: 0`) `shadowAttenuation == 1` 이다.
                // 즉 주광에 대해 `atten == pow(1, _FalloffPower) == 1` 로 **상수**이며
                // `_FalloffPower` 는 주광에 대해 아무 일도 하지 않는다.
                float atten = pow(min(saturate(mainLight.distanceAttenuation * mainLight.shadowAttenuation),
                                      _NearAttenClamp),
                                  _FalloffPower);
                float lambert = QuantizeShade(ndotl * atten, _Steps);

                // 텍스처를 **기본색에 곱한다.** 흰 텍스처면 `albedo == _BaseColor.rgb` 라
                // 기존 머티리얼의 결과가 비트 단위로 같다. 아래 세 군데가 전부 이 값을 쓴다 —
                // 하나라도 `_BaseColor` 를 직접 쓰면 텍스처가 그늘에서만 사라지는
                // 「반쯤 적용」이 되고, 그게 가장 찾기 어려운 종류의 결함이다.
                #if defined(_TRIPLANAR_ON)
                    float3 baseTex = SampleTriplanar(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap),
                                                     input.positionWS, normalWS);
                #else
                    float3 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
                #endif
                float3 albedo = _BaseColor.rgb * baseTex;

                #if defined(_DETAILMAP_ON)
                    // 근거리 그레인. 선형 회색(0.5)이 ×2 = 1.0 으로 곱셈 중립이다.
                    #if defined(_TRIPLANAR_ON)
                        float3 detail = SampleTriplanar(TEXTURE2D_ARGS(_DetailMap, sampler_DetailMap),
                                                        input.positionWS * _DetailScale, normalWS);
                    #else
                        float3 detail = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap,
                                                         uv * _DetailScale).rgb;
                    #endif
                    albedo *= lerp(1.0, detail * 2.0, saturate(_DetailStrength));
                #endif

                float3 mainTerm = albedo * mainLight.color * lambert;
                float3 lit = mainTerm;

                #if defined(_SPECULAR_ON)
                    // ⚠ 아래쪽 림이 쓰는 `viewDir` 과 같은 값이지만 **그 줄을 위로 옮기지
                    // 않고 여기서 새로 만든다.** 옮기면 기능이 **꺼진** 변형의 코드까지
                    // 바뀐다 — 「기본값 불변 규약」은 꺼진 변형이 종전과 비트 단위로 같기를
                    // 요구한다. 켜진 변형에서는 컴파일러가 공통부분식으로 합치므로 비용은 없다.
                    float3 specViewDir = normalize(GetWorldSpaceViewDir(input.positionWS));

                    // **하이라이트는 `lit` 에 넣지 않는다.** 진단 6·7 이
                    // `addTerm = lit - mainTerm` 으로 추가 광원 기여만 떼어 보는데,
                    // 여기에 섞으면 그 진단이 조용히 거짓말을 하게 된다.
                    float3 specAccum = StylizedSpec(normalWS, mainLight.direction, specViewDir,
                                                    mainLight.color, atten);
                #endif

                // 진단 6·7 이 쓰는 누적기. `color` 에 들어가지 않으므로 출력에 영향이 없다.
                float addRaw  = 0.0;   // 양자화 **입력**의 합 (연속)
                float addBand = 0.0;   // 양자화 **출력**의 합 (계단)

                // **추가 광원이 위험 단계를 나른다.** 씬의 점광 1 · 스팟 1 이 그것이고,
                // 방향광은 벽에 균일하게 닿으므로 위험 연출을 실어 나르지 못한다.
                //
                // 그래서 이 루프가 도는지 아닌지가 「위험 단계가 벽에 보이는가」를 통째로
                // 결정한다. 세 번의 실패가 전부 여기서 나왔다 — 그림자 항을 상수로 둔 것도,
                // 환경 밝기를 곱한 것도, 계단 수를 올린 것도 전부 **빛이 0 인 상태**에
                // 대고 한 조정이었다. 좌벽 200px 이 정확히 한 가지 값이었던 것이 증거다.
                //
                // Forward+ 에서는 광원 순회를 `LIGHT_LOOP_BEGIN` 이 맡는다. 그 매크로가
                // `inputData.normalizedScreenSpaceUV` 로 화면 클러스터를 찾으므로
                // 아래 두 필드를 반드시 채워야 한다 — 안 채우면 조용히 0 개가 나온다.
                // **이름이 `inputData` 여야 한다.** `LIGHT_LOOP_BEGIN` 매크로가 그 이름을
                // 그대로 참조한다 — 다른 이름을 쓰면 매크로 전개가
                // `undeclared identifier 'inputData'` 로 깨진다. 실제로 깨뜨렸고,
                // 그 뒤의 캡처가 전부 **마젠타 오류 셰이더**였다.
                // (URP 17.5 실물 확인: `ShaderLibrary/RealtimeLights.hlsl:35-41` 이
                //  `ClusterInit(inputData.normalizedScreenSpaceUV, inputData.positionWS, 0)`.)
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    // 2인자 오버로드는 `shadowAttenuation` 을 **항상 1 로 둔다.**
                    // 그림자를 실제로 샘플링하는 것은 셋째 인자(그림자 마스크)가 있는 쪽뿐이다.
                    // 라이트맵을 쓰지 않으므로 마스크는 1 을 넣는다.
                    Light extra = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    float extraNdotl = saturate(dot(normalWS, extra.direction));
                    float extraAtten = pow(min(saturate(extra.distanceAttenuation * extra.shadowAttenuation),
                                               _NearAttenClamp),
                                           _FalloffPower);
                    float extraRaw  = extraNdotl * extraAtten;
                    float extraBand = QuantizeShade(extraRaw, _Steps);
                    lit += albedo * extra.color * extraBand;
                    addRaw  += extraRaw;
                    addBand += extraBand;

                    #if defined(_SPECULAR_ON)
                        // 이 씬에서 **하이라이트를 실어 나르는 것은 사실상 이 루프뿐이다** —
                        // 방향광 루트가 꺼져 있고 활성 조명 2개가 전부 점광이다
                        // (`LT_CabBulb` · `LT_SoulSpill`). 주광 쪽에도 넣어 두는 것은
                        // 나중에 방향광을 되살릴 때 하이라이트만 빠지지 않게 하기 위해서다.
                        specAccum += StylizedSpec(normalWS, extra.direction, specViewDir,
                                                  extra.color, extraAtten);
                    #endif
                LIGHT_LOOP_END

                // **빛은 더한다. 스타일은 더한 뒤에 건다.**
                //
                // 이 셰이더는 위험 단계를 두 번 먹었고, 두 번 다 원인이 여기였다.
                //
                // 첫 판본은 `lerp(shadowed, lit, saturate(lambert + _AmbientFloor))` 였다.
                // `shadowed` 는 재질 색과 색조만으로 만든 **상수**라 조명과 무관했고,
                // 섞임 계수 `lambert` 는 **주광만** 보고 만든 값이었다. 위험 연출은
                // **추가 광원**을 움직이는데, 그것들이 아무리 흔들려도 벽에 실리는 양이
                // 주광이 정한 만큼으로 눌렸다. `_AmbientFloor 0.35` 가 그 눌림의 바닥을
                // 고정했다.
                //
                // 11차 독립 판정 실측 — 벽에 켠 순간 좌벽 휘도가 89.2 → 79.4 → 72.5 → 66.7
                // (위험 4단계 단조 하강)에서 **네 단계 전부 82.3** 으로 평탄해졌고,
                // `Stable` 과 `Collapse` 프레임의 **60.5% 가 바이트 동일**이 됐다.
                // 「최악 상태의 계기가 가장 태연하다」(`UP-FIX-10`)가 환경 전체로 번진 것이다.
                //
                // 두 번째 시도는 그림자 항에 「환경 밝기」를 곱하는 것이었다. 더 나빠졌다 —
                // 좌벽이 위험 3단계에서 **55.24 로 완전히 동일**했다. 곱한 값 자체가 상수였다.
                // 증상을 한 겹 아래에서 다시 만든 것이고, 축을 안 바꾸고 세게 민 셈이다.
                //
                // URP/Lit 이 위험 단계를 그대로 보여 준 이유는 단순하다 — **그건 빛을 더한다.**
                // 계단 셰이딩과 회녹색 그림자는 빛을 어떻게 **누적하느냐**와 무관한 축이다.
                // 누적 방식까지 바꿀 이유가 없었다. 누적은 표준대로 두고, 스타일은
                // 누적이 끝난 뒤에 건다.
                //
                // 회녹색은 그래서 **주변광에 얹는다** — 빛이 없는데 색조가 남아 있으면
                // 그건 그림자가 아니라 자체발광이다. 검정으로 떨어뜨리지 않는 것이 락의
                // 요구이고(어두운 무쇠가 그늘에서 형태를 잃으면 안 된다), 그건 색조를
                // **더해서** 지킨다. 곱하면 어두운 기본색이 0 이 된다 — 심볼 3종에서
                // 그렇게 「거의 검은 덩어리」가 나와 되돌린 적이 있다.
                //
                // ⚠ 관측: 이 씬은 `m_AmbientMode: 3`(Flat) 이라 `SampleSH(normalWS)` 가
                // **법선과 무관한 상수**를 돌려준다. 즉 앰비언트는 화면 어디서도 변하지
                // 않으며, 「앰비언트가 연속이라 계단을 메운다」는 가설은 이 씬에서 성립하지
                // 않는다. 진단 2 번으로 직접 확인할 수 있다 — 화면 전체가 단색이면 상수다.
                float3 sh = SampleSH(normalWS);
                float3 ambientTerm = albedo * sh * _ShadowLift
                                   + _ShadowTint.rgb * 0.35 * saturate(Lum(sh) * 2.0);
                #if defined(_AOMAP_ON)
                    // 위 프로퍼티 주석의 실측 근거를 따른다 — 이 씬은 주변광이
                    // 선형 0.00146 이라 **주변광에만 곱면 화면이 비트 단위로 안 바뀐다.**
                    // 직접광에도 `_AODirect` 비율로 곱해야 접촉 음영이 실제로 보인다.
                    float ao     = SAMPLE_TEXTURE2D(_AOMap, sampler_AOMap, uv).r;
                    float aoStr  = saturate(_AOStrength);
                    ambientTerm *= lerp(1.0, ao, aoStr);
                    lit         *= lerp(1.0, ao, aoStr * saturate(_AODirect));
                #endif

                float3 color = ambientTerm + lit;

                #if defined(_MINFILL_ON)
                    // **AO 뒤에 더한다.** 앞에 두면 AO 가 이 항까지 0 으로 만들어,
                    // 정작 검게 떨어지는 오목한 곳(= 사용자가 지적한 그 테두리)에서만
                    // 효과가 사라진다. 「어떤 면도 순수 검정으로 떨어지지 않는다」가
                    // 이 항의 계약이므로 무엇도 이것을 지우면 안 된다.
                    color += albedo * _MinFill;
                #endif

                #if defined(_SPECULAR_ON)
                    // **더한다, 곱하지 않는다.** 이 파일이 회녹색 색조에서 배운 것과 같다 —
                    // 곱하면 어두운 기본색이 0 이 되어 하이라이트도 같이 사라진다.
                    color += specAccum;
                #endif

                // **계단은 이미 걸려 있다 — 빛마다, 감쇠에.** 여기서 최종 휘도에 또 걸지 않는다.
                //
                // 한 번 그렇게 해 봤고 위험 단계를 통째로 먹었다. `_Steps` 6 이면 한 칸이
                // 0.167 인데 위험 단계 사이의 차이는 0.07 남짓이다 — 네 단계가 **같은 칸**에
                // 떨어져 109.45 로 완전히 동일해진다. 계단 수를 8 로 올려도 마찬가지다.
                //
                // 계단이 끊어야 하는 것은 **형태 음영**이지 **전체 밝기**가 아니다.
                // 「빛이 얼마나 비스듬히 닿는가」는 칸으로 끊어야 폴리곤 면이 드러나고,
                // 「빛이 얼마나 센가」는 이어져야 위험이 읽힌다. 위쪽 `QuantizeShade(ndotl * atten)`
                // 이 앞의 것을 하고, 빛 색·세기는 그 뒤에 연속으로 곱해진다.
                // 두 축이 한 곱셈 안에서 각자 제 일을 한다.
                // 실루엣을 살짝 세운다. 「큰 실루엣」이 락의 첫 항목이고,
                // 무지 머티리얼끼리 겹치면 경계가 사라진다.
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float rim = pow(1.0 - saturate(dot(normalWS, viewDir)), 3.0);

                // **림도 계단으로 끊는다. 이것이 계단 셰이딩이 안 보이던 마지막 구멍이다.**
                //
                // 12차 판정 실측 — 셰이더가 실제로 도는데도 `12` y=470 의 200px 에
                // 평탄 구간이 **156개**(최장 7px)였다. 사실상 연속 그라데이션이다.
                // 「렌더러에 안 걸렸다」로는 더 설명되지 않으므로 로직 문제로 재분류됐다.
                //
                // 조명 항은 전부 이미 끊겨 있다 — 주광 `Quantize(ndotl * atten)`,
                // 추가 광원도 같은 형태, 앰비언트는 Flat 이라 면 안에서 상수다.
                // **남은 연속 항이 림 하나였다.** `pow(1 - dot(N, V), 3)` 은 시선각을
                // 따라 매끄럽게 변하고, 넓은 벽을 비스듬히 보면 그 부드러운 기울기가
                // 앞에서 끊어 놓은 계단을 **뒤에서 메운다.**
                //
                // 세기(`_RimStrength`)를 건드리지 않고 **모양만** 끊는다. 그래서 이 변경은
                // 밝기 수준에 손대지 않고, 위험 단계가 나르는 신호(앰비언트·광원 세기)와
                // 직교한다 — 12차가 회귀 감시로 지정한 좌벽 ΔL ≥ 15 를 구조적으로 건드리지 않는다.
                //
                // ── 그리고 그것도 아니었다. 14차 실측도 24장 중 0장이었다. ──────────
                //
                // 그래서 15차는 추측을 멈추고 씬 데이터를 직접 읽었다. 위 「남은 연속 항이
                // 림 하나」라는 진단은 **틀렸다.** 실제로는 연속 항이 문제가 아니라
                // **양자화기의 입력이 면 위에서 변하지 않는다**는 것이 문제였다.
                //
                //   ① 벽 8장이 전부 **Unity 기본 Cube 한 장**이다 (`m_Mesh: 10202`).
                //      Cube 의 한 면은 법선이 하나뿐인 쿼드 2삼각형이라 `normalWS` 가
                //      **면 전체에서 상수**다. 좌벽은 3.2 m × 3.2 m 짜리 상수 법선 (1,0,0).
                //   ② 주광은 방향광이라 `distanceAttenuation == 1` 이고, 이 씬에서는
                //      **그림자도 꺼져 있어**(`m_Shadows.m_Type: 0`) `shadowAttenuation == 1`.
                //      따라서 `atten == 1`, `ndotl` 은 상수 → `Quantize(상수) == 상수`.
                //      **주광이 벽에 만들 수 있는 계단은 원리적으로 0개다.**
                //      실제 값(주광 방향 (-0.244, 0.616, -0.749), `_Steps 3`):
                //        좌벽 0 · 우벽 0 · 앞벽 0 · 천장 0 · 바닥 0.5 · 뒷벽 1.0
                //      — 여섯 면이 각각 한 가지 값. 계단이 아니라 **면 단위 단색**이다.
                //   ③ 앰비언트는 `m_AmbientMode: 3`(Flat) 이라 `SampleSH` 가 법선과
                //      무관한 상수다. 여기도 변화가 없다.
                //   ④ 남은 유일한 가변 조명은 점광 `CabinLight` 하나다. 그런데
                //      `pow(distanceAttenuation, 2.5)` 가 값을 짓눌러 `_Steps 3` 의 첫
                //      칸 경계(1/3)를 거의 못 넘는다. 좌벽에서 가장 가까운 지점
                //      (거리 1.2 m)조차 `ndotl · pow(atten, 2.5) ≈ 0.40` 이고,
                //      경계를 넘는 영역은 **반지름 0.30 m 의 원 하나**뿐이다.
                //      3.2 m 벽에서 경계 한 개 = 사람 눈에 계단이 아니다.
                //
                // 결론: 계단이 「메워진」 것이 아니라 **처음부터 그려지지 않았다.**
                // 고칠 축은 둘이고 둘 다 기본 꺼짐으로 넣어 뒀다 —
                //   · `_ShadeGamma`(≈3): ④의 눌린 값을 펴서 경계를 늘린다.
                //     예측치 — `_Steps 3`, `_ShadeGamma 3` 이면 좌벽에 경계가
                //     반지름 0.39 m 와 1.33 m 두 곳에 생긴다. 캡처로 검산 가능하다.
                //   · `_ColorDepth`: 최종 색을 N 단으로 끊어 평탄 구간을 만든다.
                // 그리고 ①②③ 은 셰이더가 고칠 수 없다 — **벽을 쪼개거나 광원을
                // 늘리는 것은 씬 소유자의 일이다.** 진단 8 번이 그 근거를 캡처로 낸다.
                //
                // ⚠ 림은 `QuantizeShade` 가 아니라 `Quantize` 를 그대로 쓴다. 림의 입력은
                // 이미 0~1 전 구간에 퍼져 있어 축을 재분배할 이유가 없고, 재분배하면
                // `_ShadeGamma` 한 손잡이가 조명 계단과 실루엣 계단을 동시에 움직여
                // 어느 쪽이 바뀌었는지 판정에서 분리되지 않는다.
                float3 rimTerm = Quantize(rim, _Steps) * _RimStrength * _ShadowTint.rgb;
                color += rimTerm;

                // 발광은 조명과 무관하게 **더한다**. 정화 점등은 어두운 칸에서도
                // 보여야 하고, 곱하면 그림자 쪽에서 사라진다.
                //
                // `_EMISSIONMAP_ON` 이 꺼진 기본 변형에서는 아래가 정확히
                // `color += _EmissionColor.rgb` 이다 — `MaterialPropertyBlock` 경로 불변.
                float3 emission = _EmissionColor.rgb;
                #if defined(_EMISSIONMAP_ON)
                    #if defined(_TRIPLANAR_ON)
                        emission *= SampleTriplanar(TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap),
                                                    input.positionWS, normalWS);
                    #else
                        emission *= SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb;
                    #endif
                #endif
                color += emission;

                // ════════════════════════════════════════════════════════════
                // **진단 출력. 기본 0 이면 아무것도 안 바뀐다.**
                //
                // 왜 여기로 옮겼는가: 종전에는 이 분기가 림·발광·안개 **앞**에 있어서
                // 「직접광만」을 골라도 그 위에 림과 발광이 다시 얹혔다. 즉 **진단 1·2 가
                // 순수한 항이 아니었다.** 무엇을 보고 있는지 모르는 진단은 진단이 아니다.
                // 이제는 최종 반환 직전에서 항을 통째로 갈아끼운다.
                //
                // ── 어떤 진단값을 어떤 캡처에서 보면 무엇이 갈리는가 ──────────
                //
                // **먼저 9 번을 찍는다.** 9 번은 셰이딩·재질·조명·기하와 **전혀 무관하게**
                // 화면 X 를 8 칸으로 끊어 칠한다. 1920 폭이면 한 칸이 240px, 960 폭이면
                // 120px 다. 그러므로 200px 주사선은 **경계를 최대 1 개만 지난다.**
                //   · 9 번 캡처의 평탄 구간이 ≤ 2 개, 최장 ≥ 100px → 캡처·측정 경로는 정상.
                //     계단이 안 보이는 원인은 **셰이더 안**에 있다. 6·7 번으로 내려간다.
                //   · 9 번 캡처에서도 평탄 구간이 수십~수백 개 → 계단을 **화면에 그린 뒤
                //     누군가 부순다.** 셰이더를 더 고쳐도 소용없다. 그때 의심 대상은
                //     포스트 처리·리샘플·측정기이지 이 파일이 아니다.
                //     (정적 조사로는 이미 전부 배제됐다 — 카메라 `m_RenderPostProcessing: 0`,
                //      씬에 `Volume` 0 개, `m_MSAA: 1`, `m_RenderScale: 1`, 캡처 RT
                //      `antiAliasing = 1`, `m_Fog: 0`. 그래도 **재보다.**)
                //
                // **8 번** — 벽 한 면이 통째로 한 가지 색이면 그 면의 법선은 상수다.
                //   상수 법선 위에서는 `dot(N, L)` 도 상수이고, 상수를 양자화하면 상수다.
                //   즉 **주광이 만들 수 있는 계단이 그 면에는 원리적으로 없다.**
                //   현재 씬의 벽은 전부 Unity 기본 Cube 한 장이라 8 번이 단색으로 나올 것이다.
                //
                // **6 번 vs 7 번** — 6 은 추가 광원 양자화 **입력**, 7 은 **출력**이다.
                //   · 6 이 부드러운 그라디언트이고 7 도 부드럽다 → 양자화가 일을 안 한다.
                //     원인은 입력이 첫 칸(0 ~ 1/3) 안에 몰려 있는 것이다. `_ShadeGamma` 를
                //     켜고 3 정도로 올린 뒤 7 번을 다시 찍으면 칸 경계가 나타나야 한다.
                //   · 6 이 부드럽고 7 이 뚜렷한 몇 칸이다 → 양자화는 정상이다.
                //     그런데 0 번(정상)에서 계단이 안 보인다면, 그 계단이 **다른 항에 묻힌
                //     것**이다. 1·2·3·4·5 로 항별 세기를 비교한다.
                //
                // **1·2·3·4·5** — 항별 분리. 2(앰비언트)가 화면 전체 단색이면 앰비언트는
                //   상수이고 계단을 메우는 범인이 아니다. 3(림)이 벽에서 가장 밝으면
                //   그 벽의 명암은 사실상 림이 만들고 있다는 뜻이다.
                // ════════════════════════════════════════════════════════════
                if (_DebugOutput > 0.5)
                {
                    float3 addTerm  = lit - mainTerm;
                    float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);

                    float  raw   = saturate(addRaw);
                    float  band  = saturate(addBand);
                    float  ref9  = floor(saturate(screenUV.x) * 8.0) / 7.0;

                    if      (_DebugOutput < 1.5) color = lit;                        // 1 직접광
                    else if (_DebugOutput < 2.5) color = ambientTerm;                // 2 앰비언트
                    else if (_DebugOutput < 3.5) color = rimTerm;                    // 3 림
                    else if (_DebugOutput < 4.5) color = addTerm;                    // 4 추가 광원
                    else if (_DebugOutput < 5.5) color = mainTerm;                   // 5 주광
                    else if (_DebugOutput < 6.5) color = float3(raw,  raw,  raw);    // 6 양자화 입력
                    else if (_DebugOutput < 7.5) color = float3(band, band, band);   // 7 양자화 출력
                    else if (_DebugOutput < 8.5) color = normalWS * 0.5 + 0.5;       // 8 월드 법선
                    else                         color = float3(ref9, ref9, ref9);   // 9 기준 계단
                    // 진단은 이 셰이더 안의 안개·색심도를 타지 않는다.
                    // **다만 포스트 처리는 여전히 탄다** — 그건 화면 전체 패스라
                    // 셰이더가 막을 수 없다. 그리고 그것이 9 번의 존재 이유다:
                    // 9 번이 흐트러지면 흐트러뜨린 것은 포스트 처리다.
                    return half4(color, 1.0);
                }

                // 색 심도 양자화 + 오더드 디더. **안개보다 뒤, 반환 직전**에 건다 —
                // PS1 의 프레임버퍼가 마지막이었기 때문이고, 앞에 걸면 안개가 다시
                // 칸 사이를 메워 버린다.
                color = MixFog(color, input.fogCoord);

                #if defined(_COLORDEPTH_ON)
                    if (_ColorDepth > 1.5)
                    {
                        color = QuantizeColorDepth(color, input.positionCS.xy);
                    }
                #endif

                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // 그림자 드리우기. 이게 없으면 이 셰이더를 쓴 물체가 그림자를 못 만든다.
        //
        // ⚠ 이 파스는 정점 스냅을 **적용하지 않는다.** 스냅은 카메라 클립 공간에서
        // 도는데 이 파스의 클립 공간은 광원 공간이라, 같은 격자를 걸면 그림자가
        // 화면 격자와 무관하게 흔들려 오히려 어긋난다. 대신 `_PS1DISTORT_ON` 을 켠
        // 머티리얼은 **자기 그림자가 정점 위치와 최대 1 격자만큼 어긋날 수 있다.**
        // 현재 씬은 주광 그림자가 꺼져 있어(`m_Shadows.m_Type: 0`) 관측되지 않는다.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);
                float4 clip = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    clip.z = min(clip.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    clip.z = max(clip.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.positionCS = clip;
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
