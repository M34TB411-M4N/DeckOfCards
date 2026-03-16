using UnityEngine;
using Unity.Netcode;

public class OutOfBoundsCatcher : NetworkBehaviour {
    [Tooltip("Drag an empty GameObject here to act as the teleport destination")]
    public Transform resetPoint;

    private void OnTriggerEnter(Collider other) {
        // ONLY the Server is legally allowed to teleport networked objects!
        if (!IsServer) return;

        // Check if the object falling is a card, deck, or pile
        if (other.CompareTag("MoveableObject")) {

            // Grab the NetworkObject ID
            NetworkObject netObj = other.GetComponent<NetworkObject>();
            if (netObj == null) netObj = other.GetComponentInParent<NetworkObject>();

            if (netObj != null) {
                // 1. Teleport it back to the center of the table
                netObj.transform.position = resetPoint.position;

                // 2. Clear its rotation so it lands flat
                netObj.transform.rotation = Quaternion.identity;

                // 3. Kill all physics momentum so it doesn't bounce away again!
                if (netObj.TryGetComponent<Rigidbody>(out var rb)) {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                Debug.Log($"<color=red>[Safety Net]</color> Rescued {netObj.name} from falling off the map!");
            }
        }
    }
}