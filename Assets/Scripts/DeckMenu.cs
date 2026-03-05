using TMPro;
using UnityEngine;

public class DeckMenu : MonoBehaviour {
    [HideInInspector] public ObjectSelect controller;

    private Deck currentDeck;
    private bool isActive;

    [SerializeField] private TMP_InputField dealCountInput;

    void Awake() {
        Hide();
    }

    public bool GetActive() { return isActive; }

    public void Show(Deck deck, Vector3 screenPosition) {
        isActive = true;
        currentDeck = deck;
        gameObject.SetActive(true);
        gameObject.transform.position = screenPosition;

        if (dealCountInput != null) dealCountInput.text = "7";
    }

    public void Hide() {
        isActive = false;
        gameObject.SetActive(false);
        currentDeck = null;
    }

    public void OnDrawCardPressed() {
        if (currentDeck == null) return;

        if (GameManager.Instance != null && GameManager.Instance.MyHand != null) {
            // Uses the new network-safe request
            currentDeck.RequestDrawCard();
        } else {
            Debug.LogError("DeckMenu: Cannot draw because MyHand is null!");
        }

        if (controller != null) controller.MenuActionCompleted();
    }

    public void OnFlipPressed() {
        if (currentDeck == null) return;
        currentDeck.Flip();
        if (controller != null) controller.MenuActionCompleted();
    }

    public void OnDealButtonPressed() {
        if (currentDeck == null) return;

        int count = 7;
        if (dealCountInput != null && int.TryParse(dealCountInput.text, out int result)) {
            count = result;
        }

        currentDeck.StartDealing(count);
        if (controller != null) controller.MenuActionCompleted();
    }

    public void OnRemoveTopCardButtonPressed() {
        if (currentDeck == null) return;
        currentDeck.RemoveTopCard();
        if (controller != null) controller.MenuActionCompleted();
    }

    public void OnShufflePressed() {
        if (currentDeck == null) return;
        currentDeck.Shuffle();
        if (controller != null) controller.MenuActionCompleted();
    }
}