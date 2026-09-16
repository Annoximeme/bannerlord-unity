# Synchronization & Ownership Model

**Goal 27 deliverable.** Built only on `VERIFIED` findings. Items that depend on `UNCONFIRMED` facts are marked and gated.

---

## 1. Core Principle

**Single-writer authority.** Every piece of replicated state has exactly one writer: the dedicated server. Clients emit *intent*; the server validates, applies through the game's own APIs, and replicates the result.

No shared-write state. No client-side campaign mutation. No merge resolution.

## 2. Ownership Tiers

| Tier | State | Writer | Readers | Replication |
|---|---|---|---|---|
| **T0 World** | Campaign clock, weather/storms, world AI, economy, diplomacy, settlement ownership | Server only | All | Delta broadcast |
| **T1 Shared objects** | Settlements, clans, kingdoms, AI parties, `MapEvent`s | Server only | All | Delta broadcast, interest-filtered |
| **T2 Owned party** | A player's `MobileParty` + `PartyBase` | Server (from client intent) | All | Intent up, state down |
| **T3 Owned inventory** | Rosters, gold, items, **ships** of an owned party | Server (from client intent) | Owner + relevant | Intent up, state down |
| **T4 Mission local** | Agent positions, animations, in-mission physics | Owning client | Mission participants | High-rate, lossy |
| **T5 Derived** | `[CachedData]` members, computed stats | **Nobody** | Local | **Never replicated** — recomputed |

## 3. The Metadata-Driven Authority Rule

The engine already annotates its own state, and we read those annotations directly (VERIFIED — `tools/apiscan/cli_meta.py` surfaces them):

| Attribute | Meaning for us |
|---|---|
| `[TaleWorlds.SaveSystem.SaveableFieldAttribute]` | Persistent truth → **server-authoritative, replicated** |
| `[TaleWorlds.SaveSystem.SaveablePropertyAttribute]` | Persistent truth → **server-authoritative, replicated** |
| `[TaleWorlds.Library.CachedDataAttribute]` | Derived cache → **never replicate**, recompute locally |
| `[System.Runtime.CompilerServices.CompilerGeneratedAttribute]` on a backing field | Inspect the property instead |
| No attribute | **Investigate individually** — do not assume |

Worked example on `MobileParty` (all VERIFIED):

| Member | Attribute | Decision |
|---|---|---|
| `_targetPosition`, `_targetParty`, `_targetSettlement` | `[SaveableField]` | Replicate |
| `_army`, `_besiegerCamp`, `_currentSettlement`, `_attachedTo` | `[SaveableField]` | Replicate (as ids) |
| `_isCurrentlyAtSea`, `_isInRaftState`, `_isTargetingPort` | `[SaveableField]` | Replicate |
| `NextTargetPosition`, `MoveTargetPoint`, `_pathMode`, `_pathLastPosition` | `[SaveableField]` | Replicate |
| `_lastCalculatedSpeed`, `_cachedPartySizeRatio`, `_partyLastCheckAtNight`, `_itemRosterVersionNo` | `[CachedData]` | **Never replicate** |
| `NextLongTermPathPoint`, `_lastCalculatedBaseSpeedExplained` | `[CachedData]` | **Never replicate** |
| `_attachedParties` | `[CachedData]` | **Never replicate** — rebuild from `_attachedTo` |

**This eliminates an entire class of desync bugs by construction:** replicating a cache and then recomputing it is how co-op mods produce drift. The engine tells us which is which; we obey it mechanically rather than by judgement.

> **Generate, don't hand-write.** `tools/apiscan` can emit the replicated-state manifest from real metadata. Phase 1.7 in `ROADMAP.md`.

## 4. Identity — including the `Ship` problem (RISK-01)

### 4.1 What the engine gives us

| Type family | Identity | Stable across save/load | Stable across processes |
|---|---|---|---|
| Anything `: MBObjectBase` (Hero, Clan, Kingdom, Settlement, **MobileParty**, CharacterObject, ItemObject…) | `MBGUID Id` + `string StringId`, both `[SaveableProperty]` | **Yes** (VERIFIED — persisted) | **Yes**, provided both sides load the same save |
| `PartyBase` | **None** | No | No — but reachable via its owning `MobileParty`/`Settlement` |
| **`Ship`** | **None** | **No** | **No** |
| `AnchorPoint` | **None** | No | No |

### 4.2 Design: `MBGUID`-backed registry for `MBObjectBase`

Straightforward. `MBGUID` wraps a `uint _internalValue` (`[SaveableField]`) with full equality/ordering (VERIFIED), so it is directly usable as a wire key. Wire format in `NETWORK_PROTOCOL.md` §4.

### 4.3 Design: synthesized persistent identity for `Ship`

Because `Ship` has no engine identity, we assign our own.

```
CoopShipId : ulong        // monotonic, server-assigned, never reused

Server:
  ConditionalWeakTable<Ship, CoopShipId>   forward map (no lifetime leak)
  Dictionary<CoopShipId, Ship>             reverse map

Persistence (survives save/load AND server restart):
  Our own CampaignBehaviorBase.SyncData(IDataStore) stores, per PartyBase:
      ownerKey  : MBGUID of the owning MobileParty/Settlement
      ships     : ordered CoopShipId[]  aligned to PartyBase.Ships order
      nextId    : ulong
  On load: walk each party's PartyBase.Ships in order and re-bind ids positionally.
```

**Why positional rebinding is sound here:** `PartyBase._ships` is an `MBList<Ship>` serialized as an ordered container by the save system (VERIFIED: it is a `[SaveableField]` of a list type), so element order is preserved through save/load. Ownership is also directly persisted on `Ship._owner` (`[SaveableField]`, VERIFIED), giving a second, independent cross-check.

**Assumptions this rests on, and their status:**

| Assumption | Confidence | Mitigation if false |
|---|---|---|
| `MBList<Ship>` preserves element order through save/load | **LIKELY** (ordered container type; not runtime-verified) | Fall back to a content fingerprint: `(ShipHull.StringId, _name, _hitPoints, _sailHitPoints, RandomValue)`. `RandomValue` is `[SaveableProperty]` and per-ship, making collisions unlikely. |
| Server and clients agree on ship ordering | VERIFIED by design | Ids are server-assigned and replicated; clients never derive them |
| `Ship` instances are not silently replaced on load | **UNCONFIRMED** | Fingerprint fallback above; validated by the Phase 1 save/load round-trip test |

> **Gate:** the ordering assumption is `LIKELY`, not `VERIFIED`. Phase 1.9 must include an explicit save → load → id-stability test before any naval feature depends on it. Until that passes, ship ids are treated as session-scoped only.

`Ship.VersionNo` / `UpdateVersionNo()` / `PartyBase.GetShipsVersion()` are useful **change hints** (VERIFIED they exist), but `_versionNo` is **not** `[SaveableField]` (VERIFIED) — it resets on load, so it must never be used as an identity or as a cross-session baseline.

### 4.4 `PartyBase` identity

`PartyBase` has no id but is always reachable from an identified owner (`MobileParty.Party`, or a `Settlement`). Address it as `(ownerMBGUID, ownerKind)` rather than giving it an id of its own.

## 5. Replication Mechanics

| Concern | Approach |
|---|---|
| Transport | Reliable ordered for state/commands; unreliable for high-rate mission data (`NETWORK_PROTOCOL.md`) |
| Granularity | Per-object field deltas, not whole-object snapshots |
| Change detection | Primary: `CampaignEvents` (277 events, VERIFIED). Secondary: version counters (`Ship.VersionNo`, `PartyBase.GetShipsVersion()`, `MobileParty.VersionNo`). Last resort: periodic diff sweep. |
| Interest management | Phase 1: broadcast everything (simple, correct). Phase 2: spatial/relevance filtering once measured. (A4 in `ARCHITECTURE.md`.) |
| Ordering | Per-object sequence numbers; server assigns, client applies in order |
| Tick rate | Campaign state is low-frequency; mission state is high-frequency. Separate channels. |

## 6. Command (Intent) Model

Clients send intent, never state:

| Intent | Server applies via | Confidence |
|---|---|---|
| Move my party | `MobileParty` target setters (`_targetPosition` / `MoveTargetPoint` / `SetMoveGoToPoint`-family) | VERIFIED members exist |
| Enter settlement | `EncounterManager.StartSettlementEncounter(MobileParty, Settlement)` | VERIFIED |
| Engage a party | `EncounterManager.StartPartyEncounter(PartyBase, PartyBase)` | VERIFIED |
| Join a battle | `PlayerEncounter.JoinBattle(BattleSideEnum)` | VERIFIED |
| Trade / give items | `ChangeShipOwnerAction.ApplyByTrade`, roster APIs; validated server-side | VERIFIED |
| Transfer a ship | `ChangeShipOwnerAction.ApplyByTransferring(PartyBase, Ship)` | VERIFIED |
| Repair a ship | `RepairShipAction.Apply(Ship, Settlement)` | VERIFIED |
| Set sail / anchor | `MobileParty.IsCurrentlyAtSea` (public set), `SetAnchor(AnchorPoint)` | VERIFIED |
| Raft toggle | `RaftStateChangeAction.Activate/DeactivateRaftStateForParty(MobileParty)` | VERIFIED |

**Every intent is validated server-side.** Client-supplied ids are resolved against the server's registries; unresolvable or unauthorized ids are rejected and logged, never trusted.

## 7. Conflict Resolution

Single-writer authority means conflicts reduce to **intent races**, resolved by arrival order at the server:

| Race | Resolution |
|---|---|
| Two players engage the same AI party | First intent creates the `MapEvent`; second is converted to a join request |
| Two players claim the same ship | First `ApplyByTransferring` wins; second fails validation (ship's `_owner` no longer matches) |
| Player acts on stale state | Server rejects; client reconciles from the authoritative delta |
| Player disconnects mid-intent | Intent completes or is discarded atomically server-side; never half-applied |

## 8. PvP

PvP is a `MapEvent` with player parties on both `BattleSideEnum` sides. Nothing structurally new: the server owns the `MapEvent`, both clients are participants, and `CampaignBattleResult` is applied server-side only. The open question is not ownership but **mission hosting** (A3), which is gated on RISK-03.

## 9. Known Weak Points

| # | Weak point | Confidence | Gate |
|---|---|---|---|
| S1 | Campaign AI/economy may not tick identically server vs client | UNCONFIRMED | Server-authoritative design already assumes it doesn't; verified by Phase 1.5 harness |
| S2 | `CampaignEvents` ordering/re-entrancy unknown | UNCONFIRMED | Phase 1.5 harness |
| S3 | Ship id stability across save/load rests on a `LIKELY` ordering assumption | LIKELY | Phase 1.9 round-trip test + fingerprint fallback |
| S4 | `PartyBase.AddShipInternal`/`RemoveShipInternal` are `internal` | VERIFIED | Publicizer or Harmony; decide in Phase 1.6 |
| S5 | Mission-layer naval authority entirely unresolved | UNKNOWN | Out of Phase 1 scope |
| S6 | Interest management unmeasured; full broadcast may not scale to 8 players | UNCONFIRMED | Phase 1.8 measurement |
