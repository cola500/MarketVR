// EPIC-017 (SPEC §33): the shared building site at the play table, where the child and the player build together.
// A fixed grid of Columns x Layers cells, side by side along x (table space, y = 0 on the table top). A cell is stable
// on the table or on an occupied cell. Pure logic, unit-tested.
//
// Turns:  Child --(ChildPlaced)--> Player --(PlayerPlaced: a beat, or PlayerTurnSeconds without a block)--> Child
//         The player's time stands still while the player holds a block. The first block comes after a beat.
// Snap:   a block let go near the site goes to the nearest free stable cell: horizontal distance counts fully, going
//         up counts fully, falling down counts a quarter (a block over a column lands on top of that column).
using System.Collections.Generic;
using UnityEngine;

namespace Fruktstand.Core
{
    public sealed class BuildSite
    {
        public const int Columns = 3, Layers = 3, Cells = Columns * Layers;
        public const float SnapReach = 0.08f;  // horizontal, from the released block's centre to a cell's
        public const float SnapAbove = 0.10f;  // released at most this far above the cell's centre
        public const float PlayerTurnSeconds = 8f, ChildBeatSeconds = 2f;
        const float DownWeight = 0.25f;

        // The child's order: the middle, then wider, then the next layer up.
        static readonly int[] ChildOrder = { 1, 0, 2 };

        public enum Turn { Child, Player }

        readonly Vector3 centre;
        readonly float size;
        readonly bool[] occupied = new bool[Cells];
        float timer;

        public BuildSite(Vector3 centre, float size)
        {
            this.centre = new Vector3(centre.x, 0, centre.z);
            this.size = size;
        }

        public static int CellOf(int column, int layer) => layer * Columns + column;
        public static int ColumnOf(int cell) => cell % Columns;
        public static int LayerOf(int cell) => cell / Columns;

        public Vector3 Centre(int cell) => centre + new Vector3((ColumnOf(cell) - 1) * size, size * (LayerOf(cell) + 0.5f), 0);
        public bool Occupied(int cell) => occupied[cell];

        public int Count
        {
            get
            {
                var n = 0;
                foreach (var o in occupied) if (o) n++;
                return n;
            }
        }

        public int LayersBuilt
        {
            get
            {
                var n = 0;
                for (var c = 0; c < Cells; c++) if (occupied[c]) n = Mathf.Max(n, LayerOf(c) + 1);
                return n;
            }
        }

        public bool Stable(int cell) => LayerOf(cell) == 0 || occupied[cell - Columns];
        bool Open(int cell) => !occupied[cell] && Stable(cell);

        public int NextForChild()
        {
            for (var layer = 0; layer < Layers; layer++)
                foreach (var column in ChildOrder)
                    if (Open(CellOf(column, layer))) return CellOf(column, layer);
            return -1;
        }

        public int Snap(Vector3 released)
        {
            var best = -1;
            var bestScore = float.MaxValue;
            for (var c = 0; c < Cells; c++)
            {
                if (!Open(c)) continue;
                var p = Centre(c);
                var side = new Vector2(released.x - p.x, released.z - p.z).magnitude;
                var rise = released.y - p.y; // > 0: the cell is below the hand
                if (side > SnapReach || rise > SnapAbove) continue;
                var vertical = rise > 0 ? rise * DownWeight : -rise;
                var score = side * side + vertical * vertical;
                if (score < bestScore)
                {
                    best = c;
                    bestScore = score;
                }
            }
            return best;
        }

        public void Place(int cell) => occupied[cell] = true;

        // Takes a block out; whatever stood on it (the column above) is freed too and returned.
        public List<int> Remove(int cell)
        {
            occupied[cell] = false;
            var fell = new List<int>();
            for (var above = cell + Columns; above < Cells && occupied[above]; above += Columns)
            {
                occupied[above] = false;
                fell.Add(above);
            }
            return fell;
        }

        public Turn Whose { get; private set; } = Turn.Child;
        public bool ChildMayPlace => Whose == Turn.Child && timer >= ChildBeatSeconds;

        public void ChildPlaced() => Enter(Turn.Player, 0);
        public void PlayerPlaced() => Enter(Turn.Child, 0);

        public void Tick(float dt, bool playerHolds)
        {
            if (Whose == Turn.Player && playerHolds) return;
            timer += dt;
            if (Whose == Turn.Player && timer >= PlayerTurnSeconds) Enter(Turn.Child, ChildBeatSeconds); // the child goes on
        }

        public void Clear()
        {
            for (var c = 0; c < Cells; c++) occupied[c] = false;
            Enter(Turn.Child, 0);
        }

        void Enter(Turn t, float time)
        {
            Whose = t;
            timer = time;
        }
    }
}
