// EPIC-017 in the real scene, with the game's own grab and release (simulated hands): ball passes with the child,
// a missed toss, a toss while walking, and building together. Names match docs/EPIC-017.md.
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
    public class SharedPlaySceneTests
    {
        const float Dt = 1 / 72f;
        FruitStand s;

        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return SceneLoad.Fresh(x => s = x);
            Assert.IsNotNull(s);
        }

        void Step(Vector3? right = null, bool grab = false, float dt = Dt, Matrix4x4? rig = null)
        {
            var f = new ShopFrame { DeltaTime = dt, RigDelta = rig };
            if (right.HasValue) f.Right = new HandFrame { Tracked = true, Position = right.Value, Rotation = Quaternion.identity, Grab = grab };
            s.Step(f);
        }

        void Wait(float seconds)
        {
            for (var t = 0f; t < seconds; t += 0.05f) Step(dt: 0.05f);
        }

        bool WaitUntil(System.Func<bool> condition, float seconds, System.Action each = null)
        {
            for (var t = 0f; t < seconds; t += 0.05f)
            {
                if (condition()) return true;
                Step(dt: 0.05f);
                each?.Invoke();
            }
            return condition();
        }

        void SellToyAndWaitForPlay(Ware toy)
        {
            foreach (var t in Assortment.ClassicOf(Category.Toys)) if (PlayArea.SpotOf(t) != PlayArea.SpotOf(toy)) Assert.IsTrue(s.PlayArea.TryReserve(t, 99));
            for (var k = 0; k < 10; k++)
            {
                Assert.IsTrue(WaitUntil(() => s.Shop.State == SaleState.Ordering, 60), "a customer is ordering");
                var order = s.Shop.Order;
                foreach (var w in WareInfo.All)
                    for (var i = 0; i < order.Count(w); i++) s.Step(new ShopFrame { DeltaTime = 0.02f, AddDirect = w });
                Wait(0.6f);
                Assert.IsTrue(s.RegisterCoin(), $"paid for {order}");
                if (order.Toy == toy) break;
                Wait(Shop.ThankSeconds + Shop.LeaveSeconds);
            }
            Assert.IsTrue(WaitUntil(() => s.PlayArea.State(toy) == SpotState.Playing, PlayArea.WalkSeconds + 6), $"the {toy} child plays");
        }

        Vector3 Local(Item i) => s.Play.ToTable(i.Position);

        void Take(Item item)
        {
            Step(item.Position);
            Assert.AreSame(item, s.Hover(1), $"hand next to {item.Transform.name} highlights it");
            Step(item.Position, true);
            Assert.AreSame(item, s.Held(1));
        }

        // The hand moves (held) to a table-space point in `seconds`, optionally while the rig walks.
        Vector3 Move(Vector3 from, Vector3 toTable, float seconds, Matrix4x4? walk = null)
        {
            var to = s.Play.ToWorld(toTable);
            var n = Mathf.Max(1, Mathf.RoundToInt(seconds / Dt));
            var carried = Vector3.zero; // where the rig has carried the hand so far
            for (var i = 1; i <= n; i++)
            {
                if (walk is Matrix4x4 w) carried = w.MultiplyPoint3x4(carried);
                Step(Vector3.Lerp(from, to, i / (float)n) + carried, true, Dt, walk);
            }
            return to + carried;
        }

        // ---------------- Ball ----------------

        Item Ball => s.Play.Toys(Ware.Ball).First();
        bool BallAtPlayer => Local(Ball).z < 0 && Local(Ball).y > -0.02f && Ball.Body.linearVelocity.magnitude < 0.05f && !Ball.Moving
                             && s.PlayArea.Control(Ware.Ball) == ToyControl.Child;

        // The seated toss: lift the ball about 15 cm, then a short push (6 cm forward, 3 cm up in 0.1 s), let go.
        void Toss(Vector3 direction, float push = 0.06f, Matrix4x4? walk = null)
        {
            Take(Ball);
            var at = Move(Ball.Position, Local(Ball) + Vector3.up * 0.15f, 0.3f);
            direction.y = 0;
            at = Move(at, s.Play.ToTable(at) + direction.normalized * push + Vector3.up * 0.03f, 0.1f, walk);
            if (walk is Matrix4x4 w) at = w.MultiplyPoint3x4(at); // the rig carries the hand on in the release frame too
            Step(at, false, Dt, walk);
            Assert.IsNull(s.Held(1), "let go");
        }

        Vector3 TowardsChild => PlayTableView.CatchPoint - Local(Ball);

        [UnityTest]
        public IEnumerator Scene_ThreeBallPassesInARow()
        {
            SellToyAndWaitForPlay(Ware.Ball);
            Assert.IsTrue(WaitUntil(() => BallAtPlayer, 10), "the child rolls the ball to the player first");
            for (var pass = 1; pass <= 3; pass++)
            {
                Toss(TowardsChild);
                var lowest = float.MaxValue;
                void Track() => lowest = Mathf.Min(lowest, Local(Ball).y);
                Assert.IsTrue(WaitUntil(() => s.Play.BallCatches == pass, 1.5f, Track), $"pass {pass}: the child catches it ({Local(Ball):F2}, {s.PlayArea.Control(Ware.Ball)})");
                var back = 0f;
                Assert.IsTrue(WaitUntil(() => BallAtPlayer, 8, () => { Track(); back += 0.05f; }), $"pass {pass}: and rolls it back ({Local(Ball):F2})");
                Assert.Greater(lowest, -0.02f, $"pass {pass}: the ball stayed on the table");
                Debug.Log($"[test] pass {pass}: back at the player {back:F1} s after the catch");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_MissedTossCarriesOn()
        {
            SellToyAndWaitForPlay(Ware.Ball);
            Assert.IsTrue(WaitUntil(() => BallAtPlayer, 10));
            Toss(new Vector3(-1, 0, 0.25f), push: 0.12f); // a big push to the left, past the end of the table
            Wait(1.5f);
            Assert.Less(Local(Ball).y, -PlayTableView.LostBelow, $"missed: on the floor ({Local(Ball):F2})");
            Assert.AreEqual(0, s.Play.BallCatches);
            Assert.IsTrue(WaitUntil(() => BallAtPlayer, PlayTableView.LostSeconds + 8), $"back with the child, who rolls it to the player ({Local(Ball):F2})");
            Toss(TowardsChild);
            Assert.IsTrue(WaitUntil(() => s.Play.BallCatches == 1, 1.5f), "the next toss is caught");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_TossWhileWalkingSameToss()
        {
            SellToyAndWaitForPlay(Ware.Ball);
            Assert.IsTrue(WaitUntil(() => BallAtPlayer, 10));
            // ~80° left of straight ahead: clearly a wide toss (its speed follows the hand) from anywhere the stick may
            // carry the hand in 0.1 s. Near the aim cone's edge, the walk alone could move the release onto the child's
            // line and turn it into a toss to the child (5 whys in EPIC-017).
            var aside = new Vector3(-1, 0, 0.18f);
            Toss(aside);
            var still = Ball.Body.linearVelocity;
            Assert.IsTrue(WaitUntil(() => BallAtPlayer, PlayTableView.LostSeconds + 10), "back to the player");
            var walk = Matrix4x4.Translate(s.Play.Top.right * (Locomotion.WalkSpeed * Dt)); // the stick walks sideways
            Toss(aside, walk: walk);
            var walking = Ball.Body.linearVelocity;
            Debug.Log($"[test] toss standing {still:F3}, walking {walking:F3}");
            Assert.Greater(still.magnitude, 0.5f, "a toss");
            Assert.Less(Vector3.Distance(still, walking), 0.02f, "walking with the stick adds no speed");
            yield return null;
        }

        // ---------------- Blocks ----------------

        IEnumerable<Item> Blocks => s.Play.Toys(Ware.Blocks);
        IEnumerable<Item> PlayersBlocks => Blocks.Where(b => !PlayTableView.IsChildsBlock(b.Index));
        Item PlayersLoose => PlayersBlocks.FirstOrDefault(b => b.Slot < 0 && b.Place == ItemPlace.Play && !b.Moving);
        BuildSite Site => s.Play.Build;

        // The player puts a block near a cell (as a hand would, a little off).
        void PlaceNear(Item block, int cell, Vector3 off)
        {
            Take(block);
            var at = Move(block.Position, Site.Centre(cell) + off, 0.4f);
            Step(at, false);
            Assert.AreEqual(cell, block.Slot, $"{block.Transform.name} went into cell {cell} ({Local(block):F3})");
        }

        [UnityTest]
        public IEnumerator Scene_ChildLaysOneAndLeavesRoom()
        {
            SellToyAndWaitForPlay(Ware.Blocks);
            var start = PlayersBlocks.ToDictionary(b => b, Local);
            Assert.AreEqual(3, start.Count, "three blocks are the player's");
            Assert.IsTrue(start.Values.All(p => p.z < -0.1f), "in the row nearest the player");
            Assert.IsTrue(WaitUntil(() => Site.Count == 1, 4), "the child lays one");
            Assert.IsTrue(PlayTableView.IsChildsBlock(Blocks.First(b => b.Slot >= 0).Index), "one of its own");
            Wait(5f);
            Assert.AreEqual(1, Site.Count, "and leaves room for the player");
            Assert.AreEqual(BuildSite.Turn.Player, Site.Whose);
            var mine = PlayersLoose;
            PlaceNear(mine, BuildSite.CellOf(0, 0), new Vector3(0.02f, 0.05f, -0.02f)); // beside the child's: wider
            var placed = Local(mine);
            Assert.IsTrue(WaitUntil(() => Site.Count == 3, BuildSite.ChildBeatSeconds + 2), "the child lays the next after a beat");
            foreach (var b in PlayersBlocks.Where(b => b != mine))
                Assert.Less(Vector3.Distance(Local(b), start[b]), 0.002f, $"the child never moved the player's {b.Transform.name}");
            Assert.Less(Vector3.Distance(Local(mine), placed), 0.002f, "nor the one the player built with");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_SharedBuildStands()
        {
            SellToyAndWaitForPlay(Ware.Blocks);
            // Child, player, child, player, child, player: the player goes higher on the child's block, then wider.
            var plan = new[] { BuildSite.CellOf(1, 1), BuildSite.CellOf(0, 0), BuildSite.CellOf(0, 1) };
            var offs = new[] { new Vector3(0.015f, 0.04f, 0.01f), new Vector3(0.02f, 0.03f, -0.02f), new Vector3(-0.01f, 0.05f, 0.015f) };
            for (var k = 0; k < 3; k++)
            {
                var n = Site.Count;
                Assert.IsTrue(WaitUntil(() => Site.Count == n + 1 && Blocks.All(b => !b.Moving), BuildSite.PlayerTurnSeconds), $"turn {k}: the child lays one");
                var cell = plan[k];
                if (Site.Occupied(cell) || !Site.Stable(cell)) cell = Enumerable.Range(0, BuildSite.Cells).First(c => !Site.Occupied(c) && Site.Stable(c) && c >= 3);
                PlaceNear(PlayersLoose, cell, offs[k]);
            }
            Assert.AreEqual(6, Site.Count, "six blocks built together");
            var used = Blocks.Where(b => b.Slot >= 0).ToList();
            Assert.GreaterOrEqual(used.Select(b => BuildSite.ColumnOf(b.Slot)).Distinct().Count(), 2, "wider");
            Assert.GreaterOrEqual(Site.LayersBuilt, 2, "and higher");
            var before = used.ToDictionary(b => b, Local);
            Wait(5f); // 360 physics steps
            foreach (var b in used)
            {
                Assert.Less(Vector3.Distance(Local(b), before[b]), 0.001f, $"{b.Transform.name} stands where it was built");
                Assert.Less(Vector3.Distance(Local(b), Site.Centre(b.Slot)), 0.001f, "in its cell");
                Assert.IsTrue(b.Body.isKinematic, "fixed in the build");
            }
            for (var i = 0; i < used.Count; i++)
            for (var j = i + 1; j < used.Count; j++)
                Assert.Greater(Vector3.Distance(Local(used[i]), Local(used[j])), Models.BlockSize * 0.95f, "no two blocks in one place");
            yield return null;
        }
    }
}
