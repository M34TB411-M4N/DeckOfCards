using Unity.Netcode.Components;
using UnityEngine;

// This tiny script overrides Unity's default NetworkTransform to allow whoever "Owns" the object to move it!
[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform {
    protected override bool OnIsServerAuthoritative() {
        return false;
    }
}