using System;

namespace Coop.Core.Identity
{
    /// <summary>
    /// Wire-stable id for anything with an engine-native MBGUID-shaped identity
    /// (MBObjectBase-derived types: Hero, Clan, Kingdom, Settlement, MobileParty, ShipHull...).
    /// docs/SYNCHRONIZATION_MODEL.md §4.2: MBGUID wraps a uint and is already directly usable
    /// as a wire key, so this just carries that uint without Coop.Core ever referencing
    /// TaleWorlds.ObjectSystem.MBGUID itself — Coop.GameInterface converts.
    /// </summary>
    public readonly struct EngineObjectId : IEquatable<EngineObjectId>
    {
        public uint Value { get; }

        public EngineObjectId(uint value)
        {
            Value = value;
        }

        public bool Equals(EngineObjectId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is EngineObjectId other && Equals(other);

        public override int GetHashCode() => (int)Value;

        public override string ToString() => $"E{Value}";

        public static bool operator ==(EngineObjectId left, EngineObjectId right) => left.Equals(right);
        public static bool operator !=(EngineObjectId left, EngineObjectId right) => !left.Equals(right);
    }
}
