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

## 2. ⚠ The Blocking Unknown (RISK-02)

| Question | Confidence |
|---|---|
| Is `GameNetwork` usable inside a singleplayer `Campaign` session? | **UNCONFIRMED** |

The API exists and is complete. Whether the native transport initializes when the game is in campaign mode rather than a multiplayer game mode **cannot be answered from metadata** — that logic is in method bodies and native code. `GameNetwork.MultiplayerDisabled` shows the engine gates this somehow; its exact semantics are unverified.

### The experiment (Phase 1.4, requires a real install)

1. Start a campaign. Log `GameNetwork.IsSessionActive`, `.IsMultiplayer`, `.MultiplayerDisabled`, `.IsServer`, `.IsClient`.
2. From a `MBSubModuleBase`, attempt `GameNetwork.Initialize(...)` + `PreStartMultiplayerOnServer()` + `StartMultiplayerOnServer(port)` while a `Campaign` is active. Record outcome (success / exception / silent no-op).
3. Register a trivial module event via `AddRemoveMessageHandlers` and attempt a loopback send with `BeginBroadcastModuleEvent()` / `EndBroadcastModuleEvent(...)`.
4. Record whether `Campaign` continues ticking normally afterwards.

**Outcome A — it works:** adopt `GameNetwork` module events (native, already integrated, handles peers/disconnects).
**Outcome B — it does not:** implement our own transport. `TaleWorlds.Network.TcpSocket` and the `MessageContract` machinery are available and are plain managed types (VERIFIED), or we use `System.Net.Sockets` directly.

**Until resolved, no code may bind to `GameNetwork` directly.** Everything goes through:

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

Phase 1 ships a **loopback** implementation so L3–L6 are testable before the answer is known.

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
