using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class RelayManager : MonoBehaviour {
    public static RelayManager Instance { get; private set; }

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
        } else {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Keep this alive across all scenes!
        }
    }

    private async void Start() {
        // 1. Initialize Unity Services
        await UnityServices.InitializeAsync();

        // 2. Sign in silently if not already signed in
        if (!AuthenticationService.Instance.IsSignedIn) {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log($"<color=green>[Relay]</color> Signed in to Unity Cloud! Player ID: {AuthenticationService.Instance.PlayerId}");
        }
    }

    /// <summary>
    /// Starts a Host via Relay and returns the Join Code.
    /// </summary>
    public async Task<string> CreateRelay(int maxPlayers) {
        try {
            // Relay allocations DO NOT include the Host. 
            // So if you want 4 players total, you ask for 3 allocation slots.
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);

            // Get the 6-character Join Code
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log($"<color=cyan>[Relay]</color> Successfully created Relay! JOIN CODE: {joinCode}");

            // Configure our NetworkManager to use the Relay server instead of local IP
            RelayServerData relayServerData = allocation.ToRelayServerData("dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);
            // Start the actual Host
            NetworkManager.Singleton.StartHost();

            return joinCode;
        } catch (RelayServiceException e) {
            Debug.LogError($"<color=red>[Relay Error]</color> Failed to create Relay: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Connects a Client to a Host using a Join Code.
    /// </summary>
    public async Task<bool> JoinRelay(string joinCode) {
        try {
            Debug.Log($"<color=yellow>[Relay]</color> Attempting to join Relay with code: {joinCode}");

            // Ask Unity's cloud for the server info matching this code
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            // Configure our NetworkManager to connect to that Relay server
            RelayServerData relayServerData = joinAllocation.ToRelayServerData("dtls"); 
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            // Start the actual Client
            NetworkManager.Singleton.StartClient();

            return true;
        } catch (RelayServiceException e) {
            Debug.LogError($"<color=red>[Relay Error]</color> Failed to join Relay: {e.Message}");
            return false;
        }
    }
}