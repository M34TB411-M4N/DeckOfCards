public static class GoFishSettings {
    public static int PlayerCount = 4;
    public static int DeckCount = 1;
    public enum ScoringMode { Books, Pairs }
    public static ScoringMode CurrentMode = ScoringMode.Books;

    // Helper to get the required cards for a "match"
    public static int GetMatchCount() {
        return (CurrentMode == ScoringMode.Books) ? 4 : 2;
    }
}