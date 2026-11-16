// Rules from SPEC §4 / EPIC-001 S3–S5. Names match the [T: ...] references in docs/EPIC-001.md.
using System.Collections.Generic;
using Fruktstand.Core;
using NUnit.Framework;

namespace Fruktstand.Tests
{
    public class ShopTests
    {
        static Shop Ordering(int seed = 1)
        {
            var shop = new Shop(seed);
            shop.Tick(Shop.ArriveSeconds);
            Assert.AreEqual(SaleState.Ordering, shop.State);
            return shop;
        }

        static void Fill(Shop shop)
        {
            foreach (var p in WareInfo.All)
                for (var i = 0; i < shop.Order.Count(p); i++) shop.TryAddToBasket(p);
        }

        // Walks a whole sale to the next customer's ordering state.
        static void CompleteSale(Shop shop)
        {
            Fill(shop);
            Assert.IsTrue(shop.TryPay());
            shop.Tick(Shop.ThankSeconds);
            shop.Tick(Shop.LeaveSeconds);
            shop.Tick(Shop.ArriveSeconds);
        }

        [Test]
        public void Orders_FirstIsOneApple()
        {
            Assert.AreEqual(Order.Of(Ware.Apple), new Shop(1).Order);
            Assert.AreEqual(Order.Of(Ware.Apple), new Shop(42).Order);
        }

        [Test]
        public void Orders_TotalOneToThree()
        {
            var gen = new OrderGenerator(7);
            var totals = new HashSet<int>();
            for (var n = 1; n <= 300; n++)
            {
                var o = gen.Next(n);
                Assert.That(o.Total, Is.InRange(1, OrderGenerator.MaxTotalFor(n)), $"customer {n}");
                totals.Add(o.Total);
            }
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, totals, "all sizes occur");
        }

        [Test]
        public void Orders_Deterministic()
        {
            var a = new OrderGenerator(3);
            var b = new OrderGenerator(3);
            for (var n = 1; n <= 50; n++) Assert.AreEqual(a.Next(n), b.Next(n));
            a.Reset();
            var c = new OrderGenerator(3);
            for (var n = 1; n <= 50; n++) Assert.AreEqual(c.Next(n), a.Next(n), "Reset restarts the same sequence");
        }

        [Test]
        public void Orders_NoRepeatInARow()
        {
            for (var seed = 1; seed <= 20; seed++)
            {
                var gen = new OrderGenerator(seed);
                Order previous = null;
                for (var n = 1; n <= 100; n++)
                {
                    var o = gen.Next(n);
                    Assert.AreNotEqual(previous, o, $"seed {seed} customer {n}");
                    previous = o;
                }
            }
        }

        [Test]
        public void Match_ExactCounts()
        {
            var b = new Basket();
            var order = Order.Of(Ware.Banana, Ware.Apple, Ware.Banana);
            b.Add(Ware.Banana);
            b.Add(Ware.Apple);
            Assert.IsFalse(b.Matches(order));
            b.Add(Ware.Banana);
            Assert.IsTrue(b.Matches(order), "order of items does not matter");
        }

        [Test]
        public void Match_TooFewTooManyWrongKind()
        {
            var order = Order.Of(Ware.Apple, Ware.Apple);
            var b = new Basket();
            b.Add(Ware.Apple);
            Assert.IsFalse(b.Matches(order), "too few");
            b.Add(Ware.Apple);
            b.Add(Ware.Apple);
            Assert.IsFalse(b.Matches(order), "too many");
            b.Clear();
            b.Add(Ware.Apple);
            b.Add(Ware.Carrot);
            Assert.IsFalse(b.Matches(order), "wrong kind");
            b.Clear();
            b.Add(Ware.Apple);
            b.Add(Ware.Apple);
            b.Add(Ware.Carrot);
            Assert.IsFalse(b.Matches(order), "right ones plus an extra kind");
        }

        [Test]
        public void Match_RemovedItemNoLongerCounts()
        {
            var shop = Ordering();
            CompleteSale(shop); // customer 2: fruit/veg, 1–2 items, so at least one ware of the category is not in the order
            var order = shop.Order;
            var wrong = System.Array.Find(shop.Assortment.Active(order.Category), p => order.Count(p) == 0);
            var wrongSlot = shop.TryAddToBasket(wrong);
            Fill(shop);
            Assert.AreEqual(SaleState.Ordering, shop.State, "an extra item blocks the match (no penalty, just not done)");
            Assert.AreEqual(wrong, shop.TakeFromBasket(wrongSlot));
            Assert.AreEqual(0, shop.Basket.Count(wrong));
            Assert.AreEqual(SaleState.AwaitingPayment, shop.State, "taking the wrong item out completes the order");

            // and a correct item taken out stops counting too
            var again = Ordering();
            var slot = again.TryAddToBasket(Ware.Carrot);
            Assert.AreEqual(Ware.Carrot, again.TakeFromBasket(slot));
            Assert.AreEqual(0, again.Basket.Total);
            Assert.IsNull(again.TakeFromBasket(slot), "taking from an empty slot does nothing");
        }

        [Test]
        public void Progress_SatisfiedPerOrderedUnit()
        {
            var order = Order.Of(Ware.Apple, Ware.Apple, Ware.Carrot);
            var b = new Basket();
            Assert.AreEqual(0, b.Satisfied(order, Ware.Apple));
            b.Add(Ware.Apple);
            Assert.AreEqual(1, b.Satisfied(order, Ware.Apple));
            b.Add(Ware.Apple);
            b.Add(Ware.Apple);
            Assert.AreEqual(2, b.Satisfied(order, Ware.Apple), "never more ticks than ordered");
            Assert.AreEqual(0, b.Satisfied(order, Ware.Banana));
        }

        [Test]
        public void Basket_FullRefusesAndItemReturns()
        {
            var shop = Ordering();
            // first order is one apple, so fill with other things to avoid a match
            for (var i = 0; i < Basket.Capacity; i++) Assert.GreaterOrEqual(shop.TryAddToBasket(Ware.Carrot), 0);
            Assert.IsTrue(shop.Basket.IsFull);
            Assert.IsFalse(shop.AcceptsItems);
            Assert.AreEqual(-1, shop.TryAddToBasket(Ware.Apple), "full basket refuses; the view sends the item home");
            Assert.AreEqual(Basket.Capacity, shop.Basket.Total);
        }

        [Test]
        public void Basket_PreferredSlotUsedWhenFree()
        {
            var b = new Basket();
            Assert.AreEqual(2, b.Add(Ware.Apple, 2));
            Assert.AreEqual(0, b.Add(Ware.Apple, 2), "taken slot falls back to the lowest free");
        }

        [Test]
        public void Sale_ArrivingCustomerDoesNotTakeItemsYet()
        {
            var shop = new Shop();
            Assert.AreEqual(SaleState.CustomerArriving, shop.State);
            Assert.AreEqual(-1, shop.TryAddToBasket(Ware.Apple));
            shop.Tick(Shop.ArriveSeconds * 0.5f);
            Assert.AreEqual(SaleState.CustomerArriving, shop.State);
            shop.Tick(Shop.ArriveSeconds * 0.5f);
            Assert.AreEqual(SaleState.Ordering, shop.State);
        }

        [Test]
        public void Sale_MatchMovesToAwaitingPaymentAndLocksBasket()
        {
            var shop = Ordering();
            Assert.IsFalse(shop.CoinOnCounter);
            var slot = shop.TryAddToBasket(Ware.Apple);
            Assert.AreEqual(SaleState.AwaitingPayment, shop.State);
            Assert.IsTrue(shop.CoinOnCounter);
            Assert.IsFalse(shop.BasketUnlocked);
            Assert.IsNull(shop.TakeFromBasket(slot), "basket is locked once it matches");
            Assert.AreEqual(-1, shop.TryAddToBasket(Ware.Banana), "nothing more goes in");
            Assert.AreEqual(1, shop.Basket.Total);
        }

        [Test]
        public void Sale_PaymentBeforeMatchIsRejected()
        {
            var shop = new Shop();
            Assert.IsFalse(shop.TryPay(), "while the customer arrives");
            shop.Tick(Shop.ArriveSeconds);
            Assert.IsFalse(shop.TryPay(), "empty basket");
            shop.TryAddToBasket(Ware.Banana);
            Assert.IsFalse(shop.TryPay(), "wrong basket");
            Assert.AreEqual(0, shop.CompletedSales);
            Assert.AreEqual(SaleState.Ordering, shop.State);
        }

        [Test]
        public void Sale_PaymentRegistersExactlyOnce()
        {
            var shop = Ordering();
            shop.TryAddToBasket(Ware.Apple);
            Assert.IsTrue(shop.TryPay());
            Assert.IsFalse(shop.TryPay(), "repeat in the same frame");
            shop.Tick(0.1f);
            Assert.IsFalse(shop.TryPay(), "repeat a frame later");
            shop.Tick(Shop.ThankSeconds);
            Assert.IsFalse(shop.TryPay(), "repeat while the customer leaves");
            Assert.AreEqual(1, shop.CompletedSales);
        }

        [Test]
        public void Sale_ThanksThenLeavesThenNextCustomer()
        {
            var shop = Ordering();
            shop.TryAddToBasket(Ware.Apple);
            shop.TryPay();
            Assert.AreEqual(SaleState.Thanking, shop.State);
            Assert.AreEqual(1, shop.Basket.Total, "goods stay in the basket during thanks");
            shop.Tick(Shop.ThankSeconds);
            Assert.AreEqual(SaleState.CustomerLeaving, shop.State);
            Assert.IsTrue(shop.Basket.IsEmpty, "cleared when the customer leaves");
            shop.Tick(Shop.LeaveSeconds);
            Assert.AreEqual(SaleState.CustomerArriving, shop.State);
            Assert.AreEqual(2, shop.CustomerNumber);
            Assert.AreNotEqual(Order.Of(Ware.Apple), shop.Order, "a new order");
            shop.Tick(Shop.ArriveSeconds);
            for (var i = 0; i < 20; i++) CompleteSale(shop);
            Assert.AreEqual(21, shop.CompletedSales, "customers keep coming");
            Assert.AreEqual(22, shop.CustomerNumber);
        }

        [Test]
        public void Sale_RestartResetsEverything()
        {
            var shop = Ordering();
            CompleteSale(shop);
            CompleteSale(shop);
            shop.TryAddToBasket(Ware.Carrot);
            shop.Restart();
            Assert.AreEqual(0, shop.CompletedSales);
            Assert.AreEqual(1, shop.CustomerNumber);
            Assert.AreEqual(Order.Of(Ware.Apple), shop.Order);
            Assert.IsTrue(shop.Basket.IsEmpty);
            Assert.AreEqual(SaleState.CustomerArriving, shop.State);
            var fresh = new Shop();
            for (var i = 0; i < 5; i++)
            {
                shop.Tick(Shop.ArriveSeconds);
                fresh.Tick(Shop.ArriveSeconds);
                Assert.AreEqual(fresh.Order, shop.Order, "same sequence as a fresh start");
                CompleteSaleFromOrdering(shop);
                CompleteSaleFromOrdering(fresh);
            }
        }

        static void CompleteSaleFromOrdering(Shop shop)
        {
            Fill(shop);
            shop.TryPay();
            shop.Tick(Shop.ThankSeconds);
            shop.Tick(Shop.LeaveSeconds);
        }
    }
}
