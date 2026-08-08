using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ascend.CaptureHarness.EditorTools
{
    /// <summary>
    /// 3×3 결과판을 옛 <c>ReferenceRoom/SoulMachineFrame</c> 에서 AD47 기계로 옮긴다.
    ///
    /// ## 왜 옮기나
    ///
    /// AD47 의 뒷벽에는 기계 개구부가 **이 기계 치수에 맞춰** 뚫려 있다
    /// (x ±1.0965). 옛 기계는 x −1.414..0.758 이라 왼쪽이 벽에 파묻히고
    /// 오른쪽이 뜬다. 둘 다 켜면 겹치고, 옛 것만 켜면 개구부와 어긋난다.
    ///
    /// ## 인덱스 규약을 추측하지 않는 이유
    ///
    /// <c>SpinBoard.Index(col,row) = col*3 + row</c> 이고 주석은 「열 0 = 왼쪽,
    /// 행 0 = 위」라고 한다. 그런데 **누구의 왼쪽인지**는 적혀 있지 않다 — 기계가
    /// 바라보는 방향에 따라 월드 +x 일 수도 −x 일 수도 있다. 한 칸이라도 틀리면
    /// 「가로 3줄이 세로로 터진다」처럼 조용히 어긋나고, 그건 캡처로도 잘 안 보인다.
    ///
    /// 그래서 이 스크립트는 **지금 동작 중인 배선에서 방향을 읽는다.** 옛 셀 9개의
    /// 월드 좌표에 인덱스를 대 보고 열이 +x 로 가는지 −x 로 가는지, 행이 위에서
    /// 아래인지를 판정한 뒤, 같은 규칙을 AD47 챔버에 적용한다. 규약이 바뀌면
    /// 이 스크립트도 따라 바뀐다 — 손으로 맞춘 상수가 아니라 유도된 값이기 때문이다.
    ///
    /// ## 구슬은 그레이박스다
    ///
    /// 2026-08-08 사용자 지시 — 구슬 디자인은 아직 미결정이므로 그레이박스로 둔다.
    /// 다만 무드보드 §8 이 요구하는 **「색 없이 외곽선만으로 구분」** 은 지금 지킨다.
    /// 정상=매끈한 구 · 흡수체=중심이 함몰된 구 · 증식체=마디가 붙은 뭉치.
    /// 최종 디자인이 오면 이 세 프리미티브만 갈아 끼우면 된다.
    /// </summary>
    internal static class AscendAD47Board
    {
        private const string CellRoot = "BoardCells";
        private const string MatDir = "Assets/Prototype_Elevator/Materials/CabinAD47";

        [MenuItem("Ascend/Cabin/3. AD47 3×3 재배선")]
        public static void Rewire()
        {
            var log = new StringBuilder("[상승] AD47 3×3 재배선\n");

            var cab = GameObject.Find("CabinAD47");
            if (cab == null) { Debug.LogError("[상승] CabinAD47 없음"); return; }

            var view = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                             .FirstOrDefault(m => m != null && m.GetType().Name == "SpinBoardView");
            if (view == null) { Debug.LogError("[상승] SpinBoardView 없음"); return; }

            // ── 1. 옛 배선에서 방향을 유도한다 ─────────────────────────────
            var so = new SerializedObject(view);
            var arr = so.FindProperty("_cells");
            var old = new Vector3[9];
            int found = 0;
            for (int i = 0; i < 9 && i < arr.arraySize; i++)
            {
                var t = arr.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (t != null) { old[i] = t.position; found++; }
            }
            if (found != 9)
            {
                Debug.LogError("[상승] 옛 셀이 9개가 아니다 (" + found + ") — 방향을 유도할 수 없다. 중단한다.");
                return;
            }

            // 인덱스 = col*3 + row. col 0..2 는 i/3, row 0..2 는 i%3.
            // 열이 커질 때 x 가 늘면 +1, 줄면 −1. 행이 커질 때 y 가 줄면 위→아래.
            float colDx = (old[6].x - old[0].x);      // col 2 − col 0 (같은 row 0)
            float rowDy = (old[2].y - old[0].y);      // row 2 − row 0 (같은 col 0)
            int colSign = colDx >= 0f ? +1 : -1;
            int rowSign = rowDy >= 0f ? +1 : -1;      // −1 = 행 0 이 위
            log.AppendLine(string.Format("  유도한 규약: 열 증가 → x {0}{1:F3}m,  행 증가 → y {2}{3:F3}m",
                colSign > 0 ? "+" : "-", Mathf.Abs(colDx), rowSign > 0 ? "+" : "-", Mathf.Abs(rowDy)));
            log.AppendLine("  → 행 0 은 " + (rowSign < 0 ? "위" : "아래") + ", 열 0 은 x 가 " + (colSign > 0 ? "작은" : "큰") + " 쪽");

            // ── 2. AD47 챔버 9개를 같은 규칙으로 정렬한다 ──────────────────
            //
            // 🔴 **앵커는 챔버 유리다. `SM_Soul_Core_*` 가 아니다** (2026-08-08 정정).
            //
            // 원래 이 함수는 `SM_Soul_Core_*` 의 바운드 중심을 격자로 썼다. 그 결과
            // 그레이박스 구슬이 **기계 앞 635 mm 허공에 떠 있었다** — 사용자가
            // 「구슬 위치가 기계랑 안 맞다」고 지적한 것이 그것이다.
            //
            // 블렌더 실측(`tools/blender_bridge.py`)이 원인을 확정했다.
            //   · `SM_Chamber_Glass_*` 유리면      Y 2.788 ~ 2.826
            //   · `SM_ChamberArray` 전체 깊이       135 mm (Y 2.795 ~ 2.930)
            //   · 유리 **뒤쪽** 공동                없음 (−7 mm)
            // 즉 635 mm 를 담을 공동이 애초에 존재하지 않는다. 구슬은 벽 뒤에 파묻힌
            // 것이 아니라 **방 쪽으로 튀어나와 떠 있었다.**
            //
            // 게다가 `SM_Soul_Core_*` 는 **현재 블렌더 파일에 더 이상 없다**(AD57 에서
            // 삭제됐다). Unity 의 FBX 가 AD47 판이라 그것만 남아 있는 것이다. 즉 이
            // 앵커는 「지워진 것의 옛 좌표」이고 앞으로도 갱신되지 않는다.
            //
            // 챔버 유리는 살아 있고, 깊이 135 mm 안에 들어가야 하는 것이 구슬이므로
            // 그쪽이 정본이다. 유리가 없으면 옛 앵커로 물러나되 **경고를 남긴다** —
            // 조용히 물러나면 이 사고가 그대로 재현된다.
            var cores = cab.GetComponentsInChildren<Transform>(true)
                           .Where(t => t.name.StartsWith("SM_Chamber_Glass_"))
                           .ToList();
            if (cores.Count == 9)
            {
                log.AppendLine("  앵커: SM_Chamber_Glass_* 9개 (챔버 유리면)");
            }
            else
            {
                var legacy = cab.GetComponentsInChildren<Transform>(true)
                                .Where(t => t.name.StartsWith("SM_Soul_Core_"))
                                .ToList();
                Debug.LogWarning("[상승] SM_Chamber_Glass_* 가 9개가 아니다 (" + cores.Count
                               + ") — SM_Soul_Core_* 로 물러난다. ⚠ 그 앵커는 2026-08-08 에"
                               + " 구슬을 기계 앞 635mm 허공에 띄운 원인이다. 결과를 눈으로 확인할 것.");
                log.AppendLine("  ⚠ 앵커 폴백: SM_Soul_Core_* (유리 " + cores.Count + "개뿐)");
                cores = legacy;
            }
            if (cores.Count != 9)
            {
                Debug.LogError("[상승] 챔버 앵커가 9개가 아니다 (" + cores.Count + ") — 중단한다.");
                return;
            }

            // ⚠ `transform.position` 을 쓰면 안 된다. 블렌더에서 온 구슬 아홉 개는
            // **트랜스폼이 전부 같은 좌표**(0, −0.211, −0.826)이고 격자는 정점 데이터에
            // 들어 있다. 좌표로 정렬하면 아홉 개가 같은 칸으로 몰린다
            // (2026-08-08 — 이 스크립트의 첫 실행이 「칸 2 가 겹쳤다」로 멈춘 이유).
            // 렌더러 바운드 중심이 실제 격자다: x −0.714/−0.238/+0.238, y 2.214/1.738/1.262.
            var pos = new Dictionary<Transform, Vector3>();
            foreach (var t in cores)
            {
                var r = t.GetComponent<Renderer>();
                if (r == null)
                {
                    Debug.LogError("[상승] " + t.name + " 에 렌더러가 없다 — 격자 위치를 알 수 없다. 중단한다.");
                    return;
                }
                pos[t] = r.bounds.center;
            }

            // 열 = x 를 3덩어리로, 행 = y 를 3덩어리로
            var byX = cores.OrderBy(t => pos[t].x).ToList();
            var colOf = new Dictionary<Transform, int>();
            for (int k = 0; k < 9; k++)
            {
                int bucket = k / 3;                          // x 오름차순 3개씩
                colOf[byX[k]] = colSign > 0 ? bucket : 2 - bucket;
            }
            var byY = cores.OrderBy(t => pos[t].y).ToList();
            var rowOf = new Dictionary<Transform, int>();
            for (int k = 0; k < 9; k++)
            {
                int bucket = k / 3;                          // y 오름차순 3개씩
                rowOf[byY[k]] = rowSign > 0 ? bucket : 2 - bucket;
            }

            var slot = new Transform[9];
            foreach (var t in cores)
            {
                int idx = colOf[t] * 3 + rowOf[t];
                if (slot[idx] != null)
                {
                    Debug.LogError("[상승] 칸 " + idx + " 가 겹쳤다 (" + slot[idx].name + " vs " + t.name
                                 + ") — 챔버가 3×3 격자가 아니다. 중단한다.");
                    return;
                }
                slot[idx] = t;
            }

            // ── 3. 기계를 바꿔 켠다 ────────────────────────────────────────
            foreach (var n in new[] { "SOCKET_ElevPanel", "SM_Cab_MachineFrame" })
            {
                var t = cab.transform.Find(n);
                if (t != null && !t.gameObject.activeSelf)
                {
                    Undo.RecordObject(t.gameObject, "enable");
                    t.gameObject.SetActive(true);
                    log.AppendLine("  ON   CabinAD47/" + n);
                }
            }
            var oldMachine = GameObject.Find("ReferenceRoom/SoulMachineFrame");
            if (oldMachine != null && oldMachine.activeSelf)
            {
                Undo.RecordObject(oldMachine, "disable");
                oldMachine.SetActive(false);
                log.AppendLine("  off  ReferenceRoom/SoulMachineFrame");
            }

            // ── 4. 셀과 그레이박스 구슬 ────────────────────────────────────
            //
            // 🔴 **깊이 탐색으로 지운다. `Find` 는 직속 자식만 본다** (2026-08-08 정정).
            //
            // 직전 판본은 `cab.transform.Find(CellRoot)` 였다. 그런데 실제 경로는
            // `CabinAD47/SOCKET_ElevPanel/BoardCells` — **직속 자식이 아니다.**
            // (첫 실행은 `cab` 직속으로 만들지만 이후 다른 배선이 소켓 아래로 옮긴다.)
            // 그래서 재실행마다 못 찾고 새로 만들어 **구슬 63개가 매번 누적됐다.**
            // 실측: 한 번 재실행하니 63 → 126 이 됐다. 옛 집합은 `_cells` 에서 떨어져
            // 나가 조작에 반응하지 않으면서 계속 그려진다 — 화면에는 구슬이 두 벌 겹친다.
            int purged = 0;
            foreach (var t in cab.GetComponentsInChildren<Transform>(true).ToList())
            {
                if (t == null || t.name != CellRoot) continue;
                Object.DestroyImmediate(t.gameObject);
                purged++;
            }
            if (purged > 0) log.AppendLine("  옛 " + CellRoot + " 제거: " + purged + "개");
            var root = new GameObject(CellRoot);
            root.transform.SetParent(cab.transform, false);

            var matNormal = MakeOrbMaterial("GB_Orb_Normal", new Color(0.62f, 0.60f, 0.52f), new Color(0.85f, 0.80f, 0.62f));
            var matAbsorb = MakeOrbMaterial("GB_Orb_Absorber", new Color(0.07f, 0.07f, 0.08f), new Color(0.06f, 0.16f, 0.20f));
            var matProlif = MakeOrbMaterial("GB_Orb_Proliferator", new Color(0.46f, 0.40f, 0.24f), new Color(0.72f, 0.52f, 0.20f));

            for (int i = 0; i < 9; i++)
            {
                var cell = new GameObject("Cell_" + i);
                cell.transform.SetParent(root.transform, false);
                cell.transform.position = pos[slot[i]];      // 트랜스폼이 아니라 실제 격자 위치
                cell.transform.rotation = slot[i].rotation;

                BuildNormal(cell.transform, matNormal);
                BuildAbsorber(cell.transform, matAbsorb);
                BuildProliferator(cell.transform, matProlif);

                // ⚠ **여기서 `slot[i]` 를 끄지 않는다.** 앵커가 챔버 유리로 바뀌었으므로
                // 예전처럼 `slot[i].gameObject.SetActive(false)` 를 하면 **유리 9장을 끈다.**
                // 앵커(위치의 출처)와 교체 대상(끌 것)은 다른 역할이고, 한 변수로 겸하면
                // 앵커를 바꾸는 순간 조용히 다른 것을 끄게 된다.
                slot[i] = cell.transform;
            }

            // 원래 구슬 메시는 끈다 — 그레이박스가 그 자리를 대신한다.
            // 앵커와 무관하게 **`SM_Soul_Core_*` 만** 대상으로 삼는다. 없으면(AD57 에서
            // 삭제됐다) 끌 것이 없으므로 0 개가 정상이다.
            int hidden = 0;
            foreach (var t in cab.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("SM_Soul_Core_")) continue;
                if (!t.gameObject.activeSelf) continue;
                Undo.RecordObject(t.gameObject, "hide legacy soul core");
                t.gameObject.SetActive(false);
                hidden++;
            }
            log.AppendLine("  옛 구슬 메시 끔: " + hidden + "개 (SM_Soul_Core_*)");

            // ── 5. 재배선 ─────────────────────────────────────────────────
            for (int i = 0; i < 9; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = slot[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);

            log.AppendLine("  _cells 9칸 재배선 완료");
            for (int i = 0; i < 9; i++)
            {
                var p = slot[i].position;
                log.AppendLine(string.Format("    [{0}] col={1} row={2}  ({3,6:F3},{4,6:F3},{5,6:F3})",
                    i, i / 3, i % 3, p.x, p.y, p.z));
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log(log.ToString());
        }

        // ── 그레이박스 세 형태 ────────────────────────────────────────────
        // 무드보드 §8: 색이 아니라 **외곽선**으로 갈려야 한다. 실루엣만 다르게 만든다.

        private const float R = 0.052f;   // 구슬 반지름 — 챔버 안에 들어가는 크기

        /// <summary>정상 영혼 — 매끈한 달걀형.</summary>
        private static void BuildNormal(Transform parent, Material mat)
        {
            var go = Prim(parent, "Sym_NormalSoul", PrimitiveType.Sphere, mat);
            go.transform.localScale = new Vector3(R * 2f, R * 2.25f, R * 2f);
        }

        /// <summary>흡수체 — 중심이 깊게 함몰됐다. 구 안에 검은 구를 박아 실루엣에 홈을 만든다.</summary>
        private static void BuildAbsorber(Transform parent, Material mat)
        {
            var go = new GameObject("Sym_Absorber");
            go.transform.SetParent(parent, false);
            var shell = Prim(go.transform, "Shell", PrimitiveType.Sphere, mat);
            shell.transform.localScale = Vector3.one * R * 2f;
            var pit = Prim(go.transform, "Pit", PrimitiveType.Sphere, mat);
            pit.transform.localScale = Vector3.one * R * 1.45f;
            pit.transform.localPosition = new Vector3(0f, 0f, -R * 0.95f);   // 보는 쪽으로 파인다
        }

        /// <summary>증식체 — 비대칭 마디. 위성 구슬이 밀려 나오는 실루엣.</summary>
        private static void BuildProliferator(Transform parent, Material mat)
        {
            var go = new GameObject("Sym_Proliferator");
            go.transform.SetParent(parent, false);
            var offs = new[]
            {
                new Vector3( 0.00f,  0.00f, 0f),
                new Vector3( 0.62f,  0.34f, 0f),
                new Vector3(-0.48f,  0.55f, 0f),
                new Vector3(-0.30f, -0.58f, 0f),
            };
            var scales = new[] { 1.00f, 0.66f, 0.54f, 0.46f };
            for (int i = 0; i < offs.Length; i++)
            {
                var b = Prim(go.transform, "Bud_" + i, PrimitiveType.Sphere, mat);
                b.transform.localScale = Vector3.one * R * 2f * scales[i];
                b.transform.localPosition = offs[i] * R;
            }
        }

        private static GameObject Prim(Transform parent, string name, PrimitiveType type, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);   // 구슬은 물리에 참여하지 않는다
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static Material MakeOrbMaterial(string name, Color baseColor, Color emission)
        {
            string path = MatDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var template = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Prototype_Elevator/Materials/Cabin/ELV_LampGlass.mat");
            if (mat == null)
            {
                mat = new Material(template) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = template.shader;
            mat.CopyPropertiesFromMaterial(template);
            mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", null);
            mat.SetColor("_EmissionColor", emission);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }
    }
}
