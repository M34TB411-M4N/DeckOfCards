using UnityEngine;
using Unity.Netcode;

public class MultiplayerOnlyUI : MonoBehaviour {
    void Start() {
        // If the NetworkManager isn't running, or if we are the only person on the server, turn this UI off!
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) {
            gameObject.SetActive(false);
            return;
        }

        // If we are a Host and nobody else is connected, hide this.
        if (NetworkManager.Singleton.IsServer && NetworkManager.Singleton.ConnectedClientsList.Count <= 1) {
            gameObject.SetActive(false);
        }
    }
}