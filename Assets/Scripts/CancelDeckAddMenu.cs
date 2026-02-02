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
        if (controller != null) 
            controller.onCancelDeckAddPressed();
        else {
            // fallback: do nothing
            Debug.LogWarning("CancelDeckAddMenu controller missing - cannot leave add-to-deck mode.");
        }

        // The menu hides; the controller will now be in ChoosingDeckForCard state.
        Hide();
    }
}
