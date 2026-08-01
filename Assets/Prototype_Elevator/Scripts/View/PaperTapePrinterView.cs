using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using Ascend.Prototype.Events;
using Ascend.Prototype.Risk;
using Ascend.Prototype.Run;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.View
{
    /// <summary>
    /// 사고 기록기의 **물리적 형태**. 엘리베이터 벽에 붙은 기계식 프린터가
    /// 층이 끝날 때마다 종이 테이프를 한 줄씩 밀어낸다.
    ///
    /// 왜 화면 오버레이로 충분하지 않은가: `MASTER_PRD.md` §10.1 이
    /// "단순 결과창 대신 엘리베이터 내부의 기계식 프린터, 종이 테이프 또는 펀치카드
    /// 장치"를 명시적으로 요구한다. 오버레이는 공간에 존재하지 않으므로
    /// 플레이어가 다가가 읽을 수 없고, §15.1 의 고정 캡처에서도 다른 캡처와
    /// 같은 조건으로 찍히지 않는다(17번 캡처만 해상도가 달랐던 원인이다).
    ///
    /// 오버레이 HUD 를 대체하지 않고 **함께** 존재한다 — `PENDING_DECISIONS.md` PD-05 가
    /// "어느 쪽도 제거하지 않는다"로 열려 있다. 이 컴포넌트를 꺼도 게임은 그대로 돈다.
    ///
    /// 데이터는 만들지 않는다. <see cref="AccidentRecorder"/> 가 만든
    /// <see cref="FloorRecord"/> 를 그대로 읽는다 — §10.3 "인게임 출력과 디버그가
    /// 같은 데이터를 쓴다"가 두 벌의 진실을 금지한다.
    /// </summary>
    public sealed class PaperTapePrinterView : MonoBehaviour
    {
        [Header("데이터 원본")]
        [SerializeField] private RunSessionBehaviour _run;
        [SerializeField] private AccidentRecorder _recorder;

        [Header("장치 부품")]
        [Tooltip("테이프가 나오는 슬롯. 테이프는 여기서 아래로 자란다.")]
        [SerializeField] private Transform _tapeOrigin;

        [Tooltip("테이프 자체(스케일이 늘어나는 판). 비어 있으면 런타임에 만든다.")]
        [SerializeField] private Transform _tape;

        [Tooltip("테이프 위의 글자. 비어 있으면 런타임에 만든다.")]
        [SerializeField] private TextMeshPro _tapeText;

        [Tooltip("찍히는 동안 흔들리는 인쇄 헤드. 없어도 된다.")]
        [SerializeField] private Transform _printHead;

        [Header("인쇄 템포")]
        [Tooltip("한 줄을 찍는 데 걸리는 시간(초). 기계라는 느낌은 여기서 나온다.")]
        [Min(0.02f)]
        [SerializeField] private float _secondsPerLine = 0.18f;

        [Tooltip("테이프 한 줄의 높이(미터).")]
        [Min(0.001f)]
        [SerializeField] private float _lineHeight = 0.035f;

        [Tooltip("테이프가 보관하는 최대 줄 수. 넘으면 위에서부터 잘린다.")]
        [Min(4)]
        [SerializeField] private int _maxLines = 24;

        [Tooltip("인쇄 헤드가 좌우로 움직이는 폭(미터).")]
        [SerializeField] private float _headTravel = 0.02f;

        // 줄 단위 큐. 한 프레임에 전부 찍지 않는 것이 이 장치의 전부다 —
        // 한 번에 뿌리면 오버레이와 다를 바가 없다.
        private readonly Queue<string> _pending = new Queue<string>();
        private readonly List<string> _printed = new List<string>();
        private readonly StringBuilder _composed = new StringBuilder(512);

        private GameEventBus _bus;
        private RunSession _session;
        private int _lastRecordCount;
        private float _lineTimer;
        private Vector3 _headHome;
        private bool _headHomeCaptured;

        /// <summary>지금 찍히기를 기다리는 줄 수. 캡처 리그가 "인쇄가 끝났는가"를 물을 때 쓴다.</summary>
        public int PendingLines => _pending.Count;

        /// <summary>인쇄가 진행 중인가. 고정 캡처는 이것이 false 가 된 뒤에 찍어야 재현된다.</summary>
        public bool IsPrinting => _pending.Count > 0;

        /// <summary>테이프에 실제로 찍혀 있는 줄. 테스트와 캡처 검증이 읽는다.</summary>
        public IReadOnlyList<string> PrintedLines => _printed;

        private void Awake()
        {
            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();
            if (_recorder == null) _recorder = FindAnyObjectByType<AccidentRecorder>();

            EnsureParts();
            if (_run != null) _run.RunStarted += OnRunStarted;
        }

        private void OnDestroy()
        {
            if (_run != null) _run.RunStarted -= OnRunStarted;
            Unsubscribe();
        }

        private void OnRunStarted(RunSession session)
        {
            Unsubscribe();
            _session = session;
            _bus = session != null ? session.Events : null;
            if (_bus != null) _bus.Published += OnGameEvent;

            _pending.Clear();
            _printed.Clear();
            _lastRecordCount = 0;
            _lineTimer = 0f;
            Feed($"상승 — 운행 기록  시드 {(session != null ? session.Seed : 0)}");
            Feed("────────────────────");
            Redraw();
        }

        private void Unsubscribe()
        {
            if (_bus != null) _bus.Published -= OnGameEvent;
            _bus = null;
        }

        /// <summary>
        /// 사건이 아니라 <see cref="FloorRecord"/> 가 기록의 원본이다.
        /// 사건은 "언제 찍을지"만 알려 준다 — 내용을 사건에서 다시 조립하면
        /// 기록기와 프린터가 서로 다른 숫자를 말하게 된다(§10.3).
        /// </summary>
        private void OnGameEvent(GameEvent e)
        {
            switch (e.Kind)
            {
                case GameEventKind.FloorResolved:
                    // 기록기는 LateUpdate 에서 한 프레임 늦게 만든다. 여기서 바로 읽지 않고
                    // 다음 Update 의 폴링이 새 레코드를 발견하게 둔다.
                    break;

                case GameEventKind.RunEnded:
                    Feed("────────────────────");
                    Feed($"운행 종료 — {e.Text}");
                    Feed($"도달 최고 층 {e.IntValue}층 · 소지금 {e.FloatValue:F0}");
                    break;
            }
        }

        private void Update()
        {
            PollNewRecords();
            AdvancePrinting();
        }

        private void PollNewRecords()
        {
            if (_recorder == null) return;
            IReadOnlyList<FloorRecord> records = _recorder.Records;
            if (records == null || records.Count <= _lastRecordCount) return;

            for (int i = _lastRecordCount; i < records.Count; i++)
                FeedRecord(records[i]);
            _lastRecordCount = records.Count;
        }

        /// <summary>
        /// 한 층의 기록을 테이프 줄로 옮긴다. `MASTER_PRD.md` §10 의 필수 기록 중
        /// 층 단위로 의미 있는 것만 고른다 — 테이프는 좁고, 전문은 콘솔과
        /// `FullReport()` 가 이미 갖고 있다.
        /// </summary>
        private void FeedRecord(FloorRecord record)
        {
            if (record == null) return;

            Feed($"{record.Floor:D2}층  {(record.Succeeded ? "상승" : "정지")}  {record.Band.DisplayName()}");
            Feed($"  전력 {record.FinalPower:F0} / 요구 {record.RequiredPower:F0}");
            Feed($"  계약 {Shorten(record.Contract, 14)}  스핀 {record.SpinsUsed}/{record.SpinsAllowed}");
            Feed($"  무게 {record.CarriedWeight:F0}/{record.WeightCapacity:F0}" +
                 (record.Overloaded ? " 과적" : string.Empty));
            Feed($"  위험 최고 {record.PeakRisk.DisplayName()}");
            if (record.ExtraSpinsTaken > 0)
                Feed($"  과수확 {record.ExtraSpinsTaken}회  판돈 {record.TotalAnte:F0}");
            if (!string.IsNullOrEmpty(record.Jettison))
                Feed($"  {Shorten(record.Jettison, 20)}");
            if (!record.Succeeded && !string.IsNullOrEmpty(record.FailureReason))
                Feed($"  원인 {Shorten(record.FailureReason, 18)}");
        }

        private void Feed(string line)
        {
            if (line == null) return;
            _pending.Enqueue(line);
        }

        private void AdvancePrinting()
        {
            if (_pending.Count == 0)
            {
                RestHead();
                return;
            }

            _lineTimer += Time.deltaTime;
            AnimateHead();

            while (_lineTimer >= _secondsPerLine && _pending.Count > 0)
            {
                _lineTimer -= _secondsPerLine;
                _printed.Add(_pending.Dequeue());
                if (_printed.Count > _maxLines) _printed.RemoveAt(0);
                Redraw();
            }
        }

        private void Redraw()
        {
            if (_tapeText == null) return;

            _composed.Clear();
            for (int i = 0; i < _printed.Count; i++)
            {
                if (i > 0) _composed.Append('\n');
                _composed.Append(_printed[i]);
            }
            _tapeText.SetText(_composed);

            if (_tape != null)
            {
                // 테이프는 줄 수만큼 아래로 자란다. 스케일만 바꾸므로 메시를 다시 만들지 않는다.
                float height = Mathf.Max(_lineHeight, _printed.Count * _lineHeight);
                Vector3 s = _tape.localScale;
                _tape.localScale = new Vector3(s.x, height, s.z);
                _tape.localPosition = new Vector3(0f, -height * 0.5f, 0f);
            }
        }

        private void AnimateHead()
        {
            if (_printHead == null) return;
            CaptureHeadHome();
            // 찍는 동안만 좌우로 움직인다. 멈춰 있을 때 흔들리면 고장난 기계로 보인다.
            float t = _secondsPerLine > 0f ? _lineTimer / _secondsPerLine : 0f;
            float offset = Mathf.PingPong(t * 2f, 1f) * _headTravel;
            _printHead.localPosition = _headHome + new Vector3(offset, 0f, 0f);
        }

        private void RestHead()
        {
            if (_printHead == null || !_headHomeCaptured) return;
            _printHead.localPosition = _headHome;
        }

        private void CaptureHeadHome()
        {
            if (_headHomeCaptured) return;
            _headHome = _printHead.localPosition;
            _headHomeCaptured = true;
        }

        private static string Shorten(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return "—";
            return value.Length <= max ? value : value.Substring(0, max - 1) + "…";
        }

        /// <summary>
        /// 씬에 부품이 없으면 런타임에 만든다. 그레이박스 단계에서 씬 편집 없이도
        /// 장치가 존재하게 하려는 것이지 최종 형상이 아니다 — 최종 모델링은 Pass 3 다.
        /// </summary>
        private void EnsureParts()
        {
            if (_tapeOrigin == null) _tapeOrigin = transform;

            if (_tape == null)
            {
                var tape = GameObject.CreatePrimitive(PrimitiveType.Quad);
                tape.name = "Tape";
                Destroy(tape.GetComponent<Collider>());
                tape.transform.SetParent(_tapeOrigin, false);
                tape.transform.localPosition = Vector3.zero;
                tape.transform.localScale = new Vector3(0.28f, _lineHeight, 1f);
                _tape = tape.transform;
            }

            if (_tapeText == null)
            {
                var go = new GameObject("TapeText");
                go.transform.SetParent(_tapeOrigin, false);
                go.transform.localPosition = new Vector3(0f, 0f, -0.004f);
                _tapeText = go.AddComponent<TextMeshPro>();
                _tapeText.fontSize = 0.9f;
                _tapeText.alignment = TextAlignmentOptions.TopLeft;
                _tapeText.enableWordWrapping = false;
                _tapeText.color = new Color(0.12f, 0.11f, 0.10f);
                var rt = _tapeText.rectTransform;
                rt.sizeDelta = new Vector2(0.28f, 1.0f);
                rt.pivot = new Vector2(0.5f, 1f);
            }
        }
    }
}
