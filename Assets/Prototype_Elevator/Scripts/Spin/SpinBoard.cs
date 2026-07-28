using System;
using System.Collections.Generic;

namespace Ascend.Prototype.Spin
{
    /// <summary>
    /// 3열(통관) × 3행 결과판. 통관 c의 위쪽부터 r번째 칸이 (c, r)이다.
    ///
    /// 좌표계는 한 곳에서만 정의한다 — 인접 판정(직교), 직선 판정(가로·세로·대각),
    /// 캐스케이드 충전이 전부 이 규칙을 공유해야 UI 하이라이트와 판정이 어긋나지 않는다.
    /// </summary>
    [Serializable]
    public struct SpinBoard : IEquatable<SpinBoard>
    {
        public const int Columns = 3;
        public const int Rows    = 3;
        public const int Cells   = Columns * Rows;

        // 인덱스 = column * Rows + row. 통관 단위 접근이 잦으므로 열 우선이다.
        private SymbolKind _c0r0, _c0r1, _c0r2;
        private SymbolKind _c1r0, _c1r1, _c1r2;
        private SymbolKind _c2r0, _c2r1, _c2r2;

        public static int Index(int column, int row) => column * Rows + row;
        public static int ColumnOf(int index)        => index / Rows;
        public static int RowOf(int index)           => index % Rows;

        public SymbolKind this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return _c0r0; case 1: return _c0r1; case 2: return _c0r2;
                    case 3: return _c1r0; case 4: return _c1r1; case 5: return _c1r2;
                    case 6: return _c2r0; case 7: return _c2r1; case 8: return _c2r2;
                    default: throw new IndexOutOfRangeException($"SpinBoard index {index} out of range 0..8");
                }
            }
            set
            {
                switch (index)
                {
                    case 0: _c0r0 = value; break; case 1: _c0r1 = value; break; case 2: _c0r2 = value; break;
                    case 3: _c1r0 = value; break; case 4: _c1r1 = value; break; case 5: _c1r2 = value; break;
                    case 6: _c2r0 = value; break; case 7: _c2r1 = value; break; case 8: _c2r2 = value; break;
                    default: throw new IndexOutOfRangeException($"SpinBoard index {index} out of range 0..8");
                }
            }
        }

        public SymbolKind this[int column, int row]
        {
            get => this[Index(column, row)];
            set => this[Index(column, row)] = value;
        }

        public static SpinBoard FromArray(IReadOnlyList<SymbolKind> cells)
        {
            if (cells == null || cells.Count != Cells)
                throw new ArgumentException($"SpinBoard.FromArray needs exactly {Cells} cells.", nameof(cells));

            var board = default(SpinBoard);
            for (int i = 0; i < Cells; i++) board[i] = cells[i];
            return board;
        }

        public SymbolKind[] ToArray()
        {
            var cells = new SymbolKind[Cells];
            for (int i = 0; i < Cells; i++) cells[i] = this[i];
            return cells;
        }

        /// <summary>해당 종류가 보드에 몇 개 있는지. 개수 안전망(3개 이상 기본 정화)의 입력이다.</summary>
        public int CountOf(SymbolKind kind)
        {
            int n = 0;
            for (int i = 0; i < Cells; i++) if (this[i] == kind) n++;
            return n;
        }

        public bool HasEmpty()
        {
            for (int i = 0; i < Cells; i++) if (this[i] == SymbolKind.Empty) return true;
            return false;
        }

        // ── 인접 규칙 ──
        //
        // 노션 01 "인접 판정 원칙": 연결 덩어리는 상하좌우 직교만 사용한다.
        // 대각선은 덩어리에 넣지 않고, 대각 3칸 직선 패턴으로만 따로 인정한다.
        // 승객·부품이 이 규칙을 바꿀 수 있으므로 diagonalCounts를 인자로 받는다.

        /// <summary>index 칸의 직교 이웃 인덱스를 buffer에 채우고 개수를 반환한다.</summary>
        public static int OrthogonalNeighbours(int index, int[] buffer)
        {
            int c = ColumnOf(index), r = RowOf(index), n = 0;
            if (c > 0)           buffer[n++] = Index(c - 1, r);
            if (c < Columns - 1) buffer[n++] = Index(c + 1, r);
            if (r > 0)           buffer[n++] = Index(c, r - 1);
            if (r < Rows - 1)    buffer[n++] = Index(c, r + 1);
            return n;
        }

        /// <summary>대각 이웃까지 포함한 이웃(사선 결속기 등 빌드 효과용). buffer는 8칸 이상이어야 한다.</summary>
        public static int AllNeighbours(int index, int[] buffer)
        {
            int c = ColumnOf(index), r = RowOf(index), n = 0;
            for (int dc = -1; dc <= 1; dc++)
            for (int dr = -1; dr <= 1; dr++)
            {
                if (dc == 0 && dr == 0) continue;
                int nc = c + dc, nr = r + dr;
                if (nc < 0 || nc >= Columns || nr < 0 || nr >= Rows) continue;
                buffer[n++] = Index(nc, nr);
            }
            return n;
        }

        /// <summary>
        /// 3칸 직선 8종: 가로 3, 세로 3, 대각 2. 각 원소는 보드 인덱스 3개다.
        /// 세로줄은 "같은 통관"을 뜻하므로 배관공 같은 승객이 이 라인만 강화할 수 있다.
        /// </summary>
        public static readonly int[][] Lines =
        {
            // 세로 (통관 단위)
            new[] { Index(0,0), Index(0,1), Index(0,2) },
            new[] { Index(1,0), Index(1,1), Index(1,2) },
            new[] { Index(2,0), Index(2,1), Index(2,2) },
            // 가로 (행 단위)
            new[] { Index(0,0), Index(1,0), Index(2,0) },
            new[] { Index(0,1), Index(1,1), Index(2,1) },
            new[] { Index(0,2), Index(1,2), Index(2,2) },
            // 대각
            new[] { Index(0,0), Index(1,1), Index(2,2) },
            new[] { Index(0,2), Index(1,1), Index(2,0) },
        };

        /// <summary>Lines와 같은 순서의 라인 종류 라벨. 로그·UI에서 어떤 줄이 터졌는지 설명한다.</summary>
        public static readonly LineKind[] LineKinds =
        {
            LineKind.Column, LineKind.Column, LineKind.Column,
            LineKind.Row,    LineKind.Row,    LineKind.Row,
            LineKind.Diagonal, LineKind.Diagonal,
        };

        public bool Equals(SpinBoard other)
        {
            for (int i = 0; i < Cells; i++) if (this[i] != other[i]) return false;
            return true;
        }

        public override bool Equals(object obj) => obj is SpinBoard b && Equals(b);

        public override int GetHashCode()
        {
            int h = 17;
            for (int i = 0; i < Cells; i++) h = h * 31 + (int)this[i];
            return h;
        }

        /// <summary>결정론 로그용 한 줄 표기. 예: "N A N / P P N / A N A" (행 우선 읽기).</summary>
        public override string ToString()
        {
            var sb = new System.Text.StringBuilder(20);
            for (int r = 0; r < Rows; r++)
            {
                if (r > 0) sb.Append(" / ");
                for (int c = 0; c < Columns; c++)
                {
                    if (c > 0) sb.Append(' ');
                    switch (this[c, r])
                    {
                        case SymbolKind.NormalSoul:   sb.Append('N'); break;
                        case SymbolKind.Absorber:     sb.Append('A'); break;
                        case SymbolKind.Proliferator: sb.Append('P'); break;
                        default:                      sb.Append('.'); break;
                    }
                }
            }
            return sb.ToString();
        }
    }

    public enum LineKind { Column, Row, Diagonal }
}
