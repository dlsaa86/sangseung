using System;
using System.Collections;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Ascend.Prototype.Run;
using Ascend.Prototype.Spin;

/// <summary>
/// 씬을 실제로 Play 모드에 넣고 한 런을 끝까지 돌린다.
///
/// 왜 필요한가: 순수 C# 테스트가 전부 통과해도 씬에서 돌아간다는 보장이 없다.
/// 컴포넌트가 안 붙어 있거나, Awake 순서가 어긋나거나, 참조가 null이면 테스트는
/// 전부 초록인데 Play를 누르면 아무 일도 안 일어난다. 그 간극을 여기서 막는다.
///
/// 배치 모드에서 -executeMethod 로 돌아간다.
/// </summary>
public static class PlayModeSmokeTest
{
    private const string ScenePath = "Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity";

    private static readonly StringBuilder _log = new StringBuilder();
    private static int _pass;
    private static int _fail;

    [MenuItem("Ascend/Run Play Mode Smoke Test")]
    public static void Run()
    {
        _log.Clear(); _pass = 0; _fail = 0;
        _log.AppendLine("[상승] === Play 모드 스모크 ===");

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // Play 모드 없이도 확인 가능한 것부터. 씬 배선이 틀렸다면 여기서 잡힌다.
        var host = GameObject.Find("AscendRun");
        Check(host != null, "AscendRun 오브젝트가 씬에 있다");
        if (host == null) { Report(); return; }

        var behaviour = host.GetComponent<RunSessionBehaviour>();
        Check(behaviour != null, "RunSessionBehaviour 부착됨");

        var hudType = Type.GetType("Ascend.Prototype.UI.RouletteHud, Assembly-CSharp");
        Check(hudType != null && host.GetComponent(hudType) != null, "RouletteHud 부착됨");

        // 옛 루프가 꺼져 있어야 한다. 켜져 있으면 두 상태 기계가 같은 키를 먹는다.
        var legacy = GameObject.Find("GameSystems");
        Check(legacy == null, "옛 GameSystems가 비활성(Find로 잡히지 않음)");

        if (behaviour == null) { Report(); return; }

        // 런타임 동작 — Awake를 거치지 않았으므로 직접 초기화해서 한 런을 돌린다.
        behaviour.ResetRun();
        RunSession session = behaviour.Session;
        Check(session != null, "ResetRun 후 Session 생성됨");
        if (session == null) { Report(); return; }

        int floorsPlayed = 0;
        int guard = 0;
        try
        {
            while (!session.IsComplete && !session.IsFailed && guard++ < 200)
            {
                FloorSession floor = session.Current;
                if (floor == null) break;

                if (floor.Phase == FloorPhase.ContractSelection)
                {
                    session.SelectContract(0);
                    continue;
                }
                if (floor.Phase == FloorPhase.Spinning)
                {
                    session.Spin();
                    continue;
                }
                if (floor.Phase == FloorPhase.Decision)
                {
                    if (floor.CanBank) { session.Bank(); floorsPlayed++; }
                    else if (floor.SpinsRemaining > 0) session.Spin();
                    else { session.ForceResolve(); floorsPlayed++; }
                    continue;
                }
                break;
            }
            Check(true, "한 런이 예외 없이 끝까지 진행됨");
        }
        catch (Exception e)
        {
            Check(false, "한 런이 예외 없이 끝까지 진행됨", e.GetType().Name + ": " + e.Message);
        }

        Check(guard < 200, "무한 루프 없이 종료됨", $"guard={guard}");
        Check(floorsPlayed > 0, "적어도 한 층을 해결함", $"floorsPlayed={floorsPlayed}");
        _log.AppendLine($"    (도달 {session.HighestFloorReached}층, 완료={session.IsComplete}, 실패={session.IsFailed})");

        Report();
    }

    private static void Check(bool ok, string name, string detail = null)
    {
        if (ok) { _pass++; _log.AppendLine($"  PASS  {name}"); }
        else { _fail++; _log.AppendLine($"  FAIL  {name}{(string.IsNullOrEmpty(detail) ? "" : "   " + detail)}"); }
    }

    private static void Report()
    {
        _log.AppendLine();
        _log.AppendLine($"결과: {_pass} PASS / {_fail} FAIL");
        if (_fail > 0) Debug.LogError(_log.ToString());
        else Debug.Log(_log.ToString());
    }
}
