# CoopNetworkProbe

**Purpose:** answer RISK-02 — is `TaleWorlds.MountAndBlade.GameNetwork` usable from inside a
running singleplayer `Campaign`, or does this project need to build its own transport
(`TaleWorlds.Network.TcpSocket` / raw sockets)? Procedure specified in
`docs/NETWORK_PROTOCOL.md` §2; this is Phase 1 step 1.4 / Blocked-item B3 in
`PROJECT_STATUS.md`.

This is a **research probe, not a game feature**. It does not persist state, does not touch
save data, does not add gameplay, and is not meant to be kept installed afterwards. It only
calls the exact `GameNetwork` members `docs/NETWORK_PROTOCOL.md` §1 already verified exist,
in the order §2 specifies, and logs the outcome of each call.

## What it does

On `OnCampaignStart` (fired for both a new campaign and a loaded save):

1. Re-verifies the running game is the pinned target (`Native v1.4.8`) via
   `ModuleHelper.GetActiveModules()` — refuses to run the experiment on a mismatch
   (CLAUDE.md §2 hard version gate). This also verifies TD4 (that entry point was
   previously "illustrative, not verified").
2. Logs `GameNetwork.IsSessionActive` / `.IsMultiplayer` / `.MultiplayerDisabled` /
   `.IsServer` / `.IsClient` as a baseline.
3. Attempts, each wrapped so one failure doesn't abort the rest:
   - `GameNetwork.Initialize(IGameNetworkHandler)`
   - `GameNetwork.PreStartMultiplayerOnServer()`
   - `GameNetwork.StartMultiplayerOnServer(7773)`
   - `GameNetwork.AddRemoveMessageHandlers(RegisterMode.Add)`
   - `GameNetwork.BeginBroadcastModuleEvent()` / `EndBroadcastModuleEvent(...)` (loopback)
   Logs the network state again after each step.
4. Over the next ~25 seconds of `OnApplicationTick`, logs `Campaign.Current != null` and
   `Campaign.CurrentTime` five times, to confirm the campaign keeps ticking normally
   afterwards (§2 step 4).

All output goes to **`Documents\Mount and Blade II Bannerlord\CoopNetworkProbe\network-probe.log`**
(append-only, flushed per line) and mirrored to the game's console/log via `Console.WriteLine`.

## Why the API calls look like that

Every member name, signature, and nested-type path here was read directly from
`Bannerlord.ReferenceAssemblies.Core 1.4.8.119303` metadata with `tools/apiscan/cli_meta.py`
before being written — not guessed (CLAUDE.md §1). In particular, checking every
`AddRemoveMessageHandlers` override in `TaleWorlds.MountAndBlade.dll` turned up a
structural finding worth carrying into `RISK-02`/`RISK-03`: **every real handler-registration
override site is on a `Mission`/`MissionBehavior`-scoped type** (`MissionNetworkComponent`,
`MissionLobbyComponent`, etc., taking a `NetworkMessageHandlerRegistererContainer`); there is
no campaign-level (non-`Mission`) type in the assembly that receives this callback. Only the
bare static `GameNetwork.AddRemoveMessageHandlers(RegisterMode)` exists outside a `Mission`,
which this probe calls directly. This is `VERIFIED` from metadata alone — it doesn't need a
running game — and it reinforces the case for the `ICoopTransport` abstraction already chosen
over binding straight to `GameNetwork`.

## Build

```powershell
cd tools\network-probe
dotnet build
```

Restores `Bannerlord.ReferenceAssemblies.Core` at the pinned version from NuGet — no game
DLL is copied into the repo (`.gitignore` still excludes `*.dll`; `bin/`/`obj/` are ignored
too). Output: `bin\Debug\CoopNetworkProbe.dll`.

## Deploy (manual — do this yourself, or ask the session to)

```powershell
$modDir = "G:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\CoopNetworkProbe"
New-Item -ItemType Directory -Force "$modDir\bin\Win64_Shipping_Client" | Out-Null
Copy-Item SubModule.xml "$modDir\SubModule.xml"
Copy-Item bin\Debug\CoopNetworkProbe.dll "$modDir\bin\Win64_Shipping_Client\CoopNetworkProbe.dll"
```

## Run with the RISK-16 clean dev profile

The project's clean-dev-profile decision (`docs/RISK_REGISTER.md` RISK-16) says development
and testing use **official modules + `Bannerlord.Harmony` only**. For this run that means:
official modules, `Bannerlord.Harmony`, and `CoopNetworkProbe` selected — the other 14
third-party mods (`Bannerlord.Diplomacy`, `Bannerlord.ButterLib`, `Bannerlord.MBOptionScreen`,
`Bannerlord.UIExtenderEx`, `ImprovedGarrisons`, `RaiseYourBanner`,
`DisableCompanionDonations`, `NoWaterEscape`, `RTSCamera`, `RTSCamera.CommandSystem`,
`DismembermentPlus`, `UnblockableThrust`, `BannerFix`, `AchievementUnblocker`) deselected for
this launch only.

Easiest path: open the normal TaleWorlds/BLSE launcher, uncheck those 14, check
`CoopNetworkProbe`, Play. (Your existing selection lives in
`Documents\Mount and Blade II Bannerlord\Configs\LauncherData.xml` as one
`<UserModData><Id>…</Id><IsSelected>true|false</IsSelected></UserModData>` block per mod —
untouched by this probe; re-check your usual mods afterwards.)

Then: start a new campaign, or load a save — `OnCampaignStart` fires either way. Let it sit
for at least 30 seconds so all five tick-check log lines land.

## After running

Report back (or open) `Documents\Mount and Blade II Bannerlord\CoopNetworkProbe\network-probe.log`.
Its content resolves RISK-02: whether each `GameNetwork` call in the experiment logged `STEP
OK` or `STEP THREW`, whether `IsSessionActive` ever became `true`, and whether the campaign
kept ticking afterwards. That answer gets written into `docs/RISK_REGISTER.md` RISK-02 (and
`docs/NETWORK_PROTOCOL.md` §2) with `VERIFIED` confidence, replacing the current
`UNCONFIRMED`.

## Cleanup

This module is disposable. Once RISK-02 is answered, delete the deployed
`Modules\CoopNetworkProbe\` folder from the game install — it has no reason to persist past
the experiment.
