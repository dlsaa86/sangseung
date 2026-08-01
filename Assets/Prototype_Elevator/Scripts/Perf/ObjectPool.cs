using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Ascend.Prototype.Perf
{
    /// <summary>
    /// 재사용 가능한 인스턴스 창고. 순수 C# 이라 씬 없이 검증된다(`TECH_SPEC.md` §2).
    ///
    /// 왜 필요한가: `TECH_SPEC.md` §13 이 "워밍업 이후 일반 렌더·게임플레이 루프는
    /// 프레임당 0 B GC Alloc" 을 목표로 못박았다. 파티클·결과판 심볼·사운드 소스는
    /// 스핀마다 수십 개가 생겼다 사라지는 것들이라, 매번 새로 만들면 그 목표가
    /// 구조적으로 불가능해진다. 캐스케이드 10단계에서 특히 그렇다.
    ///
    /// **이중 반환을 조용히 무시하지 않는 이유**가 이 클래스의 설계 핵심이다.
    /// 같은 인스턴스가 두 번 반환되면 풀은 그것을 두 번 내주고, 두 소유자가 같은
    /// 오브젝트를 각자 자기 것이라 믿으며 움직인다. 그 증상은 "파티클이 가끔 엉뚱한
    /// 자리에서 튄다" 같은 모습으로 나타나 원인 추적이 사실상 불가능해진다.
    /// 그래서 <see cref="Release"/> 는 bool 을 돌려주고 <see cref="DoubleReleaseCount"/>
    /// 를 올린다 — 버그가 통계로 남아야 나중에 세어볼 수 있다.
    ///
    /// UnityEngine 에 의존하지 않는다. 경고를 알리는 통로는 <see cref="OnWarning"/> 이고,
    /// Unity 어댑터(<c>ComponentPool</c>)가 이것을 <c>Debug.LogWarning</c> 에 연결한다.
    /// </summary>
    public sealed class ObjectPool<T> where T : class
    {
        /// <summary>
        /// 신원 비교는 반드시 참조로 한다. <c>T</c> 가 <c>Equals</c> 를 재정의했다면
        /// 값이 같은 두 인스턴스가 같은 것으로 취급되고, 그러면 이중 반환 감지가
        /// 정확히 감지해야 할 상황에서 거짓말을 한다.
        /// </summary>
        private sealed class ReferenceComparer : IEqualityComparer<T>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public bool Equals(T a, T b) { return ReferenceEquals(a, b); }
            public int GetHashCode(T value) { return RuntimeHelpers.GetHashCode(value); }
        }

        private readonly Func<T> _create;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;
        private readonly int _maxSize;

        private readonly Stack<T> _inactive;

        /// <summary>
        /// 지금 밖에 나가 있는 인스턴스. 이중 반환 판정의 근거는 "비활성 목록에 있는가"가
        /// 아니라 "이 풀이 실제로 내준 것인가"여야 한다 — maxSize 초과로 버려진 인스턴스는
        /// 비활성 목록에 없지만 그것을 다시 반환하는 것도 똑같은 소유권 버그다.
        /// </summary>
        private readonly HashSet<T> _active;

        /// <summary>ReleaseAll 이 순회 중 컬렉션을 바꾸지 않도록 쓰는 재사용 버퍼.</summary>
        private readonly List<T> _scratch = new List<T>();

        /// <summary>
        /// 경고를 받는 곳. null 이면 통계만 남는다 — 순수 C# 층위에는 로거가 없기 때문이다.
        /// 실패를 삼키지 않으려면 소유자가 이것을 반드시 연결한다.
        /// </summary>
        public Action<string> OnWarning;

        /// <summary>
        /// 인스턴스가 아직 쓸 수 있는가. Unity 오브젝트는 외부에서 Destroy 되어도
        /// 관리 객체는 살아 있으므로, 그 판정을 이 순수 클래스가 알 수는 없다.
        /// 어댑터가 채워 넣는다. null 이면 모든 인스턴스를 유효한 것으로 본다.
        /// </summary>
        public Func<T, bool> IsAlive;

        public ObjectPool(Func<T> create, Action<T> onGet, Action<T> onRelease, int prewarm, int maxSize)
        {
            if (create == null)
                throw new ArgumentNullException("create", "풀은 인스턴스를 만드는 법을 모르면 존재할 수 없다.");
            if (maxSize < 1)
                throw new ArgumentOutOfRangeException("maxSize", maxSize,
                    "보관 상한이 1 미만이면 풀이 아무것도 재사용하지 못한다. 의도한 것이라면 풀을 쓰지 않는 편이 정직하다.");

            _create = create;
            _onGet = onGet;
            _onRelease = onRelease;
            _maxSize = maxSize;

            int capacity = prewarm > 0 ? Math.Min(prewarm, maxSize) : 0;
            _inactive = new Stack<T>(capacity);
            _active = new HashSet<T>(ReferenceComparer.Instance);

            if (prewarm > 0) Prewarm(prewarm);
        }

        /// <summary>보관 상한. 이보다 많이 반환된 것은 버려진다.</summary>
        public int MaxSize { get { return _maxSize; } }

        /// <summary>지금 밖에 나가 있는 개수.</summary>
        public int CountActive { get { return _active.Count; } }

        /// <summary>지금 창고에 잠들어 있는 개수.</summary>
        public int CountInactive { get { return _inactive.Count; } }

        /// <summary>실제로 새로 만든 횟수. 워밍업 이후 이 값이 계속 오르면 풀이 일을 못 하고 있다.</summary>
        public int CreatedCount { get; private set; }

        /// <summary>창고에서 꺼내 재사용한 횟수.</summary>
        public int ReusedCount { get; private set; }

        /// <summary>이중 반환 횟수. 0 이 아니면 소유권 버그가 있다는 뜻이다.</summary>
        public int DoubleReleaseCount { get; private set; }

        /// <summary>null 을 반환하려 한 횟수.</summary>
        public int NullReleaseCount { get; private set; }

        /// <summary>보관 상한을 넘겨 버려진 횟수. 계속 오르면 상한이 실사용보다 작다.</summary>
        public int DiscardedCount { get; private set; }

        /// <summary><see cref="IsAlive"/> 가 거짓이라 폐기한 횟수. 밖에서 파괴된 인스턴스다.</summary>
        public int DeadDiscardedCount { get; private set; }

        /// <summary>
        /// 미리 만들어 재워 둔다. 첫 스핀에서 한꺼번에 생성되는 스파이크를
        /// 로딩 시점으로 옮기는 것이 목적이다(`TECH_SPEC.md` §13 "체감 가능한 프레임 스파이크 금지").
        ///
        /// 만들자마자 <c>onRelease</c> 를 부른다. "풀 안에 있는 것은 반환된 상태"라는
        /// 불변식이 없으면, 어댑터가 비활성화를 담당하는 구조에서 예열된 오브젝트가
        /// 씬에 활성 상태로 서 있게 된다.
        /// </summary>
        public int Prewarm(int count)
        {
            int made = 0;
            for (int i = 0; i < count; i++)
            {
                if (_inactive.Count >= _maxSize)
                {
                    Warn("예열 요청이 보관 상한 " + _maxSize + " 를 넘었다. " + made + "개에서 멈춘다.");
                    break;
                }
                T instance = CreateChecked();
                if (_onRelease != null) _onRelease(instance);
                _inactive.Push(instance);
                made++;
            }
            return made;
        }

        /// <summary>
        /// 하나 꺼낸다. 창고가 비어 있으면 만든다. 절대 null 을 돌려주지 않는다 —
        /// 호출부가 null 검사를 잊어도 되는 것이 풀을 쓰는 이유의 절반이다.
        /// </summary>
        public T Get()
        {
            T instance = null;
            while (_inactive.Count > 0)
            {
                T candidate = _inactive.Pop();
                if (IsAlive != null && !IsAlive(candidate))
                {
                    // 밖에서 파괴된 것을 다시 내주면 호출부가 "살아 있는데 아무 일도
                    // 안 일어난다"는 가장 나쁜 형태의 침묵을 겪는다.
                    DeadDiscardedCount++;
                    Warn("풀에 잠들어 있던 인스턴스가 밖에서 파괴돼 있었다. 폐기하고 새로 만든다.");
                    continue;
                }
                instance = candidate;
                ReusedCount++;
                break;
            }

            if (instance == null) instance = CreateChecked();

            _active.Add(instance);
            if (_onGet != null) _onGet(instance);
            return instance;
        }

        /// <summary>
        /// 창고로 돌려보낸다.
        ///
        /// 돌려주는 값의 뜻: **이 반환이 정상이었는가**다. 보관 상한을 넘겨 버려진
        /// 경우에도 true 다 — 반환 자체는 옳았고 버린 것은 풀의 정책이기 때문이다.
        /// false 는 오직 소유권이 어긋났을 때(null, 이중 반환, 남의 인스턴스)만 나온다.
        /// </summary>
        public bool Release(T instance)
        {
            if (instance == null)
            {
                NullReleaseCount++;
                Warn("null 을 Release 했다. 반환 경로 어딘가에서 참조가 이미 지워졌다는 뜻이다.");
                return false;
            }

            if (!_active.Remove(instance))
            {
                DoubleReleaseCount++;
                Warn("이 풀이 내주지 않았거나 이미 반환된 " + instance.GetType().Name +
                     " 인스턴스를 Release 했다. 같은 오브젝트를 두 소유자가 쓰게 되는 경로다.");
                return false;
            }

            if (_onRelease != null) _onRelease(instance);

            if (_inactive.Count >= _maxSize)
            {
                DiscardedCount++;
                return true;
            }

            _inactive.Push(instance);
            return true;
        }

        /// <summary>
        /// 밖에 나가 있는 것을 전부 회수한다. 층 경계나 런 재시작에서 쓴다 —
        /// 회수하지 않으면 남은 인스턴스가 다음 층 화면에 그대로 서 있게 된다.
        /// </summary>
        public int ReleaseAll()
        {
            if (_active.Count == 0) return 0;

            _scratch.Clear();
            foreach (T item in _active) _scratch.Add(item);

            int released = 0;
            for (int i = 0; i < _scratch.Count; i++)
                if (Release(_scratch[i])) released++;

            _scratch.Clear();
            return released;
        }

        /// <summary>
        /// 창고를 비운다. 밖에 나가 있는 것은 건드리지 않는다 — 그것들의 소유자는
        /// 풀이 아니고, 여기서 파괴하면 소유자가 파괴된 참조를 계속 들고 있게 된다.
        /// </summary>
        public int Clear(Action<T> onDestroy)
        {
            int cleared = 0;
            while (_inactive.Count > 0)
            {
                T instance = _inactive.Pop();
                if (onDestroy != null) onDestroy(instance);
                cleared++;
            }
            return cleared;
        }

        /// <summary>한 줄 요약. 성능 보고서와 디버그 패널이 그대로 찍는다.</summary>
        public string Stats()
        {
            return "활성 " + _active.Count + " / 대기 " + _inactive.Count + " / 상한 " + _maxSize +
                   " · 생성 " + CreatedCount + " 재사용 " + ReusedCount +
                   " · 버림 " + DiscardedCount + " 사망폐기 " + DeadDiscardedCount +
                   " · 이중반환 " + DoubleReleaseCount + " null반환 " + NullReleaseCount;
        }

        private T CreateChecked()
        {
            T instance = _create();
            if (instance == null)
            {
                // 팩토리가 null 을 돌려주는 것은 밸런스 문제가 아니라 결선 실수다.
                // 여기서 삼키면 나중에 NullReference 가 전혀 무관한 곳에서 터진다.
                throw new InvalidOperationException(
                    "ObjectPool<" + typeof(T).Name + "> 의 생성 팩토리가 null 을 돌려줬다. " +
                    "프리팹 참조가 비어 있는지 확인한다.");
            }
            CreatedCount++;
            return instance;
        }

        private void Warn(string message)
        {
            Action<string> sink = OnWarning;
            if (sink != null) sink("[풀:" + typeof(T).Name + "] " + message);
        }
    }
}
