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

        /// <summary>
        /// 승객 음성은 사건 표를 거치지 않는다. 어느 승객이 왜 소리를 내는지는
        /// 승객 반응 시스템(UP-NPC-*)이 알고 이 표는 모르기 때문이다.
        /// 승객 인덱스로 피치만 갈라 준다 — 넷이 같은 목소리면 승객이 아니라 스피커다.
        /// </summary>
        public static AudioCueRequest PassengerVoice(int passengerIndex, float intensity)
        {
            int variant = Clamp(passengerIndex, 0, 7);
            // 승객마다 장3도씩 어긋나게 흩어 놓는다. 반음으로 흩으면 같은 사람이
            // 목이 쉰 것처럼 들리고, 옥타브로 흩으면 종족이 달라진다.
            float pitch = Clamp(1f + 0.09f * (variant - 3), MinPitch, MaxPitch);
            return new AudioCueRequest(AudioCueKind.PassengerVoice,
                Scale(intensity, 0f, 1f, 0.4f, 0.95f), pitch, variant);
        }

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
