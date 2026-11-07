using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor-only: renders the main (start) screen from the real Main scene to a
// PNG at a landscape phone resolution, without Play mode or a phone. The scene
// is opened but never saved.
//
// Command line:
//   Unity -batchmode -quit -projectPath . -executeMethod MainScreenPreview.Render \
//         -previewOut Temp/MainScreenPreview.png [-previewSize 1560x720]
public static class MainScreenPreview
{
    private const string ScenePath = "Assets/Scenes/Main.unity";

    [MenuItem("Tools/Drain Run/Render Main Screen Preview")]
    public static void RenderFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string path = RenderToFile("Temp/MainScreenPreview.png", 1560, 720);
        EditorUtility.RevealInFinder(path);
    }

    public static void Render()
    {
        string outPath = GetArg("-previewOut") ?? "Temp/MainScreenPreview.png";
        int width = 1560, height = 720;
        string size = GetArg("-previewSize");
        if (size != null)
        {
            string[] parts = size.ToLowerInvariant().Split('x');
            width = int.Parse(parts[0]);
            height = int.Parse(parts[1]);
        }

        RenderToFile(outPath, width, height);
    }

    private static string RenderToFile(string outPath, int width, int height)
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        MainScreen mainScreen = Object.FindFirstObjectByType<MainScreen>(FindObjectsInactive.Include);
        Camera camera = Camera.main;
        if (mainScreen == null || camera == null)
        {
            Debug.LogError("Main screen preview: MainScreen or Main Camera not found.");
            return null;
        }

        RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = target;

        mainScreen.BuildForPreview(326f, 7, 1.3f);

        Canvas.ForceUpdateCanvases();
        foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            text.ForceMeshUpdate(true, true);
        Canvas.ForceUpdateCanvases();

        camera.Render();
        RenderTexture.active = target;
        Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        RenderTexture.active = null;
        camera.targetTexture = null;

        string fullPath = Path.GetFullPath(outPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllBytes(fullPath, image.EncodeToPNG());
        Debug.Log($"Main screen preview written to {fullPath}");
        return fullPath;
    }

    private static string GetArg(string name)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
                return args[i + 1];
        }

        return null;
    }
}
