using Hairibar.Ragdoll;
using NUnit.Framework;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollPuppetCollisionProcessorTests
    {
        [Test]
        public void AcceptsEnterAtInclusiveSquaredImpulseThreshold()
        {
            RagdollPuppetCollisionProcessor processor =
                new RagdollPuppetCollisionProcessor();

            RagdollPuppetCollisionRejectionReason reason;
            bool accepted = processor.TryAccept(
                1f,
                RagdollCollisionPhase.Enter,
                7,
                4f,
                1 << 7,
                4f,
                3,
                out reason);

            Assert.That(accepted, Is.True);
            Assert.That(reason, Is.EqualTo(
                RagdollPuppetCollisionRejectionReason.None));
            Assert.That(processor.Snapshot.AcceptedCount, Is.EqualTo(1));
        }

        [Test]
        public void AcceptsStayButRejectsExit()
        {
            RagdollPuppetCollisionProcessor processor =
                new RagdollPuppetCollisionProcessor();
            RagdollPuppetCollisionRejectionReason reason;

            Assert.That(processor.TryAccept(
                2f,
                RagdollCollisionPhase.Stay,
                0,
                1f,
                -1,
                0f,
                3,
                out reason), Is.True);

            Assert.That(processor.TryAccept(
                2f,
                RagdollCollisionPhase.Exit,
                0,
                1f,
                -1,
                0f,
                3,
                out reason), Is.False);
            Assert.That(reason, Is.EqualTo(
                RagdollPuppetCollisionRejectionReason.UnsupportedPhase));
            Assert.That(processor.Snapshot.RejectedPhaseCount, Is.EqualTo(1));
        }

        [Test]
        public void RejectsLayersOutsideMask()
        {
            RagdollPuppetCollisionProcessor processor =
                new RagdollPuppetCollisionProcessor();
            RagdollPuppetCollisionRejectionReason reason;

            bool accepted = processor.TryAccept(
                3f,
                RagdollCollisionPhase.Enter,
                8,
                10f,
                1 << 7,
                0f,
                3,
                out reason);

            Assert.That(accepted, Is.False);
            Assert.That(reason, Is.EqualTo(
                RagdollPuppetCollisionRejectionReason.LayerFiltered));
            Assert.That(processor.Snapshot.RejectedLayerCount, Is.EqualTo(1));
        }

        [Test]
        public void RejectsImpulseBelowSquaredThreshold()
        {
            RagdollPuppetCollisionProcessor processor =
                new RagdollPuppetCollisionProcessor();
            RagdollPuppetCollisionRejectionReason reason;

            bool accepted = processor.TryAccept(
                4f,
                RagdollCollisionPhase.Enter,
                0,
                3.999f,
                -1,
                4f,
                3,
                out reason);

            Assert.That(accepted, Is.False);
            Assert.That(reason, Is.EqualTo(
                RagdollPuppetCollisionRejectionReason.BelowThreshold));
            Assert.That(processor.Snapshot.RejectedThresholdCount, Is.EqualTo(1));
        }

        [Test]
        public void FilteredCallbacksDoNotConsumeAcceptedBudget()
        {
            RagdollPuppetCollisionProcessor processor =
                new RagdollPuppetCollisionProcessor();
            RagdollPuppetCollisionRejectionReason reason;

            Assert.That(processor.TryAccept(
                5f,
                RagdollCollisionPhase.Enter,
                2,
                10f,
                1 << 1,
                0f,
                1,
                out reason), Is.False);

            Assert.That(processor.TryAccept(
                5f,
                RagdollCollisionPhase.Enter,
                1,
                10f,
                1 << 1,
                0f,
                1,
                out reason), Is.True);

            Assert.That(processor.TryAccept(
                5f,
                RagdollCollisionPhase.Stay,
                1,
                10f,
                1 << 1,
                0f,
                1,
                out reason), Is.False);
            Assert.That(reason, Is.EqualTo(
                RagdollPuppetCollisionRejectionReason.BudgetExceeded));

            RagdollPuppetCollisionStepSnapshot snapshot = processor.Snapshot;
            Assert.That(snapshot.ReportedCount, Is.EqualTo(3));
            Assert.That(snapshot.AcceptedCount, Is.EqualTo(1));
            Assert.That(snapshot.RejectedLayerCount, Is.EqualTo(1));
            Assert.That(snapshot.RejectedBudgetCount, Is.EqualTo(1));
        }

        [Test]
        public void NewFixedTimestampResetsStepBudgetAndCounters()
        {
            RagdollPuppetCollisionProcessor processor =
                new RagdollPuppetCollisionProcessor();
            RagdollPuppetCollisionRejectionReason reason;

            Assert.That(processor.TryAccept(
                6f,
                RagdollCollisionPhase.Enter,
                0,
                1f,
                -1,
                0f,
                1,
                out reason), Is.True);
            Assert.That(processor.TryAccept(
                6f,
                RagdollCollisionPhase.Stay,
                0,
                1f,
                -1,
                0f,
                1,
                out reason), Is.False);

            Assert.That(processor.TryAccept(
                6.02f,
                RagdollCollisionPhase.Enter,
                0,
                1f,
                -1,
                0f,
                1,
                out reason), Is.True);

            RagdollPuppetCollisionStepSnapshot snapshot = processor.Snapshot;
            Assert.That(snapshot.FixedTime, Is.EqualTo(6.02f));
            Assert.That(snapshot.ReportedCount, Is.EqualTo(1));
            Assert.That(snapshot.AcceptedCount, Is.EqualTo(1));
            Assert.That(snapshot.RejectedCount, Is.Zero);
        }

        [Test]
        public void InvalidImpulseIsRejectedConservatively()
        {
            RagdollPuppetCollisionProcessor processor =
                new RagdollPuppetCollisionProcessor();
            RagdollPuppetCollisionRejectionReason reason;

            bool accepted = processor.TryAccept(
                7f,
                RagdollCollisionPhase.Enter,
                0,
                float.NaN,
                -1,
                0f,
                3,
                out reason);

            Assert.That(accepted, Is.False);
            Assert.That(reason, Is.EqualTo(
                RagdollPuppetCollisionRejectionReason.InvalidImpulse));
        }
    }
}
