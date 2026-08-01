using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Ascend.Prototype.Data.Profiles;

namespace Ascend.Prototype.EditorTools
{
    /// <summary>
    /// 텍스처·오디오 임포트 규칙을 코드로 강제한다 — PRD §13.4 의
    /// 「텍스처 임포트 크기·압축·밉맵·알파는 에셋 카테고리별 Preset 으로 관리한다」와
    /// 「무압축 원본을 런타임에 직접 사용하지 않는다」 (`UP-PLAT-05` · `UP-AUD-05`).
    ///
    /// **왜 `.preset` 파일이 아닌가**: `.preset` 은 직렬화 에셋이라 diff 가 fileID 뭉치로만
    /// 읽히고, 규칙이 조용히 바뀌어도 코드 리뷰에 걸리지 않는다. 이 저장소는 YAML 에셋을
    /// 병렬로 건드려 손상시킨 이력 때문에 그 부류 전체를 단일 소유로 묶어 두고 있다
    /// (`CLAUDE.md` 소유권 규칙). 규칙이 코드에 있으면 diff 가 문장으로 읽히고,
    /// 규칙 값 자체는 프로파일 에셋에 남아 데이터화 요구(PRD §14.1)도 함께 만족한다.
    ///
    /// **적용 시점**: 첫 임포트에만 적용한다(<c>importSettingsMissing</c>).
    /// 사람이 인스펙터에서 일부러 바꿔 둔 값을, 파일을 옮겼다는 이유만으로 되돌리면
    /// 「내가 바꾼 게 왜 사라지지」가 되고 그건 규칙이 아니라 사고다.
    /// 기존 에셋을 규칙에 맞추려면 의도적으로 재임포트해야 한다.
    ///
    /// **관할 범위**: <see cref="AssetImportPaths.ManagedRoot"/> 아래만. TextMesh Pro·튜토리얼
    /// 샘플까지 바꾸면 그쪽이 깨졌을 때 원인이 우리 규칙인지 원본인지 구분되지 않는다.
    /// </summary>
    public sealed class AscendImportRules : AssetPostprocessor
    {
        private const string ProfileFolder = "Assets/Prototype_Elevator/Data/Profiles";
        private const string QualityProfilePath = ProfileFolder + "/VisualQualityProfile.asset";
        private const string AudioProfilePath = ProfileFolder + "/AudioMixProfile.asset";

        // ── 텍스처 ────────────────────────────────────────────────────────────

        private void OnPreprocessTexture()
        {
            if (!ShouldApply()) return;

            var importer = assetImporter as TextureImporter;
            if (importer == null) return;

            TextureAssetCategory category = AssetImportPaths.ClassifyTexture(assetPath);
            TextureImportRule rule = LoadTextureRules().For(category);

            importer.maxTextureSize = rule.MaxSize;
            importer.textureCompression = ToImporterCompression(rule.Compression);
            importer.mipmapEnabled = rule.GenerateMipmaps;
            importer.alphaIsTransparency = rule.AlphaIsTransparency;
            importer.sRGBTexture = rule.SRgb;

            // 노멀맵은 타입까지 바꿔야 한다. 값만 맞추고 타입이 Default 로 남으면
            // 압축 포맷이 노멀 전용이 아니게 되고, 그 차이는 조명이 얼룩질 때까지 안 보인다.
            if (category == TextureAssetCategory.NormalMap)
                importer.textureType = UnityEditor.TextureImporterType.NormalMap;
        }

        // ── 오디오 ────────────────────────────────────────────────────────────

        private void OnPreprocessAudio()
        {
            if (!ShouldApply()) return;

            var importer = assetImporter as AudioImporter;
            if (importer == null) return;

            AudioAssetClass audioClass = AssetImportPaths.ClassifyAudio(assetPath);
            AudioImportRule rule = LoadAudioRules().For(audioClass);

            importer.forceToMono = rule.ForceToMono;

            UnityEditor.AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = ToImporterLoadType(rule.LoadType);
            settings.compressionFormat = ToImporterCompression(rule.Compression);
            settings.quality = rule.Quality;
            importer.defaultSampleSettings = settings;
        }

        // ── 공통 ──────────────────────────────────────────────────────────────

        /// <summary>관할 안이고, 아직 사람이 설정을 남긴 적 없는 첫 임포트인가.</summary>
        private bool ShouldApply()
        {
            if (!AssetImportPaths.IsManaged(assetPath)) return false;
            return assetImporter != null && assetImporter.importSettingsMissing;
        }

        private static TextureImportRuleSet LoadTextureRules()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VisualQualityProfile>(QualityProfilePath);
            return VisualQualityProfile.ImportRulesOrDefault(profile, nameof(AscendImportRules));
        }

        private static AudioImportRuleSet LoadAudioRules()
        {
            var profile = AssetDatabase.LoadAssetAtPath<AudioMixProfile>(AudioProfilePath);
            return AudioMixProfile.ImportRulesOrDefault(profile, nameof(AscendImportRules));
        }

        /// <summary>
        /// 런타임 열거형 → 임포터 열거형. 매핑이 여기 한 곳뿐이어야
        /// `Scripts/Data/` 가 `UnityEditor` 를 참조하지 않는다(참조하면 빌드가 깨진다).
        /// </summary>
        /// 열거형을 전체 이름으로 적는 이유: 임포터 열거형의 소속이 직관과 다르다.
        /// `TextureImporterCompression` · `TextureImporterType` 은 `UnityEditor` 인데
        /// `AudioClipLoadType` 과 **`AudioCompressionFormat` 은 `UnityEngine`** 이다
        /// (`UnityEngine.AudioModule`). 짧은 이름으로 적고 `UnityEditor` 쪽을 가정하면
        /// 이 한 줄이 `Assembly-CSharp-Editor` 전체 컴파일을 막는다 — asmdef 이 없어서
        /// 그 실패가 곧 프로젝트 전체 정지다.
        private static UnityEditor.TextureImporterCompression ToImporterCompression(TextureImportCompression value)
        {
            switch (value)
            {
                case TextureImportCompression.Uncompressed:
                    return UnityEditor.TextureImporterCompression.Uncompressed;
                case TextureImportCompression.LowQuality:
                    return UnityEditor.TextureImporterCompression.CompressedLQ;
                case TextureImportCompression.HighQuality:
                    return UnityEditor.TextureImporterCompression.CompressedHQ;
                default:
                    return UnityEditor.TextureImporterCompression.Compressed;
            }
        }

        private static UnityEngine.AudioClipLoadType ToImporterLoadType(AudioImportLoadType value)
        {
            switch (value)
            {
                case AudioImportLoadType.CompressedInMemory:
                    return UnityEngine.AudioClipLoadType.CompressedInMemory;
                case AudioImportLoadType.Streaming:
                    return UnityEngine.AudioClipLoadType.Streaming;
                default:
                    return UnityEngine.AudioClipLoadType.DecompressOnLoad;
            }
        }

        private static UnityEngine.AudioCompressionFormat ToImporterCompression(AudioImportCompression value)
        {
            switch (value)
            {
                case AudioImportCompression.Pcm:
                    return UnityEngine.AudioCompressionFormat.PCM;
                case AudioImportCompression.Vorbis:
                    return UnityEngine.AudioCompressionFormat.Vorbis;
                default:
                    return UnityEngine.AudioCompressionFormat.ADPCM;
            }
        }

        // ── 증거 ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 규칙과 **적용 대상 개수**를 `Logs/import_rules.txt` 에 적는다. 아무것도 재임포트하지 않는다.
        ///
        /// 개수를 함께 적는 이유: 지금 이 저장소의 관할 아래 텍스처는 0개이고 오디오는
        /// 절차적 생성이라 파일이 없다. 대상이 0이면 규칙은 **한 번도 실행되지 않는다.**
        /// 「규칙이 있다」와 「규칙이 적용됐다」를 같은 줄에 적으면 다음 세션이 후자를
        /// 근거로 이 항목을 VERIFIED 로 올린다 — 이 저장소가 반복해 온 실패가 그것이다.
        /// </summary>
        [MenuItem("Ascend/Report Import Rules")]
        public static void ReportImportRules()
        {
            var sb = new StringBuilder(2048);
            sb.AppendLine("[상승] === 에셋 임포트 규칙 (PRD §13.4) ===");
            sb.AppendLine($"관할 루트: {AssetImportPaths.ManagedRoot}");
            sb.AppendLine("적용 시점: 첫 임포트만 (importSettingsMissing). 기존 에셋은 재임포트해야 바뀐다.");
            sb.AppendLine();

            TextureImportRuleSet textures = LoadTextureRules();
            sb.AppendLine($"텍스처 규칙 출처: {textures.SourceName}" +
                          (textures.IsCodePreset ? "  ← 에셋에 값이 없다" : string.Empty));
            sb.AppendLine($"  {textures.For(TextureAssetCategory.Ui).Describe()}");
            sb.AppendLine($"  {textures.For(TextureAssetCategory.World).Describe()}");
            sb.AppendLine($"  {textures.For(TextureAssetCategory.NormalMap).Describe()}");
            sb.AppendLine($"  {textures.For(TextureAssetCategory.Vfx).Describe()}");
            sb.AppendLine();

            AudioImportRuleSet audio = LoadAudioRules();
            sb.AppendLine($"오디오 규칙 출처: {audio.SourceName}" +
                          (audio.IsCodePreset ? "  ← 에셋에 값이 없다" : string.Empty));
            sb.AppendLine($"  {audio.For(AudioAssetClass.ShortEffect).Describe()}");
            sb.AppendLine($"  {audio.For(AudioAssetClass.Loop).Describe()}");
            sb.AppendLine($"  {audio.For(AudioAssetClass.Voice).Describe()}");
            sb.AppendLine();

            string root = AssetImportPaths.ManagedRoot.TrimEnd('/');
            int textureCount = CountAssets("t:Texture", root);
            int audioCount = CountAssets("t:AudioClip", root);
            sb.AppendLine($"관할 아래 텍스처 {textureCount}개 · 오디오 클립 {audioCount}개");
            if (textureCount == 0 && audioCount == 0)
                sb.AppendLine("→ 적용 대상이 0건이다. 규칙은 존재하지만 아직 한 번도 실행되지 않았다.");

            string report = sb.ToString();
            WriteArtifact("import_rules.txt", report);
            Debug.Log(report);
        }

        private static int CountAssets(string filter, string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return 0;
            string[] guids = AssetDatabase.FindAssets(filter, new[] { folder });
            return guids == null ? 0 : guids.Length;
        }

        private static void WriteArtifact(string fileName, string body)
        {
            try
            {
                string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, fileName), body + "\n");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[상승] {fileName} 을 쓰지 못했다: {e.GetType().Name}: {e.Message}");
            }
        }
    }
}

// 씬 배선 필요: 없다. 임포트 파이프라인은 씬과 무관하다.
// 에셋 수정 필요 (씬 오너): `VisualQualityProfile.asset` · `AudioMixProfile.asset` 은
//   새로 붙은 규칙 배열이 없는 채로 디스크에 있으므로 지금은 코드 프리셋으로 폴백한다.
//   인스펙터에서 두 에셋의 톱니 메뉴 → Reset 을 한 번 누르면 값이 에셋에 굳는다.
//   그 전까지 `Logs/import_rules.txt` 의 출처는 「코드 프리셋」으로 찍힌다 — 의도된 표시다.
