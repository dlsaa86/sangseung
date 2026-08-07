using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Ascend.CaptureHarness.EditorTools
{
    /// <summary>
    /// AD47 캐빈을 고정 시점에서 PNG 로 떨군다.
    ///
    /// ## 왜 씬뷰 캡처를 쓰지 않나
    ///
    /// 2026-08-08 실측 — 에디터가 포커스를 잃으면 씬뷰는 **다시 그려지지 않는다.**
    /// 시점을 바꾸고 캡처해도 직전 그림이 그대로 나오고, 바이트 수까지 같다.
    /// 자동화 중에는 포커스가 없는 것이 정상이므로 씬뷰 캡처는 조용히 거짓말을 한다.
    ///
    /// 카메라를 만들어 <c>RenderTexture</c> 로 직접 렌더하면 포커스와 무관하다.
    /// 덤으로 시점이 코드에 박혀 있어 세션이 바뀌어도 같은 그림이 나온다 — 비교가 된다.
    ///
    /// 평균 휘도를 같이 찍는 이유: 「밝다/어둡다」는 인상이고, 인상은 두 세션 뒤에
    /// 대조할 수 없다. 숫자는 대조된다.
    /// </summary>
    internal static class AscendAD47Shots
    {
        private const string OutDir =
            @"C:\Users\hufea\AppData\Local\Temp\claude\B--PROJECT-NEW-BORN-Upandup-DDD\b4abe3c6-cd91-4072-90bf-4a7f61a2c424\scratchpad";

        private struct Shot
        {
            public string Name;
            public Vector3 Eye;
            public Vector3 Look;
            public float Fov;
        }

        private static readonly Shot[] Shots =
        {
            // 플레이어 눈높이에서 기계 벽 — 3×3 판독성의 기준 시점
            new Shot { Name = "machine", Eye = new Vector3( 0.00f, 1.62f, -1.55f), Look = new Vector3(-0.30f, 1.10f,  2.10f), Fov = 70f },
            // 문 — 열림/닫힘과 문틀 두께를 본다
            new Shot { Name = "door",    Eye = new Vector3( 0.60f, 1.62f,  0.40f), Look = new Vector3(-2.10f, 1.20f,  0.00f), Fov = 72f },
            // 모서리 넓은 시야 — 벽·천장·바닥의 값 대역을 한 장에서 본다
            new Shot { Name = "corner",  Eye = new Vector3( 1.55f, 2.10f, -1.55f), Look = new Vector3(-0.60f, 1.05f,  1.60f), Fov = 78f },
            // 천장 — 찢긴 판과 처진 판
            new Shot { Name = "ceiling", Eye = new Vector3( 0.00f, 1.20f,  0.00f), Look = new Vector3( 0.10f, 3.00f,  0.30f), Fov = 80f },
        };

        [MenuItem("Ascend/Cabin/5. AD47 고정 캡처")]
        public static void Capture()
        {
            Directory.CreateDirectory(OutDir);

            var go = new GameObject("__ShotCam");
            go.hideFlags = HideFlags.HideAndDontSave;
            var cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            var log = new StringBuilder("[상승] AD47 고정 캡처 → " + OutDir + "\n");

            for (int i = 0; i < Shots.Length; i++)
            {
                var s = Shots[i];
                cam.fieldOfView = s.Fov;
                go.transform.SetPositionAndRotation(
                    s.Eye, Quaternion.LookRotation((s.Look - s.Eye).normalized, Vector3.up));

                var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
                rt.antiAliasing = 1;
                cam.targetTexture = rt;
                cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                File.WriteAllBytes(Path.Combine(OutDir, "AD47_" + s.Name + ".png"), tex.EncodeToPNG());

                var px = tex.GetPixels();
                float lum = 0f, black = 0f, blown = 0f;
                for (int p = 0; p < px.Length; p++)
                {
                    float l = px[p].r * 0.2126f + px[p].g * 0.7152f + px[p].b * 0.0722f;
                    lum += l;
                    if (l < 0.02f) black += 1f;
                    if (l > 0.95f) blown += 1f;
                }
                lum /= px.Length;
                black = black * 100f / px.Length;
                blown = blown * 100f / px.Length;

                log.AppendLine(string.Format("  {0,-9} 평균휘도={1:F4}  검정={2:F1}%  날림={3:F1}%",
                    s.Name, lum, black, blown));

                cam.targetTexture = null;
                Object.DestroyImmediate(tex);
                rt.Release();
                Object.DestroyImmediate(rt);
            }

            Object.DestroyImmediate(go);
            Debug.Log(log.ToString());
        }
    }
}
