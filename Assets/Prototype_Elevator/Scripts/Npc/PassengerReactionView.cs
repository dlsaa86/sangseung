using System;
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

        [Tooltip("비언어 음성을 실제로 내는 쪽. 비면 Awake 에서 씬에서 찾는다. " +
                 "없으면 자세·시선·대사만 나가고 음성 채널이 사건 버스 폴백에 남는다.")]
        [SerializeField] private Audio.AudioDirector _audio;

        [Tooltip("한 사건에 동시에 반응할 수 있는 최대 승객 수 (§9.4).")]
        [SerializeField, Min(1)] private int _maxConcurrent = 2;

        private PassengerReactionDirector _director;
        private PassengerReactionRouter _router;
        private GameEventBus _bus;
        private int _knownPassengerCount = -1;

        private int _startedBeforeRebuild;
        private int _suppressedBeforeRebuild;
        private int _firedKindsBeforeRebuild;

        // 표현 채널 넷의 누적(UP-NPC-04). 중재기가 아니라 **뷰**가 들고 있는 이유는
        // `Rebuild` 가 중재기를 통째로 새로 만들기 때문이다 — 승객 한 명이 내리는 순간
        // 「어떤 자세가 나왔나」가 0으로 돌아가면 층 끝에서 세는 하네스가 아무것도 못 본다.
        private int _poseKindsMask;
        private int _gazeKindsMask;
        private int _lineCount;
        private int _voiceCueCount;
        private int _contrastCount;

        /// <summary>지금 반응 중인 승객 수. 검증 하네스가 §9.4 상한을 확인할 때 읽는다.</summary>
        public int ActiveReactionCount => _director != null ? _director.ActiveCount : 0;

        /// <summary>지금까지 실제로 시작된 반응 수. 0이면 배선이 끊긴 것이다.</summary>
        public int StartedCount => _director != null ? _director.StartedCount : 0;

        /// <summary>동시 수 제한에 걸려 눌린 반응 수. 0만 나오면 제한이 작동하지 않은 것이다.</summary>
        public int SuppressedCount => _director != null ? _director.SuppressedCount : 0;

        /// <summary>
        /// 런 시작 이후 누적된 반응 수. **재구성으로 지워지지 않는다.**
        ///
        /// <see cref="StartedCount"/>만으로는 관측이 안 된다: 승객이 타고 내릴 때마다
        /// <see cref="Rebuild"/>가 중재기를 새로 만들고 그 카운터는 0부터 다시 센다.
        /// 하네스가 층 끝에서 읽으면 방금 하차한 층의 반응이 통째로 사라져 있고,
        /// 그러면 「반응 0건」이 배선 끊김인지 카운터 초기화인지 구분되지 않는다.
        /// </summary>
        public int TotalStartedCount => _startedBeforeRebuild + StartedCount;

        /// <summary>
        /// 같은 이유로 누적하는 억제 수.
        ///
        /// **§9.4 동시 반응 상한의 증거가 아니다.** `PassengerReactionDirector.Notify` 는
        /// 「아무도 못 했다」(`_selected.Count == 0`)에서만 이 값을 올린다. 상한 2에 승객 3명이고
        /// 한 명이 시작하면 나머지 둘이 막혀도 오르지 않고, 반대로 전원이 **쿨다운**(4~10초)이라
        /// 아무도 못 하면 상한과 무관하게 오른다. 하네스가 스핀 하나에 수십 개의 사건을 쏘므로
        /// 실제로 이 수의 대부분은 쿨다운이다.
        ///
        /// 상한을 확인하려면 <see cref="PeakActiveCount"/>를 <see cref="MaxConcurrent"/>와 비교한다.
        /// </summary>
        public int TotalSuppressedCount => _suppressedBeforeRebuild + SuppressedCount;

        /// <summary>런 중 관측된 최대 동시 반응 수. <see cref="MaxConcurrent"/>를 넘으면 §9.4 위반이다.</summary>
        public int PeakActiveCount { get; private set; }

        /// <summary>인스펙터에 설정된 동시 반응 상한.</summary>
        public int MaxConcurrent => Mathf.Max(1, _maxConcurrent);

        /// <summary>중재 대상 승객 수. 0 이면 칸이 비어 있어 반응이 **관측 불가**한 것이다.</summary>
        public int PassengerCount => _director != null ? _director.PassengerCount : 0;

        /// <summary>
        /// 런 전체에서 울린 사건 종류의 비트 집합. 누적 카운터와 같은 이유로 재구성을 견딘다 —
        /// 하차 한 번에 「어떤 종류가 울렸나」가 통째로 사라지면 `UP-NPC-02` 를 셀 수 없다.
        /// </summary>
        public int FiredKindsMask =>
            _firedKindsBeforeRebuild | (_director != null ? _director.FiredKindsMask : 0);

        /// <summary>울린 종류 수.</summary>
        public int FiredKindCount
        {
            get
            {
                int n = 0;
                for (int mask = FiredKindsMask; mask != 0; mask &= mask - 1) n++;
                return n;
            }
        }

        // ── 표현 채널 넷 (UP-NPC-04 · MASTER_PRD §9.3) ────────────────────────
        //
        // 하네스가 「반응 110건」이 아니라 **종류**를 셀 수 있어야 한다.
        // 한 자세가 110번 나온 것과 일곱 자세가 골고루 나온 것은 같은 총합을 낸다.

        /// <summary>런 중 실제로 걸린 자세의 비트 집합. 비트 n 은 <c>(ReactionPose)n</c>.</summary>
        public int PoseKindsMask => _poseKindsMask;

        /// <summary>런 중 실제로 걸린 시선 대상의 비트 집합. 비트 n 은 <c>(ReactionGaze)n</c>.</summary>
        public int GazeKindsMask => _gazeKindsMask;

        public int PoseKindCount => CountBits(_poseKindsMask);
        public int GazeKindCount => CountBits(_gazeKindsMask);

        /// <summary>화면에 내보낸 짧은 대사 수. 0이면 §9.3의 대사 채널이 죽어 있다.</summary>
        public int SpokenLineCount => _lineCount;

        /// <summary>
        /// <see cref="Audio.AudioDirector.PlayPassengerVoice"/>를 실제로 부른 횟수.
        /// 0인데 반응은 나갔다면 오디오가 배선되지 않았거나 데이터의 음성 큐가 비어 있는 것이다.
        /// </summary>
        public int VoiceCueCount => _voiceCueCount;

        /// <summary>
        /// 대표 반응 대신 상반된 반응이 걸린 횟수(§9.3 마지막 줄).
        /// 다른 누적 카운터와 같은 이유로 <see cref="Rebuild"/>를 견딘다.
        /// </summary>
        public int ContrastCount =>
            _contrastCount + (_director != null ? _director.ContrastCount : 0);

        /// <summary>마지막으로 나간 대사. 디버그 패널이 "방금 누가 뭐라고 했나"를 띄울 때 쓴다.</summary>
        public string LastLine { get; private set; }

        /// <summary>그 대사를 말한 승객 번호. 아직 없으면 −1.</summary>
        public int LastLinePassenger { get; private set; } = -1;

        /// <summary>
        /// 대사가 나갈 때마다 불린다. (승객 번호, 대사)
        ///
        /// **표시 경로를 여기서 정하지 않는다.** 지금은 <see cref="BuildFigureView"/>가
        /// 승객 머리 위 월드 라벨로 띄우지만, 화면 하단 자막이나 HUD가 붙을 자리도
        /// 같은 사건이어야 한다 — 표시 방식이 늘 때마다 이 파일을 고치게 되면
        /// 반응 규칙과 UI가 다시 붙는다.
        /// </summary>
        public event Action<int, string> LineSpoken;

        private static int CountBits(int mask)
        {
            int n = 0;
            for (int m = mask; m != 0; m &= m - 1) n++;
            return n;
        }

        private void Awake()
        {
            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();
            if (_figures == null) _figures = FindAnyObjectByType<BuildFigureView>();
            // 오디오는 **없어도 된다.** 없으면 음성 채널만 `AudioDirector` 의 사건 버스
            // 폴백에 남고 자세·시선·대사는 그대로 나간다. 경고하지 않는 이유는 그것이
            // 고장이 아니라 이 씬에 오디오가 아직 없다는 뜻이기 때문이다.
            if (_audio == null) _audio = FindAnyObjectByType<Audio.AudioDirector>();
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
            _figures?.ClearAllReactions();
            // 새 런의 적재는 항상 비어 있으므로 0으로 세운다. (`Rebuild` 가 곧
            // `_knownPassengerCount` 를 0 으로 덮으므로 여기서 -1 을 넣는 것은 의미가 없다.)
            Rebuild(0);

            // 누적 초기화는 **Rebuild 뒤**다. 앞에 두면 `Rebuild` 가 직전 런의 카운터를
            // 누적에 더한 뒤라 0 으로 돌아가지 않는다.
            _startedBeforeRebuild = 0;
            _suppressedBeforeRebuild = 0;
            _firedKindsBeforeRebuild = 0;
            PeakActiveCount = 0;

            _poseKindsMask = 0;
            _gazeKindsMask = 0;
            _lineCount = 0;
            _voiceCueCount = 0;
            _contrastCount = 0;
            LastLine = null;
            LastLinePassenger = -1;
        }

        /// <summary>
        /// 승객 수가 바뀌면 중재기를 다시 만든다. 자리 수가 곧 중재 대상 수라서,
        /// 하차·화물 포기로 인원이 줄었는데 옛 크기를 들고 있으면 없는 승객에게
        /// 반응을 배정하고 그 반응은 화면에 나타나지 않는다 — 조용한 실패다.
        /// </summary>
        private void Rebuild(int passengerCount)
        {
            _router?.Detach();

            // 버리기 전에 거둬 둔다 — 이 세 줄이 없으면 하차 한 번에 그 층의 반응 기록이 사라진다.
            _startedBeforeRebuild += StartedCount;
            _suppressedBeforeRebuild += SuppressedCount;
            if (_director != null)
            {
                _firedKindsBeforeRebuild |= _director.FiredKindsMask;
                _contrastCount += _director.ContrastCount;
            }

            _director = new PassengerReactionDirector(
                passengerCount,
                Mathf.Max(1, _maxConcurrent),
                LookUp,
                LookUpContrast);

            _router = new PassengerReactionRouter(_director, () => Time.time);
            _router.Reacted += OnReacted;
            if (_bus != null) _router.Attach(_bus);
            _knownPassengerCount = passengerCount;
        }

        private PassengerReaction LookUp(PassengerReactionEvent reactionEvent)
            => _reactions != null
                ? _reactions.For(reactionEvent)
                : PassengerReactionSet.DefaultFor(reactionEvent);

        private PassengerReactionContrast LookUpContrast(PassengerReactionEvent reactionEvent)
            => _reactions != null
                ? _reactions.ContrastFor(reactionEvent)
                : PassengerReactionSet.DefaultContrastFor(reactionEvent);

        /// <summary>
        /// 정해진 반응을 표현 채널 넷으로 흘려보낸다(§9.3).
        ///
        /// **승객마다 반응을 따로 읽는다.** `LookUp(사건)` 하나로 전원을 칠하면 상반된
        /// 반응이 사라진다 — 어느 승객이 대표를 받고 어느 승객이 대조를 받았는지는
        /// 중재기만 알고, 그 결과가 `CurrentOf(i)` 에 이미 들어 있다.
        /// </summary>
        private void OnReacted(PassengerReactionEvent reactionEvent, IReadOnlyList<int> passengers)
        {
            if (passengers == null) return;

            PassengerReaction fallback = LookUp(reactionEvent);

            for (int i = 0; i < passengers.Count; i++)
            {
                int passenger = passengers[i];
                PassengerReaction reaction = _director != null
                    ? _director.CurrentOf(passenger)
                    : fallback;
                if (!reaction.IsActive) reaction = fallback;

                // 1·2·3 채널 — 자세 · 시선 · 짧은 대사. 정지 화면에 남는 것이 이 셋이다.
                if (_figures != null)
                {
                    _figures.SetReaction(passenger, reaction.Pose, reaction.Gaze,
                                         reaction.Intensity, reaction.Line);
                }

                // 4 채널 — 비언어 음성. 큐 ID 가 비어 있으면 데이터가 이 반응의 소리를
                // 끈 것이므로 부르지 않는다(`PassengerReaction.VoiceCue` 주석).
                //
                // 큐 ID 자체는 아직 오디오에 전달되지 않는다 — `PlayPassengerVoice` 가
                // (승객 번호, 강도)만 받고 클립은 절차적으로 굽기 때문이다. 그래도
                // **여기서 불러야** 목소리가 실제로 반응한 승객에게 붙고,
                // `AudioDirector` 의 사건 버스 폴백이 물러나 한 사건에 목소리가 두 번
                // 나는 상태가 사라진다(그 파일의 배선 메모 6번).
                if (_audio != null && reaction.HasVoice)
                {
                    _audio.PlayPassengerVoice(passenger, reaction.Intensity);
                    _voiceCueCount++;
                }

                if (reaction.HasLine)
                {
                    _lineCount++;
                    LastLine = reaction.Line;
                    LastLinePassenger = passenger;

                    Action<int, string> handler = LineSpoken;
                    if (handler != null) handler(passenger, reaction.Line);
                }

                int pose = (int)reaction.Pose;
                if (pose >= 0 && pose < 32) _poseKindsMask |= 1 << pose;
                int gaze = (int)reaction.Gaze;
                if (gaze >= 0 && gaze < 32) _gazeKindsMask |= 1 << gaze;
            }
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
            int active = _director.ActiveCount;
            if (active > PeakActiveCount) PeakActiveCount = active;
            for (int p = 0; p < count; p++)
                if (!_director.IsReacting(p)) _figures.ClearReaction(p);
        }
    }
}
