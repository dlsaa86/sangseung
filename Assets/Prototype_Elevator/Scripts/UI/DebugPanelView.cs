using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Ascend.Prototype.Risk;
using Ascend.Prototype.Run;
using Ascend.Prototype.Spin;
using Ascend.Prototype.View;

namespace Ascend.Prototype.UI
{
    /// <summary>
    /// 디버그 패널. 게임 UI(<see cref="GameHudView"/>)와 **분리되어 있고 기본은 꺼져 있다.**
    ///
    /// 분리한 이유: `VISUAL_SPEC.md` §5의 표시 우선순위에서 "보조 수치와 디버그 정보"는
    /// 6순위, 즉 맨 끝이다. 이전 IMGUI HUD는 이걸 게임 정보와 같은 덩어리로 붙여 놓고
    /// 기본으로 켜 두어서, 캡처와 성능 양쪽에 상시로 끼어들었다.
    ///
    /// **게임 조작은 여기 없다.** 계약·스핀·확정·과수확은 전부 엘리베이터 안의 물체로만 한다
    /// (`CURRENT_PHASE.md` Gate B). 여기 있는 것은 재현과 조사 도구뿐이다:
    ///   [F1] 패널 · [R] 같은 시드 재시작 · [T] 시드 입력 · [L] 마지막 스핀 로그
    ///
    /// 시드 입력에 `TMP_InputField`를 쓰지 않는다. 1인칭 조작이 커서를 잠그고 있어서
    /// 입력 필드에 포커스를 주려면 커서 잠금을 풀어야 하고, 그러면 조사하려던 상태가
    /// 바뀐다. Input System의 `onTextInput`으로 직접 받으면 커서를 건드리지 않는다.
    /// </summary>
    public sealed class DebugPanelView : MonoBehaviour
    {
        [SerializeField] private RunSessionBehaviour _run;
        [SerializeField] private RouletteInteractionBridge _bridge;
        [SerializeField] private RiskStateView _risk;

        [SerializeField] private CanvasGroup _group;
        [SerializeField] private TextMeshProUGUI _bodyText;

        [Tooltip("기본은 꺼짐. 캡처와 성능 측정에 상시로 끼어들지 않게 한다.")]
        [SerializeField] private bool _visible;

        private readonly StringBuilder _text = new StringBuilder(512);
        private string _seedField = string.Empty;
        private bool _editingSeed;
        private string _note = string.Empty;
        private int _key = int.MinValue;

        /// <summary>검증 하네스가 상태를 확인할 때 쓴다.</summary>
        public bool IsVisible => _visible;

        private void Awake()
        {
            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();
            if (_bridge == null) _bridge = FindAnyObjectByType<RouletteInteractionBridge>();
            if (_risk == null) _risk = FindAnyObjectByType<RiskStateView>();
            if (_group != null) _group.alpha = _visible ? 1f : 0f;
        }

        private void OnEnable()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null) keyboard.onTextInput += OnTextInput;
        }

        private void OnDisable()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null) keyboard.onTextInput -= OnTextInput;
        }

        /// <summary>
        /// 릴리스 빌드에서는 **켜진 채로 시작하지 않는다.** `Awake` 가 씬에 직렬화된
        /// `_visible` 을 그대로 쓰기 때문에, 누가 인스펙터에서 체크해 둔 채 빌드하면
        /// 입력을 막아도 패널이 처음부터 떠 있다. 「기본 활성화」를 막는 것이 요구다.
        /// </summary>
        private void Start()
        {
            if (DebugToolsAllowed) return;
            SetVisible(false);
        }

        /// <summary>시드 편집 중에만 글자를 받는다. 평소에는 게임 단축키를 가로채지 않는다.</summary>
        private void OnTextInput(char character)
        {
            if (!_editingSeed) return;
            if (character >= '0' && character <= '9' && _seedField.Length < 9)
                _seedField += character;
            else if (character == '-' && _seedField.Length == 0)
                _seedField += character;
        }

        /// <summary>
        /// 개발 빌드와 에디터에서만 참. 릴리스 빌드에서는 F1·R·T·L 이 죽는다.
        ///
        /// N08 §17 이 요구하는 것은 「**개발 빌드에서만** 기본 활성화」인데
        /// 이 파일에는 `UNITY_EDITOR`·`DEVELOPMENT_BUILD`·`Debug.isDebugBuild` 가
        /// **하나도 없었다** — 릴리스에서 F1 이 시드 재시작(R)·시드 입력(T)·
        /// 스핀 로그(L)까지 그대로 열었다.
        ///
        /// 조건부 컴파일(`#if`)이 아니라 런타임 조건을 쓰는 이유: 컴포넌트 자체는
        /// 남아야 한다. `HeroSliceAutoPilot` 과 `HeroSlicePerfProbe` 가
        /// `FindAnyObjectByType&lt;DebugPanelView&gt;()` / `Disable&lt;DebugPanelView&gt;()` 로
        /// 이 타입을 참조하므로, 타입이 사라지면 하네스가 깨진다.
        /// `Debug.isDebugBuild` 는 에디터에서 참이라 하네스는 그대로 돈다.
        /// </summary>
        public static bool DebugToolsAllowed => Debug.isDebugBuild;

        private void Update()
        {
            if (!DebugToolsAllowed) return;

            Keyboard k = Keyboard.current;
            if (k == null || _run == null) return;

            if (k.f1Key.wasPressedThisFrame) SetVisible(!_visible);

            if (_editingSeed)
            {
                if (k.backspaceKey.wasPressedThisFrame && _seedField.Length > 0)
                    _seedField = _seedField.Substring(0, _seedField.Length - 1);

                if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame)
                {
                    if (int.TryParse(_seedField, out int seed))
                    {
                        _run.ResetRun(seed);
                        _note = $"시드 {seed} 로 재시작.";
                    }
                    else _note = "정수만 받는다.";
                    _editingSeed = false;
                    _key = int.MinValue;
                }
                else if (k.escapeKey.wasPressedThisFrame)
                {
                    _editingSeed = false;
                    _note = "시드 입력 취소.";
                    _key = int.MinValue;
                }
                Refresh();
                return;   // 편집 중에는 다른 단축키가 먹지 않는다
            }

            if (k.tKey.wasPressedThisFrame)
            {
                _editingSeed = true;
                _seedField = string.Empty;
                _note = "숫자 입력 후 [Enter], 취소는 [Esc]";
                if (!_visible) SetVisible(true);
            }

            if (k.rKey.wasPressedThisFrame)
            {
                _run.ResetRun();
                _note = $"시드 {_run.Seed} 로 재시작.";
            }

            if (k.lKey.wasPressedThisFrame) DumpLastSpin();

            Refresh();
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;
            if (_group != null) _group.alpha = visible ? 1f : 0f;
            _key = int.MinValue;
        }

        /// <summary>
        /// 마지막 스핀을 로그 한 줄 + 단계별 진단으로 남긴다. 그 줄만 있으면
        /// 헤드리스로 같은 스핀을 재현할 수 있다(`TECH_SPEC.md` §11).
        /// </summary>
        private void DumpLastSpin()
        {
            FloorSession floor = _run.Session != null ? _run.Session.Current : null;
            var history = floor != null ? floor.History : null;
            if (history == null || history.Count == 0) { _note = "기록된 스핀이 없다."; return; }

            SpinResolution last = history[history.Count - 1];
            Debug.Log($"[상승] 마지막 스핀 재현 정보\n{last.DescribeCascade()}");
            _note = "마지막 스핀을 콘솔에 기록했다.";
        }

        /// <summary>
        /// 꺼져 있으면 아무 일도 하지 않는다. 켜져 있어도 표시값이 바뀔 때만 문자열을 짓는다 —
        /// 이 패널이 매 프레임 할당하면 분리한 의미가 없다.
        /// </summary>
        private void Refresh()
        {
            if (!_visible || _bodyText == null) return;

            RunSession run = _run.Session;
            FloorSession floor = run != null ? run.Current : null;
            int riskLevel = _risk != null ? (int)_risk.Level : -1;
            bool locked = _bridge != null && _bridge.IsLocked;

            int key = _run.Seed
                    ^ (riskLevel << 8)
                    ^ ((locked ? 1 : 0) << 12)
                    ^ ((_editingSeed ? 1 : 0) << 13)
                    ^ (_seedField.Length << 14)
                    ^ ((floor != null ? (int)floor.Phase : 0) << 18)
                    ^ (_note != null ? _note.Length << 22 : 0);
            if (key == _key) return;
            _key = key;

            _text.Clear();
            _text.Append("<b>디버그</b>   [F1] 닫기").AppendLine();
            _text.Append("시드 ").Append(_run.Seed).Append("    모드 ").Append(_run.Mode).AppendLine();

            if (_editingSeed)
                _text.Append("새 시드 ▸ ").Append(_seedField.Length > 0 ? _seedField : "_")
                     .AppendLine("   [Enter] 적용 · [Esc] 취소");
            else
                _text.AppendLine("[T] 시드 입력   [R] 재시작   [L] 마지막 스핀 로그");

            if (_risk != null)
                _text.Append("위험 ").Append(_risk.Level.DisplayName())
                     .Append(" (점수 ").AppendFormat("{0:F1}", _risk.Score).Append(")   ")
                     .AppendLine(_risk.Reason);

            _text.Append("입력잠금 ").Append(locked ? "예(연출중)" : "아니오");
            if (floor != null)
                _text.Append("    단계 ").Append(floor.Phase);

            if (!string.IsNullOrEmpty(_note))
                _text.AppendLine().Append("<i>").Append(_note).Append("</i>");

            _bodyText.SetText(_text);
        }
    }
}
