using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class RankButton : MonoBehaviour {
    public Rank myRank;
    private GoFishRankSelector selector;
    [SerializeField] private TextMeshProUGUI buttonText;

    public void Setup(Rank rank, GoFishRankSelector parentSelector, bool canClick) {
        myRank = rank;
        selector = parentSelector;

        if (buttonText != null) {
            // Converts "Ace" to "A", "Two" to "2", etc., or just use .ToString()
            buttonText.text = rank.ToString();
        }

        Button btn = GetComponent<Button>();
        btn.interactable = canClick;

        // Clear old listeners to prevent double-clicks or errors
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnClick);
    }

    private void OnClick() {
        selector.OnRankSelected(myRank);
    }
}