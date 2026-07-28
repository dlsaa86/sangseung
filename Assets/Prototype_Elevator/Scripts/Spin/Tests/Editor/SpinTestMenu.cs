using UnityEditor;
using UnityEngine;

namespace Ascend.Prototype.Spin.Tests
{
    public static class SpinTestMenu
    {
        [MenuItem("Ascend/Run Spin Tests")]
        public static void RunAll()
        {
            var result = SpinEngineTests.RunAll();
            if (result.failed > 0)
                Debug.LogError(result.report);
            else
                Debug.Log(result.report);
        }
    }
}
