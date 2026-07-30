using Ascend.Prototype.Spin;

namespace Ascend.Prototype.Run
{
    /// <summary>
    /// 스핀 결과를 시간축에 펼치는 연출자. 상호작용 브리지가 "지금 연출 중이라 입력을 막아야
    /// 하는가"를 물어보는 유일한 창구다.
    ///
    /// 연출자는 <see cref="SpinResolution"/>을 **읽기만 한다.** 판정은 이미 끝났고, 연출이
    /// 결과를 바꿀 수 있으면 시드 재현이 무너진다(`TECH_SPEC.md` §5).
    /// 연출자가 없어도 게임은 돌아야 한다 — 그래서 브리지는 이 참조가 null인 경우를 허용한다.
    /// </summary>
    public interface ISpinPresentation
    {
        /// <summary>재생 중인가. true인 동안 브리지는 모든 상호작용을 잠근다.</summary>
        bool IsPresenting { get; }

        /// <summary>확정된 결과를 재생한다.</summary>
        void Present(SpinResolution resolution);

        /// <summary>층·런이 바뀌어 화면을 비워야 할 때.</summary>
        void Clear();
    }
}
