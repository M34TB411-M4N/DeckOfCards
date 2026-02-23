using UnityEngine;
using TMPro;

public class BootLogger : MonoBehaviour {
    public TextMeshProUGUI debugText; // Optional: drag a UI text element here

    void Awake() {
        // This will show up in your Logcat!
        Debug.Log("!!! APP REACHED MAIN MENU AWAKE !!!");
        if (debugText != null) debugText.text = "App Started!";
    }

    void Start() {
        Debug.Log("!!! APP REACHED MAIN MENU START !!!");
    }
}