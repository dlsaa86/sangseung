using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ascend.Prototype.Art
{
    /// <summary>프롭이 어디에 붙는가. 피벗 규약이 여기서 갈린다.</summary>
    public enum PropMount
    {
        /// <summary>바닥에 놓인다. 원점 = 바닥 접촉면(y = 0), X·Z 중심, 정면 +Z.</summary>
        Floor = 0,
        /// <summary>벽에 붙는다. 원점 = 벽 접촉면(z = 0), X·Y 중심, 밖으로 +Z.</summary>
        Wall = 1,
        /// <summary>천장에 매달린다. 원점 = 천장 접촉면(y = 0), 아래로 −Y.</summary>
        Ceiling = 2,
    }

    /// <summary>프롭 24종의 식별자. 순서가 <see cref="PropLibrary.Specs"/> 의 순서다.</summary>
    public enum PropId
    {
        FireExtinguisher = 0,
        ToolBox = 1,
        Bucket = 2,
        Broom = 3,
        PipeBundle = 4,
        ElectricalBox = 5,
        WarningSign = 6,
        EmergencyLamp = 7,
        VentGrille = 8,
        FloorHatch = 9,
        DrainGrate = 10,
        GrabRail = 11,
        HangingChain = 12,
        CargoPallet = 13,
        WoodenCrate = 14,
        Sack = 15,
        GaugeHousing = 16,
        CableReel = 17,
        BoltBin = 18,
        Bench = 19,
        DustyShelf = 20,
        PosterBoard = 21,
        CraneRail = 22,
        CargoHook = 23,
    }

    /// <summary>프롭 하나의 계약 — 이름·부착면·공칭 크기·삼각형 예산.</summary>
    public readonly struct PropSpec
    {
        public readonly PropId Id;
        public readonly string DisplayName;
        public readonly PropMount Mount;
        /// <summary>공칭 경계 상자(m). 검증이 실제 메시 bounds 를 이 값과 대조한다.</summary>
        public readonly Vector3 NominalSize;
        /// <summary>이 프롭이 넘으면 안 되는 삼각형 수.</summary>
        public readonly int TriangleBudget;

        public PropSpec(PropId id, string displayName, PropMount mount, Vector3 nominalSize, int budget)
        {
            Id = id;
            DisplayName = displayName;
            Mount = mount;
            NominalSize = nominalSize;
            TriangleBudget = budget;
        }
    }

    /// <summary>
    /// 캐빈에 실제로 놓을 **프롭 24종**. `GRAPHICS_TARGET.md` §G-5 가 요구하는
    /// 「캐빈 내부 고유 프롭 종 수 ≥ 24」를 채우는 것이 이 클래스의 목적이다.
    ///
    /// ## 어휘 — 무엇을 만들고 무엇을 만들지 않았는가
    ///
    /// 전부 **「낡은 1960년대 산업용 화물 엘리베이터」에 실제로 있을 물건**이다.
    /// `VISUAL_BIBLE.md` §4 금지 21항 중 이 클래스가 특히 조심한 것:
    ///
    /// - **금지 4 (카지노 슬롯머신)** · **금지 3 (스팀펑크 장식)** · **금지 5 (장식용 황동과 톱니)**
    ///   — 9라운드 연속 스타일 1점을 받고 있는 항목이다. 그래서 여기에는 **돌아가지 않는
    ///   톱니도, 장식용 황동 부품도, 릴·레버형 유희 기구도 없다.** 릴은 하나 있지만
    ///   그건 **케이블 릴**이고, 감긴 것이 케이블이라는 것이 형상으로 보인다.
    /// - **금지 6 (과도한 파이프와 계기판)** — 배관 다발과 계기 하우징은 **각 1종**이다.
    ///   벽을 파이프로 덮는 데 쓰라고 만든 것이 아니다.
    /// - **금지 9 (깨끗한 대칭 쇼룸)** — 자루·상자·판자에 결정론적 비대칭을 넣었다.
    /// - **금지 10 (작은 디테일로 실루엣 흐리기)** — 프롭당 300 삼각형이 상한이다.
    ///   그 예산이 「디테일을 못 넣는 제약」이 아니라 **「큰 형태에 쓰라」는 지시**다.
    ///
    /// ## 규약
    ///
    /// - 피벗은 <see cref="PropMount"/> 가 정한다. 씬 소유자가 좌표를 계산하지 않아도
    ///   바닥/벽/천장에 **원점을 그대로 붙이면** 맞게 놓인다.
    /// - 모든 메시가 **flat normal** 이다 (`GRAPHICS_TARGET.md` §G-4).
    /// - **Transform 스케일을 쓰지 마라.** UV 가 미터 기준이라 스케일하면 텍셀 밀도가 어긋난다.
    ///   크기를 바꾸려면 여기 값을 고친다.
    /// - 프롭 하나 ≤ 300 삼각형, 24종 합계 ≤ 5,000. 로우폴리가 **스타일 락**이다.
    /// </summary>
    public static class PropLibrary
    {
        public const int PropCount = 24;
        /// <summary>프롭 하나의 상한. 넘으면 검증이 실패한다.</summary>
        public const int PerPropTriangleLimit = 300;
        /// <summary>24종 합계 상한.</summary>
        public const int TotalTriangleLimit = 5000;

        // **공칭 크기는 실측에서 나왔다.** 손으로 어림한 숫자가 아니라 헤드리스 검증이
        // 잰 경계 상자를 1cm 단위로 올림한 값이고, `ProcMeshTests` 가 매번 다시 대조한다.
        //
        // 이 숫자가 정확해야 하는 이유: 씬 소유자가 배치 간격을 여기서 읽는다. 어림값이면
        // 「스펙대로 놓았는데 겹친다」가 되고, 그건 씬을 다시 여는 비용으로 돌아온다
        // (`.unity` 는 직렬화 에셋이라 되돌리기가 비싸다).
        // 검증은 **양쪽**을 건다 — 실측이 공칭을 넘지 않고, 공칭의 절반 아래로도 안 내려간다.
        // 후자가 없으면 공칭을 넉넉하게 적어 두는 것으로 검사를 무력화할 수 있다.
        private static readonly PropSpec[] _specs =
        {
            new PropSpec(PropId.FireExtinguisher, "소화기",        PropMount.Wall,    new Vector3(0.16f, 0.45f, 0.19f), 200),
            new PropSpec(PropId.ToolBox,          "공구함",        PropMount.Floor,   new Vector3(0.44f, 0.28f, 0.24f), 160),
            new PropSpec(PropId.Bucket,           "양동이",        PropMount.Floor,   new Vector3(0.29f, 0.40f, 0.29f), 140),
            new PropSpec(PropId.Broom,            "빗자루",        PropMount.Floor,   new Vector3(0.28f, 1.30f, 0.10f), 140),
            new PropSpec(PropId.PipeBundle,       "배관 다발",     PropMount.Wall,    new Vector3(0.29f, 1.46f, 0.15f), 180),
            new PropSpec(PropId.ElectricalBox,    "전기 배전함",   PropMount.Wall,    new Vector3(0.34f, 0.61f, 0.17f), 200),
            new PropSpec(PropId.WarningSign,      "경고 표지판",   PropMount.Wall,    new Vector3(0.35f, 0.25f, 0.07f), 160),
            new PropSpec(PropId.EmergencyLamp,    "비상등",        PropMount.Wall,    new Vector3(0.15f, 0.17f, 0.17f), 200),
            new PropSpec(PropId.VentGrille,       "환풍구",        PropMount.Wall,    new Vector3(0.39f, 0.27f, 0.07f), 160),
            new PropSpec(PropId.FloorHatch,       "바닥 해치",     PropMount.Floor,   new Vector3(0.71f, 0.09f, 0.71f), 220),
            new PropSpec(PropId.DrainGrate,       "배수구 그레이트", PropMount.Floor, new Vector3(0.33f, 0.04f, 0.33f), 180),
            new PropSpec(PropId.GrabRail,         "손잡이 봉",     PropMount.Wall,    new Vector3(0.81f, 0.07f, 0.12f), 160),
            new PropSpec(PropId.HangingChain,     "매달린 사슬",   PropMount.Ceiling, new Vector3(0.08f, 0.85f, 0.08f), 160),
            new PropSpec(PropId.CargoPallet,      "화물 팔레트",   PropMount.Floor,   new Vector3(1.10f, 0.13f, 0.91f), 180),
            new PropSpec(PropId.WoodenCrate,      "나무 상자",     PropMount.Floor,   new Vector3(0.59f, 0.53f, 0.59f), 220),
            new PropSpec(PropId.Sack,             "자루",          PropMount.Floor,   new Vector3(0.43f, 0.39f, 0.42f), 140),
            new PropSpec(PropId.GaugeHousing,     "계기 하우징",   PropMount.Wall,    new Vector3(0.21f, 0.26f, 0.13f), 180),
            new PropSpec(PropId.CableReel,        "케이블 릴",     PropMount.Floor,   new Vector3(0.34f, 0.42f, 0.42f), 200),
            new PropSpec(PropId.BoltBin,          "볼트 다발",     PropMount.Floor,   new Vector3(0.32f, 0.16f, 0.24f), 140),
            new PropSpec(PropId.Bench,            "벤치",          PropMount.Floor,   new Vector3(1.21f, 0.46f, 0.34f), 170),
            new PropSpec(PropId.DustyShelf,       "먼지 쌓인 선반", PropMount.Wall,   new Vector3(0.89f, 1.41f, 0.32f), 200),
            new PropSpec(PropId.PosterBoard,      "낡은 포스터판", PropMount.Wall,    new Vector3(0.45f, 0.58f, 0.04f), 160),
            new PropSpec(PropId.CraneRail,        "천장 크레인 레일", PropMount.Ceiling, new Vector3(2.17f, 0.29f, 0.16f), 180),
            new PropSpec(PropId.CargoHook,        "화물 고리",     PropMount.Wall,    new Vector3(0.07f, 0.18f, 0.17f), 160),
        };

        public static IReadOnlyList<PropSpec> Specs => _specs;

        public static PropSpec GetSpec(PropId id)
        {
            int i = (int)id;
            if (i < 0 || i >= _specs.Length)
                throw new ArgumentOutOfRangeException(nameof(id), $"알 수 없는 프롭 {id}");
            return _specs[i];
        }

        /// <summary>프롭 하나를 <see cref="Mesh"/> 로. 초기화 시점에만 부른다.</summary>
        public static Mesh Build(PropId id, float uvPerMeter = 1f)
        {
            var b = new ProcMeshBuilder(512);
            Build(b, id, uvPerMeter);
            return b.ToMesh("PROP_" + id);
        }

        /// <summary>
        /// 프롭 하나를 빌더에 덧붙인다. 여러 프롭을 한 메시로 묶고 싶을 때
        /// (예: 선반 위 소품 뭉치) 이쪽을 쓴다 — 드로우콜이 하나로 끝난다.
        /// </summary>
        public static void Build(ProcMeshBuilder b, PropId id, float uvPerMeter = 1f)
        {
            if (b == null) throw new ArgumentNullException(nameof(b));
            switch (id)
            {
                case PropId.FireExtinguisher: FireExtinguisher(b, uvPerMeter); break;
                case PropId.ToolBox: ToolBox(b, uvPerMeter); break;
                case PropId.Bucket: Bucket(b, uvPerMeter); break;
                case PropId.Broom: Broom(b, uvPerMeter); break;
                case PropId.PipeBundle: PipeBundle(b, uvPerMeter); break;
                case PropId.ElectricalBox: ElectricalBox(b, uvPerMeter); break;
                case PropId.WarningSign: WarningSign(b, uvPerMeter); break;
                case PropId.EmergencyLamp: EmergencyLamp(b, uvPerMeter); break;
                case PropId.VentGrille: VentGrille(b, uvPerMeter); break;
                case PropId.FloorHatch: FloorHatch(b, uvPerMeter); break;
                case PropId.DrainGrate: DrainGrate(b, uvPerMeter); break;
                case PropId.GrabRail: GrabRail(b, uvPerMeter); break;
                case PropId.HangingChain: HangingChain(b, uvPerMeter); break;
                case PropId.CargoPallet: CargoPallet(b, uvPerMeter); break;
                case PropId.WoodenCrate: WoodenCrate(b, uvPerMeter); break;
                case PropId.Sack: Sack(b, uvPerMeter); break;
                case PropId.GaugeHousing: GaugeHousing(b, uvPerMeter); break;
                case PropId.CableReel: CableReel(b, uvPerMeter); break;
                case PropId.BoltBin: BoltBin(b, uvPerMeter); break;
                case PropId.Bench: Bench(b, uvPerMeter); break;
                case PropId.DustyShelf: DustyShelf(b, uvPerMeter); break;
                case PropId.PosterBoard: PosterBoard(b, uvPerMeter); break;
                case PropId.CraneRail: CraneRail(b, uvPerMeter); break;
                case PropId.CargoHook: CargoHook(b, uvPerMeter); break;
                default: throw new ArgumentOutOfRangeException(nameof(id), $"알 수 없는 프롭 {id}");
            }
        }

        // ══ 벽 프롭 ═══════════════════════════════════════════════════════════

        /// <summary>소화기 — 벽 브래킷에 걸린 몸통 + 밸브 + 호스.</summary>
        private static void FireExtinguisher(ProcMeshBuilder b, float uv)
        {
            const float bodyZ = 0.115f;
            // 벽 브래킷 2단. 이것이 없으면 소화기가 벽에 박혀 있는 것처럼 보인다.
            b.AddBox(new Vector3(0f, 0.11f, 0.026f), new Vector3(0.10f, 0.03f, 0.052f), 0f, uv);
            b.AddBox(new Vector3(0f, -0.12f, 0.026f), new Vector3(0.10f, 0.03f, 0.052f), 0f, uv);

            b.AddPrism(new Vector3(0f, -0.02f, bodyZ), 0.072f, 0.072f, 0.30f, 6, MeshAxis.Y, 0f, true, true, false, uv);
            b.AddPrism(new Vector3(0f, 0.155f, bodyZ), 0.072f, 0.034f, 0.05f, 6, MeshAxis.Y, 0f, true, true, false, uv);
            b.AddPrism(new Vector3(0f, 0.202f, bodyZ), 0.026f, 0.026f, 0.045f, 6, MeshAxis.Y, 0f, true, true, false, uv);
            b.AddBox(new Vector3(0f, 0.232f, bodyZ), new Vector3(0.06f, 0.026f, 0.05f), 0.008f, uv);
            // 손잡이 — 살짝 기울어 있다. 수평이면 「부품」이고 기울면 「눌러 본 적 있는 것」이다.
            b.AddBox(new Vector3(0f, 0.252f, bodyZ + 0.008f), new Vector3(0.05f, 0.012f, 0.075f),
                     ProcQuat.Euler(-9f, 0f, 0f), 0f, uv);
            // 호스 — 밸브에서 몸통 아래로 늘어진다.
            ProcMesh.Cable(b, new Vector3(0.026f, 0.236f, bodyZ + 0.03f),
                              new Vector3(0.055f, 0.02f, bodyZ + 0.055f),
                              0.045f, 4, 0.009f, 4, false, uv);
        }

        /// <summary>배관 다발 — 파이프 3줄 + 스트랩 2개 + 플랜지. 벽을 세로로 지난다.</summary>
        private static void PipeBundle(ProcMeshBuilder b, float uv)
        {
            float[] xs = { -0.085f, 0f, 0.085f };
            for (int i = 0; i < 3; i++)
            {
                float r = 0.034f + ProcMeshBuilder.Hash01(i * 13) * 0.008f;
                b.AddPrism(new Vector3(xs[i], 0f, 0.075f), r, r, 1.44f, 6, MeshAxis.Y,
                           i * 12f, true, true, false, uv);
                // 플랜지 하나씩 — 높이를 어긋나게 둔다. 나란히 있으면 「장식 띠」로 읽힌다.
                float fy = -0.30f + i * 0.26f;
                b.AddPrism(new Vector3(xs[i], fy, 0.075f), r * 1.5f, r * 1.5f, 0.035f, 6, MeshAxis.Y,
                           i * 12f, true, true, false, uv);
            }
            // 스트랩 + **벽 접촉판**. 접촉판이 없으면 파이프가 벽에서 3.3cm 떠 있고,
            // 그건 「벽에 붙은 배관」이 아니라 「공중에 뜬 관 세 개」다.
            // 피벗 규약(벽 프롭은 z = 0 이 접촉면)도 이것이 있어야 성립한다.
            for (int i = 0; i < 2; i++)
            {
                float y = i == 0 ? 0.55f : -0.58f;
                b.AddBox(new Vector3(0f, y, 0.075f), new Vector3(0.25f, 0.035f, 0.10f), 0f, uv);
                b.AddBox(new Vector3(0f, y, 0.017f), new Vector3(0.09f, 0.05f, 0.034f), 0f, uv);
            }
        }

        /// <summary>전기 배전함 — 문·경첩·걸쇠·아래로 빠지는 전선관.</summary>
        private static void ElectricalBox(ProcMeshBuilder b, float uv)
        {
            b.AddBox(new Vector3(0f, 0.06f, 0.065f), new Vector3(0.32f, 0.40f, 0.13f), 0.014f, uv);
            b.AddBox(new Vector3(0.012f, 0.06f, 0.138f), new Vector3(0.29f, 0.37f, 0.018f), 0f, uv);
            b.AddBox(new Vector3(-0.155f, 0.17f, 0.132f), new Vector3(0.03f, 0.05f, 0.026f), 0f, uv);
            b.AddBox(new Vector3(-0.155f, -0.05f, 0.132f), new Vector3(0.03f, 0.05f, 0.026f), 0f, uv);
            b.AddBox(new Vector3(0.152f, 0.06f, 0.146f), new Vector3(0.022f, 0.055f, 0.02f), 0f, uv);
            // 전선관 — 함이 어디로 연결되는지를 형상으로 말한다 (금지 6 의 「기능적 연결」).
            b.AddPrism(new Vector3(0.06f, -0.24f, 0.065f), 0.022f, 0.022f, 0.20f, 6, MeshAxis.Y, 0f, true, true, false, uv);
            b.AddPrism(new Vector3(0.06f, -0.16f, 0.065f), 0.032f, 0.032f, 0.03f, 6, MeshAxis.Y, 0f, true, true, false, uv);
        }

        /// <summary>경고 표지판 — 판 + 볼트 4개 + 벽에서 띄우는 스탠드오프.</summary>
        private static void WarningSign(ProcMeshBuilder b, float uv)
        {
            // 스탠드오프가 벽면(z = 0)에서 판까지 **닿아 있어야** 한다. 중간에 틈이 있으면
            // 판이 허공에 떠 보이고, 어두운 캐빈에서 그 틈은 검은 선으로 도드라진다.
            b.AddBox(new Vector3(0f, 0.02f, 0.017f), new Vector3(0.24f, 0.05f, 0.034f), 0f, uv);
            b.AddBox(new Vector3(0f, 0f, 0.042f), new Vector3(0.34f, 0.24f, 0.016f), 0.005f, uv);
            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0) ? -1f : 1f;
                float sy = (i < 2) ? -1f : 1f;
                ProcMesh.Rivet(b, new Vector3(sx * 0.145f, sy * 0.095f, 0.050f), 0.010f, 0.007f, MeshAxis.Z, 4, uv);
            }
        }

        /// <summary>비상등 — 하우징 + 돔 + 보호 케이지. 케이지가 있어야 「산업용」으로 읽힌다.</summary>
        private static void EmergencyLamp(ProcMeshBuilder b, float uv)
        {
            b.AddBox(new Vector3(0f, 0f, 0.012f), new Vector3(0.14f, 0.16f, 0.024f), 0f, uv);
            b.AddBox(new Vector3(0f, 0f, 0.048f), new Vector3(0.11f, 0.12f, 0.05f), 0.012f, uv);
            // 돔 — 원뿔대 두 단. 반구보다 면이 보이고, 그것이 스타일 락이다.
            b.AddPrism(new Vector3(0f, 0f, 0.098f), 0.058f, 0.046f, 0.05f, 6, MeshAxis.Z, 0f, true, true, false, uv);
            b.AddPrism(new Vector3(0f, 0f, 0.141f), 0.046f, 0.020f, 0.036f, 6, MeshAxis.Z, 0f, true, true, false, uv);
            // 케이지 4줄. 실제로 램프를 가로지르므로 어두울 때 실루엣이 생긴다.
            for (int i = 0; i < 4; i++)
            {
                float a = 45f + i * 45f;
                b.AddBox(new Vector3(0f, 0f, 0.11f), new Vector3(0.008f, 0.135f, 0.008f),
                         ProcQuat.Euler(0f, 0f, a), 0f, uv);
            }
        }

        /// <summary>환풍구.</summary>
        private static void VentGrille(ProcMeshBuilder b, float uv)
            => ProcMesh.Vent(b, 0.38f, 0.26f, 5, 0.06f, uv);

        /// <summary>손잡이 봉 — 벽에서 띄운 가로 봉. 승객이 잡는 것이라 눈에 띄어야 한다.</summary>
        private static void GrabRail(ProcMeshBuilder b, float uv)
        {
            b.AddPrism(new Vector3(0f, 0f, 0.088f), 0.021f, 0.021f, 0.80f, 6, MeshAxis.X, 0f, true, true, false, uv);
            for (int i = 0; i < 2; i++)
            {
                float x = (i == 0 ? -1f : 1f) * 0.36f;
                b.AddBox(new Vector3(x, 0f, 0.010f), new Vector3(0.055f, 0.055f, 0.020f), 0.006f, uv);
                b.AddPrism(new Vector3(x, 0f, 0.052f), 0.017f, 0.017f, 0.064f, 6, MeshAxis.Z, 0f, true, true, false, uv);
            }
        }

        /// <summary>계기 하우징 — 몸통 + 베젤 + 유리면 + 전선관.</summary>
        private static void GaugeHousing(ProcMeshBuilder b, float uv)
        {
            b.AddBox(new Vector3(0f, 0f, 0.045f), new Vector3(0.20f, 0.20f, 0.09f), 0.014f, uv);
            b.AddPrism(new Vector3(0f, 0f, 0.104f), 0.083f, 0.083f, 0.028f, 8, MeshAxis.Z, 22.5f, true, true, false, uv);
            // 유리면은 베젤보다 **안으로** 들어간다. 튀어나오면 「덮개 없는 계기」다.
            b.AddPrism(new Vector3(0f, 0f, 0.112f), 0.066f, 0.066f, 0.006f, 8, MeshAxis.Z, 22.5f, true, true, false, uv);
            b.AddPrism(new Vector3(0f, -0.115f, 0.045f), 0.017f, 0.017f, 0.07f, 6, MeshAxis.Y, 0f, true, true, false, uv);
        }

        /// <summary>먼지 쌓인 선반 — 기둥 2 + 선반 3 + 브래킷 4 + 뒤 보강재.</summary>
        private static void DustyShelf(ProcMeshBuilder b, float uv)
        {
            for (int i = 0; i < 2; i++)
            {
                float x = (i == 0 ? -1f : 1f) * 0.42f;
                b.AddBox(new Vector3(x, 0f, 0.035f), new Vector3(0.04f, 1.40f, 0.06f), 0f, uv);
            }
            float[] ys = { -0.52f, 0.02f, 0.54f };
            for (int i = 0; i < 3; i++)
            {
                // 선반 깊이를 조금씩 다르게 — 같으면 「새 가구」로 읽힌다 (금지 9).
                float depth = 0.28f + ProcMeshBuilder.Hash01(i * 37) * 0.03f;
                b.AddBox(new Vector3(0f, ys[i], depth * 0.5f + 0.01f),
                         new Vector3(0.86f, 0.022f, depth), 0f, uv);
                if (i == 2) continue;
                for (int s = 0; s < 2; s++)
                {
                    float x = (s == 0 ? -1f : 1f) * 0.34f;
                    b.AddBox(new Vector3(x, ys[i] - 0.055f, 0.10f),
                             new Vector3(0.016f, 0.115f, 0.115f), ProcQuat.Euler(-45f, 0f, 0f), 0f, uv);
                }
            }
            b.AddBox(new Vector3(0f, -0.28f, 0.012f), new Vector3(0.84f, 0.03f, 0.014f), 0f, uv);
        }

        /// <summary>낡은 포스터판 — 판 + 테두리 4 + 압정 4.</summary>
        private static void PosterBoard(ProcMeshBuilder b, float uv)
        {
            b.AddBox(new Vector3(0f, 0f, 0.009f), new Vector3(0.40f, 0.54f, 0.018f), 0f, uv);
            b.AddBox(new Vector3(0f, 0.272f, 0.016f), new Vector3(0.44f, 0.024f, 0.020f), 0f, uv);
            b.AddBox(new Vector3(0f, -0.272f, 0.016f), new Vector3(0.44f, 0.024f, 0.020f), 0f, uv);
            b.AddBox(new Vector3(-0.208f, 0f, 0.016f), new Vector3(0.024f, 0.568f, 0.020f), 0f, uv);
            b.AddBox(new Vector3(0.208f, 0f, 0.016f), new Vector3(0.024f, 0.568f, 0.020f), 0f, uv);
            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0) ? -1f : 1f;
                float sy = (i < 2) ? -1f : 1f;
                // 압정 위치를 미세하게 흐트러뜨린다 — 사람이 붙인 것으로 읽힌다.
                float jx = ProcMeshBuilder.HashSigned(i * 53) * 0.012f;
                ProcMesh.Rivet(b, new Vector3(sx * 0.165f + jx, sy * 0.225f, 0.018f), 0.008f, 0.008f, MeshAxis.Z, 4, uv);
            }
        }

        /// <summary>화물 고리 — 결박용. 레퍼런스 `5f22ccc3` 의 벽면 링 고리에 대응한다.</summary>
        private static void CargoHook(ProcMeshBuilder b, float uv)
            => ProcMesh.Hook(b, 0.16f, 0.013f, uv);

        // ══ 바닥 프롭 ═════════════════════════════════════════════════════════

        /// <summary>공구함 — 몸통 + 뚜껑 + 손잡이 + 걸쇠 2개.</summary>
        private static void ToolBox(ProcMeshBuilder b, float uv)
        {
            b.AddBox(new Vector3(0f, 0.095f, 0f), new Vector3(0.42f, 0.19f, 0.22f), 0.014f, uv);
            b.AddBox(new Vector3(0f, 0.198f, 0f), new Vector3(0.435f, 0.022f, 0.235f), 0f, uv);
            for (int i = 0; i < 2; i++)
            {
                float x = (i == 0 ? -1f : 1f) * 0.075f;
                b.AddBox(new Vector3(x, 0.238f, 0f), new Vector3(0.016f, 0.06f, 0.016f), 0f, uv);
            }
            b.AddBox(new Vector3(0f, 0.264f, 0f), new Vector3(0.185f, 0.016f, 0.022f), 0.005f, uv);
            for (int i = 0; i < 2; i++)
            {
                float x = (i == 0 ? -1f : 1f) * 0.14f;
                b.AddBox(new Vector3(x, 0.185f, 0.114f), new Vector3(0.03f, 0.05f, 0.012f), 0f, uv);
            }
        }

        /// <summary>양동이 — 위가 넓은 원뿔대 + 테 + 손잡이. 손잡이가 한쪽으로 넘어가 있다.</summary>
        private static void Bucket(ProcMeshBuilder b, float uv)
        {
            b.AddPrism(new Vector3(0f, 0.14f, 0f), 0.105f, 0.136f, 0.28f, 8, MeshAxis.Y, 0f, true, true, false, uv);
            b.AddPrism(new Vector3(0f, 0.288f, 0f), 0.142f, 0.138f, 0.018f, 8, MeshAxis.Y, 0f, true, true, false, uv);
            // 손잡이 — 수직으로 세우지 않고 한쪽으로 눕힌다. 세우면 대칭이라 「진열품」이다.
            var arc = new List<Vector3>(5);
            for (int i = 0; i < 5; i++)
            {
                float t = i / 4f;
                float a = Mathf.Lerp(178f, 34f, t) * Mathf.Deg2Rad;
                arc.Add(new Vector3(Mathf.Cos(a) * 0.134f, 0.286f + Mathf.Sin(a) * 0.104f, 0f));
            }
            b.AddTube(arc, 0.008f, 4, false, true, false, uv);
        }

        /// <summary>빗자루 — 벽에 기대 세워 둔다. 자루 굵기와 솔 뭉치가 다르다.</summary>
        private static void Broom(ProcMeshBuilder b, float uv)
        {
            int mark = b.Mark();
            b.AddPrism(new Vector3(0f, 0.72f, 0f), 0.016f, 0.014f, 1.10f, 6, MeshAxis.Y, 0f, true, true, false, uv);
            b.AddPrism(new Vector3(0f, 0.20f, 0f), 0.022f, 0.022f, 0.05f, 6, MeshAxis.Y, 0f, true, true, false, uv);
            b.AddBox(new Vector3(0f, 0.145f, 0f), new Vector3(0.26f, 0.055f, 0.090f), 0.010f, uv);
            // 솔 뭉치 셋의 길이가 다르다 — 같으면 「새 빗자루」이고, 이 캐빈에 새 것은 없다.
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 0.082f;
                float h = 0.10f + ProcMeshBuilder.Hash01(i * 91) * 0.028f;
                b.AddBox(new Vector3(x, h * 0.5f + 0.004f, 0f), new Vector3(0.076f, h, 0.078f), 0f, uv);
            }
            // 통째로 기울인다 — 벽에 기대 세운 상태. 기울이면 솔 뭉치의 바깥 모서리가
            // 원점보다 아래로 내려가므로(x 가 클수록 크게), 그만큼 되올려 바닥 접점을
            // y = 0 에 맞춘다. 이 보정이 없으면 빗자루만 바닥에 8mm 박힌다.
            b.TransformSince(mark, new Vector3(0f, 0.012f, 0f), ProcQuat.Euler(0f, 0f, 6f));
        }

        /// <summary>바닥 해치 — 판 + 테두리 + 리벳 4 + 링 손잡이. 레퍼런스 `5f22ccc3` 대응.</summary>
        private static void FloorHatch(ProcMeshBuilder b, float uv)
        {
            b.AddBox(new Vector3(0f, 0.014f, 0f), new Vector3(0.70f, 0.028f, 0.70f), 0.008f, uv);
            b.AddBox(new Vector3(0f, 0.034f, 0.325f), new Vector3(0.70f, 0.012f, 0.05f), 0f, uv);
            b.AddBox(new Vector3(0f, 0.034f, -0.325f), new Vector3(0.70f, 0.012f, 0.05f), 0f, uv);
            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0) ? -1f : 1f;
                float sz = (i < 2) ? -1f : 1f;
                ProcMesh.Rivet(b, new Vector3(sx * 0.29f, 0.028f, sz * 0.29f), 0.016f, 0.012f, MeshAxis.Y, 4, uv);
            }
            // 링 손잡이 — 닫힌 고리. 눕혀 두면 밟히고, 세우면 걸린다. 살짝 세운다.
            var ring = new List<Vector3>(6);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                ring.Add(new Vector3(Mathf.Cos(a) * 0.075f, 0.046f + Mathf.Sin(a) * 0.030f, Mathf.Sin(a) * 0.052f));
            }
            b.AddTube(ring, 0.010f, 4, true, false, false, uv);
        }

        /// <summary>배수구 그레이트 — 진짜 구멍이 뚫린 격자.</summary>
        private static void DrainGrate(ProcMeshBuilder b, float uv)
            => ProcMesh.Grating(b, 0.32f, 0.32f, 5, 0.018f, 0.030f, uv);

        /// <summary>화물 팔레트 — 아래 받침 3 + 세로대 3 + 상판 5.</summary>
        private static void CargoPallet(ProcMeshBuilder b, float uv)
        {
            for (int i = 0; i < 3; i++)
            {
                float z = (i - 1) * 0.38f;
                b.AddBox(new Vector3(0f, 0.018f, z), new Vector3(1.08f, 0.036f, 0.10f), 0f, uv);
                b.AddBox(new Vector3(0f, 0.060f, z), new Vector3(1.08f, 0.048f, 0.09f), 0f, uv);
            }
            // 상판이 아래 받침보다 밖으로 나가면 팔레트가 아니라 「판때기 얹은 것」이다.
            // 간격 0.19 는 바깥 판이 받침(±0.38 + 0.05)과 거의 같은 깊이에서 끝나게 한다.
            for (int i = 0; i < 5; i++)
            {
                float z = (i - 2) * 0.19f;
                float w = 1.10f - ProcMeshBuilder.Hash01(i * 61) * 0.02f;
                b.AddBox(new Vector3(0f, 0.100f, z), new Vector3(w, 0.026f, 0.135f), 0f, uv);
            }
        }

        /// <summary>나무 상자.</summary>
        private static void WoodenCrate(ProcMeshBuilder b, float uv)
            => ProcMesh.Crate(b, new Vector3(0.58f, 0.52f, 0.58f), 3, uv);

        /// <summary>자루 — 아래가 퍼지고 위가 묶인 형태. 상단이 한쪽으로 기울어 있다.</summary>
        private static void Sack(ProcMeshBuilder b, float uv)
        {
            b.AddPrism(new Vector3(0f, 0.10f, 0f), 0.170f, 0.205f, 0.20f, 6, MeshAxis.Y, 0f, true, true, false, uv);
            int mark = b.Mark();
            b.AddPrism(new Vector3(0f, 0.27f, 0f), 0.205f, 0.105f, 0.15f, 6, MeshAxis.Y, 30f, true, true, false, uv);
            b.AddPrism(new Vector3(0f, 0.365f, 0f), 0.048f, 0.062f, 0.05f, 6, MeshAxis.Y, 30f, true, true, false, uv);
            b.AddBox(new Vector3(0f, 0.352f, 0f), new Vector3(0.075f, 0.014f, 0.075f), 0f, uv);
            // 윗부분만 기울인다. 바닥은 그대로 바닥에 붙어 있고 위가 쏠린 모양 —
            // 「채워져서 무거운 것」의 실루엣이고, 완전 대칭(금지 9)을 깬다.
            b.TransformSince(mark, new Vector3(0.014f, -0.012f, 0.008f), ProcQuat.Euler(6f, 0f, -8f));
        }

        /// <summary>케이블 릴 — 옆으로 눕힌 드럼. 감긴 것이 케이블이라는 것이 형상으로 보인다.</summary>
        private static void CableReel(ProcMeshBuilder b, float uv)
        {
            // 8각 플랜지의 최저 정점이 정확히 바닥에 닿는 높이. 반지름이 아니라
            // `r·cos(22.5°)` 인 이유는 각기둥이라 꼭짓점 사이가 평평하기 때문이다 —
            // 반지름으로 잡으면 릴이 2.2cm 떠 있고, 그 틈이 그림자에서 곧바로 보인다.
            const float cy = 0.20325f;
            b.AddPrism(new Vector3(-0.135f, cy, 0f), 0.220f, 0.220f, 0.026f, 8, MeshAxis.X, 22.5f, true, true, false, uv);
            b.AddPrism(new Vector3(0.135f, cy, 0f), 0.220f, 0.220f, 0.026f, 8, MeshAxis.X, 22.5f, true, true, false, uv);
            b.AddPrism(new Vector3(0f, cy, 0f), 0.095f, 0.095f, 0.25f, 8, MeshAxis.X, 22.5f, true, true, false, uv);
            // 감긴 케이블 — 드럼보다 굵고 플랜지보다 얇다. 이 세 굵기의 차이가 「감겨 있다」다.
            b.AddPrism(new Vector3(0f, cy, 0f), 0.162f, 0.162f, 0.21f, 8, MeshAxis.X, 22.5f, true, true, false, uv);
            b.AddPrism(new Vector3(0f, cy, 0f), 0.026f, 0.026f, 0.33f, 6, MeshAxis.X, 0f, true, true, false, uv);
        }

        /// <summary>볼트 다발 — 뚜껑 없는 통 + 안에 담긴 볼트. 내용물이 보여야 「자재」다.</summary>
        private static void BoltBin(ProcMeshBuilder b, float uv)
        {
            b.AddBox(new Vector3(0f, 0.014f, 0f), new Vector3(0.30f, 0.028f, 0.22f), 0f, uv);
            b.AddBox(new Vector3(0f, 0.085f, 0.105f), new Vector3(0.30f, 0.115f, 0.020f), 0f, uv);
            b.AddBox(new Vector3(0f, 0.085f, -0.105f), new Vector3(0.30f, 0.115f, 0.020f), 0f, uv);
            b.AddBox(new Vector3(0.145f, 0.085f, 0f), new Vector3(0.020f, 0.115f, 0.19f), 0f, uv);
            b.AddBox(new Vector3(-0.145f, 0.085f, 0f), new Vector3(0.020f, 0.115f, 0.19f), 0f, uv);
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 0.075f + ProcMeshBuilder.HashSigned(i * 23) * 0.02f;
                float z = ProcMeshBuilder.HashSigned(i * 47) * 0.05f;
                b.AddBox(new Vector3(x, 0.052f, z), new Vector3(0.055f, 0.042f, 0.055f),
                         ProcQuat.Euler(0f, ProcMeshBuilder.Hash01(i * 7) * 60f, 0f), 0f, uv);
            }
        }

        /// <summary>벤치 — 앉는 판 2 + 다리 4 + 가로 보강 2.</summary>
        private static void Bench(ProcMeshBuilder b, float uv)
        {
            for (int i = 0; i < 2; i++)
            {
                float z = (i == 0 ? -1f : 1f) * 0.085f;
                b.AddBox(new Vector3(0f, 0.435f, z), new Vector3(1.20f, 0.032f, 0.155f), 0.006f, uv);
            }
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? -0.52f : 0.52f;
                float z = (i < 2) ? -0.115f : 0.115f;
                b.AddBox(new Vector3(x, 0.209f, z), new Vector3(0.045f, 0.418f, 0.045f), 0f, uv);
            }
            for (int i = 0; i < 2; i++)
            {
                float x = (i == 0 ? -1f : 1f) * 0.52f;
                b.AddBox(new Vector3(x, 0.115f, 0f), new Vector3(0.030f, 0.026f, 0.245f), 0f, uv);
            }
        }

        // ══ 천장 프롭 ═════════════════════════════════════════════════════════

        /// <summary>매달린 사슬 — 링크가 90°씩 번갈아 돈다. 물리 레인이 흔들 대상이다.</summary>
        private static void HangingChain(ProcMeshBuilder b, float uv)
        {
            b.AddBox(new Vector3(0f, -0.014f, 0f), new Vector3(0.07f, 0.028f, 0.07f), 0f, uv);
            const int links = 8;
            for (int i = 0; i < links; i++)
            {
                float y = -0.055f - i * 0.104f;
                // 링크가 번갈아 도는 것이 사슬을 사슬로 만든다. 같은 방향이면 「막대 여러 개」다.
                float yaw = (i % 2 == 0) ? 0f : 90f;
                // 아래로 갈수록 아주 조금씩 흔들린 각도 — 완전한 수직은 정지 화면에서도 죽어 보인다.
                float tilt = ProcMeshBuilder.HashSigned(i * 19) * 3.5f;
                b.AddBox(new Vector3(0f, y, 0f), new Vector3(0.030f, 0.105f, 0.014f),
                         ProcQuat.Euler(tilt, yaw, tilt * 0.6f), 0f, uv);
            }
        }

        /// <summary>천장 크레인 레일 — I 빔 + 행어 2 + 트롤리. 화물 엘리베이터의 기능 언어.</summary>
        private static void CraneRail(ProcMeshBuilder b, float uv)
        {
            for (int i = 0; i < 2; i++)
            {
                float x = (i == 0 ? -1f : 1f) * 0.78f;
                b.AddBox(new Vector3(x, -0.014f, 0f), new Vector3(0.11f, 0.028f, 0.11f), 0f, uv);
                b.AddPrism(new Vector3(x, -0.075f, 0f), 0.019f, 0.019f, 0.095f, 6, MeshAxis.Y, 0f, true, true, false, uv);
            }
            // I 빔 — 위 플랜지·웹·아래 플랜지. 세 조각의 폭이 다른 것이 I 빔의 전부다.
            b.AddBox(new Vector3(0f, -0.136f, 0f), new Vector3(2.16f, 0.026f, 0.115f), 0f, uv);
            b.AddBox(new Vector3(0f, -0.184f, 0f), new Vector3(2.16f, 0.070f, 0.026f), 0f, uv);
            b.AddBox(new Vector3(0f, -0.232f, 0f), new Vector3(2.16f, 0.026f, 0.145f), 0f, uv);
            // 트롤리 — 레일 위 어딘가에 멈춰 있다. 정중앙이 아니어야 「쓰이는 장비」다.
            b.AddBox(new Vector3(0.42f, -0.256f, 0f), new Vector3(0.13f, 0.055f, 0.10f), 0.012f, uv);
        }
    }
}
