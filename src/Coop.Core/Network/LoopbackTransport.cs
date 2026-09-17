using System;

namespace Coop.Core.Network
{
    /// <summary>
    /// In-memory <see cref="ICoopTransport"/> for testing everything above L2 before the real
    /// transport exists or without needing real sockets — docs/NETWORK_PROTOCOL.md §2:
    /// "Phase 1 ships a loopback implementation first." Delivery is synchronous.
    /// </summary>
    public sealed class LoopbackTransport : ICoopTransport
    {
        public static readonly PeerId ServerSidePeerId = new PeerId(1);
        public static readonly PeerId ClientSidePeerId = new PeerId(2);

        /// <summary>How this instance's own subscribers should refer to the peer on the other end.</summary>
        public PeerId RemotePeerId { get; }

        private LoopbackTransport _other;

        public event Action<PeerId, ArraySegment<byte>> Received;
        public event Action<PeerId> PeerConnected;
        public event Action<PeerId, DisconnectReason> PeerDisconnected;

        private LoopbackTransport(PeerId remotePeerId)
        {
            RemotePeerId = remotePeerId;
        }

        /// <summary>Creates two already-linked instances — no separate StartServer/Connect call needed to wire them together, though calling Connect still raises PeerConnected on both sides.</summary>
        public static (ICoopTransport Server, ICoopTransport Client) CreateConnectedPair()
        {
            var server = new LoopbackTransport(ClientSidePeerId);
            var client = new LoopbackTransport(ServerSidePeerId);
            server._other = client;
            client._other = server;
            return (server, client);
        }

        public void StartServer(int port)
        {
            // No-op: the pair is already linked by CreateConnectedPair.
        }

        public void Connect(string host, int port)
        {
            PeerConnected?.Invoke(RemotePeerId);
            _other.PeerConnected?.Invoke(_other.RemotePeerId);
        }

        public void Send(PeerId to, ReadOnlySpan<byte> payload, DeliveryMode mode) => Deliver(payload);

        public void Broadcast(ReadOnlySpan<byte> payload, DeliveryMode mode) => Deliver(payload);

        public void SimulateDisconnect(DisconnectReason reason)
        {
            PeerDisconnected?.Invoke(RemotePeerId, reason);
            _other.PeerDisconnected?.Invoke(_other.RemotePeerId, reason);
        }

        private void Deliver(ReadOnlySpan<byte> payload)
        {
            byte[] copy = payload.ToArray();
            _other.Received?.Invoke(_other.RemotePeerId, new ArraySegment<byte>(copy));
        }
    }
}
