using NUnit.Framework;
using UnityEngine;

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
        public void ConfiguredPinWeight_ComposesAuthoredMasterAndPersistentWeights()
        {
            float configured =
                RagdollPuppetBehaviourMath.ResolveConfiguredPinWeight(
                    0.5f,
                    0.8f,
                    0.25f);

            Assert.That(configured, Is.EqualTo(0.1f).Within(0.0001f));
        }

        [Test]
        public void EffectivePinWeight_ComposesSuppressionMinimumAndPuppetState()
        {
            float effective =
                RagdollPuppetBehaviourMath.ResolveEffectivePinWeight(
                    0.8f,
                    0.75f,
                    0.5f,
                    0.25f);

            Assert.That(effective, Is.EqualTo(0.1f).Within(0.0001f));
        }

        [Test]
        public void ZeroConfiguredPin_IsIgnoredWhenUnpinnedMuscleKnockoutIsDisabled()
        {
            bool shouldLoseBalance =
                RagdollPuppetBehaviourMath.ShouldLoseBalance(
                    2f,
                    1f,
                    0f,
                    0f,
                    1f,
                    1f,
                    false);

            Assert.That(shouldLoseBalance, Is.False);
        }

        [Test]
        public void ZeroConfiguredPin_CanKnockOutWhenOptionIsEnabled()
        {
            bool shouldLoseBalance =
                RagdollPuppetBehaviourMath.ShouldLoseBalance(
                    2f,
                    1f,
                    0f,
                    0f,
                    1f,
                    1f,
                    true);

            Assert.That(shouldLoseBalance, Is.True);
        }

        [Test]
        public void TemporaryFullSuppression_DoesNotBecomeAZeroConfiguredPin()
        {
            bool shouldLoseBalance =
                RagdollPuppetBehaviourMath.ShouldLoseBalance(
                    2f,
                    1f,
                    0f,
                    1f,
                    1f,
                    1f,
                    false);

            Assert.That(shouldLoseBalance, Is.True);
        }

        [Test]
        public void VelocityLimit_ClampsMagnitudeAndPreservesDirection()
        {
            Vector3 original = new Vector3(3f, 4f, 0f);
            Vector3 limited =
                RagdollPuppetBehaviourMath.LimitVelocity(original, 2f);

            Assert.That(limited.magnitude, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(
                Vector3.Dot(original.normalized, limited.normalized),
                Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void VelocityLimit_ZeroStopsLinearVelocity()
        {
            Vector3 limited =
                RagdollPuppetBehaviourMath.LimitVelocity(
                    new Vector3(3f, 4f, 0f),
                    0f);

            Assert.That(limited, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void VelocityLimit_InfinityLeavesVelocityUntouched()
        {
            Vector3 original = new Vector3(3f, 4f, 5f);
            Vector3 limited =
                RagdollPuppetBehaviourMath.LimitVelocity(
                    original,
                    Mathf.Infinity);

            Assert.That(limited, Is.EqualTo(original));
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
