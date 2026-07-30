using TMPro;
using UnityEngine;
using Ascend.Prototype.Run;

namespace Ascend.Prototype.View
{
    /// <summary>
    /// 출입구 위 층수 표시등. 낡은 화물 엘리베이터의 기계식 표시기를 흉내낸다.
    ///
    /// `AUTONOMOUS_PROTOTYPE_GOAL.md` §4가 "출입구 위 현재 층수 표시"를 요구한다.
    /// 층이 열 개가 되는 순간 "지금 몇 층인가"는 HUD 구석 숫자가 아니라 공간에 있어야 한다 —
    /// 플레이어가 등을 돌리고 통관을 보고 있어도 고개만 들면 읽혀야 하기 때문이다.
    ///
    /// 현대적 홀로그램이 아니라 **점등판**이다(§4 금지 항목). 발광은 숫자에만 있고
    /// 판 자체는 어둡고 각진 철판이다.
    /// </summary>
    public sealed class FloorIndicatorView : MonoBehaviour
    {
        [SerializeField] private RunSessionBehaviour _run;
        [SerializeField] private TMP_Text _display;

        [Tooltip("표시등 하우징. 비어 있으면 Awake 에서 만든다.")]
        [SerializeField] private Transform _housing;

        private int _shownFloor = int.MinValue;
        private bool _shownComplete;
        private bool _shownFailed;

        private void Awake()
        {
            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();
            EnsureDisplay();
            if (_run != null) _run.RunStarted += OnRunStarted;
        }

        private void OnDestroy()
        {
            if (_run != null) _run.RunStarted -= OnRunStarted;
        }

        private void OnRunStarted(RunSession session)
        {
            _shownFloor = int.MinValue;
            Refresh();
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            RunSession run = _run != null ? _run.Session : null;
            if (run == null || _display == null) return;

            int floor = run.CurrentFloor;
            if (floor == _shownFloor && run.IsComplete == _shownComplete && run.IsFailed == _shownFailed)
                return;

            // 문자열은 층이 바뀔 때만 짓는다. 매 프레임 지으면 초당 60개의 쓰레기다.
            _shownFloor = floor;
            _shownComplete = run.IsComplete;
            _shownFailed = run.IsFailed;

            if (run.IsFailed)
            {
                _display.text = "<size=70%>정지</size>";
                _display.color = new Color(0.78f, 0.30f, 0.22f);
                return;
            }

            if (run.IsComplete)
            {
                _display.text = "<size=70%>최상층</size>";
                _display.color = new Color(0.62f, 0.78f, 0.58f);
                return;
            }

            int last = run.Floors != null ? run.Floors.LastFloor : floor;
            _display.text = last > 1 ? $"{floor}<size=45%> / {last}</size>" : floor.ToString();
            _display.color = new Color(0.86f, 0.62f, 0.28f);
        }

        private void EnsureDisplay()
        {
            if (_display != null) return;

            if (_housing == null)
            {
                GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plate.name = "IndicatorHousing";
                plate.transform.SetParent(transform, false);
                plate.transform.localPosition = Vector3.zero;
                plate.transform.localScale = new Vector3(0.62f, 0.26f, 0.06f);

                Collider collider = plate.GetComponent<Collider>();
                if (collider != null)
                {
                    // 조준 대상이 아니다. 콜라이더가 있으면 크로스헤어가 여기에 걸린다.
                    if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
                }

                var renderer = plate.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    var material = new Material(shader) { name = "FloorIndicatorPlate" };
                    material.color = new Color(0.13f, 0.13f, 0.12f);
                    if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.05f);
                    renderer.sharedMaterial = material;
                }
                _housing = plate.transform;
            }

            var holder = new GameObject("Readout");
            holder.transform.SetParent(transform, false);
            holder.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            holder.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var text = holder.AddComponent<TextMeshPro>();
            text.text = "1";
            text.fontSize = 2.4f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.86f, 0.62f, 0.28f);
            var rect = holder.GetComponent<RectTransform>();
            if (rect != null) rect.sizeDelta = new Vector2(0.6f, 0.26f);

            _display = text;
        }
    }
}
