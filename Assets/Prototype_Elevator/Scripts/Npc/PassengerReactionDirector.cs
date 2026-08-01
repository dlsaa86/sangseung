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
        private readonly Func<PassengerReactionEvent, PassengerReactionContrast> _contrastLookup;
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
            : this(passengerCount, maxConcurrent, lookup, null)
        {
        }

        /// <param name="contrastLookup">
        /// 사건 → 상반된 반응(§9.3). 보통 <see cref="PassengerReactionSet.ContrastFor"/>를 넘긴다.
        /// null이면 전원이 같은 반응을 한다 — 그것도 유효한 상태이므로 경고하지 않는다.
        /// </param>
        public PassengerReactionDirector(int passengerCount, int maxConcurrent,
                                         Func<PassengerReactionEvent, PassengerReaction> lookup,
                                         Func<PassengerReactionEvent, PassengerReactionContrast> contrastLookup)
        {
            int count = passengerCount > 0 ? passengerCount : 0;
            _slots = new Slot[count];
            // 음수는 0으로 접는다. 0은 "아무도 반응하지 않는다"는 유효한 설정이다 —
            // 반응 전체를 끄고 다른 채널만 보고 싶을 때 쓴다.
            _maxConcurrent = maxConcurrent > 0 ? maxConcurrent : 0;
            _lookup = lookup;
            _contrastLookup = contrastLookup;
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

        /// <summary>
        /// 실제로 반응을 일으킨 사건 종류의 비트 집합. 비트 n 은 `(PassengerReactionEvent)n`.
        ///
        /// **총합이 아니라 종류를 세는 이유**: `UP-NPC-02` 는 「프로토타입 반응 이벤트 10종」을
        /// 요구한다. 「반응 110건」은 한 종류가 110번 울려도 나오는 숫자다.
        /// </summary>
        public int FiredKindsMask { get; private set; }

        /// <summary>울린 종류 수. 비트를 센다.</summary>
        public int FiredKindCount
        {
            get
            {
                int n = 0;
                for (int mask = FiredKindsMask; mask != 0; mask &= mask - 1) n++;
                return n;
            }
        }

        // ── 표현 채널 관측 (UP-NPC-04) ────────────────────────────────────────
        //
        // 총합이 아니라 **종류**를 센다. §9.3은 채널 넷을 요구하는데 「반응 110건」은
        // 한 자세가 110번 나와도 나오는 숫자다. `FiredKindsMask`가 사건 종류에 대해
        // 같은 일을 하는 것과 같은 이유이며, 같은 방식(비트 집합)을 쓴다.

        /// <summary>실제로 몸에 걸린 자세의 비트 집합. 비트 n 은 <c>(ReactionPose)n</c>.</summary>
        public int PoseKindsMask { get; private set; }

        /// <summary>실제로 걸린 시선 대상의 비트 집합. 비트 n 은 <c>(ReactionGaze)n</c>.</summary>
        public int GazeKindsMask { get; private set; }

        /// <summary>대사가 실린 반응 수. 0이면 §9.3의 「짧은 대사」 채널이 비어 있다.</summary>
        public int LineCount { get; private set; }

        /// <summary>음성 큐가 실린 반응 수. 0이면 「비언어 음성」 채널이 비어 있다.</summary>
        public int VoiceCueCount { get; private set; }

        /// <summary>대표 반응 대신 <b>상반된</b> 반응이 걸린 횟수(§9.3 마지막 줄).</summary>
        public int ContrastCount { get; private set; }

        public int PoseKindCount => CountBits(PoseKindsMask);
        public int GazeKindCount => CountBits(GazeKindsMask);

        private static int CountBits(int mask)
        {
            int n = 0;
            for (int m = mask; m != 0; m &= m - 1) n++;
            return n;
        }

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
            FiredKindsMask = 0;
            PoseKindsMask = 0;
            GazeKindsMask = 0;
            LineCount = 0;
            VoiceCueCount = 0;
            ContrastCount = 0;
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

            // 상반된 반응(§9.3)은 **표현만** 바꾼다. 지속·우선순위·쿨다운은
            // `ApplyTo` 가 대표 반응에서 그대로 물려주므로 아래 중재 규칙에는
            // 대조가 있든 없든 같은 값이 들어간다 — UP-NPC-05 의 세 축이 그대로 남는다.
            PassengerReaction contrast;
            bool hasContrast = TryResolveContrast(reactionEvent, in reaction, out contrast);

            int start = _cursor;

            // 1차 — 비어 있고 쿨다운도 끝난 승객. 동시 한도를 넘지 않는 선까지만 채운다.
            int active = ActiveCount;
            for (int i = 0; i < _slots.Length && _selected.Count < _maxConcurrent
                                              && active < _maxConcurrent; i++)
            {
                int index = (start + i) % _slots.Length;
                if (_slots[index].Active) continue;
                if (now < _slots[index].CooldownUntil) continue;
                Begin(index, reactionEvent, in reaction, in contrast, hasContrast, now);
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
                Begin(index, reactionEvent, in reaction, in contrast, hasContrast, now);
            }

            if (_selected.Count == 0) SuppressedCount++;
            return _selected;
        }

        private void Begin(int index, PassengerReactionEvent reactionEvent,
                           in PassengerReaction primary, in PassengerReaction contrast,
                           bool hasContrast, float now)
        {
            // 이 사건에서 **몇 번째로 뽑혔는가**로 대표/대조를 가른다. 무작위가 아니다 —
            // `_cursor` 라운드 로빈과 같은 이유로, 같은 시드의 고정 캡처가 매번 같아야 한다.
            bool useContrast = hasContrast && (_selected.Count & 1) == 1;
            PassengerReaction reaction = useContrast ? contrast : primary;
            if (useContrast) ContrastCount++;

            _slots[index].Active = true;
            _slots[index].Event = reactionEvent;
            _slots[index].Reaction = reaction;
            // 타이밍은 **대표 반응** 것을 쓴다. 대조가 다른 시각에 끝나면 같은 사건의
            // 반응이 사람마다 다른 길이가 되어, 그 차이가 연출인지 버그인지 갈리지 않는다.
            _slots[index].EndsAt = now + primary.Duration;
            // 쿨다운은 시작 시점부터 잰다. 종료 시점부터 재면 지속이 긴 반응일수록
            // 실질 침묵 시간이 길어져, 데이터에서 지속만 늘렸는데 반응 빈도가 같이 줄어든다.
            _slots[index].CooldownUntil = now + Math.Max(primary.Duration, primary.Cooldown);
            _selected.Add(index);
            _cursor = (index + 1) % _slots.Length;
            StartedCount++;

            // 어떤 **종류**가 울렸는지 비트로 남긴다. 총합만으로는 한 종류가 110번 울린 것과
            // 열한 종류가 열 번씩 울린 것이 구분되지 않고, `UP-NPC-02` 가 묻는 것은 후자다.
            // 종류는 1~11 이라 int 하나로 충분하다.
            int bit = (int)reactionEvent;
            if (bit > 0 && bit < 32) FiredKindsMask |= 1 << bit;

            // 표현 채널 넷도 같은 방식으로 센다(UP-NPC-04).
            int pose = (int)reaction.Pose;
            if (pose >= 0 && pose < 32) PoseKindsMask |= 1 << pose;
            int gaze = (int)reaction.Gaze;
            if (gaze >= 0 && gaze < 32) GazeKindsMask |= 1 << gaze;
            if (reaction.HasLine) LineCount++;
            if (reaction.HasVoice) VoiceCueCount++;
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

        /// <summary>
        /// 상반된 반응을 대표 반응 위에 얹은 결과. 조회기가 없거나 대조가 정의돼 있지
        /// 않으면 대표 반응 그대로다 — 그 경우 전원이 같은 반응을 한다.
        ///
        /// 조회기가 없다고 경고하지 않는 이유: `_lookup` 이 없는 것은 배선 사고지만
        /// 대조가 없는 것은 **정당한 설정**이다. 둘을 같은 경고로 묶으면 진짜 배선
        /// 누락이 정상 상태의 잡음에 묻힌다.
        /// </summary>
        private bool TryResolveContrast(PassengerReactionEvent reactionEvent,
                                        in PassengerReaction primary,
                                        out PassengerReaction resolved)
        {
            resolved = primary;
            if (_contrastLookup == null) return false;

            PassengerReactionContrast contrast = _contrastLookup(reactionEvent);
            // **정의되지 않은 대조는 "대조 없음"이다.** 여기서 true 를 돌려주면
            // `ContrastCount` 가 실제로는 대표 반응만 나갔는데도 오르고, 그 값으로
            // "상반된 반응이 나오는가"를 물을 수 없게 된다.
            if (!contrast.IsDefined) return false;

            resolved = contrast.ApplyTo(in primary);
            return true;
        }
    }
}
