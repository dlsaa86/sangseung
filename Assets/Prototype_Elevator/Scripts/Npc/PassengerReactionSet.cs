using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ascend.Prototype.Npc
{
    /// <summary>
    /// 반응 정의 묶음. `MASTER_PRD.md` §9.4 「반응은 `PassengerReactionSet` 데이터로
    /// 이벤트별 교체 가능」이 이 에셋을 이름까지 지정한 요구다(UP-NPC-03).
    ///
    /// 왜 ScriptableObject인가: 최종 모션·대사·음성은 승인 대기 항목이라(§8, `VISUAL_SPEC` §11)
    /// 하나로 잠글 수 없다. 프로파일을 여러 개 두고 인스펙터에서 바꿔 끼우는 것이
    /// "코드 수정 없이 반영"의 유일한 형태다.
    ///
    /// 배열로 두고 선형 탐색하는 이유: 항목이 11개다. 사전을 만들면 직렬화되지 않는
    /// 캐시를 도메인 리로드마다 다시 지어야 하고, 그 복잡도가 11회 비교보다 비싸다.
    /// </summary>
    [CreateAssetMenu(fileName = "PassengerReactionSet",
                     menuName = "Ascend/Profiles/Passenger Reaction Set", order = 107)]
    public sealed class PassengerReactionSet : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("어떤 사건에 대한 반응인가.")]
            public PassengerReactionEvent Event;

            public PassengerReaction Reaction;

            public Entry(PassengerReactionEvent kind, PassengerReaction reaction)
            {
                Event = kind;
                Reaction = reaction;
            }
        }

        [Tooltip("이벤트별 반응. 비어 있거나 항목이 없으면 코드 기본값으로 폴백한다.")]
        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

        public IReadOnlyList<Entry> Entries => _entries ?? Array.Empty<Entry>();

        /// <summary>
        /// 이 사건의 반응. 항목이 없으면 <see cref="DefaultFor"/>로 폴백한다.
        ///
        /// 폴백이 있어야 하는 이유: 에셋 하나가 비었다고 승객이 통째로 얼어붙으면
        /// "반응이 정의되지 않았다"와 "반응 배선이 끊겼다"가 화면에서 같아 보인다.
        /// 기본값이 나오면 최소한 배선은 살아 있다는 증거가 된다.
        /// </summary>
        public PassengerReaction For(PassengerReactionEvent reactionEvent)
        {
            if (reactionEvent == PassengerReactionEvent.None) return default(PassengerReaction);

            if (_entries != null)
            {
                for (int i = 0; i < _entries.Length; i++)
                    if (_entries[i].Event == reactionEvent) return _entries[i].Reaction;
            }

            return DefaultFor(reactionEvent);
        }

        /// <summary>이 사건이 에셋에 명시돼 있는가. 폴백과 실제 데이터를 구분해야 하는 검증용이다.</summary>
        public bool HasEntry(PassengerReactionEvent reactionEvent)
        {
            if (_entries == null) return false;
            for (int i = 0; i < _entries.Length; i++)
                if (_entries[i].Event == reactionEvent) return true;
            return false;
        }

        /// <summary>
        /// 11종 전부를 기본값으로 채운다. Unity가 에셋 생성·인스펙터 Reset에서 부른다.
        ///
        /// **처음부터 전부 채워 둔다.** §9.4의 "이벤트별 교체 가능"은 편집자가 빈 배열을
        /// 보고 11개를 손으로 만들어 넣는 것이 아니라, 이미 있는 값을 고치는 것이어야
        /// 성립한다. 빈 에셋은 폴백 덕분에 조용히 동작하므로 "아직 안 채웠다"를
        /// 아무도 눈치채지 못한다.
        /// </summary>
        public void Reset()
        {
            IReadOnlyList<PassengerReactionEvent> all = PassengerReactionEvents.All;
            var made = new Entry[all.Count];
            for (int i = 0; i < all.Count; i++) made[i] = new Entry(all[i], DefaultFor(all[i]));
            _entries = made;
        }

        /// <summary>
        /// 항목을 통째로 비운다. 그 뒤의 모든 조회는 <see cref="DefaultFor"/>로 폴백한다.
        ///
        /// 왜 필요한가: 에디터에서 <c>ScriptableObject.CreateInstance</c>는 <see cref="Reset"/>을
        /// 함께 부른다. 그래서 "갓 만든 세트는 비어 있다"를 전제로 폴백을 검사하면
        /// **검사 자체가 성립하지 않는다** — 실제로 그 전제로 쓴 테스트가 실패했다.
        /// 폴백은 부분 정의 에셋(편집자가 몇 개만 지운 상태)에서도 걸려야 하는 동작이므로,
        /// 그 상태를 만들 수 있는 통로가 코드에 있어야 검증할 수 있다.
        /// </summary>
        public void Clear() => _entries = Array.Empty<Entry>();

        /// <summary>
        /// 코드 기본값 스냅샷. 밸런스가 아니라 **판독 순서**를 담은 값이다 —
        /// 우선순위는 `MASTER_PRD.md` §6.1이 정한 강조 순서를 따른다. 붕괴가 가장 위고,
        /// 가장 흔한 기본 정화가 가장 아래다. 흔한 것이 우선하면 드문 순간이 묻힌다.
        ///
        /// 지속·쿨다운은 임시값이다(`ASSUMPTION_LOG` 대상). 다만 쿨다운은 항상 지속보다
        /// 길게 둔다 — 짧으면 같은 승객이 반응이 끝나자마자 다시 반응해서
        /// "한 사람만 계속 말하는" 그림이 된다(§9.4가 금지하는 것과 같은 실패).
        /// </summary>
        public static PassengerReaction DefaultFor(PassengerReactionEvent reactionEvent)
        {
            switch (reactionEvent)
            {
                // 관심. 아직 아무 일도 일어나지 않았으므로 가장 작다.
                case PassengerReactionEvent.ContractChosen:
                    return new PassengerReaction(ReactionPose.Lean, ReactionGaze.Device,
                        "npc_murmur_low", 1.6f, 0.30f, 10, 6.0f);

                // 가장 흔한 성공. 여기가 크면 5연쇄와 임계점이 커 보이지 않는다.
                case PassengerReactionEvent.BasicPurify:
                    return new PassengerReaction(ReactionPose.Lean, ReactionGaze.Device,
                        "npc_relief_short", 0.9f, 0.22f, 5, 4.0f);

                case PassengerReactionEvent.FiveChain:
                    return new PassengerReaction(ReactionPose.Cheer, ReactionGaze.Device,
                        "npc_awe", 2.2f, 0.70f, 40, 8.0f);

                // 살아서 내릴 수 있게 된 순간. 환호지만 300%보다는 작다.
                case PassengerReactionEvent.Threshold100:
                    return new PassengerReaction(ReactionPose.Cheer, ReactionGaze.Device,
                        "npc_relief_long", 1.8f, 0.55f, 35, 8.0f);

                // 여기서부터는 기쁨이 아니라 "이걸 더 해도 되나"가 된다.
                case PassengerReactionEvent.Threshold170:
                    return new PassengerReaction(ReactionPose.Stare, ReactionGaze.Device,
                        "npc_gasp", 1.8f, 0.65f, 45, 8.0f);

                case PassengerReactionEvent.Threshold300:
                    return new PassengerReaction(ReactionPose.Brace, ReactionGaze.Device,
                        "npc_awe_deep", 2.4f, 0.85f, 55, 10.0f);

                // 레버 덮개가 열렸다. 시선이 장치에서 레버로 옮겨가는 첫 순간이다.
                case PassengerReactionEvent.OverharvestUnlocked:
                    return new PassengerReaction(ReactionPose.Stare, ReactionGaze.OverharvestLever,
                        "npc_murmur_rise", 2.0f, 0.50f, 50, 8.0f);

                // §7의 대표 장면. 아직 당기지 않았으므로 소리는 숨을 멈추는 쪽이다.
                case PassengerReactionEvent.OverharvestApproach:
                    return new PassengerReaction(ReactionPose.Brace, ReactionGaze.OverharvestLever,
                        "npc_breath_hold", 2.6f, 0.80f, 70, 6.0f);

                case PassengerReactionEvent.ExtraSpin:
                    return new PassengerReaction(ReactionPose.Cower, ReactionGaze.OverharvestLever,
                        "npc_yelp", 2.2f, 0.90f, 75, 7.0f);

                // 가장 높은 우선순위. 무엇이 진행 중이든 이건 덮어써야 한다.
                case PassengerReactionEvent.CollapseImminent:
                    return new PassengerReaction(ReactionPose.Cower, ReactionGaze.Ceiling,
                        "npc_scream_short", 3.0f, 1.00f, 95, 5.0f);

                // 결과. 붕괴 다음으로 높다 — 층이 끝났는데 5연쇄에 환호하고 있으면 안 된다.
                case PassengerReactionEvent.AccidentOrSuccess:
                    return new PassengerReaction(ReactionPose.Idle, ReactionGaze.Door,
                        "npc_sigh", 2.4f, 0.45f, 60, 6.0f);

                default:
                    return default(PassengerReaction);
            }
        }
    }
}

// 씬 배선 필요:
// 1) `Assets/Prototype_Elevator/Data/PassengerReactionSet.asset` 을 만든다
//    (Create ▸ Ascend ▸ Profiles ▸ Passenger Reaction Set). 생성 시 Reset() 이 11종을 채운다.
//    이 파일을 만들지 않아도 코드 기본값으로 동작하지만, 그러면 §9.4의 "코드 수정 없이 교체"가
//    성립하지 않는다.
// 2) 반응을 실제 자세·시선·소리로 옮기는 MonoBehaviour 가 아직 없다(UP-NPC-04).
//    현재 `Scripts/Build/BuildFigureView.ReactToRisk` 가 위험 단계만 보고 자세를 만든다 —
//    그쪽이 `PassengerReactionDirector.CurrentOf(i)` 를 읽도록 잇는 것이 다음 단계다.
