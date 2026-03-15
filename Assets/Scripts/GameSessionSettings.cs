public enum GameMode { GoFish, Sandbox }

// "static" means this data lives in the background of the game forever, 
// surviving scene loads so any scene can ask for this information!
public static class GameSessionData {
    public static GameMode SelectedMode = GameMode.GoFish;
    public static int PlayerCount = 2;
    public static int DeckCount = 1;
    public static int ScoringMode = 1;

    // THE FIX: Save the name here so the Game Scene can read it!
    public static string PlayerName = "Player";
}