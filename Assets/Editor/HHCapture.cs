// HHCapture.cs — 에디트 모드에서 카메라를 PNG 로 굽는다(플레이 진입 없이 룩 비교용).
using System.IO;
using UnityEngine;
using UnityEditor;

public static class HHCapture
{
    public static void Shoot(Camera cam, string absPath, int w = 1600, int h = 900)
    {
        if (cam == null) { Debug.LogError("[HHCapture] camera null"); return; }
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        var prev = cam.targetTexture;
        var prevActive = RenderTexture.active;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        cam.targetTexture = prev;
        RenderTexture.active = prevActive;
        Directory.CreateDirectory(Path.GetDirectoryName(absPath));
        File.WriteAllBytes(absPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release(); Object.DestroyImmediate(rt);
        Debug.Log("[HHCapture] wrote " + absPath);
    }

    public static void ShootMain(string absPath, int w = 1600, int h = 900)
    {
        var cam = Camera.main;
        if (cam == null)
        {
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            { cam = c; break; }
        }
        Shoot(cam, absPath, w, h);
    }
}
