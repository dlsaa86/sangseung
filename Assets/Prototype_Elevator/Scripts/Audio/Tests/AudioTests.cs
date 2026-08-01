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
