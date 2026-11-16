// EPIC-011: where a released building block ends up, so a four-year-old's rough release never ends inside another
// block or the table (which made the physics engine shoot it away). Pure function over the table-space centres of
// the other blocks (y = 0 on the table top); unit-tested. Volumes are compared as axis-aligned cubes of the block size,
// so an edge overlap counts, not only the centre.
//
//   overlapping a block, or a little above one  -> straight on top of that stack, if that spot is free     (OnTop)
//   partly inside the table top / over the rim   -> lifted onto the table (and in from the rim), if free     (Lifted)
//   otherwise overlapping, or the spot is taken  -> the nearest free spot on the table within 0.15 m          (Nearby)
//   nothing free                                 -> where it lay before the player took it                    (LastValid)
//   clear of everything (free building), or clearly off the table (it may fall; the lost rule brings it back) (AsReleased)
//
// Other blocks are never moved. This replaces StackHelp (EPIC-003), whose snap gave up when a block was released
// deep inside another one.
using System.Collections.Generic;
using UnityEngine;

namespace Fruktstand.Core
{
    public static class BlockPlacement
    {
        public enum Kind { AsReleased, OnTop, Lifted, Nearby, LastValid }

        public const float Rim = 0.02f;          // the table's rim, inside its half extents
        public const float MaxSideways = 0.04f;  // still "above" a block: centre this far from its centre (as StackHelp)
        public const float MaxAbove = 0.08f;     // ... and its bottom at most this far above the block's top
        public const float NearbyReach = 0.15f;  // how far a blocked block may be moved (never across the table)
        const float EdgeSlack = 0.03f;           // released this far outside the table: still meant for the table
        const float Overlap = 0.98f;             // cubes closer than this (in block sizes, on every axis) overlap

        public readonly struct Result
        {
            public Result(Kind kind, Vector3 position, int below)
            {
                Kind = kind;
                Position = position;
                Below = below;
            }

            public Kind Kind { get; }
            public Vector3 Position { get; }
            public int Below { get; } // OnTop: the index (in `others`) of the block it now stands on, else -1
        }

        public static Result Resolve(Vector3 released, IReadOnlyList<Vector3> others, float size, Vector2 tableHalf, Vector3 lastValid, int maxStack = 10)
        {
            var aboveTable = Mathf.Abs(released.x) <= tableHalf.x + EdgeSlack && Mathf.Abs(released.y) < 1f && Mathf.Abs(released.z) <= tableHalf.y + EdgeSlack;
            if (!aboveTable) return new Result(Kind.AsReleased, released, -1);

            // 1. On top of the stack it overlaps or hovers just above.
            var column = ColumnUnder(released, others, size);
            if (column >= 0)
            {
                var topOf = TopOfColumn(column, others, size, out var height);
                var spot = others[topOf] + Vector3.up * size;
                if (height < maxStack && Free(spot, others, size)) return new Result(Kind.OnTop, spot, topOf);
            }

            // 2. In from the rim and up out of the table top.
            var inner = Inner(released, size, tableHalf);
            var lifted = new Vector3(inner.x, Mathf.Max(inner.y, size / 2), inner.z);
            var moved = lifted != released;
            if (Free(lifted, others, size))
            {
                if (!moved) return new Result(Kind.AsReleased, released, -1); // free building: let it fall from here
                return new Result(Kind.Lifted, lifted, -1);
            }

            // 3. The nearest free spot on the table nearby.
            var best = NearestFree(lifted, others, size, tableHalf);
            if (best is Vector3 near) return new Result(Kind.Nearby, near, -1);

            // 4. Back where it was.
            return new Result(Kind.LastValid, lastValid, -1);
        }

        // A free spot on the table for a block, as near `wanted` as possible (also for putting lost blocks home).
        public static Vector3? NearestFree(Vector3 wanted, IReadOnlyList<Vector3> others, float size, Vector2 tableHalf)
        {
            var centre = Inner(new Vector3(wanted.x, size / 2, wanted.z), size, tableHalf);
            if (Free(centre, others, size)) return centre;
            Vector3? best = null;
            var bestDistance = float.MaxValue;
            for (var r = size * 0.5f; r <= NearbyReach + 1e-4f; r += size * 0.25f)
                for (var k = 0; k < 16; k++)
                {
                    var a = k * Mathf.PI * 2 / 16;
                    var p = Inner(centre + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r), size, tableHalf);
                    var d = new Vector2(p.x - wanted.x, p.z - wanted.z).magnitude;
                    if (d > NearbyReach + 1e-4f || d >= bestDistance || !Free(p, others, size)) continue;
                    best = p;
                    bestDistance = d;
                }
            return best;
        }

        public static bool Overlaps(Vector3 a, Vector3 b, float size) =>
            Mathf.Abs(a.x - b.x) < size * Overlap && Mathf.Abs(a.y - b.y) < size * Overlap && Mathf.Abs(a.z - b.z) < size * Overlap;

        static bool Free(Vector3 p, IReadOnlyList<Vector3> others, float size)
        {
            foreach (var o in others) if (Overlaps(p, o, size)) return false;
            return true;
        }

        // Inside the rim, so the whole block is on the table.
        static Vector3 Inner(Vector3 p, float size, Vector2 tableHalf)
        {
            var mx = tableHalf.x - Rim - size / 2;
            var mz = tableHalf.y - Rim - size / 2;
            return new Vector3(Mathf.Clamp(p.x, -mx, mx), p.y, Mathf.Clamp(p.z, -mz, mz));
        }

        // The block the released one overlaps (or hovers just above), nearest sideways; -1 if none.
        static int ColumnUnder(Vector3 released, IReadOnlyList<Vector3> others, float size)
        {
            var best = -1;
            var bestSide = float.MaxValue;
            for (var i = 0; i < others.Count; i++)
            {
                var o = others[i];
                var side = new Vector2(released.x - o.x, released.z - o.z).magnitude;
                var gap = (released.y - size / 2) - (o.y + size / 2);
                var overlapping = Overlaps(released, o, size);
                var hovering = side <= MaxSideways && gap >= -size * 0.3f && gap <= MaxAbove;
                if (!overlapping && !hovering) continue;
                if (side < bestSide)
                {
                    best = i;
                    bestSide = side;
                }
            }
            return best;
        }

        // The highest block standing on `start` (straight above it, within half a block), and the stack's height.
        static int TopOfColumn(int start, IReadOnlyList<Vector3> others, float size, out int height)
        {
            var topIndex = start;
            height = 1;
            for (var climbing = true; climbing;)
            {
                climbing = false;
                var t = others[topIndex];
                for (var i = 0; i < others.Count; i++)
                {
                    var o = others[i];
                    if (new Vector2(o.x - t.x, o.z - t.z).magnitude < size * 0.5f && Mathf.Abs(o.y - (t.y + size)) < size * 0.5f)
                    {
                        topIndex = i;
                        height++;
                        climbing = true;
                        break;
                    }
                }
            }
            // the stack also counts what stands below the start block
            var b = others[start];
            for (var below = true; below;)
            {
                below = false;
                foreach (var o in others)
                    if (new Vector2(o.x - b.x, o.z - b.z).magnitude < size * 0.5f && Mathf.Abs(o.y - (b.y - size)) < size * 0.5f)
                    {
                        b = o;
                        height++;
                        below = true;
                        break;
                    }
            }
            return topIndex;
        }
    }
}
