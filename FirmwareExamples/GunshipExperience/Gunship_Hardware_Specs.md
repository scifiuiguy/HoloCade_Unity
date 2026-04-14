# Gunship Experience — Solenoid Kicker Hardware Spec (Alpha)

Purpose: Define affordable-pro and high-end-pro options for a jolting recoil haptic at each gun station, along with electrical, control, and safety constraints. Keep ECU code and transport agnostic across supported embedded platforms.

## Summary
- Haptic type: Linear pull-type solenoid producing short, sharp recoil pulses transmitted through the controller chassis to the handlebars (not directly into the user's hands).
- Default system voltage: 24 VDC.
- Duty model: Intermittent pulses (50–200 ms) with enforced cool-off to protect coils.
- ECU: One embedded controller per gun; reports button state and tracker pose to the primary Gunship ECU over UDP; receives fire commands or runs locally gated logic.

## Recommended Default Configuration

**Default Setup (Affordable-Pro with Redundancy):**
- **Solenoids:** 2× affordable-pro pull-type solenoids
  - **Guardian Electric T8X16-I-24D** (recommended): 24VDC, 34.75N @ 10.16mm stroke, intermittent duty, 16.8W, 36.1Ω
  - **Johnson Electric Ledex D/PL series** (verify availability): 24VDC, 20–40N @ 5–8mm stroke target (contact manufacturer/distributor for current part numbers)
- **Drivers:** 2× Pololu G2 High-Power Motor Driver 24v13 (one per solenoid for independent control and thermal isolation)
- **Thermal management:** Each Pololu G2 equipped with:
  - Small aluminum heatsink (20×30mm, 10–15mm height) with thermal paste
  - Dedicated 40–50mm DC fan (5V or 12V, ~0.1A) blowing across driver and heatsink
- **Benefits of this configuration:**
  - Redundancy: N=2 solenoids with automatic temperature-based alternation
  - Independent driver per solenoid: Better thermal isolation, no shared driver failure point
  - Robust thermal management: Heatsink + fan per driver provides maximum cooling headroom
  - Production-ready: Suitable for high-duty commercial installations
  - Cost-effective: Affordable-pro solenoids with professional-grade drivers and cooling

**Cost estimate (per gun station):** ~$150–$200 (2× solenoids + 2× Pololu G2 + 2× heatsinks + 2× fans + ECU + misc hardware)

## Recommended Solenoid Options

- Affordable‑Pro (Default)
  - **Guardian Electric T8X16-I-24D** (recommended part number):
    - Voltage: 24 VDC
    - Stroke: 10.16 mm (0.4")
    - Force at stroke: 34.75 N (3.5 kgf)
    - Coil resistance: 36.1 Ω
    - Power: 16.8 W
    - Current: ~0.66 A peak
    - Duty cycle: Intermittent
    - Dimensions: 25.4 mm diameter × 51.8 mm length
    - Distributor: Newark (P/N: 03M5759), Digi-Key, Mouser
  - **Johnson Electric Ledex D/PL series** (alternative, verify current part numbers):
    - Voltage: 24 VDC (target)
    - Stroke: 5–8 mm (target window)
    - Force at stroke: 20–40 N (2–4 kgf) target
    - Response: < 20 ms mechanical rise (typical), total pulse 50–200 ms
    - Coil resistance: ~8–24 Ω (indicative; verify per model)
    - Current: 1–3 A peak (24–72 W)
    - Duty cycle: Intermittent; minimum 800 ms recovery for 100 ms pulse at room temp (tune per datasheet/thermal tests)
    - **Note:** Contact Johnson Electric or distributor (Digi-Key, Mouser) for current 24VDC models matching these specifications

- High‑End‑Pro (Upmarket)
  - Manufacturer families: Magnet‑Schultz (MS series), Johnson Electric premium lines
  - Example characteristics (target window, verify per model):
    - Voltage: 24 VDC
    - Stroke: 8–10 mm
    - Force at stroke: 40–60 N (4–6 kgf)
    - Improved thermal handling (higher copper fill / larger mass)
    - Optional noise‑reduced or guided plunger variants for smoother impact shaping

Notes:
- Prefer pull‑type geometry for compact mounting and recoil feel.
- Use a small impact mass (50–100 g) on the plunger to shape the impulse.
- Avoid continuous duty coils; this is an intermittent pulse application.

## Optional Redundancy & Thermal Management (Recommended for High-Duty Operation)

**Multi-Solenoid Redundancy:**
- Install N identical solenoids per gun station (N ≥ 1; limited only by microcontroller GPIO/ADC availability).
- All solenoids mounted in parallel within the chassis; share the same impact path to handlebars.
- ECU alternates which solenoid is active per play session based on temperature (selects coolest available).

**Temperature Monitoring:**
- One NTC thermistor (10 kΩ @ 25°C, B=3950K typical) per solenoid, mounted on or near the coil housing.
- Temperature read via ADC on ECU (voltage divider: NTC + 10 kΩ reference resistor per channel).
- Temperature range: 0–100°C (coil operating range); target keep-below: 70°C for longevity.
- ECU maintains temperature array for all N solenoids.

**Alternation Logic:**
- At session start: ECU reads all N solenoid temperatures; selects the coolest solenoid below 80°C threshold.
- During session: Active solenoid handles all fire commands; inactive solenoids remain off (cooling).
- Session end: Log final temperatures for all solenoids; next session automatically selects the coolest available unit.
- Fallback: If active solenoid exceeds 80°C or faults, automatically switch to the next coolest available solenoid (if below 80°C).
- Priority: Always prefer the coolest available solenoid; if all exceed 80°C, select coolest and log thermal warning.

**Throttling Logic (PWM Duty Reduction):**
- **Purpose:** Reduce solenoid coil power (via PWM duty reduction) when system-wide thermal issues occur, even with redundancy enabled.
- **Trigger conditions:** 
  - All N solenoids exceed 70°C (approaching 80°C threshold)
  - PWM driver module temperature exceeds 70°C (more critical than coil temp)
  - Rate of fire exceeds safe thermal limits
- **Throttling behavior:**
  - Reduce PWM duty cycle from 100% (full power) to 50–80% (reduced power) to lower coil current and driver dissipation.
  - Maintain recoil feel: Even at 50% duty, solenoid still provides noticeable haptic feedback (force scales roughly with current).
  - Gradual reduction: Step down duty in 10% increments as temperature rises (100% → 90% → 80% → 70% → 60% → 50%).
  - Recovery: Gradually restore duty as temperatures cool below thresholds.
- **Priority order:** 
  1. First: Switch to cooler solenoid (if redundancy enabled)
  2. Second: Throttle PWM duty (if all solenoids hot or driver module hot)
  3. Last: Thermal shutdown (if driver module > 85°C or all solenoids > 85°C)
- **Note:** Throttling is a software solution that works even with single-solenoid (N=1) installations; redundancy provides additional headroom but throttling is still useful for extreme duty cycles.

**Benefits:**
- Extended hardware life: Each solenoid gets recovery time between sessions.
- Higher throughput: Supports back-to-back play sessions without forced cooldown delays (scales with N).
- Fault tolerance: Multiple solenoid failures don't disable the gun station (N-1 redundancy).
- Thermal headroom: Allows more aggressive duty cycles per solenoid while maintaining safety margins.
- Scalability: Add more solenoids for higher-duty installations (limited only by hardware/GPIO constraints).

**Implementation Notes:**
- ECU must track which solenoid is active per session; report active solenoid ID (0 to N-1) in telemetry.
- Temperature readings: 1 Hz minimum (10 Hz recommended for responsive switching); read all N channels.
- Solenoids can share MOSFET driver circuit (add relay/multiplexer to select active coil) OR use separate drivers with ECU GPIO selection (one GPIO per solenoid enable).
- Cost impact: ~N× solenoid cost + N× NTC sensors + relay/multiplexer or N× driver circuits (~$50–$150 per additional solenoid).
- Typical configurations: N=1 (single, no redundancy), N=2 (dual redundancy, recommended default), N≥3 (high-duty installations).

## Mechanical Integration
- Controller chassis: Aluminum enclosure centered between vertical handlebars; rigid coupling to handle structure to transmit impulse without metal‑to‑skin impact.
- Plunger/impact path: Plunger strikes internal mass/stop; impulse transmits through chassis to handlebars. Add thin elastomer interface to reduce audible metallic "ring" while preserving feel.
- Handlebar spacing & barrel length: Ensure users cannot reach through the gun center while strapped; recommended barrel ≥ 30–40 cm from handle centerline to tip.
- Vibration isolation: Use rubber grommets/dampers between solenoid mount and chassis to tune spectral content and reduce peak shock to electronics.

## Electrical & Power
- Bus voltage: 24 VDC per gun station.
- Per‑gun PSU budget: 3–5 A (72–120 W) headroom for inrush and back‑to‑back pulses.
- Coil protection: Mandatory flyback diode across coil; consider TVS diode for surge.
- Driver stage: N‑channel MOSFET rated ≥ 40 V, ≥ 20 A pulsed; logic‑level gate; include gate resistor (10–47 Ω) and fast diode.
- **Note:** Solenoids are typically sold as bare components (coil + plunger assembly) without integrated PWM drivers. You must build your own driver circuit (MOSFET + protection diodes) or purchase separate driver boards. Some manufacturers offer optional driver modules, but these are not standard inclusions.
- **Drop-in PWM Driver Module Options (Recommended for Faster Development):**
  - **IBT-2 H-Bridge Module** (BTS7960-based): ~$8–$15; handles 24V, 43A peak; PWM capable; includes flyback protection. Designed for motors but works for solenoids (use one half-bridge). Logic-level control (3.3V/5V compatible).
    - **Thermal considerations:** BTS7960 is sufficient for 1–3A solenoids with our duty cycle (50–200ms pulses, ~11% duty). At 3A pulse current, power dissipation ~0.2W per pulse; average ~0.02W with our duty cycle. Module can overheat without heatsink at continuous 3A, but our pulsed application is well within limits.
  - **Pololu G2 High-Power Motor Driver 24v13** (DRV8871-based): ~$25–$35; handles 24V, 13A continuous; PWM capable; better thermal design and protection than IBT-2. More robust for production use, higher cost.
  
- **PWM Driver Thermal Management (Primary Thermal Concern):**
  - **Note:** PWM driver modules (IBT-2, Pololu G2) are the most likely temperature culprit, not the solenoid coils. Coils can be throttled via PWM duty reduction, but driver modules have fixed thermal limits.
  - **Heatsink (Recommended):** Small aluminum heatsink (20×20mm to 30×30mm, 10–15mm height) attached to driver module with thermal paste. Cost: ~$2–$5. **Required for IBT-2 in high-duty applications or ambient temp > 40°C.**
  - **Fan (Optional, Last Line of Defense):** Small 40mm or 50mm DC fan (5V or 12V, ~0.1A) mounted to blow air across driver module and heatsink. Cost: ~$3–$8. **Use if driver module temperatures exceed 70°C even with heatsink, or for high-duty installations (N≥3 solenoids).**
  - **Temperature monitoring:** Add NTC thermistor on driver module (if not already present) to monitor driver temperature independently from solenoid coil temperatures.
  - **Thermal shutdown:** ECU should disable driver if module temperature exceeds 85°C to prevent permanent damage.
  - **SparkFun Solenoid Driver** (smaller solenoids only): Not suitable for 1–3A coils; max ~500mA.
  - **Custom MOSFET Driver Board:** Build your own using IRF540N/IRFZ44N MOSFET + gate driver IC (TC4427) + protection diodes; ~$5–$10 in parts but requires PCB design.
  - **Note:** BLDC ESCs (Electronic Speed Controllers) are NOT suitable for solenoids; they drive 3-phase brushless motors, not inductive loads. Use H-bridge or single MOSFET drivers instead.
- Optional PWM: 200–1 kHz for intensity shaping; implement via ECU GPIO PWM output driving driver module enable pin; default on/off is acceptable if PWM not needed.
- Harnessing: Locking connectors; strain relief inside chassis; segregate logic and coil grounds at star point near PSU return.

## Controls & Safety
- Pulse envelope: 50–200 ms; clamp rate of fire to protect coil (e.g., ≥ 8× pulse off‑time).
- Force limit: Target ≤ 60 N at installed stroke; rely on mechanical tuning if needed.
- Thermal limits: 
  - Track coil temperature via NTC or estimate via I²t; enforce cool‑off.
  - **Track PWM driver module temperature** (primary thermal concern); monitor independently from coil temps.
  - Throttle PWM duty if driver module > 70°C or all coils > 70°C (see Throttling Logic section).
  - Thermal shutdown if driver module > 85°C or all coils > 85°C.
- E‑stop: Coil disabled on emergency stop; driver defaults open; watchdog required.

## Communication Architecture

**Network Topology (Star Configuration):**
```
┌─────────────────────────────────────────────────────────────┐
│  Game Engine Server (Unreal/Unity)                          │
│  ─────────────────────────────────────────────────────────  │
│  • Receives aggregated gun/platform state from Gunship ECU  │
│  • Sends fire commands and motion control to Gunship ECU     │
│  • Manages VR headset connections (wireless)                │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        │ Wireless (WiFi/UDP)
                        │ HoloCade UDP Protocol
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│  Primary Gunship ECU (Onboard Scissor Lift Chassis)         │
│  ─────────────────────────────────────────────────────────  │
│  • Aggregates 4× Gun ECU data                               │
│  • Controls scissor lift platform (pitch/roll/Y/Z)          │
│  • Relays fused gun/platform state to game engine           │
│  • Optionally arbitrates fire authorization                 │
│  • Manages SteamVR Ultimate tracker relay (if centralized)  │
└───────┬─────────────────────────────────────────────────────┘
        │
        │ Star Topology (4× Gun ECUs)
        │
        ├──────────┬──────────┬──────────┬──────────┐
        │          │          │          │          │
        ▼          ▼          ▼          ▼          ▼
┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌─────────────┐
│ Gun ECU #1  │ │ Gun ECU #2  │ │ Gun ECU #3  │ │ Gun ECU #4  │
│ (Station 1) │ │ (Station 2) │ │ (Station 3) │ │ (Station 4) │
│ ─────────── │ │ ─────────── │ │ ─────────── │ │ ─────────── │
│ •Dual       │ │ •Dual       │ │ •Dual       │ │ •Dual       │
│  buttons    │ │  buttons    │ │  buttons    │ │  buttons    │
│ •N solenoids│ │ •N solenoids│ │ •N solenoids│ │ •N solenoids│
│ •Tracker    │ │ •Tracker    │ │ •Tracker    │ │ •Tracker    │
│  pose       │ │  pose       │ │  pose       │ │  pose       │
└─────────────┘ └─────────────┘ └─────────────┘ └─────────────┘
```

**Connection Options:**

**Gun ECUs → Primary Gunship ECU:**
- **Recommended (Lower Latency):** Wired Ethernet (CAT5/CAT6) — star topology, all 4 Gun ECUs connect to Primary Gunship ECU via Ethernet switch or direct point-to-point.
  - Latency: < 1 ms (local network)
  - Reliability: High (no wireless interference)
  - Setup: Requires Ethernet wiring on chassis (all ECUs onboard same platform)
- **Alternative (Flexibility):** Wireless (WiFi/UDP) — all ECUs on same WiFi network.
  - Latency: 5–20 ms (WiFi dependent)
  - Reliability: Good (LAN conditions)
  - Setup: Easier (no wiring), but higher latency
- **Protocol:** HoloCade UDP binary protocol (same as engine communication)
- **Update Rate:** 10–30 Hz (gun telemetry), 30–100 Hz (tracker pose if relayed)

**Primary Gunship ECU → Game Engine Server:**
- **Connection:** Wireless (WiFi/UDP) — Gunship ECU connects to game engine server on LAN
- **Protocol:** HoloCade UDP binary protocol
- **Update Rate:** 30–60 Hz (aggregated state relay)

**Game Engine Server → VR Headsets:**
- **Connection:** Wireless (WiFi/UDP or proprietary VR protocol) — game engine manages VR headset connections
- **Protocol:** Engine-specific (Unreal networking, Unity networking, etc.)
- **Update Rate:** 60–90 Hz (VR frame rate dependent)

**Note:** All ECUs are onboard the same physical chassis (scissor lift platform), making wired connections practical and recommended for lowest latency gun control.

## ECU Architecture (Agnostic)

**Per‑Gun ECU Responsibilities:**
- Read dual thumb buttons (debounce, rate limit, safety lockouts).
- Drive solenoid coil per pulse envelope with thermal/rate guard.
- **If redundancy enabled:** Monitor all N solenoid temperatures; select coolest solenoid at session start; alternate per session; handle automatic fallback on thermal fault.
- Read gun tracker pose (SteamVR Ultimate tracker relayed by primary ECU or local receiver; implementation‑specific).
- Report telemetry to primary Gunship ECU at 10–30 Hz: button states, last fire timestamps, active solenoid ID (0 to N-1, if redundant), coil temperatures (array of N temperatures if redundant), fault flags, total solenoid count N.
- **Connection:** Wired Ethernet (recommended) or WiFi to Primary Gunship ECU.

**Primary Gunship ECU Responsibilities:**
- Aggregate four Gun ECUs data (star topology).
- Control scissor lift platform directly (pitch/roll/Y/Z translation via actuators and lift mechanism).
- Relay gun states and fused platform state to game engine via HoloCade UDP (wireless).
- Optionally arbitrate fire authorization (central gating) and log KPIs.
- Manage SteamVR Ultimate tracker relay if centralized (or each Gun ECU handles its own tracker).
- **Connection:** Wired Ethernet (recommended) or WiFi to 4× Gun ECUs; Wireless to game engine server.
- **Note:** Primary Gunship ECU and Scissor Lift ECU are the same unit (integrated platform control).

## Engine‑Side Interfaces
- Tracker nodes: One transform per gun; sourced from station ECU/primary ECU poses.
- Events: FirePressed/Released, FireAuthorized, RecoilFeedbackRequested (for synthesis if physical coil disabled).
- Parameters: Pulse ms, intensity (PWM duty), rate caps, safety lockout toggles.
- Replication: Per‑station IDs stable across sessions; map to player seats.

## Tracker Vibration Dampening (Required)

**Problem:** Solenoid recoil pulses cause mechanical vibration that shakes the SteamVR Ultimate tracker mounted on the gun nose, resulting in tracking jitter and position/rotation noise in the engine.

**Hardware Solution (First Layer):**
- Rubber-mount the tracker: Use compliant rubber grommets or vibration-damping mounts between tracker and gun chassis.
- Isolation frequency: Target isolation for 50–200 Hz (solenoid pulse frequency range).
- Mounting strategy: Decouple tracker from direct chassis contact; allow small relative motion to absorb impulse energy.

**Software Solution (Second Layer — Required):**
- **Normalized engine-side filtering:** Implement software dampening/filtering on tracker transform data before use in game logic.
- Filter type: Low-pass filter (exponential moving average or Butterworth) on position and rotation.
- Cutoff frequency: 10–20 Hz (smooth out high-frequency jitter while preserving intentional gun movement).
- Alternative: Kalman filter for position/rotation smoothing with motion prediction.
- Implementation: Apply filter to tracker transform in `AGunshipExperience` or dedicated tracker node component before exposing to game code.
- **Note:** Software filtering is mandatory even with rubber mounting; hardware isolation alone is insufficient for smooth tracking during active firing.

**Validation:**
- Measure tracker jitter RMS during active firing (with and without filtering).
- Target: < 1 mm position jitter, < 0.5° rotation jitter after filtering.
- Test with various fire rates and intensities to ensure filter doesn't lag intentional rapid movements.

## HoloCade UDP Transport (Indicative Mapping)
- Channels (example; finalization in protocol doc):
  - Ch10+n (n=0..3): Fire command bools (engine→station ECU), or station→primary ECU status
  - Ch20+n: Intensity (0.0–1.0) or envelope index
  - Ch30+n: Telemetry (active solenoid temp or duty proxy) as float
  - **Ch40+n (if redundant):** Active solenoid ID (0 to N-1) as int32
  - **Ch50+n (if redundant):** Total solenoid count N as int32
  - **Struct 160+n (if redundant):** Solenoid temperature array struct (N temperatures as float array, max N=16 for struct size limits)
  - **Alternative (if N>16):** Use multiple float channels Ch60+n through Ch(60+N-1)+n for individual solenoid temperatures
  - Struct 150+n: Gun pose struct (orientation/position) per station
  - Ch7: Global emergency stop (already standardized)

## Supported Embedded Platforms (Agnostic ECU Targeting)
- ESP32 (default prototype): Wi‑Fi/UDP; dual buttons; MOSFET driver; PWM capable.
- STM32 (F4/F7/H7): Ethernet/UDP or Wi‑Fi; hardware timers; robust RT control.
- Arduino (SAM/AVR with ESP8266/ESP32 coprocessor): Entry option; reduced timing fidelity.
- Raspberry Pi (Zero 2/4) + HAT: Linux UDP; use dedicated driver HAT for coil.
- NVIDIA Jetson Nano/Orin Nano: Overkill but viable; use GPIO expander and driver board.

All platforms must implement the same HoloCade binary UDP framing and channel/struct mappings to remain engine‑agnostic.

## Bill‑of‑Materials (Per Gun Station — Indicative)

**Recommended Default Configuration (Dual Solenoids, Dual Pololu G2, Heatsinks + Fans):**
- 2× Affordable-pro solenoids:
  - **Guardian Electric T8X16-I-24D** (recommended): 24VDC, 34.75N @ 10.16mm stroke, intermittent duty
  - **Johnson Electric Ledex D/PL series** (alternative): 24VDC, 20–40N @ 5–8mm stroke target (verify current part numbers with manufacturer/distributor)
- 2× Pololu G2 High-Power Motor Driver 24v13 (~$25–$35 each)
- 2× Small aluminum heatsinks (20×30mm, 10–15mm height, ~$2–$5 each)
- 2× 40–50mm DC fans (5V or 12V, ~0.1A, ~$3–$8 each)
- 2× NTC thermistors (10 kΩ @ 25°C) for solenoid temperature monitoring
- 1× NTC thermistor for PWM driver temperature monitoring (optional, if not built into Pololu)

**Network Communication (Wired Ethernet Recommended):**
- **ESP32:** ESP32 development board + Ethernet PHY module
  - **Recommended PHY:** LAN8720A (most common, ~$1–$3, widely available)
  - **Alternatives:** TLK110, DP83848, RTL8201
  - **Pre-built boards:** ESP32-Ethernet-Kit (Olimex), ESP32-POE (Olimex), or custom PCB with ESP32 + LAN8720
  - **Cabling:** Standard CAT5e or CAT6 Ethernet cable (RJ45 connectors)
- **STM32:** STM32 board with built-in Ethernet MAC + external PHY (same PHY chips as ESP32)
- **Arduino:** Arduino Ethernet Shield (W5500 chip) or similar
- **Raspberry Pi / Jetson Nano:** Built-in Ethernet port (no additional hardware needed)

**Note:** All platforms use standard Ethernet cables (CAT5e/CAT6). The PHY module is only needed for ESP32 and some STM32 boards that don't have built-in Ethernet.

**Additional Components:**
- Embedded ECU (ESP32 or STM32) + enclosure + connectors
- **Total per station:** ~$150–$200

**Alternative Configurations:**
- **Single solenoid (N=1, no redundancy):** 1× solenoid + 1× Pololu G2 + 1× heatsink + 1× fan (~$80–$120)
- **High-duty (N≥3):** 3× solenoids + 3× Pololu G2 + 3× heatsinks + 3× fans (~$220–$300)
- **Budget prototype:** 1× solenoid + 1× IBT-2 module + 1× heatsink + optional fan (~$30–$50)
- **Custom build:** N× solenoids + custom MOSFET driver PCB + heatsinks/fans as needed

**Common Components (All Configurations):**
- 24 VDC PSU (3–5 A) or shared bus feed with local fuse
- Tracker mount for SteamVR Ultimate tracker at gun nose
- Aluminum chassis, impact mass/stop, elastomer pads, hardware
- **Network connectivity (recommended wired):**
  - 4× Ethernet cables (CAT5/CAT6, appropriate lengths for chassis routing) for 4× Gun ECUs → Primary Gunship ECU
  - Small Ethernet switch (5–8 port, unmanaged) OR direct point-to-point connections if Gunship ECU has multiple Ethernet ports
  - **Alternative:** WiFi modules (if wireless option chosen) — ESP32 has built-in WiFi; STM32 may need external WiFi module
  - **Note:** Primary Gunship ECU handles scissor lift control directly (no separate Scissor Lift ECU)

## Memory Requirements & Platform Analysis

### Memory Usage Analysis

**Gunship ECU Telemetry Memory Footprint:**

**Static Allocations (persistent):**
- `FGunECUState[4]`: ~100-120 bytes per station (with struct padding) = **~400-480 bytes total**
  - Button states: 4 bools = 4 bytes
  - Temperature data: 10 floats = 40 bytes
  - Solenoid state: 2 uint8_t + 1 bool + 1 float = 6 bytes
  - Fire command: 1 bool + 1 float + 2 unsigned long = 13 bytes
  - System state: 3 bools = 3 bytes
  - Tracker pose: 7 floats = 28 bytes
  - Timestamp: 1 unsigned long = 4 bytes
  - Struct padding/alignment: ~10-20 bytes per struct
- `IPAddress[4]`: 16 bytes (4 bytes per IP address)
- `uint16_t[4]`: 8 bytes (Gun ECU ports)
- `unsigned long[4]`: 16 bytes (telemetry timestamps)
- UDP receive buffer: 256 bytes
- **Total static RAM: ~700-800 bytes**

**Stack Allocations (temporary, when sending):**
- `FGunButtonEvents`: ~12-16 bytes (created in `SendGunButtonEvents()`, freed after use)
- `FGunTelemetry`: ~108-120 bytes (created in `SendGunTelemetry()`, freed after use)
- **Peak stack usage: ~120 bytes**

**Network Stack Overhead:**
- WiFiUDP/EthernetUDP internal buffers: ~1-2KB
- WiFi/Ethernet stack: Platform-dependent (see platform analysis below)

### Platform-Specific Memory Analysis

#### **ESP32 (Recommended)**
- **Total SRAM:** ~520KB
- **Available for user code:** ~320KB (WiFi stack uses ~200KB)
- **Heap available:** ~280KB (after static allocations)
- **Our usage:** ~700-800 bytes static + ~50-60KB with WiFi stack
- **Headroom:** ~260KB remaining
- **Verdict:** ✅ **More than sufficient** — < 0.3% of available memory used

#### **ESP8266**
- **Total SRAM:** ~80KB
- **Available for user code:** ~50KB (WiFi stack uses ~30KB)
- **Our usage:** ~700-800 bytes static + ~30-40KB with WiFi stack
- **Headroom:** ~10-20KB remaining
- **Verdict:** ✅ **Sufficient** — ~1.5% of available memory used, but tight if adding features

#### **STM32 (F4/F7/H7 series)**
- **STM32F4:** 128-192KB SRAM
- **STM32F7:** 320-512KB SRAM
- **STM32H7:** 1MB SRAM
- **Ethernet stack:** ~20-30KB (lighter than WiFi)
- **Our usage:** ~700-800 bytes static + ~20-30KB with Ethernet stack
- **Headroom:** 100KB+ (F4), 300KB+ (F7), 900KB+ (H7)
- **Verdict:** ✅ **More than sufficient** — < 0.1% of available memory used

#### **Arduino (SAM/AVR with ESP8266/ESP32 coprocessor)**
- **Arduino Uno (AVR):** 2KB SRAM — ❌ **Insufficient** (WiFi stack alone exceeds this)
- **Arduino Due (SAM):** 96KB SRAM — ✅ **Sufficient** if using ESP8266/ESP32 coprocessor for networking
- **Note:** Arduino + WiFi Shield uses coprocessor for networking, so main MCU only handles GPIO
- **Verdict:** ✅ **Sufficient** (with coprocessor), ❌ **Insufficient** (standalone AVR)

#### **Raspberry Pi (Zero 2/4)**
- **RAM:** 512MB-8GB (system RAM, not SRAM)
- **Our usage:** Negligible (< 1MB)
- **Verdict:** ✅ **More than sufficient** — Linux system with abundant RAM

#### **NVIDIA Jetson Nano/Orin Nano**
- **RAM:** 4GB-16GB (system RAM)
- **Our usage:** Negligible (< 1MB)
- **Verdict:** ✅ **More than sufficient** — Linux system with abundant RAM

### Memory Optimization Recommendations

**If memory becomes constrained (e.g., ESP8266 with additional features):**
1. **Reduce `SolenoidTemps[8]` to `[2]`** if only dual redundancy is needed: saves ~24 bytes per station (96 bytes total)
2. **Use `uint16_t` for timestamps** instead of `unsigned long` if millisecond precision over ~65 seconds is sufficient: saves 2 bytes per timestamp (8 bytes total)
3. **Pack bools more efficiently** (though compiler alignment may limit gains)
4. **Use `PROGMEM` for constant strings** on AVR platforms (not applicable to ESP32/STM32)

### Conclusion

**Memory is not a concern** for the Gunship ECU telemetry system on all recommended platforms:
- ✅ **ESP32:** Excellent headroom (~260KB remaining)
- ✅ **STM32:** Excellent headroom (100KB+ remaining)
- ✅ **Raspberry Pi/Jetson:** Abundant system RAM
- ⚠️ **ESP8266:** Sufficient but tighter (~10-20KB remaining)
- ❌ **Standalone Arduino AVR:** Insufficient (must use coprocessor for networking)

The current design is memory-efficient and well within the capabilities of all supported platforms except standalone AVR (which requires a WiFi coprocessor anyway).

## Validation & Test
- Bench tests: Pulse energy, thermal rise vs duty; audible/noise profile; impulse feel rating.
- In‑rig tests: Vibration transmission to handlebars; user comfort; long‑run soak with enforced duty limits; E‑stop behavior.
- **Redundancy tests (if enabled):** Temperature alternation accuracy across all N solenoids; session-to-session switching (selects coolest); fault detection and automatic fallback to next coolest; long-term wear distribution across all solenoids; verify behavior with N=1, N=2, and N≥3 configurations.
- **Thermal management tests:** PWM driver module temperature monitoring; throttling logic activation and recovery; heatsink/fan effectiveness; thermal shutdown behavior; verify throttling maintains acceptable recoil feel at reduced duty cycles.

## Notes
- This document defines target envelopes; finalize exact part numbers during procurement with vendor datasheets and availability checks.

