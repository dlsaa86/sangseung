using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ascend.Prototype.Player;
using Ascend.Prototype.Run;

/// <summary>
/// 그레이박스를 사람 기준으로 재배치하고 1인칭 리그를 세운다.
///
/// 기존 배치는 디오라마였다. 실내 폭 10.5m·높이 7.1m에 계기판이 바닥에서 6.2m,
/// 콘솔 상판이 0.48m였다. 카메라를 차 바깥(z=-9.2)에 두고 무대를 들여다보는 전제다.
/// 사람 눈높이 1.6m를 기준으로 하면 계기판은 눈높이의 3.7배 위, 콘솔은 무릎 높이다.
/// 노션이 1인칭을 고정 조건으로 못 박았고 시각 기준도 "핵심 장치가 시선 높이에
/// 있어야 한다"고 요구하므로, 비율을 바꾸는 것 말고는 방법이 없다.
///
/// 균일 축소로는 안 된다. 0.25배로 줄이면 계기판은 눈높이에 맞지만 콘솔이 0.12m가
/// 되고 실내 높이도 1.85m라 사람이 설 수 없다. 원래 비율 자체가 사람 기준이 아니다.
/// 그래서 각 오브젝트를 개별 목표 수치로 다시 놓는다.
///
/// 좌표계: 바닥 윗면 y=0. 실내 폭 3.2m(x ±1.6), 깊이 3.0m(z ±1.5), 높이 2.5m.
/// 앞쪽(z=-1.5)은 열려 있다.
///
/// 멱등이다. 여러 번 돌려도 같은 결과가 나온다.
/// </summary>
public static class HumanScaleLayout
{
    private const string ScenePath = "Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity";

    private const float EyeHeight = 1.62f;   // 서 있는 성인 눈높이
    private const float DeskTop   = 1.00f;   // 콘솔 상판
    private const float PanelMid  = 1.55f;   // 계기판 중심 — 눈높이 언저리

    private static StringBuilder _log;
    private static Dictionary<string, Transform> _index;

    // ── 명도 위계 ────────────────────────────────────────────────────────
    //
    // 알베도만으로는 계층이 서지 않는다. 상자 안에 방향광이 하나뿐이라 광원을 향한 면은
    // 밝고 등진 면은 어두워지고, 그 차이가 알베도 차이보다 크다. 실제로 같은 레버가
    // 한 시점에서는 체감 0.2, 다른 시점에서는 0.7로 읽혀 계층이 뒤집혔다.
    //
    // 그래서 조작 대상은 자발광으로 만든다. 스스로 빛나는 면은 광원 각도와 무관하게
    // 일정한 밝기로 읽히므로, 어느 위치에서 봐도 "만질 수 있는 것"이 같은 값을 유지한다.
    // 구조물은 반대로 발광을 0으로 두어 조명에 따라 자연스럽게 가라앉는다.
    private const float ReadoutValue      = 0.85f;
    private const float ReadoutEmission   = 0.55f;   // 결과판 — 가장 밝게 스스로 빛난다
    private const float InteractiveValue  = 0.62f;
    private const float InteractiveEmission = 0.30f; // 조작 대상 — 중간. 각도 무관하게 유지된다
    private const float RecessiveValue    = 0.16f;   // 구조물 — 발광 없음

    private const string MatDir = "Assets/Prototype_Elevator/Materials/Graybox";

    [MenuItem("Ascend/Layout — 사람 기준 재배치")]
    public static void Run()
    {
        _log = new StringBuilder();
        _log.AppendLine("[상승] === 사람 기준 재배치 ===");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        BuildIndex(scene);

        LayoutShell();
        LayoutTubes();
        BuildBoardCells();
        LayoutConsole();
        LayoutPanel();
        HideDeprecated();
        GameObject player = BuildPlayerRig();
        BuildCrosshairUI(player);
        WireInteractables(player);
        ApplyValueHierarchy();

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        _log.AppendLine(saved ? "  씬 저장 완료" : "  FAIL  씬 저장 실패");

        Verify();
        Debug.Log(_log.ToString());
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────────

    private static void BuildIndex(Scene scene)
    {
        _index = new Dictionary<string, Transform>();
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                _index[t.name] = t;   // 이름이 겹치면 마지막 것. 이 씬에는 겹치는 이름이 없다.
    }

    private static Transform Find(string name)
    {
        _index.TryGetValue(name, out Transform t);
        if (t == null) _log.AppendLine($"  WARN  '{name}' 없음 — 건너뜀");
        return t;
    }

    /// <summary>월드 위치와 크기를 직접 지정한다. 부모 스케일이 1이므로 localScale이 곧 크기다.</summary>
    private static void Place(string name, Vector3 pos, Vector3 size)
    {
        Transform t = Find(name);
        if (t == null) return;
        Undo.RecordObject(t, "Human scale layout");
        t.position = pos;
        t.localScale = size;
        EnsureBoxCollider(t.gameObject);
    }

    private static void Move(string name, Vector3 pos)
    {
        Transform t = Find(name);
        if (t == null) return;
        Undo.RecordObject(t, "Human scale layout");
        t.position = pos;
    }

    /// <summary>
    /// 월드 텍스트를 폭·정렬·크기까지 고정해서 놓는다. 위치만 옮기면 글자 길이에 따라
    /// 이웃을 덮으므로, 시작점을 왼쪽으로 고정하고 폭을 명시한다.
    /// </summary>
    private static void LabelAt(string name, Vector3 pos, float width)
    {
        Transform t = Find(name);
        if (t == null) return;

        var text = t.GetComponent<TMPro.TMP_Text>();
        if (text == null) { Move(name, pos); return; }

        Undo.RecordObject(t, "Label layout");
        t.position = pos;

        // 글자 크기는 추측하지 않고 잰다. TMP의 fontSize가 어떤 월드 크기로 떨어지는지는
        // 폰트 에셋의 메트릭에 달려 있어서, 숫자를 넣고 눈으로 확인하는 방식으로는
        // 두 번 연속 틀렸다(0.11 → 깨알, 1.0/스케일 0.1 → 여전히 깨알).
        // 실제 렌더 크기를 측정해서 목표 높이에 맞춘다.
        const float targetCapHeight = 0.085f;          // 2.3m 거리에서 읽히는 크기

        var rt = text.rectTransform;
        rt.pivot = new Vector2(0f, 0.5f);              // 왼쪽 기준 — 글자가 오른쪽으로만 자란다
        text.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = false;
        text.overflowMode = TMPro.TextOverflowModes.Overflow;

        const float probe = 10f;
        text.fontSize = probe;
        t.localScale = Vector3.one;
        rt.sizeDelta = new Vector2(1000f, 100f);       // 측정 중에는 넉넉히
        text.ForceMeshUpdate();

        // 실제 문자열이 아니라 한 줄짜리 기준 문자열로 잰다. 텍스트가 2줄이면 전체 높이로
        // 맞춰져 한 줄이 절반 크기가 되는데, 실제로 WeightLabel이 그렇게 작아졌다.
        // 줄 수와 무관하게 글자 하나의 높이가 같아야 한다.
        float measured = text.GetPreferredValues("0층").y;
        float scale = measured > 0.0001f ? targetCapHeight / measured : 0.01f;

        t.localScale = Vector3.one * scale;
        rt.sizeDelta = new Vector2(width / scale, measured * 1.4f);
        text.overflowMode = TMPro.TextOverflowModes.Truncate;   // 넘치면 잘린다. 옆을 덮지 않는다.
        text.ForceMeshUpdate();
        EditorUtility.SetDirty(text);
        _log.AppendLine($"    {name}: 측정 {measured:F3} → 스케일 {scale:F4} (목표 {targetCapHeight:F3}m)");
    }

    /// <summary>
    /// 이름이 정해진 그레이박스 머티리얼을 만들거나 가져온다. 이미 있으면 값만 맞춘다.
    /// 프로젝트의 기존 머티리얼은 건드리지 않는다.
    /// </summary>
    private static Material EnsureMaterial(string name, float value, float emission)
    {
        string path = $"{MatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
            _log.AppendLine($"  머티리얼 생성 {name} (값 {value:F2})");
        }

        var c = new Color(value, value * 1.02f, value * 1.06f, 1f);   // 아주 옅은 청색 — 회색조에선 동일
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);

        // 발광은 조명 각도를 무시하고 일정한 밝기를 보장한다. 이것이 없으면 같은 물체가
        // 시점에 따라 다른 계층으로 읽혀 위계 자체가 무의미해진다.
        if (mat.HasProperty("_EmissionColor"))
        {
            if (emission > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", c * emission);
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                mat.SetColor("_EmissionColor", Color.black);
            }
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void Paint(string objectName, Material mat) => PaintTree(Find(objectName), mat);

    /// <summary>
    /// 이름이 아니라 트랜스폼으로 칠한다. 이름으로 찾으면 안 되는 이유는 인덱스가
    /// 이름당 하나만 들고 있기 때문이다 — 결과칸 심볼 27개가 전부 같은 이름이라
    /// 이름으로 칠했더니 3개만 칠해졌다.
    /// </summary>
    private static void PaintTree(Transform t, Material mat)
    {
        if (t == null || mat == null) return;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
        {
            if (r is TMPro.TMP_SubMesh || r is TMPro.TMP_SubMeshUI) continue;
            if (r.GetComponent<TMPro.TMP_Text>() != null) continue;
            r.sharedMaterial = mat;
            EditorUtility.SetDirty(r);
        }
    }

    private static void EnsureBoxCollider(GameObject go)
    {
        // 그레이박스는 MeshRenderer만으로 만들어져 콜라이더가 하나도 없었다.
        // 조준점 SphereCast가 아무것도 못 맞히므로 1인칭 상호작용이 성립하지 않는다.
        if (go.GetComponent<Renderer>() == null) return;
        if (go.GetComponent<Collider>() != null) return;
        go.AddComponent<BoxCollider>();
    }

    private static void SetActive(string name, bool active)
    {
        Transform t = Find(name);
        if (t == null) return;
        if (t.gameObject.activeSelf == active) return;
        Undo.RecordObject(t.gameObject, "Toggle");
        t.gameObject.SetActive(active);
    }

    private static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { _log.AppendLine($"  WARN  {target.GetType().Name}.{field} 없음"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── 배치 ──────────────────────────────────────────────────────────────

    private static void LayoutShell()
    {
        Place("Floor",   new Vector3(0f, -0.10f, 0f), new Vector3(3.6f, 0.20f, 3.4f));
        Place("Ceiling", new Vector3(0f,  2.60f, 0f), new Vector3(3.6f, 0.20f, 3.4f));
        Place("WallL",   new Vector3(-1.70f, 1.25f, 0f), new Vector3(0.20f, 2.50f, 3.4f));
        Place("WallR",   new Vector3( 1.70f, 1.25f, 0f), new Vector3(0.20f, 2.50f, 3.4f));

        // 뒷벽에 문 구멍(x 0.15~1.15, y 0~2.05)을 남긴다.
        Place("BackWall_Left",   new Vector3(-0.825f, 1.25f,  1.60f), new Vector3(1.95f, 2.50f, 0.20f));
        Place("BackWall_Right",  new Vector3( 1.475f, 1.25f,  1.60f), new Vector3(0.65f, 2.50f, 0.20f));
        Place("BackWall_Lintel", new Vector3( 0.650f, 2.275f, 1.60f), new Vector3(1.00f, 0.45f, 0.20f));

        Move("DoorLeftPivot",  new Vector3(0.15f, 0f, 1.55f));
        Move("DoorRightPivot", new Vector3(1.15f, 0f, 1.55f));
        Place("DoorLeft",  new Vector3(0.40f, 1.025f, 1.55f), new Vector3(0.50f, 2.05f, 0.08f));
        Place("DoorRight", new Vector3(0.90f, 1.025f, 1.55f), new Vector3(0.50f, 2.05f, 0.08f));
        Move("DoorSign",   new Vector3(0.65f, 2.28f, 1.44f));

        // 문 밖 짧은 복도. 노션은 문이 열리며 보이는 장면을 요구한다.
        Place("LobbyFloor", new Vector3(0.65f, -0.10f, 3.2f), new Vector3(3.0f, 0.20f, 3.0f));
        Place("LobbyBack",  new Vector3(0.65f,  1.25f, 4.6f), new Vector3(3.0f, 2.50f, 0.20f));

        // 방이 텅 비어서 "압박감 있는 승강기"가 아니라 "가구 몇 개 놓인 빈 방"으로 보였다.
        // 손잡이·조명·층 표시등은 승강기라면 당연히 있는 것들이고, 없으면 공간의 정체가
        // 읽히지 않는다. 접근을 막지 않는 위치에만 놓는다.
        GameObject rail = EnsurePrimitive("Handrail_R", PrimitiveType.Cube, Find("Car"));
        rail.transform.position = new Vector3(1.55f, 0.92f, 0.35f);
        rail.transform.localScale = new Vector3(0.06f, 0.06f, 1.90f);
        EnsureBoxCollider(rail);

        GameObject railBack = EnsurePrimitive("Handrail_B", PrimitiveType.Cube, Find("Car"));
        railBack.transform.position = new Vector3(-0.80f, 0.92f, 1.45f);
        railBack.transform.localScale = new Vector3(1.60f, 0.06f, 0.06f);
        EnsureBoxCollider(railBack);

        GameObject lamp = EnsurePrimitive("CeilingLamp", PrimitiveType.Cube, Find("Car"));
        lamp.transform.position = new Vector3(0f, 2.46f, 0f);
        lamp.transform.localScale = new Vector3(0.85f, 0.05f, 0.45f);
        EnsureBoxCollider(lamp);

        _log.AppendLine("  껍데기: 실내 3.2×3.0×2.5m, 바닥 윗면 y=0 (손잡이 2·천장등 1)");
    }

    private static void LayoutTubes()
    {
        // 세 통관을 왼쪽 벽에 나란히. 결과판이 눈높이 언저리(0.85~2.05m)에 오도록 한다.
        Move("TubesRoot", Vector3.zero);
        float[] z = { -0.50f, 0f, 0.50f };
        for (int i = 0; i < 3; i++)
        {
            Move($"Tube_{i}", new Vector3(-1.45f, 1.60f, z[i]));
            Transform frame = Find("TubeFrame");   // 이름이 같아 인덱스가 마지막 것만 잡는다
            _ = frame;
        }
        // TubeFrame·HarvestMarker·BallContainer는 이름이 셋 다 같으므로 부모를 통해 직접 순회한다.
        for (int i = 0; i < 3; i++)
        {
            Transform tube = Find($"Tube_{i}");
            if (tube == null) continue;
            foreach (Transform child in tube)
            {
                if (child.name == "TubeFrame")
                {
                    child.position = new Vector3(-1.45f, 1.60f, z[i]);
                    child.localScale = new Vector3(0.30f, 1.30f, 0.34f);
                    EnsureBoxCollider(child.gameObject);
                }
                else if (child.name == "HarvestMarker")
                {
                    // 타이밍 시절의 수확선. 자동 룰렛에는 조준할 선이 없다.
                    child.gameObject.SetActive(false);
                }
                else if (child.name == "BallContainer")
                {
                    child.position = new Vector3(-1.45f, 1.60f, z[i]);
                }
            }
        }
        _log.AppendLine("  통관 3개: 왼쪽 벽, 결과판 y 0.80~2.10m");
    }

    /// <summary>
    /// 통관마다 결과칸 3개를 만든다. 이것이 없으면 통관은 빈 판때기 세 장이고,
    /// 판정 엔진이 아무리 정확해도 화면에 게임이 존재하지 않는다.
    ///
    /// 칸 사이에 물리적 단차(칸막이)를 둔다. 선을 그리려면 텍스처나 머티리얼이 필요한데
    /// 그레이박스 단계에서 머티리얼을 건드리면 다른 오브젝트까지 번진다. 형태로 나눈다.
    /// </summary>
    private static void BuildBoardCells()
    {
        float[] tubeZ = { -0.50f, 0f, 0.50f };
        // 아래 칸이 콘솔 상판(1.00m)에 가려 안 보였다. 9칸 중 하나가 안 보이면 판독이 성립하지
        // 않으므로 전체를 0.15m 올린다. 천장이 2.5m라 위쪽에 여유가 있다.
        float[] rowY  = { 2.00f, 1.60f, 1.20f };   // r=0이 위. SpinBoard 규약과 같다.
        const float faceX = -1.34f;                // 통관 앞면(플레이어 쪽)보다 살짝 안쪽

        var cells = new Transform[SpinBoard_Cells];

        for (int c = 0; c < 3; c++)
        {
            Transform tube = Find($"Tube_{c}");
            if (tube == null) continue;

            // 칸막이 2개 — 세 칸으로 나뉜다는 것을 실루엣만으로 알 수 있게 한다.
            for (int d = 0; d < 2; d++)
            {
                float y = (rowY[d] + rowY[d + 1]) * 0.5f;
                GameObject div = EnsureChildPrimitive(tube, $"Divider_{d}", PrimitiveType.Cube);
                div.transform.position = new Vector3(-1.42f, y, tubeZ[c]);
                div.transform.localScale = new Vector3(0.20f, 0.035f, 0.38f);
                EnsureBoxCollider(div);
            }

            for (int r = 0; r < 3; r++)
            {
                GameObject cell = EnsurePlainChild(tube, $"Cell_{r}");
                cell.transform.position = new Vector3(faceX, rowY[r], tubeZ[c]);
                cells[c * 3 + r] = cell.transform;

                // 심볼 3종을 미리 만들어두고 하나만 켠다. 런타임에 프리미티브를 만들면
                // 첫 스핀에서 끊기고, 결정론 캡처에서도 프레임마다 결과가 달라진다.
                MakeSymbol(cell.transform, "Sym_NormalSoul",   PrimitiveType.Sphere,  0.17f);
                MakeSymbol(cell.transform, "Sym_Absorber",     PrimitiveType.Cube,    0.16f);
                MakeSymbol(cell.transform, "Sym_Proliferator", PrimitiveType.Capsule, 0.15f);
            }
        }

        Transform runT = Find("AscendRun");
        if (runT != null)
        {
            var view = runT.GetComponent<Ascend.Prototype.View.SpinBoardView>();
            if (view == null) view = runT.gameObject.AddComponent<Ascend.Prototype.View.SpinBoardView>();
            SetRef(view, "_run", runT.GetComponent<RunSessionBehaviour>());

            var so = new SerializedObject(view);
            SerializedProperty arr = so.FindProperty("_cells");
            arr.arraySize = SpinBoard_Cells;
            for (int i = 0; i < SpinBoard_Cells; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = cells[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        _log.AppendLine("  결과칸 9개 + 칸막이 6개 생성 — 심볼은 구/정육면체/캡슐로 형태 구분");
    }

    private const int SpinBoard_Cells = 9;

    private static void MakeSymbol(Transform cell, string name, PrimitiveType type, float size)
    {
        Transform existing = cell.Find(name);
        GameObject go;
        if (existing != null) go = existing.gameObject;
        else
        {
            go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(cell, false);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            // 심볼은 조준 대상이 아니다. 콜라이더를 두면 뒤의 통관·벽 조준을 방해한다.
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
        }
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one * size;
        go.SetActive(false);   // SpinBoardView가 켠다
    }

    private static GameObject EnsurePlainChild(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null) return t.gameObject;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go;
    }

    private static GameObject EnsureChildPrimitive(Transform parent, string name, PrimitiveType type)
    {
        Transform t = parent.Find(name);
        if (t != null) return t.gameObject;
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, true);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go;
    }

    private static void LayoutConsole()
    {
        // 통관 앞의 조작대. 상판 1.0m — 서서 손이 닿는 높이다.
        // 상판이 여전히 결과판 아래 줄을 가로질러 가렸다. 서 있는 위치에 따라 9칸 중
        // 3칸이 잘리는데, 이는 "핵심 결과가 특정 위치에서만 보인다"는 금지 항목에 걸린다.
        // 상판을 0.88m로 낮추고 폭을 줄여 시야에서 물러나게 한다.
        Place("ConsoleSlab", new Vector3(-1.02f, 0.83f, -0.20f), new Vector3(0.34f, 0.09f, 1.60f));

        // 레버는 상판 위로 충분히 솟아야 실루엣이 어두운 벽이 아니라 빈 공간을 등진다.
        GameObject lever = EnsurePrimitive("ExecutionLever", PrimitiveType.Cube, Find("Console"));
        lever.transform.position = new Vector3(-1.02f, 1.16f, -0.88f);
        lever.transform.localScale = new Vector3(0.11f, 0.52f, 0.11f);
        lever.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
        EnsureBoxCollider(lever);
        if (lever.GetComponent<InteractableLever>() == null) lever.AddComponent<InteractableLever>();

        // 손잡이. 지금 레버는 상판에 꽂힌 판자 하나라 "당기는 것"으로 안 읽힌다.
        // 레버의 자식으로 두면 부모 스케일(0.10 × 0.42 × 0.10)에 눌려 찌그러지므로
        // 콘솔에 붙이고 레버 끝 위치만 따라가게 한다.
        GameObject knob = EnsurePrimitive("LeverKnob", PrimitiveType.Sphere, Find("Console"));
        knob.transform.position = lever.transform.position + lever.transform.up * 0.24f;
        knob.transform.localScale = Vector3.one * 0.11f;
        Collider knobCol = knob.GetComponent<Collider>();
        if (knobCol != null) Object.DestroyImmediate(knobCol);   // 레버 콜라이더가 대신 받는다

        _log.AppendLine("  콘솔 상판 1.00m / 레버 1.18m — 서서 닿는 높이");
    }

    private static void LayoutPanel()
    {
        Place("PanelBack",  new Vector3(-0.80f, PanelMid,  1.45f), new Vector3(1.70f, 0.55f, 0.06f));
        // 게이지는 라벨 아래 한 줄로 내린다. 라벨과 같은 높이에 두면 둘이 서로를 가린다.
        Place("PowerBarBg", new Vector3(-0.72f, 1.30f, 1.40f), new Vector3(1.72f, 0.10f, 0.04f));
        Move("PowerBarPivot", new Vector3(-1.58f, 1.30f, 1.38f));
        Place("PowerBarFill", new Vector3(-1.58f, 1.30f, 1.38f), new Vector3(0.02f, 0.07f, 0.03f));
        // 라벨은 위치만 옮기면 안 된다. 폭을 모른 채 x만 벌려놓으면 글자가 길어지는 순간
        // 옆 라벨을 덮는다. 실제로 "10 층" 위에 "전력 0 / 0"이 그대로 올라타 있었다.
        // 폭·정렬·크기를 못 박아 겹칠 수 없게 만든다. 왼쪽 정렬로 시작점을 고정한다.
        LabelAt("FloorLabel",  new Vector3(-1.58f, 1.74f, 1.38f), 0.52f);
        LabelAt("PowerLabel",  new Vector3(-1.58f, 1.58f, 1.38f), 0.90f);
        LabelAt("WeightLabel", new Vector3(-1.58f, 1.42f, 1.38f), 0.90f);
        Place("OverloadLight", new Vector3(0.02f, 1.55f, 1.42f), new Vector3(0.10f, 0.10f, 0.10f));

        // 과부하 램프가 전력 게이지 바로 옆에 붙어 있어서 "진행 바 + 정지 버튼"으로 읽혔다.
        // 폐기한 타이밍 축의 잔재로 오해받는 배치다. 게이지에서 떼어 위로 올리고
        // 하우징에 앉혀 램프임을 형태로 알린다.
        //
        // 높이는 `TenFloorSceneBuilder`가 다시 잡는다 — 이 메뉴는 비례 재조정 **이전**의
        // 좌표계로 쓰여 있어서, 재조정된 씬에 다시 돌리면 값이 어긋난다.
        Place("OverloadLight", new Vector3(-0.10f, 1.82f, 1.42f), new Vector3(0.09f, 0.09f, 0.09f));
        GameObject housing = EnsurePrimitive("OverloadHousing", PrimitiveType.Cube, Find("InstrumentPanel"));
        housing.transform.position = new Vector3(-0.10f, 1.82f, 1.46f);
        housing.transform.localScale = new Vector3(0.16f, 0.16f, 0.05f);
        EnsureBoxCollider(housing);

        // 계약 패널과 전력 탱크는 원래 없던 물건이다. 노션이 요구하는 상호작용 대상이므로 만든다.
        GameObject contract = EnsurePrimitive("ContractPanel", PrimitiveType.Cube, Find("InstrumentPanel"));
        contract.transform.position = new Vector3(1.58f, 1.50f, 0.30f);
        contract.transform.localScale = new Vector3(0.05f, 0.62f, 0.94f);
        EnsureBoxCollider(contract);
        if (contract.GetComponent<InteractableContractPanel>() == null)
            contract.AddComponent<InteractableContractPanel>();

        // 판에 홈 3줄을 파는 방식은 실패했다 — 환기구·라디에이터와 형태가 같아진다.
        // 대신 서로 떨어진 명패 3장을 벽에서 띄워 붙인다. 간격이 있는 별개 물체 셋은
        // 마감재가 아니라 "고르는 것"으로 읽힌다. 계약은 최대 3개다(없음·흡수체·증식체).
        for (int i = 0; i < 3; i++)
        {
            GameObject plaque = EnsurePrimitive($"ContractPlaque_{i}", PrimitiveType.Cube, contract.transform.parent);
            plaque.transform.position = new Vector3(1.50f, 1.72f - i * 0.22f, 0.30f);
            plaque.transform.localScale = new Vector3(0.05f, 0.15f, 0.66f);
            Collider pc = plaque.GetComponent<Collider>();
            if (pc != null) Object.DestroyImmediate(pc);   // 패널 본체가 조준을 받는다
        }
        // 이전 시도의 홈은 지운다. 남겨두면 명패 뒤에서 여전히 환기구로 읽힌다.
        for (int i = 0; i < 3; i++)
        {
            Transform old = Find($"ContractSlot_{i}");
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }

        // 탱크를 시선 높이 전경으로 올린 것은 과했다. 결과판보다 크고 밝아 시선을 먼저
        // 가져갔고, 결과판과 계약 패널이 한 화면에 들어오는 유일한 시점에서 계약 패널을
        // 가렸다. 소품이 핵심 판독물을 이기면 안 된다.
        //
        // 뒷벽 오른쪽 구석으로 밀고 크게 줄인다. 상호작용은 계속 가능하되 화면을
        // 지배하지 않는 크기다. 눈금은 남긴다 — "임계점이 있는 용기"라는 정보는 유효하다.
        GameObject tank = EnsurePrimitive("PowerTank", PrimitiveType.Cylinder, Find("Car"));
        tank.transform.position = new Vector3(1.34f, 1.02f, 1.18f);
        tank.transform.localScale = new Vector3(0.22f, 0.34f, 0.22f);
        EnsureBoxCollider(tank);
        if (tank.GetComponent<InteractablePowerTank>() == null)
            tank.AddComponent<InteractablePowerTank>();

        for (int i = 0; i < 4; i++)
        {
            GameObject tick = EnsurePrimitive($"TankTick_{i}", PrimitiveType.Cylinder, Find("Car"));
            tick.transform.position = new Vector3(1.34f, 0.78f + i * 0.16f, 1.18f);
            tick.transform.localScale = new Vector3(0.25f, 0.008f, 0.25f);
            Collider tc = tick.GetComponent<Collider>();
            if (tc != null) Object.DestroyImmediate(tc);   // 탱크 본체가 조준을 받는다
        }
        GameObject stand = EnsurePrimitive("TankStand", PrimitiveType.Cylinder, Find("Car"));
        stand.transform.position = new Vector3(1.34f, 0.34f, 1.18f);
        stand.transform.localScale = new Vector3(0.16f, 0.34f, 0.16f);
        EnsureBoxCollider(stand);

        _log.AppendLine($"  계기판 {PanelMid:F2}m / 계약 패널 1.50m(슬롯 3) / 전력 탱크 1.05m(눈금 4)");
    }

    private static GameObject EnsurePrimitive(string name, PrimitiveType type, Transform parent)
    {
        if (_index.TryGetValue(name, out Transform existing) && existing != null)
            return existing.gameObject;

        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        if (parent != null) go.transform.SetParent(parent, true);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        _index[name] = go.transform;
        _log.AppendLine($"  {name} 생성");
        return go;
    }

    private static void HideDeprecated()
    {
        // 노션이 명시적으로 버린 타이밍 조작부. 화면에 남아 있으면 누르라는 신호가 된다.
        int n = 0;
        foreach (var kv in new List<KeyValuePair<string, Transform>>(_index))
        {
            string name = kv.Key;
            if (name.StartsWith("StopButton") || name.StartsWith("ButtonPivot")
                || name.StartsWith("ButtonLabel") || name.StartsWith("TubeReadout"))
            {
                if (kv.Value != null && kv.Value.gameObject.activeSelf)
                { kv.Value.gameObject.SetActive(false); n++; }
            }
        }
        // 옛 uGUI HUD. GameHudView(UGUI)가 대신한다. 참조가 비활성 GameSystems를 가리켜
        // 켜두면 매 프레임 null을 만진다.
        SetActive("PrototypeUI", false);
        SetActive("Canvas", false);
        _log.AppendLine($"  폐기 오브젝트 숨김 {n}개 + 옛 HUD 2개");
    }

    private static GameObject BuildPlayerRig()
    {
        if (!_index.TryGetValue("Player", out Transform pt) || pt == null)
        {
            var go = new GameObject("Player");
            Undo.RegisterCreatedObjectUndo(go, "Create Player");
            pt = go.transform;
            _index["Player"] = pt;
            _log.AppendLine("  Player 리그 생성");
        }
        GameObject player = pt.gameObject;

        // 스폰 시선에 조작할 것이 하나도 없고 문만 크게 보였다. 시작하자마자 출구를
        // 바라보는 배치는 "여기서 무엇을 하는가"를 잘못 알려준다. 통관과 계기판이
        // 함께 들어오도록 더 왼쪽으로 돌린다(통관 -67°, 계기판 -30° 사이).
        player.transform.position = new Vector3(0.55f, 0f, -0.85f);
        player.transform.rotation = Quaternion.Euler(0f, -50f, 0f);

        var cc = player.GetComponent<CharacterController>();
        if (cc == null) cc = player.AddComponent<CharacterController>();
        cc.height = 1.75f;
        cc.radius = 0.28f;
        cc.center = new Vector3(0f, 0.875f, 0f);

        Transform head = player.transform.Find("Head");
        if (head == null)
        {
            var h = new GameObject("Head");
            h.transform.SetParent(player.transform, false);
            head = h.transform;
        }
        head.localPosition = new Vector3(0f, EyeHeight, 0f);
        head.localRotation = Quaternion.identity;

        // 기존 Main Camera를 그대로 머리에 붙인다. 카메라 설정(클리어 플래그·포스트 처리)을
        // 새로 만들면 렌더링이 달라져 비교가 어긋난다.
        Transform cam = Find("Main Camera");
        if (cam != null)
        {
            cam.SetParent(head, false);
            cam.localPosition = Vector3.zero;
            cam.localRotation = Quaternion.identity;
        }

        var fpc = player.GetComponent<FirstPersonController>();
        if (fpc == null) fpc = player.AddComponent<FirstPersonController>();
        SetRef(fpc, "_characterController", cc);
        if (cam != null) SetRef(fpc, "_viewCamera", cam.GetComponent<Camera>());

        var interactor = player.GetComponent<CrosshairInteractor>();
        if (interactor == null) interactor = player.AddComponent<CrosshairInteractor>();
        if (cam != null) SetRef(interactor, "_viewCamera", cam.GetComponent<Camera>());

        _log.AppendLine($"  Player @({player.transform.position.x:F2}, 0, {player.transform.position.z:F2}) 눈높이 {EyeHeight:F2}m");
        return player;
    }

    /// <summary>
    /// 조준점과 프롬프트를 만든다. 없으면 1인칭에서 무엇을 겨냥 중인지 알 수 없고,
    /// 노션이 요구한 "조준점이 대상에 닿으면 짧은 설명이 나타난다"가 성립하지 않는다.
    /// </summary>
    private static void BuildCrosshairUI(GameObject player)
    {
        GameObject canvasGo;
        if (_index.TryGetValue("PlayerHUD", out Transform existing) && existing != null)
        {
            canvasGo = existing.gameObject;
        }
        else
        {
            canvasGo = new GameObject("PlayerHUD",
                typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create PlayerHUD");
            _index["PlayerHUD"] = canvasGo.transform;
            _log.AppendLine("  PlayerHUD 캔버스 생성");
        }

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // 점 하나. 노션이 정밀 조준을 난이도로 쓰지 말라고 했으므로 조준점은 작게 두되
        // 판정 부피(SphereCast 0.18m)가 훨씬 넓다 — 보이는 것보다 관대하게 잡힌다.
        GameObject dot = EnsureChild(canvasGo.transform, "CrosshairDot");
        var dotImage = dot.GetComponent<UnityEngine.UI.Image>();
        if (dotImage == null) dotImage = dot.AddComponent<UnityEngine.UI.Image>();
        var dotRect = dot.GetComponent<RectTransform>();
        dotRect.anchorMin = dotRect.anchorMax = new Vector2(0.5f, 0.5f);
        dotRect.pivot = new Vector2(0.5f, 0.5f);
        dotRect.anchoredPosition = Vector2.zero;
        dotRect.sizeDelta = new Vector2(8f, 8f);

        GameObject promptGo = EnsureChild(canvasGo.transform, "PromptText");
        var prompt = promptGo.GetComponent<TMPro.TextMeshProUGUI>();
        if (prompt == null) prompt = promptGo.AddComponent<TMPro.TextMeshProUGUI>();
        var promptRect = promptGo.GetComponent<RectTransform>();
        promptRect.anchorMin = promptRect.anchorMax = new Vector2(0.5f, 0.5f);
        promptRect.pivot = new Vector2(0.5f, 1f);
        promptRect.anchoredPosition = new Vector2(0f, -28f);   // 조준점 바로 아래
        promptRect.sizeDelta = new Vector2(600f, 44f);
        prompt.alignment = TMPro.TextAlignmentOptions.Top;
        prompt.fontSize = 26f;
        prompt.text = string.Empty;

        // 한글 프롬프트가 두부로 나오지 않게 한글 폰트를 명시한다.
        var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(
            "Assets/Prototype_Elevator/Fonts/NanumGothic SDF.asset");
        if (font != null) prompt.font = font;
        else _log.AppendLine("  WARN  NanumGothic SDF 없음 — 프롬프트가 깨질 수 있다");

        var view = canvasGo.GetComponent<CrosshairView>();
        if (view == null) view = canvasGo.AddComponent<CrosshairView>();
        SetRef(view, "_crosshairGraphic", dotImage);
        SetRef(view, "_promptText", prompt);

        var interactor = player.GetComponent<CrosshairInteractor>();
        if (interactor != null) SetRef(interactor, "_view", view);

        _log.AppendLine("  조준점·프롬프트 연결");
    }

    private static GameObject EnsureChild(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null) return t.gameObject;
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        _index[name] = go.transform;
        return go;
    }

    private static void WireInteractables(GameObject player)
    {
        Transform runT = Find("AscendRun");
        if (runT == null) { _log.AppendLine("  WARN  AscendRun 없음 — 브리지 연결 생략"); return; }

        var run = runT.GetComponent<RunSessionBehaviour>();
        var bridge = runT.GetComponent<RouletteInteractionBridge>();
        if (bridge == null) bridge = runT.gameObject.AddComponent<RouletteInteractionBridge>();

        SetRef(bridge, "_run", run);
        SetRef(bridge, "_lever", Find("ExecutionLever")?.GetComponent<InteractableLever>());
        SetRef(bridge, "_contractPanel", Find("ContractPanel")?.GetComponent<InteractableContractPanel>());
        SetRef(bridge, "_powerTank", Find("PowerTank")?.GetComponent<InteractablePowerTank>());

        _log.AppendLine("  상호작용 브리지 연결: 레버 / 계약 패널 / 전력 탱크");
    }

    // ── 검증 ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 명도 위계를 실제 오브젝트에 배정한다. 이 단계가 배치 맨 끝인 이유는,
    /// 앞에서 만들어진 오브젝트가 전부 존재해야 한 번에 칠할 수 있기 때문이다.
    /// </summary>
    private static void ApplyValueHierarchy()
    {
        Material readout     = EnsureMaterial("M_Gray_Readout",     ReadoutValue,     ReadoutEmission);
        Material interactive = EnsureMaterial("M_Gray_Interactive", InteractiveValue, InteractiveEmission);
        Material recessive   = EnsureMaterial("M_Gray_Recessive",   RecessiveValue,   0f);

        // 결과판 — 가장 밝다. 이 게임이 보여줘야 할 것이 여기다.
        // 칸막이는 어둡게 눌러 칸 경계가 홈처럼 읽히게 한다.
        int painted = 0;
        for (int c = 0; c < 3; c++)
        {
            Transform tube = Find($"Tube_{c}");
            if (tube == null) continue;
            foreach (Transform child in tube.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.StartsWith("Sym_")) { PaintTree(child, readout); painted++; }
                else if (child.name.StartsWith("Divider_")) PaintTree(child, recessive);
            }
        }
        _log.AppendLine($"    심볼 {painted}개 칠함 (9칸 × 3종 = 27이어야 한다)");

        // 조작 대상 — 중간 값. 배경보다 밝고 결과판보다 어둡다.
        Paint("ExecutionLever", interactive);
        Paint("LeverKnob", interactive);
        Paint("ContractPanel", interactive);
        Paint("PowerTank", interactive);
        Paint("TankStand", interactive);
        for (int i = 0; i < 3; i++) Paint($"ContractPlaque_{i}", interactive);
        for (int i = 0; i < 4; i++) Paint($"TankTick_{i}", recessive);   // 눈금은 탱크보다 어둡게 — 홈처럼 보인다

        // 구조물 — 어둡게 눌러 시선을 뺏지 않게 한다. 문이 화면에서 가장 밝고 큰 면이라
        // 어느 프레임에서든 시선이 먼저 출구로 갔다. 출구는 목적지가 아니다.
        // 문만 눌렀더니 문이 붙은 벽 전체가 방에서 가장 밝은 면으로 남았다. 방의 밝은
        // 절반이 통째로 출구 쪽이면 시선은 계속 그리로 간다. 벽까지 함께 누른다.
        Paint("DoorLeft", recessive);
        Paint("DoorRight", recessive);
        Paint("BackWall_Left", recessive);
        Paint("BackWall_Right", recessive);
        Paint("BackWall_Lintel", recessive);
        Paint("WallR", recessive);
        Paint("Ceiling", recessive);
        Paint("Handrail_R", recessive);
        Paint("Handrail_B", recessive);
        Paint("OverloadHousing", recessive);

        // 출구 간판이 월드에서 가장 큰 글자였다. 읽히되 지배하지 않을 크기로 줄인다.
        Transform sign = Find("DoorSign");
        if (sign != null)
        {
            var st = sign.GetComponent<TMPro.TMP_Text>();
            if (st != null)
            {
                sign.localScale = sign.localScale * 0.55f;
                EditorUtility.SetDirty(st);
            }
        }

        AssetDatabase.SaveAssets();
        _log.AppendLine($"  명도 위계 배정: 결과판 {ReadoutValue:F2} / 조작 {InteractiveValue:F2} / 구조 {RecessiveValue:F2}");
    }

    private static void Verify()
    {
        _log.AppendLine();
        int problems = 0;

        problems += Expect(Find("Player") != null, "Player 리그 존재");
        problems += Expect(Find("ExecutionLever") != null, "레버 존재");
        problems += Expect(Find("ContractPanel") != null, "계약 패널 존재");
        problems += Expect(Find("PowerTank") != null, "전력 탱크 존재");

        // 시각 기준 B-1-2: 핵심 장치가 시선 높이에 있어야 한다.
        problems += ExpectHeight("PanelBack", 1.0f, 2.1f);
        problems += ExpectHeight("ContractPanel", 1.0f, 2.1f);
        problems += ExpectHeight("ExecutionLever", 0.8f, 1.5f);

        // 콜라이더가 없으면 조준점이 아무것도 못 맞힌다.
        // 글자(TextMeshPro)는 제외한다. 읽는 것이지 누르는 것이 아니고, 콜라이더를 붙이면
        // 오히려 뒤에 있는 진짜 대상을 가려서 조준을 방해한다.
        var missing = new List<string>();
        foreach (var kv in _index)
        {
            Transform t = kv.Value;
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            Renderer r = t.GetComponent<Renderer>();
            if (r == null || t.GetComponent<Collider>() != null) continue;
            if (r is TMPro.TMP_SubMesh || r is TMPro.TMP_SubMeshUI) continue;
            if (t.GetComponent<TMPro.TMP_Text>() != null) continue;
            // 장식은 일부러 콜라이더가 없다. 붙이면 조준을 가로채서 진짜 대상을 못 누르게 된다.
            // 결과칸 심볼은 읽는 것이고, 레버 손잡이는 레버 본체 콜라이더가 대신 받는다.
            if (t.name.StartsWith("Sym_") || t.name.StartsWith("ContractPlaque_")
                || t.name.StartsWith("TankTick_") || t.name == "LeverKnob") continue;
            missing.Add(t.name);
        }
        problems += Expect(missing.Count == 0,
            $"보이는 메시에 콜라이더 있음{(missing.Count == 0 ? "" : " — 없는 것: " + string.Join(", ", missing))}");

        // 눈높이만 맞고 손이 안 닿으면 소용없다. CrosshairInteractor의 기본 사거리는 5m지만,
        // 노션이 "반복 스핀마다 장치 사이를 길게 왕복하게 하지 마라"고 했으므로 더 좁게 본다.
        Transform head = Find("Player")?.Find("Head");
        if (head != null)
        {
            problems += ExpectReach(head.position, "ExecutionLever", 2.5f);
            problems += ExpectReach(head.position, "ContractPanel", 2.5f);
            problems += ExpectReach(head.position, "PowerTank", 2.5f);
            problems += ExpectReach(head.position, "PanelBack", 3.5f);   // 읽기만 하므로 조금 멀어도 된다
        }
        else { _log.AppendLine("  FAIL  Head 없음 — 도달 거리 확인 불가"); problems++; }

        // 거리가 가까워도 벽이나 콘솔에 가리면 조준이 안 된다. 실제로 CrosshairInteractor와
        // 같은 SphereCast를 쏴서 첫 히트가 목표인지 본다. "붙였다"와 "잡힌다"는 다르다.
        if (head != null)
        {
            problems += ExpectAimable(head.position, "ExecutionLever");
            problems += ExpectAimable(head.position, "ContractPanel");
            problems += ExpectAimable(head.position, "PowerTank");
        }

        // 조준점 UI가 없으면 무엇을 겨냥 중인지 알 수 없다.
        Transform hud = Find("PlayerHUD");
        var view = hud != null ? hud.GetComponent<CrosshairView>() : null;
        problems += Expect(view != null && view.HasCrosshairGraphic && view.HasPromptText,
            "조준점·프롬프트 참조 연결됨");

        _log.AppendLine();
        _log.AppendLine(problems == 0 ? "  결과: OK" : $"  결과: 문제 {problems}건");
    }

    private static int Expect(bool ok, string name)
    {
        _log.AppendLine(ok ? $"  PASS  {name}" : $"  FAIL  {name}");
        return ok ? 0 : 1;
    }

    /// <summary>
    /// CrosshairInteractor와 같은 조건(반경 0.18m, 사거리 5m)으로 대상을 향해 쏜다.
    /// 첫 히트가 대상이 아니면 무언가에 가려 있다는 뜻이고, 플레이어는 그것을 누를 수 없다.
    /// </summary>
    private static int ExpectAimable(Vector3 eye, string name)
    {
        Transform t = Find(name);
        Collider c = t != null ? t.GetComponent<Collider>() : null;
        if (c == null) { _log.AppendLine($"  FAIL  {name} 조준 확인 불가 (콜라이더 없음)"); return 1; }

        Vector3 target = c.bounds.center;
        Vector3 dir = (target - eye).normalized;

        if (!Physics.SphereCast(eye, 0.18f, dir, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Collide))
        {
            _log.AppendLine($"  FAIL  {name} 조준 — 아무것도 맞지 않음");
            return 1;
        }

        bool ok = hit.collider == c || hit.collider.transform.IsChildOf(t) || t.IsChildOf(hit.collider.transform);
        _log.AppendLine(ok
            ? $"  PASS  {name} 조준 가능 ({hit.distance:F2}m)"
            : $"  FAIL  {name} 조준 — '{hit.collider.name}'에 가림");
        return ok ? 0 : 1;
    }

    private static int ExpectReach(Vector3 from, string name, float maxDistance)
    {
        Transform t = Find(name);
        if (t == null) { _log.AppendLine($"  FAIL  {name} 도달 거리 확인 불가"); return 1; }

        // 표면까지의 거리로 잰다. 중심까지 재면 큰 물체가 실제보다 멀어 보인다.
        Collider c = t.GetComponent<Collider>();
        float d = c != null
            ? Vector3.Distance(from, c.ClosestPoint(from))
            : Vector3.Distance(from, t.position);

        bool ok = d <= maxDistance;
        _log.AppendLine(ok
            ? $"  PASS  {name} 도달 {d:F2}m (상한 {maxDistance:F1})"
            : $"  FAIL  {name} 도달 {d:F2}m — 상한 {maxDistance:F1} 초과, 왕복이 노동이 된다");
        return ok ? 0 : 1;
    }

    private static int ExpectHeight(string name, float lo, float hi)
    {
        Transform t = Find(name);
        if (t == null) { _log.AppendLine($"  FAIL  {name} 높이 확인 불가"); return 1; }
        float y = t.position.y;
        bool ok = y >= lo && y <= hi;
        _log.AppendLine(ok
            ? $"  PASS  {name} 높이 {y:F2}m (시선 범위 {lo:F1}~{hi:F1})"
            : $"  FAIL  {name} 높이 {y:F2}m — 시선 범위 {lo:F1}~{hi:F1} 밖");
        return ok ? 0 : 1;
    }
}
