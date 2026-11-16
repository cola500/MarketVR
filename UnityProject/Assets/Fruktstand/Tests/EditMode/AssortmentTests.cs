// EPIC-009: eight fruit/veg wares, three active at a time (Core.Assortment + Market/Shop/RemoteCustomer wiring).
// Names match the [T: ...] references in docs/EPIC-009.md.
using System.Collections.Generic;
using System.Linq;
using Fruktstand.Core;
using Fruktstand.Game;
using NUnit.Framework;

namespace Fruktstand.Tests
{
    public class AssortmentTests
    {
        static readonly Ware[] Fruit = WareInfo.Catalogue(Category.FruitVeg);

        static void UntilOrdering(Market m)
        {
            for (var i = 0; i < 400 && m.Shop.State != SaleState.Ordering; i++) m.Tick(0.25f);
            Assert.AreEqual(SaleState.Ordering, m.Shop.State);
        }

        static int Pay(Market m)
        {
            UntilOrdering(m);
            var order = m.Shop.Order;
            foreach (var w in WareInfo.All)
                for (var i = 0; i < order.Count(w); i++) Assert.GreaterOrEqual(m.Shop.TryAddToBasket(w), 0);
            Assert.IsTrue(m.Shop.TryPay());
            return order.Sum;
        }

        static void Leave(Market m)
        {
            m.Tick(Shop.ThankSeconds);
            m.Tick(Shop.LeaveSeconds);
        }

        static string Key(Assortment a) => string.Join(",", a.Active(Category.FruitVeg));

        [Test]
        public void Catalogue_EightFruitAllPriceOne()
        {
            Assert.AreEqual(8, Fruit.Length);
            CollectionAssert.AllItemsAreUnique(Fruit);
            CollectionAssert.IsSubsetOf(new[] { Ware.Apple, Ware.Banana, Ware.Carrot, Ware.Pear, Ware.Orange, Ware.Strawberry, Ware.Grapes, Ware.Watermelon }, Fruit);
            foreach (var w in Fruit)
            {
                Assert.AreEqual(Category.FruitVeg, WareInfo.CategoryOf(w), w.ToString());
                Assert.AreEqual(1, WareInfo.Price(w), w.ToString());
                Assert.IsNotEmpty(WareInfo.Name(w));
                Assert.IsTrue(RemoteCustomer.TryParseWare(RemoteCustomer.KeyOf(w), out var back) && back == w, w + " has a phone key");
            }
            Assert.AreEqual(8, WareInfo.Catalogue(Category.Candy).Length, "EPIC-010 (CandyToyTests)");
            Assert.AreEqual(7, WareInfo.Catalogue(Category.Toys).Length); // EPIC-018: + hoppgrodor
            Assert.AreEqual(2, WareInfo.Price(Ware.Polkagris));
            Assert.AreEqual(4, WareInfo.Price(Ware.Blocks));
            Assert.AreEqual(WareInfo.Count, WareInfo.All.Length);
        }

        [Test]
        public void Assortment_ThreeDistinctFruit()
        {
            for (var seed = 1; seed <= 50; seed++)
            {
                var a = new Assortment(seed);
                for (var draw = 0; draw < 5; draw++)
                {
                    var active = a.Active(Category.FruitVeg);
                    Assert.AreEqual(3, active.Length);
                    CollectionAssert.AllItemsAreUnique(active);
                    CollectionAssert.IsSubsetOf(active, Fruit);
                    for (var bin = 0; bin < 3; bin++)
                    {
                        Assert.IsTrue(a.IsActive(active[bin]));
                        Assert.AreEqual(bin, a.Bin(active[bin]));
                    }
                    foreach (var w in Fruit.Except(active))
                    {
                        Assert.IsFalse(a.IsActive(w));
                        Assert.AreEqual(-1, a.Bin(w));
                    }
                    a.Redraw();
                }
            }
        }

        [Test]
        public void Assortment_SameSeedSameWares()
        {
            var a = new Assortment(42);
            var b = new Assortment(42);
            for (var i = 0; i < 10; i++)
            {
                Assert.AreEqual(Key(a), Key(b));
                a.Redraw();
                b.Redraw();
            }
        }

        [Test]
        public void Assortment_VariesOverSeeds()
        {
            var seen = new HashSet<string>();
            for (var seed = 1; seed <= 30; seed++) seen.Add(Key(new Assortment(seed)));
            Assert.Greater(seen.Count, 5, "different seeds give different assortments");
            var shown = new HashSet<Ware>();
            for (var seed = 1; seed <= 30; seed++) shown.UnionWith(new Assortment(seed).Active(Category.FruitVeg));
            Assert.AreEqual(8, shown.Count, "every fruit can be on sale");
        }

        [Test]
        public void Assortment_RedrawDiffers()
        {
            var a = new Assortment(7);
            for (var i = 0; i < 50; i++)
            {
                var before = new HashSet<Ware>(a.Active(Category.FruitVeg));
                var revision = a.Revision;
                a.Redraw();
                Assert.IsFalse(before.SetEquals(a.Active(Category.FruitVeg)), "a new draw is never the same three");
                Assert.AreNotEqual(revision, a.Revision);
            }
            var fixedOne = new Assortment(1, Ware.Pear, Ware.Grapes, Ware.Apple);
            CollectionAssert.AreEqual(new[] { Ware.Pear, Ware.Grapes, Ware.Apple }, fixedOne.Active(Category.FruitVeg), "a given start is kept in bin order");
            Assert.Throws<System.ArgumentException>(() => new Assortment(1, Ware.Pear, Ware.Pear, Ware.Apple));
            Assert.Throws<System.ArgumentException>(() => new Assortment(1, Ware.Pear, Ware.Lollipop, Ware.Apple));
        }

        [Test]
        public void Orders_OnlyActiveFruit()
        {
            foreach (var seed in new[] { 1, 2, 3, 11, 99 })
            {
                var m = new Market(seed, threshold: 10000, assortment: new Assortment(seed * 13)); // no party in between
                for (var customer = 0; customer < 40; customer++)
                {
                    UntilOrdering(m);
                    var order = m.Shop.Order;
                    if (order.Category == Category.FruitVeg)
                        foreach (var w in Fruit)
                            if (order.Count(w) > 0) Assert.IsTrue(m.Assortment.IsActive(w), $"seed {seed}: {order} only active fruit");
                    Assert.LessOrEqual(order.Total, OrderGenerator.MaxTotalFor(m.Shop.CustomerNumber));
                    Pay(m);
                    Leave(m);
                }
            }
        }

        [Test]
        public void Orders_FirstCustomerWantsFirstActiveFruit()
        {
            var m = new Market(assortment: new Assortment(1, Ware.Strawberry, Ware.Orange, Ware.Pear));
            UntilOrdering(m);
            Assert.AreEqual(Order.Of(Ware.Strawberry), m.Shop.Order);
            Assert.AreEqual(Order.Of(Ware.Apple), new Market().Shop.Order, "the default market keeps apple, banana, carrot");
        }

        [Test]
        public void Sale_EveryFruitSellsForOne()
        {
            foreach (var w in Fruit)
            {
                var others = Fruit.Where(f => f != w).Take(2).ToArray();
                var m = new Market(assortment: new Assortment(1, w, others[0], others[1]));
                UntilOrdering(m);
                Assert.AreEqual(Order.Of(w), m.Shop.Order, "customer 1 wants the first active fruit");
                Assert.AreEqual(0, m.Shop.TryAddToBasket(w));
                Assert.AreEqual(SaleState.AwaitingPayment, m.Shop.State, w + " matches");
                Assert.IsTrue(m.Shop.TryPay());
                Assert.AreEqual(1, m.Wallet.Balance, w + " costs 1");
            }
            var three = Order.Of(Ware.Grapes, Ware.Grapes, Ware.Watermelon);
            Assert.AreEqual(3, three.Sum);
        }

        [Test]
        public void Market_NewGameDrawsNewAssortment()
        {
            var m = new Market(assortment: new Assortment(5));
            var before = Key(m.Assortment);
            m.NewGame();
            Assert.AreNotEqual(before, Key(m.Assortment));
            UntilOrdering(m);
            Assert.AreEqual(Order.Of(m.Assortment.Active(Category.FruitVeg)[0]), m.Shop.Order, "customer 1 of the new game wants the new first fruit");
        }

        [Test]
        public void Market_NextRoundDrawsNewAssortment()
        {
            var m = new Market(threshold: 1, assortment: new Assortment(5));
            var before = Key(m.Assortment);
            Pay(m); // 1 >= threshold: party pending
            Assert.AreEqual(RoundState.PartyPending, m.Round.State);
            Leave(m);
            for (var i = 0; i < 100 && m.Round.State != RoundState.Resting; i++) m.Tick(0.5f, presentationReady: true);
            Assert.AreEqual(RoundState.Resting, m.Round.State);
            Assert.AreEqual(before, Key(m.Assortment), "the party and the rest keep the assortment");
            Assert.IsTrue(m.StartNextRound());
            Assert.AreNotEqual(before, Key(m.Assortment), "the next round has new fruit");
            UntilOrdering(m);
            if (m.Shop.Order.Category == Category.FruitVeg)
                foreach (var w in Fruit) if (m.Shop.Order.Count(w) > 0) Assert.IsTrue(m.Assortment.IsActive(w));
        }

        [Test]
        public void Market_AssortmentKeptDuringOrderAndPause()
        {
            var m = new Market(assortment: new Assortment(9));
            var before = Key(m.Assortment);
            UntilOrdering(m);
            Assert.GreaterOrEqual(m.Shop.TryAddToBasket(m.Assortment.Active(Category.FruitVeg)[1]), 0);
            m.Shop.ClearBasket();
            for (var i = 0; i < 40; i++) m.Tick(0.5f, presentationReady: false, paused: true);
            m.Shop.SetOpen(false);
            m.Shop.SetOpen(true);
            for (var i = 0; i < 5; i++)
            {
                Pay(m);
                Leave(m);
            }
            Assert.AreEqual(before, Key(m.Assortment), "orders, Töm korgen, pause and open/closed never change it");
        }

        [Test]
        public void Market_ChangeWaitsForOrderInProgress()
        {
            var m = new Market(assortment: new Assortment(3));
            var before = Key(m.Assortment);
            UntilOrdering(m);
            var order = m.Shop.Order;
            var customer = m.Shop.CustomerNumber;
            Assert.IsTrue(m.TakeRest(), "rest in the middle of an order");
            Assert.IsTrue(m.StartNextRound());
            Assert.AreEqual(before, Key(m.Assortment), "not while the customer is still at the counter");
            Assert.AreEqual(customer, m.Shop.CustomerNumber);
            Assert.AreEqual(order, m.Shop.Order, "the order carries on");
            Pay(m);
            Leave(m);
            Assert.AreNotEqual(before, Key(m.Assortment), "changed when the next customer came");
            Assert.AreEqual(customer + 1, m.Shop.CustomerNumber);
        }

        [Test]
        public void Market_AcceptedPhoneOrderSurvivesRoundChange()
        {
            var m = new Market(assortment: new Assortment(1, Ware.Apple, Ware.Pear, Ware.Grapes));
            UntilOrdering(m);
            Assert.IsTrue(m.Remote.Request("phone-0001", new[] { "pear", "grapes" }).Accepted, "an accepted phone order of active fruit");
            Assert.IsTrue(m.TakeRest());
            Assert.IsTrue(m.StartNextRound());
            Assert.AreEqual("Apple,Pear,Grapes", Key(m.Assortment), "not changed while the phone order is open");
            Pay(m); // the NPC
            Leave(m);
            UntilOrdering(m);
            Assert.IsTrue(m.Shop.CustomerIsHuman, "the phone customer is served");
            Assert.AreEqual(Order.Of(Ware.Pear, Ware.Grapes), m.Shop.Order);
            Assert.GreaterOrEqual(m.Shop.TryAddToBasket(Ware.Pear), 0);
            Assert.GreaterOrEqual(m.Shop.TryAddToBasket(Ware.Grapes), 0);
            Assert.IsTrue(m.Shop.TryPay());
            Assert.AreEqual(RemoteState.Done, m.Remote.Status("phone-0001").State);
            Leave(m);
            Assert.AreNotEqual("Apple,Pear,Grapes", Key(m.Assortment), "changed once the phone order was done");
        }

        [Test]
        public void Remote_InactiveFruitRejected()
        {
            var m = new Market(assortment: new Assortment(1, Ware.Apple, Ware.Banana, Ware.Carrot));
            var r = m.Remote.Request("phone-0002", new[] { "watermelon" });
            Assert.AreEqual(RemoteState.Rejected, r.State);
            Assert.AreEqual("inactive", r.Reason);
            Assert.IsNull(m.Remote.ActiveId);
            Assert.IsTrue(m.Remote.Request("phone-0002", new[] { "banana" }).Accepted, "not remembered: the same id may order an active fruit");
            Assert.IsTrue(m.Remote.Request("phone-0003", new[] { "lollipop" }).State == RemoteState.Rejected, "busy, as before");
        }
    
        [Test]
        public void Session_MarketCarriesActiveFruit()
        {
            var m = new Market(assortment: new Assortment(1, Ware.Pear, Ware.Apple, Ware.Grapes));
            var s = new RemoteSession(m.Shop, new PhotoStore());
            var first = s.PumpMarket();
            StringAssert.Contains("\"state\":\"playing\"", first);
            StringAssert.Contains("\"fruit\":[\"pear\",\"apple\",\"grapes\"]", first, "the three on sale, in bin order");
            Assert.IsNull(s.PumpMarket(), "only on change");
            m.NewGame();
            var after = s.PumpMarket();
            Assert.IsNotNull(after, "sent again when the fruit change");
            foreach (var w in m.Assortment.Active(Category.FruitVeg)) StringAssert.Contains("\"" + RemoteCustomer.KeyOf(w) + "\"", after);
            var inactive = System.Array.Find(Fruit, w => !m.Assortment.IsActive(w));
            var reply = s.Handle("{\"t\":\"order\",\"orderId\":\"order-0009abcd\",\"items\":[\"" + RemoteCustomer.KeyOf(inactive) + "\"]}").Single();
            StringAssert.Contains("\"reason\":\"inactive\"", reply);
        }
    }
}
