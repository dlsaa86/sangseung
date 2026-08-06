using UnityEngine;
using Ascend.Prototype.Build;
using Ascend.Prototype.Run;

namespace Ascend.Prototype.Demo
{
    /// <summary>
    /// 지정한 빌드를 런 시작 시점에 강제로 싣는다. **데모·검증 전용**이다.
    ///
    /// 이 컴포넌트가 <see cref="RunSessionBehaviour"/>를 고치지 않고 붙는 이유:
    /// 그 파일은 지금 다른 세션이 밸런스 작업으로 쓰고 있다
    /// (`docs/runtime/SESSION_20260806_DEMO_LANE.md` §0). 마침 이미 있는
    /// <see cref="RunSessionBehaviour.RunStarted"/> 이벤트가 필요한 전부였다 —
    /// 소유 경로를 넘지 않고도 같은 일을 한다.
    ///
    /// <para>
    /// <b>기본은 꺼져 있다.</b> 켜진 채로 커밋되면 사람이 실제 제시에서 고른 빌드가
    /// 조용히 덮어써지고, 그러면 「내가 고른 게 왜 안 먹지」를 규칙에서 찾게 된다.
    /// 화면과 콘솔에 켜져 있다는 사실이 항상 남는 것도 같은 이유다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoLoadoutInjector : MonoBehaviour
    {
        [Header("데모 적재")]
        [Tooltip("꺼져 있으면 아무것도 하지 않는다. 기본값 = 꺼짐.")]
        [SerializeField] private bool _enabled;

        [Tooltip("실을 품목 id 를 쉼표로 나열한다. 에디터 창(Ascend/Demo Loadout)이 여기에 쓴다.")]
        [SerializeField] private string _itemIds = "";

        [Tooltip("승객이 목적지에서 내려도 다시 태운다. 축 하나를 10층까지 관측할 때만 켠다.")]
        [SerializeField] private bool _keepAboard;

        [Tooltip("비어 있으면 FindAnyObjectByType 으로 찾는다.")]
        [SerializeField] private RunSessionBehaviour _run;

        private DemoLoadoutSpec _spec;
        private int _lastFloor = -1;

        /// <summary>지금 이 주입기가 실제로 개입하고 있는가. HUD·보고가 읽는다.</summary>
        public bool IsActive => _enabled && _spec != null && _spec.Count > 0;

        /// <summary>사람이 읽는 한 줄. 꺼져 있으면 빈 문자열.</summary>
        public string Describe() => IsActive ? _spec.Describe() : string.Empty;

        /// <summary>런타임에서 갈아끼운다. 다음 <see cref="RunSessionBehaviour.ResetRun()"/>부터 적용된다.</summary>
        public void SetSpec(DemoLoadoutSpec spec, bool keepAboard)
        {
            _spec = spec;
            _itemIds = spec != null ? spec.Encode() : "";
            _keepAboard = keepAboard;
            _enabled = spec != null && spec.Count > 0;
        }

        private void Awake()
        {
            _spec = DemoLoadoutSpec.Decode(_itemIds);
            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();

            if (_run == null)
            {
                if (_enabled)
                    Debug.LogWarning("[데모 적재] RunSessionBehaviour 를 찾지 못했다 — 주입하지 않는다.", this);
                return;
            }

            _run.RunStarted += OnRunStarted;
        }

        private void OnDestroy()
        {
            if (_run != null) _run.RunStarted -= OnRunStarted;
        }

        private void OnRunStarted(RunSession session)
        {
            _lastFloor = -1;
            if (!IsActive || session == null) return;

            // 실을 수 없는 것은 **적용 전에** 말한다. 적용 도중 조용히 떨어뜨리면
            // 「6개를 골랐는데 4개만 탔다」가 어디에도 안 남는다.
            var problems = _spec.Problems();
            for (int i = 0; i < problems.Count; i++)
                Debug.LogWarning($"[데모 적재] {problems[i]}", this);

            int applied = _spec.ApplyTo(session.Loadout);
            _lastFloor = session.CurrentFloor;

            Debug.Log($"[데모 적재] 강제 탑승 {applied}개 — {_spec.Describe()}" +
                      (_keepAboard ? " (하차해도 다시 태운다)" : ""), this);
        }

        /// <summary>
        /// 층이 바뀌었을 때만 빈자리를 다시 채운다.
        ///
        /// 매 프레임이 아니라 층 경계인 이유: 하차는 <see cref="BuildLoadout.TakeDeparting"/>가
        /// 층 이동에서만 일으킨다. 그 사이에 폴링하면 같은 상태를 반복해서 확인할 뿐이고,
        /// 적재 변경 이벤트에 물리면 <see cref="BuildLoadout.Add"/>가 다시 그 이벤트를
        /// 일으켜 재진입이 된다.
        /// </summary>
        private void Update()
        {
            if (!_keepAboard || !IsActive) return;

            RunSession session = _run != null ? _run.Session : null;
            if (session == null) return;

            if (session.CurrentFloor == _lastFloor) return;
            _lastFloor = session.CurrentFloor;

            int added = _spec.TopUp(session.Loadout);
            if (added > 0)
                Debug.Log($"[데모 적재] {session.CurrentFloor}층 — 내린 자리 {added}개 재탑승", this);
        }
    }
}
