// EPIC-013: the little park round the market (plan in SPEC §30), in market space (origin = centre of the work area,
// +z = towards the fruit stall). Everything here is flat or stands on the ground; nothing is ever lifted with the
// counters. The gravel path is a ring under the customers' and children's routes plus two arms (front and back,
// where the children leave). Trees, bench and stones stand beside it, well outside the market and every route
// (ParkTests), and have footprints so stick walking stops at them like at the counters. Flowers and grass tufts are
// walked through. Decoration has no colliders and never takes part in grabbing, pointing or toy physics.
// EPIC-014: groups of trees and round bushes in a broken ring 6-20 m out make it a park all round instead of a plain;
// the views along the path, ahead and back, stay open (SightlineHalfWidth). Birds sing from the nearest trees.
using System.Collections.Generic;
using UnityEngine;

namespace Fruktstand.Game
{
    // Everything before Flowers comes from the Asset Store package (scripts/park-assets.sh); Flowers and Bush are built
    // from simple shapes in ParkView.
    public enum ParkPiece
    {
        SphericalTree, RoundTree, OakTree, LeafTree, Bench, Grass, TallGrass, Stone, BigStone,
        BirchTree, TallTree, // EPIC-014: more kinds for the groups further out
        Flowers, Bush,
    }

    public readonly struct ParkItem
    {
        public ParkItem(ParkPiece piece, Vector3 position, float yaw, float scale, float radius, Vector2 half = default)
        {
            Piece = piece;
            Position = position;
            Yaw = yaw;
            Scale = scale;
            Radius = radius;
            Half = half;
        }

        public ParkPiece Piece { get; }
        public Vector3 Position { get; }   // on the ground (market space)
        public float Yaw { get; }
        public float Scale { get; }
        public float Radius { get; }       // how far it reaches on the ground (routes keep clear of it)
        public Vector2 Half { get; }       // its walking footprint (half width, half depth); zero = walked through
        public bool Solid => Half != Vector2.zero;
        public MarketCollision.Footprint Footprint => new(Piece.ToString(), Position, Yaw, Half);
    }

    public static class ParkLayout
    {
        // The gravel ring: between two rounded squares round the market.
        public const float RingInner = 1.0f, RingInnerCorner = 0.3f;
        public const float RingOuter = 1.85f, RingOuterCorner = 0.7f;
        public const float ArmHalfWidth = 0.55f, ArmEnd = 9f; // front and back arms, out to 9 m (EPIC-015: the gates beyond)
        public const float SightlineHalfWidth = 1.5f;          // EPIC-014: nothing tall this close to the path's line
        public const float PathLift = 0.0015f;                 // above the grass, below the calibration marking

        static readonly Vector2 Trunk = new(0.3f, 0.3f);

        static ParkItem Tree(ParkPiece p, float x, float z, float yaw, float scale) => new(p, new Vector3(x, 0, z), yaw, scale, 0.4f, Trunk);
        static ParkItem Tuft(ParkPiece p, float x, float z, float yaw) => new(p, new Vector3(x, 0, z), yaw, 0.6f, 0.25f);
        static ParkItem Flowers(float x, float z, float yaw) => new(ParkPiece.Flowers, new Vector3(x, 0, z), yaw, 1f, 0.3f);

        // Built on first use: static fields initialise in text order, and the lists below come later.
        static List<ParkItem> items;
        public static IReadOnlyList<ParkItem> Items => items ??= Build();

        static List<ParkItem> Build()
        {
            var items = new List<ParkItem>(Near);
            items.AddRange(Groups());
            return items;
        }

        static readonly ParkItem[] Near =
        {
            // five trees framing the market: two either side of the front path, one on each side, one behind
            Tree(ParkPiece.OakTree, -3.4f, 3.3f, 20, 0.6f),
            Tree(ParkPiece.RoundTree, 3.3f, 3.5f, -30, 0.85f),
            Tree(ParkPiece.SphericalTree, 3.9f, -0.6f, 0, 0.8f),
            Tree(ParkPiece.LeafTree, -3.8f, -1.2f, 60, 0.75f),
            Tree(ParkPiece.SphericalTree, 2.5f, -3.7f, 140, 0.7f),
            // a bench on the toy side, facing the market
            new(ParkPiece.Bench, new Vector3(-2.65f, 0, 1.3f), 90, 0.8f, 0.8f, new Vector2(0.78f, 0.3f)),
            new(ParkPiece.BigStone, new Vector3(2.9f, 0, 2.4f), 30, 0.6f, 0.3f, new Vector2(0.22f, 0.22f)),
            new(ParkPiece.Stone, new Vector3(-2.4f, 0, -2.7f), 0, 0.6f, 0.25f, new Vector2(0.18f, 0.18f)),
            // grass at the tree bases and beside the bench
            Tuft(ParkPiece.TallGrass, -3.0f, 2.8f, 0),
            Tuft(ParkPiece.Grass, -2.7f, 2.2f, 70),
            Tuft(ParkPiece.Grass, 3.5f, -1.3f, 20),
            Tuft(ParkPiece.TallGrass, -3.3f, -0.6f, 120),
            Tuft(ParkPiece.Grass, 2.1f, -3.2f, 200),
            Tuft(ParkPiece.TallGrass, 2.8f, 3.0f, 300),
            // flower beds along the outside of the path, between the routes
            Flowers(2.35f, 1.0f, 0),
            Flowers(-2.3f, -1.0f, 50),
            Flowers(1.05f, 2.35f, 100),
            Flowers(-1.1f, 2.4f, 150),
            Flowers(1.25f, -2.45f, 200),
            Flowers(-1.35f, -2.35f, 250),
            // EPIC-015: a gate at the end of each arm, where visitors come and go out of sight behind a bush
            Gate(true), Gate(false),
            Tree(ParkPiece.RoundTree, -1.5f, 10.6f, 40, 0.8f), Tree(ParkPiece.SphericalTree, 2.3f, 10.9f, 200, 0.75f),
            Tree(ParkPiece.LeafTree, 1.5f, -10.6f, 110, 0.75f), Tree(ParkPiece.RoundTree, -2.3f, -10.9f, 300, 0.8f),
        };

        static ParkItem Gate(bool front) =>
            new(ParkPiece.Bush, new Vector3(front ? 0.9f : -0.9f, 0, front ? 9.8f : -9.8f), front ? 20 : 200, 1.3f, 0.6f * 1.3f, new Vector2(0.45f, 0.45f) * 1.3f);

        // EPIC-014: groups round the park. Mid ring (about 7.5 m): three trees and a bush or two each; far ring (about 15 m):
        // four bigger trees each. The angles leave the views along the path (ahead and back) open. Each group's trees
        // stand round its centre at fixed angles (never on top of each other), with a seeded scale and kind.
        static readonly (float angle, float radius, int trees, int bushes)[] GroupPlan =
        {
            (35, 7.5f, 3, 2), (80, 7.2f, 3, 1), (125, 7.8f, 3, 2), (235, 7.6f, 3, 1), (280, 7.3f, 3, 2), (325, 7.7f, 3, 1),
            (18, 15f, 4, 0), (55, 16f, 4, 1), (100, 15f, 4, 0), (145, 16f, 4, 1), (162, 14f, 3, 0),
            (198, 14f, 3, 0), (215, 16f, 4, 1), (260, 15f, 4, 0), (305, 16f, 4, 1), (342, 15f, 4, 0),
        };

        static readonly ParkPiece[] GroupKinds =
            { ParkPiece.SphericalTree, ParkPiece.RoundTree, ParkPiece.LeafTree, ParkPiece.BirchTree, ParkPiece.TallTree, ParkPiece.OakTree };

        static IEnumerable<ParkItem> Groups()
        {
            var random = new Core.Prng(14);
            float Unit() => random.Below(1000) / 999f;
            foreach (var (angle, radius, trees, bushes) in GroupPlan)
            {
                var a = angle * Mathf.Deg2Rad;
                var centre = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * radius;
                var far = radius > 10;
                for (var t = 0; t < trees; t++)
                {
                    var around = (t * 360f / trees + angle * 3) * Mathf.Deg2Rad;
                    var spread = far ? 2.2f : 1.5f;
                    var at = centre + new Vector3(Mathf.Sin(around), 0, Mathf.Cos(around)) * spread * (0.8f + 0.4f * Unit());
                    var kind = GroupKinds[random.Below(GroupKinds.Length)];
                    var scale = (far ? 0.9f : 0.7f) + 0.3f * Unit();
                    if (kind == ParkPiece.OakTree) scale *= 0.7f; // the oak is the biggest model
                    yield return Tree(kind, at.x, at.z, random.Below(360), scale);
                }
                for (var b = 0; b < bushes; b++)
                {
                    // in front of the group (towards the market), so the bushes soften the trees' feet
                    var side = (b == 0 ? -1 : 1) * 1.3f;
                    var inward = -centre.normalized * 1.6f;
                    var across = new Vector3(centre.z, 0, -centre.x).normalized * side;
                    var at = centre + inward + across;
                    var size = 0.8f + 0.4f * Unit();
                    yield return new ParkItem(ParkPiece.Bush, at, random.Below(360), size, 0.6f * size, new Vector2(0.45f, 0.45f) * size);
                }
            }
        }

        // EPIC-014: where the birds sing: up in the nearest trees (the five round the market and the mid groups' first).
        static List<Vector3> birdSpots;
        public static IReadOnlyList<Vector3> BirdSpots => birdSpots ??= FindBirdSpots();

        static List<Vector3> FindBirdSpots()
        {
            var spots = new List<Vector3>();
            foreach (var i in Items)
            {
                var r = new Vector2(i.Position.x, i.Position.z).magnitude;
                if (i.Solid && i.Piece is not (ParkPiece.Bench or ParkPiece.Stone or ParkPiece.BigStone or ParkPiece.Bush) && r < 9 && spots.Count < 8)
                    spots.Add(i.Position + Vector3.up * (3.2f * i.Scale));
            }
            return spots;
        }

        public static IEnumerable<MarketCollision.Footprint> Footprints
        {
            get { foreach (var i in Items) if (i.Solid) yield return i.Footprint; }
        }

        public static bool OnPath(Vector3 p)
        {
            var ring = InRoundedSquare(p, RingOuter, RingOuterCorner) && !InRoundedSquare(p, RingInner, RingInnerCorner);
            var arm = Mathf.Abs(p.x) <= ArmHalfWidth && Mathf.Abs(p.z) >= RingInner && Mathf.Abs(p.z) <= ArmEnd;
            return ring || arm;
        }

        public static bool InRoundedSquare(Vector3 p, float half, float corner)
        {
            float ax = Mathf.Abs(p.x), az = Mathf.Abs(p.z);
            if (ax > half || az > half) return false;
            var c = half - corner;
            if (ax <= c || az <= c) return true;
            return new Vector2(ax - c, az - c).magnitude <= corner;
        }
    }
}
