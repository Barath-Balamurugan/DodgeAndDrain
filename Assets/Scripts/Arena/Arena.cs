using UnityEngine;

// The bounded play field, centred on this object. The player is kept inside
// it, drainables live in it, enemies spawn inside it, and the follow camera
// never shows past it. A thin border marks the walls.
public class Arena : MonoBehaviour
{
    public static Arena Instance { get; private set; }

    [Tooltip("Width and height in world units. The camera view is 30 units tall (about 53 wide on a 16:9 phone).")]
    [SerializeField] private Vector2 size = new Vector2(84f, 48f);

    [Header("Border")]
    [SerializeField] private bool showBorder = true;
    [SerializeField] private Color borderColor = new Color(1f, 0.3f, 0.46f, 0.55f);
    [SerializeField, Min(0.01f)] private float borderWidth = 0.22f;

    private LineRenderer border;

    public Vector2 Center => transform.position;
    public Vector2 Size => size;
    public Vector2 Min => Center - size * 0.5f;
    public Vector2 Max => Center + size * 0.5f;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        if (showBorder)
            BuildBorder();
    }

    // Keeps a point inside the walls, margin units in from them.
    public Vector2 Clamp(Vector2 point, float margin = 0f)
    {
        Vector2 min = Min + Vector2.one * margin;
        Vector2 max = Max - Vector2.one * margin;
        return new Vector2(
            min.x <= max.x ? Mathf.Clamp(point.x, min.x, max.x) : Center.x,
            min.y <= max.y ? Mathf.Clamp(point.y, min.y, max.y) : Center.y);
    }

    public bool Contains(Vector2 point, float margin = 0f)
    {
        Vector2 min = Min + Vector2.one * margin;
        Vector2 max = Max - Vector2.one * margin;
        return point.x >= min.x && point.x <= max.x && point.y >= min.y && point.y <= max.y;
    }

    public Vector2 RandomPoint(float margin = 0f)
    {
        Vector2 min = Min + Vector2.one * margin;
        Vector2 max = Max - Vector2.one * margin;
        return new Vector2(Random.Range(min.x, max.x), Random.Range(min.y, max.y));
    }

    private void BuildBorder()
    {
        // Borrow the player's sprite material and sorting so the border draws
        // with the rest of the gameplay (same approach as the feedback rings).
        GameManager gameManager = GameManager.Instance;
        PlayerMovement2D player = gameManager != null ? gameManager.player : null;
        SpriteRenderer reference = player != null
            ? (player.VisualRenderer != null ? player.VisualRenderer : player.GetComponent<SpriteRenderer>())
            : null;
        if (reference == null || reference.sharedMaterial == null)
            return;

        GameObject borderObject = new GameObject("ArenaBorder");
        borderObject.transform.SetParent(transform, false);
        border = borderObject.AddComponent<LineRenderer>();
        border.useWorldSpace = false;
        border.loop = true;
        border.positionCount = 4;
        Vector2 half = size * 0.5f;
        border.SetPosition(0, new Vector3(-half.x, -half.y, 0f));
        border.SetPosition(1, new Vector3(-half.x, half.y, 0f));
        border.SetPosition(2, new Vector3(half.x, half.y, 0f));
        border.SetPosition(3, new Vector3(half.x, -half.y, 0f));
        border.startWidth = borderWidth;
        border.endWidth = borderWidth;
        border.startColor = borderColor;
        border.endColor = borderColor;
        border.numCornerVertices = 2;
        border.sharedMaterial = reference.sharedMaterial;
        border.sortingLayerID = reference.sortingLayerID;
        border.sortingOrder = reference.sortingOrder - 1;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.46f, 0.8f);
        Gizmos.DrawWireCube(transform.position, new Vector3(size.x, size.y, 0f));
    }
}
