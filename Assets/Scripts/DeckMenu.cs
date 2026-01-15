using UnityEngine;

public class DeckMenu : MonoBehaviour {

    private Deck currentDeck;
    private bool isActive;

    void Awake() {
        Hide();
    }

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

    public void OnDrawCardPressed() {
        if (currentDeck == null)
            return;

        currentDeck.DrawCard();
        Hide();
    }
    public bool GetActive() { return isActive; }

}
