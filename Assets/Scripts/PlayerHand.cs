using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using TMPro;

public class PlayerHand : NetworkBehaviour {
    [Header("UI")]
    public TextMeshPro nameTextDisplay;

    [Header("Layout Settings")]
    [SerializeField] private float cardWidth = 2.0f;
    [SerializeField] private float padding = 0.1f;
    [SerializeField] private float maxHandWidth = 10.0f;

    [Header("Visual Tweaks")]
    [SerializeField] private float cardThickness = 0.03f;
    [SerializeField] private Vector3 standingRotation = new Vector3(-90, 180, 0); // Fixed for face up
    [SerializeField] private float transitionSpeed = 10f;

    // NEW: Emphasize height for Cribbage
    [SerializeField] private float emphasizeHeight = 1.0f;

    public List<CardView> cardsInHand = new List<CardView>();

    // NEW: Track which cards are selected
    public List<CardView> emphasizedCards = new List<CardView>();

    [Header("Camera Settings")]
    public Transform cameraAnchor;

    void Update() {
        ArrangeCards();
    }

    public void UpdateNameText(string newName) {
        if (nameTextDisplay != null) nameTextDisplay.text = newName;
    }

    public void AddCard(CardView card) {
        if (card == null) return;
        if (!cardsInHand.Contains(card)) {
            cardsInHand.Add(card);

            if (GoFishManager.Instance != null || CribbageManager.Instance != null) {
                SortHandByRank();
            }

            if (card.TryGetComponent<Rigidbody>(out var rb)) {
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            if (card.TryGetComponent<Collider>(out var col)) {
                col.enabled = true;
                col.isTrigger = true;
            }
        }
    }

    public void RemoveCard(CardView card) {
        if (card == null) return;
        if (cardsInHand.Contains(card)) {
            cardsInHand.Remove(card);
            if (emphasizedCards.Contains(card)) emphasizedCards.Remove(card); // Clean up if removed

            if (GoFishManager.Instance == null && CribbageManager.Instance == null) {
                if (card.TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = false;
                if (card.TryGetComponent<Collider>(out var col)) {
                    col.enabled = true;
                    col.isTrigger = false;
                }
            }
        }
    }

    // NEW: Toggles a card's emphasize state (Max 2 for Cribbage)
    public void ToggleEmphasize(CardView card) {
        if (!cardsInHand.Contains(card)) return;

        if (emphasizedCards.Contains(card)) {
            emphasizedCards.Remove(card);
        } else {
            if (emphasizedCards.Count < 2) {
                emphasizedCards.Add(card);
            }
        }

        // Let the Cribbage Manager know the selection changed so it can enable/disable the "Send to Crib" button
        if (CribbageManager.Instance != null) {
            CribbageManager.Instance.OnEmphasizeChanged(emphasizedCards.Count);
        }
    }

    public void ClearEmphasizedCards() {
        emphasizedCards.Clear();
    }

    private void SortHandByRank() {
        cardsInHand.RemoveAll(c => c == null);
        if (cardsInHand.Count <= 1) return;

        cardsInHand = cardsInHand
            .OrderBy(c => c.GetCardData() != null ? (int)c.GetCardData().rank : 999)
            .ToList();
    }
    private void ArrangeCards() {
        cardsInHand.RemoveAll(c => c == null);
        int count = cardsInHand.Count;
        if (count == 0) return;

        float targetSpacing = cardWidth + padding;
        float totalRequiredWidth = (count - 1) * targetSpacing;

        if (totalRequiredWidth > maxHandWidth) {
            targetSpacing = maxHandWidth / (count - 1);
            totalRequiredWidth = maxHandWidth;
        }

        float startX = -totalRequiredWidth / 2f;

        for (int i = 0; i < count; i++) {

            // THE ULTIMATE PHYSICS OVERRIDE:
            // Force kinematic every single frame. The OutOfBoundsCatcher cannot fight this!
            if (cardsInHand[i].TryGetComponent<Rigidbody>(out var rb)) {
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero;
            }

            float xPos = startX + (i * targetSpacing);
            float zPos = i * -cardThickness;

            Vector3 targetPos = transform.position
                              + (transform.right * xPos)
                              + (transform.forward * zPos);

            if (emphasizedCards.Contains(cardsInHand[i])) {
                targetPos += transform.up * emphasizeHeight;
            }

            Quaternion seatRot = transform.rotation;
            Quaternion standRot = Quaternion.Euler(standingRotation);
            Quaternion finalRot = seatRot * standRot;

            Transform cardTransform = cardsInHand[i].transform;

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
        }
    }

    protected override void OnOwnershipChanged(ulong previousOwner, ulong newOwner) {
        if (IsOwner && GameManager.Instance != null) {
            GameManager.Instance.myPlayerIndex = GameManager.Instance.allSeats.IndexOf(this);
        }
    }
}       