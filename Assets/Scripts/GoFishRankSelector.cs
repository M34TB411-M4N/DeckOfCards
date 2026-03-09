using UnityEngine;
using System.Collections.Generic;
using System;

public class GoFishRankSelector : MonoBehaviour {
    [SerializeField] private GameObject buttonPrefab;
    [SerializeField] private Transform gridContainer;

    private int targetSeatIndex;

    public void Open(int targetIndex) {
        targetSeatIndex = targetIndex;
        gameObject.SetActive(true);
        RefreshButtons();
    }

    private void RefreshButtons() {
        foreach (Transform child in gridContainer) {
            Destroy(child.gameObject);
        }

        GoFishPlayer localGoFishData = GameManager.Instance.MyHand.GetComponent<GoFishPlayer>();

        foreach (Rank r in Enum.GetValues(typeof(Rank))) {
            GameObject btnObj = Instantiate(buttonPrefab, gridContainer);
            RankButton btnScript = btnObj.GetComponent<RankButton>();

            // You can only ask for a rank if you already have one in your hand
            bool hasRank = localGoFishData != null && localGoFishData.HasRank(r);
            btnScript.Setup(r, this, hasRank);
        }
    }

    public void OnRankSelected(Rank selectedRank) {
        if (GoFishManager.Instance != null) {
            // Send the request to the Server!
            GoFishManager.Instance.SubmitRequestServerRpc(GameManager.Instance.myPlayerIndex, targetSeatIndex, selectedRank);
        }
        gameObject.SetActive(false);
    }

    public void CloseMenu() {
        gameObject.SetActive(false);
    }
}