namespace Coop.Core.Network
{
    public enum DisconnectReason
    {
        ClientRequested,
        Timeout,
        Kicked,
        Banned,
        ServerShutdown,
        TransportError,
    }
}
