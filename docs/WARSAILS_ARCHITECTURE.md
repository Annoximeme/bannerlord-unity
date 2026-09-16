# War Sails (NavalDLC) Architecture

**Every type, member and signature below was read directly from `NavalDLC.dll` / `TaleWorlds.CampaignSystem.dll` (reference assemblies, v1.4.8.119303) using `tools/apiscan/cli_meta.py`.** Confidence is `VERIFIED` unless stated.

---

## 1. The Defining Structural Fact

> **The naval data model ships in the base game. The DLC ships behaviors, missions and content.**

| In `TaleWorlds.CampaignSystem.dll` (always present) | In `NavalDLC.dll` (DLC only) |
|---|---|
| `Naval.Ship`, `Naval.AnchorPoint`, `Naval.Figurehead`, `Naval.DefaultFigureheads` | 28 `CampaignBehaviors` (production, trade, repair, upgrade, distribution, pirates, storms, fishing, transitions…) |
| `Actions.ChangeShipOwnerAction`, `DestroyShipAction`, `RepairShipAction`, `RaftStateChangeAction` | `Missions.Objects.MissionShip` + 61 mission objects |
| `PartyBase.Ships`, `.FlagShip`, `._ships`, `AddShipInternal`, `RemoveShipInternal`, `GetShipsVersion()` | `Missions.MissionLogics.*` (44 types) incl. `NavalShipsLogic` |
| `MobileParty.Ships`, `.IsCurrentlyAtSea`, `.IsInRaftState`, `.Anchor`, `.IsTargetingPort`, `.HasNavalNavigationCapability` | `Missions.NavalPhysics` (14), `ShipActuators` (16), `ShipControl` (7), `ShipInput` (6) |
| `Clan.HasNavalNavigationCapability` | `Missions.AI.*` (naval AI behaviors, tactics, team AI) |
| `MapEvent.IsNavalMapEvent`, `BattleTypes.BlockadeBattle/BlockadeSallyOutBattle`, `BlockadeBattleMapEvent` | `NavalMissions` / `NavalMissionManager`, `NavalMissionState` |
| `PlayerEncounter.IsNavalEncounter()` | `Storyline` (47 types), 15 quests |
| `Helpers.ShipHelper`, `Party.ShipTemplateStack` | `Map.Storm`, `Map.StormManager` |
| Models: `ShipStatModel`, `ShipCostModel`, `FleetManagementModel`, `PartyShipLimitModel`, `CampaignShipDamageModel`, `CampaignShipParametersModel` | `NavalDLC.GameModels` (89 types), `NavalPolicies`, `NavalSkills`, `NavalPerks` |
| `TaleWorlds.Core`: `ShipHull`, `ShipUpgradePiece`, `IShipOrigin`, `MissionShipObject`, `ShipVisualSlotInfo` | `NavalDLCEvents`, `NavalDLCManager`, `SaveableNavalDLCTypeDefiner`, `NavalVersion` |

**Design consequence:** ship *ownership* replication targets the base game and is DLC-independent. Only naval *behaviors* and *missions* require `NavalDLC.dll`, which we isolate in an optional assembly (`ARCHITECTURE.md` §9).

---

## 2. Ship Representation (Goal 16)

`TaleWorlds.CampaignSystem.Naval.Ship` — **base type `System.Object`**, implements `TaleWorlds.Core.IShipOrigin`, `TaleWorlds.CampaignSystem.IRandomOwner`.

### Persisted state (carries `[SaveableField]` / `[SaveableProperty]`)

| Member | Type | Kind |
|---|---|---|
| `_shipPieces` | `Dictionary<string, ShipUpgradePiece>` | `[SaveableField]` |
| `ShipHull` | `TaleWorlds.Core.ShipHull` | `[SaveableField]`, public |
| `_hitPoints` | `float` | `[SaveableField]` |
| `_sailHitPoints` | `float` | `[SaveableField]` |
| **`_owner`** | **`PartyBase`** | `[SaveableField]` |
| `_name` | `TextObject` | `[SaveableField]` |
| `_unlockedUpgradePieces` | `MBList<ShipUpgradePiece>` | `[SaveableField]` |
| `Figurehead` | `Naval.Figurehead` | `[SaveableProperty]` |
| `IsInvulnerable`, `IsTradeable`, `IsUsedByQuest` | `bool` | `[SaveableProperty]`, public set |
| `RandomValue` | `int` | `[SaveableProperty]` |

### Runtime-only (NOT saved)

`_versionNo` (uint), `_isVersionDirty` (bool), `CustomSailPatternId` (string property, no Saveable attribute).

### Mutable surface

`Owner` (public set) · `HitPoints` (public set) · `SailHitPoints` (public set) · `IsInvulnerable`/`IsTradeable`/`IsUsedByQuest` (public set) · `SetName(TextObject)` · `ChangeFigurehead(Figurehead)` · `EquipUpgradePiece(string, ShipUpgradePiece)` · `OnShipDamaged(float, IShipOrigin, ref float)` · `UpdateVersionNo()`.

### Derived stats (read-only, recompute locally — do **not** replicate)

`MaxHitPoints`, `MaxSailHitPoints`, `MaxFireHitPoints`, `TotalCrewCapacity`, `MainDeckCrewCapacity`, `SkeletalCrewCapacity`, `SeaWorthiness`, `FlagshipScore`, `InventoryCapacity`, `CrewCapacityBonusFactor`, `ShipWeightFactor`, `ForwardDragFactor`, `CrewShieldHitPointsFactor`, `CrewMeleeDamageFactor`, `AdditionalAmmo`, `AdditionalArcherQuivers`, `AdditionalThrowingWeaponStack`, `MaxOarPowerFactor`, `MaxOarForceFactor`, `SailForceFactor`, `SailRotationSpeedFactor`, `FurlUnfurlSpeedFactor`, `RudderSurfaceAreaFactor`, `MaxRudderForceFactor`, `CampaignSpeedBonusFactor`, `CanEquipFigurehead`, `GetCampaignSpeed()`, `GetCombatFactor()`, `GetSiegeEngines()`.

### ⚠ `Ship` has no identity

No `MBGUID`. No `StringId`. Not an `MBObjectBase`. **This is RISK-01.** See `SYNCHRONIZATION_MODEL.md` §4 for the synthesized-identity design.

---

## 3. Fleet Representation (Goal 16)

**There is no `Fleet` type.** A fleet is:

| Concept | Actual representation |
|---|---|
| A party's fleet | `PartyBase._ships` : `MBList<Ship>`, exposed as `PartyBase.Ships` / `MobileParty.Ships` (`MBReadOnlyList<Ship>`) |
| Flagship | `PartyBase.FlagShip` : `Ship` |
| Fleet mutation | `PartyBase.AddShipInternal(Ship)` / `RemoveShipInternal(Ship)` — **`internal`** |
| Fleet change detection | `PartyBase.GetShipsVersion()` → `int` |
| Clan-level fleet logistics | `NavalDLC.CampaignBehaviors.ClanFleetManagementCampaignBehavior` (+ nested `SentTroopsData`, `ClanFleetManagementCampaignBehaviorTypeDefiner`), interface `IFleetManagementCampaignBehavior` |
| Fleet capacity rules | `ComponentInterfaces.PartyShipLimitModel` → `GetIdealShipNumber(MobileParty)`, `GetIdealShipNumber(Clan)`, `GetShipPriority(MobileParty, Ship, bool)` |
| Troop return rules | `ComponentInterfaces.FleetManagementModel` → `CanTroopsReturn()`, `GetReturnTimeForTroops(Ship)`, `CanSendShipToPlayerClan(Ship, int, int, out TextObject)` |
| Templates | `Party.ShipTemplateStack` struct (`ShipHull`, `MinValue`, `MaxValue`) |
| Distribution | `NavalShipDistributionCampaignBehavior`; **beta adds** `ComponentInterfaces.ShipDistributionModel` |

---

## 4. Naval Movement (Goal 17)

### Campaign map

| Member | Access |
|---|---|
| `MobileParty.IsCurrentlyAtSea` | get/set **public** (backing `_isCurrentlyAtSea` `[SaveableField]`) |
| `MobileParty.IsInRaftState` | get/set **public** (backing `_isInRaftState` `[SaveableField]`) |
| `MobileParty.IsTargetingPort` | get public / set private (backing `_isTargetingPort` `[SaveableField]`) |
| `MobileParty.Anchor` : `AnchorPoint` | get public / set private; **`SetAnchor(AnchorPoint)` is public** |
| `MobileParty.HasNavalNavigationCapability` / `HasLandNavigationCapability` | get public |
| `Clan.HasNavalNavigationCapability` | get public |
| `MobileParty.GetRegionSwitchCostFromLandToSea()` / `GetRegionSwitchCostFromSeaToLand()` | public → int |
| `MobileParty.StartTransitionNextFrameToExitFromPort` | public field (bool) |
| `MobileParty.IsNavalVisualDirty`, `SetNavalVisualAsDirty()`, `OnNavalVisualsUpdated()` | public |
| `MobileParty.ChangeIsCurrentlyAtSeaCheat()` | public (cheat path — useful for testing) |
| `Ship.GetCampaignSpeed()`, `Ship.SeaWorthiness`, `Ship.CampaignSpeedBonusFactor` | public |
| Land↔sea transition driver | `NavalDLC.CampaignBehaviors.NavalTransitionCampaignBehavior` |
| Raft state | `Actions.RaftStateChangeAction.ActivateRaftStateForParty(MobileParty)` / `.DeactivateRaftStateForParty(MobileParty)`; `NavalDLC.CampaignBehaviors.RaftStateCampaignBehavior` |
| Sea hazards | `NavalDLC.Map.StormManager` (`ICustomSystemManager`): `SpawnedStorms`, `CreateStormAtPosition(Vec2)`, `CreateStormAtPosition(Vec2, Storm.StormTypes)`, `OnAfterLoad()`; `NavalDLC.Map.Storm` (+ `StormTypes`, `PreviousData`); `StormCampaignBehavior`, `SeaDamageCampaignBehavior` |
| Storm event | `NavalDLCEvents.OnStormCreatedEvent` : `IMbEvent<Map.Storm>` |
| Map scene | `NavalDLC.INavalMapSceneWrapper` (via `NavalDLCManager.NavalMapSceneWrapper`) |

**Sync note:** `IsCurrentlyAtSea` and `IsInRaftState` are publicly settable and saveable — clean replication targets. `Anchor` needs `SetAnchor`. Storms are server-authoritative world state; `CreateStormAtPosition` is a server-only call replicated as an event.

### Mission layer

`NavalDLC.Missions.ShipControl.{NavalState, NavalVec}` · `MissionShip.GetNavalState()` / `GetNavalState(out NavalVec)` · `Missions.NavalPhysics` (14 types incl. `NavalPhysics.SinkingState`) · `Missions.ShipActuators` (16, incl. `MissionSail`) · `Missions.ShipInput` (6) · `Missions.MissionLogics.NavalTrajectoryPlanningLogic`, `WaveParametersComputerLogic` (+ `WaterParameters`), `NavalCustomBattleWindAndWaveLogic` (+ `NavalCustomBattleWindConfig.Direction`).

---

## 4a. Water Navigation & Terrain Classification

`CLAUDE.md` calls out `CoastalSea`, `OpenSea`, `River` and `NonNavigableRiver`. All four are **VERIFIED** as members of the `TaleWorlds.Core.TerrainType` enum in **`TaleWorlds.Core.dll`** — i.e. water navigation is classified by **base-game map terrain**, not by a DLC-specific system.

### `TaleWorlds.Core.TerrainType` — complete enum (VERIFIED)

`Plain` · `Desert` · `Snow` · `Forest` · `Steppe` · `Fording` · `Mountain` · `Lake` · `Water` · **`River`** · `Canyon` · `RuralArea` · `Swamp` · `Dune` · `Bridge` · **`CoastalSea`** · **`OpenSea`** · `Beach` · `Cliff` · **`NonNavigableRiver`** · `LandRestriction` · `SeaRestriction` · `UnderBridge`

Water-relevant members and their significance:

| Member | Significance |
|---|---|
| `CoastalSea` | Navigable coastal water — the normal naval travel surface near land |
| `OpenSea` | Deep water; carries distinct speed and attrition rules (see below) |
| `River` | Navigable river |
| `NonNavigableRiver` | River that blocks naval movement — a hard navigation boundary |
| `Lake`, `Water` | Other water bodies |
| `Beach` | Land/sea interface — relevant to seaborne raids and landings |
| `Cliff` | Impassable coast — blocks landing |
| `Fording`, `Bridge`, `UnderBridge` | Land/water crossing points |
| `LandRestriction`, `SeaRestriction` | Explicit movement restriction zones |

### Terrain query API (VERIFIED)

| Member | Assembly |
|---|---|
| `TaleWorlds.CampaignSystem.Campaign.MapSceneWrapper` → `TaleWorlds.CampaignSystem.Map.IMapScene` | `TaleWorlds.CampaignSystem.dll` |
| `IMapScene.GetTerrainTypeAtPosition(ref CampaignVec2)` → `TerrainType` | `TaleWorlds.CampaignSystem.dll` |
| `IMapScene.GetFaceTerrainType(PathFaceRecord)` → `TerrainType` | `TaleWorlds.CampaignSystem.dll` |
| `IMapScene.GetTerrainTypeName(TerrainType)` → `string` | `TaleWorlds.CampaignSystem.dll` |
| `TaleWorlds.CampaignSystem.Map.IMapSceneCreator` | `TaleWorlds.CampaignSystem.dll` |

### Naval map scene extension (VERIFIED)

`NavalDLC.INavalMapSceneWrapper` — obtained via `NavalDLCManager.NavalMapSceneWrapper`:

| Member | Signature |
|---|---|
| `GetSpawnPoints(string)` | → `List<(CampaignVec2, float)>` |
| `GetWindAtPosition(Vec2)` | → `Vec2` |
| `Tick(float)` | → `void` |

> **`GetWindAtPosition` is world state that affects naval movement.** It must be server-authoritative and replicated, or two clients will compute different ship speeds. Treated as T0 world state in `SYNCHRONIZATION_MODEL.md` §2.

### Water-type-dependent campaign rules (VERIFIED)

| Rule | Exact symbol |
|---|---|
| Open-sea attrition damage | `NavalDLC.GameComponents.NavalDLCCampaignShipDamageModel.CalculateOpenSeaAttritionDamageForShip(Ship)`; field `AverageBeingOnOpenSeaRatio` |
| Open-sea speed bonus | `NavalDLC.GameComponents.NavalDLCPartySpeedCalculationModel.OpenSeaBonus`; `_openSeaEffect` (`TextObject`) |
| Storm placement | `NavalDLC.CampaignBehaviors.StormCampaignBehavior._allOpenSeaWeatherNodePositions` : `List<Vec2>` |

### Battle power context by water type (VERIFIED)

`TaleWorlds.CampaignSystem.MapEvents.MapEvent.PowerCalculationContext` — complete enum:

`PlainBattle` · `SteppeBattle` · `DesertBattle` · `DuneBattle` · `SnowBattle` · `ForestBattle` · `RiverCrossingBattle` · `Village` · `Siege` · **`SeaBattle`** · **`OpenSeaBattle`** · **`RiverBattle`** · **`NavalRaid`** · `Estimated`

⇒ The campaign distinguishes **coastal sea battles, open-sea battles, river battles and naval raids** as separate power-calculation contexts. Battle outcome simulation is water-type dependent, so the server must resolve naval battles with the correct context or results will diverge from single-player expectations.

### Synchronization implications

1. **Navigation boundaries are deterministic map data**, not runtime state — `TerrainType` comes from the map scene, identical on every client. It does **not** need replication, and must **not** be replicated.
2. **Wind does need replication** (`GetWindAtPosition`) — it is dynamic and affects speed.
3. **Storms are server-authoritative** (`StormManager.CreateStormAtPosition`), placed on open-sea nodes.
4. **Open-sea attrition is a server-side tick effect** on `Ship._hitPoints` — clients must never apply it locally or hit points will double-decay.

---

## 5. Naval Encounters (Goal 18)

All **base game**:

| API | Signature |
|---|---|
| `PlayerEncounter.IsNavalEncounter()` | `public static bool` |
| `MapEvent.IsNavalMapEvent` | `public bool` property |
| `Helpers.MapEventHelper.IsNavalRaid(MapEvent)` | `public static bool` |
| `MapEvent.BattleTypes.BlockadeBattle` / `.BlockadeSallyOutBattle` | enum members |
| `MapEvents.BlockadeBattleMapEvent` | class; `CalculateBesiegerSideNavalPower(BesiegerCamp)`, `CalculateAttackerNavalPower(PartyBase)` |
| `MapEvent.PowerCalculationContext.NavalRaid` | enum member |
| Conversation gating | `Conversation.Tags.PlayerIsAtSeaTag`, `Conversation.Tags.NPCIsInSeaTag` |
| Naval party AI | `TaleWorlds.CampaignSystem.CampaignBehaviors.NavalPatrolPartiesCampaignBehavior` (**declared in `NavalDLC.dll`, in the `TaleWorlds.CampaignSystem.CampaignBehaviors` namespace**) |
| Pirates | `NavalDLC.CampaignBehaviors.PiratesCampaignBehavior` (+ `PatrolZone`, `PiratesCampaignBehaviorSaveDefiner`) |

Encounter creation itself is the shared path: `EncounterManager.StartPartyEncounter(PartyBase, PartyBase)` / `StartSettlementEncounter(MobileParty, Settlement)`.

---

## 6. Naval Mission Creation (Goal 19)

**`NavalDLC.Missions.NavalMissions`** — public static factory:

| Method | Signature |
|---|---|
| `OpenNavalBattleMission` | `(MissionInitializerRecord) → Mission` |
| `OpenNavalRaidMission` | `(TroopRoster, BattleSideEnum, List<Ship>) → Mission` |
| `OpenNavalSetPieceBattleMission` | `(MissionInitializerRecord, MBList<IShipOrigin>, MBList<IShipOrigin>, MBList<IShipOrigin>) → Mission` |
| `OpenNavalStorylineCaptivityMission` | `(MissionInitializerRecord, CharacterObject ×3) → Mission` |
| `OpenNavalStorylinePirateBattleMission` | `(MissionInitializerRecord, MobileParty, int) → Mission` |
| `OpenNavalStorylineQuest5SetPieceBattleMission` | `(MissionInitializerRecord, MobileParty, Quest5SetPieceBattleMissionState) → Mission` |
| `OpenNavalStorylineWoundedBeastBattleMission` | `(MissionInitializerRecord) → Mission` |
| `OpenNavalStorylineAlleyFightMission` | `(MissionInitializerRecord) → Mission` |
| `OpenNavalFinalConversationMission` | `() → Mission` |

**`NavalDLC.Missions.NavalMissionManager`** — instance equivalents returning `TaleWorlds.Core.IMission`: `OpenNavalBattleMission(MissionInitializerRecord)`, `OpenNavalRaidMission(TroopRoster, BattleSideEnum, List<Ship>)`, `OpenNavalSetPieceBattleMission(...)`.

Host: **`NavalDLC.NavalMissionState : TaleWorlds.MountAndBlade.MissionState`**.
Force-start / debug: `NavalDLC.CampaignBehaviors.NavalForceStartNavalMissionCampaignBehavior` → `StartNavalBattle(MenuCallbackArgs)`, `StartNavalMissionFromCheats()`, `StartNavalMissionWithHandlingCheat()` — useful test entry points.
Ship instantiation: `NavalDLC.MissionShipFactory`; assignment record: `NavalDLC.ShipAssignment` (binds `TeamSideEnum`, `FormationClass`, `MissionShipObject`, `IShipOrigin`, `MissionShip`, `Formation`).

**UNCONFIRMED:** the exact code path from a campaign `MapEvent` with `IsNavalMapEvent == true` to one of these calls. Method bodies are stripped. This is investigation item 5 in audit §16.

---

## 7. Boarding (Goal 20) — mission layer only

| Concern | API |
|---|---|
| Order state | `NavalDLC.Missions.ShipOrder.BoardAtWill` (get public/set private); `.IsBoardingAvailable` (get/set public) |
| Target | `ShipOrder.GetBoardingTargetShip()` → `MissionShip`; `SetBoardingTargetShip(MissionShip)` |
| Query | `ShipOrder.GetIsAttemptingBoarding()` → bool |
| Capture hook | `ShipOrder.OnShipCaptured(MissionShip, MissionShip)` |
| Broadcast | `NavalShipsLogic.BoardingOrderEvent` : `Action<MissionShip, MissionShip>` |
| Attachment chain | `NavalShipsLogic.ShipHookThrowEvent`, `.ShipsConnectedEvent`, `.BridgeConnectedEvent`, `.ShipAttachmentBrokenEvent`, `.ShipAttachmentLostEvent`, `.CutLooseOrderEvent` |
| Troop placement | `TaleWorlds.MountAndBlade.ShipPlacementDetachment.SetBoarding(bool, Vec2)`; `ShipPlacementDetachment.ShipPlacementPosition.CalculateBoardingScore(Vec2, out float, out float, out PositionCondition, out bool)` |
| AI | `NavalDLC.Missions.AI.Behaviors.NavalBehaviorBoardShipSubtask` (+ `ShipBoardingState`); `BehaviorNavalEngageCorrespondingEnemy.ShipBoardingState`; `AgentNavalAIComponent.DecideBoardingTaunts()` |
| Damage model | `NavalDLC.GameComponents.NavalAgentApplyDamageModel.IsAgentCrewBoarded(Agent)` |
| Input | `TaleWorlds.MountAndBlade.GameKeyDefinition.AttemptBoarding` |
| Perk | `NavalDLC.CharacterDevelopment.NavalPerks.Mariner.BoardingMaster` |

> **No campaign-layer boarding API exists.** Boarding is purely in-mission; campaign consequences are applied afterwards via §8.

---

## 8. Ship Ownership & State Mutation (Goal 21)

### Campaign actions — `TaleWorlds.CampaignSystem.Actions` (1.4.8 stable)

| Action | Signature | Maps to |
|---|---|---|
| `ChangeShipOwnerAction.ApplyByTransferring` | `(PartyBase, Ship)` | player/party transfer |
| `ChangeShipOwnerAction.ApplyByTrade` | `(PartyBase, Ship)` | purchase/sale |
| **`ChangeShipOwnerAction.ApplyByLooting`** | `(PartyBase, Ship)` | **ship capture** |
| `ChangeShipOwnerAction.ApplyByProduction` | `(PartyBase, Ship)` | shipyard output |
| `ChangeShipOwnerAction.ApplyByMobilePartyCreation` | `(PartyBase, Ship)` | party spawn |
| `DestroyShipAction.Apply` | `(Ship)` | **ship destruction / sinking** |
| `DestroyShipAction.ApplyByDiscard` | `(Ship)` | scuttle/discard |
| `RepairShipAction.Apply` | `(Ship, Settlement)` | paid repair |
| `RepairShipAction.ApplyForFree` | `(Ship)` | free repair |
| `RepairShipAction.ApplyForBanditShip` | `(Ship)` | bandit repair |
| `RaftStateChangeAction.ActivateRaftStateForParty` | `(MobileParty)` | raft on |
| `RaftStateChangeAction.DeactivateRaftStateForParty` | `(MobileParty)` | raft off |
| `MapEvent.LootDefeatedPartyShips` | `(MBReadOnlyList<MapEventParty>, MBReadOnlyList<MapEventParty>)` | **naval loot** |

⚠ **This API changed in 1.5.3-beta** — see §11 and `docs/VERSION_SUPPORT.md`.

### Supporting helpers

`Helpers.ShipHelper.GetOrderedNavalRaidShipsOfPlayerParty()` → `List<Ship>` · `GetShipBanner(PartyBase)` / `(IShipOrigin, IAgent)` · `GetSailColors(...)`.
Behaviors: `ShipProductionCampaignBehavior`, `ShipTradeCampaignBehavior`, `ShipRepairCampaignBehavior`, `ShipUpgradeCampaignBehavior`, `ShipNameCampaignBehavior` (+ `NameTrait`), `NavalDLCFigureheadCampaignBehavior`.

### Mission-layer state — `NavalDLC.Missions.Objects.MissionShip`

Mutators: `DealDamage(float, MissionShip, out int, out int, out DamageTypes, out bool)` · `DealDamageToSails(Agent, float, float, MissionSail)` · `DealCollisionDamage(MissionShip, bool, Vec3, float)` · `DealFireDamage(float)` · `SetSinkingState(NavalPhysics.SinkingState)` · `SetOriginalTeamSide(TeamSideEnum)`.
State: `HitPoints`, `SailHitPoints`, `MaxSailHitPoints`, `FireHitPoints`, `BurntHullDamageTotal`, `IsSinking`, `ShipSailState` (`SailState`), `Team`, `OriginalTeamSide`, `CrewSizeOnMainDeck`, `CrewSizeOnLowerDeck`, `TotalCrewCapacity`, `GetPartialHitPoints(int)`, `DWAAgentState`.

---

## 9. Mission-Layer Sync Hooks — `NavalDLC.Missions.MissionLogics.NavalShipsLogic`

`MissionLogic`, implements `IVehicleHandler`, `IMissionBehavior`.

State: `PlayerControlledShip`, `AllShips` (`MBReadOnlyList<MissionShip>`), `SeaPathfindingEnabled`, `IsTeleportingShips`, `IsMissionEnding`, `IsDeploymentMode`, `CanHaveConnectionCooldown`.

**27 `System.Action` events** (not `IMbEvent` — no engine ordering/replay guarantees):

| Category | Events |
|---|---|
| Lifecycle | `ShipSpawnedEvent`, `BeforeShipRemovedEvent`, `ShipRemovedEvent`, `MissionEndEvent`, `ShipPreparedForAbandonmentEvent` |
| **Outcome** | **`ShipCapturedEvent`** `(MissionShip, MissionShip, Formation, Formation)`, **`ShipSunkEvent`** `(MissionShip)`, `ShipBurnedEvent`, `ShipLowHealthEvent`, `SailsDeadEvent` |
| **Boarding** | **`BoardingOrderEvent`** `(MissionShip, MissionShip)`, `ShipHookThrowEvent`, `ShipsConnectedEvent`, `BridgeConnectedEvent`, `ShipAttachmentBrokenEvent`, `ShipAttachmentLostEvent`, `CutLooseOrderEvent` |
| Combat | `ShipHitEvent` `(MissionShip, Agent, int, Vec3, Vec3, MissionWeapon, int)`, `ShipRammingEvent`, `ShipAboutToBeRammedEvent`, `ShipCollisionEvent` |
| Control | `ShipControllerChanged`, `ShipTeleportedEvent`, `ShipTransferredToTeamEvent` / `Before…`, `ShipTransferredToFormationEvent` / `Before…` |

These are the natural interception points for naval battle replication. **UNCONFIRMED:** their firing order and whether any are re-entrant.

---

## 10. Other Naval Campaign Systems (Goal 15)

`NavalDLCManager` exposes: `GameModels`, `NavalCulturalFeats`, `NavalBuildingTypes`, `NavalVillageTypes`, `NavalSkills`, `NavalSkillEffects`, `NavalPerks`, `NavalPolicies`, `NavalStorylineData`, `NavalDLCEvents`, `NavalItemCategories`, `NavalMapSceneWrapper`, `FishingParties` (`Dictionary<Village, List<FishingPartyComponent>>`), `StormManager`.

`NavalDLCEvents` (`IMbEvent`-based, so these *do* have engine event semantics): `IsNavalQuestPartyEvent` `<PartyBase, NavalStorylinePartyData>`, `OnNavalStorylineActivityChangedEvent` `<bool>`, `OnStormCreatedEvent` `<Map.Storm>`, `OnSisterRansomedEvent`, `OnGunnarSavedEvent`, `OnSisterRansomRequestedEvent`, `OnNavalStorylineCanceledEvent` `<StorylineCancelDetail>`, `OnNavalStorylineTutorialSkippedEvent`.

Storyline: 47 types, 15 quest classes (`NavalStorylineQuestBase` and subclasses), `NavalStorylineData.StartNavalStoryline()`, `GetNavalMissionInitializerTemplate(string)`. Persistence: `NavalDLC.SaveableNavalDLCTypeDefiner`.

Also present: `FishingPartyComponent`, `PortCharactersCampaignBehavior`, `NavalKingdomPolicyCampaignBehaviour`, `NavalOrderOfBattleCampaignBehavior` (+ `NavalOrderOfBattleFormationData`), `NavalCompanionRolesCampaignBehavior`, `NavalCharacterCreationCampaignBehavior`, perk behaviors (`NavalNimbleSurge`, `NavalStormrider`, `NavalVeteransWisdom`).

---

## 11. Version Sensitivity of the Naval API

Between **1.4.8.119303 (stable)** and **1.5.3.122374-beta** (VERIFIED by diff, `docs/evidence/`):

`NavalDLC.dll`: 159 members removed, 930 added — **relatively stable**.
`Ship` itself: only 2 removed (`OnPlayerCharacterChanged()`, `ResetUnlockedUpgradePieces()`), 3 added (`CanHaveUnlockedPieces` + accessors) — **very stable**.

But **ship ownership gained a whole subsystem in beta**:

| Added in 1.5.3-beta | |
|---|---|
| `ChangeShipOwnerAction.ApplyByStashingShipIntoSettlement(Settlement, Ship)` | ship stashing |
| `ChangeShipOwnerAction.ApplyByUnstashingShipFromSettlement(PartyBase, Settlement, Ship)` | |
| `ChangeShipOwnerAction.ApplyByTemporarilyRemovingShipsFromPlayer(Ship)` | |
| `ChangeShipOwnerAction.ApplyByGivingBackShipsToPlayer(Ship)` | |
| `ChangeShipOwnerAction.ApplyInternal(PartyBase, Ship, Settlement, ShipOwnerChangeDetail)` | new funnel |
| `ChangeShipOwnerAction.ShipOwnerChangeDetail` (enum) | new |
| `Settlement.ShipStash` : `MBList<Ship>` | new persisted state |
| `CampaignSystem.PlayerDataForNavalAutoTravel` (+ `_reservedShips`) | naval auto-travel |
| `ComponentInterfaces.ShipDistributionModel` / `GameComponents.DefaultShipDistributionModel` | new model |
| `MapEventSide._shipSiegeEngineList` : `MBList<(SiegeEngineType, Ship)>` | naval siege engines |
| `CampaignEvents._canHaveUnlockedUpgradePieceEvent` | new event |
| `Helpers.ShipHelper.GetAmountToRecoverFromRemainingShipsAfterDistribution(...)`, `GetClanPartyToGetAvailableShip(...)`, `GetSailColorsForParty(...)` | new helpers |
| **`MapEvent.LootDefeatedPartyShips` gained a `bool` parameter** | **signature break** |

**Implication:** ship ownership must be wrapped behind our own adapter interface from day one. Binding directly to `ChangeShipOwnerAction`'s 1.4.8 shape will break on 1.5.x. See `docs/VERSION_SUPPORT.md`.

---

## 12. Naval Synchronization Plan (summary)

| Feature | Layer | Authority | Mechanism | Confidence |
|---|---|---|---|---|
| Ship ownership | Campaign | Server | Replicate `Ship._owner` + party ship list; apply via `ChangeShipOwnerAction` adapter | VERIFIED APIs |
| Fleet composition | Campaign | Server | Replicate ordered ship-id list per `PartyBase`; poll `GetShipsVersion()` | VERIFIED |
| Ship damage/repair (campaign) | Campaign | Server | Replicate `_hitPoints`, `_sailHitPoints`; apply via `RepairShipAction` | VERIFIED |
| Ship destruction | Campaign | Server | `DestroyShipAction.Apply` server-side, replicate removal | VERIFIED |
| Ship capture | Mission→Campaign | Server | Observe `ShipCapturedEvent` → report → server applies `ApplyByLooting` | VERIFIED hooks, UNCONFIRMED flow |
| Naval loot | Campaign | Server | `MapEvent.LootDefeatedPartyShips` server-side only (version-adapted) | VERIFIED |
| At-sea / raft state | Campaign | Server (client intent) | Replicate `IsCurrentlyAtSea`, `IsInRaftState`, `Anchor` | VERIFIED |
| Storms | Campaign | Server | `CreateStormAtPosition` server-only; replicate as event | VERIFIED |
| Seaborne raids | Campaign | Server | `MapEventHelper.IsNavalRaid`, `OpenNavalRaidMission` | VERIFIED |
| Naval blockade sieges | Campaign | Server | `BattleTypes.BlockadeBattle`, `BlockadeBattleMapEvent` | VERIFIED |
| **Boarding** | **Mission** | **Deferred** | Needs RISK-03 resolution | **UNCONFIRMED** |
| **Naval battle simulation** | **Mission** | **Deferred** | Needs RISK-03 + install | **UNKNOWN** |

**Phase 1 takes the campaign-layer rows only.** The mission-layer rows are explicitly out of scope until a real install exists.
