# Project Status

**Last updated:** 2026-09-16
**Phase:** 0 (Technical Audit) — **COMPLETE**
**Next:** Phase 0.5 (unblock) — **requires project-owner decisions**
**Code written:** none, by design. Phase 0 is audit-only.

---

## Where This Stands

A complete Phase 0 technical audit has been performed and written up across ten documents. The architecture is specified, the risks are registered, and Phase 1 is sequenced — but **not started**, per the brief.

Two things need your decision before any implementation begins (§4).

## What Was Actually Verified

The audit ran on a Linux container with **no Bannerlord installation and no .NET toolchain**. Rather than guess, the audit obtained the **official TaleWorlds assemblies** (published reference assemblies for v1.4.8.119303 stable and v1.5.3.122374-beta) and parsed their CLI metadata directly with a purpose-built ECMA-335 reader (`tools/apiscan/cli_meta.py`).

That turned out to be a strong substrate: the reference assemblies retain **private fields, custom attributes (including `[SaveableField]`/`[CachedData]`), properties, events and full method signatures**. Method *bodies* are stripped — so every claim about API *shape* is `VERIFIED`, and every claim about *ordering or control flow* is honestly marked `UNCONFIRMED`.

**2,306 types** in `TaleWorlds.CampaignSystem.dll` and **692** in `NavalDLC.dll` were available for inspection. All findings are reproducible with the committed tool.

## Headline Findings

1. **The naval data model is in the base game, not the DLC.** `TaleWorlds.CampaignSystem.Naval.Ship`, `ChangeShipOwnerAction`, `DestroyShipAction`, `PartyBase.Ships`, `MapEvent.IsNavalMapEvent`, `BattleTypes.BlockadeBattle` all live in `TaleWorlds.CampaignSystem.dll`. `NavalDLC.dll` adds behaviors, missions and content. **Ship ownership can be synchronized without depending on the DLC** — this shapes the whole naval design.

2. **`Ship` has no network identity.** It derives from `System.Object` — no `MBGUID`, no `StringId`. Every naval feature needs to name a specific ship across processes, and the engine provides no way to do it. This is the single largest technical risk (RISK-01); a synthesized, persisted id registry is designed in `SYNCHRONIZATION_MODEL.md` §4.3.

3. **The engine tells us what to replicate.** `[SaveableField]`/`[SaveableProperty]` = authoritative state; `[CachedData]` = derived, never replicate. This is machine-readable and eliminates a whole class of desync bug by construction rather than by judgement.

4. **A full networking API exists — but its usability in a campaign session is unverified.** `GameNetwork` has complete client/server and custom-message support (verified), but whether it initializes outside a multiplayer game mode cannot be determined from metadata. This is the highest-leverage unknown (RISK-02); an experiment is specified and the architecture is insulated from the answer.

5. **The incumbent co-op mod does not support War Sails.** `Bannerlord-Coop-Team/BannerlordCoop` lists it as *planned* and its README says "Do not enable the War Sails DLC." Their "Epic: War Sails Sync" (#3060) and three `[WARSAILS]` issues are all open and unstarted. **Naval co-op is unbuilt by anyone** — this project's clearest differentiator, and also why there is no prior art to learn from.

6. **⚠ That repository's licence prohibits this project from using its code.** Since 2026-06-17 it is source-available, not MIT, and explicitly forbids use "to create… a competing… co-op… mod." See §4.

## Deliverables

| Document | Contents |
|---|---|
| `docs/INITIAL_TECHNICAL_AUDIT.md` | The full audit — all 30 goals, evidence tables, confidence levels |
| `docs/ARCHITECTURE.md` | Topology, layers, authority model, module layout |
| `docs/WARSAILS_ARCHITECTURE.md` | Complete naval API map: ships, fleets, movement, encounters, missions, boarding, ownership |
| `docs/SYNCHRONIZATION_MODEL.md` | Ownership tiers, metadata-driven authority rule, the ship-identity design |
| `docs/NETWORK_PROTOCOL.md` | Transport surface, channels, wire format, lifecycle, reconnect, restart |
| `docs/SAVE_FORMAT.md` | Save architecture, what we persist, verified per-type persisted state |
| `docs/RISK_REGISTER.md` | 16 scored risks with evidence and confidence |
| `docs/VERSION_SUPPORT.md` | Version landscape, measured cross-version API churn, detection procedure |
| `docs/ROADMAP.md` | Phase 0.5 → Phase 5, with exit criteria |
| `tools/apiscan/cli_meta.py` | The ECMA-335 reader — makes every finding reproducible |
| `docs/evidence/` | Raw API diffs and dumps |

## ⚠ 4. Decisions Needed From You

### 4.1 Licensing (RISK-00) — blocking

`Bannerlord-Coop-Team/BannerlordCoop` changed licence on **2026-06-17** from MIT to source-available. It now explicitly forbids using the source "to create, contribute to, improve, support, or maintain a competing Mount & Blade II: Bannerlord multiplayer, co-op, networking, synchronization, or derivative mod without prior written permission." This project falls inside that.

**This audit already took the clean path:** the entire architecture is derived from TaleWorlds' own published API surface. Nothing in these documents comes from their implementation.

Your options:
- **(a) Clean-room** — keep deriving only from TaleWorlds APIs. *Recommended, and already the status quo.*
- **(b) Seek written permission** from the maintainers.
- **(c) Contribute upstream instead.** Worth serious thought: they have working co-op and an open, unstarted naval epic. If the goal is *playing naval co-op*, this is the shortest path to it.

### 4.2 Get a real install (RISK-07) — blocking nine investigations

A machine with Bannerlord + War Sails and a .NET toolchain converts nine `UNCONFIRMED` items into testable ones, including the two biggest (RISK-02, RISK-03) and the actual version pin. **This is the highest-value practical next step.**

## 5. Current State of Knowledge

| Area | Status |
|---|---|
| Campaign state model | ✅ Verified |
| Party model | ✅ Verified |
| Campaign events (277) | ✅ Verified (ordering ❌) |
| Battle & siege lifecycle | ✅ Verified (transitions ❌) |
| Save/load architecture | ✅ Verified |
| Networking API surface | ✅ Verified |
| Networking *usability in campaign* | ❌ **Unverified — RISK-02** |
| Mission API | ✅ Verified |
| Naval campaign systems | ✅ Verified |
| Ships & fleets | ✅ Verified |
| Naval movement, encounters, mission creation, boarding, ownership | ✅ Verified |
| Campaign↔mission naval binding | ❌ **Unverified — RISK-03** |
| Campaign determinism | ❌ **Unverified — RISK-04** |
| Installed version | ❌ **Unknown — no install** |

## 6. Honest Assessment

The audit is stronger than expected given no game install — the reference assemblies carry enough metadata that most structural questions were answered with direct evidence rather than inference, and the two that weren't are clearly flagged rather than papered over.

Three things are worth being plain about:

- **The scope is very large.** The requested feature list spans ~20 major systems. The incumbent project has 4,257 source files and years of work and still hasn't shipped quests, hideouts, or naval. Phase 1 is deliberately narrow for that reason.
- **Naval battles are the genuinely unknown part.** Naval *campaign* state is well-understood and buildable. Naval *missions* — boarding, ship capture in combat — sit behind two unresolved risks and nobody has done it before.
- **The licensing question is real and is yours to decide.** It is not an engineering problem and no amount of design work resolves it.

**Nothing below `VERIFIED` has been turned into an implementation assumption anywhere in these documents.**
