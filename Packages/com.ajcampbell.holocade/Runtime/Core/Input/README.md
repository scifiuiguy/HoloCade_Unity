# HoloCade Input Adapter (Unity)

## Overview

`HoloCadeInputAdapter` is a universal input component that handles all input sources for HoloCade experiences, with automatic authority checks, replication, and RPC routing.

**Works with:**
- ✅ **All Server Types** - Dedicated servers and listen servers
- ✅ **All Input Sources** - Embedded systems (ESP32), VR controllers, keyboard, gamepad, AI, custom
- ✅ **All Templates** - AIFacemask, FlightSim, MovingPlatform, Gunship, CarSim

**Key Features:**
- **Automatic Authority Checks** - No manual `IsServer` checks needed
- **Automatic Replication** - State syncs to all clients via NetworkVariable
- **Automatic RPC Routing** - Client input sent to server seamlessly
- **Edge Detection** - Button press/release events
- **Event-Driven** - C# `event Action` for high performance
- **Separation of Concerns** - Input logic separate from experience logic

---

## Architecture

```
┌────────────────────────────────────────────────────┐
│  Input Sources (Any/All Can Be Active)            │
│  ─────────────────────────────────                 │
│  • Embedded Systems (ESP32, Arduino, etc.)         │
│  • VR Controllers (Meta, Vive, Index)              │
│  • Keyboard / Gamepad                              │
│  • AI / Scripted Events                            │
│  • Custom MonoBehaviour Logic                      │
└──────────────────┬─────────────────────────────────┘
                   │
                   ▼
┌────────────────────────────────────────────────────┐
│  HoloCadeInputAdapter (NetworkBehaviour)            │
│  ───────────────────────────────────               │
│  • Authority check (IsServer)                      │
│  • RPC routing (Client → Server)                   │
│  • State replication (Server → Clients)            │
│  • Edge detection (press/release)                  │
└──────────────────┬─────────────────────────────────┘
                   │
                   ▼  Replicates Automatically
┌────────────────────────────────────────────────────┐
│  C# Events Fire on ALL Machines                    │
│  ────────────────────────────                      │
│  • OnButtonPressed (event Action<int>)             │
│  • OnButtonReleased (event Action<int>)            │
│  • OnAxisChanged (event Action<int, float>)        │
└──────────────────┬─────────────────────────────────┘
                   │
                   ▼
┌────────────────────────────────────────────────────┐
│  Your Experience Template                          │
│  ────────────────────────                          │
│  void OnButtonPressed(int buttonIndex)             │
│  {                                                  │
│      // Fires on server AND clients automatically  │
│      // No authority checks needed!                │
│      experienceLoop.AdvanceState();                │
│  }                                                  │
└────────────────────────────────────────────────────┘
```

### Design Principles

**Separation of Concerns:**
- **Input Adapter:** "Button 0 was pressed"
- **Experience Template:** "Button 0 means advance to next state"

**Dependency Inversion:**
- Experience depends on abstraction (InputAdapter), not implementation (specific input source)

**Open/Closed:**
- Open for extension (add new input sources by calling `InjectButtonPress()`)
- Closed for modification (no adapter changes needed)

**Single Responsibility:**
- InputAdapter: Reads input, handles networking, manages replication
- Experience: State management, game logic

---

## Quick Start

### Step 1: Add Component (Auto-Created)

The `HoloCadeInputAdapter` is automatically created by `HoloCadeExperienceBase` in Awake().

### Step 2: Configure

```csharp
// In your experience's InitializeExperienceImpl() or Awake()
protected override void Awake()
{
    base.Awake();
    
    if (inputAdapter != null)
    {
        // Connect embedded device controller (ESP32, Arduino, etc.)
        inputAdapter.embeddedDeviceController = costumeController;
        
        // Enable input sources
        inputAdapter.enableEmbeddedSystemInput = true;  // ESP32 buttons
        inputAdapter.enableVRControllerInput = false;   // VR (override in subclass)
        
        // Configure channels
        inputAdapter.buttonCount = 4;  // 4 buttons
        inputAdapter.axisCount = 0;    // No analog axes
    }
}
```

### Step 3: Subscribe to Events

```csharp
private void OnEnable()
{
    // Subscribe to button press events
    if (inputAdapter != null)
    {
        inputAdapter.OnButtonPressed += OnInputButtonPressed;
        inputAdapter.OnButtonReleased += OnInputButtonReleased;
        inputAdapter.OnAxisChanged += OnInputAxisChanged;
    }
}

private void OnDisable()
{
    // Unsubscribe to prevent memory leaks
    if (inputAdapter != null)
    {
        inputAdapter.OnButtonPressed -= OnInputButtonPressed;
        inputAdapter.OnButtonReleased -= OnInputButtonReleased;
        inputAdapter.OnAxisChanged -= OnInputAxisChanged;
    }
}

private void OnInputButtonPressed(int buttonIndex)
{
    // ✅ Fires on server AND clients automatically
    // ✅ No authority checks needed
    // ✅ Already replicated by InputAdapter
    
    Debug.Log($"Button {buttonIndex} pressed!");
    
    if (buttonIndex == 0 || buttonIndex == 2)  // Forward buttons
        experienceLoop.AdvanceState();
    
    if (buttonIndex == 1 || buttonIndex == 3)  // Backward buttons
        experienceLoop.RetreatState();
}
```

**That's it!** ~10 lines of code for full multiplayer input support.

---

## Input Sources

### 1. Embedded Systems (ESP32, Arduino, STM32)

**Automatic** - Just connect the controller:

```csharp
inputAdapter.embeddedDeviceController = costumeController;
inputAdapter.enableEmbeddedSystemInput = true;
```

The adapter will:
- Read button/axis states every frame
- Detect edges (press/release events)
- Replicate to all clients
- Fire events on all machines

**Supported:**
- Digital inputs (`GetDigitalInput()` → button press/release)
- Analog inputs (`GetAnalogInput()` → axis value changes)
- Any protocol (WiFi, Serial, Bluetooth, Ethernet)

---

### 2. Unity Input System (Gamepad, Keyboard, Mouse)

**Use the HoloCadePlayerController helper component:**

**Setup in Editor:**
1. Create or use the included `HoloCadeInputActions.inputactions` asset
2. Add `PlayerInput` component to a GameObject
3. Add `HoloCadePlayerController` component
4. Assign the Input Action Asset to `HoloCadePlayerController.inputActions`
5. Test with gamepad/keyboard!

**Example Input Mappings (from HoloCadeInputActions.inputactions):**
```
Button0 → Gamepad South (A/Cross),  Keyboard 1
Button1 → Gamepad East (B/Circle),  Keyboard 2
Button2 → Gamepad West (X/Square),  Keyboard 3
Button3 → Gamepad North (Y/Triangle), Keyboard 4
Button4 → Gamepad Left Shoulder,    Keyboard Q
Button5 → Gamepad Right Shoulder,   Keyboard E
Button6 → Gamepad Left Trigger,     Keyboard Z
Button7 → Gamepad Right Trigger,    Keyboard X

Axis0   → Gamepad Left Stick X
Axis1   → Gamepad Left Stick Y
Axis2   → Gamepad Right Stick X
Axis3   → Gamepad Right Stick Y
```

**Automatic Routing:**
The `HoloCadePlayerController` automatically:
- Finds the current experience in the scene
- Routes all Input System actions to `inputAdapter.InjectButtonPress()` / `InjectAxisValue()`
- Handles button press/release edge detection
- Supports 8 digital buttons and 4 analog axes out of the box

**Use Cases:**
- ✅ Development testing with gamepad before ESP32 hardware is available
- ✅ Listen server hosts using keyboard/mouse instead of VR controllers
- ✅ Rapid prototyping without physical hardware setup

**Note:** In production LBE venues, dedicated servers read directly from ESP32. This is optional for development only.

---

### 3. VR Controllers (Listen Server Hosts)

**Override `ProcessVRControllerInput()` in a subclass:**

```csharp
public class MyInputAdapter : HoloCadeInputAdapter
{
    protected override void ProcessVRControllerInput()
    {
        // Only runs on server/host
        // Read VR controller state and inject input
        
        // Example: Right hand trigger = Button 0
        if (OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
        {
            InjectButtonPress(0);
        }
        else
        {
            InjectButtonRelease(0);
        }
    }
}
```

---

### 4. AI / Scripted Events

```csharp
// In your AI controller, sequencer, or game logic
void TriggerNextState()
{
    if (ShouldAdvanceExperience())
    {
        experience.inputAdapter.InjectButtonPress(0);
    }
}
```

**Use cases:**
- AI-driven narrative progression
- Timed events
- Trigger volumes
- Interactive objects

---

## API Reference

### Public Methods

```csharp
// Inject input (works on server or client)
public void InjectButtonPress(int buttonIndex);
public void InjectButtonRelease(int buttonIndex);
public void InjectAxisValue(int axisIndex, float value);

// Query current state (for polling)
public bool IsButtonPressed(int buttonIndex);
public float GetAxisValue(int axisIndex);
```

### Events (C# event Action)

```csharp
// Subscribe to these in your experience
public event Action<int> OnButtonPressed;          // void(int buttonIndex)
public event Action<int> OnButtonReleased;         // void(int buttonIndex)
public event Action<int, float> OnAxisChanged;     // void(int axisIndex, float value)

// Example subscription:
inputAdapter.OnButtonPressed += MyHandler;

// IMPORTANT: Always unsubscribe in OnDisable()!
inputAdapter.OnButtonPressed -= MyHandler;
```

### Configuration Properties

```csharp
// Embedded device reference
public SerialDeviceController embeddedDeviceController;

// Enable/disable input sources
public bool enableEmbeddedSystemInput = true;
public bool enableVRControllerInput = false;

// Channel counts (1-32)
[Range(1, 32)] public int buttonCount = 4;
[Range(0, 32)] public int axisCount = 0;
```

---

## How It Works

### Authority (Server / Listen Server Host)

```
InputAdapter.Update()
  └─ if (IsServer)
      ├─ ProcessEmbeddedSystemInput()        ← Read ESP32 buttons
      │   └─ for each button:
      │       └─ if (state changed):
      │           ├─ replicatedButtonStates.Value |= (1 << index)
      │           └─ OnButtonPressed?.Invoke(index)
      │
      └─ ProcessVRControllerInput()           ← Override in subclass
          └─ InjectButtonPress(0)
              └─ UpdateButtonState()
                  ├─ replicatedButtonStates.Value |= (1 << 0)
                  └─ OnButtonPressed?.Invoke(0)
```

### Clients (No Authority)

```
NetworkVariable.OnValueChanged fires automatically
  └─ OnButtonStatesChanged()
      └─ for each button:
          └─ if (replicated != previous):
              └─ OnButtonPressed?.Invoke(index)
```

### Client Requesting Input

```
experience.inputAdapter.InjectButtonPress(0)
  └─ InputAdapter checks authority:
      ├─ IsServer?  → UpdateButtonState() directly
      └─ !IsServer? → InjectButtonPressServerRpc(0)
                      └─ Server executes → UpdateButtonState()
                          └─ Replicates to all clients
```

**Key Point:** You never need to check authority or write RPCs yourself!

---

## Dedicated vs. Listen Server

### Dedicated Server

```
Server PC (No HMD)
  └─ InputAdapter reads ESP32 buttons
      └─ Updates NetworkVariable
          └─ All clients receive state updates

HMD Clients
  └─ Receive replicated button states
      └─ OnButtonPressed fires automatically
```

**Use case:** Production LBE venues with physical wrist buttons

---

### Listen Server

```
Host PC (with HMD)
  └─ InputAdapter reads:
      ├─ ESP32 buttons (if connected)
      └─ VR controllers (override ProcessVRControllerInput)
          └─ Both work simultaneously!

HMD Clients
  └─ Receive replicated button states
      └─ OnButtonPressed fires automatically
```

**Use case:** Development/testing with VR controllers

---

## Template Examples

### AIFacemask Experience

```csharp
// 4 wrist buttons (2 left, 2 right)
inputAdapter.buttonCount = 4;
inputAdapter.enableEmbeddedSystemInput = true;  // ESP32
inputAdapter.enableVRControllerInput = false;   // No VR

private void OnEnable()
{
    inputAdapter.OnButtonPressed += OnInputButtonPressed;
}

private void OnInputButtonPressed(int buttonIndex)
{
    if (buttonIndex == 0 || buttonIndex == 2) experienceLoop.AdvanceState();
    if (buttonIndex == 1 || buttonIndex == 3) experienceLoop.RetreatState();
}
```

---

### Flight Sim Experience

```csharp
// HOTAS controls: 16 buttons + 4 axes
inputAdapter.buttonCount = 16;  // Fire, landing gear, etc.
inputAdapter.axisCount = 4;     // Throttle, pitch, roll, yaw

private void OnEnable()
{
    inputAdapter.OnButtonPressed += OnInputButtonPressed;
    inputAdapter.OnAxisChanged += OnInputAxisChanged;
}

private void OnInputButtonPressed(int buttonIndex)
{
    if (buttonIndex == 0) FireWeapon();
    if (buttonIndex == 1) ToggleLandingGear();
}

private void OnInputAxisChanged(int axisIndex, float value)
{
    if (axisIndex == 0) SetThrottle(value);
    if (axisIndex == 1) SetPitch(value);
}
```

---

### Moving Platform Experience

```csharp
// Operator control panel: 8 buttons + emergency stop
inputAdapter.buttonCount = 9;

private void OnEnable()
{
    inputAdapter.OnButtonPressed += OnInputButtonPressed;
}

private void OnInputButtonPressed(int buttonIndex)
{
    if (buttonIndex == 8) EmergencyStop();  // Red button
    else ExecuteMovementSequence(buttonIndex);
}
```

---

## Benefits

### For Developers
✅ ~10 lines of code per template (vs. ~100 lines with manual implementation)  
✅ No authority checks required  
✅ No RPC code required  
✅ No replication code required  
✅ Event-driven (C# `event Action` for performance)  

### For Templates
✅ Reusable across all experiences  
✅ Decoupled from experience logic  
✅ Easy to add new input sources  

### For Deployment
✅ Dedicated server support (ESP32, operator panels)  
✅ Listen server support (VR controllers, testing)  
✅ Mixed input (ESP32 + VR simultaneously)  

---

## Code Location

**Component:** `HoloCadeInputAdapter.cs` (this directory)  
**Helper Controller:** `HoloCadePlayerController.cs` (this directory)  
**Example Input Actions:** `HoloCadeInputActions.inputactions` (this directory)

---

## Next Steps

1. **Configure InputAdapter** - Set button/axis counts in your experience
2. **Subscribe to Events** - Bind your input handlers in OnEnable()
3. **Test with Gamepad** - Use HoloCadePlayerController for testing
4. **Test with ESP32** - Connect physical hardware
5. **Deploy to Production** - Use dedicated server with ESP32 input

---

## See Also

- **NetCode for GameObjects Documentation** - Unity's multiplayer networking
- **Unity Input System Documentation** - For creating custom Input Action Assets
- **Embedded Systems README** - How to connect ESP32, Arduino, etc.



