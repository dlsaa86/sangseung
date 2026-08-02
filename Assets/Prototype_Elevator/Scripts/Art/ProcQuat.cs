using UnityEngine;

namespace Ascend.Prototype.Art
{
    /// <summary>
    /// 회전 두 개를 **관리 코드로** 계산한다. `Quaternion` 타입 자체는 그대로 쓴다 —
    /// 바꾸는 것은 값을 만드는 방법뿐이다.
    ///
    /// ## 왜 `Quaternion.Euler` / `Quaternion.FromToRotation` 을 쓰지 않는가
    ///
    /// 둘 다 **네이티브 호출**이다(`[FreeFunction]`). 그래서 Unity 프로세스 밖에서는
    /// 실행되지 않는다. 이 저장소의 규칙은 「Unity 를 띄우지 않고 오프라인 Roslyn 으로
    /// 컴파일을 먼저 확인한다」인데(`docs/runtime/TOPDOWN_PROGRESS.md` §6),
    /// **컴파일이 되는 것과 형상이 맞는 것은 다른 문제**다.
    ///
    /// 메시 생성기의 결함은 컴파일러가 잡지 못한다 — 뒤집힌 면, 열린 껍질, 어긋난 법선은
    /// 전부 「컴파일도 되고 오류도 안 나는데 화면에서만 틀린」 종류다. 이 저장소가 가장
    /// 여러 번 당한 실패의 형태가 정확히 그것이고(`UP-VIS-01` 텍스처 0장, `UP-FIX-34`),
    /// 그때마다 **관측 수단이 없어서** 여러 세션 동안 발견되지 않았다.
    ///
    /// 회전만 관리 코드로 옮기면 <see cref="ProcMeshBuilder"/> · <see cref="ProcMesh"/> ·
    /// <see cref="PropLibrary"/> 의 **기하 경로 전체가 네이티브 의존이 0** 이 된다.
    /// 그러면 에디터를 띄우지 않고도 닫힘·winding·법선·경계 상자를 실제로 잴 수 있다.
    /// (<see cref="Mesh"/> 를 만드는 마지막 한 걸음만 네이티브이고, 그건 에디터 러너가 검증한다.)
    ///
    /// 부수 효과 하나: 값이 **플랫폼과 Unity 판올림에 묶이지 않는다.** 결정론 요구
    /// (`TECH_SPEC` · `GRAPHICS_TARGET` §G-7)가 표현 계층에도 그대로 적용된다.
    ///
    /// ⚠ Unity 와 **같은 규약**을 지킨다 — 오일러 회전 순서는 Z → X → Y (즉 q = qY·qX·qZ).
    /// 다른 순서를 쓰면 씬에서 Inspector 값과 코드 값이 어긋나고, 그 어긋남은
    /// 「어떤 프롭만 90도 돌아 있다」로만 나타난다.
    /// </summary>
    public static class ProcQuat
    {
        /// <summary>Unity 와 동일한 ZXY 순서의 오일러 회전.</summary>
        public static Quaternion Euler(float xDeg, float yDeg, float zDeg)
        {
            float hx = xDeg * Mathf.Deg2Rad * 0.5f;
            float hy = yDeg * Mathf.Deg2Rad * 0.5f;
            float hz = zDeg * Mathf.Deg2Rad * 0.5f;

            var qx = new Quaternion(Mathf.Sin(hx), 0f, 0f, Mathf.Cos(hx));
            var qy = new Quaternion(0f, Mathf.Sin(hy), 0f, Mathf.Cos(hy));
            var qz = new Quaternion(0f, 0f, Mathf.Sin(hz), Mathf.Cos(hz));

            // Unity 의 오일러 순서는 Z → X → Y 다. 곱은 적용의 역순으로 쓴다.
            return qy * qx * qz;
        }

        public static Quaternion Euler(Vector3 eulerDeg) => Euler(eulerDeg.x, eulerDeg.y, eulerDeg.z);

        /// <summary>
        /// <paramref name="from"/> 을 <paramref name="to"/> 로 옮기는 최단 회전.
        /// 튜브의 평행 이동 프레임이 이것 하나로 돈다.
        /// </summary>
        public static Quaternion FromTo(Vector3 from, Vector3 to)
        {
            Vector3 a = from.normalized;
            Vector3 b = to.normalized;
            if (a.sqrMagnitude < 1e-12f || b.sqrMagnitude < 1e-12f) return Quaternion.identity;

            float d = Vector3.Dot(a, b);
            if (d >= 1f - 1e-7f) return Quaternion.identity;

            if (d <= -1f + 1e-7f)
            {
                // 정반대 — 축이 하나로 정해지지 않는다. **아무 축이나 고르되 결정론적으로** 고른다.
                // 여기서 난수나 시간에 기대면 같은 인자가 다른 메시를 낸다.
                Vector3 axis = Vector3.Cross(Vector3.right, a);
                if (axis.sqrMagnitude < 1e-8f) axis = Vector3.Cross(Vector3.up, a);
                axis = axis.normalized;
                return new Quaternion(axis.x, axis.y, axis.z, 0f);
            }

            Vector3 c = Vector3.Cross(a, b);
            float s = Mathf.Sqrt((1f + d) * 2f);
            float inv = 1f / s;
            return new Quaternion(c.x * inv, c.y * inv, c.z * inv, s * 0.5f);
        }
    }
}
