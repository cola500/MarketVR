// Generates the stand scene and its few material assets, so the scene file never has to be hand-edited.
// The stand, goods and customers themselves are built at runtime from primitives (FruitStand.Awake); this scene
// only holds the environment, the camera, the XR rig and references to the meshes, base materials and font,
// so they are guaranteed to ship in the Quest build.
// Menu: Fruktstånd > Bygg om scenen. Batch: -executeMethod Fruktstand.EditorTools.SceneSetup.CreateScene
using Fruktstand.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fruktstand.EditorTools
{
    public static class SceneSetup
    {
        public const string ScenePath = "Assets/Scenes/FruitStand.unity";
        const string MaterialDir = "Assets/Fruktstand/Materials";

        [MenuItem("Fruktstånd/Bygg om scenen")]
        public static void CreateScene()
        {
            var matte = MaterialAsset("Matte", "Standard", m => m.SetFloat("_Glossiness", 0.15f));
            var unlit = MaterialAsset("Unlit", "Unlit/Color", null);
            var panel = MaterialAsset("Panel", "Unlit/Color", m => m.color = new Color(0.97f, 0.96f, 0.92f));
            var photo = MaterialAsset("Photo", "Unlit/Texture", null); // the phone customer's portrait (EPIC-004)
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = CreateEnvironment();

            var go = new GameObject("Fruit Stand");
            var stand = go.AddComponent<FruitStand>();
            var hud = go.AddComponent<DesktopHud>();
            Assign(hud, ("stand", stand));
            var rig = CreateXrRig(font, panel);
            Assign(stand, ("viewCamera", camera), ("xrRig", rig), ("hud", hud), ("matte", matte), ("unlit", unlit), ("photo", photo), ("font", font));
            AssignMeshes(stand);

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"Fruktstand: scene written to {ScenePath}");
        }

        const string ModelDir = "Assets/Fruktstand/Models"; // made by art/{fruit,candy,toys}/make_*.py

        // A mesh exported from Blender: no imported materials (colours come from code), no rig or animation.
        // Missing file -> null, and Models falls back to the procedural shape.
        static Mesh ImportedMesh(string path)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
            {
                Debug.LogWarning($"Fruktstand: {path} missing; using the procedural fallback");
                return null;
            }
            if (importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.importAnimation || importer.animationType != ModelImporterAnimationType.None)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = false;
                importer.animationType = ModelImporterAnimationType.None;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        // Unity's built-in primitive meshes, referenced from the scene so they are in the build.
        static void AssignMeshes(FruitStand stand)
        {
            Mesh Primitive(PrimitiveType type)
            {
                var tmp = GameObject.CreatePrimitive(type);
                var mesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(tmp);
                return mesh;
            }
            var so = new SerializedObject(stand);
            var lib = so.FindProperty("meshes");
            lib.FindPropertyRelative("Cube").objectReferenceValue = Primitive(PrimitiveType.Cube);
            lib.FindPropertyRelative("Sphere").objectReferenceValue = Primitive(PrimitiveType.Sphere);
            lib.FindPropertyRelative("Cylinder").objectReferenceValue = Primitive(PrimitiveType.Cylinder);
            lib.FindPropertyRelative("Capsule").objectReferenceValue = Primitive(PrimitiveType.Capsule);
            lib.FindPropertyRelative("Quad").objectReferenceValue = Primitive(PrimitiveType.Quad);
            // EPIC-009/010: the ware models exported from Blender (art/*/make_*.py), by ware index; a missing file = null.
            var models = lib.FindPropertyRelative("WareModels");
            models.arraySize = Core.WareInfo.Count;
            for (var i = 0; i < Core.WareInfo.Count; i++)
            {
                var w = (Core.Ware)i;
                models.GetArrayElementAtIndex(i).objectReferenceValue = ImportedMesh($"{ModelDir}/{w}.fbx");
            }
            lib.FindPropertyRelative("RingModel").objectReferenceValue = ImportedMesh($"{ModelDir}/Ring.fbx");
            lib.FindPropertyRelative("RingPegModel").objectReferenceValue = ImportedMesh($"{ModelDir}/RingPeg.fbx");
            lib.FindPropertyRelative("FrogModel").objectReferenceValue = ImportedMesh($"{ModelDir}/Frog.fbx"); // EPIC-018
            AssignParkModels(lib);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // EPIC-013: the few models taken from the Asset Store package "Low Poly Environment Park" (scripts/park-assets.sh,
        // git-ignored). Read/write so ParkView can merge them into one mesh; the FBX node's transform (centimetres,
        // turned for Blender) is kept as the basis. Missing package -> no park models, the market works as before.
        const string ParkDir = "Assets/Palmov Island/Low Poly Environment Park";

        static string ParkFile(ParkPiece p) => p switch
        {
            ParkPiece.SphericalTree => "Models/Trees/spherical tree.fbx",
            ParkPiece.RoundTree => "Models/Trees/tree round foliage.fbx",
            ParkPiece.OakTree => "Models/Trees/tree oak.fbx",
            ParkPiece.LeafTree => "Models/Trees/deciduous tree.fbx",
            ParkPiece.Bench => "Models/Environment/bench 1.fbx",
            ParkPiece.Grass => "Models/Grass/grass 3.fbx",
            ParkPiece.TallGrass => "Models/Grass/grass 4.fbx",
            ParkPiece.Stone => "Models/Stones/stone 1.fbx",
            ParkPiece.BigStone => "Models/Stones/stone 3.fbx",
            ParkPiece.BirchTree => "Models/Trees/birch tree.fbx",       // EPIC-014
            ParkPiece.TallTree => "Models/Trees/elongated tree.fbx",
            _ => throw new System.ArgumentOutOfRangeException(nameof(p)),
        };

        static void AssignParkModels(SerializedProperty lib)
        {
            var models = lib.FindPropertyRelative("ParkModels");
            var basis = lib.FindPropertyRelative("ParkBasis");
            models.arraySize = basis.arraySize = MeshLibrary.ParkPieces;
            var found = 0;
            for (var i = 0; i < MeshLibrary.ParkPieces; i++)
            {
                var path = $"{ParkDir}/{ParkFile((ParkPiece)i)}";
                Mesh mesh = null;
                var node = Matrix4x4.identity;
                if (AssetImporter.GetAtPath(path) is ModelImporter importer)
                {
                    if (importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.importAnimation ||
                        importer.animationType != ModelImporterAnimationType.None || !importer.isReadable)
                    {
                        importer.materialImportMode = ModelImporterMaterialImportMode.None;
                        importer.importAnimation = false;
                        importer.animationType = ModelImporterAnimationType.None;
                        importer.isReadable = true;
                        importer.SaveAndReimport();
                    }
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var filter = model != null ? model.GetComponentInChildren<MeshFilter>() : null;
                    if (filter != null)
                    {
                        mesh = filter.sharedMesh;
                        node = model.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                        found++;
                    }
                }
                models.GetArrayElementAtIndex(i).objectReferenceValue = mesh;
                SetMatrix(basis.GetArrayElementAtIndex(i), node);
            }
            lib.FindPropertyRelative("ParkMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>($"{ParkDir}/Materials/mat main.mat");
            if (found < MeshLibrary.ParkPieces) Debug.LogWarning($"Fruktstand: {found}/{MeshLibrary.ParkPieces} park models found; run scripts/park-assets.sh for the park");
        }

        static void SetMatrix(SerializedProperty p, Matrix4x4 m)
        {
            for (var r = 0; r < 4; r++)
                for (var c = 0; c < 4; c++)
                    p.FindPropertyRelative($"e{r}{c}").floatValue = m[r, c];
        }

        // Sunny outdoor setting from Magic Tiles, deliberately cheap for Quest: built-in procedural sky, one
        // hard-shadowed sun, three-colour ambient (no baked lighting, no post effects) and a flat matte lawn. The park
        // round the market (trees, bench, path, flowers; EPIC-013) belongs to the market and is built at runtime
        // (ParkView), so it follows the market's placement; this plane is only the endless lawn beyond it.
        static Camera CreateEnvironment()
        {
            var sky = MaterialAsset("Sky", "Skybox/Procedural", m =>
            {
                m.SetFloat("_SunSize", 0.035f);
                m.SetFloat("_AtmosphereThickness", 0.8f);
                m.SetColor("_SkyTint", new Color(0.55f, 0.7f, 0.95f));
                m.SetColor("_GroundColor", new Color(0.34f, 0.47f, 0.27f)); // below the hills: the lawn, not sand (EPIC-014)
                m.SetFloat("_Exposure", 1.25f);
            });
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.6f, 0.65f, 0.74f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.57f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.36f, 0.27f);
            RenderSettings.fog = false;

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = 0.02f;
            camera.farClipPlane = 300;
            cameraGo.AddComponent<AudioListener>();

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 0.9f;
            light.shadows = LightShadows.Hard;
            light.shadowStrength = 0.55f;
            lightGo.transform.rotation = Quaternion.Euler(55, -35, 0);
            RenderSettings.sun = light;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.transform.localScale = new Vector3(40, 1, 40);
            ground.GetComponent<MeshRenderer>().sharedMaterial = MaterialAsset("Ground", "Standard", m =>
            {
                m.color = new Color(0.39f, 0.6f, 0.27f); // the park trees' green (EPIC-013/014)
                m.SetFloat("_Glossiness", 0f);
            });

            return camera;
        }

        // VR-only objects, inactive until FruitStand detects an XR device.
        static XrRig CreateXrRig(Font font, Material panelMaterial)
        {
            var rigGo = new GameObject("XR Rig");
            var rig = rigGo.AddComponent<XrRig>();

            var menuGo = new GameObject("Menu");
            menuGo.transform.SetParent(rigGo.transform, false);
            var text = menuGo.AddComponent<TextMesh>();
            text.font = font;
            text.fontSize = 64;
            text.characterSize = 0.003f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Left;
            text.color = new Color(0.1f, 0.1f, 0.12f);
            var textRenderer = menuGo.GetComponent<MeshRenderer>();
            textRenderer.sharedMaterial = font.material;
            textRenderer.shadowCastingMode = ShadowCastingMode.Off;
            textRenderer.receiveShadows = false;
            var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.name = "Panel";
            Object.DestroyImmediate(panel.GetComponent<Collider>());
            panel.transform.SetParent(menuGo.transform, false);
            panel.transform.localPosition = new Vector3(0, 0, 0.003f);
            panel.transform.localScale = new Vector3(0.42f, 0.32f, 1);
            panel.GetComponent<MeshRenderer>().sharedMaterial = panelMaterial;
            panel.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var menuPanel = menuGo.AddComponent<MenuPanel>();
            // EPIC-012: the menu is drawn on top of the world (materials as assets, so the shader ships in the build).
            var overlay = MaterialAsset("MenuOverlay", "Fruktstand/MenuOverlay", null);
            var overlayText = MaterialAsset("MenuOverlayText", "Fruktstand/MenuOverlay", m => m.SetFloat("_Text", 1));
            Assign(menuPanel, ("text", text), ("overlay", overlay), ("overlayText", overlayText));
            menuGo.SetActive(false);

            Assign(rig, ("menuPanel", menuPanel));
            rigGo.SetActive(false);
            return rig;
        }

        static void Assign(Object target, params (string field, Object value)[] values)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in values)
            {
                var prop = so.FindProperty(field);
                if (prop == null) throw new System.ArgumentException($"{target.GetType().Name} has no field {field}");
                prop.objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Material MaterialAsset(string name, string shader, System.Action<Material> configure)
        {
            System.IO.Directory.CreateDirectory(MaterialDir);
            var path = $"{MaterialDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shader)) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            configure?.Invoke(material);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
