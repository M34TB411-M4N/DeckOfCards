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
        if (currentDeck == null) return;
        currentDeck.DrawCard();
        // Tell the controller we completed a UI action -> clear selection & menus
        if (controller != null) controller.MenuActionCompleted();
    }

    public void OnFlipPressed() {
        if (currentDeck == null) return;
        currentDeck.transform.Rotate(0f, 0f, 180f, Space.Self);
        if (controller != null) controller.MenuActionCompleted();
    }
    //public void OnOtherButtonPressed() {
    //    if (controller != null) controller.MenuActionCompleted();
    //}
    public bool GetActive() { return isActive; }
}
