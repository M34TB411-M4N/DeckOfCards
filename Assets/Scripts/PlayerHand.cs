using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class PlayerHand : NetworkBehaviour {
    [Header("Layout Settings")]
    [SerializeField] private float cardWidth = 2.0f;
    [SerializeField] private float padding = 0.1f;
    [SerializeField] private float maxHandWidth = 10.0f;

    [Header("Visual Tweaks")]
    [SerializeField] private float cardThickness = 0.03f;
    [SerializeField] private Vector3 standingRotation = new Vector3(-90, 0, 0);
    [SerializeField] private float transitionSpeed = 10f;

    public List<CardView> cardsInHand = new List<CardView>();

    [Header("Camera Settings")]
    public Transform cameraAnchor;

    void Update() {
        ArrangeCards();
    }

    public void AddCard(CardView card) {
        if (card == null) return;

        if (!cardsInHand.Contains(card)) {
            cardsInHand.Add(card);

            if (GoFishManager.Instance != null) {
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

            // THE FIX: Do not turn physics back on if we are transferring cards in Go Fish!
            if (GoFishManager.Instance == null) {
                if (card.TryGetComponent<Rigidbody>(out var rb)) {
                    rb.isKinematic = false;
                }
                if (card.TryGetComponent<Collider>(out var col)) {
                    col.enabled = true;
                    col.isTrigger = false;
                }
            }
        }
    }

    private void SortHandByRank() {
        if (cardsInHand.Count <= 1) return;
        cardsInHand = cardsInHand
            .Where(c => c != null && c.GetCardData() != null)
            .OrderBy(c => c.GetCardData().rank)
            .ToList();
    }

    private void ArrangeCards() {
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
            float xPos = startX + (i * targetSpacing);
            float zPos = i * -cardThickness;

            Vector3 targetPos = transform.position
                              + (transform.right * xPos)
                              + (transform.forward * zPos);

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
            Debug.Log($"<color=green>[Network]</color> I am Local Player at Seat Index: {GameManager.Instance.myPlayerIndex}");
        }
    }
}