using Unity.Netcode;
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
    [Header("Spawning")]
    [SerializeField] private GameObject deckPrefab;
    [SerializeField] private float defaultSpawnDistance = 10f;
    [SerializeField] private float spawnHeightOffset = 2f; // Spawn slightly above table so it drops in

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
        Vector3 spawnPosition = CalculateSpawnPosition();

        // Instantiate the deck
        GameObject newDeck = Instantiate(deckPrefab, spawnPosition, Quaternion.identity);

        // Optional: If you want it to land flat, reset rotation
        newDeck.transform.rotation = Quaternion.Euler(0, Camera.main.transform.eulerAngles.y, 0);

        Debug.Log($"Deck spawned at {spawnPosition}");
        ResumeGame(); // Close menu after adding
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
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) {

            // 2. Sever all connections and kill the server/client
            NetworkManager.Singleton.Shutdown();

            // 3. (Optional but recommended) Destroy the NetworkManager object completely 
            // to ensure a 100% clean slate the next time they click "Host" or "Join"
            Destroy(NetworkManager.Singleton.gameObject);
        }

        // 4. Now it is safe to load the Main Menu!
        SceneManager.LoadScene("MainMenu"); // Replace with your actual Main Menu scene name
    }

    private Vector3 CalculateSpawnPosition() {
        Camera cam = Camera.main;
        // 1. Define the ray from the center of the screen
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        // 2. Define a mathematical plane at the table's Y level (assuming Y=0 or floor height)
        // Find the floor Y from your existing logic or a serialized field
        float tableY = 0f;
        GameObject floor = GameObject.FindGameObjectWithTag("Floor");
        if (floor != null) tableY = floor.transform.position.y;

        Plane tablePlane = new Plane(Vector3.up, new Vector3(0, tableY, 0));

        // 3. Try to intersect the ray with the plane
        if (tablePlane.Raycast(ray, out float enter)) {
            // Limit how far away they can spawn a deck so it's not miles away
            if (enter <= defaultSpawnDistance * 2f) {
                return ray.GetPoint(enter) + Vector3.up * spawnHeightOffset;
            }
        }

        // 4. Fallback: If looking at sky or too far, spawn in front of camera
        Vector3 fallbackPos = cam.transform.position + cam.transform.forward * defaultSpawnDistance;
        fallbackPos.y = tableY + spawnHeightOffset; // Keep it at a reasonable height
        return fallbackPos;
    }
}