using Coop.Core.Identity;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.GameInterface.Identity
{
    /// <summary>Adapts a real <c>PartyBase</c> to <see cref="PartyBaseId"/> (docs/SYNCHRONIZATION_MODEL.md §4.4).</summary>
    public static class PartyBaseIdAdapter
    {
        /// <summary>
        /// Null if the party has neither a <c>MobileParty</c> nor a <c>Settlement</c> owner.
        /// Per the design this shouldn't happen for anything we need to address, but we
        /// don't assume it's impossible — better a null the caller must handle than a
        /// silently wrong id.
        /// </summary>
        public static PartyBaseId? ToPartyBaseId(this PartyBase partyBase)
        {
            if (partyBase.MobileParty != null)
            {
                return new PartyBaseId(partyBase.MobileParty.ToEngineObjectId(), OwnerKind.MobileParty);
            }
            if (partyBase.Settlement != null)
            {
                return new PartyBaseId(partyBase.Settlement.ToEngineObjectId(), OwnerKind.Settlement);
            }
            return null;
        }
    }
}
