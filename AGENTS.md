# AGENTS.md: Repo Onboarding for AI Agents

## README Hierarchy
- **Root Overview**: README.md – High-level project philosophy, quickstart, prerequisites, installation, architecture (3-tier modular: Low-Level APIs → Experience Templates → Custom Logic), features (VR/AR LBE with AI, haptics, networking), examples (Gunship, FlightSim), and basic troubleshooting.
- **Engine-Specific Guides**:
  - /Packages/com.ajcampbell.holocade/Runtime/HoloCade.Core/README.md (if added) – Core Unity C# APIs for HoloCade modules (e.g., AIFacemask, LargeHaptics, EmbeddedSystems).
  - /Packages/com.ajcampbell.holocade/Editor/README.md (if added) – Editor tools for Unity-specific workflows (e.g., UI Toolkit GUIs, NetCode setup).
- **Advanced/Dev Docs**:
  - /FlightSimExperience/README.md – Template-specific guide for FlightSim (HOTAS input, motion platforms).
  - /Packages/com.ajcampbell.holocade/FirmwareExamples/GunshipExperience/Gunship_Hardware_Specs.md – Hardware integration (ESP32 shields, solenoids, PWM drivers).
  - /docs/contributing.md (future) – Branching rules, testing with Unity Test Runner, PR guidelines.

Use this tree for context: Root → Runtime Modules → Templates/Hardware. Prioritize OpenXR for VR tasks; use NetCode for multiplayer sims. For cross-engine parity, reference sibling Unreal repo (github.com/scifiuiguy/HoloCade_Unreal) via shared API mappings in /src/common (if added).

## Table of Contents

### The Main Project README
- [Prerequisites & Package Dependencies](#-prerequisites--package-dependencies)
- [Three-Tier Architecture](#-three-tier-architecture)
- [Hardware-Agnostic Input System](#-hardware-agnostic-input-system)
- [Features](#-features)
- [Experience Genre Templates](#experience-genre-templates-pre-configured-solutions)
- [Low-Level APIs](#low-level-apis-technical-modules)
- [Installation](#-installation)
- [Examples](#-examples)
- [Dedicated Server & Server Manager](#-dedicated-server--server-manager)
- [Roadmap](#-roadmap)
- [License](#-license)

### Other READMEs in This Project

**Low-Level APIs:**
- VRPlayerTransport README - `Packages/com.ajcampbell.holocade/Runtime/Core/VRPlayerTransport/README.md`
- Input README - `Packages/com.ajcampbell.holocade/Runtime/Core/Input/README.md`
- VOIP README - `Packages/com.ajcampbell.holocade/Runtime/VOIP/README.md`
- EmbeddedSystems README - `Packages/com.ajcampbell.holocade/Runtime/EmbeddedSystems/README.md`

**Experience Genre Templates:**
- AIFacemask Experience README - `Packages/com.ajcampbell.holocade/Runtime/ExperienceTemplates/AIFacemaskExperience/README.md`

**Firmware Examples:**
- FirmwareExamples README - `Packages/com.ajcampbell.holocade/FirmwareExamples/README.md`
- GunshipExperience README - `Packages/com.ajcampbell.holocade/FirmwareExamples/GunshipExperience/README.md`
- FlightSimExperience README - `Packages/com.ajcampbell.holocade/FirmwareExamples/FlightSimExperience/README.md`
- EscapeRoom README - `Packages/com.ajcampbell.holocade/FirmwareExamples/EscapeRoom/README.md`
- Base Examples README - `Packages/com.ajcampbell.holocade/FirmwareExamples/Base/Examples/README.md`
- Base Templates README - `Packages/com.ajcampbell.holocade/FirmwareExamples/Base/Templates/README.md`

## Dependencies
```json
{
  "core": {
    "unity_packages": [
      "com.unity.xr.management@4.4.0",
      "com.unity.xr.openxr@1.9.0",
      "com.unity.xr.hands@1.3.0",
      "com.unity.inputsystem@1.7.0",
      "com.unity.textmeshpro@3.0.6"
    ],
    "notes": "Auto-resolve via Package Manager; OpenXR mandatory for HMD/hand tracking."
  },
  "multiplayer": {
    "unity_packages": [
      "com.unity.netcode.gameobjects@1.8.0",
      "com.unity.transport@2.2.0"
    ],
    "notes": "Required for AIFacemask/VOIP; enable in XR Management."
  },
  "optional": {
    "platform_specific": [
      "com.valvesoftware.steamvr@2.7.3",
      "com.unity.xr.oculus@latest",
      "com.unity.xr.interaction.toolkit@2.5.0"
    ],
    "external": [
      "NVIDIA ACE SDK (HTTP/WebSocket/gRPC clients for AI facial animation)"
    ],
    "notes": "SteamVR for 6DOF; Quest via OpenXR. No npm/pip—Unity-native."
  }
}
```

## Code Style Guidelines

### Code Style

* Eliminate curly braces wherever possible.
* Eliminate extra newline spaces wherever possible.
* I like my C# concise, but don't place two or more statements (each finalized by a semicolon) on a single line.
* You can inline an if condition with its single-line statement if both in total are less than 100 characters, but otherwise place the condition on one line and its statement on the next with an indent.
* You can inline an 'else' under the same conditions as for 'if'.
* Any time a function is more than ~30 lines of code, you should chunk it into sensible code blocks and place each code block into a helper method in a region called Helper Methods.
* When refactoring code blocks into helper methods, use ref and out keywords appropriately to minimize total number of lines and prevent extra memory allocation.
* Write comments where appropriate, but note that many comments are unnecessary if code blocks are chunked well with helper methods accurately named.
* Use expression-bodied members liberally in favor of local vars any time it is more efficient or equivalent for memory management.
* For simple assignment passthrough methods (like getters that just return a field), always use expression-bodied members instead of full method bodies.
* Always use LINQ for filtering sizable data sets unless I specify otherwise.
* Ask my permission before using reflection for anything.
* If/else formatting: inline each branch if its condition+statement < 100 chars, but put else on its own line; never place both branches on the same line; one semicolon per line.
* In class names (and similar type names), treat well-known acronyms as exceptions to camel case: keep them all caps (e.g., `ECULocalizationIngress`, `UDPTransport`, `VOIPChannel`), not `EcuLocalizationIngress` or `UdpTransport`.

### Interface Naming Conventions

* Prefer verb-style interface names (e.g., `IBridgeEvents`, `IDMXTransport`) over adjective-style names (e.g., `IEventBridgeable`, `IDMXTransportable`).
* Verb-style names describe what the interface does, which is more idiomatic in C# and C++.
* This keeps interface names concise and action-oriented, consistent with common .NET and C++ patterns.

### Unity Meta Files

* Unity expects exactly one `.meta` file per asset. When generating metas, only create them for files that lack one.
* **Never** point meta-generation helpers at files that already end in `.meta`; doing so creates `.meta.meta` (and worse) duplicates that must be deleted.
* If you need bulk metas, filter out existing `.meta` files first (e.g., `Get-ChildItem -Recurse -File | Where-Object { $_.Extension -ne '.meta' }`).

## Automated Compilation Workflow

Claude has a custom automated compilation system for Unity projects to enable AI-assisted error detection and debugging without requiring the Unity Editor to be open. This system mirrors the Unreal Engine's command-line build capabilities.

### Architecture Overview

The system consists of three components:
1. CompilationReporter.cs - Editor script that monitors compilation events
2. CompilationReporterCLI.cs - Command-line interface for batch mode execution
3. `BuildTooling~/CompileProject_Silent.bat` (Windows) — batch script that orchestrates the workflow (lives under a `~` folder so Unity does not import it)

### How It Works

1. Batch script launches Unity in batch mode with -executeMethod flag
2. Unity loads the project and begins compilation
3. CompilationReporter hooks into Unity's CompilationPipeline events
4. Compilation results are written to Temp/CompilationErrors.log
5. Batch script detects report file creation (with 2-minute timeout)
6. Batch script terminates Unity process
7. Batch script returns exit code 0 (success) or 1 (failure)

### Key Design Decisions

- DO NOT use Unity's -quit flag - it exits before the report can be written
- Instead, launch Unity with 'start /B' and manually kill it after report generation
- Use distinctive log markers (e.g., 🤖 [PROJECT AUTO-COMPILE]) for AI readability
- Write report to Temp/CompilationErrors.log (gitignored, ephemeral)
- Include Report ID (GUID) for tracking across multiple runs
- Support both event-driven compilation monitoring and CLI-triggered reports

### Usage for Claude

When you need to check Unity compilation without user intervention:
1. Run: `.\BuildTooling~\CompileProject_Silent.bat` (Windows; run from the HoloCade package root, or invoke by full path)
2. Wait for exit code (0 = success, 1 = failure)
3. Read: Temp/CompilationErrors.log for structured error report
4. Parse errors in format: [Type] file(line,column): message

### File Locations

**Editor Scripts** (must be in Assets/[ProjectName]/Editor/ or similar):
- CompilationReporter.cs - Auto-loads via [InitializeOnLoad]
- CompilationReporterCLI.cs - Provides static CompileAndExit() method

**Batch scripts** (in `BuildTooling~/` next to `package.json`; package root should be the Unity `-projectPath` when compiling):
- `CompileProject_Silent.bat` (Windows)
- `CompileProject.bat` (Windows, verbose)

### Critical Implementation Notes

1. CompilationReporter MUST use [InitializeOnLoad] attribute
2. CompilationReporterCLI.CompileAndExit() MUST NOT call EditorApplication.Exit()
3. Batch script MUST wait for report file creation before killing Unity
4. Use 'start /B' on Windows to launch Unity without blocking
5. Include timeout mechanism (default: 120 seconds) to prevent infinite hangs
6. taskkill /IM Unity.exe /F to forcefully terminate Unity on Windows

### Race Condition Prevention

The original implementation had a race condition where Unity would exit before writing the report. Solution:
- Remove -quit flag from Unity command line
- CompileAndExit() generates report but does NOT exit
- Batch script waits for Temp/CompilationErrors.log to exist
- Batch script adds 2-second grace period after detection
- Batch script explicitly kills Unity process

### Report Format

The report is structured for AI parsing:
```
===========================================
[PROJECT NAME] COMPILATION REPORT
🤖 AI-READABLE AUTOMATED COMPILATION CHECK
===========================================
Generated: YYYY-MM-DD HH:MM:SS
Report ID: PROJECT-XXXXXXXX

[Errors and warnings organized by assembly]

===========================================
Status: SUCCESS | FAILED
===========================================
```

### White-Label Template

A generic white-label version of this system is available in the Claude_Unity_AutoCompilation directory at the repository root. Copy these files into any Unity project:
1. Copy Editor scripts to Assets/[YourProject]/Editor/
2. Copy batch scripts to Unity project root
3. Update Unity executable paths in batch/shell scripts
4. (Optional) Update log markers from "[YOURPROJECT AUTO-COMPILE]" to "[YOURPROJECT AUTO-COMPILE]"
5. (Optional) Update report header from "YOURPROJECT COMPILATION REPORT" to "[YOURPROJECT] COMPILATION REPORT"

**No namespace customization needed** - Scripts use global namespace for simplicity.

### Integration with AI Workflow

This system enables Claude to:
- Compile Unity projects without user intervention
- Detect compilation errors in real-time
- Fix errors iteratively without manual user feedback
- Verify fixes before committing changes
- Match the Unreal Engine workflow for consistency

See Claude_Unity_AutoCompilation/README.md for detailed setup instructions.

## NOOP Marking

When generating new code, maintain awareness of all instances of NOOP parts of the implementation that are intended to be implemented later. Mark them clearly with NOOP comments and list all such instances in summaries of your work in chat when you're done.