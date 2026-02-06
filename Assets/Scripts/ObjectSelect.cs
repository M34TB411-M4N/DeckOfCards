using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class ObjectSelect : MonoBehaviour {
    [Header("Menus")]
    [SerializeField] private DeckMenu deckMenu;
    [SerializeField] private CardMenu cardMenu;
    [SerializeField] private CancelDeckAddMenu cancelDeckAddMenu;

    [Header("Drag")]
    [SerializeField] private float dragThresholdPixels = 8f;
    [SerializeField] private float maxDragSpeed = 10f;
    [SerializeField] private float dragResponsiveness = 25f;

    [Header("Bounds")]
    [SerializeField] private float boundsPushForce = 40f;

    [Header("Highlight")]
    [SerializeField] private HighlightController highlight;

    private Deck selectedDeck;
    private CardView selectedCardView;

    private HoverWhileDragged hoverComponent = null;

    private enum InputState {
        Idle,
        PressedObject,
        Dragging,
        ChoosingDeckForCard
    }

    private InputState state = InputState.Idle;

    private Vector2 pressScreenPos;
    private GameObject pressedCandidate;

    private Rigidbody draggedRb;
    private DraggedMarker draggedMarker;

    [SerializeField] private float markerKeepTime = 0.15f;
    private Coroutine removeMarkerCoroutine = null;

    private Collider tableCollider;

    // prevents UI click pointer-up from also acting as world click
    private bool suppressNextPointerUp = false;

    void Start() {
        if (deckMenu != null) deckMenu.controller = this;
        if (cardMenu != null) cardMenu.controller = this;
        if (cancelDeckAddMenu != null) cancelDeckAddMenu.controller = this;

        HideAllMenus();
        ClearSelection();

        GameObject floor = GameObject.FindGameObjectWithTag("Floor");
        if (floor != null)
            tableCollider = floor.GetComponent<Collider>();
    }

    void Update() {
        HandlePointer();
    }

    void FixedUpdate() {
        if (state == InputState.Dragging && draggedRb != null) {
            ApplyDragVelocity();
            ApplySoftBounds();
        }
    }

    // =====================
    // INPUT
    // =====================

    private void HandlePointer() {
        if (PointerDown()) {
            // Ignore UI clicks for world logic
            if (IsPointerOverUI())
                return;

            // Only enter pressed state if idle
            if (state == InputState.Idle) {
                HideAllMenus();
                ClearSelection();
                state = InputState.PressedObject;
            }

            pressScreenPos = PointerPosition();
            pressedCandidate = RaycastWorldObject();
        }

        if (PointerHeld() && state == InputState.PressedObject) {
            if (Vector2.Distance(pressScreenPos, PointerPosition()) >= dragThresholdPixels) {
                BeginDrag();
            }
        }

        if (PointerUp()) {
            if (suppressNextPointerUp) {
                suppressNextPointerUp = false;
                return;
            }

            if (IsPointerOverUI())
                return;

            ConfirmClick();
            EndDrag();

            if (state != InputState.ChoosingDeckForCard)
                state = InputState.Idle;
        }
    }

    // =====================
    // DRAGGING
    // =====================

    private void BeginDrag() {
        if (pressedCandidate == null)
            return;

        draggedRb = pressedCandidate.GetComponent<Rigidbody>();
        if (draggedRb == null)
            return;

        hoverComponent = pressedCandidate.GetComponent<HoverWhileDragged>();
        if (hoverComponent != null)
            hoverComponent.BeginHover();

        draggedMarker = pressedCandidate.GetComponent<DraggedMarker>();
        if (draggedMarker == null)
            draggedMarker = pressedCandidate.AddComponent<DraggedMarker>();

        draggedRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        draggedRb.interpolation = RigidbodyInterpolation.Interpolate;

        state = InputState.Dragging;
    }

    private void ApplyDragVelocity() {
        Ray ray = Camera.main.ScreenPointToRay(PointerPosition());
        if (!Physics.Raycast(ray, out RaycastHit hit))
            return;

        Vector3 target = new Vector3(hit.point.x, draggedRb.position.y, hit.point.z);
        Vector3 toTarget = target - draggedRb.position;
        toTarget.y = 0f;

        Vector3 desired = toTarget * dragResponsiveness;
        if (desired.magnitude > maxDragSpeed)
            desired = desired.normalized * maxDragSpeed;

        Vector3 v = draggedRb.linearVelocity;
        v.x = desired.x;
        v.z = desired.z;
        draggedRb.linearVelocity = v;
    }

    private void ApplySoftBounds() {
        if (tableCollider == null || draggedRb == null)
            return;

        Bounds b = tableCollider.bounds;
        Vector3 p = draggedRb.position;
        Vector3 push = Vector3.zero;

        if (p.x < b.min.x) push.x = 1f;
        else if (p.x > b.max.x) push.x = -1f;

        if (p.z < b.min.z) push.z = 1f;
        else if (p.z > b.max.z) push.z = -1f;

        if (push != Vector3.zero)
            draggedRb.AddForce(push.normalized * boundsPushForce, ForceMode.Acceleration);
    }

    private void EndDrag() {
        if (hoverComponent != null) {
            hoverComponent.EndHover();
            hoverComponent = null;
        }

        if (draggedRb != null)
            draggedRb.angularVelocity = Vector3.zero;

        if (draggedMarker != null) {
            if (removeMarkerCoroutine != null)
                StopCoroutine(removeMarkerCoroutine);

            removeMarkerCoroutine = StartCoroutine(RemoveDraggedMarkerAfterDelay(draggedMarker.gameObject));
            draggedMarker = null;
        }

        draggedRb = null;
        pressedCandidate = null;
    }

    private IEnumerator RemoveDraggedMarkerAfterDelay(GameObject obj) {
        yield return new WaitForSeconds(markerKeepTime);
        if (obj != null)
            Destroy(obj.GetComponent<DraggedMarker>());
    }

    // =====================
    // CLICK LOGIC
    // =====================

    private void ConfirmClick() {
        if (state == InputState.ChoosingDeckForCard) {
            GameObject hitObj = RaycastWorldObject();
            if (hitObj != null) {
                Deck deck = hitObj.GetComponentInParent<Deck>();
                if (deck != null && selectedCardView != null) {
                    deck.AddCard(selectedCardView.GetCardData());
                    Destroy(selectedCardView.gameObject);
                }
            }

            ClearSelection();
            HideAllMenus();
            state = InputState.Idle;
            return;
        }

        if (pressedCandidate == null)
            return;

        SelectObject(pressedCandidate);
    }

    private void SelectObject(GameObject obj) {
        ClearSelection();

        highlight.Show(obj);

        selectedDeck = obj.GetComponent<Deck>();
        selectedCardView = obj.GetComponent<CardView>();

        if (selectedDeck != null)
            deckMenu.Show(selectedDeck, PointerPosition());

        else if (selectedCardView != null)
            cardMenu.Show(selectedCardView, PointerPosition());
    }

    // =====================
    // UI CALLBACKS
    // =====================

    public void OnDeckMenuDrawPressed() {
        if (selectedDeck == null)
            return;

        selectedDeck.DrawCard();
        ClearSelectionAndMenus();
    }

    public void OnCardMenuAddToDeckPressed() {
        if (selectedCardView == null)
            return;

        ClearSelection();
        HideAllMenus();

        suppressNextPointerUp = true;
        cancelDeckAddMenu.Show();
        state = InputState.ChoosingDeckForCard;
    }

    public void onCancelDeckAddPressed() {
        ClearSelectionAndMenus();
        state = InputState.Idle;
    }

    // =====================
    // HELPERS
    // =====================

    private void ClearSelection() {
        selectedDeck = null;
        selectedCardView = null;
        highlight.Hide();
    }

    private void ClearSelectionAndMenus() {
        ClearSelection();
        HideAllMenus();
    }

    private void HideAllMenus() {
        if (deckMenu != null) deckMenu.Hide();
        if (cardMenu != null) cardMenu.Hide();
        if (cancelDeckAddMenu != null) cancelDeckAddMenu.Hide();
    }

    private GameObject RaycastWorldObject() {
        Ray ray = Camera.main.ScreenPointToRay(PointerPosition());
        if (Physics.Raycast(ray, out RaycastHit hit))
            return hit.collider.gameObject;
        return null;
    }

    // =====================
    // INPUT UTILS
    // =====================

    Vector2 PointerPosition() {
#if UNITY_ANDROID
        return Input.touchCount > 0 ? Input.GetTouch(0).position : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    bool PointerDown() {
#if UNITY_ANDROID
        return Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    bool PointerHeld() {
#if UNITY_ANDROID
        return Input.touchCount > 0 &&
               (Input.GetTouch(0).phase == TouchPhase.Moved ||
                Input.GetTouch(0).phase == TouchPhase.Stationary);
#else
        return Input.GetMouseButton(0);
#endif
    }

    bool PointerUp() {
#if UNITY_ANDROID
        return Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Ended;
#else
        return Input.GetMouseButtonUp(0);
#endif
    }

    private bool IsPointerOverUI() {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
