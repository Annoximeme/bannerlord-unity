using System;
using System.IO;
using System.Linq;
using Coop.Core.Identity;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.GameInterface.Identity
{
    /// <summary>
    /// Phase 1.6 exit-criterion demonstration: every currently-existing <see cref="Ship"/>
    /// is addressable by a stable <see cref="CoopShipId"/> for the running session. Logs
    /// the assignment once per in-game day so a real play session can confirm the same ship
    /// keeps the same id across ticks — not wired into replication yet (that's Phase 1.7+).
    /// </summary>
    public sealed class ShipIdentityCampaignBehavior : CampaignBehaviorBase
    {
        private static string _logPath;
        private static readonly object LogLock = new object();

        private readonly CoopShipRegistry _registry = new CoopShipRegistry();

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
            // Cross-session persistence of the id<->ship binding is Phase 1.9 scope
            // (docs/ROADMAP.md) — this behavior only proves same-session stability for now.
        }

        private void OnGameLoadFinished() => AssignAndLogAll("OnGameLoadFinished");

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
