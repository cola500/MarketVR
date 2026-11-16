// EPIC-013 park round the market: the decoration keeps the work area, the customers' and children's routes and the
// play table free, the customers walk on the gravel path, and stick walking stops at trees, bench and stones like it
// does at the counters. Names match the [T: ...] references in docs/EPIC-013.md.
using System.Collections.Generic;
using Fruktstand.Core;
using Fruktstand.Game;
using NUnit.Framework;
using UnityEngine;

namespace Fruktstand.Tests
{
    public class ParkTests
    {
        static readonly float[] Spacings = { SpaceFit.MinStall, 0.80f, SpaceFit.MaxStall };
        const float Clearance = 0.35f; // between a figure's route and the nearest decoration

        [TearDown]
        public void ResetSpacing() => MarketLayout.SetStallDistance(MarketLayout.DefaultStallDistance);

        // Every point a customer or child stands or walks on, in market space, at spacing d.
        static IEnumerable<(string who, Vector3 at)> Routes(float d)
        {
            foreach (var c in WareInfo.Categories)
            {
                var (p, yaw) = MarketLayout.Stall(c, d);
                for (var k = -1f; k <= 1.001f; k += 0.05f)
                    yield return ($"{c} customer", MarketLayout.Local(p, yaw, CustomerView.Spot + Vector3.right * k * CustomerView.WalkDistance));
            }
            var (sp, syaw) = MarketLayout.Stall(Category.Toys, d);
            var from = MarketLayout.Local(sp, syaw, CustomerView.Spot);
            var (tp, tyaw) = MarketLayout.PlayTableAt(d);
            foreach (var toy in new[] { Ware.Ball, Ware.Car, Ware.Blocks })
            {
                var spot = MarketLayout.Local(tp, tyaw, PlayTableView.ChildSpot(toy));
                for (var k = 0f; k <= 1.001f; k += 0.05f)
                {
                    // the table is turned 180 degrees, so its walk corner (from.x, spot.z) is the same in market space
                    yield return ($"child to {toy}", PlayTableView.WalkPoint(from, spot, k));
                    yield return ($"child leaving {toy}", MarketLayout.Local(tp, tyaw, PlayTableView.ChildSpot(toy) + Vector3.forward * k * PlayTableView.LeaveDistance));
                }
            }
        }

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        [Test]
        public void Park_KeepsRoutesFree()
        {
            Assert.Greater(ParkLayout.Items.Count, 0);
            foreach (var d in Spacings)
                foreach (var (who, at) in Routes(d))
                    foreach (var item in ParkLayout.Items)
                        Assert.GreaterOrEqual(Flat(at, item.Position), item.Radius + Clearance, $"d {d}: {item.Piece} at {item.Position} too close to the {who} at {at}");
        }

        [Test]
        public void Park_KeepsMarketAndWorkAreaFree()
        {
            foreach (var d in Spacings)
                foreach (var item in ParkLayout.Items)
                {
                    Assert.Greater(MarketLayout.OutsideBodyRegion(item.Position), item.Radius + 1.0f, $"{item.Piece} well outside where the player plays");
                    foreach (var f in MarketCollision.FootprintsAt(d))
                        Assert.AreEqual(0, f.Depth(item.Position, item.Radius + Clearance), $"d {d}: {item.Piece} clear of {f.Name}");
                }
        }

        [Test]
        public void Park_CustomersWalkOnTheGravelPath()
        {
            foreach (var d in Spacings)
                foreach (var (who, at) in Routes(d))
                    Assert.IsTrue(ParkLayout.OnPath(at), $"d {d}: the {who} at {at} is on the path");
            Assert.IsFalse(ParkLayout.OnPath(Vector3.zero), "the work area stays grass");
        }

        [Test]
        public void Park_TreesStandOffThePath()
        {
            foreach (var item in ParkLayout.Items)
                Assert.IsFalse(ParkLayout.OnPath(item.Position), $"{item.Piece} at {item.Position} is beside the path, not on it");
        }

        // EPIC-014: the view along the path, ahead past the fruit stall and back where the children leave, stays open.
        [Test]
        public void Park_KeepsSightlinesAlongThePath()
        {
            foreach (var item in ParkLayout.Items)
                // along the path to its end (low flower beds don't block a view; EPIC-015's gates close it beyond)
                if (item.Solid && Mathf.Abs(item.Position.z) > ParkLayout.RingOuter && Mathf.Abs(item.Position.z) < ParkLayout.ArmEnd)
                    Assert.Greater(Mathf.Abs(item.Position.x), ParkLayout.SightlineHalfWidth + item.Radius,
                        $"{item.Piece} at {item.Position} blocks the view along the path");
        }

        // EPIC-014: trees all round between 6 and 20 m (apart from the two sightlines): a park, not an open plain.
        [Test]
        public void Park_SurroundsTheMarket()
        {
            var angles = new List<float>();
            foreach (var item in ParkLayout.Items)
            {
                var r = new Vector2(item.Position.x, item.Position.z).magnitude;
                if (item.Solid && item.Piece != ParkPiece.Bench && r >= 6 && r <= 20)
                    angles.Add(Mathf.Atan2(item.Position.x, item.Position.z) * Mathf.Rad2Deg);
            }
            Assert.GreaterOrEqual(angles.Count, 20, "groups of trees, not single ones");
            angles.Sort();
            for (var i = 0; i < angles.Count; i++)
            {
                var gap = (i + 1 < angles.Count ? angles[i + 1] : angles[0] + 360) - angles[i];
                Assert.LessOrEqual(gap, 70, $"an empty sector after {angles[i]:0} degrees");
            }
        }

        [Test]
        public void Walk_StopsAtParkTreesBenchAndStones()
        {
            var solid = 0;
            foreach (var item in ParkLayout.Items)
            {
                if (!item.Solid) continue; // flowers and grass: walked through
                solid++;
                // start 1.2 m out, from the first direction that is free (trees in a group stand close together)
                var p = Vector3.zero;
                for (var k = 0; k < 16; k++)
                {
                    var dir = Quaternion.Euler(0, k * 22.5f, 0) * new Vector3(item.Position.x, 0, item.Position.z).normalized;
                    p = item.Position + dir * (item.Radius + 1.2f) + Vector3.up * 1.1f;
                    if (!MarketCollision.Blocked(p)) break;
                }
                Assert.IsFalse(MarketCollision.Blocked(p), $"{item.Piece}: a free place to start from");
                for (var i = 0; i < 400; i++)
                {
                    var dir = item.Position + Vector3.up * 1.1f - p;
                    p = MarketCollision.Constrain(p, p + dir.normalized * Mathf.Min(dir.magnitude, Locomotion.WalkSpeed / 72f));
                }
                var f = item.Footprint;
                Assert.GreaterOrEqual(Flat(p, item.Position), Mathf.Min(f.Half.x, f.Half.y) + MarketCollision.BodyRadius - 0.01f, $"{item.Piece}: stopped outside");
                Assert.AreEqual(0, f.Depth(p), 1e-4f, $"{item.Piece}: never inside its footprint");
                Assert.IsFalse(MarketCollision.Blocked(p), $"{item.Piece}: never inside anything");
            }
            Assert.Greater(solid, 0);
        }
    }
}
