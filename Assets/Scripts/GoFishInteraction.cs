using UnityEngine;
using UnityEngine.EventSystems;

public class GoFishInteraction : MonoBehaviour {
    [Header("UI References")]
    public GameObject rankSelectionPanel;
    public GoFishRankSelector rankSelectorUI;

    void Update() {
        if (GoFishManager.Instance == null || GameManager.Instance == null) return;
        if (GoFishManager.Instance.netCurrentTurn.Value != GameManager.Instance.myPlayerIndex) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (Input.GetMouseButtonDown(0)) {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            // PIERCE FIX: Get EVERYTHING the mouse clicked through
            RaycastHit[] hits = Physics.RaycastAll(ray);

            foreach (RaycastHit hit in hits) {
                PlayerHand clickedHand = hit.collider.GetComponentInParent<PlayerHand>();

                if (clickedHand != null) {
                    // If we hit our own hand, ignore it and keep looking through the stack!
                    if (clickedHand == GameManager.Instance.MyHand) continue;

                    // We found an opponent! Open the menu and stop looking.
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