namespace Coop.Core.Network.Handshake
{
    /// <summary>Mirrors TaleWorlds.Library.ApplicationVersionType (VERIFIED, all five real values) without Coop.Core referencing it directly.</summary>
    public enum GameVersionType
    {
        Invalid,
        Alpha,
        Beta,
        EarlyAccess,
        Release,
        Development,
    }
}
