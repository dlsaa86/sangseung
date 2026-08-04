using System.Collections.Generic;
using UnityEngine;
using Ascend.Prototype.Risk;

namespace Ascend.Prototype.Run
{
    /// <summary>
    /// 층이 끝나는 순간을 잡아 <see cref="FloorRecord"/>를 만든다.
    ///
    /// 왜 폴링인가: `RunSession`은 층을 확정하면 곧바로 `Current`를 교체하거나 비운다.
    /// 확정 뒤에 물어보면 이미 사라지고 없다. 그래서 지금 보고 있는 층을 **직접 붙들고**
    /// 있다가 `Result`가 채워지는 프레임에 기록한다.
    ///
    /// 최고 위험 단계도 여기서 누적한다 — 층이 끝난 시점의 위험만 남기면
    /// "중간에 얼마나 위험했는가"가 사라져 실패 원인을 설명할 수 없다.
    /// </summary>
    public sealed class AccidentRecorder : MonoBehaviour
    {
        [SerializeField] private RunSessionBehaviour _run;
        [SerializeField] private RiskStateView _risk;

        [Tooltip("층이 끝날 때 전문을 콘솔에 남긴다. 재현 시드가 포함된다.")]
        [SerializeField] private bool _logFullReport = true;

        private readonly List<FloorRecord> _records = new List<FloorRecord>();
        private FloorSession _tracked;
        private RiskLevel _peakRisk = RiskLevel.Stable;
        private string _peakReason = "위험 요인 없음";

        /// <summary>
        /// 이 층에서 위험 단계가 바뀐 순간들. **최고치와 별개로 필요하다** —
        /// PRD §10 이 요구하는 것은 「위험 상태 **변화**」이고, 실패 원인을 설명할 때
        /// 「3스핀째부터 Critical 이었다」가 「최고 Critical」보다 많이 말한다.
        ///
        /// 매 프레임이 아니라 **단계가 실제로 바뀐 프레임만** 담는다. 그러지 않으면
        /// 60fps × 층 길이만큼의 쓰레기가 되고, 기록도 못 읽는다.
        /// </summary>
        private readonly List<FloorRecord.RiskTransition> _riskTransitions =
            new List<FloorRecord.RiskTransition>();

        /// <summary>직전 프레임의 단계. 전이 검출의 기준점이다.</summary>
        private RiskLevel _lastRisk = RiskLevel.Stable;

        /// <summary>지금까지 기록된 층. 마지막 원소가 가장 최근이다.</summary>
        public IReadOnlyList<FloorRecord> Records => _records;

        /// <summary>가장 최근 기록. 없으면 null.</summary>
        public FloorRecord Latest => _records.Count > 0 ? _records[_records.Count - 1] : null;

        private void Awake()
        {
            if (_run == null) _run = GetComponent<RunSessionBehaviour>();
            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();
            if (_risk == null) _risk = FindAnyObjectByType<RiskStateView>();
            if (_run != null) _run.RunStarted += OnRunStarted;
        }

        private void OnDestroy()
        {
            if (_run != null) _run.RunStarted -= OnRunStarted;
        }

        private void OnRunStarted(RunSession session)
        {
            _records.Clear();
            _tracked = null;
            ResetPeak();
        }

        private void LateUpdate()
        {
            RunSession session = _run != null ? _run.Session : null;
            if (session == null) return;

            // 붙들고 있는 층이 확정됐으면 기록한다. Current 가 이미 다른 층으로
            // 넘어간 뒤여도 참조를 들고 있으므로 늦지 않는다.
            if (_tracked != null && _tracked.Result != null)
            {
                // **실패한 층의 위험도를 기록에 넣는다.**
                //
                // 이전에는 `TrackPeakRisk()` 가 이 메서드의 **맨 마지막 줄**이라,
                // 층이 확정되는 프레임의 위험도가 기록에 한 번도 들어가지 않았다.
                // 그래서 사고 기록기에 「위험 최고 **안정**(위험 요인 없음)」 바로 아래
                // 「원인: 추락」이 찍혔다 — 독립 시각 평가가 자기모순으로 두 번 지적한 것이다.
                //
                // 여기서 `TrackPeakRisk()` 를 한 번 더 부르는 것으로는 부족하다.
                // `RiskStateView.LateUpdate` 가 이 컴포넌트보다 **뒤에** 돌면 같은 프레임에서
                // 아직 Collapse 가 아니고, 스크립트 실행 순서는 씬 설정에 달렸다.
                // 그래서 순서에 기대지 않고 **결과에서 직접 유도한다** — 층이 실패했다면
                // 그 자체가 Collapse 다(`RiskEvaluator` 도 「층 실패는 점수와 무관하게
                // Collapse」로 같은 규칙을 쓴다).
                TrackPeakRisk();
                RiskLevel recordedRisk = _peakRisk;
                string recordedReason = _peakReason;
                if (!_tracked.Result.Succeeded && recordedRisk < RiskLevel.Collapse)
                {
                    recordedRisk = RiskLevel.Collapse;
                    if (string.IsNullOrEmpty(recordedReason) || recordedReason == "위험 요인 없음")
                        recordedReason = "층 실패";
                }

                // 결과에서 유도한 Collapse 는 전이 목록에도 들어가야 한다.
                // 요약에는 「최고 붕괴」라고 적혀 있는데 변화 목록에는 없으면
                // 같은 화면 안에서 두 줄이 서로를 부정한다 — 이 파일이 이미
                // 한 번 겪은 자기모순이다(위 주석 참조).
                if (recordedRisk == RiskLevel.Collapse && _lastRisk != RiskLevel.Collapse)
                    _riskTransitions.Add(new FloorRecord.RiskTransition(
                        _tracked.SpinsUsed, RiskLevel.Collapse, recordedReason));

                _records.Add(FloorRecord.Capture(session.Seed, _tracked, _tracked.Result,
                                                 recordedRisk, recordedReason, session.LastJettison,
                                                 _riskTransitions));
                if (_logFullReport) Debug.Log($"[상승]\n{_records[_records.Count - 1].FullReport()}");
                _tracked = null;
                ResetPeak();
            }

            FloorSession current = session.Current;
            if (!ReferenceEquals(current, _tracked) && current != null && current.Result == null)
            {
                _tracked = current;
                ResetPeak();
            }

            TrackPeakRisk();
        }

        private void TrackPeakRisk()
        {
            if (_risk == null || _tracked == null) return;

            // 전이는 **오르내림 둘 다** 잡는다. 내려가는 것도 정보다 —
            // 「Critical 까지 갔다가 정화로 Strain 으로 내려왔다」는 그 층의 이야기다.
            if (_risk.Level != _lastRisk)
            {
                _riskTransitions.Add(new FloorRecord.RiskTransition(
                    _tracked.SpinsUsed, _risk.Level, _risk.Reason));
                _lastRisk = _risk.Level;
            }

            if (_risk.Level > _peakRisk)
            {
                _peakRisk = _risk.Level;
                _peakReason = _risk.Reason;
            }
        }

        private void ResetPeak()
        {
            _peakRisk = RiskLevel.Stable;
            _peakReason = "위험 요인 없음";
            _riskTransitions.Clear();
            // 층이 바뀌면 기준점도 돌아간다. 안 그러면 새 층의 첫 전이가
            // 「이미 그 단계였다」로 읽혀 기록에서 사라진다.
            _lastRisk = RiskLevel.Stable;
        }
    }
}
