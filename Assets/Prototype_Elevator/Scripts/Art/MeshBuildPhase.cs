using System;

namespace Ascend.Prototype.Art
{
    /// <summary>
    /// 절차적 메시를 **언제** 만들어도 되는지를 코드로 못 박는 문지기.
    ///
    /// 왜 이런 것이 필요한가: `UP-TECH-04` 가 **GC Alloc 0 B/frame** 으로 VERIFIED 를 받았고
    /// (`GRAPHICS_TARGET.md` §G-8), 그 0 B 는 이번 작업이 가장 깨뜨리기 쉬운 것이다.
    /// `new Mesh()` · `List&lt;Vector3&gt;` · `mesh.SetVertices` 는 전부 관리 힙 할당이다.
    /// 프레임 루프 안에서 한 번만 불려도 0 B 가 즉시 무너진다.
    ///
    /// 그래서 이 라이브러리는 **메시를 만드는 API 를 프레임 루프에 노출하지 않는다.**
    /// 대신 조립이 끝난 시점에 <see cref="Close"/> 를 부르면, 그 뒤의 메시 생성은
    /// 조용히 통과하지 않고 <see cref="InvalidOperationException"/> 으로 **터진다.**
    ///
    /// 「조용히 통과하지 않는다」가 이 클래스의 존재 이유다. 이 저장소는 배선이 끊긴 채
    /// 아무 오류도 안 나는 실패를 여러 번 겪었고(`UP-VIS-01` 텍스처 0장, `UP-FIX-34`),
    /// 그때마다 **관측 수단이 없어서** 여러 세션 동안 발견되지 않았다.
    /// 성능 회귀도 같은 형태로 숨는다 — 프로파일러를 켜기 전까지는 아무도 모른다.
    ///
    /// 기본값은 **열림**이다. 아무도 <see cref="Close"/> 를 부르지 않으면 이 클래스는
    /// 아무것도 막지 않는다. 씬 소유자가 조립 마지막 줄에서 닫아 주어야 실제로 지킨다.
    /// </summary>
    public static class MeshBuildPhase
    {
        /// <summary>지금 메시를 만들어도 되는가. 기본 true.</summary>
        public static bool IsOpen { get; private set; } = true;

        /// <summary>지금까지 만들어진 메시 수. 성능 프로브가 「조립 후 0 증가」를 단정할 수 있다.</summary>
        public static int MeshesCreated { get; private set; }

        /// <summary>닫힌 뒤에 생성이 시도된 횟수. 예외를 잡아먹는 경로가 생겨도 여기 남는다.</summary>
        public static int ViolationCount { get; private set; }

        /// <summary>마지막 위반의 설명. 스택 없이도 어디였는지 알 수 있게.</summary>
        public static string LastViolation { get; private set; }

        /// <summary>씬 조립이 끝났다. 이후의 메시 생성은 전부 예외다.</summary>
        public static void Close() => IsOpen = false;

        /// <summary>다시 만들어야 할 일이 생겼다(에디터 도구·레벨 재구성). 명시적으로만 연다.</summary>
        public static void Open() => IsOpen = true;

        /// <summary>테스트가 상태를 남기지 않도록 되돌린다.</summary>
        public static void ResetForTests()
        {
            IsOpen = true;
            MeshesCreated = 0;
            ViolationCount = 0;
            LastViolation = null;
        }

        /// <summary>
        /// 메시를 실제로 만드는 지점에서 부른다. 닫혀 있으면 던진다.
        /// </summary>
        internal static void RequireOpen(string what)
        {
            if (IsOpen)
            {
                MeshesCreated++;
                return;
            }

            ViolationCount++;
            LastViolation = what;
            throw new InvalidOperationException(
                $"[상승] 메시 생성 단계가 닫힌 뒤에 '{what}' 을 만들려 했다. " +
                "절차적 메시는 초기화 시점에만 만든다 — UP-TECH-04 의 GC Alloc 0 B/frame 이 " +
                "이 규칙 위에 서 있다. 정말 다시 만들어야 한다면 MeshBuildPhase.Open() 을 " +
                "명시적으로 부르고, 그 프레임이 할당 측정 구간 밖인지 확인한다.");
        }
    }
}
