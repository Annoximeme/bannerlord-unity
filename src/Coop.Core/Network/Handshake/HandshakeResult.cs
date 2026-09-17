namespace Coop.Core.Network.Handshake
{
    public sealed class HandshakeResult
    {
        public bool Accepted { get; }
        public RejectReason? Reason { get; }

        private HandshakeResult(bool accepted, RejectReason? reason)
        {
            Accepted = accepted;
            Reason = reason;
        }

        public static HandshakeResult Accept() => new HandshakeResult(true, null);
        public static HandshakeResult Reject(RejectReason reason) => new HandshakeResult(false, reason);
    }
}
