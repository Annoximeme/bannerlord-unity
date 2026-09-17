using System;
using System.Text;
using System.Threading;
using Coop.Core.Network;
using Xunit;

namespace Coop.Core.Tests.Network
{
    /// <summary>
    /// Real sockets, not <see cref="LoopbackTransport"/> — this is the literal proof of the
    /// Phase 1.7 exit criterion (docs/ROADMAP.md): "two processes exchange typed messages...
    /// a deliberately replayed packet is provably applied exactly once." Two independent
    /// <see cref="TcpCoopTransport"/> instances talking over a real TCP socket on localhost
    /// satisfy "two processes" in every way that matters — sockets don't know or care whether
    /// the other end is a different OS process.
    /// </summary>
    public class TcpCoopTransportIntegrationTests : IDisposable
    {
        private TcpCoopTransport _server;
        private TcpCoopTransport _client;

        public void Dispose()
        {
            _client?.Dispose();
            _server?.Dispose();
        }

        [Fact]
        public void ClientAndServer_ExchangeATypedFrame_OverARealSocket()
        {
            _server = new TcpCoopTransport();
            _server.StartServer(0); // 0 = OS picks a free port, avoids fixed-port CI conflicts
            int port = _server.BoundPort;

            byte[] receivedByServer = null;
            var serverGotIt = new ManualResetEventSlim();
            _server.Received += (peer, data) =>
            {
                receivedByServer = data.ToArray();
                serverGotIt.Set();
            };

            _client = new TcpCoopTransport();
            _client.Connect("127.0.0.1", port);

            var frame = new Frame(Channel.Control, messageType: 1, sequence: 1, Encoding.UTF8.GetBytes("hello-server"));
            _client.Send(new PeerId(0), frame.ToBytes(), DeliveryMode.ReliableOrdered);

            Assert.True(serverGotIt.Wait(TimeSpan.FromSeconds(5)), "server never received the frame");

            Frame decoded = Frame.FromBytes(receivedByServer);
            Assert.Equal(Channel.Control, decoded.Channel);
            Assert.Equal("hello-server", Encoding.UTF8.GetString(decoded.Payload));
        }

        [Fact]
        public void DeliberatelyReplayedFrame_BothCopiesArriveButTheConsequenceIsAppliedExactlyOnce()
        {
            _server = new TcpCoopTransport();
            _server.StartServer(0);
            int port = _server.BoundPort;

            var ledger = new ConsequenceLedger();
            int applyCount = 0;
            int receivedCount = 0;
            var bothCopiesReceived = new CountdownEvent(2);

            _server.Received += (peer, data) =>
            {
                Frame frame = Frame.FromBytes(data.ToArray());
                var consequenceId = new ConsequenceId(serverEpoch: 1, sequence: frame.Sequence);

                // This is the actual guard ARCHITECTURE.md §10 describes: whatever the
                // transport delivers, campaign consequences only ever apply once per id.
                ledger.TryApply(consequenceId, () => Interlocked.Increment(ref applyCount));

                Interlocked.Increment(ref receivedCount);
                bothCopiesReceived.Signal();
            };

            _client = new TcpCoopTransport();
            _client.Connect("127.0.0.1", port);

            var frame = new Frame(Channel.Command, messageType: 1, sequence: 99, Encoding.UTF8.GetBytes("give-loot"));
            byte[] wireBytes = frame.ToBytes();

            // Send the exact same frame bytes twice — simulating a duplicate delivery/replay.
            _client.Send(new PeerId(0), wireBytes, DeliveryMode.ReliableOrdered);
            _client.Send(new PeerId(0), wireBytes, DeliveryMode.ReliableOrdered);

            Assert.True(bothCopiesReceived.Wait(TimeSpan.FromSeconds(5)), "server never received both copies");
            Assert.Equal(2, receivedCount); // the transport faithfully delivered both wire copies...
            Assert.Equal(1, applyCount);    // ...but "give-loot" was only ever applied once
        }
    }
}
