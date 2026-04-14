// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using System.Collections.Generic;

namespace HoloCade.Cabinet.Diagnostics
{
    /// <summary>Allows game assemblies to register title-specific diagnostic pages (e.g. piezo strike maps) without HoloCade depending on the game.</summary>
    public static class CabinetDiagnosticsRegistry
    {
        static readonly List<ICabinetDiagnosticPage> CustomPages = new List<ICabinetDiagnosticPage>();

        public static void Register(ICabinetDiagnosticPage page)
        {
            if (page != null && !CustomPages.Contains(page))
                CustomPages.Add(page);
        }

        public static void Unregister(ICabinetDiagnosticPage page)
        {
            if (page != null)
                CustomPages.Remove(page);
        }

        public static IReadOnlyList<ICabinetDiagnosticPage> RegisteredPages => CustomPages;
    }
}
