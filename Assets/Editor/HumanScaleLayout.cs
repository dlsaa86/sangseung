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

    [MenuItem("Ascend/Layout — 사람 기준 재배치")]
    public static void Run()
    {
        _log = new StringBuilder();
        _log.AppendLine("[상승] === 사람 기준 재배치 ===");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        BuildIndex(scene);

        LayoutShell();
        LayoutTubes();
        LayoutConsole();
        LayoutPanel();
        HideDeprecated();
        GameObject player = BuildPlayerRig();
        BuildCrosshairUI(player);
        WireInteractables(player);

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

        _log.AppendLine("  껍데기: 실내 3.2×3.0×2.5m, 바닥 윗면 y=0");
    }

    private static void LayoutTubes()
    {
        // 세 통관을 왼쪽 벽에 나란히. 결과판이 눈높이 언저리(0.85~2.05m)에 오도록 한다.
        Move("TubesRoot", Vector3.zero);
        float[] z = { -0.50f, 0f, 0.50f };
        for (int i = 0; i < 3; i++)
        {
            Move($"Tube_{i}", new Vector3(-1.45f, 1.45f, z[i]));
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
                    child.position = new Vector3(-1.45f, 1.45f, z[i]);
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
                    child.position = new Vector3(-1.45f, 1.45f, z[i]);
                }
            }
        }
        _log.AppendLine("  통관 3개: 왼쪽 벽, 결과판 y 0.80~2.10m");
    }

    private static void LayoutConsole()
    {
        // 통관 앞의 조작대. 상판 1.0m — 서서 손이 닿는 높이다.
        Place("ConsoleSlab", new Vector3(-1.05f, 0.95f, 0f), new Vector3(0.42f, 0.10f, 1.60f));

        GameObject lever = EnsurePrimitive("ExecutionLever", PrimitiveType.Cube, Find("Console"));
        lever.transform.position = new Vector3(-1.05f, 1.18f, -0.55f);
        lever.transform.localScale = new Vector3(0.09f, 0.38f, 0.09f);
        lever.transform.rotation = Quaternion.Euler(14f, 0f, 0f);
        EnsureBoxCollider(lever);
        if (lever.GetComponent<InteractableLever>() == null) lever.AddComponent<InteractableLever>();

        _log.AppendLine("  콘솔 상판 1.00m / 레버 1.18m — 서서 닿는 높이");
    }

    private static void LayoutPanel()
    {
        Place("PanelBack",  new Vector3(-0.80f, PanelMid,  1.45f), new Vector3(1.70f, 0.55f, 0.06f));
        Place("PowerBarBg", new Vector3(-0.80f, 1.48f,     1.40f), new Vector3(1.40f, 0.14f, 0.04f));
        Move("PowerBarPivot", new Vector3(-1.50f, 1.48f, 1.38f));
        Place("PowerBarFill", new Vector3(-1.50f, 1.48f, 1.38f), new Vector3(0.02f, 0.10f, 0.03f));
        Move("FloorLabel",  new Vector3(-1.50f, 1.76f, 1.38f));
        Move("PowerLabel",  new Vector3(-0.80f, 1.70f, 1.38f));
        Move("WeightLabel", new Vector3(-0.15f, 1.76f, 1.38f));
        Place("OverloadLight", new Vector3(0.02f, 1.55f, 1.42f), new Vector3(0.10f, 0.10f, 0.10f));

        // 계약 패널과 전력 탱크는 원래 없던 물건이다. 노션이 요구하는 상호작용 대상이므로 만든다.
        GameObject contract = EnsurePrimitive("ContractPanel", PrimitiveType.Cube, Find("InstrumentPanel"));
        contract.transform.position = new Vector3(1.56f, 1.50f, 0.30f);
        contract.transform.localScale = new Vector3(0.06f, 0.55f, 0.90f);
        EnsureBoxCollider(contract);
        if (contract.GetComponent<InteractableContractPanel>() == null)
            contract.AddComponent<InteractableContractPanel>();

        GameObject tank = EnsurePrimitive("PowerTank", PrimitiveType.Cylinder, Find("Car"));
        tank.transform.position = new Vector3(1.30f, 0.62f, -0.85f);
        tank.transform.localScale = new Vector3(0.42f, 0.62f, 0.42f);
        EnsureBoxCollider(tank);
        if (tank.GetComponent<InteractablePowerTank>() == null)
            tank.AddComponent<InteractablePowerTank>();

        _log.AppendLine($"  계기판 중심 {PanelMid:F2}m / 계약 패널 1.50m / 전력 탱크 0.62m");
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
        // 옛 uGUI HUD. RouletteHud(IMGUI)가 대신한다. 참조가 비활성 GameSystems를 가리켜
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

        // 문을 등지고 통관·계기판을 함께 볼 수 있는 자리.
        player.transform.position = new Vector3(0.55f, 0f, -0.85f);
        player.transform.rotation = Quaternion.Euler(0f, -32f, 0f);

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
