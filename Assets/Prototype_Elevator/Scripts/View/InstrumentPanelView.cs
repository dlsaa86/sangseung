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

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();
            if (_bridge == null) _bridge = FindAnyObjectByType<RouletteInteractionBridge>();
            if (_risk == null) _risk = FindAnyObjectByType<RiskStateView>();
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

            SetText(_floorLabel, $"{floor.Plan.Floor}층 / {run.Floors.LastFloor}   " +
                                 $"위험 {(_risk != null ? _risk.Level.DisplayName() : "—")}");

            SetText(_powerLabel, $"전력 {floor.Power:F0} / 요구 {floor.RequiredPower:F0}   " +
                                 $"{ratio:P0}  {floor.CurrentBand.DisplayName()}");

            SetText(_statusLabel, BuildStatus(floor));
            ApplyBar(ratio, floor);
            ApplyPlaques(floor);
        }

        private string BuildStatus(FloorSession floor)
        {
            _text.Clear();
            _text.Append($"스핀 {floor.SpinsRemaining}/{floor.Plan.Spins}");
            if (floor.ExtraSpinsTaken > 0) _text.Append($"   과수확 {floor.ExtraSpinsTaken}회");
            _text.AppendLine();

            // 잔류 저항은 "숫자만 작게" 두면 위협으로 안 읽힌다(visual-criteria B-3.10).
            // 그래서 계기판 본문에 원인 문장으로 올린다.
            ResidualState residual = floor.Residual;
            _text.Append(residual.IsClean ? "잔류 없음" : residual.Describe());

            if (floor.Phase == FloorPhase.Decision && floor.CanBank && floor.SpinsRemaining > 0)
                _text.Append($"\n확정 가능 / 과수확 판돈 {floor.PendingAnte:F0}");
            return _text.ToString();
        }

        private void ShowRunOver(RunSession run)
        {
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

        private static void SetText(TextMeshPro label, string value)
        {
            if (label != null && label.text != value) label.text = value;
        }
    }
}
