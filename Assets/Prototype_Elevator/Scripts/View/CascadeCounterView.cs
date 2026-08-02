using TMPro;
using UnityEngine;

namespace Ascend.Prototype.View
{
    /// <summary>
    /// 장치 본체에 붙은 **연쇄 계수기**. 마지막 스핀이 몇 단계까지 이어졌는지를
    /// 월드 공간 글자로 표시한다.
    ///
    /// ## 왜 HUD 가 아니라 월드인가
    ///
    /// 연쇄 단계 숫자가 화면에 없다는 지적이 **여섯 라운드 연속** 나왔고, 매번
    /// `GameHudView` 쪽을 고치려 했다. 그 층위에서는 **원리적으로 불가능하다.**
    ///
    /// - `15_cascade_deep` 은 전용 카메라의 `RenderTexture` 렌더다.
    ///   `GameHudView` 의 캔버스는 `ScreenSpaceOverlay` 라 그 렌더에 **절대 안 들어간다.**
    /// - `15`·`19` 는 둘 다 `run.Spin()` 을 직접 부른다. `SpinPresenter` 를 거치지 않으므로
    ///   `GameHudView.UpdateCascade` 의 `IsPresenting` 게이트가 **항상 거짓**이고,
    ///   연쇄 라벨은 갱신될 기회조차 없다.
    ///
    /// 그래서 표시 층위를 옮긴다. 월드 공간 글자는 전용 카메라 렌더에도, 화면 캡처에도
    /// 똑같이 들어간다. 그리고 이건 우회가 아니라 **원래 방향에 더 맞다** —
    /// `UP-DEVICE-10` 이 「장치가 평면 UI 가 아니라 물리적 조작부를 가진다」를 요구한다.
    /// 기계에 달린 계수기는 오버레이 숫자보다 그 요구에 가깝다.
    ///
    /// ## 왜 연출자가 아니라 세션을 **먼저** 읽는가
    ///
    /// <see cref="SpinPresenter"/> 의 `CurrentDepth` 는 **재생 중**에만 값이 있다.
    /// 그건 「지금 연출이 몇 번째를 보여 주는가」이지 「이 스핀이 몇 단계였는가」가 아니다.
    /// 계수기는 후자를 말해야 한다 — 연출이 끝난 뒤에도, 연출을 아예 거치지 않아도.
    ///
    /// 그래서 <see cref="Run.FloorSession.History"/> 의 마지막 항목을 기준으로 삼는다.
    /// `run.Spin()` 이 직접 채우는 값이라 두 캡처 경로 모두에서 살아 있다.
    ///
    /// **다만 연출이 도는 동안에는 기록이 아직 없을 수 있다.** `22_presenting_screen` 이
    /// 정확히 그 순간이다 — 레버를 당겨 `SpinPresenter` 가 재생 중이고 결과는 아직
    /// 세션 기록에 들어가지 않았다. 그때 계수기가 0 을 말하면 「연출 중인데 0단계」라는
    /// 거짓을 적는 것이다. 그래서 재생 중에는 **둘 중 큰 값**을 쓴다 —
    /// 기록이 있으면 기록이, 없으면 연출자가 답한다. 어느 쪽도 값을 지어내지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CascadeCounterView : MonoBehaviour
    {
        [SerializeField] private Run.RunSessionBehaviour _run;
        [SerializeField] private TextMeshPro _label;

        [Tooltip("연출 중 단계를 읽을 연출자. 비어 있으면 Awake 에서 찾는다.")]
        [SerializeField] private SpinPresenter _presenter;

        [Tooltip("연쇄가 아직 없을 때 보일 글자. 빈 문자열이면 계수기가 통째로 사라져 " +
                 "「장치가 꺼졌다」로 읽힌다 — 0 을 적어 두는 편이 상태를 말한다.")]
        [SerializeField] private string _idleText = "연쇄 0";

        private int _shown = int.MinValue;

        private void Awake()
        {
            if (_run == null) _run = FindAnyObjectByType<Run.RunSessionBehaviour>();
            if (_label == null) _label = GetComponentInChildren<TextMeshPro>(true);
            if (_presenter == null) _presenter = FindAnyObjectByType<SpinPresenter>();
        }

        private void LateUpdate()
        {
            if (_label == null) return;

            int depth = CurrentDepth();
            if (depth == _shown) return;   // 값이 바뀔 때만 쓴다 — 매 프레임 쓰면 할당이 된다
            _shown = depth;
            _label.text = depth > 0 ? $"연쇄 {depth}" : _idleText;
        }

        /// <summary>
        /// 이번 층에서 **마지막으로 해결된** 스핀의 연쇄 단계. 없으면 0.
        /// 층이 바뀌면 기록도 새로 시작하므로 자연히 0 으로 돌아간다.
        ///
        /// 연출이 도는 동안에는 연출자의 현재 단계와 **큰 쪽**을 쓴다 — 위 클래스 주석 참조.
        /// 연출이 멈춰 있으면 연출자는 아예 보지 않는다(그쪽은 0 으로 리셋돼 있고,
        /// 그 0 을 기록보다 우선하면 스핀이 끝난 뒤 계수기가 조용히 0 으로 떨어진다).
        /// </summary>
        private int CurrentDepth()
        {
            // `RunSession.CurrentFloor` 는 **층 번호(int)** 다. 층 세션은 `Current` 쪽이다 —
            // 이름이 비슷해 한 번 틀렸고, 컴파일러가 잡아 줬다.
            int recorded = 0;
            Run.FloorSession floor = _run != null && _run.Session != null ? _run.Session.Current : null;
            if (floor != null)
            {
                var history = floor.History;
                if (history != null && history.Count > 0)
                    recorded = history[history.Count - 1].ChainDepth;
            }

            if (_presenter != null && _presenter.IsPresenting)
                return Mathf.Max(recorded, _presenter.CurrentDepth);
            return recorded;
        }
    }
}
