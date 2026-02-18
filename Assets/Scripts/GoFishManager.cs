using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;

public class GoFishManager : MonoBehaviour {
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
    public int currentPlayerTurnIndex = 0;
    public bool gameInProgress = false;

    private List<GoFishPlayer> activePlayers = new List<GoFishPlayer>();

    void Awake() {
        if (Instance == null) Instance = this;
    }

    void Start() {
        // Apply settings from Lobby
        GameManager.Instance.totalPlayers = GoFishSettings.PlayerCount;
        StartCoroutine(SetupGameRoutine());
    }

    IEnumerator SetupGameRoutine() {
        yield return new WaitForSeconds(0.5f);

        // Re-call AssignSeats to ensure the right number of seats are active
        GameManager.Instance.AssignSeats();

        var allSeats = GameManager.Instance.allSeats;
        activePlayers.Clear();

        for (int i = 0; i < allSeats.Count; i++) {
            if (allSeats[i].gameObject.activeSelf) {
                GoFishPlayer player = allSeats[i].GetComponent<GoFishPlayer>();
                if (player == null) player = allSeats[i].gameObject.AddComponent<GoFishPlayer>();

                player.seatIndex = i;
                player.playerName = (i == GameManager.Instance.myPlayerIndex) ? "You" : $"Opponent {i}";
                activePlayers.Add(player);
            }
        }

        yield return StartCoroutine(InitialDeal());
        gameInProgress = true;
        StartTurn(0);
    }

    IEnumerator InitialDeal() {
        UpdateLog("Dealing cards...");
        for (int i = 0; i < startingHandSize; i++) {
            foreach (var player in activePlayers) {
                PlayerHand visualHand = GameManager.Instance.allSeats[player.seatIndex];
                Card drawnData = mainDeck.DrawCard(visualHand);
                player.AddCard(drawnData);
                yield return new WaitForSeconds(0.1f);
            }
        }
    }

    public void StartTurn(int playerIndex) {
        currentPlayerTurnIndex = playerIndex;
        GoFishPlayer activePlayer = activePlayers.Find(p => p.seatIndex == playerIndex);
        StartCoroutine(ShowTurnNotification(activePlayer.playerName));

        if (playerIndex != GameManager.Instance.myPlayerIndex) {
            StartCoroutine(AITurnRoutine(activePlayer));
        }
    }

    IEnumerator AITurnRoutine(GoFishPlayer aiPlayer) {
        yield return new WaitForSeconds(2f);
        if (aiPlayer.logicalHand.Count == 0) {
            int nextPlayer = (activePlayers.IndexOf(aiPlayer) + 1) % activePlayers.Count;
            StartTurn(activePlayers[nextPlayer].seatIndex);
            yield break;
        }

        Rank randomRank = aiPlayer.logicalHand[Random.Range(0, aiPlayer.logicalHand.Count)].rank;
        List<GoFishPlayer> validTargets = activePlayers.Where(p => p != aiPlayer).ToList();
        GoFishPlayer target = validTargets[Random.Range(0, validTargets.Count)];

        UpdateLog($"{aiPlayer.playerName}: 'Do you have any {randomRank}s, {target.playerName}?'");
        yield return new WaitForSeconds(1.5f);
        ProcessRequestAI(aiPlayer, target, randomRank);
    }

    private void ProcessRequestAI(GoFishPlayer requester, GoFishPlayer target, Rank requestedRank) {
        if (target.HasRank(requestedRank)) {
            TransferCards(requester, target, requestedRank);
            CheckForBooks(requester);
            StartTurn(requester.seatIndex);
        } else {
            StartCoroutine(GoFishRoutine(requester, requestedRank));
        }
    }

    public void ProcessRequest(int targetPlayerIndex, Rank requestedRank) {
        GoFishPlayer requester = activePlayers.Find(p => p.seatIndex == GameManager.Instance.myPlayerIndex);
        GoFishPlayer target = activePlayers.Find(p => p.seatIndex == targetPlayerIndex);

        if (target.HasRank(requestedRank)) {
            TransferCards(requester, target, requestedRank);
            CheckForBooks(requester);
            StartTurn(currentPlayerTurnIndex);
        } else {
            StartCoroutine(GoFishRoutine(requester, requestedRank));
        }
    }

    private void TransferCards(GoFishPlayer requester, GoFishPlayer target, Rank rank) {
        PlayerHand requesterVisual = GameManager.Instance.allSeats[requester.seatIndex];
        PlayerHand targetVisual = GameManager.Instance.allSeats[target.seatIndex];

        List<CardView> visualCardsToMove = targetVisual.cardsInHand.Where(cv => cv.GetCardData().rank == rank).ToList();

        foreach (CardView cv in visualCardsToMove) {
            Card data = cv.GetCardData();
            target.RemoveCard(data);
            requester.AddCard(data);
            targetVisual.RemoveCard(cv);
            requesterVisual.AddCard(cv);
        }
    }

    IEnumerator GoFishRoutine(GoFishPlayer player, Rank requestedRank) {
        PlayerHand visualHand = GameManager.Instance.allSeats[player.seatIndex];
        if (mainDeck.cards.Count > 0) {
            Card drawnCard = mainDeck.DrawCard(visualHand);
            player.AddCard(drawnCard);
            yield return new WaitForSeconds(1.0f);

            if (drawnCard.rank == requestedRank) {
                CheckForBooks(player);
                if (player.logicalHand.Count == 0) yield return StartCoroutine(RefillHandRoutine(player));
                StartTurn(player.seatIndex);
                yield break;
            }
        }

        CheckForBooks(player);
        if (player.logicalHand.Count == 0) yield return StartCoroutine(RefillHandRoutine(player));

        int currentIndex = activePlayers.IndexOf(player);
        int nextPlayerIndex = (currentIndex + 1) % activePlayers.Count;

        if (CheckGameOver()) EndGame();
        else StartTurn(activePlayers[nextPlayerIndex].seatIndex);
    }

    IEnumerator RefillHandRoutine(GoFishPlayer player) {
        if (mainDeck.cards.Count == 0) yield break;
        PlayerHand visualHand = GameManager.Instance.allSeats[player.seatIndex];
        for (int i = 0; i < refillAmount; i++) {
            if (mainDeck.cards.Count > 0) {
                player.AddCard(mainDeck.DrawCard(visualHand));
                yield return new WaitForSeconds(0.2f);
            }
        }
    }

    void CheckForBooks(GoFishPlayer player) {
        int required = GoFishSettings.GetMatchCount();
        var groups = player.logicalHand.GroupBy(c => c.rank).Where(g => g.Count() >= required).ToList();

        foreach (var group in groups) {
            Rank matchRank = group.Key;
            player.logicalHand.RemoveAll(c => c.rank == matchRank);
            GameManager.Instance.AddScore(player.seatIndex, 1);

            PlayerHand visualHand = GameManager.Instance.allSeats[player.seatIndex];
            List<CardView> toRemove = visualHand.cardsInHand.Where(cv => cv.GetCardData().rank == matchRank).ToList();
            foreach (CardView cv in toRemove) {
                visualHand.RemoveCard(cv);
                cv.ConvertToPile();
            }
        }
    }

    bool CheckGameOver() {
        return mainDeck.cards.Count == 0 && activePlayers.All(p => p.logicalHand.Count == 0);
    }

    void EndGame() {
        gameInProgress = false;
        UpdateLog("GAME OVER!");
    }

    public void UpdateLog(string message) {
        if (gameLogText != null) gameLogText.text = message;
    }

    IEnumerator ShowTurnNotification(string pName) {
        if (turnIndicatorPopUp == null) yield break;
        turnIndicatorText.text = (pName == "You") ? "YOUR TURN" : $"{pName.ToUpper()}'S TURN";
        turnIndicatorPopUp.SetActive(true);
        yield return new WaitForSeconds(1.5f);
        turnIndicatorPopUp.SetActive(false);
    }
}