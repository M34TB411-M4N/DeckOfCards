using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Deck : MonoBehaviour {
    [SerializeField] private List<Card> cards = new List<Card>();

    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private Transform drawSpawnPoint;
    [SerializeField] private float dealSpeed = 0.15f; 
    public int cardCount = 0;

    void Awake() {
        CreateStandardDeck();
        Shuffle();
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
        if (GameManager.Instance == null || GameManager.Instance.MyHand == null) {
            Debug.LogError("No Local Player Hand found!");
            return;
        }

        GameObject newCardObj = Instantiate(cardPrefab, transform.position, Quaternion.identity);
        CardView newCardView = newCardObj.GetComponent<CardView>();

        // Reset the card's data/state if needed
        // newCardView.SetData(...); 

        GameManager.Instance.MyHand.AddCard(newCardView);
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
    void SpawnCardObject(Card card) {
        GameObject cardGO = Instantiate(cardPrefab, drawSpawnPoint.position, Quaternion.identity);
        CardView view = cardGO.GetComponent<CardView>();
        view.Initialize(card);
    }

    void Shuffle() {
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
}
