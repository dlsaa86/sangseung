using System;

namespace Ascend.Prototype.Data.Profiles
{
    /// <summary>
    /// 텍스처가 속하는 갈래. 카테고리 판정은 **경로 규칙**으로만 한다 —
    /// 파일마다 손으로 태그를 붙이는 방식은 붙이지 않은 파일이 조용히 기본값으로
    /// 들어가고, 그 사실을 아무도 눈치채지 못한다(`UP-PLAT-05`).
    /// </summary>
    public enum TextureAssetCategory
    {
        /// <summary>UI·HUD·결과판. 확대돼도 글자가 읽혀야 하므로 크기를 크게 두고 밉맵을 끈다.</summary>
        Ui = 0,

        /// <summary>월드 표면(벽·바닥·기계). 대부분이 여기다.</summary>
        World = 1,

        /// <summary>노멀맵. 색이 아니라 벡터라 sRGB 로 읽으면 조명이 어긋난다.</summary>
        NormalMap = 2,

        /// <summary>파티클·VFX. 화면을 덮는 반투명이라 해상도보다 오버드로우가 먼저 아프다.</summary>
        Vfx = 3,
    }

    /// <summary>
    /// 압축 강도. `UnityEditor.TextureImporterCompression` 을 그대로 쓰지 않는 이유는
    /// 이 파일이 런타임 어셈블리에 있기 때문이다 — 에디터 열거형을 여기서 참조하면
    /// 빌드가 깨진다. 매핑은 `Assets/Editor/AscendImportRules.cs` 한 곳에서만 한다.
    /// </summary>
    public enum TextureImportCompression
    {
        Uncompressed = 0,
        LowQuality = 1,
        NormalQuality = 2,
        HighQuality = 3,
    }

    /// <summary>
    /// 오디오가 속하는 갈래. PRD §13.4 의 「무압축 원본을 런타임에 직접 사용하지 않는다」와
    /// 「에셋 카테고리별 Preset」이 오디오에도 걸린다 — `UP-AUD-05` 의 나머지 절반이다.
    ///
    /// 세 갈래로 나누는 근거는 **메모리와 지연이 서로 반대 방향으로 아프다**는 것이다.
    /// 짧은 효과음은 지연이 0이어야 하고(미리 풀어 둔다), 루프는 상시 살아 있으니
    /// 메모리를 아껴야 하며(압축한 채 둔다), 음성은 길어서 통째로 올릴 수 없다(스트리밍).
    /// </summary>
    public enum AudioAssetClass
    {
        /// <summary>1초 안팎의 사건음 — 정화·클릭·레버. 지연이 곧 조작감이다.</summary>
        ShortEffect = 0,

        /// <summary>기계 험·앰비언스처럼 계속 도는 소리. 개수는 적고 수명이 길다.</summary>
        Loop = 1,

        /// <summary>승객 음성·내레이션. 길고, 동시에 여러 개가 나지 않는다.</summary>
        Voice = 2,
    }

    /// <summary>
    /// 적재 방식. `UnityEngine.AudioClipLoadType` 과 값이 같지만 이름을 따로 두는 이유는
    /// <see cref="TextureImportCompression"/> 과 같다 — 이 어셈블리가 임포터 열거형에
    /// 의존하지 않게 한다.
    /// </summary>
    public enum AudioImportLoadType
    {
        DecompressOnLoad = 0,
        CompressedInMemory = 1,
        Streaming = 2,
    }

    /// <summary>압축 포맷. Pcm 은 「무압축 원본」이며 규칙상 어느 갈래에도 기본값이 아니다.</summary>
    public enum AudioImportCompression
    {
        Pcm = 0,
        Adpcm = 1,
        Vorbis = 2,
    }

    /// <summary>텍스처 한 갈래에 적용할 임포트 값.</summary>
    [Serializable]
    public struct TextureImportRule
    {
        public TextureAssetCategory Category;

        /// <summary>임포터 최대 변 길이. 2의 거듭제곱이어야 한다.</summary>
        public int MaxSize;

        public TextureImportCompression Compression;

        /// <summary>밉맵 생성. UI 는 화면에 1:1 로 놓이므로 만들면 메모리만 33% 더 쓴다.</summary>
        public bool GenerateMipmaps;

        /// <summary>알파를 투명도로 다룰 것인가. 컷아웃·UI 에서 가장자리 검은 테를 막는다.</summary>
        public bool AlphaIsTransparency;

        /// <summary>색 공간. 노멀맵·마스크는 반드시 꺼야 한다.</summary>
        public bool SRgb;

        public TextureImportRule(TextureAssetCategory category, int maxSize,
            TextureImportCompression compression, bool generateMipmaps,
            bool alphaIsTransparency, bool sRgb)
        {
            Category = category;
            MaxSize = maxSize;
            Compression = compression;
            GenerateMipmaps = generateMipmaps;
            AlphaIsTransparency = alphaIsTransparency;
            SRgb = sRgb;
        }

        public string Describe()
        {
            return $"{Category}: ≤{MaxSize}px · {Compression} · 밉맵 {(GenerateMipmaps ? "켬" : "끔")} · " +
                   $"알파투명 {(AlphaIsTransparency ? "켬" : "끔")} · sRGB {(SRgb ? "켬" : "끔")}";
        }
    }

    /// <summary>오디오 한 갈래에 적용할 임포트 값.</summary>
    [Serializable]
    public struct AudioImportRule
    {
        public AudioAssetClass Class;
        public AudioImportLoadType LoadType;
        public AudioImportCompression Compression;

        /// <summary>Vorbis 품질(0~1). ADPCM·PCM 은 이 값을 쓰지 않는다.</summary>
        public float Quality;

        /// <summary>모노 강제. 좁은 칸 안의 사건음은 스테레오일 이유가 없다.</summary>
        public bool ForceToMono;

        public AudioImportRule(AudioAssetClass audioClass, AudioImportLoadType loadType,
            AudioImportCompression compression, float quality, bool forceToMono)
        {
            Class = audioClass;
            LoadType = loadType;
            Compression = compression;
            Quality = quality;
            ForceToMono = forceToMono;
        }

        public string Describe()
        {
            return $"{Class}: {LoadType} · {Compression} · 품질 {Quality:0.00} · " +
                   $"모노 {(ForceToMono ? "강제" : "유지")}";
        }
    }

    /// <summary>
    /// 에셋 경로 → 갈래. 순수 함수라 씬도 에디터도 없이 검사할 수 있다.
    ///
    /// **판정 순서가 규칙의 일부다.** 노멀맵이 UI 폴더 안에 있을 수는 없지만,
    /// VFX 폴더 안의 노멀맵은 실제로 있을 수 있다. 그래서 노멀맵을 먼저 본다 —
    /// sRGB 를 잘못 켜는 것이 크기를 잘못 잡는 것보다 눈에 띄게 틀린 그림을 만든다.
    /// </summary>
    public static class AssetImportPaths
    {
        /// <summary>규칙을 적용할 루트. 밖은 손대지 않는다 — TMP·튜토리얼 에셋까지 바꾸면 원인 추적이 끊긴다.</summary>
        public const string ManagedRoot = "Assets/Prototype_Elevator/";

        /// <summary>이 경로가 우리 규칙의 관할인가.</summary>
        public static bool IsManaged(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            return Normalize(assetPath).StartsWith(ManagedRoot, StringComparison.OrdinalIgnoreCase);
        }

        public static TextureAssetCategory ClassifyTexture(string assetPath)
        {
            string path = Normalize(assetPath);
            string file = FileNameWithoutExtension(path);

            if (Contains(path, "/normal") || Contains(path, "/normalmaps/")
                || EndsWith(file, "_n") || EndsWith(file, "_normal") || EndsWith(file, "_nrm"))
                return TextureAssetCategory.NormalMap;

            if (Contains(path, "/ui/") || Contains(path, "/hud/") || EndsWith(file, "_ui"))
                return TextureAssetCategory.Ui;

            if (Contains(path, "/vfx/") || Contains(path, "/particles/") || EndsWith(file, "_vfx"))
                return TextureAssetCategory.Vfx;

            return TextureAssetCategory.World;
        }

        public static AudioAssetClass ClassifyAudio(string assetPath)
        {
            string path = Normalize(assetPath);
            string file = FileNameWithoutExtension(path);

            if (Contains(path, "/voice/") || Contains(path, "/vo/")
                || StartsWith(file, "vo_") || Contains(file, "_voice"))
                return AudioAssetClass.Voice;

            if (Contains(path, "/loops/") || Contains(path, "/ambience/")
                || Contains(file, "_loop") || Contains(file, "_amb"))
                return AudioAssetClass.Loop;

            return AudioAssetClass.ShortEffect;
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            return path.Replace('\\', '/');
        }

        private static string FileNameWithoutExtension(string path)
        {
            int slash = path.LastIndexOf('/');
            string file = slash >= 0 ? path.Substring(slash + 1) : path;
            int dot = file.LastIndexOf('.');
            if (dot > 0) file = file.Substring(0, dot);
            return file.ToLowerInvariant();
        }

        private static bool Contains(string haystack, string needle)
            => haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool StartsWith(string value, string prefix)
            => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

        private static bool EndsWith(string value, string suffix)
            => value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 텍스처 규칙 네 갈래의 값 사본. <c>SourceName</c> 이 「코드 프리셋」이면 에셋이
    /// 배선되지 않았다는 뜻이다 — 리포트에 그대로 찍혀야 검사가 공허하게 통과하지 않는다.
    /// </summary>
    public readonly struct TextureImportRuleSet
    {
        public const int CategoryCount = 4;

        /// <summary>에셋이 없을 때 쓰는 이름. 리포트에서 이 문자열을 찾는 것으로 폴백을 검출한다.</summary>
        public const string CodePresetName = "코드 프리셋";

        public readonly string SourceName;
        private readonly TextureImportRule[] _rules;

        public TextureImportRuleSet(string sourceName, TextureImportRule[] rules)
        {
            SourceName = string.IsNullOrEmpty(sourceName) ? CodePresetName : sourceName;
            _rules = rules;
        }

        public bool IsCodePreset => SourceName == CodePresetName;

        public TextureImportRule For(TextureAssetCategory category)
        {
            TextureImportRule[] rules = _rules;
            if (rules != null)
            {
                for (int i = 0; i < rules.Length; i++)
                    if (rules[i].Category == category) return rules[i];
            }
            return Preset(category);
        }

        /// <summary>
        /// 코드 프리셋. 값의 근거는 각 줄 주석에 있다 — 「왜 이 숫자인가」가 없으면
        /// 다음 사람이 바꿔도 되는 값인지 알 수 없다.
        /// </summary>
        public static TextureImportRule Preset(TextureAssetCategory category)
        {
            switch (category)
            {
                // UI 는 1:1 로 놓이므로 밉맵이 낭비다. 결과판 심볼 3종이 1초 안에 구분돼야
                // 하므로(PRD §13.4) 압축은 표준까지만 내린다.
                case TextureAssetCategory.Ui:
                    return new TextureImportRule(TextureAssetCategory.Ui, 2048,
                        TextureImportCompression.NormalQuality, false, true, true);

                // 노멀맵은 sRGB 를 끄고 압축 품질을 올린다. 블록 압축의 색 뭉개짐이
                // 노멀에서는 조명 얼룩으로 보인다.
                case TextureAssetCategory.NormalMap:
                    return new TextureImportRule(TextureAssetCategory.NormalMap, 1024,
                        TextureImportCompression.HighQuality, true, false, false);

                // VFX 는 화면을 덮는 반투명이라 해상도보다 오버드로우가 먼저 아프다.
                // 512 로 조이는 것이 `VisualQualityProfile.OverdrawBudget` 과 같은 방향이다.
                case TextureAssetCategory.Vfx:
                    return new TextureImportRule(TextureAssetCategory.Vfx, 512,
                        TextureImportCompression.LowQuality, true, true, true);

                // 월드 표면. 대부분이 여기이고, 카메라가 벽에 붙는 좁은 칸이므로 1024 가 상한이다.
                default:
                    return new TextureImportRule(TextureAssetCategory.World, 1024,
                        TextureImportCompression.NormalQuality, true, false, true);
            }
        }

        public static TextureImportRule[] Presets()
        {
            return new[]
            {
                Preset(TextureAssetCategory.Ui),
                Preset(TextureAssetCategory.World),
                Preset(TextureAssetCategory.NormalMap),
                Preset(TextureAssetCategory.Vfx),
            };
        }

        public static TextureImportRuleSet CodePreset
        {
            get { return new TextureImportRuleSet(CodePresetName, Presets()); }
        }
    }

    /// <summary>오디오 규칙 세 갈래의 값 사본.</summary>
    public readonly struct AudioImportRuleSet
    {
        public const int ClassCount = 3;
        public const string CodePresetName = "코드 프리셋";

        public readonly string SourceName;
        private readonly AudioImportRule[] _rules;

        public AudioImportRuleSet(string sourceName, AudioImportRule[] rules)
        {
            SourceName = string.IsNullOrEmpty(sourceName) ? CodePresetName : sourceName;
            _rules = rules;
        }

        public bool IsCodePreset => SourceName == CodePresetName;

        public AudioImportRule For(AudioAssetClass audioClass)
        {
            AudioImportRule[] rules = _rules;
            if (rules != null)
            {
                for (int i = 0; i < rules.Length; i++)
                    if (rules[i].Class == audioClass) return rules[i];
            }
            return Preset(audioClass);
        }

        /// <summary>
        /// 코드 프리셋. **세 갈래가 서로 다른 값을 가져야 한다** — 같으면 「구분했다」가
        /// 이름만 셋이고 동작은 하나라는 뜻이고, 그것이 `UP-AUD-05` 가 미충족이었던 이유다.
        /// 테스트가 셋의 상이함을 직접 검사한다.
        /// </summary>
        public static AudioImportRule Preset(AudioAssetClass audioClass)
        {
            switch (audioClass)
            {
                // 사건음. 눌린 순간 나야 하므로 미리 풀어 둔다. ADPCM 은 디코드가 거의
                // 공짜라 DecompressOnLoad 와 짝이 맞는다. PCM(무압축)은 쓰지 않는다 —
                // PRD §13.4 「무압축 원본을 런타임에 직접 사용하지 않는다」.
                case AudioAssetClass.ShortEffect:
                    return new AudioImportRule(AudioAssetClass.ShortEffect,
                        AudioImportLoadType.DecompressOnLoad, AudioImportCompression.Adpcm, 1.00f, true);

                // 루프. 런 내내 살아 있으니 압축한 채 메모리에 둔다. 험은 저역이라
                // Vorbis 0.5 에서도 열화가 들리지 않는다.
                case AudioAssetClass.Loop:
                    return new AudioImportRule(AudioAssetClass.Loop,
                        AudioImportLoadType.CompressedInMemory, AudioImportCompression.Vorbis, 0.50f, false);

                // 음성. 길고 동시 재생이 없으므로 스트리밍이 맞다. 명료도가 중요해
                // 품질을 루프보다 높인다.
                default:
                    return new AudioImportRule(AudioAssetClass.Voice,
                        AudioImportLoadType.Streaming, AudioImportCompression.Vorbis, 0.70f, true);
            }
        }

        public static AudioImportRule[] Presets()
        {
            return new[]
            {
                Preset(AudioAssetClass.ShortEffect),
                Preset(AudioAssetClass.Loop),
                Preset(AudioAssetClass.Voice),
            };
        }

        public static AudioImportRuleSet CodePreset
        {
            get { return new AudioImportRuleSet(CodePresetName, Presets()); }
        }
    }
}

// 씬 배선 필요: 없다. 순수 데이터·순수 함수다.
// 에셋 생성 필요: 없다. 규칙 값은 기존 `VisualQualityProfile.asset` ·
//   `AudioMixProfile.asset` 에 새로 붙은 배열에서 읽는다. 두 에셋은 이 배열이 없는
//   상태로 이미 디스크에 있으므로 **빈 배열로 역직렬화되고 코드 프리셋으로 폴백한다.**
//   그 사실이 리포트에 「코드 프리셋」으로 찍힌다 — 값을 에셋에 굳히려면
//   인스펙터에서 각 프로파일의 Reset 을 한 번 눌러야 한다(에셋 수정은 씬 오너 몫).
