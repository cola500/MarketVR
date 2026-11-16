// EPIC-011: where a released building block ends up (Core.BlockPlacement). Table space: y = 0 on the table top.
using System.Collections.Generic;
using Fruktstand.Core;
using NUnit.Framework;
using UnityEngine;

namespace Fruktstand.Tests
{
    public class BlockPlacementTests
    {
        const float S = 0.06f; // block size
        static readonly Vector2 Table = new(0.5f, 0.25f); // half extents (x, z)
        static readonly Vector3 Last = new(0.3f, S / 2, -0.13f);

        static BlockPlacement.Result Place(Vector3 released, params Vector3[] others) =>
            BlockPlacement.Resolve(released, new List<Vector3>(others), S, Table, Last);

        static bool Overlaps(Vector3 a, Vector3 b) =>
            Mathf.Abs(a.x - b.x) < S * 0.98f && Mathf.Abs(a.y - b.y) < S * 0.98f && Mathf.Abs(a.z - b.z) < S * 0.98f;

        [Test]
        public void Placement_OverlapLandsOnTop()
        {
            var below = new Vector3(0.1f, S / 2, 0.0f);
            foreach (var offset in new[] { new Vector3(0.01f, 0.01f, 0.008f), new Vector3(-0.03f, 0.0f, 0.02f), new Vector3(0.0f, -0.02f, 0.0f), new Vector3(0.02f, 0.05f, -0.01f) })
            {
                var r = Place(below + offset, below);
                Assert.AreEqual(BlockPlacement.Kind.OnTop, r.Kind, $"released at {offset}");
                Assert.AreEqual(0, r.Below);
                Assert.AreEqual(below + Vector3.up * S, r.Position, $"straight on top ({offset})");
            }
        }

        [Test]
        public void Placement_TopOfTheColumn()
        {
            var a = new Vector3(0.1f, S / 2, 0f);
            var b = new Vector3(0.1f, S * 1.5f, 0f);
            var r = Place(a + new Vector3(0.01f, 0, 0.01f), a, b); // released into the bottom block of a two-block stack
            Assert.AreEqual(BlockPlacement.Kind.OnTop, r.Kind);
            Assert.AreEqual(b + Vector3.up * S, r.Position, "on the top of the stack, not inside it");
        }

        [Test]
        public void Placement_WholeVolumeNotJustCentre()
        {
            var other = new Vector3(0.1f, S / 2, 0f);
            // Centre clearly beside the other block, but the volumes overlap by a third of a block.
            var released = other + new Vector3(S * 0.66f, 0, 0);
            var r = Place(released, other);
            Assert.AreNotEqual(BlockPlacement.Kind.AsReleased, r.Kind, "an edge overlap is resolved too");
            Assert.IsFalse(Overlaps(r.Position, other), "no overlap afterwards");
        }

        [Test]
        public void Placement_BlockedTopGoesNearby()
        {
            var below = new Vector3(0.1f, S / 2, 0f);
            var leaning = below + Vector3.up * S * 1.02f + new Vector3(0.035f, 0, 0); // half over the top of `below`
            var r = Place(below + new Vector3(0.01f, 0.01f, 0f), below, leaning);
            Assert.AreEqual(BlockPlacement.Kind.Nearby, r.Kind, "the top is blocked by a leaning block");
            Assert.AreEqual(S / 2, r.Position.y, 1e-4, "on the table");
            Assert.Less(new Vector2(r.Position.x - below.x, r.Position.z - below.z).magnitude, 0.15f + 1e-4f, "near where it was let go");
            Assert.IsFalse(Overlaps(r.Position, below));
        }

        [Test]
        public void Placement_InsideTableIsLifted()
        {
            var r = Place(new Vector3(-0.1f, 0.005f, 0.05f));
            Assert.AreEqual(BlockPlacement.Kind.Lifted, r.Kind);
            Assert.AreEqual(new Vector3(-0.1f, S / 2, 0.05f), r.Position);
        }

        [Test]
        public void Placement_NearTheEdgeComesIn()
        {
            var r = Place(new Vector3(0.49f, 0.04f, 0.24f)); // overlapping the rim at a corner
            Assert.LessOrEqual(Mathf.Abs(r.Position.x), Table.x - BlockPlacement.Rim - S / 2 + 1e-4f);
            Assert.LessOrEqual(Mathf.Abs(r.Position.z), Table.y - BlockPlacement.Rim - S / 2 + 1e-4f);
            Assert.Less(Vector3.Distance(r.Position, new Vector3(0.49f, 0.04f, 0.24f)), 0.1f, "helped only a little");
        }

        [Test]
        public void Placement_FreeReleasesAreLeftAlone()
        {
            var high = new Vector3(-0.2f, 0.2f, 0f); // above the table, nothing below
            Assert.AreEqual(BlockPlacement.Kind.AsReleased, Place(high, new Vector3(0.2f, S / 2, 0f)).Kind, "free building: it falls from where it was let go");
            var outside = new Vector3(0f, 0.1f, -0.6f); // clearly off the table: it may fall (the lost rule brings it back)
            Assert.AreEqual(BlockPlacement.Kind.AsReleased, Place(outside).Kind);
            Assert.AreEqual(outside, Place(outside).Position);
        }

        [Test]
        public void Placement_NothingFreeGoesToTheLastPlace()
        {
            // A carpet of blocks round the release point: nothing free within reach.
            var others = new List<Vector3>();
            for (var x = -4; x <= 4; x++)
            for (var z = -3; z <= 3; z++)
            {
                var p = new Vector3(0.0f + x * S, S / 2, 0.0f + z * S);
                if (Mathf.Abs(p.z) < Table.y - BlockPlacement.Rim) others.Add(p);
                others.Add(p + Vector3.up * S); // two high, so the tops are taken as well
                others.Add(p + Vector3.up * S * 2);
            }
            var r = BlockPlacement.Resolve(new Vector3(0.01f, 0.02f, 0.01f), others, S, Table, Last, maxStack: 3);
            Assert.AreEqual(BlockPlacement.Kind.LastValid, r.Kind);
            Assert.AreEqual(Last, r.Position);
        }
    }
}
