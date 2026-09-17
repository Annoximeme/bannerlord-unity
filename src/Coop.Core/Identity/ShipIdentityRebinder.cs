using System;
using System.Collections.Generic;

namespace Coop.Core.Identity
{
    /// <summary>A previously persisted ship slot: the id it was assigned, and the fingerprint it had at persist time.</summary>
    public readonly struct PersistedShipEntry
    {
        public CoopShipId Id { get; }
        public ShipFingerprint Fingerprint { get; }

        public PersistedShipEntry(CoopShipId id, ShipFingerprint fingerprint)
        {
            Id = id;
            Fingerprint = fingerprint;
        }
    }

    public sealed class RebindResult
    {
        /// <summary>
        /// Aligned with the <c>current</c> list passed to <see cref="ShipIdentityRebinder.Rebind"/>:
        /// the resolved id for each current-order position, or <c>null</c> where no persisted
        /// entry existed at that position — the caller should allocate a fresh id there.
        /// </summary>
        public IReadOnlyList<CoopShipId?> ResolvedIds { get; }

        /// <summary>
        /// Positions where a persisted id existed but its fingerprint disagreed with the
        /// current ship there. The ordering assumption (S3) may have been violated — the
        /// caller should treat the id at this position with suspicion, not silently trust it.
        /// </summary>
        public IReadOnlyList<int> FingerprintMismatches { get; }

        /// <summary>Persisted ids with no corresponding current ship — destroyed, transferred away, or the party's ship count shrank.</summary>
        public IReadOnlyList<CoopShipId> UnmatchedPersistedIds { get; }

        public RebindResult(
            IReadOnlyList<CoopShipId?> resolvedIds,
            IReadOnlyList<int> fingerprintMismatches,
            IReadOnlyList<CoopShipId> unmatchedPersistedIds)
        {
            ResolvedIds = resolvedIds;
            FingerprintMismatches = fingerprintMismatches;
            UnmatchedPersistedIds = unmatchedPersistedIds;
        }
    }

    /// <summary>
    /// Implements the positional-rebinding design in docs/SYNCHRONIZATION_MODEL.md §4.3:
    /// match the i-th currently-loaded ship to the i-th persisted (id, fingerprint) pair,
    /// and flag — never silently trust — any position whose fingerprint disagrees with what
    /// was persisted there, since <c>MBList&lt;Ship&gt;</c> preserving order through
    /// save/load is LIKELY, not VERIFIED (S3), until the Phase 1.9 round-trip test passes.
    /// </summary>
    public static class ShipIdentityRebinder
    {
        public static RebindResult Rebind(IReadOnlyList<PersistedShipEntry> persisted, IReadOnlyList<ShipFingerprint> current)
        {
            if (persisted == null) throw new ArgumentNullException(nameof(persisted));
            if (current == null) throw new ArgumentNullException(nameof(current));

            var resolved = new CoopShipId?[current.Count];
            var mismatches = new List<int>();

            int overlap = Math.Min(persisted.Count, current.Count);
            for (int i = 0; i < overlap; i++)
            {
                resolved[i] = persisted[i].Id;
                if (!persisted[i].Fingerprint.Equals(current[i]))
                {
                    mismatches.Add(i);
                }
            }
            // Any current ships beyond what was persisted are new — their slot stays null
            // so the caller allocates a fresh CoopShipId for them.

            var unmatched = new List<CoopShipId>();
            for (int i = overlap; i < persisted.Count; i++)
            {
                unmatched.Add(persisted[i].Id);
            }

            return new RebindResult(resolved, mismatches, unmatched);
        }
    }
}
