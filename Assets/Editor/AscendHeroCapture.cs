using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Ascend.Prototype.Art;
using UnityEditor;
using UnityEngine;

namespace Ascend.Prototype.EditorTools
{
    /// <summary>
    /// 영웅 오브젝트(3×3 통관 장치 · 실행 레버) 전용 **에디터 캡처**.
    ///
    /// ## 왜 PlayMode 캡처 하네스를 쓰지 않는가
    ///
    /// `HeroSliceCaptureRig` 는 플레이 모드에서 한 판을 돌려 열 장을 찍는다 —
    /// 도메인 리로드까지 합쳐 **수십 초**다. 형상과 재질만 바뀐 것을 확인하는 데
    /// 그 값을 치를 이유가 없고, 실제로 그 반복이 이 프로젝트에서 가장 큰
    /// 시간 낭비였다. 이쪽은 씬을 열어둔 채 즉시 찍는다.
    ///
    /// ## ⚠ 씬의 카메라를 빌려 쓴다. 새로 만들지 않는다.
    ///
    /// 임시 `Camera` 를 만들어 찍었더니 **방이 실제보다 훨씬 밝게** 나왔다.
    /// 포스트프로세싱·볼륨 설정이 붙지 않았기 때문이다. 그 캡처를 근거로 톤을
    /// 판단하면 게임에 없는 문제를 고치게 된다 — 실제로 「방이 너무 밝다」로
    /// 한 번 오진했다. 그래서 **게임이 쓰는 카메라 그대로** 위치만 옮겨 찍고
    /// 원위치로 되돌린다.
    /// </summary>
    public static class AscendHeroCapture
    {
        public const string OutDir = "Captures/Hero";

        /// <summary>이름 · 위치 · 오일러 · 수직 FOV.</summary>
        public readonly struct Pose
        {
            public readonly string Name;
            public readonly Vector3 Position;
            public readonly Vector3 Euler;
            public readonly float Fov;

            public Pose(string name, Vector3 position, Vector3 euler, float fov)
            { Name = name; Position = position; Euler = euler; Fov = fov; }
        }

        /// <summary>
        /// 고정 시점 다섯. **좌표를 손으로 적지 않고 명세에서 끌어온다** — 장치나
        /// 레버가 움직이면 캡처도 따라 움직여야 비교가 성립한다.
        ///
        /// 2026-08-03 지시가 최종 결과에 남길 이미지 다섯을 직접 열거했고,
        /// 그 다섯이 이 배열이다. 순서도 지시 그대로다.
        ///   ① 무재질 정면 그레이박스        → <see cref="CaptureGreybox"/> 가 ①②만 찍는다
        ///   ② 무재질 45도 측면 그레이박스
        ///   ③ 재질 적용 후 플레이어 기본 시점
        ///   ④ 레버와 본체 연결부 클로즈업
        ///   ⑤ 유리와 영혼의 깊이가 보이는 창 하나의 클로즈업
        /// </summary>
        /// <summary>
        /// 반폭·반높이를 **화면 안에 담는** 최소 거리(m).
        ///
        /// ⚠ 첫 판본은 거리를 손으로 1.95 라고 적었고, 캐비닛 위아래가 잘렸다 —
        /// 세로가 부족했는데 가로만 계산해 본 것이다. 이 파일의 첫 규칙이
        /// 「좌표를 손으로 적지 않고 명세에서 끌어온다」인데 거리만 예외였다.
        /// 두 축을 다 계산하고 **더 먼 쪽**을 쓴다.
        /// </summary>
        private static float FitDistance(float halfWidth, float halfHeight, float vFovDeg, float margin)
        {
            float halfV = vFovDeg * 0.5f * Mathf.Deg2Rad;
            float halfH = Mathf.Atan(Mathf.Tan(halfV) * ReferenceRoomSpec.ReferenceAspect);
            float byHeight = (halfHeight + margin) / Mathf.Tan(halfV);
            float byWidth = (halfWidth + margin) / Mathf.Tan(halfH);
            return Mathf.Max(byHeight, byWidth);
        }

        public static Pose[] Standard()
        {
            float mx = ReferenceRoomSpec.MachineCenterX;
            float lx = ReferenceRoomSpec.LeverColumnCenterX;
            float face = ReferenceRoomSpec.MachineFrontZ;

            // ── 담아야 할 것의 실제 경계 ──
            // 지시의 합격 기준이 「플레이어 기본 시점에서 9개 창과 레버가 함께 보임」
            // 이므로 캐비닛 **+ 레버 컬럼 + 경고등**이 한 상자 안에 들어가야 한다.
            float boxLeft = ReferenceRoomSpec.MachineLeftX;
            float boxRight = lx + ReferenceRoomSpec.LeverColumnWidth * 0.5f;
            float boxBottom = ReferenceRoomSpec.MachineBottomY;
            float boxTop = ReferenceRoomSpec.WarningLampCenterY + ReferenceRoomSpec.WarningLampDiameter * 0.5f;
            float pairX = (boxLeft + boxRight) * 0.5f;
            float pairY = (boxBottom + boxTop) * 0.5f;
            float halfW = (boxRight - boxLeft) * 0.5f;
            float halfH = (boxTop - boxBottom) * 0.5f;

            const float frontFov = 46f;
            float frontDist = FitDistance(halfW, halfH, frontFov, 0.14f);

            // 사선은 깊이가 폭에 더해지므로 조금 더 물러난다.
            const float obliqueFov = 44f;
            float obliqueDist = FitDistance(halfW, halfH, obliqueFov, 0.14f) * 1.08f;
            const float yaw = 42f;
            float yawRad = yaw * Mathf.Deg2Rad;

            return new[]
            {
                // ① 정면 — 하나의 직사각형 기계로 읽히는지. 3×3·뱅크 셋·레버 컬럼.
                new Pose("01_front", new Vector3(pairX, pairY, face - frontDist), Vector3.zero, frontFov),

                // ② 사선 — **깊이 단계가 실루엣으로 갈라지는지.**
                // 벽 / 후면 장착 프레임 / 외곽 프레임 / 뱅크 리브 / 도어 / 클램프 링 / 들어간 유리.
                // 정면에서만 원이 보이고 측면에서 전부 같은 깊이면 실패다.
                //
                // 궤도로 잡는다 — 장치 중심을 기준으로 회전시키므로 거리가 바뀌어도
                // 대상이 화면 밖으로 나가지 않는다. 손으로 x·z 를 적으면 반드시 어긋난다.
                new Pose("02_oblique45",
                         new Vector3(pairX + Mathf.Sin(yawRad) * obliqueDist, pairY + 0.10f,
                                     face - Mathf.Cos(yawRad) * obliqueDist),
                         new Vector3(3f, -yaw, 0f), obliqueFov),

                // ③ 플레이어 기본 시점 — 사람이 실제로 서는 자리·눈높이·화각.
                // **여기서 읽히지 않으면 다른 어디서 읽혀도 소용없다.**
                new Pose("03_player_eye",
                         new Vector3(0f, ReferenceRoomSpec.EyeHeight, ReferenceRoomSpec.CameraZ),
                         Vector3.zero, ReferenceRoomSpec.VerticalFov),

                // ④ 레버와 본체 연결부 — 베이스 플레이트가 캐비닛을 물고,
                // 구동 로드가 상단 하우징으로 올라가는 그 지점.
                new Pose("04_lever_join",
                         new Vector3(lx + 0.54f, ReferenceRoomSpec.LeverPivotY + 0.34f, face - 0.92f),
                         new Vector3(10f, -42f, 0f), 44f),

                // ⑤ 창 하나 근접 — 유리와 영혼 **사이의 공간**이 보이는 각도.
                // ⚠ 온축으로 잡지 않는다. 정면에서는 패럴랙스가 0 이라
                // 「유리에 붙은 스티커」와 「유리 뒤 115mm 의 물질」이 구분되지 않는다.
                new Pose("05_window_macro",
                         new Vector3(mx + 0.34f, ReferenceRoomSpec.WindowGridCenterY + 0.09f, face - 0.52f),
                         new Vector3(6f, -30f, 0f), 32f),

                // ⑥ **진짜 측면** — 지시의 「측면에서 최소 네 단계의 깊이 단차」는
                // 이 각도에서만 판정된다.
                //
                // ⚠ 직전 세트의 최대 각도가 −42° 였고, 독립 평가가
                // 「진짜 측면(−75~−90°) 캡처가 없어 여유를 확인할 수 없다」고
                // 판정 자체를 보류했다. 22mm 돌출은 −42° 에서 화면상 5~6px 라
                // **단차가 있는데도 없는 것과 구분되지 않는다.**
                //
                // −78° 로 잡는다. −90° 는 도어 면이 완전히 사라져 링과 유리의
                // 관계가 안 보이고, 그러면 이 컷이 재는 것이 하나 줄어든다.
                new Pose("06_side78",
                         new Vector3(pairX + Mathf.Sin(78f * Mathf.Deg2Rad) * obliqueDist * 0.86f,
                                     pairY + 0.06f,
                                     face - Mathf.Cos(78f * Mathf.Deg2Rad) * obliqueDist * 0.86f),
                         new Vector3(2f, -78f, 0f), obliqueFov),
            };
        }

        [MenuItem("Ascend/Room/Capture Hero Objects")]
        public static void CaptureStandard() => Capture(Standard(), "");

        // ══════════════════════════════════════════════════════════════════════
        //  무재질 그레이박스 — 형태가 재질 없이 읽히는가
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 🔴 지시 「모든 녹, 텍스처, 발광과 조명을 제거한 중성 회색 상태에서 먼저
        /// 확인한다. **그레이박스 상태에서도 장치의 기능이 이해되지 않으면 텍스처
        /// 작업으로 넘어가지 마라.**」
        ///
        /// ## 왜 재질을 「지우지」 않고 **빌려 바꾸는가**
        ///
        /// `.mat` 에셋을 회색으로 덮어쓰고 되돌리는 방식은 두 번 실패할 수 있다 —
        /// 되돌리기 전에 예외가 나면 프로젝트의 재질이 통째로 회색으로 남고,
        /// 그것은 **직렬화 에셋 손상**이다. 이 저장소가 이미 겪은 종류다.
        ///
        /// 그래서 에셋을 건드리지 않는다. 렌더러의 `sharedMaterial` 포인터만
        /// 메모리 상에서 갈아 끼우고 `finally` 에서 원복한다. 에셋 파일은 한 바이트도
        /// 바뀌지 않으므로 최악의 경우에도 씬을 다시 열면 끝난다.
        /// </summary>
        [MenuItem("Ascend/Room/Capture Greybox (No Materials)")]
        public static void CaptureGreybox()
        {
            GameObject room = GameObject.Find(AscendReferenceRoom.RootName);
            if (room == null) { Debug.LogError("[상승] ReferenceRoom 이 없다 — 먼저 Build Reference Room."); return; }

            // 중성 회색 하나. 반사율 0.5, 금속성 0 — 형상만 남기고 재질 정보를 지운다.
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            var grey = new Material(lit) { name = "TEMP_Greybox", hideFlags = HideFlags.HideAndDontSave };
            grey.SetColor("_BaseColor", new Color(0.52f, 0.52f, 0.52f, 1f));
            grey.SetFloat("_Smoothness", 0.18f);
            grey.SetFloat("_Metallic", 0f);

            // 🔴 **방 밖까지 덮는다.** 직전 판본은 `ReferenceRoom` 자식만 회색으로
            // 바꿨고, 그 결과 `GrayboxWorld/Car/DoorControl` 이 그레이박스 캡처에서
            // **혼자 순수 검정 사각형**으로 남았다. 독립 평가자가 「정체 불명 ·
            // 그레이 오버라이드가 닿지 않은 오브젝트이거나 벽의 구멍」으로 지목했다.
            //
            // 그레이박스의 목적은 「재질을 지우고 형태만 본다」이다. 한 물체라도
            // 재질을 유지하면 그 물체가 형태 판정에서 가장 눈에 띄는 것이 된다 —
            // 정확히 반대 효과다. 화면에 나오는 것은 전부 덮는다.
            var all = new List<Renderer>();
            foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!r.enabled || r.GetComponent<TMPro.TMP_Text>() != null) continue;
                all.Add(r);
            }
            Renderer[] renderers = all.ToArray();
            var saved = new Material[renderers.Length][];

            // 🔴 **유리와 영혼은 그레이박스에서 숨긴다.**
            //
            // 첫 그레이박스에서 유리가 **불투명 회색 원반**이 되어 보어를 막았고,
            // 독립 평가자가 아홉 창을 「평판에 뚫린 얕은 열린 구멍」으로 읽었다.
            // 챔버가 170mm 깊이로 실제로 있는데 **캡처가 그것을 가린 것이다** —
            // 평가가 틀린 게 아니라 내가 못 보이게 찍었다.
            //
            // 지시의 그레이박스 확인 목록에 유리도 영혼도 없다(실루엣·3×3 정렬·
            // 캐비닛 깊이·도어와 창의 관계·외곽 프레임·레버 연결·시점 크기).
            // 형태를 보는 캡처가 형태를 가리면 안 된다.
            var hiddenParts = new List<Renderer>();
            foreach (Renderer r in renderers)
            {
                string n = r.gameObject.name;
                if (n != AscendReferenceRoom.GlassName && n != AscendReferenceRoom.SoulName && n != "Core") continue;
                if (!r.enabled) continue;
                r.enabled = false;
                hiddenParts.Add(r);
            }

            // 그레이박스에서는 **발광도 끈다.** 영혼이 빛나면 그것이 화면에서 가장
            // 밝은 물체가 되어 시선을 독점하고, 판단 대상이 「형태」에서 「구슬」로 바뀐다.
            Light[] lights = room.GetComponentsInChildren<Light>(false);
            var lightHome = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) lightHome[i] = lights[i].intensity;

            try
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    saved[i] = renderers[i].sharedMaterials;
                    var swap = new Material[saved[i].Length];
                    for (int m = 0; m < swap.Length; m++) swap[m] = grey;
                    renderers[i].sharedMaterials = swap;
                }

                // 형태를 보는 조명으로 바꾼다 — 점광 하나로는 구석이 죽어 실루엣이
                // 안 보인다. 앰비언트를 올려 **면의 방향**만 읽히게 한다.
                Color ambient = RenderSettings.ambientLight;
                float ambIntensity = RenderSettings.ambientIntensity;
                try
                {
                    RenderSettings.ambientLight = new Color(0.42f, 0.43f, 0.46f, 1f);
                    RenderSettings.ambientIntensity = 1f;
                    for (int i = 0; i < lights.Length; i++) lights[i].intensity = lightHome[i] * 0.45f;

                    // 🔴 **여섯 장 전부 찍는다.**
                    //
                    // 직전 판본은 정면과 −42° 두 장만 찍었다. 그런데 지시의 합격
                    // 조건 여덟 개 중 셋(동력 전달·부품 기능·기능 이해)은
                    // 근접 컷 없이는 원리적으로 판정할 수 없다 — 399 px/m 배율에서
                    // 46mm 힌지는 18px 이고, 「이게 경첩이다」로 읽히지 않는 것이
                    // 형태 결함인지 배율 탓인지 갈리지 않는다.
                    //
                    // 독립 평가가 세 조건을 **판정 불가**로 돌려보냈다. 판정할 수
                    // 없는 증거를 내는 것은 증거를 안 내는 것과 같다.
                    Debug.Log("[상승] 무재질 그레이박스 캡처\n" + Capture(Standard(), "grey_"));
                }
                finally
                {
                    RenderSettings.ambientLight = ambient;
                    RenderSettings.ambientIntensity = ambIntensity;
                    for (int i = 0; i < lights.Length; i++)
                        if (lights[i] != null) lights[i].intensity = lightHome[i];
                }
            }
            finally
            {
                // ⚠ 반드시 되돌린다. 여기서 실패하면 씬이 회색으로 남는다.
                for (int i = 0; i < renderers.Length; i++)
                    if (renderers[i] != null && saved[i] != null) renderers[i].sharedMaterials = saved[i];

                // 🔴 **끈 렌더러를 되살린다.** 이 두 줄을 빠뜨려서 그레이박스 캡처가
                // 유리와 영혼을 **영구히** 꺼 버렸고, 바로 다음 재질 캡처가
                // 붉은 화소 **0.00%** 로 나왔다(직전 정상값 0.89%).
                //
                // 이 파일에 「반드시 되돌린다」를 두 번이나 적어 놓고 새로 추가한
                // 상태 하나를 되돌리지 않았다. 껐다 켜는 코드는 **끄는 줄과 켜는 줄이
                // 같은 화면에 없으면** 반드시 한쪽을 빠뜨린다.
                foreach (Renderer r in hiddenParts) if (r != null) r.enabled = true;

                UnityEngine.Object.DestroyImmediate(grey);
            }
        }

        /// <summary>
        /// 찍고, 컷마다 판단 근거가 되는 화소 통계를 함께 돌려준다.
        ///
        /// 통계를 같이 내는 이유: 「분홍으로 보인다」는 인상이고 「붉은 화소 0.13%,
        /// 흰끼 2.4%」는 증거다. 이 저장소는 발광을 세게 줄수록 붉어질 것이라는
        /// **인상**을 믿었다가 실제로는 희어지는 것을 실측으로 뒤집은 적이 있다.
        /// </summary>
        public static string Capture(IEnumerable<Pose> poses, string prefix)
        {
            Camera cam = Camera.main;
            if (cam == null)
                cam = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(c => c.targetTexture == null);
            if (cam == null) return "카메라 없음 — 캡처하지 않았다.";

            Directory.CreateDirectory(OutDir);
            var sb = new StringBuilder();

            // 원상복구용. 캡처가 씬을 영구히 바꾸면 안 된다.
            bool wasEnabled = cam.enabled;
            Vector3 home = cam.transform.position;
            Quaternion homeRot = cam.transform.rotation;
            float homeFov = cam.fieldOfView;
            RenderTexture homeTarget = cam.targetTexture;

            // 월드 공간 안내문은 **끄고 찍는다.** 형상·재질을 판단하는 캡처에
            // 「과수확」 같은 흰 글자가 겹치면 그것이 화면에서 가장 밝은 물체가
            // 되어 시선이 거기로 간다 — 평가 대상이 바뀌는 것이다.
            var hidden = new List<Renderer>();
            foreach (TMPro.TextMeshPro t in
                     UnityEngine.Object.FindObjectsByType<TMPro.TextMeshPro>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var r = t.GetComponent<Renderer>();
                if (r != null && r.enabled) { r.enabled = false; hidden.Add(r); }
            }

            // 🔴 **색수차를 끄고 찍는다.**
            //
            // 독립 평가자가 그레이박스 캡처에서 세로 마젠타/시안 프린지를 지적했다.
            // 그건 `VISUAL_SPEC` §8 이 금지한 「지속적 색수차」이면서, 동시에
            // **형상 판정용 증거를 오염시킨다** — 부재 경계마다 색 분리가 생겨
            // 「이 선이 부품 경계인가 렌즈 수차인가」가 구분되지 않는다.
            // 씬 설정은 건드리지 않고 컴포넌트만 잠시 끄고 되돌린다.
            var caOff = new List<UnityEngine.Rendering.VolumeComponent>();
            foreach (UnityEngine.Rendering.Volume v in
                     UnityEngine.Object.FindObjectsByType<UnityEngine.Rendering.Volume>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (v.profile == null) continue;
                foreach (UnityEngine.Rendering.VolumeComponent c in v.profile.components)
                {
                    if (c == null || !c.active) continue;
                    if (c.GetType().Name != "ChromaticAberration") continue;
                    c.active = false;
                    caOff.Add(c);
                }
            }

            try
            {
                foreach (Pose p in poses)
                {
                    cam.enabled = true;
                    cam.transform.SetPositionAndRotation(p.Position, Quaternion.Euler(p.Euler));
                    cam.fieldOfView = p.Fov;
                    sb.AppendLine(Shoot(cam, prefix + p.Name));
                    // 카메라 좌표를 **캡처와 함께** 남긴다. 좌표 없는 캡처는
                    // 평가자가 역산해야 하고, 역산은 틀릴 수 있다.
                    sb.AppendLine($"      pos=({p.Position.x:F3}, {p.Position.y:F3}, {p.Position.z:F3}) " +
                                  $"rot=({p.Euler.x:F1}, {p.Euler.y:F1}, {p.Euler.z:F1}) vFov={p.Fov:F2}");
                }
                WriteManifest(poses, prefix, sb.ToString());
            }
            finally
            {
                foreach (UnityEngine.Rendering.VolumeComponent c in caOff) if (c != null) c.active = true;
                // ⚠ 반드시 되돌린다. 캡처가 씬을 영구히 바꾸면 다음 사람이
                // 「안내문이 사라졌다」를 버그로 쫓게 된다.
                foreach (Renderer r in hidden) if (r != null) r.enabled = true;
                cam.targetTexture = homeTarget;
                cam.transform.SetPositionAndRotation(home, homeRot);
                cam.fieldOfView = homeFov;
                cam.enabled = wasEnabled;
            }

            Debug.Log("[상승] 영웅 캡처\n" + sb);
            return sb.ToString();
        }

        /// <summary>
        /// 🔴 **좌표 없는 캡처는 증거가 아니다.**
        ///
        /// 독립 평가자가 `Captures/Hero/` 에 manifest 가 없어 카메라를 소스에서
        /// **역산**해야 했다고 보고했다. 역산은 성립했지만, 성립하지 않았다면
        /// 판정 전체가 틀린 전제 위에 서게 된다. `Captures/TenFloor/` 는 이미
        /// manifest 를 쓰고 있었고 여기만 빠져 있었다.
        /// </summary>
        private static void WriteManifest(IEnumerable<Pose> poses, string prefix, string stats)
        {
            var m = new StringBuilder();
            m.AppendLine("# Hero Capture Manifest");
            m.AppendLine($"machineFingerprint: {SystemInfo.deviceName} / {SystemInfo.graphicsDeviceName} / {SystemInfo.graphicsDeviceType}");
            m.AppendLine($"unity: {Application.unityVersion}");
            m.AppendLine($"resolution: 1600x900");
            m.AppendLine($"prefix: {(string.IsNullOrEmpty(prefix) ? "(none)" : prefix)}");
            m.AppendLine($"chromaticAberration: disabled during capture");
            m.AppendLine();
            m.AppendLine("## 카메라");
            foreach (Pose p in poses)
                m.AppendLine($"  {prefix}{p.Name}: pos=({p.Position.x:F3}, {p.Position.y:F3}, {p.Position.z:F3}) " +
                             $"euler=({p.Euler.x:F1}, {p.Euler.y:F1}, {p.Euler.z:F1}) vFov={p.Fov:F2}");
            m.AppendLine();
            m.AppendLine("## 장치 치수 (ReferenceRoomSpec 실측)");
            m.AppendLine($"  캐비닛 {ReferenceRoomSpec.MachineWidth:F3} × {ReferenceRoomSpec.MachineHeight:F3} × {ReferenceRoomSpec.MachineDepth:F2}");
            m.AppendLine($"  프레임 굵기 외곽 {ReferenceRoomSpec.OuterFrameBand * 1000f:F0} / 리브 {ReferenceRoomSpec.BankRibWidth * 1000f:F0} / 격벽 {ReferenceRoomSpec.BulkheadHeight * 1000f:F0} mm");
            m.AppendLine($"  프레임 돌출 외곽 {ReferenceRoomSpec.OuterFrameProud * 1000f:F0} / " +
                         $"리브 캡 {ReferenceRoomSpec.RibCapProud * 1000f:F0} / 리브 {ReferenceRoomSpec.BankRibProud * 1000f:F0} / " +
                         $"격벽 {ReferenceRoomSpec.BulkheadProud * 1000f:F0} mm (격벽은 **음각**)");
            m.AppendLine($"  세로 무중단 비 {ReferenceRoomSpec.LongestVerticalRunRatio:P1} (상한 {ReferenceRoomSpec.MaxVerticalRunRatio:P0})");
            m.AppendLine($"  간격 이방비 {ReferenceRoomSpec.WindowPitchAnisotropy:F3}");
            m.AppendLine();
            m.AppendLine(LockStateFacts());
            m.AppendLine();
            m.AppendLine("## 화소 통계");
            m.Append(stats);

            File.WriteAllText($"{OutDir}/manifest{(string.IsNullOrEmpty(prefix) ? "" : "_" + prefix.TrimEnd('_'))}.txt", m.ToString());
        }

        /// <summary>
        /// 🔴 **찍은 순간의 잠금 기구 자세를 적는다.**
        ///
        /// 왜 필요한가: 이 장치의 정체가 「레버 하나가 챔버 아홉을 동시에 잠근다」인데,
        /// 매니페스트가 캡처 순간이 **체결인지 해제인지** 말하지 않았다. 독립 평가는
        /// 그 때문에 「동력 전달 경로가 추적 가능한가」를 **판정 불가**로 돌려보냈다 —
        /// 상태 탭이 올라가 있어야 정상인지 내려가 있어야 정상인지 알 수 없으니
        /// 화면에 무엇이 보이든 옳다고도 그르다고도 할 수 없었다.
        ///
        /// 각도와 거리는 주장하지 않고 **트랜스폼에서 잰다.**
        /// </summary>
        private static string LockStateFacts()
        {
            var sb = new StringBuilder();
            sb.AppendLine("## 잠금 기구 자세 (캡처 순간 · 트랜스폼 실측)");

            var view = UnityEngine.Object.FindFirstObjectByType<Prototype.View.CustomsLockView>(
                FindObjectsInactive.Include);
            if (view == null)
            {
                sb.AppendLine("  **확인 못 함** — CustomsLockView 를 찾지 못했다. " +
                              "동력 전달 관련 판정은 이 세트로 하지 말 것");
                return sb.ToString();
            }

            sb.AppendLine($"  체결 {(view.IsEngaged ? "걸림" : "해제")} · " +
                          $"체결 진행도 {view.Engagement:P0} · 핀 후퇴 {view.PinRetraction:P0}");

            var grid = GameObject.Find("ReferenceRoom/SoulMachine/WindowGrid");
            var shaft = GameObject.Find($"ReferenceRoom/SoulMachine/ShaftHousing/{AscendReferenceRoom.CommonShaftName}");
            if (shaft != null)
                sb.AppendLine($"  공통축 회전 {shaft.transform.localEulerAngles.x:F1}° (X축)");

            for (int i = 0; i < 3; i++)
            {
                var tab = GameObject.Find($"ReferenceRoom/SoulMachine/ShaftHousing/{AscendReferenceRoom.StatusTabName}_{i}");
                if (tab != null)
                    sb.AppendLine($"  상태 탭 {i} y {tab.transform.localPosition.y * 1000f:F1} mm");
            }

            if (grid != null)
            {
                int measured = 0;
                float sum = 0f;
                for (int col = 0; col < 3; col++)
                    for (int row = 0; row < 3; row++)
                    {
                        Transform module = grid.transform.Find($"{AscendReferenceRoom.WindowModuleName}_{col}{row}");
                        Transform clamp = module != null ? module.Find(AscendReferenceRoom.LockClampName) : null;
                        if (clamp == null) continue;
                        sum += clamp.localEulerAngles.z;
                        measured++;
                    }
                sb.AppendLine(measured == 9
                    ? $"  클램프 9개 평균 각 {sum / 9f:F1}° (9/9 실측)"
                    : $"  클램프 **{measured}/9 만 실측됐다** — 나머지는 확인 못 함");
            }

            sb.AppendLine("  ※ 이 세트는 에디터 정지 상태다. 「탭이 안 움직인다」는 지적은 " +
                          "이 값이 해제 자세인지 먼저 확인한 뒤에만 성립한다.");
            return sb.ToString();
        }

        private static string Shoot(Camera cam, string name)
        {
            const int W = 1600, H = 900;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;

            File.WriteAllBytes($"{OutDir}/{name}.png", tex.EncodeToPNG());

            Color32[] px = tex.GetPixels32();
            long lum = 0; int red = 0, washed = 0, dark = 0;
            var hist = new int[256];
            for (int i = 0; i < px.Length; i++)
            {
                int l = (px[i].r * 77 + px[i].g * 150 + px[i].b * 29) >> 8;
                lum += l; hist[l]++;
                if (l < 12) dark++;
                // 붉다 = 다른 두 채널을 확실히 앞선다. 「분홍」은 여기서 탈락한다.
                if (px[i].r > 80 && px[i].r > px[i].g * 1.7f && px[i].r > px[i].b * 1.7f) red++;
                // 흰끼 = 붉어야 할 곳이 채널 포화로 색을 잃은 화소
                if (px[i].r > 205 && px[i].g > 150) washed++;
            }
            int total = px.Length;
            int p50 = Percentile(hist, total, 0.50f), p95 = Percentile(hist, total, 0.95f);
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release(); UnityEngine.Object.DestroyImmediate(rt);

            return $"  {name,-16} 평균 {lum / (float)total:F1}  p50 {p50}  p95 {p95}  " +
                   $"암부 {dark * 100f / total:F1}%  붉은 {red * 100f / total:F2}%  흰끼 {washed * 100f / total:F2}%";
        }

        private static int Percentile(int[] hist, int total, float q)
        {
            int want = Mathf.RoundToInt(total * q), acc = 0;
            for (int i = 0; i < 256; i++) { acc += hist[i]; if (acc >= want) return i; }
            return 255;
        }
    }
}
