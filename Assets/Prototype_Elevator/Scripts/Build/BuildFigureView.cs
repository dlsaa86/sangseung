using System.Collections.Generic;
using TMPro;
using UnityEngine;
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
        private readonly List<TMP_Text> _labels = new List<TMP_Text>();

        private Material _passengerMaterial;
        private Material _partMaterial;
        private Material _candidateMaterial;
        private Risk.RiskStateView _risk;
        private Transform _head;
        private int _signature = int.MinValue;
        private int _doorPromptKey = int.MinValue;

        /// <summary>
        /// `RiskStateView`·`Camera.main` 탐색을 매 프레임 반복하지 않기 위한 표식.
        /// 씬에 대상이 없으면 `null` 검사만으로는 영구히 매 프레임 전역 탐색이 돈다.
        /// </summary>
        private bool _searchedRisk;
        private bool _searchedHead;

        private void Awake()
        {
            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();
            if (_doorControl == null) _doorControl = FindAnyObjectByType<InteractableDoorControl>();
            if (_doorControl != null) _doorControl.onDepart.AddListener(OnDepart);
            if (_run != null) _run.RunStarted += OnRunStarted;

            Camera camera = Camera.main;
            if (camera != null) _head = camera.transform;
        }

        private void OnDestroy()
        {
            if (_doorControl != null) _doorControl.onDepart.RemoveListener(OnDepart);
            if (_run != null) _run.RunStarted -= OnRunStarted;
            DestroyMaterial(ref _passengerMaterial);
            DestroyMaterial(ref _partMaterial);
            DestroyMaterial(ref _candidateMaterial);
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
        /// </summary>
        private void ReactToRisk()
        {
            if (_carFigures.Count == 0) return;
            if (_risk == null)
            {
                if (_searchedRisk) return;
                _searchedRisk = true;
                _risk = FindAnyObjectByType<Risk.RiskStateView>();
                if (_risk == null) return;
            }

            float amplitude;
            float speed;
            switch (_risk.Level)
            {
                case Risk.RiskLevel.Warning:  amplitude = 0.006f; speed = 3.2f;  break;
                case Risk.RiskLevel.Critical: amplitude = 0.017f; speed = 9.5f;  break;
                case Risk.RiskLevel.Collapse: amplitude = 0.020f; speed = 14.0f; break;
                default:                      amplitude = 0f;     speed = 0f;    break;
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
                    continue;
                }

                // 위상을 자리마다 어긋나게 둔다. 전원이 같은 박자로 흔들리면
                // 사람이 아니라 하나의 기계로 읽힌다.
                float phase = i * 1.7f;
                float sway = Mathf.Sin(time * speed + phase) * amplitude;
                float bob = Mathf.Sin(time * speed * 0.63f + phase) * amplitude * 0.5f;
                figure.transform.position = basePosition + new Vector3(sway, bob, sway * 0.6f);
            }
        }

        /// <summary>라벨이 플레이어를 본다. 정면에서 읽히지 않으면 배치의 뜻이 사라진다.</summary>
        private void FaceReader()
        {
            if (_head == null)
            {
                // 한 번만 찾는다. 못 찾았을 때 매 프레임 다시 찾으면 카메라가 없는 씬에서
                // 영구적으로 전역 탐색이 돈다.
                if (_searchedHead) return;
                _searchedHead = true;
                Camera camera = Camera.main;
                if (camera == null) return;
                _head = camera.transform;
            }

            for (int i = 0; i < _labels.Count; i++)
            {
                TMP_Text label = _labels[i];
                if (label == null) continue;
                Vector3 toReader = label.transform.position - _head.position;
                toReader.y = 0f;
                if (toReader.sqrMagnitude > 0.0001f)
                    label.transform.rotation = Quaternion.LookRotation(toReader, Vector3.up);
            }
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
                    _carIsPassenger.Add(item.Kind == BuildItemKind.Passenger);
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
            _labels.Clear();
            _carBasePositions.Clear();
            _carIsPassenger.Clear();
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
                height = 1.56f;
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

        private void AddLabel(Transform parent, BuildItem item, float height, bool isCandidate)
        {
            var holder = new GameObject("Label");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = new Vector3(0f, height + 0.24f, 0f);

            var text = holder.AddComponent<TextMeshPro>();
            text.text = isCandidate
                ? $"{item.Label}\n<size=60%>{item.Weight:F0}kg · {ShortEffect(item)}</size>"
                : item.Label;
            text.fontSize = 1.6f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = isCandidate ? new Color(0.86f, 0.84f, 0.70f) : new Color(0.62f, 0.65f, 0.60f);

            var rect = holder.GetComponent<RectTransform>();
            if (rect != null) rect.sizeDelta = new Vector2(1.6f, 0.7f);

            _labels.Add(text);
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
