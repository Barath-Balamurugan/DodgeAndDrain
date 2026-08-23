using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DrainZone : MonoBehaviour
{
    public float drainRate = 10f; // HP per second

    public PlayerMovement2D player;
    public GameManager gameManager;

    [Header("Ammo Gain")]
    public bool drainGivesAmmo = true;
    public float hpPerAmmo = 2f;     // 2 HP drained -> +1 ammo

    private PlayerAmmo playerAmmo;   // ✅ ref to the player ammo
    private Collider2D col;
    private float ammoCarry;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void Start()
    {
        // Grab ammo from player
        if (player != null)
            playerAmmo = player.GetComponent<PlayerAmmo>();
    }

    private void Update()
    {
        bool active =
            player != null &&
            gameManager != null &&
            player.isDraining &&
            !gameManager.IsOverheated &&
            !gameManager.IsGameOver;

        col.enabled = active;

        // Optional: stop draining visuals when disabled
        // (not strictly necessary)
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!col.enabled) return; // if not active, do nothing

        DrainableBase drainable = other.GetComponent<DrainableBase>();
        if (drainable == null) return;

        float drained = drainable.TakeDrain(drainRate * Time.deltaTime);

        if (drained > 0f)
        {
            // Keep your energy system if you still use overheating, etc.
            if (gameManager != null)
                gameManager.AddEnergy(drained);

            // ✅ Convert drained HP -> ammo
            if (drainGivesAmmo && playerAmmo != null)
            {
                ammoCarry += drained;

                int ammoGain = Mathf.FloorToInt(ammoCarry / Mathf.Max(0.0001f, hpPerAmmo));
                if (ammoGain > 0)
                {
                    ammoCarry -= ammoGain * hpPerAmmo;
                    playerAmmo.Add(ammoGain);
                }
            }
        }

        drainable.SetDrainingVisual(true, player.transform);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        DrainableBase drainable = other.GetComponent<DrainableBase>();
        if (drainable != null)
            drainable.StopDraining();
    }
}
