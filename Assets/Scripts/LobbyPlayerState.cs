using Unity.Collections;
using Unity.Netcode;

public struct LobbyPlayerState : INetworkSerializable, System.IEquatable<LobbyPlayerState> {
    public ulong ClientId;
    public FixedString32Bytes PlayerName; // FixedString is required for NetworkVariables
    public bool IsReady;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref PlayerName);
        serializer.SerializeValue(ref IsReady);
    }

    public bool Equals(LobbyPlayerState other) {
        return ClientId == other.ClientId && PlayerName == other.PlayerName && IsReady == other.IsReady;
    }
}