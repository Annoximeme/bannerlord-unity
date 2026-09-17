namespace Coop.Core.Network.Handshake
{
    /// <summary>
    /// Implements docs/NETWORK_PROTOCOL.md §5's negotiation policy exactly, including the DLC
    /// table. Pure and stateless on purpose — <see cref="RejectReason.Banned"/> and
    /// <see cref="RejectReason.ServerFull"/> depend on server-side state this function
    /// deliberately doesn't see, and are applied separately by the caller.
    /// </summary>
    public static class HandshakeValidator
    {
        public static HandshakeResult Validate(HelloMessage hello, ServerHandshakeConfig server)
        {
            if (hello.ProtocolVersion != server.ProtocolVersion)
            {
                return HandshakeResult.Reject(RejectReason.ProtocolMismatch);
            }

            if (hello.GameVersion != server.GameVersion || hello.GameVersionType != server.GameVersionType)
            {
                return HandshakeResult.Reject(RejectReason.GameVersionMismatch);
            }

            // docs/NETWORK_PROTOCOL.md §5 DLC policy table.
            if (server.NavalDlcPresent && !hello.NavalDlcPresent)
            {
                // Server on, client off: reject in Phase 1 — naval campaign state would be
                // unrepresentable on the client.
                return HandshakeResult.Reject(RejectReason.DlcMismatch);
            }
            if (server.NavalDlcPresent && hello.NavalDlcPresent &&
                server.NavalDlcVersion.HasValue && hello.NavalDlcVersion.HasValue &&
                server.NavalDlcVersion.Value != hello.NavalDlcVersion.Value)
            {
                // Both have it, but different builds — VERSION_SUPPORT.md §7.6: never mix
                // versions across a session.
                return HandshakeResult.Reject(RejectReason.DlcMismatch);
            }
            // Server off, client on: allowed, naval systems inert. Both off: standard co-op.

            if (hello.ModuleSetHash != server.ModuleSetHash)
            {
                return HandshakeResult.Reject(RejectReason.ModuleSetMismatch);
            }

            return HandshakeResult.Accept();
        }
    }
}
