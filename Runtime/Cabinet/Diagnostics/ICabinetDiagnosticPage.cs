// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using HoloCade.Core.Networking;
using UnityEngine.UIElements;

namespace HoloCade.Cabinet.Diagnostics
{
    /// <summary>
    /// Optional page supplied by a game (e.g. piezo calibration in DodgeThis). Built-in pages live in <see cref="CabinetDiagnosticsHost"/>.
    /// </summary>
    public interface ICabinetDiagnosticPage
    {
        string Title { get; }
        void Build(VisualElement container, CabinetDiagnosticsContext context);
    }

    /// <summary>Services exposed to diagnostic pages (transport + façade + host).</summary>
    public sealed class CabinetDiagnosticsContext
    {
        public ArcadeCabinetBridge Bridge { get; }
        public HoloCadeUDPTransport Transport { get; }

        public CabinetDiagnosticsContext(ArcadeCabinetBridge bridge, HoloCadeUDPTransport transport)
        {
            Bridge = bridge;
            Transport = transport;
        }
    }
}
