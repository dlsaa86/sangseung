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

        /// <summary>
        /// 본선 런. **꽂으면 가짜 전력을 쓰지 않고 실제 스핀 결과를 받는다**
        /// (2026-08-09 사용자 결정: 「RoundSession 을 본선에 연결한다」).
        ///
        /// 이 척추는 목표층·이동 비용·최대 이동층을 이미 갖고 있고 검사로 잠겨 있었지만,
        /// **전력을 스스로 지어내고 있었다**(`_powerPerSpin` + `_powerJitter`).
        /// 그래서 규칙은 옳은데 게임과 무관했다. 여기가 그 유일한 이음매다.
        /// </summary>
        [Header("본선 연결")]
        [Tooltip("비우면 예전처럼 가짜 전력으로 도는 샌드박스가 된다.")]
        [SerializeField] private RunSessionBehaviour _run;

        private Events.GameEventBus _bus;

        /// <summary>플레이어가 ▲▼ 로 고른 층. 확인을 누르기 전까지는 이동하지 않는다.</summary>
        public int SelectedFloor { get; private set; }

        public void BindReadout(TMPro.TMP_Text readout) => _readout = readout;

        private void OnEnable()
        {
            if (_run == null) _run = FindFirstObjectByType<RunSessionBehaviour>();
            if (_run != null) _run.RunStarted += OnRunStarted;
            Subscribe(_run != null && _run.Session != null ? _run.Session.Events : null);
        }

        private void OnDisable()
        {
            if (_run != null) _run.RunStarted -= OnRunStarted;
            Subscribe(null);
        }

        private void OnRunStarted(RunSession session)
        {
            Subscribe(session != null ? session.Events : null);
            Restart();
        }

        private void Subscribe(Events.GameEventBus bus)
        {
            if (_bus == bus) return;
            if (_bus != null) _bus.Published -= OnGameEvent;
            _bus = bus;
            if (_bus != null) _bus.Published += OnGameEvent;
        }

        /// <summary>
        /// 실제 스핀이 끝날 때마다 그 **순 전력**을 라운드에 넣는다.
        ///
        /// ⚠ `NetPower` 는 음수가 될 수 있다(잔류 저항). `RoundSession.Spin` 은 그대로
        ///   받아야 한다 — 여기서 0 으로 깎으면 잔류 저항이 화면에서만 사라진다.
        /// </summary>
        private void OnGameEvent(Events.GameEvent e)
        {
            if (e.Kind != Events.GameEventKind.SpinResolved) return;
            if (Round.IsOver || Round.SpinsRemaining <= 0) return;
            _round.Spin(e.FloatValue);
            _lastGain = e.FloatValue;
            _lastEvent = $"스핀 {(e.FloatValue >= 0 ? "+" : "")}{e.FloatValue:0} 전력 · 남은 스핀 {_round.SpinsRemaining}";
            ClampSelection();
        }

        /// <summary>본선에 붙어 있으면 전력을 스스로 짓지 않는다.</summary>
        public bool DrivenByRun => _run != null;

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
            SelectedFloor = _round.CurrentFloor;
        }

        public bool CanSpin => !Round.IsOver && Round.SpinsRemaining > 0;

        public void Spin()
        {
            // ⚠ 본선에 붙어 있으면 **여기서 전력을 짓지 않는다.** 실제 스핀이
            //   `SpinResolved` 로 넣어 준다. 둘 다 넣으면 한 번 돌리고 두 번 번다.
            if (DrivenByRun) return;
            if (!CanSpin) return;
            float gain = Mathf.Max(0f, _powerPerSpin + UnityEngine.Random.Range(-_powerJitter, _powerJitter));
            gain = Mathf.Round(gain);
            _round.Spin(gain);
            _lastGain = gain;
            _lastEvent = $"스핀 +{gain:0} 전력 · 남은 스핀 {_round.SpinsRemaining}";
            ClampSelection();
        }

        // ── 층 선택 (▲ ▼ 확인) ──────────────────────────────────────────────
        //
        // 사용자 명세 (2026-08-09): 「버튼은 올라가기·내려가기·확인 3종류. 층버튼을
        // 누를 때마다 목표층수(선택한 층수)가 표시되고, 확인을 누르면 해당 층으로 이동.
        // 갈 수 있는 최대 층수는 스테이지별로 상이하다.」
        //
        // 즉 ▲▼ 는 **이동이 아니라 선택**이다. 누를 때마다 움직이면 되돌릴 수 없는
        // 조작이 연타로 일어나고, 전력이 한 층씩 조용히 빠진다.

        /// <summary>지금 전력과 스테이지 상한 안에서 갈 수 있는 가장 높은 층.</summary>
        public int HighestReachable => Mathf.Min(Round.Travel.MaxFloor,
                                                 Round.CurrentFloor + Round.MaxFloorsNow);

        /// <summary>내려갈 수 있는 가장 낮은 층. 하강도 전력을 쓴다.</summary>
        public int LowestReachable => Mathf.Max(Round.Travel.MinFloor,
                                                Round.CurrentFloor - Round.MaxFloorsNow);

        /// <summary>
        /// 「몇 층 **이동하는가**」를 부호와 함께 한 토큰으로 만든다.
        ///
        /// 사용자 지시(2026-08-09): 「현재 층버튼의 위아래를 누르면 **몇 층 이동하는지**
        /// 알기 쉽게 표현되어야 함」. 직전 판본은 **도착 층수**만 보여 줬는데,
        /// 도착 층에서 현재 층을 빼는 계산을 플레이어에게 시키는 셈이었다 —
        /// 층이 7734 까지 가는 게임에서 그 뺄셈은 매번 부담이다.
        ///
        /// 도착 층을 지우지 않고 **함께** 쓴다. 둘은 다른 질문에 답한다 —
        /// 도착 층은 「어디로 가나」, 이동량은 「얼마나 가나」다.
        /// </summary>
        public static string DeltaToken(int delta)
            => delta == 0 ? "그대로"
             : delta > 0 ? $"▲{delta}층"
             : $"▼{-delta}층";

        public void AdjustSelection(int delta)
        {
            if (Round.IsOver) return;
            SelectedFloor = Mathf.Clamp(SelectedFloor + delta, LowestReachable, HighestReachable);
            int moved = SelectedFloor - Round.CurrentFloor;
            int need = Mathf.RoundToInt(Round.Travel.CostFor(moved));
            _lastEvent = moved == 0
                ? "선택 — 현재 층"
                : $"선택 {SelectedFloor}층  {DeltaToken(moved)} 이동 · 필요 전력 {need}";
        }

        /// <summary>전력이 모자라거나 층이 바뀌면 선택을 유효 범위로 되돌린다.</summary>
        private void ClampSelection()
        {
            SelectedFloor = Mathf.Clamp(SelectedFloor, LowestReachable, HighestReachable);
        }

        public bool CanConfirm => !Round.IsOver && SelectedFloor != Round.CurrentFloor
                                  && CanMove(SelectedFloor - Round.CurrentFloor);

        /// <summary>확인 — 고른 층으로 실제로 이동한다.</summary>
        public void Confirm()
        {
            if (Round.IsOver) return;
            int delta = SelectedFloor - Round.CurrentFloor;
            if (delta == 0) { _lastEvent = "현재 층이다"; return; }
            Move(delta);
            SelectedFloor = Round.CurrentFloor;
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
            // 이동하면 전력이 줄어 갈 수 있는 범위가 좁아진다. 선택이 범위 밖에
            // 남아 있으면 확인 버튼이 영원히 죽은 채로 보인다.
            ClampSelection();
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
            int moved = SelectedFloor - r.CurrentFloor;
            int need = Mathf.RoundToInt(r.Travel.CostFor(moved));
            string sel = moved == 0
                ? "선택 —"
                : $"선택 {SelectedFloor}층  {DeltaToken(moved)} 이동 (필요 {need})";
            // **선택 줄이 맨 위다.** 직전 판본은 세 번째 줄이었는데, ▲▼ 를 연타하는
            // 동안 눈이 가장 먼저 닿는 자리에 「목표 10층 최대 10층」이라는 **안 바뀌는
            // 값**이 앉아 있었다. 바뀌는 것이 위로 온다.
            // 다섯 줄에서 네 줄로 줄였다. 콘솔 판은 0.36m 폭이라 다섯 줄이 들어가려면
            // 글자가 판독 한계 아래로 내려간다 — 정보를 다 띄우려다 하나도 못 읽는 판이 된다.
            return $"{sel}\n"
                 + $"현재 {r.CurrentFloor}층 · 전력 {r.Power:0}   갈 수 있는 곳 {LowestReachable}~{HighestReachable}층\n"
                 + $"{head} · 목표까지 {r.PowerToGoal:0} · 스핀 {r.SpinsRemaining}/{r.Goal.Spins}\n"
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
        /// <summary>
        /// ⚠ `Move` 는 **누르는 즉시 이동한다.** 사용자 명세는 그게 아니라
        ///   「▲▼ 로 고르고 **확인**을 눌러야 이동」이므로 `Select` 와 `Confirm` 을
        ///   추가했다. `Move` 는 샌드박스 단독 시험용으로 남긴다 —
        ///   지우면 씬에 남아 있는 옛 버튼이 MISSING 이 된다.
        /// </summary>
        public enum ButtonAction { Spin, Move, Resolve, Restart, Select, Confirm }

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
                    case ButtonAction.Select:
                    {
                        // 사용자 지시: 「위 또는 아래를 누를 때마다 **이동하려는 층수**가
                        // 표기되고 **요구되는 전력량**을 보여주면 된다」.
                        // 그래서 다음에 선택될 층과, 현재 층에서 거기까지의 비용을 함께 쓴다 —
                        // 한 칸 값이 아니라 **총액**이어야 「지금 갈 수 있나」가 바로 읽힌다.
                        int next = _sandbox.SelectedFloor + _floorDelta;
                        int moved = next - _sandbox.Round.CurrentFloor;
                        int cost = Mathf.RoundToInt(_sandbox.Round.Travel.CostFor(moved));
                        // 「몇 층 이동하는가」를 도착 층보다 **앞에** 둔다 — 버튼을 연타할 때
                        // 눈이 먼저 닿는 자리가 변화량이어야 한다.
                        return $"{(_floorDelta > 0 ? "올라가기" : "내려가기")}  " +
                               $"{RoundSandbox.DeltaToken(moved)} 이동  →  {next}층   전력 {cost}";
                    }
                    case ButtonAction.Confirm:
                    {
                        int d = _sandbox.SelectedFloor - _sandbox.Round.CurrentFloor;
                        if (d == 0) return "확인 — 층을 고른다";
                        return $"확인 — {RoundSandbox.DeltaToken(d)} 이동 " +
                               $"({_sandbox.Round.CurrentFloor}층 → {_sandbox.SelectedFloor}층 · " +
                               $"전력 {_sandbox.Round.Travel.CostFor(d):0})";
                    }
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
                    // 선택은 **범위 안에서만** 눌린다. 끝에 닿으면 죽어야
                    // 「더 못 간다」가 손끝에 전해진다.
                    case ButtonAction.Select:
                        return !_sandbox.Round.IsOver &&
                               _sandbox.SelectedFloor + _floorDelta >= _sandbox.LowestReachable &&
                               _sandbox.SelectedFloor + _floorDelta <= _sandbox.HighestReachable;
                    case ButtonAction.Confirm: return _sandbox.CanConfirm;
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
                case ButtonAction.Select:  _sandbox.AdjustSelection(_floorDelta); break;
                case ButtonAction.Confirm: _sandbox.Confirm(); break;
                default:                   _sandbox.Move(_floorDelta); break;
            }
            Press();
        }

        // ── 누름 연출 ────────────────────────────────────────────────────────
        //
        // 캡이 실제로 **들어갔다 나온다.** 없으면 「눌렀다」는 사실이 화면 어디에도
        // 남지 않는다 — 판독창의 숫자가 바뀌긴 하지만 그건 결과지 입력 확인이 아니고,
        // 값이 안 바뀌는 경우(끝 층에서 ▲)에는 아무 반응도 없어 **버튼이 죽은 것처럼
        // 보인다.** 실행 레버가 `_handleAmount` 로 같은 일을 하는 이유와 같다.
        //
        // 코루틴을 쓰지 않는다. 이 저장소는 GC 를 134KB → 3.8KB 로 끌어내린 적이 있고
        // 버튼마다 코루틴을 돌리면 누를 때마다 힙이 붙는다. 상태 하나와 `Update` 로 족하다.

        [Tooltip("캡이 들어가는 깊이(m). 모델 캡 두께가 38mm 라 6mm 면 눈에 보이고 안 뚫린다.")]
        [SerializeField, Range(0f, 0.02f)] private float _pressDepth = 0.006f;

        [Tooltip("들어갔다 돌아오는 데 걸리는 시간(초).")]
        [SerializeField, Range(0.02f, 0.6f)] private float _pressReturn = 0.14f;

        /// <summary>1 = 완전히 눌림, 0 = 원위치. `Update` 가 0 으로 되돌린다.</summary>
        private float _press;

        /// <summary>원위치. **`Awake` 에서 잡는다** — 눌린 순간에 잡으면 눌린 자리가 원점이 된다.</summary>
        private Vector3 _restLocal;
        private bool _restCaptured;

        /// <summary>캡이 들어가는 방향(로컬). 셀·패널이 회전해 있어 월드 −Z 를 로컬로 바꿔 둔다.</summary>
        private Vector3 _pressAxisLocal = Vector3.forward;

        private void Awake()
        {
            _restLocal = transform.localPosition;
            _restCaptured = true;
            // 「누르는 방향」은 **패널 안쪽**이다. 부모가 돌아 있을 수 있으므로
            // 월드 +Z(캐빈 안쪽에서 벽 쪽)를 로컬로 환산한다 — 감으로 축을 고르면
            // 캡이 옆으로 미끄러진다(이 저장소가 발광판에서 이미 한 번 겪었다).
            Transform p = transform.parent;
            Vector3 worldIn = Vector3.forward;
            _pressAxisLocal = p != null
                ? p.InverseTransformDirection(worldIn).normalized
                : worldIn;
        }

        /// <summary>검증용. 지금 눌린 정도 0~1.</summary>
        public float PressAmount => _press;

        private void Press() => _press = 1f;

        private void Update()
        {
            if (!_restCaptured || _press <= 0f) return;
            _press = Mathf.Max(0f, _press - Time.deltaTime / Mathf.Max(0.02f, _pressReturn));
            transform.localPosition = _restLocal + _pressAxisLocal * (_pressDepth * Smooth(_press));
            if (_press <= 0f) transform.localPosition = _restLocal;
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
