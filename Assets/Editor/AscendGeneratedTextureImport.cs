using UnityEditor;
using UnityEngine;

namespace Ascend.Prototype.EditorTools
{
    /// <summary>
    /// 생성 텍스처 폴더에만 걸리는 임포트 보정. **크기·압축·밉맵·sRGB 는 건드리지 않는다** —
    /// 그건 <see cref="AscendImportRules"/> 가 프로파일 값으로 이미 하고 있고, 두 곳에서
    /// 같은 축을 쓰면 어느 쪽이 이겼는지 알 수 없게 된다.
    ///
    /// ## 여기서만 하는 것 — 필터 모드
    ///
    /// `TextureImportRule` 에는 **필터 모드 축이 아예 없다.** 그래서 임포터 기본값인
    /// Bilinear 가 걸리고, 그러면 `VISUAL_BIBLE.md` §2.3 이 세운 판정 기준의 절반이 무너진다 —
    /// 「텍스처를 확대하면 픽셀 그레인이 보여야 한다」. Bilinear 는 확대할 때 이웃 텍셀을
    /// 섞어서 그 그레인을 정확히 지운다. 12~24색 양자화와 오더드 디더링으로 만든 알갱이가
    /// 화면에 닿기 직전에 사라지는 것이고, 그건 이 세트 전체를 무의미하게 만든다.
    ///
    /// 밉맵은 **끄지 않는다.** 규칙이 켜 두고 있고, 그게 맞다 — 축소될 때 밉맵이 없으면
    /// 고주파 그레인이 프레임마다 반짝여(`VISUAL_SPEC` §8 판독성) 오히려 해롭다.
    /// 확대에서는 Point, 축소에서는 밉맵. 두 축은 서로 다른 문제를 푼다.
    ///
    /// ## 통합자에게 — 이 파일은 임시다
    ///
    /// 옳은 자리는 `Scripts/Data/Profiles/AssetImportRules.cs` 의 `TextureImportRule` 에
    /// `FilterMode` 축을 추가하고 World 프리셋을 Point 로 두는 것이다. 그 파일은 데이터
    /// 소유 영역이라 이 세션에서 손대지 않았다(`CLAUDE.md` 소유권 규칙).
    /// 그 축이 생기면 **이 파일을 지워라.** 남겨 두면 규칙이 두 곳에 있게 된다.
    ///
    /// ## 왜 매 임포트마다 적용하나
    ///
    /// <see cref="AscendImportRules"/> 는 <c>importSettingsMissing</c> 일 때만 적용한다 —
    /// 사람이 인스펙터에서 바꿔 둔 값을 되돌리지 않기 위해서다. 여기는 반대로 **항상**
    /// 적용한다. 이 폴더의 파일은 전부 코드가 낳은 것이고 사람이 손으로 조정할 대상이
    /// 아니기 때문이다. 사람이 이 폴더의 필터를 일부러 Bilinear 로 바꿔야 하는 상황이
    /// 생겼다면, 그건 인스펙터가 아니라 위 문단의 프로파일 축에서 바꿀 일이다.
    /// </summary>
    public sealed class AscendGeneratedTextureImport : AssetPostprocessor
    {
        /// <summary>
        /// <see cref="AscendImportRules"/> 보다 **뒤에** 돌게 한다. 기본 순서가 0 이므로
        /// 양수를 준다. 겹치는 축이 없으니 순서가 결과를 바꾸지는 않지만, 나중에 축이
        /// 겹치게 됐을 때 「누가 마지막에 썼는가」가 우연이 아니어야 한다.
        /// </summary>
        public override int GetPostprocessOrder() { return 100; }

        private void OnPreprocessTexture()
        {
            if (!IsGenerated(assetPath)) return;

            var importer = assetImporter as TextureImporter;
            if (importer == null) return;

            // 확대 시 텍셀 경계를 살린다 — 이 세트의 존재 이유다.
            importer.filterMode = FilterMode.Point;

            // 이방성 필터링은 비스듬히 본 바닥에서 텍셀을 다시 뭉갠다. 1 = 끔.
            importer.anisoLevel = 1;

            // 벽·바닥에 반복해 깔 물건이다. 텍스처 자체가 주기 함수로 만들어져 있으므로
            // Repeat 이어야 그 성질이 화면에 쓰인다. Clamp 면 한 장만 늘어난다.
            importer.wrapMode = TextureWrapMode.Repeat;
        }

        private static bool IsGenerated(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string normalized = path.Replace('\\', '/');
            return normalized.StartsWith(AscendSurfaceSynth.OutputFolder + "/",
                                         System.StringComparison.OrdinalIgnoreCase);
        }
    }
}

// 씬 배선 필요: 없다. 임포트 파이프라인은 씬과 무관하다.
// 통합자 요청: `TextureImportRule` 에 `FilterMode` 축이 생기면 이 파일을 삭제한다.
