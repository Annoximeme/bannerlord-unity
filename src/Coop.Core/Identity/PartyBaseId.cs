using System;

namespace Coop.Core.Identity
{
    /// <summary>
    /// <c>PartyBase</c> has no identity of its own (docs/SYNCHRONIZATION_MODEL.md §4.4,
    /// resolves ARCHITECTURE.md A5) — address it as <c>(ownerId, ownerKind)</c> instead of
    /// giving it a synthesized id like <see cref="CoopShipId"/>. It is always reachable
    /// from an identified owner: a <c>MobileParty</c> or a <c>Settlement</c>, both
    /// MBObjectBase-derived and therefore already stable via <see cref="EngineObjectId"/>.
    /// </summary>
    public readonly struct PartyBaseId : IEquatable<PartyBaseId>
    {
        public EngineObjectId OwnerId { get; }
        public OwnerKind OwnerKind { get; }

        public PartyBaseId(EngineObjectId ownerId, OwnerKind ownerKind)
        {
            OwnerId = ownerId;
            OwnerKind = ownerKind;
        }

        public bool Equals(PartyBaseId other) => OwnerId == other.OwnerId && OwnerKind == other.OwnerKind;

        public override bool Equals(object obj) => obj is PartyBaseId other && Equals(other);

        public override int GetHashCode() => unchecked(OwnerId.GetHashCode() * 31 + (int)OwnerKind);

        public override string ToString() => $"{OwnerKind}:{OwnerId}";

        public static bool operator ==(PartyBaseId left, PartyBaseId right) => left.Equals(right);
        public static bool operator !=(PartyBaseId left, PartyBaseId right) => !left.Equals(right);
    }
}
