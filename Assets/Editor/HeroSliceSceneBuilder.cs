using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Ascend.Prototype.Player;
using Ascend.Prototype.Risk;
using Ascend.Prototype.Run;
using Ascend.Prototype.UI;
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

            // 실내등을 먼저 세운다 — 과수확 해제 연출이 "실내등을 어둡게" 하려면 참조가 필요하다.
            EnsureRiskStateView(car.transform);

            GameObject overharvest = BuildOverharvestLever(car.transform);
            EnsurePresenter();
            WireBridge(overharvest);
            DisableDeadGrayboxView();
            BuildPowerGauge();
            EnsureInstrumentPanelView();
            WidenPanelLabels();
            BuildScreenUi();

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
            // 실행 레버는 축 0.11×0.52. 여기는 하우징만 0.60×0.84다.
            //
            // **깊이 배치가 이 물체의 전부다.** 잠김이 잠김으로 보이려면 덮개가 손잡이보다
            // 확실히 앞에 있어야 한다. 두 번 틀렸다:
            //   1차 — 덮개를 손잡이보다 0.22m 뒤에 둬서 잠겨도 손잡이가 그대로 보였다.
            //   2차 — 앞으로 옮겼지만 0.022m 만 앞이라 손잡이가 덮개를 **관통**했다.
            // 지금은 하우징을 오목한 함으로 만들고 손잡이를 그 안에 넣는다.
            //
            //   z 축(앞이 −):  덮개 앞면 −0.435 │ 덮개 뒷면 −0.405 │ 손잡이 앞면 −0.367 │ 하우징 안쪽
            //   손잡이 앞면이 덮개 뒷면보다 0.038m 뒤 — 닫히면 실제로 가려진다.
            GameObject housing = Box(root.transform, "Housing", new Vector3(0f, 0f, -0.15f),
                new Vector3(0.60f, 0.84f, 0.52f), recessive);
            BoxCollider housingCollider = housing.AddComponent<BoxCollider>();

            // 경고 띠는 덮개 **바깥쪽**(x=±0.26)에 세로로 세운다. 덮개 폭이 0.44라
            // 그 안쪽에 두면 닫혔을 때 띠까지 같이 가려져 잠김/해제 구분이 사라진다.
            // 색 하나로 위험을 말하지 않는다(VISUAL_SPEC §7) — 밝기와 발광이 함께 뒤집힌다.
            GameObject stripeLower = Box(root.transform, "WarningStripe",
                new Vector3(-0.26f, 0f, -0.42f), new Vector3(0.06f, 0.76f, 0.02f), button);
            GameObject stripeUpper = Box(root.transform, "WarningStripe_Upper",
                new Vector3(0.26f, 0f, -0.42f), new Vector3(0.06f, 0.76f, 0.02f), button);

            // 손잡이 — 하우징 안쪽. 덮개가 닫히면 가려진다.
            var handlePivot = new GameObject("HandlePivot");
            handlePivot.transform.SetParent(root.transform, false);
            handlePivot.transform.localPosition = new Vector3(0f, -0.12f, -0.05f);
            Box(handlePivot.transform, "HandleShaft", new Vector3(0f, 0.26f, 0f),
                new Vector3(0.09f, 0.52f, 0.09f), interactive);
            GameObject grip = Box(handlePivot.transform, "HandleGrip", new Vector3(0f, 0.54f, -0.05f),
                new Vector3(0.24f, 0.13f, 0.17f), interactive);
            BoxCollider gripCollider = grip.AddComponent<BoxCollider>();

            // 보호 덮개 — 잠금의 물리적 표현. 하우징 앞면을 막는다.
            var coverPivot = new GameObject("CoverPivot");
            coverPivot.transform.SetParent(root.transform, false);
            coverPivot.transform.localPosition = new Vector3(0f, 0.46f, -0.42f);
            Box(coverPivot.transform, "CoverPlate", new Vector3(0f, -0.28f, 0f),
                new Vector3(0.44f, 0.56f, 0.03f), panel);
            Box(coverPivot.transform, "CoverRib", new Vector3(0f, -0.28f, -0.02f),
                new Vector3(0.07f, 0.56f, 0.02f), recessive);

            // 잠금등은 덮개 아래(y=-0.30)에 둔다. 덮개는 y -0.10~0.46 을 차지하므로
            // 그 안에 두면 닫혔을 때 등까지 가려진다.
            GameObject lockLight = Box(root.transform, "LockLight",
                new Vector3(0f, -0.30f, -0.42f), new Vector3(0.18f, 0.07f, 0.02f), light);

            // 해제 순간을 비추는 전용 등. 실내등만으로는 "이 레버가 지금 살아났다"가
            // 공간에서 읽히지 않는다(VISUAL_SPEC §7 "해제 순간 조명·소리·기계 반응이 집중된다").
            var spotObject = new GameObject("UnlockSpot");
            spotObject.transform.SetParent(root.transform, false);
            spotObject.transform.localPosition = new Vector3(0f, 0.66f, -0.62f);
            spotObject.transform.localRotation = Quaternion.Euler(34f, 0f, 0f);
            Light spot = spotObject.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 3.2f;
            spot.spotAngle = 74f;
            spot.intensity = 0f;
            spot.shadows = LightShadows.None;

            Label(root.transform, "OverharvestLabel", new Vector3(0f, 0.56f, -0.43f), "과수확");

            var lever = root.AddComponent<InteractableOverharvestLever>();
            var so = new SerializedObject(lever);
            so.FindProperty("_grip").objectReferenceValue = gripCollider;
            so.FindProperty("_coverPivot").objectReferenceValue = coverPivot.transform;
            so.FindProperty("_closedAngle").floatValue = 0f;
            so.FindProperty("_openAngle").floatValue = -105f;     // 위·뒤로 젖혀 하우징에 붙는다 (앞으로 젖히면 손잡이를 가린다)
            so.FindProperty("_handlePivot").objectReferenceValue = handlePivot.transform;
            so.FindProperty("_handleRestAngle").floatValue = -20f;
            so.FindProperty("_handlePulledAngle").floatValue = 55f;
            so.FindProperty("_lockLight").objectReferenceValue = lockLight.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();

            // 하우징 콜라이더는 항상 켜 둔다. 잠긴 동안에도 조준은 걸려야
            // "왜 못 쓰는지"가 프롬프트로 읽힌다. 클릭은 CanInteract가 막는다.
            housingCollider.enabled = true;

            WireUnlockEffect(root, lever, spot, stripeLower, stripeUpper);
            return root;
        }

        /// <summary>
        /// 해제 순간 연출을 붙인다. `InteractableOverharvestLever.Unlocked` 이벤트는
        /// 진작 있었지만 **구독자가 없어서** 덮개만 조용히 열리고 끝났다.
        /// </summary>
        private static void WireUnlockEffect(GameObject root, InteractableOverharvestLever lever,
                                             Light spot, GameObject stripeLower, GameObject stripeUpper)
        {
            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.loop = false;
            audio.spatialBlend = 0.6f;   // 레버 쪽에서 나되 방 전체에 들린다
            audio.volume = 0f;

            var effect = root.AddComponent<OverharvestUnlockEffect>();
            var so = new SerializedObject(effect);
            so.FindProperty("_lever").objectReferenceValue = lever;
            so.FindProperty("_spotLight").objectReferenceValue = spot;
            so.FindProperty("_shakeTarget").objectReferenceValue = root.transform.Find("Housing");
            so.FindProperty("_audio").objectReferenceValue = audio;

            so.FindProperty("_riskView").objectReferenceValue = Object.FindAnyObjectByType<RiskStateView>();

            SerializedProperty stripes = so.FindProperty("_warningStripes");
            stripes.arraySize = 2;
            stripes.GetArrayElementAtIndex(0).objectReferenceValue = stripeLower.GetComponent<Renderer>();
            stripes.GetArrayElementAtIndex(1).objectReferenceValue = stripeUpper.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();
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

            // 정화 표식은 전용 오브젝트에 둔다. 막대 풀을 자식으로 만들기 때문에
            // 런 오브젝트에 붙이면 하이라키가 막대 28개로 덮인다.
            Transform markerRoot = GameObject.Find("PurifyMarkers")?.transform;
            if (markerRoot == null)
            {
                var go = new GameObject("PurifyMarkers");
                markerRoot = go.transform;
            }
            var markers = markerRoot.GetComponent<PurifyMarkerView>();
            if (markers == null) markers = markerRoot.gameObject.AddComponent<PurifyMarkerView>();

            var boardView = Object.FindAnyObjectByType<SpinBoardView>();
            var mso = new SerializedObject(markers);
            mso.FindProperty("_board").objectReferenceValue = boardView;
            mso.ApplyModifiedPropertiesWithoutUndo();

            var so = new SerializedObject(presenter);
            so.FindProperty("_board").objectReferenceValue = boardView;
            so.FindProperty("_markers").objectReferenceValue = markers;
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

        // ── 전력 게이지 ──

        private const float BarLeftX = -1.58f;
        private const float BarWidth = 1.72f;
        private const float BarMaxRatio = 3f;

        /// <summary>
        /// 게이지를 왼쪽 끝에서 자라도록 정규화하고 임계점 눈금을 세운다.
        ///
        /// 기존 채움 막대는 pivot 중심에 붙어 있어 절반이 왼쪽으로 삐져나왔다. 그 상태로는
        /// "지금 몇 %인가"가 눈금과 어긋난다. 여기서 폭 1 기준으로 맞춰 두면 뷰는
        /// `pivot.localScale.x = 비율 × 폭` 한 줄로 끝난다.
        ///
        /// 눈금이 필요한 이유: `visual-criteria.md` B-3.9 "100/130/170/220/300% 경계가
        /// 게이지 위에 표시되고, 넘는 순간이 사건으로 보이는가."
        /// </summary>
        private static void BuildPowerGauge()
        {
            Transform panel = GameObject.Find("InstrumentPanel")?.transform;
            if (panel == null) { Debug.LogWarning("[상승] InstrumentPanel 을 찾지 못했다."); return; }

            Transform pivot = panel.Find("PowerBarPivot");
            if (pivot == null) { Debug.LogWarning("[상승] PowerBarPivot 을 찾지 못했다."); return; }

            Transform fill = pivot.Find("PowerBarFill");
            if (fill != null)
            {
                fill.localScale = new Vector3(1f, 0.07f, 0.03f);
                fill.localPosition = new Vector3(0.5f, 0f, 0f);   // pivot 왼쪽 끝에서 오른쪽으로 자란다
            }
            pivot.localScale = new Vector3(0f, 1f, 1f);

            // 이름이 실제 내용과 달라 다음 세션을 헷갈리게 한다. 무게가 아니라 상태를 띄운다.
            Transform weightLabel = panel.Find("WeightLabel");
            if (weightLabel != null) weightLabel.name = "StatusLabel";

            Replace(panel, "PowerBarTicks", out GameObject ticks);
            Material tickMaterial = Mat("M_Gray_Readout");
            // 300%(폭주 상승)가 빠져 있었다. `PowerThresholds`는 임계점을 다섯 개
            // 정의하는데 게이지에는 넷만 그려져, 마지막 구간만 경계 없이 넘어갔다.
            // `visual-criteria` B-3.9가 "경계가 게이지 위에 표시되고 넘는 순간이 사건으로
            // 보이는가"를 묻는 항목이라 하나가 빠지면 그 구간은 판정 자체가 불가능하다.
            // `BarMaxRatio`가 3이므로 300%는 바의 오른쪽 끝에 정확히 놓인다.
            float[] gates = { 1.0f, 1.3f, 1.7f, 2.2f, 3.0f };
            foreach (float gate in gates)
            {
                float x = BarLeftX + BarWidth * (gate / BarMaxRatio);
                // 100%만 굵게 — 나머지 임계점과 "요구 전력"은 무게가 다르다.
                float width = Mathf.Approximately(gate, 1f) ? 0.030f : 0.016f;
                float height = Mathf.Approximately(gate, 1f) ? 0.19f : 0.15f;
                Box(ticks.transform, $"Tick_{Mathf.RoundToInt(gate * 100f)}",
                    new Vector3(x, 1.30f, 1.36f), new Vector3(width, height, 0.02f), tickMaterial);
            }
        }

        // ── 계기판 / 위험 상태 ──

        private static void EnsureInstrumentPanelView()
        {
            Transform panel = GameObject.Find("InstrumentPanel")?.transform;
            if (panel == null) return;

            var view = panel.GetComponent<InstrumentPanelView>();
            if (view == null) view = panel.gameObject.AddComponent<InstrumentPanelView>();

            var so = new SerializedObject(view);
            so.FindProperty("_run").objectReferenceValue = Object.FindAnyObjectByType<RunSessionBehaviour>();
            so.FindProperty("_bridge").objectReferenceValue = Object.FindAnyObjectByType<RouletteInteractionBridge>();
            so.FindProperty("_floorLabel").objectReferenceValue = Tmp(panel, "FloorLabel");
            so.FindProperty("_powerLabel").objectReferenceValue = Tmp(panel, "PowerLabel");
            so.FindProperty("_statusLabel").objectReferenceValue = Tmp(panel, "StatusLabel");
            so.FindProperty("_barPivot").objectReferenceValue = panel.Find("PowerBarPivot");
            so.FindProperty("_barWidth").floatValue = BarWidth;
            so.FindProperty("_maxRatio").floatValue = BarMaxRatio;

            Transform fill = panel.Find("PowerBarPivot/PowerBarFill");
            so.FindProperty("_barFill").objectReferenceValue = fill != null ? fill.GetComponent<Renderer>() : null;

            SerializedProperty plaques = so.FindProperty("_contractPlaques");
            plaques.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                Transform plaque = panel.Find($"ContractPlaque_{i}");
                plaques.GetArrayElementAtIndex(i).objectReferenceValue =
                    plaque != null ? plaque.GetComponent<Renderer>() : null;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 위험 상태를 실제로 표현하려면 실내등과 소리가 필요하다. 씬에는 방향광 하나뿐이라
        /// 천장등을 껐다 켜도 실내 밝기가 안 변한다 — Stable과 Critical이 캡처에서 구분되지 않는다.
        /// </summary>
        private static void EnsureRiskStateView(Transform car)
        {
            Transform lamp = car.Find("CeilingLamp");
            if (lamp == null) { Debug.LogWarning("[상승] CeilingLamp 를 찾지 못했다."); return; }

            Transform lightTransform = lamp.Find("CabinLight");
            Light cabinLight;
            if (lightTransform == null)
            {
                var go = new GameObject("CabinLight");
                go.transform.SetParent(lamp, false);
                go.transform.localPosition = new Vector3(0f, -0.6f, 0f);
                cabinLight = go.AddComponent<Light>();
                cabinLight.type = LightType.Point;
                cabinLight.range = 7f;
                cabinLight.shadows = LightShadows.Soft;
            }
            else cabinLight = lightTransform.GetComponent<Light>();

            var run = Object.FindAnyObjectByType<RunSessionBehaviour>();
            if (run == null) return;

            var hum = run.GetComponent<AudioSource>();
            if (hum == null) hum = run.gameObject.AddComponent<AudioSource>();
            hum.playOnAwake = false;
            hum.loop = true;
            hum.spatialBlend = 0f;   // 기계 험은 방 전체의 상태다. 위치를 갖지 않는다.
            hum.volume = 0f;

            var view = run.GetComponent<RiskStateView>();
            if (view == null) view = run.gameObject.AddComponent<RiskStateView>();

            var recorder = run.GetComponent<AccidentRecorder>();
            if (recorder == null) recorder = run.gameObject.AddComponent<AccidentRecorder>();
            var rso = new SerializedObject(recorder);
            rso.FindProperty("_run").objectReferenceValue = run;
            rso.FindProperty("_risk").objectReferenceValue = view;
            rso.ApplyModifiedPropertiesWithoutUndo();

            Transform overload = GameObject.Find("InstrumentPanel")?.transform.Find("OverloadLight");
            Transform head = Object.FindAnyObjectByType<Ascend.Prototype.Player.FirstPersonController>()
                                   ?.transform.Find("Head");

            var so = new SerializedObject(view);
            so.FindProperty("_run").objectReferenceValue = run;
            so.FindProperty("_cabinLight").objectReferenceValue = cabinLight;
            so.FindProperty("_lampRenderer").objectReferenceValue = lamp.GetComponent<Renderer>();
            so.FindProperty("_warningLight").objectReferenceValue =
                overload != null ? overload.GetComponent<Renderer>() : null;
            so.FindProperty("_swayTarget").objectReferenceValue = lamp;
            so.FindProperty("_cameraTarget").objectReferenceValue = head;
            so.FindProperty("_hum").objectReferenceValue = hum;
            so.ApplyModifiedPropertiesWithoutUndo();

            var panelView = Object.FindAnyObjectByType<InstrumentPanelView>();
            if (panelView != null)
            {
                var pso = new SerializedObject(panelView);
                pso.FindProperty("_risk").objectReferenceValue = view;
                pso.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ── 화면 UI ──

        private const float UiWidth = 1920f;
        private const float UiHeight = 1080f;

        /// <summary>
        /// 화면 UI를 UGUI로 세운다. IMGUI HUD를 대체한다.
        ///
        /// 배치 원칙은 `VISUAL_SPEC.md` §5의 우선순위다. 화면에는 **공간이 담을 수 없는 것만**
        /// 남긴다 — 전력·요구·스핀·잔류·계약은 이미 벽면 계기판이 말하고 있으므로 올리지 않는다.
        /// </summary>
        private static void BuildScreenUi()
        {
            Replace(null, "GameHUD", out GameObject root);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;   // 조준 HUD 위

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UiWidth, UiHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // 레이캐스터를 붙이지 않는다. 이 UI는 클릭 대상이 아니고,
            // 붙이면 1인칭 조준 클릭을 가로챈다.

            // 지금 무엇을 할 수 있는가 — 화면 아래 가운데, 한 줄
            GameObject hint = Panel(root.transform, "Hint",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 132f),
                new Vector2(1100f, 46f));
            TextMeshProUGUI hintText = Label(hint.transform, "HintText", 26f,
                TextAlignmentOptions.Center, new Color(0.92f, 0.93f, 0.96f));

            // 지금 무엇 때문에 터졌는가 — 화면 위 가운데, 연출 중에만
            GameObject cascade = Panel(root.transform, "Cascade",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f),
                new Vector2(1200f, 130f));
            TextMeshProUGUI depthText = Label(cascade.transform, "DepthText", 54f,
                TextAlignmentOptions.Top, new Color(1f, 0.88f, 0.58f));
            TextMeshProUGUI causeText = Label(cascade.transform, "CauseText", 28f,
                TextAlignmentOptions.Bottom, new Color(0.88f, 0.90f, 0.94f));

            // 층 결과 — 사고 기록기 요약
            GameObject result = Panel(root.transform, "Result",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f),
                new Vector2(1000f, 420f));
            TextMeshProUGUI resultTitle = Label(result.transform, "ResultTitle", 62f,
                TextAlignmentOptions.Top, new Color(1f, 0.92f, 0.72f));
            TextMeshProUGUI resultBody = Label(result.transform, "ResultBody", 28f,
                TextAlignmentOptions.Center, new Color(0.90f, 0.92f, 0.96f));

            // 디버그 — 좌하단, 기본 꺼짐
            GameObject debug = Panel(root.transform, "Debug",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 24f),
                new Vector2(760f, 210f));
            var debugRect = debug.GetComponent<RectTransform>();
            debugRect.pivot = new Vector2(0f, 0f);
            TextMeshProUGUI debugText = Label(debug.transform, "DebugText", 22f,
                TextAlignmentOptions.BottomLeft, new Color(0.80f, 0.86f, 0.92f));

            var hud = root.AddComponent<GameHudView>();
            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("_run").objectReferenceValue = Object.FindAnyObjectByType<RunSessionBehaviour>();
            hudSo.FindProperty("_bridge").objectReferenceValue = Object.FindAnyObjectByType<RouletteInteractionBridge>();
            hudSo.FindProperty("_presenter").objectReferenceValue = Object.FindAnyObjectByType<SpinPresenter>();
            hudSo.FindProperty("_recorder").objectReferenceValue = Object.FindAnyObjectByType<AccidentRecorder>();
            hudSo.FindProperty("_hintGroup").objectReferenceValue = hint.GetComponent<CanvasGroup>();
            hudSo.FindProperty("_hintText").objectReferenceValue = hintText;
            hudSo.FindProperty("_cascadeGroup").objectReferenceValue = cascade.GetComponent<CanvasGroup>();
            hudSo.FindProperty("_cascadeDepthText").objectReferenceValue = depthText;
            hudSo.FindProperty("_cascadeCauseText").objectReferenceValue = causeText;
            hudSo.FindProperty("_resultGroup").objectReferenceValue = result.GetComponent<CanvasGroup>();
            hudSo.FindProperty("_resultTitleText").objectReferenceValue = resultTitle;
            hudSo.FindProperty("_resultBodyText").objectReferenceValue = resultBody;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            var panel = root.AddComponent<DebugPanelView>();
            var panelSo = new SerializedObject(panel);
            panelSo.FindProperty("_run").objectReferenceValue = Object.FindAnyObjectByType<RunSessionBehaviour>();
            panelSo.FindProperty("_bridge").objectReferenceValue = Object.FindAnyObjectByType<RouletteInteractionBridge>();
            panelSo.FindProperty("_risk").objectReferenceValue = Object.FindAnyObjectByType<RiskStateView>();
            panelSo.FindProperty("_group").objectReferenceValue = debug.GetComponent<CanvasGroup>();
            panelSo.FindProperty("_bodyText").objectReferenceValue = debugText;
            panelSo.FindProperty("_visible").boolValue = false;
            panelSo.ApplyModifiedPropertiesWithoutUndo();

            RemoveLegacyImguiHud();
        }

        /// <summary>
        /// 옛 IMGUI HUD 컴포넌트를 씬에서 뗀다. 남겨 두면 UGUI와 같은 정보를 두 벌 그리고,
        /// 프레임당 GC 할당도 그대로 남는다.
        /// </summary>
        private static void RemoveLegacyImguiHud()
        {
            var run = Object.FindAnyObjectByType<RunSessionBehaviour>();
            if (run == null) return;

            foreach (MonoBehaviour behaviour in run.GetComponents<MonoBehaviour>())
            {
                if (behaviour == null) continue;
                if (behaviour.GetType().Name != "RouletteHud") continue;
                Object.DestroyImmediate(behaviour);
                Debug.Log("[상승] 옛 IMGUI HUD(RouletteHud) 제거 — UGUI GameHudView 로 대체.");
            }
        }

        private static GameObject Panel(Transform parent, string name,
                                        Vector2 anchorMin, Vector2 anchorMax,
                                        Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            // 클릭을 먹지 않는다 — 1인칭 조준이 화면 UI에 가리면 안 된다.
            go.GetComponent<CanvasGroup>().blocksRaycasts = false;
            return go;
        }

        private static TextMeshProUGUI Label(Transform parent, string name, float size,
                                             TextAlignmentOptions alignment, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var label = go.AddComponent<TextMeshProUGUI>();
            label.fontSize = size;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            label.text = string.Empty;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) label.font = font;
            return label;
        }

        private static TMP_Text Tmp(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            return t != null ? t.GetComponent<TextMeshPro>() : null;
        }

        // ── 헬퍼 ──

        /// <summary>parent가 null이면 씬 루트에 만든다.</summary>
        private static void Replace(Transform parent, string name, out GameObject created)
        {
            Transform existing = parent != null ? parent.Find(name) : FindSceneRoot(name);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            created = new GameObject(name);
            if (parent != null) created.transform.SetParent(parent, false);
        }

        private static Transform FindSceneRoot(string name)
        {
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == name) return root.transform;
            return null;
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

        /// <summary>
        /// 3D 라벨. 기존 계기판 라벨과 **같은 규약**을 쓴다 — 스케일 0.07 / fontSize 10.
        ///
        /// 처음에는 fontSize 를 월드 단위로 계산하고 Y 180° 를 걸었다가, 캡처에서 글자가
        /// 벽 하나를 덮고 좌우가 뒤집혀 나왔다. TMP 3D 텍스트는 회전 없이 이미 방 안쪽을
        /// 향하고, 크기는 transform 스케일로 잡는 것이 이 씬의 기존 방식이다.
        /// </summary>
        private static void Label(Transform parent, string name, Vector3 localPosition, string text)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = Vector3.one * 0.05f;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 10f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) tmp.font = font;
            tmp.rectTransform.sizeDelta = new Vector2(5f, 1.2f);
        }

        /// <summary>
        /// 계기판 라벨이 오른쪽에서 잘려 "위험 안정"이 "위험 안"으로 보이고 있었다.
        /// rect 가 좁아서 생긴 문제라 폭만 넓힌다.
        /// </summary>
        private static void WidenPanelLabels()
        {
            Transform panel = GameObject.Find("InstrumentPanel")?.transform;
            if (panel == null) return;

            // 세로 위치도 다시 잡는다. 상태 라벨(2줄)이 게이지 눈금 위에서 끝나야 한다.
            var rows = new (string Name, float Y)[]
            {
                ("FloorLabel", 1.76f),
                ("PowerLabel", 1.62f),
                ("StatusLabel", 1.50f),
            };

            foreach ((string name, float y) in rows)
            {
                var tmp = Tmp(panel, name);
                if (tmp == null) continue;
                tmp.rectTransform.sizeDelta = new Vector2(26f, 6f);
                tmp.enableWordWrapping = false;
                tmp.overflowMode = TextOverflowModes.Overflow;
                tmp.alignment = TextAlignmentOptions.TopLeft;

                Vector3 position = tmp.transform.localPosition;
                position.y = y;
                tmp.transform.localPosition = position;
            }
        }

        private static Material Mat(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/{name}.mat");
            if (material == null) Debug.LogWarning($"[상승] 머티리얼을 찾지 못했다: {name}");
            return material;
        }
    }
}
