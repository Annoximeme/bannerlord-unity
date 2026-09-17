using System;
using System.IO;
using System.Linq;
using Coop.Core.Identity;
using Coop.Core.Persistence;
using Coop.GameInterface.Persistence;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.GameInterface.Identity
{
    /// <summary>
    /// Phase 1.6 exit-criterion demonstration: every currently-existing <see cref="Ship"/>
    /// is addressable by a stable <see cref="CoopShipId"/> for the running session. Logs
    /// the assignment once per in-game day so a real play session can confirm the same ship
    /// keeps the same id across ticks. Phase 1.9 adds cross-session persistence and the
    /// live round-trip test toward RISK-15 — not wired into replication yet (that's 1.7+'s
    /// transport carrying it, once there's something on the other end to send it to).
    /// </summary>
    public sealed class ShipIdentityCampaignBehavior : CampaignBehaviorBase
    {
        private const string BlobKey = "Coop.ShipIdentity.State";

        private static string _logPath;
        private static readonly object LogLock = new object();

        private readonly CoopShipRegistry _registry = new CoopShipRegistry();
        private readonly SchemaMigrationChain _migrationChain = new SchemaMigrationChain();
        private CoopStateSnapshot _pendingLoadedSnapshot;

        public override void RegisterEvents()
        {
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string dir = Path.Combine(docs, "Mount and Blade II Bannerlord", "Coop.GameInterface");
            Directory.CreateDirectory(dir);
            _logPath = Path.Combine(dir, "ship-identity.log");
            Log("=== ShipIdentityCampaignBehavior session start ===");

            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, OnGameLoadFinished);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                CoopStateSnapshot snapshot = CoopStateAdapter.BuildSnapshot(_registry);
                byte[] encoded = CoopStateBlobCodecV1.Encode(snapshot);
                string blob = Convert.ToBase64String(encoded);
                dataStore.SyncData(BlobKey, ref blob);
                Log($"SyncData(saving): {snapshot.OwnersWithShips.Count} owner(s) with ships, nextShipId={snapshot.NextShipId}.");
                return;
            }

            // Loading. Deferred: the object graph (Ship/MobileParty/Settlement instances)
            // isn't necessarily safe to walk from inside SyncData itself — real TaleWorlds
            // behaviors consistently defer this kind of work to OnGameLoadFinishedEvent, so
            // we do the same rather than risk it (LIKELY, not independently decompiled for
            // this exact ordering guarantee).
            //
            // The whole block is wrapped, not just the decode step: dataStore.SyncData(...)
            // itself is engine code we don't control, and a real-world load failure on
            // 2026-09-17 exposed that a throw there — from any cause — was previously
            // uncaught and could take the whole load down with it. Never again: our own
            // optional data must not be able to block someone's entire campaign from
            // loading (SAVE_FORMAT.md §5 "fail loudly" means log clearly, not crash).
            try
            {
                string loadedBlob = null;
                bool found = dataStore.SyncData(BlobKey, ref loadedBlob);
                if (!found || string.IsNullOrEmpty(loadedBlob))
                {
                    Log("SyncData(loading): no persisted Coop.ShipIdentity.State found — first load, or a save from before this behavior existed.");
                    return;
                }

                byte[] raw = Convert.FromBase64String(loadedBlob);
                uint version = CoopStateBlobCodecV1.PeekSchemaVersion(raw);
                byte[] current = _migrationChain.MigrateTo(version, raw, CoopStateBlobCodecV1.SchemaVersion);
                _pendingLoadedSnapshot = CoopStateBlobCodecV1.Decode(current);
                Log($"SyncData(loading): decoded schema v{version}, {_pendingLoadedSnapshot.OwnersWithShips.Count} owner(s) with ships pending application at OnGameLoadFinished.");
            }
            catch (Exception ex)
            {
                // Every ship will simply get a fresh id next time it's observed instead of
                // rebinding — a real degradation, but never a reason to block the load.
                Log($"SyncData(loading): FAILED — {ex.GetType().Name}: {ex.Message}. Ship ids will NOT be stable across this load.");
            }
        }

        private void OnGameLoadFinished()
        {
            if (_pendingLoadedSnapshot != null)
            {
                ApplyResult result = CoopStateAdapter.ApplySnapshot(_registry, _pendingLoadedSnapshot);
                Log($"OnGameLoadFinished: round-trip applied — {result.ShipsRebound} ship(s) rebound, " +
                    $"{result.FingerprintMismatches} fingerprint mismatch(es) (RISK-15 S3 — the ordering assumption held IFF this is 0), " +
                    $"{result.UnmatchedPersistedShips} unmatched persisted ship(s), {result.UnresolvedOwners} unresolved owner(s).");
                _pendingLoadedSnapshot = null;
            }

            AssignAndLogAll("OnGameLoadFinished");
        }

        private void OnDailyTick() => AssignAndLogAll("DailyTick");

        private void AssignAndLogAll(string trigger)
        {
            int partiesWithShips = 0;
            int shipsAssigned = 0;

            foreach (MobileParty party in MobileParty.All)
            {
                PartyBase partyBase = party?.Party;
                if (partyBase == null || partyBase.Ships.Count == 0)
                {
                    continue;
                }

                partiesWithShips++;
                for (int i = 0; i < partyBase.Ships.Count; i++)
                {
                    Ship ship = partyBase.Ships[i];
                    CoopShipId id = _registry.GetOrAssign(ship);
                    shipsAssigned++;
                    Log($"[{trigger}] party={party.StringId} slot={i} ship=\"{ship.Name}\" -> {id}");
                }
            }

            Log($"[{trigger}] summary: {partiesWithShips} part(y/ies) with ships, {shipsAssigned} ship(s) assigned.");
        }

        private static void Log(string line)
        {
            lock (LogLock)
            {
                string stamped = $"{DateTime.UtcNow:O} {line}";
                Console.WriteLine("[ShipIdentity] " + stamped);
                try
                {
                    if (_logPath != null)
                    {
                        File.AppendAllText(_logPath, stamped + Environment.NewLine);
                    }
                }
                catch
                {
                    // Best-effort; the in-game console output above is the fallback record.
                }
            }
        }
    }
}
