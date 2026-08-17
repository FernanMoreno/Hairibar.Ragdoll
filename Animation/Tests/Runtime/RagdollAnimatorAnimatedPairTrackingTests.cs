using System.Collections.Generic;
using NUnit.Framework;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollAnimatorAnimatedPairTrackingTests
    {
        [Test]
        public void AnimatorExposesTheAuthoritativeAnimatedPairCollectionReadOnly()
        {
            var property = typeof(RagdollAnimator).GetProperty(nameof(RagdollAnimator.AnimatedPairs));

            Assert.That(property, Is.Not.Null);
            Assert.That(property.PropertyType, Is.EqualTo(typeof(IReadOnlyList<RagdollAnimator.AnimatedPair>)));
            Assert.That(property.CanRead, Is.True);
            Assert.That(property.CanWrite, Is.False);
        }

        [Test]
        public void AnimatedPairExposesTargetDerivativeAndEffectiveMappingEvidence()
        {
            var pairType = typeof(RagdollAnimator.AnimatedPair);

            Assert.That(pairType.GetProperty(nameof(RagdollAnimator.AnimatedPair.TargetLinearVelocity)), Is.Not.Null);
            Assert.That(pairType.GetProperty(nameof(RagdollAnimator.AnimatedPair.TargetLinearAcceleration)), Is.Not.Null);
            Assert.That(pairType.GetProperty(nameof(RagdollAnimator.AnimatedPair.TargetLinearJerk)), Is.Not.Null);
            Assert.That(pairType.GetProperty(nameof(RagdollAnimator.AnimatedPair.TargetKinematicsAvailable)), Is.Not.Null);
            Assert.That(pairType.GetProperty(nameof(RagdollAnimator.AnimatedPair.TargetAccelerationAvailable)), Is.Not.Null);
            Assert.That(pairType.GetProperty(nameof(RagdollAnimator.AnimatedPair.TargetJerkAvailable)), Is.Not.Null);
            Assert.That(pairType.GetProperty(nameof(RagdollAnimator.AnimatedPair.EffectiveMappingWeights)), Is.Not.Null);
            Assert.That(pairType.GetProperty(nameof(RagdollAnimator.AnimatedPair.EffectiveMappingAvailable)), Is.Not.Null);
        }
    }
}
