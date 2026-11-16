// SPIKE-007 / EPIC-007: what the existing XR stack tells us about the room's play area on a real Quest. Logs, and
// returns, both sources without adding an SDK:
// - UnityEngine.XR.XRInputSubsystem.TryGetBoundaryPoints: the boundary polygon in tracking space (if the provider
//   gives one);
// - Unity.XR.Oculus.Boundary: configured/visible and the axis-aligned dimensions of PlayArea / OuterBoundary
//   (size only: no position or orientation).
// Nothing here changes the system boundary; it is only read.
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.XR;

namespace Fruktstand.Game
{
    public static class BoundaryProbe
    {
        public sealed class Reading
        {
            public readonly List<Vector3> Points = new(); // tracking space, from TryGetBoundaryPoints
            public bool? Configured;
            public bool? Visible;
            public Vector3? PlayArea;  // Oculus PlayArea dimensions (x, y, z)
            public Vector3? Outer;     // Oculus OuterBoundary dimensions
            public string Origin = "";
            public bool HasPolygon => Points.Count >= 3;
        }

        public static Reading Read()
        {
            var r = new Reading();
            var subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var s in subsystems)
            {
                r.Origin = s.GetTrackingOriginMode().ToString();
                var points = new List<Vector3>();
                if (s.TryGetBoundaryPoints(points) && points.Count > r.Points.Count)
                {
                    r.Points.Clear();
                    r.Points.AddRange(points);
                }
            }
            try
            {
                r.Configured = Unity.XR.Oculus.Boundary.GetBoundaryConfigured();
                r.Visible = Unity.XR.Oculus.Boundary.GetBoundaryVisible();
                if (Unity.XR.Oculus.Boundary.GetBoundaryDimensions(Unity.XR.Oculus.Boundary.BoundaryType.PlayArea, out var play)) r.PlayArea = play;
                if (Unity.XR.Oculus.Boundary.GetBoundaryDimensions(Unity.XR.Oculus.Boundary.BoundaryType.OuterBoundary, out var outer)) r.Outer = outer;
            }
            catch (System.Exception e)
            {
                Debug.Log($"[Fruktstand] boundary: Oculus Boundary API threw {e.GetType().Name}: {e.Message}");
            }
            return r;
        }

        public static string Describe(Reading r)
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append($"origin={r.Origin} configured={r.Configured?.ToString() ?? "?"} visible={r.Visible?.ToString() ?? "?"}");
            sb.Append($" playArea={(r.PlayArea is Vector3 p ? p.ToString("0.00") : "none")} outer={(r.Outer is Vector3 o ? o.ToString("0.00") : "none")}");
            sb.Append($" points={r.Points.Count}");
            if (r.Points.Count > 0)
            {
                var min = r.Points[0];
                var max = r.Points[0];
                foreach (var q in r.Points)
                {
                    min = Vector3.Min(min, q);
                    max = Vector3.Max(max, q);
                }
                sb.Append($" bbox x {min.x.ToString("0.00", c)}..{max.x.ToString("0.00", c)} z {min.z.ToString("0.00", c)}..{max.z.ToString("0.00", c)} y {min.y.ToString("0.00", c)}..{max.y.ToString("0.00", c)}");
                sb.Append(" pts");
                var step = Mathf.Max(1, r.Points.Count / 16); // at most ~16 points in the log
                for (var i = 0; i < r.Points.Count; i += step)
                    sb.Append($" ({r.Points[i].x.ToString("0.00", c)},{r.Points[i].z.ToString("0.00", c)})");
            }
            return sb.ToString();
        }

        public static Reading Log(string when)
        {
            var r = Read();
            Debug.Log($"[Fruktstand] boundary ({when}): {Describe(r)}");
            return r;
        }
    }
}
