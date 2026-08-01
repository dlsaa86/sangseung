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
    /// 같은 막대 풀이 **순차 공개 셔터**도 그린다(`UP-FIX-20`). 예전에는 아직 안 열린 칸이
    /// 그냥 빈칸이라 **정화로 비워진 칸과 픽셀이 같았고**, 정지 캡처로는 「공개 중」과
    /// 「빈 판」을 구별할 수 없었다. 셋을 형태로 가른다:
    ///
    ///   아직 안 열림 — 칸 한가운데를 가로지르는 **두꺼운 막대 1개**, 어둡다
    ///   지금 열림    — 위아래로 물러난 **얇은 막대 2개**, 밝다 (셔터가 갈라졌다)
    ///   다 열림      — 막대 없음. 심볼만 남는다
    ///
    /// 개수(1/2/0)·위치(중앙/가장자리)·두께·밝기가 전부 다르므로 회색조에서도 갈린다.
    /// 두 용도는 시간대가 겹치지 않는다 — 셔터는 공개 구간, 정화 막대는 맥동 구간이다.
    ///
    /// 풀은 미리 잡는다. 연출 중 프레임마다 오브젝트를 만들면 GC가 튄다.
    /// </summary>
    public sealed class PurifyMarkerView : MonoBehaviour
    {
        /// <summary>셔터가 한 프레임에 요구하는 막대 수의 상한. 풀이 이보다 작으면 표식이 조용히 빠진다.</summary>
        public const int RevealPoolRequirement = 12;

        /// <summary>닫힌 칸 하나가 쓰는 막대 수.</summary>
        public const int SealedBarsPerCell = 1;

        /// <summary>열리는 칸 하나가 쓰는 막대 수(갈라진 두 짝).</summary>
        public const int OpeningBarsPerCell = 2;

        [SerializeField] private SpinBoardView _board;

        [Header("표식")]
        [Tooltip("동시에 세울 수 있는 막대 수. 9칸 전부 이어지면 직교 연결이 12개다.")]
        [SerializeField, Min(RevealPoolRequirement)] private int _poolSize = 28;
        [Tooltip("막대 두께(미터).")]
        [SerializeField] private float _barThickness = 0.045f;
        [Tooltip("결과판 평면에서 앞으로 띄우는 거리. 심볼에 파묻히면 안 보인다.")]
        [SerializeField] private float _surfaceOffset = 0.12f;
        [Tooltip("직선 막대가 양 끝 칸 바깥으로 더 뻗는 길이. 줄이 '관통한다'는 느낌을 만든다.")]
        [SerializeField] private float _lineOverhang = 0.16f;

        [Header("순차 공개 셔터 — UP-FIX-20")]
        [Tooltip("셔터 막대가 칸 폭의 몇 배를 덮는가.")]
        [SerializeField, Range(0.3f, 1f)] private float _shutterSpan = 0.78f;
        [Tooltip("열리는 칸에서 갈라진 두 짝이 중심에서 물러나는 거리(행 간격 배수).")]
        [SerializeField, Range(0.15f, 0.5f)] private float _shutterOpenGap = 0.34f;
        [Tooltip("닫힌 셔터의 두께 배수. 열린 짝(얇음)과 두께로도 갈린다.")]
        [SerializeField, Range(1f, 3f)] private float _sealedThickness = 2.1f;
        [Tooltip("닫힌 셔터의 발광. 밝기가 「아직 안 열렸다」의 신호이므로 낮게 둔다.")]
        [SerializeField, Range(0f, 1.5f)] private float _sealedEmission = 0.35f;
        [Tooltip("닫힌 셔터의 바탕색. 회색조에서 열린 짝보다 확실히 어두워야 한다.")]
        [SerializeField] private Color _sealedTint = new Color(0.30f, 0.32f, 0.35f);

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
            // 풀이 모자라면 `PlaceBar`가 **조용히** 생략한다 — 그러면 셔터가 몇 칸만
            // 서고 정지 화면이 거짓말을 한다. 인스펙터 값이 요구량보다 작으면 올리고 적는다.
            int size = _poolSize;
            if (size < RevealPoolRequirement)
            {
                Debug.LogWarning($"[상승] {name}: 막대 풀 {size}개는 순차 공개 셔터 " +
                                 $"{RevealPoolRequirement}개를 못 채운다 — {RevealPoolRequirement}개로 올린다.", this);
                size = RevealPoolRequirement;
            }

            _bars = new Transform[size];
            _barRenderers = new Renderer[size];

            // 표식 전용 머티리얼 하나를 공유한다. 인스턴스가 막대마다 생기면
            // 색을 바꿀 때마다 드로우콜과 할당이 늘어난다 — 색은 MPB로 민다.
            Material shared = null;
            for (int i = 0; i < size; i++)
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

        /// <summary>
        /// 순차 공개 셔터를 한 번에 세운다(Begin·End 포함).
        ///
        /// <paramref name="revealedColumns"/>는 <see cref="SpinPresenter.RevealedColumns"/>와
        /// 같은 값이다 — 0이면 전부 닫힘, 1~3이면 그 열이 방금 내려앉은 상태,
        /// <see cref="SpinPresenter.RevealComplete"/>면 표식이 사라진다.
        ///
        /// 캡처 하네스가 재생을 거치지 않고 판을 직접 밀어 넣을 때도 이 한 줄로 같은 그림을
        /// 세울 수 있다(`TenFloorCaptureRig.ShowPurifies`와 같은 용법).
        /// </summary>
        public void ShowReveal(int revealedColumns)
        {
            Begin();
            AddRevealShutters(revealedColumns);
            End();
        }

        /// <summary>
        /// 셔터 막대를 현재 프레임 표식에 더한다. 호출 순서는 Begin → Add… → End.
        ///
        /// 단계 판정은 <see cref="SpinPresenter.StageOfColumn"/> 하나만 쓴다 — 여기서
        /// 다시 세면 표식과 판이 서로 다른 진행도를 주장하게 된다.
        /// </summary>
        public void AddRevealShutters(int revealedColumns)
        {
            if (!EnsureGeometry()) return;

            float width = _columnStep.magnitude;
            if (width < 0.0001f) return;

            Vector3 across = _columnStep / width;    // 칸을 가로지르는 방향
            Vector3 gap = _rowStep * _shutterOpenGap; // 갈라진 짝이 물러나는 오프셋
            float span = width * _shutterSpan;

            for (int column = 0; column < SpinBoard.Columns; column++)
            {
                SpinPresenter.RevealStage stage = SpinPresenter.StageOfColumn(column, revealedColumns);
                if (stage == SpinPresenter.RevealStage.Open) continue;

                for (int row = 0; row < SpinBoard.Rows; row++)
                {
                    Transform cell = _board.CellTransform(SpinBoard.Index(column, row));
                    if (cell == null) continue;      // 배선 구멍에 원점 막대를 세우지 않는다
                    Vector3 center = cell.position;

                    if (stage == SpinPresenter.RevealStage.Sealed)
                    {
                        PlaceBar(center, across, span,
                                 _barThickness * _sealedThickness, _sealedTint, _sealedEmission);
                        continue;
                    }

                    PlaceBar(center + gap, across, span, _barThickness, _tint, _emission);
                    PlaceBar(center - gap, across, span, _barThickness, _tint, _emission);
                }
            }
        }

        /// <summary>
        /// 주어진 진행도에서 셔터가 쓰는 막대 수. **순수 함수다** — 풀이 모자라 표식이
        /// 조용히 빠지는지를 씬 없이 검사할 수 있다(`OverharvestStageTests`).
        /// </summary>
        public static int RevealBarsNeeded(int revealedColumns)
        {
            int bars = 0;
            for (int column = 0; column < SpinBoard.Columns; column++)
            {
                switch (SpinPresenter.StageOfColumn(column, revealedColumns))
                {
                    case SpinPresenter.RevealStage.Sealed:
                        bars += SpinBoard.Rows * SealedBarsPerCell;
                        break;
                    case SpinPresenter.RevealStage.Opening:
                        bars += SpinBoard.Rows * OpeningBarsPerCell;
                        break;
                }
            }
            return bars;
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
            => PlaceBar(center, direction, length,
                        _barThickness, _tint, _emission * Mathf.Clamp01(intensity));

        private void PlaceBar(Vector3 center, Vector3 direction, float length,
                              float thickness, Color color, float emission)
        {
            if (_used >= _bars.Length) return;   // 풀 고갈 — 조용히 생략한다. 프레임을 멈추는 것보다 낫다

            Transform bar = _bars[_used];
            bar.position = center + _normal * _surfaceOffset;
            bar.rotation = Quaternion.LookRotation(_normal, direction);
            bar.localScale = new Vector3(thickness, length, thickness);
            if (!bar.gameObject.activeSelf) bar.gameObject.SetActive(true);

            Renderer renderer = _barRenderers[_used];
            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            _block.SetColor(EmissionColorId, color * emission);
            renderer.SetPropertyBlock(_block);

            _used++;
        }
    }
}
