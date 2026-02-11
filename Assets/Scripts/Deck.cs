using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Deck : MonoBehaviour {
    [SerializeField] private List<Card> cards = new List<Card>();

    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private Transform drawSpawnPoint;
    [SerializeField] private float dealSpeed = 0.15f; 
    public int cardCount = 0;

    public List<Card> GetCards() {  return cards; }
    void Awake() {
        CreateStandardDeck();
        Shuffle();
    }

    public void InitializeWithCards(List<Card> initialCards) {
        // Clear any existing cards just in case
        if (cards == null) cards = new System.Collections.Generic.List<Card>();
        cards.Clear();

        // Add the new cards
        cards.AddRange(initialCards);

        Debug.Log($"{gameObject.name} initialized with {cards.Count} cards.");
    }

    void CreateStandardDeck() {
        cards.Clear();

        foreach (Suit suit in System.Enum.GetValues(typeof(Suit))) {
            foreach (Rank rank in System.Enum.GetValues(typeof(Rank))) {
                cards.Add(new Card(suit, rank));
                ++cardCount;
            }
        }
    }

    public void DrawCard() {
        // 1. Safety Check: Is there a hand to send it to?
        if (GameManager.Instance == null || GameManager.Instance.MyHand == null) {
            Debug.LogError("No Local Player Hand found!");
            return;
        }

        // 2. Data Check: Is the deck empty?
        if (cards == null || cards.Count == 0) {
            Debug.LogWarning("Deck is empty! Nothing to draw.");
            return;
        }

        // 3. Extract the top card data and REMOVE it from the list
        Card topCardData = cards[0];
        cards.RemoveAt(0);

        // 4. Instantiate the card at the deck's position
        GameObject newCardObj = Instantiate(cardPrefab, transform.position, Quaternion.identity);
        newCardObj.tag = "MoveableObject"; // Ensure it's selectable later if dropped

        // 5. Assign the data to the CardView
        CardView newCardView = newCardObj.GetComponent<CardView>();
        if (newCardView != null) {
            newCardView.SetCardData(topCardData);
        }

        // 6. Add it to the hand (This handles the movement/physics/parenting)
        GameManager.Instance.MyHand.AddCard(newCardView);

        Debug.Log($"Card drawn. {cards.Count} remaining in {gameObject.name}.");
    }
    public void DealCardToHand(PlayerHand targetHand) {
        if (targetHand == null) return;

        // Instantiate the card at the deck's position
        // (Replace 'cardPrefab' with your actual variable name for the card object)
        GameObject newCard = Instantiate(cardPrefab, transform.position, Quaternion.identity);

        // Get the CardView
        CardView cardView = newCard.GetComponent<CardView>();

        // Send it to the specific hand requested
        targetHand.AddCard(cardView);
    }

    public void AddCard(Card card) {
        cards.Add((Card)card);
        ++cardCount;
    }
    //void SpawnCardObject(Card card) {
    //    GameObject cardGO = Instantiate(cardPrefab, drawSpawnPoint.position, Quaternion.identity);
    //    CardView view = cardGO.GetComponent<CardView>();
    //    view.Initialize(card);
    //}

    public void Shuffle() {
        for (int i = 0; i < cards.Count; i++) {
            int rand = Random.Range(i, cards.Count);
            (cards[i], cards[rand]) = (cards[rand], cards[i]);
        }
    }

    public void Flip() {
        transform.Rotate(0f, 0f, 180f, Space.Self);
    }

    public void StartDealing(int cardsPerPlayer) {
        StartCoroutine(DealRoutine(cardsPerPlayer));
    }

    private IEnumerator DealRoutine(int count) {
        if (GameManager.Instance == null) yield break;

        for (int i = 0; i < count; i++) {
            foreach (PlayerHand seat in GameManager.Instance.allSeats) {
                if (seat != null && seat.gameObject.activeInHierarchy) {
                    // Logic to spawn and send to hand
                    GameObject newCard = Instantiate(cardPrefab, transform.position, Quaternion.identity);
                    CardView cv = newCard.GetComponent<CardView>();
                    seat.AddCard(cv);

                    yield return new WaitForSeconds(dealSpeed);
                }
            }
        }
    }

    public void RemoveTopCard() {
        if (cards == null || cards.Count == 0) {
            Debug.LogWarning("Deck is empty!");
            return;
        }

        // 1. Get and remove data
        Card topCardData = cards[0];
        cards.RemoveAt(0);

        // 2. Find a valid spawn position on the table
        float offsetDistance = cardPrefab.gameObject.transform.localScale.x * 1.1f;
        Vector3[] directions = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        Vector3 finalSpawnPos = transform.position + (Vector3.up * 0.2f); // Default to on top if all else fails

        foreach (Vector3 dir in directions) {
            Vector3 potentialPos = transform.position + (dir * offsetDistance);

            // Raycast downward from above the potential position to find the table
            // We look for the "Table" or "Floor" tag we set up in Start()
            if (Physics.Raycast(potentialPos + Vector3.up, Vector3.down, out RaycastHit hit, 2f)) {
                if (hit.collider.CompareTag("Floor")) {
                    // Found the table! Set the spawn position to the hit point + a tiny bit of height
                    finalSpawnPos = hit.point + (Vector3.up * 0.05f);
                    break;
                }
            }
        }

        // 3. Spawn the card flat on its back
        // Quaternion.identity results in (0, 0, 0) rotation
        GameObject newCardGO = Instantiate(cardPrefab, finalSpawnPos, Quaternion.identity);
        newCardGO.tag = "MoveableObject";

        // 4. Setup CardView
        CardView cv = newCardGO.GetComponent<CardView>();
        if (cv != null) {
            cv.SetCardData(topCardData);
        }

        // 5. Physics Polish
        if (newCardGO.TryGetComponent<Rigidbody>(out var rb)) {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero; // Keep it still so it stays flat
            rb.angularVelocity = Vector3.zero;
        }
    }
}
