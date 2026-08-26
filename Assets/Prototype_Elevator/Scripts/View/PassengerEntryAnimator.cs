using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ascend.Prototype.View
{
    /// <summary>
    /// 탑승 연출. 승객을 슬롯에 즉시 세우지 않고 **문 밖에서 걸어 들어오게** 한다.
    ///
    /// ## 왜 뷰가 아니라 별도 컴포넌트인가
    ///
    /// <see cref="ElevatorGrayboxView"/> 는 매 프레임 상태를 읽어 화면을 다시 만드는
    /// 순수 반영기다. 반면 걸어 들어오는 동작은 **시간을 갖는 사건**이라 프레임 단위
    /// 재생성과 섞이면 매 프레임 리셋된다. 그래서 뷰는 "새 승객이 생겼다"만 알려주고,
    /// 이 컴포넌트가 그 오브젝트를 잠시 넘겨받아 코루틴으로 옮긴다.
    ///
    /// ## 경로
    ///
    /// 문 밖 후보 자리(P0) → 차 안쪽으로 파고드는 제어점(Q) → 배정된 슬롯(target)
    /// 을 잇는 2차 베지에다. 직선으로 그으면 문틀을 대각선으로 통과해 벽을 뚫고
    /// 들어오는 것처럼 보인다. Q 를 문 안쪽 중앙에 두면 "일단 들어와서 자리로
    /// 비켜선다"는 실제 승강기 동선이 된다.
    ///
    /// 좌표는 전부 <c>target</c> 의 부모(승객 앵커) 로컬 공간에서 계산한다. 앵커가
    /// 회전해 있어도 경로가 같이 돌아야 하기 때문이다.
    /// </summary>
    public sealed class PassengerEntryAnimator : MonoBehaviour
    {
        [Header("걸어 들어오기")]
        [SerializeField, Min(0.1f)] private float _walkDuration = 1.5f;

        /// <summary>제어점을 앵커 원점 쪽으로 얼마나 당길지. 1 이면 원점까지 들어왔다 나간다.</summary>
        [SerializeField, Range(0.3f, 1.2f)] private float _entryDepth = 0.85f;

        [Header("걸음")]
        [SerializeField, Min(0f)] private float _stepHeight = 0.055f;
        [SerializeField, Min(0.1f)] private float _stepsPerSecond = 2.6f;
        [SerializeField, Range(0f, 15f)] private float _leanDegrees = 5f;

        [Header("자리 양보")]
        /// <summary>이미 탄 승객이 새 슬롯으로 밀려날 때의 이동 시간.</summary>
        [SerializeField, Min(0.05f)] private float _slideDuration = 0.4f;

        private readonly Dictionary<GameObject, Coroutine> _running =
            new Dictionary<GameObject, Coroutine>();

        /// <summary>지금 걸어 들어오는 중인 승객이 있으면 true. 문 닫힘 억제 등에 쓸 수 있다.</summary>
        public bool IsBoarding { get; private set; }

        /// <summary>
        /// <paramref name="figure"/> 를 <paramref name="fromAnchor"/> 위치에서 현재 자리까지 걸어오게 한다.
        /// 호출 시점의 <c>localPosition</c> 을 목적지로 삼으므로, 뷰는 평소처럼 슬롯에
        /// 세워 두고 이 함수를 부르기만 하면 된다.
        /// </summary>
        public void PlayEntry(GameObject figure, Transform fromAnchor)
        {
            if (figure == null || fromAnchor == null) return;
            Transform t = figure.transform;
            Transform parent = t.parent;
            if (parent == null) return;

            Vector3 target = t.localPosition;
            Vector3 start = parent.InverseTransformPoint(fromAnchor.position);
            start.y = target.y;

            // 문 안쪽으로 파고드는 제어점. start 에서 앵커 원점 방향으로 _entryDepth 만큼.
            Vector3 control = Vector3.Lerp(start, Vector3.zero, _entryDepth);
            control.y = target.y;

            Restart(figure, WalkRoutine(figure, start, control, target));
        }

        /// <summary>
        /// 출발점과 경유점을 월드 좌표로 명시하는 판. 문과 자리가 직각으로 꾺어져 있을 때
        /// 자동 제어점으로는 문틀을 비컴 수 없어서, 문턴 지점을 직접 받는다.
        /// </summary>
        public void PlayEntry(GameObject figure, Vector3 startWorld, Vector3 waypointWorld)
        {
            if (figure == null) return;
            Transform t = figure.transform;
            Transform parent = t.parent;
            if (parent == null) return;

            Vector3 target = t.localPosition;
            Vector3 start = parent.InverseTransformPoint(startWorld);
            Vector3 control = parent.InverseTransformPoint(waypointWorld);
            start.y = target.y;
            control.y = target.y;

            Restart(figure, WalkRoutine(figure, start, control, target));
        }

        /// <summary>
        /// 문 → 모퉁이 → 자리 의 3점 경로. 문과 자리가 직각으로 꾺어져 있을 때 쓴다.
        ///
        /// 2차 베지에 하나로 잉으면 커브가 문틀 모퉁이를 질러 법으면서 벽을 통과하고,
        /// 그 다음 방 한가운데를 가로질러 플레이어 앞을 지나간다. 첫 제어점을 문 안쪽에
        /// 두면 경로가 일단 문을 띄고 들어오게 되고, 둘째 제어점을 백월 모퉁이에 두면
        /// 벽을 따라 도는 동선이 된다.
        /// </summary>
        public void PlayEntry(GameObject figure, Vector3 startWorld,
                              Vector3 doorWorld, Vector3 turnWorld)
        {
            if (figure == null) return;
            Transform t = figure.transform;
            Transform parent = t.parent;
            if (parent == null) return;

            Vector3 target = t.localPosition;
            Vector3 p0 = parent.InverseTransformPoint(startWorld);
            Vector3 c1 = parent.InverseTransformPoint(doorWorld);
            Vector3 c2 = parent.InverseTransformPoint(turnWorld);
            p0.y = target.y; c1.y = target.y; c2.y = target.y;

            Restart(figure, WalkRoutineCubic(figure, p0, c1, c2, target));
        }

        /// <summary>
        /// 이미 타고 있던 승객이 새 슬롯으로 옮겨갈 때. 팝 대신 짧게 미끄러진다.
        /// </summary>
        public void PlaySlide(GameObject figure, Vector3 fromLocal)
        {
            if (figure == null) return;
            Transform t = figure.transform;
            if ((t.localPosition - fromLocal).sqrMagnitude < 0.0001f) return;
            Restart(figure, SlideRoutine(figure, fromLocal, t.localPosition));
        }

        private void Restart(GameObject figure, IEnumerator routine)
        {
            if (_running.TryGetValue(figure, out Coroutine existing) && existing != null)
                StopCoroutine(existing);
            _running[figure] = StartCoroutine(routine);
        }

        private IEnumerator WalkRoutine(GameObject figure, Vector3 p0, Vector3 q, Vector3 p2)
        {
            IsBoarding = true;
            Transform t = figure.transform;
            Quaternion homeRot = t.localRotation;

            float elapsed = 0f;
            Vector3 previous = p0;
            while (elapsed < _walkDuration)
            {
                if (figure == null) { IsBoarding = false; yield break; }

                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / _walkDuration);

                // 출발은 느슨하게, 도착은 확실히 멈추도록 감속만 준다.
                float e = 1f - (1f - u) * (1f - u);
                Vector3 pos = Bezier(p0, q, p2, e);

                // 걸음: 좌우 발이 번갈아 닿는 두 배 주기라 절댓값 사인이 맞다.
                float bob = Mathf.Abs(Mathf.Sin(Mathf.PI * elapsed * _stepsPerSecond)) * _stepHeight;
                bob *= Mathf.Sin(Mathf.PI * u);   // 출발·도착에서 0 으로 수렴
                pos.y += bob;

                Vector3 delta = pos - previous;
                previous = pos;
                t.localPosition = pos;

                Vector3 flat = new Vector3(delta.x, 0f, delta.z);
                if (flat.sqrMagnitude > 1e-8f)
                {
                    Quaternion face = Quaternion.LookRotation(flat.normalized, Vector3.up);
                    float lean = _leanDegrees * (1f - u);
                    t.localRotation = face * Quaternion.Euler(lean, 0f, 0f);
                }

                yield return null;
            }

            if (figure != null)
            {
                t.localPosition = p2;
                t.localRotation = homeRot;
            }
            _running.Remove(figure);
            IsBoarding = false;
        }

        private IEnumerator SlideRoutine(GameObject figure, Vector3 from, Vector3 to)
        {
            Transform t = figure.transform;
            float elapsed = 0f;
            while (elapsed < _slideDuration)
            {
                if (figure == null) yield break;
                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / _slideDuration);
                float e = u * u * (3f - 2f * u);
                Vector3 pos = Vector3.Lerp(from, to, e);
                pos.y = to.y + Mathf.Abs(Mathf.Sin(Mathf.PI * elapsed * _stepsPerSecond)) * _stepHeight * 0.5f
                             * Mathf.Sin(Mathf.PI * u);
                t.localPosition = pos;
                yield return null;
            }
            if (figure != null) t.localPosition = to;
            _running.Remove(figure);
        }

        private IEnumerator WalkRoutineCubic(GameObject figure, Vector3 p0, Vector3 c1,
                                             Vector3 c2, Vector3 p3)
        {
            IsBoarding = true;
            Transform t = figure.transform;
            Quaternion homeRot = t.localRotation;

            float elapsed = 0f;
            Vector3 previous = p0;
            while (elapsed < _walkDuration)
            {
                if (figure == null) { IsBoarding = false; yield break; }

                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / _walkDuration);
                float e = 1f - (1f - u) * (1f - u);
                Vector3 pos = BezierCubic(p0, c1, c2, p3, e);

                float bob = Mathf.Abs(Mathf.Sin(Mathf.PI * elapsed * _stepsPerSecond)) * _stepHeight;
                bob *= Mathf.Sin(Mathf.PI * u);
                pos.y += bob;

                Vector3 delta = pos - previous;
                previous = pos;
                t.localPosition = pos;

                Vector3 flat = new Vector3(delta.x, 0f, delta.z);
                if (flat.sqrMagnitude > 1e-8f)
                {
                    Quaternion face = Quaternion.LookRotation(flat.normalized, Vector3.up);
                    float lean = _leanDegrees * (1f - u);
                    t.localRotation = face * Quaternion.Euler(lean, 0f, 0f);
                }
                yield return null;
            }

            if (figure != null)
            {
                t.localPosition = p3;
                t.localRotation = homeRot;   // 도착하면 자리가 정한 방향(기계 쪽)으로 선다
            }
            _running.Remove(figure);
            IsBoarding = false;
        }

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float m = 1f - t;
            return m * m * a + 2f * m * t * b + t * t * c;
        }

        private static Vector3 BezierCubic(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float m = 1f - t;
            return m * m * m * a + 3f * m * m * t * b + 3f * m * t * t * c + t * t * t * d;
        }
    }
}
