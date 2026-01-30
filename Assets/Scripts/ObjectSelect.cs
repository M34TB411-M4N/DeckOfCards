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

    [Header("Drag Settings")]
    [SerializeField] private float dragThresholdPixels = 8f;

    private enum InputState {
        Idle,
        PressedObject,
        DeckDragging,
        CardDragging,
        ChoosingDeckForCard
    }

    private InputState state = InputState.Idle;

    // pointer / press tracking
    private Vector2 pressScreenPos;
    private GameObject pressedCandidate;    // object under pointer on pointer-down (no selection yet)
    private GameObject pressedObject;       // object being dragged once drag begins
    private Rigidbody pressedRb;

    // drag math
    private Plane dragPlane;
    private Vector3 dragOffset;
    private float lockedY;

    // table collider for bounds
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

    // --------------------------
    // Input helpers (mouse + touch)
    // --------------------------
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
        return Input.touchCount > 0 && (Input.GetTouch(0).phase == TouchPhase.Moved || Input.GetTouch(0).phase == TouchPhase.Stationary);
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

    private void HandlePointer() {
        // Pointer down: record candidate but do not select yet.
        if (PointerDown()) {
            // If pointer is over UI, do nothing (keep menus)
            if (IsPointerOverUI()) {
                // do not change menus or selection
            } else {
                // new interaction, clear context
                HideAllMenus();
                ClearSelectionHighlight();
            }

            pressScreenPos = PointerPosition();
            state = InputState.PressedObject;

            // record the candidate (no selection yet)
            pressedCandidate = null;
            Ray ray = GetRayOnPointer();
            if (Physics.Raycast(ray, out RaycastHit hit)) {
                pressedCandidate = hit.collider.gameObject;
                // prepare locked Y for drag if it starts
                lockedY = pressedCandidate.transform.position.y;
                dragPlane = new Plane(Vector3.up, new Vector3(0f, lockedY, 0f));
                // do NOT compute dragOffset yet; compute in BeginDrag to avoid snap if pointer moved
            }
        }

        // Pointer held: check threshold to start drag
        if (PointerHeld() && state == InputState.PressedObject) {
            if (Vector2.Distance(pressScreenPos, PointerPosition()) >= dragThresholdPixels) {
                BeginDrag();
            }
        }

        // While dragging, update object
        if (state == InputState.DeckDragging || state == InputState.CardDragging) {
            if (PointerHeld())
                DragObjectPhysics();
        }

        // Pointer up: either confirm click or end drag
        if (PointerUp()) {
            if (state == InputState.PressedObject) {
                // pointer released without exceeding threshold -> confirmed click
                ConfirmClick();
            } else if (state == InputState.DeckDragging || state == InputState.CardDragging) {
                EndDrag();
            }

            // cleanup
            pressedCandidate = null;
            pressedObject = null;
            pressedRb = null;
            state = InputState.Idle;
        }
    }

    // --------------------------
    // Drag lifecycle
    // --------------------------
    private void BeginDrag() {
        if (pressedCandidate == null) {
            state = InputState.Idle;
            return;
        }

        // choose the dragged object
        pressedObject = pressedCandidate;
        pressedRb = pressedObject.GetComponent<Rigidbody>();
        if (pressedRb == null) {
            // cannot drag objects without rigidbody in this system
            pressedObject = null;
            pressedRb = null;
            state = InputState.Idle;
            return;
        }

        // compute offset so object doesn't jump to pointer
        Ray ray = GetRayOnPointer();
        if (dragPlane.Raycast(ray, out float enter)) {
            Vector3 hitPoint = ray.GetPoint(enter);
            dragOffset = pressedObject.transform.position - hitPoint;
        } else {
            dragOffset = Vector3.zero;
        }

        // decide drag type
        if (pressedObject.GetComponent<Deck>() != null)
            state = InputState.DeckDragging;
        else if (pressedObject.GetComponent<CardView>() != null)
            state = InputState.CardDragging;
        else
            state = InputState.Idle;
    }

    private void DragObjectPhysics() {
        if (pressedRb == null || pressedObject == null)
            return;

        Ray ray = GetRayOnPointer();
        Vector3 worldPoint;
        // use plane intersection if possible
        if (dragPlane.Raycast(ray, out float enter))
            worldPoint = ray.GetPoint(enter);
        else
            return;

        Vector3 target = worldPoint + dragOffset;
        target.y = lockedY;

        // clamp by table bounds using object's extents
        if (tableCollider != null) {
            Bounds tableB = tableCollider.bounds;
            Collider objCol = pressedObject.GetComponent<Collider>();
            if (objCol != null) {
                Bounds objB = objCol.bounds;
                float halfX = objB.extents.x;
                float halfZ = objB.extents.z;

                target.x = Mathf.Clamp(target.x, tableB.min.x + halfX, tableB.max.x - halfX);
                target.z = Mathf.Clamp(target.z, tableB.min.z + halfZ, tableB.max.z - halfZ);
            } else {
                // fallback: clamp center
                target.x = Mathf.Clamp(target.x, tableB.min.x, tableB.max.x);
                target.z = Mathf.Clamp(target.z, tableB.min.z, tableB.max.z);
            }
        }

        // Move via physics (keeps collisions correct)
        pressedRb.MovePosition(target);
    }

    private void EndDrag() {
        if (pressedRb != null) {
            // stop residual motion to avoid glide
            pressedRb.linearVelocity = Vector3.zero;
            pressedRb.angularVelocity = Vector3.zero;
        }
    }

    // --------------------------
    // Click confirmation (selection & menus)
    // --------------------------
    private void ConfirmClick() {
        if (pressedCandidate == null)
            return;

        // If choosing deck to add a card, that flow takes precedence
        if (state == InputState.ChoosingDeckForCard) {
            TryConsumeCardToDeck(pressedCandidate);
            return;
        }

        // Normal click: select and show menu
        ClearLogicalSelection();
        SelectObject(pressedCandidate);

        Deck d = pressedCandidate.GetComponent<Deck>();
        if (d != null) {
            selectedDeck = d;
            Vector3 pos = PointerPosition() + new Vector2(100f, -100f);
            deckMenu.Show(d, pos);
            return;
        }

        CardView cv = pressedCandidate.GetComponent<CardView>();
        if (cv != null) {
            selectedCardView = cv;
            Vector3 pos = PointerPosition() + new Vector2(100f, -100f);
            cardMenu.Show(cv, pos);
        }
    }

    // --------------------------
    // Card to Deck flow
    // --------------------------
    private void TryConsumeCardToDeck(GameObject clickedObject) {
        Deck deck = clickedObject.GetComponent<Deck>();
        if (deck == null || selectedCardView == null)
            return;

        deck.AddCard(selectedCardView.GetCardData());
        Destroy(selectedCardView.gameObject);

        selectedCardView = null;
        HideAllMenus();
        state = InputState.Idle;
    }

    // --------------------------
    // Selection helpers and menus
    // --------------------------
    private void SelectObject(GameObject obj) {
        selectedObject = obj;
        MeshRenderer mr = selectedObject.GetComponent<MeshRenderer>();
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

    // --------------------------
    // Menu callbacks
    // --------------------------
    public void OnDeckMenuDrawPressed() {
        if (selectedDeck == null) return;
        selectedDeck.DrawCard();
        HideAllMenus();
        state = InputState.Idle;
    }

    public void OnCardMenuAddToDeckPressed() {
        if (selectedCardView == null) return;
        HideAllMenus();
        cancelDeckAddMenu.Show();
        state = InputState.ChoosingDeckForCard;
    }

    public void onCancelDeckAddPressed() {
        HideAllMenus();
        state = InputState.Idle;
    }

    // --------------------------
    // Utility
    // --------------------------
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
