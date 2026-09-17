using System;

namespace Coop.Core.Network
{
    /// <summary>Identifies one connected peer from a transport's perspective. Server-assigned; opaque to callers.</summary>
    public readonly struct PeerId : IEquatable<PeerId>
    {
        public int Value { get; }

        public PeerId(int value)
        {
            Value = value;
        }

        public bool Equals(PeerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PeerId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => $"Peer#{Value}";

        public static bool operator ==(PeerId left, PeerId right) => left.Equals(right);
        public static bool operator !=(PeerId left, PeerId right) => !left.Equals(right);
    }
}
