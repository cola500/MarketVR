// The compact market (EPIC-003, SPEC §14): three shallow stalls and the play table round a small shared work area in
// a physical play space of about 1.5 x 2.0 m (conservative, not measured). The player stands in the middle and turns and
// takes short steps; nothing teleports. Market space: origin = centre of the work area, +z = towards the fruit stall,
// +x = to the right. Every piece of furniture faces the centre (its local -z points at the work area).
// Locked by layout tests; the plan view with all measurements is in the spec.
using Fruktstand.Core;
using UnityEngine;

namespace Fruktstand.Game
{
    // Desktop camera views only (development); the VR player just turns and steps.
    public enum DesktopView { Overview = 0, FruitVeg = 1, Candy = 2, Toys = 3, Play = 4 }

    public static class MarketLayout
    {
        public static readonly Vector2 PhysicalHalf = new(0.75f, 1.0f); // 1.5 x 2.0 m
        public const float BoundaryMargin = 0.25f;                      // keep the body this far inside it
        public static readonly Vector2 BodyHalf = PhysicalHalf - Vector2.one * BoundaryMargin; // where the body may be
        public const float WorkAreaHalf = 0.55f;                         // free floor: 1.1 x 1.1 m
        public const float DefaultStallDistance = 0.70f;                 // centre of each stall from the middle (compact)
        public const float TableBehindStalls = 0.10f;                    // the play table is this much further away

        // EPIC-007: the spacing in use (0.70-0.90 m, SpaceFit). Only the distances change; nothing is scaled.
        public static float StallDistance { get; private set; } = DefaultStallDistance;
        public static float TableDistance => StallDistance + TableBehindStalls;
        public static int Revision { get; private set; } // changes when the spacing does (cached footprints follow)

        public static void SetStallDistance(float d)
        {
            if (Mathf.Approximately(d, StallDistance)) return;
            StallDistance = d;
            Revision++;
        }
        public const float StandIn = 0.30f;                              // a player stands this far inside an edge
        public const int ViewCount = 5;

        public static float StallYaw(Category c) => c switch { Category.FruitVeg => 0f, Category.Candy => 90f, _ => -90f };

        static Vector3 Ahead(float yaw, float distance) => Quaternion.Euler(0, yaw, 0) * Vector3.forward * distance;

        public static (Vector3 position, float yaw) Stall(Category c) => Stall(c, StallDistance);
        public static (Vector3 position, float yaw) Stall(Category c, float d) => (Ahead(StallYaw(c), d), StallYaw(c));
        public static (Vector3 position, float yaw) PlayTable => PlayTableAt(StallDistance);
        public static (Vector3 position, float yaw) PlayTableAt(float d) => (Ahead(180f, d + TableBehindStalls), 180f);

        public static Vector3 Local(Vector3 origin, float yaw, Vector3 local) => origin + Quaternion.Euler(0, yaw, 0) * local;

        // Where a player would naturally stand (ground) to use a stall or the play table: 0.30 m in from its inner edge.
        public static Vector3 StandingSpot(Category c) => StandingSpot(c, StallDistance);

        public static Vector3 StandingSpot(Category c, float d)
        {
            var (p, y) = Stall(c, d);
            return Local(p, y, new Vector3(0, 0, StandView.InnerEdge - StandIn));
        }

        public static Vector3 PlayStandingSpot => PlayStandingSpotAt(StallDistance);

        public static Vector3 PlayStandingSpotAt(float d)
        {
            var (p, y) = PlayTableAt(d);
            return Local(p, y, new Vector3(0, 0, -PlayTableView.TableSize.z / 2 - StandIn));
        }

        public static bool InWorkArea(Vector3 p, float margin = 0) => Mathf.Abs(p.x) < WorkAreaHalf + margin && Mathf.Abs(p.z) < WorkAreaHalf + margin;

        // How far a point is outside the region where the body may be (0 inside).
        public static float OutsideBodyRegion(Vector3 p) =>
            new Vector2(Mathf.Max(0, Mathf.Abs(p.x) - BodyHalf.x), Mathf.Max(0, Mathf.Abs(p.z) - BodyHalf.y)).magnitude;

        public static string Name(DesktopView v) => v switch
        {
            DesktopView.Overview => "Översikt",
            DesktopView.FruitVeg => "Frukt",
            DesktopView.Candy => "Godis",
            DesktopView.Toys => "Leksaker",
            _ => "Lekbordet",
        };
    }
}
