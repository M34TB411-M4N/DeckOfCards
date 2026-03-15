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
    [SerializeField] private Transform deckMesh;
    [SerializeField] private SpriteRenderer faceRenderer;
    [SerializeField] private float maxDeckHeight = 0.2f;

    public int cardCount = 0;

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

                float heightPercent = Mathf.Clamp01((float)netCardCount.Value / 52f);
                float currentHeight = Mathf.Max(0.01f, maxDeckHeight * heightPercent);
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

    private int GetCorrectDrawIndex() {
        bool isFaceUp = (transform.eulerAngles.z < 90f || transform.eulerAngles.z > 270f);
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
        
        // THE FIX: Assign the values BEFORE spawning!
        CardView cv = newCardObj.GetComponent<CardView>();
        cv.netSuit.Value = topCardData.suit;
        cv.netRank.Value = topCardData.rank;
        cv.netTargetHand.Value = targetSeatIndex;

        // Now spawn it. The payload will safely contain all the data.
        NetworkObject netObj = newCardObj.GetComponent<NetworkObject>();
        netObj.SpawnWithOwnership(rpcParams.Receive.SenderClientId);
    }

    public Card ServerDrawCard(PlayerHand targetHand) {
        if (!IsServer || cards.Count == 0 || targetHand == null) return null;

        int drawIndex = GetCorrectDrawIndex();
        Card topCardData = cards[drawIndex];
        cards.RemoveAt(drawIndex);
        UpdateServerDeckState();

        GameObject newCardObj = Instantiate(cardPrefab, drawSpawnPoint.position, transform.rotation);
        
        int seatIndex = GameManager.Instance.allSeats.IndexOf(targetHand);

        // THE FIX: Assign the values BEFORE spawning!
        CardView cv = newCardObj.GetComponent<CardView>();
        cv.netSuit.Value = topCardData.suit;
        cv.netRank.Value = topCardData.rank;
        cv.netTargetHand.Value = seatIndex;

        NetworkObject netObj = newCardObj.GetComponent<NetworkObject>();
        NetworkObject handNetObj = targetHand.GetComponent<NetworkObject>();
        netObj.SpawnWithOwnership(handNetObj.OwnerClientId);

        return topCardData;
    }

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
        
        // THE FIX: Assign the values BEFORE spawning!
        CardView cv = newCardGO.GetComponent<CardView>();
        cv.netSuit.Value = topCardData.suit;
        cv.netRank.Value = topCardData.rank;
        cv.netTargetHand.Value = -1; // Throw it on the table

        NetworkObject netObj = newCardGO.GetComponent<NetworkObject>();
        netObj.Spawn();

        if (newCardGO.TryGetComponent<Rigidbody>(out var rb)) {
            rb.isKinematic = false;
        }
    }

    public void RemoveTopCard() { RemoveTopCardServerRpc(); }

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
}