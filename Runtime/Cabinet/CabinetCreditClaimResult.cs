// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

namespace HoloCade.Cabinet
{
    /// <summary>
    /// Outcome of evaluating whether a cabinet Start press may consume/bind a credit for a seat.
    /// Paid mode requires a title-provided <see cref="ICabinetPaidCreditPool"/> with available credits;
    /// Free Play mode always grants without reading the pool.
    /// </summary>
    public readonly struct CabinetCreditClaimResult
    {
        public CabinetCreditClaimResult(bool granted, CabinetCreditDisplayMode displayMode)
        {
            Granted = granted;
            DisplayMode = displayMode;
        }

        public bool Granted { get; }
        public CabinetCreditDisplayMode DisplayMode { get; }
    }
}
