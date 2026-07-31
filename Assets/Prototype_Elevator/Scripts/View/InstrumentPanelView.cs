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

        [Header("표시")]
        [SerializeField] private TextMeshPro _floorLabel;
        [SerializeField] private TextMeshPro _powerLabel;
        [SerializeField] private TextMeshPro _statusLabel;

        [Header("전력 게이지")]
        [SerializeField] private Transform _barPivot;
        [Tooltip("게이지 전체 폭(미터). 0%~이 폭이 MaxRatio에 대응한다.")]
        [SerializeField] private float _barWidth = 1.72f;
        [Tooltip("게이지가 표시하는 최대 비율. 임계점 표(300%)에 맞춘다.")]
        [SerializeField] private float _maxRatio = 3f;
        [SerializeField] private Renderer _barFill;

        [Header("계약 명판 — 위에서부터 선택지 순서")]
        [SerializeField] private Renderer[] _contractPlaques = new Renderer[3];
        [SerializeField] private Color _plaqueIdle = new Color(0.20f, 0.21f, 0.23f);
        [SerializeField] private Color _plaquePreview = new Color(0.95f, 0.80f, 0.35f);
        [SerializeField] private Color _plaqueChosen = new Color(0.45f, 0.90f, 0.70f);

        [Header("게이지 색 — 밴드 구분")]
        [SerializeField] private Color _belowRequired = new Color(0.55f, 0.60f, 0.68f);
        [SerializeField] private Color _atRequired = new Color(0.45f, 0.90f, 0.70f);
        [SerializeField] private Color _overharvested = new Color(1f, 0.66f, 0.25f);

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

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();
            if (_bridge == null) _bridge = FindAnyObjectByType<RouletteInteractionBridge>();
            if (_risk == null) _risk = FindAnyObjectByType<RiskStateView>();
            if (_run != null) _run.RunStarted += _ => InvalidateCache();
        }

        /// <summary>런이 새로 시작되면 캐시 키를 푼다. 안 그러면 종료 화면이 그대로 남는다.</summary>
        private void InvalidateCache()
        {
            _floorKey = int.MinValue;
            _shownPower = int.MinValue;
            _shownRequired = int.MinValue;
            _shownBand = int.MinValue;
            _statusKey = int.MinValue;
        }

        private void LateUpdate()
        {
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
                _text.Append(floor.Plan.Floor).Append("층 / ").Append(run.Floors.LastFloor)
                     .Append("   위험도 ")
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
            int power = Mathf.RoundToInt(floor.Power);
            int required = Mathf.RoundToInt(floor.RequiredPower);
            int band = (int)floor.CurrentBand;
            if (power != _shownPower || required != _shownRequired || band != _shownBand)
            {
                _shownPower = power;
                _shownRequired = required;
                _shownBand = band;
                _text.Clear();
                _text.Append("전력 ").AppendFormat("{0:F0}", floor.Power)
                     .Append(" / 요구 ").AppendFormat("{0:F0}", floor.RequiredPower)
                     .Append("   ").AppendFormat("{0:P0}", ratio)
                     .Append("  ").Append(floor.CurrentBand.DisplayName());
                Apply(_powerLabel, _text);
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

            ApplyBar(ratio, floor);
            ApplyPlaques(floor);
        }

        /// <summary>결과를 <see cref="_text"/>에 남긴다. 문자열을 돌려주지 않는다 — 그게 할당이다.</summary>
        private void BuildStatus(FloorSession floor)
        {
            // **두 줄을 넘기지 않는다.** 세 줄이 되면 아래의 전력 게이지를 덮어
            // 잔류 경고와 눈금이 서로를 가린다 — 실제로 첫 캡처에서 그렇게 나왔다.
            _text.Clear();
            _text.Append("스핀 ").Append(floor.SpinsRemaining).Append('/').Append(floor.Plan.Spins);
            if (floor.ExtraSpinsTaken > 0)
                _text.Append("   과수확 ").Append(floor.ExtraSpinsTaken).Append('회');
            if (floor.Phase == FloorPhase.Decision && floor.CanBank && floor.SpinsRemaining > 0)
                _text.Append("   판돈 ").AppendFormat("{0:F0}", floor.PendingAnte);
            _text.AppendLine();

            // 잔류 저항은 "숫자만 작게" 두면 위협으로 안 읽힌다(visual-criteria B-3.10).
            // 그래서 계기판 본문에 원인 문장으로 올린다.
            ResidualState residual = floor.Residual;
            if (residual.IsClean) _text.Append("잔류 없음");
            else
            {
                if (residual.AbsorberCount > 0)
                    _text.Append("흡수체 ").Append(residual.AbsorberCount)
                         .Append("개 → 저장 전력 −").AppendFormat("{0:F1}", residual.StoredPowerLoss);
                if (residual.AbsorberCount > 0 && residual.ProliferatorCount > 0)
                    _text.Append("  /  ");
                if (residual.ProliferatorCount > 0)
                    _text.Append("증식체 ").Append(residual.ProliferatorCount)
                         .Append("개 → 다음 스핀 출현 +")
                         .AppendFormat("{0:F2}", residual.NextProliferatorWeightAdd);
            }
        }

        private void ShowRunOver(RunSession run)
        {
            int key = run.IsFailed ? -1 : -2;
            if (key == _floorKey) return;   // 종료 화면은 더 바뀌지 않는다
            _floorKey = key;

            SetText(_floorLabel, run.IsFailed ? "층 실패" : "층 확정");
            var results = run.Results;
            if (results.Count > 0)
            {
                FloorResult last = results[results.Count - 1];
                SetText(_powerLabel, $"전력 {last.FinalPower:F0} / 요구 {last.RequiredPower:F0}   " +
                                     $"{last.Band.DisplayName()}");
                SetText(_statusLabel, last.ExtraSpinsTaken > 0
                    ? $"과수확 {last.ExtraSpinsTaken}회 / 판돈 {last.TotalAnte:F0} / 순손익 {last.NetProfit:+0;−0;0}"
                    : "과수확 없이 확정");
            }
            SetBar(0f, _belowRequired);
        }

        /// <summary>
        /// 게이지는 요구 전력(100%)을 넘는 순간 색이 바뀐다. 임계점 돌파가 단순한 숫자 증가가
        /// 아니라 사건으로 보여야 한다는 요구(`visual-criteria.md` B-3.9)의 최소선이다.
        /// </summary>
        private void ApplyBar(float ratio, FloorSession floor)
        {
            Color color = ratio < 1f ? _belowRequired
                        : floor.ExtraSpinsTaken > 0 ? _overharvested
                        : _atRequired;
            SetBar(ratio, color);
        }

        private void SetBar(float ratio, Color color)
        {
            if (_barPivot != null)
            {
                float clamped = Mathf.Clamp(ratio, 0f, _maxRatio);
                Vector3 scale = _barPivot.localScale;
                scale.x = _barWidth * (clamped / Mathf.Max(0.0001f, _maxRatio));
                _barPivot.localScale = scale;
            }

            if (_barFill == null) return;
            _barFill.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            _block.SetColor(EmissionColorId, color * 1.6f);
            _barFill.SetPropertyBlock(_block);
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

                plaque.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                _block.SetColor(EmissionColorId, color * emission);
                plaque.SetPropertyBlock(_block);
            }
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
