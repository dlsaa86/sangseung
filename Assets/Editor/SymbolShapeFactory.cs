using System.Collections.Generic;
using System.IO;
using Ascend.Prototype.Art;
using UnityEditor;
using UnityEngine;

namespace Ascend.Prototype.EditorTools
{
    /// <summary>
    /// 결과판 심볼 3종의 **유일한 정의.** (`UP-FIX-82`)
    ///
    /// ## 왜 이 파일이 생겼나
    ///
    /// 직전까지 심볼은 두 파일에서 각각 이렇게 만들어졌다 —
    ///
    ///     Sym_NormalSoul    Sphere   0.17
    ///     Sym_Absorber      Cube     0.16
    ///     Sym_Proliferator  Capsule  0.15
    ///
    /// 즉 **색 외 차이가 「프리미티브 종류」 하나뿐**이고 크기 차이 12% 는 화면에서
    /// 안 보인다. `VISUAL_SPEC` §4 는 색 외 **최소 세 항목**을 요구한다.
    /// 독립 평가가 「회색조로 바꾸면 구분이 완전히 사라진다」로 판정했다.
    ///
    /// ## 더 깊은 원인 — 심볼은 애초에 화면에 없었다
    ///
    /// 실측(2026-08-04): 관찰창 모듈은 항상 켜진 장식 `SoulObject`(월드 폭 **0.177**)를
    /// z=0.090 에 들고 있고, 결과판 칸은 그보다 **30mm 뒤**(z=0.120)에서 폭 0.145 의
    /// 프리미티브를 켠다. 앞의 것이 더 크고 더 가깝다 ⇒ **정면에서 심볼은 100% 가려진다.**
    /// 평가자가 본 「회색 원반 위 붉은 각진 덩어리 하나」가 바로 그 장식이고,
    /// 아홉 칸이 같아 보인 것은 **아홉 칸이 실제로 같은 물체**였기 때문이다.
    ///
    /// 그래서 이 개정은 값이 아니라 **구조**를 바꾼다 — 심볼이 영혼의 자리를 차지한다.
    /// `SoulObject` 렌더러는 꺼지고(오브젝트는 남는다), 칸이 챔버의 내용물이 된다.
    ///
    /// ## 색 외 차이 네 축
    ///
    /// | 심볼 | 형태 | 조각 수 | 조각 지름 | 표면 |
    /// |---|---|---|---|---|
    /// | 정상 영혼 | 매끈한 구 | **1** | **0.168** | 12각 매끈 |
    /// | 흡수체 | 뾰족한 다면체 | **1** | **0.124** | 8개 각뿔 스파이크 |
    /// | 증식체 | 뭉친 덩어리 | **3** | **0.058** | 울퉁불퉁 |
    ///
    /// 축이 넷이다 — **형태 종류 · 전체 크기 · 조각 개수 · 표면 각짐.**
    /// 「개수」가 요점이다. 실루엣이 뭉개지는 축소 화면에서도 살아남는 유일한 축이고,
    /// 회색조에서도 사라지지 않는다.
    ///
    /// ## 지켜야 하는 계약
    ///
    /// - 최상위 자식 이름은 `Sym_NormalSoul`/`Sym_Absorber`/`Sym_Proliferator` 여야 한다.
    ///   <see cref="View.SpinBoardView.KindOf"/> 가 이름으로 켠다. 증식체는 그 이름의
    ///   **부모 아래에** 조각을 넣으므로 `SetCell` 의 최상위 순회는 그대로 작동한다.
    /// - **런타임에 프리미티브를 만들지 않는다.** 첫 스핀에서 끊기고 결정론 캡처가 깨진다.
    /// - **멱등이다.** 「없으면 만든다」가 아니라 「항상 이 상태로 만든다」 —
    ///   메시·재질·좌표·스케일·활성 상태를 매번 절대값으로 다시 쓴다.
    /// </summary>
    public static class SymbolShapeFactory
    {
        public const string NormalSoulName = "Sym_NormalSoul";
        public const string AbsorberName = "Sym_Absorber";
        public const string ProliferatorName = "Sym_Proliferator";
        public const string CoreName = "Core";

        private const string MeshDir = "Assets/Prototype_Elevator/Art/Meshes/Symbol";
        private const string MaterialDir = "Assets/Prototype_Elevator/Art/Materials/Room";

        // ── 치수(m). 칸 스케일 1 기준이므로 **월드 크기 그대로다.** ─────────────
        //
        // 상한은 관찰창 유리 지름 0.29 다. 가장 큰 심볼도 0.168 이라 둘레에
        // 0.061 의 어둠이 남고, 그래야 「챔버 안의 물체」로 읽힌다.

        /// <summary>정상 영혼 — 가장 크다. 온전한 것이므로 기준점이다.</summary>
        public const float NormalSoulSpan = 0.168f;

        /// <summary>
        /// 흡수체 — 스파이크 끝에서 끝. 정상 영혼의 **76%.**
        ///
        /// ⚠ 이 값은 「가장 긴 스파이크」에 정규화된다(<see cref="BuildSpike"/>).
        /// 처음엔 정규화 없이 상수만 적었는데, 길이 흔들림 최대 +14% 와 기울임이
        /// 겹쳐 실측 AABB 가 0.154 로 나왔다 — 구(0.168)의 **92%** 라 크기 축이
        /// 사실상 사라졌다. 상수와 화면이 다르면 그 상수는 문서가 아니라 거짓말이다.
        /// </summary>
        public const float AbsorberSpan = 0.128f;

        /// <summary>증식체 조각 하나의 지름. **셋 중 가장 작다.**</summary>
        public const float ProliferatorBlobSpan = 0.058f;

        public const int ProliferatorCount = 3;

        /// <summary>
        /// 조각 중심이 놓이는 원의 반지름.
        ///
        /// ⚠ **이 값이 「개수」 축의 전부다.** 이웃 조각 중심 거리는
        /// 2·R·sin60° = 0.0831 이고 조각 지름이 0.058 이므로 틈이 **0.025m** 다.
        /// 2m 거리 · 1920px · 수직 60° 에서 약 21px — 축소되어도 셋으로 남는다.
        /// 조각을 키우거나 R 을 줄이면 셋이 한 덩어리로 붙어 이 축이 사라진다.
        /// </summary>
        public const float ProliferatorClusterRadius = 0.048f;

        /// <summary>
        /// z 방향 납작 비율. 정면 실루엣은 그대로 두고 깊이만 줄인다.
        ///
        /// 이유는 챔버가 얇기 때문이다 — 심볼 중심은 도어 면에서 0.090 이고
        /// 유리 뒷면이 −0.006, 챔버 후면이 +0.198 이다. 구 반지름 0.084 를 그대로
        /// 쓰면 앞뒤 여유가 각각 0.012 / 0.024 로 붙는다. 0.80 을 곱하면
        /// 0.023 / 0.041 이 되어 어느 쪽도 뚫지 않는다.
        /// </summary>
        public const float DepthFlatten = 0.80f;

        /// <summary>핵의 껍질 대비 배율. 장식 영혼(`AscendReferenceRoom`)과 같은 값이다.</summary>
        public const float CoreScale = 0.52f;

        /// <summary>가장 큰 심볼의 z 반폭. 챔버 여유 검사에 쓴다.</summary>
        public static float MaxHalfDepth => NormalSoulSpan * 0.5f * DepthFlatten;

        /// <summary>증식체 뭉치의 전체 폭. 정상 영혼보다 작아야 크기 서열이 유지된다.</summary>
        public static float ProliferatorSpan
            => ProliferatorClusterRadius * 2f + ProliferatorBlobSpan;

        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 칸 하나에 심볼 3종을 **항상 이 상태로** 세운다. 전부 꺼진 채로 끝난다 —
        /// <see cref="View.SpinBoardView"/> 가 하나만 켠다.
        /// </summary>
        public static void Build(Transform cell)
        {
            if (cell == null) return;
            EnsureMeshes(out Mesh sphere, out Mesh spike, out Mesh blob);
            Material shell = ShellMaterial();
            Material core = CoreMaterial();

            // ── 정상 영혼 — 매끈한 구 하나 ──────────────────────────────────
            Transform soul = EnsureChild(cell, NormalSoulName);
            SetPiece(soul, sphere, shell, Vector3.zero, Quaternion.identity);
            EnsureCore(soul, sphere, core);
            Prune(soul, CoreName);

            // ── 흡수체 — 뾰족한 다면체 하나 ────────────────────────────────
            Transform abs = EnsureChild(cell, AbsorberName);
            // 정면에서 스파이크가 축에 딱 맞으면 「별 아이콘」이 된다. 살짝 돌려
            // 비대칭으로 보이게 한다 — 결정론적 상수다.
            SetPiece(abs, spike, shell, Vector3.zero, Quaternion.Euler(11f, 0f, 13f));
            EnsureCore(abs, spike, core);
            Prune(abs, CoreName);

            // ── 증식체 — 작은 덩어리 셋 ────────────────────────────────────
            Transform pro = EnsureChild(cell, ProliferatorName);
            StripRenderer(pro);                      // 부모는 그리지 않는다
            pro.localPosition = Vector3.zero;
            pro.localRotation = Quaternion.identity;
            pro.localScale = Vector3.one;
            var keep = new List<string>(ProliferatorCount);
            for (int i = 0; i < ProliferatorCount; i++)
            {
                string name = $"Blob_{i}";
                keep.Add(name);
                Transform piece = EnsureChild(pro, name);
                // 정삼각형 배치. 90° 에서 시작해 하나가 위로 오게 한다 —
                // 위 하나 · 아래 둘이면 「뭉친 것」이 가장 빨리 읽힌다.
                float a = (90f + 120f * i) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a) * ProliferatorClusterRadius,
                                    Mathf.Sin(a) * ProliferatorClusterRadius,
                                    // 앞뒤로 조금씩 어긋나야 뭉치가 평면 아이콘이 아니다.
                                    (i - 1) * 0.009f);
                var r = Quaternion.Euler(ProcMeshBuilder.HashSigned(i * 61 + 5) * 40f,
                                         ProcMeshBuilder.HashSigned(i * 97 + 11) * 40f,
                                         ProcMeshBuilder.Hash01(i * 131 + 3) * 360f);
                SetPiece(piece, blob, shell, p, r);
                EnsureCore(piece, blob, core);
                Prune(piece, CoreName);
            }
            Prune(pro, keep.ToArray());

            // 칸에 남아 있는 옛 심볼 잔재(이름이 셋 중 하나가 아닌 자식)는 그대로 둔다 —
            // 이 저장소는 씬 오브젝트를 지우지 않는다. 다만 심볼로 오인되지 않도록
            // `KindOf` 가 Empty 로 읽고 `SetCell` 이 항상 끈다.
        }

        /// <summary>세 심볼을 전부 끈다. 조립 직후의 정본 상태다.</summary>
        public static void ClearCell(Transform cell)
        {
            if (cell == null) return;
            foreach (Transform child in cell)
                if (View.SpinBoardView.KindOf(child.name) != Spin.SymbolKind.Empty)
                    child.gameObject.SetActive(false);
        }

        /// <summary>이름으로 한 종류만 켠다. `SetCell` 과 같은 규약이다.</summary>
        public static void ShowKind(Transform cell, Spin.SymbolKind kind)
        {
            if (cell == null) return;
            foreach (Transform child in cell)
            {
                Spin.SymbolKind k = View.SpinBoardView.KindOf(child.name);
                if (k == Spin.SymbolKind.Empty) continue;
                child.gameObject.SetActive(k == kind);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  오브젝트
        // ══════════════════════════════════════════════════════════════════════

        private static Transform EnsureChild(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t != null) return t;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "create symbol piece");
            return go.transform;
        }

        /// <summary>
        /// 조각 하나를 **절대값으로** 세운다. 메시·재질·좌표·회전·스케일·그림자를
        /// 매번 전부 다시 쓴다 — 「생성할 때만 설정」하면 다시 고칠 기회가 없다.
        /// </summary>
        private static void SetPiece(Transform t, Mesh mesh, Material mat, Vector3 pos, Quaternion rot)
        {
            var go = t.gameObject;
            // ⚠ **컴포넌트를 다 붙인 뒤에 잡는다.** `Undo.AddComponent` 는 대상
            // 게임오브젝트를 다시 직렬화하므로 **직전에 얻어 둔 컴포넌트 참조를
            // 무효화한다.** 붙이면서 잡으면 두 번째 `AddComponent` 가 첫 참조를
            // 죽이고, 그 다음 줄이 「MeshFilter 가 없다」로 터진다 — 실제로 터졌다.
            if (go.GetComponent<MeshFilter>() == null) Undo.AddComponent<MeshFilter>(go);
            if (go.GetComponent<MeshRenderer>() == null) Undo.AddComponent<MeshRenderer>(go);
            var mf = go.GetComponent<MeshFilter>();
            var mr = go.GetComponent<MeshRenderer>();
            mf.sharedMesh = mesh;
            mr.sharedMaterial = mat;
            mr.enabled = true;
            // 챔버 안이라 그림자를 만들 이유가 없다 — 보이지 않는데 비용만 든다.
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

            // 심볼은 조준 대상이 아니다. 콜라이더를 두면 뒤의 창·벽 조준을 방해한다.
            Collider col = go.GetComponent<Collider>();
            if (col != null) Undo.DestroyObjectImmediate(col);

            t.localPosition = pos;
            t.localRotation = rot;
            t.localScale = Vector3.one;
            EditorUtility.SetDirty(go);
        }

        /// <summary>껍질 안의 뜨거운 핵. 한 겹이면 균일 밝기 + 둥근 실루엣 = UI 아이콘이 된다.</summary>
        private static void EnsureCore(Transform shellPiece, Mesh mesh, Material core)
        {
            Transform t = EnsureChild(shellPiece, CoreName);
            SetPiece(t, mesh, core, Vector3.zero, Quaternion.identity);
            t.localScale = Vector3.one * CoreScale;
            EditorUtility.SetDirty(t.gameObject);
        }

        /// <summary>부모가 스스로 그리지 않게 한다. 옛 프리미티브의 캡슐 메시가 여기 남아 있었다.</summary>
        private static void StripRenderer(Transform t)
        {
            var mr = t.GetComponent<MeshRenderer>();
            if (mr != null) Undo.DestroyObjectImmediate(mr);
            var mf = t.GetComponent<MeshFilter>();
            if (mf != null) Undo.DestroyObjectImmediate(mf);
            Collider col = t.GetComponent<Collider>();
            if (col != null) Undo.DestroyObjectImmediate(col);
        }

        /// <summary>허용 목록에 없는 자식을 **끈다.** 지우지 않는다(이 저장소의 규율).</summary>
        private static void Prune(Transform parent, params string[] allowed)
        {
            foreach (Transform child in parent)
            {
                bool ok = false;
                for (int i = 0; i < allowed.Length; i++)
                    if (child.name == allowed[i]) { ok = true; break; }
                if (ok) continue;
                if (child.gameObject.activeSelf) child.gameObject.SetActive(false);
                var mr = child.GetComponent<MeshRenderer>();
                if (mr != null && mr.enabled) { mr.enabled = false; EditorUtility.SetDirty(mr); }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  메시
        // ══════════════════════════════════════════════════════════════════════

        private static void EnsureMeshes(out Mesh sphere, out Mesh spike, out Mesh blob)
        {
            sphere = SaveMesh(BuildSphere(), "SYM_NormalSoul");
            spike = SaveMesh(BuildSpike(), "SYM_Absorber");
            blob = SaveMesh(BuildBlob(), "SYM_ProliferatorBlob");
        }

        /// <summary>
        /// 정상 영혼 — 12각 × 6단의 매끈한 구. 「온전한 것」이므로 흠이 없어야 한다.
        /// 로우폴리 락 안이라 완전한 곡면이 아니라 12각으로 끊긴다.
        /// </summary>
        private static Mesh BuildSphere()
        {
            const int lat = 6, lon = 12;
            float r = NormalSoulSpan * 0.5f;
            return Lathe(lat, lon, (j, i) => 1f, r, "SYM_NormalSoul");
        }

        /// <summary>
        /// 증식체 조각 — 울퉁불퉁한 작은 돌. 결정론적 해시로 반지름을 흔든다.
        /// 매끈한 구와 **표면이 달라야** 세 종류가 축소 화면에서도 갈린다.
        /// </summary>
        private static Mesh BuildBlob()
        {
            const int lat = 3, lon = 5;
            float r = ProliferatorBlobSpan * 0.5f;
            return Lathe(lat, lon, (j, i) => 0.78f + ProcMeshBuilder.Hash01(j * 37 + i * 11 + 3) * 0.44f,
                         r, "SYM_ProliferatorBlob");
        }

        /// <summary>
        /// 위도·경도 격자로 만드는 덩어리. <paramref name="radiusAt"/> 가 1 을 돌려주면 구다.
        ///
        /// ⚠ 이웃 격자가 **같은 반지름 값**을 봐야 껍질이 닫힌다. 그래서 스케일 함수는
        /// (위도 인덱스, 경도 인덱스)만 받는다 — 좌표를 받으면 같은 정점이 두 값을 갖는다.
        /// </summary>
        private static Mesh Lathe(int lat, int lon, System.Func<int, int, float> radiusAt,
                                  float radius, string name)
        {
            var b = new ProcMeshBuilder(lat * lon * 6);
            const float uv = 8f;

            Vector3 V(int j, int i)
            {
                float ph = Mathf.PI * j / lat;
                float th = Mathf.PI * 2f * (i % lon) / lon;
                float k = radiusAt(j, i % lon);
                return new Vector3(Mathf.Sin(ph) * Mathf.Cos(th),
                                   Mathf.Sin(ph) * Mathf.Sin(th),
                                   Mathf.Cos(ph) * DepthFlatten) * (radius * k);
            }

            for (int j = 0; j < lat; j++)
            {
                for (int i = 0; i < lon; i++)
                {
                    Vector3 a = V(j, i), c = V(j, i + 1);
                    Vector3 d = V(j + 1, i + 1), e = V(j + 1, i);
                    Vector3 n = ((a + c + d + e) * 0.25f).normalized;
                    if (j == 0) b.AddTriangle(a, d, e, n, uv);
                    else if (j == lat - 1) b.AddTriangle(a, c, d, n, uv);
                    else b.AddQuad(a, c, d, e, n, uv);
                }
            }
            return b.ToMesh(name);
        }

        /// <summary>
        /// 흡수체 — 작은 핵에서 뻗은 **각뿔 스파이크 여덟.**
        ///
        /// 「빨아들이는 것」이므로 각이 서 있어야 하고, 그 각짐이 회색조에서도
        /// 매끈한 구와 갈리는 유일한 단서다. 스파이크 길이를 결정론적으로 흔들어
        /// **대칭 아이콘으로 읽히지 않게** 한다 — 완전 대칭이면 별표가 된다.
        /// </summary>
        private static Mesh BuildSpike()
        {
            var b = new ProcMeshBuilder(256);
            const float uv = 8f;

            // 길이 흔들림 계수를 **먼저** 다 뽑고 최대값으로 정규화한다.
            // 그래야 「가장 긴 스파이크 = AbsorberSpan/2」가 정의대로 참이 된다.
            var k = new float[8];
            float kMax = 0f;
            for (int i = 0; i < k.Length; i++)
            {
                k[i] = 0.86f + ProcMeshBuilder.Hash01(i * 173 + 29) * 0.28f;
                if (k[i] > kMax) kMax = k[i];
            }
            float rTip = AbsorberSpan * 0.5f / kMax;   // 스파이크 끝까지
            float rBase = rTip * 0.30f;                // 스파이크 밑동 반지름
            float rCore = rTip * 0.34f;                // 가운데 덩어리

            // 가운데 — 6면 각기둥. 스파이크만 있으면 가운데가 비어 보인다.
            b.AddPrism(Vector3.zero, rCore, rCore, rCore * 2f * DepthFlatten,
                       6, MeshAxis.Z, 0f, true, true, false, uv);

            // 스파이크 여덟. 정면(xy)에 여섯, 앞뒤(±z)에 하나씩.
            // 앞뒤 것은 실루엣에 안 들어오지만 깊이를 만들어 「평면 아이콘」을 막는다.
            var dirs = new List<Vector3>(8);
            for (int i = 0; i < 6; i++)
            {
                float a = (i * 60f + 8f) * Mathf.Deg2Rad;
                dirs.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f));
            }
            dirs.Add(new Vector3(0f, 0f, 1f));
            dirs.Add(new Vector3(0f, 0f, -1f));

            for (int i = 0; i < dirs.Count; i++)
            {
                // 길이 ±14% 흔들림. 해시라 매번 같은 형상이 나온다.
                float len = rTip * k[i];
                if (i >= 6) len *= DepthFlatten;       // 앞뒤는 챔버 여유만큼만
                int mark = b.Mark();
                // 끝을 0 으로 두면 퇴화 삼각형이 생긴다. 0.8mm 는 화면에서 점이다.
                b.AddPrism(new Vector3(0f, 0f, len * 0.5f), rBase, 0.0008f, len,
                           4, MeshAxis.Z, 45f, true, true, false, uv);
                b.TransformSince(mark, Vector3.zero,
                                 Quaternion.FromToRotation(Vector3.forward, dirs[i]));
            }
            return b.ToMesh("SYM_Absorber");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  에셋
        // ══════════════════════════════════════════════════════════════════════

        private static readonly Dictionary<string, Mesh> Baked = new Dictionary<string, Mesh>();

        /// <summary>
        /// 🔴 **`EditorUtility.CopySerialized` 를 쓰지 않는다.** 그것은 직렬화 필드만
        /// 복사하고 **이미 로드된 `Mesh` 의 렌더 표현을 다시 만들지 않는다** —
        /// 디스크는 새 형상인데 화면은 옛 형상을 계속 그린다. `AscendReferenceRoom.SaveMesh`
        /// 가 같은 함정을 통제 실험으로 확인해 기록해 뒀다. 채널을 직접 덮어쓴다.
        ///
        /// 에셋을 지우고 새로 만드는 방법도 쓰지 않는다 — GUID 가 바뀌어 씬의
        /// `MeshFilter` 참조가 통째로 끊긴다.
        /// </summary>
        private static Mesh SaveMesh(Mesh mesh, string key)
        {
            if (!Directory.Exists(MeshDir)) Directory.CreateDirectory(MeshDir);
            string path = $"{MeshDir}/{key}.asset";

            if (Baked.TryGetValue(path, out Mesh cached) && cached != null)
            {
                if (mesh != cached) Object.DestroyImmediate(mesh);
                return cached;
            }

            mesh.name = key;
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                existing.indexFormat = mesh.indexFormat;
                existing.SetVertices(mesh.vertices);
                existing.SetNormals(mesh.normals);
                existing.SetUVs(0, mesh.uv);
                existing.SetTriangles(mesh.triangles, 0, true);
                existing.RecalculateBounds();
                existing.name = key;
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                Baked[path] = existing;
                return existing;
            }

            AssetDatabase.CreateAsset(mesh, path);
            Baked[path] = mesh;
            return mesh;
        }

        /// <summary>
        /// 껍질 — 검붉고 반투명. 값은 <see cref="AscendReferenceRoom"/> 의 장식 영혼
        /// 재질에서 **그대로 가져왔다** (k=1.0 인 경우). 심볼이 그 자리를 대신하므로
        /// 방의 붉은 광원이 형태만 바뀌고 밝기는 유지된다 (`UP-FIX-53` 회귀 방지).
        ///
        /// 이름에 `Symbol` 이 들어가야 `AscendValueBandPass` 의 보호 목록에 걸린다 —
        /// 값 폭 압축이 이 재질을 회색으로 눌러 버리면 판독성이 다시 죽는다.
        /// </summary>
        private static Material ShellMaterial() => EnsureMaterial("SymbolShell", core: false);

        private static Material CoreMaterial() => EnsureMaterial("SymbolCore", core: true);

        private static Material EnsureMaterial(string key, bool core)
        {
            if (!Directory.Exists(MaterialDir)) Directory.CreateDirectory(MaterialDir);
            string path = $"{MaterialDir}/RM_{key}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Material m = existing ?? new Material(lit) { name = $"RM_{key}" };
            if (lit != null) m.shader = lit;

            // ⚠ **전 필드를 매번 다시 쓴다.** 기존 에셋을 재사용하면서 일부만 쓰면
            // 지운 코드의 값이 에셋에 남아 계속 작동한다 — 이 저장소가 발광에서
            // 한 번, 형상에서 한 번 당했다.
            m.SetTexture("_BaseMap", null);
            m.SetTexture("_EmissionMap", null);
            m.DisableKeyword("_EMISSIONMAP_ON");
            if (m.HasProperty("_EmissionMapEnabled")) m.SetFloat("_EmissionMapEnabled", 0f);
            m.SetFloat("_Metallic", 0f);

            if (core)
            {
                // 핵만 1 을 넘긴다. 작은 면적이라 블룸이 넓게 번지지 않아
                // 채널 포화 없이 「뜨겁다」가 읽힌다.
                m.SetFloat("_Smoothness", 0.55f);
                m.SetColor("_BaseColor", new Color(0.30f, 0.03f, 0.01f, 1f));
                m.SetColor("_EmissionColor", new Color(1.00f, 0.13f, 0.05f) * 1.15f);
                Opaque(m);
            }
            else
            {
                m.SetFloat("_Smoothness", 0.28f);
                m.SetColor("_BaseColor", new Color(0.105f, 0.011f, 0.007f, 0.62f));
                m.SetColor("_EmissionColor", new Color(0.62f, 0.055f, 0.020f) * 0.085f);
                Transparent(m);
            }
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            if (existing == null) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            return m;
        }

        private static void Transparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }

        private static void Opaque(Material m)
        {
            m.SetFloat("_Surface", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            m.SetFloat("_ZWrite", 1f);
            m.renderQueue = -1;
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
    }
}
