// EPIC-011 in the real scene: sloppy releases of toys are forgiven; nothing vanishes, falls through or is shot away.
// Names match docs/EPIC-011.md. Every check runs over many physics steps, not one frame.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fruktstand.Core;
using Fruktstand.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fruktstand.Tests
{
    public class ForgivingPlaySceneTests
    {
        const float S = Models.BlockSize;
        FruitStand s;

        IEnumerator Load(Ware[] wares = null)
        {
            yield return SceneLoad.Fresh(x => s = x, wares);
            Assert.IsNotNull(s);
        }

        void Step(Vector3? right = null, bool grab = false, float dt = 0.02f)
        {
            var f = new ShopFrame { DeltaTime = dt };
            if (right.HasValue) f.Right = new HandFrame { Tracked = true, Position = right.Value, Rotation = Quaternion.identity, Grab = grab };
            s.Step(f);
        }

        void Wait(float seconds)
        {
            for (var t = 0f; t < seconds; t += 0.02f) Step();
        }

        bool WaitUntil(System.Func<bool> condition, float seconds)
        {
            for (var t = 0f; t < seconds; t += 0.05f)
            {
                if (condition()) return true;
                Step(dt: 0.05f);
            }
            return condition();
        }

        void Serve()
        {
            Assert.IsTrue(WaitUntil(() => s.Shop.State == SaleState.Ordering, 60));
            var order = s.Shop.Order;
            foreach (var w in WareInfo.All)
                for (var i = 0; i < order.Count(w); i++) s.Step(new ShopFrame { DeltaTime = 0.02f, AddDirect = w });
            Wait(0.6f);
            Assert.IsTrue(s.RegisterCoin());
        }

        void SellToyAndWaitForPlay(Ware toy)
        {
            var placeholders = s.Market.Assortment.Active(Category.Toys)
                .Where(t => PlayArea.SpotOf(t) != PlayArea.SpotOf(toy) && s.PlayArea.TryReserve(t, 99)).ToList();
            for (var k = 0; k < 10; k++)
            {
                Assert.IsTrue(WaitUntil(() => s.Shop.State == SaleState.Ordering, 60));
                if (s.Shop.Order.Toy == toy) break;
                Serve();
                Wait(Shop.ThankSeconds + Shop.LeaveSeconds);
            }
            Assert.AreEqual(toy, s.Shop.Order.Toy);
            Serve();
            foreach (var t in placeholders) s.PlayArea.CancelReservation(t);
            Assert.IsTrue(WaitUntil(() => s.PlayArea.State(toy) == SpotState.Playing, PlayArea.WalkSeconds + 6));
            Wait(0.3f);
        }

        Vector3 Local(Item i) => s.Play.ToTable(i.Position);

        void Take(Item item)
        {
            Step(item.Position);
            Step(item.Position, true);
            Assert.AreSame(item, s.Held(1), $"holding {item.Transform.name}");
        }

        // Moves the held item to `world` and lets go there, with a calm hand (no throw).
        void ReleaseAt(Vector3 world)
        {
            var from = s.Held(1).Position;
            for (var k = 1; k <= 10; k++) Step(Vector3.Lerp(from, world, k / 10f), true);
            Step(world, true);
            Step(world, false);
        }

        static bool OnTable(Vector3 local) =>
            local.y > -0.005f && Mathf.Abs(local.x) < PlayTableView.TableSize.x / 2 && Mathf.Abs(local.z) < PlayTableView.TableSize.z / 2;

        void AssertBlocksSound(string when)
        {
            var blocks = s.Play.Toys(Ware.Blocks).ToList();
            Assert.AreEqual(WareInfo.BlocksInSet, blocks.Count(b => b.Place != ItemPlace.Gone), $"{when}: all six blocks are there");
            foreach (var b in blocks)
            {
                if (b.Place == ItemPlace.Held) continue;
                Assert.IsTrue(OnTable(Local(b)), $"{when}: {b.Transform.name} on the table ({Local(b)})");
                if (b.Body != null && !b.Body.isKinematic) Assert.Less(b.Body.linearVelocity.magnitude, 0.5f, $"{when}: {b.Transform.name} not shot away");
            }
            for (var i = 0; i < blocks.Count; i++)
            for (var j = i + 1; j < blocks.Count; j++)
            {
                var d = Local(blocks[i]) - Local(blocks[j]);
                var overlap = Mathf.Abs(d.x) < S * 0.85f && Mathf.Abs(d.y) < S * 0.85f && Mathf.Abs(d.z) < S * 0.85f;
                Assert.IsFalse(overlap, $"{when}: {blocks[i].Transform.name} and {blocks[j].Transform.name} overlap");
            }
        }

        // A block on the table that is not in the tower, not moving and not held.
        Item LooseBlock(Item except = null) => s.Play.Toys(Ware.Blocks)
            .FirstOrDefault(b => b != except && b.Slot < 0 && b.Place == ItemPlace.Play && !b.Moving && OnTable(Local(b)));

        [UnityTest]
        public IEnumerator Scene_BlockReleasedInsideAnotherLandsOnTop()
        {
            yield return Load();
            SellToyAndWaitForPlay(Ware.Blocks);
            var held = LooseBlock();
            Take(held);
            var target = LooseBlock(held);
            Assert.IsNotNull(target);
            var targetAt = Local(target);
            ReleaseAt(s.Play.ToWorld(targetAt + new Vector3(0.01f, 0.01f, 0.008f))); // deep inside the other block
            Wait(0.3f); // the child waits until everything lies still; physics runs
            var p = Local(held);
            Assert.AreEqual(targetAt.y + S, p.y, 0.012f, $"on top of the other block ({p} over {targetAt})");
            Assert.Less(new Vector2(p.x - targetAt.x, p.z - targetAt.z).magnitude, 0.015f, $"straight on top: held {p}, target was {targetAt} and is {Local(target)}");
            Assert.Less(Vector3.Distance(Local(target), targetAt), 0.005f, "the other block was not moved");
            AssertBlocksSound("just after");
            Wait(3f); // many physics steps (and the child may build on)
            AssertBlocksSound("3 s later");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_BlockReleasedInsideTableLandsOnIt()
        {
            yield return Load();
            SellToyAndWaitForPlay(Ware.Blocks);
            var held = LooseBlock();
            Take(held);
            ReleaseAt(s.Play.ToWorld(new Vector3(-0.1f, 0.004f, 0.06f))); // more than half inside the table top
            Wait(0.3f);
            Assert.AreEqual(S / 2, Local(held).y, 0.01f, "resting on the table");
            Assert.Less(new Vector2(Local(held).x + 0.1f, Local(held).z - 0.06f).magnitude, 0.03f, "where it was let go");
            Wait(2f);
            AssertBlocksSound("2 s later");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_LostBlockReturnsWhileTowerStands()
        {
            yield return Load();
            SellToyAndWaitForPlay(Ware.Blocks);
            Assert.IsTrue(WaitUntil(() => s.Play.Build.Count >= 1, 8), "the child has started the build");
            var held = LooseBlock();
            Take(held);
            ReleaseAt(s.Play.ToWorld(new Vector3(0f, 0.1f, -0.6f))); // beside the table: it falls to the floor
            // The left hand holds another block meanwhile, so the child waits and only the lost-toy rule can help.
            var other = LooseBlock(held);
            var left = other.Position;
            HandFrame Left(bool grab) => new() { Tracked = true, Position = left, Rotation = Quaternion.identity, Grab = grab };
            s.Step(new ShopFrame { DeltaTime = 0.02f, Left = Left(false) });
            s.Step(new ShopFrame { DeltaTime = 0.02f, Left = Left(true) });
            Assert.AreSame(other, s.Held(0), "the left hand holds another block");
            var back = false;
            for (var t = 0f; t < PlayTableView.LostSeconds + 4 && !back; t += 0.05f)
            {
                s.Step(new ShopFrame { DeltaTime = 0.05f, Left = Left(true) });
                back = OnTable(Local(held)) && !held.Moving;
            }
            Assert.IsTrue(back, $"it comes back although the tower has fixed blocks and a block is held ({Local(held)})");
            Assert.AreSame(other, s.Held(0), "nothing was taken from the hand");
            s.Step(new ShopFrame { DeltaTime = 0.02f, Left = Left(false) });
            Wait(1f);
            AssertBlocksSound("after coming back");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_BlockNearTheEdgeStaysOn()
        {
            yield return Load();
            SellToyAndWaitForPlay(Ware.Blocks);
            var held = LooseBlock();
            Take(held);
            ReleaseAt(s.Play.ToWorld(new Vector3(0.49f, 0.035f, -0.02f))); // half over the rim at the end of the table
            Wait(2f);
            Assert.IsTrue(OnTable(Local(held)), $"on the table ({Local(held)})");
            Assert.Greater(Local(held).x, 0.38f, "helped in only a little, near where it was let go");
            AssertBlocksSound("2 s later");
            yield return null;
        }

        // A toy is not grabbable during the child's short move (0.5 s; ItemPlace.Play => !Moving), so the child and the
        // player never steer it at once. Taking from the child's tower when it stands:
        [UnityTest]
        public IEnumerator Scene_TakeBlockFromTheChildsTower()
        {
            yield return Load();
            SellToyAndWaitForPlay(Ware.Blocks);
            Assert.IsTrue(WaitUntil(() => s.Play.Build.Count >= 2 && s.Play.Toys(Ware.Blocks).All(b => !b.Moving), BuildSite.ChildBeatSeconds + BuildSite.PlayerTurnSeconds + 2), "two blocks built (EPIC-017: the child waits for the player's turn)");
            var topBlock = s.Play.Toys(Ware.Blocks).Where(b => b.Slot >= 0).OrderBy(b => b.Slot).Last();
            Take(topBlock);
            Assert.AreEqual(ToyControl.Player, s.PlayArea.Control(Ware.Blocks), "the child lets go and waits");
            Assert.AreEqual(-1, topBlock.Slot, "no longer part of the tower");
            ReleaseAt(s.Play.ToWorld(new Vector3(-0.05f, 0.08f, -0.1f)));
            Assert.IsTrue(WaitUntil(() => s.PlayArea.Control(Ware.Blocks) == ToyControl.Child, 6), "the child takes over again");
            Wait(3f);
            AssertBlocksSound("after the child built on");
            foreach (var b in s.Play.Toys(Ware.Blocks).Where(b => b.Slot >= 0 && !b.Moving))
                Assert.Less(Vector3.Distance(Local(b), s.Play.Build.Centre(b.Slot)), 0.01f, "built blocks are in their cells");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_HeightChangeKeepsLooseToys()
        {
            yield return Load();
            SellToyAndWaitForPlay(Ware.Blocks);
            Wait(3f); // some in the tower, some loose
            var before = s.Play.Toys(Ware.Blocks).ToDictionary(b => b, Local);
            s.Step(new ShopFrame { DeltaTime = 0.02f, CounterHeightDelta = 0.1f });
            s.Step(new ShopFrame { DeltaTime = 0.02f, CounterHeightDelta = -0.05f });
            Wait(1f);
            AssertBlocksSound("after raising and lowering the table");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_EveryToySurvivesASloppyRelease()
        {
            foreach (var set in new[] { Assortment.ClassicOf(Category.Toys), new[] { Ware.Top, Ware.Train, Ware.StackingRings } })
            {
                yield return Load(set);
                foreach (var toy in set)
                {
                    if (toy == Ware.Blocks) continue; // its own tests above
                    SellToyAndWaitForPlay(toy);
                    var item = s.Play.Toys(toy).First(t => t.Place == ItemPlace.Play && !t.Moving && (toy != Ware.StackingRings || t.Slot < 0 || true));
                    Take(item);
                    var at = new Vector3(PlayArea.SpotOf(toy) == 0 ? -0.3f : PlayArea.SpotOf(toy) == 1 ? 0.0f : 0.2f, 0.0f, -0.12f); // half inside the table
                    ReleaseAt(s.Play.ToWorld(at));
                    for (var t = 0f; t < 2f; t += 0.02f)
                    {
                        Step();
                        if (item.Body != null && !item.Body.isKinematic) Assert.Less(item.Body.linearVelocity.magnitude, PlayTableView.MaxThrowSpeed + 0.1f, $"{toy} is not shot away");
                    }
                    Assert.AreNotEqual(ItemPlace.Gone, item.Place, $"{toy} is still there");
                    Assert.IsTrue(OnTable(Local(item)), $"{toy} is on the table ({Local(item)})");
                    Assert.Greater(Local(item).y, 0.005f, $"{toy} rests on the table top, not in it");
                }
            }
        }

        [UnityTest]
        public IEnumerator Scene_FastReleaseIsCapped()
        {
            yield return Load();
            SellToyAndWaitForPlay(Ware.Ball);
            var ball = s.Play.Toys(Ware.Ball).First();
            Take(ball);
            // A quick low swipe just above the table, let go mid-swipe.
            var p = s.Play.ToWorld(new Vector3(-0.3f, 0.06f, 0.1f));
            var dir = s.Play.Top.TransformDirection(Vector3.back);
            for (var k = 0; k < 5; k++)
            {
                p += dir * 0.08f; // 4 m/s
                Step(p, true);
            }
            Step(p, false);
            Step(dt: 0.005f);
            Assert.LessOrEqual(ball.Body.linearVelocity.magnitude, PlayTableView.MaxThrowSpeed + 0.05f, "release speed capped");
            Assert.Greater(ball.Body.linearVelocity.magnitude, 0.3f, "but it really rolls");
            Wait(3f);
            Assert.AreNotEqual(ItemPlace.Gone, ball.Place);
            yield return null;
        }
    }
}
