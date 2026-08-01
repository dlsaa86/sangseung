using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Ascend.Prototype.Data.Profiles;

namespace Ascend.Prototype.EditorTools
{
    /// <summary>
    /// 플레이스홀더 텍스처 4장을 **절차적으로** 만든다 — `UP-VIS-01` 스타일 락의 첫 항목
    /// 「저해상도 손그림 픽셀 텍스처」와 `UP-PLAT-05` 의 「규칙이 적용됐다」가 같은 뿌리에서
    /// 막혀 있었기 때문이다. 이 저장소의 관할 루트(<see cref="AssetImportPaths.ManagedRoot"/>)
    /// 아래 텍스처는 **0개**였고, 대상이 0이면 `AscendImportRules.OnPreprocessTexture` 는
    /// 한 번도 실행되지 않는다.
    ///
    /// **왜 생성기인가**: 이미지 파일을 손으로 그려 넣으면 diff 가 바이너리 뭉치가 되고,
    /// 「이 얼룩이 왜 여기 있나」에 답할 근거가 사라진다. 픽셀이 코드에서 나오면 팔레트도
    /// 구조도 문장으로 읽히고, 헤드리스로 단정할 수 있다.
    ///
    /// **왜 `System.Random` 이 아닌가**: 목표는 「같은 입력이 같은 PNG」이고 그 PNG 는
    /// 캡처 베이스라인의 입력이 된다. `Random(seed)` 의 수열은 **런타임 구현에 딸린 계약**이라
    /// (.NET Framework/Mono/CoreCLR 사이에서 보장되지 않는 부분이 있다) 골든 파일의 근거로
    /// 쓸 수 없다. 그래서 정수 해시(lowbias32) 기반의 자체 난수를 쓰고, 노이즈 보간까지
    /// **전부 정수 고정소수점**으로 계산한다 — 부동소수 1 ULP 차이가 픽셀을 뒤집는 경로를
    /// 아예 없앤다. 어느 기기·어느 런타임에서 돌려도 바이트가 같다.
    ///
    /// **PNG 를 Unity 의 `EncodeToPNG` 로 굽지 않는 이유도 같다.** 인코더가 바뀌면 픽셀이
    /// 같아도 파일 바이트가 달라져 골든 해시가 헛되이 깨진다. 여기서는 필터 0 + 무압축
    /// deflate(stored block)로만 굽는다 — 사양에 맞는 정식 PNG 이고, 인코딩 경로에
    /// 구현 자유도가 없어 항상 같은 바이트가 나온다. 부작용으로 `ProfileTests` 가 zlib 없이
    /// 40 줄짜리 디코더로 픽셀을 되읽을 수 있다.
    ///
    /// **채택하지 않는다.** 여기서 만드는 것은 PNG 까지다. 어느 머티리얼에 어떻게 물릴지는
    /// 파일 맨 아래 「씬 오너 요청」에 적었다 — `.mat` 과 씬은 단일 소유이고, 이 저장소는
    /// 셰이더·머티리얼 일괄 교체를 이미 두 번 되돌린 이력이 있다(`UP-VIS-04` 6차 판정).
    /// </summary>
    public static class AscendTextureGen
    {
        /// <summary>
        /// 산출 폴더. **이 경로가 규칙의 일부다** — `AssetImportPaths.ClassifyTexture` 는
        /// `/ui/`·`/hud/`·`/vfx/`·`/particles/`·`/normal` 과 접미사 `_n`·`_ui`·`_vfx` 만
        /// 특별 취급하고 나머지는 <see cref="TextureAssetCategory.World"/> 로 떨어뜨린다.
        /// 여기 넷은 전부 벽·기계·상자 표면이므로 World 가 맞다. 파일명 접미사를 바꾸면
        /// 카테고리가 조용히 바뀐다는 뜻이기도 하다 — 그래서 테스트가 넷 다 World 인지 단정한다.
        /// </summary>
        public const string OutputFolder = "Assets/Prototype_Elevator/Art/Textures";

        [MenuItem("Ascend/Generate Placeholder Textures")]
        public static void GeneratePlaceholderTextures()
        {
            // 플레이 모드에서 `AssetDatabase.Refresh()` 를 부르면 도메인이 리로드되고
            // `Awake()` 에서 잡은 비직렬화 필드가 전부 null 이 된 채 오브젝트만 살아남는다.
            // 런은 계속 도는 것처럼 보이지만 결과는 오염된다 (`CLAUDE.md` 환경 주의사항).
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[상승] 플레이 모드에서는 생성하지 않는다 — Refresh 가 도메인을 리로드한다.");
                return;
            }

            string projectRoot = Directory.GetCurrentDirectory();
            string absoluteFolder = Path.Combine(projectRoot, OutputFolder.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(absoluteFolder);

            var sb = new StringBuilder(4096);
            sb.AppendLine("[상승] === 플레이스홀더 텍스처 생성 (UP-VIS-01 · UP-PLAT-05) ===");
            sb.AppendLine($"알고리즘: {AscendTextureSynth.AlgorithmId}   출력: {OutputFolder}");
            sb.AppendLine("스타일 락 출처: docs/VISUAL_BIBLE.md §2.1 「저해상도 손그림 픽셀 텍스처」 · §2.2 · §3 팔레트");
            sb.AppendLine();

            bool drifted = false;
            bool wrote = false;

            foreach (AscendTextureSynth.Spec spec in AscendTextureSynth.Specs())
            {
                byte[] png = AscendTextureSynth.Encode(spec);
                string filePath = Path.Combine(absoluteFolder, spec.FileName);
                string assetPath = OutputFolder + "/" + spec.FileName;

                string state;
                if (File.Exists(filePath))
                {
                    byte[] existing = File.ReadAllBytes(filePath);
                    if (AscendTextureSynth.BytesEqual(existing, png))
                    {
                        // 같으면 쓰지 않는다. 파일을 건드리면 재임포트가 돌고, 재임포트가 돌면
                        // 사람이 인스펙터에서 바꿔 둔 값과 규칙 중 무엇이 이겼는지 흐려진다.
                        state = "동일 — 쓰지 않았다";
                    }
                    else
                    {
                        File.WriteAllBytes(filePath, png);
                        state = "다르다 → 덮어썼다  ※ 결정론 위반";
                        drifted = true;
                        wrote = true;
                    }
                }
                else
                {
                    File.WriteAllBytes(filePath, png);
                    state = "새로 썼다";
                    wrote = true;
                }

                TextureAssetCategory category = AssetImportPaths.ClassifyTexture(assetPath);
                bool managed = AssetImportPaths.IsManaged(assetPath);
                TextureImportRule rule = TextureImportRuleSet.CodePreset.For(category);

                sb.AppendLine($"{spec.FileName}");
                sb.AppendLine($"  {spec.Size}×{spec.Size}px · 팔레트 {spec.Palette.Length}색 · 시드 0x{spec.Seed:X8} · {png.Length} bytes");
                sb.AppendLine($"  FNV-1a64 = 0x{AscendTextureSynth.Fnv1a64(png):X16}");
                sb.AppendLine($"  관할 {(managed ? "안" : "밖")} · 카테고리 {category} · 규칙 {rule.Describe()}");
                sb.AppendLine($"  {state}");
            }

            sb.AppendLine();
            if (drifted)
                sb.AppendLine("🚨 이미 있던 파일과 새로 만든 바이트가 다르다. 생성기가 결정론을 잃었거나 " +
                              "누가 PNG 를 손으로 고쳤다. 골든 해시 테스트가 같이 실패한다.");
            else
                sb.AppendLine("결정론 자체검사: 기존 파일과 재생성 결과가 바이트까지 같다.");

            sb.AppendLine("적용 관측: 첫 임포트에만 규칙이 걸린다(importSettingsMissing). `.meta` 를 " +
                          "손으로 만들지 않았으므로 이 넷은 처음 임포트되는 순간 규칙을 받는다. " +
                          "확인은 `Ascend/Report Import Rules` 의 「관할 아래 텍스처 N개」로 한다.");

            string report = sb.ToString();
            WriteArtifact("texture_gen.txt", report);
            Debug.Log(report);

            if (wrote) AssetDatabase.Refresh();
        }

        private static void WriteArtifact(string fileName, string body)
        {
            try
            {
                string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, fileName), body + "\n");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[상승] {fileName} 을 쓰지 못했다: {e.GetType().Name}: {e.Message}");
            }
        }
    }

    // <PURE>
    // ─────────────────────────────────────────────────────────────────────────────
    // 이 아래는 **Unity 타입을 하나도 참조하지 않는다.** 그래야 에디터를 띄우지 않고도
    // 같은 소스를 그대로 컴파일해 PNG 를 뽑아 대조할 수 있고, 「에디터에서 돌려야만
    // 확인되는 결정론」이라는 검증 불가능한 주장이 안 생긴다.
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 팔레트 인덱스만 칠하는 절차적 텍스처 합성기 + 결정론적 PNG 인코더.
    ///
    /// **핵심 제약: 한 텍스처의 모든 픽셀은 선언된 팔레트 중 정확히 하나다.** 중간색을
    /// 만들지 않는다. 이유가 둘이다 — ① `VISUAL_BIBLE.md` §3 「탁하고 제한된 색상」과
    /// §2.1 「저해상도 손그림 픽셀 텍스처」는 매끄러운 그라디언트의 반대말이다. 색을 5~6개로
    /// 못박으면 확대했을 때 픽셀 그레인이 그대로 보이고(§2.3 판정 기준 전자), 그러면서도
    /// 실루엣과 조명은 3D 가 만든다(후자). ② 테스트가 **관측 색 집합 == 선언 팔레트**를
    /// 정확히 단정할 수 있다. 범위 검사는 통과해도 실제로 무엇이 칠해졌는지 말해주지 않는다.
    /// </summary>
    public static class AscendTextureSynth
    {
        /// <summary>알고리즘 판. 픽셀 규칙을 바꾸면 이 문자열을 올리고 골든 해시를 다시 박는다.</summary>
        public const string AlgorithmId = "AscendSynth-v1";

        /// <summary>PNG 안에 provenance 를 남기는 tEXt 청크의 키워드.</summary>
        public const string TextKeyword = "Ascend";

        public enum Material { Iron = 0, Wood = 1, Brass = 2, Glass = 3 }

        /// <summary>
        /// 팔레트. 값은 `VISUAL_BIBLE.md` §3 의 재질·색조 표를 sRGB 로 옮긴 것이다.
        /// 전부 **채도 ≤ 0.50 · 명도 ≤ 0.55 · 색상각 15°~160°** 안에 있다 —
        /// 「바랜 산업용 색」이고, 적색 대역(15° 미만)은 위험 신호 전용이라 비워 둔다.
        /// 이 세 경계를 `ProfileTests` 가 독립적으로 다시 단정한다.
        /// </summary>
        // 산화 철: 목탄 검정 → 무쇠 → 밝은 무쇠 → 더러운 올리브 때 → 녹(어두움) → 녹(밝음)
        public static readonly int[] IronPalette = { 0x23231F, 0x33322C, 0x45443C, 0x3A3B2E, 0x4A362A, 0x5E4735 };
        // 얼룩진 목재: 어두운 결 → 중간 → 밝은 면 → 결 선(가장 어두움) → 올리브 얼룩
        public static readonly int[] WoodPalette = { 0x2A2018, 0x3D2E22, 0x52402F, 0x1E1710, 0x3A3524 };
        // 낡은 황동: 어두운 놋쇠 → 중간 → 닳은 밝은 면 → 녹청(차가운 회녹색) → 변색
        public static readonly int[] BrassPalette = { 0x443C28, 0x6E5F3D, 0x7F7050, 0x3E4A42, 0x2B2619 };
        // 오염된 유리: 어두운 면 → 기본 → 밝은 면 → 갈색 때 → 흘러내린 자국
        public static readonly int[] GlassPalette = { 0x262B27, 0x384039, 0x4C564E, 0x3A342A, 0x626E64 };

        public sealed class Spec
        {
            public readonly Material Kind;
            public readonly string FileName;
            public readonly int Size;
            public readonly uint Seed;
            public readonly int[] Palette;

            public Spec(Material kind, string fileName, int size, uint seed, int[] palette)
            {
                Kind = kind;
                FileName = fileName;
                Size = size;
                Seed = seed;
                Palette = palette;
            }
        }

        /// <summary>
        /// 넷뿐이다. Pass 3 은 완성도가 아니라 **락이 요구하는 것이 화면에 존재하는가**를
        /// 여는 단계이고, 재질이 늘수록 채택 판정 주기가 그만큼 늘어난다.
        /// 파일명에 `_n`·`_ui`·`_vfx` 접미사를 쓰지 않는다 — 쓰면 카테고리가 바뀐다.
        /// </summary>
        public static Spec[] Specs()
        {
            return new[]
            {
                new Spec(Material.Iron,  "TEX_Iron_Rust.png",     128, 0x5A170001u, IronPalette),
                new Spec(Material.Wood,  "TEX_Wood_Stained.png",  128, 0x5A170002u, WoodPalette),
                new Spec(Material.Brass, "TEX_Brass_Aged.png",     64, 0x5A170003u, BrassPalette),
                new Spec(Material.Glass, "TEX_Glass_Dirty.png",    64, 0x5A170004u, GlassPalette),
            };
        }

        // ── 합성 ──────────────────────────────────────────────────────────────

        /// <summary>팔레트 인덱스 버퍼를 만든다. 길이 = size*size, 값 = 팔레트 첨자.</summary>
        public static byte[] PaintIndices(Spec spec)
        {
            int size = spec.Size;
            var buffer = new byte[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    buffer[y * size + x] = (byte)PaintPixel(spec, x, y);
            return buffer;
        }

        private static int PaintPixel(Spec spec, int x, int y)
        {
            switch (spec.Kind)
            {
                case Material.Iron: return PaintIron(x, y, spec.Size, spec.Seed);
                case Material.Wood: return PaintWood(x, y, spec.Size, spec.Seed);
                case Material.Brass: return PaintBrass(x, y, spec.Size, spec.Seed);
                default: return PaintGlass(x, y, spec.Size, spec.Seed);
            }
        }

        /// <summary>
        /// 산화 철 판. 64px 판 네 장이 이음매로 갈리고 볼트가 박힌 구조 — 「장식이 아니라
        /// 기능과 마모가 먼저 보인다」(§4 금지 9번의 반대편)를 텍스처 수준에서 만든다.
        /// 녹은 **세로로 흘러야** 중력이 읽히므로 노이즈를 x 방향으로만 잘게 준다.
        /// </summary>
        private static int PaintIron(int x, int y, int size, uint seed)
        {
            int idx;

            int body = Fbm(x, y, 16, 16, size, seed, 3);
            if (body < 24000) idx = 0;
            else if (body < 46000) idx = 1;
            else idx = 2;

            int grime = Fbm(x, y, 32, 32, size, seed + 101u, 2);
            if (grime > 44000) idx = 3;

            // cellX 8 / cellY 32 → 가로로 잘고 세로로 길다 = 흘러내린 녹.
            // 문턱값은 한 번 조정했다 — 47000/56000 에서는 밝은 녹이 16384px 중 38px(0.2%)라
            // 선언만 있고 화면에 없는 색이 됐다. 44500/52500 에서 녹 전체가 5.8% → 13.6% 가 된다.
            // 「산화 철이 주 재질」(§3)이 텍스처 한 장 안에서도 읽혀야 한다.
            int rust = Fbm(x, y, 8, 32, size, seed + 211u, 3);
            if (rust > 44500) idx = 4;
            if (rust > 52500) idx = 5;

            int speck = Lattice(x, y, seed + 909u);
            if (speck < 2200) idx = 0;
            else if (speck > 63400) idx = 2;

            int mx = x & 63, my = y & 63;
            if (mx < 2 || my < 2) idx = 0;
            else if (mx == 2 || my == 2) idx = 3;

            // 볼트 넷. 마지막에 그린다 — 녹이 덮으면 판독이 먼저 사라진다(§4 금지 10번).
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
            if (d2 > 8) return current;
            return d2 <= 3 ? 2 : 0;
        }

        /// <summary>
        /// 얼룩진 목재. 32px 세로 판자 넷, 결은 판자를 따라 세로로 흐르고 판자마다 옹이가 하나.
        /// 옹이 y 위치는 판자 첨자의 해시라 판자마다 다르되 시드가 같으면 늘 같은 자리다.
        /// </summary>
        private static int PaintWood(int x, int y, int size, uint seed)
        {
            int plank = x >> 5;
            int inPlank = x & 31;

            // cellX 2 / cellY 32 → 세로로 길게 늘어난 결.
            int grain = Fbm(x, y, 2, 32, size, seed, 3);
            int idx;
            if (grain < 17000) idx = 3;
            else if (grain < 33000) idx = 0;
            else if (grain < 51000) idx = 1;
            else idx = 2;

            int stain = Fbm(x, y, 32, 32, size, seed + 77u, 2);
            if (stain > 45000 && idx >= 1) idx = 4;

            int knotY = Lattice(plank, 7, seed + 31u) % size;
            int kdx = inPlank - 16;
            int kdy = y - knotY;
            if (kdy > size / 2) kdy -= size;
            if (kdy < -size / 2) kdy += size;
            int kd = Isqrt(kdx * kdx * 3 + kdy * kdy);
            if (kd < 7) idx = (kd & 1) == 0 ? 3 : 1;

            int speck = Lattice(x, y, seed + 505u);
            if (speck < 2000) idx = 3;
            else if (speck > 63800) idx = 2;

            if (inPlank == 0) idx = 3;
            else if (inPlank == 1) idx = 0;
            return idx;
        }

        /// <summary>
        /// 낡은 황동. 16px 소판 격자에 위/왼쪽 밝고 아래/오른쪽 어두운 베벨 — 계기 베젤과
        /// 배관 조인트용이다. **국소 액센트 전용**이라 타일이 작다(§3, §4 금지 5번).
        /// 결은 가로 브러시라 노이즈를 세로로만 잘게 준다.
        /// </summary>
        private static int PaintBrass(int x, int y, int size, uint seed)
        {
            int brush = Fbm(x, y, 16, 2, size, seed, 3);
            int idx;
            if (brush < 19000) idx = 4;
            else if (brush < 38000) idx = 0;
            else if (brush < 55000) idx = 1;
            else idx = 2;

            int patina = Fbm(x, y, 16, 16, size, seed + 61u, 2);
            if (patina > 48000) idx = 3;

            int speck = Lattice(x, y, seed + 313u);
            if (speck < 1800) idx = 4;
            else if (speck > 63900) idx = 2;

            int mx = x & 15, my = y & 15;
            if (mx == 0 || my == 0) idx = 2;
            else if (mx == 15 || my == 15) idx = 4;
            return idx;
        }

        /// <summary>
        /// 오염된 유리. 세로로 흘러내린 자국 + 갈색 때. 색상각이 전부 120°~135° 라
        /// 「차가운 회녹색」(§2.1)이 이 한 장에 그대로 들어 있다.
        /// </summary>
        private static int PaintGlass(int x, int y, int size, uint seed)
        {
            int idx = 1;

            int drip = Fbm(x, y, 2, 32, size, seed, 3);
            if (drip > 43000) idx = 2;
            if (drip > 54000) idx = 4;

            int grime = Fbm(x, y, 8, 8, size, seed + 91u, 2);
            if (grime > 49000) idx = 3;
            if (grime < 14000) idx = 0;

            int speck = Lattice(x, y, seed + 707u);
            if (speck < 2500) idx = 0;
            else if (speck > 63000) idx = 4;
            return idx;
        }

        // ── 결정론적 노이즈 (전부 정수) ───────────────────────────────────────

        /// <summary>lowbias32 정수 해시. 부동소수가 없으므로 런타임·JIT 과 무관하게 같다.</summary>
        private static uint Mix(uint v)
        {
            unchecked
            {
                v ^= v >> 16;
                v *= 0x7FEB352Du;
                v ^= v >> 15;
                v *= 0x846CA68Bu;
                v ^= v >> 16;
                return v;
            }
        }

        /// <summary>격자점 난수. 0~65535.</summary>
        private static int Lattice(int x, int y, uint seed)
        {
            unchecked
            {
                return (int)(Mix((uint)x * 0x9E3779B1u + (uint)y * 0x85EBCA77u + seed) >> 16);
            }
        }

        /// <summary>Q16 smoothstep — t*t*(3-2t). 입력·출력 모두 0~65536.</summary>
        private static long Smooth(int t)
        {
            long q = t;
            return (q * q * (196608L - 2L * q)) >> 32;
        }

        private static int Wrap(int v, int period)
        {
            v %= period;
            return v < 0 ? v + period : v;
        }

        /// <summary>
        /// 이중선형 value noise. `cellX`·`cellY` 는 2의 거듭제곱이고 `size` 를 나눠야
        /// **격자가 텍스처 경계에서 이어진다**(타일링). 벽에 반복해 붙일 물건이라 이음매가
        /// 보이면 그 자체로 판독을 해친다. 0~65535 를 돌려준다.
        /// </summary>
        private static int Noise(int px, int py, int cellX, int cellY, int size, uint seed)
        {
            int periodX = size / cellX;
            int periodY = size / cellY;

            int xi = px / cellX, yi = py / cellY;
            int tx = (px - xi * cellX) * 65536 / cellX;
            int ty = (py - yi * cellY) * 65536 / cellY;
            long sx = Smooth(tx), sy = Smooth(ty);

            int x0 = Wrap(xi, periodX), x1 = Wrap(xi + 1, periodX);
            int y0 = Wrap(yi, periodY), y1 = Wrap(yi + 1, periodY);

            int n00 = Lattice(x0, y0, seed), n10 = Lattice(x1, y0, seed);
            int n01 = Lattice(x0, y1, seed), n11 = Lattice(x1, y1, seed);

            long a = n00 + (((long)(n10 - n00) * sx) >> 16);
            long b = n01 + (((long)(n11 - n01) * sx) >> 16);
            return (int)(a + (((b - a) * sy) >> 16));
        }

        /// <summary>옥타브를 8:4:2:1 정수 가중으로 합친다. 나눗셈이 정수라 값이 흔들리지 않는다.</summary>
        private static int Fbm(int px, int py, int cellX, int cellY, int size, uint seed, int octaves)
        {
            int sum = 0, total = 0, weight = 8;
            for (int o = 0; o < octaves; o++)
            {
                int cx = cellX >> o, cy = cellY >> o;
                if (cx < 1 || cy < 1) break;
                sum += Noise(px, py, cx, cy, size, seed + (uint)o * 0x2545F491u) * weight;
                total += weight;
                if (weight > 1) weight >>= 1;
            }
            return total == 0 ? 0 : sum / total;
        }

        private static int Isqrt(int v)
        {
            if (v <= 0) return 0;
            int r = 0;
            while ((r + 1) * (r + 1) <= v) r++;
            return r;
        }

        // ── PNG (필터 0 + 무압축 deflate) ─────────────────────────────────────

        /// <summary>
        /// 스펙 하나를 완성된 PNG 바이트로. RGB 8bit(색상 타입 2), 인터레이스 없음.
        /// tEXt 청크에 알고리즘·시드·크기·팔레트를 적어 **파일 자신이 출처를 들고 다니게** 한다 —
        /// 나중에 누가 이 PNG 를 보고 「어디서 왔나」를 묻지 않아도 되게.
        /// </summary>
        public static byte[] Encode(Spec spec)
        {
            byte[] indices = PaintIndices(spec);
            int size = spec.Size;

            // 스캔라인 = [필터 0][RGB × width]
            int stride = 1 + size * 3;
            var raw = new byte[stride * size];
            for (int y = 0; y < size; y++)
            {
                int row = y * stride;
                raw[row] = 0;
                for (int x = 0; x < size; x++)
                {
                    int rgb = spec.Palette[indices[y * size + x]];
                    int at = row + 1 + x * 3;
                    raw[at] = (byte)((rgb >> 16) & 0xFF);
                    raw[at + 1] = (byte)((rgb >> 8) & 0xFF);
                    raw[at + 2] = (byte)(rgb & 0xFF);
                }
            }

            var png = new MemoryStream(raw.Length + 512);
            byte[] signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            png.Write(signature, 0, signature.Length);

            var ihdr = new byte[13];
            WriteBigEndian(ihdr, 0, size);
            WriteBigEndian(ihdr, 4, size);
            ihdr[8] = 8;   // bit depth
            ihdr[9] = 2;   // color type: truecolor RGB
            ihdr[10] = 0;  // compression: deflate
            ihdr[11] = 0;  // filter method 0
            ihdr[12] = 0;  // no interlace
            WriteChunk(png, "IHDR", ihdr);

            WriteChunk(png, "tEXt", TextChunk(spec));
            WriteChunk(png, "IDAT", ZlibStored(raw));
            WriteChunk(png, "IEND", new byte[0]);
            return png.ToArray();
        }

        private static byte[] TextChunk(Spec spec)
        {
            // 숫자 → 문자열을 **전부 불변 문화권으로** 고정한다. `Append(int)` 는 현재 문화권을
            // 타고, 이 문자열은 파일 바이트가 되어 골든 해시에 들어간다 — 로케일이 다른 기기에서
            // 해시가 깨지는 종류의 실패는 원인을 찾는 데 하루가 든다.
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            var value = new StringBuilder(160);
            value.Append("algo=").Append(AlgorithmId);
            value.Append(";seed=0x").Append(spec.Seed.ToString("X8", culture));
            value.Append(";size=").Append(spec.Size.ToString(culture));
            value.Append('x').Append(spec.Size.ToString(culture));
            value.Append(";palette=");
            for (int i = 0; i < spec.Palette.Length; i++)
            {
                if (i > 0) value.Append(',');
                value.Append(spec.Palette[i].ToString("X6", culture));
            }

            byte[] keyword = Latin1(TextKeyword);
            byte[] body = Latin1(value.ToString());
            var chunk = new byte[keyword.Length + 1 + body.Length];
            Buffer.BlockCopy(keyword, 0, chunk, 0, keyword.Length);
            chunk[keyword.Length] = 0;
            Buffer.BlockCopy(body, 0, chunk, keyword.Length + 1, body.Length);
            return chunk;
        }

        /// <summary>ASCII 만 쓴다 — tEXt 는 Latin-1 이고, 인코더에 자유도를 주지 않는 편이 안전하다.</summary>
        private static byte[] Latin1(string text)
        {
            var bytes = new byte[text.Length];
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                bytes[i] = c < 256 ? (byte)c : (byte)'?';
            }
            return bytes;
        }

        /// <summary>
        /// zlib 스트림을 **무압축 stored block** 으로만 만든다. 압축기를 쓰면 구현·버전에 따라
        /// 바이트가 달라져 골든 해시가 헛되이 깨진다. 대신 파일이 크다 — 128×128 이 약 49KB 다.
        /// 플레이스홀더 넷의 합이 100KB 대이므로 감수한다.
        /// </summary>
        private static byte[] ZlibStored(byte[] data)
        {
            var stream = new MemoryStream(data.Length + 64);
            stream.WriteByte(0x78); // CM=8, CINFO=7
            stream.WriteByte(0x01); // FCHECK 가 맞는 조합: 0x7801 % 31 == 0

            int offset = 0;
            do
            {
                int length = Math.Min(65535, data.Length - offset);
                bool last = offset + length >= data.Length;
                stream.WriteByte((byte)(last ? 1 : 0)); // BFINAL, BTYPE=00(stored)
                stream.WriteByte((byte)(length & 0xFF));
                stream.WriteByte((byte)((length >> 8) & 0xFF));
                stream.WriteByte((byte)(~length & 0xFF));
                stream.WriteByte((byte)((~length >> 8) & 0xFF));
                stream.Write(data, offset, length);
                offset += length;
            }
            while (offset < data.Length);

            uint adler = Adler32(data);
            stream.WriteByte((byte)((adler >> 24) & 0xFF));
            stream.WriteByte((byte)((adler >> 16) & 0xFF));
            stream.WriteByte((byte)((adler >> 8) & 0xFF));
            stream.WriteByte((byte)(adler & 0xFF));
            return stream.ToArray();
        }

        private static void WriteChunk(MemoryStream stream, string type, byte[] data)
        {
            var header = new byte[4];
            WriteBigEndian(header, 0, data.Length);
            stream.Write(header, 0, 4);

            byte[] typeBytes = Latin1(type);
            stream.Write(typeBytes, 0, 4);
            stream.Write(data, 0, data.Length);

            uint crc = Crc32(typeBytes, data);
            var tail = new byte[4];
            WriteBigEndian(tail, 0, (int)crc);
            stream.Write(tail, 0, 4);
        }

        private static void WriteBigEndian(byte[] target, int offset, int value)
        {
            target[offset] = (byte)((value >> 24) & 0xFF);
            target[offset + 1] = (byte)((value >> 16) & 0xFF);
            target[offset + 2] = (byte)((value >> 8) & 0xFF);
            target[offset + 3] = (byte)(value & 0xFF);
        }

        private static uint Adler32(byte[] data)
        {
            uint a = 1, b = 0;
            for (int i = 0; i < data.Length; i++)
            {
                a = (a + data[i]) % 65521u;
                b = (b + a) % 65521u;
            }
            return (b << 16) | a;
        }

        private static readonly uint[] CrcTable = BuildCrcTable();

        private static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                table[n] = c;
            }
            return table;
        }

        private static uint Crc32(byte[] first, byte[] second)
        {
            uint c = 0xFFFFFFFFu;
            for (int i = 0; i < first.Length; i++) c = CrcTable[(c ^ first[i]) & 0xFF] ^ (c >> 8);
            for (int i = 0; i < second.Length; i++) c = CrcTable[(c ^ second[i]) & 0xFF] ^ (c >> 8);
            return c ^ 0xFFFFFFFFu;
        }

        // ── 대조용 ────────────────────────────────────────────────────────────

        /// <summary>골든 해시. 테스트가 같은 식으로 계산해 박아 둔 상수와 맞춘다.</summary>
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

        public static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
    // </PURE>
}

// 씬 배선 필요: 없다. 생성기는 씬을 열지 않는다.
//
// ── 씬 오너 요청 (이 세션에서 하지 않았다) ──────────────────────────────────────
// 텍스처를 머티리얼에 물리는 것은 `.mat` 수정이고 단일 소유다. 그리고 이 저장소는
// 셰이더·머티리얼 일괄 교체를 이미 두 번 되돌렸다(`UP-VIS-04`, 6차 판정 「순손실」).
// 그래서 **PNG 까지만** 만들었다. 채택할 때 다음 순서를 지킨다.
//
//   1) `Ascend/Generate Placeholder Textures` 를 한 번 돌린다(파일이 이미 있으면
//      「동일 — 쓰지 않았다」가 넷 다 찍혀야 한다. 하나라도 「덮어썼다」면 결정론이 깨진 것이다).
//   2) `Ascend/Report Import Rules` 를 돌려 `Logs/import_rules.txt` 의
//      「관할 아래 텍스처 N개」가 **0 → 4** 로 바뀌었는지 본다. 이것이 `UP-PLAT-05` 의
//      「규칙이 적용됐다」에 대한 유일한 증거다.
//   3) 머티리얼 배정 — **한 번에 하나씩, 매번 캡처·판정을 받는다.**
//        · `MAT_Ascend_Iron.mat`  ← `TEX_Iron_Rust.png`    (`_BaseMap`, 타일링 권장 2×2)
//        · `MAT_Ascend_Wood.mat`  ← `TEX_Wood_Stained.png` (`_BaseMap`, 타일링 1×1)
//        · `MAT_Ascend_Brass.mat` ← `TEX_Brass_Aged.png`   (`_BaseMap`, 타일링 4×4 — 액센트라 작게)
//        · 유리는 대응 머티리얼이 없다. `TEX_Glass_Dirty.png` 는 통관·계기 커버용이며
//          머티리얼을 새로 만들어야 하므로 이번 요청에서 제외한다.
//      ⚠️ `MAT_Ascend_*` 3종은 `Ascend/Stylized` 셰이더를 쓴다. 그 셰이더에 `_BaseMap`
//      텍스처 슬롯이 실제로 있는지 **먼저 확인한다** — 없으면 배정이 조용히 무시되고
//      「물렸는데 화면이 그대로」가 된다. 없으면 셰이더 소유자에게 슬롯 추가를 요청한다.
//      또한 `_EmissionColor` 는 절대 제거하지 않는다(`SpinBoardView` 등 셋이 그 이름을 쓴다).
//   4) 임포터 설정 요청 (데이터 소유자): World 규칙은 `filterMode` 를 다루지 않아 임포트
//      기본값 Bilinear 가 걸린다. 128px 손그림을 Bilinear + 밉맵으로 보면 확대 시 픽셀
//      그레인이 뭉개져 §2.3 의 판정 기준 전자(「확대하면 픽셀 그레인이 보여야 한다」)를
//      못 넘길 수 있다. `TextureImportRule` 에 `FilterMode` 축을 추가해 World 를 Point 로
//      두는 것을 검토한다 — `Scripts/Data/Profiles/AssetImportRules.cs` 는 이 세션의 소유가
//      아니라 손대지 않았다.
