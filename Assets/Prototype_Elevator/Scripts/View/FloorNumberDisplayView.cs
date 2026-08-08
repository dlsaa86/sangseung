using System.Text;
using TMPro;
using UnityEngine;
using Ascend.Prototype.Diagnostics;
using Ascend.Prototype.Run;

namespace Ascend.Prototype.View
{
    /// <summary>
    /// UP-DEVICE-08 그레이박스 — 엘리베이터 입구/탑 근처에서 **멀리서도 읽히는 큰 층수**.
    ///
    /// `docs/runtime/PLAN_GRAYBOX_UI.md` §1 "진짜 빠진 것"이 지목한 것: 지금은
    /// `InstrumentPanelView._floorLabel`(계기판 좌상단, 작은 글자)에만 층수가 있고,
    /// 그건 계기판을 들여다볼 때만 보인다. 이 컴포넌트는 그 정보를 계기판과 무관한
    /// 별도 위치에 큰 글자로 반복한다 — 배치 좌표는 씬 소유자가 정한다(이 스크립트는
    /// 어디에 서 있든 동작한다).
    ///
    /// ## 문구를 `InstrumentPanelView`와 다르게 짓지 않는다
    ///
    /// `InstrumentPanelView.LateUpdate`(`Scripts/View/InstrumentPanelView.cs:283`)가
    /// 층수를 표시하는 실제 줄은 이렇다 —
    /// <code>
    /// _text.Append(floor.Plan.Floor).Append("층 / ").Append(run.Floors.LastFloor)
    /// </code>
    /// 이 컴포넌트도 같은 세 조각(`floor.Plan.Floor` · 고정 문자열 `"층 / "` ·
    /// `run.Floors.LastFloor`)을 같은 순서로 이어 **바이트 단위로 같은 문자열**을 만든다.
    /// 두 표시가 같은 값을 다른 말로 적으면(예: 한쪽 "3층", 한쪽 "3/10") 화면이
    /// 스스로와 모순되는 것으로 읽히고, 이 저장소는 그 실패를 여러 번 겪었다
    /// (`InstrumentPanelView.ShowRunOver`의 "9차 판정" 주석 — 같은 화면에 같은 항목이
    /// 다른 값으로 두 번 나온 사고). 위험도 줄은 여기서 뺀다 — 그건 계기판이
    /// `RiskStateView`로 이미 전담하는 정보이고, 이 컴포넌트가 다시 재는 순간
    /// 두 번째 진실의 원천이 생긴다.
    /// </summary>
    public sealed class FloorNumberDisplayView : MonoBehaviour
    {
        [Tooltip("층수를 읽어올 런. 비어 있으면 씬에서 자동으로 찾는다 — 선택적 참조라 " +
                 "RequiredReference를 붙이지 않는다(Awake의 FindAnyObjectByType 대체 경로 참고).")]
        [SerializeField] private RunSessionBehaviour _run;

        [RequiredReference("배선이 없으면 이 오브젝트는 큰 층수 표시라는 목적을 하나도 못 한다 " +
                            "— 자동 대체 경로가 없는 필수 참조다(UP-DEVICE-08)")]
        [Tooltip("큰 층수 텍스트 한 덩어리. TMP_Text로 받아 3D TextMeshPro와 UI " +
                 "TextMeshProUGUI 어느 쪽에 붙여도 동작한다 — 배치를 정할 씬 소유자가 " +
                 "둘 중 무엇을 쓸지 나중에 고를 수 있게 자유도를 남긴다.")]
        [SerializeField] private TMP_Text _label;

        // 값이 실제로 바뀔 때만 문자열을 새로 짓는다. 이 저장소는 "프레임당 0B"를
        // 요구하고(`InstrumentPanelView`·`AscentColumnView`의 캐시 키 주석과 같은 이유),
        // 매 프레임 Append 체인을 돌리면 큰 글자 하나 그리자고 60fps마다 문자열 쓰레기를
        // 만든다. 층수는 한 층 안에서는 절대 바뀌지 않으므로 캐시 적중률은 사실상 100%다.
        //
        // 정수 하나로 세 상태(정상 진행 · 층 실패 · 층 확정)를 전부 표현한다.
        // 0 이상  = 그 값이 곧 `floor.Plan.Floor`(1~10, 항상 양수 — `FloorPlan.IsValid`가
        //           `Floor > 0`을 요구한다).
        // -1      = 런이 실패로 끝났다(`RunSession.IsFailed`).
        // -2      = 런이 실패 없이 끝났다(완주 또는 마지막 층 확정).
        // int.MinValue = 아직 아무것도 그리지 않았다(최초 프레임 강제 갱신용 보초값).
        private int _shownKey = int.MinValue;

        private readonly StringBuilder _text = new StringBuilder(24);

        private void Awake()
        {
            // `_run`은 선택적 참조다 — 비어 있으면 씬에서 하나 찾는다. `InstrumentPanelView.Awake`·
            // `AscentColumnView.Awake`와 같은 대체 경로를 그대로 따른다(대체 경로가 있는
            // 참조에는 RequiredReference를 붙이지 않는다는 이 저장소의 규칙 — 붙이면
            // "자동으로 채워지는 빈 칸"이 소음으로 보고된다).
            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();
            if (_run != null) _run.RunStarted += _ => InvalidateCache();
        }

        /// <summary>런이 다시 시작되면 캐시 키를 푼다. 안 그러면 새 런 1층에서도
        /// 직전 런의 종료 문구("층 확정" 등)가 그대로 남는다.</summary>
        private void InvalidateCache()
        {
            _shownKey = int.MinValue;
        }

        private void LateUpdate()
        {
            if (_label == null) return;

            RunSession run = _run != null ? _run.Session : null;
            // 배선이 아예 없으면(런을 못 찾음) 아무것도 안 건드린다 — 씬에 미리 적혀 있는
            // 플레이스홀더 문구를 그대로 둔다. `InstrumentPanelView.LateUpdate`·
            // `AscentColumnView.LateUpdate`가 `run == null`일 때 그냥 return하는 것과 같은
            // 판단이다: "값을 모른다"를 빈 화면이나 지어낸 값으로 덮지 않는다.
            if (run == null) return;

            FloorSession floor = run.Current;
            if (floor == null)
            {
                // `run.Current`가 null이라는 것은 런이 끝났다는 뜻이다(완주 또는 실패) —
                // `RunSession.CreateCurrentFloor`가 범위를 벗어나면 `_current = null`로
                // 못 박는다(`Run/RunSession.cs:506`). 이 순간 큰 층수 표시를 비워 두면
                // "방금까지 있던 정보가 갑자기 사라졌다"로 읽혀 고장처럼 보인다.
                // 그래서 `InstrumentPanelView.ShowRunOver`(`Scripts/View/InstrumentPanelView.cs:526`)와
                // 정확히 같은 두 문구로 종료 상태를 이어서 보여준다.
                ShowRunOver(run);
                return;
            }

            int key = floor.Plan.Floor;
            if (key == _shownKey) return;
            _shownKey = key;

            _text.Clear();
            _text.Append(floor.Plan.Floor).Append("층 / ").Append(run.Floors.LastFloor);
            _label.SetText(_text);
        }

        private void ShowRunOver(RunSession run)
        {
            int key = run.IsFailed ? -1 : -2;
            if (key == _shownKey) return;
            _shownKey = key;

            // `InstrumentPanelView.ShowRunOver`(`Scripts/View/InstrumentPanelView.cs:526`)와
            // 글자 그대로 같은 두 문구. 여기서 새 표현을 지어내지 않는다 — 그 이유는
            // 클래스 요약의 "문구를 다르게 짓지 않는다" 절 참고.
            _label.SetText(run.IsFailed ? "층 실패" : "층 확정");
        }
    }
}
