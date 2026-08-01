using System;
using System.Collections.Generic;
using Ascend.Prototype.Events;

namespace Ascend.Prototype.Npc
{
    /// <summary>
    /// 사건 버스와 중재기를 잇는 얇은 어댑터. 하는 일은 하나다 —
    /// <see cref="PassengerReactionEvents.TryMap"/>로 걸러 <see cref="PassengerReactionDirector.Notify"/>에 넘긴다.
    ///
    /// 왜 따로 있는가: 중재 규칙(누가 반응하나)과 배선(무엇이 반응을 부르나)은 서로 다른
    /// 이유로 바뀐다. 중재기가 버스를 직접 구독하면 규칙을 테스트하려고 버스를 만들어야
    /// 하고, 버스를 갈아 끼우면 규칙 테스트가 같이 깨진다.
    ///
    /// MonoBehaviour가 아닌 이유: 이 프로토타입에서 사건 흐름은 `RunSession`(순수 C#)이
    /// 소유한다. 라우터가 씬 오브젝트면 헤드리스 시뮬레이터가 승객 반응을 못 본다.
    /// 시간을 <see cref="Func{T}"/>로 받는 것도 같은 이유다 — <c>Time.time</c>을 직접
    /// 읽으면 테스트가 시간을 조작할 수 없어 쿨다운을 검증할 방법이 사라진다.
    /// </summary>
    public sealed class PassengerReactionRouter
    {
        private readonly PassengerReactionDirector _director;
        private readonly Func<float> _clock;
        private GameEventBus _bus;

        /// <summary>
        /// 반응이 실제로 시작될 때마다 불린다. 승객 자세를 그리는 쪽(UP-NPC-04)과
        /// 오디오가 여기에 붙는다. 인자로 오는 목록은 중재기의 내부 버퍼이므로
        /// **핸들러 안에서만** 유효하다.
        /// </summary>
        public event Action<PassengerReactionEvent, IReadOnlyList<int>> Reacted;

        /// <summary>버스에서 받아 반응으로 옮긴 사건 수. UP-NPC-02의 검증 증거다.</summary>
        public int RoutedCount { get; private set; }

        /// <summary>마지막으로 옮긴 반응. 디버그 패널이 "방금 무엇에 반응했나"를 띄울 때 쓴다.</summary>
        public PassengerReactionEvent LastRouted { get; private set; }

        public PassengerReactionRouter(PassengerReactionDirector director, Func<float> clock)
        {
            if (director == null)
                throw new ArgumentNullException(nameof(director),
                    "중재기 없이 라우터만 살아 있으면 사건은 흐르는데 아무도 반응하지 않는다.");

            _director = director;
            if (clock != null)
            {
                _clock = clock;
            }
            else
            {
                _clock = DefaultClock;
                UnityEngine.Debug.LogWarning(
                    "[상승] PassengerReactionRouter 에 시계가 없다 — Time.time 으로 폴백한다. " +
                    "헤드리스 시뮬레이터에서는 시간이 흐르지 않아 쿨다운이 영원히 걸린다.");
            }
        }

        private static float DefaultClock() => UnityEngine.Time.time;

        public bool IsAttached => _bus != null;
        public PassengerReactionDirector Director => _director;

        /// <summary>
        /// 버스를 구독한다. 이미 붙어 있으면 먼저 뗀다 — 두 번 구독하면 한 사건이
        /// 두 번 들어와 동시 반응 한도가 한 번에 소진된다.
        /// </summary>
        public void Attach(GameEventBus bus)
        {
            if (bus == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[상승] PassengerReactionRouter.Attach 에 null 버스가 왔다 — 승객이 아무것에도 반응하지 않는다. " +
                    "RunSession 이 아직 시작되지 않았을 가능성이 높다.");
                return;
            }

            if (ReferenceEquals(_bus, bus)) return;
            Detach();
            _bus = bus;
            _bus.Published += OnPublished;
        }

        /// <summary>구독을 끊는다. 런이 끝나거나 다시 시작할 때 반드시 부른다.</summary>
        public void Detach()
        {
            if (_bus == null) return;
            _bus.Published -= OnPublished;
            _bus = null;
        }

        /// <summary>만료 정리를 중재기에 전달한다. 뷰가 매 프레임 부른다.</summary>
        public void Tick() => _director.Tick(_clock());

        private void OnPublished(GameEvent e)
        {
            PassengerReactionEvent reaction;
            if (!PassengerReactionEvents.TryMap(in e, out reaction)) return;

            RoutedCount++;
            LastRouted = reaction;

            IReadOnlyList<int> reacting = _director.Notify(reaction, _clock());
            if (reacting.Count == 0) return;

            Action<PassengerReactionEvent, IReadOnlyList<int>> handler = Reacted;
            if (handler != null) handler(reaction, reacting);
        }
    }
}

// 씬 배선 필요:
// `RunSession`(또는 `RunSessionBehaviour`)이 살아난 뒤 다음을 잇는 MonoBehaviour 가 필요하다.
//   var director = new PassengerReactionDirector(적재된 승객 수, 2, set.For);
//   var router   = new PassengerReactionRouter(director, () => Time.time);
//   router.Attach(run.Session.Events);          // 런이 끝나면 router.Detach()
//   매 프레임 router.Tick();
// 승객 수는 `RunSession.Loadout` 에서 `BuildItemKind.Passenger` 개수를 세면 된다
// (`BuildFigureView` 가 이미 `_carIsPassenger` 로 같은 것을 세고 있다).
// 승객 수가 바뀌면 중재기를 새로 만든다 — 슬롯 배열이 생성 시점에 고정된다.
