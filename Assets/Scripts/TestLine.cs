using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class TestLine : MonoBehaviour
{
    void Start()
    {
        var lr = GetComponent<LineRenderer>();

        lr.useWorldSpace = true;
        lr.positionCount = 2;

        lr.startWidth = 0.1f;
        lr.endWidth = 0.1f;

        // If you have URP, "Sprites/Default" still works for a simple unlit line.
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = Color.cyan;
        lr.endColor = Color.cyan;

        lr.sortingLayerName = "Default";
        lr.sortingOrder = 10;

        lr.SetPosition(0, new Vector3(-2f, 0f, 0f));
        lr.SetPosition(1, new Vector3(2f, 0f, 0f));
    }
}
