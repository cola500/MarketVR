// EPIC-015: customers walk the park's paths. The shop waits until the customer has actually arrived; the routes reach
// the right stall and stay on the gravel, clear of counters, table, shelf, bench, stools and the work area; visitors
// choose their stroll with weighted, injected randomness, share one bench seat and are limited in number.
// Names match the [T: ...] references in docs/EPIC-015.md.
using System.Collections.Generic;
using Fruktstand.Core;
using Fruktstand.Game;
using NUnit.Framework;
using UnityEngine;

namespace Fruktstand.Tests
{
    public class ParkVisitTests
    {
        static readonly float[] Spacings = { SpaceFit.MinStall, 0.80f, SpaceFit.MaxStall };

        [TearDown]
        public void ResetSpacing() => MarketLayout.SetStallDistance(MarketLayout.DefaultStallDistance);

        [Test]
        public void Shop_WaitsForTheCustomerToArrive()
        {
            var shop = new Shop(1) { WaitsForArrival = true };
            Assert.AreEqual(SaleState.CustomerArriving, shop.State);
            shop.CustomerArrived(); // too early: ArriveSeconds still applies, and it is remembered
            shop.Tick(30);
            Assert.AreEqual(SaleState.Ordering, shop.State, "arrived and the usual short pause has passed");

            shop = new Shop(1) { WaitsForArrival = true };
            shop.Tick(30);
            Assert.AreEqual(SaleState.CustomerArriving, shop.State, "still walking: no order yet");
            Assert.Less(shop.TryAddToBasket(Ware.Apple), 0, "nothing goes in the basket before the customer is there");
            Assert.IsFalse(shop.TryPay(), "no payment before the customer is there");
            shop.CustomerArrived();
            shop.Tick(0.01f);
            Assert.AreEqual(SaleState.Ordering, shop.State);
            Assert.GreaterOrEqual(shop.TryAddToBasket(Ware.Apple), 0);
            Assert.IsTrue(shop.TryPay());
            shop.Tick(Shop.ThankSeconds);
            shop.CustomerArrived(); // ignored outside CustomerArriving: not carried over to the next customer
            shop.Tick(Shop.LeaveSeconds);
            Assert.AreEqual(SaleState.CustomerArriving, shop.State, "the counter is free: the next customer is coming");
            shop.Tick(30);
            Assert.AreEqual(SaleState.CustomerArriving, shop.State, "and must walk in too");
        }

        // A random source that returns these numbers in turn (below n, wrapping).
        static System.Func<int, int> Sequence(params int[] values)
        {
            var i = 0;
            return n => values[i++ % values.Length] % n;
        }

        [Test]
        public void Visits_WeightedChoice()
        {
            Assert.AreEqual(10, ParkVisits.TotalWeight, "exit 5, detour 3, bench 2");
            var plans = new List<Stroll>();
            foreach (var r in new[] { 0, 4, 5, 7, 8 })
            {
                var visits = new ParkVisits(Sequence(r));
                var id = visits.Begin();
                plans.Add(visits.Choose(id));
            }
            CollectionAssert.AreEqual(new[] { Stroll.Exit, Stroll.Exit, Stroll.Detour, Stroll.Detour, Stroll.Bench }, plans);
        }

        [Test]
        public void Visits_BenchReservedAndFreed()
        {
            var visits = new ParkVisits(Sequence(9)); // always the bench, if it can be
            var first = visits.Begin();
            Assert.AreEqual(Stroll.Bench, visits.Choose(first));
            Assert.AreEqual(0, visits.FreeSeats, "the seat is reserved");
            visits.End(first);
            var second = visits.Begin();
            Assert.AreEqual(Stroll.Bench, visits.Choose(second), "freed when the first one left");
            var third = new ParkVisits(Sequence(9, 0));
            var a = third.Begin();
            Assert.AreEqual(Stroll.Bench, third.Choose(a));
            third.ReleaseSeat(a); // stood up, still walking
            Assert.AreEqual(1, third.FreeSeats);
            Assert.AreEqual(1, third.Active, "still a visitor until it has left the park");
        }

        [Test]
        public void Visits_LimitedVisitors()
        {
            var visits = new ParkVisits(Sequence(9));
            var a = visits.Begin();
            Assert.AreEqual(Stroll.Bench, visits.Choose(a));
            var b = visits.Begin();
            Assert.AreEqual(Stroll.Exit, visits.Choose(b), "no seat and no spare room: straight out");
            Assert.IsTrue(visits.Full);
            Assert.AreEqual(ParkVisits.MaxVisitors, visits.Active);
            Assert.Throws<System.InvalidOperationException>(() => visits.Begin(), "the caller makes room first");
            visits.Clear();
            Assert.AreEqual(0, visits.Active);
            Assert.AreEqual(1, visits.FreeSeats, "a new game frees the bench");
        }

        static IEnumerable<(string name, Polyline route)> AllRoutes(float d)
        {
            foreach (var c in WareInfo.Categories)
            {
                yield return ($"arrive {c}", ParkRoutes.Arrival(c, d));
                foreach (var plan in new[] { Stroll.Exit, Stroll.Detour, Stroll.Bench })
                    yield return ($"leave {c} {plan}", ParkRoutes.Departure(c, d, plan));
            }
            foreach (var toy in new[] { Ware.Ball, Ware.Car, Ware.Blocks })
                foreach (var plan in new[] { Stroll.Exit, Stroll.Detour, Stroll.Bench })
                    yield return ($"child {toy} {plan}", ParkRoutes.FromPlay(ParkRoutes.PlayLeft(toy, d), plan));
            yield return ("bench to exit", ParkRoutes.BenchToExit());
        }

        [Test]
        public void Routes_ArriveAtTheRightStall()
        {
            foreach (var d in Spacings)
                foreach (var c in WareInfo.Categories)
                {
                    var r = ParkRoutes.Arrival(c, d);
                    Assert.IsTrue(ParkRoutes.IsEntrance(r.Start), $"{c} d {d}: from an entrance");
                    Assert.AreEqual(0, Vector3.Distance(ParkRoutes.CounterSpot(c, d), r.End), 1e-4f, $"{c} d {d}: at its own stall");
                    var leave = ParkRoutes.Departure(c, d, Stroll.Exit);
                    Assert.AreEqual(0, Vector3.Distance(ParkRoutes.CounterSpot(c, d), leave.Start), 1e-4f, $"{c}: leaves from the counter");
                    Assert.IsTrue(ParkRoutes.IsEntrance(leave.End), $"{c}: out of the park");
                    Assert.IsTrue(ParkRoutes.IsEntrance(ParkRoutes.Departure(c, d, Stroll.Detour).End));
                    Assert.Greater(ParkRoutes.Departure(c, d, Stroll.Detour).Length, leave.Length, $"{c}: the detour is longer");
                    Assert.AreEqual(0, Vector3.Distance(ParkRoutes.BenchStand, ParkRoutes.Departure(c, d, Stroll.Bench).End), 1e-4f, "to the bench");
                }
            Assert.IsTrue(ParkRoutes.IsEntrance(ParkRoutes.BenchToExit().End));
        }

        [Test]
        public void Routes_StayOnThePathAndClear()
        {
            foreach (var d in Spacings)
            {
                var stools = new List<Vector3>();
                var (tp, tyaw) = MarketLayout.PlayTableAt(d);
                foreach (var toy in new[] { Ware.Ball, Ware.Car, Ware.Blocks }) stools.Add(MarketLayout.Local(tp, tyaw, PlayTableView.ChildSpot(toy)));
                foreach (var (name, route) in AllRoutes(d))
                {
                    for (var s = 0f; s <= route.Length; s += 0.05f)
                    {
                        var (p, _) = route.At(s);
                        var where = $"d {d}, {name}, at {s:0.00} m {p}";
                        var nearBench = Vector3.Distance(p, ParkRoutes.BenchStand) < ParkRoutes.BenchApproach + 0.01f;
                        var nearGate = Mathf.Abs(p.z) > ParkLayout.ArmEnd - 0.01f;
                        Assert.IsTrue(ParkLayout.OnPath(p) || nearBench || nearGate, $"on the gravel: {where}");
                        Assert.IsFalse(MarketLayout.InWorkArea(p), $"never in the work area: {where}");
                        foreach (var f in MarketCollision.FootprintsAt(d))
                            Assert.AreEqual(0, f.Depth(p, 0.15f), $"clear of {f.Name}: {where}");
                        foreach (var item in ParkLayout.Items)
                            if (item.Solid) Assert.AreEqual(0, item.Footprint.Depth(p, 0.1f), $"clear of {item.Piece} at {item.Position}: {where}");
                        if (!name.StartsWith("child"))
                            foreach (var stool in stools) Assert.GreaterOrEqual(Vector3.Distance(p, stool), 0.25f, $"past the children's stools: {where}");
                    }
                }
            }
        }
    }
}
