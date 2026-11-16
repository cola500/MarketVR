// EPIC-016 in the scene: when a customer has walked in and is ready to order, one short 3D pling sounds from that
// stall (not from the camera), and a small bell shows on that stall's awning while the customer waits. Once per
// customer: not again for menus, pauses or Töm korgen, none afterwards for a customer who became ready behind the
// menu. The phone customer gets the same. Names match docs/EPIC-016.md.
using System.Collections;
using System.Linq;
using Fruktstand.Core;
using Fruktstand.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fruktstand.Tests
{
    public class ArrivalSceneTests
    {
        FruitStand s;

        [TearDown]
        public void Reset() => ParkRoutes.WalkSpeed = ParkRoutes.DefaultWalkSpeed;

        void Step(float seconds, bool menu = false, ShopFrame? first = null)
        {
            var n = Mathf.Max(1, Mathf.RoundToInt(seconds / 0.05f));
            for (var i = 0; i < n; i++)
            {
                var f = i == 0 && first.HasValue ? first.Value : new ShopFrame();
                f.DeltaTime = 0.05f;
                f.ReleaseAll = menu;
                s.Step(f);
            }
        }

        void UntilOrdering(bool menu = false)
        {
            for (var t = 0f; t < 40 && s.Shop.State != SaleState.Ordering; t += 0.05f)
            {
                Assert.AreEqual(0, MarkersShown, "no marker while the customer is still walking in");
                Step(0.05f, menu);
            }
            Assert.AreEqual(SaleState.Ordering, s.Shop.State);
        }

        void Serve()
        {
            UntilOrdering();
            foreach (var w in WareInfo.All)
                for (var i = 0; i < s.Shop.Order.Count(w); i++) Step(0.05f, false, new ShopFrame { AddDirect = w });
            Step(0.6f);
            Assert.IsTrue(s.RegisterCoin(), "paid");
        }

        int MarkersShown => WareInfo.Categories.Count(c => s.Stall(c).WaitingShown);

        [UnityTest]
        public IEnumerator Arrival_ChimeOnceFromTheStall()
        {
            ParkRoutes.WalkSpeed = 4; // the walk is not what this is about
            yield return SceneLoad.Fresh(x => s = x);
            UntilOrdering();
            Assert.AreEqual(1, s.Sounds.Arrivals, "one pling when the customer is ready");
            Assert.Less(Vector3.Distance(s.Sounds.ArrivalSource.position, s.ActiveStall.Top.position), 0.6f, "it sounds from that stall");
            Assert.AreEqual(1f, s.Sounds.ArrivalSpatialBlend, "fully 3D: the direction follows the head");
            Assert.IsFalse(Camera.main != null && s.Sounds.ArrivalSource.IsChildOf(Camera.main.transform), "not on the camera");
            Step(3);
            Step(1, menu: true);
            Step(1);
            Step(0.05f, false, new ShopFrame { ClearBasket = true });
            Step(1);
            Assert.AreEqual(1, s.Sounds.Arrivals, "not again for waiting, the menu or Töm korgen");
            Serve();
            Step(Shop.ThankSeconds + Shop.LeaveSeconds + 0.1f);
            UntilOrdering();
            Assert.AreEqual(2, s.Sounds.Arrivals, "the next customer gets one");
            Assert.Less(Vector3.Distance(s.Sounds.ArrivalSource.position, s.ActiveStall.Top.position), 0.6f, "from the next customer's stall");
        }

        [UnityTest]
        public IEnumerator Arrival_NoChimeWhenReadyBehindTheMenu()
        {
            ParkRoutes.WalkSpeed = 4;
            yield return SceneLoad.Fresh(x => s = x);
            UntilOrdering(menu: true);
            Step(0.5f, menu: true);
            Step(3);
            Assert.AreEqual(0, s.Sounds.Arrivals, "no old pling once the menu is closed");
            Assert.IsTrue(s.ActiveStall.WaitingShown, "the marker still shows who is waiting");
        }

        [UnityTest]
        public IEnumerator Arrival_MarkerFollowsTheStallAndTheCustomer()
        {
            ParkRoutes.WalkSpeed = 4;
            yield return SceneLoad.Fresh(x => s = x);
            var seen = new System.Collections.Generic.HashSet<Category>();
            for (var k = 0; k < 8 && seen.Count < 2; k++)
            {
                UntilOrdering();
                Assert.AreEqual(1, MarkersShown, "one marker");
                Assert.IsTrue(s.ActiveStall.WaitingShown, "at the stall of this customer's order");
                seen.Add(s.Shop.Category);
                foreach (var w in WareInfo.All)
                    for (var i = 0; i < s.Shop.Order.Count(w); i++) Step(0.05f, false, new ShopFrame { AddDirect = w });
                Step(0.3f);
                Assert.AreEqual(SaleState.AwaitingPayment, s.Shop.State);
                Assert.IsTrue(s.ActiveStall.WaitingShown, "still waiting for the coin");
                Assert.IsTrue(s.RegisterCoin());
                Step(0.1f);
                Assert.AreEqual(0, MarkersShown, "gone once the customer is done");
                Step(Shop.ThankSeconds + Shop.LeaveSeconds + 0.1f);
            }
            Assert.GreaterOrEqual(seen.Count, 2, "followed the customers to more than one stall");
        }

        [UnityTest]
        public IEnumerator Arrival_PhoneCustomerGetsTheSameSignal()
        {
            ParkRoutes.WalkSpeed = 4;
            yield return SceneLoad.Fresh(x => s = x);
            Assert.IsTrue(s.Shop.Remote.Request("order-0001abcd", new[] { "banana" }).Accepted);
            Serve(); // the NPC already on the way is served first
            Step(Shop.ThankSeconds + Shop.LeaveSeconds + 0.1f);
            UntilOrdering();
            Assert.IsTrue(s.Shop.CustomerIsHuman, "the parent's turn");
            Assert.AreEqual(2, s.Sounds.Arrivals, "the phone customer plings like anyone");
            Assert.IsTrue(s.ActiveStall.WaitingShown);
        }
    }
}
