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
`tools/apiscan/check_api_drift.py` runs `cli_meta.py surface` against the pinned reference assemblies (the same package `Coop.GameInterface.csproj` restores) and diffs against a committed baseline (`docs/evidence/api-baseline/`), scoped to the assemblies `Coop.GameInterface` references. Fails on any removal. Wired into `.github/workflows/ci.yml`.
**Exit:** CI fails on a deliberately introduced baseline change. *Mitigates RISK-05.*

### 1.4 ✅ `GameNetwork`-in-campaign probe — done
The four-step experiment in `NETWORK_PROTOCOL.md` §2, run 2026-09-17. Result: `GameNetwork` activates without error but crashes the engine (native access violation) within ~15–40s when forced into multiplayer mode inside a live singleplayer campaign. Transport implementation chosen: `TaleWorlds.Network.TcpSocket` behind `ICoopTransport`, not `GameNetwork`.
**Exit:** documented, reproducible answer; transport implementation chosen. *Resolves RISK-02.*

### 1.5 Campaign event & determinism harness
`tools/campaign-event-harness` subscribes to all 277 `CampaignEvents` generically (reflection +
runtime-built delegates, verified uniform `AddNonSerializedListener` pattern across all arities
— not a hand-picked subset) and logs ordering, frequency, re-entrancy. Built, deployed,
awaiting a play session. The "two machines" diff from the original plan isn't possible with
only one machine available; single-run ordering/frequency data is still a real improvement
over UNCONFIRMED.
**Exit:** an ordering/determinism report. *Converts RISK-04 from UNCONFIRMED to measured.*

### 1.6 Identity layer
`EngineObjectId` (adapts `MBGUID` for `MBObjectBase` types — no registry needed, the engine already guarantees it) **+ synthesized `CoopShipId`** via `CoopShipRegistry`, with `ShipIdentityRebinder`/`ShipFingerprint` implementing the fingerprint fallback (`SYNCHRONIZATION_MODEL.md` §4). RISK-06 decided: publicizer over Harmony, when the need arises — building this needed zero internal access, so nothing to publicize yet.
**Exit:** every relevant object addressable by a stable wire id. *Mitigates RISK-01.* Demonstrated live via `ShipIdentityCampaignBehavior` (logs an id for every ship, every in-game day); cross-session persistence (`SyncData`) and the real save/load round-trip test are Phase 1.9.

### 1.7 Transport + wire protocol + idempotent consequence ledger — done
`Coop.Core.Network`: `TcpCoopTransport` (the chosen transport — plain `System.Net.Sockets`, never `GameNetwork`) and `LoopbackTransport` both implement `ICoopTransport`; `Frame` implements the `[u8 channel][u16 msgType][u32 seq][varint len][payload]` wire format from `NETWORK_PROTOCOL.md` §4, self-delimiting so it doubles as the transport's own stream framing. `Channel` enum covers C0–C5; only C0 (`Handshake`) has real message types so far — C1–C5 arrive with the systems that use them, per this file's own "don't implement the whole project at once." `Coop.Core.Network.Handshake`: `HelloMessage`/`HandshakeValidator` implement the version/DLC negotiation table from `NETWORK_PROTOCOL.md` §5 exactly (including the asymmetric DLC policy) as a pure, unit-tested function. `ConsequenceId`/`ConsequenceLedger` implement `ARCHITECTURE.md` §10's exactly-once guard. `tools/apiscan/cli_meta.py manifest` generates the replicated-state manifest from `[Saveable*]`/`[CachedData]` metadata; run against `Ship`, `MobileParty`, `PartyBase` and committed to `docs/evidence/replication-manifest/`.
**Exit:** proven with real sockets, not just unit tests — `TcpCoopTransportIntegrationTests` starts an actual `TcpCoopTransport` server and client on localhost, exchanges a typed `Frame`, and sends the same frame bytes twice to prove the `ConsequenceLedger` applies the consequence exactly once despite both wire copies arriving. Handshake rejection is proven by `HandshakeValidatorTests`' full policy matrix. Persisting the ledger's applied-set into the save of record (so idempotency survives a restart) is Phase 1.9, not this step.

### 1.8 First vertical slice — party position
One replicated system end-to-end: server-authoritative party movement, client intent, two clients observing each other. Measure bandwidth and snapshot size.
**Exit:** two players see each other move correctly on a shared map. Bandwidth measured. *Informs RISK-10, A4.*

### 1.9 Persistence, reconnect & save safety — core built, live round-trip pending
`Coop.Core.Persistence`: `SaveGenerationStore` (atomic temp-file-then-rename, checksum via SHA-256, corrupted-generation fallback, prune-to-N) and `SchemaMigrationChain` (`vN → vN+1`, refuses an unknown-newer schema, no implicit defaulting) implement `CLAUDE.md` §7 / `SAVE_FORMAT.md` §5 exactly, both fully unit-tested against a real temp directory — including the literal exit-criterion scenario (a write interrupted before its atomic rename leaves the previous generation intact and loadable). `CoopStateSnapshot` + `CoopStateBlobCodecV1` hand-encode our own ship-identity state (sanctioned by `SAVE_FORMAT.md` §7 rule 1 — ids and our own data, never an engine type), versioned via a schema header.

`Coop.GameInterface`: `ShipIdentityCampaignBehavior.SyncData` now persists the ship registry through the engine's own `IDataStore.SyncData<T>` (a Base64 blob of our codec's bytes — `string` being an unambiguously supported basic type sidesteps needing a custom `SaveableTypeDefiner` class registration). On load it defers applying anything until `OnGameLoadFinishedEvent` (real TaleWorlds behaviors consistently do the same; the object graph is confirmed valid by then, though the exact SyncData-vs-OnGameLoadFinished ordering itself is LIKELY, not decompiled to certainty), then runs `ShipIdentityRebinder` against the real, just-reloaded ships and logs mismatches — this **is** the save/load round-trip test for RISK-15, wired and ready; running it against a real save is the one piece that needs you at the keyboard.

**Deferred, and said so rather than silently skipped:** `SaveGenerationStore` isn't wired to a live save trigger yet — there's no dedicated-server host to own an autosave schedule until Phase 1.8 exists. Player-identity bindings (`SAVE_FORMAT.md` §3.1) have a ready, tested codec path but nothing to populate them with — no join flow exists before a real client can connect (also 1.8). The full `SAVE_FORMAT.md` §6 compatibility matrix (War-Sails-on/off save swaps, vanilla↔our-save, cross-version) needs many manual save/reload cycles across mod configurations on a real install and has not been run.
**Exit:** disconnect → reconnect → resume; server restart → clients reconnect — **not yet demonstrable, no session/server host exists to reconnect to (1.8).** A save interrupted mid-write leaves the previous generation intact and loadable — **proven, `SaveGenerationStoreTests`.** *Ship-id round-trip mechanism built and deployed; RISK-15 resolves once you run it. Informs RISK-13 once the compatibility matrix is run.*

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
| ~~I1~~ | ~~`GameNetwork` in a campaign session?~~ | All netcode | **RESOLVED — no, own transport** |
| I2 | Campaign tick determinism & event ordering | Sync model | RISK-04 |
| I3 | `Ship` ↔ `MissionShip` binding lifetime | Naval capture | RISK-03 |
| ~~I4~~ | ~~Which mutations bump `Ship.VersionNo`~~ | Change detection | **RESOLVED** |
| I5 | `MapEvent` → naval mission launch path (partial — decorator seam found, trigger site not) | Naval battles | RISK-08 |
| I6 | Siege stage transitions (refactored across versions) | Sieges | RISK-05 |
| I7 | Save compat with/without War Sails | Persistence | RISK-13 |
| I8 | `MissionShip` authority & physics determinism | Naval battles | RISK-12 |
| I9 | Exact installed version | Version pin | RISK-07 |
