using System;
using System.IO;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;

namespace CoopNetworkProbe
{
    /// <summary>
    /// Phase 1.4 / B3 — the GameNetwork-in-campaign probe specified in
    /// docs/NETWORK_PROTOCOL.md §2. Answers RISK-02: is TaleWorlds.MountAndBlade.GameNetwork
    /// usable from inside a singleplayer Campaign session, or does the transport layer
    /// need to be built from scratch (TcpSocket / raw sockets)?
    ///
    /// This module makes no gameplay claims and ships no gameplay feature — it only
    /// observes and logs. Every step is wrapped so one failure does not abort the rest;
    /// docs/NETWORK_PROTOCOL.md §2 asks for "success / exception / silent no-op" per step.
    /// </summary>
    public class SubModule : MBSubModuleBase
    {
        // docs/VERSION_SUPPORT.md §6 — the pinned target this experiment's findings apply to.
        private const string ExpectedNativeVersionPrefix = "v1.4.8";

        private static string _logPath;
        private static readonly object LogLock = new object();

        private bool _experimentRan;
        private float _tickAccumulator;
        private int _tickLogCount;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var dir = Path.Combine(docs, "Mount and Blade II Bannerlord", "CoopNetworkProbe");
            Directory.CreateDirectory(dir);
            _logPath = Path.Combine(dir, "network-probe.log");

            Log("=== CoopNetworkProbe loaded (OnSubModuleLoad) ===");
            LogNetworkState("module load");
        }

        public override void OnCampaignStart(Game game, object starterObject)
        {
            base.OnCampaignStart(game, starterObject);
            Log("--- OnCampaignStart fired ---");

            if (_experimentRan)
            {
                Log("Experiment already ran this process; skipping re-run.");
                return;
            }

            if (!VersionGatePasses())
            {
                Log("VERSION GATE FAILED — experiment NOT run. Findings below would not be pinned to a verified version.");
                return;
            }

            _experimentRan = true;
            RunExperiment();
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            if (!_experimentRan || _tickLogCount >= 5)
            {
                return;
            }

            _tickAccumulator += dt;
            if (_tickAccumulator < 5f)
            {
                return;
            }

            _tickAccumulator = 0f;
            _tickLogCount++;
            bool campaignAlive = Campaign.Current != null;
            Log($"[tick check {_tickLogCount}/5, +{_tickLogCount * 5}s since experiment] " +
                $"Campaign.Current != null: {campaignAlive}; Campaign.CurrentTime: " +
                (campaignAlive ? Campaign.CurrentTime.ToString("F1") : "n/a"));
        }

        /// <summary>
        /// Hard version gate (CLAUDE.md §2 / docs/VERSION_SUPPORT.md §7.3), read from the
        /// running game via TaleWorlds.ModuleManager.ModuleHelper.GetActiveModules() rather
        /// than assumed — this also verifies that entry point for TD4.
        /// </summary>
        private bool VersionGatePasses()
        {
            try
            {
                var active = ModuleHelper.GetActiveModules();
                var native = active.FirstOrDefault(m => m.Id == "Native");
                var naval = active.FirstOrDefault(m => m.Id == "NavalDLC");
                string nativeVer = native != null ? native.Version.ToString() : "MISSING";
                string navalVer = naval != null ? naval.Version.ToString() : "not active";
                Log($"Active module versions — Native: {nativeVer}, NavalDLC: {navalVer}");

                bool ok = native != null && nativeVer.StartsWith(ExpectedNativeVersionPrefix, StringComparison.Ordinal);
                if (!ok)
                {
                    Log($"MISMATCH: expected Native {ExpectedNativeVersionPrefix}. Re-run docs/HANDOFF.md's " +
                        "collector and re-pin docs/VERSION_SUPPORT.md before trusting any result from this build.");
                }
                return ok;
            }
            catch (Exception ex)
            {
                Log($"Version gate threw: {ex}");
                return false;
            }
        }

        private void RunExperiment()
        {
            Log("=== RISK-02 experiment start (docs/NETWORK_PROTOCOL.md §2) ===");
            LogNetworkState("before any GameNetwork call");

            RunStep("Initialize", () => GameNetwork.Initialize(new ProbeNetworkHandler(Log)));
            LogNetworkState("after Initialize");

            RunStep("PreStartMultiplayerOnServer", () => GameNetwork.PreStartMultiplayerOnServer());
            LogNetworkState("after PreStartMultiplayerOnServer");

            RunStep("StartMultiplayerOnServer(7773)", () => GameNetwork.StartMultiplayerOnServer(7773));
            LogNetworkState("after StartMultiplayerOnServer");

            RunStep(
                "AddRemoveMessageHandlers(RegisterMode.Add)",
                () => GameNetwork.AddRemoveMessageHandlers(GameNetwork.NetworkMessageHandlerRegisterer.RegisterMode.Add));

            RunStep("BeginBroadcastModuleEvent / EndBroadcastModuleEvent (loopback)", () =>
            {
                GameNetwork.BeginBroadcastModuleEvent();
                GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.None, null);
            });
            LogNetworkState("after loopback broadcast attempt");

            Log("=== RISK-02 experiment complete. Campaign-tick confirmations follow over the next ~25s. ===");
        }

        private static void RunStep(string name, Action step)
        {
            try
            {
                step();
                Log($"STEP OK: {name}");
            }
            catch (Exception ex)
            {
                Log($"STEP THREW: {name} -> {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void LogNetworkState(string label)
        {
            try
            {
                Log($"[state @ {label}] IsSessionActive={GameNetwork.IsSessionActive} " +
                    $"IsMultiplayer={GameNetwork.IsMultiplayer} MultiplayerDisabled={GameNetwork.MultiplayerDisabled} " +
                    $"IsServer={GameNetwork.IsServer} IsClient={GameNetwork.IsClient}");
            }
            catch (Exception ex)
            {
                Log($"[state @ {label}] threw reading GameNetwork properties: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void Log(string line)
        {
            lock (LogLock)
            {
                var stamped = $"{DateTime.UtcNow:O} {line}";
                Console.WriteLine("[CoopNetworkProbe] " + stamped);
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

    /// <summary>Minimal, logging-only IGameNetworkHandler so GameNetwork.Initialize has something to call.</summary>
    internal sealed class ProbeNetworkHandler : IGameNetworkHandler
    {
        private readonly Action<string> _log;

        public ProbeNetworkHandler(Action<string> log)
        {
            _log = log;
        }

        public void OnNewPlayerConnect(PlayerConnectionInfo info, NetworkCommunicator peer) =>
            _log("IGameNetworkHandler.OnNewPlayerConnect");

        public void OnInitialize() => _log("IGameNetworkHandler.OnInitialize");

        public void OnPlayerConnectedToServer(NetworkCommunicator peer) =>
            _log("IGameNetworkHandler.OnPlayerConnectedToServer");

        public void OnPlayerDisconnectedFromServer(NetworkCommunicator peer) =>
            _log("IGameNetworkHandler.OnPlayerDisconnectedFromServer");

        public void OnDisconnectedFromServer() => _log("IGameNetworkHandler.OnDisconnectedFromServer");

        public void OnStartMultiplayer() => _log("IGameNetworkHandler.OnStartMultiplayer");

        public void OnStartReplay() => _log("IGameNetworkHandler.OnStartReplay");

        public void OnEndMultiplayer() => _log("IGameNetworkHandler.OnEndMultiplayer");

        public void OnEndReplay() => _log("IGameNetworkHandler.OnEndReplay");

        public void OnHandleConsoleCommand(string command) =>
            _log("IGameNetworkHandler.OnHandleConsoleCommand: " + command);
    }
}
