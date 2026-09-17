using System.IO;
using System.Text;
using Coop.Core.Network;
using Xunit;

namespace Coop.Core.Tests.Network
{
    public class FrameTests
    {
        [Fact]
        public void RoundTrips_ChannelMessageTypeSequenceAndPayload()
        {
            byte[] payload = Encoding.UTF8.GetBytes("hello");
            var frame = new Frame(Channel.Command, 42, 7, payload);

            Frame decoded = Frame.FromBytes(frame.ToBytes());

            Assert.Equal(Channel.Command, decoded.Channel);
            Assert.Equal((ushort)42, decoded.MessageType);
            Assert.Equal(7u, decoded.Sequence);
            Assert.Equal(payload, decoded.Payload);
        }

        [Fact]
        public void EmptyPayload_RoundTrips()
        {
            var frame = new Frame(Channel.Control, 1, 1, System.Array.Empty<byte>());

            Frame decoded = Frame.FromBytes(frame.ToBytes());

            Assert.Empty(decoded.Payload);
        }

        [Fact]
        public void NullPayload_NormalizedToEmpty()
        {
            var frame = new Frame(Channel.Control, 1, 1, null);

            Assert.Empty(frame.Payload);
        }

        [Fact]
        public void LargePayload_ExercisesMultiByteVarIntLength()
        {
            byte[] payload = new byte[70000];
            for (int i = 0; i < payload.Length; i++) payload[i] = (byte)(i % 256);
            var frame = new Frame(Channel.Snapshot, 5, 1, payload);

            Frame decoded = Frame.FromBytes(frame.ToBytes());

            Assert.Equal(payload, decoded.Payload);
        }

        [Fact]
        public void SelfDelimiting_TwoFramesWrittenToOneStream_BothReadBackCorrectly()
        {
            var first = new Frame(Channel.Control, 1, 1, Encoding.UTF8.GetBytes("first"));
            var second = new Frame(Channel.Command, 2, 2, Encoding.UTF8.GetBytes("second"));

            using var stream = new MemoryStream();
            first.WriteTo(stream);
            second.WriteTo(stream);
            stream.Position = 0;

            Frame readFirst = Frame.ReadFrom(stream);
            Frame readSecond = Frame.ReadFrom(stream);

            Assert.Equal("first", Encoding.UTF8.GetString(readFirst.Payload));
            Assert.Equal("second", Encoding.UTF8.GetString(readSecond.Payload));
            Assert.Equal(stream.Length, stream.Position); // nothing left over
        }
    }
}
