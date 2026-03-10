using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using System;

[System.Serializable]
public class PlayerScoreData {
    public string playerName;
    public int score;
}

public class GameManager : NetworkBehaviour {
    public static GameManager Instance;

    [Header("Table Setup")]
    public List<PlayerHand> allSeats;

    public NetworkVariable<int> netPlayerCount = new NetworkVariable<int>(0);

    public List<PlayerScoreData> playerScores = new List<PlayerScoreData>();
    public int totalPlayers = 4;
    public int myPlayerIndex = -1;

    // GLOBAL EVENT FOR THE UI TO LISTEN TO
    public event Action OnScoresUpdated;

    public ulong MyClientId {
        get {
            if (NetworkManager.Singleton != null) return NetworkManager.Singleton.LocalClientId;
            return 0;
        }
    }

    public PlayerHand MyHand {
        get {
            if (myPlayerIndex >= 0 && myPlayerIndex < allSeats.Count) {
                return allSeats[myPlayerIndex];
            }

            for (int i = 0; i < allSeats.Count; i++) {
                if (allSeats[i] != null) {
                    NetworkObject netObj = allSeats[i].GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsSpawned && netObj.OwnerClientId == MyClientId) {
                        myPlayerIndex = i;
                        return allSeats[i];
                    }
                }
            }

            if (!IsServer && myPlayerIndex == -1) {
                RequestSeatAssignmentServerRpc();
            }

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

        netPlayerCount.OnValueChanged += (oldVal, newVal) => UpdateSeatVisibility(newVal);
        UpdateSeatVisibility(netPlayerCount.Value);
    }

    public void AssignSeats() {
        if (!IsServer) return;

        int seatIndex = 1;

        if (allSeats.Count > 0 && allSeats[0] != null) {
            allSeats[0].gameObject.SetActive(true);
            myPlayerIndex = 0;

            NetworkObject hostSeatNet = allSeats[0].GetComponent<NetworkObject>();
            ulong serverId = NetworkManager.ServerClientId;

            if (hostSeatNet.IsSpawned) {
                if (hostSeatNet.OwnerClientId != serverId) hostSeatNet.ChangeOwnership(serverId);
            } else {
                try { hostSeatNet.SpawnWithOwnership(serverId); } catch { }
            }
        }

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList) {
            if (client.ClientId == NetworkManager.ServerClientId) continue;

            if (seatIndex < allSeats.Count) {
                PlayerHand seat = allSeats[seatIndex];
                if (seat != null) {
                    seat.gameObject.SetActive(true);
                    NetworkObject seatNetObj = seat.GetComponent<NetworkObject>();

                    if (seatNetObj.IsSpawned) {
                        if (seatNetObj.OwnerClientId != client.ClientId) seatNetObj.ChangeOwnership(client.ClientId);
                    } else {
                        try { seatNetObj.SpawnWithOwnership(client.ClientId); } catch { }
                    }

                    ClientRpcParams rpcParams = new ClientRpcParams {
                        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { client.ClientId } }
                    };
                    SetPlayerIndexClientRpc(seatIndex, rpcParams);
                }
                seatIndex++;
            }
        }

        for (int i = seatIndex; i < allSeats.Count; i++) {
            if (allSeats[i] != null) allSeats[i].gameObject.SetActive(false);
        }

        netPlayerCount.Value = seatIndex;
        totalPlayers = seatIndex;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSeatAssignmentServerRpc(ServerRpcParams rpcParams = default) {
        ulong senderId = rpcParams.Receive.SenderClientId;
        for (int i = 0; i < allSeats.Count; i++) {
            if (allSeats[i] != null) {
                NetworkObject netObj = allSeats[i].GetComponent<NetworkObject>();
                if (netObj != null && netObj.OwnerClientId == senderId) {
                    ClientRpcParams cParams = new ClientRpcParams {
                        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { senderId } }
                    };
                    SetPlayerIndexClientRpc(i, cParams);
                    return;
                }
            }
        }
    }

    [ClientRpc]
    private void SetPlayerIndexClientRpc(int assignedIndex, ClientRpcParams rpcParams = default) {
        myPlayerIndex = assignedIndex;
    }

    private void UpdateSeatVisibility(int count) {
        for (int i = 0; i < allSeats.Count; i++) {
            if (allSeats[i] != null) {
                allSeats[i].gameObject.SetActive(i < count);
            }
        }

        // Populate the scoreboard list dynamically for all clients when seats are assigned
        if (playerScores.Count != count && count > 0) {
            playerScores.Clear();
            for (int i = 0; i < count; i++) {
                playerScores.Add(new PlayerScoreData {
                    playerName = (i == 0) ? "Host" : $"Player {i + 1}",
                    score = 0
                });
            }
            OnScoresUpdated?.Invoke();
        }
    }

    // --- NETWORKED SCORING LOGIC ---
    public void AddScore(int playerIndex, int amount) {
        if (!IsServer) return;
        AddScoreClientRpc(playerIndex, amount);
    }

    [ClientRpc]
    private void AddScoreClientRpc(int playerIndex, int amount) {
        if (playerIndex >= 0 && playerIndex < playerScores.Count) {
            playerScores[playerIndex].score += amount;

            // Ring the global alarm bell so the UI knows to update!
            OnScoresUpdated?.Invoke();
        }
    }

    public void RequestAddCardToSpecificHand(CardView card, PlayerHand targetHand) {
        if (card == null || targetHand == null) return;
        NetworkObject cardNetObj = card.GetComponent<NetworkObject>();
        int seatIndex = allSeats.IndexOf(targetHand);

        if (cardNetObj != null && seatIndex >= 0) {
            AddCardToHandServerRpc(cardNetObj.NetworkObjectId, seatIndex);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void AddCardToHandServerRpc(ulong cardNetId, int seatIndex) {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cardNetId, out NetworkObject cardNetObj)) {
            ulong newOwner = allSeats[seatIndex].GetComponent<NetworkObject>().OwnerClientId;
            if (cardNetObj.OwnerClientId != newOwner) {
                cardNetObj.ChangeOwnership(newOwner);
            }
            AddCardToHandClientRpc(cardNetId, seatIndex);
        }
    }

    [ClientRpc]
    private void AddCardToHandClientRpc(ulong cardNetId, int seatIndex) {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cardNetId, out NetworkObject cardNetObj)) {
            CardView cv = cardNetObj.GetComponent<CardView>();
            if (cv == null) return;

            foreach (var h in allSeats) {
                if (h != null && h.cardsInHand.Contains(cv)) h.RemoveCard(cv);
            }

            if (seatIndex >= 0 && seatIndex < allSeats.Count) {
                PlayerHand hand = allSeats[seatIndex];
                if (hand != null) {
                    hand.gameObject.SetActive(true);
                    hand.AddCard(cv);
                }
            }
        }
    }

    public void RequestRemoveCardFromHands(CardView card) {
        if (card == null) return;
        NetworkObject cardNetObj = card.GetComponent<NetworkObject>();
        if (cardNetObj != null) {
            RemoveCardFromHandsServerRpc(cardNetObj.NetworkObjectId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RemoveCardFromHandsServerRpc(ulong cardNetId) {
        RemoveCardFromHandsClientRpc(cardNetId);
    }

    [ClientRpc]
    private void RemoveCardFromHandsClientRpc(ulong cardNetId) {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cardNetId, out NetworkObject cardNetObj)) {
            CardView cv = cardNetObj.GetComponent<CardView>();
            if (cv == null) return;

            foreach (var h in allSeats) {
                if (h != null && h.cardsInHand.Contains(cv)) h.RemoveCard(cv);
            }
        }
    }
}