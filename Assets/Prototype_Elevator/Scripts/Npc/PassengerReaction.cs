using System;
using UnityEngine;

namespace Ascend.Prototype.Npc
{
    /// <summary>
    /// 승객의 자세. `MASTER_PRD.md` §9.3이 요구하는 표현 채널 중 **정지 화면에서 유일하게
    /// 남는 것**이다 — 흔들림과 소리는 고정 캡처에 찍히지 않는다(`BuildFigureView.ReactToRisk`가
    /// 같은 이유로 기울기를 상수 절반으로 둔다).
    ///
    /// 실루엣이 서로 달라야 한다(§4 "큰 실루엣"). 색을 빼고 봐도 웅크린 사람과
    /// 버티는 사람이 구분되지 않으면 이 열거형은 값이 하나인 것과 같다.
    /// </summary>
    public enum ReactionPose
    {
        /// <summary>기본. 반응이 끝나면 여기로 돌아온다.</summary>
        Idle = 0,

        /// <summary>몸을 앞으로 기울여 들여다본다. 관심이지 공포가 아니다.</summary>
        Lean = 1,

        /// <summary>움찔한다. 짧고 날카롭다.</summary>
        Flinch = 2,

        /// <summary>웅크린다. 가장 작아지는 자세 — Critical·Collapse 전용이다.</summary>
        Cower = 3,

        /// <summary>한 곳을 굳어서 본다. 움직이지 않는 것이 정보다.</summary>
        Stare = 4,

        /// <summary>발을 벌리고 무언가를 붙잡는다. 충격을 예상한 자세.</summary>
        Brace = 5,

        /// <summary>환호. 이 프로토타입에서 유일하게 위를 향하는 자세다.</summary>
        Cheer = 6,
    }

    /// <summary>
    /// 시선 대상. §9.3의 「시선」 채널이다.
    ///
    /// 좌표가 아니라 **의미**로 둔다 — 장치와 레버의 실제 위치는 씬 배치에 달려 있고
    /// 아직 확정되지 않았다(`ASSUMPTION_LOG`). 반응 데이터가 좌표를 들고 있으면
    /// 장치를 30cm 옮길 때마다 반응 에셋을 전부 고쳐야 한다.
    /// </summary>
    public enum ReactionGaze
    {
        /// <summary>시선을 옮기지 않는다.</summary>
        None = 0,

        /// <summary>룰렛 장치. 대부분의 성공 반응이 여기를 본다.</summary>
        Device = 1,

        /// <summary>플레이어. "당신이 결정한다"는 뜻이 실리는 유일한 대상이다.</summary>
        Player = 2,

        /// <summary>과수확 레버. §7이 요구하는 "공간적 사건"의 시선 채널.</summary>
        OverharvestLever = 3,

        /// <summary>문. 나갈 수 있는가를 묻는 시선.</summary>
        Door = 4,

        /// <summary>천장. 무너질 것을 올려다본다.</summary>
        Ceiling = 5,
    }

    /// <summary>
    /// 반응 하나의 정의. `MASTER_PRD.md` §9.4가 "반응은 `PassengerReactionSet` 데이터로
    /// 이벤트별 교체 가능"을 요구하므로 **값은 전부 필드다** — 코드에 상수로 박으면
    /// 교체에 코드 수정이 필요해진다. `RiskProfile`이 같은 이유로 같은 모양을 하고 있다.
    ///
    /// 최종 모션·대사·음성은 승인 전까지 교체 가능한 플레이스홀더로 유지한다(§8 마지막 줄).
    /// 그래서 여기 있는 것은 실제 클립이 아니라 **큐 ID**뿐이다.
    /// </summary>
    [Serializable]
    public struct PassengerReaction
    {
        [Tooltip("자세. 정지 화면에서 읽히는 유일한 채널이다.")]
        public ReactionPose Pose;

        [Tooltip("시선 대상. 좌표가 아니라 의미다 — 씬 배치가 바뀌어도 데이터는 그대로다.")]
        public ReactionGaze Gaze;

        [Tooltip("비언어 음성 큐 ID. 실제 클립은 오디오 쪽이 이 ID로 찾는다. 비면 소리 없음.")]
        public string VoiceCue;

        [Tooltip("짧은 대사 한 줄. §9.4가 긴 대화 트리를 제외하므로 1문장 이하다.")]
        public string Line;

        [Tooltip("반응이 유지되는 초. 0 이하는 '이 이벤트에는 반응하지 않는다'는 뜻이다.")]
        public float Duration;

        [Tooltip("강도 0~1. 자세 진폭과 음량이 여기서 갈린다.")]
        [Range(0f, 1f)] public float Intensity;

        [Tooltip("우선순위. 클수록 우선하며, 진행 중인 낮은 반응을 덮어쓸 수 있다(§9.4).")]
        public int Priority;

        [Tooltip("같은 승객이 다시 반응하기까지의 초. 반응 시작 시점부터 잰다.")]
        public float Cooldown;

        /// <summary>
        /// 대사 없는 반응. **기존 호출부를 깨지 않으려고 남긴다** — 이 프로젝트에는
        /// asmdef이 없어서 생성자 하나를 바꾸면 전체 컴파일이 막힌다.
        /// </summary>
        public PassengerReaction(ReactionPose pose, ReactionGaze gaze, string voiceCue,
                                 float duration, float intensity, int priority, float cooldown)
            : this(pose, gaze, voiceCue, null, duration, intensity, priority, cooldown)
        {
        }

        public PassengerReaction(ReactionPose pose, ReactionGaze gaze, string voiceCue, string line,
                                 float duration, float intensity, int priority, float cooldown)
        {
            Pose = pose;
            Gaze = gaze;
            VoiceCue = voiceCue;
            Line = line;
            Duration = duration;
            Intensity = intensity;
            Priority = priority;
            Cooldown = cooldown;
        }

        /// <summary>
        /// 실제로 무언가를 하는 반응인가. 지속 시간 0을 "없음"으로 읽는 것은
        /// 데이터로 반응을 **끄는** 수단이기도 하다 — 항목을 지우면 코드 기본값으로
        /// 폴백되므로 지우는 것으로는 끌 수 없다(<see cref="PassengerReactionSet.For"/>).
        /// </summary>
        public bool IsActive => Duration > 0f;

        /// <summary>화면에 띄울 대사가 있는가.</summary>
        public bool HasLine => !string.IsNullOrEmpty(Line);

        /// <summary>소리를 낼 큐가 있는가. 비어 있으면 데이터가 이 반응의 음성을 끈 것이다.</summary>
        public bool HasVoice => !string.IsNullOrEmpty(VoiceCue);

        public override string ToString() =>
            $"{Pose}/{Gaze} {Duration:0.##}s i={Intensity:0.##} p={Priority} cd={Cooldown:0.##}" +
            (string.IsNullOrEmpty(VoiceCue) ? string.Empty : $" \"{VoiceCue}\"") +
            (string.IsNullOrEmpty(Line) ? string.Empty : $" 「{Line}」");
    }

    /// <summary>
    /// 같은 사건에 대한 **다른 쪽 반응**. `MASTER_PRD.md` §9.3 마지막 줄이 요구하는
    /// "승객마다 상반된 반응"의 데이터 형태다.
    ///
    /// 왜 <see cref="PassengerReaction"/> 을 통째로 하나 더 두지 않는가:
    /// 지속·우선순위·쿨다운은 **중재 규칙의 입력**이다(UP-NPC-05, 이미 VERIFIED).
    /// 대조 반응이 그 셋을 따로 들면 같은 사건인데 사람마다 반응이 다른 시각에 끝나고,
    /// 우선순위가 갈려 누구는 덮이고 누구는 안 덮인다 — 중재기의 계약이 조용히 깨진다.
    /// 그래서 여기에는 **표현 채널 넷만** 있고 타이밍은 대표 반응에서 그대로 물려받는다.
    ///
    /// 정의되지 않은 대조(<see cref="IsDefined"/> == false)는 "전원이 같은 반응"이라는
    /// 뜻이다. 대조를 끄고 싶으면 자세·시선을 대표 반응과 같게 두면 된다.
    /// </summary>
    [Serializable]
    public struct PassengerReactionContrast
    {
        [Tooltip("대조 자세. 대표 반응과 실루엣이 갈려야 의미가 있다.")]
        public ReactionPose Pose;

        [Tooltip("대조 시선. 같은 사건을 다른 곳에서 찾는다.")]
        public ReactionGaze Gaze;

        [Tooltip("대조 음성 큐 ID. 비면 대표 반응의 큐를 그대로 쓴다.")]
        public string VoiceCue;

        [Tooltip("대조 대사 한 줄. 비면 대표 반응의 대사를 그대로 쓴다.")]
        public string Line;

        [Tooltip("대조 강도 0~1. 0이면 대표 반응의 강도를 물려받는다.")]
        [Range(0f, 1f)] public float Intensity;

        public PassengerReactionContrast(ReactionPose pose, ReactionGaze gaze,
                                         string voiceCue, string line, float intensity)
        {
            Pose = pose;
            Gaze = gaze;
            VoiceCue = voiceCue;
            Line = line;
            Intensity = intensity;
        }

        /// <summary>
        /// 이 대조가 실제로 무언가를 바꾸는가.
        ///
        /// 자세·시선이 기본값이고 문자열도 비어 있으면 편집자가 아직 채우지 않은 것이다 —
        /// 기존 `.asset`은 이 필드가 생기기 전에 직렬화됐으므로 전부 이 상태로 읽힌다.
        /// </summary>
        public bool IsDefined =>
            Pose != ReactionPose.Idle || Gaze != ReactionGaze.None ||
            !string.IsNullOrEmpty(VoiceCue) || !string.IsNullOrEmpty(Line);

        /// <summary>
        /// 대표 반응 위에 대조를 얹는다. **타이밍 셋은 건드리지 않는다** —
        /// 그래야 중재기가 어느 쪽을 골랐든 같은 규칙으로 돈다.
        /// </summary>
        public PassengerReaction ApplyTo(in PassengerReaction primary)
        {
            if (!IsDefined) return primary;
            return new PassengerReaction(
                Pose,
                Gaze,
                string.IsNullOrEmpty(VoiceCue) ? primary.VoiceCue : VoiceCue,
                string.IsNullOrEmpty(Line) ? primary.Line : Line,
                primary.Duration,
                Intensity > 0f ? Intensity : primary.Intensity,
                primary.Priority,
                primary.Cooldown);
        }
    }
}
