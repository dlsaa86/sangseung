namespace Ascend.Prototype.Spin
{
    /// <summary>
    /// 시드 파생 규칙의 **단일 출처**. `TECH_SPEC.md` §7이 요구하는 계약이다 —
    /// "각 층과 스핀은 RunSeed, 층 번호, 스핀 인덱스에서 파생한 시드를 사용한다.
    /// 시드 파생 규칙은 한 곳에 정의한다."
    ///
    /// 왜 순차 스트림(`Random.Next()` 반복)이 아니라 좌표 기반 파생인가:
    /// 순차 스트림은 결정론적이긴 하지만 **호출 순서에 의존한다.** 앞선 층에서 스핀을
    /// 한 번만 더 하거나 덜 해도 이후 모든 시드가 밀린다. 그러면 "3층 2번째 스핀"을
    /// 단독으로 재현할 수 없고, 고정 시드로 캡처를 재현하는 것도 불가능하다.
    /// 좌표 기반 파생은 (런 시드, 층, 스핀 인덱스) 세 값만 알면 어디서든 같은 판을 만든다.
    ///
    /// 연출용 난수는 이 경로를 쓰지 않는다(`TECH_SPEC.md` §7 마지막 항목).
    /// </summary>
    public static class SpinSeed
    {
        // 서로 다른 축을 섞기 전에 곱하는 홀수 상수. 층과 스핀 인덱스가 작은 정수라
        // 그냥 더하면 (층 2, 스핀 0)과 (층 1, 스핀 1)이 같은 값으로 겹친다.
        private const uint FloorStride = 0x9E3779B1u;   // 2^32 / 황금비
        private const uint SpinStride  = 0x85EBCA77u;

        /// <summary>
        /// 한 스핀의 시드. 같은 세 값이면 항상 같은 결과가 나와야 한다.
        /// </summary>
        /// <param name="runSeed">런 시작 시 정해진 시드.</param>
        /// <param name="floor">층 번호(1부터).</param>
        /// <param name="spinIndex">해당 층에서 몇 번째 스핀인가(0부터). 추가 스핀도 포함한다.</param>
        public static int Derive(int runSeed, int floor, int spinIndex)
        {
            unchecked
            {
                uint h = (uint)runSeed;
                h ^= (uint)floor * FloorStride;
                h = Mix(h);
                h ^= (uint)spinIndex * SpinStride;
                h = Mix(h);

                // System.Random(int.MinValue)은 Math.Abs에서 터진다. 음수 시드 자체는
                // 유효하지만 최솟값만 피해두면 호출부가 방어할 필요가 없어진다.
                int seed = (int)h;
                return seed == int.MinValue ? int.MaxValue : seed;
            }
        }

        /// <summary>
        /// murmur3 finalizer. 상위·하위 비트를 골고루 섞어 인접한 입력이 인접한 출력을
        /// 만들지 않게 한다 — 층 1과 층 2의 판이 비슷해 보이면 결정론이 아니라 패턴이 된다.
        /// </summary>
        private static uint Mix(uint h)
        {
            unchecked
            {
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
