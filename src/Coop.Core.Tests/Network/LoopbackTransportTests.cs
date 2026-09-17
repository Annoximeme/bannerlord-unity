using System;
using System.Text;
using Coop.Core.Network;
using Xunit;

namespace Coop.Core.Tests.Network
{
    public class LoopbackTransportTests
    {
        [Fact]
        public void Connect_RaisesPeerConnectedOnBothSides()
        {
            (ICoopTransport server, ICoopTransport client) = LoopbackTransport.CreateConnectedPair();
            PeerId? serverSaw = null;
            PeerId? clientSaw = null;
            server.PeerConnected += p => serverSaw = p;
            client.PeerConnected += p => clientSaw = p;

            client.Connect("localhost", 0);

            Assert.Equal(LoopbackTransport.ClientSidePeerId, serverSaw);
            Assert.Equal(LoopbackTransport.ServerSidePeerId, clientSaw);
        }

        [Fact]
        public void Send_DeliversPayloadToTheOtherSide_WithCorrectSenderPeerId()
        {
            (ICoopTransport server, ICoopTransport client) = LoopbackTransport.CreateConnectedPair();
            byte[] received = null;
            PeerId? fromPeer = null;
            server.Received += (from, data) => { fromPeer = from; received = data.ToArray(); };

            byte[] payload = Encoding.UTF8.GetBytes("ping");
            client.Send(LoopbackTransport.ServerSidePeerId, payload, DeliveryMode.ReliableOrdered);

            Assert.Equal(payload, received);
            Assert.Equal(LoopbackTransport.ClientSidePeerId, fromPeer);
        }

        [Fact]
        public void Broadcast_AlsoDeliversToTheOtherSide()
        {
            (ICoopTransport server, ICoopTransport client) = LoopbackTransport.CreateConnectedPair();
            byte[] received = null;
            client.Received += (_, data) => received = data.ToArray();

            byte[] payload = Encoding.UTF8.GetBytes("world-state");
            server.Broadcast(payload, DeliveryMode.ReliableOrdered);

            Assert.Equal(payload, received);
        }

        [Fact]
        public void SimulateDisconnect_RaisesPeerDisconnectedOnBothSides()
        {
            (ICoopTransport server, ICoopTransport client) = LoopbackTransport.CreateConnectedPair();
            DisconnectReason? serverSaw = null;
            server.PeerDisconnected += (_, reason) => serverSaw = reason;

            ((LoopbackTransport)client).SimulateDisconnect(DisconnectReason.ClientRequested);

            Assert.Equal(DisconnectReason.ClientRequested, serverSaw);
        }
    }
}
