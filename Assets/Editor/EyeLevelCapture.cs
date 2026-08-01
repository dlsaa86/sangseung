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
        // 좌표는 2026-07-31 비례 재조정 이후 기준이다(내부 x[-1.20..1.20] · z[-1.50..1.50] ·
        // 높이 3.20). 이전 좌표는 통관을 x=-1.45, 계기판을 x=1.56에서 찾았는데, 그 자리는
        // 지금 벽 **안쪽**이라 카메라가 벽면만 보고 있었다.
        (string name, Vector3 pos, Vector3 look)[] shots =
        {
            ("00_spawn_forward",  eye, eye + player.forward * 3f),
            ("01_tubes",          eye, new Vector3(-0.88f, 1.55f,  0.00f)),
            ("02_panel",          eye, new Vector3(-0.60f, 1.55f,  1.45f)),
            ("03_lever_console",  eye, new Vector3(-0.85f, 1.10f, -0.55f)),
            ("04_contract_panel", eye, new Vector3( 1.12f, 1.50f,  0.30f)),
            ("05_door",           eye, new Vector3( 0.65f, 1.20f,  1.60f)),
            // 요구 캡처 "엘리베이터 입구에서 본 전체 내부". 문지방에 선 시야다.
            // 승강장(z=2.6)까지 물러나면 어두운 복도와 뒷벽 바깥면만 찍힌다 — 실제로
            // 그렇게 어둡게 만든 것이 맞지만(§4 "어두운 외부 복도"), 그 장면은
            // "내부 전체"를 보여주지 못해 요구 캡처의 뜻을 잃는다.
            ("07_entry",          new Vector3(0.65f, 1.62f, 1.35f), new Vector3(-0.70f, 1.35f, -0.90f)),
            // 층수 표시등은 출입구 위에 있다. 고개를 든 시야가 따로 필요하다.
            ("08_floor_sign",     eye, new Vector3( 0.65f, 2.26f,  1.45f)),
            // 사고 기록기는 정면 벽(z=-1.43)에 있다. 위 시야 어느 것도 그 벽을 보지 않아
            // "입구에서 들어와 보이는가"를 판정할 그림이 없었다. 문지방에서 정면을 본다.
            ("09_accident_printer", new Vector3(0.65f, 1.62f, 1.10f), new Vector3(0.55f, 2.02f, -1.43f)),
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
        // 앞벽이 생긴 뒤로는 밖에서 들여다볼 수 없다 — 안쪽 앞모서리 위에서 내려다본다.
        cam.transform.position = new Vector3(0.95f, 2.60f, -1.30f);
        cam.transform.rotation = Quaternion.LookRotation(
            (new Vector3(-0.55f, 1.25f, 0.55f) - cam.transform.position).normalized, Vector3.up);
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
