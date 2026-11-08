using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

// Builds a debug-signed APK for installing straight onto a test phone over USB
// (adb install), without needing the upload keystore's password.
// Store builds still go through AndroidReleaseBuilder and stay keystore-signed.
// Device builds also show test controls and, until Drain Run's own ad IDs are
// set, use LevelPlay's demo ads (store builds don't).
//
// Command line:
//   Unity -batchmode -quit -projectPath . -buildTarget Android \
//         -executeMethod AndroidDeviceBuilder.BuildDeviceApk
public static class AndroidDeviceBuilder
{
    public const string OutputPath = "Builds/DrainRun-device.apk";

    public static void BuildDeviceApk()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

        bool previousAppBundle = EditorUserBuildSettings.buildAppBundle;
        bool previousCustomKeystore = PlayerSettings.Android.useCustomKeystore;

        try
        {
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            EditorUserBuildSettings.buildAppBundle = false;
            PlayerSettings.Android.useCustomKeystore = false;
            ResolveAndroidDependencies();

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
                // Test builds only: test controls, pacing log and LevelPlay's
                // demo ads until Drain Run's own ad IDs are set (see TestBuild,
                // AdsConfig). -storeLike builds without them (store screenshots).
                extraScriptingDefines = HasArg("-storeLike")
                    ? new string[0]
                    : new[] { "DRAINRUN_TEST_BUILD" }
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception($"Device APK build failed: {report.summary.result}");

            UnityEngine.Debug.Log($"Built device APK: {OutputPath}");
        }
        finally
        {
            // Put the release signing setup back exactly as it was, and save it
            // so a failed build can't leave it changed on disk.
            EditorUserBuildSettings.buildAppBundle = previousAppBundle;
            PlayerSettings.Android.useCustomKeystore = previousCustomKeystore;
            AssetDatabase.SaveAssets();
        }
    }

    // Runs the External Dependency Manager's Android resolver (LevelPlay's SDK
    // and adapters are Maven libraries listed in Assets/LevelPlay/Editor), which
    // adds them to mainTemplate.gradle. In batch mode it doesn't run by itself.
    private static void ResolveAndroidDependencies()
    {
        Type resolver = Type.GetType("GooglePlayServices.PlayServicesResolver, Google.JarResolver");
        if (resolver == null)
        {
            UnityEngine.Debug.LogWarning("Android resolver not found; skipping dependency resolution.");
            return;
        }

        object resolved = resolver.GetMethod("ResolveSync", new[] { typeof(bool) })?.Invoke(null, new object[] { true });
        UnityEngine.Debug.Log($"Android dependency resolution finished: {resolved}");
    }

    private static bool HasArg(string name)
    {
        return Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;
    }
}
