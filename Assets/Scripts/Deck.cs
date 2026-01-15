using System.Collections.Generic;
using UnityEngine;

public class Deck : MonoBehaviour {
    [SerializeField] private List<Card> cards = new List<Card>();

    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private Transform drawSpawnPoint;
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

    public Card DrawCard() {
        if (cards.Count == 0)
            return null;

        Card card = cards[0];
        cards.RemoveAt(0);
        --cardCount;

        SpawnCardObject(card);
        return card;
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

}
