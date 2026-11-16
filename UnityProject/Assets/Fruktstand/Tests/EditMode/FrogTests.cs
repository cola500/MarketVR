// EPIC-018: the hopping frogs' rules (Core.FrogPond): one hop per press and release, the hop's fixed arc in the frog's
// facing, the generous lily pad, misses, and the turns with the child. Table space: y = 0 on the table top, the
// player's side is -z, the child's +z; yaw 0 faces +z.
using System.Linq;
using Fruktstand.Core;
using NUnit.Framework;
using UnityEngine;

namespace Fruktstand.Tests
{
    public class FrogTests
    {
        static readonly Vector2 Table = new(0.5f, 0.25f);
        const FrogOwner P = FrogOwner.Player, C = FrogOwner.Child;

        static FrogPond NewPond() => new(Table);

        // Steps until the frog has landed (or a second), with nobody busy.
        static void Land(FrogPond f)
        {
            for (var t = 0f; t < 1f; t += 0.02f) f.Tick(0.02f, false);
        }

        [Test]
        public void Frogs_AToyForTheLeftZone()
        {
            Assert.AreEqual(Category.Toys, WareInfo.CategoryOf(Ware.Frogs));
            CollectionAssert.Contains(WareInfo.Catalogue(Category.Toys), Ware.Frogs);
            CollectionAssert.Contains(WareInfo.All, Ware.Frogs);
            Assert.AreEqual(3, WareInfo.Price(Ware.Frogs));
            Assert.AreEqual("hoppgrodor", WareInfo.Name(Ware.Frogs));
            Assert.AreEqual(PlayArea.SpotOf(Ware.Ball), PlayArea.SpotOf(Ware.Frogs), "the left zone, with the ball and the top");
            Assert.AreEqual(0, PlayArea.BodiesOf(Ware.Frogs), "no physics bodies: hops are arcs");
            Assert.AreEqual(8, PlayArea.MaxBodies, "the physics budget is unchanged");
        }

        [Test]
        public void Frog_OneHopPerPressAndRelease()
        {
            var f = NewPond();
            Assert.IsTrue(f.Press(P), "pressed down");
            Assert.AreEqual(FrogPhase.Pressed, f.Phase(P));
            f.Tick(0.5f, true);
            Assert.AreEqual(0, f.Hops(P), "nothing while held down");
            Assert.IsTrue(f.Release(P), "the hand lets go: it hops");
            Assert.AreEqual(1, f.Hops(P));
            Assert.IsFalse(f.Release(P), "a second release is nothing");
            Land(f);
            Assert.AreEqual(1, f.Hops(P), "exactly one hop");
            Assert.AreEqual(FrogPhase.Sitting, f.Phase(P), "and it sits again");
        }

        [Test]
        public void Frog_ReleaseWithoutPressDoesNothing()
        {
            var f = NewPond();
            Assert.IsFalse(f.Release(P));
            Assert.AreEqual(0, f.Hops(P));
            Assert.AreEqual(FrogPhase.Sitting, f.Phase(P));
        }

        [Test]
        public void Frog_NoPressInTheAir()
        {
            var f = NewPond();
            f.Press(P);
            f.Release(P);
            f.Tick(FrogPond.HopSeconds / 2, false);
            Assert.AreEqual(FrogPhase.Flying, f.Phase(P));
            Assert.Greater(f.Position(P).y, FrogPond.HopHeight * 0.9f, "high in its arc halfway");
            Assert.IsFalse(f.Press(P), "a frog in the air can't be pressed");
        }

        [Test]
        public void Frog_CancelledPressNeverHops()
        {
            var f = NewPond();
            f.Press(P);
            f.Cancel(P); // pause, menu, focus lost
            Assert.AreEqual(FrogPhase.Sitting, f.Phase(P));
            Assert.IsFalse(f.Release(P), "letting go afterwards is no hop");
            Land(f);
            Assert.AreEqual(0, f.Hops(P));
            Assert.AreEqual(FrogPond.Home(P), f.Position(P), "still at home");
        }

        [Test]
        public void Frog_HomeHopLandsOnThePad()
        {
            foreach (var o in new[] { P, C })
            {
                var f = NewPond();
                f.Press(o);
                f.Release(o);
                Land(f);
                Assert.IsTrue(f.OnPad(o), $"{o}: a first press without aiming lands on the pad");
                Assert.AreEqual(1, f.PadLandings(o));
                Assert.Less(Vector3.Distance(f.Position(o), FrogPond.PadCentre), 0.01f);
            }
        }

        [Test]
        public void Frog_HopFollowsTheFacing()
        {
            var f = NewPond();
            var from = new Vector3(0.1f, 0, -0.1f);
            f.Place(P, from, 90); // facing +x
            f.Press(P);
            f.Release(P);
            Land(f);
            Assert.Less(Vector3.Distance(f.Position(P), from + Vector3.right * FrogPond.HopLength), 1e-4f, "a fixed, soft hop the way it faces");
            Assert.IsFalse(f.OnPad(P));
            Assert.AreEqual(90, f.Yaw(P), 1e-3f);
        }

        [Test]
        public void Frog_GenerousPad()
        {
            var f = NewPond();
            // Aimed 25° off: lands about 7 cm from the pad's centre, inside the generous target.
            f.Place(P, FrogPond.Home(P), 25);
            f.Press(P);
            f.Release(P);
            Land(f);
            Assert.IsTrue(f.OnPad(P), "near enough counts");
            Assert.LessOrEqual(Vector3.Distance(f.Position(P), FrogPond.PadCentre), FrogPond.PadRadius, "and it sits on the pad");
            // Aimed 60° off: beside the pad.
            f.Place(P, FrogPond.Home(P), 60);
            f.Press(P);
            f.Release(P);
            Land(f);
            Assert.IsFalse(f.OnPad(P), "a miss is part of the game");
            Assert.AreEqual(1, f.PadLandings(P));
        }

        [Test]
        public void Frog_OffTheTableIsAMiss()
        {
            var f = NewPond();
            f.Place(P, new Vector3(-0.42f, 0, 0f), -90); // near the table's end, facing out
            f.Press(P);
            f.Release(P);
            Land(f);
            Assert.AreEqual(FrogPhase.Fallen, f.Phase(P), "off the table: it falls");
            Assert.IsFalse(f.Press(P), "a fallen frog can't hop");
            f.Reset(P); // the lost-toy rule brings it back
            Assert.AreEqual(FrogPhase.Sitting, f.Phase(P));
            Assert.AreEqual(FrogPond.Home(P), f.Position(P));
            Assert.AreEqual(FrogPond.HomeYaw(P), f.Yaw(P));
            Assert.IsTrue(f.Press(P), "and can hop again");
        }

        [Test]
        public void Frog_TwoFrogsShareThePad()
        {
            var f = NewPond();
            f.Press(P);
            f.Release(P);
            Land(f);
            Assert.IsTrue(f.OnPad(P));
            f.Press(C);
            f.Release(C);
            Land(f);
            Assert.IsTrue(f.OnPad(C), "the child's frog lands on the pad too");
            Assert.GreaterOrEqual(Vector3.Distance(f.Position(C), f.Position(P)), FrogPond.FrogsApart - 1e-4f, "beside the other frog, never in it");
            Assert.LessOrEqual(Vector3.Distance(f.Position(C), FrogPond.PadCentre), FrogPond.PadRadius + 1e-4f, "still on the pad");
        }

        [Test]
        public void Frog_DroppedOffTheTableFalls()
        {
            var f = NewPond();
            f.Fall(P, new Vector3(-0.6f, 0, 0));
            Assert.AreEqual(FrogPhase.Fallen, f.Phase(P));
            Assert.IsFalse(f.Press(P), "no hop from the floor");
            f.Reset(P);
            Assert.IsTrue(f.Press(P), "back home it hops again");
        }

        [Test]
        public void Frog_ChildShowsThenWaits()
        {
            var f = NewPond();
            Assert.IsFalse(f.ChildMayHop, "a moment first");
            f.Tick(FrogPond.ChildBeatSeconds, false);
            Assert.IsTrue(f.ChildMayHop, "the child shows a hop");
            Assert.IsTrue(f.Press(C) && f.Release(C));
            Assert.AreEqual(FrogTurn.Player, f.Whose, "then it is the player's turn");
            Land(f);
            f.Tick(FrogPond.PlayerTurnSeconds - 1.5f, false);
            Assert.IsFalse(f.ChildMayHop, "the child leaves time for the player");
            f.Press(P);
            f.Release(P);
            Assert.AreEqual(FrogTurn.Child, f.Whose);
            Assert.IsFalse(f.ChildMayHop, "a beat after the player's hop");
            Land(f);
            f.Tick(FrogPond.ChildBeatSeconds, false);
            Assert.IsTrue(f.ChildMayHop, "then the child again");
        }

        [Test]
        public void Frog_PlayerTurnTimesOut()
        {
            var f = NewPond();
            f.Tick(FrogPond.ChildBeatSeconds, false);
            f.Press(C);
            f.Release(C);
            Land(f);
            f.Tick(FrogPond.PlayerTurnSeconds * 2, true);
            Assert.IsFalse(f.ChildMayHop, "never while the player holds or presses a frog");
            f.Tick(FrogPond.PlayerTurnSeconds, false);
            Assert.IsTrue(f.ChildMayHop, "the player did not hop: the child goes on");
        }

        [Test]
        public void Frog_PlayerOwnPace()
        {
            var f = NewPond();
            for (var k = 0; k < 3; k++)
            {
                f.Place(P, FrogPond.Home(P), 0);
                Assert.IsTrue(f.Press(P) && f.Release(P), $"hop {k}: the player may hop any time");
                Land(f);
            }
            Assert.AreEqual(3, f.Hops(P));
            Assert.AreEqual(0, f.Hops(C));
            Assert.IsFalse(f.ChildMayHop, "the child waits a beat after the player's latest hop");
            f.Tick(FrogPond.ChildBeatSeconds, false);
            Assert.IsTrue(f.ChildMayHop);
        }
    }
}
