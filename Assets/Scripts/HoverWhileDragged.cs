using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class HoverWhileDragged : MonoBehaviour {
    [Header("Hover Settings")]
    [Tooltip("Base hover height above the top surface of whatever is below this object.")]
    [SerializeField] private float hoverHeight = 5f;

    [Tooltip("How quickly the object moves toward the cached target Y.")]
    [SerializeField] private float responsiveness = 20f;

    [Tooltip("Layers considered as hover surfaces (table, decks, cards).")]
    [SerializeField] private LayerMask hoverSurfaceMask;

    [Tooltip("Minimum lateral movement (meters) to force recompute of surface below.")]
    [SerializeField] private float lateralUpdateThreshold = 0.05f;

    [Tooltip("Seconds between forced updates even if lateral movement is small.")]
    [SerializeField] private float updateInterval = 0.08f;

    // how far down to look in case there's a big drop (safety)
    [SerializeField] private float maxLookDistance = 50f;

    private Rigidbody rb;
    private Collider col;

    // hover state
    private bool hovering = false;
    private bool savedUseGravity;
    private RigidbodyConstraints savedConstraints;
    private bool savedStateCaptured = false;

    // cached hover target state
    private float cachedTargetY;
    private Vector3 lastLateralPosition;
    private float timeSinceLastUpdate = 0f;
    private Collider lastSurfaceCollider = null;

    void Awake() {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        cachedTargetY = rb.position.y;
        lastLateralPosition = rb.position;
    }

    void FixedUpdate() {
        if (!hovering)
            return;

        timeSinceLastUpdate += Time.fixedDeltaTime;

        bool needRecompute = false;

        if (timeSinceLastUpdate >= updateInterval)
            needRecompute = true;
        else {
            float lateralMoved = Vector2.Distance(
                new Vector2(lastLateralPosition.x, lastLateralPosition.z),
                new Vector2(rb.position.x, rb.position.z)
            );
            if (lateralMoved >= lateralUpdateThreshold)
                needRecompute = true;
        }

        if (needRecompute) {
            RecomputeHoverTarget();
            timeSinceLastUpdate = 0f;
            lastLateralPosition = rb.position;
        }

        // Smoothly move vertically toward the cached target Y
        float newY = Mathf.Lerp(rb.position.y, cachedTargetY, responsiveness * Time.fixedDeltaTime);
        Vector3 newPos = new Vector3(rb.position.x, newY, rb.position.z);
        rb.MovePosition(newPos);
    }

    /// <summary>
    /// Call when the object begins being dragged.
    /// </summary>
    public void BeginHover() {
        if (hovering)
            return;

        hovering = true;

        // Capture saved state exactly once per hover.
        if (!savedStateCaptured) {
            savedUseGravity = rb.useGravity;
            savedConstraints = rb.constraints;
            savedStateCaptured = true;
        }

        // Disable gravity and freeze Y + rotation while hovered so solver cannot touch Y
        rb.useGravity = false;
        rb.constraints = savedConstraints | RigidbodyConstraints.FreezeRotation;

        // compute initial target immediately
        RecomputeHoverTarget();
        lastLateralPosition = rb.position;
        timeSinceLastUpdate = 0f;
    }

    /// <summary>
    /// Call when the object stops being dragged.
    /// </summary>
    public void EndHover() {
        if (!hovering)
            return;

        hovering = false;

        // Restore saved state (only if we captured it)
        if (savedStateCaptured) {
            rb.constraints = savedConstraints;
            rb.useGravity = savedUseGravity;
            savedStateCaptured = false;
        } else {
            // Safe fallback
            rb.constraints = RigidbodyConstraints.None;
            rb.useGravity = true;
        }

        // small downward nudge so gravity starts affecting the object immediately
        Vector3 v = rb.linearVelocity;
        v.y = -10f;
        rb.linearVelocity = v;
    }

    /// <summary>
    /// Robust detection of highest surface beneath the object's footprint.
    /// Uses OverlapBox to find any colliders under the footprint first (best for partial overlap).
    /// If none are found, performs a downward Raycast from the object's center to find table or distant surfaces.
    /// Only updates cachedTargetY when a real surface is found (prevents spurious fallback to table).
    /// </summary>
    private void RecomputeHoverTarget() {
        Bounds b = col.bounds;

        // X/Z extents slightly reduced to avoid edge self-hits
        float halfX = Mathf.Max(0.01f, b.extents.x * 0.95f);
        float halfZ = Mathf.Max(0.01f, b.extents.z * 0.95f);

        // how far down we search (include base hover + object height + cushion)
        float searchDistance = hoverHeight + b.size.y + maxLookDistance;

        // Build an OverlapBox that extends downward from just below the object's bottom
        // Center it halfway down the search distance below b.min.y
        float boxHalfY = Mathf.Max(0.1f, searchDistance * 0.5f);
        Vector3 boxCenter = new Vector3(b.center.x, b.min.y - boxHalfY, b.center.z);
        Vector3 boxHalfExtents = new Vector3(halfX, boxHalfY, halfZ);

        Collider[] hits = Physics.OverlapBox(boxCenter, boxHalfExtents, Quaternion.identity, hoverSurfaceMask, QueryTriggerInteraction.Ignore);

        float highestTopY = float.NegativeInfinity;
        Collider chosen = null;

        // process overlap hits
        if (hits != null && hits.Length > 0) {
            foreach (var c in hits) {
                if (c == col) // ignore self
                    continue;
                // ignore if attached to same rigidbody
                if (c.attachedRigidbody != null && c.attachedRigidbody == rb)
                    continue;

                // get top y from bounds (robust)
                float topY = c.bounds.max.y;
                if (topY > highestTopY) {
                    highestTopY = topY;
                    chosen = c;
                }
            }
        }

        // if OverlapBox found something, set target based on that
        if (chosen != null) {
            cachedTargetY = highestTopY + hoverHeight + b.extents.y;
            lastSurfaceCollider = chosen;
            return;
        }

        // Otherwise, fallback to a downward raycast from center (will detect table or distant surface)
        Vector3 rayOrigin = new Vector3(b.center.x, b.center.y + 0.1f, b.center.z);
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit rayHit, searchDistance + 0.5f, hoverSurfaceMask, QueryTriggerInteraction.Ignore)) {
            // Use either the collider bounds top (if collider has reasonable bounds) or the actual hit point
            Collider rc = rayHit.collider;
            float topY = rc.bounds.max.y;

            // if hit point is higher than bounds.max.y use hit.point.y (rare cases)
            if (rayHit.point.y > topY + 0.001f)
                topY = rayHit.point.y;

            cachedTargetY = topY + hoverHeight + b.extents.y;
            lastSurfaceCollider = rc;
            return;
        }

        // No surface found beneath us — do NOT snap to table blindly.
        // Keep existing cachedTargetY (so object doesn't suddenly drop to table).
        // Optionally, you could slowly move down over time if you want a fallback behavior:
        // cachedTargetY = Mathf.Max(cachedTargetY - 0.05f, someMinY);
        // For now: do nothing (prevents spurious drops).
    }

    // Debug: visualize the overlap/box cast volume in the editor
    void OnDrawGizmosSelected() {
        if (col == null)
            return;

        Bounds b = col.bounds;

        float halfX = Mathf.Max(0.01f, b.extents.x * 0.95f);
        float halfZ = Mathf.Max(0.01f, b.extents.z * 0.95f);
        float searchDistance = hoverHeight + b.size.y + maxLookDistance;
        float boxHalfY = Mathf.Max(0.1f, searchDistance * 0.5f);
        Vector3 boxCenter = new Vector3(b.center.x, b.min.y - boxHalfY, b.center.z);
        Vector3 boxSize = new Vector3(halfX * 2f, boxHalfY * 2f, halfZ * 2f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(boxCenter, boxSize);
    }
}
