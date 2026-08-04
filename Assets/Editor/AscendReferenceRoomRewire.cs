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
            EnforceShaftOpening(root);
            RelocateExecutionLabel();
            AscentColumnBuilder.Build(root);
            ApplyInkWeights();
            WireProfiles();
            ClearGhostGlyphs();

            Scene();
            Debug.Log(_report.ToString());
        }

        // ══════════════════════════════════════════════════════════════════════
        //  승강로 개구부 — 「바깥」이 보이는 유일한 지점
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 가위문 너머가 실제 승강로인지 닫힌 판인지를 **불변식으로** 정한다.
        ///
        /// 🔴 3차 독립 평가가 잡은 회귀 (2026-08-04) — `ShaftBackdrop` 이 켜진 채로
        /// 세 라운드를 통과했다. 경위는 이렇다.
        ///   ① `Cabin/3 문 밖 통로 채택` 이 실제 통로(`CabinShaft`)를 세우고 이 판을 껐다.
        ///   ② 재질·조명 원복 사고를 복구하느라 `Room/Build` 를 다시 돌렸다.
        ///   ③ `ResetRoot` 가 `ReferenceRoom` 자식을 전부 지우고 이 판을 **활성으로**
        ///      다시 만들었다. 복구 순서에 Cabin/3 이 없었으므로 아무도 다시 끄지 않았다.
        ///   ④ 그 결과 `C_toward_gate` 가 전면 흑색 프레임이 됐다. 레퍼런스가 명시한
        ///      「그 너머 어두운 승강로에 먼 불빛 하나」가 사라졌고 대가는 0 이었다.
        ///
        /// 구현자는 A·D 의 화소 통계를 v8 과 맞춰 복구를 확인했지만 **검증표에 C 가 없었다.**
        /// 「재는 것」과 「무엇을 재는지 고르는 것」이 다르다는 증거다.
        ///
        /// 그래서 두 군데에 같은 불변식을 건다 — 조립기(`AscendReferenceRoom.BuildScissorGate`)
        /// 와 여기. 배선기는 문서화된 복구 순서의 **마지막 단계**라, 조립기를 건너뛰는
        /// 어떤 경로로 들어와도 여기서 다시 걸린다. 절대값이라 몇 번을 돌려도 같다.
        /// </summary>
        private static void EnforceShaftOpening(GameObject root)
        {
            GameObject shaft = null;
            foreach (GameObject g in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (g.name == "CabinShaft") { shaft = g; break; }

            Transform backdrop = FindDeep(root.transform, "ShaftBackdrop");
            if (backdrop == null)
            { _report.AppendLine("  ⚠ ShaftBackdrop 을 찾지 못했다 — 승강로 개구부 미확인"); return; }

            bool wantActive = shaft == null;
            bool changed = backdrop.gameObject.activeSelf != wantActive;
            if (changed)
            {
                Undo.RecordObject(backdrop.gameObject, "enforce shaft opening");
                backdrop.gameObject.SetActive(wantActive);
                EditorUtility.SetDirty(backdrop.gameObject);
            }

            // 통로가 있으면 그 안의 등도 켜져 있어야 「먼 불빛 하나」가 성립한다.
            int lamps = 0;
            if (shaft != null)
                foreach (Light l in shaft.GetComponentsInChildren<Light>(true))
                    if (l.enabled && l.gameObject.activeInHierarchy) lamps++;

            _report.AppendLine($"  승강로 개구부 — CabinShaft {(shaft == null ? "없음" : "있음")} · " +
                               $"ShaftBackdrop {(wantActive ? "활성" : "비활성")}{(changed ? " (이번에 교정)" : " (이미 정상)")} · " +
                               $"통로 광원 {lamps}개");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  실행 표찰 — VISUAL_SPEC §5 상호작용 우선순위 1위
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// `ExecutionLabel` 의 **면 방향**(m). 이 방 규약에서 월드 TMP 는 `forward` 쪽에서
        /// 읽힌다 (`RelocateContractPlaques` 주석의 `_CullMode = 2` 설명 참조).
        /// 뒤벽 앞면이 z = 2.258 이므로 13mm 앞에 붙여 「벽에 붙은 표찰」로 만든다 —
        /// 종전 z = 2.125 는 벽에서 133mm 떠 있었다.
        ///
        /// ## 🔴 2026-08-05 — 벽에서 **레버로** 옮긴다 (`UP-FIX-100`)
        ///
        /// 7차 독립 평가가 이 저장소의 오래된 착각을 잡았다 —
        /// **「레버 형상은 하나인데 라벨은 둘이다.」**
        ///
        /// 씬 실측이 그 말을 그대로 확인해 준다.
        ///
        /// | 오브젝트 | 월드 위치 |
        /// |---|---|
        /// | `ReferenceRoom/ExecutionLeverBase/ExecutionLeverHandle/Grip` | (0.76, 1.25, 1.70) |
        /// | `GrayboxWorld/Car/OverharvestLever/OverharvestLabel` | (0.76, **1.81**, 1.61) |
        /// | `GrayboxWorld/Car/Console/ExecutionLabel` (종전) | (**1.32**, 2.05, 2.25) |
        ///
        /// 즉 **유일한 레버 기구 위에 붙어 있던 라벨이 `과수확`** 이었고,
        /// 1순위 `실행` 은 560mm 떨어진 콘솔 위에 아무 기구 없이 떠 있었다.
        /// 평가자가 「플레이어가 무엇을 당길 수 있는가를 물으면 화면이 답하는 것은
        /// **3순위 레버**다」라고 적은 것이 이것이다.
        ///
        /// ## 기구를 새로 만들지 않는 이유 — 하나인 것이 설계다
        ///
        /// Notion `MASTER PRD` §2.2 가 「전력 확정 브레이크와 **2단 실행·과수확 레버**」라고
        /// 적는다. 둘은 별개 레버가 아니라 **한 레버의 두 단**이다
        /// (`MACHINE_SPEC` §4.4 「100% 달성 시 내부 잠금쇠가 풀린다」).
        /// 그러므로 고칠 것은 기구의 수가 아니라 **라벨의 자리**다 —
        /// 1단(`실행`)을 그립 바로 위에, 2단(`과수확`)을 그 위에 두면
        /// 하나의 2단 레버로 읽힌다. 아래에서 위로 «잡는 곳 → 실행 → 과수확(잠김)».
        ///
        /// z = 1.61 은 `과수확` 표찰과 **같은 판독면**이다. 둘을 같은 평면에 두어야
        /// 「한 기구의 두 단」으로 읽히고, 깊이가 갈리면 다시 두 물체가 된다.
        /// y = 1.500 은 그립 상단(1.28)과 `과수확`(1.81) 사이다.
        /// </summary>
        private static readonly Vector3 ExecutionLabelPos = new Vector3(0.758f, 1.500f, 1.610f);

        /// <summary>
        /// 표찰의 **월드 배율**(부모 배율을 흡수한 뒤의 값).
        ///
        /// 종전 0.0350 은 `fontSize 6.20 × 0.1 × 0.0350 = 0.0217 m` 의 em 이고,
        /// B 포즈에서 **글자당 가로 9.4 px** 이었다 (3차 평가 실측 9 px 과 일치).
        /// 목표는 20 px 이므로 최소 2.13배가 필요하다. 2.6배를 쓴다 —
        /// 배수가 아니라 화소로 검증하며, 캡처 후 실측을 매니페스트에 적는다.
        /// 0.0350 × 2.6 = 0.0910.
        /// </summary>
        private const float ExecutionLabelLossy = 0.0910f;

        /// <summary>
        /// 「실행」 표찰을 **읽히게** 만든다. 세 라운드째 이월된 항목이다.
        ///
        /// 실측이 원인을 둘로 갈랐다 (2026-08-04, 포즈 카메라에 글리프 사각형 투영):
        ///
        ///   포즈   글자 가로   글자 세로   facingDot
        ///   A       1.3 px      5.6 px     +0.244
        ///   B       9.4 px     13.4 px     +0.665
        ///   D       0.7 px      6.2 px     **−0.057**
        ///
        /// ① **크기** — B 에서 9.4 px. 한글 안정 판독 하한(16 px)의 60% 다.
        /// ② **면 방향** — `rotY 90` 이라 면이 ±X 를 향한다. A 는 76° 비스듬하고
        ///    **D 는 dot 이 음수, 즉 단면 재질의 뒷면이라 아예 안 그려진다.**
        ///    같은 방의 `OverharvestLabel`·계기판 다섯 줄은 전부 `rotY 0`(fwd +Z)이다.
        ///    이 표찰 하나만 90° 돌아 있었고, 그래서 가로만 유독 뭉개졌다 —
        ///    「작다」로만 보면 세 배를 키워도 D 는 여전히 0 이다.
        ///
        /// 둘 다 절대값으로 대입한다. 위치는 뒤벽에 붙여 부유감(§10 S2)도 없앤다.
        /// 하우징(`ExecutionPlate`)은 렌더러가 꺼져 있고 면이 ±X 라 켜면 모서리만
        /// 보인다 — **키우지 않고 그대로 둔다.** 배경은 뒤벽이 맡는다.
        /// </summary>
        private static void RelocateExecutionLabel()
        {
            Transform label = FindAnywhere("ExecutionLabel");
            if (label == null) { _report.AppendLine("  ⚠ ExecutionLabel 이 없다 — 실행 표찰 미교정"); return; }

            Undo.RecordObject(label, "resize execution label");
            if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
            // 회전 0 = fwd (0,0,+1). 플레이어는 −Z 쪽에 있고 A·B·D 세 포즈 전부 그렇다.
            label.SetPositionAndRotation(ExecutionLabelPos, Quaternion.identity);

            // 부모 배율을 흡수해 **월드 배율을 목표값으로** 만든다. 부모가 바뀌어도
            // 화면에서의 크기가 같아야 하므로 로컬 배율을 직접 적지 않는다.
            float parent = label.parent != null ? label.parent.lossyScale.x : 1f;
            if (Mathf.Abs(parent) < 1e-5f) parent = 1f;
            label.localScale = Vector3.one * (ExecutionLabelLossy / parent);
            EditorUtility.SetDirty(label);

            var tmp = label.GetComponent<TMPro.TMP_Text>();
            if (tmp != null)
            {
                Undo.RecordObject(tmp, "execution label text");
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
                if (tmp.text != "실행") tmp.SetText("실행");
                tmp.ForceMeshUpdate();
                EditorUtility.SetDirty(tmp);
            }

            float em = (tmp != null ? tmp.fontSize : 0f) * 0.1f * label.lossyScale.x;
            _report.AppendLine($"  실행 표찰 → ({ExecutionLabelPos.x:F3}, {ExecutionLabelPos.y:F3}, {ExecutionLabelPos.z:F3}) " +
                               $"· rotY 0 (fwd {label.forward.z:F0}Z) · 월드배율 {label.lossyScale.x:F4} · em {em:F4} m");
        }

        /// <summary>
        /// **유령 글리프를 지운다.** 3차 평가의 「전력 0 / — 분모가 없다」가 여기서 나왔다.
        ///
        /// 실측: `PowerLabel` 의 문구는 `전력 0` 이고 가시 글리프도 `전`·`력`·`0` 셋뿐인데,
        /// 화면에는 `전력 0 /` 로 슬래시가 하나 더 있었다. `RequiredLabel` 도 마찬가지로
        /// `요구 0   0 %` 가 `요구 0 / 0 %` 로 보였다.
        ///
        /// 원인은 서식도 값도 아니다 — `/` 는 `NanumGothic Atlas 2` 에 있어 별도
        /// 서브메시로 그려지는데, 문구가 `전력 N / 요구 M` 에서 `전력 N` 으로 바뀌었을 때
        /// TMP 가 **더 이상 쓰지 않는 서브메시의 지오메트리를 지우지 않았다.**
        /// 두 라벨의 서브메시가 로컬 x[3.048, 3.435] 에 쿼드 하나씩을 그대로 들고 있었고,
        /// 그 자리가 각각 「0 다음」과 「두 0 사이의 빈칸」이었다.
        ///
        /// v8 매니페스트가 이미 이 함정을 적어 뒀고 `KoreanLabelFontFix` 가 청소기를
        /// 갖고 있다. 빠져 있던 것은 **문구를 바꾼 뒤 그 청소기를 다시 부르는 일**이다.
        /// 문구를 바꾸는 곳이 여기이므로 여기서 부른다.
        /// </summary>
        private static void ClearGhostGlyphs()
        {
            var sub = new StringBuilder();
            int cleared = PrototypeEditor.KoreanLabelFontFix.ClearGhostSubMeshes(
                EditorSceneManager.GetActiveScene(), sub);
            _report.AppendLine($"  유령 서브메시 정리 {cleared}개 (0 이면 이미 깨끗 — 멱등)");
            if (cleared > 0) _report.Append(sub);
        }

        private static void Scene()
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        // ══════════════════════════════════════════════════════════════════════
        //  잉크 무게 — 「한눈에 보이는 것」 넷과 나머지를 가른다
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 계기탑 조립 + 잉크 무게 + 프로파일 배선만 따로 돌린다.
        ///
        /// 전체 <see cref="Rewire"/> 는 결과판 아홉 칸과 레버 참조까지 옮기므로,
        /// 표시 계층만 손볼 때 그것까지 돌릴 이유가 없다. 셋 다 절대값이라 멱등이다.
        /// </summary>
        [MenuItem("Ascend/Room/Rebuild Readout Layer")]
        public static void RebuildReadoutLayer()
        {
            if (EditorApplication.isPlaying)
            { Debug.LogError("[상승] Play 모드에서는 씬을 고치지 않는다."); return; }

            GameObject root = GameObject.Find(AscendReferenceRoom.RootName);
            if (root == null)
            { Debug.LogError($"[상승] `{AscendReferenceRoom.RootName}` 이 없다."); return; }

            _report = new StringBuilder("[상승] 판독 계층 재조립\n");
            AscentColumnBuilder.Build(root);
            RelocateExecutionLabel();
            SetDefaultTexts(FindAnywhere("InstrumentPanel"));
            ApplyInkWeights();
            WireProfiles();
            ClearGhostGlyphs();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log(_report.ToString());
        }

        /// <summary>
        /// 씬의 **모든 월드 TMP 색을 절대값으로 다시 쓴다.** 두 가지를 동시에 고친다.
        ///
        /// ## ① 사용자가 정한 위계 (2026-08-04)
        ///
        /// 「한눈에 보이는 건 **전력량 / 남은 스핀 횟수 / 레버 / 수확 기계** 이거면 충분해.」
        /// 나머지(계약·무게·위험도·연쇄·층)는 2순위다. **없애지 않는다** — 무게만 내린다.
        ///
        /// 5차 독립 평가가 정확히 그 반대를 지적했다: 「한 판에 층·전력·요구·무게·상태·연쇄
        /// **여섯 필드가 전부 같은 크기·같은 백색**이다 — §5 「한 화면에 모든 숫자를 동시에
        /// 강조하지 않는다」 정면 위반」. 사용자가 그 위계를 정해 줬으므로 여기서 적용한다.
        ///
        /// ## ② `UP-FIX-90` · `UP-FIX-94` — 방 안 최고 백색이 계기 텍스트였다
        ///
        /// 실측: 계기 라벨 다섯 줄이 전부 `(1,1,1)` 이었다. TMP 는 **비조명 셰이더**라
        /// 그 값이 곧 화면 값이고, 이 어두운 방(§12 mean ≈ .044)에서 유일한 순백이다.
        /// `G_gauge_face` 의 p99 **.9033**(대역 .25~.36) 이 그것이다.
        ///
        /// 그래서 **크기가 아니라 밝기**를 내린다. 크기를 줄이면 `UP-FIX-86`
        /// (글자 배율 0.3393 → 1.0000)이 되돌아간다 — 그건 하면 안 되는 축이다.
        ///
        /// ## ③ 잉크는 전부 따뜻하다 (r &gt; g &gt; b)
        ///
        /// `VISUAL_SPEC` §12 는 g/r 0.78~0.90 · b/r 0.45~0.62 를 요구한다. 순백은
        /// g/r = 1.00 이라 그 축을 **잡아당기고 있었다.** 아래 잉크는 전부 대역 안이다.
        ///
        /// 절대값이라 몇 번을 돌려도 같다.
        /// </summary>
        private static readonly (string path, Color ink, string rank)[] InkWeights =
        {
            // 1순위 — `VISUAL_SPEC` §5 「현재 사용 가능한 핵심 레버」. 방에서 가장 밝은 글자.
            ("GrayboxWorld/Car/Console/ExecutionLabel",       new Color(0.95f, 0.83f, 0.57f), "1순위 레버"),

            // 1순위 보조 — 전력량. 큰 값은 계기탑이 형상으로 나르고 여기는 정확한 수치다.
            ("GrayboxWorld/Car/InstrumentPanel/PowerLabel",    new Color(0.66f, 0.56f, 0.38f), "1순위 보조"),
            ("GrayboxWorld/Car/InstrumentPanel/RequiredLabel", new Color(0.66f, 0.56f, 0.38f), "1순위 보조"),

            // 2순위 — 층 · 위험도 · 무게 · 연쇄. `MASTER_PRD` §8.1 이 위험 단계명을
            // 「디버그와 최소 보조 표시용」이라 적으므로 이 강등은 PRD 와 어긋나지 않는다.
            ("GrayboxWorld/Car/InstrumentPanel/FloorLabel",    new Color(0.46f, 0.39f, 0.27f), "2순위"),
            ("GrayboxWorld/Car/InstrumentPanel/StatusLabel",   new Color(0.46f, 0.39f, 0.27f), "2순위"),
            ("GrayboxWorld/Car/InstrumentPanel/CascadeLabel",  new Color(0.46f, 0.39f, 0.27f), "2순위"),

            // 2순위 — 계약. 세 장이 서로 비교돼야 하므로 셋이 같은 값이다.
            ("GrayboxWorld/Car/ContractPlaqueLabel_0",         new Color(0.62f, 0.54f, 0.38f), "2순위"),
            ("GrayboxWorld/Car/ContractPlaqueLabel_1",         new Color(0.62f, 0.54f, 0.38f), "2순위"),
            ("GrayboxWorld/Car/ContractPlaqueLabel_2",         new Color(0.62f, 0.54f, 0.38f), "2순위"),

            // 3순위 — 과수확은 **잠겨 있다.** 잠긴 선택지가 사용 가능한 선택지보다
            // 밝을 이유가 없다 (`UP-FIX-89` 정보 위계 역전: B 기준 과수확 35px > 실행 24px).
            // 해제되면 `AscentColumnView` 가 같은 프레임에 붉게 살린다 — 여기 값은 잠금 상태다.
            ("GrayboxWorld/Car/OverharvestLever/OverharvestLabel", new Color(0.46f, 0.38f, 0.26f), "3순위(잠김)"),
        };

        private static void ApplyInkWeights()
        {
            _report.AppendLine("  잉크 무게 (순백 제거 · 1순위/2순위 분리) —");
            foreach (var (path, ink, rank) in InkWeights)
            {
                GameObject go = GameObject.Find(path);
                if (go == null) { _report.AppendLine($"    ⚠ 없음 — {path}"); continue; }
                var tmp = go.GetComponent<TMPro.TMP_Text>();
                if (tmp == null) { _report.AppendLine($"    ⚠ TMP 없음 — {path}"); continue; }

                float before = Luminance(tmp.color);
                if (tmp.color != ink)
                {
                    Undo.RecordObject(tmp, "ink weight");
                    tmp.color = ink;
                    tmp.ForceMeshUpdate();
                    EditorUtility.SetDirty(tmp);
                }
                _report.AppendLine($"    {Leaf(path),-22} {rank,-10} 휘도 {before:F3} → {Luminance(ink):F3} " +
                                   $"· g/r {ink.g / Mathf.Max(ink.r, 1e-4f):F3} · b/r {ink.b / Mathf.Max(ink.r, 1e-4f):F3}");
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  프로파일 배선 — 「에셋을 고쳐도 게임이 안 바뀌는」 상태를 없앤다
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// `RunSessionBehaviour` 의 프로파일 슬롯이 비어 있으면 같은 이름의 에셋을 꽂는다.
        ///
        /// ## 왜 지금 값이 같은데도 고치는가
        ///
        /// `_settlementProfile` 이 `{fileID: 0}` 이었다. 지금은 에셋(`0.15 / 0.60 / 1`)과
        /// 코드 프리셋이 **같은 값**이라 동작 차이가 없다 — 그래서 아무도 못 본다.
        /// 그러나 그 상태의 뜻은 「**`.asset` 을 고쳐도 게임이 안 바뀐다**」이고,
        /// 이 저장소가 「프로파일 8종이 여덟 세션 동안 죽어 있었다」고 기록한 사고의
        /// 재발 조건이 정확히 그것이다.
        ///
        /// `SnapshotOrDefault` 가 남기는 `Source` 문자열이 「배선했다」와 「그 값이 쓰였다」를
        /// 가르고, 자체 검증이 그것을 읽는다. 값이 같으므로 **캡처와 지표는 전부 불변**이다 —
        /// 움직이면 배선 실수다.
        /// </summary>
        private static void WireProfiles()
        {
            var run = Object.FindAnyObjectByType<Run.RunSessionBehaviour>(FindObjectsInactive.Include);
            if (run == null) { _report.AppendLine("  ⚠ RunSessionBehaviour 가 없다 — 프로파일 미배선"); return; }

            var so = new SerializedObject(run);
            (string field, string asset)[] wants =
            {
                ("_settlementProfile",   "RemainingSpinSettlementProfile"),
                ("_overharvestProfile",  "OverharvestProfile"),
                ("_weightProfile",       "WeightProfile"),
                ("_spinBalanceProfile",  "SpinBalanceProfile"),
                ("_floorCurriculum",     "FloorCurriculumProfile"),
                ("_contractProfile",     "ContractProfile"),
            };

            int wired = 0;
            foreach (var (field, asset) in wants)
            {
                SerializedProperty p = so.FindProperty(field);
                if (p == null) { _report.AppendLine($"    ⚠ 필드 없음 — {field}"); continue; }
                if (p.objectReferenceValue != null) continue;

                string path = $"Assets/Prototype_Elevator/Data/Profiles/{asset}.asset";
                var obj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (obj == null) { _report.AppendLine($"    ⚠ 에셋 없음 — {path}"); continue; }
                p.objectReferenceValue = obj;
                wired++;
                _report.AppendLine($"    배선 {field} → {asset}.asset");
            }
            if (wired > 0)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(run);
            }
            _report.AppendLine($"  프로파일 배선 {wired}건 (0 이면 이미 전부 배선 — 멱등)");
        }

        /// <summary>sRGB 가중 휘도. `VISUAL_SPEC` §12 와 같은 계수(선형화하지 않는다).</summary>
        private static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        private static string Leaf(string path)
        {
            int i = path.LastIndexOf('/');
            return i < 0 ? path : path.Substring(i + 1);
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

            var board = Object.FindAnyObjectByType<View.SpinBoardView>(FindObjectsInactive.Include);
            if (board == null) { _report.AppendLine("  ⚠ SpinBoardView 가 없다 — 결과판 이전 실패"); return; }

            // 🔴 **인덱스가 정본이다. 경로도 이름도 아니다.**
            //
            // 직전 판본은 `GameObject.Find("TubesRoot/Tube_c/Cell_r")` 로 찾았다.
            // 그 경로는 **첫 실행에서만 참이다** — 한 번 옮기고 나면 칸은 관찰창
            // 안에 있고, 두 번째 실행부터 영원히 「없음」이 된다. 2026-08-03 에
            // 그 상태에서 재조립이 돌아 아홉 칸을 통째로 파괴했고, 보고서는
            // 「0/9」한 줄만 남겼다.
            //
            // `SpinBoardView._cells[i]` 를 읽으면 위치와 무관하게 정확히 그 칸이다.
            // 살아 있으면 옮기고, 죽었으면 **새로 만든다** — 그러면 이 함수가
            // 손상 복구기도 겸하게 되어 같은 사고에서 저절로 회복된다.
            var so = new SerializedObject(board);
            SerializedProperty cells = so.FindProperty("_cells");
            if (cells == null) { _report.AppendLine("  ⚠ SpinBoardView._cells 를 찾지 못했다"); return; }
            cells.arraySize = 9;

            int moved = 0, created = 0, hiddenSouls = 0;
            var wired = new Transform[9];
            for (int col = 0; col < 3; col++)
            {
                for (int row = 0; row < 3; row++)
                {
                    // 인덱스 규약은 `SoulReelView`·`CustomsLockView` 와 **같다** — 열 우선.
                    int index = col * 3 + row;
                    Transform module = grid.Find($"{AscendReferenceRoom.WindowModuleName}_{col}{row}");
                    if (module == null)
                    { _report.AppendLine($"  ⚠ 칸 ({col},{row}) — 관찰창 모듈이 없다"); continue; }

                    SerializedProperty slot = cells.GetArrayElementAtIndex(index);
                    var cell = slot.objectReferenceValue as Transform;

                    // 구조 보관소에 있으면 거기서 꺼낸다(재조립이 빼 둔 것).
                    if (cell == null) cell = FindRescued(col, row);
                    // 최초 1회 — 아직 옛 위치에 있다.
                    if (cell == null) cell = GameObject.Find($"TubesRoot/Tube_{col}/Cell_{row}")?.transform;

                    if (cell == null) { cell = CreateCell(module, row); created++; }
                    else { Undo.SetTransformParent(cell, module, "rewire board cell"); moved++; }

                    // 🔴 **심볼이 영혼의 자리를 차지한다** (`UP-FIX-82`).
                    //
                    // 직전 판본은 칸을 영혼보다 **30mm 뒤**(z = 영혼 + 0.030)에 두고
                    // 0.85 로 줄였다. 실측하면 그 배치는 심볼을 화면에서 완전히 지운다 —
                    // 장식 `SoulObject` 의 월드 폭이 0.177 이고 심볼은 0.145 인데
                    // **장식이 더 크고 더 앞에** 있다. 정면 가림률 100% 다.
                    // 평가자가 「아홉 칸이 전부 같은 붉은 덩어리」로 본 것이 그것이고,
                    // 실제로 아홉 칸은 **같은 물체**를 보여 주고 있었다.
                    //
                    // 그래서 칸을 영혼과 **같은 z** 에 놓고 배율을 1 로 되돌린다.
                    // 크기는 메시에 구워져 있다(`SymbolShapeFactory`) — 배율로
                    // 조절하면 두 경로가 또 달라진다.
                    cell.localPosition = new Vector3(0f, 0f, ReferenceRoomSpec.SoulDepthFromDoorFace);
                    cell.localRotation = Quaternion.identity;
                    cell.localScale = Vector3.one;

                    // **매번 다시 만든다.** 「없으면 만든다」였을 때 이미 있던 칸 아홉이
                    // 옛 프리미티브를 그대로 들고 있어 형상 개정이 화면에 도달하지 않았다.
                    SymbolShapeFactory.Build(cell);

                    slot.objectReferenceValue = cell;
                    wired[index] = cell;
                    EditorUtility.SetDirty(cell);

                    // 장식 영혼을 끈다 — 그 자리를 심볼이 대신한다. 오브젝트는 남긴다.
                    hiddenSouls += HideSoulDecoration(module);
                }
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(board);

            SeedPreviewBoard(wired);

            // 구조 보관소가 비었으면 치운다. 빈 노드를 남기면 다음 사람이
            // 「이게 뭐지」를 쫓게 된다.
            GameObject park = GameObject.Find(AscendReferenceRoom.RescueRootName);
            if (park != null && park.GetComponentsInChildren<Transform>(true).Length <= 1 + park.transform.childCount)
            {
                bool empty = true;
                foreach (Transform s in park.transform) if (s.childCount > 0) { empty = false; break; }
                if (empty) Object.DestroyImmediate(park);
            }

            _report.AppendLine($"  결과판 — 이전 {moved} · 신규 {created} / 9 칸 " +
                               "(행 0 = 위, 열 0 = 왼쪽 · 인덱스 = 열×3+행)");
            _report.AppendLine($"     심볼 형상 재적용 9칸 · 장식 영혼 렌더러 {hiddenSouls}개 비표시 " +
                               $"· 칸 z={ReferenceRoomSpec.SoulDepthFromDoorFace:F3} 배율 1.000");
            if (created > 0)
                _report.AppendLine("     ℹ 신규는 재조립이 파괴한 칸을 복구한 것이다 — 정상 경로다.");
        }

        /// <summary>
        /// 관찰창의 장식 영혼(`SoulObject` + 그 `Core`) **렌더러를 끈다.**
        ///
        /// 오브젝트는 지우지도 끄지도 않는다 — 조립기가 매 재조립마다 다시 만들고,
        /// 지우면 다음 재조립이 「빠뜨렸다」로 읽어 되돌린다. 렌더러만 끄면
        /// 조립기가 다시 켜도 이 함수가 다시 끄므로 두 builder 가 싸우지 않는다.
        ///
        /// 왜 꺼야 하는가: 이것이 아홉 칸을 같아 보이게 만든 물체다. 모든 창에
        /// 같은 형상·같은 크기로 항상 켜져 있었고 결과판 심볼보다 크고 앞이었다.
        /// 심볼이 같은 자리에 같은 재질로 서므로 **붉은 광원은 사라지지 않는다** —
        /// 형태만 칸마다 달라진다 (`UP-FIX-53` 회귀 방지).
        /// </summary>
        private static int HideSoulDecoration(Transform module)
        {
            Transform soul = module.Find(AscendReferenceRoom.SoulName);
            if (soul == null) return 0;
            int hidden = 0;
            foreach (Renderer r in soul.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled) continue;
                Undo.RecordObject(r, "hide soul decoration");
                r.enabled = false;
                EditorUtility.SetDirty(r);
                hidden++;
            }
            return hidden;
        }

        /// <summary>
        /// 저장된 씬의 **기본 판**을 세운다.
        ///
        /// 왜 필요한가: 장식 영혼을 끄고 나면 빈 판은 아홉 개의 검은 구멍이다.
        /// 그런데 에디트 모드 캡처(`Ascend/Room/Capture Hero Objects` 등)는
        /// 게임을 돌리지 않으므로 판이 항상 비어 있고, 그러면 시각 평가가
        /// **플레이어가 실제로 보는 화면이 아닌 것**을 판정하게 된다.
        /// `EyeLevelCapture` 는 이미 같은 이유로 촬영 전에 판을 채운다.
        ///
        /// 값은 고정 패턴이다 — 정상 5 · 흡수 2 · 증식 2. 직선을 만들지 않아
        /// 「정화 직전」처럼 보이지 않고, 세 종류가 한 화면에 다 들어온다.
        /// 플레이가 시작되면 `SpinBoardView.Awake` 의 `ClearAll` 이 즉시 지운다.
        /// </summary>
        private static void SeedPreviewBoard(Transform[] cells)
        {
            // 열 우선 인덱스(= 열×3 + 행). 행 0 이 위다.
            Spin.SymbolKind[] pattern =
            {
                Spin.SymbolKind.NormalSoul,   Spin.SymbolKind.Absorber,     Spin.SymbolKind.NormalSoul,
                Spin.SymbolKind.Proliferator, Spin.SymbolKind.NormalSoul,   Spin.SymbolKind.Absorber,
                Spin.SymbolKind.NormalSoul,   Spin.SymbolKind.Proliferator, Spin.SymbolKind.NormalSoul,
            };
            int shown = 0;
            for (int i = 0; i < cells.Length && i < pattern.Length; i++)
            {
                if (cells[i] == null) continue;
                SymbolShapeFactory.ShowKind(cells[i], pattern[i]);
                shown++;
            }
            _report.AppendLine($"     기본 판 {shown}/9 칸 (정상 5 · 흡수 2 · 증식 2) " +
                               "— 에디트 모드 캡처용. 플레이 진입 시 ClearAll 이 지운다");
        }

        /// <summary>재조립이 빼 둔 칸을 원래 모듈 이름으로 되찾는다.</summary>
        private static Transform FindRescued(int col, int row)
        {
            GameObject park = GameObject.Find(AscendReferenceRoom.RescueRootName);
            if (park == null) return null;
            Transform slot = park.transform.Find($"{AscendReferenceRoom.WindowModuleName}_{col}{row}");
            return slot != null ? slot.Find($"Cell_{row}") : null;
        }

        /// <summary>
        /// 결과판 칸 하나를 **껍데기만** 새로 만든다. 심볼 형상은 곧바로
        /// <see cref="SymbolShapeFactory.Build"/> 가 채운다 — 호출부가 신규·기존을
        /// 가리지 않고 매번 부르므로, 여기서 형상을 적으면 정의가 둘이 된다.
        /// 그것이 `UP-FIX-82` 가 화면에 도달하지 못한 이유였다.
        /// </summary>
        private static Transform CreateCell(Transform module, int row)
        {
            var cell = new GameObject($"Cell_{row}");
            cell.transform.SetParent(module, false);
            Undo.RegisterCreatedObjectUndo(cell, "create board cell");
            return cell.transform;
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
            // 🔴 **2026-08-03 정정 — 「덮개와 잠금등은 남긴다」가 사실이 아니었다.**
            //
            // 직전 판본의 이 자리에는 「`OverharvestUnlockEffect` 가 덮개(`CoverPivot`)와
            // 잠금등(`LockLight`)을 움직여 2단 구간이 열렸음을 표현하므로 남긴다」고
            // 적혀 있었다. **씬을 직접 읽어 보니 거짓이다** — 그 컴포넌트의 필드는
            // `_warningStripes` = [WarningStripe, WarningStripe_Upper] · `_shakeTarget` =
            // Housing · `_spotLight` = UnlockSpot 뿐이고, 덮개·리브·잠금등을 가리키는
            // 참조가 씬 어디에도 없다.
            //
            // 그리고 그 남겨 둔 덮개가 실제 피해를 냈다 — 0.44 × 0.56 짜리 판이
            // 레버 컬럼 앞 0.31m 에 떠서 **새 컬럼과 캐비닛 우측 뱅크를 가렸다.**
            // 그레이박스 캡처에서 검은 사각형으로 나타났고, 지시의 합격 기준
            // 「9개 창과 레버가 함께 보임」을 정면으로 깼다.
            //
            // 기록이 실물과 다를 때는 실물이 이긴다. 덮개 셋도 함께 숨긴다.
            string[] duplicateBody =
            {
                "Housing", "HandleShaft", "HandleGrip", "WarningStripe", "WarningStripe_Upper",
                "CoverPlate", "CoverRib", "LockLight",
            };
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

        /// <summary>
        /// 판독면(`Screen`) 왼쪽 끝과 계기 내용 사이의 여백(m). 0 이면 글자가 유리
        /// 가장자리에 닿아 「판에 얹힌 것」이 아니라 「판에서 흘러나온 것」으로 보인다.
        /// </summary>
        private const float PanelFaceMargin = 0.012f;

        private static void MovePowerReadout(GameObject root)
        {
            Transform anchor = FindDeep(root.transform, "Anchor_Value");
            Transform panel = GameObject.Find("GrayboxWorld/Car/InstrumentPanel")?.transform;
            if (anchor == null || panel == null)
            { _report.AppendLine($"  ⚠ 계기판 이전 실패 — anchor={(anchor == null ? "없음" : "있음")} panel={(panel == null ? "없음" : "있음")}"); return; }

            Undo.RecordObject(panel, "rewire instrument panel");
            panel.position = anchor.position;

            // 🔴 **`Euler(0, 180, 0)` 이었다. 계약 표찰과 **같은** 오해다** (`UP-FIX-87` 2차).
            //
            // 주석은 「실내(−Z)를 정면으로 세운다」였고 `forward` 도 정말 (0,0,−1) 이었다.
            // 그런데 이 씬의 월드 TMP 는 **`forward` 반대쪽에서 읽힌다**
            // (재질 `NanumGothic SDF Material SingleSided` 가 `_CullMode = 2`).
            // 계기판은 후면 벽에 있고 플레이어는 −Z 쪽이므로 `forward` 는 **+Z** 여야 한다.
            //
            // 차분 렌더로 확정했다 (A 포즈, post ON, 1920×1080) —
            //
            //   rotY 180 (직전)  Floor 0 · Power 0 · Status 0 · Required 0 · Cascade **0 px**
            //   rotY 0   (지금)  Floor 174 · Power 194 · Status 327 · Required 252 · Cascade 269
            //
            // 독립 평가가 여섯 포즈 전부에서 「숫자·게이지·눈금 0개」라고 적은 것이 이것이다.
            // 판(배경 쿼드)은 렌더되는데 그 위 글자만 안 그려지고 있었다 — 배치 문제가
            // **아니었다.** 배치를 세 번째로 만졌으면 또 못 고쳤을 자리다.
            //
            // 자식 전체를 함께 뒤집는다(라벨만 뒤집으면 정렬 기준점이 반대로 가서
            // 글자 블록이 판 밖으로 나간다 — 실측으로 확인했다). 로컬 배치는
            // 이미 왼→오른쪽 오름차순(눈금 100% 가 −1.007, 300% 가 −0.613)이라
            // 뒤집으면 게이지가 **관례대로** 왼→오른쪽으로 자란다.
            panel.rotation = Quaternion.identity;

            // 🔴 **크기를 눈으로 정하지 않고 화면에 맞춘다.**
            //
            // 0.5 로 고정해 뒀더니 계기판이 표시기 화면보다 커서 **왼쪽으로
            // 넘쳐 레버 컬럼을 가렸다.** 그레이박스 캡처에서 컬럼 앞에 검은
            // 판이 덮여 있었고, 그건 지시의 합격 기준 「플레이어 기본 시점에서
            // 9개 창과 **레버**가 함께 보임」을 바로 깨뜨린다.
            //
            // 실제 경계를 재서 화면 안에 들어갈 배율을 계산한다. 원본 크기를
            // 모르는 채 상수를 적으면 원본이 바뀔 때마다 같은 사고가 난다.
            panel.localScale = Vector3.one;
            // ⚠ **순서가 결과를 정한다.** 아래 셋은 전부 경계를 바꾸므로 재기 **전에**
            // 끝나야 한다. 예전엔 `ShowPanelExceptBackplate` 가 배율 계산 뒤에 있었고,
            // 그러면 그때그때의 렌더러 상태에 따라 배율이 달라져 멱등이 깨진다.
            CompactInstrumentPanel(panel);
            ShowPanelExceptBackplate("GrayboxWorld/Car/InstrumentPanel", "PanelBack");
            LiftOverloadLamp(panel);

            // ⚠ **그려지는 것만 잰다.** `PanelBack` 은 렌더러가 꺼져 있는데도 폭 1.70 으로
            // 경계를 지배해 배율을 눌러 왔다 — 화면에 없는 물체가 글자 크기를 정하고 있었다.
            Bounds b = EncapsulateVisible(panel);
            float screenW = ReferenceRoomSpec.PowerMeterWidth - 0.11f;
            // ⚠ 판독면 **아래 띠는 눈금과 바늘이 쓴다.** 화면 전체 높이로 맞추면
            // 글자가 눈금 위로 내려와 겹친다 — `UP-FIX-51` 과 같은 종류의 결함이다.
            float screenH = ReferenceRoomSpec.PowerMeterHeight - 0.11f - 0.10f;
            float fit = 1f;
            if (b.size.x > 0.0001f) fit = Mathf.Min(fit, screenW / b.size.x);
            if (b.size.y > 0.0001f) fit = Mathf.Min(fit, screenH / b.size.y);
            panel.localScale = Vector3.one * Mathf.Clamp(fit, 0.05f, 1f);
            EditorUtility.SetDirty(panel);
            _report.AppendLine($"     계기판 배율 {panel.localScale.x:F3} " +
                               $"(원본 {b.size.x:F2}×{b.size.y:F2} → 화면 {screenW:F2}×{screenH:F2})");

            // 🔴 **배율만 맞추고 위치를 안 맞췄다** (`UP-FIX-87`).
            //
            // `panel.position = anchor.position` 은 **루트**를 옮긴다. 그런데 이
            // 계기판의 자식들은 구 그레이박스 시절의 **월드 좌표**를 로컬로 들고 있어
            // 루트에서 크게 떨어져 있다. 실측 — 루트 (1.288, 1.412, 2.162) 인데
            // 실제 그려지는 덩어리의 중심은 (1.543, 1.961, 1.681) 이었다.
            // 즉 판독면에서 **0.26 오른쪽 · 0.55 위 · 0.48 앞**의 허공이다.
            //
            // 재는 값이 하나 빠져 있으면 그 축은 조용히 어긋난다 — 배율은 화면에
            // 맞췄는데 그 화면 위에 있지 않았다. 실제 경계를 다시 재서 **차이만큼**
            // 루트를 민다. 상수를 적지 않으므로 원본이 바뀌어도 따라간다.
            Bounds fitted = EncapsulateVisible(panel);
            // ⚠ 실내 쪽은 **−forward** 다. 회전이 identity 가 되면서 `forward` 가 벽 안쪽
            // (+Z)을 가리키게 됐다 — 부호를 같이 뒤집지 않으면 계기판이 벽 속으로 들어간다.
            //
            // ⚠ 세로 기준은 `Anchor_Value`(판독면 중심 +16%)가 아니라 **판독면 중심 + 아래
            // 예약 띠의 절반**이다. 앵커의 +16% 는 계기판이 작던 시절에 정해진 값이고,
            // 내용이 판독 상자를 꽉 채우는 지금은 그만큼 위로 밀려 첫 줄이 판 밖으로 나간다
            // (실측 — 「0 / 10 층」이 판독면 위 0.025m 에 떠 있었다).
            // `screenH` 가 쓰는 것과 **같은 0.10** 에서 유도하므로 둘이 어긋날 수 없다.
            //
            // 🔴 **가로는 가운데가 아니라 왼쪽에 붙인다** (`UP-FIX-88`).
            //
            // 4차 독립 평가: 「B·F 포즈에서 계기판이 우측 프레임 밖으로 절단
            // (`층`, `0 %`, `무게 0/0` 끝자리)」. 원인을 좌표로 쟀다 —
            //
            //   판독면 `Screen`      월드 X 0.983 … 1.773
            //   포즈 B 프레임 우단   월드 X **1.447** (눈 x −0.35, 수평 FOV 91.5°, 라벨 깊이 z 2.051)
            //   포즈 F 프레임 우단   월드 X **1.661**
            //
            // 즉 **계기판은 자기 판독면 안에 있고, 판독면이 포즈 안에 없다.**
            // 평가자가 「글자 크기가 아니라 판독면 폭 또는 포즈 프레이밍」이라고 적은 것이 맞다.
            //
            // 여기서 할 수 있는 것은 하나다 — 판독면의 **왼쪽부터** 쓰는 것.
            // 가운데 정렬은 오른쪽에 0.045 m 를 놀리면서 그만큼을 프레임 밖으로 내보내고
            // 있었다. 왼쪽 정렬로 내용 전체가 0.026 m(≈22 px) 왼쪽으로 온다.
            // 상수를 적지 않고 `Screen` 의 실제 경계를 재는 이유는 판이 바뀌면 상수가
            // 조용히 어긋나기 때문이다 — 이 파일이 세 번 당한 그 함정이다.
            Transform face = anchor.parent != null ? anchor.parent : anchor;
            float centerX = anchor.position.x;
            Transform screen = face.Find("Screen");
            if (screen != null && screen.TryGetComponent(out Renderer screenRenderer))
                centerX = screenRenderer.bounds.min.x + PanelFaceMargin + fitted.size.x * 0.5f;
            Vector3 target = new Vector3(centerX, face.position.y + 0.10f * 0.5f, anchor.position.z);
            Vector3 want = target - panel.forward * (fitted.size.z * 0.5f + 0.006f);
            panel.position += want - fitted.center;
            EditorUtility.SetDirty(panel);
            Bounds after = EncapsulateVisible(panel);
            _report.AppendLine($"     계기판 정렬 — 덩어리 중심 ({fitted.center.x:F3}, {fitted.center.y:F3}, {fitted.center.z:F3}) → " +
                               $"({after.center.x:F3}, {after.center.y:F3}, {after.center.z:F3}) " +
                               $"· 판독면 ({anchor.position.x:F3}, {anchor.position.y:F3}, {anchor.position.z:F3})");

            // 🔴 **구 계기판의 배경 쿼드를 숨긴다. 글자만 남긴다.**
            //
            // 독립 평가자가 지목한 「기능 표시 0 인 빈 밝은 사각형」의 정체가 이것이었다.
            // 조립기가 만든 판독면을 어둡게 고쳐도 화면은 그대로였다 — 그 앞에 **구
            // 계기판의 밝은 배경 판**이 덮여 있었기 때문이다. 근접 캡처에서 새로
            // 만든 눈금·바늘이 그 판 **아래로** 삐져나와 보였고, 그게 단서였다.
            //
            // 이 저장소는 같은 실패를 세 번째 겪는다 — 구 레버 몸통, 구 조작대,
            // 그리고 이 판. **구 형상이 새 형상 앞을 덮는 것**이 반복되는 이유는
            // 배선 이전이 위치만 옮기고 렌더러를 그대로 두기 때문이다.
            // TMP 글자는 남긴다 — 판독 내용이 그것이다.
            //
            // 🔴 **2026-08-04 정정 (`UP-FIX-87`) — 여기서 계기판이 통째로 꺼져 있었다.**
            //
            // 직전 판본은 `HideRenderers("…/InstrumentPanel")` 을 불렀고, 그 함수는
            // **TMP 가 아닌 자식 렌더러를 전부** 끈다. 주석은 「배경 쿼드만」이라고
            // 적고 있었지만 실제로 꺼진 것은 열 개였다 —
            //
            //   PanelBack · PowerBarBg · PowerBarFill · OverloadLight · OverloadHousing
            //   Tick_100 · Tick_130 · Tick_170 · Tick_220 · Tick_300
            //
            // 즉 **전력 막대 자체와 임계점 눈금 다섯이 화면에서 사라졌다.** 독립 평가가
            // 「현재 전력과 위험 이해」에 2점을 준 라운드의 캡처 네 장 전부에서
            // 이 열 개의 기여 화소가 0 이었다. 게이지 없이 글자만 남은 계기판이었다.
            //
            // 이름으로 **정확히 하나만** 끈다. 나머지는 명시적으로 **켠다** —
            // 이미 꺼진 채 저장된 씬을 고쳐야 하므로 「끄기」만으로는 멱등이 아니다.
            //
            // ⚠ 이 둘은 **배율 계산 앞으로 옮겼다** (위 참조). 여기 남겨 두면
            // 「그려지는 것만 잰다」가 자기가 바꾸기 전의 상태를 재게 된다.

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

            // 🔴 **실행 레버 상호작용체는 새 그립 **위에** 놓고 형상은 숨긴다.**
            //
            // 그레이박스 캡처에서 이 오브젝트가 레버 컬럼 앞을 **검은 판으로 덮고
            // 있었다.** 구 조작대 슬래브라 새 컬럼과 같은 자리를 차지하고, 그 결과
            // 지시의 합격 기준 「9개 창과 **레버**가 함께 보임」이 깨졌다.
            //
            // 이 저장소는 같은 실패를 이미 한 번 겪었다 — 구 레버 몸통이 새 축 보스와
            // 그립을 통째로 가렸고, 사용자가 「모델링 디테일이 왜 반영 안 됐냐」고
            // 물었다. 반영은 됐고 **가려져** 있었다.
            //
            // 오브젝트를 끄지 않는다. `TenFloorAutoPilot` 이 `FindAnyObjectByType<T>()`
            // 를 인자 없이 불러 **비활성을 찾지 못하고**, 그러면 10층 검증이 첫 줄에서
            // 죽는다(실제로 죽였다). 콜라이더는 살려 두고 **렌더러만** 끈다 —
            // 조준은 그대로 되고 화면에서만 사라진다.
            Transform grip = FindDeep(root.transform, "Grip");
            Vector3 leverSpot = grip != null
                ? grip.position
                : new Vector3(ReferenceRoomSpec.LeverColumnCenterX, ReferenceRoomSpec.LeverPivotY,
                              ReferenceRoomSpec.WallRearZ - ReferenceRoomSpec.LeverColumnDepth - 0.30f);
            Place("GrayboxWorld/Car/Console", leverSpot,
                  Quaternion.Euler(0f, 180f, 0f), 0.5f, "실행 레버(InteractableLever)");
            HideRenderers("GrayboxWorld/Car/Console", "구 조작대 슬래브");

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

            // 🔴 **계약 명판 셋을 되살려 글자 뒤에 세운다.**
            //
            // 독립 평가자가 「판도 테도 없이 흰 글자만 벽에 얹혀 있다 ·
            // `VISUAL_SPEC` §5 「현대적 HUD처럼 떠 보이면 안 된다」 위반」으로 지목했다.
            //
            // 원인은 **파킹이 판만 껐고 글자는 안 껐다**는 것이다. 실측 —
            //   `ContractPlaqueLabel_0..2` 활성 · 렌더 True @ (0.95, 1.2~1.64, −0.72)
            //   `ContractPlaque_0..2`      **비활성** @ (0.99, 1.28~1.72, +0.30)
            // 둘이 서로 다른 자리에 있고 한쪽만 살아 있었다. 글자가 뜬 것이 아니라
            // **판이 꺼지고 딴 데 있었다.**
            //
            // 이 저장소가 이번 배치에서 네 번째로 겪는 「구 형상과 새 배치가 어긋난다」다.
            // 판과 글자를 **같은 자리에** 세우고 판을 되살린다.
            RelocateContractPlaques();

            // 사고 기록기도 후면 벽에서 뺀다 — 같은 이유다.
            Place("GrayboxWorld/Car/AccidentPrinter",
                  new Vector3(ReferenceRoomSpec.WallLeftX + 0.10f, 1.30f,
                              ReferenceRoomSpec.WallFrontZ + 0.55f),
                  Quaternion.Euler(0f, 90f, 0f), 0.8f, "사고 기록기");
        }

        /// <summary>
        /// 오브젝트는 살려 두고 **렌더러만** 끈다.
        ///
        /// 끄지 않는 이유가 하네스에 있다 — `FindAnyObjectByType&lt;T&gt;()` 는 비활성
        /// 오브젝트를 찾지 않는다. 콜라이더도 남겨야 조준이 된다.
        /// TMP 텍스트는 남긴다(월드 안내문은 캡처 쪽에서 따로 끈다).
        /// </summary>
        private static void HideRenderers(string path, string what)
        {
            GameObject go = GameObject.Find(path);
            if (go == null)
            {
                string leaf = path.Substring(path.LastIndexOf('/') + 1);
                foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                    if (t.name == leaf) { go = t.gameObject; break; }
            }
            if (go == null) { _report.AppendLine($"  ⚠ {what} — `{path}` 를 찾지 못했다"); return; }

            int hidden = 0;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponent<TMPro.TMP_Text>() != null) continue;
                if (!r.enabled) continue;
                r.enabled = false;
                EditorUtility.SetDirty(r);
                hidden++;
            }
            _report.AppendLine($"     {what} 렌더러 {hidden}개 비표시 (오브젝트·콜라이더는 살린다)");
        }

        /// <summary>
        /// 계기판 내용을 **한 단(column)으로 압축한다.** (`UP-FIX-86` — 글자 크기)
        ///
        /// ## 왜 필요한가
        ///
        /// 3차 독립 평가 실측 — 한글 글리프가 `A_entry_to_machine`(거리 4.16m)에서
        /// **6~7px** 였다. 한글은 안정 판독에 약 16px 를 요구하므로 그건 글자가 아니라
        /// 텍스처 노이즈다. 두 라운드 연속 미룬 항목이라 이번에 배치를 확정한다.
        ///
        /// 배율은 `min(판독면 / 내용 크기)` 이므로 글자를 키우는 길은 둘뿐이다 —
        /// 판독면을 키우거나 내용을 줄이거나. **둘 다 한다.**
        /// 판독면은 `ReferenceRoomSpec.PowerMeterHeight` 0.50 → 0.92 (그 주석이 이유를 든다).
        ///
        /// ## 내용에서 무엇이 폭을 잡아먹고 있었나 (실측, 로컬 단위)
        ///
        ///   PanelBack      x −1.650 … +0.050  폭 1.70  ← **렌더러가 꺼져 있는데도** 경계를 지배
        ///   PowerBarBg     x −1.580 … +0.140  폭 1.72  ← 게이지 막대
        ///   글자 다섯 줄   x −1.580 … −1.081  폭 0.50  ← 정작 읽어야 할 것
        ///   과적등         x −0.180 … −0.020           ← 글자 단에서 1.4 떨어져 떠 있다
        ///
        /// **글자는 전체 폭의 28% 만 쓰고 있었다.** 막대와 과적등을 글자 단 안으로
        /// 들이고 배면판을 그 상자에 맞추면 내용 폭이 1.79 → 0.69 로 떨어진다.
        ///
        /// ## 줄을 지우지 않았다
        ///
        /// 여섯 줄을 줄이는 선택지도 있었지만, 그 줄들은 `UP-FIX-44`·`UP-FIX-52` 가
        /// 여러 라운드에 걸쳐 **화면에 올려 놓은 것**이다. 지우면 다른 축이 후퇴한다.
        /// 폭이 남아 있었으므로 줄을 지울 이유가 없었다.
        ///
        /// 멱등이다 — 전부 절대값으로 쓴다. 배율을 곱하지 않는다.
        /// </summary>
        private static void CompactInstrumentPanel(Transform panel)
        {
            // 글자 단 왼쪽 끝. 다섯 라벨이 전부 여기에 있다(실측 lp.x = −1.580).
            const float colX = -1.580f;
            // 막대 길이. 글자 단 폭 0.50 보다 살짝 넓어야 「게이지」로 읽힌다.
            const float barLen = 0.62f;
            const float maxRatio = 3.0f;   // 눈금 300% 가 막대 끝

            Transform bg = panel.Find("PowerBarBg");
            Transform pivot = panel.Find("PowerBarPivot");
            Transform ticks = panel.Find("PowerBarTicks");
            Transform housing = panel.Find("OverloadHousing");
            Transform lamp = panel.Find("OverloadLight");
            Transform back = panel.Find("PanelBack");

            if (bg != null)
            {
                Undo.RecordObject(bg, "compact bar");
                Vector3 s = bg.localScale; s.x = barLen; bg.localScale = s;
                Vector3 p = bg.localPosition; p.x = colX + barLen * 0.5f; bg.localPosition = p;
                EditorUtility.SetDirty(bg);
            }
            if (pivot != null)
            {
                Undo.RecordObject(pivot, "compact bar pivot");
                Vector3 p = pivot.localPosition; p.x = colX; pivot.localPosition = p;
                EditorUtility.SetDirty(pivot);
            }
            if (ticks != null)
            {
                // 눈금은 이름이 곧 임계값이다 — 상수 표를 따로 두면 둘이 어긋난다.
                foreach (Transform t in ticks)
                {
                    int cut = t.name.LastIndexOf('_');
                    if (cut < 0 || !int.TryParse(t.name.Substring(cut + 1), out int pct)) continue;
                    Undo.RecordObject(t, "compact tick");
                    Vector3 p = t.localPosition;
                    p.x = colX + barLen * Mathf.Clamp01(pct / 100f / maxRatio);
                    t.localPosition = p;
                    EditorUtility.SetDirty(t);
                }
            }
            // 과적등을 글자 단 오른쪽 위로. 단 밖에 떠 있으면 그것 하나가 폭을 1.4 늘린다.
            if (housing != null && lamp != null)
            {
                Undo.RecordObject(housing, "compact overload housing");
                Vector3 p = housing.localPosition;
                p.x = colX + 0.62f;   // 막대 오른쪽 끝과 같은 x
                housing.localPosition = p;
                EditorUtility.SetDirty(housing);
                Undo.RecordObject(lamp, "compact overload lamp");
                Vector3 lp = lamp.localPosition; lp.x = p.x; lamp.localPosition = lp;
                EditorUtility.SetDirty(lamp);
            }
            // 배면판은 꺼져 있지만 **크기를 맞춰 둔다** — 남겨 두면 다음 사람이
            // 켰을 때 1.70 짜리 판이 다시 나온다. 꺼진 값이 틀린 채 남는 것이
            // 이 저장소가 발광과 형상에서 두 번 당한 함정이다.
            if (back != null)
            {
                Undo.RecordObject(back, "compact backplate");
                back.localScale = new Vector3(barLen + 0.10f, 0.85f, back.localScale.z);
                Vector3 p = back.localPosition;
                p.x = colX + (barLen + 0.10f) * 0.5f - 0.05f;
                p.y = 1.62f;
                back.localPosition = p;
                EditorUtility.SetDirty(back);
            }

            // 막대 길이를 뷰에도 알린다. 여기만 고치면 채움이 옛 길이로 자란다.
            var view = panel.GetComponent<View.InstrumentPanelView>();
            if (view != null)
            {
                var so = new SerializedObject(view);
                SerializedProperty w = so.FindProperty("_barWidth");
                SerializedProperty m = so.FindProperty("_maxRatio");
                if (w != null) w.floatValue = barLen;
                if (m != null) m.floatValue = maxRatio;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(view);
            }

            // 🔴 **왼쪽 정렬을 「설정」하지 않고 「재서 교정」한다.**
            //
            // 다섯 라벨은 정렬(TopLeft) · 렉트(26×6) · 피벗(0, 0.5) · 좌표(x −1.580)가
            // 전부 같은데 `CascadeLabel` 의 글리프만 x −0.958 에서 시작했다.
            // 즉 **인스펙터에서 같아 보이는데 화면이 다른** 종류다(마진·선행 공백 등).
            // 원인을 쫓는 대신 결과를 직접 맞춘다 — 글리프 왼쪽 끝을 재서 그만큼 민다.
            // 그 한 줄이 내용 폭을 0.50 → 0.93 으로 **1.9배** 부풀리고 있었고,
            // 배율이 폭에 묶여 있으므로 그것이 곧 글자 크기였다.
            //
            // 멱등이다 — 교정 뒤에는 왼쪽 끝이 colX 라 다음 실행의 이동량이 0 이다.
            // 저장된 씬의 기본 문구. `PowerLabel` 과 `RequiredLabel` 이 둘 다
            // 「전력 0 / 0」 이라 에디트 모드 캡처에서 **같은 줄이 두 번** 보였다.
            // 런타임 `ApplyPower` 가 쓰는 형식(`전력 N` / `요구 N  P%`)을 그대로 넣는다 —
            // 값을 지어내지 않고 **0 인 상태의 실제 출력**을 적는 것이다.
            // ⚠ 런타임 `InstrumentPanelView` 가 쓰는 형식과 **글자 하나까지 같아야 한다.**
            // 고정 캡처는 에디트 모드에서 찍히므로 여기 적힌 것이 곧 평가받는 화면이다 —
            // 형식이 어긋나면 캡처가 게임을 증명하지 못한다.
            // 공백 셋 → 하나, `{0:P0}`(값과 % 사이에 공백을 하나 더 넣는다) → `%` 직결.
            // `UP-FIX-88` 의 우측 절단이 이 줄에서 나왔다.
            // 🔴 `UP-FIX-93` (2026-08-04) — 달성률을 이 줄에서 **뺐다.**
            //    `요구 0 0%` 는 세 토큰 간격이 균등해 「요구값과 달성률을 가를 수 없다」는
            //    회귀를 만들었다. 공백을 되돌리면 `UP-FIX-88`(B 포즈 절단)이 커진다 —
            //    두 요구가 같은 줄에서 서로를 배신하므로 **토큰을 하나로 줄인다.**
            //    달성률은 `AscentColumnView` 의 탱크 채움 + 「전력 N   P%」로 옮겼다.
            SetDefaultTexts(panel);

            int aligned = 0;
            foreach (TMPro.TMP_Text t in panel.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                t.ForceMeshUpdate();
                float left = GlyphLeftLocal(panel, t);
                if (float.IsNaN(left)) continue;
                float delta = colX - left;
                if (Mathf.Abs(delta) < 1e-4f) continue;
                Undo.RecordObject(t.transform, "align text column");
                Vector3 p = t.transform.localPosition;
                p.x += delta;
                t.transform.localPosition = p;
                EditorUtility.SetDirty(t.transform);
                aligned++;
            }

            _report.AppendLine($"     계기 내용 압축 — 막대 {barLen:F2} · 눈금 {colX:F2}…{colX + barLen:F2} " +
                               $"· 과적등을 단 안으로 · 배면판 재단 · 글자 단 정렬 교정 {aligned}줄");
        }

        /// <summary>저장 씬의 기본 문구를 정한다. 런타임이 첫 갱신에서 덮어쓴다.</summary>
        /// <summary>
        /// 저장된 씬의 기본 문구. **런타임 `InstrumentPanelView` 가 쓰는 형식과 글자
        /// 하나까지 같아야 한다** — 고정 캡처는 에디트 모드에서 찍히므로 여기 적힌 것이
        /// 곧 평가받는 화면이다. 형식이 어긋나면 캡처가 게임을 증명하지 못한다.
        ///
        /// 값을 지어내지 않는다. **전력 0 인 상태의 실제 출력**을 적는다.
        /// </summary>
        private static void SetDefaultTexts(Transform panel)
        {
            if (panel == null) return;
            SetDefault(panel, "PowerLabel", "전력 0");
            SetDefault(panel, "RequiredLabel", "요구 0");
        }

        private static void SetDefault(Transform panel, string name, string text)
        {
            var t = panel.Find(name)?.GetComponent<TMPro.TMP_Text>();
            if (t == null || t.text == text) return;
            Undo.RecordObject(t, "seed panel text");
            t.SetText(text);
            t.ForceMeshUpdate();
            EditorUtility.SetDirty(t);
        }

        /// <summary>
        /// 보이는 글리프의 **로컬 x 최소값.** 글자가 없으면 `NaN`.
        ///
        /// ⚠ `Renderer.bounds` 를 쓰면 안 된다. TMP 는 정점 버퍼를 올림 할당하고
        /// 미사용 정점을 로컬 원점에 남겨 경계를 부풀린다 — 이 저장소가 v8 매니페스트에
        /// 이미 적어 둔 함정이다. `characterInfo[i].isVisible` 인 글자만 센다.
        /// </summary>
        private static float GlyphLeftLocal(Transform space, TMPro.TMP_Text text)
        {
            var info = text.textInfo;
            float min = float.NaN;
            for (int i = 0; i < info.characterCount; i++)
            {
                TMPro.TMP_CharacterInfo ch = info.characterInfo[i];
                if (!ch.isVisible) continue;
                Vector3 a = space.InverseTransformPoint(text.transform.TransformPoint(ch.bottomLeft));
                Vector3 b = space.InverseTransformPoint(text.transform.TransformPoint(ch.topRight));
                float lo = Mathf.Min(a.x, b.x);
                if (float.IsNaN(min) || lo < min) min = lo;
            }
            return min;
        }

        /// <summary>
        /// **그려지는 것**의 월드 경계. 꺼진 렌더러는 화면에 없다.
        ///
        /// 🔴 **TMP 는 `Renderer.bounds` 를 쓰면 안 된다.** 정점 버퍼를 올림 할당하고
        /// 미사용 정점을 로컬 원점에 남겨 경계를 부풀린다. 실측 — `CascadeLabel` 의
        /// 글리프는 x −1.580…−1.267 인데 `Renderer.bounds` 는 −2.202…−1.267 이었다.
        /// 그 **0.62 의 허깨비**가 계기판 폭을 0.70 → 1.32 로 만들고, 배율이 폭에
        /// 묶여 있으므로 그대로 글자 크기를 절반으로 눌렀다.
        ///
        /// v8 매니페스트가 이 함정을 이미 적어 두었는데 그때는 라벨 계측에만 썼다.
        /// 배율 계산에도 같은 함정이 있다는 것을 이번 실측으로 알았다.
        /// </summary>
        private static Bounds EncapsulateVisible(Transform t)
        {
            Bounds b = new Bounds(t.position, Vector3.zero);
            bool first = true;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;

                var text = r.GetComponent<TMPro.TMP_Text>();
                Bounds piece;
                if (text != null)
                {
                    if (!TryGlyphBounds(text, out piece)) continue;
                }
                else if (r.GetComponentInParent<TMPro.TMP_Text>() != null) continue;  // TMP SubMesh — 부모가 이미 냈다
                else piece = r.bounds;

                if (first) { b = piece; first = false; } else b.Encapsulate(piece);
            }
            return first ? Encapsulate(t) : b;
        }

        /// <summary>보이는 글리프 네 모서리만의 월드 AABB. 글자가 없으면 false.</summary>
        private static bool TryGlyphBounds(TMPro.TMP_Text text, out Bounds bounds)
        {
            bounds = default;
            text.ForceMeshUpdate();
            var info = text.textInfo;
            bool any = false;
            for (int i = 0; i < info.characterCount; i++)
            {
                TMPro.TMP_CharacterInfo ch = info.characterInfo[i];
                if (!ch.isVisible) continue;
                Vector3 a = text.transform.TransformPoint(ch.bottomLeft);
                Vector3 c = text.transform.TransformPoint(ch.topRight);
                if (!any) { bounds = new Bounds(a, Vector3.zero); any = true; }
                else bounds.Encapsulate(a);
                bounds.Encapsulate(c);
            }
            return any;
        }

        /// <summary>
        /// 과적등을 **하우징 앞으로** 꺼낸다. (`UP-FIX-87`)
        ///
        /// 렌더러를 되살리고 나서 차분 렌더로 재 보니 `OverloadLight` 만 여전히
        /// 네 포즈 전부 0 화소였다. 실측 — 등은 z 2.117…2.147(⌀0.031),
        /// 하우징은 z 2.111…2.127(0.054 각). **하우징이 6mm 더 앞이고 1.7배 넓다.**
        /// 즉 등이 자기 케이스 안에 들어가 있었다. 「방향」도 「가림 대상」도 아닌
        /// **깊이 순서**였고, 그건 재기 전에는 알 수 없는 종류다.
        ///
        /// 하우징의 앞면 + 등 반지름의 35% 로 놓아 반쯤 튀어나오게 한다.
        /// 상수를 적지 않고 실제 스케일에서 유도하므로 원본이 바뀌어도 따라간다.
        /// </summary>
        private static void LiftOverloadLamp(Transform panel)
        {
            Transform housing = panel.Find("OverloadHousing");
            Transform lamp = panel.Find("OverloadLight");
            if (housing == null || lamp == null) { _report.AppendLine("     ⚠ 과적등/하우징을 찾지 못했다"); return; }

            // ⚠ **부호가 루트 회전에 묶여 있다.** 계기판 루트를 `identity` 로 세우므로
            // (바로 위 `MovePowerReadout` 참조) 실내 쪽은 로컬 **−z** 다.
            //
            // 처음엔 루트가 y 180° 였고 여기 `+z` 라고 적었다. 루트를 뒤집은 라운드에
            // 이 부호를 같이 안 뒤집어서 과적등이 **벽 안으로 들어갔고** 차분 렌더가
            // 네 포즈 전부 0 화소로 다시 잡았다. 한쪽만 고치면 다른 쪽이 조용히 깨진다.
            float front = housing.localPosition.z - housing.localScale.z * 0.5f;
            float radius = lamp.localScale.z * 0.5f;
            Undo.RecordObject(lamp, "lift overload lamp");
            Vector3 before = lamp.localPosition;
            lamp.localPosition = new Vector3(housing.localPosition.x, housing.localPosition.y,
                                             front - radius * 0.35f);
            EditorUtility.SetDirty(lamp);
            _report.AppendLine($"     과적등 z {before.z:F3} → {lamp.localPosition.z:F3} " +
                               $"(하우징 실내면 {front:F3} − 반지름 {radius:F3}×0.35)");
        }

        /// <summary>
        /// 계기판에서 **배면판 하나만** 끄고 나머지 계기 부재는 **켠다.** (`UP-FIX-87`)
        ///
        /// `HideRenderers` 와 갈라 놓은 이유는 방향이 반대이기 때문이다 —
        /// 저쪽은 「끈다」만 하고, 그래서 한 번 잘못 꺼진 것을 스스로 되돌리지 못했다.
        /// 여기는 두 상태를 **둘 다 절대값으로** 쓴다. 그것이 멱등의 정의다.
        /// </summary>
        private static void ShowPanelExceptBackplate(string path, string hideName)
        {
            GameObject go = GameObject.Find(path);
            if (go == null) { _report.AppendLine($"  ⚠ 계기판 렌더러 복원 — `{path}` 를 찾지 못했다"); return; }

            int shown = 0, hidden = 0;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponent<TMPro.TMP_Text>() != null) continue;
                bool want = r.gameObject.name != hideName;
                if (r.enabled == want) { if (want) shown++; else hidden++; continue; }
                Undo.RecordObject(r, "restore instrument renderer");
                r.enabled = want;
                EditorUtility.SetDirty(r);
                if (want) shown++; else hidden++;
            }
            _report.AppendLine($"     계기 부재 표시 {shown}개 · `{hideName}` 비표시 {hidden}개 " +
                               "(전력 막대·임계 눈금·과적등이 여기 포함된다 — UP-FIX-87)");
        }

        /// <summary>
        /// 계약 명판 셋과 그 글자를 계약 패널 옆 우벽에 나란히 세운다.
        ///
        /// **판을 되살리는 것이 요점이다.** 파킹 목록이 판만 끄고 글자는 안 꺼서
        /// 흰 글자가 벽에 떠 있었다. 자리는 계약 패널과 같은 벽·같은 z 로 잡는다 —
        /// 「무엇을·얼마에」를 한 시선 안에서 읽어야 하기 때문이다.
        /// </summary>
        /// <summary>
        /// 명판 한 장의 크기(로컬). 메시가 **단위 정육면체**라 이 값이 곧 미터다.
        ///
        /// 0.34 × 0.17 → **0.42 × 0.26** (2026-08-04, 3차 평가 회신).
        /// 세 줄(이름 / 출현·보상 / 잔류 대가)을 담아야 계약이 서로 **구분**된다 —
        /// 직전까지 세 장이 전부 「계약」 한 낱말이라 선택지가 화면에서 같은 것이었다.
        /// </summary>
        private static readonly Vector3 PlaqueScale = new Vector3(0.42f, 0.26f, 0.024f);

        /// <summary>명판 세 장의 세로 간격(m). 높이 0.26 이면 사이에 0.04 의 틈이 남는다.</summary>
        private const float PlaquePitchY = 0.30f;

        private static void RelocateContractPlaques()
        {
            float x = ReferenceRoomSpec.WallRightX - 0.05f;
            float z = ReferenceRoomSpec.ShelfCenterZ - ReferenceRoomSpec.ShelfLength * 0.5f - 0.45f;

            // 🔴 **`Euler(0, −90, 0)` 이었다. 180° 반대였다** (`UP-FIX-87`).
            //
            // 주석은 「실내(−X)를 향한다」였고 `transform.forward` 도 정말 (−1, 0, 0) 이었다.
            // 그런데 이 씬의 월드 TMP 표찰은 **`forward` 의 반대쪽에서 읽힌다.**
            // 재질 `NanumGothic SDF Material SingleSided` 가 `_CullMode = 2`(Back) 인데
            // TMP 글리프 쿼드의 감김이 로컬 −Z 를 앞면으로 만들기 때문이다.
            // 씬 전체가 이미 그 규약을 따르고 있었다 —
            //
            //   OverharvestLabel  rotY   0  fwd (0,0,+1)  ← 플레이어는 −Z 에서 읽는다
            //   ExecutionLabel    rotY  90  fwd (+1,0,0)  ← 플레이어는 −X 에서 읽는다
            //   ContractPlaqueLabel rotY 270 fwd (−1,0,0) ← **혼자만 반대**
            //
            // 차분 렌더로 확정했다: 같은 자리에서 rotY 270 → **0 화소**,
            // rotY 90 → 글자가 뜨고 좌우도 뒤집히지 않는다(캡처로 확인).
            // 「단면 재질이라 벽 뒤에서만 보인다」는 직전 세션의 추정이 맞았고,
            // 다만 **어느 축이 앞면인지**가 `forward` 와 반대였다.
            //
            // 명판 자체는 대칭 상자라 이 회전에서도 폭·두께가 그대로다
            // (로컬 x → 월드 z 폭 0.34 · 로컬 z → 월드 x 두께 0.024).
            var rot = Quaternion.Euler(0f, 90f, 0f);
            int revived = 0, moved = 0;

            // 🔴 **문구를 데이터에서 읽는다. 지어내지도 하드코딩하지도 않는다.**
            //
            // 3차 독립 평가 지적 — `E_contract_wall` 에서 세 표찰이 **글자까지 동일**하고
            // (전부 「계약」) 출현률↑·정화 보상↑·잔류 대가↑ 중 화면에 있는 것이 0개다.
            // 선택지가 서로 구분되지 않으면 선택이 성립하지 않는다 (`B-4 #11`).
            //
            // 런타임 `InstrumentPanelView.ApplyPlaqueLabel` 은 이미 계약 객체에서
            // 문구를 만들고 있었다. 빠져 있던 것은 **저장된 씬의 기본값**이고,
            // 그래서 에디트 모드 캡처에서는 영원히 「계약」이었다 — 결과판에 기본 판을
            // 심은 것과 같은 종류의 결함이다.
            //
            // 7층은 세 선택지(계약 없음 / 흡수체 / 증식체)가 나란히 놓이는 유일한 층이라
            // 명판 세 장과 개수가 정확히 맞는다. 배수 값은 오늘도 바뀌었으므로
            // **계약 객체에서 읽는다.**
            Spin.ResistanceContract[] choices = Spin.PrototypeCurriculum.For(7).ContractChoices;

            for (int i = 0; i < 3; i++)
            {
                float y = 1.68f - i * PlaquePitchY;

                Transform plaque = FindAnywhere($"ContractPlaque_{i}");
                if (plaque != null)
                {
                    Undo.RecordObject(plaque.gameObject, "revive contract plaque");
                    if (!plaque.gameObject.activeSelf) { plaque.gameObject.SetActive(true); revived++; }
                    plaque.SetPositionAndRotation(new Vector3(x, y, z), rot);
                    // 🔴 **`Vector3.one` 이었다 — 그래서 명판이 한 변 1m 짜리 강철
                    // 정육면체였다** (`UP-FIX-87`). 메시가 단위 `Cube` 프리미티브라
                    // 배율 1 은 「원래 크기」가 아니라 「1 미터」다.
                    //
                    // 결과: 세 명판이 서로를 삼키고 벽을 0.55m 뚫고 나와 우측 앞
                    // 모서리를 채웠고, 글자는 판 앞 30mm 에 있으니 **정육면체 안**에
                    // 갇혔다. 캡처 네 장 전부에서 계약 글자 화소가 0 이었던 이유가 이것이다.
                    // 「안쪽을 안 향한다」도 「`ContractPanel` 이 가린다」도 아니었다 —
                    // **자기 판 안에 들어가 있었다.**
                    //
                    // 회전이 Y −90° 라 로컬 x → 월드 z(벽을 따라 폭), 로컬 z → 월드 −x(두께).
                    plaque.localScale = PlaqueScale;
                    EditorUtility.SetDirty(plaque);
                }

                Transform label = FindAnywhere($"ContractPlaqueLabel_{i}");
                if (label != null)
                {
                    Undo.RecordObject(label, "relocate contract label");
                    // 글자는 판보다 **앞**에 — 30mm 띄우면 판이 배경이 된다.
                    label.SetPositionAndRotation(new Vector3(x - 0.030f, y, z), rot);
                    EditorUtility.SetDirty(label);

                    // 🔴 **정렬이 `Left` 였고 렉트 폭이 17 이었다** (`UP-FIX-87`).
                    //
                    // 그래서 글자는 트랜스폼이 아니라 **렉트 왼쪽 끝**에서 시작했고,
                    // 실측 글리프 중심이 z −2.361 — 트랜스폼(z −1.800)에서 **0.561m**
                    // 옆이었다. 명판 폭은 0.34 다. 즉 글자는 판 위가 아니라
                    // 판에서 한 뼘 반 떨어진 허공에 있었고, 그 자리는 벽 안이다.
                    //
                    // 「안쪽을 안 향한다」도 「무언가에 가려 있다」도 아니었다 —
                    // **좌표가 정렬 규칙 때문에 옆으로 밀려 있었다.** 가운데로 맞춘다.
                    var tmp = label.GetComponent<TMPro.TMP_Text>();
                    if (tmp != null)
                    {
                        Undo.RecordObject(tmp, "align contract label");
                        tmp.alignment = TMPro.TextAlignmentOptions.Center;
                        // 세 줄이 0.42 × 0.26 판에 들어가야 한다. 유효 글자 크기
                        // 4.8 × 0.070 = 0.336 → 글리프 약 0.032m, 1.85m 거리에서 16px.
                        // 한글 안정 판독 하한이 16px 이라는 3차 평가의 기준을 그대로 쓴다.
                        tmp.fontSize = 4.8f;
                        if (choices != null && i < choices.Length)
                            tmp.SetText(PlaqueText(choices[i]));
                        tmp.ForceMeshUpdate();
                        EditorUtility.SetDirty(tmp);
                    }
                    moved++;
                }
            }
            _report.AppendLine($"  계약 명판 — 판 되살림 {revived}/3 · 글자 이동 {moved}/3 " +
                               $"@ 우벽 x={x:F2} z={z:F2} (판이 글자 뒤에 선다)");
            if (choices != null)
                for (int i = 0; i < choices.Length && i < 3; i++)
                    _report.AppendLine($"     명판 {i}: {PlaqueText(choices[i]).Replace("\n", " | ")}");
        }

        /// <summary>
        /// 명판 세 줄. **숫자는 전부 계약 객체에서 온다.**
        ///
        /// `ResistanceContract.Preview()` 는 한 줄짜리라 0.42m 판에 안 들어간다
        /// (약 35자 → 1.1m). 같은 낱말(출현·정화 보상·잔류 대가)을 쓰되 세 줄로 접는다 —
        /// 문구를 새로 짓는 것이 아니라 **줄바꿈만 다르다.**
        ///
        /// 셋을 **같은 순서·같은 크기**로 낸다. `visual-criteria` B-4 #11 이
        /// 「보상만 크게 보이고 대가가 작게 적혀 있으면 함정이다」라고 못 박은 지점이다.
        /// </summary>
        private static string PlaqueText(in Spin.ResistanceContract c)
        {
            if (c.IsNone) return "계약 없음\n출현·보상·대가\n변화 없음";
            return $"{c.Label}\n출현 ×{c.AppearanceMultiplier:0.##}  보상 ×{c.PurifyRewardMultiplier:0.##}\n" +
                   $"잔류 대가 ×{c.ResidualPenaltyMultiplier:0.##}";
        }

        /// <summary>이름으로 찾는다. 비활성도 찾아야 파킹된 것을 되살릴 수 있다.</summary>
        private static Transform FindAnywhere(string name)
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.name == name) return t;
            return null;
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

            // ── 공통 잠금 기구 — 「외부 연결부가 가만히 있으면 실패다」 ────────
            //
            // 지시(2026-08-03)가 실패 조건을 한 문장으로 못박았다: 「레버를 당겼는데
            // 외부 연결부는 가만히 있고 영혼만 갑자기 멈추면 실패다.」
            // 그래서 구동 로드·공통축·상태 탭·클램프 9개를 한 컴포넌트에 묶는다.
            WireCustomsLock(root, grid);

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

        // ══════════════════════════════════════════════════════════════════════
        //  공통 잠금 기구 — 레버에서 9개 클램프까지의 동력 전달
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// <see cref="View.CustomsLockView"/> 를 세우고 부품 14개를 물린다.
        ///
        /// **개수를 보고한다.** 하나라도 비면 그 단계가 조용히 안 움직이고,
        /// 「전달 경로를 추적할 수 있다」는 합격 기준이 그 자리에서 깨진다.
        /// 조용한 실패를 화면이 아니라 로그에서 먼저 잡으려는 것이다.
        /// </summary>
        private static void WireCustomsLock(GameObject root, Transform grid)
        {
            Transform machine = root.transform.Find(AscendReferenceRoom.MachineName);
            Transform column = root.transform.Find(AscendReferenceRoom.LeverBaseName);
            if (machine == null || column == null)
            { _report.AppendLine("  ⚠ 잠금 기구 미배선 — 장치 또는 레버 컬럼이 없다"); return; }

            var fsm = Object.FindAnyObjectByType<View.LeverStateMachine>(FindObjectsInactive.Include);
            var lockView = root.GetComponent<View.CustomsLockView>();
            if (lockView == null) lockView = root.AddComponent<View.CustomsLockView>();

            Transform rod = FindDeep(column, AscendReferenceRoom.DriveRodName);
            Transform shaft = FindDeep(machine, AscendReferenceRoom.CommonShaftName);
            Transform pin = FindDeep(column, AscendReferenceRoom.LeverLockPinName);

            var tabs = new Transform[View.CustomsLockView.Banks];
            int tabCount = 0;
            for (int b = 0; b < View.CustomsLockView.Banks; b++)
            {
                tabs[b] = FindDeep(machine, $"{AscendReferenceRoom.StatusTabName}_{b}");
                if (tabs[b] != null) tabCount++;
            }

            // 클램프 인덱스 규약은 `SoulReelView.Index` 와 **같다** — 열 우선.
            // 다르게 두면 「어느 뱅크가 먼저 물리는가」가 릴과 어긋나 인과가 깨진다.
            var clamps = new Transform[View.CustomsLockView.Chambers];
            int clampCount = 0;
            for (int col = 0; col < 3; col++)
                for (int row = 0; row < 3; row++)
                {
                    Transform module = grid.Find($"{AscendReferenceRoom.WindowModuleName}_{col}{row}");
                    Transform c = module != null ? module.Find(AscendReferenceRoom.LockClampName) : null;
                    clamps[col * 3 + row] = c;
                    if (c != null) clampCount++;
                }

            lockView.Configure(fsm, rod, shaft, pin, tabs, clamps);
            EditorUtility.SetDirty(lockView);

            // 발동은 배선으로 건다. `onLatched` 는 레버가 바닥에 걸린 그 순간이고,
            // 그것이 「동력이 전달되기 시작하는」 시각의 정의다.
            if (fsm != null)
            {
                var fso = new SerializedObject(fsm);
                SerializedProperty latch = Calls(fso, "onLatched");
                if (latch != null) Hook(latch, lockView, typeof(View.CustomsLockView), "Engage");
                fso.ApplyModifiedProperties();
                EditorUtility.SetDirty(fsm);
            }

            _report.AppendLine($"  공통 잠금 기구 — 구동 로드 {(rod != null ? "○" : "×")} · 공통축 {(shaft != null ? "○" : "×")} " +
                               $"· 잠금핀 {(pin != null ? "○" : "×")} · 상태 탭 {tabCount}/3 · 클램프 {clampCount}/9 " +
                               $"· 레버 {(fsm != null ? "○" : "×")}");
            if (rod == null || shaft == null || tabCount < 3 || clampCount < 9)
                _report.AppendLine("     ⚠ 전달 경로에 빈 곳이 있다 — 「외부 연결부가 가만히 있는」 실패 조건이다.");
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

            // 🔴 **전력 환경 프로파일을 배선한다** (`P-20260804-05` B).
            //
            // 에셋을 만들어 놓고 배선하지 않으면 값이 아무 데도 흐르지 않는다 —
            // `DangerFeedbackProfile` 이 정확히 그 상태로 만들어져 `DEAD_IMPLEMENTATION_AUDIT`
            // §1 에 「죽은 구현」으로 기록됐다. 같은 실수를 반복하지 않으려고
            // **에셋 생성과 배선을 같은 자리에** 둔다. 배선이 끊기면 `PowerAmbienceSource`
            // 가 「코드 프리셋 …」이라고 말하므로 반증 가능하다.
            SerializedProperty pPower = so.FindProperty("_powerAmbienceProfile");
            string powerAsset = "(배선 실패)";
            if (pPower != null)
            {
                if (pPower.objectReferenceValue == null)
                {
                    var profile = EnsurePowerAmbienceProfile();
                    pPower.objectReferenceValue = profile;
                    powerAsset = profile != null ? profile.name + " (이번에 배선)" : "(에셋 생성 실패)";
                }
                else powerAsset = pPower.objectReferenceValue.name + " (이미 배선)";
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(risk);

            _report.AppendLine($"  위험 조명 — _cabinLight={(light != null ? light.name : "없음")} " +
                               $"_lampRenderer={(bulbRenderer != null ? bulbRenderer.name : "없음")} " +
                               $"_swayTarget={(lamp != null ? lamp.name : "없음")}");
            _report.AppendLine($"     전력 환경 — _powerAmbienceProfile={powerAsset}");
        }

        /// <summary>
        /// `PowerAmbienceProfile.asset` 이 없으면 만든다. 있으면 **그대로 둔다** —
        /// 손으로 조정한 값을 빌더가 매번 되돌리면 「인스펙터가 원본」이 성립하지 않는다.
        /// 멱등이다: 두 번째 실행은 찾기만 하고 아무것도 쓰지 않는다.
        /// </summary>
        private static Ascend.Prototype.Data.Profiles.PowerAmbienceProfile EnsurePowerAmbienceProfile()
        {
            const string path = "Assets/Prototype_Elevator/Data/Profiles/PowerAmbienceProfile.asset";
            var existing = AssetDatabase.LoadAssetAtPath<
                Ascend.Prototype.Data.Profiles.PowerAmbienceProfile>(path);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<
                Ascend.Prototype.Data.Profiles.PowerAmbienceProfile>();
            created.ApplyPreset(Ascend.Prototype.Data.Profiles.PowerAmbienceIntensity.Standard);
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();
            return created;
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
                // ⚠ `ContractPlaque_0..2` 는 **여기서 뺐다.** 파킹이 판만 끄고 글자는
                // 안 꺼서 흰 글자가 벽에 떠 있었고, 그것이 `UP-FIX-63` 이다.
                // `RelocateContractPlaques` 가 판을 되살려 글자 뒤에 세운다 —
                // 파킹 목록에 남겨 두면 되살린 직후 다시 꺼진다(이 함수가 나중에 돈다).
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

        /// <summary>
        /// 자식 렌더러 전체의 월드 경계. 없으면 크기 0.
        /// 배율을 상수로 적지 않고 **재서** 정하기 위한 것이다.
        /// </summary>
        private static Bounds Encapsulate(Transform t)
        {
            Renderer[] rs = t.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

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
