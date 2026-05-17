// Copyright (c) 2025 AJ Campbell. Licensed under the MIT License.

using HoloCade.Cabinet;
using NUnit.Framework;

namespace HoloCade.Tests.Editor
{
    sealed class FakePaidPool : ICabinetPaidCreditPool
    {
        public int AvailableCredits { get; set; }
    }

    public class CabinetCreditEvaluationTests
    {
        [Test]
        public void TryClaimCredit_FreePlay_AlwaysGrantsWithoutReadingPool()
        {
            var pool = new FakePaidPool { AvailableCredits = 0 };
            var r = CabinetCreditEvaluation.TryClaimCredit(true, pool, 0);
            Assert.IsTrue(r.Granted);
            Assert.AreEqual(CabinetCreditDisplayMode.FreePlay, r.DisplayMode);
        }

        [Test]
        public void TryClaimCredit_Paid_EmptyPool_Rejects()
        {
            var pool = new FakePaidPool { AvailableCredits = 0 };
            var r = CabinetCreditEvaluation.TryClaimCredit(false, pool, 0);
            Assert.IsFalse(r.Granted);
            Assert.AreEqual(CabinetCreditDisplayMode.NumericPool, r.DisplayMode);
        }

        [Test]
        public void TryClaimCredit_Paid_WithCredits_Grants()
        {
            var pool = new FakePaidPool { AvailableCredits = 3 };
            var r = CabinetCreditEvaluation.TryClaimCredit(false, pool, 0);
            Assert.IsTrue(r.Granted);
            Assert.AreEqual(CabinetCreditDisplayMode.NumericPool, r.DisplayMode);
        }
    }
}
