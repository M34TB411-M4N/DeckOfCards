using UnityEngine;

public class CardView : MonoBehaviour {
    [Header("Data")]
    [SerializeField] private Card card;
    [SerializeField] private Rank rank;
    [SerializeField] private Suit suit;

    [Header("Visuals")]
    [Tooltip("Drag the Card_Face Quad's SpriteRenderer here")]
    public SpriteRenderer faceRenderer;

    [Tooltip("Drag your CardFacePlaceholder sprite here to hide opponent cards!")]
    public Sprite hiddenFaceSprite;

    [Header("Prefabs")]
    [SerializeField] private GameObject pilePrefab;

    private bool currentlyHidden = false;

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

    void Update() {
        // Only run the anti-cheat monitor if Go Fish is actively playing
        if (GoFishManager.Instance == null || GameManager.Instance == null) return;

        // Default to hidden to prevent deck peeking or flying card peeking
        bool shouldBeHidden = true;

        // If the card is physically inside OUR hand list, we are allowed to see it!
        if (GameManager.Instance.MyHand != null && GameManager.Instance.MyHand.cardsInHand.Contains(this)) {
            shouldBeHidden = false;
        }

        // If the visibility state changed this exact frame, trigger the sprite swap
        if (shouldBeHidden != currentlyHidden) {
            currentlyHidden = shouldBeHidden;
            UpdateVisuals();
        }
    }

    void UpdateVisuals() {
        if (faceRenderer == null) {
            Debug.LogWarning($"CardView: No face renderer assigned on {gameObject.name}!");
            return;
        }

        if (card == null) return;

        if (currentlyHidden) {
            // Apply the Anti-Cheat Placeholder Sprite
            if (hiddenFaceSprite != null) {
                faceRenderer.sprite = hiddenFaceSprite;
            } else {
                // Fallback: Try to load it dynamically if it wasn't assigned in the inspector
                Sprite loadedPlaceholder = Resources.Load<Sprite>("CardFacePlaceholder");
                if (loadedPlaceholder != null) faceRenderer.sprite = loadedPlaceholder;
                else Debug.LogWarning("CardView: Assign a hiddenFaceSprite in the Inspector, or place 'CardFacePlaceholder' in a Resources folder!");
            }
        } else {
            // Apply the True Face Sprite
            string resourceName = $"CardFaces/{card.suit}_{card.rank}";
            Sprite loadedFace = Resources.Load<Sprite>(resourceName);

            if (loadedFace != null) {
                faceRenderer.sprite = loadedFace;
            } else {
                Debug.LogError($"CardView: Could not find image at Resources/{resourceName}");
            }
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