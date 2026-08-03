using UnityEngine;

namespace Ascend.Prototype.View
{
    /// <summary>
    /// **동력 전달 경로를 화면에서 실제로 움직인다.** 레버 각도 하나를 읽어
    /// 구동 로드 → 공통 수평축 → 상태 탭 3 → 챔버 클램프 9 를 순서대로 민다.
    ///
    /// ## 왜 이 컴포넌트가 필요했나
    ///
    /// 사용자 지시의 실패 조건이 한 문장으로 적혀 있다 —
    /// 「레버를 당겼는데 **외부 연결부는 가만히 있고** 영혼만 갑자기 멈추면 실패다.」
    ///
    /// 직전까지 정확히 그 상태였다. <see cref="LeverStateMachine"/> 이 손잡이 각도를
    /// 굴리고 <see cref="SoulReelView"/> 가 구슬을 멈췄는데, **그 사이에 아무것도
    /// 없었다.** 레버와 통관 장치가 같은 기계라는 근거가 화면에 없었고, 그래서
    /// 장치가 「기계처럼 보이기 위한 장식의 집합」으로 읽혔다.
    ///
    /// ## 각 단계가 이전 단계의 **결과**로 보여야 한다
    ///
    /// 그래서 전부 같은 값(레버 각도의 정규화)에서 파생시키되 **단계마다 지연**을
    /// 준다. 동시에 움직이면 여섯 부품이 한 덩어리로 보이고, 한 덩어리는 전달이
    /// 아니라 그냥 애니메이션이다.
    ///
    /// <code>
    ///   0.00  솔레노이드 잠금핀 해제 (잠김이 풀린 경우)
    ///   0.00  레버 이동          ← LeverStateMachine 이 소유. 여기서는 읽기만 한다
    ///   0.04  구동 로드 하강
    ///   0.07  공통 수평축 회전
    ///   0.11  상태 탭 3개 동시 하강
    ///   0.16  챔버 클램프 9개 체결
    /// </code>
    ///
    /// ## 판정을 몰지 않는다
    ///
    /// 트랜스폼만 쓴다. 어떤 심볼이 어디 있는지는 <see cref="SpinBoardView"/> 가
    /// 정하고 이 클래스는 읽지도 않는다 — 같은 시드가 같은 <c>SpinResult</c> 를 낸다.
    ///
    /// ## 프레임당 0 B
    ///
    /// 정지 상태에서 이른 반환한다. 배열은 전부 미리 잡고 루프 안에서 할당하지
    /// 않는다(`UP-TECH-05` 유휴 0 B 회귀선).
    /// </summary>
    [DefaultExecutionOrder(215)]
    public sealed class CustomsLockView : MonoBehaviour
    {
        public const int Banks = 3;
        public const int Chambers = 9;

        [Header("입력 — 레버가 이 값을 만든다")]
        [Tooltip("각도를 소유하는 상태 기계. 비어 있으면 아무것도 움직이지 않는다.")]
        [SerializeField] private LeverStateMachine _lever;

        [Header("① 구동 로드 — 허브의 회전을 상단 공통축으로 올린다")]
        [SerializeField] private Transform _driveRod;
        [Tooltip("체결될 때 로드가 내려가는 거리(m). 눈에 보여야 하되 로드가 빠지면 안 된다.")]
        [SerializeField, Range(0f, 0.12f)] private float _driveRodTravel = 0.042f;

        [Header("② 공통 수평축 — 세 뱅크를 동시에 돌린다")]
        [SerializeField] private Transform _commonShaft;
        [Tooltip("체결까지 축이 도는 각도(도). 축은 X 를 따라 눕는다.")]
        [SerializeField, Range(0f, 180f)] private float _shaftRotation = 62f;

        [Header("③ 상태 탭 — 밖에서 보이는 유일한 잠금 상태")]
        [SerializeField] private Transform[] _statusTabs = new Transform[Banks];
        [SerializeField, Range(0f, 0.10f)] private float _tabTravel = 0.034f;

        [Header("④ 챔버 클램프 9개")]
        [SerializeField] private Transform[] _clamps = new Transform[Chambers];
        [SerializeField, Range(0f, 90f)] private float _clampSwing = 34f;

        [Header("⑤ 솔레노이드 잠금핀 — 잠기면 슬롯을 물리적으로 막는다")]
        [SerializeField] private Transform _lockPin;
        [Tooltip("해제될 때 핀이 물러나는 거리(m). 슬롯 밖으로 완전히 빠져야 한다.")]
        [SerializeField, Range(0f, 0.14f)] private float _pinRetract = 0.062f;

        [Header("단계 지연(초) — 이것이 「전달」과 「동시 재생」을 가른다")]
        [SerializeField, Range(0f, 0.30f)] private float _rodDelay = 0.04f;
        [SerializeField, Range(0f, 0.30f)] private float _shaftDelay = 0.07f;
        [SerializeField, Range(0f, 0.30f)] private float _tabDelay = 0.11f;
        [SerializeField, Range(0f, 0.40f)] private float _clampDelay = 0.16f;
        [Tooltip("한 단계가 목표까지 가는 데 걸리는 시간(초).")]
        [SerializeField, Range(0.05f, 0.60f)] private float _stageDuration = 0.18f;

        // ── 원위치. `Awake` 에서 한 번 잡는다 ──
        //
        // ⚠ **직렬화하지 않는다.** 씬에 구운 값과 실제 트랜스폼이 갈라지면
        // 「조립기를 다시 돌렸더니 부품이 어긋난 자리에서 시작한다」가 된다.
        private Vector3 _rodHome, _pinHome;
        private Quaternion _shaftHome;
        private readonly Vector3[] _tabHome = new Vector3[Banks];
        private readonly Quaternion[] _clampHome = new Quaternion[Chambers];
        private bool _homeCaptured;

        /// <summary>체결 진행도 0~1. 지난 프레임에 적용한 값 — 헤드리스 단정이 읽는다.</summary>
        public float Engagement { get; private set; }

        /// <summary>잠금핀이 물러난 정도 0~1. 1 이면 슬롯이 완전히 열렸다.</summary>
        public float PinRetraction { get; private set; }

        /// <summary>체결이 걸려 있는가. 헤드리스 단정이 읽는다.</summary>
        public bool IsEngaged => _engaged;

        /// <summary>마지막 단계까지 끝나는 데 걸리는 시간(초).</summary>
        public float TotalDuration => _clampDelay + _stageDuration;

        /// <summary>단계 <paramref name="stage"/> 의 진행도 0~1 (0=로드 1=축 2=탭 3=클램프).</summary>
        public float StageProgress(int stage)
        {
            float d = stage <= 0 ? _rodDelay : stage == 1 ? _shaftDelay : stage == 2 ? _tabDelay : _clampDelay;
            return Stage(_age, d);
        }

        private float _age = -1f;
        private bool _unlocked = true;
        private bool _dirty = true;

        /// <summary>
        /// 체결이 걸려 있는가. **레버 상태를 매 프레임 되묻지 않고 여기에 건다.**
        ///
        /// ⚠ 첫 판본은 `_lever.Current` 를 직접 폴링해서 「걸린 상태인가」를 판정했다.
        /// 그러면 `Engage()` 를 부른 직후에도 레버가 아직 Idle 이면 그 프레임에
        /// **곧바로 되감긴다.** 실제로 그랬다 — 실측에서 로드·축·탭·클램프가
        /// 전부 0.0mm / 0.0° 로 아무것도 움직이지 않았다.
        ///
        /// 그리고 그 실패는 **레버가 없는 헤드리스에서는 드러나지 않는다.**
        /// 소유권을 뒤집는다: `Engage()` 가 이 깃발을 세우고, 레버는 「언제 풀리는가」
        /// 만 알려 준다. 그러면 레버 없이도 시험할 수 있다.
        /// </summary>
        private bool _engaged;

        private void Awake() => CaptureHome();

        private void CaptureHome()
        {
            if (_homeCaptured) return;
            if (_driveRod != null) _rodHome = _driveRod.localPosition;
            if (_commonShaft != null) _shaftHome = _commonShaft.localRotation;
            if (_lockPin != null) _pinHome = _lockPin.localPosition;
            for (int i = 0; i < Banks; i++)
                if (i < _statusTabs.Length && _statusTabs[i] != null) _tabHome[i] = _statusTabs[i].localPosition;
            for (int i = 0; i < Chambers; i++)
                if (i < _clamps.Length && _clamps[i] != null) _clampHome[i] = _clamps[i].localRotation;
            _homeCaptured = true;
        }

        /// <summary>
        /// 잠금 상태를 바꾼다. 전력이 모자라면 <c>false</c> — 핀이 슬롯을 막는다.
        /// UI 의 「잠김」 문구를 보지 않아도 왜 레버가 안 움직이는지 형상으로 읽혀야 한다.
        /// </summary>
        public void SetUnlocked(bool unlocked)
        {
            if (_unlocked == unlocked) return;
            _unlocked = unlocked;
            _dirty = true;
        }

        /// <summary>레버가 걸린 순간. `LeverStateMachine.onLatched` 에 인스펙터로 건다.</summary>
        public void Engage()
        {
            CaptureHome();
            if (_engaged) return;      // 연타해도 처음으로 되돌리지 않는다
            _engaged = true;
            _age = 0f;
            _dirty = true;
        }

        /// <summary>
        /// 체결을 푼다. 레버가 원위치로 돌아오면 자동으로 불리고,
        /// `LeverStateMachine.onReturned` 에 걸어도 된다.
        /// </summary>
        public void Release()
        {
            if (!_engaged) return;
            _engaged = false;
            _dirty = true;
        }

        /// <summary>즉시 해제 자세로. 캡처 하네스가 정적인 장치를 찍을 때 쓴다.</summary>
        public void SnapToRest()
        {
            CaptureHome();
            _engaged = false;
            _age = -1f;
            Engagement = 0f;
            _dirty = true;
            Apply(-1f);
        }

        private void LateUpdate() => Step(Time.deltaTime);

        /// <summary>한 스텝. **테스트가 고정 dt 로 직접 부른다.**</summary>
        public void Step(float dt)
        {
            CaptureHome();

            // 레버가 **원위치로 돌아왔으면** 자동으로 푼다. 레버는 「언제 풀리는가」만
            // 알려 주고, 「걸려 있는가」는 `_engaged` 가 소유한다 — 그 소유권 분리가
            // 이 컴포넌트를 레버 없이도 시험 가능하게 만든다.
            if (_engaged && _lever != null &&
                (_lever.Current == LeverStateMachine.State.Idle ||
                 _lever.Current == LeverStateMachine.State.Ready ||
                 _lever.Current == LeverStateMachine.State.Locked))
                _engaged = false;

            float total = TotalDuration;
            if (_engaged) _age = Mathf.Min(_age < 0f ? dt : _age + dt, total);
            else if (_age >= 0f) _age -= dt * 2f;   // 복귀는 두 배 빠르다. 사람이 미는 게 아니다
            if (!_engaged && _age < 0f) _age = -1f;

            float pinTarget = _unlocked ? 1f : 0f;

            // 🔴 **정지 판정은 나이로 한다. 진행도로 하면 안 된다.**
            //
            // 첫 판본은 `Engagement`(= 마지막 단계인 클램프의 진행도)가 목표에
            // 닿으면 정지로 보고 이른 반환했다. 되감을 때 **클램프가 먼저 0 에
            // 닿는다** — 지연이 가장 크기 때문이다. 그 순간 갱신이 멈춰
            // **구동 로드가 38.3mm 남은 채 얼어붙었다.**
            //
            // 헤드리스 스위트가 이것을 잡았다(「풀면 원위치로 정확히 돌아온다」).
            // 화면으로는 못 봤을 결함이다 — 레버는 이미 올라가 있고 로드만
            // 조금 내려가 있어서, 그 상태가 「원래 그런 모양」과 구분되지 않는다.
            bool settled = _engaged ? _age >= total - 1e-4f : _age <= -1f + 1e-4f;

            // 정지 상태에서 이른 반환. 유휴 프레임에 트랜스폼을 쓰지 않는다.
            if (!_dirty && settled && Mathf.Approximately(PinRetraction, pinTarget)) return;

            PinRetraction = Mathf.MoveTowards(PinRetraction, pinTarget, dt / Mathf.Max(0.01f, _stageDuration));
            Apply(_age);

            _dirty = !settled || !Mathf.Approximately(PinRetraction, pinTarget);
        }

        /// <summary>
        /// 나이(초)를 부품 다섯의 자세로 바꾼다. **모든 단계가 같은 값에서 파생되고
        /// 지연만 다르다** — 그래서 순서가 항상 지켜지고, 되감을 때도 역순이 된다.
        /// </summary>
        private void Apply(float age)
        {
            float rod = Stage(age, _rodDelay);
            float shaft = Stage(age, _shaftDelay);
            float tab = Stage(age, _tabDelay);
            float clamp = Stage(age, _clampDelay);
            Engagement = clamp;

            if (_driveRod != null)
                _driveRod.localPosition = _rodHome + Vector3.down * (_driveRodTravel * rod);

            if (_commonShaft != null)
                _commonShaft.localRotation = _shaftHome * Quaternion.AngleAxis(_shaftRotation * shaft, Vector3.right);

            for (int i = 0; i < Banks && i < _statusTabs.Length; i++)
                if (_statusTabs[i] != null)
                    _statusTabs[i].localPosition = _tabHome[i] + Vector3.down * (_tabTravel * tab);

            for (int i = 0; i < Chambers && i < _clamps.Length; i++)
                if (_clamps[i] != null)
                    _clamps[i].localRotation = _clampHome[i] * Quaternion.AngleAxis(_clampSwing * clamp, Vector3.forward);

            // 잠금핀은 체결과 **별개 축**이다. 전력이 모자라면 레버를 당기기 전부터
            // 슬롯을 막고 있어야 하므로 나이가 아니라 잠금 상태를 따른다.
            if (_lockPin != null)
                _lockPin.localPosition = _pinHome + Vector3.right * (_pinRetract * PinRetraction);
        }

        /// <summary>지연 <paramref name="delay"/> 를 지난 단계의 진행도 0~1.</summary>
        private float Stage(float age, float delay)
        {
            if (age < 0f) return 0f;
            float t = Mathf.Clamp01((age - delay) / Mathf.Max(0.01f, _stageDuration));
            // 감속만 준다. 기계 링크는 가속 구간이 짧고 도착에서 멈춘다.
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        /// <summary>조립기·테스트용. 인스펙터 배선 없이 부품을 물린다.</summary>
        public void Configure(LeverStateMachine lever, Transform driveRod, Transform commonShaft,
                              Transform lockPin, Transform[] tabs, Transform[] clamps)
        {
            _lever = lever;
            _driveRod = driveRod;
            _commonShaft = commonShaft;
            _lockPin = lockPin;
            if (tabs != null) _statusTabs = tabs;
            if (clamps != null) _clamps = clamps;
            _homeCaptured = false;
            CaptureHome();
        }
    }
}
