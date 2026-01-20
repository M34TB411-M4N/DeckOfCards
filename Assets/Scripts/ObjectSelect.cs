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

    private enum InputState {
        Idle,
        ObjectSelected,
        DeckMenuOpen,
        CardMenuOpen,
        ChoosingDeckForCard
    }

    private InputState state = InputState.Idle;

    void Start() {
        if (deckMenu != null) deckMenu.controller = this;
        if (cardMenu != null) cardMenu.controller = this;
        if (cancelDeckAddMenu != null) cancelDeckAddMenu.controller = this;

        HideAllMenus();
    }

    void Update() {
        if (!Input.GetMouseButtonDown(0))
            return;


        // Ignore UI clicks entirely
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        // ALWAYS clear previous highlight on any click
        ClearSelectionHighlight();
        
        Ray ray = GetRayOnMousePosition();
        if (!Physics.Raycast(ray, out RaycastHit hit)) {
            HandleClickOnEmptySpace();
            return;
        }

        GameObject clickedObject = hit.transform.gameObject;

        // Special mode: next deck click consumes selected card
        if (state == InputState.ChoosingDeckForCard) {
            TryConsumeCardToDeck(clickedObject);
            return;
        }

        HandleNormalClick(clickedObject);
    }

    // -----------------------
    // Click handlers
    // -----------------------

    private void HandleClickOnEmptySpace() {
        if (state == InputState.ChoosingDeckForCard) {
            return;
        }

        HideAllMenus();
        ClearLogicalSelection();
        state = InputState.Idle;
    }

    private void HandleNormalClick(GameObject clickedObject) {
        HideAllMenus();
        ClearLogicalSelection();

        // Select object visually
        SelectObject(clickedObject);

        // Deck selection
        Deck deck = clickedObject.GetComponent<Deck>();
        if (deck != null) {
            selectedDeck = deck;
            state = InputState.DeckMenuOpen;

            Vector3 pos = Input.mousePosition + new Vector3(100, -100);
            deckMenu.Show(deck, pos);
            return;
        }

        // Card selection
        CardView card = clickedObject.GetComponent<CardView>();
        if (card != null) {
            selectedCardView = card;
            state = InputState.CardMenuOpen;

            Vector3 pos = Input.mousePosition + new Vector3(100, -100);
            cardMenu.Show(card, pos);
            return;
        }

        // Some other selectable object
        state = InputState.ObjectSelected;
    }

    private void TryConsumeCardToDeck(GameObject clickedObject) {
        Deck deck = clickedObject.GetComponent<Deck>();
        if (deck == null || selectedCardView == null) {
            Debug.Log("Click a deck to add the card, or click empty space to cancel.");
            return;
        }

        deck.AddCard(selectedCardView.GetCardData());
        Destroy(selectedCardView.gameObject);

        selectedCardView = null;
        HideAllMenus();
        state = InputState.Idle;
    }

    // -----------------------
    // Selection helpers
    // -----------------------

    private void SelectObject(GameObject obj) {
        selectedObject = obj;

        MeshRenderer renderer = selectedObject.GetComponent<MeshRenderer>();
        if (renderer != null) {
            prevMat = renderer.material;
            renderer.material = selectedMat;
        }
    }

    private void ClearSelectionHighlight() {
        if (selectedObject == null)
            return;

        MeshRenderer renderer = selectedObject.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.material = prevMat;

        selectedObject = null;
    }

    private void ClearLogicalSelection() {
        selectedDeck = null;
        selectedCardView = null;
    }

    private void HideAllMenus() {
        if (deckMenu != null && deckMenu.GetActive())
            deckMenu.Hide();

        if (cardMenu != null && cardMenu.GetActive())
            cardMenu.Hide();

        if (cancelDeckAddMenu != null && cancelDeckAddMenu.GetActive())
            cancelDeckAddMenu.Hide();
    }


    // -----------------------
    // Menu callbacks
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
    // Utility
    // -----------------------

    private Ray GetRayOnMousePosition() {
        return Camera.main.ScreenPointToRay(Input.mousePosition);
    }
}
