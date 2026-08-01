using System;
using System.Text;
using Ascend.Prototype.Events;

namespace Ascend.Prototype.Audio.Tests
{
    /// <summary>
    /// 사운드 채널의 헤드리스 검증. UP-AUD-02(룰렛 10종)와 UP-AUD-03(과수확 정적)의
    /// 검증 기준이 각각 "사운드 이벤트 발동 로그"와 "오디오 게인 타임라인 측정"이라
    /// 스피커 없이도 틀렸는지 알 수 있어야 한다.
    ///
    /// <c>AudioDirector</c>와 <c>ProceduralClipFactory</c>는 검사하지 않는다 — 전자는
    /// MonoBehaviour라 씬이 필요하고, 후자는 파형이라 자동 판정 기준이 없다. 그 둘은
    /// 실제 청취와 캡처의 몫이다. 여기서 지키는 것은 **어떤 사건이 어떤 소리로 가는가**와
    /// **정적이 몇 초인가**뿐이며, 그 둘이 틀리면 나머지가 아무리 잘 들려도 거짓말이 된다.
    ///
    /// NUnit에 의존하지 않는 이유는 `DECISION_LOG.md` D-20260730-06 참조.
    /// </summary>
    public static class AudioTests
    {
        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0;
            int failed = 0;
            var report = new StringBuilder();

            Run("룰렛 사운드 10종이 전부 매핑된다", TestTenRouletteCues, ref passed, ref failed, report);
            Run("10종이 서로 다른 큐로 간다", TestCuesAreDistinct, ref passed, ref failed, report);
            Run("소리 없는 사건은 false 를 돌려준다", TestUnmappedEvents, ref passed, ref failed, report);
            Run("캐스케이드 깊이 1→5 에서 피치가 단조 증가", TestCascadePitchRises, ref passed, ref failed, report);
            Run("캐스케이드 피치가 상한에서 멈춘다", TestCascadePitchCaps, ref passed, ref failed, report);
            Run("정화 칸 수가 볼륨을 올린다", TestPurifyVolumeScales, ref passed, ref failed, report);
            Run("볼륨·피치가 유효 범위 안이다", TestRequestRanges, ref passed, ref failed, report);
            Run("승객 인덱스가 목소리를 가른다", TestPassengerVoices, ref passed, ref failed, report);
            Run("승객 반응 사건이 목소리를 만든다", TestVoiceFromReactionEvents, ref passed, ref failed, report);
            Run("반응이 아닌 사건은 조용하다", TestNonReactionEventsAreVoiceless, ref passed, ref failed, report);
            Run("같은 사건은 같은 목소리를 낸다", TestVoiceIsDeterministic, ref passed, ref failed, report);
            Run("§9.3 의 다섯 표현이 전부 쓰인다", TestFiveVoiceExpressions, ref passed, ref failed, report);
            Run("변형 하나가 종류와 목소리를 함께 싣는다", TestVoiceVariantEncoding, ref passed, ref failed, report);
            Run("반응 데이터의 큐 ID 가 전부 소리에 닿는다", TestVoiceCueIdsResolve, ref passed, ref failed, report);
            Run("사이렌은 §8.3 의 네 순간에만 울린다", TestSirenOnlyOnFourMoments, ref passed, ref failed, report);
            Run("사이렌 넷이 서로 다르게 들린다", TestSirenVariantsDiffer, ref passed, ref failed, report);
            Run("지속 위험 레이어가 단계마다 두꺼워진다", TestDangerBedRises, ref passed, ref failed, report);
            Run("안정 단계에는 응력음이 없다", TestDangerBedQuietWhenStable, ref passed, ref failed, report);
            Run("지속 레이어가 정적과 데이터를 따른다", TestDangerBedFollowsInputs, ref passed, ref failed, report);
            Run("정적 — 시작 전 1, 정적 0, 끝난 뒤 1", TestSilenceBoundaries, ref passed, ref failed, report);
            Run("정적 — 감쇠는 단조 감소, 재개는 단조 증가", TestSilenceMonotonic, ref passed, ref failed, report);
            Run("정적 길이가 0.3~0.7 로 조여진다", TestSilenceClamped, ref passed, ref failed, report);
            Run("정적 — IsSilent / IsActive 경계", TestSilenceFlags, ref passed, ref failed, report);
            Run("조준을 거두면 그 자리에서 재개된다", TestSilenceCancel, ref passed, ref failed, report);

            report.Insert(0, "[상승] === Audio Tests ===\n");
            report.Append($"결과: {passed} PASS / {failed} FAIL");
            return (passed, failed, report.ToString());
        }

        private static void Run(string name, Func<string> test,
                                ref int passed, ref int failed, StringBuilder report)
        {
            try
            {
                string failure = test();
                if (string.IsNullOrEmpty(failure)) { passed++; report.AppendLine($"  PASS  {name}"); }
                else { failed++; report.AppendLine($"  FAIL  {name} — {failure}"); }
            }
            catch (Exception exception)
            {
                failed++;
                report.AppendLine($"  FAIL  {name} — 예외: {exception.Message}");
            }
        }

        // ── 매핑 ─────────────────────────────────────────────────────────────

        /// <summary>UP-AUD-02가 지정한 열 가지(N08 §16.4). 하나라도 조용하면 그 항목은 미구현이다.</summary>
        private static readonly GameEventKind[] RouletteEvents =
        {
            GameEventKind.SpinStarted,
            GameEventKind.ColumnRevealed,
            GameEventKind.NormalSoulHarvested,
            GameEventKind.PurifyScattered,
            GameEventKind.PurifyLine,
            GameEventKind.PurifyCluster,
            GameEventKind.CascadeStep,
            GameEventKind.PowerThresholdCrossed,
            GameEventKind.ResidualDamage,
            GameEventKind.PowerBanked,
        };

        private static readonly AudioCueKind[] ExpectedCues =
        {
            AudioCueKind.LeverPull,
            AudioCueKind.ColumnReveal,
            AudioCueKind.SoulHarvest,
            AudioCueKind.PurifyScattered,
            AudioCueKind.PurifyLine,
            AudioCueKind.PurifyCluster,
            AudioCueKind.CascadeStep,
            AudioCueKind.ThresholdCrossed,
            AudioCueKind.ResidualDamage,
            AudioCueKind.PowerBanked,
        };

        /// <summary>대표값이 들어간 사건 하나. 볼륨·피치 계산이 0으로 눌리지 않게 실제 값을 넣는다.</summary>
        private static GameEvent Sample(GameEventKind kind)
        {
            switch (kind)
            {
                case GameEventKind.ColumnRevealed:
                    return new GameEvent(kind, 1, 0, 1);
                case GameEventKind.NormalSoulHarvested:
                    return new GameEvent(kind, 1, 0, 4, 12f);
                case GameEventKind.PurifyScattered:
                case GameEventKind.PurifyLine:
                case GameEventKind.PurifyCluster:
                    return new GameEvent(kind, 1, 0, 4, 30f);
                case GameEventKind.CascadeStep:
                    return new GameEvent(kind, 1, 0, 2, 18f);
                case GameEventKind.PowerThresholdCrossed:
                    return new GameEvent(kind, 1, 0, 170);
                case GameEventKind.ResidualDamage:
                    return new GameEvent(kind, 1, 0, 2, 9f);
                case GameEventKind.PowerBanked:
                    return new GameEvent(kind, 1, 0, 0, 140f);
                default:
                    return new GameEvent(kind, 1, 0);
            }
        }

        private static string TestTenRouletteCues()
        {
            for (int i = 0; i < RouletteEvents.Length; i++)
            {
                GameEvent e = Sample(RouletteEvents[i]);
                AudioCueRequest req;
                if (!AudioCueTable.TryMap(in e, out req))
                    return $"{RouletteEvents[i]} 가 소리를 내지 않는다";
                if (req.Kind != ExpectedCues[i])
                    return $"{RouletteEvents[i]} → {req.Kind}, 기대 {ExpectedCues[i]}";
            }
            return null;
        }

        private static string TestCuesAreDistinct()
        {
            var seen = new AudioCueKind[RouletteEvents.Length];
            for (int i = 0; i < RouletteEvents.Length; i++)
            {
                GameEvent e = Sample(RouletteEvents[i]);
                AudioCueRequest req;
                if (!AudioCueTable.TryMap(in e, out req)) return $"{RouletteEvents[i]} 매핑 없음";
                seen[i] = req.Kind;
            }

            for (int i = 0; i < seen.Length; i++)
                for (int j = i + 1; j < seen.Length; j++)
                    if (seen[i] == seen[j])
                        return $"{RouletteEvents[i]} 와 {RouletteEvents[j]} 가 같은 큐({seen[i]})로 뭉쳤다";
            return null;
        }

        /// <summary>
        /// 층 흐름과 종합 사건은 조용해야 한다. SpinResolved가 소리를 내면 스핀 하나가
        /// 두 번 일어난 것처럼 들린다 — 안의 단계들이 이미 각각 울렸기 때문이다.
        /// </summary>
        private static string TestUnmappedEvents()
        {
            GameEventKind[] silent =
            {
                GameEventKind.None,
                GameEventKind.FloorStarted,
                GameEventKind.ItemBoarded,
                GameEventKind.BoardingFinished,
                GameEventKind.ContractSelected,
                GameEventKind.SpinResolved,
                GameEventKind.CascadeCapReached,
                GameEventKind.ExtraSpinTaken,
                GameEventKind.FloorResolved,
                GameEventKind.RunEnded,
                GameEventKind.JettisonPaid,
            };

            foreach (GameEventKind kind in silent)
            {
                var e = new GameEvent(kind, 1, 0, 3, 5f);
                AudioCueRequest req;
                if (AudioCueTable.TryMap(in e, out req))
                    return $"{kind} 가 소리를 낸다 ({req.Kind}) — 중복 재생이 된다";
                if (req.Kind != AudioCueKind.None)
                    return $"{kind} 실패 경로가 req 를 비우지 않았다 ({req.Kind})";
            }
            return null;
        }

        // ── 캐스케이드 ───────────────────────────────────────────────────────

        /// <summary>
        /// `MASTER_PRD.md` §6.1 판독 순서 6번의 청각판 — 깊이를 귀로 셀 수 있어야 한다.
        /// 피치가 평평하면 20연쇄와 2연쇄가 똑같이 들린다.
        /// </summary>
        private static string TestCascadePitchRises()
        {
            float previous = 0f;
            for (int depth = 1; depth <= 5; depth++)
            {
                var e = new GameEvent(GameEventKind.CascadeStep, 1, 0, depth, 10f);
                AudioCueRequest req;
                if (!AudioCueTable.TryMap(in e, out req)) return $"깊이 {depth} 매핑 없음";
                if (req.Pitch <= previous)
                    return $"깊이 {depth} 피치 {req.Pitch:0.####} ≤ 깊이 {depth - 1} 피치 {previous:0.####}";
                previous = req.Pitch;
            }

            // 반음이라는 성질 자체를 고정한다. 밸런스가 아니라 판독성의 근거다.
            float one = AudioCueTable.CascadePitch(1);
            float two = AudioCueTable.CascadePitch(2);
            if (Math.Abs(two / one - AudioCueTable.SemitoneRatio) > 0.0005f)
                return $"한 단계 간격 {two / one:0.#####}, 기대 {AudioCueTable.SemitoneRatio:0.#####}";
            return null;
        }

        private static string TestCascadePitchCaps()
        {
            // 깊이 0 이나 음수가 와도 원음 아래로 내려가지 않는다.
            if (AudioCueTable.CascadePitch(0) != AudioCueTable.CascadePitch(1))
                return "깊이 0 이 깊이 1 과 다르게 처리됐다";
            if (AudioCueTable.CascadePitch(-5) != AudioCueTable.CascadePitch(1))
                return "음수 깊이가 조여지지 않았다";

            // 하드 캡 20까지 와도 피치 상한을 넘지 않는다(`MASTER_PRD.md` §6).
            float deep = AudioCueTable.CascadePitch(20);
            if (deep > AudioCueTable.MaxPitch) return $"깊이 20 피치 {deep} > 상한 {AudioCueTable.MaxPitch}";
            if (deep != AudioCueTable.CascadePitch(AudioCueTable.MaxCascadePitchDepth))
                return "상한 깊이를 넘어서도 피치가 계속 오른다";
            return null;
        }

        // ── 볼륨 ─────────────────────────────────────────────────────────────

        private static string TestPurifyVolumeScales()
        {
            GameEventKind[] purifies =
            {
                GameEventKind.PurifyScattered, GameEventKind.PurifyLine, GameEventKind.PurifyCluster,
            };

            foreach (GameEventKind kind in purifies)
            {
                var small = new GameEvent(kind, 1, 0, 3, 10f);
                var large = new GameEvent(kind, 1, 0, 9, 40f);
                AudioCueRequest a, b;
                AudioCueTable.TryMap(in small, out a);
                AudioCueTable.TryMap(in large, out b);
                if (b.Volume <= a.Volume)
                    return $"{kind}: 9칸 볼륨 {b.Volume:0.###} ≤ 3칸 볼륨 {a.Volume:0.###}";
            }

            // 정상 영혼도 개수가 볼륨을 만든다 — 아홉을 거둔 것이 하나와 같게 들리면 안 된다.
            var one = new GameEvent(GameEventKind.NormalSoulHarvested, 1, 0, 1, 3f);
            var nine = new GameEvent(GameEventKind.NormalSoulHarvested, 1, 0, 9, 27f);
            AudioCueRequest r1, r9;
            AudioCueTable.TryMap(in one, out r1);
            AudioCueTable.TryMap(in nine, out r9);
            if (r9.Volume <= r1.Volume)
                return $"영혼 9개 볼륨 {r9.Volume:0.###} ≤ 1개 볼륨 {r1.Volume:0.###}";
            return null;
        }

        /// <summary>
        /// 범위를 벗어난 볼륨·피치는 재생기에서 조용히 클램프되어 "왜 안 들리지"로 끝난다.
        /// 극단값을 넣어도 표가 스스로 범위를 지키는지 본다.
        /// </summary>
        private static string TestRequestRanges()
        {
            GameEventKind[] all =
            {
                GameEventKind.SpinStarted, GameEventKind.ColumnRevealed,
                GameEventKind.NormalSoulHarvested, GameEventKind.PurifyScattered,
                GameEventKind.PurifyLine, GameEventKind.PurifyCluster,
                GameEventKind.CascadeStep, GameEventKind.PowerThresholdCrossed,
                GameEventKind.ResidualDamage, GameEventKind.PowerBanked,
                GameEventKind.OverharvestUnlocked, GameEventKind.OverharvestPulled,
                GameEventKind.CollapseBegan, GameEventKind.RiskLevelChanged,
            };

            int[] extremes = { -99, 0, 1, 3, 9, 170, 999 };
            float[] floats = { -50f, 0f, 12f, 999f };

            foreach (GameEventKind kind in all)
                foreach (int i in extremes)
                    foreach (float f in floats)
                    {
                        var e = new GameEvent(kind, 1, 0, i, f);
                        AudioCueRequest req;
                        if (!AudioCueTable.TryMap(in e, out req)) return $"{kind} 매핑 없음";

                        if (float.IsNaN(req.Volume) || float.IsNaN(req.Pitch))
                            return $"{kind} i={i} f={f}: NaN";
                        if (req.Volume <= 0f || req.Volume > 1f)
                            return $"{kind} i={i} f={f}: 볼륨 {req.Volume}";
                        if (req.Pitch < AudioCueTable.MinPitch || req.Pitch > AudioCueTable.MaxPitch)
                            return $"{kind} i={i} f={f}: 피치 {req.Pitch}";
                        if (req.Variant < 0) return $"{kind} i={i}: 변형 {req.Variant}";
                    }
            return null;
        }

        private static string TestPassengerVoices()
        {
            var pitches = new float[8];
            for (int i = 0; i < 8; i++)
            {
                AudioCueRequest req = AudioCueTable.PassengerVoice(i, 0.6f);
                if (req.Kind != AudioCueKind.PassengerVoice) return $"승객 {i} 가 {req.Kind} 로 갔다";
                if (req.Pitch < AudioCueTable.MinPitch || req.Pitch > AudioCueTable.MaxPitch)
                    return $"승객 {i} 피치 {req.Pitch}";
                pitches[i] = req.Pitch;
            }

            for (int i = 0; i < pitches.Length; i++)
                for (int j = i + 1; j < pitches.Length; j++)
                    if (Math.Abs(pitches[i] - pitches[j]) < 0.01f)
                        return $"승객 {i} 와 {j} 의 목소리가 사실상 같다 ({pitches[i]:0.###})";

            // 범위 밖 인덱스가 예외를 내거나 캐시를 늘리면 안 된다.
            AudioCueRequest clamped = AudioCueTable.PassengerVoice(99, 0.6f);
            if (Math.Abs(clamped.Pitch - pitches[7]) > 0.0001f)
                return $"인덱스 99 가 조여지지 않았다 ({clamped.Pitch:0.###})";

            // 강도가 볼륨을 만든다 — 놀란 승객과 흘긋 본 승객이 같은 크기면 정보가 아니다.
            if (AudioCueTable.PassengerVoice(0, 1f).Volume
                <= AudioCueTable.PassengerVoice(0, 0f).Volume)
                return "반응 강도가 볼륨을 바꾸지 않는다";
            return null;
        }

        // ── 승객 비언어 음성 (UP-AUD-04) ─────────────────────────────────────

        /// <summary>
        /// PRD §9.2 가 세는 반응 사건에서 승객 채널이 실제로 울리는가.
        ///
        /// 이 검사가 없던 동안 <c>PassengerVoice</c> 는 **구조적으로 도달 불가**였다 —
        /// 합성기도 있고 큐 종류도 있고 재생 통로도 있었지만 그것을 부르는 코드가
        /// 어디에도 없었다. 10층 런에서 "울린 종류 14"가 나온 뒤에야 드러났고,
        /// 14 가 도달 가능한 전부였다는 사실은 로그만 봐서는 알 수 없었다.
        /// </summary>
        private static readonly GameEventKind[] VoiceEvents =
        {
            GameEventKind.ContractSelected,     // §9.2 계약 선택
            GameEventKind.PurifyScattered,      // §9.2 기본 정화
            GameEventKind.OverharvestUnlocked,  // §9.2 과수확 해금
            GameEventKind.ExtraSpinTaken,       // §9.2 추가 스핀
            GameEventKind.CollapseBegan,        // §9.2 Collapse 직전
            GameEventKind.FloorResolved,        // §9.2 사고·성공
            GameEventKind.RunEnded,
        };

        private static string TestVoiceFromReactionEvents()
        {
            var variants = new int[VoiceEvents.Length];

            for (int i = 0; i < VoiceEvents.Length; i++)
            {
                var e = new GameEvent(VoiceEvents[i], 3, 1, 4, 20f);
                AudioCueRequest req;
                if (!AudioCueTable.TryMapPassengerVoice(in e, out req))
                    return $"{VoiceEvents[i]} 에 승객이 반응하지 않는다";
                if (req.Kind != AudioCueKind.PassengerVoice)
                    return $"{VoiceEvents[i]} → {req.Kind}, 기대 PassengerVoice";
                if (req.Volume <= 0f || req.Volume > 1f)
                    return $"{VoiceEvents[i]} 볼륨 {req.Volume}";
                if (req.Pitch < AudioCueTable.MinPitch || req.Pitch > AudioCueTable.MaxPitch)
                    return $"{VoiceEvents[i]} 피치 {req.Pitch}";

                // Prewarm 이 굽는 범위를 넘으면 첫 발성이 굽는 프레임이 되고,
                // 하필 그 순간이 붕괴라 성능 캡처에 스파이크로 남는다.
                // 변형은 이제 **종류와 목소리를 함께** 싣는다(`PassengerVoices.Encode`) —
                // 그래서 "4 미만인가"가 아니라 "구워 둔 집합 안인가"를 묻는다.
                if (!PassengerVoices.IsPrewarmed(req.Variant))
                    return $"{VoiceEvents[i]} 변형 {req.Variant}" +
                           $"(종류 {PassengerVoices.KindOf(req.Variant)}, " +
                           $"목소리 {PassengerVoices.SlotOf(req.Variant)}) — Prewarm 범위 밖";

                variants[i] = PassengerVoices.SlotOf(req.Variant);
            }

            // 한 층 안에서 언제나 같은 사람만 말하면 승객이 넷이라는 사실이 소리에 없다.
            // **목소리 슬롯**으로 센다 — 변형 번호로 세면 종류만 달라도 통과해 버려서
            // "다른 사람이 말했다"를 증명하지 못한다.
            bool varied = false;
            for (int i = 1; i < variants.Length; i++)
                if (variants[i] != variants[0]) { varied = true; break; }
            if (!varied) return "모든 반응이 같은 승객에게 갔다";

            return null;
        }

        /// <summary>
        /// `MASTER_PRD.md` §9.3 은 비언어 음성으로 웃음·한숨·호흡·기도·비난 다섯을 센다.
        /// 다섯 종류를 **정의만** 해 두고 반응 배분에서 셋만 쓰면, 남은 둘은 합성기에만
        /// 있고 게임에서는 영원히 들리지 않는다 — 이 저장소가 `PassengerVoice` 자체에서
        /// 이미 겪은 형태의 실패다(「합성기도 있고 큐 종류도 있고 재생 통로도 있었지만
        /// 부르는 코드가 없었다」).
        /// </summary>
        private static string TestFiveVoiceExpressions()
        {
            int mask = 0;
            foreach (Npc.PassengerReactionEvent reaction in Npc.PassengerReactionEvents.All)
                mask |= 1 << (int)PassengerVoices.FromReaction(reaction);

            for (int k = 0; k < PassengerVoices.KindCount; k++)
                if ((mask & (1 << k)) == 0)
                    return $"음성 종류 {(PassengerVoiceKind)k} 를 아무 반응도 쓰지 않는다";

            // 열거가 늘었는데 KindCount 가 그대로면 새 종류가 **조용히** 마지막 종류로
            // 접힌다(`Encode` 가 범위를 조인다). 컴파일도 되고 소리도 나므로 아무도
            // 눈치채지 못한다 — 이 저장소가 채널 열거에서 이미 겪은 실패의 모양이다.
            int declared = Enum.GetValues(typeof(PassengerVoiceKind)).Length;
            if (declared != PassengerVoices.KindCount)
                return $"PassengerVoiceKind 멤버 {declared} 개인데 KindCount 는 {PassengerVoices.KindCount} 다";

            return null;
        }

        /// <summary>
        /// 변형 번호 하나에 두 축이 접혀 있다 — 종류(어떤 소리인가)와 슬롯(누가 냈는가).
        /// 인코딩이 어긋나면 **컴파일도 되고 소리도 나는데 종류만 틀린다.**
        /// 이 저장소가 채널 열거 두 벌에서 이미 겪은 실패와 같은 모양이다
        /// (`AudioDirector.ToMixChannel` 주석).
        /// </summary>
        private static string TestVoiceVariantEncoding()
        {
            var used = new bool[PassengerVoices.MaxVariant + 1];

            for (int k = 0; k < PassengerVoices.KindCount; k++)
                for (int s = 0; s < PassengerVoices.SlotCount; s++)
                {
                    var kind = (PassengerVoiceKind)k;
                    int variant = PassengerVoices.Encode(kind, s);

                    if (variant < 0 || variant > PassengerVoices.MaxVariant)
                        return $"{kind}/{s} → 변형 {variant} 이 상한 {PassengerVoices.MaxVariant} 밖이다";
                    if (used[variant]) return $"{kind}/{s} 의 변형 {variant} 이 다른 조합과 겹친다";
                    used[variant] = true;

                    if (PassengerVoices.KindOf(variant) != kind)
                        return $"변형 {variant} 의 종류가 {PassengerVoices.KindOf(variant)} 로 풀린다 (기대 {kind})";
                    if (PassengerVoices.SlotOf(variant) != s)
                        return $"변형 {variant} 의 슬롯이 {PassengerVoices.SlotOf(variant)} 로 풀린다 (기대 {s})";
                }

            // 범위 밖 입력이 예외를 내거나 캐시를 늘리면 안 된다.
            if (PassengerVoices.Encode((PassengerVoiceKind)99, 99) > PassengerVoices.MaxVariant)
                return "범위 밖 인코딩이 조여지지 않았다";
            if (PassengerVoices.KindOf(-7) != PassengerVoiceKind.Breath)
                return "음수 변형이 기본 종류로 조여지지 않았다";

            // **종류는 피치를 건드리지 않아야 한다.** 종류가 피치를 바꾸면
            // "누가 냈는가"가 "무슨 소리인가"에 덮여서 승객 구분이 사라진다.
            for (int s = 0; s < PassengerVoices.SlotCount; s++)
            {
                float basePitch = AudioCueTable.PassengerVoice(s, PassengerVoiceKind.Breath, 0.6f).Pitch;
                for (int k = 1; k < PassengerVoices.KindCount; k++)
                {
                    AudioCueRequest req = AudioCueTable.PassengerVoice(s, (PassengerVoiceKind)k, 0.6f);
                    if (Math.Abs(req.Pitch - basePitch) > 0.0001f)
                        return $"슬롯 {s}: 종류 {(PassengerVoiceKind)k} 가 피치를 {req.Pitch:0.###} 로 바꿨다 " +
                               $"(기대 {basePitch:0.###})";
                    if (PassengerVoices.SlotOf(req.Variant) != s)
                        return $"슬롯 {s} 가 종류 {(PassengerVoiceKind)k} 에서 사라졌다";
                }
            }

            return null;
        }

        /// <summary>
        /// `PassengerReactionSet` 의 큐 ID 가 전부 음성 종류로 풀리는가.
        ///
        /// **이것이 UP-NPC-04 의 「큐 ID 만 있고 재생 배선이 없다」를 막는 검사다.**
        /// 반응 데이터에 새 큐 ID 를 적어 넣고 오디오 쪽에 그 ID 를 모르면, 승객은
        /// 몸으로는 반응하는데 소리는 기본값(호흡)으로 떨어진다 — 화면만 봐서는
        /// "그 반응은 원래 조용한가"와 구분되지 않는다.
        /// </summary>
        /// <summary>
        /// 대조 반응(`PassengerReactionSet.DefaultContrastFor`)의 큐 ID.
        ///
        /// **문자열로 베껴 둔 것은 의도적이다.** 그 API 는 지금 다른 소유 영역
        /// (`Scripts/Npc`)에서 만들어지는 중이라, 여기서 타입을 이름으로 붙들면
        /// 그쪽이 이름을 한 번 바꿀 때 **오디오가 아니라 전체 어셈블리**가 컴파일되지
        /// 않는다(asmdef 이 없다). 그 API 가 굳으면 이 배열을 지우고
        /// `DefaultContrastFor` 를 순회하는 것이 맞다.
        ///
        /// 대조 반응의 소리가 주 반응과 같으면 §9.3 의 「같은 사건에 상반된 반응」이
        /// 자세에만 남고 귀로는 사라진다 — 그래서 여기가 비어 있으면 안 된다.
        /// </summary>
        private static readonly string[] ContrastCueIds =
        {
            "npc_murmur_doubt", "npc_tsk", "npc_gasp", "npc_breath_hold",
            "npc_murmur_rise", "npc_awe", "npc_whimper", "npc_murmur_urge",
            "npc_cheer_wild", "npc_scream_long", "npc_flinch_short",
        };

        private static string TestVoiceCueIdsResolve()
        {
            foreach (string cue in ContrastCueIds)
            {
                PassengerVoiceKind contrastKind;
                if (!PassengerVoices.TryFromCueId(cue, out contrastKind))
                    return $"대조 반응의 큐 ID \"{cue}\" 를 오디오가 모른다";
            }

            // 같은 사건의 주 반응과 대조 반응이 같은 소리면 대비가 소리에서 사라진다.
            // 가장 크게 갈려야 하는 한 쌍(5연쇄: 환호 ↔ 공포)으로 확인한다.
            PassengerVoiceKind cheer, brace;
            PassengerVoices.TryFromCueId("npc_awe", out cheer);
            PassengerVoices.TryFromCueId("npc_gasp", out brace);
            if (cheer == brace)
                return $"5연쇄의 환호와 공포가 같은 소리({cheer})로 간다";

            foreach (Npc.PassengerReactionEvent reaction in Npc.PassengerReactionEvents.All)
            {
                Npc.PassengerReaction data = Npc.PassengerReactionSet.DefaultFor(reaction);
                if (string.IsNullOrEmpty(data.VoiceCue)) continue;

                PassengerVoiceKind kind;
                if (!PassengerVoices.TryFromCueId(data.VoiceCue, out kind))
                    return $"{Npc.PassengerReactionEvents.DisplayName(reaction)} 의 큐 ID " +
                           $"\"{data.VoiceCue}\" 를 오디오가 모른다";
            }

            // 모르는 ID 는 조용히 기본값으로 떨어지지 않고 false 여야 한다 —
            // 그래야 오타와 의도적인 호흡 선택이 구분된다.
            PassengerVoiceKind unknown;
            if (PassengerVoices.TryFromCueId("npc_저것은_없는_큐", out unknown))
                return "없는 큐 ID 가 참을 돌려준다";
            if (PassengerVoices.TryFromCueId(null, out unknown))
                return "null 큐 ID 가 참을 돌려준다";
            if (unknown != PassengerVoiceKind.Breath)
                return $"실패 경로의 기본 종류가 {unknown} 다 (기대 Breath)";

            return null;
        }

        /// <summary>
        /// 반응 목록에 없는 사건은 조용해야 한다. 열이 공개될 때마다 누가 소리를 내면
        /// 정작 5연쇄와 붕괴가 묻힌다(`PassengerReactionEvents.TryMap` 주석과 같은 이유).
        ///
        /// 「과수확 접근」이 여기 있는 것은 특히 중요하다 — PRD §7.3 이 그 순간을
        /// **정적**으로 규정한다. 조용하게 만들라고 한 자리에 숨소리를 하나 넣으면
        /// 이 층에서 가장 긴 침묵이 그냥 없어진다.
        /// </summary>
        private static string TestNonReactionEventsAreVoiceless()
        {
            GameEventKind[] silent =
            {
                GameEventKind.None,
                GameEventKind.FloorStarted,
                GameEventKind.ItemBoarded,
                GameEventKind.BoardingFinished,
                GameEventKind.SpinStarted,
                GameEventKind.ColumnRevealed,
                GameEventKind.NormalSoulHarvested,
                GameEventKind.PurifyLine,          // 「기본 정화」는 개수 정화만이다
                GameEventKind.PurifyCluster,
                GameEventKind.CascadeCapReached,
                GameEventKind.SpinResolved,
                GameEventKind.ResidualDamage,
                GameEventKind.PowerBanked,
                GameEventKind.OverharvestApproached,   // §7.3 정적
                GameEventKind.OverharvestReleased,
                GameEventKind.JettisonPaid,
            };

            foreach (GameEventKind kind in silent)
            {
                // IntValue 4 는 어느 임계점(100·170·300)도 아니고 5연쇄 깊이도 아니다.
                var e = new GameEvent(kind, 3, 1, 4, 20f);
                AudioCueRequest req;
                if (AudioCueTable.TryMapPassengerVoice(in e, out req))
                    return $"{kind} 에 승객이 소리를 낸다 — 반응 목록에 없는 사건이다";
                if (req.Kind != AudioCueKind.None)
                    return $"{kind} 실패 경로가 req 를 비우지 않았다 ({req.Kind})";
            }

            // 깊이 4 는 5연쇄가 아니다. 여기서 울리면 「5연쇄」라는 말이 뜻을 잃는다.
            var shallow = new GameEvent(GameEventKind.CascadeStep, 3, 1, 4, 20f);
            AudioCueRequest shallowReq;
            if (AudioCueTable.TryMapPassengerVoice(in shallow, out shallowReq))
                return "캐스케이드 깊이 4 에 승객이 반응한다";

            // 임계점이 아닌 퍼센트도 마찬가지다.
            var offThreshold = new GameEvent(GameEventKind.PowerThresholdCrossed, 3, 1, 140);
            AudioCueRequest offReq;
            if (AudioCueTable.TryMapPassengerVoice(in offThreshold, out offReq))
                return "임계점이 아닌 140% 에 승객이 반응한다";

            return null;
        }

        /// <summary>
        /// 같은 사건은 언제 물어도 같은 승객·같은 피치여야 한다. 난수를 쓰면
        /// 같은 시드의 런이 다른 소리를 내고, 캡처 회귀가 오디오 때문에 흔들린다.
        /// </summary>
        private static string TestVoiceIsDeterministic()
        {
            var e = new GameEvent(GameEventKind.ContractSelected, 2, 0);

            AudioCueRequest a, b;
            if (!AudioCueTable.TryMapPassengerVoice(in e, out a)) return "첫 호출이 실패했다";
            if (!AudioCueTable.TryMapPassengerVoice(in e, out b)) return "둘째 호출이 실패했다";

            if (a.Variant != b.Variant || a.Pitch != b.Pitch || a.Volume != b.Volume)
                return $"같은 사건이 다른 목소리를 냈다 ({a} vs {b})";

            // 층이 다르면 갈릴 수 있어야 한다 — 열 층 내내 같은 사람만 말하지 않는다.
            bool anyDifferent = false;
            for (int floor = 1; floor <= 10; floor++)
            {
                var other = new GameEvent(GameEventKind.ContractSelected, floor, 0);
                AudioCueRequest req;
                AudioCueTable.TryMapPassengerVoice(in other, out req);
                if (req.Variant != a.Variant) { anyDifferent = true; break; }
            }
            if (!anyDifferent) return "열 층 내내 같은 승객만 말한다";

            return null;
        }

        // ── 사이렌 (UP-RISK-05) ──────────────────────────────────────────────

        /// <summary>
        /// Notion MASTER PRD §8.3 — "사이렌은 지속 재생하지 않는다. **단계 상승·과수확
        /// 해금·레버 결정·사고 순간에만** 강하게 사용한다."
        ///
        /// 목록을 늘리는 것은 의도적인 결정이어야 한다. 사이렌이 흔해지는 순간
        /// 그것은 신호가 아니라 배경이 되고, `VISUAL_BIBLE.md` 금지 16번
        /// (「지속 재생되는 사이렌」)이 다른 이름으로 재현된다.
        /// </summary>
        private static string TestSirenOnlyOnFourMoments()
        {
            foreach (GameEventKind kind in Enum.GetValues(typeof(GameEventKind)))
            {
                // 단계 전이는 값에 따라 갈리므로 아래에서 따로 본다.
                if (kind == GameEventKind.RiskLevelChanged) continue;

                bool allowed = kind == GameEventKind.OverharvestUnlocked
                            || kind == GameEventKind.OverharvestPulled
                            || kind == GameEventKind.CollapseBegan;

                var e = new GameEvent(kind, 1, 0, 3, 12f);
                AudioCueRequest req;
                bool fired = AudioCueTable.TryMapSiren(in e, out req);

                if (fired != allowed)
                    return allowed ? $"{kind} 에서 사이렌이 빠졌다"
                                   : $"{kind} 가 사이렌을 울린다 — §8.3 의 네 순간이 아니다";
                if (fired && req.Kind != AudioCueKind.Siren)
                    return $"{kind} → {req.Kind}, 기대 Siren";
                if (!fired && req.Kind != AudioCueKind.None)
                    return $"{kind} 실패 경로가 req 를 비우지 않았다 ({req.Kind})";
            }

            // 안정(Stable)으로 돌아온 것은 경보가 아니다.
            for (int level = 0; level <= 3; level++)
            {
                var e = new GameEvent(GameEventKind.RiskLevelChanged, 1, -1, level, 0.5f);
                AudioCueRequest req;
                bool fired = AudioCueTable.TryMapSiren(in e, out req);
                bool expected = level >= AudioCueTable.SirenMinRiskLevel;
                if (fired != expected)
                    return $"위험 단계 {level}: 사이렌 {(fired ? "울림" : "없음")}, " +
                           $"기대 {(expected ? "울림" : "없음")}";
            }

            return null;
        }

        /// <summary>
        /// 넷이 같은 소리면 "경보가 켜졌다"는 사실만 남고 **왜** 켜졌는지가 사라진다.
        /// 변형 번호가 달라야 <c>ProceduralClipFactory</c> 가 다른 파형을 굽는다.
        /// </summary>
        private static string TestSirenVariantsDiffer()
        {
            GameEvent[] moments =
            {
                new GameEvent(GameEventKind.RiskLevelChanged, 1, -1, 2, 0.6f),
                new GameEvent(GameEventKind.OverharvestUnlocked, 1, 0),
                new GameEvent(GameEventKind.OverharvestPulled, 1, 0, 0, 40f),
                new GameEvent(GameEventKind.CollapseBegan, 1, 0),
            };

            var variants = new int[moments.Length];
            for (int i = 0; i < moments.Length; i++)
            {
                AudioCueRequest req;
                if (!AudioCueTable.TryMapSiren(in moments[i], out req))
                    return $"{moments[i].Kind} 에서 사이렌이 빠졌다";

                if (req.Volume <= 0f || req.Volume > 1f)
                    return $"{moments[i].Kind} 사이렌 볼륨 {req.Volume}";
                if (req.Pitch < AudioCueTable.MinPitch || req.Pitch > AudioCueTable.MaxPitch)
                    return $"{moments[i].Kind} 사이렌 피치 {req.Pitch}";

                // Prewarm 이 굽는 범위(0~3) 밖이면 첫 사이렌이 굽는 프레임이 된다.
                if (req.Variant < 0 || req.Variant > 3)
                    return $"{moments[i].Kind} 사이렌 변형 {req.Variant} — Prewarm 범위 밖";

                variants[i] = req.Variant;
            }

            for (int i = 0; i < variants.Length; i++)
                for (int j = i + 1; j < variants.Length; j++)
                    if (variants[i] == variants[j])
                        return $"{moments[i].Kind} 와 {moments[j].Kind} 가 같은 사이렌으로 뭉쳤다";

            // 단계가 깊을수록 크게. 같은 크기면 Strain 과 Collapse 가 구분되지 않는다.
            var strain = new GameEvent(GameEventKind.RiskLevelChanged, 1, -1, 1, 0.3f);
            var collapse = new GameEvent(GameEventKind.RiskLevelChanged, 1, -1, 3, 0.9f);
            AudioCueRequest low, high;
            AudioCueTable.TryMapSiren(in strain, out low);
            AudioCueTable.TryMapSiren(in collapse, out high);
            if (high.Volume <= low.Volume)
                return $"Collapse 사이렌 {high.Volume:0.###} ≤ Strain {low.Volume:0.###}";

            return null;
        }

        // ── 지속 위험 레이어 (UP-RISK-05) ────────────────────────────────────
        //
        // 사이렌 검사(위)와 **짝을 이룬다.** 사이렌이 네 순간에만 울린다는 것만 지키면
        // 위험 단계가 오른 뒤 아무 사건도 없는 구간이 통째로 조용해진다. §8.3 은
        // 사이렌을 금지한 자리를 저주파와 금속 응력음으로 채우라고 지정했고,
        // 아래 셋이 그것이 실제로 채워졌는지 눈금으로 묻는다.

        /// <summary>단계별 험 볼륨을 흉내 낸 표본. 표준 프리셋의 `RiskProfile.HumVolume` 이다.</summary>
        private static readonly float[] SampleHumVolume = { 0.10f, 0.20f, 0.34f, 0.42f };

        private static string TestDangerBedRises()
        {
            // 험 볼륨을 **고정**해서 부른다. 프로파일 값이 이미 오르는 값이라
            // 그대로 넣으면 "가중치가 오른다"인지 "험이 올라서다"인지 갈리지 않는다.
            const float hum = 0.30f;

            float previousSub = -1f;
            float previousStress = -1f;
            float previousPitch = -1f;

            for (int level = 0; level < DangerBed.LevelCount; level++)
            {
                DangerBedTargets t = DangerBed.Evaluate(
                    (Risk.RiskLevel)level, hum, 1f, 1f, 1f, 1f);

                if (t.StressVariant != level)
                    return $"단계 {level} 의 응력 파형이 {t.StressVariant} 로 간다";

                if (t.SubVolume <= previousSub)
                    return $"단계 {level} 저역 {t.SubVolume:0.####} ≤ 단계 {level - 1} {previousSub:0.####}";
                if (t.StressPitch <= previousPitch)
                    return $"단계 {level} 응력 피치 {t.StressPitch:0.###} ≤ 단계 {level - 1} {previousPitch:0.###}";

                // 안정(0)의 응력은 0 이므로 "커진다"는 1단계부터 잰다.
                if (level > 0 && t.StressVolume <= previousStress)
                    return $"단계 {level} 응력 {t.StressVolume:0.####} ≤ 단계 {level - 1} {previousStress:0.####}";

                previousSub = t.SubVolume;
                previousStress = t.StressVolume;
                previousPitch = t.StressPitch;
            }

            // 실제 프로파일 값으로도 같은 방향이어야 한다 — 험 자체가 Collapse 에서
            // 조금 꺾여도(표준 프리셋 0.42) 지속층은 더 두꺼워져야 한다.
            float last = -1f;
            for (int level = 0; level < DangerBed.LevelCount; level++)
            {
                DangerBedTargets t = DangerBed.Evaluate(
                    (Risk.RiskLevel)level, SampleHumVolume[level], 1f, 1f, 1f, 1f);
                if (t.SubVolume <= last)
                    return $"프로파일 값에서 단계 {level} 저역이 늘지 않았다 ({t.SubVolume:0.####})";
                last = t.SubVolume;
            }

            return null;
        }

        /// <summary>
        /// 안정 단계에서 삐걱이면 그건 안정이 아니다. 응력음이 늘 깔려 있으면
        /// Strain 으로 올라간 것을 귀로 알 수 없고, 그러면 이 레이어는 위험 표현이 아니라
        /// 배경음이 된다 — `VISUAL_BIBLE.md` 금지 16번(「지속 재생되는 사이렌」)이
        /// 다른 이름으로 재현되는 것과 같은 실패다.
        ///
        /// 반대로 **저역은 안정에서도 0 이 아니어야 한다.** 0 이면 위험이 오를 때
        /// 「소리가 생겼다」로 들려서 그 순간이 사건처럼 읽히고, 지속층이라는 성질이 사라진다.
        /// </summary>
        private static string TestDangerBedQuietWhenStable()
        {
            DangerBedTargets stable = DangerBed.Evaluate(
                Risk.RiskLevel.Stable, 0.10f, 1f, 1f, 1f, 1f);

            if (stable.StressVolume != 0f)
                return $"안정 단계 응력음 {stable.StressVolume:0.####} — 0 이어야 한다";
            if (stable.SubVolume <= 0f)
                return "안정 단계 저역이 0 이다 — 지속층이 위험과 함께 생겨나면 안 된다";

            // 배율을 아무리 올려도 안정의 응력은 0 이다. 가중치가 아니라 **구조**여야 한다.
            DangerBedTargets loud = DangerBed.Evaluate(
                Risk.RiskLevel.Stable, 1f, 1f, 2f, 2f, 1f);
            if (loud.StressVolume != 0f)
                return $"배율을 올리자 안정 단계에 응력음이 생겼다 ({loud.StressVolume:0.####})";

            return null;
        }

        private static string TestDangerBedFollowsInputs()
        {
            for (int level = 0; level < DangerBed.LevelCount; level++)
            {
                var risk = (Risk.RiskLevel)level;

                // 과수확 정적(§7.3) — 게인 0 이면 지속층도 사라진다. 남아 있으면
                // 「방이 조용해졌다」가 아니라 「사건음만 꺼졌다」가 된다.
                DangerBedTargets silent = DangerBed.Evaluate(risk, 0.5f, 1f, 1f, 1f, 0f);
                if (silent.SubVolume != 0f || silent.StressVolume != 0f)
                    return $"단계 {level}: 정적 게인 0 인데 {silent}";

                // 프로파일이 험을 끄면 지속층도 없다 — 값의 출처가 하나라는 증거다.
                DangerBedTargets noHum = DangerBed.Evaluate(risk, 0f, 1f, 1f, 1f, 1f);
                if (noHum.SubVolume != 0f || noHum.StressVolume != 0f)
                    return $"단계 {level}: 험 0 인데 지속층이 남았다 ({noHum})";

                // 배율 0 이면 그 층만 사라진다. 둘이 한 스위치로 묶여 있으면
                // 저주파를 끈 사람에게서 위험의 청각 채널이 통째로 없어진다.
                DangerBedTargets noSub = DangerBed.Evaluate(risk, 0.5f, 1f, 0f, 1f, 1f);
                if (noSub.SubVolume != 0f) return $"단계 {level}: 저역 배율 0 인데 {noSub.SubVolume:0.####}";
                if (level > 0 && noSub.StressVolume <= 0f)
                    return $"단계 {level}: 저역을 껐더니 응력음까지 사라졌다";

                // 극단값에서도 범위를 지킨다. 범위 밖 볼륨은 재생기에서 조용히 잘려
                // "왜 안 들리지"로 끝난다.
                float[] hums = { -5f, 0f, 0.3f, 50f };
                float[] pitches = { -1f, 0f, 1f, 99f };
                foreach (float hum in hums)
                    foreach (float pitch in pitches)
                    {
                        DangerBedTargets t = DangerBed.Evaluate(risk, hum, pitch, 3f, 3f, 1f);
                        if (float.IsNaN(t.SubVolume) || float.IsNaN(t.StressVolume)) return $"단계 {level}: NaN";
                        if (t.SubVolume < 0f || t.SubVolume > 1f) return $"단계 {level}: 저역 {t.SubVolume}";
                        if (t.StressVolume < 0f || t.StressVolume > 1f) return $"단계 {level}: 응력 {t.StressVolume}";
                        if (t.SubPitch < DangerBed.MinPitch || t.SubPitch > DangerBed.MaxPitch)
                            return $"단계 {level}: 저역 피치 {t.SubPitch}";
                        if (t.StressPitch < DangerBed.MinPitch || t.StressPitch > DangerBed.MaxPitch)
                            return $"단계 {level}: 응력 피치 {t.StressPitch}";
                    }
            }

            // 저역 피치는 위험 프로파일의 험 피치를 따라간다 — 같은 기계이기 때문이다.
            DangerBedTargets low = DangerBed.Evaluate(Risk.RiskLevel.Collapse, 0.4f, 0.78f, 1f, 1f, 1f);
            if (Math.Abs(low.SubPitch - 0.78f) > 0.0001f)
                return $"저역 피치가 험 피치를 따라가지 않는다 ({low.SubPitch:0.###})";

            // 응력음 피치는 따라가지 **않는다.** 표준 프리셋의 험 피치는 Collapse 에서
            // 0.78 로 떨어지는데, 그 형태를 그대로 쓰면 응력음이 붕괴 직전에 가장
            // 느슨해진다. 금속은 부러지기 직전에 가장 조여 있다.
            if (low.StressPitch <= DangerBed.StressPitchAt(Risk.RiskLevel.Critical))
                return $"Collapse 응력 피치 {low.StressPitch:0.###} ≤ Critical " +
                       $"{DangerBed.StressPitchAt(Risk.RiskLevel.Critical):0.###}";

            return null;
        }

        // ── 정적 구간 ────────────────────────────────────────────────────────

        private static SilenceWindow Window()
        {
            var w = new SilenceWindow();
            w.DuckSeconds = 0.1f;
            w.ResumeSeconds = 0.2f;
            return w;
        }

        private static string TestSilenceBoundaries()
        {
            SilenceWindow w = Window();

            // 한 번도 시작하지 않았으면 언제 물어도 1이다. 과수확에 손을 대기 전의 방은
            // 조용하지 않다.
            if (!Near(w.GainAt(0f), 1f)) return $"시작 전 게인 {w.GainAt(0f)}";
            if (!Near(w.GainAt(1000f), 1f)) return "시작 전 게인이 시간에 따라 변한다";

            const float begin = 10f;
            w.Begin(begin, 0.5f);

            if (!Near(w.GainAt(begin - 1f), 1f)) return "시작 이전 시각의 게인이 1이 아니다";
            if (!Near(w.GainAt(begin), 1f)) return $"시작 순간 게인 {w.GainAt(begin)}, 기대 1";

            float silenceStart = begin + w.DuckSeconds;
            float silenceEnd = silenceStart + w.SilenceSeconds;

            // 정적은 "거의 0"이 아니라 0이다. §7.3(4)가 요구하는 것은 감쇠가 아니라 정적이다.
            if (w.GainAt(silenceStart) != 0f) return $"정적 시작 게인 {w.GainAt(silenceStart)}";
            if (w.GainAt((silenceStart + silenceEnd) * 0.5f) != 0f) return "정적 중간이 0이 아니다";
            if (w.GainAt(silenceEnd - 0.001f) != 0f) return "정적 끝 직전이 0이 아니다";

            if (!Near(w.GainAt(silenceEnd + w.ResumeSeconds), 1f))
                return $"재개 완료 게인 {w.GainAt(silenceEnd + w.ResumeSeconds)}";
            if (!Near(w.GainAt(begin + 100f), 1f)) return "한참 뒤 게인이 1로 돌아오지 않았다";
            return null;
        }

        private static string TestSilenceMonotonic()
        {
            SilenceWindow w = Window();
            const float begin = 5f;
            w.Begin(begin, 0.4f);

            // 감쇠 — 단조 감소
            float previous = w.GainAt(begin);
            for (int i = 1; i <= 40; i++)
            {
                float t = begin + w.DuckSeconds * (i / 40f);
                float g = w.GainAt(t);
                if (g > previous + 0.0001f)
                    return $"감쇠 중 게인이 올라갔다 ({previous:0.####} → {g:0.####})";
                previous = g;
            }
            // 마지막 표본은 부동소수 때문에 경계에 정확히 닿지 않을 수 있다. 정확한 0은
            // 경계 안쪽에서 따로 확인한다 — 그 확인이 §7.3(4)가 요구하는 "정적"의 근거다.
            if (!Near(previous, 0f, 0.01f)) return $"감쇠가 끝났는데 게인 {previous}";
            if (w.GainAt(begin + w.DuckSeconds + 0.01f) != 0f) return "감쇠 직후가 정확히 0이 아니다";

            // 재개 — 단조 증가
            float resumeStart = begin + w.DuckSeconds + w.SilenceSeconds;
            previous = 0f;
            for (int i = 1; i <= 40; i++)
            {
                float t = resumeStart + w.ResumeSeconds * (i / 40f);
                float g = w.GainAt(t);
                if (g < previous - 0.0001f)
                    return $"재개 중 게인이 내려갔다 ({previous:0.####} → {g:0.####})";
                previous = g;
            }
            if (!Near(previous, 1f)) return $"재개가 끝났는데 게인 {previous}";
            return null;
        }

        /// <summary>
        /// `MASTER_PRD.md` §7.3(4)은 0.3~0.7초라고 못박는다. 프로파일 값이 잘못 들어와도
        /// 그 약속은 코드가 지켜야 한다 — 데이터 실수 하나로 대표 장면이 무너지면 안 된다.
        /// </summary>
        private static string TestSilenceClamped()
        {
            SilenceWindow w = Window();

            w.Begin(0f, 0.05f);
            if (!Near(w.SilenceSeconds, SilenceWindow.MinSilenceSeconds))
                return $"0.05 요청 → {w.SilenceSeconds}, 기대 {SilenceWindow.MinSilenceSeconds}";

            w.Begin(0f, -3f);
            if (!Near(w.SilenceSeconds, SilenceWindow.MinSilenceSeconds))
                return $"음수 요청 → {w.SilenceSeconds}";

            w.Begin(0f, 5f);
            if (!Near(w.SilenceSeconds, SilenceWindow.MaxSilenceSeconds))
                return $"5초 요청 → {w.SilenceSeconds}, 기대 {SilenceWindow.MaxSilenceSeconds}";

            w.Begin(0f, 0.45f);
            if (!Near(w.SilenceSeconds, 0.45f)) return $"범위 안 값이 변형됐다 → {w.SilenceSeconds}";

            // 조인 결과가 실제로 타임라인에 반영됐는가. SilenceSeconds 만 맞고 GainAt 이
            // 옛 길이를 쓰고 있으면 눈금을 재는 쪽이 속는다.
            w.Begin(0f, 5f);
            float end = w.DuckSeconds + SilenceWindow.MaxSilenceSeconds;
            if (w.GainAt(end - 0.01f) != 0f) return "상한으로 조인 정적이 끝나기 전에 소리가 돌아왔다";
            if (!Near(w.GainAt(end + w.ResumeSeconds + 0.01f), 1f))
                return "상한으로 조인 정적이 제때 끝나지 않았다";
            return null;
        }

        private static string TestSilenceFlags()
        {
            SilenceWindow w = Window();
            if (w.IsActive(0f)) return "시작 전에 IsActive 가 참이다";
            if (w.IsSilent(0f)) return "시작 전에 IsSilent 가 참이다";

            const float begin = 2f;
            w.Begin(begin, 0.6f);

            if (!w.IsActive(begin + 0.01f)) return "감쇠 중인데 IsActive 가 거짓이다";
            if (w.IsSilent(begin + 0.01f)) return "감쇠 중인데 벌써 IsSilent 다";

            float mid = begin + w.DuckSeconds + w.SilenceSeconds * 0.5f;
            if (!w.IsSilent(mid)) return "정적 한가운데인데 IsSilent 가 거짓이다";
            if (!w.IsActive(mid)) return "정적 한가운데인데 IsActive 가 거짓이다";

            float after = begin + w.TotalSeconds + 0.01f;
            if (w.IsActive(after)) return "다 끝났는데 IsActive 가 참이다";
            if (w.IsSilent(after)) return "다 끝났는데 IsSilent 가 참이다";

            w.Reset();
            if (w.HasBegun) return "Reset 후에도 HasBegun 이 참이다";
            if (!Near(w.GainAt(mid), 1f)) return "Reset 후 게인이 1이 아니다";
            return null;
        }

        private static string TestSilenceCancel()
        {
            SilenceWindow w = Window();
            const float begin = 1f;
            w.Begin(begin, 0.5f);

            float at = begin + w.DuckSeconds * 0.5f;
            float before = w.GainAt(at);
            if (before <= 0f || before >= 1f) return $"선행 조건 실패 — 감쇠 중 게인 {before}";

            w.Cancel(at);

            // 1로 튕겨 올리지 않는다. 손을 뗀 순간 원래 크기로 돌아오면 물러남이 사라진다.
            if (!Near(w.GainAt(at), before)) return $"취소 순간 게인이 튀었다 ({w.GainAt(at)} vs {before})";
            if (w.IsSilent(at + 0.2f)) return "취소했는데 정적으로 들어갔다";

            float end = at + w.ResumeSeconds + 0.01f;
            if (!Near(w.GainAt(end), 1f)) return $"취소 후 재개가 끝났는데 게인 {w.GainAt(end)}";
            if (w.IsActive(end)) return "취소 후 재개가 끝났는데 IsActive 가 참이다";

            // 취소가 단조 증가인지도 본다.
            float previous = before;
            for (int i = 1; i <= 20; i++)
            {
                float g = w.GainAt(at + w.ResumeSeconds * (i / 20f));
                if (g < previous - 0.0001f) return $"취소 후 게인이 내려갔다 ({previous:0.###} → {g:0.###})";
                previous = g;
            }
            return null;
        }

        private static bool Near(float a, float b, float tolerance = 0.0005f)
            => Math.Abs(a - b) <= tolerance;
    }
}
