using UnityEngine;
using UnityEngine.EventSystems;

public class Joystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler {
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform handle;
    [SerializeField] private float handleRange = 1f;

    private Vector2 input = Vector2.zero;
    private Canvas canvas;

    // This property allows the CameraController to read the value
    public Vector2 Direction => input;

    void Awake() {
        // This finds the Canvas component on this object or any parent
        canvas = GetComponentInParent<Canvas>();

        if (canvas == null) {
            Debug.LogError("Joystick must be placed inside a Canvas!");
        }
    }

    public void OnPointerDown(PointerEventData eventData) {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData) {
        Vector2 pos;
        // We use 'null' for Overlay mode, or the specific UI camera for Camera mode
        Camera uiCam = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : eventData.pressEventCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, uiCam, out pos)) {
            // Calculate based on the actual radius of the background
            float radius = background.sizeDelta.x / 2f;

            // Clamp the handle within the radius
            input = pos / radius;
            if (input.magnitude > 1.0f) input = input.normalized;

            handle.anchoredPosition = input * radius * handleRange;
        }
    }
    public void OnPointerUp(PointerEventData eventData) {
        input = Vector2.zero;
        handle.anchoredPosition = Vector2.zero;
    }
}