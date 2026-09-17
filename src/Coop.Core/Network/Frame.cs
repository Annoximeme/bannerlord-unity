using System;
using System.IO;

namespace Coop.Core.Network
{
    /// <summary>
    /// docs/NETWORK_PROTOCOL.md §4: <c>[u8 channel][u16 msgType][u32 seq][varint len][payload]</c>.
    /// An L3 concept — <see cref="ICoopTransport"/> never constructs or parses one of these; a
    /// caller builds a <see cref="Frame"/>, gets its bytes, and passes them as an opaque payload
    /// to <c>Send</c>/<c>Broadcast</c>. Self-delimiting: reading one off a stream never needs an
    /// outer length prefix, which is what lets it also serve as the transport's own
    /// message-boundary framing when a caller chooses to use it that way.
    /// </summary>
    public readonly struct Frame
    {
        public Channel Channel { get; }
        public ushort MessageType { get; }
        public uint Sequence { get; }
        public byte[] Payload { get; }

        public Frame(Channel channel, ushort messageType, uint sequence, byte[] payload)
        {
            Channel = channel;
            MessageType = messageType;
            Sequence = sequence;
            Payload = payload ?? Array.Empty<byte>();
        }

        public void WriteTo(Stream stream)
        {
            stream.WriteByte((byte)Channel);
            WriteUInt16(stream, MessageType);
            WriteUInt32(stream, Sequence);
            VarInt.WriteUInt32(stream, (uint)Payload.Length);
            stream.Write(Payload, 0, Payload.Length);
        }

        public byte[] ToBytes()
        {
            using var ms = new MemoryStream();
            WriteTo(ms);
            return ms.ToArray();
        }

        public static Frame ReadFrom(Stream stream)
        {
            int channelByte = ReadByteOrThrow(stream);
            ushort messageType = ReadUInt16(stream);
            uint sequence = ReadUInt32(stream);
            uint length = VarInt.ReadUInt32(stream);

            var payload = new byte[length];
            ReadExactly(stream, payload);

            return new Frame((Channel)channelByte, messageType, sequence, payload);
        }

        public static Frame FromBytes(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            return ReadFrom(ms);
        }

        private static void WriteUInt16(Stream stream, ushort value)
        {
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        private static ushort ReadUInt16(Stream stream)
        {
            int hi = ReadByteOrThrow(stream);
            int lo = ReadByteOrThrow(stream);
            return (ushort)((hi << 8) | lo);
        }

        private static void WriteUInt32(Stream stream, uint value)
        {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        private static uint ReadUInt32(Stream stream)
        {
            uint result = 0;
            for (int i = 0; i < 4; i++)
            {
                result = (result << 8) | (uint)ReadByteOrThrow(stream);
            }
            return result;
        }

        private static int ReadByteOrThrow(Stream stream)
        {
            int b = stream.ReadByte();
            if (b < 0)
            {
                throw new EndOfStreamException("Stream ended mid-frame.");
            }
            return b;
        }

        internal static void ReadExactly(Stream stream, byte[] buffer)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int read = stream.Read(buffer, offset, buffer.Length - offset);
                if (read <= 0)
                {
                    throw new EndOfStreamException("Stream ended before the expected payload length was read.");
                }
                offset += read;
            }
        }
    }
}
