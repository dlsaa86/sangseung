using Ascend.Prototype.Npc;

namespace Ascend.Prototype.Audio
{
    /// <summary>
    /// 승객이 내는 비언어 음성의 종류. `MASTER_PRD.md` §9.3 이 세는 다섯 가지다 —
    /// 웃음 · 한숨 · 호흡 · 기도 · 비난.
    ///
    /// **왜 이것이 필요한가.** 그전까지 승객 음성은 파형이 하나였고 승객 인덱스로
    /// 피치만 갈렸다. 그 상태는 「다섯 표현을 구현했다」가 아니라 「같은 소리를 다섯 번
    /// 썼다」이며, §18 Phase 4 의 무영상 청취에서 5연쇄의 웃음과 붕괴 직전의 기도가
    /// 같은 소리로 들린다. 소리의 **종류**와 **누가 냈는가**는 서로 다른 축이므로
    /// 서로 다른 파라미터로 실려야 한다 — 종류는 구운 파형이, 사람은 재생 피치가 나른다
    /// (<see cref="AudioCueTable.PassengerVoice(int, PassengerVoiceKind, float)"/>).
    ///
    /// **열거 순서가 PRD 의 나열 순서와 다르다.** PRD 는 웃음부터 세지만 여기서는
    /// <see cref="Breath"/> 가 0 이다. 값 0 이 「종류를 모를 때의 기본」이 되어야
    /// 종류를 받지 않는 옛 호출(<c>PassengerVoice(index, intensity)</c>)이 예전과
    /// **똑같은 변형 번호**를 만들고, 그래야 이미 구운 캐시와 기존 단정이 흔들리지 않는다.
    /// 호흡이 기본인 것은 뜻으로도 맞다 — 아무 일도 없을 때 사람이 내는 소리다.
    ///
    /// 값을 명시적으로 고정한다. 변형 번호에 섞여 캐시 키와 로그로 나가므로
    /// 중간에 끼워 넣으면 과거 기록의 뜻이 바뀐다(<see cref="AudioCueKind"/> 와 같은 규약).
    /// </summary>
    public enum PassengerVoiceKind
    {
        /// <summary>호흡. 들이켜거나 참는 숨 — 목소리보다 잡음이 많고 음정이 거의 없다.</summary>
        Breath = 0,

        /// <summary>한숨. 음정이 아래로 흘러내린다. 안도와 체념이 같은 방향이다.</summary>
        Sigh = 1,

        /// <summary>웃음. 진폭이 끊어지며 뛴다 — 이 다섯 중 유일하게 맥동하는 소리다.</summary>
        Laugh = 2,

        /// <summary>기도. 낮고 고르게 웅얼거린다. 길고 거의 변하지 않는 것이 정보다.</summary>
        Prayer = 3,

        /// <summary>비난. 날카롭게 시작해 짧게 끊는다. 유일하게 공격적인 포락선이다.</summary>
        Blame = 4,
    }

    /// <summary>
    /// 음성 종류 · 승객 슬롯 · 변형 번호 사이의 변환. **순수 C# 이다** — UnityEngine 을
    /// 참조하지 않는다.
    ///
    /// 왜 한 곳에 모으는가: 이 인코딩을 두 곳이 쓴다. 매핑 표(<see cref="AudioCueTable"/>)가
    /// 만들고 합성기(<c>ProceduralClipFactory</c>)가 푼다. 둘이 각자 <c>kind * 8 + slot</c> 을
    /// 적으면 한쪽만 고쳐졌을 때 **컴파일도 되고 소리도 나는데 종류만 어긋난다** —
    /// 이 저장소가 채널 열거 두 벌에서 이미 겪은 실패다(`AudioDirector.ToMixChannel` 주석).
    /// </summary>
    public static class PassengerVoices
    {
        /// <summary>음성 종류 수. <see cref="PassengerVoiceKind"/> 멤버 수와 반드시 같다.</summary>
        public const int KindCount = 5;

        /// <summary>
        /// 한 종류 안에서 구분되는 목소리 수. 승객 자리 수가 아니라 **음색 슬롯** 수다 —
        /// 승객이 더 많으면 슬롯을 돌려 쓴다. 8 을 넘기면 변형 번호가 캐시 키
        /// (<c>ProceduralClipFactory</c> 의 하위 8비트)를 넘본다.
        /// </summary>
        public const int SlotCount = 8;

        /// <summary>변형 번호의 상한(포함). 39 — 캐시 키의 8비트 안에 들어간다.</summary>
        public const int MaxVariant = KindCount * SlotCount - 1;

        /// <summary>
        /// <c>ProceduralClipFactory.Prewarm</c> 이 미리 굽는 슬롯 수.
        /// 사건 버스 폴백이 고르는 승객 수(<see cref="AudioCueTable.VoicePassengerCount"/>)의
        /// 원본이 여기다 — 두 곳에 4 를 적으면 한쪽만 늘렸을 때 첫 발성이 굽는 프레임이 되고,
        /// 하필 그 순간이 붕괴라 성능 캡처에 스파이크로 남는다.
        /// </summary>
        public const int PrewarmSlotCount = 4;

        /// <summary>종류와 슬롯을 변형 번호 하나로 접는다. 범위 밖 값은 조인다.</summary>
        public static int Encode(PassengerVoiceKind kind, int slot)
        {
            int k = (int)kind;
            if (k < 0) k = 0;
            if (k >= KindCount) k = KindCount - 1;
            if (slot < 0) slot = 0;
            if (slot >= SlotCount) slot = SlotCount - 1;
            return k * SlotCount + slot;
        }

        /// <summary>변형 번호에서 종류를 꺼낸다.</summary>
        public static PassengerVoiceKind KindOf(int variant)
        {
            int v = Clamp(variant, 0, MaxVariant);
            return (PassengerVoiceKind)(v / SlotCount);
        }

        /// <summary>변형 번호에서 목소리 슬롯을 꺼낸다.</summary>
        public static int SlotOf(int variant)
        {
            int v = Clamp(variant, 0, MaxVariant);
            return v % SlotCount;
        }

        /// <summary>
        /// 이 변형이 <c>Prewarm</c> 이 굽는 집합 안에 있는가.
        /// 밖이면 그 소리는 **첫 재생 순간에 구워진다** — 그 프레임만 길어지고,
        /// 승객 음성이 나는 순간은 대개 5연쇄나 붕괴라 성능 캡처의 가장 나쁜 자리다.
        /// </summary>
        public static bool IsPrewarmed(int variant)
        {
            if (variant < 0 || variant > MaxVariant) return false;
            return SlotOf(variant) < PrewarmSlotCount;
        }

        /// <summary>
        /// 반응 사건 → 음성 종류. 다섯 표현이 **전부** 쓰이도록 배분한다.
        ///
        /// 하나라도 안 쓰이면 그 종류는 합성기에만 있고 게임에서는 들리지 않는다 —
        /// 이 저장소가 `PassengerVoice` 자체에서 이미 겪은 형태의 실패다
        /// (「합성기도 있고 큐 종류도 있고 재생 통로도 있었지만 부르는 코드가 없었다」).
        ///
        /// 배분 근거는 사건의 정서다.
        ///   웃음 — 판이 스스로 굴러가는 순간(5연쇄)과 최대치(300%)
        ///   한숨 — 무사히 넘긴 것들(기본 정화 · 100% · 층 결과)
        ///   호흡 — 아직 결정되지 않은 것들(계약 선택 · 170% · 과수확 접근)
        ///   기도 — 되돌릴 수 없어진 것들(과수확 해금 · 붕괴 직전)
        ///   비난 — 플레이어가 저지른 것(추가 스핀)
        /// </summary>
        public static PassengerVoiceKind FromReaction(PassengerReactionEvent reaction)
        {
            switch (reaction)
            {
                case PassengerReactionEvent.FiveChain:
                case PassengerReactionEvent.Threshold300:
                    return PassengerVoiceKind.Laugh;

                case PassengerReactionEvent.BasicPurify:
                case PassengerReactionEvent.Threshold100:
                case PassengerReactionEvent.AccidentOrSuccess:
                    return PassengerVoiceKind.Sigh;

                case PassengerReactionEvent.OverharvestUnlocked:
                case PassengerReactionEvent.CollapseImminent:
                    return PassengerVoiceKind.Prayer;

                // 「왜 당겼느냐」. 이 층에서 유일하게 플레이어를 향하는 소리다.
                case PassengerReactionEvent.ExtraSpin:
                    return PassengerVoiceKind.Blame;

                // 계약 선택 · 170% · 과수확 접근. 셋 다 「아직 아무 일도 일어나지 않았다」이다.
                // (과수확 접근은 §7.3 이 정적으로 규정하므로 강도 0 이라 실제로는 울리지 않는다.
                //  그래도 종류를 비워 두지 않는 이유는, 강도를 데이터로 올리는 날
                //  「무슨 소리를 낼지 아무도 안 정했다」가 되지 않게 하려는 것이다.)
                default:
                    return PassengerVoiceKind.Breath;
            }
        }

        /// <summary>
        /// 반응 데이터의 큐 ID → 음성 종류. ID 목록의 출처는 둘이다 —
        /// `PassengerReactionSet.DefaultFor`(11종)와 `DefaultContrastFor`(대조 반응).
        ///
        /// **대조 반응의 ID 가 특히 중요하다.** 같은 사건에 한 사람은 환호하고 다른
        /// 사람은 버티는 것이 §9.3 의 요구인데, 두 반응의 소리가 같으면 그 대비는
        /// 자세에만 남고 귀로는 사라진다. 그래서 `npc_cheer_wild`(환호)와 `npc_gasp`
        /// (숨을 들이켬)이 서로 다른 종류로 풀려야 한다.
        ///
        /// **이 함수가 UP-NPC-04 의 「큐 ID 만 있고 재생 배선이 없다」를 잇는 자리다.**
        /// 반응 사건만으로 종류를 정하면 `PassengerReactionSet` 에서 큐 ID 를 바꿔도
        /// 소리가 그대로여서, §9.4 의 「데이터로 이벤트별 교체 가능」이 소리 채널에서만
        /// 성립하지 않는다.
        ///
        /// 모르는 ID 는 <c>false</c> 다 — 조용히 호흡으로 떨어뜨리면 편집자가 오타를 낸
        /// 것과 의도적으로 호흡을 고른 것이 구분되지 않는다. 호출자가 반응 사건으로
        /// 폴백한다.
        /// </summary>
        public static bool TryFromCueId(string cueId, out PassengerVoiceKind kind)
        {
            switch (cueId)
            {
                // 웃음 — 위를 향하는 소리. 「한 번 더!」가 여기 있는 것이
                // `ExtraSpin` 대조 반응의 핵심이다(주 반응은 비명이다).
                case "npc_awe":
                case "npc_awe_deep":
                case "npc_cheer_wild":
                case "npc_laugh":
                    kind = PassengerVoiceKind.Laugh;
                    return true;

                // 한숨 — 음정이 아래로 흘러내린다.
                case "npc_sigh":
                case "npc_relief_short":
                case "npc_relief_long":
                    kind = PassengerVoiceKind.Sigh;
                    return true;

                // 호흡 — 들이켜거나 참거나 움찔한다. 셋 다 음정이 거의 없다.
                case "npc_breath_hold":
                case "npc_gasp":
                case "npc_flinch_short":
                case "npc_murmur_low":
                    kind = PassengerVoiceKind.Breath;
                    return true;

                // 기도 — 낮고 고르게 웅얼거리는 간청. 「지금 내리게 해 줘」와
                // 「당겨」는 방향이 반대지만 소리의 결은 같다. 비명도 여기다 —
                // 다섯 표현 중 절규에 가장 가까운 것이 기도의 음역이다.
                case "npc_murmur_rise":
                case "npc_murmur_urge":
                case "npc_whimper":
                case "npc_scream_short":
                case "npc_scream_long":
                case "npc_pray":
                    kind = PassengerVoiceKind.Prayer;
                    return true;

                // 비난 — 플레이어를 향한다. 「누가 결정했지?」·「겨우 하나야」가
                // 이 프로토타입에서 유일하게 사람을 겨누는 소리다.
                case "npc_yelp":
                case "npc_murmur_doubt":
                case "npc_tsk":
                case "npc_blame":
                    kind = PassengerVoiceKind.Blame;
                    return true;

                default:
                    kind = PassengerVoiceKind.Breath;
                    return false;
            }
        }

        /// <summary>
        /// **반응 하나가 낼 소리의 최종 판정.** 큐 ID 가 이기고, 모르면 사건으로 떨어진다.
        ///
        /// 이 함수가 따로 있는 이유는 같은 판정을 두 곳이 필요로 하기 때문이다 —
        /// 소리를 내는 쪽(<c>AudioDirector.PlayPassengerVoice</c>)과 「무슨 종류를
        /// 요청했는가」를 세는 쪽(<c>PassengerReactionView</c>)이다. 세는 쪽이 자기
        /// 판정을 따로 적으면 **로그와 실제 소리가 어긋나고**, 어긋난 줄도 모른다 —
        /// 로그 쪽만 고쳐도 컴파일되고 소리도 그대로 나기 때문이다.
        ///
        /// **큐 ID 가 이기는 것이 이 항목의 핵심이다.** 대표 반응과 대조 반응은 같은
        /// 사건에서 나오므로 <see cref="FromReaction"/> 으로는 **구분이 불가능하다** —
        /// 5연쇄에서 환호하는 사람과 숨을 들이켜는 사람이 같은 소리를 낸다.
        /// 둘을 가르는 정보는 오직 `PassengerReaction.VoiceCue` 에만 있다.
        /// </summary>
        public static PassengerVoiceKind Resolve(string cueId, PassengerReactionEvent reaction)
        {
            PassengerVoiceKind kind;
            if (TryFromCueId(cueId, out kind)) return kind;
            return FromReaction(reaction);
        }

        /// <summary>
        /// 자막과 로그가 쓰는 한국어 이름.
        ///
        /// `AccessibilityProfile.ShowSubtitles` 가 「비언어 음성·기계음에 자막을 붙인다」고
        /// 약속하는데 승객 쪽에는 자막으로 낼 문안 자체가 없었다. 그리는 쪽은
        /// UI 소유자의 몫이라 여기서는 문안까지만 확정한다
        /// (`RiskStateView.HumCaptions` 와 같은 구조다).
        /// </summary>
        public static string Caption(this PassengerVoiceKind kind)
        {
            switch (kind)
            {
                case PassengerVoiceKind.Sigh:   return "[승객 — 한숨]";
                case PassengerVoiceKind.Laugh:  return "[승객 — 웃음]";
                case PassengerVoiceKind.Prayer: return "[승객 — 웅얼거리는 기도]";
                case PassengerVoiceKind.Blame:  return "[승객 — 비난]";
                default:                        return "[승객 — 참는 숨]";
            }
        }

        /// <summary>디버그 패널과 진행 로그가 쓰는 짧은 이름.</summary>
        public static string DisplayName(this PassengerVoiceKind kind)
        {
            switch (kind)
            {
                case PassengerVoiceKind.Sigh:   return "한숨";
                case PassengerVoiceKind.Laugh:  return "웃음";
                case PassengerVoiceKind.Prayer: return "기도";
                case PassengerVoiceKind.Blame:  return "비난";
                default:                        return "호흡";
            }
        }

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
