using System;

namespace Coop.Core.Network
{
    /// <summary>
    /// ARCHITECTURE.md §10: <c>(serverEpoch:u32, sequence:u64)</c>, globally unique and
    /// monotonic. <c>serverEpoch</c> increments on every server start, so ids from a previous
    /// run can never collide with a new one.
    /// </summary>
    public readonly struct ConsequenceId : IEquatable<ConsequenceId>
    {
        public uint ServerEpoch { get; }
        public ulong Sequence { get; }

        public ConsequenceId(uint serverEpoch, ulong sequence)
        {
            ServerEpoch = serverEpoch;
            Sequence = sequence;
        }

        public bool Equals(ConsequenceId other) => ServerEpoch == other.ServerEpoch && Sequence == other.Sequence;
        public override bool Equals(object obj) => obj is ConsequenceId other && Equals(other);
        public override int GetHashCode() => unchecked((int)ServerEpoch * 397 ^ Sequence.GetHashCode());
        public override string ToString() => $"{ServerEpoch}:{Sequence}";

        public static bool operator ==(ConsequenceId left, ConsequenceId right) => left.Equals(right);
        public static bool operator !=(ConsequenceId left, ConsequenceId right) => !left.Equals(right);
    }
}
