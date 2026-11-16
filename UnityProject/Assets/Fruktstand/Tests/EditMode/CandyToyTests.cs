// EPIC-010: candy and toys with their own catalogues, three active each, play zones and the physics budget.
// Names match the [T: ...] references in docs/EPIC-010.md.
using System.Collections.Generic;
using System.Linq;
using Fruktstand.Core;
using Fruktstand.Game;
using NUnit.Framework;

namespace Fruktstand.Tests
{
    public class CandyToyTests
    {
        static readonly Ware[] Candy = WareInfo.Catalogue(Category.Candy);
        static readonly Ware[] Toys = WareInfo.Catalogue(Category.Toys);
        static readonly Ware[] NewCandy = { Ware.GummyBear, Ware.Chocolate, Ware.WrappedCandy, Ware.RainbowStrip, Ware.CandyRing };
        static readonly Ware[] NewToys = { Ware.Top, Ware.Train, Ware.StackingRings };

        static void UntilOrdering(Market m)
        {
            for (var i = 0; i < 400 && m.Shop.State != SaleState.Ordering; i++) m.Tick(0.25f);
            Assert.AreEqual(SaleState.Ordering, m.Shop.State);
        }

        static void Fill(Market m)
        {
            var order = m.Shop.Order;
            foreach (var w in WareInfo.All)
                for (var i = 0; i < order.Count(w); i++) Assert.GreaterOrEqual(m.Shop.TryAddToBasket(w), 0, w.ToString());
        }

        static void Leave(Market m)
        {
            m.Tick(Shop.ThankSeconds);
            m.Tick(Shop.LeaveSeconds);
        }

        // Serves customers until one wants `ware`; returns false if none did within the limit.
        static bool ServeUntilOrderHas(Market m, Ware ware, int limit = 300)
        {
            for (var i = 0; i < limit; i++)
            {
                UntilOrdering(m);
                if (m.Shop.Order.Count(ware) > 0) return true;
                Fill(m);
                Assert.IsTrue(m.Shop.TryPay());
                Leave(m);
                m.Tick(PlayArea.PlaySeconds); // children finish playing, so every zone frees up again
                m.Tick(PlayArea.LingerSeconds + PlayArea.LeaveSeconds + 1);
            }
            return false;
        }

        [Test]
        public void Catalogue_CandyAndToys()
        {
            Assert.AreEqual(8, Candy.Length);
            Assert.AreEqual(7, Toys.Length); // EPIC-018: + hoppgrodor
            CollectionAssert.AllItemsAreUnique(Candy.Concat(Toys));
            CollectionAssert.IsSubsetOf(NewCandy.Concat(new[] { Ware.Lollipop, Ware.Polkagris, Ware.CandyBag }), Candy);
            CollectionAssert.IsSubsetOf(NewToys.Concat(new[] { Ware.Ball, Ware.Car, Ware.Blocks }), Toys);
            foreach (var w in Candy)
            {
                Assert.AreEqual(Category.Candy, WareInfo.CategoryOf(w), w.ToString());
                Assert.That(WareInfo.Price(w), Is.InRange(1, 3), w + " costs 1-3");
                Assert.IsNotEmpty(WareInfo.Name(w));
                Assert.IsTrue(RemoteCustomer.TryParseWare(RemoteCustomer.KeyOf(w), out var back) && back == w, w + " has a phone key");
            }
            foreach (var w in Toys)
            {
                Assert.AreEqual(Category.Toys, WareInfo.CategoryOf(w), w.ToString());
                Assert.IsTrue(WareInfo.IsToy(w));
                Assert.That(WareInfo.Price(w), Is.InRange(2, 4), w + " costs 2-4");
                Assert.IsNotEmpty(WareInfo.Name(w));
                Assert.IsFalse(RemoteCustomer.TryParseWare(WareInfo.Name(w), out _), "toys are not ordered from the phone");
            }
            // the existing prices are kept
            Assert.AreEqual(1, WareInfo.Price(Ware.Lollipop));
            Assert.AreEqual(2, WareInfo.Price(Ware.Polkagris));
            Assert.AreEqual(3, WareInfo.Price(Ware.CandyBag));
            Assert.AreEqual(2, WareInfo.Price(Ware.Ball));
            Assert.AreEqual(3, WareInfo.Price(Ware.Car));
            Assert.AreEqual(4, WareInfo.Price(Ware.Blocks));
            Assert.AreEqual(WareInfo.Count, WareInfo.All.Length);
            CollectionAssert.AllItemsAreUnique(WareInfo.All);
        }

        [Test]
        public void Assortment_ThreeDistinctPerCategory()
        {
            var seen = new Dictionary<Category, HashSet<Ware>>();
            foreach (var c in WareInfo.Categories) seen[c] = new HashSet<Ware>();
            for (var seed = 1; seed <= 40; seed++)
            {
                var a = new Assortment(seed);
                for (var draw = 0; draw < 4; draw++)
                {
                    foreach (var c in WareInfo.Categories)
                    {
                        var active = a.Active(c);
                        Assert.AreEqual(3, active.Length, c.ToString());
                        CollectionAssert.AllItemsAreUnique(active);
                        CollectionAssert.IsSubsetOf(active, WareInfo.Catalogue(c));
                        for (var bin = 0; bin < 3; bin++) Assert.AreEqual(bin, a.Bin(active[bin]));
                        foreach (var w in WareInfo.Catalogue(c).Except(active)) Assert.IsFalse(a.IsActive(w));
                        seen[c].UnionWith(active);
                    }
                    a.Redraw();
                }
            }
            foreach (var c in WareInfo.Categories) Assert.AreEqual(WareInfo.Catalogue(c).Length, seen[c].Count, c + ": every ware can be on sale");
            var b1 = new Assortment(77);
            var b2 = new Assortment(77);
            foreach (var c in WareInfo.Categories) CollectionAssert.AreEqual(b1.Active(c), b2.Active(c), "same seed, same wares");
        }

        [Test]
        public void Assortment_RedrawChangesEveryCategory()
        {
            var a = new Assortment(3);
            for (var i = 0; i < 30; i++)
            {
                var before = WareInfo.Categories.ToDictionary(c => c, c => new HashSet<Ware>(a.Active(c)));
                a.Redraw();
                foreach (var c in WareInfo.Categories) Assert.IsFalse(before[c].SetEquals(a.Active(c)), c + " changed");
            }
            var given = new Assortment(1, Ware.GummyBear, Ware.Chocolate, Ware.CandyRing, Ware.Top, Ware.Train, Ware.Ball);
            CollectionAssert.AreEqual(Assortment.ClassicOf(Category.FruitVeg), given.Active(Category.FruitVeg), "categories not given keep the classic wares");
            CollectionAssert.AreEqual(new[] { Ware.GummyBear, Ware.Chocolate, Ware.CandyRing }, given.Active(Category.Candy));
            CollectionAssert.AreEqual(new[] { Ware.Top, Ware.Train, Ware.Ball }, given.Active(Category.Toys));
            Assert.Throws<System.ArgumentException>(() => new Assortment(1, Ware.Top, Ware.Train));
            CollectionAssert.AreEqual(new[] { Ware.Lollipop, Ware.Polkagris, Ware.CandyBag }, new Market().Assortment.Active(Category.Candy), "the default market is classic");
            CollectionAssert.AreEqual(new[] { Ware.Ball, Ware.Car, Ware.Blocks }, new Market().Assortment.Active(Category.Toys));
        }

        [Test]
        public void Orders_OnlyActiveCandyAndToys()
        {
            foreach (var seed in new[] { 1, 4, 9, 21 })
            {
                var m = new Market(seed, threshold: 100000, assortment: new Assortment(seed * 7));
                for (var customer = 0; customer < 45; customer++)
                {
                    UntilOrdering(m);
                    var order = m.Shop.Order;
                    foreach (var w in WareInfo.All)
                        if (order.Count(w) > 0) Assert.IsTrue(m.Assortment.IsActive(w), $"seed {seed}: {order} only active wares");
                    Assert.LessOrEqual(m.PlayArea.Children, PlayArea.Spots);
                    Fill(m);
                    Assert.IsTrue(m.Shop.TryPay());
                    Leave(m);
                }
            }
        }

        [Test]
        public void Sale_EveryNewCandyAndToy()
        {
            foreach (var w in NewCandy.Concat(NewToys))
            {
                var c = WareInfo.CategoryOf(w);
                var others = WareInfo.Catalogue(c).Where(x => x != w && (c != Category.Toys || PlayArea.SpotOf(x) != PlayArea.SpotOf(w))).Take(2);
                var m = new Market(threshold: 100000, assortment: new Assortment(5, new[] { w }.Concat(others).ToArray()));
                Assert.IsTrue(ServeUntilOrderHas(m, w), w + " is ordered at some point");
                var before = m.Wallet.Balance;
                var sum = m.Shop.Order.Sum;
                Assert.AreEqual(WareInfo.All.Sum(x => m.Shop.Order.Count(x) * WareInfo.Price(x)), sum, w + ": the order is priced from the catalogue");
                Assert.Greater(m.Shop.Order.Count(w) * WareInfo.Price(w), 0);
                Fill(m);
                Assert.IsTrue(m.Shop.TryPay());
                Assert.AreEqual(before + sum, m.Wallet.Balance);
                if (c == Category.Toys) Assert.AreEqual(SpotState.Walking, m.PlayArea.State(w), "the child takes the toy to the play area");
            }
        }

        [Test]
        public void Play_ZonePerToyAndReservation()
        {
            Assert.AreEqual(PlayArea.SpotOf(Ware.Ball), PlayArea.SpotOf(Ware.Top), "left: ball, top");
            Assert.AreEqual(PlayArea.SpotOf(Ware.Car), PlayArea.SpotOf(Ware.Train), "middle: car, train (the road)");
            Assert.AreEqual(PlayArea.SpotOf(Ware.Blocks), PlayArea.SpotOf(Ware.StackingRings), "right: blocks, rings");
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, new[] { Ware.Ball, Ware.Car, Ware.Blocks }.Select(PlayArea.SpotOf));
            var area = new PlayArea();
            Assert.IsTrue(area.TryReserve(Ware.Top, 2));
            Assert.AreEqual(Ware.Top, area.ToyIn(PlayArea.SpotOf(Ware.Top)));
            Assert.AreEqual(SpotState.Reserved, area.State(Ware.Top));
            Assert.IsFalse(area.IsFree(Ware.Ball), "the left zone is taken by the top");
            Assert.IsFalse(area.TryReserve(Ware.Ball, 3));
            Assert.AreEqual(SpotState.Free, area.State(Ware.Ball), "the ball itself is not at the table");
            Assert.IsTrue(area.IsFree(Ware.Train));
            Assert.IsTrue(area.Sold(Ware.Top));
            Assert.IsFalse(area.Sold(Ware.Ball), "only the reserved toy can be sold into its zone");
            area.Tick(PlayArea.WalkSeconds + 0.1f);
            Assert.AreEqual(SpotState.Playing, area.State(Ware.Top));
            Assert.AreEqual(ToyControl.Child, area.Control(Ware.Top));
            area.PlayerTook(Ware.Ball); // not playing: nothing happens
            Assert.AreEqual(ToyControl.Child, area.Control(Ware.Top));
            area.Tick(PlayArea.PlaySeconds + PlayArea.LingerSeconds);
            area.Tick(PlayArea.LeaveSeconds + 0.1f);
            Assert.IsTrue(area.IsFree(Ware.Ball), "free again for any toy of the zone");
            Assert.IsNull(area.ToyIn(PlayArea.SpotOf(Ware.Ball)));
        }

        [Test]
        public void Play_ToyOrderOnlyWithFreeZone()
        {
            // Ball and top share the left zone: while one plays, the other is never offered.
            var m = new Market(threshold: 100000, assortment: new Assortment(2, Ware.Ball, Ware.Top, Ware.Car));
            var toyOrders = 0;
            for (var customer = 0; customer < 60; customer++)
            {
                UntilOrdering(m);
                if (m.Shop.Order.Toy is Ware toy)
                {
                    toyOrders++;
                    Assert.AreEqual(SpotState.Reserved, m.PlayArea.State(toy), "its zone was free and is now reserved");
                    foreach (var other in m.Assortment.Active(Category.Toys))
                        if (other != toy && PlayArea.SpotOf(other) == PlayArea.SpotOf(toy)) Assert.AreEqual(SpotState.Free, m.PlayArea.State(other));
                }
                Assert.LessOrEqual(m.PlayArea.Children, PlayArea.Spots);
                Fill(m);
                Assert.IsTrue(m.Shop.TryPay());
                Leave(m);
            }
            Assert.Greater(toyOrders, 2, "toys are still sold");
        }

        [Test]
        public void Play_SurvivesAssortmentChange()
        {
            var m = new Market(threshold: 100000, assortment: new Assortment(1, Ware.Ball, Ware.Car, Ware.Blocks));
            Assert.IsTrue(ServeUntilOrderHas(m, Ware.Ball));
            Fill(m);
            Assert.IsTrue(m.Shop.TryPay());
            m.Tick(PlayArea.WalkSeconds + 0.1f);
            Assert.AreEqual(SpotState.Playing, m.PlayArea.State(Ware.Ball));
            for (var i = 0; i < 10 && m.Assortment.IsActive(Ware.Ball); i++) m.Assortment.Redraw();
            Assert.IsFalse(m.Assortment.IsActive(Ware.Ball));
            m.Tick(1f);
            Assert.AreEqual(SpotState.Playing, m.PlayArea.State(Ware.Ball), "the child keeps playing with a toy no longer on sale");
            Assert.AreEqual(Ware.Ball, m.PlayArea.ToyIn(PlayArea.SpotOf(Ware.Ball)));
            m.NewGame(); // the only thing that clears the play area
            Assert.IsNull(m.PlayArea.ToyIn(PlayArea.SpotOf(Ware.Ball)));
        }

        [Test]
        public void Play_PhysicsBudget()
        {
            Assert.AreEqual(8, PlayArea.MaxBodies, "1 + 1 + 6: the biggest toy per zone");
            Assert.AreEqual(1, PlayArea.BodiesOf(Ware.Ball));
            Assert.AreEqual(1, PlayArea.BodiesOf(Ware.Top));
            Assert.AreEqual(1, PlayArea.BodiesOf(Ware.Car));
            Assert.AreEqual(1, PlayArea.BodiesOf(Ware.Train));
            Assert.AreEqual(WareInfo.BlocksInSet, PlayArea.BodiesOf(Ware.Blocks));
            Assert.AreEqual(WareInfo.RingsInSet, PlayArea.BodiesOf(Ware.StackingRings));
            // Every possible set of three active toys, with every zone filled as far as it can be, stays in the budget.
            foreach (var a in Toys)
            foreach (var b in Toys)
            foreach (var c in Toys)
            {
                if (a == b || b == c || a == c) continue;
                var perZone = new int[PlayArea.Spots];
                foreach (var t in new[] { a, b, c }) perZone[PlayArea.SpotOf(t)] = System.Math.Max(perZone[PlayArea.SpotOf(t)], PlayArea.BodiesOf(t));
                Assert.LessOrEqual(perZone.Sum(), PlayArea.MaxBodies);
            }
        }

        [Test]
        public void Session_MarketCarriesActiveCandy()
        {
            var m = new Market(assortment: new Assortment(1, Ware.GummyBear, Ware.Lollipop, Ware.CandyRing));
            var s = new RemoteSession(m.Shop, new PhotoStore());
            var first = s.PumpMarket();
            StringAssert.Contains("\"candy\":[\"gummybear\",\"lollipop\",\"candyring\"]", first);
            StringAssert.Contains("\"fruit\":[\"apple\",\"banana\",\"carrot\"]", first);
            StringAssert.DoesNotContain("ball", first, "toys are not sent: the phone can't order them");
            m.NewGame();
            var after = s.PumpMarket();
            foreach (var w in m.Assortment.Active(Category.Candy)) StringAssert.Contains("\"" + RemoteCustomer.KeyOf(w) + "\"", after);
        }

        [Test]
        public void Remote_InactiveCandyRejected()
        {
            var m = new Market(assortment: new Assortment(1, Ware.GummyBear, Ware.Lollipop, Ware.CandyRing));
            var r = m.Remote.Request("phone-0101", new[] { "chocolate" });
            Assert.AreEqual(RemoteState.Rejected, r.State);
            Assert.AreEqual("inactive", r.Reason);
            Assert.IsTrue(m.Remote.Request("phone-0101", new[] { "gummybear", "candyring" }).Accepted, "active candy is fine; the id was not remembered");
        }
    }
}
