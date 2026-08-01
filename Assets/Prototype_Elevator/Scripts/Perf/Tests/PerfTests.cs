using System;
using System.Text;

namespace Ascend.Prototype.Perf.Tests
{
    /// <summary>
    /// 성능 기반 부품의 헤드리스 검증 — UP-TECH-06(오브젝트 풀링)과
    /// UP-TECH-08(메모리 누적 판정) 중 씬 없이 돌릴 수 있는 부분.
    ///
    /// MonoBehaviour(<c>RenderBudgetProbe</c>, <c>MemoryTrendProbe</c>)는 여기서 검사하지
    /// 않는다. 대신 판정 규칙을 <see cref="MemoryTrend"/> 로 떼어내 두었고, 그 규칙이
    /// 이 스위트의 검증 대상이다 — "메모리 누적 없음"이라는 문장이 무엇을 뜻하는지가
    /// 씬 없이 검증되어야 하는 부분이기 때문이다.
    ///
    /// NUnit 에 의존하지 않는 이유는 `DECISION_LOG.md` D-20260730-06 참조.
    /// </summary>
    public static class PerfTests
    {
        private const long MB = 1024L * 1024L;

        public static (int passed, int failed, string report) RunAll()
        {
            int passed = 0;
            int failed = 0;
            var report = new StringBuilder();

            Run("예열한 개수만큼 창고에 쌓인다", TestPrewarmCount, ref passed, ref failed, report);
            Run("Get/Release 후 활성·대기 수가 맞는다", TestGetReleaseCounts, ref passed, ref failed, report);
            Run("Get 이 새로 만들지 않고 재사용한다", TestGetReuses, ref passed, ref failed, report);
            Run("onGet·onRelease 가 정확히 한 번씩 불린다", TestCallbacksExactlyOnce, ref passed, ref failed, report);
            Run("이중 Release 는 false 를 돌려주고 통계를 올린다", TestDoubleRelease, ref passed, ref failed, report);
            Run("남의 인스턴스 Release 도 이중 반환으로 잡힌다", TestForeignRelease, ref passed, ref failed, report);
            Run("null Release 는 false 이고 이중 반환으로 세지 않는다", TestNullRelease, ref passed, ref failed, report);
            Run("maxSize 를 넘는 Release 는 쌓이지 않고 버려진다", TestMaxSizeDiscards, ref passed, ref failed, report);
            Run("버려진 인스턴스를 다시 Release 해도 이중 반환으로 잡힌다", TestDiscardedThenReleased, ref passed, ref failed, report);
            Run("ReleaseAll 이 밖에 나간 것을 전부 회수한다", TestReleaseAll, ref passed, ref failed, report);
            Run("IsAlive 가 죽은 인스턴스를 걸러낸다", TestIsAliveFilters, ref passed, ref failed, report);
            Run("생성자가 잘못된 인자를 조용히 넘기지 않는다", TestConstructorGuards, ref passed, ref failed, report);
            Run("팩토리가 null 을 주면 예외로 알린다", TestNullFactoryResultThrows, ref passed, ref failed, report);

            Run("단조 증가 표본은 '증가'", TestTrendGrowing, ref passed, ref failed, report);
            Run("작아도 단조 증가면 '증가'", TestTrendSmallMonotonicGrowing, ref passed, ref failed, report);
            Run("평평한 표본은 '안정'", TestTrendStable, ref passed, ref failed, report);
            Run("단조 감소 표본은 '감소'", TestTrendShrinking, ref passed, ref failed, report);
            Run("표본 하나의 스파이크를 증가로 오판하지 않는다", TestTrendSpikeIsNotGrowth, ref passed, ref failed, report);
            Run("표본이 부족하면 안정이 아니라 판정 불가", TestTrendInsufficient, ref passed, ref failed, report);
            Run("표본 2~3개에는 단조 규칙을 적용하지 않는다", TestTrendMonotonicNeedsEnoughSamples, ref passed, ref failed, report);
            Run("첫 층 대비 마지막 층 증가분을 낸다", TestTrendFirstToLast, ref passed, ref failed, report);

            Run("하이라이트 주기를 반복해도 새로 만들지 않는다", TestHighlightCycleReuses, ref passed, ref failed, report);
            Run("풀에서 나온 블록은 반드시 비어 있다", TestPooledBlockComesOutCleared, ref passed, ref failed, report);
            Run("해제가 두 번 돌면 이중 반환으로 잡힌다", TestHighlightDoubleClearIsCaught, ref passed, ref failed, report);
            Run("회수를 빠뜨리면 활성 수가 그것을 드러낸다", TestHighlightLeakIsVisible, ref passed, ref failed, report);

            report.Insert(0, "[상승] === Perf (풀링·메모리 추세) Tests ===\n");
            report.Append($"결과: {passed} PASS / {failed} FAIL");

            // (배선 진단 스위트는 여기 임시로 붙어 있었다. 통합자가 두 등록처
            //  — `Assets/Editor/AscendTestMenu.AllSuites()` 와
            //  `PrototypeSelfTest.RunAllToString()` — 에 별도 줄로 넣었으므로 뗐다.
            //  붙어 있는 동안은 합계가 정확했지만 라벨이 「풀링·메모리 추세」로 찍혀
            //  배선 11건이 그 아래 섞여 보였다.)

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

        // ── 풀링 ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 풀이 다루는 대상 대역. <c>Equals</c> 를 재정의해서 참조 비교가 실제로
        /// 참조 비교인지 함께 검증한다 — 값이 같다고 같은 인스턴스로 취급하면
        /// 이중 반환 감지가 무너진다.
        /// </summary>
        private sealed class Dummy
        {
            public int Id;
            public bool Alive = true;
            public override bool Equals(object obj) { return obj is Dummy; }   // 일부러 엉성하게
            public override int GetHashCode() { return 1; }                     // 일부러 전부 충돌
        }

        private static ObjectPool<Dummy> MakePool(int prewarm, int maxSize)
        {
            int next = 0;
            return new ObjectPool<Dummy>(
                delegate { return new Dummy { Id = next++ }; },
                null, null, prewarm, maxSize);
        }

        private static string TestPrewarmCount()
        {
            ObjectPool<Dummy> pool = MakePool(5, 10);
            if (pool.CountInactive != 5) return $"대기 {pool.CountInactive}, 기대 5";
            if (pool.CountActive != 0) return $"활성 {pool.CountActive}, 기대 0";
            if (pool.CreatedCount != 5) return $"생성 {pool.CreatedCount}, 기대 5";

            // 상한을 넘는 예열은 상한에서 멈춘다. 넘겨서 쌓으면 상한이 거짓말이 된다.
            ObjectPool<Dummy> capped = MakePool(0, 3);
            int made = capped.Prewarm(10);
            if (made != 3) return $"상한 3 에서 예열이 {made}개 됐다";
            if (capped.CountInactive != 3) return $"대기 {capped.CountInactive}, 기대 3";
            return null;
        }

        private static string TestGetReleaseCounts()
        {
            ObjectPool<Dummy> pool = MakePool(5, 10);
            var taken = new Dummy[3];
            for (int i = 0; i < 3; i++) taken[i] = pool.Get();

            if (pool.CountActive != 3) return $"Get 3회 후 활성 {pool.CountActive}, 기대 3";
            if (pool.CountInactive != 2) return $"Get 3회 후 대기 {pool.CountInactive}, 기대 2";

            for (int i = 0; i < 3; i++)
                if (!pool.Release(taken[i])) return $"정상 반환이 false 를 돌려줬다 (i={i})";

            if (pool.CountActive != 0) return $"반환 후 활성 {pool.CountActive}, 기대 0";
            if (pool.CountInactive != 5) return $"반환 후 대기 {pool.CountInactive}, 기대 5";
            if (pool.CreatedCount != 5) return $"생성 {pool.CreatedCount} — 반환분을 재사용하지 않았다";
            return null;
        }

        private static string TestGetReuses()
        {
            ObjectPool<Dummy> pool = MakePool(0, 10);
            Dummy first = pool.Get();
            int createdAfterFirst = pool.CreatedCount;
            pool.Release(first);

            Dummy second = pool.Get();
            if (!ReferenceEquals(first, second)) return "반환한 것과 다른 인스턴스를 돌려줬다";
            if (pool.CreatedCount != createdAfterFirst)
                return $"재사용 대신 새로 만들었다 (생성 {pool.CreatedCount}, 기대 {createdAfterFirst})";
            if (pool.ReusedCount != 1) return $"재사용 카운트 {pool.ReusedCount}, 기대 1";
            return null;
        }

        private static string TestCallbacksExactlyOnce()
        {
            int gets = 0;
            int releases = 0;
            // 예열은 onRelease 를 부르므로(풀 안의 것은 반환된 상태라는 불변식) 0으로 둔다.
            var pool = new ObjectPool<Dummy>(
                delegate { return new Dummy(); },
                delegate { gets++; },
                delegate { releases++; },
                0, 8);

            Dummy d = pool.Get();
            if (gets != 1) return $"Get 1회에 onGet {gets}회";
            if (releases != 0) return $"Get 만 했는데 onRelease {releases}회";

            pool.Release(d);
            if (gets != 1) return $"Release 후 onGet 이 {gets}회로 늘었다";
            if (releases != 1) return $"Release 1회에 onRelease {releases}회";

            // 실패한 반환은 콜백을 부르지 않아야 한다 — 부르면 비활성화가 두 번 돈다.
            pool.Release(d);
            if (releases != 1) return $"이중 반환인데 onRelease 가 {releases}회로 늘었다";
            return null;
        }

        private static string TestDoubleRelease()
        {
            ObjectPool<Dummy> pool = MakePool(0, 8);
            Dummy d = pool.Get();

            if (!pool.Release(d)) return "첫 반환이 false 를 돌려줬다";
            if (pool.DoubleReleaseCount != 0) return $"첫 반환에서 이중 반환 카운트 {pool.DoubleReleaseCount}";

            if (pool.Release(d)) return "이중 반환이 true 를 돌려줬다 — 조용히 통과하면 안 된다";
            if (pool.DoubleReleaseCount != 1) return $"이중 반환 카운트 {pool.DoubleReleaseCount}, 기대 1";
            if (pool.CountInactive != 1) return $"이중 반환이 창고에 하나 더 쌓았다 (대기 {pool.CountInactive})";

            // 두 번 더 반환해도 계속 세어야 한다. 첫 번만 세면 통계가 사고 횟수를 감춘다.
            pool.Release(d);
            pool.Release(d);
            if (pool.DoubleReleaseCount != 3) return $"이중 반환 카운트 {pool.DoubleReleaseCount}, 기대 3";
            return null;
        }

        private static string TestForeignRelease()
        {
            ObjectPool<Dummy> pool = MakePool(0, 8);
            var stranger = new Dummy { Id = 999 };

            // Dummy.Equals 는 모든 Dummy 를 같다고 말한다. 참조 비교가 아니면 여기서 통과해 버린다.
            pool.Get();
            if (pool.Release(stranger)) return "이 풀이 내주지 않은 인스턴스 반환이 true 를 돌려줬다";
            if (pool.DoubleReleaseCount != 1) return $"이중 반환 카운트 {pool.DoubleReleaseCount}, 기대 1";
            if (pool.CountActive != 1) return $"활성 {pool.CountActive} — 남의 반환이 활성 집합을 건드렸다";
            return null;
        }

        private static string TestNullRelease()
        {
            ObjectPool<Dummy> pool = MakePool(0, 8);
            if (pool.Release(null)) return "null 반환이 true 를 돌려줬다";
            if (pool.NullReleaseCount != 1) return $"null 반환 카운트 {pool.NullReleaseCount}, 기대 1";
            if (pool.DoubleReleaseCount != 0)
                return "null 반환을 이중 반환으로 셌다 — 원인이 다르면 통계도 달라야 한다";
            return null;
        }

        private static string TestMaxSizeDiscards()
        {
            ObjectPool<Dummy> pool = MakePool(0, 3);
            var taken = new Dummy[6];
            for (int i = 0; i < 6; i++) taken[i] = pool.Get();

            for (int i = 0; i < 6; i++)
                if (!pool.Release(taken[i])) return $"정상 반환이 false 를 돌려줬다 (i={i})";

            if (pool.CountInactive != 3) return $"대기 {pool.CountInactive}, 상한 3 을 넘겼다";
            if (pool.DiscardedCount != 3) return $"버림 {pool.DiscardedCount}, 기대 3";
            if (pool.CountActive != 0) return $"활성 {pool.CountActive}, 기대 0";
            if (pool.DoubleReleaseCount != 0)
                return "상한 초과 반환을 이중 반환으로 셌다 — 반환 자체는 옳았다";
            return null;
        }

        private static string TestDiscardedThenReleased()
        {
            ObjectPool<Dummy> pool = MakePool(0, 1);
            Dummy a = pool.Get();
            Dummy b = pool.Get();

            pool.Release(a);            // 창고에 들어간다
            pool.Release(b);            // 상한 초과 — 버려진다
            if (pool.DiscardedCount != 1) return $"버림 {pool.DiscardedCount}, 기대 1";

            // 버려졌더라도 소유권은 이미 끝났다. 다시 반환하는 것은 여전히 사고다.
            if (pool.Release(b)) return "버려진 인스턴스 재반환이 true 를 돌려줬다";
            if (pool.DoubleReleaseCount != 1) return $"이중 반환 카운트 {pool.DoubleReleaseCount}, 기대 1";
            return null;
        }

        private static string TestReleaseAll()
        {
            ObjectPool<Dummy> pool = MakePool(0, 16);
            for (int i = 0; i < 5; i++) pool.Get();

            int released = pool.ReleaseAll();
            if (released != 5) return $"회수 {released}, 기대 5";
            if (pool.CountActive != 0) return $"회수 후 활성 {pool.CountActive}, 기대 0";
            if (pool.CountInactive != 5) return $"회수 후 대기 {pool.CountInactive}, 기대 5";
            if (pool.DoubleReleaseCount != 0) return "ReleaseAll 이 스스로 이중 반환을 만들었다";

            if (pool.ReleaseAll() != 0) return "빈 활성 집합에서 ReleaseAll 이 무언가를 회수했다";
            return null;
        }

        private static string TestIsAliveFilters()
        {
            ObjectPool<Dummy> pool = MakePool(0, 8);
            pool.IsAlive = delegate (Dummy d) { return d.Alive; };

            Dummy first = pool.Get();
            pool.Release(first);
            first.Alive = false;        // 밖에서 파괴된 상황

            Dummy second = pool.Get();
            if (ReferenceEquals(first, second)) return "죽은 인스턴스를 그대로 다시 내줬다";
            if (pool.DeadDiscardedCount != 1) return $"사망 폐기 카운트 {pool.DeadDiscardedCount}, 기대 1";
            if (!second.Alive) return "새로 만든 인스턴스가 살아 있지 않다";
            return null;
        }

        private static string TestConstructorGuards()
        {
            try
            {
                var bad = new ObjectPool<Dummy>(null, null, null, 0, 4);
                return "create 가 null 인데 풀이 만들어졌다 (" + bad.MaxSize + ")";
            }
            catch (ArgumentNullException) { }

            try
            {
                var bad = new ObjectPool<Dummy>(delegate { return new Dummy(); }, null, null, 0, 0);
                return "maxSize 0 인데 풀이 만들어졌다 (" + bad.MaxSize + ")";
            }
            catch (ArgumentOutOfRangeException) { }

            return null;
        }

        private static string TestNullFactoryResultThrows()
        {
            var pool = new ObjectPool<Dummy>(delegate { return (Dummy)null; }, null, null, 0, 4);
            try
            {
                pool.Get();
                return "팩토리가 null 을 줬는데 Get 이 조용히 통과했다";
            }
            catch (InvalidOperationException) { return null; }
        }

        // ── 하이라이트 블록 풀 (UP-TECH-06 의 유일한 소비처) ────────────────────
        //
        // `CrosshairInteractor.ApplyHighlight` 가 쓰는 계약을 그대로 재현한다.
        // 실제 `MaterialPropertyBlock` 을 쓰지 않는 이유는 이 스위트가 씬도 렌더러도
        // 없이 돌기 때문이고, 여기서 지키려는 것은 블록의 그래픽 동작이 아니라
        // **소유권 계약**이다 — 「꺼낸 것은 비어 있다 · 되돌린 것은 정확히 한 번만
        // 돌아간다」. 그 둘이 깨지는 순간의 증상은 각각 「앞 대상의 색이 남는다」와
        // 「두 대상이 같은 블록을 쓴다」이고, 둘 다 화면에서 원인을 못 찾는 종류다.

        private sealed class FakeBlock
        {
            /// <summary>풀에 들어가 있는 동안은 반드시 참이어야 한다.</summary>
            public bool Cleared = true;
        }

        /// <summary><c>CrosshairInteractor</c> 와 같은 형태로 만든다 — 예열 8 · 상한 32 · 반환 시 비움.</summary>
        private static ObjectPool<FakeBlock> HighlightPool()
        {
            return new ObjectPool<FakeBlock>(
                delegate { return new FakeBlock(); },
                null,
                delegate (FakeBlock block) { block.Cleared = true; },
                8, 32);
        }

        /// <summary>한 대상에 하이라이트를 걸었다 푸는 한 주기.</summary>
        private static bool HighlightCycle(ObjectPool<FakeBlock> pool, int rendererCount, FakeBlock[] scratch)
        {
            for (int i = 0; i < rendererCount; i++)
            {
                FakeBlock block = pool.Get();
                block.Cleared = false;      // GetPropertyBlock 이 값을 채운 상태
                scratch[i] = block;
            }

            for (int i = 0; i < rendererCount; i++)
                if (!pool.Release(scratch[i])) return false;

            return true;
        }

        private static string TestHighlightCycleReuses()
        {
            ObjectPool<FakeBlock> pool = HighlightPool();
            var scratch = new FakeBlock[5];

            if (!HighlightCycle(pool, 5, scratch)) return "첫 주기의 반환이 false 를 돌려줬다";
            int createdAfterFirst = pool.CreatedCount;

            // 조준 대상이 계속 바뀌는 것이 이 경로의 정상 상태다. 주기를 돌 때마다
            // 새로 만들면 풀을 넣기 전과 할당이 같다.
            for (int cycle = 0; cycle < 20; cycle++)
                if (!HighlightCycle(pool, 5, scratch)) return $"{cycle}번째 주기의 반환이 false 를 돌려줬다";

            if (pool.CreatedCount != createdAfterFirst)
                return $"생성 {pool.CreatedCount} — 20주기 동안 {pool.CreatedCount - createdAfterFirst}개를 더 만들었다";
            if (pool.CountActive != 0) return $"주기가 끝났는데 활성 {pool.CountActive}";
            if (pool.DoubleReleaseCount != 0) return $"이중 반환 {pool.DoubleReleaseCount}";
            if (pool.ReusedCount < 100) return $"재사용 {pool.ReusedCount} — 풀이 일하지 않았다";
            return null;
        }

        private static string TestPooledBlockComesOutCleared()
        {
            ObjectPool<FakeBlock> pool = HighlightPool();
            var scratch = new FakeBlock[3];

            HighlightCycle(pool, 3, scratch);
            for (int i = 0; i < 3; i++)
                if (!scratch[i].Cleared) return $"반환된 블록 {i} 가 비워지지 않았다";

            // 예열분이 먼저 나올 수도 있으므로 여러 번 꺼내 전부 확인한다.
            for (int i = 0; i < 12; i++)
            {
                FakeBlock block = pool.Get();
                if (!block.Cleared)
                    return "값이 남은 블록이 나왔다 — 앞 대상의 색이 다음 대상에 실린다";
                block.Cleared = false;
            }
            return null;
        }

        private static string TestHighlightDoubleClearIsCaught()
        {
            ObjectPool<FakeBlock> pool = HighlightPool();
            var scratch = new FakeBlock[2];
            HighlightCycle(pool, 2, scratch);

            // 목록을 비우지 않고 해제를 한 번 더 돌린 상황. 조용히 통과하면
            // 같은 블록이 두 대상에게 나가고, 그 증상은 화면에서 추적할 수 없다.
            if (pool.Release(scratch[0])) return "이중 반환이 true 를 돌려줬다";
            if (pool.DoubleReleaseCount != 1) return $"이중 반환 카운트 {pool.DoubleReleaseCount}, 기대 1";
            return null;
        }

        private static string TestHighlightLeakIsVisible()
        {
            ObjectPool<FakeBlock> pool = HighlightPool();

            // 회수를 빠뜨린 경우. 통계가 그것을 드러내지 않으면 「풀을 넣었다」가
            // 참인 채로 할당은 계속 늘어난다.
            for (int i = 0; i < 40; i++) pool.Get();

            if (pool.CountActive != 40) return $"활성 {pool.CountActive}, 기대 40";
            if (pool.CreatedCount < 40 - 8) return $"생성 {pool.CreatedCount} — 새는데도 늘지 않았다";

            int recovered = pool.ReleaseAll();
            if (recovered != 40) return $"회수 {recovered}, 기대 40";
            if (pool.CountActive != 0) return $"회수 후 활성 {pool.CountActive}";
            if (pool.DiscardedCount != 40 - 32) return $"버림 {pool.DiscardedCount}, 기대 8 (상한 32)";
            return null;
        }

        // ── 메모리 추세 ─────────────────────────────────────────────────────────

        private static long[] Ramp(long startMb, long stepMb, int count)
        {
            var samples = new long[count];
            for (int i = 0; i < count; i++) samples[i] = (startMb + stepMb * i) * MB;
            return samples;
        }

        private static long[] Flat(long valueMb, int count)
        {
            var samples = new long[count];
            for (int i = 0; i < count; i++) samples[i] = valueMb * MB;
            return samples;
        }

        private static string TestTrendGrowing()
        {
            // 10층 런의 층 종료 표본을 흉내낸다: 층마다 10 MB 씩 남는다.
            MemoryTrendResult result = MemoryTrend.Analyze(Ramp(200, 10, 10));
            if (result.Verdict != MemoryTrendVerdict.Growing)
                return $"판정 {result.Verdict} — {result.Describe()}";
            if (result.RisingSteps != 9) return $"상승 단계 {result.RisingSteps}, 기대 9";
            if (result.DeltaBytes <= 0) return "구간 평균 차가 양수가 아니다";
            return null;
        }

        private static string TestTrendSmallMonotonicGrowing()
        {
            // 층마다 64 KB. 절대 허용치(4 MB)에는 한참 못 미치지만 한 번도 되돌아가지
            // 않았다. 이것이 누수의 실제 모습이고, 노이즈 허용치로 덮으면 안 된다.
            var samples = new long[10];
            for (int i = 0; i < samples.Length; i++) samples[i] = 128L * MB + i * 64L * 1024L;

            MemoryTrendResult result = MemoryTrend.Analyze(samples);
            if (result.Verdict != MemoryTrendVerdict.Growing)
                return $"판정 {result.Verdict} — {result.Describe()}";
            if (!result.DecidedByMonotonicity) return "단조 규칙으로 판정했다고 밝히지 않았다";
            return null;
        }

        private static string TestTrendStable()
        {
            MemoryTrendResult result = MemoryTrend.Analyze(Flat(256, 12));
            if (result.Verdict != MemoryTrendVerdict.Stable)
                return $"판정 {result.Verdict} — {result.Describe()}";
            if (result.RisingSteps != 0 || result.FallingSteps != 0)
                return $"평평한데 상승 {result.RisingSteps} / 하강 {result.FallingSteps}";
            if (result.FirstToLastBytes != 0) return $"증가분 {result.FirstToLastBytes}, 기대 0";

            // 허용치 안에서 흔들리는 것도 안정이다. GC 타이밍은 늘 흔들린다.
            var jitter = new long[] {
                256L * MB, 257L * MB, 255L * MB, 256L * MB,
                258L * MB, 255L * MB, 256L * MB, 257L * MB,
            };
            MemoryTrendResult jittered = MemoryTrend.Analyze(jitter);
            if (jittered.Verdict != MemoryTrendVerdict.Stable)
                return $"흔들림을 {jittered.Verdict} 로 판정 — {jittered.Describe()}";
            return null;
        }

        private static string TestTrendShrinking()
        {
            MemoryTrendResult result = MemoryTrend.Analyze(Ramp(400, -20, 10));
            if (result.Verdict != MemoryTrendVerdict.Shrinking)
                return $"판정 {result.Verdict} — {result.Describe()}";
            if (result.FirstToLastBytes >= 0) return $"증가분 {result.FirstToLastBytes} — 음수여야 한다";
            return null;
        }

        private static string TestTrendSpikeIsNotGrowth()
        {
            // 가운데 한 번 크게 튀었다가 되돌아온다. 이것을 누수라고 부르면
            // 이 측정은 곧 아무도 읽지 않게 된다.
            var samples = new long[] {
                200L * MB, 200L * MB, 201L * MB, 199L * MB,
                600L * MB,
                200L * MB, 201L * MB, 199L * MB, 200L * MB, 200L * MB,
            };
            MemoryTrendResult result = MemoryTrend.Analyze(samples);
            if (result.Verdict != MemoryTrendVerdict.Stable)
                return $"판정 {result.Verdict} — {result.Describe()}";
            if (result.Max != 600L * MB) return "최댓값을 놓쳤다 — 스파이크 자체는 기록되어야 한다";
            return null;
        }

        private static string TestTrendInsufficient()
        {
            if (MemoryTrend.Analyze(null).Verdict != MemoryTrendVerdict.Insufficient)
                return "null 표본을 판정 불가로 보지 않았다";
            if (MemoryTrend.Analyze(new long[0]).Verdict != MemoryTrendVerdict.Insufficient)
                return "빈 표본을 판정 불가로 보지 않았다";

            MemoryTrendResult single = MemoryTrend.Analyze(new long[] { 256L * MB });
            if (single.Verdict != MemoryTrendVerdict.Insufficient)
                return $"표본 1개를 {single.Verdict} 로 판정 — 안정이라고 말하면 거짓 안심을 준다";
            if (!single.Describe().Contains("판정하지 않았다"))
                return $"설명이 판정 불가임을 밝히지 않는다: {single.Describe()}";
            return null;
        }

        /// <summary>
        /// 층 두 개만 재고 1 B 올랐다고 "누수"라 부르면, 이 측정은 첫날부터 거짓 경보만 낸다.
        /// 단조 규칙에는 최소 표본 수가 있어야 한다.
        /// </summary>
        private static string TestTrendMonotonicNeedsEnoughSamples()
        {
            var two = new long[] { 200L * MB, 200L * MB + 1L };
            MemoryTrendResult pair = MemoryTrend.Analyze(two);
            if (pair.Verdict != MemoryTrendVerdict.Stable)
                return $"표본 2개를 {pair.Verdict} 로 판정 — {pair.Describe()}";
            if (pair.DecidedByMonotonicity) return "표본 2개에 단조 규칙을 적용했다";

            var three = new long[] { 200L * MB, 200L * MB + 1L, 200L * MB + 2L };
            MemoryTrendResult triple = MemoryTrend.Analyze(three);
            if (triple.Verdict != MemoryTrendVerdict.Stable)
                return $"표본 3개를 {triple.Verdict} 로 판정 — {triple.Describe()}";

            // 최소치를 넘기면 같은 기울기가 증가로 잡혀야 한다. 규칙이 꺼진 채로 남으면 안 된다.
            var enough = new long[MemoryTrend.MinimumSamplesForMonotonicRule];
            for (int i = 0; i < enough.Length; i++) enough[i] = 200L * MB + i;
            MemoryTrendResult atThreshold = MemoryTrend.Analyze(enough);
            if (atThreshold.Verdict != MemoryTrendVerdict.Growing)
                return $"표본 {enough.Length}개(최소치)를 {atThreshold.Verdict} 로 판정 — {atThreshold.Describe()}";
            if (!atThreshold.DecidedByMonotonicity) return "최소치에서 단조 규칙이 적용되지 않았다";
            return null;
        }

        private static string TestTrendFirstToLast()
        {
            long[] samples = Ramp(100, 5, 11);   // 100 → 150 MB
            MemoryTrendResult result = MemoryTrend.Analyze(samples);
            if (result.FirstToLastBytes != 50L * MB)
                return $"첫→마지막 {MemoryTrend.FormatBytes(result.FirstToLastBytes)}, 기대 50 MB";
            if (result.First != 100L * MB || result.Last != 150L * MB)
                return $"첫 {result.First} / 마지막 {result.Last}";
            if (Math.Abs(result.PerSampleBytes - 5L * MB) > 1.0)
                return $"표본당 {MemoryTrend.FormatBytes((long)result.PerSampleBytes)}, 기대 5 MB";

            // 부분 구간만 분석할 수 있어야 한다. 고정 크기 배열을 부분적으로 채워 쓰는
            // MemoryTrendProbe 가 이 형태로 부른다.
            MemoryTrendResult partial = MemoryTrend.Analyze(samples, 3,
                MemoryTrend.DefaultAbsoluteToleranceBytes, MemoryTrend.DefaultRelativeTolerance);
            if (partial.SampleCount != 3) return $"부분 분석 표본 수 {partial.SampleCount}, 기대 3";
            if (partial.Last != 110L * MB)
                return $"부분 분석 마지막 {MemoryTrend.FormatBytes(partial.Last)}, 기대 110 MB";
            return null;
        }
    }
}
