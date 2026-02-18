using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class GoFishLobbyManager : MonoBehaviour {
    [Header("UI References")]
    public TMP_Dropdown playerDropdown;
    public TMP_Dropdown deckDropdown;
    public TMP_Dropdown modeDropdown;
    public TextMeshProUGUI warningText;
    public Button startButton;

    void Start() {
        // Initialize UI with default values
        playerDropdown.onValueChanged.AddListener(delegate { OnSettingsChanged(); });
        deckDropdown.onValueChanged.AddListener(delegate { OnSettingsChanged(); });
        modeDropdown.onValueChanged.AddListener(delegate { OnSettingsChanged(); });

        OnSettingsChanged();
    }

    public void OnSettingsChanged() {
        int players = playerDropdown.value + 2; // Value 0 = 2 players
        int decks = deckDropdown.value + 1;    // Value 0 = 1 deck

        // Rule: > 4 players requires at least 2 decks
        if (players > 4 && decks < 2) {
            warningText.text = "Error: More than 4 players requires at least 2 decks!";
            warningText.color = Color.red;
            startButton.interactable = false;
        } else {
            warningText.text = "Settings Valid";
            warningText.color = Color.green;
            startButton.interactable = true;
        }

        // Save to static class
        GoFishSettings.PlayerCount = players;
        GoFishSettings.DeckCount = decks;
        GoFishSettings.CurrentMode = (GoFishSettings.ScoringMode)modeDropdown.value;
    }

    public void StartGame() {
        // Replace "GoFish_Game" with your actual game scene name
        SceneManager.LoadScene("GoFishLobby");
    }
}