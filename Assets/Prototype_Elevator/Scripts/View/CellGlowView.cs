using UnityEngine;
using Ascend.Prototype.Events;
using Ascend.Prototype.Run;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.View
{
    /// <summary>
    /// 통관 아홉 칸의 **내부 발광**을 구동한다.
    ///
    /// ## 왜 만들었나 — 두 요구가 같은 곳을 가리켰다
    ///
    /// ① 사용자: 「슬롯 돌아갈 때 직사각형 두 개 뜨는 게 어색하다. 차라리 **동그란
    ///    통관 내부가 밝아지는** 연출이 좋겠다」
    /// ② 사용자: 「통관 상단부에 검은 가로줄이 보인다」
    ///
    /// ②의 정체는 그림자 아크네가 아니었다(바이어스를 올린 뒤 그림자를 꺼도 그림이
    /// 같았다 — 아크네는 이미 없다). 남은 것은 **통관 안쪽이 어떤 광원에도 닿지
    /// 않아서** 생기는 음영이고, 원형 오목의 위쪽이 가장 어두워 가로 띠로 읽힌다.
    ///
    /// 안쪽에 **자체 발광하는 판**을 넣으면 그 띠가 사라진다. 그리고 그 판의 밝기를
    /// 흔드는 것이 곧 ①이 요청한 연출이다. 하나로 둘을 갚는다.
    ///
    /// ## 세 단계 밝기
    ///
    ///   쉼    — 아주 약하게. 「기계가 켜져 있다」와 검은 띠 제거만 한다
    ///   스핀  — 밝게 맥동. 도는 동안 창 안이 살아 있다
    ///   적중  — 가장 밝게 한 번 치고 감쇠. **어느 칸이 전력이 됐는가**를 말한다
    ///
    /// 마지막이 특히 중요하다. 결과판을 `InitialBoard` 로 바꾸면서(사용자 요청:
    /// 「다음 슬롯 돌리기 전까지 남아 있어야 한다」) **캐스케이드로 무엇이 사라졌는지가
    /// 화면에서 빠졌다.** 그 정보를 이 점등이 대신 진다.
    ///
    /// ## 프레임당 0 B
    ///
    /// `MaterialPropertyBlock` 하나를 재사용하고 배열은 미리 잡는다.
    /// 쉼 상태에서 값이 안 변하면 `SetPropertyBlock` 자체를 건너뛴다.
    /// </summary>
    [DefaultExecutionOrder(215)]
    public sealed class CellGlowView : MonoBehaviour
    {
        public const int Cells = 9;

        [Tooltip("칸별 내부 발광판. SpinBoard.Index(column,row) 순서.")]
        [SerializeField] private Renderer[] _glows = new Renderer[Cells];

        [SerializeField] private RunSessionBehaviour _run;
        [Tooltip("도는 중인지 판단한다. 비면 스핀 맥동 없이 쉼/적중만 쓴다.")]
        [SerializeField] private SoulReelView _reel;

        [Header("색")]
        [Tooltip("쉼. 검은 띠만 지울 정도로 약하게.")]
        [SerializeField] private Color _idle = new Color(0.055f, 0.028f, 0.016f);
        [Tooltip("스핀 중.")]
        [SerializeField] private Color _spin = new Color(0.62f, 0.30f, 0.12f);
        [Tooltip("적중 — 이 칸이 전력이 됐다.")]
        [SerializeField] private Color _hit = new Color(1.35f, 0.92f, 0.38f);

        [Header("시간")]
        [Tooltip("스핀 맥동 주기(Hz).")]
        [SerializeField, Range(0.5f, 12f)] private float _spinPulseHz = 5.5f;
        [Tooltip("적중 점등이 쉼으로 돌아가는 데 걸리는 시간(초).")]
        [SerializeField, Range(0.2f, 4f)] private float _hitFade = 1.15f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock _block;
        private GameEventBus _bus;
        private readonly float[] _hitLevel = new float[Cells];   // 1 → 0 으로 감쇠
        private readonly Color[] _applied = new Color[Cells];
        private int _lastSpinSeen = -1;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (_run == null) _run = FindFirstObjectByType<RunSessionBehaviour>();
            if (_reel == null) _reel = FindFirstObjectByType<SoulReelView>();
            for (int i = 0; i < Cells; i++) _applied[i] = new Color(-1f, -1f, -1f);
        }

        private void OnEnable()
        {
            if (_run != null) _run.RunStarted += OnRunStarted;
            Subscribe(_run != null && _run.Session != null ? _run.Session.Events : null);
        }

        private void OnDisable()
        {
            if (_run != null) _run.RunStarted -= OnRunStarted;
            Subscribe(null);
        }

        private void OnRunStarted(RunSession s)
        {
            Subscribe(s != null ? s.Events : null);
            for (int i = 0; i < Cells; i++) _hitLevel[i] = 0f;
        }

        private void Subscribe(GameEventBus bus)
        {
            if (_bus == bus) return;
            if (_bus != null) _bus.Published -= OnEvent;
            _bus = bus;
            if (_bus != null) _bus.Published += OnEvent;
        }

        /// <summary>
        /// 적중한 칸을 점등한다.
        ///
        /// `SpinResolution.Steps[].Purifies[].Cells` 가 **정화된 칸 번호**다 —
        /// 캐스케이드 단계마다 어떤 칸이 사라졌는지가 거기 들어 있다.
        /// 깊이가 여럿이면 전부 켠다: 한 스핀에서 두 번 터진 칸도 한 번 터진 칸과
        /// 같이 「전력이 됐다」이기 때문이다.
        /// </summary>
        private void OnEvent(GameEvent e)
        {
            if (e.Kind != GameEventKind.SpinResolved) return;
            var res = e.Payload as SpinResolution?;
            if (!res.HasValue || res.Value.Steps == null) return;

            var steps = res.Value.Steps;
            for (int s = 0; s < steps.Length; s++)
            {
                var pur = steps[s].Purifies;
                if (pur == null) continue;
                for (int p = 0; p < pur.Length; p++)
                {
                    int[] cells = pur[p].Cells;
                    if (cells == null) continue;
                    for (int c = 0; c < cells.Length; c++)
                    {
                        int idx = cells[c];
                        if (idx >= 0 && idx < Cells) _hitLevel[idx] = 1f;
                    }
                }
            }
            _lastSpinSeen = e.SpinIndex;
        }

        private void Update()
        {
            if (_glows == null) return;
            // ⚠ 직렬화되지 않는 필드라 도메인 리로드 뒤 null 이 된다.
            //   `InteractableOverharvestLever` 가 정확히 이것으로 매 프레임 던지고
            //   있었다(로그 6,732건). 같은 실수를 새 코드에서 반복하지 않는다.
            if (_block == null) _block = new MaterialPropertyBlock();

            bool spinning = _reel != null && _reel.IsSpinning;
            float pulse = spinning
                ? 0.55f + 0.45f * Mathf.Sin(Time.time * _spinPulseHz * Mathf.PI * 2f)
                : 0f;
            float decay = _hitFade > 0.001f ? Time.deltaTime / _hitFade : 1f;

            for (int i = 0; i < Cells; i++)
            {
                Renderer r = _glows[i];
                if (r == null) continue;

                // 적중 → 스핀 → 쉼 순으로 덮는다. 적중이 가장 세다.
                Color c = _idle;
                if (spinning) c = Color.Lerp(_idle, _spin, pulse);
                if (_hitLevel[i] > 0.001f)
                {
                    c = Color.Lerp(c, _hit, _hitLevel[i]);
                    _hitLevel[i] = Mathf.Max(0f, _hitLevel[i] - decay);
                }

                // 값이 그대로면 건드리지 않는다 — 쉼 상태에서 9칸을 매 프레임 쓰지 않기 위해.
                if (Approximately(_applied[i], c)) continue;
                _applied[i] = c;

                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, c);
                _block.SetColor(EmissionColorId, c);
                r.SetPropertyBlock(_block);
            }
        }

        private static bool Approximately(Color a, Color b)
            => Mathf.Abs(a.r - b.r) < 0.002f
            && Mathf.Abs(a.g - b.g) < 0.002f
            && Mathf.Abs(a.b - b.b) < 0.002f;

        /// <summary>검증용. 마지막으로 반영한 스핀 번호.</summary>
        public int LastSpinSeen => _lastSpinSeen;

        /// <summary>검증용. 지금 점등 중인 칸 수.</summary>
        public int LitCells
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Cells; i++) if (_hitLevel[i] > 0.01f) n++;
                return n;
            }
        }
    }
}
