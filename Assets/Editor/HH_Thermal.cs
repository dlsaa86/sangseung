// HH_Thermal.cs — 에디터 발열/부하 계측 + 저발열 프리셋
// 해븐즈헝거 작업용. 런타임 빌드에는 포함되지 않음(Editor 폴더).
using System;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class HH_Thermal
{
    // ── 에디터 update 틱 계측 (도메인 리로드에도 살아남게 파일 스크립트로 둔다) ──
    static int s_ticks;
    static double s_t0;
    static bool s_running;

    static HH_Thermal()
    {
        // 항상 켜둔다. 비용은 int++ 하나.
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        s_ticks = 0;
        s_t0 = EditorApplication.timeSinceStartup;
        s_running = true;
    }

    static void Tick() { s_ticks++; }

    [MenuItem("HeavensHunger/Thermal/측정 리셋", priority = 10)]
    public static void MeterReset()
    {
        s_ticks = 0;
        s_t0 = EditorApplication.timeSinceStartup;
        s_running = true;
        Debug.Log("[HH_Thermal] meter reset");
    }

    [MenuItem("HeavensHunger/Thermal/측정 읽기", priority = 11)]
    public static void MeterRead()
    {
        double dt = EditorApplication.timeSinceStartup - s_t0;
        double hz = s_ticks / Math.Max(0.001, dt);
        Debug.Log(string.Format("[HH_Thermal] ticks={0} sec={1:F2} Hz={2:F1}", s_ticks, dt, hz));
    }

    public static string MeterLine()
    {
        double dt = EditorApplication.timeSinceStartup - s_t0;
        return string.Format("ticks={0} sec={1:F2} Hz={2:F1}", s_ticks, dt, s_ticks / Math.Max(0.001, dt));
    }

    // ── 현재 상태 리포트 ──
    [MenuItem("HeavensHunger/Thermal/현재 설정 리포트", priority = 1)]
    public static void Report()
    {
        Debug.Log(BuildReport());
    }

    public static string BuildReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("===== HH THERMAL REPORT =====");
        sb.AppendLine("[meter] " + MeterLine());
        sb.AppendLine("[editor] InteractionMode=" + EditorPrefs.GetInt("InteractionMode", -1)
                      + " ApplicationIdleTime=" + EditorPrefs.GetInt("ApplicationIdleTime", -1) + "ms"
                      + " AutoRefreshMode=" + EditorPrefs.GetInt("kAutoRefreshMode", -1));
        sb.AppendLine("[editor] EnterPlayModeOptions=" + EditorSettings.enterPlayModeOptions
                      + " enabled=" + EditorSettings.enterPlayModeOptionsEnabled);
        sb.AppendLine("[editor] giWorkflow=" + Lightmapping.giWorkflowMode);
        sb.AppendLine("[quality] level=" + QualitySettings.GetQualityLevel()
                      + " vSync=" + QualitySettings.vSyncCount);

        var urp = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) urp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null)
        {
            sb.AppendLine("[urp] asset=" + urp.name
                          + " renderScale=" + urp.renderScale
                          + " msaa=" + urp.msaaSampleCount
                          + " hdr=" + urp.supportsHDR);
            sb.AppendLine("[urp] shadowDist=" + urp.shadowDistance
                          + " cascades=" + urp.shadowCascadeCount
                          + " mainRes=" + urp.mainLightShadowmapResolution
                          + " addRes=" + urp.additionalLightsShadowmapResolution);
            sb.AppendLine("[urp] depthTex=" + urp.supportsCameraDepthTexture
                          + " opaqueTex=" + urp.supportsCameraOpaqueTexture);
        }
        else sb.AppendLine("[urp] <none>");
        return sb.ToString();
    }

    // ── 저발열 프리셋 적용 ──
    // idleMs: 에디터가 프레임 사이에 쉬는 시간(ms). 33 = 약 30fps, 50 = 약 20fps.
    public static void ApplyLowHeat(int idleMs, int msaa, float shadowDist, int cascades,
                                    int shadowRes, bool opaqueTex, int addShadowRes = 0)
    {
        // UnityEditor.PreferencesProvider+InteractionMode 로 확인함:
        // 0 Default / 1 NoThrottling / 2 MonitorRefreshRate / 3 Custom
        EditorPrefs.SetInt("InteractionMode", 3);
        EditorPrefs.SetInt("ApplicationIdleTime", idleMs);
        PushInteractionMode();

        QualitySettings.vSyncCount = 1;
        Lightmapping.giWorkflowMode = Lightmapping.GIWorkflowMode.OnDemand;

        var urp = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) urp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null)
        {
            var so = new SerializedObject(urp);
            SetIf(so, "m_MSAA", msaa);
            SetIf(so, "m_ShadowDistance", shadowDist);
            SetIf(so, "m_ShadowCascadeCount", cascades);
            SetIf(so, "m_MainLightShadowmapResolution", shadowRes);
            // 포인트 라이트 1개 = 섬도우맵 6장. 1024 아틀라스에 밀어넣으면 장당 256px 로 리듀스된다.
            // 그림자를 하나만 두되 아틀라스를 넓히는 게 같은 비용에 훨씬 낫다.
            SetIf(so, "m_AdditionalLightsShadowmapResolution", addShadowRes > 0 ? addShadowRes : shadowRes);
            SetIf(so, "m_RequireOpaqueTexture", opaqueTex);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);
            AssetDatabase.SaveAssetIfDirty(urp);
        }
        MeterReset();
        Debug.Log("[HH_Thermal] low-heat preset applied.\n" + BuildReport());
    }

    [MenuItem("HeavensHunger/Thermal/저발열 켜기 (30fps)", priority = 2)]
    public static void LowHeat30() { ApplyLowHeat(33, 2, 18f, 1, 1024, false, 2048); }

    [MenuItem("HeavensHunger/Thermal/저발열 강 (20fps)", priority = 3)]
    public static void LowHeat20() { ApplyLowHeat(50, 1, 14f, 1, 512, false, 1024); }

    [MenuItem("HeavensHunger/Thermal/원복 (모니터 주사율)", priority = 4)]
    public static void RestoreDefault()
    {
        EditorPrefs.SetInt("InteractionMode", 2);
        EditorPrefs.SetInt("ApplicationIdleTime", 16);
        PushInteractionMode();
        MeterReset();
        Debug.Log("[HH_Thermal] restored to monitor refresh rate.");
    }

    // EditorApplication.UpdateInteractionModeSettings() 는 internal 이라 리플렉션으로 호출한다.
    // 이걸 불러야 재시작 없이 즉시 적용된다.
    public static void PushInteractionMode()
    {
        var mi = typeof(EditorApplication).GetMethod("UpdateInteractionModeSettings",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);
        if (mi != null) { mi.Invoke(null, null); Debug.Log("[HH_Thermal] UpdateInteractionModeSettings() invoked"); }
        else Debug.LogWarning("[HH_Thermal] UpdateInteractionModeSettings not found — 에디터 재시작 시 적용됨");
    }

    static void SetIf(SerializedObject so, string prop, int v)
    {
        var p = so.FindProperty(prop); if (p != null) p.intValue = v;
        else Debug.LogWarning("[HH_Thermal] missing prop " + prop);
    }
    static void SetIf(SerializedObject so, string prop, float v)
    {
        var p = so.FindProperty(prop); if (p != null) p.floatValue = v;
        else Debug.LogWarning("[HH_Thermal] missing prop " + prop);
    }
    static void SetIf(SerializedObject so, string prop, bool v)
    {
        var p = so.FindProperty(prop); if (p != null) p.boolValue = v;
        else Debug.LogWarning("[HH_Thermal] missing prop " + prop);
    }
}
