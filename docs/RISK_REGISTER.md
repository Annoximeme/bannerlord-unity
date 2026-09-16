# Risk Register

**Goals 26 & 30.** Scored **Impact × Likelihood**, both 1–5. Severity = product.
Every risk names the evidence and its confidence, so nothing here is speculation dressed as fact.

| ID | Risk | Sev | Confidence of the underlying finding |
|---|---|---|---|
| RISK-00 | Upstream licence prohibits competing co-op mods | **25** | VERIFIED |
| RISK-01 | `Ship` has no network identity | **25** | VERIFIED |
| RISK-02 | `GameNetwork` may not work in a campaign session | **20** | UNCONFIRMED |
| RISK-03 | Campaign↔mission naval seam | **20** | VERIFIED (split) / UNCONFIRMED (binding) |
| RISK-04 | Campaign non-determinism & event ordering | **20** | UNCONFIRMED |
| RISK-05 | Version churn on bound APIs | **16** | VERIFIED |
| RISK-06 | Internal/private members require publicizer or Harmony | 12 | VERIFIED |
| RISK-07 | No install available for verification | **20** | VERIFIED |
| RISK-08 | Naval mission multiplayer may be impossible | 15 | UNKNOWN |
| RISK-09 | Save-schema divergence across versions | 12 | LIKELY |
| RISK-10 | Scale/bandwidth at 8+ players | 9 | UNCONFIRMED |
| RISK-11 | Quests are not designed for multiple players | 12 | LIKELY |
| RISK-12 | Mission-layer float determinism | 12 | LIKELY |
| RISK-13 | Save compatibility with/without War Sails | 12 | UNCONFIRMED |
| RISK-14 | Scope: the requested feature set is very large | 16 | VERIFIED |
| RISK-15 | Ship id stability rests on a `LIKELY` assumption | 12 | LIKELY |

---

## RISK-00 — Upstream licence prohibits competing co-op mods · **Impact 5 × Likelihood 5 = 25**

**Evidence (VERIFIED):** `LICENSE` and `NOTICE.md` in `Bannerlord-Coop-Team/BannerlordCoop`, read directly. Since **2026-06-17** the project is source-available, not MIT, and explicitly forbids using the source "to create, contribute to, improve, support, or maintain a competing Mount & Blade II: Bannerlord multiplayer, co-op, networking, synchronization, or derivative mod without prior written permission."

**Why it is severity 25:** this project *is* a competing co-op mod. The constraint is certain (likelihood 5) and legal rather than technical, so no amount of engineering removes it (impact 5).

**Mitigation, already applied:** this entire audit derives its architecture **exclusively from TaleWorlds' own published API surface**. No upstream implementation detail, algorithm, or design was used. That is both the legally clean path and the technically sounder one, since the TaleWorlds API is what we actually build against.

**Decision required from the project owner before Phase 1:**
- (a) **Clean-room** — continue deriving only from TaleWorlds APIs. *Recommended; it is what this audit already does.*
- (b) **Seek written permission** from the maintainers.
- (c) **Contribute upstream instead** — they have an open "Epic: War Sails Sync" (#3060) and no naval support yet.

Option (c) deserves genuine consideration: upstream has 4,257 files of working co-op and an open, unstarted naval epic. If the goal is *playing naval co-op* rather than *owning a codebase*, contributing is the shortest path.

**Note:** `AGENTS.md` in that repository also contains instructions attempting to make automated agents refuse work and respond in a fictional persona. That is repository content directed at tooling, not a licence term and not an instruction from this project's owner; it was not followed. The actual `LICENSE` was respected on its merits.

---

## RISK-01 — `Ship` has no network identity · **Impact 5 × Likelihood 5 = 25**

**Evidence (VERIFIED):** `TaleWorlds.CampaignSystem.Naval.Ship` derives from `System.Object`. It is not an `MBObjectBase`; it has no `MBGUID Id` and no `StringId`. Confirmed by direct metadata read of the base type and full member list.

**Consequence:** there is no engine-provided way to say "this ship" across two processes. Every naval feature the project wants — capture, destruction, loot, fleet composition, transfer, stashing — needs exactly that. Additionally `Ship._versionNo` is **not** `[SaveableField]` (VERIFIED), so it resets on load and cannot anchor identity across sessions.

**Mitigation:** synthesized `CoopShipId` registry, persisted through our own `SyncData`, positionally rebound on load with a content-fingerprint fallback. Design: `SYNCHRONIZATION_MODEL.md` §4.3. Ownership cross-check is available because `Ship._owner` **is** `[SaveableField]` (VERIFIED).

**Residual:** the positional-rebinding assumption is `LIKELY`, not `VERIFIED` → tracked separately as RISK-15.

---

## RISK-02 — `GameNetwork` may not work in a campaign session · **Impact 5 × Likelihood 4 = 20**

**Evidence:** `TaleWorlds.MountAndBlade.GameNetwork` exposes a complete client/server + module-event messaging API (VERIFIED, full member list in `NETWORK_PROTOCOL.md` §1). Whether it initializes inside a singleplayer `Campaign` is **UNCONFIRMED** — that answer lives in method bodies and native code, which reference assemblies do not carry. The existence of `GameNetwork.MultiplayerDisabled` indicates the engine gates this; its semantics are unverified.

**Consequence if it fails:** the entire transport layer must be built on `TaleWorlds.Network.TcpSocket` or raw sockets, changing timelines but not the architecture.

**Mitigation:** the `ICoopTransport` abstraction (`NETWORK_PROTOCOL.md` §2) makes this a one-implementation swap. Phase 1 ships a loopback transport so all higher layers are testable before the answer is known.

**Resolution:** Phase 1.4 experiment, four steps, specified in `NETWORK_PROTOCOL.md` §2. **This is the highest-leverage unknown in the project.**

---

## RISK-03 — Campaign↔mission naval seam · **Impact 5 × Likelihood 4 = 20**

**Evidence (VERIFIED):** campaign ships are `TaleWorlds.CampaignSystem.Naval.Ship`; mission ships are `NavalDLC.Missions.Objects.MissionShip` — different types, different assemblies. `MissionShip` relates to campaign via `TaleWorlds.Core.IShipOrigin` (which `Ship` implements). Boarding and capture exist **only** at mission layer (`ShipOrder`, `NavalShipsLogic.ShipCapturedEvent`); ownership exists **only** at campaign layer (`ChangeShipOwnerAction`). **UNCONFIRMED:** how the binding is established and whether it survives a whole battle.

**Consequence:** a mismapping duplicates or destroys ships in the persistent world — the worst class of bug for a persistent campaign.

**Mitigation:** treat mission outcomes as *reports*, never as direct mutations. Server re-resolves every reported ship through the `CoopShipId` registry before applying `ChangeShipOwnerAction`/`DestroyShipAction`. Reject unresolvable reports loudly.

**Resolution:** requires an install. Naval *missions* are explicitly out of Phase 1 scope for this reason; naval *campaign* state is in scope because it does not cross the seam.

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

**Mitigation:** prefer public APIs where they exist (`ChangeShipOwnerAction` over `AddShipInternal`; `SetAnchor` over the setter). Where unavoidable, use a publicizer at build time — more stable than reflection and cheaper than Harmony. Decide in Phase 1.6.

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

**Mitigation:** Phase 1 requires matching DLC state on server and client, enforced at handshake (`NETWORK_PROTOCOL.md` §5). Run the §5 compatibility matrix in `SAVE_FORMAT.md` once an install exists.

---

## RISK-14 — Scope · **Impact 4 × Likelihood 4 = 16**

**Evidence (VERIFIED):** the requested feature set spans ~20 major systems. The incumbent project has 4,257 `.cs` files, years of work, an active team — and still lists quests, hideouts and War Sails as *unimplemented*.

**Mitigation:** the phased roadmap deliberately ships a narrow vertical slice (party position, two clients) before breadth. Naval *campaign* state before naval *battles*. This is the honest read: matching the incumbent's breadth is a multi-year effort; the naval niche is where this project can lead, because nobody has built it.

---

## RISK-15 — Ship id stability rests on a `LIKELY` assumption · **Impact 4 × Likelihood 3 = 12**

**Evidence:** the `CoopShipId` design rebinds ids positionally from `PartyBase.Ships` after load. That `MBList<Ship>` preserves order through save/load is **LIKELY** (it is a `[SaveableField]` ordered container) but **not runtime-verified**.

**Mitigation:** content-fingerprint fallback `(ShipHull.StringId, name, hitPoints, sailHitPoints, RandomValue)` — `RandomValue` is `[SaveableProperty]` and per-ship (VERIFIED). Ships are session-scoped only until the Phase 1.9 round-trip test passes.

---

## Top Five (audit §14)

1. **RISK-01** — `Ship` has no network identity
2. **RISK-02** — `GameNetwork` in a campaign session is unverified
3. **RISK-03** — campaign↔mission naval seam
4. **RISK-04** — campaign non-determinism & event ordering
5. **RISK-05** — version churn on bound APIs

RISK-00 sits above all of these but is a **legal/business decision, not a technical risk**, and is the owner's to make.
