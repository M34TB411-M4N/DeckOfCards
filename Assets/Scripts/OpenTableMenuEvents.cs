using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class OpenTableMenuEvents : MonoBehaviour {
    private UIDocument document;

    private Button singleplayerButton;
    private Button multiplayerButton;
    private Button backButton;

    private void Awake() {
        document = GetComponent<UIDocument>();

        singleplayerButton = document.rootVisualElement.Q("Singleplayer") as Button;
        singleplayerButton.RegisterCallback<ClickEvent>(OnSingleplayerClicked);

        multiplayerButton = document.rootVisualElement.Q("Multiplayer") as Button;
        multiplayerButton.RegisterCallback<ClickEvent>(OnMultiplayerGameClicked);

        backButton = document.rootVisualElement.Q("Back") as Button;
        backButton.RegisterCallback<ClickEvent>(OnBackClicked);
    }

    private void OnSingleplayerClicked(ClickEvent e) {
        // Singleplayer doesn't need Relay, so local Host is fine!
        NetworkManager.Singleton.StartHost();
        NetworkManager.Singleton.SceneManager.LoadScene("Table", LoadSceneMode.Single);
    }

    private async void OnMultiplayerGameClicked(ClickEvent e) {
        multiplayerButton.SetEnabled(false); // Prevent spam clicking

        GameSessionData.SelectedMode = GameMode.Sandbox;

        // Tell RelayManager to host the game
        string code = await RelayManager.Instance.CreateRelay(4);

        if (!string.IsNullOrEmpty(code)) {
            GameSessionData.RelayJoinCode = code; // Save the code!
            NetworkManager.Singleton.SceneManager.LoadScene("LobbySettings", LoadSceneMode.Single);
        } else {
            multiplayerButton.SetEnabled(true);
            Debug.LogError("Relay generation failed!");
        }
    }

    private void OnBackClicked(ClickEvent e) {
        SceneManager.LoadScene("MainMenu");
    }
}