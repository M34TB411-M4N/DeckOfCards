using UnityEngine;
using UnityEngine.UIElements;

public class MainMenuEvents : MonoBehaviour
{
    private UIDocument document;

    private Button openTableButton;
    private Button premadeGameButton;
    private Button joinLobbyButton;

    private void Awake() {
        document = GetComponent<UIDocument>();

        openTableButton = document.rootVisualElement.Q("OpenTable") as Button;
        openTableButton.RegisterCallback<ClickEvent>(OnOpenTableClicked);
        premadeGameButton = document.rootVisualElement.Q("PremadeGame") as Button;
        premadeGameButton.RegisterCallback<ClickEvent>(OnPremadeGameClicked);
        joinLobbyButton = document.rootVisualElement.Q("JoinLobby") as Button;
        joinLobbyButton.RegisterCallback<ClickEvent>(OnJoinLobbyClicked);
    }

    private void OnOpenTableClicked(ClickEvent e) {
        Debug.Log("opentable clicked");
    }

    private void OnPremadeGameClicked(ClickEvent e) {
        Debug.Log("premadegame clicked");
    }

    private void OnJoinLobbyClicked(ClickEvent e) {
        Debug.Log("joinlobby clicked");
    }
}
