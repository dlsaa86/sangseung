using System;

namespace Ascend.Prototype.Events
{
    /// <summary>
    /// 사건 하나의 전부. 구독자마다 다른 필드를 읽는다 —
    /// 텔레메트리는 숫자를, 사운드는 종류를, 승객 반응은 종류와 강도를 본다.
    ///
    /// `readonly struct`인 이유: 사건은 스핀당 수십 개가 오간다. 클래스로 두면
    /// 프레임당 할당이 생기고 `MASTER_PRD.md` §13.2의 "워밍업 후 매 프레임 0 B"가
    /// 구조적으로 불가능해진다. <see cref="Payload"/>만 예외로 박싱을 허용하되,
    /// 그 필드를 쓰는 사건은 스핀 경계에서만 발행한다(프레임마다가 아니다).
    /// </summary>
    public readonly struct GameEvent
    {
        public readonly GameEventKind Kind;

        /// <summary>층 번호. 층과 무관한 사건이면 0.</summary>
        public readonly int Floor;

        /// <summary>그 층의 몇 번째 스핀인가(0부터). 스핀과 무관하면 −1.</summary>
        public readonly int SpinIndex;

        /// <summary>사건별 정수 값. 뜻은 <see cref="GameEventKind"/>의 각 항목이 정의한다.</summary>
        public readonly int IntValue;

        /// <summary>사건별 실수 값. 대개 전력이나 비율.</summary>
        public readonly float FloatValue;

        /// <summary>사람이 읽는 짧은 라벨. 없으면 null.</summary>
        public readonly string Text;

        /// <summary>
        /// 구조체에 담을 수 없는 큰 데이터. 박싱되므로 **스핀 경계 사건에만** 쓴다.
        /// 지금 쓰는 곳은 <see cref="GameEventKind.SpinResolved"/> 하나다.
        /// </summary>
        public readonly object Payload;

        public GameEvent(GameEventKind kind, int floor = 0, int spinIndex = -1,
            int intValue = 0, float floatValue = 0f, string text = null, object payload = null)
        {
            Kind = kind;
            Floor = floor;
            SpinIndex = spinIndex;
            IntValue = intValue;
            FloatValue = floatValue;
            Text = text;
            Payload = payload;
        }

        public override string ToString() =>
            $"{Kind} floor={Floor} spin={SpinIndex} i={IntValue} f={FloatValue:0.##}" +
            (Text != null ? $" \"{Text}\"" : string.Empty);
    }

    /// <summary>
    /// 사건을 발행하고 나눠 주는 곳. 순수 C#이라 씬 없이도 돈다 —
    /// 헤드리스 테스트와 시뮬레이터가 같은 사건 흐름을 볼 수 있어야
    /// 텔레메트리를 씬 없이 검증할 수 있다(`TECH_SPEC.md` §2).
    ///
    /// 소유자는 <c>RunSession</c>이다. 정적 싱글턴으로 두지 않는 이유:
    /// 런을 재시작하면 옛 구독자가 살아남아 두 런의 사건이 한 파일에 섞인다.
    /// 런이 죽으면 버스도 같이 죽어야 한다.
    /// </summary>
    public sealed class GameEventBus
    {
        /// <summary>사건 하나가 발행될 때마다 호출된다. 예외를 던지는 구독자는 다른 구독자를 막지 않는다.</summary>
        public event Action<GameEvent> Published;

        /// <summary>발행된 사건 수. 테스트가 "정말로 흘렀는가"를 물을 때 쓴다.</summary>
        public int PublishedCount { get; private set; }

        public void Publish(in GameEvent e)
        {
            PublishedCount++;
            Action<GameEvent> handlers = Published;
            if (handlers == null) return;

            // 구독자 하나가 던진 예외로 나머지 채널이 통째로 죽지 않게 한다.
            // 사운드가 실패해도 텔레메트리는 기록되어야 한다.
            foreach (Delegate d in handlers.GetInvocationList())
            {
                try { ((Action<GameEvent>)d)(e); }
                catch (Exception ex) { LastError = ex; ErrorCount++; }
            }
        }

        /// <summary>구독자가 던진 마지막 예외. 조용히 삼키지 않기 위한 것이다(`TECH_SPEC.md` §2).</summary>
        public Exception LastError { get; private set; }
        public int ErrorCount { get; private set; }

        public void Publish(GameEventKind kind, int floor = 0, int spinIndex = -1,
            int intValue = 0, float floatValue = 0f, string text = null, object payload = null)
        {
            var e = new GameEvent(kind, floor, spinIndex, intValue, floatValue, text, payload);
            Publish(in e);
        }

        /// <summary>모든 구독을 끊는다. 런이 끝나거나 다시 시작할 때 호출한다.</summary>
        public void Clear()
        {
            Published = null;
            PublishedCount = 0;
            ErrorCount = 0;
            LastError = null;
        }
    }
}
