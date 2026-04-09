# AIFacemask System Port Plan: Unreal → Unity

## Overview

This document outlines the plan to port the latest AIFacemask system changes from Unreal Engine to Unity. The Unreal implementation has been significantly updated with NVIDIA ACE integration, narrative state machine refactoring, and a visitor pattern for VOIP audio processing.

---

## Current State Analysis

### Unreal (Source - Latest)
**Location:** `HoloCade_UnrealPlugin/Plugins/HoloCade/Source/AIFacemask/`

**Components:**
- ✅ `UAIFaceController` - Receives NVIDIA ACE output (blend shapes + textures)
- ✅ `UAIFacemaskACEScriptManager` - Manages pre-baked ACE scripts
- ✅ `UAIFacemaskACEImprovManager` - Handles real-time improvised responses (local LLM + TTS + Audio2Face)
- ✅ `UAIFacemaskASRManager` - Converts player voice to text (implements `IVOIPAudioVisitor`)
- ✅ `AAIFacemaskExperience` - Uses base class `NarrativeStateMachine` (no duplicate state machine)
- ✅ `IVOIPAudioVisitor` - Visitor interface for VOIP audio events (in VOIP module)

**Key Features:**
- Fully automated NVIDIA ACE pipeline (no manual facial animation)
- Narrative state machine integration (inherited from base class)
- Pre-baked script collections with automatic triggering
- Real-time improv with local LLM/TTS/Audio2Face support
- ASR integration via visitor pattern
- Dedicated server architecture enforced

### Unity (Target - Needs Update)
**Location:** `HoloCade_Unity/Packages/com.ajcampbell.holocade/Runtime/`

**Current Components:**
- ⚠️ `FacialAnimationController` - Old implementation (manual control, needs refactor)
- ⚠️ `AIFacemaskExperience` - Basic implementation (uses separate `ExperienceStateMachine`)
- ❌ Missing: ACE Script Manager
- ❌ Missing: ACE Improv Manager
- ❌ Missing: ASR Manager
- ❌ Missing: VOIP visitor interface

**Gaps:**
- Still has manual facial animation control (needs to be receiver-only)
- Uses separate state machine instead of base class narrative state machine
- No NVIDIA ACE integration architecture
- No visitor pattern for VOIP audio processing

---

## Port Plan

### Phase 1: Core Component Refactoring

#### 1.1 Refactor `FacialAnimationController` → `AIFaceController`
**File:** `Runtime/AIFacemask/AIFaceController.cs` (rename from `FacialAnimationController.cs`)

**Changes:**
- Remove all manual control methods (`SetBlendShapeWeight`, `SetAnimationMode`, etc.)
- Convert to pure receiver/display system for NVIDIA ACE output
- Add `ReceiveFacialAnimationData()` method
- Add `FFacialAnimationData` struct (blend shapes + texture)
- Add `FAIFaceConfig` struct (target mesh, NVIDIA ACE endpoint URL, update rate)
- Remove `FacialAnimationMode` enum (no longer needed)
- Update comments to reflect automated NVIDIA ACE pipeline
- Add NOOP TODOs for:
  - Receiving facial animation data from NVIDIA ACE endpoint
  - Applying blend shape weights to SkinnedMeshRenderer
  - Applying facial texture to mesh material

**Unity-Specific Notes:**
- Use `SkinnedMeshRenderer` instead of `USkeletalMeshComponent`
- Use `Texture2D` instead of `UTexture2D`
- Use `Dictionary<string, float>` instead of `TMap<FName, float>`
- Use Unity's `MonoBehaviour` lifecycle instead of `UActorComponent`

---

#### 1.2 Update `AIFacemaskExperience` to Use Base Class Narrative State Machine
**File:** `Runtime/ExperienceTemplates/AIFacemaskExperience.cs`

**Changes:**
- Remove `ExperienceStateMachine experienceLoop` field
- Use base class `narrativeStateMachine` instead (from `HoloCadeExperienceBase`)
- Set `useNarrativeStateMachine = true` in `Awake()`
- Update `InitializeExperienceImpl()` to initialize base class narrative state machine
- Remove manual `ExperienceStateMachine` creation
- Update `OnExperienceStateChanged()` to match base class signature
- Update `AdvanceExperience()` and `RetreatExperience()` to call base class methods
- Add component references for new managers:
  - `AIFacemaskACEScriptManager acescriptManager`
  - `AIFacemaskACEImprovManager aceimprovManager`
  - `AIFacemaskASRManager aceasrManager`
- Initialize new managers in `InitializeExperienceImpl()`
- Update comments to reflect automated AI facemask performances

**Unity-Specific Notes:**
- Use `[SerializeField]` instead of `UPROPERTY`
- Use Unity's `GetComponent<T>()` instead of `CreateDefaultSubobject`
- Use `UnityEvent` instead of `DECLARE_DYNAMIC_MULTICAST_DELEGATE`
- Use `string` instead of `FName`

---

### Phase 2: New Component Creation

#### 2.1 Create `AIFacemaskACEScriptManager`
**File:** `Runtime/AIFacemask/AIFacemaskACEScriptManager.cs`

**Port from:** `Source/AIFacemask/Public/AIFacemaskACEScriptManager.h` and `.cpp`

**Key Features to Port:**
- Script collection management
- Pre-baked script triggering on narrative state changes
- HTTP requests to NVIDIA ACE server (NOOP)
- Script playback state tracking
- Events for script started/finished/line started

**Unity-Specific Adaptations:**
- Use `UnityEvent` for delegates
- Use `System.Collections.Generic.List` instead of `TArray`
- Use `Dictionary<string, AIFacemaskACEScript>` instead of `TMap<FName, FAIFacemaskACEScript>`
- Use `UnityWebRequest` for HTTP (NOOP implementation)
- Use `Coroutine` for async operations

**Dependencies:**
- Create `AIFacemaskACEScript.cs` (data structures)
- Create `AIFacemaskACEScriptLine.cs` (script line data)
- Create `AIFacemaskACEScriptCollection.cs` (script collection)

---

#### 2.2 Create `AIFacemaskACEImprovManager`
**File:** `Runtime/AIFacemask/AIFacemaskACEImprovManager.cs`

**Port from:** `Source/AIFacemask/Public/AIFacemaskACEImprov.h` and `.cpp`

**Key Features to Port:**
- Local LLM integration (Ollama, vLLM, NVIDIA NIM support)
- Local TTS integration (NVIDIA Riva support)
- Local Audio2Face integration (NVIDIA NIM support)
- Conversation history management
- Async response generation
- Events for improv response generated/started/finished

**Unity-Specific Adaptations:**
- Use `UnityWebRequest` for HTTP requests (NOOP)
- Use `Coroutine` for async operations
- Use `System.Collections.Generic.List<string>` for conversation history
- Use `System.Threading.Tasks` for async LLM requests (optional)

**Dependencies:**
- Create `AIFacemaskACEImprovConfig.cs` (configuration struct)
- Reference `AIFacemaskASRManager` for triggering improv

---

#### 2.3 Create `AIFacemaskASRManager`
**File:** `Runtime/AIFacemask/AIFacemaskASRManager.cs`

**Port from:** `Source/AIFacemask/Public/AIFacemaskASRManager.h` and `.cpp`

**Key Features to Port:**
- Implements `IVOIPAudioVisitor` interface
- Audio buffering per player
- Voice activity detection
- ASR transcription requests (NOOP)
- Auto-trigger improv after transcription

**Unity-Specific Adaptations:**
- Use `List<float>` for audio buffers
- Use `Dictionary<int, List<float>>` for per-player buffers
- Use `UnityWebRequest` or gRPC client for ASR requests (NOOP)
- Use `Coroutine` for async transcription

**Dependencies:**
- Create `AIFacemaskASRConfig.cs` (configuration struct)
- Implement `IVOIPAudioVisitor` interface (create in VOIP module first)

---

#### 2.4 Create Data Structures
**Files:**
- `Runtime/AIFacemask/AIFacemaskACEScript.cs`
- `Runtime/AIFacemask/AIFacemaskACEScriptLine.cs`
- `Runtime/AIFacemask/AIFacemaskACEScriptCollection.cs`
- `Runtime/AIFacemask/AIFacemaskACEImprovConfig.cs`
- `Runtime/AIFacemask/AIFacemaskASRConfig.cs`
- `Runtime/AIFacemask/AIFacemaskTypes.cs` (enums: `ACEVoiceType`, `ACEScriptMode`)

**Port from:** `Source/AIFacemask/Public/AIFacemaskACEScript.h`

**Unity-Specific Adaptations:**
- Use `[System.Serializable]` instead of `USTRUCT`
- Use `public` fields with `[SerializeField]` instead of `UPROPERTY`
- Use `string` instead of `FName`
- Use `List<T>` instead of `TArray<T>`
- Use `Dictionary<string, T>` instead of `TMap<FName, T>`

---

### Phase 3: VOIP Integration

#### 3.1 Create `IVOIPAudioVisitor` Interface
**File:** `Runtime/VOIP/IVOIPAudioVisitor.cs`

**Port from:** `Source/VOIP/Public/IVOIPAudioVisitor.h`

**Key Features:**
- Interface for subscribing to VOIP audio events
- `OnPlayerAudioReceived(int playerId, float[] audioData, int sampleRate, Vector3 position)` method
- Extensive documentation for experience template usage

**Unity-Specific Adaptations:**
- Use C# interface instead of Unreal's `UINTERFACE`
- Use `float[]` instead of `TArray<float>`
- Use `Vector3` instead of `FVector`
- Add XML documentation comments

---

#### 3.2 Update `VOIPManager` to Support Visitors
**File:** `Runtime/VOIP/VOIPManager.cs`

**Changes:**
- Add `List<IVOIPAudioVisitor> audioVisitors` field
- Add `RegisterAudioVisitor(IVOIPAudioVisitor visitor)` method
- Add `UnregisterAudioVisitor(IVOIPAudioVisitor visitor)` method
- Update audio receive handler to notify all registered visitors
- Add NOOP for Opus to PCM decoding before passing to visitors

**Unity-Specific Notes:**
- Use `List<IVOIPAudioVisitor>` instead of `TArray<TScriptInterface<IVOIPAudioVisitor>>`
- Use Unity's audio system for decoding (or Mumble client library)

---

### Phase 4: Base Class Updates

#### 4.1 Verify `HoloCadeExperienceBase` Has Narrative State Machine
**File:** `Runtime/Core/HoloCadeExperienceBase.cs`

**Verify:**
- Has `ExperienceStateMachine narrativeStateMachine` field
- Has `useNarrativeStateMachine` bool flag
- Has `AdvanceNarrativeState()` and `RetreatNarrativeState()` methods
- Has `OnNarrativeStateChanged` event/callback
- Auto-creates `narrativeStateMachine` when `useNarrativeStateMachine = true`

**If Missing:**
- Port narrative state machine functionality from Unreal base class
- Ensure it matches the pattern used in `EscapeRoomExperience`

---

### Phase 5: Documentation Updates

#### 5.1 Update Unity README
**File:** `HoloCade_Unity/Packages/com.ajcampbell.holocade/README.md`

**Sections to Update:**

1. **AIFacemask Experience Section:**
   - Update description to reflect automated NVIDIA ACE pipeline
   - Remove references to manual facial animation
   - Add information about pre-baked scripts and improv responses
   - Update architecture diagram
   - Add complete system flow diagrams (pre-baked script flow, improv flow)

2. **Add "AI Facial Animation (Fully Automated)" Section:**
   - Port from Unreal README
   - Explain NVIDIA ACE pipeline
   - Clarify no manual control/rigging
   - Explain real-time application

3. **Add "Live Actor Control (High-Level Flow Only)" Section:**
   - Port from Unreal README
   - Explain wireless trigger buttons
   - Explain narrative state control
   - Explain automated performance triggers

4. **Add "NVIDIA ACE Integration Architecture" Section:**
   - Port component architecture diagram
   - Explain script manager, improv manager, ASR manager
   - Explain visitor pattern for VOIP

5. **Add "Custom Audio Processing (Visitor Pattern)" Section:**
   - Port from Unreal README VOIP API section
   - Explain `IVOIPAudioVisitor` interface
   - Provide Unity C# code examples
   - Explain benefits

6. **Add "AI Facemask Experience - NOOP Implementations" Section:**
   - List all NOOP TODOs
   - Categorize by priority and component

7. **Apply Formatting Updates:**
   - Add collapsible sections using HTML `<details>` and `<summary>` tags
   - Add indented content with `div style="margin-left: 20px;"`
   - Collapse Experience Templates, Low-Level APIs, Quick Start options
   - Collapse "When to Use What?" sections
   - Collapse installation methods
   - Reorganize "Three-Tier Architecture" section (Code Structure vs LAN Configuration)
   - Add LAN Server/Client Configuration diagrams
   - Move "Philosophy" section before "Three-Tier Architecture"
   - Add "Who is HoloCade for?" and "Who is HoloCade not for?" collapsible sections
   - Update installation methods to reflect simple git clone (if restructure is complete)

---

## Implementation Order

### Step 1: Foundation (Do First)
1. ✅ Verify/Update `HoloCadeExperienceBase` narrative state machine
2. ✅ Create `IVOIPAudioVisitor` interface
3. ✅ Update `VOIPManager` to support visitors

### Step 2: Data Structures (Do Second)
4. ✅ Create all data structure files (`AIFacemaskACEScript.cs`, etc.)
5. ✅ Create enum types (`ACEVoiceType`, `ACEScriptMode`)

### Step 3: Core Components (Do Third)
6. ✅ Refactor `FacialAnimationController` → `AIFaceController`
7. ✅ Create `AIFacemaskASRManager` (simplest, depends on visitor interface)
8. ✅ Create `AIFacemaskACEScriptManager` (depends on data structures)
9. ✅ Create `AIFacemaskACEImprovManager` (depends on ASR manager)

### Step 4: Experience Template (Do Fourth)
10. ✅ Update `AIFacemaskExperience` to use base class narrative state machine
11. ✅ Integrate all new managers
12. ✅ Update initialization and state change handlers

### Step 5: Documentation (Do Last)
13. ✅ Update README with all new sections
14. ✅ Apply formatting updates (collapsible sections, etc.)
15. ✅ Add code examples for Unity C#

---

## Unity-Specific Considerations

### C# vs C++ Differences
- **Memory Management:** Unity uses garbage collection, no manual `new`/`delete`
- **Component System:** Use `MonoBehaviour` and `GetComponent<T>()` instead of `UActorComponent` and `CreateDefaultSubobject`
- **Serialization:** Use `[SerializeField]` instead of `UPROPERTY`
- **Events:** Use `UnityEvent` instead of `DECLARE_DYNAMIC_MULTICAST_DELEGATE`
- **Collections:** Use `List<T>` and `Dictionary<K, V>` instead of `TArray` and `TMap`
- **Strings:** Use `string` instead of `FName` or `FString`
- **Async:** Use `Coroutine` or `async/await` instead of Unreal's async system
- **HTTP:** Use `UnityWebRequest` instead of Unreal's HTTP module
- **Networking:** Use Unity Netcode for GameObjects instead of Unreal's replication

### Namespace Structure
```
HoloCade.AIFacemask
  - AIFaceController
  - AIFacemaskACEScriptManager
  - AIFacemaskACEImprovManager
  - AIFacemaskASRManager
  - AIFacemaskACEScript
  - AIFacemaskACEScriptLine
  - AIFacemaskACEScriptCollection
  - AIFacemaskACEImprovConfig
  - AIFacemaskASRConfig
  - AIFacemaskTypes

HoloCade.VOIP
  - IVOIPAudioVisitor
  - VOIPManager (updated)

HoloCade.ExperienceTemplates
  - AIFacemaskExperience (updated)
```

---

## Testing Checklist

After porting, verify:
- [ ] `AIFaceController` receives and applies facial animation data (NOOP)
- [ ] `AIFacemaskACEScriptManager` triggers scripts on state changes (NOOP)
- [ ] `AIFacemaskACEImprovManager` generates improv responses (NOOP)
- [ ] `AIFacemaskASRManager` receives audio from VOIP and triggers transcription (NOOP)
- [ ] `AIFacemaskExperience` uses base class narrative state machine
- [ ] Wireless trigger buttons advance/retreat narrative state
- [ ] State changes trigger automated AI facemask performances
- [ ] Visitor pattern works for VOIP audio processing
- [ ] All NOOP implementations are clearly marked
- [ ] README documentation is complete and matches Unreal version

---

## Estimated Effort

- **Phase 1 (Core Refactoring):** 4-6 hours
- **Phase 2 (New Components):** 8-12 hours
- **Phase 3 (VOIP Integration):** 2-3 hours
- **Phase 4 (Base Class Updates):** 1-2 hours (if needed)
- **Phase 5 (Documentation):** 3-4 hours

**Total:** ~18-27 hours

---

## Notes

- All HTTP/gRPC requests to NVIDIA ACE services are NOOP implementations
- Focus on architecture and data flow first, implementation details later
- Maintain Unity coding standards (see `Claude Unity Rules.txt`)
- Keep NOOP implementations clearly marked for future work
- Ensure all components are properly namespaced and organized



