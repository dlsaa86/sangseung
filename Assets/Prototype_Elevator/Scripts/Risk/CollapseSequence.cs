using UnityEngine;
using Ascend.Prototype.Diagnostics;
using Ascend.Prototype.Events;
using Ascend.Prototype.Run;

namespace Ascend.Prototype.Risk
{
    /// <summary>
    /// Collapse 단계의 **연출**. 판정은 <see cref="RiskEvaluator"/> 와
    /// <see cref="FloorResult"/> 가 이미 끝냈다 — 여기서는 그 사실을 공간이 말하게 한다.
    ///
    /// 왜 따로 있는가: 지금까지 Collapse 는 `RiskLevel` 값으로만 존재했고,
    /// `RiskStateView` 의 프로파일 보간은 Critical 과 Collapse 를 **연속적으로** 섞는다.
    /// 그래서 고정 캡처 10(Critical)과 16(Collapse)이 서로 구분되지 않았다
    /// (수정 백로그 `UP-FIX-05`). 사고는 더 어두운 Critical 이 아니라 **사건**이다 —
    /// 연속 보간이 아니라 시간 축을 가진 시퀀스여야 구분된다.
    ///
    /// `MASTER_PRD.md` §9 의 Collapse("제어 상실과 결과 기록")를 네 박자로 편다.
    ///   1) 암전       — 실내등이 즉시 죽는다. 보간하지 않는다.
    ///   2) 파열음     — 금속이 끊어지는 소리. 사건 버스로 나가 오디오가 받는다.
    ///   3) 급강하     — 매달린 것과 카메라가 자유낙하 뒤 바닥을 친다.
    ///   4) 불규칙 재점등 — 규칙적인 깜빡임이 아니라 **불규칙**해야 고장으로 읽힌다.
    ///
    /// 조명을 직접 건드리지 않고 <see cref="RiskStateView.CabinLightMultiplier"/> 를 쓴다.
    /// `RiskStateView` 가 매 LateUpdate 마다 밝기를 절대값으로 다시 쓰기 때문에,
    /// 여기서 intensity 를 대입해 봐야 같은 프레임에 덮어써진다(그 컴포넌트의 주석 참고).
    /// </summary>
    public sealed class CollapseSequence : MonoBehaviour
    {
        [SerializeField] private RunSessionBehaviour _run;
        [SerializeField] private RiskStateView _risk;

        [Header("흔들 대상")]
        [Tooltip("급강하 때 아래로 떨어졌다가 튀는 것들. 매달린 등·탱크·승객 루트를 넣는다.")]
        // 비면 **급강하가 통째로 일어나지 않는다** — `UP-RISK-06` 이 이름으로 지정한
        // 네 단계(암전 → 파열음 → 급강하 → 재점등) 중 하나가 조용히 빠진다.
        // 위의 `_run`·`_risk` 와 달리 폴백 검색 경로가 없다. 길이 0 도 비어 있는 것으로 친다.
        [RequiredReference("급강하 단계가 통째로 일어나지 않는다 (UP-RISK-06 4단계 중 하나)")]
        [SerializeField] private Transform[] _dropTargets;

        [Tooltip("카메라 **리그**(카메라 자체가 아니다). RiskStateView 가 카메라 로컬 위치를 " +
                 "매 프레임 덮어쓰므로, 그 위쪽 트랜스폼을 흔들어야 두 연출이 싸우지 않는다.")]
        [SerializeField] private Transform _cameraRig;

        [Header("타이밍 — 승인 대기 항목이므로 잠그지 않는다")]
        [Tooltip("암전이 유지되는 시간. 이 구간에는 아무것도 읽히지 않아야 사고로 느껴진다.")]
        [SerializeField, Min(0f)] private float _blackoutSeconds = 0.55f;

        [Tooltip("급강하 지속 시간.")]
        [SerializeField, Min(0.05f)] private float _dropSeconds = 0.9f;

        [Tooltip("자유낙하 최대 낙차(미터).")]
        [SerializeField, Min(0f)] private float _dropDistance = 0.42f;

        [Tooltip("불규칙 재점등이 이어지는 시간. 끝나면 평소 조명으로 돌아간다.")]
        [SerializeField, Min(0f)] private float _relightSeconds = 1.8f;

        [Tooltip("재점등 깜빡임의 불규칙 시드. 같은 시드면 같은 깜빡임이 나온다 — 캡처 재현을 위해 고정한다.")]
        [SerializeField] private int _flickerSeed = 20260801;

        private GameEventBus _bus;
        private Vector3[] _dropHome;
        private Vector3 _rigHome;
        private bool _running;
        private float _t;
        private System.Random _flicker;
        private float _nextFlickerAt;
        private float _flickerLevel;

        /// <summary>지금 사고 연출이 돌고 있는가. 고정 캡처가 "사고 직후"를 잡을 때 본다.</summary>
        public bool IsPlaying => _running;

        /// <summary>연출 시작으로부터 흐른 시간. 캡처 리그가 특정 박자를 노릴 때 쓴다.</summary>
        public float Elapsed => _t;

        /// <summary>전체 길이. 이 시간이 지나면 공간이 평소 상태로 돌아간다.</summary>
        public float TotalSeconds => _blackoutSeconds + _dropSeconds + _relightSeconds;

        private void Awake()
        {
            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();
            if (_risk == null) _risk = FindAnyObjectByType<RiskStateView>();
            if (_run != null) _run.RunStarted += OnRunStarted;
            CaptureHomes();
        }

        private void OnDestroy()
        {
            if (_run != null) _run.RunStarted -= OnRunStarted;
            Unsubscribe();
        }

        private void OnRunStarted(RunSession session)
        {
            Unsubscribe();
            _bus = session != null ? session.Events : null;
            if (_bus != null) _bus.Published += OnGameEvent;
            Stop();
        }

        private void Unsubscribe()
        {
            if (_bus != null) _bus.Published -= OnGameEvent;
            _bus = null;
        }

        private void OnGameEvent(GameEvent e)
        {
            if (e.Kind == GameEventKind.CollapseBegan) Begin();
        }

        /// <summary>연출을 시작한다. 캡처 리그와 디버그 패널이 직접 부를 수 있게 public 이다.</summary>
        public void Begin()
        {
            CaptureHomes();
            _running = true;
            _t = 0f;
            _flicker = new System.Random(_flickerSeed);
            _nextFlickerAt = 0f;
            _flickerLevel = 0f;
        }

        /// <summary>연출을 즉시 끝내고 공간을 원래대로 돌린다.</summary>
        public void Stop()
        {
            _running = false;
            _t = 0f;
            RestoreHomes();
            if (_risk != null) _risk.CabinLightMultiplier = 1f;
        }

        private void LateUpdate()
        {
            if (!_running) return;

            _t += Time.deltaTime;
            float blackoutEnd = _blackoutSeconds;
            float dropEnd = blackoutEnd + _dropSeconds;
            float relightEnd = dropEnd + _relightSeconds;

            ApplyLight(blackoutEnd, dropEnd, relightEnd);
            ApplyDrop(blackoutEnd, dropEnd);

            if (_t >= relightEnd) Stop();
        }

        private void ApplyLight(float blackoutEnd, float dropEnd, float relightEnd)
        {
            if (_risk == null) return;

            if (_t < blackoutEnd)
            {
                // 완전 암전. 보간하지 않는 것이 핵심이다 — 서서히 어두워지면
                // "더 위험한 Critical"로 읽히고 사고로 읽히지 않는다.
                _risk.CabinLightMultiplier = 0f;
                return;
            }

            if (_t < dropEnd)
            {
                // 급강하 중에는 비상등 수준으로만 살아난다.
                _risk.CabinLightMultiplier = 0.18f;
                return;
            }

            // 불규칙 재점등. 간격도 밝기도 난수라서 규칙적인 사인과 구분된다.
            if (_t >= _nextFlickerAt)
            {
                _nextFlickerAt = _t + 0.04f + (float)_flicker.NextDouble() * 0.22f;
                _flickerLevel = (float)_flicker.NextDouble();
                // 뒤로 갈수록 켜져 있는 쪽으로 기운다 — 복구되는 중이라는 뜻이다.
                float recovery = Mathf.InverseLerp(dropEnd, relightEnd, _t);
                _flickerLevel = Mathf.Lerp(_flickerLevel, 1f, recovery * recovery);
            }
            _risk.CabinLightMultiplier = _flickerLevel;
        }

        private void ApplyDrop(float blackoutEnd, float dropEnd)
        {
            if (_t < blackoutEnd) return;

            float offset;
            if (_t < dropEnd)
            {
                float k = Mathf.InverseLerp(blackoutEnd, dropEnd, _t);
                // 앞은 자유낙하(가속), 뒤는 바닥을 치고 감쇠 진동. 둘을 이어 붙인다.
                if (k < 0.55f)
                {
                    float f = k / 0.55f;
                    offset = -_dropDistance * f * f;
                }
                else
                {
                    float f = (k - 0.55f) / 0.45f;
                    float bounce = Mathf.Exp(-6f * f) * Mathf.Cos(f * 26f);
                    offset = -_dropDistance * bounce * 0.5f;
                }
            }
            else
            {
                offset = 0f;
            }

            if (_dropTargets != null && _dropHome != null)
            {
                for (int i = 0; i < _dropTargets.Length && i < _dropHome.Length; i++)
                {
                    Transform t = _dropTargets[i];
                    if (t == null) continue;
                    // 매달린 것은 관성 때문에 카본다 크게 움직인다.
                    t.localPosition = _dropHome[i] + new Vector3(0f, offset * 1.4f, 0f);
                }
            }

            if (_cameraRig != null)
                _cameraRig.localPosition = _rigHome + new Vector3(0f, offset, 0f);
        }

        private void CaptureHomes()
        {
            if (_dropTargets != null)
            {
                if (_dropHome == null || _dropHome.Length != _dropTargets.Length)
                    _dropHome = new Vector3[_dropTargets.Length];
                for (int i = 0; i < _dropTargets.Length; i++)
                    if (_dropTargets[i] != null && !_running) _dropHome[i] = _dropTargets[i].localPosition;
            }
            if (_cameraRig != null && !_running) _rigHome = _cameraRig.localPosition;
        }

        private void RestoreHomes()
        {
            if (_dropTargets != null && _dropHome != null)
            {
                for (int i = 0; i < _dropTargets.Length && i < _dropHome.Length; i++)
                    if (_dropTargets[i] != null) _dropTargets[i].localPosition = _dropHome[i];
            }
            if (_cameraRig != null) _cameraRig.localPosition = _rigHome;
        }
    }
}
