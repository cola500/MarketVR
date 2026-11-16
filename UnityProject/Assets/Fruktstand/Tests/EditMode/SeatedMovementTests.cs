// EPIC-005 (seated play): left stick walks, right stick turns or changes the counter height, furniture stops stick
// movement. Names match the [T: ...] references in docs/EPIC-005.md.
using System.Collections.Generic;
using Fruktstand.Core;
using Fruktstand.Game;
using NUnit.Framework;
using UnityEngine;

namespace Fruktstand.Tests
{
    public class SeatedMovementTests
    {
        static XrState Sticks(Vector2 left, Vector2 right)
        {
            var s = new XrState();
            s.Left.Tracked = s.Right.Tracked = true;
            s.Left.Rotation = s.Right.Rotation = Quaternion.identity;
            s.Left.Stick = left;
            s.Right.Stick = right;
            return s;
        }

        static XrInputRouter ReadyRouter()
        {
            var r = new XrInputRouter();
            r.Route(Sticks(Vector2.zero, Vector2.zero), 0.01f, out _);
            return r;
        }

        static ShopFrame Route(XrInputRouter r, Vector2 left, Vector2 right, float dt = 0.1f) => r.Route(Sticks(left, right), dt, out _);

        // ---------- router ----------

        [Test]
        public void Router_LeftStickOnlyWalks()
        {
            var r = ReadyRouter();
            foreach (var stick in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right, new Vector2(0.7f, 0.7f) })
            {
                var f = Route(r, stick, Vector2.zero);
                Assert.AreEqual(0, f.CounterHeightDelta, $"left {stick} never changes the height");
                Assert.AreEqual(0, f.Turn, $"left {stick} never turns");
                Assert.Greater(f.Walk.magnitude, 0.9f, $"left {stick} walks");
                Route(r, Vector2.zero, Vector2.zero);
            }
            Assert.AreEqual(Vector2.zero, Route(r, new Vector2(0.15f, 0.1f), Vector2.zero).Walk, "dead zone");
            Assert.AreEqual(1f, Route(r, new Vector2(1, 1), Vector2.zero).Walk.magnitude, 1e-4, "a diagonal is no longer than straight");
            Assert.AreEqual(Vector2.zero, Route(r, Vector2.zero, Vector2.up).Walk, "the right stick never walks");
        }

        [Test]
        public void Router_RightStickOneTurnPerFlick()
        {
            var r = ReadyRouter();
            Assert.AreEqual(30f, XrInputRouter.TurnDegrees);
            var right = new Vector2(0.9f, 0.1f);
            Assert.AreEqual(1, Route(r, Vector2.zero, right).Turn, "flick right");
            for (var i = 0; i < 20; i++) Assert.AreEqual(0, Route(r, Vector2.zero, right).Turn, "held: still one step");
            Assert.AreEqual(0, Route(r, Vector2.zero, new Vector2(0.4f, 0)).Turn, "not back near the middle yet");
            Route(r, Vector2.zero, Vector2.zero);
            Assert.AreEqual(-1, Route(r, Vector2.zero, new Vector2(-1, 0)).Turn, "flick left after neutral");
            Route(r, Vector2.zero, Vector2.zero);
            Assert.AreEqual(0, Route(r, Vector2.zero, new Vector2(0.5f, 0)).Turn, "a small nudge does not turn");
        }

        [Test]
        public void Router_RightStickAxisLock()
        {
            var r = ReadyRouter();
            var up = Route(r, Vector2.zero, new Vector2(0.1f, 1));
            Assert.Greater(up.CounterHeightDelta, 0, "up raises");
            Assert.AreEqual(XrInputRouter.CounterSpeed * 0.1f, up.CounterHeightDelta, 0.01f, "full stick = CounterSpeed");
            var drift = Route(r, Vector2.zero, new Vector2(0.95f, 0.3f)); // the thumb slides sideways mid-flick
            Assert.AreEqual(0, drift.Turn, "locked to height: a sideways slide never turns");
            Route(r, Vector2.zero, Vector2.zero);
            Assert.Less(Route(r, Vector2.zero, new Vector2(0, -1)).CounterHeightDelta, 0, "down lowers");
            Route(r, Vector2.zero, Vector2.zero);

            var turn = Route(r, Vector2.zero, new Vector2(1, 0.1f));
            Assert.AreEqual(1, turn.Turn);
            Assert.AreEqual(0, Route(r, Vector2.zero, new Vector2(0.3f, 1)).CounterHeightDelta, "locked to turning: sliding up never changes height");
            Route(r, Vector2.zero, Vector2.zero);

            var diagonal = Route(r, Vector2.zero, new Vector2(0.75f, 0.7f));
            Assert.IsFalse(diagonal.Turn != 0 && diagonal.CounterHeightDelta != 0, "a diagonal does one or the other, never both");
            for (var i = 0; i < 10; i++)
            {
                var f = Route(r, Vector2.zero, new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)));
                Assert.IsFalse(f.Turn != 0 && f.CounterHeightDelta != 0);
            }
            Assert.AreEqual(0, Route(r, Vector2.zero, new Vector2(0, 0.25f)).CounterHeightDelta, "dead zone");
        }

        [Test]
        public void Router_SticksBlockedByMenuFocusAndNeedNeutral()
        {
            var walk = Vector2.up;
            var turn = new Vector2(1, 0);
            var height = new Vector2(0, 1);

            // Game menu
            var r = ReadyRouter();
            var menu = Sticks(Vector2.zero, Vector2.zero);
            menu.Left.Menu = true;
            r.Route(menu, 0.01f, out _);
            Assert.IsTrue(r.Menu.IsOpen);
            Route(r, Vector2.zero, Vector2.zero);
            foreach (var (left, right) in new[] { (walk, Vector2.zero), (Vector2.zero, turn), (Vector2.zero, height) })
            {
                var f = Route(r, left, right);
                Assert.AreEqual(Vector2.zero, f.Walk, "menu open: no walking");
                Assert.AreEqual(0, f.Turn, "menu open: no turning");
                Assert.AreEqual(0, f.CounterHeightDelta, "menu open: no height");
            }
            r.Route(menu, 0.01f, out _); // close it
            Assert.IsFalse(r.Menu.IsOpen);
            var held = Route(r, walk, turn);
            Assert.AreEqual(Vector2.zero, held.Walk, "sticks still pushed after the menu: nothing");
            Assert.AreEqual(0, held.Turn);
            Route(r, Vector2.zero, Vector2.zero);
            Assert.AreNotEqual(Vector2.zero, Route(r, walk, Vector2.zero).Walk, "after neutral: walking again");
            Assert.AreEqual(1, Route(r, Vector2.zero, turn).Turn, "and turning");

            // System menu (focus), pause, headset off
            foreach (var block in new System.Action<XrInputRouter, bool>[] { (x, v) => x.SetFocus(v), (x, v) => x.SetPaused(!v), (x, v) => x.SetPresent(v) })
            {
                var q = ReadyRouter();
                block(q, false);
                var f = Route(q, walk, height);
                Assert.AreEqual(Vector2.zero, f.Walk);
                Assert.AreEqual(0, f.CounterHeightDelta);
                Assert.AreEqual(0, Route(q, Vector2.zero, turn).Turn);
                block(q, true);
                f = Route(q, walk, turn);
                Assert.AreEqual(Vector2.zero, f.Walk, "held through the interruption: nothing");
                Assert.AreEqual(0, f.Turn);
                Assert.AreEqual(0, Route(q, Vector2.zero, height).CounterHeightDelta);
                Route(q, Vector2.zero, Vector2.zero);
                Assert.AreNotEqual(Vector2.zero, Route(q, walk, Vector2.zero).Walk);
            }
        }

        // ---------- walking ----------

        static Quaternion Head(float yaw, float pitch = 0, float roll = 0) => Quaternion.Euler(pitch, yaw, roll);

        [Test]
        public void Walk_FollowsHeadYaw()
        {
            foreach (var yaw in new[] { 0f, 45f, 90f, 180f, -120f })
            {
                var forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                var rightDir = Quaternion.Euler(0, yaw, 0) * Vector3.right;
                var d = Locomotion.Displacement(Vector2.up, Head(yaw), 1);
                Assert.AreEqual(0, Vector3.Distance(forward * Locomotion.WalkSpeed, d), 1e-4, $"forward at yaw {yaw}");
                Assert.AreEqual(0, Vector3.Distance(-forward * Locomotion.WalkSpeed, Locomotion.Displacement(Vector2.down, Head(yaw), 1)), 1e-4, "back");
                Assert.AreEqual(0, Vector3.Distance(rightDir * Locomotion.WalkSpeed, Locomotion.Displacement(Vector2.right, Head(yaw), 1)), 1e-4, "sideways right");
                Assert.AreEqual(0, Vector3.Distance(-rightDir * Locomotion.WalkSpeed, Locomotion.Displacement(Vector2.left, Head(yaw), 1)), 1e-4, "sideways left");
            }
        }

        [Test]
        public void Walk_DiagonalSameMaxSpeed()
        {
            Assert.AreEqual(0.5f, Locomotion.WalkSpeed, 1e-4);
            var straight = Locomotion.Displacement(XrInputRouter.WalkInput(new Vector2(0, 1)), Head(30), 1).magnitude;
            var diagonal = Locomotion.Displacement(XrInputRouter.WalkInput(new Vector2(1, 1)), Head(30), 1).magnitude;
            var corner = Locomotion.Displacement(new Vector2(1, 1), Head(30), 1).magnitude; // even unclamped input
            Assert.AreEqual(Locomotion.WalkSpeed, straight, 1e-4);
            Assert.AreEqual(straight, diagonal, 1e-4);
            Assert.AreEqual(straight, corner, 1e-4);
        }

        [Test]
        public void Walk_FrameRateIndependent()
        {
            Vector3 Sum(float dt)
            {
                var total = Vector3.zero;
                for (var t = 0f; t < 1f - 1e-4f; t += dt) total += Locomotion.Displacement(new Vector2(0.3f, 0.8f), Head(10), dt);
                return total;
            }
            Assert.AreEqual(0, Vector3.Distance(Sum(1 / 72f), Sum(1 / 30f)), 1e-3, "same distance in one second at 72 and 30 fps");
            Assert.AreEqual(Vector3.zero, Locomotion.Displacement(Vector2.up, Head(0), 0));
        }

        [Test]
        public void Walk_PitchAndRollDoNotChangeSpeedOrHeight()
        {
            var level = Locomotion.Displacement(Vector2.up, Head(60), 1);
            foreach (var (pitch, roll) in new[] { (-60f, 0f), (60f, 0f), (89.9f, 0f), (-89.9f, 0f), (90f, 0f), (-90f, 0f), (20f, 30f), (0f, -40f) })
            {
                var d = Locomotion.Displacement(Vector2.up, Head(60, pitch, roll), 1);
                Assert.AreEqual(0, d.y, 1e-6, $"pitch {pitch} roll {roll}: never vertical");
                Assert.AreEqual(Locomotion.WalkSpeed, d.magnitude, 1e-4, $"pitch {pitch} roll {roll}: same speed");
                Assert.Greater(Vector3.Dot(d.normalized, level.normalized), 0.99f, $"pitch {pitch} roll {roll}: same direction");
            }
        }

        static (Transform rig, Transform head) Rig(Vector3 headLocal, float rigYaw = 0)
        {
            var rig = new GameObject("Rig").transform;
            rig.rotation = Quaternion.Euler(0, rigYaw, 0);
            var head = new GameObject("Head").transform;
            head.SetParent(rig, false);
            head.localPosition = headLocal;
            return (rig, head);
        }

        [Test]
        public void Scene_TurnPivotsOnHead()
        {
            var (rig, head) = Rig(new Vector3(0.3f, 1.1f, -0.2f), 10); // head well off the rig's centre (leaning)
            var before = head.position;
            var yawBefore = head.eulerAngles.y;
            Locomotion.Turn(rig, head, XrInputRouter.TurnDegrees);
            Assert.AreEqual(0, Vector3.Distance(before, head.position), 1e-4, "the head stays where it is");
            Assert.AreEqual(30f, Mathf.DeltaAngle(yawBefore, head.eulerAngles.y), 1e-3);
            Assert.AreEqual(1.1f, head.position.y, 1e-4, "no vertical change");
            Object.DestroyImmediate(rig.gameObject);
        }

        [Test]
        public void Walk_MovesRigNotHeadLocalPose()
        {
            var (rig, head) = Rig(new Vector3(0.1f, 1.0f, 0.05f));
            head.localRotation = Quaternion.Euler(-50, 90, 0); // looking up, facing +x
            var local = head.localPosition;
            for (var i = 0; i < 72; i++) Locomotion.Walk(rig, head, Vector2.up, 1 / 72f);
            Assert.AreEqual(local, head.localPosition, "the tracked pose is never touched");
            Assert.AreEqual(Locomotion.WalkSpeed, head.position.x - 0.1f, 1e-3, "half a metre along +x in one second");
            Assert.AreEqual(1.0f, head.position.y, 1e-5, "no vertical movement");
            Assert.AreEqual(0, rig.position.y, 1e-6);
            Object.DestroyImmediate(rig.gameObject);
        }

        // ---------- furniture ----------

        static Vector3 WalkTowards(Vector3 from, Vector3 target, float seconds = 6)
        {
            var p = from;
            for (var t = 0f; t < seconds; t += 1 / 72f)
            {
                var dir = new Vector3(target.x - p.x, 0, target.z - p.z);
                if (dir.magnitude < 1e-3f) break;
                var step = dir.normalized * Mathf.Min(dir.magnitude, Locomotion.WalkSpeed / 72f);
                p = MarketCollision.Constrain(p, p + step);
            }
            return p;
        }

        [Test]
        public void Collision_StopsAtFurniture()
        {
            foreach (var f in MarketCollision.Footprints)
            {
                if (f.Name == MarketCollision.ShelfName) continue; // tucked behind the candy stall and table: Layout_CrocOwnPlaceReachable
                var stop = WalkTowards(Vector3.zero, f.Centre + Vector3.up * 1.1f);
                Assert.IsFalse(MarketCollision.Blocked(stop), $"{f.Name}: never inside");
                var gap = f.ToLocal(stop);
                Assert.AreEqual(f.Half.y + MarketCollision.BodyRadius, Mathf.Abs(gap.y), 0.01f, $"{f.Name}: stops BodyRadius from the edge");
            }
            var fruit = MarketCollision.Footprints[0];
            var beyond = WalkTowards(Vector3.zero, fruit.Centre + Vector3.forward * 2);
            Assert.Less(beyond.z, fruit.Centre.z, "cannot walk through the counter");
        }

        [Test]
        public void Collision_SlidesAlongEdge()
        {
            var fruit = MarketCollision.Footprints[0]; // ahead, facing the work area
            var atEdge = WalkTowards(Vector3.zero, fruit.Centre);
            var slid = MarketCollision.Constrain(atEdge, atEdge + new Vector3(0.1f, 0, 0.1f));
            Assert.AreEqual(atEdge.x + 0.1f, slid.x, 1e-4, "the part along the edge is kept");
            Assert.AreEqual(atEdge.z, slid.z, 1e-4, "the part into the counter is not");
        }

        [Test]
        public void Collision_NeverPushesDeeper()
        {
            var fruit = MarketCollision.Footprints[0];
            var tooClose = fruit.ToMarket(new Vector2(0, -fruit.Half.y - 0.05f), 1.1f); // leaned in physically
            Assert.IsTrue(MarketCollision.Blocked(tooClose));
            var deeper = MarketCollision.Constrain(tooClose, tooClose + Vector3.forward * 0.02f);
            Assert.AreEqual(tooClose, deeper, "the stick can't take you further in");
            var back = tooClose + Vector3.back * 0.02f;
            Assert.AreEqual(back, MarketCollision.Constrain(tooClose, back), "but always back out");
            var along = tooClose + Vector3.right * 0.02f;
            Assert.AreEqual(along, MarketCollision.Constrain(tooClose, along), "or along");
        }

        [Test]
        public void Layout_ReachableFromClosestWalkPosition()
        {
            const float seatedReach = 0.45f; // horizontal, head to hand target, for a seated child leaning a little [A, H]
            var needed = new List<(Vector3 p, string what)>();
            foreach (var c in WareInfo.Categories)
            {
                var (o, y) = MarketLayout.Stall(c);
                for (var bin = 0; bin < 3; bin++) needed.Add((MarketLayout.Local(o, y, StandView.CrateTop(bin, WareInfo.Catalogue(c)[bin])), $"{c} bin {bin}"));
                needed.Add((MarketLayout.Local(o, y, StandView.BasketCentre), $"{c} basket"));
                foreach (var s in StandView.Slots) needed.Add((MarketLayout.Local(o, y, s), $"{c} basket slot"));
                needed.Add((MarketLayout.Local(o, y, StandView.CoinSpot), $"{c} token"));
                needed.Add((MarketLayout.Local(o, y, StandView.RegisterCentre), $"{c} register"));
            }
            var (t, ty) = MarketLayout.PlayTable;
            foreach (var p in new[] { PlayTableView.BallTarget, PlayTableView.RoadPoint(0.75f), PlayTableView.TidyButton })
                needed.Add((MarketLayout.Local(t, ty, p), $"play table {p}"));
            for (var i = 0; i < WareInfo.BlocksInSet; i++) needed.Add((MarketLayout.Local(t, ty, PlayTableView.BlockPile(i)), $"block {i}"));

            foreach (var (p, what) in needed)
            {
                var head = WalkTowards(Vector3.zero, new Vector3(p.x, 0, p.z), 10);
                var d = new Vector2(p.x - head.x, p.z - head.z).magnitude;
                Assert.LessOrEqual(d, seatedReach, $"{what}: {d:0.00} m from the closest place the stick can take you");
            }
        }

        // ---------- market placement ----------

        [Test]
        public void Placement_StickMovementNeverRecenters()
        {
            // Stick walking/turning moves the rig; the tracked head pose (what recenter detection reads) stays smooth.
            var (rig, head) = Rig(new Vector3(0.05f, 1.0f, 0));
            var p = new StandPlacement();
            Assert.IsTrue(p.ShouldPlace(true, null));
            var lastPos = head.localPosition;
            var lastRot = head.localRotation;
            for (var i = 0; i < 200; i++)
            {
                if (i % 20 == 0) Locomotion.Turn(rig, head, XrInputRouter.TurnDegrees);
                Locomotion.Walk(rig, head, new Vector2(0.3f, 1), 1 / 72f);
                Assert.IsFalse(StandPlacement.LooksLikeRecenter(lastPos, lastRot, head.localPosition, head.localRotation, 1 / 72f));
                Assert.IsFalse(p.ShouldPlace(true, true), "the market is not placed again");
                lastPos = head.localPosition;
                lastRot = head.localRotation;
            }
            Object.DestroyImmediate(rig.gameObject);
        }

        [Test]
        public void Menu_ControlsShowStickMapping()
        {
            var text = GameMenu.ControlsText;
            StringAssert.Contains("Vänster spak: gå", text);
            StringAssert.Contains("Höger spak vänster/höger: vänd dig 30°", text);
            StringAssert.Contains("Höger spak upp/ned: höj/sänk", text);
            StringAssert.DoesNotContain("45°", text);
        }

        // ---------- EPIC-006: the crocodile's own place at the play table ----------

        [Test]
        public void Layout_CrocOwnPlaceReachable()
        {
            const float seatedReach = 0.45f;
            var (t, ty) = MarketLayout.PlayTable;
            Vector3 Market(Vector3 croc) => MarketLayout.Local(t, ty, PlayTableView.CrocOrigin + Quaternion.Euler(0, PlayTableView.CrocYaw, 0) * croc);
            for (var i = 0; i < CrocodileGame.Teeth; i++)
            {
                var tooth = Market(CrocodileView.ToothLocal(i));
                var head = WalkTowards(Vector3.zero, new Vector3(tooth.x, 0, tooth.z), 10);
                var d = new Vector2(tooth.x - head.x, tooth.z - head.z).magnitude;
                Assert.LessOrEqual(d, seatedReach, $"tooth {i}: {d:0.00} m from the closest place you can walk to");
            }
            // Its own place: off the table top (toys, road, blocks and children keep theirs) ...
            var shelf = PlayTableView.ShelfCentre;
            Assert.GreaterOrEqual(Mathf.Abs(shelf.x) - PlayTableView.ShelfSize.x / 2, PlayTableView.TableSize.x / 2 - 1e-4f, "beside the table, not on it");
            foreach (var p in new[] { PlayTableView.BallHome, PlayTableView.BallTarget, PlayTableView.RoadCentre, PlayTableView.BuildSpot, PlayTableView.TidyButton })
                Assert.Greater(Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(shelf.x, 0, shelf.z)), 0.3f, $"clear of {p}");
            // ... not in the children's way ...
            foreach (var toy in WareInfo.Toys)
            {
                var from = Quaternion.Euler(0, -ty, 0) * (MarketLayout.Stall(Category.Toys).position - t);
                for (var k = 0f; k <= 1f; k += 0.02f)
                {
                    var w = PlayTableView.WalkPoint(from, PlayTableView.ChildSpot(toy), k);
                    var dx = Mathf.Abs(w.x - shelf.x) - PlayTableView.ShelfSize.x / 2;
                    var dz = Mathf.Abs(w.z - shelf.z) - PlayTableView.ShelfSize.z / 2;
                    Assert.IsTrue(dx > 0.2f || dz > 0.2f, $"a child walking to the {toy} passes {w} next to the crocodile");
                }
            }
            // ... nor in any stall, and walking stops at it like at other furniture.
            var shelfMarket = MarketLayout.Local(t, ty, shelf);
            foreach (var c in WareInfo.Categories) Assert.AreEqual(0, MarketCollision.Footprints[(int)c].Depth(shelfMarket, 0), $"not inside the {c} stall");
            Assert.IsTrue(MarketCollision.Blocked(shelfMarket), "stick walking stops at the shelf");
        }
    }
}
