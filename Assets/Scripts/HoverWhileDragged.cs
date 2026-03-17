using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class HoverWhileDragged : MonoBehaviour {
    [Header("Debugging")]
    [Tooltip("Turn this on to spam the console with exactly what this script is hitting!")]
    [SerializeField] private bool enableVerboseLogging = false;

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

    [Header("Safety Fallbacks")]
    [Tooltip("If the card is dragged completely off the table into the abyss, assume the floor is at this Y level.")]
    [SerializeField] private float fallbackFloorY = 0f;

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

        if (enableVerboseLogging && newY > 10f) {
            Debug.LogWarning($"<color=red>[Hover Warning]</color> {gameObject.name} is flying very high! Current Y: {newY}, Target Y: {cachedTargetY}");
        }
    }

    public void BeginHover() {
        if (hovering) return;
        hovering = true;

        if (enableVerboseLogging) Debug.Log($"<color=magenta>[Hover Start]</color> {gameObject.name} picked up.");

        if (!savedStateCaptured) {
            savedUseGravity = rb.useGravity;
            savedConstraints = rb.constraints;
            savedStateCaptured = true;
        }

        float currentZ = transform.eulerAngles.z;
        float snappedZ = (currentZ > 90f && currentZ < 270f) ? 180f : 0f;
        transform.rotation = Quaternion.Euler(0f, 0f, snappedZ);

        rb.useGravity = false;
        rb.constraints = savedConstraints | RigidbodyConstraints.FreezeRotation;

        RecomputeHoverTarget();
        lastLateralPosition = rb.position;
        timeSinceLastUpdate = 0f;
    }

    public void EndHover() {
        if (!hovering) return;
        hovering = false;

        if (enableVerboseLogging) Debug.Log($"<color=magenta>[Hover End]</color> {gameObject.name} dropped.");

        if (savedStateCaptured) {
            rb.constraints = savedConstraints;
            rb.useGravity = savedUseGravity;
            savedStateCaptured = false;
        } else {
            rb.constraints = RigidbodyConstraints.None;
            rb.useGravity = true;
        }

        Vector3 v = rb.linearVelocity;
        v.y = -10f;
        rb.linearVelocity = v;
    }

    private void RecomputeHoverTarget() {
        if (enableVerboseLogging) Debug.Log("<color=yellow>--- Recomputing Hover Target ---</color>");
        Bounds b = col.bounds;

        float halfX = Mathf.Max(0.01f, b.extents.x * 0.95f);
        float halfZ = Mathf.Max(0.01f, b.extents.z * 0.95f);
        float searchDistance = hoverHeight + b.size.y + maxLookDistance;

        float boxHalfY = Mathf.Max(0.1f, searchDistance * 0.5f);
        Vector3 boxCenter = new Vector3(b.center.x, b.min.y - boxHalfY, b.center.z);
        Vector3 boxHalfExtents = new Vector3(halfX, boxHalfY, halfZ);

        Collider[] hits = Physics.OverlapBox(boxCenter, boxHalfExtents, Quaternion.identity, hoverSurfaceMask, QueryTriggerInteraction.Ignore);

        float highestTopY = float.NegativeInfinity;
        Collider chosen = null;

        if (hits != null && hits.Length > 0) {
            foreach (var c in hits) {
                if (c == col) continue;
                if (c.attachedRigidbody != null && c.attachedRigidbody == rb) continue;

                if (enableVerboseLogging) Debug.Log($"<color=cyan>[OverlapBox Hit]</color> Found: {c.gameObject.name} (Layer: {LayerMask.LayerToName(c.gameObject.layer)})");

                float topY = c.bounds.max.y;
                if (topY > highestTopY) {
                    highestTopY = topY;
                    chosen = c;
                }
            }
        }

        if (chosen != null) {
            cachedTargetY = highestTopY + hoverHeight + b.extents.y;
            lastSurfaceCollider = chosen;
            if (enableVerboseLogging) Debug.Log($"<color=green>[Hover Success]</color> Box locked onto {chosen.name}. Target Y set to {cachedTargetY}");
            return;
        }

        if (enableVerboseLogging) Debug.Log("<color=orange>[Hover Info]</color> OverlapBox found nothing valid. Firing downward RaycastAll...");

        // THE FIX: Use RaycastAll so we can filter out the card itself!
        Vector3 rayOrigin = new Vector3(b.center.x, b.center.y + 0.1f, b.center.z);
        RaycastHit[] rayHits = Physics.RaycastAll(rayOrigin, Vector3.down, searchDistance + 0.5f, hoverSurfaceMask, QueryTriggerInteraction.Ignore);

        float highestRayY = float.NegativeInfinity;
        Collider chosenRayCol = null;

        if (rayHits != null && rayHits.Length > 0) {
            foreach (var hit in rayHits) {
                // IGNORE OURSELVES!
                if (hit.collider == col) continue;
                if (hit.collider.attachedRigidbody != null && hit.collider.attachedRigidbody == rb) continue;

                if (enableVerboseLogging) Debug.Log($"<color=cyan>[Raycast Hit]</color> Hit: {hit.collider.gameObject.name} (Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)})");

                float topY = hit.collider.bounds.max.y;
                if (hit.point.y > topY + 0.001f) topY = hit.point.y;

                if (topY > highestRayY) {
                    highestRayY = topY;
                    chosenRayCol = hit.collider;
                }
            }
        }

        if (chosenRayCol != null) {
            cachedTargetY = highestRayY + hoverHeight + b.extents.y;
            lastSurfaceCollider = chosenRayCol;
            if (enableVerboseLogging) Debug.Log($"<color=green>[Hover Success]</color> Raycast locked onto {chosenRayCol.name}. Target Y set to {cachedTargetY}");
            return;
        }

        // The Abyss Fallback
        cachedTargetY = fallbackFloorY + hoverHeight + b.extents.y;
        lastSurfaceCollider = null;
        if (enableVerboseLogging) Debug.Log($"<color=red>[Hover Fallback]</color> Hit absolutely nothing! Engaging Abyss Fallback. Target Y set to {cachedTargetY}");
    }

    void OnDrawGizmosSelected() {
        if (col == null) return;
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