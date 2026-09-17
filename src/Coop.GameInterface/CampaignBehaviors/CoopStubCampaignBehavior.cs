using TaleWorlds.CampaignSystem;

namespace Coop.GameInterface.CampaignBehaviors
{
    /// <summary>
    /// Phase 1.2 exit criterion: proves behavior registration works. Deliberately empty —
    /// real behaviors arrive with the systems they belong to, starting Phase 1.5 onward.
    /// </summary>
    public sealed class CoopStubCampaignBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
        }

        public override void SyncData(IDataStore dataStore)
        {
        }
    }
}
