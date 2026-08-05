using System;
using System.Collections.Generic;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.Build
{
    /// <summary>
    /// 지금 엘리베이터에 실려 있는 것 전부. 무게·허용 중량·규칙 변경의 단일 출처다.
    ///
    /// 순수 C#으로 두는 이유: 무게가 요구 전력과 위험 점수를 바꾸므로 씬 없이 검증할 수
    /// 있어야 한다. 이전 구현은 무게 계산이 비활성 `GameObject` 위의 `PassengerManager`에만
    /// 있어서, 실제로 도는 런에서는 무게가 영원히 0이었다.
    /// </summary>
    public sealed class BuildLoadout
    {
        private readonly List<BuildItem> _items = new List<BuildItem>();

        /// <summary>승객·부품을 합쳐 실을 수 있는 최대 개수. 공간이 동선을 막지 않는 상한이다.</summary>
        public const int MaxSlots = 6;

        /// <summary>
        /// 적재가 바뀔 때마다 발생한다. 무게가 요구 전력과 위험 점수의 입력이므로,
        /// 바뀐 사실을 층이 즉시 알아야 한다.
        ///
        /// 이게 없을 때 실제로 무슨 일이 있었는가: `RunSession.AddWeight`에만 갱신을 붙였더니
        /// `Loadout.Add`로 직접 실은 경로가 그대로 새어 나갔다. 캡처 리그가 6개를 실었는데
        /// 층은 옛 무게를 들고 있었고, 과적 상태에서 위험 단계가 Stable로 찍혔다.
        /// 호출부마다 갱신을 기억하게 하는 대신 **변경 자체가 알리도록** 뒤집는다.
        /// </summary>
        public event Action Changed;

        public IReadOnlyList<BuildItem> Items => _items;
        public int Count => _items.Count;
        public bool IsFull => _items.Count >= MaxSlots;

        public float TotalWeight
        {
            get
            {
                float sum = 0f;
                for (int i = 0; i < _items.Count; i++) sum += _items[i].Weight;
                return sum;
            }
        }

        public float TotalCapacityBonus
        {
            get
            {
                float sum = 0f;
                for (int i = 0; i < _items.Count; i++) sum += _items[i].CapacityBonus;
                return sum;
            }
        }

        public bool Contains(string id)
        {
            for (int i = 0; i < _items.Count; i++)
                if (string.Equals(_items[i].Id, id, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>이미 실려 있거나 자리가 없으면 거부한다. 조용히 무시하지 않는다.</summary>
        public bool Add(BuildItem item)
        {
            if (item == null || IsFull || Contains(item.Id)) return false;
            _items.Add(item);
            Changed?.Invoke();
            return true;
        }

        public bool Remove(string id)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (!string.Equals(_items[i].Id, id, StringComparison.Ordinal)) continue;
                _items.RemoveAt(i);
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        public void Clear()
        {
            if (_items.Count == 0) return;
            _items.Clear();
            Changed?.Invoke();
        }

        /// <summary>
        /// 이 층에서 내리는 승객을 떼어내고 요금 합계를 돌려준다.
        /// 부품은 목적지가 없으므로 남는다.
        /// </summary>
        public List<BuildItem> TakeDeparting(int floor, out float reward)
        {
            var leaving = new List<BuildItem>();
            reward = 0f;
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (!_items[i].LeavesAt(floor)) continue;
                reward += _items[i].DisembarkReward;
                leaving.Add(_items[i]);
                _items.RemoveAt(i);
            }
            leaving.Reverse();
            if (leaving.Count > 0) Changed?.Invoke();
            return leaving;
        }

        /// <summary>
        /// 규칙 다발에 적재 효과를 적용한다. 반드시 계약 적용 **뒤에** 호출한다 —
        /// `SpinRuleSet` 주석이 못박은 발동 순서(기본값 → 층 → 계약 → 승객·부품)다.
        /// 순서가 뒤집히면 계약의 곱셈이 승객의 가산 위에 얹혀 값이 달라진다.
        /// </summary>
        /// <remarks>
        /// **두 패스로 돈다** (`BuildEffectCondition` 주석에 경위가 있다).
        ///
        /// ① 조건 없는 효과를 전부 적용한다.
        /// ② 그 결과를 **얼려서**(`Clone`) 조건을 판정하고, 조건부 효과를 적용한다.
        ///
        /// 얼리는 것이 핵심이다. 살아 있는 `rules` 를 그대로 조건 판정에 쓰면 2패스에서
        /// 먼저 적용된 조건부 효과가 뒤 품목의 조건을 켤 수 있고, 그러면 **같은 조합이
        /// 집은 순서에 따라 다른 값**을 낸다. 그 차이는 플레이어가 설명할 수 없다.
        ///
        /// 비용은 층당 `Clone` 한 번이다 — 스핀마다가 아니라 규칙 다발을 만들 때뿐이다.
        /// </remarks>
        public void ApplyTo(SpinRuleSet rules)
        {
            if (rules == null) return;

            for (int i = 0; i < _items.Count; i++) _items[i].ApplyUnconditionalTo(rules);

            // 조건부가 하나도 없으면 복제하지 않는다. 대부분의 적재가 여기에 걸린다.
            bool anyConditional = false;
            for (int i = 0; i < _items.Count && !anyConditional; i++)
            {
                BuildEffect[] effects = _items[i].Effects;
                if (effects == null) continue;
                for (int e = 0; e < effects.Length; e++)
                    if (!effects[e].IsUnconditional) { anyConditional = true; break; }
            }
            if (!anyConditional) return;

            SpinRuleSet probe = rules.Clone();
            for (int i = 0; i < _items.Count; i++) _items[i].ApplyConditionalTo(rules, probe);
        }

        public BuildLoadout Clone()
        {
            var copy = new BuildLoadout();
            copy._items.AddRange(_items);
            return copy;
        }

        /// <summary>사고 기록기가 "무엇을 싣고 있었는가"를 적을 때 쓴다.</summary>
        public string Describe()
        {
            if (_items.Count == 0) return "적재 없음";
            var lines = new string[_items.Count];
            for (int i = 0; i < _items.Count; i++) lines[i] = _items[i].Describe();
            return string.Join("\n", lines);
        }

        public string DescribeShort()
        {
            if (_items.Count == 0) return "없음";
            var names = new string[_items.Count];
            for (int i = 0; i < _items.Count; i++) names[i] = _items[i].Label;
            return string.Join(", ", names);
        }
    }

    /// <summary>
    /// 프로토타입의 승객·부품 목록과 층별 제시 규칙.
    ///
    /// 밸런스 수치는 전부 임시값이다(`ASSUMPTION_LOG.md` A-20260731-02). 승객과 부품을
    /// 나눈 축은 무게가 아니라 **영속성**이다 — 승객은 목적지에서 내리며 요금을 남기고,
    /// 부품은 런이 끝날 때까지 무게로 남는다. 그래서 부품은 강하고 무겁다.
    /// </summary>
    public static class BuildCatalog
    {
        /// <summary>제시 시드가 스핀 시드와 충돌하지 않도록 밀어둔 좌표. 스핀은 0~12만 쓴다.</summary>
        private const int OfferSeedOffset = 900;

        // ── 2026-08-05 개편 — 「효과들이 서로를 모른다」를 고친다 ────────────────
        //
        // 개편 전 실측(시드 3000, `tools/headless -- build`): 전체 이득 +24.13%p 중
        // **사선 결속기 혼자 +19.67%p(82%)**, 음(−)의 기여 5종, 시너지 짝 0개.
        // 전략이 하나뿐이라는 뜻이고, 그건 밸런스 수치가 아니라 **설계 결함**이다
        // (`docs/runtime/BUILD_DIVERSITY_AUDIT.md`).
        //
        // 세 가지를 바꿨다.
        // ① **1등을 깎지 않는다.** 사선 결속기는 한 자리도 안 건드렸다 — 너프하면
        //    2등이 새 1등이 될 뿐이고, 2·3등 차이는 0.9%p 였다(같은 보고서 §안 C).
        // ② **나머지에 조건부 상단을 준다.** 각 품목이 「무엇이 켜져 있을 때 진짜 값을
        //    내는가」를 갖는다. 이것이 짝 시너지가 나올 수 있는 유일한 경로다.
        // ③ **음의 기여를 방향으로 되돌린다.** 영혼 포집망(−3.00%p)은 보장 2개가 9칸 중
        //    저항을 밀어내 정화 재료를 없앴다 — 효과의 방향이 이 게임의 전력 원천과 반대였다.
        //    보장을 1개로 내려 「바닥을 받친다」로 되돌리고 산출을 따로 얹는다.
        //
        // 조건은 **1패스가 끝난 상태에서만** 읽는다(`BuildLoadout.ApplyTo`). 그래서 같은
        // 조합은 집은 순서와 무관하게 같은 값을 낸다.
        private static readonly BuildItem[] _all =
        {
            // ── 승객: 가볍고, 내리고, 요금을 남긴다 ──
            new BuildItem
            {
                Id = "PSG_SURVEYOR", Label = "계측 기사", Kind = BuildItemKind.Passenger,
                Axis = BuildAxis.Stability,
                Description = "흡수체를 두 개만 모여도 정화 대상으로 읽는다. " +
                              "대각까지 연결로 세는 장치가 있으면 그 눈금이 값으로 바뀐다.",
                Weight = 8f, DestinationFloor = 5, DisembarkReward = 40f,
                Effects = new[]
                {
                    BuildEffect.Of(BuildEffectKind.PurifyThreshold, SymbolKind.Absorber, 2f),
                    BuildEffect.Of(BuildEffectKind.PatternBonus, SymbolKind.Absorber, 0.6f)
                               .When(BuildEffectCondition.DiagonalConnects),
                },
            },
            new BuildItem
            {
                Id = "PSG_MOURNER", Label = "문상객", Kind = BuildItemKind.Passenger,
                Axis = BuildAxis.Stability,
                Description = "빈칸이 다시 채워질 때 정상 영혼이 더 자주 온다. " +
                              "영혼이 이미 보장돼 있으면 그 하나하나가 더 무거워진다.",
                Weight = 7f, DestinationFloor = 6, DisembarkReward = 35f,
                Effects = new[]
                {
                    BuildEffect.Of(BuildEffectKind.RefillSoulBias, 0.15f),
                    BuildEffect.Of(BuildEffectKind.NormalSoulValue, 5f)
                               .When(BuildEffectCondition.SoulsGuaranteed),
                },
            },
            new BuildItem
            {
                Id = "PSG_TECHNICIAN", Label = "정비공", Kind = BuildItemKind.Passenger,
                Axis = BuildAxis.Stability,
                Description = "장치를 손봐 정상 영혼 하나의 산출을 올린다. " +
                              "확보된 영혼이 있으면 손볼 곳이 더 많아진다.",
                Weight = 9f, DestinationFloor = 7, DisembarkReward = 45f,
                Effects = new[]
                {
                    BuildEffect.Of(BuildEffectKind.NormalSoulValue, 3f),
                    BuildEffect.Of(BuildEffectKind.NormalSoulValue, 5f)
                               .When(BuildEffectCondition.SoulsGuaranteed),
                },
            },
            new BuildItem
            {
                Id = "PSG_PORTER", Label = "짐꾼", Kind = BuildItemKind.Passenger,
                Axis = BuildAxis.Load,
                Description = "적재를 다시 묶어 허용 중량을 늘린다. 규칙은 바꾸지 않는다.",
                // 무게 12 → 10. 규칙을 하나도 안 바꾸는 품목이 남들보다 무거울 이유가 없다.
                // 짐꾼의 값은 **과적 대가가 실제로 물릴 때** 생긴다 — 개편 전 실측에서
                // 과적은 층의 5.47% 에서만 일어났고, 그래서 이 품목은 「거의 항상 빈 짐」이었다.
                // 그 대가 축은 `WeightSnapshot` 쪽에서 따로 고친다.
                Weight = 10f, CapacityBonus = 30f, DestinationFloor = 8, DisembarkReward = 45f,
                Effects = Array.Empty<BuildEffect>(),
            },
            new BuildItem
            {
                Id = "PSG_ZEALOT", Label = "광신자", Kind = BuildItemKind.Passenger,
                Axis = BuildAxis.Residual,
                Description = "증식체를 불러들이고 그 패턴의 값을 올린다. 무겁고 위험하다. " +
                              "남긴 저항의 대가가 이미 커져 있다면 그 광기가 보상으로 돌아온다.",
                Weight = 16f, DestinationFloor = 10, DisembarkReward = 90f,
                Effects = new[]
                {
                    BuildEffect.Of(BuildEffectKind.PatternBonus, SymbolKind.Proliferator, 0.8f),
                    BuildEffect.Of(BuildEffectKind.Appearance, SymbolKind.Proliferator, 1.25f),
                    BuildEffect.Of(BuildEffectKind.PurifyReward, SymbolKind.Proliferator, 1.5f)
                               .When(BuildEffectCondition.ResidualAmplified),
                },
            },

            // ── Notion 「적재·탑승·빌드 시스템」에서 동결한 2종 ──
            // 저장소 문서에 없던 설계라 `NotionSyncReport.md` 절차대로 옮겼다.
            // 나머지 2종(연쇄 코일 = 캐스케이드 칸 1개 추가 재추첨, 검침원 = 잔류 1개 무효화)은
            // `SpinEngine` 자체를 고쳐야 해서 이번 범위에 넣지 않았다 — 같은 보고서에 기록.
            new BuildItem
            {
                Id = "PSG_SURVEYOR_LINE", Label = "측량사", Kind = BuildItemKind.Passenger,
                Axis = BuildAxis.Pattern,
                Description = "한 줄로 선 저항의 값을 읽어낸다. 직선 패턴만 강해진다. " +
                              "한 저항을 여러 모양으로 세는 장치와 만나면 줄이 두 번 읽힌다.",
                Weight = 6f, DestinationFloor = 9, DisembarkReward = 55f,
                Effects = new[]
                {
                    BuildEffect.Of(BuildEffectKind.LineMultiplier, 0.5f),
                    BuildEffect.Of(BuildEffectKind.LineMultiplier, 0.8f)
                               .When(BuildEffectCondition.MultiplePatterns),
                },
            },
            new BuildItem
            {
                Id = "PRT_OVERHARVEST_TRANSFORMER", Label = "과수확 변압기", Kind = BuildItemKind.Part,
                Axis = BuildAxis.Residual,
                Description = "정화 보상을 끌어올리는 대신 남긴 저항의 대가도 함께 커진다.",
                Weight = 24f,
                Effects = new[]
                {
                    BuildEffect.Of(BuildEffectKind.PurifyReward, SymbolKind.Absorber, 1.5f),
                    BuildEffect.Of(BuildEffectKind.PurifyReward, SymbolKind.Proliferator, 1.5f),
                    BuildEffect.Of(BuildEffectKind.ResidualMitigation, 1.25f),
                },
            },

            // ── 부품: 무겁고, 남고, 규칙 자체를 바꾼다 ──
            new BuildItem
            {
                // 🔴 **값을 깎지 않고 모양을 바꿨다.**
                //
                // 이 품목 혼자 전체 이득의 82%를 가져갔다. 감사는 「1등을 너프하면
                // 2등이 새 1등이 될 뿐」이라며 수치 조정을 금했고, 그 판단은 옳다 —
                // 그래서 배수를 깎는 대신 **대가를 붙였다.**
                //
                // 대각을 세면 연결이 훨씬 자주 성립한다. 그 대신 한 덩어리의 값이
                // 내려간다 — 「느슨하게 묶으니 자주 걸리지만 무르다」. 결속기는
                // **연쇄의 빈도**를 사고 **한 번의 크기**를 판다. 노션 §6.2 의
                // 「강한 조합은 더 높은 요구 전력·무게·잔류 위험을 동반한다」가
                // 이 품목에만 적용되지 않고 있었다.
                //
                // 이 대가가 연쇄 조속기와의 짝을 **더** 의미 있게 만든다 — 조속기는
                // 깊이가 늘수록 배수를 밀어 올리므로, 얕아진 한 방을 길이로 되찾는다.
                Id = "PRT_DIAGONAL_BINDER", Label = "사선 결속기", Kind = BuildItemKind.Part,
                Axis = BuildAxis.Cascade,
                Description = "대각으로 붙은 저항도 연결 덩어리로 센다. " +
                              "대신 느슨하게 묶인 덩어리 하나의 값은 내려간다.",
                Weight = 26f,
                Effects = new[]
                {
                    BuildEffect.Of(BuildEffectKind.DiagonalConnects, 1f),
                    BuildEffect.Of(BuildEffectKind.ClusterMultiplier, -1.2f),
                },
            },
            new BuildItem
            {
                Id = "PRT_CASCADE_GOVERNOR", Label = "연쇄 조속기", Kind = BuildItemKind.Part,
                Axis = BuildAxis.Cascade,
                Description = "연쇄가 깊어질수록 배수가 더 가파르게 오른다. " +
                              "연쇄가 실제로 길어지는 판에서만 그 기울기가 값이 된다.",
                Weight = 22f,
                Effects = new[]
                {
                    BuildEffect.Of(BuildEffectKind.CascadeStep, 0.25f),
                    // 감사가 예시로 든 바로 그 형태다 — 「연결 판정이 있었던 판에서만
                    // 증분이 더 붙는다」. 조속기 단독은 연쇄가 잘 안 나는 판에서
                    // 기울기만 가팔라 아무 일도 안 했고, 그래서 −1.27%p 였다.
                    BuildEffect.Of(BuildEffectKind.CascadeStep, 0.40f)
                               .When(BuildEffectCondition.DiagonalConnects),
                },
            },
            new BuildItem
            {
                Id = "PRT_RESIDUAL_DAMPENER", Label = "잔류 감쇠기", Kind = BuildItemKind.Part,
                Axis = BuildAxis.Residual,
                Description = "정화하지 못한 저항이 물리는 대가를 절반 가까이 깎는다. " +
                              "적은 수로도 정화가 되는 눈금이 있으면 그 여유가 보상으로 바뀐다.",
                Weight = 18f,
                Effects = new[]
                {
                    BuildEffect.Of(BuildEffectKind.ResidualMitigation, 0.55f),
                    BuildEffect.Of(BuildEffectKind.PurifyReward, SymbolKind.Absorber, 1.4f)
                               .When(BuildEffectCondition.PurifyThresholdLowered),
                },
            },
            new BuildItem
            {
                Id = "PRT_SOUL_TRAP", Label = "영혼 포집망", Kind = BuildItemKind.Part,
                Axis = BuildAxis.Stability,
                // 🔴 보장 2 → 1. `SpinEngine.ApplyGuaranteedNormalSouls` 는 저항 칸을
                //    **덮어써서** 영혼을 만든다. 9칸에서 2개를 강제하면 정화 재료가
                //    사라져 전력 원천이 줄고, 실측 기여가 **−3.00%p** 였다.
                //    노션 §3.1 이 이 품목에 요구하는 것은 평균 상승이 아니라
                //    「완전한 꽝을 줄인다」 — 바닥을 받치되 천장을 깎지 않는 형태다.
                Description = "스핀마다 정상 영혼 하나를 확보해 바닥을 받치고, 그 하나를 무겁게 만든다.",
                Weight = 16f,
                Effects = new[]
                {
                    BuildEffect.Of(BuildEffectKind.GuaranteeNormalSouls, 1f),
                    BuildEffect.Of(BuildEffectKind.NormalSoulValue, 2f),
                },
            },
            new BuildItem
            {
                // 신규. `BuildEffectKind.MultiplePatterns` 는 `SpinRuleSet` 에 구현돼
                // 있었지만 **이 값을 켜는 품목이 카탈로그에 하나도 없었다** — 만들어졌고
                // 아무도 안 쓰는 상태였고, 그 탓에 측량사의 조건도 성립할 수 없었다.
                Id = "PRT_PATTERN_DOUBLER", Label = "중복 계수기", Kind = BuildItemKind.Part,
                Axis = BuildAxis.Pattern,
                Description = "한 저항을 여러 모양으로 동시에 센다. 같은 판이 두 번 읽힌다.",
                Weight = 24f,
                Effects = new[]
                {
                    BuildEffect.Of(BuildEffectKind.MultiplePatterns, 1f),
                },
            },
        };

        public static IReadOnlyList<BuildItem> All => _all;

        public static BuildItem ById(string id)
        {
            for (int i = 0; i < _all.Length; i++)
                if (string.Equals(_all[i].Id, id, StringComparison.Ordinal)) return _all[i];
            return null;
        }

        /// <summary>
        /// 이 층에서 제시할 후보. 이미 실은 것은 빼고, 런 시드와 층 번호에서 결정론적으로 뽑는다.
        /// 같은 시드·같은 층은 언제 물어봐도 같은 목록을 준다 — 재현 없이는 밸런스를 못 고친다.
        /// </summary>
        /// <remarks>
        /// 🔴 **축이 겹치지 않게 뽑는다** (2026-08-05).
        ///
        /// 직전 판본은 풀에서 그냥 무작위 3장이었다. 그러면 「안정 축 3장」 같은 제시가
        /// 나오고, 그 층의 선택은 **방향의 선택이 아니라 수치의 선택**으로 줄어든다.
        /// 노션 §4 는 각 층이 「최소 하나의 명확한 질문」을 만들 것을 요구하는데,
        /// 같은 방향 세 장은 질문을 만들지 못한다.
        ///
        /// 규칙은 하나다 — **먼저 축을 뽑고, 그 축에서 한 장을 뽑는다.** 축이 모자라면
        /// (후반에 이미 많이 실어 남은 축이 적을 때) 남은 것에서 채운다. 진행 불가를
        /// 만들지 않는 것이 축 다양성보다 우선한다.
        ///
        /// 결정론은 그대로다. 같은 (런 시드, 층)은 언제 물어도 같은 목록을 준다 —
        /// 재현 없이는 밸런스를 못 고친다.
        ///
        /// ⚠ 축은 **뽑는 데만** 쓴다. 화면에 태그로 노출하지 않는다
        /// (노션 §6.2 「태그·세트 카운터·추천 조합은 선공개하지 않는다」).
        /// </remarks>
        public static BuildItem[] OffersFor(int runSeed, int floor, BuildLoadout carried, int count)
        {
            var pool = new List<BuildItem>(_all.Length);
            for (int i = 0; i < _all.Length; i++)
            {
                if (carried != null && carried.Contains(_all[i].Id)) continue;
                pool.Add(_all[i]);
            }
            if (pool.Count == 0 || count <= 0) return Array.Empty<BuildItem>();

            var random = new Random(SpinSeed.Derive(runSeed, floor, OfferSeedOffset));
            int take = Math.Min(count, pool.Count);
            var picked = new List<BuildItem>(take);
            var usedAxes = new List<BuildAxis>(take);

            // ① 축이 겹치지 않는 한 장씩.
            while (picked.Count < take)
            {
                var candidates = new List<BuildItem>(pool.Count);
                for (int i = 0; i < pool.Count; i++)
                    if (!usedAxes.Contains(pool[i].Axis)) candidates.Add(pool[i]);
                if (candidates.Count == 0) break;

                BuildItem chosen = candidates[random.Next(candidates.Count)];
                picked.Add(chosen);
                usedAxes.Add(chosen.Axis);
                pool.Remove(chosen);
            }

            // ② 남은 자리는 축을 따지지 않고 채운다. 빈 제시가 진행을 막는 것보다 낫다.
            while (picked.Count < take && pool.Count > 0)
            {
                int index = random.Next(pool.Count);
                picked.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return picked.ToArray();
        }
    }
}
