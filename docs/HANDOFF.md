# Session Handoff — Cloud → Local

**Written:** 2026-09-16, at the end of the cloud audit session.
**Read this first if you are a local session picking up this project.**

---

## Where the project stands

**Phase 0 (Technical Audit) is COMPLETE.** No gameplay code exists — `CLAUDE.md` forbids starting Phase 1 until the audit is reviewed. The audit is in `docs/INITIAL_TECHNICAL_AUDIT.md`; current state is in `PROJECT_STATUS.md`.

### Version is pinned (measured, not assumed)

| Field | Value |
|---|---|
| Bannerlord | **`v1.4.8`**, changeset `119303` (LIKELY — module XML omits the changeset) |
| Branch | **STABLE** — Steam public/live, `BetaKey "public"`, buildid `24573425` |
| War Sails | **installed** — `NavalDLC` module **`v1.2.8`** (independent version line) |
| Modules | 24 — 9 official, **15 third-party** |
| Install | `G:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord` |

The audit was conducted against `Bannerlord.ReferenceAssemblies.* 1.4.8.119303`, which **matches this install exactly** — type and surface counts agree across all 11 assemblies. Every `VERIFIED` audit finding applies.

`CLAUDE.md` §2 requires a version check at the start of every session. Re-run it:

```powershell
python tools\apiscan\collect_install_report.py "G:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
```

If it disagrees with the table above, the game updated — **stop and re-pin** before doing anything else.

---

## First actions for the local session

1. **`git pull`** — the cloud session pushed through commit `7a86c96`.
2. **Commit the install report** (TD10). It was generated locally and is untracked:
   ```powershell
   git add docs/install-report
   git commit -m "Add install report from target machine"
   git push
   ```
   Then diff the install's real surface against the audit baseline to upgrade "counts match" to "byte-identical":
   ```powershell
   python tools\apiscan\cli_meta.py surface "G:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\bin\Win64_Shipping_Client\..\..\..\..\Native\bin\Win64_Shipping_Client\TaleWorlds.CampaignSystem.dll"
   ```
   (or just diff `docs/install-report/surface/TaleWorlds.CampaignSystem.dll.surface.txt` against the Phase 0 baseline).
3. ~~**Do not start Phase 1** until RISK-00 and RISK-16 are decided by the project owner.~~ **Both decided 2026-09-17 — see below. Phase 1 is unblocked.**

---

## Two decisions made 2026-09-17 (owner)

| ID | Decision |
|---|---|
| **RISK-00** | **Clean-room.** Continue deriving architecture and implementation exclusively from TaleWorlds' own API surface; the incumbent BannerlordCoop project may be studied for public facts only, never for implementation detail to port or adapt. Seeking permission and contributing upstream were considered and declined. Full record: `docs/RISK_REGISTER.md` RISK-00. |
| **RISK-16** | **Clean dev profile.** Development and testing use official modules + `Bannerlord.Harmony` only; `Bannerlord.Diplomacy`, `ImprovedGarrisons`, `RaiseYourBanner`, `DisableCompanionDonations`, and `NoWaterEscape` must be disabled in the build/test profile. Full record: `docs/RISK_REGISTER.md` RISK-16. |

---

## What a local session unlocks that the cloud one could not

The cloud session ran in an isolated Linux VM with no access to `G:\`. These were blocked there and are now actionable:

| ID | Item | Needs |
|---|---|---|
| **B3** | `GameNetwork`-in-campaign probe (**RISK-02** — the highest-leverage unknown) | A running game. Procedure: `docs/NETWORK_PROTOCOL.md` §2 |
| **B6** | Which mutations bump `Ship.VersionNo` | Method bodies (real IL) |
| **B7** | `MapEvent` → naval mission launch path | Method bodies |
| **B8** | Siege stage transition sequence | Method bodies |
| **B9** | Save compat with/without War Sails (**RISK-13**) | A running game |
| **B10** | `MissionShip` authority & physics determinism | A running game |

**B6–B8 need a decompiler.** `tools/apiscan/cli_meta.py` reads type/member metadata but deliberately does not decode IL. Use [ILSpy](https://github.com/icsharpcode/ILSpy) or `dotnet tool install -g ilspycmd`.

**Suggested order:** B3 first — it decides the transport, and every other networking decision waits on it.

---

## What the cloud session built

| Artefact | Purpose |
|---|---|
| `docs/INITIAL_TECHNICAL_AUDIT.md` | The audit — all 30 goals, evidence tables, confidence levels |
| `docs/ARCHITECTURE.md` | Topology, layers, authority, the 14 service interfaces, idempotency ledger |
| `docs/WARSAILS_ARCHITECTURE.md` | Naval API map incl. water navigation / `TerrainType` |
| `docs/SYNCHRONIZATION_MODEL.md` | Ownership tiers, metadata-driven authority, ship-identity design |
| `docs/NETWORK_PROTOCOL.md` | Transport, channels, wire format, lifecycle, reconnect |
| `docs/SAVE_FORMAT.md` | Persistence + save-safety requirements |
| `docs/RISK_REGISTER.md` | 17 scored risks |
| `docs/VERSION_SUPPORT.md` | Pinned target, measured API churn, detection |
| `docs/ROADMAP.md` | Phases 0.5 → 5 with exit criteria |
| `docs/LOCAL_SETUP.md` | Local dev environment setup |
| `tools/apiscan/` | ECMA-335 metadata reader + install collector |
| `docs/evidence/` | Raw API diffs, 1.4.8-stable vs 1.5.3-beta |

### Key findings to carry forward

1. **The naval data model is in the base game, not the DLC** — `Naval.Ship`, `ChangeShipOwnerAction`, `PartyBase.Ships`, `MapEvent.IsNavalMapEvent` are all in `TaleWorlds.CampaignSystem.dll`. Ship ownership syncs without a DLC dependency.
2. **`Ship` has no network identity** — no `MBGUID`, no `StringId`, and its version counter is not persisted. **RISK-01**, the largest technical risk. Synthesized-id design in `SYNCHRONIZATION_MODEL.md` §4.3.
3. **The engine declares what to replicate** — `[SaveableField]`/`[SaveableProperty]` = authoritative, `[CachedData]` = derived and must never be replicated. Machine-readable via `tools/apiscan`.
4. **Water navigation is base-game terrain data** — `CoastalSea`/`OpenSea`/`River`/`NonNavigableRiver` are `TaleWorlds.Core.TerrainType`. Terrain must not be replicated; **wind and storms must be**.
5. **The incumbent mod does not support War Sails** — planned only, its epic is open and unstarted. Naval co-op is unbuilt by anyone.

### Reproducing the reference-assembly baseline locally

Not required (baselines are committed in `docs/evidence/`), but if needed — `docs/VERSION_SUPPORT.md` §8.

---

## Conventions

- Branch: **`main`**. Keep using it so history stays linear.
- **Never commit game DLLs** — `.gitignore` excludes `*.dll`; leave that rule.
- Confidence discipline (`CLAUDE.md`): `VERIFIED` / `LIKELY` / `UNCONFIRMED` / `UNKNOWN`. Nothing below `VERIFIED` may become an implementation assumption.
- Keep `PROJECT_STATUS.md` current — `CLAUDE.md` mandates its fields.
