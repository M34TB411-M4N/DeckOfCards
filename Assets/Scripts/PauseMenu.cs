using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour {
    [Header("UI Panels")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject rulesPanel; // If you have a separate rules sub-menu

    [Header("Controllers to Disable")]
    [SerializeField] private ObjectSelect objectSelect;

    private bool isPaused = false;

    void Start() {
        // Ensure the menu is hidden on start
        if (pausePanel != null) pausePanel.SetActive(false);
        if (rulesPanel != null) rulesPanel.SetActive(false);
    }

    public void TogglePause() {
        isPaused = !isPaused;

        if (isPaused) {
            PauseGame();
        } else {
            ResumeGame();
        }
    }

    private void PauseGame() {
        pausePanel.SetActive(true);
        Time.timeScale = 0f; // Freezes physics and FixedUpdate

        if (objectSelect != null) objectSelect.enabled = false;
    }

    public void ResumeGame() {
        pausePanel.SetActive(false);
        if (rulesPanel != null) rulesPanel.SetActive(false);

        Time.timeScale = 1f;
        if (objectSelect != null) objectSelect.enabled = true;

        isPaused = false;
    }

    // --- Button Actions ---

    public void OnAddDeckPressed() {
        // We can implement the logic to spawn a new Deck prefab here later
        Debug.Log("Add Deck logic goes here.");
        // ResumeGame(); // Optional: close menu after adding?
    }

    public void OnViewRulesPressed() {
        if (rulesPanel != null) {
            rulesPanel.SetActive(true);
            pausePanel.SetActive(false);
        }
    }

    public void OnBackToPauseMenu() {
        if (rulesPanel != null) rulesPanel.SetActive(false);
        pausePanel.SetActive(true);
    }

    public void OnLeaveGamePressed() {
        Time.timeScale = 1f; // Always reset time before changing scenes!
        SceneManager.LoadScene("MainMenu");
    }
}