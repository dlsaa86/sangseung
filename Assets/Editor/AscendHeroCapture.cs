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
        /// 고정 시점 넷. **좌표를 손으로 적지 않고 명세에서 끌어온다** — 장치나
        /// 레버가 움직이면 캡처도 따라 움직여야 비교가 성립한다.
        /// </summary>
        public static Pose[] Standard()
        {
            float gy = ReferenceRoomSpec.WindowGridCenterY;
            float mx = ReferenceRoomSpec.MachineCenterX;
            float lx = ReferenceRoomSpec.LeverColumnCenterX;
            float face = ReferenceRoomSpec.WallRearZ - ReferenceRoomSpec.MachineDepth;

            return new[]
            {
                // 장치 정면 — 아홉 창이 모두 들어오는 최소 거리
                new Pose("device_front", new Vector3(mx, gy, face - 1.55f), Vector3.zero, 44f),
                // 모듈 하나 근접 — 「유리 뒤의 물질」이 읽히는지 보는 시점
                new Pose("module_macro",
                         new Vector3(mx + ReferenceRoomSpec.WindowPitch, gy + ReferenceRoomSpec.WindowPitch, face - 0.46f),
                         Vector3.zero, 30f),
                // 장치 사선 — 층의 두께가 실루엣으로 읽히는지
                new Pose("device_oblique", new Vector3(mx + 1.05f, gy + 0.28f, face - 1.15f),
                         new Vector3(6f, -34f, 0f), 40f),
                // 레버 — 사람이 실제로 서서 보는 눈높이·거리
                // ⚠ 레버는 **정면에서** 본다. 비스듬한 각도로 잡았더니 프레임에
                // 벽만 들어오고 레버가 빠졌다 — 위치를 눈대중으로 맞추지 않는다.
                new Pose("lever",
                         new Vector3(lx, ReferenceRoomSpec.LeverPivotY + 0.16f, face - 1.05f),
                         new Vector3(7f, 0f, 0f), 42f),
                // 레버 근접 — 잠금핀·스토퍼·허브가 개별 부품으로 읽히는지
                new Pose("lever_macro",
                         new Vector3(lx + 0.26f, ReferenceRoomSpec.LeverPivotY + 0.10f, face - 0.52f),
                         new Vector3(4f, -22f, 0f), 40f),
            };
        }

        [MenuItem("Ascend/Room/Capture Hero Objects")]
        public static void CaptureStandard() => Capture(Standard(), "");

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

            try
            {
                foreach (Pose p in poses)
                {
                    cam.enabled = true;
                    cam.transform.SetPositionAndRotation(p.Position, Quaternion.Euler(p.Euler));
                    cam.fieldOfView = p.Fov;
                    sb.AppendLine(Shoot(cam, prefix + p.Name));
                }
            }
            finally
            {
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
