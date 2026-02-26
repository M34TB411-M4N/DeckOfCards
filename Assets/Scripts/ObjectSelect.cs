using System.Collections;
using System.Collections.Generic;
using TMPro.Examples;
using UnityEngine;
using UnityEngine.EventSystems;

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
    private enum InputState { Idle, PressedObject, Dragging, ChoosingDeckForCard }
    private InputState state = InputState.Idle;

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

    // ==========================================
    // CAMERA SYSTEM INTEGRATION
    // ==========================================

    /// <summary>
    /// Call this from your Camera Controller script!
    /// If this returns true, the camera is allowed to rotate.
    /// </summary>
    public bool CanCameraRotate() {
        // 1. If we are currently dragging a card, don't rotate.
        if (state == InputState.Dragging) return false;

        // 2. If the pointer is over ACTUAL UI (Layer 5), don't rotate.
        if (IsPointerOverUI()) return false;

        // 3. If we just clicked on a card/deck but haven't dragged it yet, block rotation
        if (state == InputState.PressedObject && pressedCandidate != null && pressedCandidate.CompareTag("MoveableObject")) return false;

        // Otherwise, we are clicking the floor or empty space: Rotation is OK!
        return true;
    }

    void Start() {
        Debug.Log("<color=cyan>[ObjectSelect]</color> System Online. Unity 6 Version: " + Application.unityVersion);
        if (deckMenu != null) deckMenu.controller = this;
        if (cardMenu != null) cardMenu.controller = this;
        if (cancelDeckAddMenu != null) cancelDeckAddMenu.controller = this;

        GameObject floor = GameObject.FindGameObjectWithTag("Floor");
        if (floor != null) {
            tableCollider = floor.GetComponent<Collider>();
        }
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

    private void HandlePointer() {
        if (PointerDown()) {
            pointerDownScreenPos = PointerPosition();
            didDrag = false;

            // If clicking actual UI, stop here.
            if (IsPointerOverUI()) return;

            GameObject hit = RaycastWorldObject();
            if (hit == null) {
                // Clicked Floor or Space
                HideAllMenus();
                ClearSelection();
                pressedCandidate = null;
                state = InputState.Idle;
            } else {
                // Clicked a Card/Deck
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
            if (state == InputState.ChoosingDeckForCard) {
                HandleAddToDeckClick();
            } else if (state == InputState.Dragging) {
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

    private void BeginDrag() {
        if (pressedCandidate == null) return;

        draggedRb = pressedCandidate.GetComponent<Rigidbody>();
        if (draggedRb == null) {
            state = InputState.Idle;
            return;
        }

        CardView card = pressedCandidate.GetComponent<CardView>();
        if (card != null) {
            PlayerHand[] allHands = FindObjectsByType<PlayerHand>(FindObjectsSortMode.None);
            foreach (var hand in allHands) {
                if (hand.cardsInHand.Contains(card)) {
                    hand.RemoveCard(card);
                    break;
                }
            }
        }

        hoverComponent = pressedCandidate.GetComponent<HoverWhileDragged>();
        if (hoverComponent != null) hoverComponent.BeginHover();

        draggedMarker = pressedCandidate.GetComponent<DraggedMarker>();
        if (draggedMarker == null) draggedMarker = pressedCandidate.AddComponent<DraggedMarker>();

        if (removeMarkerCoroutine != null) StopCoroutine(removeMarkerCoroutine);

        state = InputState.Dragging;
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
            v.x = desired.x;
            v.z = desired.z;
            draggedRb.linearVelocity = v;
        }
    }

    private void EndDrag() {
        if (hoverComponent != null) {
            hoverComponent.EndHover();
            hoverComponent = null;
        }

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

    private void ConfirmClick() {
        if (pressedCandidate == null) return;
        SelectObject(pressedCandidate);
    }

    private void SelectObject(GameObject obj) {
        if (obj == null || !obj.CompareTag("MoveableObject")) return;

        ClearSelection();
        if (highlight != null) highlight.Show(obj);

        selectedDeck = obj.GetComponentInParent<Deck>();
        selectedCardView = obj.GetComponentInParent<CardView>();

        if (selectedDeck != null) {
            deckMenu.Show(selectedDeck, PointerPosition());
        } else if (selectedCardView != null) {
            cardMenu.Show(selectedCardView, PointerPosition());
        }
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
#else
        eventData.pointerId = -1;
#endif

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var result in results) {
            // ONLY block if it hits Layer 5 (UI). 
            // This prevents the "Floor" (Layer 8) from blocking the camera.
            if (result.gameObject.layer == 5) {
                return true;
            }
        }
        return false;
    }

    // --- HELPER FUNCTIONS ---

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
            PlayerHand[] allHands = FindObjectsByType<PlayerHand>(FindObjectsSortMode.None);
            foreach (var hand in allHands) {
                if (hand.cardsInHand.Contains(selectedCardView)) {
                    hand.RemoveCard(selectedCardView);
                    break;
                }
            }
            myHand.AddCard(selectedCardView);
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

    private void HandleAddToDeckClick() {
        GameObject hitObj = RaycastWorldObject();
        if (hitObj != null) {
            Deck deck = hitObj.GetComponentInParent<Deck>();
            if (deck != null && pendingCard != null) {
                deck.AddCard(pendingCard);
                if (pendingCardGO != null) Destroy(pendingCardGO);
            }
        }
        MenuActionCompleted();
    }

    public void OnFocusOnHandButtonPressed() {
        if (GameManager.Instance?.MyHand?.cameraAnchor != null) {
            CameraController.Instance?.FocusOnTransform(GameManager.Instance.MyHand.cameraAnchor);
        }
    }

    public bool IsPointerOverDraggable() {
        if (state == InputState.Dragging) return true;
        GameObject hit = RaycastWorldObject();
        return hit != null && hit.CompareTag("MoveableObject");
    }

    private void CheckForHandDrop(GameObject obj) {
        CardView card = obj.GetComponent<CardView>();
        if (card == null) return;
        Collider[] hitColliders = Physics.OverlapSphere(obj.transform.position, handCheckRadius);
        foreach (var hit in hitColliders) {
            PlayerHand hand = hit.GetComponent<PlayerHand>();
            if (hand != null) {
                hand.AddCard(card);
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

    private void ClearSelection() {
        selectedDeck = null;
        selectedCardView = null;
        if (highlight != null) highlight.Hide();
    }

    private void ClearSelectionAndMenus() {
        ClearSelection();
        HideAllMenus();
    }

    private void HideAllMenus() {
        if (deckMenu) deckMenu.Hide();
        if (cardMenu) cardMenu.Hide();
        if (cancelDeckAddMenu) cancelDeckAddMenu.Hide();
    }

    // --- INPUT WRAPPERS ---
    Vector2 PointerPosition() =>
#if UNITY_ANDROID && !UNITY_EDITOR
        Input.touchCount > 0 ? Input.GetTouch(0).position : Vector2.zero;
#else
        Input.mousePosition;
#endif

    bool PointerDown() =>
#if UNITY_ANDROID && !UNITY_EDITOR
        Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;
#else
        Input.GetMouseButtonDown(0);
#endif

    bool PointerHeld() =>
#if UNITY_ANDROID && !UNITY_EDITOR
        Input.touchCount > 0 && (Input.GetTouch(0).phase == TouchPhase.Moved || Input.GetTouch(0).phase == TouchPhase.Stationary);
#else
        Input.GetMouseButton(0);
#endif

    bool PointerUp() =>
#if UNITY_ANDROID && !UNITY_EDITOR
        Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Ended;
#else
        Input.GetMouseButtonUp(0);
#endif
}