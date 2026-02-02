using UnityEngine;

public class CardMenu : MonoBehaviour {
    [HideInInspector] public ObjectSelect controller;

    private CardView selectedCard;

    void Awake() {
        gameObject.SetActive(false);
    }

    public void Show(CardView card, Vector3 screenPosition) {
        selectedCard = card;
        gameObject.SetActive(true);
        transform.position = screenPosition;
    }

    public void Hide() {
        gameObject.SetActive(false);
    }

    public bool GetActive() {
        return gameObject.activeSelf;
    }

    // Called by the AddToDeck UI button
    public void OnAddToDeckPressed() {
        if (controller != null)
        controller.OnCardMenuAddToDeckPressed();
        else {
            // fallback: do nothing
            Debug.LogWarning("CardMenu controller missing - cannot enter add-to-deck mode.");
        }

        // The menu hides; the controller will now be in ChoosingDeckForCard state.
        Hide();
    }

    public CardView GetSelectedCard() { return selectedCard; }
}
