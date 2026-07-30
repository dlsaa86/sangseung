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
            return true;
        }

        public bool Remove(string id)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (!string.Equals(_items[i].Id, id, StringComparison.Ordinal)) continue;
                _items.RemoveAt(i);
                return true;
            }
            return false;
        }

        public void Clear() => _items.Clear();

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
            return leaving;
        }

        /// <summary>
        /// 규칙 다발에 적재 효과를 적용한다. 반드시 계약 적용 **뒤에** 호출한다 —
        /// `SpinRuleSet` 주석이 못박은 발동 순서(기본값 → 층 → 계약 → 승객·부품)다.
        /// 순서가 뒤집히면 계약의 곱셈이 승객의 가산 위에 얹혀 값이 달라진다.
        /// </summary>
        public void ApplyTo(SpinRuleSet rules)
        {
            if (rules == null) return;
            for (int i = 0; i < _items.Count; i++) _items[i].ApplyTo(rules);
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

        private static readonly BuildItem[] _all =
        {
            // ── 승객: 가볍고, 내리고, 요금을 남긴다 ──
            new BuildItem
            {
                Id = "PSG_SURVEYOR", Label = "계측 기사", Kind = BuildItemKind.Passenger,
                Description = "흡수체를 두 개만 모여도 정화 대상으로 읽는다.",
                Weight = 8f, DestinationFloor = 5, DisembarkReward = 40f,
                Effects = new[]
                {
                    BuildEffect.Of(BuildEffectKind.PurifyThreshold, SymbolKind.Absorber, 2f),
                },
            },
            new BuildItem
            {
                Id = "PSG_MOURNER", Label = "문상객", Kind = BuildItemKind.Passenger,
                Description = "빈칸이 다시 채워질 때 정상 영혼이 더 자주 온다.",
                Weight = 7f, DestinationFloor = 6, DisembarkReward = 35f,
                Effects = new[]
                {
                    BuildEffect.Of(BuildEffectKind.RefillSoulBias, 0.12f),
                },
            },
            new BuildItem
            {
                Id = "PSG_TECHNICIAN", Label = "정비공", Kind = BuildItemKind.Passenger,
                Description = "장치를 손봐 정상 영혼 하나의 산출을 올린다.",
                Weight = 9f, DestinationFloor = 7, DisembarkReward = 45f,
                Effects = new[]
                {
                    BuildEffect.Of(BuildEffectKind.NormalSoulValue, 2f),
                },
            },
            new BuildItem
            {
                Id = "PSG_PORTER", Label = "짐꾼", Kind = BuildItemKind.Passenger,
                Description = "적재를 다시 묶어 허용 중량을 늘린다. 규칙은 바꾸지 않는다.",
                Weight = 12f, CapacityBonus = 30f, DestinationFloor = 8, DisembarkReward = 30f,
                Effects = Array.Empty<BuildEffect>(),
            },
            new BuildItem
            {
                Id = "PSG_ZEALOT", Label = "광신자", Kind = BuildItemKind.Passenger,
                Description = "증식체를 불러들이고 그 패턴의 값을 올린다. 무겁고 위험하다.",
                Weight = 16f, DestinationFloor = 10, DisembarkReward = 90f,
                Effects = new[]
                {
                    BuildEffect.Of(BuildEffectKind.PatternBonus, SymbolKind.Proliferator, 0.8f),
                    BuildEffect.Of(BuildEffectKind.Appearance, SymbolKind.Proliferator, 1.25f),
                },
            },

            // ── Notion 「적재·탑승·빌드 시스템」에서 동결한 2종 ──
            // 저장소 문서에 없던 설계라 `NotionSyncReport.md` 절차대로 옮겼다.
            // 나머지 2종(연쇄 코일 = 캐스케이드 칸 1개 추가 재추첨, 검침원 = 잔류 1개 무효화)은
            // `SpinEngine` 자체를 고쳐야 해서 이번 범위에 넣지 않았다 — 같은 보고서에 기록.
            new BuildItem
            {
                Id = "PSG_SURVEYOR_LINE", Label = "측량사", Kind = BuildItemKind.Passenger,
                Description = "한 줄로 선 저항의 값을 읽어낸다. 직선 패턴만 강해진다.",
                Weight = 6f, DestinationFloor = 9, DisembarkReward = 55f,
                Effects = new[]
                {
                    BuildEffect.Of(BuildEffectKind.LineMultiplier, 0.5f),
                },
            },
            new BuildItem
            {
                Id = "PRT_OVERHARVEST_TRANSFORMER", Label = "과수확 변압기", Kind = BuildItemKind.Part,
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
                Id = "PRT_DIAGONAL_BINDER", Label = "사선 결속기", Kind = BuildItemKind.Part,
                Description = "대각으로 붙은 저항도 연결 덩어리로 센다.",
                Weight = 26f,
                Effects = new[]
                {
                    BuildEffect.Of(BuildEffectKind.DiagonalConnects, 1f),
                },
            },
            new BuildItem
            {
                Id = "PRT_CASCADE_GOVERNOR", Label = "연쇄 조속기", Kind = BuildItemKind.Part,
                Description = "연쇄가 깊어질수록 배수가 더 가파르게 오른다.",
                Weight = 22f,
                Effects = new[]
                {
                    BuildEffect.Of(BuildEffectKind.CascadeStep, 0.25f),
                },
            },
            new BuildItem
            {
                Id = "PRT_RESIDUAL_DAMPENER", Label = "잔류 감쇠기", Kind = BuildItemKind.Part,
                Description = "정화하지 못한 저항이 물리는 대가를 절반 가까이 깎는다.",
                Weight = 18f,
                Effects = new[]
                {
                    BuildEffect.Of(BuildEffectKind.ResidualMitigation, 0.55f),
                },
            },
            new BuildItem
            {
                Id = "PRT_SOUL_TRAP", Label = "영혼 포집망", Kind = BuildItemKind.Part,
                Description = "스핀마다 정상 영혼 두 개를 확보해 바닥을 받친다.",
                Weight = 20f,
                Effects = new[]
                {
                    BuildEffect.Of(BuildEffectKind.GuaranteeNormalSouls, 2f),
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
            var picked = new BuildItem[take];
            for (int i = 0; i < take; i++)
            {
                int index = random.Next(pool.Count);
                picked[i] = pool[index];
                pool.RemoveAt(index);
            }
            return picked;
        }
    }
}
