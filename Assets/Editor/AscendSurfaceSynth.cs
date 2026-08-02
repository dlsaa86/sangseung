using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Ascend.Prototype.EditorTools
{
    // <PURE>
    // ─────────────────────────────────────────────────────────────────────────────
    // 이 파일 전체가 **Unity 타입을 하나도 참조하지 않는다.** `AscendTextureGen.cs` 의
    // `<PURE>` 절과 같은 규약이다 — 에디터를 띄우지 않고 같은 소스를 그대로 컴파일해
    // PNG 를 뽑고 지표를 재기 위한 것이고, 그래야 「에디터에서 돌려야만 확인되는 통과」라는
    // 검증 불가능한 주장이 안 생긴다. Unity 껍데기는 `AscendSurfaceTextureGen.cs` 에만 있다.
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 산업 표면 텍스처 세트 12종 + 발광 마스크 2종을 절차적으로 만든다.
    ///
    /// ## 왜 또 만드나 — 기존 넷과의 관계
    ///
    /// <see cref="AscendTextureSynth"/> 의 넷(`TEX_Iron_Rust` 등)은 **뼈대이자 선례**다.
    /// 정수 해시 난수·정수 고정소수점 노이즈·무압축 deflate PNG 라는 결정론 규약을
    /// 그쪽이 세웠고, 이 파일은 그 규약과 그 코드를 **그대로 재사용한다**(노이즈·인코더 모두
    /// `AscendTextureSynth` 의 것을 부른다). 새로 만든 것은 규약이 아니라 **표면의 종류와
    /// 밝기 대역**이다.
    ///
    /// 기존 넷을 확장하지 않고 새 세트를 두는 이유는 하나다 — **밝기 대역이 다르다.**
    /// 기존 넷의 팔레트는 평균 휘도가 0.13~0.28 이다. `AscendStylized.shader` 는 `_BaseMap`
    /// 을 `_BaseColor` 에 **곱하고**, 씬의 `CarShell_*` 13장은 기본색이 0.24 언저리다.
    /// 0.24 × 0.20 = 0.048 — 어두운 텍스처를 어두운 기본색에 곱하면 재질이 통째로 검게
    /// 죽는다. 그래서 이 세트는 평균 휘도를 **0.45~0.75** 로 잡는다. 색은 텍스처가 나르고
    /// 기본색은 흰색 쪽으로 올리는 것이 이 셰이더에서 유일하게 성립하는 배선이다
    /// (배선 제안표는 파일 맨 아래).
    ///
    /// ## 통과선 — `docs/GRAPHICS_TARGET.md` §2 G-1
    ///
    /// 「텍스처를 배선했다」는 머티리얼을 세는 것이고 「화면에 텍스처가 보인다」는 화소를
    /// 세는 것이다. 이 저장소는 그 둘을 혼동해 두 번 실패했다. 그래서 여기서 만든 PNG 는
    /// <see cref="AscendSurfaceMetrics"/> 가 **파일을 다시 읽어** 8×8 블록 표준편차 중앙값을
    /// 재고, 12.0 에 못 미치면 그 텍스처는 실패로 찍힌다.
    ///
    /// ## 「저해상도 손그림 픽셀」을 무엇으로 만드는가
    ///
    /// 세 가지가 함께여야 한다 — ① 색을 12~24개로 못박는 팔레트 양자화,
    /// ② 그 양자화가 만드는 밴딩을 픽셀 그레인으로 바꾸는 **오더드 디더링**,
    /// ③ 기능에서 나온 결정론적 기하 스탬프(리벳·이음매·돌기·격자).
    /// ①만 하면 밴딩이 남고, ②만 하면 백색소음이 되고, ③이 없으면 「무늬」이지 「표면」이
    /// 아니다. `VISUAL_BIBLE.md` §2.3 의 판정 기준 — 확대하면 픽셀 그레인이 보이고,
    /// 실루엣과 조명은 3D 가 만든다 — 이 셋의 합이다.
    /// </summary>
    public static class AscendSurfaceSynth
    {
        /// <summary>알고리즘 판. 픽셀 규칙을 바꾸면 올린다.</summary>
        public const string AlgorithmId = "AscendSurface-v1";

        /// <summary>
        /// 산출 폴더. `AssetImportPaths.ManagedRoot` 아래이고 `/ui/`·`/vfx/`·`/normal` 어디에도
        /// 걸리지 않으므로 <c>TextureAssetCategory.World</c> 로 분류된다 — 벽·바닥·기계 표면이니
        /// 그게 맞다. 폴더 이름을 바꾸면 카테고리가 조용히 바뀐다.
        /// </summary>
        public const string OutputFolder = "Assets/Prototype_Elevator/Art/Textures/Generated";

        public enum Surface
        {
            FloorPlateRust = 0,
            WallPanelRiveted = 1,
            WallPaintPeeled = 2,
            GratingSteel = 3,
            ConcreteShaft = 4,
            PalletWood = 5,
            MachineHousing = 6,
            ConduitCable = 7,
            StencilWarning = 8,
            GaugeEnamel = 9,
            GlassSmudged = 10,
            FabricSack = 11,

            /// <summary>계기 눈금·파일럿 램프만 밝은 마스크. 나머지는 검정.</summary>
            GaugeEnamelEmissive = 12,

            /// <summary>기계 하우징의 표시창·경고등만 밝은 마스크.</summary>
            MachineHousingEmissive = 13,
        }

        public sealed class Spec
        {
            public readonly Surface Kind;
            public readonly string FileName;
            public readonly int Size;
            public readonly uint Seed;
            public readonly int[] Palette;

            /// <summary>월드 1m 당 이 텍스처가 반복되는 횟수 — 씬 소유자를 위한 권장값.</summary>
            public readonly float TilesPerMeter;

            /// <summary>이 텍스처가 덮으라고 만들어진 것. 배선 제안표의 근거다.</summary>
            public readonly string Purpose;

            /// <summary>발광 마스크인가. 참이면 밝기·색 수 통과선이 알베도와 다르다.</summary>
            public readonly bool IsEmissive;

            public Spec(Surface kind, string fileName, int size, uint seed, int[] palette,
                        float tilesPerMeter, string purpose, bool isEmissive)
            {
                Kind = kind;
                FileName = fileName;
                Size = size;
                Seed = seed;
                Palette = palette;
                TilesPerMeter = tilesPerMeter;
                Purpose = purpose;
                IsEmissive = isEmissive;
            }
        }

        // ── 팔레트 ────────────────────────────────────────────────────────────
        //
        // 전부 `VISUAL_BIBLE.md` §3 의 재질·색조 표 안이다 — 산화 철, 더러운 올리브,
        // 탁한 갈색, 목탄 검정, 탈색된 뼈색. 채도를 낮게 유지하되 **명도는 기존 넷보다
        // 높다**(위 클래스 주석의 이유). 적색 대역은 위험 신호 전용이라 계기 레드라인
        // 하나에만 쓴다(§3 「적색: 위험·경고 전용」).
        //
        // ⚠ **노란색을 쓰지 않는다.** 경고 스텐실은 바랜 뼈색과 바랜 주황으로만 만든다 —
        // 금색·황색 페인트라인 지적이 9라운드째다.

        /// <summary>선형 보간 램프. 정수 나눗셈이라 런타임과 무관하게 같은 값이 나온다.</summary>
        internal static int[] Ramp(int fromRgb, int toRgb, int steps)
        {
            if (steps < 1) throw new ArgumentOutOfRangeException("steps");
            var result = new int[steps];
            int fr = (fromRgb >> 16) & 255, fg = (fromRgb >> 8) & 255, fb = fromRgb & 255;
            int tr = (toRgb >> 16) & 255, tg = (toRgb >> 8) & 255, tb = toRgb & 255;
            int den = steps == 1 ? 1 : steps - 1;
            for (int i = 0; i < steps; i++)
            {
                int r = fr + (tr - fr) * i / den;
                int g = fg + (tg - fg) * i / den;
                int b = fb + (tb - fb) * i / den;
                result[i] = (r << 16) | (g << 8) | b;
            }
            return result;
        }

        internal static int[] Join(params int[][] parts)
        {
            int total = 0;
            for (int i = 0; i < parts.Length; i++) total += parts[i].Length;
            var result = new int[total];
            int at = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                Array.Copy(parts[i], 0, result, at, parts[i].Length);
                at += parts[i].Length;
            }
            return result;
        }

        // 각 팔레트의 **구간 경계가 곧 레시피의 계약**이다. 아래 상수들이 그 경계를
        // 이름으로 들고 있어야 레시피에서 매직 넘버가 사라진다.

        private const int FloorBase = 0, FloorBaseLen = 10;
        private const int FloorRust = 10, FloorRustLen = 4;
        private const int FloorSeam = 14, FloorHilite = 15;
        internal static readonly int[] FloorPlatePalette = Join(
            Ramp(0x565349, 0xC6C1B0, FloorBaseLen),          // 무쇠 판 — 어두운 면 → 닳아 밝은 면
            Ramp(0x6A4630, 0x8E6A50, FloorRustLen),          // 부식 얼룩 — 탁한 갈색(§3)
            new[] { 0x36352D, 0xD8D3C0 });                   // 이음매 / 돌기 윗면 하이라이트

        private const int PanelBase = 0, PanelBaseLen = 10;
        private const int PanelGrime = 10, PanelGrimeLen = 3;
        private const int PanelRust = 13, PanelSeam = 14, PanelRivet = 15;
        internal static readonly int[] WallPanelPalette = Join(
            Ramp(0x5A5A4E, 0xC2C0AA, PanelBaseLen),          // 강판
            Ramp(0x5E6046, 0x8A8A6C, PanelGrimeLen),         // 더러운 올리브 때(§3)
            new[] { 0x8A6050, 0x3B3A32, 0xD6D2BE });         // 녹 / 이음매 / 리벳 정수리

        private const int PaintBase = 0, PaintBaseLen = 8;
        private const int PaintMetal = 8, PaintMetalLen = 6;
        private const int PaintChip = 14, PaintBleed = 15;
        internal static readonly int[] WallPaintPalette = Join(
            Ramp(0x6E7154, 0xC2C3A4, PaintBaseLen),          // 바랜 올리브 도장
            Ramp(0x4A4C46, 0x929286, PaintMetalLen),         // 벗겨져 드러난 차콜 금속
            new[] { 0x33352F, 0x8A6050 });                   // 벗겨진 가장자리 / 녹물

        private const int GrateBar = 0, GrateBarLen = 8;
        private const int GrateVoid = 8, GrateVoidLen = 5;
        private const int GrateEdge = 13, GrateRust = 14, GrateGrime = 15;
        internal static readonly int[] GratingPalette = Join(
            Ramp(0x6A6A60, 0xCAC8B8, GrateBarLen),           // 격자 바 윗면
            Ramp(0x44443C, 0x747266, GrateVoidLen),          // 구멍 안쪽 — 완전 검정이 아니다(§6 「형태를 숨기지 않을 정도」)
            new[] { 0xDBD8C6, 0x8A6452, 0x585646 });

        private const int ConcreteBase = 0, ConcreteBaseLen = 12;
        private const int ConcreteStain = 12, ConcreteStainLen = 4;
        internal static readonly int[] ConcretePalette = Join(
            Ramp(0x6C6B62, 0xC6C4B6, ConcreteBaseLen),
            new[] { 0x585650, 0x7E7462, 0x8C8272, 0x4C4A44 });

        private const int WoodBase = 0, WoodBaseLen = 10;
        private const int WoodGrain = 10, WoodGrainLen = 2;
        private const int WoodStain = 12, WoodStainLen = 2;
        private const int WoodNail = 14, WoodEdge = 15;
        internal static readonly int[] WoodPalette = Join(
            Ramp(0x6E5335, 0xB49E80, WoodBaseLen),           // 얼룩진 목재(§3)
            new[] { 0x4A3722, 0x5B4429 },                    // 결 선
            new[] { 0x6E6A46, 0x3E301E },                    // 올리브 얼룩 / 짙은 얼룩
            new[] { 0x9C978A, 0xC0AE90 });                   // 못 대가리 / 모서리

        private const int HousingBase = 0, HousingBaseLen = 10;
        private const int HousingOil = 10, HousingOilLen = 2;
        private const int HousingOlive = 12, HousingOliveLen = 2;
        private const int HousingBolt = 14, HousingEdge = 15;
        internal static readonly int[] HousingPalette = Join(
            Ramp(0x5A5A55, 0xB2B2A8, HousingBaseLen),        // 무쇠
            new[] { 0x393832, 0x474437 },                    // 기름때
            new[] { 0x6A6C52, 0x8A8C6E },                    // 올리브 케이싱
            new[] { 0x7E7258, 0xC7C6B8 });                   // 낡은 황동 볼트 — 번들거리지 않는 값(국소 액센트만, 금지 5) / 모서리

        private const int ConduitPipe = 0, ConduitPipeLen = 9;
        private const int ConduitCableBase = 9, ConduitCableLen = 5;
        private const int ConduitRust = 14, ConduitTape = 15;
        internal static readonly int[] ConduitPalette = Join(
            Ramp(0x5E5D53, 0xBEBCAC, ConduitPipeLen),
            Ramp(0x37362F, 0x726F64, ConduitCableLen),
            new[] { 0x8A6050, 0x6E7052 });

        private const int StencilBase = 0, StencilBaseLen = 6;
        private const int StencilBone = 6, StencilBoneLen = 5;
        private const int StencilOrange = 11, StencilOrangeLen = 4;
        private const int StencilWear = 15;
        internal static readonly int[] StencilPalette = Join(
            Ramp(0x4E5046, 0x8C8E80, StencilBaseLen),        // 바탕 강판
            Ramp(0xA8A290, 0xDCD6C2, StencilBoneLen),        // 탈색된 뼈색(§3) — 노란색이 아니다
            Ramp(0x8E5040, 0xB07058, StencilOrangeLen),      // 바랜 산화철 주황 (색상각 ~13도 — 금색 축에서 내려온 값)
            new[] { 0x6E6A5A });

        private const int GaugeFace = 0, GaugeFaceLen = 8;
        private const int GaugeBezel = 8, GaugeBezelLen = 4;
        private const int GaugeScratch = 12, GaugeChip = 13, GaugeDial = 14, GaugeRed = 15;
        internal static readonly int[] GaugePalette = Join(
            Ramp(0x5A6058, 0xACB2A6, GaugeFaceLen),          // 에나멜 (차가운 회녹색 계열)
            Ramp(0x7E7A66, 0xC2BCA2, GaugeBezelLen),         // 베젤
            new[] { 0xD3D1C2, 0x3C403A, 0xC6C0AC, 0x9E4A3A });// 긁힘 / 깨진 자리 / 다이얼 면 / 레드라인(위험 전용)

        private const int GlassBase = 0, GlassBaseLen = 8;
        private const int GlassSmudgeIdx = 8, GlassSmudgeLen = 4;
        private const int GlassPrint = 12, GlassDrip = 13, GlassGlare = 14, GlassDust = 15;
        internal static readonly int[] GlassPalette = Join(
            Ramp(0x6E7A72, 0xBAC4BA, GlassBaseLen),          // 오염된 유리(§3)
            Ramp(0x82887A, 0xA2A89C, GlassSmudgeLen),
            new[] { 0xADB4A9, 0x7E8878, 0xCCD4CA, 0x8E9488 });

        private const int FabricBase = 0, FabricBaseLen = 9;
        private const int FabricWeave = 9, FabricWeaveLen = 2;
        private const int FabricStain = 11, FabricStainLen = 3;
        private const int FabricPatch = 14, FabricThread = 15;
        internal static readonly int[] FabricPalette = Join(
            Ramp(0x7A6B4E, 0xCCBB92, FabricBaseLen),         // 자루 천
            new[] { 0x584C36, 0x6A5C42 },                    // 씨실·날실 그늘
            new[] { 0x8A7A52, 0x463C2A, 0x7E6E4E },          // 얼룩 3종
            new[] { 0x6E6C4A, 0xD8C9A0 });                   // 덧댄 천 / 밝은 실

        /// <summary>
        /// 발광 마스크 팔레트. 0번이 **정확히 검정**이어야 한다 —
        /// 곱하든 더하든 「발광이 없는 곳」이 0 이 아니면 면 전체가 뜬다.
        /// </summary>
        internal static readonly int[] EmissivePalette = Join(
            new[] { 0x000000 },
            Ramp(0x241E12, 0xF2E6C2, 6),                     // 백열 눈금 — 탁한 따뜻함(§6)
            Ramp(0x3A1410, 0xC8543C, 3));                    // 경고등 — 적색은 위험 전용(§3)

        /// <summary>
        /// 세트 정의. 해상도는 **256 또는 128** 이다 — PS1~초기 PS2 감각이고
        /// `GRAPHICS_TARGET.md` §4 「폴리곤 예산 상향 없음」과 같은 방향이다.
        ///
        /// 파일명에 `_n`·`_ui`·`_vfx` 접미사를 쓰지 않는다 — 쓰면 임포트 카테고리가 바뀐다.
        /// 발광 마스크는 `_Emis` 로 끝나는데, 이 접미사는 어느 분류 규칙에도 걸리지 않으므로
        /// World 로 남는다(의도한 것이다 — 마스크도 월드 표면 크기다).
        /// </summary>
        public static Spec[] Specs()
        {
            return new[]
            {
                new Spec(Surface.FloorPlateRust, "TEX_FloorPlate_Rust.png", 256, 0x5B100001u,
                    FloorPlatePalette, 0.75f, "캐빈 바닥 철판 — 부식 얼룩 + 미끄럼 방지 돌기", false),
                new Spec(Surface.WallPanelRiveted, "TEX_WallPanel_Riveted.png", 256, 0x5B100002u,
                    WallPanelPalette, 0.50f, "캐빈 벽 강판 — 이음매와 리벳 열", false),
                new Spec(Surface.WallPaintPeeled, "TEX_WallPaint_Peeled.png", 256, 0x5B100003u,
                    WallPaintPalette, 0.50f, "도색이 벗겨진 벽 — 바랜 올리브 위 차콜 금속", false),
                new Spec(Surface.GratingSteel, "TEX_Grating_Steel.png", 256, 0x5B100004u,
                    GratingPalette, 1.00f, "바닥 그레이팅 — 금속 격자", false),
                new Spec(Surface.ConcreteShaft, "TEX_Concrete_Shaft.png", 256, 0x5B100005u,
                    ConcretePalette, 0.40f, "승강로·로비 콘크리트 벽", false),
                new Spec(Surface.PalletWood, "TEX_Pallet_Wood.png", 256, 0x5B100006u,
                    WoodPalette, 1.00f, "화물 팔레트·상자 — 얼룩진 목재", false),
                new Spec(Surface.MachineHousing, "TEX_Machine_Housing.png", 256, 0x5B100007u,
                    HousingPalette, 1.50f, "기계 하우징 — 무쇠와 기름때", false),
                new Spec(Surface.ConduitCable, "TEX_Conduit_Cable.png", 128, 0x5B100008u,
                    ConduitPalette, 2.00f, "배관·케이블 표면", false),
                new Spec(Surface.StencilWarning, "TEX_Stencil_Warning.png", 256, 0x5B100009u,
                    StencilPalette, 0.50f, "경고 스텐실 사선 — 바랜 뼈색/주황 (노란색 아님)", false),
                new Spec(Surface.GaugeEnamel, "TEX_Gauge_Enamel.png", 256, 0x5B10000Au,
                    GaugePalette, 2.00f, "계기 패널 면 — 에나멜과 긁힘", false),
                new Spec(Surface.GlassSmudged, "TEX_Glass_Smudged.png", 128, 0x5B10000Bu,
                    GlassPalette, 1.00f, "통관 유리 — 얼룩과 지문", false),
                new Spec(Surface.FabricSack, "TEX_Fabric_Sack.png", 128, 0x5B10000Cu,
                    FabricPalette, 2.00f, "천·자루 — 승객 의복과 화물 덮개", false),

                new Spec(Surface.GaugeEnamelEmissive, "TEX_Gauge_Enamel_Emis.png", 256, 0x5B10000Du,
                    EmissivePalette, 2.00f, "계기 눈금·파일럿 램프 발광 마스크", true),
                new Spec(Surface.MachineHousingEmissive, "TEX_Machine_Housing_Emis.png", 256, 0x5B10000Eu,
                    EmissivePalette, 1.50f, "기계 하우징 표시창·경고등 발광 마스크", true),
            };
        }

        // ── 합성 진입점 ───────────────────────────────────────────────────────

        public static byte[] PaintIndices(Spec spec)
        {
            int size = spec.Size;
            var buffer = new byte[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    buffer[y * size + x] = (byte)Paint(spec, x, y);
            return buffer;
        }

        public static byte[] Encode(Spec spec)
        {
            byte[] indices = PaintIndices(spec);
            return AscendTextureSynth.EncodeIndexed(spec.Size, spec.Size, indices, spec.Palette,
                                                    MetaText(spec));
        }

        internal static string MetaText(Spec spec)
        {
            // 문화권을 불변으로 고정한다 — 이 문자열이 파일 바이트가 되고, 로케일이 다른
            // 기기에서 바이트가 달라지는 종류의 실패는 원인을 찾는 데 하루가 든다.
            CultureInfo c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(200);
            sb.Append("algo=").Append(AlgorithmId);
            sb.Append(";surface=").Append(spec.Kind.ToString());
            sb.Append(";seed=0x").Append(spec.Seed.ToString("X8", c));
            sb.Append(";size=").Append(spec.Size.ToString(c)).Append('x').Append(spec.Size.ToString(c));
            sb.Append(";colors=").Append(spec.Palette.Length.ToString(c));
            sb.Append(";tilesPerMeter=").Append(spec.TilesPerMeter.ToString("0.00", c));
            return sb.ToString();
        }

        private static int Paint(Spec spec, int x, int y)
        {
            int size = spec.Size;
            uint seed = spec.Seed;
            switch (spec.Kind)
            {
                case Surface.FloorPlateRust: return PaintFloorPlate(x, y, size, seed);
                case Surface.WallPanelRiveted: return PaintWallPanel(x, y, size, seed);
                case Surface.WallPaintPeeled: return PaintWallPaint(x, y, size, seed);
                case Surface.GratingSteel: return PaintGrating(x, y, size, seed);
                case Surface.ConcreteShaft: return PaintConcrete(x, y, size, seed);
                case Surface.PalletWood: return PaintWood(x, y, size, seed);
                case Surface.MachineHousing: return PaintHousing(x, y, size, seed);
                case Surface.ConduitCable: return PaintConduit(x, y, size, seed);
                case Surface.StencilWarning: return PaintStencil(x, y, size, seed);
                case Surface.GaugeEnamel: return PaintGauge(x, y, size, seed);
                case Surface.GlassSmudged: return PaintGlass(x, y, size, seed);
                case Surface.FabricSack: return PaintFabric(x, y, size, seed);
                case Surface.GaugeEnamelEmissive: return PaintGaugeEmissive(x, y, size, seed);
                default: return PaintHousingEmissive(x, y, size, seed);
            }
        }

        // ── 레시피 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 녹슨 철판 바닥. 32px 격자의 마름모 돌기(체커플레이트)가 기능이고, 부식은
        /// 보로노이 얼룩으로 **덩어리지게** 들어간다 — 녹은 균일하게 퍼지지 않는다.
        /// 돌기를 마지막에 그리는 이유는 `AscendTextureSynth.PaintIron` 과 같다:
        /// 녹이 덮으면 판독이 먼저 사라진다(`VISUAL_BIBLE.md` §4 금지 10).
        /// </summary>
        private static int PaintFloorPlate(int x, int y, int size, uint seed)
        {
            int v = Fbm(x, y, 32, 32, size, seed, 3);
            v = Blend(v, Fbm(x, y, 4, 4, size, seed + 17u, 2), 18000);
            v = Blend(v, Grain(x, y, size, seed + 91u), 11000);
            int idx = FloorBase + Level(v, FloorBaseLen, x, y, 5200);

            // 부식 — 보로노이 F1 이 작을수록(중심에 가까울수록) 짙다.
            //
            // **얼룩 크기와 빈도를 한 번 줄였다.** 셀 8개(32px) · 문턱 22000 판본은 부식이
            // 판 면적의 절반 가까이를 덮어 「녹슨 철판」이 아니라 **위장무늬**로 읽혔다.
            // 마름모 돌기가 그 아래로 사라지는 것이 특히 나빴다 — 돌기는 미끄럼 방지라는
            // 기능이고, 기능이 마모에 묻히면 `VISUAL_BIBLE.md` §4 금지 10(작은 디테일로
            // 실루엣을 흐림)의 텍스처판이 된다. 셀을 16개(16px)로 잘게 쪼개고 문턱을
            // 올려서 **점점이 앉은 부식**으로 바꾼다.
            int rustCell;
            int rust = VoronoiF1(x, y, 16, size, seed + 401u, out rustCell);
            int rustBias = Fbm(x, y, 16, 16, size, seed + 402u, 2);
            if (rust < 15000 && rustBias > 34000)
            {
                int deep = 65535 - rust * 3;
                if (deep < 0) deep = 0;
                idx = FloorRust + Level(Blend(deep, Grain(x, y, size, seed + 403u), 26000),
                                        FloorRustLen, x, y, 9000);
            }

            // 판 이음매 — 128px 판 두 장.
            int mx = x & 127, my = y & 127;
            if (mx < 2 || my < 2) idx = FloorSeam;

            // 마름모 돌기. 32px 칸마다 방향이 바뀌어 체커플레이트가 된다.
            int cx = x & 31, cy = y & 31;
            bool flip = (((x >> 5) + (y >> 5)) & 1) == 0;
            int band = flip ? ((cx + cy) & 31) : ((cx - cy) & 31);
            if (cx >= 3 && cx <= 28 && cy >= 3 && cy <= 28)
            {
                if (band == 12) idx = FloorHilite;                 // 돌기 윗면
                else if (band >= 13 && band <= 16) idx = FloorBase + FloorBaseLen - 3;
                else if (band == 17) idx = FloorSeam;              // 돌기 그림자
            }
            return idx;
        }

        /// <summary>
        /// 리벳 벽 패널. 64px 판 + 판마다 리벳 네 개 + 세로로 흘러내린 녹물.
        /// 「장식이 아니라 기능과 마모가 먼저 보임」(§4 금지 9의 반대편)을 텍스처에서 만든다.
        /// </summary>
        private static int PaintWallPanel(int x, int y, int size, uint seed)
        {
            // 이 텍스처가 `CarShell_*` 13장 중 6장을 덮을 후보라 **화면 국소 분산의
            // 대부분을 혼자 정한다.** 그래서 그레인 가중치를 다른 면보다 높게 잡는다.
            int v = Fbm(x, y, 32, 32, size, seed, 3);
            v = Blend(v, Fbm(x, y, 4, 8, size, seed + 21u, 2), 22000);
            v = Blend(v, Grain(x, y, size, seed + 55u), 21000);
            int idx = PanelBase + Level(v, PanelBaseLen, x, y, 8200);

            int grime = Fbm(x, y, 16, 16, size, seed + 131u, 2);
            if (grime > 44000)
                idx = PanelGrime + Level(Blend((grime - 44000) * 3, Grain(x, y, size, seed + 132u), 22000),
                                         PanelGrimeLen, x, y, 12000);

            // 녹물은 **세로로 흘러야** 중력이 읽힌다 — 가로로 잘고 세로로 긴 셀.
            int drip = Fbm(x, y, 4, 64, size, seed + 211u, 3);
            if (drip > 52000) idx = PanelRust;

            int mx = x & 63, my = y & 63;
            if (mx < 2 || my < 2) idx = PanelSeam;
            else if (mx == 2 || my == 2) idx = PanelBase + 1;

            idx = Rivet(mx, my, 10, 10, idx);
            idx = Rivet(mx, my, 53, 10, idx);
            idx = Rivet(mx, my, 10, 53, idx);
            idx = Rivet(mx, my, 53, 53, idx);
            return idx;
        }

        private static int Rivet(int mx, int my, int cx, int cy, int current)
        {
            int dx = mx - cx, dy = my - cy;
            int d2 = dx * dx + dy * dy;
            if (d2 > 9) return current;
            if (d2 <= 2) return PanelRivet;             // 정수리
            return dy > 0 ? PanelSeam : PanelBase + 6;  // 아래는 그림자, 위는 밝은 면
        }

        /// <summary>
        /// 도색이 벗겨진 벽. **두 층**이다 — 위층은 바랜 올리브 도장, 아래층은 차콜 금속.
        /// 보로노이가 벗겨진 조각의 경계를 만들고, 경계선 한 픽셀만 어둡게 둬서
        /// 「칠이 들뜬 가장자리」가 읽히게 한다.
        /// </summary>
        private static int PaintWallPaint(int x, int y, int size, uint seed)
        {
            int cell;
            int f1 = VoronoiF1(x, y, 8, size, seed + 71u, out cell);
            int wear = Fbm(x, y, 32, 32, size, seed + 72u, 3);

            // 어느 조각이 벗겨졌는가는 조각 자신의 해시가 정한다 — 시드가 같으면 늘 같다.
            bool stripped = (cell & 0xFFFF) > 40000 && wear > 24000;

            int v = Fbm(x, y, 16, 16, size, seed, 3);
            v = Blend(v, Fbm(x, y, 2, 2, size, seed + 5u, 1), 22000);
            // **그레인 가중치가 이 텍스처의 G-1 을 통째로 결정한다.** 13000 에서는
            // 8×8 블록 표준편차 중앙값이 10.20 으로 통과선(12.0)에 못 미쳤다.
            // 도색면은 구조가 없는 대신 알갱이가 유일한 국소 분산원이기 때문이다.
            v = Blend(v, Grain(x, y, size, seed + 6u), 26000);

            int idx;
            if (stripped) idx = PaintMetal + Level(v, PaintMetalLen, x, y, 9000);
            else idx = PaintBase + Level(v, PaintBaseLen, x, y, 9500);

            // 조각 경계 — 들뜬 칠의 가장자리.
            if (!stripped && f1 > 42000 && wear > 20000) idx = PaintChip;

            // 칠 표면의 잔 흠집. 구조가 거의 없는 면이라 이것까지 있어야
            // 「도색된 금속」이지 「색면」이 아니다.
            int nick = Grain(x, y, size, seed + 313u);
            if (nick < 2200) idx = PaintChip;
            else if (nick > 63400) idx = stripped ? PaintMetal + PaintMetalLen - 1 : PaintBase + PaintBaseLen - 1;

            int scratch = ScratchHit(x, y, size, seed + 314u, 8);
            if (scratch == 2) idx = stripped ? PaintMetal + PaintMetalLen - 1 : PaintChip;

            int bleed = Fbm(x, y, 4, 64, size, seed + 311u, 3);
            if (bleed > 55000) idx = PaintBleed;
            return idx;
        }

        /// <summary>
        /// 바닥 그레이팅. 16px 세로 바 + 64px 가로 크로스바. 구멍은 **검정이 아니라
        /// 어두운 회색 램프**다 — §6 「대기광은 형태를 숨기지 않을 정도로 유지」와 같은 이유로,
        /// 완전히 검은 구멍은 그늘에서 판 전체를 검은 덩어리로 만든다.
        /// </summary>
        private static int PaintGrating(int x, int y, int size, uint seed)
        {
            int bx = x & 15, by = y & 63;
            bool bar = bx < 11;
            bool cross = by < 7;

            int v = Fbm(x, y, 8, 8, size, seed, 2);
            v = Blend(v, Grain(x, y, size, seed + 3u), 16000);

            int idx;
            if (bar || cross) idx = GrateBar + Level(v, GrateBarLen, x, y, 6000);
            else idx = GrateVoid + Level(v, GrateVoidLen, x, y, 7000);

            // 바 가장자리 — 위/왼쪽은 밝고 아래/오른쪽은 어둡다. 격자가 입체로 읽힌다.
            if (bar && (bx == 0 || (cross && by == 0))) idx = GrateEdge;
            if (bar && bx == 10) idx = GrateVoid;
            if (cross && by == 6) idx = GrateVoid;

            int rust = Fbm(x, y, 4, 32, size, seed + 77u, 3);
            if (rust > 55000 && (bar || cross)) idx = GrateRust;

            int grime = Fbm(x, y, 32, 32, size, seed + 78u, 2);
            if (grime > 52000 && !bar && !cross) idx = GrateGrime;
            return idx;
        }

        /// <summary>
        /// 콘크리트 승강로 벽. 구조가 없는 대신 **다중 스케일 얼룩**만으로 밀도를 만든다.
        /// 거푸집 자국을 128px 간격 세로선으로 아주 약하게 남긴다 — 「기능과 마모」의 흔적이다.
        /// </summary>
        private static int PaintConcrete(int x, int y, int size, uint seed)
        {
            int v = Fbm(x, y, 64, 64, size, seed, 4);
            v = Blend(v, Fbm(x, y, 8, 8, size, seed + 11u, 2), 26000);
            v = Blend(v, Grain(x, y, size, seed + 12u), 15000);
            int idx = ConcreteBase + Level(v, ConcreteBaseLen, x, y, 5000);

            // **세로 셀은 size 를 나눠야 한다.** 여기 48 을 쓴 판본이 타일링을 깼다 —
            // `AscendTextureSynth.Noise` 는 `size / cellY` 를 격자 주기로 삼는데
            // 256/48 = 5(정수 나눗셈)라 격자가 경계에서 어긋난다. 64 는 나눈다.
            int stain = Fbm(x, y, 4, 64, size, seed + 201u, 3);
            if (stain > 54000) idx = ConcreteStain + (stain > 59000 ? 1 : 0);

            int pit = Grain(x, y, size, seed + 202u);
            if (pit < 1400) idx = ConcreteStain + 3;
            else if (pit > 64200) idx = ConcreteBase + ConcreteBaseLen - 1;

            // 거푸집 이음매.
            if ((x & 127) == 0) idx = ConcreteStain + 3;
            else if ((x & 127) == 1) idx = ConcreteStain + 2;
            return idx;
        }

        /// <summary>
        /// 얼룩진 목재(화물 팔레트). 32px 세로 판자, 결은 세로로 길게, 판자마다 옹이 하나.
        /// 옹이 위치는 판자 첨자의 해시라 판자마다 다르되 시드가 같으면 늘 같은 자리다.
        /// </summary>
        private static int PaintWood(int x, int y, int size, uint seed)
        {
            int inPlank = x & 31;

            int v = Fbm(x, y, 2, 64, size, seed, 4);
            v = Blend(v, Fbm(x, y, 16, 16, size, seed + 9u, 2), 18000);
            v = Blend(v, Grain(x, y, size, seed + 10u), 12000);
            int idx = WoodBase + Level(v, WoodBaseLen, x, y, 5400);

            int grain = Fbm(x, y, 2, 32, size, seed + 33u, 3);
            if (grain < 16000) idx = WoodGrain + (grain < 9000 ? 0 : 1);

            // 얼룩 문턱을 올렸다. 50000 판본은 올리브 얼룩이 판자를 가로지르는 **큰 초록
            // 덩어리**로 앉아 목재보다 먼저 읽혔다 — §3 이 목재에 요구하는 것은 「얼룩진」
            // 이지 「초록 반점」이 아니다.
            int stain = Fbm(x, y, 32, 32, size, seed + 77u, 2);
            if (stain > 56000) idx = WoodStain + 0;
            if (stain > 61000) idx = WoodStain + 1;

            int knotY = AscendTextureSynth.Lattice(CellIndex(x, 5, size), 7, seed + 31u) % size;
            int kdx = inPlank - 16;
            int kdy = WrapDelta(y - knotY, size);
            int kd = AscendTextureSynth.Isqrt(kdx * kdx * 3 + kdy * kdy);
            if (kd < 8) idx = (kd & 1) == 0 ? WoodGrain : WoodBase + 3;

            // 판자 사이 틈과 못.
            if (inPlank == 0) idx = WoodGrain;
            else if (inPlank == 1) idx = WoodEdge;
            if (((y & 63) == 12 || (y & 63) == 51) && (inPlank == 8 || inPlank == 23)) idx = WoodNail;
            return idx;
        }

        /// <summary>
        /// 기계 하우징. 32px 볼트 격자 + 올리브 케이싱 패널 + **세로로 흘러내린 기름때**.
        /// 황동 볼트는 국소 액센트로만 쓴다 — §4 금지 5 는 재질로서의 황동이 아니라
        /// 기능 없는 황동 장식을 금지한다. 볼트는 기능이다.
        /// </summary>
        private static int PaintHousing(int x, int y, int size, uint seed)
        {
            int v = Fbm(x, y, 16, 16, size, seed, 3);
            v = Blend(v, Fbm(x, y, 4, 4, size, seed + 13u, 2), 20000);
            v = Blend(v, Grain(x, y, size, seed + 14u), 12000);
            int idx = HousingBase + Level(v, HousingBaseLen, x, y, 5600);

            // 케이싱 패널 — 64px 칸 중 절반쯤이 올리브다.
            int panel = AscendTextureSynth.Lattice(CellIndex(x, 6, size), CellIndex(y, 6, size), seed + 501u);
            if (panel > 36000)
                idx = HousingOlive + (Level(v, 2, x, y, 9000));

            int oil = Fbm(x, y, 4, 64, size, seed + 601u, 3);
            if (oil > 55000) idx = HousingOil + (oil > 60000 ? 0 : 1);

            // **긁힘은 닳은 금속이지 하이라이트가 아니다.** `HousingEdge`(0xC7C6B8) 를
            // 심으로 쓴 판본은 흰 사선이 면 위에 떠 보였고, 그건 §4 금지 21
            // 「화면 전체가 반짝이는 금속 glint」와 같은 실패를 텍스처에서 만든다.
            // 램프 안쪽 값으로 내리고 개수도 줄인다.
            int scratch = ScratchHit(x, y, size, seed + 701u, 5);
            if (scratch == 2) idx = HousingBase + HousingBaseLen - 1;
            else if (scratch == 1) idx = HousingBase + HousingBaseLen - 3;

            int mx = x & 31, my = y & 31;
            if (mx < 1 || my < 1) idx = HousingOil;
            int bd = (mx - 16) * (mx - 16) + (my - 16) * (my - 16);
            if (bd <= 2) idx = HousingBolt;
            else if (bd <= 6) idx = HousingEdge;
            return idx;
        }

        /// <summary>
        /// 배관·케이블. 32px 주기의 원통 음영(가로 위치에 따른 램프)이 파이프이고,
        /// 그 사이에 케이블 다발이 지나간다. 원통 음영을 **계단으로** 끊는 것이
        /// 셰이더의 `Quantize` 와 같은 방향이다 — 표면 자체가 이미 저해상도여야 한다.
        /// </summary>
        private static int PaintConduit(int x, int y, int size, uint seed)
        {
            int px = x & 31;
            bool cable = px >= 22;

            int idx;
            if (cable)
            {
                // 케이블 다발 — 3가닥, 각각 3px.
                int c = (px - 22) % 3;
                int shade = c == 0 ? 52000 : (c == 1 ? 30000 : 12000);
                shade = Blend(shade, Grain(x, y, size, seed + 3u), 14000);
                idx = ConduitCableBase + Level(shade, ConduitCableLen, x, y, 8000);
            }
            else
            {
                // 원통 — 왼쪽에서 빛이 든다고 가정한 램프.
                int lit = px < 11 ? px * 5900 : (21 - px) * 4200 + 6000;
                if (lit > 65535) lit = 65535;
                lit = Blend(lit, Fbm(x, y, 8, 32, size, seed, 2), 22000);
                lit = Blend(lit, Grain(x, y, size, seed + 1u), 12000);
                idx = ConduitPipe + Level(lit, ConduitPipeLen, x, y, 6000);
            }

            int rust = Fbm(x, y, 8, 16, size, seed + 91u, 3);
            if (rust > 53000) idx = ConduitRust;

            // 배관 조인트 밴드 + 테이프.
            int by = y & 63;
            if (!cable && (by == 30 || by == 33)) idx = ConduitPipe + ConduitPipeLen - 1;
            if (!cable && by == 31) idx = ConduitPipe + 1;
            if (!cable && by == 32) idx = ConduitPipe + ConduitPipeLen - 2;
            if (cable && (by >= 44 && by <= 48)) idx = ConduitTape;
            return idx;
        }

        /// <summary>
        /// 경고 스텐실 사선. **노란색을 쓰지 않는다** — 바랜 뼈색과 바랜 주황의 교대다.
        /// 스텐실이므로 가장자리가 깔끔하지 않아야 하고(붓 자국 노이즈), 페인트가
        /// 군데군데 벗겨져 바탕 강판이 드러난다.
        ///
        /// 사선 주기는 32px 이고 `(x+y)>>4` 의 홀짝을 쓴다 — x 로 256 을 이동하면 첨자가
        /// 16 만큼 변해 홀짝이 보존되므로 **좌우·상하 모두 이음매가 없다.**
        /// </summary>
        private static int PaintStencil(int x, int y, int size, uint seed)
        {
            int v = Fbm(x, y, 16, 16, size, seed, 3);
            v = Blend(v, Grain(x, y, size, seed + 2u), 14000);
            int idx = StencilBase + Level(v, StencilBaseLen, x, y, 6000);

            // 붓 자국으로 사선 경계를 흔든다 — 자를 대고 그은 선은 스텐실이 아니다.
            int jitter = (Fbm(x, y, 8, 8, size, seed + 5u, 2) - 32768) / 9000; // 약 ±3px
            int band = ((x + y + jitter) >> 4) & 1;

            int paint = Fbm(x, y, 8, 8, size, seed + 601u, 3);
            bool worn = paint < 15000;    // 벗겨진 자리 — 바탕이 드러난다

            if (!worn)
            {
                int shade = Blend(Fbm(x, y, 4, 4, size, seed + 7u, 2), Grain(x, y, size, seed + 8u), 22000);
                idx = band == 0
                    ? StencilBone + Level(shade, StencilBoneLen, x, y, 9000)
                    : StencilOrange + Level(shade, StencilOrangeLen, x, y, 9000);
            }

            int wear = Grain(x, y, size, seed + 909u);
            if (wear < 1600) idx = StencilWear;
            return idx;
        }

        /// <summary>
        /// 계기 패널 면. 64px 칸마다 다이얼 하나 + 베젤, 나머지는 에나멜 면.
        /// 다이얼 안에 눈금 12 칸과 레드라인 한 칸이 있다 — §4 금지 20 「작은 글자」를
        /// 피해 **숫자를 쓰지 않고 눈금만** 둔다.
        /// </summary>
        private static int PaintGauge(int x, int y, int size, uint seed)
        {
            int v = Fbm(x, y, 32, 32, size, seed, 3);
            v = Blend(v, Fbm(x, y, 4, 4, size, seed + 15u, 2), 16000);
            v = Blend(v, Grain(x, y, size, seed + 16u), 11000);
            int idx = GaugeFace + Level(v, GaugeFaceLen, x, y, 5200);

            int mx = x & 63, my = y & 63;
            int dx = mx - 32, dy = my - 32;
            int d = AscendTextureSynth.Isqrt(dx * dx + dy * dy);

            if (d <= 26)
            {
                if (d >= 22) idx = GaugeBezel + Level(v, GaugeBezelLen, x, y, 8000);
                else if (d >= 20) idx = GaugeChip;
                else
                {
                    idx = GaugeDial;
                    // 눈금 — 각도 대신 **8방향 대각 격자**로 근사한다. 원호 계산이 없으므로
                    // 부동소수가 끼어들 자리도 없다.
                    if (d >= 14)
                    {
                        bool tick = (dx == 0) || (dy == 0) || (dx == dy) || (dx == -dy)
                                 || (dx * 2 == dy) || (dy * 2 == dx)
                                 || (dx * 2 == -dy) || (dy * 2 == -dx);
                        if (tick) idx = GaugeChip;
                    }
                    // 레드라인 — 우상단 사분면 바깥쪽만. 위험은 색 하나로 말하지 않는다(금지 15):
                    // 여기서는 **위치(바깥쪽 끝)와 색**의 둘이다.
                    if (d >= 16 && dx > 0 && dy < 0 && dx > -dy) idx = GaugeRed;
                }
            }
            else
            {
                int scratch = ScratchHit(x, y, size, seed + 801u, 9);
                if (scratch == 2) idx = GaugeScratch;
                else if (scratch == 1) idx = GaugeFace + GaugeFaceLen - 2;
                if (mx < 1 || my < 1) idx = GaugeChip;
            }
            return idx;
        }

        /// <summary>
        /// 통관 유리. 세로로 흘러내린 자국 + 지문 링 + 먼지. 색상각이 전부 회녹색
        /// 대역이라 §2.1 「차가운 회녹색」이 이 한 장에 들어 있다.
        /// </summary>
        private static int PaintGlass(int x, int y, int size, uint seed)
        {
            int v = Fbm(x, y, 16, 16, size, seed, 3);
            v = Blend(v, Grain(x, y, size, seed + 4u), 13000);
            int idx = GlassBase + Level(v, GlassBaseLen, x, y, 6400);

            int smudge = Fbm(x, y, 8, 8, size, seed + 121u, 2);
            if (smudge > 40000)
                idx = GlassSmudgeIdx + Level((smudge - 40000) * 2, GlassSmudgeLen, x, y, 12000);

            // 지문 — 32px 칸 중 일부에 동심 링.
            int mx = x & 31, my = y & 31;
            int fx = mx - 16, fy = my - 16;
            int fd = AscendTextureSynth.Isqrt(fx * fx + fy * fy);
            int print = AscendTextureSynth.Lattice(CellIndex(x, 5, size), CellIndex(y, 5, size), seed + 131u);
            if (print > 46000 && fd < 12 && (fd & 1) == 0) idx = GlassPrint;

            int drip = Fbm(x, y, 2, 64, size, seed + 141u, 3);
            if (drip > 54000) idx = GlassDrip;

            int dust = Grain(x, y, size, seed + 151u);
            if (dust < 2000) idx = GlassDust;
            else if (dust > 63800) idx = GlassGlare;
            return idx;
        }

        /// <summary>
        /// 천·자루. 씨실과 날실이 2px 주기로 교차하는 격자가 밑바탕이고, 그 위에
        /// 얼룩과 덧댄 천이 온다. 2px 격자가 바로 「손그림 픽셀」의 그레인 원천이다.
        /// </summary>
        private static int PaintFabric(int x, int y, int size, uint seed)
        {
            int v = Fbm(x, y, 16, 16, size, seed, 3);
            v = Blend(v, Fbm(x, y, 4, 4, size, seed + 19u, 2), 20000);
            v = Blend(v, Grain(x, y, size, seed + 20u), 14000);
            int idx = FabricBase + Level(v, FabricBaseLen, x, y, 6000);

            // 직조 — 가로실이 위로 올라온 칸과 세로실이 위로 올라온 칸이 2px 주기로 교대한다.
            //
            // **극단값으로 튀지 않고 램프 안에서 흔든다.** 처음엔 실을 가장 밝은 색,
            // 그늘을 가장 어두운 색으로 찍었는데 8×8 표준편차가 43.8 까지 올라갔다.
            // 통과선(12)의 세 배가 넘는 것은 「밀도가 높다」가 아니라 **백색소음**이고,
            // 그 상태에서는 관측 색이 11개로 오히려 줄었다 — 중간 계조가 전부 뛰어넘어졌기
            // 때문이다. 램프 첨자를 ±2 흔들면 알갱이는 남고 계조는 살아난다.
            int level = idx - FabricBase;
            bool warp = ((((x >> 1) + (y >> 1)) & 1) == 0);
            if (warp) level += ((y & 1) == 0) ? 2 : -1;
            else level += ((x & 1) == 0) ? -2 : 1;
            if (level < 0) idx = FabricWeave + 1;
            else if (level >= FabricBaseLen) idx = FabricThread;
            else idx = FabricBase + level;

            // 실 사이로 비치는 가장 깊은 그늘 — 네 칸에 하나꼴.
            if (warp && (y & 1) == 1 && (x & 1) == 1 && level <= 1) idx = FabricWeave;

            int stain = Fbm(x, y, 32, 32, size, seed + 161u, 3);
            if (stain > 46000) idx = FabricStain + 0;
            if (stain > 54000) idx = FabricStain + 2;
            if (stain > 60000) idx = FabricStain + 1;

            // 덧댄 천 — 64px 칸 하나.
            int patch = AscendTextureSynth.Lattice(CellIndex(x, 6, size), CellIndex(y, 6, size), seed + 171u);
            int px = x & 63, py = y & 63;
            if (patch > 52000 && px >= 8 && px <= 55 && py >= 8 && py <= 55)
            {
                idx = FabricPatch;
                if (px == 8 || px == 55 || py == 8 || py == 55) idx = FabricStain + 1;
            }
            return idx;
        }

        // ── 발광 마스크 ───────────────────────────────────────────────────────
        //
        // ⚠ **현재 `Ascend/Stylized` 셰이더에는 발광 텍스처 슬롯이 없다.** `_EmissionColor`
        // 만 있고 그것은 `MaterialPropertyBlock` 이 쓰는 단색이다. 이 마스크 두 장은
        // 셰이더에 `_EmissionMap` 이 추가되기 전에는 **배선할 곳이 없다.** 그 사실을
        // 여기와 보고서에 함께 적는다 — 배선 못 하는 에셋을 만들어 두고 「만들었다」로
        // 끝내는 것이 이 저장소가 반복한 실패다(`UP-VIS-01`: 텍스처 4장 배선 0건).

        private static int PaintGaugeEmissive(int x, int y, int size, uint seed)
        {
            int mx = x & 63, my = y & 63;
            int dx = mx - 32, dy = my - 32;
            int d = AscendTextureSynth.Isqrt(dx * dx + dy * dy);

            // 눈금 반경대를 12~22 로 잡는다. 14~20 에서는 켜진 화소가 0.19% 로
            // 통과 하한(0.2%)에 못 미쳤다 — 어두운 칸에서 발광이 판독의 주 채널인데
            // (`GRAPHICS_TARGET.md` §2 G-3) 그 정도면 화면에서 사실상 안 보인다.
            if (d >= 12 && d < 22)
            {
                bool tick = (dx == 0) || (dy == 0) || (dx == dy) || (dx == -dy)
                         || (dx * 2 == dy) || (dy * 2 == dx)
                         || (dx * 2 == -dy) || (dy * 2 == -dx);
                if (tick)
                {
                    int flicker = Grain(x, y, size, seed + 3u);
                    return 1 + Level(flicker, 6, x, y, 20000);
                }
            }

            // 파일럿 램프 — 칸마다 하나, 절반쯤만 켜져 있다.
            int lamp = AscendTextureSynth.Lattice(CellIndex(x, 6, size), CellIndex(y, 6, size), seed + 11u);
            int lx = mx - 54, ly = my - 10;
            int ld = lx * lx + ly * ly;
            if (ld <= 10) return lamp > 32000 ? 9 : 7;
            if (ld <= 20) return lamp > 32000 ? 7 : 0;
            return 0;
        }

        private static int PaintHousingEmissive(int x, int y, int size, uint seed)
        {
            int mx = x & 63, my = y & 63;

            // 표시창 — 가로로 긴 창 하나. 안쪽에 가로 스캔선이 있어 백열등처럼 보인다.
            if (mx >= 12 && mx <= 51 && my >= 20 && my <= 33)
            {
                if ((my & 1) == 0) return 0;
                int v = Fbm(x, y, 8, 8, size, seed, 2);
                return 1 + Level(v, 6, x, y, 18000);
            }

            // 경고등 — 64px 칸 중 소수만.
            int cell = AscendTextureSynth.Lattice(CellIndex(x, 6, size), CellIndex(y, 6, size), seed + 23u);
            int wx = mx - 32, wy = my - 50;
            int wd = wx * wx + wy * wy;
            if (cell > 50000)
            {
                if (wd <= 4) return 9;
                if (wd <= 9) return 8;
                if (wd <= 16) return 7;
            }
            return 0;
        }

        // ── 도구 ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Bayer 8×8 오더드 디더 행렬(0~63). **이것이 「손그림 픽셀」의 핵심이다.**
        /// 색을 12~24개로 못박으면 넓은 면에 밴딩이 생기는데, 디더링은 그 밴딩을
        /// 규칙적인 픽셀 그레인으로 바꾼다. 무작위 디더(백색소음)를 쓰지 않는 이유는
        /// 그쪽이 「지저분한 사진」으로 보이기 때문이다 — 오더드는 **그린 것처럼** 보인다.
        /// </summary>
        private static readonly int[] BayerMatrix =
        {
             0, 32,  8, 40,  2, 34, 10, 42,
            48, 16, 56, 24, 50, 18, 58, 26,
            12, 44,  4, 36, 14, 46,  6, 38,
            60, 28, 52, 20, 62, 30, 54, 22,
             3, 35, 11, 43,  1, 33,  9, 41,
            51, 19, 59, 27, 49, 17, 57, 25,
            15, 47,  7, 39, 13, 45,  5, 37,
            63, 31, 55, 23, 61, 29, 53, 21,
        };

        /// <summary>0~65535 값에 오더드 디더를 얹고 <paramref name="levels"/> 칸으로 양자화한다.</summary>
        /// <param name="amplitude">디더 진폭(0~65535 단위). 한 칸 폭의 절반 언저리가 자연스럽다.</param>
        internal static int Level(int value, int levels, int x, int y, int amplitude)
        {
            int b = BayerMatrix[(y & 7) * 8 + (x & 7)];
            int d = ((b * 2 + 1) - 64) * amplitude / 64;
            int v = value + d;
            if (v < 0) v = 0;
            else if (v > 65535) v = 65535;
            int i = (v * levels) >> 16;
            if (i < 0) return 0;
            return i >= levels ? levels - 1 : i;
        }

        internal static int Blend(int a, int b, int weight)
        {
            if (weight <= 0) return a;
            if (weight >= 65536) return b;
            return (int)(((long)a * (65536 - weight) + (long)b * weight) >> 16);
        }

        /// <summary>
        /// 픽셀 단위 백색 잡음 0~65535. 고주파 그레인 층이고, 「손그림 픽셀」의 알갱이가
        /// 여기서 나온다.
        ///
        /// **좌표를 텍스처 크기로 감싼다.** 감싸지 않으면 이 층 하나 때문에 타일링이 깨진다 —
        /// 화면에 그리는 동안에는 x,y 가 이미 범위 안이라 그림이 똑같아서 **증상이 안 보이고**,
        /// 주기성 검사에서만 드러난다. 그것이 이 마스킹을 「불필요한 방어」로 오해하기 쉬운
        /// 이유이자, 남겨 둬야 하는 이유다.
        /// </summary>
        internal static int Grain(int x, int y, int size, uint seed)
        {
            int mask = size - 1;
            return AscendTextureSynth.Lattice(x & mask, y & mask, seed);
        }

        internal static int Fbm(int px, int py, int cellX, int cellY, int size, uint seed, int octaves)
        {
            return AscendTextureSynth.Fbm(px, py, cellX, cellY, size, seed, octaves);
        }

        /// <summary>
        /// 셀 격자 첨자. `x >> shift` 를 그대로 해시에 넣으면 **텍스처 밖에서 첨자가 계속
        /// 자라** 타일링이 깨진다. 화면 안에서는 x 가 범위 안이라 그림이 같아서 증상이
        /// 안 보이고, 주기성 검사에서만 드러난다.
        /// </summary>
        internal static int CellIndex(int coordinate, int shift, int size)
        {
            int cells = size >> shift;
            if (cells < 1) cells = 1;
            return AscendTextureSynth.Wrap(coordinate >> shift, cells);
        }

        /// <summary>주기 <paramref name="size"/> 의 원환 위에서 가장 짧은 부호 있는 차이.</summary>
        internal static int WrapDelta(int delta, int size)
        {
            int half = size >> 1;
            delta %= size;
            if (delta < -half) delta += size;
            else if (delta >= half) delta -= size;
            return delta;
        }

        /// <summary>
        /// 보로노이 F1. **원환 위에서** 계산하므로 좌우·상하 이음매가 없다 —
        /// 특징점의 해시는 감싼 좌표에서 뽑고 위치는 감싸지 않은 격자에서 만든다.
        /// 얼룩·부식처럼 「덩어리져야 하는」 것에 쓴다. FBM 문턱만으로는 덩어리가
        /// 안 생기고 구름이 된다.
        /// </summary>
        /// <returns>0~65535. 작을수록 특징점에 가깝다.</returns>
        internal static int VoronoiF1(int px, int py, int cells, int size, uint seed, out int cellId)
        {
            int cell = size / cells;                 // 2의 거듭제곱이어야 한다
            int mask = cell - 1;
            int gx0 = px / cell, gy0 = py / cell;
            int best = int.MaxValue;
            cellId = 0;

            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    int gx = gx0 + ox, gy = gy0 + oy;
                    int wx = AscendTextureSynth.Wrap(gx, cells);
                    int wy = AscendTextureSynth.Wrap(gy, cells);
                    int hx = AscendTextureSynth.Lattice(wx, wy, seed) & mask;
                    int hy = AscendTextureSynth.Lattice(wx, wy, seed + 7919u) & mask;
                    int dx = px - (gx * cell + hx);
                    int dy = py - (gy * cell + hy);
                    int d2 = dx * dx + dy * dy;
                    if (d2 < best)
                    {
                        best = d2;
                        cellId = AscendTextureSynth.Lattice(wx, wy, seed + 104729u);
                    }
                }
            }

            int d = AscendTextureSynth.Isqrt(best);
            int n = d * 65536 / (cell * 2);
            return n > 65535 ? 65535 : n;
        }

        // 방향 벡터 — |d|² ≤ 2 인 것만 쓴다. 그보다 기울면 정수 투영이 건너뛰어
        // 선이 점선이 되고, 「긁힘」이 아니라 「점 자국」이 된다.
        private static readonly int[] ScratchDirX = { 1, 0, 1, 1 };
        private static readonly int[] ScratchDirY = { 0, 1, 1, -1 };

        /// <summary>
        /// 결정론적 긁힘. 0 = 없음, 1 = 가장자리, 2 = 심. 원환 위에서 계산하므로
        /// 텍스처 경계를 넘어가도 이어진다.
        /// </summary>
        internal static int ScratchHit(int x, int y, int size, uint seed, int count)
        {
            int best = 0;
            for (int i = 0; i < count; i++)
            {
                uint s = seed + (uint)i * 0x9E3779B1u;
                int ox = (AscendTextureSynth.Lattice(i, 1, s) * size) >> 16;
                int oy = (AscendTextureSynth.Lattice(i, 2, s) * size) >> 16;
                int sel = AscendTextureSynth.Lattice(i, 3, s) & 3;
                int dx = ScratchDirX[sel], dy = ScratchDirY[sel];
                int len = 10 + ((AscendTextureSynth.Lattice(i, 4, s) * (size / 3)) >> 16);

                int rx = WrapDelta(x - ox, size);
                int ry = WrapDelta(y - oy, size);

                int dd = dx * dx + dy * dy;
                int num = rx * dx + ry * dy;
                int t = FloorDiv(num, dd);
                if (t < 0 || t > len) continue;

                int qx = rx - t * dx, qy = ry - t * dy;
                int d2 = qx * qx + qy * qy;
                if (d2 == 0) return 2;
                if (d2 <= 2 && best < 1) best = 1;
            }
            return best;
        }

        /// <summary>바닥 나눗셈. C# 의 `/` 는 0 쪽으로 자르므로 음수에서 선이 끊긴다.</summary>
        private static int FloorDiv(int a, int b)
        {
            int q = a / b;
            if ((a % b != 0) && ((a < 0) != (b < 0))) q--;
            return q;
        }

        /// <summary>
        /// **타일링을 임계값이 아니라 정의로 단정한다.**
        ///
        /// 「좌측 열과 우측 열의 평균 차이가 임계 이하」라는 검사는 이 세트에서 쓸 수 없다.
        /// 벽 패널의 이음매선·그레이팅의 바 경계처럼 **의도한 하드 에지가 마침 x=0 에 놓이면**
        /// 그 검사가 실패하는데, 그 텍스처는 실제로는 완벽하게 이어진다. 반대로 이음매 근처만
        /// 매끄럽게 뭉개 놓으면 그 검사는 통과하면서 타일링은 깨져 있을 수 있다.
        /// 두 방향 모두로 틀리는 검사다.
        ///
        /// 그래서 함수 자체의 주기성을 본다 — 모든 화소에서
        /// <c>Paint(x ± size, y) == Paint(x, y)</c> 이고 세로도 같은가.
        /// 참이면 무한히 타일링해도 이음매가 **존재할 수 없다.**
        ///
        /// **음의 좌표는 보지 않는다.** 타일링된 무한 평면은 `I(x,y) = Paint(x mod P, y mod P)`
        /// 로 정의되고, 화소는 언제나 `[0, P)` 에서만 뽑힌다. 그러므로 잡아야 할 결함은
        /// 「`[0,P)` 안의 어떤 화소가 감싸지 않은 좌표를 읽어서 옆 타일과 값이 갈리는가」이고,
        /// 그것이 정확히 `Paint(x+P, y) != Paint(x, y)` 로 드러난다.
        /// 음수까지 요구하면 <see cref="AscendTextureSynth.Noise"/> 의 `px / cellX` 가
        /// 0 쪽으로 잘리는 것에 걸리는데, 그 함수는 애초에 음이 아닌 정의역 계약이고
        /// 이 파이프라인은 거기에 음수를 넣지 않는다 — **쓰지 않는 정의역의 실패를
        /// 타일링 결함으로 보고하면 진짜 결함이 그 잡음에 묻힌다.**
        /// </summary>
        public static bool IsPeriodic(Spec spec, out string detail)
        {
            int size = spec.Size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int a = Paint(spec, x, y);
                    if (Paint(spec, x + size, y) != a) { detail = Where("x+size", x, y); return false; }
                    if (Paint(spec, x, y + size) != a) { detail = Where("y+size", x, y); return false; }
                    if (Paint(spec, x + size, y + size) != a) { detail = Where("대각", x, y); return false; }
                }
            }
            detail = null;
            return true;
        }

        private static string Where(string axis, int x, int y)
        {
            return "(" + x + "," + y + ") 에서 " + axis + " 가 다르다";
        }

        public static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }

    /// <summary>
    /// PNG 되읽기. **자기가 만든 바이트를 다시 파일에서 읽어 재는 것이 이 파이프라인의
    /// 요구다** — 「생성했다」가 아니라 「생성된 파일이 지표를 통과한다」가 통과 조건이고,
    /// 메모리 안의 버퍼를 재면 그 둘의 차이가 사라진다.
    ///
    /// 지원 범위: 비트 깊이 8, 색상 타입 2(RGB)·3(팔레트), 인터레이스 없음, 필터 0~4.
    /// deflate 는 stored block 만 푼다 — 이 저장소의 인코더가 그것만 쓰고,
    /// `System.IO.Compression` 에 의존하지 않아야 어느 런타임에서도 같은 결과가 나온다.
    /// </summary>
    public static class AscendPngReader
    {
        public static bool TryDecode(byte[] png, out int width, out int height, out byte[] rgb,
                                     out string error)
        {
            width = 0; height = 0; rgb = null; error = null;
            if (png == null || png.Length < 8) { error = "빈 파일"; return false; }

            byte[] signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            for (int i = 0; i < 8; i++)
                if (png[i] != signature[i]) { error = "PNG 시그니처 불일치"; return false; }

            int bitDepth = 0, colorType = -1;
            byte[] palette = null;
            var idat = new MemoryStream();
            int at = 8;

            while (at + 8 <= png.Length)
            {
                int length = ReadBigEndian(png, at);
                if (length < 0 || at + 12 + length > png.Length) { error = "청크 길이 이상"; return false; }
                string type = Ascii(png, at + 4, 4);
                int body = at + 8;

                if (type == "IHDR")
                {
                    width = ReadBigEndian(png, body);
                    height = ReadBigEndian(png, body + 4);
                    bitDepth = png[body + 8];
                    colorType = png[body + 9];
                    if (png[body + 12] != 0) { error = "인터레이스는 지원하지 않는다"; return false; }
                }
                else if (type == "PLTE")
                {
                    palette = new byte[length];
                    Buffer.BlockCopy(png, body, palette, 0, length);
                }
                else if (type == "IDAT")
                {
                    idat.Write(png, body, length);
                }
                else if (type == "IEND") break;

                at = body + length + 4;
            }

            if (width <= 0 || height <= 0) { error = "IHDR 없음"; return false; }
            if (bitDepth != 8) { error = "비트 깊이 " + bitDepth + " 는 지원하지 않는다"; return false; }
            if (colorType != 2 && colorType != 3)
            {
                error = "색상 타입 " + colorType + " 는 지원하지 않는다";
                return false;
            }
            if (colorType == 3 && palette == null) { error = "PLTE 없음"; return false; }

            byte[] raw = InflateStored(idat.ToArray(), out error);
            if (raw == null) return false;

            int channels = colorType == 3 ? 1 : 3;
            int stride = 1 + width * channels;
            if (raw.Length < stride * height)
            {
                error = "스캔라인이 모자란다 (" + raw.Length + " < " + (stride * height) + ")";
                return false;
            }

            var lines = new byte[width * channels * height];
            for (int y = 0; y < height; y++)
            {
                int src = y * stride;
                int filter = raw[src];
                int dst = y * width * channels;
                int prev = dst - width * channels;
                for (int i = 0; i < width * channels; i++)
                {
                    int cur = raw[src + 1 + i];
                    int a = i >= channels ? lines[dst + i - channels] : 0;
                    int b = y > 0 ? lines[prev + i] : 0;
                    int c = (y > 0 && i >= channels) ? lines[prev + i - channels] : 0;
                    int value;
                    switch (filter)
                    {
                        case 0: value = cur; break;
                        case 1: value = cur + a; break;
                        case 2: value = cur + b; break;
                        case 3: value = cur + ((a + b) >> 1); break;
                        case 4: value = cur + Paeth(a, b, c); break;
                        default: error = "필터 " + filter + " 는 없는 값이다"; return false;
                    }
                    lines[dst + i] = (byte)value;
                }
            }

            rgb = new byte[width * height * 3];
            if (colorType == 3)
            {
                for (int i = 0; i < width * height; i++)
                {
                    int p = lines[i] * 3;
                    if (p + 2 >= palette.Length) { error = "팔레트 범위를 벗어난 인덱스"; return false; }
                    rgb[i * 3] = palette[p];
                    rgb[i * 3 + 1] = palette[p + 1];
                    rgb[i * 3 + 2] = palette[p + 2];
                }
            }
            else
            {
                Buffer.BlockCopy(lines, 0, rgb, 0, rgb.Length);
            }
            return true;
        }

        private static int Paeth(int a, int b, int c)
        {
            int p = a + b - c;
            int pa = p > a ? p - a : a - p;
            int pb = p > b ? p - b : b - p;
            int pc = p > c ? p - c : c - p;
            if (pa <= pb && pa <= pc) return a;
            return pb <= pc ? b : c;
        }

        /// <summary>stored block 만으로 이루어진 zlib 스트림을 푼다.</summary>
        private static byte[] InflateStored(byte[] zlib, out string error)
        {
            error = null;
            if (zlib.Length < 6) { error = "IDAT 가 너무 짧다"; return null; }
            if ((zlib[0] & 0x0F) != 8) { error = "zlib 압축 방식이 deflate 가 아니다"; return null; }

            var output = new MemoryStream(zlib.Length);
            int at = 2;
            while (true)
            {
                if (at + 5 > zlib.Length) { error = "블록 헤더가 잘렸다"; return null; }
                int header = zlib[at];
                int type = (header >> 1) & 3;
                if (type != 0)
                {
                    error = "stored block 이 아닌 deflate 블록(type=" + type + ")은 지원하지 않는다";
                    return null;
                }
                int len = zlib[at + 1] | (zlib[at + 2] << 8);
                at += 5;
                if (at + len > zlib.Length) { error = "stored block 본문이 잘렸다"; return null; }
                output.Write(zlib, at, len);
                at += len;
                if ((header & 1) != 0) break;
            }
            return output.ToArray();
        }

        private static int ReadBigEndian(byte[] data, int at)
        {
            return (data[at] << 24) | (data[at + 1] << 16) | (data[at + 2] << 8) | data[at + 3];
        }

        private static string Ascii(byte[] data, int at, int count)
        {
            var chars = new char[count];
            for (int i = 0; i < count; i++) chars[i] = (char)data[at + i];
            return new string(chars);
        }
    }

    /// <summary>
    /// 텍스처 한 장의 지표. **전부 `docs/GRAPHICS_TARGET.md` §2 에서 나온 축이다.**
    /// 인상 평가를 대체하지 않는다 — 회귀를 잡는 것이 목적이다.
    /// </summary>
    public sealed class AscendSurfaceMetrics
    {
        /// <summary>8×8 블록 휘도 표준편차의 **중앙값**. G-1 의 핵심 지표. 통과선 12.0.</summary>
        public double BlockStdDevMedian;

        /// <summary>가장 낮은 10% 블록의 표준편차 — 「무지 면이 섞여 있는가」를 본다.</summary>
        public double BlockStdDevP10;

        /// <summary>32×32 블록 중 표준편차 &lt; 4 인 비율(G-5 의 텍스처판). 낮을수록 좋다.</summary>
        public double FlatBlockRatio;

        /// <summary>관측된 서로 다른 RGB 색의 수. 알베도 통과 범위 12~24.</summary>
        public int UniqueColors;

        /// <summary>평균 휘도 0~1. 알베도 통과 범위 0.45~0.75.</summary>
        public double MeanBrightness;

        /// <summary>좌우 이음매의 평균 휘도 차.</summary>
        public double SeamHorizontal;

        /// <summary>상하 이음매의 평균 휘도 차.</summary>
        public double SeamVertical;

        /// <summary>내부에서 이웃한 열/행끼리의 평균 휘도 차 — 이음매를 견줄 기준선.</summary>
        public double InteriorStepHorizontal;
        public double InteriorStepVertical;

        /// <summary>
        /// 내부 이웃 열/행 단차의 **최대값**. 이음매가 「눈에 띄는가」를 판정하는 기준이다 —
        /// 평균이 아니라 최대를 쓰는 이유는, 텍스처 안에 이미 같은 크기의 에지가 존재하면
        /// 이음매의 그 에지도 눈에 띄지 않기 때문이다. 판 이음매선이 정확히 그 경우다.
        /// </summary>
        public double InteriorStepMaxHorizontal;
        public double InteriorStepMaxVertical;

        /// <summary>휘도 200 이상인 화소 비율. 발광 마스크의 판정축이다.</summary>
        public double BrightPixelRatio;

        public int Width;
        public int Height;

        /// <summary>
        /// 이음매 판정. 이음매의 단차가 **내부의 평소 단차보다 크지 않아야** 한다.
        /// 절대 임계를 쓰지 않는 이유: 고주파 그레인이 센 텍스처는 내부 단차 자체가 커서
        /// 같은 절대값이 어떤 텍스처에는 관대하고 어떤 텍스처에는 불가능해진다.
        /// </summary>
        public bool SeamlessHorizontal
        {
            get { return SeamHorizontal <= InteriorStepMaxHorizontal * 1.05 + 1.0; }
        }

        public bool SeamlessVertical
        {
            get { return SeamVertical <= InteriorStepMaxVertical * 1.05 + 1.0; }
        }

        public bool Seamless { get { return SeamlessHorizontal && SeamlessVertical; } }

        /// <summary>ITU-R BT.709 지각 휘도(0~255). 셰이더의 `Lum()` 과 같은 가중치다.</summary>
        public static double Luminance(byte r, byte g, byte b)
        {
            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }

        public static AscendSurfaceMetrics Measure(int width, int height, byte[] rgb)
        {
            var m = new AscendSurfaceMetrics();
            m.Width = width;
            m.Height = height;

            int count = width * height;
            var lum = new double[count];
            double sum = 0;
            int bright = 0;
            var colors = new HashSet<int>();

            for (int i = 0; i < count; i++)
            {
                byte r = rgb[i * 3], g = rgb[i * 3 + 1], b = rgb[i * 3 + 2];
                double l = Luminance(r, g, b);
                lum[i] = l;
                sum += l;
                if (l >= 200.0) bright++;
                colors.Add((r << 16) | (g << 8) | b);
            }

            m.MeanBrightness = (sum / count) / 255.0;
            m.BrightPixelRatio = (double)bright / count;
            m.UniqueColors = colors.Count;

            m.BlockStdDevMedian = BlockStdDev(lum, width, height, 8, out m.BlockStdDevP10);

            double flat = 0;
            int flatTotal = 0;
            for (int by = 0; by + 32 <= height; by += 32)
            {
                for (int bx = 0; bx + 32 <= width; bx += 32)
                {
                    flatTotal++;
                    if (StdDevOf(lum, width, bx, by, 32) < 4.0) flat++;
                }
            }
            m.FlatBlockRatio = flatTotal == 0 ? 0 : flat / flatTotal;

            double seamH = 0, seamV = 0;
            for (int y = 0; y < height; y++)
                seamH += Math.Abs(lum[y * width] - lum[y * width + width - 1]);
            seamH /= height;
            for (int x = 0; x < width; x++)
                seamV += Math.Abs(lum[x] - lum[(height - 1) * width + x]);
            seamV /= width;
            m.SeamHorizontal = seamH;
            m.SeamVertical = seamV;

            double stepH = 0, stepHMax = 0;
            for (int x = 0; x + 1 < width; x++)
            {
                double s = 0;
                for (int y = 0; y < height; y++) s += Math.Abs(lum[y * width + x] - lum[y * width + x + 1]);
                s /= height;
                stepH += s;
                if (s > stepHMax) stepHMax = s;
            }
            m.InteriorStepHorizontal = stepH / Math.Max(1, width - 1);
            m.InteriorStepMaxHorizontal = stepHMax;

            double stepV = 0, stepVMax = 0;
            for (int y = 0; y + 1 < height; y++)
            {
                double s = 0;
                for (int x = 0; x < width; x++) s += Math.Abs(lum[y * width + x] - lum[(y + 1) * width + x]);
                s /= width;
                stepV += s;
                if (s > stepVMax) stepVMax = s;
            }
            m.InteriorStepVertical = stepV / Math.Max(1, height - 1);
            m.InteriorStepMaxVertical = stepVMax;
            return m;
        }

        private static double BlockStdDev(double[] lum, int width, int height, int block, out double p10)
        {
            var values = new List<double>((width / block) * (height / block));
            for (int by = 0; by + block <= height; by += block)
                for (int bx = 0; bx + block <= width; bx += block)
                    values.Add(StdDevOf(lum, width, bx, by, block));

            values.Sort();
            if (values.Count == 0) { p10 = 0; return 0; }
            p10 = values[values.Count / 10];
            int mid = values.Count / 2;
            return (values.Count % 2 == 1)
                ? values[mid]
                : (values[mid - 1] + values[mid]) * 0.5;
        }

        private static double StdDevOf(double[] lum, int width, int bx, int by, int block)
        {
            double sum = 0, sumSq = 0;
            int n = block * block;
            for (int y = 0; y < block; y++)
            {
                int row = (by + y) * width + bx;
                for (int x = 0; x < block; x++)
                {
                    double v = lum[row + x];
                    sum += v;
                    sumSq += v * v;
                }
            }
            double mean = sum / n;
            double variance = sumSq / n - mean * mean;
            return variance <= 0 ? 0 : Math.Sqrt(variance);
        }
    }

    /// <summary>
    /// 생성 → 디스크 기록 → **파일 되읽기** → 지표 측정 → 판정. 한 곳에서 다 한다.
    ///
    /// Unity 없이 도는 것이 이 클래스의 존재 이유다. `System.IO` 는 Unity 타입이 아니므로
    /// `&lt;PURE&gt;` 규약을 깨지 않는다. 에디터 메뉴와 헤드리스 드라이버가 **같은 함수**를
    /// 부르기 때문에 「에디터에서만 통과한다」가 성립할 수 없다.
    /// </summary>
    public static class AscendSurfaceValidation
    {
        /// <summary>G-1 통과선 — `docs/GRAPHICS_TARGET.md` §2.</summary>
        public const double BlockStdDevFloor = 12.0;

        public const int MinColors = 12;
        public const int MaxColors = 24;
        public const double MinBrightness = 0.45;
        public const double MaxBrightness = 0.75;

        /// <summary>발광 마스크의 통과 범위 — 대부분 검정이고 켜진 화소는 소수여야 한다.</summary>
        public const double EmissiveMaxMean = 0.12;
        public const double EmissiveMinBrightRatio = 0.002;
        public const double EmissiveMaxBrightRatio = 0.080;

        public sealed class Result
        {
            public AscendSurfaceSynth.Spec Spec;
            public AscendSurfaceMetrics Metrics;
            public int FileBytes;
            public ulong Hash;
            public string WriteState;
            public bool Deterministic;
            public bool Periodic;
            public bool Passed;
            public List<string> Failures = new List<string>();
        }

        /// <summary>
        /// 세트 전체를 처리한다.
        /// </summary>
        /// <param name="absoluteFolder">산출 폴더의 절대 경로.</param>
        /// <param name="write">거짓이면 디스크를 건드리지 않고 **이미 있는 파일만** 검사한다.</param>
        public static List<Result> Run(string absoluteFolder, bool write, out string report)
        {
            var results = new List<Result>();
            var sb = new StringBuilder(16384);
            CultureInfo c = CultureInfo.InvariantCulture;

            sb.AppendLine("[상승] === 산업 표면 텍스처 세트 (G-1 텍스처 커버리지) ===");
            sb.AppendLine("알고리즘: " + AscendSurfaceSynth.AlgorithmId + "   출력: " + AscendSurfaceSynth.OutputFolder);
            sb.AppendLine("근거: docs/VISUAL_BIBLE.md §2.1 스타일 락 · §3 팔레트 · §4 금지 21항");
            sb.AppendLine("     docs/GRAPHICS_TARGET.md §2 G-1 — 8×8 블록 표준편차 중앙값 ≥ "
                          + BlockStdDevFloor.ToString("0.0", c));
            sb.AppendLine("모드: " + (write ? "생성 + 검증" : "검증만 (디스크를 쓰지 않는다)"));
            sb.AppendLine();

            if (write) Directory.CreateDirectory(absoluteFolder);

            int passed = 0;
            foreach (AscendSurfaceSynth.Spec spec in AscendSurfaceSynth.Specs())
            {
                var r = new Result { Spec = spec };
                string filePath = Path.Combine(absoluteFolder, spec.FileName);

                byte[] png = AscendSurfaceSynth.Encode(spec);

                // 결정론 — 같은 시드로 두 번 만들어 바이트를 맞춘다. 「같은 시드가 같은 비트」는
                // 한 번 만들고 주장할 수 있는 성질이 아니다.
                byte[] again = AscendSurfaceSynth.Encode(spec);
                r.Deterministic = AscendSurfaceSynth.BytesEqual(png, again);
                if (!r.Deterministic) r.Failures.Add("같은 시드 재생성이 바이트가 다르다");

                string aperiodic;
                r.Periodic = AscendSurfaceSynth.IsPeriodic(spec, out aperiodic);
                if (!r.Periodic) r.Failures.Add("타일링이 깨진다 — " + aperiodic);

                if (write)
                {
                    if (File.Exists(filePath))
                    {
                        byte[] existing = File.ReadAllBytes(filePath);
                        if (AscendSurfaceSynth.BytesEqual(existing, png))
                        {
                            r.WriteState = "동일 — 쓰지 않았다";
                        }
                        else
                        {
                            File.WriteAllBytes(filePath, png);
                            r.WriteState = "달라서 덮어썼다";
                        }
                    }
                    else
                    {
                        File.WriteAllBytes(filePath, png);
                        r.WriteState = "새로 썼다";
                    }
                }
                else
                {
                    r.WriteState = File.Exists(filePath) ? "기존 파일" : "파일 없음";
                }

                if (!File.Exists(filePath))
                {
                    r.Failures.Add("PNG 가 디스크에 없다");
                    results.Add(r);
                    continue;
                }

                // **디스크에서 다시 읽는다.** 메모리 버퍼를 재면 「썼다」와 「쓴 것이
                // 통과한다」가 같은 문장이 되고, 그 혼동이 이 저장소의 반복 실패다.
                byte[] onDisk = File.ReadAllBytes(filePath);
                r.FileBytes = onDisk.Length;
                r.Hash = Fnv1a64(onDisk);

                if (write && !AscendSurfaceSynth.BytesEqual(onDisk, png))
                    r.Failures.Add("디스크의 바이트가 방금 만든 것과 다르다");

                int w, h;
                byte[] rgb;
                string error;
                if (!AscendPngReader.TryDecode(onDisk, out w, out h, out rgb, out error))
                {
                    r.Failures.Add("PNG 를 되읽지 못했다: " + error);
                    results.Add(r);
                    continue;
                }

                AscendSurfaceMetrics m = AscendSurfaceMetrics.Measure(w, h, rgb);
                r.Metrics = m;

                if (w != spec.Size || h != spec.Size)
                    r.Failures.Add("해상도가 " + w + "×" + h + " 로 선언(" + spec.Size + ")과 다르다");

                if (spec.IsEmissive)
                {
                    if (m.MeanBrightness > EmissiveMaxMean)
                        r.Failures.Add("발광 마스크 평균 밝기 " + m.MeanBrightness.ToString("0.000", c)
                                       + " > " + EmissiveMaxMean.ToString("0.000", c));
                    if (m.BrightPixelRatio < EmissiveMinBrightRatio || m.BrightPixelRatio > EmissiveMaxBrightRatio)
                        r.Failures.Add("발광 화소 비율 " + (m.BrightPixelRatio * 100).ToString("0.00", c)
                                       + "% 가 " + (EmissiveMinBrightRatio * 100).ToString("0.0", c) + "~"
                                       + (EmissiveMaxBrightRatio * 100).ToString("0.0", c) + "% 밖이다");
                }
                else
                {
                    if (m.BlockStdDevMedian < BlockStdDevFloor)
                        r.Failures.Add("8×8 블록 표준편차 중앙값 " + m.BlockStdDevMedian.ToString("0.00", c)
                                       + " < " + BlockStdDevFloor.ToString("0.0", c) + " (G-1 미달)");
                    if (m.UniqueColors < MinColors || m.UniqueColors > MaxColors)
                        r.Failures.Add("색 수 " + m.UniqueColors + " 가 " + MinColors + "~" + MaxColors + " 밖이다");
                    if (m.MeanBrightness < MinBrightness || m.MeanBrightness > MaxBrightness)
                        r.Failures.Add("평균 밝기 " + m.MeanBrightness.ToString("0.000", c)
                                       + " 가 " + MinBrightness.ToString("0.00", c) + "~"
                                       + MaxBrightness.ToString("0.00", c) + " 밖이다");
                }

                // 주기성이 이미 참이면 이음매는 **존재할 수 없다.** 아래는 그 위에 얹는
                // 가독성 지표다 — 이음매의 단차가 텍스처 안에 이미 있는 가장 큰 에지보다
                // 두드러지면, 타일링은 맞지만 「경계선이 규칙적으로 반복되는 무늬」로 읽힌다.
                if (!m.SeamlessHorizontal)
                    r.Failures.Add("좌우 이음매 단차 " + m.SeamHorizontal.ToString("0.00", c)
                                   + " 가 내부 최대 에지 " + m.InteriorStepMaxHorizontal.ToString("0.00", c)
                                   + " 보다 두드러진다");
                if (!m.SeamlessVertical)
                    r.Failures.Add("상하 이음매 단차 " + m.SeamVertical.ToString("0.00", c)
                                   + " 가 내부 최대 에지 " + m.InteriorStepMaxVertical.ToString("0.00", c)
                                   + " 보다 두드러진다");

                r.Passed = r.Failures.Count == 0;
                if (r.Passed) passed++;
                results.Add(r);
            }

            // ── 표 ────────────────────────────────────────────────────────────
            sb.AppendLine("파일                            해상도  색  8×8중앙  P10   평탄%  평균밝기 주기 이음매LR      이음매TB     판정");
            foreach (Result r in results)
            {
                AscendSurfaceMetrics m = r.Metrics;
                string line = Pad(r.Spec.FileName, 31)
                    + Pad(r.Spec.Size + "×" + r.Spec.Size, 8)
                    + (m == null ? Pad("-", 4) : Pad(m.UniqueColors.ToString(c), 4))
                    + (m == null ? Pad("-", 9) : Pad(m.BlockStdDevMedian.ToString("0.00", c), 9))
                    + (m == null ? Pad("-", 6) : Pad(m.BlockStdDevP10.ToString("0.0", c), 6))
                    + (m == null ? Pad("-", 7) : Pad((m.FlatBlockRatio * 100).ToString("0.0", c), 7))
                    + (m == null ? Pad("-", 9) : Pad(m.MeanBrightness.ToString("0.000", c), 9))
                    + Pad(r.Periodic ? "예" : "아니오", 5)
                    + (m == null ? Pad("-", 14) : Pad(m.SeamHorizontal.ToString("0.0", c) + "/" + m.InteriorStepMaxHorizontal.ToString("0.0", c), 14))
                    + (m == null ? Pad("-", 13) : Pad(m.SeamVertical.ToString("0.0", c) + "/" + m.InteriorStepMaxVertical.ToString("0.0", c), 13))
                    + (r.Passed ? "통과" : "미달");
                sb.AppendLine(line);
            }
            sb.AppendLine();

            foreach (Result r in results)
            {
                sb.AppendLine(r.Spec.FileName);
                sb.AppendLine("  용도: " + r.Spec.Purpose);
                sb.AppendLine("  시드 0x" + r.Spec.Seed.ToString("X8", c)
                              + " · 팔레트 " + r.Spec.Palette.Length + "색 선언"
                              + " · 권장 타일링 " + r.Spec.TilesPerMeter.ToString("0.00", c) + " 회/m"
                              + " · " + r.FileBytes + " bytes"
                              + " · FNV-1a64 0x" + r.Hash.ToString("X16", c));
                sb.AppendLine("  " + r.WriteState
                              + " · 재생성 바이트 동일: " + (r.Deterministic ? "예" : "아니오")
                              + " · 함수 주기성(+size 양축·대각): " + (r.Periodic ? "성립" : "깨짐"));
                if (r.Failures.Count == 0) sb.AppendLine("  판정: 통과");
                else foreach (string f in r.Failures) sb.AppendLine("  ✗ " + f);
            }

            sb.AppendLine();
            sb.AppendLine("합계 — " + passed + " / " + results.Count + " 통과");
            if (passed != results.Count)
                sb.AppendLine("🚨 미달 항목이 있다. 위의 ✗ 줄이 각각 무엇이 모자란지 말한다.");

            report = sb.ToString();
            return results;
        }

        private static string Pad(string value, int width)
        {
            if (value == null) value = string.Empty;
            if (value.Length >= width) return value.Substring(0, width - 1) + " ";
            return value + new string(' ', width - value.Length);
        }

        public static ulong Fnv1a64(byte[] data)
        {
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < data.Length; i++)
            {
                hash ^= data[i];
                hash *= 1099511628211UL;
            }
            return hash;
        }
    }
    // </PURE>
}
