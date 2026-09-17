using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Coop.Core.Network
{
    /// <summary>
    /// The real transport chosen by RISK-02's resolution (docs/NETWORK_PROTOCOL.md §2):
    /// <c>System.Net.Sockets</c> directly, never <c>GameNetwork</c>. One instance is either a
    /// server (<see cref="StartServer"/>) or a client (<see cref="Connect"/>), not both.
    ///
    /// Message boundaries on the raw TCP stream use the same self-delimiting scheme as
    /// <see cref="Frame"/> (a leading <c>varint</c> length, then that many bytes) — this
    /// transport is otherwise unaware of <see cref="Channel"/>/message-type semantics; those
    /// are an L3 concern (ARCHITECTURE.md §3). <c>DeliveryMode.Unreliable</c> is not yet a
    /// separate (UDP) path — nothing needs it before C4 MissionState exists — so it currently
    /// behaves identically to <c>ReliableOrdered</c>, over the same TCP stream.
    /// </summary>
    public sealed class TcpCoopTransport : ICoopTransport, IDisposable
    {
        private TcpListener _listener;
        private TcpClient _clientSocket;

        private readonly object _sync = new object();
        private readonly Dictionary<int, TcpClient> _serverPeers = new Dictionary<int, TcpClient>();
        private int _nextPeerId = 1;
        private volatile bool _running;

        public event Action<PeerId, ArraySegment<byte>> Received;
        public event Action<PeerId> PeerConnected;
        public event Action<PeerId, DisconnectReason> PeerDisconnected;

        /// <summary>The actual bound port — useful when <see cref="StartServer"/> was called with 0 (let the OS pick one), e.g. in tests, to avoid fixed-port conflicts.</summary>
        public int BoundPort => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public void StartServer(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _running = true;

            var acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "Coop-Accept" };
            acceptThread.Start();
        }

        public void Connect(string host, int port)
        {
            _clientSocket = new TcpClient();
            _clientSocket.Connect(host, port);
            _running = true;

            var serverPeerId = new PeerId(0); // the server, from a client's point of view
            PeerConnected?.Invoke(serverPeerId);

            var receiveThread = new Thread(() => ReceiveLoop(_clientSocket.GetStream(), serverPeerId, ownerClient: null))
            {
                IsBackground = true,
                Name = "Coop-ClientRecv",
            };
            receiveThread.Start();
        }

        public void Send(PeerId to, ReadOnlySpan<byte> payload, DeliveryMode mode)
        {
            NetworkStream stream = ResolveStream(to);
            WriteDelimited(stream, payload);
        }

        public void Broadcast(ReadOnlySpan<byte> payload, DeliveryMode mode)
        {
            byte[] copy = payload.ToArray();
            List<TcpClient> targets;
            lock (_sync)
            {
                targets = new List<TcpClient>(_serverPeers.Values);
            }
            foreach (TcpClient client in targets)
            {
                try
                {
                    WriteDelimited(client.GetStream(), copy);
                }
                catch (IOException)
                {
                    // That peer's receive loop will observe the same failure and raise
                    // PeerDisconnected; a broadcast doesn't fail just because one peer dropped.
                }
                catch (SocketException)
                {
                }
            }
        }

        private NetworkStream ResolveStream(PeerId to)
        {
            if (_clientSocket != null)
            {
                return _clientSocket.GetStream();
            }

            lock (_sync)
            {
                if (_serverPeers.TryGetValue(to.Value, out TcpClient client))
                {
                    return client.GetStream();
                }
            }
            throw new InvalidOperationException($"No connected peer {to}.");
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                TcpClient client;
                try
                {
                    client = _listener.AcceptTcpClient();
                }
                catch (Exception)
                {
                    break; // Listener was stopped.
                }

                int id;
                lock (_sync)
                {
                    id = _nextPeerId++;
                    _serverPeers[id] = client;
                }

                var peerId = new PeerId(id);
                PeerConnected?.Invoke(peerId);

                var receiveThread = new Thread(() => ReceiveLoop(client.GetStream(), peerId, client))
                {
                    IsBackground = true,
                    Name = $"Coop-Recv-{id}",
                };
                receiveThread.Start();
            }
        }

        private void ReceiveLoop(NetworkStream stream, PeerId peerId, TcpClient ownerClient)
        {
            try
            {
                while (_running)
                {
                    byte[] payload = ReadDelimited(stream);
                    Received?.Invoke(peerId, new ArraySegment<byte>(payload));
                }
            }
            catch (Exception)
            {
                if (_running)
                {
                    PeerDisconnected?.Invoke(peerId, DisconnectReason.TransportError);
                }
            }
            finally
            {
                if (ownerClient != null)
                {
                    lock (_sync)
                    {
                        _serverPeers.Remove(peerId.Value);
                    }
                }
            }
        }

        private static void WriteDelimited(Stream stream, ReadOnlySpan<byte> payload)
        {
            byte[] buffer = payload.ToArray();
            lock (stream) // guards interleaved writes from Send + Broadcast on the same stream
            {
                VarInt.WriteUInt32(stream, (uint)buffer.Length);
                stream.Write(buffer, 0, buffer.Length);
                stream.Flush();
            }
        }

        private static byte[] ReadDelimited(Stream stream)
        {
            uint length = VarInt.ReadUInt32(stream);
            var buffer = new byte[length];
            Frame.ReadExactly(stream, buffer);
            return buffer;
        }

        public void Dispose()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
            try { _clientSocket?.Close(); } catch { }

            lock (_sync)
            {
                foreach (TcpClient client in _serverPeers.Values)
                {
                    try { client.Close(); } catch { }
                }
                _serverPeers.Clear();
            }
        }
    }
}
