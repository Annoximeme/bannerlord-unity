# Project Status

**Last updated:** 2026-09-17

---

## Version Block

**Measured on the install machine 2026-09-16** via `tools/apiscan/collect_install_report.py`.

| Field | Value | Confidence |
|---|---|---|
| **Current Bannerlord version** | **`v1.4.8`** (changeset `119303`) | VERIFIED / changeset LIKELY |
| **Current War Sails version** | **`v1.2.8`** (`NavalDLC` module — independent version line) | VERIFIED |
| **Branch (stable / beta)** | **STABLE — Steam public/live branch, `BetaKey "public"`** | VERIFIED |
| Steam `buildid` | `24573425` | VERIFIED |
| Install path | `G:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord` | VERIFIED |
| Modules installed | 24 — 9 official, **15 third-party** | VERIFIED |
| **Mod version** | `v0.0.1` (`BannerlordUnity` module skeleton; no gameplay features) | VERIFIED |
| **Current phase** | **Phase 1 — Foundations: step 1.2 in progress** | VERIFIED |
| Audit conducted against | `1.4.8.119303` reference assemblies — **matches the install exactly** | VERIFIED |

✅ **The Phase 0 audit targeted the correct version.** Type and API-surface counts from the real installed assemblies match the audit baseline across all 11 assemblies (`TaleWorlds.CampaignSystem.dll` 44,655 surface entries; `NavalDLC.dll` 13,779 — both exact). Every `VERIFIED` audit finding applies to this installation. Detail in `docs/VERSION_SUPPORT.md` §6.

⚠ **15 third-party mods are installed**, four of which mutate campaign state (`Bannerlord.Diplomacy`, `ImprovedGarrisons`, `RaiseYourBanner`, `DisableCompanionDonations`) and one of which is naval-adjacent (`NoWaterEscape`). These are a direct desync and save-schema risk for co-op — see **RISK-16**. `Bannerlord.Harmony v2.4.2.248` being present confirms the RISK-06 mitigation path is available.

---

## Completed Work

**Phase 0 — Technical Audit (complete).** All 30 audit goals addressed; 28 fully, 2 (exact installed versions) blocked on environment and answered with a landscape analysis plus a detection procedure.

| Deliverable | Contents |
|---|---|
| `docs/INITIAL_TECHNICAL_AUDIT.md` | Full audit, all 30 goals, evidence tables, confidence levels |
| `docs/ARCHITECTURE.md` | Topology, layers, authority rules, service interfaces, idempotency |
| `docs/WARSAILS_ARCHITECTURE.md` | Naval API map incl. water navigation and terrain classification |
| `docs/SYNCHRONIZATION_MODEL.md` | Ownership tiers, metadata-driven authority, ship-identity design |
| `docs/NETWORK_PROTOCOL.md` | Transport surface, channels, wire format, lifecycle, reconnect, restart |
| `docs/SAVE_FORMAT.md` | Persistence, save-safety requirements, verified per-type persisted state |
| `docs/RISK_REGISTER.md` | 16 scored risks with evidence and confidence |
| `docs/VERSION_SUPPORT.md` | Version landscape, measured API churn, detection procedure |
| `docs/ROADMAP.md` | Phases 0.5 → 5 with exit criteria |
| `docs/LOCAL_SETUP.md` | Local dev environment setup; unblocks B2, B6–B9 |
| `docs/HANDOFF.md` | Cloud → local session handoff; start here in a new session |
| `tools/apiscan/` | ECMA-335 metadata reader — makes every API claim reproducible |
| `docs/evidence/` | Raw API dumps and cross-version diffs |

**Research method.** With no install available, the audit obtained the **official TaleWorlds assemblies** as published reference assemblies (`Bannerlord.ReferenceAssemblies.*`, including `NavalDLC`) and parsed their CLI metadata directly. These retain private fields and custom attributes, so `[SaveableField]`/`[SaveableProperty]`/`[CachedData]` are directly readable. Method **bodies** are stripped, so every claim about API *shape* is VERIFIED and every claim about *ordering or control flow* is marked UNCONFIRMED. This satisfies `CLAUDE.md` research priority #1 as closely as the environment permits; items requiring a live install are listed under **Blocked Work**.

### Key verified findings

1. **The naval data model is in the base game, not the DLC.** `TaleWorlds.CampaignSystem.Naval.Ship`, `ChangeShipOwnerAction`, `DestroyShipAction`, `PartyBase.Ships`, `MapEvent.IsNavalMapEvent`, `BattleTypes.BlockadeBattle` all live in `TaleWorlds.CampaignSystem.dll`. Ship ownership can be synchronized without a DLC dependency.
2. **`Ship` has no network identity** — derives from `System.Object`, no `MBGUID`, no `StringId`, and its version counter is not persisted. Largest technical risk (RISK-01).
3. **The engine declares what to replicate.** `[SaveableField]`/`[SaveableProperty]` = authoritative; `[CachedData]` = derived, never replicate. Machine-readable, enforced mechanically.
4. **Water navigation is base-game terrain data.** `CoastalSea`, `OpenSea`, `River`, `NonNavigableRiver` are `TaleWorlds.Core.TerrainType` members, queried via `IMapScene.GetTerrainTypeAtPosition`. Static map data — must **not** be replicated. Wind and storms **must** be.
5. **`GameNetwork` exposes a complete messaging API**, but its usability inside a campaign session is unverifiable from metadata (RISK-02).
6. **The incumbent co-op mod does not support War Sails** — planned only; its README instructs disabling the DLC; its War Sails epic (#3060) and three `[WARSAILS]` issues are open and unstarted.

---

## Current Work

**RISK-17 — a real save became unloadable. LIKELY not our mod, follow-up test with `Best.sav`.** Loading an unrelated, much older save worked fine both with `BannerlordUnity`/`CampaignEventHarness` enabled and disabled, and separately tolerated a large mismatch against several other mods that save was originally made with — showing the load pipeline is healthy and module mismatches aren't what break a load here. That makes `save016.sav`'s identical failure with our mod both present and absent real evidence against it being the cause, not confounded evidence. Current best explanation: a base-game/War-Sails bug specific to that save's unusually large naval content (977 ships, 337 parties). `save016.sav` itself is very likely unrecoverable through anything this project can fix. Fixed a real defensive gap in `ShipIdentityCampaignBehavior.SyncData` regardless (its load path wasn't fully exception-safe) and redeployed. Full evidence: `RISK_REGISTER.md` RISK-17.

### Recently resolved

**B8 — siege stage transition sequence. Resolved — there is no stage state machine.** `MapEvent.CheckSiegeStageChange()`, the method the Phase 0 audit assumed was central to this, turns out to be dead code in v1.4.8 — it computes a value and does nothing with it, which is why removing it entirely in 1.5.3-beta broke nothing. What actually stands in for "stage" is `SiegeEvent.GetCurrentBattleType()`, read live off whichever `MapEvent.EventType` currently exists (or plain `Siege` if none does) — not a stored state, and not a machine that transitions on its own. Transitions are just new `MapEvent`s getting created, triggered the same way B7's naval decision is: player menu consequences (`MenuHelper`) or AI decision loops (`AiPartyThinkBehavior`). Full finding: `ARCHITECTURE.md` §6, `VERSION_SUPPORT.md` §4.2.

**B7 — `MapEvent` → naval mission launch path. Fully resolved.** The earlier partial pass had searched five assemblies and found the decorator seam but not the actual trigger; it turned out to be in a file not yet checked, `TaleWorlds.CampaignSystem.Helpers.MenuHelper`. `MenuHelper.EncounterAttackConsequence` — the "Attack" game-menu option's consequence callback — is where naval-vs-land is actually decided, branching on `PlayerEncounter.IsNavalEncounter()` for field battles and `MapEventHelper.GetRaidContext`'s per-side sea/land classification for village raids. Real consequence for our own design: this decision lives in a client-local menu handler, not a server-side campaign event — our own naval-battle-hosting logic (Phase 4) will need to hook or reimplement at this layer. Full chain in `ARCHITECTURE.md` §6, decision record in `RISK_REGISTER.md` RISK-03.

**Phase 1 step 1.9 — persistence, reconnect & save safety. Core built; awaiting a live save/reload to close RISK-15.** `src/Coop.Core/Persistence/`: `SaveGenerationStore` (atomic write-then-rename, SHA-256 checksum, corrupted-generation fallback, N-generation pruning) and `SchemaMigrationChain` (`vN → vN+1`, refuses an unknown-newer schema) implement `CLAUDE.md` §7 / `SAVE_FORMAT.md` §5, fully unit-tested against a real temp directory — 23 new tests, 94 total, all passing. `CoopStateSnapshot`/`CoopStateBlobCodecV1` hand-encode the ship-identity state.

`src/Coop.GameInterface/Identity/ShipIdentityCampaignBehavior.SyncData` now actually persists the ship registry through the real `IDataStore` (a Base64 blob of the codec's bytes), and on load defers applying it until `OnGameLoadFinishedEvent`, where it re-locates each owner by `MBGUID` and reruns `ShipIdentityRebinder` against the just-reloaded ships — logging mismatches to `Documents\Mount and Blade II Bannerlord\Coop.GameInterface\ship-identity.log`. **This is the actual RISK-15 round-trip test, wired and deployed** — what's missing is someone saving and reloading a real campaign with a ship. Builds clean (net472 and net8.0), redeployed to `Modules\BannerlordUnity\`.

**Deferred on purpose, not silently dropped:** `SaveGenerationStore` has no live save trigger yet — that needs a dedicated-server host, which is Phase 1.8. Player-identity bindings have a tested codec path but nothing to populate them — no join flow exists before a real second client can connect (also 1.8, and your friend being available soon is exactly what unblocks that). The full `SAVE_FORMAT.md` §6 save-compatibility matrix (toggling `NavalDLC` on/off across saves, cross-version, vanilla↔our-save) needs many manual save/reload cycles and hasn't been run — RISK-13 stays open until it is.

### Recently resolved

**1.7 — transport + wire protocol + idempotent consequence ledger. Proven with real sockets.** `src/Coop.Core/Network/` — `TcpCoopTransport` (plain `System.Net.Sockets`, the transport RISK-02 said to build) and `LoopbackTransport` both implement `ICoopTransport`; `Frame` implements the exact `[u8 channel][u16 msgType][u32 seq][varint len][payload]` wire format from `NETWORK_PROTOCOL.md` §4 and is self-delimiting. `Channel` covers C0–C5; only C0 has real message types yet (C1–C5 arrive with the systems that use them). `Network/Handshake/`: `HandshakeValidator` implements the version/DLC negotiation table from `NETWORK_PROTOCOL.md` §5 exactly, as a pure unit-tested function (full policy matrix covered, including the asymmetric DLC-mismatch cases). `ConsequenceId`/`ConsequenceLedger` implement `ARCHITECTURE.md` §10's exactly-once guard. 34 new unit tests, **plus two integration tests using real TCP sockets on localhost** — not loopback stubs — that are the literal exit-criterion proof: a real client and server exchange a typed `Frame`, and a deliberately duplicated frame (same bytes sent twice) is shown to arrive twice at the transport layer but get applied exactly once through the ledger. 71 `Coop.Core.Tests` total, all passing, both net472 and net8.0 builds clean (needed `System.Memory` — the standard compatibility package — for `ReadOnlySpan<byte>` on net472; the SDK copies its support DLLs into the deploy folder automatically, confirmed present after redeploy). Also added `tools/apiscan/cli_meta.py manifest`, generating the `[Saveable*]`/`[CachedData]` replicated-state manifest from real metadata (found and fixed a bug in the process: attribute names come back fully-qualified, not short names) — run against `Ship`/`MobileParty`/`PartyBase`, committed to `docs/evidence/replication-manifest/`.

**1.6 — identity layer (mitigates RISK-01; resolves ARCHITECTURE.md A5).** `src/Coop.Core/Identity/` — `EngineObjectId`, `CoopShipId`/`CoopShipIdAllocator`, `ShipFingerprint`/`ShipIdentityRebinder` (`SYNCHRONIZATION_MODEL.md` §4.3), `PartyBaseId`/`OwnerKind` (§4.4). `src/Coop.GameInterface/Identity/` adapts these to real `Ship`/`MBObjectBase`/`PartyBase`: `CoopShipRegistry`, `ShipIdentityCampaignBehavior` (assigns and logs an id for every ship, every in-game day). RISK-06 decided: publicizer, when actually needed — this layer needed zero internal-member access. Redeployed to `Modules\BannerlordUnity\`; still awaiting a live verification run (check `Documents\Mount and Blade II Bannerlord\Coop.GameInterface\ship-identity.log` after playing). Cross-session persistence and the real save/load round-trip test are Phase 1.9.

**1.5 — campaign event & determinism harness. Built, deployed, awaiting a play session (unchanged since last update).** `tools/campaign-event-harness/` subscribes to all 277 `CampaignEvents` generically via reflection and logs firing order, frequency, and re-entrancy. Passive observation only, no known crash risk. See `tools/campaign-event-harness/README.md`.

### Recently resolved

**1.3 — API-drift CI gate. Exit criterion proven on real GitHub Actions, not just locally.** `tools/apiscan/check_api_drift.py` diffs the pinned assemblies' surface (from the same `Bannerlord.ReferenceAssemblies.Core` package `Coop.GameInterface.csproj` restores) against a committed baseline (`docs/evidence/api-baseline/`), scoped to the six assemblies `Coop.GameInterface` references. Wired into `.github/workflows/ci.yml` alongside build+test. First real CI run caught a genuine bug immediately: `Coop.Core.Tests` targeted net472, which needs Mono to execute — absent on the Linux runners — so `dotnet test` failed before running a single test. Fixed by multi-targeting `Coop.Core` (`net472;net8.0`; `Coop.GameInterface` still resolves the net472 build, confirmed unaffected) and moving the test project to net8.0 alone. Then proved the actual exit criterion for real: pushed a commit injecting a fake removed member into the baseline, watched the API-drift job fail on GitHub Actions (run `35168539383`) while the build/test job stayed green, then reverted (run `26cd1b7`, back to green). TD8 (no CI infrastructure) is resolved.

**1.2 — module skeleton + hard version gate. Confirmed in-game.** `src/Coop.Core` (game-agnostic `GameVersion`/`VersionGate`, unit-tested) and `src/Coop.GameInterface` (`BannerlordUnity` Bannerlord module: version gate wired to `ModuleHelper.GetActiveModules()`, `SaveableTypeDefiner` stub, stub `CampaignBehaviorBase`). First deploy crashed the game at the loading screen — cause was a missing dependency DLL in the deploy step (`Coop.Core.dll`), not the version-gate logic itself (checked by decompiling `ModuleHelper`/`ModuleInfo`). Fixed, redeployed, confirmed reaching the main menu cleanly on the pinned version.

**B3 — `GameNetwork`-in-campaign probe. Run, and it crashed the game.** `tools/network-probe/CoopNetworkProbe` was launched on the real install (clean profile, no third-party mods). Every `GameNetwork` call succeeded (`Initialize`, `PreStartMultiplayerOnServer`, `StartMultiplayerOnServer`, message handlers, loopback broadcast — all `STEP OK`, `IsSessionActive`/`IsMultiplayer`/`IsServer` all flipped `true`), the campaign kept ticking for 15+ seconds, and then the game crashed with a native access violation (`0xc0000005`, Windows Application Error, no managed exception, no BUTR report). **RISK-02 is resolved: `GameNetwork` cannot be used to retrofit multiplayer onto a running singleplayer campaign — it destabilizes the engine.** The project builds its own transport (`TaleWorlds.Network.TcpSocket` / raw sockets), which is what `NETWORK_PROTOCOL.md`'s `ICoopTransport` abstraction already assumed. No save was at risk (crash predated any autosave). Full evidence: `docs/RISK_REGISTER.md` RISK-02, `docs/NETWORK_PROTOCOL.md` §2.

**Action needed:** remove `Modules\CoopNetworkProbe\` from the local install — its job is done and it forces the exact crash above every time it runs, so it must not stay enabled.

**B6 resolved, B7 partially advanced** (same session, while B3 was pending a run). `ilspycmd` is now installed and verified against the real install. `Ship.VersionNo`'s exact mechanism is decompiled and documented (`SYNCHRONIZATION_MODEL.md` §4.3, `WARSAILS_ARCHITECTURE.md`). For B7, traced the campaign→mission naval launch path as far as confirming it's a decorator (`NavalMissionManager` wrapping `Campaign.Current.CampaignMissionManager`, installed in `OnAfterGameInitializationFinished`) — a clean seam for our own mod — but the exact call site that decides "this encounter is naval" is still unfound across five searched assemblies (`RISK_REGISTER.md` RISK-03). B8 (siege stage transitions) hasn't been started yet; same tool, same method, doesn't need the game running.

---

## Blocked Work

| # | Blocked item | Blocked by | Unblocks |
|---|---|---|---|
| ~~B1~~ | ~~Licensing decision (RISK-00)~~ | **RESOLVED 2026-09-17** — owner chose clean-room; see RISK_REGISTER.md | All implementation |
| ~~B2~~ | ~~Version pin + session version check~~ | **RESOLVED 2026-09-16** — measured; see Version Block | — |
| ~~B3~~ | ~~`GameNetwork`-in-campaign probe (RISK-02)~~ | **RESOLVED 2026-09-17** — crashes the engine; own transport required. See Current Work above and `RISK_REGISTER.md` RISK-02 | — |
| B4 | Campaign determinism / event ordering (RISK-04) | No install | Sync model |
| B5 | `Ship` ↔ `MissionShip` binding lifetime (RISK-03) | No install | Naval capture |
| ~~B6~~ | ~~Which mutations bump `Ship.VersionNo`~~ | **RESOLVED 2026-09-17** — `ilspycmd` against the real install; see `SYNCHRONIZATION_MODEL.md` §4.3 | — |
| ~~B7~~ | ~~`MapEvent` → naval mission launch path~~ | **RESOLVED 2026-09-17** — full chain found in `Helpers.MenuHelper.EncounterAttackConsequence`; see `ARCHITECTURE.md` §6, `RISK_REGISTER.md` RISK-03 | — |
| ~~B8~~ | ~~Siege stage transition sequence~~ | **RESOLVED 2026-09-17** — no stage state machine exists; see `ARCHITECTURE.md` §6 | — |
| B9 | Save compat with/without War Sails (RISK-13) | No install | Persistence |
| B10 | `MissionShip` authority & physics determinism (RISK-12) | No install | Naval battles |
| B11 | Official TaleWorlds War Sails modding documentation | Not located in this session | Naval detail |

> **The install was the remaining real blocker; it is now available.** B3–B10 are testable now that the target machine has Bannerlord + War Sails.

### Decisions made by the project owner (2026-09-17)

**1. Licensing (RISK-00) — RESOLVED: clean-room.** `CLAUDE.md` instructs studying `Bannerlord-Coop-Team/BannerlordCoop` as a technical reference. That repository changed licence on **2026-06-17** from MIT to source-available, explicitly prohibiting use of its source "to create, contribute to, improve, support, or maintain a competing… co-op… mod." The owner chose to continue deriving architecture and implementation **exclusively from TaleWorlds' own API surface** — the same discipline the Phase 0 audit already followed. The incumbent project may still be studied for public facts (existence, README, licence, issues) but never for implementation detail to port or adapt. Full record: `docs/RISK_REGISTER.md` RISK-00.

**2. Third-party mod policy (RISK-16) — RESOLVED: clean dev profile.** Development and testing use official modules plus `Bannerlord.Harmony` only. `Bannerlord.Diplomacy`, `ImprovedGarrisons`, `RaiseYourBanner`, `DisableCompanionDonations`, and `NoWaterEscape` must be disabled in the profile used to build and test co-op systems. Full record: `docs/RISK_REGISTER.md` RISK-16.

Both decisions clear the Phase 1 gate that `docs/HANDOFF.md` set. Phase 1 work (starting with B3 / step 1.4) may now proceed.

---

## Known Bugs

No gameplay code has been written. One research tool is a known, reproducible engine crash **by design**: `tools/network-probe/CoopNetworkProbe` forces `GameNetwork` into multiplayer mode inside a live singleplayer campaign specifically to test whether that's safe — it isn't (RISK-02). If `Modules\CoopNetworkProbe\` is still deployed on any install, remove it; do not re-enable it expecting a different result.

**Not our bug, but worth knowing about:** a real save (`save016.sav`, 2026-09-17) became unloadable during the first live Phase 1.9 test — see `RISK_REGISTER.md` RISK-17. Follow-up testing (an unrelated older save loads fine both with and without our mods, and tolerates other module mismatches) points at a base-game/War-Sails bug specific to that save's huge naval content (977 ships), not `Coop.GameInterface`. A real robustness gap was found and fixed regardless (`ShipIdentityCampaignBehavior.SyncData`'s load path wasn't fully exception-safe).

---

## Technical Debt

| # | Item | Severity | Notes |
|---|---|---|---|
| ~~TD1~~ | ~~§2 session version check cannot run in a cloud session~~ | **Resolved** | Collector run on the install machine; version pinned |
| ~~TD2~~ | ~~`VERSION_SUPPORT.md` §6 pinned-target table empty~~ | **Resolved** | Filled with measured values |
| ~~TD10~~ | ~~Install surface dumps not yet committed~~ | **Resolved 2026-09-17** | Committed; re-collected and confirmed byte-identical (`git diff --stat` empty) against the committed baseline |
| TD11 | Changeset `119303` inferred from it being the only published 1.4.8.x build, not read from the game | Low | Confirm against the in-game version string |
| TD3 | Ship-id stability rests on a `LIKELY` list-ordering assumption | Medium | Fingerprint fallback designed; needs the Phase 1.9 round-trip test (RISK-15) |
| TD4 | `ModuleInfo` module-enumeration entry point is illustrative, not verified | Medium | Confirm against installed `TaleWorlds.ModuleManager.dll` |
| TD9 | Collector misclassified `BetaKey "public"` as a beta opt-in | **Fixed** | Found by real output from the install machine; `PUBLIC_BRANCH_KEYS` now excludes `public`/`none`/`default`/empty. Regression-tested for public-key, genuine-beta and no-key cases. |
| TD5 | Official TaleWorlds War Sails modding docs not consulted | Medium | Naval findings are assembly-derived only; B11 |
| ~~TD6~~ | ~~Reference assemblies lack method bodies~~ | **Resolved 2026-09-17** | `ilspycmd 8.2.0.7535` decompiles the real install's assemblies with full method bodies (`dotnet tool install -g ilspycmd --version 8.2.0.7535` — later releases 9.x–11.x fail to install, bad `DotnetToolSettings.xml` in the package as of this writing). Ordering/flow claims are now answerable, not inherently UNCONFIRMED; only claims nobody has actually decompiled yet stay UNCONFIRMED. **Decompiled output is never committed** — it reproduces TaleWorlds' own source, a different and stricter concern than the metadata-only surface dumps in `docs/evidence/`. Extract findings into our own words in the docs; `.gitignore` now blocks `*.decompiled.cs` and `/decompiled/` as a backstop. |
| TD7 | `docs/evidence/` diffs are large and uncompressed | Low | Acceptable; they are the audit trail |
| ~~TD8~~ | ~~No build, test, or CI infrastructure yet~~ | **Resolved 2026-09-17** | `.github/workflows/ci.yml` builds `src/`, runs `Coop.Core.Tests`, and runs the API-drift gate on every push/PR. |

---

## Next Tasks

**Phase 0.5 — unblock (owner):**

1. ~~Decide RISK-00 (licensing).~~ **Done 2026-09-17 — clean-room.**
2. ~~Run detection; fill the Version Block.~~ **Done 2026-09-16.**
3. ~~Decide the third-party mod policy (RISK-16).~~ **Done 2026-09-17 — clean dev profile.**
4. Confirm target player count.
5. ~~Commit `docs/install-report/` so the install surface can be diffed against the audit baseline (TD10).~~ **Done 2026-09-17 — committed and re-verified byte-identical.**

**Phase 1 — foundations (ordered; see `docs/ROADMAP.md`):**

| Step | Task |
|---|---|
| ~~1.1~~ | ~~Environment capture and version pin~~ — done |
| ~~1.2~~ | ~~Module skeleton + hard version gate~~ — done, confirmed in-game |
| ~~1.3~~ | ~~`tools/apiscan` API-drift CI gate~~ — done, exit criterion proven on real GitHub Actions runs (below) |
| ~~1.4~~ | ~~`GameNetwork`-in-campaign probe (resolves RISK-02)~~ — **done**, `GameNetwork` ruled out |
| 1.5 | Campaign event + determinism harness (resolves RISK-04) — **built, deployed, awaiting a play session** (`tools/campaign-event-harness/README.md`) |
| 1.6 | Identity layer: `MBGUID` registry + synthesized `CoopShipId` (mitigates RISK-01) — **built, unit-tested, deployed for a live verification run** |
| ~~1.7~~ | ~~Transport + wire protocol + idempotent consequence ledger~~ — done, proven with real sockets |
| 1.8 | First vertical slice: party position, two clients |
| 1.9 | Persistence, reconnect, server restart, save-safety layer — **core built and unit-tested; live round-trip awaiting a real save/reload** |
| 1.10 | Naval **campaign** state (ships, ownership, at-sea flags) — no naval missions |

Each step requires unit, integration, network and save/load tests per `CLAUDE.md`; major systems additionally require end-to-end tests.

**Explicitly not in Phase 1:** naval missions/boarding (RISK-03, RISK-08), combat sync, quests (RISK-11), sieges, armies, economy.

---

## Honest Assessment

The audit is stronger than the environment suggested it would be — reference assemblies carried enough metadata to answer most structural questions with direct evidence rather than inference, and the questions that remain are clearly flagged rather than papered over.

Three things worth stating plainly:

- **The scope is very large.** ~20 major systems. The incumbent project has 4,257 source files and years of work and still has not shipped quests, hideouts, or naval. Phase 1 is deliberately narrow.
- **Naval battles are the genuinely unknown part.** Naval *campaign* state is well understood and buildable now. Naval *missions* sit behind two unresolved risks with no prior art anywhere.
- **The licensing question is real and is yours.** It is not an engineering problem.

**Per `CLAUDE.md` §10: no gameplay functionality is claimed to work.** `src/` now exists (the Phase 1.2 module skeleton and version gate — no gameplay feature), and it is stated plainly as untested-in-game until the verification run in `src/README.md` actually happens. Nothing below `VERIFIED` has been turned into an implementation assumption anywhere in these documents.
