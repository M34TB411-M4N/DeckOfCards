using UnityEngine;

public class CardView : MonoBehaviour {
    [Header("Data")]
    [SerializeField] private Card card;
    [SerializeField] private Rank rank;
    [SerializeField] private Suit suit;

    [Header("Visuals")]
    [Tooltip("Drag the Card_Face Quad's SpriteRenderer here")]
    public SpriteRenderer faceRenderer;

    [Header("Prefabs")]
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

    public void SetCardData(Card card) {
        this.card = card;
        this.rank = card.rank;
        this.suit = card.suit;
        UpdateVisuals();
    }

    void UpdateVisuals() {
        if (faceRenderer == null) {
            Debug.LogWarning($"CardView: No face renderer assigned on {gameObject.name}!");
            return;
        }

        if (card == null) return;

        // Construct the string name based on the data to match your Resources folder files
        string resourceName = $"Cards/{card.suit}_{card.rank}";

        // Load the sprite from the Resources folder
        Sprite loadedFace = Resources.Load<Sprite>(resourceName);

        if (loadedFace != null) {
            faceRenderer.sprite = loadedFace;
        } else {
            Debug.LogError($"CardView: Could not find image at Resources/{resourceName}");
        }
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