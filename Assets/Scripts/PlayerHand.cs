using UnityEngine;
using System.Collections.Generic;

public class PlayerHand : MonoBehaviour {
    [Header("Layout Settings")]
    [SerializeField] private float cardWidth = 2.0f;       // The actual width of your card object
    [SerializeField] private float padding = 0.1f;         // Gap between cards
    [SerializeField] private float maxHandWidth = 10.0f;   // Total width available for the hand

    [Header("Visual Tweaks")]
    [SerializeField] private float cardThickness = 0.05f;  // Set this to your actual card thickness
    [SerializeField] private Vector3 standingRotation = new Vector3(-90, 0, 0); // Rotation to make them stand up
    [SerializeField] private float transitionSpeed = 10f;

    // We make this public so we can debug it in Inspector
    public List<CardView> cardsInHand = new List<CardView>();

    void Update() {
        ArrangeCards();
    }

    public void AddCard(CardView card) {
        if (!cardsInHand.Contains(card)) {
            cardsInHand.Add(card);

            if (card.TryGetComponent<Rigidbody>(out var rb)) {
                rb.isKinematic = true;
            }

            // FIX: Disable collider so it doesn't bump into things while moving/sitting in hand
            if (card.TryGetComponent<Collider>(out var coll)) {
                coll.enabled = false;
            }
        }
        if (card.TryGetComponent<Collider>(out var col)) {
            col.isTrigger = true; // No physics bounces, but still clickable!
        }
    }

    public void RemoveCard(CardView card) {
        if (cardsInHand.Contains(card)) {
            cardsInHand.Remove(card);

            if (card.TryGetComponent<Rigidbody>(out var rb)) {
                rb.isKinematic = false;
            }

            // FIX: Re-enable collider so you can pick it up again on the table
            if (card.TryGetComponent<Collider>(out var coll)) {
                coll.enabled = true;
            }
        }
        if (card.TryGetComponent<Collider>(out var col)) {
            col.isTrigger = false; // Solid again for the table
        }
    }

    private void ArrangeCards() {
        int count = cardsInHand.Count;
        if (count == 0) return;

        // 1. Calculate the standard spacing (Card Width + Padding)
        float targetSpacing = cardWidth + padding;

        // 2. Calculate total required width if we used that standard spacing
        float totalRequiredWidth = (count - 1) * targetSpacing;

        // 3. Check if we are overflowing the max width
        if (totalRequiredWidth > maxHandWidth) {
            // Squeeze them: Recalculate spacing to fit exactly in maxHandWidth
            targetSpacing = maxHandWidth / (count - 1);
            totalRequiredWidth = maxHandWidth;
        }

        // 4. Calculate the starting X position (Centered on the seat)
        float startX = -totalRequiredWidth / 2f;

        // 5. Position Loop
        for (int i = 0; i < count; i++) {
            // Horizontal Position (X)
            float xPos = startX + (i * targetSpacing);

            // Depth Offset (Z) - Using full card thickness to prevent flickering
            // We move them forward/back based on index so they stack cleanly
            float zPos = i * -cardThickness;

            // Calculate Target Position relative to the Seat
            // Right * xPos moves it side-to-side
            // Forward * zPos stacks them slightly so faces don't touch
            Vector3 targetPos = transform.position
                              + (transform.right * xPos)
                              + (transform.forward * zPos);

            // Rotation Logic:
            // Take the Seat's rotation, then apply the "Standing Up" adjustment
            Quaternion seatRot = transform.rotation;
            Quaternion standRot = Quaternion.Euler(standingRotation);
            Quaternion finalRot = seatRot * standRot;

            Transform cardTransform = cardsInHand[i].transform;

            // FIX: Snapping Logic
            // If the card is very close to its destination, just snap it there to avoid the "slow crawl"
            if (Vector3.Distance(cardTransform.position, targetPos) < 0.01f) {
                cardTransform.position = targetPos;
            } else {
                cardTransform.position = Vector3.Lerp(cardTransform.position, targetPos, Time.deltaTime * transitionSpeed);
            }

            if (Quaternion.Angle(cardTransform.rotation, finalRot) < 0.1f) {
                cardTransform.rotation = finalRot;
            } else {
                cardTransform.rotation = Quaternion.Slerp(cardTransform.rotation, finalRot, Time.deltaTime * transitionSpeed);
            }
            // Apply Smooth Movement
            //Transform cardTransform = cardsInHand[i].transform;
            //cardTransform.position = Vector3.Lerp(cardTransform.position, targetPos, Time.deltaTime * transitionSpeed);
            //cardTransform.rotation = Quaternion.Slerp(cardTransform.rotation, finalRot, Time.deltaTime * transitionSpeed);
        }
    }
}