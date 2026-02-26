using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode; // Essential for Networking
using Unity.Collections;

public class GoFishLobbyManager : NetworkBehaviour { // Changed from MonoBehaviour
    [Header("UI References")]
    public TMP_Dropdown playerDropdown;
    public TMP_Dropdown deckDropdown;
    public TMP_Dropdown modeDropdown;
    public TextMeshProUGUI warningText;
    public Button startButton;

    // --- Network Variables ---
    // These sync automatically from Server to all Clients.
    // We store the 'index' of the dropdown to keep it simple.
    private NetworkVariable<int> netPlayerIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> netDeckIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> netModeIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn() {
        // 1. If I am a Client, disable the UI so I can't change the Host's settings
        if (!IsServer) {
            playerDropdown.interactable = false;
            deckDropdown.interactable = false;
            modeDropdown.interactable = false;
            startButton.gameObject.SetActive(false); // Hide start button for clients
            warningText.text = "Waiting for Host to finalize settings...";
        }

        // 2. Subscribe to network changes so UI updates when the Host moves a dropdown
        netPlayerIndex.OnValueChanged += (oldVal, newVal) => { playerDropdown.value = newVal; RefreshLocalSettings(); };
        netDeckIndex.OnValueChanged += (oldVal, newVal) => { deckDropdown.value = newVal; RefreshLocalSettings(); };
        netModeIndex.OnValueChanged += (oldVal, newVal) => { modeDropdown.value = newVal; RefreshLocalSettings(); };

        // 3. Set up the Host's listeners
        if (IsServer) {
            playerDropdown.onValueChanged.AddListener(OnHostUIChanged);
            deckDropdown.onValueChanged.AddListener(OnHostUIChanged);
            modeDropdown.onValueChanged.AddListener(OnHostUIChanged);
        }

        RefreshLocalSettings();
    }

    // Called only by the Host's UI interactions
    private void OnHostUIChanged(int _) {
        if (!IsServer) return;

        // Update the NetworkVariables - this sends the data to BlueStacks!
        netPlayerIndex.Value = playerDropdown.value;
        netDeckIndex.Value = deckDropdown.value;
        netModeIndex.Value = modeDropdown.value;

        RefreshLocalSettings();
    }

    // This handles the logic (Warning text, Static class saving) for everyone
    public void RefreshLocalSettings() {
        int players = playerDropdown.value + 2;
        int decks = deckDropdown.value + 1;

        if (players > 4 && decks < 2) {
            warningText.text = "Error: More than 4 players requires at least 2 decks!";
            warningText.color = Color.red;
            if (IsServer) startButton.interactable = false;
        } else {
            warningText.text = IsServer ? "Settings Valid" : "Host is configuring...";
            warningText.color = IsServer ? Color.green : Color.white;
            if (IsServer) startButton.interactable = true;
        }

        // Save to your static class so the next scene can read it
        GoFishSettings.PlayerCount = players;
        GoFishSettings.DeckCount = decks;
        GoFishSettings.CurrentMode = (GoFishSettings.ScoringMode)modeDropdown.value;
    }

    public void StartGame() {
        if (!IsServer) return; // Only the host can start

        // IMPORTANT: Use the Network Scene Manager to pull the Client with you!
        // Replace "GoFish_Table" with your actual playing scene name
        NetworkManager.Singleton.SceneManager.LoadScene("GoFishLobby", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
}