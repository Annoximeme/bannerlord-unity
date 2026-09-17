using System.Collections.Generic;
using Coop.Core.Identity;
using Xunit;

namespace Coop.Core.Tests.Identity
{
    public class ShipIdentityRebinderTests
    {
        private static ShipFingerprint Fp(string hull, int random) => new ShipFingerprint(hull, "Ship", 100f, 50f, random);

        [Fact]
        public void ExactMatch_ResolvesEveryPositionWithNoMismatches()
        {
            var persisted = new List<PersistedShipEntry>
            {
                new PersistedShipEntry(new CoopShipId(1), Fp("cog", 111)),
                new PersistedShipEntry(new CoopShipId(2), Fp("longship", 222)),
            };
            var current = new List<ShipFingerprint> { Fp("cog", 111), Fp("longship", 222) };

            RebindResult result = ShipIdentityRebinder.Rebind(persisted, current);

            Assert.Equal(new CoopShipId(1), result.ResolvedIds[0]);
            Assert.Equal(new CoopShipId(2), result.ResolvedIds[1]);
            Assert.Empty(result.FingerprintMismatches);
            Assert.Empty(result.UnmatchedPersistedIds);
        }

        [Fact]
        public void NewShipAppended_GetsNullSlot_ForCallerToAllocateAFreshId()
        {
            var persisted = new List<PersistedShipEntry>
            {
                new PersistedShipEntry(new CoopShipId(1), Fp("cog", 111)),
            };
            var current = new List<ShipFingerprint> { Fp("cog", 111), Fp("longship", 222) };

            RebindResult result = ShipIdentityRebinder.Rebind(persisted, current);

            Assert.Equal(new CoopShipId(1), result.ResolvedIds[0]);
            Assert.Null(result.ResolvedIds[1]);
            Assert.Empty(result.FingerprintMismatches);
            Assert.Empty(result.UnmatchedPersistedIds);
        }

        [Fact]
        public void ShipRemoved_LeavesItsPersistedIdUnmatched()
        {
            var persisted = new List<PersistedShipEntry>
            {
                new PersistedShipEntry(new CoopShipId(1), Fp("cog", 111)),
                new PersistedShipEntry(new CoopShipId(2), Fp("longship", 222)),
            };
            var current = new List<ShipFingerprint> { Fp("cog", 111) };

            RebindResult result = ShipIdentityRebinder.Rebind(persisted, current);

            Assert.Equal(new CoopShipId(1), result.ResolvedIds[0]);
            Assert.Single(result.UnmatchedPersistedIds);
            Assert.Equal(new CoopShipId(2), result.UnmatchedPersistedIds[0]);
        }

        [Fact]
        public void FingerprintMismatchAtAPosition_IsFlaggedButStillResolvedPositionally()
        {
            // Simulates the S3 failure mode: MBList<Ship> did NOT preserve order through
            // save/load, so position 0 now holds a different ship than what was persisted
            // there. The rebinder must not silently trust it — it still returns the
            // positional id (that's the whole "LIKELY, not VERIFIED" design), but flags it.
            var persisted = new List<PersistedShipEntry>
            {
                new PersistedShipEntry(new CoopShipId(1), Fp("cog", 111)),
                new PersistedShipEntry(new CoopShipId(2), Fp("longship", 222)),
            };
            var current = new List<ShipFingerprint> { Fp("longship", 222), Fp("cog", 111) }; // swapped

            RebindResult result = ShipIdentityRebinder.Rebind(persisted, current);

            Assert.Equal(new CoopShipId(1), result.ResolvedIds[0]); // still positional
            Assert.Equal(new CoopShipId(2), result.ResolvedIds[1]);
            Assert.Equal(new[] { 0, 1 }, result.FingerprintMismatches);
        }

        [Fact]
        public void EmptyBoth_ProducesEmptyResultWithoutThrowing()
        {
            RebindResult result = ShipIdentityRebinder.Rebind(
                new List<PersistedShipEntry>(), new List<ShipFingerprint>());

            Assert.Empty(result.ResolvedIds);
            Assert.Empty(result.FingerprintMismatches);
            Assert.Empty(result.UnmatchedPersistedIds);
        }

        [Fact]
        public void NoPersistedHistory_EveryCurrentShipGetsNullSlot()
        {
            var current = new List<ShipFingerprint> { Fp("cog", 111), Fp("longship", 222) };

            RebindResult result = ShipIdentityRebinder.Rebind(new List<PersistedShipEntry>(), current);

            Assert.Null(result.ResolvedIds[0]);
            Assert.Null(result.ResolvedIds[1]);
        }
    }
}
