using UnityEngine;

namespace Ascend.Prototype.Art
{
    /// <summary>
    /// 현창 캐비닛 **주변**에 붙는 조작·표시 부품. `docs/VISUAL_REFERENCE.md` §1 의
    /// 「장치 우측」과 「좌벽 상단」에 있는데 지금 씬에는 형상이 없는 것들이다.
    ///
    /// ## 어휘 규칙
    ///
    /// - 전부 **벽 프롭**이다 — 원점이 벽 접촉면(z = 0), X·Y 중심, 밖이 +Z.
    ///   예외는 <see cref="HazardPlateStripes"/> 하나이고, 그건 <see cref="HazardPlateBase"/>
    ///   **위에** 얹히므로 z 가 판 앞면에서 시작한다(같은 로컬 프레임을 공유한다).
    /// - 프롭 하나 ≤ 300 삼각형. `PropLibrary` 와 같은 상한이다.
    /// - flat normal · 월드 미터 UV · 결정론. `ProcMesh` 규약 그대로다.
    /// - **`localScale` 을 쓰지 마라.** 크기를 바꾸려면 인자를 준다.
    ///
    /// ## 왜 「하우징」과 「움직이는 조각」을 나눴는가
    ///
    /// 레버 손잡이·층수 화살표는 씬 소유자가 **Transform 을 애니메이션**할 대상이다.
    /// 하우징과 한 메시로 굽는 순간 그게 불가능해지고, 다시 만들려 하면
    /// <see cref="MeshBuildPhase"/> 가 막는다(`UP-TECH-04` 의 0 B/frame 이 그 위에 선다).
    /// 그래서 처음부터 갈라서 낸다.
    /// </summary>
    public static class ConsolePropMesh
    {
        /// <summary>프롭 하나의 삼각형 상한. `PropLibrary.PerPropTriangleLimit` 과 같다.</summary>
        public const int PerPropTriangleLimit = 300;

        // ══ 붉은 버섯 버튼 ════════════════════════════════════════════════════

        public static Mesh MushroomButtonMesh(float capRadius = 0.052f, float uvPerMeter = 1f)
        {
            var b = new ProcMeshBuilder(256);
            MushroomButton(b, capRadius, uvPerMeter);
            return b.ToMesh("PM_MushroomButton");
        }

        /// <summary>
        /// 비상 정지형 **버섯 버튼**. 금속 베이스 → 칼라 → 역테이퍼 몸통 → 돔.
        ///
        /// 몸통이 위로 갈수록 **넓어지는** 것이 「버섯」의 전부다. 원기둥에 뚜껑을 얹으면
        /// 그냥 손잡이이고, 「손바닥으로 내려칠 수 있는 것」으로 읽히지 않는다.
        /// </summary>
        public static void MushroomButton(ProcMeshBuilder b, float capRadius = 0.052f, float uvPerMeter = 1f)
        {
            float r = Mathf.Clamp(capRadius, 0.015f, 0.12f);

            // 벽판 — 버튼이 벽에 박힌 것이 아니라 **판에 달려 있다**는 것을 말한다.
            b.AddBox(new Vector3(0f, 0f, 0.011f), new Vector3(r * 2.9f, r * 2.9f, 0.022f),
                     0.005f, uvPerMeter);
            // 칼라 — 눌리는 부분과 고정된 부분의 경계.
            b.AddPrism(new Vector3(0f, 0f, 0.022f + 0.014f), r * 1.16f, r * 1.06f, 0.028f,
                       8, MeshAxis.Z, 22.5f, true, true, false, uvPerMeter);
            // 몸통 — 위가 더 넓다.
            b.AddPrism(new Vector3(0f, 0f, 0.050f + 0.016f), r * 0.90f, r * 1.00f, 0.032f,
                       8, MeshAxis.Z, 22.5f, true, true, false, uvPerMeter);
            // 돔 — 반구가 아니라 원뿔대 한 단. 면이 보여야 스타일 락에 맞는다.
            b.AddPrism(new Vector3(0f, 0f, 0.082f + 0.010f), r * 1.00f, r * 0.62f, 0.020f,
                       8, MeshAxis.Z, 22.5f, true, true, false, uvPerMeter);
        }

        // ══ 세로 슬롯 레버 하우징 ═════════════════════════════════════════════

        public static Mesh LeverSlotHousingMesh(float height = 0.62f, float width = 0.20f,
                                                float depth = 0.10f, int detents = 5,
                                                float uvPerMeter = 1f)
        {
            var b = new ProcMeshBuilder(384);
            LeverSlotHousing(b, height, width, depth, detents, uvPerMeter);
            return b.ToMesh("PM_LeverSlotHousing");
        }

        /// <summary>
        /// **과수확 레버의 세로 슬롯 하우징.** 손잡이가 이 안에서 위아래로 움직인다.
        ///
        /// ## 왜 이것이 별도 형상이어야 하는가 — `UP-FIX-03`
        ///
        /// 실행 레버(<see cref="ProcMesh.Lever"/>)는 벽에서 **앞으로 뻗어 나와** 앞뒤로 당긴다.
        /// 과수확 레버는 **판 안에서 위아래로 미끄러진다.** 둘의 실루엣이 닮으면
        /// 「어느 쪽이 위험한 것인가」가 형상으로 전달되지 않고, 그건 색 하나로 때울 수 없다
        /// (`VISUAL_BIBLE` 금지 15 — 붉은색 단독 위험 표현).
        ///
        /// 그래서 이 하우징은 **세로가 지배축**이다 — 높이가 깊이의 3배를 넘는다.
        /// `PortholeMeshTests` 가 그 비를 실제 경계 상자에서 재고, 같은 검사가
        /// `ProcMesh.Lever` 는 **깊이**가 지배축임을 함께 단정한다. 두 형상이 닮아지는
        /// 회귀가 조용히 들어올 수 없다.
        ///
        /// 디텐트 이빨은 「몇 단까지 올릴 수 있는가」를 숫자가 아니라 **형상**으로 말한다.
        /// </summary>
        public static void LeverSlotHousing(ProcMeshBuilder b, float height = 0.62f,
                                            float width = 0.20f, float depth = 0.10f,
                                            int detents = 5, float uvPerMeter = 1f)
        {
            float h = Mathf.Clamp(height, 0.20f, 1.60f);
            float w = Mathf.Clamp(width, 0.08f, 0.50f);
            float d = Mathf.Clamp(depth, 0.03f, 0.30f);
            detents = Mathf.Clamp(detents, 0, 10);

            const float plateT = 0.024f;
            b.AddBox(new Vector3(0f, 0f, plateT * 0.5f), new Vector3(w, h, plateT),
                     0.006f, uvPerMeter);

            // 좌·우 레일. 사이에 **세로 홈**이 남는다 — 이 홈이 슬롯이다.
            float railW = w * 0.32f;
            float railX = (w - railW) * 0.5f;
            b.AddBox(new Vector3(-railX, 0f, plateT + d * 0.5f),
                     new Vector3(railW, h * 0.98f, d), 0f, uvPerMeter);
            b.AddBox(new Vector3(railX, 0f, plateT + d * 0.5f),
                     new Vector3(railW, h * 0.98f, d), 0f, uvPerMeter);

            // 상·하 마감 블록. 위아래가 막혀 있어야 「슬롯」이고, 안 막히면 「띠 두 개」다.
            float capH = Mathf.Min(0.06f, h * 0.12f);
            for (int i = 0; i < 2; i++)
            {
                float y = (i == 0 ? 1f : -1f) * (h * 0.5f - capH * 0.5f);
                b.AddBox(new Vector3(0f, y, plateT + d * 0.4f),
                         new Vector3(w, capH, d * 0.8f), 0f, uvPerMeter);
            }

            // 디텐트 이빨 — 오른쪽 레일 안쪽. 단이 몇 개인지가 옆에서도 보인다.
            if (detents > 0)
            {
                float span = h - capH * 2.4f;
                float toothH = Mathf.Min(0.030f, span / (detents * 2.2f));
                for (int i = 0; i < detents; i++)
                {
                    float t = detents == 1 ? 0.5f : (i + 0.5f) / detents;
                    float y = Mathf.Lerp(-span * 0.5f, span * 0.5f, t);
                    b.AddBox(new Vector3(railX - railW * 0.5f - 0.012f, y, plateT + d * 0.72f),
                             new Vector3(0.024f, toothH, d * 0.30f), 0f, uvPerMeter);
                }
            }
        }

        public static Mesh LeverSlotHandleMesh(float knobRadius = 0.040f, float uvPerMeter = 1f)
        {
            var b = new ProcMeshBuilder(192);
            LeverSlotHandle(b, knobRadius, uvPerMeter);
            return b.ToMesh("PM_LeverSlotHandle");
        }

        /// <summary>
        /// 슬롯 안을 오르내리는 **손잡이**. 원점은 슬라이더 블록의 뒷면(하우징 홈 바닥에 닿는 면).
        /// 씬 소유자가 이 Transform 의 **로컬 Y 만** 움직인다 — 메시를 다시 만들지 않는다.
        /// </summary>
        public static void LeverSlotHandle(ProcMeshBuilder b, float knobRadius = 0.040f,
                                           float uvPerMeter = 1f)
        {
            float r = Mathf.Clamp(knobRadius, 0.015f, 0.10f);

            b.AddBox(new Vector3(0f, 0f, 0.014f), new Vector3(r * 1.9f, r * 1.4f, 0.028f),
                     0.006f, uvPerMeter);
            b.AddPrism(new Vector3(0f, 0f, 0.028f + 0.055f), r * 0.40f, r * 0.35f, 0.110f,
                       6, MeshAxis.Z, 0f, true, true, false, uvPerMeter);
            // 붉은 손잡이 뭉치 — 축의 2.5배 굵기. 손이 닿는 곳이 가장 굵다.
            b.AddPrism(new Vector3(0f, 0f, 0.138f + 0.030f), r, r * 0.86f, 0.060f,
                       6, MeshAxis.Z, 30f, true, true, false, uvPerMeter);
        }

        // ══ 계기 베젤 ═════════════════════════════════════════════════════════

        public static Mesh GaugeBezelMesh(float width = 0.30f, float height = 0.20f,
                                          float uvPerMeter = 1f)
        {
            var b = new ProcMeshBuilder(384);
            GaugeBezel(b, width, height, uvPerMeter);
            return b.ToMesh("PM_GaugeBezel");
        }

        /// <summary>
        /// 검은 베젤 계기 하우징 — 레퍼런스의 「POWER 014 / 100 REQUIRED」 판.
        ///
        /// 판독면이 테두리보다 **안으로** 들어가 있다. 그래야 위에서 떨어지는 백열등 빛이
        /// 판독면에 직접 닿지 않고, 붉은 숫자가 주변보다 밝게 남는다 —
        /// `GRAPHICS_TARGET` §G-3 의 「발광원 종 수」가 실제로 세어지는 조건이다.
        /// 기존 <see cref="PropLibrary"/> 의 원형 계기 하우징과 달리 **사각**이라
        /// 같은 벽에 둘을 놓아도 종류가 구분된다.
        /// </summary>
        public static void GaugeBezel(ProcMeshBuilder b, float width = 0.30f, float height = 0.20f,
                                      float uvPerMeter = 1f)
        {
            float w = Mathf.Clamp(width, 0.08f, 1.20f);
            float h = Mathf.Clamp(height, 0.06f, 1.20f);
            float fr = Mathf.Min(0.030f, Mathf.Min(w, h) * 0.16f);

            const float bodyD = 0.056f;
            b.AddBox(new Vector3(0f, 0f, bodyD * 0.5f), new Vector3(w, h, bodyD), 0.008f, uvPerMeter);

            // 판독면 — 몸통 앞면보다 아주 조금 앞. 테두리가 그 위를 덮는다.
            b.AddBox(new Vector3(0f, 0f, bodyD + 0.006f),
                     new Vector3(w - fr * 2f, h - fr * 2f, 0.012f), 0f, uvPerMeter);

            // 액자 테두리 4개 — 판독면보다 앞으로 나온다.
            const float lipD = 0.026f;
            float lipZ = bodyD + lipD * 0.5f;
            b.AddBox(new Vector3(0f, (h - fr) * 0.5f, lipZ), new Vector3(w, fr, lipD), 0f, uvPerMeter);
            b.AddBox(new Vector3(0f, -(h - fr) * 0.5f, lipZ), new Vector3(w, fr, lipD), 0f, uvPerMeter);
            b.AddBox(new Vector3(-(w - fr) * 0.5f, 0f, lipZ), new Vector3(fr, h - fr * 2f, lipD), 0f, uvPerMeter);
            b.AddBox(new Vector3((w - fr) * 0.5f, 0f, lipZ), new Vector3(fr, h - fr * 2f, lipD), 0f, uvPerMeter);

            // 차양 — 위에서 오는 빛을 끊는다. 기능이 있는 부품이지 장식이 아니다(금지 5).
            // ⚠ 기울인 상자는 **회전 뒤 y 범위가 커진다.** 판 위로 삐져나가면 경계 상자가
            //   커지고 X·Y 중심이 원점에서 밀린다 — 씬 소유자가 배치를 그 값으로 잡으므로
            //   1mm 어긋남도 「스펙대로 놓았는데 겹친다」가 된다. 회전분만큼 미리 내린다.
            b.AddBox(new Vector3(0f, (h - fr) * 0.5f - 0.006f, bodyD + lipD + 0.014f),
                     new Vector3(w * 0.96f, 0.010f, 0.048f), ProcQuat.Euler(-26f, 0f, 0f),
                     0f, uvPerMeter);

            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0) ? -1f : 1f;
                float sy = (i < 2) ? -1f : 1f;
                ProcMesh.Rivet(b, new Vector3(sx * (w * 0.5f - fr * 0.5f), sy * (h * 0.5f - fr * 0.5f),
                                              bodyD + lipD), 0.009f, 0.007f, MeshAxis.Z, 4, uvPerMeter);
            }
        }

        // ══ 층수 표시 패널 ════════════════════════════════════════════════════

        public static Mesh FloorIndicatorHousingMesh(float width = 0.44f, float height = 0.24f,
                                                     float uvPerMeter = 1f)
        {
            var b = new ProcMeshBuilder(384);
            FloorIndicatorHousing(b, width, height, uvPerMeter);
            return b.ToMesh("PM_FloorIndicatorHousing");
        }

        /// <summary>
        /// 문 위 층수 표시 **하우징**. 숫자와 화살표가 앉을 오목한 판독면 + 볼트 4개.
        /// 화살표는 <see cref="FloorIndicatorArrows"/> 로 따로 낸다 — 발광 머티리얼이 다르고,
        /// 방향에 따라 켜고 꺼야 하기 때문이다.
        /// </summary>
        public static void FloorIndicatorHousing(ProcMeshBuilder b, float width = 0.44f,
                                                 float height = 0.24f, float uvPerMeter = 1f)
        {
            float w = Mathf.Clamp(width, 0.12f, 1.20f);
            float h = Mathf.Clamp(height, 0.08f, 0.80f);
            float fr = Mathf.Min(0.034f, Mathf.Min(w, h) * 0.16f);

            const float bodyD = 0.058f;
            b.AddBox(new Vector3(0f, 0f, bodyD * 0.5f), new Vector3(w, h, bodyD), 0.010f, uvPerMeter);
            b.AddBox(new Vector3(0f, 0f, bodyD + 0.005f),
                     new Vector3(w - fr * 2f, h - fr * 2f, 0.010f), 0f, uvPerMeter);

            const float lipD = 0.022f;
            float lipZ = bodyD + lipD * 0.5f;
            b.AddBox(new Vector3(0f, (h - fr) * 0.5f, lipZ), new Vector3(w, fr, lipD), 0f, uvPerMeter);
            b.AddBox(new Vector3(0f, -(h - fr) * 0.5f, lipZ), new Vector3(w, fr, lipD), 0f, uvPerMeter);
            b.AddBox(new Vector3(-(w - fr) * 0.5f, 0f, lipZ), new Vector3(fr, h - fr * 2f, lipD), 0f, uvPerMeter);
            b.AddBox(new Vector3((w - fr) * 0.5f, 0f, lipZ), new Vector3(fr, h - fr * 2f, lipD), 0f, uvPerMeter);

            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0) ? -1f : 1f;
                float sy = (i < 2) ? -1f : 1f;
                ProcMesh.Rivet(b, new Vector3(sx * (w * 0.5f - fr * 0.5f), sy * (h * 0.5f - fr * 0.5f),
                                              bodyD + lipD), 0.010f, 0.008f, MeshAxis.Z, 4, uvPerMeter);
            }
        }

        public static Mesh FloorIndicatorArrowsMesh(float arrowRadius = 0.036f,
                                                    float spacing = 0.13f, float uvPerMeter = 1f)
        {
            var b = new ProcMeshBuilder(96);
            FloorIndicatorArrows(b, arrowRadius, spacing, uvPerMeter);
            return b.ToMesh("PM_FloorIndicatorArrows");
        }

        /// <summary>
        /// 위·아래 **삼각 화살표** 두 개. 위 화살표가 −X, 아래 화살표가 +X 쪽이다.
        /// 원점은 <see cref="FloorIndicatorHousing"/> 의 판독면 앞(z = 0)이라고 보고
        /// 씬 소유자가 그 위에 얹는다.
        ///
        /// 삼각기둥이라 **16 삼각형**뿐이다. 방향은 형태로 읽히지 색으로 읽히지 않는다 —
        /// 색각 이상에서도 위·아래가 구분되어야 한다(`VISUAL_BIBLE` 금지 15).
        /// </summary>
        public static void FloorIndicatorArrows(ProcMeshBuilder b, float arrowRadius = 0.036f,
                                                float spacing = 0.13f, float uvPerMeter = 1f)
        {
            float r = Mathf.Clamp(arrowRadius, 0.010f, 0.20f);
            float gap = Mathf.Clamp(spacing, r * 1.6f, 1.0f);
            const float thick = 0.014f;

            // 각도 오프셋 90° → 꼭짓점이 위. 270° → 아래.
            b.AddPrism(new Vector3(-gap * 0.5f, 0f, thick * 0.5f), r, r, thick,
                       3, MeshAxis.Z, 90f, true, true, false, uvPerMeter);
            b.AddPrism(new Vector3(gap * 0.5f, 0f, thick * 0.5f), r, r, thick,
                       3, MeshAxis.Z, 270f, true, true, false, uvPerMeter);
        }

        // ══ 위험 라벨 판 ══════════════════════════════════════════════════════

        /// <summary>스트라이프가 얹히는 판의 앞면 z. 두 메시가 이 값으로 맞물린다.</summary>
        public const float HazardPlateFrontZ = 0.020f;

        public static Mesh HazardPlateBaseMesh(float width = 0.44f, float height = 0.14f,
                                               float uvPerMeter = 1f)
        {
            var b = new ProcMeshBuilder(256);
            HazardPlateBase(b, width, height, uvPerMeter);
            return b.ToMesh("PM_HazardPlateBase");
        }

        /// <summary>
        /// 위험 라벨 판의 **바탕**(검정). 레퍼런스의 「OVERHARVEST / EXTREME DANGER」 표지.
        /// 스트라이프는 <see cref="HazardPlateStripes"/> 로 따로 낸다 — 노랑이 다른 머티리얼이다.
        /// </summary>
        public static void HazardPlateBase(ProcMeshBuilder b, float width = 0.44f,
                                           float height = 0.14f, float uvPerMeter = 1f)
        {
            float w = Mathf.Clamp(width, 0.10f, 1.60f);
            float h = Mathf.Clamp(height, 0.05f, 0.80f);

            b.AddBox(new Vector3(0f, 0f, HazardPlateFrontZ * 0.5f),
                     new Vector3(w, h, HazardPlateFrontZ), 0.004f, uvPerMeter);
            // 상·하 마감 띠 — 판이 벽에 그려진 것이 아니라 **덧댄 판**으로 읽히게 한다.
            for (int i = 0; i < 2; i++)
            {
                float y = (i == 0 ? 1f : -1f) * (h * 0.5f - 0.008f);
                b.AddBox(new Vector3(0f, y, HazardPlateFrontZ + 0.004f),
                         new Vector3(w * 0.99f, 0.016f, 0.008f), 0f, uvPerMeter);
            }
            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0) ? -1f : 1f;
                float sy = (i < 2) ? -1f : 1f;
                ProcMesh.Rivet(b, new Vector3(sx * (w * 0.5f - 0.020f), sy * (h * 0.5f - 0.020f),
                                              HazardPlateFrontZ + 0.008f),
                               0.009f, 0.007f, MeshAxis.Z, 4, uvPerMeter);
            }
        }

        public static Mesh HazardPlateStripesMesh(float width = 0.44f, float height = 0.14f,
                                                  int stripes = 5, float uvPerMeter = 1f)
        {
            var b = new ProcMeshBuilder(256);
            HazardPlateStripes(b, width, height, stripes, uvPerMeter);
            return b.ToMesh("PM_HazardPlateStripes");
        }

        /// <summary>
        /// 45° **사선 줄무늬** — 형상이다. 텍스처가 아니다.
        ///
        /// 줄무늬를 텍스처로 그리면 정면에서만 줄무늬이고, 비스듬한 1인칭 시야에서는
        /// 평평한 얼룩이 된다. 여기서는 판보다 6mm 튀어나온 띠라서 백열등 아래에서
        /// 실제로 그림자를 만든다 — `GRAPHICS_TARGET` §G-1 의 국소 분산이 직접 오른다.
        ///
        /// 각 띠의 길이는 판 사각형과의 **현(chord)** 을 실제로 풀어서 정한다.
        /// 어림으로 자르면 모서리에서 삐져나오고, 그건 경계 상자 검증이 바로 잡는다.
        /// </summary>
        public static void HazardPlateStripes(ProcMeshBuilder b, float width = 0.44f,
                                              float height = 0.14f, int stripes = 5,
                                              float uvPerMeter = 1f)
        {
            float w = Mathf.Clamp(width, 0.10f, 1.60f);
            float h = Mathf.Clamp(height, 0.05f, 0.80f);
            stripes = Mathf.Clamp(stripes, 1, 16);

            float hx = w * 0.5f - 0.014f;
            float hy = h * 0.5f - 0.012f;
            const float k = 0.70710678f;

            // 대각 방향으로의 오프셋 범위. 판의 두 모서리를 지나는 사선이 양 끝이다.
            float sMax = (hx + hy) * k;
            float pitch = 2f * sMax / (stripes + 1);
            float bandW = pitch * 0.5f;      // 황 : 흑 = 1 : 1

            for (int i = 0; i < stripes; i++)
            {
                float s = -sMax + pitch * (i + 1);
                // **가장 좁은 현**을 쓴다. 띠는 폭이 있으므로 중심선만 자르면 양쪽 긴 모서리가
                // 판 밖으로 나간다.
                if (!ChordRange(s - bandW * 0.5f, hx, hy, out float a0, out float a1)) continue;
                if (!ChordRange(s, hx, hy, out float b0, out float b1)) continue;
                if (!ChordRange(s + bandW * 0.5f, hx, hy, out float c0, out float c1)) continue;

                float t0 = Mathf.Max(a0, Mathf.Max(b0, c0));
                float t1 = Mathf.Min(a1, Mathf.Min(b1, c1));
                float len = t1 - t0;
                if (len <= 0.012f) continue;

                float tm = (t0 + t1) * 0.5f;
                float cx = -s * k + tm * k;
                float cy = s * k + tm * k;
                b.AddBox(new Vector3(cx, cy, HazardPlateFrontZ + 0.003f),
                         new Vector3(len, bandW, 0.006f), ProcQuat.Euler(0f, 0f, 45f),
                         0f, uvPerMeter);
            }
        }

        /// <summary>
        /// 오프셋 <paramref name="s"/> 의 45° 직선이 반폭 (hx, hy) 사각형 안에 있는 t 구간.
        /// 직선은 p(t) = s·(−k, k) + t·(k, k) 다.
        /// </summary>
        private static bool ChordRange(float s, float hx, float hy, out float t0, out float t1)
        {
            const float k = 0.70710678f;
            float xa = (-hx + s * k) / k;
            float xb = (hx + s * k) / k;
            float ya = (-hy - s * k) / k;
            float yb = (hy - s * k) / k;

            t0 = Mathf.Max(Mathf.Min(xa, xb), Mathf.Min(ya, yb));
            t1 = Mathf.Min(Mathf.Max(xa, xb), Mathf.Max(ya, yb));
            return t1 > t0;
        }
    }
}
