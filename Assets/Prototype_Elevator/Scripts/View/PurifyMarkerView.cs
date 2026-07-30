using UnityEngine;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.View
{
    /// <summary>
    /// 정화가 **왜** 일어났는지를 형태로 그린다.
    ///
    /// `.claude/visual-criteria.md` B-2.6: "어떤 칸이 왜 터졌는지 — 개수 정화인지 직선인지
    /// 연결 덩어리인지가 선·윤곽·점등으로 구분되는가. **전부 같은 이펙트면 실패다.**"
    /// 맥동 세기만 다르게 하면 정지 화면에서는 셋이 똑같아 보인다. 그래서 모양을 다르게 한다:
    ///
    ///   개수 정화 — 표식 없음. 흩어져 있다는 것 자체가 그림이다.
    ///   직선     — 세 칸을 **관통하는 하나의 긴 막대**.
    ///   연결     — 붙어 있는 칸 **사이를 잇는 연결봉들**. 규칙(직교 인접)을 그대로 그린다.
    ///   잭팟     — 9칸이 전부 이어져 격자 그물이 된다.
    ///
    /// 막대와 연결봉은 길이·개수·배치가 달라 회색조에서도, 정지 화면에서도 구분된다.
    ///
    /// 어느 칸이 패턴을 이뤘는지는 <see cref="PurifyEvent.PatternCells"/>가 알려준다.
    /// 여기서 보드를 다시 훑어 직선·덩어리를 찾지 않는다 — 그건 판정의 두 번째 구현이다.
    ///
    /// 풀은 미리 잡는다. 연출 중 프레임마다 오브젝트를 만들면 GC가 튄다.
    /// </summary>
    public sealed class PurifyMarkerView : MonoBehaviour
    {
        [SerializeField] private SpinBoardView _board;

        [Header("표식")]
        [Tooltip("동시에 세울 수 있는 막대 수. 9칸 전부 이어지면 직교 연결이 12개다.")]
        [SerializeField, Min(4)] private int _poolSize = 28;
        [Tooltip("막대 두께(미터).")]
        [SerializeField] private float _barThickness = 0.045f;
        [Tooltip("결과판 평면에서 앞으로 띄우는 거리. 심볼에 파묻히면 안 보인다.")]
        [SerializeField] private float _surfaceOffset = 0.12f;
        [Tooltip("직선 막대가 양 끝 칸 바깥으로 더 뻗는 길이. 줄이 '관통한다'는 느낌을 만든다.")]
        [SerializeField] private float _lineOverhang = 0.16f;

        [Header("색 — 형태가 주 신호이고 색은 보조다")]
        [SerializeField] private Color _tint = new Color(1f, 0.86f, 0.55f);
        [SerializeField, Min(0f)] private float _emission = 4.5f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private Transform[] _bars;
        private Renderer[] _barRenderers;
        private MaterialPropertyBlock _block;
        private int _used;

        private Vector3 _columnStep;   // 통관 한 칸 이동 벡터
        private Vector3 _rowStep;      // 행 한 칸 이동 벡터
        private Vector3 _normal;       // 결과판 앞쪽
        private bool _geometryReady;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (_board == null) _board = FindAnyObjectByType<SpinBoardView>();
            BuildPool();
        }

        private void BuildPool()
        {
            _bars = new Transform[_poolSize];
            _barRenderers = new Renderer[_poolSize];

            // 표식 전용 머티리얼 하나를 공유한다. 인스턴스가 막대마다 생기면
            // 색을 바꿀 때마다 드로우콜과 할당이 늘어난다 — 색은 MPB로 민다.
            Material shared = null;
            for (int i = 0; i < _poolSize; i++)
            {
                GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = $"PurifyBar_{i:00}";
                Destroy(bar.GetComponent<BoxCollider>());   // 조준을 가로막으면 안 된다
                bar.transform.SetParent(transform, false);
                bar.SetActive(false);

                var renderer = bar.GetComponent<Renderer>();
                if (shared == null) shared = renderer.sharedMaterial;
                else renderer.sharedMaterial = shared;

                _bars[i] = bar.transform;
                _barRenderers[i] = renderer;
            }
        }

        /// <summary>
        /// 결과판의 축을 칸 좌표에서 역산한다. 씬에서 판을 옮기거나 돌려도 따라간다 —
        /// 좌표를 코드에 박으면 판을 옮긴 다음 세션이 표식이 허공에 뜬 이유를 찾게 된다.
        /// </summary>
        private bool EnsureGeometry()
        {
            if (_geometryReady) return true;
            if (_board == null) return false;

            Transform origin = _board.CellTransform(SpinBoard.Index(0, 0));
            Transform acrossColumns = _board.CellTransform(SpinBoard.Index(2, 0));
            Transform downRows = _board.CellTransform(SpinBoard.Index(0, 2));
            if (origin == null || acrossColumns == null || downRows == null) return false;

            _columnStep = (acrossColumns.position - origin.position) / (SpinBoard.Columns - 1);
            _rowStep = (downRows.position - origin.position) / (SpinBoard.Rows - 1);

            Vector3 normal = Vector3.Cross(_columnStep, _rowStep).normalized;
            if (normal.sqrMagnitude < 0.0001f) return false;

            // 부호는 방 쪽으로 잡는다. 반대로 잡으면 표식이 벽 속으로 들어간다.
            Camera view = Camera.main;
            Vector3 toViewer = view != null
                ? view.transform.position - origin.position
                : Vector3.zero;
            if (toViewer.sqrMagnitude > 0.0001f && Vector3.Dot(normal, toViewer) < 0f)
                normal = -normal;

            _normal = normal;
            _geometryReady = true;
            return true;
        }

        /// <summary>이번 프레임의 표식을 처음부터 다시 세운다. 호출 순서: Begin → Add… → End.</summary>
        public void Begin() => _used = 0;

        /// <summary>
        /// 한 정화 이벤트의 표식을 세운다. <paramref name="intensity"/>는 맥동과 같은 값이라
        /// 막대가 심볼과 함께 밝아졌다 어두워진다.
        /// </summary>
        public void Add(in PurifyEvent purify, float intensity)
        {
            if (!EnsureGeometry()) return;

            int[] cells = purify.PatternCells;
            if (cells == null || cells.Length < 2) return;   // 개수 정화는 그릴 모양이 없다

            switch (purify.Pattern)
            {
                case PatternKind.Line:
                    AddLineBar(cells, intensity);
                    break;

                case PatternKind.Cluster:
                case PatternKind.FullBoard:
                    AddConnectionBars(cells, intensity);
                    break;
            }
        }

        public void End()
        {
            for (int i = _used; i < _bars.Length; i++)
                if (_bars[i].gameObject.activeSelf) _bars[i].gameObject.SetActive(false);
        }

        public void Clear()
        {
            Begin();
            End();
        }

        /// <summary>세 칸을 관통하는 막대 하나. 길이가 곧 "한 줄"이라는 신호다.</summary>
        private void AddLineBar(int[] cells, float intensity)
        {
            Vector3 first = CellPosition(cells[0]);
            Vector3 last = CellPosition(cells[cells.Length - 1]);
            Vector3 direction = last - first;
            float length = direction.magnitude;
            if (length < 0.0001f) return;

            PlaceBar((first + last) * 0.5f, direction / length, length + _lineOverhang * 2f, intensity);
        }

        /// <summary>
        /// 붙어 있는 칸 사이를 잇는 짧은 봉들. 덩어리가 어떻게 이어졌는지를 그대로 그린다.
        ///
        /// 직교 인접만 잇는다 — 그것이 기본 연결 규칙이기 때문이다
        /// (`SpinBoard` 인접 규칙 주석). 대각 연결이 해금되면 대각 봉도 그려야 한다.
        /// </summary>
        private void AddConnectionBars(int[] cells, float intensity)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                for (int j = i + 1; j < cells.Length; j++)
                {
                    int a = cells[i];
                    int b = cells[j];
                    int columnDelta = Mathf.Abs(SpinBoard.ColumnOf(a) - SpinBoard.ColumnOf(b));
                    int rowDelta = Mathf.Abs(SpinBoard.RowOf(a) - SpinBoard.RowOf(b));
                    if (columnDelta + rowDelta != 1) continue;

                    Vector3 from = CellPosition(a);
                    Vector3 to = CellPosition(b);
                    Vector3 direction = to - from;
                    float length = direction.magnitude;
                    if (length < 0.0001f) continue;

                    PlaceBar((from + to) * 0.5f, direction / length, length, intensity);
                }
            }
        }

        private Vector3 CellPosition(int index)
        {
            Transform cell = _board.CellTransform(index);
            return cell != null ? cell.position : Vector3.zero;
        }

        private void PlaceBar(Vector3 center, Vector3 direction, float length, float intensity)
        {
            if (_used >= _bars.Length) return;   // 풀 고갈 — 조용히 생략한다. 프레임을 멈추는 것보다 낫다

            Transform bar = _bars[_used];
            bar.position = center + _normal * _surfaceOffset;
            bar.rotation = Quaternion.LookRotation(_normal, direction);
            bar.localScale = new Vector3(_barThickness, length, _barThickness);
            if (!bar.gameObject.activeSelf) bar.gameObject.SetActive(true);

            Renderer renderer = _barRenderers[_used];
            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, _tint);
            _block.SetColor(EmissionColorId, _tint * (_emission * Mathf.Clamp01(intensity)));
            renderer.SetPropertyBlock(_block);

            _used++;
        }
    }
}
