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

**L2 is deliberately an abstraction.** RISK-02 (is `GameNetwork` usable in a campaign session?) is UNCONFIRMED. The architecture must not depend on the answer, so L3–L6 talk only to an interface.

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

```
Server: EncounterManager.StartPartyEncounter(PartyBase, PartyBase)
          └─> MapEvent created            [CampaignEvents.MapEventStarted]
                ├─ MapEvent.IsNavalMapEvent ? naval : land
                ├─ MapEvent.BattleTypes: FieldBattle | Siege | SallyOut | Raid
                │                        | BlockadeBattle | BlockadeSallyOutBattle
                └─> Server decides: simulate, or instantiate a Mission
                      ├─ land:  PlayerEncounter.StartBattle() / StartAttackMission()
                      └─ naval: NavalMissions.OpenNavalBattleMission(MissionInitializerRecord)
                                NavalMissions.OpenNavalRaidMission(TroopRoster, BattleSideEnum, List<Ship>)
          <─ outcome ─ CampaignBattleResult
          └─> Server applies: casualties, loot, ChangeShipOwnerAction, DestroyShipAction
                             [CampaignEvents.MapEventEnded]
```

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

## 9. Module Layout (planned)

| Assembly | References | Purpose |
|---|---|---|
| `Coop.Core` | none (game-agnostic) | Transport interface, serialization, registries, state machines |
| `Coop.GameInterface` | `TaleWorlds.*` | Campaign binding, event subscription, action application |
| `Coop.GameInterface.Naval` | + `NavalDLC.dll` | **Optional**, loaded only when War Sails is detected |
| `Coop.Server` | `Coop.Core` | Dedicated server host |
| `Coop.Client` | `Coop.Core`, `Coop.GameInterface` | Client module |
| `tools/apiscan` | none (Python) | API extraction, version-drift CI gate |

## 10. Open Architectural Questions

These are **not** settled and are not to be treated as assumptions:

| # | Question | Confidence | Gate |
|---|---|---|---|
| A1 | Transport: `GameNetwork` module events vs. our own socket | UNCONFIRMED | RISK-02 probe (Phase 1.4) |
| A2 | Whether campaign AI ticks can run server-only with clients fully passive | UNCONFIRMED | Phase 1.5 harness |
| A3 | Whether naval missions can host multiple players at all | UNKNOWN | Needs install + RISK-03 |
| A4 | Interest management granularity (full world vs. spatial) | UNCONFIRMED | Phase 1.8 measurement |
| A5 | Whether `PartyBase` needs a synthesized id like `Ship` does | LIKELY yes | Phase 1.6 |
