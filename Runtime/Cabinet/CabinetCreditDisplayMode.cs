// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

namespace HoloCade.Cabinet
{
    /// <summary>
    /// How a title should present cabinet credits in the UI. <see cref="FreePlay"/> means no paid pool UX
    /// (titles typically hide numeric tallies and treat Start as always eligible).
    /// </summary>
    public enum CabinetCreditDisplayMode
    {
        NumericPool = 0,
        FreePlay = 1
    }
}
