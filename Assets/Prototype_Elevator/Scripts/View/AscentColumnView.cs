using System.Text;
using TMPro;
using UnityEngine;
using Ascend.Prototype.Risk;
using Ascend.Prototype.Run;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.View
{
    /// <summary>
    /// **실행 레버 바로 위의 운행 계기탑**을 런 상태로 구동한다.
    ///
    /// ## 왜 계기판이 하나 더 필요한가
    ///
    /// `MASTER_PRD` §4.4(`MACHINE_SPEC`)가 확정한 요구가 여섯 라운드째 미구현이었다 —
    /// 「레버 주변에 **현재 전력 · 유지 배수 · 추가 스핀 손실 조건**을 큰 단위로 표시」.
    /// 사용자 지시(2026-08-04)가 그것을 다시 못 박았다: 「전력표시기는 레버 위 두는 게
    /// 적당할 것 같다. 그래야 한눈에 보고 결정하지. **점점 차오르는 것도 보이고.**」
    ///
    /// 그리고 같은 날의 2차 지시가 **한눈에 읽혀야 하는 것 넷**을 확정했다 —
    /// 전력량 · 남은 스핀 횟수 · 실행 레버 · 수확 기계. 나머지(계약·무게·위험도·연쇄·층)는
    /// 2순위다. 이 컴포넌트는 그 넷 중 **둘**(전력량·남은 스핀)을 소유하고, 셋째·넷째는
    /// 물리 오브젝트라 형상이 소유한다.
    ///
    /// ## 🔴 이 컴포넌트가 하지 않는 것 — 조명으로 값을 나르지 않는다
    ///
    /// 5차 독립 평가가 「천장등 색·밝기가 달성률을 따라간다」를 **실측으로 기각**했다.
    /// 셋 다 실패했다 — ① 값을 못 싣는다 ② 회색조에서 사라진다 ③ 0%(Stable)와
    /// 240%(Collapse)가 같은 화면을 냈다. 그래서 여기서는 **채움 높이**가 값을 나른다.
    /// 높이는 회색조에서 살아남고, 두 상태가 같은 그림이 될 수 없다.
    ///
    /// 색은 **보조**다. 100% 미만은 탈색된 뼈색, 100% 이상은 붉은색 하나
    /// (`ART_BIBLE` §3.1 팔레트 · 레퍼런스 「붉은색만 강조색」).
    ///
    /// ## 밝기로 크게 만들지 않는다
    ///
    /// `UP-FIX-90`(방 안 최고 백색이 계기 텍스트)·`UP-FIX-94`(§12 대역 3항목 위반)의
    /// 원인이 순백 글자다. 이 탑의 숫자는 **따뜻한 뼈색**(r &gt; g &gt; b)이고 순백을
    /// 쓰지 않는다 — `VISUAL_SPEC` §12 가 요구하는 색조(g/r 0.78~0.90)와 같은 방향이다.
    /// 크기는 **형상**(0.15 m 글자 상자)이 만든다. 밝기가 아니다.
    /// </summary>
    public sealed class AscentColumnView : MonoBehaviour
    {
        [SerializeField] private RunSessionBehaviour _run;

        /// <summary>
        /// 결과 스포일 방지. `UP-FIX-15` 가 계기판에서 겪은 것과 **같은 결함**이 여기서
        /// 재발할 수 있다 — 연출이 도는 동안 값을 그대로 읽으면 탱크가 먼저 차오른다.
        /// </summary>
        [SerializeField] private SpinPresenter _presenter;

        [SerializeField] private RiskStateView _risk;

        // ── 전력 탱크 ────────────────────────────────────────────────────────

        [Header("전력 탱크 — 세로 채움 (형태가 값을 나른다)")]
        [Tooltip("바닥에 피벗을 둔 채움 기둥. localScale.y 가 채움 높이(m)다.")]
        [SerializeField] private Transform _tankFillPivot;
        [SerializeField] private Renderer _tankFill;

        [Tooltip("탱크 안지름 높이(m). 이 높이가 MaxRatio 에 대응한다.")]
        [SerializeField] private float _tankHeight = 0.444f;

        [Tooltip("탱크가 표시하는 최대 비율. 임계점 표(300%)에 맞춘다.")]
        [SerializeField] private float _maxRatio = 3f;

        /// <summary>측정용. 매니페스트가 「셔터 순간 얼마나 차 있었는가」를 스스로 적는다.</summary>
        public Transform TankFillPivot => _tankFillPivot;
        public float TankHeight => _tankHeight;
        public float MaxRatio => _maxRatio;

        [Header("요구선 100% — 2단 잠금과의 시각적 연결")]
        [Tooltip("요구 전력선의 붉은 띠. 100% 도달 전후로 발광이 갈린다.")]
        [SerializeField] private Renderer _requiredBand;

        [Tooltip("잠금쇠. 100% 미만이면 탱크를 가로지르고, 해제되면 옆으로 빠진다.")]
        [SerializeField] private Transform _lockLug;
        [SerializeField] private Vector3 _lockLugLocked = Vector3.zero;
        [SerializeField] private Vector3 _lockLugOpen = new Vector3(0.055f, 0f, 0f);

        // ── 1순위 숫자 ───────────────────────────────────────────────────────

        [Header("1순위 — 남은 스핀 / 상승 층수")]
        [SerializeField] private TextMeshPro _spinNumeral;
        [SerializeField] private TextMeshPro _ascentNumeral;

        [Tooltip("남은 스핀 슬러그. 남은 것은 붉은 캡, 쓴 것은 어두운 구멍.")]
        [SerializeField] private Renderer[] _spinPips = new Renderer[6];

        [Header("1순위 보조 — 전력 수치와 달성률")]
        [SerializeField] private TextMeshPro _powerLine;

        [Header("2순위 — PRD §4.4 가 요구하는 나머지 둘의 자리")]
        [Tooltip("유지 배수 · 추가 스핀 손실 조건. 값이 없으면 `—` 로 자리를 지킨다.")]
        [SerializeField] private TextMeshPro _reserveLine;

        /// <summary>
        /// 과수확 표찰. **이 컴포넌트가 소유하는 이유는 잠금이 여기서 풀리기 때문이다** —
        /// 탱크가 요구선을 넘는 순간 잠금 핀이 빠지고 같은 프레임에 표찰이 살아난다.
        /// 둘을 다른 컴포넌트가 나눠 가지면 「해제됐는데 표찰만 죽어 있다」가 생긴다.
        ///
        /// `UP-FIX-89` 의 정보 위계 역전(B 기준 과수확 35px &gt; 실행 24px)을 **크기가
        /// 아니라 밝기로** 되돌린다. 크기를 줄이면 `UP-FIX-86` 이 되돌아간다.
        /// 잠겨 있는 선택지가 사용 가능한 선택지보다 밝을 이유가 없다.
        /// </summary>
        [SerializeField] private TextMeshPro _overharvestLabel;
        [SerializeField] private Color _overharvestLocked = new Color(0.46f, 0.38f, 0.26f);
        [SerializeField] private Color _overharvestUnlocked = new Color(0.94f, 0.42f, 0.24f);

        // ── 색 ───────────────────────────────────────────────────────────────

        [Header("색 — 보조 채널. 회색조에서는 높이만 남는다")]
        [Tooltip("요구 전력 미만. 탈색된 뼈색 — 강조가 아니다.")]
        [SerializeField] private Color _belowRequired = new Color(0.62f, 0.56f, 0.40f);

        [Tooltip("요구 전력 달성. 레퍼런스의 유일한 강조색인 붉은색.")]
        [SerializeField] private Color _atRequired = new Color(0.78f, 0.26f, 0.16f);

        [Tooltip("과수확 구간(220% 이상). 같은 붉은색을 더 밀어 올린다.")]
        [SerializeField] private Color _overharvested = new Color(0.88f, 0.20f, 0.11f);

        [Tooltip("잠긴 요구선. 해제 전에는 도장된 붉은 금속일 뿐 빛나지 않는다.")]
        [SerializeField] private Color _bandLocked = new Color(0.34f, 0.11f, 0.09f);
        [SerializeField] private Color _bandUnlocked = new Color(0.86f, 0.22f, 0.14f);

        [Tooltip("남은 스핀 슬러그 — 남은 것.")]
        [SerializeField] private Color _pipLeft = new Color(0.74f, 0.26f, 0.17f);
        [Tooltip("남은 스핀 슬러그 — 이미 쓴 것.")]
        [SerializeField] private Color _pipSpent = new Color(0.10f, 0.095f, 0.088f);

        /// <summary>
        /// 발광 배수. **면적이 작아도 1 을 넘기면 블룸이 번져 면적 이득이 사라진다**
        /// (`AscendReferenceRoom.Mat` 이 세그먼트 발광에서 이미 배운 것).
        /// 탱크 채움은 최대 0.053 ㎡ 라 작지만 그 교훈을 그대로 지킨다.
        /// </summary>
        [SerializeField, Range(0f, 1f)] private float _emission = 0.55f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock _block;
        private readonly StringBuilder _text = new StringBuilder(96);

        // 값이 실제로 바뀔 때만 문자열을 짓는다. 계기판이 캐시 키를 정수 하나로 접었다가
        // 「무엇이 키에서 빠졌는가」를 못 보고 옛 값을 남긴 전례가 있어(`InstrumentPanelView`
        // 주석), 여기서는 그리는 값을 **하나씩** 들고 비교한다.
        private int _shownSpins = int.MinValue;
        private int _shownSpinCap = int.MinValue;
        private int _shownFloors = int.MinValue;
        private int _shownPower = int.MinValue;
        private int _shownPercent = int.MinValue;
        private int _shownAnte = int.MinValue;
        private int _shownMultiplierMilli = int.MinValue;
        private bool _runOverShown;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();
            if (_presenter == null) _presenter = FindAnyObjectByType<SpinPresenter>();
            if (_risk == null) _risk = FindAnyObjectByType<RiskStateView>();
            if (_run != null) _run.RunStarted += _ => InvalidateCache();
        }

        private void InvalidateCache()
        {
            _shownSpins = int.MinValue;
            _shownSpinCap = int.MinValue;
            _shownFloors = int.MinValue;
            _shownPower = int.MinValue;
            _shownPercent = int.MinValue;
            _shownAnte = int.MinValue;
            _shownMultiplierMilli = int.MinValue;
            _runOverShown = false;
        }

        private void LateUpdate()
        {
            RunSession run = _run != null ? _run.Session : null;
            if (run == null) return;

            FloorSession floor = run.Current;
            if (floor == null) { ShowRunOver(run); return; }
            _runOverShown = false;

            // 연출 중에는 **스핀 결과를 담은 것만** 얼린다. 남은 스핀·판돈은 결과가
            // 아니므로 살려 둔다 — 계기판이 같은 지점에서 내린 것과 같은 판단이다.
            if (_presenter != null && !_presenter.ResultRevealed)
            {
                ApplySpins(floor);
                return;
            }

            float ratio = floor.RequiredPower > 0f ? floor.Power / floor.RequiredPower : 0f;
            ApplyTank(ratio, floor);
            ApplySpins(floor);
            ApplyAscent(run, floor);
            ApplyNumbers(floor, ratio);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  전력 탱크 — 「점점 차오르는 것」
        // ══════════════════════════════════════════════════════════════════════

        private void ApplyTank(float ratio, FloorSession floor)
        {
            float clamped = Mathf.Clamp(ratio, 0f, _maxRatio);
            if (_tankFillPivot != null)
            {
                Vector3 s = _tankFillPivot.localScale;
                s.y = _tankHeight * (clamped / Mathf.Max(0.0001f, _maxRatio));
                _tankFillPivot.localScale = s;
            }

            Color color = ratio < 1f ? _belowRequired
                        : floor.ExtraSpinsTaken > 0 || ratio >= 2.2f ? _overharvested
                        : _atRequired;
            SetColor(_tankFill, color, _emission);

            // 요구선과 잠금쇠 — `MACHINE_SPEC` §4.4 「100% 달성 시 내부 잠금쇠가 풀린다」.
            bool unlocked = floor.IsOverharvestUnlocked;
            SetColor(_requiredBand, unlocked ? _bandUnlocked : _bandLocked, unlocked ? 0.45f : 0f);
            if (_lockLug != null)
            {
                Vector3 want = unlocked ? _lockLugOpen : _lockLugLocked;
                if ((_lockLug.localPosition - want).sqrMagnitude > 1e-8f)
                    _lockLug.localPosition = want;
            }
            if (_overharvestLabel != null)
            {
                Color want = unlocked ? _overharvestUnlocked : _overharvestLocked;
                if (_overharvestLabel.color != want) _overharvestLabel.color = want;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  남은 스핀 — 1순위. 「압박 시계」
        // ══════════════════════════════════════════════════════════════════════

        private void ApplySpins(FloorSession floor)
        {
            int left = floor.SpinsRemaining;
            int cap = Mathf.Max(1, floor.Plan.Spins);

            if (left != _shownSpins || cap != _shownSpinCap)
            {
                _shownSpins = left;
                _shownSpinCap = cap;
                if (_spinNumeral != null)
                {
                    _text.Clear();
                    _text.Append(left);
                    _spinNumeral.SetText(_text);
                }
            }

            if (_spinPips == null) return;
            for (int i = 0; i < _spinPips.Length; i++)
            {
                Renderer pip = _spinPips[i];
                if (pip == null) continue;
                // 이 층에 없는 슬러그는 아예 끈다. 꺼진 자리를 「쓴 것」으로 그리면
                // 스핀 3회짜리 층이 6회짜리 층의 소진 상태처럼 읽힌다.
                bool exists = i < cap;
                if (pip.gameObject.activeSelf != exists) pip.gameObject.SetActive(exists);
                if (!exists) continue;
                bool remaining = i < left;
                SetColor(pip, remaining ? _pipLeft : _pipSpent, remaining ? 0.30f : 0f);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  몇 층 오르는가 — 사용자 지시 (2026-08-04)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// ⚠ **`RunSession.PreviewFloorsGained()` 만 부른다.** 그 함수가 실제 판정이 쓰는
        /// `FloorSession.PreviewAscent()` + `ClampAscent()` 를 그대로 부르므로
        /// 화면과 실제 상승이 갈라질 수 없다. 여기서 직접 계산하면 그 보장이 사라진다.
        /// </summary>
        private void ApplyAscent(RunSession run, FloorSession floor)
        {
            int floors = run.PreviewFloorsGained();
            if (floors == _shownFloors) return;
            _shownFloors = floors;
            if (_ascentNumeral == null) return;

            _text.Clear();
            if (floors <= 0) _text.Append('0');
            else _text.Append('+').Append(floors);
            _ascentNumeral.SetText(_text);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  숫자 두 줄
        // ══════════════════════════════════════════════════════════════════════

        private void ApplyNumbers(FloorSession floor, float ratio)
        {
            int power = Mathf.RoundToInt(floor.Power);
            int percent = Mathf.RoundToInt(ratio * 100f);
            if (power != _shownPower || percent != _shownPercent)
            {
                _shownPower = power;
                _shownPercent = percent;
                if (_powerLine != null)
                {
                    // ⚠ **두 값 사이를 넓게 벌린다** (`UP-FIX-93`).
                    // 계기판의 `요구 0 0%` 가 세 토큰 간격이 균등해져 「요구값과 달성률을
                    // 가를 수 없다」는 회귀를 만들었다. 여기서는 토큰이 둘뿐이고
                    // 사이를 세 칸 띄운다 — 좁은 판이 아니라 넓은 판이라 여유가 있다.
                    _text.Clear();
                    _text.Append("전력 ").AppendFormat("{0:F0}", floor.Power)
                         .Append("   ").Append(percent).Append('%');
                    _powerLine.SetText(_text);
                }
            }

            // PRD §4.4 의 나머지 둘. **자리를 비워 두지 않는다** — 빈 자리는 다음 사람이
            // 「여기에 무엇이 와야 하는지」를 다시 찾게 만든다.
            //   유지 배수      = 확정하면 지키는 배수(현재 구간의 상승 층수 배수)
            //   추가 스핀 손실 = 한 번 더 당길 때 거는 판돈(`PendingAnte`)
            int ante = Mathf.RoundToInt(floor.PendingAnte);
            int multMilli = Mathf.RoundToInt(HoldMultiplier(floor) * 1000f);
            if (ante == _shownAnte && multMilli == _shownMultiplierMilli) return;
            _shownAnte = ante;
            _shownMultiplierMilli = multMilli;
            if (_reserveLine == null) return;

            _text.Clear();
            _text.Append("배수 ").AppendFormat("{0:F2}", multMilli / 1000f).Append("배   손실 ");
            if (ante > 0) _text.Append(ante);
            else _text.Append('—');
            _reserveLine.SetText(_text);
        }

        /// <summary>
        /// 지금 확정하면 **지키는 배수**. 달성률을 그대로 쓰지 않는다 — 초과 전력은
        /// 층으로도 돈으로도 갚아지므로, 요구 전력 대비 얼마를 확보했는지가 그 값이다.
        /// 미달 구간에서는 1 미만이 되어 「아직 못 지킨다」가 숫자로 읽힌다.
        /// </summary>
        private static float HoldMultiplier(FloorSession floor)
        {
            if (floor.RequiredPower <= 0f) return 0f;
            return Mathf.Max(0f, floor.Power / floor.RequiredPower);
        }

        // ══════════════════════════════════════════════════════════════════════

        private void ShowRunOver(RunSession run)
        {
            if (_runOverShown) return;                      // 종료 화면은 더 바뀌지 않는다
            var results = run.Results;
            if (results.Count == 0) return;
            _runOverShown = true;
            FloorResult last = results[results.Count - 1];

            float ratio = last.RequiredPower > 0f ? last.FinalPower / last.RequiredPower : 0f;
            float clamped = Mathf.Clamp(ratio, 0f, _maxRatio);
            if (_tankFillPivot != null)
            {
                Vector3 s = _tankFillPivot.localScale;
                s.y = _tankHeight * (clamped / Mathf.Max(0.0001f, _maxRatio));
                _tankFillPivot.localScale = s;
            }
            SetColor(_tankFill, ratio < 1f ? _belowRequired
                             : last.ExtraSpinsTaken > 0 ? _overharvested : _atRequired, _emission);

            if (_spinNumeral != null) _spinNumeral.SetText("0");
            if (_ascentNumeral != null) _ascentNumeral.SetText(run.IsFailed ? "0" : "—");
            if (_powerLine != null)
                _powerLine.SetText($"전력 {last.FinalPower:F0}   {ratio * 100f:F0}%");
            if (_reserveLine != null)
                _reserveLine.SetText(run.IsFailed ? "배수 0.00배   손실 —" : "확정 완료");
        }

        /// <summary>
        /// 위험이 오르면 명도를 함께 내린다. 색상만 물들이면 회색조에서 같은 밴드로
        /// 읽힌다 — 이 저장소가 「색거리 2배」로 두 번 실패한 자리다
        /// (`InstrumentPanelView.ApplyBar` 의 같은 판단).
        /// </summary>
        private void SetColor(Renderer target, Color color, float emission)
        {
            if (target == null) return;
            if (_block == null) _block = new MaterialPropertyBlock();

            if (_risk != null)
            {
                float top = _risk.AmbientValueFor(RiskLevel.Stable);
                if (top > 0.0001f)
                {
                    float v = Mathf.Clamp(_risk.AmbientValueFor(_risk.Level) / top, 0.45f, 1f);
                    color = new Color(color.r * v, color.g * v, color.b * v, color.a);
                }
            }

            target.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            _block.SetColor(EmissionColorId, color * emission);
            target.SetPropertyBlock(_block);
        }
    }
}
