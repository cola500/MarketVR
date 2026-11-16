// EPIC-015 in the scene: a customer walks in along the park's paths and is only served on arrival; when the counter is
// free the next one comes while the last strolls on as a park visitor; a toy child plays first; visitors are limited,
// share the bench and are gone after a new game; a rest holds them without double customers or payments.
using System.Collections;
using System.Linq;
using Fruktstand.Core;
using Fruktstand.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fruktstand.Tests
{
    public class ParkVisitorSceneTests
    {
        FruitStand s;

        [TearDown]
        public void Reset()
        {
            FruitStand.StrollRandom = null;
            ParkRoutes.WalkSpeed = ParkRoutes.DefaultWalkSpeed;
            MarketLayout.SetStallDistance(MarketLayout.DefaultStallDistance);
        }

        void Step(float seconds, ShopFrame? frame = null)
        {
            var n = Mathf.Max(1, Mathf.RoundToInt(seconds / 0.05f));
            for (var i = 0; i < n; i++)
            {
                var f = i == 0 && frame.HasValue ? frame.Value : new ShopFrame();
                f.DeltaTime = 0.05f;
                s.Step(f);
                Assert.LessOrEqual(s.Visitors.Count, ParkVisits.MaxVisitors, "never more visitors than allowed");
                MeasureSpacing();
            }
        }

        // The closest two figures on the paths have come (market space, on the ground): the customer while walking in or
        // out, and every park visitor.
        float closest = float.MaxValue;
        string closestWhen = "";

        float customerMaxSide; // the largest step aside of the walking customer so far (diagnosis)
        int sideFor;

        void MeasureSpacing()
        {
            if (s.Shop.CustomerNumber != sideFor) (sideFor, customerMaxSide) = (s.Shop.CustomerNumber, 0);
            customerMaxSide = Mathf.Max(customerMaxSide, Mathf.Abs(s.Customer.Side));
            var figures = s.Visitors.Roots.Select(r => s.ToMarket(r.position)).ToList();
            if (s.Customer.Root.gameObject.activeInHierarchy && s.Shop.State is SaleState.CustomerArriving or SaleState.CustomerLeaving)
                figures.Add(CustomerAt);
            for (var a = 0; a < figures.Count; a++)
                for (var b = a + 1; b < figures.Count; b++)
                {
                    var d = new Vector2(figures[a].x - figures[b].x, figures[a].z - figures[b].z).magnitude;
                    if (d < closest)
                    {
                        closest = d;
                        closestWhen = $"customer {s.Shop.CustomerNumber} {s.Shop.State} side {s.Customer.Side:F2} (most so far {customerMaxSide:F2}) speed {s.Customer.Speed:F2}: {figures[a]} and {figures[b]}";
                    }
                }
        }

        void UntilOrdering(float max = 40)
        {
            for (var t = 0f; t < max && s.Shop.State != SaleState.Ordering; t += 0.05f) Step(0.05f);
            Assert.AreEqual(SaleState.Ordering, s.Shop.State, "the customer arrived");
        }

        void Serve()
        {
            UntilOrdering();
            foreach (var w in WareInfo.All)
                for (var i = 0; i < s.Shop.Order.Count(w); i++) Step(0.05f, new ShopFrame { AddDirect = w });
            Step(0.6f);
            Assert.IsTrue(s.RegisterCoin(), "paid");
        }

        Vector3 CustomerAt => s.ToMarket(s.Customer.Root.position);

        [UnityTest]
        public IEnumerator Park_CustomerWalksInAndSellsOnArrival()
        {
            yield return SceneLoad.Fresh(x => s = x);
            Assert.AreEqual(SaleState.CustomerArriving, s.Shop.State);
            Step(0.05f);
            Assert.Greater(new Vector2(CustomerAt.x, CustomerAt.z).magnitude, 6, "comes from an entrance, far out");
            Assert.AreEqual("", s.Bubble.Words, "no hello from far away");
            var walked = 0f;
            var saidHello = "";
            while (s.Shop.State == SaleState.CustomerArriving && walked < 40)
            {
                Step(0.05f, new ShopFrame { AddDirect = Ware.Apple });
                walked += 0.05f;
                var p = CustomerAt;
                Assert.IsTrue(ParkLayout.OnPath(p) || Mathf.Abs(p.z) > ParkLayout.ArmEnd - 0.01f, $"walks on the path: {p}");
                Assert.AreEqual(0, p.y, 0.05f, "feet on the ground");
                if (s.Bubble.Words != "") saidHello = s.Bubble.Words;
            }
            Assert.Greater(walked, 4, "a real walk, not a pop-in");
            Assert.AreEqual("Hej!", saidHello, "says hello once close");
            Assert.AreEqual(SaleState.Ordering, s.Shop.State);
            Assert.AreEqual(0, s.Shop.Basket.Total, "nothing was taken before the customer arrived");
            Assert.Less(Vector3.Distance(CustomerAt, ParkRoutes.CounterSpot(Category.FruitVeg, MarketLayout.StallDistance)), 0.06f, "at the fruit counter");
        }

        // Polish slice (EPIC-016): the customer who leaves goes out by the nearest gate, the next one often comes in by
        // it: they meet face to face on the arm. Nobody walks through anybody, and the sales go on.
        [UnityTest]
        public IEnumerator Park_FiguresNeverWalkThroughEachOther()
        {
            FruitStand.StrollRandom = _ => 0; // always straight out: the meetings on the arms
            yield return SceneLoad.Fresh(x => s = x);
            closest = float.MaxValue;
            for (var k = 0; k < 4; k++)
            {
                Serve();
                Step(Shop.ThankSeconds + Shop.LeaveSeconds + 0.1f);
            }
            UntilOrdering();
            Step(20);
            Assert.AreEqual(4, s.Shop.CompletedSales, "the sales went on");
            Assert.GreaterOrEqual(closest, MinSpacing, $"two figures came {closest:F2} m apart ({closestWhen})");
        }

        const float MinSpacing = 0.3f; // a figure is about 0.3 m wide

        [UnityTest]
        public IEnumerator Park_NextCustomerWhenCounterFree()
        {
            yield return SceneLoad.Fresh(x => s = x);
            Serve();
            Assert.AreEqual(0, s.Visitors.Count);
            Step(Shop.ThankSeconds + Shop.LeaveSeconds + 0.1f);
            Assert.AreEqual(2, s.Shop.CustomerNumber, "the next customer is on the way");
            Assert.AreEqual(SaleState.CustomerArriving, s.Shop.State);
            Assert.AreEqual(1, s.Visitors.Count, "the last one strolls on as a park visitor");
            var v = s.ToMarket(s.Visitors.Roots.First().position);
            Assert.Less(Vector3.Distance(v, ParkRoutes.CounterSpot(Category.FruitVeg, MarketLayout.StallDistance)), 2.5f, "right where the customer left the counter");
            Step(40);
            Assert.AreEqual(0, s.Visitors.Count, "and has walked out of the park");
        }

        [UnityTest]
        public IEnumerator Park_ToyChildGoesToPlayFirst()
        {
            yield return SceneLoad.Fresh(x => s = x);
            for (var k = 0; k < 6 && s.Shop.Order.Category != Category.Toys; k++)
            {
                Serve();
                Step(Shop.ThankSeconds + Shop.LeaveSeconds + 0.1f);
            }
            Step(30); // the adults' strolls are over
            Assert.AreEqual(Category.Toys, s.Shop.Order.Category);
            Serve();
            Step(Shop.ThankSeconds + Shop.LeaveSeconds + 0.1f);
            Assert.AreEqual(0, s.Visitors.Count, "the child goes to the play table, not into the park");
            Assert.IsTrue(s.PlayArea.Children > 0);
            var sawChildVisitor = false;
            for (var t = 0f; t < PlayArea.WalkSeconds + PlayArea.PlaySeconds + PlayArea.LeaveSeconds + 30 && !sawChildVisitor; t += 0.05f)
            {
                Step(0.05f);
                sawChildVisitor = s.Visitors.Roots.Any(r => r.GetChild(0).localScale.x < 0.9f);
            }
            Assert.IsTrue(sawChildVisitor, "after playing, the child strolls on in the park");
        }

        [UnityTest]
        public IEnumerator Park_VisitorsLimitedAndClearedOnNewGame()
        {
            FruitStand.StrollRandom = n => n - 1; // the longest strolls: the bench, else a detour
            yield return SceneLoad.Fresh(x => s = x);
            var most = 0;
            for (var k = 0; k < 6; k++)
            {
                Serve();
                Step(Shop.ThankSeconds + Shop.LeaveSeconds + 0.1f); // Step asserts the limit every frame
                most = Mathf.Max(most, s.Visitors.Count);
            }
            Assert.GreaterOrEqual(most, 1, "visitors stroll in the park, never more than the limit");
            Assert.Greater(s.Visitors.Count, 0);
            Step(0.05f, new ShopFrame { NewGame = true });
            Step(0.1f);
            Assert.AreEqual(0, s.Visitors.Count, "a new game clears the park");
            Assert.AreEqual(1, s.Visits.FreeSeats, "and frees the bench");
            Assert.IsEmpty(s.Park.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Visitor")), "no figures left behind");
        }

        [UnityTest]
        public IEnumerator Park_VisitorSitsOnTheBenchAndLeaves()
        {
            FruitStand.StrollRandom = n => n - 1; // the bench whenever it can be
            yield return SceneLoad.Fresh(x => s = x);
            Serve();
            var sat = false;
            for (var t = 0f; t < 40 && !sat; t += 0.05f)
            {
                Step(0.05f);
                sat = s.Visitors.SittingCount == 1;
            }
            Assert.IsTrue(sat, "sits down on the bench");
            Step(ParkVisitors.SitDownSeconds + 0.1f);
            var hip = s.ToMarket(s.Visitors.SittingHip);
            Assert.Less(Vector3.Distance(hip, ParkRoutes.BenchSeat), 0.04f, "on the seat: not floating, not inside the bench");
            Assert.AreEqual(0, s.Visits.FreeSeats, "the seat is taken");
            Serve(); // customer 2 can't sit: the seat is taken
            for (var t = 0f; t < ParkVisitors.SitMaxSeconds + 40 && (s.Visitors.SittingCount > 0 || s.Visits.FreeSeats == 0); t += 0.05f)
            {
                Step(0.05f);
                Assert.LessOrEqual(s.Visitors.SittingCount, 1, "one on the seat at a time");
            }
            Assert.AreEqual(1, s.Visits.FreeSeats, "stood up and freed the seat");
        }

        [UnityTest]
        public IEnumerator Park_PauseHoldsVisitorsNoDoubleSale()
        {
            yield return SceneLoad.Fresh(x => s = x);
            Serve();
            Step(Shop.ThankSeconds + Shop.LeaveSeconds + 0.5f);
            Assert.AreEqual(1, s.Visitors.Count);
            Step(0.05f, new ShopFrame { TakeRest = true });
            var visitor = s.Visitors.Roots.First().position;
            var customer = s.Customer.Root.position;
            var state = (s.Shop.State, s.Shop.CustomerNumber, s.Shop.CompletedSales);
            Step(20);
            Assert.AreEqual(0, Vector3.Distance(visitor, s.Visitors.Roots.First().position), 1e-4f, "the visitor waits during the rest");
            Assert.AreEqual(0, Vector3.Distance(customer, s.Customer.Root.position), 1e-4f, "so does the coming customer");
            Assert.AreEqual(state, (s.Shop.State, s.Shop.CustomerNumber, s.Shop.CompletedSales));
            Step(0.05f, new ShopFrame { ResumeRest = true });
            Serve();
            Assert.AreEqual(2, s.Shop.CompletedSales, "one sale each, none twice");
            Assert.AreEqual(s.Shop.CompletedSales, s.SalesRegistered);
            Assert.AreEqual(1, Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name == "Customer" && t.gameObject.activeInHierarchy), "one customer at the counter");
        }
    }
}
