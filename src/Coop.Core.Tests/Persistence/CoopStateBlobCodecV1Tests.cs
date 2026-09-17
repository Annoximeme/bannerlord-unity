using System;
using System.Collections.Generic;
using Coop.Core.Identity;
using Coop.Core.Persistence;
using Xunit;

namespace Coop.Core.Tests.Persistence
{
    public class CoopStateBlobCodecV1Tests
    {
        [Fact]
        public void EmptySnapshot_RoundTrips()
        {
            var snapshot = new CoopStateSnapshot(
                new List<PlayerBinding>(), nextShipId: 1, new List<OwnerShips>());

            CoopStateSnapshot decoded = CoopStateBlobCodecV1.Decode(CoopStateBlobCodecV1.Encode(snapshot));

            Assert.Empty(decoded.PlayerBindings);
            Assert.Equal(1ul, decoded.NextShipId);
            Assert.Empty(decoded.OwnersWithShips);
        }

        [Fact]
        public void PlayerBindings_RoundTrip()
        {
            var owner = new PartyBaseId(new EngineObjectId(42), OwnerKind.MobileParty);
            var snapshot = new CoopStateSnapshot(
                new List<PlayerBinding> { new PlayerBinding("steam:12345", owner) },
                nextShipId: 1,
                new List<OwnerShips>());

            CoopStateSnapshot decoded = CoopStateBlobCodecV1.Decode(CoopStateBlobCodecV1.Encode(snapshot));

            PlayerBinding binding = Assert.Single(decoded.PlayerBindings);
            Assert.Equal("steam:12345", binding.PlayerIdentity);
            Assert.Equal(owner, binding.Owner);
        }

        [Fact]
        public void ShipRegistry_RoundTrips_PreservingPerOwnerOrder()
        {
            var owner = new PartyBaseId(new EngineObjectId(7), OwnerKind.Settlement);
            var ships = new List<PersistedShipEntry>
            {
                new PersistedShipEntry(new CoopShipId(1), new ShipFingerprint("cog_hull", "The Gull", 100f, 50f, 111)),
                new PersistedShipEntry(new CoopShipId(2), new ShipFingerprint("longship_hull", "Sea Serpent", 80f, 40f, 222)),
            };
            var snapshot = new CoopStateSnapshot(
                new List<PlayerBinding>(),
                nextShipId: 3,
                new List<OwnerShips> { new OwnerShips(owner, ships) });

            CoopStateSnapshot decoded = CoopStateBlobCodecV1.Decode(CoopStateBlobCodecV1.Encode(snapshot));

            Assert.Equal(3ul, decoded.NextShipId);
            OwnerShips decodedOwnerShips = Assert.Single(decoded.OwnersWithShips);
            Assert.Equal(owner, decodedOwnerShips.Owner);
            Assert.Equal(ships[0].Id, decodedOwnerShips.Ships[0].Id);
            Assert.Equal(ships[0].Fingerprint, decodedOwnerShips.Ships[0].Fingerprint);
            Assert.Equal(ships[1].Id, decodedOwnerShips.Ships[1].Id);
            Assert.Equal(ships[1].Fingerprint, decodedOwnerShips.Ships[1].Fingerprint);
        }

        [Fact]
        public void Encode_AlwaysWritesCurrentSchemaVersion()
        {
            var snapshot = new CoopStateSnapshot(new List<PlayerBinding>(), 1, new List<OwnerShips>());

            byte[] bytes = CoopStateBlobCodecV1.Encode(snapshot);

            Assert.Equal(CoopStateBlobCodecV1.SchemaVersion, CoopStateBlobCodecV1.PeekSchemaVersion(bytes));
        }

        [Fact]
        public void Decode_WrongSchemaVersion_ThrowsRatherThanMisreadingBytes()
        {
            // Simulates a v2 blob being handed to the v1 decoder directly, without going
            // through SchemaMigrationChain first — must fail loudly (SAVE_FORMAT.md §5),
            // never silently misinterpret the bytes.
            var snapshot = new CoopStateSnapshot(new List<PlayerBinding>(), 1, new List<OwnerShips>());
            byte[] bytes = CoopStateBlobCodecV1.Encode(snapshot);
            bytes[0] = 99; // corrupt just the schema version header (little-endian low byte)

            Assert.Throws<InvalidOperationException>(() => CoopStateBlobCodecV1.Decode(bytes));
        }
    }
}
