using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ascend.Prototype.Art
{
    /// <summary>메시를 세울 기준 축. 프리즘·튜브가 어느 방향으로 뻗는지를 정한다.</summary>
    public enum MeshAxis
    {
        X = 0,
        Y = 1,
        Z = 2,
    }

    /// <summary>
    /// 절차적 메시의 **누산기**. 삼각형을 하나씩 받아 정점·법선·UV·인덱스를 쌓는다.
    ///
    /// ## 왜 이 클래스가 이런 모양인가
    ///
    /// **① 기본이 flat normal 이다 — 이것이 `GRAPHICS_TARGET.md` §G-4 의 전제다.**
    /// `Ascend/Stylized` 는 `Quantize(ndotl)` 로 램버트를 계단으로 끊는다. 그 계단이
    /// **면**에 걸리려면 한 폴리곤 안에서 `ndotl` 이 상수여야 하고, 그러려면 정점 법선이
    /// 면 법선과 같아야 한다. 부드러운 법선을 주면 `ndotl` 이 면 안에서 연속으로 변해
    /// 계단이 **면 경계가 아니라 그라디언트 중간**에 걸린다 — 14차 실측이 벽면 200px 에서
    /// 평탄 구간 156개(최장 7px)를 잰 것이 정확히 그 상태다.
    /// 그래서 이 빌더는 삼각형마다 정점을 새로 만든다. 정점 공유를 하지 않는다.
    ///
    /// **② winding 을 「바깥 방향 힌트」로 자가 교정한다.**
    /// 뒤집힌 면은 컴파일도 되고 테스트도 통과하는데 화면에서만 사라진다 — 이 저장소가
    /// 가장 싫어하는 종류의 실패다. 그래서 호출자가 정점 순서를 외우는 대신
    /// **바깥이 어느 쪽인지**만 알려주고, 계산된 면 법선이 그 반대면 빌더가 뒤집는다.
    /// 손으로 8개 모서리의 순서를 외우다 하나를 틀리는 경로 자체를 없앤다.
    ///
    /// **③ UV 가 월드 미터 기준이다.**
    /// 128×128 손그림 텍스처를 3.2m 벽과 0.2m 리벳에 같은 [0,1] UV 로 깔면 전자는
    /// 뭉개지고 후자는 새까맣다. 여기서는 `uvPerMeter` 로 「1m 당 몇 번 반복」을 받고
    /// **지배축 평면 투영**으로 UV 를 만든다. 그래서 같은 `uvPerMeter` 를 쓴 두 물체는
    /// 크기가 달라도 텍셀 밀도가 같다.
    ///
    /// ⚠ **Transform 스케일을 쓰지 마라.** UV 가 로컬 좌표(= 미터)에서 나오므로,
    /// `localScale` 로 늘리면 텍셀 밀도가 그만큼 어긋난다. 원하는 크기를 인자로 요구하라.
    ///
    /// **④ 결정론적이다.** 난수를 쓰지 않는다. 비대칭이 필요하면 <see cref="Hash01"/> 로
    /// 정수 시드에서 뽑는다 — 같은 인자면 같은 정점 배열이 나온다.
    /// </summary>
    public sealed class ProcMeshBuilder
    {
        private readonly List<Vector3> _positions;
        private readonly List<Vector3> _normals;
        private readonly List<Vector2> _uvs;
        private readonly List<int> _triangles;

        public ProcMeshBuilder(int vertexCapacity = 256)
        {
            if (vertexCapacity < 4) vertexCapacity = 4;
            _positions = new List<Vector3>(vertexCapacity);
            _normals = new List<Vector3>(vertexCapacity);
            _uvs = new List<Vector2>(vertexCapacity);
            _triangles = new List<int>(vertexCapacity * 3);
        }

        public int VertexCount => _positions.Count;
        public int TriangleCount => _triangles.Count / 3;

        // 테스트와 결합기가 Mesh 를 만들지 않고도 결과를 검사할 수 있어야 한다.
        // Mesh 를 만들면 EditMode 테스트가 네이티브 객체를 흘리고, 그 누수는
        // 「테스트가 통과했는데 에디터가 무거워진다」로만 나타난다.
        public IReadOnlyList<Vector3> Positions => _positions;
        public IReadOnlyList<Vector3> Normals => _normals;
        public IReadOnlyList<Vector2> UVs => _uvs;
        public IReadOnlyList<int> Triangles => _triangles;

        public void Clear()
        {
            _positions.Clear();
            _normals.Clear();
            _uvs.Clear();
            _triangles.Clear();
        }

        // ── 원시 추가 ────────────────────────────────────────────────────────

        /// <summary>
        /// 삼각형 하나. 정점 3개를 **새로** 만들고 셋 다 같은 면 법선을 받는다(flat).
        /// <paramref name="outward"/> 가 0 이 아니면 계산된 면 법선이 그쪽을 향하도록 뒤집는다.
        /// </summary>
        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 outward, float uvPerMeter)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            float len = n.magnitude;
            if (len <= 1e-9f) return; // 퇴화 삼각형은 넣지 않는다 — 법선이 없으면 셰이딩이 없다.
            n /= len;

            if (outward.sqrMagnitude > 1e-12f && Vector3.Dot(n, outward) < 0f)
            {
                // 뒤집는다. 정점 순서와 법선을 **함께** 뒤집어야 한다 —
                // 한쪽만 뒤집으면 backface culling 은 통과하는데 조명이 반대로 온다.
                Vector3 t = b; b = c; c = t;
                n = -n;
            }

            EmitVertex(a, n, uvPerMeter);
            EmitVertex(b, n, uvPerMeter);
            EmitVertex(c, n, uvPerMeter);
            int i = _positions.Count;
            _triangles.Add(i - 3);
            _triangles.Add(i - 2);
            _triangles.Add(i - 1);
        }

        /// <summary>
        /// 사각형 하나 = 삼각형 둘. a→b→c→d 로 둘레를 도는 순서로 준다.
        /// 순서가 시계·반시계 어느 쪽이든 <paramref name="outward"/> 가 교정한다.
        /// </summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, float uvPerMeter)
        {
            Vector3 n1 = Vector3.Cross(b - a, c - a);   // 삼각형 (a,b,c)
            Vector3 n2 = Vector3.Cross(c - a, d - a);   // 삼각형 (a,c,d)

            // **비틀린 사각형은 사각형이 아니라 삼각형 둘이다.**
            //
            // 경로를 따라가는 관(케이블·고리·굽은 파이프)에서는 이웃 링의 프레임이
            // 조금씩 돌아가서 옆면 사각형이 평면이 아니게 된다. 그때 두 삼각형에
            // **하나의 법선**을 주면 그중 한 쪽은 자기 면과 어긋난 법선을 갖는다.
            //
            // 실측: 화물 고리에서 최대 3.401° 어긋났다. 작아 보이지만 이 저장소에서는
            // 그게 정확히 금지된 종류의 오차다 — `GRAPHICS_TARGET.md` §G-4 의 전제가
            // 「한 폴리곤 안에서 `ndotl` 이 상수」이고, 법선이 면과 어긋나면 그 전제가
            // 조용히 깨진다. 14라운드 동안 계단이 안 보인 이유가 이런 종류의
            // 「작아서 안 잡히는」 연속성이었다.
            //
            // 그래서 갈라진 면은 갈라진 채로 낸다. 로우폴리에서 그 대각선 주름은
            // 결함이 아니라 **면**이다.
            if (n1.sqrMagnitude > 1e-18f && n2.sqrMagnitude > 1e-18f &&
                Vector3.Dot(n1.normalized, n2.normalized) < 0.99998f)
            {
                AddTriangle(a, b, c, outward, uvPerMeter);
                AddTriangle(a, c, d, outward, uvPerMeter);
                return;
            }

            Vector3 n = n1.sqrMagnitude > 1e-18f ? n1 : n2;
            float len = n.magnitude;
            if (len <= 1e-9f) return;
            n /= len;

            if (outward.sqrMagnitude > 1e-12f && Vector3.Dot(n, outward) < 0f)
            {
                Vector3 t = b; b = d; d = t;   // a,b,c,d → a,d,c,b
                n = -n;
            }

            // 두 삼각형이 **같은 법선**을 받는다. 사각형이 살짝 비틀려 있어도
            // 면으로 읽혀야 한다 — 계단 셰이딩이 한 면 안에서 갈라지면 안 된다.
            int baseIndex = _positions.Count;
            EmitVertex(a, n, uvPerMeter);
            EmitVertex(b, n, uvPerMeter);
            EmitVertex(c, n, uvPerMeter);
            EmitVertex(d, n, uvPerMeter);
            _triangles.Add(baseIndex + 0);
            _triangles.Add(baseIndex + 1);
            _triangles.Add(baseIndex + 2);
            _triangles.Add(baseIndex + 0);
            _triangles.Add(baseIndex + 2);
            _triangles.Add(baseIndex + 3);
        }

        /// <summary>
        /// 법선과 UV 를 호출자가 직접 주는 사각형. **부드러운 법선(smooth)** 과
        /// 원통 UV 가 필요한 곳(파이프·케이블)에서만 쓴다.
        /// </summary>
        public void AddQuadRaw(
            Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd,
            Vector2 uva, Vector2 uvb, Vector2 uvc, Vector2 uvd,
            Vector3 outward)
        {
            Vector3 face = Vector3.Cross(b - a, c - a);
            if (face.sqrMagnitude <= 1e-18f) face = Vector3.Cross(c - a, d - a);
            if (face.sqrMagnitude <= 1e-18f) return;

            if (outward.sqrMagnitude > 1e-12f && Vector3.Dot(face, outward) < 0f)
            {
                Swap(ref b, ref d);
                Swap(ref nb, ref nd);
                Swap(ref uvb, ref uvd);
            }

            int baseIndex = _positions.Count;
            EmitVertexRaw(a, na, uva);
            EmitVertexRaw(b, nb, uvb);
            EmitVertexRaw(c, nc, uvc);
            EmitVertexRaw(d, nd, uvd);
            _triangles.Add(baseIndex + 0);
            _triangles.Add(baseIndex + 1);
            _triangles.Add(baseIndex + 2);
            _triangles.Add(baseIndex + 0);
            _triangles.Add(baseIndex + 2);
            _triangles.Add(baseIndex + 3);
        }

        /// <summary>
        /// 법선·UV 를 그대로 받는 삼각형. 결합기가 원본 데이터를 **손대지 않고** 옮길 때
        /// 쓴다 — winding 교정도 UV 재투영도 하지 않는다. 원본이 이미 옳다는 전제다.
        /// </summary>
        public void AddTriangleRaw(Vector3 a, Vector3 b, Vector3 c,
                                   Vector3 na, Vector3 nb, Vector3 nc,
                                   Vector2 uva, Vector2 uvb, Vector2 uvc)
        {
            int baseIndex = _positions.Count;
            EmitVertexRaw(a, na, uva);
            EmitVertexRaw(b, nb, uvb);
            EmitVertexRaw(c, nc, uvc);
            _triangles.Add(baseIndex + 0);
            _triangles.Add(baseIndex + 1);
            _triangles.Add(baseIndex + 2);
        }

        private static void Swap<T>(ref T x, ref T y) { T t = x; x = y; y = t; }

        private void EmitVertex(Vector3 p, Vector3 n, float uvPerMeter)
        {
            _positions.Add(p);
            _normals.Add(n);
            _uvs.Add(PlanarUV(p, n, uvPerMeter));
        }

        private void EmitVertexRaw(Vector3 p, Vector3 n, Vector2 uv)
        {
            _positions.Add(p);
            _normals.Add(n.normalized);
            _uvs.Add(uv);
        }

        /// <summary>
        /// 지배축 평면 투영 UV. 단위는 **미터 × <paramref name="uvPerMeter"/>** 다.
        ///
        /// [0,1] 을 넘어가는 것이 정상이다 — 텍스처를 `Repeat` 로 임포트해야 타일링이 성립한다
        /// (`Assets/Editor/AscendImportRules.cs` 가 그 규칙을 강제한다).
        /// `Clamp` 로 임포트된 텍스처를 쓰면 벽 하나가 통째로 가장자리 한 줄로 늘어난다.
        /// </summary>
        public static Vector2 PlanarUV(Vector3 p, Vector3 n, float uvPerMeter)
        {
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            float u, v;
            if (ax >= ay && ax >= az)
            {
                u = n.x >= 0f ? p.z : -p.z;
                v = p.y;
            }
            else if (ay >= az)
            {
                u = p.x;
                v = n.y >= 0f ? p.z : -p.z;
            }
            else
            {
                u = n.z >= 0f ? -p.x : p.x;
                v = p.y;
            }
            return new Vector2(u * uvPerMeter, v * uvPerMeter);
        }

        // ── 기본 입체 ────────────────────────────────────────────────────────

        /// <summary>
        /// 축 정렬 상자. <paramref name="bevel"/> 이 0 이면 면 6개(12 삼각형),
        /// 0 보다 크면 **모따기 상자**(44 삼각형)다.
        ///
        /// 모따기가 필요한 이유는 폴리곤을 늘리려는 것이 아니다. 모따기 띠는
        /// 실루엣 모서리에서 **주광과 다른 각도**를 갖고, `Quantize` 가 그 띠를
        /// 옆면과 다른 칸에 떨어뜨린다 → 모서리를 따라 한 줄의 밝은 선이 생긴다.
        /// 균일한 큐브가 「로우폴리」가 아니라 「미완성」으로 읽히는 이유가 그 선의 부재다.
        /// 12 → 44 삼각형의 대가로 실루엣이 읽힌다.
        /// </summary>
        public void AddBox(Vector3 center, Vector3 size, float bevel = 0f, float uvPerMeter = 1f)
        {
            float hx = Mathf.Abs(size.x) * 0.5f;
            float hy = Mathf.Abs(size.y) * 0.5f;
            float hz = Mathf.Abs(size.z) * 0.5f;
            if (hx <= 1e-6f || hy <= 1e-6f || hz <= 1e-6f) return;

            float b = Mathf.Clamp(bevel, 0f, Mathf.Min(hx, Mathf.Min(hy, hz)) * 0.9f);

            if (b <= 1e-5f)
            {
                AddQuad(center + new Vector3(hx, -hy, -hz), center + new Vector3(hx, hy, -hz),
                        center + new Vector3(hx, hy, hz), center + new Vector3(hx, -hy, hz),
                        Vector3.right, uvPerMeter);
                AddQuad(center + new Vector3(-hx, -hy, -hz), center + new Vector3(-hx, hy, -hz),
                        center + new Vector3(-hx, hy, hz), center + new Vector3(-hx, -hy, hz),
                        Vector3.left, uvPerMeter);
                AddQuad(center + new Vector3(-hx, hy, -hz), center + new Vector3(hx, hy, -hz),
                        center + new Vector3(hx, hy, hz), center + new Vector3(-hx, hy, hz),
                        Vector3.up, uvPerMeter);
                AddQuad(center + new Vector3(-hx, -hy, -hz), center + new Vector3(hx, -hy, -hz),
                        center + new Vector3(hx, -hy, hz), center + new Vector3(-hx, -hy, hz),
                        Vector3.down, uvPerMeter);
                AddQuad(center + new Vector3(-hx, -hy, hz), center + new Vector3(hx, -hy, hz),
                        center + new Vector3(hx, hy, hz), center + new Vector3(-hx, hy, hz),
                        Vector3.forward, uvPerMeter);
                AddQuad(center + new Vector3(-hx, -hy, -hz), center + new Vector3(hx, -hy, -hz),
                        center + new Vector3(hx, hy, -hz), center + new Vector3(-hx, hy, -hz),
                        Vector3.back, uvPerMeter);
                return;
            }

            float ix = hx - b, iy = hy - b, iz = hz - b;

            // 면 6개 — 접선 두 축으로 b 만큼 안으로 들어간다.
            AddFace(center + new Vector3(hx, 0f, 0f), Vector3.up * iy, Vector3.forward * iz, uvPerMeter);
            AddFace(center + new Vector3(-hx, 0f, 0f), Vector3.forward * iz, Vector3.up * iy, uvPerMeter);
            AddFace(center + new Vector3(0f, hy, 0f), Vector3.forward * iz, Vector3.right * ix, uvPerMeter);
            AddFace(center + new Vector3(0f, -hy, 0f), Vector3.right * ix, Vector3.forward * iz, uvPerMeter);
            AddFace(center + new Vector3(0f, 0f, hz), Vector3.right * ix, Vector3.up * iy, uvPerMeter);
            AddFace(center + new Vector3(0f, 0f, -hz), Vector3.up * iy, Vector3.right * ix, uvPerMeter);

            // 모서리 띠 12개.
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                AddQuad(center + new Vector3(-ix, sy * hy, sz * iz),
                        center + new Vector3(ix, sy * hy, sz * iz),
                        center + new Vector3(ix, sy * iy, sz * hz),
                        center + new Vector3(-ix, sy * iy, sz * hz),
                        new Vector3(0f, sy, sz).normalized, uvPerMeter);
            }
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                AddQuad(center + new Vector3(sx * hx, -iy, sz * iz),
                        center + new Vector3(sx * hx, iy, sz * iz),
                        center + new Vector3(sx * ix, iy, sz * hz),
                        center + new Vector3(sx * ix, -iy, sz * hz),
                        new Vector3(sx, 0f, sz).normalized, uvPerMeter);
            }
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            {
                AddQuad(center + new Vector3(sx * hx, sy * iy, -iz),
                        center + new Vector3(sx * hx, sy * iy, iz),
                        center + new Vector3(sx * ix, sy * hy, iz),
                        center + new Vector3(sx * ix, sy * hy, -iz),
                        new Vector3(sx, sy, 0f).normalized, uvPerMeter);
            }

            // 모서리 꼭짓점 8개.
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                AddTriangle(center + new Vector3(sx * hx, sy * iy, sz * iz),
                            center + new Vector3(sx * ix, sy * hy, sz * iz),
                            center + new Vector3(sx * ix, sy * iy, sz * hz),
                            new Vector3(sx, sy, sz).normalized, uvPerMeter);
            }
        }

        /// <summary>
        /// 지금까지 쌓인 정점 수를 표시해 둔다. 이후에 추가한 것만 옮기고 싶을 때 쓴다.
        /// </summary>
        public int Mark() => _positions.Count;

        /// <summary>
        /// <see cref="Mark"/> 이후에 추가된 정점을 회전·이동한다.
        /// 기울어진 레버 축·비스듬한 슬랫처럼 축에 안 맞는 조각을 만드는 유일한 수단이고,
        /// 모든 형상 함수가 축 정렬 버전 하나만 갖게 해 준다(중복 구현을 안 만든다).
        ///
        /// ⚠ 회전은 **법선에도** 적용된다. 위치만 옮기면 조명이 원래 방향으로 남아
        /// 「형태는 기울었는데 빛은 안 기운」 상태가 된다 — 계단 셰이딩에서는 특히 눈에 띈다.
        /// ⚠ UV 는 옮기지 **않는다.** 이미 로컬 미터로 구워졌고, 회전 뒤에도 텍셀 밀도는
        /// 같아야 한다. 회전으로 UV 를 다시 투영하면 조각마다 밀도가 달라진다.
        /// </summary>
        public void TransformSince(int mark, Vector3 translation, Quaternion rotation)
        {
            if (mark < 0) mark = 0;
            for (int i = mark; i < _positions.Count; i++)
            {
                _positions[i] = translation + rotation * _positions[i];
                _normals[i] = rotation * _normals[i];
            }
        }

        /// <summary>중심과 두 반접선으로 만드는 면. 법선은 Cross(u, v) 방향이다.</summary>
        private void AddFace(Vector3 center, Vector3 u, Vector3 v, float uvPerMeter)
        {
            Vector3 outward = Vector3.Cross(u, v).normalized;
            AddQuad(center - u - v, center + u - v, center + u + v, center - u + v, outward, uvPerMeter);
        }

        /// <summary>
        /// 회전된 상자. 레버 손잡이·비스듬한 판자처럼 축에 안 맞는 조각에 쓴다.
        /// </summary>
        public void AddBox(Vector3 center, Vector3 size, Quaternion rotation,
                           float bevel = 0f, float uvPerMeter = 1f)
        {
            int mark = Mark();
            AddBox(Vector3.zero, size, bevel, uvPerMeter);
            TransformSince(mark, center, rotation);
        }

        /// <summary>
        /// n 각 기둥(원뿔대 포함). <paramref name="sides"/> 는 로우폴리 락에 따라 4~8 을 쓴다.
        /// 6 이면 20 삼각형, 8 이면 28 삼각형이다.
        ///
        /// <paramref name="smooth"/> 는 **기본이 false** 다. 그것이 이 프로젝트의 기본값인 이유는
        /// `GRAPHICS_TARGET.md` §G-4 다 — 각진 법선이라야 `Quantize` 가 면을 드러낸다.
        /// smooth 는 케이블처럼 「면이 보이면 오히려 이상한」 곳에만 쓴다.
        /// </summary>
        public void AddPrism(Vector3 center, float radiusStart, float radiusEnd, float length,
                             int sides, MeshAxis axis, float angleOffsetDeg = 0f,
                             bool capStart = true, bool capEnd = true,
                             bool smooth = false, float uvPerMeter = 1f)
        {
            sides = Mathf.Clamp(sides, 3, 24);
            if (length <= 1e-6f) return;
            if (radiusStart <= 1e-6f && radiusEnd <= 1e-6f) return;

            GetAxisFrame(axis, out Vector3 dir, out Vector3 u, out Vector3 v);
            Vector3 start = center - dir * (length * 0.5f);
            Vector3 end = center + dir * (length * 0.5f);

            var ringStart = new Vector3[sides];
            var ringEnd = new Vector3[sides];
            var radial = new Vector3[sides];
            float step = 360f / sides;
            for (int i = 0; i < sides; i++)
            {
                float a = (angleOffsetDeg + step * i) * Mathf.Deg2Rad;
                Vector3 r = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                radial[i] = r;
                ringStart[i] = start + r * radiusStart;
                ringEnd[i] = end + r * radiusEnd;
            }

            // 원뿔대의 참 법선: 반경 변화가 있으면 법선이 축 쪽으로 기운다.
            float slope = radiusStart - radiusEnd;
            float circumference = Mathf.PI * (radiusStart + radiusEnd);

            // 한쪽 반경이 0 이면 그쪽은 사각형이 아니라 **뿔**이다. 사각형으로 만들면
            // 넓이 0 짜리 삼각형이 섞여 들어가고, 넓이 0 은 법선이 없어서 셰이딩이 아니라
            // 검은 조각으로 보인다. 게다가 닫힘 검사가 `a→a` 모서리에서 걸린다.
            bool degenerateStart = radiusStart <= 1e-6f;
            bool degenerateEnd = radiusEnd <= 1e-6f;
            if (degenerateStart) { AddCone(ringEnd, start, -dir, uvPerMeter); }
            if (degenerateEnd) { AddCone(ringStart, end, dir, uvPerMeter); }

            for (int i = 0; i < sides && !degenerateStart && !degenerateEnd; i++)
            {
                int j = (i + 1) % sides;
                Vector3 outward = (radial[i] + radial[j]).normalized;
                if (smooth)
                {
                    Vector3 ni = (radial[i] * length + dir * slope).normalized;
                    Vector3 nj = (radial[j] * length + dir * slope).normalized;
                    float u0 = circumference * i / sides * uvPerMeter;
                    float u1 = circumference * (i + 1) / sides * uvPerMeter;
                    float v1 = length * uvPerMeter;
                    AddQuadRaw(ringStart[i], ringStart[j], ringEnd[j], ringEnd[i],
                               ni, nj, nj, ni,
                               new Vector2(u0, 0f), new Vector2(u1, 0f),
                               new Vector2(u1, v1), new Vector2(u0, v1),
                               outward);
                }
                else
                {
                    AddQuad(ringStart[i], ringStart[j], ringEnd[j], ringEnd[i], outward, uvPerMeter);
                }
            }

            // 뿔이 된 쪽은 이미 닫혀 있으므로 뚜껑을 또 덮지 않는다.
            if (capStart && !degenerateStart) AddFan(ringStart, -dir, uvPerMeter);
            if (capEnd && !degenerateEnd) AddFan(ringEnd, dir, uvPerMeter);
        }

        private void AddFan(Vector3[] ring, Vector3 outward, float uvPerMeter)
        {
            for (int i = 1; i < ring.Length - 1; i++)
                AddTriangle(ring[0], ring[i], ring[i + 1], outward, uvPerMeter);
        }

        private void AddCone(Vector3[] ring, Vector3 apex, Vector3 axisOut, float uvPerMeter)
        {
            // 바깥 힌트는 **링 중심에서 밖으로** + 축 성분이다. 「꼭짓점에서 밖으로」로
            // 잡으면 그 방향이 원뿔 옆면과 거의 평행해서 부호 판정이 흔들린다.
            Vector3 center = Vector3.zero;
            for (int i = 0; i < ring.Length; i++) center += ring[i];
            center /= ring.Length;

            for (int i = 0; i < ring.Length; i++)
            {
                int j = (i + 1) % ring.Length;
                Vector3 radial = ((ring[i] + ring[j]) * 0.5f - center).normalized;
                Vector3 outward = (radial + axisOut * 0.5f).normalized;
                AddTriangle(apex, ring[i], ring[j], outward, uvPerMeter);
            }
        }

        /// <summary>
        /// 경로를 따라가는 관. 케이블·굽은 파이프·고리에 쓴다.
        /// 프레임은 **평행 이동(parallel transport)** 으로 옮긴다 — 경로가 꺾여도 비틀림이
        /// 최소가 되고, 무엇보다 **결정론적**이다(`Quaternion.FromToRotation` 은 순수 함수).
        /// </summary>
        public void AddTube(IReadOnlyList<Vector3> path, float radius, int sides,
                            bool closedLoop = false, bool capEnds = true,
                            bool smooth = false, float uvPerMeter = 1f)
        {
            if (path == null || path.Count < 2 || radius <= 1e-6f) return;
            sides = Mathf.Clamp(sides, 3, 16);
            int count = path.Count;

            var rings = new Vector3[count][];
            var radials = new Vector3[count][];
            var arcLength = new float[count];

            Vector3 prevTangent = Tangent(path, 0, closedLoop);
            Vector3 seed = Mathf.Abs(Vector3.Dot(prevTangent, Vector3.up)) > 0.9f
                ? Vector3.right : Vector3.up;
            Vector3 u = Vector3.Cross(seed, prevTangent).normalized;
            Vector3 v = Vector3.Cross(prevTangent, u).normalized;

            float step = 360f / sides;
            for (int p = 0; p < count; p++)
            {
                if (p > 0)
                {
                    Vector3 t = Tangent(path, p, closedLoop);
                    Quaternion rot = ProcQuat.FromTo(prevTangent, t);
                    u = (rot * u).normalized;
                    v = Vector3.Cross(t, u).normalized;
                    prevTangent = t;
                    arcLength[p] = arcLength[p - 1] + Vector3.Distance(path[p - 1], path[p]);
                }

                rings[p] = new Vector3[sides];
                radials[p] = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    float a = step * i * Mathf.Deg2Rad;
                    Vector3 r = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                    radials[p][i] = r;
                    rings[p][i] = path[p] + r * radius;
                }
            }

            float circumference = 2f * Mathf.PI * radius;
            int segments = closedLoop ? count : count - 1;
            for (int p = 0; p < segments; p++)
            {
                // 닫힌 고리의 마지막 구간은 **0번 링을 그대로 재사용**한다.
                // 새로 계산하면 프레임 표류로 정점이 미세하게 어긋나 이음매가 열린다 —
                // 닫힘 검사가 그것을 잡아내지만, 애초에 열리지 않게 만드는 쪽이 낫다.
                int q = (p + 1) % count;
                float v0 = arcLength[p] * uvPerMeter;
                float v1 = (p + 1 < count ? arcLength[p + 1]
                                          : arcLength[count - 1] + Vector3.Distance(path[count - 1], path[0]))
                           * uvPerMeter;
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    Vector3 outward = (radials[p][i] + radials[p][j] + radials[q][i] + radials[q][j]).normalized;
                    if (smooth)
                    {
                        float u0 = circumference * i / sides * uvPerMeter;
                        float u1 = circumference * (i + 1) / sides * uvPerMeter;
                        AddQuadRaw(rings[p][i], rings[p][j], rings[q][j], rings[q][i],
                                   radials[p][i], radials[p][j], radials[q][j], radials[q][i],
                                   new Vector2(u0, v0), new Vector2(u1, v0),
                                   new Vector2(u1, v1), new Vector2(u0, v1),
                                   outward);
                    }
                    else
                    {
                        AddQuad(rings[p][i], rings[p][j], rings[q][j], rings[q][i], outward, uvPerMeter);
                    }
                }
            }

            if (!closedLoop && capEnds)
            {
                AddFan(rings[0], -Tangent(path, 0, false), uvPerMeter);
                AddFan(rings[count - 1], Tangent(path, count - 1, false), uvPerMeter);
            }
        }

        private static Vector3 Tangent(IReadOnlyList<Vector3> path, int i, bool loop)
        {
            int n = path.Count;
            Vector3 t;
            if (loop) t = path[(i + 1) % n] - path[(i - 1 + n) % n];
            else if (i == 0) t = path[1] - path[0];
            else if (i == n - 1) t = path[n - 1] - path[n - 2];
            else t = path[i + 1] - path[i - 1];
            if (t.sqrMagnitude <= 1e-12f) t = Vector3.forward;
            return t.normalized;
        }

        private static void GetAxisFrame(MeshAxis axis, out Vector3 dir, out Vector3 u, out Vector3 v)
        {
            switch (axis)
            {
                case MeshAxis.X: dir = Vector3.right; u = Vector3.forward; v = Vector3.up; break;
                case MeshAxis.Z: dir = Vector3.forward; u = Vector3.right; v = Vector3.up; break;
                default: dir = Vector3.up; u = Vector3.right; v = Vector3.forward; break;
            }
        }

        // ── 산출 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 쌓인 것을 <see cref="Mesh"/> 로 굳힌다. **이 지점이 할당이 일어나는 곳**이므로
        /// <see cref="MeshBuildPhase"/> 문지기가 여기 붙어 있다.
        /// </summary>
        public Mesh ToMesh(string name)
        {
            MeshBuildPhase.RequireOpen(name ?? "(이름 없음)");
            var mesh = new Mesh { name = string.IsNullOrEmpty(name) ? "ProcMesh" : name };
            WriteInto(mesh);
            return mesh;
        }

        /// <summary>이미 있는 메시에 덮어쓴다. 에디터 도구가 재생성할 때 쓴다.</summary>
        public void WriteInto(Mesh mesh)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            mesh.Clear();
            // 정점 65,535 를 넘길 일이 없다(프롭 하나당 300 삼각형 예산). 16비트로 둔다 —
            // 32비트 인덱스는 버퍼가 두 배가 되고, 로우폴리 락에서는 낭비다.
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(_positions);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.SetTriangles(_triangles, 0, true);
            mesh.RecalculateBounds();
            // 접선은 만들지 않는다 — `Ascend/Stylized` 는 노멀맵을 읽지 않는다.
            // 만들면 정점 버퍼가 커지기만 하고 아무도 쓰지 않는다.
        }

        /// <summary>
        /// 정수 시드 → [0,1). 비대칭·마모를 넣되 **결정론을 지키는** 유일한 수단이다.
        /// `UnityEngine.Random` 은 전역 상태를 읽으므로 같은 인자에 같은 결과를 보장하지 않는다.
        /// (PCG 계열 정수 해시 — 부동소수 누적이 없어 플랫폼 간에도 같은 비트가 나온다.)
        /// </summary>
        public static float Hash01(int seed)
        {
            uint state = (uint)seed * 747796405u + 2891336453u;
            uint word = ((state >> (int)((state >> 28) + 4u)) ^ state) * 277803737u;
            word = (word >> 22) ^ word;
            return (word & 0xFFFFFFu) * (1f / 16777216f);
        }

        /// <summary>[-1,1] 로 옮긴 <see cref="Hash01"/>. 「살짝 기울인다」에 쓴다.</summary>
        public static float HashSigned(int seed) => Hash01(seed) * 2f - 1f;
    }
}
