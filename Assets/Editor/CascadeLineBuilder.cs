using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ascend.Prototype.View;

namespace Ascend.Prototype.EditorTools
{
    /// <summary>
    /// **연쇄 계수기를 계기판 배킹판의 여섯째 줄로 옮긴다** (`UP-FIX-44` · `PD-21`).
    ///
    /// ── 무엇이 문제였나 ─────────────────────────────────────────────────────
    ///
    /// 「값을 만들어라」가 아니다. 기구도 값도 이미 있고 `22`·`23`·`24` 에 실제로 렌더된다.
    /// 문제는 그 라벨이 **캐빈 한가운데 낮은 곳(0, 0.62, 0)에 독립 월드 오브젝트**로
    /// 놓여 있다는 것이다. 결과가 셋이다 —
    ///
    /// | 장 | 지금 | 옮긴 뒤 |
    /// |---|---|---|
    /// | `15`·`19` | 시야 밖 — 글자가 하나도 없다 | 계기판이 프레임에 있으면 함께 들어온다 |
    /// | `24` | 하단 19% 를 먹고 「단계」가 잘린다 | 계기판 안이라 크기가 배킹판에 종속된다 |
    /// | 배킹판 | 없다 (10라운드 미해결) | `PanelBack` 이 뒤를 받친다 |
    ///
    /// ── 왜 y = 1.264 인가 — 남은 자리가 거기 하나다 ─────────────────────────
    ///
    /// 계기판 라벨은 pivot(0, 0.5)·TopLeft 라 **글자 줄상자가 `localY + 0.136 … +0.220`**
    /// 에 놓인다(네 라벨에서 실측해 확인한 관계다). 판 로컬 y 로 이미 차 있는 것들:
    ///
    /// | 것 | y 구간 |
    /// |---|---|
    /// | `PanelBack` (넣어야 하는 범위) | 1.275 … 1.825 |
    /// | FloorLabel / PowerLabel / RequiredLabel | 1.936…2.020 / 1.836…1.920 / 1.736…1.820 |
    /// | StatusLabel 1줄 / 2줄 | 1.576…1.660 / **1.489**…1.573 |
    /// | PowerBarTicks | 1.205 … **1.395** |
    ///
    /// 남는 띠는 **1.395 … 1.489** 하나이고 폭이 0.094 다. 줄상자는 0.084 라 들어간다.
    /// 가운데에 놓으면 상자가 1.400…1.484 → 위아래 5 mm 씩 남는다.
    /// 그래서 `localY = 1.400 − 0.136 = 1.264`.
    ///
    /// ⚠ **`PanelBack` 을 키우지 않았다.** 위로 늘리면 `OverloadHousing`(y 1.87…2.03,
    /// z 1.435…1.485)이 판(z 1.420…1.480) 속에 묻혀 사라진다. 그건 다른 것을 깨는 것이다.
    /// 그래서 `FloorLabel`·`PowerLabel` 두 줄은 여전히 판 위쪽 밖에 있다 — 이 작업이
    /// 닫는 것은 **여섯째 줄의 배킹**이지 위 두 줄이 아니다. 숨기지 않고 적는다.
    ///
    /// ── 멱등 ────────────────────────────────────────────────────────────────
    ///
    /// 전부 절대값이다. 두 번 돌리면 같은 좌표·같은 크기가 나온다.
    /// 이미 옮겨져 있으면 다시 옮기지 않고 값만 덮어쓴다.
    /// </summary>
    public static class CascadeLineBuilder
    {
        /// <summary>계기판 로컬 좌표. x·z 는 형제 라벨 넷과 **같은 값**이다.</summary>
        public static readonly Vector3 LabelLocalPosition = new Vector3(-1.580f, 1.264f, 1.380f);

        /// <summary>형제 라벨과 같은 스케일. x 는 계기판의 0.62 가로 압축을 되돌린 값이다.</summary>
        public static readonly Vector3 LabelLocalScale = new Vector3(0.118f, 0.073f, 0.073f);

        /// <summary>`InstrumentPanelView._baseFontSize × _textScale` 과 같은 값.</summary>
        public const float FontSize = 10f;

        /// <summary>`InstrumentPanelView._leftPadUnits`. 통관 실루엣을 피하려고 넣은 여백이다.</summary>
        public const float LeftPadUnits = 5.20f;

        public static readonly Vector2 RectSize = new Vector2(26f, 6f);

        /// <summary>기대 줄상자(판 로컬 y). 검증이 이 값으로 회귀를 잡는다.</summary>
        public const float ExpectedLineBottom = 1.400f;
        public const float ExpectedLineTop    = 1.484f;

        [MenuItem("Ascend/Graphics/Move Cascade Line Into Panel")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogError("[상승] Play 모드에서는 씬을 고치지 않는다."); return; }

            Scene scene = AscendGraphicsBuilder.EnsureScene();
            if (!scene.IsValid()) return;

            var report = new StringBuilder("[상승] 연쇄 줄을 계기판 여섯째 줄로 (`UP-FIX-44`)\n");

            var counter = Object.FindAnyObjectByType<CascadeCounterView>(FindObjectsInactive.Include);
            if (counter == null) { Debug.LogError("[상승] CascadeCounterView 를 찾지 못했다."); return; }

            Transform panel = null;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "InstrumentPanel") { panel = t; break; }
            if (panel == null) { Debug.LogError("[상승] InstrumentPanel 을 찾지 못했다."); return; }

            var sibling = panel.Find("FloorLabel")?.GetComponent<TextMeshPro>();
            if (sibling == null) { Debug.LogError("[상승] FloorLabel 이 없다 — 서식을 베낄 기준이 없다."); return; }

            Transform label = counter.transform;
            string before = $"{Path(label)} @ world {label.position:F3} · scale {label.localScale:F3}";

            // ── 이동 ────────────────────────────────────────────────────────
            Undo.RecordObject(label, "Move cascade line");
            if (label.parent != panel) label.SetParent(panel, false);
            label.name = "CascadeLabel";
            label.localPosition = LabelLocalPosition;
            label.localRotation = Quaternion.identity;
            label.localScale = LabelLocalScale;
            EditorUtility.SetDirty(label);

            // ── 서식을 형제 라벨에 맞춘다 ──────────────────────────────────
            var tmp = label.GetComponent<TextMeshPro>();
            if (tmp == null) { Debug.LogError("[상승] CascadeLabel 에 TextMeshPro 가 없다."); return; }

            Undo.RecordObject(tmp, "Style cascade line");
            var rect = (RectTransform)label;
            Undo.RecordObject(rect, "Style cascade line rect");
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = RectSize;

            tmp.font = sibling.font;
            tmp.fontSharedMaterial = sibling.fontSharedMaterial;
            tmp.fontSize = FontSize;
            tmp.enableAutoSizing = false;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.color = Color.white;
            tmp.margin = new Vector4(LeftPadUnits, 0f, 0f, 0f);
            tmp.enableWordWrapping = false;
            EditorUtility.SetDirty(tmp);
            EditorUtility.SetDirty(rect);

            // ── 연출자 배선 ────────────────────────────────────────────────
            var presenter = Object.FindAnyObjectByType<SpinPresenter>(FindObjectsInactive.Include);
            var so = new SerializedObject(counter);
            SerializedProperty p = so.FindProperty("_presenter");
            if (p != null) { p.objectReferenceValue = presenter; so.ApplyModifiedPropertiesWithoutUndo(); }
            report.AppendLine($"  연출자 배선 — {(presenter != null ? presenter.name : "없음(런타임 탐색에 맡긴다)")}");

            // ── 실측 검증 ──────────────────────────────────────────────────
            tmp.ForceMeshUpdate(true, true);
            report.AppendLine($"  이전 — {before}");
            report.AppendLine($"  이후 — {Path(label)} @ world {label.position:F3} · " +
                              $"판로컬 {label.localPosition:F3} · scale {label.localScale:F3}");
            report.AppendLine($"  글자 — \"{tmp.text}\" · fontSize {tmp.fontSize:F2} · margin.x {tmp.margin.x:F2} · " +
                              $"rect {rect.sizeDelta:F2} · pivot {rect.pivot:F2}");

            if (tmp.textInfo != null && tmp.textInfo.lineCount > 0)
            {
                TMP_LineInfo li = tmp.textInfo.lineInfo[0];
                // 판 로컬 y 로 되돌린다 — 계기판 루트는 y 회전만 하므로 월드 y = 판 로컬 y 다.
                float top = label.TransformPoint(new Vector3(0f, li.ascender, 0f)).y;
                float bot = label.TransformPoint(new Vector3(0f, li.descender, 0f)).y;
                report.AppendLine($"  줄상자 y — {bot:F3} … {top:F3} (기대 {ExpectedLineBottom:F3} … {ExpectedLineTop:F3})");

                Transform back = panel.Find("PanelBack");
                if (back != null)
                {
                    float bMin = back.localPosition.y - back.localScale.y * 0.5f;
                    float bMax = back.localPosition.y + back.localScale.y * 0.5f;
                    bool inside = bot >= bMin && top <= bMax;
                    report.AppendLine($"  배킹판 y — {bMin:F3} … {bMax:F3} · 줄이 판 **{(inside ? "안" : "밖")}**");
                    if (!inside) report.AppendLine("    ⚠ 판 밖이다 — `UP-FIX-44` 의 요구를 만족하지 않는다");
                }

                float statusL1 = 1.489f, tickTop = 1.395f;
                report.AppendLine($"  여유 — 눈금 위끝 {tickTop:F3} 까지 {bot - tickTop:F3} m · " +
                                  $"상태줄 2줄 아래끝 {statusL1:F3} 까지 {statusL1 - top:F3} m");
                if (bot <= tickTop || top >= statusL1)
                    report.AppendLine("    ⚠ 이웃과 겹친다 — y 를 다시 잡아야 한다");
            }

            // ── 계기판이 이제 몇 줄인가 ────────────────────────────────────
            int lines = 0;
            var names = new StringBuilder();
            foreach (TextMeshPro t in panel.GetComponentsInChildren<TextMeshPro>(true))
            {
                t.ForceMeshUpdate(true, true);
                int n = t.textInfo != null ? t.textInfo.lineCount : 1;
                lines += n;
                names.Append(t.name).Append('(').Append(n).Append(") ");
            }
            report.AppendLine($"  계기 시각 줄 — 총 {lines}줄 [{names.ToString().TrimEnd()}]");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(report.ToString());
        }

        private static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }
    }
}
