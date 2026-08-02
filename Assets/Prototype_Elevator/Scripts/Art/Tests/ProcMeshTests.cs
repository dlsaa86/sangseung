using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Ascend.Prototype.Art.Tests
{
    /// <summary>
    /// 절차적 메시 라이브러리의 헤드리스 검증.
    ///
    /// ## 무엇을 반증하려는가
    ///
    /// 메시 생성기의 결함은 **컴파일러가 잡지 못하는 종류**다 — 뒤집힌 면, 열린 껍질,
    /// 부드러워진 법선, 어긋난 크기는 전부 「컴파일도 되고 예외도 안 나는데 화면에서만
    /// 틀린」 것이고, 이 저장소가 가장 여러 번 당한 실패의 형태가 정확히 그것이다
    /// (`UP-VIS-01` 텍스처 0장이 여러 세션 동안 발견되지 않은 이유가 관측 수단의 부재였다).
    ///
    /// 그래서 여기서 재는 것은 「함수가 예외 없이 돌았는가」가 아니라 **기하 자체**다.
    ///
    /// | 검사 | 반증하는 실패 |
    /// |---|---|
    /// | 모서리 균형 | 열린 껍질 — 뒤에서 보면 안이 비친다 (`Ascend/Stylized` 에 양면 패스가 없다) |
    /// | 부호 있는 부피 > 0 | 뒤집힌 면 — 컬링으로 사라져 「구멍」으로 보인다 |
    /// | 정점 법선 = 면 법선 | 계단 셰이딩이 그라디언트에 걸린다 (`GRAPHICS_TARGET.md` §G-4) |
    /// | 결정론 | 같은 씬을 다시 조립했는데 다른 그림이 나온다 |
    /// | 삼각형 예산 | 로우폴리 스타일 락의 침식 |
    /// | 경계 상자·피벗 | 씬에서 어긋난 자리에 놓인다 |
    ///
    /// ## 씬도 Mesh 객체도 만들지 않는다
    ///
    /// 판정은 전부 <see cref="ProcMeshBuilder"/> 의 배열에서 한다. <see cref="Mesh"/> 는
    /// 네이티브 객체라 EditMode 에서 만들면 회수를 손으로 해야 하고, 흘리면
    /// 「테스트는 통과했는데 에디터가 무거워진다」로만 나타난다.
    /// <see cref="Mesh"/> 를 실제로 만드는 경로(<see cref="ProcMeshBuilder.ToMesh"/> ·
    /// <see cref="MeshCombiner"/>)는 그것이 검사 대상인 세 건에서만 만들고 즉시 회수한다.
    ///
    /// NUnit 에 의존하지 않는 이유는 `DECISION_LOG.md` D-20260730-06 참조.
    /// </summary>
    public static class ProcMeshTests
    {
        /// <summary>위치를 같은 점으로 볼 간격(m). 0.1mm — 이 라이브러리는 공유 모서리를
        /// 비트 단위로 같은 식에서 만들므로 실제로는 완전 일치한다.</summary>
        private const float Quantum = 1e-4f;

        /// <summary>접촉면이 원점에 있어야 하는 허용 오차(m). 1cm.</summary>
        private const float ContactTolerance = 0.01f;

        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0;
            int failed = 0;
            var report = new StringBuilder();

            // ── 기하 무결성 ──────────────────────────────────────────────────
            Run("프롭 24종이 전부 닫혀 있다 (모서리 균형)", TestPropsAreClosed, ref passed, ref failed, report);
            Run("프롭 24종의 부호 있는 부피가 양수다 (뒤집힌 면 0)", TestPropsHavePositiveVolume, ref passed, ref failed, report);
            Run("기본 도구 13종이 전부 닫혀 있고 뒤집힌 면이 없다", TestPrimitivesAreClosed, ref passed, ref failed, report);
            Run("퇴화 삼각형이 없고 법선이 전부 단위 벡터다", TestNoDegenerateTriangles, ref passed, ref failed, report);

            // ── G-4 전제: flat normal ────────────────────────────────────────
            Run("flat 이 기본 — 정점 법선이 면 법선과 일치한다", TestFlatNormalsMatchFaces, ref passed, ref failed, report);
            Run("프롭 24종이 전부 flat normal 이다 (G-4)", TestAllPropsAreFlat, ref passed, ref failed, report);
            Run("smooth 옵션은 실제로 법선을 면에서 떼어낸다", TestSmoothActuallyDiffers, ref passed, ref failed, report);

            // ── 결정론 ──────────────────────────────────────────────────────
            Run("결정론 — 프롭 24종을 두 번 만들면 비트 동일하다", TestPropsAreDeterministic, ref passed, ref failed, report);
            Run("결정론 — 기본 도구도 두 번 만들면 비트 동일하다", TestPrimitivesAreDeterministic, ref passed, ref failed, report);
            Run("Hash01 이 결정론적이고 [0,1) 안에 있다", TestHashIsStable, ref passed, ref failed, report);

            // ── 예산 ────────────────────────────────────────────────────────
            Run("프롭 하나가 300 삼각형을 넘지 않는다", TestPerPropBudget, ref passed, ref failed, report);
            Run("프롭 24종 합계가 5,000 삼각형을 넘지 않는다", TestTotalBudget, ref passed, ref failed, report);
            Run("모따기 큐브는 12 가 아니라 44 삼각형이다 (면이 늘었다)", TestBevelAddsFaces, ref passed, ref failed, report);
            Run("프롭이 큐브 하나로 퇴화하지 않았다 (≥ 20 삼각형)", TestPropsAreNotSingleCubes, ref passed, ref failed, report);

            // ── 크기·피벗 ────────────────────────────────────────────────────
            Run("Box 의 경계 상자가 인자와 정확히 일치한다", TestBoxBoundsMatchArgument, ref passed, ref failed, report);
            Run("Box 의 부피가 인자의 곱과 일치한다", TestBoxVolumeMatchesArgument, ref passed, ref failed, report);
            Run("프롭의 경계 상자가 공칭 크기 안에 있다", TestPropBoundsMatchSpec, ref passed, ref failed, report);
            Run("피벗 규약 — 바닥·벽·천장 접촉면이 원점에 있다", TestPropPivots, ref passed, ref failed, report);

            // ── UV ──────────────────────────────────────────────────────────
            Run("UV 가 월드 미터 기준이다 (uvPerMeter 2배 → UV 2배)", TestUVScalesWithMeters, ref passed, ref failed, report);
            Run("UV 가 크기에 비례한다 (같은 텍셀 밀도)", TestUVIsSizeProportional, ref passed, ref failed, report);
            Run("큰 면의 UV 는 [0,1] 을 넘고 그래도 유한하다 (타일링)", TestUVTilesBeyondUnitSquare, ref passed, ref failed, report);
            Run("프롭 24종의 UV 에 NaN·무한이 없다", TestUVsAreFinite, ref passed, ref failed, report);

            // ── winding 자가 교정 ────────────────────────────────────────────
            Run("정점 순서를 뒤집어도 바깥 힌트가 winding 을 교정한다", TestWindingIsSelfCorrecting, ref passed, ref failed, report);
            Run("TransformSince 가 위치와 법선을 함께 옮긴다", TestTransformMovesNormals, ref passed, ref failed, report);

            // ── 사양 표 ─────────────────────────────────────────────────────
            Run("프롭 24종의 식별자·이름이 중복 없이 대응한다", TestSpecTableIsConsistent, ref passed, ref failed, report);

            // ── 결합기·생성 단계 (여기만 Mesh 를 만든다) ──────────────────────
            Run("결합기가 삼각형 수를 보존하고 변환을 적용한다", TestCombinerPreservesAndTransforms, ref passed, ref failed, report);
            Run("결합기가 flat normal 을 유지한다 (병합하지 않는다)", TestCombinerKeepsFlatNormals, ref passed, ref failed, report);
            Run("생성 단계를 닫으면 메시 생성이 예외로 막힌다", TestBuildPhaseBlocks, ref passed, ref failed, report);

            report.Insert(0, "[상승] === 절차적 메시 (ProcMesh · PropLibrary) Tests ===\n");
            report.Append($"결과: {passed} PASS / {failed} FAIL");
            return (passed, failed, report.ToString());
        }

        private static void Run(string name, Func<string> test,
                                ref int passed, ref int failed, StringBuilder report)
        {
            try
            {
                string failure = test();
                if (failure == null) { passed++; report.AppendLine($"  PASS  {name}"); }
                else { failed++; report.AppendLine($"  FAIL  {name} — {failure}"); }
            }
            catch (Exception e)
            {
                failed++;
                report.AppendLine($"  FAIL  {name} — 예외 {e.GetType().Name}: {e.Message}");
            }
        }

        // ══ 도우미 ═══════════════════════════════════════════════════════════

        private static ProcMeshBuilder BuildProp(PropId id, float uv = 1f)
        {
            var b = new ProcMeshBuilder(512);
            PropLibrary.Build(b, id, uv);
            return b;
        }

        /// <summary>이름 → 빌더. 기본 도구 전부를 한 자리에 모은다.</summary>
        private static readonly (string name, Func<ProcMeshBuilder> make)[] Primitives =
        {
            ("Box(모따기 0)", () => { var b = new ProcMeshBuilder(); ProcMesh.Box(b, new Vector3(0.4f, 0.3f, 0.2f), 0f); return b; }),
            ("Box(모따기 0.02)", () => { var b = new ProcMeshBuilder(); ProcMesh.Box(b, new Vector3(0.4f, 0.3f, 0.2f), 0.02f); return b; }),
            ("Panel", () => { var b = new ProcMeshBuilder(); ProcMesh.Panel(b, 1.6f, 1.2f, 3, 4); return b; }),
            ("Grating", () => { var b = new ProcMeshBuilder(); ProcMesh.Grating(b, 0.8f, 0.6f, 6, 0.02f); return b; }),
            ("Pipe(직선)", () => { var b = new ProcMeshBuilder(); ProcMesh.Pipe(b, 0.03f, 1.0f, 6, 0); return b; }),
            ("Pipe(꺾임 2)", () => { var b = new ProcMeshBuilder(); ProcMesh.Pipe(b, 0.03f, 1.0f, 6, 2); return b; }),
            ("Cable", () => { var b = new ProcMeshBuilder(); ProcMesh.Cable(b, new Vector3(-0.5f, 1f, 0f), new Vector3(0.5f, 1f, 0.2f), 0.18f, 6, 0.012f); return b; }),
            ("Lever", () => { var b = new ProcMeshBuilder(); ProcMesh.Lever(b); return b; }),
            ("Crate", () => { var b = new ProcMeshBuilder(); ProcMesh.Crate(b, new Vector3(0.5f, 0.45f, 0.5f), 3); return b; }),
            ("Bracket", () => { var b = new ProcMeshBuilder(); ProcMesh.Bracket(b); return b; }),
            ("Rivet", () => { var b = new ProcMeshBuilder(); ProcMesh.Rivet(b, Vector3.zero); return b; }),
            ("Hinge", () => { var b = new ProcMeshBuilder(); ProcMesh.Hinge(b); return b; }),
            ("Vent", () => { var b = new ProcMeshBuilder(); ProcMesh.Vent(b); return b; }),
            ("Hook", () => { var b = new ProcMeshBuilder(); ProcMesh.Hook(b); return b; }),
        };

        private static Vector3Int Q(Vector3 p) => new Vector3Int(
            Mathf.RoundToInt(p.x / Quantum),
            Mathf.RoundToInt(p.y / Quantum),
            Mathf.RoundToInt(p.z / Quantum));

        /// <summary>
        /// 방향 있는 모서리가 **정확히 반대쪽 짝을 갖는가**. 이 하나가 두 가지를 동시에 잡는다 —
        /// 닫혀 있지 않으면 짝 없는 모서리가 남고, winding 이 어긋나면 같은 방향이 두 번 나온다.
        ///
        /// 프롭은 닫힌 껍질 여러 개의 합집합이라 「모서리가 정확히 2번」은 성립하지 않는다
        /// (두 상자가 면을 맞대면 4번이 된다). 그래서 **개수 균형**으로 판정한다.
        /// </summary>
        private static string EdgeBalance(ProcMeshBuilder b, string label)
        {
            var ids = new Dictionary<Vector3Int, int>(b.VertexCount);
            var vid = new int[b.VertexCount];
            for (int i = 0; i < b.VertexCount; i++)
            {
                Vector3Int q = Q(b.Positions[i]);
                if (!ids.TryGetValue(q, out int id)) { id = ids.Count; ids[q] = id; }
                vid[i] = id;
            }

            var directed = new Dictionary<long, int>(b.TriangleCount * 3);
            var tris = b.Triangles;
            for (int t = 0; t < tris.Count; t += 3)
            {
                Bump(directed, vid[tris[t]], vid[tris[t + 1]]);
                Bump(directed, vid[tris[t + 1]], vid[tris[t + 2]]);
                Bump(directed, vid[tris[t + 2]], vid[tris[t]]);
            }

            foreach (var kv in directed)
            {
                int a = (int)(kv.Key >> 32);
                int c = (int)(kv.Key & 0xFFFFFFFFL);
                long reverseKey = ((long)c << 32) | (uint)a;
                directed.TryGetValue(reverseKey, out int back);
                if (back != kv.Value)
                    return $"{label}: 모서리 {a}→{c} 가 {kv.Value}회인데 역방향은 {back}회 " +
                           "(열린 껍질이거나 winding 이 어긋났다)";
            }
            return null;
        }

        private static void Bump(Dictionary<long, int> map, int a, int b)
        {
            long key = ((long)a << 32) | (uint)b;
            map.TryGetValue(key, out int n);
            map[key] = n + 1;
        }

        /// <summary>발산 정리로 낸 부호 있는 부피. 바깥 winding 이면 양수다.</summary>
        private static float SignedVolume(ProcMeshBuilder b)
        {
            float v = 0f;
            var tris = b.Triangles;
            for (int t = 0; t < tris.Count; t += 3)
            {
                Vector3 p0 = b.Positions[tris[t]];
                Vector3 p1 = b.Positions[tris[t + 1]];
                Vector3 p2 = b.Positions[tris[t + 2]];
                v += Vector3.Dot(p0, Vector3.Cross(p1, p2));
            }
            return v / 6f;
        }

        private static void Bounds(ProcMeshBuilder b, out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int i = 0; i < b.VertexCount; i++)
            {
                min = Vector3.Min(min, b.Positions[i]);
                max = Vector3.Max(max, b.Positions[i]);
            }
        }

        /// <summary>정점 법선이 면 법선에서 얼마나 벗어났는가(최대 각도, 도).</summary>
        private static float MaxNormalDeviationDeg(ProcMeshBuilder b)
        {
            float worst = 0f;
            var tris = b.Triangles;
            for (int t = 0; t < tris.Count; t += 3)
            {
                Vector3 p0 = b.Positions[tris[t]];
                Vector3 p1 = b.Positions[tris[t + 1]];
                Vector3 p2 = b.Positions[tris[t + 2]];
                Vector3 face = Vector3.Cross(p1 - p0, p2 - p0);
                if (face.sqrMagnitude <= 1e-16f) continue;
                face = face.normalized;
                for (int k = 0; k < 3; k++)
                {
                    Vector3 n = b.Normals[tris[t + k]];
                    float dot = Mathf.Clamp(Vector3.Dot(n, face), -1f, 1f);
                    float deg = Mathf.Acos(dot) * Mathf.Rad2Deg;
                    if (deg > worst) worst = deg;
                }
            }
            return worst;
        }

        private static string CompareBitwise(ProcMeshBuilder a, ProcMeshBuilder b, string label)
        {
            if (a.VertexCount != b.VertexCount) return $"{label}: 정점 수 {a.VertexCount} vs {b.VertexCount}";
            if (a.Triangles.Count != b.Triangles.Count) return $"{label}: 인덱스 수가 다르다";
            for (int i = 0; i < a.VertexCount; i++)
            {
                if (!Identical(a.Positions[i], b.Positions[i])) return $"{label}: 정점 {i} 위치가 다르다";
                if (!Identical(a.Normals[i], b.Normals[i])) return $"{label}: 정점 {i} 법선이 다르다";
                if (Bits(a.UVs[i].x) != Bits(b.UVs[i].x) ||
                    Bits(a.UVs[i].y) != Bits(b.UVs[i].y))
                    return $"{label}: 정점 {i} UV 가 다르다";
            }
            for (int i = 0; i < a.Triangles.Count; i++)
                if (a.Triangles[i] != b.Triangles[i]) return $"{label}: 인덱스 {i} 가 다르다";
            return null;
        }

        /// <summary>
        /// float 의 **원시 비트**. 「비트 동일」을 말하려면 값 비교로는 부족하다 —
        /// `-0f == 0f` 이고 `NaN != NaN` 이라, 값 비교는 결정론 회귀를 놓치거나
        /// 있지도 않은 회귀를 만들어 낸다.
        ///
        /// `BitConverter.SingleToInt32Bits` 를 쓰지 않는 이유: 그건 netstandard 2.1
        /// 전용이라 이 검사를 Mono 프로파일 위(= Unity 를 띄우지 않는 오프라인 하네스)에서
        /// 돌릴 수 없게 만든다. 공용체는 어느 프로파일에서나 돌고 할당도 없다.
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        private struct FloatBits
        {
            [FieldOffset(0)] public float F;
            [FieldOffset(0)] public int I;
        }

        private static int Bits(float f)
        {
            FloatBits u = default(FloatBits);
            u.F = f;
            return u.I;
        }

        private static bool Identical(Vector3 a, Vector3 b)
            => Bits(a.x) == Bits(b.x)
            && Bits(a.y) == Bits(b.y)
            && Bits(a.z) == Bits(b.z);

        private static void DestroyMesh(Mesh m)
        {
            if (m == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(m);
            else UnityEngine.Object.DestroyImmediate(m);
        }

        // ══ 기하 무결성 ══════════════════════════════════════════════════════

        private static string TestPropsAreClosed()
        {
            foreach (PropSpec spec in PropLibrary.Specs)
            {
                string err = EdgeBalance(BuildProp(spec.Id), spec.DisplayName);
                if (err != null) return err;
            }
            return null;
        }

        private static string TestPropsHavePositiveVolume()
        {
            foreach (PropSpec spec in PropLibrary.Specs)
            {
                float v = SignedVolume(BuildProp(spec.Id));
                if (v <= 0f)
                    return $"{spec.DisplayName}: 부피 {v:F6} m³ — 껍질이 통째로 뒤집혔다";
                // 공칭 상자의 0.2% 도 안 되면 「거의 평면」이라는 뜻이고,
                // 그건 형태가 아니라 종이다. 형상 실수를 이 하한이 잡는다.
                float nominal = spec.NominalSize.x * spec.NominalSize.y * spec.NominalSize.z;
                if (v < nominal * 0.002f)
                    return $"{spec.DisplayName}: 부피 {v:F6} m³ 가 공칭 상자 {nominal:F6} m³ 의 0.2% 미만이다";
            }
            return null;
        }

        private static string TestPrimitivesAreClosed()
        {
            foreach (var p in Primitives)
            {
                ProcMeshBuilder b = p.make();
                string err = EdgeBalance(b, p.name);
                if (err != null) return err;
                float v = SignedVolume(b);
                if (v <= 0f) return $"{p.name}: 부피 {v:F6} m³ — 뒤집힌 면이 있다";
            }
            return null;
        }

        private static string TestNoDegenerateTriangles()
        {
            foreach (PropSpec spec in PropLibrary.Specs)
            {
                ProcMeshBuilder b = BuildProp(spec.Id);
                var tris = b.Triangles;
                for (int t = 0; t < tris.Count; t += 3)
                {
                    Vector3 p0 = b.Positions[tris[t]];
                    Vector3 p1 = b.Positions[tris[t + 1]];
                    Vector3 p2 = b.Positions[tris[t + 2]];
                    float area2 = Vector3.Cross(p1 - p0, p2 - p0).magnitude;
                    if (area2 <= 1e-9f)
                        return $"{spec.DisplayName}: 삼각형 {t / 3} 의 넓이가 0 이다";
                }
                for (int i = 0; i < b.VertexCount; i++)
                {
                    float len = b.Normals[i].magnitude;
                    if (Mathf.Abs(len - 1f) > 1e-3f)
                        return $"{spec.DisplayName}: 정점 {i} 의 법선 길이가 {len:F5} 다";
                }
            }
            return null;
        }

        // ══ flat normal (G-4) ════════════════════════════════════════════════

        private static string TestFlatNormalsMatchFaces()
        {
            var b = new ProcMeshBuilder();
            ProcMesh.Box(b, new Vector3(0.5f, 0.4f, 0.3f), 0.05f);
            float dev = MaxNormalDeviationDeg(b);
            if (dev > 0.5f)
                return $"모따기 큐브의 정점 법선이 면 법선에서 최대 {dev:F3}° 벗어났다 — " +
                       "계단 셰이딩이 면이 아니라 그라디언트에 걸린다";
            return null;
        }

        private static string TestAllPropsAreFlat()
        {
            float worst = 0f;
            string worstName = null;
            foreach (PropSpec spec in PropLibrary.Specs)
            {
                float dev = MaxNormalDeviationDeg(BuildProp(spec.Id));
                if (dev > worst) { worst = dev; worstName = spec.DisplayName; }
            }
            if (worst > 0.5f)
                return $"{worstName} 의 법선이 면에서 최대 {worst:F3}° 벗어났다 (G-4 위반)";
            return null;
        }

        private static string TestSmoothActuallyDiffers()
        {
            var flat = new ProcMeshBuilder();
            flat.AddPrism(Vector3.zero, 0.2f, 0.2f, 0.5f, 8, MeshAxis.Y, 0f, true, true, false, 1f);
            var smooth = new ProcMeshBuilder();
            smooth.AddPrism(Vector3.zero, 0.2f, 0.2f, 0.5f, 8, MeshAxis.Y, 0f, true, true, true, 1f);

            float flatDev = MaxNormalDeviationDeg(flat);
            float smoothDev = MaxNormalDeviationDeg(smooth);
            if (flatDev > 0.5f) return $"flat 인데 {flatDev:F3}° 벗어났다";
            // 8각 기둥의 반쪽 각은 22.5° — 부드러운 법선이면 그만큼 벗어나야 한다.
            if (smoothDev < 15f)
                return $"smooth 인데 {smoothDev:F3}° 밖에 안 벗어났다 — 옵션이 실제로 안 걸렸다";
            return null;
        }

        // ══ 결정론 ═══════════════════════════════════════════════════════════

        private static string TestPropsAreDeterministic()
        {
            foreach (PropSpec spec in PropLibrary.Specs)
            {
                string err = CompareBitwise(BuildProp(spec.Id), BuildProp(spec.Id), spec.DisplayName);
                if (err != null) return err;
            }
            return null;
        }

        private static string TestPrimitivesAreDeterministic()
        {
            foreach (var p in Primitives)
            {
                string err = CompareBitwise(p.make(), p.make(), p.name);
                if (err != null) return err;
            }
            return null;
        }

        private static string TestHashIsStable()
        {
            for (int i = -50; i < 200; i++)
            {
                float a = ProcMeshBuilder.Hash01(i);
                float b = ProcMeshBuilder.Hash01(i);
                if (Bits(a) != Bits(b))
                    return $"Hash01({i}) 가 두 번 다르게 나왔다";
                if (a < 0f || a >= 1f) return $"Hash01({i}) = {a} 가 [0,1) 밖이다";
            }
            // 서로 다른 시드가 같은 값으로 뭉치면 「비대칭」이 사라진다.
            var seen = new HashSet<float>();
            for (int i = 0; i < 200; i++) seen.Add(ProcMeshBuilder.Hash01(i));
            if (seen.Count < 190) return $"시드 200개에서 서로 다른 값이 {seen.Count} 개뿐이다";
            return null;
        }

        // ══ 예산 ═════════════════════════════════════════════════════════════

        private static string TestPerPropBudget()
        {
            foreach (PropSpec spec in PropLibrary.Specs)
            {
                int tris = BuildProp(spec.Id).TriangleCount;
                if (tris > PropLibrary.PerPropTriangleLimit)
                    return $"{spec.DisplayName}: {tris} 삼각형 (상한 {PropLibrary.PerPropTriangleLimit})";
                if (tris > spec.TriangleBudget)
                    return $"{spec.DisplayName}: {tris} 삼각형이 자기 예산 {spec.TriangleBudget} 을 넘었다";
            }
            return null;
        }

        private static string TestTotalBudget()
        {
            int total = 0;
            foreach (PropSpec spec in PropLibrary.Specs) total += BuildProp(spec.Id).TriangleCount;
            if (total > PropLibrary.TotalTriangleLimit)
                return $"24종 합계 {total} 삼각형 (상한 {PropLibrary.TotalTriangleLimit})";
            return null;
        }

        private static string TestBevelAddsFaces()
        {
            var plain = new ProcMeshBuilder();
            ProcMesh.Box(plain, Vector3.one, 0f);
            if (plain.TriangleCount != 12) return $"모따기 0 인데 {plain.TriangleCount} 삼각형";

            var bevelled = new ProcMeshBuilder();
            ProcMesh.Box(bevelled, Vector3.one, 0.08f);
            if (bevelled.TriangleCount != 44) return $"모따기 있는데 {bevelled.TriangleCount} 삼각형 (기대 44)";

            // 면의 **종류**가 늘어야 한다. 방향이 다른 법선이 6개뿐이면 그냥 큐브다.
            var dirs = new HashSet<Vector3Int>();
            for (int i = 0; i < bevelled.VertexCount; i++)
            {
                Vector3 n = bevelled.Normals[i];
                dirs.Add(new Vector3Int(Mathf.RoundToInt(n.x * 100f),
                                        Mathf.RoundToInt(n.y * 100f),
                                        Mathf.RoundToInt(n.z * 100f)));
            }
            if (dirs.Count < 26)
                return $"모따기 큐브의 법선 방향이 {dirs.Count} 종뿐이다 (기대 26 = 면 6 + 모서리 12 + 꼭짓점 8)";
            return null;
        }

        private static string TestPropsAreNotSingleCubes()
        {
            foreach (PropSpec spec in PropLibrary.Specs)
            {
                int tris = BuildProp(spec.Id).TriangleCount;
                if (tris < 20)
                    return $"{spec.DisplayName}: {tris} 삼각형 — 사실상 큐브 하나다. " +
                           "균일한 큐브는 로우폴리가 아니라 미완성이다";
            }
            return null;
        }

        // ══ 크기·피벗 ════════════════════════════════════════════════════════

        private static string TestBoxBoundsMatchArgument()
        {
            var sizes = new[]
            {
                new Vector3(1f, 1f, 1f), new Vector3(3.2f, 2.5f, 0.2f), new Vector3(0.04f, 0.9f, 0.31f),
            };
            foreach (Vector3 s in sizes)
            foreach (float bevel in new[] { 0f, 0.01f })
            {
                var b = new ProcMeshBuilder();
                ProcMesh.Box(b, s, bevel);
                Bounds(b, out Vector3 min, out Vector3 max);
                Vector3 actual = max - min;
                if (Vector3.Distance(actual, s) > 1e-4f)
                    return $"Box({s}) 모따기 {bevel} → 실제 {actual}";
                if (Vector3.Distance((min + max) * 0.5f, Vector3.zero) > 1e-5f)
                    return $"Box({s}) 의 중심이 원점이 아니다";
            }
            return null;
        }

        private static string TestBoxVolumeMatchesArgument()
        {
            var b = new ProcMeshBuilder();
            ProcMesh.Box(b, new Vector3(0.6f, 0.4f, 0.25f), 0f);
            float expected = 0.6f * 0.4f * 0.25f;
            float actual = SignedVolume(b);
            if (Mathf.Abs(actual - expected) > expected * 1e-3f)
                return $"부피 {actual:F6} m³, 기대 {expected:F6} m³";

            // 모따기는 부피를 **깎는다.** 늘어나면 모서리 조각이 밖으로 튀어나온 것이다.
            var bev = new ProcMeshBuilder();
            ProcMesh.Box(bev, new Vector3(0.6f, 0.4f, 0.25f), 0.05f);
            float bevVolume = SignedVolume(bev);
            if (bevVolume >= expected) return $"모따기 후 부피 {bevVolume:F6} 가 안 줄었다";
            if (bevVolume < expected * 0.8f) return $"모따기 후 부피 {bevVolume:F6} 가 20% 넘게 깎였다";
            return null;
        }

        private static string TestPropBoundsMatchSpec()
        {
            foreach (PropSpec spec in PropLibrary.Specs)
            {
                ProcMeshBuilder b = BuildProp(spec.Id);
                Bounds(b, out Vector3 min, out Vector3 max);
                Vector3 size = max - min;
                for (int axis = 0; axis < 3; axis++)
                {
                    float actual = size[axis];
                    float nominal = spec.NominalSize[axis];
                    if (actual > nominal + 0.002f)
                        return $"{spec.DisplayName}: {"XYZ"[axis]} 축 {actual:F3}m 가 공칭 {nominal:F3}m 를 넘었다";
                    // 공칭의 절반도 안 되면 사양 표가 실제 형상을 설명하지 못한다 —
                    // 그러면 씬 소유자가 배치 간격을 잘못 잡는다.
                    if (actual < nominal * 0.5f)
                        return $"{spec.DisplayName}: {"XYZ"[axis]} 축 {actual:F3}m 가 공칭 {nominal:F3}m 의 절반 미만이다";
                }
            }
            return null;
        }

        private static string TestPropPivots()
        {
            foreach (PropSpec spec in PropLibrary.Specs)
            {
                ProcMeshBuilder b = BuildProp(spec.Id);
                Bounds(b, out Vector3 min, out Vector3 max);
                switch (spec.Mount)
                {
                    case PropMount.Floor:
                        if (Mathf.Abs(min.y) > ContactTolerance)
                            return $"{spec.DisplayName}(바닥): 최저점 y = {min.y:F4} (0 이어야 한다)";
                        break;
                    case PropMount.Wall:
                        if (Mathf.Abs(min.z) > ContactTolerance)
                            return $"{spec.DisplayName}(벽): 최소 z = {min.z:F4} (0 이어야 한다)";
                        if (max.z <= 0f)
                            return $"{spec.DisplayName}(벽): 밖으로(+Z) 나오지 않았다";
                        break;
                    case PropMount.Ceiling:
                        if (Mathf.Abs(max.y) > ContactTolerance)
                            return $"{spec.DisplayName}(천장): 최고점 y = {max.y:F4} (0 이어야 한다)";
                        if (min.y >= 0f)
                            return $"{spec.DisplayName}(천장): 아래로(−Y) 내려오지 않았다";
                        break;
                }
            }
            return null;
        }

        // ══ UV ═══════════════════════════════════════════════════════════════

        private static Vector2 UVSpan(ProcMeshBuilder b)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < b.VertexCount; i++)
            {
                min = Vector2.Min(min, b.UVs[i]);
                max = Vector2.Max(max, b.UVs[i]);
            }
            return max - min;
        }

        private static string TestUVScalesWithMeters()
        {
            var one = new ProcMeshBuilder();
            ProcMesh.Box(one, new Vector3(2f, 1f, 0.5f), 0f, 1f);
            var two = new ProcMeshBuilder();
            ProcMesh.Box(two, new Vector3(2f, 1f, 0.5f), 0f, 2f);

            Vector2 a = UVSpan(one), c = UVSpan(two);
            if (Mathf.Abs(c.x - a.x * 2f) > 1e-4f || Mathf.Abs(c.y - a.y * 2f) > 1e-4f)
                return $"uvPerMeter 1 → {a}, 2 → {c} (2배가 아니다)";

            // 1 m 당 1회 반복이면 2m × 1m 상자의 UV 폭이 정확히 2·1 이어야 한다.
            if (Mathf.Abs(a.x - 2f) > 1e-4f || Mathf.Abs(a.y - 1f) > 1e-4f)
                return $"uvPerMeter 1 인데 2×1×0.5 상자의 UV 범위가 {a} 다 (기대 (2, 1))";
            return null;
        }

        private static string TestUVIsSizeProportional()
        {
            // 큰 벽과 작은 리벳이 **같은 텍셀 밀도**를 가져야 한다. 이것이 안 되면
            // 3.2m 벽에 128×128 텍스처가 늘어나 뭉개진다 (G-1 국소 분산이 안 오른다).
            var big = new ProcMeshBuilder();
            ProcMesh.Box(big, new Vector3(3.2f, 2.5f, 0.2f), 0f, 1f);
            var small = new ProcMeshBuilder();
            ProcMesh.Box(small, new Vector3(0.32f, 0.25f, 0.02f), 0f, 1f);

            Vector2 a = UVSpan(big), c = UVSpan(small);
            if (Mathf.Abs(a.x - c.x * 10f) > 1e-3f || Mathf.Abs(a.y - c.y * 10f) > 1e-3f)
                return $"10배 크기인데 UV 범위가 {a} vs {c} (10배가 아니다)";
            return null;
        }

        private static string TestUVTilesBeyondUnitSquare()
        {
            var b = new ProcMeshBuilder();
            ProcMesh.Panel(b, 3.2f, 2.5f, 4, 5, 0.06f, 1f);
            Vector2 span = UVSpan(b);
            if (span.x <= 1f || span.y <= 1f)
                return $"3.2×2.5m 패널의 UV 범위가 {span} — [0,1] 을 안 넘으면 타일링이 아니다";
            for (int i = 0; i < b.VertexCount; i++)
            {
                Vector2 uv = b.UVs[i];
                if (float.IsNaN(uv.x) || float.IsNaN(uv.y) || float.IsInfinity(uv.x) || float.IsInfinity(uv.y))
                    return $"정점 {i} 의 UV 가 {uv} 다";
            }
            return null;
        }

        private static string TestUVsAreFinite()
        {
            foreach (PropSpec spec in PropLibrary.Specs)
            {
                ProcMeshBuilder b = BuildProp(spec.Id);
                for (int i = 0; i < b.VertexCount; i++)
                {
                    Vector2 uv = b.UVs[i];
                    if (float.IsNaN(uv.x) || float.IsNaN(uv.y) ||
                        float.IsInfinity(uv.x) || float.IsInfinity(uv.y))
                        return $"{spec.DisplayName}: 정점 {i} 의 UV 가 {uv} 다";
                }
            }
            return null;
        }

        // ══ winding 자가 교정 ════════════════════════════════════════════════

        private static string TestWindingIsSelfCorrecting()
        {
            Vector3 a = new Vector3(-1f, 0f, -1f), b2 = new Vector3(1f, 0f, -1f);
            Vector3 c = new Vector3(1f, 0f, 1f), d = new Vector3(-1f, 0f, 1f);

            var forward = new ProcMeshBuilder();
            forward.AddQuad(a, b2, c, d, Vector3.up, 1f);
            var reversed = new ProcMeshBuilder();
            reversed.AddQuad(a, d, c, b2, Vector3.up, 1f);

            for (int i = 0; i < forward.VertexCount; i++)
                if (Vector3.Dot(forward.Normals[i], Vector3.up) < 0.999f)
                    return $"정방향 입력의 법선이 {forward.Normals[i]} 다";
            for (int i = 0; i < reversed.VertexCount; i++)
                if (Vector3.Dot(reversed.Normals[i], Vector3.up) < 0.999f)
                    return $"역방향 입력의 법선이 {reversed.Normals[i]} 다 — 바깥 힌트가 교정하지 못했다";

            if (SignedVolume(forward) * SignedVolume(reversed) < 0f)
                return "두 입력의 winding 이 서로 반대다";
            return null;
        }

        private static string TestTransformMovesNormals()
        {
            var b = new ProcMeshBuilder();
            int mark = b.Mark();
            b.AddBox(Vector3.zero, Vector3.one, 0f, 1f);
            b.TransformSince(mark, new Vector3(2f, 0f, 0f), ProcQuat.Euler(0f, 90f, 0f));

            Bounds(b, out Vector3 min, out Vector3 max);
            Vector3 center = (min + max) * 0.5f;
            if (Vector3.Distance(center, new Vector3(2f, 0f, 0f)) > 1e-4f)
                return $"이동 후 중심이 {center} 다 (기대 (2,0,0))";

            // 회전을 위치에만 적용했다면 법선 집합은 그대로 축 정렬로 남는다.
            // 90° 회전 뒤에도 여전히 6방향이지만, **어느 정점이 어느 법선을 갖는지**가 바뀐다.
            // 그래서 「위치와 법선이 함께 돌았는가」는 둘의 대응으로 본다.
            float dev = MaxNormalDeviationDeg(b);
            if (dev > 0.5f)
                return $"회전 후 정점 법선이 면 법선에서 {dev:F3}° 벗어났다 — 법선이 같이 안 돌았다";
            return null;
        }

        // ══ 사양 표 ══════════════════════════════════════════════════════════

        private static string TestSpecTableIsConsistent()
        {
            var specs = PropLibrary.Specs;
            if (specs.Count != PropLibrary.PropCount)
                return $"사양 {specs.Count} 개, 선언 {PropLibrary.PropCount} 개";
            if (specs.Count != Enum.GetValues(typeof(PropId)).Length)
                return $"사양 {specs.Count} 개, PropId {Enum.GetValues(typeof(PropId)).Length} 개";
            if (specs.Count < 24)
                return $"프롭이 {specs.Count} 종 — GRAPHICS_TARGET §G-5 가 24 종 이상을 요구한다";

            var names = new HashSet<string>();
            for (int i = 0; i < specs.Count; i++)
            {
                if ((int)specs[i].Id != i)
                    return $"{i} 번 자리에 {specs[i].Id} 가 있다 — 표 순서가 열거형 순서와 어긋났다";
                if (string.IsNullOrWhiteSpace(specs[i].DisplayName))
                    return $"{specs[i].Id} 의 이름이 비었다";
                if (!names.Add(specs[i].DisplayName))
                    return $"이름 '{specs[i].DisplayName}' 이 중복이다 — 「고유 프롭 종 수」가 부풀려진다";
                if (specs[i].TriangleBudget > PropLibrary.PerPropTriangleLimit)
                    return $"{specs[i].Id} 의 예산 {specs[i].TriangleBudget} 이 상한을 넘는다";
                if (specs[i].NominalSize.x <= 0f || specs[i].NominalSize.y <= 0f || specs[i].NominalSize.z <= 0f)
                    return $"{specs[i].Id} 의 공칭 크기에 0 이하가 있다";
                // 사양 표를 GetSpec 으로도 같게 읽을 수 있어야 한다.
                if (PropLibrary.GetSpec(specs[i].Id).DisplayName != specs[i].DisplayName)
                    return $"GetSpec({specs[i].Id}) 가 다른 항목을 돌려준다";
            }
            return null;
        }

        // ══ 결합기·생성 단계 ═════════════════════════════════════════════════

        private static string TestCombinerPreservesAndTransforms()
        {
            MeshBuildPhase.ResetForTests();
            Mesh box = null, combined = null;
            try
            {
                box = ProcMesh.Box(new Vector3(0.2f, 0.2f, 0.2f), 0f);
                int boxTris = box.triangles.Length / 3;

                var entries = new List<MeshCombiner.Entry>
                {
                    new MeshCombiner.Entry(box, new Vector3(-1f, 0f, 0f), Quaternion.identity),
                    new MeshCombiner.Entry(box, new Vector3(1f, 0f, 0f), Quaternion.identity),
                    new MeshCombiner.Entry(box, new Vector3(0f, 2f, 0f), ProcQuat.Euler(0f, 45f, 0f)),
                };
                combined = MeshCombiner.Combine(entries, "TEST_Combined");

                int got = combined.triangles.Length / 3;
                if (got != boxTris * 3) return $"삼각형 {got} 개, 기대 {boxTris * 3} 개";
                if (MeshCombiner.LastSourceCount != 3) return $"원본 수 {MeshCombiner.LastSourceCount}";

                var bounds = combined.bounds;
                if (Mathf.Abs(bounds.min.x + 1.1f) > 1e-3f)
                    return $"결합 경계 최소 x = {bounds.min.x:F4} (기대 −1.1) — 변환이 안 걸렸다";
                if (Mathf.Abs(bounds.max.y - 2.1f) > 1e-3f)
                    return $"결합 경계 최대 y = {bounds.max.y:F4} (기대 2.1)";
                return null;
            }
            finally
            {
                DestroyMesh(combined);
                DestroyMesh(box);
                MeshBuildPhase.ResetForTests();
            }
        }

        private static string TestCombinerKeepsFlatNormals()
        {
            MeshBuildPhase.ResetForTests();
            Mesh src = null, combined = null;
            try
            {
                // 8각 기둥은 이웃한 옆면이 45° 로 만난다. 결합기가 정점을 병합하면
                // 그 45° 가 평균되어 계단이 사라진다 — 그것이 이 검사가 반증하는 것이다.
                src = new ProcMeshBuilder().Also(b => b.AddPrism(Vector3.zero, 0.2f, 0.2f, 0.4f, 8,
                                                                MeshAxis.Y, 0f, true, true, false, 1f))
                                           .ToMesh("TEST_Prism");
                var entries = new List<MeshCombiner.Entry>
                {
                    new MeshCombiner.Entry(src, Vector3.zero, Quaternion.identity),
                };
                combined = MeshCombiner.Combine(entries, "TEST_CombinedPrism");

                Vector3[] verts = combined.vertices;
                Vector3[] norms = combined.normals;
                int[] tris = combined.triangles;
                float worst = 0f;
                for (int t = 0; t < tris.Length; t += 3)
                {
                    Vector3 p0 = verts[tris[t]], p1 = verts[tris[t + 1]], p2 = verts[tris[t + 2]];
                    Vector3 face = Vector3.Cross(p1 - p0, p2 - p0);
                    if (face.sqrMagnitude <= 1e-16f) continue;
                    face = face.normalized;
                    for (int k = 0; k < 3; k++)
                    {
                        float dot = Mathf.Clamp(Vector3.Dot(norms[tris[t + k]], face), -1f, 1f);
                        worst = Mathf.Max(worst, Mathf.Acos(dot) * Mathf.Rad2Deg);
                    }
                }
                if (worst > 0.5f)
                    return $"결합 후 법선이 면에서 {worst:F3}° 벗어났다 — 정점이 병합돼 flat 이 깨졌다";
                return null;
            }
            finally
            {
                DestroyMesh(combined);
                DestroyMesh(src);
                MeshBuildPhase.ResetForTests();
            }
        }

        private static string TestBuildPhaseBlocks()
        {
            MeshBuildPhase.ResetForTests();
            Mesh open = null;
            try
            {
                if (!MeshBuildPhase.IsOpen) return "초기 상태가 닫혀 있다";
                open = ProcMesh.Box(Vector3.one, 0f);
                if (open == null) return "열린 상태인데 메시가 안 만들어졌다";
                if (MeshBuildPhase.MeshesCreated != 1)
                    return $"생성 수가 {MeshBuildPhase.MeshesCreated} 다 (기대 1)";

                MeshBuildPhase.Close();
                bool threw = false;
                try { DestroyMesh(ProcMesh.Box(Vector3.one, 0f)); }
                catch (InvalidOperationException) { threw = true; }
                if (!threw) return "닫힌 뒤에도 메시가 만들어졌다 — 프레임 루프 할당을 막지 못한다";
                if (MeshBuildPhase.ViolationCount != 1)
                    return $"위반 수가 {MeshBuildPhase.ViolationCount} 다 (기대 1)";
                if (string.IsNullOrEmpty(MeshBuildPhase.LastViolation))
                    return "위반 설명이 비었다 — 어디였는지 알 수 없다";

                MeshBuildPhase.Open();
                if (!MeshBuildPhase.IsOpen) return "Open() 이 다시 열지 못했다";
                return null;
            }
            finally
            {
                DestroyMesh(open);
                MeshBuildPhase.ResetForTests();
            }
        }
    }

    /// <summary>테스트에서만 쓰는 짧은 연쇄 도우미.</summary>
    internal static class ProcMeshTestExtensions
    {
        public static ProcMeshBuilder Also(this ProcMeshBuilder b, Action<ProcMeshBuilder> act)
        {
            act(b);
            return b;
        }
    }
}
