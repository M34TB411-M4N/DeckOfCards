using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class PlayerScoreData {
    public string playerName;
    public int score;
}

public class GameManager : MonoBehaviour {
    public static GameManager Instance;

    public List<PlayerScoreData> playerScores = new List<PlayerScoreData>();

    // Call this whenever someone earns points
    public void AddScore(int playerIndex, int amount) {
        if (playerIndex >= 0 && playerIndex < playerScores.Count) {
            playerScores[playerIndex].score += amount;
        }
    }

    [Header("Table Setup")]
    // Drag your 8 Seat GameObjects here in order (Seat 1 to Seat 8)
    public List<PlayerHand> allSeats;

    [Header("Session Data")]
    public int totalPlayers = 4; // We assume this is known at load
    public int myPlayerIndex = 0; // The index of the local user (0-7)

    // Helper to get the local player's hand quickly
    public PlayerHand MyHand {
        get {
            if (myPlayerIndex >= 0 && myPlayerIndex < allSeats.Count) {
                Debug.Log("got hand");
                return allSeats[myPlayerIndex];
            }
            return null;
        }
    }

    void Awake() {
        // Singleton Setup
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start() {
        //Temp data for testing
        playerScores.Add(new PlayerScoreData { playerName = "Local Player", score = 0 });
        playerScores.Add(new PlayerScoreData { playerName = "Opponent 1", score = 150 });
        //end temp data
        AssignSeats();
    }

    // This assigns active players to seats. 
    // For now, we just fill them sequentially (Player 0 -> Seat 0).
    public void AssignSeats() {
        for (int i = 0; i < allSeats.Count; i++) {
            // Check if this seat should be active based on player count
            bool isSeatActive = i < totalPlayers;

            allSeats[i].gameObject.SetActive(isSeatActive);

            if (isSeatActive) {
                // Determine if this is the "Local Player" (Me)
                bool isMe = (i == myPlayerIndex);

                // Optional: You can change the color of the seat or enable
                // specific UI here to show "This is you"
                if (isMe) {
                    Debug.Log($"Player assigned to Seat {i}");
                }
            }
        }
    }
}