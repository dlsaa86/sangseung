using System.Linq;
using System.Text;
using Ascend.Prototype.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ascend.CaptureHarness.EditorTools
{
    /// <summary>
    /// AD47 의 2단 중앙개폐 문을 게임 코드에 연결한다.
    ///
    /// ## 축이 다르다
    ///
    /// <c>ElevatorGrayboxView</c> 는 문을 **로컬 X** 로 민다
    /// (<c>p.x = -_doorSlide * amount</c>). AD47 의 문은 블렌더에서 로컬 Y 로
    /// 저작됐고 FBX 축 변환(<c>axis_up='Y'</c>)을 거치며 **월드 Z** 가 됐다.
    /// 그대로 배선하면 문이 벽을 뚫고 옆으로 간다.
    ///
    /// ## 왜 피벗을 못 끼우나 — 첫 시도가 여기서 막혔다
    ///
    /// 보통은 회전된 피벗 밑으로 문짝을 옮겨 축을 맞춘다. AD47 캐빈은 FBX
    /// **프리팹 인스턴스**라 그 안의 트랜스폼을 다른 부모로 옮길 수 없다.
    /// 프리팹을 풀면 되지만 그러면 FBX 링크가 끊겨 블렌더에서 다시 구운 메시가
    /// 씬에 반영되지 않는다 — 이 프로젝트는 캐빈을 계속 다시 굽는다.
    ///
    /// 그래서 뷰가 미는 대상은 **빈 프록시**로 두고
    /// <see cref="DoorAxisAdapter"/> 가 그 이동량을 월드 축으로 옮겨 준다.
    /// 뷰도 프리팹도 건드리지 않는다.
    ///
    /// ## 이동량
    ///
    /// `AD_DOOR_RIG` 계약: 닫힘 ∓0.500 / 열림 ∓1.500 — 편도 1.000m.
    /// 유니티 실측 문짝 중심은 닫힘 z ∓0.502. 뷰의 기본 <c>_doorSlide</c> 1.15 를
    /// 그대로 두면 포켓 깊이를 150mm 넘어가므로 1.0 으로 맞춘다.
    /// </summary>
    internal static class AscendAD47Doors
    {
        [MenuItem("Ascend/Cabin/6. AD47 문 개폐 배선")]
        public static void Wire()
        {
            var log = new StringBuilder("[상승] AD47 문 개폐 배선\n");
            var cab = GameObject.Find("CabinAD47");
            if (cab == null) { Debug.LogError("[상승] CabinAD47 없음"); return; }

            var view = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                             .FirstOrDefault(m => m != null && m.GetType().Name == "ElevatorGrayboxView");
            if (view == null) { Debug.LogError("[상승] ElevatorGrayboxView 없음"); return; }

            var leafL = cab.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "SM_Door_L");
            var leafR = cab.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "SM_Door_R");
            if (leafL == null || leafR == null) { Debug.LogError("[상승] SM_Door_L/R 을 못 찾았다"); return; }

            // 문짝을 닫힘 자세로 되돌린다 — 이전 실행이 중간에서 멈췄을 수 있다
            var rig = GameObject.Find("AD47_DoorRig");
            if (rig != null) Object.DestroyImmediate(rig);
            rig = new GameObject("AD47_DoorRig");
            rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var driverL = new GameObject("DoorDriver_L");
            var driverR = new GameObject("DoorDriver_R");
            driverL.transform.SetParent(rig.transform, false);
            driverR.transform.SetParent(rig.transform, false);

            var adapterL = rig.AddComponent<DoorAxisAdapter>();
            var adapterR = rig.AddComponent<DoorAxisAdapter>();
            // 왼쪽 문은 −Z 로, 오른쪽 문은 +Z 로 물러난다 (닫힘 z ∓0.502 → 열림 ∓1.502)
            adapterL.Configure(driverL.transform, leafL, Vector3.back,    1f, 1f);
            adapterR.Configure(driverR.transform, leafR, Vector3.forward, 1f, 1f);
            log.AppendLine("  AD47_DoorRig 생성 — 드라이버 2, 어댑터 2");
            log.AppendLine("  왼쪽 문 닫힘 " + leafL.position.ToString("F3") + " → −Z 로 1.000m");
            log.AppendLine("  오른쪽   닫힘 " + leafR.position.ToString("F3") + " → +Z 로 1.000m");

            var so = new SerializedObject(view);
            so.FindProperty("_doorLeft").objectReferenceValue = driverL.transform;
            so.FindProperty("_doorRight").objectReferenceValue = driverR.transform;
            var slide = so.FindProperty("_doorSlide");
            float wasSlide = slide.floatValue;
            slide.floatValue = 1.0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
            log.AppendLine("  _doorLeft/_doorRight → 드라이버,  _doorSlide " + wasSlide.ToString("F2") + " → 1.00");

            var oldDoor = GameObject.Find("GrayboxWorld/Car/Door");
            if (oldDoor != null && oldDoor.activeSelf)
            {
                oldDoor.SetActive(false);
                log.AppendLine("  off  GrayboxWorld/Car/Door");
            }

            // ── 검증: 실제로 열어 보고 닫아 본다 ───────────────────────────
            // 「배선했다」가 아니라 「움직인다」를 증거로 남긴다.
            var closedL = leafL.position; var closedR = leafR.position;

            Drive(driverL.transform, driverR.transform, 1f);
            adapterL.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
            adapterR.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
            var openL = leafL.position; var openR = leafR.position;

            Drive(driverL.transform, driverR.transform, 0f);
            adapterL.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
            adapterR.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
            var backL = leafL.position; var backR = leafR.position;

            log.AppendLine(string.Format("\n  왼쪽  닫힘 z={0:F3} → 열림 z={1:F3}  (이동 {2:F3}m)",
                closedL.z, openL.z, Vector3.Distance(closedL, openL)));
            log.AppendLine(string.Format("  오른쪽 닫힘 z={0:F3} → 열림 z={1:F3}  (이동 {2:F3}m)",
                closedR.z, openR.z, Vector3.Distance(closedR, openR)));
            log.AppendLine(string.Format("  복귀 오차  L {0:F5}m   R {1:F5}m",
                Vector3.Distance(closedL, backL), Vector3.Distance(closedR, backR)));

            bool apart = Mathf.Abs(openL.z - openR.z) > Mathf.Abs(closedL.z - closedR.z) + 1.5f;
            bool zOnly = Mathf.Abs(openL.x - closedL.x) < 0.001f && Mathf.Abs(openL.y - closedL.y) < 0.001f;
            log.AppendLine("  서로 반대로 열린다: " + (apart ? "예" : "⚠ 아니오"));
            log.AppendLine("  이동이 Z 전용이다: " + (zOnly ? "예" : "⚠ 아니오"));

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log(log.ToString());
        }

        /// <summary>뷰가 하는 것과 같은 방식으로 드라이버를 민다 — 검증이 실제 경로를 통과하도록.</summary>
        private static void Drive(Transform l, Transform r, float amount)
        {
            var pl = l.localPosition; pl.x = -1.0f * amount; l.localPosition = pl;
            var pr = r.localPosition; pr.x =  1.0f * amount; r.localPosition = pr;
        }
    }
}
