using Coop.Core.Versioning;

namespace Coop.Core.Network.Handshake
{
    /// <summary>What a joining client's <see cref="HelloMessage"/> is validated against.</summary>
    public sealed class ServerHandshakeConfig
    {
        public uint ProtocolVersion { get; }
        public GameVersion GameVersion { get; }
        public GameVersionType GameVersionType { get; }
        public string ModuleSetHash { get; }
        public bool NavalDlcPresent { get; }
        public GameVersion? NavalDlcVersion { get; }

        public ServerHandshakeConfig(
            uint protocolVersion,
            GameVersion gameVersion,
            GameVersionType gameVersionType,
            string moduleSetHash,
            bool navalDlcPresent,
            GameVersion? navalDlcVersion)
        {
            ProtocolVersion = protocolVersion;
            GameVersion = gameVersion;
            GameVersionType = gameVersionType;
            ModuleSetHash = moduleSetHash;
            NavalDlcPresent = navalDlcPresent;
            NavalDlcVersion = navalDlcVersion;
        }
    }
}
