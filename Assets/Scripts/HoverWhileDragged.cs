using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class HoverWhileDragged : MonoBehaviour {
    [Header("Hover Settings")]
    [SerializeField] private float hoverHeight = 5f;
    [SerializeField] private float hoverResponsiveness = 25f;
    [SerializeField] private LayerMask hoverSurfaceMask;

    private Rigidbody rb;
    private Collider col;

    private bool isHovering = false;
    private bool previousUseGravity;

    private RigidbodyConstraints originalConstraints;

    void Awake() {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        originalConstraints = rb.constraints;
    }

    void FixedUpdate() {
        if (!isHovering)
            return;

        ApplyHover();
    }

    public void BeginHover() {
        if (isHovering)
            return;

        isHovering = true;

        previousUseGravity = rb.useGravity;
        rb.useGravity = false;

        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    public void EndHover() {
        if (!isHovering)
            return;

        isHovering = false;

        rb.useGravity = previousUseGravity;
        rb.constraints = originalConstraints;
    }

    private void ApplyHover() {
        Bounds bounds = col.bounds;

        Vector3 castCenter = bounds.center + Vector3.up * 0.1f;
        Vector3 castHalfExtents = new Vector3(
            bounds.extents.x * 0.95f,
            0.1f,
            bounds.extents.z * 0.95f
        );

        float castDistance = hoverHeight + bounds.size.y + 10f;

        if (Physics.BoxCast(
            castCenter,
            castHalfExtents,
            Vector3.down,
            out RaycastHit hit,
            Quaternion.identity,
            castDistance,
            hoverSurfaceMask,
            QueryTriggerInteraction.Ignore)) {
            float targetY =
                hit.point.y +
                hoverHeight +
                bounds.extents.y;

            Vector3 targetPosition = new Vector3(
                rb.position.x,
                targetY,
                rb.position.z
            );

            Vector3 newPosition = Vector3.Lerp(
                rb.position,
                targetPosition,
                hoverResponsiveness * Time.fixedDeltaTime
            );

            rb.MovePosition(newPosition);
        }
    }
}
