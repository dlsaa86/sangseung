using System.Text;
using TMPro;
using UnityEngine;
using Ascend.Prototype.Risk;
using Ascend.Prototype.Run;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.View
{
    /// <summary>
    /// 벽면 계기판을 런 상태로 구동한다.
    ///
    /// 이게 없으면 전력과 위험은 화면 구석 HUD에만 존재한다. `visual-criteria.md` B-3.8이
    /// 요구하는 것은 정확히 그 반대다 — "플레이어가 보드를 안 보고 있어도 현재 전력·요구
    /// 전력·임계점이 계기판으로 읽히는가."
    ///
    /// 기존 `ElevatorGrayboxView`가 이 자리를 맡고 있었으나 폐기된 `RunController`를 참조해
    /// 첫 줄에서 return하고 있었다. 그쪽은 손대지 않고(재작성 금지) 이 컴포넌트가 대신한다.
    /// </summary>
    public sealed class InstrumentPanelView : MonoBehaviour
    {
        [SerializeField] private RunSessionBehaviour _run;
        [SerializeField] private RouletteInteractionBridge _bridge;
        [SerializeField] private RiskStateView _risk;

        /// <summary>
        /// `UP-FIX-15` — 결과 숫자가 연출보다 먼저 나와 스핀을 스포일한다.
        ///
        /// 원인이 여기였다. `RunSession.Spin()` 은 당긴 프레임에 전부 확정하는데
        /// 이 뷰는 `LateUpdate` 마다 `floor.Power` 를 그대로 읽고 **연출자를 참조조차
        /// 하지 않았다.** 결과판(`DrivenExternally`)과 HUD(`IsPresenting` 동결)는 이미
        /// 막혀 있었고 계기판만 새고 있었다 — 감사 영상의 f12→f30 변화가 전부 이 문자열이다.
        /// 「30프레임」이라고 적혀 있었으나 Standard 템포에서 실제로는 **71프레임**이다.
        /// </summary>
        [SerializeField] private SpinPresenter _presenter;

        [Header("표시")]
        [SerializeField] private TextMeshPro _floorLabel;
        [SerializeField] private TextMeshPro _powerLabel;

        /// <summary>
        /// `UP-FIX-23` — 「전력 N / 요구 M」 한 줄이 9.01 rect 단위라 어떤 여백값으로도
        /// 통관 그림자(하한 3.12)와 레버 덮개(상한 10.21) 사이 **7.09 단위**에 못 들어간다.
        /// 산술적으로 해가 없어서 직전 시도는 글자를 30% 줄이는 대가를 치렀다.
        ///
        /// **줄을 나누면 그 대가가 0 이 된다** — 「전력 N」 4.5 단위 · 「요구 M」 4.4 단위.
        /// 비면 옛 동작 그대로 한 줄에 합쳐 쓴다(배선이 빠져도 정보가 사라지지 않는다).
        /// </summary>
        [SerializeField] private TextMeshPro _requiredLabel;
        [SerializeField] private TextMeshPro _statusLabel;

        [Tooltip("계약 미리보기 — 출현률·정화 보상·잔류 대가를 한 줄에. 비어 있으면 표시하지 않는다.")]
        [SerializeField] private TextMeshPro _contractLabel;

        /// <summary>
        /// 8차 판정 §4 — 「전력」 두 글자가 통관 뒤에서 시작한다(24장 중 11장).
        ///
        /// **원인을 씬 좌표로 다시 쟀다. 「반투명 통관 너머로 비친다」가 아니다.**
        /// 가리는 것은 `TubesRoot/Tube_2`(프레임과 가운데 칸 심볼)이고 라벨보다
        /// **카메라 쪽**에 서 있다. 그래서 글자 뒤에 배킹판을 까는 수법은 여기서
        /// 원리적으로 듣지 않는다 — 승객 이름표에서 같은 수법이 24장 중 0장 보였던
        /// 것과는 실패 이유가 다르다(그쪽은 안 보였고, 이쪽은 있어도 소용이 없다).
        ///
        /// 계기 라벨 셋은 pivot(0, 0.5)·좌측 정렬이라 계기판 **좌단**(rect u=0)에서
        /// 시작해 오른쪽으로만 자란다. Risk 시점(`06`·`09`·`10`·`16`·`18`)에서
        /// `Tube_2` 실루엣의 오른쪽 끝은 화면 x=**911 px**, 라벨 시작점은 x=**816 px**.
        /// 다섯 장 전부에서 잰 같은 값이고, 씬 YAML 좌표로 쏜 광선이 예측한 경계
        /// (911 px)와 1 px 안에서 일치한다. 그 95 px = **rect 3.12 단위**이고
        /// 그 안에 「전력 1616」의 앞 여섯 글자가 들어 있다.
        ///
        /// 고치는 축은 **가로 예산**이다. 글자를 그만큼 오른쪽으로 밀고(`margin.x`),
        /// 민 만큼 오른쪽이 넘치므로 줄 전체를 같이 줄인다. 둘 중 하나만 만지면
        /// 반드시 반대쪽 가장자리가 깨진다 — 네 라운드 연속으로 그렇게 깨졌다
        /// (우측 → 상단 → 좌측).
        ///
        /// 예산은 실측이다. Risk 시점의 무가림 구간은 rect **3.12…15.34**,
        /// `21_board_and_gauge` 는 **0…10.21**(과수확 레버 덮개가 그 뒤를 가린다).
        /// 전력 줄 전체는 15.14 단위, 그중 보호해야 할 앞머리 「전력 N / 요구 M」은
        /// **9.01 단위**(4자리 전력 기준, `06` 에서 px 818…1090 실측).
        /// 두 구간을 동시에 만족하는 해는 `pad + 줄폭·scale ≤ 두 상한` 뿐이고,
        /// 축소 없이는 **산술적으로 해가 없다** — 9.01 > 10.21 − 3.12 = 7.09.
        /// </summary>
        [Header("가로 예산 — 통관 가림 회피")]
        [Tooltip("계기판 좌단에서 글자 시작까지 비우는 폭(rect 단위). Risk 시점 통관 실루엣이 3.12 까지 덮는다. 0 이면 예전 동작.")]
        [SerializeField] private float _leftPadUnits = 5.20f;

        [Tooltip("글자 크기 배율. 좌측 여백만큼 오른쪽이 밀리므로 함께 줄인다. 1 이면 예전 동작.")]
        [SerializeField, Range(0.5f, 1f)] private float _textScale = 1.00f;

        // 배율을 곱할 원본 크기. **런타임 값에서 읽지 않는다.** 라벨이 이미 들고 있는
        // 크기를 원본으로 삼으면, 이 컴포넌트가 한 번 쓴 값을 다음 번에 다시 원본으로
        // 읽어 배율이 복리로 걸린다. 씬 세 라벨은 전부 10 이고 그 사실을 여기 못 박는다.
        [Tooltip("계기 라벨 셋의 기준 글자 크기. 씬 값이 아니라 이 값이 이긴다 — 배율이 복리로 걸리는 것을 막는다.")]
        [SerializeField] private float _baseFontSize = 10f;

        [Header("전력 게이지")]
        [SerializeField] private Transform _barPivot;
        // **1.0 이다.** 2026-08-09 이전에는 1.72 였는데, 그건 폭이 아니라 **보정값**이었다 —
        // 블렌더의 `SM_Gauge_Fill` 이 0%~67% 만 덮게 모델링돼 있어서 짧은 메시를 억지로
        // 늘리던 배수다. 모델을 0%~100% 로 연장했으므로(정점 이동, 오차 0.0mm)
        // 이제 보정이 필요 없고, 1.72 를 그대로 두면 **두 번 곱해진다.**
        //
        // 즉 이 값이 1.0 이라는 것은 「모델과 코드가 같은 말을 한다」는 뜻이다.
        // 다시 1 이 아닌 값이 필요해졌다면 그건 스케일 문제가 아니라 **모델이 다시
        // 어긋났다는 신호**이므로, 여기를 만지기 전에 블렌더 쪽 폭부터 재라.
        [Tooltip("게이지 전체 폭 배수. 모델이 0%~100% 를 온전히 덮으므로 1.0 이다.")]
        [SerializeField] private float _barWidth = 1.0f;
        [Tooltip("게이지가 표시하는 최대 비율. 임계점 표(300%)에 맞춘다.")]
        [SerializeField] private float _maxRatio = 3f;
        [SerializeField] private Renderer _barFill;

        /// <summary>측정용. 캡처가 "셔터 순간 게이지가 얼마나 차 있었는가"를 스스로 적는다.</summary>
        public Transform BarPivot => _barPivot;
        public float BarWidth => _barWidth;
        public float MaxRatio => _maxRatio;

        [Header("계약 명판 — 위에서부터 선택지 순서")]
        [SerializeField] private Renderer[] _contractPlaques = new Renderer[3];

        // 색만 바뀌는 명판 세 장으로는 "무엇을 고르는가"를 알 수 없다. 계기판 한 줄에
        // 미리보기를 몰아넣어 봤지만 판(world y 1.28~1.83)에 빈 띠가 없어 글자가
        // 판 밖 벽으로 밀려 잘렸다. 선택지는 **선택자 위에** 있어야 한다.
        [Tooltip("명판별 조건 — 출현률·정화 보상·잔류 대가. 셋을 나란히 비교할 수 있어야 한다.")]
        [SerializeField] private TextMeshPro[] _plaqueLabels = new TextMeshPro[3];
        [SerializeField] private Color _plaqueIdle = new Color(0.20f, 0.21f, 0.23f);
        [SerializeField] private Color _plaquePreview = new Color(0.95f, 0.80f, 0.35f);
        [SerializeField] private Color _plaqueChosen = new Color(0.45f, 0.90f, 0.70f);

        [Header("게이지 색 — 밴드 구분")]
        [SerializeField] private Color _belowRequired = new Color(0.55f, 0.60f, 0.68f);
        [SerializeField] private Color _atRequired = new Color(0.45f, 0.90f, 0.70f);
        [SerializeField] private Color _overharvested = new Color(1f, 0.66f, 0.25f);

        [Tooltip("Critical·Collapse 에서 게이지가 끌려가는 색. 전력 비율과 무관하게 위급이 이긴다.")]
        [SerializeField] private Color _dangerTint = new Color(0.92f, 0.24f, 0.20f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock _block;
        private readonly StringBuilder _text = new StringBuilder(160);

        // 라벨 세 줄을 매 프레임 새로 조립하면 프레임당 문자열 세 개가 버려진다.
        // 표시값이 실제로 바뀔 때만 짓고, TMP 에는 StringBuilder 오버로드로 넘긴다
        // (string 오버로드는 내부에서 또 한 번 복사한다).
        private int _floorKey = int.MinValue;
        // 전력 라벨이 실제로 그리는 세 값. 정수 하나로 접지 않는다 — 접는 순간
        // 무엇이 키에서 빠졌는지 보이지 않게 된다.
        private int _shownPower = int.MinValue;
        private int _shownRequired = int.MinValue;
        private int _shownBand = int.MinValue;
        private int _statusKey = int.MinValue;
        // 계약 라벨 캐시. 정수 하나로 접지 않는다 — 바로 위 전력 라벨이 같은 실수로
        // 층이 바뀌어도 갱신되지 않았고, 여기서도 같은 일이 벌어지고 있었다.
        // `key = selecting ? 1 + shown : (IsNone ? 0 : 2)` 는 **미리보기 2번째(1+1=2)**와
        // **확정(2)**를 같은 값으로 접는다. 계약 선택지가 2개 이상인 4·6·7·8·9층에서
        // 두 번째 항목을 보다가 확정하면 본문이 "계약 2/2"로 남고, 명판은 별도 캐시라
        // 정상 전환되어 **화면이 스스로와 모순된다.**
        private bool _contractSelecting;
        private int _contractShown = int.MinValue;
        private string _contractSelectedLabel;
        private readonly int[] _plaqueKeys = new int[3];

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();
            if (_bridge == null) _bridge = FindAnyObjectByType<RouletteInteractionBridge>();
            if (_risk == null) _risk = FindAnyObjectByType<RiskStateView>();
            if (_presenter == null) _presenter = FindAnyObjectByType<SpinPresenter>();
            if (_run != null) _run.RunStarted += _ => InvalidateCache();

            ApplyLabelFit();
        }

        /// <summary>
        /// 계기 라벨 셋에 좌측 여백과 크기 배율을 건다. 계약 라벨(`_contractLabel`)은
        /// **뺀다** — 그건 오른쪽 벽의 계약 장치에 붙어 있어 통관과 무관하고,
        /// 같은 여백을 걸면 아무 이유 없이 오른쪽으로 밀린다.
        /// </summary>
        private void ApplyLabelFit()
        {
            Fit(_floorLabel);
            Fit(_powerLabel);
            Fit(_requiredLabel);
            Fit(_statusLabel);
        }

        private void Fit(TextMeshPro label)
        {
            if (label == null || _baseFontSize <= 0f) return;

            float size = _baseFontSize * _textScale;
            if (!Mathf.Approximately(label.fontSize, size)) label.fontSize = size;

            Vector4 margin = label.margin;
            if (!Mathf.Approximately(margin.x, _leftPadUnits))
            {
                margin.x = _leftPadUnits;
                label.margin = margin;
            }

            // **좌측 여백을 준 만큼 오른쪽에서 잘려 나갔다.**
            //
            // 9차 판정: 앞 문장은 통과했다 — 「전」 첫 잉크가 x=977 px 로 밀려 8차가 지목한
            // 좌측 가림 11장이 실제로 풀렸다. 그런데 같은 판정이 **새 축의 거짓 그린 7건**을
            // 잡았다. 06·08·09·10(2줄)·12 의 줄이 x≈1274 px 에서 하드 컷되고, 잘린 것이
            // 하필 `B-3 #10` 이 요구하는 대가 수치(−16.0 / −24.0 / −32.0)와 「판돈 329」였다.
            //
            // 원인은 단순하다. 글자상자의 오른쪽 끝은 그대로인데 왼쪽에서 5.20 단위를
            // 밀어냈으니 **쓸 수 있는 폭이 그만큼 줄었다.** 가림을 고치면서 잘림을 만든 것이고,
            // 「고쳤다」가 아니라 「옮겼다」다. 이 저장소가 다섯 라운드 연속 당한 실패
            // (상태 패널 가장자리가 좌 → 우로 이동)와 같은 종류다.
            //
            // 그래서 왼쪽을 되돌리지 않는다 — 되돌리면 가림이 돌아온다. 대신 **긴 줄만**
            // 상자 안에 들어가도록 줄인다. 잘린 숫자는 못 읽지만 작아진 숫자는 읽힌다.
            // 짧은 줄(전력·요구·층)은 손대지 않는다 — 라벨마다 글자 크기가 달라지면
            // 스타일 일관성이 깨지고, 그건 다른 축의 감점이다.
            if (!ReferenceEquals(label, _statusLabel)) return;
            if (!label.enableAutoSizing)
            {
                label.enableAutoSizing = true;
                label.fontSizeMax = size;
                label.fontSizeMin = size * StatusShrinkFloor;
            }
            else if (!Mathf.Approximately(label.fontSizeMax, size))
            {
                label.fontSizeMax = size;
                label.fontSizeMin = size * StatusShrinkFloor;
            }
        }

        /// <summary>
        /// 상태 줄이 줄어들 수 있는 하한. 이 아래로는 줄이지 않는다 — 상자에 넣겠다고
        /// 끝없이 줄이면 「잘려서 못 읽는다」가 「작아서 못 읽는다」로 바뀔 뿐이다.
        /// 하한에서도 넘치면 그건 글자 크기가 아니라 **문장 길이나 상자 폭의 문제**이고,
        /// 매니페스트의 `잘림(글자상자)` 이 그것을 잡아 준다.
        /// </summary>
        private const float StatusShrinkFloor = 0.66f;

        /// <summary>런이 새로 시작되면 캐시 키를 푼다. 안 그러면 종료 화면이 그대로 남는다.</summary>
        private void InvalidateCache()
        {
            _floorKey = int.MinValue;
            _shownPower = int.MinValue;
            _shownRequired = int.MinValue;
            _shownBand = int.MinValue;
            _statusKey = int.MinValue;
            _contractSelecting = false;
            _contractShown = int.MinValue;
            _contractSelectedLabel = null;
            for (int i = 0; i < _plaqueKeys.Length; i++) _plaqueKeys[i] = int.MinValue;
        }

        private void LateUpdate()
        {
            // 값이 바뀔 때만 실제로 쓴다(비교 여섯 번). 매 프레임 거는 이유는 하나다 —
            // 인스펙터에서 예산을 바꿔 보는 동안에도 화면이 따라와야 캡처 전에 검산할 수 있다.
            ApplyLabelFit();

            RunSession run = _run != null ? _run.Session : null;
            if (run == null) return;

            FloorSession floor = run.Current;
            if (floor == null)
            {
                ShowRunOver(run);
                return;
            }

            float ratio = floor.RequiredPower > 0f ? floor.Power / floor.RequiredPower : 0f;

            // "위험 위험"으로 읽히던 것을 고친다 — 항목 이름과 값이 같은 단어였다.
            int riskLevel = _risk != null ? (int)_risk.Level : -1;
            int floorKey = floor.Plan.Floor * 16 + riskLevel + 1;
            if (floorKey != _floorKey)
            {
                _floorKey = floorKey;
                _text.Clear();
                // 🔴 **한 줄 → 두 줄** (`UP-FIX-88` / `UP-FIX-105` · 2026-08-05).
                //
                // 「1층 / 10   위험도 안정」은 런타임에서 폭 **1.095 m** 다. 판독면은
                // 0.72 m 라 원리적으로 안 들어가고, 실측하니 우벽(x 2.00)까지 뚫고
                // 나가 있었다. 세 라운드 동안 「18px 넘침」으로 보였던 것은 고정 캡처가
                // 에디트 모드라 **문구가 짧았기 때문**이다.
                //
                // 글자를 줄이는 길은 막혀 있다 — `UP-FIX-86` 이 배율 0.339 → 1.018 로
                // 올린 것을 되돌리는 셈이 된다. 그래서 **줄을 접는다.**
                // 접은 뒤 최장 줄은 「위험도 안정」 0.55 m 로 판 안이다.
                // 아래 네 줄은 `AscendReferenceRoomRewire.PanelRows` 가 90mm 씩 내린다.
                _text.Append(floor.Plan.Floor).Append("층 / ").Append(run.Floors.LastFloor)
                     .Append('\n').Append("위험도 ")
                     .Append(_risk != null ? _risk.Level.DisplayName() : "—");
                Apply(_floorLabel, _text);
            }

            // 전력은 정수 단위로만 표시하므로 반올림 값이 같으면 다시 만들 이유가 없다.
            //
            // **표시하는 값 전부를 비교한다.** 예전에는 `RoundToInt(Power) * 8 + Band`
            // 하나로 눌러 담았는데, 문자열에는 `RequiredPower`도 들어가는데 키가 그것을
            // 보지 않았다. 층이 바뀌어 전력이 0으로 돌아가고 밴드가 같으면 키가 동일해져
            // 라벨이 갱신되지 않는다 — 실제로 10층 캡처에 **1층의 요구 전력 350**이
            // 그대로 남아 있었고 독립 감사가 잡았다.
            //
            // 정수 하나로 압축하는 대신 값을 따로 들고 비교한다. 비트를 접는 순간
            // "무엇이 키에 빠졌는가"가 눈에 안 보이게 되고, 그게 이 결함의 원인이었다.
            // 연출 중에는 **스핀 결과를 담은 세 블록만** 건너뛴다 (`UP-FIX-15`).
            // 층·위험도(`_floorLabel`)와 계약 미리보기·명판은 살려 둔다 — 스핀 결과가
            // 아니고, 얼리면 연출 중 위험 표시가 죽는다.
            //
            // 캐시 키(`_shownPower` 등)를 **건드리지 않고** 건너뛰므로 직전 값 문자열이
            // 그대로 남는다. 추가 상태가 필요 없고, 연출이 끝나면 다음 프레임에 정상 갱신된다.
            // `15`·`19`·`21` 은 연출자를 거치지 않고 판을 밀어 넣으므로 이 게이트가 무력 → 회귀 없음.
            bool spoil = _presenter != null && !_presenter.ResultRevealed;
            if (spoil)
            {
                ApplyContractPreview(floor);
                ApplyPlaques(floor);
                return;
            }

            int power = Mathf.RoundToInt(floor.Power);
            int required = Mathf.RoundToInt(floor.RequiredPower);
            int band = (int)floor.CurrentBand;
            if (power != _shownPower || required != _shownRequired || band != _shownBand)
            {
                _shownPower = power;
                _shownRequired = required;
                _shownBand = band;
                if (_requiredLabel != null)
                {
                    // 두 줄로 나눈다 — 각 줄이 5.0 rect 단위 아래라 통관 그림자와
                    // 레버 덮개 사이에 온전히 들어간다 (`UP-FIX-23`).
                    //
                    // ⚠ **공백 셋과 `{0:P0}` 을 쓰지 않는다** (`UP-FIX-88`).
                    //
                    // 이 한 줄이 계기판에서 **가장 넓은 줄**이고, 4차 독립 평가가
                    // 「B·F 포즈에서 `0 %` 가 우측 프레임 밖으로 절단」이라고 잡은 것이
                    // 정확히 이 줄이다. 실측(로컬 글리프 상자, 저장 씬 기본값 기준) —
                    //
                    //   `요구 0   0 %`  글리프 폭 0.6445 m  ← 우측 절단
                    //   `요구 0 0%`     글리프 폭 0.5455 m
                    //
                    // 지운 것은 **글자가 아니라 공백 셋**이다(공백 하나 = 0.0330 m).
                    // `{0:P0}` 은 ko-KR 에서 값과 `%` 사이에 공백을 하나 더 넣는다 —
                    // 즉 눈에 안 보이는 곳에서 폭을 0.033 m 쓰고 있었다.
                    // 글자 크기는 손대지 않는다. `UP-FIX-86` 이 배율을 0.3393 → 1.0000 으로
                    // 올린 것을 되돌리는 순간 「잘려서 못 읽는다」가 「작아서 못 읽는다」가 된다.
                    //
                    // 🔴 **달성률을 이 줄에서 뺀다** (`UP-FIX-93`, 2026-08-04).
                    //
                    // 5차 독립 평가: 「`요구`/`0`/`0%` 세 토큰의 간격이 균등해 **요구값과
                    // 달성률을 가를 수 없다**」. 판독성 「전력↔요구 두 줄」이 4 → 3 으로
                    // 내려간 원인이고, 그것은 바로 위 `UP-FIX-88`(폭 줄이기)의 부작용이었다.
                    //
                    // 공백을 다시 넣으면 폭이 돌아오고 B 포즈 절단이 커진다 — 두 요구가
                    // 같은 줄에서 서로를 배신한다. **그래서 토큰을 하나로 줄인다.**
                    // 달성률은 `AscentColumnView` 가 물리 탱크의 **채움 높이**와
                    // 「전력 N   P%」 한 줄로 가져갔다. 회색조에서 살아남는 쪽으로 옮긴 것이고,
                    // 이 줄은 「요구는 얼마인가」 하나만 말한다.
                    _text.Clear();
                    _text.Append("전력 ").AppendFormat("{0:F0}", floor.Power);
                    Apply(_powerLabel, _text);

                    _text.Clear();
                    _text.Append("요구 ").AppendFormat("{0:F0}", floor.RequiredPower);
                    Apply(_requiredLabel, _text);
                }
                else
                {
                    // 배선이 없으면 옛 한 줄. 정보가 사라지는 것보다 잘리는 편이 낫다.
                    _text.Clear();
                    _text.Append("전력 ").AppendFormat("{0:F0}", floor.Power)
                         .Append(" / 요구 ").AppendFormat("{0:F0}", floor.RequiredPower)
                         .Append("   ").AppendFormat("{0:P0}", ratio)
                         .Append("  ").Append(floor.CurrentBand.DisplayName());
                    Apply(_powerLabel, _text);
                }
            }

            int statusKey = floor.SpinsRemaining
                          | (floor.ExtraSpinsTaken << 4)
                          | (floor.Residual.AbsorberCount << 8)
                          | (floor.Residual.ProliferatorCount << 12)
                          | ((int)floor.Phase << 16)
                          | ((floor.CanBank ? 1 : 0) << 20);
            if (statusKey != _statusKey)
            {
                _statusKey = statusKey;
                BuildStatus(floor);
                Apply(_statusLabel, _text);
            }

            ApplyContractPreview(floor);
            ApplyBar(ratio, floor);
            ApplyPlaques(floor);
        }

        /// <summary>
        /// 지금 넘겨보고 있는 계약의 **세 요소를 함께** 보여준다.
        ///
        /// `visual-criteria` B-4.11이 요구하는 것이 정확히 이것이다 —
        /// "출현률↑·정화 보상↑·잔류 대가↑가 **함께** 제시되는가. 보상만 크게 보이고
        /// 대가가 작게 적혀 있으면 실패다 — 그건 선택이 아니라 함정이다."
        ///
        /// 그런데 계약 장치에는 색만 바뀌는 명판 세 장뿐이었고 수치는 어디에도 없었다.
        /// 독립 평가자가 "빈 프레임 안에 막대 세 개. 선택지·보상·대가 어느 것도 없다"고
        /// 지적했다. 문구는 `ResistanceContract.Preview()`가 이미 만들고 있었다 —
        /// 데이터가 소유하고 UI 는 자리만 내주면 되는 구조였는데 자리가 없었다.
        /// </summary>
        private void ApplyContractPreview(FloorSession floor)
        {
            if (_contractLabel == null) return;

            var choices = floor.Plan.ContractChoices;
            bool selecting = floor.Phase == FloorPhase.ContractSelection &&
                             choices != null && choices.Length > 0;

            // 미리보기 중이면 그것을, 확정 뒤에는 고른 것을 보여준다.
            int preview = _bridge != null ? _bridge.PreviewIndex : 0;
            int shown = selecting ? Mathf.Clamp(preview, 0, choices.Length - 1) : -1;

            // 확정 뒤에는 **어느 계약을 골랐는지**까지 키에 넣는다. 상태(선택중/확정)만
            // 보면 다른 계약으로 확정된 다음 층에서 이전 층의 문구가 남는다.
            string selectedLabel = selecting ? null : floor.SelectedContract.Label;
            if (selecting == _contractSelecting && shown == _contractShown &&
                selectedLabel == _contractSelectedLabel) return;
            _contractSelecting = selecting;
            _contractShown = shown;
            _contractSelectedLabel = selectedLabel;

            _text.Clear();
            // **여기에 시너지 줄을 붙이지 않는다** (`UP-CONTRACT-05`).
            // 이 메서드는 씬에서 `_contractLabel: {fileID: 0}` 이라 위 가드에서 즉시 반환한다 —
            // `UP-DEVICE-06` 이 「계기판의 계약 표시는 죽은 경로다」로 기록해 둔 자리다.
            // 한 번 여기 붙였다가 되돌렸다: 컴파일도 되고 테스트도 통과하는데
            // **화면에는 아무것도 안 나온다.** 살아 있는 경로는 `ApplyPlaqueLabel` 이다.
            if (selecting)
            {
                _text.Append("계약 ").Append(shown + 1).Append('/').Append(choices.Length)
                     .Append("   ").Append(choices[shown].Label).AppendLine();
                _text.Append(choices[shown].Preview());
            }
            else
            {
                _text.Append("확정 — ").Append(floor.SelectedContract.Label).AppendLine();
                _text.Append(floor.SelectedContract.Preview());
            }
            Apply(_contractLabel, _text);
        }

        /// <summary>결과를 <see cref="_text"/>에 남긴다. 문자열을 돌려주지 않는다 — 그게 할당이다.</summary>
        private void BuildStatus(FloorSession floor)
        {
            // **세 줄로 나눈다.** 「두 줄을 넘기지 않는다」였고, 그래서 잔류 두 종류를
            // `  /  ` 로 이어 한 줄에 넣었다. 그 줄이 안 들어간다 — 재 보고 알았다.
            //
            //   쓸 수 있는 폭            20.80
            //   흡수체 절만              18.15  들어감
            //   스핀·과수확·판돈 줄      19.42  들어감
            //   흡수체 + 증식체 이어붙임 34.65  **넘침**
            //
            // 넘친 14 단위가 화면에서 x≈1273 px 의 잉크 절단으로 나온 것이다.
            // 9차·10차 판정이 두 번 같은 자리를 짚었고, 10차는 「그 줄이 프레임에 걸린
            // 10장 중 9장에서 값이 사라진다」고 실측했다. 잘린 것이 하필 `B-3 #10` 이
            // 요구하는 대가 수치다.
            //
            // 두 라운드 동안 나는 이걸 글자 크기 문제로 보고 오토사이즈를 걸었다.
            // 틀린 축이었다 — 오토사이즈는 34.65 를 20.80 에 넣으려고 글자를 60% 로
            // 줄이고, 그러면 「잘려서 못 읽는다」가 「작아서 못 읽는다」로 바뀔 뿐이다.
            // 10차가 `18` 에서 그 결과를 h=20 px 로 실측했다(다른 줄 27~31 px).
            //
            // 높이는 문제가 아니었다 — 세 줄이 3.52 / 6.00 이다. 폭이 문제였고,
            // 폭을 못 늘리면 줄을 늘리는 것이 남은 축이다.
            _text.Clear();
            _text.Append("스핀 ").Append(floor.SpinsRemaining).Append('/').Append(floor.Plan.Spins);
            if (floor.ExtraSpinsTaken > 0)
                _text.Append("   과수확 ").Append(floor.ExtraSpinsTaken).Append('회');
            if (floor.Phase == FloorPhase.Decision && floor.CanBank && floor.SpinsRemaining > 0)
                _text.Append("   판돈 ").AppendFormat("{0:F0}", floor.PendingAnte);
            _text.AppendLine();

            // 잔류 저항은 "숫자만 작게" 두면 위협으로 안 읽힌다(visual-criteria B-3.10).
            // 그래서 계기판 본문에 원인 문장으로 올린다.
            // 잔류 조립은 `AppendResidual` 하나가 소유한다. **반드시 한 줄이다** —
            // 여기서 줄이 하나 더 생기면 셋째 줄이 `CascadeLabel` 자리에 앉는다
            // (`UP-FIX-51`). 그 함수의 주석이 계산과 이유를 들고 있다.
            AppendResidual(floor.Residual, _text);
        }

        /// <summary>
        /// 상태 라벨의 **잔류 줄**을 만든다. 반드시 **한 줄**이다.
        ///
        /// ## 왜 줄바꿈이 금지인가 (`UP-FIX-51`)
        ///
        /// 상태 라벨의 첫 줄은 스핀·판돈이 쓴다. 잔류가 둘째 줄이고, 여기서
        /// 줄을 하나 더 만들면 **셋째 줄상자가 판 로컬 y 1.402…1.486 에 앉는다.**
        /// 그 자리는 `CascadeLabel`(1.400…1.484) — 계기판 여섯째 줄의 자리다.
        /// 글자끼리 겹치면 둘 다 못 읽고, **가림 계측은 글자↔글자 겹침을 예외로
        /// 두기 때문에 매니페스트가 둘 다 「온전」이라고 적는다.** 즉 자동 검사가
        /// 잡지 못하는 종류의 손상이다. 그래서 여기서 구조적으로 막는다.
        ///
        /// 옮겨서 피할 수 없다 — 위는 상태 2줄, 아래는 `PowerBarTicks`(…1.395)가
        /// 막고 있어 남는 띠가 하나뿐이다(`CascadeLineBuilder` 주석의 계산).
        ///
        /// 대신 **폭을 쓴다.** 조사와 단위를 덜어 두 항목을 한 줄에 잇는다.
        /// 길어진 줄은 <see cref="Fit"/> 의 자동 축소가 흡수한다 — 이 라벨에서
        /// 이미 내린 판단이다: 잘린 숫자는 못 읽지만 작아진 숫자는 읽힌다.
        ///
        /// `static` 인 이유는 씬 없이 검사하기 위해서다.
        /// </summary>
        public static void AppendResidual(in ResidualState residual, StringBuilder into)
        {
            if (into == null) return;
            if (residual.IsClean) { into.Append("잔류 없음"); return; }

            if (residual.AbsorberCount > 0)
                into.Append("흡수체 ").Append(residual.AbsorberCount)
                    .Append(" −").AppendFormat("{0:F1}", residual.StoredPowerLoss);
            // ⚠ 공백이다. `AppendLine()` 으로 바꾸면 위 주석의 겹침이 그대로 돌아온다.
            if (residual.AbsorberCount > 0 && residual.ProliferatorCount > 0)
                into.Append("   ");
            if (residual.ProliferatorCount > 0)
                into.Append("증식체 ").Append(residual.ProliferatorCount)
                    .Append(" +").AppendFormat("{0:F2}", residual.NextProliferatorWeightAdd);
        }

        private void ShowRunOver(RunSession run)
        {
            int key = run.IsFailed ? -1 : -2;
            if (key == _floorKey) return;   // 종료 화면은 더 바뀌지 않는다
            _floorKey = key;

            SetText(_floorLabel, run.IsFailed ? "층 실패" : "층 확정");
            var results = run.Results;
            if (results.Count == 0)
            {
                SetBar(0f, _belowRequired);
                // 종료 화면에서도 직전 층의 「요구」가 남아 있으면 안 된다 — 아래 참조.
                if (_requiredLabel != null) SetText(_requiredLabel, string.Empty);
                return;
            }

            FloorResult last = results[results.Count - 1];

            // **9차 판정이 여기서 자기모순을 잡았다.** 「전력 933 / 요구 1508」 바로 아래
            // 「요구 365  0 %」가 함께 떠 있었다 — 종료 경로는 옛 한 줄을 `_powerLabel` 에
            // 쓰는데 `_requiredLabel` 에는 **직전 층의 값이 그대로 남아** 있었기 때문이다.
            //
            // 줄을 나눈 쪽만 고치고 종료 경로를 안 고친 내 실수다. 한 화면에 같은 항목이
            // 두 번, 그것도 다른 값으로 나오는 것은 「기록기가 자기가 설명해야 할 사고를
            // 부정한다」와 같은 종류의 실패다(`UP-FIX-11`).
            if (_requiredLabel != null)
            {
                SetText(_powerLabel, $"전력 {last.FinalPower:F0}");
                SetText(_requiredLabel, $"요구 {last.RequiredPower:F0}   {last.Band.DisplayName()}");
            }
            else
            {
                SetText(_powerLabel, $"전력 {last.FinalPower:F0} / 요구 {last.RequiredPower:F0}   " +
                                     $"{last.Band.DisplayName()}");
            }
            SetText(_statusLabel, last.ExtraSpinsTaken > 0
                ? $"과수확 {last.ExtraSpinsTaken}회 / 판돈 {last.TotalAnte:F0} / 순손익 {last.NetProfit:+0;−0;0}"
                : "과수확 없이 확정");

            // **게이지를 0 으로 밀고 있었다.** 글자는 "전력 314 / 요구 355"라고 말하는데
            // 바로 아래 막대는 비어 있었다. 독립 평가자가 이걸 "88%가 0%처럼 보인다 —
            // 가시성 문제"로 읽었지만, 캡처가 실측치를 적게 하자 진짜 원인이 드러났다.
            // 막대는 안 보인 게 아니라 **실제로 0** 이었다. 화면 안의 두 표시가 서로를
            // 부정하고 있었던 것이고, 그건 `VISUAL_SPEC` §6이 금지하는 정보 은폐다.
            //
            // 끝난 층도 자기 결과를 말해야 한다. 마지막 층의 실제 비율을 그대로 세운다.
            float ratio = last.RequiredPower > 0f ? last.FinalPower / last.RequiredPower : 0f;
            SetBar(ratio, ratio < 1f ? _belowRequired
                        : last.ExtraSpinsTaken > 0 ? _overharvested
                        : _atRequired);
        }

        /// <summary>
        /// 게이지는 요구 전력(100%)을 넘는 순간 색이 바뀐다. 임계점 돌파가 단순한 숫자 증가가
        /// 아니라 사건으로 보여야 한다는 요구(`visual-criteria.md` B-3.9)의 최소선이다.
        /// </summary>
        /// <summary>
        /// `UP-FIX-10` — 게이지가 **위급도와 반대로** 움직이고 있었다.
        ///
        /// 7차 판정: 「방이 가장 붉은 `16`(Collapse) 에서 전력 바가 **창백한 라벤더-흰색
        /// 조각**이다.」 원인은 색이 전력% 에만 묶여 있고 위험 단계를 **한 번도 읽지
        /// 않은 것**이다 — 300% 를 넘긴 순간이 가장 밝고, 그 순간이 곧 가장 위험한 순간이다.
        ///
        /// 고치는 축은 **명도**다(그룹 B 와 같은 교훈). 색상만 붉게 물들이면 회색조에서
        /// 여전히 같은 밴드로 읽힌다 — 이 저장소가 「색거리 2배」로 두 번 실패한 자리다.
        /// 위험이 오를수록 ① 채움색을 위험색 쪽으로 물들이고 ② **명도를 함께 낮춘다.**
        /// 두 축이 같은 방향으로 움직여야 경계가 선다.
        ///
        /// `_risk` 가 없으면 옛 동작 그대로다 — 배선이 빠져도 게이지가 죽지 않는다.
        /// </summary>
        private void ApplyBar(float ratio, FloorSession floor)
        {
            Color color = ratio < 1f ? _belowRequired
                        : floor.ExtraSpinsTaken > 0 ? _overharvested
                        : _atRequired;

            if (_risk != null)
            {
                RiskLevel level = _risk.Level;
                // Stable 에서 0, Collapse 에서 1. 단계 수가 바뀌어도 따라간다.
                float t = (int)RiskLevel.Collapse > 0
                        ? Mathf.Clamp01((float)(int)level / (int)RiskLevel.Collapse) : 0f;

                // ① 색상축 — 위험색 쪽으로. 절반까지만 물들여 「전력이 얼마나 찼는가」를 지운다.
                color = Color.Lerp(color, _dangerTint, t * 0.55f);

                // ② 명도축 — Stable 대비 그 단계의 앰비언트 명도 비율만큼 어둡게.
                //    바닥 0.45 를 둔다. 0 까지 내리면 Collapse 에서 게이지가 사라져
                //    `VISUAL_SPEC §6`(암전으로 결과를 숨기지 않는다)을 어긴다.
                float top = _risk.AmbientValueFor(RiskLevel.Stable);
                if (top > 0.0001f)
                {
                    float v = Mathf.Clamp(_risk.AmbientValueFor(level) / top, 0.45f, 1f);
                    color = new Color(color.r * v, color.g * v, color.b * v, color.a);
                }
            }

            SetBar(ratio, color);
        }

        private void SetBar(float ratio, Color color)
        {
            // ── 바 길이는 **100 에서 끝난다** (2026-08-08 사용자 지시) ─────────────
            //
            // 「전력 게이지도 끝까지 숫자 100 써 있는 곳까지 차야 하는데 이상한 곳에 멈추네.
            //  100까지는 동일하게 채워지고 100 이상 가면 색상 바뀌고 이펙트 주는 걸로,
            //  바는 더 이상 안 늘어나고.」
            //
            // 직전 판본은 `clamped / _maxRatio` 였다. `_maxRatio` 가 3 이므로 요구 전력을
            // 정확히 채워도(ratio 1.0) 바는 **1/3 만** 찼다 — 눈금판의 「100」과 어긋난다.
            // 눈금은 요구 전력을 100 으로 읽는데 바는 300% 를 만점으로 그리고 있었다.
            //
            // 초과분은 길이가 아니라 **색과 발광**이 맡는다. 그래야 「가득 찼다」와
            // 「넘쳤다」가 한 눈금 위에서 구분된다 — 길이로 둘 다 표현하면 100 지점이
            // 아무 의미 없는 중간 지점이 된다.
            float fill = Mathf.Clamp01(ratio);
            float over = Mathf.Max(0f, ratio - 1f);          // 0 = 미달·정확 · >0 = 초과

            if (_barPivot != null)
            {
                Vector3 scale = _barPivot.localScale;
                scale.x = _barWidth * fill;
                _barPivot.localScale = scale;
            }

            // **위험 단계가 전력 비율을 이긴다.** 이전에는 색이 비율과 과수확 횟수만
            // 보고 결정돼서, Collapse 인데 비율이 낮으면 창백한 회색이 되고 Stable
            // 336% 는 선명한 녹색이 됐다. 독립 시각 평가가 **세 번 연속** 「최악 상태의
            // 계기가 가장 태연하다」고 지적한 것이 이것이다(`UP-FIX-10`).
            //
            // 덮어쓰지 않고 **끌어당긴다** — 통째로 갈면 「전력이 얼마나 찼는가」라는
            // 게이지 본래의 정보가 사라진다. 색만 위급해지고 길이는 그대로다.
            color = ApplyRiskUrgency(color);

            if (_barFill == null) return;
            if (_block == null) _block = new MaterialPropertyBlock();   // 도메인 리로드 뒤 null
            _barFill.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            _block.SetColor(EmissionColorId, color * 1.6f);
            _barFill.SetPropertyBlock(_block);
        }

        /// <summary>
        /// 위험 단계에 따라 게이지 색을 위급 쪽으로 끌어당긴다.
        /// Stable·Strain 은 그대로 두고 Critical 부터 개입한다 — 이르게 개입하면
        /// 「전력이 모자란 것」과 「위험한 것」이 구분되지 않는다.
        /// </summary>
        private Color ApplyRiskUrgency(Color baseColor)
        {
            if (_risk == null) return baseColor;
            switch (_risk.Level)
            {
                case Risk.RiskLevel.Critical: return Color.Lerp(baseColor, _dangerTint, 0.55f);
                case Risk.RiskLevel.Collapse: return Color.Lerp(baseColor, _dangerTint, 0.85f);
                default:                      return baseColor;
            }
        }

        /// <summary>
        /// 명판 3장이 계약 선택지와 1:1로 대응한다. 미리보기 중인 것과 확정된 것을
        /// 다른 색·발광으로 구분해, 계약 패널을 누를 때 무엇이 움직이는지 벽에서 보이게 한다.
        /// </summary>
        private void ApplyPlaques(FloorSession floor)
        {
            var choices = floor.Plan.ContractChoices;
            bool selecting = floor.Phase == FloorPhase.ContractSelection;
            int preview = _bridge != null ? _bridge.PreviewIndex : -1;

            for (int i = 0; i < _contractPlaques.Length; i++)
            {
                Renderer plaque = _contractPlaques[i];
                if (plaque == null) continue;

                bool exists = choices != null && i < choices.Length;
                Color color = _plaqueIdle;
                float emission = 0f;

                if (exists && selecting && i == preview) { color = _plaquePreview; emission = 2.2f; }
                else if (exists && !selecting && SameContract(choices[i], floor.SelectedContract))
                {
                    color = _plaqueChosen;
                    emission = 1.4f;
                }
                else if (exists && selecting) { color = Color.Lerp(_plaqueIdle, Color.white, 0.25f); }

                if (_block == null) _block = new MaterialPropertyBlock();   // 도메인 리로드 뒤 null
                plaque.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                _block.SetColor(EmissionColorId, color * emission);
                plaque.SetPropertyBlock(_block);

                ApplyPlaqueLabel(i, exists, exists ? choices[i] : ResistanceContract.None, floor.Loadout);
            }
        }

        /// <summary>
        /// 명판 한 장의 문구. 셋이 **같은 크기·같은 순서**로 나열되어야 비교가 된다 —
        /// `visual-criteria` B-4.11이 "보상만 크게 보이고 대가가 작게 적혀 있으면
        /// 함정이다"라고 못 박은 지점이다. 문구는 데이터가 만든다.
        /// </summary>
        private void ApplyPlaqueLabel(int index, bool exists, in ResistanceContract contract,
                                      Build.BuildLoadout loadout)
        {
            if (_plaqueLabels == null || index >= _plaqueLabels.Length) return;
            TextMeshPro label = _plaqueLabels[index];
            if (label == null) return;

            if (!exists)
            {
                if (_plaqueKeys[index] == 0) return;
                _plaqueKeys[index] = 0;
                label.SetText(string.Empty);
                return;
            }

            // 층이 바뀌어야 선택지가 바뀐다. 라벨명 해시를 키로 쓰면 매 프레임
            // 문자열을 만들지 않고도 교체 시점을 잡을 수 있다.
            //
            // **적재 상태를 키에 섞는다** (`UP-CONTRACT-05`). 시너지 줄은 적재에 따라
            // 달라지는데 계약 이름만 키로 쓰면 층 안에서 부품을 실어도 문구가 얼어붙는다 —
            // 그러면 「선택 전에 공개한다」가 **틀린 값을 공개하는 것**이 된다.
            // 칸 수와 총무게만 섞어도 적재 변화는 전부 잡힌다(부품이 바뀌면 둘 중 하나가 움직인다).
            int key = contract.Label != null ? contract.Label.GetHashCode() : 1;
            if (loadout != null)
                key = key * 31 + loadout.Count * 7919 + Mathf.RoundToInt(loadout.TotalWeight * 100f);
            if (_plaqueKeys[index] == key) return;
            _plaqueKeys[index] = key;

            _text.Clear();
            _text.Append(contract.Label).AppendLine();
            _text.Append(contract.Preview()).AppendLine();
            // 필수 4정보의 넷째. 위 `Preview()` 가 셋을 낸다 (`NotionSyncReport.md:166`).
            _text.Append(contract.SynergyWith(loadout));
            Apply(label, _text);
        }

        private static bool SameContract(in ResistanceContract a, in ResistanceContract b)
            => a.Target == b.Target && a.Label == b.Label;

        /// <summary>TMP의 StringBuilder 오버로드는 중간 string을 만들지 않는다.</summary>
        private static void Apply(TextMeshPro label, StringBuilder value)
        {
            if (label != null) label.SetText(value);
        }

        private static void SetText(TextMeshPro label, string value)
        {
            if (label != null && label.text != value) label.text = value;
        }
    }
}
