using UnityEngine;

public class GoFishInteraction : MonoBehaviour {
    [Header("UI References")]
    public GameObject rankSelectionPanel; // Assign a UI Panel prefab here
    public GoFishRankSelector rankSelectorUI; // Script on that panel

    void Update() {
        // Only allow interaction if it is MY turn
        if (GoFishManager.Instance.currentPlayerTurnIndex != GameManager.Instance.myPlayerIndex) return;

        if (Input.GetMouseButtonDown(0)) {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit)) {
                // Check if we clicked a Player Hand (or the area representing them)
                PlayerHand clickedHand = hit.collider.GetComponentInParent<PlayerHand>();

                if (clickedHand != null) {
                    // Check if we clicked OURSELVES (invalid in Go Fish)
                    if (clickedHand == GameManager.Instance.MyHand) return;

                    // Open the UI to ask this player
                    OpenAskUI(clickedHand);
                }
            }
        }
    }

    public void OpenAskUI(PlayerHand targetHand) {
        // 1. Find the GoFishPlayer component on that seat to get their index
        GoFishPlayer targetData = targetHand.GetComponent<GoFishPlayer>();

        if (targetData != null && rankSelectorUI != null) {
            // 2. Open the rank selector and tell it which player we are targeting
            rankSelectorUI.Open(targetData.seatIndex);
        }
    }
}