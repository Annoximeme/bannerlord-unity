using Coop.Core.Versioning;
using Xunit;

namespace Coop.Core.Tests.Versioning
{
    public class VersionGateTests
    {
        [Fact]
        public void Accepts_ExactPinnedVersion()
        {
            var gate = new VersionGate(new GameVersion(1, 4, 8));

            Assert.True(gate.Accepts(new GameVersion(1, 4, 8)));
        }

        [Theory]
        [InlineData(1, 4, 9)]
        [InlineData(1, 5, 8)]
        [InlineData(2, 4, 8)]
        public void Rejects_AnyOtherVersion(int major, int minor, int revision)
        {
            var gate = new VersionGate(new GameVersion(1, 4, 8));

            Assert.False(gate.Accepts(new GameVersion(major, minor, revision)));
        }
    }
}
