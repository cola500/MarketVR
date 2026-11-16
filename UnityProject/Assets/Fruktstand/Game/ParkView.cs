// EPIC-013: draws the park (ParkLayout) as a child of the market root, so it follows every explicit placement and
// never the counters' height or the player's walking. Built once, cheaply for Quest: the package models share one
// material and are merged into one mesh (one draw); the gravel path, the soft shade under trees and bench and the
// flower beds are a few flat or low-poly meshes merged per colour. No colliders, no real-time shadows (the shade discs
// stand in for them), nothing grabbable. A model missing from the build (package not imported) is simply left out.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fruktstand.Game
{
    public static class ParkView
    {
        public static readonly Color Gravel = new(0.8f, 0.7f, 0.53f);
        public static readonly Color Shade = new(0.31f, 0.5f, 0.22f);
        static readonly Color Stem = new(0.3f, 0.6f, 0.25f);
        static readonly Color Leaves = new(0.3f, 0.56f, 0.25f);
        static readonly Color BushDark = new(0.27f, 0.5f, 0.24f), BushLight = new(0.38f, 0.6f, 0.3f);
        static readonly Color HillNear = new(0.42f, 0.6f, 0.32f), HillFar = new(0.52f, 0.66f, 0.47f); // farther = paler (air)
        static readonly Color[] Petals = { new(1f, 0.55f, 0.72f), new(1f, 0.86f, 0.25f), new(0.98f, 0.97f, 0.94f), new(0.72f, 0.55f, 0.95f), new(1f, 0.45f, 0.35f) };

        public static Transform Build(Art art, Transform marketRoot, MeshLibrary meshes)
        {
            var root = art.Group(marketRoot, "Park");
            var path = Flat(root, "Gravel Path", PathMesh(), art.Matte(Gravel), ParkLayout.PathLift);
            path.GetComponent<MeshRenderer>().receiveShadows = true; // the stalls' and figures' shadows land on it
            Flat(root, "Shade", ShadeMesh(meshes), art.Matte(Shade), ParkLayout.PathLift * 0.5f);
            Models(art, root, meshes);
            Shapes(art, root);
            return root;
        }

        static Transform Flat(Transform parent, string name, Mesh mesh, Material material, float lift)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * lift;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        // ------------------------------------------------------------------ package models: one merged mesh

        static Matrix4x4 Placement(ParkItem i) => Matrix4x4.TRS(i.Position, Quaternion.Euler(0, i.Yaw, 0), Vector3.one * i.Scale);

        static void Models(Art art, Transform root, MeshLibrary meshes)
        {
            if (meshes.ParkMaterial == null) return;
            var parts = new List<CombineInstance>();
            foreach (var item in ParkLayout.Items)
            {
                var (mesh, basis) = meshes.ParkModel(item.Piece);
                if (mesh == null) continue;
                var at = Placement(item) * basis;
                if (!mesh.isReadable)
                {
                    // not merged (an import setting was lost): still drawn, just one draw each
                    art.Part(root, item.Piece.ToString(), mesh, meshes.ParkMaterial, at.GetPosition(), at.lossyScale, at.rotation, false);
                    continue;
                }
                parts.Add(new CombineInstance { mesh = mesh, subMeshIndex = 0, transform = at });
            }
            if (parts.Count == 0) return;
            var merged = new Mesh { name = "Park Models", indexFormat = IndexFormat.UInt32 };
            merged.CombineMeshes(parts.ToArray(), true, true);
            merged.RecalculateBounds();
            var go = Flat(root, "Park Models", merged, meshes.ParkMaterial, 0);
            go.GetComponent<MeshRenderer>().receiveShadows = false;
        }

        // ------------------------------------------------------------------ gravel path (flat, market space)

        // The ring between two rounded squares, sampled along rays from the centre (both outlines are star-shaped),
        // and the front and back arms with round ends.
        static Mesh PathMesh()
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            const int steps = 128;
            for (var i = 0; i < steps; i++)
            {
                var a = i * Mathf.PI * 2 / steps;
                var dir = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                v.Add(OnRoundedSquare(dir, ParkLayout.RingInner, ParkLayout.RingInnerCorner));
                v.Add(OnRoundedSquare(dir, ParkLayout.RingOuter, ParkLayout.RingOuterCorner));
            }
            for (var i = 0; i < steps; i++)
            {
                int a = i * 2, b = (i + 1) % steps * 2;
                Quad(v, t, a, a + 1, b + 1, b);
            }
            foreach (var side in new[] { 1f, -1f })
            {
                float near = side * (ParkLayout.RingOuter - 0.1f), far = side * (ParkLayout.ArmEnd - ParkLayout.ArmHalfWidth), w = ParkLayout.ArmHalfWidth;
                var b = v.Count;
                v.Add(new Vector3(-w, 0, near)); v.Add(new Vector3(w, 0, near)); v.Add(new Vector3(w, 0, far)); v.Add(new Vector3(-w, 0, far));
                Quad(v, t, b, b + 1, b + 2, b + 3);
                Disc(v, t, new Vector3(0, 0, far), w, 16);
            }
            return FlatMesh("Gravel Path", v, t);
        }

        // Where a ray from the centre leaves a rounded square of half size `half` and corner radius `corner`.
        static Vector3 OnRoundedSquare(Vector3 dir, float half, float corner)
        {
            float lo = 0, hi = half * 1.5f;
            for (var k = 0; k < 30; k++)
            {
                var mid = (lo + hi) / 2;
                if (ParkLayout.InRoundedSquare(dir * mid, half, corner)) lo = mid; else hi = mid;
            }
            return dir * lo;
        }

        // Soft round shade under each tree and the bench (instead of real-time shadows).
        static Mesh ShadeMesh(MeshLibrary meshes)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            foreach (var item in ParkLayout.Items)
            {
                var r = item.Piece switch
                {
                    ParkPiece.SphericalTree or ParkPiece.RoundTree or ParkPiece.OakTree or ParkPiece.LeafTree => 1.25f * item.Scale + 0.2f,
                    ParkPiece.BirchTree or ParkPiece.TallTree => 0.9f * item.Scale + 0.2f,
                    ParkPiece.Bench => 0.75f,
                    ParkPiece.Bush => 0.8f * item.Scale,
                    _ => 0f,
                };
                var drawn = item.Piece == ParkPiece.Bush || meshes.ParkModel(item.Piece).mesh != null;
                if (r > 0 && drawn) Disc(v, t, item.Position, r, 20);
            }
            return FlatMesh("Shade", v, t);
        }

        static void Disc(List<Vector3> v, List<int> t, Vector3 centre, float radius, int sides)
        {
            var c = v.Count;
            v.Add(centre);
            for (var i = 0; i < sides; i++)
            {
                var a = i * Mathf.PI * 2 / sides;
                v.Add(centre + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * radius);
            }
            for (var i = 0; i < sides; i++) Up(v, t, c, c + 1 + i, c + 1 + (i + 1) % sides);
        }

        static void Quad(List<Vector3> v, List<int> t, int a, int b, int c, int d)
        {
            Up(v, t, a, b, c);
            Up(v, t, a, c, d);
        }

        // A triangle facing up (Unity draws clockwise seen from above).
        static void Up(List<Vector3> v, List<int> t, int a, int b, int c)
        {
            if (Vector3.Cross(v[b] - v[a], v[c] - v[a]).y < 0) (b, c) = (c, b);
            t.Add(a); t.Add(b); t.Add(c);
        }

        static Mesh FlatMesh(string name, List<Vector3> v, List<int> t)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            var up = new Vector3[v.Count];
            for (var i = 0; i < up.Length; i++) up[i] = Vector3.up;
            mesh.normals = up;
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ flower beds, bushes, hills: merged per colour

        // EPIC-014: soft low hills far out (55-80 m), all round, break the empty horizon. Out of reach, no footprints.
        static readonly (float angle, float distance, float width, float height)[] Hills =
        {
            (0, 75, 46, 9), (28, 62, 34, 7), (55, 78, 50, 11), (85, 66, 40, 8), (112, 80, 48, 12), (140, 64, 36, 7),
            (170, 76, 44, 10), (200, 60, 34, 7), (228, 74, 46, 9), (258, 66, 40, 8), (290, 79, 52, 12), (322, 63, 36, 8),
        };

        static void Shapes(Art art, Transform root)
        {
            var head = LowSphere(8, 5);
            var hill = LowSphere(12, 6);
            var colours = new List<Color> { Leaves, Stem };
            colours.AddRange(Petals);
            colours.AddRange(new[] { BushDark, BushLight, HillNear, HillFar });
            int bushDark = colours.Count - 4, bushLight = bushDark + 1, hillNear = bushDark + 2, hillFar = bushDark + 3;
            var byColour = new List<CombineInstance>[colours.Count];
            for (var i = 0; i < byColour.Length; i++) byColour[i] = new List<CombineInstance>();
            var bed = 0;
            foreach (var item in ParkLayout.Items)
            {
                if (item.Piece != ParkPiece.Flowers) continue;
                var place = Placement(item);
                // a low green mound with seven flowers on short stems, two colours per bed
                byColour[0].Add(Part(head, place * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.46f, 0.1f, 0.38f))));
                for (var k = 0; k < 7; k++)
                {
                    var a = k * 2.4f + bed;
                    var r = k == 0 ? 0 : 0.11f + 0.05f * (k % 3);
                    var h = 0.14f + 0.04f * ((k * 7 + bed) % 3);
                    var at = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r * 0.85f);
                    byColour[1].Add(Part(head, place * Matrix4x4.TRS(at + Vector3.up * h / 2, Quaternion.identity, new Vector3(0.012f, h / 2, 0.012f))));
                    var petal = 2 + (bed * 2 + k % 2) % Petals.Length;
                    byColour[petal].Add(Part(head, place * Matrix4x4.TRS(at + Vector3.up * h, Quaternion.identity, new Vector3(0.09f, 0.055f, 0.09f))));
                }
                bed++;
            }
            var n = 0;
            foreach (var item in ParkLayout.Items)
            {
                if (item.Piece != ParkPiece.Bush) continue;
                // three round blobs: a big one and two smaller ones beside it, two greens
                var place = Placement(item);
                byColour[n % 2 == 0 ? bushDark : bushLight].Add(Part(head, place * Matrix4x4.TRS(new Vector3(0, 0.32f, 0), Quaternion.identity, new Vector3(1.1f, 0.8f, 1f))));
                byColour[n % 2 == 0 ? bushLight : bushDark].Add(Part(head, place * Matrix4x4.TRS(new Vector3(0.45f, 0.22f, 0.15f), Quaternion.identity, new Vector3(0.7f, 0.55f, 0.65f))));
                byColour[bushDark].Add(Part(head, place * Matrix4x4.TRS(new Vector3(-0.4f, 0.2f, -0.1f), Quaternion.identity, new Vector3(0.65f, 0.5f, 0.6f))));
                n++;
            }
            foreach (var (angle, distance, width, height) in Hills)
            {
                var a = angle * Mathf.Deg2Rad;
                var at = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * distance + Vector3.down * height * 0.35f; // partly under the lawn
                var c = distance > 70 ? hillFar : hillNear;
                byColour[c].Add(Part(hill, Matrix4x4.TRS(at, Quaternion.Euler(0, angle, 0), new Vector3(width, height * 1.6f, width * 0.55f))));
            }
            var subs = new List<Mesh>();
            var mats = new List<Material>();
            for (var i = 0; i < colours.Count; i++)
            {
                if (byColour[i].Count == 0) continue;
                var m = new Mesh { indexFormat = IndexFormat.UInt32 };
                m.CombineMeshes(byColour[i].ToArray(), true, true);
                subs.Add(m);
                mats.Add(art.Matte(colours[i]));
            }
            if (subs.Count == 0) return;
            var all = new CombineInstance[subs.Count];
            for (var i = 0; i < all.Length; i++) all[i] = new CombineInstance { mesh = subs[i], transform = Matrix4x4.identity };
            var beds = new Mesh { name = "Park Shapes", indexFormat = IndexFormat.UInt32 };
            beds.CombineMeshes(all, false, true); // one submesh per colour
            beds.RecalculateBounds();
            foreach (var m in subs) Object.Destroy(m);
            var t = Flat(root, "Park Shapes", beds, mats[0], 0);
            t.GetComponent<MeshRenderer>().sharedMaterials = mats.ToArray();
            Object.Destroy(head);
            Object.Destroy(hill);
        }

        static CombineInstance Part(Mesh mesh, Matrix4x4 at) => new() { mesh = mesh, transform = at };

        // A unit-diameter sphere with few triangles (a flower head is 6 cm: the built-in sphere's 768 would be waste).
        static Mesh LowSphere(int sides, int rows)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            for (var r = 0; r <= rows; r++)
            {
                var phi = Mathf.PI * r / rows;
                for (var s = 0; s <= sides; s++)
                {
                    var th = Mathf.PI * 2 * s / sides;
                    v.Add(new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th)) * 0.5f);
                }
            }
            for (var r = 0; r < rows; r++)
                for (var s = 0; s < sides; s++)
                {
                    int a = r * (sides + 1) + s, b = a + sides + 1;
                    Outward(v, t, a, b, a + 1);
                    Outward(v, t, a + 1, b, b + 1);
                }
            var mesh = new Mesh { name = "Low Sphere" };
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void Outward(List<Vector3> v, List<int> t, int a, int b, int c)
        {
            var n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (n.sqrMagnitude < 1e-12f) return; // the poles' empty triangles
            if (Vector3.Dot(n, v[a] + v[b] + v[c]) < 0) (b, c) = (c, b);
            t.Add(a); t.Add(b); t.Add(c);
        }
    }
}
