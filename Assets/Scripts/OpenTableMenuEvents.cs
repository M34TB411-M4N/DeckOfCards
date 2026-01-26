using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

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
        SceneManager.LoadScene("Table");
        Debug.Log("single clicked");
    }

    private void OnMultiplayerGameClicked(ClickEvent e) {
        Debug.Log("multi clicked");
    }

    private void OnBackClicked(ClickEvent e) {
        SceneManager.LoadScene("MainMenu");
        Debug.Log("back clicked");
    }
}
