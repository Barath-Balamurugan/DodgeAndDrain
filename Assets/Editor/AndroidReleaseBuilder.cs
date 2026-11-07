using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class AndroidReleaseBuilder
{
    public static void BuildNoAdsBundle()
    {
        string outputPath = $"Builds/DrainRun-noads-v{PlayerSettings.Android.bundleVersionCode}.aab";

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

        EditorUserBuildSettings.buildAppBundle = true;
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        var report = BuildPipeline.BuildPlayer(
            new[] { "Assets/Scenes/Main.unity" },
            outputPath,
            BuildTarget.Android,
            BuildOptions.None
        );

        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new Exception($"Android App Bundle build failed: {report.summary.result}");
        }

        UnityEngine.Debug.Log($"Built Android App Bundle: {outputPath}");
    }
}
