using UnityEngine;
using UnityEngine.EventSystems; // Required for UI checking

public class GoFishInteraction : MonoBehaviour {
    [Header("UI References")]
    public GameObject rankSelectionPanel;
    public GoFishRankSelector rankSelectorUI;

    void Update() {
        if (GoFishManager.Instance == null || GameManager.Instance == null) return;

        // Only allow interaction if it's my turn
        if (GoFishManager.Instance.netCurrentTurn.Value != GameManager.Instance.myPlayerIndex) return;

        // CRITICAL FIX: Prevent raycasting if the mouse is hovering over a UI element (like the menu)
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (Input.GetMouseButtonDown(0)) {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit)) {
                Debug.Log($"<color=cyan>[GoFishInteraction]</color> Raycast hit: {hit.collider.gameObject.name}");

                // Try to find the PlayerHand on the object we hit, or its parents
                PlayerHand clickedHand = hit.collider.GetComponentInParent<PlayerHand>();

                if (clickedHand != null) {
                    if (clickedHand == GameManager.Instance.MyHand) {
                        Debug.Log("<color=yellow>[GoFishInteraction]</color> You clicked your own hand! You must click an opponent.");
                        return;
                    }

                    Debug.Log($"<color=green>[GoFishInteraction]</color> Valid target found! Opening menu for Seat {clickedHand.GetComponent<GoFishPlayer>().seatIndex}");
                    OpenAskUI(clickedHand);
                } else {
                    Debug.LogWarning("<color=red>[GoFishInteraction]</color> The object you clicked does not have a PlayerHand script on it or its parents.");
                }
            }
        }
    }

    public void OpenAskUI(PlayerHand targetHand) {
        GoFishPlayer targetData = targetHand.GetComponent<GoFishPlayer>();

        if (targetData != null && rankSelectorUI != null) {
            rankSelectorUI.Open(targetData.seatIndex);
        }
    }
}