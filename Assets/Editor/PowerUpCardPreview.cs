using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Editor-only: renders the power-up card screen to a PNG without entering
// Play mode or needing a phone. Uses the game's own card code (EnemySpawner's
// card specs and NeonPowerUpCard), at a 1560 x 720 landscape phone resolution.
//
// Command line:
//   Unity -batchmode -quit -projectPath . -executeMethod PowerUpCardPreview.Render \
//         -previewOut Temp/PowerUpCardPreview.png [-previewCards RapidFire,TwinShot,PowerRounds]
public static class PowerUpCardPreview
{
    private const string FontPath = "Assets/Fonts/Science_Gothic/ScienceGothic-VariableFont_CTRS,slnt,wdth,wght SDF.asset";

    [MenuItem("Tools/Drain Run/Render Power-Up Card Preview")]
    public static void RenderFromMenu()
    {
        string path = RenderToFile("Temp/PowerUpCardPreview.png", new[] { "RapidFire", "TwinShot", "PowerRounds" });
        EditorUtility.RevealInFinder(path);
    }

    public static void Render()
    {
        string outPath = GetArg("-previewOut") ?? "Temp/PowerUpCardPreview.png";
        string cards = GetArg("-previewCards") ?? "RapidFire,TwinShot,PowerRounds";
        RenderToFile(outPath, cards.Split(','));
    }

    private static string RenderToFile(string outPath, string[] cardNames)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Camera camera = new GameObject("PreviewCamera").AddComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.067f, 0.07f, 0.067f, 1f);
        RenderTexture target = new RenderTexture(1560, 720, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = target;

        GameObject canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 10f;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;

        // Same kind of text source the game clones (the HUD score text).
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        TextMeshProUGUI textSource = new GameObject("TextSource", typeof(RectTransform), typeof(TextMeshProUGUI))
            .GetComponent<TextMeshProUGUI>();
        textSource.transform.SetParent(canvas.transform, false);
        textSource.font = font;
        textSource.fontSharedMaterial = font.material;
        textSource.text = string.Empty;

        Image backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        backdrop.transform.SetParent(canvas.transform, false);
        backdrop.rectTransform.anchorMin = Vector2.zero;
        backdrop.rectTransform.anchorMax = Vector2.one;
        backdrop.rectTransform.offsetMin = Vector2.zero;
        backdrop.rectTransform.offsetMax = Vector2.zero;
        backdrop.color = new Color(0f, 0f, 0f, 0.92f);

        AddHeader(textSource, canvas.transform, "WAVE 3 CLEARED", 22f, new Color(1f, 0.3f, 0.46f), new Color(1f, 0.3f, 0.46f, 0.5f), 395f, 12f);
        AddHeader(textSource, canvas.transform, "CHOOSE A POWER-UP", 40f, new Color(0.95f, 0.95f, 0.95f), new Color(1f, 1f, 1f, 0.22f), 348f, 10f);

        // Build real card specs through EnemySpawner's private BuildCardSpec.
        GameObject spawnerObject = new GameObject("SpecSource");
        EnemySpawner spawner = spawnerObject.AddComponent<EnemySpawner>();
        Type powerUpType = typeof(EnemySpawner).GetNestedType("PowerUp", BindingFlags.NonPublic);
        MethodInfo buildSpec = typeof(EnemySpawner).GetMethod("BuildCardSpec", BindingFlags.NonPublic | BindingFlags.Instance);

        for (int index = 0; index < cardNames.Length && index < 3; index++)
        {
            object powerUp = Enum.Parse(powerUpType, cardNames[index].Trim());
            NeonCardSpec spec = (NeonCardSpec)buildSpec.Invoke(spawner, new[] { powerUp });
            float raise = spec.rare ? 14f : 0f;
            NeonPowerUpCard card = NeonPowerUpCard.Create(canvas.transform, textSource, spec, new Vector2((index - 1) * 420f, -10f + raise), 0f);
            card.SkipEntranceAnimation();
        }

        Canvas.ForceUpdateCanvases();
        foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true))
            text.ForceMeshUpdate(true, true);
        Canvas.ForceUpdateCanvases();

        camera.Render();
        RenderTexture.active = target;
        Texture2D image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        image.Apply();
        RenderTexture.active = null;

        string fullPath = Path.GetFullPath(outPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllBytes(fullPath, image.EncodeToPNG());
        Debug.Log($"Power-up card preview written to {fullPath}");
        return fullPath;
    }

    private static void AddHeader(TMP_Text textSource, Transform parent, string text, float size, Color color, Color glow, float y, float spacing)
    {
        TMP_Text label = UnityEngine.Object.Instantiate(textSource, parent, false);
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.characterSpacing = spacing;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.fontSharedMaterial = NeonUI.GlowTextMaterial(textSource.fontSharedMaterial, glow, 0.12f, 0.3f);
        RectTransform rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(1200f, 64f);
    }

    private static string GetArg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
                return args[i + 1];
        }

        return null;
    }
}
