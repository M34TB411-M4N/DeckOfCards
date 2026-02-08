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

    private HoverWhileDragged hoverComponent;

    private enum InputState {
        Idle,
        PressedObject,
        Dragging,
        ChoosingDeckForCard
    }

    private InputState state = InputState.Idle;

    private Vector2 pointerDownScreenPos;
    private bool didDrag;

    private GameObject pressedCandidate;
    private Rigidbody draggedRb;
    private DraggedMarker draggedMarker;

    [SerializeField] private float markerKeepTime = 0.15f;
    private Coroutine removeMarkerCoroutine;

    private Collider tableCollider;

    // NEW: pending data for Add->ChooseDeck flow
    private Card pendingCard = null;
    private GameObject pendingCardGO = null;

    // NEW: suppress the immediate pointer-up world-clear after a UI click that opened a menu
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
        bool overUI = IsPointerOverUI();

        if (PointerDown()) {
            pointerDownScreenPos = PointerPosition();
            didDrag = false;

            // If pointer started on UI, don't process a world press here
            if (overUI)
                return;

            if (state == InputState.Idle) {
                HideAllMenus();
                ClearSelection();
                state = InputState.PressedObject;
            }

            pressedCandidate = RaycastWorldObject();
        }

        if (PointerHeld() && state == InputState.PressedObject) {
            if (Vector2.Distance(pointerDownScreenPos, PointerPosition()) >= dragThresholdPixels) {
                BeginDrag();
                didDrag = true;
            }
        }

        if (PointerUp()) {
            // If the pointer-up is over UI and we were just opening a UI flow, consume it
            if (overUI) {
                if (suppressNextPointerUp) {
                    suppressNextPointerUp = false;
                    return; // allow UI to handle the click; do not clear menus here
                }

                // Normal pointer-up over UI: clear selection/menus and do nothing else
                ClearSelectionAndMenus();
                return;
            }

            // If we are in choosing-deck mode, that mode owns this click
            if (state == InputState.ChoosingDeckForCard) {
                HandleAddToDeckClick();
                return;
            }

            if (state == InputState.Dragging) {
                EndDrag();
                state = InputState.Idle;
                return;
            }

            if (state == InputState.PressedObject && !didDrag) {
                ConfirmClick();
            }

            EndDrag();
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

        if (removeMarkerCoroutine != null) {
            StopCoroutine(removeMarkerCoroutine);
            removeMarkerCoroutine = null;
        }

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
        if (pressedCandidate == null)
            return;

        SelectObject(pressedCandidate);
    }

    // NEW: dedicated handler for the "choose a deck to add the pending card" click
    private void HandleAddToDeckClick() {
        GameObject hitObj = RaycastWorldObject();

        if (hitObj != null) {
            Deck deck = hitObj.GetComponentInParent<Deck>();
            if (deck != null && pendingCard != null) {
                // add the pending card and destroy its visual
                deck.AddCard(pendingCard);
                if (pendingCardGO != null)
                    Destroy(pendingCardGO);
            }
        }

        // clear pending and UI
        pendingCard = null;
        pendingCardGO = null;

        ClearSelectionAndMenus();
        state = InputState.Idle;
    }

    private void SelectObject(GameObject obj) {
        ClearSelection();

        if (highlight != null)
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

    public void OnCardMenuAddToDeckPressed() {
        if (selectedCardView == null)
            return;

        // Capture the card data + visual before we clear selection
        pendingCard = selectedCardView.GetCardData();
        pendingCardGO = selectedCardView.gameObject;

        // Clear selection and menus (we're entering a modal "choose deck" flow)
        ClearSelection();
        HideAllMenus();

        // Show the small cancel/choose UI and enter modal state
        cancelDeckAddMenu.Show();

        // Prevent the immediate pointer-up (that triggered this UI click) from clearing the menu
        suppressNextPointerUp = true;

        state = InputState.ChoosingDeckForCard;
    }

    public void onCancelDeckAddPressed() {
        // cancel the modal flow and drop the pending card
        pendingCard = null;
        pendingCardGO = null;

        ClearSelectionAndMenus();
        state = InputState.Idle;
    }

    // Called by menus when they finish an action that should reset selection/menus
    public void MenuActionCompleted() {
        ClearSelectionAndMenus();
        state = InputState.Idle;
    }

    // =====================
    // HELPERS
    // =====================

    private void ClearSelection() {
        selectedDeck = null;
        selectedCardView = null;
        if (highlight != null)
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
