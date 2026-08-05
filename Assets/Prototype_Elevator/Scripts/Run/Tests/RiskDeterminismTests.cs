using System;
using System.Text;
using Ascend.Prototype.Data.Profiles;
using Ascend.Prototype.Risk;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.Run.Tests
{
    /// <summary>
    /// 위험 판정이 **프레임이 아니라 상태 전이에 묶여 있는가** (`D-1` 2026-08-05).
    ///
    /// ## 무엇을 반증하려는가
    ///
    /// <see cref="RiskEvaluator"/> 는 히스테리시스 상태를 들고 있어 **입력의 함수가 아니라
    /// 입력 궤적의 함수**다. 그래서 「무엇을 먹이는가」만큼 「언제 먹이는가」가 판정이다.
    ///
    /// 직전 판본은 평가기를 `RiskStateView.LateUpdate` 안에서만 돌렸다. 연출 컴포넌트가
    /// 프레임당 한 번 도는 자리라, 한 프레임 안에서 상태가 두 번 바뀌면 중간값이 통째로
    /// 사라졌다. 실측 경로는 `RouletteInteractionBridge.OnOverharvestPulled` 이다 —
    /// `PushYourLuck()`(앤티 지불, 과수확 카운터 +1 → 점수 급등)과 `DoSpin()`(잔류 정리
    /// → 점수 하락)이 **같은 프레임에** 끝난다. 그 사이의 봉우리를 관측하는 프레임이
    /// 존재하지 않으므로, `AccidentRecorder` 가 적는 「최고 위험」이 시드가 아니라
    /// **프레임 타이밍**에 달렸다.
    ///
    /// | 검사 | 깨지면 무엇이 무너지나 |
    /// |---|---|
    /// | 궤적 ≠ 프레임 샘플 | 「호출 지점이 중요하다」의 근거 자체가 사라진다 |
    /// | 뷰 없이 판정이 돈다 | 연출을 끄면 사고 기록·사운드·텔레메트리가 함께 죽는다 |
    /// | 앤티 봉우리가 살아남는다 | 같은 시드가 다른 사고 기록을 남긴다 |
    /// | 재판정이 멱등이다 | 판정이 프레임 수(=기기 성능)의 함수가 된다 |
    /// | 임계값 주입이 판정에 닿는다 | `RiskThresholdProfile` 이 장식이 된다 |
    ///
    /// 전부 헤드리스다 — **씬도 `RiskStateView` 인스턴스도 존재하지 않는다.** 이 스위트가
    /// 통과한다는 사실 자체가 「판정은 뷰의 것이 아니다」의 증거다.
    /// </summary>
    public static class RiskDeterminismTests
    {
        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0, failed = 0;
            var report = new StringBuilder();

            Run("궤적 샘플과 프레임 샘플이 다른 단계를 낸다 (결함의 근거)",
                TestTrajectoryDiffersFromFrameSample, ref passed, ref failed, report);
            Run("RiskStateView 없이 RunSession 만으로 판정이 돈다",
                TestRunSessionJudgesWithoutView, ref passed, ref failed, report);
            Run("같은 프레임 안에서 오르내린 봉우리가 살아남는다 (결정론적 경로)",
                TestSameFrameSpikeSurvives, ref passed, ref failed, report);
            Run("앤티 지불의 봉우리가 같은 프레임 스핀 뒤에도 남는다 (실제 과수확 경로)",
                TestAntePeakSurvivesFrameSampling, ref passed, ref failed, report);
            Run("재판정은 멱등이다 (판정이 프레임 수의 함수가 아니다)",
                TestEvaluateRiskIsIdempotent, ref passed, ref failed, report);
            Run("임계값 주입이 판정기까지 닿고 출처가 남는다",
                TestThresholdInjectionReachesJudgment, ref passed, ref failed, report);

            report.Insert(0, "[상승] === Risk Determinism Tests ===\n");
            report.Append($"결과: {passed} PASS / {failed} FAIL");
            return (passed, failed, report.ToString());
        }

        // ── ① 왜 호출 지점이 판정인가 ────────────────────────────────────────

        /// <summary>
        /// 같은 **시작 단계**와 같은 **끝 점수**인데, 중간값을 먹이느냐 건너뛰느냐로
        /// 결과가 갈린다는 것을 못박는다. 이 둘이 같아지면 히스테리시스가 죽은 것이고,
        /// 다르다는 사실이 곧 「어디서 부르는가가 판정이다」의 근거다.
        ///
        /// 임계값은 **평가기에서 읽는다.** 상수를 박으면 밸런스 다이얼이 움직인 날
        /// 이 테스트가 조용히 거짓말을 시작한다.
        /// </summary>
        private static void TestTrajectoryDiffersFromFrameSample()
        {
            RiskEvaluator probe = UnitWeight();

            // 세 지점: 낮음(Stable) → 봉우리(Critical 진입 이상) → 유지값(이탈 이상·진입 미만).
            int peak = (int)Math.Ceiling(probe.CriticalEnter);
            int hold = (int)Math.Ceiling(probe.CriticalExit);
            int low = (int)Math.Floor(probe.StrainExit);

            Assert(hold >= probe.CriticalExit && hold < probe.CriticalEnter,
                   $"히스테리시스 밴드가 정수 한 칸을 담지 못한다 " +
                   $"(이탈 {probe.CriticalExit} ~ 진입 {probe.CriticalEnter}) — 테스트를 구성할 수 없다");
            Assert(low < probe.StrainEnter,
                   $"출발 점수 {low} 가 Strain 진입 {probe.StrainEnter} 이상이다 — Stable 에서 시작할 수 없다");

            // ① 단계별로 먹인다. 봉우리에서 진입하고, 유지값이 이탈 위라 내려오지 않는다.
            RiskEvaluator stepwise = UnitWeight();
            stepwise.Evaluate(Absorbers(low));
            stepwise.Evaluate(Absorbers(peak));
            RiskLevel stepwiseLevel = stepwise.Evaluate(Absorbers(hold));

            // ② 봉우리를 건너뛴다. 프레임 샘플러가 보는 것이 정확히 이것이다.
            RiskEvaluator sampled = UnitWeight();
            sampled.Evaluate(Absorbers(low));
            RiskLevel sampledLevel = sampled.Evaluate(Absorbers(hold));

            Assert(stepwiseLevel == RiskLevel.Critical,
                   $"궤적 {low}→{peak}→{hold} 의 끝이 {stepwiseLevel} 다 — Critical 이어야 한다");
            Assert(sampledLevel != RiskLevel.Critical,
                   $"봉우리를 건너뛴 {low}→{hold} 도 {sampledLevel} 다 — 두 경로가 같으면 " +
                   "히스테리시스가 죽었고, 이 결함을 잴 수 없다");
        }

        // ── ② 뷰가 없어도 판정이 돈다 (S-1 이 이제 참이라는 증거) ─────────────

        /// <summary>
        /// 헤드리스 런에서 위험 단계가 산출되고, **상태 전이 뒤에** 점수가 그 시점의
        /// 입력과 일치하는지 본다.
        ///
        /// 음성 대조: `RunSession.Spin()` 의 `EvaluateRisk()` 를 지우면 점수가 0 에
        /// 머물러 아래 두 단정이 함께 깨진다.
        /// </summary>
        private static void TestRunSessionJudgesWithoutView()
        {
            bool sawNonStable = false;

            for (int seed = 0; seed < 400 && !sawNonStable; seed++)
            {
                RunSession run = NewHeroSliceRun(seed);

                Assert(run.RiskLevel == RiskLevel.Stable,
                       $"시드 {seed}: 런 시작 단계가 {run.RiskLevel} 다 — 깨끗한 시작은 Stable 이다");
                Assert(run.RiskReason == RunSession.NoRiskReason,
                       $"시드 {seed}: 시작 사유가 '{run.RiskReason}' 다");

                run.SelectContract(0);

                FloorSession floor = run.Current;
                while (floor != null && floor.SpinsRemaining > 0 && !floor.CanBank)
                {
                    if (run.Spin().Steps == null) break;

                    // 점수는 **지금 입력의 함수**여야 한다. 어긋나면 전이 뒤에 재판정이
                    // 일어나지 않았다는 뜻이다 — 판정이 어딘가 다른 시계에 묶여 있다.
                    float expected = new RiskEvaluator().Score(run.CurrentRiskInputs);
                    Assert(Math.Abs(run.RiskScore - expected) < 0.001f,
                           $"시드 {seed}: 스핀 뒤 점수 {run.RiskScore:F3} ≠ 현재 입력의 점수 " +
                           $"{expected:F3} — 상태 전이가 재판정을 부르지 않았다");

                    if (run.RiskLevel != RiskLevel.Stable) sawNonStable = true;
                }
            }

            Assert(sawNonStable,
                   "400 시드 안에 Stable 을 벗어나는 층이 하나도 없다 — 위험이 실제로 " +
                   "움직이는지 확인할 수 없으므로 이 검사는 아무것도 주장하지 못한다");
        }

        /// <summary>
        /// 봉우리가 **연속한 두 전이 사이**에 있을 때 살아남는가 — 과수확이 아닌
        /// 보통 스핀 경로로. 위 ②가 「전이마다 재판정한다」를 점수로 확인했다면
        /// 이쪽은 그 재판정이 **히스테리시스에 실제로 기록되는지**를 본다.
        ///
        /// ## 무엇이 결함이었나
        ///
        /// 평가기는 상태(직전 단계)를 들고 있으므로 입력의 함수가 아니라 **입력 궤적의
        /// 함수**다. 직전 판본은 `RiskStateView.LateUpdate` 한 곳에서만 평가기를 돌렸다.
        /// 두 전이가 한 프레임 안에서 끝나면 그 사이의 봉우리를 관측하는 프레임이
        /// 존재하지 않고, 히스테리시스는 봉우리를 **본 적 없는 것으로** 취급한다.
        /// 그러면 `AccidentRecorder` 가 읽는 최고 위험이 시드가 아니라 프레임 타이밍에 달린다.
        ///
        /// 검사는 두 경로를 나란히 놓는다 — 전이마다 먹인 쪽(`stepwise`)과, 봉우리 직전
        /// 단계에서 **끝값 하나로 건너뛴** 쪽(`sampled`). 둘이 갈려야 결함이 재발했을 때
        /// 잡히고, 갈리지 않으면 그 시드는 아무것도 증명하지 못하므로 버린다.
        ///
        /// 임계값을 궤적에 맞추는 이유는 `TestAntePeakSurvivesFrameSampling` 과 같다 —
        /// 기본값에서 이 궤적이 나오는 것은 밸런스 다이얼에 딸린 우연이라, 다이얼이
        /// 움직이면 감시선이 조용히 사라진다. 가중치 다섯은 건드리지 않으므로 점수는
        /// 두 번 모두 같아야 하고, 그 일치도 아래에서 단정한다.
        ///
        /// 음성 대조: `RunSession.Spin()` 의 `EvaluateRisk()` 를 지우면 `stepwise` 가
        /// Critical 이 아니게 된다.
        /// </summary>
        private static void TestSameFrameSpikeSurvives()
        {
            const int MaxSpins = 8;

            for (int seed = 0; seed < 3000; seed++)
            {
                // ── 1차: 스핀별 점수 궤적을 관측한다 (임계값은 기본값) ──
                RunSession probe = NewHeroSliceRun(seed);
                if (!EnterSpinning(probe)) continue;

                var scores = new float[MaxSpins];
                int count = 0;
                FloorSession probeFloor = probe.Current;
                while (probeFloor != null && probeFloor.SpinsRemaining > 0 && count < MaxSpins)
                {
                    if (probe.Spin().Steps == null) break;
                    scores[count++] = probe.RiskScore;
                }

                // 봉우리가 **중간에** 있어야 한다. 끝값이 더 높으면 건너뛴 샘플러도
                // 그 값을 보므로 애초에 놓칠 것이 없다.
                int peak = -1;
                for (int i = 0; i + 1 < count; i++)
                    if (scores[i] > scores[i + 1] + 0.05f && scores[i + 1] > 0.05f) { peak = i; break; }
                if (peak < 0) continue;

                float criticalEnter = (scores[peak] + scores[peak + 1]) * 0.5f;
                float criticalExit = scores[peak + 1] * 0.5f;
                RiskThresholdSnapshot tuned = TunedThresholds(criticalEnter, criticalExit);

                // ── 2차: 같은 시드·같은 선택을 그대로 다시 돌린다 ──
                RunSession run = NewHeroSliceRun(seed);
                run.ApplyRiskThresholds(tuned);
                if (!EnterSpinning(run)) continue;

                RiskLevel before = RiskLevel.Stable;
                bool aborted = false;
                for (int i = 0; i <= peak + 1; i++)
                {
                    // 봉우리를 만드는 스핀 **직전** 단계. 건너뛴 샘플러의 출발점이다.
                    if (i == peak) before = run.RiskLevel;

                    if (run.Spin().Steps == null) { aborted = true; break; }
                    Assert(Math.Abs(run.RiskScore - scores[i]) < 0.001f,
                           $"시드 {seed} 스핀 {i}: 임계값만 바꿨는데 점수가 달라졌다 " +
                           $"({run.RiskScore:F3} ≠ {scores[i]:F3}) — 임계값이 점수에 새고 있다");
                }
                if (aborted) continue;

                // 이미 Critical 에서 출발했다면 건너뛴 샘플러도 이탈 임계 위에서 단계를
                // 유지하므로 결함이 드러나지 않는다. 우리가 찾는 것은 **이 봉우리가 만든** 진입이다.
                if (before >= RiskLevel.Critical) continue;

                RiskLevel stepwise = run.RiskLevel;

                // 건너뛴 샘플러 — 같은 임계값, 같은 출발 단계, 그러나 **끝값 하나만** 본다.
                var skipped = new RiskEvaluator();
                skipped.Apply(tuned);
                skipped.ForceLevel(before);
                RiskLevel sampled = skipped.Evaluate(run.CurrentRiskInputs);

                Assert(stepwise == RiskLevel.Critical,
                       $"시드 {seed}: 봉우리({scores[peak]:F2}) → 다음({scores[peak + 1]:F2}) 뒤 단계가 " +
                       $"{stepwise} 다 — 진입 {criticalEnter:F2}·이탈 {criticalExit:F2} 에서 " +
                       "Critical 이어야 한다. 봉우리 시점에 판정이 일어나지 않았다는 뜻이다");
                Assert(sampled < RiskLevel.Critical,
                       $"시드 {seed}: 끝값만 본 샘플러도 {sampled} 다 — 두 경로가 같으면 " +
                       "이 시드는 결함을 드러내지 못한다 (필터가 잘못 골랐다)");
                return;   // 한 건이면 충분하다. 이 성질은 존재 증명이다.
            }

            throw new Exception(
                "3000 시드 안에 「스핀에서 올랐다가 다음 스핀에서 내려오는」 궤적을 " +
                "찾지 못했다 — 이 경로를 검사할 수 없다");
        }

        /// <summary>계약 선택을 넘겨 스핀 가능한 상태로 만든다. 못 가면 false.</summary>
        private static bool EnterSpinning(RunSession run)
        {
            FloorSession floor = run.Current;
            if (floor == null) return false;
            if (floor.Phase == FloorPhase.ContractSelection && !run.SelectContract(0)) return false;
            return run.Current != null;
        }

        // ── ③ 같은 프레임 안의 봉우리 ────────────────────────────────────────

        /// <summary>
        /// `RouletteInteractionBridge.OnOverharvestPulled` 과 **같은 순서**로
        /// 「앤티 지불 → 스핀」을 연달아 부른 뒤, 봉우리 단계가 남아 있는지 본다.
        /// 이 값이 곧 `AccidentRecorder.TrackPeakRisk` 가 읽는 값이다.
        ///
        /// ## 왜 임계값을 궤적에 맞추는가
        ///
        /// 기본 임계값(7.0 / 5.5)에서 이 결함이 드러나려면 「앤티 뒤 점수 ≥ 7, 스핀 뒤
        /// 점수 ∈ [5.5, 7)」인 시드가 필요하다. 그건 밸런스 값에 딸린 우연이고, 다이얼이
        /// 움직이면 회귀 감시선이 조용히 사라진다. 그래서 **먼저 실제 궤적을 재고**
        /// (1차), 그 궤적에 임계값을 맞춰 같은 시드를 다시 돌린다(2차). 가중치 다섯은
        /// 건드리지 않으므로 점수는 두 번 모두 같고, 그 일치도 아래에서 단정한다.
        ///
        /// 음성 대조: `RunSession.PushYourLuck()` 의 `EvaluateRisk()` 를 지우면 봉우리를
        /// 관측하는 지점이 사라져 `stepwise` 가 Critical 이 아니게 된다.
        /// </summary>
        private static void TestAntePeakSurvivesFrameSampling()
        {
            for (int seed = 0; seed < 3000; seed++)
            {
                // ── 1차: 실제 점수 궤적을 관측한다 (임계값은 기본값) ──
                RunSession probe = NewHeroSliceRun(seed);
                if (!DriveToOverharvestDecision(probe)) continue;
                if (!probe.PushYourLuck()) continue;
                float scoreAtAnte = probe.RiskScore;
                if (probe.Spin().Steps == null) continue;
                float scoreAfterSpin = probe.RiskScore;

                // 봉우리가 **중간에** 있는 경우만 쓸모가 있다. 끝값이 더 높으면 프레임
                // 샘플러도 그 값을 보므로 애초에 놓칠 것이 없다.
                if (scoreAtAnte <= scoreAfterSpin + 0.05f) continue;
                if (scoreAfterSpin <= 0.05f) continue;

                // 진입은 끝값과 봉우리 **사이**, 이탈은 끝값 **아래**.
                float criticalEnter = (scoreAtAnte + scoreAfterSpin) * 0.5f;
                float criticalExit = scoreAfterSpin * 0.5f;
                RiskThresholdSnapshot tuned = TunedThresholds(criticalEnter, criticalExit);

                // ── 2차: 같은 시드·같은 선택을 그대로 다시 돌린다 ──
                RunSession run = NewHeroSliceRun(seed);
                run.ApplyRiskThresholds(tuned);
                if (!DriveToOverharvestDecision(run)) continue;

                RiskLevel before = run.RiskLevel;
                // 이미 Critical 이면 프레임 샘플러도 이탈 임계 위에서 그 단계를 유지하므로
                // 결함이 드러나지 않는다. 우리가 찾는 것은 **앤티가 만든** 봉우리다.
                if (before >= RiskLevel.Critical) continue;

                if (!run.PushYourLuck()) continue;
                Assert(Math.Abs(run.RiskScore - scoreAtAnte) < 0.001f,
                       $"시드 {seed}: 임계값만 바꿨는데 앤티 시점 점수가 달라졌다 " +
                       $"({run.RiskScore:F3} ≠ {scoreAtAnte:F3}) — 임계값이 점수에 새고 있다");

                if (run.Spin().Steps == null) continue;
                Assert(Math.Abs(run.RiskScore - scoreAfterSpin) < 0.001f,
                       $"시드 {seed}: 스핀 뒤 점수가 1차와 다르다 " +
                       $"({run.RiskScore:F3} ≠ {scoreAfterSpin:F3})");

                RiskLevel stepwise = run.RiskLevel;

                // 프레임 샘플러 — 같은 임계값, 같은 출발 단계, 그러나 **끝값 하나만** 본다.
                var frame = new RiskEvaluator();
                frame.Apply(tuned);
                frame.ForceLevel(before);
                RiskLevel sampled = frame.Evaluate(run.CurrentRiskInputs);

                Assert(stepwise == RiskLevel.Critical,
                       $"시드 {seed}: 앤티({scoreAtAnte:F2}) → 스핀({scoreAfterSpin:F2}) 뒤 단계가 " +
                       $"{stepwise} 다 — 진입 {criticalEnter:F2}·이탈 {criticalExit:F2} 에서 " +
                       "Critical 이어야 한다. 앤티 지불 시점에 판정이 일어나지 않았다는 뜻이다");
                Assert(sampled < RiskLevel.Critical,
                       $"시드 {seed}: 끝값만 본 프레임 샘플도 {sampled} 다 — 두 경로가 같으면 " +
                       "이 시드는 결함을 드러내지 못한다 (필터가 잘못 골랐다)");
                return;   // 한 건이면 충분하다. 이 성질은 존재 증명이다.
            }

            throw new Exception(
                "3000 시드 안에 「앤티에서 오르고 같은 프레임 스핀에서 내려오는」 궤적을 " +
                "찾지 못했다 — 이 경로를 검사할 수 없다");
        }

        // ── ④ 판정은 프레임 수의 함수가 아니다 ───────────────────────────────

        private static void TestEvaluateRiskIsIdempotent()
        {
            RunSession run = NewHeroSliceRun(4242);
            run.SelectContract(0);
            run.Spin();

            RiskLevel level = run.RiskLevel;
            float score = run.RiskScore;
            string reason = run.RiskReason;

            // 60fps 두 초. 상태가 하나도 안 바뀌었으므로 판정도 움직이면 안 된다.
            for (int i = 0; i < 120; i++) run.EvaluateRisk();

            Assert(run.RiskLevel == level,
                   $"상태 변화 없이 120회 재판정했더니 {level} → {run.RiskLevel} 로 움직였다");
            Assert(Math.Abs(run.RiskScore - score) < 1e-4f,
                   $"점수가 {score:F4} → {run.RiskScore:F4} 로 움직였다");
            Assert(run.RiskReason == reason,
                   $"사유가 '{reason}' → '{run.RiskReason}' 로 움직였다");
        }

        // ── ⑤ 임계값 주입이 판정까지 닿는가 ──────────────────────────────────

        /// <summary>
        /// 「배선했다」와 「그 값으로 판정한다」는 다르다. 이 저장소는 프로파일 8종이
        /// 여덟 세션 동안 죽어 있는 것을 못 본 이력이 있다 — 그래서 출처 문자열과
        /// **판정 결과 변화**를 둘 다 본다.
        /// </summary>
        private static void TestThresholdInjectionReachesJudgment()
        {
            RunSession run = NewHeroSliceRun(11);
            Assert(run.RiskThresholdSource == "필드 초기값",
                   $"주입 전 출처가 '{run.RiskThresholdSource}' 다 — 「필드 초기값」이어야 " +
                   "「코드 프리셋을 실제로 읽었다」와 구분된다");

            run.ApplyRiskThresholds(RiskThresholdProfile.DefaultSnapshot);
            Assert(run.RiskThresholdSource == RiskThresholdSnapshot.CodePresetName,
                   $"프리셋 주입 후 출처가 '{run.RiskThresholdSource}' 다");

            // 값이 실제로 판정을 바꾸는가. 잔류가 없어 점수가 0 인 시작 상태에서도
            // 진입 임계를 0 으로 내리면 단계가 올라가야 한다.
            var harsh = new RiskThresholdSnapshot(
                RiskThresholdProfile.DefaultAbsorberWeight,
                RiskThresholdProfile.DefaultProliferatorWeight,
                RiskThresholdProfile.DefaultOverharvestWeight,
                RiskThresholdProfile.DefaultOverloadScore,
                RiskThresholdProfile.DefaultShortfallScore,
                0f, 0f, 0f, 0f, "테스트 가혹");
            run.ApplyRiskThresholds(harsh);
            Assert(run.RiskLevel == RiskLevel.Critical,
                   $"모든 임계값을 0 으로 내렸는데 단계가 {run.RiskLevel} 다 — " +
                   "주입한 값이 판정에 닿지 않는다");
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────────────

        private static RunSession NewHeroSliceRun(int seed) =>
            new RunSession(seed, 0f, 0f,
                FloorSession.DefaultAnteRatio, FloorSession.DefaultAnteEscalation,
                new HeroSliceFloorSource());

        /// <summary>
        /// 과수확을 당길 수 있는 지점(Decision · 해금 · 스핀 잔여)까지 몬다.
        /// 못 가면 false — 시드마다 되는 것도 안 되는 것도 있다.
        /// </summary>
        private static bool DriveToOverharvestDecision(RunSession run)
        {
            FloorSession floor = run.Current;
            if (floor == null) return false;
            if (floor.Phase == FloorPhase.ContractSelection && !run.SelectContract(0)) return false;

            floor = run.Current;
            if (floor == null) return false;

            while (floor.SpinsRemaining > 0 && !floor.CanBank)
                if (run.Spin().Steps == null) return false;

            return floor.Phase == FloorPhase.Decision && floor.CanTakeExtraSpin;
        }

        /// <summary>
        /// 가중치를 1 로 맞춘 평가기. 재는 것은 밸런스가 아니라 **히스테리시스라는
        /// 성질**이라, 점수를 흡수체 개수로 직접 읽을 수 있게 해 둔다
        /// (`RiskEvaluatorTests.TestHysteresis` 와 같은 관례).
        /// </summary>
        private static RiskEvaluator UnitWeight() => new RiskEvaluator
        {
            AbsorberWeight = 1f,
            ProliferatorWeight = 1f,
            OverharvestWeight = 1f,
        };

        /// <summary>점수만 움직이는 입력. 스핀이 남아 있고 요구 전력도 넘겨 미달 가산이 없다.</summary>
        private static RiskInputs Absorbers(int count) =>
            new RiskInputs(count, 0, 0, false, 3, 1.2f, false);

        /// <summary>
        /// 가중치 다섯은 코드 프리셋 그대로 두고 **임계값 넷만** 바꾼다.
        /// 가중치를 건드리면 점수가 달라져 1차·2차 대조가 무의미해진다.
        /// </summary>
        private static RiskThresholdSnapshot TunedThresholds(float criticalEnter, float criticalExit)
        {
            return new RiskThresholdSnapshot(
                RiskThresholdProfile.DefaultAbsorberWeight,
                RiskThresholdProfile.DefaultProliferatorWeight,
                RiskThresholdProfile.DefaultOverharvestWeight,
                RiskThresholdProfile.DefaultOverloadScore,
                RiskThresholdProfile.DefaultShortfallScore,
                criticalExit * 0.75f,   // Strain 진입
                criticalExit * 0.50f,   // Strain 이탈
                criticalEnter, criticalExit, "테스트 튜닝");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static void Run(string name, Action test, ref int passed, ref int failed, StringBuilder report)
        {
            try { test(); passed++; report.AppendLine($"  PASS  {name}"); }
            catch (Exception e) { failed++; report.AppendLine($"  FAIL  {name}\n        {e.Message}"); }
        }
    }
}
