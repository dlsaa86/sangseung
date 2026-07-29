using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 계층을 컴포넌트·위치·크기와 함께 덤프한다. 1인칭 리그와 상호작용 대상을 어디에
/// 놓을지 정하려면 좌표를 알아야 하는데, .unity YAML을 눈으로 읽어서 알아내는 것은
/// 느리고 틀리기 쉽다.
/// </summary>
public static class SceneSurvey
{
    private const string ScenePath = "Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity";

    [MenuItem("Ascend/Survey Scene")]
    public static void Run()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var sb = new StringBuilder(8192);
        sb.AppendLine("[상승] === 씬 조사 ===");

        foreach (GameObject root in scene.GetRootGameObjects())
            Dump(root.transform, 0, sb);

        Debug.Log(sb.ToString());
    }

    private static void Dump(Transform t, int depth, StringBuilder sb)
    {
        GameObject go = t.gameObject;
        string indent = new string(' ', depth * 2);

        var renderer = go.GetComponent<Renderer>();
        var collider = go.GetComponent<Collider>();
        var cam = go.GetComponent<Camera>();

        string tags = string.Empty;
        if (renderer != null)
        {
            Bounds b = renderer.bounds;
            tags += $"  mesh(size {b.size.x:F2}×{b.size.y:F2}×{b.size.z:F2})";
        }
        if (collider != null) tags += "  COLLIDER";
        if (cam != null) tags += "  CAMERA";
        if (go.GetComponent<Canvas>() != null) tags += "  canvas";
        if (!go.activeSelf) tags += "  [비활성]";

        Vector3 p = t.position;
        sb.AppendLine($"{indent}{go.name}  @({p.x:F2}, {p.y:F2}, {p.z:F2}){tags}");

        for (int i = 0; i < t.childCount; i++)
            Dump(t.GetChild(i), depth + 1, sb);
    }
}
