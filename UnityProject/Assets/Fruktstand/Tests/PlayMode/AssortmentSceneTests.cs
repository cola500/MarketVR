// EPIC-009 in the real scene: the fruit stall follows the Assortment. Names match docs/EPIC-009.md.
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
    public class AssortmentSceneTests
    {
        FruitStand s;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneLoad.Fresh(x => s = x, new[] { Ware.Pear, Ware.Banana, Ware.Grapes });
            s = Object.FindAnyObjectByType<FruitStand>();
            s.ReadDesktopInput = false;
            yield return null;
        }

        void Step(ShopFrame? frame = null, Vector3? right = null, bool grab = false)
        {
            var f = frame ?? new ShopFrame();
            f.DeltaTime = 0.02f;
            if (right.HasValue) f.Right = new HandFrame { Tracked = true, Position = right.Value, Rotation = Quaternion.identity, Grab = grab };
            s.Step(f);
        }

        // The stall, its crates, the crate-top items and the order all show exactly the assortment's fruit.
        void AssertStallShows(Ware[] fruit)
        {
            var stall = s.Stall(Category.FruitVeg);
            CollectionAssert.AreEqual(fruit, stall.Wares);
            var bins = stall.Top.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Bin ")).Select(t => t.name).ToArray();
            CollectionAssert.AreEquivalent(fruit.Select(w => "Bin " + WareInfo.Name(w)), bins, "one crate per fruit on sale, nothing old left");
            foreach (var w in WareInfo.Catalogue(Category.FruitVeg))
            {
                var top = s.CrateTop(w);
                if (System.Array.IndexOf(fruit, w) >= 0)
                {
                    Assert.IsNotNull(top, w + " can be taken");
                    Assert.AreEqual(ItemPlace.Crate, top.Place);
                    Assert.Less(Vector3.Distance(top.Transform.localPosition, StandView.CrateTop(System.Array.IndexOf(fruit, w), w)), 1e-3f, w + " lies on its own crate");
                }
                else Assert.IsNull(top, w + " is not on sale");
            }
            foreach (var i in s.Items)
                if (i.IsWare && i.Category == Category.FruitVeg) CollectionAssert.Contains(fruit, i.Ware, "no fruit left that is not on sale");
        }

        [UnityTest]
        public IEnumerator Scene_AssortmentChangeRebuildsFruitStall()
        {
            AssertStallShows(new[] { Ware.Pear, Ware.Banana, Ware.Grapes });
            Assert.AreEqual(Order.Of(Ware.Pear), s.Shop.Order, "customer 1 wants the first fruit on sale");

            // Holding a banana when the fruit change: it leaves the hand, nothing old is left.
            var banana = s.CrateTop(Ware.Banana);
            Step(right: banana.Position);
            Step(right: banana.Position, grab: true);
            Assert.AreSame(banana, s.Held(1));
            for (var i = 0; i < 20 && s.Market.Assortment.IsActive(Ware.Banana); i++) s.Market.Assortment.Redraw();
            Assert.IsFalse(s.Market.Assortment.IsActive(Ware.Banana));
            Step(right: banana.Position, grab: true);
            Assert.IsNull(s.Held(1), "the banana is gone from the hand");
            AssertStallShows(s.Market.Assortment.Active(Category.FruitVeg));

            // Nytt spel: new fruit at once, and the first customer wants the new first fruit.
            var before = s.Market.Assortment.Active(Category.FruitVeg);
            Step(new ShopFrame { NewGame = true });
            var after = s.Market.Assortment.Active(Category.FruitVeg);
            CollectionAssert.AreNotEquivalent(before, after);
            AssertStallShows(after);
            Assert.AreEqual(Order.Of(after[0]), s.Shop.Order);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_RecentreKeepsAssortment()
        {
            var fruit = s.Market.Assortment.Active(Category.FruitVeg);
            var revision = s.Market.Assortment.Revision;
            s.PlaceInFront(new Vector3(0.3f, 1.1f, -0.2f), new Vector3(1, 0, 1));
            Step(new ShopFrame { CounterHeightDelta = 0.05f });
            for (var i = 0; i < 30; i++) Step(new ShopFrame { ReleaseAll = true }); // menu / focus loss
            Step(new ShopFrame { ClearBasket = true });
            Step(new ShopFrame { ToggleOpen = true });
            Step(new ShopFrame { ToggleOpen = true });
            Assert.AreEqual(revision, s.Market.Assortment.Revision, "recentre, height, pause, Töm korgen and open/closed keep the fruit");
            AssertStallShows(fruit);
            yield return null;
        }
    }
}
