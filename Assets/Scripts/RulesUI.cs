using UnityEngine;

public class RulesUI : MonoBehaviour {
    [SerializeField] private GameObject rulesPanel;

    void Start() {
        if (rulesPanel != null) rulesPanel.SetActive(false);
    }

    public void ToggleRules() {
        if (rulesPanel == null) return;
        rulesPanel.SetActive(!rulesPanel.activeSelf);
    }
}