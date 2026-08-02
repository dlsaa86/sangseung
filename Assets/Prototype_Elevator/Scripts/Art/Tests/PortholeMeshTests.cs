using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Ascend.Prototype.Art.Tests
{
    /// <summary>
    /// 「원형 현창 3×3」 장치벽(<see cref="PortholeMesh"/>)과 주변 부품
    /// (<see cref="ConsolePropMesh"/>)의 헤드리스 검증.
    ///
    /// ## 이 파일이 반증하려는 것은 두 가지다
    ///
    /// **① 기하 무결성** — `ProcMeshTests` 와 같은 종류다. 뒤집힌 면, 열린 껍질,
    /// 부드러워진 법선, 어긋난 크기는 전부 「컴파일도 되고 예외도 안 나는데 화면에서만
    /// 틀린」 것이고, 이 저장소가 가장 여러 번 당한 실패의 형태가 그것이다.
    ///
    /// **② 「릴로 안 보인다」는 주장** — 이쪽이 이 파일의 존재 이유다.
    /// 16차 독립 평가는 11라운드 연속으로 「세로 발광 릴 3개」를 지적했고, 그 지적은
    /// **형상에서 왔지 색에서 오지 않았다.** 그러므로 반증도 형상에서 해야 한다.
    /// 아래 세 검사가 그 주장을 기하에서 직접 잰다.
    ///
    /// | 검사 | 무엇을 재는가 | 실패하면 무엇이 돌아오는가 |
    /// |---|---|---|
    /// | 리브 굵기비 | 가로 리브의 실측 굵기 ≥ 세로 리브 | 굵은 선이 세로가 되어 시선이 열을 훑는다 |
    /// | 가로 연속 · 세로 단절 | 가로 리브의 x 범위 = 캐비닛 폭 / 세로 리브의 y 총 길이 < 격자 높이 | 이어진 세로선이 「기둥」을 만든다 |
    /// | 개구부가 비어 있다 | 개구부 반지름 안에 판 정점이 **하나도** 없다 | 베젤·리브가 창을 잡아먹어 심볼이 사라진다 |
    ///
    /// ## 씬도 Mesh 객체도 만들지 않는다
    ///
    /// 판정은 전부 <see cref="ProcMeshBuilder"/> 의 배열에서 한다. <see cref="Mesh"/> 는
    /// 네이티브 객체라 EditMode 에서 만들면 회수를 손으로 해야 하고, 흘리면
    /// 「테스트는 통과했는데 에디터가 무거워진다」로만 나타난다.
    ///
    /// NUnit 에 의존하지 않는 이유는 `DECISION_LOG.md` D-20260730-06 참조.
    /// </summary>
    public static class PortholeMeshTests
    {
        /// <summary>같은 점으로 볼 간격(m). 0.1mm — `ProcMeshTests` 와 같은 값이다.</summary>
        private const float Quantum = 1e-4f;

        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0;
            int failed = 0;
            var report = new StringBuilder();

            // ── 기하 무결성 ──────────────────────────────────────────────────
            Run("장치 메시 3종이 전부 닫혀 있다 (모서리 균형)", TestDeviceClosed, ref passed, ref failed, report);
            Run("장치 메시 3종의 부호 있는 부피가 양수다 (뒤집힌 면 0)", TestDevicePositiveVolume, ref passed, ref failed, report);
            Run("주변 부품 8종이 닫혀 있고 뒤집힌 면이 없다", TestConsolePropsClosed, ref passed, ref failed, report);
            Run("퇴화 삼각형이 없고 법선이 전부 단위 벡터다", TestNoDegenerates, ref passed, ref failed, report);

            // ── G-4 전제: flat normal ────────────────────────────────────────
            Run("flat normal — 정점 법선이 면 법선과 일치한다 (11종 전부)", TestFlatNormals, ref passed, ref failed, report);

            // ── 결정론 ──────────────────────────────────────────────────────
            Run("결정론 — 두 번 만들면 비트 동일하다 (11종 전부)", TestDeterminism, ref passed, ref failed, report);

            // ── 「릴로 안 보인다」의 형상 근거 ────────────────────────────────
            Run("개구부 9개가 정확히 3×3 격자이고 피치가 인자와 일치한다", TestGridIsThreeByThree, ref passed, ref failed, report);
            Run("격자가 **정사각**이다 (대각선이 45°)", TestGridIsSquare, ref passed, ref failed, report);
            Run("가로 리브 굵기 ≥ 세로 리브 굵기 (실측)", TestHorizontalRibIsThicker, ref passed, ref failed, report);
            Run("가로 리브는 폭 전체를 지나고 세로 리브는 끊긴다 (실측)", TestVerticalRibsAreInterrupted, ref passed, ref failed, report);
            Run("하단 원형 해치가 가로 띠를 하나 더 만든다", TestHatchBandExists, ref passed, ref failed, report);

            // ── 판독성 ──────────────────────────────────────────────────────
            Run("베젤이 개구부 지름 대비 상한을 넘지 않는다", TestBezelWithinReadabilityCap, ref passed, ref failed, report);
            Run("개구부 안에 판 정점이 하나도 없다 (창이 실제로 비어 있다)", TestOpeningsAreEmpty, ref passed, ref failed, report);

            // ── 우물·유리 ────────────────────────────────────────────────────
            Run("우물 안쪽 벽의 법선이 안을 본다", TestWellNormalsPointInward, ref passed, ref failed, report);
            Run("우물이 개구부 안에 들어가고 판 뒤로 뚫고 나가지 않는다", TestWellFitsInsideBore, ref passed, ref failed, report);
            Run("유리가 개구부 안에 있고 앞으로 볼록하다", TestGlassIsConvexAndInside, ref passed, ref failed, report);
            Run("우물이 발광체를 담을 만큼 깊다 (SymbolFitSize · SymbolSeat)", TestSymbolFits, ref passed, ref failed, report);

            // ── 크기·피벗 ────────────────────────────────────────────────────
            Run("캐비닛 경계 상자가 PanelSize 와 정확히 일치한다", TestPanelBoundsMatchSpec, ref passed, ref failed, report);
            Run("주변 부품의 벽 접촉면이 원점에 있다", TestConsolePropPivots, ref passed, ref failed, report);
            Run("위험판 사선 줄무늬가 판 밖으로 나가지 않는다", TestHazardStripesStayInside, ref passed, ref failed, report);

            // ── 예산 ────────────────────────────────────────────────────────
            Run("장치 메시 3종이 선언한 삼각형 예산 안에 있다", TestDeviceBudgets, ref passed, ref failed, report);
            Run("주변 부품 8종이 300 삼각형을 넘지 않는다", TestConsolePropBudgets, ref passed, ref failed, report);
            Run("인자를 줄이면 삼각형이 실제로 줄어든다", TestBudgetKnobsWork, ref passed, ref failed, report);

            // ── 인자 방어 ────────────────────────────────────────────────────
            Run("Clamped 가 판독·릴 회피 규칙을 강제한다", TestClampEnforcesRules, ref passed, ref failed, report);

            // ── UP-FIX-03: 두 레버가 닮지 않았다 ─────────────────────────────
            Run("과수확 슬롯과 실행 레버의 지배축이 다르다 (UP-FIX-03)", TestLeverSilhouettesDiffer, ref passed, ref failed, report);

            report.Insert(0, "[상승] === 원형 현창 3×3 (PortholeMesh · ConsolePropMesh) Tests ===\n");
            report.AppendLine();
            report.AppendLine(Measurements());
            report.Append($"결과: {passed} PASS / {failed} FAIL");
            return (passed, failed, report.ToString());
        }

        /// <summary>
        /// 통과·실패와 무관하게 **숫자를 남긴다.** 예산은 「넘지 않았다」보다
        /// 「얼마였다」가 회귀 추적에 쓸모 있고, 보고서에 옮겨 적을 값이기도 하다.
        /// </summary>
        private static string Measurements()
        {
            PortholeSpec s = PortholeMesh.DefaultSpec;
            Vector3 size = PortholeMesh.PanelSize(s);
            PortholeSpec c = s.Clamped();
            var sb = new StringBuilder();
            sb.AppendLine("  [실측]");
            PortholeSpec lean = PortholeMesh.DefaultSpec;
            lean.BoltsPerBezel = 0;
            lean.RivetsPerBand = 3;
            lean.FrameBevel = 0f;
            sb.AppendLine($"    PortholePanel      {Panel().TriangleCount,5} 삼각형  (예산 {PortholeMesh.PanelTriangleBudget})");
            sb.AppendLine($"      └ 볼트 0 · 리벳 3 · 모따기 0 → {Panel(lean).TriangleCount} 삼각형 (원거리·저사양 대안)");
            sb.AppendLine($"    PortholeWell x9    {WellCluster().TriangleCount,5} 삼각형  (예산 {PortholeMesh.WellClusterTriangleBudget})");
            sb.AppendLine($"    PortholeGlass x9   {GlassCluster().TriangleCount,5} 삼각형  (예산 {PortholeMesh.GlassClusterTriangleBudget})");
            foreach (var p in ConsoleProps)
                sb.AppendLine($"    {p.name,-22}{p.make().TriangleCount,5} 삼각형");
            sb.AppendLine($"    캐비닛 {size.x:F3} × {size.y:F3} × {size.z:F3} m · 격자 피치 {c.CellPitch:F3} m · " +
                          $"개구부 지름 {c.OpeningRadius * 2f:F3} m");
            Vector2 fit = PortholeMesh.SymbolFitSize(s);
            sb.AppendLine($"    우물 바닥 z {PortholeMesh.WellFloorZ(s):F3} m · 입 z {PortholeMesh.CellMouth(s, 1, 1).z:F3} m · " +
                          $"심볼 최대 {fit.x:F3} × {fit.y:F3} m");
            sb.AppendLine($"    리브 가로 {c.HorizontalRibWidth * 1000f:F1} mm : 세로 {c.VerticalRibWidth * 1000f:F1} mm " +
                          $"(비 {c.HorizontalRibBoost:F2}) · 베젤/지름 {c.BezelToOpeningDiameter:F3} " +
                          $"(상한 {PortholeMesh.MaxBezelToOpeningDiameter:F2})");
            return sb.ToString();
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

        // ══ 빌더 ═════════════════════════════════════════════════════════════

        private static ProcMeshBuilder Panel(PortholeSpec? spec = null)
        {
            var b = new ProcMeshBuilder(4096);
            PortholeMesh.Panel(b, spec ?? PortholeMesh.DefaultSpec);
            return b;
        }

        private static ProcMeshBuilder WellCluster(PortholeSpec? spec = null)
        {
            var b = new ProcMeshBuilder(1024);
            PortholeMesh.WellCluster(b, spec ?? PortholeMesh.DefaultSpec);
            return b;
        }

        private static ProcMeshBuilder GlassCluster(PortholeSpec? spec = null)
        {
            var b = new ProcMeshBuilder(1024);
            PortholeMesh.GlassCluster(b, spec ?? PortholeMesh.DefaultSpec);
            return b;
        }

        private static readonly (string name, Func<ProcMeshBuilder> make)[] DeviceMeshes =
        {
            ("PortholePanel", () => Panel()),
            ("PortholeWell x9", () => WellCluster()),
            ("PortholeGlass x9", () => GlassCluster()),
        };

        /// <summary>주변 부품. 이름이 보고서와 실패 메시지에 그대로 나간다.</summary>
        private static readonly (string name, Func<ProcMeshBuilder> make, bool wallContact)[] ConsoleProps =
        {
            ("MushroomButton", () => { var b = new ProcMeshBuilder(256); ConsolePropMesh.MushroomButton(b); return b; }, true),
            ("LeverSlotHousing", () => { var b = new ProcMeshBuilder(384); ConsolePropMesh.LeverSlotHousing(b); return b; }, true),
            ("LeverSlotHandle", () => { var b = new ProcMeshBuilder(192); ConsolePropMesh.LeverSlotHandle(b); return b; }, true),
            ("GaugeBezel", () => { var b = new ProcMeshBuilder(384); ConsolePropMesh.GaugeBezel(b); return b; }, true),
            ("FloorIndicatorHousing", () => { var b = new ProcMeshBuilder(384); ConsolePropMesh.FloorIndicatorHousing(b); return b; }, true),
            ("FloorIndicatorArrows", () => { var b = new ProcMeshBuilder(96); ConsolePropMesh.FloorIndicatorArrows(b); return b; }, true),
            ("HazardPlateBase", () => { var b = new ProcMeshBuilder(256); ConsolePropMesh.HazardPlateBase(b); return b; }, true),
            // 스트라이프는 바탕판 **위에** 얹히므로 z = 0 에서 시작하지 않는다.
            ("HazardPlateStripes", () => { var b = new ProcMeshBuilder(256); ConsolePropMesh.HazardPlateStripes(b); return b; }, false),
        };

        // ══ 도우미 ═══════════════════════════════════════════════════════════

        private static Vector3Int Q(Vector3 p) => new Vector3Int(
            Mathf.RoundToInt(p.x / Quantum),
            Mathf.RoundToInt(p.y / Quantum),
            Mathf.RoundToInt(p.z / Quantum));

        /// <summary>
        /// 방향 있는 모서리가 정확히 반대쪽 짝을 갖는가. 열린 껍질과 어긋난 winding 을
        /// 동시에 잡는다. 자세한 근거는 `ProcMeshTests.EdgeBalance` 와 같다.
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
            => Bits(a.x) == Bits(b.x) && Bits(a.y) == Bits(b.y) && Bits(a.z) == Bits(b.z);

        private static string CompareBitwise(ProcMeshBuilder a, ProcMeshBuilder b, string label)
        {
            if (a.VertexCount != b.VertexCount) return $"{label}: 정점 수 {a.VertexCount} vs {b.VertexCount}";
            if (a.Triangles.Count != b.Triangles.Count) return $"{label}: 인덱스 수가 다르다";
            for (int i = 0; i < a.VertexCount; i++)
            {
                if (!Identical(a.Positions[i], b.Positions[i])) return $"{label}: 정점 {i} 위치가 다르다";
                if (!Identical(a.Normals[i], b.Normals[i])) return $"{label}: 정점 {i} 법선이 다르다";
                if (Bits(a.UVs[i].x) != Bits(b.UVs[i].x) || Bits(a.UVs[i].y) != Bits(b.UVs[i].y))
                    return $"{label}: 정점 {i} UV 가 다르다";
            }
            for (int i = 0; i < a.Triangles.Count; i++)
                if (a.Triangles[i] != b.Triangles[i]) return $"{label}: 인덱스 {i} 가 다르다";
            return null;
        }

        /// <summary>
        /// 가까운 값을 하나로 접어 **서로 다른 값 목록**을 만든다.
        ///
        /// 리브를 「묶음」으로 세면 안 되는 이유: 리브 하나의 두 모서리 사이 간격(굵기)이
        /// 리브 **사이** 간격보다 클 수도 작을 수도 있어서, 어떤 임계값을 골라도 한쪽이
        /// 틀린다. 대신 정점 값 자체를 세면 「막대 하나 = 값 두 개」가 항상 성립한다.
        /// </summary>
        private static List<float> DistinctValues(List<float> values, float tol)
        {
            values.Sort();
            var result = new List<float>();
            for (int i = 0; i < values.Count; i++)
                if (result.Count == 0 || values[i] - result[result.Count - 1] > tol)
                    result.Add(values[i]);
            return result;
        }

        /// <summary>값 목록을 <paramref name="gap"/> 보다 먼 간격에서 끊어 묶음으로 만든다.</summary>
        private static List<(float min, float max)> Cluster(List<float> values, float gap)
        {
            var result = new List<(float, float)>();
            if (values.Count == 0) return result;
            values.Sort();
            float lo = values[0], hi = values[0];
            for (int i = 1; i < values.Count; i++)
            {
                if (values[i] - hi > gap) { result.Add((lo, hi)); lo = values[i]; }
                hi = values[i];
            }
            result.Add((lo, hi));
            return result;
        }

        /// <summary>z 가 <paramref name="z"/> 인 평면 위의 정점만 고른다.</summary>
        private static List<Vector3> AtPlane(ProcMeshBuilder b, float z, float tol)
        {
            var list = new List<Vector3>();
            for (int i = 0; i < b.VertexCount; i++)
                if (Mathf.Abs(b.Positions[i].z - z) <= tol) list.Add(b.Positions[i]);
            return list;
        }

        private static Vector3[] CellCenters(PortholeSpec s)
        {
            var c = new Vector3[9];
            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
                c[row * 3 + col] = PortholeMesh.CellCenter(s, col, row);
            return c;
        }

        private static int NearestCell(Vector3[] centers, Vector3 p, out float distXY)
        {
            int best = 0;
            distXY = float.MaxValue;
            for (int i = 0; i < centers.Length; i++)
            {
                float dx = p.x - centers[i].x, dy = p.y - centers[i].y;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < distXY) { distXY = d; best = i; }
            }
            return best;
        }

        // ══ 기하 무결성 ══════════════════════════════════════════════════════

        private static string TestDeviceClosed()
        {
            foreach (var m in DeviceMeshes)
            {
                string err = EdgeBalance(m.make(), m.name);
                if (err != null) return err;
            }
            return null;
        }

        private static string TestDevicePositiveVolume()
        {
            foreach (var m in DeviceMeshes)
            {
                float v = SignedVolume(m.make());
                if (v <= 0f) return $"{m.name}: 부피 {v:F6} m³ — 껍질이 통째로 뒤집혔다";
            }
            return null;
        }

        private static string TestConsolePropsClosed()
        {
            foreach (var p in ConsoleProps)
            {
                ProcMeshBuilder b = p.make();
                string err = EdgeBalance(b, p.name);
                if (err != null) return err;
                float v = SignedVolume(b);
                if (v <= 0f) return $"{p.name}: 부피 {v:F6} m³ — 뒤집힌 면이 있다";
            }
            return null;
        }

        private static string TestNoDegenerates()
        {
            foreach (var m in AllBuilders())
            {
                ProcMeshBuilder b = m.make();
                var tris = b.Triangles;
                for (int t = 0; t < tris.Count; t += 3)
                {
                    Vector3 p0 = b.Positions[tris[t]];
                    Vector3 p1 = b.Positions[tris[t + 1]];
                    Vector3 p2 = b.Positions[tris[t + 2]];
                    if (Vector3.Cross(p1 - p0, p2 - p0).magnitude <= 1e-9f)
                        return $"{m.name}: 삼각형 {t / 3} 의 넓이가 0 이다";
                }
                for (int i = 0; i < b.VertexCount; i++)
                {
                    float len = b.Normals[i].magnitude;
                    if (Mathf.Abs(len - 1f) > 1e-3f)
                        return $"{m.name}: 정점 {i} 의 법선 길이가 {len:F5} 다";
                    Vector2 uv = b.UVs[i];
                    if (float.IsNaN(uv.x) || float.IsNaN(uv.y) ||
                        float.IsInfinity(uv.x) || float.IsInfinity(uv.y))
                        return $"{m.name}: 정점 {i} 의 UV 가 {uv} 다";
                }
            }
            return null;
        }

        private static IEnumerable<(string name, Func<ProcMeshBuilder> make)> AllBuilders()
        {
            foreach (var m in DeviceMeshes) yield return m;
            foreach (var p in ConsoleProps) yield return (p.name, p.make);
        }

        private static string TestFlatNormals()
        {
            foreach (var m in AllBuilders())
            {
                float dev = MaxNormalDeviationDeg(m.make());
                if (dev > 0.5f)
                    return $"{m.name}: 정점 법선이 면 법선에서 최대 {dev:F3}° 벗어났다 — " +
                           "계단 셰이딩이 면이 아니라 그라디언트에 걸린다 (G-4 위반)";
            }
            return null;
        }

        private static string TestDeterminism()
        {
            foreach (var m in AllBuilders())
            {
                string err = CompareBitwise(m.make(), m.make(), m.name);
                if (err != null) return err;
            }
            return null;
        }

        // ══ 「릴로 안 보인다」의 형상 근거 ════════════════════════════════════

        private static string TestGridIsThreeByThree()
        {
            PortholeSpec s = PortholeMesh.DefaultSpec.Clamped();
            ProcMeshBuilder b = Panel();
            Vector3[] centers = CellCenters(s);

            // 개구부 벽(반지름 = OpeningRadius)에 놓인 정점을 셀별로 모은다.
            var count = new int[9];
            var sumX = new float[9];
            var sumY = new float[9];
            for (int i = 0; i < b.VertexCount; i++)
            {
                Vector3 p = b.Positions[i];
                int c = NearestCell(centers, p, out float d);
                if (Mathf.Abs(d - s.OpeningRadius) > 1e-3f) continue;
                count[c]++;
                sumX[c] += p.x;
                sumY[c] += p.y;
            }

            for (int i = 0; i < 9; i++)
            {
                if (count[i] < s.OpeningSides)
                    return $"셀 {i / 3}행 {i % 3}열: 개구부 반지름 위의 정점이 {count[i]} 개뿐이다 " +
                           $"(변 {s.OpeningSides} 개 이상이어야 한다) — 개구부가 만들어지지 않았다";
                float cx = sumX[i] / count[i], cy = sumY[i] / count[i];
                if (Mathf.Abs(cx - centers[i].x) > 1e-3f || Mathf.Abs(cy - centers[i].y) > 1e-3f)
                    return $"셀 {i / 3}행 {i % 3}열의 실측 중심 ({cx:F4}, {cy:F4}) 이 " +
                           $"선언 ({centers[i].x:F4}, {centers[i].y:F4}) 와 다르다";
            }

            // 피치가 인자와 일치하는가 — 가로·세로 모두.
            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 2; col++)
            {
                float dx = centers[row * 3 + col + 1].x - centers[row * 3 + col].x;
                if (Mathf.Abs(dx - s.CellPitch) > 1e-5f)
                    return $"열 간격 {dx:F5} m 가 인자 {s.CellPitch:F5} m 와 다르다";
            }
            for (int row = 0; row < 2; row++)
            {
                float dy = centers[row * 3].y - centers[(row + 1) * 3].y;
                if (Mathf.Abs(dy - s.CellPitch) > 1e-5f)
                    return $"행 간격 {dy:F5} m 가 인자 {s.CellPitch:F5} m 와 다르다";
            }
            return null;
        }

        private static string TestGridIsSquare()
        {
            // 정사각이 아니면 대각선 판독이 무너진다 (`DEVICE_DESIGN_SPEC.md` §3.1 —
            // 현재 씬의 0.50 × 0.40 은 대각선이 38.7° 로 기운다).
            PortholeSpec s = PortholeMesh.DefaultSpec.Clamped();
            Vector3 a = PortholeMesh.CellCenter(s, 0, 0);
            Vector3 b = PortholeMesh.CellCenter(s, 1, 1);
            float ang = Mathf.Atan2(a.y - b.y, b.x - a.x) * Mathf.Rad2Deg;
            if (Mathf.Abs(ang - 45f) > 0.01f)
                return $"대각선이 {ang:F3}° 다 (45° 여야 한다) — 격자가 정사각이 아니다";
            return null;
        }

        private static string TestHorizontalRibIsThicker()
        {
            PortholeSpec s = PortholeMesh.DefaultSpec.Clamped();
            ProcMeshBuilder b = Panel();

            if (!RibPlane(b, s, s.RibDepth, true, out float hWidth, out float hSpan, out int hClusters))
                return "가로 리브의 앞면 평면에서 정점을 찾지 못했다";
            if (!RibPlane(b, s, s.RibDepth * PortholeMesh.VerticalRibDepthFactor, false, out float vWidth, out float vSpan, out int vClusters))
                return "세로 리브의 앞면 평면에서 정점을 찾지 못했다";

            if (hWidth < vWidth - 1e-4f)
                return $"가로 리브 {hWidth * 1000f:F1} mm 가 세로 리브 {vWidth * 1000f:F1} mm 보다 얇다 — " +
                       "굵은 선이 세로가 되면 3×3 이 세로 릴로 읽힌다 (UP-VIS-08)";
            if (Mathf.Abs(hWidth - s.HorizontalRibWidth) > 1e-4f)
                return $"가로 리브 실측 {hWidth:F5} m 가 인자 {s.HorizontalRibWidth:F5} m 와 다르다";
            if (Mathf.Abs(vWidth - s.VerticalRibWidth) > 1e-4f)
                return $"세로 리브 실측 {vWidth:F5} m 가 인자 {s.VerticalRibWidth:F5} m 와 다르다";
            _ = hSpan; _ = vSpan; _ = hClusters; _ = vClusters;
            return null;
        }

        private static string TestVerticalRibsAreInterrupted()
        {
            PortholeSpec s = PortholeMesh.DefaultSpec.Clamped();
            ProcMeshBuilder b = Panel();
            Vector3 size = PortholeMesh.PanelSize(s);

            if (!RibPlane(b, s, s.RibDepth, true, out _, out float hSpan, out int hClusters))
                return "가로 리브의 앞면 평면에서 정점을 찾지 못했다";
            // 가로 리브는 **캐비닛 폭 전체**를 지난다. 끊기면 행 리듬이 약해진다.
            if (Mathf.Abs(hSpan - size.x) > 1e-3f)
                return $"가로 리브의 x 범위 {hSpan:F4} m 가 캐비닛 폭 {size.x:F4} m 에 못 미친다 — 이어져 있지 않다";
            if (hClusters != 2)
                return $"가로 리브가 {hClusters} 줄이다 (2 줄이어야 한다)";

            if (!RibPlane(b, s, s.RibDepth * PortholeMesh.VerticalRibDepthFactor, false, out _, out float vCoverage, out int vClusters))
                return "세로 리브의 앞면 평면에서 정점을 찾지 못했다";
            float gridH = 3f * s.CellPitch;
            // **끊겨 있어야 한다.** 한 열의 세로 리브 총 길이가 격자 높이에 이르면
            // 세 조각이 사실상 하나의 기둥이 된다.
            if (vCoverage >= gridH * 0.95f)
                return $"한 열의 세로 리브 총 길이 {vCoverage:F4} m 가 격자 높이 {gridH:F4} m 에 육박한다 — " +
                       "끊기지 않았다면 열이 하나의 기둥(릴)으로 읽힌다";
            if (vClusters != 3)
                return $"한 열의 세로 리브가 {vClusters} 조각이다 (행마다 하나씩 3 조각이어야 한다)";
            return null;
        }

        /// <summary>
        /// 리브 앞면 평면의 정점만 골라 굵기와 뻗은 범위를 잰다.
        ///
        /// 평면 z 로 거르는 이유: 가로 리브 앞면(z = T + RibDepth)과 세로 리브
        /// 앞면(z = T + VerticalRibDepthFactor·RibDepth)은 캐비닛 안에서 **그 둘만** 갖는 높이다.
        /// 리벳·볼트·해치·베젤은 전부 다른 z 에 있다. 굵기를 인자에서 읽지 않고
        /// 이렇게 형상에서 재야 「인자는 맞는데 형상이 다른」 회귀가 잡힌다.
        /// </summary>
        private static bool RibPlane(ProcMeshBuilder b, in PortholeSpec s, float depth,
                                     bool horizontal, out float width, out float extent, out int clusters)
        {
            width = 0f; extent = 0f; clusters = 0;
            float z = s.PlateThickness + depth;
            List<Vector3> pts = AtPlane(b, z, 1e-4f);
            if (pts.Count == 0) return false;

            if (horizontal)
            {
                // 막대 하나 = 서로 다른 y 값 두 개(위·아래 모서리). 굵기는 그 차이다.
                var ys = new List<float>(pts.Count);
                float xmin = float.MaxValue, xmax = float.MinValue;
                foreach (Vector3 p in pts)
                {
                    ys.Add(p.y);
                    xmin = Mathf.Min(xmin, p.x);
                    xmax = Mathf.Max(xmax, p.x);
                }
                List<float> v = DistinctValues(ys, 1e-3f);
                if (v.Count < 2 || v.Count % 2 != 0) return false;
                clusters = v.Count / 2;
                for (int i = 0; i < v.Count; i += 2) width = Mathf.Max(width, v[i + 1] - v[i]);
                extent = xmax - xmin;   // 폭 전체를 지나는가
                return true;
            }

            // 세로: 오른쪽 열(x > 0)만 본다. 굵기는 x 값 두 개의 차이,
            // 「끊겨 있는가」는 y 값 쌍들의 **길이 합**으로 본다.
            var xs = new List<float>();
            var ysR = new List<float>();
            foreach (Vector3 p in pts)
            {
                if (p.x <= 0f) continue;
                xs.Add(p.x);
                ysR.Add(p.y);
            }
            if (xs.Count == 0) return false;
            List<float> vx = DistinctValues(xs, 1e-3f);
            if (vx.Count != 2) return false;
            width = vx[1] - vx[0];

            List<float> vy = DistinctValues(ysR, 1e-3f);
            if (vy.Count < 2 || vy.Count % 2 != 0) return false;
            clusters = vy.Count / 2;
            for (int i = 0; i < vy.Count; i += 2) extent += vy[i + 1] - vy[i];
            return true;
        }

        private static string TestHatchBandExists()
        {
            PortholeSpec s = PortholeMesh.DefaultSpec.Clamped();
            if (s.HatchCount <= 0) return "기본 인자에 해치가 없다 — 하단 가로 띠가 사라진다";
            ProcMeshBuilder b = Panel();
            PortholeMesh.PanelVerticalExtent(s, out float bottom, out _);
            float hatchY = bottom + s.HatchBandHeight * 0.5f;

            // 해치 원판 앞면 평면에서 x 묶음이 HatchCount 개여야 한다.
            float z = s.PlateThickness + s.FrameDepth + 0.028f;   // HatchDiscDepth
            var xs = new List<float>();
            for (int i = 0; i < b.VertexCount; i++)
            {
                Vector3 p = b.Positions[i];
                if (Mathf.Abs(p.z - z) > 1e-4f) continue;
                if (Mathf.Abs(p.y - hatchY) > s.HatchRadius * 1.2f) continue;
                xs.Add(p.x);
            }
            if (xs.Count == 0) return "해치 원판의 앞면에서 정점을 찾지 못했다";
            // 8각 원판 안쪽 정점 간격(≈0.54 R)보다 크고 해치 사이 간격보다 작은 임계값.
            var groups = Cluster(xs, s.HatchRadius);
            if (groups.Count != s.HatchCount)
                return $"해치가 {groups.Count} 개다 (인자 {s.HatchCount} 개)";
            foreach (var g in groups)
            {
                float d = g.max - g.min;
                if (d < s.HatchRadius * 1.4f)
                    return $"해치 지름이 {d:F4} m 로 반지름 인자 {s.HatchRadius:F4} m 에 비해 너무 작다";
            }
            return null;
        }

        // ══ 판독성 ═══════════════════════════════════════════════════════════

        private static string TestBezelWithinReadabilityCap()
        {
            // 기본값이 상한 안에 있는가.
            PortholeSpec s = PortholeMesh.DefaultSpec.Clamped();
            if (s.BezelToOpeningDiameter > PortholeMesh.MaxBezelToOpeningDiameter + 1e-5f)
                return $"기본 베젤/지름 {s.BezelToOpeningDiameter:F4} 가 상한 " +
                       $"{PortholeMesh.MaxBezelToOpeningDiameter:F2} 를 넘었다";

            // 상한을 넘겨 달라고 해도 넘지 않는가.
            PortholeSpec greedy = PortholeMesh.DefaultSpec;
            greedy.BezelWidth = 0.40f;
            PortholeSpec g = greedy.Clamped();
            if (g.BezelToOpeningDiameter > PortholeMesh.MaxBezelToOpeningDiameter + 1e-5f)
                return $"베젤 0.40 m 를 요구했더니 {g.BezelToOpeningDiameter:F4} 로 남았다 — 상한이 안 걸린다";
            if (g.BezelOuterRadius >= g.CellPitch * 0.5f)
                return $"베젤 바깥 반지름 {g.BezelOuterRadius:F4} 가 셀 반폭 {g.CellPitch * 0.5f:F4} 를 넘었다";
            return null;
        }

        private static string TestOpeningsAreEmpty()
        {
            PortholeSpec s = PortholeMesh.DefaultSpec.Clamped();
            ProcMeshBuilder b = Panel();
            Vector3[] centers = CellCenters(s);

            float worst = float.MaxValue;
            int worstCell = -1;
            for (int i = 0; i < b.VertexCount; i++)
            {
                NearestCell(centers, b.Positions[i], out float d);
                if (d < worst) { worst = d; worstCell = NearestCell(centers, b.Positions[i], out _); }
            }
            if (worst < s.OpeningRadius - 1e-3f)
                return $"셀 {worstCell / 3}행 {worstCell % 3}열의 개구부 안 {worst:F4} m 지점에 판 정점이 있다 " +
                       $"(개구부 반지름 {s.OpeningRadius:F4} m) — 베젤·리브·볼트가 창을 잡아먹었다";
            return null;
        }

        // ══ 우물·유리 ════════════════════════════════════════════════════════

        private static string TestWellNormalsPointInward()
        {
            PortholeSpec s = PortholeMesh.DefaultSpec.Clamped();
            ProcMeshBuilder b = WellCluster();
            Vector3[] centers = CellCenters(s);

            float ri = s.OpeningRadius - PortholeMesh.WellBoreClearance - s.WellWallThickness;
            int inward = 0;
            for (int i = 0; i < b.VertexCount; i++)
            {
                Vector3 p = b.Positions[i];
                int c = NearestCell(centers, p, out float d);
                if (Mathf.Abs(d - ri) > 1e-3f) continue;
                Vector3 radial = new Vector3(p.x - centers[c].x, p.y - centers[c].y, 0f);
                if (radial.sqrMagnitude < 1e-8f) continue;
                radial = radial.normalized;
                float dot = Vector3.Dot(b.Normals[i], radial);
                if (dot > 0.5f)
                    return $"우물 안쪽 반지름의 정점 {i} 법선이 바깥을 본다 (radial·n = {dot:F3}) — " +
                           "안쪽 벽이 뒤집혀 컬링으로 사라진다";
                if (dot < -0.9f) inward++;
            }
            if (inward < s.OpeningSides * 9)
                return $"안쪽을 보는 벽 정점이 {inward} 개뿐이다 (셀 9 × 변 {s.OpeningSides} 이상이어야 한다)";
            return null;
        }

        private static string TestWellFitsInsideBore()
        {
            PortholeSpec s = PortholeMesh.DefaultSpec.Clamped();
            ProcMeshBuilder b = WellCluster();
            Vector3[] centers = CellCenters(s);

            float maxR = s.OpeningRadius - PortholeMesh.WellBoreClearance;
            for (int i = 0; i < b.VertexCount; i++)
            {
                NearestCell(centers, b.Positions[i], out float d);
                if (d > maxR + 1e-4f)
                    return $"우물 정점 {i} 이 반지름 {d:F4} m 로 보어 {maxR:F4} m 를 넘었다 — 판에 파고든다";
            }
            Bounds(b, out Vector3 min, out Vector3 max);
            float mouthZ = s.PlateThickness + s.BezelProtrusion;
            if (min.z < -1e-4f)
                return $"우물 뒷면 z = {min.z:F4} — 판 뒤로 뚫고 나갔다 (벽 안쪽이 비쳐 보인다)";
            if (max.z > mouthZ + 1e-4f)
                return $"우물 입 z = {max.z:F4} 가 베젤 립 {mouthZ:F4} 보다 앞이다";
            float floorZ = PortholeMesh.WellFloorZ(s);
            if (floorZ <= min.z || floorZ >= max.z)
                return $"우물 바닥 z = {floorZ:F4} 가 우물 범위 [{min.z:F4}, {max.z:F4}] 밖이다";
            return null;
        }

        private static string TestGlassIsConvexAndInside()
        {
            PortholeSpec s = PortholeMesh.DefaultSpec.Clamped();
            ProcMeshBuilder b = GlassCluster();
            Vector3[] centers = CellCenters(s);

            float maxR = s.OpeningRadius - PortholeMesh.GlassBoreClearance;
            float mouthZ = s.PlateThickness + s.BezelProtrusion;
            float backR = 0f, frontR = 0f;
            for (int i = 0; i < b.VertexCount; i++)
            {
                Vector3 p = b.Positions[i];
                NearestCell(centers, p, out float d);
                if (d > maxR + 1e-4f)
                    return $"유리 정점 {i} 이 반지름 {d:F4} m 로 보어 {maxR:F4} m 를 넘었다";
                if (p.z < mouthZ - s.GlassThickness - 1e-4f || p.z > mouthZ + 1e-4f)
                    return $"유리 정점 {i} 의 z = {p.z:F4} 가 창 안 " +
                           $"[{mouthZ - s.GlassThickness:F4}, {mouthZ:F4}] 밖이다";
                if (Mathf.Abs(p.z - (mouthZ - s.GlassThickness)) < 1e-4f) backR = Mathf.Max(backR, d);
                if (Mathf.Abs(p.z - mouthZ) < 1e-4f) frontR = Mathf.Max(frontR, d);
            }
            if (backR <= 0f || frontR <= 0f)
                return $"유리 앞·뒤면을 찾지 못했다 (뒤 {backR:F4}, 앞 {frontR:F4})";
            // 앞면이 뒷면보다 좁아야 **볼록**이다. 같으면 원판, 넓으면 오목이다.
            if (frontR >= backR - 1e-3f)
                return $"유리 앞면 반지름 {frontR:F4} 가 뒷면 {backR:F4} 보다 작지 않다 — 볼록하지 않다";
            return null;
        }

        private static string TestSymbolFits()
        {
            PortholeSpec s = PortholeMesh.DefaultSpec.Clamped();
            Vector2 fit = PortholeMesh.SymbolFitSize(s);

            // 「담을 수 있다」가 0 이면 우물은 장식이고 발광체는 유리 밖으로 나간다.
            if (fit.x <= 0.02f || fit.y <= 0.02f)
                return $"우물에 들어가는 크기가 {fit.x:F4} × {fit.y:F4} m 뿐이다 — 발광체를 담을 수 없다";
            // 창이 오목해 보이려면 깊이가 지름의 최소 1/4 은 되어야 한다.
            if (fit.y < fit.x * 0.25f)
                return $"깊이 {fit.y:F4} m 가 지름 {fit.x:F4} m 의 1/4 에 못 미친다 — " +
                       "창이 오목한 「관」이 아니라 납작한 「판」으로 읽힌다";

            // 그 크기의 구를 바닥에 앉혔을 때 유리·보어·바닥과 겹치지 않는가.
            float d = Mathf.Min(fit.x, fit.y);
            float glassBack = s.PlateThickness + s.BezelProtrusion - s.GlassThickness;
            float wellR = s.OpeningRadius - PortholeMesh.WellBoreClearance - s.WellWallThickness;
            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
            {
                Vector3 seat = PortholeMesh.SymbolSeat(s, col, row, d);
                Vector3 cell = PortholeMesh.CellCenter(s, col, row);
                if (Mathf.Abs(seat.x - cell.x) > 1e-5f || Mathf.Abs(seat.y - cell.y) > 1e-5f)
                    return $"셀 {row}행 {col}열: 심볼 자리가 셀 중심에서 벗어났다";
                if (seat.z - d * 0.5f < PortholeMesh.WellFloorZ(s) - 1e-4f)
                    return $"셀 {row}행 {col}열: 심볼이 우물 바닥({PortholeMesh.WellFloorZ(s):F4}) 아래로 내려갔다";
                if (seat.z + d * 0.5f > glassBack + 1e-4f)
                    return $"셀 {row}행 {col}열: 심볼 앞면 {seat.z + d * 0.5f:F4} 가 유리 뒷면 {glassBack:F4} 를 뚫는다";
                if (d * 0.5f > wellR)
                    return $"심볼 반지름 {d * 0.5f:F4} 가 우물 안지름 {wellR:F4} 를 넘는다";
            }
            return null;
        }

        // ══ 크기·피벗 ════════════════════════════════════════════════════════

        private static string TestPanelBoundsMatchSpec()
        {
            PortholeSpec s = PortholeMesh.DefaultSpec;
            Vector3 declared = PortholeMesh.PanelSize(s);
            ProcMeshBuilder b = Panel();
            Bounds(b, out Vector3 min, out Vector3 max);
            Vector3 actual = max - min;

            if (Mathf.Abs(actual.x - declared.x) > 1e-3f)
                return $"가로 실측 {actual.x:F4} m 가 선언 {declared.x:F4} m 와 다르다";
            if (Mathf.Abs(actual.y - declared.y) > 1e-3f)
                return $"세로 실측 {actual.y:F4} m 가 선언 {declared.y:F4} m 와 다르다";
            if (Mathf.Abs(min.z) > 1e-4f)
                return $"벽 접촉면 z = {min.z:F5} (0 이어야 한다)";
            if (Mathf.Abs(max.z - declared.z) > 1e-3f)
                return $"돌출 실측 {max.z:F4} m 가 선언 {declared.z:F4} m 와 다르다";

            // 격자 중심이 원점인가 — 씬 소유자가 눈높이를 여기에 맞춘다.
            PortholeSpec c = s.Clamped();
            PortholeMesh.PanelVerticalExtent(c, out float bottom, out float top);
            if (Mathf.Abs(max.y - top) > 1e-4f || Mathf.Abs(min.y - bottom) > 1e-4f)
                return $"세로 범위 [{min.y:F4}, {max.y:F4}] 가 선언 [{bottom:F4}, {top:F4}] 와 다르다";
            if (top >= -bottom)
                return "캐비닛이 격자 중심 기준으로 위아래 대칭이다 — 하단 해치 띠가 없다는 뜻이다";
            return null;
        }

        private static string TestConsolePropPivots()
        {
            foreach (var p in ConsoleProps)
            {
                ProcMeshBuilder b = p.make();
                Bounds(b, out Vector3 min, out Vector3 max);
                if (p.wallContact)
                {
                    if (Mathf.Abs(min.z) > 1e-4f)
                        return $"{p.name}: 벽 접촉면 z = {min.z:F5} (0 이어야 한다)";
                }
                else
                {
                    if (Mathf.Abs(min.z - ConsolePropMesh.HazardPlateFrontZ) > 1e-3f)
                        return $"{p.name}: 뒷면 z = {min.z:F5} 가 바탕판 앞면 " +
                               $"{ConsolePropMesh.HazardPlateFrontZ:F3} 와 맞물리지 않는다";
                }
                if (max.z <= 0f) return $"{p.name}: 밖으로(+Z) 나오지 않았다";
                // X·Y 중심이 원점인가.
                Vector3 c = (min + max) * 0.5f;
                if (Mathf.Abs(c.x) > 1e-3f || Mathf.Abs(c.y) > 1e-3f)
                    return $"{p.name}: X·Y 중심이 ({c.x:F4}, {c.y:F4}) 다 (원점이어야 한다)";
            }
            return null;
        }

        private static string TestHazardStripesStayInside()
        {
            const float w = 0.44f, h = 0.14f;
            var baseB = new ProcMeshBuilder(256);
            ConsolePropMesh.HazardPlateBase(baseB, w, h);
            var stripeB = new ProcMeshBuilder(256);
            ConsolePropMesh.HazardPlateStripes(stripeB, w, h);

            if (stripeB.TriangleCount == 0) return "줄무늬가 하나도 생기지 않았다";
            Bounds(baseB, out Vector3 bmin, out Vector3 bmax);
            Bounds(stripeB, out Vector3 smin, out Vector3 smax);
            if (smin.x < bmin.x - 1e-4f || smax.x > bmax.x + 1e-4f ||
                smin.y < bmin.y - 1e-4f || smax.y > bmax.y + 1e-4f)
                return $"줄무늬 범위 x[{smin.x:F4}, {smax.x:F4}] y[{smin.y:F4}, {smax.y:F4}] 가 " +
                       $"판 x[{bmin.x:F4}, {bmax.x:F4}] y[{bmin.y:F4}, {bmax.y:F4}] 밖으로 나갔다";
            return null;
        }

        // ══ 예산 ═════════════════════════════════════════════════════════════

        private static string TestDeviceBudgets()
        {
            int panel = Panel().TriangleCount;
            if (panel > PortholeMesh.PanelTriangleBudget)
                return $"PortholePanel {panel} 삼각형 (예산 {PortholeMesh.PanelTriangleBudget})";
            int well = WellCluster().TriangleCount;
            if (well > PortholeMesh.WellClusterTriangleBudget)
                return $"PortholeWell x9 {well} 삼각형 (예산 {PortholeMesh.WellClusterTriangleBudget})";
            int glass = GlassCluster().TriangleCount;
            if (glass > PortholeMesh.GlassClusterTriangleBudget)
                return $"PortholeGlass x9 {glass} 삼각형 (예산 {PortholeMesh.GlassClusterTriangleBudget})";
            return null;
        }

        private static string TestConsolePropBudgets()
        {
            foreach (var p in ConsoleProps)
            {
                int tris = p.make().TriangleCount;
                if (tris > ConsolePropMesh.PerPropTriangleLimit)
                    return $"{p.name}: {tris} 삼각형 (상한 {ConsolePropMesh.PerPropTriangleLimit})";
                if (tris < 12)
                    return $"{p.name}: {tris} 삼각형 — 사실상 형상이 없다";
            }
            return null;
        }

        private static string TestBudgetKnobsWork()
        {
            // 「줄일 수 있게 하라」는 요구를 실제로 검사한다. 인자가 있는데 안 듣는 것이
            // 가장 흔한 종류의 거짓말이다.
            int full = Panel().TriangleCount;

            PortholeSpec lean = PortholeMesh.DefaultSpec;
            lean.BoltsPerBezel = 0;
            lean.RivetsPerBand = 0;
            lean.FrameBevel = 0f;
            lean.HatchCount = 0;
            int leanTris = Panel(lean).TriangleCount;
            if (leanTris >= full)
                return $"볼트·리벳·모따기·해치를 껐는데 {leanTris} → {full} 에서 줄지 않았다";

            PortholeSpec fine = PortholeMesh.DefaultSpec;
            fine.OpeningSides = 16;
            int fineTris = Panel(fine).TriangleCount;
            if (fineTris <= full)
                return $"변 수를 8 → 16 으로 올렸는데 {fineTris} 로 늘지 않았다 (인자가 안 듣는다)";

            int glass1 = GlassCluster().TriangleCount;
            PortholeSpec twoTier = PortholeMesh.DefaultSpec;
            twoTier.GlassTiers = 2;
            if (GlassCluster(twoTier).TriangleCount <= glass1)
                return "유리 단 수를 올렸는데 삼각형이 늘지 않았다";
            return null;
        }

        // ══ 인자 방어 ════════════════════════════════════════════════════════

        private static string TestClampEnforcesRules()
        {
            // ① 가로 리브를 세로보다 얇게 만들려는 시도.
            PortholeSpec reel = PortholeMesh.DefaultSpec;
            reel.HorizontalRibBoost = 0.4f;
            PortholeSpec r = reel.Clamped();
            if (r.HorizontalRibWidth < r.VerticalRibWidth - 1e-6f)
                return $"가로 배율 0.4 를 요구했더니 가로 {r.HorizontalRibWidth:F4} < 세로 " +
                       $"{r.VerticalRibWidth:F4} 로 남았다 — 릴 회피 규칙이 안 걸린다";

            // ② 세로 리브를 한 행 전체로 늘려 기둥을 만들려는 시도.
            PortholeSpec column = PortholeMesh.DefaultSpec;
            column.VerticalRibSpan = 3f;
            if (column.Clamped().VerticalRibSpan >= 1f)
                return "세로 리브 비율 3.0 을 요구했더니 1 이상으로 남았다 — 세 조각이 이어져 기둥이 된다";

            // ③ 개구부를 셀보다 크게 만들려는 시도.
            PortholeSpec huge = PortholeMesh.DefaultSpec;
            huge.OpeningRadius = 5f;
            PortholeSpec hc = huge.Clamped();
            if (hc.BezelOuterRadius >= hc.CellPitch * 0.5f)
                return $"개구부 5 m 를 요구했더니 베젤 바깥 {hc.BezelOuterRadius:F4} 가 셀 반폭을 넘었다";

            // ④ 변 수가 4의 배수가 아니면 셀 모서리가 정점에 안 걸려 껍질이 열린다.
            for (int sides = 6; sides <= 20; sides++)
            {
                PortholeSpec sp = PortholeMesh.DefaultSpec;
                sp.OpeningSides = sides;
                int got = sp.Clamped().OpeningSides;
                if (got % 4 != 0 || got < 8 || got > 16)
                    return $"변 수 {sides} → {got} (8~16 의 4 배수여야 한다)";
            }

            // ⑤ 우물이 판 뒤로 나가려는 시도.
            PortholeSpec deep = PortholeMesh.DefaultSpec;
            deep.WellDepth = 10f;
            PortholeSpec dc = deep.Clamped();
            if (dc.WellDepth > dc.PlateThickness + dc.BezelProtrusion)
                return $"우물 깊이 10 m 를 요구했더니 {dc.WellDepth:F4} 로 남아 판을 뚫는다";

            // ⑥ 극단 인자를 넣어도 형상이 여전히 닫혀 있는가 — clamp 가 형태를 지키는가.
            var extreme = new PortholeSpec
            {
                CellPitch = 0.001f, OpeningRadius = 99f, OpeningSides = 3,
                BezelWidth = 9f, BezelProtrusion = 9f, BoltsPerBezel = 99,
                PlateThickness = 0f, FrameMargin = 0f, FrameDepth = 0f, FrameBevel = 9f,
                RivetsPerBand = 99, RibWidth = 9f, HorizontalRibBoost = -3f, RibDepth = 9f,
                VerticalRibSpan = -1f, HatchCount = 99, HatchRadius = 9f, HatchBandHeight = 9f,
                WellDepth = -1f, WellWallThickness = 9f, WellFloorThickness = 9f,
                GlassThickness = 9f, GlassSides = 0, GlassTiers = 99,
            };
            string err = EdgeBalance(Panel(extreme), "극단 인자 Panel");
            if (err != null) return err;
            err = EdgeBalance(WellCluster(extreme), "극단 인자 Well");
            if (err != null) return err;
            err = EdgeBalance(GlassCluster(extreme), "극단 인자 Glass");
            if (err != null) return err;
            if (SignedVolume(Panel(extreme)) <= 0f) return "극단 인자에서 판의 부피가 0 이하다";
            return null;
        }

        // ══ UP-FIX-03 ════════════════════════════════════════════════════════

        private static string TestLeverSilhouettesDiffer()
        {
            var slot = new ProcMeshBuilder(384);
            ConsolePropMesh.LeverSlotHousing(slot);
            Bounds(slot, out Vector3 smin, out Vector3 smax);
            Vector3 s = smax - smin;

            var lever = new ProcMeshBuilder(256);
            ProcMesh.Lever(lever);
            Bounds(lever, out Vector3 lmin, out Vector3 lmax);
            Vector3 l = lmax - lmin;

            // 과수확 슬롯은 **세로**가 지배축이다.
            if (s.y < s.z * 3f)
                return $"슬롯 하우징의 세로 {s.y:F3} m 가 깊이 {s.z:F3} m 의 3배에 못 미친다 — " +
                       "「위아래로 미끄러지는 것」으로 읽히지 않는다";
            if (s.y < s.x * 2f)
                return $"슬롯 하우징의 세로 {s.y:F3} m 가 가로 {s.x:F3} m 의 2배에 못 미친다";

            // 실행 레버는 **깊이**가 지배축이다. 둘이 같은 방향이면 형상으로 구분되지 않는다.
            if (l.z < l.y)
                return $"실행 레버의 깊이 {l.z:F3} m 가 세로 {l.y:F3} m 보다 작다 — " +
                       "두 레버의 지배축이 같아졌다 (UP-FIX-03 회귀)";
            return null;
        }
    }
}
