// EPIC-003 rules: catalogue, one order at a time per category, the shared till, the play area, stacking help and the
// market layout. Names match the [T: ...] references in docs/EPIC-003.md.
using System.Collections.Generic;
using Fruktstand.Core;
using Fruktstand.Game;
using NUnit.Framework;
using UnityEngine;

namespace Fruktstand.Tests
{
    public class MarketTests
    {
        // Serves the current customer completely (from any state) and walks on until the next one is ordering.
        static Order Sell(Market m)
        {
            var guard = 0;
            while (m.Shop.State != SaleState.Ordering && guard++ < 200) m.Shop.Tick(0.5f);
            var order = m.Shop.Order;
            foreach (var w in WareInfo.All)
                for (var i = 0; i < order.Count(w); i++) Assert.GreaterOrEqual(m.Shop.TryAddToBasket(w), 0);
            Assert.IsTrue(m.Shop.TryPay());
            m.Shop.Tick(Shop.ThankSeconds);
            m.Shop.Tick(Shop.LeaveSeconds);
            return order;
        }

        [Test]
        public void Catalog_CategoriesAndPrices()
        {
            Assert.AreEqual(23, WareInfo.All.Length); // EPIC-009/010/018: 8 fruit/veg, 8 candy, 7 toys (AssortmentTests, CandyToyTests, FrogTests)
            foreach (var c in WareInfo.Categories) Assert.AreEqual(3, new Market().Assortment.Active(c).Length, "three bins per stall");
            CollectionAssert.AreEqual(new[] { Ware.Apple, Ware.Banana, Ware.Carrot }, new Market().Assortment.Active(Category.FruitVeg), "the default market");
            CollectionAssert.AreEqual(new[] { Ware.Lollipop, Ware.Polkagris, Ware.CandyBag }, new Market().Assortment.Active(Category.Candy), "the default market");
            CollectionAssert.AreEqual(new[] { Ware.Ball, Ware.Car, Ware.Blocks }, new Market().Assortment.Active(Category.Toys));
            foreach (var w in WareInfo.Catalogue(Category.FruitVeg)) Assert.AreEqual(1, WareInfo.Price(w), "1 per fruit/veg");
            Assert.AreEqual(1, WareInfo.Price(Ware.Lollipop));
            Assert.AreEqual(2, WareInfo.Price(Ware.Polkagris));
            Assert.AreEqual(3, WareInfo.Price(Ware.CandyBag));
            Assert.AreEqual(2, WareInfo.Price(Ware.Ball));
            Assert.AreEqual(3, WareInfo.Price(Ware.Car));
            Assert.AreEqual(4, WareInfo.Price(Ware.Blocks));
            Assert.AreEqual(6, WareInfo.BlocksInSet);
            Assert.AreEqual(3, Order.Of(Ware.Apple, Ware.Banana, Ware.Banana).Sum, "fruit: 1 per item");
            Assert.AreEqual(5, Order.Of(Ware.Polkagris, Ware.CandyBag).Sum, "candy: 2 + 3");
            Assert.AreEqual(4, Order.Of(Ware.Blocks).Sum, "a set of blocks");
        }

        [Test]
        public void Orders_OneCategoryEach()
        {
            Assert.Throws<System.ArgumentException>(() => Order.Of(Ware.Apple, Ware.Lollipop), "never mixed");
            var gen = new OrderGenerator(5);
            for (var n = 1; n <= 300; n++)
            {
                var o = gen.Next(n, _ => true);
                foreach (var w in WareInfo.All)
                    if (o.Count(w) > 0) Assert.AreEqual(o.Category, WareInfo.CategoryOf(w), $"customer {n}: {o}");
                if (o.Category == Category.Toys) Assert.AreEqual(1, o.Total, "one toy per child");
                if (o.Category == Category.Candy) Assert.LessOrEqual(o.Total, 2);
            }
        }

        [Test]
        public void Orders_CategoriesTakeTurns()
        {
            var gen = new OrderGenerator(1);
            Assert.AreEqual(Order.Of(Ware.Apple), gen.Next(1, _ => true));
            var seen = new List<Category>();
            for (var n = 2; n <= 7; n++) seen.Add(gen.Next(n, _ => true).Category);
            CollectionAssert.AreEqual(new[] { Category.FruitVeg, Category.Candy, Category.Toys, Category.FruitVeg, Category.Candy, Category.Toys }, seen);
        }

        [Test]
        public void Orders_NoToyWhenSpotTaken()
        {
            var gen = new OrderGenerator(3);
            for (var n = 1; n <= 200; n++)
            {
                var o = gen.Next(n, t => t != Ware.Ball);
                Assert.AreNotEqual(Ware.Ball, o.Toy, "the ball's spot is taken");
            }
        }

        [Test]
        public void Orders_NoToyWhenAreaFull()
        {
            var gen = new OrderGenerator(3);
            for (var n = 1; n <= 200; n++) Assert.AreNotEqual(Category.Toys, gen.Next(n, _ => false).Category, "full play area: other sales go on");
        }

        [Test]
        public void Basket_RejectsOtherCategory()
        {
            var shop = new Shop();
            shop.Tick(Shop.ArriveSeconds);
            Assert.AreEqual(Category.FruitVeg, shop.Category);
            Assert.AreEqual(-1, shop.TryAddToBasket(Ware.Lollipop), "candy is not taken at the fruit stall");
            Assert.AreEqual(-1, shop.TryAddToBasket(Ware.Ball));
            Assert.AreEqual(0, shop.Basket.Total);
        }

        [Test]
        public void Sale_PaymentCreditsOrderSumOnce()
        {
            var m = new Market();
            var total = 0;
            for (var k = 0; k < 9; k++)
            {
                var guard = 0;
                while (m.Shop.State != SaleState.Ordering && guard++ < 200) m.Shop.Tick(0.5f);
                var order = m.Shop.Order;
                foreach (var w in WareInfo.All)
                    for (var i = 0; i < order.Count(w); i++) m.Shop.TryAddToBasket(w);
                var before = m.Wallet.Balance;
                Assert.IsTrue(m.Shop.TryPay());
                Assert.IsFalse(m.Shop.TryPay(), "same frame");
                m.Shop.Tick(0.1f);
                Assert.IsFalse(m.Shop.TryPay(), "a frame later");
                Assert.AreEqual(before + order.Sum, m.Wallet.Balance, $"the till grows by the order's sum ({order})");
                total += order.Sum;
                m.Shop.Tick(Shop.ThankSeconds);
                m.Shop.Tick(Shop.LeaveSeconds);
                if (order.Toy is Ware t) for (var i = 0; i < 100; i++) m.PlayArea.Tick(1); // let the child finish
            }
            Assert.AreEqual(total, m.Wallet.Balance, "one shared till for every category");
            Assert.AreEqual(9, m.Shop.CompletedSales);
        }

        [Test]
        public void Shop_ClosedKeepsCurrentOrderAndStopsNewCustomers()
        {
            var m = new Market();
            m.Shop.Tick(Shop.ArriveSeconds);
            var order = m.Shop.Order;
            m.Shop.SetOpen(false);
            m.Tick(60);
            Assert.AreEqual(SaleState.Ordering, m.Shop.State, "the customer who is here stays, no timer");
            Assert.AreEqual(order, m.Shop.Order);
            m.Shop.TryAddToBasket(Ware.Apple);
            Assert.IsTrue(m.Shop.TryPay(), "the order can still be finished");
            m.Tick(Shop.ThankSeconds);
            m.Tick(Shop.LeaveSeconds);
            Assert.AreEqual(SaleState.Closed, m.Shop.State, "no new customer while closed");
            var customers = m.Shop.CustomerNumber;
            m.Tick(600);
            Assert.AreEqual(customers, m.Shop.CustomerNumber);
            m.Shop.SetOpen(true);
            Assert.AreEqual(customers + 1, m.Shop.CustomerNumber, "opening brings the next customer");
        }

        [Test]
        public void Shop_ClearBasketKeepsOrderAndMoney()
        {
            var m = new Market();
            Sell(m);
            while (m.Shop.State != SaleState.Ordering) m.Shop.Tick(0.5f);
            var order = m.Shop.Order;
            var till = m.Wallet.Balance;
            foreach (var w in WareInfo.All)
                for (var i = 0; i < order.Count(w); i++) m.Shop.TryAddToBasket(w);
            Assert.AreEqual(SaleState.AwaitingPayment, m.Shop.State);
            Assert.IsTrue(m.Shop.ClearBasket());
            Assert.AreEqual(SaleState.Ordering, m.Shop.State);
            Assert.IsTrue(m.Shop.Basket.IsEmpty);
            Assert.AreEqual(order, m.Shop.Order, "same customer, same order");
            Assert.AreEqual(till, m.Wallet.Balance, "the till is untouched");
        }

        [Test]
        public void Market_NewGameResetsEverything()
        {
            var m = new Market();
            for (var k = 0; k < 4; k++) Sell(m);
            Assert.Greater(m.Wallet.Balance, 0);
            m.Shop.SetOpen(false);
            m.NewGame();
            Assert.AreEqual(0, m.Wallet.Balance);
            Assert.AreEqual(0, m.Shop.CompletedSales);
            Assert.IsTrue(m.Shop.IsOpen);
            Assert.AreEqual(Order.Of(m.Assortment.FirstFruit), m.Shop.Order, "EPIC-009: the new fruit's first");
            foreach (var t in WareInfo.Toys) Assert.AreEqual(SpotState.Free, m.PlayArea.State(t), "the play area is empty");
        }

        // ---- Play area ----

        [Test]
        public void PlayArea_ReservesBeforeToyOrder()
        {
            var m = new Market();
            Sell(m); // 1 apple
            Sell(m); // fruit
            Sell(m); // candy
            while (m.Shop.State != SaleState.Ordering) m.Shop.Tick(0.5f);
            Assert.AreEqual(Category.Toys, m.Shop.Category, "customer 4 is a child who wants a toy");
            Assert.IsTrue(m.Shop.CustomerIsChild);
            var toy = m.Shop.Order.Toy.Value;
            Assert.AreEqual(SpotState.Reserved, m.PlayArea.State(toy), "the spot was reserved before the order was shown");
            Assert.IsFalse(m.PlayArea.TryReserve(toy, 99), "and nobody else can take it");
        }

        [Test]
        public void PlayArea_ToyStates()
        {
            var a = new PlayArea();
            Assert.IsTrue(a.TryReserve(Ware.Ball, 4));
            Assert.AreEqual(ToyControl.None, a.Control(Ware.Ball));
            Assert.IsTrue(a.Sold(Ware.Ball));
            Assert.IsFalse(a.Sold(Ware.Ball), "sold once");
            Assert.AreEqual(SpotState.Walking, a.State(Ware.Ball));
            a.Tick(PlayArea.WalkSeconds);
            Assert.AreEqual(SpotState.Playing, a.State(Ware.Ball));
            Assert.AreEqual(ToyControl.Child, a.Control(Ware.Ball), "the child plays");
            Assert.IsTrue(a.ChildMayAct(Ware.Ball));
            a.Tick(PlayArea.PlaySeconds);
            Assert.AreEqual(SpotState.Leaving, a.State(Ware.Ball), "a calm goodbye after a while");
            a.Tick(PlayArea.LeaveSeconds);
            Assert.AreEqual(SpotState.Free, a.State(Ware.Ball));
            Assert.AreEqual(0, a.Children);
        }

        [Test]
        public void PlayArea_PlayerAndChildNeverControlTogether()
        {
            var a = new PlayArea();
            a.TryReserve(Ware.Car, 1);
            a.Sold(Ware.Car);
            a.Tick(PlayArea.WalkSeconds);
            a.PlayerTook(Ware.Car);
            Assert.AreEqual(ToyControl.Player, a.Control(Ware.Car));
            Assert.IsFalse(a.ChildMayAct(Ware.Car), "the child waits while the player holds it");
            a.ToyAtRest(Ware.Car);
            Assert.AreEqual(ToyControl.Player, a.Control(Ware.Car), "a rest report can't take it from the hand");
            a.PlayerReleased(Ware.Car);
            Assert.AreEqual(ToyControl.Free, a.Control(Ware.Car), "rolling freely after the release");
            Assert.IsFalse(a.ChildMayAct(Ware.Car));
            a.ToyAtRest(Ware.Car);
            Assert.AreEqual(ToyControl.Child, a.Control(Ware.Car), "lying still: the child carries on");
            Assert.IsTrue(a.ChildMayAct(Ware.Car));
        }

        [Test]
        public void PlayArea_MaxThreeChildren()
        {
            var a = new PlayArea();
            foreach (var t in Assortment.ClassicOf(Category.Toys)) // one per zone
            {
                Assert.IsTrue(a.TryReserve(t, 1));
                a.Sold(t);
            }
            a.Tick(PlayArea.WalkSeconds);
            Assert.AreEqual(3, a.Children);
            foreach (var t in WareInfo.Toys) Assert.IsFalse(a.TryReserve(t, 2), "one child per spot");
            Assert.IsFalse(a.TryReserve(Ware.Apple, 2), "only toys have spots");
        }

        [Test]
        public void PlayArea_CalmFinishNotWhilePlayerPlays()
        {
            var a = new PlayArea();
            a.TryReserve(Ware.Ball, 1);
            a.Sold(Ware.Ball);
            a.Tick(PlayArea.WalkSeconds);
            a.Tick(PlayArea.PlaySeconds - 1);
            a.PlayerTook(Ware.Ball);
            a.Tick(30);
            Assert.AreEqual(SpotState.Playing, a.State(Ware.Ball), "never ends while the player holds the toy");
            a.PlayerReleased(Ware.Ball);
            a.Tick(PlayArea.LingerSeconds - 1);
            Assert.AreEqual(SpotState.Playing, a.State(Ware.Ball), "nor right after the player played with it");
            a.Tick(2);
            Assert.AreEqual(SpotState.Leaving, a.State(Ware.Ball));
            a.PlayerTook(Ware.Ball);
            a.Tick(10);
            Assert.AreEqual(SpotState.Leaving, a.State(Ware.Ball), "a leaving child waits rather than taking it from the hand");
            a.PlayerReleased(Ware.Ball);
            a.Tick(PlayArea.LeaveSeconds);
            Assert.AreEqual(SpotState.Free, a.State(Ware.Ball));
        }

        // ---- Stacking help (EPIC-003; EPIC-011: BlockPlacement replaced StackHelp) ----

        static readonly Vector2 TableHalf = new(0.5f, 0.25f);

        [Test]
        public void Stack_SnapsOnTopWhenClose()
        {
            const float size = 0.06f;
            var below = new Vector3(0.1f, size / 2, 0.2f);
            var r = BlockPlacement.Resolve(new Vector3(0.12f, size * 1.5f + 0.05f, 0.21f), new[] { below }, size, TableHalf, Vector3.zero);
            Assert.AreEqual(BlockPlacement.Kind.OnTop, r.Kind);
            Assert.AreEqual(0, r.Below, "lined up with the block below (the view takes its yaw)");
            Assert.Less(Vector3.Distance(r.Position, new Vector3(0.1f, size * 1.5f, 0.2f)), 1e-4f, "straight on top");
        }

        [Test]
        public void Stack_NoSnapWhenFar()
        {
            const float size = 0.06f;
            var below = new Vector3(0, size / 2, 0);
            BlockPlacement.Kind KindAt(Vector3 p) => BlockPlacement.Resolve(p, new[] { below }, size, TableHalf, Vector3.zero).Kind;
            Assert.AreEqual(BlockPlacement.Kind.AsReleased, KindAt(new Vector3(0.06f, size * 1.5f, 0)), "too far sideways");
            Assert.AreEqual(BlockPlacement.Kind.AsReleased, KindAt(new Vector3(0, size * 1.5f + 0.15f, 0)), "too high above");
            // EPIC-011: released *inside* it used to get no help (and the physics engine shot it away): now on top.
            Assert.AreEqual(BlockPlacement.Kind.OnTop, KindAt(new Vector3(0, size / 2, 0.01f)), "inside it");
        }

        // ---- Layout: the compact market (SPEC §14, EPIC-003 S4K) ----

        static bool Inside(Vector3 p, Vector3 centre, float yaw, Vector3 size, float margin)
        {
            var q = Quaternion.Euler(0, -yaw, 0) * (p - centre);
            return Mathf.Abs(q.x) <= size.x / 2 + margin && Mathf.Abs(q.z) <= size.z / 2 + margin;
        }

        static IEnumerable<(Vector3 c, float yaw, Vector3 size, string name)> Furniture()
        {
            foreach (var c in WareInfo.Categories)
            {
                var (p, y) = MarketLayout.Stall(c);
                yield return (p, y, StandView.CounterSize, c.ToString());
            }
            var (t, ty) = MarketLayout.PlayTable;
            yield return (t, ty, PlayTableView.TableSize, "play table");
        }

        static IEnumerable<Vector3> Corners(Vector3 c, float yaw, Vector3 size)
        {
            for (var i = 0; i <= 10; i++)
                for (var j = 0; j <= 10; j++)
                    yield return c + Quaternion.Euler(0, yaw, 0) * new Vector3(size.x * (i / 10f - 0.5f), 0, size.z * (j / 10f - 0.5f));
        }

        [Test]
        public void Layout_CompactUAroundWorkArea()
        {
            var all = new List<(Vector3 c, float yaw, Vector3 size, string name)>(Furniture());
            foreach (var f in all)
            {
                foreach (var p in Corners(f.c, f.yaw, f.size))
                    Assert.IsFalse(MarketLayout.InWorkArea(p, -0.001f), $"the {f.name} reaches into the 1.1 x 1.1 m work area at {p}");
                var towardsMiddle = Quaternion.Euler(0, f.yaw, 0) * Vector3.back;
                Assert.Greater(Vector3.Dot(towardsMiddle, -f.c.normalized), 0.99f, $"the {f.name} faces the work area");
            }
            for (var i = 0; i < all.Count; i++)
                for (var j = i + 1; j < all.Count; j++)
                    foreach (var p in Corners(all[i].c, all[i].yaw, all[i].size))
                        Assert.IsFalse(Inside(p, all[j].c, all[j].yaw, all[j].size, 0.02f), $"{all[i].name} overlaps {all[j].name}");
            Assert.Greater(MarketLayout.Stall(Category.FruitVeg).position.z, 0, "fruit & veg in front");
            Assert.Greater(MarketLayout.Stall(Category.Candy).position.x, 0, "candy to the right");
            Assert.Less(MarketLayout.Stall(Category.Toys).position.x, 0, "toys to the left");
            Assert.Less(MarketLayout.PlayTable.position.z, 0, "the play table behind");
        }

        [Test]
        public void Layout_EverythingReachableFromInsideTheArea()
        {
            const float reach = 0.20f; // beyond where the body may be (0.25 m inside the physical area)
            var needed = new List<(Vector3 p, string what)>();
            foreach (var c in WareInfo.Categories)
            {
                var (o, y) = MarketLayout.Stall(c);
                for (var bin = 0; bin < 3; bin++) needed.Add((MarketLayout.Local(o, y, StandView.CrateTop(bin, WareInfo.Catalogue(c)[bin])), $"{c} bin {bin}"));
                needed.Add((MarketLayout.Local(o, y, StandView.BasketCentre), $"{c} basket"));
                needed.Add((MarketLayout.Local(o, y, StandView.CoinSpot), $"{c} token"));
                needed.Add((MarketLayout.Local(o, y, StandView.RegisterCentre), $"{c} register"));
                needed.Add((MarketLayout.Local(o, y, StandView.OpenSignPosition), $"{c} Öppet/Stängt"));
                foreach (var s in StandView.Slots) needed.Add((MarketLayout.Local(o, y, s), $"{c} basket slot"));
            }
            var (t, ty) = MarketLayout.PlayTable;
            foreach (var p in new[] { PlayTableView.BallTarget, PlayTableView.RoadPoint(0.75f), PlayTableView.RoadCentre, PlayTableView.TidyButton })
                needed.Add((MarketLayout.Local(t, ty, p), $"play table {p}"));
            for (var i = 0; i < WareInfo.BlocksInSet; i++) needed.Add((MarketLayout.Local(t, ty, PlayTableView.BlockPile(i)), $"block {i}"));
            foreach (var (p, what) in needed)
                Assert.LessOrEqual(MarketLayout.OutsideBodyRegion(p), reach, $"{what} at {p} is out of reach from inside the play space");
            Assert.AreEqual(0.5f, MarketLayout.BodyHalf.x, 1e-4, "body at most 0.50 m sideways (0.25 m margin)");
            Assert.AreEqual(0.75f, MarketLayout.BodyHalf.y, 1e-4);
            foreach (var c in WareInfo.Categories) Assert.AreEqual(0, MarketLayout.OutsideBodyRegion(MarketLayout.StandingSpot(c)), "standing spots inside");
            Assert.AreEqual(0, MarketLayout.OutsideBodyRegion(MarketLayout.PlayStandingSpot));
        }

        [Test]
        public void Layout_ShallowCountersFullSizeObjects()
        {
            Assert.AreEqual(0.30f, StandView.CounterSize.z, 1e-4, "shallow counters");
            foreach (var p in new[] { StandView.BasketCentre, StandView.CoinSpot, StandView.RegisterCentre })
                Assert.LessOrEqual(p.z - StandView.InnerEdge, 0.16f, "goods and payment close to the inner edge");
            foreach (var w in WareInfo.All) Assert.LessOrEqual(StandView.CrateTop(0, w).z - StandView.InnerEdge, 0.16f);
            Assert.AreEqual(0.72f, CustomerView.ChildScale, 1e-4, "children keep their size");
            Assert.AreEqual(0.045f, Models.BallRadius, 1e-4, "toys keep their size");
            Assert.AreEqual(0.06f, Models.BlockSize, 1e-4);
        }

        [Test]
        public void Layout_PeopleAndPathsOutsideWorkArea()
        {
            foreach (var c in WareInfo.Categories)
            {
                var (o, y) = MarketLayout.Stall(c);
                var customer = MarketLayout.Local(o, y, CustomerView.Spot);
                Assert.IsFalse(MarketLayout.InWorkArea(customer, 0.3f), $"{c} customer stands outside the counter");
                Assert.IsFalse(Inside(customer, o, y, StandView.CounterSize, 0.05f), $"{c} customer is not in the counter");
            }
            var (t, ty) = MarketLayout.PlayTable;
            var (toyStall, toyYaw) = MarketLayout.Stall(Category.Toys);
            var start = MarketLayout.Local(toyStall, toyYaw, CustomerView.Spot);
            var from = Quaternion.Euler(0, -ty, 0) * (start - t);
            foreach (var toy in WareInfo.Toys)
            {
                var spot = PlayTableView.ChildSpot(toy);
                var child = MarketLayout.Local(t, ty, spot);
                Assert.IsFalse(MarketLayout.InWorkArea(child, 0.3f), $"the {toy} child sits across the table");
                for (var k = 0f; k <= 1f; k += 0.02f)
                {
                    var p = MarketLayout.Local(t, ty, PlayTableView.WalkPoint(from, spot, k));
                    Assert.IsFalse(MarketLayout.InWorkArea(p, 0.1f), $"the {toy} child's path crosses the work area at {p}");
                    foreach (var f in Furniture()) Assert.IsFalse(Inside(p, f.c, f.yaw, f.size, 0.05f) && k < 0.98f, $"the {toy} child walks through the {f.name} at {p}");
                }
            }
        }
    }
}
