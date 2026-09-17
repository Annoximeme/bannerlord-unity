using System.Collections.Generic;
using System.Linq;
using Coop.Core.Identity;
using Coop.Core.Persistence;
using Coop.GameInterface.Identity;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;

namespace Coop.GameInterface.Persistence
{
    /// <summary>Result of applying a persisted <see cref="CoopStateSnapshot"/> — the live evidence for RISK-15 (SYNCHRONIZATION_MODEL.md §4.3/S3).</summary>
    public sealed class ApplyResult
    {
        public int ShipsRebound;
        public int FingerprintMismatches;
        public int UnmatchedPersistedShips;
        public int UnresolvedOwners;
    }

    /// <summary>Builds/applies a <see cref="CoopStateSnapshot"/> against real TaleWorlds objects. Kept separate from the campaign behavior so that class stays thin.</summary>
    public static class CoopStateAdapter
    {
        public static CoopStateSnapshot BuildSnapshot(CoopShipRegistry registry)
        {
            var ownersWithShips = new List<OwnerShips>();

            CollectFrom(MobileParty.All.Select(p => p?.Party), registry, ownersWithShips);
            CollectFrom(Settlement.All.Select(s => s?.Party), registry, ownersWithShips);

            return new CoopStateSnapshot(new List<PlayerBinding>(), registry.NextIdForPersistence, ownersWithShips);
        }

        private static void CollectFrom(IEnumerable<PartyBase> parties, CoopShipRegistry registry, List<OwnerShips> into)
        {
            foreach (PartyBase partyBase in parties)
            {
                if (partyBase == null || partyBase.Ships.Count == 0)
                {
                    continue;
                }

                PartyBaseId? ownerId = partyBase.ToPartyBaseId();
                if (ownerId == null)
                {
                    continue; // no identified owner (shouldn't happen per SYNCHRONIZATION_MODEL.md §4.4, but don't assume)
                }

                var entries = new List<PersistedShipEntry>(partyBase.Ships.Count);
                foreach (Ship ship in partyBase.Ships)
                {
                    if (!registry.TryGetId(ship, out CoopShipId id))
                    {
                        continue; // not yet observed this session; will get a fresh id next time it's seen
                    }
                    entries.Add(new PersistedShipEntry(id, ship.ToFingerprint()));
                }
                into.Add(new OwnerShips(ownerId.Value, entries));
            }
        }

        /// <summary>
        /// Re-locates each persisted owner by its <see cref="EngineObjectId"/>, then rebinds
        /// its current ships against what was persisted via <see cref="ShipIdentityRebinder"/>
        /// — the actual save/load round-trip test toward RISK-15.
        /// </summary>
        public static ApplyResult ApplySnapshot(CoopShipRegistry registry, CoopStateSnapshot snapshot)
        {
            registry.ObservePersistedNextId(snapshot.NextShipId);

            var report = new ApplyResult();

            foreach (OwnerShips ownerShips in snapshot.OwnersWithShips)
            {
                PartyBase partyBase = ResolveOwner(ownerShips.Owner);
                if (partyBase == null)
                {
                    report.UnresolvedOwners++;
                    continue;
                }

                var currentShips = partyBase.Ships.ToList();
                var currentFingerprints = currentShips.Select(s => s.ToFingerprint()).ToList();

                RebindResult rebind = ShipIdentityRebinder.Rebind(ownerShips.Ships, currentFingerprints);
                registry.ApplyRebindResult(currentShips, rebind);

                report.ShipsRebound += currentShips.Count;
                report.FingerprintMismatches += rebind.FingerprintMismatches.Count;
                report.UnmatchedPersistedShips += rebind.UnmatchedPersistedIds.Count;
            }

            return report;
        }

        private static PartyBase ResolveOwner(PartyBaseId ownerId)
        {
            MBObjectBase obj = MBObjectManager.Instance?.GetObject(new MBGUID(ownerId.OwnerId.Value));
            switch (ownerId.OwnerKind)
            {
                case OwnerKind.MobileParty:
                    return (obj as MobileParty)?.Party;
                case OwnerKind.Settlement:
                    return (obj as Settlement)?.Party;
                default:
                    return null;
            }
        }
    }
}
