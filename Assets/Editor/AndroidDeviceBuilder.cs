using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

// Builds a debug-signed APK for installing straight onto a test phone over USB
// (adb install), without needing the upload keystore's password.
// Store builds still go through AndroidReleaseBuilder and stay keystore-signed.
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

            BuildReport report = BuildPipeline.BuildPlayer(
                new[] { "Assets/Scenes/Main.unity" },
                OutputPath,
                BuildTarget.Android,
                BuildOptions.None
            );

            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception($"Device APK build failed: {report.summary.result}");

            UnityEngine.Debug.Log($"Built device APK: {OutputPath}");
        }
        finally
        {
            // Put the release signing setup back exactly as it was.
            EditorUserBuildSettings.buildAppBundle = previousAppBundle;
            PlayerSettings.Android.useCustomKeystore = previousCustomKeystore;
        }
    }
}
