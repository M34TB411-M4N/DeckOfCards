using UnityEngine;
using TMPro; // Assuming you're using TextMeshPro
using System.Collections.Generic;

public class ScoreboardUI : MonoBehaviour {
    [SerializeField] private GameObject scoreboardPanel;
    [SerializeField] private GameObject scoreEntryPrefab; // A small UI prefab with Name and Score text
    [SerializeField] private GameObject entryContainer;   // A transform with a VerticalLayoutGroup

    void Start() {
        scoreboardPanel.SetActive(false); // Hide on start
    }

    public void ToggleScoreboard() {
        bool isActive = !scoreboardPanel.activeSelf;
        scoreboardPanel.SetActive(isActive);
        entryContainer.SetActive(isActive);

        if (isActive) {
            RefreshScores();
        }
    }

    private void RefreshScores() {
        // Clear old entries
        foreach (Transform child in entryContainer.transform) {
            Destroy(child.gameObject);
        }

        // Create new entries from GameManager data
        if (GameManager.Instance != null) {
            foreach (var playerData in GameManager.Instance.playerScores) {
                GameObject entry = Instantiate(scoreEntryPrefab, entryContainer.transform);
                // Entry setup (assuming entry has a script to set text)
                entry.GetComponent<ScoreEntry>().SetData(playerData.playerName, playerData.score);
            }
        }
    }
}