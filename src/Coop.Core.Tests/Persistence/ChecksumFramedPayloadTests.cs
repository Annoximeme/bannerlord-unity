using System.Text;
using Coop.Core.Persistence;
using Xunit;

namespace Coop.Core.Tests.Persistence
{
    public class ChecksumFramedPayloadTests
    {
        [Fact]
        public void RoundTrips_ValidPayload()
        {
            byte[] payload = Encoding.UTF8.GetBytes("save data");

            byte[] framed = ChecksumFramedPayload.Wrap(payload);
            bool ok = ChecksumFramedPayload.TryUnwrap(framed, out byte[] recovered);

            Assert.True(ok);
            Assert.Equal(payload, recovered);
        }

        [Fact]
        public void CorruptedByte_FailsVerification_NeverReturnsBadData()
        {
            byte[] payload = Encoding.UTF8.GetBytes("save data");
            byte[] framed = ChecksumFramedPayload.Wrap(payload);

            framed[framed.Length - 1] ^= 0xFF; // flip a bit in the payload region

            bool ok = ChecksumFramedPayload.TryUnwrap(framed, out byte[] recovered);

            Assert.False(ok);
            Assert.Null(recovered);
        }

        [Fact]
        public void TooShortToContainAHash_FailsCleanly()
        {
            bool ok = ChecksumFramedPayload.TryUnwrap(new byte[] { 1, 2, 3 }, out byte[] recovered);

            Assert.False(ok);
            Assert.Null(recovered);
        }

        [Fact]
        public void EmptyPayload_StillRoundTrips()
        {
            byte[] framed = ChecksumFramedPayload.Wrap(System.Array.Empty<byte>());

            bool ok = ChecksumFramedPayload.TryUnwrap(framed, out byte[] recovered);

            Assert.True(ok);
            Assert.Empty(recovered);
        }
    }
}
