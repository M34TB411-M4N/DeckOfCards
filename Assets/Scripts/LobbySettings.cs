using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Collections;
using System.Collections;

public class LobbySettings : NetworkBehaviour {
    [Header("Relay UI")]
    [Tooltip("Drag the TextMeshPro text here that will display the Join Code!")]
    public TextMeshProUGUI joinCodeTextDisplay;

    [Header("Host Settings UI")]
    public TMP_Dropdown playerDropdown;

    [Tooltip("Drag the parent GameObject of the Deck Dropdown")]
    public GameObject deckSettingUI;
    public TMP_Dropdown deckDropdown;

    [Tooltip("Drag the parent GameObject of the Mode Dropdown")]
    public GameObject modeSettingUI;
    public TMP_Dropdown modeDropdown;

    public Button startButton;

    [Header("Global UI")]
    public TMP_InputField nameInputField;
    public TextMeshProUGUI warningText;
    public GameObject playerEntryPrefab;
    public Transform entryContainer;
    public Button readyButton;

    private NetworkVariable<int> netGameMode = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> netPlayerIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> netDeckIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> netModeIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkList<LobbyPlayerState> lobbyPlayers;
    private bool hasSetInitialName = false;

    private void Awake() {
        lobbyPlayers = new NetworkList<LobbyPlayerState>();
    }

    public override void OnNetworkSpawn() {
        // --- DISPLAY THE RELAY CODE ---
        if (joinCodeTextDisplay != null) {
            if (IsServer) {
                joinCodeTextDisplay.text = "JOIN CODE: " + GameSessionData.RelayJoinCode;
            } else {
                joinCodeTextDisplay.text = "Connected via Relay!";
            }
        }
        // ------------------------------

        if (IsServer) {
            lobbyPlayers.Clear();
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

            netGameMode.Value = (int)GameSessionData.SelectedMode;
            AddPlayerState(NetworkManager.LocalClient.ClientId, "Host");

            netPlayerIndex.Value = playerDropdown.value;
            netDeckIndex.Value = deckDropdown.value;
            netModeIndex.Value = modeDropdown.value;

            playerDropdown.onValueChanged.AddListener(OnHostUIChanged);
            deckDropdown.onValueChanged.AddListener(OnHostUIChanged);
            modeDropdown.onValueChanged.AddListener(OnHostUIChanged);
        }

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

        if (nameInputField != null) {
            nameInputField.onEndEdit.AddListener(OnNameInputChanged);
            nameInputField.onSelect.AddListener(OnNameFieldSelected);
        }

        netGameMode.OnValueChanged += (oldV, newV) => UpdateGameModeUI(newV);
        netPlayerIndex.OnValueChanged += (oldV, newV) => { playerDropdown.value = newV; RefreshDataVault(); EvaluateStartConditions(); };
        netDeckIndex.OnValueChanged += (oldV, newV) => { deckDropdown.value = newV; RefreshDataVault(); EvaluateStartConditions(); };
        netModeIndex.OnValueChanged += (oldV, newV) => { modeDropdown.value = newV; RefreshDataVault(); };

        lobbyPlayers.OnListChanged += (changeEvent) => { UpdateLobbyUI(); EvaluateStartConditions(); };

        UpdateGameModeUI(netGameMode.Value);
        RefreshDataVault();
        UpdateLobbyUI();
        EvaluateStartConditions();
    }

    private void UpdateGameModeUI(int modeValue) {
        GameMode currentMode = (GameMode)modeValue;

        if (deckSettingUI != null) deckSettingUI.SetActive(false);
        if (modeSettingUI != null) modeSettingUI.SetActive(false);

        // Ensure player dropdown is normally interactable for Sandbox
        if (playerDropdown != null && IsServer) playerDropdown.interactable = true;

        switch (currentMode) {
            case GameMode.GoFish:
                if (deckSettingUI != null) deckSettingUI.SetActive(true);
                if (modeSettingUI != null) modeSettingUI.SetActive(true);
                break;
            case GameMode.Cribbage:
                // THE FIX: Lock the lobby to 2 players and hide extra rules
                if (playerDropdown != null && IsServer) {
                    playerDropdown.value = 0; // Assuming index 0 equals 2 players
                    playerDropdown.interactable = false; // Lock it!
                }
                break;
            case GameMode.Sandbox:
                break;
        }
    }

    private void OnNameFieldSelected(string currentText) {
        if (currentText.StartsWith("Host") || currentText.StartsWith("Player")) {
            nameInputField.text = "";
        }
    }

    private void OnNameInputChanged(string newName) {
        if (string.IsNullOrWhiteSpace(newName)) {
            hasSetInitialName = false;
            UpdateLobbyUI();
            return;
        }

        GameSessionData.PlayerName = newName;
        UpdatePlayerNameServerRpc(newName);
    }

    [ServerRpc(RequireOwnership = false)]
    private void UpdatePlayerNameServerRpc(string newName, ServerRpcParams rpcParams = default) {
        ulong clientId = rpcParams.Receive.SenderClientId;
        for (int i = 0; i < lobbyPlayers.Count; i++) {
            if (lobbyPlayers[i].ClientId == clientId) {
                var state = lobbyPlayers[i];
                state.PlayerName = newName;
                lobbyPlayers[i] = state;
                return;
            }
        }
    }

    private void HandleClientConnected(ulong clientId) {
        if (!IsServer) return;
        string defaultName = $"Player {lobbyPlayers.Count + 1}";
        AddPlayerState(clientId, defaultName);
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
            IsReady = (clientId == 0)
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

        foreach (var player in lobbyPlayers) {
            if (!hasSetInitialName && nameInputField != null && player.ClientId == NetworkManager.Singleton.LocalClientId) {
                nameInputField.text = player.PlayerName.ToString();
                GameSessionData.PlayerName = player.PlayerName.ToString();
                hasSetInitialName = true;
            }

            if (player.ClientId == 0) continue;
            GameObject entry = Instantiate(playerEntryPrefab, entryContainer);
            entry.GetComponent<LobbyPlayerEntry>().Setup(player.PlayerName.ToString(), player.IsReady);
        }
    }

    private void OnHostUIChanged(int _) {
        if (!IsServer) return;
        netPlayerIndex.Value = playerDropdown.value;
        netDeckIndex.Value = deckDropdown.value;
        netModeIndex.Value = modeDropdown.value;
    }

    public void RefreshDataVault() {
        GameSessionData.PlayerCount = playerDropdown.value + 2;
        GameSessionData.DeckCount = deckDropdown.value + 1;
        GameSessionData.ScoringMode = modeDropdown.value;
    }

    private void EvaluateStartConditions() {
        int targetPlayers = playerDropdown.value + 2;
        int currentPlayers = lobbyPlayers.Count;
        int decks = deckDropdown.value + 1;

        // THE FIX: Force target players to 2 if Cribbage
        if (netGameMode.Value == (int)GameMode.Cribbage) {
            targetPlayers = 2;
        }

        bool allReady = true;
        foreach (var player in lobbyPlayers) {
            if (!player.IsReady) allReady = false;
        }

        bool isLobbyFull = (currentPlayers == targetPlayers);
        bool settingsValid = true;

        if (netGameMode.Value == (int)GameMode.GoFish) {
            if (targetPlayers > 4 && decks < 2) settingsValid = false;
        }

        if (IsServer) {
            if (!settingsValid) {
                warningText.text = "Error: 5+ players needs 2+ decks!";
                warningText.color = Color.red;
            } else if (!isLobbyFull) {
                warningText.text = $"Waiting for players... ({currentPlayers}/{targetPlayers})";
                warningText.color = Color.yellow;
            } else if (!allReady) {
                warningText.text = "Waiting for players to ready up...";
                warningText.color = Color.yellow;
            } else {
                warningText.text = "All Ready! Press Start.";
                warningText.color = Color.green;
            }

            startButton.interactable = (settingsValid && isLobbyFull && allReady);
        } else {
            warningText.text = (isLobbyFull && allReady) ? "Waiting for Host to start..." : "Waiting for players to ready...";
            warningText.color = (isLobbyFull && allReady) ? Color.green : Color.white;
        }
    }   

    private void OnReadyClicked() => ToggleReadyServerRpc();

    public void StartGame() {
        if (!IsServer) return;

        if (netGameMode.Value == (int)GameMode.GoFish) {
            NetworkManager.Singleton.SceneManager.LoadScene("GoFishLobby", UnityEngine.SceneManagement.LoadSceneMode.Single);
        } else if (netGameMode.Value == (int)GameMode.Cribbage) {
            // THE FIX: Launch the Cribbage scene!
            NetworkManager.Singleton.SceneManager.LoadScene("CribbageLobby", UnityEngine.SceneManagement.LoadSceneMode.Single);
        } else {
            NetworkManager.Singleton.SceneManager.LoadScene("Table", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }
}