using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class ObjectSelect : MonoBehaviour {
    [Header("Selection")]
    public Material selectedMat;
    private Material prevMat;
    private GameObject selectedObject;

    private Deck selectedDeck;
    private CardView selectedCardView;

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
    // removed savedConstraints — Hover handles constraints now
    private DraggedMarker draggedMarker; // marker we add while dragging

    [SerializeField] private float markerKeepTime = 0.15f; // how long to keep marker after release
    private Coroutine removeMarkerCoroutine = null;

    private Plane dragPlane;
    private Vector3 dragOffset;
    private float lockedY;

    // Add this field with the rest of the private fields:
    private bool suppressNextPointerUp = false;


    private Collider tableCollider;

    void Start() {
        if (deckMenu != null) deckMenu.controller = this;
        if (cardMenu != null) cardMenu.controller = this;
        if (cancelDeckAddMenu != null) cancelDeckAddMenu.controller = this;

        HideAllMenus();

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

    // --------------------
    // Input
    // --------------------

    private void HandlePointer() {
        if (PointerDown()) {
            // If we're in ChoosingDeckForCard, do NOT clear menus or selection on pointer down.
            // We still want to record press position and what was under the pointer.
            if (state != InputState.ChoosingDeckForCard) {
                if (!IsPointerOverUI()) {
                    HideAllMenus();
                    ClearSelectionHighlight();
                }
                // only set pressed state if we're not in choosing mode
                state = InputState.PressedObject;
            }

            pressScreenPos = PointerPosition();

            pressedCandidate = null;
            Ray ray = GetRayOnPointer();
            if (Physics.Raycast(ray, out RaycastHit hit)) {
                pressedCandidate = hit.collider.gameObject;
            }
        }

        if (PointerHeld() && state == InputState.PressedObject) {
            if (Vector2.Distance(pressScreenPos, PointerPosition()) >= dragThresholdPixels) {
                BeginDrag();
            }
        }

        if (PointerUp()) {
            // If the last action set this flag (UI button just fired), consume this pointer-up here
            // so it doesn't also act as a world click. This prevents the UI click from immediately
            // being interpreted as "click the world" and clobbering the choosing state.
            if (suppressNextPointerUp) {
                suppressNextPointerUp = false;
                // Do not call ConfirmClick or EndDrag; keep the current state (likely ChoosingDeckForCard).
                // Return early so the next pointer-up will be a real world click.
                return;
            }

            // Normal behavior: confirm click if pressed, or if we're choosing a deck still allow confirm
            if (state == InputState.PressedObject || state == InputState.ChoosingDeckForCard) {
                ConfirmClick();
            }

            EndDrag();
            state = InputState.Idle;
        }

    }

    // --------------------
    // Dragging
    // --------------------

    private void BeginDrag() {
        if (pressedCandidate == null)
            return;

        draggedRb = pressedCandidate.GetComponent<Rigidbody>();
        if (draggedRb == null)
            return;

        // Get or add marker for collision detection with ROs
        draggedMarker = pressedCandidate.GetComponent<DraggedMarker>();

        // get hover component (if present) and start hover BEFORE we start moving
        hoverComponent = pressedCandidate.GetComponent<HoverWhileDragged>();
        if (hoverComponent != null) {
            hoverComponent.BeginHover();
        }

        if (draggedMarker == null)
            draggedMarker = pressedCandidate.AddComponent<DraggedMarker>();

        // If a delayed-remove coroutine was pending, cancel it
        if (removeMarkerCoroutine != null) {
            StopCoroutine(removeMarkerCoroutine);
            removeMarkerCoroutine = null;
        }

        draggedRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        draggedRb.interpolation = RigidbodyInterpolation.Interpolate;

        // compute drag offset so the object doesn't snap
        Ray ray = GetRayOnPointer();
        if (dragPlane.Raycast(ray, out float enter)) {
            Vector3 hitPoint = ray.GetPoint(enter);
            dragOffset = draggedRb.position - hitPoint;
        }

        state = InputState.Dragging;
    }

    private void ApplyDragVelocity() {
        Ray ray = GetRayOnPointer();

        // Raycast against the table only to get X/Z intent
        if (!Physics.Raycast(ray, out RaycastHit hit, 100f))
            return;

        Vector3 targetXZ = new Vector3(
            hit.point.x,
            draggedRb.position.y, // hover owns Y
            hit.point.z
        );

        Vector3 toTarget = targetXZ - draggedRb.position;
        toTarget.y = 0f;

        Vector3 desiredVelocity = toTarget * dragResponsiveness;
        if (desiredVelocity.magnitude > maxDragSpeed)
            desiredVelocity = desiredVelocity.normalized * maxDragSpeed;

        Vector3 v = draggedRb.linearVelocity;
        v.x = desiredVelocity.x;
        v.z = desiredVelocity.z;

        draggedRb.linearVelocity = v;
    }



    private void ApplySoftBounds() {
        if (tableCollider == null || draggedRb == null)
            return;

        Bounds tb = tableCollider.bounds;
        Vector3 pos = draggedRb.position;
        Vector3 push = Vector3.zero;

        if (pos.x < tb.min.x) push.x = 1f;
        else if (pos.x > tb.max.x) push.x = -1f;

        if (pos.z < tb.min.z) push.z = 1f;
        else if (pos.z > tb.max.z) push.z = -1f;

        if (push != Vector3.zero)
            draggedRb.AddForce(push.normalized * boundsPushForce, ForceMode.Acceleration);
    }

    private IEnumerator RemoveDraggedMarkerAfterDelay(GameObject markerObject, float delay) {
        yield return new WaitForSeconds(delay);

        if (markerObject != null) {
            DraggedMarker m = markerObject.GetComponent<DraggedMarker>();
            if (m != null) {
                Destroy(m);
            }
        }

        removeMarkerCoroutine = null;
    }

    private void EndDrag() {
        // First: tell hover to stop and restore constraints/gravity so the body is back to physics control
        if (hoverComponent != null) {
            hoverComponent.EndHover();
            hoverComponent = null;
        }

        if (draggedRb != null) {
            // Do not restore constraints here — Hover restored them.
            // Zero horizontal velocity but keep or enforce a small downward Y so gravity starts working
            Vector3 cur = draggedRb.linearVelocity;
            cur.x = 0f;
            cur.z = 0f;

            // Optionally stop any angular spin introduced while dragging
            // This makes released objects more stable; remove this line if you want release-spin preserved.
            draggedRb.angularVelocity = Vector3.zero;
        }

        // schedule marker removal if present (short grace period to let RO exit/dampen)
        if (draggedMarker != null) {
            if (removeMarkerCoroutine != null)
                StopCoroutine(removeMarkerCoroutine);

            removeMarkerCoroutine = StartCoroutine(RemoveDraggedMarkerAfterDelay(draggedMarker.gameObject, markerKeepTime));
            draggedMarker = null;
        }

        // Clear references
        draggedRb = null;
        pressedCandidate = null;
    }

    // --------------------
    // Click logic
    // --------------------

    private void ConfirmClick() {
        // --------------------
        // Special: choosing deck for card
        // --------------------
        if (state == InputState.ChoosingDeckForCard) {
            // Do a fresh raycast at pointer-up position to get the actual object under the pointer now.
            Ray ray = GetRayOnPointer();
            if (Physics.Raycast(ray, out RaycastHit hit)) {
                // try parent as well in case collider is on a child
                Deck clickedDeck = hit.collider.GetComponentInParent<Deck>() ?? hit.collider.GetComponent<Deck>();
                if (clickedDeck != null && selectedCardView != null) {
                    // Add card data to the clicked deck
                    clickedDeck.AddCard(selectedCardView.GetCardData());

                    // Destroy the visual card and clear selection
                    Destroy(selectedCardView.gameObject);
                    selectedCardView = null;

                    // Cleanup and return (do NOT open deck menu)
                    HideAllMenus();
                    ClearSelectionHighlight();
                    ClearLogicalSelection();
                    state = InputState.Idle;
                    return;
                }
            }

            // If we reach here, it wasn't a deck (or nothing hit). Treat as cancel.
            Debug.Log("Add-to-deck cancelled: click not on a deck.");
            HideAllMenus();
            ClearSelectionHighlight();
            ClearLogicalSelection();
            state = InputState.Idle;
            return;
        }

        // --------------------
        // Normal click flow
        // --------------------
        if (pressedCandidate == null)
            return;

        ClearLogicalSelection();
        SelectObject(pressedCandidate);

        Deck d = pressedCandidate.GetComponent<Deck>();
        if (d != null) {
            selectedDeck = d;
            deckMenu.Show(d, PointerPosition() + new Vector2(100f, -100f));
            return;
        }

        CardView cv = pressedCandidate.GetComponent<CardView>();
        if (cv != null) {
            selectedCardView = cv;
            cardMenu.Show(cv, PointerPosition() + new Vector2(100f, -100f));
        }
    }



    // --------------------
    // Menu callbacks (restored)
    // --------------------

    public void OnDeckMenuDrawPressed() {
        if (selectedDeck == null)
            return;

        selectedDeck.DrawCard();
        HideAllMenus();
        state = InputState.Idle;
    }



    public void OnCardMenuAddToDeckPressed() {
        if (selectedCardView == null)
            return;

        // Suppress the immediate pointer-up that caused this UI click so it doesn't also
        // register as a world click. The actual deck selection will come from the next click.
        suppressNextPointerUp = true;

        HideAllMenus();
        cancelDeckAddMenu.Show();
        state = InputState.ChoosingDeckForCard;
    }


    public void onCancelDeckAddPressed() {
        HideAllMenus();
        state = InputState.Idle;
    }

    // --------------------
    // Helpers
    // --------------------

    private void SelectObject(GameObject obj) {
        selectedObject = obj;
        MeshRenderer mr = obj.GetComponent<MeshRenderer>();
        if (mr != null) {
            prevMat = mr.material;
            mr.material = selectedMat;
        }
    }

    private void ClearSelectionHighlight() {
        if (selectedObject == null)
            return;

        MeshRenderer mr = selectedObject.GetComponent<MeshRenderer>();
        if (mr != null && prevMat != null)
            mr.material = prevMat;

        selectedObject = null;
        prevMat = null;
    }

    private void ClearLogicalSelection() {
        selectedDeck = null;
        selectedCardView = null;
    }

    private void HideAllMenus() {
        if (deckMenu != null && deckMenu.GetActive()) deckMenu.Hide();
        if (cardMenu != null && cardMenu.GetActive()) cardMenu.Hide();
        if (cancelDeckAddMenu != null && cancelDeckAddMenu.GetActive()) cancelDeckAddMenu.Hide();
    }

    // --------------------
    // Input utils
    // --------------------

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

    private Ray GetRayOnPointer() {
#if UNITY_ANDROID
        return Camera.main.ScreenPointToRay(Input.GetTouch(0).position);
#else
        return Camera.main.ScreenPointToRay(Input.mousePosition);
#endif
    }

    private bool IsPointerOverUI() {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
