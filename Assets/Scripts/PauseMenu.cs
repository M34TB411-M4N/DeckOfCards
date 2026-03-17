using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// THE FIX: Changed from MonoBehaviour to NetworkBehaviour
public class PauseMenu : NetworkBehaviour {
    [Header("UI Panels")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject rulesPanel;

    [Header("Controllers to Disable")]
    [SerializeField] private ObjectSelect objectSelect;

    private bool isPaused = false;

    [Header("Spawning")]
    [SerializeField] private GameObject deckPrefab;
    [SerializeField] private float defaultSpawnDistance = 10f;
    [SerializeField] private float spawnHeightOffset = 2f;

    void Start() {
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
        Time.timeScale = 0f;

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
        Quaternion spawnRotation = Quaternion.Euler(0, Camera.main.transform.eulerAngles.y, 0);

        // THE FIX: Ask the server to spawn the deck, don't do it locally!
        RequestSpawnDeckServerRpc(spawnPosition, spawnRotation);

        Debug.Log($"Requested deck spawn at {spawnPosition}");
        ResumeGame();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnDeckServerRpc(Vector3 position, Quaternion rotation) {
        // 1. The Server physically instantiates the object
        GameObject newDeck = Instantiate(deckPrefab, position, rotation);

        // 2. The Server officially registers it with the network so everyone sees it!
        NetworkObject netObj = newDeck.GetComponent<NetworkObject>();
        if (netObj != null) {
            netObj.Spawn();
        } else {
            Debug.LogError("<color=red>[Network Error]</color> Deck prefab is missing a NetworkObject component!");
        }
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
        Time.timeScale = 1f;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) {
            NetworkManager.Singleton.Shutdown();
            Destroy(NetworkManager.Singleton.gameObject);
        }

        SceneManager.LoadScene("MainMenu");
    }

    private Vector3 CalculateSpawnPosition() {
        Camera cam = Camera.main;
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        float tableY = 0f;
        GameObject floor = GameObject.FindGameObjectWithTag("Floor");
        if (floor != null) tableY = floor.transform.position.y;

        Plane tablePlane = new Plane(Vector3.up, new Vector3(0, tableY, 0));

        if (tablePlane.Raycast(ray, out float enter)) {
            if (enter <= defaultSpawnDistance * 2f) {
                return ray.GetPoint(enter) + Vector3.up * spawnHeightOffset;
            }
        }

        Vector3 fallbackPos = cam.transform.position + cam.transform.forward * defaultSpawnDistance;
        fallbackPos.y = tableY + spawnHeightOffset;
        return fallbackPos;
    }
}