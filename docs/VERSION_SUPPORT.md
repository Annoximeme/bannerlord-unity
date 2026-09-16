# Version Support

**Goals 1–3 and 25.**

---

## 1. Status: MEASURED — target pinned

**The installed version has been measured** (2026-09-16) and is recorded in §6.

| | |
|---|---|
| **Bannerlord** | **`v1.4.8`** (changeset `119303`, LIKELY), public/live branch, **stable** |
| **War Sails** | **installed** — `NavalDLC` module `v1.2.8` |
| Steam buildid | `24573425` |
| Modules | 24 total — 9 official, **15 third-party** (see §6 and RISK-16) |

The Phase 0 audit was conducted against `Bannerlord.ReferenceAssemblies.* 1.4.8.119303`, which **matches this installation exactly** — see §6. Every `VERIFIED` finding in the audit applies to it.

This document also records the version landscape (§2), a measured cross-version API-change analysis (§3–§4), and the detection procedure used (§5, §5a).

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

## 5a. Automated Collector (preferred)

`tools/apiscan/collect_install_report.py` performs the whole of §5 from the filesystem, plus the Steam branch check, without needing the game running or any .NET toolchain. **Run it on the machine where Bannerlord is installed:**

```
python tools\apiscan\collect_install_report.py "G:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
```

It reports:

| Signal | Source |
|---|---|
| Resolved game version | `Modules/Native/SubModule.xml` `<Version>`, cross-checked against `AssemblyFileVersionAttribute` on the shipped assemblies |
| Version-prefix channel | leading `v`/`e`/`b` per `ApplicationVersion.GetPrefix` |
| Steam buildid + branch | `steamapps/appmanifest_261550.acf` (`buildid`, `BetaKey`). A `BetaKey` of `public`, `none`, `default` or empty denotes the live branch, **not** a beta opt-in — only any other value is treated as beta. |
| War Sails installed + version | presence of the `NavalDLC` module and `NavalDLC.dll` |
| Full module set | every `Modules/*/SubModule.xml` with versions and dependencies |
| Real API surface | diffable dumps of the installed assemblies, for pinning and drift detection |

**Stable vs beta uses two independent signals** (Steam branch key and version prefix). They can legitimately disagree — a beta branch may ship a `v`-prefixed build — so the collector reports both and flags a conflict rather than picking one. The authoritative value remains the in-game `ApplicationVersion.ApplicationVersionType`; confirm against the main-menu version string before pinning.

Output is text and JSON only. **Do not commit the game assemblies** — they are proprietary.

## 6. Pinned Target — MEASURED 2026-09-16

Collected with `tools/apiscan/collect_install_report.py` on the install machine.

| Field | Value | Confidence |
|---|---|---|
| Install path | `G:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord` | VERIFIED |
| **Bannerlord version** | **`v1.4.8`** | **VERIFIED** |
| **Changeset** | **`119303`** — `1.4.8.119303` is the *only* published 1.4.8.x build | **LIKELY** (module XML reports `v1.4.8` without changeset) |
| **Branch** | **Public / live — not a beta opt-in** (`BetaKey "public"`) | **VERIFIED** |
| Branch assessment | `STABLE (public branch, Release-prefixed version)` | VERIFIED |
| Steam `buildid` | `24573425` | VERIFIED |
| **War Sails installed** | **YES — `NavalDLC` module present** | **VERIFIED** |
| **War Sails module version** | **`v1.2.8`** — an *independent* version line, not the game version | **VERIFIED** |
| Total modules | 24 (9 official + 15 third-party) | VERIFIED |

### Official modules (9)

`Native` `SandBox` `SandBoxCore` `StoryMode` `CustomBattle` `Multiplayer` `BirthAndDeath` `FastMode` — all `v1.4.8` — plus **`NavalDLC` `v1.2.8`**.

> **The DLC versions independently of the base game.** `NavalDLC` reports `v1.2.8` while every base module reports `v1.4.8`. Note the NuGet reference-assembly packages are keyed by *game* version (`bannerlord.referenceassemblies.navaldlc.1.4.8.119303`), **not** by DLC version — `1.2.8.31530` in that feed is an unrelated old game build. Do not conflate the two numbering schemes when pinning.

### Third-party modules present (15)

| Module | Version | Category |
|---|---|---|
| `Bannerlord.Harmony` | `v2.4.2.248` | Infrastructure — patching framework |
| `Bannerlord.ButterLib` | `v2.12.0` | Infrastructure |
| `Bannerlord.UIExtenderEx` | `v2.13.3` | Infrastructure — UI |
| `Bannerlord.MBOptionScreen` | `v5.12.3` | Infrastructure — settings (MCM) |
| `Bannerlord.Diplomacy` | `v1.5.2` | **Campaign state — high risk** |
| `ImprovedGarrisons` | `v4.2.0.7` | **Campaign state — high risk** |
| `RaiseYourBanner` | `v16.1.7` | **Campaign state — high risk** |
| `DisableCompanionDonations` | `v1.0.0` | **Campaign state** |
| `NoWaterEscape` | `v1.0.0` | **Campaign/mission — naval-adjacent** |
| `RTSCamera` | `v5.4.16` | Mission layer |
| `RTSCamera.CommandSystem` | `v5.4.16` | Mission layer |
| `DismembermentPlus` | `v2.0.8.8` | Mission layer |
| `UnblockableThrust` | `v1.1.3` | Mission layer |
| `BannerFix` | `v3.3.6` | Cosmetic |
| `AchievementUnblocker` | `v1.1.1` | Cosmetic |

See **RISK-16** in `docs/RISK_REGISTER.md`. `Bannerlord.Harmony` being present is good news — it confirms the mitigation path for RISK-06 is available.

### ✅ The Phase 0 audit was performed against the correct version

The audit used `Bannerlord.ReferenceAssemblies.* 1.4.8.119303`. The collector's type and surface counts from the **real installed assemblies** match that baseline exactly across all 11 assemblies:

| Assembly | Types (install) | Surface entries (install) | Phase 0 baseline |
|---|---|---|---|
| `TaleWorlds.CampaignSystem.dll` | 2306 | **44,655** | **44,655** ✅ |
| `NavalDLC.dll` | 692 | **13,779** | **13,779** ✅ |
| `TaleWorlds.MountAndBlade.dll` | 1738 | 30,778 | 30,778 ✅ |
| `SandBox.dll` | 594 | 9,091 | 9,091 ✅ |
| `TaleWorlds.Core.dll` | 323 | 6,660 | 6,660 ✅ |
| `TaleWorlds.Library.dll` | 256 | 3,431 | 3,431 ✅ |
| `TaleWorlds.SaveSystem.dll` | 135 | 1,725 | 1,725 ✅ |
| `TaleWorlds.Localization.dll` | 103 | 1,504 | 1,504 ✅ |
| `TaleWorlds.Network.dll` | 66 | 753 | 753 ✅ |
| `TaleWorlds.ObjectSystem.dll` | 27 | 325 | 325 ✅ |
| `TaleWorlds.ModuleManager.dll` | 19 | 238 | 238 ✅ |

**Every `VERIFIED` finding in the Phase 0 audit therefore applies to this exact installation.**

> Matching counts are strong evidence, not proof of identical content. To upgrade this from *consistent with* to *byte-identical*, commit `docs/install-report/surface/` and the baseline can be diffed member-by-member. Tracked as TD10.

### Note: assembly file versions are not a version discriminator

Every installed assembly reports `AssemblyFileVersion = 1.0.0.0`, exactly as the reference assemblies do. TaleWorlds normalises these attributes, so the collector's assembly-version cross-check adds nothing in practice — **module `SubModule.xml` versions and the Steam manifest are the usable signals.**

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
