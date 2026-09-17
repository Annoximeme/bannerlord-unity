using Coop.Core.Versioning;
using Xunit;

namespace Coop.Core.Tests.Versioning
{
    public class GameVersionTests
    {
        [Theory]
        [InlineData("v1.4.8", 1, 4, 8)]
        [InlineData("1.4.8", 1, 4, 8)]
        [InlineData("v1.4.8.119303", 1, 4, 8)] // trailing changeset ignored
        [InlineData("v1.2.8", 1, 2, 8)] // NavalDLC's independent version line
        public void TryParse_ReadsLeadingMajorMinorRevision(string text, int major, int minor, int revision)
        {
            Assert.True(GameVersion.TryParse(text, out GameVersion version));
            Assert.Equal(new GameVersion(major, minor, revision), version);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not a version")]
        [InlineData("v1.4")]
        public void TryParse_RejectsUnparsableText(string text)
        {
            Assert.False(GameVersion.TryParse(text, out _));
        }

        [Fact]
        public void Equality_ComparesComponentsOnly()
        {
            Assert.Equal(new GameVersion(1, 4, 8), new GameVersion(1, 4, 8));
            Assert.NotEqual(new GameVersion(1, 4, 8), new GameVersion(1, 4, 9));
            Assert.True(new GameVersion(1, 4, 8) == new GameVersion(1, 4, 8));
            Assert.True(new GameVersion(1, 4, 8) != new GameVersion(1, 5, 8));
        }

        [Fact]
        public void ToString_MatchesTaleWorldsPrefixConvention()
        {
            Assert.Equal("v1.4.8", new GameVersion(1, 4, 8).ToString());
        }
    }
}
