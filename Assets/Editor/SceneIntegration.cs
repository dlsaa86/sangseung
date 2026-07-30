using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ascend.Prototype.Run;
using Ascend.Prototype.UI;

/// <summary>
/// 씬을 새 자동 룰렛 루프로 전환한다. 배치 모드에서 -executeMethod 로 돌릴 수 있게
/// 정적 메서드로 노출한다 — MCP 브리지가 끊겨도 씬 작업이 막히지 않아야 한다.
///
/// 파괴 대신 비활성화를 택했다. .unity 는 fileID 로 상호 참조하는 YAML이라 지우면
/// 되돌리기가 어렵고, 지금 필요한 것은 "옛 프로토타입이 화면에서 사라지는 것"이지
/// "옛 오브젝트가 파일에서 없어지는 것"이 아니다. 방향이 확정되면 그때 지운다.
///
/// 여러 번 실행해도 결과가 같아야 한다(멱등). 통합 도중 실패해서 다시 돌릴 일이 생긴다.
/// </summary>
public static class SceneIntegration
{
    private const string ScenePath = "Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity";
    private const string HostName  = "AscendRun";

    [MenuItem("Ascend/Integrate Scene — 자동 룰렛")]
    public static void Run()
    {
        string report = RunToString();
        Debug.Log(report);
    }

    public static string RunToString()
    {
        var log = new StringBuilder();
        log.AppendLine("[상승] === 씬 통합 ===");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            log.AppendLine($"  FAIL  씬을 열 수 없다: {ScenePath}");
            return log.ToString();
        }

        List<GameObject> roots = new List<GameObject>(scene.GetRootGameObjects());

        // ── 1. 옛 루프를 끈다 ────────────────────────────────────────────
        // GameSystems 가 RunController 를 들고 매 프레임 키보드를 폴링한다. 새 HUD 도
        // 같은 키를 읽으므로 켜둔 채로는 두 상태 기계가 같은 입력에 동시에 반응한다.
        int disabledSystems = 0;
        foreach (GameObject root in roots)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = t.gameObject;
                if (go.name != "GameSystems") continue;
                if (go.activeSelf)
                {
                    Undo.RecordObject(go, "Disable legacy GameSystems");
                    go.SetActive(false);
                    disabledSystems++;
                }
            }
        }
        log.AppendLine($"  옛 GameSystems 비활성화: {disabledSystems}개");

        // ── 2. 폐기된 타이밍 조작부를 숨긴다 ──────────────────────────────
        // 노션이 명시적으로 버린 축이다. 화면에 남아 있으면 플레이어에게 "누르라"고
        // 말하는 것과 같다.
        int hiddenTiming = 0;
        foreach (GameObject root in roots)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = t.gameObject;
                if (!go.name.StartsWith("StopButton") && !go.name.StartsWith("ButtonPivot")) continue;
                if (go.activeSelf)
                {
                    Undo.RecordObject(go, "Hide deprecated timing controls");
                    go.SetActive(false);
                    hiddenTiming++;
                }
            }
        }
        log.AppendLine($"  타이밍 정지 조작부 숨김: {hiddenTiming}개");

        // ── 3. 새 루프 호스트 ────────────────────────────────────────────
        GameObject host = null;
        foreach (GameObject root in roots)
            if (root.name == HostName) { host = root; break; }

        if (host == null)
        {
            host = new GameObject(HostName);
            Undo.RegisterCreatedObjectUndo(host, "Create AscendRun host");
            log.AppendLine($"  {HostName} 생성");
        }
        else log.AppendLine($"  {HostName} 이미 존재 — 재사용");

        if (host.GetComponent<RunSessionBehaviour>() == null)
        {
            host.AddComponent<RunSessionBehaviour>();
            log.AppendLine("  RunSessionBehaviour 부착");
        }
        if (host.GetComponent<GameHudView>() == null)
        {
            // 화면 UI는 Canvas 계층까지 필요하므로 여기서 부착하지 않는다.
            // Ascend/Build Hero Slice Scene Objects 가 만든다.
            log.AppendLine("  화면 UI 없음 — Ascend/Build Hero Slice Scene Objects 실행 필요");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        log.AppendLine(saved ? "  씬 저장 완료" : "  FAIL  씬 저장 실패");

        // ── 4. 검증 ──────────────────────────────────────────────────────
        var check = new StringBuilder();
        int problems = 0;
        if (host.GetComponent<RunSessionBehaviour>() == null) { check.AppendLine("    RunSessionBehaviour 없음"); problems++; }
        if (Object.FindAnyObjectByType<GameHudView>() == null) { check.AppendLine("    GameHudView 없음"); problems++; }
        if (!saved)                                            { check.AppendLine("    씬이 저장되지 않음"); problems++; }

        log.AppendLine();
        log.AppendLine(problems == 0
            ? "  결과: OK — [Space] 스핀 / [B] 확정 / [P] 추가 스핀 / [R] 재시작"
            : $"  결과: 문제 {problems}건\n{check}");
        return log.ToString();
    }
}
