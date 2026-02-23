using UnityEngine;
using TMPro; // Use this for Canvas-based UI
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class JoinMenuCanvas : MonoBehaviour {
    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private Button joinButton;

    private void Start() {
        joinButton.onClick.AddListener(OnJoinClicked);
    }

    private void OnJoinClicked() {
        string targetIP = ipInputField.text;
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.ConnectionData.Address = targetIP;
        NetworkManager.Singleton.StartClient();
    }
}