using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Coop.Core.Identity;

namespace Coop.Core.Persistence
{
    /// <summary>
    /// Hand-rolled binary encoding for our own <see cref="CoopStateSnapshot"/> — explicitly
    /// sanctioned by SAVE_FORMAT.md §7 rule 1 ("never hand-serialize engine types... we
    /// persist only ids and our own data"; this is only ids, strings and primitives, never
    /// an engine object). Schema v1. A future v2 would live alongside this as its own codec,
    /// reached through <see cref="SchemaMigrationChain"/> rather than by editing this class.
    /// </summary>
    public static class CoopStateBlobCodecV1
    {
        public const uint SchemaVersion = 1;

        public static byte[] Encode(CoopStateSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            using var ms = new MemoryStream();
            using (var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(SchemaVersion);

                writer.Write(snapshot.PlayerBindings.Count);
                foreach (PlayerBinding binding in snapshot.PlayerBindings)
                {
                    writer.Write(binding.PlayerIdentity ?? "");
                    writer.Write(binding.Owner.OwnerId.Value);
                    writer.Write((byte)binding.Owner.OwnerKind);
                }

                writer.Write(snapshot.NextShipId);

                writer.Write(snapshot.OwnersWithShips.Count);
                foreach (OwnerShips ownerShips in snapshot.OwnersWithShips)
                {
                    writer.Write(ownerShips.Owner.OwnerId.Value);
                    writer.Write((byte)ownerShips.Owner.OwnerKind);

                    writer.Write(ownerShips.Ships.Count);
                    foreach (PersistedShipEntry entry in ownerShips.Ships)
                    {
                        writer.Write(entry.Id.Value);
                        writer.Write(entry.Fingerprint.HullStringId);
                        writer.Write(entry.Fingerprint.Name);
                        writer.Write(entry.Fingerprint.HitPoints);
                        writer.Write(entry.Fingerprint.SailHitPoints);
                        writer.Write(entry.Fingerprint.RandomValue);
                    }
                }
            }
            return ms.ToArray();
        }

        /// <summary>Reads just the schema version header, without decoding the rest — used to decide whether a migration is needed before calling <see cref="Decode"/>.</summary>
        public static uint PeekSchemaVersion(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            using var reader = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);
            return reader.ReadUInt32();
        }

        public static CoopStateSnapshot Decode(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));

            using var ms = new MemoryStream(bytes);
            using var reader = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

            uint version = reader.ReadUInt32();
            if (version != SchemaVersion)
            {
                throw new InvalidOperationException(
                    $"{nameof(CoopStateBlobCodecV1)}.{nameof(Decode)} expects schema v{SchemaVersion}, got v{version} — migrate first via {nameof(SchemaMigrationChain)}.");
            }

            int bindingCount = reader.ReadInt32();
            var bindings = new List<PlayerBinding>(bindingCount);
            for (int i = 0; i < bindingCount; i++)
            {
                string identity = reader.ReadString();
                var ownerId = new EngineObjectId(reader.ReadUInt32());
                var ownerKind = (OwnerKind)reader.ReadByte();
                bindings.Add(new PlayerBinding(identity, new PartyBaseId(ownerId, ownerKind)));
            }

            ulong nextShipId = reader.ReadUInt64();

            int ownerCount = reader.ReadInt32();
            var owners = new List<OwnerShips>(ownerCount);
            for (int i = 0; i < ownerCount; i++)
            {
                var ownerId = new EngineObjectId(reader.ReadUInt32());
                var ownerKind = (OwnerKind)reader.ReadByte();

                int shipCount = reader.ReadInt32();
                var ships = new List<PersistedShipEntry>(shipCount);
                for (int j = 0; j < shipCount; j++)
                {
                    var shipId = new CoopShipId(reader.ReadUInt64());
                    string hull = reader.ReadString();
                    string name = reader.ReadString();
                    float hitPoints = reader.ReadSingle();
                    float sailHitPoints = reader.ReadSingle();
                    int randomValue = reader.ReadInt32();
                    ships.Add(new PersistedShipEntry(shipId, new ShipFingerprint(hull, name, hitPoints, sailHitPoints, randomValue)));
                }

                owners.Add(new OwnerShips(new PartyBaseId(ownerId, ownerKind), ships));
            }

            return new CoopStateSnapshot(bindings, nextShipId, owners);
        }
    }
}
