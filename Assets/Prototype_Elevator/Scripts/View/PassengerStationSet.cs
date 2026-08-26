using System.Text;
using UnityEngine;

namespace Ascend.Prototype.View
{
    /// <summary>
    /// 승객이 서는 자리. 「기계 맞은편에 서서 대기」를 좌표로 고정한 것이다.
    ///
    /// ## 왜 슬롯 트랜스폼인가 — 겹침을 계산으로 막지 않는다
    ///
    /// 예전 뷰는 앵커 로컬 X 로 `(i - (n-1)/2) * spacing` 을 계산해 승객을 늘어놓았다.
    /// 그 방식은 인원이 바뀔 때마다 모두의 좌표가 바뀌고, 간격이 좁아지면 조용히
    /// 서로를 파고든다. 여기서는 **자리를 미리 만들어 두고 한 명씩 배정**한다.
    /// 한 슬롯에 한 명이므로 겹침은 계산 실수로 생기는 게 아니라 **애초에 불가능**하다.
    ///
    /// ## 채우는 순서가 바깥부터인 이유
    ///
    /// 플레이어는 기계를 마주 보고 서 있고, 승객은 그 뒤 벽에 붙는다. 가운데부터
    /// 채우면 첫 승객이 플레이어 바로 뒤통수에 선다. 그래서 슬롯 배열은 화면상
    /// 왼쪽·오른쪽 끝부터 채우도록 저작해 둔다 — 배열 순서가 곧 채우는 순서다.
    ///
    /// ## 검증
    ///
    /// <see cref="Validate"/> 가 슬롯 간 거리를 실제로 재서 <see cref="_personalSpace"/>
    /// 보다 가까운 쌍을 보고한다. 자리를 손으로 옮긴 뒤에도 겹침이 조용히 들어오지
    /// 않게 하는 장치다.
    /// </summary>
    public sealed class PassengerStationSet : MonoBehaviour
    {
        [Header("서는 자리 — 배열 순서가 채우는 순서")]
        [SerializeField] private Transform[] _slots = new Transform[0];

        [Header("동선")]
        /// <summary>문 밖에서 기다리는 지점. 걸어 들어오기의 출발점.</summary>
        [SerializeField] private Transform _entryOutside;

        /// <summary>문을 막 통과한 지점. 경로를 여기로 꺾어야 문틀을 안 뚫는다.</summary>
        [SerializeField] private Transform _entryInside;

        /// <summary>
        /// 백월 모퉁이. 문을 들어온 뒤 여기를 거쳐 자리로 가면 방 한가운데를 가로질러
        /// 플레이어 앞을 지나가지 않는다. 비워 두면 2점 경로로 돌아간다.
        /// </summary>
        [SerializeField] private Transform _entryTurn;

        [Header("검증")]
        [SerializeField, Min(0.1f)] private float _personalSpace = 0.9f;

        public int Count => _slots != null ? _slots.Length : 0;
        public Transform EntryOutside => _entryOutside;
        public Transform EntryInside => _entryInside;
        public Transform EntryTurn => _entryTurn;

        public Transform Slot(int index)
        {
            if (_slots == null || index < 0 || index >= _slots.Length) return null;
            return _slots[index];
        }

        /// <summary>슬롯 간 최소 거리를 실제로 잰다. 겹치는 쌍이 없으면 true.</summary>
        public bool Validate(out string report)
        {
            var sb = new StringBuilder();
            bool ok = true;
            int n = Count;
            sb.AppendLine($"slots={n}, personalSpace={_personalSpace:F2}m");

            for (int i = 0; i < n; i++)
            {
                Transform a = Slot(i);
                if (a == null) { sb.AppendLine($"  slot {i} = NULL"); ok = false; continue; }
                sb.AppendLine($"  slot {i} {a.name} @ ({a.position.x:F2}, {a.position.y:F2}, {a.position.z:F2}) " +
                              $"facing {a.forward.x:F2},{a.forward.z:F2}");
            }

            float worst = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    Transform a = Slot(i), b = Slot(j);
                    if (a == null || b == null) continue;
                    Vector3 pa = a.position, pb = b.position;
                    pa.y = 0f; pb.y = 0f;
                    float d = Vector3.Distance(pa, pb);
                    if (d < worst) worst = d;
                    if (d < _personalSpace)
                    {
                        sb.AppendLine($"  OVERLAP {i}<->{j}: {d:F2}m < {_personalSpace:F2}m");
                        ok = false;
                    }
                }
            }
            if (n >= 2) sb.AppendLine($"  closest pair = {worst:F2}m");
            if (_entryOutside == null) { sb.AppendLine("  entryOutside = NULL"); ok = false; }
            if (_entryInside == null) { sb.AppendLine("  entryInside = NULL"); ok = false; }
            report = sb.ToString();
            return ok;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 0.5f, 0.9f);
            for (int i = 0; i < Count; i++)
            {
                Transform s = Slot(i);
                if (s == null) continue;
                Gizmos.DrawWireSphere(s.position + Vector3.up * 0.9f, _personalSpace * 0.5f);
                Gizmos.DrawLine(s.position, s.position + s.forward * 0.6f);
            }
            Gizmos.color = new Color(0.95f, 0.75f, 0.25f, 0.9f);
            if (_entryOutside != null) Gizmos.DrawWireCube(_entryOutside.position + Vector3.up * 0.9f, Vector3.one * 0.4f);
            if (_entryInside != null) Gizmos.DrawWireCube(_entryInside.position + Vector3.up * 0.9f, Vector3.one * 0.3f);
            if (_entryOutside != null && _entryInside != null)
                Gizmos.DrawLine(_entryOutside.position, _entryInside.position);
        }
    }
}
