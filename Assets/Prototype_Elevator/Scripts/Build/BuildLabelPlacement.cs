using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Ascend.Prototype.Player;
using Ascend.Prototype.View;

namespace Ascend.Prototype.Build
{
    /// <summary>
    /// 한 번의 배치 판정이 **실제로 무엇을 했는가.**
    ///
    /// 8차 판정에서 이 규칙들은 「넣었다」고 적혔지만 그림에서는 **한 번도 물러선 흔적이
    /// 없었다**(`07_cargo_full` 이름표 6개 전부 표시). 규칙이 안 걸린 것인지 규칙이
    /// 실행조차 안 된 것인지 구분할 방법이 코드에 없었던 것이 진짜 결함이다.
    /// 숫자를 남기면 다음 라운드가 그림을 보기 전에 답을 안다.
    /// </summary>
    public struct LabelResolveStats
    {
        /// <summary>등록된 라벨 수(꺼진 홀더 포함).</summary>
        public int Entries;
        /// <summary>화면에 투영까지 성공한 수. 카메라 뒤·꺼진 홀더는 여기서 빠진다.</summary>
        public int Projected;
        /// <summary>글자 사각형이 화면 밖이라 뺀 수.</summary>
        public int OffFrame;
        /// <summary>배킹판이 없거나 크기를 못 재서 뺀 수.</summary>
        public int NoPlate;
        /// <summary>1~4순위와 겹쳐 물러선 수.</summary>
        public int YieldedHigherRank;
        /// <summary>더 가까운 동급에 물러선 수.</summary>
        public int YieldedPeer;
        /// <summary>이번 판정이 **실제로 사각형까지 만든** 상위 위계 상자 수.</summary>
        public int Obstacles;
        /// <summary>수집돼 있던 상위 위계 렌더러 수. 0 이면 수집 자체가 실패한 것이다.</summary>
        public int ObstacleSources;
        public int Visible;

        public override string ToString() =>
            $"라벨 {Entries} · 투영 {Projected} · 화면밖 {OffFrame} · 배킹판없음 {NoPlate}" +
            $" · 위계양보 {YieldedHigherRank} · 동급양보 {YieldedPeer}" +
            $" · 장애물 {Obstacles}/{ObstacleSources} · 표시 {Visible}";
    }

    /// <summary>
    /// 월드 라벨 배치의 **순수 기하**. Unity 오브젝트를 하나도 만들지 않으므로
    /// 헤드리스 검사가 가능하다(`BuildTests` 의 「월드 라벨 배치」 구획).
    ///
    /// 여기 상수를 고칠 때는 `TOPDOWN_MASTER_BACKLOG.md` §5.1 을 먼저 읽는다 —
    /// **크기 축(<see cref="MinScale"/>·<see cref="ReferenceDistance"/>)은 이미 두 번
    /// 시도해 두 독립 평가가 「축이 틀렸다」고 판정했다.** 그 둘은 건드리지 않는다.
    /// </summary>
    public static class BuildLabelGeometry
    {
        /// <summary>이 거리 밖에서는 크기를 손대지 않는다. §5.1 이 고정한 값.</summary>
        public const float ReferenceDistance = 1.8f;

        /// <summary>축소 하한. §5.1 이 「더 낮추지 않는다」고 못박은 값.</summary>
        public const float MinScale = 0.35f;

        /// <summary>
        /// 보는 사람 쪽으로 당기는 거리(m). **이것이 `UP-FIX-12` 의 축이다.**
        ///
        /// 라벨이 유리 통관·선반 슬래브·벽 안쪽 깊이에 놓여 잘리는 것을 크기로 고칠 수
        /// 없다(가림은 스케일 불변). 시선 방향으로 당기면 화면 위치는 그대로인 채
        /// 라벨과 카메라 사이의 물체만 사라진다.
        ///
        /// 0.30 인 근거: 씬에서 가장 깊이 묻힌 라벨은 6번 자리(-0.88, ·, -0.45)의
        /// 이름표이고, 그 자리를 감싼 `TubeFrame`(x -1.10..-0.80) 의 안쪽 면까지는
        /// **0.08 m** 다. 캡처 시점 아홉을 전부 계산했을 때 +X 로 벌어 오는 거리의
        /// 최솟값은 0.135 m(`DeviceSide`, 당김이 0.156 로 잘리는 자리)로 0.08 m 보다
        /// 크다 — `Contract` 하나를 빼면 **모든 시점에서 상자를 빠져나온다**
        /// (`BuildLabelPlacementTests` 가 이 값을 검사한다).
        /// </summary>
        public const float DepthPush = 0.30f;

        /// <summary>
        /// 당긴 뒤에도 카메라에서 이보다 가까워지지 않는다.
        ///
        /// 값이 <see cref="MinScale"/> × <see cref="ReferenceDistance"/> = 0.63 인 것이
        /// 핵심이다 — **축소 하한이 걸리기 시작하는 바로 그 거리**다. 여기까지만 당기면
        /// 축소가 항상 거리에 비례하는 구간에 머물러, 당김이 화면 크기 상한
        /// (1 ÷ 1.8)을 **올리지 못한다.**
        ///
        /// 처음엔 0.45 로 뒀는데 그러면 0.65 m 짜리 라벨이 0.45 m 로 당겨지면서
        /// 하한 0.35 에 걸려 화면 크기가 **40% 커졌다.** 「크기 축을 건드리지 않는다」를
        /// 스스로 깨는 값이었다.
        /// </summary>
        public const float MinViewerDistance = MinScale * ReferenceDistance;

        /// <summary>
        /// 화면 경계. **글자 사각형**이 이 밖으로 나가면 그리지 않는다.
        /// 잘린 글자는 정보가 아니라 흠집이다 — 4·5·7차 평가가 세 번 같은 말을 했다.
        ///
        /// 판정 대상이 판이 아니라 글자인 것이 중요하다. 판까지 온전히 들어오라고 하면
        /// `07_cargo_full` 에서 여섯 이름표가 **전부** 사라진다(윗변이 0.99~1.06).
        /// 여백이 몇 픽셀 잘리는 것은 결함이 아니다.
        /// </summary>
        public const float EdgeLimit = 0.995f;

        /// <summary>
        /// 내 판의 이만큼이 더 높은 위계(`VISUAL_SPEC` §5 1~4순위) 위에 놓이면 물러선다.
        /// 승객 이름표는 **5순위**다 — 5순위가 2순위를 덮는 그림은 그 자체로 반려 사유다.
        /// </summary>
        public const float YieldToHigherRank = 0.15f;

        /// <summary>
        /// 상대가 작을 때를 위한 반대편 기준. 계약 명판 한 줄처럼 화면에서 얇은 것은
        /// 내 면적의 3% 만 겹쳐도 **그쪽에서는 절반이 덮인 것**이다.
        /// </summary>
        public const float CoverHigherRank = 0.10f;

        /// <summary>
        /// 「상대의 몇 %를 덮었나」를 물을 가치가 있는 최소 화면 면적(정규화 좌표, 전체 화면 = 4).
        /// 0.0006 은 1920×1080 에서 약 310 px² — 17×17 픽셀이다. 이보다 작은 것은
        /// 무엇을 덮든 판정이 요동치므로 **내 면적 기준만** 쓴다.
        /// </summary>
        public const float MinObstacleArea = 0.0006f;

        /// <summary>같은 5순위끼리는 이만큼 겹칠 때만 먼 쪽이 물러선다.</summary>
        public const float YieldToNearerPeer = 0.25f;

        /// <summary>거리에 따른 축소. 기존 규칙 그대로다.</summary>
        public static float Shrink(float distance)
            => Mathf.Clamp(distance / ReferenceDistance, MinScale, 1f);

        /// <summary>실제로 당길 거리. 카메라 코앞에서는 0 으로 수렴한다.</summary>
        public static float PushDistance(float viewerDistance)
            => Mathf.Clamp(viewerDistance - MinViewerDistance, 0f, DepthPush);

        /// <summary>앵커를 보는 사람 쪽으로 당긴 위치. 시선 위에 있으므로 화면 좌표는 불변이다.</summary>
        public static Vector3 PushTowardViewer(Vector3 anchor, Vector3 viewer)
        {
            Vector3 delta = viewer - anchor;
            float distance = delta.magnitude;
            if (distance <= 0.0001f) return anchor;
            return anchor + delta * (PushDistance(distance) / distance);
        }

        /// <summary>
        /// 화면에서 차지하는 크기 계수(= 스케일 ÷ 거리). **당김이 이 값의 상한을 올리지
        /// 않는다**는 것이 「크기 축을 건드리지 않았다」의 검사 가능한 형태다.
        /// </summary>
        public static float ApparentSize(float viewerDistance)
        {
            float pushed = viewerDistance - PushDistance(viewerDistance);
            return pushed <= 0.0001f ? 0f : Shrink(pushed) / pushed;
        }

        /// <summary>정규화 화면 좌표(-1..+1). 뒤쪽이면 false.</summary>
        public static bool Project(Vector3 world, Vector3 camPos,
                                   Vector3 right, Vector3 up, Vector3 forward,
                                   float tanHalfVertical, float aspect,
                                   out Vector2 ndc, out float depth)
        {
            Vector3 delta = world - camPos;
            depth = Vector3.Dot(delta, forward);
            ndc = Vector2.zero;
            if (depth <= 0.01f || tanHalfVertical <= 0.0001f || aspect <= 0.0001f) return false;
            float k = 1f / (depth * tanHalfVertical);
            ndc = new Vector2(Vector3.Dot(delta, right) * k / aspect, Vector3.Dot(delta, up) * k);
            return true;
        }

        /// <summary>
        /// 라벨이 그리는 자세. `Solve` 와 검사가 **같은 식**을 쓰게 하려고 밖으로 뺐다.
        /// 라벨은 수평으로만 돈다 — 위아래로 꺾인 이름표는 판 위의 글자가 아니라 종잇조각으로 읽힌다.
        /// </summary>
        public static Quaternion FaceRotation(Vector3 placed, Vector3 camPos)
        {
            Vector3 toReader = placed - camPos;
            toReader.y = 0f;
            return toReader.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(toReader, Vector3.up)
                : Quaternion.identity;
        }

        /// <summary>
        /// 판 사각형과 **글자 사각형**을 함께 투영한다.
        /// 겹침 판정은 판(불투명하게 덮는 면적), 화면 경계 판정은 글자로 한다.
        ///
        /// `Transform` 을 받지 않는 것이 요점이다 — 이 식이 씬 오브젝트에 용접돼 있어서
        /// 8차의 판정 규칙은 **한 줄도 검사할 수 없었다.**
        /// </summary>
        public static bool ProjectLabelRects(Vector3 placed, Quaternion rotation, float shrink,
                                             Vector2 plateCenter, Vector2 plateHalf,
                                             Vector3 camPos, Vector3 right, Vector3 up, Vector3 forward,
                                             float tanHalf, float aspect,
                                             out Rect plate, out Rect glyphs, out float depth)
        {
            plate = default(Rect);
            glyphs = default(Rect);
            depth = float.MaxValue;

            Vector2 textHalf = new Vector2(
                Mathf.Max(plateHalf.x - BuildLabelPlate.PadX, 0.001f),
                Mathf.Max(plateHalf.y - BuildLabelPlate.PadY, 0.001f));

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            float gMinX = float.MaxValue, gMaxX = float.MinValue;
            float gMinY = float.MaxValue, gMaxY = float.MinValue;

            for (int c = 0; c < 4; c++)
            {
                float sx = (c & 1) == 0 ? -1f : 1f;
                float sy = (c & 2) == 0 ? -1f : 1f;

                Vector3 plateLocal = new Vector3(
                    (plateCenter.x + sx * plateHalf.x) * shrink,
                    (plateCenter.y + sy * plateHalf.y) * shrink,
                    0f);
                if (!Project(placed + rotation * plateLocal, camPos, right, up, forward,
                             tanHalf, aspect, out Vector2 ndc, out float d))
                    return false;
                if (ndc.x < minX) minX = ndc.x;
                if (ndc.x > maxX) maxX = ndc.x;
                if (ndc.y < minY) minY = ndc.y;
                if (ndc.y > maxY) maxY = ndc.y;
                if (d < depth) depth = d;

                Vector3 textLocal = new Vector3(
                    (plateCenter.x + sx * textHalf.x) * shrink,
                    (plateCenter.y + sy * textHalf.y) * shrink,
                    0f);
                if (!Project(placed + rotation * textLocal, camPos, right, up, forward,
                             tanHalf, aspect, out Vector2 g, out _))
                    return false;
                if (g.x < gMinX) gMinX = g.x;
                if (g.x > gMaxX) gMaxX = g.x;
                if (g.y < gMinY) gMinY = g.y;
                if (g.y > gMaxY) gMaxY = g.y;
            }

            plate = Rect.MinMaxRect(minX, minY, maxX, maxY);
            glyphs = Rect.MinMaxRect(gMinX, gMinY, gMaxX, gMaxY);
            return true;
        }

        /// <summary>월드 축 정렬 상자를 화면 사각형으로. 뒤쪽 모서리는 버린다(보수적).</summary>
        public static bool ProjectBounds(Bounds bounds, Vector3 camPos,
                                         Vector3 right, Vector3 up, Vector3 forward,
                                         float tanHalf, float aspect, out Rect rect)
        {
            rect = default(Rect);
            Vector3 c = bounds.center, e = bounds.extents;
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            bool anyFront = false;

            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    c.x + ((i & 1) == 0 ? -e.x : e.x),
                    c.y + ((i & 2) == 0 ? -e.y : e.y),
                    c.z + ((i & 4) == 0 ? -e.z : e.z));
                if (!Project(corner, camPos, right, up, forward, tanHalf, aspect,
                             out Vector2 ndc, out _))
                    continue;
                anyFront = true;
                if (ndc.x < minX) minX = ndc.x;
                if (ndc.x > maxX) maxX = ndc.x;
                if (ndc.y < minY) minY = ndc.y;
                if (ndc.y > maxY) maxY = ndc.y;
            }

            if (!anyFront) return false;
            rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        /// <summary>`mine` 면적 중 `other` 와 겹치는 비율.</summary>
        public static float OverlapFractionOf(Rect mine, Rect other)
        {
            float w = Mathf.Min(mine.xMax, other.xMax) - Mathf.Max(mine.xMin, other.xMin);
            if (w <= 0f) return 0f;
            float h = Mathf.Min(mine.yMax, other.yMax) - Mathf.Max(mine.yMin, other.yMin);
            if (h <= 0f) return 0f;
            float area = mine.width * mine.height;
            return area <= 0f ? 0f : (w * h) / area;
        }

        /// <summary>사각형 전체가 화면 안에 있는가.</summary>
        public static bool InsideFrame(Rect rect, float limit)
            => rect.xMin >= -limit && rect.xMax <= limit &&
               rect.yMin >= -limit && rect.yMax <= limit;

        public static bool InsideFrame(Rect rect) => InsideFrame(rect, EdgeLimit);

        /// <summary>
        /// **표시 여부 판정 전체.** `Renderer` 도 `Camera` 도 받지 않으므로 헤드리스로 돈다.
        ///
        /// 이 함수가 따로 있는 이유가 8차의 교훈 그 자체다 — 판정 규칙이
        /// `Renderer`·`Camera` 에 용접돼 있어 **한 줄도 검사할 수 없었고**, 그래서
        /// 「규칙을 넣었다」와 「규칙이 돌았다」의 차이가 그림이 나올 때까지 드러나지 않았다.
        /// 문턱값은 하나도 바꾸지 않았다 — 옮기기만 했다.
        ///
        /// 순서가 규칙이다. ① 배킹판 없는 것을 먼저 뺀다(불투명 판이 없는 5순위는
        /// `VISUAL_SPEC` §5 상 존재해서는 안 된다) → ② 상위 위계 양보 → ③ 동급 양보.
        /// ②를 ③보다 먼저 두는 것이 중요하다. 가까운 쪽이 위계 양보로 사라진 뒤에
        /// 먼 쪽이 남아야, 「둘 다 사라지는」 구멍이 생기지 않는다.
        /// </summary>
        /// <param name="plates">라벨의 판 사각형(정규화 화면 좌표).</param>
        /// <param name="depths">카메라 깊이. 동급 판정에서 가까운 쪽을 고른다.</param>
        /// <param name="hasPlate">불투명 배킹판이 실제로 붙어 있고 크기가 유효한가.</param>
        /// <param name="obstacles">1~4순위 상자들의 화면 사각형.</param>
        /// <param name="show">들어올 때 「투영에 성공하고 화면 안」인 것이 true. 나갈 때 최종 판정.</param>
        /// <param name="order">정렬용 스크래치. 호출자가 재사용해 할당을 없앤다.</param>
        public static void ResolveVisibility(List<Rect> plates, List<float> depths,
                                             List<bool> hasPlate, List<Rect> obstacles,
                                             List<bool> show, List<int> order,
                                             ref LabelResolveStats stats)
        {
            if (plates == null || depths == null || hasPlate == null || show == null || order == null) return;
            int n = show.Count;
            if (n > plates.Count) n = plates.Count;
            if (n > depths.Count) n = depths.Count;
            if (n > hasPlate.Count) n = hasPlate.Count;

            // ① 배킹판 강제. 8차 판정 §9 의 「① 불투명 배킹판 강제」가 이 세 줄이다.
            for (int i = 0; i < n; i++)
            {
                if (!show[i] || hasPlate[i]) continue;
                show[i] = false;
                stats.NoPlate++;
            }

            // ② 상위 위계 양보.
            if (obstacles != null)
            {
                for (int o = 0; o < obstacles.Count; o++)
                {
                    Rect box = obstacles[o];
                    bool measurable = box.width * box.height >= MinObstacleArea;
                    for (int i = 0; i < n; i++)
                    {
                        if (!show[i]) continue;
                        if (OverlapFractionOf(plates[i], box) > YieldToHigherRank ||
                            (measurable && OverlapFractionOf(box, plates[i]) > CoverHigherRank))
                        {
                            show[i] = false;
                            stats.YieldedHigherRank++;
                        }
                    }
                }
            }

            // ③ 동급 양보 — 가까운 쪽이 이긴다(배킹판이 불투명해 먼 쪽은 어차피 반쪽이다).
            while (order.Count < n) order.Add(0);
            int live = 0;
            for (int i = 0; i < n; i++) if (show[i]) order[live++] = i;

            for (int i = 1; i < live; i++)
            {
                int v = order[i];
                int j = i - 1;
                while (j >= 0 && (depths[order[j]] > depths[v] ||
                                 (depths[order[j]] == depths[v] && order[j] > v)))
                {
                    order[j + 1] = order[j];
                    j--;
                }
                order[j + 1] = v;
            }

            for (int a = 0; a < live; a++)
            {
                int near = order[a];
                if (!show[near]) continue;
                for (int b = a + 1; b < live; b++)
                {
                    int far = order[b];
                    if (!show[far]) continue;
                    if (OverlapFractionOf(plates[far], plates[near]) > YieldToNearerPeer)
                    {
                        show[far] = false;
                        stats.YieldedPeer++;
                    }
                }
            }

            for (int i = 0; i < n; i++) if (show[i]) stats.Visible++;
        }
    }

    /// <summary>
    /// 월드 라벨을 **그리는 카메라마다** 다시 배치한다.
    ///
    /// **왜 카메라마다인가**: 라벨을 플레이어 머리 쪽으로만 돌리면 고정 캡처(전용
    /// 카메라)에서 뒷면이 잡힌다. `BuildFigureView` 는 그것을 단면 재질로 막았지만,
    /// 단면은 「거울상 대신 아무것도 없음」일 뿐이라 **캡처에서 라벨이 통째로 사라진다.**
    /// 실측: `07_cargo_full`(pose `CargoBay`)에서 여섯 자리 중 1·2번 자리의 법선이
    /// 캡처 카메라 반대쪽을 향해(내적 −0.79 / −0.91) 컬링됐다.
    ///
    /// URP 는 카메라마다 <see cref="RenderPipelineManager.beginCameraRendering"/> 를
    /// 컬링 **전에** 부르므로, 거기서 자세를 잡으면 카메라가 몇 대든 각자 정면을 본다.
    ///
    /// 배치 규칙은 셋이고 전부 `VISUAL_SPEC` §5 위계에서 나온다.
    ///   ① **당김** — 시선 방향으로 <see cref="BuildLabelGeometry.DepthPush"/> 당겨
    ///      벽·통관·선반 안쪽 깊이에서 빼낸다. 화면 좌표는 그대로다.
    ///   ② **배킹판** — 글자 뒤에 불투명 판을 깔아 배경과의 값 대비를 조명과 무관하게 고정한다.
    ///   ③ **양보** — 1~4순위(통관·결과판·계기판·레버·계약 명판·벽 기록문)와 겹치거나
    ///      화면 밖으로 나가면 **그리지 않는다.** 5순위가 잘린 채 2순위를 덮는 것보다 낫다.
    /// </summary>
    public sealed class BuildLabelLayout
    {
        /// <summary>라벨 하나. 홀더는 <see cref="BuildFigureView"/> 루트의 자식이다.</summary>
        private struct Entry
        {
            public Transform Holder;
            public Transform Follow;       // 따라다니는 승객·부품 루트
            public float Rise;             // Follow.position 에서 위로 이만큼이 앵커다
            public Renderer Text;
            public Renderer Plate;
            public Vector2 PlateCenter;    // 홀더 로컬
            public Vector2 PlateHalf;      // 홀더 로컬
            /// <summary>글자 경계를 실제로 재서 판을 맞췄는가. 못 잰 판은 없는 것으로 친다.</summary>
            public bool PlateValid;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<Rect> _rects = new List<Rect>();
        private readonly List<Rect> _glyphRects = new List<Rect>();
        private readonly List<float> _depths = new List<float>();
        private readonly List<bool> _show = new List<bool>();
        private readonly List<bool> _hasPlate = new List<bool>();
        private readonly List<int> _order = new List<int>();
        private readonly List<Rect> _obstacleRects = new List<Rect>();
        private LabelResolveStats _stats;

        private readonly List<Renderer> _obstacles = new List<Renderer>();
        private readonly List<Renderer> _scratch = new List<Renderer>();
        private readonly HashSet<Renderer> _known = new HashSet<Renderer>();
        private bool _obstaclesValid;

        /// <summary>탐색 상한. 씬이 커져도 프레임 예산을 먹지 않게 한다.</summary>
        private const int MaxObstacles = 128;

        public int Count => _entries.Count;

        /// <summary>직전 판정이 실제로 한 일. 캡처 매니페스트와 검사가 이 숫자를 읽는다.</summary>
        public LabelResolveStats LastStats => _stats;

        public void Clear()
        {
            _entries.Clear();
            _obstaclesValid = false;
            _stats = default(LabelResolveStats);
        }

        /// <summary>라벨을 등록한다. 배킹판 크기는 <see cref="BuildLabelPlate"/> 가 잰 값이다.</summary>
        public void Add(Transform holder, Transform follow, float rise,
                        Renderer text, Renderer plate, Vector2 plateCenter, Vector2 plateHalf,
                        bool plateValid)
        {
            _entries.Add(new Entry
            {
                Holder = holder,
                Follow = follow,
                Rise = rise,
                Text = text,
                Plate = plate,
                PlateCenter = plateCenter,
                PlateHalf = plateHalf,
                PlateValid = plateValid,
            });
            _obstaclesValid = false;
        }

        /// <summary>대사가 바뀌거나 글자 경계를 뒤늦게 재서 판 크기가 달라졌을 때.</summary>
        public void UpdatePlate(Renderer text, Renderer plate,
                                Vector2 plateCenter, Vector2 plateHalf, bool plateValid)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Text != text) continue;
                Entry e = _entries[i];
                if (plate != null) e.Plate = plate;
                e.PlateCenter = plateCenter;
                e.PlateHalf = plateHalf;
                e.PlateValid = plateValid;
                _entries[i] = e;
                return;
            }
        }

        /// <summary>
        /// 한 카메라에 대해 자세·크기·가시성을 정한다.
        /// 카메라가 여러 대면 각자 자기 차례에 다시 부른다 — 마지막 값이 남지만
        /// 각 카메라는 **자기 렌더 직전에** 자기 값을 쓰므로 그림은 각자 맞다.
        /// </summary>
        public void Solve(Camera camera)
        {
            if (camera == null || _entries.Count == 0) return;

            Transform ct = camera.transform;
            Vector3 camPos = ct.position;
            Vector3 right = ct.right, up = ct.up, forward = ct.forward;
            float tanHalf = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float aspect = camera.aspect;
            bool perspective = !camera.orthographic && tanHalf > 0.0001f && aspect > 0.0001f;

            EnsureCapacity();
            _stats = default(LabelResolveStats);
            _stats.Entries = _entries.Count;
            _stats.ObstacleSources = _obstacles.Count;

            // ① 자세·크기·당김
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry e = _entries[i];
                _show[i] = false;
                _hasPlate[i] = e.Plate != null && e.PlateValid;
                _rects[i] = default(Rect);
                _glyphRects[i] = default(Rect);
                _depths[i] = float.MaxValue;

                if (e.Holder == null || e.Text == null) continue;
                if (!e.Holder.gameObject.activeInHierarchy) continue;

                Vector3 anchor = e.Follow != null
                    ? e.Follow.position + Vector3.up * e.Rise
                    : e.Holder.position;

                float distance = Vector3.Distance(anchor, camPos);
                Vector3 placed = BuildLabelGeometry.PushTowardViewer(anchor, camPos);
                float shrink = BuildLabelGeometry.Shrink(
                    distance - BuildLabelGeometry.PushDistance(distance));

                // 카메라와 수평 위치가 정확히 같은 퇴화 상황에서는 이전 자세를 유지한다
                // (`FaceRotation` 이 항등을 주는데, 그때 항등으로 튀면 라벨이 한 프레임 뒤집힌다).
                Vector3 flat = placed - camPos; flat.y = 0f;
                if (flat.sqrMagnitude > 0.0001f)
                    e.Holder.rotation = BuildLabelGeometry.FaceRotation(placed, camPos);
                e.Holder.position = placed;
                e.Holder.localScale = Vector3.one * shrink;

                _show[i] = true;
                if (!perspective) continue;

                if (!BuildLabelGeometry.ProjectLabelRects(
                        placed, e.Holder.rotation, shrink, e.PlateCenter, e.PlateHalf,
                        camPos, right, up, forward, tanHalf, aspect,
                        out Rect rect, out Rect glyphs, out float depth))
                {
                    _show[i] = false;   // 카메라 뒤
                    continue;
                }
                _rects[i] = rect;
                _glyphRects[i] = glyphs;
                _depths[i] = depth;
                _stats.Projected++;

                // ② 글자가 화면 밖으로 삐져나가면 그리지 않는다
                if (!BuildLabelGeometry.InsideFrame(glyphs)) { _show[i] = false; _stats.OffFrame++; }
            }

            if (perspective)
            {
                CollectObstacleRects(camPos, right, up, forward, tanHalf, aspect);
                BuildLabelGeometry.ResolveVisibility(_rects, _depths, _hasPlate, _obstacleRects,
                                                     _show, _order, ref _stats);
            }
            else
            {
                for (int i = 0; i < _entries.Count; i++) if (_show[i]) _stats.Visible++;
            }

            // ③ 적용
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry e = _entries[i];
                if (e.Text != null && e.Text.enabled != _show[i]) e.Text.enabled = _show[i];
                if (e.Plate != null && e.Plate.enabled != _show[i]) e.Plate.enabled = _show[i];
            }
        }

        private void EnsureCapacity()
        {
            while (_rects.Count < _entries.Count) _rects.Add(default(Rect));
            while (_glyphRects.Count < _entries.Count) _glyphRects.Add(default(Rect));
            while (_depths.Count < _entries.Count) _depths.Add(0f);
            while (_show.Count < _entries.Count) _show.Add(false);
            while (_hasPlate.Count < _entries.Count) _hasPlate.Add(false);
            while (_order.Count < _entries.Count) _order.Add(0);
        }

        /// <summary>
        /// 1~4순위 렌더러를 **화면 사각형으로 바꾸기만 한다.** 판정은
        /// <see cref="BuildLabelGeometry.ResolveVisibility"/> 가 한다.
        ///
        /// 살아 있는 라벨의 합집합과 안 겹치는 상자는 버린다 — 비용을 위한 것이고
        /// 판정을 바꾸지 않는다(버려진 상자는 어차피 어느 라벨과도 안 겹친다).
        /// </summary>
        private void CollectObstacleRects(Vector3 camPos, Vector3 right, Vector3 up, Vector3 forward,
                                          float tanHalf, float aspect)
        {
            _obstacleRects.Clear();

            float unionMinX = float.MaxValue, unionMaxX = float.MinValue;
            float unionMinY = float.MaxValue, unionMaxY = float.MinValue;
            bool any = false;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (!_show[i]) continue;
                any = true;
                Rect r = _rects[i];
                if (r.xMin < unionMinX) unionMinX = r.xMin;
                if (r.xMax > unionMaxX) unionMaxX = r.xMax;
                if (r.yMin < unionMinY) unionMinY = r.yMin;
                if (r.yMax > unionMaxY) unionMaxY = r.yMax;
            }
            if (!any) return;
            Rect union = Rect.MinMaxRect(unionMinX, unionMinY, unionMaxX, unionMaxY);

            for (int o = 0; o < _obstacles.Count; o++)
            {
                Renderer obstacle = _obstacles[o];
                if (obstacle == null) { _obstaclesValid = false; continue; }
                // 꺼져 있는 것은 그려지지 않으므로 자리를 주장하지 않는다.
                // `isVisible` 은 **직전 프레임의 다른 카메라** 컬링 결과라 여기서 쓰면
                // 카메라마다 다른 판정이 나온다 — 쓰지 않는다.
                if (!obstacle.enabled || !obstacle.gameObject.activeInHierarchy) continue;

                if (!ProjectBounds(obstacle.bounds, camPos, right, up, forward, tanHalf, aspect,
                                   out Rect box)) continue;
                if (!box.Overlaps(union)) continue;
                _obstacleRects.Add(box);
            }

            _stats.Obstacles = _obstacleRects.Count;
        }

        private static bool ProjectBounds(Bounds bounds, Vector3 camPos,
                                          Vector3 right, Vector3 up, Vector3 forward,
                                          float tanHalf, float aspect, out Rect rect)
            => BuildLabelGeometry.ProjectBounds(bounds, camPos, right, up, forward,
                                                tanHalf, aspect, out rect);

        // ── 장애물 수집 ──────────────────────────────────────────────────────
        //
        // **씬 배선을 요구하지 않는다.** 배선을 요구하면 씬 오너의 손이 빌 때까지
        // 이 수정이 캡처에 실리지 못하고, 배선을 빠뜨린 채 찍으면 규칙이 조용히 꺼진다.
        // 대신 위계마다 이미 존재하는 컴포넌트로 찾는다.
        //
        //   1순위 레버        `InteractableLever` · `InteractableOverharvestLever`
        //   2순위 결과판·통관  `TubeController` (3×3 칸과 심볼이 그 자식이다)
        //   3순위 계기판       `InstrumentPanelView`
        //   4순위 계약·기록문  월드 공간 `TextMeshPro` 전부(내 것 제외)

        public void RefreshObstacles(IList<Renderer> mine)
        {
            _obstacles.Clear();
            _known.Clear();
            if (mine != null)
                for (int i = 0; i < mine.Count; i++)
                    if (mine[i] != null) _known.Add(mine[i]);

            var tubes = Object.FindObjectsByType<TubeController>(FindObjectsSortMode.None);
            for (int i = 0; i < tubes.Length; i++) AddSubtree(tubes[i]);

            var panels = Object.FindObjectsByType<InstrumentPanelView>(FindObjectsSortMode.None);
            for (int i = 0; i < panels.Length; i++) AddSubtree(panels[i]);

            var levers = Object.FindObjectsByType<InteractableLever>(FindObjectsSortMode.None);
            for (int i = 0; i < levers.Length; i++) AddSubtree(levers[i]);

            var over = Object.FindObjectsByType<InteractableOverharvestLever>(FindObjectsSortMode.None);
            for (int i = 0; i < over.Length; i++) AddSubtree(over[i]);

            var texts = Object.FindObjectsByType<TextMeshPro>(FindObjectsSortMode.None);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] == null) continue;
                var renderer = texts[i].GetComponent<Renderer>();
                if (renderer == null || !_known.Add(renderer)) continue;
                if (_obstacles.Count < MaxObstacles) _obstacles.Add(renderer);
            }

            _obstaclesValid = true;
        }

        private void AddSubtree(Component root)
        {
            if (root == null) return;
            root.GetComponentsInChildren(true, _scratch);
            for (int i = 0; i < _scratch.Count; i++)
            {
                Renderer renderer = _scratch[i];
                if (renderer == null || !_known.Add(renderer)) continue;
                if (_obstacles.Count < MaxObstacles) _obstacles.Add(renderer);
            }
        }

        public bool ObstaclesValid => _obstaclesValid;

        /// <summary>다음 판정 전에 상위 위계 목록을 다시 모으게 한다.</summary>
        public void InvalidateObstacles() => _obstaclesValid = false;
    }

    /// <summary>
    /// 라벨 뒤의 **불투명 배킹판**을 만들고 글자 크기에 맞춘다.
    ///
    /// **왜 필요한가**: 이름표는 조명이 물드는 벽·궤짝·다른 라벨 위에 겹쳐 놓인다.
    /// TMP 는 정점 색 그대로 그리는 무광 셰이더라 글자 밝기는 고정인데 배경은 위험
    /// 단계마다 움직이므로, 대비가 상태에 따라 0 에 가까워지는 구간이 생긴다
    /// (`UP-FIX-14` 「가림은 스케일 불변이다」의 나머지 절반).
    /// 판을 깔면 **대비가 조명과 무관한 상수**가 된다.
    ///
    /// 판은 글자 렉트가 아니라 **실제로 그려진 글자 경계**에 맞춘다. 렉트(이름표 1.6 m)
    /// 에 맞추면 세 글자 이름 뒤에 1.6 m 짜리 검은 널빤지가 선다.
    /// </summary>
    public static class BuildLabelPlate
    {
        /// <summary>글자 경계 바깥 여백(로컬 단위 = 스케일 1 에서 미터).</summary>
        public const float PadX = 0.055f;
        public const float PadY = 0.035f;

        /// <summary>글자 뒤로 물리는 깊이. 판이 앞서면 글자가 사라진다.</summary>
        public const float BackOffset = 0.012f;

        /// <summary>글자 경계를 못 재면 쓰는 하한(로컬 반크기).</summary>
        public const float MinHalf = 0.05f;

        /// <summary>
        /// 글자 경계를 재서 판 중심·반크기를 돌려준다.
        /// `ForceMeshUpdate` 없이 읽으면 방금 넣은 문자열이 반영되지 않는다.
        ///
        /// **돌려주는 bool 이 이 함수의 요점이다.** 예전에는 못 잰 경우에도 렉트 기반
        /// 대체값을 조용히 돌려줬다. 그러면 호출자는 「쟀다」와 「못 재서 찍었다」를
        /// 구분할 수 없고, 판이 엉뚱한 크기로 한 번 굳은 뒤 아무도 다시 재지 않는다 —
        /// 이름표의 판은 <c>AddComponent&lt;TextMeshPro&gt;()</c> 와 **같은 프레임에**
        /// 딱 한 번 측정된다. TMP 의 <c>OnPreRenderObject</c> 는 그 시점에 정당하게
        /// 아무 일도 하지 않을 수 있고(비활성·미각성), 그러면 <c>textBounds</c> 는
        /// 문자 0개짜리 경계를 준다. 실패를 돌려주면 호출자가 다음 프레임에 다시 잰다.
        /// </summary>
        /// <returns>실제로 그려진 글자 경계를 읽었으면 true. false 면 대체값이다.</returns>
        public static bool Measure(TMP_Text text, out Vector2 center, out Vector2 half)
        {
            center = Vector2.zero;
            half = new Vector2(MinHalf, MinHalf);
            if (text == null) return false;

            text.ForceMeshUpdate();
            Bounds bounds = text.textBounds;
            float hx = bounds.extents.x;
            float hy = bounds.extents.y;

            bool measured = hx > 0.0001f && hy > 0.0001f && !float.IsNaN(hx) && !float.IsNaN(hy);
            if (!measured)
            {
                // 글자가 없거나 아직 안 지어졌다 — 렉트의 절반을 보수적으로 쓴다.
                RectTransform rect = text.rectTransform;
                hx = rect != null ? rect.sizeDelta.x * 0.25f : MinHalf;
                hy = rect != null ? rect.sizeDelta.y * 0.35f : MinHalf;
                center = Vector2.zero;
            }
            else
            {
                center = new Vector2(bounds.center.x, bounds.center.y);
            }

            half = new Vector2(Mathf.Max(hx + PadX, MinHalf), Mathf.Max(hy + PadY, MinHalf));
            return measured;
        }

        /// <summary>
        /// 이미 붙어 있는 판을 찾는다. **이름으로 찾는 이유**: TMP 는 폰트 폴백이
        /// 필요하면 자식으로 `TMP SubMesh` 를 스스로 만든다. 한글 문자열에서 실제로
        /// 일어나므로 `GetChild(0)` 은 판이 아닐 수 있다.
        /// </summary>
        public static Renderer Find(TMP_Text text)
        {
            if (text == null) return null;
            Transform t = text.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform child = t.GetChild(i);
                if (child.name == PlateName) return child.GetComponent<Renderer>();
            }
            return null;
        }

        public const string PlateName = "Backing";

        /// <summary>판을 만들어 붙인다. 이미 있으면 크기만 맞춘다.</summary>
        public static Renderer Attach(TMP_Text text, Material material, Renderer existing,
                                      Vector2 center, Vector2 half)
        {
            if (text == null) return existing;

            Renderer plate = existing;
            if (plate == null)
            {
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = PlateName;
                // 조준은 승객 루트 콜라이더 하나로 받는다 — 판이 레이를 가로채면
                // 프롬프트가 이름표를 가리킨다.
                Collider collider = quad.GetComponent<Collider>();
                if (collider != null)
                {
                    if (Application.isPlaying) Object.Destroy(collider);
                    else Object.DestroyImmediate(collider);
                }
                quad.transform.SetParent(text.transform, false);
                plate = quad.GetComponent<Renderer>();
                if (plate != null)
                {
                    plate.sharedMaterial = material;
                    plate.shadowCastingMode = ShadowCastingMode.Off;
                    plate.receiveShadows = false;
                }
            }
            else if (plate.sharedMaterial == null && material != null)
            {
                // 재질이 빠진 판은 그려지지 않는데 `enabled` 는 true 라 아무도 모른다.
                plate.sharedMaterial = material;
            }

            if (plate == null) return null;
            Transform t = plate.transform;
            t.localPosition = new Vector3(center.x, center.y, BackOffset);
            t.localRotation = Quaternion.identity;
            t.localScale = new Vector3(half.x * 2f, half.y * 2f, 1f);
            return plate;
        }

        /// <summary>판 재질. 조명을 받지 않아야 대비가 위험 단계와 무관해진다.</summary>
        public static Material CreateMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Color")
                            ?? Shader.Find("Sprites/Default");
            if (shader == null) return null;   // 못 찾으면 null 을 돌려준다 — 조용히 자홍색 판을 세우지 않는다

            var material = new Material(shader) { name = "BuildLabelBacking (runtime)" };
            var color = new Color(0.030f, 0.034f, 0.029f, 1f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            material.color = color;

            // **양면으로 둔다.** 판의 앞면이 어느 쪽인지가 8차 조사에서 확정되지 않았다 —
            // 기본 사각형의 법선이 로컬 −Z 라면 지금 그대로 보이고, +Z 라면 뒷면 컬링으로
            // **통째로 사라진다.** 8차 캡처 24장에서 배킹판이 보이는 장이 0장이었고
            // (라벨 자리 픽셀 최소 휘도 23 · 프레임 전체 최소 5 — 검은 판이 있었다면
            // 휘도 8 짜리 화소가 나왔어야 한다), 그 가설을 0 비용으로 닫는 방법이 이것이다.
            // 무광 단색 판은 뒤에서 봐도 같은 그림이라 앞면이 맞았더라도 변화가 없다.
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            if (material.HasProperty("_CullMode")) material.SetFloat("_CullMode", 0f);
            material.doubleSidedGI = true;
            return material;
        }
    }
}
