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
    private Coroutine bannerCoroutine;

    void Awake() {
        if (Instance == null) Instance = this;
    }

    public override void OnNetworkSpawn() {
        if (IsServer) StartCoroutine(WaitForClientsAndSetup());
        else UpdateLog("Waiting for Host to deal...");
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
        int randomStart = Random.Range(0, activePlayers.Count);
        StartTurnServer(activePlayers[randomStart].seatIndex);
    }

    IEnumerator InitialDeal() {
        UpdateLogServerAndClient("Dealing cards...");
        for (int i = 0; i < startingHandSize; i++) {
            foreach (var player in activePlayers) {
                PlayerHand visualHand = GameManager.Instance.allSeats[player.seatIndex];
                if (visualHand != null) mainDeck.ServerDrawCard(visualHand);
                yield return new WaitForSeconds(0.15f);
            }
        }

        yield return new WaitForSeconds(1.0f);

        foreach (var player in activePlayers) {
            yield return StartCoroutine(CheckForBooksRoutine(player));
        }

        UpdateLogServerAndClient("Game Started!");
        FullStateSync();
        TriggerCameraSnapClientRpc();
    }

    [ClientRpc]
    private void TriggerCameraSnapClientRpc() {
        ObjectSelect objSelect = FindFirstObjectByType<ObjectSelect>();
        if (objSelect != null) objSelect.OnFocusOnHandButtonPressed();
    }

    private void StartTurnServer(int playerSeatIndex) {
        if (!IsServer || !gameInProgress) return;

        PrintTableState($"Start of Turn for Seat {playerSeatIndex}");

        if (CheckGameOver()) {
            EndGame();
            return;
        }

        netCurrentTurn.Value = playerSeatIndex;
        GoFishPlayer activePlayer = activePlayers.Find(p => p.seatIndex == playerSeatIndex);

        ShowTurnNotificationClientRpc(activePlayer.playerName, playerSeatIndex);

        bool isHuman = GameManager.Instance.allSeats[playerSeatIndex].IsOwnedByServer == false || playerSeatIndex == 0;
        if (!isHuman) StartCoroutine(AITurnRoutine(activePlayer));
    }

    [ClientRpc]
    private void ShowTurnNotificationClientRpc(string pName, int playerSeatIndex) {
        if (turnIndicatorPopUp != null) {
            bool isMe = (playerSeatIndex == GameManager.Instance.myPlayerIndex);
            string msg = isMe ? "YOUR TURN" : $"{pName.ToUpper()}'S TURN";

            if (bannerCoroutine != null) StopCoroutine(bannerCoroutine);
            bannerCoroutine = StartCoroutine(CustomPopUpRoutine(msg, 1.5f));
        }

        if (reminderCoroutine != null) StopCoroutine(reminderCoroutine);
        if (playerSeatIndex == GameManager.Instance.myPlayerIndex) {
            reminderCoroutine = StartCoroutine(TurnReminderRoutine());
        }
    }

    IEnumerator CustomPopUpRoutine(string message, float duration) {
        turnIndicatorPopUp.SetActive(true);
        if (turnIndicatorText != null) turnIndicatorText.text = message;
        yield return new WaitForSeconds(duration);
        turnIndicatorPopUp.SetActive(false);
    }

    IEnumerator TurnReminderRoutine() {
        yield return new WaitForSeconds(4.0f);
        while (netCurrentTurn.Value == GameManager.Instance.myPlayerIndex) {
            UpdateLog("Your Turn: Click an opponent's hand to ask for a card.");
            yield return new WaitForSeconds(6.0f);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SubmitRequestServerRpc(int requesterSeat, int targetSeat, Rank requestedRank) {
        if (reminderCoroutine != null) StopCoroutine(reminderCoroutine);

        GoFishPlayer requester = activePlayers.Find(p => p.seatIndex == requesterSeat);
        GoFishPlayer target = activePlayers.Find(p => p.seatIndex == targetSeat);

        PrintTableState($"{requester.playerName} requesting {requestedRank}s from {target.playerName}");

        string highlightedRank = $"<color=yellow><b>{requestedRank}s</b></color>";
        UpdateLogServerAndClient($"{requester.playerName}: 'Do you have any {highlightedRank}, {target.playerName}?'");
        ShowBannerClientRpc($"ASKING FOR {highlightedRank}!", 2.5f);

        if (target.HasRank(requestedRank)) {
            StartCoroutine(SuccessfulStealRoutine(requester, target, requestedRank));
        } else {
            UpdateLogServerAndClient($"{target.playerName}: 'GO FISH!'");
            StartCoroutine(GoFishRoutine(requester, requestedRank));
        }
    }

    [ClientRpc]
    private void ShowBannerClientRpc(string message, float duration) {
        if (turnIndicatorPopUp != null) {
            if (bannerCoroutine != null) StopCoroutine(bannerCoroutine);
            bannerCoroutine = StartCoroutine(CustomPopUpRoutine(message, duration));
        }
    }

    IEnumerator SuccessfulStealRoutine(GoFishPlayer requester, GoFishPlayer target, Rank requestedRank) {
        TransferCardsServer(requester.seatIndex, target.seatIndex, requestedRank);
        yield return new WaitForSeconds(1.0f);
        FullStateSync();

        PrintTableState($"After Steal, Before Score Check");

        if (target.GetLogicalHand().Count == 0 && mainDeck.cards.Count > 0) {
            UpdateLogServerAndClient($"{target.playerName} was robbed of their last card! Redrawing...");
            yield return StartCoroutine(RefillHandRoutine(target));
        }

        yield return StartCoroutine(CheckForBooksRoutine(requester));

        UpdateLogServerAndClient($"{target.playerName} had it! {requester.playerName} goes again.");
        yield return new WaitForSeconds(1.5f);

        if (CheckGameOver()) EndGame();
        else StartTurnServer(requester.seatIndex);
    }

    private void TransferCardsServer(int requesterSeat, int targetSeat, Rank rank) {
        PlayerHand reqHand = GameManager.Instance.allSeats[requesterSeat];
        PlayerHand tgtHand = GameManager.Instance.allSeats[targetSeat];

        List<CardView> cardsToMove = tgtHand.cardsInHand.Where(cv => cv != null && cv.GetCardData() != null && cv.GetCardData().rank == rank).ToList();

        Debug.Log($"<color=cyan>[DEBUG-STEAL]</color> Transferring {cardsToMove.Count} cards of rank {rank} from Seat {targetSeat} to Seat {requesterSeat}");

        foreach (CardView cv in cardsToMove) {
            tgtHand.RemoveCard(cv);
            reqHand.AddCard(cv);

            NetworkObject netObj = cv.GetComponent<NetworkObject>();
            if (netObj != null) {
                netObj.ChangeOwnership(reqHand.GetComponent<NetworkObject>().OwnerClientId);
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

            if (cv.TryGetComponent<Rigidbody>(out var rb)) {
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            if (cv.TryGetComponent<Collider>(out var col)) col.enabled = false;
        }
    }

    IEnumerator GoFishRoutine(GoFishPlayer player, Rank requestedRank) {
        yield return new WaitForSeconds(1.0f);
        PlayerHand visualHand = GameManager.Instance.allSeats[player.seatIndex];
        Card drawnCard = null;

        if (mainDeck.cards.Count > 0) {
            drawnCard = mainDeck.ServerDrawCard(visualHand);
        }

        yield return new WaitForSeconds(1.5f);
        FullStateSync();

        yield return StartCoroutine(CheckForBooksRoutine(player));

        if (drawnCard != null && drawnCard.rank == requestedRank) {
            string highlightedRank = $"<color=yellow><b>{requestedRank}s</b></color>";
            UpdateLogServerAndClient($"{player.playerName} drew the {highlightedRank} they asked for! They go again.");
            ShowBannerClientRpc("LUCKY DRAW!", 2.0f);
            yield return new WaitForSeconds(2.0f);

            if (CheckGameOver()) EndGame();
            else StartTurnServer(player.seatIndex);
        } else {
            int currentIndex = activePlayers.IndexOf(player);
            int nextPlayerIndex = (currentIndex + 1) % activePlayers.Count;

            if (CheckGameOver()) EndGame();
            else StartTurnServer(activePlayers[nextPlayerIndex].seatIndex);
        }
    }

    private IEnumerator CheckForBooksRoutine(GoFishPlayer player) {
        var handData = player.GetLogicalHand();
        if (handData.Count == 0) yield break;

        int requiredCards = GoFishSettings.GetMatchCount();
        var groups = handData.GroupBy(c => c.rank).Where(g => g.Count() >= requiredCards).ToList();
        if (groups.Count == 0) yield break;

        List<string> scoredRanksThisCheck = new List<string>();
        int totalScoreAdded = 0;
        PlayerHand hand = GameManager.Instance.allSeats[player.seatIndex];
        List<ulong> networkIdsToDespawn = new List<ulong>();

        Debug.Log($"<color=cyan>[DEBUG-SCORE]</color> Found {groups.Count} matching sets for {player.playerName}");

        foreach (var group in groups) {
            Rank matchRank = group.Key;
            int totalCardsOfRank = group.Count();
            int setsScored = totalCardsOfRank / requiredCards;

            for (int i = 0; i < setsScored; i++) {
                totalScoreAdded++;
                scoredRanksThisCheck.Add($"<color=yellow><b>{matchRank}s</b></color>");

                List<CardView> cardsToRemove = hand.cardsInHand
                    .Where(c => c != null && c.GetCardData() != null && c.GetCardData().rank == matchRank)
                    .Take(requiredCards).ToList();

                foreach (var cv in cardsToRemove) {
                    hand.RemoveCard(cv);
                    if (cv.TryGetComponent<NetworkObject>(out var netObj)) {
                        networkIdsToDespawn.Add(netObj.NetworkObjectId);
                    }
                }
            }
        }

        if (totalScoreAdded > 0) {
            GameManager.Instance.AddScore(player.seatIndex, totalScoreAdded);
            string allRanks = string.Join(", ", scoredRanksThisCheck);

            UpdateLogServerAndClient($"{player.playerName} scored matching sets of: {allRanks}!");
            ShowBannerClientRpc($"{player.playerName.ToUpper()} SCORED!", 3.0f);

            RemoveCardsFromAllListsClientRpc(networkIdsToDespawn.ToArray());
            yield return new WaitForSeconds(0.2f);

            foreach (ulong id in networkIdsToDespawn) {
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject netObj)) {
                    netObj.Despawn();
                }
            }

            yield return new WaitForSeconds(0.3f);
            FullStateSync();
            PrintTableState($"After Scoring Books for {player.playerName}");

            if (player.GetLogicalHand().Count == 0 && mainDeck.cards.Count > 0) {
                UpdateLogServerAndClient($"{player.playerName} scored their last card! Redrawing...");
                yield return StartCoroutine(RefillHandRoutine(player));
            }
        }
    }

    [ClientRpc]
    private void RemoveCardsFromAllListsClientRpc(ulong[] cardIds) {
        foreach (ulong id in cardIds) {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject netObj)) {
                CardView cv = netObj.GetComponent<CardView>();
                if (cv != null) {
                    foreach (var seat in GameManager.Instance.allSeats) {
                        if (seat != null && seat.cardsInHand.Contains(cv)) seat.RemoveCard(cv);
                    }
                }
            }
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

        yield return new WaitForSeconds(1.0f);
        FullStateSync();
        yield return StartCoroutine(CheckForBooksRoutine(player));
    }

    private void FullStateSync() {
        if (!IsServer) return;
        foreach (var seat in activePlayers) {
            PlayerHand hand = GameManager.Instance.allSeats[seat.seatIndex];
            ulong handNetId = hand.GetComponent<NetworkObject>().NetworkObjectId;
            List<ulong> cardIds = hand.cardsInHand
                .Where(c => c != null)
                .Select(c => c.GetComponent<NetworkObject>().NetworkObjectId).ToList();

            SyncSingleHandClientRpc(handNetId, cardIds.ToArray());
        }
    }

    [ClientRpc]
    private void SyncSingleHandClientRpc(ulong handNetId, ulong[] cardIds) {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(handNetId, out NetworkObject handNetObj)) {
            PlayerHand hand = handNetObj.GetComponent<PlayerHand>();
            hand.cardsInHand.Clear();

            foreach (ulong cId in cardIds) {
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cId, out NetworkObject cardNetObj)) {
                    CardView cv = cardNetObj.GetComponent<CardView>();
                    if (cv != null) {
                        hand.AddCard(cv);
                        if (cv.TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = true;
                    }
                }
            }
        }
    }

    bool CheckGameOver() {
        if (mainDeck.cards.Count > 0) return false;

        List<Card> allCardsLeft = new List<Card>();
        foreach (var p in activePlayers) allCardsLeft.AddRange(p.GetLogicalHand());

        if (allCardsLeft.Count == 0) return true;

        int requiredCards = GoFishSettings.GetMatchCount();
        bool matchPossible = allCardsLeft.GroupBy(c => c.rank).Any(g => g.Count() >= requiredCards);

        if (!matchPossible) {
            string left = string.Join(", ", allCardsLeft.Select(c => c != null ? c.rank.ToString() : "NULL"));
            Debug.LogWarning($"<color=red>[GoFishManager]</color> EMERGENCY GAME OVER! Leftover cards: [{left}]. Match Size required: {requiredCards}");
            PrintTableState("EMERGENCY GAME OVER TRIGGERED");
            return true;
        }

        return false;
    }

    void EndGame() {
        if (!gameInProgress) return;
        gameInProgress = false;

        if (reminderCoroutine != null) StopCoroutine(reminderCoroutine);

        UpdateLogServerAndClient("GAME OVER! Calculating Winner...");
        ShowBannerClientRpc("GAME OVER!", 5.0f);
    }

    public void UpdateLog(string message) { if (gameLogText != null) gameLogText.text = message; }

    private void UpdateLogServerAndClient(string message) {
        UpdateLog(message);
        UpdateLogClientRpc(message);
    }

    [ClientRpc]
    private void UpdateLogClientRpc(string message) {
        if (reminderCoroutine != null) StopCoroutine(reminderCoroutine);
        UpdateLog(message);
        if (netCurrentTurn.Value == GameManager.Instance.myPlayerIndex && gameInProgress) {
            reminderCoroutine = StartCoroutine(TurnReminderRoutine());
        }
    }

    IEnumerator AITurnRoutine(GoFishPlayer aiPlayer) {
        yield return new WaitForSeconds(2f);
        var hand = aiPlayer.GetLogicalHand();
        if (hand.Count == 0) yield break;

        Rank randomRank = hand[Random.Range(0, hand.Count)].rank;
        List<GoFishPlayer> validTargets = activePlayers.Where(p => p != aiPlayer).ToList();
        GoFishPlayer target = validTargets[Random.Range(0, validTargets.Count)];

        SubmitRequestServerRpc(aiPlayer.seatIndex, target.seatIndex, randomRank);
    }

    // --- DIAGNOSTICS LOGGING ---
    private void PrintTableState(string context) {
        if (!IsServer) return;
        string log = $"<color=magenta>========== [TABLE STATE: {context}] ==========</color>\n";
        log += $"Deck remaining: {mainDeck.cards.Count} cards.\n";

        foreach (var p in activePlayers) {
            PlayerHand hand = GameManager.Instance.allSeats[p.seatIndex];
            if (hand == null) continue;

            string cardRanks = string.Join(", ", hand.cardsInHand.Select(c => {
                if (c == null) return "NULL_CARD";
                if (c.GetCardData() == null) return "NO_DATA";
                return c.GetCardData().rank.ToString();
            }));

            log += $"Player {p.playerName} (Seat {p.seatIndex}): {hand.cardsInHand.Count} cards -> [{cardRanks}]\n";
        }
        log += "<color=magenta>================================================</color>";
        Debug.Log(log);
    }
}