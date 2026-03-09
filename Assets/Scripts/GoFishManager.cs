using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;

public class GoFishManager : NetworkBehaviour {
    public static GoFishManager Instance;

    [Header("Game Settings")]
    public int startingHandSize = 7;
    public int refillAmount = 5;
    public Deck mainDeck;

    [Header("UI Feedback")]
    public TextMeshProUGUI gameLogText;
    public GameObject turnIndicatorPopUp;
    public TextMeshProUGUI turnIndicatorText;

    [Header("Session State")]
    public NetworkVariable<int> netCurrentTurn = new NetworkVariable<int>(-1);
    public bool gameInProgress = false;

    private List<GoFishPlayer> activePlayers = new List<GoFishPlayer>();
    private Coroutine reminderCoroutine;

    void Awake() {
        if (Instance == null) Instance = this;
    }

    public override void OnNetworkSpawn() {
        if (IsServer) {
            StartCoroutine(WaitForClientsAndSetup());
        } else {
            UpdateLog("Waiting for Host to deal...");
        }
    }

    IEnumerator WaitForClientsAndSetup() {
        yield return new WaitForSeconds(1.5f);
        GameManager.Instance.AssignSeats();
        yield return new WaitForSeconds(0.5f);

        activePlayers.Clear();
        var allSeats = GameManager.Instance.allSeats;

        for (int i = 0; i < allSeats.Count; i++) {
            if (allSeats[i] != null && allSeats[i].gameObject.activeSelf) {
                GoFishPlayer player = allSeats[i].GetComponent<GoFishPlayer>();
                if (player == null) player = allSeats[i].gameObject.AddComponent<GoFishPlayer>();

                player.seatIndex = i;
                player.playerName = (i == 0) ? "Host" : $"Player {i + 1}";
                activePlayers.Add(player);
            }
        }

        if (activePlayers.Count == 0) yield break;

        yield return StartCoroutine(InitialDeal());

        gameInProgress = true;

        // Randomize the starting player
        int randomStart = Random.Range(0, activePlayers.Count);
        StartTurnServer(activePlayers[randomStart].seatIndex);
    }

    IEnumerator InitialDeal() {
        UpdateLogServerAndClient("Dealing cards...");
        for (int i = 0; i < startingHandSize; i++) {
            foreach (var player in activePlayers) {
                PlayerHand visualHand = GameManager.Instance.allSeats[player.seatIndex];
                if (visualHand != null) {
                    mainDeck.ServerDrawCard(visualHand);
                }
                yield return new WaitForSeconds(0.15f);
            }
        }
        UpdateLogServerAndClient("Game Started!");
    }

    // --- TURN LOGIC (SERVER ONLY) ---
    private void StartTurnServer(int playerSeatIndex) {
        if (!IsServer) return;

        netCurrentTurn.Value = playerSeatIndex;
        GoFishPlayer activePlayer = activePlayers.Find(p => p.seatIndex == playerSeatIndex);

        ShowTurnNotificationClientRpc(activePlayer.playerName, playerSeatIndex);

        // If it's an AI/Empty seat, simulate a turn
        bool isHuman = GameManager.Instance.allSeats[playerSeatIndex].IsOwnedByServer == false || playerSeatIndex == 0;
        if (!isHuman) {
            StartCoroutine(AITurnRoutine(activePlayer));
        }
    }

    [ClientRpc]
    private void ShowTurnNotificationClientRpc(string pName, int playerSeatIndex) {
        if (turnIndicatorPopUp != null) {
            bool isMe = (playerSeatIndex == GameManager.Instance.myPlayerIndex);
            turnIndicatorText.text = isMe ? "YOUR TURN" : $"{pName.ToUpper()}'S TURN";
            StartCoroutine(PopUpRoutine());
        }

        // Handle the UX Reminder for the local player
        if (reminderCoroutine != null) StopCoroutine(reminderCoroutine);

        if (playerSeatIndex == GameManager.Instance.myPlayerIndex) {
            reminderCoroutine = StartCoroutine(TurnReminderRoutine());
        }
    }

    IEnumerator PopUpRoutine() {
        turnIndicatorPopUp.SetActive(true);
        yield return new WaitForSeconds(1.5f);
        turnIndicatorPopUp.SetActive(false);
    }

    IEnumerator TurnReminderRoutine() {
        // Wait for the popup to clear and give them a few seconds to think
        yield return new WaitForSeconds(4.0f);

        // Keep reminding them every 6 seconds as long as it remains their turn
        while (netCurrentTurn.Value == GameManager.Instance.myPlayerIndex) {
            UpdateLog("Your Turn: Click an opponent's hand to ask for a card.");
            yield return new WaitForSeconds(6.0f);
        }
    }

    // --- REQUEST & TRANSFER LOGIC ---

    [ServerRpc(RequireOwnership = false)]
    public void SubmitRequestServerRpc(int requesterSeat, int targetSeat, Rank requestedRank) {
        // Stop the reminder loop on the server immediately so it doesn't overlap the dialogue
        if (reminderCoroutine != null) StopCoroutine(reminderCoroutine);

        GoFishPlayer requester = activePlayers.Find(p => p.seatIndex == requesterSeat);
        GoFishPlayer target = activePlayers.Find(p => p.seatIndex == targetSeat);

        UpdateLogServerAndClient($"{requester.playerName}: 'Do you have any {requestedRank}s, {target.playerName}?'");

        if (target.HasRank(requestedRank)) {
            TransferCardsServer(requesterSeat, targetSeat, requestedRank);
            CheckForBooksServer(requester);
            UpdateLogServerAndClient($"{target.playerName} had it! {requester.playerName} goes again.");
            StartTurnServer(requesterSeat); // They guessed right, go again
        } else {
            UpdateLogServerAndClient($"{target.playerName}: 'GO FISH!'");
            StartCoroutine(GoFishRoutine(requester, requestedRank));
        }
    }

    private void TransferCardsServer(int requesterSeat, int targetSeat, Rank rank) {
        PlayerHand reqHand = GameManager.Instance.allSeats[requesterSeat];
        PlayerHand tgtHand = GameManager.Instance.allSeats[targetSeat];

        List<CardView> cardsToMove = tgtHand.cardsInHand.Where(cv => cv.GetCardData() != null && cv.GetCardData().rank == rank).ToList();

        foreach (CardView cv in cardsToMove) {
            NetworkObject netObj = cv.GetComponent<NetworkObject>();
            if (netObj != null) {
                // Change network ownership to the stealer
                netObj.ChangeOwnership(reqHand.GetComponent<NetworkObject>().OwnerClientId);

                // Tell clients to physically move the 3D card
                MoveCardClientRpc(netObj.NetworkObjectId, reqHand.GetComponent<NetworkObject>().NetworkObjectId, tgtHand.GetComponent<NetworkObject>().NetworkObjectId);
            }
        }
    }

    [ClientRpc]
    private void MoveCardClientRpc(ulong cardNetId, ulong newHandNetId, ulong oldHandNetId) {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cardNetId, out NetworkObject cardObj)) {
            CardView cv = cardObj.GetComponent<CardView>();

            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(oldHandNetId, out NetworkObject oldHand)) {
                oldHand.GetComponent<PlayerHand>().RemoveCard(cv);
            }

            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(newHandNetId, out NetworkObject newHand)) {
                newHand.GetComponent<PlayerHand>().AddCard(cv);
            }

            // CRITICAL FIX: Force the physics state instantly so it doesn't fall through the table during the network blip
            if (cv.TryGetComponent<Rigidbody>(out var rb)) {
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero; // Unity 6 standard
                rb.angularVelocity = Vector3.zero;
            }
            if (cv.TryGetComponent<Collider>(out var col)) {
                col.isTrigger = true;
            }
        }
    }

    IEnumerator GoFishRoutine(GoFishPlayer player, Rank requestedRank) {
        yield return new WaitForSeconds(1.0f); // dramatic pause

        PlayerHand visualHand = GameManager.Instance.allSeats[player.seatIndex];
        Card drawnCard = null;

        if (mainDeck.cards.Count > 0) {
            drawnCard = mainDeck.ServerDrawCard(visualHand);
        }

        yield return new WaitForSeconds(1.5f); // wait for card to arrive in hand

        CheckForBooksServer(player);

        if (drawnCard != null && drawnCard.rank == requestedRank) {
            UpdateLogServerAndClient($"{player.playerName} drew the {requestedRank} they asked for! They go again.");
            StartTurnServer(player.seatIndex);
        } else {
            // End turn, pass to next player
            int currentIndex = activePlayers.IndexOf(player);
            int nextPlayerIndex = (currentIndex + 1) % activePlayers.Count;

            if (CheckGameOver()) EndGame();
            else StartTurnServer(activePlayers[nextPlayerIndex].seatIndex);
        }
    }

    // --- SCORING & REFILLING ---

    private void CheckForBooksServer(GoFishPlayer player) {
        var handData = player.GetLogicalHand();
        int required = 4; // Standard book size

        var groups = handData.GroupBy(c => c.rank).Where(g => g.Count() >= required).ToList();

        foreach (var group in groups) {
            Rank matchRank = group.Key;

            GameManager.Instance.AddScore(player.seatIndex, 1);
            UpdateLogServerAndClient($"{player.playerName} scored a book of {matchRank}s!");

            PlayerHand hand = GameManager.Instance.allSeats[player.seatIndex];
            List<CardView> cardsToRemove = hand.cardsInHand.Where(c => c.GetCardData() != null && c.GetCardData().rank == matchRank).ToList();

            foreach (var cv in cardsToRemove) {
                NetworkObject netObj = cv.GetComponent<NetworkObject>();
                if (netObj != null) {
                    netObj.Despawn(); // This gracefully destroys it across the entire network
                }
            }
        }

        if (player.GetLogicalHand().Count == 0 && mainDeck.cards.Count > 0) {
            UpdateLogServerAndClient($"{player.playerName} is out of cards! Redrawing...");
            StartCoroutine(RefillHandRoutine(player));
        }
    }

    IEnumerator RefillHandRoutine(GoFishPlayer player) {
        PlayerHand visualHand = GameManager.Instance.allSeats[player.seatIndex];
        for (int i = 0; i < refillAmount; i++) {
            if (mainDeck.cards.Count > 0) {
                mainDeck.ServerDrawCard(visualHand);
                yield return new WaitForSeconds(0.2f);
            }
        }
    }

    bool CheckGameOver() {
        return mainDeck.cards.Count == 0 && activePlayers.All(p => p.GetLogicalHand().Count == 0);
    }

    void EndGame() {
        gameInProgress = false;
        if (reminderCoroutine != null) StopCoroutine(reminderCoroutine);
        UpdateLogServerAndClient("GAME OVER!");
    }

    // --- UI HELPERS ---
    public void UpdateLog(string message) {
        if (gameLogText != null) gameLogText.text = message;
    }

    private void UpdateLogServerAndClient(string message) {
        UpdateLog(message);
        UpdateLogClientRpc(message);
    }

    [ClientRpc]
    private void UpdateLogClientRpc(string message) {
        // We pause the local reminder routine here so dialogue isn't overwritten instantly
        if (reminderCoroutine != null) StopCoroutine(reminderCoroutine);
        UpdateLog(message);

        // Restart the reminder timer safely if it is still my turn
        if (netCurrentTurn.Value == GameManager.Instance.myPlayerIndex && gameInProgress) {
            reminderCoroutine = StartCoroutine(TurnReminderRoutine());
        }
    }

    // --- AI FALLBACK ---
    IEnumerator AITurnRoutine(GoFishPlayer aiPlayer) {
        yield return new WaitForSeconds(2f);
        var hand = aiPlayer.GetLogicalHand();
        if (hand.Count == 0) yield break;

        Rank randomRank = hand[Random.Range(0, hand.Count)].rank;
        List<GoFishPlayer> validTargets = activePlayers.Where(p => p != aiPlayer).ToList();
        GoFishPlayer target = validTargets[Random.Range(0, validTargets.Count)];

        SubmitRequestServerRpc(aiPlayer.seatIndex, target.seatIndex, randomRank);
    }
}