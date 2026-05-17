// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

namespace HoloCade.Cabinet
{
    /// <summary>
    /// HoloCade-owned rules for paid vs Free Play cabinet credit claims. Titles call this before mutating
    /// their own pool or lobby roster; pool decrement stays title-side for paid mode.
    /// </summary>
    public static class CabinetCreditEvaluation
    {
        /// <param name="freePlayEnabled">From <see cref="ArcadeCabinetBridge.IsFreePlayEnabled"/> (backed only by <see cref="ArcadeCabinetIOConfig.freePlayEnabled"/>).</param>
        /// <param name="paidPool">Required when <paramref name="freePlayEnabled"/> is false; may be null in Free Play (ignored).</param>
        public static CabinetCreditClaimResult TryClaimCredit(bool freePlayEnabled, ICabinetPaidCreditPool paidPool, int playerSlotIndex)
        {
            _ = playerSlotIndex;
            if (freePlayEnabled)
                return new CabinetCreditClaimResult(true, CabinetCreditDisplayMode.FreePlay);

            if (paidPool == null || paidPool.AvailableCredits < 1)
                return new CabinetCreditClaimResult(false, CabinetCreditDisplayMode.NumericPool);

            return new CabinetCreditClaimResult(true, CabinetCreditDisplayMode.NumericPool);
        }
    }
}
