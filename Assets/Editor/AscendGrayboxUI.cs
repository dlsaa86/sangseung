using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ascend.CaptureHarness
{
    /// <summary>
    /// 층수 표시와 계약 패널 그레이박스를 **다시 실행해도 같은 결과가 나오게** 조립한다.
    ///
    /// ## 왜 손으로 배치하지 않는가
    ///
    /// `CLAUDE.md` 의 Pass 1 리듬이 요구한다 — 「씬 오브젝트를 반복 수작업 배치하지 말고
    /// 데이터 기반 런타임 생성기 또는 **재실행 가능한 Editor 조립 스크립트**를 쓴다.
    /// 직렬화가 유일한 병목이기 때문이다.」
    ///
    /// 이 둘은 2026-08-08 에 MCP 명령으로 한 번씩 배치됐다. 그 명령은 대화 안에만
    /// 있었으므로 씬을 되돌리는 순간 배치가 사라지고, 좌표를 다시 찾아내야 했다.
    /// 이 파일이 그 좌표의 **유일한 출처**다.
    ///
    /// ## 좌표의 근거 (전부 실측이다)
    ///
    ///   · 캐빈 내부      x −2.61~2.61 · y 0~3.00 · z −2.61~2.61
    ///   · 기계 프레임    y 0~2.72, z 2.58~2.61 → 그 **위 y 2.72~3.00 이 비어 있다**
    ///   · 플레이어 카메라 (0, 1.70, −1.70), +z 를 본다
    ///
    /// 층수 표시를 기계 위에 두는 것은 실제 엘리베이터의 관습이고, 여기서는 그 자리가
    /// 유일하게 비어 있는 자리이기도 하다.
    ///
    /// 계약 패널은 **새로 만들지 않는다.** 이미 `GrayboxWorld/Car/ContractPanel` 로
    /// 존재하고 `InteractableContractPanel` · `BoxCollider` 가 붙어 있으며
    /// `RouletteInteractionBridge._contractPanel` 이 그것을 가리킨다. 문제는 그것이
    /// **z −1.80, 즉 플레이어 등 뒤**에 있었다는 것뿐이다. 그래서 이 스크립트는
    /// 위치만 옮기고 표시용 자식을 붙인다 — 지우고 다시 만들면 그 배선이 끊긴다.
    ///
    /// ## 방향 함정
    ///
    /// 패널은 `LookRotation(Vector3.left)` 라 **로컬 +z 가 월드 −x(방 쪽)** 이다.
    /// 부호를 반대로 주면 슬롯이 벽 속에 박히고 화면에는 그냥 「안 보임」으로 나타난다.
    /// TMP 3D 글자는 로컬 +z 쪽에서 읽히므로 라벨은 180° 돌려야 앞면이 보인다.
    /// 둘 다 실제로 겪었다.
    /// </summary>
    public static class AscendGrayboxUI
    {
        // ── 층수 표시 ──────────────────────────────────────────────────────
        private static readonly Vector3 FloorDisplayPos = new Vector3(0f, 2.82f, 2.55f);
        private static readonly Vector3 FloorPlateScale = new Vector3(1.10f, 0.24f, 0.04f);
        private const float FloorFontSize = 1.6f;

        // ── 계약 패널 ──────────────────────────────────────────────────────
        private static readonly Vector3 ContractPanelPos = new Vector3(2.18f, 1.50f, 0.90f);
        private static readonly Vector3 SlotScale = new Vector3(0.62f, 0.24f, 0.06f);
        private const float SlotSpacing = 0.30f;
        private const float SlotForward = 0.42f;   // 로컬 +z = 방 쪽
        private const float LabelForward = 0.55f;  // 슬롯 로컬 z (슬롯 스케일 0.06 이 곱해진다)
        private const float SlotFontSize = 0.9f;

        [MenuItem("Ascend/Assemble Graybox UI")]
        public static void Assemble()
        {
            var car = GameObject.Find("GrayboxWorld/Car");
            if (car == null) { Debug.LogError("[상승] GrayboxWorld/Car 를 못 찾았다. 씬이 열려 있는가?"); return; }

            TMP_FontAsset font = FindFont();
            BuildFloorDisplay(car.transform, font);
            BuildContractPanel(font);

            EditorSceneManager.MarkSceneDirty(car.scene);
            Debug.Log("[상승] 그레이박스 UI 조립 완료 — 층수 표시 @ " + FloorDisplayPos
                    + " · 계약 패널 @ " + ContractPanelPos);
        }

        /// <summary>기존 라벨과 **같은 폰트**를 쓴다. 화면에 두 서체가 섞이면 그것부터 눈에 띈다.</summary>
        private static TMP_FontAsset FindFont()
        {
            var src = GameObject.Find("GrayboxWorld/Car/InstrumentPanel/FloorLabel");
            var t = src != null ? src.GetComponent<TMP_Text>() : null;
            return t != null ? t.font : null;
        }

        private static void BuildFloorDisplay(Transform car, TMP_FontAsset font)
        {
            Transform root = car.Find("FloorDisplay");
            if (root == null)
            {
                var go = new GameObject("FloorDisplay");
                Undo.RegisterCreatedObjectUndo(go, "층수 표시 생성");
                go.transform.SetParent(car, false);
                root = go.transform;
            }
            Undo.RecordObject(root, "층수 표시 배치");
            root.position = FloorDisplayPos;
            root.rotation = Quaternion.identity;

            Transform plate = root.Find("Plate");
            if (plate == null)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                p.name = "Plate";
                p.transform.SetParent(root, false);
                // 조준을 가로막지 않는다 — 이건 표시일 뿐 조작물이 아니다.
                Object.DestroyImmediate(p.GetComponent<Collider>());
                plate = p.transform;
            }
            plate.localPosition = Vector3.zero;
            plate.localScale = FloorPlateScale;
            var pm = LoadMaterial("M_Gray_Panel");
            if (pm != null) plate.GetComponent<Renderer>().sharedMaterial = pm;

            Transform label = root.Find("Label");
            TextMeshPro tmp;
            if (label == null)
            {
                var l = new GameObject("Label");
                l.transform.SetParent(root, false);
                tmp = l.AddComponent<TextMeshPro>();
                label = l.transform;
            }
            else tmp = label.GetComponent<TextMeshPro>();

            label.localPosition = new Vector3(0f, 0f, -0.03f);   // 판보다 플레이어 쪽으로
            label.localRotation = Quaternion.identity;
            if (font != null) tmp.font = font;
            tmp.fontSize = FloorFontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.95f, 0.88f, 0.62f, 1f);
            tmp.rectTransform.sizeDelta = new Vector2(1.05f, 0.22f);
            // 플레이스홀더. 런이 붙으면 `FloorNumberDisplayView` 가 첫 프레임에 덮어쓴다.
            if (string.IsNullOrEmpty(tmp.text)) tmp.text = "1층 / 10";

            var view = root.GetComponent<Ascend.Prototype.View.FloorNumberDisplayView>();
            if (view == null) view = root.gameObject.AddComponent<Ascend.Prototype.View.FloorNumberDisplayView>();
            var so = new SerializedObject(view);
            so.FindProperty("_label").objectReferenceValue = tmp;
            so.FindProperty("_run").objectReferenceValue =
                Object.FindFirstObjectByType<Ascend.Prototype.Run.RunSessionBehaviour>(FindObjectsInactive.Include);
            so.ApplyModifiedProperties();
        }

        private static void BuildContractPanel(TMP_FontAsset font)
        {
            var panel = GameObject.Find("GrayboxWorld/Car/ContractPanel");
            if (panel == null)
            {
                Debug.LogError("[상승] ContractPanel 이 없다. 이 스크립트는 그것을 **만들지 않는다** — "
                             + "기존 오브젝트에 InteractableContractPanel 과 브리지 배선이 붙어 있어서, "
                             + "새로 만들면 그 배선이 끊긴다.");
                return;
            }

            Undo.RecordObject(panel.transform, "계약 패널 배치");
            panel.transform.position = ContractPanelPos;
            // 로컬 +z 가 월드 −x(방 쪽)를 향한다.
            panel.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);

            var slotMat = LoadMaterial("M_Gray_Button");
            var slots = new Renderer[3];
            var labels = new TMP_Text[3];

            for (int i = 0; i < 3; i++)
            {
                Transform slot = panel.transform.Find("Slot_" + i);
                if (slot == null)
                {
                    var s = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    s.name = "Slot_" + i;
                    s.transform.SetParent(panel.transform, false);
                    // 조준은 패널 몸체의 BoxCollider 가 통째로 받는다. 슬롯이 콜라이더를
                    // 들고 있으면 세 칸이 각각 조준 대상이 되어 「패널을 눌렀다」가 흐려진다.
                    Object.DestroyImmediate(s.GetComponent<Collider>());
                    slot = s.transform;
                }
                slot.localPosition = new Vector3(0f, SlotSpacing - i * SlotSpacing, SlotForward);
                slot.localRotation = Quaternion.identity;
                slot.localScale = SlotScale;
                var sr = slot.GetComponent<Renderer>();
                if (slotMat != null) sr.sharedMaterial = slotMat;
                slots[i] = sr;

                Transform lt = slot.Find("Label_" + i);
                TextMeshPro tmp;
                if (lt == null)
                {
                    var l = new GameObject("Label_" + i);
                    l.transform.SetParent(slot, false);
                    tmp = l.AddComponent<TextMeshPro>();
                    lt = l.transform;
                }
                else tmp = lt.GetComponent<TextMeshPro>();

                lt.localPosition = new Vector3(0f, 0f, LabelForward);
                // TMP 3D 글자는 로컬 +z 쪽에서 읽힌다 — 안 돌리면 좌우 반전으로 보인다.
                lt.localRotation = Quaternion.Euler(0f, 180f, 0f);
                // 부모(슬롯)의 비균등 스케일을 상쇄해 글자가 찌그러지지 않게 한다.
                lt.localScale = new Vector3(1f / SlotScale.x, 1f / SlotScale.y, 1f);
                if (font != null) tmp.font = font;
                tmp.fontSize = SlotFontSize;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.rectTransform.sizeDelta = new Vector2(0.58f, 0.22f);
                if (string.IsNullOrEmpty(tmp.text)) tmp.text = "계약 " + (i + 1);
                labels[i] = tmp;
            }

            var view = panel.GetComponent<Ascend.Prototype.View.ContractPanelGrayboxView>();
            if (view == null) view = panel.AddComponent<Ascend.Prototype.View.ContractPanelGrayboxView>();
            var so = new SerializedObject(view);
            var sp = so.FindProperty("_slots"); sp.arraySize = 3;
            var lp = so.FindProperty("_labels"); lp.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                sp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
                lp.GetArrayElementAtIndex(i).objectReferenceValue = labels[i];
            }
            so.FindProperty("_run").objectReferenceValue =
                Object.FindFirstObjectByType<Ascend.Prototype.Run.RunSessionBehaviour>(FindObjectsInactive.Include);
            so.FindProperty("_bridge").objectReferenceValue =
                Object.FindFirstObjectByType<Ascend.Prototype.Run.RouletteInteractionBridge>(FindObjectsInactive.Include);
            so.ApplyModifiedProperties();

            // 조준 상자는 패널 몸체인데 빛나야 할 것은 선택지 3칸까지다.
            var hint = panel.GetComponent<Ascend.Prototype.Player.InteractableHighlightTarget>();
            if (hint == null) hint = panel.AddComponent<Ascend.Prototype.Player.InteractableHighlightTarget>();
            var hso = new SerializedObject(hint);
            hso.FindProperty("_root").objectReferenceValue = panel.transform;
            hso.ApplyModifiedProperties();
        }

        private static Material LoadMaterial(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Material"))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m != null && m.name == name) return m;
            }
            Debug.LogWarning("[상승] 머티리얼 " + name + " 을 못 찾았다. 기본 재질로 진행한다.");
            return null;
        }
    }
}
