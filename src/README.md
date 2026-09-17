# Source — Phase 1 foundations

Implements `docs/ROADMAP.md` Phase 1 step 1.2 (module skeleton + hard version gate) and
follows the assembly layout `docs/ARCHITECTURE.md` §9 mandates.

| Project | References | Purpose |
|---|---|---|
| `Coop.Core` | none | Game-agnostic logic. No `TaleWorlds.*` reference is ever permitted here. |
| `Coop.Core.Tests` | `Coop.Core`, xUnit | Unit tests for the game-agnostic layer. |
| `Coop.GameInterface` | `Coop.Core`, `Bannerlord.ReferenceAssemblies.Core` | The actual Bannerlord module: `SubModule`, save-type registration, campaign behaviors. |

`Coop.Server`, `Coop.Client`, `Coop.GameInterface.Naval`, and the integration/network/save-load
test projects arrive with the steps that need them (1.6 onward) — not created ahead of use.

## What's here

- `Coop.Core/Versioning/GameVersion.cs` + `VersionGate.cs` — a game-agnostic version triple
  and a gate that accepts only an exact match. Unit-tested in `Coop.Core.Tests`.
- `Coop.GameInterface/SubModule.cs` — on load, reads the *running* game's `Native` module
  version via `ModuleHelper.GetActiveModules()` (not assumed from disk) and refuses to
  register anything if it doesn't match the pinned version, logging why via `Debug.Print`
  and an in-game `InformationManager.DisplayMessage`.
- `Coop.GameInterface/CoopSaveableTypeDefiner.cs` — stub, defines nothing yet. Verified by
  decompiling `TaleWorlds.SaveSystem.SaveManager`/`DefinitionContext` against the real
  install: a `SaveableTypeDefiner` subclass needs no manual registration — every assembly
  that references `TaleWorlds.SaveSystem.dll` is scanned automatically and every non-abstract
  subclass found is instantiated via `Activator.CreateInstance()`. `SaveBaseId` is provisional;
  real id-range allocation is Phase 1.9 work, once real types are defined here.
- `Coop.GameInterface/CampaignBehaviors/CoopStubCampaignBehavior.cs` — empty
  `CampaignBehaviorBase`, registered from `SubModule.InitializeGameStarter` only if the
  version gate passed. Proves the registration path works; real behaviors arrive with the
  systems they belong to.

## Build

```powershell
cd src
dotnet build BannerlordUnity.sln
```

## Test

```powershell
cd src
dotnet test Coop.Core.Tests\Coop.Core.Tests.csproj
```

Only `Coop.Core` has tests so far — it's the only project with game-agnostic logic to unit
test. `Coop.GameInterface` needs a running game to verify (see below); network and save/load
tests arrive with the systems they test (1.7, 1.9).

## Deploy and verify in-game

```powershell
$modDir = "G:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\BannerlordUnity"
New-Item -ItemType Directory -Force "$modDir\bin\Win64_Shipping_Client" | Out-Null
Copy-Item Coop.GameInterface\SubModule.xml "$modDir\SubModule.xml"
Copy-Item Coop.GameInterface\bin\Debug\Coop.GameInterface.dll "$modDir\bin\Win64_Shipping_Client\Coop.GameInterface.dll"
```

Select `BannerlordUnity` in the launcher alongside the official modules (RISK-16 clean
profile) and start the game. Unlike `tools/network-probe`, **reaching the main menu is
enough** — `OnSubModuleLoad` fires at module load, well before any campaign exists, and
nothing here touches `GameNetwork` or otherwise risks a repeat of the RISK-02 crash.

**Exit criterion (docs/ROADMAP.md 1.2):**
- On the pinned version: no refusal message, game reaches the main menu normally.
- On any other version (there is no other version installed to test this against right
  now): the `[Bannerlord: Unity] Refusing to activate — running vX.Y.Z but this build is
  pinned to v1.4.8` message should appear, and the campaign behavior must **not** register.

Remove `Modules\BannerlordUnity\` when done testing — like `tools/network-probe`, this is
build output, not something to keep manually deployed between sessions.
