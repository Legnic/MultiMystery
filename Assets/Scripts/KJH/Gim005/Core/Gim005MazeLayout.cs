using System.Collections.Generic;

namespace Gim005
{
    // 덤불 정원 미로의 "논리 구조" (어느 칸끼리 이어져 있고, 어디에 문양 덤불이 있는가).
    // 에디터 빌더(Gim005Builder)가 이 데이터대로 덤불 벽을 세우고, EditMode 테스트가 "가짜 길 4개는 모두 막다른 길인가"를 검사한다.
    //
    // 격자: 정원(16×16m)을 7×7칸으로 나눈다. (c = 서→동 0~6, r = 북→남 0~6). 북쪽 r=0 위가 정원 입구(1층 남쪽 복도 양문).
    // 가운데 3×3칸(c2~4, r2~4)이 중앙 광장(분수대 + 중앙 버튼).
    //
    //   r\c   0      1      2    3    4      5      6
    //   0    F1 ─G2─ J2 ─G1─ T ── S ── R ── J4 ─G4─ F2      S = 입구(J1)
    //         │      │G3                     │G8     │
    //   1    F1     J3 ─G6─ T ── T    F3 ── F3     F2
    //         │      │G5         │G7          │      │
    //   2    F1   F4   [   광   장   ]  F3   F2
    //   3    F1   F1   [   (분수대)  ]  F3   F2
    //   4    F1   F1   [      버튼   ]~ F2   F2      ~ = 덤불 틈(엿보기 구멍)
    //   5    F1   F1   F2 ─ F2 ─ F2 ─ F2   F2
    //   6    F1 ─ F1   F2 ─ F2 ─ F2 ─ F2 ─ F2
    //
    // 진짜 길: S → 왼쪽 → G1 → J2 → G3 → J3 → G6 → 회랑 → G7 → 광장
    // F1(J2에서 G2), F4(J3에서 G5, 진짜 길 바로 옆), F2(J4에서 G4, 가장 긴 우회로 끝에 엿보기 구멍), F3(J4에서 G8)
    public static class Gim005MazeLayout
    {
        public const int Cols = 7, Rows = 7;
        public const int PlazaMin = 2, PlazaMax = 4;   // 광장 칸 범위 (c, r 모두)
        public const int EntranceCol = 3;               // 입구 칸 (r = 0), 북쪽 벽이 뚫려 있다

        public struct Cell
        {
            public int c, r;
            public Cell(int c, int r) { this.c = c; this.r = r; }
            public override string ToString() { return "(" + c + "," + r + ")"; }
        }

        // 두 칸 사이 경계. gate = -1이면 그냥 통로, 0~7이면 문양 덤불 G1~G8
        public struct Edge
        {
            public Cell a, b;
            public int gate;
            public Edge(int ac, int ar, int bc, int br, int gate = -1) { a = new Cell(ac, ar); b = new Cell(bc, br); this.gate = gate; }
        }

        public static readonly Cell Entrance = new Cell(EntranceCol, 0);
        public static readonly Edge PeekWall = new Edge(5, 4, 4, 4);   // F2 끝: 이 벽에 엿보기 구멍 (지나갈 수는 없음)

        // 열린 경계 전부 (광장 내부는 따로 처리). 여기에 없는 이웃 칸 사이에는 덤불 벽이 선다.
        public static readonly Edge[] Edges =
        {
            // 입구 → 갈림길 J1
            new Edge(3, 0, 2, 0), new Edge(3, 0, 4, 0),
            // 진짜 길
            new Edge(2, 0, 1, 0, 0),                    // G1
            new Edge(1, 0, 1, 1, 2),                    // G3 (J2 → J3)
            new Edge(1, 1, 2, 1, 5),                    // G6 (J3 → 회랑)
            new Edge(2, 1, 3, 1),                       // 회랑
            new Edge(3, 1, 3, 2, 6),                    // G7 (회랑 → 광장)
            // F1: J2에서 G2
            new Edge(1, 0, 0, 0, 1),
            new Edge(0, 0, 0, 1), new Edge(0, 1, 0, 2), new Edge(0, 2, 0, 3), new Edge(0, 3, 0, 4), new Edge(0, 4, 0, 5), new Edge(0, 5, 0, 6),
            new Edge(0, 6, 1, 6), new Edge(1, 6, 1, 5), new Edge(1, 5, 1, 4), new Edge(1, 4, 1, 3),
            // F4: J3에서 G5 (한 칸짜리 함정, 벤치가 있는 칸)
            new Edge(1, 1, 1, 2, 4),
            // 오른쪽 → J4
            new Edge(4, 0, 5, 0),
            // F3: J4에서 G8
            new Edge(5, 0, 5, 1, 7),
            new Edge(5, 1, 4, 1), new Edge(5, 1, 5, 2), new Edge(5, 2, 5, 3),
            // F2: J4에서 G4, 동쪽 → 남쪽을 크게 돌아 광장 동쪽 엿보기 구멍에서 끝남
            new Edge(5, 0, 6, 0, 3),
            new Edge(6, 0, 6, 1), new Edge(6, 1, 6, 2), new Edge(6, 2, 6, 3), new Edge(6, 3, 6, 4), new Edge(6, 4, 6, 5), new Edge(6, 5, 6, 6),
            new Edge(6, 6, 5, 6), new Edge(5, 6, 4, 6), new Edge(4, 6, 3, 6), new Edge(3, 6, 2, 6),
            new Edge(2, 6, 2, 5), new Edge(2, 5, 3, 5), new Edge(3, 5, 4, 5), new Edge(4, 5, 5, 5), new Edge(5, 5, 5, 4),
        };

        // 기획서의 경로 표 (테스트·문서용)
        public static readonly int[] TruePathGates = { 0, 2, 5, 6 };   // G1, G3, G6, G7

        public static bool IsPlaza(Cell x) { return x.c >= PlazaMin && x.c <= PlazaMax && x.r >= PlazaMin && x.r <= PlazaMax; }
        public static bool InGrid(Cell x) { return x.c >= 0 && x.c < Cols && x.r >= 0 && x.r < Rows; }

        // 두 이웃 칸 사이 경계 정보. 열린 경계면 true와 gate 번호(-1 = 그냥 통로)
        public static bool TryGetEdge(Cell a, Cell b, out int gate)
        {
            gate = -1;
            if (IsPlaza(a) && IsPlaza(b)) return true;
            foreach (var e in Edges)
            {
                if ((Same(e.a, a) && Same(e.b, b)) || (Same(e.a, b) && Same(e.b, a))) { gate = e.gate; return true; }
            }
            return false;
        }

        // 열린 덤불(openGates[g] = true)만 지나갈 수 있다고 할 때 입구에서 갈 수 있는 칸들
        public static HashSet<int> Reachable(bool[] openGates)
        {
            var seen = new HashSet<int>();
            var queue = new Queue<Cell>();
            queue.Enqueue(Entrance);
            seen.Add(Key(Entrance));
            int[] dc = { 1, -1, 0, 0 }, dr = { 0, 0, 1, -1 };
            while (queue.Count > 0)
            {
                var x = queue.Dequeue();
                for (int k = 0; k < 4; k++)
                {
                    var y = new Cell(x.c + dc[k], x.r + dr[k]);
                    if (!InGrid(y) || seen.Contains(Key(y))) continue;
                    if (!TryGetEdge(x, y, out int gate)) continue;
                    if (gate >= 0 && !openGates[gate]) continue;
                    seen.Add(Key(y));
                    queue.Enqueue(y);
                }
            }
            return seen;
        }

        public static int Key(Cell x) { return x.r * Cols + x.c; }
        static bool Same(Cell a, Cell b) { return a.c == b.c && a.r == b.r; }
    }
}
