using System;

namespace Coop.Core.Identity
{
    /// <summary>
    /// Server-assigned, monotonic, never-reused synthesized identity for a
    /// <c>Naval.Ship</c> — which the engine gives no identity of its own (RISK-01;
    /// docs/SYNCHRONIZATION_MODEL.md §4.3). <see cref="None"/> (0) is never allocated.
    /// </summary>
    public readonly struct CoopShipId : IEquatable<CoopShipId>
    {
        public static readonly CoopShipId None = default;

        public ulong Value { get; }

        public CoopShipId(ulong value)
        {
            Value = value;
        }

        public bool IsNone => Value == 0;

        public bool Equals(CoopShipId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is CoopShipId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => IsNone ? "Ship#none" : $"Ship#{Value}";

        public static bool operator ==(CoopShipId left, CoopShipId right) => left.Equals(right);
        public static bool operator !=(CoopShipId left, CoopShipId right) => !left.Equals(right);
    }
}
