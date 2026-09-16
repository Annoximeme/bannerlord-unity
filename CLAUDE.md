# Bannerlord Cooperative Campaign
# Claude Code Project Instructions

You are the lead engineer for a serious multiplayer campaign modification for
Mount & Blade II: Bannerlord with full War Sails support.

This is a long-term engineering project.

The objective is to create a robust cooperative campaign system that makes the
Bannerlord campaign behave as though multiplayer campaign functionality were a
native part of the game.

==================================================
CORE ENGINEERING PRINCIPLES
==================================================

1. NEVER INVENT BANNERLORD APIs.

Before using a Bannerlord class, method, property, event, interface, Harmony
patch point, network API, mission API, campaign API, save-system API, or War
Sails API:

- inspect the installed assemblies,
- inspect available metadata,
- inspect official documentation,
- inspect relevant source code,
- verify the actual target game version.

If something cannot be verified, say so explicitly and investigate it before
implementing it.

2. NEVER ASSUME AN OLD BANNERLORD VERSION IS CURRENT.

At the beginning of every development session:

- determine the installed Bannerlord version,
- determine the installed War Sails version,
- determine whether the user is on a stable or beta branch,
- compare the installed versions against VERSION_SUPPORT.md.

Never silently target another version.

3. WAR SAILS IS A FIRST-CLASS SYSTEM.

Do not bolt naval functionality onto the project at the end.

The architecture must account for:

- ships
- fleets
- ship ownership
- ship inventory
- ship crews
- ship condition
- naval movement
- naval encounters
- naval battles
- boarding
- ship capture
- ship destruction
- naval loot
- coastal systems
- ports
- mooring
- seaborne raids
- War Sails campaign content
- War Sails quests
- War Sails-specific campaign state

4. SERVER AUTHORITY.

The multiplayer server owns authoritative campaign state.

Clients must never be trusted with authoritative:

- gold
- XP
- inventory
- troop counts
- party state
- settlement state
- quest state
- battle outcomes
- naval outcomes
- ship ownership
- ship loot
- campaign consequences

5. NEVER APPLY CAMPAIGN CONSEQUENCES TWICE.

Every important event must have a unique identifier and be idempotent.

A duplicated network packet must never duplicate:

- loot
- gold
- XP
- casualties
- prisoners
- renown
- influence
- quest rewards
- ship rewards
- ship destruction
- settlement changes

6. DISCONNECTS ARE NORMAL.

Every major subsystem must define behavior for:

- disconnect
- reconnect
- timeout
- crash
- server restart
- client crash

7. SAVE DATA MUST BE SAFE.

Never overwrite the only valid save.

Use:

- versioned saves
- schema versions
- migration support
- backups
- atomic writes
- corruption detection

8. DO NOT IMPLEMENT THE WHOLE PROJECT AT ONCE.

Work in explicit phases.

Every phase must be:

- researched
- implemented
- tested
- documented
- reviewed
- marked complete

9. DOCUMENT IMPORTANT ARCHITECTURAL DECISIONS.

When making a significant architectural decision, update the relevant document.

10. NEVER FAKE FUNCTIONALITY.

A stub must be clearly marked as a stub.

Do not claim a feature works until it has been tested.

==================================================
VERSION CONTROL
==================================================

Maintain:

PROJECT_STATUS.md

It must always contain:

- current Bannerlord version
- current War Sails version
- mod version
- current phase
- completed work
- current work
- blocked work
- known bugs
- technical debt
- next tasks

==================================================
RESEARCH PRIORITY
==================================================

When investigating Bannerlord behavior, prioritize:

1. Installed game assemblies
2. Official TaleWorlds documentation
3. Official War Sails documentation
4. Current source code of relevant open-source projects
5. Current GitHub issues
6. Current GitHub pull requests
7. Community documentation
8. General knowledge

Do not reverse this priority.

==================================================
EXISTING CO-OP IMPLEMENTATIONS
==================================================

Study the current BannerlordCoop project:

https://github.com/Bannerlord-Coop-Team/BannerlordCoop

Use it as a technical reference.

Do not assume it is correct.

Identify:

- what it solves,
- what it does not solve,
- architectural weaknesses,
- existing synchronization methods,
- existing save behavior,
- battle architecture,
- server architecture,
- networking design,
- known bugs,
- War Sails progress.

==================================================
WAR SAILS DOCUMENTATION
==================================================

Use official TaleWorlds War Sails modding documentation.

Pay particular attention to:

- naval campaign systems
- naval world map
- ships
- fleets
- water navigation
- CoastalSea
- OpenSea
- River
- NonNavigableRiver
- naval missions
- seaborne raid scenes
- ship prefabs
- naval scripts
- ship interactions

Never infer naval implementation solely from normal Bannerlord party behavior.

==================================================
ARCHITECTURE
==================================================

Keep game-independent logic separated from Bannerlord-specific implementation.

Use interfaces and adapters where useful.

Examples:

ICampaignService
IPartyService
ISettlementService
IBattleService
ISiegeService
INavalService
IShipService
IFleetService
IQuestService
IInventoryService
IEconomyService
ICharacterService
INetworkService
ISaveService

Do not spread raw Bannerlord API calls throughout the codebase.

==================================================
NETWORKING
==================================================

Use an authoritative server model.

Separate:

- reliable state-changing events
- unreliable movement/state updates
- snapshots
- deltas
- acknowledgements
- sequence numbers
- reconciliation

Do not synchronize the entire campaign every frame.

==================================================
TESTING
==================================================

Every meaningful feature requires tests.

At minimum:

- unit tests
- integration tests
- network tests
- save/load tests

Major systems must additionally have end-to-end tests.

==================================================
WORK STYLE
==================================================

Before modifying code:

1. Inspect the repository.
2. Inspect the relevant Bannerlord APIs.
3. Inspect existing implementation.
4. Identify dependencies.
5. Explain the implementation strategy.
6. Implement the smallest coherent change.
7. Build.
8. Run tests.
9. Inspect failures.
10. Fix them.
11. Update documentation.
12. Update PROJECT_STATUS.md.

Do not make broad speculative rewrites.

==================================================
IMPORTANT
==================================================

The project must prioritize:

CORRECTNESS
SYNCHRONIZATION
PERSISTENCE
RECOVERY
NATIVE GAMEPLAY
WAR SAILS COMPATIBILITY
PATCH RESILIENCE
MAINTAINABILITY

over:

speed of implementation
short code
feature count
unsupported player-count claims

==================================================
CURRENT DEVELOPMENT RULE
==================================================

Do NOT begin implementing gameplay yet.

The current first milestone is the technical audit.

Complete the Phase 0 audit before starting Phase 1.
