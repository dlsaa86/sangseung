using System.Collections.Generic;
using System.IO;
using System.Text;
using Ascend.Prototype.Art;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ascend.Prototype.EditorTools
{
    /// <summary>
    /// **운행 계기탑의 치수.** 값을 한곳에 모아 두는 이유는 이 저장소가 좌표를 코드
    /// 여기저기에 흩뿌렸다가 「계기판이 자기 판독면 안에 있고 판독면이 포즈 안에 없다」를
    /// 세 라운드 뒤에야 알아낸 전례가 있기 때문이다 (`UP-FIX-88`).
    ///
    /// 모든 값은 **절대값**이다. 델타가 아니다 —
    /// `Ascend/Reproportion Elevator Car` 가 멱등이 아니어서 두 번 돌리자 계기판 전체가
    /// 1/0.66 씩 두 번 밀린 사고가 이 저장소에 기록되어 있다.
    /// </summary>
    public static class AscentColumnSpec
    {
        /// <summary>실행 레버 기둥과 **같은 중심**. 「레버 위」가 좌표로 성립해야 한다.</summary>
        public static float CenterX => ReferenceRoomSpec.LeverColumnCenterX;   // +0.758

        /// <summary>
        /// 바닥 y. 과부하 경고등 상단(2.190)에 얹는다 — `MACHINE_SPEC` §4.1 이
        /// 「경고등은 레버 가까이」라고 못 박았으므로 등을 치우지 않고 그 **위**로 간다.
        /// 스택이 아래에서부터 레버 → 경고등 → 계기탑이 되어 하나의 조작 시스템으로 읽힌다.
        /// </summary>
        public const float BottomY = 2.194f;

        /// <summary>
        /// 천장 아래 13mm 를 남긴다. `CabinShell/ELV_Ceiling` 의 아랫면이 2.729 다
        /// (`ReferenceRoomSpec.InteriorHeight` 2.9 가 아니다 — 실측이 이긴다).
        ///
        /// **위아래를 끝까지 쓴다.** 이 0.522 m 가 큰 숫자의 크기를 정하고, 큰 숫자의
        /// 크기가 「A·D 먼 시점에서 읽히는가」를 정한다 — 밝기로 키우는 길이 막혀 있으므로
        /// (`UP-FIX-90`) 남은 축은 형상뿐이다.
        /// </summary>
        public const float TopY = 2.716f;

        public static float Height => TopY - BottomY;           // 0.522
        public static float CenterY => (TopY + BottomY) * 0.5f; // 2.455

        /// <summary>가로. 좌우로 남는 벽이 있어 레버 기둥(0.36)보다 넓게 잡을 수 있다.</summary>
        public const float Width = 0.86f;

        /// <summary>벽에서 앞으로 내미는 깊이. 계기판(0.152 proud)과 같은 급이다.</summary>
        public const float Depth = 0.16f;

        public const float Bezel = 0.022f;

        /// <summary>탱크 안지름 높이(m). 이 높이가 <see cref="MaxRatio"/> 에 대응한다.</summary>
        public const float TankHeight = 0.430f;
        public const float TankBoreWidth = 0.120f;
        public const float MaxRatio = 3f;

        /// <summary>탱크 중심의 로컬 X. 케이스 왼쪽 끝에 붙인다.</summary>
        public const float TankCenterX = -0.330f;

        /// <summary>큰 숫자의 글자 상자 높이(m). **1순위 판독 하한을 여기가 정한다.**</summary>
        public const float NumeralBox = 0.178f;

        /// <summary>캡션(2순위) 글자 상자 높이(m).</summary>
        public const float CaptionBox = 0.032f;

        /// <summary>전력 수치 줄(1순위 보조) 글자 상자 높이(m).</summary>
        public const float PowerLineBox = 0.070f;

        /// <summary>예비 슬롯(2순위) 글자 상자 높이(m).</summary>
        public const float ReserveLineBox = 0.046f;

        /// <summary>임계점 눈금. `PowerThresholds.Default` 와 같은 값이어야 한다.</summary>
        public static readonly (float ratio, bool major, bool red)[] Ticks =
        {
            (0.50f, false, false),
            (1.00f, true,  true ),   // 요구선 — 2단 잠금 해제 지점
            (1.30f, false, false),
            (1.70f, false, false),
            (2.20f, false, true ),   // 과수확 구간 진입
            (3.00f, true,  false),
        };
    }

    /// <summary>
    /// 실행 레버 **바로 위**의 운행 계기탑을 조립한다. `MASTER_PRD` §7 / `MACHINE_SPEC`
    /// §4.4 가 확정하고 여섯 라운드 동안 미구현이던 요구다 —
    /// 「레버 주변에 현재 전력 · 유지 배수 · 추가 스핀 손실 조건을 큰 단위로 표시」.
    ///
    /// ## 이 빌더의 계약
    ///
    /// **「없으면 만든다」가 아니라 「항상 이 상태로 만든다」다.** 매번 자식을 전부 지우고
    /// 다시 세우고, 컴포넌트의 직렬화 필드를 **전부** 절대값으로 다시 쓴다.
    /// 그래서 두 번 돌려도 결과가 같다. 이 저장소는 「생성할 때만 설정하는 구조라
    /// 다시 고칠 기회가 없었다」는 사고를 겪었다.
    ///
    /// ## 🔴 왜 조명이 아니라 형상인가
    ///
    /// 5차 독립 평가가 조명 채널(`PowerAmbienceProfile`)을 **실측으로 기각**했다 —
    /// 값을 못 싣고, 회색조에서 사라지고, 0%(Stable)와 240%(Collapse)가 같은 화면을 냈다.
    /// 그래서 여기서는 **채움 높이**가 값을 나른다. 높이는 회색조에서 살아남는다.
    ///
    /// ## 🔴 왜 순백 글자를 쓰지 않는가
    ///
    /// `UP-FIX-90`(방 안 최고 백색이 계기 텍스트)·`UP-FIX-94`(§12 대역 3항목 위반)의
    /// 원인이 (1,1,1) 글자다. 이 탑의 글자는 **따뜻한 뼈색**(r &gt; g &gt; b)이고
    /// 가장 밝은 것도 휘도 0.61 이다. 크기는 **글자 상자 0.150 m** 가 만든다.
    /// </summary>
    public static class AscentColumnBuilder
    {
        public const string RootName = "AscentColumn";

        private const string MaterialDir = "Assets/Prototype_Elevator/Art/Materials/Room";
        private const string MeshDir = "Assets/Prototype_Elevator/Art/Meshes/Room";
        private const string FontPath = "Assets/Prototype_Elevator/Fonts/NanumGothic SDF.asset";
        private const string FontMatPath =
            "Assets/Prototype_Elevator/Materials/NanumGothic SDF Material SingleSided.mat";

        // ── 잉크 (전부 따뜻하다: r > g > b) ───────────────────────────────────
        // ⚠ **실행 표찰(0.836)보다 낮게 유지한다.** `VISUAL_SPEC` §5 의 1순위는
        // 「현재 사용 가능한 핵심 레버」이고, 차분 렌더 실측에서 탑 숫자 잉크 평균이
        // 실행 표찰을 넘긴 판본이 있었다(A 0.601 vs 0.567). 크기는 탑이 이기고
        // **밝기는 레버가 이긴다** — 그래야 위계가 뒤집히지 않는다.
        private static readonly Color InkPrimary  = new Color(0.63f, 0.52f, 0.34f);   // 휘도 .531
        private static readonly Color InkSupport  = new Color(0.62f, 0.52f, 0.35f);   // 휘도 .532
        private static readonly Color InkCaption  = new Color(0.44f, 0.37f, 0.25f);   // 휘도 .372
        private static readonly Color InkReserve  = new Color(0.40f, 0.34f, 0.23f);   // 휘도 .343

        private static StringBuilder _report;

        [MenuItem("Ascend/Room/Build Ascent Column")]
        public static void BuildFromMenu()
        {
            if (EditorApplication.isPlaying)
            { Debug.LogError("[상승] Play 모드에서는 씬을 고치지 않는다."); return; }

            GameObject root = GameObject.Find(AscendReferenceRoom.RootName);
            if (root == null)
            { Debug.LogError($"[상승] `{AscendReferenceRoom.RootName}` 이 없다."); return; }

            _report = new StringBuilder("[상승] 운행 계기탑 조립\n");
            Build(root);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log(_report.ToString());
        }

        /// <summary>
        /// `AscendReferenceRoom.Build` 의 마지막 단계에서도 불린다.
        ///
        /// ⚠ **반드시 그래야 한다.** `ResetRoot` 가 `ReferenceRoom` 자식을 전부 지우므로,
        /// 여기서 다시 세우지 않으면 방을 다시 조립하는 순간 계기탑이 조용히 사라진다 —
        /// `ShaftBackdrop` 이 정확히 그렇게 세 라운드를 통과했다
        /// (`AscendReferenceRoomRewire.EnforceShaftOpening` 주석 참조).
        /// </summary>
        public static void Build(GameObject room)
        {
            if (_report == null) _report = new StringBuilder();

            Transform parent = room.transform;
            Transform existing = parent.Find(RootName);
            GameObject go;
            if (existing != null) go = existing.gameObject;
            else { go = new GameObject(RootName); go.transform.SetParent(parent, false); }

            // 자식을 **전부** 지우고 다시 세운다. 부분 갱신은 「고칠 기회가 없는」 상태를 만든다.
            for (int i = go.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(go.transform.GetChild(i).gameObject);

            go.transform.localPosition = new Vector3(AscentColumnSpec.CenterX,
                                                     AscentColumnSpec.CenterY,
                                                     ReferenceRoomSpec.WallRearZ);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            float w = AscentColumnSpec.Width;
            float h = AscentColumnSpec.Height;
            float d = AscentColumnSpec.Depth;
            float bz = AscentColumnSpec.Bezel;
            Transform t = go.transform;

            // ── ① 케이스 ─────────────────────────────────────────────────────
            Slab(t, "Back", new Vector3(0f, 0f, -0.025f), new Vector3(w, h, 0.050f), "Steel");
            Slab(t, "Frame_Top",    new Vector3(0f,  h * 0.5f - bz * 0.5f, -d * 0.5f), new Vector3(w, bz, d), "Steel");
            Slab(t, "Frame_Bottom", new Vector3(0f, -h * 0.5f + bz * 0.5f, -d * 0.5f), new Vector3(w, bz, d), "Steel");
            Slab(t, "Frame_Left",   new Vector3(-w * 0.5f + bz * 0.5f, 0f, -d * 0.5f), new Vector3(bz, h, d), "Steel");
            Slab(t, "Frame_Right",  new Vector3( w * 0.5f - bz * 0.5f, 0f, -d * 0.5f), new Vector3(bz, h, d), "Steel");

            // 차양. 계기판(`BuildPowerMeter`)과 같은 부품이다 — 위에서 오는 빛을 끊어
            // 판이 「평평한 밝은 사각형」이 되는 것을 막는다.
            Slab(t, "Hood", new Vector3(0f, h * 0.5f - bz * 0.5f, -d - 0.030f),
                 new Vector3(w, 0.020f, 0.062f), "BareSteel");

            // 어두운 판독면. `RM_Readout` 은 스페큘러·환경반사가 꺼져 있어 넓은 판이
            // 반사 로브로 희게 덮이지 않는다(그 함정을 이미 한 번 밟았다).
            Slab(t, "Face", new Vector3(0f, 0f, -d + 0.008f),
                 new Vector3(w - bz * 2f, h - bz * 2f, 0.014f), "Readout");

            Mesh bolt = BoltMesh();
            float bx = w * 0.5f - bz * 0.5f, by = h * 0.5f - bz * 0.5f;
            for (int i = 0; i < 4; i++)
                Bolt(t, $"CaseBolt_{i}", new Vector3((i & 1) == 0 ? -bx : bx,
                                                     (i & 2) == 0 ? -by : by, -d - 0.006f), bolt);

            // ── ② 전력 탱크 — 「점점 차오르는 것」 ───────────────────────────
            float tx = AscentColumnSpec.TankCenterX;
            float th = AscentColumnSpec.TankHeight;
            float tw = AscentColumnSpec.TankBoreWidth;
            float faceZ = -d + 0.008f;

            var tank = new GameObject("PowerTank");
            tank.transform.SetParent(t, false);

            Slab(tank.transform, "Bore", new Vector3(tx, 0f, faceZ - 0.010f),
                 new Vector3(tw + 0.010f, th + 0.010f, 0.012f), "ChamberDark");
            Slab(tank.transform, "Wall_Left",  new Vector3(tx - tw * 0.5f - 0.011f, 0f, faceZ - 0.028f), new Vector3(0.016f, th + 0.036f, 0.052f), "BareSteel");
            Slab(tank.transform, "Wall_Right", new Vector3(tx + tw * 0.5f + 0.011f, 0f, faceZ - 0.028f), new Vector3(0.016f, th + 0.036f, 0.052f), "BareSteel");
            Slab(tank.transform, "Cap_Top",    new Vector3(tx,  th * 0.5f + 0.014f, faceZ - 0.028f), new Vector3(tw + 0.048f, 0.018f, 0.052f), "BareSteel");
            Slab(tank.transform, "Cap_Bottom", new Vector3(tx, -th * 0.5f - 0.014f, faceZ - 0.028f), new Vector3(tw + 0.048f, 0.018f, 0.052f), "BareSteel");

            // 채움 기둥. **바닥에 피벗이 있는 단위 상자**라 `localScale.y` 가 곧 높이(m)다.
            var fillPivot = new GameObject("TankFillPivot");
            fillPivot.transform.SetParent(tank.transform, false);
            fillPivot.transform.localPosition = new Vector3(tx, -th * 0.5f, faceZ - 0.022f);
            // ⚠ **y 를 0 으로 저장한다.** 1 로 두면 저장된 씬에서 채움이 1 m 짜리 기둥이
            // 되어 천장을 뚫고 나간다(실측 Y 3.226 — 천장은 2.729). 씬의 기본 상태는
            // 「전력 0」이므로 빈 탱크가 정직한 값이고, 차오르는 증거는 캡처 스윕이 낸다.
            fillPivot.transform.localScale = new Vector3(1f, 0f, 1f);
            GameObject fill = Unit(fillPivot.transform, "TankFill",
                                  new Vector3(tw - 0.010f, 1f, 0.026f), "GaugeFill");

            // 눈금. **숫자 라벨을 붙이지 않는다** — 글자를 늘리면 `UP-FIX-90` 이 악화된다.
            // 대신 길이와 붉은색으로 구분한다(회색조에서는 길이만 남는다).
            Renderer requiredBand = null;
            var ticks = new GameObject("Ticks");
            ticks.transform.SetParent(tank.transform, false);
            foreach (var (ratio, major, red) in AscentColumnSpec.Ticks)
            {
                float y = -th * 0.5f + th * Mathf.Clamp01(ratio / AscentColumnSpec.MaxRatio);
                bool full = red || major;
                float tickW = full ? tw + 0.062f : 0.048f;
                float cx = full ? tx + 0.010f : tx + tw * 0.5f + 0.048f;
                GameObject g = Slab(ticks.transform, $"Tick_{Mathf.RoundToInt(ratio * 100f)}",
                                    new Vector3(cx, y, faceZ - 0.048f),
                                    new Vector3(tickW, red ? 0.014f : (major ? 0.011f : 0.007f), 0.030f),
                                    red ? "RedPaint" : "BareSteel");
                if (Mathf.Approximately(ratio, 1f)) requiredBand = g.GetComponent<Renderer>();
            }

            // 잠금 핀. `MACHINE_SPEC` §4.4 「100% 달성 시 내부 잠금쇠가 풀린다」의 형상.
            float lockY = -th * 0.5f + th * (1f / AscentColumnSpec.MaxRatio);
            Slab(tank.transform, "LockPinHousing", new Vector3(tx + tw * 0.5f + 0.088f, lockY, faceZ - 0.052f),
                 new Vector3(0.038f, 0.048f, 0.040f), "Collar");
            GameObject pin = Slab(tank.transform, "LockPin", Vector3.zero,
                                  new Vector3(0.104f, 0.016f, 0.024f), "RedPaint");
            Vector3 lockedPos = new Vector3(tx + 0.066f, lockY, faceZ - 0.056f);
            Vector3 openPos = new Vector3(tx + 0.132f, lockY, faceZ - 0.056f);
            pin.transform.localPosition = lockedPos;

            // ── ③ 1순위 두 드럼 ─────────────────────────────────────────────
            float drumTop = h * 0.5f - bz - 0.006f;                 // +0.233
            const float drumH = 0.220f;
            float drumCy = drumTop - drumH * 0.5f;                  // +0.123
            float numY = drumTop - AscentColumnSpec.NumeralBox * 0.5f;              // +0.144
            float capY = drumTop - drumH + AscentColumnSpec.CaptionBox * 0.5f + 0.002f;  // +0.031

            // 🔴 **기본 문구를 지어내지 않는다.** 고정 캡처는 에디트 모드에서 찍히므로
            //    여기 적힌 값이 곧 평가받는 화면이다. 1층의 실제 계획 스핀 수를 읽는다 —
            //    「3」은 아무 데서도 오지 않은 숫자였고, 실제 1층은 5회다.
            SeedState(out int seedSpins, out float seedRequired, out int seedFloors);
            TextMeshPro spinNumeral = Drum(t, "SpinDrum", -0.060f, drumCy, 0.264f, drumH, faceZ,
                                          numY, capY, seedSpins.ToString(), "스핀 남음");
            TextMeshPro ascentNumeral = Drum(t, "AscentDrum", 0.252f, drumCy, 0.296f, drumH, faceZ,
                                             numY, capY, seedFloors <= 0 ? "0" : "+" + seedFloors, "층 상승");

            // ── ④ 데이터 판 — 슬러그 · 전력 수치 · 예비 슬롯 ────────────────
            var data = new GameObject("DataPlate");
            data.transform.SetParent(t, false);
            Slab(data.transform, "Plate", new Vector3(0.104f, -0.101f, faceZ - 0.008f),
                 new Vector3(0.596f, 0.208f, 0.014f), "GaugePlate");

            var pips = new List<Renderer>();
            const int pipCount = 6;
            float pipPitch = 0.264f / pipCount;
            for (int i = 0; i < pipCount; i++)
            {
                float px = -0.192f + pipPitch * (i + 0.5f);
                GameObject g = Slab(data.transform, $"SpinPip_{i}", new Vector3(px, -0.013f, faceZ - 0.024f),
                                    new Vector3(pipPitch - 0.010f, 0.026f, 0.020f), "GaugePip");
                // 층 시작 상태 = 계획 스핀 전부가 남아 있다. 없는 슬러그는 꺼 둔다 —
                // 꺼진 자리를 「쓴 것」으로 그리면 5회짜리 층이 6회짜리 소진 상태로 읽힌다.
                g.SetActive(i < seedSpins);
                var mr = g.GetComponent<Renderer>();
                var mpb = new MaterialPropertyBlock();
                mr.GetPropertyBlock(mpb);
                Color pipColor = new Color(0.74f, 0.26f, 0.17f);
                mpb.SetColor("_BaseColor", pipColor);
                mpb.SetColor("_EmissionColor", pipColor * 0.30f);
                mr.SetPropertyBlock(mpb);
                pips.Add(mr);
            }

            TextMeshPro powerLine = Label(data.transform, "PowerLine", new Vector3(-0.192f, -0.087f, faceZ - 0.020f),
                                          AscentColumnSpec.PowerLineBox, "전력 0   0%", InkSupport,
                                          TextAlignmentOptions.MidlineLeft, 26f);
            TextMeshPro reserveLine = Label(data.transform, "ReserveLine", new Vector3(-0.192f, -0.171f, faceZ - 0.020f),
                                            AscentColumnSpec.ReserveLineBox, "배수 0.00배   손실 —", InkReserve,
                                            TextAlignmentOptions.MidlineLeft, 34f);

            // ── ⑤ 레버 기둥과 물리적으로 잇는다 ─────────────────────────────
            // 「하나의 조작 시스템처럼 보여야 한다」(`MACHINE_SPEC` §4.1).
            // 이 두 스트랩이 없으면 계기탑이 벽에 붙은 별개의 판으로 읽힌다.
            float strapTop = -h * 0.5f;
            float strapBottom = ReferenceRoomSpec.LeverColumnTopY - AscentColumnSpec.CenterY;  // 음수
            float strapH = strapTop - strapBottom;
            for (int i = 0; i < 2; i++)
                Slab(t, $"MountStrap_{i}", new Vector3(i == 0 ? -0.150f : 0.150f,
                                                       (strapTop + strapBottom) * 0.5f, -0.045f),
                     new Vector3(0.038f, strapH, 0.050f), "BareSteel");

            // ── ⑥ 컴포넌트 — 필드를 **전부** 절대값으로 다시 쓴다 ───────────
            var view = go.GetComponent<View.AscentColumnView>();
            if (view == null) view = go.AddComponent<View.AscentColumnView>();

            var so = new SerializedObject(view);
            so.FindProperty("_run").objectReferenceValue =
                Object.FindAnyObjectByType<Run.RunSessionBehaviour>(FindObjectsInactive.Include);
            so.FindProperty("_presenter").objectReferenceValue =
                Object.FindAnyObjectByType<View.SpinPresenter>(FindObjectsInactive.Include);
            so.FindProperty("_risk").objectReferenceValue =
                Object.FindAnyObjectByType<Risk.RiskStateView>(FindObjectsInactive.Include);
            so.FindProperty("_tankFillPivot").objectReferenceValue = fillPivot.transform;
            so.FindProperty("_tankFill").objectReferenceValue = fill.GetComponent<Renderer>();
            so.FindProperty("_tankHeight").floatValue = th;
            so.FindProperty("_maxRatio").floatValue = AscentColumnSpec.MaxRatio;
            so.FindProperty("_requiredBand").objectReferenceValue = requiredBand;
            so.FindProperty("_lockLug").objectReferenceValue = pin.transform;
            so.FindProperty("_lockLugLocked").vector3Value = lockedPos;
            so.FindProperty("_lockLugOpen").vector3Value = openPos;
            so.FindProperty("_spinNumeral").objectReferenceValue = spinNumeral;
            so.FindProperty("_ascentNumeral").objectReferenceValue = ascentNumeral;
            so.FindProperty("_powerLine").objectReferenceValue = powerLine;
            so.FindProperty("_reserveLine").objectReferenceValue = reserveLine;
            so.FindProperty("_overharvestLabel").objectReferenceValue =
                FindTmp("GrayboxWorld/Car/OverharvestLever/OverharvestLabel");

            SerializedProperty arr = so.FindProperty("_spinPips");
            arr.arraySize = pips.Count;
            for (int i = 0; i < pips.Count; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = pips[i];

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
            EditorUtility.SetDirty(go);

            _report.AppendLine($"  {RootName} — {w:F3} × {h:F3} × {d:F3} m @ " +
                               $"({AscentColumnSpec.CenterX:F3}, {AscentColumnSpec.CenterY:F3}, {ReferenceRoomSpec.WallRearZ:F3})");
            _report.AppendLine($"    월드 X {AscentColumnSpec.CenterX - w * 0.5f:F3}…{AscentColumnSpec.CenterX + w * 0.5f:F3} · " +
                               $"Y {AscentColumnSpec.BottomY:F3}…{AscentColumnSpec.TopY:F3} · " +
                               $"전면 z {ReferenceRoomSpec.WallRearZ - d:F3} (벽면 2.258 에서 {(2.258f - (ReferenceRoomSpec.WallRearZ - d)) * 1000f:F0}mm 돌출)");
            _report.AppendLine($"    탱크 안지름 {tw * 1000f:F0} × {th * 1000f:F0} mm · 눈금 {AscentColumnSpec.Ticks.Length}개 · " +
                               $"큰 숫자 글자상자 {AscentColumnSpec.NumeralBox * 1000f:F0} mm · 슬러그 {pipCount}개");
            _report.AppendLine($"    레버 기둥 상단 {ReferenceRoomSpec.LeverColumnTopY:F3} → 스트랩 {strapH * 1000f:F0} mm 로 연결");
        }

        /// <summary>
        /// 저장된 씬이 보여야 할 **층 시작 상태**를 실제 규칙에서 읽는다.
        ///
        /// 세션이 없으면 만든다(`RunSession` 은 순수 C# 이라 에디트 모드에서 돈다).
        /// 만들지 못하면 0 으로 두고 보고서에 적는다 — **지어낸 숫자를 화면에 올리지 않는다.**
        /// 층수는 `RunSession.PreviewFloorsGained()` 만 쓴다(전력 0 이므로 0 이어야 한다).
        /// </summary>
        private static void SeedState(out int spins, out float required, out int floors)
        {
            spins = 0; required = 0f; floors = 0;
            var rb = Object.FindAnyObjectByType<Run.RunSessionBehaviour>(FindObjectsInactive.Include);
            if (rb == null) { _report.AppendLine("    ⚠ RunSessionBehaviour 가 없다 — 기본 상태를 0 으로 둔다"); return; }
            if (rb.Session == null) rb.ResetRun();
            Run.FloorSession floor = rb.Session != null ? rb.Session.Current : null;
            if (floor == null) { _report.AppendLine("    ⚠ 층 세션을 만들지 못했다 — 기본 상태를 0 으로 둔다"); return; }
            spins = floor.Plan.Spins;
            required = floor.RequiredPower;
            floors = rb.Session.PreviewFloorsGained();
            _report.AppendLine($"    기본 상태 = {floor.Plan.Floor}층 실제값 · 계획 스핀 {spins}회 · " +
                               $"요구 전력 {required:F0} · 전력 0 일 때 상승 {floors}층");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  드럼 하나 = 큰 숫자 + 캡션
        // ══════════════════════════════════════════════════════════════════════

        private static TextMeshPro Drum(Transform parent, string name, float cx, float cy,
                                        float w, float h, float faceZ, float numY, float capY,
                                        string numeral, string caption)
        {
            var drum = new GameObject(name);
            drum.transform.SetParent(parent, false);
            // ⚠ 판 재질이 `RM_Sign`(반사율 0.268 · 사선 줄무늬) 이었다. 실측 렌더에서
            // **탑 전체가 방에서 두 번째로 밝은 덩어리**가 됐다 — 그건 `UP-FIX-90` 을
            // 옮기는 것이지 고치는 것이 아니다. `RM_Rust`(0.120)로 내린다.
            // 잉크가 밝은 뼈색(0.61)이므로 대비는 판이 어두울수록 오히려 커진다.
            Slab(drum.transform, "Plate", new Vector3(cx, cy, faceZ - 0.008f),
                 new Vector3(w, h, 0.014f), "GaugePlate");

            TextMeshPro num = Label(drum.transform, "Numeral", new Vector3(cx, numY, faceZ - 0.020f),
                                    AscentColumnSpec.NumeralBox, numeral, InkPrimary,
                                    TextAlignmentOptions.Midline, 12f);
            Label(drum.transform, "Caption", new Vector3(cx, capY, faceZ - 0.020f),
                  AscentColumnSpec.CaptionBox, caption, InkCaption,
                  TextAlignmentOptions.Midline, 30f);
            return num;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  헬퍼
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 월드 TMP 한 장. **글자 상자 높이(m)를 인자로 받는다** — `fontSize` 와
        /// `localScale` 의 곱이 실제 크기라 둘을 따로 적으면 다음 사람이 다시 계산해야 한다.
        ///
        /// TMP 의 1 fontSize 는 로컬 0.1 유닛이다(`RelocateExecutionLabel` 의 `em` 계산과 같다).
        /// 여기서는 `fontSize` 를 10 으로 고정하고 `localScale` 로만 크기를 만든다 —
        /// 배율이 하나뿐이어야 「어디서 크기가 정해지는가」가 흔들리지 않는다.
        /// </summary>
        private static TextMeshPro Label(Transform parent, string name, Vector3 localPos,
                                         float boxHeight, string text, Color ink,
                                         TextAlignmentOptions align, float rectWidthUnits)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;   // fwd +Z — 이 방의 규약
            // 글자 상자 높이 = fontSize(10) × 0.1 × scale  →  scale = boxHeight
            go.transform.localScale = Vector3.one * boxHeight;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Material fontMat = AssetDatabase.LoadAssetAtPath<Material>(FontMatPath);
            if (fontMat != null) tmp.fontSharedMaterial = fontMat;
            tmp.fontSize = 10f;
            tmp.enableAutoSizing = false;
            tmp.alignment = align;
            tmp.color = ink;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.SetText(text);

            RectTransform rt = tmp.rectTransform;
            rt.pivot = new Vector2(align == TextAlignmentOptions.Midline ? 0.5f : 0f, 0.5f);
            rt.sizeDelta = new Vector2(rectWidthUnits, 1.4f);
            tmp.ForceMeshUpdate();
            return tmp;
        }

        private static TextMeshPro FindTmp(string path)
        {
            GameObject go = GameObject.Find(path);
            return go != null ? go.GetComponent<TextMeshPro>() : null;
        }

        private static readonly Dictionary<string, Mesh> Baked = new Dictionary<string, Mesh>();

        private static GameObject Slab(Transform parent, string name, Vector3 localPos,
                                       Vector3 size, string material)
        {
            var b = new ProcMeshBuilder();
            b.AddBox(Vector3.zero, size, 0f, ReferenceRoomSpec.SurfaceUvPerMeter);
            Mesh mesh = Bake(b.ToMesh(name), $"AC_{size.x:F3}x{size.y:F3}x{size.z:F3}");
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            Render(go, mesh, material);
            return go;
        }

        /// <summary>피벗이 **바닥**에 있는 단위 상자. `localScale.y` 가 곧 높이(m)다.</summary>
        private static GameObject Unit(Transform parent, string name, Vector3 size, string material)
        {
            var b = new ProcMeshBuilder();
            b.AddBox(new Vector3(0f, 0.5f, 0f), size, 0f, ReferenceRoomSpec.SurfaceUvPerMeter);
            Mesh mesh = Bake(b.ToMesh(name), $"ACUnit_{size.x:F3}x{size.z:F3}");
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Render(go, mesh, material);
            return go;
        }

        private static Mesh BoltMesh()
        {
            var b = new ProcMeshBuilder();
            b.AddPrism(Vector3.zero, 0.011f, 0.009f, 0.010f, 6, MeshAxis.Z, 0f, true, true, false,
                       ReferenceRoomSpec.SurfaceUvPerMeter);
            return Bake(b.ToMesh("AscentColumnBolt"), "AC_Bolt");
        }

        private static void Bolt(Transform parent, string name, Vector3 localPos, Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            Render(go, mesh, "BareSteel");
        }

        private static void Render(GameObject go, Mesh mesh, string material)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialFor(material);
            // 작은 조각은 그림자를 드리우지 않는다 — 10층 PlayMode 가 이미 잡은 비용이다.
            mr.shadowCastingMode = mesh != null && mesh.bounds.size.magnitude >= 0.26f
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// 계기탑 전용 재질 둘은 **여기서 만든다.** `RM_Steel` 을 쓸 수 없는 이유가 있다 —
        /// 그 재질에 `_EMISSION` 키워드가 **꺼져 있어서** `MaterialPropertyBlock` 으로
        /// `_EmissionColor` 를 써도 화면에 아무 일도 일어나지 않는다.
        /// (`InstrumentPanelView` 의 전력 막대가 지금 그 상태다 — 이 라운드 범위 밖이라
        ///  고치지 않고 보고한다.)
        /// </summary>
        private static Material MaterialFor(string key)
        {
            if (key != "GaugeFill" && key != "GaugePip" && key != "GaugePlate")
                return AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/RM_{key}.mat");

            string path = $"{MaterialDir}/RM_{key}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                m = new Material(lit);
                if (!Directory.Exists(MaterialDir)) Directory.CreateDirectory(MaterialDir);
                AssetDatabase.CreateAsset(m, path);
            }
            // **항상 전 상태를 다시 쓴다.** 재사용하되 새로 만든 것과 같아야 한다 —
            // `RM_Readout` 에 지운 코드의 발광이 남아 세 라운드를 오염시킨 전례가 있다.
            //
            // `GaugePlate` 는 **밝기를 되돌리기 위해** 만든 것이 아니라 §12 `mean` 을
            // 대역 안으로 되돌리기 위해 만들었다. 실측: 잉크를 순백에서 뼈색으로 내리자
            // A/C/D `mean` 이 .0556 → .0547 로 대역 하한(.0550) 아래로 떨어졌다.
            // 글자를 다시 밝히면 `UP-FIX-90` 이 돌아오므로, 대신 **면적이 넓고 채도가
            // 낮은 판**을 올린다 — 같은 mean 을 훨씬 낮은 최대 휘도로 산다.
            // 색은 따뜻하다 (g/r 0.833 · b/r 0.633) — §12 색조 축과 같은 방향이다.
            Color baseColor = key == "GaugeFill" ? new Color(0.30f, 0.20f, 0.12f)
                            : key == "GaugePip"  ? new Color(0.24f, 0.14f, 0.10f)
                                                 : new Color(0.30f, 0.25f, 0.19f);
            m.SetColor("_BaseColor", baseColor);
            m.SetColor("_EmissionColor", Color.black);   // 런타임 MPB 가 결정한다
            m.SetFloat("_Smoothness", key == "GaugePlate" ? 0.10f : 0.22f);
            m.SetFloat("_Metallic", 0f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Mesh Bake(Mesh mesh, string key)
        {
            if (!Directory.Exists(MeshDir)) Directory.CreateDirectory(MeshDir);
            string safe = key.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
            string path = $"{MeshDir}/{safe}.asset";

            if (Baked.TryGetValue(path, out Mesh cached) && cached != null)
            {
                if (mesh != cached) Object.DestroyImmediate(mesh);
                return cached;
            }

            mesh.name = safe;
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                Baked[path] = existing;
                return existing;
            }

            AssetDatabase.CreateAsset(mesh, path);
            Baked[path] = mesh;
            return mesh;
        }
    }
}
