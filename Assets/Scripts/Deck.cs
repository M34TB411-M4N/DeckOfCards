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
    public GameObject cardPrefab;
    [SerializeField] private Transform drawSpawnPoint;
    [SerializeField] private float dealSpeed = 0.15f;

    [Header("Thickness & Visuals")]
    [SerializeField] private Transform deckMesh;
    [SerializeField] private SpriteRenderer faceRenderer;
    [SerializeField] private float singleCardThickness = 0.0144f;

    public int cardCount = 0;

    void Awake() {
        if (TryGetComponent<Rigidbody>(out var rb)) {
            rb.isKinematic = true;
            rb.useGravity = true; // GRAVITY FIX: Restored to true!
        }
    }

    public List<Card> GetCards() { return cards; }

    public override void OnNetworkSpawn() {
        netCardCount.OnValueChanged += (oldVal, newVal) => UpdateDeckVisuals();
        netBottomSuit.OnValueChanged += (oldVal, newVal) => UpdateDeckVisuals();
        netBottomRank.OnValueChanged += (oldVal, newVal) => UpdateDeckVisuals();

        if (IsServer) {
            int targetDecks = GameSessionData.DeckCount > 0 ? GameSessionData.DeckCount : 1;
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
            MeshRenderer meshRend = deckMesh.GetComponent<MeshRenderer>();
            Collider meshColl = deckMesh.GetComponent<Collider>();
            Collider rootColl = GetComponent<Collider>();

            if (netCardCount.Value <= 0) {
                if (meshRend != null) meshRend.enabled = false;
                if (meshColl != null) meshColl.enabled = false;
                if (rootColl != null) rootColl.enabled = false;
                faceRenderer.enabled = false;
            } else {
                if (meshRend != null) meshRend.enabled = true;
                if (meshColl != null) meshColl.enabled = true;
                if (rootColl != null) rootColl.enabled = true;
                faceRenderer.enabled = true;

                float currentHeight = Mathf.Max(0.001f, netCardCount.Value * singleCardThickness);
                deckMesh.localScale = new Vector3(deckMesh.localScale.x, currentHeight, deckMesh.localScale.z);

                string resourceName = $"CardFaces/{netBottomSuit.Value}_{netBottomRank.Value}";
                Sprite loadedFace = Resources.Load<Sprite>(resourceName);
                if (loadedFace != null) faceRenderer.sprite = loadedFace;
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

    public void RequestAbsorbCard(ulong cardNetworkId) {
        AbsorbCardServerRpc(cardNetworkId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void AbsorbCardServerRpc(ulong cardNetworkId) {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cardNetworkId, out NetworkObject cardNetObj)) {
            CardView cv = cardNetObj.GetComponent<CardView>();
            if (cv != null) {
                cards.Add(new Card(cv.netSuit.Value, cv.netRank.Value));
                UpdateServerDeckState();
                cardNetObj.Despawn();
            }
        }
    }

    public void AddCard(Card card) {
        cards.Add(card);
        UpdateServerDeckState();
    }

    private int GetCorrectDrawIndex() {
        bool isFaceUp = (transform.eulerAngles.z > 90f && transform.eulerAngles.z < 270f);
        return isFaceUp ? (cards.Count - 1) : 0;
    }

    public void RequestDrawCard() {
        if (GameManager.Instance.MyHand == null) return;
        int seatIndex = GameManager.Instance.allSeats.IndexOf(GameManager.Instance.MyHand);
        DrawCardServerRpc(seatIndex);
    }

    [ServerRpc(RequireOwnership = false)]
    private void DrawCardServerRpc(int targetSeatIndex, ServerRpcParams rpcParams = default) {
        if (cards.Count == 0) return;

        int drawIndex = GetCorrectDrawIndex();
        Card topCardData = cards[drawIndex];
        cards.RemoveAt(drawIndex);
        UpdateServerDeckState();

        GameObject newCardObj = Instantiate(cardPrefab, drawSpawnPoint.position, transform.rotation);
        NetworkObject netObj = newCardObj.GetComponent<NetworkObject>();

        netObj.SpawnWithOwnership(rpcParams.Receive.SenderClientId);

        CardView cv = newCardObj.GetComponent<CardView>();
        cv.netSuit.Value = topCardData.suit;
        cv.netRank.Value = topCardData.rank;
        cv.netTargetHand.Value = targetSeatIndex;
    }

    public Card ServerDrawCard(PlayerHand targetHand) {
        if (!IsServer || cards.Count == 0 || targetHand == null) return null;

        int drawIndex = GetCorrectDrawIndex();
        Card topCardData = cards[drawIndex];
        cards.RemoveAt(drawIndex);
        UpdateServerDeckState();

        GameObject newCardObj = Instantiate(cardPrefab, drawSpawnPoint.position, transform.rotation);

        if (newCardObj.TryGetComponent<Collider>(out var col)) col.enabled = false;
        if (newCardObj.TryGetComponent<Rigidbody>(out var rb)) {
            rb.isKinematic = true;
            rb.useGravity = true; // GRAVITY FIX: Restored to true
            rb.linearVelocity = Vector3.zero;
        }

        CardView cv = newCardObj.GetComponent<CardView>();
        int seatIndex = GameManager.Instance.allSeats.IndexOf(targetHand);
        cv.netSuit.Value = topCardData.suit;
        cv.netRank.Value = topCardData.rank;
        cv.netTargetHand.Value = seatIndex;

        NetworkObject netObj = newCardObj.GetComponent<NetworkObject>();
        NetworkObject handNetObj = targetHand.GetComponent<NetworkObject>();

        netObj.SpawnWithOwnership(handNetObj.OwnerClientId);

        return topCardData;
    }

    public void RemoveTopCard() { RemoveTopCardServerRpc(); }

    [ServerRpc(RequireOwnership = false)]
    private void RemoveTopCardServerRpc() {
        if (cards == null || cards.Count == 0) return;

        int drawIndex = GetCorrectDrawIndex();
        Card topCardData = cards[drawIndex];
        cards.RemoveAt(drawIndex);
        UpdateServerDeckState();

        float offsetDistance = cardPrefab.gameObject.transform.localScale.x * 1.1f;
        Vector3 finalSpawnPos = transform.position + (Vector3.up * 0.2f);

        GameObject newCardGO = Instantiate(cardPrefab, finalSpawnPos, transform.rotation);
        NetworkObject netObj = newCardGO.GetComponent<NetworkObject>();
        netObj.Spawn();

        CardView cv = newCardGO.GetComponent<CardView>();
        cv.netSuit.Value = topCardData.suit;
        cv.netRank.Value = topCardData.rank;
        cv.netTargetHand.Value = -1;

        if (newCardGO.TryGetComponent<Rigidbody>(out var rb)) {
            rb.isKinematic = true;
            rb.useGravity = true; // GRAVITY FIX: Restored to true
        }
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

        int activePlayerCount = NetworkManager.Singleton.ConnectedClientsIds.Count;

        for (int i = 0; i < count; i++) {
            for (int s = 0; s < activePlayerCount; s++) {
                if (s < GameManager.Instance.allSeats.Count) {
                    PlayerHand seat = GameManager.Instance.allSeats[s];

                    if (seat != null) {
                        ServerDrawCard(seat);
                        yield return new WaitForSeconds(dealSpeed);
                    }
                }
            }
        }
    }
}