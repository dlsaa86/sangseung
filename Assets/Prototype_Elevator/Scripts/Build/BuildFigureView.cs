using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Ascend.Prototype.Player;
using Ascend.Prototype.Run;

namespace Ascend.Prototype.Build
{
    /// <summary>
    /// 승객과 부품을 **엘리베이터 안의 실제 오브젝트로** 세운다.
    ///
    /// `CURRENT_PHASE.md` §2.3이 "메뉴 아이콘만으로는 불충족"이라고 못박은 요구다.
    /// 목록으로만 존재하면 무게도 적재 제한도 추상적인 숫자가 되어, 플레이어가
    /// "공간이 찼다"를 눈으로 볼 수 없다.
    ///
    /// 배치 원칙 — 둘레에 세우고 가운데를 비운다. 문(z=+1.6)에서 통관 벽(x=-1.45)까지의
    /// 통로가 막히면 `VISUAL_SPEC` B-1.3("좁아 보이되 접근이 막혀 보이지 않는다")이 깨지고,
    /// 최대 적재 캡처가 곧 "플레이 불가" 증거가 된다.
    ///
    /// 프리팹을 쓰지 않고 코드로 만드는 이유: 프리팹은 단일 소유 직렬화 에셋이라
    /// 병렬 작업에서 조용히 손상될 수 있다(`D-20260730-05`). 형태가 확정되면 프리팹으로 굳힌다.
    /// </summary>
    public sealed class BuildFigureView : MonoBehaviour
    {
        [SerializeField] private RunSessionBehaviour _run;

        [Tooltip("엘리베이터 안 적재 자리. 비어 있으면 기본 좌표를 쓴다.")]
        [SerializeField] private Transform _carAnchor;

        [Tooltip("승강장 후보 자리. 비어 있으면 기본 좌표를 쓴다.")]
        [SerializeField] private Transform _lobbyAnchor;

        [SerializeField] private InteractableDoorControl _doorControl;

        /// <summary>
        /// 둘레 배치. 가운데 통로(문 → 통관 벽)를 비워 둔 좌표다.
        /// 비례 재조정 후 내부는 x[-1.20..1.20] · z[-1.50..1.50]이므로, 어느 자리에 서도
        /// 몸통(폭 0.6)이 벽을 뚫지 않는다. 앞줄을 z=-1.15에 둔 것은 궤짝(깊이 0.5)이
        /// 새 앞벽(안쪽 면 z=-1.50)에 박히지 않게 하기 위해서다.
        /// </summary>
        private static readonly Vector3[] CarSlots =
        {
            new Vector3( 0.85f, 0f, -1.05f),
            new Vector3( 0.85f, 0f, -0.35f),
            new Vector3( 0.85f, 0f,  0.35f),
            new Vector3( 0.15f, 0f, -1.15f),
            new Vector3(-0.55f, 0f, -1.15f),
            new Vector3(-0.88f, 0f, -0.45f),
        };

        /// <summary>문 너머 승강장. 1m 폭 개구부를 통해 세 자리가 모두 보이는 위치다.</summary>
        private static readonly Vector3[] LobbySlots =
        {
            new Vector3(0.10f, 0f, 2.55f),
            new Vector3(0.65f, 0f, 2.95f),
            new Vector3(1.20f, 0f, 2.55f),
        };

        private readonly List<GameObject> _carFigures = new List<GameObject>();
        private readonly List<Vector3> _carBasePositions = new List<Vector3>();
        private readonly List<bool> _carIsPassenger = new List<bool>();
        private readonly List<GameObject> _lobbyFigures = new List<GameObject>();
        private readonly List<InteractableBuildCandidate> _candidates =
            new List<InteractableBuildCandidate>();

        /// <summary>
        /// 라벨 홀더들. **승객·부품의 자식이 아니라 이 컴포넌트의 자식이다.**
        ///
        /// 예전에는 승객 루트의 자식이었는데, 반응 자세가 루트에 비균등 스케일을 걸므로
        /// (`ApplyReactions` 의 `localScale = (spread, 1 - crouch*1.6, spread)`)
        /// 웅크린 승객의 이름표가 세로로 최대 35% 눌렸다. 배킹판 크기 계산도 그 스케일을
        /// 다시 타고 들어가 어긋난다. 위치만 따라가면 되는 것이라 부모를 끊는다 —
        /// 대신 여기서 명시적으로 파괴해야 한다(`ClearFigures`).
        /// </summary>
        private readonly List<GameObject> _labelHolders = new List<GameObject>();

        /// <summary>가시성을 켜고 끄는 대상. `BuildLabelLayout` 에 넘기는 것과 같은 목록이다.</summary>
        private readonly List<Renderer> _labelRenderers = new List<Renderer>();

        /// <summary>카메라마다 자세·크기·가시성을 다시 잡는 배치기.</summary>
        private readonly BuildLabelLayout _layout = new BuildLabelLayout();

        private Material _plateMaterial;

        /// <summary>
        /// 이번 프레임에 SRP 콜백이 배치를 끝냈는가. URP 에서는 매 프레임 참이 되므로
        /// `LateUpdate` 의 예비 경로가 헛돌지 않는다.
        /// </summary>
        private int _solvedFrame = int.MinValue;

        /// <summary>승객 실루엣의 높이. 이름표와 대사 라벨이 이 값을 기준으로 얹힌다.</summary>
        private const float PassengerHeight = 1.56f;

        /// <summary>
        /// 대사 라벨이 이름표 위로 올라가는 높이. 이름표는 <c>height + 0.24</c>에 있으므로
        /// 둘이 겹치지 않는 최소 간격이자, 캐빈 천장(≈2.4m)을 뚫지 않는 상한이다.
        /// </summary>
        private const float SpeechLabelRise = 0.52f;

        private static readonly int CullModeId = Shader.PropertyToID("_CullMode");
        private static readonly Dictionary<Material, Material> _singleSided =
            new Dictionary<Material, Material>();

        private Material _passengerMaterial;
        private Material _partMaterial;
        private Material _candidateMaterial;
        private Risk.RiskStateView _risk;
        private Transform _head;
        private Camera _headCamera;
        private int _signature = int.MinValue;
        private int _doorPromptKey = int.MinValue;

        [Header("시선 대상 — 좌표가 아니라 의미로 배선한다")]
        [Tooltip("룰렛 장치. 성공 반응 대부분이 여기를 본다.")]
        [SerializeField] private Transform _gazeDevice;
        [Tooltip("과수확 레버. §7이 요구하는 '공간적 사건'의 시선 채널.")]
        [SerializeField] private Transform _gazeOverharvestLever;
        [Tooltip("문. 나갈 수 있는가를 묻는 시선.")]
        [SerializeField] private Transform _gazeDoor;
        [Tooltip("천장. 무너질 것을 올려다본다. 비면 승객 머리 위 2m를 쓴다.")]
        [SerializeField] private Transform _gazeCeiling;

        /// <summary>
        /// 승객 자리 하나에 걸린 반응. `_passengerSlots[i]`의 승객에 대응한다.
        ///
        /// `BuildFigureView`가 반응을 **판단하지 않는다** — 어느 반응을 언제 걸지는
        /// `PassengerReactionDirector`가 정한다(§9.4의 우선순위·쿨다운·동시 수).
        /// 여기는 정해진 것을 몸으로 옮기기만 한다. 두 일을 한 곳에 두면
        /// "왜 저 승객이 저러는가"를 두 파일에서 따라가야 한다.
        /// </summary>
        private struct SlotReaction
        {
            public Npc.ReactionPose Pose;
            public Npc.ReactionGaze Gaze;
            public float Intensity;
            public string Line;
            public bool Active;
        }

        /// <summary>`_carFigures` 안에서 승객인 것의 인덱스. 부품은 반응하지 않는다.</summary>
        private readonly List<int> _passengerSlots = new List<int>();
        private readonly List<SlotReaction> _slotReactions = new List<SlotReaction>();

        /// <summary>
        /// 승객 머리 위의 대사 라벨. `_passengerSlots`와 같은 번호를 쓴다.
        ///
        /// **왜 월드 라벨인가**: §9.3의 「짧은 대사」는 누가 말했는지가 정보의 절반이다.
        /// 화면 하단 자막으로 내면 여섯 중 누구인지가 사라지고, 그러면 상반된 반응
        /// (같은 사건에 두 사람이 반대로 말한다)이 한 사람의 혼잣말로 읽힌다.
        /// 최종 형태는 아니다 — 말풍선 대신 텍스트만 띄우는 플레이스홀더다.
        /// </summary>
        private readonly List<TMP_Text> _speechLabels = new List<TMP_Text>();

        /// <summary>지금 각 라벨에 실제로 실려 있는 문자열. 매 프레임 같은 값을 다시 쓰지 않기 위한 것이다.</summary>
        private readonly List<string> _speechShown = new List<string>();

        [Tooltip("자막 허용 여부. 비면 전부 허용으로 본다 — 대사가 소리 없이 사라지지 않는다.")]
        [SerializeField] private Data.Profiles.AccessibilityProfile _accessibility;

        private Data.Profiles.AccessibilitySnapshot _accessibilitySnapshot =
            Data.Profiles.AccessibilityProfile.DefaultSnapshot;

        /// <summary>
        /// 지금 칸에 타고 있는 승객 수. 반응 중재기가 이 값으로 자리를 배분한다.
        /// 부품은 세지 않는다 — 궤짝은 놀라지 않는다.
        /// </summary>
        public int PassengerCount => _passengerSlots.Count;

        /// <summary>
        /// 승객 하나에 반응을 건다. 인덱스는 <see cref="PassengerCount"/> 범위의 승객 번호이며
        /// `_carFigures`의 인덱스가 아니다 — 부품이 섞여 있어 두 번호가 다르다.
        /// 범위를 벗어나면 조용히 무시한다(적재가 줄어드는 프레임에 실제로 일어난다).
        /// </summary>
        public void SetReaction(int passenger, Npc.ReactionPose pose, Npc.ReactionGaze gaze, float intensity)
            => SetReaction(passenger, pose, gaze, intensity, null);

        /// <summary>
        /// 대사까지 함께 거는 형태. §9.3의 표현 채널 중 정지 화면에 남는 셋
        /// (자세·시선·짧은 대사)이 여기서 한 번에 들어온다 — 셋이 따로 들어오면
        /// 한 프레임 동안 자세와 대사가 다른 반응을 가리키는 순간이 생긴다.
        /// </summary>
        public void SetReaction(int passenger, Npc.ReactionPose pose, Npc.ReactionGaze gaze,
                                float intensity, string line)
        {
            if (passenger < 0 || passenger >= _slotReactions.Count) return;
            _slotReactions[passenger] = new SlotReaction
            {
                Pose = pose,
                Gaze = gaze,
                Intensity = Mathf.Clamp01(intensity),
                Line = line,
                Active = true,
            };
        }

        /// <summary>반응을 거두고 기본 자세로 돌린다.</summary>
        public void ClearReaction(int passenger)
        {
            if (passenger < 0 || passenger >= _slotReactions.Count) return;
            _slotReactions[passenger] = default(SlotReaction);
        }

        /// <summary>전원의 반응을 거둔다. 층이 바뀌거나 런이 다시 시작할 때 쓴다.</summary>
        public void ClearAllReactions()
        {
            for (int i = 0; i < _slotReactions.Count; i++) _slotReactions[i] = default(SlotReaction);
        }

        /// <summary>
        /// `RiskStateView`·`Camera.main` 탐색을 매 프레임 반복하지 않기 위한 표식.
        /// 씬에 대상이 없으면 `null` 검사만으로는 영구히 매 프레임 전역 탐색이 돈다.
        /// </summary>
        private bool _searchedRisk;
        private bool _searchedHead;

        private void Awake()
        {
            // 접근성 값은 런 중에 바뀌지 않는다(옵션 메뉴가 아직 없다). 매 프레임
            // ScriptableObject 를 타고 들어가는 대신 한 번만 사본을 뜬다 —
            // `AudioDirector.Awake` 가 같은 이유로 같은 일을 한다.
            _accessibilitySnapshot =
                Data.Profiles.AccessibilityProfile.SnapshotOrDefault(_accessibility);

            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();
            if (_doorControl == null) _doorControl = FindAnyObjectByType<InteractableDoorControl>();
            if (_doorControl != null) _doorControl.onDepart.AddListener(OnDepart);
            if (_run != null) _run.RunStarted += OnRunStarted;

            Camera camera = Camera.main;
            if (camera != null) { _head = camera.transform; _headCamera = camera; }
        }

        /// <summary>
        /// **그리는 카메라마다** 라벨을 다시 배치한다.
        ///
        /// 이 훅이 이 파일에서 가장 중요한 한 줄이다. 예전 코드는 라벨을 플레이어 머리
        /// 쪽으로만 돌리고 「카메라가 둘인데 라벨은 하나만 볼 수 있다」고 적어 두었는데,
        /// 그 결론이 틀렸다 — URP 는 카메라마다 컬링 **전에** 이 콜백을 부른다.
        /// 고정 캡처는 전용 카메라를 쓰므로, 이것이 없으면 캡처에서 라벨의 절반이
        /// 뒷면 컬링으로 **통째로 사라진다**(`07_cargo_full` 실측 6자리 중 2자리).
        /// </summary>
        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null) return;
            // 미리보기·반사 카메라는 판정 대상이 아니다. 게임 뷰와 씬 뷰만 잡는다.
            if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) return;
            EnsureObstacles();
            _layout.Solve(camera);
            _solvedFrame = Time.frameCount;
        }

        private void EnsureObstacles()
        {
            if (!_layout.ObstaclesValid) _layout.RefreshObstacles(_labelRenderers);
        }

        private void OnDestroy()
        {
            if (_doorControl != null) _doorControl.onDepart.RemoveListener(OnDepart);
            if (_run != null) _run.RunStarted -= OnRunStarted;
            DestroyMaterial(ref _passengerMaterial);
            DestroyMaterial(ref _partMaterial);
            DestroyMaterial(ref _candidateMaterial);
            DestroyMaterial(ref _plateMaterial);
        }

        private static void DestroyMaterial(ref Material material)
        {
            if (material == null) return;
            if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
            material = null;
        }

        private void OnRunStarted(RunSession session)
        {
            _signature = int.MinValue;
            _doorPromptKey = int.MinValue;
            Rebuild();
        }

        private void OnDepart()
        {
            if (_run == null) return;
            _run.FinishBoarding();
        }

        private void LateUpdate()
        {
            RunSession run = _run != null ? _run.Session : null;
            FloorSession floor = run != null ? run.Current : null;
            bool boarding = floor != null && floor.Phase == FloorPhase.Boarding;

            // 상태 키가 바뀔 때만 다시 세운다. 매 프레임 재생성하면 초당 수백 개의
            // GameObject 쓰레기가 나온다 — 이 프로젝트가 GC를 134KB에서 3.8KB로
            // 끌어내린 작업을 그대로 되돌리는 짓이다.
            //
            // 키를 **정수로** 만든다. 처음엔 문자열로 이었는데, 그 자체가 매 프레임
            // `object[]` 배열 + enum 박싱 + `ToString()` 문자열을 낳아 바로 위 주석이
            // 경계한 일을 캐시 키가 저지르고 있었다. 독립 감사가 지목했다.
            // `RouletteInteractionBridge`와 `GameHudView`가 이미 정수 키를 쓴다 — 그 규약을 따른다.
            int loadCount = run.Loadout != null ? run.Loadout.Count : 0;
            int signature = floor == null
                ? -1
                : (floor.Plan.Floor << 12) | ((int)floor.Phase << 8) |
                  (floor.BuildOffers.Count << 4) | loadCount;

            if (signature != _signature)
            {
                _signature = signature;
                Rebuild();
            }

            if (_doorControl != null)
            {
                _doorControl.SetCanInteract(boarding);
                // 프롬프트 문자열도 상태가 바뀔 때만 짓는다. 문구는 적재 개수에만 달려 있다.
                int promptKey = boarding ? 1 + loadCount : 0;
                if (promptKey != _doorPromptKey)
                {
                    _doorPromptKey = promptKey;
                    _doorControl.SetPrompt(boarding
                        ? (loadCount > 0 ? $"문 닫고 출발 — {loadCount}개 적재" : "문 닫고 출발 — 적재 없음")
                        : "문");
                }
            }

            for (int i = 0; i < _candidates.Count; i++)
                if (_candidates[i] != null) _candidates[i].SetCanInteract(boarding);

            FaceReader();
            ReactToRisk();
        }

        /// <summary>
        /// 승객이 위험 상태에 반응한다. `AUTONOMOUS_PROTOTYPE_GOAL.md` §3이 위험을 표현할
        /// 공간 채널로 "승객 불안 반응"을 지목했고, `MASTER_PRD.md` §9는 "특정 감각 채널
        /// 하나에만 의존하지 않는다"고 요구한다. 조명과 험만으로는 채널이 둘뿐이다.
        ///
        /// 흔드는 것은 **승객뿐**이다. 궤짝은 묶여 있으므로 같이 떨면 "누가 불안한가"라는
        /// 정보가 사라진다. 무작위가 아니라 사인파를 쓰는 이유는 캡처 재현성이다 —
        /// `Random`을 쓰면 같은 시드·같은 상태의 캡처가 매번 달라진다.
        ///
        /// 진폭은 판독성을 해치지 않는 선에서 멈춘다(§3 "과도한 화면 흔들림 금지").
        /// Critical에서도 최대 2cm다.
        ///
        /// 그런데 2cm 이동은 **정지 화면에서 증거가 되지 못한다.** 고정 캡처로
        /// 판정하는 이상 위상이 어디서 잡힐지도 정해져 있지 않다. 그래서 자세
        /// 기울기를 함께 준다 — 절반 이상이 상수라 어느 순간에 찍어도 남는다.
        /// </summary>
        private void ReactToRisk()
        {
            if (_carFigures.Count == 0) return;
            if (_risk == null && !_searchedRisk)
            {
                // 한 번만 찾는다. 못 찾았을 때 매 프레임 다시 찾으면 전역 탐색이 영구히 돈다.
                _searchedRisk = true;
                _risk = FindAnyObjectByType<Risk.RiskStateView>();
            }

            // **위험 뷰가 없어도 여기서 빠져나가지 않는다.** 예전에는 `return` 했는데,
            // 그러면 아래 `ApplyReactions()` 가 통째로 실행되지 않아 §9.3의 표현 채널이
            // 「위험 시스템이 씬에 있을 때만」 동작했다. 위험과 반응은 서로 다른 요구다.
            // 위험 뷰가 없으면 Stable 로 읽어 흔들림을 0으로 두고 반응만 태운다.
            Risk.RiskLevel level = _risk != null ? _risk.Level : Risk.RiskLevel.Stable;

            float amplitude;
            float speed;
            float tilt;      // 도(°). 정지 화면에서 읽히는 유일한 채널이다.
            switch (level)
            {
                case Risk.RiskLevel.Strain:  amplitude = 0.006f; speed = 3.2f;  tilt = 3.5f;  break;
                case Risk.RiskLevel.Critical: amplitude = 0.017f; speed = 9.5f;  tilt = 8.0f;  break;
                case Risk.RiskLevel.Collapse: amplitude = 0.020f; speed = 14.0f; tilt = 11.0f; break;
                default:                      amplitude = 0f;     speed = 0f;    tilt = 0f;    break;
            }

            float time = Time.time;
            for (int i = 0; i < _carFigures.Count; i++)
            {
                GameObject figure = _carFigures[i];
                if (figure == null || i >= _carBasePositions.Count) continue;

                Vector3 basePosition = _carBasePositions[i];
                if (amplitude <= 0f || !_carIsPassenger[i])
                {
                    figure.transform.position = basePosition;
                    figure.transform.localRotation = Quaternion.identity;
                    continue;
                }

                // 위상을 자리마다 어긋나게 둔다. 전원이 같은 박자로 흔들리면
                // 사람이 아니라 하나의 기계로 읽힌다.
                float phase = i * 1.7f;
                float sway = Mathf.Sin(time * speed + phase) * amplitude;
                float bob = Mathf.Sin(time * speed * 0.63f + phase) * amplitude * 0.5f;
                figure.transform.position = basePosition + new Vector3(sway, bob, sway * 0.6f);

                // **자세를 기울인다.** ±6mm 흔들림은 정지 화면에서 잡히지 않고,
                // 셔터가 어느 위상에서 열릴지도 정해져 있지 않다. 독립 감사가
                // "09 의 승객 상자는 10 과 동일 자세·동일 위치"라고 지적한 이유다.
                //
                // 그래서 기울기의 절반 이상을 **상수로** 둔다. 어느 순간에 찍어도
                // 승객이 서로 반대로 기울어 있어 "불안한 사람들"로 읽힌다.
                // 화면을 흔드는 것이 아니라 물체의 자세라 §3 의 흔들림 금지에 걸리지 않는다.
                float lean = tilt * (0.58f + 0.42f * Mathf.Sin(time * speed * 0.8f + phase));
                float direction = (i & 1) == 0 ? 1f : -1f;
                figure.transform.localRotation =
                    Quaternion.Euler(lean * 0.35f * direction, 0f, lean * direction);
            }

            // 반응은 위험 자세 **위에** 얹는다. 두 채널이 서로를 지우면
            // "지금 위험한가"와 "방금 무슨 일이 있었나"가 번갈아 사라진다.
            ApplyReactions();
        }

        /// <summary>
        /// 중재기가 정한 반응을 몸으로 옮긴다. 자세와 시선 둘 다 — `MASTER_PRD.md` §9.3의
        /// 표현 채널 넷 중 정지 화면에 남는 것이 이 둘이다.
        ///
        /// 위험 반응이 이미 쓴 회전을 **덮어쓰지 않고 곱한다.** 웅크린 승객도 흔들려야
        /// 하고, 흔들리는 승객도 웅크릴 수 있어야 한다.
        /// </summary>
        private void ApplyReactions()
        {
            for (int p = 0; p < _passengerSlots.Count && p < _slotReactions.Count; p++)
            {
                SlotReaction reaction = _slotReactions[p];

                // 대사는 자세보다 먼저 처리한다 — 아래 `continue` 들이 대사 라벨을
                // 끄지 못하고 지나가면 반응이 끝난 승객의 말이 화면에 영원히 남는다.
                UpdateSpeech(p, reaction.Active ? reaction.Line : null);

                int index = _passengerSlots[p];
                if (index < 0 || index >= _carFigures.Count) continue;
                GameObject figure = _carFigures[index];
                if (figure == null || index >= _carBasePositions.Count) continue;

                Transform t = figure.transform;

                if (!reaction.Active)
                {
                    // 반응이 끝났으면 **스케일을 되돌린다.** 위치와 회전은 위험 반응이
                    // 매 프레임 다시 쓰지만 스케일은 아무도 건드리지 않으므로,
                    // 여기서 놓치면 웅크린 자세가 영원히 남는다.
                    if (t.localScale != Vector3.one) t.localScale = Vector3.one;
                    continue;
                }

                float k = Mathf.Clamp01(reaction.Intensity);

                // 자세 — 실루엣이 서로 달라야 한다. 색을 빼도 구분되는 것이 기준이다.
                float pitch = 0f, crouch = 0f, spread = 1f, rise = 0f;
                switch (reaction.Pose)
                {
                    case Npc.ReactionPose.Lean:   pitch = 16f * k; break;
                    case Npc.ReactionPose.Flinch: pitch = -12f * k; crouch = 0.05f * k; break;
                    case Npc.ReactionPose.Cower:  pitch = 24f * k; crouch = 0.22f * k; spread = 1f - 0.18f * k; break;
                    case Npc.ReactionPose.Stare:  pitch = 3f * k; break;
                    case Npc.ReactionPose.Brace:  pitch = 9f * k; crouch = 0.10f * k; spread = 1f + 0.16f * k; break;
                    case Npc.ReactionPose.Cheer:  pitch = -14f * k; rise = 0.07f * k; break;
                }

                t.position = _carBasePositions[index] + new Vector3(0f, rise - crouch, 0f);
                // 세로만 눌러 웅크림을 만든다. 가로까지 줄이면 사람이 아니라 축소된 모형이 된다.
                t.localScale = new Vector3(spread, 1f - crouch * 1.6f, spread);

                // 시선은 월드 방향에서 나오므로 부모 회전을 벗겨 로컬로 들여온다.
                // 부모가 회전하지 않은 지금은 항등이지만, 나중에 칸이 기울면
                // 이 한 줄이 없을 때만 승객이 엉뚱한 곳을 본다.
                Quaternion look = Quaternion.Inverse(transform.rotation) * GazeRotation(reaction.Gaze, t.position);
                t.localRotation = look * t.localRotation * Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        /// <summary>
        /// 대사 라벨 하나를 갱신한다. `null`이면 라벨을 끈다.
        ///
        /// 같은 문자열을 매 프레임 다시 넣지 않는 이유는 TMP 가 텍스트 설정에서
        /// 메시를 다시 짓기 때문이다 — 승객 여섯이면 초당 360회다.
        /// `MASTER_PRD.md` §13.2의 "워밍업 후 매 프레임 0 B"가 대사 때문에 깨지면 안 된다.
        /// </summary>
        private void UpdateSpeech(int passenger, string line)
        {
            if (passenger < 0 || passenger >= _speechLabels.Count) return;
            if (passenger >= _speechShown.Count) return;

            TMP_Text label = _speechLabels[passenger];
            if (label == null) return;

            // 접근성이 자막을 끄면 대사도 화면에 남기지 않는다. 판정은
            // `AccessibilitySnapshot.Caption` 하나에 맡긴다 — `if (ShowSubtitles)` 를
            // 각자 쓰기 시작하면 빠뜨릴 자리가 늘어난다(그 파일의 주석).
            string caption = _accessibilitySnapshot.Caption(line);
            string shown = string.IsNullOrEmpty(caption) ? null : caption;

            if (string.Equals(_speechShown[passenger], shown, StringComparison.Ordinal)) return;
            _speechShown[passenger] = shown;

            if (shown == null)
            {
                if (label.gameObject.activeSelf) label.gameObject.SetActive(false);
                return;
            }

            label.text = shown;
            // 배킹판을 새 문자열 길이에 맞춘다. 문자열이 바뀔 때만 도는 자리라
            // `ForceMeshUpdate` 를 여기서 한 번 부르는 비용은 매 프레임이 아니다.
            BuildLabelPlate.Measure(label, out Vector2 center, out Vector2 half);
            BuildLabelPlate.Attach(label, _plateMaterial, BuildLabelPlate.Find(label), center, half);
            _layout.UpdatePlate(label.GetComponent<Renderer>(), center, half);
            if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
        }

        /// <summary>
        /// 시선 대상을 회전으로 바꾼다. 대상이 배선되지 않았으면 회전하지 않는다 —
        /// 없는 곳을 보게 만드는 것보다 안 보는 편이 낫다.
        /// 몸 전체를 돌리되 **수평 성분만** 쓴다. 위아래로 꺾인 승객은 사람으로 읽히지 않는다.
        /// </summary>
        private Quaternion GazeRotation(Npc.ReactionGaze gaze, Vector3 from)
        {
            Transform target;
            switch (gaze)
            {
                case Npc.ReactionGaze.Device:           target = _gazeDevice; break;
                case Npc.ReactionGaze.OverharvestLever: target = _gazeOverharvestLever; break;
                case Npc.ReactionGaze.Door:             target = _gazeDoor; break;
                case Npc.ReactionGaze.Ceiling:          target = _gazeCeiling; break;
                case Npc.ReactionGaze.Player:           target = _head; break;
                default:                                return Quaternion.identity;
            }

            if (gaze == Npc.ReactionGaze.Ceiling && target == null)
            {
                // 천장은 배선이 없어도 성립한다 — 머리 위는 어디서든 위다.
                return Quaternion.Euler(-28f, 0f, 0f);
            }
            if (target == null) return Quaternion.identity;

            Vector3 delta = target.position - from;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.0004f) return Quaternion.identity;
            return Quaternion.LookRotation(delta.normalized, Vector3.up);
        }

        /// <summary>
        /// SRP 콜백이 돌지 않는 경로(내장 파이프라인·에디터 정지 상태)를 위한 예비 배치.
        ///
        /// 크기 규칙은 `BuildLabelGeometry` 로 옮겼을 뿐 값이 같다 — 기준 거리 1.8m,
        /// 하한 0.35. **하한을 더 내리지 않는다**(`TOPDOWN_MASTER_BACKLOG.md` §5.1:
        /// 독립 평가 둘이 「축이 틀렸다」고 판정했고, 더 내리면 「가리는 면적은 그대로인
        /// 채 의미만 없는 얼룩」이 된다).
        ///
        /// 3차 감사가 증거 영상에서 「문상객」 라벨이 **프레임의 54%** 에서 대표
        /// 오브젝트를 덮는다고 지적했고, 시각 평가도 같은 것을 봤다
        /// (`10_risk_critical` 에서 「광신자」가 프레임 좌측 22% 를 차지하며
        /// 결과판 첫 열의 획 위를 지나간다). 그 상한은 그대로 둔다.
        /// </summary>
        private void FaceReader()
        {
            // 카메라가 그리기 직전에 이미 잡았으면 다시 하지 않는다.
            if (_solvedFrame >= Time.frameCount - 1) return;

            if (_headCamera == null)
            {
                // 한 번만 찾는다. 못 찾았을 때 매 프레임 다시 찾으면 카메라가 없는 씬에서
                // 영구적으로 전역 탐색이 돈다.
                if (_searchedHead) return;
                _searchedHead = true;
                Camera camera = Camera.main;
                if (camera == null) return;
                _head = camera.transform;
                _headCamera = camera;
            }

            EnsureObstacles();
            _layout.Solve(_headCamera);
        }

        private void Rebuild()
        {
            ClearFigures();

            RunSession run = _run != null ? _run.Session : null;
            if (run == null) return;

            Vector3 carOrigin = _carAnchor != null ? _carAnchor.position : Vector3.zero;
            Vector3 lobbyOrigin = _lobbyAnchor != null ? _lobbyAnchor.position : Vector3.zero;

            // 실려 있는 것 — 엘리베이터 안 둘레에 선다.
            BuildLoadout loadout = run.Loadout;
            if (loadout != null)
            {
                for (int i = 0; i < loadout.Count && i < CarSlots.Length; i++)
                {
                    BuildItem item = loadout.Items[i];
                    Vector3 position = carOrigin + CarSlots[i];
                    GameObject figure = CreateFigure(item, position, false, -1);
                    _carFigures.Add(figure);
                    _carBasePositions.Add(position);
                    bool isPassenger = item.Kind == BuildItemKind.Passenger;
                    _carIsPassenger.Add(isPassenger);
                    if (isPassenger)
                    {
                        // 승객 번호는 부품을 건너뛰고 이어진다. 중재기가 "0번 승객"이라고
                        // 말할 때 그것이 궤짝을 가리키면 안 된다.
                        _passengerSlots.Add(_carFigures.Count - 1);
                        _slotReactions.Add(default(SlotReaction));
                        _speechLabels.Add(AddSpeechLabel(figure.transform));
                        _speechShown.Add(null);
                    }
                }
            }

            // 후보 — 문 너머 어두운 승강장에 서 있다.
            FloorSession floor = run.Current;
            if (floor == null || floor.Phase != FloorPhase.Boarding) return;

            for (int i = 0; i < floor.BuildOffers.Count && i < LobbySlots.Length; i++)
            {
                BuildItem item = floor.BuildOffers[i];
                GameObject figure = CreateFigure(item, lobbyOrigin + LobbySlots[i], true, i);
                _lobbyFigures.Add(figure);
            }
        }

        private void ClearFigures()
        {
            for (int i = 0; i < _candidates.Count; i++)
                if (_candidates[i] != null) _candidates[i].Picked -= OnCandidatePicked;
            _candidates.Clear();
            _carBasePositions.Clear();
            _carIsPassenger.Clear();
            _passengerSlots.Clear();
            _slotReactions.Clear();
            // 라벨은 이제 승객의 자식이 **아니므로** 여기서 직접 지운다. 남겨 두면
            // 파괴된 승객을 따라다니려다 다음 프레임에 「이미 파괴된 오브젝트」 예외가 난다.
            _speechLabels.Clear();
            _speechShown.Clear();
            _labelRenderers.Clear();
            _layout.Clear();
            DestroyAll(_labelHolders);
            DestroyAll(_carFigures);
            DestroyAll(_lobbyFigures);
        }

        private static void DestroyAll(List<GameObject> objects)
        {
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] == null) continue;
                if (Application.isPlaying) Destroy(objects[i]);
                else DestroyImmediate(objects[i]);
            }
            objects.Clear();
        }

        private void OnCandidatePicked(int index)
        {
            if (_run == null) return;
            _run.TakeBuildOffer(index);
            // 서명이 바뀌므로 다음 LateUpdate 가 다시 세운다.
        }

        // ── 형태 ────────────────────────────────────────────────────────────
        //
        // 각진 상자 몇 개로만 만든다(§4 "단순하고 각진 로우폴리", "큰 실루엣").
        // 승객은 다리·몸통·머리 세 덩어리로 사람 실루엣을, 부품은 궤짝 실루엣을 갖는다 —
        // 색을 빼도 둘이 구분되어야 한다.

        private GameObject CreateFigure(BuildItem item, Vector3 position, bool isCandidate, int index)
        {
            var root = new GameObject(isCandidate ? $"Candidate_{item.Id}" : $"Load_{item.Id}");
            root.transform.SetParent(transform, false);
            root.transform.position = position;

            Material material = isCandidate ? CandidateMaterial()
                : item.Kind == BuildItemKind.Passenger ? PassengerMaterial() : PartMaterial();

            float height;
            if (item.Kind == BuildItemKind.Passenger)
            {
                AddBox(root.transform, "Legs",  new Vector3(0f, 0.21f, 0f), new Vector3(0.30f, 0.42f, 0.22f), material);
                AddBox(root.transform, "Torso", new Vector3(0f, 0.86f, 0f), new Vector3(0.38f, 0.88f, 0.26f), material);
                AddBox(root.transform, "Head",  new Vector3(0f, 1.43f, 0f), new Vector3(0.22f, 0.26f, 0.22f), material);
                height = PassengerHeight;
            }
            else
            {
                AddBox(root.transform, "Crate", new Vector3(0f, 0.28f, 0f), new Vector3(0.56f, 0.56f, 0.50f), material);
                AddBox(root.transform, "Lid",   new Vector3(0f, 0.62f, 0f), new Vector3(0.38f, 0.12f, 0.34f), material);
                height = 0.68f;
            }

            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, height * 0.5f, 0f);
            collider.size = new Vector3(0.6f, height, 0.55f);

            AddLabel(root.transform, item, height, isCandidate);

            if (isCandidate)
            {
                var candidate = root.AddComponent<InteractableBuildCandidate>();
                candidate.Configure(index, $"탑승 — {item.Label} ({item.Weight:F0}kg)");
                candidate.Picked += OnCandidatePicked;
                _candidates.Add(candidate);
            }
            return root;
        }

        private static void AddBox(Transform parent, string name, Vector3 localPosition,
            Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = size;

            Collider primitiveCollider = box.GetComponent<Collider>();
            if (primitiveCollider != null)
            {
                // 조준은 루트 콜라이더 하나로 받는다. 자식마다 콜라이더가 있으면
                // 레이가 어느 것을 맞췄는지에 따라 프롬프트가 흔들린다.
                if (Application.isPlaying) Destroy(primitiveCollider);
                else DestroyImmediate(primitiveCollider);
            }

            var renderer = box.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        private void AddLabel(Transform follow, BuildItem item, float height, bool isCandidate)
        {
            float rise = height + 0.24f;
            var holder = new GameObject("Label");
            // **승객이 아니라 이 컴포넌트의 자식이다.** 반응 자세가 승객 루트에 거는
            // 비균등 스케일을 이름표가 물려받으면 웅크릴 때 글자가 눌리고, 배킹판
            // 크기 계산도 그 스케일을 다시 타고 들어가 어긋난다. 위치만 따라간다.
            holder.transform.SetParent(transform, false);
            holder.transform.position = follow.position + Vector3.up * rise;

            var text = holder.AddComponent<TextMeshPro>();
            text.text = isCandidate
                ? $"{item.Label}\n<size=60%>{item.Weight:F0}kg · {ShortEffect(item)}</size>"
                : item.Label;
            text.fontSize = 1.6f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = isCandidate ? new Color(0.86f, 0.84f, 0.70f) : new Color(0.62f, 0.65f, 0.60f);

            var rect = holder.GetComponent<RectTransform>();
            if (rect != null) rect.sizeDelta = new Vector2(1.6f, 0.7f);

            // 라벨은 그리는 카메라를 향해 돈다(`OnBeginCameraRendering`). 단면 재질은
            // 그래도 남긴다 — 콜백이 닿지 않는 카메라(미리보기·반사)에서 뒷면은 정의상
            // 거울상이고, 거꾸로 보이는 것보다는 안 보이는 편이 낫다.
            Material single = SingleSided(text.fontSharedMaterial);
            if (single != null) text.fontSharedMaterial = single;

            RegisterLabel(holder, text, follow, rise);
        }

        /// <summary>
        /// 라벨 하나를 배치기에 넘긴다. 배킹판을 이 시점에 만들어 **글자 크기에 맞춘다** —
        /// 렉트(이름표 1.6m)에 맞추면 세 글자 이름 뒤에 널빤지가 선다.
        /// </summary>
        private void RegisterLabel(GameObject holder, TMP_Text text, Transform follow, float rise)
        {
            _plateMaterial ??= BuildLabelPlate.CreateMaterial();
            BuildLabelPlate.Measure(text, out Vector2 center, out Vector2 half);
            Renderer plate = BuildLabelPlate.Attach(text, _plateMaterial, null, center, half);
            var textRenderer = text.GetComponent<Renderer>();

            _labelHolders.Add(holder);
            if (textRenderer != null) _labelRenderers.Add(textRenderer);
            if (plate != null) _labelRenderers.Add(plate);
            _layout.Add(holder.transform, follow, rise, textRenderer, plate, center, half);
        }

        /// <summary>
        /// 승객 하나의 대사 라벨을 만든다. 처음에는 꺼 둔다 — 빈 문자열로 켜 두면
        /// TMP 가 매 프레임 빈 메시를 그리고, 무엇보다 "대사가 없다"와 "대사 시스템이
        /// 없다"가 화면에서 같아 보인다.
        ///
        /// 배치기(`BuildLabelLayout`)에도 등록한다. 그래야 이름표와 **같은 규칙**으로
        /// 돌고·당겨지고·크기가 잡히고·양보한다 — 대사만 따로 처리하면 이름표는
        /// 작아지는데 대사는 화면을 덮는 상태가 된다(예전 `FaceReader` 주석이 적은 실패다).
        /// </summary>
        private TMP_Text AddSpeechLabel(Transform follow)
        {
            float rise = PassengerHeight + 0.24f + SpeechLabelRise;
            var holder = new GameObject("Speech");
            holder.transform.SetParent(transform, false);
            holder.transform.position = follow.position + Vector3.up * rise;

            var text = holder.AddComponent<TextMeshPro>();
            text.text = string.Empty;
            text.fontSize = 1.25f;
            text.alignment = TextAlignmentOptions.Center;
            // 이름표(회녹색)보다 따뜻하게 둔다. 색을 빼도 크기가 달라 구분되지만,
            // 같은 색이면 "누구인가"와 "무슨 말인가"가 한 덩어리로 읽힌다.
            text.color = new Color(0.90f, 0.84f, 0.58f);

            var rect = holder.GetComponent<RectTransform>();
            if (rect != null) rect.sizeDelta = new Vector2(2.2f, 0.6f);

            // 이름표와 같은 이유로 단면 재질을 쓴다 — 콜백이 닿지 않는 카메라에서 거울상이 나온다.
            Material single = SingleSided(text.fontSharedMaterial);
            if (single != null) text.fontSharedMaterial = single;

            RegisterLabel(holder, text, follow, rise);
            holder.SetActive(false);
            return text;
        }

        /// <summary>
        /// 폰트 재질의 단면 변형. 폰트마다 한 번만 만들고 재사용한다 — 라벨마다
        /// 새로 만들면 배치 하나에 재질 인스턴스가 여섯 개씩 쌓인다.
        /// </summary>
        private static Material SingleSided(Material source)
        {
            if (source == null || !source.HasProperty(CullModeId)) return null;
            if (Mathf.Approximately(source.GetFloat(CullModeId), 2f)) return source;

            if (_singleSided.TryGetValue(source, out Material cached)) return cached;
            var made = new Material(source) { name = source.name + " SingleSided (runtime)" };
            made.SetFloat(CullModeId, 2f);   // Back
            _singleSided[source] = made;
            return made;
        }

        private static string ShortEffect(BuildItem item)
        {
            string summary = item.EffectSummary();
            if (string.IsNullOrEmpty(summary)) return "규칙 변경 없음";
            return summary.Length <= 28 ? summary : summary.Substring(0, 27) + "…";
        }

        // ── 재질 ────────────────────────────────────────────────────────────
        //
        // 런타임 인스턴스로 만든다. `.mat` 에셋은 단일 소유 직렬화 파일이라
        // 병렬 작업에서 손상 위험이 있고, 이 형태는 아직 플레이스홀더다.

        private Material PassengerMaterial() =>
            _passengerMaterial ??= MakeMaterial(new Color(0.44f, 0.47f, 0.42f));

        private Material PartMaterial() =>
            _partMaterial ??= MakeMaterial(new Color(0.42f, 0.34f, 0.26f));

        private Material CandidateMaterial() =>
            _candidateMaterial ??= MakeMaterial(new Color(0.30f, 0.33f, 0.31f));

        private static Material MakeMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "BuildFigure_" + ColorUtility.ToHtmlStringRGB(color) };
            material.color = color;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.06f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            return material;
        }
    }
}
