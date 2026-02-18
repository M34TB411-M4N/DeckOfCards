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
        // 1. Clear existing buttons
        foreach (Transform child in gridContainer) {
            Destroy(child.gameObject);
        }

        // 2. Get local player's logic to see what ranks they currently hold
        // This is a standard rule: You can only ask for what you have.
        GoFishPlayer localGoFishData = GameManager.Instance.MyHand.GetComponent<GoFishPlayer>();

        // 3. Create a button for every Rank in the Enum
        foreach (Rank r in Enum.GetValues(typeof(Rank))) {
            GameObject btnObj = Instantiate(buttonPrefab, gridContainer);
            RankButton btnScript = btnObj.GetComponent<RankButton>();

            bool hasRank = localGoFishData != null && localGoFishData.HasRank(r);
            btnScript.Setup(r, this, hasRank);
        }
    }

    public void OnRankSelected(Rank selectedRank) {
        if (GoFishManager.Instance != null) {
            GoFishManager.Instance.ProcessRequest(targetSeatIndex, selectedRank);
        }
        gameObject.SetActive(false);
    }

    public void CloseMenu() {
        gameObject.SetActive(false);
    }
}