using UnityEngine;

namespace Ascend.Prototype.Physics
{
    /// <summary>
    /// 매달린 사슬·케이블·전선. 여러 마디가 **시간차를 두고** 흔들린다.
    ///
    /// 왜 진짜 다물체 진자가 아닌가: N-링크 진자는 카오스적이라 같은 초기 조건에서도
    /// 부동소수 오차가 지수적으로 증폭한다. 캡처 하네스가 두 번 돌면 두 번 다른 그림이
    /// 나오고, 그건 이 저장소가 요구하는 결정론이 아니다. 게다가 이중 진자 이상은
    /// 감쇠를 걸어도 국소적으로 발산하는 구간이 있어 「사슬이 폭발한다」가 실제로 일어난다.
    ///
    /// 대신 **선행-추종(follow-the-leader)** 근사를 쓴다. 첫 마디가 진자로 돌고,
    /// 아래 마디들은 위 마디의 각도를 각자의 감쇠 스프링으로 따라간다. 무조건 안정하고,
    /// 결정론적이고, 눈으로는 다물체 진자와 구분되지 않는다 — 사슬에서 사람이 읽는
    /// 것은 정확한 동역학이 아니라 **위에서 아래로 전파되는 지연**이기 때문이다.
    ///
    /// 케이블 스웨이도 같은 컴포넌트다. 마디 2~3개에 길이를 길게, 감쇠를 낮게 주면
    /// 사슬이 아니라 늘어진 케이블로 읽힌다 — 파라미터 차이일 뿐 별개 코드가 아니다.
    /// </summary>
    public sealed class HangingChain : CabinInertiaReactor
    {
        [Header("마디 — 위에서 아래 순서로 넣는다")]
        [Tooltip("각 마디의 피벗. 순서가 곧 전파 순서다. 뒤집으면 아래에서 위로 흔들려 즉시 틀려 보인다.")]
        [SerializeField] private Transform[] _links;

        [Header("첫 마디 진자")]
        [Tooltip("첫 마디의 유효 줄 길이(m).")]
        [SerializeField, Range(0.1f, 3f)] private float _length = 0.55f;

        [Tooltip("첫 마디 감쇠(1/s).")]
        [SerializeField, Range(0.05f, 6f)] private float _damping = 1.1f;

        [Tooltip("최대 각도(도). 판독성 상한.")]
        [SerializeField, Range(1f, 40f)] private float _maxAngleDegrees = 18f;

        [Header("전파")]
        [Tooltip("아래 마디가 위 마디를 따라가는 속도(rad/s). 낮을수록 늘어진 케이블로 읽힌다.")]
        [SerializeField, Range(2f, 40f)] private float _followOmega = 11f;

        [Tooltip("따라가기 감쇠비. 0.35 면 마디마다 살짝 오버슛해서 채찍처럼 보인다.")]
        [SerializeField, Range(0.1f, 1.5f)] private float _followZeta = 0.38f;

        [Tooltip("아래로 갈수록 각도가 커지는 배율. 1 보다 크면 끝이 크게 튄다.")]
        [SerializeField, Range(0.5f, 1.8f)] private float _tipGain = 1.15f;

        private PendulumState _head;
        private DampedSpring1D[] _followX;
        private DampedSpring1D[] _followZ;
        private Quaternion[] _homeRotation;

        /// <summary>마디 수. 배선 진단이 읽는다.</summary>
        public int LinkCount => _links != null ? _links.Length : 0;

        /// <summary>
        /// 마디를 코드로 꽂는다. 씬을 손으로 배치하는 대신 **재실행 가능한 조립
        /// 스크립트**를 쓰라는 `CLAUDE.md` Pass 1 지침을 따르는 쪽이 이 경로다.
        /// 헤드리스 테스트도 여기로 들어온다.
        /// </summary>
        public void ConfigureLinks(Transform[] links)
        {
            _links = links;
            ForceRecaptureHome();
        }

        public override bool IsAtRest
        {
            get
            {
                if (!_head.IsAtRest()) return false;
                if (_followX == null) return true;
                for (int i = 0; i < _followX.Length; i++)
                    if (!_followX[i].IsAtRest() || !_followZ[i].IsAtRest()) return false;
                return true;
            }
        }

        /// <summary>끝 마디의 각도(도). 진폭 상한 검증이 여기를 본다 — 끝이 가장 크다.</summary>
        public Vector2 TipAngleDegrees
        {
            get
            {
                if (_followX == null || _followX.Length == 0)
                    return new Vector2(_head.AngleX * Mathf.Rad2Deg, _head.AngleZ * Mathf.Rad2Deg);
                int last = _followX.Length - 1;
                return new Vector2(_followX[last].Value * Mathf.Rad2Deg,
                                   _followZ[last].Value * Mathf.Rad2Deg);
            }
        }

        protected override void CaptureHome()
        {
            int n = _links != null ? _links.Length : 0;
            _homeRotation = new Quaternion[n];
            // 첫 마디는 진자가 직접 몬다. 나머지가 추종 스프링을 갖는다.
            int followCount = n > 1 ? n - 1 : 0;
            _followX = new DampedSpring1D[followCount];
            _followZ = new DampedSpring1D[followCount];
            for (int i = 0; i < n; i++)
                _homeRotation[i] = _links[i] != null ? _links[i].localRotation : Quaternion.identity;
        }

        protected override void RestoreHome()
        {
            _head.Reset();
            if (_followX != null)
                for (int i = 0; i < _followX.Length; i++)
                {
                    _followX[i].Reset();
                    _followZ[i].Reset();
                }
            if (_links == null || _homeRotation == null) return;
            for (int i = 0; i < _links.Length && i < _homeRotation.Length; i++)
                if (_links[i] != null) _links[i].localRotation = _homeRotation[i];
        }

        protected override void Integrate(float dt)
        {
            Vector3 a = LocalAcceleration;
            float maxRad = _maxAngleDegrees * Mathf.Deg2Rad;
            _head.Step(dt, a.z, -a.x, a.y, _length, _damping, maxRad);

            if (_followX == null) return;
            float prevX = _head.AngleX;
            float prevZ = _head.AngleZ;
            for (int i = 0; i < _followX.Length; i++)
            {
                float tx = prevX * _tipGain;
                float tz = prevZ * _tipGain;
                _followX[i].Step(dt, tx, _followOmega, _followZeta, maxRad);
                _followZ[i].Step(dt, tz, _followOmega, _followZeta, maxRad);
                prevX = _followX[i].Value;
                prevZ = _followZ[i].Value;
            }
        }

        protected override void Apply()
        {
            if (_links == null || _homeRotation == null) return;

            for (int i = 0; i < _links.Length && i < _homeRotation.Length; i++)
            {
                Transform t = _links[i];
                if (t == null) continue;

                float ax, az;
                if (i == 0) { ax = _head.AngleX; az = _head.AngleZ; }
                else
                {
                    int f = i - 1;
                    if (_followX == null || f >= _followX.Length) continue;
                    // 부모가 이미 회전했으므로 **차이만** 준다. 절대각을 그대로 주면
                    // 회전이 마디 수만큼 곱해져 사슬이 자기 자신을 감는다.
                    ax = _followX[f].Value - (f == 0 ? _head.AngleX : _followX[f - 1].Value);
                    az = _followZ[f].Value - (f == 0 ? _head.AngleZ : _followZ[f - 1].Value);
                }

                t.localRotation = _homeRotation[i] *
                    Quaternion.Euler(ax * Mathf.Rad2Deg, 0f, az * Mathf.Rad2Deg);
            }
        }

        public override void AddShock(Vector3 worldImpulse)
        {
            Vector3 local = transform.parent != null
                ? transform.parent.InverseTransformDirection(worldImpulse)
                : worldImpulse;
            float invL = 1f / Mathf.Max(_length, 0.05f);
            _head.AddImpulse(local.z * invL, -local.x * invL);
        }
    }
}
