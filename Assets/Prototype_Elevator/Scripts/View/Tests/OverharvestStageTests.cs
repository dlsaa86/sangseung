using System;
using System.Text;
using Ascend.Prototype.Data.Profiles;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.View.Tests
{
    /// <summary>
    /// 과수확 5단계 연출의 헤드리스 검증 (`UP-POWER-06`, `MASTER_PRD.md` §7.3).
    ///
    /// **무엇을 반증하려는가**: 이 항목의 실패는 「연출이 없다」가 아니라
    /// 「연출을 만들었는데 아무도 그것이 돌았는지 모른다」였다. 독립 감사가 잡아낸 두 가지가
    /// ① 5단계 구현이 0 이고 ② 1단계가 어떤 런에서도 발화한 적이 없다는 것인데,
    /// 둘 다 **관측 수단이 없어서** 여러 세션 동안 발견되지 않았다.
    ///
    /// 그래서 여기서 세는 것은 총합이 아니라 **단계별 카운터**다. 총합만 있으면
    /// 1단계가 다섯 번 떠도 「다섯 단계가 다 돌았다」와 구별되지 않는다.
    ///
    /// 씬을 열지 않는다. 판정은 <see cref="OverharvestStageTimeline"/> 이 전부 들고 있고
    /// MonoBehaviour 는 그 값을 렌더러·Transform 에 바르기만 한다.
    ///
    /// NUnit 에 의존하지 않는 이유는 `DECISION_LOG.md` D-20260730-06 참조.
    /// </summary>
    public static class OverharvestStageTests
    {
        private const int Seed = 4242;

        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0;
            int failed = 0;
            var report = new StringBuilder();

            Run("다섯 단계가 PRD §7.3 순서대로 발화한다", TestStageOrder, ref passed, ref failed, report);
            Run("완주하면 각 단계가 정확히 한 번씩 발화한다", TestEachStageFiresOnce, ref passed, ref failed, report);
            Run("접근 없이 당겨도 5단계는 발화한다 (하네스 경로)", TestPullWithoutApproach, ref passed, ref failed, report);
            Run("1~4단계가 죽으면 카운터가 0으로 남는다 (반증 가능)", TestMissingStagesStayZero, ref passed, ref failed, report);
            Run("세 채널이 같은 순간에 재개한다", TestResumeIsSimultaneous, ref passed, ref failed, report);
            Run("세 채널이 같은 시각에 값을 움직이기 시작한다", TestChannelsDeviateTogether, ref passed, ref failed, report);
            Run("재개가 ResumeFadeSeconds 안에 끝난다", TestResumeWindowLength, ref passed, ref failed, report);
            Run("ResumeFadeSeconds 를 바꾸면 재개 창이 따라 움직인다", TestResumeFollowsProfile, ref passed, ref failed, report);
            Run("ApproachMachineDuckScale 이 감쇠 바닥값이 된다", TestDuckScaleFollowsProfile, ref passed, ref failed, report);
            Run("정적 구간이 완전 정지(0)다", TestSilenceIsFullStop, ref passed, ref failed, report);
            Run("정적 길이가 PRD 범위 안이고 시드에서 재현된다", TestSilenceLengthDeterministic, ref passed, ref failed, report);
            Run("응시 지연이 정적보다 앞선다 (단계 3 → 4)", TestGazePrecedesSilence, ref passed, ref failed, report);
            Run("진동은 평소 0이고 당길 때만 생긴다", TestShakeIsEventOnly, ref passed, ref failed, report);
            Run("재개 버스트가 홈 각도로 돌아온다 (캡처 안전)", TestBurstReturnsHome, ref passed, ref failed, report);
            Run("당기지 않고 버티면 스스로 돌아온다", TestSilenceSelfRecovers, ref passed, ref failed, report);
            Run("조준을 거두면 5단계로 세지 않는다", TestReleaseIsNotResume, ref passed, ref failed, report);
            Run("배선하지 않으면 출처가 「코드 프리셋」이다", TestUnwiredReportsCodePreset, ref passed, ref failed, report);
            Run("런을 다시 시작하면 카운터가 0이 된다", TestResetClearsCounters, ref passed, ref failed, report);

            // ── 순차 공개 (`UP-FIX-20` / `UP-CORE-11`) ────────────────────────
            // 새 스위트를 만들지 않는 이유: 스위트 하나마다 `AscendTestMenu` 와
            // `PrototypeSelfTest` 두 곳에 등록이 필요하고, 등록을 빠뜨린 검사는
            // **돌지 않으면서 통과한 것처럼 보인다.** 여기에 붙이면 등록이 0줄이다.
            Run("순차 공개 — 한 프레임에 세 단계가 동시에 존재한다", TestRevealHasThreeStages, ref passed, ref failed, report);
            Run("순차 공개 — 「아직 안 열림」과 「빈 판」이 다르다", TestSealedIsNotEmpty, ref passed, ref failed, report);
            Run("순차 공개 — 열린 열은 되돌아가지 않는다", TestRevealIsMonotonic, ref passed, ref failed, report);
            Run("순차 공개 — 열리는 열은 언제나 하나뿐이다", TestExactlyOneOpeningColumn, ref passed, ref failed, report);
            Run("순차 공개 — 칸 인덱스 매핑이 SpinBoard 와 같다", TestRevealCellMatchesColumn, ref passed, ref failed, report);
            Run("순차 공개 — 셔터가 막대 풀을 넘지 않는다", TestRevealFitsInPool, ref passed, ref failed, report);

            report.Insert(0, "[상승] === 과수확 5단계 연출 Tests ===\n");
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

        // ── 도구 ─────────────────────────────────────────────────────────────

        private static OverharvestStageTimeline Fresh()
        {
            var timeline = new OverharvestStageTimeline();
            timeline.Configure(OverharvestProfile.DefaultSnapshot, "테스트",
                               OverharvestStageTimeline.DefaultDuckSeconds,
                               OverharvestStageTimeline.DefaultBurstOvershoot);
            return timeline;
        }

        private static OverharvestStageTimeline With(OverharvestSnapshot snapshot)
        {
            var timeline = new OverharvestStageTimeline();
            timeline.Configure(snapshot, "테스트",
                               OverharvestStageTimeline.DefaultDuckSeconds,
                               OverharvestStageTimeline.DefaultBurstOvershoot);
            return timeline;
        }

        /// <summary>기본값에서 한 필드만 바꾼 스냅샷. 반증 대조용이다.</summary>
        private static OverharvestSnapshot Tweaked(float duckScale, float resumeSeconds,
                                                   float gazeDelay, float minSilence, float maxSilence)
        {
            OverharvestSnapshot d = OverharvestProfile.DefaultSnapshot;
            return new OverharvestSnapshot(d.AnteRatio, d.AnteEscalation, d.UnlockThreshold,
                duckScale, minSilence, maxSilence, gazeDelay, resumeSeconds, d.MaxExtraSpins);
        }

        /// <summary>접근 → 정적 끝까지 시간을 흘린다. 반환값은 마지막 시각.</summary>
        private static float RunApproach(OverharvestStageTimeline timeline, float start)
        {
            timeline.Approach(start, Seed);
            float end = start + timeline.SilenceStartOffset + timeline.SilenceSeconds;
            for (float t = start; t <= end; t += 0.01f) timeline.Tick(t);
            timeline.Tick(end);
            return end;
        }

        private static bool Near(float a, float b, float tolerance = 0.0005f)
            => Math.Abs(a - b) <= tolerance;

        // ── 검사 ─────────────────────────────────────────────────────────────

        private static string TestStageOrder()
        {
            OverharvestStageTimeline t = Fresh();
            float end = RunApproach(t, 10f);
            t.Pull(end + 0.05f);

            float s1 = t.FiredAt(OverharvestStage.Approach);
            float s2 = t.FiredAt(OverharvestStage.MachineDuck);
            float s3 = t.FiredAt(OverharvestStage.PassengerGaze);
            float s4 = t.FiredAt(OverharvestStage.Silence);
            float s5 = t.FiredAt(OverharvestStage.Resume);

            if (s1 < 0f || s2 < 0f || s3 < 0f || s4 < 0f || s5 < 0f)
                return $"발화하지 않은 단계가 있다 — {t.Describe()}";
            if (!(s1 <= s2 && s2 < s3 && s3 <= s4 && s4 < s5))
                return $"순서가 §7.3 과 다르다 — 1={s1:F3} 2={s2:F3} 3={s3:F3} 4={s4:F3} 5={s5:F3}";
            return null;
        }

        private static string TestEachStageFiresOnce()
        {
            OverharvestStageTimeline t = Fresh();
            float end = RunApproach(t, 0f);
            t.Pull(end + 0.05f);

            if (t.ApproachCount != 1) return $"1단계 {t.ApproachCount}회";
            if (t.MachineDuckCount != 1) return $"2단계 {t.MachineDuckCount}회";
            if (t.PassengerGazeCount != 1) return $"3단계 {t.PassengerGazeCount}회";
            if (t.SilenceCount != 1) return $"4단계 {t.SilenceCount}회";
            if (t.ResumeCount != 1) return $"5단계 {t.ResumeCount}회";
            if (!t.AllStagesFired) return "AllStagesFired 가 거짓이다";
            return null;
        }

        private static string TestPullWithoutApproach()
        {
            // 하네스는 레버를 겨누지 않고 Interact() 를 직접 부른다. 그 경로에서도
            // 「당김 뒤 재개」는 보여야 한다 — 안 그러면 대표 장면이 통째로 관측 불가다.
            OverharvestStageTimeline t = Fresh();
            t.Pull(5f);

            if (t.ResumeCount != 1) return $"5단계가 {t.ResumeCount}회다";
            if (!t.IsResuming) return "당겼는데 재개 중이 아니다";

            float burst = t.ResumeBurst(OverharvestChannel.Warning, 5f);
            if (burst < 0.99f) return $"당긴 순간 버스트가 {burst:F3} — 평소와 구분되지 않는다";
            return null;
        }

        private static string TestMissingStagesStayZero()
        {
            OverharvestStageTimeline t = Fresh();
            t.Pull(1f);

            if (t.ApproachCount != 0 || t.MachineDuckCount != 0
                || t.PassengerGazeCount != 0 || t.SilenceCount != 0)
                return $"앞 단계가 없는데 카운터가 올라갔다 — {t.Describe()}";
            if (t.AllStagesFired)
                return "5단계만 났는데 AllStagesFired 가 참이다 — 「다 됐다」를 반증할 수 없다";
            return null;
        }

        private static string TestResumeIsSimultaneous()
        {
            OverharvestStageTimeline t = Fresh();
            float end = RunApproach(t, 3f);
            float pull = end + 0.02f;
            t.Pull(pull);

            for (int i = 0; i < OverharvestStageTimeline.ChannelCount; i++)
            {
                float at = t.ResumeStartedAt((OverharvestChannel)i);
                if (!Near(at, pull, 0f))
                    return $"채널 {(OverharvestChannel)i} 재개 시각 {at:F4} ≠ 당김 {pull:F4}";
            }
            if (!t.ResumeIsSimultaneous) return "ResumeIsSimultaneous 가 거짓이다";
            return null;
        }

        private static string TestChannelsDeviateTogether()
        {
            // 시각이 같다는 것만으로는 부족하다 — 값이 실제로 같은 순간에 움직여야 한다.
            OverharvestStageTimeline t = Fresh();
            float end = RunApproach(t, 0f);
            float pull = end + 0.02f;

            var before = new float[OverharvestStageTimeline.ChannelCount];
            for (int i = 0; i < before.Length; i++)
                before[i] = t.ChannelScale((OverharvestChannel)i, pull);

            t.Pull(pull);

            var firstMove = new float[OverharvestStageTimeline.ChannelCount];
            for (int i = 0; i < firstMove.Length; i++) firstMove[i] = -1f;

            for (float dt = 0f; dt <= t.ResumeSeconds; dt += 0.005f)
            {
                for (int i = 0; i < firstMove.Length; i++)
                {
                    if (firstMove[i] >= 0f) continue;
                    float now = t.ChannelScale((OverharvestChannel)i, pull + dt);
                    if (Math.Abs(now - before[i]) > 0.01f) firstMove[i] = dt;
                }
            }

            for (int i = 0; i < firstMove.Length; i++)
                if (firstMove[i] < 0f)
                    return $"채널 {(OverharvestChannel)i} 가 재개 창 내내 움직이지 않았다";

            for (int i = 1; i < firstMove.Length; i++)
                if (!Near(firstMove[i], firstMove[0], 0.006f))
                    return $"채널 {(OverharvestChannel)i} 가 {firstMove[i]:F3}s, 채널 0 이 {firstMove[0]:F3}s 에 움직였다 — 동시가 아니다";
            return null;
        }

        private static string TestResumeWindowLength()
        {
            OverharvestStageTimeline t = Fresh();
            t.Pull(0f);

            float fade = t.ResumeSeconds;
            if (t.ResumeBurst(OverharvestChannel.Warning, fade * 0.5f) <= 0f)
                return "재개 창 한가운데인데 버스트가 0이다";
            if (t.ResumeBurst(OverharvestChannel.Warning, fade) != 0f)
                return "재개 창이 끝났는데 버스트가 남아 있다";
            if (!Near(t.ChannelLevel(OverharvestChannel.TubeSpin, fade), 1f))
                return $"재개 후 기계 수준이 {t.ChannelLevel(OverharvestChannel.TubeSpin, fade):F3} 다";
            return null;
        }

        private static string TestResumeFollowsProfile()
        {
            // 값을 바꿔도 아무 일도 안 일어나면 그건 「읽는 척」이다.
            OverharvestStageTimeline fast = With(Tweaked(0.35f, 0.10f, 0.18f, 0.3f, 0.7f));
            OverharvestStageTimeline slow = With(Tweaked(0.35f, 0.80f, 0.18f, 0.3f, 0.7f));

            fast.Pull(0f);
            slow.Pull(0f);

            if (!Near(fast.ResumeSeconds, 0.10f)) return $"빠른 쪽 재개 {fast.ResumeSeconds:F3}s";
            if (!Near(slow.ResumeSeconds, 0.80f)) return $"느린 쪽 재개 {slow.ResumeSeconds:F3}s";

            // 0.2초 시점 — 빠른 쪽은 이미 끝났고 느린 쪽은 아직 튀고 있어야 한다.
            if (fast.ResumeBurst(OverharvestChannel.Shake, 0.2f) != 0f)
                return "0.10s 창인데 0.2s 에도 버스트가 남아 있다";
            if (slow.ResumeBurst(OverharvestChannel.Shake, 0.2f) <= 0f)
                return "0.80s 창인데 0.2s 에 버스트가 없다";
            return null;
        }

        private static string TestDuckScaleFollowsProfile()
        {
            OverharvestStageTimeline shallow = With(Tweaked(0.80f, 0.25f, 0.18f, 0.3f, 0.7f));
            OverharvestStageTimeline deep = With(Tweaked(0.05f, 0.25f, 0.18f, 0.3f, 0.7f));

            shallow.Approach(0f, Seed);
            deep.Approach(0f, Seed);

            // 감쇠가 끝나고 정적이 열리기 직전. 그 구간의 값이 곧 감쇠 바닥값이다.
            float probe = (shallow.DuckSeconds + shallow.SilenceStartOffset) * 0.5f;
            if (probe <= shallow.DuckSeconds) probe = shallow.DuckSeconds + 0.001f;

            float a = shallow.ChannelLevel(OverharvestChannel.TubeSpin, probe);
            float b = deep.ChannelLevel(OverharvestChannel.TubeSpin, probe);

            if (!Near(a, 0.80f, 0.01f)) return $"0.80 배율인데 {a:F3} 이다";
            if (!Near(b, 0.05f, 0.01f)) return $"0.05 배율인데 {b:F3} 이다";
            if (a <= b) return "감쇠를 깊게 했는데 값이 더 크거나 같다";
            return null;
        }

        private static string TestSilenceIsFullStop()
        {
            OverharvestStageTimeline t = Fresh();
            t.Approach(0f, Seed);

            float mid = t.SilenceStartOffset + t.SilenceSeconds * 0.5f;
            float level = t.ChannelLevel(OverharvestChannel.TubeSpin, mid);
            if (level != 0f) return $"정적 한가운데인데 기계 수준이 {level:F4} 다 — §7.3(4)는 감쇠가 아니라 정적이다";

            float warn = t.ChannelScale(OverharvestChannel.Warning, mid);
            if (warn != 0f) return $"정적 한가운데인데 경고등 배수가 {warn:F4} 다";
            return null;
        }

        private static string TestSilenceLengthDeterministic()
        {
            OverharvestStageTimeline a = Fresh();
            OverharvestStageTimeline b = Fresh();
            a.Approach(0f, 991);
            b.Approach(17f, 991);

            if (!Near(a.SilenceSeconds, b.SilenceSeconds))
                return $"같은 시드인데 {a.SilenceSeconds:F4}s vs {b.SilenceSeconds:F4}s";

            if (a.SilenceSeconds < OverharvestProfile.DefaultMinSilenceSeconds - 0.0001f
                || a.SilenceSeconds > OverharvestProfile.DefaultMaxSilenceSeconds + 0.0001f)
                return $"정적 {a.SilenceSeconds:F4}s 가 PRD §7.3 의 0.3~0.7 범위 밖이다";

            OverharvestStageTimeline c = Fresh();
            c.Approach(0f, 992);
            if (Near(a.SilenceSeconds, c.SilenceSeconds))
                return "시드를 바꿨는데 길이가 같다 — 시드가 실제로 쓰이지 않는다";
            return null;
        }

        private static string TestGazePrecedesSilence()
        {
            // 응시 지연을 감쇠보다 훨씬 길게 잡아도 §7.3 의 3 → 4 순서가 유지되어야 한다.
            OverharvestStageTimeline t = With(Tweaked(0.35f, 0.25f, 0.5f, 0.3f, 0.7f));
            float end = RunApproach(t, 0f);
            if (end < 0f) return "접근 구간이 음수다";

            float gaze = t.FiredAt(OverharvestStage.PassengerGaze);
            float silence = t.FiredAt(OverharvestStage.Silence);
            if (gaze < 0f || silence < 0f) return $"3·4단계 중 하나가 안 났다 — {t.Describe()}";
            if (gaze > silence) return $"응시 {gaze:F3} 가 정적 {silence:F3} 보다 늦다";
            if (!Near(t.SilenceStartOffset, 0.5f))
                return $"응시 지연 0.5s 인데 정적 시작이 {t.SilenceStartOffset:F3}s 다";
            return null;
        }

        private static string TestShakeIsEventOnly()
        {
            OverharvestStageTimeline t = Fresh();
            if (t.ChannelScale(OverharvestChannel.Shake, 0f) != 0f)
                return "아무 일도 없는데 진동이 있다";

            t.Approach(0f, Seed);
            float mid = t.SilenceStartOffset + t.SilenceSeconds * 0.5f;
            if (t.ChannelScale(OverharvestChannel.Shake, mid) != 0f)
                return "정적 구간인데 진동이 있다 — 「재개」가 사건이 되지 않는다";

            t.Pull(mid);
            if (t.ChannelScale(OverharvestChannel.Shake, mid) < 0.99f)
                return "당겼는데 진동이 없다";
            return null;
        }

        private static string TestBurstReturnsHome()
        {
            OverharvestStageTimeline t = Fresh();
            t.Pull(0f);

            if (t.BurstTurns01(0f) != 0f) return "버스트 시작에서 회전량이 0 이 아니다";
            if (t.BurstTurns01(t.ResumeSeconds * 0.5f) <= 0f) return "버스트 중간에 회전량이 0 이다";
            if (t.BurstTurns01(t.ResumeSeconds) != 0f)
                return "버스트가 끝났는데 회전량이 0 이 아니다 — 정지 화면의 각도가 런마다 달라진다";

            // 단조 증가여야 한다. 되감기면 통관이 거꾸로 도는 것처럼 보인다.
            float previous = -1f;
            for (float p = 0f; p < 1f; p += 0.05f)
            {
                float turns = t.BurstTurns01(t.ResumeSeconds * p);
                if (turns < previous) return $"회전량이 {previous:F3} → {turns:F3} 로 되감겼다";
                previous = turns;
            }
            return null;
        }

        private static string TestSilenceSelfRecovers()
        {
            // 당기지 않고 계속 겨누고만 있으면 방이 영원히 멈춰 있으면 안 된다.
            OverharvestStageTimeline t = Fresh();
            t.Approach(0f, Seed);

            float end = t.SilenceStartOffset + t.SilenceSeconds + t.ResumeSeconds;
            float level = t.ChannelLevel(OverharvestChannel.TubeSpin, end + 0.01f);
            if (!Near(level, 1f, 0.01f))
                return $"정적이 끝나고 재개 창도 지났는데 기계 수준이 {level:F3} 다";

            if (t.ResumeCount != 0)
                return "당기지 않았는데 5단계가 셌다 — 시간 경과와 당김이 구분되지 않는다";
            return null;
        }

        private static string TestReleaseIsNotResume()
        {
            OverharvestStageTimeline t = Fresh();
            t.Approach(0f, Seed);
            t.Tick(0.05f);
            t.Release(0.05f);

            if (t.ResumeCount != 0) return $"조준을 거뒀는데 5단계가 {t.ResumeCount}회 셌다";
            if (t.ReleaseCount != 1) return $"해제 카운터가 {t.ReleaseCount} 다";

            // 물러남도 연출이다 — 1 로 튕겨 올리지 않고 페이드로 돌아온다.
            float atRelease = t.ChannelLevel(OverharvestChannel.Warning, 0.05f);
            if (atRelease >= 0.999f) return "손을 뗀 순간 값이 1 로 튀었다 — 「잘못 눌렀다」로 읽힌다";

            float after = t.ChannelLevel(OverharvestChannel.Warning, 0.05f + t.ResumeSeconds);
            if (!Near(after, 1f, 0.01f)) return $"복귀가 끝났는데 {after:F3} 다";
            return null;
        }

        private static string TestUnwiredReportsCodePreset()
        {
            var t = new OverharvestStageTimeline();
            if (t.ProfileSource != OverharvestStageTimeline.CodePresetSource)
                return $"초기 출처가 「{t.ProfileSource}」 다";

            // 하네스의 단정이 EndsWith("Profile") 이다. 폴백 이름이 그것을 만족하면
            // 배선이 끊겨도 통과한다 — 그 함정을 여기서 막는다.
            if (OverharvestStageTimeline.CodePresetSource.EndsWith("Profile", StringComparison.Ordinal))
                return "폴백 이름이 「Profile」로 끝난다 — 배선 검사가 무력해진다";

            if (OverharvestStageTimeline.SourceNameOf(null) != OverharvestStageTimeline.CodePresetSource)
                return "SourceNameOf(null) 이 코드 프리셋을 내지 않는다";

            // 값도 코드 기본값이어야 한다. 출처만 맞고 값이 다르면 더 나쁘다.
            if (!Near(t.Profile.ResumeFadeSeconds, OverharvestProfile.DefaultResumeFadeSeconds))
                return $"폴백 재개 시간이 {t.Profile.ResumeFadeSeconds:F3} 다";
            if (!Near(t.Profile.ApproachMachineDuckScale, OverharvestProfile.DefaultApproachMachineDuckScale))
                return $"폴백 감쇠 배율이 {t.Profile.ApproachMachineDuckScale:F3} 다";
            return null;
        }

        private static string TestResetClearsCounters()
        {
            OverharvestStageTimeline t = Fresh();
            float end = RunApproach(t, 0f);
            t.Pull(end + 0.01f);
            if (!t.AllStagesFired) return "준비 단계에서 이미 실패했다";

            t.ResetRun();

            if (t.ApproachCount != 0 || t.MachineDuckCount != 0 || t.PassengerGazeCount != 0
                || t.SilenceCount != 0 || t.ResumeCount != 0 || t.ReleaseCount != 0)
                return $"리셋 후에도 카운터가 남아 있다 — {t.Describe()}";
            if (t.IsResuming) return "리셋 후에도 재개 중이다";
            if (!Near(t.ChannelLevel(OverharvestChannel.Warning, 0f), 1f))
                return "리셋 후 기계 수준이 1 이 아니다";
            return null;
        }

        // ── 순차 공개 (`UP-FIX-20`) ──────────────────────────────────────────
        //
        // **무엇을 반증하려는가**: 이 결함은 「순차 공개를 구현하지 않았다」가 아니라
        // 「구현했는데 정지 화면에서 그 사실이 안 보인다」였다. 아직 안 열린 칸을
        // `SymbolKind.Empty` 로 그렸기 때문에 **정화로 비워진 칸과 같은 픽셀**이 됐고,
        // 그래서 캡처 한 장으로는 `UP-CORE-11` 을 영원히 증명할 수 없었다.
        //
        // 그러니 여기서 재는 것은 「연출이 돌았다」가 아니라 **「한 프레임의 세 상태가
        // 서로 다른가」**다. 씬도 코루틴도 열지 않는다 — 판정은 순수 함수 하나에 있다.

        private static string TestRevealHasThreeStages()
        {
            // 두 열이 내려앉은 순간. 0=이미 열림, 1=지금 열림, 2=아직 안 열림.
            const int revealed = 2;

            SpinPresenter.RevealStage a = SpinPresenter.StageOfColumn(0, revealed);
            SpinPresenter.RevealStage b = SpinPresenter.StageOfColumn(1, revealed);
            SpinPresenter.RevealStage c = SpinPresenter.StageOfColumn(2, revealed);

            if (a != SpinPresenter.RevealStage.Open) return $"0열이 {a} 다";
            if (b != SpinPresenter.RevealStage.Opening) return $"1열이 {b} 다";
            if (c != SpinPresenter.RevealStage.Sealed) return $"2열이 {c} 다";
            if (a == b || b == c || a == c)
                return "세 열이 한 프레임에서 같은 단계다 — 정지 화면에서 구분되지 않는다";
            return null;
        }

        private static string TestSealedIsNotEmpty()
        {
            // 「아직 안 열린 판」은 표식이 9개, 「다 열린 판」은 0개다.
            // 두 값이 같으면 그 프레임의 그림이 같다는 뜻이고, 그것이 원래 결함이다.
            for (int column = 0; column < SpinBoard.Columns; column++)
            {
                SpinPresenter.RevealStage stage = SpinPresenter.StageOfColumn(column, 0);
                if (stage != SpinPresenter.RevealStage.Sealed)
                    return $"아무 열도 안 열렸는데 {column}열이 {stage} 다";
            }

            int sealedBars = PurifyMarkerView.RevealBarsNeeded(0);
            int openBars = PurifyMarkerView.RevealBarsNeeded(SpinPresenter.RevealComplete);

            if (sealedBars != SpinBoard.Cells * PurifyMarkerView.SealedBarsPerCell)
                return $"닫힌 판의 표식이 {sealedBars}개다 — 9칸이 전부 서야 한다";
            if (openBars != 0)
                return $"공개가 끝났는데 표식이 {openBars}개 남아 있다";
            if (sealedBars == openBars)
                return "닫힌 판과 다 열린 판의 표식 수가 같다 — 정지 화면에서 같은 그림이다";
            return null;
        }

        private static string TestRevealIsMonotonic()
        {
            // 진행도가 올라가는데 단계가 뒤로 가면 셔터가 다시 닫히는 것처럼 보인다.
            for (int column = 0; column < SpinBoard.Columns; column++)
            {
                int previous = -1;
                for (int revealed = 0; revealed <= SpinPresenter.RevealComplete; revealed++)
                {
                    int now = (int)SpinPresenter.StageOfColumn(column, revealed);
                    if (now < previous)
                        return $"{column}열이 진행도 {revealed} 에서 {(SpinPresenter.RevealStage)previous}" +
                               $" → {(SpinPresenter.RevealStage)now} 로 되돌아갔다";
                    previous = now;
                }
                if (previous != (int)SpinPresenter.RevealStage.Open)
                    return $"공개가 끝났는데 {column}열이 {(SpinPresenter.RevealStage)previous} 다";
            }
            return null;
        }

        private static string TestExactlyOneOpeningColumn()
        {
            for (int revealed = 0; revealed <= SpinPresenter.RevealComplete; revealed++)
            {
                int opening = 0;
                for (int column = 0; column < SpinBoard.Columns; column++)
                    if (SpinPresenter.StageOfColumn(column, revealed) == SpinPresenter.RevealStage.Opening)
                        opening++;

                int expected = revealed >= 1 && revealed <= SpinBoard.Columns ? 1 : 0;
                if (opening != expected)
                    return $"진행도 {revealed} 에서 열리는 열이 {opening}개다 (기대 {expected})";
            }
            return null;
        }

        private static string TestRevealCellMatchesColumn()
        {
            // 좌표 변환을 두 번 구현하면 표식과 판이 어긋난다. 같은 답이 나와야 한다.
            for (int revealed = 0; revealed <= SpinPresenter.RevealComplete; revealed++)
            for (int cell = 0; cell < SpinBoard.Cells; cell++)
            {
                SpinPresenter.RevealStage byCell = SpinPresenter.StageOfCell(cell, revealed);
                SpinPresenter.RevealStage byColumn =
                    SpinPresenter.StageOfColumn(SpinBoard.ColumnOf(cell), revealed);
                if (byCell != byColumn)
                    return $"칸 {cell}(진행도 {revealed}) 가 {byCell} 인데 " +
                           $"{SpinBoard.ColumnOf(cell)}열은 {byColumn} 다";
            }

            // 범위 밖은 예외가 아니라 「열림」으로 접힌다 — 연출이 인덱스로 죽으면 안 된다.
            if (SpinPresenter.StageOfCell(-1, 0) != SpinPresenter.RevealStage.Open) return "음수 칸이 Open 이 아니다";
            if (SpinPresenter.StageOfColumn(9, 0) != SpinPresenter.RevealStage.Open) return "범위 밖 열이 Open 이 아니다";
            if (SpinPresenter.StageOfColumn(0, -5) != SpinPresenter.RevealStage.Sealed) return "음수 진행도가 Sealed 로 접히지 않는다";
            if (SpinPresenter.StageOfColumn(0, 99) != SpinPresenter.RevealStage.Open) return "과대 진행도가 Open 으로 접히지 않는다";
            return null;
        }

        private static string TestRevealFitsInPool()
        {
            // 풀이 모자라면 `PurifyMarkerView.PlaceBar` 가 **조용히** 생략한다.
            // 그 경우 캡처에는 셔터가 몇 칸만 서고, 그림이 사실과 다른 진행도를 주장한다.
            int peak = 0;
            for (int revealed = -2; revealed <= SpinPresenter.RevealComplete + 2; revealed++)
            {
                int need = PurifyMarkerView.RevealBarsNeeded(revealed);
                if (need > peak) peak = need;
            }

            if (peak > PurifyMarkerView.RevealPoolRequirement)
                return $"셔터 최대 {peak}개가 요구 풀 {PurifyMarkerView.RevealPoolRequirement}개를 넘는다";
            if (peak != PurifyMarkerView.RevealPoolRequirement)
                return $"요구 풀이 {PurifyMarkerView.RevealPoolRequirement}인데 실제 최대는 {peak}다 — " +
                       "상수가 실제를 재지 않으면 다음 사람이 그것을 믿고 줄인다";
            return null;
        }
    }
}
