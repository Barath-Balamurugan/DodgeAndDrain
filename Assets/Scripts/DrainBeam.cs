using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class DrainBeam : MonoBehaviour
{
    private LineRenderer lr;
    private DrainableBase drainable;
    private Transform player;

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        drainable = GetComponent<DrainableBase>();

        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.enabled = false; // start hidden
        lr.sortingLayerName = "Default";
        lr.sortingOrder = 10;
    }

    private void Start()
    {
        if (GameManager.Instance != null && GameManager.Instance.player != null)
        {
            player = GameManager.Instance.player.transform;
        }
    }

    private void Update()
    {
        if (player == null || drainable == null) return;

        if (drainable.isBeingDrained)
        {
            lr.enabled = true;
            lr.SetPosition(0, transform.position);       // drainable end
            lr.SetPosition(1, player.position);          // player end
        }
        else
        {
            lr.enabled = false;
        }
    }
}
