# HoloCade Cabinet — next steps (Unity)

This file tracks **intended SDK work** under `Runtime/Cabinet/`. It is not a commitment schedule.

## Diagnostics as a shared SDK surface

Operator / **QC diagnostics** for arcade cabinets should live **in the HoloCade SDK**, not only in individual games. Behavior is expected to be **very similar across cabinets** (switch tests, monitor patterns, sound, lamps/LEDs, version info, etc.), with **title-specific pages** registered on top for hardware that varies by title (see `Diagnostics/CabinetDiagnosticsHost`, `ICabinetDiagnosticPage`, `CabinetDiagnosticsRegistry`).

**Games** should stay thin: wire cabinet I/O through `ArcadeCabinetBridge` / `HoloCadeUDPTransport`, register optional custom diagnostic pages, and avoid duplicating a full QC stack per title.

## Future: Diagnostics GUI + boot entry

- **Diagnostics GUI (full interface):** A complete, SDK-owned UI for **manual QC** of connected hardware—inputs, outputs, LEDs, solenoids, displays, audio, link status, firmware/SDK versions, and other checks appropriate to HoloCade UDP peers.
- **Optional boot / launcher integration:** Support launching this diagnostics shell from a **boot menu** (e.g. before the game exe runs full attract, or from a dedicated “service mode” entry) so venue ops and factory can run **hardware verification** without loading title-specific gameplay.
- **Unity implementation:** UI Toolkit–oriented host (`CabinetDiagnosticsHost`) is a starting point; expand into the above with clear public API for embedding or standalone scene.
