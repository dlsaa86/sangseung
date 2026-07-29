using UnityEngine;
using Ascend.Prototype.Run;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.View
{
    /// <summary>
    /// 3×3 결과판을 실제 공간에 표시한다.
    ///
    /// 여기가 없으면 게임이 화면에 존재하지 않는다. 판정 엔진과 밸런스가 아무리 정확해도
    /// 플레이어가 보는 것은 빈 판때기 세 장이다.
    ///
    /// 심볼은 색이 아니라 **형태**로 구분한다. 시각 기준 B-2.5가 "회색조로 바꿨을 때
    /// 구분이 사라지면 실패"라고 요구하고, 이 씬은 지금 사실상 무채색이라 색에 기대면
    /// 아무것도 구분되지 않는다.
    ///   정상 영혼 — 구
    ///   흡수체   — 정육면체
    ///   증식체   — 캡슐(세로로 긴 알약)
    /// 실루엣이 셋 다 다르다.
    /// </summary>
    public sealed class SpinBoardView : MonoBehaviour
    {
        [SerializeField] private RunSessionBehaviour _run;

        /// <summary>9칸. SpinBoard.Index(column, row) 순서를 그대로 따른다.</summary>
        [SerializeField] private Transform[] _cells = new Transform[SpinBoard.Cells];

        private int _lastSpinCount = -1;
        private int _lastFloor = -1;

        private void Awake()
        {
            if (_run == null) _run = FindAnyObjectByType<RunSessionBehaviour>();
            ClearAll();
        }

        private void Update()
        {
            FloorSession floor = _run != null && _run.Session != null ? _run.Session.Current : null;
            if (floor == null) { ClearAll(); return; }

            int spins = floor.History != null ? floor.History.Count : 0;
            int floorNo = floor.Plan.Floor;

            // 스핀이 늘었거나 층이 바뀐 순간에만 갱신한다. 매 프레임 9칸을 다시 그리면
            // 아무것도 안 바뀌었는데 GetComponent가 계속 돈다.
            if (spins == _lastSpinCount && floorNo == _lastFloor) return;
            _lastSpinCount = spins;
            _lastFloor = floorNo;

            if (spins == 0) { ClearAll(); return; }

            SpinResolution last = floor.History[spins - 1];

            // 캐스케이드까지 끝난 뒤의 판을 보여준다. 비워진 칸은 "정화됐다"는 뜻이고,
            // 남아 있는 저항체가 곧 다음 스핀으로 넘어갈 위험이다. 초기 판을 보여주면
            // 그 두 정보가 사라진다.
            Show(last.FinalBoard);
        }

        /// <summary>
        /// 주어진 판을 그대로 표시한다. 에디터 캡처와 테스트가 Play 모드 없이
        /// 결과판을 세울 수 있어야 해서 공개한다 — Update는 에디트 모드에서 돌지 않는다.
        /// </summary>
        public void ShowBoard(SpinBoard board)
        {
            for (int i = 0; i < SpinBoard.Cells && i < _cells.Length; i++)
                SetCell(_cells[i], board[i]);
        }

        private void Show(SpinBoard board) => ShowBoard(board);

        private void ClearAll()
        {
            for (int i = 0; i < _cells.Length; i++)
                SetCell(_cells[i], SymbolKind.Empty);
        }

        private static void SetCell(Transform cell, SymbolKind kind)
        {
            if (cell == null) return;
            foreach (Transform child in cell)
            {
                SymbolKind childKind = KindOf(child.name);
                bool on = childKind != SymbolKind.Empty && childKind == kind;
                if (child.gameObject.activeSelf != on) child.gameObject.SetActive(on);
            }
        }

        /// <summary>자식 오브젝트 이름으로 어떤 심볼인지 정한다. 배치 스크립트와 규약이 같아야 한다.</summary>
        public static SymbolKind KindOf(string childName)
        {
            switch (childName)
            {
                case "Sym_NormalSoul":   return SymbolKind.NormalSoul;
                case "Sym_Absorber":     return SymbolKind.Absorber;
                case "Sym_Proliferator": return SymbolKind.Proliferator;
                default:                 return SymbolKind.Empty;
            }
        }
    }
}
