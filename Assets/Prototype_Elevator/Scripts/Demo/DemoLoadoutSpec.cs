using System;
using System.Collections.Generic;
using System.Text;
using Ascend.Prototype.Build;

namespace Ascend.Prototype.Demo
{
    /// <summary>
    /// 「이 런은 이것을 싣고 시작한다」를 적어 두는 순수 C# 명세.
    ///
    /// 왜 필요한가: 지금 적재는 <see cref="BuildCatalog.OffersFor"/>가 층마다 뽑아 주는
    /// 후보에서만 고를 수 있다. 제시는 런 시드와 층에서 결정론적으로 나오므로,
    /// **특정 빌드를 보고 싶으면 그 빌드가 제시되는 시드를 찾아내야 한다.** 축 하나를
    /// 끝까지 태워 보려면 그런 시드가 존재하지도 않을 수 있다.
    ///
    /// 실제로 그 대가를 이미 치렀다 — `PD2930_REPORT_20260806.md` §2 는 흡수체 빌드
    /// 측정의 44%가 **빈 적재**였다고 적는다. 계수를 세 배로 올려도 완주율이 20.17%에
    /// 못 박힌 듯 안 움직인 이유가 그것이었다. 효과가 없었던 게 아니라 **입력이 없었다.**
    /// 헤드리스는 그 문제를 강제 탑승으로 풀었다. 사람이 손으로 플레이할 때도 같은
    /// 장치가 필요하다 — 표로 읽는 것과 직접 태워 보는 것은 다른 검증이다.
    ///
    /// 순수 C#으로 두는 이유는 <see cref="BuildLoadPolicy"/>와 같다. 씬 없이 검증할 수
    /// 있어야 하고, 에디터 창과 런타임 주입기가 **같은 규칙**을 써야 한다. 규칙을 두 벌
    /// 쓰면 창에서 고른 것과 실제로 실리는 것이 갈라진다.
    /// </summary>
    public sealed class DemoLoadoutSpec
    {
        private readonly List<string> _ids = new List<string>();

        /// <summary>실을 품목 id. 적은 순서대로 싣는다.</summary>
        public IReadOnlyList<string> Ids => _ids;

        public int Count => _ids.Count;

        public DemoLoadoutSpec() { }

        public DemoLoadoutSpec(IEnumerable<string> ids)
        {
            if (ids == null) return;
            foreach (string id in ids) Add(id);
        }

        /// <summary>
        /// 이미 있는 id 와 빈 문자열은 거부한다. 조용히 무시하지 않는 것은
        /// <see cref="BuildLoadout.Add"/>와 같은 이유다 — 중복이 슬롯을 먹은 채
        /// 「왜 안 실렸지」로 나타나면 원인을 코드에서 찾게 된다.
        /// </summary>
        public bool Add(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            id = id.Trim();
            if (_ids.Contains(id)) return false;
            _ids.Add(id);
            return true;
        }

        public bool Remove(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            return _ids.Remove(id.Trim());
        }

        public bool Contains(string id)
            => !string.IsNullOrWhiteSpace(id) && _ids.Contains(id.Trim());

        public void Clear() => _ids.Clear();

        /// <summary>
        /// 실을 수 없는 이유를 전부 모아 준다. 비어 있으면 그대로 실린다.
        ///
        /// **적용 전에 부르는 것이 요점이다.** 적용 도중에 조용히 떨어뜨리면
        /// 「6개를 골랐는데 4개만 탔다」가 화면 어디에도 안 남는다.
        /// </summary>
        public List<string> Problems()
        {
            var problems = new List<string>();

            for (int i = 0; i < _ids.Count; i++)
            {
                if (BuildCatalog.ById(_ids[i]) == null)
                    problems.Add($"카탈로그에 없는 id: '{_ids[i]}'");
            }

            if (_ids.Count > BuildLoadout.MaxSlots)
            {
                problems.Add($"슬롯 초과: {_ids.Count}개 지정 / 상한 {BuildLoadout.MaxSlots}개 " +
                             $"— 앞의 {BuildLoadout.MaxSlots}개만 실린다");
            }

            return problems;
        }

        /// <summary>
        /// 적재를 비우고 이 명세대로 다시 싣는다. 실제로 실린 개수를 돌려준다.
        ///
        /// 비우고 시작하는 이유: 데모의 목적은 「이 빌드만」을 보는 것이다. 자동 정책이
        /// 먼저 채워 둔 것이 남으면 관측 대상이 섞인다.
        /// </summary>
        public int ApplyTo(BuildLoadout loadout)
        {
            if (loadout == null) return 0;
            loadout.Clear();
            return TopUp(loadout);
        }

        /// <summary>
        /// 비우지 않고 <b>빠진 것만</b> 채운다. 실제로 새로 실린 개수를 돌려준다.
        ///
        /// 왜 따로 있나: 승객은 <see cref="BuildItem.DestinationFloor"/>에서 내린다
        /// (<see cref="BuildLoadout.TakeDeparting"/>). 흡수체 축의 계측 기사는 5층에서
        /// 내리므로 7층을 측정하면 **그 빌드는 이미 없다.** 데모에서 축 하나를 10층까지
        /// 보려면 내린 자리를 다시 채워야 한다.
        ///
        /// 이것은 게임 규칙이 아니라 **관측 장치**다. 하차는 밸런스의 일부이므로
        /// (`재탑승 없음`이 기본), 다시 태우는 것은 데모에서 명시적으로 켤 때만 한다.
        /// </summary>
        public int TopUp(BuildLoadout loadout)
        {
            if (loadout == null) return 0;

            int added = 0;
            for (int i = 0; i < _ids.Count; i++)
            {
                if (loadout.IsFull) break;
                if (loadout.Contains(_ids[i])) continue;

                BuildItem item = BuildCatalog.ById(_ids[i]);
                if (item == null) continue;
                if (loadout.Add(item)) added++;
            }
            return added;
        }

        /// <summary>EditorPrefs·직렬화용. 쉼표 하나로 붙인다.</summary>
        public string Encode() => string.Join(",", _ids);

        public static DemoLoadoutSpec Decode(string encoded)
        {
            var spec = new DemoLoadoutSpec();
            if (string.IsNullOrWhiteSpace(encoded)) return spec;

            string[] parts = encoded.Split(',');
            for (int i = 0; i < parts.Length; i++) spec.Add(parts[i]);
            return spec;
        }

        /// <summary>사람이 읽는 한 줄. 로그와 창 머리에 같은 문자열을 쓴다.</summary>
        public string Describe()
        {
            if (_ids.Count == 0) return "(빈 적재)";

            var sb = new StringBuilder();
            for (int i = 0; i < _ids.Count; i++)
            {
                if (i > 0) sb.Append(" · ");
                BuildItem item = BuildCatalog.ById(_ids[i]);
                sb.Append(item != null ? item.Label : $"?{_ids[i]}");
            }
            return sb.ToString();
        }

        /// <summary>
        /// 한 축만 골라 담은 명세. 노션 §6.3 「내부 빌드 방향」을 **하나씩 따로**
        /// 체험하려면 이것이 출발점이다.
        ///
        /// 축은 뽑는 데만 쓰고 화면에 태그로 노출하지 않는다는 규칙
        /// (<see cref="BuildCatalog.OffersFor"/>의 주석 · 노션 §6.2)은 **플레이어용**이다.
        /// 데모 도구는 개발자가 보는 것이므로 축을 그대로 드러낸다.
        /// </summary>
        public static DemoLoadoutSpec ForAxis(BuildAxis axis, int max = BuildLoadout.MaxSlots)
        {
            var spec = new DemoLoadoutSpec();
            IReadOnlyList<BuildItem> all = BuildCatalog.All;

            for (int i = 0; i < all.Count && spec.Count < max; i++)
                if (all[i].Axis == axis) spec.Add(all[i].Id);

            return spec;
        }

        /// <summary>축마다 하나씩 — 「섞으면 무엇이 되는가」를 보는 대조군.</summary>
        public static DemoLoadoutSpec OnePerAxis(int max = BuildLoadout.MaxSlots)
        {
            var spec = new DemoLoadoutSpec();
            var used = new List<BuildAxis>();
            IReadOnlyList<BuildItem> all = BuildCatalog.All;

            for (int i = 0; i < all.Count && spec.Count < max; i++)
            {
                if (used.Contains(all[i].Axis)) continue;
                if (!spec.Add(all[i].Id)) continue;
                used.Add(all[i].Axis);
            }
            return spec;
        }

        /// <summary>품목 하나만. 「이것 하나가 무엇을 바꾸는가」를 격리해 본다.</summary>
        public static DemoLoadoutSpec Solo(string id)
        {
            var spec = new DemoLoadoutSpec();
            spec.Add(id);
            return spec;
        }

        public static readonly BuildAxis[] Axes =
        {
            BuildAxis.Stability,
            BuildAxis.Pattern,
            BuildAxis.Cascade,
            BuildAxis.Residual,
            BuildAxis.Load,
        };

        public static string AxisLabel(BuildAxis axis)
        {
            switch (axis)
            {
                case BuildAxis.Stability: return "안정 — 기본 정화를 반복한다";
                case BuildAxis.Pattern:   return "패턴 — 직선과 위치 보너스";
                case BuildAxis.Cascade:   return "연쇄 — 연결 제거와 재충전";
                case BuildAxis.Residual:  return "잔류 — 남은 저항을 재료로";
                case BuildAxis.Load:      return "적재 — 무게와 위험을 출력으로";
                default:                  return axis.ToString();
            }
        }
    }
}
