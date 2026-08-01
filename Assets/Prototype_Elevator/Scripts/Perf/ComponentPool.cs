using System;
using UnityEngine;

namespace Ascend.Prototype.Perf
{
    /// <summary>
    /// <see cref="ObjectPool{T}"/> 위에 얹은 Unity 어댑터. 프리팹이나 팩토리로 만든
    /// GameObject 를 껐다 켜며 돌려 쓴다. 파티클·결과판 심볼·사운드 소스가 이것을 쓴다
    /// (`MASTER_PRD.md` §13.2 "지정 하드웨어 기준 성능 조건 충족",
    ///  `TECH_SPEC.md` §13 "프레임당 0 B GC Alloc 목표").
    ///
    /// 왜 Destroy 하지 않고 SetActive(false) 인가: <c>Destroy</c> 는 관리 힙과 네이티브
    /// 양쪽에 부담을 남기고, 다음 <c>Instantiate</c> 는 프리팹 그래프를 통째로 다시 만든다.
    /// 스핀 하나에 심볼 9칸 + 캐스케이드 10단계가 붙는 구조에서는 그 비용이 프레임에
    /// 그대로 보인다.
    ///
    /// 왜 부모로 되돌리는가: 연출 코드가 심볼을 다른 트랜스폼에 붙였다 놓는 일이 흔한데,
    /// 원래 부모로 돌려놓지 않으면 반환된 오브젝트가 남의 계층 아래에 잠들어 있다가
    /// 그 부모가 파괴될 때 함께 사라진다. 그러면 풀은 죽은 참조를 들고 있게 된다.
    /// (그 상황 자체는 <see cref="ObjectPool{T}.IsAlive"/> 가 잡지만, 잡히지 않게
    ///  하는 것이 먼저다.)
    /// </summary>
    public sealed class ComponentPool<T> where T : Component
    {
        /// <summary>
        /// Unity 의 <c>==</c> 재정의가 파괴된 오브젝트를 null 로 보이게 해 준다.
        /// 델리게이트를 static 으로 캐시해서 풀마다 새 클로저가 생기지 않게 한다.
        /// </summary>
        private static readonly Func<T, bool> AliveCheck = delegate (T c) { return c != null; };

        private readonly ObjectPool<T> _pool;
        private readonly Transform _parent;
        private readonly string _label;

        public ComponentPool(T prefab, Transform parent, int prewarm, int maxSize, string label)
            : this(MakePrefabFactory(prefab, parent, label), parent, prewarm, maxSize,
                   label ?? (prefab != null ? prefab.name : "null-prefab"))
        {
        }

        public ComponentPool(Func<T> factory, Transform parent, int prewarm, int maxSize, string label)
        {
            if (factory == null)
                throw new ArgumentNullException("factory", "ComponentPool 은 만드는 법을 모르면 존재할 수 없다.");

            _parent = parent;
            _label = string.IsNullOrEmpty(label) ? typeof(T).Name : label;

            // 예열을 생성자에 맡기지 않고 뒤로 미룬다. 맡기면 예열 중에 나는 경고가
            // OnWarning 이 연결되기 전에 발생해 조용히 사라진다 — 상한을 넘긴 예열은
            // 정확히 알아야 하는 종류의 실수다.
            _pool = new ObjectPool<T>(factory, OnGet, OnRelease, 0, maxSize);
            _pool.IsAlive = AliveCheck;
            _pool.OnWarning = OnWarning;
            if (prewarm > 0) _pool.Prewarm(prewarm);
        }

        public int CountActive { get { return _pool.CountActive; } }
        public int CountInactive { get { return _pool.CountInactive; } }
        public int CreatedCount { get { return _pool.CreatedCount; } }
        public int ReusedCount { get { return _pool.ReusedCount; } }
        public int DoubleReleaseCount { get { return _pool.DoubleReleaseCount; } }
        public int DiscardedCount { get { return _pool.DiscardedCount; } }

        /// <summary>테스트와 성능 보고서가 내부 통계를 그대로 읽을 수 있게 열어 둔다.</summary>
        public ObjectPool<T> Raw { get { return _pool; } }

        public int Prewarm(int count) { return _pool.Prewarm(count); }

        public T Get() { return _pool.Get(); }

        public T Get(Vector3 position, Quaternion rotation)
        {
            T instance = _pool.Get();
            instance.transform.SetPositionAndRotation(position, rotation);
            return instance;
        }

        /// <summary>false 면 소유권이 어긋난 것이다. <see cref="ObjectPool{T}.Release"/> 참조.</summary>
        public bool Release(T instance) { return _pool.Release(instance); }

        public int ReleaseAll() { return _pool.ReleaseAll(); }

        /// <summary>창고에 잠든 오브젝트를 실제로 파괴한다. 씬 전환이나 런 종료에서만 쓴다.</summary>
        public int Clear()
        {
            return _pool.Clear(DestroyPooled);
        }

        public string Stats() { return _label + " — " + _pool.Stats(); }

        private static Func<T> MakePrefabFactory(T prefab, Transform parent, string label)
        {
            if (prefab == null)
            {
                // 어디의 프리팹이 비었는지 이름과 함께 남긴다. "프리팹이 null" 만으로는
                // 씬에 풀이 여럿일 때 아무것도 알려 주지 못한다.
                throw new ArgumentNullException("prefab",
                    "ComponentPool<" + typeof(T).Name + ">(" + (label ?? "무명") + ") 의 프리팹이 비어 있다. 씬 인스펙터 결선을 확인한다.");
            }
            return delegate { return UnityEngine.Object.Instantiate(prefab, parent); };
        }

        private void OnGet(T instance)
        {
            if (instance == null) return;
            instance.gameObject.SetActive(true);
        }

        private void OnRelease(T instance)
        {
            if (instance == null) return;
            instance.gameObject.SetActive(false);
            Transform t = instance.transform;
            if (_parent != null && t.parent != _parent) t.SetParent(_parent, false);
        }

        private static void DestroyPooled(T instance)
        {
            if (instance == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(instance.gameObject);
            else UnityEngine.Object.DestroyImmediate(instance.gameObject);
        }

        private void OnWarning(string message)
        {
            // 풀의 경고는 전부 소유권 사고다. Log 가 아니라 LogWarning 으로 올려서
            // 콘솔 필터를 켜 둔 상태에서도 보이게 한다.
            Debug.LogWarning("[상승] " + _label + " " + message);
        }
    }
}

// 씬 배선 필요: 없음. 이 클래스는 코드에서 직접 생성한다.
//   사용 예 — 파티클 소유자가 Awake 에서
//     _burstPool = new ComponentPool<ParticleSystem>(_burstPrefab, transform, 8, 32, "정화버스트");
//   를 만들고, 층 경계에서 ReleaseAll(), 런 종료에서 Clear() 를 부른다.
