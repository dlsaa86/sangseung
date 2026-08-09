using UnityEngine;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.View
{
    /// <summary>
    /// 정화가 **왜** 일어났는지를 형태로 그린다. 그리고 **칸 하나 안에서만** 그린다.
    ///
    /// `.claude/visual-criteria.md` B-2.6: "어떤 칸이 왜 터졌는지 — 개수 정화인지 직선인지
    /// 연결 덩어리인지가 선·윤곽·점등으로 구분되는가. **전부 같은 이펙트면 실패다.**"
    /// 맥동 세기만 다르게 하면 정지 화면에서는 셋이 똑같아 보인다. 그래서 모양을 다르게 한다.
    ///
    /// **2026-08-02 에 형상 계통을 통째로 바꿨다.** 예전에는 셀과 셀을 **잇는 막대**를 세웠다 —
    /// 직선은 세 칸을 관통하는 하나의 긴 막대, 연결은 칸 사이를 잇는 연결봉들. 그 그림이
    /// 4·5·8·9·10·11·12·13·14·15차 **10라운드 연속**으로 「카지노 슬롯머신 페이라인」
    /// (`VISUAL_SPEC §1` 명시 금지)으로 판정됐고 스타일 1점의 유일한 원인이었다.
    ///
    /// 직전 세션은 **색**으로 고치려 했다(금색 → 청록). 15차가 24장을 열어 확인한 결과
    /// 그 변경은 **화면 어디에도 도달하지 않았다** — 「막대의 알베도가 금색인 한 화면은
    /// 금색이고, 색을 바꿔도 흰 막대는 여전히 페이라인이다.」 판정문의 결론이 정확했다:
    /// **「다음은 형상이어야 한다.」**
    ///
    /// 그래서 지금은 표식이 **칸 경계를 넘지 않는다.** 형태와 좌표는 전부
    /// <see cref="PurifyMarkerLayout"/> 이 소유하고(씬 없이 판정 가능), 이 클래스는
    /// 그 칸 로컬 좌표를 월드로 옮겨 풀에서 꺼낸 막대에 바르는 일만 한다.
    ///
    ///   개수 정화(인접) — 덩어리 **외곽선**. 바깥 경계에 닿는 변만 칸 안쪽으로 들여 그린다.
    ///   직선           — 줄 방향을 가리키는 **셰브런**. 방향이 형상 안에 있다.
    ///   연결 · 잭팟    — 칸 중심에서 연결된 이웃 쪽으로 뻗다 멈추는 **연결 팔**.
    ///
    /// 세 계통은 닫힌 사각형 / V / 십자로 실루엣이 다르므로 회색조에서도, 정지 화면에서도
    /// 갈린다. **`UP-CORE-12`(판정 원인 시각화)는 살아 있다** — 오히려 예전에 표식이 아예
    /// 없던 「인접 개수 정화」가 이번에 자기 형상을 얻었다.
    ///
    /// 어느 칸이 패턴을 이뤘는지는 <see cref="PurifyEvent.PatternCells"/>가 알려준다.
    /// 여기서 보드를 다시 훑어 직선·덩어리를 찾지 않는다 — 그건 판정의 두 번째 구현이다.
    ///
    /// 같은 막대 풀이 **순차 공개 셔터**도 그린다(`UP-FIX-20`). 예전에는 아직 안 열린 칸이
    /// 그냥 빈칸이라 **정화로 비워진 칸과 픽셀이 같았고**, 정지 캡처로는 「공개 중」과
    /// 「빈 판」을 구별할 수 없었다. 셋을 형태로 가른다:
    ///
    ///   아직 안 열림 — 칸 한가운데의 **뭉툭한 빗장 1개**, 어둡다
    ///   지금 열림    — 위아래로 물러난 **얇은 조각 2개**, 밝다 (셔터가 갈라졌다)
    ///   다 열림      — 표식 없음. 심볼만 남는다
    ///
    /// 개수(1/2/0)·위치(중앙/가장자리)·두께·밝기가 전부 다르므로 회색조에서도 갈린다.
    /// 두 용도는 시간대가 겹치지 않는다 — 셔터는 공개 구간, 정화 표식은 맥동 구간이다.
    ///
    /// **셔터도 폭을 줄였다.** `21_board_and_gauge` 가 6라운드째 「결과판을 가로지르는 금색
    /// 막대 6개」로 감점됐는데, 그 여섯 개는 정화 표식이 아니라 **이 셔터**였다 —
    /// 지적된 y 좌표 6개가 3행 × 갈라진 두 짝과 정확히 맞는다. 폭 0.78 → 0.42 로 줄이고
    /// 두께 하한을 둬 「가늘고 긴 것」이 아니라 「뭉툭한 것」이 되게 했다.
    ///
    /// 풀은 미리 잡는다. 연출 중 프레임마다 오브젝트를 만들면 GC가 튄다.
    /// 그리고 이 파일은 **매 프레임 0 B**를 지켜야 한다(`UP-TECH-05`) — 그래서
    /// <see cref="PlaceBar"/> 는 `GetPropertyBlock` 을 부르지 않는다. Unity 의 그 게터는
    /// 호출마다 새 저장소를 만들고, 같은 함정이 `SpinBoardView` 에서 1,638 B/프레임으로
    /// 나타났다(2026-08-02). 여기서는 정화 맥동이 도는 동안 매 프레임 최대 24회 돌던 자리다.
    /// </summary>
    public sealed class PurifyMarkerView : MonoBehaviour
    {
        /// <summary>셔터가 한 프레임에 요구하는 막대 수의 상한. 풀이 이보다 작으면 표식이 조용히 빠진다.</summary>
        public const int RevealPoolRequirement = 12;

        /// <summary>정화 표식이 한 프레임에 요구하는 막대 수의 상한(9칸 잭팟의 연결 팔 24개).</summary>
        public const int PurifyPoolRequirement = PurifyMarkerLayout.MaxPlacementsPerEvent;

        /// <summary>두 용도 중 큰 쪽. 풀은 이보다 작을 수 없다.</summary>
        public const int PoolRequirement =
            PurifyPoolRequirement > RevealPoolRequirement ? PurifyPoolRequirement : RevealPoolRequirement;

        /// <summary>닫힌 칸 하나가 쓰는 막대 수.</summary>
        public const int SealedBarsPerCell = PurifyMarkerLayout.SealedBarsPerCell;

        /// <summary>열리는 칸 하나가 쓰는 막대 수(갈라진 두 짝).</summary>
        public const int OpeningBarsPerCell = PurifyMarkerLayout.OpeningBarsPerCell;

        [SerializeField] private SpinBoardView _board;

        [Header("표식")]
        [Tooltip("동시에 세울 수 있는 막대 수. 9칸 잭팟의 연결 팔이 24개다.")]
        [SerializeField, Min(PoolRequirement)] private int _poolSize = 28;
        [Tooltip("막대 두께(미터). 칸을 넘지 않는 선에서만 쓰인다 — 넘으면 코드가 잘라낸다.")]
        [SerializeField] private float _barThickness = 0.045f;
        [Tooltip("결과판 평면에서 앞으로 띄우는 거리. 심볼에 파묻히면 안 보인다. " +
                 "칸 피치의 20%를 넘지 않게 코드가 자른다 — 표식이 계기판 글자를 먹은 적이 있다(UP-FIX-41).")]
        [SerializeField] private float _surfaceOffset = 0.12f;

        [Header("순차 공개 셔터 — UP-FIX-20")]
        [Tooltip("닫힌 셔터의 두께 배수. 열린 짝(얇음)과 두께로도 갈린다.")]
        [SerializeField, Range(1f, 3f)] private float _sealedThickness = 2.1f;
        [Tooltip("닫힌 셔터의 발광. 밝기가 「아직 안 열렸다」의 신호이므로 낮게 둔다.")]
        [SerializeField, Range(0f, 1.5f)] private float _sealedEmission = 0.35f;
        [Tooltip("닫힌 셔터의 바탕색. 회색조에서 열린 짝보다 확실히 어두워야 한다.")]
        [SerializeField] private Color _sealedTint = new Color(0.30f, 0.32f, 0.35f);

        [Header("색 — 형태가 주 신호이고 색은 보조다")]
        [SerializeField] private Color _tint = new Color(1f, 0.86f, 0.55f);
        [SerializeField, Min(0f)] private float _emission = 4.5f;

        /// <summary>
        /// 셰이더 프로퍼티 ID 를 **정적 초기화자에 두지 않는다.**
        ///
        /// `Shader.PropertyToID` 는 네이티브 호출이라, static readonly 로 두면 이 타입의
        /// 상수 하나만 건드려도 타입 초기화자가 돌면서 Unity 런타임을 요구한다. 실제로
        /// 씬 없는 검사 두 개(`OverharvestStageTests` 의 셔터 항목)가 그것 때문에
        /// **TypeInitializationException 으로만 실패하고 있었다** — 로직은 멀쩡한데.
        /// 인스턴스 필드로 내리면 순수 정적 함수들이 씬 없이 그대로 불린다.
        ///
        /// **`_EmissionColor` 라는 이름이 사라지면 점등이 오류 없이 조용히 없어진다.**
        /// `Ascend/Stylized` 셰이더 주석이 경고하는 그대로다 —
        /// `MaterialPropertyBlock` 은 없는 프로퍼티에 값을 써도 아무 말도 하지 않는다.
        /// </summary>
        private int _baseColorId;
        private int _emissionColorId;

        private Transform[] _bars;
        private Renderer[] _barRenderers;
        private MaterialPropertyBlock _block;
        private int _used;
        private bool _poolExhaustionReported;

        /// <summary>
        /// 한 사건의 표식 배치를 받는 버퍼. **미리 잡는다** — 매 프레임 새로 만들면
        /// 정화 연출이 도는 동안 할당이 생기고 `UP-TECH-05` 가 깨진다.
        /// </summary>
        private readonly PurifyMarkerPlacement[] _placements =
            new PurifyMarkerPlacement[PoolRequirement];

        private Vector3 _columnUnit;   // 열 축 단위 벡터
        private Vector3 _rowUnit;      // 행 축 단위 벡터
        private float _pitch;          // 칸 한 칸의 간격(미터). 두 축 중 짧은 쪽 — 담김 판정을 보수적으로 만든다
        private Vector3 _normal;       // 결과판 앞쪽
        private bool _geometryReady;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _baseColorId = Shader.PropertyToID("_BaseColor");
            _emissionColorId = Shader.PropertyToID("_EmissionColor");
            if (_board == null) _board = FindAnyObjectByType<SpinBoardView>();
            BuildPool();
        }

        private void BuildPool()
        {
            // 풀이 모자라면 `PlaceBar`가 **조용히** 생략한다 — 그러면 셔터가 몇 칸만
            // 서고 정지 화면이 거짓말을 한다. 인스펙터 값이 요구량보다 작으면 올리고 적는다.
            int size = _poolSize;
            if (size < PoolRequirement)
            {
                Debug.LogWarning($"[상승] {name}: 막대 풀 {size}개는 요구량 {PoolRequirement}개" +
                                 $"(셔터 {RevealPoolRequirement} · 정화 {PurifyPoolRequirement})를 못 채운다 — " +
                                 $"{PoolRequirement}개로 올린다. 씬의 _poolSize 를 올려라.", this);
                size = PoolRequirement;
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

            Vector3 columnStep = (acrossColumns.position - origin.position) / (SpinBoard.Columns - 1);
            Vector3 rowStep = (downRows.position - origin.position) / (SpinBoard.Rows - 1);

            float columnPitch = columnStep.magnitude;
            float rowPitch = rowStep.magnitude;
            if (columnPitch < 0.0001f || rowPitch < 0.0001f) return false;

            Vector3 normal = Vector3.Cross(columnStep, rowStep).normalized;
            if (normal.sqrMagnitude < 0.0001f) return false;

            // 부호는 방 쪽으로 잡는다. 반대로 잡으면 표식이 벽 속으로 들어간다.
            Camera view = Camera.main;
            Vector3 toViewer = view != null
                ? view.transform.position - origin.position
                : Vector3.zero;
            if (toViewer.sqrMagnitude > 0.0001f && Vector3.Dot(normal, toViewer) < 0f)
                normal = -normal;

            _columnUnit = columnStep / columnPitch;
            _rowUnit = rowStep / rowPitch;

            // 두 축의 간격이 다르면 **짧은 쪽**을 단위로 쓴다. 긴 쪽을 쓰면 짧은 축에서
            // 표식이 칸을 넘어가고, 그 순간 `G-SLOT-A` ④(경계 횡단)가 되살아난다.
            _pitch = Mathf.Min(columnPitch, rowPitch);

            _normal = normal;
            _geometryReady = true;
            return true;
        }

        /// <summary>이번 프레임의 표식을 처음부터 다시 세운다. 호출 순서: Begin → Add… → End.</summary>
        public void Begin() => _used = 0;

        /// <summary>
        /// 한 정화 이벤트의 표식을 세운다. <paramref name="intensity"/>는 맥동과 같은 값이라
        /// 표식이 심볼과 함께 밝아졌다 어두워진다.
        ///
        /// 그릴 칸은 <see cref="PurifyEvent.PatternCells"/>다. 이 값이 null 인 경우는
        /// 「인접을 요구하지 않는 순수 개수 정화」뿐이고, 그건 애초에 모양이 없는 패턴이라
        /// 표식도 없다(<c>SpinRuleSet.RequireAdjacencyToPurify</c> 를 끈 경우).
        /// </summary>
        public void Add(in PurifyEvent purify, float intensity)
        {
            if (!EnsureGeometry()) return;

            int[] cells = purify.PatternCells;
            if (cells == null || cells.Length < 2) return;

            int count = PurifyMarkerLayout.Build(purify.Pattern, cells, _placements);
            float amount = Mathf.Clamp01(intensity);
            for (int i = 0; i < count; i++)
                Place(in _placements[i], amount);
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

            int count = PurifyMarkerLayout.BuildRevealShutters(revealedColumns, _placements);
            for (int i = 0; i < count; i++)
                Place(in _placements[i], 1f);
        }

        /// <summary>
        /// 주어진 진행도에서 셔터가 쓰는 막대 수. **순수 함수다** — 풀이 모자라 표식이
        /// 조용히 빠지는지를 씬 없이 검사할 수 있다(`OverharvestStageTests`).
        /// </summary>
        public static int RevealBarsNeeded(int revealedColumns)
            => PurifyMarkerLayout.RevealBarsNeeded(revealedColumns);

        /// <summary>
        /// 한 정화 사건이 요구하는 표식 수. 풀 검증용 순수 함수다.
        /// </summary>
        public static int PurifyBarsNeeded(PatternKind pattern, int[] patternCells)
            => PurifyMarkerLayout.Needed(pattern, patternCells);

        /// <summary>
        /// 칸 로컬 배치 하나를 월드 막대로 옮긴다.
        ///
        /// **여기서 칸을 넘지 않는다는 보장이 성립한다.** 길이·중심은
        /// <see cref="PurifyMarkerLayout"/> 이 칸 피치 단위로 이미 <see cref="PurifyMarkerLayout.CellHalfLimit"/>
        /// 안에 넣어 뒀고, 두께만 인스펙터에서 오므로 그 두께를 같은 모듈이 잘라낸다.
        /// </summary>
        private void Place(in PurifyMarkerPlacement placement, float intensity)
        {
            Transform cell = _board.CellTransform(placement.Cell);
            if (cell == null) return;      // 배선 구멍에 원점 막대를 세우지 않는다

            Vector3 center = cell.position
                           + _columnUnit * (placement.Center.x * _pitch)
                           + _rowUnit * (placement.Center.y * _pitch);

            Vector3 direction = _columnUnit * placement.Direction.x + _rowUnit * placement.Direction.y;
            float directionLength = direction.magnitude;
            if (directionLength < 0.0001f) return;
            direction /= directionLength;

            float requested = ThicknessFor(placement.Shape);
            float thickness = PurifyMarkerLayout.ThicknessInPitch(in placement, requested / _pitch) * _pitch;

            Color color;
            float emission;
            switch (placement.Shape)
            {
                case PurifyMarkerShape.ShutterSealed:
                    color = _sealedTint;
                    emission = _sealedEmission;
                    break;
                case PurifyMarkerShape.ShutterOpen:
                    color = _tint;
                    emission = _emission;
                    break;
                default:
                    color = _tint;
                    emission = _emission * intensity;
                    break;
            }

            PlaceBar(center, direction, placement.Length * _pitch, thickness, color, emission);
        }

        private float ThicknessFor(PurifyMarkerShape shape)
            => shape == PurifyMarkerShape.ShutterSealed
                ? _barThickness * _sealedThickness
                : _barThickness;

        private void PlaceBar(Vector3 center, Vector3 direction, float length,
                              float thickness, Color color, float emission)
        {
            if (_used >= _bars.Length)
            {
                // 풀 고갈. 프레임을 멈추는 것보다 생략이 낫지만 **조용히** 넘어가면
                // 정지 캡처가 사실과 다른 그림을 주장한다. 한 번만 적는다.
                if (!_poolExhaustionReported)
                {
                    _poolExhaustionReported = true;
                    Debug.LogWarning($"[상승] {name}: 표식 풀 {_bars.Length}개가 고갈됐다 — " +
                                     "이번 프레임의 표식 일부가 빠진다.", this);
                }
                return;
            }

            // 표면에서 띄우는 거리도 칸에 매단다. 절대값으로 두면 판을 옮겼을 때
            // 표식만 앞으로 튀어나와 계기판 글자를 먹는다(`UP-FIX-41` 이 그 사건이다).
            float offset = Mathf.Min(_surfaceOffset, _pitch * 0.2f);

            Transform bar = _bars[_used];
            bar.position = center + _normal * offset;
            bar.rotation = Quaternion.LookRotation(_normal, direction);
            bar.localScale = new Vector3(thickness, length, thickness);
            if (!bar.gameObject.activeSelf) bar.gameObject.SetActive(true);

            // **`GetPropertyBlock` 을 부르지 않는다.** Unity 의 그 게터는 호출마다 새
            // 저장소를 만들고, 같은 함정이 `SpinBoardView` 에서 1,638 B/프레임이었다.
            // 이 슬롯에 블록을 쓰는 곳은 여기뿐이고 우리가 쓰는 프로퍼티도 둘뿐이라
            // 기존 값을 읽어 올 이유가 없다.
            Renderer renderer = _barRenderers[_used];
            _block.SetColor(_baseColorId, color);
            _block.SetColor(_emissionColorId, color * emission);
            renderer.SetPropertyBlock(_block);

            _used++;
        }
    }
}
