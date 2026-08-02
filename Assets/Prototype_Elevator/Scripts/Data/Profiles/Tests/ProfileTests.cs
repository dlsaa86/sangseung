using System;
using System.IO;
using System.Text;
using UnityEngine;
using Ascend.Prototype.Risk;
// `Ascend.Prototype.Run` 은 using 하지 않는다 — 이 클래스에 `Run` 이라는 메서드가 있어서
// 이름이 겹치는 자리를 만들 이유가 없다. `FloorSession` 한 곳만 전체 이름으로 부른다.

namespace Ascend.Prototype.Data.Profiles.Tests
{
    /// <summary>
    /// 데이터 프로파일 7종의 헤드리스 검증. `UP-PLAT-04`·`UP-PLAT-05`·`UP-POWER-07`·
    /// `UP-RISK-07`·`UP-RISK-08`·`UP-AUD-05`·`UP-TECH-09`가 여기에 걸린다.
    ///
    /// 프로파일은 "값을 옮겨 담은 것"이라 겉보기에 테스트할 것이 없어 보인다.
    /// 실제로 깨지는 지점은 값 자체가 아니라 **옮기는 과정에서 원본과 어긋나는 것**이다 —
    /// 앤티 비율이 `FloorSession`과 달라지거나, 위험 프리셋이 감사 이후의 수정을 잃거나,
    /// 요약이 9줄에서 8줄로 조용히 줄어드는 쪽. 그래서 검사 대부분이 대조다.
    ///
    /// NUnit에 의존하지 않는 이유는 `DECISION_LOG.md` D-20260730-06 참조.
    /// </summary>
    public static class ProfileTests
    {
        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0;
            int failed = 0;
            var report = new StringBuilder();

            Run("기준 하드웨어 기본값이 문서와 같다", TestHardwareDefaults, ref passed, ref failed, report);
            Run("기준 하드웨어는 미승인으로 시작한다", TestHardwareNotRatified, ref passed, ref failed, report);
            Run("vSync 상한을 비용으로 착각하지 않는다", TestVSyncCeiling, ref passed, ref failed, report);
            Run("과수확 기본값이 문서와 같다", TestOverharvestDefaults, ref passed, ref failed, report);
            Run("정적 구간 길이가 범위 밖에서 조여진다", TestClampedSilence, ref passed, ref failed, report);
            Run("앤티 식이 FloorSession 과 같다", TestAnteMatchesFloorSession, ref passed, ref failed, report);
            Run("정적 길이가 같은 시드에서 재현된다", TestSilenceIsDeterministic, ref passed, ref failed, report);
            Run("위험 프로파일이 코드 프리셋을 그대로 담는다", TestDangerPresetCopied, ref passed, ref failed, report);
            Run("단계 배열이 비면 코드 기본값으로 폴백한다", TestDangerFallback, ref passed, ref failed, report);
            Run("품질 기본값이 High 다", TestVisualQualityDefaults, ref passed, ref failed, report);
            Run("품질 3단계가 모든 축에서 단조다", TestVisualQualityMonotonic, ref passed, ref failed, report);
            Run("채널 볼륨과 정적 감쇠가 분리돼 있다", TestAudioChannels, ref passed, ref failed, report);
            Run("험 배율 기본이 1 — 형태는 위험 프로파일이 정한다", TestHumScaleIsNeutral, ref passed, ref failed, report);
            Run("셰이크 0 배율이 실제로 0을 돌려준다", TestShakeScaleZero, ref passed, ref failed, report);
            Run("섬광 금지와 빈도 상한이 적용된다", TestFlickerLimits, ref passed, ref failed, report);
            Run("사이렌을 끄면 시각·피치 보상이 붙는다", TestSirenOffCompensation, ref passed, ref failed, report);
            Run("자막을 끄면 자막 문안이 비워진다", TestSubtitleGate, ref passed, ref failed, report);
            Run("과수확 자기모순이 검출된다", TestOverharvestValidate, ref passed, ref failed, report);
            Run("런 요약이 정확히 9줄이다", TestSummaryNineLines, ref passed, ref failed, report);
            Run("빈 값에서도 9줄이 유지된다", TestSummaryKeepsLinesWhenEmpty, ref passed, ref failed, report);
            Run("요약 항목 수 상수와 열거형이 일치한다", TestSummaryFieldCount, ref passed, ref failed, report);
            Run("텍스처 카테고리가 경로로 갈린다", TestTextureCategoryByPath, ref passed, ref failed, report);
            Run("텍스처 규칙이 카테고리마다 다르다", TestTextureRulesDiffer, ref passed, ref failed, report);
            Run("오디오 갈래가 경로로 갈린다", TestAudioClassByPath, ref passed, ref failed, report);
            Run("오디오 세 갈래의 적재·압축이 서로 다르다", TestAudioRulesDiffer, ref passed, ref failed, report);
            Run("무압축 원본이 어느 갈래의 기본값도 아니다", TestNoUncompressedAudio, ref passed, ref failed, report);
            Run("임포트 규칙 폴백이 「코드 프리셋」으로 찍힌다", TestImportRuleFallbackIsNamed, ref passed, ref failed, report);
            Run("관할 루트 밖은 규칙을 받지 않는다", TestManagedRootGuard, ref passed, ref failed, report);
            Run("플레이스홀더 텍스처 4장이 디스크에 있고 PNG 로 읽힌다", TestPlaceholderTexturesExist, ref passed, ref failed, report);
            Run("플레이스홀더가 관할 안에서 World 로 판정된다", TestPlaceholderClassifiesAsWorld, ref passed, ref failed, report);
            Run("플레이스홀더 해상도가 저해상도 규격 안이다", TestPlaceholderResolution, ref passed, ref failed, report);
            Run("팔레트가 스타일 락의 채도·명도·색상각 안이다", TestPlaceholderPaletteWithinStyleLock, ref passed, ref failed, report);
            Run("픽셀이 선언된 팔레트만 쓰고 전부 쓴다", TestPlaceholderPixelsMatchPalette, ref passed, ref failed, report);
            Run("같은 시드가 같은 바이트를 낸다 (골든 해시)", TestPlaceholderGoldenHash, ref passed, ref failed, report);
            Run("파티클 상한이 AmbientParticleDirector 와 같다", TestParticleCapsMatchDirector, ref passed, ref failed, report);
            Run("정지 구간이 0이 아니다", TestPresentationHoldsAreNonZero, ref passed, ref failed, report);
            Run("연쇄 압축이 하한 아래로 내려가지 않는다", TestPresentationTempoFloor, ref passed, ref failed, report);
            Run("Reset 직후 스냅샷이 기본 스냅샷과 같다", TestResetMatchesDefaults, ref passed, ref failed, report);

            // ── 과적 3종 (`UP-TECH-09` ⑤) ────────────────────────────────────
            Run("과적 프리셋이 FloorSession 상수와 같다", TestWeightPresetMatchesFloorSession, ref passed, ref failed, report);
            Run("과적 수치를 바꾸면 허용 중량이 따라온다", TestWeightCapacityFollowsProfile, ref passed, ref failed, report);
            Run("과적 수치를 바꾸면 요구 전력이 따라온다", TestRequiredPowerFollowsProfile, ref passed, ref failed, report);
            Run("과적 배수가 과적일 때만 걸린다", TestOverloadMultiplierAppliesOnlyWhenOver, ref passed, ref failed, report);
            Run("과적 폴백이 「코드 프리셋」으로 찍힌다", TestWeightFallbackIsNamed, ref passed, ref failed, report);
            Run("과적 자기모순이 검출된다", TestWeightValidate, ref passed, ref failed, report);

            // ── 스핀 밸런스 ①③ (`UP-TECH-09`) ────────────────────────────────
            Run("스핀 프리셋이 SpinRuleSet 기본값과 같다", TestSpinPresetMatchesRuleSet, ref passed, ref failed, report);
            Run("심볼 가중치를 바꾸면 규칙 다발이 따라온다", TestSymbolWeightsFollowProfile, ref passed, ref failed, report);
            Run("패턴 배수를 바꾸면 판정이 따라온다", TestPatternMultipliersFollowProfile, ref passed, ref failed, report);
            Run("밸런스가 층 세션까지 도달한다", TestBalanceReachesFloorSession, ref passed, ref failed, report);
            Run("스핀 밸런스 폴백이 「코드 프리셋」으로 찍힌다", TestSpinBalanceFallbackIsNamed, ref passed, ref failed, report);
            Run("스핀 밸런스 자기모순이 검출된다", TestSpinBalanceValidate, ref passed, ref failed, report);
            Run("연쇄 하드 캡은 프로파일에 없다", TestCascadeCapIsNotADial, ref passed, ref failed, report);

            // ── 위험 임계값 ⑦ (`UP-TECH-09`) ─────────────────────────────────
            Run("위험 프리셋이 RiskEvaluator 초기값과 같다", TestRiskPresetMatchesEvaluator, ref passed, ref failed, report);
            Run("임계값을 바꾸면 단계 판정이 따라온다", TestRiskThresholdsFollowProfile, ref passed, ref failed, report);
            Run("Apply 이전과 이후의 출처가 구분된다", TestRiskThresholdSourceDistinguishes, ref passed, ref failed, report);
            Run("히스테리시스 역전이 검출된다", TestRiskHysteresisValidate, ref passed, ref failed, report);
            Run("과수확 한 번이 방을 못 바꾸는 값이 검출된다", TestOverharvestMustLeaveStable, ref passed, ref failed, report);

            // ── 승객 대사 ⑨ (`UP-TECH-09`) ───────────────────────────────────
            Run("Reset 이 11종을 대사까지 채운다", TestReactionResetFillsLines, ref passed, ref failed, report);
            Run("빈 대사는 코드 기본값으로 채워진다", TestReactionLineFallsBack, ref passed, ref failed, report);
            Run("에셋 대사가 코드 기본값을 이긴다", TestReactionLineOverrides, ref passed, ref failed, report);
            Run("출하 에셋의 대사 직렬화가 전부거나 전무다", TestShippedReactionAssetLineCoverage, ref passed, ref failed, report);

            report.Insert(0, "[상승] === Data Profile Tests ===\n");
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

        private static bool Near(float a, float b, float tolerance = 0.0005f) => Math.Abs(a - b) <= tolerance;

        // ── UP-PLAT-04 ────────────────────────────────────────────────────────

        private static string TestHardwareDefaults()
        {
            TargetHardwareSnapshot hardware = TargetHardwareProfile.DefaultSnapshot;

            if (!Near(hardware.TargetFps, 90f)) return $"목표 FPS {hardware.TargetFps}, 기대 90 (TECH_SPEC §13)";
            if (!Near(hardware.HardFloorFps, 60f)) return $"하한 FPS {hardware.HardFloorFps}, 기대 60";
            if (hardware.ReferenceWidth != 1920 || hardware.ReferenceHeight != 1080)
                return $"기준 해상도 {hardware.ReferenceWidth}×{hardware.ReferenceHeight}, 기대 1920×1080";
            if (!Near(hardware.TargetFrameTimeMs, 1000f / 90f, 0.01f))
                return $"목표 프레임 시간 {hardware.TargetFrameTimeMs:0.000}ms, 기대 {1000f / 90f:0.000}ms";
            if (!Near(hardware.HardFloorFrameTimeMs, 1000f / 60f, 0.01f))
                return $"하한 프레임 시간 {hardware.HardFloorFrameTimeMs:0.000}ms, 기대 {1000f / 60f:0.000}ms";
            if (!hardware.MeasureWithVSyncOff) return "기본이 vSync 켠 채 측정으로 돼 있다";

            // 기기 문자열이 PD-10 의 개발 기기와 같아야 한다 — 다르면 과거 측정과 비교되지 않는다.
            if (hardware.Gpu == null || hardware.Gpu.IndexOf("3070", StringComparison.Ordinal) < 0)
                return $"GPU '{hardware.Gpu}' 가 PD-10 의 RTX 3070 이 아니다";
            if (hardware.Cpu == null || hardware.Cpu.IndexOf("5600X", StringComparison.Ordinal) < 0)
                return $"CPU '{hardware.Cpu}' 가 PD-10 의 Ryzen 5 5600X 가 아니다";
            return null;
        }

        private static string TestHardwareNotRatified()
        {
            TargetHardwareSnapshot hardware = TargetHardwareProfile.DefaultSnapshot;
            if (hardware.Ratified) return "코드 기본값이 승인된 상태로 돼 있다 — PD-10 이 미결이다";

            string line = hardware.Describe();
            if (line.IndexOf("PD-10", StringComparison.Ordinal) < 0)
                return $"미승인 사실이 리포트 한 줄에 안 보인다: {line}";
            if (line.IndexOf("90", StringComparison.Ordinal) < 0)
                return $"목표 FPS 가 리포트 한 줄에 안 보인다: {line}";
            return null;
        }

        private static string TestVSyncCeiling()
        {
            // 120Hz vSync 를 켜고 잰 8.33ms 는 비용이 아니라 상한이다. 실제로 이 값을
            // "여유 있다"로 읽은 이력이 백로그에 있다.
            var withVSync = new TargetHardwareSnapshot("cpu", "gpu", "ram", "os", "api",
                1920, 1080, 120f, false, 90f, 60f, false);
            if (!withVSync.LooksLikeVSyncCeiling(8.33f))
                return "vSync 켠 120Hz 측정의 8.33ms 를 상한으로 인식하지 못한다";
            if (withVSync.LooksLikeVSyncCeiling(14.0f))
                return "상한과 무관한 14ms 를 상한으로 잘못 인식한다";

            var withoutVSync = new TargetHardwareSnapshot("cpu", "gpu", "ram", "os", "api",
                1920, 1080, 120f, true, 90f, 60f, false);
            if (withoutVSync.LooksLikeVSyncCeiling(8.33f))
                return "vSync 를 끄고 쟀는데 상한이라고 판정한다";
            return null;
        }

        // ── UP-POWER-07 ───────────────────────────────────────────────────────

        /// <summary>
        /// 9개 항목이 전부 있는지를 "값 하나씩 확인"으로 검사한다. 상수를 9와 비교하는 방식은
        /// 컴파일 시점에 접혀 아무것도 지키지 못한다 — 항목이 실제로 존재하고 문서가 말한
        /// 값을 갖는지가 `UP-POWER-07` 이 요구하는 것이다.
        /// </summary>
        private static string TestOverharvestDefaults()
        {
            OverharvestSnapshot overharvest = OverharvestProfile.DefaultSnapshot;

            if (!Near(overharvest.AnteRatio, 0.12f)) return $"판돈 비율 {overharvest.AnteRatio}, 기대 0.12";
            if (!Near(overharvest.AnteEscalation, 0.35f)) return $"판돈 상승률 {overharvest.AnteEscalation}, 기대 0.35";
            if (!Near(overharvest.UnlockThreshold, 1.00f)) return $"잠금 해제 임계 {overharvest.UnlockThreshold}, 기대 1.00";
            if (!Near(overharvest.MinSilenceSeconds, 0.3f)) return $"정적 최소 {overharvest.MinSilenceSeconds}, 기대 0.3 (PRD §7.3)";
            if (!Near(overharvest.MaxSilenceSeconds, 0.7f)) return $"정적 최대 {overharvest.MaxSilenceSeconds}, 기대 0.7";
            if (overharvest.MinSilenceSeconds > overharvest.MaxSilenceSeconds) return "정적 최소가 최대보다 크다";
            if (!Near(overharvest.PassengerGazeDelaySeconds, 0.18f))
                return $"승객 응시 지연 {overharvest.PassengerGazeDelaySeconds}, 기대 0.18";
            if (!Near(overharvest.ResumeFadeSeconds, 0.25f))
                return $"재개 페이드 {overharvest.ResumeFadeSeconds}, 기대 0.25";
            if (overharvest.MaxExtraSpins != 4)
                return $"최대 추가 스핀 {overharvest.MaxExtraSpins}, 기대 4 (PRD §4.1 「한 층 최대 5회 스핀」)";

            // 접근 감쇠는 0 이면 안 된다 — 접근 즉시 무음이면 뒤에 오는 정적이 사건으로 읽히지 않는다.
            if (overharvest.ApproachMachineDuckScale <= 0f || overharvest.ApproachMachineDuckScale >= 1f)
                return $"접근 감쇠 배율 {overharvest.ApproachMachineDuckScale} 가 (0,1) 밖이다";

            // 잠금은 100% 달성에서 풀린다(PRD §7.1).
            if (overharvest.IsUnlocked(99f, 100f)) return "99% 에서 잠금이 풀렸다";
            if (!overharvest.IsUnlocked(100f, 100f)) return "100% 에서 잠금이 풀리지 않았다";
            return null;
        }

        private static string TestClampedSilence()
        {
            OverharvestSnapshot overharvest = OverharvestProfile.DefaultSnapshot;

            if (!Near(overharvest.ClampedSilenceSeconds(0.05f), 0.3f))
                return $"하한 미만이 조여지지 않았다: {overharvest.ClampedSilenceSeconds(0.05f)}";
            if (!Near(overharvest.ClampedSilenceSeconds(9f), 0.7f))
                return $"상한 초과가 조여지지 않았다: {overharvest.ClampedSilenceSeconds(9f)}";
            if (!Near(overharvest.ClampedSilenceSeconds(0.5f), 0.5f))
                return $"범위 안의 값이 바뀌었다: {overharvest.ClampedSilenceSeconds(0.5f)}";
            if (!Near(overharvest.ClampedSilenceSeconds(-4f), 0.3f))
                return $"음수가 하한으로 조여지지 않았다: {overharvest.ClampedSilenceSeconds(-4f)}";

            // 인스펙터에서 최소·최대가 뒤집힐 수 있다. 그때 무한 정적이 되면 안 된다.
            var reversed = new OverharvestSnapshot(0.12f, 0.35f, 1f, 0.35f,
                0.9f, 0.2f, 0.18f, 0.25f, 4);
            float clamped = reversed.ClampedSilenceSeconds(5f);
            if (clamped < 0.2f - 0.0005f || clamped > 0.9f + 0.0005f)
                return $"최소>최대 상태에서 {clamped} 가 나왔다 (0.2~0.9 기대)";
            return null;
        }

        private static string TestAnteMatchesFloorSession()
        {
            // 프로파일이 생겼는데 밸런스가 조용히 바뀌면 안 된다. 두 값은 같은 수여야 한다.
            const float sessionRatio = Ascend.Prototype.Run.FloorSession.DefaultAnteRatio;
            const float sessionEscalation = Ascend.Prototype.Run.FloorSession.DefaultAnteEscalation;

            if (!Near(OverharvestProfile.DefaultAnteRatio, sessionRatio))
                return $"판돈 비율 {OverharvestProfile.DefaultAnteRatio} vs FloorSession {sessionRatio}";
            if (!Near(OverharvestProfile.DefaultAnteEscalation, sessionEscalation))
                return $"상승률 {OverharvestProfile.DefaultAnteEscalation} vs FloorSession {sessionEscalation}";

            OverharvestSnapshot overharvest = OverharvestProfile.DefaultSnapshot;
            for (int taken = 0; taken < 5; taken++)
            {
                float expected = sessionRatio * (1f + sessionEscalation * taken);
                if (!Near(overharvest.AnteRatioForPull(taken), expected))
                    return $"{taken}회 후 비율 {overharvest.AnteRatioForPull(taken)}, 기대 {expected}";
            }

            if (!Near(overharvest.AnteFor(200f, 1), 200f * 0.12f * 1.35f))
                return $"판돈 금액 {overharvest.AnteFor(200f, 1)}, 기대 {200f * 0.12f * 1.35f}";

            // 남은 스핀이 프로파일 상한보다 적으면 남은 스핀이 이긴다.
            if (overharvest.EffectiveExtraSpinLimit(2) != 2)
                return $"남은 스핀 2에서 상한 {overharvest.EffectiveExtraSpinLimit(2)}";
            if (overharvest.EffectiveExtraSpinLimit(9) != overharvest.MaxExtraSpins)
                return "남은 스핀이 넉넉한데 프로파일 상한이 적용되지 않았다";
            return null;
        }

        private static string TestSilenceIsDeterministic()
        {
            OverharvestSnapshot overharvest = OverharvestProfile.DefaultSnapshot;
            float first = overharvest.SilenceSecondsFor(4242);
            float second = overharvest.SilenceSecondsFor(4242);
            if (!Near(first, second)) return $"같은 시드에서 {first} vs {second}";
            if (first < 0.3f - 0.0005f || first > 0.7f + 0.0005f)
                return $"뽑힌 정적 길이 {first} 가 0.3~0.7 밖이다";

            // 시드가 다르면 값도 달라야 한다 — 아니면 시드를 안 쓰는 것이다.
            bool anyDifferent = false;
            for (int seed = 1; seed < 12 && !anyDifferent; seed++)
                if (!Near(overharvest.SilenceSecondsFor(seed), first, 0.0001f)) anyDifferent = true;
            if (!anyDifferent) return "어떤 시드에서도 같은 길이가 나온다";
            return null;
        }

        // ── UP-RISK-07 / UP-RISK-08 ───────────────────────────────────────────

        private static string TestDangerPresetCopied()
        {
            var profile = ScriptableObject.CreateInstance<DangerFeedbackProfile>();
            try
            {
                profile.Reset();
                RiskProfile[] expected = RiskProfile.Preset(RiskIntensity.Standard);

                for (int i = 0; i < DangerFeedbackProfile.LevelCount; i++)
                {
                    RiskProfile actual = profile.For((RiskLevel)i);
                    if (!Near(actual.LightIntensity, expected[i].LightIntensity))
                        return $"{(RiskLevel)i} 밝기 {actual.LightIntensity}, 기대 {expected[i].LightIntensity}";
                    if (!Near(actual.LightColor.r, expected[i].LightColor.r)
                        || !Near(actual.LightColor.g, expected[i].LightColor.g)
                        || !Near(actual.LightColor.b, expected[i].LightColor.b))
                        return $"{(RiskLevel)i} 조명 색이 코드 프리셋과 다르다";
                    if (!Near(actual.HumVolume, expected[i].HumVolume))
                        return $"{(RiskLevel)i} 험 음량 {actual.HumVolume}, 기대 {expected[i].HumVolume}";
                }

                // 프리셋 교체가 진입점 하나로 끝나야 한다(PRD Phase 6 「대규모 코드 수정 없이」).
                profile.ApplyPreset(RiskIntensity.Heavy);
                RiskProfile[] heavy = RiskProfile.Preset(RiskIntensity.Heavy);
                if (!Near(profile.For(RiskLevel.Critical).LightIntensity, heavy[2].LightIntensity))
                    return "ApplyPreset(Heavy) 이후에도 값이 표준 그대로다";
                if (profile.SourcePreset != RiskIntensity.Heavy) return "출처 프리셋이 갱신되지 않았다";

                // 범위 밖 단계로 배열 밖을 읽지 않는다.
                if (!Near(profile.For((RiskLevel)99).LightIntensity, heavy[3].LightIntensity))
                    return "범위 밖 단계가 마지막 단계로 조여지지 않는다";
                return null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        private static string TestDangerFallback()
        {
            // 에셋이 없거나 배열이 비었을 때 조명이 꺼진 채 진행되면 안 된다.
            var empty = new DangerFeedbackSnapshot(RiskIntensity.Standard, "표준", null);
            RiskProfile[] expected = RiskProfile.Preset(RiskIntensity.Standard);
            if (!Near(empty.For(RiskLevel.Critical).LightIntensity, expected[2].LightIntensity))
                return "빈 배열에서 코드 기본값으로 폴백하지 않는다";

            var tooShort = new DangerFeedbackSnapshot(RiskIntensity.Standard, "표준", new RiskProfile[2]);
            if (!Near(tooShort.For(RiskLevel.Collapse).HumVolume, expected[3].HumVolume))
                return "길이가 모자란 배열에서 폴백하지 않는다";

            DangerFeedbackSnapshot fallback = DangerFeedbackProfile.DefaultSnapshot;
            if (fallback.SourcePreset != RiskIntensity.Standard) return "기본 스냅샷이 Standard 가 아니다";
            if (!Near(fallback.For(RiskLevel.Stable).LightIntensity, expected[0].LightIntensity))
                return "기본 스냅샷의 Stable 값이 코드 프리셋과 다르다";
            return null;
        }

        private static string TestShakeScaleZero()
        {
            // PD-07 이 "접근성 옵션으로 0까지 낮출 수 있어야 한다"를 제약으로 걸어 뒀다.
            var off = new AccessibilitySnapshot(0f, 1f, true, 3f, 1f, true, 1f, true);
            if (!Near(off.ScaleShake(0.0045f), 0f)) return $"셰이크 0 배율에서 {off.ScaleShake(0.0045f)} 가 나왔다";
            if (!Near(off.ScaleShake(999f), 0f)) return "큰 입력에서도 0이어야 한다";

            // 카메라를 껐다고 환경 흔들림까지 사라지면 위험 단계가 안 읽힌다(PRD §8.3).
            if (Near(off.ScaleSway(0.018f), 0f)) return "카메라 셰이크를 껐더니 환경 흔들림까지 0이 됐다";

            AccessibilitySnapshot on = AccessibilityProfile.DefaultSnapshot;
            if (!Near(on.ScaleShake(0.0045f), 0.0045f)) return "기본 상태에서 셰이크가 줄었다";
            if (!on.ShowSubtitles) return "기본이 자막 끔으로 돼 있다";
            if (!Near(on.MaxFlickerHz, 3f)) return $"기본 섬광 상한 {on.MaxFlickerHz}, 기대 3Hz";
            return null;
        }

        private static string TestFlickerLimits()
        {
            AccessibilitySnapshot on = AccessibilityProfile.DefaultSnapshot;
            if (!on.AllowFlickerAt(2.2f)) return "상한 아래 빈도가 금지됐다";
            if (on.AllowFlickerAt(6.5f)) return "상한을 넘긴 빈도가 허용됐다";
            if (!Near(on.ClampFlickerRate(6.5f), 3f)) return $"빈도 상한 조임 결과 {on.ClampFlickerRate(6.5f)}, 기대 3";
            if (!Near(on.ClampFlickerRate(2.2f), 2.2f)) return "상한 아래 값이 바뀌었다";

            var noFlicker = new AccessibilitySnapshot(1f, 1f, false, 3f, 1f, true, 1f, true);
            if (noFlicker.AllowFlickerAt(1f)) return "섬광 금지 상태에서 허용됐다";
            if (!Near(noFlicker.ClampFlickerRate(4.8f), 0f)) return "섬광 금지 상태에서 빈도가 0이 아니다";
            if (!Near(noFlicker.ScaleFlickerDepth(0.3f), 0f)) return "섬광 금지 상태에서 깊이가 0이 아니다";

            var noSiren = new AccessibilitySnapshot(1f, 1f, true, 3f, 1f, false, 0.5f, true);
            if (!Near(noSiren.SirenVolume(0.8f), 0f)) return "사이렌 금지 상태에서 볼륨이 남았다";
            if (!Near(noSiren.ScaleLowFrequency(0.4f), 0.2f)) return "저주파 감쇠가 적용되지 않았다";
            return null;
        }

        /// <summary>
        /// 사이렌을 끈 상태의 **보상**을 검사한다. 검사 대상은 "꺼졌는가"가 아니라
        /// "청각 채널을 지운 만큼 다른 채널이 커졌는가"다 — 접근성 옵션이 정보를
        /// 지우면 안 된다는 것이 `UP-RISK-08` 의 요구다(PRD §9).
        /// </summary>
        private static string TestSirenOffCompensation()
        {
            AccessibilitySnapshot on = AccessibilityProfile.DefaultSnapshot;
            if (!Near(on.CompensateWarningEmission(0.6f), 0.6f))
                return "사이렌이 켜져 있는데 경고등 발광이 바뀌었다 — 기존 캡처가 흔들린다";
            if (!Near(on.CompensateHumPitch(1.4f), 1.4f))
                return "사이렌이 켜져 있는데 험 피치가 바뀌었다";

            var off = new AccessibilitySnapshot(1f, 1f, true, 3f, 1f, false, 1f, true);
            float boosted = off.CompensateWarningEmission(0.6f);
            if (boosted <= 0.6f)
                return $"사이렌을 껐는데 경고등이 그대로다 ({boosted:0.###}) — 경고가 통째로 약해진다";
            if (!Near(boosted, 0.6f * AccessibilitySnapshot.SirenOffWarningBoost))
                return $"경고등 보상 배수가 상수와 다르다 ({boosted:0.###})";

            // 피치는 절대값이 아니라 1.0 기준 **편차**만 벌린다. 절대 피치를 올리면
            // 험이 다른 기계가 되고, 그건 보상이 아니라 사운드 변경이다.
            if (!Near(off.CompensateHumPitch(1f), 1f))
                return "중립 피치(1.0)가 사이렌 설정에 따라 움직였다";
            float widened = off.CompensateHumPitch(1.4f);
            if (widened <= 1.4f) return $"사이렌을 껐는데 피치 편차가 안 벌어졌다 ({widened:0.###})";
            if (!Near(widened, 1f + 0.4f * AccessibilitySnapshot.SirenOffPitchBoost))
                return $"피치 보상이 상수와 다르다 ({widened:0.###})";

            // 낮은 쪽 편차도 같은 비율로 벌어져야 방향이 대칭이다.
            float lowered = off.CompensateHumPitch(0.8f);
            if (lowered >= 0.8f) return $"1.0 아래 피치가 반대로 움직였다 ({lowered:0.###})";
            return null;
        }

        /// <summary>
        /// 자막 게이트. 값을 바꿨을 때 실제로 결과가 달라지는지만 본다 —
        /// 「읽히지만 아무 일도 안 일어난다」가 이 저장소의 반복 실패다.
        /// </summary>
        private static string TestSubtitleGate()
        {
            AccessibilitySnapshot on = AccessibilityProfile.DefaultSnapshot;
            if (on.Caption("[기계 험]") != "[기계 험]") return "자막이 켜져 있는데 문안이 사라졌다";

            var off = new AccessibilitySnapshot(1f, 1f, true, 3f, 1f, true, 1f, false);
            if (!string.IsNullOrEmpty(off.Caption("[기계 험]"))) return "자막을 껐는데 문안이 남았다";

            // null 문안에서 예외를 던지면 자막을 켠 순간 게임이 죽는다.
            if (!string.IsNullOrEmpty(on.Caption(null))) return "빈 문안이 빈 문자열로 오지 않는다";
            return null;
        }

        /// <summary>
        /// 과수확 값이 「범위 안이지만 기능을 죽이는」 조합인지 잡는다.
        /// 이 검사가 실제로 도는 것이 `OverharvestProfile` 9개 필드가 런타임에서
        /// 읽힌다는 증거다 — 여섯 개는 아직 게임 로직 소비처가 없다.
        /// </summary>
        private static string TestOverharvestValidate()
        {
            if (OverharvestProfile.DefaultSnapshot.Validate() != null)
                return $"기본값이 자기모순으로 판정된다 — {OverharvestProfile.DefaultSnapshot.Validate()}";

            var noSpins = new OverharvestSnapshot(0.12f, 0.35f, 1f, 0.35f, 0.3f, 0.7f, 0.18f, 0.25f, 0);
            if (noSpins.Validate() == null) return "추가 스핀 상한 0이 통과됐다 — 레버가 장식이 된다";

            var freeAnte = new OverharvestSnapshot(0f, 0.35f, 1f, 0.35f, 0.3f, 0.7f, 0.18f, 0.25f, 4);
            if (freeAnte.Validate() == null) return "판돈 0이 통과됐다 — 과수확이 공짜가 된다";

            var reversed = new OverharvestSnapshot(0.12f, 0.35f, 1f, 0.35f, 0.9f, 0.4f, 0.18f, 0.25f, 4);
            if (reversed.Validate() == null) return "정적 최소 > 최대가 통과됐다";

            var noDuck = new OverharvestSnapshot(0.12f, 0.35f, 1f, 1f, 0.3f, 0.7f, 0.18f, 0.25f, 4);
            if (noDuck.Validate() == null) return "접근 감쇠 배율 1.0이 통과됐다 — 5단계 연출이 사라진다";

            // 경고문에 값이 안 들어가면 재현할 수가 없다. 9개가 전부 찍혀야 한다.
            string described = OverharvestProfile.DefaultSnapshot.Describe();
            if (string.IsNullOrEmpty(described)) return "Describe 가 비었다";
            if (described.IndexOf("추가스핀", StringComparison.Ordinal) < 0)
                return $"Describe 에 추가 스핀 상한이 빠졌다 — {described}";
            if (described.IndexOf("응시지연", StringComparison.Ordinal) < 0)
                return $"Describe 에 응시 지연이 빠졌다 — {described}";
            return null;
        }

        // ── UP-PLAT-05 ────────────────────────────────────────────────────────

        private static string TestVisualQualityDefaults()
        {
            VisualQualitySnapshot quality = VisualQualityProfile.DefaultSnapshot;
            if (quality.Tier != VisualQualityTier.High) return $"기본 단계 {quality.Tier}, 기대 High";
            if (!quality.PostProcessing) return "High 인데 포스트프로세싱이 꺼져 있다";
            if (!Near(quality.RenderScale, 1f)) return $"렌더 스케일 {quality.RenderScale}, 기대 1.0";
            if (quality.MaxRealtimeLights <= 0) return "실시간 광원 예산이 0 이하다";

            if (!quality.WithinBudget(quality.MaxRealtimeLights, quality.MaxSimultaneousParticles, quality.OverdrawBudget))
                return "예산과 정확히 같은 실측이 초과로 판정된다";
            if (quality.WithinBudget(quality.MaxRealtimeLights + 1, 0, 1f))
                return "광원 초과가 통과된다";
            if (quality.WithinBudget(0, quality.MaxSimultaneousParticles + 1, 1f))
                return "파티클 초과가 통과된다";
            return null;
        }

        private static string TestVisualQualityMonotonic()
        {
            VisualQualitySnapshot low = VisualQualityProfile.PresetFor(VisualQualityTier.Low);
            VisualQualitySnapshot medium = VisualQualityProfile.PresetFor(VisualQualityTier.Medium);
            VisualQualitySnapshot high = VisualQualityProfile.PresetFor(VisualQualityTier.High);

            if (!(low.MaxRealtimeLights <= medium.MaxRealtimeLights && medium.MaxRealtimeLights <= high.MaxRealtimeLights))
                return "광원 수가 단조가 아니다";
            if (!(low.ShadowDistance <= medium.ShadowDistance && medium.ShadowDistance <= high.ShadowDistance))
                return "그림자 거리가 단조가 아니다";
            if (!(low.MaxSimultaneousParticles <= medium.MaxSimultaneousParticles
                  && medium.MaxSimultaneousParticles <= high.MaxSimultaneousParticles))
                return "파티클 상한이 단조가 아니다";
            if (!(low.OverdrawBudget <= medium.OverdrawBudget && medium.OverdrawBudget <= high.OverdrawBudget))
                return "오버드로우 예산이 단조가 아니다";
            if (!(low.RenderScale <= medium.RenderScale && medium.RenderScale <= high.RenderScale))
                return "렌더 스케일이 단조가 아니다";
            if (low.PostProcessing && !high.PostProcessing)
                return "Low 는 포스트를 켜는데 High 는 꺼져 있다";

            if (low.Describe() == high.Describe()) return "Low 와 High 의 리포트 문장이 같다";
            return null;
        }

        // ── UP-AUD-05 ─────────────────────────────────────────────────────────

        private static string TestAudioChannels()
        {
            AudioMixSnapshot mix = AudioMixProfile.DefaultSnapshot;

            if (!Near(mix.VolumeFor(AudioChannel.Master), 1f)) return $"마스터 {mix.VolumeFor(AudioChannel.Master)}, 기대 1";
            if (!Near(mix.VolumeFor(AudioChannel.Machine), 0.55f)) return $"기계 {mix.VolumeFor(AudioChannel.Machine)}, 기대 0.55";

            // 마스터는 정적에서 건드리지 않는다 — 전체가 사라지면 고장으로 읽힌다.
            if (!Near(mix.DuckScaleFor(AudioChannel.Master), 1f))
                return $"마스터 감쇠 {mix.DuckScaleFor(AudioChannel.Master)}, 기대 1";

            foreach (AudioChannel channel in new[]
                     { AudioChannel.Machine, AudioChannel.Event, AudioChannel.Passenger, AudioChannel.Warning })
            {
                if (mix.DuckScaleFor(channel) >= 1f) return $"{channel} 이 정적에서 줄지 않는다";
                if (mix.DuckScaleFor(channel) < 0f) return $"{channel} 감쇠 배율이 음수다";
                if (!Near(mix.DuckedVolumeFor(channel), mix.VolumeFor(channel) * mix.DuckScaleFor(channel)))
                    return $"{channel} 의 감쇠 볼륨이 볼륨×배율이 아니다";
            }

            // 승객은 정적에 남아야 한다 — 그때 들리는 유일한 소리가 「보고 있다」를 만든다.
            if (mix.DuckScaleFor(AudioChannel.Passenger) <= mix.DuckScaleFor(AudioChannel.Machine))
                return "승객이 기계음보다 더 깊게 잘린다 — 정적에 남을 소리가 없어진다";

            // 감쇠 진행 중 값은 양 끝에서 정확히 맞아야 한다.
            if (!Near(mix.VolumeDuring(AudioChannel.Machine, 0f), mix.VolumeFor(AudioChannel.Machine)))
                return "감쇠량 0 에서 평상시 볼륨이 아니다";
            if (!Near(mix.VolumeDuring(AudioChannel.Machine, 1f), mix.DuckedVolumeFor(AudioChannel.Machine)))
                return "감쇠량 1 에서 완전 감쇠 볼륨이 아니다";
            if (!Near(mix.VolumeDuring(AudioChannel.Machine, 3f), mix.DuckedVolumeFor(AudioChannel.Machine)))
                return "감쇠량이 1을 넘어도 조여지지 않는다";
            return null;
        }

        private static string TestHumScaleIsNeutral()
        {
            AudioMixSnapshot mix = AudioMixProfile.DefaultSnapshot;
            RiskProfile[] preset = RiskProfile.Preset(RiskIntensity.Standard);

            for (int i = 0; i < preset.Length; i++)
            {
                var level = (RiskLevel)i;
                if (!Near(mix.HumVolumeScaleFor(level), 1f))
                    return $"{level} 험 볼륨 배율 {mix.HumVolumeScaleFor(level)}, 기대 1 (형태는 DangerFeedbackProfile 이 정한다)";
                if (!Near(mix.HumPitchScaleFor(level), 1f))
                    return $"{level} 험 피치 배율 {mix.HumPitchScaleFor(level)}, 기대 1";

                RiskProfile risk = preset[i];
                float expected = risk.HumVolume * mix.VolumeFor(AudioChannel.Machine) * mix.VolumeFor(AudioChannel.Master);
                if (!Near(mix.HumVolumeFor(level, risk), expected))
                    return $"{level} 최종 험 볼륨 {mix.HumVolumeFor(level, risk)}, 기대 {expected}";
                if (!Near(mix.HumPitchFor(level, risk), risk.HumPitch))
                    return $"{level} 최종 험 피치 {mix.HumPitchFor(level, risk)}, 기대 {risk.HumPitch}";
            }

            // 위험이 오르면 험도 커져야 한다. 배율이 중립이므로 이 성질은 위험 프로파일에서 온다.
            RiskProfile stable = preset[0];
            RiskProfile critical = preset[2];
            if (mix.HumVolumeFor(RiskLevel.Critical, critical) <= mix.HumVolumeFor(RiskLevel.Stable, stable))
                return "Critical 의 험이 Stable 보다 크지 않다";
            return null;
        }

        // ── UP-REC-02 / UP-TECH-09 ────────────────────────────────────────────

        private static RunSummaryData SampleSummary()
        {
            return new RunSummaryData(
                highestFloor: 7,
                peakCascade: 5,
                peakOverharvestRatio: 1.34f,
                keyContract: "흡수체 계약",
                keyLoadout: "짐꾼 · 잔류 정류기",
                endCause: "추락 — 요구 전력의 70% 미만",
                lastOverharvestChoice: "확정하지 않고 한 번 더 당김",
                lostCargo: "승객 2명",
                runSeed: 4242);
        }

        private static string TestSummaryNineLines()
        {
            RunSummarySnapshot template = RunSummaryTemplate.DefaultSnapshot;
            RunSummaryData data = SampleSummary();
            string[] lines = template.ComposeLines(in data);

            if (lines.Length != RunSummaryTemplate.RequiredFieldCount)
                return $"{lines.Length} 줄, 기대 {RunSummaryTemplate.RequiredFieldCount} 줄";

            for (int i = 0; i < lines.Length; i++)
                if (string.IsNullOrEmpty(lines[i])) return $"{i} 번째 줄이 비었다";

            // 각 줄이 실제 값을 담고 있어야 한다. 라벨만 9줄 찍히는 것은 통과가 아니다.
            if (lines[0].IndexOf("7", StringComparison.Ordinal) < 0) return $"최고 층이 안 보인다: {lines[0]}";
            if (lines[1].IndexOf("5", StringComparison.Ordinal) < 0) return $"최고 캐스케이드가 안 보인다: {lines[1]}";
            if (lines[2].IndexOf("134", StringComparison.Ordinal) < 0) return $"과수확 비율이 안 보인다: {lines[2]}";
            if (lines[3].IndexOf("흡수체", StringComparison.Ordinal) < 0) return $"계약이 안 보인다: {lines[3]}";
            if (lines[4].IndexOf("짐꾼", StringComparison.Ordinal) < 0) return $"적재가 안 보인다: {lines[4]}";
            if (lines[5].IndexOf("추락", StringComparison.Ordinal) < 0) return $"종료 원인이 안 보인다: {lines[5]}";
            if (lines[6].IndexOf("당김", StringComparison.Ordinal) < 0) return $"마지막 선택이 안 보인다: {lines[6]}";
            if (lines[7].IndexOf("승객 2명", StringComparison.Ordinal) < 0) return $"잃은 것이 안 보인다: {lines[7]}";
            if (lines[8].IndexOf("4242", StringComparison.Ordinal) < 0) return $"런 시드가 안 보인다: {lines[8]}";

            // 시드가 들어 있어야 이 요약 하나로 같은 런을 다시 돌릴 수 있다(PRD §10).
            string composed = template.Compose(in data);
            if (composed.Split('\n').Length != RunSummaryTemplate.RequiredFieldCount)
                return "Compose 결과의 줄 수가 9가 아니다";
            return null;
        }

        private static string TestSummaryKeepsLinesWhenEmpty()
        {
            RunSummarySnapshot template = RunSummaryTemplate.DefaultSnapshot;
            var blank = new RunSummaryData(0, 0, 0f, null, string.Empty, null, string.Empty, null, 0);
            string[] lines = template.ComposeLines(in blank);

            if (lines.Length != RunSummaryTemplate.RequiredFieldCount)
                return $"빈 값에서 {lines.Length} 줄이 나왔다";
            for (int i = 0; i < lines.Length; i++)
                if (string.IsNullOrEmpty(lines[i])) return $"빈 값에서 {i} 번째 줄이 사라졌다";

            // 0층·시드 0 은 없는 값이 아니라 실제 값이다.
            if (lines[0].IndexOf("0", StringComparison.Ordinal) < 0) return $"0층이 「기록 없음」으로 바뀌었다: {lines[0]}";
            if (lines[8].IndexOf("0", StringComparison.Ordinal) < 0) return $"시드 0 이 「기록 없음」으로 바뀌었다: {lines[8]}";

            // 반대로 문자열이 비면 자리표시자가 들어가야 한다.
            if (lines[3].IndexOf(RunSummaryTemplate.DefaultMissingText, StringComparison.Ordinal) < 0)
                return $"빈 계약에 자리표시자가 없다: {lines[3]}";
            if (lines[4].IndexOf(RunSummaryTemplate.DefaultMissingText, StringComparison.Ordinal) < 0)
                return $"빈 적재에 자리표시자가 없다: {lines[4]}";
            return null;
        }

        private static string TestSummaryFieldCount()
        {
            // 상수를 9와 직접 비교하면 컴파일 시점에 접혀 검사가 사라진다(CS0162).
            // 런타임에 실제로 세어지는 것 — 열거형 멤버 수와 만들어진 줄 수 — 으로 대조한다.
            Array fields = Enum.GetValues(typeof(RunSummaryField));
            RunSummaryData data = SampleSummary();
            string[] lines = RunSummaryTemplate.DefaultSnapshot.ComposeLines(in data);

            if (lines.Length != 9) return $"요약 줄 수 {lines.Length}, PRD §10 은 9종";
            if (fields.Length != lines.Length)
                return $"RunSummaryField 가 {fields.Length} 개인데 줄은 {lines.Length} 개다";
            if (fields.Length != RunSummaryTemplate.RequiredFieldCount)
                return $"RunSummaryField 가 {fields.Length} 개, 상수는 {RunSummaryTemplate.RequiredFieldCount}";

            // 열거형 값이 0..8 로 빈틈없이 이어져야 배열 인덱스로 쓸 수 있다.
            // 박싱된 열거형은 int 로 직접 언박싱되지 않으므로 열거형으로 먼저 캐스팅한다.
            for (int i = 0; i < fields.Length; i++)
                if ((int)(RunSummaryField)fields.GetValue(i) != i)
                    return $"RunSummaryField 값이 연속이 아니다: {fields.GetValue(i)} 가 {i} 자리에 있다";
            return null;
        }

        // ── UP-PLAT-05 / UP-AUD-05 임포트 규칙 ────────────────────────────────

        private const string Root = AssetImportPaths.ManagedRoot;

        private static string TestTextureCategoryByPath()
        {
            // 판정 순서가 규칙의 일부다 — 노멀맵이 먼저다.
            var cases = new[]
            {
                (Root + "Art/UI/panel_bg.png",            TextureAssetCategory.Ui),
                (Root + "Art/Hud/needle.png",             TextureAssetCategory.Ui),
                (Root + "Art/World/wall_plate.png",       TextureAssetCategory.World),
                (Root + "Art/World/wall_plate_n.png",     TextureAssetCategory.NormalMap),
                (Root + "Art/Vfx/spark_n.png",            TextureAssetCategory.NormalMap),
                (Root + "Art/Vfx/spark.png",              TextureAssetCategory.Vfx),
                (Root + "Art/Particles/dust.png",         TextureAssetCategory.Vfx),
                (Root + "Art/anything_else.png",          TextureAssetCategory.World),
            };

            foreach (var pair in cases)
            {
                TextureAssetCategory actual = AssetImportPaths.ClassifyTexture(pair.Item1);
                if (actual != pair.Item2)
                    return $"{pair.Item1} → {actual}, 기대 {pair.Item2}";
            }

            // 역슬래시 경로에서도 같은 답이 나와야 한다. Windows 도구가 그 형태를 넘긴다.
            if (AssetImportPaths.ClassifyTexture(Root.Replace('/', '\\') + "Art\\UI\\x.png")
                != TextureAssetCategory.Ui)
                return "역슬래시 경로에서 카테고리가 갈리지 않는다";
            return null;
        }

        private static string TestTextureRulesDiffer()
        {
            TextureImportRuleSet set = TextureImportRuleSet.CodePreset;

            TextureImportRule ui = set.For(TextureAssetCategory.Ui);
            TextureImportRule world = set.For(TextureAssetCategory.World);
            TextureImportRule normal = set.For(TextureAssetCategory.NormalMap);
            TextureImportRule vfx = set.For(TextureAssetCategory.Vfx);

            // 노멀맵을 sRGB 로 읽으면 조명이 조용히 어긋난다. 이 한 줄이 이 규칙의 핵심이다.
            if (normal.SRgb) return "노멀맵 규칙이 sRGB 를 켜 두고 있다";

            // UI 는 화면에 1:1 이라 밉맵이 메모리 낭비다.
            if (ui.GenerateMipmaps) return "UI 규칙이 밉맵을 켜 두고 있다";
            if (!ui.AlphaIsTransparency) return "UI 규칙이 알파 투명을 꺼 두고 있다";

            // VFX 는 오버드로우가 먼저 아프므로 월드보다 작아야 한다.
            if (vfx.MaxSize >= world.MaxSize)
                return $"VFX 상한 {vfx.MaxSize} 이 월드 {world.MaxSize} 보다 작지 않다";

            // 크기는 2의 거듭제곱이어야 임포터가 그대로 받는다.
            foreach (TextureImportRule rule in TextureImportRuleSet.Presets())
            {
                if (rule.MaxSize < 32 || (rule.MaxSize & (rule.MaxSize - 1)) != 0)
                    return $"{rule.Category} 최대 크기 {rule.MaxSize} 가 2의 거듭제곱이 아니다";
            }

            // 카테고리 수와 프리셋 수가 어긋나면 조회가 조용히 폴백한다.
            if (TextureImportRuleSet.Presets().Length != TextureImportRuleSet.CategoryCount)
                return $"프리셋 {TextureImportRuleSet.Presets().Length} 개, 카테고리 상수 {TextureImportRuleSet.CategoryCount}";
            return null;
        }

        private static string TestAudioClassByPath()
        {
            var cases = new[]
            {
                (Root + "Audio/Voice/psg_gasp.wav",   AudioAssetClass.Voice),
                (Root + "Audio/Sfx/vo_intro.wav",     AudioAssetClass.Voice),
                (Root + "Audio/Loops/machine.wav",    AudioAssetClass.Loop),
                (Root + "Audio/Sfx/hum_loop.wav",     AudioAssetClass.Loop),
                (Root + "Audio/Ambience/room.wav",    AudioAssetClass.Loop),
                (Root + "Audio/Sfx/purify.wav",       AudioAssetClass.ShortEffect),
                (Root + "Audio/lever.wav",            AudioAssetClass.ShortEffect),
            };

            foreach (var pair in cases)
            {
                AudioAssetClass actual = AssetImportPaths.ClassifyAudio(pair.Item1);
                if (actual != pair.Item2)
                    return $"{pair.Item1} → {actual}, 기대 {pair.Item2}";
            }
            return null;
        }

        private static string TestAudioRulesDiffer()
        {
            // `UP-AUD-05` 의 미충족 절반이 「압축 구분 0건」이었다. 갈래 이름만 셋이고
            // 값이 같으면 여기가 빨간불이 돼야 한다 — 그러라고 있는 검사다.
            AudioImportRuleSet set = AudioImportRuleSet.CodePreset;

            AudioImportRule sfx = set.For(AudioAssetClass.ShortEffect);
            AudioImportRule loop = set.For(AudioAssetClass.Loop);
            AudioImportRule voice = set.For(AudioAssetClass.Voice);

            if (sfx.LoadType == loop.LoadType || loop.LoadType == voice.LoadType || sfx.LoadType == voice.LoadType)
                return $"적재 방식이 겹친다: {sfx.LoadType}/{loop.LoadType}/{voice.LoadType}";

            if (sfx.Compression == loop.Compression && loop.Compression == voice.Compression)
                return $"압축 포맷이 세 갈래 모두 {sfx.Compression} 이다";

            // 지연이 0이어야 하는 것은 사건음뿐이다.
            if (sfx.LoadType != AudioImportLoadType.DecompressOnLoad)
                return $"짧은 효과음이 {sfx.LoadType} 이다 — 첫 재생에 지연이 붙는다";
            if (voice.LoadType != AudioImportLoadType.Streaming)
                return $"음성이 {voice.LoadType} 이다 — 긴 클립을 통째로 올린다";

            if (AudioImportRuleSet.Presets().Length != AudioImportRuleSet.ClassCount)
                return $"프리셋 {AudioImportRuleSet.Presets().Length} 개, 갈래 상수 {AudioImportRuleSet.ClassCount}";
            return null;
        }

        private static string TestNoUncompressedAudio()
        {
            // PRD §13.4 「무압축 원본을 런타임에 직접 사용하지 않는다」.
            foreach (AudioImportRule rule in AudioImportRuleSet.Presets())
            {
                if (rule.Compression == AudioImportCompression.Pcm)
                    return $"{rule.Class} 기본값이 무압축(PCM)이다";
                if (rule.Quality < 0f || rule.Quality > 1f)
                    return $"{rule.Class} 품질 {rule.Quality} 가 0~1 밖이다";
            }
            return null;
        }

        private static string TestImportRuleFallbackIsNamed()
        {
            // 폴백이 실제 데이터와 리포트에서 같아 보이면 「데이터화했다」가 공허하게 통과한다.
            TextureImportRuleSet textures =
                VisualQualityProfile.ImportRulesOrDefault(null, nameof(TestImportRuleFallbackIsNamed));
            if (!textures.IsCodePreset || textures.SourceName != TextureImportRuleSet.CodePresetName)
                return $"텍스처 폴백 출처가 '{textures.SourceName}' 다";

            AudioImportRuleSet audio =
                AudioMixProfile.ImportRulesOrDefault(null, nameof(TestImportRuleFallbackIsNamed));
            if (!audio.IsCodePreset || audio.SourceName != AudioImportRuleSet.CodePresetName)
                return $"오디오 폴백 출처가 '{audio.SourceName}' 다";

            // 값이 비어 있는 에셋도 폴백이지 0 이 아니다 — 0 이면 임포터가 크기 0을 받는다.
            var quality = ScriptableObject.CreateInstance<VisualQualityProfile>();
            var mix = ScriptableObject.CreateInstance<AudioMixProfile>();
            try
            {
                // 이름을 명시한다. 이름이 비면 출처가 「코드 프리셋」으로 표기되도록 만들어
                // 두었기 때문에, 이름 없는 인스턴스로는 두 경로를 구분할 수 없다.
                quality.name = "테스트 품질";
                mix.name = "테스트 믹스";
                quality.Reset();
                mix.Reset();

                // Reset 으로 값이 채워졌으면 「코드 프리셋」이 아니라 에셋 이름이 출처다.
                if (quality.ImportRules().IsCodePreset)
                    return "Reset 된 VisualQualityProfile 이 여전히 코드 프리셋으로 찍힌다";
                if (mix.ImportRules().IsCodePreset)
                    return "Reset 된 AudioMixProfile 이 여전히 코드 프리셋으로 찍힌다";

                if (quality.ImportRules().For(TextureAssetCategory.NormalMap).SRgb)
                    return "에셋 경로에서도 노멀맵 sRGB 가 켜져 있다";
                if (mix.ImportRules().For(AudioAssetClass.Voice).LoadType != AudioImportLoadType.Streaming)
                    return "에셋 경로에서 음성이 스트리밍이 아니다";
                return null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(quality);
                UnityEngine.Object.DestroyImmediate(mix);
            }
        }

        private static string TestManagedRootGuard()
        {
            if (!AssetImportPaths.IsManaged(Root + "Art/UI/x.png"))
                return "관할 안 경로를 관할 밖으로 판정한다";
            if (AssetImportPaths.IsManaged("Assets/TextMesh Pro/Fonts/x.png"))
                return "TextMesh Pro 에셋을 관할로 판정한다 — 남의 에셋 설정을 덮어쓴다";
            if (AssetImportPaths.IsManaged("Packages/com.unity.render-pipelines.universal/x.png"))
                return "패키지 에셋을 관할로 판정한다";
            if (AssetImportPaths.IsManaged(null) || AssetImportPaths.IsManaged(string.Empty))
                return "빈 경로를 관할로 판정한다";
            return null;
        }

        // ── UP-VIS-01 / UP-PLAT-05 플레이스홀더 텍스처 ────────────────────────
        //
        // 이 여섯 검사는 `Assets/Editor/AscendTextureGen.cs` 를 **참조하지 않는다.**
        // 참조할 수 없기도 하다(생성기는 `Assembly-CSharp-Editor`, 이 파일은
        // `Assembly-CSharp` 다). 그리고 참조하지 않는 편이 옳다 — 생성기가 스스로
        // 「내 팔레트는 내 팔레트와 같다」를 증명하면 아무것도 증명하지 않은 것이다.
        // 여기 적힌 팔레트·해상도·해시는 **락 문서에서 독립적으로 다시 선언한 사양**이고,
        // 검사 대상은 디스크에 실재하는 PNG 바이트다. 생성기가 사양에서 벗어나면
        // 둘이 어긋나서 실패한다.
        //
        // PNG 를 zlib 없이 읽을 수 있는 이유는 생성기가 **필터 0 + 무압축 deflate** 로만
        // 굽기 때문이다. 그 선택의 값은 여기서 회수된다.

        private const string TextureFolder = Root + "Art/Textures/";

        /// <summary>생성기가 쓰는 알고리즘 판. PNG 안 tEXt 청크의 주장과 대조한다.</summary>
        private const string TextureAlgorithm = "AscendSynth-v1";

        // 스타일 락 경계 — `docs/VISUAL_BIBLE.md` §2.1 「탁하고 제한된 색상」·
        // 「바랜 산업용 색」, §3 팔레트 표, §4.2 금지 15번(적색은 위험 신호 전용).
        // 색상각 15° 아래를 비워 두는 것이 그 마지막 항목이다 — 표면색이 적색 대역에
        // 들어오면 위험 신호가 배경과 같은 색이 되어 신호이기를 멈춘다.
        private const float MaxSwatchSaturation = 0.50f;
        private const float MaxSwatchValue = 0.55f;
        private const float MinSwatchHue = 15f;
        private const float MaxSwatchHue = 160f;
        private const float AchromaticSaturation = 0.10f;

        private readonly struct Placeholder
        {
            public readonly string File;
            public readonly int Size;
            public readonly string Seed;
            public readonly ulong Golden;
            public readonly int[] Palette;

            public Placeholder(string file, int size, string seed, ulong golden, int[] palette)
            {
                File = file;
                Size = size;
                Seed = seed;
                Golden = golden;
                Palette = palette;
            }

            public string AssetPath { get { return TextureFolder + File; } }
        }

        private static Placeholder[] Placeholders()
        {
            return new[]
            {
                new Placeholder("TEX_Iron_Rust.png", 128, "0x5A170001", 0x24AA00DCCCB201B7UL,
                    new[] { 0x23231F, 0x33322C, 0x45443C, 0x3A3B2E, 0x4A362A, 0x5E4735 }),
                new Placeholder("TEX_Wood_Stained.png", 128, "0x5A170002", 0x0C3F4C0C23557E42UL,
                    new[] { 0x2A2018, 0x3D2E22, 0x52402F, 0x1E1710, 0x3A3524 }),
                new Placeholder("TEX_Brass_Aged.png", 64, "0x5A170003", 0xAF105817D5229761UL,
                    new[] { 0x443C28, 0x6E5F3D, 0x7F7050, 0x3E4A42, 0x2B2619 }),
                new Placeholder("TEX_Glass_Dirty.png", 64, "0x5A170004", 0x76F63864CEDA9154UL,
                    new[] { 0x262B27, 0x384039, 0x4C564E, 0x3A342A, 0x626E64 }),
            };
        }

        /// <summary>
        /// 넷이 실재하고 정상 PNG 이며, 파일 스스로가 자기 출처를 들고 있는가.
        /// tEXt 대조가 있는 이유: 파일만 있고 어디서 왔는지 모르면 다음 세션이
        /// 「이 PNG 를 다시 만들 수 있는가」에 답할 수 없다.
        /// </summary>
        private static string TestPlaceholderTexturesExist()
        {
            foreach (Placeholder p in Placeholders())
            {
                DecodedPng png;
                string failure = LoadPlaceholder(p, out png);
                if (failure != null) return failure;

                // 생성기와 같은 이유로 불변 문화권이다 — 여기가 현재 문화권을 타면
                // 파일은 멀쩡한데 로케일이 다른 기기에서만 이 검사가 실패한다.
                var culture = System.Globalization.CultureInfo.InvariantCulture;
                var expected = new StringBuilder(192);
                expected.Append("algo=").Append(TextureAlgorithm);
                expected.Append(";seed=").Append(p.Seed);
                expected.Append(";size=").Append(p.Size.ToString(culture));
                expected.Append('x').Append(p.Size.ToString(culture));
                expected.Append(";palette=");
                for (int i = 0; i < p.Palette.Length; i++)
                {
                    if (i > 0) expected.Append(',');
                    expected.Append(p.Palette[i].ToString("X6", culture));
                }

                if (png.Text == null)
                    return $"{p.File}: 출처 tEXt 청크(Ascend)가 없다";
                if (png.Text != expected.ToString())
                    return $"{p.File}: 출처 기록이 사양과 다르다\n        파일: {png.Text}\n        사양: {expected}";
            }
            return null;
        }

        /// <summary>
        /// `UP-PLAT-05` 의 실제 쟁점. 규칙은 경로로만 카테고리를 정하므로, 넷이 어느 갈래로
        /// 떨어지는지는 **파일을 어디에 두었는가**가 결정한다. 여기가 World 가 아니면
        /// 벽·기계 표면에 UI 나 VFX 규칙이 걸린다.
        /// </summary>
        private static string TestPlaceholderClassifiesAsWorld()
        {
            foreach (Placeholder p in Placeholders())
            {
                if (!AssetImportPaths.IsManaged(p.AssetPath))
                    return $"{p.AssetPath} 가 관할 루트({AssetImportPaths.ManagedRoot}) 밖이다 — 규칙이 걸리지 않는다";

                TextureAssetCategory category = AssetImportPaths.ClassifyTexture(p.AssetPath);
                if (category != TextureAssetCategory.World)
                    return $"{p.File} → {category}, 기대 World. 폴더나 파일명 접미사가 갈래를 바꿨다";
            }

            // World 규칙이 이 넷을 실제로 통과시키는가. 상한이 크기보다 작으면 임포터가
            // 조용히 축소하고, 그러면 「저해상도」가 의도가 아니라 사고가 된다.
            TextureImportRule rule = TextureImportRuleSet.Preset(TextureAssetCategory.World);
            foreach (Placeholder p in Placeholders())
                if (rule.MaxSize < p.Size)
                    return $"World 상한 {rule.MaxSize}px 이 {p.File} 의 {p.Size}px 보다 작다";
            if (!rule.SRgb) return "World 규칙이 sRGB 를 꺼 두고 있다 — 이 넷은 색이지 벡터가 아니다";
            return null;
        }

        /// <summary>
        /// 「저해상도 손그림 픽셀 텍스처」(§2.1). 상한만 있으면 언젠가 2048 짜리가 섞이고,
        /// 하한만 있으면 판독이 사라진다(§4.2 금지 13번 「과도한 저해상도화」).
        /// </summary>
        private static string TestPlaceholderResolution()
        {
            foreach (Placeholder p in Placeholders())
            {
                DecodedPng png;
                string failure = LoadPlaceholder(p, out png);
                if (failure != null) return failure;

                if (png.Width != p.Size || png.Height != p.Size)
                    return $"{p.File}: {png.Width}×{png.Height}, 사양 {p.Size}×{p.Size}";
                if (p.Size < 64 || p.Size > 128)
                    return $"{p.File}: {p.Size}px 가 64~128 밖이다 (PS1~초기 PS2 감각)";
                if ((p.Size & (p.Size - 1)) != 0)
                    return $"{p.File}: {p.Size}px 가 2의 거듭제곱이 아니다 — 압축 포맷이 갈린다";
            }
            return null;
        }

        /// <summary>
        /// 팔레트가 락의 색 범위 안인가. 「탁하고 제한된 색상」은 취향이 아니라 측정 가능한
        /// 경계다 — 채도·명도 상한과 색상각 대역으로 적으면 회귀를 잡을 수 있다.
        /// </summary>
        private static string TestPlaceholderPaletteWithinStyleLock()
        {
            foreach (Placeholder p in Placeholders())
            {
                if (p.Palette.Length < 4 || p.Palette.Length > 8)
                    return $"{p.File}: 팔레트 {p.Palette.Length}색 — 4~8색이어야 「제한된 색상」이다";

                for (int i = 0; i < p.Palette.Length; i++)
                {
                    for (int j = i + 1; j < p.Palette.Length; j++)
                        if (p.Palette[i] == p.Palette[j])
                            return $"{p.File}: {p.Palette[i]:X6} 이 팔레트에 두 번 있다";

                    float hue, saturation, value;
                    ToHsv(p.Palette[i], out hue, out saturation, out value);

                    if (value > MaxSwatchValue)
                        return $"{p.File} {p.Palette[i]:X6}: 명도 {value:0.000} > {MaxSwatchValue} — 「바랜 산업용 색」이 아니다";
                    if (saturation > MaxSwatchSaturation)
                        return $"{p.File} {p.Palette[i]:X6}: 채도 {saturation:0.000} > {MaxSwatchSaturation} — 「탁한 색」이 아니다";
                    if (saturation > AchromaticSaturation && (hue < MinSwatchHue || hue > MaxSwatchHue))
                        return $"{p.File} {p.Palette[i]:X6}: 색상각 {hue:0.0}° 가 {MinSwatchHue}~{MaxSwatchHue}° 밖이다 " +
                               "— 적색은 위험 신호 전용이고 청·자 대역은 락에 없다";
                }
            }
            return null;
        }

        /// <summary>
        /// **양방향** 대조다. ① 선언에 없는 색이 화면에 있으면 중간색이 생겼다는 뜻이고
        /// 그러면 「손그림 저해상도」가 아니라 그라디언트다. ② 선언에 있는데 화면에 없으면
        /// 팔레트가 실제보다 풍부해 보이는 장부일 뿐이다. 둘 다 조용히 통과하는 종류의 거짓이라
        /// 양쪽을 다 막는다.
        /// </summary>
        private static string TestPlaceholderPixelsMatchPalette()
        {
            foreach (Placeholder p in Placeholders())
            {
                DecodedPng png;
                string failure = LoadPlaceholder(p, out png);
                if (failure != null) return failure;

                var seen = new bool[p.Palette.Length];
                int pixels = png.Width * png.Height;
                for (int i = 0; i < pixels; i++)
                {
                    int rgb = (png.Rgb[i * 3] << 16) | (png.Rgb[i * 3 + 1] << 8) | png.Rgb[i * 3 + 2];
                    int found = -1;
                    for (int k = 0; k < p.Palette.Length; k++)
                        if (p.Palette[k] == rgb) { found = k; break; }
                    if (found < 0)
                        return $"{p.File} ({i % png.Width},{i / png.Width}): {rgb:X6} 이 팔레트에 없다";
                    seen[found] = true;
                }

                for (int k = 0; k < seen.Length; k++)
                    if (!seen[k])
                        return $"{p.File}: {p.Palette[k]:X6} 을 선언했는데 한 픽셀도 쓰지 않는다";
            }
            return null;
        }

        /// <summary>
        /// 「같은 시드가 같은 PNG 를 낸다」의 고정핀. 생성기는 정수 고정소수점 노이즈와
        /// 무압축 deflate 만 쓰므로 런타임·기기가 달라도 바이트가 같아야 한다.
        /// 그 주장이 틀리면 여기서 깨진다 — 캡처 베이스라인이 흔들리기 **전에** 깨진다.
        ///
        /// 이 검사는 재생성을 하지 않는다(할 수 없다 — 생성기가 에디터 어셈블리다).
        /// 재생성 쪽 대조는 `Ascend/Generate Placeholder Textures` 가 맡는다. 그 메뉴는
        /// 기존 파일과 새로 만든 바이트가 다르면 「결정론 위반」을 찍는다.
        /// </summary>
        private static string TestPlaceholderGoldenHash()
        {
            foreach (Placeholder p in Placeholders())
            {
                string path = AbsoluteAssetPath(p.AssetPath);
                if (!File.Exists(path)) return $"{p.AssetPath} 가 없다";

                byte[] bytes = File.ReadAllBytes(path);
                ulong hash = 14695981039346656037UL;
                for (int i = 0; i < bytes.Length; i++)
                {
                    hash ^= bytes[i];
                    hash *= 1099511628211UL;
                }

                if (hash != p.Golden)
                    return $"{p.File}: FNV-1a64 0x{hash:X16}, 고정핀 0x{p.Golden:X16} — " +
                           "생성기가 바뀌었거나 파일이 손으로 수정됐다";
            }
            return null;
        }

        // ── 위 검사들이 쓰는 도구 ─────────────────────────────────────────────

        private sealed class DecodedPng
        {
            public int Width;
            public int Height;
            public byte[] Rgb;   // width*height*3, 필터 해제 완료
            public string Text;  // "Ascend" tEXt 청크의 값. 없으면 null
        }

        private static string AbsoluteAssetPath(string assetPath)
        {
            DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
            string relative = assetPath.Replace('/', Path.DirectorySeparatorChar);
            return projectRoot == null ? relative : Path.Combine(projectRoot.FullName, relative);
        }

        private static string LoadPlaceholder(Placeholder placeholder, out DecodedPng png)
        {
            png = null;
            string path = AbsoluteAssetPath(placeholder.AssetPath);
            if (!File.Exists(path))
                return $"{placeholder.AssetPath} 가 없다 — `Ascend/Generate Placeholder Textures` 로 만든다";

            var decoded = new DecodedPng();
            string failure = DecodePng(File.ReadAllBytes(path), decoded);
            if (failure != null) return $"{placeholder.File}: {failure}";
            png = decoded;
            return null;
        }

        /// <summary>
        /// 최소 PNG 리더. 청크 CRC 와 zlib adler32 까지 확인하므로 「파일이 멀쩡한가」도
        /// 같이 답한다. 무압축 deflate 만 받는다 — 압축된 PNG 를 만나면 실패하고,
        /// 그 실패는 「누가 생성기를 안 거치고 파일을 갈아끼웠다」는 뜻이다.
        /// </summary>
        private static string DecodePng(byte[] file, DecodedPng result)
        {
            byte[] signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            if (file.Length < 8) return "8바이트보다 짧다";
            for (int i = 0; i < signature.Length; i++)
                if (file[i] != signature[i]) return "PNG 서명이 아니다";

            var idat = new MemoryStream();
            bool sawHeader = false, sawEnd = false;
            int at = 8;

            while (at + 12 <= file.Length && !sawEnd)
            {
                long length = ReadBigEndian(file, at);
                if (length < 0 || at + 12 + length > file.Length)
                    return $"청크 길이 {length} 가 파일 밖을 가리킨다";

                string type = Latin1(file, at + 4, 4);
                int data = at + 8;
                int count = (int)length;

                uint storedCrc = (uint)ReadBigEndian(file, data + count);
                uint actualCrc = Crc32(file, at + 4, count + 4);
                if (storedCrc != actualCrc) return $"{type} 청크 CRC 불일치";

                switch (type)
                {
                    case "IHDR":
                        if (count != 13) return "IHDR 길이가 13이 아니다";
                        result.Width = (int)ReadBigEndian(file, data);
                        result.Height = (int)ReadBigEndian(file, data + 4);
                        if (file[data + 8] != 8) return $"비트 깊이 {file[data + 8]}, 기대 8";
                        if (file[data + 9] != 2) return $"색상 타입 {file[data + 9]}, 기대 2(RGB)";
                        if (file[data + 10] != 0) return "압축 방식이 deflate 가 아니다";
                        if (file[data + 11] != 0) return "필터 방식이 0 이 아니다";
                        if (file[data + 12] != 0) return "인터레이스가 켜져 있다";
                        sawHeader = true;
                        break;

                    case "tEXt":
                    {
                        int split = -1;
                        for (int i = 0; i < count; i++)
                            if (file[data + i] == 0) { split = i; break; }
                        if (split > 0 && Latin1(file, data, split) == "Ascend")
                            result.Text = Latin1(file, data + split + 1, count - split - 1);
                        break;
                    }

                    case "IDAT":
                        idat.Write(file, data, count);
                        break;

                    case "IEND":
                        sawEnd = true;
                        break;
                }

                at = data + count + 4;
            }

            if (!sawHeader) return "IHDR 이 없다";
            if (!sawEnd) return "IEND 가 없다";
            if (result.Width <= 0 || result.Height <= 0) return "크기가 0 이하다";

            byte[] raw;
            string failure = InflateStored(idat.ToArray(), out raw);
            if (failure != null) return failure;

            int stride = 1 + result.Width * 3;
            if (raw.Length != stride * result.Height)
                return $"압축 해제 결과 {raw.Length}바이트, 기대 {stride * result.Height}";

            result.Rgb = new byte[result.Width * result.Height * 3];
            for (int y = 0; y < result.Height; y++)
            {
                if (raw[y * stride] != 0) return $"{y}번 줄의 필터 타입이 {raw[y * stride]} 다 — 기대 0";
                Buffer.BlockCopy(raw, y * stride + 1, result.Rgb, y * result.Width * 3, result.Width * 3);
            }
            return null;
        }

        private static string InflateStored(byte[] stream, out byte[] output)
        {
            output = null;
            if (stream.Length < 6) return "zlib 스트림이 너무 짧다";
            if ((stream[0] & 0x0F) != 8) return "zlib 압축 방식이 deflate 가 아니다";
            if ((((stream[0] << 8) | stream[1]) % 31) != 0) return "zlib 헤더 검사값이 틀렸다";

            var buffer = new MemoryStream(stream.Length);
            int at = 2;
            while (true)
            {
                if (at + 5 > stream.Length) return "deflate 블록 헤더가 잘렸다";
                int header = stream[at];
                int type = (header >> 1) & 3;
                if (type != 0) return $"deflate 블록 종류가 {type} 다 — 생성기는 무압축(0)만 쓴다";

                int length = stream[at + 1] | (stream[at + 2] << 8);
                int inverse = stream[at + 3] | (stream[at + 4] << 8);
                if ((length ^ 0xFFFF) != inverse) return "deflate LEN/NLEN 이 서로 보수가 아니다";

                at += 5;
                if (at + length > stream.Length) return "deflate 블록이 스트림 밖을 가리킨다";
                buffer.Write(stream, at, length);
                at += length;
                if ((header & 1) != 0) break;
            }

            output = buffer.ToArray();
            if (at + 4 > stream.Length) return "adler32 가 없다";
            if ((uint)ReadBigEndian(stream, at) != Adler32(output))
                return "adler32 불일치 — 압축 해제 결과가 원본과 다르다";
            return null;
        }

        private static long ReadBigEndian(byte[] data, int at)
        {
            return ((long)data[at] << 24) | ((long)data[at + 1] << 16)
                 | ((long)data[at + 2] << 8) | data[at + 3];
        }

        private static string Latin1(byte[] data, int at, int count)
        {
            var text = new StringBuilder(count);
            for (int i = 0; i < count; i++) text.Append((char)data[at + i]);
            return text.ToString();
        }

        private static uint Adler32(byte[] data)
        {
            uint a = 1, b = 0;
            for (int i = 0; i < data.Length; i++)
            {
                a = (a + data[i]) % 65521u;
                b = (b + a) % 65521u;
            }
            return (b << 16) | a;
        }

        private static uint[] _crcTable;

        private static uint Crc32(byte[] data, int at, int count)
        {
            if (_crcTable == null)
            {
                _crcTable = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    uint c = n;
                    for (int k = 0; k < 8; k++)
                        c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    _crcTable[n] = c;
                }
            }

            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < count; i++)
                crc = _crcTable[(crc ^ data[at + i]) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }

        /// <summary>0xRRGGBB → HSV. 색상각은 도(0~360), 채도·명도는 0~1.</summary>
        private static void ToHsv(int rgb, out float hue, out float saturation, out float value)
        {
            int r = (rgb >> 16) & 0xFF, g = (rgb >> 8) & 0xFF, b = rgb & 0xFF;
            int max = Math.Max(r, Math.Max(g, b));
            int min = Math.Min(r, Math.Min(g, b));
            int delta = max - min;

            value = max / 255f;
            saturation = max == 0 ? 0f : delta / (float)max;

            if (delta == 0) { hue = 0f; return; }
            if (max == r) hue = 60f * ((((g - b) / (float)delta) % 6f + 6f) % 6f);
            else if (max == g) hue = 60f * (((b - r) / (float)delta) + 2f);
            else hue = 60f * (((r - g) / (float)delta) + 4f);
        }

        // ── UP-TECH-09 ⑩⑪⑫ ───────────────────────────────────────────────────

        private static string TestParticleCapsMatchDirector()
        {
            // 프로파일은 아직 **사본**이다. 원본인 `AmbientParticleDirector` 만 고치고
            // 사본을 두면 다음 세션이 서로 다른 두 숫자를 보게 된다. 그것을 막는 유일한 장치.
            PresentationSnapshot snapshot = PresentationProfile.DefaultSnapshot;
            var levels = new[] { RiskLevel.Stable, RiskLevel.Strain, RiskLevel.Critical, RiskLevel.Collapse };

            foreach (RiskLevel level in levels)
            {
                int fromProfile = snapshot.MaxParticlesFor(level);
                int fromCode = Ascend.Prototype.Effects.AmbientParticleDirector.MaxParticlesFor(level);
                if (fromProfile != fromCode)
                    return $"{level} 상한 프로파일 {fromProfile} vs 코드 {fromCode} — 한쪽만 고쳤다";
            }

            // 단계가 올라갈수록 밀도가 줄면 위험이 가벼워 보인다.
            for (int i = 1; i < levels.Length; i++)
                if (snapshot.MaxParticlesFor(levels[i]) < snapshot.MaxParticlesFor(levels[i - 1]))
                    return $"{levels[i]} 상한이 {levels[i - 1]} 보다 작다";

            // 범위 밖 단계로 배열 밖을 읽지 않는다.
            if (snapshot.MaxParticlesFor((RiskLevel)99) != snapshot.MaxParticlesFor(RiskLevel.Collapse))
                return "범위 밖 단계가 마지막 단계로 조여지지 않는다";
            return null;
        }

        private static string TestPresentationHoldsAreNonZero()
        {
            // `SpinPresenter` 주석이 못박은 것: "이걸 0으로 두면 '무엇이 사라졌는지'가 사라진다".
            PresentationSnapshot snapshot = PresentationProfile.DefaultSnapshot;

            if (snapshot.ReadPause <= 0f) return "판을 읽을 정지가 0이다";
            if (snapshot.EmptyHold <= 0f) return "빈칸 정지가 0이다";
            if (snapshot.RefillHold <= 0f) return "재충전 정지가 0이다";
            if (snapshot.ColumnRevealInterval <= 0f) return "열 공개 간격이 0이다";
            if (snapshot.UnlockFlashSeconds <= 0f || snapshot.UnlockSettleSeconds <= 0f)
                return "잠금 해제 연출 길이가 0이다";

            // 값이 `SpinPresenter` 의 인스펙터 기본값과 같아야 배선해도 동작이 안 바뀐다.
            if (!Near(snapshot.ColumnRevealInterval, 0.32f)) return $"열 공개 간격 {snapshot.ColumnRevealInterval}, 기대 0.32";
            if (!Near(snapshot.ReadPause, 0.45f)) return $"읽기 정지 {snapshot.ReadPause}, 기대 0.45";
            if (!Near(snapshot.EmptyHold, 0.30f)) return $"빈칸 정지 {snapshot.EmptyHold}, 기대 0.30";
            if (!Near(snapshot.RefillHold, 0.40f)) return $"재충전 정지 {snapshot.RefillHold}, 기대 0.40";

            // ⑫ 재질. 오염은 0 이 현재 화면이고, 거칠기는 매끄러움의 여집합이어야 한다.
            if (!Near(snapshot.GrimeAmount, 0f)) return $"오염 기본값 {snapshot.GrimeAmount} — 현재 화면은 0이다";
            if (!Near(snapshot.SurfaceRoughness, 1f - snapshot.SurfaceSmoothness))
                return "거칠기가 매끄러움의 여집합이 아니다";
            return null;
        }

        private static string TestPresentationTempoFloor()
        {
            PresentationSnapshot snapshot = PresentationProfile.DefaultSnapshot;

            if (!Near(snapshot.TempoScaleAtDepth(1), 1f)) return "1연쇄에서 이미 압축이 걸린다";
            if (snapshot.TempoScaleAtDepth(2) >= 1f) return "2연쇄부터 압축이 걸리지 않는다";

            // 20연쇄가 순식간에 지나가면 원인이 읽히지 않는다(TECH_SPEC §9).
            float deep = snapshot.TempoScaleAtDepth(20);
            if (deep < snapshot.MinTempoScale - 0.0001f)
                return $"20연쇄 배율 {deep} 가 하한 {snapshot.MinTempoScale} 아래다";
            if (snapshot.TempoScaleAtDepth(200) < snapshot.MinTempoScale - 0.0001f)
                return "깊이가 커지면 하한이 무너진다";
            return null;
        }

        // ── 공통 규칙 ─────────────────────────────────────────────────────────

        private static string TestResetMatchesDefaults()
        {
            var hardware = ScriptableObject.CreateInstance<TargetHardwareProfile>();
            var overharvest = ScriptableObject.CreateInstance<OverharvestProfile>();
            var quality = ScriptableObject.CreateInstance<VisualQualityProfile>();
            var audio = ScriptableObject.CreateInstance<AudioMixProfile>();
            var access = ScriptableObject.CreateInstance<AccessibilityProfile>();
            var summary = ScriptableObject.CreateInstance<RunSummaryTemplate>();
            var presentation = ScriptableObject.CreateInstance<PresentationProfile>();
            try
            {
                hardware.Reset();
                overharvest.Reset();
                quality.Reset();
                audio.Reset();
                access.Reset();
                summary.Reset();
                presentation.Reset();

                TargetHardwareSnapshot h = hardware.Snapshot();
                if (!Near(h.TargetFps, TargetHardwareProfile.DefaultSnapshot.TargetFps)
                    || h.Ratified != TargetHardwareProfile.DefaultSnapshot.Ratified)
                    return "TargetHardwareProfile.Reset 이 기본 스냅샷과 다르다";

                OverharvestSnapshot o = overharvest.Snapshot();
                OverharvestSnapshot od = OverharvestProfile.DefaultSnapshot;
                if (!Near(o.AnteRatio, od.AnteRatio) || !Near(o.MinSilenceSeconds, od.MinSilenceSeconds)
                    || !Near(o.MaxSilenceSeconds, od.MaxSilenceSeconds) || o.MaxExtraSpins != od.MaxExtraSpins)
                    return "OverharvestProfile.Reset 이 기본 스냅샷과 다르다";

                if (quality.Snapshot().Tier != VisualQualityProfile.DefaultSnapshot.Tier)
                    return "VisualQualityProfile.Reset 이 High 로 돌아가지 않는다";

                if (!Near(audio.Snapshot().MachineVolume, AudioMixProfile.DefaultSnapshot.MachineVolume)
                    || !Near(audio.Snapshot().MachineDuck, AudioMixProfile.DefaultSnapshot.MachineDuck))
                    return "AudioMixProfile.Reset 이 기본 믹스와 다르다";

                AccessibilitySnapshot a = access.Snapshot();
                if (!Near(a.CameraShakeScale, 1f) || !a.AllowFlicker || !a.AllowSiren || !a.ShowSubtitles)
                    return "AccessibilityProfile.Reset 이 「전부 켬」으로 돌아가지 않는다";

                RunSummaryData data = SampleSummary();
                if (summary.ComposeLines(in data).Length != RunSummaryTemplate.RequiredFieldCount)
                    return "RunSummaryTemplate.Reset 이후 9줄이 나오지 않는다";

                // 프로파일 헬퍼와 스냅샷 헬퍼가 같은 답을 내야 한다.
                if (!Near(overharvest.ClampedSilenceSeconds(9f), od.ClampedSilenceSeconds(9f)))
                    return "프로파일과 스냅샷의 ClampedSilenceSeconds 가 다르다";
                if (!Near(access.ScaleShake(0.01f), AccessibilityProfile.DefaultSnapshot.ScaleShake(0.01f)))
                    return "프로파일과 스냅샷의 ScaleShake 가 다르다";

                PresentationSnapshot p = presentation.Snapshot();
                PresentationSnapshot pd = PresentationProfile.DefaultSnapshot;
                if (p.MaxParticlesFor(RiskLevel.Collapse) != pd.MaxParticlesFor(RiskLevel.Collapse)
                    || !Near(p.ReadPause, pd.ReadPause) || !Near(p.ChainSpeedup, pd.ChainSpeedup)
                    || !Near(p.SurfaceSmoothness, pd.SurfaceSmoothness) || !Near(p.GrimeAmount, pd.GrimeAmount))
                    return "PresentationProfile.Reset 이 기본 스냅샷과 다르다";

                // 임포트 규칙도 Reset 으로 채워져야 한다 — 안 채워지면 에셋을 만들어도
                // 계속 코드 프리셋으로 폴백하고, 그러면 「데이터화」가 이름뿐이다.
                // 값 대조로는 못 잡는다(폴백 값과 프리셋 값이 같다). 출처로 잡는다.
                quality.name = "품질";
                audio.name = "믹스";
                if (quality.ImportRules().IsCodePreset)
                    return "VisualQualityProfile.Reset 이 텍스처 규칙 배열을 채우지 않는다";
                if (audio.ImportRules().IsCodePreset)
                    return "AudioMixProfile.Reset 이 오디오 규칙 배열을 채우지 않는다";
                if (quality.ImportRules().For(TextureAssetCategory.Ui).MaxSize
                    != TextureImportRuleSet.Preset(TextureAssetCategory.Ui).MaxSize)
                    return "Reset 이 채운 텍스처 규칙이 코드 프리셋과 다르다";
                if (audio.ImportRules().For(AudioAssetClass.Loop).LoadType
                    != AudioImportRuleSet.Preset(AudioAssetClass.Loop).LoadType)
                    return "Reset 이 채운 오디오 규칙이 코드 프리셋과 다르다";
                return null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hardware);
                UnityEngine.Object.DestroyImmediate(overharvest);
                UnityEngine.Object.DestroyImmediate(quality);
                UnityEngine.Object.DestroyImmediate(audio);
                UnityEngine.Object.DestroyImmediate(access);
                UnityEngine.Object.DestroyImmediate(summary);
                UnityEngine.Object.DestroyImmediate(presentation);
            }
        }

        // ── 과적 3종 (`UP-TECH-09` ⑤) ────────────────────────────────────────
        //
        // 이 여섯 건이 무엇을 막는가: 이 항목의 실패 양상은 「값을 데이터로 옮겼는데
        // 게임이 옛 상수를 계속 읽는다」이다. 값 비교로는 안 잡힌다 — 프로파일의
        // 기본값이 상수와 같은 수이기 때문이다(달라야 한다면 에셋을 만드는 순간
        // 밸런스가 조용히 바뀐다). 그래서 **상수와 다른 수**를 넣고 런타임이
        // 따라오는지 본다. 누군가 `_weight` 를 지우고 상수로 되돌리면 세 건이 깨진다.

        /// <summary>상수와 프리셋이 갈라지면 에셋 유무로 다른 게임이 된다.</summary>
        private static string TestWeightPresetMatchesFloorSession()
        {
            if (!Near(WeightProfile.DefaultAllowedWeight, Ascend.Prototype.Run.FloorSession.AllowedWeight))
                return $"허용 중량 {WeightProfile.DefaultAllowedWeight} vs FloorSession {Ascend.Prototype.Run.FloorSession.AllowedWeight}";
            if (!Near(WeightProfile.DefaultWeightPowerFactor, Ascend.Prototype.Run.FloorSession.WeightPowerFactor))
                return $"무게당 요구 전력 {WeightProfile.DefaultWeightPowerFactor} vs FloorSession {Ascend.Prototype.Run.FloorSession.WeightPowerFactor}";
            if (!Near(WeightProfile.DefaultOverloadRequiredPowerMultiplier,
                      Ascend.Prototype.Run.FloorSession.OverloadRequiredPowerMultiplier))
                return $"과적 배수 {WeightProfile.DefaultOverloadRequiredPowerMultiplier} vs FloorSession {Ascend.Prototype.Run.FloorSession.OverloadRequiredPowerMultiplier}";
            return null;
        }

        /// <summary>코드 프리셋과 **다른** 수치 3종. 셋 다 프리셋과 달라야 의미가 있다.</summary>
        private static WeightSnapshot ProbeWeights()
        {
            return new WeightSnapshot(37f, 5f, 3f, "테스트 프로브");
        }

        private static Ascend.Prototype.Run.RunSession ProbeRun(float startingWeight)
        {
            return new Ascend.Prototype.Run.RunSession(
                1337, startingWeight, 0f, OverharvestProfile.DefaultSnapshot, ProbeWeights(), null);
        }

        private static string TestWeightCapacityFollowsProfile()
        {
            var run = ProbeRun(0f);
            if (!Near(run.WeightCapacity, 37f))
                return $"허용 중량이 {run.WeightCapacity} — 프로파일의 37 이 아니다 (상수 100 으로 되돌아갔나)";
            if (run.Current == null) return "첫 층이 없다";
            if (!Near(run.Current.Capacity, 37f))
                return $"층의 허용 중량이 {run.Current.Capacity} — 프로파일의 37 이 아니다";
            if (run.Current.Weight.SourceName != "테스트 프로브")
                return $"층이 든 출처가 '{run.Current.Weight.SourceName}' — 넘긴 스냅샷이 도달하지 않았다";
            return null;
        }

        private static string TestRequiredPowerFollowsProfile()
        {
            // 과적이 아닌 무게. 10 < 37 이므로 배수는 걸리지 않는다.
            var run = ProbeRun(10f);
            if (run.Current == null) return "첫 층이 없다";
            if (run.Current.IsOverloaded) return "10kg 인데 과적으로 판정됐다 (허용 37)";

            float expected = run.Current.Plan.RequiredPower + 10f * 5f;
            if (!Near(run.Current.RequiredPower, expected))
                return $"요구 전력 {run.Current.RequiredPower}, 기대 {expected}"
                     + $" (기본 {run.Current.Plan.RequiredPower} + 10×5). 무게 계수가 상수 2 로 되돌아갔나";
            return null;
        }

        private static string TestOverloadMultiplierAppliesOnlyWhenOver()
        {
            // 50 > 37 이므로 과적이고 배수 3 이 걸린다.
            var over = ProbeRun(50f);
            if (over.Current == null) return "첫 층이 없다";
            if (!over.Current.IsOverloaded)
                return $"50kg / 허용 {over.Current.Capacity} 인데 과적이 아니다";

            float expectedOver = (over.Current.Plan.RequiredPower + 50f * 5f) * 3f;
            if (!Near(over.Current.RequiredPower, expectedOver))
                return $"과적 요구 전력 {over.Current.RequiredPower}, 기대 {expectedOver}"
                     + " — 과적 배수가 상수 1.5 로 되돌아갔나";

            // 경계 바로 아래에서는 배수가 걸리면 안 된다. 「>」를 「>=」로 바꾸는 실수를 잡는다.
            var under = ProbeRun(37f);
            if (under.Current.IsOverloaded)
                return "정확히 허용 중량인데 과적으로 판정됐다 — 경계가 > 가 아니라 >= 다";
            float expectedUnder = under.Current.Plan.RequiredPower + 37f * 5f;
            if (!Near(under.Current.RequiredPower, expectedUnder))
                return $"경계 요구 전력 {under.Current.RequiredPower}, 기대 {expectedUnder}";
            return null;
        }

        private static string TestWeightFallbackIsNamed()
        {
            WeightSnapshot fallback = WeightProfile.SnapshotOrDefault(null, "테스트");
            if (fallback.SourceName != WeightSnapshot.CodePresetName)
                return $"폴백 출처가 '{fallback.SourceName}' — 「{WeightSnapshot.CodePresetName}」이어야 한다";
            if (fallback.FromAsset)
                return "폴백인데 FromAsset 이 참이다";

            var profile = ScriptableObject.CreateInstance<WeightProfile>();
            try
            {
                profile.name = "WeightProbe";
                WeightSnapshot fromAsset = WeightProfile.SnapshotOrDefault(profile, "테스트");
                if (fromAsset.SourceName != "WeightProbe")
                    return $"에셋 출처가 '{fromAsset.SourceName}' — 에셋 이름이어야 한다";
                if (!fromAsset.FromAsset)
                    return "에셋에서 왔는데 FromAsset 이 거짓이다";
                // 에셋 기본값은 프리셋과 같은 수여야 한다 — 그래야 에셋을 만드는 것만으로
                // 밸런스가 바뀌지 않는다.
                if (!Near(fromAsset.AllowedWeight, WeightProfile.DefaultAllowedWeight))
                    return $"새 에셋의 허용 중량 {fromAsset.AllowedWeight} 이 프리셋과 다르다";
                return null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        private static string TestWeightValidate()
        {
            if (WeightProfile.DefaultSnapshot.Validate() != null)
                return $"기본값이 자기모순으로 판정됐다: {WeightProfile.DefaultSnapshot.Validate()}";

            // 과적에 벌칙이 없어지는 조합 — 배수 1.0.
            if (new WeightSnapshot(100f, 2f, 1f, "x").Validate() == null)
                return "과적 배수 1.0 을 통과시켰다 — 과적 경고·사고가 의미를 잃는 값이다";
            // 첫 적재부터 항상 과적인 조합.
            if (new WeightSnapshot(0f, 2f, 1.5f, "x").Validate() == null)
                return "허용 중량 0 을 통과시켰다";
            // 무엇을 실어도 요구 전력이 그대로인 조합.
            if (new WeightSnapshot(100f, 0f, 1.5f, "x").Validate() == null)
                return "무게 계수 0 을 통과시켰다 — 적재 선택이 무의미해지는 값이다";
            return null;
        }

        // ── 스핀 밸런스 ①③ (`UP-TECH-09`) ──────────────────────────────────
        //
        // ⑤ 와 같은 함정이 여기에도 있다: 프리셋과 `SpinRuleSet` 필드 초기값이 같은 수라
        // 값만 비교하면 배선을 떼어내도 통과한다. 그래서 프리셋과 **다른** 수를 넣고
        // 규칙 다발이 따라오는지 본다.

        /// <summary>코드 프리셋과 다른 수치 10종. 자기모순 검사를 통과하는 값이어야 한다.</summary>
        private static SpinBalanceSnapshot ProbeBalance()
        {
            // 직선 4 < 연결 6 < 잭팟 9 — 단조를 지키면서 프리셋(2/3/10)과 전부 다르다.
            return new SpinBalanceSnapshot(11f, 13f, 17f, 4f, 6f, 9f, 2, 0.25f, 3f, 0.9f, "테스트 프로브");
        }

        private static string TestSpinPresetMatchesRuleSet()
        {
            // 인자 없는 `CreateDefault()` 는 프리셋으로 위임한다. 그 결과가 옛 리터럴과
            // 같아야 한다 — 다르면 이 변경이 밸런스를 건드린 것이다.
            Ascend.Prototype.Spin.SpinRuleSet rules = Ascend.Prototype.Spin.SpinRuleSet.CreateDefault();
            if (!Near(rules.WeightOf(Ascend.Prototype.Spin.SymbolKind.NormalSoul), 5f))
                return $"정상 영혼 가중치 {rules.WeightOf(Ascend.Prototype.Spin.SymbolKind.NormalSoul)}, 옛 값 5";
            if (!Near(rules.WeightOf(Ascend.Prototype.Spin.SymbolKind.Absorber), 2.5f))
                return $"흡수체 가중치 {rules.WeightOf(Ascend.Prototype.Spin.SymbolKind.Absorber)}, 옛 값 2.5";
            if (!Near(rules.WeightOf(Ascend.Prototype.Spin.SymbolKind.Proliferator), 2.5f))
                return $"증식체 가중치 {rules.WeightOf(Ascend.Prototype.Spin.SymbolKind.Proliferator)}, 옛 값 2.5";
            if (!Near(rules.LineMultiplier, 2f) || !Near(rules.ClusterMultiplier, 3f)
                || !Near(rules.FullBoardMultiplier, 10f))
                return $"패턴 배수 {rules.LineMultiplier}/{rules.ClusterMultiplier}/{rules.FullBoardMultiplier}, 옛 값 2/3/10";
            if (!Near(rules.CascadeMultiplierStep, 0.5f))
                return $"연쇄 증분 {rules.CascadeMultiplierStep}, 옛 값 0.5";
            if (!Near(rules.AbsorberResidualPowerLoss, 8f))
                return $"흡수체 잔류 손실 {rules.AbsorberResidualPowerLoss}, 옛 값 8";
            if (!Near(rules.ProliferatorResidualWeightAdd, 0.6f))
                return $"증식체 잔류 가산 {rules.ProliferatorResidualWeightAdd}, 옛 값 0.6";
            if (rules.MinimumCountFor(Ascend.Prototype.Spin.SymbolKind.Absorber) != 3)
                return $"정화 최소 개수 {rules.MinimumCountFor(Ascend.Prototype.Spin.SymbolKind.Absorber)}, 옛 값 3";
            return null;
        }

        private static string TestSymbolWeightsFollowProfile()
        {
            var rules = Ascend.Prototype.Spin.SpinRuleSet.CreateDefault(ProbeBalance());
            if (!Near(rules.WeightOf(Ascend.Prototype.Spin.SymbolKind.NormalSoul), 11f))
                return $"정상 영혼 {rules.WeightOf(Ascend.Prototype.Spin.SymbolKind.NormalSoul)} — 프로파일의 11 이 아니다 (리터럴 5 로 되돌아갔나)";
            if (!Near(rules.WeightOf(Ascend.Prototype.Spin.SymbolKind.Absorber), 13f))
                return $"흡수체 {rules.WeightOf(Ascend.Prototype.Spin.SymbolKind.Absorber)} — 프로파일의 13 이 아니다";
            if (!Near(rules.WeightOf(Ascend.Prototype.Spin.SymbolKind.Proliferator), 17f))
                return $"증식체 {rules.WeightOf(Ascend.Prototype.Spin.SymbolKind.Proliferator)} — 프로파일의 17 이 아니다";
            if (rules.MinimumCountFor(Ascend.Prototype.Spin.SymbolKind.Absorber) != 2)
                return $"정화 최소 개수 {rules.MinimumCountFor(Ascend.Prototype.Spin.SymbolKind.Absorber)} — 프로파일의 2 가 아니다";
            if (!Near(rules.AbsorberResidualPowerLoss, 3f))
                return $"흡수체 잔류 손실 {rules.AbsorberResidualPowerLoss} — 프로파일의 3 이 아니다";
            if (!Near(rules.ProliferatorResidualWeightAdd, 0.9f))
                return $"증식체 잔류 가산 {rules.ProliferatorResidualWeightAdd} — 프로파일의 0.9 가 아니다";
            return null;
        }

        private static string TestPatternMultipliersFollowProfile()
        {
            var rules = Ascend.Prototype.Spin.SpinRuleSet.CreateDefault(ProbeBalance());
            var kind = Ascend.Prototype.Spin.SymbolKind.Absorber;

            // 필드 초기값으로만 두면 이 셋이 코드 값(2/3/10)에 남는다 — 「값을 옮겼는데
            // 일부만 따라오는」 절반짜리 데이터화를 잡는 자리다.
            if (!Near(rules.PatternMultiplierFor(Ascend.Prototype.Spin.PatternKind.Line, kind), 4f))
                return $"직선 배수 {rules.PatternMultiplierFor(Ascend.Prototype.Spin.PatternKind.Line, kind)} — 프로파일의 4 가 아니다";
            if (!Near(rules.PatternMultiplierFor(Ascend.Prototype.Spin.PatternKind.Cluster, kind), 6f))
                return $"연결 배수 {rules.PatternMultiplierFor(Ascend.Prototype.Spin.PatternKind.Cluster, kind)} — 프로파일의 6 이 아니다";
            if (!Near(rules.PatternMultiplierFor(Ascend.Prototype.Spin.PatternKind.FullBoard, kind), 9f))
                return $"잭팟 배수 {rules.PatternMultiplierFor(Ascend.Prototype.Spin.PatternKind.FullBoard, kind)} — 프로파일의 9 가 아니다";
            // 흩어짐은 다이얼이 아니라 기준점 1.0 이다. 프로파일이 건드리면 안 된다.
            if (!Near(rules.PatternMultiplierFor(Ascend.Prototype.Spin.PatternKind.Scattered, kind), 1f))
                return $"흩어짐 배수 {rules.PatternMultiplierFor(Ascend.Prototype.Spin.PatternKind.Scattered, kind)} — 기준점 1.0 이어야 한다";
            if (!Near(rules.CascadeMultiplierStep, 0.25f))
                return $"연쇄 증분 {rules.CascadeMultiplierStep} — 프로파일의 0.25 가 아니다";
            return null;
        }

        /// <summary>
        /// 사슬의 마지막 고리. 스냅샷이 `RunSession` → `FloorSession` → `BuildRules` 까지
        /// 실제로 도달하는지 본다. `CreateDefault(balance)` 만 검사하면 층이 인자 없는
        /// 판본을 계속 불러도 통과한다.
        /// </summary>
        private static string TestBalanceReachesFloorSession()
        {
            var run = new Ascend.Prototype.Run.RunSession(
                1337, 0f, 0f, OverharvestProfile.DefaultSnapshot,
                WeightProfile.DefaultSnapshot, ProbeBalance(), null);

            if (run.Current == null) return "첫 층이 없다";
            if (run.Current.Balance.SourceName != "테스트 프로브")
                return $"층이 든 출처가 '{run.Current.Balance.SourceName}' — 넘긴 스냅샷이 도달하지 않았다";

            var rules = run.Current.Rules;
            if (rules == null) return "첫 층에 규칙 다발이 없다 (적재·계약 단계일 수 있다)";
            if (!Near(rules.LineMultiplier, 4f))
                return $"층 규칙의 직선 배수 {rules.LineMultiplier} — 프로파일의 4 가 아니다"
                     + " (BuildRules 가 인자 없는 판본을 부르나)";
            // 1층 풀은 영혼·흡수체뿐이라 증식체는 0으로 걸러진다. 가중치가 프로파일에서
            // 왔는지는 **풀에 있는** 종류로 본다.
            if (!Near(rules.WeightOf(Ascend.Prototype.Spin.SymbolKind.NormalSoul), 11f))
                return $"층 규칙의 정상 영혼 가중치 {rules.WeightOf(Ascend.Prototype.Spin.SymbolKind.NormalSoul)} — 프로파일의 11 이 아니다";
            return null;
        }

        private static string TestSpinBalanceFallbackIsNamed()
        {
            SpinBalanceSnapshot fallback = SpinBalanceProfile.SnapshotOrDefault(null, "테스트");
            if (fallback.SourceName != SpinBalanceSnapshot.CodePresetName)
                return $"폴백 출처가 '{fallback.SourceName}'";
            if (fallback.FromAsset) return "폴백인데 FromAsset 이 참이다";

            var profile = ScriptableObject.CreateInstance<SpinBalanceProfile>();
            try
            {
                profile.name = "SpinProbe";
                SpinBalanceSnapshot fromAsset = SpinBalanceProfile.SnapshotOrDefault(profile, "테스트");
                if (fromAsset.SourceName != "SpinProbe")
                    return $"에셋 출처가 '{fromAsset.SourceName}'";
                if (!Near(fromAsset.WeightNormalSoul, SpinBalanceProfile.DefaultWeightNormalSoul))
                    return $"새 에셋의 영혼 가중치 {fromAsset.WeightNormalSoul} 이 프리셋과 다르다";
                if (fromAsset.Validate() != null)
                    return $"새 에셋 기본값이 자기모순이다: {fromAsset.Validate()}";
                return null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        private static string TestSpinBalanceValidate()
        {
            if (SpinBalanceProfile.DefaultSnapshot.Validate() != null)
                return $"기본값이 자기모순으로 판정됐다: {SpinBalanceProfile.DefaultSnapshot.Validate()}";
            if (ProbeBalance().Validate() != null)
                return $"프로브 값이 자기모순으로 판정됐다: {ProbeBalance().Validate()}";

            // 직선이 흩어짐과 구분되지 않으면 2층이 가르칠 것이 없다.
            if (new SpinBalanceSnapshot(5f, 2.5f, 2.5f, 1f, 3f, 10f, 3, 0.5f, 8f, 0.6f, "x").Validate() == null)
                return "직선 배수 1.0 을 통과시켰다";
            // 연결이 직선 이하면 3층이 가르칠 것이 없다.
            if (new SpinBalanceSnapshot(5f, 2.5f, 2.5f, 3f, 3f, 10f, 3, 0.5f, 8f, 0.6f, "x").Validate() == null)
                return "연결 배수 == 직선 배수를 통과시켰다";
            // 잭팟이 연결 이하면 9칸 전부가 보상이 아니게 된다.
            if (new SpinBalanceSnapshot(5f, 2.5f, 2.5f, 2f, 3f, 3f, 3, 0.5f, 8f, 0.6f, "x").Validate() == null)
                return "잭팟 배수 == 연결 배수를 통과시켰다";
            // 전력 공급원이 판에 안 나오는 값.
            if (new SpinBalanceSnapshot(0f, 2.5f, 2.5f, 2f, 3f, 10f, 3, 0.5f, 8f, 0.6f, "x").Validate() == null)
                return "정상 영혼 가중치 0 을 통과시켰다";
            // 연쇄가 길어져도 보상이 그대로인 값.
            if (new SpinBalanceSnapshot(5f, 2.5f, 2.5f, 2f, 3f, 10f, 3, 0f, 8f, 0.6f, "x").Validate() == null)
                return "연쇄 증분 0 을 통과시켰다";
            return null;
        }

        /// <summary>
        /// 하드 캡 20은 밸런스 다이얼이 아니라 `MASTER_PRD.md` §6 · `TECH_SPEC.md` §9 가
        /// 못박은 명세다. 프로파일에 넣으면 「고쳐도 되는 것」과 「고치면 명세 위반인 것」이
        /// 같은 인스펙터에 나란히 놓이고, 그 상태에서 누가 20을 8로 내려도 아무도 못 막는다.
        /// 프로파일에서 온 규칙 다발이 여전히 20을 들고 있는지 본다.
        /// </summary>
        private static string TestCascadeCapIsNotADial()
        {
            var rules = Ascend.Prototype.Spin.SpinRuleSet.CreateDefault(ProbeBalance());
            if (rules.MaxCascadeDepth != 20)
                return $"연쇄 하드 캡이 {rules.MaxCascadeDepth} — 프로파일이 건드리면 안 되는 값이다";
            // `RequiredFieldCount != 10` 을 여기서 검사하려 했으나 둘 다 컴파일 상수라
            // 분기가 접혀 **아무것도 검사하지 않는 단언**이 된다(CS0162 가 그걸 알려 줬다).
            // 캡이 프로파일로 새어 들어갔는지는 위 한 줄이 실제로 잡는다.
            return null;
        }

        // ── 위험 임계값 ⑦ (`UP-TECH-09`) ────────────────────────────────────

        /// <summary>잔류·과수확·과적 없이 점수만 만드는 입력.</summary>
        private static RiskInputs Residuals(int absorbers, int extraSpins)
        {
            return new RiskInputs(absorbers, 0, extraSpins, false, 3, 0.5f, false);
        }

        private static string TestRiskPresetMatchesEvaluator()
        {
            var fresh = new RiskEvaluator();
            if (!Near(RiskThresholdProfile.DefaultAbsorberWeight, fresh.AbsorberWeight))
                return $"흡수체 가중치 {RiskThresholdProfile.DefaultAbsorberWeight} vs 평가기 {fresh.AbsorberWeight}";
            if (!Near(RiskThresholdProfile.DefaultProliferatorWeight, fresh.ProliferatorWeight))
                return $"증식체 가중치 {RiskThresholdProfile.DefaultProliferatorWeight} vs 평가기 {fresh.ProliferatorWeight}";
            if (!Near(RiskThresholdProfile.DefaultOverharvestWeight, fresh.OverharvestWeight))
                return $"과수확 점수 {RiskThresholdProfile.DefaultOverharvestWeight} vs 평가기 {fresh.OverharvestWeight}";
            if (!Near(RiskThresholdProfile.DefaultOverloadScore, fresh.OverloadScore))
                return $"과적 점수 {RiskThresholdProfile.DefaultOverloadScore} vs 평가기 {fresh.OverloadScore}";
            if (!Near(RiskThresholdProfile.DefaultShortfallScore, fresh.ShortfallScore))
                return $"미달 점수 {RiskThresholdProfile.DefaultShortfallScore} vs 평가기 {fresh.ShortfallScore}";
            if (!Near(RiskThresholdProfile.DefaultStrainEnter, fresh.StrainEnter)
                || !Near(RiskThresholdProfile.DefaultStrainExit, fresh.StrainExit)
                || !Near(RiskThresholdProfile.DefaultCriticalEnter, fresh.CriticalEnter)
                || !Near(RiskThresholdProfile.DefaultCriticalExit, fresh.CriticalExit))
                return $"임계값 {fresh.StrainExit}→{fresh.StrainEnter} / {fresh.CriticalExit}→{fresh.CriticalEnter}"
                     + " 가 프리셋과 다르다";

            // Apply 로 프리셋을 넣어도 값이 그대로여야 한다 — 그래야 에셋을 만드는 것만으로
            // 난이도가 바뀌지 않는다.
            var applied = new RiskEvaluator();
            applied.Apply(RiskThresholdProfile.DefaultSnapshot);
            if (!Near(applied.StrainEnter, fresh.StrainEnter) || !Near(applied.OverharvestWeight, fresh.OverharvestWeight))
                return "프리셋을 Apply 했더니 값이 달라졌다";
            return null;
        }

        private static string TestRiskThresholdsFollowProfile()
        {
            // 프리셋(흡수 1.0 · Strain 진입 3.0)과 다른 수. 흡수체 하나가 2.5점이고
            // Strain 진입이 2.0 이면 **하나만으로 Strain** 이다 — 프리셋이면 Stable 이다.
            var tuned = new RiskEvaluator();
            tuned.Apply(new RiskThresholdSnapshot(2.5f, 1.2f, 3.2f, 3.0f, 4.0f,
                2.0f, 1.0f, 7.0f, 5.5f, "테스트 프로브"));

            if (!Near(tuned.Score(Residuals(1, 0)), 2.5f))
                return $"흡수체 1개 점수 {tuned.Score(Residuals(1, 0))} — 프로파일의 2.5 가 아니다";

            RiskLevel level = tuned.Evaluate(Residuals(1, 0));
            if (level != RiskLevel.Strain)
                return $"흡수체 1개에서 단계가 {level} — 임계값 2.0 이면 Strain 이어야 한다"
                     + " (프리셋 3.0 으로 되돌아갔나)";

            // 같은 입력을 프리셋 평가기에 주면 Stable 이어야 한다. 두 결과가 갈려야
            // 「임계값이 실제로 판정을 움직인다」가 증명된다.
            var preset = new RiskEvaluator();
            preset.Apply(RiskThresholdProfile.DefaultSnapshot);
            RiskLevel presetLevel = preset.Evaluate(Residuals(1, 0));
            if (presetLevel != RiskLevel.Stable)
                return $"프리셋에서 흡수체 1개가 {presetLevel} — 1.0점 < 진입 3.0 이라 Stable 이어야 한다";
            return null;
        }

        private static string TestRiskThresholdSourceDistinguishes()
        {
            var untouched = new RiskEvaluator();
            if (untouched.ThresholdSource != "필드 초기값")
                return $"Apply 전 출처가 '{untouched.ThresholdSource}'";

            var fromPreset = new RiskEvaluator();
            fromPreset.Apply(RiskThresholdProfile.DefaultSnapshot);
            if (fromPreset.ThresholdSource != RiskThresholdSnapshot.CodePresetName)
                return $"프리셋 Apply 후 출처가 '{fromPreset.ThresholdSource}'";

            // 같은 수인데 경로가 다르다. 그 구분이 「배선했는가」를 답할 수 있게 한다.
            if (untouched.ThresholdSource == fromPreset.ThresholdSource)
                return "필드 초기값과 코드 프리셋의 출처가 같은 문자열이다 — 배선 여부를 구분할 수 없다";

            var profile = ScriptableObject.CreateInstance<RiskThresholdProfile>();
            try
            {
                profile.name = "RiskProbe";
                var fromAsset = new RiskEvaluator();
                fromAsset.Apply(RiskThresholdProfile.SnapshotOrDefault(profile, "테스트"));
                if (fromAsset.ThresholdSource != "RiskProbe")
                    return $"에셋 Apply 후 출처가 '{fromAsset.ThresholdSource}'";
                return null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        private static string TestRiskHysteresisValidate()
        {
            if (RiskThresholdProfile.DefaultSnapshot.Validate() != null)
                return $"기본값이 자기모순으로 판정됐다: {RiskThresholdProfile.DefaultSnapshot.Validate()}";

            // 이탈 == 진입. 경계에서 단계가 떨린다.
            if (new RiskThresholdSnapshot(1f, 1.2f, 3.2f, 3f, 4f, 3f, 3f, 7f, 5.5f, "x").Validate() == null)
                return "Strain 이탈 == 진입을 통과시켰다";
            if (new RiskThresholdSnapshot(1f, 1.2f, 3.2f, 3f, 4f, 3f, 2f, 7f, 7f, "x").Validate() == null)
                return "Critical 이탈 == 진입을 통과시켰다";
            // 단계 순서 역전 — Strain 을 건너뛴다.
            if (new RiskThresholdSnapshot(1f, 1.2f, 9f, 3f, 4f, 8f, 2f, 7f, 5.5f, "x").Validate() == null)
                return "Strain 진입 > Critical 진입을 통과시켰다";
            // 저항체를 남겨도 위험해지지 않는 값.
            if (new RiskThresholdSnapshot(0f, 0f, 3.2f, 3f, 4f, 3f, 2f, 7f, 5.5f, "x").Validate() == null)
                return "잔류 가중치가 둘 다 0인 값을 통과시켰다";
            return null;
        }

        /// <summary>
        /// `MASTER_PRD.md` §7 — 과수확은 「공간적 사건」이어야 한다. 한 번 당겼는데
        /// 방이 그대로 Stable 이면 그 문장이 거짓이 된다. 값 검사와 실제 판정 둘 다 본다.
        /// </summary>
        private static string TestOverharvestMustLeaveStable()
        {
            // 과수확 점수 2.0 < Strain 진입 3.0 — 한 번 당겨도 Stable 이다.
            var bad = new RiskThresholdSnapshot(1f, 1.2f, 2f, 3f, 4f, 3f, 2f, 7f, 5.5f, "x");
            if (bad.Validate() == null)
                return "과수확 점수 < Strain 진입을 통과시켰다 — PRD §7 이 거짓이 되는 값이다";

            // 검사만 있고 실제로 그렇게 도는지 안 보면 반쪽이다. 그 값을 실제 평가기에
            // 넣어 한 번 당긴 상태가 정말 Stable 로 나오는지 확인한다 — 검사가 막으려는
            // 것이 무엇인지 이 단언이 보여 준다.
            var loose = new RiskEvaluator();
            loose.Apply(bad);
            if (loose.Evaluate(Residuals(0, 1)) != RiskLevel.Stable)
                return "과수확 1회가 Stable 이 아니다 — 이 테스트가 막으려는 상황을 재현하지 못했다";

            // 프리셋은 반대여야 한다. 한 번 당기면 방이 바뀐다.
            var preset = new RiskEvaluator();
            preset.Apply(RiskThresholdProfile.DefaultSnapshot);
            RiskLevel pulled = preset.Evaluate(Residuals(0, 1));
            if (pulled == RiskLevel.Stable)
                return $"프리셋에서 과수확 1회가 Stable 이다 (점수 {preset.Score(Residuals(0, 1))},"
                     + $" 진입 {preset.StrainEnter}) — PRD §7 위반";
            return null;
        }

        // ── 승객 대사 ⑨ (`UP-TECH-09`) ──────────────────────────────────────
        //
        // 백로그는 ⑨ 를 「코드 상수·정적 배열」로 적어 뒀으나 **실측은 다르다.**
        // `PassengerReactionSet.asset` 은 존재하고 11종이 채워져 있으며 씬이 물고 있다.
        // 진짜 결함은 좁고 정확하다: **그 에셋에 `Line` 필드가 아예 직렬화돼 있지 않다**
        // (`Line:` 0개). `Line` 은 에셋이 만들어진 뒤에 생긴 필드라, 11종 전부가 조용히
        // 코드 기본 대사로 폴백한다. 화면에서는 대사가 나오므로 아무도 눈치채지 못한다.
        //
        // 고치는 방법은 코드가 아니다 — 인스펙터에서 그 에셋의 톱니바퀴 ▸ Reset 을 한 번
        // 누르면 대사·대조까지 다시 직렬화된다. `.asset` 은 단일 소유 파일이라 씬 오너의
        // 일이고, 그래서 여기서는 **상태를 고정하는 것까지만** 한다.


        /// <summary>
        /// Reset 이 실제로 대사를 채우는지 본다 — 그게 이 항목의 **처방**이기 때문이다.
        /// 이 검사가 통과하는데 디스크의 에셋에 대사가 없다면, 남은 것은 클릭 한 번이다.
        /// </summary>
        private static string TestReactionResetFillsLines()
        {
            var set = ScriptableObject.CreateInstance<Ascend.Prototype.Npc.PassengerReactionSet>();
            try
            {
                set.Reset();
                if (set.Entries.Count != 11)
                    return $"Reset 이 {set.Entries.Count}종을 채웠다 — PRD §9.2 의 11종이어야 한다";

                foreach (var kind in Ascend.Prototype.Npc.PassengerReactionEvents.All)
                {
                    if (!set.HasEntry(kind))
                        return $"{kind} 항목이 Reset 뒤에도 없다";
                    if (!set.For(kind).HasLine)
                        return $"{kind} 의 대사가 비어 있다 — Reset 이 대사를 채우지 않으면 처방이 성립하지 않는다";
                }
                return null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(set);
            }
        }

        private static string TestReactionLineFallsBack()
        {
            var set = ScriptableObject.CreateInstance<Ascend.Prototype.Npc.PassengerReactionSet>();
            try
            {
                set.Reset();
                var kind = Ascend.Prototype.Npc.PassengerReactionEvent.ContractChosen;
                string codeLine = set.For(kind).Line;
                if (string.IsNullOrEmpty(codeLine)) return "코드 기본 대사가 비어 있다";

                // 디스크의 에셋과 같은 상태를 만든다 — 항목은 있는데 대사만 빈 것.
                var blank = ScriptableObject.CreateInstance<Ascend.Prototype.Npc.PassengerReactionSet>();
                try
                {
                    var reaction = set.For(kind);
                    reaction.Line = null;
                    blank.ReplaceEntries(new[]
                    {
                        new Ascend.Prototype.Npc.PassengerReactionSet.Entry(kind, reaction)
                    });

                    if (blank.For(kind).Line != codeLine)
                        return $"대사가 빈 항목이 '{blank.For(kind).Line}' 를 냈다 — 코드 기본값 '{codeLine}' 으로 채워져야 한다";
                    // 나머지 채널은 에셋 값이 그대로여야 한다. 대사만 폴백이다.
                    if (blank.For(kind).Duration != reaction.Duration)
                        return "대사 폴백이 다른 채널까지 덮어썼다";
                    return null;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(blank);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(set);
            }
        }

        private static string TestReactionLineOverrides()
        {
            var set = ScriptableObject.CreateInstance<Ascend.Prototype.Npc.PassengerReactionSet>();
            try
            {
                var kind = Ascend.Prototype.Npc.PassengerReactionEvent.BasicPurify;
                var reaction = new Ascend.Prototype.Npc.PassengerReaction(
                    Ascend.Prototype.Npc.ReactionPose.Lean, Ascend.Prototype.Npc.ReactionGaze.Device,
                    "cue", "데이터에서 온 대사", 1f, 0.3f, 10, 6f);
                set.ReplaceEntries(new[]
                {
                    new Ascend.Prototype.Npc.PassengerReactionSet.Entry(kind, reaction)
                });

                if (set.For(kind).Line != "데이터에서 온 대사")
                    return $"에셋 대사가 '{set.For(kind).Line}' 로 나왔다 — 데이터가 코드를 이겨야 한다";
                return null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(set);
            }
        }

        /// <summary>
        /// 디스크의 에셋을 텍스트로 읽어 `Line:` 개수를 센다. 플레이스홀더 PNG 검사와
        /// 같은 방식이다 — 에디터 API 없이 도는 검사여야 하기 때문이다.
        ///
        /// **0 또는 11 만 허용한다.** 0 은 지금의 알려진 상태(전부 코드 폴백)이고
        /// 11 은 Reset 을 누른 뒤다. 그 사이 값은 **절반만 고친 것**이고, 그 상태가
        /// 가장 나쁘다 — 일부 사건만 데이터에서 오면 「대사를 데이터로 옮겼다」가
        /// 참인지 거짓인지 아무도 말할 수 없다.
        /// </summary>
        private static string TestShippedReactionAssetLineCoverage()
        {
            const string relative = "Prototype_Elevator/Data/Profiles/PassengerReactionSet.asset";
            string path = AbsoluteAssetPath(relative);
            if (!File.Exists(path))
                return $"{relative} 가 없다 — 씬이 물고 있는 에셋이다";

            string text = File.ReadAllText(path);
            int entries = CountOccurrences(text, "- Event:");
            int lines = CountOccurrences(text, "Line:");

            if (entries != 11)
                return $"에셋 항목이 {entries}종이다 — PRD §9.2 의 11종이어야 한다";
            if (lines != 0 && lines != entries)
                return $"대사가 {entries}종 중 {lines}종만 직렬화돼 있다 — 절반만 고친 상태다."
                     + " 인스펙터에서 이 에셋의 톱니바퀴 ▸ Reset 을 눌러 전부 채울 것";
            return null;
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0, at = 0;
            while ((at = haystack.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
            {
                count++;
                at += needle.Length;
            }
            return count;
        }
    }
}

// 씬 배선 필요: 없다. 씬 없이 도는 테스트다.
// 러너 편입: **끝났다** (2026-08-02 확인). `Assets/Editor/PrototypeSelfTest.cs:82` 의
//   FoldInSuite("데이터 프로파일", …ProfileTests.RunAll()) 과 `AscendTestMenu.cs:36` 둘 다
//   이 스위트를 부른다. 「편입 필요」는 낡은 기록이었다.
// 에셋 요구: 없다. 플레이스홀더 텍스처 검사는 `Assets/Prototype_Elevator/Art/Textures/` 의
//   PNG 4장을 디스크에서 직접 읽는다. 넷은 저장소에 커밋돼 있으므로 씬도 임포트도 필요 없다.
//   넷이 사라지면 6건이 「가 없다」로 실패한다 — 그게 맞는 동작이다.
