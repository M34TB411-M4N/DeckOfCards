using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour {
    public static GameManager Instance;

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