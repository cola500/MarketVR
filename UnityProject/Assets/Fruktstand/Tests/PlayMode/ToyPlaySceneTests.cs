// EPIC-010 in the real scene: the new toys (top, train, stacking rings) are sold, carried to the table and played
// with by the child and the player; the physics budget; play outlives an assortment change. Names match EPIC-010.
using System.Collections;
using System.Linq;
using Fruktstand.Core;
using Fruktstand.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fruktstand.Tests
{
    public class ToyPlaySceneTests
    {
        static readonly Ware[] NewToys = { Ware.Top, Ware.Train, Ware.StackingRings }; // one per zone
        FruitStand s;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneLoad.Fresh(x => s = x, NewToys);
            Assert.IsNotNull(s);
        }

        void Step(Vector3? right = null, bool grab = false, float dt = 0.02f)
        {
            var f = new ShopFrame { DeltaTime = dt };
            if (right.HasValue) f.Right = new HandFrame { Tracked = true, Position = right.Value, Rotation = Quaternion.identity, Grab = grab };
            s.Step(f);
        }

        void Wait(float seconds)
        {
            for (var t = 0f; t < seconds; t += 0.05f) Step(dt: 0.05f);
        }

        // Steps until the condition holds (at most `seconds` of game time); true if it did.
        bool WaitUntil(System.Func<bool> condition, float seconds)
        {
            for (var t = 0f; t < seconds; t += 0.05f)
            {
                if (condition()) return true;
                Step(dt: 0.05f);
            }
            return condition();
        }

        void WaitForOrdering()
        {
            Assert.IsTrue(WaitUntil(() => s.Shop.State == SaleState.Ordering, 60), "a customer is ordering");
        }

        void Serve()
        {
            WaitForOrdering();
            var order = s.Shop.Order;
            foreach (var w in WareInfo.All)
                for (var i = 0; i < order.Count(w); i++) s.Step(new ShopFrame { DeltaTime = 0.02f, AddDirect = w });
            Wait(0.6f);
            Assert.IsTrue(s.RegisterCoin(), $"paid for {order}");
        }

        // The other free zones are held for a moment, customers are served until this toy's order comes, then the
        // placeholders are let go again; the child plays.
        void SellToyAndWaitForPlay(Ware toy)
        {
            var placeholders = NewToys.Where(t => PlayArea.SpotOf(t) != PlayArea.SpotOf(toy) && s.PlayArea.TryReserve(t, 99)).ToList();
            for (var k = 0; k < 10 && !(s.Shop.State == SaleState.Ordering && s.Shop.Order.Toy == toy); k++)
            {
                WaitForOrdering();
                if (s.Shop.Order.Toy == toy) break;
                Serve();
                Wait(Shop.ThankSeconds + Shop.LeaveSeconds);
            }
            Assert.AreEqual(toy, s.Shop.Order.Toy, $"a {toy} customer came");
            Serve();
            foreach (var t in placeholders) s.PlayArea.CancelReservation(t);
            Assert.IsTrue(WaitUntil(() => s.PlayArea.State(toy) == SpotState.Playing, PlayArea.WalkSeconds + 6), $"the {toy} child plays");
            Wait(0.3f);
        }

        Item PlayToy(Ware toy) => s.Play.Toys(toy).FirstOrDefault();
        Vector3 TableLocal(Item toy) => s.Play.ToTable(toy.Position);

        void Take(Item item)
        {
            Step(item.Position);
            Assert.AreSame(item, s.Hover(1), $"hand next to {item.Transform.name} highlights it");
            Step(item.Position, true);
            Assert.AreSame(item, s.Held(1));
        }

        void DropAt(Vector3 world)
        {
            var from = s.Held(1).Position;
            Step(Vector3.Lerp(from, world, 0.5f), true);
            Step(world, true);
            Step(world, false);
        }

        [UnityTest]
        public IEnumerator Scene_TopSpinsAndPlayerCanJoin()
        {
            SellToyAndWaitForPlay(Ware.Top);
            var top = PlayToy(Ware.Top);
            Assert.IsNotNull(top, "the very top the child carried is on the table");
            Assert.AreEqual(Ware.Top.ToString(), top.Transform.GetComponentInChildren<MeshFilter>().sharedMesh.name, "the shop's model");
            Assert.IsTrue(WaitUntil(() => s.Play.Spinning(Ware.Top), 6), "the child sets it spinning");
            var yaw = top.Transform.eulerAngles.y;
            Wait(0.3f);
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(yaw, top.Transform.eulerAngles.y)), 0.5f, "it turns");
            Take(top);
            Assert.AreEqual(ToyControl.Player, s.PlayArea.Control(Ware.Top));
            Assert.IsFalse(s.Play.Spinning(Ware.Top), "not while in the hand");
            DropAt(s.Play.ToWorld(new Vector3(-0.28f, 0.08f, 0.0f)));
            Assert.IsTrue(WaitUntil(() => s.Play.Spinning(Ware.Top), 5), "it spins again when it has landed");
            Assert.IsTrue(WaitUntil(() => s.PlayArea.Control(Ware.Top) == ToyControl.Child, 6), "and the child takes over");
            Assert.Greater(Vector3.Dot(top.Transform.up, s.Play.Top.up), 0.95f, "upright on its point");
            Assert.Greater(TableLocal(top).y, -0.02f, "on the table");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_TrainDrivesTheRoad()
        {
            SellToyAndWaitForPlay(Ware.Train);
            var train = PlayToy(Ware.Train);
            Assert.IsNotNull(train);
            var start = TableLocal(train);
            Wait(2f);
            var local = TableLocal(train);
            Assert.Greater(Vector3.Distance(start, local), 0.05f, "it drives");
            var t = PlayTableView.NearestRoadT(local);
            Assert.Less(new Vector2(local.x - PlayTableView.RoadPoint(t).x, local.z - PlayTableView.RoadPoint(t).z).magnitude, 0.03f, "along the road");
            Take(train);
            DropAt(s.Play.ToWorld(new Vector3(0.0f, 0.1f, -0.16f)));
            Assert.IsTrue(WaitUntil(() => s.PlayArea.Control(Ware.Train) == ToyControl.Child, 6), "the child takes over again");
            Wait(1.5f);
            local = TableLocal(train);
            t = PlayTableView.NearestRoadT(local);
            Assert.Less(new Vector2(local.x - PlayTableView.RoadPoint(t).x, local.z - PlayTableView.RoadPoint(t).z).magnitude, 0.03f, "back on the road");
            Assert.Greater(Vector3.Dot(train.Transform.up, s.Play.Top.up), 0.95f, "upright");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_RingsAreStackedAndPlayerHelps()
        {
            SellToyAndWaitForPlay(Ware.StackingRings);
            var rings = s.Play.Toys(Ware.StackingRings).ToList();
            Assert.AreEqual(WareInfo.RingsInSet, rings.Count, "three rings at the table");
            Assert.IsTrue(s.Play.HasPeg(Ware.StackingRings), "and the peg");
            Assert.IsTrue(WaitUntil(() => s.Play.RingsOnPeg == 3, 12), "the child stacks all three");
            foreach (var r in rings) Assert.AreEqual(r.Index, r.Slot, "the biggest at the bottom");
            var middle = rings.First(r => r.Slot == 1);
            var topRing = rings.First(r => r.Slot == 2);
            Take(middle);
            Assert.AreEqual(-1, topRing.Slot, "the ring above comes off the peg");
            Assert.AreEqual(1, s.Play.RingsOnPeg);
            // Released a little beside the peg: it is helped onto the top.
            DropAt(s.Play.ToWorld(PlayTableView.RingSpot + new Vector3(0.05f, 0.15f, 0.03f)));
            Wait(0.6f);
            Assert.AreEqual(1, middle.Slot, "snapped onto the peg, on top");
            Assert.Less(new Vector2(TableLocal(middle).x - PlayTableView.RingSpot.x, TableLocal(middle).z - PlayTableView.RingSpot.z).magnitude, 0.01f);
            Assert.IsTrue(WaitUntil(() => s.Play.RingsOnPeg == 3, 12), "the child puts the last one back: " +
                string.Join(" / ", rings.Select(r => $"ring {r.Index} slot {r.Slot} {r.Place} at {TableLocal(r)} v {(r.Body != null ? r.Body.linearVelocity.magnitude : -1):F3} kin {r.Body?.isKinematic} moving {r.Moving}")));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_TidyAndLostForNewToys()
        {
            SellToyAndWaitForPlay(Ware.Top);
            var top = PlayToy(Ware.Top);
            Take(top);
            DropAt(s.Play.ToWorld(new Vector3(0, 0.1f, -0.6f))); // off the table, onto the floor
            Wait(1);
            Assert.Less(TableLocal(top).y, -PlayTableView.LostBelow, "on the floor");
            Assert.IsTrue(WaitUntil(() => TableLocal(top).y > -0.02f, PlayTableView.LostSeconds + 3), "back on the table by itself");

            SellToyAndWaitForPlay(Ware.StackingRings);
            var ring = s.Play.Toys(Ware.StackingRings).First(r => r.Slot < 0 || r.Slot == s.Play.RingsOnPeg - 1);
            Take(ring);
            s.Step(new ShopFrame { DeltaTime = 0.02f, TidyPlay = true, Right = new HandFrame { Tracked = true, Position = ring.Position, Grab = true, Rotation = Quaternion.identity } });
            Assert.AreSame(ring, s.Held(1), "tidying leaves the ring in the hand");
            DropAt(s.Play.ToWorld(new Vector3(-0.45f, 0.1f, -0.18f)));
            Wait(0.5f);
            s.TidyPlayArea();
            Wait(0.8f);
            var home = PlayTableView.Home(Ware.StackingRings, ring.Index);
            var local = TableLocal(ring);
            Assert.Less(new Vector2(local.x - home.x, local.z - home.z).magnitude, 0.04f, "tidied back to its place");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_PhysicsWithinBudget()
        {
            ParkRoutes.WalkSpeed = 30; // EPIC-015: not about walking; three children must be at the table together
            try
            {
                foreach (var toy in NewToys) SellToyAndWaitForPlay(toy);
                Assert.AreEqual(3, s.PlayArea.Children, "three children play");
                Assert.AreEqual(1 + 1 + WareInfo.RingsInSet, s.Play.PhysicsBodies, "top 1 + train 1 + three rings (the peg is fixed)");
                Assert.LessOrEqual(s.Play.PhysicsBodies, PlayArea.MaxBodies);
                yield return null;
            }
            finally { ParkRoutes.WalkSpeed = ParkRoutes.DefaultWalkSpeed; }
        }

        [UnityTest]
        public IEnumerator Scene_PlayOutlivesAssortmentChange()
        {
            SellToyAndWaitForPlay(Ware.Top);
            var top = PlayToy(Ware.Top);
            for (var i = 0; i < 20 && s.Market.Assortment.IsActive(Ware.Top); i++) s.Market.Assortment.Redraw();
            Assert.IsFalse(s.Market.Assortment.IsActive(Ware.Top));
            Wait(1f);
            Assert.AreEqual(SpotState.Playing, s.PlayArea.State(Ware.Top), "the child plays on");
            Assert.AreSame(top, PlayToy(Ware.Top), "with the same top");
            Assert.AreNotEqual(ItemPlace.Gone, top.Place);
            CollectionAssert.AreEqual(s.Market.Assortment.Active(Category.Toys), s.Stall(Category.Toys).Wares, "the toy stall shows the new toys");
            Assert.IsTrue(WaitUntil(() => s.Play.Spinning(Ware.Top), 6), "and still spins it");
            yield return null;
        }
    }
}
