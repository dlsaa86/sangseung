using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Ascend.Prototype
{
    /// <summary>
    /// Drives the 3D graybox elevator car from RunController state.
    ///
    /// Read-only with respect to gameplay: this class never mutates run state. Its whole job is
    /// to make the loop legible in the world rather than only in the text HUD — which button
    /// belongs to which tube, who is aboard, how heavy the car is, whether it is overloaded.
    /// </summary>
    public class ElevatorGrayboxView : MonoBehaviour
    {
        [Header("Run")]
        [SerializeField] private RunController _run;

        [Header("Car")]
        [SerializeField] private Transform _carRoot;

        [Header("Stop buttons (index must match tube index)")]
        [SerializeField] private Transform[] _buttons = new Transform[3];
        [SerializeField] private Renderer[] _buttonRenderers = new Renderer[3];

        [Header("Per-tube harvest readout")]
        [SerializeField] private TextMeshPro[] _tubeLabels = new TextMeshPro[3];

        [Header("Door")]
        [SerializeField] private Transform _doorLeft;
        [SerializeField] private Transform _doorRight;
        [SerializeField] private float _doorSlide = 1.15f;

        [Header("Anchors")]
        [SerializeField] private Transform _passengerAnchor;
        [SerializeField] private Transform _candidateAnchor;

        [Header("Boarding")]
        /// <summary>
        /// 있으면 새로 탄 승객이 문 밖에서 걸어 들어온다. 비워 두면 예전처럼 즉시 배치된다 —
        /// 연출은 선택이고, 게임 상태는 이 컴포넌트 유무와 무관하게 똑같이 흐른다.
        /// </summary>
        [SerializeField] private Ascend.Prototype.View.PassengerEntryAnimator _entryAnimator;

        /// <summary>
        /// 있으면 승객이 이 슬롯에 한 명씩 배정된다 — 기계 맞은편 백월, 서로 겹치지 않음.
        /// 비워 두면 예전처럼 앵커 로컬 X 로 줄지어 선다.
        /// </summary>
        [SerializeField] private Ascend.Prototype.View.PassengerStationSet _stations;

        [Header("Instrument panel")]
        [SerializeField] private TextMeshPro _floorLabel;
        [SerializeField] private TextMeshPro _powerLabel;
        [SerializeField] private TextMeshPro _weightLabel;
        [SerializeField] private Transform _powerBarPivot;
        [SerializeField] private float _powerBarWidth = 2.6f;
        [SerializeField] private Renderer _overloadLight;

        // Grade colours mirror the 2D HUD so a ball reads the same in both places.
        private static readonly Color ColCommon    = new Color(0.72f, 0.72f, 0.72f);
        private static readonly Color ColAdvanced  = new Color(0.37f, 0.78f, 0.96f);
        private static readonly Color ColRare      = new Color(0.71f, 0.51f, 0.94f);
        private static readonly Color ColLegendary = new Color(0.95f, 0.63f, 0.24f);
        private static readonly Color ColIdle      = new Color(0.55f, 0.56f, 0.60f);
        private static readonly Color ColBraking   = new Color(0.95f, 0.82f, 0.29f);
        private static readonly Color ColGood      = new Color(0.44f, 0.83f, 0.44f);
        private static readonly Color ColBad       = new Color(0.94f, 0.36f, 0.36f);

        private const float ButtonPressDepth = 0.08f;

        private readonly List<GameObject> _passengerViews = new List<GameObject>();
        private readonly List<GameObject> _candidateViews = new List<GameObject>();
        private readonly Dictionary<string, Vector3> _previousSlots = new Dictionary<string, Vector3>();
        private readonly List<Material> _ownedMaterials = new List<Material>();

        private Material[] _buttonMaterials;
        private Vector3[] _buttonHome;
        private Material _overloadMaterial;
        private Vector3 _carHomePosition;
        private int _lastBoardedCount = -1;
        private int _lastCandidateHash;
        private float _doorOpenAmount;

        private void Awake()
        {
            if (_carRoot != null) _carHomePosition = _carRoot.localPosition;

            // The press animation is relative to wherever the builder placed each button —
            // writing an absolute Y here would tear the buttons off the console.
            _buttonHome = new Vector3[_buttons.Length];
            for (int i = 0; i < _buttons.Length; i++)
                if (_buttons[i] != null) _buttonHome[i] = _buttons[i].localPosition;

            // Renderer.material instantiates a copy; keep the handles so OnDestroy can free them.
            _buttonMaterials = new Material[_buttonRenderers.Length];
            for (int i = 0; i < _buttonRenderers.Length; i++)
            {
                if (_buttonRenderers[i] == null) continue;
                _buttonMaterials[i] = _buttonRenderers[i].material;
                _ownedMaterials.Add(_buttonMaterials[i]);
            }
            if (_overloadLight != null)
            {
                _overloadMaterial = _overloadLight.material;
                _ownedMaterials.Add(_overloadMaterial);
            }
        }

        private void OnDestroy()
        {
            foreach (Material m in _ownedMaterials)
                if (m != null) Destroy(m);
            _ownedMaterials.Clear();
        }

        private void LateUpdate()
        {
            if (_run == null) return;

            UpdateButtonsAndTubes();
            UpdateDoor();
            UpdatePassengers();
            UpdateCandidates();
            UpdateInstruments();
            UpdateCarMotion();
        }

        // ── Buttons ↔ tubes ──

        private void UpdateButtonsAndTubes()
        {
            RouletteController roulette = _run.Roulette;
            IReadOnlyList<TubeController> tubes = roulette != null ? roulette.Tubes : null;
            float tolerance = _run.Config != null ? _run.Config.perfectStopTolerance : 0f;
            bool generating = _run.CurrentState == RunState.GenerationTurn;

            for (int i = 0; i < _buttons.Length; i++)
            {
                TubeController tube = (tubes != null && i < tubes.Count) ? tubes[i] : null;

                Color target = ColIdle;
                bool pressed = false;
                string label = "-";

                if (tube != null)
                {
                    if (tube.IsStopped)
                    {
                        pressed = true;
                        BallDefinition ball = tube.StoppedBall;
                        // Button takes the harvested ball's grade colour. It reports WHAT you
                        // caught, not how cleanly you caught it — there is no timing verdict.
                        target = ball != null ? GradeColor(ball.grade) : ColGood;
                        label = ball != null ? $"{ball.id}\n{GradeName(ball.grade)}" : "정지";
                    }
                    else if (tube.IsBraking || tube.IsSnapping)
                    {
                        pressed = true;
                        target = ColBraking;
                        label = "제동";
                    }
                    else
                    {
                        target = generating ? Color.Lerp(ColIdle, Color.white, 0.25f) : ColIdle;
                        label = generating ? "낙하중" : "-";
                    }
                }

                if (_buttons[i] != null && _buttonHome != null && i < _buttonHome.Length)
                {
                    Vector3 p = _buttons[i].localPosition;
                    float restY = _buttonHome[i].y;
                    p.y = Mathf.Lerp(p.y, pressed ? restY - ButtonPressDepth : restY, Time.deltaTime * 14f);
                    _buttons[i].localPosition = p;
                }

                if (_buttonMaterials != null && i < _buttonMaterials.Length && _buttonMaterials[i] != null)
                    SetColor(_buttonMaterials[i], Color.Lerp(_buttonMaterials[i].GetColor("_BaseColor"), target, Time.deltaTime * 12f));

                if (i < _tubeLabels.Length && _tubeLabels[i] != null)
                {
                    _tubeLabels[i].text = label;
                    _tubeLabels[i].color = target;
                }
            }
        }

        // ── Door ──

        private void UpdateDoor()
        {
            RunState s = _run.CurrentState;
            bool shouldOpen = _run.Outcome == RunOutcome.InProgress
                              && (s == RunState.FloorArrival || s == RunState.PassengerSelection);

            _doorOpenAmount = Mathf.MoveTowards(_doorOpenAmount, shouldOpen ? 1f : 0f, Time.deltaTime * 2.2f);

            if (_doorLeft != null)
            {
                Vector3 p = _doorLeft.localPosition;
                p.x = -_doorSlide * _doorOpenAmount;
                _doorLeft.localPosition = p;
            }
            if (_doorRight != null)
            {
                Vector3 p = _doorRight.localPosition;
                p.x = _doorSlide * _doorOpenAmount;
                _doorRight.localPosition = p;
            }
        }

        // ── Passengers inside the car ──

        private void UpdatePassengers()
        {
            PassengerManager pm = _run.Passengers;
            if (pm == null || _passengerAnchor == null) return;

            IReadOnlyList<PassengerDefinition> boarded = pm.Boarded;
            int count = boarded != null ? boarded.Count : 0;
            if (count == _lastBoardedCount) return;

            // 승객이 '늘어난' 경우에만 탑승 연출을 건다. 층이 바뀌어 명단이 초기화될 때
            // (count 가 줄거나 첫 빌드일 때) 걸으면 없던 사람이 걸어 들어온다.
            bool grew = _lastBoardedCount >= 0 && count == _lastBoardedCount + 1;
            _lastBoardedCount = count;

            // 슬롯 간격이 바뀌므로 기존 승객도 자리를 옮긴다. 팝 대신 미끄러지게 하려면
            // 옮기기 전 위치를 기억해 둬야 한다.
            _previousSlots.Clear();
            foreach (GameObject go in _passengerViews)
                if (go != null) _previousSlots[go.name] = go.transform.localPosition;

            ClearViews(_passengerViews);
            if (boarded == null) return;

            for (int i = 0; i < count; i++)
            {
                GameObject figure = BuildFigure(boarded[i], _passengerAnchor, i, count, 0.8f, true);
                _passengerViews.Add(figure);

                // 슬롯이 있으면 줄 계산을 버리고 자리에 꽂는다. 한 슬롯 = 한 명 이므로
                // 인원이 바뀜도 기존 승객의 자리가 안 밀리고, 겹침이 구조적으로 불가능하다.
                Transform slot = _stations != null ? _stations.Slot(i) : null;
                if (slot != null)
                {
                    figure.transform.SetParent(slot, false);
                    figure.transform.localPosition = Vector3.zero;
                    figure.transform.localRotation = Quaternion.identity;
                }

                if (_entryAnimator == null) continue;

                bool isNewcomer = grew && i == count - 1;
                if (isNewcomer && _stations != null &&
                    _stations.EntryOutside != null && _stations.EntryInside != null)
                {
                    if (_stations.EntryTurn != null)
                        _entryAnimator.PlayEntry(figure,
                                                 _stations.EntryOutside.position,
                                                 _stations.EntryInside.position,
                                                 _stations.EntryTurn.position);
                    else
                        _entryAnimator.PlayEntry(figure,
                                                 _stations.EntryOutside.position,
                                                 _stations.EntryInside.position);
                }
                else if (isNewcomer && _candidateAnchor != null)
                {
                    _entryAnimator.PlayEntry(figure, _candidateAnchor);
                }
                else if (slot == null && _previousSlots.TryGetValue(figure.name, out Vector3 previous))
                {
                    _entryAnimator.PlaySlide(figure, previous);
                }
            }
        }

        // ── Candidates waiting outside the door ──

        private void UpdateCandidates()
        {
            PassengerManager pm = _run.Passengers;
            if (pm == null || _candidateAnchor == null) return;

            IReadOnlyList<PassengerDefinition> candidates = pm.Candidates;
            int hash = 17;
            if (candidates != null)
                foreach (PassengerDefinition c in candidates)
                    hash = hash * 31 + (c != null ? c.id.GetHashCode() : 0);
            if (hash == _lastCandidateHash) return;
            _lastCandidateHash = hash;

            ClearViews(_candidateViews);
            if (candidates == null) return;

            for (int i = 0; i < candidates.Count; i++)
                _candidateViews.Add(BuildFigure(candidates[i], _candidateAnchor, i, candidates.Count, 1.0f, false));
        }

        /// <summary>
        /// Builds one capsule stand-in with a floating name label.
        /// Graybox on purpose — the shape carries no meaning, the colour and label do.
        /// </summary>
        private GameObject BuildFigure(PassengerDefinition def, Transform anchor,
                                       int index, int total, float spacing, bool inside)
        {
            var root = new GameObject(def != null ? def.id : "Passenger");
            root.transform.SetParent(anchor, false);

            float offset = (index - (total - 1) * 0.5f) * spacing;
            root.transform.localPosition = new Vector3(offset, 0f, 0f);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.5f, 0.55f, 0.5f);
            body.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            Collider col = body.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = body.GetComponent<Renderer>();
            if (rend != null)
            {
                Material mat = rend.material;
                Color c = def != null ? def.debugColor : Color.gray;
                if (c.a <= 0.01f) c = Color.gray;
                SetColor(mat, c);
                _ownedMaterials.Add(mat);
            }

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(root.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.45f, 0f);
            var tmp = labelGo.AddComponent<TextMeshPro>();
            tmp.text = def != null
                ? (inside ? Label(def) : $"{Label(def)}\n무게 {def.weight:F0}")
                : "?";
            tmp.fontSize = 1.6f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            var rt = tmp.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(3.2f, 1.2f);

            return root;
        }

        private void ClearViews(List<GameObject> views)
        {
            foreach (GameObject go in views)
                if (go != null) Destroy(go);
            views.Clear();
        }

        // ── Instrument panel ──

        private void UpdateInstruments()
        {
            ElevatorState st = _run.State;
            FloorController floor = _run.Floor;
            PrototypeConfig cfg = _run.Config;
            if (st == null) return;

            int target = cfg != null ? cfg.targetFloor : 0;
            float required = floor != null ? floor.RequiredPower : 0f;

            if (_floorLabel != null)
            {
                if (_run.Outcome == RunOutcome.Success)      { _floorLabel.text = "성공"; _floorLabel.color = ColGood; }
                else if (_run.Outcome == RunOutcome.Failure) { _floorLabel.text = "실패"; _floorLabel.color = ColBad; }
                else
                {
                    _floorLabel.text = $"{(floor != null ? floor.CurrentFloor : 0)} / {target} 층";
                    _floorLabel.color = Color.white;
                }
            }

            float ratio = required > 0f ? Mathf.Clamp01(st.Power / required) : 0f;
            bool powerOk = required > 0f && st.Power >= required;

            if (_powerLabel != null)
            {
                _powerLabel.text = $"전력 {st.Power:F0} / {required:F0}";
                _powerLabel.color = powerOk ? ColGood : Color.white;
            }
            if (_powerBarPivot != null)
            {
                Vector3 s = _powerBarPivot.localScale;
                s.x = Mathf.Lerp(s.x, Mathf.Max(0.0001f, _powerBarWidth * ratio), Time.deltaTime * 8f);
                _powerBarPivot.localScale = s;
            }

            if (_weightLabel != null)
            {
                bool over = st.IsOverloaded;
                _weightLabel.text = over
                    ? $"무게 {st.Weight:F0}/{st.AllowedWeight:F0}\n과적 사고 {st.AccidentChance * 100f:F0}%"
                    : $"무게 {st.Weight:F0}/{st.AllowedWeight:F0}\n정상";
                _weightLabel.color = over ? ColBad : ColGood;
            }

            if (_overloadMaterial != null)
            {
                bool over = st.IsOverloaded;
                // Pulse only while overloaded so the light reads as an active alarm, not decor.
                float pulse = over ? (0.5f + 0.5f * Mathf.Sin(Time.time * 6f)) : 0f;
                Color c = over ? Color.Lerp(new Color(0.35f, 0.05f, 0.05f), ColBad, pulse)
                               : new Color(0.12f, 0.20f, 0.12f);
                SetColor(_overloadMaterial, c);
                if (_overloadMaterial.HasProperty("_EmissionColor"))
                    _overloadMaterial.SetColor("_EmissionColor", c * (over ? 2.2f : 0.1f));
            }
        }

        // ── Car motion ──

        private void UpdateCarMotion()
        {
            if (_carRoot == null) return;

            // A short vertical rumble is the only ascent feedback in the graybox; without it the
            // floor number changes with nothing in the world acknowledging the move.
            bool ascending = _run.CurrentState == RunState.Ascending && _run.Outcome == RunOutcome.InProgress;
            float shake = ascending ? Mathf.Sin(Time.time * 34f) * 0.045f : 0f;
            Vector3 p = _carHomePosition;
            p.y += shake;
            _carRoot.localPosition = Vector3.Lerp(_carRoot.localPosition, p, Time.deltaTime * 18f);
        }

        // ── Helpers ──

        private static void SetColor(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            else if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        private static string Label(PassengerDefinition p)
            => p == null ? "?" : (string.IsNullOrEmpty(p.displayName) ? p.id : p.displayName);

        private static Color GradeColor(BallGrade grade)
        {
            switch (grade)
            {
                case BallGrade.Common:    return ColCommon;
                case BallGrade.Advanced:  return ColAdvanced;
                case BallGrade.Rare:      return ColRare;
                case BallGrade.Legendary: return ColLegendary;
                default:                  return ColIdle;
            }
        }

        private static string GradeName(BallGrade grade)
        {
            switch (grade)
            {
                case BallGrade.Common:    return "일반";
                case BallGrade.Advanced:  return "고급";
                case BallGrade.Rare:      return "희귀";
                case BallGrade.Legendary: return "전설";
                default:                  return "?";
            }
        }
    }
}
