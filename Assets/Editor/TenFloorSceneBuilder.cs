using System.Collections.Generic;
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

            // ── 4b. 계약 미리보기 줄 ────────────────────────────────────────
            //
            // 계약 장치에는 색만 바뀌는 명판 세 장뿐이었고 수치가 어디에도 없었다.
            // `visual-criteria` B-4.11은 "출현률↑·정화 보상↑·잔류 대가↑가 **함께**
            // 제시되는가 — 대가가 작게 적혀 있으면 그건 선택이 아니라 함정"을 요구한다.
            //
            // **기존 라벨을 복제한다.** 방향과 크기를 직접 계산하면 또 틀린다 —
            // 층수 표시등에서 회전을 두 번 잘못 짚었다. 이미 올바르게 보이는 형제를
            // 그대로 베끼는 것이 추론보다 안전하다.
            Transform panelRoot = car.Find("InstrumentPanel");
            Transform sibling = panelRoot != null ? panelRoot.Find("PowerLabel") : null;
            if (panelRoot == null || sibling == null)
            {
                report.AppendLine("  ⚠ InstrumentPanel/PowerLabel 없음 — 계약 줄 생략");
            }
            else
            {
                var source = sibling.GetComponent<TMPro.TextMeshPro>();

                // 계기판 한 줄에 계약 조건을 몰아넣으려 했지만 실패했다. 판(PanelBack)은
                // world y 1.28~1.83 뿐이고 그 안은 층·전력·상태·게이지가 이미 채운다.
                // 위로 밀면 판 밖 벽으로 나가 문틀 모서리에 잘리고("음" 한 글자만 남았다),
                // 아래로 밀면 게이지를 덮는다. 자리가 없는 곳에 글자를 넣을 수는 없다.
                //
                // 조건은 **선택자 위에** 붙인다 — 명판 세 장 옆에 나란히 두면 셋을
                // 한눈에 비교할 수 있고, 이것이 B-4.11이 실제로 요구하는 그림이다.
                Transform stale = panelRoot.Find("ContractLabel");
                if (stale != null)
                {
                    Object.DestroyImmediate(stale.gameObject);
                    report.AppendLine("  ContractLabel 제거 — 판에 자리가 없다. 명판으로 옮긴다");
                }

                var panelView = panelRoot.GetComponent<InstrumentPanelView>();
                var plaqueLabels = new TMPro.TextMeshPro[3];
                for (int i = 0; i < 3; i++)
                {
                    Transform plaque = panelRoot.Find($"ContractPlaque_{i}");
                    if (plaque == null) continue;

                    string name = $"ContractPlaqueLabel_{i}";
                    Transform label = panelRoot.Find(name);
                    if (label == null)
                    {
                        var created = new GameObject(name);
                        created.transform.SetParent(panelRoot, false);
                        created.AddComponent<TMPro.TextMeshPro>();
                        label = created.transform;
                        report.AppendLine($"  {name} 신설");
                    }

                    var tmp = label.GetComponent<TMPro.TextMeshPro>();
                    if (tmp != null && source != null)
                    {
                        tmp.font = source.font;
                        tmp.fontSize = source.fontSize * 0.50f;
                        tmp.alignment = TMPro.TextAlignmentOptions.Left;
                        tmp.color = new Color(0.90f, 0.88f, 0.80f);
                        tmp.text = "계약";
                        var r = tmp.GetComponent<RectTransform>();
                        if (r != null) r.sizeDelta = new Vector2(17f, 2.6f);
                    }

                    // 명판은 오른쪽 벽(월드 x≈0.99)에 붙어 안쪽을 본다. 라벨을 조금
                    // 안쪽으로 빼고 Y로 90° 돌려야 차 안에서 읽힌다. 회전한 뒤에는
                    // 라벨의 X축이 월드 Z가 되므로 벽 압축(0.66) 보정을 하면 안 된다.
                    //
                    // 글자를 명판 **위에** 얹었더니 미리보기 명판(노랑)에서 밝은 글자가
                    // 사라졌다 — 명판 색은 상태를 뜻하므로 배경으로 쓸 수 없다. 명판은
                    // 색 표식으로 두고 글자는 그 옆 어두운 벽에 놓는다.
                    // Y로 90° 돌면 라벨의 +X가 월드 -Z를 향하므로, 왼쪽 정렬 글자는
                    // 사각형 왼쪽 끝(+Z 쪽)에서 시작해 -Z 방향으로 자란다.
                    const float labelWidth = 17f * 0.07f;
                    label.localPosition = new Vector3(
                        1.44f,
                        plaque.localPosition.y - 0.08f,
                        -0.12f - labelWidth * 0.5f);
                    label.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    label.localScale = new Vector3(0.07f, 0.07f, 0.07f);
                    plaqueLabels[i] = tmp;
                }

                if (panelView != null)
                {
                    var panelObject = new SerializedObject(panelView);
                    SetReference(panelObject, "_contractLabel", null);
                    SerializedProperty array = panelObject.FindProperty("_plaqueLabels");
                    if (array != null)
                    {
                        array.arraySize = 3;
                        for (int i = 0; i < 3; i++)
                            array.GetArrayElementAtIndex(i).objectReferenceValue = plaqueLabels[i];
                    }
                    panelObject.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(panelView);
                    report.AppendLine("  InstrumentPanelView._plaqueLabels 배선 3장");
                }
            }

            // ── 4a. 실행 레버를 레버로 보이게 ───────────────────────────────
            //
            // 독립 감사가 두 가지를 따로 지적했다 — `02`·`04`·`15` 좌하단의
            // "정체 불명의 창백한 쐐기"와 "일반 실행 레버가 화면에서 가장 안 보이는
            // 물건"이라는 것. 좌표로 찾아보니 **같은 물체**였다.
            //
            // `ExecutionLever` 는 18° 기울어진 창백한 막대(0.62)에 돔 손잡이 하나뿐이다.
            // 맥락이 없으니 잡음으로 보이고, 레버로는 읽히지 않는다.
            // 과수확 레버는 하우징·라벨·경고띠가 있어 "가장 잘 작동하는 부분"으로
            // 통과했다 — 같은 것을 여기에도 준다.
            EnsureExecutionLeverContext(car, report);

            // ── 4b. 과부하 램프를 글자 띠 밖으로 ────────────────────────────
            //
            // 램프가 `추락 위험` 글자 한가운데를 관통했다. 위험 상태 캡처 8장에서
            // "추락 위[램프]험"으로 쪼개졌고, 램프가 켜지면 `위`를 아예 덮었다.
            //
            // 평면 좌표만 보면 안 겹친다 — 가장 긴 문구의 라벨이 x −1.04~−0.19,
            // 하우징이 −0.12~−0.01 로 0.07 m 떨어져 있다. 문제는 z 다. 램프가
            // 라벨면(z=1.38)보다 0.06~0.11 m 앞에 있어 시점에서 시차로 얹힌다.
            //
            // 글자 띠(윗변 y=1.83) 위로 올린다. 값을 **절대 위치로** 준다 —
            // 델타로 주면 빌더를 두 번 돌릴 때 누적된다. `Reproportion Elevator Car`가
            // 멱등이 아니어서 실제로 그렇게 계기판 전체가 밀린 적이 있다.
            if (panelRoot != null)
            {
                MoveLocalY(panelRoot.Find("OverloadLight"), 1.95f, report);
                MoveLocalY(panelRoot.Find("OverloadHousing"), 1.95f, report);
            }

            // ── 4c. 월드 글자를 단면으로 ────────────────────────────────────
            MakeWorldTextSingleSided(report);

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
            // 핸드레일은 **차갑게** 둔다. 처음에 따뜻한 회색(0.325,0.318,0.286)으로 칠했더니
            // 블라인드 평가자가 "레버 바로 아래 핸드레일이 같은 갈색이라 조작 장치와 벽면
            // 부속이 한 난색 덩어리로 묶인다"고 잡아냈다 — `visual-criteria` B-1.1의
            // 실패 조건 그대로다. 벽면 부속은 벽 쪽 계열로, 난색은 조작 장치에만 남긴다.
            Tint(car, "Handrail_R",       new Color(0.298f, 0.318f, 0.302f), report);
            Tint(car, "Handrail_B",       new Color(0.298f, 0.318f, 0.302f), report);
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

        /// <summary>
        /// 월드 글자가 좌우로 뒤집혀 보인다는 지적을 두 번의 감사에서 받았다.
        /// 처음에는 회전이 틀린 줄 알고 라벨의 euler 를 뒤졌지만 전부 (0,0,0)이었다.
        ///
        /// 원인은 회전이 아니라 **재질**이다. TMP 기본 재질은 `_CullMode = 0`(Off)이라
        /// 글자의 뒷면까지 그린다. 뒷면은 정의상 거울상이므로, 라벨을 뒤에서 보는
        /// 시점(문지방에서 안쪽을 내려다보는 화물칸 시점 등)에서는 반드시 뒤집혀 보인다.
        /// 캡처 각도를 바꿔 가리는 건 증상만 숨기는 것이다 — 플레이어도 걸어가면 본다.
        ///
        /// `_CullMode = 2`(Back)로 두면 뒷면은 그리지 않는다. 뒤에서 보면 안 보일 뿐
        /// 뒤집힌 글자는 나오지 않는다. 폰트 기본 재질을 직접 고치면 이 폰트를 쓰는
        /// 모든 것에 번지므로 **변형 에셋을 만들어** 씬의 월드 라벨에만 물린다.
        /// </summary>
        private static void MakeWorldTextSingleSided(StringBuilder report)
        {
            const string dir = "Assets/Prototype_Elevator/Materials";
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets/Prototype_Elevator", "Materials");

            var made = new Dictionary<Material, Material>();
            int applied = 0;

            foreach (TMPro.TextMeshPro text in Object.FindObjectsByType<TMPro.TextMeshPro>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Material shared = text.fontSharedMaterial;
                if (shared == null || !shared.HasProperty("_CullMode")) continue;
                if (Mathf.Approximately(shared.GetFloat("_CullMode"), 2f)) continue;

                if (!made.TryGetValue(shared, out Material single))
                {
                    string path = $"{dir}/{shared.name} SingleSided.mat";
                    single = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (single == null)
                    {
                        single = new Material(shared) { name = shared.name + " SingleSided" };
                        AssetDatabase.CreateAsset(single, path);
                        report.AppendLine($"  {System.IO.Path.GetFileName(path)} 신설");
                    }
                    single.CopyPropertiesFromMaterial(shared);
                    single.SetFloat("_CullMode", 2f);   // Back — 뒷면(거울상)을 그리지 않는다
                    EditorUtility.SetDirty(single);
                    made[shared] = single;
                }

                text.fontSharedMaterial = single;
                EditorUtility.SetDirty(text);
                applied++;
            }

            AssetDatabase.SaveAssets();
            report.AppendLine($"  월드 글자 단면 처리 {applied}개 (재질 {made.Count}종)");
        }

        /// <summary>
        /// 실행 레버에 배경판과 라벨을 붙여 "조작하는 물건"으로 읽히게 한다.
        /// 값은 전부 절대값이라 여러 번 돌려도 같다.
        /// </summary>
        private static void EnsureExecutionLeverContext(Transform car, StringBuilder report)
        {
            Transform console = car != null ? car.Find("Console") : null;
            Transform lever = console != null ? console.Find("ExecutionLever") : null;
            if (lever == null)
            {
                report.AppendLine("  ⚠ Console/ExecutionLever 없음 — 실행 레버 맥락 생략");
                return;
            }

            // 배경판 — 창백한 레버가 어두운 판 위에 서면 형태가 드러난다.
            // 레버는 월드 x≈-1.03 에서 안쪽(+X)을 향한다. 판은 그보다 바깥에 둔다.
            //
            // 높이는 **라벨까지 덮도록** 잡는다. 처음엔 0.66(윗변 y=1.55)이었는데
            // 라벨 rect가 y 1.544~1.656 이라 글자가 판 밖 맨 벽으로 삐져나갔다 —
            // "어두운 판 위의 밝은 글자"가 절반만 성립했다. 윗변을 1.70으로 올린다.
            // 아랫변 0.89는 ConsoleSlab 윗면(0.875) 바로 위라 더 못 내린다.
            // 위쪽은 조사로 확인했다: y 1.75까지 WallL(안쪽 면 x=-1.20) 외에 없다.
            GameObject plate = EnsureChild(console, "ExecutionPlate", PrimitiveType.Cube);
            plate.transform.position = new Vector3(-1.16f, 1.295f, -0.86f);
            plate.transform.localRotation = Quaternion.identity;
            plate.transform.localScale = new Vector3(0.03f, 0.81f, 0.44f);
            Paint(plate, new Color(0.13f, 0.14f, 0.15f));

            // 경고띠 — 과수확 레버와 같은 어휘를 쓴다. 다만 색은 다르다:
            // 과수확은 주황(위험), 실행은 청록(정상 조작). 색 하나에 의존하지 않도록
            // 위치와 형태도 다르다.
            GameObject stripe = EnsureChild(console, "ExecutionStripe", PrimitiveType.Cube);
            stripe.transform.position = new Vector3(-1.14f, 0.94f, -0.86f);
            stripe.transform.localRotation = Quaternion.identity;
            stripe.transform.localScale = new Vector3(0.02f, 0.05f, 0.40f);
            Paint(stripe, new Color(0.36f, 0.62f, 0.55f));

            // 라벨 — 과수확에는 "과수확"이 붙어 있는데 실행 레버에는 이름이 없었다.
            // 왼쪽 벽(x≈-1.03)에서 안쪽(+X)을 향하므로 Y 로 -90° 돌린다.
            var source = Object.FindFirstObjectByType<InstrumentPanelView>(FindObjectsInactive.Include);
            Transform sibling = source != null ? source.transform.Find("PowerLabel") : null;
            var font = sibling != null ? sibling.GetComponent<TMPro.TextMeshPro>() : null;

            Transform label = console.Find("ExecutionLabel");
            if (label == null)
            {
                var made = new GameObject("ExecutionLabel");
                made.transform.SetParent(console, false);
                made.AddComponent<TMPro.TextMeshPro>();
                label = made.transform;
                report.AppendLine("  ExecutionLabel 신설");
            }

            var text = label.GetComponent<TMPro.TextMeshPro>();
            if (text != null && font != null)
            {
                text.font = font.font;
                text.fontSize = font.fontSize * 0.62f;
                text.alignment = TMPro.TextAlignmentOptions.Center;
                text.color = new Color(0.88f, 0.90f, 0.88f);
                text.text = "실행";
                var rect = text.GetComponent<RectTransform>();
                if (rect != null) rect.sizeDelta = new Vector2(5f, 1.6f);
            }
            label.position = new Vector3(-1.12f, 1.60f, -0.86f);
            label.rotation = Quaternion.Euler(0f, -90f, 0f);
            label.localScale = new Vector3(0.07f, 0.07f, 0.07f);

            report.AppendLine("  실행 레버 배경판·경고띠·라벨 배치");
        }

        private static GameObject EnsureChild(Transform parent, string name, PrimitiveType type)
        {
            Transform found = parent.Find(name);
            if (found != null) return found.gameObject;

            GameObject made = GameObject.CreatePrimitive(type);
            made.name = name;
            made.transform.SetParent(parent, true);
            Object.DestroyImmediate(made.GetComponent<Collider>());
            return made;
        }

        /// <summary>로컬 y 만 절대값으로 바꾼다. 델타가 아니라 목표값이라 여러 번 돌려도 같다.</summary>
        private static void MoveLocalY(Transform target, float y, StringBuilder report)
        {
            if (target == null) return;
            Vector3 p = target.localPosition;
            if (Mathf.Approximately(p.y, y)) return;
            target.localPosition = new Vector3(p.x, y, p.z);
            report.AppendLine($"  {target.name} y {p.y:F2} → {y:F2}");
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
