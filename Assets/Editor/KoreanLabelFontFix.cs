using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ascend.PrototypeEditor
{
    /// <summary>
    /// 한글을 담은 TMP 라벨의 **폰트 에셋 참조만** 교정한다.
    ///
    /// ── 왜 필요한가 ──────────────────────────────────────────────────────────
    /// TMP 컴포넌트를 코드로 `AddComponent` 하면 폰트가 TMP 기본값
    /// (`LiberationSans SDF`)으로 붙는다. 그 폰트에는 **한글 글리프가 하나도 없다.**
    /// 그래서 「실행」·「계약」·「탑승구」 같은 표찰이 씬에 존재하고 렌더러도
    /// 켜져 있지만 **글자가 단 하나도 그려지지 않았다** — `mesh.vertexCount = 4`
    /// (TMP 의 최소 할당분), 렌더러 바운즈 size = (0,0,0).
    ///
    /// 이건 스타일 문제가 아니라 기능 결함이다. `VISUAL_SPEC` §5 상호작용 계층
    /// 1위인 「현재 사용 가능한 핵심 레버」의 표찰이 안 보이는 것이기 때문이다.
    /// 독립 평가에서 「실행 표찰이 글자로 안 읽힌다」는 지적이 나왔고 그때는
    /// 색수차 탓으로만 봤는데, 원인이 둘 겹쳐 있었다.
    ///
    /// ── 무엇을 바꾸는가 ──────────────────────────────────────────────────────
    /// **폰트 에셋과 그 폰트의 머티리얼, 둘뿐이다.** 위치·크기·문구·색·정렬은
    /// 건드리지 않는다. 머티리얼을 같이 바꾸는 이유는, 폰트만 교체하고
    /// 머티리얼이 옛 아틀라스(`LiberationSans Atlas`)를 계속 가리키면 새 폰트의
    /// UV 로 옛 텍스처를 샘플링해 글자가 깨져 나오기 때문이다.
    ///
    /// 월드 라벨(`TextMeshPro`)은 `... SingleSided` 변형을 쓴다. 그 변형은
    /// `_CullMode = 2`(Back)이라 뒷면(거울상)을 그리지 않는다 —
    /// <see cref="TenFloorSceneBuilder"/> 가 만들어 둔 규약이고 여기서도 지킨다.
    /// UI 라벨(`TextMeshProUGUI`)은 뒷면이 보일 일이 없으므로 기본 머티리얼을 쓴다.
    ///
    /// ── 멱등 ─────────────────────────────────────────────────────────────────
    /// 델타가 아니라 **절대값 대입**이다. 두 번 돌리면 두 번째는 `changed = 0`.
    /// 대상 선정도 상태가 아니라 「문구에 비 ASCII 문자가 있는가」라는 불변식으로
    /// 판정하므로, 나중에 라벨이 추가돼도 같은 규칙이 적용된다.
    ///
    /// ── 아틀라스 ─────────────────────────────────────────────────────────────
    /// `NanumGothic SDF` 는 `atlasPopulationMode = Dynamic` 이다. 아틀라스에 아직
    /// 없는 글리프(예: `/`, `:`)는 원본 `NanumGothic.ttf` 에서 필요할 때 구워
    /// 넣는다. 아틀라스를 **재생성하지 않는다** — 재생성은 되돌리기 어렵고
    /// 사용자 결정 사항이다. 다 못 구우면 그 글자를 보고만 하고 멈춘다.
    /// </summary>
    public static class KoreanLabelFontFix
    {
        private const string FontPath = "Assets/Prototype_Elevator/Fonts/NanumGothic SDF.asset";
        private const string WorldMatPath = "Assets/Prototype_Elevator/Materials/NanumGothic SDF Material SingleSided.mat";
        private const string UguiMatPath = "Assets/Prototype_Elevator/Fonts/NanumGothic SDF.asset"; // 기본 머티리얼은 폰트 에셋의 서브에셋이다

        [MenuItem("Ascend/Fix Korean Label Fonts")]
        public static void Run()
        {
            var report = new StringBuilder();
            Apply(report);
            Debug.Log(report.ToString());
        }

        /// <summary>
        /// 활성 씬의 모든 TMP 라벨을 훑어 한글(비 ASCII)을 담은 것만 교정한다.
        /// 반환값은 실제로 참조가 바뀐 컴포넌트 수 — 멱등 확인에 쓴다.
        /// </summary>
        public static int Apply(StringBuilder report)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                report.AppendLine("[상승] 한글 폰트 없음: " + FontPath + " — 아무것도 바꾸지 않는다.");
                return -1;
            }

            var worldMat = AssetDatabase.LoadAssetAtPath<Material>(WorldMatPath);
            if (worldMat == null)
            {
                report.AppendLine("[상승] 단면 머티리얼 없음: " + WorldMatPath + " — 아무것도 바꾸지 않는다.");
                return -1;
            }

            Material uguiMat = font.material;   // 폰트 에셋에 딸린 기본 머티리얼

            Scene scene = EditorSceneManager.GetActiveScene();
            var targets = CollectKoreanLabels(scene);

            int changed = 0;
            report.AppendLine("[상승] 한글 라벨 폰트 교정 — 대상 " + targets.Count + "개");
            report.AppendLine("       폰트 " + font.name + " (" + font.atlasPopulationMode + ", 글리프 " + font.glyphTable.Count + ")");

            foreach (TMP_Text label in targets)
            {
                bool isWorld = label is TextMeshPro;
                Material want = isWorld ? worldMat : uguiMat;

                bool fontWrong = label.font != font;
                bool matWrong = label.fontSharedMaterial != want;
                if (!fontWrong && !matWrong) continue;

                Undo.RecordObject(label, "Fix Korean Label Font");

                // 폰트를 먼저 대입한다 — TMP 가 이때 머티리얼을 폰트 기본값으로
                // 덮어쓰므로, 원하는 머티리얼은 **그 다음에** 넣어야 한다.
                if (fontWrong) label.font = font;
                if (label.fontSharedMaterial != want) label.fontSharedMaterial = want;

                EditorUtility.SetDirty(label);
                changed++;
                report.AppendLine("  " + Path(label.transform)
                                  + "  font→" + font.name
                                  + "  mat→" + want.name);
            }

            report.AppendLine("[상승] 참조 변경 " + changed + "개 (0 이면 이미 정상 — 멱등)");
            if (changed > 0) EditorSceneManager.MarkSceneDirty(scene);

            // 참조를 바꿨으니 메시를 다시 굽는다. 이 호출이 있어야 `NanumGothic SDF`
            // 의 동적 아틀라스에 아직 없는 글리프(`/`, `:` 등)가 원본 TTF 에서
            // 구워져 들어온다.
            foreach (TMP_Text label in targets) label.ForceMeshUpdate(true, true);

            int cleared = ClearStaleSubMeshes(targets, report);
            report.AppendLine("[상승] 유령 서브메시 정리 " + cleared + "개 (0 이면 이미 깨끗 — 멱등)");
            return changed + cleared;
        }

        /// <summary>
        /// 폰트를 바꾸면 글리프가 어느 아틀라스에서 오는지가 달라지고, 그에 따라
        /// TMP 가 만드는 **서브메시 구성이 바뀐다.** 문제는 TMP 가 더 이상 쓰지 않게
        /// 된 서브메시의 지오메트리를 **지우지 않고 그대로 남긴다**는 것이다.
        ///
        /// 실제로 이 작업 중에 관측했다 — `ExecutionLabel` 의 「실행」이 폰트 교체
        /// 후 본 메시(재질 인덱스 0)로 옮겨갔는데, 예전 폴백 경로로 만들어진
        /// `TMP SubMesh [LiberationSans SDF Material SingleSided + NanumGothic Atlas]`
        /// 가 **렌더러가 켜진 채 같은 자리에 12정점 6삼각형을 그대로 들고** 있었다.
        /// 그대로 두면 같은 글자가 두 번, 그것도 SDF 그라디언트 스케일이 다른
        /// 재질로 겹쳐 그려져 획이 뭉개진다.
        ///
        /// 이 오브젝트들은 `HideFlags.DontSave` 라 씬에 직렬화되지 않는다. 즉
        /// 「씬 오브젝트」가 아니라 TMP 의 런타임 산출물이다. 그래서 **지우지 않고
        /// 메시만 비운다** — 다음 재생성 때 TMP 가 필요하면 알아서 다시 채운다.
        /// </summary>
        /// <summary>
        /// 씬 전체를 대상으로 한 번 돌린다. **문구를 바꾼 쪽이 부르는 진입점이다.**
        ///
        /// 2026-08-04 3차 평가의 「전력 0 / — 분모 부재」가 이 함수를 안 부른 결과였다.
        /// 폰트를 바꿀 때만 유령이 생기는 게 아니라 **문구를 바꿔 글리프가 쓰던 아틀라스가
        /// 빠질 때도** 생긴다. 실제로 `전력 N / 요구 M` → `전력 N` 에서 `/` 하나가
        /// 서브메시에 남아 세 라운드 동안 화면에 그려졌다.
        ///
        /// 한글 여부를 가리지 않는다 — 유령은 어느 라벨에서도 생길 수 있다.
        /// </summary>
        public static int ClearGhostSubMeshes(Scene scene, StringBuilder report)
        {
            var all = new List<TMP_Text>();
            foreach (TMP_Text t in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (t == null) continue;
                if (t.gameObject.scene != scene) continue;
                if (EditorUtility.IsPersistent(t.gameObject)) continue;
                t.ForceMeshUpdate();
                all.Add(t);
            }
            return ClearStaleSubMeshes(all, report);
        }

        private static int ClearStaleSubMeshes(List<TMP_Text> targets, StringBuilder report)
        {
            int cleared = 0;
            foreach (TMP_Text label in targets)
            {
                TMP_TextInfo info = label.textInfo;
                if (info == null) continue;

                // 지금 실제로 쓰이는 서브메시 재질 목록 (인덱스 0 은 본 메시라 제외).
                var live = new List<Material>();
                for (int i = 1; i < info.materialCount && i < info.meshInfo.Length; i++)
                    if (info.meshInfo[i].vertexCount > 0 && info.meshInfo[i].material != null)
                        live.Add(info.meshInfo[i].material);

                foreach (TMP_SubMesh sub in label.GetComponentsInChildren<TMP_SubMesh>(true))
                {
                    if (sub.sharedMaterial != null && live.Contains(sub.sharedMaterial)) continue;

                    var filter = sub.GetComponent<MeshFilter>();
                    Mesh mesh = filter != null ? filter.sharedMesh : null;
                    if (mesh == null || mesh.vertexCount == 0) continue;

                    report.AppendLine("  유령 " + Path(label.transform) + " / " + sub.gameObject.name
                                      + "  vtx " + mesh.vertexCount + "→0");
                    mesh.Clear();
                    cleared++;
                }
            }
            return cleared;
        }

        /// <summary>문구에 비 ASCII 문자가 하나라도 있는 씬 안의 TMP 라벨.</summary>
        public static List<TMP_Text> CollectKoreanLabels(Scene scene)
        {
            var found = new List<TMP_Text>();
            foreach (TMP_Text t in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (t == null) continue;
                if (t.gameObject.scene != scene) continue;
                if (EditorUtility.IsPersistent(t.gameObject)) continue;
                if (!HasNonAscii(t.text)) continue;
                found.Add(t);
            }
            return found;
        }

        public static bool HasNonAscii(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s) if (c > 127) return true;
            return false;
        }

        public static string Path(Transform t)
        {
            string s = t.name;
            while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }
    }
}
