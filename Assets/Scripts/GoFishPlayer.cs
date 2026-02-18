using UnityEngine;
using System.Collections.Generic;

public class GoFishPlayer : MonoBehaviour {
    public int seatIndex;
    public string playerName;

    // The logical data of what cards this player owns
    public List<Card> logicalHand = new List<Card>();

    public void AddCard(Card card) {
        logicalHand.Add(card);
    }

    public void RemoveCard(Card card) {
        // Find a card with matching Suit and Rank to remove from the logical list
        Card target = logicalHand.Find(c => c.suit == card.suit && c.rank == card.rank);
        if (target != null) {
            logicalHand.Remove(target);
        }
    }

    public bool HasRank(Rank rank) {
        return logicalHand.Exists(c => c.rank == rank);
    }

    public List<Card> GetCardsOfRank(Rank rank) {
        return logicalHand.FindAll(c => c.rank == rank);
    }
}