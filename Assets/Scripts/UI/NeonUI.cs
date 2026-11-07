using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Procedural sprites and text materials for the neon UI (power-up cards).
// Everything is generated once in code and cached, so no image files are
// needed. Sprites are white; tint them with Image.color. They use 100 pixels
// per unit, which matches the canvas, so 1 texture pixel = 1 UI unit.
public static class NeonUI
{
    public const float CardCornerRadius = 40f;
    public const float GlowSpread = 60f;
    private const float PixelsPerUnit = 100f;

    private static Sprite roundedRect;
    private static Sprite roundedOutline;
    private static Sprite outerGlow;
    private static Sprite softDot;
    private static Sprite circle;
    private static Sprite arc;

    private static readonly Dictionary<string, Material> glowMaterials = new Dictionary<string, Material>();

    // 9-sliced filled rounded rectangle (card body).
    public static Sprite RoundedRect => roundedRect != null ? roundedRect : (roundedRect = MakeRoundedRect(128, CardCornerRadius, 0f, "NeonRoundedRect"));

    // 9-sliced rounded rectangle outline, 4 units thick (card border).
    public static Sprite RoundedOutline => roundedOutline != null ? roundedOutline : (roundedOutline = MakeRoundedRect(128, CardCornerRadius, 4f, "NeonRoundedOutline"));

    // 9-sliced soft glow around a rounded rectangle. Size the Image to the
    // card size plus 2 * GlowSpread in each direction.
    public static Sprite OuterGlow => outerGlow != null ? outerGlow : (outerGlow = MakeOuterGlow(256, CardCornerRadius, GlowSpread));

    // Radial soft light, bright centre fading to nothing at the edge.
    public static Sprite SoftDot => softDot != null ? softDot : (softDot = MakeSoftDot(128));

    // Anti-aliased solid circle.
    public static Sprite Circle => circle != null ? circle : (circle = MakeCircle(128));

    // A 70-degree glowing arc of a ring (centred at the top), for light trails.
    public static Sprite Arc => arc != null ? arc : (arc = MakeArc(256, 94f, 9f, 70f));

    // A copy of the font material with a soft coloured outline, which reads as
    // a neon glow. Outline and softness need no shader keywords, so this works
    // in player builds (keyword-based underlay/glow variants get stripped).
    public static Material GlowTextMaterial(Material baseMaterial, Color glowColor, float width = 0.15f, float softness = 0.28f)
    {
        if (baseMaterial == null)
            return null;

        Color32 key32 = glowColor;
        string key = $"{baseMaterial.GetInstanceID()}_{key32.r}_{key32.g}_{key32.b}_{key32.a}_{width:F2}_{softness:F2}";
        if (glowMaterials.TryGetValue(key, out Material cached) && cached != null)
            return cached;

        ShaderUtilities.GetShaderPropertyIDs();
        Material material = new Material(baseMaterial) { name = "NeonGlowText", hideFlags = HideFlags.DontSave };
        material.SetColor(ShaderUtilities.ID_OutlineColor, glowColor);
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
        material.SetFloat(ShaderUtilities.ID_OutlineSoftness, softness);
        ShaderUtilities.UpdateShaderRatios(material);

        glowMaterials[key] = material;
        return material;
    }

    // ---------- Texture generation ----------

    private static Sprite MakeRoundedRect(int size, float radius, float outlineThickness, string name)
    {
        Texture2D texture = NewTexture(size, size, name);
        Color32[] pixels = new Color32[size * size];
        Vector2 halfSize = new Vector2(size * 0.5f - 1f, size * 0.5f - 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = RoundedRectDistance(new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f), halfSize, radius);
                float alpha = outlineThickness > 0f
                    ? Mathf.Clamp01(0.5f - (Mathf.Abs(distance + outlineThickness * 0.5f) - outlineThickness * 0.5f))
                    : Mathf.Clamp01(0.5f - distance);
                pixels[y * size + x] = White(alpha);
            }
        }

        float border = radius + 2f;
        return Finish(texture, pixels, new Vector4(border, border, border, border));
    }

    private static Sprite MakeOuterGlow(int size, float radius, float spread)
    {
        Texture2D texture = NewTexture(size, size, "NeonOuterGlow");
        Color32[] pixels = new Color32[size * size];
        Vector2 halfSize = new Vector2(size * 0.5f - spread, size * 0.5f - spread);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = RoundedRectDistance(new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f), halfSize, radius);
                float normalized = Mathf.Clamp01(Mathf.Max(0f, distance) / spread);
                // Must reach exactly zero at the texture edge: the project renders in
                // linear colour space, where even a 1% tail shows up as a visible cut.
                float alpha = distance <= 0f ? 1f : Mathf.Pow(1f - normalized, 3.5f);
                pixels[y * size + x] = White(alpha);
            }
        }

        float border = spread + radius + 2f;
        return Finish(texture, pixels, new Vector4(border, border, border, border));
    }

    private static Sprite MakeSoftDot(int size)
    {
        Texture2D texture = NewTexture(size, size, "NeonSoftDot");
        Color32[] pixels = new Color32[size * size];
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude / half;
                float alpha = r >= 1f ? 0f : Mathf.Exp(-r * r * 4.5f) * Mathf.Clamp01((1f - r) * 8f);
                pixels[y * size + x] = White(alpha);
            }
        }

        return Finish(texture, pixels, Vector4.zero);
    }

    private static Sprite MakeCircle(int size)
    {
        Texture2D texture = NewTexture(size, size, "NeonCircle");
        Color32[] pixels = new Color32[size * size];
        float half = size * 0.5f;
        float radius = half - 1.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude;
                pixels[y * size + x] = White(Mathf.Clamp01(radius - distance + 0.5f));
            }
        }

        return Finish(texture, pixels, Vector4.zero);
    }

    private static Sprite MakeArc(int size, float radius, float thickness, float spanDegrees)
    {
        Texture2D texture = NewTexture(size, size, "NeonArc");
        Color32[] pixels = new Color32[size * size];
        float half = size * 0.5f;
        float halfSpan = spanDegrees * 0.5f;
        float glowRange = thickness * 1.6f;

        Vector2 endA = Polar(90f - halfSpan, radius);
        Vector2 endB = Polar(90f + halfSpan, radius);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(x + 0.5f - half, y + 0.5f - half);
                float angle = Mathf.Atan2(point.y, point.x) * Mathf.Rad2Deg;
                float distance = Mathf.Abs(Mathf.DeltaAngle(angle, 90f)) <= halfSpan
                    ? Mathf.Abs(point.magnitude - radius)
                    : Mathf.Min(Vector2.Distance(point, endA), Vector2.Distance(point, endB));

                float core = Mathf.Clamp01(thickness * 0.5f - distance + 0.5f);
                // Fades to exactly zero (see MakeOuterGlow) well inside the texture.
                float glow = Mathf.Pow(Mathf.Clamp01(1f - distance / (glowRange * 2f)), 3f) * 0.5f;
                pixels[y * size + x] = White(Mathf.Max(core, glow));
            }
        }

        return Finish(texture, pixels, Vector4.zero);
    }

    // Signed distance to a rounded rectangle centred at the origin (negative inside).
    private static float RoundedRectDistance(Vector2 point, Vector2 halfSize, float radius)
    {
        Vector2 q = new Vector2(Mathf.Abs(point.x), Mathf.Abs(point.y)) - (halfSize - new Vector2(radius, radius));
        Vector2 outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
        return outside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
    }

    private static Vector2 Polar(float degrees, float radius)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
    }

    private static Color32 White(float alpha)
    {
        return new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
    }

    private static Texture2D NewTexture(int width, int height, string name)
    {
        return new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };
    }

    private static Sprite Finish(Texture2D texture, Color32[] pixels, Vector4 border)
    {
        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect, border);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }
}
