using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Deck : MonoBehaviour {
    [SerializeField] public List<Card> cards = new List<Card>();

    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private Transform drawSpawnPoint;
    [SerializeField] private float dealSpeed = 0.15f;
    public int cardCount = 0;

    public List<Card> GetCards() { return cards; }

    void Awake() {
        // Initialize with settings from Lobby
        CreateStandardDeck(GoFishSettings.DeckCount);
        Shuffle();
    }

    public void InitializeWithCards(List<Card> initialCards) {
        if (cards == null) cards = new List<Card>();
        cards.Clear();
        cards.AddRange(initialCards);
        Debug.Log($"{gameObject.name} initialized with {cards.Count} cards.");
    }

    void CreateStandardDeck(int deckMultiplier) {
        cards.Clear();
        cardCount = 0;

        // Loop for the number of decks requested
        for (int d = 0; d < deckMultiplier; d++) {
            foreach (Suit suit in System.Enum.GetValues(typeof(Suit))) {
                foreach (Rank rank in System.Enum.GetValues(typeof(Rank))) {
                    cards.Add(new Card(suit, rank));
                    ++cardCount;
                }
            }
        }
        Debug.Log($"Created deck with {deckMultiplier} sets. Total: {cards.Count} cards.");
    }

    public Card DrawCard(PlayerHand targetHand) {
        if (targetHand == null) {
            Debug.LogError("No target hand provided to DrawCard!");
            return null;
        }

        if (cards == null || cards.Count == 0) {
            Debug.LogWarning("Deck is empty! Nothing to draw.");
            return null;
        }

        Card topCardData = cards[0];
        cards.RemoveAt(0);
        cardCount = cards.Count;

        GameObject newCardObj = Instantiate(cardPrefab, transform.position, Quaternion.identity);
        newCardObj.tag = "MoveableObject";

        CardView newCardView = newCardObj.GetComponent<CardView>();
        if (newCardView != null) {
            newCardView.SetCardData(topCardData);
        }

        targetHand.AddCard(newCardView);
        return topCardData;
    }

    public void DealCardToHand(PlayerHand targetHand) {
        if (targetHand == null) return;
        GameObject newCard = Instantiate(cardPrefab, transform.position, Quaternion.identity);
        CardView cardView = newCard.GetComponent<CardView>();
        targetHand.AddCard(cardView);
    }

    public void AddCard(Card card) {
        cards.Add(card);
        cardCount = cards.Count;
    }

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
                    DrawCard(seat);
                    yield return new WaitForSeconds(dealSpeed);
                }
            }
        }
    }

    public void RemoveTopCard() {
        if (cards == null || cards.Count == 0) return;

        Card topCardData = cards[0];
        cards.RemoveAt(0);
        cardCount = cards.Count;

        float offsetDistance = cardPrefab.gameObject.transform.localScale.x * 1.1f;
        Vector3 finalSpawnPos = transform.position + (Vector3.up * 0.2f);

        GameObject newCardGO = Instantiate(cardPrefab, finalSpawnPos, Quaternion.identity);
        CardView cv = newCardGO.GetComponent<CardView>();
        if (cv != null) cv.SetCardData(topCardData);

        if (newCardGO.TryGetComponent<Rigidbody>(out var rb)) {
            rb.isKinematic = false;
        }
    }
}