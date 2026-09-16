# Project Status

**Last updated:** 2026-09-16

---

## Version Block

| Field | Value | Confidence |
|---|---|---|
| **Current Bannerlord version** | **UNKNOWN — not yet measured** | — |
| **Current War Sails version** | **UNKNOWN — not yet measured** | — |
| **Branch (stable / beta)** | **UNKNOWN — not yet measured** | — |
| **Mod version** | `0.0.0` (pre-implementation; no code yet) | VERIFIED |
| **Current phase** | **Phase 0 — Technical Audit: COMPLETE** | VERIFIED |
| Audited against | `1.4.8.119303` (latest stable) and `1.5.3.122374-beta` (latest beta) | VERIFIED |

> ⚠ **`CLAUDE.md` §2 requires determining the installed Bannerlord and War Sails versions at the start of every development session.** This cannot be done from the current environment.

The installation is reported to be at `G:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord`. **That path is not reachable from this session.** Claude Code is running in an isolated Linux VM in the cloud, not on the Windows machine: root is `/dev/vda`, there are no WSL-style `/mnt/c` or `/mnt/g` mounts, no CIFS/9p/virtiofs host shares, and a full filesystem sweep finds no game files (all verified this session).

**Resolution:** run Claude Code locally on the Windows machine (see `docs/LOCAL_SETUP.md`), or at minimum run `tools/apiscan/collect_install_report.py` there. It needs only Python 3 — no .NET, no Steam API, no third-party packages — and emits `docs/install-report/INSTALL_REPORT.md` plus JSON and API surface dumps. Output is text only; it never copies game assemblies, which are proprietary and must not be committed.

```
python tools\apiscan\collect_install_report.py "G:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
```

**No version has been silently targeted** — the audit explicitly covers both the latest stable and latest beta lines and reports the differences between them.

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

**None.** `CLAUDE.md` mandates completing the Phase 0 audit before Phase 1, and forbids implementing gameplay yet. The audit is complete; work is paused pending the decisions below.

---

## Blocked Work

| # | Blocked item | Blocked by | Unblocks |
|---|---|---|---|
| B1 | **Licensing decision (RISK-00)** | Project owner | All implementation |
| B2 | Version pin + session version check (`CLAUDE.md` §2) | **Install not reachable from this cloud session** — collector script ready, needs to be run on the Windows machine | Phase 1.1, 1.2 |
| B3 | `GameNetwork`-in-campaign probe (RISK-02) | Needs a running game | All netcode |
| B4 | Campaign determinism / event ordering (RISK-04) | No install | Sync model |
| B5 | `Ship` ↔ `MissionShip` binding lifetime (RISK-03) | No install | Naval capture |
| B6 | Which mutations bump `Ship.VersionNo` | No install (method bodies) | Change detection |
| B7 | `MapEvent` → naval mission launch path | No install (method bodies) | Naval battles |
| B8 | Siege stage transition sequence | No install (method bodies) | Sieges |
| B9 | Save compat with/without War Sails (RISK-13) | No install | Persistence |
| B10 | `MissionShip` authority & physics determinism (RISK-12) | No install | Naval battles |
| B11 | Official TaleWorlds War Sails modding documentation | Not located in this session | Naval detail |

> **B1 and the install are the two real blockers.** Obtaining a machine with Bannerlord + War Sails and a .NET toolchain converts B2–B10 from blocked to testable in one step.

### ⚠ Decisions required from the project owner

**1. Licensing (RISK-00).** `CLAUDE.md` instructs studying `Bannerlord-Coop-Team/BannerlordCoop` as a technical reference. That repository changed licence on **2026-06-17** from MIT to source-available. It permits "viewing, reference, education, security review" — so *studying* it is within the licence — but explicitly prohibits using its source "to create, contribute to, improve, support, or maintain a competing… co-op… mod."

This project is such a mod. The distinction that matters: **observing what it does is permitted; deriving our implementation from it is not.** The audit stayed on the permitted side — it records public facts (that the project exists, its README, its licence, its open issues) and derives **all architecture from TaleWorlds' own API surface**. No upstream implementation detail was used.

Your options: **(a)** clean-room (status quo, recommended); **(b)** seek written permission; **(c)** contribute upstream instead — they have working co-op and an open, unstarted naval epic.

**2. Obtain a Bannerlord + War Sails install** with a .NET toolchain. Highest-value practical step; unblocks B2–B10.

---

## Known Bugs

**None.** No code has been written.

---

## Technical Debt

| # | Item | Severity | Notes |
|---|---|---|---|
| TD1 | `CLAUDE.md` §2 session version check cannot run in a cloud session | High | Collector written (`tools/apiscan/collect_install_report.py`); must be run locally and its output committed |
| TD2 | `docs/VERSION_SUPPORT.md` §6 pinned-target table is empty | High | Fill on first run with a real install |
| TD3 | Ship-id stability rests on a `LIKELY` list-ordering assumption | Medium | Fingerprint fallback designed; needs the Phase 1.9 round-trip test (RISK-15) |
| TD4 | `ModuleInfo` module-enumeration entry point is illustrative, not verified | Medium | Confirm against installed `TaleWorlds.ModuleManager.dll` |
| TD5 | Official TaleWorlds War Sails modding docs not consulted | Medium | Naval findings are assembly-derived only; B11 |
| TD6 | Reference assemblies lack method bodies | Medium | Inherent; all ordering/flow claims are UNCONFIRMED by construction |
| TD7 | `docs/evidence/` diffs are large and uncompressed | Low | Acceptable; they are the audit trail |
| TD8 | No build, test, or CI infrastructure yet | Medium | Phase 1.2–1.3 |

---

## Next Tasks

**Phase 0.5 — unblock (owner):**

1. Decide RISK-00 (licensing).
2. Provide a Bannerlord + War Sails install with a .NET toolchain.
3. Run `docs/VERSION_SUPPORT.md` §5 detection; fill §6 and this file's Version Block.
4. Confirm target player count and whether beta support is required.

**Phase 1 — foundations (ordered; see `docs/ROADMAP.md`):**

| Step | Task |
|---|---|
| 1.1 | Environment capture and version pin |
| 1.2 | Module skeleton + hard version gate |
| 1.3 | `tools/apiscan` API-drift CI gate |
| 1.4 | ⚠ `GameNetwork`-in-campaign probe (resolves RISK-02) |
| 1.5 | Campaign event + determinism harness (resolves RISK-04) |
| 1.6 | Identity layer: `MBGUID` registry + synthesized `CoopShipId` (mitigates RISK-01) |
| 1.7 | Transport + wire protocol + idempotent consequence ledger |
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
