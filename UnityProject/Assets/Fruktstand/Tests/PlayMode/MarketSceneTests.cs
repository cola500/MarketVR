// EPIC-003 in the real scene, driven through FruitStand.Step with simulated hands (the VR path) and the desktop
// shortcuts. Physics is stepped by Step (script mode), so play is deterministic. Names match docs/EPIC-003.md.
using System.Collections;
using System.Linq;
using Fruktstand.Core;
using Fruktstand.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Fruktstand.Tests
{
    public class MarketSceneTests
    {
        FruitStand s;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneLoad.Fresh(x => s = x); // synchronous load, classic fruit (see SceneLoad)
            s = Object.FindAnyObjectByType<FruitStand>();
            Assert.IsNotNull(s);
            s.ReadDesktopInput = false;
            yield return null;
        }

        void Step(Vector3? right = null, bool grab = false, float dt = 0.02f, bool releaseAll = false)
        {
            var f = new ShopFrame { DeltaTime = dt, ReleaseAll = releaseAll };
            if (right.HasValue) f.Right = new HandFrame { Tracked = true, Position = right.Value, Rotation = Quaternion.identity, Grab = grab };
            s.Step(f);
        }

        void Frame(ShopFrame f)
        {
            f.DeltaTime = 0.02f;
            s.Step(f);
        }

        void Wait(float seconds)
        {
            for (var t = 0f; t < seconds; t += 0.05f) Step(dt: 0.05f);
        }

        void WaitForOrdering()
        {
            var guard = 0;
            while (s.Shop.State != SaleState.Ordering && guard++ < 600) Step(dt: 0.1f);
            Assert.AreEqual(SaleState.Ordering, s.Shop.State);
        }

        // Serves the current customer with the desktop shortcuts (same rules as the hands).
        Order Serve()
        {
            WaitForOrdering();
            var order = s.Shop.Order;
            foreach (var w in WareInfo.All)
                for (var i = 0; i < order.Count(w); i++) Frame(new ShopFrame { AddDirect = w });
            Wait(0.6f);
            Assert.IsTrue(s.RegisterCoin(), $"paid for {order}");
            return order;
        }

        void ServeUntil(Category c)
        {
            for (var k = 0; k < 10; k++)
            {
                WaitForOrdering();
                if (s.Shop.Category == c) return;
                Serve();
                Wait(Shop.ThankSeconds + Shop.LeaveSeconds);
            }
            Assert.Fail($"no {c} customer came");
        }

        // Only this toy's play spot is left free, so the next toy order is for it.
        void OnlyToyFree(Ware toy)
        {
            // the other zones are taken (EPIC-010: one zone per toy kind; the classic toys are one per zone)
            foreach (var t in Assortment.ClassicOf(Category.Toys)) if (PlayArea.SpotOf(t) != PlayArea.SpotOf(toy)) Assert.IsTrue(s.PlayArea.TryReserve(t, 99));
        }

        Item PlayToy(Ware toy) => s.Play.Toys(toy).FirstOrDefault();
        Vector3 TableLocal(Item toy) => s.Play.ToTable(toy.Position);

        void PickAndDrop(Item item, Vector3 to, float settle = 0.5f)
        {
            var from = item.Position;
            Step(from);
            Assert.AreSame(item, s.Hover(1), $"hand next to {item.Transform.name} highlights it");
            Step(from, true);
            Assert.AreSame(item, s.Held(1));
            Step(Vector3.Lerp(from, to, 0.5f), true);
            Step(to, true);
            Step(to, false);
            Wait(settle);
        }

        void Press(Item button)
        {
            Step(button.Position);
            Step(button.Position, true);
            Step(button.Position, false);
        }

        // A toy sale up to the child playing at the table.
        void SellToyAndWaitForPlay(Ware toy)
        {
            OnlyToyFree(toy);
            ServeUntil(Category.Toys);
            Assert.AreEqual(toy, s.Shop.Order.Toy);
            Serve();
            Wait(PlayArea.WalkSeconds + 0.2f);
            Assert.AreEqual(SpotState.Playing, s.PlayArea.State(toy));
        }

        [UnityTest]
        public IEnumerator Scene_CustomerAtStallOfCategory()
        {
            foreach (var c in new[] { Category.FruitVeg, Category.Candy, Category.Toys })
            {
                ServeUntil(c);
                Step();
                Assert.AreSame(s.Stall(c).Root, s.Customer.Stall, $"the {c} customer stands at the {c} stall");
                Assert.AreSame(s.Stall(c).Top, s.Bubble.Root.parent, "and the bubble is there too");
                Assert.AreEqual(s.Shop.Order.Sum, s.Bubble.ShownSum, "the bubble shows the order's sum");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_TokenShowsOrderSum()
        {
            ServeUntil(Category.Candy);
            var order = s.Shop.Order;
            foreach (var w in WareInfo.All)
                for (var i = 0; i < order.Count(w); i++) Frame(new ShopFrame { AddDirect = w });
            Wait(0.6f);
            Assert.AreEqual(SaleState.AwaitingPayment, s.Shop.State);
            Assert.AreEqual(order.Sum.ToString(), s.TokenShown, "the payment token is worth the order's sum");
            Assert.AreSame(s.Stall(Category.Candy).Top, s.Coin.Transform.parent, "laid on the candy counter");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_CandySaleSameLoop()
        {
            ServeUntil(Category.Candy);
            Assert.AreEqual(Mood.Sad, s.Customer.Mood, "candy customers are sad while they wait (The child's wish)");
            var order = s.Shop.Order;
            var stall = s.Stall(Category.Candy);
            var before = s.Wallet.Balance;
            foreach (var w in WareInfo.All)
                for (var i = 0; i < order.Count(w); i++) PickAndDrop(s.CrateTop(w), stall.ToWorld(StandView.BasketCentre + Vector3.up * 0.12f), 0.2f);
            Assert.AreEqual(SaleState.AwaitingPayment, s.Shop.State, "picked by hand into the candy basket");
            Wait(0.6f);
            PickAndDrop(s.Coin, stall.ToWorld(StandView.RegisterCentre + Vector3.up * 0.14f), 0.1f);
            Assert.AreEqual(before + order.Sum, s.Wallet.Balance, "the till grows by the sum");
            Assert.AreEqual(Mood.Happy, s.Customer.Mood, "and the customer is happy");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_KassaShownAtEveryStall()
        {
            Serve();
            Wait(Shop.ThankSeconds + Shop.LeaveSeconds);
            Serve();
            Step();
            foreach (var c in WareInfo.Categories)
                Assert.AreEqual(s.Wallet.Balance.ToString(), s.Stall(c).WalletShown, $"the same till at the {c} stall");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_WrongCategoryGoesHome()
        {
            WaitForOrdering();
            Assert.AreEqual(Category.FruitVeg, s.Shop.Category);
            var lollipop = s.CrateTop(Ware.Lollipop);
            PickAndDrop(lollipop, s.Stand.ToWorld(StandView.BasketCentre + Vector3.up * 0.12f));
            Assert.AreEqual(ItemPlace.Gone, lollipop.Place, "candy is not taken at the fruit stall; it goes back to its bin");
            Assert.IsTrue(s.Shop.Basket.IsEmpty);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_ToyOrderComesFromChild()
        {
            ServeUntil(Category.Toys);
            Step();
            Assert.IsTrue(s.Customer.IsChild, "toy customers are children");
            Assert.AreEqual(SpotState.Reserved, s.PlayArea.State(s.Shop.Order.Toy.Value), "their spot is held already");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_SellBallToChild()
        {
            OnlyToyFree(Ware.Ball);
            ServeUntil(Category.Toys);
            Assert.AreEqual(Order.Of(Ware.Ball), s.Shop.Order);
            var stall = s.Stall(Category.Toys);
            var before = s.Wallet.Balance;
            PickAndDrop(s.CrateTop(Ware.Ball), stall.ToWorld(StandView.BasketCentre + Vector3.up * 0.12f), 0.6f);
            Assert.AreEqual("2", s.TokenShown);
            PickAndDrop(s.Coin, stall.ToWorld(StandView.RegisterCentre + Vector3.up * 0.14f), 0.05f);
            Assert.AreEqual(before + 2, s.Wallet.Balance, "a ball is 2");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_ChildCarriesSameBallToPlaySpot()
        {
            OnlyToyFree(Ware.Ball);
            ServeUntil(Category.Toys);
            Frame(new ShopFrame { AddDirect = Ware.Ball });
            var inBasket = s.BasketItem(0);
            Assert.IsNotNull(inBasket);
            var ballTransform = inBasket.Transform;
            Wait(0.6f);
            s.RegisterCoin();
            Assert.AreSame(ballTransform, s.Play.CarriedToy(Ware.Ball).Transform, "the child carries the very ball from the basket");
            Assert.AreEqual(ItemPlace.Carried, s.Play.CarriedToy(Ware.Ball).Place);
            var start = s.Play.ChildWorld(Ware.Ball);
            Wait(PlayArea.WalkSeconds * 0.5f);
            var spot = s.Play.ToWorld(Vector3.zero);
            Assert.Less(Vector3.Distance(s.Play.ChildWorld(Ware.Ball), spot), Vector3.Distance(start, spot), "walking to the play area");
            Wait(PlayArea.WalkSeconds * 0.5f + 0.2f);
            var ball = PlayToy(Ware.Ball);
            Assert.IsNotNull(ball);
            Assert.AreSame(ballTransform, ball.Transform, "and plays with that same ball");
            Assert.IsNotNull(ball.Body, "now a real rolling ball");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_ChildRollsBall()
        {
            SellToyAndWaitForPlay(Ware.Ball);
            var ball = PlayToy(Ware.Ball);
            var start = TableLocal(ball);
            var rolled = false;
            for (var t = 0f; t < 5 && !rolled; t += 0.05f)
            {
                Step(dt: 0.05f);
                rolled = ball.Body.linearVelocity.magnitude > 0.2f;
            }
            Assert.IsTrue(rolled, "the child rolls the ball");
            Wait(3);
            Assert.Less(TableLocal(ball).z, start.z - 0.15f, "over the table towards the player's side");
            Assert.Greater(TableLocal(ball).y, -0.02f, "and it stays on the table (rim)");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_SellingContinuesWhileChildPlays()
        {
            SellToyAndWaitForPlay(Ware.Ball);
            WaitForOrdering();
            Assert.AreNotEqual(Category.Toys, s.Shop.Category, "the next customer is there, wanting something else");
            Assert.AreEqual(SpotState.Playing, s.PlayArea.State(Ware.Ball));
            yield return null;
        }

        // Hand positions at the real places: in front of each stall and at the play table (world space).
        Vector3 AtStall(Category c, Vector3 counterPoint) => s.Stall(c).ToWorld(counterPoint);

        [UnityTest]
        public IEnumerator Scene_NoTeleportOrStationButtons()
        {
            Step();
            foreach (var i in s.Items)
                Assert.IsFalse(i.IsButton && i != s.Play.TidyButtonItem && s.Crocodile.ToothOf(i) < 0, $"no station or teleport button ({i.Transform.name})"); // the crocodile's teeth are toys (EPIC-006)
            Assert.IsNull(GameObject.Find("Spot Buttons"), "no button strip");
            Assert.IsNull(GameObject.Find("Spot Ring"), "no ground markings");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_MarketStaysPutWhilePlayerMoves()
        {
            var root = s.MarketRoot;
            var pose = (root.position, root.rotation);
            var placements = s.Placements;
            // The hands wander all over the work area and to every stall and the table, for a while.
            var points = new[]
            {
                AtStall(Category.FruitVeg, StandView.BasketCentre + Vector3.up * 0.2f), AtStall(Category.Candy, StandView.CoinSpot + Vector3.up * 0.2f),
                AtStall(Category.Toys, StandView.RegisterCentre + Vector3.up * 0.2f), s.Play.ToWorld(PlayTableView.BallTarget + Vector3.up * 0.2f),
                s.ToWorld(new Vector3(0.4f, 1f, 0.6f)), s.ToWorld(new Vector3(-0.4f, 1f, -0.6f)),
            };
            for (var k = 0; k < 120; k++) Step(points[k % points.Length], dt: 0.05f);
            Assert.AreEqual(pose.position, root.position, "the market does not follow the player");
            Assert.AreEqual(pose.rotation, root.rotation);
            Assert.AreEqual(placements, s.Placements, "and is not placed again by itself");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_ChildWalksAroundTheWorkArea()
        {
            OnlyToyFree(Ware.Blocks);
            ServeUntil(Category.Toys);
            Serve();
            for (var t = 0f; t < PlayArea.WalkSeconds; t += 0.05f)
            {
                Step(dt: 0.05f);
                var child = s.ToMarket(s.Play.ChildWorld(Ware.Blocks));
                Assert.IsFalse(MarketLayout.InWorkArea(child, 0.1f), $"the child walks round the outside, not through the work area ({child})");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_OrderSurvivesWalkingAround()
        {
            SellToyAndWaitForPlay(Ware.Ball);
            WaitForOrdering();
            var order = s.Shop.Order;
            var c = order.Category;
            Frame(new ShopFrame { AddDirect = s.Market.Assortment.Active(c).First(w => order.Count(w) > 0) });
            var basket = s.Shop.Basket.Total;
            var till = s.Wallet.Balance;
            // Turn round to the play table, roll the ball back, turn to the other stalls, come back.
            var ball = PlayToy(Ware.Ball);
            Wait(4);
            PickAndDrop(ball, ball.Position + (s.Play.ToWorld(PlayTableView.BallHome) - s.Play.ToWorld(PlayTableView.BallTarget)) * 0.5f, 0.1f);
            foreach (var other in WareInfo.Categories) Step(AtStall(other, StandView.BasketCentre + Vector3.up * 0.2f), dt: 0.5f);
            Assert.AreEqual(order, s.Shop.Order, "the same customer still waits");
            Assert.AreEqual(basket, s.Shop.Basket.Total, "the basket is as it was");
            Assert.AreEqual(till, s.Wallet.Balance, "the till too");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_FirstMilestone()
        {
            // Sell fruit -> sell a ball to a child -> see the child play -> turn round and roll it back -> sell again.
            // All with the hands at the real places; nothing to select or teleport.
            OnlyToyFree(Ware.Ball);
            WaitForOrdering();
            var fruit = s.Stall(Category.FruitVeg);
            PickAndDrop(s.CrateTop(Ware.Apple), fruit.ToWorld(StandView.BasketCentre + Vector3.up * 0.12f), 0.6f);
            PickAndDrop(s.Coin, fruit.ToWorld(StandView.RegisterCentre + Vector3.up * 0.14f), 0.1f);
            Assert.AreEqual(1, s.Wallet.Balance);
            Wait(Shop.ThankSeconds + Shop.LeaveSeconds);
            ServeUntil(Category.Toys);
            var toys = s.Stall(Category.Toys);
            var till = s.Wallet.Balance;
            PickAndDrop(s.CrateTop(Ware.Ball), toys.ToWorld(StandView.BasketCentre + Vector3.up * 0.12f), 0.6f);
            PickAndDrop(s.Coin, toys.ToWorld(StandView.RegisterCentre + Vector3.up * 0.14f), 0.1f);
            Assert.AreEqual(till + 2, s.Wallet.Balance, "the ball's 2 coins in the same till");
            Wait(PlayArea.WalkSeconds + 3);
            Assert.AreEqual(SpotState.Playing, s.PlayArea.State(Ware.Ball));
            var ball = PlayToy(Ware.Ball);
            Wait(3);
            PickAndDrop(ball, ball.Position + s.Play.ToWorld(PlayTableView.BallHome) - s.Play.ToWorld(PlayTableView.BallTarget), 0.1f);
            Assert.AreEqual(SpotState.Playing, s.PlayArea.State(Ware.Ball), "playing together");
            var before = s.Wallet.Balance;
            var next = Serve();
            Assert.AreEqual(before + next.Sum, s.Wallet.Balance, "turned back to the stalls, selling again");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_PlayerRollsBallBackChildContinues()
        {
            SellToyAndWaitForPlay(Ware.Ball);
            Wait(5); // the child rolls it to the player's side
            var ball = PlayToy(Ware.Ball);
            Assert.Less(TableLocal(ball).z, 0, "the ball came to the player");
            // Take it: the child waits.
            Step(ball.Position);
            Step(ball.Position, true);
            Assert.AreSame(ball, s.Held(1));
            Assert.AreEqual(ToyControl.Player, s.PlayArea.Control(Ware.Ball));
            Assert.IsFalse(s.PlayArea.ChildMayAct(Ware.Ball));
            // Roll it back: move the hand towards the child and let go while moving.
            var p = ball.Position;
            var towardChild = s.Play.ToWorld(PlayTableView.BallHome) - s.Play.ToWorld(PlayTableView.BallTarget);
            towardChild.y = 0;
            towardChild.Normalize();
            for (var i = 0; i < 4; i++)
            {
                p += towardChild * 0.012f;
                Step(p, true);
            }
            Step(p, false);
            Assert.AreEqual(ToyControl.Free, s.PlayArea.Control(Ware.Ball), "rolling freely");
            Assert.Greater(ball.Body.linearVelocity.magnitude, 0.2f, "it rolls");
            var gotBack = false;
            for (var t = 0f; t < 8 && !gotBack; t += 0.05f)
            {
                Step(dt: 0.05f);
                gotBack = s.PlayArea.Control(Ware.Ball) == ToyControl.Child;
            }
            Assert.IsTrue(gotBack, "lying still: the child carries on");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_ChildDrivesCarAlongRoad()
        {
            SellToyAndWaitForPlay(Ware.Car);
            var car = PlayToy(Ware.Car);
            var a = TableLocal(car);
            Wait(2);
            var b = TableLocal(car);
            Assert.Greater(Vector3.Distance(a, b), 0.1f, "the child drives the car");
            var t = PlayTableView.NearestRoadT(b);
            Assert.Less(Vector3.Distance(new Vector3(b.x, 0, b.z), new Vector3(PlayTableView.RoadPoint(t).x, 0, PlayTableView.RoadPoint(t).z)), 0.03f, "along the road");
            Assert.Less(Vector3.Angle(car.Transform.up, s.Play.Top.up), 5f, "upright");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_PlayerPushesCarChildResumes()
        {
            SellToyAndWaitForPlay(Ware.Car);
            var car = PlayToy(Ware.Car);
            Step(car.Position);
            Step(car.Position, true);
            Assert.AreSame(car, s.Held(1));
            // Push it sideways and forwards at once: it may only roll along its length.
            var p = car.Position;
            var push = (car.Transform.forward + car.Transform.right).normalized;
            for (var i = 0; i < 4; i++)
            {
                p += push * 0.01f;
                Step(p, true);
            }
            Step(p, false);
            Step(dt: 0.03f);
            var v = car.Body.linearVelocity;
            Assert.Greater(v.magnitude, 0.05f, "pushed");
            Assert.Less(Mathf.Abs(Vector3.Dot(v, car.Transform.right)), 0.02f, "no sliding sideways");
            var resumed = false;
            for (var t = 0f; t < 6 && !resumed; t += 0.05f)
            {
                Step(dt: 0.05f);
                resumed = s.PlayArea.Control(Ware.Car) == ToyControl.Child;
            }
            Assert.IsTrue(resumed, "the child takes over again");
            Wait(1.5f);
            var local = TableLocal(car);
            var tt = PlayTableView.NearestRoadT(local);
            Assert.Less(new Vector2(local.x - PlayTableView.RoadPoint(tt).x, local.z - PlayTableView.RoadPoint(tt).z).magnitude, 0.03f, "back on the road");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_SixBlocksFromOneSet()
        {
            OnlyToyFree(Ware.Blocks);
            ServeUntil(Category.Toys);
            Assert.AreEqual(4, s.Shop.Order.Sum, "a set of blocks is 4");
            Serve();
            Wait(PlayArea.WalkSeconds + 0.2f);
            Assert.AreEqual(WareInfo.BlocksInSet, s.Play.ToyCount(Ware.Blocks), "one set = six blocks");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_ChildBuildsTower()
        {
            SellToyAndWaitForPlay(Ware.Blocks);
            Wait(3);
            Assert.AreEqual(1, s.Play.Build.Count, "the child lays the first block (EPIC-017: then leaves room)");
            // The player adds a block on top with the placement help.
            var loose = s.Play.Toys(Ware.Blocks).First(b => b.Slot < 0 && b.Place == ItemPlace.Play && !PlayTableView.IsChildsBlock(b.Index));
            var onTop = BuildSite.CellOf(1, 1);
            PickAndDrop(loose, s.Play.ToWorld(s.Play.Build.Centre(onTop) + new Vector3(0.01f, 0.03f, 0.01f)), 0.3f);
            Assert.AreEqual(2, s.Play.Build.Count, "the player's block joined the build");
            Assert.AreEqual(onTop, loose.Slot, "on top of the child's");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_FullPlayAreaOtherSalesContinue()
        {
            foreach (var t in Assortment.ClassicOf(Category.Toys)) Assert.IsTrue(s.PlayArea.TryReserve(t, 99)); // every zone
            for (var k = 0; k < 7; k++)
            {
                var order = Serve();
                Assert.AreNotEqual(Category.Toys, order.Category, "no toy orders while every play spot is taken");
                Wait(Shop.ThankSeconds + Shop.LeaveSeconds);
            }
            Assert.AreEqual(7, s.Shop.CompletedSales);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_ChildLeavingNeverSnatchesToy()
        {
            SellToyAndWaitForPlay(Ware.Ball);
            var ball = PlayToy(Ware.Ball);
            Step(ball.Position);
            Step(ball.Position, true);
            for (var t = 0f; t < PlayArea.PlaySeconds + 10; t += 0.1f) Step(ball.Position, true, 0.1f);
            Assert.AreSame(ball, s.Held(1), "still in the hand after the play time");
            Assert.AreNotEqual(SpotState.Free, s.PlayArea.State(Ware.Ball), "the child waits");
            Step(ball.Position, false);
            Wait(PlayArea.LingerSeconds + PlayArea.LeaveSeconds + 1);
            Assert.AreEqual(SpotState.Free, s.PlayArea.State(Ware.Ball), "a calm goodbye afterwards");
            Assert.AreEqual(ItemPlace.Gone, ball.Place, "and the ball went with the child");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_TidyNeverSnatchesHeldToy()
        {
            SellToyAndWaitForPlay(Ware.Blocks);
            var block = PlayToy(Ware.Blocks);
            Step(block.Position);
            Step(block.Position, true);
            Frame(new ShopFrame { TidyPlay = true, Right = new HandFrame { Tracked = true, Position = block.Position, Grab = true, Rotation = Quaternion.identity } });
            Assert.AreSame(block, s.Held(1), "tidying leaves the block in the hand");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_LostToyReturns()
        {
            SellToyAndWaitForPlay(Ware.Ball);
            var ball = PlayToy(Ware.Ball);
            var till = s.Wallet.Balance;
            // Carry it off the table and drop it on the floor.
            var off = s.Play.ToWorld(new Vector3(0, 0.1f, -0.6f));
            PickAndDrop(ball, off, 0.05f);
            Wait(1);
            Assert.Less(TableLocal(ball).y, -PlayTableView.LostBelow, "on the floor");
            Wait(PlayTableView.LostSeconds + 1.5f);
            Assert.Greater(TableLocal(ball).y, -0.02f, "back on the table by itself");
            Assert.AreEqual(till, s.Wallet.Balance, "free");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_TidyPlayArea()
        {
            SellToyAndWaitForPlay(Ware.Car);
            var car = PlayToy(Ware.Car);
            PickAndDrop(car, s.Play.ToWorld(new Vector3(-0.45f, 0.1f, -0.15f)), 0.5f);
            var till = s.Wallet.Balance;
            Press(s.Play.TidyButtonItem);
            Wait(0.5f); // the car glides back
            var local = TableLocal(car);
            Assert.Less(Vector3.Distance(new Vector3(local.x, 0, local.z), new Vector3(PlayTableView.Home(Ware.Car).x, 0, PlayTableView.Home(Ware.Car).z)), 0.04f, "back in its place");
            Assert.AreEqual(till, s.Wallet.Balance, "the till is untouched");
            Wait(1.5f);
            Assert.AreEqual(ToyControl.Child, s.PlayArea.Control(Ware.Car), "and the child drives on from there");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_ResetsKeepKassa()
        {
            Serve();
            Wait(Shop.ThankSeconds + Shop.LeaveSeconds);
            WaitForOrdering();
            var till = s.Wallet.Balance;
            Assert.Greater(till, 0);
            Frame(new ShopFrame { AddDirect = s.Shop.Order.Category == Category.FruitVeg ? Ware.Apple : WareInfo.Catalogue(s.Shop.Category)[0] });
            Frame(new ShopFrame { ClearBasket = true });
            Frame(new ShopFrame { TidyPlay = true });
            Wait(0.5f);
            Assert.AreEqual(till, s.Wallet.Balance, "Återställ beställningen and Städa lekytan keep the till");
            Assert.IsTrue(s.Shop.Basket.IsEmpty);
            Frame(new ShopFrame { NewGame = true });
            Assert.AreEqual(0, s.Wallet.Balance, "only Nytt spel empties it");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_PauseWithToyInHand()
        {
            SellToyAndWaitForPlay(Ware.Ball);
            var ball = PlayToy(Ware.Ball);
            var till = s.Wallet.Balance;
            Step(ball.Position);
            Step(ball.Position, true);
            var p = ball.Position;
            // Moving fast when the system menu opens: no throw.
            for (var i = 0; i < 3; i++)
            {
                p += Vector3.up * 0.05f;
                Step(p, true);
            }
            for (var i = 0; i < 20; i++) Step(p, true, 0.05f, releaseAll: true);
            Wait(1);
            Assert.AreEqual(ItemPlace.Play, ball.Place, "the ball still exists, on the table");
            Assert.Less(ball.Body.linearVelocity.magnitude, 0.6f, "not thrown");
            Assert.Greater(TableLocal(ball).y, -0.02f);
            Assert.AreEqual(till, s.Wallet.Balance, "nothing bought");
            Assert.AreEqual(SpotState.Playing, s.PlayArea.State(Ware.Ball), "the child plays on");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_ObjectCountsBounded()
        {
            ParkRoutes.WalkSpeed = 30; // EPIC-015: not about walking; three children must be at the table together
            try
            {
                for (var zone = 0; zone < PlayArea.Spots; zone++)
                {
                    ServeUntil(Category.Toys);
                    Serve();
                    Wait(Shop.ThankSeconds + Shop.LeaveSeconds);
                }
                Wait(PlayArea.WalkSeconds + 1);
                var spots = string.Join(", ", WareInfo.Toys.Where(t => s.PlayArea.State(t) != SpotState.Free).Select(t => $"{t} {s.PlayArea.State(t)} {s.PlayArea.StateTime(t):F1}s"));
                Assert.AreEqual(3, s.PlayArea.Children, $"three children play (spots: {spots}; customer {s.Shop.CustomerNumber} {s.Shop.State} {s.Shop.Order})");
                Assert.LessOrEqual(s.Play.PhysicsBodies, PlayArea.MaxBodies, "the physics budget (EPIC-010: 8)");
                Assert.AreEqual(8, s.Play.PhysicsBodies);
                var rigidbodies = Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length;
                Assert.LessOrEqual(rigidbodies, 8, "nothing else has physics");
                yield return null;
            }
            finally { ParkRoutes.WalkSpeed = ParkRoutes.DefaultWalkSpeed; }
        }

        // ---------- EPIC-005: seated play, stick walking and turning ----------

        // One frame of the rig moving by `delta` (world -> world) with the hand held still relative to the rig.
        Vector3 RigFrame(Vector3 hand, Matrix4x4 delta, bool grab, float dt = 1 / 72f)
        {
            hand = delta.MultiplyPoint3x4(hand);
            s.Step(new ShopFrame
            {
                DeltaTime = dt,
                RigDelta = delta,
                Right = new HandFrame { Tracked = true, Position = hand, Rotation = Quaternion.identity, Grab = grab },
            });
            return hand;
        }

        [UnityTest]
        public IEnumerator Scene_WalkWithHeldItemNoThrow()
        {
            SellToyAndWaitForPlay(Ware.Ball);
            var ball = PlayToy(Ware.Ball);
            Wait(3);
            var at = ball.Position;
            Step(at);
            Step(at, true);
            Assert.AreSame(ball, s.Held(1));
            at += Vector3.up * 0.2f; // lifted clear of the table: no contacts, only what the release gives it
            for (var i = 0; i < 12; i++) at = RigFrame(at, Matrix4x4.identity, true); // held still > ReleaseWindow
            // Walk sideways at full speed for a while, then turn, holding the ball still in the hand.
            var shift = Matrix4x4.Translate(Vector3.right * (Locomotion.WalkSpeed / 72f));
            for (var i = 0; i < 20; i++) at = RigFrame(at, shift, true);
            var pivot = at + new Vector3(0.2f, 0.3f, -0.3f); // the head
            var turn = Matrix4x4.Translate(pivot) * Matrix4x4.Rotate(Quaternion.Euler(0, XrInputRouter.TurnDegrees, 0)) * Matrix4x4.Translate(-pivot);
            at = RigFrame(at, turn, true);
            at = RigFrame(at, Matrix4x4.identity, false); // let go right after (the last 0.1 s were all rig motion)
            Assert.IsNull(s.Held(1));
            var v = ball.Body.linearVelocity;
            Debug.Log($"[test] ball velocity after release {v} (horizontal {new Vector2(v.x, v.z).magnitude:0.000})");
            Assert.Less(new Vector2(v.x, v.z).magnitude, 0.01f, "walking or turning is not a throw (falling after release is gravity)");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_SellWhileWalkingBetweenStalls()
        {
            WaitForOrdering();
            var till = s.Wallet.Balance;
            var fruit = s.Stall(Category.FruitVeg);
            var apple = s.CrateTop(Ware.Apple);
            var at = apple.Position;
            Step(at);
            Step(at, true);
            Assert.AreSame(apple, s.Held(1));
            var basket = fruit.ToWorld(StandView.BasketCentre + Vector3.up * 0.12f);
            // Half of the way by walking (the rig carries the hand), the rest by reaching.
            var walk = Matrix4x4.Translate((basket - at) * 0.5f / 20f);
            for (var i = 0; i < 20; i++) at = RigFrame(at, walk, true);
            Step(basket, true);
            Step(basket, false);
            Wait(0.6f);
            Assert.AreEqual(SaleState.AwaitingPayment, s.Shop.State, "the apple landed in the basket");
            PickAndDrop(s.Coin, fruit.ToWorld(StandView.RegisterCentre + Vector3.up * 0.14f), 0.1f);
            Assert.AreEqual(till + 1, s.Wallet.Balance);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_HeightMovesFurnitureNotPlayer()
        {
            WaitForOrdering();
            Frame(new ShopFrame { AddDirect = s.Shop.Order.Category == Category.FruitVeg ? Ware.Apple : WareInfo.Catalogue(s.Shop.Category)[0] });
            Wait(0.5f);
            var rig = Object.FindAnyObjectByType<XrRig>(FindObjectsInactive.Include).transform;
            var rigPose = (rig.position, rig.rotation);
            var crate = s.CrateTop(Ware.Apple).Position;
            var basketItem = s.BasketItem(0);
            var inBasket = basketItem?.Position;
            var table = s.Play.ToWorld(Vector3.zero);
            var h = s.CounterHeight;
            var order = s.Shop.Order;
            Frame(new ShopFrame { CounterHeightDelta = 0.1f });
            Wait(0.1f);
            Assert.AreEqual(h + 0.1f, s.CounterHeight, 1e-4);
            Assert.AreEqual(0.1f, s.CrateTop(Ware.Apple).Position.y - crate.y, 1e-3, "wares rise with the counter");
            if (inBasket.HasValue) Assert.AreEqual(0.1f, basketItem.Position.y - inBasket.Value.y, 1e-3, "and the basket");
            Assert.AreEqual(0.1f, s.Play.ToWorld(Vector3.zero).y - table.y, 1e-3, "and the play table");
            Assert.AreEqual(rigPose, (rig.position, rig.rotation), "the player (rig) and floor never move");
            Assert.AreSame(order, s.Shop.Order, "the order is kept");
            Frame(new ShopFrame { CounterHeightDelta = 5f });
            Assert.AreEqual(StandView.MaxHeight, s.CounterHeight, 1e-4, "existing limits");
            Frame(new ShopFrame { CounterHeightDelta = -5f });
            Assert.AreEqual(StandView.MinHeight, s.CounterHeight, 1e-4);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_WalkingNeverMovesMarket()
        {
            s.PlaceInFront(new Vector3(1.5f, 1.2f, -0.7f), new Vector3(1, -0.3f, 1));
            var placed = (s.MarketRoot.position, s.MarketRoot.rotation);
            var placements = s.Placements;
            var walk = Matrix4x4.Translate(new Vector3(0.004f, 0, 0.006f));
            var at = s.ToWorld(Vector3.up);
            for (var i = 0; i < 200; i++) at = RigFrame(at, walk, false);
            Assert.AreEqual(placed, (s.MarketRoot.position, s.MarketRoot.rotation));
            Assert.AreEqual(placements, s.Placements);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_WalkStopsAtCounterWhereverTheMarketIsPlaced()
        {
            s.PlaceInFront(new Vector3(2f, 1.1f, 3f), Vector3.right); // fruit stall towards +x
            var head = s.ToWorld(new Vector3(0, 1.1f, 0));
            for (var i = 0; i < 400; i++) head = s.ConstrainWalk(head, head + Vector3.right * (Locomotion.WalkSpeed / 72f));
            var local = s.ToMarket(head);
            var fruit = MarketCollision.Footprints[0];
            Assert.IsFalse(MarketCollision.Blocked(local), "stopped outside the counter");
            Assert.AreEqual(-(fruit.Half.y + MarketCollision.BodyRadius), fruit.ToLocal(local).y, 0.01f, "right in front of it");
            Assert.AreEqual(1.1f, head.y, 1e-4, "no vertical change");
            yield return null;
        }

        // ---------- Öppet/Stängt sign hangs from a rod (EPIC-005 follow-up after the headset test) ----------

        static Renderer SignBoard(StandView stall) =>
            stall.OpenSign.GetComponentsInChildren<Renderer>().First(r => r.name == "Board");

        static bool PartOfSignOrRod(Renderer r, StandView[] stalls) =>
            r.name == "Sign Rod" || stalls.Any(st => r.transform.IsChildOf(st.OpenSign));

        [UnityTest]
        public IEnumerator Scene_OpenSignHangsClearOfTheStall()
        {
            var stalls = WareInfo.Categories.Select(c => s.Stall(c)).ToArray();
            var everything = stalls.SelectMany(st => st.Root.GetComponentsInChildren<Renderer>()).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToList();
            foreach (var stall in stalls)
            {
                var board = SignBoard(stall);
                var clear = board.bounds;
                clear.Expand(0.02f); // at least 1 cm of air on every side
                foreach (var r in everything)
                {
                    if (PartOfSignOrRod(r, stalls)) continue;
                    Assert.IsFalse(clear.Intersects(r.bounds), $"{stall.Root.name}: the sign touches {r.name} ({r.transform.parent?.name})");
                }
                var rod = stall.Root.GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name == "Sign Rod");
                Assert.IsNotNull(rod, $"{stall.Root.name}: a rod to hang the sign from");
                var post = stall.Root.GetComponentsInChildren<Renderer>().Where(r => r.name == "Post").OrderBy(r => Vector3.Distance(r.bounds.center, rod.bounds.center)).First();
                Assert.IsTrue(rod.bounds.Intersects(post.bounds), "the rod comes out of a post");
                Assert.Less(board.bounds.max.y, rod.bounds.min.y, "the sign hangs below the rod");
                var below = new Vector3(board.bounds.center.x, rod.bounds.center.y, board.bounds.center.z);
                Assert.IsTrue(rod.bounds.Contains(below) || rod.bounds.SqrDistance(below) < 0.0004f, "right under it");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_SwipeThroughOpenSignFlipsIt()
        {
            var centre = SignBoard(s.Stand).bounds.center;
            Assert.AreEqual("Öppet", s.OpenSignShown);
            Step(centre + Vector3.left * 0.3f);                 // empty hand, outside
            for (var x = -0.3f; x <= 0.3f; x += 0.03f) Step(centre + Vector3.right * x);
            Assert.AreEqual("Stängt", s.OpenSignShown, "a pass through the board flips it");
            Assert.IsFalse(s.Shop.IsOpen);
            Wait(1);
            for (var x = 0.3f; x >= -0.3f; x -= 0.03f) Step(centre + Vector3.right * x);
            Assert.AreEqual("Öppet", s.OpenSignShown, "and back");
            yield return null;
        }

        // ---------- EPIC-006: the crocodile game ----------

        // Presses a tooth with the right hand like any button (fresh grab press next to it), then takes the hand away.
        void PressTooth(int tooth)
        {
            var at = s.Crocodile.ToothWorld(tooth) + Vector3.up * 0.02f;
            Step(at);
            Step(at, true);
            Step(at, false);
            Step(at + Vector3.up * 0.4f); // away from the mouth
        }

        void SnapCroc()
        {
            for (var i = 0; i < 400 && s.Crocodile.Game.State != CrocState.Open; i++) Step(dt: 0.05f);
            Assert.AreEqual(CrocState.Open, s.Crocodile.Game.State);
            PressTooth(s.Crocodile.Game.TriggerTooth);
            Assert.AreEqual(CrocState.Closing, s.Crocodile.Game.State);
        }

        [UnityTest]
        public IEnumerator Scene_CrocAlwaysThere()
        {
            var croc = s.Crocodile;
            Assert.IsNotNull(croc);
            Assert.IsTrue(croc.Root.gameObject.activeInHierarchy);
            Assert.IsTrue(croc.Root.IsChildOf(s.Play.Root), "at the play table");
            Assert.AreEqual(CrocState.Open, croc.Game.State, "open mouth from the start");
            Assert.AreEqual(0, croc.Root.GetComponentsInChildren<TextMesh>(true).Length, "no counter or explanation shown");
            Assert.AreEqual(0, croc.Root.GetComponentsInChildren<Rigidbody>(true).Length, "nothing physical: it can't push anything");
            SellToyAndWaitForPlay(Ware.Ball); // a child comes ...
            s.TidyPlayArea();
            Frame(new ShopFrame { ClearBasket = true });
            Wait(PlayArea.PlaySeconds + PlayArea.LeaveSeconds + 2); // ... and goes
            Assert.AreEqual(SpotState.Free, s.PlayArea.State(Ware.Ball));
            Assert.IsTrue(croc.Root.gameObject.activeInHierarchy, "still there");
            for (var t = 0; t < CrocodileGame.Teeth; t++) Assert.IsNotNull(croc.ToothItem(t));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_CrocPlayedWithHands()
        {
            var croc = s.Crocodile;
            WaitForOrdering();
            var order = s.Shop.Order;
            var till = s.Wallet.Balance;
            var haptics = new System.Collections.Generic.List<int>();
            s.Haptic += (hand, amp, sec) => haptics.Add(hand);
            var trigger = croc.Game.TriggerTooth;
            var safe = (trigger + 3) % CrocodileGame.Teeth;
            var up = croc.ToothItem(safe).Transform.localPosition;
            Assert.AreEqual(1f, croc.MouthOpen, 1e-3, "open");
            PressTooth(safe);
            Assert.IsTrue(croc.Game.IsPressed(safe));
            Wait(0.3f);
            Assert.Less(croc.ToothItem(safe).Transform.localPosition.y, up.y - 0.005f, "the safe tooth sinks and stays down");
            Assert.IsNull(s.Held(1), "pressing is not holding");
            Assert.IsFalse(croc.ToothItem(safe).Grabbable, "can't be pressed again this round");

            // Holding the button on a tooth, or menus/focus loss, never presses.
            var other = (trigger + 5) % CrocodileGame.Teeth;
            var at = croc.ToothWorld(other) + Vector3.up * 0.02f;
            s.Step(new ShopFrame { DeltaTime = 0.02f, ReleaseAll = true, Right = new HandFrame { Tracked = true, Position = at, Rotation = Quaternion.identity, Grab = true } });
            Assert.IsFalse(croc.Game.IsPressed(other), "blocked while a menu is open / focus lost");
            Step(at, true);
            Assert.IsFalse(croc.Game.IsPressed(other), "a button held through it is not a press");
            Step(at, false);
            Step(at + Vector3.up * 0.4f);

            haptics.Clear();
            PressTooth(trigger);
            Assert.AreEqual(CrocState.Closing, croc.Game.State, "the trigger tooth snaps the mouth shut");
            CollectionAssert.Contains(haptics, 1, "a short buzz in the hand that set it off");
            Wait(CrocodileGame.CloseSeconds + 0.05f);
            Assert.AreEqual(0f, croc.MouthOpen, 1e-3, "shut");
            PressTooth(other);
            Assert.IsFalse(croc.Game.IsPressed(other), "no presses while shut");
            for (var i = 0; i < 400 && croc.Game.State != CrocState.Open; i++) Step(dt: 0.05f);
            Assert.AreEqual(1f, croc.MouthOpen, 1e-3, "opens again by itself");
            for (var t = 0; t < CrocodileGame.Teeth; t++) Assert.IsTrue(croc.ToothItem(t).Grabbable, $"tooth {t} up again");

            Assert.AreSame(order, s.Shop.Order, "the order is untouched");
            Assert.AreEqual(till, s.Wallet.Balance, "no money involved");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_CrocDanceEndsInBasePose()
        {
            var croc = s.Crocodile;
            var basePose = (croc.Body.localPosition, croc.Body.localRotation);
            for (var round = 1; round <= 5; round++)
            {
                SnapCroc();
                Assert.AreNotEqual(CrocState.Dancing, croc.Game.State);
            }
            // The hand stays in the mouth area: no dance yet.
            var mouth = croc.ToothWorld(0) + Vector3.up * 0.05f;
            for (var i = 0; i < 60; i++) Step(mouth, dt: 0.05f);
            Assert.AreEqual(CrocState.WaitingToDance, croc.Game.State, "waits for the hand to leave");
            Step(mouth + Vector3.up * 0.5f);
            Step(dt: 0.05f);
            Assert.AreEqual(CrocState.Dancing, croc.Game.State, "the surprise");
            var moved = false;
            for (var i = 0; i < 20; i++)
            {
                Step(dt: 0.05f);
                moved |= Vector3.Distance(croc.Body.localPosition, basePose.Item1) > 0.005f || Quaternion.Angle(croc.Body.localRotation, basePose.Item2) > 3;
            }
            Assert.IsTrue(moved, "it wiggles and bobs");
            for (var i = 0; i < 400 && croc.Game.State != CrocState.Open; i++) Step(dt: 0.05f);
            Assert.AreEqual(CrocState.Open, croc.Game.State);
            Assert.AreEqual(basePose.Item1, croc.Body.localPosition, "back exactly in its place");
            Assert.AreEqual(0, Quaternion.Angle(basePose.Item2, croc.Body.localRotation), 1e-3);
            Assert.IsTrue(croc.Game.Danced);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_CrocResetRules()
        {
            var croc = s.Crocodile;
            SnapCroc();
            SnapCroc();
            s.TidyPlayArea();
            Frame(new ShopFrame { ClearBasket = true });
            Frame(new ShopFrame { TidyPlay = true });
            Assert.AreEqual(2, croc.Game.Snaps, "tidying and resetting the order keep the count");
            s.NewGame();
            Assert.AreEqual(0, croc.Game.Snaps, "Nytt spel resets it");
            Assert.IsFalse(croc.Game.Danced);
            Assert.AreEqual(CrocState.Open, croc.Game.State);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_CrocFollowsHeight()
        {
            var croc = s.Crocodile;
            var tooth = croc.ToothWorld(0);
            var table = s.Play.ToWorld(Vector3.zero);
            Frame(new ShopFrame { CounterHeightDelta = 0.1f });
            Assert.AreEqual(0.1f, croc.ToothWorld(0).y - tooth.y, 1e-3, "rises with the play table");
            Assert.AreEqual(s.Play.ToWorld(Vector3.zero).y - table.y, croc.ToothWorld(0).y - tooth.y, 1e-4);
            yield return null;
        }
    }
}
