using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

// Central place for game feel: kill bursts, hit-stop, screen shake, score popups,
// procedural sound effects and light haptics. It creates itself when the game
// starts, so nothing needs wiring in the scene. Select the "GameFeedback" object
// in Play mode to tune values live.
public class GameFeedback : MonoBehaviour
{
    public static GameFeedback Instance { get; private set; }

    // Enemy "weight" runs 0 (chaser, 20 HP) to 1 (shooter, 45 HP) and scales
    // how big each kill feels.
    private const float LightEnemyHealth = 20f;
    private const float HeavyEnemyHealth = 45f;
    private const int SfxSourceCount = 8;
    private const int RingSegments = 32;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float hitVolume = 0.22f;
    [SerializeField, Range(0f, 1f)] private float killVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float drainablePopVolume = 0.3f;
    [SerializeField, Range(0f, 1f)] private float drainHumVolume = 0.16f;
    [SerializeField, Range(0f, 1f)] private float overheatVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float waveVolume = 0.45f;
    [SerializeField, Range(0f, 1f)] private float uiVolume = 0.4f;

    [Header("Kill Payoff (light enemy -> heavy enemy)")]
    [SerializeField] private Vector2 killHitStopSeconds = new Vector2(0.03f, 0.06f);
    [SerializeField] private Vector2 killShake = new Vector2(0.18f, 0.35f);
    [SerializeField] private Vector2Int killParticleCount = new Vector2Int(12, 22);
    [SerializeField] private Vector2 killRingRadius = new Vector2(1.2f, 2f);
    [SerializeField, Min(1f)] private float scorePopupFontSize = 13f;

    [Header("Hit-Stop")]
    [Tooltip("Minimum gap between hit-stops, so rapid kills don't make the game stutter.")]
    [SerializeField, Min(0f)] private float hitStopCooldown = 0.25f;
    [SerializeField, Range(0.01f, 0.5f)] private float hitStopTimeScale = 0.05f;

    [Header("Screen Shake")]
    [SerializeField, Min(0f)] private float maxShakeOffset = 0.45f;
    [SerializeField, Min(0.01f)] private float shakeDecayPerSecond = 2f;
    [SerializeField, Min(0.1f)] private float shakeFrequency = 28f;

    [Header("Drain")]
    [SerializeField] private Color drainEnergyColor = new Color(0.15f, 0.95f, 1f, 1f);
    [SerializeField] private Vector2 drainHumPitch = new Vector2(0.85f, 1.6f);

    [Header("Haptics (Android)")]
    [SerializeField] private bool haptics = true;
    [SerializeField, Min(0f)] private float hapticCooldown = 0.07f;

    private AudioClip hitClip;
    private AudioClip killClip;
    private AudioClip drainablePopClip;
    private AudioClip overheatClip;
    private AudioClip overheatRecoveredClip;
    private AudioClip waveStartClip;
    private AudioClip waveClearClip;
    private AudioClip powerUpSelectedClip;
    private AudioClip lowAmmoClip;
    private AudioSource[] sfxSources;
    private AudioSource drainHumSource;
    private int nextSfxSource;
    private readonly Dictionary<AudioClip, float> lastPlayTimes = new Dictionary<AudioClip, float>();

    private bool hitStopActive;
    private float hitStopEndTime;
    private float nextHitStopTime;

    private float shakeTrauma;
    private Vector3 shakeOffset;
    private Camera shakenCamera;
    private Vector3 appliedShakeOffset;

    private float nextHapticTime;

    private bool effectsReady;
    private ParticleSystem burstParticles;
    private Material ringMaterial;
    private int effectsSortingLayer;
    private int effectsSortingOrder;
    private readonly List<RingEffect> rings = new List<RingEffect>();
    private readonly List<PopupEffect> popups = new List<PopupEffect>();

    private class RingEffect
    {
        public LineRenderer line;
        public float age;
        public float duration;
        public float startRadius;
        public float endRadius;
        public float width;
        public Color color;
    }

    private class PopupEffect
    {
        public TextMeshPro text;
        public float age;
        public float duration;
        public Vector3 start;
        public Color color;
        public float scale;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateOnStartup()
    {
        if (Instance == null)
            new GameObject("GameFeedback").AddComponent<GameFeedback>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        CreateAudio();
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += ApplyShakeForRender;
        RenderPipelineManager.endCameraRendering += RemoveShakeAfterRender;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= ApplyShakeForRender;
        RenderPipelineManager.endCameraRendering -= RemoveShakeAfterRender;

        if (hitStopActive && Mathf.Approximately(Time.timeScale, hitStopTimeScale))
            Time.timeScale = 1f;

        hitStopActive = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        UpdateHitStop();
        UpdateShake();
        UpdateDrainHum();
        UpdateRings();
        UpdatePopups();
    }

    // ---------- Gameplay events ----------

    public void PlayEnemyHit()
    {
        PlaySound(hitClip, hitVolume, Random.Range(0.92f, 1.1f), 0.04f);
    }

    public void PlayEnemyKilled(Vector3 position, Color color, int score, float maxHealth, SpriteRenderer source)
    {
        EnsureEffects(source);
        position.z = 0f;
        float weight = Mathf.InverseLerp(LightEnemyHealth, HeavyEnemyHealth, maxHealth);
        float sizeScale = Mathf.Lerp(1f, 1.3f, weight);

        EmitBurst(position, color,
            Mathf.RoundToInt(Mathf.Lerp(killParticleCount.x, killParticleCount.y, weight)),
            new Vector2(4f, 10f), new Vector2(0.15f, 0.35f) * sizeScale, 0.45f);
        SpawnRing(position, Color.Lerp(color, Color.white, 0.4f), 0.3f,
            Mathf.Lerp(killRingRadius.x, killRingRadius.y, weight), 0.28f, 0.1f);
        SpawnPopup(position + Vector3.up * 0.5f, "+" + score, Color.Lerp(color, Color.white, 0.5f), Mathf.Lerp(1f, 1.35f, weight));

        PlaySound(killClip, killVolume, Mathf.Lerp(1.15f, 0.8f, weight) * Random.Range(0.95f, 1.05f), 0.03f);
        HitStop(Mathf.Lerp(killHitStopSeconds.x, killHitStopSeconds.y, weight));
        AddShake(Mathf.Lerp(killShake.x, killShake.y, weight));
        Haptic(weight > 0.5f ? 25 : 12, Mathf.RoundToInt(Mathf.Lerp(70f, 160f, weight)));
    }

    public void PlayDrainableConsumed(Vector3 position, SpriteRenderer source)
    {
        EnsureEffects(source);
        position.z = 0f;

        EmitBurst(position, drainEnergyColor, 8, new Vector2(2.5f, 6f), new Vector2(0.1f, 0.22f), 0.35f);
        SpawnRing(position, Color.Lerp(drainEnergyColor, Color.white, 0.3f), 0.15f, 0.8f, 0.2f, 0.06f);
        PlaySound(drainablePopClip, drainablePopVolume, Random.Range(0.95f, 1.15f), 0.05f);
    }

    public void PlayOverheat()
    {
        PlaySound(overheatClip, overheatVolume);
        AddShake(0.35f);
        Haptic(80, 200);
    }

    public void PlayOverheatRecovered()
    {
        PlaySound(overheatRecoveredClip, uiVolume);
    }

    public void PlayWaveStart()
    {
        PlaySound(waveStartClip, waveVolume);
    }

    public void PlayWaveClear()
    {
        PlaySound(waveClearClip, waveVolume);
        Haptic(30, 120);
    }

    public void PlayPowerUpSelected()
    {
        PlaySound(powerUpSelectedClip, uiVolume);
    }

    public void PlayLowAmmo()
    {
        PlaySound(lowAmmoClip, uiVolume, 1f, 0.5f);
    }

    // ---------- Hit-stop ----------

    public void HitStop(float duration)
    {
        if (duration <= 0f)
            return;

        float now = Time.unscaledTime;
        if (!hitStopActive)
        {
            // Never stack on top of a pause (timeScale 0) or other slow-motion.
            if (now < nextHitStopTime || !Mathf.Approximately(Time.timeScale, 1f))
                return;

            hitStopActive = true;
            hitStopEndTime = now + duration;
            Time.timeScale = hitStopTimeScale;
        }
        else
        {
            hitStopEndTime = Mathf.Max(hitStopEndTime, now + duration);
        }

        nextHitStopTime = hitStopEndTime + hitStopCooldown;
    }

    private void UpdateHitStop()
    {
        if (!hitStopActive || Time.unscaledTime < hitStopEndTime)
            return;

        hitStopActive = false;

        // Only undo our own slowdown: if the game was paused mid hit-stop, stay paused.
        if (Mathf.Approximately(Time.timeScale, hitStopTimeScale))
            Time.timeScale = 1f;
    }

    // ---------- Screen shake ----------

    public void AddShake(float trauma)
    {
        shakeTrauma = Mathf.Clamp01(shakeTrauma + trauma);
    }

    private void UpdateShake()
    {
        if (shakeTrauma <= 0f)
        {
            shakeOffset = Vector3.zero;
            return;
        }

        shakeTrauma = Mathf.Max(0f, shakeTrauma - shakeDecayPerSecond * Time.unscaledDeltaTime);
        float strength = shakeTrauma * maxShakeOffset;
        float time = Time.unscaledTime * shakeFrequency;
        shakeOffset = new Vector3(
            (Mathf.PerlinNoise(time, 0.17f) * 2f - 1f) * strength,
            (Mathf.PerlinNoise(0.71f, time) * 2f - 1f) * strength,
            0f);
    }

    // The offset is added only while the camera renders and removed right after,
    // so gameplay code (camera zooms, death shake) never sees a shaken camera.
    private void ApplyShakeForRender(ScriptableRenderContext context, Camera renderingCamera)
    {
        if (shakeOffset == Vector3.zero || renderingCamera.cameraType != CameraType.Game || renderingCamera != Camera.main)
            return;

        appliedShakeOffset = shakeOffset;
        shakenCamera = renderingCamera;
        renderingCamera.transform.position += appliedShakeOffset;
    }

    private void RemoveShakeAfterRender(ScriptableRenderContext context, Camera renderingCamera)
    {
        if (renderingCamera != shakenCamera)
            return;

        renderingCamera.transform.position -= appliedShakeOffset;
        shakenCamera = null;
    }

    // ---------- Audio ----------

    private void CreateAudio()
    {
        hitClip = FeedbackSounds.CreateEnemyHit();
        killClip = FeedbackSounds.CreateEnemyKill();
        drainablePopClip = FeedbackSounds.CreateDrainablePop();
        overheatClip = FeedbackSounds.CreateOverheatAlarm();
        overheatRecoveredClip = FeedbackSounds.CreateOverheatRecovered();
        waveStartClip = FeedbackSounds.CreateWaveStart();
        waveClearClip = FeedbackSounds.CreateWaveClear();
        powerUpSelectedClip = FeedbackSounds.CreatePowerUpSelected();
        lowAmmoClip = FeedbackSounds.CreateLowAmmo();

        sfxSources = new AudioSource[SfxSourceCount];
        for (int i = 0; i < sfxSources.Length; i++)
            sfxSources[i] = CreateAudioSource();

        drainHumSource = CreateAudioSource();
        drainHumSource.clip = FeedbackSounds.CreateDrainHumLoop();
        drainHumSource.loop = true;
        drainHumSource.volume = 0f;
    }

    private AudioSource CreateAudioSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        return source;
    }

    private void PlaySound(AudioClip clip, float volume, float pitch = 1f, float minInterval = 0f)
    {
        if (clip == null || sfxSources == null || volume <= 0f || masterVolume <= 0f)
            return;

        // Rate-limit so a pile of simultaneous kills doesn't turn into one loud blast.
        float now = Time.unscaledTime;
        if (minInterval > 0f && lastPlayTimes.TryGetValue(clip, out float lastTime) && now - lastTime < minInterval)
            return;

        lastPlayTimes[clip] = now;

        AudioSource source = sfxSources[nextSfxSource];
        nextSfxSource = (nextSfxSource + 1) % sfxSources.Length;
        source.pitch = pitch;
        source.PlayOneShot(clip, volume * masterVolume);
    }

    private void UpdateDrainHum()
    {
        if (drainHumSource == null)
            return;

        GameManager gameManager = GameManager.Instance;
        bool draining =
            gameManager != null &&
            gameManager.player != null &&
            gameManager.player.isDraining &&
            !gameManager.IsOverheated &&
            !gameManager.IsGameOver &&
            !DrainableSpawner.AreDrainablesPaused &&
            Time.timeScale > 0f;

        float heat = gameManager != null && gameManager.drainGaugeMax > 0f
            ? Mathf.Clamp01(gameManager.drainGauge / gameManager.drainGaugeMax)
            : 0f;

        float targetVolume = draining ? drainHumVolume * masterVolume * Mathf.Lerp(0.7f, 1f, heat) : 0f;
        drainHumSource.volume = Mathf.MoveTowards(drainHumSource.volume, targetVolume, Time.unscaledDeltaTime * 1.5f);
        drainHumSource.pitch = Mathf.Lerp(drainHumPitch.x, drainHumPitch.y, heat);

        if (drainHumSource.volume > 0f && !drainHumSource.isPlaying)
            drainHumSource.Play();
        else if (drainHumSource.volume <= 0f && drainHumSource.isPlaying)
            drainHumSource.Stop();
    }

    // ---------- Haptics ----------

    private void Haptic(long milliseconds, int amplitude)
    {
        if (!haptics || Time.unscaledTime < nextHapticTime)
            return;

        nextHapticTime = Time.unscaledTime + hapticCooldown;
        AndroidHaptics.Vibrate(milliseconds, amplitude);
    }

    // ---------- Bursts, rings, popups ----------

    // Built on first use from a gameplay sprite, so effects share its material
    // and sorting layer (the player may still be inactive when the scene loads).
    private void EnsureEffects(SpriteRenderer source)
    {
        if (effectsReady || source == null || source.sharedMaterial == null)
            return;

        effectsReady = true;
        ringMaterial = source.sharedMaterial;
        effectsSortingLayer = source.sortingLayerID;
        effectsSortingOrder = source.sortingOrder + 6;

        GameObject burstObject = new GameObject("FeedbackBursts");
        burstObject.transform.SetParent(transform, false);
        burstParticles = burstObject.AddComponent<ParticleSystem>();
        burstParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = burstParticles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startSpeed = 0f;
        main.startLifetime = 0.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 800;

        ParticleSystem.EmissionModule emission = burstParticles.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = burstParticles.shape;
        shape.enabled = false;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = burstParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.6f, 0.75f), new Keyframe(1f, 0f)));

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = burstParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fade);

        // Drag makes particles burst out fast and then hang, which reads as "pop".
        ParticleSystem.LimitVelocityOverLifetimeModule limitVelocity = burstParticles.limitVelocityOverLifetime;
        limitVelocity.enabled = true;
        limitVelocity.limit = 100f;
        limitVelocity.drag = 5f;

        ParticleSystemRenderer particleRenderer = burstObject.GetComponent<ParticleSystemRenderer>();
        Material particleMaterial = new Material(source.sharedMaterial) { name = "FeedbackBurstMaterial" };
        particleMaterial.mainTexture = CreateSoftDotTexture();
        particleRenderer.sharedMaterial = particleMaterial;
        particleRenderer.sortingLayerID = effectsSortingLayer;
        particleRenderer.sortingOrder = effectsSortingOrder;

        burstParticles.Play();
    }

    private void EmitBurst(Vector3 position, Color color, int count, Vector2 speedRange, Vector2 sizeRange, float lifetime)
    {
        if (burstParticles == null)
            return;

        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

            emitParams.position = position + direction * 0.1f;
            emitParams.velocity = direction * Random.Range(speedRange.x, speedRange.y);
            emitParams.startSize = Random.Range(sizeRange.x, sizeRange.y);
            emitParams.startLifetime = lifetime * Random.Range(0.7f, 1.15f);
            emitParams.startColor = Color.Lerp(color, Color.white, Random.Range(0f, 0.45f));
            burstParticles.Emit(emitParams, 1);
        }
    }

    private void SpawnRing(Vector3 position, Color color, float startRadius, float endRadius, float duration, float width)
    {
        if (ringMaterial == null)
            return;

        RingEffect ring = rings.Find(candidate => !candidate.line.gameObject.activeSelf) ?? CreateRing();
        ring.line.transform.position = position;
        ring.age = 0f;
        ring.duration = duration;
        ring.startRadius = startRadius;
        ring.endRadius = endRadius;
        ring.width = width;
        ring.color = color;
        ring.line.gameObject.SetActive(true);
        DrawRing(ring, 0f);
    }

    private RingEffect CreateRing()
    {
        GameObject ringObject = new GameObject("FeedbackRing");
        ringObject.transform.SetParent(transform, false);

        LineRenderer line = ringObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = RingSegments;
        line.sharedMaterial = ringMaterial;
        line.sortingLayerID = effectsSortingLayer;
        line.sortingOrder = effectsSortingOrder;

        RingEffect ring = new RingEffect { line = line };
        rings.Add(ring);
        return ring;
    }

    private void UpdateRings()
    {
        for (int i = 0; i < rings.Count; i++)
        {
            RingEffect ring = rings[i];
            if (!ring.line.gameObject.activeSelf)
                continue;

            ring.age += Time.deltaTime;
            float progress = ring.age / ring.duration;
            if (progress >= 1f)
            {
                ring.line.gameObject.SetActive(false);
                continue;
            }

            DrawRing(ring, progress);
        }
    }

    private static void DrawRing(RingEffect ring, float progress)
    {
        float eased = 1f - Mathf.Pow(1f - progress, 3f);
        float radius = Mathf.Lerp(ring.startRadius, ring.endRadius, eased);
        Color color = new Color(ring.color.r, ring.color.g, ring.color.b, 1f - progress);

        ring.line.startColor = color;
        ring.line.endColor = color;
        ring.line.widthMultiplier = Mathf.Lerp(ring.width, ring.width * 0.3f, progress);

        for (int index = 0; index < RingSegments; index++)
        {
            float angle = index * Mathf.PI * 2f / RingSegments;
            ring.line.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
        }
    }

    private void SpawnPopup(Vector3 position, string label, Color color, float scale)
    {
        TMP_FontAsset font = GetPopupFont();
        if (font == null)
            return;

        PopupEffect popup = popups.Find(candidate => !candidate.text.gameObject.activeSelf) ?? CreatePopup(font);
        popup.text.text = label;
        popup.age = 0f;
        popup.duration = 0.65f;
        popup.start = position;
        popup.color = color;
        popup.scale = scale;
        popup.text.gameObject.SetActive(true);
        DrawPopup(popup, 0f);
    }

    private PopupEffect CreatePopup(TMP_FontAsset font)
    {
        GameObject popupObject = new GameObject("ScorePopup");
        popupObject.transform.SetParent(transform, false);

        TextMeshPro text = popupObject.AddComponent<TextMeshPro>();
        text.font = font;
        text.fontSize = scorePopupFontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        text.rectTransform.sizeDelta = new Vector2(6f, 2f);
        text.sortingLayerID = effectsSortingLayer;
        text.sortingOrder = effectsSortingOrder + 2;

        PopupEffect popup = new PopupEffect { text = text };
        popups.Add(popup);
        return popup;
    }

    private void UpdatePopups()
    {
        for (int i = 0; i < popups.Count; i++)
        {
            PopupEffect popup = popups[i];
            if (!popup.text.gameObject.activeSelf)
                continue;

            popup.age += Time.deltaTime;
            float progress = popup.age / popup.duration;
            if (progress >= 1f)
            {
                popup.text.gameObject.SetActive(false);
                continue;
            }

            DrawPopup(popup, progress);
        }
    }

    private static void DrawPopup(PopupEffect popup, float progress)
    {
        // Pop in big, settle, float up, then fade.
        float rise = (1f - Mathf.Pow(1f - progress, 2.5f)) * 1.1f;
        float pop = progress < 0.15f
            ? Mathf.Lerp(0.6f, 1.25f, progress / 0.15f)
            : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((progress - 0.15f) / 0.2f));
        float alpha = progress < 0.6f ? 1f : 1f - (progress - 0.6f) / 0.4f;

        popup.text.transform.position = popup.start + Vector3.up * rise;
        popup.text.transform.localScale = Vector3.one * (pop * popup.scale);
        popup.text.color = new Color(popup.color.r, popup.color.g, popup.color.b, alpha);
    }

    private static TMP_FontAsset GetPopupFont()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager != null && gameManager.scoreText != null && gameManager.scoreText.font != null)
            return gameManager.scoreText.font;

        return TMP_Settings.defaultFontAsset;
    }

    private static Texture2D CreateSoftDotTexture()
    {
        const int size = 64;
        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / center;
                byte alpha = (byte)(Mathf.Clamp01((1f - distance) / 0.35f) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "FeedbackSoftDot",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }
}

// Short, light vibrations. Handheld.Vibrate is a long fixed buzz, so this calls
// Android's Vibrator directly. Other platforms do nothing.
public static class AndroidHaptics
{
#if UNITY_ANDROID && !UNITY_EDITOR
    private static bool initialized;
    private static bool available;
    private static int sdkLevel;
    private static AndroidJavaObject vibrator;
    private static AndroidJavaClass vibrationEffect;
#endif

    public static void Vibrate(long milliseconds, int amplitude)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (!initialized)
                Initialize();

            if (!available)
                return;

            if (sdkLevel >= 26)
            {
                using (AndroidJavaObject effect = vibrationEffect.CallStatic<AndroidJavaObject>(
                    "createOneShot", milliseconds, Mathf.Clamp(amplitude, 1, 255)))
                {
                    vibrator.Call("vibrate", effect);
                }
            }
            else
            {
                vibrator.Call("vibrate", milliseconds);
            }
        }
        catch (System.Exception exception)
        {
            available = false;
            Debug.LogWarning("Haptics disabled: " + exception.Message);
        }
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static void Initialize()
    {
        initialized = true;

        using (AndroidJavaClass version = new AndroidJavaClass("android.os.Build$VERSION"))
            sdkLevel = version.GetStatic<int>("SDK_INT");

        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        {
            vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
        }

        if (sdkLevel >= 26)
            vibrationEffect = new AndroidJavaClass("android.os.VibrationEffect");

        available = vibrator != null && vibrator.Call<bool>("hasVibrator");
    }
#endif
}
