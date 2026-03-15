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
        NetworkManager.Singleton.StartHost();
        NetworkManager.Singleton.SceneManager.LoadScene("Table", LoadSceneMode.Single);
    }

    private void OnMultiplayerGameClicked(ClickEvent e) {
        GameSessionData.SelectedMode = GameMode.Sandbox;
        NetworkManager.Singleton.StartHost();
        NetworkManager.Singleton.SceneManager.LoadScene("LobbySettings", LoadSceneMode.Single);
        Debug.Log("multi clicked");
    }

    private void OnBackClicked(ClickEvent e) {
        SceneManager.LoadScene("MainMenu");
        Debug.Log("back clicked");
    }
}
