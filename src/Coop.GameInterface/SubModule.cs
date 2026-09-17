using System;
using System.Linq;
using Coop.Core.Versioning;
using Coop.GameInterface.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;

namespace Coop.GameInterface
{
    /// <summary>
    /// Phase 1.2 — module skeleton + hard version gate (docs/ROADMAP.md, PROJECT_STATUS.md).
    /// Refuses to activate on any game version other than the one this build is pinned to,
    /// per CLAUDE.md §2 and docs/VERSION_SUPPORT.md §7.3, instead of silently targeting
    /// whatever happens to be installed.
    /// </summary>
    public class SubModule : MBSubModuleBase
    {
        private static readonly GameVersion PinnedNativeVersion = new GameVersion(1, 4, 8);
        private static readonly VersionGate Gate = new VersionGate(PinnedNativeVersion);

        private bool _versionAccepted;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            _versionAccepted = CheckVersionGate();
        }

        protected override void InitializeGameStarter(Game game, IGameStarter gameStarterObject)
        {
            base.InitializeGameStarter(game, gameStarterObject);

            if (!_versionAccepted)
            {
                return;
            }

            if (game.GameType is Campaign && gameStarterObject is CampaignGameStarter campaignGameStarter)
            {
                campaignGameStarter.AddBehavior(new CoopStubCampaignBehavior());
            }
        }

        private bool CheckVersionGate()
        {
            try
            {
                ModuleInfo native = ModuleHelper.GetActiveModules().FirstOrDefault(m => m.Id == "Native");
                if (native == null)
                {
                    Refuse("the Native module is not active.");
                    return false;
                }

                if (!GameVersion.TryParse(native.Version.ToString(), out GameVersion running))
                {
                    Refuse($"could not parse Native module version '{native.Version}'.");
                    return false;
                }

                if (!Gate.Accepts(running))
                {
                    Refuse($"running {running} but this build is pinned to {PinnedNativeVersion} (docs/VERSION_SUPPORT.md).");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Refuse($"version check threw: {ex}");
                return false;
            }
        }

        private static void Refuse(string reason)
        {
            string message = $"[Bannerlord: Unity] Refusing to activate — {reason}";
            Debug.Print(message);
            InformationManager.DisplayMessage(new InformationMessage(message));
        }
    }
}
