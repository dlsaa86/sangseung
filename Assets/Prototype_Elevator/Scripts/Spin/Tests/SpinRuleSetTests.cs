using System;
using System.Globalization;
using System.Text;
using Ascend.Prototype.Build;
using Ascend.Prototype.Run;

namespace Ascend.Prototype.Spin.Tests
{
    /// <summary>
    /// 규칙 다발의 **방어선**을 지키는 검증 모음. `SpinEngineTests`가 판정 규칙을 지킨다면
    /// 여기는 "규칙이 망가진 채로 굴러가지 않는가"를 지킨다.
    ///
    /// 두 백로그 항목이 같은 결함을 공유해서 한 파일에 둔다 —
    /// **구현은 있는데 그것을 검증하는 테스트가 없다.**
    ///   · `UP-CORE-14` 가중치 합이 0이면 명시적 오류 (N08 §7.2 마지막 문장)
    ///   · `UP-BUILD-09` 발동 순서가 고정되고 로그로 추적된다 (N02 「발동 순서」, N08 §8.1)
    ///
    /// NUnit에 의존하지 않는 이유는 `DECISION_LOG.md` D-20260730-06 참조.
    /// </summary>
    public static class SpinRuleSetTests
    {
        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0;
            int failed = 0;
            var report = new StringBuilder();

            // ── UP-CORE-14 · 가중치 합 0 방어 ──
            Run("가중치 총합 0 → InvalidOperationException", TestZeroWeightThrows, ref passed, ref failed, report);
            Run("예외 메시지가 원인을 말한다", TestZeroWeightMessageNamesCause, ref passed, ref failed, report);
            Run("양수 가중치가 하나면 그 심볼만 나온다", TestSinglePositiveWeightWorks, ref passed, ref failed, report);
            Run("음수 가중치는 0으로 취급되고 예외를 만들지 않는다", TestNegativeWeightIsClampedNotFatal, ref passed, ref failed, report);
            Run("전부 음수면 총합 0으로 보고 예외", TestAllNegativeWeightsThrow, ref passed, ref failed, report);
            Run("계약이 가중치를 0으로 만들면 같은 예외", TestContractZeroingWeightThrows, ref passed, ref failed, report);
            Run("잔류 가산이 가중치를 0으로 깎아도 같은 예외", TestResidualZeroingWeightThrows, ref passed, ref failed, report);
            Run("재충전 경로에서도 같은 예외 (보정이 구제하지 않는다)", TestRefillPathThrowsToo, ref passed, ref failed, report);

            // ── UP-BUILD-09 · 발동 순서와 추적성 ──
            Run("세션 규칙이 문서화된 발동 순서의 결과와 일치", TestActivationOrderMatchesPipeline, ref passed, ref failed, report);
            Run("순서 뒤집기가 값을 가르지 못함 (카나리아)", TestReversedOrderStillIndistinguishable, ref passed, ref failed, report);
            Run("발동이 Kind·Pattern·PatternMultiplier 로 추적된다", TestPurifyEventCarriesActivation, ref passed, ref failed, report);
            Run("DescribeCascade 가 단계·발동을 순서대로 담는다", TestDescribeCascadeIsOrdered, ref passed, ref failed, report);
            Run("연쇄 단계가 1부터 오름차순이고 배수가 깊이를 따른다", TestCascadeStepsAreOrdered, ref passed, ref failed, report);

            report.Insert(0, "[상승] === Spin Rule Set Guards ===\n");
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

        // ────────────────────────────────────────────────────────────────────
        // UP-CORE-14 — 가중치 합이 0이면 명시적 오류
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 가드가 없으면 무슨 일이 일어나는가가 이 테스트의 존재 이유다.
        ///
        /// `DrawSymbol`의 `total <= 0f` 검사를 지우면 NaN도 예외도 나오지 않는다.
        /// `roll = NextDouble() * 0 = 0` 이 되어 두 비교를 모두 통과하고 **마지막 분기인
        /// 증식체가 9칸 나온다.** 터지는 것보다 그럴듯한 판이 나오는 쪽이 훨씬 위험하다 —
        /// 밸런스가 이상하다는 신고를 받고 나서야 풀이 죽어 있었다는 것을 알게 된다.
        /// </summary>
        private static string TestZeroWeightThrows()
        {
            SpinRuleSet dead = AllZeroWeights();
            try
            {
                SpinResolution leaked = SpinOnce(dead, 1);
                return $"가중치 총합 0인데 예외 없이 판이 나왔다 — {leaked.InitialBoard}";
            }
            catch (Exception exception)
            {
                if (!(exception is InvalidOperationException))
                    return $"예외 종류 {exception.GetType().Name} — InvalidOperationException 이어야 한다";
            }
            return null;
        }

        /// <summary>
        /// 빈 메시지는 조용한 실패와 다르지 않다. 스택만 보고 "어느 규칙 다발이 왜 죽었는가"를
        /// 알 수 없으면 로그를 받은 사람이 재현부터 다시 해야 한다.
        /// 문구 전체를 고정하지는 않는다 — 한국어로 다시 써도 통과해야 한다.
        /// </summary>
        private static string TestZeroWeightMessageNamesCause()
        {
            SpinRuleSet dead = AllZeroWeights();
            try
            {
                SpinOnce(dead, 2);
                return "예외가 나지 않아 메시지를 검사할 수 없다";
            }
            catch (InvalidOperationException exception)
            {
                string message = exception.Message;
                if (string.IsNullOrWhiteSpace(message)) return "예외 메시지가 비어 있다";
                if (message.Length < 10) return $"메시지가 원인을 담기에 너무 짧다: \"{message}\"";

                bool namesWeight =
                    message.IndexOf("weight", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("가중치", StringComparison.Ordinal) >= 0;
                if (!namesWeight) return $"메시지가 가중치를 지목하지 않는다: \"{message}\"";
            }
            return null;
        }

        /// <summary>
        /// 방어선의 반대편. "합이 0이면 죽는다"가 "하나라도 살아 있으면 산다"와 짝을 이루지
        /// 않으면 그 가드는 그냥 과민한 검사다. 1층 풀처럼 종류가 줄어든 층이 실제로 여기 걸린다.
        /// </summary>
        private static string TestSinglePositiveWeightWorks()
        {
            foreach (SymbolKind only in AllKinds)
            {
                SpinRuleSet rules = SpinRuleSet.CreateDefault();
                // 9칸 전부 같은 저항이면 잭팟→재충전이 열린다. 깊이를 1로 묶어 두는 것은
                // 이 테스트가 보려는 것이 캐스케이드가 아니라 최초 추첨이기 때문이다.
                rules.MaxCascadeDepth = 1;
                foreach (SymbolKind kind in AllKinds) rules.Weights[kind] = 0f;
                rules.Weights[only] = 3f;

                for (int seed = 1; seed <= 20; seed++)
                {
                    SpinBoard board = SpinOnce(rules, seed).InitialBoard;
                    if (board.CountOf(only) != SpinBoard.Cells)
                        return $"{only.DisplayName()} 만 양수인데 시드 {seed} 판이 {board}";
                }
            }
            return null;
        }

        /// <summary>
        /// `WeightOf`가 음수를 0으로 읽는다. 그 규칙이 없으면 음수 가중치가 `total`을 깎아
        /// 다른 종류의 확률을 조용히 부풀린다 — 최악의 경우 total 이 양수인 채로 남아
        /// 가드도 통과한다. 여기서 검사하는 것은 "음수는 없는 것과 같다"이지 "음수는 오류다"가 아니다.
        /// </summary>
        private static string TestNegativeWeightIsClampedNotFatal()
        {
            SpinRuleSet rules = SpinRuleSet.CreateDefault();
            rules.MaxCascadeDepth = 1;
            rules.Weights[SymbolKind.NormalSoul]   = -5f;
            rules.Weights[SymbolKind.Absorber]     = 2f;
            rules.Weights[SymbolKind.Proliferator] = 0f;

            if (rules.WeightOf(SymbolKind.NormalSoul) != 0f)
                return $"음수 가중치를 {rules.WeightOf(SymbolKind.NormalSoul)} 로 읽는다";

            for (int seed = 1; seed <= 20; seed++)
            {
                SpinBoard board = SpinOnce(rules, seed).InitialBoard;
                if (board.CountOf(SymbolKind.Absorber) != SpinBoard.Cells)
                    return $"시드 {seed}: 음수 가중치 종류가 섞여 나왔다 — {board}";
            }
            return null;
        }

        /// <summary>
        /// 전부 음수는 "합이 음수"가 아니라 "합이 0"으로 읽혀야 한다. 음수 총합이 그대로
        /// 흘러가면 `roll` 이 음수가 되어 첫 비교를 통과하고 정상 영혼이 9칸 나온다.
        /// </summary>
        private static string TestAllNegativeWeightsThrow()
        {
            SpinRuleSet rules = SpinRuleSet.CreateDefault();
            rules.Weights[SymbolKind.NormalSoul]   = -5f;
            rules.Weights[SymbolKind.Absorber]     = -2f;
            rules.Weights[SymbolKind.Proliferator] = -1f;

            try
            {
                SpinResolution leaked = SpinOnce(rules, 3);
                return $"전부 음수인데 판이 나왔다 — {leaked.InitialBoard}";
            }
            catch (InvalidOperationException) { }
            return null;
        }

        /// <summary>
        /// 계약은 대상의 출현 가중치에 **곱한다**. 출현 배수가 0인 계약은 그 저항을 판에서
        /// 지우는 것과 같고, 그것이 유일하게 살아 있던 가중치였다면 풀이 통째로 죽는다.
        /// 계약을 적용하는 쪽(호출자)이 만든 상태라도 엔진은 같은 방어선을 세워야 한다 —
        /// 계약 데이터는 밸런스 조정으로 계속 바뀌는 값이기 때문이다.
        /// </summary>
        private static string TestContractZeroingWeightThrows()
        {
            SpinRuleSet rules = SpinRuleSet.CreateDefault();
            foreach (SymbolKind kind in AllKinds) rules.Weights[kind] = 0f;
            rules.Weights[SymbolKind.Absorber] = 4f;

            // 계약 적용 전에는 멀쩡히 돈다는 것을 먼저 확인한다. 이게 없으면 아래 예외가
            // 계약 때문인지 원래 죽어 있었기 때문인지 구분되지 않는다.
            SpinBoard before = SpinOnce(Clone(rules, 1), 4).InitialBoard;
            if (before.CountOf(SymbolKind.Absorber) != SpinBoard.Cells)
                return $"계약 전 대조군이 이미 이상하다 — {before}";

            var killer = new ResistanceContract
            {
                Target                    = SymbolKind.Absorber,
                Label                     = "출현 0 계약(테스트 전용)",
                AppearanceMultiplier      = 0f,
                PurifyRewardMultiplier    = 1f,
                PatternBonusAdd           = 0f,
                ResidualPenaltyMultiplier = 1f,
            };
            rules.Apply(in killer);
            if (rules.WeightOf(SymbolKind.Absorber) != 0f)
                return $"계약 적용 뒤 가중치가 {rules.WeightOf(SymbolKind.Absorber)} — 0 이어야 한다";

            try
            {
                SpinResolution leaked = SpinOnce(rules, 5);
                return $"계약이 풀을 죽였는데 판이 나왔다 — {leaked.InitialBoard}";
            }
            catch (InvalidOperationException) { }
            return null;
        }

        /// <summary>
        /// 잔류 증식체 가산은 스핀 사이에 유일하게 살아남는 상태다. 음수 가산을 주는 효과가
        /// 생기면 `PrepareRules`의 `Math.Max(0f, ...)` 가 가중치를 0으로 눌러 버릴 수 있고,
        /// 그것이 마지막 양수였다면 다음 스핀이 죽는다. 클램프가 음수를 만들지 않는다는 것과
        /// 그 결과가 조용히 넘어가지 않는다는 것을 함께 본다.
        /// </summary>
        private static string TestResidualZeroingWeightThrows()
        {
            SpinRuleSet rules = SpinRuleSet.CreateDefault();
            rules.MaxCascadeDepth = 1;
            foreach (SymbolKind kind in AllKinds) rules.Weights[kind] = 0f;
            rules.Weights[SymbolKind.Proliferator] = 2f;

            // 일부만 깎이면 살아남아야 한다 — 클램프가 과하게 0으로 밀지 않는다.
            var trimmed = new ResidualState { ProliferatorCount = 1, NextProliferatorWeightAdd = -1f };
            SpinBoard survived = new SpinEngine(0)
                .SpinWithSeed(6, rules, ResistanceContract.None, trimmed).InitialBoard;
            if (survived.CountOf(SymbolKind.Proliferator) != SpinBoard.Cells)
                return $"가중치가 1 남았는데 판이 {survived}";

            var wipe = new ResidualState { ProliferatorCount = 1, NextProliferatorWeightAdd = -10f };
            try
            {
                SpinResolution leaked = new SpinEngine(0)
                    .SpinWithSeed(7, rules, ResistanceContract.None, wipe);
                return $"잔류 가산이 풀을 죽였는데 판이 나왔다 — {leaked.InitialBoard}";
            }
            catch (InvalidOperationException) { }
            return null;
        }

        /// <summary>
        /// 최초 추첨만 막으면 절반이다. 캐스케이드 재충전(`FillEmptyCells`)도 같은 추첨기를
        /// 쓰므로 같은 방어선이 서야 한다. `RefillNormalSoulBias` 는 총합이 0이면 조기 반환하므로
        /// **보정이 없는 가중치를 만들어 내지 않는다** — 그 사실도 함께 고정한다.
        /// </summary>
        private static string TestRefillPathThrowsToo()
        {
            SpinRuleSet rules = AllZeroWeights();
            rules.MaxCascadeDepth = 2;
            rules.RefillNormalSoulBias = 0.5f;   // 보정이 총합 0을 구제하면 여기서 통과해 버린다

            // 9칸 동일 저항 → 잭팟 → 전 칸 제거 → 재충전. 판을 직접 주므로 최초 추첨은 건너뛴다.
            SpinBoard jackpot = Board(
                SymbolKind.Absorber, SymbolKind.Absorber, SymbolKind.Absorber,
                SymbolKind.Absorber, SymbolKind.Absorber, SymbolKind.Absorber,
                SymbolKind.Absorber, SymbolKind.Absorber, SymbolKind.Absorber);

            try
            {
                SpinResolution leaked = new SpinEngine(0)
                    .ResolveBoard(rules, ResistanceContract.None, ResidualState.Empty, jackpot);
                return $"재충전이 가중치 없이 칸을 채웠다 — {leaked.FinalBoard}";
            }
            catch (InvalidOperationException) { }
            return null;
        }

        // ────────────────────────────────────────────────────────────────────
        // UP-BUILD-09 — 발동 순서가 고정되고 로그로 추적된다
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 순서를 만드는 곳은 `FloorSession.BuildRules` 하나뿐이다 —
        /// 기본값 → 층 규칙 → 계약 → 승객·부품(`SpinRuleSet` 주석, N02 「발동 순서」).
        /// 그래서 이 테스트는 규칙 다발을 직접 조립하지 않고 **실제 층 세션이 만든 것**을 읽는다.
        /// 테스트가 자기 손으로 조립한 값과 비교하면 순서가 아니라 자기 자신을 검증하게 된다.
        ///
        /// 잡히는 것: 계약 누락, 적재 누락, 계약 이중 적용, 층 규칙 건너뛰기.
        /// **잡히지 않는 것: 계약과 적재의 자리 바꿈.** 아래 카나리아 테스트에 이유를 적었다.
        /// </summary>
        private static string TestActivationOrderMatchesPipeline()
        {
            FloorPlan plan = OrderProbeFloor;
            BuildLoadout loadout = OrderProbeLoadout();
            if (loadout == null) return "카탈로그에서 검사용 적재를 만들지 못했다";

            int index = ContractIndex(plan, SymbolKind.Proliferator);
            if (index < 0) return $"{plan.Floor}층에 증식체 계약이 없다 — 이 테스트의 전제가 깨졌다";
            ResistanceContract contract = plan.ContractChoices[index];

            var session = new FloorSession(plan, new SpinEngine(1), PowerThresholds.Default,
                0f, ResidualState.Empty, 0f, 0f, loadout);
            if (!session.SelectContract(index)) return $"계약 확정 실패 (단계 {session.Phase})";
            if (session.Rules == null) return "계약을 확정했는데 규칙 다발이 없다";

            SpinRuleSet documented = PrototypeCurriculum.BuildRules(in plan);
            documented.Apply(in contract);
            loadout.ApplyTo(documented);

            string actual = Describe(session.Rules);
            if (actual != Describe(documented))
                return $"세션 규칙이 문서화된 순서의 결과와 다르다\n    실제 {actual}\n    기대 {Describe(documented)}";

            // 아래 세 대조군이 없으면 위 비교는 "두 단계가 다 빠져도" 통과할 수 있다.
            SpinRuleSet contractOnly = PrototypeCurriculum.BuildRules(in plan);
            contractOnly.Apply(in contract);
            if (actual == Describe(contractOnly)) return "적재 효과가 규칙에 반영되지 않았다";

            SpinRuleSet loadoutOnly = PrototypeCurriculum.BuildRules(in plan);
            loadout.ApplyTo(loadoutOnly);
            if (actual == Describe(loadoutOnly)) return "계약 효과가 규칙에 반영되지 않았다";

            SpinRuleSet twice = PrototypeCurriculum.BuildRules(in plan);
            twice.Apply(in contract);
            twice.Apply(in contract);
            loadout.ApplyTo(twice);
            if (actual == Describe(twice)) return "계약이 두 번 얹혔다";
            return null;
        }

        /// <summary>
        /// **이 테스트는 순서를 검증하지 않는다. 순서를 검증할 수 없다는 사실을 고정한다.**
        ///
        /// 지금 계약과 적재가 같은 필드에 하는 일은 전부 같은 종류다 —
        /// 출현 가중치·정화 보상·잔류 대가는 양쪽 다 곱셈이고, 패턴 배수는 양쪽 다 덧셈이다.
        /// 곱셈끼리·덧셈끼리는 교환법칙이 성립하므로 순서를 뒤집어도 숫자가 한 자리도 달라지지
        /// 않는다. 그래서 "승객이 계약 뒤에 온다"를 값으로 잡아내는 조합이 **존재하지 않는다**.
        ///
        /// 억지로 통과하는 순서 테스트를 쓰는 대신 그 전제를 여기 못박는다. 누군가 비대칭 효과
        /// (같은 필드에 한쪽은 곱하고 한쪽은 더하는 종류)를 추가하면 이 테스트가 실패하고,
        /// 그때가 진짜 순서 테스트를 쓸 수 있게 되는 시점이다.
        /// </summary>
        private static string TestReversedOrderStillIndistinguishable()
        {
            FloorPlan plan = OrderProbeFloor;
            BuildLoadout loadout = OrderProbeLoadout();
            if (loadout == null) return "카탈로그에서 검사용 적재를 만들지 못했다";

            int index = ContractIndex(plan, SymbolKind.Proliferator);
            if (index < 0) return $"{plan.Floor}층에 증식체 계약이 없다 — 이 테스트의 전제가 깨졌다";
            ResistanceContract contract = plan.ContractChoices[index];

            SpinRuleSet documented = PrototypeCurriculum.BuildRules(in plan);
            documented.Apply(in contract);
            loadout.ApplyTo(documented);

            SpinRuleSet reversed = PrototypeCurriculum.BuildRules(in plan);
            loadout.ApplyTo(reversed);
            reversed.Apply(in contract);

            if (Describe(documented) != Describe(reversed))
                return "순서를 뒤집으니 값이 갈렸다 — 이제 발동 순서를 숫자로 검증할 수 있다. " +
                       "이 카나리아를 지우고 실제 순서 테스트로 교체하라.\n" +
                       $"    문서 순서 {Describe(documented)}\n    뒤집은 순서 {Describe(reversed)}";
            return null;
        }

        /// <summary>
        /// "로그로 추적된다"가 뜻하는 것: 한 발동을 보고 **어느 저항이 어떤 모양으로 터졌고
        /// 그 배수가 어디서 왔는지**까지 되짚을 수 있어야 한다.
        ///
        /// `PatternMultiplier` 가 장식용 필드가 아니라는 것은 전력 식으로만 증명된다 —
        /// 보고된 배수를 그대로 곱해 `Power` 가 재현되지 않으면, 화면에 찍히는 배수와 실제
        /// 계산에 쓰인 배수가 다른 것이다. `TECH_SPEC.md` §5 "UI와 연출은 SpinResult를 소비한다".
        /// </summary>
        private static string TestPurifyEventCarriesActivation()
        {
            FloorPlan plan = OrderProbeFloor;
            var loadout = new BuildLoadout();
            BuildItem zealot = BuildCatalog.ById(ZealotId);          // 증식체 패턴 배수 가산
            BuildItem surveyor = BuildCatalog.ById(LineSurveyorId);  // 직선 배수 가산
            if (zealot == null || surveyor == null) return "카탈로그에 검사용 승객이 없다";
            if (!loadout.Add(zealot) || !loadout.Add(surveyor)) return "검사용 승객 적재 실패";

            int index = ContractIndex(plan, SymbolKind.Proliferator);
            if (index < 0) return $"{plan.Floor}층에 증식체 계약이 없다";
            ResistanceContract contract = plan.ContractChoices[index];

            var session = new FloorSession(plan, new SpinEngine(1), PowerThresholds.Default,
                0f, ResidualState.Empty, 0f, 0f, loadout);
            if (!session.SelectContract(index)) return "계약 확정 실패";
            SpinRuleSet rules = session.Rules;

            // 배수를 출처별로 갈라 세운다. 합만 맞추면 계약이 빠지고 승객이 두 배로 들어가도 통과한다.
            SpinRuleSet bare = PrototypeCurriculum.BuildRules(in plan);
            float expected = (bare.LineMultiplier + AmountOf(surveyor, BuildEffectKind.LineMultiplier)) +
                             (contract.PatternBonusAdd + AmountOf(zealot, BuildEffectKind.PatternBonus));
            float bareMultiplier = bare.PatternMultiplierFor(PatternKind.Line, SymbolKind.Proliferator);
            if (expected <= bareMultiplier)
                return $"계약·적재가 배수를 올리지 못한다 (기대 {expected} ≤ 기본 {bareMultiplier}) — 이 조합으로는 아무것도 못 잡는다";

            // 증식체 3개가 첫 통관에 세로로 선다. 4칸이 아니므로 Cluster 가 아니라 Line 이고,
            // Line 은 재충전을 열지 않으므로 단계가 하나로 끝나 검사가 흔들리지 않는다.
            SpinBoard board = Board(
                SymbolKind.Proliferator, SymbolKind.Proliferator, SymbolKind.Proliferator,
                SymbolKind.NormalSoul,   SymbolKind.NormalSoul,   SymbolKind.NormalSoul,
                SymbolKind.NormalSoul,   SymbolKind.NormalSoul,   SymbolKind.NormalSoul);

            SpinResolution result = new SpinEngine(4242)
                .ResolveBoard(rules, contract, ResidualState.Empty, board);

            if (result.Steps == null || result.Steps.Length != 1)
                return $"단계 {result.ChainDepth}, 기대 1";
            CascadeStep step = result.Steps[0];
            if (step.Purifies == null || step.Purifies.Length != 1)
                return $"발동 {step.Purifies?.Length ?? -1}건, 기대 1";

            PurifyEvent purify = step.Purifies[0];
            if (purify.Kind != SymbolKind.Proliferator) return $"발동 종류 {purify.Kind}";
            if (purify.Pattern != PatternKind.Line) return $"발동 패턴 {purify.Pattern}";
            if (purify.Line != LineKind.Column) return $"직선 종류 {purify.Line}, 기대 Column";
            if (purify.PatternCells == null || purify.PatternCells.Length != 3)
                return $"모양 칸 {purify.PatternCells?.Length ?? -1}, 기대 3";
            if (purify.Cells == null || purify.Cells.Length != 3)
                return $"정화 칸 {purify.Cells?.Length ?? -1}, 기대 3";

            if (Math.Abs(purify.PatternMultiplier - expected) > 0.0001f)
                return $"보고된 배수 {purify.PatternMultiplier}, 기대 {expected}";

            // 보고된 배수가 실제로 쓰인 배수인가.
            float recomputed = purify.Cells.Length * rules.PurifyValuePerSymbol *
                               rules.PurifyRewardFor(SymbolKind.Proliferator) *
                               purify.PatternMultiplier * step.ChainMultiplier;
            if (Math.Abs(purify.Power - recomputed) > 0.01f)
                return $"전력 {purify.Power} 가 보고된 배수로 재현되지 않는다 (재계산 {recomputed})";
            return null;
        }

        /// <summary>
        /// `TECH_SPEC.md` §9가 요구하는 진단 — "시드, 초기 보드, 마지막 보드, 단계별 발동".
        /// 줄 수와 줄 순서까지 보는 이유는, 발동이 전부 들어 있어도 **순서가 섞이면**
        /// 무엇이 무엇을 불렀는지 되짚을 수 없기 때문이다.
        /// </summary>
        private static string TestDescribeCascadeIsOrdered()
        {
            SpinResolution result = TwoStepCascade();
            if (result.Steps.Length != 2) return $"단계 {result.Steps.Length}, 기대 2 — 보드 전제가 깨졌다";

            string text = result.DescribeCascade();
            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length != result.Steps.Length + 1)
                return $"줄 수 {lines.Length}, 기대 {result.Steps.Length + 1}";
            if (!lines[0].StartsWith("SPIN run=", StringComparison.Ordinal))
                return $"첫 줄이 재현 로그가 아니다: {lines[0]}";

            for (int i = 0; i < result.Steps.Length; i++)
            {
                CascadeStep step = result.Steps[i];
                string line = lines[i + 1];
                if (!line.Contains($"[{step.Depth}]"))
                    return $"{i + 1}번째 줄이 {step.Depth}단계가 아니다: {line}";
                if (step.Purifies == null) return $"{step.Depth}단계 발동 배열이 null";
                foreach (PurifyEvent purify in step.Purifies)
                {
                    if (!line.Contains(purify.Kind.DisplayName()))
                        return $"{step.Depth}단계 줄에 {purify.Kind.DisplayName()} 이 없다: {line}";
                    if (!line.Contains(purify.Pattern.DisplayName()))
                        return $"{step.Depth}단계 줄에 {purify.Pattern.DisplayName()} 이 없다: {line}";
                }
            }

            // 발동이 1단계에만 있고 2단계는 자연 종료다. 두 줄이 뒤바뀌면 위 루프가 잡는다.
            if (result.Steps[0].Purifies.Length == 0) return "1단계에 정화가 없다 — 보드 전제가 깨졌다";
            if (result.Steps[1].Purifies.Length != 0) return "2단계에 정화가 있다 — 종료 단계 전제가 깨졌다";
            return null;
        }

        /// <summary>
        /// 연쇄 배수는 깊이에서만 나온다(`1 + (depth-1) × step`). 단계 배열이 순서를 잃으면
        /// 같은 판에서 같은 전력이 나오지 않고, 재현 로그가 재현을 못 한다.
        /// </summary>
        private static string TestCascadeStepsAreOrdered()
        {
            SpinResolution result = TwoStepCascade();
            if (result.Steps.Length < 2) return $"단계 {result.Steps.Length}, 기대 2 이상";

            float step = SpinRuleSet.CreateDefault().CascadeMultiplierStep;
            for (int i = 0; i < result.Steps.Length; i++)
            {
                if (result.Steps[i].Depth != i + 1)
                    return $"{i}번째 단계의 깊이가 {result.Steps[i].Depth}, 기대 {i + 1}";
                float expected = 1f + i * step;
                if (Math.Abs(result.Steps[i].ChainMultiplier - expected) > 0.0001f)
                    return $"{i + 1}단계 배수 {result.Steps[i].ChainMultiplier}, 기대 {expected}";
            }
            if (result.Steps[1].ChainMultiplier <= result.Steps[0].ChainMultiplier)
                return "연쇄가 깊어졌는데 배수가 오르지 않는다";
            if (result.ChainDepth != result.Steps.Length)
                return $"ChainDepth {result.ChainDepth} 가 단계 수 {result.Steps.Length} 와 다르다";
            return null;
        }

        // ── 헬퍼 ────────────────────────────────────────────────────────────

        private const string ZealotId = "PSG_ZEALOT";
        private const string LineSurveyorId = "PSG_SURVEYOR_LINE";
        private const string DampenerId = "PRT_RESIDUAL_DAMPENER";

        private static readonly SymbolKind[] AllKinds =
        {
            SymbolKind.NormalSoul, SymbolKind.Absorber, SymbolKind.Proliferator,
        };

        /// <summary>
        /// 10층을 쓰는 이유: 계약이 강제라 선택 단계를 반드시 지나고, 적재 보상 층이 아니라
        /// 승차 단계가 끼어들지 않으며, 풀이 3종이라 증식체 계약이 실제로 물린다.
        /// </summary>
        private static FloorPlan OrderProbeFloor => PrototypeCurriculum.For(10);

        /// <summary>
        /// 계약이 건드리는 값(패턴 배수·출현)과 건드리지 않는 값(직선 배수·잔류 완화)을
        /// 함께 흔드는 조합. 한 종류만 실으면 "적재가 통째로 빠짐"만 잡히고
        /// "일부만 적용됨"은 빠져나간다.
        /// </summary>
        private static BuildLoadout OrderProbeLoadout()
        {
            var loadout = new BuildLoadout();
            foreach (string id in new[] { ZealotId, LineSurveyorId, DampenerId })
            {
                BuildItem item = BuildCatalog.ById(id);
                if (item == null || !loadout.Add(item)) return null;
            }
            return loadout;
        }

        private static int ContractIndex(FloorPlan plan, SymbolKind target)
        {
            if (plan.ContractChoices == null) return -1;
            for (int i = 0; i < plan.ContractChoices.Length; i++)
                if (plan.ContractChoices[i].Target == target) return i;
            return -1;
        }

        private static float AmountOf(BuildItem item, BuildEffectKind kind)
        {
            if (item == null || item.Effects == null) return 0f;
            for (int i = 0; i < item.Effects.Length; i++)
                if (item.Effects[i].Kind == kind) return item.Effects[i].Amount;
            return 0f;
        }

        /// <summary>
        /// 흡수체 4개가 2×2로 붙어 연결 붕괴 → 재충전 → 저항 없는 판에서 자연 종료.
        /// 재충전 가중치를 정상 영혼 하나로 묶어 두었으므로 시드와 무관하게 항상 2단계다.
        /// </summary>
        private static SpinResolution TwoStepCascade()
        {
            SpinRuleSet rules = SpinRuleSet.CreateDefault();
            rules.MaxCascadeDepth = 2;
            rules.Weights[SymbolKind.NormalSoul]   = 1f;
            rules.Weights[SymbolKind.Absorber]     = 0f;
            rules.Weights[SymbolKind.Proliferator] = 0f;

            SpinBoard board = Board(
                SymbolKind.Absorber,   SymbolKind.Absorber,   SymbolKind.NormalSoul,
                SymbolKind.Absorber,   SymbolKind.Absorber,   SymbolKind.NormalSoul,
                SymbolKind.NormalSoul, SymbolKind.NormalSoul, SymbolKind.NormalSoul);

            return new SpinEngine(1234)
                .ResolveBoard(rules, ResistanceContract.None, ResidualState.Empty, board);
        }

        private static SpinRuleSet AllZeroWeights()
        {
            SpinRuleSet rules = SpinRuleSet.CreateDefault();
            foreach (SymbolKind kind in AllKinds) rules.Weights[kind] = 0f;
            return rules;
        }

        private static SpinRuleSet Clone(SpinRuleSet rules, int maxDepth)
        {
            SpinRuleSet copy = rules.Clone();
            copy.MaxCascadeDepth = maxDepth;
            return copy;
        }

        private static SpinResolution SpinOnce(SpinRuleSet rules, int spinSeed)
            => new SpinEngine(0).SpinWithSeed(spinSeed, rules, ResistanceContract.None, ResidualState.Empty);

        private static SpinBoard Board(params SymbolKind[] cells) => SpinBoard.FromArray(cells);

        /// <summary>
        /// 규칙 다발 전체를 한 줄로 정규화한다. 필드를 하나씩 비교하면 새 필드가 생겼을 때
        /// 조용히 검사에서 빠지고, 실패 메시지도 "어느 값이 다른가"를 못 알려준다.
        /// 소수 다섯 자리에서 끊는 이유는 곱셈 순서가 만드는 ULP 차이로 흔들리지 않기 위해서다.
        /// </summary>
        private static string Describe(SpinRuleSet rules)
        {
            var sb = new StringBuilder(320);
            sb.Append("W[");
            foreach (SymbolKind kind in AllKinds) sb.Append($"{(int)kind}:{N(rules.WeightOf(kind))} ");
            sb.Append("] PR[");
            foreach (SymbolKind kind in SymbolKinds.ResistanceKinds) sb.Append($"{(int)kind}:{N(rules.PurifyRewardFor(kind))} ");
            sb.Append("] PB[");
            foreach (SymbolKind kind in SymbolKinds.ResistanceKinds) sb.Append($"{(int)kind}:{N(rules.PatternBonusFor(kind))} ");
            sb.Append("] RP[");
            foreach (SymbolKind kind in SymbolKinds.ResistanceKinds) sb.Append($"{(int)kind}:{N(rules.ResidualPenaltyFor(kind))} ");
            sb.Append("] MIN[");
            foreach (SymbolKind kind in SymbolKinds.ResistanceKinds) sb.Append($"{(int)kind}:{rules.MinimumCountFor(kind)} ");
            sb.Append($"] soul={N(rules.NormalSoulValue)} guard={rules.GuaranteedNormalSouls}");
            sb.Append($" purify={N(rules.PurifyValuePerSymbol)} line={N(rules.LineMultiplier)}");
            sb.Append($" cluster={N(rules.ClusterMultiplier)} full={N(rules.FullBoardMultiplier)}");
            sb.Append($" cascade={N(rules.CascadeMultiplierStep)} depth={rules.MaxCascadeDepth}");
            sb.Append($" refill={N(rules.RefillNormalSoulBias)} mitigation={N(rules.ResidualMitigation)}");
            sb.Append($" absorberLoss={N(rules.AbsorberResidualPowerLoss)}");
            sb.Append($" prolifAdd={N(rules.ProliferatorResidualWeightAdd)}");
            sb.Append($" diagonal={rules.DiagonalCountsAsConnected} multi={rules.AllowMultiplePatternsPerKind}");
            sb.Append($" adjacency={rules.RequireAdjacencyToPurify} patternCells={rules.PurifyOnlyPatternCells}");
            return sb.ToString();
        }

        private static string N(float value) => value.ToString("0.#####", CultureInfo.InvariantCulture);
    }
}

// 씬 배선 필요: 없음. 순수 C# 헤드리스 테스트다.
//
// 배선 필요(코드): `Assets/Editor/PrototypeSelfTest.cs` 에 아래 한 줄을 추가해야
// 커밋 게이트와 `Ascend/Run Self Tests` 가 이 묶음을 함께 센다. 그 파일은 다른
// 에이전트의 소유라 여기서 고치지 않았다.
//
//   FoldInSuite("규칙 다발 방어선", Ascend.Prototype.Spin.Tests.SpinRuleSetTests.RunAll());
//
// 같은 이유로 `Scripts/Spin/Tests/Editor/SpinTestMenu.cs` 도 손대지 않았다 —
// 메뉴에서 단독 실행하려면 그쪽에 호출을 하나 더 붙이면 된다.
