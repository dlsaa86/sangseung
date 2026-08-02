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

            [Tooltip("같은 사건에 대한 상반된 반응(§9.3). 비워 두면 전원이 같은 반응을 한다.")]
            public PassengerReactionContrast Contrast;

            public Entry(PassengerReactionEvent kind, PassengerReaction reaction)
                : this(kind, reaction, default(PassengerReactionContrast))
            {
            }

            public Entry(PassengerReactionEvent kind, PassengerReaction reaction,
                         PassengerReactionContrast contrast)
            {
                Event = kind;
                Reaction = reaction;
                Contrast = contrast;
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
                {
                    if (_entries[i].Event != reactionEvent) continue;

                    PassengerReaction reaction = _entries[i].Reaction;

                    // **필드 단위 폴백은 대사에만 건다.** `Line` 은 이 에셋이 이미
                    // 직렬화된 뒤에 생긴 필드라서, 기존 `.asset` 의 11개 항목은 전부
                    // 빈 문자열로 읽힌다. 그 상태를 "대사 없음"으로 읽으면 §9.3의 채널
                    // 하나가 데이터가 있는데도 통째로 사라지고, 화면에서는 "대사를
                    // 구현하지 않았다"와 구분되지 않는다.
                    //
                    // `VoiceCue` 에는 걸지 않는다 — 그쪽의 "비면 소리 없음"은 이미
                    // 출하된 계약이고, 뒤늦게 폴백을 넣으면 데이터로 음성을 끈 항목이
                    // 다시 울리기 시작한다.
                    if (string.IsNullOrEmpty(reaction.Line))
                        reaction.Line = DefaultFor(reactionEvent).Line;

                    return reaction;
                }
            }

            return DefaultFor(reactionEvent);
        }

        /// <summary>
        /// 이 사건의 **상반된 반응**(§9.3). 항목이 없거나 아직 채우지 않았으면
        /// <see cref="DefaultContrastFor"/>로 폴백한다 — <see cref="For"/>의 대사 폴백과 같은 이유다.
        /// </summary>
        public PassengerReactionContrast ContrastFor(PassengerReactionEvent reactionEvent)
        {
            if (reactionEvent == PassengerReactionEvent.None)
                return default(PassengerReactionContrast);

            if (_entries != null)
            {
                for (int i = 0; i < _entries.Length; i++)
                {
                    if (_entries[i].Event != reactionEvent) continue;
                    return _entries[i].Contrast.IsDefined
                        ? _entries[i].Contrast
                        : DefaultContrastFor(reactionEvent);
                }
            }

            return DefaultContrastFor(reactionEvent);
        }

        /// <summary>이 사건이 에셋에 명시돼 있는가. 폴백과 실제 데이터를 구분해야 하는 검증용이다.</summary>
        public bool HasEntry(PassengerReactionEvent reactionEvent)
        {
            if (_entries == null) return false;
            for (int i = 0; i < _entries.Length; i++)
                if (_entries[i].Event == reactionEvent) return true;
            return false;
        }

        /// <summary>대조 반응이 에셋에 실제로 채워져 있는가. 폴백과 데이터를 가르는 검증용이다.</summary>
        public bool HasContrastEntry(PassengerReactionEvent reactionEvent)
        {
            if (_entries == null) return false;
            for (int i = 0; i < _entries.Length; i++)
                if (_entries[i].Event == reactionEvent) return _entries[i].Contrast.IsDefined;
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
            for (int i = 0; i < all.Count; i++)
                made[i] = new Entry(all[i], DefaultFor(all[i]), DefaultContrastFor(all[i]));
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
        /// 항목을 통째로 갈아 끼운다. <see cref="Clear"/>의 일반형이다.
        ///
        /// 필요한 이유도 <see cref="Clear"/>와 같다 — **부분 정의 상태를 만들 통로**가
        /// 없으면 폴백을 검증할 수 없다. 특히 디스크의 `PassengerReactionSet.asset` 은
        /// 항목은 11종 다 있는데 `Line` 필드만 직렬화되지 않은 상태다(그 필드가 에셋보다
        /// 늦게 생겼다). 그 조합은 <see cref="Reset"/>으로도 <see cref="Clear"/>로도
        /// 만들 수 없다. 실제 출하 상태를 재현하지 못하는 검사는 그 상태를 지켜 주지 못한다.
        /// </summary>
        public void ReplaceEntries(Entry[] entries)
            => _entries = entries ?? Array.Empty<Entry>();

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
                        "npc_murmur_low", "그걸 정말 싣겠다고?", 1.6f, 0.30f, 10, 6.0f);

                // 가장 흔한 성공. 여기가 크면 5연쇄와 임계점이 커 보이지 않는다.
                case PassengerReactionEvent.BasicPurify:
                    return new PassengerReaction(ReactionPose.Lean, ReactionGaze.Device,
                        "npc_relief_short", "하나 걷혔다.", 0.9f, 0.22f, 5, 4.0f);

                case PassengerReactionEvent.FiveChain:
                    return new PassengerReaction(ReactionPose.Cheer, ReactionGaze.Device,
                        "npc_awe", "멈추질 않아!", 2.2f, 0.70f, 40, 8.0f);

                // 살아서 내릴 수 있게 된 순간. 환호지만 300%보다는 작다.
                case PassengerReactionEvent.Threshold100:
                    return new PassengerReaction(ReactionPose.Cheer, ReactionGaze.Device,
                        "npc_relief_long", "이제 내려도 되는 거지?", 1.8f, 0.55f, 35, 8.0f);

                // 여기서부터는 기쁨이 아니라 "이걸 더 해도 되나"가 된다.
                case PassengerReactionEvent.Threshold170:
                    return new PassengerReaction(ReactionPose.Stare, ReactionGaze.Device,
                        "npc_gasp", "여기서 그만하자.", 1.8f, 0.65f, 45, 8.0f);

                case PassengerReactionEvent.Threshold300:
                    return new PassengerReaction(ReactionPose.Brace, ReactionGaze.Device,
                        "npc_awe_deep", "이건 사람이 만질 게 아니야.", 2.4f, 0.85f, 55, 10.0f);

                // 레버 덮개가 열렸다. 시선이 장치에서 레버로 옮겨가는 첫 순간이다.
                case PassengerReactionEvent.OverharvestUnlocked:
                    return new PassengerReaction(ReactionPose.Stare, ReactionGaze.OverharvestLever,
                        "npc_murmur_rise", "덮개가 열렸어.", 2.0f, 0.50f, 50, 8.0f);

                // §7의 대표 장면. 아직 당기지 않았으므로 소리는 숨을 멈추는 쪽이다.
                case PassengerReactionEvent.OverharvestApproach:
                    return new PassengerReaction(ReactionPose.Brace, ReactionGaze.OverharvestLever,
                        "npc_breath_hold", "손 떼.", 2.6f, 0.80f, 70, 6.0f);

                case PassengerReactionEvent.ExtraSpin:
                    return new PassengerReaction(ReactionPose.Cower, ReactionGaze.OverharvestLever,
                        "npc_yelp", "당겼어, 당겼다고!", 2.2f, 0.90f, 75, 7.0f);

                // 가장 높은 우선순위. 무엇이 진행 중이든 이건 덮어써야 한다.
                case PassengerReactionEvent.CollapseImminent:
                    return new PassengerReaction(ReactionPose.Cower, ReactionGaze.Ceiling,
                        "npc_scream_short", "천장이 내려온다!", 3.0f, 1.00f, 95, 5.0f);

                // 결과. 붕괴 다음으로 높다 — 층이 끝났는데 5연쇄에 환호하고 있으면 안 된다.
                case PassengerReactionEvent.AccidentOrSuccess:
                    return new PassengerReaction(ReactionPose.Idle, ReactionGaze.Door,
                        "npc_sigh", "끝난 건가.", 2.4f, 0.45f, 60, 6.0f);

                default:
                    return default(PassengerReaction);
            }
        }

        /// <summary>
        /// 대조 반응의 코드 기본값. `MASTER_PRD.md` §9.3이 요구하는 "같은 사건에 승객마다
        /// 상반된 반응"을 데이터가 비었을 때도 성립시키는 값이다.
        ///
        /// **감정의 방향이 반대여야 한다.** 자세만 살짝 다른 두 반응은 정지 화면에서
        /// 같은 그림이고, 그러면 이 채널은 있으나 마나다 — 5연쇄에 한 사람이 환호할 때
        /// 다른 사람은 천장을 붙잡고 버텨야 "같은 사건을 다르게 겪는 방"이 된다.
        ///
        /// 대사도 같은 원칙이다. 「멈추질 않아!」와 「이건 너무 많아」는 같은 사실을
        /// 반대로 읽은 문장이다.
        /// </summary>
        public static PassengerReactionContrast DefaultContrastFor(PassengerReactionEvent reactionEvent)
        {
            switch (reactionEvent)
            {
                // 관심 ↔ 의심. 시선이 장치가 아니라 **결정한 사람**을 향한다.
                case PassengerReactionEvent.ContractChosen:
                    return new PassengerReactionContrast(ReactionPose.Stare, ReactionGaze.Player,
                        "npc_murmur_doubt", "누가 결정했지?", 0.35f);

                // 안도 ↔ 냉담.
                case PassengerReactionEvent.BasicPurify:
                    return new PassengerReactionContrast(ReactionPose.Stare, ReactionGaze.Device,
                        "npc_tsk", "겨우 하나야.", 0.20f);

                // 환호 ↔ 공포. 이 프로토타입에서 가장 크게 갈리는 한 쌍이다.
                case PassengerReactionEvent.FiveChain:
                    return new PassengerReactionContrast(ReactionPose.Brace, ReactionGaze.Ceiling,
                        "npc_gasp", "이건 너무 많아.", 0.75f);

                // 살았다 ↔ 아직 문은 안 열렸다.
                case PassengerReactionEvent.Threshold100:
                    return new PassengerReactionContrast(ReactionPose.Stare, ReactionGaze.Door,
                        "npc_breath_hold", "문은 아직 안 열렸어.", 0.50f);

                // 그만하자 ↔ 조금만 더. 과수확 직전의 갈등이 여기서 시작된다.
                case PassengerReactionEvent.Threshold170:
                    return new PassengerReactionContrast(ReactionPose.Lean, ReactionGaze.Device,
                        "npc_murmur_rise", "조금만 더.", 0.60f);

                // 경외 ↔ 도취.
                case PassengerReactionEvent.Threshold300:
                    return new PassengerReactionContrast(ReactionPose.Cheer, ReactionGaze.Device,
                        "npc_awe", "이게 우리가 원한 거잖아.", 0.85f);

                // 호기심 ↔ 도주. 시선이 레버가 아니라 문으로 간다.
                case PassengerReactionEvent.OverharvestUnlocked:
                    return new PassengerReactionContrast(ReactionPose.Cower, ReactionGaze.Door,
                        "npc_whimper", "지금 내리게 해 줘.", 0.55f);

                // 만류 ↔ 부추김. §7.3의 정적 구간이라 목소리는 작게 둔다.
                case PassengerReactionEvent.OverharvestApproach:
                    return new PassengerReactionContrast(ReactionPose.Lean, ReactionGaze.OverharvestLever,
                        "npc_murmur_urge", "당겨.", 0.45f);

                // 비명 ↔ 환호.
                case PassengerReactionEvent.ExtraSpin:
                    return new PassengerReactionContrast(ReactionPose.Cheer, ReactionGaze.Device,
                        "npc_cheer_wild", "한 번 더!", 0.90f);

                // 웅크림 ↔ 탈출 시도.
                case PassengerReactionEvent.CollapseImminent:
                    return new PassengerReactionContrast(ReactionPose.Brace, ReactionGaze.Door,
                        "npc_scream_long", "문, 문을 열어!", 1.00f);

                // 체념 ↔ 아직 끝나지 않았다는 확신.
                case PassengerReactionEvent.AccidentOrSuccess:
                    return new PassengerReactionContrast(ReactionPose.Flinch, ReactionGaze.Ceiling,
                        "npc_flinch_short", "아직 안 끝났어.", 0.50f);

                default:
                    return default(PassengerReactionContrast);
            }
        }
    }
}

// 씬 배선 필요:
// 1) `Assets/Prototype_Elevator/Data/PassengerReactionSet.asset` 을 만든다
//    (Create ▸ Ascend ▸ Profiles ▸ Passenger Reaction Set). 생성 시 Reset() 이 11종을 채운다.
//    이 파일을 만들지 않아도 코드 기본값으로 동작하지만, 그러면 §9.4의 "코드 수정 없이 교체"가
//    성립하지 않는다.
// 2) **이 에셋은 `Line`·`Contrast` 가 생기기 전에 직렬화됐다.** 두 필드는 지금
//    코드 기본값으로 폴백해서 동작하지만(`For` / `ContrastFor` 주석), 그러면 §9.4의
//    "코드 수정 없이 이벤트별 교체"가 그 두 채널에는 성립하지 않는다.
//    인스펙터에서 이 에셋의 톱니바퀴 ▸ Reset 을 한 번 누르면 11종이 대사·대조까지
//    채워져 다시 직렬화된다. `.asset` 은 단일 소유 파일이라 씬 오너가 눌러야 한다.
