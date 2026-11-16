// EPIC-017: playing together at the play table. The ball's soft toss (Core.BallToss) and the shared building site
// with turns (Core.BuildSite). Table space: y = 0 on the table top, the player's side is -z, the child's +z.
using System.Linq;
using Fruktstand.Core;
using NUnit.Framework;
using UnityEngine;

namespace Fruktstand.Tests
{
    public class SharedPlayTests
    {
        static readonly Vector3 Catch = new(-0.30f, 0.10f, 0.20f);
        static readonly Vector3 Lifted = new(-0.25f, 0.15f, -0.15f);
        static readonly Vector3 Gravity = new(0, -9.81f, 0);

        static Vector3 At(Vector3 from, Vector3 v, float t) => from + v * t + Gravity * (0.5f * t * t);

        static Vector3 TowardsChild(float speed, float up = 0.1f)
        {
            var d = Catch - Lifted;
            d.y = 0;
            return d.normalized * speed + Vector3.up * up;
        }

        // ---------------- Ball ----------------

        [Test]
        public void Toss_LowReleaseRolls()
        {
            var toss = BallToss.Plan(new Vector3(-0.25f, 0.05f, -0.15f), TowardsChild(0.6f, 0), Catch);
            Assert.AreEqual(BallToss.Kind.None, toss.Kind, "released near the table: the ball rolls as before");
        }

        [Test]
        public void Toss_LiftAndPushReachesTheChild()
        {
            var toss = BallToss.Plan(Lifted, TowardsChild(0.6f), Catch);
            Assert.AreEqual(BallToss.Kind.ToChild, toss.Kind);
            Assert.Less(Vector3.Distance(At(Lifted, toss.Velocity, toss.Flight), Catch), 0.005f, "straight into the child's hands");
            var peak = Enumerable.Range(0, 101).Max(i => At(Lifted, toss.Velocity, toss.Flight * i / 100f).y);
            Assert.AreEqual(Lifted.y + BallToss.Apex, peak, 0.005f, "a soft arc, its top a little above the hand");
            Assert.LessOrEqual(toss.Velocity.magnitude, BallToss.MaxSpeed, "never wild");
            Assert.Greater(toss.Flight, 0.2f, "slow enough to follow with the eyes");
        }

        [Test]
        public void Toss_SmallFlickIsEnough()
        {
            var toss = BallToss.Plan(Lifted, TowardsChild(BallToss.MinHandSpeed + 0.02f, 0), Catch);
            Assert.AreEqual(BallToss.Kind.ToChild, toss.Kind, "a small push forward is a toss");
            Assert.Less(Vector3.Distance(At(Lifted, toss.Velocity, toss.Flight), Catch), 0.005f, "and it still reaches the child");
        }

        [Test]
        public void Toss_FasterHandSameLob()
        {
            var slow = BallToss.Plan(Lifted, TowardsChild(0.3f), Catch);
            var fast = BallToss.Plan(Lifted, TowardsChild(2.5f, 0.8f), Catch);
            Assert.AreEqual(BallToss.Kind.ToChild, fast.Kind);
            Assert.Less(Vector3.Distance(slow.Velocity, fast.Velocity), 1e-4f, "aimed at the child, the hand's speed adds nothing");
        }

        [Test]
        public void Toss_OffAimFollowsTheGesture()
        {
            var aim = Catch - Lifted;
            aim.y = 0;
            var off = Quaternion.Euler(0, -60, 0) * aim.normalized; // 60° to the left of the child
            float Distance(float speed, out Vector3 dir)
            {
                var toss = BallToss.Plan(Lifted, off * speed, Catch);
                Assert.AreEqual(BallToss.Kind.Wide, toss.Kind, $"{speed} m/s, 60° off: not at the child");
                var land = toss.Target - Lifted;
                land.y = 0;
                dir = land.normalized;
                Assert.Less(Vector3.Distance(At(Lifted, toss.Velocity, toss.Flight), toss.Target), 0.005f, "lands where planned");
                return land.magnitude;
            }
            var near = Distance(0.3f, out var d1);
            var far = Distance(1.0f, out _);
            var farthest = Distance(5f, out _);
            Assert.Less(Vector3.Angle(d1, off), 2f, "along the hand's movement");
            Assert.GreaterOrEqual(near, BallToss.MinDistance - 1e-4f);
            Assert.Greater(far, near, "a bigger push goes further");
            Assert.LessOrEqual(farthest, BallToss.MaxDistance + 1e-4f, "but never far");
        }

        [Test]
        public void Toss_StillHandIsNoToss()
        {
            Assert.AreEqual(BallToss.Kind.None, BallToss.Plan(Lifted, TowardsChild(0.1f, 0.05f), Catch).Kind, "lifted and let go: it just falls");
            Assert.AreEqual(BallToss.Kind.None, BallToss.Plan(Lifted, new Vector3(0, 0.1f, -0.6f), Catch).Kind, "pulled back towards the player: no toss");
        }

        // ---------------- Blocks ----------------

        const float S = 0.06f;
        static readonly Vector3 Site = new(0.30f, 0, 0.04f);

        static BuildSite NewSite() => new(Site, S);
        static int Cell(int column, int layer) => BuildSite.CellOf(column, layer);

        [Test]
        public void Build_OnlyStableCells()
        {
            var b = NewSite();
            Assert.IsTrue(b.Stable(Cell(0, 0)) && b.Stable(Cell(2, 0)), "on the table");
            Assert.IsFalse(b.Stable(Cell(1, 1)), "nothing to stand on");
            b.Place(Cell(1, 0));
            Assert.IsTrue(b.Stable(Cell(1, 1)));
            Assert.IsFalse(b.Stable(Cell(1, 2)));
            Assert.AreEqual(Site.x - S, b.Centre(Cell(0, 0)).x, 1e-5f, "side by side, a block apart");
            Assert.AreEqual(S * 1.5f, b.Centre(Cell(2, 1)).y, 1e-5f, "one block up");
        }

        [Test]
        public void Build_ChildStartsInTheMiddle()
        {
            var b = NewSite();
            Assert.AreEqual(Cell(1, 0), b.NextForChild());
            b.Place(Cell(1, 0));
            Assert.AreEqual(Cell(0, 0), b.NextForChild(), "then wider");
            b.Place(Cell(0, 0));
            b.Place(Cell(2, 0));
            Assert.AreEqual(Cell(1, 1), b.NextForChild(), "then higher");
            for (var c = 0; c < BuildSite.Cells; c++) if (!b.Occupied(c)) b.Place(c);
            Assert.AreEqual(-1, b.NextForChild(), "full");
        }

        [Test]
        public void Build_ChildWaitsForThePlayer()
        {
            var b = NewSite();
            Assert.IsFalse(b.ChildMayPlace, "a moment before the first block");
            b.Tick(BuildSite.ChildBeatSeconds, false);
            Assert.IsTrue(b.ChildMayPlace);
            b.Place(Cell(1, 0));
            b.ChildPlaced();
            Assert.AreEqual(BuildSite.Turn.Player, b.Whose, "now the player");
            b.Tick(BuildSite.PlayerTurnSeconds - 0.1f, false);
            Assert.IsFalse(b.ChildMayPlace, "the child leaves room");
            b.Place(Cell(0, 0));
            b.PlayerPlaced();
            Assert.AreEqual(BuildSite.Turn.Child, b.Whose);
            Assert.IsFalse(b.ChildMayPlace, "a short beat first");
            b.Tick(BuildSite.ChildBeatSeconds, false);
            Assert.IsTrue(b.ChildMayPlace, "then the child lays the next");
        }

        [Test]
        public void Build_PlayerTurnTimesOut()
        {
            var b = NewSite();
            b.Tick(BuildSite.ChildBeatSeconds, false);
            b.Place(Cell(1, 0));
            b.ChildPlaced();
            b.Tick(BuildSite.PlayerTurnSeconds * 2, true);
            Assert.AreEqual(BuildSite.Turn.Player, b.Whose, "never while the player holds a block");
            b.Tick(BuildSite.PlayerTurnSeconds, false);
            Assert.IsTrue(b.ChildMayPlace, "the player did not build: the child goes on");
        }

        [Test]
        public void Build_SnapNearTheHand()
        {
            var b = NewSite();
            b.Place(Cell(1, 0));
            var beside = b.Centre(Cell(0, 0)) + new Vector3(0.01f, 0.04f, -0.02f); // roughly left of the child's block
            Assert.AreEqual(Cell(0, 0), b.Snap(beside), "wider");
            var above = b.Centre(Cell(1, 1)) + new Vector3(-0.02f, 0.05f, 0.02f);
            Assert.AreEqual(Cell(1, 1), b.Snap(above), "higher");
            var overEmpty = b.Centre(Cell(2, 1)) + new Vector3(0, 0.02f, 0); // above the empty right cell
            Assert.AreEqual(Cell(2, 0), b.Snap(overEmpty), "never in the air: down onto the table");
        }

        [Test]
        public void Build_SnapOutOfAnOccupiedCell()
        {
            var b = NewSite();
            b.Place(Cell(1, 0));
            var inside = b.Centre(Cell(1, 0)) + new Vector3(0.01f, 0.02f, 0);
            Assert.AreEqual(Cell(1, 1), b.Snap(inside), "deep inside the child's block: the nearest stable free cell (on top)");
            var insideLeft = b.Centre(Cell(1, 0)) + new Vector3(-0.025f, -0.01f, 0);
            Assert.AreEqual(Cell(0, 0), b.Snap(insideLeft), "low and to the left: beside it");
        }

        [Test]
        public void Build_FarReleaseIsNotSnapped()
        {
            var b = NewSite();
            Assert.AreEqual(-1, b.Snap(Site + new Vector3(-0.2f, S / 2, 0)), "free building elsewhere on the table");
            Assert.AreEqual(-1, b.Snap(b.Centre(Cell(1, 0)) + Vector3.up * 0.25f), "high above: falls as before");
        }

        [Test]
        public void Build_RemovingAFootFreesWhatStoodOnIt()
        {
            var b = NewSite();
            b.Place(Cell(1, 0));
            b.Place(Cell(1, 1));
            b.Place(Cell(1, 2));
            b.Place(Cell(0, 0));
            var fell = b.Remove(Cell(1, 0));
            CollectionAssert.AreEquivalent(new[] { Cell(1, 1), Cell(1, 2) }, fell, "the column above falls");
            Assert.IsFalse(b.Occupied(Cell(1, 1)));
            Assert.IsTrue(b.Occupied(Cell(0, 0)), "the neighbour stands");
            Assert.AreEqual(1, b.Count);
        }
    }
}
