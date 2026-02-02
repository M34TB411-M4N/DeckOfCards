using UnityEngine;

public class DeckMenu : MonoBehaviour {
    [HideInInspector] public ObjectSelect controller;

    private Deck currentDeck;
    private bool isActive;

    void Awake() {
        Hide();
    }

    // Show a menu for a particular deck (keeps a reference for display, but actions go to controller)
    public void Show(Deck deck, Vector3 screenPosition) {
        isActive = true;
        currentDeck = deck;
        gameObject.SetActive(true);
        gameObject.transform.position = screenPosition;
    }

    public void Hide() {
        isActive = false;
        gameObject.SetActive(false);
        currentDeck = null;
    }

    // Button callback wired in inspector (Draw)
    // The menu delegates the action to the controller (state machine)
    public void OnDrawCardPressed() {
        if (controller != null) 
            controller.OnDeckMenuDrawPressed();
        else {
            // fallback behavior (not recommended)
            if (currentDeck != null)
                currentDeck.DrawCard();

            Hide();
        }
    }

    public bool GetActive() { return isActive; }
}
