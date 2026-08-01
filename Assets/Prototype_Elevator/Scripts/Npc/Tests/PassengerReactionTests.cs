using System;
using System.Collections.Generic;
using System.Text;
using Ascend.Prototype.Events;
using UnityEngine;

namespace Ascend.Prototype.Npc.Tests
{
    /// <summary>
    /// 승객 반응 계약의 헤드리스 검증. UP-NPC-02(반응 이벤트 10종)·UP-NPC-03(데이터화)·
    /// UP-NPC-05(동시 반응 제한)가 여기서 고정된다.
    ///
    /// 씬을 만들지 않는다 — 이 세 항목의 실패는 전부 "규칙이 틀렸다"이지 "배치가 틀렸다"가
    /// 아니다. 씬이 필요한 순간(자세가 실제로 보이는가)은 UP-NPC-04의 고정 캡처가 맡는다.
    /// NUnit에 의존하지 않는 이유는 `DECISION_LOG.md` D-20260730-06 참조.
    /// </summary>
    public static class PassengerReactionTests
    {
        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0;
            int failed = 0;
            var report = new StringBuilder();

            Run("11종 전부가 사건 목록에서 유도된다", TestMapsAllEleven, ref passed, ref failed, report);
            Run("반응 대상이 아닌 사건은 false", TestUnrelatedKindsAreNotMapped, ref passed, ref failed, report);
            Run("5연쇄는 깊이 5부터, 4에서는 아니다", TestFiveChainDepth, ref passed, ref failed, report);
            Run("임계점 100·170·300 이 서로 갈린다", TestThresholdsSplit, ref passed, ref failed, report);
            Run("동시 반응이 최대 수를 넘지 않는다", TestMaxConcurrent, ref passed, ref failed, report);
            Run("쿨다운 안에는 같은 승객이 다시 반응하지 않는다", TestCooldown, ref passed, ref failed, report);
            Run("높은 우선순위만 진행 중인 반응을 덮어쓴다", TestPriorityOverride, ref passed, ref failed, report);
            Run("승객 0명에서 예외가 나지 않는다", TestNoPassengers, ref passed, ref failed, report);
            Run("라운드 로빈이 결정론적이다", TestRoundRobinDeterminism, ref passed, ref failed, report);
            Run("만료된 반응이 슬롯을 놓아준다", TestExpiryFreesSlot, ref passed, ref failed, report);
            Run("Reset 이 11종을 전부 채운다", TestSetResetFillsAll, ref passed, ref failed, report);
            Run("빈 세트는 코드 기본값으로 폴백한다", TestEmptySetFallsBack, ref passed, ref failed, report);
            Run("라우터가 버스 사건을 중재기로 잇는다", TestRouterBridgesBus, ref passed, ref failed, report);
            Run("라우터를 두 번 붙여도 한 번만 센다", TestRouterDoesNotDoubleSubscribe, ref passed, ref failed, report);

            // UP-NPC-04 — 표현 채널 넷. **총합이 아니라 종류를 센다.**
            Run("표현 채널 넷이 각각 값을 낸다", TestFourChannelsProduceValues, ref passed, ref failed, report);
            Run("짧은 대사가 한 문장 이하다", TestLinesAreShort, ref passed, ref failed, report);
            Run("같은 사건에 승객마다 상반된 반응이 나온다", TestContrastSplitsPassengers, ref passed, ref failed, report);
            Run("대조 반응이 중재 규칙을 바꾸지 않는다", TestContrastKeepsArbitration, ref passed, ref failed, report);
            Run("대조 조회기가 없으면 전원이 같은 반응이다", TestNoContrastMeansUniform, ref passed, ref failed, report);
            Run("Reset 이 대조 반응 11종도 채운다", TestSetResetFillsContrasts, ref passed, ref failed, report);
            Run("대사 없는 옛 항목이 코드 기본값으로 폴백한다", TestLegacyEntryFallsBackForLine, ref passed, ref failed, report);

            report.Insert(0, "[상승] === Passenger Reaction Tests ===\n");
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

        // ── 변환 계약 ────────────────────────────────────────────────────────

        private static string Expect(GameEventKind kind, int intValue, PassengerReactionEvent expected)
        {
            var e = new GameEvent(kind, 3, 0, intValue);
            PassengerReactionEvent got;
            if (!PassengerReactionEvents.TryMap(in e, out got))
                return $"{kind}(i={intValue}) 가 매핑되지 않았다";
            if (got != expected)
                return $"{kind}(i={intValue}) → {got}, 기대 {expected}";
            return null;
        }

        private static string ExpectNoReaction(GameEventKind kind, int intValue)
        {
            var e = new GameEvent(kind, 3, 0, intValue);
            PassengerReactionEvent got;
            if (PassengerReactionEvents.TryMap(in e, out got))
                return $"{kind}(i={intValue}) 가 {got} 로 매핑됐다 — 반응 대상이 아니다";
            if (got != PassengerReactionEvent.None)
                return $"{kind}: false 인데 out 이 {got} 다";
            return null;
        }

        private static string TestMapsAllEleven()
        {
            string failure =
                Expect(GameEventKind.ContractSelected, 0, PassengerReactionEvent.ContractChosen) ??
                Expect(GameEventKind.PurifyScattered, 3, PassengerReactionEvent.BasicPurify) ??
                Expect(GameEventKind.CascadeStep, 5, PassengerReactionEvent.FiveChain) ??
                Expect(GameEventKind.PowerThresholdCrossed, 100, PassengerReactionEvent.Threshold100) ??
                Expect(GameEventKind.PowerThresholdCrossed, 170, PassengerReactionEvent.Threshold170) ??
                Expect(GameEventKind.PowerThresholdCrossed, 300, PassengerReactionEvent.Threshold300) ??
                Expect(GameEventKind.OverharvestUnlocked, 100, PassengerReactionEvent.OverharvestUnlocked) ??
                Expect(GameEventKind.OverharvestApproached, 0, PassengerReactionEvent.OverharvestApproach) ??
                Expect(GameEventKind.ExtraSpinTaken, 1, PassengerReactionEvent.ExtraSpin) ??
                Expect(GameEventKind.RiskLevelChanged, (int)Risk.RiskLevel.Critical, PassengerReactionEvent.CollapseImminent) ??
                Expect(GameEventKind.CollapseBegan, 0, PassengerReactionEvent.CollapseImminent) ??
                Expect(GameEventKind.FloorResolved, 4, PassengerReactionEvent.AccidentOrSuccess) ??
                Expect(GameEventKind.RunEnded, 7, PassengerReactionEvent.AccidentOrSuccess);
            if (failure != null) return failure;

            // 목록에 있는데 아무 사건에서도 나올 수 없는 반응이 있으면 그 항목은 죽은 코드다.
            // PRD §9.2가 10종을 세는 이상 도달 불가능한 항목이 있어서는 안 된다.
            var reachable = new HashSet<PassengerReactionEvent>();
            foreach (GameEventKind kind in (GameEventKind[])Enum.GetValues(typeof(GameEventKind)))
            {
                foreach (int value in new[] { 0, 1, 4, 5, 100, 170, 300, (int)Risk.RiskLevel.Critical })
                {
                    var e = new GameEvent(kind, 1, 0, value);
                    PassengerReactionEvent got;
                    if (PassengerReactionEvents.TryMap(in e, out got)) reachable.Add(got);
                }
            }

            IReadOnlyList<PassengerReactionEvent> all = PassengerReactionEvents.All;
            if (all.Count != 11) return $"반응 목록이 {all.Count}종 — PRD §9.2 는 10종 + 결과다";
            for (int i = 0; i < all.Count; i++)
                if (!reachable.Contains(all[i]))
                    return $"{all[i]} 는 어떤 사건에서도 나오지 않는다";
            return null;
        }

        private static string TestUnrelatedKindsAreNotMapped()
        {
            // 직선·연결 정화가 여기 있는 것은 의도다. PRD §9.2 의 「기본 정화」는 개수 정화
            // 하나이고, 나머지 정화까지 같은 반응으로 묶으면 "기본"이 뜻을 잃는다.
            GameEventKind[] silent =
            {
                GameEventKind.None,
                GameEventKind.FloorStarted,
                GameEventKind.ItemBoarded,
                GameEventKind.BoardingFinished,
                GameEventKind.SpinStarted,
                GameEventKind.ColumnRevealed,
                GameEventKind.NormalSoulHarvested,
                GameEventKind.PurifyLine,
                GameEventKind.PurifyCluster,
                GameEventKind.CascadeCapReached,
                GameEventKind.ResidualDamage,
                GameEventKind.SpinResolved,
                GameEventKind.OverharvestReleased,
                GameEventKind.OverharvestPulled,
                GameEventKind.PowerBanked,
                GameEventKind.JettisonPaid,
            };

            for (int i = 0; i < silent.Length; i++)
            {
                string failure = ExpectNoReaction(silent[i], 0) ?? ExpectNoReaction(silent[i], 300);
                if (failure != null) return failure;
            }

            // Warning 진입은 반응하지 않는다 — 매 경고마다 비명이면 Critical 이 강해지지 않는다.
            return ExpectNoReaction(GameEventKind.RiskLevelChanged, (int)Risk.RiskLevel.Strain);
        }

        private static string TestFiveChainDepth()
        {
            string failure = ExpectNoReaction(GameEventKind.CascadeStep, 1)
                          ?? ExpectNoReaction(GameEventKind.CascadeStep, 4);
            if (failure != null) return failure;

            return Expect(GameEventKind.CascadeStep, 5, PassengerReactionEvent.FiveChain)
                ?? Expect(GameEventKind.CascadeStep, 9, PassengerReactionEvent.FiveChain);
        }

        private static string TestThresholdsSplit()
        {
            string failure =
                Expect(GameEventKind.PowerThresholdCrossed, 100, PassengerReactionEvent.Threshold100) ??
                Expect(GameEventKind.PowerThresholdCrossed, 170, PassengerReactionEvent.Threshold170) ??
                Expect(GameEventKind.PowerThresholdCrossed, 300, PassengerReactionEvent.Threshold300);
            if (failure != null) return failure;

            // `FloorSession.ThresholdGates` 가 세 값만 발행한다. 그 사이 값이 반응을 내면
            // 게이트 목록과 반응 목록이 어긋났다는 뜻이다.
            return ExpectNoReaction(GameEventKind.PowerThresholdCrossed, 99)
                ?? ExpectNoReaction(GameEventKind.PowerThresholdCrossed, 250);
        }

        // ── 중재 규칙 ────────────────────────────────────────────────────────

        /// <summary>테스트가 값을 직접 정한다. 기본값 밸런스가 움직여도 규칙 자체는 유지되어야 한다.</summary>
        private static PassengerReaction Reaction(int priority, float duration, float cooldown) =>
            new PassengerReaction(ReactionPose.Flinch, ReactionGaze.Device,
                                  "test_cue", duration, 0.5f, priority, cooldown);

        private static Func<PassengerReactionEvent, PassengerReaction> Fixed(float duration, float cooldown)
        {
            return reactionEvent =>
            {
                switch (reactionEvent)
                {
                    case PassengerReactionEvent.CollapseImminent: return Reaction(90, duration, cooldown);
                    case PassengerReactionEvent.BasicPurify:      return Reaction(5, duration, cooldown);
                    default:                                     return Reaction(50, duration, cooldown);
                }
            };
        }

        private static string TestMaxConcurrent()
        {
            var director = new PassengerReactionDirector(5, 2, Fixed(2f, 4f));
            int reacted = director.Notify(PassengerReactionEvent.FiveChain, 0f).Count;
            if (reacted != 2) return $"승객 5명 / 한도 2인데 {reacted}명이 반응했다";
            if (director.ActiveCount != 2) return $"ActiveCount {director.ActiveCount}";

            // 같은 순간에 다른 사건이 또 와도 한도는 그대로다(§9.4).
            director.Notify(PassengerReactionEvent.Threshold170, 0f);
            if (director.ActiveCount > 2) return $"두 번째 사건 뒤 ActiveCount {director.ActiveCount}";

            int reacting = 0;
            for (int i = 0; i < 5; i++) if (director.IsReacting(i)) reacting++;
            if (reacting != director.ActiveCount)
                return $"IsReacting {reacting} ≠ ActiveCount {director.ActiveCount}";
            return null;
        }

        private static string TestCooldown()
        {
            var director = new PassengerReactionDirector(2, 2, Fixed(1f, 5f));
            if (director.Notify(PassengerReactionEvent.FiveChain, 0f).Count != 2)
                return "선행 조건 실패: 두 명이 반응하지 않았다";

            director.Tick(1.5f);   // 지속 1초는 끝났다
            if (director.ActiveCount != 0) return $"만료 후 ActiveCount {director.ActiveCount}";

            // 반응은 끝났지만 쿨다운(5초)은 남아 있다.
            if (director.Notify(PassengerReactionEvent.FiveChain, 2f).Count != 0)
                return "쿨다운 안인데 다시 반응했다";

            if (director.Notify(PassengerReactionEvent.FiveChain, 6f).Count != 2)
                return "쿨다운이 끝났는데도 반응하지 않았다";
            return null;
        }

        private static string TestPriorityOverride()
        {
            // 승객 2명 · 한도 2 — 자리가 꽉 차 있어야 "덮어쓰기"와 "빈자리 채우기"가 구분된다.
            var director = new PassengerReactionDirector(2, 2, Fixed(10f, 10f));

            if (director.Notify(PassengerReactionEvent.BasicPurify, 0f).Count != 2)
                return "선행 조건 실패: 낮은 우선순위 반응이 시작되지 않았다";

            int overridden = director.Notify(PassengerReactionEvent.CollapseImminent, 1f).Count;
            if (overridden != 2) return $"높은 우선순위가 {overridden}명만 덮었다";
            if (director.ActiveCount != 2) return $"덮어쓰기가 인원을 늘렸다: {director.ActiveCount}";
            if (director.CurrentEventOf(0) != PassengerReactionEvent.CollapseImminent)
                return $"0번 승객이 아직 {director.CurrentEventOf(0)}";
            if (director.CurrentOf(0).Priority != 90)
                return $"덮어쓴 반응의 우선순위 {director.CurrentOf(0).Priority}";

            // 반대는 안 된다.
            if (director.Notify(PassengerReactionEvent.BasicPurify, 2f).Count != 0)
                return "낮은 우선순위가 높은 반응을 덮었다";

            // 같아도 안 된다 — 같은 사건이 연달아 오면 반응이 계속 리셋돼 끝나지 않는다.
            if (director.Notify(PassengerReactionEvent.CollapseImminent, 3f).Count != 0)
                return "같은 우선순위가 진행 중인 반응을 덮었다";
            return null;
        }

        private static string TestNoPassengers()
        {
            var director = new PassengerReactionDirector(0, 2, Fixed(1f, 1f));
            if (director.PassengerCount != 0) return $"승객 수 {director.PassengerCount}";
            if (director.Notify(PassengerReactionEvent.CollapseImminent, 0f).Count != 0)
                return "승객이 없는데 반응이 나왔다";
            director.Tick(1f);
            if (director.IsReacting(0)) return "IsReacting(0) 이 true";
            if (director.CurrentOf(0).IsActive) return "CurrentOf(0) 이 살아 있다";
            if (director.CurrentEventOf(-1) != PassengerReactionEvent.None) return "음수 인덱스가 반응을 냈다";
            director.Reset();

            // 음수 승객 수도 0으로 접힌다. 적재가 비어 있을 때 −1이 흘러들어올 수 있다.
            var negative = new PassengerReactionDirector(-3, 2, Fixed(1f, 1f));
            if (negative.PassengerCount != 0) return $"음수 승객 수 → {negative.PassengerCount}";
            if (negative.Notify(PassengerReactionEvent.FiveChain, 0f).Count != 0) return "음수 승객 수에서 반응이 나왔다";
            return null;
        }

        private static string TestRoundRobinDeterminism()
        {
            // 한도 1이면 매 사건마다 한 명씩 — 순서가 그대로 드러난다.
            int[] first = RunSequence();
            int[] second = RunSequence();

            for (int i = 0; i < first.Length; i++)
                if (first[i] != second[i])
                    return $"{i}번째 사건에서 {first[i]} vs {second[i]} — 결정론이 깨졌다";

            int[] expected = { 0, 1, 2, 3, 0 };
            for (int i = 0; i < expected.Length; i++)
                if (first[i] != expected[i])
                    return $"순서 [{string.Join(",", first)}] — 기대 [{string.Join(",", expected)}]";
            return null;
        }

        private static int[] RunSequence()
        {
            var director = new PassengerReactionDirector(4, 1, Fixed(0.5f, 0.5f));
            var picked = new int[5];
            for (int step = 0; step < picked.Length; step++)
            {
                IReadOnlyList<int> selected = director.Notify(PassengerReactionEvent.FiveChain, step);
                picked[step] = selected.Count > 0 ? selected[0] : -1;
            }
            return picked;
        }

        private static string TestExpiryFreesSlot()
        {
            var director = new PassengerReactionDirector(4, 2, Fixed(2f, 2f));
            director.Notify(PassengerReactionEvent.FiveChain, 0f);
            if (director.ActiveCount != 2) return "선행 조건 실패";

            // Notify 는 스스로 만료를 정리해야 한다. 그렇지 않으면 끝난 반응이 자리를
            // 잡고 있어 이후 모든 사건이 조용히 묻힌다.
            int later = director.Notify(PassengerReactionEvent.Threshold300, 3f).Count;
            if (later != 2) return $"만료 뒤 새 사건에서 {later}명만 반응했다";
            if (director.ActiveCount != 2) return $"ActiveCount {director.ActiveCount}";
            if (director.StartedCount != 4) return $"StartedCount {director.StartedCount}, 기대 4";
            return null;
        }

        // ── 데이터화 ─────────────────────────────────────────────────────────

        private static string TestSetResetFillsAll()
        {
            var set = ScriptableObject.CreateInstance<PassengerReactionSet>();
            try
            {
                set.Reset();
                IReadOnlyList<PassengerReactionEvent> all = PassengerReactionEvents.All;
                if (set.Entries.Count != all.Count)
                    return $"항목 {set.Entries.Count}개, 기대 {all.Count}개";

                for (int i = 0; i < all.Count; i++)
                {
                    PassengerReactionEvent reactionEvent = all[i];
                    if (!set.HasEntry(reactionEvent))
                        return $"{reactionEvent.DisplayName()} 항목이 없다";

                    PassengerReaction reaction = set.For(reactionEvent);
                    if (!reaction.IsActive)
                        return $"{reactionEvent.DisplayName()} 의 지속 시간이 {reaction.Duration}";
                    if (string.IsNullOrEmpty(reaction.VoiceCue))
                        return $"{reactionEvent.DisplayName()} 에 음성 큐가 없다";
                    if (reaction.Intensity <= 0f || reaction.Intensity > 1f)
                        return $"{reactionEvent.DisplayName()} 의 강도 {reaction.Intensity} 가 0~1 밖이다";
                    if (reaction.Cooldown < reaction.Duration)
                        return $"{reactionEvent.DisplayName()} 의 쿨다운 {reaction.Cooldown} < 지속 {reaction.Duration}";
                }

                // 붕괴가 가장 높아야 한다 — 무엇이 진행 중이든 덮어써야 하기 때문이다.
                int collapse = PassengerReactionSet.DefaultFor(PassengerReactionEvent.CollapseImminent).Priority;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] == PassengerReactionEvent.CollapseImminent) continue;
                    if (PassengerReactionSet.DefaultFor(all[i]).Priority >= collapse)
                        return $"{all[i].DisplayName()} 의 우선순위가 붕괴 직전 이상이다";
                }
                return null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(set);
            }
        }

        private static string TestEmptySetFallsBack()
        {
            var set = ScriptableObject.CreateInstance<PassengerReactionSet>();
            try
            {
                // 에디터의 CreateInstance 는 Reset 을 함께 부른다 — 갓 만든 세트는
                // 비어 있지 않다. 폴백이 걸리는 실제 상태(부분 정의 에셋)를 직접 만든다.
                set.Clear();
                if (set.HasEntry(PassengerReactionEvent.FiveChain)) return "빈 세트에 항목이 있다";

                PassengerReaction fallback = set.For(PassengerReactionEvent.FiveChain);
                PassengerReaction expected = PassengerReactionSet.DefaultFor(PassengerReactionEvent.FiveChain);
                if (fallback.Priority != expected.Priority || fallback.Pose != expected.Pose)
                    return $"폴백이 기본값과 다르다: {fallback} vs {expected}";

                if (set.For(PassengerReactionEvent.None).IsActive)
                    return "None 이 살아 있는 반응을 냈다";
                return null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(set);
            }
        }

        // ── 표현 채널 넷 (UP-NPC-04 · MASTER_PRD §9.3) ───────────────────────
        //
        // **종류를 센다.** 「반응 110건」은 한 자세가 110번 나와도 나오는 숫자이고,
        // §9.3이 묻는 것은 시선·자세·짧은 대사·비언어 음성 넷이 각각 값을 내는가다.
        // `PassengerReactionDirector.FiredKindsMask`가 사건 종류에 대해 같은 일을 한다.

        private static int CountBits(int mask)
        {
            int n = 0;
            for (int m = mask; m != 0; m &= m - 1) n++;
            return n;
        }

        private static string TestFourChannelsProduceValues()
        {
            IReadOnlyList<PassengerReactionEvent> all = PassengerReactionEvents.All;

            int poseMask = 0;
            int gazeMask = 0;
            var lines = new HashSet<string>();
            var cues = new HashSet<string>();

            for (int i = 0; i < all.Count; i++)
            {
                PassengerReaction primary = PassengerReactionSet.DefaultFor(all[i]);
                PassengerReactionContrast contrast = PassengerReactionSet.DefaultContrastFor(all[i]);
                if (!contrast.IsDefined)
                    return $"{all[i].DisplayName()} 에 상반된 반응이 정의돼 있지 않다";

                PassengerReaction other = contrast.ApplyTo(in primary);

                if (!primary.HasLine) return $"{all[i].DisplayName()} 대표 반응에 대사가 없다";
                if (!other.HasLine) return $"{all[i].DisplayName()} 대조 반응에 대사가 없다";
                if (!primary.HasVoice) return $"{all[i].DisplayName()} 대표 반응에 음성 큐가 없다";
                if (!other.HasVoice) return $"{all[i].DisplayName()} 대조 반응에 음성 큐가 없다";

                poseMask |= 1 << (int)primary.Pose;
                poseMask |= 1 << (int)other.Pose;
                gazeMask |= 1 << (int)primary.Gaze;
                gazeMask |= 1 << (int)other.Gaze;
                lines.Add(primary.Line);
                lines.Add(other.Line);
                cues.Add(primary.VoiceCue);
                cues.Add(other.VoiceCue);
            }

            // 자세 7종이 전부 쓰여야 한다. 하나라도 안 쓰이면 그 값은 죽은 열거 항목이고,
            // `ReactionPose` 주석이 경계한 "값이 하나인 것과 같은 열거형"에 한 걸음 가까워진다.
            int poseKinds = CountBits(poseMask);
            if (poseKinds < 7) return $"자세가 {poseKinds}종만 쓰인다 — 7종 전부가 나와야 한다";

            // 시선은 `None`(옮기지 않음)을 뺀 5종이 실제 대상이다.
            int gazeKinds = CountBits(gazeMask);
            if (gazeKinds < 5) return $"시선 대상이 {gazeKinds}종만 쓰인다 — 5종이 나와야 한다";
            if ((gazeMask & (1 << (int)ReactionGaze.Player)) == 0)
                return "플레이어를 보는 반응이 하나도 없다 — \"당신이 결정한다\"가 실릴 자리가 없다";

            // 22개(대표 11 + 대조 11) 중 대부분이 서로 달라야 한다. 같은 문장이
            // 열한 번 나오면 대사 채널은 켜져 있지만 정보를 싣지 않는다.
            if (lines.Count < 18) return $"대사가 {lines.Count}종 — 22개 반응이 거의 다 달라야 한다";
            if (cues.Count < 14) return $"음성 큐가 {cues.Count}종 — 반응마다 소리가 갈려야 한다";
            return null;
        }

        /// <summary>
        /// `MASTER_PRD.md` §9.4가 긴 대화 트리를 **제외한다**. 대사가 길어지면 그건
        /// 반응이 아니라 대화이고, 승객 머리 위 월드 라벨은 그것을 담을 수 없다.
        /// </summary>
        private static string TestLinesAreShort()
        {
            IReadOnlyList<PassengerReactionEvent> all = PassengerReactionEvents.All;
            for (int i = 0; i < all.Count; i++)
            {
                PassengerReaction primary = PassengerReactionSet.DefaultFor(all[i]);
                PassengerReaction other =
                    PassengerReactionSet.DefaultContrastFor(all[i]).ApplyTo(in primary);

                string failure = CheckLine(all[i], primary.Line, "대표")
                              ?? CheckLine(all[i], other.Line, "대조");
                if (failure != null) return failure;
            }
            return null;
        }

        private static string CheckLine(PassengerReactionEvent reactionEvent, string line, string role)
        {
            if (string.IsNullOrEmpty(line)) return $"{reactionEvent.DisplayName()} {role} 대사가 비었다";
            if (line.Length > 24)
                return $"{reactionEvent.DisplayName()} {role} 대사가 {line.Length}자 — 한 문장 이하여야 한다";
            if (line.IndexOf('\n') >= 0)
                return $"{reactionEvent.DisplayName()} {role} 대사에 줄바꿈이 있다 — 두 문장이라는 뜻이다";
            return null;
        }

        private static Func<PassengerReactionEvent, PassengerReactionContrast> FixedContrast()
        {
            return reactionEvent => new PassengerReactionContrast(
                ReactionPose.Cheer, ReactionGaze.Door, "test_cue_contrast", "대조 대사", 0.8f);
        }

        private static string TestContrastSplitsPassengers()
        {
            var director = new PassengerReactionDirector(
                2, 2, PassengerReactionSet.DefaultFor, PassengerReactionSet.DefaultContrastFor);

            if (director.Notify(PassengerReactionEvent.FiveChain, 0f).Count != 2)
                return "선행 조건 실패: 두 명이 반응하지 않았다";

            PassengerReaction a = director.CurrentOf(0);
            PassengerReaction b = director.CurrentOf(1);

            if (a.Pose == b.Pose) return $"두 승객이 같은 자세({a.Pose})다 — 상반된 반응이 없다";
            if (a.Gaze == b.Gaze) return $"두 승객이 같은 곳({a.Gaze})을 본다";
            if (string.Equals(a.Line, b.Line)) return $"두 승객이 같은 말(\"{a.Line}\")을 한다";
            if (string.Equals(a.VoiceCue, b.VoiceCue)) return $"두 승객이 같은 소리({a.VoiceCue})를 낸다";

            // **타이밍은 같아야 한다.** 다르면 중재기의 세 축이 사람마다 갈린다.
            if (a.Priority != b.Priority) return $"우선순위가 {a.Priority} vs {b.Priority}";
            if (Math.Abs(a.Duration - b.Duration) > 0.0001f)
                return $"지속이 {a.Duration} vs {b.Duration}";
            if (Math.Abs(a.Cooldown - b.Cooldown) > 0.0001f)
                return $"쿨다운이 {a.Cooldown} vs {b.Cooldown}";

            if (director.ContrastCount != 1) return $"대조 반응 {director.ContrastCount}회, 기대 1회";
            if (director.PoseKindCount < 2) return $"관측된 자세가 {director.PoseKindCount}종";
            if (director.GazeKindCount < 2) return $"관측된 시선이 {director.GazeKindCount}종";
            if (director.LineCount != 2) return $"대사가 실린 반응 {director.LineCount}건, 기대 2건";
            if (director.VoiceCueCount != 2) return $"음성 큐가 실린 반응 {director.VoiceCueCount}건";

            // 결정론 — 같은 순서로 다시 돌리면 같은 사람이 같은 쪽을 받아야 한다.
            var again = new PassengerReactionDirector(
                2, 2, PassengerReactionSet.DefaultFor, PassengerReactionSet.DefaultContrastFor);
            again.Notify(PassengerReactionEvent.FiveChain, 0f);
            if (again.CurrentOf(0).Pose != a.Pose || again.CurrentOf(1).Pose != b.Pose)
                return "같은 입력에서 대조 배정이 달라졌다 — 고정 캡처가 흔들린다";
            return null;
        }

        /// <summary>
        /// UP-NPC-05는 이미 VERIFIED다. 대조 반응이 그 세 축(최대 수·쿨다운·우선순위)을
        /// 건드리지 않는다는 것을 **같은 시나리오로** 다시 확인한다 — 규칙 코드가 아니라
        /// 규칙의 입력이 바뀐 변경이므로, 통과하던 검사가 그대로 통과해야 한다.
        /// </summary>
        private static string TestContrastKeepsArbitration()
        {
            // 최대 수
            var byMax = new PassengerReactionDirector(5, 2, Fixed(2f, 4f), FixedContrast());
            int reacted = byMax.Notify(PassengerReactionEvent.FiveChain, 0f).Count;
            if (reacted != 2) return $"승객 5명 / 한도 2인데 {reacted}명이 반응했다";
            byMax.Notify(PassengerReactionEvent.Threshold170, 0f);
            if (byMax.ActiveCount > 2) return $"두 번째 사건 뒤 ActiveCount {byMax.ActiveCount}";

            // 쿨다운
            var byCooldown = new PassengerReactionDirector(2, 2, Fixed(1f, 5f), FixedContrast());
            if (byCooldown.Notify(PassengerReactionEvent.FiveChain, 0f).Count != 2)
                return "선행 조건 실패: 두 명이 반응하지 않았다";
            byCooldown.Tick(1.5f);
            if (byCooldown.ActiveCount != 0) return $"만료 후 ActiveCount {byCooldown.ActiveCount}";
            if (byCooldown.Notify(PassengerReactionEvent.FiveChain, 2f).Count != 0)
                return "쿨다운 안인데 다시 반응했다 — 대조가 타이밍을 물려받지 않았다";
            if (byCooldown.Notify(PassengerReactionEvent.FiveChain, 6f).Count != 2)
                return "쿨다운이 끝났는데도 반응하지 않았다";

            // 우선순위
            var byPriority = new PassengerReactionDirector(2, 2, Fixed(10f, 10f), FixedContrast());
            if (byPriority.Notify(PassengerReactionEvent.BasicPurify, 0f).Count != 2)
                return "선행 조건 실패: 낮은 우선순위 반응이 시작되지 않았다";
            if (byPriority.Notify(PassengerReactionEvent.CollapseImminent, 1f).Count != 2)
                return "높은 우선순위가 덮지 못했다 — 대조가 우선순위를 갈아치웠다";
            if (byPriority.Notify(PassengerReactionEvent.BasicPurify, 2f).Count != 0)
                return "낮은 우선순위가 높은 반응을 덮었다";
            if (byPriority.Notify(PassengerReactionEvent.CollapseImminent, 3f).Count != 0)
                return "같은 우선순위가 진행 중인 반응을 덮었다";
            return null;
        }

        private static string TestNoContrastMeansUniform()
        {
            // 대조 조회기 없음 — 기존 생성자와 같은 경로다.
            var director = new PassengerReactionDirector(2, 2, PassengerReactionSet.DefaultFor);
            if (director.Notify(PassengerReactionEvent.FiveChain, 0f).Count != 2)
                return "선행 조건 실패";
            if (director.CurrentOf(0).Pose != director.CurrentOf(1).Pose)
                return "대조가 없는데 자세가 갈렸다";
            if (director.ContrastCount != 0) return $"대조 반응 {director.ContrastCount}회";

            // 정의되지 않은 대조도 같다 — 옛 `.asset` 이 그 상태로 읽힌다.
            var undefined = new PassengerReactionDirector(2, 2, PassengerReactionSet.DefaultFor,
                _ => default(PassengerReactionContrast));
            if (undefined.Notify(PassengerReactionEvent.FiveChain, 0f).Count != 2)
                return "선행 조건 실패(빈 대조)";
            if (undefined.CurrentOf(0).Pose != undefined.CurrentOf(1).Pose)
                return "빈 대조가 자세를 바꿨다";
            // 적용되지 않은 대조는 세지 않는다. 세면 `ContrastCount` 로는
            // "상반된 반응이 실제로 나왔는가"를 물을 수 없다.
            if (undefined.ContrastCount != 0)
                return $"적용되지 않은 대조가 {undefined.ContrastCount}회로 세졌다";
            return null;
        }

        private static string TestSetResetFillsContrasts()
        {
            var set = ScriptableObject.CreateInstance<PassengerReactionSet>();
            try
            {
                set.Reset();
                IReadOnlyList<PassengerReactionEvent> all = PassengerReactionEvents.All;
                for (int i = 0; i < all.Count; i++)
                {
                    if (!set.HasContrastEntry(all[i]))
                        return $"{all[i].DisplayName()} 의 대조 반응이 에셋에 없다";

                    PassengerReaction primary = set.For(all[i]);
                    PassengerReaction other = set.ContrastFor(all[i]).ApplyTo(in primary);
                    if (other.Pose == primary.Pose && other.Gaze == primary.Gaze)
                        return $"{all[i].DisplayName()} 의 대조가 대표와 같은 자세·시선이다";
                    if (!other.HasLine) return $"{all[i].DisplayName()} 의 대조에 대사가 없다";
                }
                return null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(set);
            }
        }

        /// <summary>
        /// `PassengerReactionSet.asset`은 `Line`·`Contrast`가 생기기 전에 직렬화됐다.
        /// 그 상태의 항목이 대사를 잃지 않는다는 것을 확인한다 — 잃으면 §9.3의 채널
        /// 하나가 데이터가 있는데도 화면에서 통째로 사라진다.
        /// </summary>
        private static string TestLegacyEntryFallsBackForLine()
        {
            var set = ScriptableObject.CreateInstance<PassengerReactionSet>();
            try
            {
                set.Reset();

                // `Entries`는 내부 배열을 그대로 돌려준다. 옛 직렬화 상태(대사 없음 ·
                // 대조 없음)를 만들 다른 통로가 없어서 여기로 들어간다. 이 캐스트가
                // 언젠가 사본을 돌려주게 바뀌면 아래 "우선순위가 보인다" 검사가 먼저 깨져
                // **검사 자체가 무력화된 것을 알려 준다.**
                var entries = set.Entries as PassengerReactionSet.Entry[];
                if (entries == null) return "Entries 가 내부 배열이 아니다 — 이 검사가 성립하지 않는다";

                int index = -1;
                for (int i = 0; i < entries.Length; i++)
                    if (entries[i].Event == PassengerReactionEvent.FiveChain) { index = i; break; }
                if (index < 0) return "5연쇄 항목이 없다";

                entries[index].Reaction.Line = null;
                entries[index].Reaction.Priority = 41;          // 변경이 실제로 닿았는지 보는 표식
                entries[index].Contrast = default(PassengerReactionContrast);

                PassengerReaction got = set.For(PassengerReactionEvent.FiveChain);
                if (got.Priority != 41)
                    return "에셋 수정이 조회에 반영되지 않았다 — 이 검사가 무력화됐다";

                string expected = PassengerReactionSet.DefaultFor(PassengerReactionEvent.FiveChain).Line;
                if (!string.Equals(got.Line, expected))
                    return $"빈 대사가 폴백되지 않았다: \"{got.Line}\", 기대 \"{expected}\"";

                PassengerReactionContrast contrast = set.ContrastFor(PassengerReactionEvent.FiveChain);
                if (!contrast.IsDefined) return "빈 대조가 코드 기본값으로 폴백되지 않았다";
                if (contrast.Pose !=
                    PassengerReactionSet.DefaultContrastFor(PassengerReactionEvent.FiveChain).Pose)
                    return "대조 폴백이 코드 기본값과 다르다";

                // 음성 큐에는 폴백이 없다 — "비면 소리 없음"이 이미 출하된 계약이다.
                entries[index].Reaction.VoiceCue = null;
                if (set.For(PassengerReactionEvent.FiveChain).HasVoice)
                    return "빈 음성 큐가 폴백됐다 — 데이터로 소리를 끌 수 없게 된다";
                return null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(set);
            }
        }

        // ── 배선 ─────────────────────────────────────────────────────────────

        private static string TestRouterBridgesBus()
        {
            var bus = new GameEventBus();
            float now = 0f;
            var director = new PassengerReactionDirector(3, 2, Fixed(1f, 1f));
            var router = new PassengerReactionRouter(director, () => now);

            int callbacks = 0;
            router.Reacted += (reactionEvent, reacting) => callbacks += reacting.Count;
            router.Attach(bus);

            bus.Publish(GameEventKind.ContractSelected, 1, -1, 0, 0f, "테스트 계약");
            if (router.RoutedCount != 1) return $"RoutedCount {router.RoutedCount}";
            if (router.LastRouted != PassengerReactionEvent.ContractChosen)
                return $"LastRouted {router.LastRouted}";
            if (director.ActiveCount != 2) return $"ActiveCount {director.ActiveCount}";
            if (callbacks != 2) return $"콜백이 받은 인원 {callbacks}";

            // 반응 대상이 아닌 사건은 세지 않는다.
            bus.Publish(GameEventKind.ColumnRevealed, 1, 0, 2);
            if (router.RoutedCount != 1) return $"무관한 사건이 세졌다: {router.RoutedCount}";

            router.Detach();
            now = 100f;
            bus.Publish(GameEventKind.CollapseBegan, 1, 0);
            if (router.RoutedCount != 1) return "Detach 이후에도 사건이 흘렀다";

            // 라우터가 던진 예외를 버스가 삼키고 있으면 위 검사가 통과해도 의미가 없다.
            if (bus.ErrorCount != 0) return $"구독자 예외 {bus.ErrorCount}건: {bus.LastError}";
            return null;
        }

        private static string TestRouterDoesNotDoubleSubscribe()
        {
            var bus = new GameEventBus();
            var director = new PassengerReactionDirector(4, 2, Fixed(5f, 5f));
            var router = new PassengerReactionRouter(director, () => 0f);

            router.Attach(bus);
            router.Attach(bus);
            bus.Publish(GameEventKind.CollapseBegan, 2, 0);

            if (router.RoutedCount != 1) return $"한 사건이 {router.RoutedCount}번 들어왔다";
            if (director.ActiveCount != 2) return $"ActiveCount {director.ActiveCount}";
            if (bus.ErrorCount != 0) return $"구독자 예외 {bus.ErrorCount}건: {bus.LastError}";

            router.Detach();
            return null;
        }
    }
}
