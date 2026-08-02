using System.Collections.Generic;
using UnityEngine;

namespace Ascend.Prototype.Art
{
    /// <summary>
    /// 절차적 로우폴리 **기본 도구 상자**. 여기 있는 것들은 프롭이 아니라 **부품**이다 —
    /// 벽 패널, 그레이팅, 파이프, 케이블, 레버, 상자, 그리고 반복되는 작은 금속 조각들.
    ///
    /// ## 왜 이것이 필요한가
    ///
    /// 독립 정찰이 잰 사실: 이 씬의 메시 **91개가 전부 Unity 기본 프리미티브**다
    /// (큐브 65 · 스피어 11 · 캡슐 9 · 실린더 6). 임포트된 `.fbx`/`.obj` 는 0개이고,
    /// `new Mesh()` 로 메시를 만드는 코드도 저장소에 0건이었다.
    ///
    /// **균일한 큐브는 로우폴리가 아니라 미완성이다.** `VISUAL_BIBLE.md` §2.1 이 요구하는
    /// 것은 「단순하고 각진 로우폴리」와 함께 「**큰 실루엣과 눈에 띄는 폴리곤 면**」이다.
    /// 폴리곤 면이 눈에 띄려면 면이 **여러 개**여야 하고, 큐브 하나는 한 방향에서 3면밖에
    /// 안 보인다. 모따기·리브·리벳·면 분할이 하는 일이 그것이다.
    ///
    /// ## 규약
    ///
    /// - **법선은 flat 이 기본**이다 (`GRAPHICS_TARGET.md` §G-4). smooth 는 명시 옵션.
    /// - **UV 는 월드 미터**다. `uvPerMeter` = 1 이면 1m 당 텍스처 1회. 텍스처는 `Repeat`.
    /// - **Transform 스케일을 쓰지 마라.** 원하는 크기를 인자로 준다. 스케일을 쓰면 UV 밀도가 어긋난다.
    /// - **결정론적**이다. 난수가 없고, 비대칭은 <see cref="ProcMeshBuilder.Hash01"/> 로 만든다.
    /// - 모든 형상은 **닫혀 있다.** 열린 면을 만들지 않는다 — 열린 면은 뒤에서 보면
    ///   내부가 비쳐 보이고, `Ascend/Stylized` 에는 양면 패스가 없다.
    ///
    /// ## 피벗
    ///
    /// | 함수 | 원점 |
    /// |---|---|
    /// | <see cref="Box"/> · <see cref="Rivet"/> | 중심 |
    /// | <see cref="Panel"/> · <see cref="Vent"/> · <see cref="Lever"/> · <see cref="Hinge"/> · <see cref="Bracket"/> · <see cref="Hook"/> | 벽 접촉면(z = 0), 앞이 +Z |
    /// | <see cref="Grating"/> · <see cref="Crate"/> | 바닥 접촉면(y = 0) |
    /// | <see cref="Pipe"/> | 아래 끝(y = 0), 위가 +Y |
    /// | <see cref="Cable"/> | 인자로 준 월드(또는 로컬) 좌표 그대로 |
    ///
    /// 각 함수에는 <c>Mesh</c> 를 돌려주는 판과 <see cref="ProcMeshBuilder"/> 에 **덧붙이는**
    /// 판이 있다. 프롭 조립에는 후자를 쓴다 — 메시 하나에 다 넣어야 드로우콜이 하나로 끝난다.
    /// </summary>
    public static class ProcMesh
    {
        // 로우폴리 락. 6~8 면이면 「원기둥」으로 읽히면서 면이 보인다.
        // 12 를 넘으면 매끈해져서 스타일이 깨지고, 4 이하면 각재로 읽힌다.
        public const int DefaultPipeSides = 6;
        public const int DefaultCableSides = 4;

        // ── Box ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 모따기 큐브. <paramref name="bevel"/> 0 이면 기본 큐브와 같은 12 삼각형,
        /// 0 보다 크면 44 삼각형이다.
        /// </summary>
        public static Mesh Box(Vector3 size, float bevel = 0f, float uvPerMeter = 1f)
        {
            var b = new ProcMeshBuilder(96);
            Box(b, size, bevel, uvPerMeter);
            return b.ToMesh("PM_Box");
        }

        public static void Box(ProcMeshBuilder b, Vector3 size, float bevel = 0f, float uvPerMeter = 1f)
            => b.AddBox(Vector3.zero, size, bevel, uvPerMeter);

        // ── Panel ────────────────────────────────────────────────────────────

        /// <summary>
        /// 리브와 리벳이 **형상으로** 있는 벽 패널. 텍스처로 그린 리벳은 정면에서만 리벳이고,
        /// 비스듬히 보면 평평한 자국이다 — 레퍼런스 이미지 5장이 전부 **돌출된** 리벳을 보여 준다.
        ///
        /// 구성: 판 + 수평 밴드 1개(`image.png` 의 3분할) + 수직 리브 N + 리브당 리벳 M.
        /// 삼각형 = 24 + ribs×12 + ribs×rivets×12.
        /// </summary>
        /// <param name="width">가로(m)</param>
        /// <param name="height">세로(m)</param>
        /// <param name="ribs">수직 리브 수. 0 이면 민판</param>
        /// <param name="rivetsPerRib">리브 하나당 리벳 수. 0 이면 리벳 없음</param>
        public static Mesh Panel(float width, float height, int ribs = 3, int rivetsPerRib = 4,
                                 float thickness = 0.06f, float uvPerMeter = 1f)
        {
            var b = new ProcMeshBuilder(512);
            Panel(b, width, height, ribs, rivetsPerRib, thickness, uvPerMeter);
            return b.ToMesh("PM_Panel");
        }

        public static void Panel(ProcMeshBuilder b, float width, float height, int ribs = 3,
                                 int rivetsPerRib = 4, float thickness = 0.06f, float uvPerMeter = 1f)
        {
            width = Mathf.Max(0.05f, width);
            height = Mathf.Max(0.05f, height);
            thickness = Mathf.Max(0.005f, thickness);

            // 판. 모따기를 살짝 줘서 패널 경계에 하이라이트 한 줄이 생기게 한다 —
            // 벽이 여러 장 붙었을 때 「한 장이 어디서 끝나는가」가 그 선으로 읽힌다.
            float plateBevel = Mathf.Min(thickness * 0.35f, Mathf.Min(width, height) * 0.02f);
            b.AddBox(new Vector3(0f, 0f, thickness * 0.5f), new Vector3(width, height, thickness),
                     plateBevel, uvPerMeter);

            // 수평 밴드. 레퍼런스의 벽은 전부 가로로 나뉘어 있고, 그 선이 없으면
            // 큰 벽이 「하나의 거대한 무지 면」이 된다 (`GRAPHICS_TARGET.md` §G-5).
            float bandH = Mathf.Min(0.10f, height * 0.06f);
            b.AddBox(new Vector3(0f, -height * 0.16f, thickness + bandH * 0.22f),
                     new Vector3(width, bandH, bandH * 0.55f), 0f, uvPerMeter);

            ribs = Mathf.Clamp(ribs, 0, 12);
            if (ribs <= 0) return;

            float ribW = Mathf.Min(0.09f, width / (ribs * 3f));
            float ribD = Mathf.Max(0.012f, thickness * 0.55f);
            float ribH = height * 0.94f;
            rivetsPerRib = Mathf.Clamp(rivetsPerRib, 0, 16);

            for (int r = 0; r < ribs; r++)
            {
                float t = (r + 0.5f) / ribs;
                float x = Mathf.Lerp(-width * 0.5f + ribW, width * 0.5f - ribW, t);
                b.AddBox(new Vector3(x, 0f, thickness + ribD * 0.5f),
                         new Vector3(ribW, ribH, ribD), ribD * 0.3f, uvPerMeter);

                for (int k = 0; k < rivetsPerRib; k++)
                {
                    float ty = (k + 0.5f) / rivetsPerRib;
                    // 리벳 간격에 아주 작은 결정론적 흔들림을 준다. 완벽한 등간격은
                    // 「깨끗하고 대칭적인 쇼룸」(금지 9)으로 읽히고, 1960년대 산업 시설은
                    // 그렇게 만들어지지 않았다.
                    float jitter = ProcMeshBuilder.HashSigned(r * 131 + k) * ribH * 0.006f;
                    float y = Mathf.Lerp(-ribH * 0.46f, ribH * 0.46f, ty) + jitter;
                    Rivet(b, new Vector3(x, y, thickness + ribD),
                          ribW * 0.30f, ribW * 0.26f, MeshAxis.Z, 4, uvPerMeter);
                }
            }
        }

        // ── Grating ──────────────────────────────────────────────────────────

        /// <summary>
        /// 바닥 그레이팅. 바가 **따로 떨어진 상자**라서 사이에 진짜 구멍이 뚫린다 —
        /// 텍스처로 그린 격자는 위에서 보면 격자지만 비스듬히 보면 평평한 그림이다.
        /// 구멍이 실재하면 그 아래 어둠이 보이고, 그것이 `G-5` 의 「빈 평면 비율」을 직접 낮춘다.
        /// </summary>
        /// <param name="w">가로(m, X)</param>
        /// <param name="d">세로(m, Z)</param>
        /// <param name="barCount">주 바 수(Z 방향으로 배열, X 로 뻗음)</param>
        /// <param name="barWidth">바 폭(m)</param>
        public static Mesh Grating(float w, float d, int barCount = 7, float barWidth = 0.02f,
                                   float thickness = 0.03f, float uvPerMeter = 1f)
        {
            var b = new ProcMeshBuilder(384);
            Grating(b, w, d, barCount, barWidth, thickness, uvPerMeter);
            return b.ToMesh("PM_Grating");
        }

        public static void Grating(ProcMeshBuilder b, float w, float d, int barCount = 7,
                                   float barWidth = 0.02f, float thickness = 0.03f, float uvPerMeter = 1f)
        {
            w = Mathf.Max(0.05f, w);
            d = Mathf.Max(0.05f, d);
            thickness = Mathf.Max(0.004f, thickness);
            barWidth = Mathf.Clamp(barWidth, 0.004f, d * 0.2f);
            barCount = Mathf.Clamp(barCount, 1, 32);

            float frame = Mathf.Max(barWidth * 1.6f, thickness);
            float cy = thickness * 0.5f;

            // 테두리 4개. 프레임이 없으면 바 끝이 허공에 떠서 「잘라 낸 조각」으로 보인다.
            b.AddBox(new Vector3(0f, cy, (d - frame) * 0.5f), new Vector3(w, thickness, frame), 0f, uvPerMeter);
            b.AddBox(new Vector3(0f, cy, -(d - frame) * 0.5f), new Vector3(w, thickness, frame), 0f, uvPerMeter);
            b.AddBox(new Vector3((w - frame) * 0.5f, cy, 0f), new Vector3(frame, thickness, d - frame * 2f), 0f, uvPerMeter);
            b.AddBox(new Vector3(-(w - frame) * 0.5f, cy, 0f), new Vector3(frame, thickness, d - frame * 2f), 0f, uvPerMeter);

            // 주 바. 테두리 안쪽에서만 배열하고, 프레임에 **살짝 파고들게** 한다 —
            // 정확히 맞대면 같은 면이 두 장 겹쳐 z-파이팅이 난다.
            float inner = d - frame * 2f;
            float span = w - frame * 1.2f;
            for (int i = 0; i < barCount; i++)
            {
                float t = barCount == 1 ? 0.5f : (i + 0.5f) / barCount;
                float z = Mathf.Lerp(-inner * 0.5f + barWidth, inner * 0.5f - barWidth, t);
                b.AddBox(new Vector3(0f, cy, z), new Vector3(span, thickness * 0.86f, barWidth), 0f, uvPerMeter);
            }

            // 직교 보강 바 2개. 격자가 한 방향뿐이면 「빗살」이지 그레이팅이 아니다.
            for (int i = 0; i < 2; i++)
            {
                float x = (i == 0 ? -1f : 1f) * (w * 0.22f);
                b.AddBox(new Vector3(x, cy + thickness * 0.06f, 0f),
                         new Vector3(barWidth, thickness * 0.7f, inner), 0f, uvPerMeter);
            }
        }

        // ── Pipe ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 각진 파이프. <paramref name="bends"/> 만큼 옆으로 꺾이고, 꺾이는 자리마다
        /// **접합 블록**이 붙는다.
        ///
        /// 왜 매끄러운 엘보가 아니라 블록인가: ① 로우폴리 락에서 엘보 하나가 파이프 전체보다
        /// 비싸다 ② 1960년대 산업 배관은 실제로 플랜지와 접합 박스로 이어져 있다
        /// ③ 관을 한 줄로 꺾으면 꺾인 자리의 링이 찌그러져 형태가 무너진다.
        ///
        /// ⚠ `VISUAL_BIBLE.md` 금지 6 — **기능 없는 배관을 늘리지 마라.** 파이프는
        /// 「무엇이 무엇에 연결되는가」를 설명할 때만 놓는다.
        /// </summary>
        public static Mesh Pipe(float radius, float length, int sides = DefaultPipeSides,
                                int bends = 0, float uvPerMeter = 1f)
        {
            var b = new ProcMeshBuilder(256);
            Pipe(b, radius, length, sides, bends, uvPerMeter);
            return b.ToMesh("PM_Pipe");
        }

        public static void Pipe(ProcMeshBuilder b, float radius, float length,
                                int sides = DefaultPipeSides, int bends = 0, float uvPerMeter = 1f)
        {
            radius = Mathf.Max(0.004f, radius);
            length = Mathf.Max(0.02f, length);
            sides = Mathf.Clamp(sides, 3, 12);
            bends = Mathf.Clamp(bends, 0, 4);

            int runs = bends + 1;
            float runLen = length / runs;
            float x = 0f, z = 0f;

            for (int r = 0; r < runs; r++)
            {
                float y0 = runLen * r;
                b.AddPrism(new Vector3(x, y0 + runLen * 0.5f, z), radius, radius, runLen,
                           sides, MeshAxis.Y, 0f, true, true, false, uvPerMeter);

                if (r == runs - 1) break;

                // 다음 구간의 가로 이동. 해시라서 같은 인자면 같은 파이프가 나온다.
                float dx = ProcMeshBuilder.HashSigned(r * 17 + 3) * radius * 3.2f;
                float dz = ProcMeshBuilder.HashSigned(r * 29 + 11) * radius * 3.2f;
                float nx = x + dx, nz = z + dz;

                // 접합 블록 — 두 구간의 축을 모두 덮는다.
                b.AddBox(new Vector3((x + nx) * 0.5f, y0 + runLen, (z + nz) * 0.5f),
                         new Vector3(Mathf.Abs(dx) + radius * 2.4f,
                                     radius * 2.2f,
                                     Mathf.Abs(dz) + radius * 2.4f),
                         radius * 0.35f, uvPerMeter);
                x = nx; z = nz;
            }
        }

        // ── Cable ────────────────────────────────────────────────────────────

        /// <summary>
        /// 늘어진 케이블(카테너리 근사 = 포물선). 물리 레인이 이 메시를 흔들 것이므로
        /// **가운데가 실제로 처져 있어야** 흔들림이 읽힌다 — 직선을 흔들면 막대가 흔들린다.
        /// </summary>
        /// <param name="sag">중앙 처짐(m). 0 이면 직선</param>
        /// <param name="segments">경로 분할 수. 4~10 이면 충분하다</param>
        public static Mesh Cable(Vector3 from, Vector3 to, float sag, int segments = 6,
                                 float radius = 0.012f, int sides = DefaultCableSides,
                                 bool smooth = false, float uvPerMeter = 1f)
        {
            var b = new ProcMeshBuilder(256);
            Cable(b, from, to, sag, segments, radius, sides, smooth, uvPerMeter);
            return b.ToMesh("PM_Cable");
        }

        public static void Cable(ProcMeshBuilder b, Vector3 from, Vector3 to, float sag,
                                 int segments = 6, float radius = 0.012f,
                                 int sides = DefaultCableSides, bool smooth = false,
                                 float uvPerMeter = 1f)
        {
            segments = Mathf.Clamp(segments, 2, 24);
            var path = new List<Vector3>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector3 p = Vector3.Lerp(from, to, t);
                // 포물선 처짐. 낮은 진폭에서 카테너리와 시각적으로 구분되지 않고,
                // cosh 와 달리 부동소수 누적이 없어 비트 단위로 재현된다.
                p.y -= sag * 4f * t * (1f - t);
                path.Add(p);
            }
            b.AddTube(path, radius, sides, false, true, smooth, uvPerMeter);
        }

        // ── Lever ────────────────────────────────────────────────────────────

        /// <summary>
        /// 손잡이·축·베이스가 **다른 굵기**인 레버. 굵기가 같으면 「어디를 잡는가」가 안 읽히고,
        /// 그것이 `VISUAL_SPEC.md` §5 「상호작용물이 배경과 분리될 것」의 실패 형태다.
        ///
        /// 축은 `pullDeg` 만큼 기울어 있다. 0 이면 벽에서 똑바로 튀어나온다.
        /// 씬 소유자가 이 각도를 애니메이션하려면 **메시가 아니라 Transform 을 돌린다** —
        /// 메시를 다시 만들면 `MeshBuildPhase` 가 막는다.
        /// </summary>
        public static Mesh Lever(float handleLength = 0.34f, float knobRadius = 0.05f,
                                 float pullDeg = -22f, float uvPerMeter = 1f)
        {
            var b = new ProcMeshBuilder(256);
            Lever(b, handleLength, knobRadius, pullDeg, uvPerMeter);
            return b.ToMesh("PM_Lever");
        }

        public static void Lever(ProcMeshBuilder b, float handleLength = 0.34f,
                                 float knobRadius = 0.05f, float pullDeg = -22f, float uvPerMeter = 1f)
        {
            handleLength = Mathf.Max(0.05f, handleLength);
            knobRadius = Mathf.Max(0.008f, knobRadius);

            float plateW = knobRadius * 4.4f;
            float plateT = 0.028f;
            // 베이스 판 — 벽에 붙는 면. 모따기가 실루엣을 세운다.
            b.AddBox(new Vector3(0f, 0f, plateT * 0.5f), new Vector3(plateW, plateW * 1.25f, plateT),
                     plateT * 0.32f, uvPerMeter);
            // 축 하우징 — 판보다 좁고 두껍다.
            float housD = knobRadius * 1.9f;
            b.AddBox(new Vector3(0f, 0f, plateT + housD * 0.5f),
                     new Vector3(knobRadius * 2.6f, knobRadius * 2.6f, housD), knobRadius * 0.3f, uvPerMeter);

            // 축 + 목 칼라 + 손잡이 뭉치를 한 덩어리로 만들고 통째로 기울인다.
            Vector3 pivot = new Vector3(0f, 0f, plateT + housD);
            int mark = b.Mark();
            float shaftR = knobRadius * 0.42f;
            b.AddPrism(new Vector3(0f, 0f, handleLength * 0.5f), shaftR, shaftR * 0.86f,
                       handleLength, 6, MeshAxis.Z, 0f, true, true, false, uvPerMeter);
            // 칼라 — 축이 하우징에서 나오는 지점. 「여기가 회전축이다」를 형상으로 말한다.
            b.AddPrism(new Vector3(0f, 0f, handleLength * 0.16f), shaftR * 1.75f, shaftR * 1.75f,
                       handleLength * 0.09f, 6, MeshAxis.Z, 0f, true, true, false, uvPerMeter);
            // 손잡이 — 축의 2.4배 굵기. 손이 닿는 곳이 가장 굵다.
            b.AddPrism(new Vector3(0f, 0f, handleLength * 0.94f), knobRadius, knobRadius * 0.82f,
                       knobRadius * 1.9f, 6, MeshAxis.Z, 30f, true, true, false, uvPerMeter);
            b.TransformSince(mark, pivot, ProcQuat.Euler(pullDeg, 0f, 0f));
        }

        // ── Crate ────────────────────────────────────────────────────────────

        /// <summary>
        /// 판자가 **보이는** 화물 상자. 판자 사이 홈이 실제 형상이라 비스듬히 봐도 판자다.
        /// </summary>
        public static Mesh Crate(Vector3 size, int plankCount = 3, float uvPerMeter = 1f)
        {
            var b = new ProcMeshBuilder(384);
            Crate(b, size, plankCount, uvPerMeter);
            return b.ToMesh("PM_Crate");
        }

        public static void Crate(ProcMeshBuilder b, Vector3 size, int plankCount = 3, float uvPerMeter = 1f)
        {
            float w = Mathf.Max(0.06f, size.x);
            float h = Mathf.Max(0.06f, size.y);
            float d = Mathf.Max(0.06f, size.z);
            plankCount = Mathf.Clamp(plankCount, 1, 6);

            float post = Mathf.Min(0.05f, Mathf.Min(w, d) * 0.12f);
            // 몸통은 기둥보다 안쪽으로 들어가 있다. 그래야 모서리 기둥이 실루엣에 남는다.
            b.AddBox(new Vector3(0f, h * 0.5f, 0f),
                     new Vector3(w - post * 0.9f, h - post * 0.5f, d - post * 0.9f),
                     post * 0.28f, uvPerMeter);

            // 모서리 기둥은 모따기하지 않는다 — 넷이면 176 삼각형이고, 이미 몸통이
            // 안쪽으로 들어가 있어서 기둥의 각이 실루엣에 그대로 남는다.
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                b.AddBox(new Vector3(sx * (w - post) * 0.5f, h * 0.5f, sz * (d - post) * 0.5f),
                         new Vector3(post, h, post), 0f, uvPerMeter);
            }

            // 앞뒤 면의 가로 판자. 두께가 조금씩 다르다 — 같으면 「새 상자」로 읽힌다.
            float plankH = (h * 0.86f) / plankCount;
            for (int face = 0; face < 2; face++)
            {
                float sz = face == 0 ? 1f : -1f;
                for (int i = 0; i < plankCount; i++)
                {
                    float y = h * 0.07f + plankH * (i + 0.5f);
                    float t = 0.012f + ProcMeshBuilder.Hash01(face * 71 + i) * 0.006f;
                    b.AddBox(new Vector3(0f, y, sz * ((d - post * 0.9f) * 0.5f + t * 0.5f)),
                             new Vector3(w - post * 2.2f, plankH * 0.82f, t), 0f, uvPerMeter);
                }
            }
        }

        // ── 작은 반복 프롭 ──────────────────────────────────────────────────

        /// <summary>
        /// 리벳/볼트 머리. 축 방향으로 <paramref name="height"/> 만큼 튀어나온다.
        /// 벽에는 <see cref="MeshAxis.Z"/>, 바닥에는 <see cref="MeshAxis.Y"/> 를 쓴다.
        /// </summary>
        public static Mesh Rivet(float radius = 0.012f, float height = 0.008f,
                                 MeshAxis axis = MeshAxis.Z, int sides = 5, float uvPerMeter = 1f)
        {
            var b = new ProcMeshBuilder(48);
            Rivet(b, Vector3.zero, radius, height, axis, sides, uvPerMeter);
            return b.ToMesh("PM_Rivet");
        }

        /// <summary>표면 위 <paramref name="basePoint"/> 에서 축 방향으로 솟는 리벳.</summary>
        public static void Rivet(ProcMeshBuilder b, Vector3 basePoint, float radius = 0.012f,
                                 float height = 0.008f, MeshAxis axis = MeshAxis.Z,
                                 int sides = 5, float uvPerMeter = 1f)
        {
            radius = Mathf.Max(0.002f, radius);
            height = Mathf.Max(0.001f, height);
            Vector3 dir = axis == MeshAxis.X ? Vector3.right
                        : axis == MeshAxis.Y ? Vector3.up : Vector3.forward;
            // 위가 좁은 원뿔대 = 돔 머리. 완전한 반구는 면이 안 보여서 스타일에 안 맞는다.
            b.AddPrism(basePoint + dir * (height * 0.5f), radius, radius * 0.62f, height,
                       sides, axis, 0f, true, true, false, uvPerMeter);
        }

        /// <summary>
        /// L 자 브래킷. 벽(+Y 판)과 선반(+Z 판)을 잇고 대각 보강재가 붙는다.
        /// 원점은 두 판이 만나는 안쪽 모서리, 벽면은 z = 0.
        /// </summary>
        public static Mesh Bracket(float armZ = 0.22f, float armY = 0.20f,
                                   float thickness = 0.016f, float width = 0.05f, float uvPerMeter = 1f)
        {
            var b = new ProcMeshBuilder(128);
            Bracket(b, armZ, armY, thickness, width, uvPerMeter);
            return b.ToMesh("PM_Bracket");
        }

        public static void Bracket(ProcMeshBuilder b, float armZ = 0.22f, float armY = 0.20f,
                                   float thickness = 0.016f, float width = 0.05f, float uvPerMeter = 1f)
        {
            armZ = Mathf.Max(0.02f, armZ);
            armY = Mathf.Max(0.02f, armY);
            thickness = Mathf.Max(0.003f, thickness);
            width = Mathf.Max(0.006f, width);

            b.AddBox(new Vector3(0f, armY * 0.5f, thickness * 0.5f),
                     new Vector3(width, armY, thickness), 0f, uvPerMeter);
            b.AddBox(new Vector3(0f, thickness * 0.5f, armZ * 0.5f),
                     new Vector3(width, thickness, armZ), 0f, uvPerMeter);

            // 대각 보강재. 이것이 없으면 「ㄱ 자로 접은 판」이고, 있으면 「하중을 받는 부재」다.
            float diagLen = Mathf.Sqrt(armY * armY + armZ * armZ) * 0.82f;
            float angle = Mathf.Atan2(armY, armZ) * Mathf.Rad2Deg;
            b.AddBox(new Vector3(0f, armY * 0.42f, armZ * 0.42f),
                     new Vector3(width * 0.7f, thickness, diagLen),
                     ProcQuat.Euler(angle, 0f, 0f), 0f, uvPerMeter);
        }

        /// <summary>경첩. 통 하나 + 잎 둘. 원점은 통의 중심, 벽 쪽 잎이 −Z.</summary>
        public static Mesh Hinge(float length = 0.10f, float barrelRadius = 0.012f,
                                 float leaf = 0.05f, float uvPerMeter = 1f)
        {
            var b = new ProcMeshBuilder(128);
            Hinge(b, length, barrelRadius, leaf, uvPerMeter);
            return b.ToMesh("PM_Hinge");
        }

        public static void Hinge(ProcMeshBuilder b, float length = 0.10f, float barrelRadius = 0.012f,
                                 float leaf = 0.05f, float uvPerMeter = 1f)
        {
            length = Mathf.Max(0.02f, length);
            barrelRadius = Mathf.Max(0.003f, barrelRadius);
            leaf = Mathf.Max(0.01f, leaf);
            float leafT = barrelRadius * 0.7f;

            b.AddPrism(Vector3.zero, barrelRadius, barrelRadius, length, 6, MeshAxis.Y,
                       0f, true, true, false, uvPerMeter);
            b.AddBox(new Vector3(0f, 0f, -(leaf * 0.5f + barrelRadius * 0.4f)),
                     new Vector3(leafT, length * 0.86f, leaf), 0f, uvPerMeter);
            b.AddBox(new Vector3(0f, 0f, leaf * 0.5f + barrelRadius * 0.4f),
                     new Vector3(leafT, length * 0.86f, leaf), 0f, uvPerMeter);
        }

        /// <summary>
        /// 환풍구. 테두리 + 기울어진 슬랫. 슬랫이 기울어 있어야 그림자가 줄무늬로 떨어지고,
        /// 그 줄무늬가 `G-1` 의 국소 분산을 올린다.
        /// </summary>
        public static Mesh Vent(float width = 0.36f, float height = 0.24f, int slats = 5,
                                float depth = 0.05f, float uvPerMeter = 1f)
        {
            var b = new ProcMeshBuilder(256);
            Vent(b, width, height, slats, depth, uvPerMeter);
            return b.ToMesh("PM_Vent");
        }

        public static void Vent(ProcMeshBuilder b, float width = 0.36f, float height = 0.24f,
                                int slats = 5, float depth = 0.05f, float uvPerMeter = 1f)
        {
            width = Mathf.Max(0.04f, width);
            height = Mathf.Max(0.04f, height);
            depth = Mathf.Max(0.008f, depth);
            slats = Mathf.Clamp(slats, 1, 12);

            float f = Mathf.Min(0.028f, Mathf.Min(width, height) * 0.12f);
            float cz = depth * 0.5f;
            // 테두리는 **모따기하지 않는다.** 모따기 상자는 12 → 44 삼각형이라 테두리
            // 넷만으로 176 이 되는데, 환풍구의 실루엣은 슬랫 줄무늬가 만들지 테두리가
            // 만들지 않는다. 예산은 형태가 읽히는 데 쓴다.
            b.AddBox(new Vector3(0f, (height - f) * 0.5f, cz), new Vector3(width, f, depth), 0f, uvPerMeter);
            b.AddBox(new Vector3(0f, -(height - f) * 0.5f, cz), new Vector3(width, f, depth), 0f, uvPerMeter);
            b.AddBox(new Vector3((width - f) * 0.5f, 0f, cz), new Vector3(f, height - f * 2f, depth), 0f, uvPerMeter);
            b.AddBox(new Vector3(-(width - f) * 0.5f, 0f, cz), new Vector3(f, height - f * 2f, depth), 0f, uvPerMeter);

            float inner = height - f * 2f;
            float slatH = inner / (slats + 0.4f);
            for (int i = 0; i < slats; i++)
            {
                float t = (i + 0.5f) / slats;
                float y = Mathf.Lerp(-inner * 0.5f + slatH * 0.6f, inner * 0.5f - slatH * 0.6f, t);
                b.AddBox(new Vector3(0f, y, depth * 0.42f),
                         new Vector3(width - f * 1.6f, slatH * 0.8f, depth * 0.5f),
                         ProcQuat.Euler(34f, 0f, 0f), 0f, uvPerMeter);
            }
        }

        /// <summary>
        /// 화물 고리. 눈판 + 목 + 굽은 갈고리. 원점은 벽 접촉면(z = 0), 갈고리가 아래로 휜다.
        /// </summary>
        public static Mesh Hook(float reach = 0.14f, float thickness = 0.014f, float uvPerMeter = 1f)
        {
            var b = new ProcMeshBuilder(192);
            Hook(b, reach, thickness, uvPerMeter);
            return b.ToMesh("PM_Hook");
        }

        public static void Hook(ProcMeshBuilder b, float reach = 0.14f, float thickness = 0.014f,
                                float uvPerMeter = 1f)
        {
            reach = Mathf.Max(0.03f, reach);
            thickness = Mathf.Max(0.003f, thickness);

            float plate = thickness * 4.2f;
            b.AddBox(new Vector3(0f, 0f, thickness * 0.6f), new Vector3(plate, plate * 1.15f, thickness * 1.2f),
                     thickness * 0.3f, uvPerMeter);
            b.AddPrism(new Vector3(0f, 0f, thickness * 1.2f + reach * 0.22f), thickness, thickness,
                       reach * 0.44f, 6, MeshAxis.Z, 0f, true, true, false, uvPerMeter);

            // 갈고리 — 원호 5점. 굽은 것이 형상으로 있어야 「걸 수 있다」가 읽힌다.
            float z0 = thickness * 1.2f + reach * 0.44f;
            float r = reach * 0.42f;
            var path = new List<Vector3>(5);
            for (int i = 0; i < 5; i++)
            {
                float a = Mathf.Lerp(0f, 205f, i / 4f) * Mathf.Deg2Rad;
                path.Add(new Vector3(0f, -r + Mathf.Cos(a) * r, z0 + Mathf.Sin(a) * r));
            }
            b.AddTube(path, thickness * 0.92f, 4, false, true, false, uvPerMeter);
        }
    }
}
