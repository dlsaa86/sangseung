using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ascend.Prototype.Build;
using Ascend.Prototype.Run;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.Telemetry
{
    /// <summary>
    /// 진행 중인 층에게 직접 물어 요구 전력·무게·과적을 가져온다.
    ///
    /// 사건만으로 이 셋을 정확히 얻을 수 없는 두 지점이 있다.
    ///   1) 1층 — `RunSession` 생성자 안에서 `FloorStarted`가 발행되므로 구독자가 없다.
    ///   2) 적재 층 — 무엇을 실으면 요구 전력이 다시 계산되는데(`FloorSession.RecomputeLoad`)
    ///      `BoardingFinished`는 무게만 싣고 요구 전력을 싣지 않는다.
    ///
    /// 사건 계약을 늘려 해결하지 않는 이유: `GameEventKind`는 값이 고정된 공용 목록이고
    /// 텔레메트리 하나 때문에 사운드·승객 반응이 함께 읽는 계약을 바꾸는 것은 대가가 크다.
    /// **읽기 전용**으로 층을 들여다보는 이 어댑터가 더 좁은 변경이다.
    ///
    /// 층을 소유하지 않는다. `RunSession.Current`는 층이 확정되면 곧바로 교체되므로
    /// 참조를 캐시하지 않고 매번 다시 묻는다.
    /// </summary>
    public sealed class RunSessionTelemetryContext : ITelemetryFloorContext
    {
        private readonly RunSession _run;

        public RunSessionTelemetryContext(RunSession run)
        {
            _run = run;
        }

        public bool TryGetFloorContext(out float requiredPower, out float carriedWeight, out bool overloaded)
        {
            requiredPower = 0f;
            carriedWeight = 0f;
            overloaded = false;

            FloorSession floor = _run != null ? _run.Current : null;
            if (floor == null) return false;

            requiredPower = floor.RequiredPower;
            // 층이 든 무게를 쓴다. `RunSession.CarriedWeight`도 같은 값이지만, 층이 확정된
            // 뒤에는 하차와 화물 포기로 런 쪽이 먼저 움직인다 — 기록은 그 스핀이 실제로
            // 지고 있던 무게여야 한다(`FloorSession.Capacity` 주석과 같은 이유).
            carriedWeight = floor.CarriedWeight;
            overloaded = floor.IsOverloaded;
            return true;
        }

        /// <summary>
        /// 지금 실려 있는 승객·부품과 그것들이 규칙에 거는 효과.
        ///
        /// 층의 적재를 먼저 보는 이유는 <see cref="TryGetFloorContext"/>와 같다 — 층이
        /// 확정된 뒤에는 하차와 화물 포기로 런 쪽이 먼저 움직이므로, 기록은 그 스핀이
        /// 실제로 지고 있던 것이어야 한다. 층이 없으면 런의 적재로 물러난다.
        /// </summary>
        public bool TryGetLoadoutSummary(out string summary)
        {
            summary = null;

            FloorSession floor = _run != null ? _run.Current : null;
            BuildLoadout loadout = floor != null ? floor.Loadout : null;
            if (loadout == null && _run != null) loadout = _run.Loadout;
            if (loadout == null) return false;

            summary = Describe(loadout);
            return true;
        }

        /// <summary>
        /// `SpinTelemetryRecord.Loadout` 형식으로 접는다.
        ///
        /// `BuildLoadout.DescribeShort()`를 쓰지 않는 이유: 저쪽은 사람이 읽는 한국어
        /// 라벨만 이어 붙이고 **무엇을 발동시켰는지가 빠져 있다.** §16.2가 요구하는 것은
        /// "승객·부품 발동"이지 "탑승자 명단"이 아니다. 라벨 대신 아이디를 쓰는 이유는
        /// `SpinTelemetryRecord.BestPattern`과 같다 — 표시 문구가 바뀌어도 로그는 그대로여야 한다.
        /// </summary>
        private static string Describe(BuildLoadout loadout)
        {
            IReadOnlyList<BuildItem> items = loadout.Items;
            if (items == null || items.Count == 0) return SpinTelemetryRecord.NoneMarker;

            var sb = new StringBuilder(96);
            for (int i = 0; i < items.Count; i++)
            {
                BuildItem item = items[i];
                if (item == null) continue;

                if (sb.Length > 0) sb.Append(SpinTelemetryRecord.ListSeparator);
                sb.Append(string.IsNullOrEmpty(item.Id) ? "?" : item.Id);

                BuildEffect[] effects = item.Effects;
                if (effects == null || effects.Length == 0) continue;

                sb.Append('[');
                for (int e = 0; e < effects.Length; e++)
                {
                    if (e > 0) sb.Append('+');
                    sb.Append(effects[e].Kind.ToString());
                    if (effects[e].Target != SymbolKind.Empty)
                        sb.Append('@').Append(effects[e].Target.ToString());
                    sb.Append('=').Append(effects[e].Amount.ToString("0.####", CultureInfo.InvariantCulture));
                }
                sb.Append(']');
            }

            return sb.Length == 0 ? SpinTelemetryRecord.NoneMarker : sb.ToString();
        }
    }
}
