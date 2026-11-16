// EPIC-018 in the real scene, with the game's own grab and release (simulated hands): the frog set sold and carried to
// the play table, the child's first hop, pressing a frog's rear, turns, a miss, a pause in the middle of a press, and
// a frog in the player's hand. The frogs are on sale from the start (the deterministic scenario). Names match
// docs/EPIC-018.md.
using System.Collections;
using System.Linq;
using Fruktstand.Core;
using Fruktstand.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fruktstand.Tests
{
    public class FrogSceneTests
    {
        const float Dt = 1 / 72f;
        const FrogOwner P = FrogOwner.Player, C = FrogOwner.Child;
        public static readonly Ware[] FrogToys = { Ware.Frogs, Ware.Car, Ware.Blocks }; // one per zone
        FruitStand s;

        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return SceneLoad.Fresh(x => s = x, FrogToys);
            Assert.IsNotNull(s);
        }

        void Step(Vector3? right = null, bool grab = false, float dt = Dt, Quaternion? turn = null, bool releaseAll = false)
        {
            var f = new ShopFrame { DeltaTime = dt, ReleaseAll = releaseAll };
            if (right.HasValue) f.Right = new HandFrame { Tracked = true, Position = right.Value, Rotation = turn ?? Quaternion.identity, Grab = grab };
            s.Step(f);
        }

        void Wait(float seconds)
        {
            for (var t = 0f; t < seconds; t += 0.05f) Step(dt: 0.05f);
        }

        bool WaitUntil(System.Func<bool> condition, float seconds)
        {
            for (var t = 0f; t < seconds; t += 0.05f)
            {
                if (condition()) return true;
                Step(dt: 0.05f);
            }
            return condition();
        }

        static string MeshOf(Transform t) => t.GetComponentInChildren<MeshFilter>()?.sharedMesh?.name;

        void PickAndDrop(Item item, Vector3 to)
        {
            Step(item.Position);
            Step(item.Position, true);
            Assert.AreSame(item, s.Held(1));
            Step(Vector3.Lerp(item.Position, to, 0.5f), true);
            Step(to, true);
            Step(to, false);
        }

        // The whole chain: wait for the frog child, sell the set with the hands, the child carries it to the table.
        void SellFrogsAndWaitForPlay()
        {
            foreach (var t in FrogToys.Skip(1)) Assert.IsTrue(s.PlayArea.TryReserve(t, 99), $"{t}'s zone held");
            for (var k = 0; k < 12; k++)
            {
                Assert.IsTrue(WaitUntil(() => s.Shop.State == SaleState.Ordering, 60), "a customer is ordering");
                if (s.Shop.Order.Toy == Ware.Frogs) break;
                var order = s.Shop.Order;
                foreach (var w in WareInfo.All)
                    for (var i = 0; i < order.Count(w); i++) s.Step(new ShopFrame { DeltaTime = 0.02f, AddDirect = w });
                Wait(0.6f);
                Assert.IsTrue(s.RegisterCoin());
                Wait(Shop.ThankSeconds + Shop.LeaveSeconds);
            }
            Assert.AreEqual(Order.Of(Ware.Frogs), s.Shop.Order, "a child orders the frog set");
            Assert.IsTrue(s.Shop.CustomerIsChild);
            var stall = s.Stall(Category.Toys);
            var crate = s.CrateTop(Ware.Frogs);
            Assert.AreEqual("Frogs", MeshOf(crate.Transform), "the frog set in the shop");
            PickAndDrop(crate, stall.ToWorld(StandView.BasketCentre + Vector3.up * 0.12f));
            Wait(0.6f);
            var before = s.Wallet.Balance;
            PickAndDrop(s.Coin, stall.ToWorld(StandView.RegisterCentre + Vector3.up * 0.14f));
            Assert.AreEqual(before + 3, s.Wallet.Balance, "sold as a ware, 3 coins");
            Assert.AreEqual("Frogs", MeshOf(s.Play.CarriedToy(Ware.Frogs).Transform), "the child carries the very set");
            Assert.IsTrue(WaitUntil(() => s.PlayArea.State(Ware.Frogs) == SpotState.Playing, PlayArea.WalkSeconds + 6), "the frog child plays");
            Step(dt: 0.05f);
        }

        FrogPond Pond => s.Play.Frogs;
        Item Body(FrogOwner o) => s.Play.FrogItem(o);
        Item Rear(FrogOwner o) => s.Play.FrogRear(o);

        // The hand goes to the frog's rear, presses (grip), holds a moment and lifts (lets go).
        void PressAndLift(FrogOwner o, float hold = 0.3f)
        {
            var at = Rear(o).Position;
            Step(at);
            Assert.AreSame(Rear(o), s.Hover(1), "a hand at the rear highlights the press");
            Step(at, true);
            Assert.AreEqual(FrogPhase.Pressed, Pond.Phase(o), "pressed down");
            for (var t = 0f; t < hold; t += Dt) Step(at, true);
            Assert.AreEqual(FrogPhase.Pressed, Pond.Phase(o), "no hop while held down");
            Step(at + Vector3.up * 0.05f, false);
        }

        bool ChildDemoDone() => Pond.Hops(C) >= 1 && Pond.Phase(C) == FrogPhase.Sitting && !Body(C).Moving
                                && Vector3.Distance(Pond.Position(C), FrogPond.Home(C)) < 0.01f;

        [UnityTest]
        public IEnumerator Scene_FrogsSoldCarriedAndShown()
        {
            SellFrogsAndWaitForPlay();
            Assert.IsNotNull(Pond, "a pond at the table");
            foreach (var o in new[] { P, C })
            {
                Assert.AreEqual("Frog", MeshOf(Body(o).Transform), $"the {o}'s frog is the same frog");
                Assert.Less(Vector3.Distance(s.Play.ToTable(Body(o).Position), FrogPond.Home(o) + Vector3.up * s.Play.FrogLift), 0.01f, $"{o} at home");
            }
            Assert.AreEqual(0, s.Play.PhysicsBodies, "no physics bodies");
            Assert.IsTrue(WaitUntil(() => Pond.Hops(C) == 1, FrogPond.ChildBeatSeconds + 1), "the child shows a hop");
            Assert.IsTrue(WaitUntil(() => Pond.PadLandings(C) == 1, 1), "onto the pad");
            Assert.AreEqual(1, s.Play.Quacks, "a quack");
            Assert.IsTrue(WaitUntil(ChildDemoDone, 4), "and takes its frog home");
            Wait(FrogPond.PlayerTurnSeconds - 4);
            Assert.AreEqual(1, Pond.Hops(C), "then waits so that the player can try");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_FrogTurnsWithTheChild()
        {
            SellFrogsAndWaitForPlay();
            Assert.IsTrue(WaitUntil(ChildDemoDone, FrogPond.ChildBeatSeconds + 5), "the child's first hop");
            PressAndLift(P);
            Assert.AreEqual(1, Pond.Hops(P), "the hand let go: one hop");
            Assert.IsTrue(WaitUntil(() => Pond.PadLandings(P) == 1, 1), "onto the pad, unaimed");
            Assert.AreEqual(2, s.Play.Quacks);
            Step(dt: 0.5f);
            Assert.AreEqual(1, Pond.Hops(P), "exactly one");
            Assert.IsTrue(WaitUntil(() => Pond.Hops(C) == 2, FrogPond.ChildBeatSeconds + 1.5f), "the child's turn again");
            Assert.IsTrue(WaitUntil(ChildDemoDone, 4));
            // In the player's own pace: hop again straight away (from the pad).
            PressAndLift(P, 0.1f);
            Assert.AreEqual(2, Pond.Hops(P));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_FrogMissCarriesOn()
        {
            SellFrogsAndWaitForPlay();
            Assert.IsTrue(WaitUntil(ChildDemoDone, FrogPond.ChildBeatSeconds + 5));
            // Lift the frog and turn it to face the table's end (aim), set it down near the edge.
            var frog = Body(P);
            Step(frog.Position);
            Assert.AreSame(frog, s.Hover(1), "a hand at the frog's middle highlights the frog");
            Step(frog.Position, true);
            Assert.AreSame(frog, s.Held(1));
            var turned = s.Play.Top.rotation * Quaternion.Euler(0, -90, 0);
            var edge = s.Play.ToWorld(new Vector3(-0.42f, 0.08f, -0.05f));
            for (var i = 1; i <= 10; i++) Step(Vector3.Lerp(frog.Position, edge, i / 10f), true, Dt, turned);
            Step(edge, false, Dt, turned);
            Wait(0.3f);
            Assert.AreEqual(270, Pond.Yaw(P), 2, "set down facing the way the hand turned it");
            PressAndLift(P);
            Assert.IsTrue(WaitUntil(() => Pond.Phase(P) == FrogPhase.Fallen, 1), "off the table: a miss");
            Wait(0.6f);
            Assert.Less(s.Play.ToTable(frog.Position).y, -0.2f, "on the floor");
            Assert.IsTrue(WaitUntil(() => Pond.Phase(P) == FrogPhase.Sitting && !frog.Moving, PlayTableView.LostSeconds + 2), "it comes back");
            Assert.Less(Vector3.Distance(Pond.Position(P), FrogPond.Home(P)), 0.01f, "home again");
            PressAndLift(P);
            Assert.IsTrue(WaitUntil(() => Pond.PadLandings(P) == 1, 1), "and the next hop lands on the pad");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_FrogPauseMidPressNoHop()
        {
            SellFrogsAndWaitForPlay();
            var at = Rear(P).Position;
            Step(at);
            Step(at, true);
            Assert.AreEqual(FrogPhase.Pressed, Pond.Phase(P));
            for (var t = 0f; t < 1f; t += 0.05f) Step(at, true, 0.05f, releaseAll: true); // the menu opens mid-press
            for (var t = 0f; t < 0.5f; t += 0.05f) Step(at, true, 0.05f); // closed, the grip is still held
            Step(at, false);
            Wait(1f);
            Assert.AreEqual(0, Pond.Hops(P), "the frog never hops by itself afterwards");
            Assert.AreEqual(FrogPhase.Sitting, Pond.Phase(P));
            PressAndLift(P);
            Assert.AreEqual(1, Pond.Hops(P), "a fresh press hops");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_ChildNeverTakesAHeldFrog()
        {
            SellFrogsAndWaitForPlay();
            var frog = Body(C);
            var at = frog.Position;
            Step(at);
            Step(at, true);
            Assert.AreSame(frog, s.Held(1), "the player holds the child's frog");
            var hand = at + Vector3.up * 0.1f;
            for (var t = 0f; t < FrogPond.ChildBeatSeconds + FrogPond.PlayerTurnSeconds + 2; t += 0.05f) Step(hand, true, 0.05f);
            Assert.AreSame(frog, s.Held(1), "still in the hand");
            Assert.Less(Vector3.Distance(frog.Position, hand), 0.001f, "where the hand is");
            Assert.AreEqual(0, Pond.Hops(C), "the child never takes it");
            Step(s.Play.ToWorld(new Vector3(-0.1f, 0.06f, 0f)), false);
            Assert.IsTrue(WaitUntil(() => Pond.Hops(C) == 1, FrogPond.ChildBeatSeconds + 4), "let go: the child fetches it and hops");
            yield return null;
        }
    }
}
