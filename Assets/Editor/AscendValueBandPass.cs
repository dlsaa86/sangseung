using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace Ascend.CaptureHarness.EditorTools
{
    /// <summary>
    /// **값 폭 압축 패스.** 화면에서 밝은 무텍스처 슬래브를 없애 벽과 같은 명도 대역으로
    /// 끌어내린다. 새 셸(`AscendCabinAdoption`)이 못 덮는 **기존 오브젝트**가 대상이다.
    ///
    /// ## 왜 필요한가 — 측정된 사실
    ///
    /// 2026-08-04 기준선 캡처 `C_toward_gate.png` 에서 관측:
    /// 벽 패널은 명도 ~15% 인데 가위문 문설주·헤더·바닥레일·우측 소패널이 ~70% 로 떠 있고,
    /// 가위문 격자는 채도 높은 올리브다. 레퍼런스는 **모든 면이 서로 몇 % 안쪽**이며
    /// 채도 예산은 위험 신호와 심볼에만 쓴다(`VISUAL_BIBLE.md` §3).
    ///
    /// ## 왜 「일괄 교체」가 아닌가
    ///
    /// 이 저장소는 재질 일괄 교체를 두 번 되돌렸다(`AscendMaterialFactory` 주석).
    /// 그래서 이 패스는 세 가지로 스스로를 묶는다.
    ///  ① **측정 기준으로만 고른다** — 휘도가 <see cref="Ceiling"/> 를 넘는 재질만 건드린다.
    ///     "다 갈았다"가 아니라 "넘은 것만 내렸다"라서 무엇이 왜 바뀌었는지 목록으로 남는다.
    ///  ② **보호 목록이 먼저 이긴다** — 판독·발광·표지·심볼·계기는 어떤 경우에도 제외된다.
    ///     금지 11항(플레이 불가능한 분위기 공간)이 여기서 걸린다.
    ///  ③ **원래 색을 JSON 으로 남긴다** — 되돌리기가 추측이 아니라 복원이다.
    ///
    /// 색조는 유지하고 **명도만** 내린다. 색을 새로 정하는 것이 아니라 대역을 좁히는 것이다.
    /// </summary>
    internal static class AscendValueBandPass
    {
        /// <summary>이 휘도를 넘는 비보호 재질만 대상이 된다.</summary>
        private const float Ceiling = 0.155f;

        /// <summary>대상 재질이 내려갈 목표 휘도.</summary>
        private const float Target = 0.105f;

        /// <summary>채도 상한. 넘으면 회색 쪽으로 당긴다 (가위문 올리브가 여기 걸린다).</summary>
        private const float MaxSaturation = 0.28f;

        private const string BackupPath = "docs/runtime/value_band_backup.json";

        /// <summary>새 셸이 형상으로 대신하는 가위문 부재 — 끄기만 한다.</summary>
        private static readonly string[] RedundantGateParts =
        {
            "ReferenceRoom/LeftScissorGate/Jamb_Front",
            "ReferenceRoom/LeftScissorGate/Jamb_Rear",
            "ReferenceRoom/LeftScissorGate/Header",
            "ReferenceRoom/LeftScissorGate/Rail_Floor",
            "ReferenceRoom/LeftScissorGate/Rail_Ceiling",
        };

        /// <summary>
        /// 어떤 경우에도 건드리지 않는다. 판독성이 분위기를 이긴다
        /// (`VISUAL_SPEC.md` §8 — "로우파이 스타일을 명분으로 결과 판독성을 희생하지 않는다").
        /// </summary>
        private static readonly string[] Protected =
        {
            "Readout", "Segment", "Emis", "Emission", "Symbol", "Sym_", "Soul",
            "Lamp", "Lens", "Light", "Glass", "Bulb", "Glow",
            "Warning", "TMP", "Font",
            "Contract", "Power", "Tick", "Gauge", "Indicator", "Lock",
            "Lever", "Handle", "Knob", "Grip",   // 상호작용물은 배경에 묻히면 안 된다
            "ELV_",                               // 새 셸은 이미 대역 안에 있다
            "Absorber", "Proliferator", "Purify",
        };

        /// <summary>
        /// **완전 보호와 일반 압축 사이.** 표지·표찰은 읽혀야 하지만 화면에서
        /// 가장 밝은 물체가 되면 안 된다.
        ///
        /// v1 실측 — `RM_Sign` 휘도 0.716 으로 새 셸 밴드(0.054~0.151)의 **13배**였고,
        /// 실행 레버보다 눈을 먼저 끌었다. `VISUAL_SPEC` §5 의 상호작용 계층에서
        /// 표지는 레버보다 **아래**인데 화면에서는 위였다 — 계층이 뒤집혀 있었다.
        /// 그래서 지우지 않고 **천장만 낮춘다.** 여전히 비발광 물체 중 가장 밝다.
        /// </summary>
        private static readonly string[] SoftCapped =
        {
            "Stencil", "Sign", "Plaque", "Label", "Placard",
        };

        private const float SoftCeiling = 0.34f;
        private const float SoftTarget = 0.26f;

        [MenuItem("Ascend/Cabin/2. 값 폭 압축 패스")]
        public static void Apply()
        {
            var log = new StringBuilder("=== 값 폭 압축 ===\n");
            var backup = new List<Entry>();

            int off = 0;
            foreach (var path in RedundantGateParts)
            {
                var go = FindByPath(path);
                if (go != null && go.activeSelf) { go.SetActive(false); off++; }
            }
            log.AppendLine($"  새 벽이 대신하는 가위문 부재 {off}개 비활성");

            var mats = CollectSceneMaterials();
            log.AppendLine($"  씬에서 쓰이는 재질 {mats.Count}종 검사");

            int changed = 0, skippedProtected = 0, skippedInBand = 0;
            foreach (var m in mats)
            {
                if (m == null) continue;
                if (IsProtected(m.name)) { skippedProtected++; continue; }

                var prop = m.HasProperty("_BaseColor") ? "_BaseColor"
                         : m.HasProperty("_Color") ? "_Color" : null;
                if (prop == null) continue;

                bool soft = SoftCapped.Any(p => m.name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);
                float ceiling = soft ? SoftCeiling : Ceiling;
                float target = soft ? SoftTarget : Target;

                var c = m.GetColor(prop);
                float lum = Luminance(c);
                Color.RGBToHSV(c, out float h, out float s, out float v);

                bool tooBright = lum > ceiling;
                // 표지의 채도는 경고 기능이다 — 낮추지 않는다.
                bool tooSaturated = !soft && s > MaxSaturation;
                if (!tooBright && !tooSaturated) { skippedInBand++; continue; }

                var pathOfMat = AssetDatabase.GetAssetPath(m);
                backup.Add(new Entry
                {
                    guid = string.IsNullOrEmpty(pathOfMat) ? "" : AssetDatabase.AssetPathToGUID(pathOfMat),
                    assetPath = pathOfMat,
                    matName = m.name,
                    prop = prop,
                    r = c.r, g = c.g, b = c.b, a = c.a,
                });

                // 🔴 v2 실측 버그 — 탈채도와 명도 상한을 **따로** 계산하면 어두운 고채도색이
                // 오히려 밝아진다. `RM_RedPaint` .227 → .444, `RM_Rust` .327 → .442 로
                // 올라가 비발광 중 최댓값이 됐다. 채도를 빼면 같은 v 라도 휘도가 오르는데
                // 그 오른 값을 다시 재지 않았기 때문이다.
                // **순서를 고친다 — 먼저 탈채도하고, 그 결과의 휘도를 다시 재서 상한을 건다.**
                float newS = soft ? s : Mathf.Min(s, MaxSaturation);
                var desat = Color.HSVToRGB(h, newS, v);
                float lumAfterDesat = Luminance(desat);

                float newV = v;
                if (lumAfterDesat > ceiling)
                    newV = v * Mathf.Clamp01(target / Mathf.Max(lumAfterDesat, 1e-4f));

                // 색조(h)는 그대로 — 대역을 좁히는 것이지 색을 다시 칠하는 게 아니다.
                var nc = Color.HSVToRGB(h, newS, newV);
                nc.a = c.a;

                // 🔴 v3 실측 — 순서를 고친 뒤에도 `RM_RedPaint` 가 0.2271 → 0.2733 으로
                // **여전히 올랐다.** 상한(선형 0.155)이 그 색의 원래 휘도보다 위에 있어서
                // 탈채도가 만든 상승을 막을 게 없었기 때문이다.
                //
                // 「압축」이라는 이름이 보장해야 하는 것은 **단조 비증가**다 —
                // 어떤 재질도 이 패스를 지나서 밝아지면 안 된다. 그게 아니면
                // 압축 패스가 밝기를 만드는 자기모순이 된다 (v2 에서 실제로 그랬다).
                if (Luminance(nc) > lum)
                {
                    float k = Mathf.Clamp01(lum / Mathf.Max(Luminance(nc), 1e-4f));
                    nc = new Color(nc.r * k, nc.g * k, nc.b * k, c.a);
                }
                m.SetColor(prop, nc);
                if (m.HasProperty("_Smoothness"))
                    m.SetFloat("_Smoothness", Mathf.Min(m.GetFloat("_Smoothness"), 0.18f));
                EditorUtility.SetDirty(m);
                changed++;
                log.AppendLine($"    {m.name,-34} lum {lum:F3}→{Luminance(nc):F3}  sat {s:F2}→{newS:F2}");
            }

            WriteBackup(backup, log);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            log.AppendLine($"  변경 {changed} / 보호로 제외 {skippedProtected} / 이미 대역 안 {skippedInBand}");
            log.AppendLine($"  되돌리기: Ascend/Cabin/8. 값 폭 압축 되돌리기");
            Debug.Log(log.ToString());
        }

        [MenuItem("Ascend/Cabin/8. 값 폭 압축 되돌리기")]
        public static void Revert()
        {
            var full = Path.GetFullPath(BackupPath);
            if (!File.Exists(full)) { Debug.LogError($"[ValueBand] 백업 없음: {full}"); return; }

            var wrap = JsonUtility.FromJson<Wrapper>(File.ReadAllText(full));
            var log = new StringBuilder("=== 값 폭 압축 원복 ===\n");
            int n = 0;
            foreach (var e in wrap.items)
            {
                var p = !string.IsNullOrEmpty(e.guid) ? AssetDatabase.GUIDToAssetPath(e.guid) : e.assetPath;
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null) { log.AppendLine($"  ⚠ 못 찾음 {e.matName} ({p})"); continue; }
                if (m.HasProperty(e.prop))
                {
                    m.SetColor(e.prop, new Color(e.r, e.g, e.b, e.a));
                    EditorUtility.SetDirty(m);
                    n++;
                }
            }
            foreach (var path in RedundantGateParts)
            {
                var go = FindByPath(path);
                if (go != null && !go.activeSelf) go.SetActive(true);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            log.AppendLine($"  재질 {n}종 복원 + 가위문 부재 복원");
            Debug.Log(log.ToString());
        }

        // ── 내부 ──────────────────────────────────────────────────────────────
        [Serializable] private class Entry
        {
            public string guid, assetPath, matName, prop;
            public float r, g, b, a;
        }

        [Serializable] private class Wrapper { public List<Entry> items = new List<Entry>(); }

        private static void WriteBackup(List<Entry> items, StringBuilder log)
        {
            var full = Path.GetFullPath(BackupPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            // 기존 백업을 덮어쓰지 않는다 — 두 번 돌리면 이미 내려간 값이 원본으로 굳는다.
            if (File.Exists(full))
            {
                log.AppendLine($"  ⚠ 백업이 이미 있다. 덮어쓰지 않는다 ({BackupPath})");
                log.AppendLine("     두 번째 실행이면 먼저 원복하고 다시 돌린다.");
                return;
            }
            File.WriteAllText(full, JsonUtility.ToJson(new Wrapper { items = items }, true));
            log.AppendLine($"  원본 색 {items.Count}건 백업 → {BackupPath}");
        }

        private static List<Material> CollectSceneMaterials()
        {
            var set = new HashSet<Material>();
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var r in root.GetComponentsInChildren<Renderer>(false))
                    foreach (var m in r.sharedMaterials)
                        if (m != null) set.Add(m);
            return set.ToList();
        }

        private static bool IsProtected(string name)
        {
            return Protected.Any(p => name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// 🔴 v4 실측 버그 — **측정 공간이 이 파일 안에서 어긋나 있었다.**
        ///
        /// `Ceiling`(0.155) · `Target`(0.105) · `SoftCeiling`(0.34) 은 전부 감마 직관으로
        /// 잡은 값이고, 이 저장소가 보고에 써 온 휘도도 sRGB 가중이다
        /// (`RM_RedPaint` 원본 0.56/0.14/0.11 → 0.2271 — 보고서 수치와 정확히 일치).
        /// 그런데 이 함수만 `c.linear` 를 썼다.
        ///
        /// 결과: `RM_RedPaint` 가 linear 기준으로는 0.0715 → 0.0625 로 **이미 내려가 있어**
        /// 단조 보장 가드가 한 번도 발동하지 않았고, sRGB 기준으로는 0.2271 → 0.2743 으로
        /// **올랐다.** 가드를 넣었는데 가드가 재는 공간에서는 위반이 없었던 것이다.
        ///
        /// 상한 값들과 **같은 공간**으로 통일한다. 이게 세 번째 수정이지만
        /// 앞의 둘(순서 · 단조 가드)과 다른 층위다 — 값이 아니라 **단위**가 틀렸다.
        /// </summary>
        private static float Luminance(Color c)
        {
            return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        }

        private static GameObject FindByPath(string path)
        {
            var parts = path.Split('/');
            var cur = SceneManager.GetActiveScene().GetRootGameObjects()
                                  .FirstOrDefault(r => r.name == parts[0]);
            for (int i = 1; i < parts.Length && cur != null; i++)
            {
                var next = cur.transform.Find(parts[i]);
                cur = next != null ? next.gameObject : null;
            }
            return cur;
        }
    }
}
