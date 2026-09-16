# Roadmap

**Phase 0 is complete. Phase 1 has NOT started** — per the audit brief, work stops here pending review.

---

## Phase 0 — Technical Audit ✅ COMPLETE

Delivered: `INITIAL_TECHNICAL_AUDIT.md`, `ARCHITECTURE.md`, `WARSAILS_ARCHITECTURE.md`, `SYNCHRONIZATION_MODEL.md`, `NETWORK_PROTOCOL.md`, `SAVE_FORMAT.md`, `RISK_REGISTER.md`, `VERSION_SUPPORT.md`, this file, `PROJECT_STATUS.md`, plus `tools/apiscan/` and `docs/evidence/`.

Two goals could not be answered in this environment (no game install): the *exact installed* Bannerlord and War Sails versions. Detection procedure delivered instead — `VERSION_SUPPORT.md` §5.

---

## Phase 0.5 — Unblock (do this before Phase 1)

| # | Item | Owner | Blocks |
|---|---|---|---|
| 0.5.1 | **Decide RISK-00** (clean-room / seek permission / contribute upstream) | **Project owner** | Everything |
| 0.5.2 | **Obtain a Bannerlord + War Sails install** with a .NET toolchain | Project owner | 9 investigations |
| 0.5.3 | Run `VERSION_SUPPORT.md` §5; fill in §6 | Either | Version pinning |
| 0.5.4 | Confirm player count target and whether beta support is needed | Project owner | Scope |

Phase 0.5 is small but genuinely blocking. 0.5.1 is a decision, not a task; 0.5.2 converts most `UNCONFIRMED` items into testable ones.

---

## Phase 1 — Foundations

Ordered so each step de-risks the next. Nothing here depends on an `UNCONFIRMED` fact without an explicit gate.

### 1.1 Environment capture & version pin
Run the §5 detection; record exact version, `ApplicationVersionType`, module set, War Sails build. Pin it.
**Exit:** `VERSION_SUPPORT.md` §6 filled with measured values.

### 1.2 Module skeleton + version gate
`MBSubModuleBase`, `SubModule.xml`, behavior registration, `SaveableTypeDefiner` stub. Refuse to load on a non-pinned version with a clear message.
**Exit:** module loads in-game; wrong version refuses cleanly.

### 1.3 API-drift CI gate
`cli_meta.py surface` against pinned assemblies, diff vs committed baseline, fail on removal of anything we bind to.
**Exit:** CI fails on a deliberately introduced baseline change. *Mitigates RISK-05.*

### 1.4 ⚠ `GameNetwork`-in-campaign probe
The four-step experiment in `NETWORK_PROTOCOL.md` §2, behind `ICoopTransport`.
**Exit:** documented, reproducible answer; transport implementation chosen. *Resolves RISK-02.*

### 1.5 Campaign event & determinism harness
Subscribe to all 277 `CampaignEvents`; log ordering, frequency, re-entrancy. Run the same save on two machines and diff.
**Exit:** an ordering/determinism report. *Converts RISK-04 from UNCONFIRMED to measured.*

### 1.6 Identity layer
`MBGUID` registry for `MBObjectBase` types **+ synthesized `CoopShipId`** with fingerprint fallback (`SYNCHRONIZATION_MODEL.md` §4). Decide publicizer vs Harmony for RISK-06.
**Exit:** every relevant object addressable by a stable wire id. *Mitigates RISK-01.*

### 1.7 Transport + wire protocol + idempotent consequence ledger
Implement the chosen transport, channels C0–C5, framing, handshake with version/DLC negotiation. Generate the replicated-state manifest from `[Saveable*]`/`[CachedData]` metadata via `tools/apiscan`. Implement the `ConsequenceId` ledger (`ARCHITECTURE.md` §10) so a duplicated packet can never double-apply loot, gold, XP, casualties or ship changes.
**Exit:** two processes exchange typed messages; handshake rejects mismatches; a deliberately replayed packet is provably applied exactly once.

### 1.8 First vertical slice — party position
One replicated system end-to-end: server-authoritative party movement, client intent, two clients observing each other. Measure bandwidth and snapshot size.
**Exit:** two players see each other move correctly on a shared map. Bandwidth measured. *Informs RISK-10, A4.*

### 1.9 Persistence, reconnect & save safety
Our state through `SaveableTypeDefiner` + `SyncData`. Implement the save-safety layer mandated by `CLAUDE.md` §7 and specified in `SAVE_FORMAT.md` §5: versioned save generations, schema versions, migration chain, backups, atomic write-then-rename, checksum corruption detection. **Run the save/load round-trip test for ship-id stability** and the `SAVE_FORMAT.md` §6 compatibility matrix.
**Exit:** disconnect → reconnect → resume; server restart → clients reconnect; a save interrupted mid-write leaves the previous generation intact and loadable. *Resolves RISK-15, informs RISK-13.*

### 1.10 Naval campaign state
Ships, ownership, fleet composition, at-sea/raft/anchor flags, storms — **campaign layer only, no naval missions**. Ownership via a `ChangeShipOwnerAction` adapter (version-aware per `VERSION_SUPPORT.md` §4.4).
**Exit:** two players see each other's fleets; ownership transfer replicates and survives save/load.

### Testing requirement (all steps)

`CLAUDE.md` requires every meaningful feature to carry **unit, integration, network and save/load tests**, with **end-to-end tests** for major systems. No Phase 1 step is complete without them, and per `CLAUDE.md` §10 no feature may be described as working until tested. Stubs must be explicitly marked as stubs.

**Phase 1 exit criteria:** two players, shared persistent world, synchronized movement and ship ownership, surviving disconnect and server restart, on a pinned version, with CI guarding API drift.

**Explicitly NOT in Phase 1:** naval missions/boarding (RISK-03, RISK-08), combat sync, quests (RISK-11), sieges, armies, economy.

---

## Phase 2 — Core Campaign (sketch)

Settlements & economy · clans, kingdoms, diplomacy · armies · AI party replication at scale · interest management (from 1.8 measurements) · battle *initiation* and auto-resolve · PvP encounters.

## Phase 3 — Battles (sketch)

Land mission hosting · co-op battles · PvP battles · sieges & sally-outs · village raids. Gated on Phase 1.4/1.5 results.

## Phase 4 — Naval Battles (sketch)

**Gated on RISK-03 and RISK-08.** Degradation ladder: (1) auto-resolved naval battles *(Phase 1.10 already enables this)* → (2) single-player-fights, others spectate → (3) full multiplayer naval battles with boarding and capture.

## Phase 5 — Content & Polish (sketch)

Quests (RISK-11) · War Sails storyline · hideouts · persistent progression · performance · dedicated-server tooling.

---

## Sequencing Rationale

- **Identity before transport** — you cannot send what you cannot name; RISK-01 is discoverable and solvable on paper now.
- **Transport probe before netcode** — RISK-02's answer picks the implementation; guessing wastes the most work.
- **Determinism harness before sync breadth** — RISK-04 is measurable cheaply and shapes everything after.
- **One vertical slice before breadth** — proves the whole stack on the smallest possible surface.
- **Persistence before features** — reconnect and restart are requirements, not polish; retrofitting them is expensive.
- **Naval campaign state before naval battles** — campaign-layer naval avoids the RISK-03 seam entirely and delivers visible naval value early.

## Investigation Backlog (needs an install)

| # | Question | Unblocks | Risk |
|---|---|---|---|
| I1 | `GameNetwork` in a campaign session? | All netcode | RISK-02 |
| I2 | Campaign tick determinism & event ordering | Sync model | RISK-04 |
| I3 | `Ship` ↔ `MissionShip` binding lifetime | Naval capture | RISK-03 |
| I4 | Which mutations bump `Ship.VersionNo` | Change detection | RISK-01 |
| I5 | `MapEvent` → naval mission launch path | Naval battles | RISK-08 |
| I6 | Siege stage transitions (refactored across versions) | Sieges | RISK-05 |
| I7 | Save compat with/without War Sails | Persistence | RISK-13 |
| I8 | `MissionShip` authority & physics determinism | Naval battles | RISK-12 |
| I9 | Exact installed version | Version pin | RISK-07 |
