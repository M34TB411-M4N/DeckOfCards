using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class ScoreboardUI : MonoBehaviour {
    [SerializeField] private GameObject scoreboardPanel;
    [SerializeField] private GameObject scoreEntryPrefab;
    [SerializeField] private GameObject entryContainer;

    void Start() {
        scoreboardPanel.SetActive(false);

        // Listen for the global score update event
        if (GameManager.Instance != null) {
            GameManager.Instance.OnScoresUpdated += HandleScoreUpdate;
        }
    }

    void OnDestroy() {
        // Always clean up listeners when the object is destroyed
        if (GameManager.Instance != null) {
            GameManager.Instance.OnScoresUpdated -= HandleScoreUpdate;
        }
    }

    public void ToggleScoreboard() {
        bool isActive = !scoreboardPanel.activeSelf;
        scoreboardPanel.SetActive(isActive);
        entryContainer.SetActive(isActive);

        if (isActive) {
            RefreshScores();
        }
    }

    // Called automatically whenever a point is scored anywhere on the network
    private void HandleScoreUpdate() {
        // If the menu is currently open on my screen, refresh it instantly
        if (scoreboardPanel.activeSelf) {
            RefreshScores();
        }
    }

    private void RefreshScores() {
        foreach (Transform child in entryContainer.transform) {
            Destroy(child.gameObject);
        }

        if (GameManager.Instance != null) {
            foreach (var playerData in GameManager.Instance.playerScores) {
                GameObject entry = Instantiate(scoreEntryPrefab, entryContainer.transform);
                entry.GetComponent<ScoreEntry>().SetData(playerData.playerName, playerData.score);
            }
        }
    }
}