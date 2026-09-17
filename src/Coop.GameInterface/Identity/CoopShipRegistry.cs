using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Coop.Core.Identity;
using TaleWorlds.CampaignSystem.Naval;

namespace Coop.GameInterface.Identity
{
    /// <summary>
    /// Server-side registry binding real <see cref="Ship"/> instances to synthesized
    /// <see cref="CoopShipId"/>s (docs/SYNCHRONIZATION_MODEL.md §4.3). Forward lookup uses a
    /// <see cref="ConditionalWeakTable{TKey,TValue}"/> so a <see cref="Ship"/> the engine
    /// drops (destroyed, its owning party gone) doesn't leak here just because we once
    /// looked at it.
    ///
    /// Cross-session persistence (SyncData wiring, the save/load round-trip test) is
    /// Phase 1.9 scope — this satisfies Phase 1.6's exit criterion, that every current
    /// Ship is addressable by a stable id for the running session.
    /// </summary>
    public sealed class CoopShipRegistry
    {
        private sealed class IdBox
        {
            public readonly CoopShipId Id;
            public IdBox(CoopShipId id) => Id = id;
        }

        private readonly CoopShipIdAllocator _allocator;
        private readonly ConditionalWeakTable<Ship, IdBox> _forward = new ConditionalWeakTable<Ship, IdBox>();
        private readonly Dictionary<CoopShipId, Ship> _reverse = new Dictionary<CoopShipId, Ship>();

        public CoopShipRegistry(CoopShipIdAllocator allocator = null)
        {
            _allocator = allocator ?? new CoopShipIdAllocator();
        }

        /// <summary>Fast-forwards the allocator past a persisted "next id" — call this before allocating anything, on load.</summary>
        public void ObservePersistedNextId(ulong persistedNextId) => _allocator.ObserveMinimumNext(persistedNextId);

        /// <summary>Returns the ship's existing id, or allocates and binds a new one.</summary>
        public CoopShipId GetOrAssign(Ship ship)
        {
            if (_forward.TryGetValue(ship, out IdBox existing))
            {
                return existing.Id;
            }

            CoopShipId id = _allocator.Allocate();
            Bind(ship, id);
            return id;
        }

        /// <summary>Binds a ship to a specific id — used when restoring from a persisted/rebound id rather than allocating fresh.</summary>
        public void Bind(Ship ship, CoopShipId id)
        {
            // ConditionalWeakTable in net472 has no AddOrUpdate; Remove-then-Add is the
            // documented way to rebind an existing key.
            _forward.Remove(ship);
            _forward.Add(ship, new IdBox(id));
            _reverse[id] = ship;
        }

        public bool TryGetId(Ship ship, out CoopShipId id)
        {
            if (_forward.TryGetValue(ship, out IdBox box))
            {
                id = box.Id;
                return true;
            }
            id = CoopShipId.None;
            return false;
        }

        public bool TryGetShip(CoopShipId id, out Ship ship) => _reverse.TryGetValue(id, out ship);

        /// <summary>Drops the binding — call when a ship is destroyed or otherwise permanently gone.</summary>
        public void Forget(Ship ship)
        {
            if (_forward.TryGetValue(ship, out IdBox box))
            {
                _reverse.Remove(box.Id);
                _forward.Remove(ship);
            }
        }

        /// <summary>
        /// Applies a <see cref="ShipIdentityRebinder"/> result for one party's ships (in
        /// <see cref="TaleWorlds.CampaignSystem.Party.PartyBase.Ships"/> order): binds every
        /// resolved id, allocates a fresh one for anything the rebinder left unresolved.
        /// </summary>
        public void ApplyRebindResult(IReadOnlyList<Ship> shipsInOrder, RebindResult result)
        {
            for (int i = 0; i < shipsInOrder.Count; i++)
            {
                CoopShipId id = result.ResolvedIds[i] ?? _allocator.Allocate();
                Bind(shipsInOrder[i], id);
            }
        }
    }
}
