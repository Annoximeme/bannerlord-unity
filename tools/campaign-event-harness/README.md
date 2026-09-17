# CampaignEventHarness

**Purpose:** Phase 1.5 (`docs/ROADMAP.md`) — subscribe to all 277 `CampaignEvents` and log
firing order, frequency, and re-entrancy, to convert RISK-04 (campaign non-determinism &
event ordering) from `UNCONFIRMED` toward measured.

This is a **research probe, not a game feature** — passive logging only, no gameplay change,
not meant to be kept installed afterwards. Unlike `tools/network-probe`, it does not touch
`GameNetwork` or force any engine state change, so there is no known crash risk; it's read-only
observation of events that fire during normal play.

## What it does

Every public static property on `TaleWorlds.CampaignSystem.CampaignEvents` (all 277 — verified
count via reflection at startup, not assumed) gets a listener attached generically: each
property uniformly exposes `AddNonSerializedListener(object, Action<...>)` for 0 to 7 generic
arguments (confirmed against the real install with `tools/apiscan` before writing this), so one
piece of code builds the right delegate type for each at runtime via `System.Linq.Expressions`
rather than 277 hand-written signatures.

Each firing logs: a global monotonic sequence number (reconstructs cross-event ordering), the
event name, a best-effort summary of its arguments (primitives/enums/strings shown directly;
complex game objects shown only by type name — never `ToString()`'d, since that could run
arbitrary game code on a partially-constructed object), and whether the same event is already
mid-fire on this thread (`REENTRANT`). Every ~15 in-game minutes (`QuarterHourlyTickEvent`, one
of the 277, doubles as a periodic checkpoint) a frequency summary of the top 20 most-fired
events is written.

Output: `Documents\Mount and Blade II Bannerlord\CampaignEventHarness\event-order.log`
(append-only, flushed per line), mirrored to the game's console/log.

## Known limitation

`docs/ROADMAP.md` 1.5 specifies "run the same save on two machines and diff" to directly
measure non-determinism. Only one machine with the game installed is available to this
project right now, so that comparison isn't possible yet. What this harness *can* still
establish from a single run — real ordering, frequency, and re-entrancy data, replacing pure
speculation — is worth having now; the cross-machine diff is a follow-up once a second machine
is available.

## Build

```powershell
cd tools\campaign-event-harness
dotnet build
```

## Deploy

```powershell
$modDir = "G:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\CampaignEventHarness"
New-Item -ItemType Directory -Force "$modDir\bin\Win64_Shipping_Client" | Out-Null
Copy-Item SubModule.xml "$modDir\SubModule.xml"
Copy-Item bin\Debug\*.dll "$modDir\bin\Win64_Shipping_Client\"
```

(No other project references here, unlike `src/Coop.GameInterface` — just the one DLL, but the
wildcard copy is still the safer habit.)

## Run

RISK-16 clean profile: official modules + `Bannerlord.Harmony` (if present) + this module.
Start or load a campaign and play normally for a bounded session — traveling, entering a
settlement, maybe a battle, letting time pass — for something like 15–30 real minutes. Longer
sessions produce a bigger log with more coverage but take more time to review; this doesn't
need to be exhaustive on the first pass.

## After running

Report back (or open) `Documents\Mount and Blade II Bannerlord\CampaignEventHarness\event-order.log`.
Its content feeds directly into `docs/RISK_REGISTER.md` RISK-04 and `docs/ARCHITECTURE.md` A2
("whether campaign AI ticks can run server-only with clients fully passive") with real ordering
and frequency data instead of the current `UNCONFIRMED` status.

## Cleanup

Disposable, like `tools/network-probe`. Once RISK-04 has enough data, remove
`Modules\CampaignEventHarness\` from the install — no reason to keep it running after.
