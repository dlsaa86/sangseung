using System;
using System.Collections.Generic;

namespace Ascend.Prototype.Npc
{
    /// <summary>
    /// 누가 반응할지 정하는 중재기(UP-NPC-05). `MASTER_PRD.md` §9.4
    /// 「한 이벤트에서 모든 승객이 동시에 말하지 않는다」가 이 클래스의 존재 이유 전부다.
    ///
    /// 왜 순수 C#인가: 중재 규칙이 틀리면 승객 여섯이 한꺼번에 소리를 지르는데, 그건
    /// 자세 코드·오디오 코드·이벤트 배선 어디서든 나올 수 있는 그림이다. 씬 없이
    /// 돌려서 규칙만 따로 검증할 수 있어야 원인이 갈린다(`TECH_SPEC.md` §2).
    ///
    /// **무작위를 쓰지 않는다.** 누가 반응하는지를 `Random`으로 고르면 같은 시드·같은
    /// 상태의 고정 캡처가 매번 달라져 §11의 "동일 조건 캡처"가 성립하지 않는다.
    /// `BuildFigureView.ReactToRisk`가 사인파를 쓰는 것과 같은 이유다. 여기서는
    /// 라운드 로빈 — 순서가 정해져 있으면서도 같은 사람만 계속 반응하지 않는다.
    /// </summary>
    public sealed class PassengerReactionDirector
    {
        private struct Slot
        {
            public bool Active;
            public PassengerReactionEvent Event;
            public PassengerReaction Reaction;

            /// <summary>이 시각이 지나면 반응이 끝난다.</summary>
            public float EndsAt;

            /// <summary>이 시각까지는 새 반응을 받지 않는다. 반응 **시작** 시점부터 잰다.</summary>
            public float CooldownUntil;
        }

        private readonly Slot[] _slots;
        private readonly Func<PassengerReactionEvent, PassengerReaction> _lookup;
        private readonly List<int> _selected = new List<int>(4);
        private readonly int _maxConcurrent;

        /// <summary>라운드 로빈 커서. 다음 탐색이 시작되는 승객 인덱스다.</summary>
        private int _cursor;

        private bool _warnedMissingLookup;

        /// <param name="passengerCount">승객 수. 0 이하면 이 중재기는 아무것도 하지 않는다.</param>
        /// <param name="maxConcurrent">동시에 반응할 수 있는 최대 인원. §9.4의 기본값은 2다.</param>
        /// <param name="lookup">
        /// 사건 → 반응 정의. 보통 <see cref="PassengerReactionSet.For"/>를 넘긴다.
        /// null이면 <see cref="PassengerReactionSet.DefaultFor"/>로 폴백하되 한 번 경고한다 —
        /// 조용히 기본값으로 도는 것과 데이터가 연결된 것을 화면에서 구분할 수 없기 때문이다.
        /// </param>
        public PassengerReactionDirector(int passengerCount, int maxConcurrent,
                                         Func<PassengerReactionEvent, PassengerReaction> lookup)
        {
            int count = passengerCount > 0 ? passengerCount : 0;
            _slots = new Slot[count];
            // 음수는 0으로 접는다. 0은 "아무도 반응하지 않는다"는 유효한 설정이다 —
            // 반응 전체를 끄고 다른 채널만 보고 싶을 때 쓴다.
            _maxConcurrent = maxConcurrent > 0 ? maxConcurrent : 0;
            _lookup = lookup;
        }

        public int PassengerCount => _slots.Length;
        public int MaxConcurrent => _maxConcurrent;

        /// <summary>지금 반응 중인 승객 수. 항상 <see cref="MaxConcurrent"/> 이하다.</summary>
        public int ActiveCount
        {
            get
            {
                int active = 0;
                for (int i = 0; i < _slots.Length; i++) if (_slots[i].Active) active++;
                return active;
            }
        }

        /// <summary>지금까지 실제로 시작된 반응 수. UP-NPC-02의 "반응 발동 로그"가 세는 값이다.</summary>
        public int StartedCount { get; private set; }

        /// <summary>동시 반응 한도나 쿨다운 때문에 버려진 반응 수. 한도가 너무 낮은지 판단하는 근거다.</summary>
        public int SuppressedCount { get; private set; }

        public bool IsReacting(int passenger) =>
            passenger >= 0 && passenger < _slots.Length && _slots[passenger].Active;

        /// <summary>지금 이 승객이 하고 있는 반응. 반응 중이 아니면 기본값(<c>Duration = 0</c>).</summary>
        public PassengerReaction CurrentOf(int passenger) =>
            IsReacting(passenger) ? _slots[passenger].Reaction : default(PassengerReaction);

        /// <summary>지금 이 승객을 움직이고 있는 사건. 반응 중이 아니면 <see cref="PassengerReactionEvent.None"/>.</summary>
        public PassengerReactionEvent CurrentEventOf(int passenger) =>
            IsReacting(passenger) ? _slots[passenger].Event : PassengerReactionEvent.None;

        /// <summary>
        /// 만료된 반응을 정리한다. 매 프레임 불러도 되고, <see cref="Notify"/>가 먼저 부른다.
        ///
        /// 정리를 게을리하면 이미 끝난 반응이 동시 한도의 슬롯을 잡고 있어 다음 사건이
        /// 통째로 묻힌다 — 승객이 조용해지는 버그는 거의 항상 여기서 나온다.
        /// </summary>
        public void Tick(float now)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].Active) continue;
                if (now < _slots[i].EndsAt) continue;
                _slots[i].Active = false;
                _slots[i].Event = PassengerReactionEvent.None;
                _slots[i].Reaction = default(PassengerReaction);
            }
        }

        /// <summary>모든 반응과 쿨다운을 지운다. 층·런이 다시 시작될 때 부른다.</summary>
        public void Reset()
        {
            for (int i = 0; i < _slots.Length; i++) _slots[i] = default(Slot);
            _cursor = 0;
            StartedCount = 0;
            SuppressedCount = 0;
        }

        /// <summary>
        /// 사건 하나를 알리고, **실제로 반응할 승객 인덱스**를 돌려준다.
        ///
        /// 규칙(§9.4):
        /// 1. 동시 반응은 <see cref="MaxConcurrent"/>를 넘지 않는다.
        /// 2. 쿨다운 중인 승객은 다시 반응하지 않는다.
        /// 3. 우선순위가 더 높은 반응만 진행 중인 반응을 덮어쓴다. 같거나 낮으면 무시한다.
        /// 4. 고르는 순서는 라운드 로빈이다 — 무작위가 아니다.
        ///
        /// 돌려주는 목록은 **내부 버퍼**다. 다음 <see cref="Notify"/>까지만 유효하다.
        /// 매 사건마다 새 리스트를 만들면 스핀당 수십 번의 할당이 생기고,
        /// §13.2의 "워밍업 후 매 프레임 0 B"를 지키기 어려워진다.
        /// </summary>
        public IReadOnlyList<int> Notify(PassengerReactionEvent reactionEvent, float now)
        {
            _selected.Clear();
            if (_slots.Length == 0) return _selected;                 // 승객 0명은 정상 상태다
            if (reactionEvent == PassengerReactionEvent.None) return _selected;
            if (_maxConcurrent <= 0) { SuppressedCount++; return _selected; }

            Tick(now);

            PassengerReaction reaction = Resolve(reactionEvent);
            if (!reaction.IsActive) return _selected;                 // 지속 0 = 데이터로 꺼 둔 반응

            int start = _cursor;

            // 1차 — 비어 있고 쿨다운도 끝난 승객. 동시 한도를 넘지 않는 선까지만 채운다.
            int active = ActiveCount;
            for (int i = 0; i < _slots.Length && _selected.Count < _maxConcurrent
                                              && active < _maxConcurrent; i++)
            {
                int index = (start + i) % _slots.Length;
                if (_slots[index].Active) continue;
                if (now < _slots[index].CooldownUntil) continue;
                Begin(index, reactionEvent, reaction, now);
                active++;
            }

            // 2차 — 자리가 남았는데 채우지 못했다면, 진행 중인 **더 낮은** 반응을 덮어쓴다.
            // 덮어쓰기는 인원을 늘리지 않으므로 동시 한도를 다시 볼 필요가 없다.
            for (int i = 0; i < _slots.Length && _selected.Count < _maxConcurrent; i++)
            {
                int index = (start + i) % _slots.Length;
                if (!_slots[index].Active) continue;
                if (_selected.Contains(index)) continue;              // 방금 시작한 것을 덮지 않는다
                if (_slots[index].Reaction.Priority >= reaction.Priority) continue;
                Begin(index, reactionEvent, reaction, now);
            }

            if (_selected.Count == 0) SuppressedCount++;
            return _selected;
        }

        private void Begin(int index, PassengerReactionEvent reactionEvent,
                           PassengerReaction reaction, float now)
        {
            _slots[index].Active = true;
            _slots[index].Event = reactionEvent;
            _slots[index].Reaction = reaction;
            _slots[index].EndsAt = now + reaction.Duration;
            // 쿨다운은 시작 시점부터 잰다. 종료 시점부터 재면 지속이 긴 반응일수록
            // 실질 침묵 시간이 길어져, 데이터에서 지속만 늘렸는데 반응 빈도가 같이 줄어든다.
            _slots[index].CooldownUntil = now + Math.Max(reaction.Duration, reaction.Cooldown);
            _selected.Add(index);
            _cursor = (index + 1) % _slots.Length;
            StartedCount++;
        }

        private PassengerReaction Resolve(PassengerReactionEvent reactionEvent)
        {
            if (_lookup != null) return _lookup(reactionEvent);

            if (!_warnedMissingLookup)
            {
                _warnedMissingLookup = true;
                // 조용히 기본값으로 돌면 "에셋이 안 붙었다"와 "에셋 값이 그렇다"를
                // 화면에서 구분할 수 없다.
                UnityEngine.Debug.LogWarning(
                    "[상승] PassengerReactionDirector 에 반응 조회기가 없다 — " +
                    "PassengerReactionSet 이 연결되지 않았다. 코드 기본값으로 진행한다.");
            }
            return PassengerReactionSet.DefaultFor(reactionEvent);
        }
    }
}
