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
| **Mod version** | `0.0.0` (pre-implementation; no code yet) | VERIFIED |
| **Current phase** | **Phase 0 — Technical Audit: COMPLETE** | VERIFIED |
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
| `docs/LOCAL_SETUP.md` | Running Claude Code locally; unblocks B2, B6–B9 |
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

**None blocking.** B3 resolved (below); Phase 1 foundations (§ Next Tasks) are next, or B7/B8 if continuing the decompiler-based audit work first.

### Just resolved

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
| B7 | `MapEvent` → naval mission launch path | **Partially resolved 2026-09-17** — decorator seam confirmed (`RISK_REGISTER.md` RISK-03), exact trigger call site still unfound | Naval battles |
| B8 | Siege stage transition sequence | Method bodies — same `ilspycmd` path as B6, not yet run | Sieges |
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
| TD8 | No build, test, or CI infrastructure yet | Medium | Phase 1.2–1.3 |

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
| 1.1 | Environment capture and version pin |
| 1.2 | Module skeleton + hard version gate |
| 1.3 | `tools/apiscan` API-drift CI gate |
| ~~1.4~~ | ~~`GameNetwork`-in-campaign probe (resolves RISK-02)~~ — **done**, `GameNetwork` ruled out |
| 1.5 | Campaign event + determinism harness (resolves RISK-04) |
| 1.6 | Identity layer: `MBGUID` registry + synthesized `CoopShipId` (mitigates RISK-01) |
| 1.7 | Transport + wire protocol + idempotent consequence ledger — build on `TaleWorlds.Network.TcpSocket`, per 1.4's result |
| 1.8 | First vertical slice: party position, two clients |
| 1.9 | Persistence, reconnect, server restart, save-safety layer |
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

**Per `CLAUDE.md` §10: no functionality is claimed to work. No code exists. Nothing below `VERIFIED` has been turned into an implementation assumption anywhere in these documents.**
