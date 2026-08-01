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

        [Tooltip("계약 미리보기 — 출현률·정화 보상·잔류 대가를 한 줄에. 비어 있으면 표시하지 않는다.")]
        [SerializeField] private TextMeshPro _contractLabel;

        [Header("전력 게이지")]
        [SerializeField] private Transform _barPivot;
        [Tooltip("게이지 전체 폭(미터). 0%~이 폭이 MaxRatio에 대응한다.")]
        [SerializeField] private float _barWidth = 1.72f;
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
            _contractSelecting = false;
            _contractShown = int.MinValue;
            _contractSelectedLabel = null;
            for (int i = 0; i < _plaqueKeys.Length; i++) _plaqueKeys[i] = int.MinValue;
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
            if (results.Count == 0) { SetBar(0f, _belowRequired); return; }

            FloorResult last = results[results.Count - 1];
            SetText(_powerLabel, $"전력 {last.FinalPower:F0} / 요구 {last.RequiredPower:F0}   " +
                                 $"{last.Band.DisplayName()}");
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

            // **위험 단계가 전력 비율을 이긴다.** 이전에는 색이 비율과 과수확 횟수만
            // 보고 결정돼서, Collapse 인데 비율이 낮으면 창백한 회색이 되고 Stable
            // 336% 는 선명한 녹색이 됐다. 독립 시각 평가가 **세 번 연속** 「최악 상태의
            // 계기가 가장 태연하다」고 지적한 것이 이것이다(`UP-FIX-10`).
            //
            // 덮어쓰지 않고 **끌어당긴다** — 통째로 갈면 「전력이 얼마나 찼는가」라는
            // 게이지 본래의 정보가 사라진다. 색만 위급해지고 길이는 그대로다.
            color = ApplyRiskUrgency(color);

            if (_barFill == null) return;
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

                plaque.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                _block.SetColor(EmissionColorId, color * emission);
                plaque.SetPropertyBlock(_block);

                ApplyPlaqueLabel(i, exists, exists ? choices[i] : ResistanceContract.None);
            }
        }

        /// <summary>
        /// 명판 한 장의 문구. 셋이 **같은 크기·같은 순서**로 나열되어야 비교가 된다 —
        /// `visual-criteria` B-4.11이 "보상만 크게 보이고 대가가 작게 적혀 있으면
        /// 함정이다"라고 못 박은 지점이다. 문구는 데이터가 만든다.
        /// </summary>
        private void ApplyPlaqueLabel(int index, bool exists, in ResistanceContract contract)
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
            int key = contract.Label != null ? contract.Label.GetHashCode() : 1;
            if (_plaqueKeys[index] == key) return;
            _plaqueKeys[index] = key;

            _text.Clear();
            _text.Append(contract.Label).AppendLine();
            _text.Append(contract.Preview());
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
