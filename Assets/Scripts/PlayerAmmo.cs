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

    private Coroutine emptyFeedbackRoutine;

    private void Start()
    {
        RefreshUI();
    }

    public bool HasAmmo(int cost = 1) => currentAmmo >= cost;

    public bool Spend(int cost = 1)
    {
        if (currentAmmo < cost) return false;
        currentAmmo -= cost;
        RefreshUI();
        return true;
    }

    public void Add(int amount)
    {
        currentAmmo = Mathf.Clamp(currentAmmo + amount, 0, maxAmmo);
        RefreshUI();
    }

    public void PlayEmptyAmmoFeedback()
    {
        if (ammoText == null)
            return;

        if (emptyFeedbackRoutine != null)
            StopCoroutine(emptyFeedbackRoutine);

        emptyFeedbackRoutine = StartCoroutine(PlayEmptyAmmoFeedbackRoutine());
    }

    private System.Collections.IEnumerator PlayEmptyAmmoFeedbackRoutine()
    {
        RectTransform textTransform = ammoText.rectTransform;
        Vector2 restPosition = textTransform.anchoredPosition;
        Color originalColor = ammoText.color;
        float elapsed = 0f;

        while (elapsed < emptyShakeDuration)
        {
            float progress = elapsed / emptyShakeDuration;
            float shake = Mathf.Sin(progress * Mathf.PI * 8f) * emptyShakeDistance * (1f - progress);
            textTransform.anchoredPosition = restPosition + new Vector2(shake, 0f);
            ammoText.color = Color.Lerp(emptyAmmoColor, originalColor, progress);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        textTransform.anchoredPosition = restPosition;
        ammoText.color = originalColor;
        emptyFeedbackRoutine = null;
    }

    private void RefreshUI()
    {
        if (ammoText != null)
            ammoText.text = $"AMMO: {currentAmmo}";
    }
}
