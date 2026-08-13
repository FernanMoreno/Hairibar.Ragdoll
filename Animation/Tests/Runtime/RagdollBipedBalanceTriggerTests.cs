using NUnit.Framework;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class RagdollBipedBalanceTriggerTests
    {
        const float MinimumDuration = 0.1f;

        [Test]
        public void SingleFrameRequiresStep_DoesNotFire()
        {
            var trigger = new RagdollBipedBalanceTrigger();
            bool fired = trigger.Evaluate(
                RagdollBipedBalanceState.RequiresStep, MinimumDuration * 0.5f, MinimumDuration);
            Assert.That(fired, Is.False);
        }

        [Test]
        public void SustainedRequiresStep_FiresExactlyOnceWhenThresholdCrossed()
        {
            var trigger = new RagdollBipedBalanceTrigger();
            float dt = MinimumDuration * 0.4f;

            bool first = trigger.Evaluate(RagdollBipedBalanceState.RequiresStep, dt, MinimumDuration);
            bool second = trigger.Evaluate(RagdollBipedBalanceState.RequiresStep, dt, MinimumDuration);
            bool third = trigger.Evaluate(RagdollBipedBalanceState.RequiresStep, dt, MinimumDuration);

            Assert.That(first, Is.False);
            Assert.That(second, Is.False);
            Assert.That(third, Is.True, "Accumulated time (1.2x minimum) must cross the threshold on this call.");
        }

        [Test]
        public void StillRequiresStepAfterFiring_DoesNotFireAgain()
        {
            var trigger = new RagdollBipedBalanceTrigger();
            float dt = MinimumDuration;
            trigger.Evaluate(RagdollBipedBalanceState.RequiresStep, dt, MinimumDuration);

            bool firedAgain = trigger.Evaluate(RagdollBipedBalanceState.RequiresStep, dt, MinimumDuration);

            Assert.That(firedAgain, Is.False);
        }

        [Test]
        public void StableFrame_ResetsAccumulatedDuration()
        {
            var trigger = new RagdollBipedBalanceTrigger();
            trigger.Evaluate(RagdollBipedBalanceState.RequiresStep, MinimumDuration * 0.9f, MinimumDuration);

            trigger.Evaluate(RagdollBipedBalanceState.Stable, MinimumDuration, MinimumDuration);
            bool fired = trigger.Evaluate(RagdollBipedBalanceState.RequiresStep, MinimumDuration * 0.9f, MinimumDuration);

            Assert.That(fired, Is.False, "The Stable frame must have reset the accumulated duration.");
        }

        [Test]
        public void RecoverableWithoutStepOrUnrecoverable_NeverFireDirectly()
        {
            var trigger = new RagdollBipedBalanceTrigger();
            Assert.That(trigger.Evaluate(
                RagdollBipedBalanceState.RecoverableWithoutStep, 10f, MinimumDuration), Is.False);
            Assert.That(trigger.Evaluate(
                RagdollBipedBalanceState.Unrecoverable, 10f, MinimumDuration), Is.False);
        }

        [Test]
        public void Reset_ClearsMidCountState()
        {
            var trigger = new RagdollBipedBalanceTrigger();
            trigger.Evaluate(RagdollBipedBalanceState.RequiresStep, MinimumDuration * 0.9f, MinimumDuration);

            trigger.Reset();
            bool fired = trigger.Evaluate(RagdollBipedBalanceState.RequiresStep, MinimumDuration * 0.9f, MinimumDuration);

            Assert.That(fired, Is.False, "Reset must clear the accumulated duration.");
        }
    }
}
