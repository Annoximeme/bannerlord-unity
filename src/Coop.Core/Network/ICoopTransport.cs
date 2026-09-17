using System;

namespace Coop.Core.Network
{
    /// <summary>
    /// docs/NETWORK_PROTOCOL.md §2. L2 of ARCHITECTURE.md §3 — deliberately an abstraction,
    /// so nothing above it depends on the RISK-02 answer (RESOLVED: not <c>GameNetwork</c>).
    /// Channel-agnostic on purpose: it moves opaque byte payloads. <see cref="Channel"/> and
    /// message framing (<see cref="Frame"/>) are an L3 concern built on top — a caller that
    /// wants channel semantics encodes a <see cref="Frame"/> and passes its bytes as the
    /// payload here.
    /// </summary>
    public interface ICoopTransport
    {
        void StartServer(int port);
        void Connect(string host, int port);
        void Send(PeerId to, ReadOnlySpan<byte> payload, DeliveryMode mode);
        void Broadcast(ReadOnlySpan<byte> payload, DeliveryMode mode);

        event Action<PeerId, ArraySegment<byte>> Received;
        event Action<PeerId> PeerConnected;
        event Action<PeerId, DisconnectReason> PeerDisconnected;
    }
}
