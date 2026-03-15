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

    private void OnCribbageClicked(ClickEvent e) {
        Debug.Log("cribbage clicked");
    }

    private void OnTexasHoldEmClicked(ClickEvent e) {
        Debug.Log("texas clicked");
    }

    private void OnGoFishClicked(ClickEvent e) {
        GameSessionData.SelectedMode = GameMode.GoFish;
        NetworkManager.Singleton.StartHost();
        NetworkManager.Singleton.SceneManager.LoadScene("LobbySettings", UnityEngine.SceneManagement.LoadSceneMode.Single);

        //SceneManager.LoadScene("GoFishSettings");
        //Debug.Log("go fish clicked");
    }

    private void OnBackClicked(ClickEvent e) {
        SceneManager.LoadScene("MainMenu");
        Debug.Log("back clicked");
    }
}
