using UnityEngine;
using TMPro;

public class PlayerProfileManager : MonoBehaviour {
    public static PlayerProfileManager Instance { get; private set; }

    private const string NAME_KEY = "PlayerName";
    public string PlayerName { get; private set; }

    private void Awake() {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadName();
    }

    public void SaveName(string newName) {
        if (string.IsNullOrWhiteSpace(newName)) return;
        PlayerName = newName;
        PlayerPrefs.SetString(NAME_KEY, newName);
        PlayerPrefs.Save();
        Debug.Log($"<color=cyan>[Profile]</color> Name saved: {newName}");
    }

    private void LoadName() {
        PlayerName = PlayerPrefs.GetString(NAME_KEY, "New Player");
    }
}