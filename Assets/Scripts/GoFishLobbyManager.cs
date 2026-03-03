using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Collections;
using System.Collections;

public class GoFishLobbyManager : NetworkBehaviour {
    [Header("Host Settings")]
    public TMP_Dropdown playerDropdown;
    public TMP_Dropdown deckDropdown;
    public TMP_Dropdown modeDropdown;
    public Button startButton;

    [Header("Client/Global UI")]
    public TextMeshProUGUI warningText;
    public GameObject playerEntryPrefab;
    public Transform entryContainer;
    public Button readyButton; // This should be a separate button in your UI

    private NetworkVariable<int> netPlayerIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> netDeckIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> netModeIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkList<LobbyPlayerState> lobbyPlayers;

    private void Awake() {
        lobbyPlayers = new NetworkList<LobbyPlayerState>();
    }

    public override void OnNetworkSpawn() {
        // 1. CLEAR OLD DATA (Critical for re-joins)
        if (IsServer) {
            lobbyPlayers.Clear();
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

            // Add Host
            AddPlayerState(NetworkManager.LocalClient.ClientId, PlayerPrefs.GetString("DisplayName", "Host"));

            playerDropdown.onValueChanged.AddListener(OnHostUIChanged);
            deckDropdown.onValueChanged.AddListener(OnHostUIChanged);
            modeDropdown.onValueChanged.AddListener(OnHostUIChanged);
        }

        // 2. UI VISIBILITY SETUP
        // Host: No ready button, yes settings.
        // Client: Yes ready button, no settings.
        if (IsServer) {
            readyButton.gameObject.SetActive(false);
            startButton.gameObject.SetActive(true);
        } else {
            readyButton.gameObject.SetActive(true);
            startButton.gameObject.SetActive(false);

            playerDropdown.interactable = false;
            deckDropdown.interactable = false;
            modeDropdown.interactable = false;

            readyButton.onClick.RemoveAllListeners();
            readyButton.onClick.AddListener(OnReadyClicked);
        }

        // 3. SYNC LISTENERS
        netPlayerIndex.OnValueChanged += (oldV, newV) => { playerDropdown.value = newV; RefreshLocalSettings(); };
        netDeckIndex.OnValueChanged += (oldV, newV) => { deckDropdown.value = newV; RefreshLocalSettings(); };
        netModeIndex.OnValueChanged += (oldV, newV) => { modeDropdown.value = newV; RefreshLocalSettings(); };

        lobbyPlayers.OnListChanged += (changeEvent) => UpdateLobbyUI();

        // 4. THE FIX: WAIT FOR SERVER TO REGISTER US
        if (IsClient) {
            StartCoroutine(ClientHandshakeRoutine());
            // Force pull current values immediately
            playerDropdown.value = netPlayerIndex.Value;
            deckDropdown.value = netDeckIndex.Value;
            modeDropdown.value = netModeIndex.Value;
        }

        RefreshLocalSettings();
        UpdateLobbyUI();
    }

    private IEnumerator ClientHandshakeRoutine() {
        // Wait until the Server actually adds our ClientId to the NetworkList
        bool foundMe = false;
        while (!foundMe) {
            foreach (var p in lobbyPlayers) {
                if (p.ClientId == NetworkManager.LocalClient.ClientId) {
                    foundMe = true;
                    break;
                }
            }
            yield return new WaitForSeconds(0.2f);
        }

        // Now that the Server knows we exist, send the name!
        string myName = PlayerPrefs.GetString("DisplayName", "Player");
        UpdatePlayerNameServerRpc(myName);
    }

    [ServerRpc(RequireOwnership = false)]
    private void UpdatePlayerNameServerRpc(string newName, ServerRpcParams rpcParams = default) {
        ulong clientId = rpcParams.Receive.SenderClientId;
        for (int i = 0; i < lobbyPlayers.Count; i++) {
            if (lobbyPlayers[i].ClientId == clientId) {
                var state = lobbyPlayers[i];
                state.PlayerName = newName;
                lobbyPlayers[i] = state;
                Debug.Log($"<color=green>[Lobby]</color> Updated Name for ID {clientId} to {newName}");
                return;
            }
        }
    }

    private void HandleClientConnected(ulong clientId) {
        if (!IsServer) return;
        Debug.Log($"<color=cyan>[Lobby]</color> Client {clientId} connected. Adding to list.");
        AddPlayerState(clientId, "Joining...");
    }

    private void HandleClientDisconnected(ulong clientId) {
        for (int i = 0; i < lobbyPlayers.Count; i++) {
            if (lobbyPlayers[i].ClientId == clientId) {
                lobbyPlayers.RemoveAt(i);
                break;
            }
        }
    }

    private void AddPlayerState(ulong clientId, string playerName) {
        lobbyPlayers.Add(new LobbyPlayerState {
            ClientId = clientId,
            PlayerName = playerName,
            IsReady = (clientId == 0) // Host is always ready
        });
    }

    [ServerRpc(RequireOwnership = false)]
    private void ToggleReadyServerRpc(ServerRpcParams rpcParams = default) {
        ulong clientId = rpcParams.Receive.SenderClientId;
        for (int i = 0; i < lobbyPlayers.Count; i++) {
            if (lobbyPlayers[i].ClientId == clientId) {
                var state = lobbyPlayers[i];
                state.IsReady = !state.IsReady;
                lobbyPlayers[i] = state;
                break;
            }
        }
    }

    private void UpdateLobbyUI() {
        foreach (Transform child in entryContainer) Destroy(child.gameObject);

        bool allReady = true;
        int clientCount = 0;

        foreach (var player in lobbyPlayers) {
            if (player.ClientId == 0) continue;

            clientCount++;
            GameObject entry = Instantiate(playerEntryPrefab, entryContainer);
            entry.GetComponent<LobbyPlayerEntry>().Setup(player.PlayerName.ToString(), player.IsReady);

            if (!player.IsReady) allReady = false;
        }

        if (IsServer) {
            startButton.interactable = allReady && clientCount > 0;
        }
    }

    private void OnHostUIChanged(int _) {
        if (!IsServer) return;
        netPlayerIndex.Value = playerDropdown.value;
        netDeckIndex.Value = deckDropdown.value;
        netModeIndex.Value = modeDropdown.value;
        RefreshLocalSettings();
    }

    public void RefreshLocalSettings() {
        int players = playerDropdown.value + 2;
        int decks = deckDropdown.value + 1;

        if (players > 4 && decks < 2) {
            warningText.text = "Error: 5+ players needs 2+ decks!";
            warningText.color = Color.red;
            if (IsServer) startButton.interactable = false;
        } else {
            warningText.text = IsServer ? "Settings Valid" : "Waiting for Host...";
            warningText.color = IsServer ? Color.green : Color.white;
        }

        GoFishSettings.PlayerCount = players;
        GoFishSettings.DeckCount = decks;
        GoFishSettings.CurrentMode = (GoFishSettings.ScoringMode)modeDropdown.value;
    }

    private void OnReadyClicked() => ToggleReadyServerRpc();

    public void StartGame() {
        if (!IsServer) return;
        NetworkManager.Singleton.SceneManager.LoadScene("GoFishLobby", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
}