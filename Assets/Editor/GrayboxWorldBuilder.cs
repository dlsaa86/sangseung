using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ascend.Prototype;

/// <summary>
/// Builds the 3D graybox elevator car in the open scene.
///
/// Written as a builder rather than hand-placed objects so the layout is reproducible and
/// reviewable: re-running it rebuilds the world from scratch without disturbing GameSystems,
/// the tubes, or the 2D HUD.
/// </summary>
public static class GrayboxWorldBuilder
{
    private const string RootName = "GrayboxWorld";
    private const string MatDir = "Assets/Prototype_Elevator/Materials/Graybox";

    // Car volume. The front (-Z) is deliberately left open so the fixed camera can see inside.
    private const float CarLeft = -5.4f, CarRight = 5.4f;
    private const float FloorY = -3.2f, CeilY = 4.2f;
    private const float BackZ = 4.3f, FrontZ = -2.6f;

    // Tubes sit left of centre; the door takes the right of the back wall so they never overlap.
    private const float TubeCenterX = -2.0f, TubesZ = 2.6f, TubesY = 0.2f;
    private const float DoorMinX = 2.2f, DoorMaxX = 4.6f, DoorTopY = 1.3f;

    private const float ConsoleZ = -0.6f;
    private const float PanelY = 3.30f, PanelZ = 1.2f;

    [MenuItem("Ascend/Build Graybox World")]
    public static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.path.EndsWith("Prototype_Elevator.unity"))
        {
            Debug.LogError("[상승] Prototype_Elevator 씬을 먼저 열어라. 현재: " + scene.path);
            return;
        }

        var mats = BuildMaterials();

        // Rebuild from scratch so re-running never leaves stale duplicates behind.
        GameObject old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);

        var root = new GameObject(RootName);
        var car = new GameObject("Car");
        car.transform.SetParent(root.transform, false);

        BuildShell(car.transform, mats);
        var doorLeft = BuildDoor(car.transform, mats, out Transform doorRight);
        BuildConsole(car.transform, mats, out Transform[] buttons, out Renderer[] buttonRends);
        BuildPanel(car.transform, mats,
                   out TextMeshPro floorLabel, out TextMeshPro powerLabel,
                   out TextMeshPro weightLabel, out Transform barPivot, out Renderer overloadLight);
        Transform[] tubeLabelTs = BuildTubeLabels(car.transform, out TextMeshPro[] tubeLabels);

        var passengerAnchor = new GameObject("PassengerAnchor");
        passengerAnchor.transform.SetParent(car.transform, false);
        // Kept on the right half of the car so boarded passengers never stand in front of a tube.
        passengerAnchor.transform.localPosition = new Vector3(2.7f, FloorY + 0.15f, 0.7f);

        var candidateAnchor = new GameObject("CandidateAnchor");
        candidateAnchor.transform.SetParent(root.transform, false);
        candidateAnchor.transform.localPosition = new Vector3((DoorMinX + DoorMaxX) * 0.5f, FloorY + 0.15f, 6.2f);

        BuildLobby(root.transform, mats);

        MoveTubesIntoCar();
        SetUpCamera();
        SetUpLighting();

        WireView(root, car.transform, buttons, buttonRends, tubeLabels,
                 doorLeft, doorRight, passengerAnchor.transform, candidateAnchor.transform,
                 floorLabel, powerLabel, weightLabel, barPivot, overloadLight);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[상승] 그레이박스 월드 생성 완료. 통관 3개 앞에 대응 버튼 3개, 우측 문 뒤에 승객 후보가 배치된다.");
    }

    // ── Materials ──

    private class Mats
    {
        public Material Wall, Floor, Console, Button, Door, Panel, BarBg, BarFill, Light, Lobby;
    }

    private static Mats BuildMaterials()
    {
        Directory.CreateDirectory(MatDir);
        return new Mats
        {
            Wall    = Mat("M_Gray_Wall",    new Color(0.26f, 0.27f, 0.29f)),
            Floor   = Mat("M_Gray_Floor",   new Color(0.19f, 0.20f, 0.22f)),
            Console = Mat("M_Gray_Console", new Color(0.15f, 0.16f, 0.18f)),
            Button  = Mat("M_Gray_Button",  new Color(0.30f, 0.30f, 0.32f)),
            Door    = Mat("M_Gray_Door",    new Color(0.34f, 0.37f, 0.42f)),
            Panel   = Mat("M_Gray_Panel",   new Color(0.10f, 0.11f, 0.12f)),
            BarBg   = Mat("M_Gray_BarBg",   new Color(0.08f, 0.09f, 0.10f)),
            BarFill = Mat("M_Gray_BarFill", new Color(0.44f, 0.83f, 0.44f)),
            Light   = Mat("M_Gray_Light",   new Color(0.12f, 0.20f, 0.12f)),
            Lobby   = Mat("M_Gray_Lobby",   new Color(0.13f, 0.13f, 0.15f)),
        };
    }

    private static Material Mat(string name, Color color)
    {
        string path = $"{MatDir}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) { SetColor(existing, color); EditorUtility.SetDirty(existing); return existing; }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(shader) { name = name };
        SetColor(m, color);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    private static void SetColor(Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    // ── Primitive helpers ──

    private static Transform Box(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        return go.transform;
    }

    /// <summary>
    /// 이 프로젝트의 월드 라벨은 전부 한글이다. `AddComponent&lt;TextMeshPro&gt;()` 는
    /// 폰트를 TMP 기본값(`LiberationSans SDF`)으로 붙이는데 **그 폰트에는 한글
    /// 글리프가 없다** — 라벨이 씬에 있고 렌더러도 켜져 있는데 글자가 단 하나도
    /// 그려지지 않는다(vtx 4, 바운즈 0). 실제로 「실행」·「계약」·「탑승구」가 그
    /// 상태로 오래 방치돼 독립 평가에서 「표찰이 글자로 안 읽힌다」는 지적을 받았다.
    /// 그래서 생성 지점에서 한글 폰트를 못 박는다.
    /// </summary>
    private const string KoreanFontPath = "Assets/Prototype_Elevator/Fonts/NanumGothic SDF.asset";

    private static TextMeshPro Text(Transform parent, string name, Vector3 pos, float size,
                                    string content, Vector2 box, Vector3 euler)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        var tmp = go.AddComponent<TextMeshPro>();
        var koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
        if (koreanFont != null) tmp.font = koreanFont;
        else Debug.LogWarning("[상승] " + KoreanFontPath + " 없음 — " + name + " 의 한글이 안 그려진다.");
        tmp.text = content;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        var rt = tmp.GetComponent<RectTransform>();
        if (rt != null) rt.sizeDelta = box;
        return tmp;
    }

    // ── Car shell ──

    private static void BuildShell(Transform car, Mats m)
    {
        float w = CarRight - CarLeft;
        float d = BackZ - FrontZ;
        float cz = (BackZ + FrontZ) * 0.5f;

        Box(car, "Floor",   new Vector3(0f, FloorY, cz),           new Vector3(w, 0.3f, d), m.Floor);
        Box(car, "Ceiling", new Vector3(0f, CeilY, cz),            new Vector3(w, 0.3f, d), m.Wall);
        Box(car, "WallL",   new Vector3(CarLeft, 0.5f, cz),        new Vector3(0.3f, CeilY - FloorY, d), m.Wall);
        Box(car, "WallR",   new Vector3(CarRight, 0.5f, cz),       new Vector3(0.3f, CeilY - FloorY, d), m.Wall);

        // Back wall is split around the doorway instead of using one slab.
        float leftW = DoorMinX - CarLeft;
        Box(car, "BackWall_Left",  new Vector3(CarLeft + leftW * 0.5f, 0.5f, BackZ),
            new Vector3(leftW, CeilY - FloorY, 0.3f), m.Wall);

        float rightW = CarRight - DoorMaxX;
        Box(car, "BackWall_Right", new Vector3(DoorMaxX + rightW * 0.5f, 0.5f, BackZ),
            new Vector3(rightW, CeilY - FloorY, 0.3f), m.Wall);

        float lintelH = CeilY - DoorTopY;
        Box(car, "BackWall_Lintel", new Vector3((DoorMinX + DoorMaxX) * 0.5f, DoorTopY + lintelH * 0.5f, BackZ),
            new Vector3(DoorMaxX - DoorMinX, lintelH, 0.3f), m.Wall);
    }

    // ── Door ──

    private static Transform BuildDoor(Transform car, Mats m, out Transform right)
    {
        var doorRoot = new GameObject("Door");
        doorRoot.transform.SetParent(car, false);
        doorRoot.transform.localPosition = new Vector3((DoorMinX + DoorMaxX) * 0.5f, 0f, BackZ - 0.05f);

        float width = DoorMaxX - DoorMinX;
        float height = DoorTopY - FloorY;
        float half = width * 0.5f;

        Transform left = Box(doorRoot.transform, "DoorLeft",
            new Vector3(-half * 0.5f, FloorY + height * 0.5f, 0f), new Vector3(half, height, 0.12f), m.Door);
        right = Box(doorRoot.transform, "DoorRight",
            new Vector3(half * 0.5f, FloorY + height * 0.5f, 0f), new Vector3(half, height, 0.12f), m.Door);

        // Slide is applied to the panel roots, so bake the closed offset into a parent each.
        var lp = new GameObject("DoorLeftPivot"); lp.transform.SetParent(doorRoot.transform, false);
        var rp = new GameObject("DoorRightPivot"); rp.transform.SetParent(doorRoot.transform, false);
        left.SetParent(lp.transform, true);
        right.SetParent(rp.transform, true);

        Text(doorRoot.transform, "DoorSign", new Vector3(0f, DoorTopY + 0.45f, -0.2f), 1.5f,
             "탑승구", new Vector2(3f, 1f), Vector3.zero);

        right = rp.transform;
        return lp.transform;
    }

    // ── Console with the three stop buttons ──

    private static void BuildConsole(Transform car, Mats m, out Transform[] buttons, out Renderer[] rends)
    {
        var consoleRoot = new GameObject("Console");
        consoleRoot.transform.SetParent(car, false);

        // Buttons sit directly in front of their own tube so the 1:1 mapping is spatial,
        // not something the player has to memorise from the HUD.
        float[] xs = { TubeCenterX - 2f, TubeCenterX, TubeCenterX + 2f };
        float slabCenter = TubeCenterX;
        Box(consoleRoot.transform, "ConsoleSlab", new Vector3(slabCenter, -2.75f, ConsoleZ),
            new Vector3(6.4f, 0.35f, 1.5f), m.Console);

        // 통관별 정지 버튼은 **만들지 않는다.**
        //
        // `D-20260730-04`가 통관별 정지 버튼·타이밍 정지를 1차 프로토타입에서 제외했고,
        // `docs/VISUAL_CRITERIA.md` B-5.13은 "타이밍 바·정지 버튼·반응속도를 요구하는
        // UI가 남아 있는가 — 잔재가 보이면 실패다"를 감점 항목으로 못박았다.
        //
        // 예전에는 여기서 `StopButton_1~3`·`ButtonPivot_1~3`·`ButtonLabel_1~3`을 만든 뒤
        // `SceneIntegration`과 `HumanScaleLayout`이 나중에 **숨기는** 방식이었다.
        // 그러면 이 빌더를 다시 돌릴 때마다 폐기된 조작부가 되살아나고, 숨김 패스가
        // 한 번만 누락돼도 캡처에 노출된다. 안 만드는 것이 유일하게 안전한 상태다.
        //
        // 플레이어 판타지는 반응 조작자가 아니라 규칙을 설계하는 운영자다(`MASTER_PRD.md` §2.2).
        buttons = System.Array.Empty<Transform>();
        rends = System.Array.Empty<Renderer>();
    }

    // ── Instrument panel above the tubes ──

    private static void BuildPanel(Transform car, Mats m,
                                   out TextMeshPro floorLabel, out TextMeshPro powerLabel,
                                   out TextMeshPro weightLabel, out Transform barPivot,
                                   out Renderer overloadLight)
    {
        var panel = new GameObject("InstrumentPanel");
        panel.transform.SetParent(car, false);

        Box(panel.transform, "PanelBack", new Vector3(0f, PanelY - 0.15f, PanelZ + 0.25f),
            new Vector3(10.4f, 1.6f, 0.15f), m.Panel);

        floorLabel = Text(panel.transform, "FloorLabel", new Vector3(-4.0f, PanelY + 0.15f, PanelZ), 2.4f,
                          "0 / 10 층", new Vector2(4f, 1.4f), Vector3.zero);

        powerLabel = Text(panel.transform, "PowerLabel", new Vector3(-0.4f, PanelY + 0.35f, PanelZ), 1.5f,
                          "전력 0 / 0", new Vector2(4.4f, 1f), Vector3.zero);

        Box(panel.transform, "PowerBarBg", new Vector3(-0.4f, PanelY - 0.35f, PanelZ),
            new Vector3(2.7f, 0.28f, 0.08f), m.BarBg);

        var pivot = new GameObject("PowerBarPivot");
        pivot.transform.SetParent(panel.transform, false);
        pivot.transform.localPosition = new Vector3(-0.4f - 1.3f, PanelY - 0.35f, PanelZ - 0.06f);
        pivot.transform.localScale = new Vector3(0.0001f, 1f, 1f);
        Box(pivot.transform, "PowerBarFill", new Vector3(0.5f, 0f, 0f),
            new Vector3(1f, 0.2f, 0.06f), m.BarFill);
        barPivot = pivot.transform;

        weightLabel = Text(panel.transform, "WeightLabel", new Vector3(3.1f, PanelY + 0.15f, PanelZ), 1.4f,
                           "무게 0/0\n정상", new Vector2(4.2f, 1.4f), Vector3.zero);

        GameObject light = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        light.name = "OverloadLight";
        light.transform.SetParent(panel.transform, false);
        light.transform.localPosition = new Vector3(5.0f, PanelY + 0.1f, PanelZ);
        light.transform.localScale = Vector3.one * 0.55f;
        Object.DestroyImmediate(light.GetComponent<Collider>());
        light.GetComponent<Renderer>().sharedMaterial = m.Light;
        overloadLight = light.GetComponent<Renderer>();
    }

    // ── Per-tube harvest readouts ──

    private static Transform[] BuildTubeLabels(Transform car, out TextMeshPro[] labels)
    {
        var root = new GameObject("TubeReadouts");
        root.transform.SetParent(car, false);

        labels = new TextMeshPro[3];
        var ts = new Transform[3];
        float[] xs = { TubeCenterX - 2f, TubeCenterX, TubeCenterX + 2f };
        for (int i = 0; i < 3; i++)
        {
            labels[i] = Text(root.transform, $"TubeReadout_{i + 1}",
                             new Vector3(xs[i], TubesY - 2.55f, TubesZ - 1.9f), 2.4f,
                             "-", new Vector2(2.4f, 1.3f), Vector3.zero);
            ts[i] = labels[i].transform;
        }
        return ts;
    }

    // ── Lobby seen through the open door ──

    private static void BuildLobby(Transform root, Mats m)
    {
        var lobby = new GameObject("Lobby");
        lobby.transform.SetParent(root, false);
        float cx = (DoorMinX + DoorMaxX) * 0.5f;

        Box(lobby.transform, "LobbyFloor", new Vector3(cx, FloorY, 6.4f), new Vector3(6f, 0.3f, 4.5f), m.Lobby);
        Box(lobby.transform, "LobbyBack",  new Vector3(cx, 0.4f, 8.5f),   new Vector3(6f, 7f, 0.3f), m.Lobby);
    }

    // ── Existing objects ──

    private static void MoveTubesIntoCar()
    {
        GameObject tubes = GameObject.Find("TubesRoot");
        if (tubes == null) { Debug.LogWarning("[상승] TubesRoot 없음 — 통관 위치를 옮기지 못했다."); return; }
        Undo.RecordObject(tubes.transform, "Move tubes");
        tubes.transform.position = new Vector3(TubeCenterX, TubesY, TubesZ);
    }

    private static void SetUpCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) { Debug.LogWarning("[상승] Main Camera 없음"); return; }
        Undo.RecordObject(cam.transform, "Camera");
        Undo.RecordObject(cam, "Camera settings");
        cam.transform.position = new Vector3(0f, 0.9f, -9.2f);
        cam.transform.rotation = Quaternion.Euler(4f, 0f, 0f);
        cam.fieldOfView = 60f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
    }

    private static void SetUpLighting()
    {
        Light sun = null;
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { sun = l; break; }
        if (sun == null) return;
        Undo.RecordObject(sun.transform, "Light");
        Undo.RecordObject(sun, "Light settings");
        sun.transform.rotation = Quaternion.Euler(38f, 18f, 0f);
        sun.intensity = 1.15f;
        sun.color = new Color(1f, 0.97f, 0.92f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.26f, 0.27f, 0.31f);
    }

    // ── Wiring ──

    private static void WireView(GameObject root, Transform car,
                                 Transform[] buttons, Renderer[] rends, TextMeshPro[] tubeLabels,
                                 Transform doorLeft, Transform doorRight,
                                 Transform passengerAnchor, Transform candidateAnchor,
                                 TextMeshPro floorLabel, TextMeshPro powerLabel, TextMeshPro weightLabel,
                                 Transform barPivot, Renderer overloadLight)
    {
        var view = root.GetComponent<ElevatorGrayboxView>();
        if (view == null) view = root.AddComponent<ElevatorGrayboxView>();

        var run = Object.FindFirstObjectByType<RunController>();
        var so = new SerializedObject(view);

        so.FindProperty("_run").objectReferenceValue = run;
        so.FindProperty("_carRoot").objectReferenceValue = car;
        so.FindProperty("_doorLeft").objectReferenceValue = doorLeft;
        so.FindProperty("_doorRight").objectReferenceValue = doorRight;
        so.FindProperty("_passengerAnchor").objectReferenceValue = passengerAnchor;
        so.FindProperty("_candidateAnchor").objectReferenceValue = candidateAnchor;
        so.FindProperty("_floorLabel").objectReferenceValue = floorLabel;
        so.FindProperty("_powerLabel").objectReferenceValue = powerLabel;
        so.FindProperty("_weightLabel").objectReferenceValue = weightLabel;
        so.FindProperty("_powerBarPivot").objectReferenceValue = barPivot;
        so.FindProperty("_overloadLight").objectReferenceValue = overloadLight;

        FillArray(so.FindProperty("_buttons"), buttons);
        FillArray(so.FindProperty("_buttonRenderers"), rends);
        FillArray(so.FindProperty("_tubeLabels"), tubeLabels);

        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    private static void FillArray<T>(SerializedProperty prop, IList<T> items) where T : Object
    {
        if (prop == null) return;
        prop.ClearArray();
        prop.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
}
