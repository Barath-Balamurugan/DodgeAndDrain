using UnityEngine;
using TMPro;

public class PlayerAmmo : MonoBehaviour
{
    public TextMeshProUGUI ammoText;
    public int maxAmmo = 50;
    public int currentAmmo = 15;

    [Header("Empty Ammo Feedback")]
    [SerializeField, Min(0f)] private float emptyShakeDistance = 14f;
    [SerializeField, Min(0.01f)] private float emptyShakeDuration = 0.16f;
    [SerializeField] private Color emptyAmmoColor = new Color(1f, 0.3f, 0.35f, 1f);

    [Header("Ammo Reward Feedback")]
    [SerializeField, Min(0.01f)] private float rewardPulseDuration = 0.28f;
    [SerializeField, Min(0f)] private float rewardPulseScaleAmount = 0.28f;
    [SerializeField] private Color rewardAmmoColor = new Color(0.7f, 1f, 1f, 1f);

    [Header("Ammo Gain Tick")]
    [Tooltip("Quick bump on the counter whenever ammo goes up (e.g. from draining).")]
    [SerializeField, Min(0.01f)] private float gainTickDuration = 0.14f;
    [SerializeField, Min(0f)] private float gainTickScaleAmount = 0.12f;

    [Header("Low Ammo Warning")]
    [SerializeField, Min(0)] private int lowAmmoThreshold = 5;
    [SerializeField] private Color lowAmmoColor = new Color(1f, 0.72f, 0.2f, 1f);
    [SerializeField, Min(0.1f)] private float lowAmmoPulsesPerSecond = 2f;
    [SerializeField, Min(0f)] private float lowAmmoPulseScaleAmount = 0.07f;

    private int startingAmmo;
    private float emptyTimer;
    private float rewardTimer;
    private float gainTimer;
    private RectTransform ammoTextTransform;
    private Vector2 ammoTextRestPosition;
    private Vector3 ammoTextRestScale;
    private Color ammoTextRestColor;
    private bool hasAmmoTextRestState;

    public bool IsLow => currentAmmo <= lowAmmoThreshold;

    private void Awake()
    {
        startingAmmo = currentAmmo;
        CacheAmmoTextRestState();
    }

    private void Start()
    {
        RefreshUI();
    }

    public bool HasAmmo(int cost = 1) => currentAmmo >= cost;

    public bool Spend(int cost = 1)
    {
        if (currentAmmo < cost) return false;

        int before = currentAmmo;
        currentAmmo -= cost;
        WarnIfAmmoTurnedLow(before);
        RefreshUI();
        return true;
    }

    public void Add(int amount)
    {
        int before = currentAmmo;
        currentAmmo = Mathf.Clamp(currentAmmo + amount, 0, maxAmmo);

        if (currentAmmo > before)
            gainTimer = gainTickDuration;

        WarnIfAmmoTurnedLow(before);
        RefreshUI();
    }

    public void ResetForNewRun()
    {
        emptyTimer = 0f;
        rewardTimer = 0f;
        gainTimer = 0f;
        currentAmmo = Mathf.Clamp(startingAmmo, 0, maxAmmo);
        RestoreAmmoTextRestState();
        RefreshUI();
    }

    public void PlayEmptyAmmoFeedback()
    {
        emptyTimer = emptyShakeDuration;
        rewardTimer = 0f;
    }

    public void PlayAmmoRewardFeedback()
    {
        rewardTimer = rewardPulseDuration;
        emptyTimer = 0f;
    }

    private void WarnIfAmmoTurnedLow(int ammoBefore)
    {
        if (ammoBefore > lowAmmoThreshold && currentAmmo <= lowAmmoThreshold && GameFeedback.Instance != null)
            GameFeedback.Instance.PlayLowAmmo();
    }

    // All counter effects are layered here each frame, so they never fight over
    // the text's position, scale or colour. Unscaled time keeps them responsive
    // during hit-stop.
    private void LateUpdate()
    {
        if (ammoText == null)
            return;

        CacheAmmoTextRestState();
        if (!hasAmmoTextRestState)
            return;

        float deltaTime = Time.unscaledDeltaTime;
        emptyTimer = Mathf.Max(0f, emptyTimer - deltaTime);
        rewardTimer = Mathf.Max(0f, rewardTimer - deltaTime);
        gainTimer = Mathf.Max(0f, gainTimer - deltaTime);

        Vector2 position = ammoTextRestPosition;
        float scale = 1f;
        Color color = ammoTextRestColor;

        // Base layer: amber pulse when low, faster red pulse when empty.
        if (currentAmmo <= lowAmmoThreshold)
        {
            bool empty = currentAmmo <= 0;
            Color warningColor = empty ? emptyAmmoColor : lowAmmoColor;
            float speed = lowAmmoPulsesPerSecond * (empty ? 1.75f : 1f);
            float pulse = (Mathf.Sin(Time.unscaledTime * speed * Mathf.PI * 2f) + 1f) * 0.5f;

            color = Color.Lerp(warningColor, Color.Lerp(warningColor, Color.white, 0.35f), pulse);
            scale *= 1f + lowAmmoPulseScaleAmount * pulse;
        }

        if (gainTimer > 0f)
        {
            float strength = gainTimer / gainTickDuration;
            scale *= 1f + gainTickScaleAmount * strength;
            color = Color.Lerp(color, rewardAmmoColor, strength);
        }

        if (rewardTimer > 0f)
        {
            float pulse = Mathf.Sin((1f - rewardTimer / rewardPulseDuration) * Mathf.PI);
            scale *= 1f + rewardPulseScaleAmount * pulse;
            color = Color.Lerp(color, rewardAmmoColor, pulse);
        }

        if (emptyTimer > 0f)
        {
            float progress = 1f - emptyTimer / emptyShakeDuration;
            position.x += Mathf.Sin(progress * Mathf.PI * 8f) * emptyShakeDistance * (1f - progress);
            color = Color.Lerp(emptyAmmoColor, color, progress);
        }

        ammoTextTransform.anchoredPosition = position;
        ammoTextTransform.localScale = ammoTextRestScale * scale;
        ammoText.color = color;
    }

    private void RefreshUI()
    {
        if (ammoText != null)
            ammoText.text = $"AMMO: {currentAmmo}";
    }

    private void CacheAmmoTextRestState()
    {
        if (ammoText == null || hasAmmoTextRestState)
            return;

        ammoTextTransform = ammoText.rectTransform;
        ammoTextRestPosition = ammoTextTransform.anchoredPosition;
        ammoTextRestScale = ammoTextTransform.localScale;
        ammoTextRestColor = ammoText.color;
        hasAmmoTextRestState = true;
    }

    private void RestoreAmmoTextRestState()
    {
        CacheAmmoTextRestState();

        if (!hasAmmoTextRestState)
            return;

        ammoTextTransform.anchoredPosition = ammoTextRestPosition;
        ammoTextTransform.localScale = ammoTextRestScale;
        ammoText.color = ammoTextRestColor;
    }
}
