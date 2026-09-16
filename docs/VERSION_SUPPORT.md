# Version Support

**Goals 1–3 and 25.**

---

## 1. Status: the installed version is NOT yet known

The Phase 0 audit ran on a Linux container with **no Bannerlord installation** (VERIFIED: filesystem sweep found no game files, no `TaleWorlds*.dll`, no Steam library). The literal questions "what exact version is installed" and "what exact War Sails version is installed" **cannot be answered from that environment**.

What this document provides instead:
1. The complete landscape of versions that exist, and which are stable vs beta (§2).
2. The exact runtime procedure to measure the real machine (§5).
3. A measured cross-version API-change analysis (§3–§4).

**Action required:** run §5 on the target machine and record the result in §6.

## 2. Version Landscape (VERIFIED from the public package feed)

| Fact | Value |
|---|---|
| Total published Core reference-assembly versions | 126 |
| **Latest stable** | **`1.4.8.119303`** |
| **Latest beta** | **`1.5.3.122374-beta`** |
| Current beta line | `1.5.1.120547-beta`, `1.5.2.120933-beta`, `1.5.2.121216-beta`, `1.5.3.122374-beta` |
| Recent stable line | `1.4.5.114896`, `1.4.5.114927`, `1.4.5.115026`, `1.4.6.115439`, `1.4.6.115628`, `1.4.7.117131`, `1.4.7.117484`, `1.4.8.119303` |
| NavalDLC package versions | 62 |

Version format: `Major.Minor.Revision.ChangeSet`, with a channel prefix (`v` release / `e` early access) produced by `ApplicationVersion.GetPrefix(ApplicationVersionType)` (VERIFIED).

### Stable vs beta is read, not guessed

`TaleWorlds.Library.ApplicationVersionType` (VERIFIED) = `Invalid | Alpha | Beta | EarlyAccess | Release | Development`.
`Release` ⇒ stable. `Beta` ⇒ beta. Exposed as `ApplicationVersion.ApplicationVersionType`.

> **UNCONFIRMED:** `NavalDLC` packages exist for version strings as early as `1.0.0.4407`, predating War Sails. Whether those contain real naval assemblies was not verified. Do **not** infer the War Sails release date from the package feed.

## 3. Measured API Churn: 1.4.8.119303 → 1.5.3.122374-beta

Produced by `tools/apiscan/cli_meta.py surface` + set diff. Raw data in `docs/evidence/`.

| Assembly | Members removed | Members added | Types removed | Types added |
|---|---|---|---|---|
| `TaleWorlds.CampaignSystem.dll` | **2,638** | **4,248** | 92 | 170 |
| `NavalDLC.dll` | 159 | 930 | — | — |

### Churn on the types we bind to (exact owner match, VERIFIED)

| Type | Removed | Added | Assessment |
|---|---|---|---|
| `Naval.Ship` | 2 | 3 | **Very stable** |
| `Party.PartyBase` | 4 | 2 | Stable |
| `Party.MobileParty` | 9 | 9 | Moderate |
| `EncounterManager` | **0** | **0** | **Unchanged** |
| `Siege.SiegeEvent` | 1 | 1 | Stable |
| `Army` | 0 | 2 | Stable |
| `Settlements.Settlement` | 0 | 9 | Additive |
| `Clan` | 1 | 10 | Additive |
| `Hero` | 1 | 16 | Additive |
| `Encounters.PlayerEncounter` | 8 | 2 | **Volatile** |
| `MapEvents.MapEventSide` | 8 | 5 | **Volatile** |
| **`MapEvents.MapEvent`** | **16** | **6** | **Most volatile** |
| `Campaign` | 7 | 25 | Moderate |
| `CampaignEvents` | 11 | 24 | Moderate |
| `Actions.ChangeShipOwnerAction` | 1 | 5 | **Reshaped** |

## 4. Specific Breaking Changes

### 4.1 `CampaignEvents` — only 3 events removed (VERIFIED)

`OnCharacterCreationIsOverEvent`, `OnMapMarkerCreatedEvent`, `OnMapMarkerRemovedEvent`.

**None are sync-critical.** All the events in `INITIAL_TECHNICAL_AUDIT.md` §6 survive both versions. Good news for the sync design.

### 4.2 `MapEvent` — significant refactor (VERIFIED)

Removed in beta: `_mapEventType` field · `MapEventSettlement` property + backing field + setter · `Initialize(PartyBase, PartyBase, MapEventComponent, BattleTypes)` · `CheckSiegeStageChange()` · `FinishBattleAndKeepSiegeEvent()` · `_keepSiegeEvent` · `AddInsideSettlementParties(Settlement)` · `GetEventDirection(BattleSideEnum, out float)` · `SetPartyBaseEventLocalPosition(...)` · `SetPositionAfterMapChange(CampaignVec2)` · `OverrideMapEventSettlementForRaidToFieldBattleSwitch(...)` · `LootDefeatedPartyShips(list, list)`.

Added in beta: `Initialize(PartyBase, PartyBase, MapEventComponent)` (**arity change**) · `LootDefeatedPartyShips(list, list, bool)` (**signature change**) · `SetPositionAfterMapChange(CampaignVec2, CampaignVec2)` (**signature change**) · `Sides` property (`MapEventSide[]`) · `DoVisualAdjustmentsOfParties()`.

⇒ **Battle and siege coupling was restructured between the lines.** Any direct binding to `MapEvent`'s 1.4.8 shape breaks on 1.5.x.

### 4.3 `MobileParty` — `PartyObjective` removed (VERIFIED)

Removed: `Objective` property + backing field + `SetPartyObjective(PartyObjective)`, `IsSpotted()`, `HasPerk(PerkObject, bool)`, `CanAssignMoreRoleToHero(Hero)`.

### 4.4 Ship ownership — reshaped in beta (VERIFIED)

Added: `ApplyByStashingShipIntoSettlement(Settlement, Ship)` · `ApplyByUnstashingShipFromSettlement(PartyBase, Settlement, Ship)` · `ApplyByTemporarilyRemovingShipsFromPlayer(Ship)` · `ApplyByGivingBackShipsToPlayer(Ship)` · `ApplyInternal(PartyBase, Ship, Settlement, ShipOwnerChangeDetail)` · enum `ShipOwnerChangeDetail`.

New persisted naval state in beta: `Settlement.ShipStash` (`MBList<Ship>`), `PlayerDataForNavalAutoTravel` (+ `_reservedShips`), `MapEventSide._shipSiegeEngineList` (`MBList<(SiegeEngineType, Ship)>`), `ComponentInterfaces.ShipDistributionModel`.

⇒ **Save schema differs between the lines for naval data.** Cross-version saves will not be compatible (LIKELY).

## 5. Runtime Detection Procedure

Run on the target machine; paste output into §6.

```csharp
// In MBSubModuleBase.OnSubModuleLoad / OnBeforeInitialModuleScreenSetAsRoot
var v = TaleWorlds.Library.ApplicationVersion.FromParametersFile(/* engine params path */);
Log($"Game: {v}  Major={v.Major} Minor={v.Minor} Rev={v.Revision} ChangeSet={v.ChangeSet}");
Log($"Channel: {v.ApplicationVersionType}");   // Release => stable, Beta => beta

foreach (var m in TaleWorlds.ModuleManager.ModuleInfo.GetModules())   // enumerate loaded modules
    Log($"Module {m.Id} v{m.Version} requiresBase={m.RequiredBaseVersion}");

// War Sails present?
bool naval = AppDomain.CurrentDomain.GetAssemblies()
    .Any(a => a.GetName().Name == "NavalDLC");
if (naval)
    Log($"War Sails build: {NavalDLC.NavalVersion.GetApplicationVersionBuildNumber()}");
```

APIs used are all VERIFIED to exist: `ApplicationVersion`, `.ApplicationVersionType`, `ApplicationVersion.FromParametersFile`, `ModuleInfo.Version`, `ModuleInfo.RequiredBaseVersion`, `NavalVersion.GetApplicationVersionBuildNumber()`.

> `ModuleInfo.GetModules()` is illustrative — confirm the exact enumeration entry point against the installed `TaleWorlds.ModuleManager.dll` before relying on it (the `Version`/`RequiredBaseVersion` members themselves are verified).

## 6. Pinned Target — TO BE FILLED IN

| Field | Value |
|---|---|
| Bannerlord version | **UNKNOWN — pending §5** |
| Channel (`ApplicationVersionType`) | **UNKNOWN — pending §5** |
| War Sails installed | **UNKNOWN — pending §5** |
| War Sails build number | **UNKNOWN — pending §5** |
| Module set | **UNKNOWN — pending §5** |

## 7. Support Policy

1. **Support exactly one game version at a time.** Churn (§3) makes multi-version support a cost multiplier with no early payoff.
2. **Prefer the latest stable** (`1.4.8.119303` today) over beta. Betas move; `1.5.3-beta` restructured `MapEvent` mid-line.
3. **Hard version gate at module load.** Refuse to run on an unpinned version with a clear message. Cheap; prevents a whole failure class.
4. **Wrap volatile APIs behind adapters** from day one: `MapEvent`, `PlayerEncounter`, `MapEventSide`, `ChangeShipOwnerAction`. Adapters are the only place version conditionals may live.
5. **CI gate on API drift.** `cli_meta.py surface` the pinned assemblies, diff against a committed baseline, fail the build on removal of anything we bind to.
6. **Never mix versions across a session.** Enforced at handshake (`NETWORK_PROTOCOL.md` §5).

## 8. Reproducing This Analysis

```bash
# fetch a version
curl -sSL -o pkg.nupkg \
  "https://api.nuget.org/v3-flatcontainer/bannerlord.referenceassemblies.core/1.4.8.119303/bannerlord.referenceassemblies.core.1.4.8.119303.nupkg"
unzip -q pkg.nupkg -d pkg

# extract API surface and diff two versions
python3 tools/apiscan/cli_meta.py surface pkg/ref/net472/TaleWorlds.CampaignSystem.dll | sort -u > a.txt
python3 tools/apiscan/cli_meta.py surface other/TaleWorlds.CampaignSystem.dll      | sort -u > b.txt
comm -23 a.txt b.txt   # removed
comm -13 a.txt b.txt   # added
```
