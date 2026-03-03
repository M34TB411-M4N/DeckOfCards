using TMPro;
using UnityEngine;

public class LobbyPlayerEntry : MonoBehaviour {
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Color readyColor = Color.green;
    [SerializeField] private Color notReadyColor = Color.red;

    public void Setup(string pName, bool isReady) {
        nameText.text = pName;
        SetReady(isReady);
    }

    public void SetReady(bool isReady) {
        statusText.text = isReady ? "READY" : "NOT READY";
        statusText.color = isReady ? readyColor : notReadyColor;
    }
}