# CLAUDE.md — Project Working Agreement

> **Note on provenance:** the Phase 0 brief referred to this file as already defining the audit, but the repository was empty at audit start (zero commits, no `CLAUDE.md`). This file was authored on 2026-09-16 from the Phase 0 brief so that the rules are captured for future sessions. **Review and correct it** — where it differs from your intent, your intent wins.

---

## 1. Project

A cooperative multiplayer campaign mod for Mount & Blade II: Bannerlord, **including War Sails (NavalDLC) support**.

Target capabilities: multiple independent player parties · shared campaign world · PvP · co-op battles · sieges · armies · kingdoms · economy · quests · persistent progression · disconnect/reconnect · server restarts · naval travel · ships · fleets · naval battles · boarding · ship capture · ship destruction · naval loot · seaborne raids · War Sails campaign content.

## 2. The Confidence Discipline (non-negotiable)

Every technical claim in this project carries a confidence level:

| Level | Meaning |
|---|---|
| **VERIFIED** | Read directly from shipped assembly metadata, a primary source, or an executed test. Reproducible. |
| **LIKELY** | Strongly implied by verified evidence, but not itself observed. |
| **UNCONFIRMED** | Plausible; needs a runtime experiment or a source we lack. |
| **UNKNOWN** | No evidence either way. |

**The rule: `LIKELY`, `UNCONFIRMED` and `UNKNOWN` findings must never become implementation assumptions.** They become tracked investigations in `docs/ROADMAP.md` and risks in `docs/RISK_REGISTER.md`.

When you meet an unknown API: **investigate it, don't guess.** If it cannot be investigated in the current environment, say so explicitly and record what would resolve it.

### Recording a finding

Every important finding states: exact class/type · exact assembly · exact method/property/event · source/reference · confidence · whether directly verified.

## 3. Phases

| Phase | Scope | Status |
|---|---|---|
| **0** | Technical audit — no gameplay code | ✅ Complete |
| **0.5** | Unblock: licensing decision, obtain install, pin version | ⬜ Blocked on owner |
| **1** | Foundations: identity, transport, one vertical slice, persistence, naval campaign state | ⬜ Not started |
| **2** | Core campaign breadth | ⬜ |
| **3** | Land battles & sieges | ⬜ |
| **4** | Naval battles (gated on RISK-03/RISK-08) | ⬜ |
| **5** | Content & polish | ⬜ |

**Do not start a phase before its predecessor's exit criteria are met** (`docs/ROADMAP.md`).

## 4. Hard Constraints

1. **Licensing (RISK-00).** `Bannerlord-Coop-Team/BannerlordCoop` has been source-available since 2026-06-17 and explicitly prohibits use of its source in a competing co-op mod. **Do not copy, port, translate, adapt or derive from it.** Derive from TaleWorlds' published API surface only. Their repository may be *referenced* as public context (it exists, its README, its issues) but never as an implementation source.
2. **Pin one game version.** 2,638 CampaignSystem members changed between 1.4.8-stable and 1.5.3-beta. Hard version gate at module load. See `docs/VERSION_SUPPORT.md`.
3. **Server is authoritative.** Clients send intent, never state. No client-side campaign mutation.
4. **Never replicate `[CachedData]`.** Replicate `[SaveableField]`/`[SaveableProperty]`. This is readable from metadata — enforce it mechanically.
5. **Fail loudly.** A missing registry entry or unresolvable id is a hard error, not a silent default. Silent defaults corrupt persistent worlds.
6. **Transport stays behind `ICoopTransport`** until RISK-02 is resolved.

## 5. Tooling

`tools/apiscan/cli_meta.py` — ECMA-335 metadata reader; requires only Python 3 (no .NET toolchain).

```bash
python3 tools/apiscan/cli_meta.py asminfo <dll>...            # version / type count
python3 tools/apiscan/cli_meta.py types   <dll> [regex]       # list types
python3 tools/apiscan/cli_meta.py type    <dll> <FullName>... # full member dump incl. attributes
python3 tools/apiscan/cli_meta.py grep    <regex> <dll>...    # search members across assemblies
python3 tools/apiscan/cli_meta.py attrs   <regex> <dll>...    # find members carrying an attribute
python3 tools/apiscan/cli_meta.py refs    <dll>               # assembly references
python3 tools/apiscan/cli_meta.py surface <dll>               # normalized, diffable API listing
```

Reference assemblies come from `Bannerlord.ReferenceAssemblies.*` on nuget.org (see `docs/VERSION_SUPPORT.md` §8).

## 6. Documents

| File | Purpose |
|---|---|
| `PROJECT_STATUS.md` | Current state; read first |
| `docs/INITIAL_TECHNICAL_AUDIT.md` | Phase 0 audit, all 30 goals |
| `docs/ARCHITECTURE.md` | Topology, layers, authority |
| `docs/WARSAILS_ARCHITECTURE.md` | Naval API map |
| `docs/SYNCHRONIZATION_MODEL.md` | Ownership & identity |
| `docs/NETWORK_PROTOCOL.md` | Transport, wire format, lifecycle |
| `docs/SAVE_FORMAT.md` | Persistence |
| `docs/RISK_REGISTER.md` | Scored risks |
| `docs/VERSION_SUPPORT.md` | Versions & API churn |
| `docs/ROADMAP.md` | Phases & exit criteria |
| `docs/evidence/` | Raw API dumps and diffs |

Keep `PROJECT_STATUS.md` current. Update `docs/RISK_REGISTER.md` when a risk's confidence changes — especially when an `UNCONFIRMED` item becomes measured.
