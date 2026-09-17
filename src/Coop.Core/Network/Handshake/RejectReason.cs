namespace Coop.Core.Network.Handshake
{
    /// <summary>docs/NETWORK_PROTOCOL.md §5's exact rejection reason list.</summary>
    public enum RejectReason
    {
        ProtocolMismatch,
        GameVersionMismatch,
        DlcMismatch,
        ModuleSetMismatch,
        Banned,
        ServerFull,
    }
}
