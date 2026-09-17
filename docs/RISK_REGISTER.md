# Risk Register

**Goals 26 & 30.** Scored **Impact × Likelihood**, both 1–5. Severity = product.
Every risk names the evidence and its confidence, so nothing here is speculation dressed as fact.

| ID | Risk | Sev | Confidence of the underlying finding |
|---|---|---|---|
| RISK-00 | Upstream licence prohibits competing co-op mods | **25** | VERIFIED |
| RISK-01 | `Ship` has no network identity | **25** | VERIFIED |
| ~~RISK-02~~ | ~~`GameNetwork` may not work in a campaign session~~ | **RESOLVED** | Crashes the engine when retrofitted onto a live singleplayer campaign — VERIFIED 2026-09-17, own transport required |
| RISK-03 | Campaign↔mission naval seam | **20** | VERIFIED (split) / UNCONFIRMED (binding) |
| RISK-04 | Campaign non-determinism & event ordering | **20** | UNCONFIRMED |
| RISK-05 | Version churn on bound APIs | **16** | VERIFIED |
| RISK-06 | Internal/private members require publicizer or Harmony | 12 | VERIFIED |
| ~~RISK-07~~ | ~~No install available for verification~~ | **RESOLVED** | Measured 2026-09-16 |
| RISK-08 | Naval mission multiplayer may be impossible | 15 | UNKNOWN |
| RISK-09 | Save-schema divergence across versions | 12 | LIKELY |
| RISK-10 | Scale/bandwidth at 8+ players | 9 | UNCONFIRMED |
| RISK-11 | Quests are not designed for multiple players | 12 | LIKELY |
| RISK-12 | Mission-layer float determinism | 12 | LIKELY |
| RISK-13 | Save compatibility with/without War Sails | 12 | UNCONFIRMED |
| RISK-14 | Scope: the requested feature set is very large | 16 | VERIFIED |
| RISK-15 | Ship id stability rests on a `LIKELY` assumption | 12 | LIKELY |
| RISK-16 | Third-party mods on the target install mutate campaign state | **16** | VERIFIED |
| RISK-17 | Real save became unloadable during Phase 1.9 live testing | **20** (if base-game) | UNCONFIRMED root cause |

---

## RISK-00 — Upstream licence prohibits competing co-op mods · **Impact 5 × Likelihood 5 = 25**

**Evidence (VERIFIED):** `LICENSE` and `NOTICE.md` in `Bannerlord-Coop-Team/BannerlordCoop`, read directly. Since **2026-06-17** the project is source-available, not MIT, and explicitly forbids using the source "to create, contribute to, improve, support, or maintain a competing Mount & Blade II: Bannerlord multiplayer, co-op, networking, synchronization, or derivative mod without prior written permission."

**Why it is severity 25:** this project *is* a competing co-op mod. The constraint is certain (likelihood 5) and legal rather than technical, so no amount of engineering removes it (impact 5).

**Mitigation, already applied:** this entire audit derives its architecture **exclusively from TaleWorlds' own published API surface**. No upstream implementation detail, algorithm, or design was used. That is both the legally clean path and the technically sounder one, since the TaleWorlds API is what we actually build against.

**Decision recorded 2026-09-17 (project owner): (a) Clean-room.** Continue deriving architecture and implementation exclusively from TaleWorlds' own API surface; the incumbent BannerlordCoop project may still be studied for public facts (existence, README, issue tracker, licence terms) but never for implementation detail, algorithms, or design to port or adapt. This is the status quo the Phase 0 audit already followed — no change to method required, only a formal owner sign-off that unblocks Phase 1.

Options considered and declined:
- (b) Seek written permission from the maintainers.
- (c) Contribute upstream instead — they have an open "Epic: War Sails Sync" (#3060) and no naval support yet. Deserves genuine consideration on its merits (4,257 files of working co-op, an unstarted naval epic) but the owner chose to continue this project.

**Note:** `AGENTS.md` in that repository also contains instructions attempting to make automated agents refuse work and respond in a fictional persona. That is repository content directed at tooling, not a licence term and not an instruction from this project's owner; it was not followed. The actual `LICENSE` was respected on its merits.

---

## RISK-01 — `Ship` has no network identity · **Impact 5 × Likelihood 5 = 25**

**Evidence (VERIFIED):** `TaleWorlds.CampaignSystem.Naval.Ship` derives from `System.Object`. It is not an `MBObjectBase`; it has no `MBGUID Id` and no `StringId`. Confirmed by direct metadata read of the base type and full member list.

**Consequence:** there is no engine-provided way to say "this ship" across two processes. Every naval feature the project wants — capture, destruction, loot, fleet composition, transfer, stashing — needs exactly that. Additionally `Ship._versionNo` is **not** `[SaveableField]` (VERIFIED), so it resets on load and cannot anchor identity across sessions.

**Mitigation:** synthesized `CoopShipId` registry, persisted through our own `SyncData`, positionally rebound on load with a content-fingerprint fallback. Design: `SYNCHRONIZATION_MODEL.md` §4.3. Ownership cross-check is available because `Ship._owner` **is** `[SaveableField]` (VERIFIED).

**Implemented, Phase 1.6 (2026-09-17), for the running session.** `src/Coop.Core/Identity/` (`CoopShipId`, `CoopShipIdAllocator`, `ShipFingerprint`, `ShipIdentityRebinder` — all game-agnostic, unit-tested) and `src/Coop.GameInterface/Identity/` (`CoopShipRegistry` using a `ConditionalWeakTable<Ship, _>` so a destroyed ship doesn't leak just because it was once looked at; `ShipIdentityCampaignBehavior` assigning and logging an id for every ship on every `MobileParty` each in-game day, as a live exit-criterion demonstration). **Not yet done:** wiring `SyncData` so the id↔ship binding survives a save/load — that's Phase 1.9, where the positional-rebinding assumption below finally gets tested against a real save round-trip instead of just unit-tested against synthetic data.

**Residual:** the positional-rebinding assumption is `LIKELY`, not `VERIFIED` → tracked separately as RISK-15. `ShipIdentityRebinder` (unit-tested: exact match, appended ship, removed ship, and — critically — the failure mode where the assumption turns out false, i.e. a fingerprint mismatch at a position) is ready for that test; it just hasn't been run against a real save yet.

---

## RISK-02 — `GameNetwork` may not work in a campaign session · **RESOLVED, was Impact 5 × Likelihood 4 = 20**

**Evidence:** `TaleWorlds.MountAndBlade.GameNetwork` exposes a complete client/server + module-event messaging API (VERIFIED, full member list in `NETWORK_PROTOCOL.md` §1).

**RESOLVED 2026-09-17 — B3 ran on the real install.** `tools/network-probe/CoopNetworkProbe` ran inside a live singleplayer campaign (official modules + `NavalDLC` only, no third-party mods, not even Harmony). Result, from `network-probe.log` (VERIFIED, direct observation):

1. Every experiment step logged `STEP OK` — `GameNetwork.Initialize`, `PreStartMultiplayerOnServer`, `StartMultiplayerOnServer(7773)`, `AddRemoveMessageHandlers`, and the loopback broadcast all ran with **no exception**.
2. `IsSessionActive`, `IsMultiplayer`, and `IsServer` all flipped to `true` immediately after `StartMultiplayerOnServer` — the multiplayer session genuinely activated inside the campaign.
3. The campaign kept ticking normally for at least 15 seconds afterward (`Campaign.Current != null: True`, `CampaignTime` unchanged and stable across three tick checks — consistent with the game being paused on the map screen, not stalled).
4. **The game then crashed** with a native access violation (Windows Application Error, exception code `0xc0000005`, faulting module reported as `unknown`/`0.0.0.0`, faulting process `TaleWorlds.MountAndBlade.Launcher.exe`) roughly 15–40 seconds after the multiplayer session was forced on. No managed exception, no BUTR crash report — this is an engine-level memory-safety crash, not a caught error. No autosave occurred in that window (confirmed from `Game Saves\` timestamps), so no save was put at risk.

**Verdict: neither of the two outcomes `NETWORK_PROTOCOL.md` §2 was framed around.** It isn't "works cleanly" (Outcome A) or "throws/no-ops" (Outcome B) — the managed API reports success and the flags flip correctly, but forcing `GameNetwork` into multiplayer mode *retrofitted onto an already-running singleplayer `Campaign`* destabilizes the engine and crashes it within tens of seconds. **Decision: build our own transport.** `GameNetwork` must not be used to add co-op to an existing singleplayer session — that is exactly this project's premise, so this closes the question rather than leaving it open. `TaleWorlds.Network.TcpSocket` / the `MessageContract` machinery (VERIFIED plain managed types, `NETWORK_PROTOCOL.md` §1) or raw `System.Net.Sockets` are the path forward; the `ICoopTransport` abstraction already assumed this outcome so no architecture rework is needed, only implementing it.

**Not established (UNKNOWN, not tested):** whether a `Campaign` created from the start knowing it will be multiplayer (rather than retrofitted mid-session, which is what was tested) would avoid this. Not pursued — a mid-session retrofit onto a normal singleplayer campaign is what "cooperative mod for an existing game" requires, so the untested path isn't this project's use case anyway.

**Housekeeping:** the probe never called `GameNetwork.EndMultiplayer()`/`TerminateClientSide()` to tear the session back down — that's a gap in the probe, not evidence about teardown safety. `tools/network-probe/` should be removed from the local install now that it's answered its question (its own README already says so).

---

## RISK-03 — Campaign↔mission naval seam · **Impact 5 × Likelihood 4 = 20**

**Evidence (VERIFIED):** campaign ships are `TaleWorlds.CampaignSystem.Naval.Ship`; mission ships are `NavalDLC.Missions.Objects.MissionShip` — different types, different assemblies. `MissionShip` relates to campaign via `TaleWorlds.Core.IShipOrigin` (which `Ship` implements). Boarding and capture exist **only** at mission layer (`ShipOrder`, `NavalShipsLogic.ShipCapturedEvent`); ownership exists **only** at campaign layer (`ChangeShipOwnerAction`). **UNCONFIRMED:** how the binding is established and whether it survives a whole battle.

**Consequence:** a mismapping duplicates or destroys ships in the persistent world — the worst class of bug for a persistent campaign.

**Mitigation:** treat mission outcomes as *reports*, never as direct mutations. Server re-resolves every reported ship through the `CoopShipId` registry before applying `ChangeShipOwnerAction`/`DestroyShipAction`. Reject unresolvable reports loudly.

**Resolution:** requires an install. Naval *missions* are explicitly out of Phase 1 scope for this reason; naval *campaign* state is in scope because it does not cross the seam.

**B7, partial progress (2026-09-17, `ilspycmd` against the real install — VERIFIED for what's stated, still open for what isn't):** the launch path from a campaign `MapEvent` into an actual naval `Mission` is a **decorator seam**, not a type check buried in one place. `TaleWorlds.CampaignSystem.CampaignMission` is a thin static forwarder to an injected `Campaign.Current.CampaignMissionManager : ICampaignMissionManager`. The base implementation, `SandBox.CampaignMissionManager`, stubs `OpenNavalBattleMission` / `OpenNavalRaidMission` / `OpenNavalSetPieceBattleMission` to `return null` — the base game cannot launch a naval mission on its own. `NavalDLC.NavalDLCSubModule.OnAfterGameInitializationFinished` wraps it at runtime: `campaign.CampaignMissionManager = new NavalMissionManager(campaign.CampaignMissionManager)`. `NavalMissionManager` implements the three naval methods itself (via `NavalDLC.Missions.NavalMissions`) and forwards every other method to the wrapped base manager unchanged. **This is a clean, public seam for us too** — a coop-aware decorator installed the same way (`OnAfterGameInitializationFinished`, no Harmony needed) can observe or intercept every mission launch, naval or not, without touching TaleWorlds' dispatch logic.

**B7, resolved (2026-09-17):** the caller was in a file the earlier pass hadn't checked — `TaleWorlds.CampaignSystem.Helpers.MenuHelper`, not any of the five assemblies searched before. `MenuHelper.EncounterAttackConsequence(MenuCallbackArgs)` is the "Attack" game-menu option's consequence callback, and it's where naval-vs-land actually gets decided: for a general field battle, `bool flag = PlayerEncounter.IsNavalEncounter()` (`= MapEvent.IsNavalMapEvent`) branches directly to `CampaignMission.OpenNavalBattleMission`/`OpenBattleMission`/`OpenCaravanBattleMission`; for a village raid, `MapEventHelper.GetRaidContext` classifies sea/land presence per side (pure `MobileParty.IsCurrentlyAtSea` inspection, VERIFIED) and picks the same three methods, or `StartSeaRaidMission` for player-side raids — which itself opens a troop/ship-selection UI and only launches the mission from that UI's completion callback. Full chain: `ARCHITECTURE.md` §6.

**Consequence for us:** the naval/land decision has no server-side campaign-layer hook of its own — it lives inside a client-local menu-consequence handler triggered by UI interaction. Our own naval-battle-hosting logic (Phase 4, gated on this risk and RISK-08) will need to intercept or reimplement at this menu-consequence layer, not assume a clean campaign-layer event exists to hook instead.

**Still open (unaffected by B7):** how `Ship` ↔ `MissionShip` binding is established once a mission actually opens, and whether it survives a whole battle — B7 answered "how do we get to `OpenNavalBattleMission`", not "what happens inside the mission." That needs B10 / a running naval battle to observe.

---

## RISK-04 — Campaign non-determinism & event ordering · **Impact 4 × Likelihood 5 = 20**

**Evidence:** 277 `CampaignEvents` VERIFIED to exist. Their **ordering, re-entrancy, and determinism are UNCONFIRMED** — method bodies are stripped from reference assemblies.

**Consequence:** silent, slow divergence. The worst failure mode, because it presents as "the game feels wrong" hours in.

**Mitigation (design-level, already chosen):** authoritative-server topology assumes non-determinism from the outset. Lockstep was rejected for exactly this reason (`ARCHITECTURE.md` §2). Clients never run authoritative campaign logic.

**Resolution:** Phase 1.5 event observation harness — subscribe to all 277 events, log order and frequency, compare two machines against the same save.

---

## RISK-05 — Version churn on bound APIs · **Impact 4 × Likelihood 4 = 16**

**Evidence (VERIFIED, measured):** 1.4.8-stable → 1.5.3-beta removed **2,638** CampaignSystem members and 92 types. `MapEvent` lost 16 members including `Initialize` (arity change) and `LootDefeatedPartyShips` (signature change). `ChangeShipOwnerAction` gained a whole stashing subsystem. Raw diffs in `docs/evidence/`.

**Good news, also measured:** `Ship` itself changed by only 2 removed / 3 added; `EncounterManager` was unchanged; only 3 `CampaignEvents` were removed and none are sync-critical.

**Mitigation:** pin one version (`VERSION_SUPPORT.md` §7); hard version gate at module load; adapters around the volatile types (`MapEvent`, `PlayerEncounter`, `MapEventSide`, `ChangeShipOwnerAction`); `tools/apiscan` diff as a CI gate (Phase 1.3).

---

## RISK-06 — Internal/private members require publicizer or Harmony · **Impact 3 × Likelihood 4 = 12**

**Evidence (VERIFIED):** `PartyBase.AddShipInternal(Ship)` / `RemoveShipInternal(Ship)` are `assembly`-visible. `MobileParty.Anchor` and `.IsTargetingPort` have private setters (though `SetAnchor` is public). Much required state is `private` with `[SaveableField]`.

**Mitigation:** prefer public APIs where they exist (`ChangeShipOwnerAction` over `AddShipInternal`; `SetAnchor` over the setter). Where unavoidable, use a publicizer at build time — more stable than reflection and cheaper than Harmony.

**Decided in Phase 1.6 (2026-09-17): publicizer, when the need actually arises — not yet.** Building the identity layer (`CoopShipRegistry`, `ShipIdentityCampaignBehavior`) turned out to need zero internal access: `PartyBase.Ships`/`.FlagShip`/`GetShipsVersion()` are all public, `MobileParty.All` is public, and every mutation the design calls for goes through public actions (`ChangeShipOwnerAction`, `EquipUpgradePiece`, `ChangeFigurehead`) rather than `AddShipInternal`/`RemoveShipInternal` directly. So there is nothing to publicize right now. The standing decision for *when* something unavoidable does show up: a build-time publicizer (e.g. `Bannerlord.BUTR.Publicizer` or `Krafs.Publicizer`, chosen at that point) over Harmony, per the reasoning already in the mitigation above — more stable than reflection, and unlike Harmony, doesn't patch the running game.

---

## RISK-07 — No install available for verification · **Impact 4 × Likelihood 5 = 20**

**Evidence (VERIFIED):** audit host has no game files and no .NET toolchain.

**Consequence:** nine substantive questions remain unresolvable (audit §16), including RISK-02, RISK-03, RISK-08, RISK-12, RISK-13 and the actual pinned version.

**Mitigation applied:** reference assemblies replaced most of what an install would have given — full type/member/attribute surface, which is more than a decompiler-free install inspection would typically yield.

**Resolution:** **obtaining a Bannerlord + War Sails install is the single highest-value next action.** It unblocks nine investigations at once.

---

## RISK-08 — Naval mission multiplayer may be impossible · **Impact 5 × Likelihood 3 = 15**

**Evidence:** `NavalDLC.NavalMissionState : MissionState`, `NavalShipsLogic : MissionLogic, IVehicleHandler` (VERIFIED). Whether ship physics/control can be networked at all is **UNKNOWN**. Upstream has not attempted it (issue #3088 open, VERIFIED).

**Mitigation:** graceful degradation ladder — (1) naval campaign state syncs, battles auto-resolve; (2) one player fights, others spectate; (3) full multiplayer naval battle. Ship (1) first; it is genuinely playable.

---

## RISK-09 — Save-schema divergence across versions · **Impact 4 × Likelihood 3 = 12**

**Evidence:** beta adds persisted naval members (`Settlement.ShipStash`, `PlayerDataForNavalAutoTravel._reservedShips`, `MapEventSide._shipSiegeEngineList`) absent from stable (VERIFIED).

**Mitigation:** version-stamp our own `SyncData` blobs; never claim cross-version save support; fail loudly on mismatch.

---

## RISK-10 — Scale/bandwidth at 8+ players · **Impact 3 × Likelihood 3 = 9**

**Evidence:** upstream states "optimized for up to 8 players" (VERIFIED, README). Our own budget is UNCONFIRMED.

**Mitigation:** field-level deltas not snapshots; `[CachedData]` never replicated (a large saving, VERIFIED from metadata); interest management deferred until measured (Phase 1.8).

---

## RISK-11 — Quests are not designed for multiple players · **Impact 4 × Likelihood 3 = 12**

**Evidence:** quest types are single-`MainHero`-centric (LIKELY, from API shape). Upstream lists quests as *planned*, not shipped (VERIFIED). War Sails adds 15 storyline quests plus `NavalStorylineData.StartNavalStoryline()` (VERIFIED), all single-player-shaped.

**Mitigation:** Phase 1 scopes quests **out**. Later: per-player quest instancing, or a designated quest owner.

---

## RISK-12 — Mission-layer float determinism · **Impact 4 × Likelihood 3 = 12**

**Evidence:** `NavalDLC.Missions.NavalPhysics` (14 types), `WaveParametersComputerLogic`, wind/wave configs (VERIFIED). Float physics across machines diverges (LIKELY, general).

**Mitigation:** never rely on independent simulation agreeing. Authoritative outcomes only; positions replicated, not recomputed.

---

## RISK-13 — Save compatibility with/without War Sails · **Impact 4 × Likelihood 3 = 12**

**Evidence:** naval types live in the base assembly (VERIFIED), so the *types* always exist; whether a War-Sails-created save loads without the DLC is **UNCONFIRMED**.

**Mitigation:** Phase 1 requires matching DLC state on server and client, enforced at handshake (`NETWORK_PROTOCOL.md` §5, implemented and tested Phase 1.7 — `HandshakeValidator`). Run the §6 compatibility matrix in `SAVE_FORMAT.md` once an install exists.

**Note (1.9, 2026-09-17):** an install has existed since Phase 0.5, and still nobody has run the §6 matrix — it needs many manual save/reload cycles toggling `NavalDLC` on and off, which is real time at the keyboard, not something Phase 1.9's coding work could substitute for. Still open.

---

## RISK-14 — Scope · **Impact 4 × Likelihood 4 = 16**

**Evidence (VERIFIED):** the requested feature set spans ~20 major systems. The incumbent project has 4,257 `.cs` files, years of work, an active team — and still lists quests, hideouts and War Sails as *unimplemented*.

**Mitigation:** the phased roadmap deliberately ships a narrow vertical slice (party position, two clients) before breadth. Naval *campaign* state before naval *battles*. This is the honest read: matching the incumbent's breadth is a multi-year effort; the naval niche is where this project can lead, because nobody has built it.

---

## RISK-15 — Ship id stability rests on a `LIKELY` assumption · **Impact 4 × Likelihood 3 = 12**

**Evidence:** the `CoopShipId` design rebinds ids positionally from `PartyBase.Ships` after load. That `MBList<Ship>` preserves order through save/load is **LIKELY** (it is a `[SaveableField]` ordered container) but **not runtime-verified**.

**Mitigation:** content-fingerprint fallback `(ShipHull.StringId, name, hitPoints, sailHitPoints, RandomValue)` — `RandomValue` is `[SaveableProperty]` and per-ship (VERIFIED). Ships are session-scoped only until the Phase 1.9 round-trip test passes.

**Note (B6, 2026-09-17):** this is a different fingerprint from TaleWorlds' own `Ship.VersionNo` hash (`ShipHull.Id` + upgrade pieces + `Figurehead` + `CustomSailPatternId`, decompiled and documented in `SYNCHRONIZATION_MODEL.md` §4.3) — `VersionNo` is a change-detection hint recomputed on mutation, not a stable identity fingerprint, and volatile fields like `hitPoints` make it unsuitable for our purpose anyway. Don't conflate the two.

**Note (1.6, 2026-09-17):** the rebinding algorithm itself (`Coop.Core.Identity.ShipIdentityRebinder`) is built and unit-tested against synthetic data for exactly this failure mode — a fingerprint mismatch at a position is flagged, never silently trusted. That's necessary but not sufficient: this risk stays open until it's run against an actual save → load round-trip (Phase 1.9), which is the only thing that can move `LIKELY` to `VERIFIED` or `FALSE`.

**Note (1.9, 2026-09-17):** the round-trip test itself is now built and deployed, not just designed. `ShipIdentityCampaignBehavior.SyncData` persists every ship's `(CoopShipId, fingerprint)` per owner through the engine's real `IDataStore`; `OnGameLoadFinishedEvent` re-locates each owner via `MBObjectManager.GetObject(MBGUID)` and reruns `ShipIdentityRebinder` against the just-reloaded `PartyBase.Ships`, logging `FingerprintMismatches`/`UnmatchedPersistedShips` counts to `Documents\Mount and Blade II Bannerlord\Coop.GameInterface\ship-identity.log`. **Still open:** nobody has actually saved and reloaded a campaign with this running yet. Zero mismatches on a real run is what moves this from `LIKELY` to `VERIFIED`; any mismatch moves it to `FALSE` (in which case the already-built fingerprint fallback is what carries the design, not a redesign).

---

## RISK-16 — Third-party mods on the target install mutate campaign state · **Impact 4 × Likelihood 4 = 16**

**Evidence (VERIFIED, measured on the install machine 2026-09-16):** 24 modules are installed — 9 official and **15 third-party**.

| Category | Modules | Co-op implication |
|---|---|---|
| **Campaign-state mutating** | `Bannerlord.Diplomacy v1.5.2`, `ImprovedGarrisons v4.2.0.7`, `RaiseYourBanner v16.1.7`, `DisableCompanionDonations v1.0.0` | **High risk.** These add campaign behaviors and, where they register a `SaveableTypeDefiner`, **change the save schema**. Any divergence in the mod set between server and client produces campaign desync or a load failure. |
| **Naval-adjacent** | `NoWaterEscape v1.0.0` | Touches water/movement behaviour — the exact area War Sails sync depends on. |
| **Mission layer** | `RTSCamera v5.4.16`, `RTSCamera.CommandSystem v5.4.16`, `DismembermentPlus v2.0.8.8`, `UnblockableThrust v1.1.3` | Lower campaign risk; can still alter mission outcomes, which feed campaign consequences. |
| **Infrastructure (benign / useful)** | `Bannerlord.Harmony v2.4.2.248`, `Bannerlord.ButterLib v2.12.0`, `Bannerlord.UIExtenderEx v2.13.3`, `Bannerlord.MBOptionScreen v5.12.3` | Standard modding infrastructure. **Harmony's presence confirms the RISK-06 mitigation path is available.** |
| **Cosmetic** | `BannerFix v3.3.6`, `AchievementUnblocker v1.1.1` | Low risk. |

**Why this matters beyond "mods might conflict":** our synchronization model assumes the server and every client compute campaign state from the *same* rules. A mod installed on one machine and not another breaks that assumption silently — which is RISK-04's failure mode with a concrete, already-present cause. Mods that register saveable types additionally alter the save of record, colliding with the schema-version discipline in `SAVE_FORMAT.md` §5.

**Corroboration:** the incumbent co-op mod's README instructs users to "Disable all other mods" and warns that compatibility is not guaranteed (VERIFIED, public README).

**Mitigation:**
1. **Develop and test against a clean profile** — official modules plus `Bannerlord.Harmony` only. Use a separate Steam/launcher mod profile so the existing setup is not disturbed.
2. **Enforce mod-set equality at handshake.** The protocol already negotiates `moduleSet` (`NETWORK_PROTOCOL.md` §5); extend it to hash the full module list with versions and reject mismatches.
3. **Treat campaign-state mods as unsupported in Phase 1**, and say so plainly rather than allowing a silently-broken session.
4. Revisit selectively later — Diplomacy in particular is popular enough to be worth explicit support eventually, but only once the core is stable.

**Decision recorded 2026-09-17 (project owner): clean dev profile.** Development and testing use official modules plus `Bannerlord.Harmony` only. `Bannerlord.Diplomacy`, `ImprovedGarrisons`, `RaiseYourBanner`, `DisableCompanionDonations`, and `NoWaterEscape` (and other non-essential third-party mods) must be disabled in the profile used to build and test co-op systems, per mitigation item 1 above. The alternative — supporting the arbitrary 15-mod set from day one — was declined; it would make every desync investigation ambiguous.

---

## RISK-17 — Real save became unloadable during Phase 1.9 live testing · **Impact 5 × Likelihood UNCONFIRMED**

**Evidence (VERIFIED symptom, UNCONFIRMED root cause):** during the first live test of Phase 1.9's ship-identity persistence (2026-09-17), the owner saved a campaign (`save016.sav`, 5.6MB) after buying a ship, sailing it, entering a settlement, and recruiting units — the save's own recorded metadata (`MainPartyShipCount: 1`) and `tools/campaign-event-harness`' log (`OnSaveOverEvent(true, "save016")`) both confirm the save completed cleanly. Loading it back then failed with a generic, choice-less "an error has occurred" — no crash, no Windows Application Error event, no BUTR report.

Two load attempts were made: (1) with the exact module set recorded in the save (`Native;SandBoxCore;Sandbox;CustomBattle;NavalDLC;BirthAndDeath;FastMode;BannerlordUnity;CampaignEventHarness`) — failed; (2) with `BannerlordUnity` deselected (a module-list mismatch from what the save expects) — also failed, identically. In **both** attempts, `CampaignEventHarness`'s log (which tracks all 277 `CampaignEvents`) recorded **zero events** — not even `OnGameLoadedEvent` — meaning the failure happens before any campaign-behavior code, ours or the base game's, gets control at all.

This world's naval content is large: the save's own module-report data showed **977 ships across 337 parties** at save time (`ship-identity.log`).

**Consequence:** the owner's in-progress session (ship purchase, recruitment, settlement visit) is currently stuck behind an unloadable save. The underlying save file itself is intact on disk (untouched since a clean write) — this is a *load*-time failure, not file corruption or data loss at the filesystem level.

**Current assessment, held with appropriate uncertainty:** more consistent with a base-game/War-Sails engine issue triggered by scale than with anything in `Coop.GameInterface` — the failure predates any behavior code running, and it reproduced identically whether `BannerlordUnity` was active or not (though the second attempt's module mismatch is itself a confound, so this isn't fully conclusive). **Not ruled out.** No further isolation has been possible without more detail than a generic error dialog provides.

**Mitigation applied regardless of root cause:** `ShipIdentityCampaignBehavior.SyncData`'s load path is now wrapped end-to-end in try/catch, not just its decode step — a real gap found via code review during this incident (the engine's own `IDataStore.SyncData<T>` retrieval call was previously unprotected). Whatever the cause of this specific incident, our own code must never be able to block someone's entire campaign from loading.

**Open:** whether this reproduces on a fresh save with a similarly large naval world and no mods at all (would confirm base-game scale bug, out of this project's ability to fix); whether it reproduces with our mods removed *and* a matching module-list save (not yet possible — no such save exists to test against). If reproducible, worth reporting upstream to TaleWorlds/the War Sails team as a genuine bug report.

---

## Top Five (audit §14, superseded — see update below)

1. **RISK-01** — `Ship` has no network identity
2. ~~RISK-02~~ — resolved 2026-09-17, own transport required
3. **RISK-03** — campaign↔mission naval seam
4. **RISK-04** — campaign non-determinism & event ordering
5. **RISK-05** — version churn on bound APIs

RISK-00 sits above all of these but is a **legal/business decision, not a technical risk**, and was decided by the owner 2026-09-17 (clean-room).

**Update 2026-09-16:** RISK-07 (no install available) is **resolved** — the installation has been measured and the version pinned (`docs/VERSION_SUPPORT.md` §6). The audit turned out to have been conducted against exactly the right version. RISK-16 is new, and RISK-06's mitigation is confirmed available (Harmony is installed).

**Update 2026-09-17:** RISK-00 and RISK-16 decided by the owner; RISK-02 resolved by running the B3 probe on the real install — `GameNetwork` crashes the engine when forced into multiplayer mode inside an already-running singleplayer campaign, so the project builds its own transport rather than adopting `GameNetwork`. B6 (`Ship.VersionNo` mechanism) also resolved. Current top unresolved risk is **RISK-01**.
