using UnityEngine;
using UnityEngine.EventSystems;

public class Joystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    public RectTransform background;
    public RectTransform handle;
    public float handleLimit = 1f; // how far handle can move in radius (0–1 normalized)

    private Vector2 inputVector;

    public Vector2 Direction => inputVector; // public read-only

    private void Start()
    {
        if (background == null)
            background = GetComponent<RectTransform>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 pos;
        // convert screen pos → local pos in joystick BG
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background,
            eventData.position,
            eventData.pressEventCamera,
            out pos))
        {
            // pos.x, pos.y are in pixels relative to center
            // normalize to -1…1
            pos.x /= background.sizeDelta.x / 2f;
            pos.y /= background.sizeDelta.y / 2f;

            // clamp to circle of radius 1
            inputVector = Vector2.ClampMagnitude(pos, 1f);

            // move handle visually
            handle.anchoredPosition = inputVector * (background.sizeDelta.x / 2f * handleLimit);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        inputVector = Vector2.zero;
        handle.anchoredPosition = Vector2.zero;
    }
}
