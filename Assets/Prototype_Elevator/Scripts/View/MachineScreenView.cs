using System.Text;
using TMPro;
using UnityEngine;
using Ascend.Prototype.Events;
using Ascend.Prototype.Run;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.View
{
    /// <summary>
    /// 3×3 아래 가로 화면을 **이번 스핀의 결과**로 구동한다.
    ///
    /// ## 왜 새로 만드는가
    ///
    /// 이 자리에는 `SM_Gauge_Screen` 이라는 판이 있었는데 `MeshFilter` + `MeshRenderer`
    /// 뿐이었다 — 「POWER GAINED +240 / CLUSTER ×2.5 / CASCADE 3」이 **구운 텍스처**였다.
    /// 값이 영원히 +240 이라 사용자가 「더미로 고정되어 보임」이라고 지적했고, 맞다.
    ///
    /// ## 왜 폴링이 아니라 사건인가
    ///
    /// 「이번 스핀으로 얻은 전력」은 **스핀 경계에만 존재하는 값**이다. `FloorSession`
    /// 은 누적 `Power` 만 들고 있어서 매 프레임 물어봐도 「방금 얼마 벌었나」는 나오지
    /// 않는다. `SpinResolved` 가 `NetPower` 와 `ChainDepth` 를 실어 보내므로 그것을 받는다.
    ///
    /// ⚠ **연출 중에는 값을 미리 쓰지 않는다.** `AscentColumnView` 와 같은 이유다 —
    ///   결과 숫자가 연쇄 연출보다 앞서 나오면 공개를 스포일한다. 다만 여기서는
    ///   `SpinResolved` 자체가 연출 시작점이라, 연출자가 있으면 그쪽이 끝날 때까지
    ///   **직전 값을 유지**한다.
    /// </summary>
    public sealed class MachineScreenView : MonoBehaviour
    {
        [Tooltip("런 세션. 비우면 씬에서 찾는다.")]
        [SerializeField] private RunSessionBehaviour _run;

        [Tooltip("큰 숫자 — 이번 스핀으로 얻은 전력.")]
        [SerializeField] private TextMeshPro _gainText;

        [Tooltip("보조 줄 — 연쇄 깊이와 누적.")]
        [SerializeField] private TextMeshPro _detailText;

        /// <summary>층 이동이 깎는 쪽. 「누적」은 이 잔액이어야 한다 —
        /// 쓰지 않는 지갑의 잔액을 보여주면 이동해도 안 줄어든다.</summary>
        [SerializeField] private Ascend.Prototype.Run.RoundSandbox _round;

        private GameEventBus _bus;
        private readonly StringBuilder _text = new StringBuilder(48);
        private bool _hasSpin;

        private void Awake()
        {
            if (_run == null) _run = FindFirstObjectByType<RunSessionBehaviour>();
            if (_round == null) _round = FindFirstObjectByType<Ascend.Prototype.Run.RoundSandbox>();
        }

        private void OnEnable()
        {
            if (_run != null) _run.RunStarted += OnRunStarted;
            Subscribe(_run != null && _run.Session != null ? _run.Session.Events : null);
            Clear();
        }

        private void OnDisable()
        {
            if (_run != null) _run.RunStarted -= OnRunStarted;
            Subscribe(null);
        }

        private void OnRunStarted(RunSession session)
        {
            Subscribe(session != null ? session.Events : null);
            Clear();
        }

        /// <summary>버스를 갈아끼운다. 같은 버스면 아무 일도 하지 않는다 — 중복 구독은
        /// 같은 스핀을 두 번 그린다.</summary>
        private void Subscribe(GameEventBus bus)
        {
            if (_bus == bus) return;
            if (_bus != null) _bus.Published -= OnEvent;
            _bus = bus;
            if (_bus != null) _bus.Published += OnEvent;
        }

        private void Clear()
        {
            _hasSpin = false;
            Apply(_gainText, "—");
            Apply(_detailText, "대기");
        }

        private void OnEvent(GameEvent e)
        {
            if (e.Kind == GameEventKind.FloorStarted) { Clear(); return; }
            if (e.Kind != GameEventKind.SpinResolved) return;

            // `SpinResolved` 는 intValue = 연쇄 깊이, floatValue = 순 전력으로 온다
            // (`FloorSession` 발행부). 페이로드가 있으면 더 자세히 읽는다.
            int chain = e.IntValue;
            float net = e.FloatValue;
            var res = e.Payload as SpinResolution?;
            if (res.HasValue)
            {
                chain = res.Value.ChainDepth;
                net = res.Value.NetPower;
            }

            _hasSpin = true;

            _text.Clear();
            // 부호를 붙인다 — 잔류 저항이 깎으면 **음수가 될 수 있고**, 그때 「얻었다」로
            // 읽히면 안 된다. `SpinResolution.NetPower` 주석이 명시한 경우다.
            if (net >= 0f) _text.Append('+');
            _text.AppendFormat("{0:F0}", net);
            Apply(_gainText, _text.ToString());

            _text.Clear();
            _text.Append("연쇄 ").Append(chain);
            if (_round != null)
                _text.Append("   누적 ").AppendFormat("{0:F0}", _round.Round.Power);
            else if (_run != null && _run.Session != null && _run.Session.Current != null)
                _text.Append("   누적 ").AppendFormat("{0:F0}", _run.Session.Current.Power);
            Apply(_detailText, _text.ToString());
        }

        private static void Apply(TextMeshPro target, string value)
        {
            if (target == null) return;
            if (!string.Equals(target.text, value)) target.SetText(value);
        }

        /// <summary>검증용. 스핀이 한 번이라도 그려졌는가.</summary>
        public bool HasSpin => _hasSpin;
    }
}
