using System;
using UnityEngine;
using Ascend.Prototype.Player;

namespace Ascend.Prototype.Run
{
    /// <summary>
    /// 새 코어 루프를 **손으로 만져 보기 위한 샌드박스** (2026-08-09).
    ///
    /// ## 왜 `RunSession` 에 붙이지 않았나
    ///
    /// 새 규칙(<see cref="RoundSession"/>)을 `RunSession` 에 배선하려면 「층마다 요구
    /// 전력」을 전제한 검사 88개를 함께 옮겨야 하고, 그동안 게임이 플레이 불가가 된다.
    /// 그 상태로는 규칙이 재미있는지 확인할 방법이 없다 — **규칙을 확인한 뒤에 옮기는
    /// 편이 순서상 맞다.** 그래서 이 컴포넌트는 자기 라운드를 스스로 소유한다.
    ///
    /// ⚠ **임시다.** 배선이 끝나면 이 클래스는 지운다. 그때까지 이것이 게임의 진짜
    /// 상태인 것처럼 다른 시스템이 읽어 가면 안 된다 — 읽는 쪽이 생기는 순간
    /// 「임시」가 아니게 된다.
    /// </summary>
    public sealed class RoundSandbox : MonoBehaviour
    {
        [Header("라운드 규칙")]
        [Tooltip("이번 라운드 안에 도달해야 하는 층.")]
        [SerializeField] private int _targetFloor = 10;

        [Tooltip("주어지는 스핀 수.")]
        [SerializeField, Min(1)] private int _spins = 5;

        [Tooltip("시작 층.")]
        [SerializeField] private int _startFloor = 1;

        [Header("이동 비용")]
        [Tooltip("층 하나를 오르내리는 데 드는 전력. 오르내림 대칭이다.")]
        [SerializeField, Min(1f)] private float _powerPerFloor = 60f;

        [SerializeField] private int _minFloor = 1;
        [SerializeField] private int _maxFloor = 100;

        [Header("정산")]
        [Tooltip("목표 도달 시점의 남은 스핀 하나당 지급되는 돈.")]
        [SerializeField, Min(0f)] private float _moneyPerUnusedSpin = 4f;

        [Header("샌드박스 전용")]
        [Tooltip("스핀 버튼 한 번에 들어오는 전력. 진짜 게임에서는 룰렛이 정한다.")]
        [SerializeField, Min(0f)] private float _powerPerSpin = 140f;

        [Tooltip("스핀 전력에 ±이만큼 흔들림을 준다. 0 이면 항상 같은 값.")]
        [SerializeField, Min(0f)] private float _powerJitter = 60f;

        [Header("표시")]
        [Tooltip("버튼 위 판독부. 비어 있으면 화면에 아무것도 안 나온다 — 그 사실이 보이도록 " +
                 "일부러 자동 탐색하지 않는다.")]
        [SerializeField] private TMPro.TMP_Text _readout;

        [Tooltip("LED 디스플레이(SM_Gauge_Screen)에 스핀당 획득 전력을 그릴 대상. 아직 없으면 비워 둔다.")]
        [SerializeField] private TMPro.TMP_Text _gainDisplay;

        private RoundSession _round;
        private string _lastEvent = "라운드 시작";
        private float _lastGain;

        public void BindReadout(TMPro.TMP_Text readout) => _readout = readout;

        private void LateUpdate()
        {
            if (_readout != null) _readout.text = StatusLines();
            if (_gainDisplay != null) _gainDisplay.text = _lastGain > 0f ? $"+{_lastGain:0}" : "—";
        }

        /// <summary>지금 돌고 있는 라운드. 없으면 <see cref="Restart"/> 가 만든다.</summary>
        public RoundSession Round
        {
            get
            {
                if (_round == null) Restart();
                return _round;
            }
        }

        /// <summary>마지막 사건 한 줄. 디스플레이가 이걸 그린다.</summary>
        public string LastEvent => _lastEvent;

        /// <summary>마지막 스핀으로 얻은 전력. **LED 디스플레이가 보여줄 값이다.**</summary>
        public float LastGain => _lastGain;

        private void Awake()
        {
            if (_round == null) Restart();
        }

        public void Restart()
        {
            _round = new RoundSession(
                new RoundGoal(_targetFloor, _spins, _moneyPerUnusedSpin),
                new ElevatorTravel(_powerPerFloor, _minFloor, _maxFloor),
                _startFloor);
            _lastEvent = $"라운드 시작 — 목표 {_targetFloor}층 · 스핀 {_spins}";
            _lastGain = 0f;
        }

        public bool CanSpin => !Round.IsOver && Round.SpinsRemaining > 0;

        public void Spin()
        {
            if (!CanSpin) return;
            float gain = Mathf.Max(0f, _powerPerSpin + UnityEngine.Random.Range(-_powerJitter, _powerJitter));
            gain = Mathf.Round(gain);
            _round.Spin(gain);
            _lastGain = gain;
            _lastEvent = $"스핀 +{gain:0} 전력 · 남은 스핀 {_round.SpinsRemaining}";
        }

        public bool CanMove(int delta)
        {
            if (Round.IsOver || delta == 0) return false;
            return Round.Travel.Move(Round.CurrentFloor, delta, Round.Power).Accepted;
        }

        public void Move(int delta)
        {
            if (Round.IsOver) return;
            TravelResult r = _round.Move(delta);
            if (!r.Accepted) { _lastEvent = "거절 — " + r.Rejection; return; }

            string dir = r.FloorsMoved > 0 ? "상승" : "하강";
            _lastEvent = $"{dir} {Math.Abs(r.FloorsMoved)}층 → {r.ToFloor}층 · 전력 −{r.PowerSpent:0}";
            if (_round.Outcome == RoundOutcome.Survived)
                _lastEvent += $"   ★ 도달! 남은 스핀 {_round.SpinsRemaining} → {_round.MoneyEarned:0} 골드";
        }

        public bool CanResolve => !Round.IsOver && Round.SpinsRemaining == 0;

        public void Resolve()
        {
            if (!_round.Resolve()) return;
            _lastEvent = _round.Outcome == RoundOutcome.Survived
                ? $"★ 도달 — {_round.MoneyEarned:0} 골드"
                : $"☠ 추락 — {_round.CurrentFloor}층에서 멈췄다 (목표 {_targetFloor}층)";
        }

        /// <summary>버튼 위·디스플레이에 띄우는 상태. 서식은 임시다.</summary>
        public string StatusLines()
        {
            RoundSession r = Round;
            string head = r.IsOver
                ? (r.Outcome == RoundOutcome.Survived ? "도달" : "추락")
                : $"목표 {r.Goal.TargetFloor}층";
            return $"{head}\n"
                 + $"현재 {r.CurrentFloor}층   전력 {r.Power:0}\n"
                 + $"목표까지 {r.PowerToGoal:0}   스핀 {r.SpinsRemaining}/{r.Goal.Spins}\n"
                 + $"{_lastEvent}";
        }
    }

    /// <summary>
    /// 그레이박스 버튼 하나. <see cref="RoundSandbox"/> 의 동작 하나에 대응한다.
    ///
    /// ⚠ **초안이다** — 형상·치수·배치만 잡는다(2026-08-09 사용자 결정:
    /// 「만들어야 하는 기계는 그레이박스로 초안만 만들어 둬, 모델링은 내가 직접 만들게」).
    /// </summary>
    public sealed class InteractableRoundButton : MonoBehaviour, IInteractable
    {
        public enum ButtonAction { Spin, Move, Resolve, Restart }

        [SerializeField] private RoundSandbox _sandbox;
        [SerializeField] private ButtonAction _action = ButtonAction.Move;

        [Tooltip("Move 일 때 움직일 층수. 음수면 하강.")]
        [SerializeField] private int _floorDelta = 1;

        public void Configure(RoundSandbox sandbox, ButtonAction action, int floorDelta)
        {
            _sandbox = sandbox; _action = action; _floorDelta = floorDelta;
        }

        public ButtonAction Action => _action;
        public int FloorDelta => _floorDelta;

        /// <summary>
        /// 조작에 필요한 전력. **버튼 위에 띄우는 값이 이것이다**
        /// (2026-08-09 「버튼 위에는 조작하기 위한 전력 요구량이 보일 예정」).
        /// </summary>
        public float PowerNeeded =>
            _action == ButtonAction.Move && _sandbox != null
                ? _sandbox.Round.Travel.CostFor(_floorDelta)
                : 0f;

        public string Prompt
        {
            get
            {
                if (_sandbox == null) return "(샌드박스 없음)";
                switch (_action)
                {
                    case ButtonAction.Spin:    return $"스핀 ({_sandbox.Round.SpinsRemaining} 남음)";
                    case ButtonAction.Resolve: return "라운드 종료";
                    case ButtonAction.Restart: return "다시 시작";
                    default:
                        string dir = _floorDelta > 0 ? "상승" : "하강";
                        return $"{Math.Abs(_floorDelta)}층 {dir}  (전력 {PowerNeeded:0})";
                }
            }
        }

        public bool CanInteract
        {
            get
            {
                if (_sandbox == null) return false;
                switch (_action)
                {
                    case ButtonAction.Spin:    return _sandbox.CanSpin;
                    case ButtonAction.Resolve: return _sandbox.CanResolve;
                    case ButtonAction.Restart: return true;
                    default:                   return _sandbox.CanMove(_floorDelta);
                }
            }
        }

        public void Interact(GameObject interactor)
        {
            if (_sandbox == null) return;
            switch (_action)
            {
                case ButtonAction.Spin:    _sandbox.Spin(); break;
                case ButtonAction.Resolve: _sandbox.Resolve(); break;
                case ButtonAction.Restart: _sandbox.Restart(); break;
                default:                   _sandbox.Move(_floorDelta); break;
            }
        }
    }
}
