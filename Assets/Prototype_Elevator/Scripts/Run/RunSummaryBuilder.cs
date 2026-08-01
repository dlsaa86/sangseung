using System;
using System.Collections.Generic;
using System.Text;
using Ascend.Prototype.Data.Profiles;

namespace Ascend.Prototype.Run
{
    /// <summary>
    /// 런 요약 9종(`MASTER_PRD.md` §10.2 / `UP-REC-02`)을 조립한다.
    ///
    /// **왜 이 파일이 필요했는가**: `RunSummaryTemplate` 은 문장 서식만 들고 있고
    /// 값을 채우는 쪽이 저장소에 **0곳**이었다. 그래서 프로파일 8종 중 이것만 아무도
    /// 읽지 않았고, 「9종이 전부 나오는지 검증되지 않았다」가 백로그의 남은 문제로 남았다.
    ///
    /// **단일 원본을 쓴다**(`UP-REC-03`). 값은 전부 `AccidentRecorder` 가 만든
    /// <see cref="FloorRecord"/> 목록과 <see cref="RunSession"/> 에서만 온다 —
    /// 인게임 화면과 디버그 출력이 서로 다른 집계를 갖지 않게 하려는 것이다.
    /// 여기서 새로 세는 것은 「런 전체의 최대값」뿐이고, 그 재료도 전부 기록에서 나온다.
    ///
    /// **`UP-FIX-11` 이 잡은 함정을 피한다.** 그 결함은 확정 시점 스냅샷(`Loadout`)과
    /// 라이브 값(`CarriedWeight`·`Overloaded`)을 한 화면에 나란히 놓아 둘이 서로를
    /// 부정한 것이었다. 그래서 이 요약은 **무게를 아예 다루지 않고**, 적재는 기록에
    /// 박제된 문자열(`FloorRecord.Loadout`)만 쓴다. 그리고 <see cref="Build"/> 는
    /// 한 번의 호출 안에서 모든 값을 읽어 불변 구조체로 굳힌다 — 반환된
    /// <see cref="RunSummaryData"/> 는 그 순간의 스냅샷이고 이후 런이 움직여도 변하지 않는다.
    ///
    /// 순수 C# 이다. 씬 없이 만들 수 있어야 헤드리스 테스트와 텔레메트리가 같은 것을 본다.
    /// </summary>
    public static class RunSummaryBuilder
    {
        public const string NoneText = "없음";
        public const string NoContractText = "계약 없음";
        public const string NoOverharvestText = "과수확 없음 — 달성 시점에 확정";
        public const string InProgressText = "진행 중";
        public const string CompleteText = "완주";
        public const string UnknownAccidentText = "사고 — 원인 미상";

        /// <summary>
        /// 9종을 채운 스냅샷을 만든다. <paramref name="run"/> 이 null 이면 전 항목이
        /// 비고, 서식이 그 자리에 「기록 없음」을 넣는다 — 줄 수는 줄지 않는다.
        /// </summary>
        public static RunSummaryData Build(RunSession run, IReadOnlyList<FloorRecord> records)
        {
            if (run == null)
                return new RunSummaryData(0, 0, 0f, null, null, null, null, null, 0);

            int count = records != null ? records.Count : 0;

            int highestFloor = run.HighestFloorReached;
            int peakCascade = 0;
            float peakOverharvestRatio = 0f;

            // 계약·적재는 「가장 오래 들고 간 것」을 핵심으로 본다. 한 층만 잘 나온
            // 조합보다 런을 관통한 조합이 그 런을 설명하기 때문이다.
            var contractKeys = new List<string>(4);
            var contractCounts = new List<int>(4);
            var contractBestRatio = new List<float>(4);

            var loadoutKeys = new List<string>(4);
            var loadoutCounts = new List<int>(4);

            string lastOverharvest = null;
            string lostCargo = null;
            int lostCargoCount = 0;
            string lostCargoLast = null;

            for (int i = 0; i < count; i++)
            {
                FloorRecord record = records[i];
                if (record == null) continue;

                if (record.Floor > highestFloor) highestFloor = record.Floor;

                // 캐스케이드 최고 단계는 스핀 단위로만 존재한다. 층 기록에는 없다.
                var spins = record.Spins;
                if (spins != null)
                {
                    for (int s = 0; s < spins.Count; s++)
                    {
                        int depth = spins[s].Resolution.ChainDepth;
                        if (depth > peakCascade) peakCascade = depth;
                    }
                }

                float ratio = record.FinalPower / Math.Max(1f, record.RequiredPower);

                // 「최고 과수확 비율」은 **과수확한 층에서만** 센다. 과수확 없이 높이
                // 나온 층을 여기 넣으면 이 항목이 「최고 달성 비율」로 뜻이 바뀐다.
                if (record.ExtraSpinsTaken > 0)
                {
                    if (ratio > peakOverharvestRatio) peakOverharvestRatio = ratio;
                    lastOverharvest = DescribeOverharvest(record);
                }

                Tally(contractKeys, contractCounts, contractBestRatio,
                      record.Contract, ratio, NoContractText);
                Tally(loadoutKeys, loadoutCounts, null, record.Loadout, ratio, NoneText);

                if (!string.IsNullOrEmpty(record.Jettison))
                {
                    lostCargoCount++;
                    if (lostCargo == null) lostCargo = record.Jettison;
                    lostCargoLast = record.Jettison;
                }
            }

            // 진행 중인 층도 도달한 층이다. 1층에서 실패하면 `HighestFloorReached` 는
            // 아직 0 이라, 그것만 쓰면 「최고 층 0층」이 나온다.
            if (run.Current != null && run.Current.Plan.Floor > highestFloor)
                highestFloor = run.Current.Plan.Floor;

            return new RunSummaryData(
                highestFloor: highestFloor,
                peakCascade: peakCascade,
                peakOverharvestRatio: peakOverharvestRatio,
                keyContract: Best(contractKeys, contractCounts, contractBestRatio) ?? NoContractText,
                keyLoadout: Best(loadoutKeys, loadoutCounts, null) ?? NoneText,
                endCause: DescribeEnd(run, records, count),
                lastOverharvestChoice: lastOverharvest ?? NoOverharvestText,
                lostCargo: DescribeLostCargo(lostCargo, lostCargoLast, lostCargoCount),
                runSeed: run.Seed);
        }

        /// <summary>
        /// 서식을 통과시킨 9줄. <paramref name="template"/> 이 비어 있어도 코드 기본
        /// 서식으로 9줄이 나온다 — 배선을 잊었다고 요약이 사라지지는 않는다.
        /// </summary>
        public static string[] ComposeLines(RunSummaryTemplate template,
                                            RunSession run, IReadOnlyList<FloorRecord> records)
        {
            RunSummarySnapshot snapshot =
                RunSummaryTemplate.SnapshotOrDefault(template, nameof(RunSummaryBuilder));
            RunSummaryData data = Build(run, records);
            return snapshot.ComposeLines(in data);
        }

        /// <summary>줄바꿈으로 이은 9줄. 콘솔·텔레메트리용.</summary>
        public static string Compose(RunSummaryTemplate template,
                                     RunSession run, IReadOnlyList<FloorRecord> records)
        {
            RunSummarySnapshot snapshot =
                RunSummaryTemplate.SnapshotOrDefault(template, nameof(RunSummaryBuilder));
            RunSummaryData data = Build(run, records);
            return snapshot.Compose(in data);
        }

        /// <summary>
        /// 종료 원인. 실패 사유는 `AscendResult` 가 영어로 하드코딩해 두었으므로 여기서
        /// 사람이 읽는 말로 바꾼다(<see cref="FloorRecord"/> 가 같은 이유로 같은 짓을 한다 —
        /// 그쪽 매핑이 private 이라 부를 수 없다. **둘이 갈라지면 안 되므로 표를
        /// 공용으로 올리는 것이 다음 정리 대상이다**).
        /// </summary>
        private static string DescribeEnd(RunSession run, IReadOnlyList<FloorRecord> records, int count)
        {
            if (run.IsFailed)
            {
                string reason = run.FailureReason;
                if (string.IsNullOrEmpty(reason) && count > 0)
                    reason = records[count - 1] != null ? records[count - 1].FailureReason : null;
                if (string.IsNullOrEmpty(reason)) return UnknownAccidentText;

                int floor = count > 0 && records[count - 1] != null ? records[count - 1].Floor : 0;
                string localized = LocalizeFailure(reason);
                return floor > 0 ? floor + "층 · " + localized : localized;
            }

            if (run.IsComplete) return CompleteText;
            return InProgressText;
        }

        private static string LocalizeFailure(string reason)
        {
            switch (reason)
            {
                case "Crash":             return "추락 — 요구 전력의 70% 미만";
                case "Jettison required": return "화물 포기 — 요구 전력의 70~89%";
                default:                  return reason;
            }
        }

        /// <summary>
        /// 마지막 과수확 선택. 「몇 번 더 당겼는가」와 「그래서 이득이었는가」를 함께 적는다 —
        /// 횟수만으로는 그 선택이 옳았는지 되짚을 수 없다.
        ///
        /// 기호를 쓰지 않는다. `FloorRecord` 가 `⚠` 두부 글자로 두 번 지적당했고
        /// (`UP-FIX-17`), 부호도 같은 위험이 있어 ASCII `-` 로 적는다.
        /// </summary>
        private static string DescribeOverharvest(FloorRecord record)
        {
            var sb = new StringBuilder(64);
            sb.Append(record.Floor).Append("층 — 과수확 ").Append(record.ExtraSpinsTaken).Append("회 · 순손익 ");
            sb.Append(record.NetProfit.ToString("+0;-0;0"));
            return sb.ToString();
        }

        /// <summary>
        /// 잃은 승객·화물. 여러 번 잃었으면 전부 늘어놓지 않는다 — 결과 화면은 9줄이고,
        /// 한 줄이 줄바꿈으로 세 줄이 되면 9줄 규약이 화면에서 깨진다.
        /// </summary>
        private static string DescribeLostCargo(string first, string last, int occurrences)
        {
            if (occurrences <= 0) return NoneText;
            if (occurrences == 1) return first;
            return last + "  (그 외 " + (occurrences - 1) + "건)";
        }

        /// <summary>
        /// 빈도를 센다. <paramref name="ignored"/> 와 같거나 비어 있는 값은 세지 않는다 —
        /// 「계약 없음」이 가장 흔하다고 해서 그것이 그 런의 핵심 계약은 아니다.
        /// </summary>
        private static void Tally(List<string> keys, List<int> counts, List<float> bestRatio,
                                  string value, float ratio, string ignored)
        {
            if (string.IsNullOrEmpty(value)) return;
            if (string.Equals(value, ignored, StringComparison.Ordinal)) return;

            for (int i = 0; i < keys.Count; i++)
            {
                if (!string.Equals(keys[i], value, StringComparison.Ordinal)) continue;
                counts[i] = counts[i] + 1;
                if (bestRatio != null && ratio > bestRatio[i]) bestRatio[i] = ratio;
                return;
            }

            keys.Add(value);
            counts.Add(1);
            bestRatio?.Add(ratio);
        }

        /// <summary>가장 자주 나온 것. 동률이면 달성 비율이 높았던 쪽이 이긴다.</summary>
        private static string Best(List<string> keys, List<int> counts, List<float> bestRatio)
        {
            int bestIndex = -1;
            for (int i = 0; i < keys.Count; i++)
            {
                if (bestIndex < 0 || counts[i] > counts[bestIndex])
                {
                    bestIndex = i;
                    continue;
                }
                if (counts[i] == counts[bestIndex] && bestRatio != null && bestRatio[i] > bestRatio[bestIndex])
                    bestIndex = i;
            }
            return bestIndex < 0 ? null : keys[bestIndex];
        }
    }
}

// 씬 배선 필요:
//   `GameHudView` 의 `_summaryTemplate` 에
//   `Assets/Prototype_Elevator/Data/Profiles/RunSummaryTemplate.asset` 를 물린다.
//   비어 있어도 코드 기본 서식으로 9줄이 나오지만 경고가 남고, 문장을 데이터로 고칠 수 없다.
