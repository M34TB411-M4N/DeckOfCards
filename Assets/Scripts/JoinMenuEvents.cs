using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;
using System.Threading.Tasks; // REQUIRED for async

public class JoinMenuEvents : MonoBehaviour {
    [SerializeField] private TMP_InputField ipInputField; // Now used for the Join Code!
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private Button joinButton;

    private void Start() {
        nameInputField.text = PlayerPrefs.GetString("DisplayName", "New Player");
        joinButton.onClick.AddListener(OnJoinClicked);
    }

    private async void OnJoinClicked() {
        PlayerPrefs.SetString("DisplayName", nameInputField.text);
        PlayerPrefs.Save();

        // Format the code (removes accidental spaces and makes it uppercase)
        string joinCode = ipInputField.text.Trim().ToUpper();
        if (string.IsNullOrWhiteSpace(joinCode)) return;

        joinButton.interactable = false; // Prevent double-clicking

        // Let the RelayManager do the heavy lifting!
        bool success = await RelayManager.Instance.JoinRelay(joinCode);

        if (!success) {
            joinButton.interactable = true;
            Debug.LogError("Failed to join Relay!");
        }
    }

    public void OnHostClicked() {
        PlayerPrefs.SetString("DisplayName", nameInputField.text);
        PlayerPrefs.Save();
        NetworkManager.Singleton.StartHost();
    }
}