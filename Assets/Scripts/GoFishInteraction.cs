using UnityEngine;
using UnityEngine.EventSystems;

public class GoFishInteraction : MonoBehaviour {
    [Header("UI References")]
    public GameObject rankSelectionPanel;
    public GoFishRankSelector rankSelectorUI;

    void Update() {
        if (GoFishManager.Instance == null || GameManager.Instance == null) return;

        // Only allow interaction if it's my turn
        if (GoFishManager.Instance.netCurrentTurn.Value != GameManager.Instance.myPlayerIndex) return;

        // Prevent clicking through UI menus
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (Input.GetMouseButtonDown(0)) {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            // Pierce through our own cards to see what's behind them
            RaycastHit[] hits = Physics.RaycastAll(ray);

            foreach (RaycastHit hit in hits) {
                PlayerHand clickedHand = hit.collider.GetComponentInParent<PlayerHand>();

                if (clickedHand != null) {
                    // Ignore our own hand
                    if (clickedHand == GameManager.Instance.MyHand) continue;

                    // THE FIX: Prevent clicking on opponents who have no cards!
                    if (clickedHand.cardsInHand.Count == 0) {
                        Debug.Log("<color=yellow>[GoFishInteraction]</color> That opponent has no cards!");
                        GoFishManager.Instance.UpdateLog("That player has no cards to steal!");
                        continue; // Keep looking through the raycast in case they clicked a stack
                    }

                    Debug.Log($"<color=green>[GoFishInteraction]</color> Valid target found! Opening menu for Seat {clickedHand.GetComponent<GoFishPlayer>().seatIndex}");
                    OpenAskUI(clickedHand);
                    break;
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