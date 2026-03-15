using UnityEngine;
using Unity.Netcode;

public class CardView : NetworkBehaviour {
    [Header("Network Data")]
    public NetworkVariable<Suit> netSuit = new NetworkVariable<Suit>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<Rank> netRank = new NetworkVariable<Rank>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> netTargetHand = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Data")]
    [SerializeField] private Card card;
    [SerializeField] private Rank rank;
    [SerializeField] private Suit suit;

    [Header("Visuals")]
    public SpriteRenderer faceRenderer;
    public Sprite hiddenFaceSprite;

    [Header("Prefabs")]
    [SerializeField] private GameObject pilePrefab;

    private bool currentlyHidden = true;

    public override void OnNetworkSpawn() {
        // Instantly apply data the moment it spawns on the client
        SyncFromNetwork();
        HandleHandAssignment();

        // Listen for future changes
        netSuit.OnValueChanged += (oldVal, newVal) => SyncFromNetwork();
        netRank.OnValueChanged += (oldVal, newVal) => SyncFromNetwork();
        netTargetHand.OnValueChanged += (oldVal, newVal) => HandleHandAssignment();
    }

    private void SyncFromNetwork() {
        this.suit = netSuit.Value;
        this.rank = netRank.Value;
        this.card = new Card(suit, rank);
        UpdateVisuals();
    }

    // Automatically jumps into the correct hand based on the NetworkVariable!
    private void HandleHandAssignment() {
        if (netTargetHand.Value >= 0 && GameManager.Instance != null) {
            if (netTargetHand.Value < GameManager.Instance.allSeats.Count) {
                PlayerHand hand = GameManager.Instance.allSeats[netTargetHand.Value];
                if (hand != null && !hand.cardsInHand.Contains(this)) {
                    gameObject.tag = "MoveableObject";
                    hand.gameObject.SetActive(true);
                    hand.AddCard(this);
                }
            }
        }
    }

    public Card GetCardData() { return card; }

    public void SetCardData(Card card) {
        this.card = card;
        this.rank = card.rank;
        this.suit = card.suit;
        UpdateVisuals();
    }

    void Update() {
        if (GoFishManager.Instance == null || GameManager.Instance == null) return;

        bool shouldBeHidden = true;
        if (GameManager.Instance.MyHand != null && GameManager.Instance.MyHand.cardsInHand.Contains(this)) {
            shouldBeHidden = false;
        }

        if (shouldBeHidden != currentlyHidden) {
            currentlyHidden = shouldBeHidden;
            UpdateVisuals();
        }
    }

    void UpdateVisuals() {
        if (faceRenderer == null) return;
        if (card == null) return;

        if (currentlyHidden && GoFishManager.Instance != null) {
            if (hiddenFaceSprite != null) {
                faceRenderer.sprite = hiddenFaceSprite;
                faceRenderer.color = Color.white;
            } else {
                Sprite loadedPlaceholder = Resources.Load<Sprite>("CardFacePlaceholder");
                if (loadedPlaceholder != null) {
                    faceRenderer.sprite = loadedPlaceholder;
                    faceRenderer.color = Color.white;
                } else {
                    faceRenderer.sprite = null;
                    faceRenderer.color = Color.black;
                }
            }
        } else {
            faceRenderer.color = Color.white;
            string resourceName = $"CardFaces/{card.suit}_{card.rank}";
            Sprite loadedFace = Resources.Load<Sprite>(resourceName);

            if (loadedFace != null) {
                faceRenderer.sprite = loadedFace;
            } else {
                Debug.LogError($"CardView: Could not find image at Resources/{resourceName}");
            }
        }
    }

    public void OnClicked() { }

    public void Flip() {
        if (IsServer) FlipClientRpc();
        else if (IsClient) FlipServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void FlipServerRpc() => FlipClientRpc();

    [ClientRpc]
    private void FlipClientRpc() => transform.Rotate(0f, 0f, 180f, Space.Self);

    public void ConvertToPile() {
        if (IsServer) ExecuteConvertToPile();
        else if (IsClient) ConvertToPileServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ConvertToPileServerRpc() => ExecuteConvertToPile();

    private void ExecuteConvertToPile() {
        Card cardData = GetCardData();
        Vector3 spawnPos = transform.position;
        Quaternion spawnRot = transform.rotation;

        PlayerHand[] allHands = Object.FindObjectsByType<PlayerHand>(FindObjectsSortMode.None);
        foreach (var hand in allHands) {
            if (hand.cardsInHand.Contains(this)) {
                hand.RemoveCard(this);
                break;
            }
        }

        GameObject newPileGO = Instantiate(pilePrefab, spawnPos, spawnRot);
        newPileGO.tag = "MoveableObject";

        NetworkObject netObj = newPileGO.GetComponent<NetworkObject>();
        if (netObj != null) netObj.Spawn();

        Pile newPile = newPileGO.GetComponent<Pile>();
        if (newPile != null) {
            var cardList = new System.Collections.Generic.List<Card> { cardData };
            newPile.InitializeWithCards(cardList);
        }

        if (TryGetComponent<NetworkObject>(out var myNetObj)) myNetObj.Despawn();
        else Destroy(gameObject);
    }
}