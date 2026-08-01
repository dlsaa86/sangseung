using System.Collections.Generic;

namespace Ascend.Prototype.Build
{
    /// <summary>
    /// 자동 진행이 「무엇을 실을 것인가」를 정하는 규칙. 하네스와 헤드리스 테스트가
    /// **같은 것을** 쓴다.
    ///
    /// 왜 규칙이 따로 필요했나: 기존 하네스는 「몇 개」만 말할 수 있었고
    /// (<c>boardCount</c>), 무엇을 집을지는 후보 번호가 가장 작은 것으로 정했다.
    /// 그 결과 아홉 런 전부에서 칸이 거의 비었다 — 보수 정책 0kg, 공격 정책 최고
    /// 51kg(허용 100kg). 승객이 타지 않으면 승객 반응은 「시작 0건」으로 찍히는데,
    /// 그것은 **고장이 아니라 관측 불가**다. 숫자가 0 이면 구현이 없어서인지
    /// 상태에 도달하지 못해서인지부터 갈라야 한다.
    ///
    /// 이 규칙 하나가 여섯 항목의 증거를 연다 —
    /// <c>UP-BUILD-04</c>(무게가 요구 전력을 민다) · <c>UP-BUILD-11</c>(과적) ·
    /// <c>UP-NPC-02·04·05</c>(반응 종류·표현 채널·동시 반응 제한) ·
    /// <c>UP-SPACE-06</c>(최대 적재 상태의 동선).
    ///
    /// <see cref="UnityEngine"/>에 의존하지 않는다. 헤드리스에서 시드를 먼저 고르고
    /// 그 시드를 씬 하네스가 그대로 쓰려면 두 경로의 선택이 **비트 단위로 같아야** 한다.
    /// 규칙을 두 벌 쓰면 헤드리스가 고른 시드가 씬에서 다른 결과를 낸다.
    /// </summary>
    public static class BuildLoadPolicy
    {
        /// <summary>
        /// 이 수까지는 무게를 제치고 승객을 먼저 태운다.
        ///
        /// 3 인 이유는 <c>UP-NPC-05</c>「동시 반응 제한(우선순위·쿨다운·최대 수)」이
        /// 2 명 이하에서는 의미를 갖지 않기 때문이다. 최대 동시 반응 수가 기본 2 이므로
        /// 세 번째 승객이 있어야 「제한이 실제로 걸렸다」를 관측할 수 있다.
        /// </summary>
        public const int PassengerFloor = 3;

        /// <summary>
        /// 점수가 높을수록 먼저 집는다. 세 가지를 동시에 만족해야 위의 여섯 항목이 열린다.
        ///
        /// <list type="number">
        /// <item>승객 <see cref="PassengerFloor"/>명 이상 — 그래서 그 수까지는 승객이 무조건 앞선다.</item>
        /// <item>허용 중량 초과 — 그래서 무게가 그대로 점수다.</item>
        /// <item>칸이 가득 참 — 그래서 점수가 음수여도 자리가 남으면 집는다.</item>
        /// </list>
        ///
        /// <see cref="BuildItem.CapacityBonus"/>를 **빼는 것이 핵심**이다. 짐꾼은 12kg 으로
        /// 측량사(6kg)보다 무겁지만 허용 중량을 30 늘리므로, 무게만 보면 정책이 스스로
        /// 과적이라는 목표를 지운다.
        ///
        /// 부품에 가산을 주는 이유는 무게가 아니라 **영속성**이다. 승객은 목적지에서 내려
        /// 무게를 도로 가져가지만 부품은 런이 끝날 때까지 남는다. 승객 정원을 채운 뒤에는
        /// 남는 무게가 더 값지다.
        /// </summary>
        public static float Score(BuildItem item, int passengersHeld)
        {
            if (item == null) return float.MinValue;

            float score = item.Weight - item.CapacityBonus;

            if (item.Kind == BuildItemKind.Passenger)
            {
                // 정원을 채우기 전에는 무게와 무관하게 승객이 앞선다. 100 은 카탈로그의
                // 어떤 무게 차이보다 크다(최대 26kg).
                score += passengersHeld < PassengerFloor ? 100f : item.DestinationFloor;
            }
            else
            {
                score += 20f;
            }

            return score;
        }

        /// <summary>지금 실려 있는 승객 수. 부품은 세지 않는다.</summary>
        public static int CountPassengers(BuildLoadout loadout)
        {
            if (loadout == null) return 0;
            int count = 0;
            IReadOnlyList<BuildItem> items = loadout.Items;
            for (int i = 0; i < items.Count; i++)
                if (items[i].Kind == BuildItemKind.Passenger) count++;
            return count;
        }

        /// <summary>
        /// 지금 집어야 할 후보의 번호. 자리가 없거나 후보가 없으면 −1.
        ///
        /// 동점은 **번호가 작은 쪽**으로 깬다. 하네스가 결정론을 잃으면 게임의 결정론을
        /// 잴 수 없다 — 이 프로젝트는 이미 한 번 그렇게 당했다(같은 시드·같은 정책인데
        /// 한 번은 5층 사고, 한 번은 10층 도달).
        /// </summary>
        public static int PickIndex(IReadOnlyList<BuildItem> offers, BuildLoadout carried)
        {
            if (offers == null || offers.Count == 0) return -1;
            if (carried != null && carried.IsFull) return -1;

            int held = CountPassengers(carried);
            int best = -1;
            float bestScore = float.MinValue;

            for (int i = 0; i < offers.Count; i++)
            {
                float score = Score(offers[i], held);
                if (best >= 0 && score <= bestScore) continue;
                best = i;
                bestScore = score;
            }
            return best;
        }
    }
}
