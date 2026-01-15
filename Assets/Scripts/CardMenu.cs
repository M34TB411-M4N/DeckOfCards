using UnityEngine;

public class CardMenu : MonoBehaviour {
    private CardView selectedCard;
    private bool waitingForDeck;

    void Awake() {
        gameObject.SetActive(false);
    }

    public void Show(CardView card, Vector3 screenPosition) {
        selectedCard = card;
        waitingForDeck = false;
        gameObject.SetActive(true);
        transform.position = screenPosition;
    }

    public void Hide() {
        gameObject.SetActive(false);
    }

    public bool GetActive() {
        return gameObject.activeSelf;
    }

    public bool IsWaitingForDeck() {
        return waitingForDeck;
    }

    public void OnAddToDeckPressed() {
        // Enter "next deck click consumes this card" mode
        waitingForDeck = true;
        Hide();
    }

    public void AddCardToDeck(Deck deck) {
        if (!waitingForDeck || selectedCard == null)
            return;

        deck.AddCard(selectedCard.GetCardData());

        Destroy(selectedCard.gameObject);

        selectedCard = null;
        waitingForDeck = false;
    }
}
