using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ascend.Prototype.Build;
using Ascend.Prototype.Player;
using Ascend.Prototype.Run;
using Ascend.Prototype.View;

namespace Ascend.Prototype.EditorTools
{
    /// <summary>
    /// 10층 런에 필요한 씬 오브젝트를 세운다. **멱등**이다 — 여러 번 실행해도 같은 결과다.
    ///
    /// 씬을 손으로 편집하지 않고 빌더로 만드는 것이 이 저장소의 방식이다
    /// (`HeroSliceSceneBuilder`, `GrayboxWorldBuilder`와 같은 패턴). 이유는 두 가지다.
    /// 첫째, `.unity`는 fileID로 상호 참조하는 YAML이라 손 편집이 조용히 깨진다.
    /// 둘째, 좌표가 코드에 남아야 "왜 여기인가"를 주석으로 설명할 수 있다.
    /// </summary>
    public static class TenFloorSceneBuilder
    {
        private const string ScenePath = "Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity";

        [MenuItem("Ascend/Build Ten Floor Scene Objects")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[상승] Play 모드에서는 씬을 고치지 않는다. 먼저 종료한다.");
                return;
            }

            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var report = new StringBuilder("[상승] 10층 씬 오브젝트\n");

            Transform car = Find("GrayboxWorld/Car");
            if (car == null)
            {
                Debug.LogError("[상승] GrayboxWorld/Car 를 찾지 못했다. 먼저 'Ascend/Build Graybox World' 를 실행한다.");
                return;
            }

            // ── 1. 런 모드 ─────────────────────────────────────────────────
            var run = Object.FindFirstObjectByType<RunSessionBehaviour>(FindObjectsInactive.Include);
            if (run == null)
            {
                Debug.LogError("[상승] RunSessionBehaviour 가 씬에 없다.");
                return;
            }

            var runObject = new SerializedObject(run);
            SerializedProperty mode = runObject.FindProperty("_mode");
            if (mode != null && mode.enumValueIndex != (int)RunMode.TenFloor)
            {
                mode.enumValueIndex = (int)RunMode.TenFloor;
                runObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(run);
                report.AppendLine("  런 모드 → TenFloor");
            }
            else report.AppendLine("  런 모드: 이미 TenFloor");

            // ── 2. 층수 표시등 ──────────────────────────────────────────────
            //
            // 출입구 상인방(BackWall_Lintel)은 (0.65, 2.28, 1.60)에 두께 0.20이므로
            // 실내 쪽 면이 z=1.50이다. 표시등은 그 면에 붙는다.
            Transform indicator = EnsureChild(car, "FloorIndicator",
                new Vector3(0.65f, 2.26f, 1.45f), Quaternion.identity);
            if (indicator.GetComponent<FloorIndicatorView>() == null)
            {
                indicator.gameObject.AddComponent<FloorIndicatorView>();
                report.AppendLine("  FloorIndicatorView 추가");
            }
            else report.AppendLine("  FloorIndicatorView: 이미 있음");

            // ── 3. 문 개폐 손잡이 ───────────────────────────────────────────
            //
            // 출입구 왼쪽 벽(BackWall_Left, x -1.805~0.145)의 실내 면에 붙인다.
            // 과수확 레버(0.55, 1.32, 1.36)에서 1.1m 떨어뜨려 혼동을 막는다
            // (`D-20260730-08` — 한 물체가 한 뜻).
            Transform door = EnsureChild(car, "DoorControl",
                new Vector3(-0.55f, 1.20f, 1.44f), Quaternion.identity);
            EnsureLeverShape(door);
            var doorControl = door.GetComponent<InteractableDoorControl>();
            if (doorControl == null)
            {
                doorControl = door.gameObject.AddComponent<InteractableDoorControl>();
                report.AppendLine("  InteractableDoorControl 추가");
            }
            else report.AppendLine("  InteractableDoorControl: 이미 있음");

            // ── 4. 승객·부품 배치 뷰 ────────────────────────────────────────
            Transform figures = EnsureRoot(scene, "BuildFigures", Vector3.zero);
            var figureView = figures.GetComponent<BuildFigureView>();
            if (figureView == null)
            {
                figureView = figures.gameObject.AddComponent<BuildFigureView>();
                report.AppendLine("  BuildFigureView 추가");
            }
            else report.AppendLine("  BuildFigureView: 이미 있음");

            var viewObject = new SerializedObject(figureView);
            SetReference(viewObject, "_run", run);
            SetReference(viewObject, "_doorControl", doorControl);
            viewObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(figureView);

            var indicatorView = indicator.GetComponent<FloorIndicatorView>();
            var indicatorObject = new SerializedObject(indicatorView);
            SetReference(indicatorObject, "_run", run);
            indicatorObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(indicatorView);

            // ── 5. 배선 확인 ────────────────────────────────────────────────
            var bridge = Object.FindFirstObjectByType<RouletteInteractionBridge>(FindObjectsInactive.Include);
            report.AppendLine(bridge != null
                ? "  RouletteInteractionBridge: 있음"
                : "  ⚠ RouletteInteractionBridge 없음 — 레버가 런에 연결되지 않는다");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report.AppendLine("  씬 저장 완료");
            Debug.Log(report.ToString());
        }

        // ══════════════════════════════════════════════════════════════════════
        //  좁고 높은 화물 엘리베이터로 다시 비례를 잡는다.
        //
        //  실측 결과 내부가 폭 3.20 × 깊이 3.40 × 높이 2.50이었다. 높이보다 폭이 큰
        //  상자는 `AUTONOMOUS_PROTOTYPE_GOAL.md` §4의 "좁고 높은 직사각형 구조"와
        //  정반대로 읽힌다. 게다가 z=-1.70 면에는 벽 오브젝트가 아예 없어서
        //  뒤를 돌면 상자 밖이 보였다.
        //
        //  목표: 폭 2.40 × 깊이 3.00 × 높이 3.20. 좁고, 깊고, 높다.
        //  1960년대 화물 엘리베이터의 실제 비례에 가깝고, 승객 6명과 화물이
        //  둘레에 서도 가운데 통로가 남는다.
        //
        //  좌표를 코드에 두는 이유는 `Build()`와 같다 — 왜 여기인지 설명이 붙어야 한다.
        // ══════════════════════════════════════════════════════════════════════

        private const float InnerHalfWidth = 1.20f;   // 벽 안쪽 면
        private const float WallThickness = 0.20f;
        private const float InnerHeight = 3.20f;
        private const float FrontWallZ = -1.60f;      // 앞벽 중심 (안쪽 면 -1.50)

        [MenuItem("Ascend/Reproportion Elevator Car")]
        public static void Reproportion()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[상승] Play 모드에서는 씬을 고치지 않는다.");
                return;
            }

            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            Transform car = Find("GrayboxWorld/Car");
            if (car == null) { Debug.LogError("[상승] GrayboxWorld/Car 없음"); return; }

            var report = new StringBuilder("[상승] 엘리베이터 비례 재조정\n");
            float outer = InnerHalfWidth + WallThickness;   // 1.40
            float shellWidth = outer * 2f;                  // 2.80

            // ── 껍데기 ──
            Place(car, "Floor",   new Vector3(0f, -0.10f, -0.05f), new Vector3(shellWidth, 0.20f, 3.20f), report);
            Place(car, "Ceiling", new Vector3(0f, InnerHeight + 0.10f, -0.05f), new Vector3(shellWidth, 0.20f, 3.20f), report);
            Place(car, "WallL",   new Vector3(-(InnerHalfWidth + WallThickness * 0.5f), InnerHeight * 0.5f, -0.05f),
                new Vector3(WallThickness, InnerHeight, 3.20f), report);
            Place(car, "WallR",   new Vector3( InnerHalfWidth + WallThickness * 0.5f, InnerHeight * 0.5f, -0.05f),
                new Vector3(WallThickness, InnerHeight, 3.20f), report);

            // 출입구는 폭 1.00, 높이 2.05를 유지한다. 사람이 드나드는 치수라 함부로 못 바꾼다.
            // 좌우 벽면만 새 폭에 맞춰 줄인다.
            Place(car, "BackWall_Left",  new Vector3((-outer + 0.15f) * 0.5f, InnerHeight * 0.5f, 1.60f),
                new Vector3(outer + 0.15f, InnerHeight, WallThickness), report);
            Place(car, "BackWall_Right", new Vector3((1.15f + outer) * 0.5f, InnerHeight * 0.5f, 1.60f),
                new Vector3(outer - 1.15f, InnerHeight, WallThickness), report);
            Place(car, "BackWall_Lintel", new Vector3(0.65f, (2.05f + InnerHeight) * 0.5f, 1.60f),
                new Vector3(1.00f, InnerHeight - 2.05f, WallThickness), report);

            // ── 앞벽 신설 ──
            // 없으면 뒤를 돌았을 때 상자 밖이 보인다. 좁은 공간의 압박은 네 면이 다 있어야 생긴다.
            Transform front = car.Find("FrontWall");
            if (front == null)
            {
                GameObject created = GameObject.CreatePrimitive(PrimitiveType.Cube);
                created.name = "FrontWall";
                created.transform.SetParent(car, false);
                front = created.transform;
                report.AppendLine("  FrontWall 신설 — z=-1.70 면의 구멍을 막는다");
            }
            front.localPosition = new Vector3(0f, InnerHeight * 0.5f, FrontWallZ);
            front.localScale = new Vector3(shellWidth, InnerHeight, WallThickness);

            // ── 장치: 새 벽 안쪽으로 당긴다 ──
            MoveX(car, "PowerTank", 0.95f, report);
            MoveX(car, "TankTick_0", 0.95f, report);
            MoveX(car, "TankTick_1", 0.95f, report);
            MoveX(car, "TankTick_2", 0.95f, report);
            MoveX(car, "TankTick_3", 0.95f, report);
            MoveX(car, "TankStand", 0.95f, report);
            MoveX(car, "Handrail_R", 1.14f, report);
            Place(car, "Handrail_B", new Vector3(-0.55f, 0.92f, 1.45f), new Vector3(1.20f, 0.06f, 0.06f), report);
            Place(car, "CeilingLamp", new Vector3(0f, InnerHeight - 0.06f, 0f), new Vector3(0.85f, 0.05f, 0.45f), report);

            // 통관은 왼쪽 벽에 붙어 있다. 벽이 0.50 안으로 들어왔으므로 같이 들어온다.
            // 높이는 건드리지 않는다 — 결과판 중심 y=1.60이 눈높이 1.62와 맞아 있고,
            // 그건 `visual-criteria` B-1.2가 요구하는 조건이다.
            Transform tubes = Find("TubesRoot");
            if (tubes != null)
            {
                foreach (Transform tube in tubes)
                {
                    Vector3 p = tube.position;
                    if (p.x < -1.0f) { tube.position = new Vector3(-0.95f, p.y, p.z); }
                }
                report.AppendLine("  TubesRoot: 세 통관을 x=-0.95 로 이동");
            }

            // ── 계기판을 새 폭 안으로 ──
            //
            // 계기판은 폭 3.26m로 저작돼 있었다(구 내부 폭 3.20에 맞춘 값이다).
            // 벽을 2.40으로 좁히자 x[-1.65..1.61]이 되어 **양쪽 벽을 뚫고 나갔다.**
            // 전력 바와 임계점 눈금의 일부가 벽 속에 묻힌다는 뜻이고, 그러면
            // `visual-criteria` B-3.8("등을 돌려도 전력을 아는가")이 성립하지 않는다.
            //
            // X만 눌러 담는다. 균등 축소하면 패널이 원점 기준으로 줄어들어 눈높이 아래로
            // 내려간다 — 시선 높이는 B-1.2가 요구하는 조건이라 건드리면 안 된다.
            // 대신 글자는 부모의 X 압축을 자식에서 되돌려 찌그러지지 않게 한다.
            Transform panel = car.Find("InstrumentPanel");
            if (panel != null)
            {
                const float squeeze = 0.66f;
                panel.localScale = new Vector3(squeeze, 1f, 1f);
                foreach (TMPro.TMP_Text label in panel.GetComponentsInChildren<TMPro.TMP_Text>(true))
                {
                    // 보정값의 기준을 **Y 스케일**에서 가져온다. 이 라벨들은 균등 스케일
                    // (0.07)로 저작돼 있고 Y는 아무도 건드리지 않으므로, Y가 원본의
                    // 유일하게 믿을 수 있는 사본이다.
                    //
                    // 처음에는 `1f / squeeze`를 그대로 넣었다. 그러면 0.07이어야 할 X가
                    // 1.52가 되어 **글자가 21배로 늘어났고**, 패널 경계가 x=3.05까지
                    // 밀려 나갔다. 그다음엔 기존 값에 곱했더니 실행할 때마다 누적됐다.
                    // Y 기준 절대값이라야 21배도 누적도 없다.
                    Vector3 s = label.transform.localScale;
                    label.transform.localScale = new Vector3(s.y / squeeze, s.y, s.z);
                }

                // 300%(폭주 상승) 눈금이 없었다. `PowerThresholds`는 임계점을 다섯 개
                // 정의하는데 게이지에는 넷만 그려져 마지막 구간만 경계 없이 넘어갔다.
                // `HeroSliceSceneBuilder`의 게이트 목록도 함께 고쳤지만, 그 빌더를 통째로
                // 다시 돌리면 이번 세션의 배치가 전부 덮이므로 여기서 하나만 세운다.
                EnsureTick(panel, "Tick_300", new Vector3(0.14f, 1.30f, 1.36f),
                    new Vector3(0.016f, 0.15f, 0.02f), report);

                report.AppendLine($"  InstrumentPanel X ×{squeeze} (글자는 Y 기준으로 보정)");
            }

            // ── 폐기된 타이밍 정지 조작부 제거 ──
            //
            // `D-20260730-04`가 통관별 정지 버튼을 제외했는데, 씬에는 비활성 상태로 남아
            // 있었다. 숨기는 것으로는 부족하다 — `visual-criteria` B-5.13이 "잔재가 보이면
            // 실패"라고 했고, 비활성 오브젝트는 누가 한 번 켜면 그대로 캡처에 들어온다.
            // 빌더 쪽(`GrayboxWorldBuilder`)에서도 생성을 끊었으므로 여기서 실물을 지운다.
            int purged = PurgeDeprecated(scene, report);
            report.AppendLine($"  폐기 조작부 {purged}개 제거");

            // ── 외부 복도 깊이 ──
            //
            // Notion 「자동 룰렛 프로토타입 3D 리소스 제작 타겟」 §4가 문 밖 공간을 3~5m로
            // 요구하는데 현재는 1.4m다. 후보 승객이 서 있을 자리도, 문이 열릴 때 보여줄
            // 장면도 그 깊이에서는 만들어지지 않는다.
            Transform lobby = Find("GrayboxWorld/Lobby");
            if (lobby != null)
            {
                Place(lobby, "LobbyFloor", new Vector3(0.65f, -0.10f, 3.40f), new Vector3(3.00f, 0.20f, 3.60f), report);
                Place(lobby, "LobbyBack",  new Vector3(0.65f,  1.60f, 5.30f), new Vector3(3.00f, 3.20f, 0.20f), report);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report.AppendLine($"  → 내부 폭 {InnerHalfWidth * 2f:F2} × 깊이 3.00 × 높이 {InnerHeight:F2}");
            Debug.Log(report.ToString());
        }

        /// <summary>임계점 눈금 하나를 보장한다. 이웃 눈금의 재질을 그대로 쓴다.</summary>
        private static void EnsureTick(Transform panel, string name, Vector3 localPosition,
            Vector3 scale, StringBuilder report)
        {
            Transform ticks = panel.Find("PowerBarTicks");
            if (ticks == null)
            {
                // 눈금 컨테이너가 없으면 패널 바로 아래에 둔다.
                ticks = panel;
            }

            Transform existing = ticks.Find(name);
            if (existing == null)
            {
                GameObject created = GameObject.CreatePrimitive(PrimitiveType.Cube);
                created.name = name;
                created.transform.SetParent(ticks, false);
                Object.DestroyImmediate(created.GetComponent<Collider>());

                // 이웃 눈금과 같은 재질이어야 한 줄로 읽힌다.
                Transform sibling = ticks.Find("Tick_220") ?? ticks.Find("Tick_100");
                var siblingRenderer = sibling != null ? sibling.GetComponent<MeshRenderer>() : null;
                var renderer = created.GetComponent<MeshRenderer>();
                if (siblingRenderer != null && renderer != null)
                    renderer.sharedMaterial = siblingRenderer.sharedMaterial;

                existing = created.transform;
                report.AppendLine($"  {name} 신설 — 300% 경계가 게이지에 없었다");
            }
            existing.localPosition = localPosition;
            existing.localScale = scale;
        }

        /// <summary>폐기된 조작부 오브젝트를 이름으로 찾아 지운다. 멱등이다.</summary>
        private static int PurgeDeprecated(Scene scene, StringBuilder report)
        {
            string[] prefixes = { "StopButton", "ButtonPivot", "ButtonLabel", "TubeReadout" };
            var doomed = new System.Collections.Generic.List<GameObject>();

            foreach (GameObject root in scene.GetRootGameObjects())
                Collect(root.transform, prefixes, doomed);

            foreach (GameObject target in doomed)
            {
                report.AppendLine($"  제거: {target.name}");
                Object.DestroyImmediate(target);
            }
            return doomed.Count;
        }

        private static void Collect(Transform node, string[] prefixes,
            System.Collections.Generic.List<GameObject> doomed)
        {
            foreach (string prefix in prefixes)
            {
                if (!node.name.StartsWith(prefix, System.StringComparison.Ordinal)) continue;
                doomed.Add(node.gameObject);
                return;   // 자식은 부모와 함께 사라진다
            }
            for (int i = node.childCount - 1; i >= 0; i--)
                Collect(node.GetChild(i), prefixes, doomed);
        }

        /// <summary>
        /// 껍데기에 산업용 팔레트를 입힌다.
        ///
        /// 캡처를 보면 지금은 밝은 중성 회색 상자다. `AUTONOMOUS_PROTOTYPE_GOAL.md` §4가
        /// 금지 목록에 "깨끗하고 대칭적인 쇼룸 구성"을 넣은 이유가 바로 이 상태다.
        /// 요구는 "탁하고 제한된 색상 / 차가운 회녹색 그림자 / 바랜 산업용 색"이다.
        ///
        /// 조명은 건드리지 않는다 — `RiskStateView`가 Stable/Warning/Critical 을 조명으로
        /// 표현하므로, 기본값을 여기서 바꾸면 위험 단계 연출이 조용히 어긋난다.
        /// 색은 재질에서만 낮춘다.
        /// </summary>
        [MenuItem("Ascend/Apply Industrial Palette")]
        public static void ApplyPalette()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[상승] Play 모드에서는 씬을 고치지 않는다.");
                return;
            }

            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            Transform car = Find("GrayboxWorld/Car");
            if (car == null) { Debug.LogError("[상승] Car 없음"); return; }

            var report = new StringBuilder("[상승] 산업용 팔레트\n");

            // 녹슨 철판 바닥, 차가운 회녹색 벽, 더 어두운 천장.
            // 천장을 벽보다 어둡게 두면 같은 조명에서도 공간이 높아 보인다.
            Tint(car, "Floor",            new Color(0.243f, 0.216f, 0.184f), report);
            Tint(car, "Ceiling",          new Color(0.145f, 0.161f, 0.153f), report);
            Tint(car, "WallL",            new Color(0.259f, 0.286f, 0.267f), report);
            Tint(car, "WallR",            new Color(0.259f, 0.286f, 0.267f), report);
            Tint(car, "FrontWall",        new Color(0.235f, 0.263f, 0.243f), report);
            Tint(car, "BackWall_Left",    new Color(0.239f, 0.267f, 0.247f), report);
            Tint(car, "BackWall_Right",   new Color(0.239f, 0.267f, 0.247f), report);
            Tint(car, "BackWall_Lintel",  new Color(0.192f, 0.216f, 0.200f), report);
            Tint(car, "Handrail_R",       new Color(0.325f, 0.318f, 0.286f), report);
            Tint(car, "Handrail_B",       new Color(0.325f, 0.318f, 0.286f), report);
            Tint(car, "TankStand",        new Color(0.278f, 0.263f, 0.231f), report);

            Transform lobby = Find("GrayboxWorld/Lobby");
            if (lobby != null)
            {
                // 외부 복도는 더 어둡다(§4 "어두운 외부 복도"). 문 너머가 밝으면
                // 좁은 실내의 압박이 사라지고, 후보가 서 있는 어둠도 만들어지지 않는다.
                Tint(lobby, "LobbyFloor", new Color(0.106f, 0.114f, 0.110f), report);
                Tint(lobby, "LobbyBack",  new Color(0.086f, 0.094f, 0.090f), report);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(report.ToString());
        }

        private static void Tint(Transform parent, string name, Color color, StringBuilder report)
        {
            Transform target = parent.Find(name);
            if (target == null) { report.AppendLine($"  ⚠ {name} 없음"); return; }
            var renderer = target.GetComponent<MeshRenderer>();
            if (renderer == null) { report.AppendLine($"  ⚠ {name} 렌더러 없음"); return; }

            // 공유 재질을 그대로 칠하면 같은 재질을 쓰는 다른 오브젝트까지 바뀐다.
            // 오브젝트마다 전용 인스턴스를 만들어 씬에 묻는다.
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "CarShell_" + name };
            material.color = color;
            // 광택을 죽인다. 반사가 있으면 로우폴리 면이 매끈해 보여 PS1 방향에서 멀어진다.
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.03f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(renderer);
            report.AppendLine($"  {name} → #{ColorUtility.ToHtmlStringRGB(color)}");
        }

        private static void Place(Transform parent, string name, Vector3 localPosition,
            Vector3 scale, StringBuilder report)
        {
            Transform target = parent.Find(name);
            if (target == null) { report.AppendLine($"  ⚠ {name} 없음"); return; }
            target.localPosition = localPosition;
            target.localScale = scale;
            report.AppendLine($"  {name} → pos {localPosition} scale {scale}");
        }

        private static void MoveX(Transform parent, string name, float x, StringBuilder report)
        {
            Transform target = parent.Find(name);
            if (target == null) { report.AppendLine($"  ⚠ {name} 없음"); return; }
            Vector3 p = target.localPosition;
            target.localPosition = new Vector3(Mathf.Sign(p.x == 0f ? 1f : p.x) * Mathf.Abs(x), p.y, p.z);
            report.AppendLine($"  {name} x {p.x:F2} → {target.localPosition.x:F2}");
        }

        private static void SetReference(SerializedObject serialized, string path, Object value)
        {
            SerializedProperty property = serialized.FindProperty(path);
            if (property != null) property.objectReferenceValue = value;
        }

        private static Transform Find(string path)
        {
            GameObject found = GameObject.Find(path);
            return found != null ? found.transform : null;
        }

        private static Transform EnsureRoot(Scene scene, string name, Vector3 position)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) return root.transform;

            var created = new GameObject(name);
            created.transform.position = position;
            return created.transform;
        }

        private static Transform EnsureChild(Transform parent, string name,
            Vector3 worldPosition, Quaternion rotation)
        {
            Transform existing = parent.Find(name);
            if (existing == null)
            {
                var created = new GameObject(name);
                created.transform.SetParent(parent, false);
                existing = created.transform;
            }
            existing.position = worldPosition;
            existing.rotation = rotation;
            return existing;
        }

        /// <summary>손잡이 형태 — 벽판 + 각진 손잡이. 조준 콜라이더는 루트에 하나만 둔다.</summary>
        private static void EnsureLeverShape(Transform root)
        {
            if (root.Find("Plate") == null)
            {
                GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plate.name = "Plate";
                plate.transform.SetParent(root, false);
                plate.transform.localPosition = Vector3.zero;
                plate.transform.localScale = new Vector3(0.26f, 0.34f, 0.05f);
                StripCollider(plate);
                Paint(plate, new Color(0.20f, 0.21f, 0.20f));
            }

            if (root.Find("Handle") == null)
            {
                GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                handle.name = "Handle";
                handle.transform.SetParent(root, false);
                handle.transform.localPosition = new Vector3(0f, -0.04f, -0.10f);
                handle.transform.localRotation = Quaternion.Euler(28f, 0f, 0f);
                handle.transform.localScale = new Vector3(0.07f, 0.07f, 0.22f);
                StripCollider(handle);
                Paint(handle, new Color(0.46f, 0.42f, 0.30f));
            }

            var collider = root.GetComponent<BoxCollider>();
            if (collider == null) collider = root.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, -0.02f, -0.06f);
            collider.size = new Vector3(0.30f, 0.38f, 0.22f);
        }

        private static void StripCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
        }

        private static void Paint(GameObject target, Color color)
        {
            var renderer = target.GetComponent<MeshRenderer>();
            if (renderer == null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "TenFloor_" + ColorUtility.ToHtmlStringRGB(color) };
            material.color = color;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.06f);
            renderer.sharedMaterial = material;
        }
    }
}
