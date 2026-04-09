# Pro Audio UI Toolkit Templates - Implementation Guide

## Overview

This document outlines the implementation steps for creating UI Toolkit window templates that provide a virtual sound board interface in the Command Console, synchronized bidirectionally with physical pro audio consoles via OSC.

## Current Implementation Status

✅ **ProAudioController Backend (Complete)**
- OSC client/server implemented (`HoloCadeOSCClient`, `HoloCadeOSCServer`)
- Bidirectional sync Actions ready:
  - `OnChannelFaderChanged` (Action<int, float>) - (virtualChannel, level)
  - `OnChannelMuteChanged` (Action<int, bool>) - (virtualChannel, mute)
  - `OnMasterFaderChanged` (Action<float>) - (level)
- Virtual-to-physical channel mapping system (`RegisterChannelForSync`)
- Channel validation (max channels per console type via `GetMaxChannelsForConsole()`)
- Address parsing for multiple manufacturers (Behringer, Yamaha, Allen & Heath, etc.)
- Channel offset support (0-based vs 1-based indexing via `Config.ChannelOffset`)
- "Other" option for unsupported hardware (64 channels, generic `/ch/XX/` paths)
- "Custom" option with XX/YY placeholder patterns (user-defined OSC paths)

🔄 **UI Toolkit Templates (Next Step)**
- Editor window template needs to be created
- Action event bindings need to be implemented
- Two-way sync (UI ↔ Physical) needs to be wired
- Console type dropdown needs to be added
- Custom OSC pattern input fields need to be added (when "Custom" is selected)

## UI Toolkit Template Requirements

### Window Structure

Create an Editor Window (`EditorWindow`) using UI Toolkit (UXML/USS):

```
ProAudioMixerWindow (EditorWindow)
├── MasterSection
│   ├── MasterLabel: Label ("MASTER")
│   └── MasterFader: Slider (0-1 range, bound to SetMasterFader)
├── ConsoleConfigSection
│   ├── ConsoleTypeDropdown: EnumField or PopupField<ProAudioConsole>
│   ├── BoardIPField: TextField
│   ├── OSCPortField: IntegerField
│   ├── EnableReceiveToggle: Toggle
│   ├── ReceivePortField: IntegerField (visible when EnableReceive is true)
│   ├── ChannelOffsetField: IntegerField (default 0, can be -1 for 0-based)
│   └── CustomPatternSection (visible when ConsoleType = Custom)
│       ├── CustomFaderPatternField: TextField ("/ch/XX/fader")
│       ├── CustomMutePatternField: TextField ("/ch/XX/mute")
│       ├── CustomBusSendPatternField: TextField ("/ch/XX/bus/YY/level")
│       └── CustomMasterPatternField: TextField ("/master/fader")
├── ChannelsContainer (ScrollView)
│   └── ChannelRow (Template - reusable VisualElement)
│       ├── ChannelNumberLabel: Label ("CH 1")
│       ├── PhysicalChannelInput: TextField (small, for sync button input)
│       ├── SyncButton: Button (circular arrow icon)
│       ├── ChannelFader: Slider (0-1 range, bound to SetChannelFader)
│       ├── MuteButton: Toggle (bound to SetChannelMute)
│       └── DeleteButton: Button
└── AddChannelButton: Button
```

### Minimum Requirements for Testing

1. **Master Fader**
   - Visual slider (0.0 to 1.0 range)
   - Updates `ProAudioController.SetMasterFader()` when moved
   - Listens to `OnMasterFaderChanged` delegate for physical board updates

2. **Channel Faders (Minimum 2 channels for testing)**
   - Each channel has a fader slider (0.0 to 1.0 range)
   - Updates `ProAudioController.SetChannelFader(VirtualChannel, Level)` when moved
   - Listens to `OnChannelFaderChanged` delegate for physical board updates

3. **Mute Buttons**
   - Toggle button per channel
   - Updates `ProAudioController.SetChannelMute(VirtualChannel, Mute)` when clicked
   - Listens to `OnChannelMuteChanged` delegate for physical board updates

4. **Add Channel Button**
   - Creates a new channel row
   - Channel number increments automatically (VirtualChannel = 1, 2, 3, ...)

5. **Delete Channel Button**
   - Removes channel row
   - Calls `ProAudioController.UnregisterChannelForSync(VirtualChannel)` before deletion

6. **Sync Button + Physical Channel Input**
   - Text input field for physical channel number
   - Circular arrow button
   - On click: `ProAudioController.RegisterChannelForSync(VirtualChannel, PhysicalChannel)`
   - Validates physical channel against `GetMaxChannelsForConsole()`
   - Shows error if channel is invalid

### Two-Way Sync Implementation

#### UI → Physical (User Manipulates UI)

When user interacts with UI elements:

1. **Fader Slider Changed**:
   ```csharp
   private void OnChannelFaderValueChanged(ChangeEvent<float> evt, int virtualChannel)
   {
       float level = evt.newValue;
       proAudioController.SetChannelFader(virtualChannel, level);
       // Prevent feedback loop: temporarily unbind delegate
       // (see "Feedback Loop Prevention" below)
   }
   ```

2. **Mute Button Clicked**:
   ```csharp
   private void OnMuteButtonClicked(ChangeEvent<bool> evt, int virtualChannel)
   {
       bool mute = evt.newValue;
       proAudioController.SetChannelMute(virtualChannel, mute);
   }
   ```

3. **Master Fader Changed**:
   ```csharp
   private void OnMasterFaderValueChanged(ChangeEvent<float> evt)
   {
       float level = evt.newValue;
       proAudioController.SetMasterFader(level);
   }
   ```

#### Physical → UI (Board Sends OSC Update)

Subscribe to `ProAudioController` Actions in window initialization:

```csharp
private void OnEnable()
{
    // Find or create ProAudioController instance
    proAudioController = FindObjectOfType<ProAudioController>();
    if (proAudioController == null)
    {
        GameObject go = new GameObject("ProAudioController");
        proAudioController = go.AddComponent<ProAudioController>();
    }

    // Subscribe to Actions
    proAudioController.OnChannelFaderChanged += OnPhysicalFaderChanged;
    proAudioController.OnChannelMuteChanged += OnPhysicalMuteChanged;
    proAudioController.OnMasterFaderChanged += OnPhysicalMasterFaderChanged;

    // Initialize UI
    RefreshUI();
}

private void OnDisable()
{
    if (proAudioController != null)
    {
        proAudioController.OnChannelFaderChanged -= OnPhysicalFaderChanged;
        proAudioController.OnChannelMuteChanged -= OnPhysicalMuteChanged;
        proAudioController.OnMasterFaderChanged -= OnPhysicalMasterFaderChanged;
    }
}

private void OnPhysicalFaderChanged(int virtualChannel, float level)
{
    // Update UI slider for this channel (prevent feedback loop)
    var slider = GetChannelSlider(virtualChannel);
    if (slider != null && !isUpdatingFromPhysical)
    {
        isUpdatingFromPhysical = true;
        slider.value = level;
        isUpdatingFromPhysical = false;
    }
}

private void OnPhysicalMuteChanged(int virtualChannel, bool mute)
{
    // Update UI toggle for this channel
    var toggle = GetChannelMuteToggle(virtualChannel);
    if (toggle != null && !isUpdatingFromPhysical)
    {
        isUpdatingFromPhysical = true;
        toggle.value = mute;
        isUpdatingFromPhysical = false;
    }
}

private void OnPhysicalMasterFaderChanged(float level)
{
    // Update master fader slider
    if (masterFaderSlider != null && !isUpdatingFromPhysical)
    {
        isUpdatingFromPhysical = true;
        masterFaderSlider.value = level;
        isUpdatingFromPhysical = false;
    }
}
```

### Feedback Loop Prevention

To prevent infinite loops (UI change → OSC → Physical → Delegate → UI change), use a flag:

```csharp
private bool isUpdatingFromPhysical = false;

private void OnChannelFaderValueChanged(ChangeEvent<float> evt, int virtualChannel)
{
    if (isUpdatingFromPhysical)
        return; // Ignore if update came from physical board
    
    float level = evt.newValue;
    proAudioController.SetChannelFader(virtualChannel, level);
}
```

## Step-by-Step Implementation

### Step 1: Create Editor Window Script

1. Create `Assets/HoloCade/Editor/ProAudio/ProAudioMixerWindow.cs`
2. Inherit from `EditorWindow`
3. Use UI Toolkit (`rootVisualElement`)
4. Load UXML template (or create programmatically)

```csharp
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using HoloCade.ProAudio;

public class ProAudioMixerWindow : EditorWindow
{
    private ProAudioController proAudioController;
    private VisualElement root;
    private bool isUpdatingFromPhysical = false;

    [MenuItem("HoloCade/Pro Audio Mixer")]
    public static void ShowWindow()
    {
        GetWindow<ProAudioMixerWindow>("Pro Audio Mixer");
    }

    private void OnEnable()
    {
        // Find or create controller
        proAudioController = FindObjectOfType<ProAudioController>();
        if (proAudioController == null)
        {
            GameObject go = new GameObject("ProAudioController");
            proAudioController = go.AddComponent<ProAudioController>();
        }

        // Subscribe to delegates
        proAudioController.OnChannelFaderChanged += OnPhysicalFaderChanged;
        proAudioController.OnChannelMuteChanged += OnPhysicalMuteChanged;
        proAudioController.OnMasterFaderChanged += OnPhysicalMasterFaderChanged;

        // Build UI
        CreateGUI();
    }

    private void OnDisable()
    {
        // Unsubscribe from Actions
        if (proAudioController != null)
        {
            proAudioController.OnChannelFaderChanged -= OnPhysicalFaderChanged;
            proAudioController.OnChannelMuteChanged -= OnPhysicalMuteChanged;
            proAudioController.OnMasterFaderChanged -= OnPhysicalMasterFaderChanged;
        }
    }

    private void CreateGUI()
    {
        root = rootVisualElement;

        // TODO: Add master fader, console config, channel rows, etc.
        // See UXML template structure above
    }

    // ... delegate handlers, UI event handlers, etc.
}
```

### Step 2: Create UXML Template (Optional)

Create `Assets/HoloCade/Editor/ProAudio/ProAudioMixerWindow.uxml`:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
  <ui:VisualElement name="master-section">
    <ui:Label text="MASTER" />
    <ui:Slider name="master-fader" low-value="0" high-value="1" />
  </ui:VisualElement>
  
  <ui:VisualElement name="console-config">
    <ui:EnumField name="console-type" binding-path="ConsoleType" />
    <ui:TextField name="board-ip" binding-path="BoardIPAddress" />
    <ui:IntegerField name="osc-port" binding-path="OSCPort" />
    <!-- ... more fields ... -->
  </ui:VisualElement>
  
  <ui:ScrollView name="channels-container">
    <!-- Channel rows added dynamically -->
  </ui:ScrollView>
  
  <ui:Button name="add-channel-button" text="+ Add Channel" />
</ui:UXML>
```

### Step 3: Create Channel Row Template

Create `Assets/HoloCade/Editor/ProAudio/ChannelRow.uxml`:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
  <ui:VisualElement name="channel-row">
    <ui:Label name="channel-number-label" />
    <ui:TextField name="physical-channel-input" />
    <ui:Button name="sync-button" text="↻" />
    <ui:Slider name="channel-fader" low-value="0" high-value="1" />
    <ui:Toggle name="mute-button" />
    <ui:Button name="delete-button" text="×" />
  </ui:VisualElement>
</ui:UXML>
```

### Step 4: Implement Sync Button Logic

```csharp
private void OnSyncButtonClicked(ClickEvent evt, int virtualChannel, TextField physicalChannelInput)
{
    if (!int.TryParse(physicalChannelInput.value, out int physicalChannel))
    {
        EditorUtility.DisplayDialog("Error", "Invalid physical channel number", "OK");
        return;
    }

    if (physicalChannel <= 0)
    {
        EditorUtility.DisplayDialog("Error", "Physical channel must be greater than 0", "OK");
        return;
    }

    // Validate against console max channels
    int maxChannels = proAudioController.GetMaxChannelsForConsole();
    if (physicalChannel > maxChannels)
    {
        bool proceed = EditorUtility.DisplayDialog(
            "Warning",
            $"Physical channel {physicalChannel} exceeds maximum for selected console (max: {maxChannels}). " +
            "This may not work with your hardware. Continue anyway?",
            "Yes", "No");
        if (!proceed)
            return;
    }

    // Register channel
    bool success = proAudioController.RegisterChannelForSync(virtualChannel, physicalChannel);
    if (!success)
    {
        EditorUtility.DisplayDialog("Error", "Failed to register channel for sync", "OK");
    }
    else
    {
        Debug.Log($"Registered virtual channel {virtualChannel} -> physical channel {physicalChannel}");
    }
}
```

## Testing Checklist

### Physical → UI Sync
- [ ] Move physical board fader → UI slider updates
- [ ] Press physical board mute → UI toggle updates
- [ ] Move physical board master fader → UI master slider updates

### UI → Physical Sync
- [ ] Move UI channel fader → Physical board fader moves
- [ ] Click UI mute button → Physical board channel mutes
- [ ] Move UI master fader → Physical board master moves

### Multi-Channel Testing
- [ ] Register channel 1 → physical channel 5: UI fader controls physical CH 5
- [ ] Register channel 2 → physical channel 10: UI fader controls physical CH 10
- [ ] Move physical CH 5 → Only UI channel 1 updates (not channel 2)
- [ ] Move physical CH 10 → Only UI channel 2 updates (not channel 1)

### Channel Management
- [ ] Add channel → New row appears
- [ ] Delete channel → Row removed, channel unregistered
- [ ] Sync button validates channel number correctly
- [ ] Sync button shows error for invalid channels

### Console Type Selection
- [ ] Select "Behringer X32" → Max channels = 32, validates correctly
- [ ] Select "Other" → Max channels = 64, allows up to 64
- [ ] Select "Custom" → Custom pattern fields appear
- [ ] Custom pattern with XX/YY placeholders works correctly

### Channel Offset
- [ ] Set ChannelOffset = 0 → Channels are 1-based (CH 1 → /ch/01/)
- [ ] Set ChannelOffset = -1 → Channels are 0-based (CH 1 → /ch/00/)
- [ ] Physical board sends CH 01 → UI correctly interprets as virtual CH 1 (with offset applied)

## Notes

### Virtual-to-Physical Channel Mapping
- Virtual channels are 1-based and sequential (1, 2, 3, ...)
- Physical channels can be any valid hardware channel (e.g., map UI CH 1 → Physical CH 5)
- Multiple virtual channels can map to the same physical channel (though this is unusual)
- When a physical channel sends an update, ALL virtual channels mapped to it receive the update

### Channel Numbering
- Default is 1-based indexing (ChannelOffset = 0)
- For 0-based hardware, set ChannelOffset = -1
- The offset is applied when building OSC paths (sending) and when parsing incoming OSC addresses (receiving)

### Console Type Selection
- Dropdown should show all `ProAudioConsole` enum values
- When "Other" is selected, max channels = 64, validation is lenient
- When "Custom" is selected, custom pattern input fields should become visible
- Custom patterns use `XX` for channel number, `YY` for bus number (zero-padded automatically)

### Channel Validation
- `RegisterChannelForSync` validates physical channel against `GetMaxChannelsForConsole()`
- For "Other" console type, validation is lenient (won't throw error for channels > 64)
- For "Custom" console type, validation uses conservative default (64 channels)

## Next Steps After UI Templates

Once the UI Toolkit templates are complete and tested:

1. **Integration with Command Console**: Add ProAudio mixer panel as a tab/section in the main Command Console window
2. **Preset Management**: Save/load channel mappings and console configurations
3. **Bus Control**: Add bus send controls (reverb, monitor sends) to channel rows
4. **Scenes/Snapshots**: Save and recall mixer states
5. **MIDI Integration**: Optional MIDI control surface support

