using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class Deck : NetworkBehaviour {
    [Header("Network Sync State")]
    public NetworkVariable<int> netCardCount = new NetworkVariable<int>(0);
    public NetworkVariable<Suit> netBottomSuit = new NetworkVariable<Suit>();
    public NetworkVariable<Rank> netBottomRank = new NetworkVariable<Rank>();

    [Header("Deck Data")]
    [SerializeField] public List<Card> cards = new List<Card>();
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private Transform drawSpawnPoint;
    [SerializeField] private float dealSpeed = 0.15f;

    [Header("Thickness & Visuals")]
    [Tooltip("The Cube mesh representing the body of the deck")]
    [SerializeField] private Transform deckMesh;
    [Tooltip("The SpriteRenderer on the Quad that acts as the bottom face")]
    [SerializeField] private SpriteRenderer faceRenderer;
    [Tooltip("How tall the deck is when it has 52 cards")]
    [SerializeField] private float maxDeckHeight = 0.2f;

    public int cardCount = 0;

    public List<Card> GetCards() { return cards; }

    public override void OnNetworkSpawn() {
        netCardCount.OnValueChanged += (oldVal, newVal) => UpdateDeckVisuals();
        netBottomSuit.OnValueChanged += (oldVal, newVal) => UpdateDeckVisuals();
        netBottomRank.OnValueChanged += (oldVal, newVal) => UpdateDeckVisuals();

        if (IsServer) {
            int targetDecks = GoFishSettings.DeckCount > 0 ? GoFishSettings.DeckCount : 1;
            CreateStandardDeck(targetDecks);
            Shuffle();
        }

        UpdateDeckVisuals();
    }

    private void UpdateServerDeckState() {
        if (!IsServer) return;

        netCardCount.Value = cards.Count;
        cardCount = cards.Count;

        if (cards.Count > 0) {
            Card bottomCard = cards[cards.Count - 1];
            netBottomSuit.Value = bottomCard.suit;
            netBottomRank.Value = bottomCard.rank;
        }
    }

    private void UpdateDeckVisuals() {
        if (deckMesh != null && faceRenderer != null) {
            // Get the visual and physics components to soft-hide them
            MeshRenderer meshRend = deckMesh.GetComponent<MeshRenderer>();
            Collider meshColl = deckMesh.GetComponent<Collider>();
            Collider rootColl = GetComponent<Collider>(); // In case the collider is on the root object

            if (netCardCount.Value <= 0) {
                // SOFT HIDE: Turn off visuals and physical interactions, but leave the script ALIVE
                if (meshRend != null) meshRend.enabled = false;
                if (meshColl != null) meshColl.enabled = false;
                if (rootColl != null) rootColl.enabled = false;
                faceRenderer.enabled = false;
            } else {
                // TURN BACK ON
                if (meshRend != null) meshRend.enabled = true;
                if (meshColl != null) meshColl.enabled = true;
                if (rootColl != null) rootColl.enabled = true;
                faceRenderer.enabled = true;

                float heightPercent = Mathf.Clamp01((float)netCardCount.Value / 52f);
                float currentHeight = Mathf.Max(0.01f, maxDeckHeight * heightPercent);
                deckMesh.localScale = new Vector3(deckMesh.localScale.x, currentHeight, deckMesh.localScale.z);

                string resourceName = $"Cards/{netBottomSuit.Value}_{netBottomRank.Value}";
                Sprite loadedFace = Resources.Load<Sprite>(resourceName);
                if (loadedFace != null) {
                    faceRenderer.sprite = loadedFace;
                }
            }
        }
    }

    public void InitializeWithCards(List<Card> initialCards) {
        if (cards == null) cards = new List<Card>();
        cards.Clear();
        cards.AddRange(initialCards);
        UpdateServerDeckState();
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
        UpdateServerDeckState();
    }

    public void RequestDrawCard() {
        if (GameManager.Instance.MyHand == null) return;

        int seatIndex = GameManager.Instance.allSeats.IndexOf(GameManager.Instance.MyHand);
        DrawCardServerRpc(seatIndex);
    }

    [ServerRpc(RequireOwnership = false)]
    private void DrawCardServerRpc(int targetSeatIndex, ServerRpcParams rpcParams = default) {
        if (cards.Count == 0) return;

        Card topCardData = cards[0];
        cards.RemoveAt(0);
        UpdateServerDeckState();

        GameObject newCardObj = Instantiate(cardPrefab, drawSpawnPoint.position, Quaternion.identity);
        NetworkObject netObj = newCardObj.GetComponent<NetworkObject>();

        netObj.SpawnWithOwnership(rpcParams.Receive.SenderClientId);

        SetCardDataClientRpc(netObj.NetworkObjectId, topCardData.suit, topCardData.rank, targetSeatIndex);
    }

    public Card ServerDrawCard(PlayerHand targetHand) {
        if (!IsServer || cards.Count == 0 || targetHand == null) return null;

        Card topCardData = cards[0];
        cards.RemoveAt(0);
        UpdateServerDeckState();

        GameObject newCardObj = Instantiate(cardPrefab, drawSpawnPoint.position, Quaternion.identity);
        NetworkObject netObj = newCardObj.GetComponent<NetworkObject>();
        NetworkObject handNetObj = targetHand.GetComponent<NetworkObject>();

        netObj.SpawnWithOwnership(handNetObj.OwnerClientId);

        int seatIndex = GameManager.Instance.allSeats.IndexOf(targetHand);
        SetCardDataClientRpc(netObj.NetworkObjectId, topCardData.suit, topCardData.rank, seatIndex);

        return topCardData;
    }

    [ClientRpc]
    private void SetCardDataClientRpc(ulong cardNetworkId, Suit suit, Rank rank, int targetSeatIndex) {
        StartCoroutine(WaitAndAssignCard(cardNetworkId, suit, rank, targetSeatIndex));
    }

    private IEnumerator WaitAndAssignCard(ulong cardNetworkId, Suit suit, Rank rank, int targetSeatIndex) {
        NetworkObject cardNetObj = null;
        float timeout = 3.0f;

        while (timeout > 0) {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cardNetworkId, out cardNetObj)) {
                break;
            }
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (cardNetObj != null) {
            CardView newCardView = cardNetObj.GetComponent<CardView>();
            if (newCardView != null) {
                newCardView.SetCardData(new Card(suit, rank));
                cardNetObj.gameObject.tag = "MoveableObject";
            }

            if (targetSeatIndex >= 0 && targetSeatIndex < GameManager.Instance.allSeats.Count) {
                PlayerHand targetHand = GameManager.Instance.allSeats[targetSeatIndex];
                if (targetHand != null) {
                    targetHand.gameObject.SetActive(true);
                    targetHand.AddCard(newCardView);
                }
            }
        } else {
            Debug.LogError($"<color=red>[Deck]</color> ClientRpc timed out! Card {cardNetworkId} never spawned on Client.");
        }
    }

    public void AddCard(Card card) {
        cards.Add(card);
        UpdateServerDeckState();
    }

    public void Shuffle() {
        if (!IsServer) return;
        for (int i = 0; i < cards.Count; i++) {
            int rand = Random.Range(i, cards.Count);
            (cards[i], cards[rand]) = (cards[rand], cards[i]);
        }
        UpdateServerDeckState();
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
        UpdateServerDeckState();

        float offsetDistance = cardPrefab.gameObject.transform.localScale.x * 1.1f;
        Vector3 finalSpawnPos = transform.position + (Vector3.up * 0.2f);

        GameObject newCardGO = Instantiate(cardPrefab, finalSpawnPos, Quaternion.identity);
        NetworkObject netObj = newCardGO.GetComponent<NetworkObject>();

        netObj.Spawn();
        SetTopCardDataClientRpc(netObj.NetworkObjectId, topCardData.suit, topCardData.rank);
    }

    [ClientRpc]
    private void SetTopCardDataClientRpc(ulong cardNetworkId, Suit suit, Rank rank) {
        StartCoroutine(WaitAndAssignTopCard(cardNetworkId, suit, rank));
    }

    private IEnumerator WaitAndAssignTopCard(ulong cardNetworkId, Suit suit, Rank rank) {
        NetworkObject cardNetObj = null;
        float timeout = 3.0f;

        while (timeout > 0) {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cardNetworkId, out cardNetObj)) {
                break;
            }
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (cardNetObj != null) {
            CardView cv = cardNetObj.GetComponent<CardView>();
            if (cv != null) cv.SetCardData(new Card(suit, rank));

            if (cardNetObj.TryGetComponent<Rigidbody>(out var rb)) {
                rb.isKinematic = false;
            }
        }
    }
}