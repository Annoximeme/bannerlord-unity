using Coop.Core.Identity;
using TaleWorlds.CampaignSystem.Naval;

namespace Coop.GameInterface.Identity
{
    /// <summary>Extracts the exact fallback fingerprint tuple from a real <see cref="Ship"/> (docs/SYNCHRONIZATION_MODEL.md §4.3).</summary>
    public static class ShipFingerprintAdapter
    {
        public static ShipFingerprint ToFingerprint(this Ship ship) =>
            new ShipFingerprint(
                ship.ShipHull?.StringId,
                ship.Name?.ToString(),
                ship.HitPoints,
                ship.SailHitPoints,
                ship.RandomValue);
    }
}
