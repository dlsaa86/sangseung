using System.Collections.Generic;
using System.IO;
using System.Text;
using Ascend.CaptureHarness.EditorTools;
using Ascend.Prototype.Art;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ascend.Prototype.EditorTools
{
    /// <summary>
    /// 사용자 명세(2026-08-02 「산업용 화물 엘리베이터 내부」)를 형상으로 세우는
    /// **멱등 조립기**.
    ///
    /// ## 규칙
    ///
    /// 1. **숫자를 갖지 않는다.** 전부 <see cref="ReferenceRoomSpec"/> 에서 읽는다.
    ///    이 파일에 리터럴 치수가 등장하면 그것은 결함이다 — 두 곳에 적힌 치수는
    ///    반드시 갈라지고, 이 저장소는 그 사고를 이미 여러 번 겪었다.
    /// 2. **멱등이다.** 루트를 찾아 자식을 통째로 지우고 다시 만든다. 「없으면 만든다」는
    ///    두 번째 실행에서 고칠 기회를 잃고, 「현재 값에 곱한다」는 실행할 때마다 밀린다
    ///    (`Reproportion Elevator Car` 가 계기판을 두 번 밀었던 그 사고).
    /// 3. **명세 위반을 조립 전에 막는다.** <see cref="ReferenceRoomSpec.Violations"/> 가
    ///    비어 있지 않으면 아무것도 만들지 않고 멈춘다. 위반한 방을 세워 놓고
    ///    캡처로 발견하는 것이 가장 비싸다.
    /// 4. **모듈 이름은 명세 §12 그대로다.** 이름이 규약이므로 바꾸면 배선이 끊긴다.
    ///
    /// ## 이 조립기가 만들지 않는 것
    ///
    /// - **게임플레이 컴포넌트를 배선하지 않는다.** 기존 씬의 `SpinBoardView` ·
    ///   `InteractableLever` · `InstrumentPanelView` 등은 fileID 로 서로를 참조하고
    ///   있고, 그 참조는 **트랜스폼을 옮겨도 살아남는다.** 그래서 배치와 배선을
    ///   분리한다 — 이 파일은 형상만, 배선 이전은 <see cref="AscendReferenceRoomRewire"/>.
    /// - 텍스처. 표면 합성은 `AscendSurfaceSynth` 가 이미 하고 있고, 그쪽 소유다.
    /// </summary>
    public static class AscendReferenceRoom
    {
        public const string RootName = "ReferenceRoom";
        private const string MaterialDir = "Assets/Prototype_Elevator/Art/Materials/Room";

        // 명세 §12 의 모듈 이름. **문자열 상수로 둔다** — 배선 스크립트가 같은 이름을
        // 찾으므로, 오타가 나면 조용히 못 찾고 배선이 비어 버린다.
        public const string ShellName        = "ElevatorShell";
        public const string GateName         = "LeftScissorGate";
        public const string GrateName        = "FloorCenterGrate";
        public const string BorderName       = "FloorBorderPlates";
        public const string MachineName      = "SoulMachineFrame";
        public const string WindowModuleName = "SoulWindowModule";
        public const string GlassName        = "SoulWindowGlass";
        public const string SoulName         = "SoulObject";
        // ── 공통 잠금 기구 (2026-08-03) ──
        // 연출(`CustomsLockView`)이 **이름으로** 찾아 움직인다. 오타가 나면 조용히
        // 아무것도 안 움직이고, 그건 「레버를 당겼는데 외부 연결부는 가만히 있는」
        // 지시가 실패로 규정한 바로 그 상태다.
        public const string CommonShaftName  = "CommonLockShaft";
        public const string StatusTabName    = "LockStatusTab";
        public const string LockClampName    = "LockClamp";
        public const string DriveRodName     = "LeverDriveRod";
        public const string LeverBaseName    = "ExecutionLeverBase";
        public const string LeverHandleName  = "ExecutionLeverHandle";
        /// <summary>잠금핀. 잠긴 상태에서 암을 **물리적으로** 막는 부품이라
        /// 연출(<c>LeverLockView</c>)이 이름으로 찾아 쓴다.</summary>
        public const string LeverLockPinName = "LeverLockPin";
        public const string WarningLampName  = "WarningLamp";
        public const string PowerMeterName   = "PowerMeter";
        public const string ShelfName        = "StorageShelf";
        public const string PropsName        = "StorageProps";
        public const string CeilingLampName  = "CeilingLamp";
        public const string SignsName        = "SafetySigns";

        private static readonly Dictionary<string, Material> Palette = new Dictionary<string, Material>();
        private static StringBuilder _report;

        // ══════════════════════════════════════════════════════════════════════

        [MenuItem("Ascend/Room/Build Reference Room")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            { Debug.LogError("[상승] Play 모드에서는 씬을 고치지 않는다."); return; }

            // ── 명세 위반이면 아무것도 만들지 않는다 ──
            string[] violations = ReferenceRoomSpec.Violations();
            if (violations.Length > 0)
            {
                Debug.LogError("[상승] 명세 위반 " + violations.Length + "건 — 조립을 중단한다.\n  "
                               + string.Join("\n  ", violations));
                return;
            }

            Scene scene = AscendGraphicsBuilder.EnsureScene();
            if (!scene.IsValid()) return;

            _report = new StringBuilder("[상승] 레퍼런스 룸 조립 (명세 2026-08-02)\n");
            Palette.Clear();
            // **반드시 비운다.** 이 캐시는 정적이라 도메인이 살아 있는 동안 남는다.
            // 안 비우면 두 번째 실행이 첫 실행의 메시 객체를 그대로 돌려주고,
            // 그 사이에 형상 코드를 고쳤다면 **바뀌지 않은 방이 나온다** —
            // 「고쳤는데 화면이 그대로다」로 나타나는 가장 찾기 어려운 종류의 결함이다.
            BakedMeshes.Clear();
            EnsurePalette();

            GameObject root = ResetRoot(scene);

            BuildShell(root);
            BuildFloor(root);
            BuildCeilingAndLamp(root);
            BuildScissorGate(root);
            BuildSoulMachine(root);
            BuildLeverColumn(root);
            BuildPowerMeter(root);
            BuildStorage(root);
            AddColliders(root);
            PlaceCamera();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            _report.AppendLine($"  실내 {ReferenceRoomSpec.InteriorWidth} × {ReferenceRoomSpec.InteriorDepth} × {ReferenceRoomSpec.InteriorHeight} m");
            _report.AppendLine($"  중앙 이동 공간 {ReferenceRoomSpec.ClearSpanX:F2} × {ReferenceRoomSpec.ClearSpanZ:F2} m " +
                               $"(요구 {ReferenceRoomSpec.RequiredClearX} × {ReferenceRoomSpec.RequiredClearZ})");
            _report.AppendLine("  명세 위반 0건");
            Debug.Log(_report.ToString());
        }

        // ══════════════════════════════════════════════════════════════════════
        //  루트와 팔레트
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 루트를 찾아 **자식을 통째로 지운다.** 이것이 멱등성의 전부다.
        /// 「이미 있으면 재사용」은 전 실행이 남긴 잘못된 자식을 그대로 물려받는다.
        /// </summary>
        private static GameObject ResetRoot(Scene scene)
        {
            GameObject root = null;
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.name == RootName) { root = go; break; }

            if (root == null)
            {
                root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, scene);
                _report.AppendLine($"  {RootName} 신설");
            }
            else
            {
                // 🔴 **파괴 전에 남의 물건을 빼낸다.** 2026-08-03 에 이것 없이 돌려
                // 결과판 9칸을 통째로 날렸다(`SpinBoardView._cells` 가 9/9 null 이 됐다).
                //
                // 원인은 두 파일의 전제가 어긋난 것이다 —
                //   `AscendReferenceRoomRewire.MoveBoardCells` 는 칸을 이 루트 **안으로**
                //   옮기고, 여기 `ResetRoot` 는 이 루트의 자식을 **전부** 지운다.
                //   각각은 옳고, 둘을 이어 놓으면 재조립이 게임플레이를 파괴한다.
                //
                // 그리고 이 손상은 **조용하다.** 컴파일도 되고 조립 보고서도 정상이며,
                // 다음 PlayMode 에서 결과판이 안 그려질 때야 드러난다.
                // 그래서 감지가 아니라 **원리적으로 불가능**하게 만든다.
                int rescued = RescueForeignCarriers(root.transform, scene);
                int killed = root.transform.childCount;
                for (int i = killed - 1; i >= 0; i--)
                    Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                _report.AppendLine($"  {RootName} 재구성 — 기존 자식 {killed}개 제거" +
                                   (rescued > 0 ? $" · 게임플레이 운반체 {rescued}개 구조" : ""));
            }

            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;
            return root;
        }

        /// <summary>재조립에서 구조된 게임플레이 운반체가 잠시 머무는 곳.</summary>
        public const string RescueRootName = "RescuedFromRebuild";

        /// <summary>
        /// 조립기가 **만들지 않은** 오브젝트를 파괴 전에 루트 밖으로 빼낸다.
        ///
        /// 판정 기준은 「누가 만들었나」가 아니라 **「무엇을 들고 있나」**다.
        /// 이름으로 가르면 반드시 틀린다 — 이 저장소가 이미 그렇게 당했다
        /// (`ParkLegacyVisuals` 가 눈으로 껍데기와 게임플레이를 갈랐고,
        ///  눈은 컴포넌트를 못 봐서 10층 검증이 첫 줄에서 죽었다).
        ///
        /// 그래서 `Ascend.` 네임스페이스의 컴포넌트나 TMP 텍스트를 들고 있으면
        /// 무조건 구조한다. 조립기가 스스로 붙이는 것들만 예외다.
        ///
        /// **원래 부모 이름을 보존해서** park 한다. 아홉 칸이 전부 `Cell_0`~`Cell_2`
        /// 라 평평하게 모아 두면 어느 칸이 어느 창이었는지 잃는다.
        /// </summary>
        private static int RescueForeignCarriers(Transform root, Scene scene)
        {
            var carriers = new List<Transform>();
            CollectForeign(root, carriers);
            if (carriers.Count == 0) return 0;

            GameObject park = null;
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.name == RescueRootName) { park = go; break; }
            if (park == null)
            {
                park = new GameObject(RescueRootName);
                SceneManager.MoveGameObjectToScene(park, scene);
            }

            foreach (Transform t in carriers)
            {
                string parentName = t.parent != null ? t.parent.name : "_";
                Transform slot = park.transform.Find(parentName);
                if (slot == null)
                {
                    var s = new GameObject(parentName);
                    s.transform.SetParent(park.transform, false);
                    slot = s.transform;
                }
                t.SetParent(slot, false);
            }
            return carriers.Count;
        }

        private static void CollectForeign(Transform t, List<Transform> found)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                Transform c = t.GetChild(i);
                // 운반체를 찾으면 **자식째** 빼낸다. 심볼 세 개가 칸의 자식이다.
                if (IsForeignCarrier(c)) { found.Add(c); continue; }
                CollectForeign(c, found);
            }
        }

        private static bool IsForeignCarrier(Transform t)
        {
            var comps = t.GetComponents<MonoBehaviour>();
            for (int i = 0; i < comps.Length; i++)
            {
                MonoBehaviour mb = comps[i];
                if (mb == null) continue;                      // 스크립트가 빠진 컴포넌트
                System.Type ty = mb.GetType();
                // 조립기가 직접 붙이는 것 — 이건 조립기 소유다.
                if (ty == typeof(Prototype.View.LeverStateMachine)) continue;
                if (mb is TMPro.TMP_Text) return true;
                if (ty.Namespace != null && ty.Namespace.StartsWith("Ascend.")) return true;
            }
            // 결과판 칸은 **컴포넌트가 없는 순수 트랜스폼**이다(`SpinBoardView` 가
            // 자식 이름으로 심볼을 켠다). 컴포넌트 검사만으로는 절대 안 걸린다 —
            // 실제로 그래서 파괴됐다. 자식 심볼 이름으로 알아본다.
            for (int i = 0; i < t.childCount; i++)
                if (Prototype.View.SpinBoardView.KindOf(t.GetChild(i).name) != Prototype.Spin.SymbolKind.Empty)
                    return true;
            return false;
        }

        /// <summary>
        /// 명세 §10 의 재질 5~7종. **그 이상 만들지 않는다** — 명세가 통일을 요구한다.
        /// `.mat` 에셋으로 굽는 이유는 씬에 인라인 머티리얼이 쌓이면 YAML 이 부풀고
        /// 머지가 위험해지기 때문이다(이 저장소에 이미 24개가 인라인으로 들어 있다).
        /// </summary>
        private static void EnsurePalette()
        {
            if (!Directory.Exists(MaterialDir)) Directory.CreateDirectory(MaterialDir);

            // 명세 §10 「주요 재질은 5~7개 안에서 통일한다」.
            //
            // ⚠ **여기 있는 것은 「화면에서 보일 색」이 아니라 반사율이다.**
            // 명세 §10 의 「철판: 검은색에 가까운 짙은 회갈색」을 반사율 0.126 으로
            // 받아 적었다가 방이 **통째로 검게** 나왔다(p50 = 0). 빛이 되돌아올 것이
            // 없으면 그건 「어둡다」가 아니라 「없다」다.
            //
            // 레퍼런스 이미지의 금속도 중간 회색 반사율이고, 검게 보이는 이유는
            // 광원이 하나뿐이고 빠르게 감쇠하기 때문이다. **어둠은 조명이 만든다.**
            Mat("Steel",     new Color(0.340f, 0.318f, 0.272f));   // ① 검게 도장된 철판
            Mat("BareSteel", new Color(0.455f, 0.440f, 0.404f));   // ② 도장 벗겨진 강철
            Mat("Rust",      new Color(0.520f, 0.286f, 0.160f));   // ③ 녹
            Mat("Glass",     new Color(0.230f, 0.222f, 0.210f));   // ④ 긁힌 두꺼운 유리
            Mat("RedPaint",  new Color(0.560f, 0.140f, 0.110f));   // ⑤ 붉은 도장 금속
            Mat("Sign",      new Color(0.760f, 0.716f, 0.592f));   // ⑥ 낡은 크림색 표지판
            Mat("Grease",    new Color(0.190f, 0.180f, 0.166f));   // ⑦ 검은 고무·기름때

            // ⑧ **모듈 칼라 전용** — 영웅 오브젝트 명세의 재질 계층이 요구한다:
            //    「벽: 무광 어두운 철판 / 메인 프레임: 벽보다 약간 더 어두운 도장 강철 /
            //     모듈 칼라: 마모되어 밝은 금속이 드러난 산화 강철」
            // 칼라가 벽·프레임과 같은 재질이면 장치가 벽으로 뭉개진다. 그것이
            // 근접 캡처에서 칼라가 「자갈」로 읽힌 이유이기도 하다.
            Mat("Collar",    new Color(0.585f, 0.560f, 0.512f));

            // ⑨ **챔버 내부 — 거의 검다.**
            //
            // 근접 캡처에서 챔버 안이 밝게 떠 「벽에 뚫린 구멍」으로 읽혔다.
            // 안쪽이 밝으면 영혼이 어둠 속에서 빛나는 게 아니라 **밝은 배경 위에
            // 놓인 도형**이 된다 — 없애려던 「스티커」의 다른 형태다.
            // 반사율 0.055 는 이 방의 어떤 표면보다도 낮다. 의도다.
            Mat("ChamberDark", new Color(0.055f, 0.050f, 0.048f));
        }

        /// <summary>
        /// 재질별 (거칠기 보정, 금속성). 명세 §10 「거칠기 대체로 0.70~0.95 ·
        /// 모서리와 손이 닿은 부분만 부분적으로 매끄럽게 마모」.
        ///
        /// URP 는 거칠기가 아니라 **매끄러움**을 받으므로 `1 - roughness` 를 넣는다.
        /// 0.70~0.95 거칠기 = 0.05~0.30 매끄러움이다.
        /// </summary>
        private static (float smoothness, float metallic) Finish(string key)
        {
            switch (key)
            {
                case "Steel":     return (0.16f, 0.75f);   // 도장된 철판 — 약하게 반사
                case "BareSteel": return (0.30f, 0.90f);   // 노출 강철 — 손이 닿는 곳
                case "Rust":      return (0.05f, 0.15f);   // 녹은 금속이 아니다
                // ⚠ 0.62 로는 램프의 스페큘러가 걸리지 않아 **유리가 화면에서
                // 사라졌다.** 뒤가 검은 챔버라 투과로도 안 읽힌다 — 유리는
                // 「비침」이 아니라 **반사**로 존재를 드러낸다.
                case "Glass":     return (0.90f, 0.05f);   // 긁힌 두꺼운 유리
                case "RedPaint":  return (0.22f, 0.30f);   // 도장
                case "Sign":      return (0.10f, 0.00f);   // 종이·에나멜 표지판
                case "Grease":    return (0.08f, 0.35f);   // 기름때 낀 고무·금속
                // 칼라는 손과 공구가 닿는 곳이라 **가장 매끄럽고 가장 금속답다.**
                case "Collar":    return (0.42f, 0.95f);
                // 그을음이 앉은 내부 — 반짝이면 다시 밝아 보인다.
                case "ChamberDark": return (0.05f, 0.10f);
                default:          return (0.15f, 0.50f);
            }
        }

        /// <summary>
        /// 🔴 **`Ascend/Stylized` 를 쓰지 않는다 — URP/Lit 을 쓴다.** (2026-08-02 사용자 지시)
        ///
        /// 사용자가 레퍼런스 이미지를 기준으로 못박으면서 「현재 비주얼 우선순위는
        /// 기존 PS1 스타일 문구보다 레퍼런스의 공간 구조·조명·재질·배치를 우선한다」고
        /// 지시했다. 그 지시가 이 한 줄을 바꾼다.
        ///
        /// 왜 이것이 결정적인가: `Ascend/Stylized` 의 `Quantize` 는
        /// `v &lt; 1/steps` 를 **정확히 0** 으로 만든다. URP 점광의 감쇠는 역제곱이라
        /// 실내 거리에서 0.05~0.3 에 몰리고, 결과는 두 가지 중 하나뿐이었다 —
        /// **완전한 검정**(`_BandFloor` 없이) 또는 **완전히 평평한 조명**(`_BandFloor` 로
        /// 첫 칸을 들어 올리면 모든 표면이 같은 칸에 들어간다). 이 세션에서 다섯 번
        /// 측정했고 다섯 번 다 그 둘 사이였다.
        ///
        /// 레퍼런스가 요구하는 것은 그 중간이 아니라 **연속적인 감쇠**다 — 등 아래는
        /// 밝고 구석은 죽는다. 그건 계단 셰이더가 원리적으로 못 만든다.
        /// 산업 금속의 무게감도 마찬가지로 거칠기·금속성 반사에서 나오는데
        /// 그 셰이더는 비물리라 그 축이 아예 없다.
        ///
        /// PS1 감각이 필요하면 **포스트 처리**로 건다(해상도·디더·그레인).
        /// 표면 셰이딩에 섞으면 이번처럼 둘 다 잃는다.
        /// </summary>
        private static Material Mat(string key, Color color)
        {
            string path = $"{MaterialDir}/RM_{key}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            Material m = existing ?? AscendMaterialFactory.Create($"RM_{key}", color, false, out _);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null && m.shader != lit) m.shader = lit;

            (float smoothness, float metallic) f = Finish(key);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", f.smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", f.metallic);
            // 스펙큘러 하이라이트와 환경 반사를 켠다 — 이게 금속을 금속으로 보이게 한다.
            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 1f);
            if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 1f);

            // ── 유리는 **투명이어야 한다** ──────────────────────────────────
            //
            // ⚠ 이 블록이 없으면 관찰창 유리가 불투명이 되어 **영혼 구슬을 통째로
            // 가린다.** URP/Lit 으로 옮기면서 실제로 그렇게 됐고, 화면에서 아홉 창이
            // 전부 빈 회색 원으로 보였다. 한 번 손으로 고쳤는데 다음 조립에서 되돌아갔다 —
            // **조립기가 정본이므로 조립기가 알아야 한다.**
            if (key == "Glass")
            {
                Transparent(m);
                color.a = 0.34f;              // 구슬이 비치되 유리가 있다는 것은 읽힌다
            }
            else
            {
                Opaque(m);
                color.a = 1f;
            }

            // **항상 전 필드를 다시 쓴다.** 재사용하되 상태는 새로 만든 것과 같아야 한다.
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);

            // ── 이 두 줄이 없으면 방이 검게 나온다 ──────────────────────────
            //
            // `Ascend/Stylized` 의 `Quantize` 는 `v < 1/steps` 에서 정확히 0 이다.
            // URP 점광의 `distanceAttenuation` 은 역제곱이라 2.7m 에서 약 0.137 이고
            // 4단 계단의 첫 칸 경계 0.25 를 못 넘는다 — 즉 **이 방의 거의 모든 표면에서
            // 직접광 항이 통째로 사라진다.** `range` 를 늘려도 역제곱이 지배해 소용없다.
            //
            // `_BandFloor` 가 칸 값의 바닥만 들어 올린다. 기본값 0 은 종전과 수식적으로
            // 동일하므로 이 파일 밖의 머티리얼은 영향을 받지 않는다.
            if (m.HasProperty("_BandFloor")) m.SetFloat("_BandFloor", 0.55f);

            // ⚠ **`_ShadeGamma` 를 켜지 않는다 — 두 번 시도했고 두 번 다 검게 나왔다.**
            //
            //   ① 복원 단계 포함  → p99 47 → 1
            //   ② 복원 단계 제거  → p50 45 → 0
            //
            // ②는 산술적으로 **밝아져야** 했다. `pow(0.137, 1/2.6) = 0.465` 이고
            // 그러면 4단 중 1번 칸에 들어가 직전(0번 칸)보다 밝다. 예측과 반대 결과가
            // 나왔다는 것은 원인이 이 수식 밖에 있다는 뜻이고, 후보는 셰이더 변형
            // (`shader_feature_local` 조합)이 실제로 컴파일되는지 쪽이다.
            //
            // **근거 없이 세 번째를 시도하지 않는다** (`CLAUDE.md` 「동일 오류를 세 번
            // 이상 같은 방식으로 수정하지 않는다」). 측정으로 확인된 구성만 남긴다.
            //
            // 남은 대가: 첫 칸이 넓어 조명 계조가 평평하다. 이건 열린 결함이고
            // 진행 문서에 적었다 — 숨기지 않는다.
            if (m.HasProperty("_ShadeGammaEnabled")) m.SetFloat("_ShadeGammaEnabled", 0f);
            m.DisableKeyword("_SHADEGAMMA_ON");

            // 감쇠 거듭제곱은 **방향광**을 전제로 정해진 기본값(2.5)이다. 이 방의 주광은
            // 점광이라 그 손잡이가 처음으로 실제로 작동하고, 2.5 제곱은 실내 거리에서
            // 빛을 한 번 더 짓누른다. 「빠른 감쇠」는 광원의 range 와 ndotl 이 만든다.
            if (m.HasProperty("_FalloffPower")) m.SetFloat("_FalloffPower", 1.0f);
            if (m.HasProperty("_ShadowLift")) m.SetFloat("_ShadowLift", 0.75f);


            // 명세 §14 「디더링된 그림자와 색상 전환」 — 색 심도 양자화를 켠다.
            // 이 셰이더의 규약상 키워드를 켜야 코드가 컴파일된다.
            if (m.HasProperty("_ColorDepthEnabled"))
            {
                m.SetFloat("_ColorDepthEnabled", 1f);
                m.SetFloat("_ColorDepth", 20f);
                m.SetFloat("_DitherStrength", 1f);
                m.EnableKeyword("_COLORDEPTH_ON");
            }

            if (existing == null) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);

            Palette[key] = m;
            return m;
        }

        private static Material P(string key) => Palette.TryGetValue(key, out Material m) ? m : null;

        // ══════════════════════════════════════════════════════════════════════
        //  §9 셸 — 벽·천장
        // ══════════════════════════════════════════════════════════════════════

        private static void BuildShell(GameObject root)
        {
            GameObject shell = Child(root.transform, ShellName);

            float w = ReferenceRoomSpec.InteriorWidth;
            float d = ReferenceRoomSpec.InteriorDepth;
            float h = ReferenceRoomSpec.InteriorHeight;
            float t = ReferenceRoomSpec.ShellThickness;

            // 벽 네 장 + 천장. 바닥은 §8 이 따로 만든다.
            // 안쪽면이 정확히 명세 좌표에 오도록 **중심을 두께의 절반만큼 밖으로** 민다.
            Slab(shell.transform, "Wall_Left",  new Vector3(ReferenceRoomSpec.WallLeftX - t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d + t * 2f), "Steel");
            Slab(shell.transform, "Wall_Right", new Vector3(ReferenceRoomSpec.WallRightX + t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d + t * 2f), "Steel");
            Slab(shell.transform, "Wall_Rear",  new Vector3(0f, h * 0.5f, ReferenceRoomSpec.WallRearZ + t * 0.5f), new Vector3(w, h, t), "Steel");
            Slab(shell.transform, "Wall_Front", new Vector3(0f, h * 0.5f, ReferenceRoomSpec.WallFrontZ - t * 0.5f), new Vector3(w, h, t), "Steel");
            Slab(shell.transform, "Ceiling",    new Vector3(0f, h + t * 0.5f, 0f), new Vector3(w, t, d), "Steel");

            // ── 수평 보강 레일. 명세 §9 「허리 높이와 천장 가까이」 ──
            // 왼쪽 벽에는 걸지 않는다 — 거기는 가위문 개구부다.
            foreach (float y in new[] { ReferenceRoomSpec.WallRailLowY, ReferenceRoomSpec.WallRailHighY })
            {
                string tag = Mathf.Approximately(y, ReferenceRoomSpec.WallRailLowY) ? "Low" : "High";
                float p = ReferenceRoomSpec.WallRailProtrusion;
                Slab(shell.transform, $"Rail_Right_{tag}",
                     new Vector3(ReferenceRoomSpec.WallRightX - p * 0.5f, y, 0f),
                     new Vector3(p, ReferenceRoomSpec.WallRailHeight, d), "BareSteel");
                // 🔴 **후면 레일은 장치를 피해 왼쪽 구간만 지난다.**
                //
                // 독립 평가자가 「벽 몰딩이 레버 플레이트로 그대로 파고든다 —
                // 접합이 아니라 관통이다」를 지적했다. 실제로 낮은 레일(y=0.95)이
                // 캐비닛과 레버 베이스 플레이트를 통과하고 있었다.
                //
                // 실제 벽 보강재는 벽에 붙은 기계를 관통하지 않는다. 장치가 차지한
                // 구간에서 끊고 **왼쪽 빈 벽에만** 남긴다 — 그 구간이 바로
                // 「장치 왼쪽에 남는 후면 벽」이고, 거기서는 레일이 제 역할을 한다.
                float machineLeft = ReferenceRoomSpec.MachineLeftX - 0.06f;
                float railSpan = machineLeft - ReferenceRoomSpec.WallLeftX;
                if (railSpan > 0.10f)
                    Slab(shell.transform, $"Rail_Rear_{tag}",
                         new Vector3((ReferenceRoomSpec.WallLeftX + machineLeft) * 0.5f, y,
                                     ReferenceRoomSpec.WallRearZ - p * 0.5f),
                         new Vector3(railSpan, ReferenceRoomSpec.WallRailHeight, p), "BareSteel");
                Slab(shell.transform, $"Rail_Front_{tag}",
                     new Vector3(0f, y, ReferenceRoomSpec.WallFrontZ + p * 0.5f),
                     new Vector3(w, ReferenceRoomSpec.WallRailHeight, p), "BareSteel");
            }

            // ── 세로 철판 이음매. 명세 §9 「세로 철판 이음매」 ──
            // 형상은 얇은 돌출 하나다 — 명세 §14 가 「작은 디테일은 지오메트리보다
            // 픽셀 텍스처로」라고 못박으므로 여기서는 **판 경계만** 세운다.
            BuildSeams(shell.transform, "Seam_Rear", w, ReferenceRoomSpec.WallRearZ, true);
            BuildSeams(shell.transform, "Seam_Front", w, ReferenceRoomSpec.WallFrontZ, false);

            _report.AppendLine($"  {ShellName} — 벽 4 · 천장 1 · 보강 레일 6 · 이음매");
        }

        private static void BuildSeams(Transform parent, string prefix, float wallWidth, float z, bool rear)
        {
            float pitch = ReferenceRoomSpec.WallPlateSeamPitch;
            int count = Mathf.Max(1, Mathf.RoundToInt(wallWidth / pitch) - 1);
            float step = wallWidth / (count + 1);
            float inset = 0.012f;
            float zc = rear ? z - inset * 0.5f : z + inset * 0.5f;

            for (int i = 1; i <= count; i++)
            {
                float x = -wallWidth * 0.5f + step * i;
                Slab(parent, $"{prefix}_{i}", new Vector3(x, ReferenceRoomSpec.InteriorHeight * 0.5f, zc),
                     new Vector3(0.03f, ReferenceRoomSpec.InteriorHeight, inset), "BareSteel");
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  §8 바닥
        // ══════════════════════════════════════════════════════════════════════

        private static void BuildFloor(GameObject root)
        {
            GameObject border = Child(root.transform, BorderName);
            float t = ReferenceRoomSpec.ShellThickness;

            // 테두리 철판. 중앙 타공판이 앉을 자리만 비운 **틀**이다.
            // 통짜 바닥 위에 타공판을 얹으면 명세 §8 의 「약간 낮은」이 성립하지 않는다.
            float bx = ReferenceRoomSpec.FloorBorderX;
            float bz = ReferenceRoomSpec.FloorBorderZ;
            float w = ReferenceRoomSpec.InteriorWidth;
            float d = ReferenceRoomSpec.InteriorDepth;
            float gw = ReferenceRoomSpec.GrateWidth;
            float gd = ReferenceRoomSpec.GrateDepth;

            Slab(border.transform, "Border_Left",  new Vector3(ReferenceRoomSpec.WallLeftX + bx * 0.5f, -t * 0.5f, 0f), new Vector3(bx, t, d), "Steel");
            Slab(border.transform, "Border_Right", new Vector3(ReferenceRoomSpec.WallRightX - bx * 0.5f, -t * 0.5f, 0f), new Vector3(bx, t, d), "Steel");
            Slab(border.transform, "Border_Front", new Vector3(0f, -t * 0.5f, ReferenceRoomSpec.WallFrontZ + bz * 0.5f), new Vector3(gw, t, bz), "Steel");
            Slab(border.transform, "Border_Rear",  new Vector3(0f, -t * 0.5f, ReferenceRoomSpec.WallRearZ - bz * 0.5f), new Vector3(gw, t, bz), "Steel");

            // ── 중앙 타공 철판 ──
            // 명세 §14 「볼트, 녹, 긁힘 같은 작은 디테일은 실제 지오메트리보다 픽셀
            // 텍스처로 표현」 — 타공을 실제 구멍으로 뚫지 않는다. 2.7 × 3.5 m 에
            // 0.11 m 간격이면 구멍이 780개이고, 그것을 기하로 만들면 로우폴리 락이
            // 이 바닥 하나로 무너진다. 낮은 판 하나 + 타공 텍스처가 명세의 지시다.
            GameObject grate = Child(root.transform, GrateName);
            Slab(grate.transform, "Plate",
                 new Vector3(0f, -ReferenceRoomSpec.GrateRecess - t * 0.5f, 0f),
                 new Vector3(gw, t, gd), "Grease");

            // 타공판 둘레의 단차 테. 「약간 낮다」가 실루엣에서 읽히게 한다.
            float lip = 0.035f;
            Slab(grate.transform, "Lip_Left",  new Vector3(-gw * 0.5f - lip * 0.5f, -ReferenceRoomSpec.GrateRecess * 0.5f, 0f), new Vector3(lip, ReferenceRoomSpec.GrateRecess, gd), "BareSteel");
            Slab(grate.transform, "Lip_Right", new Vector3( gw * 0.5f + lip * 0.5f, -ReferenceRoomSpec.GrateRecess * 0.5f, 0f), new Vector3(lip, ReferenceRoomSpec.GrateRecess, gd), "BareSteel");
            Slab(grate.transform, "Lip_Front", new Vector3(0f, -ReferenceRoomSpec.GrateRecess * 0.5f, -gd * 0.5f - lip * 0.5f), new Vector3(gw + lip * 2f, ReferenceRoomSpec.GrateRecess, lip), "BareSteel");
            Slab(grate.transform, "Lip_Rear",  new Vector3(0f, -ReferenceRoomSpec.GrateRecess * 0.5f,  gd * 0.5f + lip * 0.5f), new Vector3(gw + lip * 2f, ReferenceRoomSpec.GrateRecess, lip), "BareSteel");

            // ── 타이다운 링. 명세 §8 「바닥과 거의 평평하게 접히는 구조」 ──
            // 높이 12mm 라 이동을 방해하지 않는다. 벽 가까이에 둔다.
            var mesh = new ProcMeshBuilder();
            mesh.AddPrism(Vector3.zero, 0.055f, 0.055f, ReferenceRoomSpec.TieDownRingHeight,
                          8, MeshAxis.Y, 0f, true, true, false, ReferenceRoomSpec.HeroUvPerMeter);
            Mesh ringMesh = SaveMesh(mesh.ToMesh("TieDownRing"), "TieDownRing");

            int n = ReferenceRoomSpec.TieDownRingCount;
            float zSpan = ReferenceRoomSpec.GrateDepth * 0.5f - 0.25f;
            for (int i = 0; i < n; i++)
            {
                // 좌우 벽 가까이 번갈아. 짝수는 왼쪽, 홀수는 오른쪽.
                bool left = i % 2 == 0;
                float x = left ? -gw * 0.5f + 0.18f : gw * 0.5f - 0.18f;
                float z = Mathf.Lerp(-zSpan, zSpan, n <= 2 ? 0.5f : (i / 2) / Mathf.Max(1f, (n / 2f) - 1f));
                var go = new GameObject($"TieDownRing_{i}");
                go.transform.SetParent(grate.transform, false);
                go.transform.localPosition = new Vector3(x, -ReferenceRoomSpec.GrateRecess, z);
                Render(go, ringMesh, "BareSteel");
            }

            _report.AppendLine($"  {BorderName} · {GrateName} — 타공판 {gw} × {gd} m " +
                               $"(테두리 X {bx:F2} / Z {bz:F2}) · 타이다운 링 {n}개");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  §9·§11 천장 보강빔과 케이지 전구
        // ══════════════════════════════════════════════════════════════════════

        private static void BuildCeilingAndLamp(GameObject root)
        {
            GameObject shell = root.transform.Find(ShellName).gameObject;

            // 보강빔. 명세 §9 「굵은 사각 보강빔」 · §11 「천장 보강빔이 굵은 그림자를 만든다」.
            int beams = ReferenceRoomSpec.CeilingBeamCount;
            float step = ReferenceRoomSpec.InteriorDepth / (beams + 1);
            for (int i = 1; i <= beams; i++)
            {
                float z = ReferenceRoomSpec.WallFrontZ + step * i;
                Slab(shell.transform, $"CeilingBeam_{i}",
                     new Vector3(0f, ReferenceRoomSpec.InteriorHeight - ReferenceRoomSpec.CeilingBeamDrop * 0.5f, z),
                     new Vector3(ReferenceRoomSpec.InteriorWidth, ReferenceRoomSpec.CeilingBeamDrop, ReferenceRoomSpec.CeilingBeamWidth),
                     "BareSteel");
            }

            // ── 케이지 전구 ──
            // 명세 §9 「짧은 금속 베이스에 직접 고정 · 길게 늘어진 펜던트 조명은 사용하지 않는다」.
            GameObject lamp = Child(root.transform, CeilingLampName);
            lamp.transform.localPosition = new Vector3(0f, ReferenceRoomSpec.CageLampBottomY, 0f);

            float dia = ReferenceRoomSpec.CageLampDiameter;
            float hgt = ReferenceRoomSpec.CageLampHeight;

            var b = new ProcMeshBuilder();
            // 베이스 (천장에 붙는 짧은 원통)
            b.AddPrism(new Vector3(0f, hgt - 0.03f, 0f), dia * 0.30f, dia * 0.34f, 0.06f,
                       8, MeshAxis.Y, 0f, true, true, false, 4f);
            // 보호망 상단 링
            b.AddPrism(new Vector3(0f, hgt - 0.075f, 0f), dia * 0.5f, dia * 0.5f, 0.018f,
                       ReferenceRoomSpec.CageLampRibs * 2, MeshAxis.Y, 0f, true, true, false, 4f);
            // 보호망 하단 링
            b.AddPrism(new Vector3(0f, 0.02f, 0f), dia * 0.34f, dia * 0.34f, 0.018f,
                       ReferenceRoomSpec.CageLampRibs * 2, MeshAxis.Y, 0f, true, true, false, 4f);
            // 세로살
            for (int i = 0; i < ReferenceRoomSpec.CageLampRibs; i++)
            {
                float a = i * Mathf.PI * 2f / ReferenceRoomSpec.CageLampRibs;
                var top = new Vector3(Mathf.Cos(a) * dia * 0.47f, hgt - 0.08f, Mathf.Sin(a) * dia * 0.47f);
                var bot = new Vector3(Mathf.Cos(a) * dia * 0.32f, 0.03f, Mathf.Sin(a) * dia * 0.32f);
                b.AddBox((top + bot) * 0.5f, new Vector3(0.012f, (top - bot).magnitude, 0.012f),
                         Quaternion.FromToRotation(Vector3.up, (top - bot).normalized), 0f, 4f);
            }
            Mesh cageMesh = SaveMesh(b.ToMesh("CageLampCage"), "CageLampCage");
            var cage = new GameObject("Cage");
            cage.transform.SetParent(lamp.transform, false);
            Render(cage, cageMesh, "BareSteel");

            // 전구. 명세 §10 「조명: 탁한 황백색」. 발광은 `RiskStateView` 가 런타임에
            // 덮으므로 여기서는 **형상과 기본 발광만** 준다.
            var bulbB = new ProcMeshBuilder();
            bulbB.AddPrism(Vector3.zero, dia * 0.10f, dia * 0.26f, hgt * 0.34f, 8, MeshAxis.Y, 0f, true, true, false, 6f);
            bulbB.AddPrism(new Vector3(0f, hgt * 0.24f, 0f), dia * 0.26f, dia * 0.10f, hgt * 0.14f, 8, MeshAxis.Y, 0f, true, true, false, 6f);
            Mesh bulbMesh = SaveMesh(bulbB.ToMesh("CageLampBulb"), "CageLampBulb");

            var bulb = new GameObject("Bulb");
            bulb.transform.SetParent(lamp.transform, false);
            bulb.transform.localPosition = new Vector3(0f, hgt * 0.30f, 0f);
            Renderer bulbRenderer = Render(bulb, bulbMesh, "Sign");
            var glow = new Material(bulbRenderer.sharedMaterial) { name = "RM_BulbGlow" };
            glow.SetColor("_BaseColor", ReferenceRoomSpec.CageLampColor);
            glow.SetColor("_EmissionColor", ReferenceRoomSpec.CageLampColor * 3.4f);
            glow.EnableKeyword("_EMISSION");
            glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            SaveMaterial(glow, "BulbGlow");
            bulbRenderer.sharedMaterial = glow;

            // ── 주광 ──
            // 명세 §11 「주 광원은 천장의 케이지 전구 하나」 · 색온도 2700~3000K.
            // 이름을 `CabinLight` 로 둔다 — `RiskStateView` 와 기존 배선이 그 이름을 쓴다.
            var lightGo = new GameObject("CabinLight");
            lightGo.transform.SetParent(lamp.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, hgt * 0.45f, 0f);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = ReferenceRoomSpec.CageLampColor;
            // ⚠ 실측으로 정해진 값이다. `RiskStateView._baseLightIntensity` 도 배선기가
            // 같은 값으로 묶는다 — 안 묶으면 플레이 모드에서만 어두워진다.
            light.intensity = 5.4f;
            // 사거리는 방 대각선(약 5.4m)보다 짧게 — 명세 §11 「벽 모서리는 거의 검게」.
            light.range = 4.4f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.9f;

            // 앰비언트는 **거의 0** 이다. 어둠은 조명이 만들어야 하고,
            // 앰비언트로 들어 올리면 명세 §11 「벽 모서리와 선반 아래는 거의 검게」가 무너진다.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.030f, 0.026f, 0.021f, 1f);

            _report.AppendLine($"  {CeilingLampName} — 케이지 지름 {dia} · 높이 {hgt} · 필라멘트 y={ReferenceRoomSpec.CageLampFilament.y:F2} " +
                               $"· 보강빔 {beams}개");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  §3 왼쪽 가위문
        // ══════════════════════════════════════════════════════════════════════

        private static void BuildScissorGate(GameObject root)
        {
            GameObject gate = Child(root.transform, GateName);
            gate.transform.localPosition = new Vector3(ReferenceRoomSpec.WallLeftX, 0f, 0f);

            float ow = ReferenceRoomSpec.GateOpeningWidth;
            float oh = ReferenceRoomSpec.GateOpeningHeight;
            float prot = ReferenceRoomSpec.GateProtrusion;

            // 개구부를 뚫기 위해 좌벽을 네 조각으로 다시 만든다. 이 조각들은
            // `ElevatorShell/Wall_Left` 를 **대체하지 않고 가린다** — 셸을 뚫으면
            // 셸 조립이 가위문을 알아야 하고, 그러면 두 모듈이 서로를 안다.
            float t = ReferenceRoomSpec.ShellThickness;
            float d = ReferenceRoomSpec.InteriorDepth;
            float h = ReferenceRoomSpec.InteriorHeight;
            float side = (d - ow) * 0.5f;

            Slab(gate.transform, "Jamb_Front", new Vector3(prot * 0.5f, h * 0.5f, -ow * 0.5f - side * 0.5f), new Vector3(prot, h, side), "Steel");
            Slab(gate.transform, "Jamb_Rear",  new Vector3(prot * 0.5f, h * 0.5f,  ow * 0.5f + side * 0.5f), new Vector3(prot, h, side), "Steel");
            Slab(gate.transform, "Header",     new Vector3(prot * 0.5f, oh + (h - oh) * 0.5f, 0f), new Vector3(prot, h - oh, ow), "Steel");

            // 승강로 어둠. 명세 §3 「문이 닫혔을 때도 바깥의 어두운 승강로가 좁은 틈 사이로 보인다」.
            Slab(gate.transform, "ShaftBackdrop", new Vector3(-0.42f, oh * 0.5f, 0f), new Vector3(0.05f, oh, ow), "Grease");

            // 가이드 레일. 명세 §3 「바닥과 천장에 각각 철제 가이드 레일」.
            float rail = ReferenceRoomSpec.GateRailSize;
            Slab(gate.transform, "Rail_Floor",   new Vector3(prot * 0.5f, rail * 0.5f, 0f), new Vector3(prot + 0.02f, rail, ow), "BareSteel");
            Slab(gate.transform, "Rail_Ceiling", new Vector3(prot * 0.5f, oh - rail * 0.5f, 0f), new Vector3(prot + 0.02f, rail, ow), "BareSteel");

            // ── 접이식 격자 ──
            BuildGateLattice(gate.transform, ow, oh);

            // ── 층수 표시기 ──
            // 명세 §3 「폭 0.65 · 높이 0.22 · 오래된 검은 금속 프레임 · 붉은 세그먼트 · 좌우 삼각 표시등」.
            var indicator = new GameObject("FloorIndicatorPanel");
            indicator.transform.SetParent(gate.transform, false);
            indicator.transform.localPosition = new Vector3(prot, ReferenceRoomSpec.FloorIndicatorCenterY, 0f);
            // 좌벽에 붙으므로 +X 를 향한다.
            indicator.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            float iw = ReferenceRoomSpec.FloorIndicatorWidth;
            float ih = ReferenceRoomSpec.FloorIndicatorHeight;
            Slab(indicator.transform, "Bezel", new Vector3(0f, 0f, 0.02f), new Vector3(iw, ih, 0.04f), "Steel");
            Slab(indicator.transform, "Readout", new Vector3(0f, 0f, -0.002f), new Vector3(iw * 0.46f, ih * 0.62f, 0.01f), "Glass");
            // 좌우 삼각 상승·하강 표시등. 삼각형은 3각 프리즘으로 만든다.
            var arrowB = new ProcMeshBuilder();
            arrowB.AddPrism(Vector3.zero, ih * 0.20f, ih * 0.20f, 0.012f, 3, MeshAxis.Z, 0f, true, true, false, 8f);
            Mesh arrowMesh = SaveMesh(arrowB.ToMesh("IndicatorArrow"), "IndicatorArrow");
            for (int i = 0; i < 2; i++)
            {
                var a = new GameObject(i == 0 ? "ArrowUp" : "ArrowDown");
                a.transform.SetParent(indicator.transform, false);
                a.transform.localPosition = new Vector3(i == 0 ? -iw * 0.36f : iw * 0.36f, 0f, -0.004f);
                a.transform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 90f : -90f);
                Render(a, arrowMesh, "RedPaint");
            }

            // 문 주변 보조등. 명세 §3 「작은 보조등 하나만 · 출입구 전체를 밝게 비추지 말고」.
            var gateLightGo = new GameObject("GateLamp");
            gateLightGo.transform.SetParent(gate.transform, false);
            gateLightGo.transform.localPosition = new Vector3(prot + 0.12f, oh - 0.18f, -ow * 0.32f);
            Light gl = gateLightGo.AddComponent<Light>();
            gl.type = LightType.Point;
            gl.color = ReferenceRoomSpec.GateLampColor;
            gl.intensity = 0.55f;
            gl.range = 1.5f;              // 문살 일부와 바닥 레일만 닿는 거리
            gl.shadows = LightShadows.None;

            _report.AppendLine($"  {GateName} — 개구부 {ow} × {oh} · 돌출 {prot} · 층수 표시기 {iw} × {ih} @ y={ReferenceRoomSpec.FloorIndicatorCenterY}");
        }

        /// <summary>
        /// 명세 §3 의 접이식 가위문 격자.
        ///
        /// 「얇은 철망이 아니라 무겁고 낡은 평철이 교차하는 구조」이므로 두 방향의
        /// 평철 다발을 만든다. 기울기는 명세가 준 마름모 간격에서 나온다 —
        /// 가로 180mm 에 세로 380mm 이므로 수평에서 약 64.6°.
        ///
        /// X자 링크의 **리벳 축**을 실제 오브젝트로 남긴다(명세 §12 「가위문 링크:
        /// 각 리벳 회전축」). 애니메이션이 그 자리를 필요로 하고, 없으면 나중에
        /// 형상을 다시 뜯어야 한다.
        /// </summary>
        private static void BuildGateLattice(Transform parent, float openWidth, float openHeight)
        {
            var lattice = new GameObject("Lattice");
            lattice.transform.SetParent(parent, false);
            lattice.transform.localPosition = new Vector3(ReferenceRoomSpec.GateProtrusion * 0.5f, 0f, 0f);

            float bw = ReferenceRoomSpec.GateFlatBarWidth;
            float bt = ReferenceRoomSpec.GateFlatBarThickness;
            float pz = ReferenceRoomSpec.GateLatticePitchZ;
            float py = ReferenceRoomSpec.GateLatticePitchY;
            float railY = ReferenceRoomSpec.GateRailSize;
            float y0 = railY;
            float y1 = openHeight - railY;
            float span = y1 - y0;

            // 한 평철이 위아래를 가로지르며 z 방향으로 이동하는 양.
            float run = span * (pz / py);
            float uv = ReferenceRoomSpec.SurfaceUvPerMeter;

            var b = new ProcMeshBuilder();
            var rivets = new List<Vector3>();

            for (int dir = 0; dir < 2; dir++)
            {
                float sign = dir == 0 ? 1f : -1f;
                // 두 다발이 개구부를 덮도록 시작점을 run 만큼 넉넉히 잡는다.
                float zStart = -openWidth * 0.5f - run;
                float zEnd = openWidth * 0.5f + run;
                for (float z = zStart; z <= zEnd + 0.001f; z += pz * 2f)
                {
                    var a = new Vector3(0f, y0, z);
                    var c = new Vector3(0f, y1, z + run * sign);
                    Vector3 mid = (a + c) * 0.5f;
                    Vector3 axis = c - a;

                    // 개구부 밖으로 완전히 나간 막대는 만들지 않는다.
                    if (Mathf.Max(a.z, c.z) < -openWidth * 0.5f) continue;
                    if (Mathf.Min(a.z, c.z) > openWidth * 0.5f) continue;

                    Quaternion rot = Quaternion.FromToRotation(Vector3.up, axis.normalized);
                    b.AddBox(mid, new Vector3(bt, axis.magnitude, bw), rot, 0f, uv);

                    // 이 막대가 지나는 리벳 축(반대 다발과 만나는 높이)을 기록한다.
                    for (int k = 0; k <= Mathf.RoundToInt(span / py); k++)
                    {
                        float ty = y0 + py * k;
                        if (ty > y1 + 0.001f) break;
                        float tz = z + (ty - y0) * (pz / py) * sign;
                        if (tz < -openWidth * 0.5f || tz > openWidth * 0.5f) continue;
                        if (dir == 0) rivets.Add(new Vector3(0f, ty, tz));
                    }
                }
            }

            Mesh latticeMesh = SaveMesh(b.ToMesh("ScissorGateLattice"), "ScissorGateLattice");
            Render(lattice, latticeMesh, "BareSteel");

            // 리벳 축. 애니메이션 피벗이므로 **빈 트랜스폼**으로 둔다 — 렌더러를 붙이면
            // 수백 개의 드로우콜이 되고, 명세 §14 가 작은 디테일을 텍스처로 넘기라고 한다.
            var pivots = new GameObject("LinkPivots");
            pivots.transform.SetParent(lattice.transform, false);
            for (int i = 0; i < rivets.Count; i++)
            {
                var p = new GameObject($"LinkPivot_{i}");
                p.transform.SetParent(pivots.transform, false);
                p.transform.localPosition = rivets[i];
            }

            // 하단 롤러. 명세 §3 「문 하단에는 바닥 레일을 따라 움직이는 작은 금속 롤러」.
            var rollerB = new ProcMeshBuilder();
            rollerB.AddPrism(Vector3.zero, ReferenceRoomSpec.GateRollerRadius, ReferenceRoomSpec.GateRollerRadius,
                             0.022f, 8, MeshAxis.X, 0f, true, true, false, 8f);
            Mesh rollerMesh = SaveMesh(rollerB.ToMesh("GateRoller"), "GateRoller");
            int rollers = Mathf.Max(2, Mathf.RoundToInt(openWidth / (pz * 3f)));
            for (int i = 0; i < rollers; i++)
            {
                float z = Mathf.Lerp(-openWidth * 0.5f + 0.12f, openWidth * 0.5f - 0.12f, i / (float)(rollers - 1));
                var r = new GameObject($"Roller_{i}");
                r.transform.SetParent(lattice.transform, false);
                r.transform.localPosition = new Vector3(0f, railY + ReferenceRoomSpec.GateRollerRadius, z);
                Render(r, rollerMesh, "Grease");
            }

            _report.AppendLine($"     격자 — 평철 {bw * 1000f:F0}×{bt * 1000f:F0}mm · 마름모 {pz * 1000f:F0}×{py * 1000f:F0}mm " +
                               $"· 리벳 축 {rivets.Count}개 · 롤러 {rollers}개");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  §4 후면 3×3 영혼 통관 장치
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// **9실 영혼 통관 장치** — 하나의 밀폐 캐비닛 안에 동일 규격 챔버 9개.
        ///
        /// 🔴 2026-08-03 사용자 지시로 형태 아키텍처를 다시 정의했다. 직전 판본은
        /// 「원형 기계 9개를 벽에 붙인 것」이었고, 지금은 **하나의 캐비닛**이다.
        /// 무엇이 왜 사라졌는지는 각 자리에 적었다 — 지운 이유를 안 적으면
        /// 다음 사람이 「빠뜨렸구나」로 읽고 되돌린다.
        ///
        /// 다섯 부분만 만든다(지시 §전체 구조).
        ///   ① MountFrame   벽에 고정된 직사각형 하중 프레임
        ///   ② Cabinet      9개 챔버가 들어 있는 하나의 금속 캐비닛
        ///   ③ WindowGrid   각 챔버를 관찰하는 원형 압력창 9개
        ///   ④ ShaftHousing 세 뱅크를 동시에 제어하는 공통 잠금축
        ///   ⑤ (레버 컬럼은 <see cref="BuildLeverColumn"/>)
        /// </summary>
        private static void BuildSoulMachine(GameObject root)
        {
            GameObject machine = Child(root.transform, MachineName);
            // 장치 원점 = 후면 벽 안쪽면의 장치 중심. −Z 가 실내 쪽이다.
            machine.transform.localPosition = new Vector3(ReferenceRoomSpec.MachineCenterX, 0f, ReferenceRoomSpec.WallRearZ);

            float mw = ReferenceRoomSpec.MachineWidth;
            float mh = ReferenceRoomSpec.MachineHeight;
            float md = ReferenceRoomSpec.MachineDepth;
            float outer = ReferenceRoomSpec.OuterFrameBand;
            float rib = ReferenceRoomSpec.BankRibWidth;
            float bulk = ReferenceRoomSpec.BulkheadHeight;
            float uv = ReferenceRoomSpec.HeroUvPerMeter;
            float cy = ReferenceRoomSpec.MachineBottomY + mh * 0.5f;

            // 깊이 축의 기준면 넷. −z 가 실내 쪽이므로 전부 음수다.
            float zMount = -ReferenceRoomSpec.MountFrameThickness;                          // −0.038
            float zBack = zMount - ReferenceRoomSpec.CabinetBackThickness;                  // −0.062
            float zFace = -md;                                                              // −0.260

            // ══ ① 벽에 고정된 직사각형 하중 프레임 ═══════════════════════════
            //
            // 캐비닛과 벽 사이에 낀다. 이것이 있어야 캐비닛이 **벽에 걸려 있다**로
            // 읽히고, 지시가 제거를 요구한 「벽과 장치 사이에 남아 있는 큰 빈 구멍」이
            // 구조적으로 메워진다.
            var mount = new GameObject("MountFrame");
            mount.transform.SetParent(machine.transform, false);
            float mb = ReferenceRoomSpec.MountFrameBand;
            float mt = ReferenceRoomSpec.MountFrameThickness;
            // 🔴 캐비닛보다 **눈에 띄게** 크다. 첫 판본은 26mm 였고 캐비닛 뒤에
            // 완전히 가려, 독립 평가자가 「벽 앵커·볼트·거싯이 없다 · 액자 몰딩으로
            // 읽힌다」고 판정했다. 벽에 고정하는 부재가 안 보이면 고정돼 있지 않은
            // 것과 같다. 76mm 로 키워 캐비닛 둘레에 25mm 씩 테가 드러나게 한다.
            float mow = mw + 0.076f, moh = mh + 0.076f;
            Slab(mount.transform, "Mount_Top", new Vector3(0f, cy + moh * 0.5f - mb * 0.5f, -mt * 0.5f),
                 new Vector3(mow, mb, mt), "Steel");
            Slab(mount.transform, "Mount_Bottom", new Vector3(0f, cy - moh * 0.5f + mb * 0.5f, -mt * 0.5f),
                 new Vector3(mow, mb, mt), "Steel");
            Slab(mount.transform, "Mount_Left", new Vector3(-mow * 0.5f + mb * 0.5f, cy, -mt * 0.5f),
                 new Vector3(mb, moh - mb * 2f, mt), "Steel");
            Slab(mount.transform, "Mount_Right", new Vector3(mow * 0.5f - mb * 0.5f, cy, -mt * 0.5f),
                 new Vector3(mb, moh - mb * 2f, mt), "Steel");

            // 벽 고정 볼트 — **8개뿐이다.** 직전 판본은 20개를 캐비닛 앞면 둘레에
            // 둘렀고, 지시가 그것을 「작동 구조와 무관한 볼트 및 브래킷」으로 지목했다.
            // 앞면 볼트는 아무것도 붙잡지 않는다. 벽에 박히는 볼트만 남긴다.
            var boltB = new ProcMeshBuilder();
            boltB.AddPrism(Vector3.zero, 0.020f, 0.018f, 0.015f, 6, MeshAxis.Z, 0f, true, true, false, uv);
            Mesh boltMesh = SaveMesh(boltB.ToMesh("MountBolt"), "MountBolt");
            var mountBolts = new GameObject("MountBolts");
            mountBolts.transform.SetParent(mount.transform, false);
            for (int i = 0; i < 8; i++)
            {
                bool top = i < 4;
                float f = (i % 4) / 3f;
                AddBolt(mountBolts.transform, boltMesh,
                        new Vector3(Mathf.Lerp(-mow * 0.5f + mb * 0.6f, mow * 0.5f - mb * 0.6f, f),
                                    top ? cy + moh * 0.5f - mb * 0.5f : cy - moh * 0.5f + mb * 0.5f,
                                    -mt - 0.006f),
                        $"MountBolt_{i}");
            }

            // 코너 거싯 넷 — 「하중이 어디로 가는가」를 형상으로 답한다.
            // 독립 평가자가 「코너 거싯 없음 · 받침 다리 없음 · 하중 경로를 증명하는
            // 것이 아무것도 없다」고 지적했다. 거싯은 장식이 아니라 사각 프레임이
            // 마름모로 무너지는 것을 막는 부재이고, 없으면 프레임은 액자가 맞다.
            var gussets = new GameObject("MountGussets");
            gussets.transform.SetParent(mount.transform, false);
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1f : 1f;
                float sy = (i & 2) == 0 ? -1f : 1f;
                var gsb = new ProcMeshBuilder();
                // 직각삼각형 판. 두 변이 프레임 두 부재에 닿는다.
                var a = new Vector3(0f, 0f, 0f);
                var b2 = new Vector3(sx * 0.16f, 0f, 0f);
                var c2 = new Vector3(0f, sy * 0.16f, 0f);
                float th = 0.026f;
                var zf = new Vector3(0f, 0f, -th);
                Vector3 nrm = sx * sy > 0f ? Vector3.back : Vector3.back;
                gsb.AddTriangle(a, b2, c2, nrm, uv);
                gsb.AddTriangle(a + zf, c2 + zf, b2 + zf, Vector3.forward, uv);
                gsb.AddQuad(b2, a, a + zf, b2 + zf, new Vector3(0f, -sy, 0f), uv);
                gsb.AddQuad(a, c2, c2 + zf, a + zf, new Vector3(-sx, 0f, 0f), uv);
                gsb.AddQuad(c2, b2, b2 + zf, c2 + zf, new Vector3(sx, sy, 0f).normalized, uv);
                Mesh gm = SaveMesh(gsb.ToMesh($"MountGusset_{i}"), $"MountGusset_{i}");
                var g = new GameObject($"Gusset_{i}");
                g.transform.SetParent(gussets.transform, false);
                g.transform.localPosition = new Vector3(
                    sx * (mow * 0.5f - mb), cy + sy * (moh * 0.5f - mb), -mt);
                Render(g, gm, "BareSteel");
            }

            // ══ ② 하나의 금속 캐비닛 ═════════════════════════════════════════
            var cab = new GameObject("Cabinet");
            cab.transform.SetParent(machine.transform, false);

            // 후면판 — 챔버 아홉의 **공통 뒷벽**이다.
            Slab(cab.transform, "CabinetBack", new Vector3(0f, cy, zMount - ReferenceRoomSpec.CabinetBackThickness * 0.5f),
                 new Vector3(mw, mh, ReferenceRoomSpec.CabinetBackThickness), "Steel");

            // ⚠ **연속 전면 셸을 두지 않는다 — 두면 챔버를 통째로 막는다.**
            //
            // 지시의 「링 뒤에는 하나의 연속된 어두운 금속 캐비닛 표면이 있어야
            // 한다」를 도어 평면에 판 한 장 깔아서 만족시키려 했다. 그런데 그 판이
            // 도어 바로 뒤에 있으니 **챔버 아홉의 앞을 전부 막았다.**
            //
            // 벽 노출을 막는 것은 이미 `CabinetBack`(전면적 후면판)이 한다 —
            // 이음매로 새어 보이는 것은 벽이 아니라 캐비닛 안쪽이다. 그게 맞는 그림이다.
            //
            // 「빈틈을 막는 판」을 하나 더 까는 것은 싸 보이지만, 그 판이 무엇을
            // 가리는지 세어 보지 않으면 이렇게 된다.

            // ── 프레임 3단 위계 ──────────────────────────────────────────────
            // 굵기가 위계다. 여기서 하중 경로가 형상으로 보인다.

            // 🔴 **위계는 굵기만이 아니라 깊이로도 나온다.**
            //
            // 첫 그레이박스에서 셋이 전부 도어와 **같은 평면**에 있었고, 그래서
            // 외곽 프레임이 「하중을 받는 테」가 아니라 그냥 더 넓은 벽으로 보였다.
            // 지시의 합격 기준 하나가 「외곽 프레임이 장식을 위한 테두리가 아니라
            // 본체 하중을 지지함」이라, 평면에 그린 테로는 통과할 수 없다.
            //
            // 앞으로 나온 순서가 하중 순서와 같아야 한다 — 프레임이 도어를 **물고**
            // 있는 것으로 읽힌다. (−z 가 실내 쪽이므로 뺄수록 앞이다.)
            // 🔴 **굵기 위계와 깊이 위계를 일부러 어긋나게 둔다.**
            //
            // 굵기는 지시대로 외곽 > 뱅크 리브 > 격벽 (86 > 58 > 30mm) 이다.
            // 깊이는 **뒤집는다** — 외곽 > 격벽 > 리브.
            //
            // 왜: 리브를 가장 앞에 두고 캐비닛 전 높이로 이었더니 판이 **끊기지 않는
            // 세로 채널 둘**로 3분할됐다. 독립 평가가 「무중단 713px · 세로 띠 셋 ·
            // 재질을 입힐수록 나빠진다」로 잡았고, 그건 이 저장소가 `G-SLOT` 축으로
            // 12라운드 싸운 슬롯머신 릴 띠의 재발이다. 지시의 계층 요구
            // 「가장 먼저 **하나의** 직사각형 통관 기계가 보임」과도 정면으로 충돌한다.
            //
            // 압력 챔버 랙의 실제 구성이 답을 준다 — **수평 선반판이 연속이고
            // 수직 스티프너가 그 사이에 끼워진다.** 굵기 위계는 지키면서
            // 연속성만 가로로 넘긴다.
            float outerProud = ReferenceRoomSpec.OuterFrameProud;
            float ribProud = ReferenceRoomSpec.BankRibProud;
            float bulkProud = ReferenceRoomSpec.BulkheadProud;

            // 외곽 하중 프레임 — 가장 두껍고 가장 앞. 캐비닛 전체 자중 + 벽 고정.
            float ofd = md + outerProud;
            float ofz = (zFace - outerProud) * 0.5f;
            // ⚠ **상단 프레임은 캐비닛 꼭대기가 아니라 챔버 적층 바로 위다.**
            // 그 위에 샤프트 하우징이 얹힌다(높이 유도식이 그렇게 쌓았다).
            // 첫 판본은 `MachineTopY − outer/2` 로 잡아 **하우징과 같은 자리를
            // 차지했고**, 화면에서 둘이 한 덩어리 뚜껑으로 보였다.
            Slab(cab.transform, "Frame_Top", new Vector3(0f, ReferenceRoomSpec.ChamberStackTopY + outer * 0.5f, ofz),
                 new Vector3(mw, outer, ofd), "BareSteel");
            Slab(cab.transform, "Frame_Bottom", new Vector3(0f, ReferenceRoomSpec.MachineBottomY + outer * 0.5f, ofz),
                 new Vector3(mw, outer, ofd), "BareSteel");
            Slab(cab.transform, "Frame_Left", new Vector3(-mw * 0.5f + outer * 0.5f, cy, ofz),
                 new Vector3(outer, mh, ofd), "BareSteel");
            Slab(cab.transform, "Frame_Right", new Vector3(mw * 0.5f - outer * 0.5f, cy, ofz),
                 new Vector3(outer, mh, ofd), "BareSteel");

            // 세로 뱅크 프레임 — 중간 굵기, 중간 깊이. 챔버 3개 적층 하중을 받고,
            // 챔버 간 격벽이며, **공통 잠금 링크(캠 로드)를 보호하고**, 정비 시
            // 한 열씩 분리하는 경계다.
            // 🔴 **리브를 행마다 끊는다.** 전 높이로 이으면 세로 채널이 되고,
            // 그 채널이 판을 3분할해 슬롯머신 릴 띠가 된다(위 주석).
            //
            // 세 토막으로 나누면 「뱅크가 나뉜다」는 그대로 읽히면서
            // **무중단 세로 길이가 도어 한 장 높이(0.48)로 떨어진다** —
            // 캐비닛 높이의 27%다. 불변식이 이 비를 40% 이하로 고정한다.
            for (int k = 0; k < 2; k++)
            {
                float x = ReferenceRoomSpec.BankCenterX(k) - ReferenceRoomSpec.MachineCenterX
                        + ReferenceRoomSpec.WindowPitchX * 0.5f;
                for (int row = 0; row < 3; row++)
                {
                    float y = ReferenceRoomSpec.ChamberStackBottomY
                            + ReferenceRoomSpec.ChamberDoorHeight * (row + 0.5f)
                            + bulk * row;
                    Slab(cab.transform, $"BankRib_{k}_{row}",
                         new Vector3(x, y, zFace - ribProud * 0.5f),
                         new Vector3(rib, ReferenceRoomSpec.ChamberDoorHeight, 0.050f + ribProud), "BareSteel");
                }
            }

            // 챔버 사이 가로 격벽 — 가장 얇지만 **연속이고 가장 앞이다.**
            // 리브를 가로질러 지나가므로 교차점에서 격벽이 이긴다.
            for (int k = 0; k < 2; k++)
            {
                float y = ReferenceRoomSpec.ChamberStackBottomY
                        + ReferenceRoomSpec.ChamberDoorHeight * (k + 1)
                        + bulk * (k + 0.5f);
                Slab(cab.transform, $"Bulkhead_{k}",
                     new Vector3(0f, y, zFace - bulkProud * 0.5f),
                     new Vector3(mw - outer * 2f, bulk, 0.040f + bulkProud), "Steel");
            }

            // ══ ④ 공통 잠금축 — 상단 샤프트 하우징 ═══════════════════════════
            //
            // 「세 개의 세로 뱅크를 동시에 제어하는 공통 잠금축」. 지시가 「캐비닛
            // 상단 또는 측면」을 허용했고 상단을 골랐다 — 측면에 두면 레버 쪽
            // 한 변에만 몰려 세 뱅크가 대칭으로 물린다는 것이 안 읽힌다.
            var housing = new GameObject("ShaftHousing");
            housing.transform.SetParent(cab.transform, false);
            float shH = ReferenceRoomSpec.ShaftHousingHeight;
            float shY = ReferenceRoomSpec.CommonShaftY;

            // ⚠ **하우징이 얼마나 튀어나오는지를 유도한다.** 첫 판본은 중심과 깊이를
            // 따로 적어 앞면이 도어 면보다 **130mm** 앞으로 나갔고, 그 결과
            // 상태 탭 셋이 하우징 **안에** 파묻혀 화면에서 사라졌다.
            // 「밖에서 보이는 유일한 잠금 상태」가 안 보이면 그건 없는 것과 같다.
            float zHousingFront = ReferenceRoomSpec.ShaftHousingFrontZ;   // 외곽 프레임보다 10mm 앞
            float housingDepth = -zHousingFront;                          // 벽면(0)부터 앞면까지
            Slab(housing.transform, "HousingBody", new Vector3(0f, shY, zHousingFront * 0.5f),
                 new Vector3(mw - outer * 2f, shH, housingDepth), "Steel");

            // 공통 수평축 — 하우징 앞면에서 절반쯤 드러난다. 완전히 감추면
            // 「무엇이 도는가」가 화면에 없고, 완전히 노출하면 지시가 금지한
            // 「밖으로 노출된 복잡한 링크」가 된다. 반쯤이 답이다.
            // 🔴 **축이 모서리 기어박스까지 닿아야 한다.**
            //
            // 첫 판본은 길이를 `mw − outer*2` 로 잡아 오른쪽 끝이 x=0.478 에서 끝났고,
            // 기어박스는 x=0.684 에서 시작했다 — **206mm 벌어져 있었다.**
            // 독립 평가가 「기어박스와 공통축이 만나는 것이 보이지 않는다 · 거기서
            // 사슬이 끊긴다」로 잡았고, 좌표로 확인하니 그대로였다.
            //
            // 원인은 치수가 두 함수에 나뉘어 있던 것이다. 이제 양 끝을
            // `ReferenceRoomSpec` 이 유도하고 **두 함수가 같은 값을 읽는다.**
            float csr = ReferenceRoomSpec.CommonShaftRadius;
            float shaftLen = ReferenceRoomSpec.CommonShaftLength;
            var shaftB = new ProcMeshBuilder();
            shaftB.AddPrism(Vector3.zero, csr, csr, shaftLen, 10, MeshAxis.X, 0f, true, true, false, uv);
            // 축받이 넷 — 축이 어디에 얹혀 도는지 말한다.
            for (int i = 0; i < 4; i++)
            {
                float t = (i / 3f - 0.5f) * (shaftLen - 0.18f);
                shaftB.AddBox(new Vector3(t, 0f, 0.010f), new Vector3(0.034f, csr * 2.6f, csr * 2.2f), 0f, uv);
            }
            Mesh shaftMesh = SaveMesh(shaftB.ToMesh("CommonLockShaft"), "CommonLockShaft");
            var shaft = new GameObject(CommonShaftName);
            shaft.transform.SetParent(housing.transform, false);
            // 하우징 앞면에 **반쯤 걸친다.** 완전히 감추면 무엇이 도는지 화면에 없고,
            // 완전히 노출하면 지시가 금지한 「밖으로 노출된 복잡한 링크」가 된다.
            // 탭보다 아래에 둬서 둘이 겹치지 않게 한다 — 겹치면 탭이 도는 축에
            // 파묻혀 이동이 안 읽힌다.
            //
            // x 는 **양 끝의 중점**이다. 손으로 0 을 적으면 캐비닛 중심에 놓여
            // 오른쪽 끝이 기어박스에 못 닿는다 — 그것이 직전 결함이었다.
            float shaftMidX = (ReferenceRoomSpec.CommonShaftLeftX + ReferenceRoomSpec.CommonShaftRightX) * 0.5f
                            - ReferenceRoomSpec.MachineCenterX;
            shaft.transform.localPosition = new Vector3(shaftMidX, shY - 0.026f, ReferenceRoomSpec.CommonShaftZ);
            Render(shaft, shaftMesh, "Collar");

            // 축이 우측 외곽 프레임을 통과하는 지점의 **베어링 보스.**
            // 축이 프레임을 그냥 뚫고 지나가면 「어떻게 지지되는가」가 화면에 없다.
            var bossB = new ProcMeshBuilder();
            bossB.AddPrism(Vector3.zero, csr * 2.1f, csr * 1.9f, 0.052f, 8, MeshAxis.X, 0f, true, true, false, uv);
            Mesh bossMesh = SaveMesh(bossB.ToMesh("ShaftBearingBoss"), "ShaftBearingBoss");
            var boss = new GameObject("ShaftBearingBoss");
            boss.transform.SetParent(housing.transform, false);
            boss.transform.localPosition = new Vector3(
                mw * 0.5f - outer * 0.5f, shY - 0.026f, ReferenceRoomSpec.CommonShaftZ);
            Render(boss, bossMesh, "BareSteel");

            // 상태 탭 셋 — **뱅크 위에 정렬한다.** 밖에서 보이는 유일한 잠금 상태다.
            var tabB = new ProcMeshBuilder();
            tabB.AddBox(Vector3.zero, new Vector3(ReferenceRoomSpec.StatusTabWidth,
                                                  ReferenceRoomSpec.StatusTabHeight, 0.030f), 0f, uv);
            tabB.AddBox(new Vector3(0f, ReferenceRoomSpec.StatusTabHeight * 0.5f, -0.004f),
                        new Vector3(ReferenceRoomSpec.StatusTabWidth * 0.42f, 0.018f, 0.022f), 0f, uv);
            Mesh tabMesh = SaveMesh(tabB.ToMesh("LockStatusTab"), "LockStatusTab");
            for (int bank = 0; bank < ReferenceRoomSpec.BankCount; bank++)
            {
                var tab = new GameObject($"{StatusTabName}_{bank}");
                tab.transform.SetParent(housing.transform, false);
                tab.transform.localPosition = new Vector3(
                    ReferenceRoomSpec.BankCenterX(bank) - ReferenceRoomSpec.MachineCenterX,
                    shY + ReferenceRoomSpec.StatusTabTravel * 0.62f,
                    zHousingFront - 0.016f);   // 하우징 앞면 위에 올라탄다
                Render(tab, tabMesh, "RedPaint");
            }

            // ══ ③ 원형 압력창 9개 ════════════════════════════════════════════
            BuildWindowMeshes(out Mesh doorMesh, out Mesh ringMesh, out Mesh chamberMesh,
                              out Mesh glassMesh, out Mesh soulMesh,
                              out Mesh windowBoltMesh, out Mesh hingeMesh, out Mesh clampMesh);

            var grid = new GameObject("WindowGrid");
            grid.transform.SetParent(machine.transform, false);

            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    Vector2 c = ReferenceRoomSpec.WindowCenter(col, row);
                    int seed = col * 3 + row;
                    var module = new GameObject($"{WindowModuleName}_{col}{row}");
                    module.transform.SetParent(grid.transform, false);
                    module.transform.localPosition = new Vector3(c.x - ReferenceRoomSpec.MachineCenterX, c.y, zFace);

                    // ⚠ **모듈을 회전시키지 않는다.** 규격품 9개가 정확히 정렬해야
                    // 「하나의 시스템」으로 읽힌다. 개체차는 **내용물(영혼)에만** 준다.

                    // ① 사각 챔버 도어
                    var door = new GameObject("Door");
                    door.transform.SetParent(module.transform, false);
                    Renderer doorRenderer = Render(door, doorMesh, "Steel");

                    // 🔴 **도어는 그림자를 드리우지 않는다.**
                    //
                    // 통제 실험이 이것을 강제했다. 도어에 0.264m 정사각 구멍이
                    // 뚫려 있고(정점 실측·메시 콜라이더 레이캐스트로 확인), 영혼은
                    // 그 한가운데 0.136m 크기로 있는데도 **도어를 켜면 붉은 화소가
                    // 0.53% → 0.00% 로 사라졌다.** 기하로는 완전히 열려 있다.
                    //
                    // 남는 설명은 그림자 맵이다 — 주광이 점광이라 큐브맵 6면을
                    // 나눠 쓰고, 4.4m 범위에서 0.26m 구멍은 몇 텍셀에 불과해
                    // **구멍이 그림자 맵에서 메워진다.** 그러면 챔버 안이 통째로
                    // 그림자에 들어가 영혼이 검게 죽는다.
                    //
                    // 그리고 도어의 그림자는 얻는 것이 없다 — 벽에 붙은 평판이라
                    // 자기 뒤 말고는 드리울 곳이 없다. 이 저장소는 같은 이유로
                    // 볼트 그림자를 껐고 프레임타임이 89ms → 8.4ms 가 됐다.
                    doorRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                    // 힌지 둘 — 왼쪽 가장자리. 「실제로 열릴 것처럼」.
                    for (int h = 0; h < ReferenceRoomSpec.DoorHingeCount; h++)
                    {
                        var hinge = new GameObject($"Hinge_{h}");
                        hinge.transform.SetParent(module.transform, false);
                        hinge.transform.localPosition = new Vector3(
                            -ReferenceRoomSpec.ChamberDoorWidth * 0.5f + ReferenceRoomSpec.DoorHingeWidth * 0.5f + 0.006f,
                            (h == 0 ? 1f : -1f) * ReferenceRoomSpec.ChamberDoorHeight * 0.30f,
                            -0.014f);
                        Render(hinge, hingeMesh, "BareSteel");
                    }

                    // 잠금 클램프 하나 — 오른쪽 가장자리. **캠 로드가 이것을 돌린다.**
                    // 좌우 두 개였던 직전 판본과 다르다: 두 개면 어느 쪽이 구동인지
                    // 안 읽히고, 지시도 「반대쪽에 잠금 클램프 **하나**」다.
                    var clamp = new GameObject(LockClampName);
                    clamp.transform.SetParent(module.transform, false);
                    clamp.transform.localPosition = new Vector3(
                        ReferenceRoomSpec.ChamberDoorWidth * 0.5f - ReferenceRoomSpec.DoorClampWidth * 0.5f - 0.006f,
                        0f, -0.016f);
                    Render(clamp, clampMesh, "BareSteel");

                    // ④ 챔버 내부 — 도어 뒤의 실제 공간
                    var cham = new GameObject("Chamber");
                    cham.transform.SetParent(module.transform, false);
                    Render(cham, chamberMesh, "ChamberDark");

                    // 영혼 — 유리 뒤 115mm. 이 거리가 패럴랙스의 전부다.
                    var soul = new GameObject(SoulName);
                    soul.transform.SetParent(module.transform, false);
                    float jitter = ProcMeshBuilder.HashSigned(seed * 977 + 13);
                    float scale = 1f + jitter * 0.22f;
                    soul.transform.localPosition = new Vector3(
                        ProcMeshBuilder.HashSigned(seed * 131 + 7) * 0.026f,
                        ProcMeshBuilder.HashSigned(seed * 197 + 3) * 0.026f,
                        ReferenceRoomSpec.SoulDepthFromDoorFace);
                    soul.transform.localScale = new Vector3(scale, scale * (1f + jitter * 0.06f), scale);
                    soul.transform.localRotation = Quaternion.Euler(
                        ProcMeshBuilder.HashSigned(seed * 419) * 26f,
                        ProcMeshBuilder.HashSigned(seed * 523) * 26f,
                        ProcMeshBuilder.Hash01(seed * 313) * 360f);
                    // 🔴 **한 겹이면 반드시 「분홍 스티커」가 된다.** 발광은 메시 전체에
                    // 균일하게 실리고, 균일한 밝기 + 둥근 실루엣 = UI 아이콘이다.
                    // 밝기 차이를 **기하로** 만든다 — 어둡고 반투명한 껍질 안의 뜨거운 핵.
                    Renderer soulRenderer = Render(soul, soulMesh, "RedPaint");
                    soulRenderer.sharedMaterial = SoulMaterial(seed, core: false);
                    soulRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                    var soulCore = new GameObject("Core");
                    soulCore.transform.SetParent(soul.transform, false);
                    soulCore.transform.localScale = Vector3.one * 0.52f;
                    soulCore.transform.localRotation = Quaternion.Euler(
                        0f, 0f, ProcMeshBuilder.Hash01(seed * 641) * 360f);
                    Renderer coreRenderer = Render(soulCore, soulMesh, "RedPaint");
                    coreRenderer.sharedMaterial = SoulMaterial(seed, core: true);
                    coreRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                    // ③ 두꺼운 유리 — 링 보어 안쪽
                    var glass = new GameObject(GlassName);
                    glass.transform.SetParent(module.transform, false);
                    Render(glass, glassMesh, "Glass");

                    // ② 낮고 두꺼운 원형 클램프 링
                    var ring = new GameObject("ClampRing");
                    ring.transform.SetParent(module.transform, false);
                    Render(ring, ringMesh, "Collar");

                    // 체결 볼트 — **모든 창에서 같은 개수·같은 각도.**
                    var mBolts = new GameObject("Bolts");
                    mBolts.transform.SetParent(module.transform, false);
                    for (int i = 0; i < ReferenceRoomSpec.WindowBoltCount; i++)
                    {
                        float a = i * Mathf.PI * 2f / ReferenceRoomSpec.WindowBoltCount
                                + Mathf.PI / ReferenceRoomSpec.WindowBoltCount;
                        float r = ReferenceRoomSpec.WindowBoltRadius;
                        AddBolt(mBolts.transform, windowBoltMesh,
                                new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r,
                                            -ReferenceRoomSpec.WindowProtrusion - 0.006f),
                                $"Bolt_{i}");
                    }
                }
            }

            // ⚠ **정비 패널 3개와 케이블 소켓 9개를 만들지 않는다.** 지시가 각각
            // 「각 열 아래에 매달린 기능 불명의 작은 원판」·「실제 연결 대상을 알 수
            // 없는 선과 케이블」로 지목해 제거를 요구했다. 지웠다는 사실을 여기 적어
            // 두지 않으면 다음 배치가 「빠뜨렸다」로 읽고 되돌린다.

            _report.AppendLine($"  {MachineName} — 캐비닛 {mw:F3} × {mh:F3} × {md:F2} " +
                               $"(벽 폭 {ReferenceRoomSpec.MachineWallCoverage * 100f:F1}% · 벽 높이 {mh / ReferenceRoomSpec.InteriorHeight * 100f:F1}%)");
            _report.AppendLine($"    프레임 위계 — 외곽 {outer * 1000f:F0} > 뱅크 {rib * 1000f:F0} > 격벽 {bulk * 1000f:F0} mm");
            _report.AppendLine($"    챔버 9 — 도어 {ReferenceRoomSpec.ChamberDoorWidth} × {ReferenceRoomSpec.ChamberDoorHeight} " +
                               $"· 링 ⌀{ReferenceRoomSpec.WindowRingDiameter} (돌출 {ReferenceRoomSpec.WindowProtrusion * 1000f:F0}mm) " +
                               $"· 유리 ⌀{ReferenceRoomSpec.WindowGlassDiameter} (인셋 {ReferenceRoomSpec.WindowGlassInset * 1000f:F0}mm)");
            _report.AppendLine($"    깊이 4단 — 도어 0 / 링앞 −{ReferenceRoomSpec.WindowProtrusion * 1000f:F0} / " +
                               $"유리 −{ReferenceRoomSpec.WindowGlassFrontOffset * 1000f:F0} / 영혼 +{ReferenceRoomSpec.SoulDepthFromDoorFace * 1000f:F0} / " +
                               $"챔버후면 +{ReferenceRoomSpec.ChamberBackFromDoorFace * 1000f:F0} mm");
            _report.AppendLine($"    공통 잠금축 y={shY:F3} · 상태 탭 {ReferenceRoomSpec.BankCount} · 캠 로드 {ReferenceRoomSpec.BankCount}");
        }

        /// <summary>
        /// 압력 관찰창의 메시 여덟 장. **한 번 만들어 9번 인스턴스한다.**
        ///
        /// ## 층이 다섯이 아니라 **넷**이다 — 이것이 이번 개정의 요점이다
        ///
        /// 직전 판본은 후면 장착판·지지 브래킷을 넣어 다섯 층이었다. 그 둘은
        /// 「원형 모듈을 벽에 붙인다」는 전제에서만 필요한 부품이고, 캐비닛이
        /// 연속면이 된 지금은 **받을 하중이 없다.** 하중을 안 받는 브래킷은
        /// 정의상 장식이고, 지시가 그것을 「기능적 관계 없이 추가된」 것으로 지목했다.
        ///
        /// 지금은 넷뿐이다 (−z 가 실내 쪽) —
        /// <code>
        ///   z +0.198   ④ 챔버 후면 — 영혼 뒤의 어두운 배경
        ///   z +0.100      영혼 (유리에서 115mm 뒤)
        ///   z +0.028      챔버 입구 (도어 판 안쪽면)
        ///   z  0.000   ① 사각 챔버 도어 바깥면 = 캐비닛 전면
        ///   z −0.015   ③ 유리 — 링 앞면에서 45mm 안쪽
        ///   z −0.060   ② 클램프 링 앞면 — 도어에서 60mm 돌출
        /// </code>
        ///
        /// 지시 「측면에서 최소 네 단계의 깊이가 구분됨」이 이 표다.
        /// </summary>
        private static void BuildWindowMeshes(out Mesh door, out Mesh ring, out Mesh chamber,
                                              out Mesh glass, out Mesh soul,
                                              out Mesh bolt, out Mesh hinge, out Mesh clamp)
        {
            int sides = ReferenceRoomSpec.WindowSilhouetteSides;
            float rGlass = ReferenceRoomSpec.WindowGlassDiameter * 0.5f;
            float rRing = ReferenceRoomSpec.WindowRingDiameter * 0.5f;
            float prot = ReferenceRoomSpec.WindowProtrusion;
            float face = ReferenceRoomSpec.CabinetFaceThickness;
            float uv = ReferenceRoomSpec.HeroUvPerMeter;

            // ── ① 사각 챔버 도어 ───────────────────────────────────────────
            // 원형 창이 붙는 **판**이다. 판 자체가 챔버를 밀폐하므로 두께가 있다.
            // 가장자리를 살짝 접어(0.008 단) 판이 프레임 안에 앉은 것으로 읽히게 한다.
            //
            // 🔴 **판이 아니라 「문」으로 읽혀야 한다.** 첫 판본은 이음매 5mm 짜리
            // 민판이었고 독립 평가자가 「도어가 없다 — 리브 격자만 있고 그 안은
            // 통짜 면이다」로 읽었다. 세 가지를 더한다 —
            //   ① 이음매를 11mm 로 넓혀 판의 경계가 그림자로 보이게
            //   ② 둘레에 개스킷 압착 프레임 (문을 눌러 밀폐하는 실제 부품)
            //   ③ 링이 앉는 얕은 단
            float dw = ReferenceRoomSpec.ChamberDoorWidth - ReferenceRoomSpec.DoorSeamWidth * 2f;
            float dh = ReferenceRoomSpec.ChamberDoorHeight - ReferenceRoomSpec.DoorSeamWidth * 2f;
            var db = new ProcMeshBuilder();

            // 🔴 **판에 구멍이 있어야 한다. 통짜 상자면 챔버가 통째로 가려진다.**
            //
            // 첫 판본은 `AddBox` 하나였다. 그 판이 도어 전체(0.52 × 0.48)를 막아
            // **영혼도 챔버도 단 한 화소도 렌더되지 않았다.** 통제 실험이 그것을
            // 증명했다 — 유리를 꺼도, 발광을 ×8 해도 화소 통계가 **비트 단위로
            // 동일**했다(평균 9.7 / 붉은 0.00% / 암부 65.6%, 세 번 모두).
            //
            // 독립 평가자가 「9개가 전부 평판에 뚫린 얕은 열린 구멍이다 · 챔버가
            // 없다」로 판정한 것이 이것이다. 그가 본 「얕은 바닥」은 챔버 후면이
            // 아니라 **막힌 도어 판**이었다. 판정이 맞았고 원인만 달랐다.
            //
            // 🔴 **네 조각으로 만든다. 부채꼴로 이으면 뚫렸는지 증명할 수 없다.**
            //
            // 첫 판본은 사각 판과 12각 구멍을 부채꼴 `AddQuad` 로 이었다. 그 메시는
            // **모든 검사를 통과했다** — 중심을 덮는 삼각형 0개, 메시 콜라이더
            // 레이캐스트 r≤0.14 전부 통과, 삼각형 수도 정확히 예상대로 176.
            // **그런데 화면에서는 막혀 있었다.** 그 도어 하나만 렌더러를 끄면
            // 붉은 화소가 0.00% → 0.58% 로 살아났다.
            //
            // 원인을 끝내 형상 수준에서 특정하지 못했다(삼각화 순서나 노멀 처리로
            // 의심되지만 증거가 없다). **증명할 수 없는 구조는 쓰지 않는다** —
            // 사각 조각 넷은 「구멍이 있다」가 좌표만 봐도 자명하고, 같은 종류의
            // 실패가 원리적으로 불가능하다.
            //
            // 개구부는 정사각이고 그 대각(0.187)이 링 안착 단(0.204)보다 작으므로
            // **네 모서리가 밖에서 보이지 않는다.** 보이는 것은 원형 보어뿐이다.
            float hwD = dw * 0.5f, hhD = dh * 0.5f;
            float ap = ReferenceRoomSpec.DoorApertureHalf;
            float bandY = hhD - ap, bandX = hwD - ap;
            db.AddBox(new Vector3(0f, (ap + hhD) * 0.5f, face * 0.5f), new Vector3(dw, bandY, face), 0f, uv);
            db.AddBox(new Vector3(0f, -(ap + hhD) * 0.5f, face * 0.5f), new Vector3(dw, bandY, face), 0f, uv);
            db.AddBox(new Vector3(-(ap + hwD) * 0.5f, 0f, face * 0.5f), new Vector3(bandX, ap * 2f, face), 0f, uv);
            db.AddBox(new Vector3((ap + hwD) * 0.5f, 0f, face * 0.5f), new Vector3(bandX, ap * 2f, face), 0f, uv);

            // ② 개스킷 압착 프레임 — 네 변. 문의 윤곽을 그림자로 그린다.
            float gb2 = ReferenceRoomSpec.DoorGasketBand;
            float gp = ReferenceRoomSpec.DoorGasketProud;
            db.AddBox(new Vector3(0f, dh * 0.5f - gb2 * 0.5f, -gp * 0.5f), new Vector3(dw, gb2, gp), 0f, uv);
            db.AddBox(new Vector3(0f, -dh * 0.5f + gb2 * 0.5f, -gp * 0.5f), new Vector3(dw, gb2, gp), 0f, uv);
            db.AddBox(new Vector3(-dw * 0.5f + gb2 * 0.5f, 0f, -gp * 0.5f), new Vector3(gb2, dh - gb2 * 2f, gp), 0f, uv);
            db.AddBox(new Vector3(dw * 0.5f - gb2 * 0.5f, 0f, -gp * 0.5f), new Vector3(gb2, dh - gb2 * 2f, gp), 0f, uv);

            // ③ 링이 앉는 얕은 단 — 링이 판 위에 그냥 떠 있지 않다.
            //
            // 🔴 **고리여야 한다. 원기둥이면 보어를 막는다.**
            //
            // 첫 판본은 `AddPrism(..., capA:true, capB:true, ...)` 였다. 뚜껑 달린
            // 원기둥은 반지름 0.204 짜리 **꽉 찬 원반**이고, 그것이 도어에 뚫어 놓은
            // 구멍을 그대로 덮었다. 그래서 도어 정점을 세어 「중심 0.10m 이내 0개」로
            // 확인하고도 화면은 여전히 막혀 있었다 — 캡은 테두리 정점만으로
            // 부채꼴을 만들기 때문에 **중심에 정점이 없으면서 중심을 덮는다.**
            //
            // 통제 실험이 결론을 냈다: 도어만 끄면 붉은 화소 0.00% → **2.11%**.
            // 이 한 부품이 지시의 「영혼 내부 표현」 전체를 무효화하고 있었고,
            // 독립 평가자가 본 「얕은 열린 구멍」의 바닥이 바로 이 원반이다.
            float rSeat = ReferenceRoomSpec.WindowSeatRadius;
            float rBore = rGlass;
            const float seatDepth = 0.012f;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 si0 = new Vector3(Mathf.Cos(a0) * rBore, Mathf.Sin(a0) * rBore, -seatDepth);
                Vector3 si1 = new Vector3(Mathf.Cos(a1) * rBore, Mathf.Sin(a1) * rBore, -seatDepth);
                Vector3 so0 = new Vector3(Mathf.Cos(a0) * rSeat, Mathf.Sin(a0) * rSeat, -seatDepth);
                Vector3 so1 = new Vector3(Mathf.Cos(a1) * rSeat, Mathf.Sin(a1) * rSeat, -seatDepth);
                Vector3 outward = ((so0 + so1) * 0.5f).normalized;
                var down = new Vector3(0f, 0f, seatDepth);

                db.AddQuad(si0, so0, so1, si1, Vector3.back, uv);                         // 단 앞면 (고리)
                db.AddQuad(so0 + down, so0, so1, so1 + down, outward, uv);                // 단 바깥 옆면
            }
            door = SaveMesh(db.ToMesh("ChamberDoor"), "ChamberDoor");

            // ── ② 낮고 두꺼운 원형 클램프 링 ───────────────────────────────
            //
            // 🔴 지시 「외부 링은 12~16각형의 저폴리 원형으로 만든다. 현재처럼
            // 장식적인 톱니나 꽃잎 모양의 팔각 실루엣을 사용하지 마라.」
            //
            // **끊긴 상자가 아니라 이어진 고리다.** 인접 세그먼트가 같은 점을 공유하도록
            // 각 변마다 안팎 네 점을 직접 잇는다. `AddBox` 를 각도만 돌려 늘어놓으면
            // 모서리마다 틈이 벌어지고, 그 틈이 「톱니·꽃잎」으로 보인다 —
            // 직전 판본이 정확히 그랬다.
            //
            // 그리고 **한 겹이다.** 「과도하게 두꺼운 이중 팔각 링」이 제거 대상이므로
            // 바깥 플랜지 + 안쪽 베젤 2단 구성을 하나로 합쳤다.
            var cb = new ProcMeshBuilder();
            float zFront = -prot;
            float zBack = 0f;                 // 도어 면에 그대로 앉는다 — 뜨지 않는다
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides;
                float a1 = (i + 1) * Mathf.PI * 2f / sides;
                var i0 = new Vector3(Mathf.Cos(a0) * rGlass, Mathf.Sin(a0) * rGlass, 0f);
                var i1 = new Vector3(Mathf.Cos(a1) * rGlass, Mathf.Sin(a1) * rGlass, 0f);
                var o0 = new Vector3(Mathf.Cos(a0) * rRing, Mathf.Sin(a0) * rRing, 0f);
                var o1 = new Vector3(Mathf.Cos(a1) * rRing, Mathf.Sin(a1) * rRing, 0f);
                var fz = new Vector3(0f, 0f, zFront);
                var bz = new Vector3(0f, 0f, zBack);
                Vector3 outward = ((o0 + o1) * 0.5f).normalized;

                cb.AddQuad(i0 + fz, o0 + fz, o1 + fz, i1 + fz, Vector3.back, uv);      // 앞면
                cb.AddQuad(o0 + bz, o0 + fz, o1 + fz, o1 + bz, outward, uv);           // 바깥 옆면
                cb.AddQuad(i0 + fz, i0 + bz, i1 + bz, i1 + fz, -outward, uv);          // 안쪽 보어
                cb.AddQuad(o0 + bz, i0 + bz, i1 + bz, o1 + bz, Vector3.forward, uv);   // 뒷면
            }
            ring = SaveMesh(cb.ToMesh("PressureWindowRing"), "PressureWindowRing");

            // ── ④ 챔버 내부 ────────────────────────────────────────────────
            //
            // 🔴 **사각 상자다.** 직전 판본은 유리 지름의 원뿔대였고, 그래서 유리
            // 바로 뒤가 곧 벽이었다 — 지시가 「원형창과 영혼 사이의 의미 없는 검은
            // 빈 공간」이라고 부른 것이 그 원뿔이다. 밀폐 챔버는 도어 뒤의 **방**이지
            // 창에 뚫린 구멍이 아니다.
            //
            // 안쪽을 향한 다섯 면(벽 넷 + 후면). 앞면은 도어가 막으므로 없다.
            var mb = new ProcMeshBuilder();
            float hw = ReferenceRoomSpec.ChamberInnerWidth * 0.5f;
            float hh = ReferenceRoomSpec.ChamberInnerHeight * 0.5f;
            float z0 = face;                                        // 입구 (도어 안쪽면)
            float z1 = ReferenceRoomSpec.ChamberBackFromDoorFace;   // 후면
            var b000 = new Vector3(-hw, -hh, z0); var b100 = new Vector3(hw, -hh, z0);
            var b110 = new Vector3(hw, hh, z0);   var b010 = new Vector3(-hw, hh, z0);
            var b001 = new Vector3(-hw, -hh, z1); var b101 = new Vector3(hw, -hh, z1);
            var b111 = new Vector3(hw, hh, z1);   var b011 = new Vector3(-hw, hh, z1);
            mb.AddQuad(b001, b101, b111, b011, Vector3.back, uv);      // 후면 (실내를 향한다)
            mb.AddQuad(b000, b010, b011, b001, Vector3.right, uv);     // 좌벽 → 안쪽
            mb.AddQuad(b100, b101, b111, b110, Vector3.left, uv);      // 우벽 → 안쪽
            mb.AddQuad(b000, b001, b101, b100, Vector3.up, uv);        // 바닥 → 위
            mb.AddQuad(b010, b110, b111, b011, Vector3.down, uv);      // 천장 → 아래
            chamber = SaveMesh(mb.ToMesh("SoulChamberBox"), "SoulChamberBox");

            // ── ③ 두꺼운 유리 ──────────────────────────────────────────────
            // 링 앞면에서 45mm 안쪽. 두께 18mm 라 가장자리가 어둡게 읽힌다.
            var gb = new ProcMeshBuilder();
            gb.AddPrism(new Vector3(0f, 0f, -ReferenceRoomSpec.WindowGlassFrontOffset),
                        rGlass * 0.98f, rGlass * 0.98f, ReferenceRoomSpec.WindowGlassThickness,
                        sides, MeshAxis.Z, 0f, true, true, false, uv);
            glass = SaveMesh(gb.ToMesh("SoulWindowGlass"), "SoulWindowGlass");

            // ── 영혼 물질 ──────────────────────────────────────────────────
            // **완벽한 원이 아니라 찌그러지고 응축된 저폴리 덩어리.**
            // 직전 판본은 8각 원뿔대 둘이라 정면에서 정팔각형으로 보였고, 그것이
            // 「평면 분홍 아이콘」의 정체였다. 이제 표본마다 반지름을 결정론적으로
            // 흔들어 어느 각도에서도 대칭이 아니게 만든다.
            var sb2 = new ProcMeshBuilder();
            // ⚠ **유리도 챔버 후면도 뚫지 않는 크기여야 한다.**
            // `ReferenceRoomSpec.SoulMaxHalfDepth` 가 인스턴스 크기 흔들림까지 곱해
            // 그 두 여유(standoff 0.105 · 후면 0.108)와 비교되고, 불변식이 그것을 단정한다.
            // 첫 값(지름의 0.30)이 실제로 후면을 뚫었고 검사가 즉시 잡았다.
            float rs = ReferenceRoomSpec.SoulRadius;
            const int lat = 4, lon = 7;
            Vector3 V(float ph, float th, int seed)
            {
                float k = 0.74f + ProcMeshBuilder.Hash01(seed) * 0.52f;
                return new Vector3(Mathf.Sin(ph) * Mathf.Cos(th),
                                   Mathf.Sin(ph) * Mathf.Sin(th) * 0.86f,
                                   Mathf.Cos(ph) * 1.02f) * (rs * k);
            }
            for (int j = 0; j < lat; j++)
            {
                float p0 = Mathf.PI * j / lat, p1 = Mathf.PI * (j + 1) / lat;
                for (int i = 0; i < lon; i++)
                {
                    float q0 = i * Mathf.PI * 2f / lon, q1 = (i + 1) * Mathf.PI * 2f / lon;
                    int s00 = j * 31 + i * 7, s10 = j * 31 + ((i + 1) % lon) * 7;
                    int s01 = (j + 1) * 31 + i * 7, s11 = (j + 1) * 31 + ((i + 1) % lon) * 7;
                    Vector3 a = V(p0, q0, s00), b = V(p0, q1, s10);
                    Vector3 c = V(p1, q1, s11), d = V(p1, q0, s01);
                    Vector3 n = ((a + b + c + d) * 0.25f).normalized;
                    if (j == 0) sb2.AddTriangle(a, c, d, n, uv);
                    else if (j == lat - 1) sb2.AddTriangle(a, b, c, n, uv);
                    else sb2.AddQuad(a, b, c, d, n, uv);
                }
            }
            soul = SaveMesh(sb2.ToMesh("SoulObject"), "SoulObject");

            // ── 체결 볼트 ──────────────────────────────────────────────────
            // 링을 도어에 물리는 볼트. **8개, 등간격, 모든 창이 같다.**
            var bb = new ProcMeshBuilder();
            bb.AddPrism(Vector3.zero, 0.0145f, 0.0125f, 0.014f, 6, MeshAxis.Z, 0f, true, true, false, uv);
            bolt = SaveMesh(bb.ToMesh("WindowClampBolt"), "WindowClampBolt");

            // ── 도어 힌지 ──────────────────────────────────────────────────
            // 경첩 몸통 + 핀. 「실제로 열릴 것처럼 보이는 작은 힌지」.
            var hb2 = new ProcMeshBuilder();
            hb2.AddBox(new Vector3(0f, 0f, -0.010f),
                       new Vector3(ReferenceRoomSpec.DoorHingeWidth,
                                   ReferenceRoomSpec.DoorHingeHeight, 0.020f), 0f, uv);
            hb2.AddPrism(new Vector3(-ReferenceRoomSpec.DoorHingeWidth * 0.5f, 0f, -0.020f),
                         0.011f, 0.011f, ReferenceRoomSpec.DoorHingeHeight * 1.15f,
                         8, MeshAxis.Y, 0f, true, true, false, uv);
            hinge = SaveMesh(hb2.ToMesh("ChamberDoorHinge"), "ChamberDoorHinge");

            // ── 잠금 클램프 ────────────────────────────────────────────────
            //
            // **원점이 회전축이다.** 이 메시는 체결될 때 z 축으로 34도 돈다
            // (`CustomsLockView`). 회전축이 원점이 아니면 클램프가 제자리에서
            // 도는 게 아니라 궤도를 그려 「부품이 날아간다」로 보인다.
            //
            // 몸통은 회전축에서 **한쪽으로만** 뻗는다 — 대칭이면 돌아도 실루엣이
            // 그대로라 「움직였다」가 화면에 없다. 이것이 상태 탭과 함께
            // 「9개 챔버 잠금장치가 한 번에 체결됨」을 보여 주는 유일한 형상이다.
            var lb = new ProcMeshBuilder();
            lb.AddPrism(Vector3.zero, 0.013f, 0.013f, 0.026f, 8, MeshAxis.Z, 0f, true, true, false, uv);
            lb.AddBox(new Vector3(0f, -ReferenceRoomSpec.DoorClampHeight * 0.55f, -0.008f),
                      new Vector3(ReferenceRoomSpec.DoorClampWidth * 0.5f,
                                  ReferenceRoomSpec.DoorClampHeight * 1.1f, 0.018f), 0f, uv);
            lb.AddBox(new Vector3(0f, -ReferenceRoomSpec.DoorClampHeight * 1.05f, -0.014f),
                      new Vector3(ReferenceRoomSpec.DoorClampWidth, 0.016f, 0.024f), 0f, uv);
            clamp = SaveMesh(lb.ToMesh("ChamberLockClamp"), "ChamberLockClamp");
        }

        /// <summary>
        /// 영혼 발광 머티리얼. 명세 §4 「중심부만 약하게 붉게 발광 · 유리 전체를 밝히지
        /// 말고 · 가장 밝은 모듈도 주변을 강하게 밝힐 정도로 발광하지 않는다」.
        ///
        /// 세기를 칸마다 조금씩 다르게 하되 **결정론적**으로 만든다.
        /// </summary>
        /// <summary>
        /// URP/Lit 을 **알파 블렌드 투명**으로 세운다.
        ///
        /// 프로퍼티 하나만 바꾸면 안 된다 — `_Surface` 는 인스펙터 표시를,
        /// 키워드는 셰이더 변형을, `renderQueue` 는 정렬을 각각 정한다.
        /// 셋 중 하나라도 빠지면 **에디터에서는 투명해 보이는데 빌드에서 불투명**
        /// 같은 방식으로 어긋난다. 그래서 한 곳에 묶는다.
        /// </summary>
        private static void Transparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);     // Alpha
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }

        private static void Opaque(Material m)
        {
            m.SetFloat("_Surface", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            m.SetFloat("_ZWrite", 1f);
            m.renderQueue = -1;
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        private static Material SoulMaterial(int seed, bool core)
        {
            string key = core ? $"SoulCore_{seed}" : $"SoulShell_{seed}";
            string path = $"{MaterialDir}/RM_{key}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            Material m = existing ?? new Material(P("RedPaint")) { name = $"RM_{key}" };

            // ⚠ **이 값들은 실측으로 정해졌다. 되돌리지 않는다.**
            //
            // 발광을 ×3.6 으로 줬더니 톤매핑이 채널을 전부 포화시켜 **색상 정보가
            // 사라졌다** — 구슬이 붉지 않고 **흰색**으로 보였고 붉은 화소가 0.13% 였다.
            // ×1.05 로 내리자 0.91% 로 올랐다. HDR 발광은 세게 줄수록 붉어지는 게
            // 아니라 **희어진다.** 1 을 살짝 넘겨 블룸만 걸리고 포화는 안 되는 값이다.
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null) m.shader = lit;
            m.SetFloat("_Smoothness", 0.55f);
            m.SetFloat("_Metallic", 0f);

            // ⚠ **텍스처를 뗀다.** 이 재질은 `RM_RedPaint` 를 복제해 만들어지는데,
            // 거기엔 `TEX_Machine_Housing`(무쇠와 기름때)이 물려 있다. 그대로 두면
            // 영혼이 **도장된 금속 덩어리**로 보인다 — 실측으로 그렇게 붙어 있었다.
            // 영혼은 물질이 아니라 빛이므로 순수 색이어야 한다.
            m.SetTexture("_BaseMap", null);
            m.SetTexture("_EmissionMap", null);
            m.DisableKeyword("_EMISSIONMAP_ON");
            if (m.HasProperty("_EmissionMapEnabled")) m.SetFloat("_EmissionMapEnabled", 0f);

            // 밝기도 칸마다 다르다. `seed % 3` 은 세 값만 만들어 **열마다 같은 밝기**가
            // 반복됐다 — 규칙적인 반복은 개체차가 아니라 패턴으로 읽힌다.
            float k = 0.68f + ProcMeshBuilder.Hash01(seed * 733 + 41) * 0.52f;
            if (core)
            {
                // 핵만 1 을 넘긴다. **작은 면적**이라 블룸이 넓게 번지지 않아
                // 채널 포화 없이 「뜨겁다」가 읽힌다.
                m.SetColor("_BaseColor", new Color(0.30f, 0.03f, 0.01f, 1f));
                m.SetColor("_EmissionColor", new Color(1.00f, 0.13f, 0.05f) * (1.15f * k));
                Opaque(m);
            }
            else
            {
                // 껍질은 **1 을 넘지 않는다.** 넘는 순간 다시 균일한 흰 덩어리다.
                // 반투명이라 뒤의 핵이 비치고, 그 겹침이 밀도로 읽힌다.
                m.SetColor("_BaseColor", new Color(0.26f, 0.030f, 0.016f, 0.48f));
                m.SetColor("_EmissionColor", new Color(0.86f, 0.16f, 0.07f) * (0.24f * k));
                Transparent(m);
                m.SetFloat("_Smoothness", 0.72f);
            }
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            if (existing == null) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            return m;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  §5 실행 레버
        // ══════════════════════════════════════════════════════════════════════

        private static void BuildLeverColumn(GameObject root)
        {
            GameObject column = Child(root.transform, LeverBaseName);
            column.transform.localPosition = new Vector3(ReferenceRoomSpec.LeverColumnCenterX, 0f, ReferenceRoomSpec.WallRearZ);

            float cw = ReferenceRoomSpec.LeverColumnWidth;
            float ch = ReferenceRoomSpec.LeverColumnHeight;
            float cd = ReferenceRoomSpec.LeverColumnDepth;
            float uv = ReferenceRoomSpec.HeroUvPerMeter;

            Slab(column.transform, "Housing", new Vector3(0f, ReferenceRoomSpec.LeverColumnCenterY, -cd * 0.5f),
                 new Vector3(cw, ch, cd), "Steel");

            // ── 기계 장착부 ───────────────────────────────────────────────
            //
            // 「레버가 벽에 그냥 꽂혀 있는」 것으로 보이면 안 된다. 실제 기계식
            // 레버는 **판 → 허브 → 암** 순서로 하중을 넘긴다. 그 셋이 다 보여야
            // 「당기면 무언가가 실제로 움직인다」가 읽힌다.
            float bpw = ReferenceRoomSpec.LeverBasePlateW;
            float bph = ReferenceRoomSpec.LeverBasePlateH;
            float bpt = ReferenceRoomSpec.LeverBasePlateT;

            // ① 베이스 플레이트 — 하우징 표면에 볼트로 물린 두꺼운 판.
            Slab(column.transform, "BasePlate",
                 new Vector3(0f, ReferenceRoomSpec.LeverPivotY, -cd - bpt * 0.5f),
                 new Vector3(bpw, bph, bpt), "BareSteel");

            // 플레이트 네 귀퉁이 볼트 — 「떼어낼 수 있는 부품」이라고 말한다.
            var plateBolts = new GameObject("BasePlateBolts");
            plateBolts.transform.SetParent(column.transform, false);
            var pbB = new ProcMeshBuilder();
            pbB.AddPrism(Vector3.zero, 0.013f, 0.011f, 0.014f, 6, MeshAxis.Z, 0f, true, true, false, uv);
            Mesh plateBoltMesh = SaveMesh(pbB.ToMesh("LeverPlateBolt"), "LeverPlateBolt");
            for (int i = 0; i < 4; i++)
            {
                float bx = ((i & 1) == 0 ? -1f : 1f) * (bpw * 0.5f - 0.030f);
                float by = ReferenceRoomSpec.LeverPivotY + ((i & 2) == 0 ? -1f : 1f) * (bph * 0.5f - 0.030f);
                AddBolt(plateBolts.transform, plateBoltMesh, new Vector3(bx, by, -cd - bpt - 0.006f), $"PlateBolt_{i}");
            }

            // ② 하중 허브 — 플레이트에서 **앞으로 튀어나온** 굵은 원통.
            // 암의 회전축이 이 안에 들어간다. 허브가 없으면 암이 판에 붙어
            // 회전하는 것처럼 보이고, 그건 종이 인형이다.
            float hubR = ReferenceRoomSpec.LeverHubRadius;
            float hubD = ReferenceRoomSpec.LeverHubDepth;
            var hubB = new ProcMeshBuilder();
            hubB.AddPrism(Vector3.zero, hubR, hubR, hubD, 12, MeshAxis.Z, 0f, true, true, false, uv);
            hubB.AddPrism(new Vector3(0f, 0f, hubD * 0.5f - 0.008f), hubR * 1.30f, hubR * 1.30f, 0.016f,
                          12, MeshAxis.Z, 0f, true, true, false, uv);
            // 허브에 물린 그리스 니플 — 정비 대상이라는 신호
            hubB.AddPrism(new Vector3(hubR * 0.72f, hubR * 0.72f, -hubD * 0.5f - 0.008f),
                          0.008f, 0.006f, 0.018f, 6, MeshAxis.Z, 0f, true, true, false, uv);
            Mesh hubMesh = SaveMesh(hubB.ToMesh("LeverHub"), "LeverHub");
            var hub = new GameObject("PivotHub");
            hub.transform.SetParent(column.transform, false);
            hub.transform.localPosition = new Vector3(0f, ReferenceRoomSpec.LeverPivotY, -cd - bpt - hubD * 0.5f);
            // 허브는 **가공된 축받이**다. 베이스판과 같은 재질이면 근접에서 둘이
            // 한 덩어리 콘크리트로 읽힌다 — 실제로 그렇게 보였다.
            Render(hub, hubMesh, "Collar");

            // ②b 구동 로드 — 허브의 회전을 **상단 공통축까지 올린다.**
            //
            // ⚠ 지시의 사슬(허브 → 공통 수평축)에 없는 부품이라 근거를 남긴다.
            // 공통축은 상단 하우징(y≈1.93)에 있고 허브는 손이 닿아야 하므로 1.25 다.
            // 둘을 직결하려면 공통축을 내리거나(→ 하우징이 3×3 격자 한가운데를
            // 가로질러 「하나의 직사각형 기계」가 깨진다) 허브를 올려야 한다
            // (→ 눈높이 1.62 에서 손이 안 닿는다). 그래서 세로 로드를 **명시적
            // 부품으로** 넣는다. 이름이 있고, 무엇을 무엇으로 옮기는지 한 문장으로
            // 답할 수 있고, 없으면 사슬이 끊긴다 — 지시의 부품 판단 규칙을 통과한다.
            float rodW = ReferenceRoomSpec.LeverDriveRodWidth;
            float rodTop = ReferenceRoomSpec.CommonShaftY;
            float rodLen = rodTop - ReferenceRoomSpec.LeverPivotY;
            Slab(column.transform, DriveRodName,
                 new Vector3(0f, ReferenceRoomSpec.LeverPivotY + rodLen * 0.5f, -cd - bpt - 0.018f),
                 new Vector3(rodW, rodLen, 0.036f), "Collar");

            // 로드를 컬럼에 잡아 두는 가이드 셋. 「이 로드는 여기를 지난다」.
            for (int i = 0; i < 3; i++)
            {
                float t = (i + 0.5f) / 3f;
                Slab(column.transform, $"RodGuide_{i}",
                     new Vector3(0f, Mathf.Lerp(ReferenceRoomSpec.LeverPivotY, rodTop, t), -cd - bpt - 0.006f),
                     new Vector3(rodW * 1.9f, 0.026f, 0.030f), "BareSteel");
            }

            // ②c 모서리 기어박스 — 세로 로드가 가로 공통축으로 방향을 바꾸는 자리.
            //
            // ⚠ **z 를 손으로 적지 않는다.** 첫 판본은 `-cd - bpt - 0.020` 이었고,
            // 공통축(캐비닛 쪽에서 계산됨)과 **206mm 어긋났다.** 두 부품이 만나는
            // 지점의 좌표는 한 곳에서만 나와야 한다 — `CommonShaftZ` 가 그 한 곳이다.
            float cbx = ReferenceRoomSpec.LeverCornerBoxSize;
            Slab(column.transform, "CornerGearbox",
                 new Vector3(0f, rodTop, ReferenceRoomSpec.CommonShaftZ),
                 new Vector3(cbx * 1.3f, cbx, cbx * 0.62f), "Collar");

            // ③ 시작·끝 스토퍼 — 「이 범위를 넘지 않는다」를 형상으로 말한다.
            // 각도 계산 없이 슬롯 양 끝에 둔다. 두 개가 마주 보아야 범위로 읽힌다.
            float stop = ReferenceRoomSpec.LeverStopperSize;
            float travelH = ReferenceRoomSpec.LeverHandleLength
                          * Mathf.Sin(ReferenceRoomSpec.LeverSwingDegrees * 0.5f * Mathf.Deg2Rad);
            for (int i = 0; i < 2; i++)
            {
                float sy = ReferenceRoomSpec.LeverPivotY + (i == 0 ? travelH : -travelH);
                Slab(column.transform, i == 0 ? "StopperTop" : "StopperBottom",
                     new Vector3(0f, sy, -cd - bpt - stop * 0.5f),
                     new Vector3(stop * 2.2f, stop, stop), i == 0 ? "BareSteel" : "Rust");
            }

            // ④ 잠금핀 — **경로를 물리적으로 막는 부품.** 「왜 안 내려가는가」의 답이
            // 화면 안에 있어야 한다. 자물쇠 아이콘이 아니라 쇠막대가 답이다.
            var pinB = new ProcMeshBuilder();
            // ⚠ 핀이 작으면 「왜 안 내려가는가」의 답이 화면에서 안 보인다.
            // 근접 캡처에서 폭 15mm 짜리가 배경에 묻혔다.
            pinB.AddPrism(Vector3.zero, ReferenceRoomSpec.LeverPinRadius, ReferenceRoomSpec.LeverPinRadius,
                          ReferenceRoomSpec.LeverPinLength, 8, MeshAxis.X, 0f, true, true, false, uv);
            pinB.AddPrism(new Vector3(-ReferenceRoomSpec.LeverPinLength * 0.5f - 0.008f, 0f, 0f),
                          ReferenceRoomSpec.LeverPinRadius * 1.7f, ReferenceRoomSpec.LeverPinRadius * 1.7f,
                          0.016f, 8, MeshAxis.X, 0f, true, true, false, uv);
            Mesh pinMesh = SaveMesh(pinB.ToMesh("LeverLockPin"), "LeverLockPin");
            var pin = new GameObject(LeverLockPinName);
            pin.transform.SetParent(column.transform, false);
            // 잠금핀은 **암이 지나갈 자리** 바로 아래에 있다 — 살짝만 내려가다 여기 부딪힌다.
            float pinDrop = ReferenceRoomSpec.LeverHandleLength
                          * Mathf.Sin(ReferenceRoomSpec.LeverLockedTravelDegrees * Mathf.Deg2Rad);
            // ⚠ **허브와 겹치면 안 된다.** 근접 캡처에서 핀이 허브 안에 박힌 것처럼
            // 보였고, 그러면 「축의 일부」로 읽혀 막는 부품이라는 정보가 사라진다.
            // 허브보다 **앞으로**(방 쪽) 그리고 **아래로** 빼서, 내려오는 암이
            // 부딪히는 자리에 홀로 서 있게 한다.
            pin.transform.localPosition = new Vector3(
                ReferenceRoomSpec.LeverPinLength * 0.30f,
                ReferenceRoomSpec.LeverPivotY - pinDrop - 0.058f,
                -cd - bpt - hubD - 0.062f);
            Render(pin, pinMesh, "RedPaint");

            // ⚠ **`LinkShaft` 를 만들지 않는다.** 레버에서 장치로 가로질러 가던
            // 그 축은 「레버와 장치가 같은 기계다」를 말하려던 것인데, 지금은
            // 컬럼이 캐비닛에 **직접 결합**돼 있고(간격 0, 베이스 플레이트가
            // 캐비닛을 10mm 물고 들어간다) 구동 로드가 상단 하우징까지 올라간다.
            // 그 둘이 같은 말을 이미 하므로, 세 번째 축은 지시가 금지한
            // 「실제 연결 대상을 알 수 없는 선」이 된다.

            // ⑥ 수직 슬롯. 레버가 움직이는 범위를 형상으로 말한다.
            float slotH = 2f * ReferenceRoomSpec.LeverHandleLength * Mathf.Sin(ReferenceRoomSpec.LeverSwingDegrees * 0.5f * Mathf.Deg2Rad);
            Slab(column.transform, "Slot", new Vector3(0f, ReferenceRoomSpec.LeverPivotY, -cd - 0.004f),
                 new Vector3(ReferenceRoomSpec.LeverSlotWidth, slotH, 0.02f), "Grease");

            // ⚠ **톱니 7개와 스프링 가이드를 만들지 않는다.** 지시 「레버 옆에
            // 의미 없는 작은 장치, 손잡이, 다이얼을 추가하지 마라」 — 컬럼에 허용된
            // 부품 여덟에 톱니는 없다. 그 톱니는 「기계식 걸쇠처럼 보이게」 하려고
            // 넣은 것이고, 실제로 무엇을 거는지 답할 수 없었다.

            // ── 손잡이 ──
            // 명세 §12 「레버: 손잡이 하단의 실제 회전축」 — 피벗을 별도 트랜스폼으로 둔다.
            var pivot = new GameObject(LeverHandleName);
            pivot.transform.SetParent(column.transform, false);
            pivot.transform.localPosition = new Vector3(0f, ReferenceRoomSpec.LeverPivotY, -cd);

            float armLen = ReferenceRoomSpec.LeverHandleLength - ReferenceRoomSpec.LeverGripLength;
            // ── 손잡이 조립체 ────────────────────────────────────────────────
            // 사용자 지적: 「레버의 디자인이 너무 그냥 박스 형태」.
            // 실제 산업용 레버의 실루엣은 **축 보스 · 클레비스(포크) · 각진 팔 ·
            // 홈이 파인 그립** 넷으로 읽힌다. 상자 하나로는 어느 쪽이 축인지도 안 보인다.
            var hb = new ProcMeshBuilder();

            // ① 회전축 보스 — 굵은 원통 + 바깥 플랜지. 「여기가 축이다」를 형상으로 말한다.
            hb.AddPrism(Vector3.zero, 0.034f, 0.034f, 0.058f, 10, MeshAxis.Z, 0f, true, true, false, uv);
            hb.AddPrism(new Vector3(0f, 0f, -0.034f), 0.048f, 0.040f, 0.014f, 10, MeshAxis.Z, 0f, true, true, false, uv);
            // 보스 둘레 볼트 넷
            for (int i = 0; i < 4; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 0.5f;
                hb.AddPrism(new Vector3(Mathf.Cos(a) * 0.040f, Mathf.Sin(a) * 0.040f, -0.044f),
                            0.008f, 0.007f, 0.010f, 6, MeshAxis.Z, 0f, true, true, false, uv);
            }

            // ② 클레비스 — 보스를 감싸는 두 갈래 포크. 팔이 축에 **물려 있다**를 만든다.
            for (int s = -1; s <= 1; s += 2)
            {
                hb.AddBox(new Vector3(0f, s * 0.030f, -0.055f), new Vector3(0.020f, 0.016f, 0.075f), 0f, uv);
            }

            // ③ 팔 — 단면이 세로로 긴 각재. 정사각 단면은 방향이 안 읽힌다.
            hb.AddBox(new Vector3(0f, 0f, -armLen * 0.5f - 0.055f),
                      new Vector3(0.020f, 0.034f, armLen), 0f, uv);
            // 팔 중간 보강 리브
            hb.AddBox(new Vector3(0f, 0f, -armLen * 0.5f - 0.055f),
                      new Vector3(0.030f, 0.012f, armLen * 0.55f), 0f, uv);

            // ④ 그립 목 — 팔에서 그립으로 넘어가는 테이퍼. 이게 없으면 그립이 떠 보인다.
            hb.AddPrism(new Vector3(0f, 0f, -armLen - 0.045f), 0.017f, 0.026f, 0.038f,
                        8, MeshAxis.Z, 0f, true, true, false, uv);
            Mesh armMesh = SaveMesh(hb.ToMesh("LeverArm"), "LeverArm");
            var arm = new GameObject("Arm");
            arm.transform.SetParent(pivot.transform, false);
            Render(arm, armMesh, "BareSteel");

            // 붉은 원통 그립. 명세 §5 「칠이 벗겨진 어두운 적색 도장 · 8각 이하」.
            // 원통 하나가 아니라 **홈이 파인 손잡이**로 만든다. 손이 닿는 물건은
            // 미끄럼 방지 홈이 있고, 그 홈이 실루엣에서 「잡는 것」이라고 말한다.
            var gb = new ProcMeshBuilder();
            float gr = ReferenceRoomSpec.LeverGripDiameter * 0.5f;
            int gs = ReferenceRoomSpec.LeverGripSides;
            float gl = ReferenceRoomSpec.LeverGripLength;

            // 뒤쪽 칼라(손이 미끄러져 빠지지 않게 하는 턱)
            gb.AddPrism(new Vector3(0f, 0f, gl * 0.5f - 0.012f), gr * 1.45f, gr * 1.45f, 0.016f,
                        gs, MeshAxis.Z, 0f, true, true, false, uv);

            // 본체를 다섯 마디로 나누고 마디마다 굵기를 번갈아 — 홈이 생긴다.
            const int knurls = 5;
            float seg = (gl - 0.030f) / knurls;
            for (int i = 0; i < knurls; i++)
            {
                float z = gl * 0.5f - 0.024f - seg * (i + 0.5f);
                float r = (i % 2 == 0) ? gr : gr * 0.80f;
                gb.AddPrism(new Vector3(0f, 0f, z), r, r, seg, gs, MeshAxis.Z,
                            (i % 2) * (180f / gs), true, true, false, uv);
            }

            // 앞쪽 마감 캡 — 둥근 끝. 잘린 원통은 「부러진 것」으로 보인다.
            gb.AddPrism(new Vector3(0f, 0f, -gl * 0.5f + 0.008f), gr * 1.18f, gr * 0.55f, 0.026f,
                        gs, MeshAxis.Z, 0f, true, true, false, uv);
            Mesh gripMesh = SaveMesh(gb.ToMesh("LeverGrip"), "LeverGrip");
            var grip = new GameObject("Grip");
            grip.transform.SetParent(pivot.transform, false);
            grip.transform.localPosition = new Vector3(0f, 0f, -armLen - gl * 0.5f - 0.055f);
            Render(grip, gripMesh, "RedPaint");

            // ── 동작 ──
            // 명세 §5 「레버는 수직 슬롯을 따라 약 55도 범위로 움직인다」.
            //
            // ⚠ **한 트랜스폼에 주인은 하나다.** 전에는 `LeverPhysics` 를 붙였는데,
            // 그것은 반동만 알고 잠김·처리·복귀를 모른다. 나머지를 코루틴으로 덧대면
            // 두 주인이 같은 프레임에 각도를 써서 떨린다. `LeverStateMachine` 은
            // 여덟 상태를 한 시간축에서 굴려 그 경합 자체를 없앤다.
            //
            // (실측: `LeverPhysics.Pull()` 의 런타임 호출자가 0 개였다 — 레버는
            //  지금까지 한 번도 움직인 적이 없다. 반동을 정교하게 만들어 둔 것과
            //  그것이 화면에서 일어나는 것은 다른 문제였다.)
            var fsm = pivot.AddComponent<Prototype.View.LeverStateMachine>();
            fsm.Configure(pivot.transform, Vector3.right, ReferenceRoomSpec.LeverSwingDegrees);
            var pso = new SerializedObject(fsm);
            SetFloat(pso, "_lockedTravelDegrees", ReferenceRoomSpec.LeverLockedTravelDegrees);
            pso.ApplyModifiedProperties();

            // ── 경고등 ──
            GameObject lamp = Child(root.transform, WarningLampName);
            lamp.transform.localPosition = new Vector3(ReferenceRoomSpec.LeverColumnCenterX,
                                                      ReferenceRoomSpec.WarningLampCenterY,
                                                      ReferenceRoomSpec.WallRearZ);
            float lr = ReferenceRoomSpec.WarningLampDiameter * 0.5f;
            var lb = new ProcMeshBuilder();
            lb.AddPrism(new Vector3(0f, 0f, -0.030f), lr, lr, 0.060f, ReferenceRoomSpec.WarningLampSides, MeshAxis.Z, 0f, true, true, false, uv);
            Mesh housingMesh = SaveMesh(lb.ToMesh("WarningLampHousing"), "WarningLampHousing");
            var housing = new GameObject("Housing");
            housing.transform.SetParent(lamp.transform, false);
            Render(housing, housingMesh, "BareSteel");

            var lensB = new ProcMeshBuilder();
            lensB.AddPrism(Vector3.zero, lr * 0.72f, lr * 0.56f, 0.030f, ReferenceRoomSpec.WarningLampSides, MeshAxis.Z, 0f, true, true, false, uv);
            Mesh lensMesh = SaveMesh(lensB.ToMesh("WarningLampLens"), "WarningLampLens");
            var lens = new GameObject("Lens");
            lens.transform.SetParent(lamp.transform, false);
            lens.transform.localPosition = new Vector3(0f, 0f, -0.062f);
            Renderer lensRenderer = Render(lens, lensMesh, "RedPaint");

            // 명세 §5 「평상시에는 매우 약하게 빛남」 — 기본 발광을 낮게 두고,
            // 런타임에 `MaterialPropertyBlock` 이 점멸을 건다.
            var lensMat = new Material(P("RedPaint")) { name = "RM_WarningLens" };
            lensMat.SetColor("_BaseColor", ReferenceRoomSpec.FadedRed);
            lensMat.SetColor("_EmissionColor", new Color(0.85f, 0.12f, 0.07f) * 0.35f);
            lensMat.EnableKeyword("_EMISSION");
            lensMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            SaveMaterial(lensMat, "WarningLens");
            lensRenderer.sharedMaterial = lensMat;

            // 보호 링과 볼트
            var ringB = new ProcMeshBuilder();
            for (int i = 0; i < ReferenceRoomSpec.WarningLampSides; i++)
            {
                float am = (i + 0.5f) * Mathf.PI * 2f / ReferenceRoomSpec.WarningLampSides;
                float segLen = 2f * Mathf.Tan(Mathf.PI / ReferenceRoomSpec.WarningLampSides) * lr * 0.86f;
                ringB.AddBox(new Vector3(Mathf.Cos(am) * lr * 0.86f, Mathf.Sin(am) * lr * 0.86f, -0.066f),
                             new Vector3(segLen, 0.026f, 0.020f),
                             Quaternion.Euler(0f, 0f, am * Mathf.Rad2Deg + 90f), 0f, uv);
            }
            Mesh guardMesh = SaveMesh(ringB.ToMesh("WarningLampGuard"), "WarningLampGuard");
            var guard = new GameObject("Guard");
            guard.transform.SetParent(lamp.transform, false);
            Render(guard, guardMesh, "BareSteel");

            // ⚠ **표지판을 세우지 않는다.** 지시가 「단순 장식용 위험 표지판」을
            // 제거 대상으로 명시했다. 「글자는 나중에 텍스처로」라는 계획이 세
            // 배치째 이어졌고, 그동안 그것은 기능을 설명할 수 없는 밝은 사각형이었다.

            _report.AppendLine($"  {LeverBaseName} — 컬럼 {cw:F2} × {ch:F3} × {cd:F2} @ x={ReferenceRoomSpec.LeverColumnCenterX:F3} " +
                               $"· 캐비닛과 간격 {ReferenceRoomSpec.LeverGapFromMachine:F2} " +
                               $"· 베이스 플레이트 겹침 {ReferenceRoomSpec.LeverPlateOverlap * 1000f:F0}mm");
            _report.AppendLine($"    사슬 — 그립 → 암 → 허브(y={ReferenceRoomSpec.LeverPivotY}) → 구동 로드 {rodLen:F3}m " +
                               $"→ 기어박스(y={rodTop:F3}) → 공통축 → 캠 로드 3 → 클램프 9");
            _report.AppendLine($"    가동 {ReferenceRoomSpec.LeverSwingDegrees}° (슬롯 {slotH:F3}m) " +
                               $"· {WarningLampName} ⌀{ReferenceRoomSpec.WarningLampDiameter} @ y={ReferenceRoomSpec.WarningLampCenterY:F3}");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  §6 전력 표시기
        // ══════════════════════════════════════════════════════════════════════

        private static void BuildPowerMeter(GameObject root)
        {
            GameObject meter = Child(root.transform, PowerMeterName);
            meter.transform.localPosition = new Vector3(ReferenceRoomSpec.PowerMeterCenterX,
                                                       ReferenceRoomSpec.PowerMeterCenterY,
                                                       ReferenceRoomSpec.WallRearZ);

            float w = ReferenceRoomSpec.PowerMeterWidth;
            float h = ReferenceRoomSpec.PowerMeterHeight;
            float d = ReferenceRoomSpec.PowerMeterDepth;
            float bezel = 0.055f;

            // 두꺼운 검은 철제 프레임. 명세 §6.
            Slab(meter.transform, "Frame_Top",    new Vector3(0f,  h * 0.5f - bezel * 0.5f, -d * 0.5f), new Vector3(w, bezel, d), "Steel");
            Slab(meter.transform, "Frame_Bottom", new Vector3(0f, -h * 0.5f + bezel * 0.5f, -d * 0.5f), new Vector3(w, bezel, d), "Steel");
            Slab(meter.transform, "Frame_Left",   new Vector3(-w * 0.5f + bezel * 0.5f, 0f, -d * 0.5f), new Vector3(bezel, h, d), "Steel");
            Slab(meter.transform, "Frame_Right",  new Vector3( w * 0.5f - bezel * 0.5f, 0f, -d * 0.5f), new Vector3(bezel, h, d), "Steel");
            Slab(meter.transform, "Back",         new Vector3(0f, 0f, -d * 0.15f), new Vector3(w, h, d * 0.3f), "Steel");

            // 화면. 명세 §6 「오래된 전광판 또는 세그먼트 숫자 장치 · 유리는 약간 흐리다」.
            // **`Readout` 은 배선 지점이다** — `InstrumentPanelView` 가 여기에 숫자를 그린다.
            var readout = new GameObject("Readout");
            readout.transform.SetParent(meter.transform, false);
            readout.transform.localPosition = new Vector3(0f, 0f, -d + 0.012f);
            Slab(readout.transform, "Screen", Vector3.zero, new Vector3(w - bezel * 2f, h - bezel * 2f, 0.012f), "Glass");

            // 텍스트 앵커 셋. 명세 §6 이 요구하는 세 줄의 자리를 **형상으로 남긴다** —
            // 나중에 TMP 를 붙일 때 위치를 다시 재지 않아도 되고, 캡처 판정이
            // 「어느 줄이 안 읽히는가」를 지목할 수 있다.
            Anchor(readout.transform, "Anchor_Power",    new Vector3(0f,  (h - bezel * 2f) * 0.34f, -0.010f));
            Anchor(readout.transform, "Anchor_Value",    new Vector3(0f,  0f,                       -0.010f));
            Anchor(readout.transform, "Anchor_Required", new Vector3(0f, -(h - bezel * 2f) * 0.36f, -0.010f));

            _report.AppendLine($"  {PowerMeterName} — {w} × {h} × {d} @ x={ReferenceRoomSpec.PowerMeterCenterX:F2} y={ReferenceRoomSpec.PowerMeterCenterY} " +
                               $"· 읽기 거리 요구 {ReferenceRoomSpec.PowerMeterReadDistance}m");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  §7 오른쪽 보관 선반
        // ══════════════════════════════════════════════════════════════════════

        private static void BuildStorage(GameObject root)
        {
            GameObject shelf = Child(root.transform, ShelfName);
            shelf.transform.localPosition = new Vector3(ReferenceRoomSpec.ShelfCenterX, 0f, ReferenceRoomSpec.ShelfCenterZ);

            float len = ReferenceRoomSpec.ShelfLength;
            float dep = ReferenceRoomSpec.ShelfDepth;
            float top = ReferenceRoomSpec.ShelfTopHeight;
            float tt = ReferenceRoomSpec.ShelfTopThickness;
            float low = ReferenceRoomSpec.ShelfLowerHeight;

            Slab(shelf.transform, "TopPlate", new Vector3(0f, top - tt * 0.5f, 0f), new Vector3(dep, tt, len), "BareSteel");
            Slab(shelf.transform, "LowerShelf", new Vector3(0f, low, 0f), new Vector3(dep - 0.06f, 0.03f, len - 0.10f), "Steel");

            // 용접된 앵글 철제 프레임 — 수직 지지대. 명세 §7 「네 개 이상」.
            int legs = ReferenceRoomSpec.ShelfLegCount;
            for (int i = 0; i < legs; i++)
            {
                float z = Mathf.Lerp(-len * 0.5f + 0.09f, len * 0.5f - 0.09f, i / (float)(legs - 1));
                for (int s = 0; s < 2; s++)
                {
                    float x = s == 0 ? -dep * 0.5f + 0.045f : dep * 0.5f - 0.045f;
                    Slab(shelf.transform, $"Leg_{i}_{s}", new Vector3(x, (top - tt) * 0.5f, z),
                         new Vector3(0.045f, top - tt, 0.045f), "BareSteel");
                }
            }
            // 앞뒤 가로대
            Slab(shelf.transform, "Rail_Front", new Vector3(-dep * 0.5f + 0.045f, low - 0.03f, 0f), new Vector3(0.035f, 0.035f, len - 0.14f), "BareSteel");
            Slab(shelf.transform, "Rail_Back",  new Vector3( dep * 0.5f - 0.045f, low - 0.03f, 0f), new Vector3(0.035f, 0.035f, len - 0.14f), "BareSteel");

            // ── 프롭 ──
            // 명세 §7 이 상판·하단에 놓을 물건을 **정확히 열거한다.** 그 이상 놓지 않는다
            // (명세 「별도의 지시가 없는 요소를 임의로 추가하지 마라」).
            GameObject props = Child(root.transform, PropsName);
            props.transform.localPosition = shelf.transform.localPosition;

            float topY = top;
            // 상판: 공구함 1 · TOOLS 01 상자 1 · 부품 바구니 1 · 오일통 2~3
            Prop(props.transform, "Toolbox",     new Vector3(0f, topY + 0.11f, -0.95f), new Vector3(0.24f, 0.22f, 0.44f), "BareSteel");
            Prop(props.transform, "Handle",      new Vector3(0f, topY + 0.24f, -0.95f), new Vector3(0.03f, 0.05f, 0.20f), "Grease");
            Prop(props.transform, "Box_Tools01", new Vector3(0f, topY + 0.08f, -0.34f), new Vector3(0.28f, 0.16f, 0.46f), "Steel");
            Prop(props.transform, "PartsBasket", new Vector3(0f, topY + 0.07f,  0.16f), new Vector3(0.26f, 0.14f, 0.30f), "Rust");
            Prop(props.transform, "OilCan_A",    new Vector3(-0.06f, topY + 0.13f, 0.66f), new Vector3(0.15f, 0.26f, 0.15f), "Grease");
            Prop(props.transform, "OilCan_B",    new Vector3( 0.10f, topY + 0.10f, 0.83f), new Vector3(0.13f, 0.20f, 0.13f), "Grease");
            Prop(props.transform, "OilCan_C",    new Vector3(-0.02f, topY + 0.08f, 1.06f), new Vector3(0.12f, 0.16f, 0.12f), "Rust");

            // 하단: 보급품 상자 2~3 · SUPPLIES 12 큰 상자 1 · 붉은 스트랩 공구 케이스 1 · 수리 장비 1
            float lowY = low + 0.015f;
            Prop(props.transform, "Supply_A",       new Vector3(0f, lowY + 0.14f, -0.92f), new Vector3(0.40f, 0.28f, 0.50f), "Steel");
            Prop(props.transform, "Supply_B",       new Vector3(0f, lowY + 0.12f, -0.32f), new Vector3(0.38f, 0.24f, 0.42f), "Rust");
            Prop(props.transform, "Box_Supplies12", new Vector3(0f, lowY + 0.17f,  0.28f), new Vector3(0.44f, 0.34f, 0.62f), "Steel");
            Prop(props.transform, "ToolCase",       new Vector3(0f, lowY + 0.10f,  0.92f), new Vector3(0.36f, 0.20f, 0.44f), "BareSteel");
            Prop(props.transform, "Strap",          new Vector3(0f, lowY + 0.10f,  0.92f), new Vector3(0.37f, 0.045f, 0.046f), "RedPaint");
            Prop(props.transform, "RepairRig",      new Vector3(0f, lowY + 0.09f,  1.22f), new Vector3(0.30f, 0.18f, 0.24f), "Grease");

            // ── 벽 표지판 ──
            GameObject signs = Child(root.transform, SignsName);
            signs.transform.localPosition = new Vector3(ReferenceRoomSpec.WallRightX, ReferenceRoomSpec.WallSignCenterY, 0f);
            signs.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);

            // 「STORAGE」 스텐실 + 짧은 밑줄. 글자는 텍스처가 나른다(명세 §14).
            Slab(signs.transform, "Stencil_Storage", new Vector3(-0.55f, 0.10f, 0.008f), new Vector3(0.62f, 0.16f, 0.006f), "Sign");
            Slab(signs.transform, "Stencil_Underline", new Vector3(-0.55f, -0.02f, 0.008f), new Vector3(0.62f, 0.022f, 0.006f), "Sign");
            // 안전 표지판. 명세 §7 「크림색 바탕에 검은 그림과 글씨」.
            Slab(signs.transform, "Sign_SafetyFirst", new Vector3(0.52f, 0.0f, 0.010f), new Vector3(0.52f, 0.62f, 0.008f), "Sign");

            _report.AppendLine($"  {ShelfName} — {len} × {dep} · 상판 {top} · 하단 {low} · 지지대 {legs}×2 " +
                               $"· {PropsName} 상판 7 / 하단 6 · {SignsName} 3");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  §12 콜라이더 — **이게 없으면 플레이어가 바닥을 뚫고 떨어진다**
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 🔴 **이 함수가 없어서 게임이 시작하자마자 플레이어가 떨어졌다.**
        ///
        /// 구 셸(`GrayboxWorld/Car/Floor` 등)을 비활성화하면서 그 콜라이더도 같이
        /// 사라졌는데, 새 방에는 콜라이더를 붙이지 않았다. 진행 문서에 「콜라이더는
        /// 아직」이라고 **적어 두기까지 했으면서** 그것이 「미완성 항목」이 아니라
        /// **「게임이 즉시 망가지는 결함」**이라는 것을 못 봤다.
        ///
        /// 증상이 「화면이 검다」로 나타나는 것이 이 결함의 고약한 점이다 —
        /// 플레이어가 방 밖 허공으로 떨어지면 보이는 것은 조명 없는 빈 공간이고,
        /// 그건 「조명이 잘못됐다」와 화면에서 구분되지 않는다. 나는 이 세션에서
        /// 조명·재질·셰이더를 다섯 번 고쳤고, 그중 어느 것도 원인이 아니었다.
        /// **사용자가 직접 플레이해서 찾았다.**
        ///
        /// 명세 §12 를 그대로 따른다 —
        ///   · 벽과 선반에는 **단순한 Box Collider**
        ///   · 원형 관찰창에는 복잡한 메시 콜라이더를 쓰지 않는다
        ///   · 작은 볼트와 장식에는 개별 콜라이더를 만들지 않는다
        ///   · 중앙 이동 공간에는 보이지 않는 충돌체가 남아 있으면 안 된다
        /// </summary>
        private static void AddColliders(GameObject root)
        {
            // 이름으로 고른다. 「렌더러가 있으면 전부」로 하면 볼트 수백 개에
            // 콜라이더가 붙고, 그건 명세가 명시적으로 금지한다.
            var solid = new HashSet<string>
            {
                // 셸 — 바닥·벽·천장. 바닥이 이 목록의 존재 이유다.
                "Wall_Left", "Wall_Right", "Wall_Rear", "Wall_Front", "Ceiling",
                "Border_Left", "Border_Right", "Border_Front", "Border_Rear",
                // 장치 — 통과해 들어갈 수 없어야 한다.
                // ⚠ 구 `BackPlate` 는 사라졌다(모듈 후면 장착판 삭제). 캐비닛의
                // 연속 전면 셸이 그 역할을 대신하므로 **이름을 갈아 끼운다** —
                // 안 갈면 장치 앞면에 콜라이더가 하나도 없어 플레이어가 통과한다.
                // ⚠ `CabinetShell` 은 사라졌다(챔버를 막아서). 장치 앞면을 막는 것은
                // 이제 **챔버 도어 9장**이다 — 없으면 플레이어가 캐비닛 안으로 걸어
                // 들어간다. 상자 콜라이더는 메시 경계(도어 사각형 전체)로 잡히므로
                // 보어가 뚫려 있어도 통과하지 못한다.
                "Door", "CabinetBack",
                "Frame_Top", "Frame_Bottom", "Frame_Left", "Frame_Right",
                // 레버 컬럼과 전력 표시기
                "Housing", "Back",
                // 선반 — 명세 §7 「벽과 선반에는 단순한 Box Collider」
                "TopPlate", "LowerShelf",
                // 가위문 — 문틀과 격자
                "Jamb_Front", "Jamb_Rear", "Header", "Lattice",
            };

            int added = 0, floors = 0;
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!solid.Contains(mf.gameObject.name)) continue;
                if (mf.gameObject.GetComponent<Collider>() != null) continue;
                var bc = mf.gameObject.AddComponent<BoxCollider>();
                // `AddComponent<BoxCollider>` 는 메시 경계로 자동 맞춰지지만,
                // 명시적으로 다시 써서 멱등성을 지킨다.
                if (mf.sharedMesh != null)
                {
                    bc.center = mf.sharedMesh.bounds.center;
                    bc.size = mf.sharedMesh.bounds.size;
                }
                added++;
            }

            // ── 바닥은 **따로, 확실하게** ──
            // 타공판(`FloorCenterGrate/Plate`)은 0.018m 낮게 앉아 있고 테두리는 별개다.
            // 둘 사이의 이음매로 빠질 여지를 없애기 위해 방 전체를 덮는 바닥 콜라이더를
            // 하나 더 둔다. 보이지 않는 판이지만 **중앙 이동 공간을 막지 않는다** —
            // 바닥면 아래에 있기 때문이다(명세 §12 마지막 줄).
            var floor = new GameObject("FloorCollider");
            floor.transform.SetParent(root.transform, false);
            floor.transform.localPosition = new Vector3(0f, -ReferenceRoomSpec.ShellThickness * 0.5f, 0f);
            var fc = floor.AddComponent<BoxCollider>();
            fc.size = new Vector3(ReferenceRoomSpec.InteriorWidth,
                                  ReferenceRoomSpec.ShellThickness,
                                  ReferenceRoomSpec.InteriorDepth);
            floors++;

            _report.AppendLine($"  콜라이더 — 구조물 {added}개 + 바닥 전면 {floors}개 " +
                               "(볼트·장식·관찰창에는 붙이지 않는다 — 명세 §12)");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  §2 카메라
        // ══════════════════════════════════════════════════════════════════════

        private static void PlaceCamera()
        {
            int touched = 0;
            foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (cam.orthographic) continue;
                Undo.RecordObject(cam, "Reference room camera");
                // **수직값을 넣는다.** 명세는 수평 72~78 이고 Unity 필드는 수직이다.
                cam.fieldOfView = ReferenceRoomSpec.VerticalFov;
                EditorUtility.SetDirty(cam);
                touched++;
            }
            // ── 기준 구도 ──
            // 명세 §2 「앞쪽 중앙에서 후면 벽을 바라본다 · 카메라는 수평을 유지」.
            //
            // ⚠ **눈높이를 여기서 더하지 않는다.** 이 저장소는 눈높이 소유자를 둘로
            // 갈랐다가 1.62 가 두 번 더해져 시점이 천장 위로 간 적이 있다
            // (`TOPDOWN_PROGRESS` 「카메라가 눈높이보다 1.6m 높다」). 눈높이는 계층이
            // 소유하므로 여기서는 **바닥 위치와 방향만** 준다.
            GameObject player = GameObject.Find("Player");
            if (player != null)
            {
                Undo.RecordObject(player.transform, "Reference room camera");
                player.transform.position = new Vector3(0f, 0f, ReferenceRoomSpec.CameraZ);
                player.transform.rotation = Quaternion.Euler(0f, 0f, 0f);   // +Z = 장치 벽
                EditorUtility.SetDirty(player);
                _report.AppendLine($"  Player — (0, 0, {ReferenceRoomSpec.CameraZ:F2}) 정면 +Z " +
                                   $"· 후면 벽까지 {ReferenceRoomSpec.CameraToRearWall:F2}m " +
                                   $"(눈높이는 계층이 소유 — 여기서 더하지 않는다)");
            }
            else _report.AppendLine("  ⚠ `Player` 를 찾지 못했다 — 기준 구도를 세우지 못했다");

            _report.AppendLine($"  카메라 {touched}대 — 수평 {ReferenceRoomSpec.HorizontalFovDegrees}° " +
                               $"→ 수직 {ReferenceRoomSpec.VerticalFov:F2}° (16:9)");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  헬퍼
        // ══════════════════════════════════════════════════════════════════════

        private static void SetFloat(SerializedObject so, string field, float value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p != null) p.floatValue = value;
        }

        private static void SetVector(SerializedObject so, string field, Vector3 value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p != null) p.vector3Value = value;
        }

        private static GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Anchor(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
        }

        /// <summary>축 정렬 상자 하나. 셸·프레임·판 대부분이 이것이다.</summary>
        private static GameObject Slab(Transform parent, string name, Vector3 localPos, Vector3 size, string material)
        {
            var b = new ProcMeshBuilder();
            b.AddBox(Vector3.zero, size, 0f, ReferenceRoomSpec.SurfaceUvPerMeter);
            Mesh mesh = SaveMesh(b.ToMesh(name), $"Slab_{name}_{size.x:F3}x{size.y:F3}x{size.z:F3}");

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            Render(go, mesh, material);
            return go;
        }

        /// <summary>
        /// 프롭 상자. 명세 §12 「작은 볼트와 장식에는 개별 콜라이더를 만들지 않는다」에
        /// 따라 콜라이더를 붙이지 않는다.
        /// </summary>
        private static GameObject Prop(Transform parent, string name, Vector3 localPos, Vector3 size, string material)
            => Slab(parent, name, localPos, size, material);

        private static void AddBolt(Transform parent, Mesh mesh, Vector3 localPos, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            Render(go, mesh, "BareSteel");
        }

        /// <summary>
        /// 작은 조각은 그림자를 드리우지 않는 크기(m). 이보다 작은 경계 상자를 가진
        /// 렌더러는 <c>ShadowCastingMode.Off</c> 로 만든다.
        /// </summary>
        private const float ShadowCasterMinSize = 0.26f;

        private static Renderer Render(GameObject go, Mesh mesh, string material)
        {
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = P(material);

            // ── 🔴 볼트에 그림자를 시키지 않는다 ────────────────────────────
            //
            // 직전 판본은 **모든** 렌더러에 그림자를 켰다. 10층 PlayMode 가 그것을
            // 잡았다 — 프레임타임 p95 **89.03 ms = 11 FPS**, PRD §13.1 하한(60 FPS)의
            // 5분의 1이다.
            //
            // 원인이 산술로 설명된다. 켜진 렌더러 292개 중 **279개가 그림자를 드리웠고**,
            // 주광이 **점광**이라 그림자 패스가 큐브맵 **6면**이다. 즉 볼트 하나가
            // 드로우콜 7개(본 패스 1 + 그림자 6)를 만든다. 볼트·리벳·톱니·이음매가
            // 그 279개의 대부분이다.
            //
            // **그리고 그것들은 그림자를 만들 이유가 없다.** 지름 20mm 짜리 볼트가
            // 드리우는 그림자는 화면에서 1~2 화소이고, 그 화소는 볼트 자신의 명암과
            // 구분되지 않는다. 비용은 전부 내고 얻는 것은 없다.
            //
            // 벽·바닥·장치 프레임·선반처럼 **공간을 만드는 것**만 남긴다 —
            // 명세 §11 「천장 보강빔이 굵은 그림자를 만든다」가 요구하는 것이 그쪽이다.
            Bounds b = mesh != null ? mesh.bounds : new Bounds();
            bool bigEnough = b.size.magnitude >= ShadowCasterMinSize;
            mr.shadowCastingMode = bigEnough
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;

            return mr;
        }

        // ── 에셋 굽기 ────────────────────────────────────────────────────────
        //
        // 메시를 에셋으로 굽지 않으면 씬 YAML 안에 정점 배열이 통째로 들어간다.
        // 이 저장소는 씬 파일이 조용히 손상된 이력이 있고, 그 위험은 파일이 클수록 커진다.

        private const string MeshDir = "Assets/Prototype_Elevator/Art/Meshes/Room";

        /// <summary>이번 실행에서 이미 구운 메시. 같은 키를 두 번 구우면 에셋이 갈린다.</summary>
        private static readonly Dictionary<string, Mesh> BakedMeshes = new Dictionary<string, Mesh>();

        /// <summary>
        /// 메시를 에셋으로 굽고 **에셋 쪽 객체를 돌려준다.**
        ///
        /// ⚠ 돌려주는 것이 중요하다. 첫 판본은 `void` 였고 호출부가 <b>임시 메시</b>를
        /// 그대로 `MeshFilter` 에 물렸다. 그러면 씬은 에셋이 아니라 메모리 객체를
        /// 가리키고, 저장할 때 정점 배열이 **씬 YAML 안으로 통째로 들어가거나**
        /// 참조가 끊긴다. 두 결과 다 「조립 직후에는 멀쩡해 보이고 다음에 열면 깨져
        /// 있다」로 나타나는데, 그건 이 저장소가 직렬화 에셋에서 반복해서 당한
        /// 실패의 정확한 형태다.
        /// </summary>
        private static Mesh SaveMesh(Mesh mesh, string key)
        {
            if (!Directory.Exists(MeshDir)) Directory.CreateDirectory(MeshDir);
            string safe = key.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
            string path = $"{MeshDir}/{safe}.asset";

            if (BakedMeshes.TryGetValue(path, out Mesh cached) && cached != null)
            {
                // 같은 형상을 또 만들었다. 임시 객체를 흘리지 않고 회수한다 —
                // `Mesh` 는 네이티브 객체라 GC 가 치우지 않는다.
                if (mesh != cached) Object.DestroyImmediate(mesh);
                return cached;
            }

            // 이름을 **두 경로 모두에서** 파일명과 맞춘다. `CopySerialized` 는 이름까지
            // 복사하므로, 생성 경로에서만 맞추면 재조립 때마다 경고가 돌아온다.
            mesh.name = safe;

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                BakedMeshes[path] = existing;
                return existing;
            }

            AssetDatabase.CreateAsset(mesh, path);
            BakedMeshes[path] = mesh;
            return mesh;
        }

        private static void SaveMaterial(Material m, string key)
        {
            string path = $"{MaterialDir}/RM_{key}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) { EditorUtility.CopySerialized(m, existing); return; }
            AssetDatabase.CreateAsset(m, path);
        }
    }
}
