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
    [SerializeField] private float dragThreshold = 10f;
    [SerializeField] private LayerMask floorMask;
    [SerializeField] private Collider tableBoundsCollider;

    private enum InputState {
        Idle,
        PointerDown,
        Dragging,
        ChoosingDeckForCard
    }

    private InputState state = InputState.Idle;

    private Vector2 pointerDownPos;
    private Rigidbody draggedRigidbody;
    private float lockedY;

    void Start() {
        if (deckMenu != null) deckMenu.controller = this;
        if (cardMenu != null) cardMenu.controller = this;
        if (cancelDeckAddMenu != null) cancelDeckAddMenu.controller = this;

        HideAllMenus();
    }

    void Update() {
        HandlePointer();
    }

    // -----------------------
    // Pointer Handling
    // -----------------------

    private void HandlePointer() {
        if (Input.GetMouseButtonDown(0)) {
            if (!IsPointerOverUI())
                HideAllMenus();

            pointerDownPos = Input.mousePosition;
            state = InputState.PointerDown;

            TrySelectObject();
        }

        if (Input.GetMouseButton(0) && state == InputState.PointerDown) {
            if (Vector2.Distance(pointerDownPos, Input.mousePosition) > dragThreshold) {
                BeginDrag();
            }
        }

        if (Input.GetMouseButton(0) && state == InputState.Dragging) {
            UpdateDrag();
        }

        if (Input.GetMouseButtonUp(0)) {
            EndPointer();
        }
    }

    // -----------------------
    // Selection
    // -----------------------

    private void TrySelectObject() {
        ClearSelectionHighlight();

        Ray ray = GetRayOnMousePosition();
        if (!Physics.Raycast(ray, out RaycastHit hit))
            return;

        GameObject obj = hit.transform.gameObject;

        // Special mode: choosing deck
        if (state == InputState.ChoosingDeckForCard) {
            TryConsumeCardToDeck(obj);
            return;
        }

        SelectObject(obj);

        selectedDeck = obj.GetComponent<Deck>();
        selectedCardView = obj.GetComponent<CardView>();

        if (selectedDeck != null) {
            Vector3 pos = Input.mousePosition + new Vector3(100, -100);
            deckMenu.Show(selectedDeck, pos);
        } else if (selectedCardView != null) {
            Vector3 pos = Input.mousePosition + new Vector3(100, -100);
            cardMenu.Show(selectedCardView, pos);
        }
    }

    private void SelectObject(GameObject obj) {
        selectedObject = obj;

        MeshRenderer r = selectedObject.GetComponent<MeshRenderer>();
        if (r != null) {
            prevMat = r.material;
            r.material = selectedMat;
        }
    }

    private void ClearSelectionHighlight() {
        if (selectedObject == null)
            return;

        MeshRenderer r = selectedObject.GetComponent<MeshRenderer>();
        if (r != null)
            r.material = prevMat;

        selectedObject = null;
    }

    // -----------------------
    // Dragging
    // -----------------------

    private void BeginDrag() {
        if (selectedObject == null)
            return;

        draggedRigidbody = selectedObject.GetComponent<Rigidbody>();
        if (draggedRigidbody == null)
            return;

        HideAllMenus();

        lockedY = selectedObject.transform.position.y;

        draggedRigidbody.isKinematic = true;
        draggedRigidbody.velocity = Vector3.zero;
        draggedRigidbody.angularVelocity = Vector3.zero;

        state = InputState.Dragging;
    }

    private void UpdateDrag() {
        Ray ray = GetRayOnMousePosition();
        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, floorMask))
            return;

        Vector3 target = hit.point;
        target.y = lockedY;

        if (tableBoundsCollider != null) {
            Bounds b = tableBoundsCollider.bounds;
            target.x = Mathf.Clamp(target.x, b.min.x, b.max.x);
            target.z = Mathf.Clamp(target.z, b.min.z, b.max.z);
        }

        draggedRigidbody.MovePosition(target);
    }

    private void EndPointer() {
        if (state == InputState.Dragging && draggedRigidbody != null) {
            draggedRigidbody.isKinematic = false;
            draggedRigidbody.velocity = Vector3.zero;
            draggedRigidbody.angularVelocity = Vector3.zero;
        }

        draggedRigidbody = null;
        state = InputState.Idle;
    }

    // -----------------------
    // Card > Deck Flow
    // -----------------------

    private void TryConsumeCardToDeck(GameObject obj) {
        Deck deck = obj.GetComponent<Deck>();
        if (deck == null || selectedCardView == null)
            return;

        deck.AddCard(selectedCardView.GetCardData());
        Destroy(selectedCardView.gameObject);

        selectedCardView = null;
        state = InputState.Idle;
    }

    // -----------------------
    // Menu Callbacks
    // -----------------------

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

    // -----------------------
    // Utilities
    // -----------------------

    private void HideAllMenus() {
        if (deckMenu != null && deckMenu.GetActive())
            deckMenu.Hide();

        if (cardMenu != null && cardMenu.GetActive())
            cardMenu.Hide();

        if (cancelDeckAddMenu != null && cancelDeckAddMenu.GetActive())
            cancelDeckAddMenu.Hide();
    }

    private bool IsPointerOverUI() {
        return EventSystem.current != null &&
               EventSystem.current.IsPointerOverGameObject();
    }

    private Ray GetRayOnMousePosition() {
        return Camera.main.ScreenPointToRay(Input.mousePosition);
    }
}
