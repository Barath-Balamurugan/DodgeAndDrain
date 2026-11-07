using System.Collections.Generic;
using UnityEngine;

// Follows the player with a dead zone: the player moves freely in the middle
// of the screen and the camera only catches up, smoothly, once they push past
// it. The camera never shows outside the Arena. Effects that move the camera
// themselves (the wave-1 intro zoom, the power-up zoom) put the follow on hold
// while they run.
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public static CameraFollow Instance { get; private set; }

    [Tooltip("Free-movement box in the middle of the screen, as a fraction of the view (0.36 = middle 36%).")]
    [SerializeField] private Vector2 deadZoneFraction = new Vector2(0.36f, 0.36f);
    [Tooltip("How quickly the camera catches up once the player leaves the dead zone. Higher = snappier.")]
    [SerializeField, Min(0.1f)] private float followSharpness = 8f;
    [Tooltip("How far past the arena wall the camera may show, in world units, so the wall reads as a wall instead of the screen edge.")]
    [SerializeField, Min(0f)] private float viewBeyondWall = 2f;

    private Camera cam;
    private readonly HashSet<string> holds = new HashSet<string>();

    private void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Pause / resume following for a named reason (several can overlap).
    public void SetHold(string reason, bool held)
    {
        if (held)
            holds.Add(reason);
        else
            holds.Remove(reason);
    }

    private void LateUpdate()
    {
        if (holds.Count > 0 || !cam.orthographic)
            return;

        Transform target = GetTarget();
        if (target == null)
            return;

        Vector3 position = transform.position;
        Vector2 halfDeadZone = Vector2.Scale(ViewSize(cam.orthographicSize), deadZoneFraction) * 0.5f;
        Vector2 offset = (Vector2)target.position - (Vector2)position;
        Vector3 goal = position;

        if (offset.x > halfDeadZone.x)
            goal.x = target.position.x - halfDeadZone.x;
        else if (offset.x < -halfDeadZone.x)
            goal.x = target.position.x + halfDeadZone.x;

        if (offset.y > halfDeadZone.y)
            goal.y = target.position.y - halfDeadZone.y;
        else if (offset.y < -halfDeadZone.y)
            goal.y = target.position.y + halfDeadZone.y;

        goal = ClampToArena(goal, cam.orthographicSize);
        float blend = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        transform.position = Vector3.Lerp(position, goal, blend);
    }

    // Where the camera may sit so that a view of this size stays inside the arena.
    public Vector3 ClampToArena(Vector3 position, float orthoSize)
    {
        Arena arena = Arena.Instance;
        if (arena == null || cam == null)
            return position;

        Vector2 half = ViewSize(orthoSize) * 0.5f - Vector2.one * viewBeyondWall;
        Vector2 min = arena.Min + half;
        Vector2 max = arena.Max - half;
        position.x = min.x <= max.x ? Mathf.Clamp(position.x, min.x, max.x) : arena.Center.x;
        position.y = min.y <= max.y ? Mathf.Clamp(position.y, min.y, max.y) : arena.Center.y;
        return position;
    }

    // The resting camera position for a view centred on a point (e.g. the player).
    public Vector3 SettledPositionFor(Vector2 focus, float orthoSize)
    {
        return ClampToArena(new Vector3(focus.x, focus.y, transform.position.z), orthoSize);
    }

    // Jump straight to the player (new run / restart).
    public void SnapToTarget()
    {
        Transform target = GetTarget();
        if (target != null && cam != null)
            transform.position = SettledPositionFor(target.position, cam.orthographicSize);
    }

    // True if a world point is on screen; inset shrinks the view by that many units.
    public static bool IsInView(Vector2 point, float inset = 0f)
    {
        Camera camera = Instance != null ? Instance.cam : Camera.main;
        if (camera == null || !camera.orthographic)
            return true;

        Vector2 half = new Vector2(camera.orthographicSize * camera.aspect, camera.orthographicSize) - Vector2.one * inset;
        Vector2 delta = point - (Vector2)camera.transform.position;
        return Mathf.Abs(delta.x) <= half.x && Mathf.Abs(delta.y) <= half.y;
    }

    private Vector2 ViewSize(float orthoSize)
    {
        return new Vector2(orthoSize * 2f * cam.aspect, orthoSize * 2f);
    }

    private static Transform GetTarget()
    {
        GameManager gameManager = GameManager.Instance;
        return gameManager != null && gameManager.player != null ? gameManager.player.transform : null;
    }
}
