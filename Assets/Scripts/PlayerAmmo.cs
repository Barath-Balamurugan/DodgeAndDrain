using UnityEngine;
using TMPro;

public class PlayerAmmo : MonoBehaviour
{
    public TextMeshProUGUI ammoText;
    public int maxAmmo = 50;
    public int currentAmmo = 15;

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

    private void RefreshUI()
    {
        if (ammoText != null)
            ammoText.text = $"AMMO: {currentAmmo}";
    }
}
