using UnityEngine;
using UnityEngine.InputSystem.XR;

public class CancelDeckAddMenu : MonoBehaviour
{
    [HideInInspector] public ObjectSelect controller;
    void Awake() {
        gameObject.SetActive(false);
    }

    public void Show() {
        gameObject.SetActive(true);
    }

    public void Hide() {
        gameObject.SetActive(false);
    }

    public bool GetActive() {
        return gameObject.activeSelf;
    }

    // Called by the AddToDeck UI button
    public void OnCancelDeckAddPressed() {
        if (controller != null) controller.onCancelDeckAddPressed();
    }
}
