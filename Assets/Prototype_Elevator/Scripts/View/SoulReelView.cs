using UnityEngine;

namespace Ascend.Prototype.View
{
    /// <summary>
    /// 레버를 내리면 **구슬이 통관 안에서 위에서 아래로 흐르다가 열마다 차례로 멈춘다.**
    /// 슬롯머신의 릴 감각이되, 형상은 릴이 아니라 3×3 격자 그대로다.
    ///
    /// ## 형상은 릴이 아니고 **움직임만** 릴이다
    ///
    /// 명세 §13 은 「카지노나 슬롯머신처럼 보이는 장식」을 금지하고, 이 저장소는
    /// `G-SLOT` 축으로 「세로 띠」를 화소 단위로 측정해 왔다. 그 금지는 **형상**에
    /// 걸린 것이다 — 세로로 긴 릴 창, 열로 뭉치는 배치.
    ///
    /// 사용자가 요청한 것은 **움직임**이다(「구슬이 위에서 아래로 흘렀다가 멈추는」).
    /// 그래서 구슬은 **각자의 원형 창 안에서만** 흐른다. 창은 여전히 등방 격자로
    /// 흩어져 있고 세로로 이어진 통이 아니다. 두 요구가 충돌하지 않는다.
    ///
    /// ## 왜 열마다 멈추는 시각이 다른가
    ///
    /// 동시에 멈추면 결과가 한 순간에 통째로 나타나 **읽을 시간이 없다.**
    /// 사용자가 앞서 지적한 「정확히 어떤 일이 일어나는지 인지가 안 된다」가 그것이다.
    /// 왼쪽부터 차례로 멈추면 눈이 따라갈 수 있고, 마지막 열이 멈추는 순간이
    /// 자연스럽게 「결과 확정」이 된다.
    ///
    /// ## 판정을 몰지 않는다
    ///
    /// 이 컴포넌트는 **트랜스폼만** 움직인다. 어떤 심볼이 어디 있는지는
    /// <see cref="SpinBoardView"/> 가 정하고, 이 클래스는 그것이 이미 정해진 뒤에
    /// 그 자리로 **미끄러져 들어가는 모양**만 만든다. 같은 시드가 같은 결과를 낸다.
    ///
    /// ## 프레임당 0 B
    ///
    /// 유휴에서 이른 반환한다. 배열은 전부 미리 잡고 루프 안에서 할당하지 않는다.
    /// </summary>
    [DefaultExecutionOrder(210)]
    public sealed class SoulReelView : MonoBehaviour
    {
        public const int Columns = 3;
        public const int Rows = 3;

        [Header("칸 (열 우선 · 열 0 = 왼쪽, 행 0 = 위)")]
        [Tooltip("9개. 각 구슬의 트랜스폼. 비어 있으면 그 칸은 조용히 건너뛴다.")]
        [SerializeField] private Transform[] _souls = new Transform[Columns * Rows];

        [Header("흐름")]
        [Tooltip("구슬이 창 안에서 오르내리는 폭(m). 창 유리 지름보다 커야 한 바퀴 도는 것으로 읽힌다.")]
        [SerializeField, Range(0.05f, 0.6f)] private float _travel = 0.34f;

        [Tooltip("초당 몇 바퀴 도는가. 너무 느리면 '떨어진다', 너무 빠르면 '깜빡인다'로 보인다.")]
        [SerializeField, Range(2f, 40f)] private float _spinRate = 13f;

        [Tooltip("첫 열이 멈추는 시각(초).")]
        [SerializeField, Range(0.1f, 2f)] private float _firstStop = 0.42f;

        [Tooltip("열 사이 멈춤 간격(초). 0 이면 세 열이 동시에 멈춰 순서가 사라진다.")]
        [SerializeField, Range(0.02f, 0.6f)] private float _stopStagger = 0.16f;

        [Tooltip("멈출 때 지나쳤다 돌아오는 양(0~1). 이것이 '철컥' 하고 물리는 느낌을 만든다.")]
        [SerializeField, Range(0f, 1f)] private float _settleOvershoot = 0.42f;

        [Tooltip("정착 시간(초). 마지막 반동이 잦아드는 데 걸리는 시간.")]
        [SerializeField, Range(0.05f, 0.8f)] private float _settle = 0.26f;

        [Header("멈추는 순간")]
        [Tooltip("열이 멈출 때 함께 때릴 대상. 없어도 된다.")]
        [SerializeField] private MachineImpactView _impact;

        private Vector3[] _home;
        private bool _homeCaptured;
        private float _age = -1f;          // 음수 = 도는 중 아님

        /// <summary>지금 도는가. 헤드리스 단정이 읽는다.</summary>
        public bool IsSpinning => _age >= 0f;

        /// <summary>시작 이후 경과(초). 음수면 도는 중이 아니다.</summary>
        public float Age => _age;

        /// <summary>모든 열이 멈추는 데 걸리는 총 시간(초).</summary>
        public float TotalDuration => _firstStop + _stopStagger * (Columns - 1) + _settle;

        /// <summary>열 <paramref name="column"/> 이 멈추는 시각(초).</summary>
        public float StopTimeOf(int column) => _firstStop + _stopStagger * Mathf.Clamp(column, 0, Columns - 1);

        /// <summary>열 <paramref name="column"/> 이 아직 도는가.</summary>
        public bool ColumnSpinning(int column) => _age >= 0f && _age < StopTimeOf(column);

        /// <summary>
        /// 레버를 내린 순간 호출한다. `LeverPhysics.onBottomedOut` 에 인스펙터로 건다.
        /// </summary>
        public void Spin()
        {
            EnsureHome();
            _age = 0f;
        }

        /// <summary>즉시 제자리로. 캡처 하네스가 정적인 판을 찍을 때 쓴다.</summary>
        public void SnapToRest()
        {
            EnsureHome();
            _age = -1f;
            for (int i = 0; i < _souls.Length; i++)
                if (_souls[i] != null) _souls[i].localPosition = _home[i];
        }

        /// <summary>헤드리스 테스트 전용. 씬 없이 순서와 정착을 확인한다.</summary>
        public void StepForTest(float dt, int steps)
        {
            EnsureHome();
            for (int i = 0; i < steps; i++) Integrate(dt);
        }

        /// <summary>칸 하나의 현재 세로 오프셋(m). 0 이면 제자리다.</summary>
        public float OffsetOf(int column, int row)
        {
            int i = Index(column, row);
            if (_home == null || i < 0 || i >= _souls.Length || _souls[i] == null) return 0f;
            return _souls[i].localPosition.y - _home[i].y;
        }

        private static int Index(int column, int row) => column * Rows + row;

        private void Awake() => EnsureHome();

        /// <summary>
        /// 기준 위치를 잡는다. **에디트 모드에서도 돌아야** 하므로 `Awake` 에만 두지 않는다 —
        /// `MachineImpactView` 에서 같은 실수를 해서 `StepForTest` 가 null 로 죽은 적이 있다.
        /// </summary>
        private void EnsureHome()
        {
            if (_home == null || _home.Length != _souls.Length) _home = new Vector3[_souls.Length];
            if (_homeCaptured) return;
            for (int i = 0; i < _souls.Length; i++)
                if (_souls[i] != null) _home[i] = _souls[i].localPosition;
            _homeCaptured = true;
        }

        private void LateUpdate()
        {
            if (_age < 0f) return;      // 유휴에서는 아무것도 하지 않는다 (0 B)
            Integrate(Time.deltaTime);
        }

        private void Integrate(float dt)
        {
            if (_age < 0f) return;
            _age += dt;

            for (int col = 0; col < Columns; col++)
            {
                float stopAt = StopTimeOf(col);
                float y = ColumnOffset(col, stopAt);

                for (int row = 0; row < Rows; row++)
                {
                    int i = Index(col, row);
                    if (i >= _souls.Length || _souls[i] == null) continue;
                    Vector3 p = _home[i];
                    p.y += y;
                    _souls[i].localPosition = p;
                }
            }

            if (_age >= TotalDuration) SnapToRest();
        }

        /// <summary>
        /// 한 열의 세로 오프셋. 세 국면이다 —
        /// **① 흐름**(톱니파로 위→아래 반복) → **② 감속** → **③ 정착 반동**.
        ///
        /// 톱니파를 쓰는 이유: 사인파는 위아래로 왕복해서 「흔들린다」로 보인다.
        /// 릴은 **한 방향으로만** 흘러야 하고, 그것이 톱니파다.
        /// </summary>
        private float ColumnOffset(int col, float stopAt)
        {
            if (_age < stopAt)
            {
                // ① 흐름. 멈추기 직전 0.12초 동안 속도를 줄여 급정거를 피한다.
                float remain = stopAt - _age;
                float rate = _spinRate * Mathf.Clamp01(remain / 0.12f + 0.35f);
                float phase = _age * rate;
                // 톱니: [0,1) 를 위(+travel/2) → 아래(−travel/2) 로 한 번에 훑는다.
                return Mathf.Lerp(_travel * 0.5f, -_travel * 0.5f, phase - Mathf.Floor(phase));
            }

            // ②③ 정착. 아래에서 살짝 지나쳤다가 감쇠 진동으로 0 에 붙는다.
            float t = (_age - stopAt) / Mathf.Max(0.0001f, _settle);
            if (t >= 1f) return 0f;
            float decay = Mathf.Exp(-t * 5.5f);
            return -_travel * 0.5f * _settleOvershoot * decay * Mathf.Cos(t * Mathf.PI * 2.4f);
        }
    }
}
