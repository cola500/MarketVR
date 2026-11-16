// EPIC-007: fitting the market to the room's play area, and the room -> world transform. Names match the [T: ...]
// references in docs/EPIC-007.md. Polygons are in tracking space (x, z), metres.
using System.Collections.Generic;
using Fruktstand.Core;
using Fruktstand.Game;
using NUnit.Framework;
using UnityEngine;

namespace Fruktstand.Tests
{
    public class SpaceFitTests
    {
        static Vector2[] Rect(Vector2 centre, float yaw, float width, float length) => SpaceFit.RectPolygon(centre, yaw, new Vector2(width, length));

        // The rectangle the layout needs at the result's spacing, as placed by the result, lies inside the polygon.
        static void AssertNeededInside(FitResult r, IReadOnlyList<Vector2> polygon)
        {
            Assert.IsTrue(r.Fits);
            var needed = SpaceFit.RectPolygon(r.Centre, r.Yaw, SpaceFit.NeededSize(r.StallDistance));
            Assert.IsTrue(SpaceFit.RectInside(needed, polygon), $"needed {SpaceFit.NeededSize(r.StallDistance)} at {r.Centre} yaw {r.Yaw} is inside the area");
        }

        // Difference between two directions modulo 90° (a rectangle looks the same turned a quarter).
        static float AxisDelta(float a, float b)
        {
            var d = Mathf.Repeat(a - b, 90f);
            return Mathf.Min(d, 90f - d);
        }

        [Test]
        public void Fit_RectangleCentreAndOrientation()
        {
            var room = Rect(new Vector2(1.2f, -0.6f), 30, 2.2f, 2.6f);
            var r = SpaceFit.Fit(room, preferredYaw: 30);
            AssertNeededInside(r, room);
            Assert.AreEqual(0, Vector2.Distance(new Vector2(1.2f, -0.6f), r.Centre), 0.06f, "in the middle of the area");
            Assert.AreEqual(0, AxisDelta(r.Yaw, 30), 0.5f, "along its edges");
            Assert.IsTrue(r.HasData);
        }

        [Test]
        public void Fit_MeasuredQuestPlayArea()
        {
            // SPIKE-007: the Quest reported a play area of 1.71 x 1.86 m (size only), centred at the tracking origin [A].
            var room = Rect(Vector2.zero, 0, 1.71f, 1.86f);
            var r = SpaceFit.Fit(room, preferredYaw: 0);
            AssertNeededInside(r, room);
            Assert.GreaterOrEqual(r.StallDistance, SpaceFit.MinStall);
            Assert.AreEqual(0, r.Centre.magnitude, 0.06f);
            TestContext.WriteLine($"1.71 x 1.86 m: d = {r.StallDistance:0.00}, yaw {r.Yaw:0}, needs {SpaceFit.NeededSize(r.StallDistance)}");
        }

        [Test]
        public void Fit_IrregularLShapeUsesUsableRectangle()
        {
            // An L: a 2.0 x 2.0 m square with a 1.2 x 3.0 m arm. The bounding box (3.2 x 3.0) centre lies in the empty
            // corner; the usable part is the square.
            var l = new[]
            {
                new Vector2(0, 0), new Vector2(3.2f, 0), new Vector2(3.2f, 2.0f), new Vector2(2.0f, 2.0f),
                new Vector2(2.0f, 3.0f), new Vector2(0, 3.0f),
            };
            var r = SpaceFit.Fit(l);
            AssertNeededInside(r, l);
            var bboxCentre = new Vector2(1.6f, 1.5f);
            Assert.Greater(Vector2.Distance(bboxCentre, r.Centre), 0.1f, "not simply the bounding box centre");
            Assert.IsFalse(SpaceFit.RectInside(SpaceFit.RectPolygon(new Vector2(2.6f, 2.5f), 0, SpaceFit.NeededSize(SpaceFit.MinStall)), l),
                "(the bounding box's empty corner really is outside)");
        }

        [Test]
        public void Fit_IrregularCorridorTooNarrow()
        {
            // A long 0.9 m wide corridor: its bounding box is large, but nothing fits.
            var corridor = Rect(Vector2.zero, 0, 0.9f, 5f);
            var r = SpaceFit.Fit(corridor);
            Assert.IsTrue(r.HasData);
            Assert.IsFalse(r.Fits);
        }

        [Test]
        public void Fit_MissingData()
        {
            foreach (var poly in new[] { null, new Vector2[0], new[] { Vector2.zero, Vector2.one } })
            {
                var r = SpaceFit.Fit(poly);
                Assert.IsFalse(r.HasData, "no area: no guess");
                Assert.IsFalse(r.Fits);
            }
        }

        [Test]
        public void Fit_TooSmallReportsNeed()
        {
            var small = Rect(Vector2.zero, 0, 1.2f, 1.3f);
            var r = SpaceFit.Fit(small);
            Assert.IsFalse(r.Fits);
            var need = SpaceFit.NeededSize(SpaceFit.MinStall);
            Assert.AreEqual(need, r.Need, "reports the minimum size");
            StringAssert.Contains("behöver minst", r.Reason);
            Assert.Greater(need.x, 1.2f);
        }

        [Test]
        public void Fit_SpacingWithinLimitsNoScaling()
        {
            var counter = StandView.CounterSize;
            var table = PlayTableView.TableSize;
            var previous = 0f;
            foreach (var size in new[] { 1.8f, 2.2f, 2.6f, 4f, 10f })
            {
                var r = SpaceFit.Fit(Rect(Vector2.zero, 0, size, size));
                Assert.IsTrue(r.Fits, $"{size} m square");
                Assert.That(r.StallDistance, Is.InRange(SpaceFit.MinStall, SpaceFit.MaxStall));
                Assert.GreaterOrEqual(r.StallDistance, previous, "more room never gives less space between the stalls");
                previous = r.StallDistance;
            }
            Assert.AreEqual(SpaceFit.MaxStall, previous, "a big room gives at most the maximum");
            Assert.AreEqual(0.70f, SpaceFit.MinStall, 1e-4, "today's compact market is the minimum");
            Assert.AreEqual(0.90f, SpaceFit.MaxStall, 1e-4);
            Assert.AreEqual(counter, StandView.CounterSize, "nothing is scaled");
            Assert.AreEqual(table, PlayTableView.TableSize);
            for (var d = SpaceFit.MinStall; d <= SpaceFit.MaxStall + 1e-4f; d += SpaceFit.StallStep)
            {
                var fruit = MarketLayout.Stall(Category.FruitVeg, d).position;
                var candy = MarketLayout.Stall(Category.Candy, d).position;
                Assert.Greater(candy.x - StandView.CounterSize.z / 2, StandView.CounterSize.x / 2, $"d {d}: the counters don't overlap");
                Assert.AreEqual(d, fruit.z, 1e-4);
                Assert.AreEqual(-(d + 0.10f), MarketLayout.PlayTableAt(d).position.z, 1e-4, "the play table 0.10 m further");
            }
        }

        [Test]
        public void Fit_InteractionsFitWithMargin()
        {
            for (var d = SpaceFit.MinStall; d <= SpaceFit.MaxStall + 1e-4f; d += SpaceFit.StallStep)
            {
                var half = SpaceFit.NeededSize(d) / 2;
                var body = half - Vector2.one * SpaceFit.Margin; // where the body may be: 0.25 m inside the area
                Assert.Greater(body.x, 0);
                Assert.Greater(body.y, 0);
                var points = SpaceFit.NeededPoints(d);
                Assert.Greater(points.Count, 40, "crates, baskets, tokens, registers, signs, table, blocks, crocodile teeth");
                foreach (var p in points)
                {
                    var outside = new Vector2(Mathf.Max(0, Mathf.Abs(p.x) - body.x), Mathf.Max(0, Mathf.Abs(p.z) - body.y)).magnitude;
                    Assert.LessOrEqual(outside, SpaceFit.Reach + 1e-4f, $"d {d}: {p} within reach of where the body may be");
                }
                foreach (var c in WareInfo.Categories)
                {
                    var spot = MarketLayout.StandingSpot(c, d);
                    Assert.IsTrue(Mathf.Abs(spot.x) <= body.x && Mathf.Abs(spot.z) <= body.y, $"d {d}: the {c} work position is inside");
                }
            }
            Assert.AreEqual(0.25f, SpaceFit.Margin, 1e-4);
            Assert.AreEqual(0.20f, SpaceFit.Reach, 1e-4);
        }

        [Test]
        public void Room_ToWorldThroughCurrentRig()
        {
            var placement = new RoomPlacement(new Vector2(0.4f, -0.3f), 20, 0.75f);
            var rig = new GameObject("Rig").transform;
            var (p0, y0) = placement.ToWorld(rig);
            Assert.AreEqual(new Vector3(0.4f, 0, -0.3f), p0, "rig at the origin: tracking space = world");
            Assert.AreEqual(20, y0, 1e-4);
            // After stick walking and turning the rig, the same spot in the real room is somewhere else in the world.
            rig.SetPositionAndRotation(new Vector3(3, 0.2f, -1), Quaternion.Euler(0, 90, 0));
            var (p1, y1) = placement.ToWorld(rig);
            var expected = rig.TransformPoint(new Vector3(0.4f, 0, -0.3f));
            Assert.AreEqual(0, Vector3.Distance(new Vector3(expected.x, 0, expected.z), p1), 1e-4, "through the rig's current pose");
            Assert.AreEqual(0, p1.y, 1e-6, "on the floor");
            Assert.AreEqual(110, Mathf.Repeat(y1, 360), 1e-3, "rig yaw + room yaw");
            Object.DestroyImmediate(rig.gameObject);
        }
    }
}
