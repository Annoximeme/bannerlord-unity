namespace Coop.Core.Network
{
    /// <summary>docs/NETWORK_PROTOCOL.md §3. Only C0's message types (the handshake) exist so far; C1–C5 arrive with the systems that use them.</summary>
    public enum Channel : byte
    {
        Control = 0,
        Snapshot = 1,
        CampaignDelta = 2,
        Command = 3,
        MissionState = 4,
        MissionEvent = 5,
    }
}
