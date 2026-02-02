using UnityEngine;
using UnityEngine.EventSystems;

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
    private RigidbodyConstraints savedConstraints;
    private DraggedMarker draggedMarker; // marker we add while dragging

    private Plane dragPlane;
    private Vector3 dragOffset;
    private float lockedY;

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
            if (!IsPointerOverUI()) {
                HideAllMenus();
                ClearSelectionHighlight();
            }

            pressScreenPos = PointerPosition();
            state = InputState.PressedObject;

            pressedCandidate = null;
            Ray ray = GetRayOnPointer();
            if (Physics.Raycast(ray, out RaycastHit hit)) {
                pressedCandidate = hit.collider.gameObject;
                lockedY = pressedCandidate.transform.position.y;
                dragPlane = new Plane(Vector3.up, new Vector3(0f, lockedY, 0f));
            }
        }

        if (PointerHeld() && state == InputState.PressedObject) {
            if (Vector2.Distance(pressScreenPos, PointerPosition()) >= dragThresholdPixels) {
                BeginDrag();
            }
        }

        if (PointerUp()) {
            if (state == InputState.PressedObject) {
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

        // mark dragged object so other objects can notice collisions with it
        draggedMarker = pressedCandidate.GetComponent<DraggedMarker>();
        if (draggedMarker == null)
            draggedMarker = pressedCandidate.AddComponent<DraggedMarker>();

        draggedRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        draggedRb.interpolation = RigidbodyInterpolation.Interpolate;

        // Freeze rotation ONLY for the dragged object so PO doesn't spin while being moved
        savedConstraints = draggedRb.constraints;
        draggedRb.constraints = RigidbodyConstraints.FreezeRotation;

        Ray ray = GetRayOnPointer();
        if (dragPlane.Raycast(ray, out float enter)) {
            Vector3 hitPoint = ray.GetPoint(enter);
            dragOffset = draggedRb.position - hitPoint;
        }

        state = InputState.Dragging;
    }

    private void ApplyDragVelocity() {
        Ray ray = GetRayOnPointer();
        if (!dragPlane.Raycast(ray, out float enter))
            return;

        Vector3 target = ray.GetPoint(enter) + dragOffset;
        target.y = lockedY;

        Vector3 toTarget = target - draggedRb.position;

        // desired velocity tries to reach the target in a single fixed step scaled by responsiveness
        Vector3 desiredVelocity = toTarget * dragResponsiveness;
        if (desiredVelocity.magnitude > maxDragSpeed)
            desiredVelocity = desiredVelocity.normalized * maxDragSpeed;

        // Smoothly pull toward desired velocity but do not apply forces to other objects directly
        draggedRb.linearVelocity = Vector3.Lerp(
            draggedRb.linearVelocity,
            desiredVelocity,
            Time.fixedDeltaTime * dragResponsiveness
        );
    }

    private void ApplySoftBounds() {
        if (tableCollider == null)
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

    private void EndDrag() {
        if (draggedRb != null) {
            // restore saved rotation constraints
            draggedRb.constraints = savedConstraints;

            // ensure PO stops completely when user releases
            draggedRb.linearVelocity = Vector3.zero;
            draggedRb.angularVelocity = Vector3.zero;
        }

        // remove marker component if present
        if (draggedMarker != null) {
            Destroy(draggedMarker);
            draggedMarker = null;
        }

        draggedRb = null;
        pressedCandidate = null;
    }

    // --------------------
    // Click logic
    // --------------------

    private void ConfirmClick() {
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
