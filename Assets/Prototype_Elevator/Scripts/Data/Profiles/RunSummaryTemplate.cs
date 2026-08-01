using System;
using UnityEngine;

namespace Ascend.Prototype.Data.Profiles
{
    /// <summary>
    /// 런 요약이 반드시 담아야 하는 9종. `MASTER_PRD.md` §10이 요구하는 것은 결과 나열이
    /// 아니라 **설명 가능성**이고, 백로그 `UP-REC-02`의 남은 문제가 "항목 대조표가 없다.
    /// 9종이 전부 나오는지 검증되지 않았다"였다.
    ///
    /// 열거형으로 고정하는 이유: 문자열 서식만 있으면 한 항목이 빠져도 화면은 멀쩡해 보인다.
    /// 항목을 값으로 세어야 테스트가 "9줄이 나왔는가"를 물을 수 있다.
    /// </summary>
    public enum RunSummaryField
    {
        HighestFloor = 0,
        PeakCascade = 1,
        PeakOverharvestRatio = 2,
        KeyContract = 3,
        KeyLoadout = 4,
        EndCause = 5,
        LastOverharvestChoice = 6,
        LostCargo = 7,
        RunSeed = 8,
    }

    /// <summary>
    /// 런 요약 9종의 실제 값. 순수 값이라 씬 없이 만들 수 있고, 텔레메트리와 화면이
    /// 같은 것을 쓴다(`MASTER_PRD.md` §10.3 「인게임 출력과 디버그가 같은 데이터」).
    /// </summary>
    public readonly struct RunSummaryData
    {
        public readonly int HighestFloor;
        public readonly int PeakCascade;

        /// <summary>달성률 기준 최고 과수확 비율. 1.34면 요구 전력의 134%.</summary>
        public readonly float PeakOverharvestRatio;

        public readonly string KeyContract;
        public readonly string KeyLoadout;
        public readonly string EndCause;
        public readonly string LastOverharvestChoice;

        /// <summary>잃은 승객·화물. **아무것도 잃지 않았으면 "없음"을 넣는다** — 아래 주석 참조.</summary>
        public readonly string LostCargo;

        public readonly int RunSeed;

        public RunSummaryData(int highestFloor, int peakCascade, float peakOverharvestRatio,
            string keyContract, string keyLoadout, string endCause, string lastOverharvestChoice,
            string lostCargo, int runSeed)
        {
            HighestFloor = highestFloor;
            PeakCascade = peakCascade;
            PeakOverharvestRatio = peakOverharvestRatio;
            KeyContract = keyContract;
            KeyLoadout = keyLoadout;
            EndCause = endCause;
            LastOverharvestChoice = lastOverharvestChoice;
            LostCargo = lostCargo;
            RunSeed = runSeed;
        }

        /// <summary>서식에 넘길 값. 런 종료에 한 번 도는 경로라 박싱을 허용한다.</summary>
        public object ValueFor(RunSummaryField field)
        {
            switch (field)
            {
                case RunSummaryField.HighestFloor:          return HighestFloor;
                case RunSummaryField.PeakCascade:           return PeakCascade;
                case RunSummaryField.PeakOverharvestRatio:  return PeakOverharvestRatio;
                case RunSummaryField.KeyContract:           return KeyContract;
                case RunSummaryField.KeyLoadout:            return KeyLoadout;
                case RunSummaryField.EndCause:              return EndCause;
                case RunSummaryField.LastOverharvestChoice: return LastOverharvestChoice;
                case RunSummaryField.LostCargo:             return LostCargo;
                default:                                    return RunSeed;
            }
        }
    }

    /// <summary>
    /// 런 요약의 라벨과 서식. `UP-REC-02` / `UP-TECH-09`.
    ///
    /// 문장을 코드에서 빼는 것이 목적이다 — 지금은 `FloorRecord.Summary()`에 한국어 문장이
    /// 박혀 있어서, 표현을 고치려면 순수 C# 판정 코드를 건드려야 한다.
    /// </summary>
    [CreateAssetMenu(fileName = "RunSummaryTemplate",
                     menuName = "Ascend/Profiles/Run Summary Template", order = 106)]
    public sealed class RunSummaryTemplate : ScriptableObject
    {
        /// <summary>`MASTER_PRD.md` §10이 요구하는 항목 수. 테스트가 이 수로 대조한다.</summary>
        public const int RequiredFieldCount = 9;

        public const string DefaultMissingText = "기록 없음";

        [Header("공통")]
        [Tooltip("라벨과 값 사이에 넣는 구분자.")]
        [SerializeField] private string _separator = "  ";

        [Tooltip("값이 비어 있을 때 대신 넣는 말. 「없음」이 아니라 「기록 없음」인 이유는 아래 주석 참조.")]
        [SerializeField] private string _missingText = DefaultMissingText;

        [Header("1. 최고 층")]
        [SerializeField] private string _highestFloorLabel = "최고 층";
        [SerializeField] private string _highestFloorFormat = "{0}층";

        [Header("2. 최고 캐스케이드")]
        [SerializeField] private string _peakCascadeLabel = "최고 캐스케이드";
        [SerializeField] private string _peakCascadeFormat = "{0}단계";

        [Header("3. 최고 과수확 비율")]
        [SerializeField] private string _peakOverharvestLabel = "최고 과수확";
        [SerializeField] private string _peakOverharvestFormat = "요구 전력의 {0:P0}";

        [Header("4. 핵심 계약")]
        [SerializeField] private string _keyContractLabel = "핵심 계약";
        [SerializeField] private string _keyContractFormat = "{0}";

        [Header("5. 핵심 승객·부품")]
        [SerializeField] private string _keyLoadoutLabel = "핵심 승객·부품";
        [SerializeField] private string _keyLoadoutFormat = "{0}";

        [Header("6. 종료 원인")]
        [SerializeField] private string _endCauseLabel = "종료 원인";
        [SerializeField] private string _endCauseFormat = "{0}";

        [Header("7. 마지막 과수확 선택")]
        [SerializeField] private string _lastOverharvestLabel = "마지막 선택";
        [SerializeField] private string _lastOverharvestFormat = "{0}";

        [Header("8. 잃은 승객·화물")]
        [SerializeField] private string _lostCargoLabel = "잃은 것";
        [SerializeField] private string _lostCargoFormat = "{0}";

        [Header("9. 런 시드")]
        [SerializeField] private string _runSeedLabel = "런 시드";
        [SerializeField] private string _runSeedFormat = "{0}";

        public void Reset()
        {
            _separator = "  ";
            _missingText = DefaultMissingText;

            _highestFloorLabel = "최고 층";           _highestFloorFormat = "{0}층";
            _peakCascadeLabel = "최고 캐스케이드";    _peakCascadeFormat = "{0}단계";
            _peakOverharvestLabel = "최고 과수확";    _peakOverharvestFormat = "요구 전력의 {0:P0}";
            _keyContractLabel = "핵심 계약";          _keyContractFormat = "{0}";
            _keyLoadoutLabel = "핵심 승객·부품";      _keyLoadoutFormat = "{0}";
            _endCauseLabel = "종료 원인";             _endCauseFormat = "{0}";
            _lastOverharvestLabel = "마지막 선택";    _lastOverharvestFormat = "{0}";
            _lostCargoLabel = "잃은 것";              _lostCargoFormat = "{0}";
            _runSeedLabel = "런 시드";                _runSeedFormat = "{0}";
        }

        public RunSummarySnapshot Snapshot()
        {
            var labels = new string[RequiredFieldCount];
            var formats = new string[RequiredFieldCount];

            labels[0] = _highestFloorLabel;        formats[0] = _highestFloorFormat;
            labels[1] = _peakCascadeLabel;         formats[1] = _peakCascadeFormat;
            labels[2] = _peakOverharvestLabel;     formats[2] = _peakOverharvestFormat;
            labels[3] = _keyContractLabel;         formats[3] = _keyContractFormat;
            labels[4] = _keyLoadoutLabel;          formats[4] = _keyLoadoutFormat;
            labels[5] = _endCauseLabel;            formats[5] = _endCauseFormat;
            labels[6] = _lastOverharvestLabel;     formats[6] = _lastOverharvestFormat;
            labels[7] = _lostCargoLabel;           formats[7] = _lostCargoFormat;
            labels[8] = _runSeedLabel;             formats[8] = _runSeedFormat;

            return new RunSummarySnapshot(labels, formats, _separator, _missingText);
        }

        public static RunSummarySnapshot DefaultSnapshot
        {
            get
            {
                var labels = new[]
                {
                    "최고 층", "최고 캐스케이드", "최고 과수확", "핵심 계약", "핵심 승객·부품",
                    "종료 원인", "마지막 선택", "잃은 것", "런 시드",
                };
                var formats = new[]
                {
                    "{0}층", "{0}단계", "요구 전력의 {0:P0}", "{0}", "{0}",
                    "{0}", "{0}", "{0}", "{0}",
                };
                return new RunSummarySnapshot(labels, formats, "  ", DefaultMissingText);
            }
        }

        public static RunSummarySnapshot SnapshotOrDefault(RunSummaryTemplate template, string caller)
        {
            if (template != null) return template.Snapshot();
            Debug.LogWarning($"[상승] RunSummaryTemplate 이 배선되지 않았다 ({caller}). 코드 기본 서식으로 진행한다.");
            return DefaultSnapshot;
        }

        public string[] ComposeLines(in RunSummaryData data) => Snapshot().ComposeLines(in data);
        public string Compose(in RunSummaryData data) => Snapshot().Compose(in data);
    }

    /// <summary>런 요약 서식의 값 사본. 조립 로직이 여기 있어서 에셋 없이도 9줄이 나온다.</summary>
    public readonly struct RunSummarySnapshot
    {
        private readonly string[] _labels;
        private readonly string[] _formats;
        private readonly string _separator;
        private readonly string _missingText;

        public RunSummarySnapshot(string[] labels, string[] formats, string separator, string missingText)
        {
            _labels = labels;
            _formats = formats;
            _separator = separator ?? "  ";
            _missingText = string.IsNullOrEmpty(missingText)
                ? RunSummaryTemplate.DefaultMissingText : missingText;
        }

        public string LabelFor(RunSummaryField field) => At(_labels, (int)field, string.Empty);
        public string FormatFor(RunSummaryField field) => At(_formats, (int)field, "{0}");

        /// <summary>
        /// 9줄을 만든다. 값이 비어도 줄 수는 줄지 않는다 — 줄이 사라지면 화면만 보고는
        /// "그 항목이 없었다"인지 "출력이 빠졌다"인지 구분할 수 없고, 그것이 바로
        /// `UP-REC-02`가 지적당한 지점이다.
        /// </summary>
        public string[] ComposeLines(in RunSummaryData data)
        {
            var lines = new string[RunSummaryTemplate.RequiredFieldCount];
            for (int i = 0; i < lines.Length; i++)
            {
                var field = (RunSummaryField)i;
                lines[i] = ComposeLine(field, data.ValueFor(field));
            }
            return lines;
        }

        public string Compose(in RunSummaryData data)
        {
            return string.Join("\n", ComposeLines(in data));
        }

        private string ComposeLine(RunSummaryField field, object value)
        {
            string body;
            if (IsMissing(value))
            {
                // 서식을 통과시키지 않는다. "{0}층"에 빈 값을 넣으면 "층"만 남아
                // 실제 결과처럼 읽힌다.
                body = _missingText;
            }
            else
            {
                string format = FormatFor(field);
                try
                {
                    body = string.Format(format, value);
                }
                catch (FormatException exception)
                {
                    // 서식은 인스펙터에서 손으로 고치는 값이라 깨질 수 있다. 요약 전체를
                    // 날리는 대신 그 줄만 원시 값으로 떨어뜨리고 왜 그랬는지 남긴다.
                    Debug.LogWarning($"[상승] RunSummaryTemplate 의 {field} 서식 '{format}' 이 잘못됐다: {exception.Message}");
                    body = value.ToString();
                }
            }

            string label = LabelFor(field);
            return string.IsNullOrEmpty(label) ? body : label + _separator + body;
        }

        /// <summary>
        /// 값이 "없다"인가. 문자열이 비었을 때만 없는 것으로 본다.
        ///
        /// 0층·시드 0은 **없는 값이 아니라 실제 값**이다. 그리고 "아무것도 잃지 않았다"는
        /// 호출자가 "없음"이라고 적어 넘겨야 한다 — 서식이 빈 문자열을 보고 그 둘을
        /// 구분하려 들면, 요약이 모르는 것을 아는 척하게 된다.
        /// </summary>
        private static bool IsMissing(object value)
        {
            if (value == null) return true;
            var text = value as string;
            return text != null && text.Length == 0;
        }

        private static string At(string[] values, int index, string fallback)
        {
            if (values == null || index < 0 || index >= values.Length) return fallback;
            return values[index] ?? fallback;
        }
    }
}

// 씬 배선 필요:
//   1. `Scripts/UI/GameHudView.cs` 의 사고 기록기 출력이 `FloorRecord.Summary()` 대신
//      (또는 그와 나란히) `RunSummaryTemplate.ComposeLines(...)` 를 쓰도록 바꿔야
//      `UP-REC-02` 의 9종 대조가 화면에서 성립한다.
//   2. `RunSummaryData` 를 채우려면 런 단위 집계가 필요하다 — 최고 캐스케이드와 최고
//      과수확 비율은 현재 `FloorRecord` 에 층 단위로만 있고 런 단위 최대값이 없다.
//      `RunSession`/`AccidentRecorder` 소유자가 누적해야 한다.
// 에셋 생성 필요: Assets/Prototype_Elevator/Data/Profiles/RunSummaryTemplate.asset
