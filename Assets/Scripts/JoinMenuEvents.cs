using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class JoinMenuEvents : MonoBehaviour {
    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private TMP_InputField nameInputField; // NEW: Name field
    [SerializeField] private Button joinButton;

    private void Start() {
        // Load the last used name so they don't HAVE to type it every time
        nameInputField.text = PlayerPrefs.GetString("DisplayName", "New Player");

        joinButton.onClick.AddListener(OnJoinClicked);
    }

    private void OnJoinClicked() {
        // Save the current name in the box (allows them to change it every join)
        PlayerPrefs.SetString("DisplayName", nameInputField.text);
        PlayerPrefs.Save();

        string targetIP = ipInputField.text;
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.ConnectionData.Address = targetIP;

        NetworkManager.Singleton.StartClient();
    }

    // Call this if you have a Host button somewhere too!
    public void OnHostClicked() {
        PlayerPrefs.SetString("DisplayName", nameInputField.text);
        PlayerPrefs.Save();
        NetworkManager.Singleton.StartHost();
    }
}