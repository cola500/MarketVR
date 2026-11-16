// The real scene, driven through FruitStand.Step with simulated hand poses: the same path the VR controllers use
// (Grabber -> drop zones -> Shop), bypassing only the device read. Names match docs/EPIC-001.md.
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
    public class StandSceneTests
    {
        FruitStand s;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneLoad.Fresh(x => s = x); // synchronous load, classic fruit (see SceneLoad)
            s = Object.FindAnyObjectByType<FruitStand>();
            Assert.IsNotNull(s, "scene contains FruitStand");
            s.ReadDesktopInput = false;
            yield return null;
        }

        void Step(Vector3? right = null, bool grab = false, float dt = 0.02f, bool releaseAll = false) =>
            SceneSteps.Step(s, right, grab, dt, releaseAll);

        void Wait(float seconds) => SceneSteps.Wait(seconds, 0.05f, dt => Step(dt: dt));

        // EPIC-015: the customer walks in along the park's paths first: wait until they are at the counter.
        void ToOrdering()
        {
            for (var t = 0f; t < 40 && s.Shop.State != SaleState.Ordering; t += 0.05f) Step(dt: 0.05f);
            Assert.AreEqual(SaleState.Ordering, s.Shop.State);
        }

        Vector3 AboveBasket => s.ActiveStall.ToWorld(StandView.BasketCentre + Vector3.up * 0.12f);
        Vector3 AboveRegister => s.ActiveStall.ToWorld(StandView.RegisterCentre + Vector3.up * 0.14f);
        Vector3 Away => s.Stand.ToWorld(new Vector3(-0.2f, 0.35f, -0.25f)); // in the air in front of the player, no zone

        // Hover, press, carry, release, then let animations finish.
        void PickAndDrop(Item item, Vector3 to, float settle = 0.5f)
        {
            Assert.IsNotNull(item);
            var from = item.Position;
            Step(from);
            Assert.AreSame(item, s.Hover(1), "hand next to the item highlights it");
            Step(from, true);
            Assert.AreSame(item, s.Held(1), "grabbed");
            Step(Vector3.Lerp(from, to, 0.5f), true);
            Step(to, true);
            Assert.Less(Vector3.Distance(item.Position, to), 1e-3f, "follows the hand");
            Step(to, false);
            Assert.IsNull(s.Held(1));
            Wait(settle);
        }

        void PayWithCoin()
        {
            Wait(0.6f); // coin slides in from the customer
            Assert.AreEqual(ItemPlace.Tray, s.Coin.Place);
            Assert.IsTrue(s.Coin.Transform.gameObject.activeSelf, "coin token on the counter");
            PickAndDrop(s.Coin, AboveRegister, 0.1f);
        }

        void FillOrderByHand()
        {
            foreach (var p in WareInfo.All)
                for (var i = 0; i < s.Shop.Order.Count(p); i++) PickAndDrop(s.CrateTop(p), AboveBasket, 0.2f);
        }

        [UnityTest, Timeout(20000)]
        public IEnumerator Scene_StartsInDesktopModeWithoutXr()
        {
            var until = Time.realtimeSinceStartup + 10;
            while (!s.ModeDecided && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(s.ModeDecided, "mode decided within 10 s");
            Assert.IsFalse(s.XrMode);
            Assert.AreEqual(SaleState.CustomerArriving, s.Shop.State);
            Assert.AreEqual(Order.Of(Ware.Apple), s.Shop.Order, "first customer wants one apple");
            foreach (var p in WareInfo.All) if (s.Market.Assortment.IsActive(p)) Assert.IsNotNull(s.CrateTop(p), $"{p} crate has something to take");
        }

        [UnityTest]
        public IEnumerator Scene_AppleFromCrateToBasket()
        {
            ToOrdering();
            var apple = s.CrateTop(Ware.Apple);
            PickAndDrop(apple, AboveBasket + new Vector3(0.03f, 0, 0.02f));
            Assert.AreEqual(ItemPlace.Basket, apple.Place);
            Assert.AreEqual(1, s.Shop.Basket.Count(Ware.Apple));
            Assert.Less(Vector3.Distance(apple.Transform.localPosition, StandView.SlotRest(apple.Slot, Ware.Apple)), 1e-3f, "lies still in its slot");
            Assert.AreEqual(SaleState.AwaitingPayment, s.Shop.State);
            Assert.GreaterOrEqual(s.Sounds.Played, 2, "tick on pick-up, pop in the basket");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_DroppedItemReturnsToCrate()
        {
            ToOrdering();
            var banana = s.CrateTop(Ware.Banana);
            PickAndDrop(banana, Away);
            Assert.AreEqual(ItemPlace.Gone, banana.Place, "went back into its crate");
            Assert.IsTrue(s.Shop.Basket.IsEmpty);
            Assert.AreNotSame(banana, s.CrateTop(Ware.Banana));
            foreach (var i in s.Items) Assert.AreNotEqual(ItemPlace.Returning, i.Place, "nothing left lying around");
            yield return null;
        }

        // Blender models (SPIKE-BLENDER, EPIC-009 M1, EPIC-010 M1): every ware is its exported FBX, coloured per submesh
        // from Models.ModelColours, centred on the grab point and about as big as its grab radius; long ones along +x.
        [UnityTest]
        public IEnumerator Scene_WaresAreBlenderModels()
        {
            foreach (var w in WareInfo.All)
            {
                var root = Models.Ware(s.Art, s.Stand.Top, w, "Probe");
                var filters = root.GetComponentsInChildren<MeshFilter>();
                Assert.AreEqual(1, filters.Length, w + ": one mesh, not primitive parts");
                var mesh = filters[0].sharedMesh;
                Assert.IsTrue(mesh != null, w + ": has a mesh");
                Assert.AreEqual(w.ToString(), mesh.name, w + " is its Blender model");
                var colours = Models.ModelColours(w);
                Assert.AreEqual(colours.Length, mesh.subMeshCount, w + ": one submesh per material slot");
                var materials = filters[0].GetComponent<MeshRenderer>().sharedMaterials;
                for (var i = 0; i < colours.Length; i++) Assert.AreEqual(colours[i], materials[i].color, $"{w} submesh {i}");
                var bounds = filters[0].GetComponent<Renderer>().bounds;
                Assert.Less(Vector3.Distance(bounds.center, root.position), 0.01f, w + ": pivot at the grab point");
                var half = Mathf.Max(mesh.bounds.extents.x, mesh.bounds.extents.y, mesh.bounds.extents.z);
                Assert.That(half / Models.Radius(w), Is.InRange(0.5f, 1.8f), w + ": about as big as its grab radius");
                Art.Discard(root);
            }
            var banana = s.Art.ModelOf(Ware.Banana);
            Assert.Greater(banana.GetSubMesh(1).bounds.center.x, 0.05f, "banana stalk at +x, as before");
            Assert.Greater(banana.GetSubMesh(1).bounds.center.y, 0f, "banana ends curve up");
            Assert.Less(banana.GetSubMesh(2).bounds.center.x, -0.05f, "banana tip at -x");
            var carrot = s.Art.ModelOf(Ware.Carrot);
            Assert.Less(carrot.GetSubMesh(1).bounds.center.x, -0.05f, "carrot leaves at -x, tip at +x, as before");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_CrateRefillsAfterGrab()
        {
            ToOrdering();
            foreach (var p in WareInfo.Categories.SelectMany(c => s.Market.Assortment.Active(c)))
            {
                var top = s.CrateTop(p);
                Step(top.Position);
                Step(top.Position, true);
                var next = s.CrateTop(p);
                Assert.AreNotSame(top, next);
                Assert.AreEqual(ItemPlace.Crate, next.Place);
                Assert.Less(Vector3.Distance(next.Transform.localPosition, StandView.CrateTop(s.Market.Assortment.Bin(p), p)), 1e-3f);
                Step(Away, false);
                Wait(0.5f);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_FullSaleWithHands()
        {
            ToOrdering();
            PickAndDrop(s.CrateTop(Ware.Apple), AboveBasket);
            Assert.AreEqual(SaleState.AwaitingPayment, s.Shop.State);
            var soundsBefore = s.Sounds.Played;
            PayWithCoin();
            Assert.AreEqual(1, s.Shop.CompletedSales);
            Assert.AreEqual(1, s.SalesRegistered);
            Assert.AreEqual(SaleState.Thanking, s.Shop.State);
            Assert.Greater(s.Sounds.Played, soundsBefore, "pling");
            Wait(Shop.ThankSeconds + 0.1f);
            Assert.AreEqual(SaleState.CustomerLeaving, s.Shop.State);
            Wait(0.6f);
            for (var slot = 0; slot < Basket.Capacity; slot++) Assert.IsNull(s.BasketItem(slot), "basket cleared away");
            Wait(Shop.LeaveSeconds);
            Assert.AreEqual(2, s.Shop.CustomerNumber, "next customer");
            Assert.AreEqual(SaleState.CustomerArriving, s.Shop.State);
            Assert.IsFalse(s.Coin.Transform.gameObject.activeSelf, "no coin until the next order is right");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_RepeatedRegisterEventsCountOnce()
        {
            ToOrdering();
            s.Step(new ShopFrame { DeltaTime = 0.02f, AddDirect = Ware.Apple });
            Wait(0.6f);
            Assert.IsTrue(s.RegisterCoin());
            Assert.IsFalse(s.RegisterCoin(), "second event, same frame");
            s.Step(new ShopFrame { DeltaTime = 0.02f, PayDirect = true });
            Step(AboveRegister, false);
            Assert.AreEqual(1, s.Shop.CompletedSales);
            Assert.AreEqual(1, s.SalesRegistered);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_PaymentBeforeMatchHasNoCoin()
        {
            ToOrdering();
            Assert.IsFalse(s.Coin.Transform.gameObject.activeSelf);
            Assert.IsFalse(s.RegisterCoin(), "no coin, nothing to register");
            s.Step(new ShopFrame { DeltaTime = 0.02f, PayDirect = true });
            Assert.AreEqual(0, s.Shop.CompletedSales);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_CoinDroppedElsewhereReturnsToTray()
        {
            ToOrdering();
            PickAndDrop(s.CrateTop(Ware.Apple), AboveBasket);
            Wait(0.6f);
            PickAndDrop(s.Coin, Away);
            Assert.AreEqual(ItemPlace.Tray, s.Coin.Place);
            Assert.Less(Vector3.Distance(s.Coin.Transform.localPosition, StandView.CoinSpot), 1e-3f);
            Assert.AreEqual(0, s.Shop.CompletedSales);
            PickAndDrop(s.Coin, AboveRegister, 0.1f);
            Assert.AreEqual(1, s.Shop.CompletedSales);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_TwoSalesInARow()
        {
            ToOrdering();
            FillOrderByHand();
            PayWithCoin();
            Wait(Shop.ThankSeconds + Shop.LeaveSeconds + 0.2f);
            ToOrdering();
            var order = s.Shop.Order;
            Assert.AreEqual(order.Total, s.Bubble.TickCount, "one picture per ordered item");
            var first = System.Array.Find(WareInfo.All, p => order.Count(p) > 0);
            PickAndDrop(s.CrateTop(first), AboveBasket, 0.1f);
            if (order.Total > 1) Assert.AreEqual(1, s.Bubble.TicksOn, "one tick for the item in the basket");
            for (var i = 1; i < order.Count(first); i++) PickAndDrop(s.CrateTop(first), AboveBasket, 0.1f);
            foreach (var p in WareInfo.All)
                if (p != first)
                    for (var i = 0; i < order.Count(p); i++) PickAndDrop(s.CrateTop(p), AboveBasket, 0.1f);
            Assert.AreEqual(SaleState.AwaitingPayment, s.Shop.State);
            PayWithCoin();
            Assert.AreEqual(2, s.Shop.CompletedSales);
            Assert.AreEqual(2, s.SalesRegistered);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_WrongItemCanBeRemovedByHand()
        {
            ToOrdering(); // one apple
            var carrot = s.CrateTop(Ware.Carrot);
            PickAndDrop(carrot, AboveBasket);
            Assert.AreEqual(ItemPlace.Basket, carrot.Place);
            Assert.AreEqual(SaleState.Ordering, s.Shop.State, "a wrong item: nothing happens, no penalty");
            PickAndDrop(carrot, Away);
            Assert.AreEqual(ItemPlace.Gone, carrot.Place, "taken out and back to its crate");
            Assert.IsTrue(s.Shop.Basket.IsEmpty);
            PickAndDrop(s.CrateTop(Ware.Apple), AboveBasket);
            Assert.AreEqual(SaleState.AwaitingPayment, s.Shop.State);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_FocusLossReturnsHeldItemHome()
        {
            ToOrdering();
            var apple = s.CrateTop(Ware.Apple);
            Step(apple.Position);
            Step(apple.Position, true);
            Step(AboveBasket, true);
            Assert.AreSame(apple, s.Held(1));
            // System menu opens while the apple is held over the basket.
            // The button stays held the whole time the menu is open.
            for (var i = 0; i < 10; i++) Step(AboveBasket, true, 0.05f, releaseAll: true);
            Assert.IsNull(s.Held(1));
            Assert.AreEqual(ItemPlace.Gone, apple.Place, "went home, was not dropped into the basket");
            Assert.IsTrue(s.Shop.Basket.IsEmpty);
            // Back with the button still held at a crate: nothing is grabbed until a fresh press.
            var top = s.CrateTop(Ware.Apple);
            Step(top.Position, true);
            Assert.IsNull(s.Held(1));
            Step(top.Position, false);
            Step(top.Position, true);
            Assert.AreSame(top, s.Held(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_RestartFromMenu()
        {
            ToOrdering();
            FillOrderByHand();
            PayWithCoin();
            Wait(Shop.ThankSeconds + Shop.LeaveSeconds);
            ToOrdering();
            PickAndDrop(s.CrateTop(Ware.Carrot), AboveBasket);
            s.Step(new ShopFrame { DeltaTime = 0.02f, NewGame = true });
            Assert.AreEqual(0, s.Shop.CompletedSales);
            Assert.AreEqual(1, s.Shop.CustomerNumber);
            Assert.AreEqual(Order.Of(s.Market.Assortment.FirstFruit), s.Shop.Order);
            for (var slot = 0; slot < Basket.Capacity; slot++) Assert.IsNull(s.BasketItem(slot));
            foreach (var i in s.Items) Assert.IsTrue(i.IsCoin || i.IsButton || i.Place == ItemPlace.Crate, "only the bins' wares, the token and the buttons remain");
            yield return null;
        }

        [UnityTest]
        public IEnumerator CounterHeight_MovesEverythingOnCounterAndClamps()
        {
            ToOrdering();
            PickAndDrop(s.CrateTop(Ware.Carrot), AboveBasket);
            var carrot = s.BasketItem(FindSlot());
            var crate = s.CrateTop(Ware.Apple);
            var h = s.Stand.Height;
            var crateY = crate.Position.y;
            var basketY = carrot.Position.y;
            s.Step(new ShopFrame { DeltaTime = 0.02f, CounterHeightDelta = 0.1f });
            Assert.AreEqual(h + 0.1f, s.Stand.Height, 1e-4);
            Assert.AreEqual(crateY + 0.1f, crate.Position.y, 1e-4, "crates follow");
            Assert.AreEqual(basketY + 0.1f, carrot.Position.y, 1e-4, "basket contents follow");
            s.Step(new ShopFrame { DeltaTime = 0.02f, CounterHeightDelta = 5 });
            Assert.AreEqual(StandView.MaxHeight, s.Stand.Height, 1e-4);
            s.Step(new ShopFrame { DeltaTime = 0.02f, CounterHeightDelta = -5 });
            Assert.AreEqual(StandView.MinHeight, s.Stand.Height, 1e-4);
            // still playable at the new height
            PickAndDrop(carrot, Away);
            PickAndDrop(s.CrateTop(Ware.Apple), AboveBasket);
            Assert.AreEqual(SaleState.AwaitingPayment, s.Shop.State);
            yield return null;
        }

        int FindSlot()
        {
            for (var i = 0; i < Basket.Capacity; i++) if (s.BasketItem(i) != null) return i;
            return 0;
        }
    }
}
