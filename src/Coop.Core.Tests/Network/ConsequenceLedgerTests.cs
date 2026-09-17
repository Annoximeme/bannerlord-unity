using System;
using Coop.Core.Network;
using Xunit;

namespace Coop.Core.Tests.Network
{
    public class ConsequenceLedgerTests
    {
        [Fact]
        public void FirstApply_InvokesTheActionAndReturnsTrue()
        {
            var ledger = new ConsequenceLedger();
            int calls = 0;

            bool applied = ledger.TryApply(new ConsequenceId(1, 1), () => calls++);

            Assert.True(applied);
            Assert.Equal(1, calls);
        }

        [Fact]
        public void ReplayedId_NeverInvokesTheActionAgain()
        {
            var ledger = new ConsequenceLedger();
            int calls = 0;
            var id = new ConsequenceId(1, 1);

            ledger.TryApply(id, () => calls++);
            bool secondAttempt = ledger.TryApply(id, () => calls++);
            bool thirdAttempt = ledger.TryApply(id, () => calls++);

            Assert.False(secondAttempt);
            Assert.False(thirdAttempt);
            Assert.Equal(1, calls); // exactly once, no matter how many times it's replayed
        }

        [Fact]
        public void DifferentEpoch_SameSequence_IsNotATreatedAsReplay()
        {
            // serverEpoch increments on every server start specifically so this can't collide.
            var ledger = new ConsequenceLedger();
            int calls = 0;

            ledger.TryApply(new ConsequenceId(serverEpoch: 1, sequence: 1), () => calls++);
            ledger.TryApply(new ConsequenceId(serverEpoch: 2, sequence: 1), () => calls++);

            Assert.Equal(2, calls);
        }

        [Fact]
        public void FailedApply_IsNotRecorded_SoItCanBeRetried()
        {
            var ledger = new ConsequenceLedger();
            var id = new ConsequenceId(1, 1);

            Assert.Throws<InvalidOperationException>(() =>
                ledger.TryApply(id, () => throw new InvalidOperationException("transient failure")));

            Assert.False(ledger.HasApplied(id));

            int calls = 0;
            bool retrySucceeded = ledger.TryApply(id, () => calls++);

            Assert.True(retrySucceeded);
            Assert.Equal(1, calls);
        }

        [Fact]
        public void SnapshotAndRestore_RoundTripTheAppliedSet()
        {
            var original = new ConsequenceLedger();
            original.TryApply(new ConsequenceId(1, 1), () => { });
            original.TryApply(new ConsequenceId(1, 2), () => { });

            var restored = new ConsequenceLedger();
            restored.Restore(original.Snapshot());

            int calls = 0;
            bool appliedAgain = restored.TryApply(new ConsequenceId(1, 1), () => calls++);

            Assert.False(appliedAgain); // restored ledger recognizes it as already applied
            Assert.Equal(0, calls);
        }
    }
}
