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
        if (controller != null) controller.OnCardMenuAddToDeckPressed();
    }

    //public void OnInspectPressed() {
    //    if (selectedCard == null) return;
    //    // do inspect work...
    //    if (controller != null) controller.MenuActionCompleted();
    //}
    public void OnAddToHandButtonPressed() {
        if (controller != null) {
            controller.OnCardMenuAddToHandPressed();
        }
    }

    public void OnFlipButtonPressed() {
        if (selectedCard != null) selectedCard.Flip();
        if (controller != null) controller.MenuActionCompleted();
    }
    public void OnCreatePileButtonPressed() {
        if (selectedCard != null) selectedCard.ConvertToPile();
        if (controller != null) controller.MenuActionCompleted();
    }
    public CardView GetSelectedCard() { return selectedCard; }
}
