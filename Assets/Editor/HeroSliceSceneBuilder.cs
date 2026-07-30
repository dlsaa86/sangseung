using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
using Ascend.Prototype.Player;
using Ascend.Prototype.Run;
using Ascend.Prototype.View;

namespace Ascend.Prototype.EditorTools
{
    /// <summary>
    /// Hero Slice에 필요한 씬 오브젝트를 만들고 배선한다.
    ///
    /// 씬을 손으로 편집하지 않고 스크립트로 짓는 이유: `.unity`는 fileID로 상호 참조되는
    /// YAML이라 손편집·머지에서 조용히 손상된다(`CLAUDE.md` "왜 필요한가"). 스크립트는
    /// 몇 번을 돌려도 같은 결과를 내고, 결과가 마음에 안 들면 지우고 다시 지으면 된다.
    ///
    /// **멱등**이다. 이미 있으면 지우고 다시 만든다.
    /// </summary>
    public static class HeroSliceSceneBuilder
    {
        private const string MaterialRoot = "Assets/Prototype_Elevator/Materials/Graybox";
        private const string FontPath = "Assets/Prototype_Elevator/Fonts/NanumGothic SDF.asset";

        [MenuItem("Ascend/Build Hero Slice Scene Objects")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[상승] Play 모드에서는 씬을 짓지 않는다. 먼저 Play를 끈다.");
                return;
            }

            GameObject car = GameObject.Find("Car");
            if (car == null) { Debug.LogError("[상승] Car 를 찾지 못했다. 씬이 열려 있는지 확인한다."); return; }

            GameObject overharvest = BuildOverharvestLever(car.transform);
            EnsurePresenter();
            WireBridge(overharvest);
            DisableDeadGrayboxView();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[상승] Hero Slice 씬 오브젝트 구축 완료.");
        }

        // ── 과수확 레버 ──

        /// <summary>
        /// 위치 근거: 뒷벽에 [계기판 — 전력/요구] → [과수확 레버 — 한 번 더] → [전력 탱크 — 확정]
        /// 순서로 늘어놓는다. 세 물체가 한 줄로 이야기를 만들어야 플레이어가 "지금 무엇을
        /// 고르는 중인가"를 공간에서 읽는다(`visual-criteria.md` B-3.8, B-4.12).
        ///
        /// 실행 레버는 반대쪽(콘솔, x≈-1.02)에 있으므로 물리적으로 헷갈릴 수 없다.
        /// </summary>
        private static GameObject BuildOverharvestLever(Transform car)
        {
            Replace(car, "OverharvestLever", out GameObject root);
            root.transform.localPosition = new Vector3(0.55f, 1.32f, 1.36f);

            Material recessive = Mat("M_Gray_Recessive");
            Material interactive = Mat("M_Gray_Interactive");
            Material panel = Mat("M_Gray_Panel");
            Material light = Mat("M_Gray_Light");
            Material button = Mat("M_Gray_Button");

            // 덩어리 자체가 커야 "크고 무거운 별도 레버"로 읽힌다(MASTER_PRD §7).
            // 실행 레버는 축 0.11×0.52. 여기는 하우징만 0.46×0.80이다.
            GameObject housing = Box(root.transform, "Housing", Vector3.zero,
                new Vector3(0.46f, 0.80f, 0.22f), recessive);
            BoxCollider housingCollider = housing.AddComponent<BoxCollider>();

            // 색 하나로 위험을 말하지 않는다(VISUAL_SPEC §7). 사선 경고 띠는 회색조에서도 남는다.
            Box(root.transform, "WarningStripe", new Vector3(0f, -0.30f, -0.115f),
                new Vector3(0.46f, 0.09f, 0.02f), button);
            Box(root.transform, "WarningStripe_Upper", new Vector3(0f, 0.30f, -0.115f),
                new Vector3(0.46f, 0.09f, 0.02f), button);

            // 손잡이 — 덮개 뒤에 있다.
            var handlePivot = new GameObject("HandlePivot");
            handlePivot.transform.SetParent(root.transform, false);
            handlePivot.transform.localPosition = new Vector3(0f, -0.12f, -0.14f);
            Box(handlePivot.transform, "HandleShaft", new Vector3(0f, 0.26f, 0f),
                new Vector3(0.09f, 0.52f, 0.09f), interactive);
            GameObject grip = Box(handlePivot.transform, "HandleGrip", new Vector3(0f, 0.54f, -0.05f),
                new Vector3(0.24f, 0.13f, 0.17f), interactive);
            BoxCollider gripCollider = grip.AddComponent<BoxCollider>();

            // 보호 덮개 — 잠금의 물리적 표현. 닫혀 있으면 손잡이가 실제로 가려진다.
            var coverPivot = new GameObject("CoverPivot");
            coverPivot.transform.SetParent(root.transform, false);
            coverPivot.transform.localPosition = new Vector3(0f, 0.36f, -0.13f);
            Box(coverPivot.transform, "CoverPlate", new Vector3(0f, -0.24f, -0.02f),
                new Vector3(0.42f, 0.48f, 0.03f), panel);
            Box(coverPivot.transform, "CoverRib", new Vector3(0f, -0.24f, -0.04f),
                new Vector3(0.06f, 0.48f, 0.02f), recessive);

            GameObject lockLight = Box(root.transform, "LockLight",
                new Vector3(0f, 0.30f, -0.125f), new Vector3(0.13f, 0.07f, 0.02f), light);

            Label(root.transform, "OverharvestLabel", new Vector3(0f, 0.52f, -0.13f),
                "과수확", 0.16f, TextAlignmentOptions.Center);

            var lever = root.AddComponent<InteractableOverharvestLever>();
            var so = new SerializedObject(lever);
            so.FindProperty("_grip").objectReferenceValue = gripCollider;
            so.FindProperty("_coverPivot").objectReferenceValue = coverPivot.transform;
            so.FindProperty("_closedAngle").floatValue = 0f;
            so.FindProperty("_openAngle").floatValue = 105f;      // 앞·위로 젖혀져 열린 것이 보인다
            so.FindProperty("_handlePivot").objectReferenceValue = handlePivot.transform;
            so.FindProperty("_handleRestAngle").floatValue = -20f;
            so.FindProperty("_handlePulledAngle").floatValue = 55f;
            so.FindProperty("_lockLight").objectReferenceValue = lockLight.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();

            // 하우징 콜라이더는 항상 켜 둔다. 잠긴 동안에도 조준은 걸려야
            // "왜 못 쓰는지"가 프롬프트로 읽힌다. 클릭은 CanInteract가 막는다.
            housingCollider.enabled = true;

            return root;
        }

        // ── 배선 ──

        /// <summary>
        /// 연출자는 런 오브젝트(AscendRun)에 붙인다. 결과판 뷰와 같은 오브젝트에 두면
        /// 뷰를 끄는 순간 연출도 같이 죽어 "왜 입력이 안 풀리지"가 된다.
        /// </summary>
        private static void EnsurePresenter()
        {
            var run = Object.FindAnyObjectByType<RunSessionBehaviour>();
            if (run == null) { Debug.LogError("[상승] RunSessionBehaviour 가 씬에 없다."); return; }

            var presenter = run.GetComponent<SpinPresenter>();
            if (presenter == null) presenter = run.gameObject.AddComponent<SpinPresenter>();

            var so = new SerializedObject(presenter);
            so.FindProperty("_board").objectReferenceValue = Object.FindAnyObjectByType<SpinBoardView>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireBridge(GameObject overharvest)
        {
            var bridge = Object.FindAnyObjectByType<RouletteInteractionBridge>();
            if (bridge == null) { Debug.LogError("[상승] RouletteInteractionBridge 가 씬에 없다."); return; }

            var so = new SerializedObject(bridge);
            so.FindProperty("_overharvestLever").objectReferenceValue =
                overharvest.GetComponent<InteractableOverharvestLever>();

            var presenter = Object.FindAnyObjectByType<SpinPresenter>();
            if (presenter != null)
                so.FindProperty("_presentationSource").objectReferenceValue = presenter;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// `ElevatorGrayboxView`는 폐기된 `RunController`를 참조해 첫 줄에서 return한다.
        /// 고치지 않고 끈다 — 폐기 경로를 재작성하는 것은 이번 Phase의 일이 아니고,
        /// 켜 둔 채로 두면 "계기판이 왜 안 움직이지"를 다음 세션이 또 조사하게 된다.
        /// </summary>
        private static void DisableDeadGrayboxView()
        {
            var view = Object.FindAnyObjectByType<ElevatorGrayboxView>(FindObjectsInactive.Include);
            if (view != null && view.enabled)
            {
                view.enabled = false;
                Debug.Log("[상승] ElevatorGrayboxView 비활성화 — 폐기된 RunController 참조로 죽어 있던 컴포넌트.");
            }
        }

        // ── 헬퍼 ──

        private static void Replace(Transform parent, string name, out GameObject created)
        {
            Transform existing = parent.Find(name);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            created = new GameObject(name);
            created.transform.SetParent(parent, false);
        }

        private static GameObject Box(Transform parent, string name, Vector3 localPosition,
                                      Vector3 localScale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<BoxCollider>());   // 필요할 때만 다시 붙인다
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static void Label(Transform parent, string name, Vector3 localPosition,
                                  string text, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            // 뒷벽에 붙은 글자는 방 안쪽(-Z)을 봐야 한다.
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = size * 40f;
            tmp.alignment = align;
            tmp.enableWordWrapping = false;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) tmp.font = font;
            tmp.rectTransform.sizeDelta = new Vector2(1.2f, 0.3f);
        }

        private static Material Mat(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/{name}.mat");
            if (material == null) Debug.LogWarning($"[상승] 머티리얼을 찾지 못했다: {name}");
            return material;
        }
    }
}
