# Network Protocol & Multiplayer Lifecycle

**Goal 29 deliverable.**

---

## 1. Verified Networking Surface

### `TaleWorlds.MountAndBlade.GameNetwork` (VERIFIED — all members read from metadata)

| Group | Members |
|---|---|
| Role | `IsServer`, `IsClient`, `IsDedicatedServer`, `IsMultiplayer`, `IsSessionActive`, `IsReplay`, `IsServerOrRecorder`, `IsClientOrReplay`, `IsMultiplayerOrReplay`, `MultiplayerDisabled` |
| Peers | `NetworkPeers`, `DisconnectedNetworkPeers`, `NetworkPeersIncludingDisconnectedPeers`, `NetworkPeerCount`, `NetworkPeersValid`, `MyPeer`, `IsMyPeerReady`, `VirtualPlayers`, `FindNetworkPeer(int)` |
| Lifecycle | `Initialize(IGameNetworkHandler)`, `PreStartMultiplayerOnServer()`, `StartMultiplayerOnServer(int)`, `StartMultiplayerOnClient(string,int,int,int)`, `InitializeClientSide(string,int,int,int)`, `TerminateClientSide()`, `EndMultiplayer()`, `ClearAllPeers()` |
| Admission | `AddNewPlayerOnServer(PlayerConnectionInfo,bool,bool)`, `AddNewPlayersOnServer(PlayerConnectionInfo[],bool)`, `HandleNewClientConnect(...)`, `HandleNewClientsConnect(...)`, `ClientFinishedLoading(NetworkCommunicator)` |
| **Custom messages** | `BeginModuleEventAsClient()`/`EndModuleEventAsClient()`; `BeginModuleEventAsServer(NetworkCommunicator\|VirtualPlayer)`/`EndModuleEventAsServer()`; `BeginBroadcastModuleEvent()`/`EndBroadcastModuleEvent(EventBroadcastFlags, NetworkCommunicator)`; `…Unreliable` variants of each |
| Handlers | `AddRemoveMessageHandlers(NetworkMessageHandlerRegisterer.RegisterMode)`, `AddNetworkHandler(IUdpNetworkHandler)`, `AddNetworkComponent<T>()`, `DestroyComponent(UdpNetworkComponent)` |
| Disconnect | `AddNetworkPeerToDisconnectAsServer(NetworkCommunicator)`, `UnSynchronizeEveryone()` |
| Mission objects | `GetSynchedMissionObjectReadableRecordTypeFromIndex(int)`, `GetSynchedMissionObjectReadableRecordIndexFromType(Type)` |
| Timing | `ElapsedTimeSinceLastUdpPacketArrived()` |

### `TaleWorlds.Network.dll` (VERIFIED — 39 types)

`TcpSocket`/`TcpStatus`/`TcpMessageReceiverDelegate`/`TcpCloseDelegate` · `NetworkSession`/`ClientsideSession`/`ServersideSession`/`ServersideSessionManager` · `MessageBuffer`, `MessageId`, `MessageContract`, `MessageContractCreator<T>`, `MessageContractHandler<T>`, `MessageContractHandlerManager` · `NetworkMessage`, `INetworkMessageWriter`, `INetworkMessageReader`, `INetworkSerializable` · `MessageProxy`, `MessageServiceConnection`, `ConnectionState` · `RESTClient`, `ClientWebSocketHandler`, `JsonSocketMessage`, `Authorize`, `PostBoxId` · `CoroutineManager` and friends.

## 2. RESOLVED (RISK-02) — `GameNetwork` is not usable for this

| Question | Confidence |
|---|---|
| Is `GameNetwork` usable inside a singleplayer `Campaign` session? | **RESOLVED — no, it crashes the engine** |

### The experiment, as run (Phase 1.4, `tools/network-probe/CoopNetworkProbe`, 2026-09-17)

Ran on the real install (official modules + `NavalDLC` only — no third-party mods, Harmony included, per the RISK-16 clean profile). All four steps from the original plan executed:

1. Logged `GameNetwork.IsSessionActive`/`.IsMultiplayer`/`.MultiplayerDisabled`/`.IsServer`/`.IsClient` on `OnCampaignStart` — all `false`, as expected for a singleplayer session.
2. `GameNetwork.Initialize(...)`, `PreStartMultiplayerOnServer()`, `StartMultiplayerOnServer(7773)` — **all three logged `STEP OK`, no exception.** `IsSessionActive`/`IsMultiplayer`/`IsServer` flipped to `true` immediately after.
3. `AddRemoveMessageHandlers(RegisterMode.Add)` and a loopback `BeginBroadcastModuleEvent()`/`EndBroadcastModuleEvent(...)` — both `STEP OK`.
4. The campaign kept ticking normally for at least 15 seconds afterward (`Campaign.Current` valid, `CampaignTime` stable across three checks).

**Then the game crashed** — a native access violation (`0xc0000005`, Windows Application Error, faulting module reported `unknown`), roughly 15–40 seconds after `StartMultiplayerOnServer`. Not a managed exception; no BUTR crash report. Full evidence and reasoning: `docs/RISK_REGISTER.md` RISK-02.

**Outcome: neither "works" nor "throws."** The managed calls all report success and the flags flip correctly, but forcing a live singleplayer `Campaign` into multiplayer mode destabilizes the engine and crashes it shortly after. **Decision: `GameNetwork` is not used.** The project builds its own transport. `TaleWorlds.Network.TcpSocket` and the `MessageContract` machinery are available and are plain managed types (VERIFIED), or `System.Net.Sockets` directly.

Whether a `Campaign` created multiplayer-aware *from the start* (rather than retrofitted mid-session, which is what was tested) would avoid this is **UNKNOWN and not pursued** — it isn't this project's use case, which is adding co-op to an otherwise-normal singleplayer campaign.

**`GameNetwork` must never be bound to directly.** Everything goes through:

```csharp
interface ICoopTransport {
    void StartServer(int port);
    void Connect(string host, int port);
    void Send(PeerId to, ReadOnlySpan<byte> payload, DeliveryMode mode);
    void Broadcast(ReadOnlySpan<byte> payload, DeliveryMode mode);
    event Action<PeerId, ArraySegment<byte>> Received;
    event Action<PeerId> PeerConnected;
    event Action<PeerId, DisconnectReason> PeerDisconnected;
}
enum DeliveryMode { ReliableOrdered, Unreliable }
```

Phase 1 ships a **loopback** implementation first so higher layers are testable while the real (`TcpSocket`-backed) implementation is built.

## 3. Channels

| Channel | Delivery | Carries |
|---|---|---|
| `C0 Control` | Reliable ordered | Handshake, version/DLC negotiation, auth, disconnect |
| `C1 Snapshot` | Reliable ordered | Full world state on join/reconnect |
| `C2 CampaignDelta` | Reliable ordered | Campaign state diffs, event notifications |
| `C3 Command` | Reliable ordered | Client intents |
| `C4 MissionState` | Unreliable | High-rate in-mission positions/animations |
| `C5 MissionEvent` | Reliable ordered | Mission-critical events (capture, sink, boarding) |

Reliable-ordered is the default; unreliable is used only where loss is genuinely tolerable.

## 4. Wire Format

```
Frame:  [u8 channel][u16 msgType][u32 seq][varint len][payload]
```

Identity encoding:

| Reference | Encoding |
|---|---|
| Any `MBObjectBase` | `u32` = `MBGUID.InternalValue` (VERIFIED: `MBGUID` wraps a `uint`) |
| `MobileParty` | `u32` MBGUID |
| `PartyBase` | `(u8 ownerKind, u32 ownerMBGUID)` — has no id of its own (VERIFIED) |
| **`Ship`** | **`u64 CoopShipId`** — server-assigned synthetic id (`SYNCHRONIZATION_MODEL.md` §4.3) |
| `Settlement`, `Hero`, `Clan`, `Kingdom` | `u32` MBGUID |
| `MapEvent` | `u32` server-assigned battle id |
| String ids | Interned: negotiated dictionary on handshake, `u16` thereafter |

**Rule:** the wire never carries object references or indices into a client-side list. Only ids that the server issued or that the engine persists.

## 5. Lifecycle State Machines

### Server

```
Boot → LoadSaveOfRecord → RegistriesRehydrated → Listening
     → [per client] Handshaking → Authorizing → Snapshotting → Live
Live → (periodic) Autosave → Live
Shutdown → FlushSaveOfRecord → Closed
```

### Client

```
Disconnected → Connecting → Handshaking → VersionCheck
             → ReceivingSnapshot → Live
Live → ConnectionLost → Reconnecting → (resume at Handshaking)
Live → Disconnect → Disconnected
```

### Handshake (C0)

```
C→S  Hello { protocolVersion, gameVersion, gameVersionType, moduleSet, navalDlcBuild?, playerIdentity }
S→C  HelloAck | Reject { reason }
```

Rejection reasons: protocol mismatch · game version mismatch · **DLC mismatch** · module-set mismatch · banned · server full.

**DLC negotiation is explicit** (VERIFIED APIs: `ApplicationVersion`, `ApplicationVersionType`, `ModuleInfo`, `NavalVersion.GetApplicationVersionBuildNumber()`). Policy:

| Server | Client | Outcome |
|---|---|---|
| War Sails on | War Sails on | Full naval |
| War Sails on | War Sails off | **Reject** in Phase 1 (naval campaign state would be unrepresentable). Revisit later. |
| War Sails off | War Sails on | Allow; naval systems inert |
| War Sails off | War Sails off | Standard co-op |

## 6. Disconnect / Reconnect

| Event | Server behaviour |
|---|---|
| Client disconnects | Party remains in the world, frozen (no client intent accepted). All state retained. Detected via `PeerDisconnected` / `GameNetwork.DisconnectedNetworkPeers` if that transport is used. |
| Disconnect mid-battle | Server resolves the `MapEvent` authoritatively. `CampaignEvents.PlayerDesertedBattleEvent` (VERIFIED) is a relevant observation hook. |
| Reconnect | Re-handshake → fresh snapshot → resume control of the same party, matched by persisted player identity → `MBGUID`. |
| Reconnect after server restart | Identical path: server reloaded the save of record and rehydrated registries, so ids resolve. |

**Requirement:** the player-identity → party `MBGUID` binding must live in the **save of record**, not in memory. Implemented via our `CampaignBehaviorBase.SyncData` (VERIFIED API). Without this, reconnect after restart cannot work.

## 7. Server Restart

```
Shutdown  → flush save of record (TaleWorlds save pipeline + our SyncData blocks)
Restart   → load that save → rebuild MBGUID registry from MBObjectManager
                           → rebuild CoopShipId registry from persisted per-party id lists
                           → rebuild identity→party bindings
          → Listening; clients reconnect and resync
```

**Gate:** ship-registry rehydration rests on the `LIKELY` list-ordering assumption (`SYNCHRONIZATION_MODEL.md` §4.3). Must pass the Phase 1.9 round-trip test before naval state is trusted across restarts.

## 8. Security

Threat model: griefing and cheating among semi-trusted players, not hostile internet.

| Control | Mechanism |
|---|---|
| Server authority | Clients cannot write campaign state at all |
| Intent validation | Every command re-validated: ownership, range, legality, affordability |
| Id validation | Client ids resolved against server registries; unknown/unowned → reject + log |
| Rate limiting | Per-peer command budget |
| Identity | Platform identity at handshake; server-side ban list |
| Message bounds | Length caps, type whitelist, decode in a guarded context |

## 9. Open Protocol Questions

| # | Question | Confidence | Gate |
|---|---|---|---|
| P1 | `GameNetwork` in campaign? | UNCONFIRMED | Phase 1.4 |
| P2 | Full-broadcast bandwidth at 8 players | UNCONFIRMED | Phase 1.8 measurement |
| P3 | Can a naval `Mission` host multiple network players? | UNKNOWN | Needs install |
| P4 | Is `GetSynchedMissionObjectReadableRecordType*` usable for `MissionShip`? | UNCONFIRMED | RISK-03 |
| P5 | Snapshot size / join time for a mature campaign | UNCONFIRMED | Phase 1.8 |
