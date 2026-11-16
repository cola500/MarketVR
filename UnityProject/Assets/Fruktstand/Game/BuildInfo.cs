// Which build is running. QuestBuild writes Resources/BuildInfo.json just before an APK build (and deletes it
// afterwards, so the editor never shows a stale id); scripts/quest.sh supplies the id, commit and dirty flag.
// In the editor, or in a build made without the script, the id is "editor" / "okänt".
using UnityEngine;

namespace Fruktstand.Game
{
    public static class BuildInfo
    {
        public const string ResourceName = "BuildInfo";

        [System.Serializable]
        public sealed class Data
        {
            public string buildId;
            public string builtAt;
            public string gitCommit;
            public bool gitDirty;
        }

        static Data data;
        static bool loaded;

        public static Data Current
        {
            get
            {
                if (loaded) return data;
                loaded = true;
                var asset = Resources.Load<TextAsset>(ResourceName);
                if (asset != null)
                {
                    try { data = JsonUtility.FromJson<Data>(asset.text); }
                    catch (System.Exception) { data = null; }
                }
                return data;
            }
        }

        public static string Id => Current?.buildId is { Length: > 0 } id ? id : Application.isEditor ? "editor" : "okänt";

        public static bool Dirty => Current?.gitDirty ?? false;

        // One short line for the menu: "Bygge 2026-11-01_143052_abc1234" (+ a clear mark for local changes).
        public static string MenuLine => "Bygge " + Id + (Dirty ? "  (lokala ändringar)" : "");
    }
}
