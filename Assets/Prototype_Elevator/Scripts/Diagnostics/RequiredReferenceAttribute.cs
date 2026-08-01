using System;

namespace Ascend.Prototype.Diagnostics
{
    /// <summary>
    /// 이 필드가 비어 있으면 그 오브젝트는 조용히 아무것도 하지 않는다 — 는 표시.
    ///
    /// `TECH_SPEC.md` §2 「null 상태에서 조용히 실패하지 않는다. 개발 빌드와 에디터에서는
    /// 원인과 경로를 명확히 출력한다」를 강제하는 쪽의 절반이다. 나머지 절반은
    /// <see cref="SceneWiringValidator"/> 가 씬 로드 직후 훑는 것이다.
    ///
    /// **선택적 참조에는 붙이지 않는다.** 이 프로젝트에는 「없으면 자동 검색한다」,
    /// 「없으면 그 연출만 건너뛴다」인 참조가 많고, 그것까지 필수로 표시하면 보고가
    /// 소음이 되어 진짜 결선 실패가 묻힌다. 자동 대체 경로가 있으면 필수가 아니다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class RequiredReferenceAttribute : Attribute
    {
        /// <summary>비어 있을 때 무엇이 죽는지. 보고에 그대로 실린다.</summary>
        public string Consequence { get; }

        public RequiredReferenceAttribute(string consequence = null)
        {
            Consequence = consequence;
        }
    }
}
