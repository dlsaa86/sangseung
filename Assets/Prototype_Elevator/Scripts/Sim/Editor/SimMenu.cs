using UnityEditor;
using UnityEngine;

namespace Ascend.Prototype
{
    public static class SimMenu
    {
        private const int DefaultRunCount = 1000;

        [MenuItem("Ascend/Run Balance Sim")]
        public static void RunBalanceSimulation()
        {
            string report = RunSimulator.RunHeadless(DefaultRunCount);
            Debug.Log(report);
        }
    }
}
