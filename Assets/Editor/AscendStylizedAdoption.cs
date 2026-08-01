using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ascend.CaptureHarness.EditorTools
{
    /// <summary>
    /// `Ascend/Stylized` 를 **이미 씬에 있는 렌더러에 그 자리에서** 입힌다.
    ///
    /// ## 왜 씬 빌더를 다시 돌리지 않나
    ///
    /// `TenFloorSceneBuilder` 를 다시 돌리면 재질뿐 아니라 위치·회전·비율까지 다시
    /// 쓴다. 그 스크립트 안에 「Reproportion 메뉴는 회전·위치를 되돌리지 않는다」는
    /// 경고가 이미 붙어 있다 — 즉 **멱등이 아니다.** 손으로 맞춰 둔 배치가 조용히
    /// 사라질 수 있고, 그건 승인 없이 할 일이 아니다.
    ///
    /// 여기서는 셰이더만 바꾼다. 메시·트랜스폼·계층에 손대지 않는다.
    /// 되돌리기는 같은 메뉴의 「되돌리기」 쪽이고, 색을 그대로 옮기므로
    /// 왕복하면 원래 그림으로 돌아온다.
    ///
    /// ## 왜 무리를 나누나
    ///
    /// 이 저장소는 셰이더 일괄 교체를 **두 번 되돌렸다**(6차 판정 「순손실」).
    /// 두 번 다 전부 갈고 나서 비교했다. 한 무리씩 켜고 캡처·독립 판정을 받는다.
    /// 나빠지면 그 무리만 되돌린다.
    /// </summary>
    internal static class AscendStylizedAdoption
    {
        /// <summary>벽·바닥·천장. 가장 넓은 면이라 플랫 셰이딩이 가장 크게 드러난다.</summary>
        private static readonly string[] WallPrefixes = { "CarShell_" };

        /// <summary>
        /// **한 장만.** 11차에서 13장을 한꺼번에 켰다가 위험 채널을 잃고 되돌렸다.
        /// 셰이더를 고친 뒤의 첫 확인은 가장 작은 단위여야 한다 — 나빠져도 한 장이고,
        /// 좋아져도 그것이 이 셰이더의 공인지 아닌지가 다른 12장에 섞이지 않는다.
        /// 좌벽을 고른 이유는 독립 평가자가 휘도를 재는 스트립이 거기이기 때문이다.
        /// </summary>
        private static readonly string[] OneWallPrefixes = { "CarShell_WallL" };

        [MenuItem("Ascend/Adopt Stylized — 좌벽 한 장만")]
        private static void AdoptOneWall() => Run(OneWallPrefixes, toStylized: true);

        [MenuItem("Ascend/Adopt Stylized — 되돌리기 (좌벽 한 장)")]
        private static void RevertOneWall() => Run(OneWallPrefixes, toStylized: false);

        [MenuItem("Ascend/Adopt Stylized — 벽·바닥·천장")]
        private static void AdoptWalls() => Run(WallPrefixes, toStylized: true);

        [MenuItem("Ascend/Adopt Stylized — 되돌리기 (벽·바닥·천장)")]
        private static void RevertWalls() => Run(WallPrefixes, toStylized: false);

        private static void Run(string[] prefixes, bool toStylized)
        {
            Shader target = Shader.Find(toStylized ? "Ascend/Stylized" : "Universal Render Pipeline/Lit");
            if (target == null)
            {
                Debug.LogError("[상승] 셰이더를 못 찾았다. 아무것도 바꾸지 않았다.");
                return;
            }

            var report = new StringBuilder();
            report.AppendLine($"[상승] 스타일 셰이더 {(toStylized ? "채택" : "되돌리기")} — 대상 접두어 {string.Join(", ", prefixes)}");

            var touched = new HashSet<Material>();
            int renderers = 0, changed = 0, skipped = 0;

            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,
                                                                            FindObjectsSortMode.None))
            {
                Material[] mats = renderer.sharedMaterials;
                bool any = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null || !HasPrefix(m.name, prefixes)) continue;
                    renderers++;
                    if (!touched.Add(m)) { any = true; continue; }

                    if (m.shader == target) { skipped++; any = true; continue; }

                    // **색을 먼저 읽고 셰이더를 바꾼다.** 순서를 뒤집으면 새 셰이더에
                    // 없는 프로퍼티가 날아간 뒤라 원래 색을 못 되찾는다.
                    Color color = ReadColor(m);
                    Undo.RecordObject(m, "Adopt Stylized Shader");
                    m.shader = target;
                    WriteColor(m, color);
                    if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.03f);
                    if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
                    EditorUtility.SetDirty(m);
                    changed++;
                    any = true;
                    report.AppendLine($"  {m.name} → {target.name} · 색 #{ColorUtility.ToHtmlStringRGB(color)}");
                }
                if (any) EditorUtility.SetDirty(renderer);
            }

            report.AppendLine($"  합계 — 렌더러 참조 {renderers} · 머티리얼 {touched.Count}종 · 바꿈 {changed} · 이미 그 셰이더 {skipped}");

            if (changed == 0)
            {
                // **0 을 성공으로 적지 않는다.** 대상이 없다는 것과 다 됐다는 것은 다르고,
                // 그 둘을 같은 문장으로 적는 것이 이 저장소가 반복해 온 실패다.
                report.AppendLine("  ⚠ 바꾼 것이 0개다 — 접두어가 안 맞았거나 이미 적용돼 있다. 화면은 그대로다.");
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log(report.ToString());
            System.IO.File.WriteAllText("Logs/stylized_adoption.txt", report.ToString());
        }

        private static bool HasPrefix(string name, string[] prefixes)
        {
            foreach (string p in prefixes)
                if (name.StartsWith(p, System.StringComparison.Ordinal)) return true;
            return false;
        }

        private static Color ReadColor(Material m)
        {
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            if (m.HasProperty("_Color")) return m.GetColor("_Color");
            return Color.gray;
        }

        private static void WriteColor(Material m, Color color)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        }
    }
}
