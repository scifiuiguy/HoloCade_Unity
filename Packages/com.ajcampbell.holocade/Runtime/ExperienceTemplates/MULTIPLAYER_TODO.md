# AIFacemask Multiplayer Implementation TODO (Unity)

## Current Status

### ✅ **Implemented:**
- Server discovery (UDP broadcast beacon)
- Client discovery (listening for servers)
- Server metadata broadcast (player count, state, version)
- Dedicated server enforcement
- Experience Loop state machine (local only)
- Embedded Systems button input (local only)
- Server Manager GUI (UI Toolkit)

### ❌ **NOT Implemented:**
- Actual client-server connection
- Game state synchronization (NetCode)
- Player spawning/management
- Button press synchronization
- Experience Loop state synchronization
- Authority checks

---

## What Needs to Be Built

### **1. Unity NetCode for GameObjects (NGO) Setup**

**File:** `AIFacemaskExperience.cs`

Add NetCode components:

```csharp
using Unity.Netcode;

public class AIFacemaskExperience : HoloCadeExperienceBase
{
    // Make this a NetworkBehaviour instead of MonoBehaviour
    // (HoloCadeExperienceBase should inherit from NetworkBehaviour)

    // Replicated state
    private NetworkVariable<FixedString64Bytes> replicatedExperienceState = 
        new NetworkVariable<FixedString64Bytes>(
            default, 
            NetworkVariableReadPermission.Everyone, 
            NetworkVariableWritePermission.Server
        );

    private NetworkList<bool> replicatedButtonStates;

    protected override void Awake()
    {
        base.Awake();
        replicatedButtonStates = new NetworkList<bool>();
    }
}
```

---

### **2. Client Connection**

**File:** `AIFacemaskExperience.cs`

Currently:
```csharp
private void OnServerDiscovered(ServerInfo serverInfo)
{
    // TODO: Implement actual connection logic
}
```

Should be:
```csharp
private void OnServerDiscovered(ServerInfo serverInfo)
{
    if (serverInfo.ExperienceType == "AIFacemask" && serverInfo.bAcceptingConnections)
    {
        // Get the NetworkManager
        NetworkManager networkManager = NetworkManager.Singleton;
        
        if (networkManager != null)
        {
            // Set connection parameters
            var transport = networkManager.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            if (transport != null)
            {
                transport.ConnectionData.Address = serverInfo.ServerIP;
                transport.ConnectionData.Port = (ushort)serverInfo.ServerPort;
            }

            // Start as client
            networkManager.StartClient();
            Debug.Log($"[AIFacemask] Connecting to server at {serverInfo.ServerIP}:{serverInfo.ServerPort}");
        }
    }
}
```

---

### **3. Server RPCs for Button Presses**

**File:** `AIFacemaskExperience.cs`

Add Server RPCs:

```csharp
/// <summary>
/// Server RPC: Advance experience (called by live actor button press)
/// </summary>
[ServerRpc(RequireOwnership = false)]
private void ServerAdvanceExperienceRpc()
{
    if (experienceLoop != null && experienceLoop.AdvanceState())
    {
        replicatedExperienceState.Value = new FixedString64Bytes(experienceLoop.GetCurrentStateName());
        Debug.Log($"[AIFacemask] Experience advanced to: {experienceLoop.GetCurrentStateName()}");
    }
}

/// <summary>
/// Server RPC: Retreat experience (called by live actor button press)
/// </summary>
[ServerRpc(RequireOwnership = false)]
private void ServerRetreatExperienceRpc()
{
    if (experienceLoop != null && experienceLoop.RetreatState())
    {
        replicatedExperienceState.Value = new FixedString64Bytes(experienceLoop.GetCurrentStateName());
        Debug.Log($"[AIFacemask] Experience retreated to: {experienceLoop.GetCurrentStateName()}");
    }
}
```

---

### **4. Authority Checks**

**File:** `AIFacemaskExperience.cs`

Update `ProcessButtonInput()`:

```csharp
private void ProcessButtonInput()
{
    // Only read buttons on server (which is connected to the physical microcontroller)
    if (!IsServer)
    {
        return;
    }

    if (costumeController == null || !costumeController.IsConnected() || experienceLoop == null)
    {
        return;
    }

    // Read current button states
    bool[] currentButtonStates = new bool[4];
    for (int i = 0; i < 4; i++)
    {
        currentButtonStates[i] = costumeController.GetDigitalInput(i);
    }

    // Button 0 (Left Wrist Forward) or Button 2 (Right Wrist Forward)
    if ((currentButtonStates[0] && !previousButtonStates[0]) || 
        (currentButtonStates[2] && !previousButtonStates[2]))
    {
        ServerAdvanceExperienceRpc();  // Use RPC instead of direct call
    }

    // Button 1 (Left Wrist Backward) or Button 3 (Right Wrist Backward)
    if ((currentButtonStates[1] && !previousButtonStates[1]) || 
        (currentButtonStates[3] && !previousButtonStates[3]))
    {
        ServerRetreatExperienceRpc();  // Use RPC instead of direct call
    }

    // Store current states for next frame
    for (int i = 0; i < 4; i++)
    {
        previousButtonStates[i] = currentButtonStates[i];
    }

    // Update replicated button states
    replicatedButtonStates.Clear();
    for (int i = 0; i < 4; i++)
    {
        replicatedButtonStates.Add(currentButtonStates[i]);
    }
}
```

---

### **5. NetworkBehaviour Base Class**

**File:** `HoloCadeExperienceBase.cs`

Change from MonoBehaviour to NetworkBehaviour:

```csharp
using Unity.Netcode;

/// <summary>
/// Base class for all HoloCade Experience Templates
/// </summary>
public abstract class HoloCadeExperienceBase : NetworkBehaviour
{
    // ... existing code ...

    [Header("Networking")]
    [SerializeField] protected bool multiplayerEnabled = false;

    /// <summary>
    /// Called when the object is spawned on the network
    /// </summary>
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            Debug.Log("[HoloCade] Experience spawned on server");
        }
        else if (IsClient)
        {
            Debug.Log("[HoloCade] Experience spawned on client");
        }
    }
}
```

---

### **6. Player Role Management**

**File:** Create new `HoloCadePlayerController.cs`

```csharp
using Unity.Netcode;
using UnityEngine;

namespace HoloCade.Core
{
    /// <summary>
    /// HoloCade Player Controller
    /// Manages player roles (live actor vs player)
    /// </summary>
    public class HoloCadePlayerController : NetworkBehaviour
    {
        private NetworkVariable<bool> isLiveActor = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private NetworkVariable<int> liveActorIndex = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public bool IsLiveActor => isLiveActor.Value;
        public int LiveActorIndex => liveActorIndex.Value;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                // Assign role based on connection order or other logic
                AssignPlayerRole();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void AssignPlayerRole()
        {
            // TODO: Implement role assignment logic
            // For now, just log
            Debug.Log($"[HoloCade] Player {OwnerClientId} connected, assigning role...");
        }
    }
}
```

---

### **7. Experience Loop State Sync**

**File:** `ExperienceStateMachine.cs`

Add network synchronization:

```csharp
/// <summary>
/// Set state from replicated value (called on clients)
/// </summary>
public void SetCurrentStateFromReplication(string stateName)
{
    if (isRunning)
    {
        int targetIndex = states.FindIndex(s => s.StateName == stateName);
        if (targetIndex != -1)
        {
            ChangeState(targetIndex);
        }
    }
}
```

**File:** `AIFacemaskExperience.cs`

Subscribe to NetworkVariable changes:

```csharp
private void Awake()
{
    // ... existing code ...

    // Subscribe to replicated state changes
    replicatedExperienceState.OnValueChanged += OnExperienceStateReplicated;
}

private void OnExperienceStateReplicated(FixedString64Bytes oldState, FixedString64Bytes newState)
{
    // Update local experience loop to match server
    if (IsClient && !IsServer)
    {
        experienceLoop?.SetCurrentStateFromReplication(newState.ToString());
    }
}
```

---

## Architecture: How It Should Work

```
┌─────────────────────────────────────────────────┐
│  Dedicated Server PC                            │
│  ──────────────────                             │
│  • Runs AIFacemaskExperience (Server Authority) │
│  • Connected to ESP32 via WiFi                  │
│  • Reads button presses from GetDigitalInput()  │
│  • Updates ExperienceLoop state                 │
│  • Syncs state to all clients (NetCode)         │
│  • Runs Omniverse Audio2Face                    │
│  • Streams facial animation                     │
└─────────────────────────────────────────────────┘
                    │
                    │ Unity NetCode Replication
                    ├─────────────┬─────────────┐
                    │             │             │
        ┌───────────▼──┐   ┌──────▼──────┐   ┌──▼───────────┐
        │  HMD 1       │   │  HMD 2      │   │  HMD 3       │
        │  (Client)    │   │  (Client)   │   │  (Client)    │
        │  • Live Actor│   │  • Player   │   │  • Player    │
        │  • Receives  │   │  • Receives │   │  • Receives  │
        │    state     │   │    state    │   │    state     │
        └──────────────┘   └─────────────┘   └──────────────┘
```

---

## Testing Checklist

Once implemented, test:

- [ ] Client can discover and connect to server
- [ ] Multiple clients can connect simultaneously
- [ ] Button press on server advances experience for all clients
- [ ] Experience state syncs correctly to late-joining clients
- [ ] Server can track which clients are live actors vs players
- [ ] Disconnection/reconnection works correctly
- [ ] Experience Loop state persists through client reconnects

---

## Unity NetCode Setup Notes

### **Required Packages**

Add to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.unity.netcode.gameobjects": "1.8.0",
    "com.unity.transport": "2.2.0"
  }
}
```

### **NetworkManager Setup**

1. Create a GameObject in your scene named `NetworkManager`
2. Add `NetworkManager` component
3. Add `UnityTransport` component
4. Configure NetworkManager:
   - **Server Bind Address:** `0.0.0.0`
   - **Server Listen Port:** `7777`
   - **Client Address:** (set dynamically from server discovery)
5. Add your experience prefab to **NetworkManager > Network Prefabs** list

### **Important Differences from Unreal**

| Unreal | Unity NetCode |
|--------|---------------|
| `UPROPERTY(Replicated)` | `NetworkVariable<T>` |
| `UFUNCTION(Server, Reliable)` | `[ServerRpc]` |
| `UFUNCTION(Client, Reliable)` | `[ClientRpc]` |
| `HasAuthority()` | `IsServer` |
| `GetLifetimeReplicatedProps()` | Not needed (automatic) |
| `DOREPLIFETIME()` | Not needed (automatic) |

---

## Notes

- **EmbeddedSystems only runs on server** - The physical microcontroller is connected to the server PC
- **Clients receive synchronized state** - They don't read from hardware directly
- **Audio2Face streams separately** - Uses NVIDIA's streaming, not Unity networking
- **This uses Unity NetCode for GameObjects** - Not custom networking like the Server Beacon

---

**Priority:** Medium (Server discovery works, but actual multiplayer gameplay doesn't)

**Estimated Work:** 4-6 hours for basic implementation, 8-12 hours with testing/polish

---

**Related Files:**
- `AIFacemaskExperience.cs` - Main experience class
- `HoloCadeExperienceBase.cs` - Base class (needs NetworkBehaviour)
- `ExperienceStateMachine.cs` - State machine sync
- `HoloCadePlayerController.cs` - New file for player role management
- Server Manager already implemented ✅



