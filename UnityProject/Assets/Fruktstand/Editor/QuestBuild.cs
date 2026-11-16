// Quest 3 configuration and APK build. Copied from Magic Tiles (itself mirroring the setup verified in CampfireVR:
// Oculus XR loader on Android only, IL2CPP/ARM64, min SDK 29, target SDK 34, Vulkan) with MarketVR's identity.
// Desktop/Standalone gets no XR loader, so the editor and desktop builds stay non-VR.
// Batch: -buildTarget Android -executeMethod Fruktstand.EditorTools.QuestBuild.Build
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.XR.Oculus;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;

namespace Fruktstand.EditorTools
{
    public static class QuestBuild
    {
        public const string ApplicationId = "com.lillafruktstandet.prototype"; // kept from the old name: new APKs update the app
        public const string ProductName = "MarketVR"; // the name on the headset (formerly Lilla fruktståndet)
        public const string ApkPath = "Builds/LillaFruktstandet-quest.apk";
        const string XrSettingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
        const string OculusSettingsPath = "Assets/XR/Settings/OculusSettings.asset";

        [MenuItem("Fruktstånd/Quest: konfigurera projektet")]
        public static void Configure()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            var android = NamedBuildTarget.Android;
            PlayerSettings.companyName = "Lilla fruktstandet";
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(android, ApplicationId);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
            // EPIC-004: the optional phone-customer link opens sockets on the local network (no internet needed).
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });

            var oculus = OculusSettingsAsset();
            var manager = AndroidXrManager();
            var assigned = XRPackageMetadataStore.AssignLoader(manager, "Unity.XR.Oculus.OculusLoader", BuildTargetGroup.Android);
            EnsurePreloadedXrAssets();
            AssetDatabase.SaveAssets();
            Debug.Log($"[QuestBuild] Configured for Quest: id={ApplicationId}, Oculus loader assigned={assigned || manager.activeLoaders.Count > 0}, Quest 3={oculus.TargetQuest3}");
        }

        // Written just before the build so the app can show its build id (BuildInfo), deleted right after.
        const string BuildInfoPath = "Assets/Fruktstand/Resources/BuildInfo.json";

        // scripts/quest.sh build sets these (Europe/Stockholm time, short git sha, dirty flag). Without them (menu
        // build in the editor) the app shows "okänt" and the APK is not archived.
        static string Env(string name, string fallback) => System.Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : fallback;

        [MenuItem("Fruktstånd/Quest: bygg APK")]
        public static void Build()
        {
            Configure();
            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
            if (File.Exists(ApkPath)) File.Delete(ApkPath);

            // Same application id and versionCode as always (adb install -r accepts an equal versionCode, so every
            // build installs as an update and an archived build can be installed back). Only versionName carries the
            // build id, for this one build, so `adb shell dumpsys package` shows it too; ProjectSettings is restored.
            var buildId = Env("FRUKTSTAND_BUILD_ID", "");
            var versionName = PlayerSettings.bundleVersion;
            var info = new
            {
                buildId,
                builtAt = Env("FRUKTSTAND_BUILT_AT", ""),
                gitCommit = Env("FRUKTSTAND_GIT_COMMIT", ""),
                gitDirty = Env("FRUKTSTAND_GIT_DIRTY", "false") == "true",
            };
            BuildReport report;
            try
            {
                if (buildId.Length > 0)
                {
                    PlayerSettings.bundleVersion = versionName + "+" + buildId;
                    Directory.CreateDirectory(Path.GetDirectoryName(BuildInfoPath));
                    File.WriteAllText(BuildInfoPath, JsonUtility.ToJson(new BuildInfoJson
                    {
                        buildId = info.buildId, builtAt = info.builtAt, gitCommit = info.gitCommit, gitDirty = info.gitDirty,
                    }));
                    AssetDatabase.ImportAsset(BuildInfoPath, ImportAssetOptions.ForceSynchronousImport);
                }
                Debug.Log($"[QuestBuild] build id '{buildId}' · versionName {PlayerSettings.bundleVersion} · versionCode {PlayerSettings.Android.bundleVersionCode}");
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    locationPathName = ApkPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None,
                });
            }
            finally
            {
                PlayerSettings.bundleVersion = versionName;
                if (File.Exists(BuildInfoPath)) AssetDatabase.DeleteAsset(BuildInfoPath);
                var folder = Path.GetDirectoryName(BuildInfoPath);
                if (AssetDatabase.IsValidFolder(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) AssetDatabase.DeleteAsset(folder);
                AssetDatabase.SaveAssets();
            }
            var ok = report.summary.result == BuildResult.Succeeded && File.Exists(ApkPath);
            Debug.Log(ok
                ? $"[QuestBuild] OK · {report.summary.totalSize / (1024 * 1024)} MB · {Path.GetFullPath(ApkPath)}"
                : $"[QuestBuild] FAILED · result={report.summary.result} · errors={report.summary.totalErrors}");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        [System.Serializable]
        sealed class BuildInfoJson
        {
            public string buildId, builtAt, gitCommit;
            public bool gitDirty;
        }

        static OculusSettings OculusSettingsAsset()
        {
            var settings = AssetDatabase.LoadAssetAtPath<OculusSettings>(OculusSettingsPath);
            if (settings == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(OculusSettingsPath));
                settings = ScriptableObject.CreateInstance<OculusSettings>();
                AssetDatabase.CreateAsset(settings, OculusSettingsPath);
            }
            settings.m_StereoRenderingModeAndroid = OculusSettings.StereoRenderingModeAndroid.Multiview;
            settings.TargetQuest2 = true;
            settings.TargetQuest3 = true;
            settings.TargetQuest3S = true;
            EditorUtility.SetDirty(settings);
            EditorBuildSettings.AddConfigObject("Unity.XR.Oculus.Settings", settings, true);
            return settings;
        }

        static XRManagerSettings AndroidXrManager()
        {
            var perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(XrSettingsPath);
            if (perTarget == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(XrSettingsPath));
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, XrSettingsPath);
            }
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);

            var general = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (general == null)
            {
                general = ScriptableObject.CreateInstance<XRGeneralSettings>();
                general.name = "Android Settings";
                var manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                manager.name = "Android Managers";
                general.Manager = manager;
                AssetDatabase.AddObjectToAsset(general, perTarget);
                AssetDatabase.AddObjectToAsset(manager, perTarget);
                perTarget.SetSettingsForBuildTarget(BuildTargetGroup.Android, general);
            }
            general.InitManagerOnStart = true;
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(perTarget);
            return general.Manager;
        }

        // CampfireVR found that Unity 6 + XR Management can drop these from preloadedAssets during a build,
        // which ships an APK where XR never starts. Re-added before and after every build.
        internal static void EnsurePreloadedXrAssets()
        {
            var required = new List<Object>();
            var general = AssetDatabase.LoadAllAssetsAtPath(XrSettingsPath).OfType<XRGeneralSettings>().FirstOrDefault();
            if (general != null) required.Add(general);
            var oculus = AssetDatabase.LoadAssetAtPath<OculusSettings>(OculusSettingsPath);
            if (oculus != null) required.Add(oculus);

            var current = (PlayerSettings.GetPreloadedAssets() ?? new Object[0]).Where(o => o != null).ToList();
            var missing = required.Where(r => !current.Contains(r)).ToList();
            if (missing.Count == 0) return;
            PlayerSettings.SetPreloadedAssets(current.Concat(missing).ToArray());
            AssetDatabase.SaveAssets();
            Debug.Log($"[QuestBuild] Restored {missing.Count} XR entries in preloadedAssets.");
        }
    }

    public sealed class PreloadedXrAssetsGuard : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => int.MaxValue;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android) QuestBuild.EnsurePreloadedXrAssets();
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android) QuestBuild.EnsurePreloadedXrAssets();
        }
    }
}
