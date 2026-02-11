using UnityEngine;

public class CardView : MonoBehaviour {
    private Card card;
    [SerializeField] private Rank rank;
    [SerializeField] private Suit suit;

    [SerializeField] private GameObject pilePrefab;
    public void Initialize(Card card) {
        this.card = card;
        this.rank = card.rank;
        this.suit = card.suit;
        UpdateVisuals();
    }
    public Card GetCardData() {
        return card;
    }

    public void SetCardData(Card card) { this.card = card; }

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

    public void ConvertToPile() {
        // 1. Capture the data and position before this object is destroyed
        Card cardData = GetCardData();
        Vector3 spawnPos = transform.position;
        Quaternion spawnRot = transform.rotation;

        // 2. Remove from any hands it might be in
        PlayerHand[] allHands = Object.FindObjectsByType<PlayerHand>(FindObjectsSortMode.None);
        foreach (var hand in allHands) {
            if (hand.cardsInHand.Contains(this)) {
                hand.RemoveCard(this);
                break;
            }
        }

        // 3. Spawn the Pile
        GameObject newPileGO = Object.Instantiate(pilePrefab, spawnPos, spawnRot);
        newPileGO.tag = "MoveableObject";

        // 4. Initialize the Pile
        Pile newPile = newPileGO.GetComponent<Pile>();
        if (newPile != null) {
            var cardList = new System.Collections.Generic.List<Card> { cardData };
            newPile.InitializeWithCards(cardList);
        }

        Object.Destroy(gameObject);
    }
}
