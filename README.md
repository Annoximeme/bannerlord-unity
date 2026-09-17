# Bannerlord: Unity

A cooperative multiplayer campaign mod for **Mount & Blade II: Bannerlord**, with first-class support for the **War Sails (NavalDLC)** expansion.

By Annoximeme (Gianni).

## Status

**Phase 0 — Technical Audit: complete. Phase 1 — Foundations: in progress (module skeleton, no gameplay features yet).**

Start with **[`PROJECT_STATUS.md`](PROJECT_STATUS.md)**.

## Goals

Multiple independent player parties · shared persistent campaign world · PvP · cooperative battles · sieges · armies · kingdoms · economy · quests · persistent progression · disconnect/reconnect · server restarts · **naval travel, ships, fleets, naval battles, boarding, ship capture, ship destruction, naval loot, seaborne raids, and War Sails campaign content**.

## Documentation

| Document | Purpose |
|---|---|
| [`PROJECT_STATUS.md`](PROJECT_STATUS.md) | Current state and decisions needed |
| [`docs/INITIAL_TECHNICAL_AUDIT.md`](docs/INITIAL_TECHNICAL_AUDIT.md) | The Phase 0 audit |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | System architecture |
| [`docs/WARSAILS_ARCHITECTURE.md`](docs/WARSAILS_ARCHITECTURE.md) | Naval/War Sails API map |
| [`docs/SYNCHRONIZATION_MODEL.md`](docs/SYNCHRONIZATION_MODEL.md) | Ownership & identity model |
| [`docs/NETWORK_PROTOCOL.md`](docs/NETWORK_PROTOCOL.md) | Protocol & multiplayer lifecycle |
| [`docs/SAVE_FORMAT.md`](docs/SAVE_FORMAT.md) | Save-data architecture |
| [`docs/RISK_REGISTER.md`](docs/RISK_REGISTER.md) | Scored risk register |
| [`docs/VERSION_SUPPORT.md`](docs/VERSION_SUPPORT.md) | Version support & API churn |
| [`docs/ROADMAP.md`](docs/ROADMAP.md) | Phased plan |
| [`CLAUDE.md`](CLAUDE.md) | **Project engineering instructions — authoritative** |
| [`docs/HANDOFF.md`](docs/HANDOFF.md) | **Start here if picking this up in a new session** |
| [`docs/LOCAL_SETUP.md`](docs/LOCAL_SETUP.md) | Local dev environment setup on Windows; GitHub push setup |

## Source

[`src/`](src/) — the mod itself: `Coop.Core` (game-agnostic logic, unit-tested), `Coop.GameInterface` (the Bannerlord module). See [`src/README.md`](src/README.md) to build, test, and deploy it locally.

## Tooling

[`tools/apiscan/`](tools/apiscan/) — a dependency-free ECMA-335 metadata reader used to verify every API claim in the docs directly against shipped TaleWorlds assemblies, with no .NET toolchain or game install required.

[`tools/network-probe/`](tools/network-probe/) — the (now-resolved) RISK-02 experiment; kept as a record.

## Note on the existing BannerlordCoop project

[`Bannerlord-Coop-Team/BannerlordCoop`](https://github.com/Bannerlord-Coop-Team/BannerlordCoop) is an established Bannerlord co-op mod. **Since 2026-06-17 it is source-available, not MIT**, and its licence explicitly prohibits use of its source in a competing co-op mod.

This project therefore derives its architecture **exclusively from TaleWorlds' own published API surface**. No code, algorithm or design from that project is used anywhere. See RISK-00 in [`docs/RISK_REGISTER.md`](docs/RISK_REGISTER.md).
