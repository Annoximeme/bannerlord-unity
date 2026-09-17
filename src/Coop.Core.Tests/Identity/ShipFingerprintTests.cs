using Coop.Core.Identity;
using Xunit;

namespace Coop.Core.Tests.Identity
{
    public class ShipFingerprintTests
    {
        [Fact]
        public void Equality_SameValues_AreEqual()
        {
            var a = new ShipFingerprint("cog_hull", "The Gull", 100f, 50f, 12345);
            var b = new ShipFingerprint("cog_hull", "The Gull", 100f, 50f, 12345);

            Assert.Equal(a, b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Theory]
        [InlineData("longship_hull", "The Gull", 100f, 50f, 12345)] // different hull
        [InlineData("cog_hull", "Different Name", 100f, 50f, 12345)] // different name
        [InlineData("cog_hull", "The Gull", 99f, 50f, 12345)] // different hit points
        [InlineData("cog_hull", "The Gull", 100f, 49f, 12345)] // different sail hit points
        [InlineData("cog_hull", "The Gull", 100f, 50f, 99999)] // different random value
        public void Equality_AnyDifference_AreNotEqual(string hull, string name, float hp, float sail, int random)
        {
            var baseline = new ShipFingerprint("cog_hull", "The Gull", 100f, 50f, 12345);
            var other = new ShipFingerprint(hull, name, hp, sail, random);

            Assert.NotEqual(baseline, other);
        }

        [Fact]
        public void NullHullOrName_NormalizedToEmptyString_NotNullReferenceException()
        {
            var fingerprint = new ShipFingerprint(null, null, 0f, 0f, 0);

            Assert.Equal("", fingerprint.HullStringId);
            Assert.Equal("", fingerprint.Name);
        }
    }
}
