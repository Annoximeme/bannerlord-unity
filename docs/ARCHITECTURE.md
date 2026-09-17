# Architecture

**Status:** Phase 0 design. Derived exclusively from `VERIFIED` TaleWorlds API findings in `docs/INITIAL_TECHNICAL_AUDIT.md`.
**Constraint:** Nothing here may be derived from the BannerlordCoop codebase — see RISK-00.

---

## 1. Target Capability Set

Multiple independent player parties · shared campaign world · PvP · co-op battles · sieges · armies · kingdoms · economy · quests · persistent progression · disconnect/reconnect · server restarts · naval travel · ships · fleets · naval battles · boarding · ship capture · ship destruction · naval loot · seaborne raids · War Sails campaign content.

## 2. Topology

**Authoritative dedicated server.** One process owns campaign truth; clients are replicas with local prediction for their own party only.

Rejected alternatives and why:

| Model | Verdict | Reason |
|---|---|---|
| Lockstep determinism | **Rejected** | Campaign determinism is UNCONFIRMED (RISK-04) and `Ship` physics in missions is float-based. Lockstep fails catastrophically and silently on any divergence. |
| Peer-to-peer / host-migration | **Rejected** | Cannot satisfy "server restarts" or "disconnect/reconnect" with a coherent world. |
| Authoritative server + replication | **Chosen** | Tolerates non-determinism, survives restarts, gives a single save of record, and matches the `[SaveableField]`/`[CachedData]` split the engine already exposes. |

```
                    ┌─────────────────────────────┐
                    │   DEDICATED SERVER          │
                    │   Campaign = source of truth│
                    │   Owns: world, AI, economy, │
                    │   time, MapEvents, ships    │
                    │   Writes: the save of record│
                    └────────────┬────────────────┘
                     replication │ commands
       ┌─────────────────────────┼─────────────────────────┐
       ▼                         ▼                         ▼
  ┌─────────┐              ┌─────────┐              ┌─────────┐
  │ Client A│              │ Client B│              │ Client C│
  │ owns its│              │ owns its│              │ owns its│
  │ party   │              │ party   │              │ party   │
  │ (intent)│              │ (intent)│              │ (intent)│
  └─────────┘              └─────────┘              └─────────┘
```

Clients never mutate shared campaign state directly. They send **intent**; the server applies it through the game's own action APIs and replicates the result.

## 3. Layer Model

| Layer | Responsibility | Key verified types |
|---|---|---|
| **L0 Platform** | Version gate, module load, DLC detection | `ApplicationVersion`, `ApplicationVersionType`, `ModuleInfo`, `NavalVersion` |
| **L1 Identity** | Stable cross-process object names | `MBObjectBase.Id` (`MBGUID`), `.StringId`; **+ our synthesized ship ids** |
| **L2 Transport** | Reliable/unreliable ordered channels | `GameNetwork` module events *or* our own socket — see `NETWORK_PROTOCOL.md` §2 |
| **L3 Replication** | State diffs, command routing, interest management | Our code, driven by `[Saveable*]` metadata |
| **L4 Domain sync** | Parties, settlements, battles, sieges, economy, **ships** | `CampaignEvents` (277), campaign `Action` classes |
| **L5 Mission bridge** | Campaign ↔ battle handoff, incl. naval | `PlayerEncounter`, `MapEvent`, `Mission`, `NavalMissions` |
| **L6 Persistence** | Save of record, reconnect, restart | `SaveableTypeDefiner`, `IDataStore.SyncData` |

**L2 is deliberately an abstraction.** RISK-02 (is `GameNetwork` usable in a campaign session?) is **RESOLVED — no**, it crashes the engine (Phase 1.4). Keeping L3–L6 talking only to `ICoopTransport` paid off exactly as planned: `TcpCoopTransport` (Phase 1.7) slotted in with no rework needed elsewhere.

## 4. Authority Rules

| Domain | Authority | Client role |
|---|---|---|
| Campaign clock | Server | Display only |
| Own party movement | **Client intent → server applies** | Predict locally, reconcile |
| Other parties (player & AI) | Server | Interpolate |
| AI decisions | Server | None |
| Settlements, economy, prices | Server | None |
| Clans, kingdoms, diplomacy | Server | Request only |
| `MapEvent` creation/resolution | Server | Request join |
| **Ship ownership** | **Server, exclusively** | Request only |
| Ship hit points / damage | Server | Display |
| In-mission agent control | Owning client | Server reconciles outcome |
| Wind (`INavalMapSceneWrapper.GetWindAtPosition`) | Server | Display/apply replicated value |
| Storms (`StormManager`) | Server | Display |
| Map terrain (`TerrainType`) | **Neither — static map data** | Query locally, never replicate |
| Quests | Server | Request only |

**Rule:** if it carries `[SaveableField]` or `[SaveableProperty]`, it is server-authoritative. If it carries `[CachedData]`, it is derived and must be recomputed locally, never replicated. This split is directly readable from assembly metadata (VERIFIED) and is the backbone of §3 in `SYNCHRONIZATION_MODEL.md`.

## 5. Campaign Data Model (verified anchors)

```
Campaign (root)
└── MBObjectManager ── MBGUID identity for every MBObjectBase
    ├── Hero, Clan, Kingdom, Settlement, CharacterObject, ItemObject   [MBGUID ✓]
    └── MobileParty : CampaignObjectBase : MBObjectBase               [MBGUID ✓]
        ├── .Party : PartyBase                    [System.Object — NO MBGUID ✗]
        │   ├── ._ships : MBList<Ship>            [Ship: NO MBGUID ✗]
        │   ├── .FlagShip : Ship
        │   ├── MemberRoster / PrisonRoster / ItemRoster
        │   └── .AddShipInternal / .RemoveShipInternal   [internal]
        ├── .Ai : MobilePartyAi
        ├── ._army : Army
        ├── ._besiegerCamp : BesiegerCamp
        └── naval: .IsCurrentlyAtSea, .IsInRaftState, .Anchor, .IsTargetingPort
```

The two identity holes — `PartyBase` and `Ship` — are the structural reason RISK-01 exists and why L1 is its own layer.

## 6. Battle / Mission Flow

**B7, resolved (2026-09-17, `ilspycmd` against the real install, VERIFIED — not the reference assembly, which has no method bodies).** The actual trigger is a game-menu consequence, `Helpers.MenuHelper.EncounterAttackConsequence(MenuCallbackArgs)` — the "Attack" option's callback — not a bare campaign-layer dispatch:

```
Server: EncounterManager.StartPartyEncounter(PartyBase, PartyBase)
          └─> MapEvent created            [CampaignEvents.MapEventStarted]
          └─> player picks "Attack" in the encounter menu
                └─> MenuHelper.EncounterAttackConsequence(args)
                      ├─ BeHostileAction.ApplyEncounterHostileAction(...)
                      ├─ settlement fortification, siege ambush/assault/sally-out/blockade
                      │     → PlayerEncounter.StartSiegeAmbushMission() / PlayerSiege.Start*
                      ├─ settlement is a village, raid in progress
                      │     → MapEventHelper.GetRaidContext(...) classifies sea/land
                      │       presence on each side (purely MobileParty.IsCurrentlyAtSea,
                      │       no mission-layer state) → StartSeaRaidMission(...) or
                      │       PlayerEncounter.StartVillageBattleMission(), or — when both
                      │       sides are sea-only — CampaignMission.OpenNavalBattleMission
                      │       directly
                      ├─ settlement is a hideout
                      │     → CampaignMission.OpenHideoutBattleMission("sea_bandit_a", ...)
                      └─ general field battle (no settlement)
                            ├─ PlayerEncounter.IsNavalEncounter() (= MapEvent.IsNavalMapEvent)
                            │     → true:  CampaignMission.OpenNavalBattleMission(rec)
                            ├─ caravan/village-defender on the far side
                            │     → CampaignMission.OpenCaravanBattleMission(rec, ...)
                            └─ else
                                  → CampaignMission.OpenBattleMission(rec)
                └─> CampaignMission.Open*(...) routes through Campaign.Current.CampaignMissionManager
                      — the base SandBox.CampaignMissionManager, or NavalDLC's decorating
                      NavalMissionManager for the three naval methods (RISK-03 decorator
                      seam, already documented below) — into NavalDLC.Missions.NavalMissions
                      for the naval cases.
                └─> PlayerEncounter.StartAttackMission() (resets CampaignBattleResult)
                └─> MapEvent.PlayerMapEvent.BeginWait()
          <─ outcome ─ CampaignBattleResult
          └─> Server applies: casualties, loot, ChangeShipOwnerAction, DestroyShipAction
                             [CampaignEvents.MapEventEnded]
```

`StartSeaRaidMission` (also in `MenuHelper`) is worth noting on its own: when the player is on the raiding side, it opens a troop-and-ship selection UI (`MenuContext.OpenNavalTroopSelection`, capped at 3 ships by shallow-draft/crew-capacity) and only calls `CampaignMission.OpenNavalRaidMission(troops, navalSide, selectedShips)` from that UI's completion callback — the mission doesn't open until the player has chosen which ships go.

**The mission layer is a subordinate simulation.** Its result is reported to the server, which alone commits campaign consequences. No client may apply a campaign mutation from a mission outcome.

## 7. Naval Integration Principle

Because the naval **data model is in the base game** and only the **behaviors/missions are in the DLC** (VERIFIED, audit §3):

- Ship ownership, fleets, at-sea flags, and naval `MapEvent` types sync against **`TaleWorlds.CampaignSystem.dll`** and need no `NavalDLC` reference.
- `NavalDLC.dll` is referenced only by an **optional** assembly that is loaded when the DLC is detected.
- Consequence: the core can be built, tested and shipped **before** naval missions work, and a DLC-less client degrades to "no naval systems" rather than "cannot connect".

Detail: `docs/WARSAILS_ARCHITECTURE.md`.

## 8. Lifecycle

Full state machine in `docs/NETWORK_PROTOCOL.md` §5. Summary:

`Server boot → load save of record → listen → client connect → version/DLC handshake → full world snapshot → delta stream → play`
`Disconnect → party frozen server-side, state retained → reconnect → resync from snapshot + deltas`
`Server restart → save of record reloaded → clients reconnect → identity registries rehydrated from persisted ids`

## 9. Service Interfaces (mandated by `CLAUDE.md`)

`CLAUDE.md` requires game-independent logic to be separated from Bannerlord-specific implementation, with raw TaleWorlds API calls confined to adapters. Every service below is an interface in `Coop.Core` with a Bannerlord adapter in `Coop.GameInterface`.

| Service | Wraps (verified anchors) | Notes |
|---|---|---|
| `ICampaignService` | `Campaign`, `CampaignEvents` (277), `CampaignTime`, `Campaign.SupportsSaving` | Clock, event hub, campaign root |
| `IPartyService` | `MobileParty`, `PartyBase`, `MobilePartyAi`, `Army` | Movement, rosters, army membership |
| `ISettlementService` | `Settlement`, `Town`, `Village`, `Alley`, `Buildings` | Ownership, garrisons, production |
| `IBattleService` | `EncounterManager`, `PlayerEncounter`, `MapEvent`, `MapEventSide`, `CampaignBattleResult` | **Volatile — see RISK-05** |
| `ISiegeService` | `SiegeEvent`, `BesiegerCamp`, `BlockadeBattleMapEvent` | Incl. naval blockade |
| `INavalService` | `MobileParty.IsCurrentlyAtSea/IsInRaftState/Anchor`, `TerrainType`, `IMapScene`, `INavalMapSceneWrapper`, `StormManager` | Naval movement, water navigation, weather |
| `IShipService` | `Naval.Ship`, `ChangeShipOwnerAction`, `DestroyShipAction`, `RepairShipAction` | **Reshaped between versions — adapter mandatory** |
| `IFleetService` | `PartyBase.Ships`/`.FlagShip`/`GetShipsVersion()`, `FleetManagementModel`, `PartyShipLimitModel` | No engine `Fleet` type exists |
| `IQuestService` | `QuestBase`, `NavalStorylineQuestBase` | Phase 5 (RISK-11) |
| `IInventoryService` | `ItemRoster`, `TroopRoster`, `FlattenedTroopRoster` | |
| `IEconomyService` | Gold, trade, prices, `HeroOrPartyTradedGold` | |
| `ICharacterService` | `Hero`, `CharacterObject`, skills, perks, `NavalPerks` | |
| `INetworkService` | `ICoopTransport` (see §3 L2) | **Never binds `GameNetwork` directly — RISK-02** |
| `ISaveService` | `SaveableTypeDefiner`, `IDataStore.SyncData`, `ISaveDriver` | Versioned/atomic writes per `SAVE_FORMAT.md` |

**Rule:** no raw `TaleWorlds.*` call outside `Coop.GameInterface*`. This is what makes RISK-05 (2,638 members changed between versions) survivable — version conditionals live only in adapters.

### Assembly layout

| Assembly | References | Purpose |
|---|---|---|
| `Coop.Core` | none (game-agnostic) | Service interfaces, transport interface, serialization, registries, state machines |
| `Coop.GameInterface` | `TaleWorlds.*` | Adapters implementing the services above |
| `Coop.GameInterface.Naval` | + `NavalDLC.dll` | **Optional**, loaded only when War Sails is detected |
| `Coop.Server` | `Coop.Core` | Dedicated server host |
| `Coop.Client` | `Coop.Core`, `Coop.GameInterface` | Client module |
| `Coop.Tests` / `Coop.IntegrationTests` / `Coop.E2E.Tests` | | Unit, integration, network, save/load, end-to-end (mandated by `CLAUDE.md`) |
| `tools/apiscan` | none (Python) | API extraction, version-drift CI gate |

## 10. Idempotency (mandated by `CLAUDE.md` §5)

**Campaign consequences must never be applied twice.** A duplicated or replayed packet must not duplicate loot, gold, XP, casualties, prisoners, renown, influence, quest rewards, ship rewards, ship destruction, or settlement changes.

Design:

```
ConsequenceId : (serverEpoch:u32, sequence:u64)   // globally unique, monotonic
```

- Every state-changing effect the server applies carries a `ConsequenceId`.
- The server keeps an applied-set, persisted in the save of record (`SAVE_FORMAT.md` §3.3), so idempotency survives a restart.
- Clients keep a received-set per epoch and discard replays.
- Effects are applied **exactly once**, inside a guard that checks-and-records atomically.
- `serverEpoch` increments on every server start, so ids from a previous run can never collide with a new one.

This matters most where the engine's own actions are not naturally idempotent: `ChangeShipOwnerAction.ApplyByLooting`, `DestroyShipAction.Apply`, `MapEvent.LootDefeatedPartyShips`, and all roster/gold mutations.

## 11. Open Architectural Questions

These are **not** settled and are not to be treated as assumptions:

| # | Question | Confidence | Gate |
|---|---|---|---|
| ~~A1~~ | ~~Transport: `GameNetwork` module events vs. our own socket~~ | **RESOLVED — our own socket.** `GameNetwork` crashes the engine (RISK-02, Phase 1.4). `Coop.Core.Network.TcpCoopTransport` (plain `System.Net.Sockets`) implemented and proven with real sockets, Phase 1.7. |
| A2 | Whether campaign AI ticks can run server-only with clients fully passive | UNCONFIRMED | Phase 1.5 harness |
| A3 | Whether naval missions can host multiple players at all | UNKNOWN | Needs install + RISK-03 |
| A4 | Interest management granularity (full world vs. spatial) | UNCONFIRMED | Phase 1.8 measurement |
| ~~A5~~ | ~~Whether `PartyBase` needs a synthesized id like `Ship` does~~ | **RESOLVED, no** — `PartyBase` is always reachable from an identified `MobileParty` or `Settlement` owner, both already `MBObjectBase`-derived; addressed as `PartyBaseId = (ownerId, ownerKind)` instead of giving it an id of its own. Implemented, Phase 1.6: `Coop.Core.Identity.PartyBaseId`/`OwnerKind`, `Coop.GameInterface.Identity.PartyBaseIdAdapter`. |
