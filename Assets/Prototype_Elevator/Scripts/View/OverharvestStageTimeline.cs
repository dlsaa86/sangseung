using System.Text;
using Ascend.Prototype.Data.Profiles;

namespace Ascend.Prototype.View
{
    /// <summary>`MASTER_PRD.md` §7.3 이 나열한 다섯 단계. 값은 그 문서의 번호 그대로다.</summary>
    public enum OverharvestStage
    {
        None = 0,

        /// <summary>1. 플레이어가 레버에 접근한다.</summary>
        Approach = 1,

        /// <summary>2. 주변 기계음과 사이렌이 잠깐 줄어든다.</summary>
        MachineDuck = 2,

        /// <summary>3. 승객이 플레이어와 레버를 바라본다.</summary>
        PassengerGaze = 3,

        /// <summary>4. 손을 올리는 순간 0.3~0.7초의 정적을 만든다.</summary>
        Silence = 4,

        /// <summary>5. 끝까지 당기면 경고등·진동·통관 회전이 동시에 재개된다.</summary>
        Resume = 5,
    }

    /// <summary>
    /// 5단계에서 「동시에」 되살아나야 하는 세 채널. §7.3 의 마지막 문장이 이름 그대로다.
    /// 열거형으로 두는 이유는 셋이 **같은 시각에서 파생됐다**를 반증 가능하게 만들기 위해서다 —
    /// 채널마다 재개 시작 시각을 따로 들고 있고, 그 셋이 같은지 밖에서 물을 수 있다.
    /// </summary>
    public enum OverharvestChannel
    {
        /// <summary>경고등 — 경고 띠와 전용 스팟.</summary>
        Warning = 0,

        /// <summary>진동 — 환경 오브젝트. 카메라가 아니다(`VISUAL_SPEC.md` §8.3).</summary>
        Shake = 1,

        /// <summary>통관 회전.</summary>
        TubeSpin = 2,
    }

    /// <summary>
    /// 과수확 5단계의 **판정만** 담은 타임라인. MonoBehaviour 가 아니다.
    ///
    /// 왜 분리하는가: `UP-POWER-06` 의 검증 기준은 「다섯 단계가 순서대로, 프로파일이 정한
    /// 시각에 일어났는가」이고, 그것이 MonoBehaviour 안에 있으면 재려면 씬을 띄우고 눈으로
    /// 봐야 한다. 0.29초와 0.31초를 눈으로 구분할 방법은 없다. 시간을 인자로 받는 계산기로
    /// 두면 씬 없이 눈금을 잰다 — <see cref="Ascend.Prototype.Audio.SilenceWindow"/> 가
    /// 같은 이유로 순수 C# 이다.
    ///
    /// **5단계가 1~4 없이도 발화한다.** 하네스는 레버를 겨누지 않고 <c>Interact()</c> 를
    /// 직접 부르므로 1단계가 성립하지 않는데, 그렇다고 「당김 뒤 재개」까지 사라지면
    /// 대표 장면이 통째로 관측 불가능해진다. 그래서 <see cref="Pull"/> 은 앞 단계와
    /// 무관하게 재개를 연다. 대신 단계별 카운터가 **1~4 가 0 이었다는 사실을 남긴다** —
    /// 「다 됐다」와 「5만 됐다」가 숫자로 갈린다.
    /// </summary>
    public sealed class OverharvestStageTimeline
    {
        public const int ChannelCount = 3;

        /// <summary>§7.3 의 단계 수. 카운터 배열의 길이이자 대조표가 셀 수 있어야 하는 값이다.</summary>
        public const int StageCount = 5;

        /// <summary>
        /// 프로파일이 배선되지 않았을 때 <see cref="ProfileSource"/> 에 찍히는 이름.
        /// **`"Profile"` 로 끝나지 않는다** — 하네스의 단정이 <c>EndsWith("Profile")</c> 이라
        /// 폴백 이름이 그것을 만족하면 배선이 끊겨도 검사가 통과한다.
        /// </summary>
        public const string CodePresetSource = "코드 프리셋";

        /// <summary>기계음·기계 동작이 1 에서 감쇠 배율까지 내려가는 시간. `AudioDirector` 의 기본값과 같다.</summary>
        public const float DefaultDuckSeconds = 0.12f;

        /// <summary>재개 순간의 과증폭. 0 이면 「돌아왔다」만 있고 「튀었다」가 없어 사건으로 안 읽힌다.</summary>
        public const float DefaultBurstOvershoot = 0.6f;

        private OverharvestSnapshot _profile = OverharvestProfile.DefaultSnapshot;
        private float _duckSeconds = DefaultDuckSeconds;
        private float _burstOvershoot = DefaultBurstOvershoot;

        private readonly int[] _stageCounts = new int[StageCount];
        private readonly float[] _stageFiredAt = new float[StageCount];
        private readonly bool[] _stageFired = new bool[StageCount];

        /// <summary>채널별 재개 시작 시각. 셋이 같아야 §7.3 의 「동시에」가 성립한다.</summary>
        private readonly float[] _channelResumeAt = new float[ChannelCount];

        private bool _sequenceOpen;
        private float _approachAt;
        private float _silenceSeconds = OverharvestProfile.DefaultMinSilenceSeconds;

        private bool _released;
        private float _releaseAt;
        private float _releaseFrom = 1f;

        private bool _resuming;
        private float _resumeFrom = 1f;

        /// <summary>과수확 수치가 어디서 왔는가. 폴백이면 <see cref="CodePresetSource"/> 다.</summary>
        public string ProfileSource { get; private set; } = CodePresetSource;

        /// <summary>지금 들고 있는 값 사본. 하네스가 「무엇을 읽었는지」를 되물을 수 있어야 한다.</summary>
        public OverharvestSnapshot Profile { get { return _profile; } }

        public float DuckSeconds { get { return _duckSeconds; } }
        public float BurstOvershoot { get { return _burstOvershoot; } }

        /// <summary>접근 중 기계가 내려앉는 바닥값. `OverharvestProfile.ApproachMachineDuckScale`.</summary>
        public float DuckScale { get { return Clamp01(_profile.ApproachMachineDuckScale); } }

        /// <summary>재개에 걸리는 시간. `OverharvestProfile.ResumeFadeSeconds`.</summary>
        public float ResumeSeconds
        {
            get { return _profile.ResumeFadeSeconds > 0.01f ? _profile.ResumeFadeSeconds : 0.01f; }
        }

        /// <summary>
        /// 접근 시각에서 정적이 시작되기까지의 오프셋.
        ///
        /// 감쇠(2단계)와 응시 지연(3단계) 중 **늦은 쪽**을 쓴다. §7.3 의 번호가 곧 순서이고,
        /// 기본값에서 응시 지연 0.18s 가 감쇠 0.12s 보다 길기 때문에 감쇠 끝에 바로 정적을
        /// 열면 3단계가 4단계 뒤로 밀린다 — 「승객이 돌아본 다음 방이 멈춘다」가 뒤집힌다.
        /// </summary>
        public float SilenceStartOffset
        {
            get
            {
                float gaze = _profile.PassengerGazeDelaySeconds;
                if (gaze < 0f) gaze = 0f;
                return _duckSeconds > gaze ? _duckSeconds : gaze;
            }
        }

        /// <summary>이번 접근에 뽑힌 정적 길이. 시드에서 파생되므로 같은 시드에서 재현된다.</summary>
        public float SilenceSeconds { get { return _silenceSeconds; } }

        /// <summary>접근~정적~복귀 구간이 아직 열려 있는가.</summary>
        public bool SequenceOpen { get { return _sequenceOpen; } }

        /// <summary>당김 뒤 재개가 진행 중인가.</summary>
        public bool IsResuming { get { return _resuming; } }

        // ── 단계별 카운터 ────────────────────────────────────────────────────
        // 총합이 아니라 단계별이다. 총합만 있으면 「5단계가 다 돌았다」를 반증할 수 없다 —
        // 1단계가 다섯 번 뛰어도 총합은 5 가 된다.

        public int ApproachCount { get { return _stageCounts[0]; } }
        public int MachineDuckCount { get { return _stageCounts[1]; } }
        public int PassengerGazeCount { get { return _stageCounts[2]; } }
        public int SilenceCount { get { return _stageCounts[3]; } }
        public int ResumeCount { get { return _stageCounts[4]; } }

        /// <summary>조준을 거둔 횟수. 5단계가 아니다 — 물러남과 당김을 같은 수로 세면 둘이 구분되지 않는다.</summary>
        public int ReleaseCount { get; private set; }

        public int CountOf(OverharvestStage stage)
        {
            int i = (int)stage - 1;
            return i >= 0 && i < StageCount ? _stageCounts[i] : 0;
        }

        /// <summary>그 단계가 마지막으로 발화한 시각. 한 번도 안 났으면 음수.</summary>
        public float FiredAt(OverharvestStage stage)
        {
            int i = (int)stage - 1;
            return i >= 0 && i < StageCount && _stageCounts[i] > 0 ? _stageFiredAt[i] : -1f;
        }

        /// <summary>다섯 단계가 전부 한 번 이상 발화했는가.</summary>
        public bool AllStagesFired
        {
            get
            {
                for (int i = 0; i < StageCount; i++) if (_stageCounts[i] <= 0) return false;
                return true;
            }
        }

        // ── 설정 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 값 사본으로 직접 설정한다. 테스트가 경고 없이 값을 흔들어 볼 수 있는 경로다.
        /// </summary>
        public void Configure(OverharvestSnapshot profile, string source,
                              float duckSeconds, float burstOvershoot)
        {
            _profile = profile;
            ProfileSource = string.IsNullOrEmpty(source) ? CodePresetSource : source;
            _duckSeconds = duckSeconds > 0.01f ? duckSeconds : 0.01f;
            _burstOvershoot = burstOvershoot >= 0f ? burstOvershoot : 0f;
        }

        /// <summary>
        /// 에셋에서 설정한다. 폴백이면 <see cref="CodePresetSource"/> 가 찍히고
        /// <see cref="OverharvestProfile.SnapshotOrDefault"/> 가 경고를 남긴다.
        /// </summary>
        public void Configure(OverharvestProfile profile, string caller,
                              float duckSeconds, float burstOvershoot)
        {
            Configure(OverharvestProfile.SnapshotOrDefault(profile, caller),
                      SourceNameOf(profile), duckSeconds, burstOvershoot);
        }

        /// <summary>배선 여부를 이름 하나로. 폴백 이름은 「Profile」로 끝나지 않는다.</summary>
        public static string SourceNameOf(OverharvestProfile profile)
        {
            return profile != null ? profile.name : CodePresetSource;
        }

        /// <summary>런이 새로 시작됐다. 카운터와 진행 상태를 전부 버린다.</summary>
        public void ResetRun()
        {
            for (int i = 0; i < StageCount; i++)
            {
                _stageCounts[i] = 0;
                _stageFiredAt[i] = 0f;
                _stageFired[i] = false;
            }
            for (int i = 0; i < ChannelCount; i++) _channelResumeAt[i] = 0f;

            ReleaseCount = 0;
            _sequenceOpen = false;
            _released = false;
            _resuming = false;
            _releaseFrom = 1f;
            _resumeFrom = 1f;
            _approachAt = 0f;
        }

        // ── 사건 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 1단계 — 레버를 겨눴다. <paramref name="seed"/> 에서 정적 길이를 뽑으므로
        /// 같은 시드는 같은 길이를 낸다(`TECH_SPEC.md` §14, 캡처 재현).
        /// </summary>
        public bool Approach(float now, int seed)
        {
            if (_sequenceOpen && !_released) return false;

            _sequenceOpen = true;
            _released = false;
            _resuming = false;
            _approachAt = now;
            _silenceSeconds = _profile.ClampedSilenceSeconds(_profile.SilenceSecondsFor(seed));

            for (int i = 0; i < StageCount; i++) _stageFired[i] = false;

            Fire(OverharvestStage.Approach, now);
            Tick(now);
            return true;
        }

        /// <summary>
        /// 조준을 거뒀다. 지금 값에서 곧바로 복귀 페이드로 넘어간다 —
        /// 1 로 튕겨 올리면 「물러났다」가 「고장났다」로 읽힌다
        /// (<see cref="Ascend.Prototype.Audio.SilenceWindow.Cancel"/> 와 같은 판단).
        /// </summary>
        public bool Release(float now)
        {
            if (!_sequenceOpen || _released) return false;

            _releaseFrom = ChannelLevel(OverharvestChannel.TubeSpin, now);
            _released = true;
            _releaseAt = now;
            ReleaseCount++;
            return true;
        }

        /// <summary>
        /// 5단계 — 끝까지 당겼다. **세 채널의 재개 시각을 한 번에 같은 값으로 적는다.**
        /// 이 배열이 곧 「동시에」의 근거이고, <see cref="ResumeIsSimultaneous"/> 가 그것을 되묻는다.
        ///
        /// 앞 단계가 없어도 연다. 이유는 클래스 주석 참조.
        /// </summary>
        public bool Pull(float now)
        {
            _resumeFrom = ChannelLevel(OverharvestChannel.TubeSpin, now);

            for (int i = 0; i < ChannelCount; i++) _channelResumeAt[i] = now;

            _resuming = true;
            _sequenceOpen = false;
            _released = false;

            Fire(OverharvestStage.Resume, now);
            return true;
        }

        /// <summary>
        /// 시간이 흘렀다. 2·3·4 단계는 사건이 아니라 **시각**이라 여기서만 발화한다.
        /// 호출하지 않아도 <see cref="ChannelScale"/> 의 값은 옳다 — 카운터만 멈춘다.
        /// </summary>
        public void Tick(float now)
        {
            if (_sequenceOpen && !_released)
            {
                float e = now - _approachAt;
                if (e >= 0f) FireOnce(OverharvestStage.MachineDuck, now);

                float gaze = _profile.PassengerGazeDelaySeconds;
                if (gaze < 0f) gaze = 0f;
                if (e >= gaze) FireOnce(OverharvestStage.PassengerGaze, now);

                if (e >= SilenceStartOffset) FireOnce(OverharvestStage.Silence, now);
            }

            if (_released && now - _releaseAt >= ResumeSeconds)
            {
                _released = false;
                _sequenceOpen = false;
            }

            if (_resuming && now - _channelResumeAt[0] >= ResumeSeconds) _resuming = false;
        }

        // ── 채널 값 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 기계가 얼마나 돌고 있는가(0~1). 1 은 평소, 0 은 완전 정지.
        /// **채널마다 따로 계산한다** — 재개 시작 시각을 채널별 배열에서 읽기 때문이다.
        /// 셋이 같은 값을 들고 있으면 결과도 같고, 하나라도 어긋나면 여기서 갈린다.
        /// </summary>
        public float ChannelLevel(OverharvestChannel channel, float now)
        {
            if (_resuming)
            {
                float p = Clamp01((now - _channelResumeAt[(int)channel]) / ResumeSeconds);
                return _resumeFrom + (1f - _resumeFrom) * p;
            }

            if (_released)
            {
                float p = Clamp01((now - _releaseAt) / ResumeSeconds);
                return _releaseFrom + (1f - _releaseFrom) * p;
            }

            if (!_sequenceOpen) return 1f;

            float e = now - _approachAt;
            if (e <= 0f) return 1f;

            float duck = DuckScale;
            if (e < _duckSeconds) return 1f + (duck - 1f) * (e / _duckSeconds);

            float silenceStart = SilenceStartOffset;
            if (e < silenceStart) return duck;

            float silenceEnd = silenceStart + _silenceSeconds;
            if (e < silenceEnd) return 0f;

            // 당기지 않고 버티면 방은 영원히 멈춰 있지 않는다. 오디오의 정적 창도 같은 식으로
            // 스스로 돌아온다 — 여기만 0 에 머물면 소리는 살아났는데 화면은 죽어 있다.
            float resumed = e - silenceEnd;
            if (resumed >= ResumeSeconds) return 1f;
            return resumed / ResumeSeconds;
        }

        /// <summary>재개 순간의 과증폭(1 → 0). 당기지 않았으면 0.</summary>
        public float ResumeBurst(OverharvestChannel channel, float now)
        {
            if (!_resuming) return 0f;
            float p = Clamp01((now - _channelResumeAt[(int)channel]) / ResumeSeconds);
            if (p >= 1f) return 0f;
            return 1f - p;
        }

        /// <summary>
        /// 그 채널이 지금 내야 하는 배수.
        ///   경고등·통관 — 평소 1, 정적에서 0, 당기면 1 을 넘겨 튄 뒤 1 로 앉는다.
        ///   진동       — 평소 0. 「진동이 재개된다」는 없던 것이 생기는 사건이다.
        /// </summary>
        public float ChannelScale(OverharvestChannel channel, float now)
        {
            if (channel == OverharvestChannel.Shake) return ResumeBurst(channel, now);
            return ChannelLevel(channel, now) + _burstOvershoot * ResumeBurst(channel, now);
        }

        /// <summary>
        /// 재개 버스트가 돌린 회전량(0~1). **끝나면 정확히 0 으로 돌아온다** —
        /// 이 값에 곱하는 회전수를 정수로 두면 버스트가 끝난 각도가 시작 각도와 같아진다.
        /// 캡처가 「과수확을 한 번 당긴 런」과 「안 당긴 런」에서 다른 각도로 굳지 않게 하려는 것이다.
        /// </summary>
        public float BurstTurns01(float now)
        {
            if (!_resuming) return 0f;
            float p = Clamp01((now - _channelResumeAt[(int)OverharvestChannel.TubeSpin]) / ResumeSeconds);
            if (p >= 1f) return 0f;
            float inv = 1f - p;
            return 1f - inv * inv;
        }

        /// <summary>그 채널의 재개가 시작된 시각. 한 번도 안 당겼으면 0.</summary>
        public float ResumeStartedAt(OverharvestChannel channel)
        {
            return _channelResumeAt[(int)channel];
        }

        /// <summary>
        /// 세 채널이 같은 순간에 재개했는가. §7.3 의 「동시에」를 반증 가능하게 만드는 값이다.
        /// 한 번도 당기지 않았으면 셋 다 0 이라 참이 되므로, <see cref="ResumeCount"/> 와
        /// 함께 읽어야 뜻이 있다.
        /// </summary>
        public bool ResumeIsSimultaneous
        {
            get
            {
                float first = _channelResumeAt[0];
                for (int i = 1; i < ChannelCount; i++)
                    if (_channelResumeAt[i] != first) return false;
                return true;
            }
        }

        /// <summary>단계별 카운터를 한 줄로. 「다 됐다」가 아니라 숫자를 남긴다.</summary>
        public string Describe()
        {
            var sb = new StringBuilder(160);
            sb.Append("과수확 5단계 [");
            sb.Append("1접근 ").Append(ApproachCount);
            sb.Append(" / 2감쇠 ").Append(MachineDuckCount);
            sb.Append(" / 3응시 ").Append(PassengerGazeCount);
            sb.Append(" / 4정적 ").Append(SilenceCount);
            sb.Append(" / 5재개 ").Append(ResumeCount);
            sb.Append("] 해제 ").Append(ReleaseCount);
            sb.Append(" 출처 「").Append(ProfileSource).Append("」");
            sb.Append(" 감쇠 ").Append(DuckScale.ToString("0.###"));
            sb.Append(" 재개 ").Append(ResumeSeconds.ToString("0.###")).Append("s");
            sb.Append(" 동시 ").Append(ResumeIsSimultaneous ? "예" : "아니오");
            return sb.ToString();
        }

        // ── 내부 ─────────────────────────────────────────────────────────────

        private void Fire(OverharvestStage stage, float now)
        {
            int i = (int)stage - 1;
            if (i < 0 || i >= StageCount) return;
            _stageCounts[i]++;
            _stageFiredAt[i] = now;
            _stageFired[i] = true;
        }

        private void FireOnce(OverharvestStage stage, float now)
        {
            int i = (int)stage - 1;
            if (i < 0 || i >= StageCount || _stageFired[i]) return;
            Fire(stage, now);
        }

        private static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }
    }
}
