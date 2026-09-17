using System;
using System.IO;

namespace Coop.Core.Network
{
    /// <summary>Unsigned LEB128 variable-length integer — the <c>varint len</c> in docs/NETWORK_PROTOCOL.md §4's wire format.</summary>
    public static class VarInt
    {
        public static void WriteUInt32(Stream stream, uint value)
        {
            while (value >= 0x80)
            {
                stream.WriteByte((byte)(value | 0x80));
                value >>= 7;
            }
            stream.WriteByte((byte)value);
        }

        public static uint ReadUInt32(Stream stream)
        {
            uint result = 0;
            int shift = 0;
            while (true)
            {
                int b = stream.ReadByte();
                if (b < 0)
                {
                    throw new EndOfStreamException("Stream ended mid-varint.");
                }

                result |= (uint)(b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                {
                    break;
                }

                shift += 7;
                if (shift > 35)
                {
                    throw new FormatException("VarInt longer than 5 bytes — malformed stream.");
                }
            }
            return result;
        }
    }
}
