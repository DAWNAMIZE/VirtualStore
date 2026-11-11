using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Minimal fixed on-screen virtual joystick for touch devices.
/// Exposes a normalized value (x = right, y = up) that the
/// FirstPersonController reads every frame.
/// </summary>
public class MobileJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public RectTransform background;
    public RectTransform knob;
    public float radius = 92f;

    public Vector2 Value { get; private set; }
    public bool IsDragging { get; private set; }
    public int FingerId { get; private set; } = -1;

    Canvas canvas;

    void Awake()
    {
        if (background == null)
            background = transform as RectTransform;
        canvas = GetComponentInParent<Canvas>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        IsDragging = true;
        FingerId = eventData.pointerId;
        UpdateFromPointer(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != FingerId) return;
        UpdateFromPointer(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != FingerId) return;
        IsDragging = false;
        FingerId = -1;
        Value = Vector2.zero;
        if (knob != null)
            knob.anchoredPosition = Vector2.zero;
    }

    void UpdateFromPointer(PointerEventData eventData)
    {
        if (background == null) return;

        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                background, eventData.position, cam, out Vector2 local))
            return;

        Vector2 value = Vector2.ClampMagnitude(local / radius, 1f);
        Value = value;

        if (knob != null)
            knob.anchoredPosition = value * radius;
    }
}
