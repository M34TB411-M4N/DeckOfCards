using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[DefaultExecutionOrder(-100)]
public class ObjectSelect : MonoBehaviour {
    [Header("Menus")]
    [SerializeField] private DeckMenu deckMenu;
    [SerializeField] private CardMenu cardMenu;
    [SerializeField] private CancelDeckAddMenu cancelDeckAddMenu;

    [Header("Drag Settings")]
    [SerializeField] private float dragThresholdPixels = 8f;
    [SerializeField] private float maxDragSpeed = 10f;
    [SerializeField] private float dragResponsiveness = 25f;

    [Header("Hand System")]
    [SerializeField] private float handCheckRadius = 1.0f;
    [SerializeField] private LayerMask handLayer;

    [Header("Physics & Bounds")]
    [SerializeField] private float boundsPushForce = 40f;
    [SerializeField] private HighlightController highlight;

    private Deck selectedDeck;
    private CardView selectedCardView;
    private HoverWhileDragged hoverComponent;

    public enum InputState { Idle, PressedObject, Dragging, ChoosingDeckForCard }
    private InputState state = InputState.Idle;
    public InputState CurrentState => state;

    private Vector2 pointerDownScreenPos;
    private bool didDrag;
    private GameObject pressedCandidate;
    private Rigidbody draggedRb;
    private DraggedMarker draggedMarker;
    private Collider tableCollider;

    [SerializeField] private float markerKeepTime = 0.15f;
    private Coroutine removeMarkerCoroutine;

    private Card pendingCard = null;
    private GameObject pendingCardGO = null;

    // --- REQUIRED CAMERA/UTILITY CHECKS ---
    public bool CanCameraRotate() {
        if (state == InputState.Dragging) return false;
        if (state == InputState.ChoosingDeckForCard) return false;
        if (state == InputState.PressedObject && pressedCandidate != null && pressedCandidate.CompareTag("MoveableObject")) return false;
        if (IsPointerOverUI()) return false;
        return true;
    }

    public bool IsPointerOverDraggable() {
        if (state == InputState.Dragging) return true;
        GameObject hit = RaycastWorldObject();
        return hit != null && hit.CompareTag("MoveableObject");
    }

    // --- INITIALIZATION ---
    void Start() {
        if (deckMenu != null) deckMenu.controller = this;
        if (cardMenu != null) cardMenu.controller = this;
        if (cancelDeckAddMenu != null) cancelDeckAddMenu.controller = this;

        GameObject floor = GameObject.FindGameObjectWithTag("Floor");
        if (floor != null) tableCollider = floor.GetComponent<Collider>();
    }

    void Update() {
        // THE MASTER KILLSWITCH: If Go Fish is running, this script goes to sleep.
        if (GoFishManager.Instance != null) return;

        HandlePointer();
    }

    void FixedUpdate() {
        // THE MASTER KILLSWITCH: Prevent any rogue dragging physics in Go Fish
        if (GoFishManager.Instance != null) return;

        if (state == InputState.Dragging && draggedRb != null) {
            ApplyDragVelocity();
            ApplySoftBounds();
        }
    }

    // --- INPUT HANDLING ---
    private void HandlePointer() {
        if (PointerDown()) {
            pointerDownScreenPos = PointerPosition();
            didDrag = false;

            if (IsPointerOverUI()) return;

            if (state == InputState.ChoosingDeckForCard) {
                HandleAddToDeckClick();
                return;
            }

            GameObject hit = RaycastWorldObject();
            if (hit == null) {
                ClearSelectionAndMenus();
                pressedCandidate = null;
                state = InputState.Idle;
            } else {
                pressedCandidate = hit;
                state = InputState.PressedObject;
            }
        }

        if (PointerHeld()) {
            if (state == InputState.PressedObject && !didDrag) {
                if (Vector2.Distance(pointerDownScreenPos, PointerPosition()) > dragThresholdPixels) {
                    didDrag = true;
                    BeginDrag();
                }
            }
        }

        if (PointerUp()) {
            if (state == InputState.ChoosingDeckForCard) return;

            if (state == InputState.Dragging) {
                EndDrag();
                state = InputState.Idle;
            } else if (state == InputState.PressedObject && !didDrag) {
                ConfirmClick();
                state = InputState.Idle;
            } else {
                state = InputState.Idle;
            }
        }
    }

    // --- INTERACTION LOGIC ---
    private void ConfirmClick() {
        if (pressedCandidate == null) return;
        SelectObject(pressedCandidate);
    }

    private void SelectObject(GameObject obj) {
        if (obj == null || !obj.CompareTag("MoveableObject")) return;

        ClearSelectionAndMenus();
        if (highlight != null) highlight.Show(obj);

        selectedDeck = obj.GetComponentInParent<Deck>();
        selectedCardView = obj.GetComponentInParent<CardView>();

        if (selectedDeck != null) deckMenu.Show(selectedDeck, PointerPosition());
        else if (selectedCardView != null) cardMenu.Show(selectedCardView, PointerPosition());
    }

    private void HandleAddToDeckClick() {
        Ray ray = Camera.main.ScreenPointToRay(PointerPosition());
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));

        bool success = false;
        foreach (var hit in hits) {
            Deck deck = hit.collider.GetComponentInParent<Deck>();

            if (deck != null && pendingCard != null) {
                deck.AddCard(pendingCard);
                if (pendingCardGO != null) Destroy(pendingCardGO);
                success = true;
                Debug.Log("<color=green>[ObjectSelect]</color> Card added to deck successfully.");
                break;
            }

            if (hit.collider.CompareTag("Floor")) break;
        }

        if (!success) {
            Debug.Log("<color=orange>[ObjectSelect]</color> Add to Deck cancelled: No Deck component found.");
        }

        MenuActionCompleted();
    }

    // --- MENU WRAPPERS ---
    public void OnCardMenuAddToDeckPressed() {
        if (selectedCardView == null) return;
        pendingCard = selectedCardView.GetCardData();
        pendingCardGO = selectedCardView.gameObject;
        ClearSelectionAndMenus();
        cancelDeckAddMenu.Show();
        state = InputState.ChoosingDeckForCard;
    }

    public void OnCardMenuAddToHandPressed() {
        if (selectedCardView == null || GameManager.Instance == null) return;

        PlayerHand myHand = GameManager.Instance.MyHand;
        if (myHand != null) {
            GameManager.Instance.RequestAddCardToSpecificHand(selectedCardView, myHand);
        }

        MenuActionCompleted();
    }

    public void onCancelDeckAddPressed() => MenuActionCompleted();

    public void MenuActionCompleted() {
        pendingCard = null;
        pendingCardGO = null;
        ClearSelectionAndMenus();
        state = InputState.Idle;
    }

    public void OnFocusOnHandButtonPressed() {
        if (GameManager.Instance == null) {
            Debug.LogError("<color=red>[Camera]</color> Failed: GameManager is missing!");
            return;
        }

        PlayerHand myHand = GameManager.Instance.MyHand;

        if (myHand == null) {
            Debug.LogError("<color=red>[Camera]</color> Failed: MyHand is NULL. The Client does not know its seat yet.");
            return;
        }

        if (myHand.cameraAnchor == null) {
            Debug.LogError($"<color=red>[Camera]</color> Failed: cameraAnchor is missing on {myHand.gameObject.name}! Please assign the child empty in the Inspector.");
            return;
        }

        if (CameraController.Instance == null) {
            Debug.LogError("<color=red>[Camera]</color> Failed: CameraController.Instance is missing from the scene!");
            return;
        }

        CameraController.Instance.FocusOnTransform(myHand.cameraAnchor);
        Debug.Log($"<color=cyan>[Camera]</color> Successfully focused on {myHand.gameObject.name}");
    }

    // --- DRAG & PHYSICS ---
    private void BeginDrag() {
        if (pressedCandidate == null) return;
        draggedRb = pressedCandidate.GetComponent<Rigidbody>();
        if (draggedRb == null) { state = InputState.Idle; return; }

        state = InputState.Dragging;

        CardView card = pressedCandidate.GetComponent<CardView>();
        if (card != null && GameManager.Instance != null) {
            GameManager.Instance.RequestRemoveCardFromHands(card);
        }

        hoverComponent = pressedCandidate.GetComponent<HoverWhileDragged>();
        if (hoverComponent != null) hoverComponent.BeginHover();

        draggedMarker = pressedCandidate.GetComponent<DraggedMarker>();
        if (draggedMarker == null) draggedMarker = pressedCandidate.AddComponent<DraggedMarker>();
        if (removeMarkerCoroutine != null) StopCoroutine(removeMarkerCoroutine);
    }

    private void ApplyDragVelocity() {
        Plane tablePlane = new Plane(Vector3.up, new Vector3(0, draggedRb.position.y, 0));
        Ray ray = Camera.main.ScreenPointToRay(PointerPosition());
        if (tablePlane.Raycast(ray, out float distance)) {
            Vector3 target = ray.GetPoint(distance);
            Vector3 toTarget = target - draggedRb.position;
            toTarget.y = 0f;
            Vector3 desired = toTarget * dragResponsiveness;
            if (desired.magnitude > maxDragSpeed) desired = desired.normalized * maxDragSpeed;
            Vector3 v = draggedRb.linearVelocity;
            v.x = desired.x; v.z = desired.z;
            draggedRb.linearVelocity = v;
        }
    }

    private void EndDrag() {
        if (hoverComponent != null) { hoverComponent.EndHover(); hoverComponent = null; }
        if (draggedRb != null) {
            CheckForHandDrop(draggedRb.gameObject);
            draggedRb.linearVelocity = Vector3.zero;
            draggedRb.angularVelocity = Vector3.zero;
        }
        if (draggedMarker != null) {
            removeMarkerCoroutine = StartCoroutine(RemoveDraggedMarkerAfterDelay(draggedMarker.gameObject));
            draggedMarker = null;
        }
        draggedRb = null;
        pressedCandidate = null;
    }

    private void CheckForHandDrop(GameObject obj) {
        CardView card = obj.GetComponent<CardView>();
        if (card == null) return;
        Collider[] hitColliders = Physics.OverlapSphere(obj.transform.position, handCheckRadius);

        foreach (var hit in hitColliders) {
            PlayerHand hand = hit.GetComponent<PlayerHand>();
            if (hand != null && GameManager.Instance != null) {
                GameManager.Instance.RequestAddCardToSpecificHand(card, hand);
                break;
            }
        }
    }

    private void ApplySoftBounds() {
        if (tableCollider == null || draggedRb == null) return;
        Bounds b = tableCollider.bounds;
        Vector3 p = draggedRb.position;
        Vector3 push = Vector3.zero;
        if (p.x < b.min.x) push.x = 1f; else if (p.x > b.max.x) push.x = -1f;
        if (p.z < b.min.z) push.z = 1f; else if (p.z > b.max.z) push.z = -1f;
        if (push != Vector3.zero) draggedRb.AddForce(push.normalized * boundsPushForce, ForceMode.Acceleration);
    }

    private IEnumerator RemoveDraggedMarkerAfterDelay(GameObject obj) {
        yield return new WaitForSeconds(markerKeepTime);
        if (obj != null) {
            var marker = obj.GetComponent<DraggedMarker>();
            if (marker) Destroy(marker);
        }
    }

    // --- SELECTION & UI HELPERS ---
    public void ClearSelectionAndMenus() {
        selectedDeck = null;
        selectedCardView = null;
        if (highlight != null) highlight.Hide();
        HideAllMenus();
    }

    private void HideAllMenus() {
        if (deckMenu) deckMenu.Hide();
        if (cardMenu) cardMenu.Hide();
        if (cancelDeckAddMenu) cancelDeckAddMenu.Hide();
    }

    public GameObject RaycastWorldObject() {
        Ray ray = Camera.main.ScreenPointToRay(PointerPosition());
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
        foreach (var hit in hits) {
            if (hit.collider.CompareTag("MoveableObject")) return hit.collider.gameObject;
            if (hit.collider.CompareTag("Floor")) return null;
        }
        return null;
    }

    public bool IsPointerOverUI() {
        if (EventSystem.current == null) return false;
        PointerEventData eventData = new PointerEventData(EventSystem.current) { position = PointerPosition() };
#if UNITY_ANDROID && !UNITY_EDITOR
        if (Input.touchCount > 0) eventData.pointerId = Input.GetTouch(0).fingerId;
        else return false;
#endif
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        foreach (var result in results) {
            if (result.gameObject.layer == 5) return true;
        }
        return false;
    }

    // --- TOUCH/MOUSE WRAPPERS ---
    Vector2 PointerPosition() {
#if UNITY_ANDROID && !UNITY_EDITOR
        return Input.touchCount > 0 ? Input.GetTouch(0).position : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    bool PointerDown() {
#if UNITY_ANDROID && !UNITY_EDITOR
        return Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    bool PointerHeld() {
#if UNITY_ANDROID && !UNITY_EDITOR
        return Input.touchCount > 0 && (Input.GetTouch(0).phase == TouchPhase.Moved || Input.GetTouch(0).phase == TouchPhase.Stationary);
#else
        return Input.GetMouseButton(0);
#endif
    }

    bool PointerUp() {
#if UNITY_ANDROID && !UNITY_EDITOR
        return Input.touchCount == 0 || Input.GetTouch(0).phase == TouchPhase.Ended || Input.GetTouch(0).phase == TouchPhase.Canceled;
#else
        return Input.GetMouseButtonUp(0);
#endif
    }
}