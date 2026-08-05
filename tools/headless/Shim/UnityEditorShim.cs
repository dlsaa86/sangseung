// UnityEditor 최소 shim — 하네스 전용. 에디터 툴(`BalanceSweep` 등)을 **수정 없이**
// 그대로 컴파일해 돌리기 위한 대역이다. 툴을 고쳐서 포팅하면 그 순간 두 갈래가 되고,
// 이 저장소가 반복해서 당한 실패가 정확히 그것이다 — 재는 도구가 다른 게임을 잰다.
using System;

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class MenuItem : Attribute
    {
        public MenuItem(string path) { }
        public MenuItem(string path, bool validate) { }
        public MenuItem(string path, bool validate, int priority) { }
    }

    public static class EditorUtility
    {
        public static void DisplayProgressBar(string t, string i, float p) { }
        public static void ClearProgressBar() { }
        public static bool DisplayDialog(string t, string m, string ok) => true;
        public static void SetDirty(UnityEngine.Object o) { }
    }

    public static class AssetDatabase
    {
        public static void Refresh() { }
        public static void SaveAssets() { }
        public static string[] FindAssets(string filter, string[] folders = null) => Array.Empty<string>();
        public static string GUIDToAssetPath(string guid) => string.Empty;
        public static T LoadAssetAtPath<T>(string path) where T : class => null;
        public static void CreateAsset(UnityEngine.Object o, string path) { }
    }
}
