using UnityEngine;
using UnityEditor;
using Unity.Netcode;

public class FindHashTool : EditorWindow {

    [MenuItem("Tools/Find Network Hash")]
    public static void FindHash() {
        // This scans everything: the open scene, hidden objects, and your project prefabs
        NetworkObject[] allNetObjs = Resources.FindObjectsOfTypeAll<NetworkObject>();
        bool found = false;

        Debug.Log("<color=yellow>--- SCANNING FOR HASH 1610850345 ---</color>");

        foreach (var netObj in allNetObjs) {
            // We use SerializedObject to bypass your code API and read the raw Inspector data
            SerializedObject serializedObj = new SerializedObject(netObj);
            SerializedProperty hashProp = serializedObj.FindProperty("GlobalObjectIdHash");
            SerializedProperty prefabHashProp = serializedObj.FindProperty("PrefabHash");

            uint foundHash = 0;
            if (hashProp != null) foundHash = hashProp.uintValue;
            else if (prefabHashProp != null) foundHash = prefabHashProp.uintValue;

            if (foundHash == 1610850345) {
                found = true;
                // Ping the object so it highlights in your Unity Editor
                EditorGUIUtility.PingObject(netObj.gameObject);
                Debug.LogError($"<color=green><b>FOUND IT! The culprit is: {netObj.gameObject.name}</b></color>");
            }
        }

        if (!found) {
            Debug.Log("<color=red>Hash not found in current scene or prefabs.</color>");
        }
    }
}