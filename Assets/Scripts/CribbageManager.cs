using UnityEngine;
using Unity.Netcode;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

public class CribbageManager : NetworkBehaviour {
    public static CribbageManager Instance;

    public enum GamePhase { Setup, DiscardToCrib, Cutting, Pegging, TheShow, GameOver }

    [Header("Network State")]
    public NetworkVariable<GamePhase> netCurrentPhase = new NetworkVariable<GamePhase>(GamePhase.Setup);
    public GamePhase CurrentPhase => netCurrentPhase.Value;
    public NetworkVariable<int> cribPlayerSeat = new NetworkVariable<int>(-1);
    public NetworkVariable<int> clientsReady = new NetworkVariable<int>(0);

    [Header("UI Elements")]
    public TextMeshProUGUI phaseText;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI promptText;
    public Button sendToCribButton;
    public Button cutDeckButton;
    public Button goButton;
    public Button showButton;

    [Header("Table Layout & Prefabs")]
    public GameObject pilePrefab;
    public Deck mainDeck;
    public Vector3 cribPileOffset = new Vector3(3.0f, 0f, 1.5f);
    public Vector3 starterCardOffset = new Vector3(0f, 0.05f, 0f);

    private NetworkVariable<int> playersDiscarded = new NetworkVariable<int>(0);
    private Pile activeCribPile;
    public Card starterCardData;

    [Header("Pegging State")]
    public NetworkVariable<int> activeTurnSeat = new NetworkVariable<int>(-1);
    public NetworkVariable<int> peggingTotal = new NetworkVariable<int>(0);
    public NetworkVariable<int> lastPlayerToPeg = new NetworkVariable<int>(-1);
    private NetworkVariable<bool> p0Passed = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> p1Passed = new NetworkVariable<bool>(false);

    [Header("Pegging Areas")]
    public Transform[] peggingAreas = new Transform[2];
    private int[] peggedCardsCount = new int[2];
    private List<Card> currentPeggingSequence = new List<Card>();

    // TRACKERS: Remembers exactly which cards belong to which player so we can pull them back for The Show
    private List<ulong> p0PeggedCards = new List<ulong>();
    private List<ulong> p1PeggedCards = new List<ulong>();

    [Header("Show State")]
    // 0 = Non-Dealer, 1 = Dealer, 2 = Crib
    public NetworkVariable<int> currentShowStep = new NetworkVariable<int>(0);

    void Awake() {
        if (Instance == null) Instance = this;
    }

    public override void OnNetworkSpawn() {
        if (sendToCribButton != null) sendToCribButton.onClick.AddListener(OnSendToCribClicked);
        if (cutDeckButton != null) cutDeckButton.onClick.AddListener(OnCutDeckClicked);
        if (goButton != null) goButton.onClick.AddListener(OnGoClicked);
        if (showButton != null) {
            showButton.onClick.AddListener(OnShowClicked);
            showButton.gameObject.SetActive(false);
        }

        netCurrentPhase.OnValueChanged += (oldPhase, newPhase) => {
            if (phaseText != null) phaseText.text = $"PHASE: {newPhase.ToString().ToUpper()}";
            UpdateUIButtons(activeTurnSeat.Value);
        };

        activeTurnSeat.OnValueChanged += (oldVal, newVal) => {
            UpdateUIButtons(newVal);
        };

        if (IsServer) {
            clientsReady.Value = 0;
            StartCoroutine(WaitForClientsAndSetup());
        }
        ClientReadyServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ClientReadyServerRpc() { clientsReady.Value++; }

    [ClientRpc]
    private void UpdateUIClientRpc(string mainPrompt, string subStatus) {
        if (promptText != null) promptText.text = mainPrompt;
        if (statusText != null) statusText.text = subStatus;
    }

    // --- GAME END (121 CHECK) ---
    public void CheckForWinCondition(int seat) {
        if (CurrentPhase == GamePhase.GameOver) return;

        if (GameManager.Instance.playerScores[seat].score >= 121) {
            if (IsServer) netCurrentPhase.Value = GamePhase.GameOver;
            string winnerName = GameManager.Instance.playerScores[seat].playerName;
            UpdateUIClientRpc($"<color=yellow>{winnerName.ToUpper()} WINS!</color>", "Game Over!");
        }
    }

    // --- ROUND SETUP ---
    private IEnumerator WaitForClientsAndSetup() {
        UpdateUIClientRpc("Waiting for opponent to load...", "Setting up table...");
        yield return new WaitUntil(() => clientsReady.Value >= 2);
        yield return new WaitForSeconds(1.0f);

        GameManager.Instance.AssignSeats();
        cribPlayerSeat.Value = Random.Range(0, 2);

        yield return StartCoroutine(DealCardsRoutine());
    }

    private IEnumerator DealCardsRoutine() {
        if (mainDeck == null) mainDeck = FindFirstObjectByType<Deck>();
        if (mainDeck != null) {
            UpdateUIClientRpc("Dealing cards...", "Shuffling...");
            for (int i = 0; i < 6; i++) {
                for (int s = 0; s < 2; s++) {
                    if (s < GameManager.Instance.allSeats.Count) {
                        PlayerHand seat = GameManager.Instance.allSeats[s];
                        if (seat != null) {
                            mainDeck.ServerDrawCard(seat);
                            yield return new WaitForSeconds(0.15f);
                        }
                    }
                }
            }
        }
        yield return new WaitForSeconds(1.0f);
        FullStateSync();
        TriggerCameraSnapClientRpc();
        StartPhase_DiscardToCrib();
    }

    [ClientRpc]
    private void TriggerCameraSnapClientRpc() {
        ObjectSelect objSelect = FindFirstObjectByType<ObjectSelect>();
        if (objSelect != null) objSelect.OnFocusOnHandButtonPressed();
    }

    private void FullStateSync() {
        if (!IsServer) return;
        for (int s = 0; s < 2; s++) {
            if (s < GameManager.Instance.allSeats.Count) {
                PlayerHand hand = GameManager.Instance.allSeats[s];
                ulong handNetId = hand.GetComponent<NetworkObject>().NetworkObjectId;
                List<ulong> cardIds = hand.cardsInHand.Where(c => c != null).Select(c => c.GetComponent<NetworkObject>().NetworkObjectId).ToList();
                SyncSingleHandClientRpc(handNetId, cardIds.ToArray());
            }
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

    // --- PHASE 1: DISCARD TO CRIB ---
    private void StartPhase_DiscardToCrib() {
        if (IsServer) netCurrentPhase.Value = GamePhase.DiscardToCrib;
        playersDiscarded.Value = 0;
        string cribOwner = GameManager.Instance.playerScores[cribPlayerSeat.Value].playerName;
        UpdateUIClientRpc("Select 2 cards for the Crib.", $"It is {cribOwner}'s Crib.");
        ShowDiscardUIButtonClientRpc();
    }

    [ClientRpc]
    private void ShowDiscardUIButtonClientRpc() {
        if (sendToCribButton != null) {
            sendToCribButton.gameObject.SetActive(true);
            sendToCribButton.interactable = false;
        }
    }

    public void OnEmphasizeChanged(int emphasizedCount) {
        if (CurrentPhase != GamePhase.DiscardToCrib) return;
        if (sendToCribButton != null) sendToCribButton.interactable = (emphasizedCount == 2);
    }

    public void OnSendToCribClicked() {
        sendToCribButton.gameObject.SetActive(false);
        PlayerHand myHand = GameManager.Instance.MyHand;
        if (myHand == null || myHand.emphasizedCards.Count != 2) return;

        ulong card1Id = myHand.emphasizedCards[0].GetComponent<NetworkObject>().NetworkObjectId;
        ulong card2Id = myHand.emphasizedCards[1].GetComponent<NetworkObject>().NetworkObjectId;

        SubmitToCribServerRpc(card1Id, card2Id);
        myHand.ClearEmphasizedCards();
        UpdateUIClientRpc("Waiting for opponent...", "Cards sent to Crib.");
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitToCribServerRpc(ulong card1Id, ulong card2Id) {
        List<Card> cribData = new List<Card>();

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(card1Id, out NetworkObject c1)) {
            cribData.Add(c1.GetComponent<CardView>().GetCardData());
            c1.Despawn();
        }
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(card2Id, out NetworkObject c2)) {
            cribData.Add(c2.GetComponent<CardView>().GetCardData());
            c2.Despawn();
        }

        AddDataToCribPile(cribData);
        playersDiscarded.Value++;

        if (playersDiscarded.Value >= 2) StartPhase_TheCut();
    }

    private void AddDataToCribPile(List<Card> newCards) {
        if (activeCribPile == null) {
            float sideMultiplier = (cribPlayerSeat.Value == 0) ? -1f : 1f;
            Vector3 rawOffset = new Vector3(cribPileOffset.x * sideMultiplier, cribPileOffset.y, cribPileOffset.z);
            Vector3 cribPos = mainDeck.transform.position + rawOffset;

            GameObject pileGO = Instantiate(pilePrefab, cribPos, mainDeck.transform.rotation);
            if (pileGO.TryGetComponent<Rigidbody>(out var rb)) {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            NetworkObject netObj = pileGO.GetComponent<NetworkObject>();
            netObj.Spawn();
            SnapObjectToPositionClientRpc(netObj.NetworkObjectId, cribPos, mainDeck.transform.rotation);

            activeCribPile = pileGO.GetComponent<Pile>();
            activeCribPile.InitializeWithCards(newCards);
        } else {
            // FIX: Append to the pile instead of overwriting the previous player's discard!
            foreach (Card c in newCards) {
                activeCribPile.AddCard(c);
            }
        }
    }

    // --- PHASE 2: THE CUT ---
    private void StartPhase_TheCut() {
        if (IsServer) netCurrentPhase.Value = GamePhase.Cutting;

        int nonCribSeat = (cribPlayerSeat.Value + 1) % 2;
        string cribOwnerName = GameManager.Instance.playerScores[cribPlayerSeat.Value].playerName;
        string cutterName = GameManager.Instance.playerScores[nonCribSeat].playerName;

        UpdateUIClientRpc($"{cutterName}, please cut the deck.", $"Crib belongs to: {cribOwnerName}.");
        ShowCutButtonClientRpc(nonCribSeat);
    }

    [ClientRpc]
    private void ShowCutButtonClientRpc(int cutterSeat) {
        if (cutDeckButton != null) {
            bool isMyTurn = (GameManager.Instance.myPlayerIndex == cutterSeat);
            cutDeckButton.gameObject.SetActive(isMyTurn);
            cutDeckButton.interactable = isMyTurn;
        }
    }

    public void OnCutDeckClicked() {
        cutDeckButton.gameObject.SetActive(false);
        CutDeckServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void CutDeckServerRpc() {
        if (mainDeck == null || mainDeck.cards.Count == 0) return;

        int drawIndex = mainDeck.cards.Count - 1;
        starterCardData = mainDeck.cards[drawIndex];
        mainDeck.cards.RemoveAt(drawIndex);

        Vector3 spawnPos = mainDeck.transform.position + starterCardOffset;
        Quaternion faceUpRot = mainDeck.transform.rotation * Quaternion.Euler(0, 0, 180f);

        GameObject newCardObj = Instantiate(mainDeck.cardPrefab, spawnPos, faceUpRot);
        if (newCardObj.TryGetComponent<Collider>(out var col)) col.enabled = true;
        if (newCardObj.TryGetComponent<Rigidbody>(out var rb)) {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        CardView cv = newCardObj.GetComponent<CardView>();
        cv.netSuit.Value = starterCardData.suit;
        cv.netRank.Value = starterCardData.rank;
        cv.netTargetHand.Value = -1;

        newCardObj.GetComponent<NetworkObject>().Spawn();
        SnapObjectToPositionClientRpc(newCardObj.GetComponent<NetworkObject>().NetworkObjectId, spawnPos, faceUpRot);

        if (starterCardData.rank == Rank.Jack) {
            GameManager.Instance.AddScore(cribPlayerSeat.Value, 2);
            CheckForWinCondition(cribPlayerSeat.Value);
            UpdateUIClientRpc("His Heels! +2 Points.", "Starter Card flipped.");
        } else {
            UpdateUIClientRpc("The Deck has been cut.", "Starter Card flipped.");
        }

        StartCoroutine(TransitionToPeggingRoutine());
    }

    private IEnumerator TransitionToPeggingRoutine() {
        yield return new WaitForSeconds(3.0f);
        StartPhase_Pegging();
    }

    // --- PHASE 3: PEGGING ---
    private void StartPhase_Pegging() {
        if (IsServer) {
            netCurrentPhase.Value = GamePhase.Pegging;
            peggingTotal.Value = 0;
            lastPlayerToPeg.Value = -1;
            activeTurnSeat.Value = (cribPlayerSeat.Value + 1) % 2;

            peggedCardsCount[0] = 0;
            peggedCardsCount[1] = 0;
            p0PeggedCards.Clear();
            p1PeggedCards.Clear();

            ResetPeggingSequenceServer();
        }
        UpdatePeggingUIClientRpc(activeTurnSeat.Value, peggingTotal.Value);
    }

    public void OnGoClicked() {
        if (CurrentPhase != GamePhase.Pegging) return;
        if (activeTurnSeat.Value != GameManager.Instance.myPlayerIndex) return;

        if (goButton != null) goButton.gameObject.SetActive(false);
        PassTurnServerRpc(GameManager.Instance.myPlayerIndex);
    }

    [ServerRpc(RequireOwnership = false)]
    public void PassTurnServerRpc(int playerSeat) {
        if (playerSeat == 0) p0Passed.Value = true;
        else p1Passed.Value = true;

        bool p0Done = p0Passed.Value || (4 - peggedCardsCount[0] == 0);
        bool p1Done = p1Passed.Value || (4 - peggedCardsCount[1] == 0);

        if (p0Done && p1Done) {
            if (lastPlayerToPeg.Value >= 0) {
                GameManager.Instance.AddScore(lastPlayerToPeg.Value, 1);
                CheckForWinCondition(lastPlayerToPeg.Value);
            }

            ResetPeggingSequenceServer();

            if (4 - peggedCardsCount[0] == 0 && 4 - peggedCardsCount[1] == 0) {
                if (CurrentPhase != GamePhase.GameOver) StartCoroutine(TransitionToShowRoutine());
                return;
            }

            if (lastPlayerToPeg.Value >= 0) {
                int nextP = (lastPlayerToPeg.Value + 1) % 2;
                if (4 - peggedCardsCount[nextP] > 0) activeTurnSeat.Value = nextP;
                else activeTurnSeat.Value = lastPlayerToPeg.Value;
            }
        } else {
            int nextP = (playerSeat + 1) % 2;
            if (4 - peggedCardsCount[nextP] > 0) activeTurnSeat.Value = nextP;
            else activeTurnSeat.Value = playerSeat;
        }
        UpdatePeggingUIClientRpc(activeTurnSeat.Value, peggingTotal.Value);
    }

    private void ResetPeggingSequenceServer() {
        peggingTotal.Value = 0;
        currentPeggingSequence.Clear();
        p0Passed.Value = false;
        p1Passed.Value = false;
        lastPlayerToPeg.Value = -1;
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlayCardForPeggingServerRpc(ulong cardId, int playerSeat) {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cardId, out NetworkObject cardNetObj)) {
            CardView cv = cardNetObj.GetComponent<CardView>();
            Card playedCard = cv.GetCardData();

            int cardValue = playedCard.rank >= Rank.Ten ? 10 : (int)playedCard.rank;
            if (peggingTotal.Value + cardValue > 31) return;

            // Track exactly which card belonged to which player so we can recall them in The Show
            if (playerSeat == 0) p0PeggedCards.Add(cardId);
            else p1PeggedCards.Add(cardId);

            if (playerSeat == 0) p0Passed.Value = false;
            else p1Passed.Value = false;
            lastPlayerToPeg.Value = playerSeat;

            cv.netTargetHand.Value = -1;
            PlayerHand hand = GameManager.Instance.allSeats[playerSeat];
            if (hand.cardsInHand.Contains(cv)) hand.RemoveCard(cv);
            RemoveCardFromHandClientRpc(cardId, playerSeat);

            Transform pegArea = peggingAreas[playerSeat];
            int cardsAlreadyPegged = peggedCardsCount[playerSeat];
            peggedCardsCount[playerSeat]++;

            Vector3 playPos = pegArea.position + (pegArea.right * 0.3f * cardsAlreadyPegged) + new Vector3(0, 0.001f * cardsAlreadyPegged, 0);
            Quaternion faceUpRot = pegArea.rotation * Quaternion.Euler(0, 0, 180f);

            // FIX: Set to null to respect NGO rules, we will recall them using the Tracker Lists
            cv.transform.SetParent(null, true);
            if (cv.TryGetComponent<Rigidbody>(out var rb)) {
                rb.isKinematic = true;
                rb.useGravity = false;
                cv.transform.position = playPos;
                cv.transform.rotation = faceUpRot;
                rb.position = playPos;
                rb.rotation = faceUpRot;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            if (cv.TryGetComponent<Collider>(out var col)) col.enabled = true;

            SnapPeggingCardClientRpc(cardId, playerSeat, playPos, faceUpRot);

            int newTotal = peggingTotal.Value + cardValue;
            peggingTotal.Value = newTotal;
            currentPeggingSequence.Add(playedCard);
            int points = EvaluatePeggingScore(currentPeggingSequence, newTotal);

            if (points > 0) {
                GameManager.Instance.AddScore(playerSeat, points);
                CheckForWinCondition(playerSeat);
            }

            int p0Remaining = 4 - peggedCardsCount[0];
            int p1Remaining = 4 - peggedCardsCount[1];

            if (p0Remaining == 0 && p1Remaining == 0) {
                if (newTotal < 31) {
                    GameManager.Instance.AddScore(playerSeat, 1);
                    CheckForWinCondition(playerSeat);
                }
                if (CurrentPhase != GamePhase.GameOver) {
                    StartCoroutine(TransitionToShowRoutine());
                }
                return;
            }

            if (newTotal == 31) {
                ResetPeggingSequenceServer();
                int nextP = (playerSeat + 1) % 2;
                if ((nextP == 0 ? p0Remaining : p1Remaining) > 0) activeTurnSeat.Value = nextP;
                else activeTurnSeat.Value = playerSeat;
            } else {
                int nextP = (playerSeat + 1) % 2;
                bool nextPlayerPassed = (nextP == 0) ? p0Passed.Value : p1Passed.Value;

                if (!nextPlayerPassed && (nextP == 0 ? p0Remaining : p1Remaining) > 0) {
                    activeTurnSeat.Value = nextP;
                } else {
                    activeTurnSeat.Value = playerSeat;
                }
            }

            UpdatePeggingUIClientRpc(activeTurnSeat.Value, peggingTotal.Value);
        }
    }

    [ClientRpc]
    private void UpdatePeggingUIClientRpc(int currentTurnSeat, int currentTotal) {
        UpdateUIButtons(currentTurnSeat);
        string turnName = GameManager.Instance.playerScores[currentTurnSeat].playerName;

        if (GameManager.Instance.myPlayerIndex == currentTurnSeat) {
            promptText.text = $"<color=#00FF00>YOUR TURN!</color>\nTotal: {currentTotal}";
        } else {
            promptText.text = $"WAITING FOR {turnName.ToUpper()}\nTotal: {currentTotal}";
        }
    }

    private IEnumerator TransitionToShowRoutine() {
        yield return new WaitForSeconds(2.0f);
        if (CurrentPhase == GamePhase.GameOver) yield break;
        StartPhase_TheShow();
    }

    // --- PHASE 4: THE SHOW ---
    private void StartPhase_TheShow() {
        if (IsServer) {
            netCurrentPhase.Value = GamePhase.TheShow;
            currentShowStep.Value = 0;
            activeTurnSeat.Value = (cribPlayerSeat.Value + 1) % 2; // Non-dealer scores first

            // 1. Physically pull the cards back into the respective Player Hands
            foreach (ulong id in p0PeggedCards) {
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject netObj)) {
                    netObj.GetComponent<CardView>().netTargetHand.Value = 0;
                }
            }
            foreach (ulong id in p1PeggedCards) {
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject netObj)) {
                    netObj.GetComponent<CardView>().netTargetHand.Value = 1;
                }
            }

            // 2. Rotate the hands 90 degrees forward so everyone can clearly see them laid on the table
            RotateHandsClientRpc(90f);
        }
        UpdateShowUIClientRpc(activeTurnSeat.Value, currentShowStep.Value);
    }

    [ClientRpc]
    private void RotateHandsClientRpc(float xAngle) {
        for (int i = 0; i < 2; i++) {
            if (i < GameManager.Instance.allSeats.Count) {
                PlayerHand hand = GameManager.Instance.allSeats[i];
                hand.transform.localEulerAngles = new Vector3(xAngle, hand.transform.localEulerAngles.y, hand.transform.localEulerAngles.z);
            }
        }
    }

    public void OnShowClicked() {
        if (CurrentPhase != GamePhase.TheShow) return;
        if (activeTurnSeat.Value != GameManager.Instance.myPlayerIndex) return;

        if (showButton != null) showButton.gameObject.SetActive(false);
        ShowHandServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ShowHandServerRpc() {
        int scoringSeat = activeTurnSeat.Value;
        int step = currentShowStep.Value;

        List<Card> cardsToScore = new List<Card>();
        bool isCrib = (step == 2);

        // Fetch all cards belonging to the person currently scoring
        CardView[] allCards = FindObjectsByType<CardView>(FindObjectsSortMode.None);
        foreach (var cv in allCards) {
            if (cv.netTargetHand.Value == scoringSeat) {
                cardsToScore.Add(cv.GetCardData());
            }
        }

        int score = EvaluateHandScore(cardsToScore, starterCardData, isCrib);

        string handType = isCrib ? "Crib" : "Hand";
        string pName = GameManager.Instance.playerScores[scoringSeat].playerName;
        UpdateUIClientRpc($"<color=green>{pName}'s {handType} scores {score}!</color>", "Scoring Phase");

        if (score > 0) {
            GameManager.Instance.AddScore(scoringSeat, score);
            CheckForWinCondition(scoringSeat);
        }

        if (CurrentPhase == GamePhase.GameOver) return;

        StartCoroutine(AdvanceShowRoutine());
    }

    private IEnumerator AdvanceShowRoutine() {
        yield return new WaitForSeconds(4.0f);

        if (currentShowStep.Value == 0) {
            currentShowStep.Value = 1;
            activeTurnSeat.Value = cribPlayerSeat.Value;
            UpdateShowUIClientRpc(activeTurnSeat.Value, currentShowStep.Value);
        } else if (currentShowStep.Value == 1) {
            // DESTRUCT DEALER'S HAND AND OPEN THE CRIB
            CardView[] allCards = FindObjectsByType<CardView>(FindObjectsSortMode.None);
            foreach (var cv in allCards) {
                if (cv.netTargetHand.Value == cribPlayerSeat.Value) {
                    cv.GetComponent<NetworkObject>().Despawn();
                }
            }

            if (activeCribPile != null) {
                List<Card> cribCards = activeCribPile.GetCards();
                Vector3 pilePos = activeCribPile.transform.position;
                Quaternion pileRot = activeCribPile.transform.rotation;

                activeCribPile.GetComponent<NetworkObject>().Despawn();
                activeCribPile = null;

                foreach (Card c in cribCards) {
                    GameObject newCardObj = Instantiate(mainDeck.cardPrefab, pilePos, pileRot);
                    if (newCardObj.TryGetComponent<Rigidbody>(out var rb)) {
                        rb.isKinematic = true;
                        rb.useGravity = false;
                    }
                    CardView cv = newCardObj.GetComponent<CardView>();
                    cv.netSuit.Value = c.suit;
                    cv.netRank.Value = c.rank;
                    cv.netTargetHand.Value = cribPlayerSeat.Value;

                    newCardObj.GetComponent<NetworkObject>().Spawn();
                }
            }

            currentShowStep.Value = 2;
            activeTurnSeat.Value = cribPlayerSeat.Value;
            UpdateShowUIClientRpc(activeTurnSeat.Value, currentShowStep.Value);
        } else {
            StartCoroutine(ResetRoundRoutine());
        }
    }

    private IEnumerator ResetRoundRoutine() {
        UpdateUIClientRpc("Round Complete!", "Clearing Board...");
        RotateHandsClientRpc(0f); // Rotate hands back to vertical for the new round
        yield return new WaitForSeconds(2.0f);

        var objectsToDespawn = new List<NetworkObject>();
        foreach (var netObj in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values) {
            if (netObj.GetComponent<CardView>() != null || netObj.GetComponent<Pile>() != null) {
                objectsToDespawn.Add(netObj);
            }
        }
        foreach (var obj in objectsToDespawn) obj.Despawn();
        activeCribPile = null;

        cribPlayerSeat.Value = (cribPlayerSeat.Value + 1) % 2;

        List<Card> freshCards = new List<Card>();
        int targetDecks = GameSessionData.DeckCount > 0 ? GameSessionData.DeckCount : 1;
        for (int d = 0; d < targetDecks; d++) {
            foreach (Suit suit in System.Enum.GetValues(typeof(Suit))) {
                foreach (Rank rank in System.Enum.GetValues(typeof(Rank))) {
                    freshCards.Add(new Card(suit, rank));
                }
            }
        }
        mainDeck.InitializeWithCards(freshCards);
        mainDeck.Shuffle();

        yield return StartCoroutine(DealCardsRoutine());
    }

    [ClientRpc]
    private void UpdateShowUIClientRpc(int currentTurnSeat, int step) {
        UpdateUIButtons(currentTurnSeat);
        string turnName = GameManager.Instance.playerScores[currentTurnSeat].playerName;
        string handName = (step == 2) ? "Crib" : "Hand";

        if (GameManager.Instance.myPlayerIndex == currentTurnSeat) {
            promptText.text = $"<color=#00FF00>YOUR TURN!</color>\nScore your {handName}.";
        } else {
            promptText.text = $"WAITING FOR {turnName.ToUpper()}...\nTo score {handName}.";
        }
    }

    private void UpdateUIButtons(int currentTurnSeat) {
        bool isMyTurn = currentTurnSeat == GameManager.Instance.myPlayerIndex;

        if (goButton != null) {
            goButton.gameObject.SetActive(CurrentPhase == GamePhase.Pegging && isMyTurn);
        }
        if (showButton != null) {
            showButton.gameObject.SetActive(CurrentPhase == GamePhase.TheShow && isMyTurn);
        }
    }

    // --- SCORING ENGINES ---

    private int EvaluatePeggingScore(List<Card> sequence, int currentTotal) {
        int score = 0;
        if (currentTotal == 15) score += 2;
        if (currentTotal == 31) score += 2;

        int count = sequence.Count;
        if (count < 2) return score;

        int matchCount = 1;
        for (int i = count - 2; i >= 0; i--) {
            if (sequence[i].rank == sequence[count - 1].rank) matchCount++;
            else break;
        }
        if (matchCount == 2) score += 2;
        else if (matchCount == 3) score += 6;
        else if (matchCount == 4) score += 12;

        for (int length = count; length >= 3; length--) {
            if (IsValidRun(sequence.GetRange(count - length, length))) {
                score += length;
                break;
            }
        }
        return score;
    }

    private int EvaluateHandScore(List<Card> hand, Card starter, bool isCrib) {
        if (hand == null || hand.Count != 4) {
            Debug.LogError($"[Scoring] Expected exactly 4 cards, but found {hand?.Count}. Score evaluated to 0 to prevent crashes.");
            return 0;
        }

        int score = 0;
        List<Card> all = new List<Card>(hand);
        all.Add(starter); // The cut card correctly joins the hand for the combinatorics!

        // 1. Calculate 15s using a bitmask of all 32 combinations
        for (int i = 1; i < 32; i++) {
            int sum = 0;
            for (int j = 0; j < 5; j++) {
                if ((i & (1 << j)) != 0) {
                    int val = (int)all[j].rank;
                    sum += val >= 10 ? 10 : val;
                }
            }
            if (sum == 15) score += 2;
        }

        // 2. Calculate Pairs
        for (int i = 0; i < 4; i++) {
            for (int j = i + 1; j < 5; j++) {
                if (all[i].rank == all[j].rank) score += 2;
            }
        }

        // 3. Calculate Runs (Groups naturally find Double and Triple runs)
        var groups = all.GroupBy(c => (int)c.rank).OrderBy(g => g.Key).ToList();
        int currentStreak = 1;
        int currentMult = groups[0].Count();
        int maxStreak = 1;
        int maxMult = 1;

        for (int i = 1; i < groups.Count; i++) {
            if (groups[i].Key == groups[i - 1].Key + 1) {
                currentStreak++;
                currentMult *= groups[i].Count();
            } else {
                if (currentStreak > maxStreak) { maxStreak = currentStreak; maxMult = currentMult; }
                currentStreak = 1;
                currentMult = groups[i].Count();
            }
        }
        if (currentStreak > maxStreak) { maxStreak = currentStreak; maxMult = currentMult; }
        if (maxStreak >= 3) score += (maxStreak * maxMult);

        // 4. Calculate Flushes
        bool handFlush = hand.All(c => c.suit == hand[0].suit);
        bool starterMatch = starter.suit == hand[0].suit;
        if (isCrib) {
            if (handFlush && starterMatch) score += 5;
        } else {
            if (handFlush) score += starterMatch ? 5 : 4;
        }

        // 5. Calculate Nobs
        if (hand.Any(c => c.rank == Rank.Jack && c.suit == starter.suit)) score += 1;

        return score;
    }

    private bool IsValidRun(List<Card> subSeq) {
        List<int> ranks = subSeq.Select(c => (int)c.rank).ToList();
        ranks.Sort();
        for (int i = 0; i < ranks.Count - 1; i++) {
            if (ranks[i + 1] - ranks[i] != 1) return false;
        }
        return true;
    }

    // --- TELEPORTATION RPCs ---
    [ClientRpc]
    private void SnapObjectToPositionClientRpc(ulong netId, Vector3 pos, Quaternion rot) {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(netId, out NetworkObject netObj)) {
            netObj.transform.SetParent(null, true);
            if (netObj.TryGetComponent<Rigidbody>(out var rb)) {
                rb.isKinematic = true;
                rb.useGravity = false;
                netObj.transform.position = pos;
                netObj.transform.rotation = rot;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            if (netObj.TryGetComponent<Collider>(out var col)) col.enabled = true;
        }
    }

    [ClientRpc]
    private void SnapPeggingCardClientRpc(ulong netId, int playerSeat, Vector3 pos, Quaternion rot) {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(netId, out NetworkObject netObj)) {
            netObj.transform.SetParent(null, true); // FIX: Ensure this avoids the NGO Invalid Parent error
            if (netObj.TryGetComponent<Rigidbody>(out var rb)) {
                rb.isKinematic = true;
                rb.useGravity = false;
                netObj.transform.position = pos;
                netObj.transform.rotation = rot;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            if (netObj.TryGetComponent<Collider>(out var col)) col.enabled = true;
        }
    }

    [ClientRpc]
    private void RemoveCardFromHandClientRpc(ulong cardId, int playerSeat) {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cardId, out NetworkObject cardNetObj)) {
            CardView cv = cardNetObj.GetComponent<CardView>();
            PlayerHand hand = GameManager.Instance.allSeats[playerSeat];
            if (hand.cardsInHand.Contains(cv)) hand.RemoveCard(cv);
        }
    }
}