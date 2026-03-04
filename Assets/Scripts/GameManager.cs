using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

[System.Serializable]
public class PlayerScoreData {
    public string playerName;
    public int score;
}

public class GameManager : NetworkBehaviour {
    public static GameManager Instance;

    [Header("Table Setup")]
    public List<PlayerHand> allSeats;

    // Sync the active player count to all clients automatically
    public NetworkVariable<int> netPlayerCount = new NetworkVariable<int>(0);

    public List<PlayerScoreData> playerScores = new List<PlayerScoreData>();
    public int totalPlayers = 4;
    public int myPlayerIndex = -1;

    public ulong MyClientId => NetworkManager.Singleton.LocalClientId;

    public PlayerHand MyHand {
        get {
            if (myPlayerIndex >= 0 && myPlayerIndex < allSeats.Count)
                return allSeats[myPlayerIndex];
            return null;
        }
    }

    void Awake() {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn() {
        if (IsServer) {
            AssignSeats();
        }

        // Listen for the server changing the player count, and update visual seats
        netPlayerCount.OnValueChanged += (oldVal, newVal) => UpdateSeatVisibility(newVal);
        UpdateSeatVisibility(netPlayerCount.Value);
    }

    public void AssignSeats() {
        if (!IsServer) return;

        int seatIndex = 0;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList) {
            if (seatIndex < allSeats.Count) {
                PlayerHand seat = allSeats[seatIndex];

                if (seat == null) {
                    Debug.LogError($"<color=red>[GameManager]</color> Seat at index {seatIndex} is NULL! Check your 'allSeats' list in the Inspector.");
                    continue;
                }

                seat.gameObject.SetActive(true);

                NetworkObject seatNetObj = seat.GetComponent<NetworkObject>();

                if (seatNetObj == null) {
                    Debug.LogError($"<color=red>[GameManager]</color> Seat '{seat.gameObject.name}' is missing a NetworkObject component! Please add one in the Inspector.");
                    continue; // Skip this broken seat so the rest of the game doesn't crash
                }

                // Assign ownership so the specific client can control their hand
                if (!seatNetObj.IsSpawned) {
                    seatNetObj.SpawnWithOwnership(client.ClientId);
                } else {
                    seatNetObj.ChangeOwnership(client.ClientId);
                }

                seatIndex++;
            }
        }

        // Deactivate unused seats
        for (int i = seatIndex; i < allSeats.Count; i++) {
            if (allSeats[i] != null) {
                allSeats[i].gameObject.SetActive(false);
            }
        }

        netPlayerCount.Value = seatIndex;
        totalPlayers = seatIndex;
    }

    private void UpdateSeatVisibility(int count) {
        for (int i = 0; i < allSeats.Count; i++) {
            allSeats[i].gameObject.SetActive(i < count);
        }
    }

    public void AddScore(int playerIndex, int amount) {
        if (playerIndex >= 0 && playerIndex < playerScores.Count) {
            playerScores[playerIndex].score += amount;
        }
    }
}