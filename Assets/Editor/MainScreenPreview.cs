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

    // How to play screen. Videos don't play in the editor, so pass still frames:
    //   -executeMethod MainScreenPreview.RenderTutorial -previewOut out.png
    //   -previewStills move.png,drain.png,shoot.png,heat.png [-previewBack true]
    public static void RenderTutorial()
    {
        string outPath = GetArg("-previewOut") ?? "Temp/HowToPlayPreview.png";
        string[] stillPaths = (GetArg("-previewStills") ?? string.Empty).Split(',');
        bool withBack = (GetArg("-previewBack") ?? "true") == "true";

        RenderScene(outPath, 1560, 720, () =>
        {
            // Load the stills after the scene opens (opening it unloads loose textures).
            Texture[] stills = new Texture[stillPaths.Length];
            for (int i = 0; i < stillPaths.Length; i++)
            {
                if (!File.Exists(stillPaths[i]))
                    continue;
                Texture2D still = new Texture2D(2, 2);
                still.LoadImage(File.ReadAllBytes(stillPaths[i]));
                stills[i] = still;
            }

            HowToPlayScreen screen = Object.FindFirstObjectByType<HowToPlayScreen>(FindObjectsInactive.Include);
            if (screen == null)
                return false;
            screen.gameObject.SetActive(true);
            screen.BuildForPreview(withBack, stills);
            return true;
        });
    }

    // Game Over screen with sample results:
    //   -executeMethod MainScreenPreview.RenderGameOver -previewOut out.png
    //   [-previewRevive true|false] [-previewNewBest true|false] [-previewSize 1920x1080]
    public static void RenderGameOver()
    {
        string outPath = GetArg("-previewOut") ?? "Temp/GameOverPreview.png";
        bool canRevive = (GetArg("-previewRevive") ?? "true") == "true";
        bool newBest = (GetArg("-previewNewBest") ?? "true") == "true";
        int width = 1560, height = 720;
        string size = GetArg("-previewSize");
        if (size != null)
        {
            string[] parts = size.ToLowerInvariant().Split('x');
            width = int.Parse(parts[0]);
            height = int.Parse(parts[1]);
        }

        RenderScene(outPath, width, height, () =>
        {
            GameManager gameManager = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
            if (gameManager == null || gameManager.gameOverPanel == null)
                return false;

            // Only the Game Over screen over the arena, as in play.
            MainScreen mainScreen = Object.FindFirstObjectByType<MainScreen>(FindObjectsInactive.Include);
            if (mainScreen != null)
                mainScreen.gameObject.SetActive(false);

            gameManager.gameOverPanel.SetActive(true);
            GameOverScreen screen = GameOverScreen.AttachTo(gameManager.gameOverPanel);
            screen.Show(new GameOverResult
            {
                score = 412,
                bestScore = newBest ? 412 : 512,
                newBest = newBest,
                wave = 4,
                kills = 37,
                bestMultiplier = 3,
                seconds = 134f,
                tip = GameOverScreen.TipFor(DeathCause.Unknown, true, false),
                canRevive = canRevive
            }, skipEntrance: true);
            return true;
        });
    }

    // Ad consent card over the main screen:
    //   -executeMethod MainScreenPreview.RenderConsent -previewOut out.png
    public static void RenderConsent()
    {
        string outPath = GetArg("-previewOut") ?? "Temp/ConsentPreview.png";
        RenderScene(outPath, 1560, 720, () =>
        {
            MainScreen mainScreen = Object.FindFirstObjectByType<MainScreen>(FindObjectsInactive.Include);
            if (mainScreen == null)
                return false;
            mainScreen.BuildForPreview(964f, 7, 1.3f);
            mainScreen.ShowAdConsentForPreview();
            return true;
        });
    }

    private static string RenderToFile(string outPath, int width, int height)
    {
        return RenderScene(outPath, width, height, () =>
        {
            MainScreen mainScreen = Object.FindFirstObjectByType<MainScreen>(FindObjectsInactive.Include);
            if (mainScreen == null)
                return false;
            mainScreen.BuildForPreview(326f, 7, 1.3f);
            return true;
        });
    }

    private static string RenderScene(string outPath, int width, int height, System.Func<bool> setup)
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.LogError("Screen preview: Main Camera not found.");
            return null;
        }

        RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = target;

        if (!setup())
        {
            Debug.LogError("Screen preview: screen component not found.");
            return null;
        }

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
