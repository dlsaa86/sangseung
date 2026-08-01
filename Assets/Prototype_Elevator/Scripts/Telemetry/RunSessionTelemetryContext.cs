using Ascend.Prototype.Run;

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
    }
}
