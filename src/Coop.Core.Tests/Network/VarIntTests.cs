using System.IO;
using Coop.Core.Network;
using Xunit;

namespace Coop.Core.Tests.Network
{
    public class VarIntTests
    {
        [Theory]
        [InlineData(0u)]
        [InlineData(1u)]
        [InlineData(127u)] // largest single-byte value
        [InlineData(128u)] // smallest two-byte value
        [InlineData(300u)]
        [InlineData(16384u)] // three bytes
        [InlineData(uint.MaxValue)]
        public void RoundTrips(uint value)
        {
            using var ms = new MemoryStream();
            VarInt.WriteUInt32(ms, value);
            ms.Position = 0;

            Assert.Equal(value, VarInt.ReadUInt32(ms));
        }

        [Fact]
        public void SmallValues_EncodeToOneByte()
        {
            using var ms = new MemoryStream();
            VarInt.WriteUInt32(ms, 100);

            Assert.Equal(1, ms.Length);
        }

        [Fact]
        public void LargeValues_EncodeToMultipleBytes()
        {
            using var ms = new MemoryStream();
            VarInt.WriteUInt32(ms, uint.MaxValue);

            Assert.True(ms.Length > 1);
        }
    }
}
