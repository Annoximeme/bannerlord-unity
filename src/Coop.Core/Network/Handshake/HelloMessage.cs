using Coop.Core.Versioning;

namespace Coop.Core.Network.Handshake
{
    /// <summary>docs/NETWORK_PROTOCOL.md §5: <c>C→S Hello { protocolVersion, gameVersion, gameVersionType, moduleSet, navalDlcBuild?, playerIdentity }</c>.</summary>
    public sealed class HelloMessage
    {
        public uint ProtocolVersion { get; }
        public GameVersion GameVersion { get; }
        public GameVersionType GameVersionType { get; }

        /// <summary>Negotiated as a hash, not the raw module list — actually hashing the real module set (RISK-16's mitigation) is Coop.GameInterface's job.</summary>
        public string ModuleSetHash { get; }

        public bool NavalDlcPresent { get; }
        public GameVersion? NavalDlcVersion { get; }
        public string PlayerIdentity { get; }

        public HelloMessage(
            uint protocolVersion,
            GameVersion gameVersion,
            GameVersionType gameVersionType,
            string moduleSetHash,
            bool navalDlcPresent,
            GameVersion? navalDlcVersion,
            string playerIdentity)
        {
            ProtocolVersion = protocolVersion;
            GameVersion = gameVersion;
            GameVersionType = gameVersionType;
            ModuleSetHash = moduleSetHash;
            NavalDlcPresent = navalDlcPresent;
            NavalDlcVersion = navalDlcVersion;
            PlayerIdentity = playerIdentity;
        }
    }
}
