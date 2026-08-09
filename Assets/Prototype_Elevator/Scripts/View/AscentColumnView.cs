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

        // ── 전력 반복기 (UP-FIX-92) ──────────────────────────────────────────
        //
        // 🔴 **왜 배열인가 — 갈라질 수 없게 하려고.**
        //
        // 여섯 라운드 연속 2점을 받은 항목이 「기계 벽을 등진 화각(`C_toward_gate`·
        // `E_contract_wall`)에 전력 정보가 0개」다. 여섯 번째 표시기를 같은 벽에 또
        // 얹지 말고 **다른 벽으로 옮기라**는 것이 6차 독립 평가의 판정이었다.
        //
        // 그 벽에 세운 사본을 **별도 컴포넌트로 만들지 않는다.** 6차 평가가 같은
        // 라운드에 잡아낸 새 결함이 「같은 프레임의 두 전력 표시가 다른 값」이었고
        // (탑 `전력 516 240%` / 벽 `전력 0`), 그것은 표시기마다 갱신 경로가 따로
        // 있었기 때문에 생긴다. 여기서는 <see cref="ApplyTank"/> 하나가 탑과 사본을
        // **같은 프레임에 같은 값으로** 쓴다 — 구조적으로 어긋날 수 없다.
        [Header("전력 반복기 — 기계 벽을 등진 화각용 사본 (UP-FIX-92)")]
        [Tooltip("사본 채움 기둥. 탑과 같은 프레임에 같은 높이로 갱신된다.")]
        [SerializeField] private Transform[] _repeaterFillPivots = new Transform[0];

        [Tooltip("사본 채움 렌더러. 탑과 같은 색·발광을 받는다.")]
        [SerializeField] private Renderer[] _repeaterFills = new Renderer[0];

        [Tooltip("사본 요구선 띠. 100% 전후로 탑과 함께 갈린다.")]
        [SerializeField] private Renderer[] _repeaterBands = new Renderer[0];

        [Tooltip("사본 잠금쇠. 탑의 잠금 오프셋을 그대로 쓴다.")]
        [SerializeField] private Transform[] _repeaterLockLugs = new Transform[0];

        [Tooltip("사본 전력 수치 줄. 탑의 `PowerLine` 과 같은 문자열을 받는다.")]
        [SerializeField] private TextMeshPro[] _repeaterPowerLines = new TextMeshPro[0];

        /// <summary>측정용. 반복기가 실제로 배선돼 있는가를 매니페스트가 스스로 적는다.</summary>
        public int RepeaterCount => _repeaterFillPivots != null ? _repeaterFillPivots.Length : 0;
        public Transform RepeaterFillPivot(int i)
            => _repeaterFillPivots != null && i >= 0 && i < _repeaterFillPivots.Length
               ? _repeaterFillPivots[i] : null;

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

        // 남은 스핀 슬러그 — 사용자 지시 (2026-08-09):
        //   「5개일때 초록, 4개일때도 초록, 3개 2개는 주황, 1개일땐 빨강」
        //
        // ⚠ **칸 번호가 아니라 남은 개수로 정한다.** 칸마다 색이 다르면 색을 보고도
        //   개수를 세어야 하고, 그러면 색이 하는 일이 없다. 여섯 개가 한꺼번에
        //   같은 색으로 바뀌어야 「이제 위험하다」가 세지 않고 읽힌다.
        [Tooltip("남은 스핀이 넉넉하다 (4개 이상).")]
        [SerializeField] private Color _pipSafe = new Color(0.28f, 0.70f, 0.30f);
        [Tooltip("남은 스핀이 줄었다 (2~3개).")]
        [SerializeField] private Color _pipWarn = new Color(0.90f, 0.48f, 0.10f);
        [Tooltip("마지막 한 번 (1개).")]
        [SerializeField] private Color _pipLast = new Color(0.88f, 0.16f, 0.11f);
        [Tooltip("남은 스핀 슬러그 — 이미 쓴 것.")]
        [SerializeField] private Color _pipSpent = new Color(0.10f, 0.095f, 0.088f);

        /// <summary>
        /// 켜져 있는 슬러그의 색. 회색조에서도 갈리도록 **명도가 함께 내려간다** —
        /// 초록 0.55 → 주황 0.52 → 빨강 0.36. 색상만 바꾸면 이 저장소가 두 번 겪은
        /// 「색거리는 2배인데 회색조에서 같은 밴드」가 그대로 재현된다.
        /// </summary>
        private Color PipColor(int left)
            => left >= 4 ? _pipSafe : left >= 2 ? _pipWarn : _pipLast;

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
        private int _shownRequired = int.MinValue;
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
            _shownRequired = int.MinValue;
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
            SetFillHeight(_tankFillPivot, clamped);

            Color color = ratio < 1f ? _belowRequired
                        : floor.ExtraSpinsTaken > 0 || ratio >= 2.2f ? _overharvested
                        : _atRequired;
            SetColor(_tankFill, color, _emission);

            // 요구선과 잠금쇠 — `MACHINE_SPEC` §4.4 「100% 달성 시 내부 잠금쇠가 풀린다」.
            bool unlocked = floor.IsOverharvestUnlocked;
            SetColor(_requiredBand, unlocked ? _bandUnlocked : _bandLocked, unlocked ? 0.45f : 0f);
            MoveLug(_lockLug, unlocked);

            // 사본 — **같은 프레임, 같은 값.** 별도 계산이 없으므로 어긋날 수 없다.
            for (int i = 0; i < RepeaterCount; i++) SetFillHeight(_repeaterFillPivots[i], clamped);
            for (int i = 0; _repeaterFills != null && i < _repeaterFills.Length; i++)
                SetColor(_repeaterFills[i], color, _emission);
            for (int i = 0; _repeaterBands != null && i < _repeaterBands.Length; i++)
                SetColor(_repeaterBands[i], unlocked ? _bandUnlocked : _bandLocked, unlocked ? 0.45f : 0f);
            for (int i = 0; _repeaterLockLugs != null && i < _repeaterLockLugs.Length; i++)
                MoveLug(_repeaterLockLugs[i], unlocked);
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
            Color lit = PipColor(left);
            for (int i = 0; i < _spinPips.Length; i++)
            {
                Renderer pip = _spinPips[i];
                if (pip == null) continue;
                // 이 층에 없는 슬러그는 아예 끈다. 꺼진 자리를 「쓴 것」으로 그리면
                // 스핀 3회짜리 층이 6회짜리 층의 소진 상태처럼 읽힌다.
                bool exists = i < cap;
                if (pip.gameObject.activeSelf != exists) pip.gameObject.SetActive(exists);
                if (!exists) continue;

                // 🔴 **렌더러를 여기서 켠다.** 「레버를 당겨도 인디케이터에 변화가 없다」의
                //    진짜 원인이 이것이었다 (2026-08-09 씬 점검). 배열은 `size=6 · null=0`
                //    으로 멀쩡했고 색 대비도 충분했는데 **여섯 개 전부
                //    `renderer.enabled == false`** 였다. GameObject 는 활성이라
                //    `activeSelf` 검사에는 걸리지 않는다 — 그래서 몇 라운드 동안
                //    「배선은 다 돼 있는데 왜 안 보이나」로 남아 있었다.
                //    색만 칠하는 코드는 보이지 않는 것을 칠할 수 있다.
                if (!pip.enabled) pip.enabled = true;

                bool remaining = i < left;
                SetColor(pip, remaining ? lit : _pipSpent, remaining ? 0.30f : 0f);
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
            int required = Mathf.RoundToInt(floor.RequiredPower);
            if (power != _shownPower || required != _shownRequired)
            {
                _shownPower = power;
                _shownRequired = required;
                // 사용자 지시 (2026-08-09): 「전력을 **숫자로만** 표현한다」.
                //
                // 퍼센트를 뺀 이유는 자리 부족이 아니라 **층 콘솔이 이미 숫자로
                // 말하고 있기 때문**이다. 한 화면에서 한쪽은 `1400`, 한쪽은 `78%` 면
                // 플레이어가 두 체계를 동시에 읽고 머릿속에서 환산해야 한다.
                // 그리고 요구 전력은 위로 끝이 없으므로 퍼센트는 언젠가 거짓말을 한다 —
                // 분모가 계속 커지면 같은 100% 가 매번 다른 양을 뜻한다.
                //
                // ⚠ **두 값 사이를 넓게 벌린다** (`UP-FIX-93`).
                // 계기판의 `요구 0 0%` 가 세 토큰 간격이 균등해져 「요구값과 달성률을
                // 가를 수 없다」는 회귀를 만들었다. 여기서는 토큰이 둘뿐이고
                // 사이를 세 칸 띄운다 — 좁은 판이 아니라 넓은 판이라 여유가 있다.
                // 「이동시 소비되는 전력」 = **다음 한 층 비용** (2026-08-09 사용자 결정).
                //
                // 처음 후보는 `AscendResult.PowerSpent`(= 추가층 × 층당비용)였는데,
                // 자체 검증이 「40개 시드에서 다층 상승이 **한 번도** 발생하지 않았다」고
                // 보고했다. 그대로 붙이면 영원히 0 만 찍히는 칸이 하나 느는 것이라
                // — 사용자가 「더미로 보인다」고 지적한 바로 그 물건이 된다 —
                // 항상 값이 있는 「한 층 더 가는 데 드는 전력」으로 정의를 바꿨다.
                //
                // ⚠ **식을 복제하지 않는다.** `PreviewAscent()` 는 `Resolve()` 가 부르는
                //   것과 같은 한 줄이다(`FloorSession` 주석). 여기서 60 을 직접 쓰면
                //   프로파일이 배선되는 날 화면과 실제 상승이 갈라진다 — 이 저장소가
                //   가장 두려워하는 결함이다.
                // ⚠ 할당이 있으므로 **더티 검사 안에서만** 부른다. 매 프레임 부르면
                //   `AscendResult` 가 프레임마다 하나씩 쌓인다.
                int perFloor = Mathf.RoundToInt(floor.PreviewAscent().PowerPerExtraFloor);
                _text.Clear();
                _text.Append("저장 ").AppendFormat("{0:F0}", floor.Power)
                     .Append("   필요 ").AppendFormat("{0:F0}", floor.RequiredPower)
                     .Append("   이동 ").Append(perFloor);
                SetPowerLine(_text.ToString());
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
            SetFillHeight(_tankFillPivot, clamped);
            Color endColor = ratio < 1f ? _belowRequired
                           : last.ExtraSpinsTaken > 0 ? _overharvested : _atRequired;
            SetColor(_tankFill, endColor, _emission);

            // 종료 화면에서도 사본이 함께 멈춘다 — 한쪽만 옛 값으로 남으면 그것이 곧
            // 「같은 프레임의 두 전력 표시가 다른 값」이다.
            for (int i = 0; i < RepeaterCount; i++) SetFillHeight(_repeaterFillPivots[i], clamped);
            for (int i = 0; _repeaterFills != null && i < _repeaterFills.Length; i++)
                SetColor(_repeaterFills[i], endColor, _emission);

            if (_spinNumeral != null) _spinNumeral.SetText("0");
            if (_ascentNumeral != null) _ascentNumeral.SetText(run.IsFailed ? "0" : "—");
            // 종료 화면도 같은 세 값. 여기서는 이미 확정된 결과가 있으므로
            // `last.Ascent` 를 그대로 읽는다 — 다시 계산하지 않는다.
            SetPowerLine($"저장 {last.FinalPower:F0}   필요 {last.RequiredPower:F0}" +
                         $"   이동 {last.Ascent.PowerPerExtraFloor:F0}");
            if (_reserveLine != null)
                _reserveLine.SetText(run.IsFailed ? "배수 0.00배   손실 —" : "확정 완료");
        }

        /// <summary>
        /// 위험이 오르면 명도를 함께 내린다. 색상만 물들이면 회색조에서 같은 밴드로
        /// 읽힌다 — 이 저장소가 「색거리 2배」로 두 번 실패한 자리다
        /// (`InstrumentPanelView.ApplyBar` 의 같은 판단).
        /// </summary>
        /// <summary>
        /// 채움 기둥 하나의 높이를 쓴다. 탑과 사본이 **이 한 줄을 공유**하기 때문에
        /// 「같은 프레임의 두 전력 표시가 다른 값」(6차 평가 신규 결함)이 재발할 수 없다.
        /// </summary>
        private void SetFillHeight(Transform pivot, float clamped)
        {
            if (pivot == null) return;
            Vector3 s = pivot.localScale;
            s.y = _tankHeight * (clamped / Mathf.Max(0.0001f, _maxRatio));
            pivot.localScale = s;
        }

        /// <summary>
        /// 전력 수치 줄을 **탑과 사본에 동시에** 쓴다.
        ///
        /// 6차 독립 평가가 새로 잡은 결함이 「같은 프레임의 두 전력 표시가 다른 값」
        /// (탑 `전력 516 240%` / 벽 `전력 0`)이었다. 갱신 지점이 하나면 그 결함이
        /// 재현될 수 없다 — 그래서 호출자는 이 함수 말고 다른 경로를 쓰지 않는다.
        /// </summary>
        private void SetPowerLine(string text)
        {
            if (_powerLine != null) _powerLine.SetText(text);
            for (int i = 0; _repeaterPowerLines != null && i < _repeaterPowerLines.Length; i++)
                if (_repeaterPowerLines[i] != null) _repeaterPowerLines[i].SetText(text);
        }

        /// <summary>잠금쇠 하나를 잠김/해제 자리로 옮긴다. 오프셋은 탑의 것을 공유한다.</summary>
        private void MoveLug(Transform lug, bool unlocked)
        {
            if (lug == null) return;
            Vector3 want = unlocked ? _lockLugOpen : _lockLugLocked;
            if ((lug.localPosition - want).sqrMagnitude > 1e-8f) lug.localPosition = want;
        }

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
