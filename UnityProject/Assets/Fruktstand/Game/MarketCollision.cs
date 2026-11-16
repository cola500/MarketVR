// Simple floor footprints of the counters and the play table (EPIC-005) and the park's trees, bench and stones
// (EPIC-013), in market space. Stick walking keeps the
// head (the seated body) BodyRadius away from them and slides along their edges. Only artificial movement is
// constrained: the tracked head is never adjusted, and someone who physically leans or steps too close can always
// walk back out, just not further in.
using System.Collections.Generic;
using Fruktstand.Core;
using UnityEngine;

namespace Fruktstand.Game
{
    public static class MarketCollision
    {
        public const float BodyRadius = 0.18f;
        public const string ShelfName = "Krokodilhyllan"; // EPIC-006
        const int Passes = 3; // corners where two footprints meet

        public readonly struct Footprint
        {
            public Footprint(string name, Vector3 centre, float yaw, Vector2 half)
            {
                Name = name;
                Centre = centre;
                Yaw = yaw;
                Half = half;
            }

            public string Name { get; }
            public Vector3 Centre { get; }
            public float Yaw { get; }
            public Vector2 Half { get; } // x across, y = depth (local z)

            public Vector2 ToLocal(Vector3 p)
            {
                var l = Quaternion.Euler(0, -Yaw, 0) * (p - Centre);
                return new Vector2(l.x, l.z);
            }

            public Vector3 ToMarket(Vector2 local, float y)
            {
                var p = Centre + Quaternion.Euler(0, Yaw, 0) * new Vector3(local.x, 0, local.y);
                p.y = y;
                return p;
            }

            // How far inside the footprint grown by `grow` a point is (0 = outside or on the edge).
            public float Depth(Vector3 p, float grow = BodyRadius)
            {
                var l = ToLocal(p);
                var dx = Half.x + grow - Mathf.Abs(l.x);
                var dz = Half.y + grow - Mathf.Abs(l.y);
                return dx > 0 && dz > 0 ? Mathf.Min(dx, dz) : 0;
            }
        }

        static List<Footprint> footprints, obstacles;
        static int footprintsRevision = -1;

        public static IReadOnlyList<Footprint> Footprints
        {
            get
            {
                if (footprints != null && footprintsRevision == MarketLayout.Revision) return footprints;
                footprintsRevision = MarketLayout.Revision; // the spacing changed (EPIC-007): rebuild
                footprints = FootprintsAt(MarketLayout.StallDistance);
                obstacles = new List<Footprint>(footprints);
                obstacles.AddRange(ParkLayout.Footprints); // EPIC-013: trees, bench and stones stop stick walking too
                return footprints;
            }
        }

        // What stick walking stops at: the market's furniture and the park's trees, bench and stones.
        public static IReadOnlyList<Footprint> Obstacles
        {
            get
            {
                _ = Footprints;
                return obstacles;
            }
        }

        // The footprints at any spacing d (the floor marking and the calibration preview use these too).
        public static List<Footprint> FootprintsAt(float d)
        {
            var list = new List<Footprint>();
            foreach (var c in WareInfo.Categories)
            {
                var (p, yaw) = MarketLayout.Stall(c, d);
                list.Add(new Footprint(WareInfo.Name(c), p, yaw, new Vector2(StandView.CounterSize.x / 2, StandView.CounterSize.z / 2)));
            }
            var (tp, tyaw) = MarketLayout.PlayTableAt(d);
            list.Add(new Footprint("Lekbordet", tp, tyaw, new Vector2(PlayTableView.TableSize.x / 2, PlayTableView.TableSize.z / 2)));
            list.Add(new Footprint(ShelfName, MarketLayout.Local(tp, tyaw, PlayTableView.ShelfCentre), tyaw, new Vector2(PlayTableView.ShelfSize.x / 2, PlayTableView.ShelfSize.z / 2)));
            return list;
        }

        // The allowed end point of a stick move from -> to (market space; y is kept from `to`).
        public static Vector3 Constrain(Vector3 from, Vector3 to)
        {
            for (var pass = 0; pass < Passes; pass++)
            {
                var changed = false;
                foreach (var f in Obstacles)
                {
                    var depthTo = f.Depth(to);
                    if (depthTo <= 0) continue;
                    var depthFrom = f.Depth(from);
                    if (depthFrom > 0)
                    {
                        // Already too close (physically): moving out or along is fine, further in is not.
                        if (depthTo > depthFrom + 1e-5f)
                        {
                            to = new Vector3(from.x, to.y, from.z);
                            changed = true;
                        }
                        continue;
                    }
                    // Push out along the shallower axis: the part of the move along the edge is kept (sliding).
                    var l = f.ToLocal(to);
                    float hx = f.Half.x + BodyRadius, hz = f.Half.y + BodyRadius;
                    if (hx - Mathf.Abs(l.x) < hz - Mathf.Abs(l.y)) l.x = Mathf.Sign(l.x) * hx;
                    else l.y = Mathf.Sign(l.y) * hz;
                    to = f.ToMarket(l, to.y);
                    changed = true;
                }
                if (!changed) break;
            }
            return to;
        }

        public static bool Blocked(Vector3 p)
        {
            foreach (var f in Obstacles) if (f.Depth(p) > 1e-4f) return true;
            return false;
        }
    }
}
