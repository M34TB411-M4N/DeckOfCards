using UnityEngine;
using Unity.Netcode;

public class OutOfBoundsCatcher : NetworkBehaviour {
    [Tooltip("Drag an empty GameObject here to act as the teleport destination")]
    public Transform resetPoint;

    // THE FIX: Change Enter to Exit! 
    // Now this acts as a "Safe Zone Boundary" instead of a catcher trap.
    private void OnTriggerExit(Collider other) {
        if (!IsServer) return;

        // THE FIX: Ignore phantom physics rebuilds. 
        // If the object is within the boundary but its collider was just disabled/destroyed, ignore it!
        if (!other.gameObject.activeInHierarchy || !other.enabled) {
            return;
        }

        if (other.CompareTag("MoveableObject")) {
            NetworkObject netObj = other.GetComponent<NetworkObject>();
            if (netObj == null) netObj = other.GetComponentInParent<NetworkObject>();

            if (netObj != null) {
                Vector3 exitPos = other.transform.position;
                Vector3 exitScale = other.transform.lossyScale;

                if (other.TryGetComponent<Rigidbody>(out var rb) && rb.isKinematic) {
                    Debug.Log($"<color=yellow>[Safety Net]</color> {netObj.name} exited at Pos: {exitPos} with Scale: {exitScale}. Ignored because it is Kinematic.");
                    return;
                }

                netObj.transform.position = resetPoint.position;
                netObj.transform.rotation = Quaternion.identity;

                if (rb != null) {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                Debug.Log($"<color=red>[Safety Net TRIPPED]</color> Rescued {netObj.name}.\n" +
                          $"-> Exact Exit Pos: {exitPos}\n" +
                          $"-> Exact Scale: {exitScale}\n" +
                          $"-> Target Reset Pos: {resetPoint.position}");
            }
        }
    }
}