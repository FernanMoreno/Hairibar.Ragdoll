using NUnit.Framework;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class RagdollPuppetBehaviourMathTests
    {
        [Test]
        public void PuppetWeights_AreNeutral()
        {
            RagdollPuppetStateWeights weights =
                RagdollPuppetStateWeights.Evaluate(
                    RagdollPuppetState.Puppet,
                    1f,
                    0.3f);

            Assert.That(weights.PositionAuthority, Is.EqualTo(1f));
            Assert.That(weights.RotationAuthority, Is.EqualTo(1f));
            Assert.That(weights.MaximumMappingBlend, Is.EqualTo(0f));
        }

        [Test]
        public void UnpinnedWeights_DisablePinAndKeepReducedMuscle()
        {
            RagdollPuppetStateWeights weights =
                RagdollPuppetStateWeights.Evaluate(
                    RagdollPuppetState.Unpinned,
                    0f,
                    0.3f);

            Assert.That(weights.PositionAuthority, Is.EqualTo(0f));
            Assert.That(weights.RotationAuthority, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(weights.MaximumMappingBlend, Is.EqualTo(1f));
        }

        [Test]
        public void GetUpWeights_BlendBackToPuppet()
        {
            RagdollPuppetStateWeights weights =
                RagdollPuppetStateWeights.Evaluate(
                    RagdollPuppetState.GetUp,
                    0.5f,
                    0.2f);

            Assert.That(weights.PositionAuthority, Is.EqualTo(0.5f));
            Assert.That(weights.RotationAuthority, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(weights.MaximumMappingBlend, Is.EqualTo(0.5f));
        }

        [Test]
        public void KnockOutRequiresDistanceAndPinThreshold()
        {
            Assert.That(
                RagdollPuppetBehaviourMath.ShouldLoseBalance(
                    1.1f, 1f, 0.5f, 0.6f, 1f),
                Is.True);

            Assert.That(
                RagdollPuppetBehaviourMath.ShouldLoseBalance(
                    0.9f, 1f, 0.5f, 0.6f, 1f),
                Is.False);

            Assert.That(
                RagdollPuppetBehaviourMath.ShouldLoseBalance(
                    1.1f, 1f, 0.8f, 0.6f, 1f),
                Is.False);
        }

        [Test]
        public void GetUpReadiness_RequiresDelayAndLowVelocity()
        {
            Assert.That(
                RagdollPuppetBehaviourMath.IsGetUpReady(
                    1f, 1f, 0.4f, 0.5f),
                Is.True);
            Assert.That(
                RagdollPuppetBehaviourMath.IsGetUpReady(
                    0.9f, 1f, 0.4f, 0.5f),
                Is.False);
            Assert.That(
                RagdollPuppetBehaviourMath.IsGetUpReady(
                    1f, 1f, 0.6f, 0.5f),
                Is.False);
        }
    }
}
