// How every PlayMode test loads the market scene. Synchronous on purpose: with LoadSceneAsync, Unity's preload thread
// can deadlock against the main thread destroying MeshRenderers that use imported assets (the Blender FBX meshes):
// the loader holds PersistentManager's lock and waits for the object lock, the deferred Destroy holds the object lock
// and waits for PersistentManager (AssetChangedNotifier, editor only). Seen 2026-11-01; see docs/TESTING.md.
using System.Collections;
using Fruktstand.Core;
using Fruktstand.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fruktstand.Tests
{
    public static class SceneLoad
    {
        public const string Scene = "FruitStand";

        // Loads a fresh market with these wares on sale (three per category given; the rest classic: apple, banana,
        // carrot / lollipop, polkagris, candy bag / ball, car, blocks), desktop input off.
        public static IEnumerator Fresh(System.Action<FruitStand> done, Ware[] wares = null)
        {
            yield return null; // let deferred Destroy calls of the last test finish first
            FruitStand.StartWares = wares ?? Assortment.Classic;
            SceneManager.LoadScene(Scene); // synchronous: takes effect at the start of the next frame
            yield return null;
            FruitStand.StartWares = null;
            var s = Object.FindAnyObjectByType<FruitStand>();
            if (s != null) s.ReadDesktopInput = false;
            done(s);
        }
    }
}
