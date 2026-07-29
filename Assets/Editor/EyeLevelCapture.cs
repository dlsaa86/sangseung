using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 플레이어 눈높이에서 씬을 렌더해 PNG로 남긴다.
///
/// 캡처 하네스(Play 모드 기반)를 쓰지 않는 이유: 지금 평가할 것은 공간 배치다 —
/// 상호작용물이 눈에 띄는가, 시선 높이에 있는가, 좁은데 접근이 막히지는 않는가.
/// 전부 정적인 성질이라 Play 모드가 필요 없다. Play 모드를 끌어들이면 도메인 리로드와
/// 코루틴 타이밍이 붙어서 실패 지점만 늘어난다.
///
/// 런타임 HUD(IMGUI)는 여기 안 잡힌다. 그건 의도다 — 버릴 디버그 UI이고,
/// 지금 판정 대상이 아니다.
/// </summary>
public static class EyeLevelCapture
{
    private const string ScenePath = "Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity";
    private const int Width = 1280;
    private const int Height = 720;

    [MenuItem("Ascend/Capture — 눈높이 뷰")]
    public static void Run()
    {
        var log = new StringBuilder();
        log.AppendLine("[상승] === 눈높이 캡처 ===");

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Transform player = GameObject.Find("Player")?.transform;
        Transform head = player != null ? player.Find("Head") : null;
        Camera cam = head != null ? head.GetComponentInChildren<Camera>() : null;

        if (cam == null)
        {
            Debug.LogError("[상승] 플레이어 카메라를 찾을 수 없다 — 캡처 중단");
            return;
        }

        // 결과판을 채운 뒤에 찍는다. 빈 판을 찍으면 "3×3이 읽히는가"를 판정할 수 없고,
        // 실제로 플레이어가 보게 되는 화면도 아니다. 시드를 고정해 매번 같은 판이 나오게 한다.
        var view = Object.FindAnyObjectByType<Ascend.Prototype.View.SpinBoardView>();
        if (view != null)
        {
            var rules = Ascend.Prototype.Spin.PrototypeCurriculum.BuildRules(
                Ascend.Prototype.Spin.PrototypeCurriculum.For(7));
            var engine = new Ascend.Prototype.Spin.SpinEngine(20260729);
            var spin = engine.Spin(rules,
                Ascend.Prototype.Spin.PrototypeCurriculum.AbsorberContract,
                Ascend.Prototype.Spin.ResidualState.Empty);
            view.ShowBoard(spin.InitialBoard);
            log.AppendLine($"  결과판: {spin.InitialBoard}");
        }
        else log.AppendLine("  WARN  SpinBoardView 없음 — 빈 판으로 촬영");

        string root = Path.Combine(Directory.GetCurrentDirectory(), "Captures", "eyelevel");
        Directory.CreateDirectory(root);

        Vector3 eye = head.position;

        // 플레이어가 실제로 서 있는 자리에서 둘러본 방향들. 인위적인 예쁜 각도가 아니라
        // 게임 중 실제로 보게 되는 시야여야 판정이 의미가 있다.
        (string name, Vector3 pos, Vector3 look)[] shots =
        {
            ("00_spawn_forward",  eye, eye + player.forward * 3f),
            ("01_tubes",          eye, new Vector3(-1.45f, 1.45f,  0.00f)),
            ("02_panel",          eye, new Vector3(-0.80f, 1.55f,  1.45f)),
            ("03_lever_console",  eye, new Vector3(-1.05f, 1.10f, -0.55f)),
            ("04_contract_panel", eye, new Vector3( 1.56f, 1.50f,  0.30f)),
            ("05_door",           eye, new Vector3( 0.65f, 1.20f,  1.60f)),
        };

        foreach (var s in shots)
        {
            cam.transform.position = s.pos;
            cam.transform.rotation = Quaternion.LookRotation((s.look - s.pos).normalized, Vector3.up);
            string path = Path.Combine(root, s.name + ".png");
            Capture(cam, path);
            log.AppendLine($"  {s.name}.png");
        }

        // 공간 전체를 이해할 수 있는 한 장. 사람 크기 대비 방 크기를 판정하려면 필요하다.
        cam.transform.position = new Vector3(1.45f, 2.15f, -2.20f);
        cam.transform.rotation = Quaternion.LookRotation(
            (new Vector3(-0.4f, 1.1f, 0.6f) - cam.transform.position).normalized, Vector3.up);
        Capture(cam, Path.Combine(root, "06_overview.png"));
        log.AppendLine("  06_overview.png");

        log.AppendLine($"  → {root}");
        Debug.Log(log.ToString());
    }

    private static void Capture(Camera cam, string path)
    {
        var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 4
        };
        RenderTexture previous = cam.targetTexture;
        RenderTexture active = RenderTexture.active;

        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        tex.Apply();

        File.WriteAllBytes(path, tex.EncodeToPNG());

        cam.targetTexture = previous;
        RenderTexture.active = active;
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
    }
}
