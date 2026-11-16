// EPIC-015: the walking routes through the park, in market space (they follow calibration; nothing here moves with the
// counters' height). A loop on the gravel ring (a rounded square, half side 1.72 m) runs outside the counters, the play
// table and the children's stools; the two arms lead out to a gate behind a bush at either end, where figures appear
// and disappear out of sight. At a stall a customer steps in along the counter (In -> Spot) and out the other way
// (Spot -> Out), exactly the lane the customers always used. Routes: Arrival (gate -> counter), Departure (counter ->
// gate, the long way round for a detour, or to the bench), FromPlay (a child after playing) and BenchToExit.
using System.Collections.Generic;
using Fruktstand.Core;
using UnityEngine;

namespace Fruktstand.Game
{
    // A walkable line: position and direction at a distance along it.
    public sealed class Polyline
    {
        readonly List<Vector3> points = new();
        readonly List<float> at = new(); // distance at each point

        public Polyline(IEnumerable<Vector3> path)
        {
            foreach (var p in path)
            {
                if (points.Count > 0 && Vector3.Distance(points[^1], p) < 1e-4f) continue;
                at.Add(points.Count == 0 ? 0 : at[^1] + Vector3.Distance(points[^1], p));
                points.Add(p);
            }
        }

        public IReadOnlyList<Vector3> Points => points;
        public float Length => at.Count > 0 ? at[^1] : 0;
        public Vector3 Start => points[0];
        public Vector3 End => points[^1];

        public (Vector3 position, Vector3 direction) At(float distance)
        {
            if (points.Count == 1) return (points[0], Vector3.forward);
            distance = Mathf.Clamp(distance, 0, Length);
            var i = 1;
            while (i < points.Count - 1 && at[i] < distance) i++;
            var a = points[i - 1];
            var b = points[i];
            var k = (distance - at[i - 1]) / Mathf.Max(1e-5f, at[i] - at[i - 1]);
            return (Vector3.Lerp(a, b, k), (b - a).normalized);
        }
    }

    public static class ParkRoutes
    {
        public const float DefaultWalkSpeed = 1.1f;   // m/s [A: check the pace on the headset]
        // Static like MarketLayout.StallDistance: tests that are not about walking may speed it up; they reset it in
        // [TearDown]. The game never changes it.
        public static float WalkSpeed { get; set; } = DefaultWalkSpeed;
        public const float LoopHalf = 1.72f, LoopCorner = 0.6f;
        const int LoopSamples = 160;                   // divisible by 4: the arms' joins are samples
        public const float BenchApproach = 0.6f;       // the last bit to the bench is on the grass

        // The gates: a bush hides where figures appear (ParkLayout.Gates), just beyond the arm's end.
        public static Vector3 Entrance(bool front) => new(front ? 1.0f : -1.0f, 0, front ? 11f : -11f);
        static Vector3 GateTurn(bool front) => new(0, 0, front ? 10.4f : -10.4f);
        static Vector3 ArmEnd(bool front) => new(0, 0, front ? ParkLayout.ArmEnd : -ParkLayout.ArmEnd);
        static Vector3 Join(bool front) => new(0, 0, front ? LoopHalf : -LoopHalf);

        public static bool IsEntrance(Vector3 p) => Vector3.Distance(p, Entrance(true)) < 1e-3f || Vector3.Distance(p, Entrance(false)) < 1e-3f;

        // The customer's place across the counter, and where they step in and out along it.
        public static Vector3 CounterSpot(Category c, float d) => AtStall(c, d, CustomerView.Spot);
        static Vector3 In(Category c, float d) => AtStall(c, d, CustomerView.Spot + Vector3.right * CustomerView.WalkDistance);
        static Vector3 Out(Category c, float d) => AtStall(c, d, CustomerView.Spot - Vector3.right * CustomerView.WalkDistance);

        static Vector3 AtStall(Category c, float d, Vector3 local)
        {
            var (p, yaw) = MarketLayout.Stall(c, d);
            return MarketLayout.Local(p, yaw, local);
        }

        // Where a child is when its walk away from the play table ends (PlayTableView: straight back, LeaveDistance).
        public static Vector3 PlayLeft(Ware toy, float d)
        {
            var (tp, tyaw) = MarketLayout.PlayTableAt(d);
            return MarketLayout.Local(tp, tyaw, PlayTableView.ChildSpot(toy) + Vector3.forward * PlayTableView.LeaveDistance);
        }

        // The bench (the first in ParkLayout): where to stand in front of it, and where the hip goes on the seat.
        static ParkItem Bench
        {
            get
            {
                foreach (var i in ParkLayout.Items) if (i.Piece == ParkPiece.Bench) return i;
                throw new System.InvalidOperationException("no bench");
            }
        }

        public static Vector3 BenchFront => Quaternion.Euler(0, Bench.Yaw, 0) * Vector3.forward; // towards the market
        public static Vector3 BenchStand => Bench.Position + BenchFront * (Bench.Half.y + 0.32f);
        public const float SeatHeight = 0.48f, SeatForward = 0.08f; // the model's seat (metres at scale 1)
        public static Vector3 BenchSeat => Bench.Position + BenchFront * (SeatForward * Bench.Scale) + Vector3.up * (SeatHeight * Bench.Scale);

        // ---------------------------------------------------------------- routes

        public static Polyline Arrival(Category c, float d)
        {
            var into = In(c, d);
            var best = Shortest(Join(true), into, true);
            var fromBack = Shortest(Join(false), into, false);
            var (front, arc) = Length(fromBack.arc) + Length(fromBack.gate) < Length(best.arc) + Length(best.gate) ? (false, fromBack.arc) : (true, best.arc);
            var path = new List<Vector3> { Entrance(front), GateTurn(front), ArmEnd(front) };
            path.AddRange(arc);
            path.Add(into);
            path.Add(CounterSpot(c, d));
            return new Polyline(path);
        }

        public static Polyline Departure(Category c, float d, Stroll plan)
        {
            var path = new List<Vector3> { CounterSpot(c, d), Out(c, d) };
            path.AddRange(Onwards(Out(c, d), plan));
            return new Polyline(path);
        }

        public static Polyline FromPlay(Vector3 start, Stroll plan)
        {
            var path = new List<Vector3> { start };
            if (plan == Stroll.Exit)
            {
                // straight down the back arm, to the middle before the gate's bush
                path.Add(new Vector3(start.x, 0, -ParkLayout.ArmEnd + 0.4f));
                path.Add(ArmEnd(false));
                path.Add(GateTurn(false));
                path.Add(Entrance(false));
                return new Polyline(path);
            }
            var onLoop = new Vector3(start.x, 0, -LoopHalf);
            path.Add(onLoop);
            path.AddRange(Onwards(onLoop, plan == Stroll.Detour ? Stroll.Detour : Stroll.Bench, avoidBack: true));
            return new Polyline(path);
        }

        public static Polyline BenchToExit()
        {
            var path = new List<Vector3> { BenchStand };
            path.AddRange(Onwards(Nearest(BenchStand), Stroll.Exit));
            return new Polyline(path);
        }

        // From a point on (or next to) the loop: out of the nearest gate, the other gate the long way, or to the bench.
        static List<Vector3> Onwards(Vector3 from, Stroll plan, bool avoidBack = false)
        {
            var path = new List<Vector3>();
            if (plan == Stroll.Bench)
            {
                path.AddRange(Shortest(from, Nearest(BenchStand), null).arc);
                path.Add(BenchStand);
                return path;
            }
            var toFront = Shortest(from, Join(true), null);
            var toBack = Shortest(from, Join(false), null);
            bool front;
            List<Vector3> arc;
            if (plan == Stroll.Exit) (front, arc) = Length(toFront.arc) <= Length(toBack.arc) ? (true, toFront.arc) : (false, toBack.arc);
            else
            {
                // the detour: to the gate the exit would not take, the long way round
                var exitFront = Length(toFront.arc) <= Length(toBack.arc);
                front = avoidBack || !exitFront;
                arc = LoopArc(from, Join(front), longWay: true);
            }
            path.AddRange(arc);
            path.Add(ArmEnd(front));
            path.Add(GateTurn(front));
            path.Add(Entrance(front));
            return path;
        }

        static float Length(List<Vector3> pts)
        {
            var l = 0f;
            for (var i = 1; i < pts.Count; i++) l += Vector3.Distance(pts[i - 1], pts[i]);
            return l;
        }

        // ---------------------------------------------------------------- the loop

        static List<Vector3> loop;

        static List<Vector3> Loop
        {
            get
            {
                if (loop != null) return loop;
                loop = new List<Vector3>();
                for (var i = 0; i < LoopSamples; i++)
                {
                    var a = i * Mathf.PI * 2 / LoopSamples;
                    var dir = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                    float lo = 0, hi = LoopHalf * 1.5f;
                    for (var k = 0; k < 30; k++)
                    {
                        var mid = (lo + hi) / 2;
                        if (ParkLayout.InRoundedSquare(dir * mid, LoopHalf, LoopCorner)) lo = mid; else hi = mid;
                    }
                    loop.Add(dir * lo);
                }
                return loop;
            }
        }

        static int Index(Vector3 p)
        {
            var best = 0;
            for (var i = 1; i < Loop.Count; i++)
                if ((Loop[i] - p).sqrMagnitude < (Loop[best] - p).sqrMagnitude) best = i;
            return best;
        }

        static Vector3 Nearest(Vector3 p) => Loop[Index(p)];

        // The loop between two points (each taken to its nearest loop sample), the short way or the long way round.
        static List<Vector3> LoopArc(Vector3 from, Vector3 to, bool longWay)
        {
            int a = Index(from), b = Index(to), n = Loop.Count;
            var forward = (b - a + n) % n;
            var step = (forward <= n - forward) != longWay ? 1 : -1;
            var count = step == 1 ? forward : (n - forward) % n;
            var arc = new List<Vector3>();
            for (var i = 0; i <= count; i++) arc.Add(Loop[((a + step * i) % n + n) % n]);
            return arc;
        }

        static (List<Vector3> arc, List<Vector3> gate) Shortest(Vector3 from, Vector3 to, bool? frontGate)
        {
            var arc = LoopArc(from, to, false);
            var gate = new List<Vector3>();
            if (frontGate is bool f) gate.AddRange(new[] { Entrance(f), GateTurn(f), ArmEnd(f) });
            return (arc, gate);
        }
    }
}
