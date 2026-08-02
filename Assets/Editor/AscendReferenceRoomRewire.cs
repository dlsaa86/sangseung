using System.Text;
using Ascend.Prototype.Art;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ascend.Prototype.EditorTools
{
    /// <summary>
    /// 기존 게임플레이 오브젝트를 <see cref="AscendReferenceRoom"/> 이 세운 새 형상 위로
    /// **옮긴다.** 형상 조립과 분리된 이유는 두 작업의 실패 방식이 다르기 때문이다 —
    /// 조립이 틀리면 방이 이상하게 생기고, 배선이 틀리면 **게임이 조용히 죽는다.**
    ///
    /// ## 핵심 전제: 배선은 트랜스폼 이동에서 살아남는다
    ///
    /// `SpinBoardView._cells` · `InteractableLever` · `InstrumentPanelView` 는 서로를
    /// **fileID 로** 참조한다. 오브젝트를 옮기거나 부모를 바꿔도 그 참조는 끊기지 않는다.
    /// 그래서 새 방을 세우면서 기존 시스템을 다시 만들 이유가 없다 — 옮기기만 하면 된다.
    ///
    /// **이것이 이 작업 전체의 위험을 결정한다.** 다시 만들었다면 268회 스핀을 도는
    /// PlayMode 스위트와 1900건의 단정을 전부 다시 맞춰야 했다.
    ///
    /// ## 이 파일이 하지 않는 것
    ///
    /// - **아무것도 지우지 않는다.** 구 형상은 <see cref="Park"/> 로 비활성화만 한다.
    ///   씬 오브젝트 삭제는 되돌릴 수 없고, 이 저장소는 직렬화 에셋 손상 이력이 있다.
    /// - 컴포넌트를 새로 붙이지 않는다. 옮기고, 참조를 다시 가리킬 뿐이다.
    /// </summary>
    public static class AscendReferenceRoomRewire
    {
        private static StringBuilder _report;

        [MenuItem("Ascend/Room/Rewire Gameplay Into Reference Room")]
        public static void Rewire()
        {
            if (EditorApplication.isPlaying)
            { Debug.LogError("[상승] Play 모드에서는 씬을 고치지 않는다."); return; }

            GameObject root = GameObject.Find(AscendReferenceRoom.RootName);
            if (root == null)
            { Debug.LogError($"[상승] `{AscendReferenceRoom.RootName}` 이 없다 — 먼저 Build Reference Room 을 돌린다."); return; }

            _report = new StringBuilder("[상승] 게임플레이 배선 이전\n");

            MoveBoardCells(root);
            MoveLever(root);
            MovePowerReadout(root);
            MoveFloorIndicator(root);
            RelocateInteractables(root);
            WireImpact(root);
            RepointRiskLighting(root);
            ParkLegacyVisuals();

            Scene();
            Debug.Log(_report.ToString());
        }

        private static void Scene()
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        // ══════════════════════════════════════════════════════════════════════
        //  3×3 결과판 — 아홉 칸을 아홉 관찰창 안으로
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// `SpinBoardView._cells` 가 가리키는 아홉 트랜스폼을 새 관찰창 안으로 옮긴다.
        ///
        /// **인덱스 규약이 이 함수의 전부다.** `SpinBoard.Index(column, row)` 와
        /// <see cref="ReferenceRoomSpec.WindowCenter"/> 가 같은 순서를 써야 결과가
        /// 뒤집히지 않는다. 구 씬은 `Tube_{col}/Cell_{row}` 였고 행 0 이 **위**였다
        /// (Cell_0 이 y=+0.44 로 가장 높다). `WindowCenter` 도 행 0 이 위다 —
        /// 그 일치를 <see cref="Ascend.Prototype.Art.Tests.ReferenceRoomSpecTests"/> 가
        /// 이미 단정으로 고정해 두었다.
        ///
        /// 뒤집힘은 판정이 아니라 **표시만** 틀리므로 테스트가 잡지 못한다.
        /// 그래서 여기서 좌표를 찍어 보고서에 남긴다.
        /// </summary>
        private static void MoveBoardCells(GameObject root)
        {
            Transform grid = FindDeep(root.transform, "WindowGrid");
            if (grid == null) { _report.AppendLine("  ⚠ WindowGrid 를 찾지 못했다 — 결과판 이전 실패"); return; }

            int moved = 0;
            for (int col = 0; col < 3; col++)
            {
                for (int row = 0; row < 3; row++)
                {
                    Transform cell = GameObject.Find($"TubesRoot/Tube_{col}/Cell_{row}")?.transform;
                    Transform module = grid.Find($"{AscendReferenceRoom.WindowModuleName}_{col}{row}");
                    if (cell == null || module == null)
                    {
                        _report.AppendLine($"  ⚠ 칸 ({col},{row}) — cell={(cell == null ? "없음" : "있음")} module={(module == null ? "없음" : "있음")}");
                        continue;
                    }

                    Undo.SetTransformParent(cell, module, "rewire board cell");
                    // 유리 뒤, 우물 앞. 심볼이 유리에 파묻히거나 뚫고 나오지 않는 자리다.
                    cell.localPosition = new Vector3(0f, 0f, -0.028f);
                    cell.localRotation = Quaternion.identity;
                    // 구 심볼은 통관 크기(0.15~0.17)에 맞춰 저작됐다. 새 유리 지름은
                    // 0.32 라 그대로 두면 창을 꽉 채운다 — 명세 §4 「유리 전체를 밝히지
                    // 말고 중앙에 작은 불규칙한 빛」에 어긋난다.
                    cell.localScale = Vector3.one * 0.62f;
                    EditorUtility.SetDirty(cell);
                    moved++;
                }
            }
            _report.AppendLine($"  결과판 — {moved}/9 칸을 관찰창 안으로 (행 0 = 위, 열 0 = 왼쪽)");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  레버
        // ══════════════════════════════════════════════════════════════════════

        private static void MoveLever(GameObject root)
        {
            Transform handle = FindDeep(root.transform, AscendReferenceRoom.LeverHandleName);
            Transform legacy = GameObject.Find("GrayboxWorld/Car/OverharvestLever")?.transform;
            if (handle == null || legacy == null)
            { _report.AppendLine($"  ⚠ 레버 이전 실패 — handle={(handle == null ? "없음" : "있음")} legacy={(legacy == null ? "없음" : "있음")}"); return; }

            Undo.RecordObject(legacy, "rewire lever");
            // 상호작용 판정과 물리(`LeverPhysics`)는 이 오브젝트에 붙어 있다.
            // 형상만 새 손잡이가 맡고, **소유권은 옮기지 않는다** — 한 트랜스폼에
            // 두 주인을 두면 매 프레임 싸우고 그건 물리가 아니라 떨림으로 보인다
            // (`LeverPhysics` 주석이 같은 함정을 이미 기록해 뒀다).
            legacy.position = handle.position;
            legacy.rotation = handle.rotation;
            EditorUtility.SetDirty(legacy);

            // ── 🔴 구 레버 **몸통**을 숨긴다 ────────────────────────────────
            //
            // 상호작용체를 새 회전축으로 옮기면 **그 자식 그레이박스가 통째로 따라온다.**
            // 실측 z — 구 `CoverPlate` 1.70 · `Housing` 1.99 · `HandleShaft` 2.04 인데
            // 새 `Grip` 은 1.81, `Arm` 은 2.14 다. 즉 **새로 만든 디테일이 구 덩어리
            // 뒤에 가려 화면에서 안 보인다.** 사용자가 「모델링 디테일이 왜 반영 안
            // 됐냐」고 물은 것이 이것이다 — 반영은 됐고 가려져 있었다.
            //
            // 오브젝트를 끄지 않고 **렌더러만** 끈다. 끄면
            // `FindAnyObjectByType<InteractableOverharvestLever>()` 가 못 찾아
            // 10층 검증이 첫 줄에서 죽는다 (이미 한 번 그렇게 죽였다).
            //
            // ⚠ **덮개(`CoverPivot`)와 잠금등(`LockLight`)은 남긴다.**
            // `OverharvestUnlockEffect` 가 그것들을 움직여 「2단 구간이 열렸다」를
            // 표현하고, 그건 `D-20260802-10` 의 통합 레버가 요구하는 바로 그 신호다.
            // 형상이 겹치는 것과 연출이 사는 것은 다른 문제다.
            string[] duplicateBody = { "Housing", "HandleShaft", "HandleGrip", "WarningStripe", "WarningStripe_Upper" };
            int hiddenBody = 0;
            foreach (Renderer r in legacy.GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponent<TMPro.TMP_Text>() != null) continue;
                bool dup = false;
                for (int i = 0; i < duplicateBody.Length; i++)
                    if (r.gameObject.name == duplicateBody[i]) { dup = true; break; }
                if (!dup || !r.enabled) continue;
                r.enabled = false;
                EditorUtility.SetDirty(r);
                hiddenBody++;
            }

            _report.AppendLine($"  레버 — 상호작용체를 새 회전축 ({handle.position.x:F2}, {handle.position.y:F2}, {handle.position.z:F2}) 으로 " +
                               $"(명세 회전축 y={ReferenceRoomSpec.LeverPivotY}) " +
                               $"· 구 몸통 렌더러 {hiddenBody}개 비표시 (덮개·잠금등은 남긴다)");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  전력 표시기
        // ══════════════════════════════════════════════════════════════════════

        private static void MovePowerReadout(GameObject root)
        {
            Transform anchor = FindDeep(root.transform, "Anchor_Value");
            Transform panel = GameObject.Find("GrayboxWorld/Car/InstrumentPanel")?.transform;
            if (anchor == null || panel == null)
            { _report.AppendLine($"  ⚠ 계기판 이전 실패 — anchor={(anchor == null ? "없음" : "있음")} panel={(panel == null ? "없음" : "있음")}"); return; }

            Undo.RecordObject(panel, "rewire instrument panel");
            panel.position = anchor.position;
            // 구 계기판은 −45° 로 비스듬했다. 명세 §6 은 벽에 붙은 정면 표시기를
            // 요구하므로 실내(−Z)를 정면으로 세운다.
            panel.rotation = Quaternion.Euler(0f, 180f, 0f);
            // 구 패널은 0.62 로 눌려 있었다. 새 표시기 안에 들어가게 정규화한다.
            panel.localScale = Vector3.one * 0.5f;
            EditorUtility.SetDirty(panel);

            _report.AppendLine($"  전력 표시기 — 계기판을 Anchor_Value ({anchor.position.x:F2}, {anchor.position.y:F2}, {anchor.position.z:F2}) 로 " +
                               $"· 읽기 거리 요구 {ReferenceRoomSpec.PowerMeterReadDistance}m");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  층수 표시기
        // ══════════════════════════════════════════════════════════════════════

        private static void MoveFloorIndicator(GameObject root)
        {
            Transform panel = FindDeep(root.transform, "FloorIndicatorPanel");
            Transform legacy = GameObject.Find("GrayboxWorld/Car/FloorIndicator")?.transform;
            if (panel == null || legacy == null)
            { _report.AppendLine($"  ⚠ 층수 표시기 이전 실패"); return; }

            Undo.RecordObject(legacy, "rewire floor indicator");
            Transform readout = panel.Find("Readout") ?? panel;
            legacy.position = readout.position + panel.forward * -0.012f;
            legacy.rotation = panel.rotation;
            legacy.localScale = Vector3.one * 0.5f;
            EditorUtility.SetDirty(legacy);

            _report.AppendLine($"  층수 표시기 — 가위문 위 y={ReferenceRoomSpec.FloorIndicatorCenterY} " +
                               $"({ReferenceRoomSpec.FloorIndicatorWidth} × {ReferenceRoomSpec.FloorIndicatorHeight})");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  상호작용체를 새 방 안으로 — **끄지 않고 옮긴다**
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 게임플레이 컴포넌트를 든 구 오브젝트를 새 방의 실제 자리로 옮긴다.
        ///
        /// **끄면 안 되는 이유가 하네스에 있다.** `TenFloorAutoPilot` 은
        /// <c>FindAnyObjectByType&lt;T&gt;()</c> 를 인자 없이 부르고, 그 오버로드는
        /// 비활성 오브젝트를 **찾지 않는다.** 하나라도 꺼져 있으면 10층 검증이
        /// 첫 줄에서 죽는다 — 실제로 죽였다.
        ///
        /// 자리는 명세가 직접 정해 주지 않는 것들이라 §1 의 제약(중앙 바닥을 비운다)과
        /// §13 (추가 콘솔·기계를 늘리지 않는다)만 지켜 **벽면에** 붙인다.
        /// 형상은 그레이박스 그대로다 — 이 배치의 목적은 배치이지 조형이 아니다.
        /// </summary>
        private static void RelocateInteractables(GameObject root)
        {
            // 장치 왼쪽에 남는 후면 벽 (x −2.00 ~ −1.40) 이 유일하게 빈 벽면이다.
            float freeWallX = (ReferenceRoomSpec.WallLeftX + ReferenceRoomSpec.MachineLeftX) * 0.5f;

            Place("GrayboxWorld/Car/Console",
                  new Vector3(ReferenceRoomSpec.LeverColumnCenterX,
                              ReferenceRoomSpec.LeverPivotY,
                              ReferenceRoomSpec.WallRearZ - ReferenceRoomSpec.LeverColumnDepth - 0.05f),
                  Quaternion.Euler(0f, 180f, 0f), 0.5f, "실행 레버(InteractableLever)");

            // ⚠ **후면 벽에 두지 않는다.** 첫 판본이 장치 왼쪽(freeWallX)에 뒀더니
            // 밝은 판이 통관 장치를 정면에서 가렸다 — 명세 §2 의 시각적 우선순위 1 이
            // 「후면의 붉게 빛나는 3×3 통관 장치」이므로 그것을 가리는 배치는 실패다.
            // 우벽 앞쪽(선반보다 앞)으로 보내 화각 가장자리에 둔다.
            Place("GrayboxWorld/Car/ContractPanel",
                  new Vector3(ReferenceRoomSpec.WallRightX - 0.06f, 1.45f,
                              ReferenceRoomSpec.ShelfCenterZ - ReferenceRoomSpec.ShelfLength * 0.5f - 0.45f),
                  Quaternion.Euler(0f, -90f, 0f), 0.75f, "계약 패널(InteractableContractPanel)");

            // 전력 탱크는 선반 **아래 단**에 둔다 — 명세 §7 은 물건을 선반 위·아래에만
            // 두라고 하고 §1 은 중앙 바닥을 비우라고 한다. 둘을 동시에 만족하는 자리다.
            Place("GrayboxWorld/Car/PowerTank",
                  new Vector3(ReferenceRoomSpec.ShelfCenterX,
                              ReferenceRoomSpec.ShelfLowerHeight + 0.30f,
                              ReferenceRoomSpec.ShelfCenterZ - 1.05f),
                  Quaternion.identity, 1f, "전력 탱크(InteractablePowerTank)");

            Place("GrayboxWorld/Car/DoorControl",
                  new Vector3(ReferenceRoomSpec.WallLeftX + ReferenceRoomSpec.GateProtrusion + 0.05f,
                              1.20f,
                              ReferenceRoomSpec.GateOpeningWidth * 0.5f + 0.22f),
                  Quaternion.Euler(0f, 90f, 0f), 1f, "문 제어(InteractableDoorControl)");

            // 사고 기록기도 후면 벽에서 뺀다 — 같은 이유다.
            Place("GrayboxWorld/Car/AccidentPrinter",
                  new Vector3(ReferenceRoomSpec.WallLeftX + 0.10f, 1.30f,
                              ReferenceRoomSpec.WallFrontZ + 0.55f),
                  Quaternion.Euler(0f, 90f, 0f), 0.8f, "사고 기록기");
        }

        private static void Place(string path, Vector3 pos, Quaternion rot, float scale, string what)
        {
            GameObject go = GameObject.Find(path);
            if (go == null)
            {
                // 비활성이면 `GameObject.Find` 가 못 찾는다. 전수 탐색으로 한 번 더 본다 —
                // 직전 실행이 껐을 수 있고, 껐다면 **다시 켜는 것**이 이 함수의 일이다.
                string leaf = path.Substring(path.LastIndexOf('/') + 1);
                foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                    if (t.name == leaf) { go = t.gameObject; break; }
            }
            if (go == null) { _report.AppendLine($"  ⚠ {what} — `{path}` 를 찾지 못했다"); return; }

            Undo.RecordObject(go.transform, "relocate interactable");
            if (!go.activeSelf) go.SetActive(true);       // 직전 판본이 껐다면 되살린다
            go.transform.SetPositionAndRotation(pos, rot);
            if (scale > 0f) go.transform.localScale = Vector3.one * scale;
            EditorUtility.SetDirty(go);
            _report.AppendLine($"  {what} → ({pos.x:F2}, {pos.y:F2}, {pos.z:F2}) · 활성 {go.activeInHierarchy}");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  타격감 — 레버를 당기면 방이 반응한다
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// <see cref="View.MachineImpactView"/> 를 세우고 네 채널을 물린다.
        ///
        /// **열 단위로 나눠 묶는 것이 요점이다.** 아홉 칸을 한 배열로 주면 동시에
        /// 켜져 무엇이 일어났는지 읽을 시간이 없다 — 사용자가 지적한 「정확히 어떤
        /// 일이 일어나는지 인지가 안 된다」가 그것이다.
        ///
        /// 발동은 코드가 아니라 **배선**으로 건다. `LeverPhysics.onBottomedOut` 은
        /// 손잡이가 최저점에 닿은 **그 순간** 한 번만 울리므로, 타격의 정의와 정확히 같다.
        /// </summary>
        private static void WireImpact(GameObject root)
        {
            Transform lamp = root.transform.Find(AscendReferenceRoom.CeilingLampName);
            Transform warn = root.transform.Find(AscendReferenceRoom.WarningLampName);
            Transform grid = FindDeep(root.transform, "WindowGrid");
            if (grid == null) { _report.AppendLine("  ⚠ WindowGrid 없음 — 타격 연출 미배선"); return; }

            var impact = root.GetComponent<View.MachineImpactView>();
            if (impact == null) impact = root.AddComponent<View.MachineImpactView>();

            // ── 릴 ──
            // 사용자 요청: 「레버를 내리면 통관을 통해서 구슬이 위에서 아래로
            // 흘렀다가 멈추는(슬롯머신처럼) 연출」.
            //
            // ⚠ **형상이 아니라 움직임만 릴이다.** 명세 §13 의 「슬롯머신처럼 보이는
            // 장식 금지」는 형상(세로 릴 창·열로 뭉치는 배치)에 걸린 것이고, 창은
            // 여전히 등방 3×3 격자다. 구슬은 **각자의 원형 창 안에서만** 흐른다.
            var reel = root.GetComponent<View.SoulReelView>();
            if (reel == null) reel = root.AddComponent<View.SoulReelView>();

            var rso2 = new SerializedObject(reel);
            SerializedProperty souls = rso2.FindProperty("_souls");
            if (souls != null)
            {
                souls.arraySize = View.SoulReelView.Columns * View.SoulReelView.Rows;
                int found = 0;
                for (int col = 0; col < View.SoulReelView.Columns; col++)
                    for (int row = 0; row < View.SoulReelView.Rows; row++)
                    {
                        Transform module = grid.Find($"{AscendReferenceRoom.WindowModuleName}_{col}{row}");
                        Transform soul = module != null ? module.Find(AscendReferenceRoom.SoulName) : null;
                        // 인덱스 규약은 `SoulReelView.Index` 와 **같아야** 한다 —
                        // 열 우선. 어긋나면 엉뚱한 열이 멈춘다.
                        souls.GetArrayElementAtIndex(col * View.SoulReelView.Rows + row)
                             .objectReferenceValue = soul;
                        if (soul != null) found++;
                    }
                Set(rso2, "_impact", impact);
                rso2.ApplyModifiedProperties();
                EditorUtility.SetDirty(reel);
                _report.AppendLine($"  릴 연출 — 구슬 {found}/9 배선 · 총 {reel.TotalDuration:F2}초 " +
                                   $"(열 정지 {reel.StopTimeOf(0):F2} / {reel.StopTimeOf(1):F2} / {reel.StopTimeOf(2):F2}초)");
            }

            var so = new SerializedObject(impact);
            Set(so, "_keyLight", lamp != null ? lamp.GetComponentInChildren<Light>(true) : null);
            Set(so, "_warningLens", warn != null ? FindDeep(warn, "Lens")?.GetComponent<Renderer>() : null);
            // 흔들 대상은 **물체**여야 한다. 카메라를 흔들면 `VISUAL_SPEC` §8 의
            // 「과도한 카메라 흔들림을 피한다」에 걸리고, 무엇보다 공간이 아니라
            // 화면이 흔들린 것으로 읽힌다.
            Set(so, "_shakeTarget", lamp);

            for (int col = 0; col < 3; col++)
            {
                SerializedProperty arr = so.FindProperty("_column" + col);
                if (arr == null) continue;
                arr.arraySize = 3;
                for (int row = 0; row < 3; row++)
                {
                    Transform module = grid.Find($"{AscendReferenceRoom.WindowModuleName}_{col}{row}");
                    Transform soul = module != null ? module.Find(AscendReferenceRoom.SoulName) : null;
                    // ⚠ **핵만 점화한다. 껍질은 건드리지 않는다.**
                    //
                    // `MachineImpactView` 는 배열 전체에 **같은** `MaterialPropertyBlock`
                    // 을 쓴다. 껍질까지 넣으면 두 겹이 같은 밝기가 되어, 어둡고 반투명한
                    // 껍질 안에 뜨거운 핵이 있다는 구조가 발동하는 순간 무너진다 —
                    // 없애려던 「균일한 분홍 덩어리」로 정확히 되돌아간다.
                    //
                    // 핵만 밝아지면 껍질을 통과해 번지므로 밝기 대비가 오히려 커진다.
                    Transform core = soul != null ? soul.Find("Core") : null;
                    Transform target = core != null ? core : soul;
                    arr.GetArrayElementAtIndex(row).objectReferenceValue =
                        target != null ? target.GetComponent<Renderer>() : null;
                }
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(impact);

            // ── 발동 배선 ──────────────────────────────────────────────────
            //
            // 코드가 아니라 **배선**으로 건다. 그래야 소유 경로를 넘지 않고,
            // 인스펙터에서 무엇이 무엇을 부르는지 눈으로 확인된다.
            //
            // 걸어야 하는 사슬은 셋이다. 하나라도 빠지면 조용히 아무 일도
            // 일어나지 않으므로 **각각을 개수로 보고한다.**
            //   ① 사람 입력  InteractableLever.onPulled  → 레버가 움직인다
            //   ② 잠긴 입력  InteractableLever.onBlocked → 핀에 부딪혀 튕긴다
            //   ③ 걸린 순간  LeverStateMachine.onLatched → 장치가 반응한다
            int pulled = 0, blocked = 0, latched = 0;

            foreach (View.LeverStateMachine fsm in
                     Object.FindObjectsByType<View.LeverStateMachine>(FindObjectsInactive.Include))
            {
                var fso = new SerializedObject(fsm);
                SerializedProperty latch = Calls(fso, "onLatched");
                if (latch != null)
                {
                    // 레버가 걸린 그 순간이 「당겼다」의 정의다. 타격과 릴이 같이 돈다.
                    Hook(latch, impact, typeof(View.MachineImpactView), "Strike");
                    Hook(latch, reel, typeof(View.SoulReelView), "Spin");
                    latched++;
                }
                SerializedProperty denied = Calls(fso, "onLockBlocked");
                if (denied != null)
                    Hook(denied, impact, typeof(View.MachineImpactView), "Deny");
                fso.ApplyModifiedProperties();
                EditorUtility.SetDirty(fsm);

                // 사람 입력 → 레버. 같은 기둥에 있는 `InteractableLever` 를 찾는다.
                // **없으면 레버는 영원히 움직이지 않는다** — 실측으로 지금까지
                // 그 상태였다(`Pull()` 의 런타임 호출자 0개).
                foreach (Player.InteractableLever il in
                         Object.FindObjectsByType<Player.InteractableLever>(FindObjectsInactive.Include))
                {
                    var iso = new SerializedObject(il);
                    SerializedProperty onPulled = Calls(iso, "onPulled");
                    if (onPulled != null && Hook(onPulled, fsm, typeof(View.LeverStateMachine), "Pull")) pulled++;
                    SerializedProperty onBlocked = Calls(iso, "onBlocked");
                    if (onBlocked != null && Hook(onBlocked, fsm, typeof(View.LeverStateMachine), "Blocked")) blocked++;
                    iso.ApplyModifiedProperties();
                    EditorUtility.SetDirty(il);
                }
            }

            _report.AppendLine($"  타격 연출 — 전구 {(lamp != null ? "○" : "×")} · 경고등 {(warn != null ? "○" : "×")} " +
                               $"· 통관 3열 × 3");
            _report.AppendLine($"  발동 사슬 — onPulled→Pull {pulled} · onBlocked→Blocked {blocked} · onLatched→Strike/Spin {latched}");
            if (latched == 0)
                _report.AppendLine("     ⚠ `LeverStateMachine` 이 씬에 없다 — 타격이 발동하지 않는다.");
            if (pulled == 0)
                _report.AppendLine("     ⚠ `InteractableLever` 와 연결되지 않았다 — **레버가 움직이지 않는다.**");
        }

        /// <summary>UnityEvent 의 영구 호출 목록을 꺼낸다. 없으면 null.</summary>
        private static SerializedProperty Calls(SerializedObject so, string eventField)
        {
            SerializedProperty ev = so.FindProperty(eventField);
            return ev?.FindPropertyRelative("m_PersistentCalls.m_Calls");
        }

        /// <summary>
        /// 인스펙터 배선을 하나 건다. **이미 같은 대상·같은 메서드가 있으면 걸지 않는다** —
        /// 조립기는 여러 번 돌므로, 중복을 막지 않으면 돌린 횟수만큼 때린다.
        /// </summary>
        /// <returns>걸려 있게 되었으면 true(이번에 새로 걸었든, 이미 있었든).</returns>
        private static bool Hook(SerializedProperty calls, Object target, System.Type type, string method)
        {
            if (target == null) return false;
            for (int i = 0; i < calls.arraySize; i++)
            {
                SerializedProperty c = calls.GetArrayElementAtIndex(i);
                if (c.FindPropertyRelative("m_Target").objectReferenceValue == target &&
                    c.FindPropertyRelative("m_MethodName").stringValue == method)
                    return true;
            }
            calls.arraySize++;
            SerializedProperty n = calls.GetArrayElementAtIndex(calls.arraySize - 1);
            n.FindPropertyRelative("m_Target").objectReferenceValue = target;
            n.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = type.AssemblyQualifiedName;
            n.FindPropertyRelative("m_MethodName").stringValue = method;
            n.FindPropertyRelative("m_Mode").intValue = 1;        // Void
            n.FindPropertyRelative("m_CallState").intValue = 2;   // RuntimeOnly
            return true;
        }

        private static void Set(SerializedObject so, string field, Object value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p != null) p.objectReferenceValue = value;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  위험 연출이 새 전구를 본다
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// `RiskStateView` 의 `_cabinLight` · `_lampRenderer` 를 새 케이지 전구로 다시 가리킨다.
        ///
        /// **이걸 안 하면 위험 단계가 통째로 죽는다.** 구 `CabinLight` 는 비활성화된
        /// `CeilingLampRig` 아래에 있고, 비활성 광원은 아무것도 비추지 않는다.
        /// 그런데 `RiskStateView` 는 그 광원의 intensity 를 매 LateUpdate 마다 성실하게
        /// 덮어쓴다 — **오류도 경고도 나지 않는다.** 조명이 그냥 없다.
        ///
        /// 사유 필드가 `private` 이라 `SerializedObject` 로 쓴다. 리플렉션을 쓰지 않는
        /// 이유는 MCP `Unity_RunCommand` 가 `System.Reflection` 을 차단하기 때문이고,
        /// `SerializedObject` 는 Undo 와 프리팹 오버라이드도 올바르게 처리한다.
        /// </summary>
        private static void RepointRiskLighting(GameObject root)
        {
            var risk = Object.FindAnyObjectByType<Ascend.Prototype.Risk.RiskStateView>(FindObjectsInactive.Include);
            if (risk == null) { _report.AppendLine("  ⚠ RiskStateView 를 찾지 못했다 — 위험 조명 미배선"); return; }

            Transform lamp = root.transform.Find(AscendReferenceRoom.CeilingLampName);
            Light light = lamp != null ? lamp.GetComponentInChildren<Light>(true) : null;
            Transform bulb = lamp != null ? lamp.Find("Bulb") : null;
            Renderer bulbRenderer = bulb != null ? bulb.GetComponent<Renderer>() : null;

            var so = new SerializedObject(risk);
            SerializedProperty pLight = so.FindProperty("_cabinLight");
            SerializedProperty pLamp = so.FindProperty("_lampRenderer");
            SerializedProperty pSway = so.FindProperty("_swayTarget");

            if (pLight != null && light != null) pLight.objectReferenceValue = light;

            // 🔴 **이 한 줄이 없으면 플레이 모드에서만 화면이 검게 나온다.**
            //
            // `RiskStateView.ApplyLighting` 이 매 LateUpdate 마다
            // `intensity = _baseLightIntensity × LightIntensity × flicker` 를
            // **절대값으로** 쓴다. 즉 에디터에서 광원 인스펙터에 넣은 값은 플레이가
            // 시작되는 순간 버려진다.
            //
            // 기본값 1.6 은 구 캐빈(4.80 × 6.00 × 5.50, 밝은 그레이박스 재질)에서 정해진
            // 값이다. 새 방은 재질이 어둡고 셰이더의 양자화 첫 칸이 `atten < 1/steps` 를
            // 0 으로 만들기 때문에, 1.6 이면 거의 모든 표면이 0번 칸으로 떨어진다.
            //
            // **에디터 캡처가 멀쩡한데 플레이가 검은** 종류의 결함이라 캡처로는 못 잡는다.
            // 조립기가 광원에 넣는 값과 같은 값을 여기에도 넣어 둘을 묶는다.
            SerializedProperty pBase = so.FindProperty("_baseLightIntensity");
            if (pBase != null && light != null)
            {
                float before = pBase.floatValue;
                pBase.floatValue = light.intensity;
                _report.AppendLine($"     _baseLightIntensity {before:F2} → {light.intensity:F2} " +
                                   "(플레이 모드에서 광원 세기를 덮어쓰는 값)");
            }
            if (pLamp != null && bulbRenderer != null) pLamp.objectReferenceValue = bulbRenderer;
            // 흔들릴 물체도 새 등으로. 명세 §9 는 펜던트를 금지하므로 진폭은 작아야 한다.
            if (pSway != null && lamp != null) pSway.objectReferenceValue = lamp;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(risk);

            _report.AppendLine($"  위험 조명 — _cabinLight={(light != null ? light.name : "없음")} " +
                               $"_lampRenderer={(bulbRenderer != null ? bulbRenderer.name : "없음")} " +
                               $"_swayTarget={(lamp != null ? lamp.name : "없음")}");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  구 형상 파킹
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 구 형상을 **끈다.** 게임플레이를 나르는 자식(칸·상호작용체)은 이미 위에서
        /// 새 방으로 옮겼으므로, 여기서 꺼지는 것은 껍데기뿐이다.
        ///
        /// 지우지 않는 이유는 하나다 — 무엇이 아직 참조를 붙들고 있는지 확실하지 않고,
        /// 비활성화는 되돌리기가 한 줄이지만 삭제는 아니다.
        /// </summary>
        private static void ParkLegacyVisuals()
        {
            string[] paths =
            {
                "TubesRoot/Tube_0/TubeFrame", "TubesRoot/Tube_1/TubeFrame", "TubesRoot/Tube_2/TubeFrame",
                "TubesRoot/Tube_0/HarvestMarker", "TubesRoot/Tube_1/HarvestMarker", "TubesRoot/Tube_2/HarvestMarker",
                "TubesRoot/Tube_0/Divider_0", "TubesRoot/Tube_0/Divider_1",
                "TubesRoot/Tube_1/Divider_0", "TubesRoot/Tube_1/Divider_1",
                "TubesRoot/Tube_2/Divider_0", "TubesRoot/Tube_2/Divider_1",
                "TubesRoot/PortholeWall",
                "GrayboxWorld/Car/TankStand",
                "GrayboxWorld/Car/TankTick_0", "GrayboxWorld/Car/TankTick_1",
                "GrayboxWorld/Car/TankTick_2", "GrayboxWorld/Car/TankTick_3",
                "GrayboxWorld/Car/ContractPlaque_0", "GrayboxWorld/Car/ContractPlaque_1", "GrayboxWorld/Car/ContractPlaque_2",
                "GrayboxWorld/Car/Door",
            };

            // 🔴 **`Console` · `PowerTank` · `ContractPanel` 은 여기 없다.** 첫 판본은
            // 셋을 파킹했고 10층 PlayMode 가 **첫 검사에서 즉시 죽었다** —
            //   `FAIL 씬 배선 — lever=False panel=False tank=False`
            //
            // 이유: 하네스가 `FindAnyObjectByType<T>()` 를 인자 없이 부르고, 그 오버로드는
            // **비활성 오브젝트를 찾지 않는다.** 셋은 각각 `InteractableLever` ·
            // `InteractablePowerTank` · `InteractableContractPanel` 을 들고 있다.
            //
            // 이 함수의 첫 주석이 「꺼지는 것은 껍데기뿐」이라고 적고 있었는데
            // **그 진술이 이 셋에 대해 거짓이었다.** 껍데기와 게임플레이를 눈으로
            // 갈랐고, 눈은 컴포넌트를 못 본다. 그래서 지금은 파킹 목록을 만들 때
            // `RelocateInteractables` 가 먼저 돌아 상호작용체를 새 방으로 **옮긴다** —
            // 끄는 대신 옮기면 이 실패가 원리적으로 불가능해진다.

            int off = 0, missing = 0;
            foreach (string p in paths)
            {
                GameObject go = GameObject.Find(p);
                if (go == null) { missing++; continue; }
                Undo.RecordObject(go, "park legacy visual");
                go.SetActive(false);
                EditorUtility.SetDirty(go);
                off++;
            }
            _report.AppendLine($"  구 형상 파킹 — 비활성 {off}개 · 이미 없음 {missing}개 (삭제하지 않았다)");
        }

        // ══════════════════════════════════════════════════════════════════════

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeep(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
