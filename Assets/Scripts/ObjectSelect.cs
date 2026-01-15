using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectSelect : MonoBehaviour
{
    public Material selectedMat;

    private Material prevMat;
    
    private GameObject selectedObject;
    private Deck deck;

    [SerializeField] private DeckMenu deckMenu;
    [SerializeField] private CardMenu cardMenu;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
           
    }



    // Update is called once per frame
    void Update() {
        if (Input.GetMouseButtonDown(0)) {
            // Restore previous selection
            if (selectedObject != null) {
                selectedObject.GetComponent<MeshRenderer>().material = prevMat;
            }

            // Block clicks on scene if any menu is open
            if (deckMenu.GetActive() || cardMenu.GetActive()) {
                // But if cardMenu is waiting for a deck, allow deck clicks
                if (!cardMenu.IsWaitingForDeck())
                    return; // skip world raycast
            }
            var ray = GetRayOnMousePosition();
            if (Physics.Raycast(ray, out var raycastHit)) {
                GameObject clickedObject = raycastHit.transform.gameObject;

                // --- Handle waiting-for-deck mode first ---
                if (cardMenu.IsWaitingForDeck()) {
                    Deck clickedDeck = clickedObject.GetComponent<Deck>();
                    if (clickedDeck != null) {
                        cardMenu.AddCardToDeck(clickedDeck);
                        return; // consume click
                    } else {
                        Debug.Log("Click a deck to add the card.");
                        return; // ignore click
                    }
                }

                // --- Normal selection ---
                if (deckMenu.GetActive()) deckMenu.Hide();
                if (cardMenu.GetActive()) cardMenu.Hide();


                // Update selection
                selectedObject = clickedObject;
                prevMat = selectedObject.GetComponent<MeshRenderer>().material;
                selectedObject.GetComponent<MeshRenderer>().material = selectedMat;

                // Deck click
                Deck deckComponent = selectedObject.GetComponent<Deck>();
                if (deckComponent != null) {
                    deck = deckComponent;
                    Vector3 pos = Input.mousePosition;
                    pos.x += 100;
                    pos.y -= 100;
                    deckMenu.Show(deck, pos);
                }
                // Card click
                else {
                    CardView cardComponent = selectedObject.GetComponent<CardView>();
                    if (cardComponent != null) {
                        Vector3 pos = Input.mousePosition;
                        pos.x += 100;
                        pos.y -= 100;
                        cardMenu.Show(cardComponent, pos);
                    }
                }
            }
        }
    }




    public Ray GetRayOnMousePosition()
    {
        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        return ray;
    }
}
