using System;

namespace Coop.Core.Identity
{
    /// <summary>
    /// Content-fingerprint fallback for ship identity, exactly the tuple specified in
    /// docs/SYNCHRONIZATION_MODEL.md §4.3: <c>(ShipHull.StringId, Name, HitPoints,
    /// SailHitPoints, RandomValue)</c>. Used only to cross-check positional rebinding — the
    /// assumption that ship order survives save/load is LIKELY, not VERIFIED (S3), until
    /// the Phase 1.9 round-trip test passes.
    /// </summary>
    public readonly struct ShipFingerprint : IEquatable<ShipFingerprint>
    {
        public string HullStringId { get; }
        public string Name { get; }
        public float HitPoints { get; }
        public float SailHitPoints { get; }
        public int RandomValue { get; }

        public ShipFingerprint(string hullStringId, string name, float hitPoints, float sailHitPoints, int randomValue)
        {
            HullStringId = hullStringId ?? "";
            Name = name ?? "";
            HitPoints = hitPoints;
            SailHitPoints = sailHitPoints;
            RandomValue = randomValue;
        }

        public bool Equals(ShipFingerprint other) =>
            HullStringId == other.HullStringId &&
            Name == other.Name &&
            HitPoints.Equals(other.HitPoints) &&
            SailHitPoints.Equals(other.SailHitPoints) &&
            RandomValue == other.RandomValue;

        public override bool Equals(object obj) => obj is ShipFingerprint other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + HullStringId.GetHashCode();
                hash = hash * 31 + Name.GetHashCode();
                hash = hash * 31 + HitPoints.GetHashCode();
                hash = hash * 31 + SailHitPoints.GetHashCode();
                hash = hash * 31 + RandomValue;
                return hash;
            }
        }

        public override string ToString() =>
            $"({HullStringId}, \"{Name}\", hp={HitPoints}, sail={SailHitPoints}, rnd={RandomValue})";
    }
}
