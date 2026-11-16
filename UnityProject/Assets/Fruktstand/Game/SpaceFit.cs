// EPIC-007: fitting the market to the room's play area. Pure geometry, tested in EditMode.
// The area is a polygon in tracking space (x, z). The layout needs a rectangle round its centre, whose size depends
// on the spacing d (MarketLayout: stalls at d, the play table at d + 0.10): every work position must lie in the part
// where the body may be (the area minus Margin), and every needed hand interaction at most Reach outside that part.
// Only the spacing adapts (MinStall..MaxStall); nothing is ever scaled. A rectangle only counts when it lies wholly
// inside the polygon (all corners in, no polygon corner inside it, no edges crossing): for an irregular area a
// bounding box is not usable space.
using System.Collections.Generic;
using Fruktstand.Core;
using UnityEngine;

namespace Fruktstand.Game
{
    public sealed class FitResult
    {
        public bool HasData;         // there was an area to fit to
        public bool Fits;
        public Vector2 Centre;       // tracking space (x, z)
        public float Yaw;            // degrees: the market's +z (the fruit stall) direction
        public float StallDistance;
        public Vector2 Need;         // the smallest area (width x, length z) the compact layout needs
        public string Reason = "";
    }

    // Where the market goes in the real room (tracking space), and how far apart the stalls are.
    public readonly struct RoomPlacement
    {
        public RoomPlacement(Vector2 centre, float yaw, float stallDistance)
        {
            Centre = centre;
            Yaw = yaw;
            StallDistance = stallDistance;
        }

        public Vector2 Centre { get; }
        public float Yaw { get; }
        public float StallDistance { get; }

        // Through the rig's *current* pose: the same spot in the real room, wherever stick walking has taken the rig.
        public (Vector3 position, float yaw) ToWorld(Transform rig)
        {
            var p = rig.TransformPoint(new Vector3(Centre.x, 0, Centre.y));
            return (new Vector3(p.x, 0, p.z), rig.eulerAngles.y + Yaw);
        }
    }

    public static class SpaceFit
    {
        public const float MinStall = MarketLayout.DefaultStallDistance; // today's compact market; closer would overlap
        public const float MaxStall = 0.90f;
        public const float StallStep = 0.05f;
        public const float Margin = MarketLayout.BoundaryMargin;          // 0.25 m between the body and the boundary
        public const float Reach = 0.20f;                                 // hand beyond where the body may be
        const float GridStep = 0.05f;

        // Every hand interaction the game needs, in market space, at spacing d.
        public static List<Vector3> NeededPoints(float d)
        {
            var needed = new List<Vector3>();
            foreach (var c in WareInfo.Categories)
            {
                var (o, y) = MarketLayout.Stall(c, d);
                var wares = WareInfo.Catalogue(c);
                for (var bin = 0; bin < 3; bin++) needed.Add(MarketLayout.Local(o, y, StandView.CrateTop(bin, wares[bin])));
                needed.Add(MarketLayout.Local(o, y, StandView.BasketCentre));
                foreach (var s in StandView.Slots) needed.Add(MarketLayout.Local(o, y, s));
                needed.Add(MarketLayout.Local(o, y, StandView.CoinSpot));
                needed.Add(MarketLayout.Local(o, y, StandView.RegisterCentre));
                needed.Add(MarketLayout.Local(o, y, StandView.OpenSignPosition));
            }
            var (t, ty) = MarketLayout.PlayTableAt(d);
            foreach (var p in new[] { PlayTableView.BallTarget, PlayTableView.RoadPoint(0.75f), PlayTableView.RoadCentre, PlayTableView.TidyButton })
                needed.Add(MarketLayout.Local(t, ty, p));
            for (var i = 0; i < WareInfo.BlocksInSet; i++) needed.Add(MarketLayout.Local(t, ty, PlayTableView.BlockPile(i)));
            for (var i = 0; i < CrocodileGame.Teeth; i++)
                needed.Add(MarketLayout.Local(t, ty, PlayTableView.CrocOrigin + Quaternion.Euler(0, PlayTableView.CrocYaw, 0) * CrocodileView.ToothLocal(i)));
            return needed;
        }

        // The rectangle (width x, length z) the layout needs round its centre at spacing d.
        public static Vector2 NeededSize(float d)
        {
            var half = Vector2.zero;
            foreach (var p in NeededPoints(d))
                half = Vector2.Max(half, new Vector2(Mathf.Abs(p.x) - Reach, Mathf.Abs(p.z) - Reach));
            foreach (var c in WareInfo.Categories)
            {
                var s = MarketLayout.StandingSpot(c, d);
                half = Vector2.Max(half, new Vector2(Mathf.Abs(s.x), Mathf.Abs(s.z)));
            }
            var play = MarketLayout.PlayStandingSpotAt(d);
            half = Vector2.Max(half, new Vector2(Mathf.Abs(play.x), Mathf.Abs(play.z)));
            half += Vector2.one * Margin;
            return new Vector2(Round(half.x * 2), Round(half.y * 2));
        }

        static float Round(float v) => Mathf.Ceil(v * 100f - 1e-3f) / 100f; // whole centimetres, never less

        // Corners of a rectangle: centre, yaw (degrees, the rectangle's +z direction), size (width x, length z).
        public static Vector2[] RectPolygon(Vector2 centre, float yaw, Vector2 size)
        {
            var r = Quaternion.Euler(0, yaw, 0);
            var corners = new Vector2[4];
            var signs = new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) };
            for (var i = 0; i < 4; i++)
            {
                var v = r * new Vector3(signs[i].Item1 * size.x / 2, 0, signs[i].Item2 * size.y / 2);
                corners[i] = centre + new Vector2(v.x, v.z);
            }
            return corners;
        }

        public static FitResult Fit(IReadOnlyList<Vector2> polygon, float preferredYaw = 0, Vector2? preferredCentre = null)
        {
            var need = NeededSize(MinStall);
            if (polygon == null || polygon.Count < 3 || Mathf.Abs(Area(polygon)) < 1e-4f)
                return new FitResult { HasData = false, Need = need, Reason = "Ingen spelyta från systemet" };

            var centroid = Centroid(polygon);
            var target = preferredCentre ?? centroid;
            var centres = Centres(polygon, centroid);
            centres.Sort((a, b) => (a - target).sqrMagnitude.CompareTo((b - target).sqrMagnitude));
            var yaws = Yaws(polygon);
            yaws.Sort((a, b) => Mathf.Abs(Mathf.DeltaAngle(a, preferredYaw)).CompareTo(Mathf.Abs(Mathf.DeltaAngle(b, preferredYaw))));

            for (var d = MaxStall; d >= MinStall - 1e-4f; d -= StallStep)
            {
                var size = NeededSize(d);
                foreach (var yaw in yaws)
                    foreach (var c in centres)
                        if (RectInside(RectPolygon(c, yaw, size), polygon))
                            return new FitResult { HasData = true, Fits = true, Centre = c, Yaw = yaw, StallDistance = Mathf.Round(d * 100) / 100, Need = need, Reason = "Får plats" };
            }
            return new FitResult
            {
                HasData = true, Fits = false, Centre = centroid, Yaw = preferredYaw, StallDistance = MinStall, Need = need,
                Reason = $"Får inte plats: behöver minst {Calibration.Sv(need.x)} × {Calibration.Sv(need.y)} m",
            };
        }

        // Candidate directions: along every edge (and its quarter turns), plus every 15°.
        static List<float> Yaws(IReadOnlyList<Vector2> polygon)
        {
            var yaws = new List<float>();
            void Add(float y)
            {
                y = Mathf.Repeat(y, 360f);
                foreach (var e in yaws) if (Mathf.Abs(Mathf.DeltaAngle(e, y)) < 0.5f) return;
                yaws.Add(y);
            }
            for (var i = 0; i < polygon.Count; i++)
            {
                var e = polygon[(i + 1) % polygon.Count] - polygon[i];
                if (e.sqrMagnitude < 1e-6f) continue;
                var a = Mathf.Atan2(e.x, e.y) * Mathf.Rad2Deg; // yaw whose +z runs along the edge
                for (var k = 0; k < 4; k++) Add(a + k * 90);
            }
            for (var a = 0; a < 360; a += 15) Add(a);
            return yaws;
        }

        static List<Vector2> Centres(IReadOnlyList<Vector2> polygon, Vector2 centroid)
        {
            var list = new List<Vector2>();
            if (PointInside(centroid, polygon)) list.Add(centroid);
            var min = polygon[0];
            var max = polygon[0];
            foreach (var p in polygon)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            for (var x = min.x; x <= max.x; x += GridStep)
                for (var z = min.y; z <= max.y; z += GridStep)
                {
                    var p = new Vector2(x, z);
                    if (PointInside(p, polygon)) list.Add(p);
                }
            return list;
        }

        public static bool RectInside(IReadOnlyList<Vector2> rect, IReadOnlyList<Vector2> polygon)
        {
            foreach (var c in rect) if (!PointInside(c, polygon)) return false;
            foreach (var v in polygon) if (StrictlyInsideConvex(v, rect)) return false;
            for (var i = 0; i < rect.Count; i++)
                for (var j = 0; j < polygon.Count; j++)
                    if (Crosses(rect[i], rect[(i + 1) % rect.Count], polygon[j], polygon[(j + 1) % polygon.Count])) return false;
            return true;
        }

        // Inside or on the boundary.
        public static bool PointInside(Vector2 p, IReadOnlyList<Vector2> polygon)
        {
            var inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var a = polygon[i];
                var b = polygon[j];
                if (DistanceToSegment(p, a, b) < 1e-4f) return true;
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        static bool StrictlyInsideConvex(Vector2 p, IReadOnlyList<Vector2> rect)
        {
            for (var i = 0; i < rect.Count; i++)
                if (Cross(rect[(i + 1) % rect.Count] - rect[i], p - rect[i]) <= 1e-4f) return false;
            return true;
        }

        // Proper crossing of two segments (touching ends or overlapping edges don't count).
        static bool Crosses(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float d1 = Cross(b - a, c - a), d2 = Cross(b - a, d - a), d3 = Cross(d - c, a - c), d4 = Cross(d - c, b - c);
            return ((d1 > 1e-5f && d2 < -1e-5f) || (d1 < -1e-5f && d2 > 1e-5f)) && ((d3 > 1e-5f && d4 < -1e-5f) || (d3 < -1e-5f && d4 > 1e-5f));
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = ab.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0;
            return Vector2.Distance(p, a + ab * t);
        }

        static float Area(IReadOnlyList<Vector2> polygon)
        {
            var a = 0f;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++) a += Cross(polygon[j], polygon[i]);
            return a / 2;
        }

        static Vector2 Centroid(IReadOnlyList<Vector2> polygon)
        {
            var area = Area(polygon);
            var c = Vector2.zero;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var f = Cross(polygon[j], polygon[i]);
                c += (polygon[j] + polygon[i]) * f;
            }
            return c / (6 * area);
        }
    }
}
