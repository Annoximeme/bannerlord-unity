using System.Collections.Generic;
using Coop.Core.Identity;

namespace Coop.Core.Persistence
{
    /// <summary>One player-identity → owning-party binding (SAVE_FORMAT.md §3.1). Required for reconnect to work after a server restart.</summary>
    public readonly struct PlayerBinding
    {
        public string PlayerIdentity { get; }
        public PartyBaseId Owner { get; }

        public PlayerBinding(string playerIdentity, PartyBaseId owner)
        {
            PlayerIdentity = playerIdentity;
            Owner = owner;
        }
    }

    /// <summary>One owner's ships, in <c>PartyBase.Ships</c> order — what <see cref="Identity.ShipIdentityRebinder"/> rebinds against on load (SAVE_FORMAT.md §3.2).</summary>
    public readonly struct OwnerShips
    {
        public PartyBaseId Owner { get; }
        public IReadOnlyList<PersistedShipEntry> Ships { get; }

        public OwnerShips(PartyBaseId owner, IReadOnlyList<PersistedShipEntry> ships)
        {
            Owner = owner;
            Ships = ships;
        }
    }

    /// <summary>Everything we persist ourselves (SAVE_FORMAT.md §3) — the whole point being that the engine never needs to know about any of this; it's our own data, addressed by ids.</summary>
    public sealed class CoopStateSnapshot
    {
        public IReadOnlyList<PlayerBinding> PlayerBindings { get; }
        public ulong NextShipId { get; }
        public IReadOnlyList<OwnerShips> OwnersWithShips { get; }

        public CoopStateSnapshot(
            IReadOnlyList<PlayerBinding> playerBindings,
            ulong nextShipId,
            IReadOnlyList<OwnerShips> ownersWithShips)
        {
            PlayerBindings = playerBindings;
            NextShipId = nextShipId;
            OwnersWithShips = ownersWithShips;
        }
    }
}
