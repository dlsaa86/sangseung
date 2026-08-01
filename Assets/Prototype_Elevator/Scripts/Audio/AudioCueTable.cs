using Ascend.Prototype.Events;

namespace Ascend.Prototype.Audio
{
    /// <summary>
    /// "이 소리를 이만큼 크게, 이 피치로 울려라" 한 건. 재생 방법은 모른다.
    ///
    /// <c>readonly struct</c>인 이유는 <see cref="GameEvent"/>와 같다 — 스핀 하나가
    /// 수십 개의 큐를 만든다. 클래스로 두면 스핀마다 힙이 늘고 `MASTER_PRD.md` §13.2의
    /// "워밍업 후 매 프레임 0 B"가 구조적으로 불가능해진다.
    /// </summary>
    public readonly struct AudioCueRequest
    {
        public readonly AudioCueKind Kind;

        /// <summary>0~1. 채널 볼륨과 마스터 볼륨은 여기 곱해져 있지 않다.</summary>
        public readonly float Volume;

        /// <summary>재생 피치 배수. 1이 원음.</summary>
        public readonly float Pitch;

        /// <summary>같은 종류 안에서 음색을 가르는 번호(통관 인덱스, 승객 인덱스 등).</summary>
        public readonly int Variant;

        public AudioCueRequest(AudioCueKind kind, float volume, float pitch, int variant)
        {
            Kind = kind;
            Volume = volume;
            Pitch = pitch;
            Variant = variant;
        }

        public override string ToString() =>
            Kind + " v=" + Volume.ToString("0.##") + " p=" + Pitch.ToString("0.###") + " var=" + Variant;
    }

    /// <summary>
    /// 사건 하나를 소리 한 건으로 옮긴다. **순수 C#이다** — UnityEngine을 참조하지 않는다.
    ///
    /// 왜 MonoBehaviour 밖에 두는가: 이 표가 곧 "무엇이 들리는가"의 정의이고,
    /// 그 정의는 씬 없이 검증할 수 있어야 한다(`TECH_SPEC.md` §2). 실제로 UP-AUD-02의
    /// 검증 항목은 "사운드 이벤트 발동 로그"이지 캡처가 아니다. 스피커 없이도 틀렸는지
    /// 알 수 있어야 그 검증이 성립한다.
    ///
    /// 매핑되지 않는 사건은 조용히 무시하지 않고 <c>false</c>를 돌려준다. 소리가 나야 할
    /// 사건이 빠졌는지 호출자가 셀 수 있어야 한다.
    /// </summary>
    public static class AudioCueTable
    {
        /// <summary>피치 하한. 이보다 낮으면 재생 길이가 늘어 다음 큐와 겹친다.</summary>
        public const float MinPitch = 0.5f;

        /// <summary>피치 상한. 이보다 높으면 금속음이 삑 소리로 변해 종류 구분이 사라진다.</summary>
        public const float MaxPitch = 2.4f;

        /// <summary>
        /// 캐스케이드 한 단계당 올라가는 피치 — 반음(2^(1/12)).
        /// `MASTER_PRD.md` §6.1 판독 순서 6번("캐스케이드 신규 심볼")을 귀로 세게 하려는 것이다.
        /// 깊이를 숫자로만 보여 주면 화면을 안 보는 순간 사라진다.
        /// </summary>
        public const float SemitoneRatio = 1.0594631f;

        /// <summary>
        /// 캐스케이드 피치가 오르기를 멈추는 깊이. 하드 캡은 20이지만(`MASTER_PRD.md` §6)
        /// 12단계면 이미 한 옥타브라 그 위로는 음정이 아니라 잡음으로 들린다.
        /// </summary>
        public const int MaxCascadePitchDepth = 12;

        public static bool TryMap(in GameEvent e, out AudioCueRequest req)
        {
            switch (e.Kind)
            {
                // ── 룰렛 10종 ────────────────────────────────────────────────
                case GameEventKind.SpinStarted:
                    req = new AudioCueRequest(AudioCueKind.LeverPull, 0.95f, 1f, 0);
                    return true;

                case GameEventKind.ColumnRevealed:
                    // 통관 셋이 왼쪽부터 멈춘다. 피치를 조금씩 올려 "몇 번째가 멈췄나"를
                    // 화면을 안 봐도 알게 한다 — §6.1 판독 순서 1번의 청각판이다.
                    req = new AudioCueRequest(AudioCueKind.ColumnReveal, 0.55f,
                        1f + 0.07f * Clamp(e.IntValue, 0, 2), Clamp(e.IntValue, 0, 2));
                    return true;

                case GameEventKind.NormalSoulHarvested:
                    // 개수가 많을수록 크게. 한 번에 아홉을 거둔 것과 하나를 거둔 것이
                    // 같은 크기로 들리면 그 소리는 정보가 아니라 장식이다.
                    req = new AudioCueRequest(AudioCueKind.SoulHarvest,
                        Scale(e.IntValue, 1, 9, 0.45f, 0.9f), 1f, 0);
                    return true;

                case GameEventKind.PurifyScattered:
                    req = new AudioCueRequest(AudioCueKind.PurifyScattered,
                        PurifyVolume(e.IntValue), 1f, 0);
                    return true;

                case GameEventKind.PurifyLine:
                    req = new AudioCueRequest(AudioCueKind.PurifyLine,
                        PurifyVolume(e.IntValue), 1f, 0);
                    return true;

                case GameEventKind.PurifyCluster:
                    req = new AudioCueRequest(AudioCueKind.PurifyCluster,
                        PurifyVolume(e.IntValue), 1f, 0);
                    return true;

                case GameEventKind.CascadeStep:
                    req = new AudioCueRequest(AudioCueKind.CascadeStep, 0.7f,
                        CascadePitch(e.IntValue), 0);
                    return true;

                case GameEventKind.PowerThresholdCrossed:
                    // 100 / 170 / 300%. 위 임계점일수록 크고 조금 높다.
                    req = new AudioCueRequest(AudioCueKind.ThresholdCrossed,
                        Scale(ThresholdTier(e.IntValue), 0, 2, 0.7f, 1f),
                        1f + 0.05f * ThresholdTier(e.IntValue), ThresholdTier(e.IntValue));
                    return true;

                case GameEventKind.ResidualDamage:
                    // FloatValue = 깎인 전력(양수). 많이 깎일수록 크고 낮게 — 손해는
                    // 이득과 반대 방향으로 들려야 구분된다.
                    req = new AudioCueRequest(AudioCueKind.ResidualDamage,
                        Scale(e.FloatValue, 0f, 40f, 0.5f, 1f),
                        Clamp(1f - Scale(e.FloatValue, 0f, 40f, 0f, 0.15f), MinPitch, MaxPitch), 0);
                    return true;

                case GameEventKind.PowerBanked:
                    req = new AudioCueRequest(AudioCueKind.PowerBanked, 0.9f, 1f, 0);
                    return true;

                // ── 룰렛 밖 ──────────────────────────────────────────────────
                case GameEventKind.OverharvestUnlocked:
                    req = new AudioCueRequest(AudioCueKind.OverharvestUnlock, 0.85f, 1f, 0);
                    return true;

                case GameEventKind.OverharvestPulled:
                    req = new AudioCueRequest(AudioCueKind.OverharvestPull, 1f, 1f, 0);
                    return true;

                case GameEventKind.CollapseBegan:
                    req = new AudioCueRequest(AudioCueKind.CollapseImpact, 1f, 1f, 0);
                    return true;

                case GameEventKind.RiskLevelChanged:
                    // IntValue = 새 RiskLevel(0~3). 단계가 깊어질수록 낮고 크다.
                    // Payload(이전 단계)는 읽지 않는다 — 박싱된 값을 언박싱하려면
                    // Risk 네임스페이스를 알아야 하고, 이 표는 그것을 몰라야 한다.
                    req = new AudioCueRequest(AudioCueKind.MetalStress,
                        Scale(e.IntValue, 0, 3, 0.4f, 1f),
                        Clamp(1.1f - 0.12f * Clamp(e.IntValue, 0, 3), MinPitch, MaxPitch),
                        Clamp(e.IntValue, 0, 3));
                    return true;

                default:
                    // 층 흐름(FloorStarted·ItemBoarded·…)과 종합 사건(SpinResolved)은
                    // 소리를 내지 않는다. 이미 그 안의 단계들이 각각 울렸기 때문에
                    // 한 번 더 울리면 같은 일이 두 번 일어난 것처럼 들린다.
                    req = default(AudioCueRequest);
                    return false;
            }
        }

        // ── 겹쳐 우는 큐 ─────────────────────────────────────────────────────
        //
        // 아래 둘은 `TryMap` 과 **나란히** 돈다. 한 사건이 큐 하나라는 규칙을 깨는 것이
        // 아니라, 사건 하나가 서로 다른 채널에서 각각 울린다는 뜻이다 — 붕괴는 기계
        // 채널의 충격음이면서 동시에 경고 채널의 사이렌이고 승객 채널의 비명이다.
        // `MASTER_PRD.md` §9 마지막 문장("특정 감각 채널 하나에만 의존하지 않는다")의
        // 소리 안쪽 판이다.
        //
        // `TryMap` 에 끼워 넣지 않는 이유는 두 가지다. 첫째, 그 표는 "이 사건의 대표음은
        // 무엇인가"의 정의라서 하나여야 한다. 둘째, `AudioTests.TestUnmappedEvents` 가
        // 층 흐름 사건이 `TryMap` 에서 조용하기를 단정한다 — 계약 선택이 대표음을 갖는
        // 순간 그 단정이 뜻을 잃는다.

        /// <summary>
        /// 이 사건에 승객이 목소리를 내는가(UP-AUD-04, `MASTER_PRD.md` §9.3).
        ///
        /// **어떤 사건이 승객 반응인가를 여기서 다시 정하지 않는다.** 그 정의는
        /// <see cref="Npc.PassengerReactionEvents.TryMap"/> 하나뿐이고(PRD §9.2 의 11종),
        /// 목록을 여기 복사하면 승객이 몸으로는 반응하는데 소리는 안 나거나 그 반대인
        /// 상태가 조용히 생긴다. 이 함수가 정하는 것은 **얼마나 크게**뿐이고,
        /// 「어떤 소리인가」도 여기서 새로 정하지 않는다 —
        /// <see cref="PassengerVoices.FromReaction"/> 하나가 그 배분을 갖는다.
        ///
        /// 이것은 폴백 경로다. 승객 반응 시스템이 <c>AudioDirector.PlayPassengerVoice</c> 를
        /// 부르기 시작하면 그쪽이 이긴다(누가 반응 중인지는 중재기만 안다).
        /// 그때까지는 사건 버스만으로도 승객 채널이 비어 있지 않아야 한다 —
        /// 비면 §18 Phase 4 의 무영상 청취 테스트에서 채널 하나가 통째로 없다.
        /// </summary>
        public static bool TryMapPassengerVoice(in GameEvent e, out AudioCueRequest req)
        {
            Npc.PassengerReactionEvent reaction;
            if (!Npc.PassengerReactionEvents.TryMap(in e, out reaction))
            {
                req = default(AudioCueRequest);
                return false;
            }

            float intensity = VoiceIntensity(reaction);
            if (intensity <= 0f)
            {
                req = default(AudioCueRequest);
                return false;
            }

            // 종류는 반응 사건이 정한다(웃음·한숨·호흡·기도·비난, `MASTER_PRD.md` §9.3).
            // 여기서 종류를 다시 판정하지 않는 이유는 `Npc.PassengerReactionEvents.TryMap` 을
            // 다시 쓰지 않는 이유와 같다 — 정의가 두 벌이 되면 한쪽만 고쳐진다.
            req = PassengerVoice(PassengerVoiceIndex(in e),
                                 PassengerVoices.FromReaction(reaction), intensity);
            return true;
        }

        /// <summary>
        /// 목소리가 나는 승객 수. <c>ProceduralClipFactory.Prewarm</c> 이 굽는 수와 같아야
        /// 한다 — 넘어가면 첫 발성이 굽는 프레임이 되어 성능 캡처에 스파이크로 찍힌다.
        /// (표 자체는 <see cref="PassengerVoice(int, float)"/>에서 0~7 을 받는다.
        /// 여기서 고르는 범위만 좁다.)
        ///
        /// 값은 <see cref="PassengerVoices.PrewarmSlotCount"/> 하나에서 온다. 두 곳에 4 를
        /// 적으면 한쪽만 늘렸을 때 아무 경고 없이 굽는 프레임이 생긴다.
        /// </summary>
        public const int VoicePassengerCount = PassengerVoices.PrewarmSlotCount;

        /// <summary>
        /// 이 사건에 목소리를 낼 승객. **난수를 쓰지 않는다** — 같은 시드의 런이 같은
        /// 소리를 내야 캡처 회귀가 오디오 때문에 흔들리지 않는다(`ProceduralClipFactory`
        /// 가 <c>UnityEngine.Random</c> 을 피하는 것과 같은 이유다).
        ///
        /// 층·스핀·사건 종류를 섞는 이유는 한 층 안에서 같은 사람만 계속 말하지 않게
        /// 하려는 것뿐이다. 실제로 누가 반응하는지는 중재기가 알고 이 표는 모른다.
        /// </summary>
        public static int PassengerVoiceIndex(in GameEvent e)
        {
            int h = e.Floor * 7 + (e.SpinIndex + 1) * 3 + (int)e.Kind * 5;
            if (h < 0) h = -h;
            return h % VoicePassengerCount;
        }

        /// <summary>
        /// 반응 사건별 발성 강도(0~1). 0이면 소리를 내지 않는다.
        ///
        /// 「과수확 접근」이 0인 것은 누락이 아니다. `MASTER_PRD.md` §7.3 이 그 순간을
        /// **정적**으로 규정한다 — 방을 조용하게 만들라고 한 자리에 소리를 하나 더
        /// 넣으면 그 연출은 그냥 없어진다. 승객은 그 구간에 자세와 시선으로만 반응한다.
        /// </summary>
        private static float VoiceIntensity(Npc.PassengerReactionEvent reaction)
        {
            switch (reaction)
            {
                case Npc.PassengerReactionEvent.ContractChosen:      return 0.30f;
                case Npc.PassengerReactionEvent.BasicPurify:         return 0.35f;
                case Npc.PassengerReactionEvent.FiveChain:           return 0.72f;
                case Npc.PassengerReactionEvent.Threshold100:        return 0.55f;
                case Npc.PassengerReactionEvent.Threshold170:        return 0.70f;
                case Npc.PassengerReactionEvent.Threshold300:        return 0.90f;
                case Npc.PassengerReactionEvent.OverharvestUnlocked: return 0.60f;
                case Npc.PassengerReactionEvent.OverharvestApproach: return 0f;      // §7.3 정적
                case Npc.PassengerReactionEvent.ExtraSpin:           return 0.75f;
                case Npc.PassengerReactionEvent.CollapseImminent:    return 1f;
                case Npc.PassengerReactionEvent.AccidentOrSuccess:   return 0.65f;
                default:                                             return 0f;
            }
        }

        /// <summary>
        /// 사이렌이 울리는 최소 위험 단계 — `RiskLevel.Strain`(1).
        ///
        /// <c>RiskLevel</c> 을 직접 참조하지 않는다. 이 표가 Risk 네임스페이스를 알면
        /// 밸런스 쪽 열거가 바뀔 때 사운드 매핑이 같이 흔들린다(<see cref="TryMap"/>의
        /// <c>RiskLevelChanged</c> 주석과 같은 이유다). 값이 바뀌면 여기서 한 줄 고친다.
        /// </summary>
        public const int SirenMinRiskLevel = 1;

        /// <summary>사이렌 변형 — 왜 울렸는가. 넷이 같은 소리면 "경보"라는 사실만 남는다.</summary>
        public const int SirenVariantRiskRise = 0;
        public const int SirenVariantOverharvestUnlock = 1;
        public const int SirenVariantLeverDecision = 2;
        public const int SirenVariantAccident = 3;

        /// <summary>
        /// 이 사건에 사이렌이 울리는가(UP-RISK-05).
        ///
        /// Notion MASTER PRD §8.3 이 허용한 **네 순간뿐이다** — 단계 상승 · 과수확 해금 ·
        /// 레버 결정 · 사고. 목록을 늘리는 것은 의도적인 결정이어야 한다. 사이렌이
        /// 흔해지는 순간 그것은 신호가 아니라 배경이 되고, 그러면 "단계 상승을 못 읽는다"는
        /// `VISUAL_BIBLE.md` 금지 16번이 그대로 재현된다.
        ///
        /// 여기서 판정하지 **않는** 것이 하나 있다: 단계가 올랐는지 내렸는지.
        /// 이 사건의 <c>Payload</c> 에 이전 단계가 박싱돼 들어 있지만 그것을 꺼내려면
        /// Risk 네임스페이스를 알아야 한다. 방향은 사건을 순서대로 보는 쪽
        /// (<c>AudioDirector</c>)이 안다.
        /// </summary>
        public static bool TryMapSiren(in GameEvent e, out AudioCueRequest req)
        {
            switch (e.Kind)
            {
                case GameEventKind.RiskLevelChanged:
                    // IntValue = 새 단계(0~3). Stable 로 돌아온 것은 경보가 아니다.
                    if (e.IntValue >= SirenMinRiskLevel)
                    {
                        req = new AudioCueRequest(AudioCueKind.Siren,
                            Scale(e.IntValue, SirenMinRiskLevel, 3, 0.70f, 1f),
                            Clamp(1.12f - 0.06f * Clamp(e.IntValue, 0, 3), MinPitch, MaxPitch),
                            SirenVariantRiskRise);
                        return true;
                    }
                    break;

                case GameEventKind.OverharvestUnlocked:
                    req = new AudioCueRequest(AudioCueKind.Siren, 0.80f, 1.06f,
                        SirenVariantOverharvestUnlock);
                    return true;

                case GameEventKind.OverharvestPulled:
                    req = new AudioCueRequest(AudioCueKind.Siren, 0.90f, 0.94f,
                        SirenVariantLeverDecision);
                    return true;

                case GameEventKind.CollapseBegan:
                    req = new AudioCueRequest(AudioCueKind.Siren, 1f, 0.84f,
                        SirenVariantAccident);
                    return true;
            }

            req = default(AudioCueRequest);
            return false;
        }

        /// <summary>
        /// 승객 음성 한 건. 승객 음성은 사건 표를 거치지 않는다 — 어느 승객이 왜 소리를
        /// 내는지는 승객 반응 시스템(UP-NPC-*)이 알고 이 표는 모르기 때문이다.
        ///
        /// **두 축이 함께 실린다.**
        ///   <paramref name="voice"/>       → 어떤 소리인가(웃음·한숨·호흡·기도·비난).
        ///                                    변형 번호에 접혀 들어가 **다른 파형**을 굽게 한다.
        ///   <paramref name="passengerIndex"/> → 누가 냈는가. 재생 피치를 가른다.
        ///
        /// 종류를 볼륨이나 피치로만 가르지 않는 이유: 그러면 「크고 높은 한숨」과
        /// 「작고 낮은 웃음」이 같은 소리가 된다. 다섯을 구분 가능하게 만들라는 요구
        /// (`MASTER_PRD.md` §9.3)는 파형이 달라야 성립한다.
        /// </summary>
        public static AudioCueRequest PassengerVoice(int passengerIndex, PassengerVoiceKind voice,
                                                     float intensity)
        {
            int slot = Clamp(passengerIndex, 0, PassengerVoices.SlotCount - 1);

            // 승객마다 장3도씩 어긋나게 흩어 놓는다. 반음으로 흩으면 같은 사람이
            // 목이 쉰 것처럼 들리고, 옥타브로 흩으면 종족이 달라진다.
            float pitch = Clamp(1f + 0.09f * (slot - 3), MinPitch, MaxPitch);

            return new AudioCueRequest(AudioCueKind.PassengerVoice,
                Scale(intensity, 0f, 1f, 0.4f, 0.95f), pitch,
                PassengerVoices.Encode(voice, slot));
        }

        /// <summary>
        /// 종류를 모를 때. 기본은 호흡이다 — 아무 일도 없을 때 사람이 내는 소리이고,
        /// <see cref="PassengerVoiceKind.Breath"/>가 0 이라 변형 번호가 승객 인덱스와
        /// 그대로 같아진다(옛 호출과 같은 소리가 난다).
        /// </summary>
        public static AudioCueRequest PassengerVoice(int passengerIndex, float intensity)
            => PassengerVoice(passengerIndex, PassengerVoiceKind.Breath, intensity);

        /// <summary>정화한 칸이 많을수록 크다. 3칸이 하한, 9칸(전판)이 상한이다.</summary>
        private static float PurifyVolume(int cells) => Scale(cells, 3, 9, 0.6f, 1f);

        /// <summary>깊이 1이 원음, 이후 한 단계마다 반음.</summary>
        public static float CascadePitch(int depth)
        {
            int steps = Clamp(depth, 1, MaxCascadePitchDepth) - 1;
            float pitch = 1f;
            for (int i = 0; i < steps; i++) pitch *= SemitoneRatio;
            return Clamp(pitch, MinPitch, MaxPitch);
        }

        /// <summary>100 → 0, 170 → 1, 300 이상 → 2. 임계점 목록은 `MASTER_PRD.md` §7.</summary>
        public static int ThresholdTier(int percent)
        {
            if (percent >= 300) return 2;
            if (percent >= 170) return 1;
            return 0;
        }

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);

        /// <summary>[inLo,inHi]를 [outLo,outHi]로 옮기고 범위 밖은 끝값으로 조인다.</summary>
        private static float Scale(float value, float inLo, float inHi, float outLo, float outHi)
        {
            if (inHi <= inLo) return outLo;
            float t = Clamp((value - inLo) / (inHi - inLo), 0f, 1f);
            return outLo + (outHi - outLo) * t;
        }
    }
}
