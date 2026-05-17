// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

namespace HoloCade.Cabinet
{
    /// <summary>
    /// Title-owned paid credit pool surface used by <see cref="CabinetCreditEvaluation"/> when
    /// <see cref="ArcadeCabinetIOConfig.freePlayEnabled"/> is false. Implement on a MonoBehaviour in the title
    /// (e.g. forwarding to <c>GameManager.AvailableCoinCredits</c>).
    /// </summary>
    public interface ICabinetPaidCreditPool
    {
        int AvailableCredits { get; }
    }
}
