using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ascend.CaptureHarness.EditorTools
{
    /// <summary>
    /// AD47 캐빈을 **플레이 가능한** 상태로 만든다 — 충돌과 조준.
    ///
    /// ## 1. 캐빈에 콜라이더가 하나도 없었다
    ///
    /// FBX 임포트는 콜라이더를 만들지 않는다. 그래서 플레이어를 막고 있던 것은
    /// 옛 <c>GrayboxWorld/Car</c> 의 벽(x ±2.50)이었고, AD47 의 벽 안쪽 면은
    /// ±2.09 다. **차이 410mm 만큼 플레이어가 새 벽 속으로 걸어 들어간다.**
    /// 화면에는 벽을 뚫고 들어간 것으로 보인다.
    ///
    /// 메시 콜라이더를 쓰지 않는 이유: AD47 의 뒷벽에는 기계 개구부가 뚫려 있어
    /// 메시 콜라이더면 플레이어가 그 벽감 **안으로** 들어간다. 방의 경계는
    /// 형상이 아니라 「여기까지가 방이다」라는 판단이므로 상자 여섯 개로 둔다.
    ///
    /// ## 2. 보이는 레버와 눌리는 레버가 달랐다
    ///
    /// 크로스헤어는 콜라이더를 맞힌다. <c>InteractableLever</c> 는
    /// <c>GrayboxWorld/Car/Console/ExecutionLever</c> 의 상자에 붙어 있고 그 상자는
    /// (1.27, 1.83, 2.14) 에 있는데, **눈에 보이는 레버**는 AD47 의 레버 베이
    /// (0.714, 1.262, 2.163) 와 기존 방의 애니메이션 레버 (0.758, ~1.2, ~1.95) 다.
    /// 750mm 어긋나 있다 — 플레이어는 보이는 레버를 조준하고 아무 일도 안 일어난다.
    ///
    /// 상자를 **보이는 레버 위로** 옮긴다. 반대로 레버를 상자로 옮기지 않는 이유는
    /// AD47 의 베이 위치가 모델링된 벽 개구부와 물려 있어서다.
    ///
    /// ## 3. 레버가 둘이었다
    ///
    /// AD47 도 레버를 갖고 있고 기존 방도 갖고 있다. 둘 다 켜면 겹친다.
    /// **기존 방 것을 남긴다** — <c>LeverStateMachine</c> 이 그 트랜스폼을 축으로
    /// 저작돼 있어(스윙 55°) 실제로 움직이는 쪽이기 때문이다. AD47 의 정적 손잡이
    /// 두 개만 끄고 베이(<c>SM_LeverBay</c>)는 남긴다 — 움직이는 손잡이가 그 함몰
    /// 안에 앉으면 구도가 오히려 맞는다.
    ///
    /// 대신 기존 레버의 재질을 AD47 것으로 바꿔 값 대역을 맞춘다.
    /// </summary>
    internal static class AscendAD47Interaction
    {
        private const string MatDir = "Assets/Prototype_Elevator/Materials/CabinAD47";

        // AD47 내부 치수 (렌더러 실측, 2026-08-08)
        private const float InnerX = 2.090f;   // 좌우 벽 안쪽 면
        private const float InnerZ = 2.090f;   // 앞뒤 벽 안쪽 면
        private const float FloorY = 0.000f;
        private const float CeilY = 3.000f;
        private const float Thick = 0.30f;     // 콜라이더 두께 — 얇으면 빠르게 걸을 때 통과한다

        [MenuItem("Ascend/Cabin/4. AD47 충돌·조작 정렬")]
        public static void Wire()
        {
            var log = new StringBuilder("[상승] AD47 충돌·조작 정렬\n");
            var cab = GameObject.Find("CabinAD47");
            if (cab == null) { Debug.LogError("[상승] CabinAD47 없음"); return; }

            // ── 1. 방 경계 상자 여섯 개 ───────────────────────────────────
            var shellT = cab.transform.Find("ShellCollision");
            if (shellT != null) Object.DestroyImmediate(shellT.gameObject);
            var shell = new GameObject("ShellCollision");
            shell.transform.SetParent(cab.transform, false);

            AddBox(shell.transform, "Col_Floor",   new Vector3(0f, FloorY - Thick * 0.5f, 0f), new Vector3(InnerX * 2f, Thick, InnerZ * 2f));
            AddBox(shell.transform, "Col_Ceiling", new Vector3(0f, CeilY + Thick * 0.5f, 0f),  new Vector3(InnerX * 2f, Thick, InnerZ * 2f));
            AddBox(shell.transform, "Col_Wall_Xn", new Vector3(-InnerX - Thick * 0.5f, CeilY * 0.5f, 0f), new Vector3(Thick, CeilY, InnerZ * 2f));
            AddBox(shell.transform, "Col_Wall_Xp", new Vector3( InnerX + Thick * 0.5f, CeilY * 0.5f, 0f), new Vector3(Thick, CeilY, InnerZ * 2f));
            AddBox(shell.transform, "Col_Wall_Zn", new Vector3(0f, CeilY * 0.5f, -InnerZ - Thick * 0.5f), new Vector3(InnerX * 2f, CeilY, Thick));
            AddBox(shell.transform, "Col_Wall_Zp", new Vector3(0f, CeilY * 0.5f,  InnerZ + Thick * 0.5f), new Vector3(InnerX * 2f, CeilY, Thick));
            log.AppendLine("  방 경계 상자 6개 (안쪽 면 ±" + InnerX.ToString("F3") + "m, 천장 " + CeilY.ToString("F2") + "m)");

            // ── 2. AD47 의 중복 레버 손잡이를 끈다 ────────────────────────
            foreach (var n in new[] { "SM_Lever_Handle.001", "SM_Lever_Handle.003" })
            {
                var t = cab.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == n);
                if (t != null && t.gameObject.activeSelf)
                {
                    t.gameObject.SetActive(false);
                    log.AppendLine("  off  " + n + "  (움직이는 쪽은 ReferenceRoom 레버다)");
                }
            }

            // ── 3. 기존 레버를 AD47 재질로 다시 입힌다 ────────────────────
            var leverIron = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/M_Elev_LeverIron.mat");
            var leverPlate = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/M_Elev_LeverPlate.mat");
            var leverBase = GameObject.Find("ReferenceRoom/ExecutionLeverBase");
            Transform handle = null;
            if (leverBase != null && leverIron != null)
            {
                int n = 0;
                foreach (var r in leverBase.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                        mats[i] = r.name.Contains("Handle") ? leverIron : (leverPlate ?? leverIron);
                    r.sharedMaterials = mats;
                    EditorUtility.SetDirty(r);
                    n++;
                }
                handle = leverBase.GetComponentsInChildren<Transform>(true)
                                  .FirstOrDefault(t => t.name.Contains("Handle"));
                log.AppendLine("  기존 레버 렌더러 " + n + "개를 AD47 재질로 교체");
            }

            // ── 4. 손잡이를 눈에 띄게 ─────────────────────────────────────
            // 굽기 실측값(0.0199 × 5 = 0.0995)은 단일 점광 실내에서 순검정으로 떨어져
            // **조작 가능한 것이 검은 판으로 읽힌다.** 손잡이는 「손이 닿아 닳은 금속」이니
            // 올려도 노후화 서사와 어긋나지 않는다. 본체 판은 어둡게 둬서 손잡이를 액자에 넣는다.
            if (leverIron != null)
            {
                leverIron.SetColor("_BaseColor", new Color(0.340f, 0.300f, 0.215f, 1f));
                leverIron.SetFloat("_RimStrength", 0.42f);
                EditorUtility.SetDirty(leverIron);
            }
            if (leverPlate != null)
            {
                leverPlate.SetColor("_BaseColor", new Color(0.150f, 0.143f, 0.132f, 1f));
                EditorUtility.SetDirty(leverPlate);
            }
            AssetDatabase.SaveAssets();

            // ── 5. 조준 프록시 ────────────────────────────────────────────
            // 원본 조작 상자를 **옮기지 않는다.** 옮겨도 되지만 원본은 `Console` 하위이고
            // 계층이 얽혀 있어 되돌리기가 어렵다. 대신 자식으로 프록시 콜라이더를 단다 —
            // `CrosshairInteractor` 는 맞은 콜라이더에서 `GetComponentInParent<IInteractable>()`
            // 로 대상을 찾으므로 자식 콜라이더도 같은 레버로 해석된다.
            //
            // 두 레버를 **세로로 가른다.** 과수확 레버의 `Housing` 콜라이더가
            // (0.758, 1.250, 1.890) 크기 (0.60, 0.84, 0.52) 로 실행 레버 자리를 통째로
            // 덮고 있어서, 플레이어가 실행 레버를 조준하면 과수확이 먼저 잡혔다.
            // 아래 = 실행(매 층 쓴다), 위 = 과수확(의도적으로 손을 뻗어야 한다).
            var exec = GameObject.Find("GrayboxWorld/Car/Console/ExecutionLever");
            var over = GameObject.Find("GrayboxWorld/Car/OverharvestLever");
            AddProxy(exec, "AimProxy_Exec", new Vector3(0.714f, 1.06f, 2.02f), new Vector3(0.44f, 0.52f, 0.34f), log);
            AddProxy(over, "AimProxy_Over", new Vector3(0.714f, 1.80f, 2.02f), new Vector3(0.44f, 0.40f, 0.34f), log);
            DisableOriginalColliders(exec, log);
            DisableOriginalColliders(over, log);

            // 움직이는 손잡이를 실행 레버 자리로 옮긴다
            if (leverBase != null)
            {
                var rs = leverBase.GetComponentsInChildren<Renderer>(true);
                if (rs.Length > 0)
                {
                    var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
                    var delta = new Vector3(0.714f, 1.10f, 2.03f) - bb.center;
                    leverBase.transform.position += delta;
                    log.AppendLine("  기존 레버 본체 이동 " + delta.ToString("F3"));
                }
            }

            // ⚠ 이게 없으면 아래 검증이 **옛 위치를 본다.** `Physics.autoSyncTransforms` 는
            // 기본이 false 라 트랜스폼을 옮겨도 콜라이더 바운드와 질의는 갱신되지 않는다.
            // 2026-08-08 — 이것 때문에 「레버를 옮겼는데 여전히 가린다」로 두 턴을 썼다.
            Physics.SyncTransforms();

            // ── 6. 검증: 다섯 조작이 전부 조준되는가 ──────────────────────
            var player = GameObject.Find("Player");
            var eye = player != null ? player.transform.position + Vector3.up * 1.62f : new Vector3(0f, 1.62f, -1.70f);
            int okCount = 0, total = 0;
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (mb == null || !(mb is Ascend.Prototype.Player.IInteractable)) continue;
                total++;
                var proxy = mb.transform.GetComponentsInChildren<Transform>(true)
                                        .FirstOrDefault(t => t.name.StartsWith("AimProxy"));
                var aimAt = proxy != null ? proxy.position : mb.transform.position;
                var hits = Physics.SphereCastAll(eye, 0.18f, (aimAt - eye).normalized, 5f, ~0, QueryTriggerInteraction.Collide)
                                  .OrderBy(h => h.distance).ToArray();
                var first = hits.Select(h => h.collider.GetComponentInParent<Ascend.Prototype.Player.IInteractable>())
                                .FirstOrDefault(x => x != null);
                bool ok = first != null && ReferenceEquals(first, mb);
                if (ok) okCount++;
                else log.AppendLine("  ⚠ 조준 실패: " + mb.name + (first == null ? " (아무것도 안 잡힘)" : " (가림: " + ((MonoBehaviour)first).name + ")"));
            }
            log.AppendLine("  조준 검증: " + okCount + "/" + total + " 통과");

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log(log.ToString());
        }

        private static void AddProxy(GameObject owner, string name, Vector3 worldPos, Vector3 size, StringBuilder log)
        {
            if (owner == null) { log.AppendLine("  ⚠ " + name + " 의 소유자를 못 찾았다"); return; }
            var old = owner.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject(name);
            go.transform.SetParent(owner.transform, false);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.AddComponent<BoxCollider>().size = size;
            log.AppendLine("  프록시 " + name + " @ " + worldPos.ToString("F3"));
        }

        /// <summary>옛 자리에 남은 콜라이더는 조준을 가로챈다. 지우지 않고 끈다 — 되돌리기 쉽게.</summary>
        private static void DisableOriginalColliders(GameObject owner, StringBuilder log)
        {
            if (owner == null) return;
            foreach (var c in owner.GetComponentsInChildren<Collider>(true))
            {
                if (c.name.StartsWith("AimProxy")) continue;
                if (!c.enabled) continue;
                c.enabled = false;
                EditorUtility.SetDirty(c);
                log.AppendLine("  off 콜라이더 " + c.name);
            }
        }

        private static void AddBox(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            var c = go.AddComponent<BoxCollider>();
            c.size = size;
        }
    }
}
