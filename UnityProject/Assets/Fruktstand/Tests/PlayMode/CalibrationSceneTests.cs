// EPIC-007 in the real scene: the market's floor marking shares the market's root and layout, the preview never
// moves anything, and a confirmed calibration moves the whole market (stalls, play table, crocodile shelf, marking)
// at once with the chosen spacing, after which selling and playing work as before. Names match docs/EPIC-007.md.
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
    public class CalibrationSceneTests
    {
        FruitStand s;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneLoad.Fresh(x => s = x); // synchronous load, classic fruit (see SceneLoad)
            s = Object.FindAnyObjectByType<FruitStand>();
            s.ReadDesktopInput = false;
            yield return null;
        }

        [TearDown]
        public void CompactAgain() => MarketLayout.SetStallDistance(MarketLayout.DefaultStallDistance);

        void Step(Vector3? right = null, bool grab = false, float dt = 0.02f) => SceneSteps.Step(s, right, grab, dt);

        void Wait(float seconds) => SceneSteps.Wait(seconds, 0.05f, dt => Step(dt: dt));

        void PickAndDrop(Item item, Vector3 to, float settle = 0.5f)
        {
            var from = item.Position;
            Step(from);
            Step(from, true);
            Assert.AreSame(item, s.Held(1));
            Step(Vector3.Lerp(from, to, 0.5f), true);
            Step(to, true);
            Step(to, false);
            Wait(settle);
        }

        static Transform Piece(Transform marking, string name) =>
            marking.GetComponentsInChildren<Transform>(true).First(t => t.name == name);

        [UnityTest]
        public IEnumerator Scene_MarkingSharesMarketLayout()
        {
            foreach (var d in new[] { 0.70f, 0.85f })
            {
                s.ApplyPlacement(new Vector3(0.6f, 0, -0.4f), 25, d);
                var marking = s.Marking;
                Assert.IsTrue(marking.IsChildOf(s.MarketRoot), "the marking is part of the market");
                foreach (var c in WareInfo.Categories)
                {
                    var footprint = Piece(marking, $"Footprint {WareInfo.Name(c)}");
                    Assert.AreEqual(0, Vector3.Distance(Flat(s.Stall(c).Root.position), Flat(footprint.position)), 0.01f, $"d {d}: the {c} outline is where the stall is");
                }
                var table = Piece(marking, "Footprint Lekbordet");
                Assert.AreEqual(0, Vector3.Distance(Flat(s.Play.Root.position), Flat(table.position)), 0.01f, $"d {d}: the play table outline");
                var area = Piece(marking, "Area");
                var need = SpaceFit.NeededSize(d);
                Assert.AreEqual(need.x, area.localScale.x, 0.01f, $"d {d}: the outlined area is the layout's needed area");
                Assert.AreEqual(need.y, area.localScale.z, 0.01f);
            }
            Assert.IsFalse(s.Marking.gameObject.activeInHierarchy, "hidden outside calibration: it is no promise of free floor");
            yield return null;
        }

        static Vector3 Flat(Vector3 p) => new(p.x, 0, p.z);

        [UnityTest]
        public IEnumerator Scene_CalibrationMovesWholeMarketOnConfirm()
        {
            var before = (s.MarketRoot.position, s.MarketRoot.rotation);
            var order = s.Shop.Order;
            Step();
            s.ShowPreview(new Vector3(1.0f, 0, 0.5f), 90, 0.85f, true);
            Wait(0.5f);
            Assert.AreEqual(before, (s.MarketRoot.position, s.MarketRoot.rotation), "a preview never moves the market");
            Assert.IsTrue(s.Preview.gameObject.activeInHierarchy);
            Assert.IsTrue(s.Marking.gameObject.activeInHierarchy, "the current marking shows next to the proposal");
            var ghostFruit = Piece(s.Preview, $"Footprint {WareInfo.Name(Category.FruitVeg)}").position;

            s.ApplyPlacement(new Vector3(1.0f, 0, 0.5f), 90, 0.85f);
            s.HidePreview();
            Step();
            Assert.AreEqual(0, Vector3.Distance(new Vector3(1.0f, s.MarketRoot.position.y, 0.5f), s.MarketRoot.position), 1e-4);
            Assert.AreEqual(90, s.MarketRoot.eulerAngles.y, 0.01f);
            Assert.AreEqual(0, Vector3.Distance(Flat(ghostFruit), Flat(s.Stall(Category.FruitVeg).Root.position)), 0.01f, "where the preview showed it");
            foreach (var c in WareInfo.Categories)
                Assert.AreEqual(0, Vector3.Distance(MarketLayout.Stall(c, 0.85f).position, s.ToMarket(s.Stall(c).Root.position)), 0.001f, $"{c} stall at the new spacing");
            Assert.AreEqual(0, Vector3.Distance(MarketLayout.PlayTableAt(0.85f).position, Flat(s.ToMarket(s.Play.Root.position))), 0.001f, "the play table too");
            Assert.IsTrue(s.Crocodile.Root.IsChildOf(s.Play.Root), "the crocodile shelf moves with the table");
            Assert.AreSame(order, s.Shop.Order, "the order is kept");
            Assert.IsFalse(s.Preview.gameObject.activeInHierarchy);
            Assert.IsFalse(s.Marking.gameObject.activeInHierarchy);
            Assert.AreEqual(StandView.CounterSize, StandView.CounterSize, "(sizes are constants: nothing scaled)");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_TooSmallKeepsPlacement()
        {
            var before = (s.MarketRoot.position, s.MarketRoot.rotation, MarketLayout.StallDistance);
            var c = new Calibration();
            c.Start(null, new Vector2(2, 2), 0);
            for (var i = 0; i < 10; i++) c.Adjust(CalibItem.Length, -1, Vector2.zero, 0);
            s.ShowPreview(new Vector3(2, 0, 2), 0, c.Fit.StallDistance, c.Fit.Fits);
            Step();
            Assert.IsFalse(c.Fit.Fits);
            Assert.AreEqual(Colors.PreviewBad, s.PreviewColor, "shown red: it doesn't fit");
            Assert.IsFalse(c.Confirm().HasValue);
            Assert.AreEqual(before, (s.MarketRoot.position, s.MarketRoot.rotation, MarketLayout.StallDistance), "the previous placement stays");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_SellAndPlayAfterCalibration()
        {
            s.ApplyPlacement(new Vector3(-0.5f, 0, 0.3f), -30, SpaceFit.MaxStall);
            Step();
            for (var i = 0; i < 600 && s.Shop.State != SaleState.Ordering; i++) Step(dt: 0.1f);
            var stall = s.ActiveStall;
            var order = s.Shop.Order;
            var till = s.Wallet.Balance;
            foreach (var w in WareInfo.All)
                for (var i = 0; i < order.Count(w); i++) PickAndDrop(s.CrateTop(w), stall.ToWorld(StandView.BasketCentre + Vector3.up * 0.12f), 0.2f);
            Wait(0.6f);
            PickAndDrop(s.Coin, stall.ToWorld(StandView.RegisterCentre + Vector3.up * 0.14f), 0.1f);
            Assert.AreEqual(till + order.Sum, s.Wallet.Balance, "selling works at the new spacing");

            var tooth = (s.Crocodile.Game.TriggerTooth + 1) % CrocodileGame.Teeth;
            var at = s.Crocodile.ToothWorld(tooth) + Vector3.up * 0.02f;
            Step(at);
            Step(at, true);
            Step(at, false);
            Assert.IsTrue(s.Crocodile.Game.IsPressed(tooth), "the crocodile too");

            // Stick walking stops at the moved fruit counter.
            var head = s.ToWorld(new Vector3(0, 1.1f, 0));
            for (var i = 0; i < 400; i++) head = s.ConstrainWalk(head, head + s.MarketRoot.forward * (Locomotion.WalkSpeed / 72f));
            var fruit = MarketCollision.Footprints[0];
            Assert.AreEqual(-(fruit.Half.y + MarketCollision.BodyRadius), fruit.ToLocal(s.ToMarket(head)).y, 0.01f, "walking stops at the counter where it now is");
            Assert.AreEqual(SpaceFit.MaxStall, fruit.Centre.z, 1e-4);
            yield return null;
        }
    }
}
