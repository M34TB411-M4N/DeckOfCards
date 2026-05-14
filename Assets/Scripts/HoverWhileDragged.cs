using UnityEngine;
using Unity.Netcode; // Required for NetworkBehaviour

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class HoverWhileDragged : NetworkBehaviour { // CHANGED to NetworkBehaviour
    [Header("Debugging")]
    [SerializeField] private bool enableVerboseLogging = false;

    [Header("Hover Settings")]
    [SerializeField] private float hoverHeight = 5f;
    [SerializeField] private float responsiveness = 20f;
    [SerializeField] private LayerMask hoverSurfaceMask;
    [SerializeField] private float lateralUpdateThreshold = 0.05f;
    [SerializeField] private float updateInterval = 0.08f;
    [SerializeField] private float maxLookDistance = 50f;

    [Header("Safety Fallbacks")]
    [SerializeField] private float fallbackFloorY = 0f;

    private Rigidbody rb;
    private Collider col;

    private bool hovering = false;
    private bool savedUseGravity;
    private RigidbodyConstraints savedConstraints;
    private bool savedStateCaptured = false;

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
        if (!hovering) return;

        timeSinceLastUpdate += Time.fixedDeltaTime;
        bool needRecompute = false;

        if (timeSinceLastUpdate >= updateInterval)
            needRecompute = true;
        else {
            float lateralMoved = Vector2.Distance(
                new Vector2(lastLateralPosition.x, lastLateralPosition.z),
                new Vector2(rb.position.x, rb.position.z)
            );
            if (lateralMoved >= lateralUpdateThreshold) needRecompute = true;
        }

        if (needRecompute) {
            RecomputeHoverTarget();
            timeSinceLastUpdate = 0f;
            lastLateralPosition = rb.position;
        }

        float newY = Mathf.Lerp(rb.position.y, cachedTargetY, responsiveness * Time.fixedDeltaTime);
        Vector3 newPos = new Vector3(rb.position.x, newY, rb.position.z);
        rb.MovePosition(newPos);
    }

    public void BeginHover() {
        if (hovering) return;
        hovering = true;

        // THE INTERACTION FIX: Instantly seize ownership of the object from the other player!
        if (IsSpawned && !IsOwner) {
            RequestOwnershipServerRpc();
        }

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

        // VISIBILITY UPDATE: Tell the network this card is free and should show its face
        if (TryGetComponent<CardView>(out var cv)) {
            cv.NetworkUpdateTargetHand(-1);
        }
    }

    // This tells the server to transfer the ClientNetworkTransform authority to whoever just clicked!
    [ServerRpc(RequireOwnership = false)]
    private void RequestOwnershipServerRpc(ServerRpcParams rpcParams = default) {
        if (NetworkObject != null) {
            NetworkObject.ChangeOwnership(rpcParams.Receive.SenderClientId);
        }
    }

    public void EndHover() {
        if (!hovering) return;
        hovering = false;

        if (savedStateCaptured) {
            rb.constraints = savedConstraints;
            rb.useGravity = savedUseGravity; // Will correctly restore TRUE now!
            rb.isKinematic = false;
            savedStateCaptured = false;
        } else {
            rb.constraints = RigidbodyConstraints.None;
            rb.useGravity = true;
            rb.isKinematic = false;
        }

        Vector3 v = rb.linearVelocity;
        v.y = -10f;
        rb.linearVelocity = v;
    }

    private void RecomputeHoverTarget() {
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
            return;
        }

        Vector3 rayOrigin = new Vector3(b.center.x, b.center.y + 0.1f, b.center.z);
        RaycastHit[] rayHits = Physics.RaycastAll(rayOrigin, Vector3.down, searchDistance + 0.5f, hoverSurfaceMask, QueryTriggerInteraction.Ignore);

        float highestRayY = float.NegativeInfinity;
        Collider chosenRayCol = null;

        if (rayHits != null && rayHits.Length > 0) {
            foreach (var hit in rayHits) {
                if (hit.collider == col) continue;
                if (hit.collider.attachedRigidbody != null && hit.collider.attachedRigidbody == rb) continue;

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
            return;
        }

        cachedTargetY = fallbackFloorY + hoverHeight + b.extents.y;
        lastSurfaceCollider = null;
    }
}