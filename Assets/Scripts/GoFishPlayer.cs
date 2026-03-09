using UnityEngine;
using System.Collections.Generic;

public class GoFishPlayer : MonoBehaviour {
    public int seatIndex;
    public string playerName;
    private PlayerHand myHand;

    void Awake() {
        myHand = GetComponent<PlayerHand>();
    }

    // Dynamically reads the physical cards currently in the PlayerHand
    public List<Card> GetLogicalHand() {
        List<Card> cards = new List<Card>();
        if (myHand == null) return cards;

        foreach (var cv in myHand.cardsInHand) {
            if (cv != null && cv.GetCardData() != null) {
                cards.Add(cv.GetCardData());
            }
        }
        return cards;
    }

    public bool HasRank(Rank rank) {
        return GetLogicalHand().Exists(c => c.rank == rank);
    }

    public List<Card> GetCardsOfRank(Rank rank) {
        return GetLogicalHand().FindAll(c => c.rank == rank);
    }
}