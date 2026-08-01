namespace Ascend.Prototype.Audio
{
    /// <summary>
    /// 과수확 레버에 손을 올린 순간 방을 조용하게 만드는 게인 곡선 하나.
    /// UP-AUD-03이고, 근거는 `MASTER_PRD.md` §7.3 5단계 중 4번 —
    /// "손을 올리는 순간 **0.3~0.7초의 정적**을 만든다."
    ///
    /// 왜 순수 C#인가: 이 항목의 검증 기준은 "오디오 게인 타임라인 측정"이다
    /// (`TOPDOWN_MASTER_BACKLOG.md` UP-AUD-03). 타임라인이 MonoBehaviour 안에 있으면
    /// 측정하려면 씬을 띄우고 귀로 들어야 하고, 그러면 0.29초와 0.31초를 구분할 방법이 없다.
    /// 시간을 인자로 받는 순수 함수로 두면 씬 없이 눈금을 잴 수 있다.
    ///
    /// 왜 즉시 0으로 떨어뜨리지 않는가: §7.3의 순서가 "2. 주변 기계음이 **잠깐 줄어든다** →
    /// 4. 정적"이다. 두 단계다. 한 프레임에 무음으로 끊으면 그건 정적이 아니라
    /// 오디오 버그로 들린다 — 실제로 소리가 뚝 끊기면 사람은 고장을 의심하지 긴장하지 않는다.
    /// </summary>
    public sealed class SilenceWindow
    {
        /// <summary>정적 길이의 하한. `MASTER_PRD.md` §7.3(4).</summary>
        public const float MinSilenceSeconds = 0.3f;

        /// <summary>정적 길이의 상한. 같은 문장. 0.7초를 넘기면 정지 화면처럼 읽힌다.</summary>
        public const float MaxSilenceSeconds = 0.7f;

        private float _duckSeconds = 0.12f;
        private float _resumeSeconds = 0.25f;

        private bool _begun;
        private float _begin;
        private float _silence = MinSilenceSeconds;

        private bool _cancelled;
        private float _cancelAt;
        private float _cancelGain;

        /// <summary>1 → 0으로 내려가는 시간(§7.3 2단계 "잠깐 줄어든다").</summary>
        public float DuckSeconds
        {
            get { return _duckSeconds; }
            set { _duckSeconds = value < 0.01f ? 0.01f : value; }
        }

        /// <summary>0 → 1로 돌아오는 시간(§7.3 5단계 "동시에 재개").</summary>
        public float ResumeSeconds
        {
            get { return _resumeSeconds; }
            set { _resumeSeconds = value < 0.01f ? 0.01f : value; }
        }

        /// <summary>실제로 쓰이는 정적 길이. 요청값을 0.3~0.7로 조인 결과다.</summary>
        public float SilenceSeconds { get { return _silence; } }

        /// <summary>한 번이라도 시작했는가. 시작한 적이 없으면 게인은 항상 1이다.</summary>
        public bool HasBegun { get { return _begun; } }

        /// <summary>감쇠 시작부터 재개 완료까지의 전체 길이.</summary>
        public float TotalSeconds { get { return _duckSeconds + _silence + _resumeSeconds; } }

        /// <summary>
        /// 정적을 시작한다. <paramref name="duration"/>은 **완전 정적 구간의 길이**이며
        /// 감쇠·재개 시간은 포함하지 않는다. 범위 밖이면 조인다 — 호출자가 프로파일에서
        /// 이상한 값을 넣어도 §7.3의 약속은 코드가 지킨다.
        /// </summary>
        public void Begin(float now, float duration)
        {
            _begun = true;
            _begin = now;
            _silence = duration < MinSilenceSeconds ? MinSilenceSeconds
                     : (duration > MaxSilenceSeconds ? MaxSilenceSeconds : duration);
            _cancelled = false;
            _cancelAt = 0f;
            _cancelGain = 0f;
        }

        /// <summary>
        /// 조준을 거뒀다(<c>OverharvestReleased</c>). 지금 게인에서 곧바로 재개 페이드로 넘어간다.
        ///
        /// 1로 튕겨 올리지 않는 이유: 손을 뗀 순간 소리가 원래 크기로 되돌아오면
        /// "가까이 갔다가 물러났다"가 아니라 "잘못 눌렀다"로 읽힌다. 물러남도 연출이다.
        /// </summary>
        public void Cancel(float now)
        {
            if (!_begun || _cancelled) return;
            _cancelGain = GainAt(now);
            _cancelAt = now;
            _cancelled = true;
        }

        /// <summary>시작한 적 없는 상태로 되돌린다. 런 재시작 지점에서 부른다.</summary>
        public void Reset()
        {
            _begun = false;
            _cancelled = false;
            _begin = 0f;
            _cancelAt = 0f;
            _cancelGain = 0f;
        }

        /// <summary>
        /// 이 시각의 볼륨 배수. 시작 전 1, 정적 구간 정확히 0, 끝난 뒤 다시 1.
        /// 감쇠·재개 구간은 선형이다 — 지수 곡선이 더 자연스럽지만 그러면 타임라인을
        /// 눈금으로 재기 어려워지고, 이 항목의 검증 기준이 바로 그 눈금이다.
        /// </summary>
        public float GainAt(float now)
        {
            if (!_begun) return 1f;

            if (_cancelled)
            {
                float since = now - _cancelAt;
                if (since <= 0f) return _cancelGain;
                if (since >= _resumeSeconds) return 1f;
                return _cancelGain + (1f - _cancelGain) * (since / _resumeSeconds);
            }

            float elapsed = now - _begin;
            if (elapsed <= 0f) return 1f;
            if (elapsed < _duckSeconds) return 1f - elapsed / _duckSeconds;

            float silenceEnd = _duckSeconds + _silence;
            if (elapsed < silenceEnd) return 0f;

            float resumed = elapsed - silenceEnd;
            if (resumed >= _resumeSeconds) return 1f;
            return resumed / _resumeSeconds;
        }

        /// <summary>완전 정적 구간 안인가. 승객 응시·기계 정지 같은 다른 채널이 이걸 본다.</summary>
        public bool IsSilent(float now)
        {
            if (!_begun || _cancelled) return false;
            float elapsed = now - _begin;
            return elapsed >= _duckSeconds && elapsed < _duckSeconds + _silence;
        }

        /// <summary>감쇠~재개 사이 어딘가인가. 끝났으면 false — 호출자가 손을 떼도 되는 시점이다.</summary>
        public bool IsActive(float now)
        {
            if (!_begun) return false;
            if (_cancelled) return now - _cancelAt < _resumeSeconds;
            return now - _begin < TotalSeconds;
        }
    }
}
