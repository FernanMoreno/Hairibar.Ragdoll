using NUnit.Framework;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollSimulationTransitionTests
    {
        [Test]
        public void ZeroDurationCompletesAtEndValue()
        {
            RagdollSimulationTransition transition =
                new RagdollSimulationTransition(0f, 1f, 0f);

            Assert.That(transition.IsComplete, Is.True);
            Assert.That(transition.Value, Is.EqualTo(1f));
            Assert.That(transition.Progress, Is.EqualTo(1f));
        }

        [Test]
        public void AdvanceInterpolatesLinearly()
        {
            RagdollSimulationTransition transition =
                new RagdollSimulationTransition(0f, 1f, 2f);

            Assert.That(transition.Advance(0.5f), Is.False);
            Assert.That(transition.Value, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(transition.Progress, Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        public void AdvanceClampsAtCompletion()
        {
            RagdollSimulationTransition transition =
                new RagdollSimulationTransition(1f, 0f, 1f);

            Assert.That(transition.Advance(5f), Is.True);
            Assert.That(transition.Value, Is.EqualTo(0f));
            Assert.That(transition.Progress, Is.EqualTo(1f));
        }

        [Test]
        public void NegativeDeltaDoesNotReverseTransition()
        {
            RagdollSimulationTransition transition =
                new RagdollSimulationTransition(0f, 1f, 1f);

            transition.Advance(-1f);

            Assert.That(transition.Value, Is.EqualTo(0f));
            Assert.That(transition.Progress, Is.EqualTo(0f));
        }

        [Test]
        public void RemainingDurationPreservesFullRangeRate()
        {
            float remaining =
                RagdollSimulationTransition.ScaleRemainingDuration(
                    2f,
                    0.25f,
                    1f);

            Assert.That(remaining, Is.EqualTo(1.5f).Within(0.0001f));
        }

        [Test]
        public void RemainingDurationClampsInputs()
        {
            float remaining =
                RagdollSimulationTransition.ScaleRemainingDuration(
                    -2f,
                    -1f,
                    4f);

            Assert.That(remaining, Is.EqualTo(0f));
        }
    }
}
