// Tiny art kit: built-in primitive meshes and the Blender fruit models (assigned by SceneSetup so they ship in the build),
// procedural meshes (cone, bent tube, star) as the fallback, and a colour -> material cache cloned from scene-referenced base materials.
// No textures, no Asset Store content, no colliders (nothing uses physics).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fruktstand.Game
{
    [System.Serializable]
    public sealed class MeshLibrary
    {
        public Mesh Cube, Sphere, Cylinder, Capsule, Quad;
        public Mesh[] WareModels = new Mesh[Core.WareInfo.Count];
        public Mesh RingModel, RingPegModel; // EPIC-010 play pieces (Models/Ring.fbx, RingPeg.fbx) // Blender exports (Models/<Ware>.fbx) by ware; null -> procedural
        public Mesh FrogModel; // EPIC-018 play piece (Models/Frog.fbx)

        // EPIC-013: the park's Asset Store models by ParkPiece (null = not imported: left out), each with its FBX node's
        // transform (the package's files are in centimetres and turned for Blender), and their one shared material.
        public Mesh[] ParkModels = new Mesh[ParkPieces];
        public Matrix4x4[] ParkBasis = new Matrix4x4[ParkPieces];
        public Material ParkMaterial;
        public const int ParkPieces = (int)ParkPiece.Flowers; // everything before Flowers comes from the package

        public (Mesh mesh, Matrix4x4 basis) ParkModel(ParkPiece p)
        {
            var i = (int)p;
            if (ParkModels == null || i >= ParkModels.Length || ParkModels[i] == null) return (null, Matrix4x4.identity);
            return (ParkModels[i], ParkBasis != null && i < ParkBasis.Length ? ParkBasis[i] : Matrix4x4.identity);
        }
    }

    public sealed class Art
    {
        readonly MeshLibrary meshes;
        readonly Material matte, unlit;
        readonly Dictionary<Color, Material> matteCache = new(), unlitCache = new();
        Mesh cone, banana, star;

        public Font Font { get; }

        public Art(MeshLibrary meshes, Material matte, Material unlit, Font font)
        {
            this.meshes = meshes;
            this.matte = matte != null ? matte : new Material(Shader.Find("Standard"));
            this.unlit = unlit != null ? unlit : new Material(Shader.Find("Unlit/Color"));
            Font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (meshes.Cube == null) meshes.Cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if (meshes.Sphere == null) meshes.Sphere = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
            if (meshes.Cylinder == null) meshes.Cylinder = Resources.GetBuiltinResource<Mesh>("New-Cylinder.fbx");
            if (meshes.Capsule == null) meshes.Capsule = Resources.GetBuiltinResource<Mesh>("New-Capsule.fbx");
            if (meshes.Quad == null) meshes.Quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        }

        public Mesh Cube => meshes.Cube;
        public Mesh Sphere => meshes.Sphere;
        public Mesh Cylinder => meshes.Cylinder; // height 2, radius 0.5
        public Mesh Capsule => meshes.Capsule;
        public Mesh Quad => meshes.Quad;
        public Mesh Cone => cone ??= BuildCone(16);       // base radius 0.5 at y = 0, tip at y = 1
        public Mesh Banana => banana ??= BuildBanana();   // curved tube, about 1 long along x (fallback)
        // A ware's Blender model (metres, pivot at the grab point; submeshes coloured by Models.ModelColours), or null.
        public Mesh RingModel => meshes.RingModel != null ? meshes.RingModel : null;
        public Mesh RingPegModel => meshes.RingPegModel != null ? meshes.RingPegModel : null;
        public Mesh FrogModel => meshes.FrogModel != null ? meshes.FrogModel : null;

        public Mesh ModelOf(Core.Ware w)
        {
            var m = meshes.WareModels != null && (int)w < meshes.WareModels.Length ? meshes.WareModels[(int)w] : null;
            return m != null ? m : null; // Unity's null check: a missing asset reference counts as none
        }
        public Mesh Star => star ??= BuildStar();         // flat five-pointed star, radius 0.5, facing -z

        public Material Matte(Color c)
        {
            if (!matteCache.TryGetValue(c, out var m))
            {
                m = new Material(matte) { color = c, name = "Matte " + ColorUtility.ToHtmlStringRGB(c) };
                matteCache[c] = m;
            }
            return m;
        }

        public Material Unlit(Color c)
        {
            if (!unlitCache.TryGetValue(c, out var m))
            {
                m = new Material(unlit) { color = c, name = "Unlit " + ColorUtility.ToHtmlStringRGB(c) };
                unlitCache[c] = m;
            }
            return m;
        }

        public Transform Part(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 scale, Quaternion? rotation = null, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(position, rotation ?? Quaternion.identity);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go.transform;
        }

        // A multi-material mesh (one material per submesh), e.g. an imported model.
        public Transform Part(Transform parent, string name, Mesh mesh, Material[] materials, Vector3 position, Vector3 scale, Quaternion? rotation = null)
        {
            var t = Part(parent, name, mesh, materials[0], position, scale, rotation);
            t.GetComponent<MeshRenderer>().sharedMaterials = materials;
            return t;
        }

        // Destroy is deferred to the end of the frame; hide it now so a rebuild in the same frame never overlaps.
        public static void Discard(Transform t)
        {
            if (t == null) return;
            t.gameObject.SetActive(false);
            t.SetParent(null, false);
            Object.Destroy(t.gameObject);
        }

        public Transform Group(Transform parent, string name, Vector3 position = default)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            return t;
        }

        // TextMesh reads correctly when its forward points away from the viewer.
        public TextMesh Text(Transform parent, string name, string text, Vector3 position, float characterSize, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var t = go.AddComponent<TextMesh>();
            t.font = Font;
            t.fontSize = 96;
            t.characterSize = characterSize;
            t.anchor = anchor;
            t.alignment = TextAlignment.Center;
            t.color = color;
            t.text = text;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = Font.material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return t;
        }

        static Mesh BuildCone(int sides)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var tris = new List<int>();
            for (var i = 0; i < sides; i++)
            {
                var a0 = i * Mathf.PI * 2 / sides;
                var a1 = (i + 1) * Mathf.PI * 2 / sides;
                var p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0, Mathf.Sin(a0) * 0.5f);
                var p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0, Mathf.Sin(a1) * 0.5f);
                var tip = Vector3.up;
                var normal = Vector3.Cross(tip - p0, p1 - p0).normalized;
                if (normal.y < 0) normal = -normal;
                var b = v.Count;
                v.Add(p0); v.Add(tip); v.Add(p1);
                n.Add(normal); n.Add(normal); n.Add(normal);
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                // base
                b = v.Count;
                v.Add(Vector3.zero); v.Add(p0); v.Add(p1);
                n.Add(Vector3.down); n.Add(Vector3.down); n.Add(Vector3.down);
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
            }
            var mesh = new Mesh { name = "Cone" };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetTriangles(tris, 0);
            FixWinding(mesh);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Tube along a gentle arc, thick in the middle and thinner at the ends.
        static Mesh BuildBanana()
        {
            const int rings = 14, sides = 10;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var tris = new List<int>();
            for (var r = 0; r <= rings; r++)
            {
                var t = (float)r / rings;
                var angle = Mathf.Lerp(-0.7f, 0.7f, t);
                var centre = new Vector3(Mathf.Sin(angle) * 0.6f, -Mathf.Cos(angle) * 0.6f + 0.6f, 0);
                var tangent = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                var radius = 0.11f * Mathf.Sqrt(Mathf.Sin(Mathf.PI * Mathf.Lerp(0.06f, 0.94f, t)));
                var side = Vector3.forward;
                var up = Vector3.Cross(side, tangent).normalized;
                for (var s = 0; s < sides; s++)
                {
                    var a = s * Mathf.PI * 2 / sides;
                    var dir = Mathf.Cos(a) * up + Mathf.Sin(a) * side;
                    v.Add(centre + dir * radius);
                    n.Add(dir);
                }
            }
            for (var r = 0; r < rings; r++)
                for (var s = 0; s < sides; s++)
                {
                    int a = r * sides + s, b = r * sides + (s + 1) % sides, c = a + sides, d = b + sides;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            var mesh = new Mesh { name = "Banana" };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetTriangles(tris, 0);
            FixWinding(mesh);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh BuildStar()
        {
            var v = new List<Vector3> { Vector3.zero };
            for (var i = 0; i < 10; i++)
            {
                var a = Mathf.PI / 2 + i * Mathf.PI / 5;
                var r = i % 2 == 0 ? 0.5f : 0.21f;
                v.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0));
            }
            var tris = new List<int>();
            for (var i = 0; i < 10; i++)
            {
                // both faces, so it reads from either side
                tris.Add(0); tris.Add(1 + i); tris.Add(1 + (i + 1) % 10);
                tris.Add(0); tris.Add(1 + (i + 1) % 10); tris.Add(1 + i);
            }
            var mesh = new Mesh { name = "Star" };
            mesh.SetVertices(v);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Makes every triangle face along its vertex normals (Unity front faces are clockwise seen from outside).
        static void FixWinding(Mesh mesh)
        {
            var v = mesh.vertices;
            var n = mesh.normals;
            var t = mesh.triangles;
            for (var i = 0; i < t.Length; i += 3)
            {
                var face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                var avg = n[t[i]] + n[t[i + 1]] + n[t[i + 2]];
                if (Vector3.Dot(face, avg) < 0) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
            }
            mesh.triangles = t;
        }
    }
}
