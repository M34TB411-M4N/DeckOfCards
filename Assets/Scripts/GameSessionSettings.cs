public enum GameMode { Sandbox, Cribbage, GoFish }

public static class GameSessionData {
    public static GameMode SelectedMode = GameMode.GoFish;
    public static int PlayerCount = 2;
    public static int DeckCount = 1;
    public static int ScoringMode = 0;
    public static string PlayerName = "Player";
    public static string RelayJoinCode = "";
}