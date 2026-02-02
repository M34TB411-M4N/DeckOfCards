using UnityEngine;

// Attach this to movable prefabs (cards, decks).
// While colliding with a dragged object, this will damp the receiving object's
// linear velocity every physics step. When the collision ends, we zero the
// linear velocity to prevent any residual glide. Rotation is preserved.
[RequireComponent(typeof(Rigidbody))]
public class StopAfterCollision : MonoBehaviour {
    [Tooltip("Fraction to keep each physics step while in contact. 0.5 means cut velocity in half each step.")]
    [Range(0f, 1f)]
    public float linearDampingDuringContact = 0.45f;

    private Rigidbody rb;

    void Awake() {
        rb = GetComponent<Rigidbody>();
    }

    // Called each physics step while colliders are touching
    void OnCollisionStay(Collision collision) {
        if (collision == null || collision.rigidbody == null)
            return;

        // If the other body is the dragged object (has DraggedMarker), damp translation
        if (collision.rigidbody.GetComponent<DraggedMarker>() != null) {
            // Only affect linear velocity (translation). Do not zero rotation.
            Vector3 lv = rb.linearVelocity;

            // Apply damping - reduces translational energy gradually.
            // We write linearVelocity directly so the solver sees lower translational speed
            rb.linearVelocity = lv * linearDampingDuringContact;
        }
    }

    // Called once after the colliders separate
    void OnCollisionExit(Collision collision) {
        if (collision == null || collision.rigidbody == null)
            return;

        if (collision.rigidbody.GetComponent<DraggedMarker>() != null) {
            // Final safety: zero translational velocity immediately when contact ends.
            // Preserve angularVelocity so rotation continues naturally from torque.
            rb.linearVelocity = Vector3.zero;
        }
    }
}
