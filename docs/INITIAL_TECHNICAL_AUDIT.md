# Phase 0 — Initial Technical Audit

**Date:** 2026-09-16
**Status:** Complete (with two goals unanswerable in this environment — see §1)
**Audit method:** Direct metadata inspection of official TaleWorlds assemblies + public repository inspection.

---

## Confidence Legend

| Level | Meaning |
|---|---|
| **VERIFIED** | Read directly out of shipped assembly metadata or a primary source in this session. Reproducible with the tooling in `tools/apiscan/`. |
| **LIKELY** | Strongly implied by verified evidence, but the specific claim was not itself observed. |
| **UNCONFIRMED** | Plausible, requires a runtime experiment or a source we do not have. |
| **UNKNOWN** | No evidence either way. |

**Rule enforced throughout:** nothing below `VERIFIED` may become an implementation assumption. `LIKELY`/`UNCONFIRMED`/`UNKNOWN` items are tracked as investigation tasks in `docs/ROADMAP.md` and `docs/RISK_REGISTER.md`.

---

## 0. Audit Environment (read this first)

| Fact | Value | Confidence | Verified |
|---|---|---|---|
| Audit host OS | Linux 6.18.44 container (`/home/user/bannerlordcoop`) | VERIFIED | Yes |
| Bannerlord installed on audit host | **No** — filesystem sweep for `*Bannerlord*`, `TaleWorlds*.dll`, `steamapps` returned nothing | VERIFIED | Yes |
| War Sails installed on audit host | **No** (same sweep) | VERIFIED | Yes |
| .NET / Mono / ILSpy available | **No** (`dotnet`, `mono`, `ilspycmd`, `msbuild` all absent) | VERIFIED | Yes |
| Project repository state at audit start | Empty — zero commits, no `CLAUDE.md` | VERIFIED | Yes |

### Consequence

Goals 1–3 as literally worded ("detect the **exact installed** version") **cannot be answered from this machine**: there is no installation to inspect. Rather than guess, the audit did two things:

1. Enumerated the **complete set of game versions that actually exist**, and which are stable vs. beta, from the authoritative public package feed (§1).
2. Specified the **exact runtime procedure** the project will use to detect version and DLC presence on a real machine (§1.4), built on APIs verified in this session.

To replace the analysis substrate that a local install would have given, the audit downloaded the **official TaleWorlds assemblies** as published reference assemblies and parsed their CLI metadata directly.

### Evidence substrate

| Item | Value | Confidence | Verified |
|---|---|---|---|
| Source | `Bannerlord.ReferenceAssemblies.*` on nuget.org, published by the BUTR project | VERIFIED | Yes |
| Versions pulled | `1.4.8.119303` (latest **stable**) and `1.5.3.122374-beta` (latest **beta**) | VERIFIED | Yes |
| Packages pulled | Core, Native, SandBox, StoryMode, **NavalDLC**, Multiplayer, CustomBattle (×2 versions = 14 packages) | VERIFIED | Yes |
| Parser | `tools/apiscan/cli_meta.py` — ECMA-335 PE/CLI metadata reader written for this audit (no .NET toolchain required) | VERIFIED | Yes |
| Metadata completeness | Reference assemblies retain **private fields**, **custom attributes** (incl. `[SaveableField]`), properties, events, and full method signatures. Method **bodies** are stripped. | VERIFIED | Yes |

> **Why this matters:** because `[SaveableField]`/`[SaveableProperty]` attributes survive, the save schema and the exact persisted state of every campaign object are directly readable. Because method bodies are stripped, *control flow and ordering* are not — every claim about "when" or "in what order" something happens is marked `UNCONFIRMED` below.

Reproduce any finding:
```bash
python3 tools/apiscan/cli_meta.py type <assembly.dll> <FullTypeName>
python3 tools/apiscan/cli_meta.py grep '<regex>' <assembly.dll> ...
python3 tools/apiscan/cli_meta.py surface <assembly.dll>   # diffable API listing
```

---

## 1. Version Landscape (Goals 1–3)

### 1.1 Version line

`ApplicationVersion` is the game's version type.

| Item | Exact symbol | Assembly | Confidence | Verified |
|---|---|---|---|---|
| Version struct | `TaleWorlds.Library.ApplicationVersion` | `TaleWorlds.Library.dll` | VERIFIED | Yes |
| Fields | `.Major`, `.Minor`, `.Revision`, `.ChangeSet`, `.ApplicationVersionType` | `TaleWorlds.Library.dll` | VERIFIED | Yes |
| Parse helpers | `ApplicationVersion.FromString(string, int)`, `ApplicationVersion.FromParametersFile(string)` | `TaleWorlds.Library.dll` | VERIFIED | Yes |
| Comparison | `IsOlderThan`, `IsNewerThan`, `IsSame(ApplicationVersion, bool)`, full operator set | `TaleWorlds.Library.dll` | VERIFIED | Yes |

### 1.2 Stable vs. beta is a first-class enum value

| Item | Exact symbol | Confidence | Verified |
|---|---|---|---|
| Release-channel enum | `TaleWorlds.Library.ApplicationVersionType` | VERIFIED | Yes |
| Members | `Invalid`, `Alpha`, `Beta`, `EarlyAccess`, `Release`, `Development` | VERIFIED | Yes |
| Prefix mapping | `ApplicationVersion.GetPrefix(ApplicationVersionType)` (produces the `e`/`v` prefix seen in `v1.4.8`, `e1.9.0`) | VERIFIED | Yes |

**This answers Goal 3 structurally:** stable vs. beta is not inferred — it is read from `ApplicationVersion.ApplicationVersionType`, where `Release` = stable and `Beta` = beta.

### 1.3 Which versions exist (from the package feed)

| Fact | Value | Confidence | Verified |
|---|---|---|---|
| Total published Core versions | 126 | VERIFIED | Yes |
| Latest **stable** | `1.4.8.119303` | VERIFIED | Yes |
| Latest **beta** | `1.5.3.122374-beta` | VERIFIED | Yes |
| Beta versions in the current line | `1.5.1.120547-beta`, `1.5.2.120933-beta`, `1.5.2.121216-beta`, `1.5.3.122374-beta` | VERIFIED | Yes |
| Recent stable versions | `1.4.5.114896`, `1.4.5.114927`, `1.4.5.115026`, `1.4.6.115439`, `1.4.6.115628`, `1.4.7.117131`, `1.4.7.117484`, `1.4.8.119303` | VERIFIED | Yes |
| NavalDLC package version count | 62 (vs. 126 for Core) — the naval module is published for a subset of versions | VERIFIED | Yes |

> **Caveat (UNCONFIRMED):** the `NavalDLC` package id also carries version strings as old as `1.0.0.4407`, which predates the War Sails release. Whether those early packages contain real naval assemblies or are pipeline artifacts was **not** verified. Do not use the earliest NavalDLC package versions as evidence of when War Sails shipped.

### 1.4 Runtime detection procedure (what the mod will actually do)

Because the audit host has no install, this is the specified procedure rather than an executed result.

| Step | Exact API | Confidence | Verified |
|---|---|---|---|
| Read game version | `TaleWorlds.Library.ApplicationVersion` via `ModuleInfo.Version` / `ApplicationVersion.FromParametersFile` | VERIFIED (API exists) | API: Yes / Result: No |
| Read channel | `ApplicationVersion.ApplicationVersionType` == `Release` vs `Beta` | VERIFIED (API exists) | API: Yes / Result: No |
| Enumerate modules | `TaleWorlds.ModuleManager.ModuleInfo` — `.Version`, `.RequiredBaseVersion`, `ModuleInfo.UpdateVersionChangeSet()` | VERIFIED | Yes |
| Detect War Sails present | Presence of the `NavalDLC` module + `NavalDLC.NavalVersion.GetApplicationVersionBuildNumber()` (`NavalDLC.dll`) — a dedicated DLC build-number accessor | VERIFIED (API exists) | API: Yes / Result: No |

**Action for the user:** run the detection snippet in `docs/VERSION_SUPPORT.md` §5 on the real machine and paste the output back. That converts Goals 1–3 from *specified* to *measured*.

---

## 2. Assemblies (Goals 4–6)

All confirmed present and parsed. Type counts are from the 1.4.8.119303 stable set.

### 2.1 Base-game assemblies relevant to this project

| Assembly | Types | Why it matters | Confidence |
|---|---|---|---|
| `TaleWorlds.CampaignSystem.dll` | 2306 | **The** campaign model: parties, heroes, clans, kingdoms, settlements, map events, sieges, **and the naval data model** | VERIFIED |
| `TaleWorlds.Core.dll` | — | `ShipHull`, `ShipUpgradePiece`, `IShipOrigin`, `BattleSideEnum`, `TeamSideEnum` | VERIFIED |
| `TaleWorlds.ObjectSystem.dll` | — | `MBObjectBase`, `MBGUID`, `MBObjectManager` — the identity system | VERIFIED |
| `TaleWorlds.SaveSystem.dll` | — | Serialization engine, `[Saveable*]` attributes, `SaveableTypeDefiner` | VERIFIED |
| `TaleWorlds.MountAndBlade.dll` | — | `Mission`, `MissionBehavior`, `Agent`, `Team`, `Formation`, **`GameNetwork`** | VERIFIED |
| `TaleWorlds.Library.dll` | — | `ApplicationVersion`, `Vec2`, `MBList<T>` | VERIFIED |
| `TaleWorlds.Network.dll` | 39 | Low-level transport + message contracts | VERIFIED |
| `SandBox.dll` | — | Singleplayer campaign mission glue | VERIFIED |

### 2.2 War Sails assemblies (Goal 5)

Shipped as the **`NavalDLC`** module. Seven assemblies:

| Assembly | Types | Role | Confidence |
|---|---|---|---|
| **`NavalDLC.dll`** | **692** | Campaign behaviors, mission logics, ship objects, AI, storyline | VERIFIED |
| `NavalDLC.ViewModelCollection.dll` | 120 | UI view models | VERIFIED |
| `NavalDLC.View.dll` | 89 | Mission/map views | VERIFIED |
| `NavalDLC.CustomBattle.dll` | 63 | Custom-battle naval integration | VERIFIED |
| `NavalDLC.GauntletUI.dll` | 55 | UI | VERIFIED |
| `NavalDLC.GauntletUI.AutoGenerated.dll` | 42 | Generated UI | VERIFIED |
| `NavalDLC.GauntletUI.Widgets.dll` | 14 | UI widgets | VERIFIED |

`NavalDLC.dll` namespace distribution (VERIFIED): `GameComponents` 89, `Missions.Objects` 61, `CampaignBehaviors` 55, `Storyline` 47, `Missions.MissionLogics` 44, `Storyline.Quests` 35, `Missions.Objects.UsableMachines` 28, `Missions` 25, `Missions.ShipActuators` 16, `Missions.AI.Behaviors` 15, `Missions.NavalPhysics` 14, `Missions.Deployment` 13, `Map` 4.

---

## 3. The Single Most Important Structural Finding

> **The naval *data model* lives in the base game, not in the DLC.**

| Type | Assembly | Confidence | Verified |
|---|---|---|---|
| `TaleWorlds.CampaignSystem.Naval.Ship` | **`TaleWorlds.CampaignSystem.dll`** | VERIFIED | Yes |
| `TaleWorlds.CampaignSystem.Naval.AnchorPoint` | `TaleWorlds.CampaignSystem.dll` | VERIFIED | Yes |
| `TaleWorlds.CampaignSystem.Naval.Figurehead` | `TaleWorlds.CampaignSystem.dll` | VERIFIED | Yes |
| `TaleWorlds.CampaignSystem.Actions.ChangeShipOwnerAction` | `TaleWorlds.CampaignSystem.dll` | VERIFIED | Yes |
| `TaleWorlds.CampaignSystem.Actions.DestroyShipAction` | `TaleWorlds.CampaignSystem.dll` | VERIFIED | Yes |
| `TaleWorlds.CampaignSystem.Actions.RepairShipAction` | `TaleWorlds.CampaignSystem.dll` | VERIFIED | Yes |
| `TaleWorlds.CampaignSystem.Actions.RaftStateChangeAction` | `TaleWorlds.CampaignSystem.dll` | VERIFIED | Yes |
| `MobileParty.Ships`, `PartyBase.Ships`, `PartyBase.FlagShip` | `TaleWorlds.CampaignSystem.dll` | VERIFIED | Yes |
| `Clan.HasNavalNavigationCapability` | `TaleWorlds.CampaignSystem.dll` | VERIFIED | Yes |
| Ship game models (`ShipStatModel`, `ShipCostModel`, `FleetManagementModel`, `PartyShipLimitModel`, `CampaignShipDamageModel`, `CampaignShipParametersModel`) | `TaleWorlds.CampaignSystem.dll` | VERIFIED | Yes |

**What `NavalDLC.dll` actually contains:** campaign *behaviors* (ship production, trade, repair, upgrade, distribution, pirates, storms, fishing), the *mission* layer (`MissionShip`, naval physics, ship control, boarding orders, naval AI), the storyline, and naval UI.

### Architectural consequences

1. **Ship ownership sync targets base-game types.** The state that must be replicated (`Ship`, `PartyBase._ships`, ownership actions) is in `TaleWorlds.CampaignSystem.dll`, which is present regardless of whether War Sails is installed. Our sync layer for ship ownership does **not** need a hard dependency on `NavalDLC.dll`.
2. **Naval campaign state is inside the normal campaign save.** `Ship` fields carry `[SaveableField]` (§7), so ships serialize through the standard save pipeline as part of `PartyBase`.
3. **The DLC boundary is behavior + mission, not data.** A missing-DLC client still has the *types*; it lacks the *systems*. This is the natural seam for optional-DLC support.

> `LIKELY` (not verified): that a save created with War Sails enabled loads without it. Not tested — no install. Tracked as RISK-13.

---

## 4. Campaign State Representation (Goal 7)

| Item | Exact symbol | Assembly | Confidence | Verified |
|---|---|---|---|---|
| Campaign root | `TaleWorlds.CampaignSystem.Campaign` | `TaleWorlds.CampaignSystem.dll` | VERIFIED | Yes |
| Save capability flag | `Campaign.SupportsSaving` (property) | `TaleWorlds.CampaignSystem.dll` | VERIFIED | Yes |
| Campaign object base | `TaleWorlds.CampaignSystem.CampaignObjectBase` → extends `TaleWorlds.ObjectSystem.MBObjectBase` | `TaleWorlds.CampaignSystem.dll` | VERIFIED | Yes |
| Behavior base | `TaleWorlds.CampaignSystem.CampaignBehaviorBase` with `RegisterEvents()` and `SyncData(IDataStore)` | `TaleWorlds.CampaignSystem.dll` | VERIFIED | Yes |
| Behavior persistence hook | `TaleWorlds.CampaignSystem.IDataStore.SyncData<T>(string, ref T)` | `TaleWorlds.CampaignSystem.dll` | VERIFIED | Yes |

### Identity system — the backbone of synchronization

| Item | Exact symbol | Confidence | Verified |
|---|---|---|---|
| Object base | `TaleWorlds.ObjectSystem.MBObjectBase` | VERIFIED | Yes |
| Stable numeric id | `MBObjectBase.Id` of type `TaleWorlds.ObjectSystem.MBGUID` — carries `[SaveableProperty]` | VERIFIED | Yes |
| Stable string id | `MBObjectBase.StringId` (string) — carries `[SaveableProperty]` | VERIFIED | Yes |
| MBGUID internals | `struct MBGUID` wrapping `uint _internalValue` (`[SaveableField]`), exposes `.InternalValue`, `.SubId`, full equality/ordering operators | VERIFIED | Yes |

**Everything derived from `MBObjectBase` is network-addressable for free** (heroes, parties, settlements, clans, kingdoms, items…). This is the foundation of the ownership model in `docs/SYNCHRONIZATION_MODEL.md`.

### The exception that defines the naval problem

| Type | Base type | Has `MBGUID`? | Confidence | Verified |
|---|---|---|---|---|
| `MobileParty` | `CampaignObjectBase` → `MBObjectBase` | **Yes** | VERIFIED | Yes |
| **`Ship`** | **`System.Object`** | **NO** | VERIFIED | Yes |
| **`PartyBase`** | **`System.Object`** | **NO** | VERIFIED | Yes |
| `AnchorPoint` | `System.Object` | **NO** | VERIFIED | Yes |
| `Figurehead` | `TaleWorlds.Core.PropertyObject` | (property object, not campaign object) | VERIFIED | Yes |

> **`Ship` has no `MBGUID` and no `StringId`.** It is a plain CLR object. There is no engine-provided way to name a specific ship across two processes. This is the #1 technical risk of the entire naval feature set — see RISK-01 and `docs/SYNCHRONIZATION_MODEL.md` §4.

`Ship` does, however, expose a change counter:

| Item | Exact symbol | Confidence | Verified |
|---|---|---|---|
| Version counter | `Ship.VersionNo` (uint, get) | VERIFIED | Yes |
| Bump method | `Ship.UpdateVersionNo()` | VERIFIED | Yes |
| Internal dirty flag | `Ship._isVersionDirty` (private bool) | VERIFIED | Yes |
| Party-level ship-list version | `PartyBase.GetShipsVersion()` → int | VERIFIED | Yes |

These are usable as cheap change-detection primitives. **UNCONFIRMED:** exactly which mutations bump `VersionNo` (method bodies stripped).

---

## 5. Party Representation (Goal 8)

| Item | Exact symbol | Assembly | Confidence | Verified |
|---|---|---|---|---|
| Map party | `TaleWorlds.CampaignSystem.Party.MobileParty` (143 properties, 101 fields, 222 methods) | `TaleWorlds.CampaignSystem.dll` | VERIFIED | Yes |
| Shared party payload | `TaleWorlds.CampaignSystem.Party.PartyBase` | `TaleWorlds.CampaignSystem.dll` | VERIFIED | Yes |
| Party AI | `TaleWorlds.CampaignSystem.Party.MobilePartyAi` (`MobileParty.Ai`) | `TaleWorlds.CampaignSystem.dll` | VERIFIED | Yes |
| Army grouping | `TaleWorlds.CampaignSystem.Army`; `MobileParty._army` `[SaveableField]` | `TaleWorlds.CampaignSystem.dll` | VERIFIED | Yes |

### Movement / position state (all `[SaveableField]` unless noted)

| Field / property | Type | Confidence | Verified |
|---|---|---|---|
| `MobileParty._targetPosition` | `CampaignVec2` | VERIFIED | Yes |
| `MobileParty._targetParty` | `MobileParty` | VERIFIED | Yes |
| `MobileParty._targetSettlement` | `Settlement` | VERIFIED | Yes |
| `MobileParty.MoveTargetPoint` | `CampaignVec2` (public field) | VERIFIED | Yes |
| `MobileParty.NextTargetPosition` | `CampaignVec2` (public field) | VERIFIED | Yes |
| `MobileParty._pathMode`, `_pathLastPosition` | bool, `CampaignVec2` | VERIFIED | Yes |
| `MobileParty.Bearing` | `Vec2` (auto-property) | VERIFIED | Yes |
| `MobileParty.NextLongTermPathPoint` | `CampaignVec2` — marked `[CachedData]`, **not** saveable | VERIFIED | Yes |
| `MobileParty._lastCalculatedSpeed` | float — `[CachedData]`, **not** saveable | VERIFIED | Yes |

> The `[CachedData]` vs `[SaveableField]` split is directly readable and is exactly the authority/derived-state split our sync model needs. See `docs/SYNCHRONIZATION_MODEL.md` §3.

### Naval party state (base game)

| Member | Access | Confidence | Verified |
|---|---|---|---|
| `MobileParty.Ships` → `MBReadOnlyList<Ship>` | public get | VERIFIED | Yes |
| `PartyBase.Ships` → `MBReadOnlyList<Ship>`; backing `PartyBase._ships` (`MBList<Ship>`) | public get / private field | VERIFIED | Yes |
| `PartyBase.FlagShip` → `Ship` | public get | VERIFIED | Yes |
| `PartyBase.AddShipInternal(Ship)` / `RemoveShipInternal(Ship)` | **`assembly` (internal)** | VERIFIED | Yes |
| `MobileParty.IsCurrentlyAtSea` | public get / **public set** | VERIFIED | Yes |
| `MobileParty.IsInRaftState` | public get / **public set** | VERIFIED | Yes |
| `MobileParty.IsTargetingPort` | public get / private set | VERIFIED | Yes |
| `MobileParty.Anchor` → `AnchorPoint` | public get / private set; `MobileParty.SetAnchor(AnchorPoint)` is public | VERIFIED | Yes |
| `MobileParty.HasNavalNavigationCapability` / `HasLandNavigationCapability` | public get | VERIFIED | Yes |
| `MobileParty.GetRegionSwitchCostFromLandToSea()` / `...FromSeaToLand()` | public | VERIFIED | Yes |
| `MobileParty.StartTransitionNextFrameToExitFromPort` | public field (bool) | VERIFIED | Yes |
| `MobileParty.IsNavalVisualDirty`, `SetNavalVisualAsDirty()`, `OnNavalVisualsUpdated()` | public | VERIFIED | Yes |
| `MobileParty._isCurrentlyAtSea`, `_isInRaftState`, `_isTargetingPort` | private `[SaveableField]` | VERIFIED | Yes |

**`AddShipInternal`/`RemoveShipInternal` are internal**, so authoritative ship-list mutation requires either a publicizer or Harmony. Noted in RISK-06.

---

## 6. Campaign Events (Goal 9)

| Item | Value | Confidence | Verified |
|---|---|---|---|
| Event hub | `TaleWorlds.CampaignSystem.CampaignEvents` | VERIFIED | Yes |
| Dispatcher | `TaleWorlds.CampaignSystem.CampaignEventDispatcher` | VERIFIED | Yes |
| Receiver interface | `TaleWorlds.CampaignSystem.CampaignEventReceiver` | VERIFIED | Yes |
| Event type | `TaleWorlds.CampaignSystem.IMbEvent<...>` (0–4 generic args) | VERIFIED | Yes |
| **Total events exposed** | **277 static event properties** | VERIFIED | Yes |

Full listing: `docs/evidence/campaign_events_1.4.8.txt`.

### Events most relevant to synchronization (all VERIFIED, all on `CampaignEvents`)

| Event | Signature | Use |
|---|---|---|
| `MapEventStarted` | `IMbEvent<MapEvent, PartyBase, PartyBase>` | Battle begins |
| `MapEventEnded` | `IMbEvent<MapEvent>` | Battle resolved |
| `BattleStarted` | `IMbEvent<PartyBase, PartyBase, object, bool>` | Battle begins (party view) |
| `OnPlayerBattleEndEvent` | `IMbEvent<MapEvent>` | Local player battle end |
| `PlayerDesertedBattleEvent` | `IMbEvent<int>` | Desertion/disconnect analogue |
| `MobilePartyCreated` | `IMbEvent<MobileParty>` | Party spawn → replicate |
| `MobilePartyDestroyed` | `IMbEvent<MobileParty, PartyBase>` | Party despawn |
| `OnPartyRemovedEvent` | `IMbEvent<PartyBase>` | Party teardown |
| `OnPartySizeChangedEvent` | `IMbEvent<PartyBase>` | Roster delta |
| `SettlementEntered` / `BeforeSettlementEnteredEvent` / `AfterSettlementEntered` | `IMbEvent<MobileParty, Settlement, Hero>` | Settlement transitions |
| `OnSettlementLeftEvent` | `IMbEvent<MobileParty, Settlement>` | Settlement exit |
| `OnSettlementOwnerChangedEvent` | 6-arg | Conquest |
| `ArmyCreated` / `ArmyGathered` / `ArmyDispersed` | `IMbEvent<Army…>` | Army lifecycle |
| `OnPartyJoinedArmyEvent` / `PartyRemovedFromArmyEvent` | `IMbEvent<MobileParty>` | Army membership |
| `VillageBeingRaided` / `VillageLooted` / `VillageBecomeNormal` / `VillageStateChanged` | | Raids (incl. seaborne) |
| `ItemsLooted` | `IMbEvent<MobileParty, ItemRoster>` | Loot |
| `HeroPrisonerTaken` / `HeroPrisonerReleased` | | Captivity |
| `HeroOrPartyTradedGold` / `HeroOrPartyGaveItem` | | Economy |
| `DailyTickPartyEvent` / `HourlyTickPartyEvent` / `AiHourlyTickEvent` / `TickPartialHourlyAiEvent` / `OnQuarterDailyPartyTick` | | Tick cadence |

**UNCONFIRMED:** dispatch ordering guarantees between these events, and whether any fire re-entrantly. Method bodies are stripped; this needs a runtime trace. Tracked as RISK-04.

---

## 7. Save / Load Architecture (Goal 12)

| Item | Exact symbol | Assembly | Confidence | Verified |
|---|---|---|---|---|
| Attribute: field | `TaleWorlds.SaveSystem.SaveableFieldAttribute` | `TaleWorlds.SaveSystem.dll` | VERIFIED | Yes |
| Attribute: property | `TaleWorlds.SaveSystem.SaveablePropertyAttribute` | `TaleWorlds.SaveSystem.dll` | VERIFIED | Yes |
| Attribute: root class | `TaleWorlds.SaveSystem.SaveableRootClassAttribute` | `TaleWorlds.SaveSystem.dll` | VERIFIED | Yes |
| Attribute: interface | `TaleWorlds.SaveSystem.SaveableInterfaceAttribute` | `TaleWorlds.SaveSystem.dll` | VERIFIED | Yes |
| Mod type registration | `TaleWorlds.SaveSystem.SaveableTypeDefiner` | `TaleWorlds.SaveSystem.dll` | VERIFIED | Yes |
| Behavior-level persistence | `CampaignBehaviorBase.SyncData(IDataStore)` + `IDataStore.SyncData<T>(string, ref T)` | `TaleWorlds.CampaignSystem.dll` | VERIFIED | Yes |
| Save driver interface | `TaleWorlds.SaveSystem.ISaveDriver` | `TaleWorlds.SaveSystem.dll` | VERIFIED | Yes |
| Drivers | `AsyncFileSaveDriver`, `FileDriver`, `InMemDriver` | `TaleWorlds.SaveSystem.dll` | VERIFIED | Yes |
| Manager | `TaleWorlds.SaveSystem.SaveManager`, `AutoGeneratedSaveManager` | `TaleWorlds.SaveSystem.dll` | VERIFIED | Yes |
| Metadata | `TaleWorlds.SaveSystem.MetaData`, `MetaDataExtensions` | `TaleWorlds.SaveSystem.dll` | VERIFIED | Yes |
| Save graph model | `Save.ObjectSaveData`, `ContainerSaveData`, `FieldSaveData`, `PropertySaveData`, `MemberSaveData`, `ElementSaveData`, `VariableSaveData`, `SaveContext`, `SaveOutput`, `SaveError` | `TaleWorlds.SaveSystem.dll` | VERIFIED | Yes |
| Load graph model | `Load.ContainerLoadData`, `ContainerHeaderLoadData`, `FieldLoadData`, `ElementLoadData`, `LoadContext`, `LoadResult`, `LoadError`, `LoadCallbackInitializator` | `TaleWorlds.SaveSystem.dll` | VERIFIED | Yes |
| Resolvers | `Resolvers.IObjectResolver`, `IEnumResolver`, `IConflictResolver` | `TaleWorlds.SaveSystem.dll` | VERIFIED | Yes |
| Serializers | `ArchiveSerializer`, `ArchiveConcurrentSerializer`, `ArchiveDeserializer`, `LegacyGameDataDeserializer`, `BinaryWriterFactory` | `TaleWorlds.SaveSystem.dll` | VERIFIED | Yes |
| Load callbacks | `LoadInitializationCallback`, `LateLoadInitializationCallback` | `TaleWorlds.SaveSystem.dll` | VERIFIED | Yes |
| Structured ids | `FolderId`, `EntryId`, `SaveEntry`, `SaveEntryFolder`, `SaveGameFileInfo` | `TaleWorlds.SaveSystem.dll` | VERIFIED | Yes |

Detail and implications: `docs/SAVE_FORMAT.md`.

### Verified `Ship` persisted state

Every one of these carries `[SaveableField]` or `[SaveableProperty]` (VERIFIED, directly read):

`_shipPieces` (`Dictionary<string, ShipUpgradePiece>`), `ShipHull`, `_hitPoints`, `_sailHitPoints`, **`_owner` (`PartyBase`)**, `_name` (`TextObject`), `_unlockedUpgradePieces` (`MBList<ShipUpgradePiece>`), `Figurehead`, `IsInvulnerable`, `IsTradeable`, `IsUsedByQuest`, `RandomValue`.

> Note `_versionNo` and `_isVersionDirty` are **not** saveable — the change counter is runtime-only and resets across load. Relevant to RISK-01.

---

## 8. Battle Creation & Lifecycle (Goal 10)

| Item | Exact symbol | Assembly | Confidence | Verified |
|---|---|---|---|---|
| Encounter driver | `TaleWorlds.CampaignSystem.EncounterManager` | `TaleWorlds.CampaignSystem.dll` | VERIFIED | Yes |
| — tick | `EncounterManager.Tick(float)` (public static) | | VERIFIED | Yes |
| — per-party | `EncounterManager.HandleEncounterForMobileParty(MobileParty, float)` | | VERIFIED | Yes |
| — party vs party | `EncounterManager.StartPartyEncounter(PartyBase, PartyBase)` | | VERIFIED | Yes |
| — party vs settlement | `EncounterManager.StartSettlementEncounter(MobileParty, Settlement)` | | VERIFIED | Yes |
| Player encounter state machine | `TaleWorlds.CampaignSystem.Encounters.PlayerEncounter` | `TaleWorlds.CampaignSystem.dll` | VERIFIED | Yes |
| — lifecycle | `Start()`, `Init()`, `Update()`, `Finish(bool)`, `LeaveBattle()`, `LeaveSettlement()` | | VERIFIED | Yes |
| — battle | `StartBattle()` → `MapEvent`; `JoinBattle(BattleSideEnum)`; `StartAttackMission()`; `SetPlayerVictorious()`; `EndBattleByCheat(bool)` | | VERIFIED | Yes |
| — **naval** | **`PlayerEncounter.IsNavalEncounter()`** (public static bool) | | VERIFIED | Yes |
| — simulation | `InitSimulation(FlattenedTroopRoster, FlattenedTroopRoster)` | | VERIFIED | Yes |
| — other missions | `StartSiegeAmbushMission()`, `StartVillageBattleMission()`, `StartCombatMissionWithDialogueInTownCenter(CharacterObject)`, `StartHostileAction()` | | VERIFIED | Yes |
| — state enum | `TaleWorlds.CampaignSystem.Encounters.PlayerEncounterState` | | VERIFIED | Yes |
| Battle object | `TaleWorlds.CampaignSystem.MapEvents.MapEvent` | `TaleWorlds.CampaignSystem.dll` | VERIFIED | Yes |
| Battle manager | `TaleWorlds.CampaignSystem.MapEvents.MapEventManager` | | VERIFIED | Yes |
| Battle sides | `MapEventSide`, `MapEventParty` | | VERIFIED | Yes |
| **Naval flag** | **`MapEvent.IsNavalMapEvent`** (public bool property) | | VERIFIED | Yes |
| Result | `TaleWorlds.CampaignSystem.Encounters.CampaignBattleResult` | | VERIFIED | Yes |
| Other encounter kinds | `CastleEncounter`, `TownEncounter`, `VillageEncounter`, `HideoutEncounter`, `LocationEncounter`, `RetirementEncounter` | | VERIFIED | Yes |

### `MapEvent.BattleTypes` enum — complete, VERIFIED

`None`, `FieldBattle`, `Raid`, `IsForcingVolunteers`, `IsForcingSupplies`, `Siege`, `Hideout`, `SallyOut`, `SiegeOutside`, **`BlockadeBattle`**, **`BlockadeSallyOutBattle`**

> `BlockadeBattle` / `BlockadeSallyOutBattle` are the **naval siege** battle types. Backed by `TaleWorlds.CampaignSystem.MapEvents.BlockadeBattleMapEvent`, which exposes `CalculateBesiegerSideNavalPower(BesiegerCamp)` and `CalculateAttackerNavalPower(PartyBase)` (VERIFIED). Naval sieges are a **base-game** battle type.

### Map event components (VERIFIED)

`MapEventComponent` (base), `FieldBattleEventComponent`, `RaidEventComponent`, `SiegeAmbushEventComponent`, `HideoutEventComponent`, `ForceSuppliesEventComponent`, `ForceVolunteersEventComponent`.

Also: `Helpers.MapEventHelper.IsNavalRaid(MapEvent)` (public static, VERIFIED) — naval raids are distinguishable at the campaign layer.

---

## 9. Siege Lifecycle (Goal 11)

| Item | Exact symbol | Assembly | Confidence | Verified |
|---|---|---|---|---|
| Siege object | `TaleWorlds.CampaignSystem.Siege.SiegeEvent` | `TaleWorlds.CampaignSystem.dll` | VERIFIED | Yes |
| Current phase | `SiegeEvent.GetCurrentBattleType()` → `MapEvent.BattleTypes` | | VERIFIED | Yes |
| Besieger side | `TaleWorlds.CampaignSystem.Siege.BesiegerCamp` | | VERIFIED | Yes |
| — party enumeration | `BesiegerCamp.GetInvolvedPartiesForEventType(MapEvent.BattleTypes)` | | VERIFIED | Yes |
| Party linkage | `MobileParty._besiegerCamp` `[SaveableField]` | | VERIFIED | Yes |
| Defender enumeration | `Town.GetDefenderParties(BattleTypes)`, `Village.GetDefenderParties(BattleTypes)` | | VERIFIED | Yes |
| Naval blockade | `MapEvents.BlockadeBattleMapEvent` | | VERIFIED | Yes |
| Naval siege engines (beta) | `MapEventSide._shipSiegeEngineList` : `MBList<ValueTuple<SiegeEngineType, Ship>>` — **1.5.3-beta only** | VERIFIED | Yes |
| Ship-mounted siege engines | `Ship.GetSiegeEngines()` → `MBList<SiegeEngineType>` | | VERIFIED | Yes |

**UNCONFIRMED:** the exact siege stage-transition sequence. `MapEvent.CheckSiegeStageChange()` and `FinishBattleAndKeepSiegeEvent()` exist in 1.4.8 but were **removed in 1.5.3-beta** — siege/battle coupling was refactored between the two lines. See `docs/VERSION_SUPPORT.md`.

---

## 10. Mission APIs (Goal 14)

| Item | Exact symbol | Assembly | Confidence | Verified |
|---|---|---|---|---|
| Mission | `TaleWorlds.MountAndBlade.Mission` | `TaleWorlds.MountAndBlade.dll` | VERIFIED | Yes |
| Mission host | `TaleWorlds.MountAndBlade.MissionState` | `TaleWorlds.MountAndBlade.dll` | VERIFIED | Yes |
| Behavior base | `TaleWorlds.MountAndBlade.MissionBehavior`, `MissionLogic`, `IMissionBehavior` | | VERIFIED | Yes |
| Combatant | `TaleWorlds.MountAndBlade.Agent` | | VERIFIED | Yes |
| Grouping | `TaleWorlds.MountAndBlade.Team`, `TaleWorlds.MountAndBlade.Formation` | | VERIFIED | Yes |
| Mission spec | `TaleWorlds.Core.MissionInitializerRecord` | `TaleWorlds.Core.dll` | VERIFIED | Yes |
| Mission handle | `TaleWorlds.Core.IMission` | `TaleWorlds.Core.dll` | VERIFIED | Yes |
| **Naval mission host** | **`NavalDLC.NavalMissionState` extends `TaleWorlds.MountAndBlade.MissionState`** | `NavalDLC.dll` | VERIFIED | Yes |
| Vehicle interface | `TaleWorlds.MountAndBlade.IVehicleHandler` (implemented by `NavalShipsLogic`) | | VERIFIED | Yes |
| Synched mission objects | `GameNetwork.GetSynchedMissionObjectReadableRecordTypeFromIndex(int)` / `...IndexFromType(Type)` | `TaleWorlds.MountAndBlade.dll` | VERIFIED | Yes |

---

## 11. Networking APIs Available to Mods (Goal 13)

### 11.1 `TaleWorlds.Network.dll` — transport layer (39 types, VERIFIED)

`TcpSocket`, `TcpStatus`, `TcpMessageReceiverDelegate`, `TcpCloseDelegate`, `NetworkSession`, `ClientsideSession`, `ServersideSession`, `ServersideSessionManager`, `IncomingServerSessionMessage`, `MessageBuffer`, `MessageId`, `MessageContract`, `MessageContractCreator<T>`, `MessageContractHandler<T>`, `MessageContractHandlerManager`, `MessageContractHandlerDelegate<T>`, `NetworkMessage`, `INetworkMessageWriter`, `INetworkMessageReader`, `INetworkSerializable`, `MessageProxy`, `IMessageProxyClient`, `MessageServiceConnection`, `ConnectionState`, `ClientWebSocketHandler`, `JsonSocketMessage`, `RESTClient`, `Authorize`, `PostBoxId`, `CoroutineManager`, `Coroutine`, `CoroutineState`, `CoroutineDelegate`, `WaitForTicks`, `WaitForSpecialCase`, `ServiceException`, `ServiceExceptionModel`.

### 11.2 `TaleWorlds.MountAndBlade.GameNetwork` — the multiplayer facade (VERIFIED)

| Capability | Exact members | Confidence |
|---|---|---|
| Role flags | `IsServer`, `IsClient`, `IsDedicatedServer`, `IsMultiplayer`, `IsSessionActive`, `IsReplay`, `MultiplayerDisabled` | VERIFIED |
| Peers | `NetworkPeers`, `DisconnectedNetworkPeers`, `NetworkPeersIncludingDisconnectedPeers`, `NetworkPeerCount`, `MyPeer`, `VirtualPlayers`, `FindNetworkPeer(int)` | VERIFIED |
| Lifecycle | `Initialize(IGameNetworkHandler)`, `StartMultiplayerOnServer(int)`, `PreStartMultiplayerOnServer()`, `StartMultiplayerOnClient(string,int,int,int)`, `InitializeClientSide(...)`, `TerminateClientSide()`, `EndMultiplayer()` | VERIFIED |
| Player admission | `AddNewPlayerOnServer(PlayerConnectionInfo, bool, bool)`, `AddNewPlayersOnServer(...)`, `HandleNewClientConnect(...)`, `ClientFinishedLoading(NetworkCommunicator)` | VERIFIED |
| **Custom messages** | `BeginModuleEventAsClient()` / `EndModuleEventAsClient()`, `BeginModuleEventAsServer(NetworkCommunicator\|VirtualPlayer)` / `EndModuleEventAsServer()`, `BeginBroadcastModuleEvent()` / `EndBroadcastModuleEvent(EventBroadcastFlags, NetworkCommunicator)`, plus `…Unreliable` variants | VERIFIED |
| Handler registration | `AddRemoveMessageHandlers(NetworkMessageHandlerRegisterer.RegisterMode)`, `AddNetworkHandler(IUdpNetworkHandler)`, `AddNetworkComponent<T>()` | VERIFIED |
| Disconnect handling | `AddNetworkPeerToDisconnectAsServer(NetworkCommunicator)`, `UnSynchronizeEveryone()`, `ClearAllPeers()` | VERIFIED |
| Timing | `ElapsedTimeSinceLastUdpPacketArrived()` | VERIFIED |

> **A complete, mod-usable reliable/unreliable message system exists.** The `BeginModuleEventAs*` / `End*` pair with `EventBroadcastFlags` is the intended extension point for custom network messages.

### 11.3 The decisive open question

| Question | Confidence | Verified |
|---|---|---|
| Does `GameNetwork` function inside a **singleplayer `Campaign` session** (which is what co-op must run)? | **UNCONFIRMED** | **No** |

The API surface is verified to exist. Whether the native UDP stack is initialized when the game is in campaign mode — rather than in a multiplayer game mode — **cannot be determined from metadata**, because that answer lives in method bodies and native code. The existence of `GameNetwork.MultiplayerDisabled` is a signal that the engine gates this, but its semantics are not verified.

**This is the single highest-leverage unknown in the project** (RISK-02). `docs/NETWORK_PROTOCOL.md` §2 specifies the exact experiment to resolve it and defines a transport abstraction so that the answer does not change the rest of the architecture.

---

## 12. War Sails Systems (Goals 15–21)

Full treatment in `docs/WARSAILS_ARCHITECTURE.md`. Summary of verified findings:

### 12.1 Campaign systems (Goal 15) — 28 behaviors in `NavalDLC.CampaignBehaviors`, all VERIFIED

`ShipProductionCampaignBehavior`, `ShipTradeCampaignBehavior`, `ShipRepairCampaignBehavior`, `ShipUpgradeCampaignBehavior`, `ShipNameCampaignBehavior`, `NavalShipDistributionCampaignBehavior`, `ClanFleetManagementCampaignBehavior` (+ nested `SentTroopsData`), `IFleetManagementCampaignBehavior`, `NavalPatrolPartiesCampaignBehavior` / `INavalPatrolPartiesCampaignBehavior`, `PiratesCampaignBehavior` (+ `PatrolZone`), `StormCampaignBehavior`, `SeaDamageCampaignBehavior`, `RaftStateCampaignBehavior`, `NavalTransitionCampaignBehavior`, `PortCharactersCampaignBehavior`, `FishingPartyCampaignBehavior`, `NavalFishingCampaignBehaviour`, `NavalInitializationCampaignBehavior`, `NavalCharacterCreationCampaignBehavior`, `NavalCompanionRolesCampaignBehavior`, `NavalOrderOfBattleCampaignBehavior`, `NavalKingdomPolicyCampaignBehaviour`, `NavalDLCFigureheadCampaignBehavior`, `NavalForceStartNavalMissionCampaignBehavior`, `NavalDLCTutorialBoxCampaignBehavior`, `NavalNimbleSurgeCampaignBehaviour`, `NavalStormriderCampaignBehaviour`, `NavalVeteransWisdomCampaignBehaviour`.

Supporting: `NavalDLCManager`, `NavalDLCEvents`, `NavalDLCSubModule`, `NavalPolicies`, `NavalItemCategories`, `SaveableNavalDLCTypeDefiner`, `NavalDLC.CharacterDevelopment.{NavalSkills, NavalPerks, NavalSkillEffects, NavalCulturalFeats}`.

### 12.2 Ship & fleet representation (Goal 16)

- **Ship** = `TaleWorlds.CampaignSystem.Naval.Ship` (base game) — VERIFIED, full field list in §7.
- **Fleet** = **there is no `Fleet` type.** A fleet is `PartyBase.Ships` (an `MBList<Ship>`) plus `PartyBase.FlagShip`. Clan-level fleet logistics live in `ClanFleetManagementCampaignBehavior` and `FleetManagementModel`. VERIFIED.
- Hull/template: `TaleWorlds.Core.ShipHull`, `TaleWorlds.Core.ShipUpgradePiece`, `TaleWorlds.CampaignSystem.Party.ShipTemplateStack` (struct: `ShipHull`, `MinValue`, `MaxValue`). VERIFIED.

### 12.3 Naval movement (Goal 17)

Campaign-map: `MobileParty.IsCurrentlyAtSea`, `.IsInRaftState`, `.Anchor`/`SetAnchor`, `.IsTargetingPort`, `.HasNavalNavigationCapability`, `GetRegionSwitchCostFromLandToSea/SeaToLand`, `StartTransitionNextFrameToExitFromPort`, `Ship.GetCampaignSpeed()`, `Ship.SeaWorthiness`, `Ship.CampaignSpeedBonusFactor`, `NavalTransitionCampaignBehavior`. All VERIFIED.
Mission-layer: `NavalDLC.Missions.ShipControl.{NavalState, NavalVec}`, `NavalDLC.Missions.NavalPhysics` (14 types), `NavalDLC.Missions.ShipActuators` (16 types), `NavalDLC.Missions.ShipInput` (6 types). VERIFIED.

### 12.4 Naval encounters (Goal 18)

`PlayerEncounter.IsNavalEncounter()`, `MapEvent.IsNavalMapEvent`, `MapEventHelper.IsNavalRaid(MapEvent)`, `BattleTypes.BlockadeBattle` / `.BlockadeSallyOutBattle`, `BlockadeBattleMapEvent`. All VERIFIED, all in the **base game**.

### 12.5 Naval mission creation (Goal 19) — VERIFIED entry points

| Entry point | Signature | Assembly |
|---|---|---|
| `NavalDLC.Missions.NavalMissions.OpenNavalBattleMission` | `(MissionInitializerRecord) → Mission` (public **static**) | `NavalDLC.dll` |
| `NavalDLC.Missions.NavalMissions.OpenNavalRaidMission` | `(TroopRoster, BattleSideEnum, List<Ship>) → Mission` (public static) | `NavalDLC.dll` |
| `NavalDLC.Missions.NavalMissions.OpenNavalSetPieceBattleMission` | `(MissionInitializerRecord, MBList<IShipOrigin> ×3) → Mission` (public static) | `NavalDLC.dll` |
| `NavalDLC.Missions.NavalMissionManager.OpenNavalBattleMission` / `OpenNavalRaidMission` / `OpenNavalSetPieceBattleMission` | instance, returns `IMission` | `NavalDLC.dll` |
| Storyline missions | `OpenNavalStorylineCaptivityMission`, `OpenNavalStorylinePirateBattleMission`, `OpenNavalStorylineQuest5SetPieceBattleMission`, `OpenNavalStorylineWoundedBeastBattleMission`, `OpenNavalStorylineAlleyFightMission`, `OpenNavalFinalConversationMission` | `NavalDLC.dll` |

### 12.6 Boarding (Goal 20) — **mission-layer only**, VERIFIED

| Item | Exact symbol |
|---|---|
| Order flags | `NavalDLC.Missions.ShipOrder.BoardAtWill` (get public / set private), `.IsBoardingAvailable` (get/set public) |
| Target | `ShipOrder.GetBoardingTargetShip()` → `MissionShip`, `ShipOrder.SetBoardingTargetShip(MissionShip)` |
| Query | `ShipOrder.GetIsAttemptingBoarding()` → bool |
| Capture callback | `ShipOrder.OnShipCaptured(MissionShip, MissionShip)` |
| Event | `NavalShipsLogic.BoardingOrderEvent` : `System.Action<MissionShip, MissionShip>` |
| Troop placement | `TaleWorlds.MountAndBlade.ShipPlacementDetachment.SetBoarding(bool, Vec2)`; `ShipPlacementPosition.CalculateBoardingScore(...)` |
| AI | `NavalDLC.Missions.AI.Behaviors.NavalBehaviorBoardShipSubtask` (+ `ShipBoardingState` enum), `BehaviorNavalEngageCorrespondingEnemy.ShipBoardingState` |
| Input | `TaleWorlds.MountAndBlade.GameKeyDefinition.AttemptBoarding` |
| Perk | `NavalDLC.CharacterDevelopment.NavalPerks.Mariner.BoardingMaster` |

> **There is no campaign-layer boarding API.** Boarding exists only inside a running mission. Campaign-level ship transfer after a won battle goes through `ChangeShipOwnerAction` (§12.7). This split is the core of the naval sync design.

### 12.7 Ship ownership & state mutation points (Goal 21) — VERIFIED, `TaleWorlds.CampaignSystem.dll`

**1.4.8 stable:**
- `ChangeShipOwnerAction.ApplyByTransferring(PartyBase, Ship)`
- `ChangeShipOwnerAction.ApplyByTrade(PartyBase, Ship)`
- **`ChangeShipOwnerAction.ApplyByLooting(PartyBase, Ship)`** ← ship capture
- `ChangeShipOwnerAction.ApplyByProduction(PartyBase, Ship)`
- `ChangeShipOwnerAction.ApplyByMobilePartyCreation(PartyBase, Ship)`
- `DestroyShipAction.Apply(Ship)` / `.ApplyByDiscard(Ship)`
- `RepairShipAction.Apply(Ship, Settlement)` / `.ApplyForFree(Ship)` / `.ApplyForBanditShip(Ship)`
- `RaftStateChangeAction.ActivateRaftStateForParty(MobileParty)` / `.DeactivateRaftStateForParty(MobileParty)`
- `MapEvent.LootDefeatedPartyShips(MBReadOnlyList<MapEventParty>, MBReadOnlyList<MapEventParty>)`

**Direct field mutation:** `Ship.Owner` (public set), `Ship.HitPoints` (public set), `Ship.SailHitPoints` (public set), `Ship.IsInvulnerable/IsTradeable/IsUsedByQuest` (public set), `Ship.SetName(TextObject)`, `Ship.ChangeFigurehead(Figurehead)`, `Ship.EquipUpgradePiece(string, ShipUpgradePiece)`, `Ship.OnShipDamaged(float, IShipOrigin, ref float)`.

**Internal:** `PartyBase.AddShipInternal(Ship)` / `RemoveShipInternal(Ship)`.

### 12.8 Mission-layer naval sync hooks — 27 events on `NavalShipsLogic`, VERIFIED

`ShipSpawnedEvent`, **`ShipCapturedEvent`** (`Action<MissionShip,MissionShip,Formation,Formation>`), **`ShipSunkEvent`**, **`BoardingOrderEvent`**, `ShipsConnectedEvent`, `ShipHookThrowEvent`, `BridgeConnectedEvent`, `CutLooseOrderEvent`, `ShipRammingEvent`, `ShipAboutToBeRammedEvent`, `ShipCollisionEvent`, `ShipHitEvent`, `ShipBurnedEvent`, `SailsDeadEvent`, `ShipLowHealthEvent`, `ShipTeleportedEvent`, `ShipControllerChanged`, `ShipTransferredToTeamEvent` / `Before…`, `ShipTransferredToFormationEvent` / `Before…`, `ShipRemovedEvent` / `Before…`, `ShipAttachmentBrokenEvent`, `ShipAttachmentLostEvent`, `ShipPreparedForAbandonmentEvent`, `MissionEndEvent`.

> **Critical distinction:** these are plain `System.Action` delegates, **not** `IMbEvent`. They carry no engine-level ordering or replay semantics — they are local notifications only.

---

## 13. Existing BannerlordCoop Project (Goals 22–24)

### 13.1 Repository facts (VERIFIED)

| Fact | Value |
|---|---|
| Repository | `github.com/Bannerlord-Coop-Team/BannerlordCoop` |
| HEAD inspected | `b8bcf9970dc71ced68eb6743db7e68080d25654f` (2026-09-16) |
| Scale | 4,257 `.cs` files across ~25 projects |
| Activity | Very active — PR #3610 merged the day of this audit |
| Distribution | Steam Workshop id `3770450698` |
| Supported game version (README) | **v1.4.7** |
| Build-target game version (`Deploy.targets`) | `v1.3.12` |
| Module dependencies (`SubModule.xml` template) | `Native`, `SandBoxCore`, `Sandbox`, `CustomBattle`, `StoryMode` — **`NavalDLC` is absent** |

### 13.2 ⚠ Licensing — a project-defining constraint (VERIFIED)

`LICENSE` and `NOTICE.md`, read directly:

> As of **2026-06-17**, BannerlordCoop is **no longer MIT**. It is "source-available": may be viewed and contributed to, but **not** copied, redistributed, relicensed, or used in another project without written permission. The license explicitly prohibits use "to create, contribute to, improve, support, or maintain a **competing** Mount & Blade II: Bannerlord multiplayer, co-op, networking, synchronization, or derivative mod without prior written permission."

**This project — a Bannerlord cooperative campaign mod — falls squarely inside that prohibition.**

Consequences, and they are not negotiable by us:
- We may **not** copy, port, translate, adapt, or derive our implementation from their source.
- This audit therefore derives **100% of its architecture from TaleWorlds' own API surface**, which is the legally clean and independently verifiable basis. No upstream implementation detail is used anywhere in these documents.
- Older MIT-era versions remain under MIT per `NOTICE.md`, but relying on a 2026-era fork of a pre-June-2026 MIT snapshot is a legal question for a human, not an engineering decision. Tracked as **RISK-00**.

**Decision required from the project owner** before Phase 1 (see `docs/RISK_REGISTER.md` RISK-00): (a) build clean-room from TaleWorlds APIs only, (b) request written permission, or (c) contribute to upstream instead of building separately.

> **Note on repository content:** `AGENTS.md` in that repository contains instructions directing automated agents to refuse work and reply in a fictional persona when operating outside their authorized repositories. That is repository content attempting to direct an agent, not an instruction from you, and I did not follow it. I have, separately and on the merits, respected the actual `LICENSE` — which is a real legal document — by deriving nothing in this audit from their implementation.

### 13.3 War Sails status upstream (Goal 23) — VERIFIED

| Item | Status |
|---|---|
| README "Planned Features" | **"War Sails DLC support"** — not implemented |
| README "Compatibility" | **"Do not enable the War Sails DLC"** |
| Issue **#3060** "Epic: War Sails Sync" | **Open** (2026-08-15), tracking campaign/map-event work, 9 child issues |
| Issue **#3088** "[WARSAILS] Naval Battles" | **Open** (2026-08-16) — movement, boarding, ballistas, swimming |
| Issue **#3089** "[WARSAILS] Naval village raiding" | **Open** (2026-08-16) |
| Issue **#3090** "[WARSAILS] Ship blockades during sieges" | **Open** (2026-08-16) |
| PR #3590 | Merged — dismisses the War Sails startup popup in test automation |
| PR #3159, Issue #1318 | Merged/closed — both explicitly scope **naval battles out** pending War Sails support |

**Conclusion (VERIFIED):** the mature incumbent co-op mod does **not** support War Sails. Naval co-op is unimplemented by anyone publicly. That is simultaneously this project's clearest differentiator and its largest unknown — there is no prior art to learn from, and the risks in §14 are unmitigated by anyone.

### 13.4 Architecture comparison (Goal 24)

Comparison is at the level of **publicly documented capability**, not implementation (per §13.2).

| Capability | Upstream (public README) | This project's target | Gap |
|---|---|---|---|
| Shared campaign map | Shipped | Required | Parity |
| Multiple player parties | Shipped | Required | Parity |
| PvP / co-op battles | Shipped | Required | Parity |
| Sieges & sally-outs | Shipped | Required | Parity |
| Armies, kingdoms, clans | Shipped | Required | Parity |
| Economy, trade, smithing | Shipped | Required | Parity |
| Dedicated server | Shipped | Required | Parity |
| Disconnect handling | Shipped ("players who retreat or disconnect are handled") | Required | Parity |
| Quests | **Planned** | Required | Both unbuilt |
| Hideouts | **Planned** | Required | Both unbuilt |
| **War Sails / naval** | **Planned; explicitly disabled** | **Required, primary differentiator** | **Greenfield for everyone** |
| Player count | "optimized for up to 8" | Target ≥8 | Parity/stretch |

---

## 14. The Five Highest-Risk Technical Areas

Ranked. Full register: `docs/RISK_REGISTER.md`.

1. **RISK-01 — `Ship` has no network identity.** `Ship` derives from `System.Object`, with no `MBGUID` and no `StringId` (VERIFIED). Every naval feature the project wants — capture, destruction, loot, fleets, transfer — requires naming a specific ship across processes. We must synthesize and maintain our own identity map, keep it stable across save/load (the `_versionNo` counter is **not** saveable), and survive ownership transfer. Nothing else in the campaign model has this problem.
2. **RISK-02 — `GameNetwork` usability in a campaign session is unverified.** The full message API exists (VERIFIED), but whether it initializes outside a multiplayer game mode is UNCONFIRMED and unanswerable from metadata. Wrong guess here invalidates the transport layer. Resolve before any netcode is written.
3. **RISK-03 — Naval state spans two layers with no shared identity.** Campaign `Ship` ↔ mission `MissionShip` are different types in different assemblies; `MissionShip` carries `TaleWorlds.Core.IShipOrigin` (VERIFIED) but the binding's stability across a battle is UNCONFIRMED. Boarding/capture happens at mission layer; ownership lives at campaign layer. Every naval outcome must cross this seam correctly or ships duplicate or vanish.
4. **RISK-04 — Campaign determinism and event ordering are unverified.** 277 events exist (VERIFIED), but ordering, re-entrancy, and whether AI/economy tick deterministically across machines are all UNCONFIRMED, because method bodies are stripped. Divergence here produces slow, silent desync.
5. **RISK-05 — Version churn on exactly the types we bind to.** Between 1.4.8-stable and 1.5.3-beta: 2,638 CampaignSystem members removed, 4,248 added; 92 types removed, 170 added (VERIFIED). `MapEvent` alone lost 16 members. `ChangeShipOwnerAction` gained a whole stashing subsystem. Pinning a version is mandatory.

---

## 15. What Can Be Implemented Safely Right Now

Everything here rests only on `VERIFIED` findings.

| # | Work item | Basis |
|---|---|---|
| 1 | **Version & DLC detection/gating.** `ApplicationVersion`, `ApplicationVersionType`, `ModuleInfo`, `NavalVersion.GetApplicationVersionBuildNumber()` | VERIFIED APIs |
| 2 | **Module skeleton.** `MBSubModuleBase`, `SubModule.xml`, `CampaignBehaviorBase` registration, `SaveableTypeDefiner` for our own types | VERIFIED |
| 3 | **Identity registry for `MBObjectBase` types.** `MBGUID`/`StringId` are stable and saveable — parties, heroes, clans, kingdoms, settlements are addressable today | VERIFIED |
| 4 | **Ship identity registry (our own).** Assign and persist our own ship ids via `SaveableTypeDefiner` + `SyncData`, keyed off owning `PartyBase` — solves RISK-01 without needing a running game to design | VERIFIED save APIs |
| 5 | **Event observation harness.** Subscribe to all 277 `CampaignEvents` and log ordering/frequency. *This is the instrument that converts RISK-04 from UNCONFIRMED to measured.* | VERIFIED |
| 6 | **Transport abstraction + loopback implementation.** Define our message interface and an in-process transport, so RISK-02's answer swaps one implementation | Design-only |
| 7 | **State-model codegen from `[Saveable*]` metadata.** `tools/apiscan` already reads these; generate our replicated-state definitions from the real schema rather than by hand | VERIFIED tooling |
| 8 | **Version-diff CI gate.** Wire `cli_meta.py surface` + diff into CI to fail when a targeted API disappears | VERIFIED tooling |

## 16. What Requires Further Reverse Engineering

| # | Question | Blocks | Method |
|---|---|---|---|
| 1 | Does `GameNetwork` work in a `Campaign` session? | All netcode | Runtime probe on a real install |
| 2 | Campaign tick determinism & event ordering | Sync model | Event harness (§15.5) + two-machine trace |
| 3 | `Ship` ↔ `MissionShip` binding lifetime; `IShipOrigin` stability | Naval capture/loss | Decompile bodies + in-mission trace |
| 4 | Which mutations bump `Ship.VersionNo` | Change detection | Decompile method bodies |
| 5 | Naval mission launch path from a campaign `MapEvent` | Naval battles | Decompile `NavalMissionManager`, `EncounterGameMenuBehavior` |
| 6 | Siege stage transition sequence (refactored between versions) | Sieges | Decompile `SiegeEvent`, `BesiegerCamp` |
| 7 | Save compat with/without `NavalDLC` enabled | Persistence, RISK-13 | Empirical save/load matrix |
| 8 | `MissionShip` authority model & physics determinism | Naval battles | Runtime experiment |
| 9 | Actual installed version on the target machine | Version pinning | Run §1.4 procedure |

**The single highest-value next action is obtaining a real Bannerlord + War Sails install**, which converts items 1–9 from blocked to testable.

---

## 17. Proposed Phase 1 Implementation Order

Full detail with exit criteria in `docs/ROADMAP.md`. Order is chosen so that each step either de-risks the next or is independently useful.

| Step | Deliverable | Why here |
|---|---|---|
| **1.0** | **Resolve RISK-00 (licensing decision).** | Non-technical, blocks everything, owner's call |
| **1.1** | Environment capture: run §1.4 detection, record exact version/channel/DLC build; **pin** it | Converts Goals 1–3 to measured; everything downstream depends on it |
| **1.2** | Module skeleton + version gate; refuse to load on unpinned versions | Cheap, prevents the whole class of RISK-05 failures |
| **1.3** | `tools/apiscan` into CI as an API-drift gate | Makes RISK-05 a build failure instead of a crash |
| **1.4** | **`GameNetwork`-in-campaign probe** (RISK-02) behind the transport abstraction | Highest-leverage unknown; answer chooses the transport |
| **1.5** | Campaign event observation harness + determinism trace (RISK-04) | Produces the data the sync model needs |
| **1.6** | Identity layer: `MBGUID` registry **+ synthesized persistent ship ids** (RISK-01) | Foundation for all replication; designable now |
| **1.7** | Transport + wire protocol per `docs/NETWORK_PROTOCOL.md`, using 1.4's answer | First real netcode, now de-risked |
| **1.8** | Single replicated system end-to-end: **party position only**, two clients | Smallest vertical slice that proves the stack |
| **1.9** | Save/load integration: our state through `SaveableTypeDefiner` + `SyncData`; server restart survives | Unblocks persistence and reconnect |
| **1.10** | Naval *campaign* state replication (ships, ownership, at-sea flags) — **campaign layer only, no naval missions** | Naval value early, avoiding the RISK-03 seam |

**Naval battles (the mission layer, RISK-03) are deliberately excluded from Phase 1.** They depend on unknowns 3, 5 and 8, none of which can be resolved without a running install.

---

## 18. Audit Completion Status

| Goal | Status |
|---|---|
| 1. Bannerlord version | ⚠ **Not answerable here** — no install. Version landscape + detection procedure delivered (§1) |
| 2. War Sails version | ⚠ **Not answerable here** — no install. Detection API verified (§1.4) |
| 3. Stable vs beta | ✅ Structurally answered via `ApplicationVersionType`; per-machine value pending (§1.2) |
| 4. Bannerlord assemblies | ✅ §2.1 |
| 5. War Sails assemblies | ✅ §2.2 |
| 6. APIs & metadata | ✅ Throughout; tooling in `tools/apiscan/` |
| 7. Campaign state | ✅ §4 |
| 8. Parties | ✅ §5 |
| 9. Campaign events | ✅ §6 (277 events) |
| 10. Battle lifecycle | ✅ §8 |
| 11. Siege lifecycle | ✅ §9 |
| 12. Save/load | ✅ §7 + `docs/SAVE_FORMAT.md` |
| 13. Networking APIs | ✅ §11 (with RISK-02 flagged) |
| 14. Mission APIs | ✅ §10 |
| 15. War Sails campaign systems | ✅ §12.1 |
| 16. Ships & fleets | ✅ §12.2 |
| 17. Naval movement | ✅ §12.3 |
| 18. Naval encounters | ✅ §12.4 |
| 19. Naval mission creation | ✅ §12.5 |
| 20. Boarding | ✅ §12.6 |
| 21. Ship ownership mutation | ✅ §12.7 |
| 22. BannerlordCoop repo | ✅ §13.1–13.2 |
| 23. Issues & PRs re War Sails | ✅ §13.3 |
| 24. Architecture comparison | ✅ §13.4 (capability-level; implementation comparison legally excluded) |
| 25. Cross-version API changes | ✅ `docs/VERSION_SUPPORT.md` + `docs/evidence/` |
| 26. Dangerous systems | ✅ §14 + `docs/RISK_REGISTER.md` |
| 27. Ownership model | ✅ `docs/SYNCHRONIZATION_MODEL.md` |
| 28. Save-data architecture | ✅ `docs/SAVE_FORMAT.md` |
| 29. Multiplayer lifecycle | ✅ `docs/NETWORK_PROTOCOL.md` |
| 30. Risk register | ✅ `docs/RISK_REGISTER.md` |
