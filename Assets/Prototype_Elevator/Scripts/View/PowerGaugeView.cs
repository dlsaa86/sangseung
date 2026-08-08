using UnityEngine;

namespace Ascend.Prototype.View
{
    /// <summary>
    /// 3×3 아래 전력 모듈의 막대를 0→100 으로 채우고, 100 을 넘으면 색을 바꾸며 뛴다.
    ///
    /// ## 왜 「100 초과」가 별도 상태인가
    ///
    /// 이 게임에서 요구 전력을 넘기는 것은 실패가 아니라 **선택지가 열리는 순간**이다
    /// (`FloorSession.CanBank`, `IsOverharvestUnlocked`). 그래서 100 은 막대가 멈추는
    /// 지점이 아니라 **색과 거동이 바뀌는 문턱**이다. 넘긴 만큼은 막대 끝에서 계속
    /// 밀려 나가는 대신 맥동으로 표현한다 — 길이로 표현하면 눈금이 거짓말을 한다.
    ///
    /// ## 좌표를 월드로 다루는 이유
    ///
    /// 막대는 FBX 프리팹 인스턴스 안에 있고 부모(<c>CabinAD47</c>)가 Y 180° 로 돌아
    /// 있다. 로컬 X 로 계산하면 「왼쪽에서 차오른다」가 오른쪽에서 차오르는 것으로
    /// 뒤집히고, 그 실수는 값이 절반일 때만 드러난다. 월드 X 로 고정한다.
    /// </summary>
    [ExecuteAlways]
    public sealed class PowerGaugeView : MonoBehaviour
    {
        [Header("배선")]
        [SerializeField] private Renderer _fill;

        /// <summary>눈금 0 의 월드 X. 기본값은 <c>SM_Gauge_Labels</c> 실측 왼쪽 끝.</summary>
        [SerializeField] private float _trackStartX = -0.765f;

        /// <summary>눈금 100 의 월드 X.</summary>
        [SerializeField] private float _trackEndX = 0.315f;

        [Header("색")]
        [SerializeField] private Color _lowEmission  = new Color(1.20f, 0.34f, 0.05f);
        [SerializeField] private Color _fullEmission = new Color(1.55f, 0.72f, 0.14f);
        /// <summary>100 초과. 넘겼다는 것이 색 하나로 읽혀야 한다.</summary>
        [SerializeField] private Color _overEmission = new Color(2.40f, 0.28f, 0.16f);

        [Header("초과 연출")]
        [SerializeField, Range(0.5f, 12f)] private float _overPulseHz = 3.2f;
        [SerializeField, Range(0f, 1f)]    private float _overPulseDepth = 0.45f;
        /// <summary>초과분이 이만큼이면 맥동이 최대가 된다 (요구 대비 배수).</summary>
        [SerializeField, Range(0.05f, 2f)] private float _overFullAt = 0.60f;

        [Header("검사용")]
        [SerializeField] private bool _previewInEditor = true;
        [SerializeField, Range(0f, 2f)] private float _previewRatio = 0.62f;

        private MaterialPropertyBlock _mpb;
        private Run.RunSessionBehaviour _run;
        private float _baseWidth;      // 스케일 1 일 때 막대의 월드 폭
        private float _baseY, _baseZ;
        private Vector3 _baseScale;
        private bool _measured;

        /// <summary>마지막으로 그린 비율. 검사에서 읽는다.</summary>
        public float Ratio { get; private set; }

        private void OnEnable() { Measure(); }

        private void Measure()
        {
            if (_fill == null) return;
            var b = _fill.bounds;
            _baseScale = _fill.transform.localScale;
            _baseWidth = b.size.x / Mathf.Max(_baseScale.x, 1e-4f);
            _baseY = b.center.y;
            _baseZ = b.center.z;
            _measured = _baseWidth > 1e-4f;
        }

        private void LateUpdate()
        {
            if (_fill == null) return;
            if (!_measured) Measure();
            if (!_measured) return;

            float ratio = ReadRatio();
            Ratio = ratio;

            // 길이는 100 에서 멈춘다. 넘긴 것은 맥동이 말한다.
            float shown = Mathf.Clamp01(ratio);
            float track = _trackEndX - _trackStartX;
            float len = Mathf.Max(track * shown, 1e-4f);

            var s = _baseScale;
            s.x = _baseScale.x * (len / _baseWidth);
            _fill.transform.localScale = s;

            // 왼쪽 끝을 눈금 0 에 고정한다.
            //
            // ⚠ `transform.position` 을 목표 중심에 놓으면 안 된다. 이 메시의 원점은
            // 중심이 아니라 한쪽 끝에 있어서 막대가 길이의 절반만큼 밀린다
            // (2026-08-08 실측 — 100% 에서 −0.225..0.855 로 나왔다. 정답은 −0.765..0.315).
            // 원점이 어디인지 가정하지 말고, 놓고 나서 **실제 바운드를 읽어 보정**한다.
            float wantCenter = _trackStartX + len * 0.5f;
            _fill.transform.position = new Vector3(wantCenter, _baseY, _baseZ);
            float drift = wantCenter - _fill.bounds.center.x;
            if (Mathf.Abs(drift) > 1e-5f)
                _fill.transform.position += new Vector3(drift, 0f, 0f);

            // 색
            Color c;
            if (ratio <= 1f)
            {
                c = Color.Lerp(_lowEmission, _fullEmission, Mathf.Clamp01(ratio));
            }
            else
            {
                float over = Mathf.Clamp01((ratio - 1f) / _overFullAt);
                c = Color.Lerp(_fullEmission, _overEmission, over);
                float t = Application.isPlaying ? Time.time : (float)UnityEditor_TimeSafe();
                float pulse = 1f + _overPulseDepth * over * Mathf.Sin(t * _overPulseHz * Mathf.PI * 2f);
                c *= pulse;
            }

            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _fill.GetPropertyBlock(_mpb);
            _mpb.SetColor("_EmissionColor", c);
            _mpb.SetColor("_BaseColor", c * 0.35f);
            _fill.SetPropertyBlock(_mpb);
        }

        /// <summary>에디터에서도 맥동이 보이게 — 플레이 중이 아니면 실시간 시계를 쓴다.</summary>
        private static double UnityEditor_TimeSafe()
        {
#if UNITY_EDITOR
            return UnityEditor.EditorApplication.timeSinceStartup;
#else
            return Time.time;
#endif
        }

        private float ReadRatio()
        {
            if (!Application.isPlaying)
                return _previewInEditor ? _previewRatio : 0f;

            if (_run == null) _run = FindAnyObjectByType<Run.RunSessionBehaviour>();
            var floor = _run != null && _run.Session != null ? _run.Session.Current : null;
            if (floor == null || floor.RequiredPower <= 0f) return 0f;
            return floor.Power / floor.RequiredPower;
        }
    }
}
