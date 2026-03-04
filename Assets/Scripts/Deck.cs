using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class Deck : NetworkBehaviour {
    public NetworkVariable<int> netCardCount = new NetworkVariable<int>(0);

    [SerializeField] public List<Card> cards = new List<Card>();
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private Transform drawSpawnPoint;
    [SerializeField] private float dealSpeed = 0.15f;

    public int cardCount = 0;

    public List<Card> GetCards() { return cards; }

    public override void OnNetworkSpawn() {
        Debug.Log("<color=yellow>[Deck]</color> OnNetworkSpawn triggered.");
        if (IsServer) {
            int targetDecks = GoFishSettings.DeckCount > 0 ? GoFishSettings.DeckCount : 1;
            Debug.Log($"<color=yellow>[Deck]</color> Server spawning deck. Target deck multiplier: {targetDecks}");
            CreateStandardDeck(targetDecks);
            Shuffle();
        } else {
            Debug.Log("<color=yellow>[Deck]</color> Client spawned deck. Waiting for server sync.");
        }
    }

    public void InitializeWithCards(List<Card> initialCards) {
        if (cards == null) cards = new List<Card>();
        cards.Clear();
        cards.AddRange(initialCards);
        cardCount = cards.Count;
        if (IsServer) netCardCount.Value = cards.Count;
        Debug.Log($"<color=yellow>[Deck]</color> initialized manually with {cards.Count} cards.");
    }

    void CreateStandardDeck(int deckMultiplier) {
        cards.Clear();
        for (int d = 0; d < deckMultiplier; d++) {
            foreach (Suit suit in System.Enum.GetValues(typeof(Suit))) {
                foreach (Rank rank in System.Enum.GetValues(typeof(Rank))) {
                    cards.Add(new Card(suit, rank));
                }
            }
        }
        cardCount = cards.Count;
        netCardCount.Value = cards.Count;
        Debug.Log($"<color=yellow>[Deck]</color> Created deck with {cards.Count} cards.");
    }

    public void RequestDrawCard() {
        if (GameManager.Instance.MyHand == null) {
            Debug.LogError("<color=red>[Deck]</color> RequestDrawCard failed: GameManager.Instance.MyHand is null!");
            return;
        }
        ulong mySeatId = GameManager.Instance.MyHand.GetComponent<NetworkObject>().NetworkObjectId;
        Debug.Log($"<color=yellow>[Deck]</color> Client requesting card for seat ID {mySeatId}.");
        DrawCardServerRpc(mySeatId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void DrawCardServerRpc(ulong targetSeatNetworkId, ServerRpcParams rpcParams = default) {
        if (cards.Count == 0) {
            Debug.LogWarning("<color=yellow>[Deck]</color> DrawCardServerRpc aborted: Deck is empty.");
            return;
        }

        Card topCardData = cards[0];
        cards.RemoveAt(0);
        netCardCount.Value = cards.Count;
        cardCount = cards.Count;

        if (cardPrefab == null || drawSpawnPoint == null) {
            Debug.LogError("<color=red>[Deck]</color> DrawCardServerRpc failed: cardPrefab or drawSpawnPoint is null!");
            return;
        }

        GameObject newCardObj = Instantiate(cardPrefab, drawSpawnPoint.position, Quaternion.identity);
        NetworkObject netObj = newCardObj.GetComponent<NetworkObject>();

        if (netObj == null) {
            Debug.LogError("<color=red>[Deck]</color> DrawCardServerRpc failed: instantiated card missing NetworkObject!");
            return;
        }

        netObj.SpawnWithOwnership(rpcParams.Receive.SenderClientId);
        SetCardDataClientRpc(netObj.NetworkObjectId, topCardData.suit, topCardData.rank, targetSeatNetworkId);
    }

    public Card ServerDrawCard(PlayerHand targetHand) {
        Debug.Log("<color=yellow>[Deck]</color> ServerDrawCard called.");
        if (!IsServer) {
            Debug.LogError("<color=red>[Deck]</color> ServerDrawCard failed: Called by a Client!");
            return null;
        }

        if (cards.Count == 0) {
            Debug.LogWarning("<color=red>[Deck]</color> ServerDrawCard failed: Deck is empty!");
            return null;
        }

        if (targetHand == null) {
            Debug.LogError("<color=red>[Deck]</color> ServerDrawCard failed: targetHand is null!");
            return null;
        }

        if (cardPrefab == null) {
            Debug.LogError("<color=red>[Deck]</color> ServerDrawCard failed: cardPrefab is not assigned in the Inspector!");
            return null;
        }

        if (drawSpawnPoint == null) {
            Debug.LogError("<color=red>[Deck]</color> ServerDrawCard failed: drawSpawnPoint is not assigned in the Inspector!");
            return null;
        }

        Card topCardData = cards[0];
        cards.RemoveAt(0);
        netCardCount.Value = cards.Count;
        cardCount = cards.Count;

        Debug.Log("<color=yellow>[Deck]</color> Instantiating card prefab...");
        GameObject newCardObj = Instantiate(cardPrefab, drawSpawnPoint.position, Quaternion.identity);
        NetworkObject netObj = newCardObj.GetComponent<NetworkObject>();

        if (netObj == null) {
            Debug.LogError("<color=red>[Deck]</color> ServerDrawCard failed: Card Prefab is missing a NetworkObject component!");
            return null;
        }

        NetworkObject handNetObj = targetHand.GetComponent<NetworkObject>();
        if (handNetObj == null) {
            Debug.LogError("<color=red>[Deck]</color> ServerDrawCard failed: targetHand is missing a NetworkObject!");
            return null;
        }

        Debug.Log($"<color=yellow>[Deck]</color> Spawning card with ownership for client {handNetObj.OwnerClientId}...");

        try {
            netObj.SpawnWithOwnership(handNetObj.OwnerClientId);
        } catch (System.Exception e) {
            Debug.LogError($"<color=red>[Deck]</color> ServerDrawCard Exception during SpawnWithOwnership: {e.Message}");
            return null;
        }

        Debug.Log("<color=yellow>[Deck]</color> Card spawned successfully. Sending ClientRpc to update visuals.");
        SetCardDataClientRpc(netObj.NetworkObjectId, topCardData.suit, topCardData.rank, handNetObj.NetworkObjectId);

        return topCardData;
    }

    [ClientRpc]
    private void SetCardDataClientRpc(ulong cardNetworkId, Suit suit, Rank rank, ulong targetSeatNetworkId) {
        Debug.Log($"<color=yellow>[Deck]</color> SetCardDataClientRpc received for Card NetID {cardNetworkId}.");
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cardNetworkId, out NetworkObject cardNetObj)) {
            CardView newCardView = cardNetObj.GetComponent<CardView>();
            if (newCardView != null) {
                newCardView.SetCardData(new Card(suit, rank));
                cardNetObj.gameObject.tag = "MoveableObject";
            } else {
                Debug.LogError("<color=red>[Deck]</color> SetCardDataClientRpc: CardView component missing on spawned card!");
            }

            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetSeatNetworkId, out NetworkObject seatNetObj)) {
                PlayerHand targetHand = seatNetObj.GetComponent<PlayerHand>();
                if (targetHand != null) {
                    targetHand.AddCard(newCardView);
                } else {
                    Debug.LogError("<color=red>[Deck]</color> SetCardDataClientRpc: PlayerHand component missing on seat object!");
                }
            } else {
                Debug.LogError($"<color=red>[Deck]</color> SetCardDataClientRpc: Could not find seat with NetID {targetSeatNetworkId}!");
            }
        } else {
            Debug.LogError($"<color=red>[Deck]</color> SetCardDataClientRpc: Could not find spawned card with NetID {cardNetworkId}!");
        }
    }

    public void AddCard(Card card) {
        cards.Add(card);
        cardCount = cards.Count;
        if (IsServer) netCardCount.Value = cards.Count;
    }

    public void Shuffle() {
        if (!IsServer) return;
        for (int i = 0; i < cards.Count; i++) {
            int rand = Random.Range(i, cards.Count);
            (cards[i], cards[rand]) = (cards[rand], cards[i]);
        }
        Debug.Log("<color=yellow>[Deck]</color> Deck shuffled.");
    }

    public void Flip() { FlipServerRpc(); }

    [ServerRpc(RequireOwnership = false)]
    private void FlipServerRpc() { FlipClientRpc(); }

    [ClientRpc]
    private void FlipClientRpc() { transform.Rotate(0f, 0f, 180f, Space.Self); }

    public void StartDealing(int cardsPerPlayer) { StartDealingServerRpc(cardsPerPlayer); }

    [ServerRpc(RequireOwnership = false)]
    private void StartDealingServerRpc(int cardsPerPlayer) { StartCoroutine(DealRoutine(cardsPerPlayer)); }

    private IEnumerator DealRoutine(int count) {
        if (GameManager.Instance == null) yield break;
        for (int i = 0; i < count; i++) {
            foreach (PlayerHand seat in GameManager.Instance.allSeats) {
                if (seat != null && seat.gameObject.activeInHierarchy) {
                    ServerDrawCard(seat);
                    yield return new WaitForSeconds(dealSpeed);
                }
            }
        }
    }

    public void RemoveTopCard() { RemoveTopCardServerRpc(); }

    [ServerRpc(RequireOwnership = false)]
    private void RemoveTopCardServerRpc() {
        if (cards == null || cards.Count == 0) return;

        Card topCardData = cards[0];
        cards.RemoveAt(0);
        netCardCount.Value = cards.Count;
        cardCount = cards.Count;

        float offsetDistance = cardPrefab.gameObject.transform.localScale.x * 1.1f;
        Vector3 finalSpawnPos = transform.position + (Vector3.up * 0.2f);

        GameObject newCardGO = Instantiate(cardPrefab, finalSpawnPos, Quaternion.identity);
        NetworkObject netObj = newCardGO.GetComponent<NetworkObject>();

        netObj.Spawn();
        SetTopCardDataClientRpc(netObj.NetworkObjectId, topCardData.suit, topCardData.rank);
    }

    [ClientRpc]
    private void SetTopCardDataClientRpc(ulong cardNetworkId, Suit suit, Rank rank) {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cardNetworkId, out NetworkObject cardNetObj)) {
            CardView cv = cardNetObj.GetComponent<CardView>();
            if (cv != null) cv.SetCardData(new Card(suit, rank));

            if (cardNetObj.TryGetComponent<Rigidbody>(out var rb)) {
                rb.isKinematic = false;
            }
        }
    }
}