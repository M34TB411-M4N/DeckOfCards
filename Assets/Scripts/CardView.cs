using UnityEngine;

public class CardView : MonoBehaviour {
    private Card card;
    [SerializeField] private Rank rank;
    [SerializeField] private Suit suit;

    public void Initialize(Card card) {
        this.card = card;
        this.rank = card.rank;
        this.suit = card.suit;
        UpdateVisuals();
    }
    public Card GetCardData() {
        return card;
    }

    void UpdateVisuals() {
        // Set sprite, text, mesh, etc. based on card.suit and card.rank
        Debug.Log($"{card.rank} of {card.suit}");
    }

    public void OnClicked() {
        // Card-specific behavior
    }

    public void Flip() {
        transform.Rotate(0f, 0f, 180f, Space.Self);
    }
}
