using System;
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
            Run("파티클 상한이 AmbientParticleDirector 와 같다", TestParticleCapsMatchDirector, ref passed, ref failed, report);
            Run("정지 구간이 0이 아니다", TestPresentationHoldsAreNonZero, ref passed, ref failed, report);
            Run("연쇄 압축이 하한 아래로 내려가지 않는다", TestPresentationTempoFloor, ref passed, ref failed, report);
            Run("Reset 직후 스냅샷이 기본 스냅샷과 같다", TestResetMatchesDefaults, ref passed, ref failed, report);

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
    }
}

// 씬 배선 필요: 없다. 씬 없이 도는 테스트다.
// 러너 편입 필요: `Assets/Editor/PrototypeSelfTest.cs` 의 `RunAllToString()` 에
//   FoldInSuite("데이터 프로파일", Ascend.Prototype.Data.Profiles.Tests.ProfileTests.RunAll());
//   를 추가해야 커밋 게이트가 이 19건을 지킨다. 그 파일은 다른 소유 영역이라 여기서 고치지 않았다.
