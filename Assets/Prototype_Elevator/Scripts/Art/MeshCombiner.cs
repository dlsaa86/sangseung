using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ascend.Prototype.Art
{
    /// <summary>
    /// 여러 메시를 하나로 합쳐 드로우콜을 줄인다.
    ///
    /// ## 왜 직접 만드는가 — `Mesh.CombineMeshes` 를 안 쓰는 이유
    ///
    /// ① `CombineMeshes` 는 `CombineInstance[]` 를 요구해서 **호출마다 배열을 할당**한다.
    /// ② 법선을 원본 그대로 옮기지 않고 상황에 따라 재계산하는데, 재계산은 **정점을 병합**해
    ///    부드러운 법선을 만든다. 그러면 `GRAPHICS_TARGET.md` §G-4 의 전제(flat normal)가
    ///    조용히 깨지고, 계단 셰이딩이 사라진 채 아무 오류도 안 난다 — 이 저장소가 14라운드
    ///    동안 스타일 2.20/5 에 묶여 있던 실패의 정확한 형태다.
    /// ③ 결과가 결정론적이라는 보장이 문서에 없다.
    ///
    /// 여기서는 정점·법선·UV·인덱스를 **그대로 옮긴다.** 법선은 역전치 행렬로 변환하고
    /// 정규화만 한다 — 병합하지 않으므로 flat 이 flat 으로 남는다.
    ///
    /// ## 프레임 루프에서 부르지 마라
    ///
    /// `UP-TECH-04` 가 **GC Alloc 0 B/frame** 으로 VERIFIED 를 받았고
    /// (`GRAPHICS_TARGET.md` §G-8), 이 클래스의 모든 경로가 할당원이다.
    /// 그래서 매 프레임 호출할 수 있는 API 를 **노출하지 않는다** — 인자로 받는 것은
    /// 완성된 목록뿐이고, 증분 갱신·부분 재결합 같은 것은 일부러 없다.
    /// <see cref="MeshBuildPhase"/> 가 조립 종료 후의 호출을 예외로 막는다.
    /// </summary>
    public static class MeshCombiner
    {
        /// <summary>합칠 조각 하나 — 메시와 그것을 놓을 자리.</summary>
        public readonly struct Entry
        {
            public readonly Mesh Mesh;
            public readonly Matrix4x4 Transform;

            public Entry(Mesh mesh, Matrix4x4 transform)
            {
                Mesh = mesh;
                Transform = transform;
            }

            public Entry(Mesh mesh, Vector3 position, Quaternion rotation)
                : this(mesh, Matrix4x4.TRS(position, rotation, Vector3.one)) { }
        }

        /// <summary>마지막 결합에서 나온 통계. 「정말 줄었는가」를 숫자로 남긴다.</summary>
        public static int LastSourceCount { get; private set; }
        public static int LastVertexCount { get; private set; }
        public static int LastTriangleCount { get; private set; }

        /// <summary>
        /// 여러 메시를 하나로. 정점 총합이 65,535 를 넘으면 던진다 —
        /// 조용히 32비트 인덱스로 넘어가면 버퍼가 두 배가 되고, 그건 로우폴리 락에서
        /// **설계가 틀렸다는 신호**이지 자동으로 넘어갈 일이 아니다.
        /// </summary>
        public static Mesh Combine(IReadOnlyList<Entry> entries, string name = "PM_Combined")
        {
            var builder = CombineIntoBuilder(entries);
            return builder.ToMesh(name);
        }

        /// <summary>
        /// 결합 결과를 <see cref="ProcMeshBuilder"/> 로 돌려준다. 여기에 더 붙일 것이
        /// 남았거나, <see cref="Mesh"/> 를 만들지 않고 검사만 하고 싶을 때 쓴다.
        /// </summary>
        public static ProcMeshBuilder CombineIntoBuilder(IReadOnlyList<Entry> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));

            int totalVerts = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                Mesh m = entries[i].Mesh;
                if (m != null) totalVerts += m.vertexCount;
            }
            if (totalVerts > 65535)
            {
                throw new InvalidOperationException(
                    $"[상승] 결합 결과 정점이 {totalVerts} 개다(상한 65,535). " +
                    "로우폴리가 스타일 락이므로 인덱스 폭을 늘리는 것이 답이 아니다 — " +
                    "결합 묶음을 나누거나 조각의 면 수를 줄인다.");
            }

            var builder = new ProcMeshBuilder(Mathf.Max(16, totalVerts));
            var positions = new List<Vector3>(256);
            var normals = new List<Vector3>(256);
            var uvs = new List<Vector2>(256);
            var tris = new List<int>(512);

            int used = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                Mesh m = entries[i].Mesh;
                if (m == null) continue;
                used++;

                m.GetVertices(positions);
                m.GetNormals(normals);
                m.GetUVs(0, uvs);
                tris.Clear();
                for (int s = 0; s < m.subMeshCount; s++)
                {
                    var sub = new List<int>();
                    m.GetTriangles(sub, s);
                    tris.AddRange(sub);
                }

                Matrix4x4 trs = entries[i].Transform;
                // 법선은 **역전치**로 옮긴다. 비균등 스케일이 들어오면 위치 행렬로
                // 법선을 옮긴 순간 법선이 표면과 어긋나고, 계단 셰이딩에서는 그게
                // 「면이 엉뚱한 칸에 떨어진다」로 곧장 보인다.
                Matrix4x4 normalMatrix = trs.inverse.transpose;

                // 뒤집는 스케일(행렬식 < 0)은 winding 을 뒤집는다. 그대로 두면
                // 그 조각만 안팎이 뒤집힌 채 렌더링된다 — 컴파일도 되고 오류도 없다.
                bool mirrored = trs.determinant < 0f;

                AppendTransformed(builder, positions, normals, uvs, tris, trs, normalMatrix, mirrored);
            }

            LastSourceCount = used;
            LastVertexCount = builder.VertexCount;
            LastTriangleCount = builder.TriangleCount;
            return builder;
        }

        private static void AppendTransformed(
            ProcMeshBuilder builder,
            List<Vector3> positions, List<Vector3> normals, List<Vector2> uvs, List<int> tris,
            Matrix4x4 trs, Matrix4x4 normalMatrix, bool mirrored)
        {
            bool hasNormals = normals.Count == positions.Count;
            bool hasUVs = uvs.Count == positions.Count;

            for (int t = 0; t + 2 < tris.Count; t += 3)
            {
                int i0 = tris[t];
                int i1 = tris[mirrored ? t + 2 : t + 1];
                int i2 = tris[mirrored ? t + 1 : t + 2];

                Vector3 p0 = trs.MultiplyPoint3x4(positions[i0]);
                Vector3 p1 = trs.MultiplyPoint3x4(positions[i1]);
                Vector3 p2 = trs.MultiplyPoint3x4(positions[i2]);

                Vector3 n0 = hasNormals ? normalMatrix.MultiplyVector(normals[i0]).normalized
                                        : Vector3.Cross(p1 - p0, p2 - p0).normalized;
                Vector3 n1 = hasNormals ? normalMatrix.MultiplyVector(normals[i1]).normalized : n0;
                Vector3 n2 = hasNormals ? normalMatrix.MultiplyVector(normals[i2]).normalized : n0;

                Vector2 u0 = hasUVs ? uvs[i0] : Vector2.zero;
                Vector2 u1 = hasUVs ? uvs[i1] : Vector2.zero;
                Vector2 u2 = hasUVs ? uvs[i2] : Vector2.zero;

                // 삼각형을 「퇴화한 사각형」으로 넣는다. `AddQuadRaw` 가 마지막 정점을
                // 첫 정점과 같게 받으면 두 번째 삼각형이 넓이 0 이 되어 버려진다 —
                // 그렇게 되지 않도록 여기서는 전용 경로를 쓴다.
                builder.AddTriangleRaw(p0, p1, p2, n0, n1, n2, u0, u1, u2);
            }
        }

        /// <summary>
        /// <paramref name="root"/> 아래의 <see cref="MeshFilter"/> 를 전부 모아 하나로 합친다.
        /// **에디터 조립 스크립트와 초기화 코드 전용**이다.
        ///
        /// 머티리얼이 서로 다른 렌더러를 섞으면 결합해도 드로우콜이 안 줄어드는 것이 아니라
        /// **그림이 틀려진다**(하나의 머티리얼로 그려지므로). 그래서 이 함수는 자기가
        /// 판단하지 않고, 호출자가 같은 머티리얼끼리 묶어서 넘기게 한다.
        /// </summary>
        public static Mesh CombineChildren(GameObject root, string name = "PM_CombinedChildren")
        {
            if (root == null) throw new ArgumentNullException(nameof(root));

            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            var entries = new List<Entry>(filters.Length);
            Matrix4x4 worldToRoot = root.transform.worldToLocalMatrix;

            for (int i = 0; i < filters.Length; i++)
            {
                Mesh m = filters[i].sharedMesh;
                if (m == null) continue;
                entries.Add(new Entry(m, worldToRoot * filters[i].transform.localToWorldMatrix));
            }
            return Combine(entries, name);
        }
    }
}
