using System.Collections.Generic;
using UnityEngine;
using Ascend.Prototype.Build;
using Ascend.Prototype.Events;
using Ascend.Prototype.Run;

namespace Ascend.Prototype.Npc
{
    /// <summary>
    /// 사건 → 중재기 → 승객의 몸. 이 컴포넌트가 승객 반응의 유일한 씬 진입점이다.
    ///
    /// 세 조각이 따로 있는 이유:
    ///   `PassengerReactionEvent.TryMap`  — 어떤 사건이 어떤 반응인가 (순수 함수, 테스트됨)
    ///   `PassengerReactionDirector`      — **누가** 반응할 것인가 (우선순위·쿨다운·동시 수)
    ///   여기                              — 그것을 몸으로 옮긴다
    ///
    /// 판단을 씬 컴포넌트에 두면 헤드리스로 검증할 수 없다. 실제로 `MASTER_PRD.md` §9.4의
    /// "한 이벤트에서 모든 승객이 동시에 말하지 않는다"는 규칙이지 연출이 아니므로,
    /// 규칙 쪽에 있어야 테스트가 지킬 수 있다.
    /// </summary>
    public sealed class PassengerReactionView : MonoBehaviour
    {
        [SerializeField] private RunSessionBehaviour _run;
        [SerializeField] private BuildFigureView _figures;

        [Tooltip("이벤트별 반응 데이터. 비면 코드 기본값으로 폴백한다 — 소리 없이 죽지 않는다.")]
        [SerializeField] private PassengerReactionSet _reactions;

        [Tooltip("한 사건에 동시에 반응할 수 있는 최대 승객 수 (§9.4).")]
        [SerializeField, Min(1)] private int _maxConcurrent = 2;

        private PassengerReactionDirector _director;
        private PassengerReactionRouter _router;
        private GameEventBus _bus;
        private int _knownPassengerCount = -1;

        /// <summary>지금 반응 중인 승객 수. 검증 하네스가 §9.4 상한을 확인할 때 읽는다.</summary>
        public int ActiveReactionCount => _director != null ? _director.ActiveCount : 0;

        /// <summary>지금까지 실제로 시작된 반응 수. 0이면 배선이 끊긴 것이다.</summary>
        public int StartedCount => _director != null ? _director.StartedCount : 0;

        /// <summary>동시 수 제한에 걸려 눌린 반응 수. 0만 나오면 제한이 작동하지 않은 것이다.</summary>
        public int SuppressedCount => _director != null ? _director.SuppressedCount : 0;

        private void Awake()
        {
            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();
            if (_figures == null) _figures = FindAnyObjectByType<BuildFigureView>();
            if (_run != null) _run.RunStarted += OnRunStarted;
        }

        private void OnDestroy()
        {
            if (_run != null) _run.RunStarted -= OnRunStarted;
            _router?.Detach();
        }

        private void OnRunStarted(RunSession session)
        {
            _router?.Detach();
            _bus = session != null ? session.Events : null;
            _knownPassengerCount = -1;   // 다음 Update 가 인원에 맞춰 다시 세운다
            _figures?.ClearAllReactions();
            Rebuild(0);
        }

        /// <summary>
        /// 승객 수가 바뀌면 중재기를 다시 만든다. 자리 수가 곧 중재 대상 수라서,
        /// 하차·화물 포기로 인원이 줄었는데 옛 크기를 들고 있으면 없는 승객에게
        /// 반응을 배정하고 그 반응은 화면에 나타나지 않는다 — 조용한 실패다.
        /// </summary>
        private void Rebuild(int passengerCount)
        {
            _router?.Detach();

            _director = new PassengerReactionDirector(
                passengerCount,
                Mathf.Max(1, _maxConcurrent),
                LookUp);

            _router = new PassengerReactionRouter(_director, () => Time.time);
            _router.Reacted += OnReacted;
            if (_bus != null) _router.Attach(_bus);
            _knownPassengerCount = passengerCount;
        }

        private PassengerReaction LookUp(PassengerReactionEvent reactionEvent)
            => _reactions != null
                ? _reactions.For(reactionEvent)
                : PassengerReactionSet.DefaultFor(reactionEvent);

        private void OnReacted(PassengerReactionEvent reactionEvent, IReadOnlyList<int> passengers)
        {
            if (_figures == null || passengers == null) return;
            PassengerReaction reaction = LookUp(reactionEvent);
            for (int i = 0; i < passengers.Count; i++)
                _figures.SetReaction(passengers[i], reaction.Pose, reaction.Gaze, reaction.Intensity);
        }

        private void Update()
        {
            if (_figures == null) return;

            if (_bus == null)
            {
                RunSession session = _run != null ? _run.Session : null;
                if (session == null) return;
                _bus = session.Events;
                _knownPassengerCount = -1;
            }

            int count = _figures.PassengerCount;
            if (count != _knownPassengerCount) Rebuild(count);
            if (_director == null) return;

            // 만료를 먼저 정리하고 그 결과를 몸에 반영한다. 순서를 뒤집으면
            // 끝난 반응이 한 프레임 더 남는다.
            _router.Tick();
            for (int p = 0; p < count; p++)
                if (!_director.IsReacting(p)) _figures.ClearReaction(p);
        }
    }
}
