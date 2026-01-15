public enum Suit { Hearts, Diamonds, Clubs, Spades }
public enum Rank {
    Ace = 1, Two, Three, Four, Five, Six, Seven,
    Eight, Nine, Ten, Jack, Queen, King
}
[System.Serializable]
public class Card {
    public Suit suit;
    public Rank rank;

    public Card(Suit suit, Rank rank) {
        this.suit = suit;
        this.rank = rank;
    }
}
