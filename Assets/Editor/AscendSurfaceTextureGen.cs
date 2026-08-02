using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Ascend.Prototype.Data.Profiles;

namespace Ascend.Prototype.EditorTools
{
    /// <summary>
    /// <see cref="AscendSurfaceSynth"/> 의 Unity 껍데기. **여기에는 픽셀 규칙이 하나도 없다** —
    /// 생성·측정·판정은 전부 `&lt;PURE&gt;` 쪽이 하고, 이 클래스는 메뉴 항목과
    /// `AssetDatabase.Refresh()` 와 로그만 담당한다.
    ///
    /// 그렇게 나눈 이유: 같은 함수(<see cref="AscendSurfaceValidation.Run"/>)를 헤드리스
    /// 드라이버도 부르기 때문에 **「에디터에서만 통과한다」가 성립할 수 없다.** 이 저장소는
    /// 「생성했다」와 「생성된 것이 지표를 통과한다」를 혼동해 두 번 실패했다
    /// (`GRAPHICS_TARGET.md` §2 G-1 서두).
    ///
    /// ## 재실행 가능하다
    ///
    /// 같은 시드로 다시 돌리면 같은 바이트가 나오고, 바이트가 같으면 **파일을 쓰지 않는다.**
    /// 쓰지 않으면 재임포트가 돌지 않고, 재임포트가 돌지 않으면 사람이 인스펙터에서 바꿔 둔
    /// 값이 살아남는다. 로그에 14줄 전부 「동일 — 쓰지 않았다」가 찍히는 것이 결정론의 증거다.
    /// 하나라도 「달라서 덮어썼다」면 생성기가 결정론을 잃었거나 누가 PNG 를 손으로 고쳤다.
    /// </summary>
    public static class AscendSurfaceTextureGen
    {
        /// <summary>
        /// ⚠ **`AssetDatabase.Refresh()` 를 부른다.** 그래서 플레이 모드에서는 실행하지 않는다.
        ///
        /// 플레이 모드 자동화가 도는 중에 Refresh 하면 도메인이 리로드되고,
        /// `Awake()` 에서 잡은 비직렬화 필드(`MaterialPropertyBlock` 등)가 전부 null 이 된 채
        /// 오브젝트만 살아남는다. 런은 계속 도는 것처럼 보이지만 결과는 오염돼 있다
        /// (`CLAUDE.md` 마지막 절). **이 메뉴는 사람이 누르는 것이라 안전하지만,
        /// 자동화에서 부르면 안 된다** — 부를 일이 있으면 아래 검증 전용 메뉴를 쓴다.
        /// </summary>
        [MenuItem("Ascend/텍스처 생성")]
        public static void GenerateSurfaceTextures()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[상승] 플레이 모드에서는 생성하지 않는다 — Refresh 가 도메인을 리로드한다. "
                               + "「Ascend/텍스처 검증」은 디스크를 건드리지 않으므로 그쪽을 쓴다.");
                return;
            }

            Run(write: true);
        }

        /// <summary>
        /// 디스크를 **쓰지 않는다.** 이미 있는 PNG 를 되읽어 지표만 다시 잰다.
        /// `AssetDatabase.Refresh()` 를 부르지 않으므로 플레이 모드에서도 안전하다.
        /// </summary>
        [MenuItem("Ascend/텍스처 검증")]
        public static void VerifySurfaceTextures()
        {
            Run(write: false);
        }

        private static void Run(bool write)
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string absoluteFolder = Path.Combine(
                projectRoot,
                AscendSurfaceSynth.OutputFolder.Replace('/', Path.DirectorySeparatorChar));

            string report;
            List<AscendSurfaceValidation.Result> results =
                AscendSurfaceValidation.Run(absoluteFolder, write, out report);

            var sb = new StringBuilder(report.Length + 2048);
            sb.Append(report);
            sb.AppendLine();
            sb.AppendLine("── 임포트 분류 (AscendImportRules 가 첫 임포트에 적용한다) ──");
            bool wrote = false;
            foreach (AscendSurfaceValidation.Result r in results)
            {
                string assetPath = AscendSurfaceSynth.OutputFolder + "/" + r.Spec.FileName;
                TextureAssetCategory category = AssetImportPaths.ClassifyTexture(assetPath);
                bool managed = AssetImportPaths.IsManaged(assetPath);
                sb.AppendLine("  " + r.Spec.FileName + " — 관할 " + (managed ? "안" : "밖")
                              + " · 카테고리 " + category);
                if (!managed)
                    sb.AppendLine("    ⚠ 관할 밖이면 임포트 규칙이 걸리지 않는다. 경로를 확인한다.");
                if (r.WriteState != null && r.WriteState.Contains("썼다")) wrote = true;
            }

            sb.AppendLine();
            sb.AppendLine("배선은 이 스크립트가 하지 않는다 — `.mat` 과 씬은 단일 소유다. "
                          + "제안표는 이 파일 맨 아래 「씬 오너 요청」에 있다.");

            string full = sb.ToString();
            WriteArtifact("surface_textures.txt", full);

            bool allPassed = true;
            foreach (AscendSurfaceValidation.Result r in results) if (!r.Passed) allPassed = false;

            if (allPassed) Debug.Log(full);
            else Debug.LogError(full);

            // 파일을 실제로 쓴 경우에만 Refresh 한다. 아무것도 안 바뀌었는데 Refresh 하면
            // 재임포트만 돌고 얻는 것이 없다.
            if (write && wrote) AssetDatabase.Refresh();
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
                Debug.LogWarning("[상승] " + fileName + " 을 쓰지 못했다: "
                                 + e.GetType().Name + ": " + e.Message);
            }
        }
    }
}

// 씬 배선 필요: 없다. 이 스크립트는 씬을 열지 않고 `.mat` 을 만들지도 고치지도 않는다.
//
// ── 씬 오너 요청 — 배선 제안표 (이 세션에서 하지 않았다) ──────────────────────────
//
// 근거: `docs/GRAPHICS_TARGET.md` §2 G-1 — 「텍스처가 배선된 머티리얼 비율 0/47 → ≥40/47」.
// 씬이 실제로 쓰는 표면은 씬 내장 머티리얼이고, 그중 `CarShell_*` 13장이 캐빈 표면 전체다.
// `Assets/Prototype_Elevator/Art/Materials/MAT_Ascend_*.mat` 3장은 **씬에서 0회 참조**되므로
// 거기에 물려도 화면은 바뀌지 않는다 — 배선 대상은 씬 내장 13장이다.
//
//   씬 머티리얼                   ← 텍스처                        타일링(회/m)  6.4m 벽 환산
//   ─────────────────────────────────────────────────────────────────────────────────────
//   CarShell_Floor               ← TEX_FloorPlate_Rust.png        0.75          4.8 × 4.8
//   CarShell_Ceiling             ← TEX_WallPanel_Riveted.png      0.50          3.2 × 3.2
//   CarShell_WallL               ← TEX_WallPanel_Riveted.png      0.50          3.2 × 2.75(5.5m 높이)
//   CarShell_WallR               ← TEX_WallPanel_Riveted.png      0.50          3.2 × 2.75
//   CarShell_BackWall_Left       ← TEX_WallPanel_Riveted.png      0.50
//   CarShell_BackWall_Right      ← TEX_WallPanel_Riveted.png      0.50
//   CarShell_FrontWall           ← TEX_WallPaint_Peeled.png       0.50
//   CarShell_BackWall_Lintel     ← TEX_Stencil_Warning.png        0.50
//   CarShell_LobbyFloor          ← TEX_Grating_Steel.png          1.00
//   CarShell_LobbyBack           ← TEX_Concrete_Shaft.png         0.40
//   CarShell_Handrail_R          ← TEX_Conduit_Cable.png          2.00
//   CarShell_Handrail_B          ← TEX_Conduit_Cable.png          2.00
//   CarShell_TankStand           ← TEX_Machine_Housing.png        1.50
//   (프롭·미사용) M_Gray_Console  ← TEX_Gauge_Enamel.png           2.00
//   (프롭·미사용) M_Gray_Panel    ← TEX_Machine_Housing.png        1.50
//   (프롭·미사용) 화물 상자        ← TEX_Pallet_Wood.png            1.00
//   (프롭·미사용) 통관 커버        ← TEX_Glass_Smudged.png          1.00
//   (프롭·미사용) 승객 의복·덮개   ← TEX_Fabric_Sack.png            2.00
//
// **타일링 배율의 뜻**: 「월드 1m 당 이 텍스처가 몇 번 반복되는가」다.
//   `material.SetTextureScale("_BaseMap", new Vector2(폭_m × 배율, 높이_m × 배율))`.
//   예 — 6.4m × 5.5m 좌벽에 0.50 회/m 면 `_BaseMap_ST` 의 tiling 은 (3.2, 2.75).
//   캐빈이 바닥 4배(선형 2배)·천장 3.2m→5.5m 로 확대되면 **벽 한 장의 면적이 두 배가 되므로
//   타일 수도 그만큼 커져야 한다.** 배율을 고정해 두면 확대가 곧 「텍셀이 늘어난 벽」이 아니라
//   「늘어난 만큼 뭉갠 벽」이 된다 — 그것이 G-1 국소 분산을 다시 0 으로 되돌리는 경로다.
//   256px ÷ 2m = 128 텍셀/m 이고, 1080p 에서 6.4m 벽이 화면 800px 을 차지하면 125 화면px/m 이다.
//   거의 1:1 이라 픽셀 그레인이 밉맵에 먹히지 않고 살아남는다 — 그 지점을 노린 값이다.
//
// ⚠ **`_BaseColor` 를 함께 올려야 한다.** `Ascend/Stylized` 는 `_BaseMap` 을 `_BaseColor` 에
//   **곱한다.** 지금 `CarShell_*` 13장의 기본색은 0.24 언저리이고, 이 텍스처들의 평균 밝기는
//   0.47~0.60 이다. 그대로 곱하면 반사율이 0.12 로 떨어져 **텍스처를 물린 쪽이 더 어두워진다.**
//   텍스처를 물릴 때 그 머티리얼의 `_BaseColor` 를 0.85~1.00 의 중립색으로 올린다 —
//   색은 이제 텍스처가 나른다. 이 한 줄을 빠뜨리면 「물렸는데 화면이 더 나빠졌다」가 되고,
//   이 저장소는 그 이유로 셰이더·머티리얼 교체를 두 번 되돌렸다.
//
// ⚠ **발광 마스크 2장은 지금 배선할 곳이 없다.** `Ascend/Stylized` 에는 `_EmissionColor`
//   (단색, `MaterialPropertyBlock` 이 쓴다)만 있고 **발광 텍스처 슬롯이 없다.**
//   `TEX_Gauge_Enamel_Emis.png` · `TEX_Machine_Housing_Emis.png` 를 쓰려면 셰이더에
//   `_EmissionMap` 을 추가해 `_EmissionColor` 에 곱해야 하고, 그건 셰이더 소유자 몫이다.
//   슬롯이 생기기 전까지 이 두 장은 **배선 0건인 에셋**이다 — 그 사실을 숨기지 않는다.
//
// 채택 절차 (한 번에 하나씩, 매번 캡처·독립 판정):
//   1) `Ascend/텍스처 생성` 을 한 번 돌린다. 14줄 전부 「동일 — 쓰지 않았다」여야 한다.
//   2) `Ascend/Report Import Rules` 로 「관할 아래 텍스처 N개」가 4 → 18 로 바뀌는지 본다.
//   3) `CarShell_WallL` **한 장만** 먼저 물리고 캡처한다 — `AscendStylizedAdoption` 이
//      같은 이유로 좌벽 한 장부터 시작한 그 자리다(독립 평가자가 휘도를 재는 스트립).
//   4) `tools/capture-metrics.ps1` 로 G-1 국소 분산이 0.00 에서 움직이는지 확인한 뒤
//      나머지 12장으로 넓힌다.
