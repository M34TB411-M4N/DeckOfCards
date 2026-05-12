using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class PremadeGameMenuEvents : MonoBehaviour {
    private UIDocument document;

    private Button cribbageButton;
    private Button texasHoldEmButton;
    private Button goFishButton;
    private Button backButton;

    private void Awake() {
        document = GetComponent<UIDocument>();

        cribbageButton = document.rootVisualElement.Q("Cribbage") as Button;
        cribbageButton.RegisterCallback<ClickEvent>(OnCribbageClicked);
        texasHoldEmButton = document.rootVisualElement.Q("TexasHoldEm") as Button;
        texasHoldEmButton.RegisterCallback<ClickEvent>(OnTexasHoldEmClicked);
        goFishButton = document.rootVisualElement.Q("GoFish") as Button;
        goFishButton.RegisterCallback<ClickEvent>(OnGoFishClicked);
        backButton = document.rootVisualElement.Q("Back") as Button;
        backButton.RegisterCallback<ClickEvent>(OnBackClicked);
    }

    private async void OnCribbageClicked(ClickEvent e) {
        cribbageButton.SetEnabled(false);
        GameSessionData.SelectedMode = GameMode.Cribbage;

        // Request a 2-player Relay
        string code = await RelayManager.Instance.CreateRelay(2);

        if (!string.IsNullOrEmpty(code)) {
            GameSessionData.RelayJoinCode = code;
            // THE FIX: Route to the Lobby Settings first!
            NetworkManager.Singleton.SceneManager.LoadScene("LobbySettings", LoadSceneMode.Single);
        } else {
            cribbageButton.SetEnabled(true);
            Debug.LogError("Relay generation failed!");
        }
    }
    private void OnTexasHoldEmClicked(ClickEvent e) {
        Debug.Log("texas clicked");
    }

    private async void OnGoFishClicked(ClickEvent e) {
        goFishButton.SetEnabled(false);

        GameSessionData.SelectedMode = GameMode.GoFish;

        string code = await RelayManager.Instance.CreateRelay(4);

        if (!string.IsNullOrEmpty(code)) {
            GameSessionData.RelayJoinCode = code; // Save the code!
            NetworkManager.Singleton.SceneManager.LoadScene("LobbySettings", LoadSceneMode.Single);
        } else {
            goFishButton.SetEnabled(true);
            Debug.LogError("Relay generation failed!");
        }
    }

    private void OnBackClicked(ClickEvent e) {
        SceneManager.LoadScene("MainMenu");
    }
}