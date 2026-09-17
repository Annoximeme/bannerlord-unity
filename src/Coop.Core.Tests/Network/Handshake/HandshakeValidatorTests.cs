using Coop.Core.Network.Handshake;
using Coop.Core.Versioning;
using Xunit;

namespace Coop.Core.Tests.Network.Handshake
{
    public class HandshakeValidatorTests
    {
        private static ServerHandshakeConfig BaseServerConfig(bool navalDlc, GameVersion? navalDlcVersion = null) =>
            new ServerHandshakeConfig(1, new GameVersion(1, 4, 8), GameVersionType.Release, "hash-abc", navalDlc, navalDlcVersion);

        private static HelloMessage MatchingHello(ServerHandshakeConfig server, bool navalDlc, GameVersion? navalDlcVersion = null) =>
            new HelloMessage(server.ProtocolVersion, server.GameVersion, server.GameVersionType, server.ModuleSetHash, navalDlc, navalDlcVersion, "player-1");

        [Fact]
        public void EverythingMatches_NeitherHasNavalDlc_Accepted()
        {
            ServerHandshakeConfig server = BaseServerConfig(navalDlc: false);
            HelloMessage hello = MatchingHello(server, navalDlc: false);

            HandshakeResult result = HandshakeValidator.Validate(hello, server);

            Assert.True(result.Accepted);
        }

        [Fact]
        public void BothHaveMatchingNavalDlc_Accepted()
        {
            var dlcVersion = new GameVersion(1, 2, 8);
            ServerHandshakeConfig server = BaseServerConfig(navalDlc: true, dlcVersion);
            HelloMessage hello = MatchingHello(server, navalDlc: true, dlcVersion);

            HandshakeResult result = HandshakeValidator.Validate(hello, server);

            Assert.True(result.Accepted);
        }

        [Fact]
        public void ServerHasNavalDlc_ClientDoesNot_Rejected()
        {
            ServerHandshakeConfig server = BaseServerConfig(navalDlc: true, new GameVersion(1, 2, 8));
            HelloMessage hello = MatchingHello(server, navalDlc: false);

            HandshakeResult result = HandshakeValidator.Validate(hello, server);

            Assert.False(result.Accepted);
            Assert.Equal(RejectReason.DlcMismatch, result.Reason);
        }

        [Fact]
        public void ServerLacksNavalDlc_ClientHasIt_Accepted()
        {
            ServerHandshakeConfig server = BaseServerConfig(navalDlc: false);
            HelloMessage hello = MatchingHello(server, navalDlc: true, new GameVersion(1, 2, 8));

            HandshakeResult result = HandshakeValidator.Validate(hello, server);

            Assert.True(result.Accepted); // allowed; naval systems inert
        }

        [Fact]
        public void BothHaveNavalDlc_DifferentVersions_Rejected()
        {
            ServerHandshakeConfig server = BaseServerConfig(navalDlc: true, new GameVersion(1, 2, 8));
            HelloMessage hello = MatchingHello(server, navalDlc: true, new GameVersion(1, 2, 9));

            HandshakeResult result = HandshakeValidator.Validate(hello, server);

            Assert.False(result.Accepted);
            Assert.Equal(RejectReason.DlcMismatch, result.Reason);
        }

        [Fact]
        public void ProtocolVersionMismatch_Rejected()
        {
            ServerHandshakeConfig server = BaseServerConfig(navalDlc: false);
            HelloMessage hello = new HelloMessage(server.ProtocolVersion + 1, server.GameVersion, server.GameVersionType, server.ModuleSetHash, false, null, "p1");

            HandshakeResult result = HandshakeValidator.Validate(hello, server);

            Assert.False(result.Accepted);
            Assert.Equal(RejectReason.ProtocolMismatch, result.Reason);
        }

        [Theory]
        [InlineData(1, 4, 9)] // different revision
        public void GameVersionMismatch_Rejected(int major, int minor, int revision)
        {
            ServerHandshakeConfig server = BaseServerConfig(navalDlc: false);
            var hello = new HelloMessage(server.ProtocolVersion, new GameVersion(major, minor, revision), server.GameVersionType, server.ModuleSetHash, false, null, "p1");

            HandshakeResult result = HandshakeValidator.Validate(hello, server);

            Assert.False(result.Accepted);
            Assert.Equal(RejectReason.GameVersionMismatch, result.Reason);
        }

        [Fact]
        public void GameVersionTypeMismatch_SameNumberDifferentChannel_Rejected()
        {
            // Same v1.4.8 but one is Beta and the other Release — a real, distinct hazard
            // per VERSION_SUPPORT.md (1.5.3-beta restructured MapEvent mid-line).
            ServerHandshakeConfig server = BaseServerConfig(navalDlc: false);
            var hello = new HelloMessage(server.ProtocolVersion, server.GameVersion, GameVersionType.Beta, server.ModuleSetHash, false, null, "p1");

            HandshakeResult result = HandshakeValidator.Validate(hello, server);

            Assert.False(result.Accepted);
            Assert.Equal(RejectReason.GameVersionMismatch, result.Reason);
        }

        [Fact]
        public void ModuleSetHashMismatch_Rejected()
        {
            ServerHandshakeConfig server = BaseServerConfig(navalDlc: false);
            var hello = new HelloMessage(server.ProtocolVersion, server.GameVersion, server.GameVersionType, "different-hash", false, null, "p1");

            HandshakeResult result = HandshakeValidator.Validate(hello, server);

            Assert.False(result.Accepted);
            Assert.Equal(RejectReason.ModuleSetMismatch, result.Reason);
        }
    }
}
