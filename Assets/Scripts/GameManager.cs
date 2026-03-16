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

    public event Action OnScoresUpdated;

    private Dictionary<ulong, string> clientNames = new Dictionary<ulong, string>();

    public ulong MyClientId {
        get {
            if (NetworkManager.Singleton != null) return NetworkManager.Singleton.LocalClientId;
            return 0;
        }
    }

    private PlayerHand _myHand;
    public PlayerHand MyHand {
        get {
            if (_myHand != null) return _myHand;

            if (myPlayerIndex >= 0 && myPlayerIndex < allSeats.Count) {
                _myHand = allSeats[myPlayerIndex];
                return _myHand;
            }

            if (allSeats != null) {
                foreach (var seat in allSeats) {
                    if (seat != null) {
                        NetworkObject netObj = seat.GetComponent<NetworkObject>();
                        if (netObj != null && netObj.IsOwner) {
                            _myHand = seat;
                            myPlayerIndex = allSeats.IndexOf(seat);
                            return _myHand;
                        }
                    }
                }
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
            clientNames[NetworkManager.ServerClientId] = GameSessionData.PlayerName;

            if (FindAnyObjectByType<GoFishManager>() == null) {
                StartCoroutine(WaitForSandboxPlayersRoutine());
            }
        } else {
            RegisterNameServerRpc(GameSessionData.PlayerName);
            RequestSeatAssignmentServerRpc();
        }

        netPlayerCount.OnValueChanged += (oldVal, newVal) => UpdateSeatVisibility(newVal);
        UpdateSeatVisibility(netPlayerCount.Value);
    }

    // --- NAME SYNCING LOGIC ---
    [ServerRpc(RequireOwnership = false)]
    private void RegisterNameServerRpc(string pName, ServerRpcParams rpcParams = default) {
        clientNames[rpcParams.Receive.SenderClientId] = pName;
        PushNamesToClients();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestNameSyncServerRpc() {
        PushNamesToClients();
    }

    private void PushNamesToClients() {
        if (!IsServer) return;

        string[] names = new string[allSeats.Count];
        names[0] = clientNames.ContainsKey(NetworkManager.ServerClientId) ? clientNames[NetworkManager.ServerClientId] : "Host";

        int seatIndex = 1;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList) {
            if (client.ClientId == NetworkManager.ServerClientId) continue;
            if (seatIndex < allSeats.Count) {
                names[seatIndex] = clientNames.ContainsKey(client.ClientId) ? clientNames[client.ClientId] : $"Player {seatIndex + 1}";
                seatIndex++;
            }
        }

        string joinedNames = string.Join("|", names);
        SyncNamesClientRpc(joinedNames);
    }

    [ClientRpc]
    private void SyncNamesClientRpc(string joinedNames) {
        string[] names = joinedNames.Split('|');
        for (int i = 0; i < names.Length && i < playerScores.Count; i++) {
            if (!string.IsNullOrEmpty(names[i])) {
                // 1. Update the scoreboard
                playerScores[i].playerName = names[i];

                // 2. THE NEW FIX: Update the 3D text floating over the table!
                if (i < allSeats.Count && allSeats[i] != null) {
                    allSeats[i].UpdateNameText(names[i]);
                }
            }
        }
        OnScoresUpdated?.Invoke();
    }
    // --------------------------

    private System.Collections.IEnumerator WaitForSandboxPlayersRoutine() {
        while (NetworkManager.Singleton.ConnectedClients.Count < GameSessionData.PlayerCount) {
            yield return new WaitForSeconds(0.5f);
        }
        yield return new WaitForSeconds(1.0f);
        AssignSeats();
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

        PushNamesToClients();
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
        if (assignedIndex >= 0 && assignedIndex < allSeats.Count) {
            _myHand = allSeats[assignedIndex];
        }
    }

    private void UpdateSeatVisibility(int count) {
        for (int i = 0; i < allSeats.Count; i++) {
            if (allSeats[i] != null) {
                allSeats[i].gameObject.SetActive(i < count);

                // If a seat gets turned off (e.g., in a 2 player game), clear its floating name just in case!
                if (i >= count) {
                    allSeats[i].UpdateNameText("");
                }
            }
        }

        if (playerScores.Count != count && count > 0) {
            playerScores.Clear();
            for (int i = 0; i < count; i++) {
                playerScores.Add(new PlayerScoreData {
                    playerName = (i == 0) ? "Host" : $"Player {i + 1}",
                    score = 0
                });
            }
            OnScoresUpdated?.Invoke();

            if (!IsServer) {
                RequestNameSyncServerRpc();
            }
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

    // --- SANDBOX PHYSICS & OWNERSHIP ROUTERS ---
    [ServerRpc(RequireOwnership = false)]
    public void GrabObjectServerRpc(ulong networkObjectId, ServerRpcParams rpcParams = default) {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject netObj)) {
            if (netObj.OwnerClientId != rpcParams.Receive.SenderClientId) {
                netObj.ChangeOwnership(rpcParams.Receive.SenderClientId);
            }
            SetObjectKinematicClientRpc(networkObjectId, true);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void DropObjectServerRpc(ulong networkObjectId) {
        SetObjectKinematicClientRpc(networkObjectId, false);
    }

    [ClientRpc]
    private void SetObjectKinematicClientRpc(ulong networkObjectId, bool isKinematic) {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject netObj)) {
            if (netObj.TryGetComponent<Rigidbody>(out var rb)) {
                if (netObj.IsOwner) {
                    rb.isKinematic = false;
                } else {
                    rb.isKinematic = isKinematic;
                }
            }
        }
    }
}