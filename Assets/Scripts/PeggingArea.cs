using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PeggingArea : NetworkBehaviour {
    [Header("Layout Settings")]
    [Tooltip("How far apart to spread the cards horizontally")]
    [SerializeField] private float cardSpacingX = 0.6f;
    [Tooltip("Slight vertical lift so cards don't Z-fight (clip) into each other")]
    [SerializeField] private float cardSpacingY = 0.02f;
    [SerializeField] private float transitionSpeed = 10f;

    public List<CardView> peggedCards = new List<CardView>();

    void Update() {
        ArrangeCards();
    }

    public void AddCard(CardView card) {
        if (card == null || peggedCards.Contains(card)) return;
        peggedCards.Add(card);

        // Turn off physics so it sits perfectly still on the table
        if (card.TryGetComponent<Rigidbody>(out var rb)) {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    public void RemoveCard(CardView card) {
        if (peggedCards.Contains(card)) peggedCards.Remove(card);
    }

    public void ClearArea() {
        peggedCards.Clear();
    }

    private void ArrangeCards() {
        peggedCards.RemoveAll(c => c == null);

        for (int i = 0; i < peggedCards.Count; i++) {
            // Cascade them slightly to the right, and slightly up
            Vector3 targetPos = transform.position
                              + (transform.right * (i * cardSpacingX))
                              + (transform.up * (i * cardSpacingY));

            Transform cardTransform = peggedCards[i].transform;

            cardTransform.position = Vector3.Lerp(cardTransform.position, targetPos, Time.deltaTime * transitionSpeed);
            cardTransform.rotation = Quaternion.Slerp(cardTransform.rotation, transform.rotation, Time.deltaTime * transitionSpeed);
        }
    }
}   