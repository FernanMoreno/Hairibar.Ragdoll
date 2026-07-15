using NUnit.Framework;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollMuscleRecoveryMathTests
    {
        [Test]
        public void PositionRecoveryRateComposesBaseGlobalAndGroup()
        {
            Assert.That(
                RagdollMuscleRecoveryMath.ResolvePositionRecoveryRate(
                    2f,
                    3f,
                    0.5f),
                Is.EqualTo(3f).Within(0.00001f));
        }

        [Test]
        public void ZeroMultiplierPausesPositionRecovery()
        {
            Assert.That(
                RagdollMuscleRecoveryMath.ResolvePositionRecoveryRate(
                    2f,
                    0f,
                    4f),
                Is.EqualTo(0f));
        }

        [Test]
        public void InvalidMultipliersUseNeutralFallback()
        {
            Assert.That(
                RagdollMuscleRecoveryMath.ResolvePositionRecoveryRate(
                    2f,
                    float.NaN,
                    float.PositiveInfinity),
                Is.EqualTo(2f).Within(0.00001f));
        }

        [Test]
        public void InvalidBaseRateDisablesRecoveryConservatively()
        {
            Assert.That(
                RagdollMuscleRecoveryMath.ResolvePositionRecoveryRate(
                    float.NaN,
                    2f,
                    2f),
                Is.EqualTo(0f));
        }

        [Test]
        public void RecoveryRateSaturatesWithoutInfinity()
        {
            float result =
                RagdollMuscleRecoveryMath.ResolvePositionRecoveryRate(
                    float.MaxValue,
                    float.MaxValue,
                    float.MaxValue);

            Assert.That(result, Is.EqualTo(float.MaxValue));
            Assert.That(float.IsInfinity(result), Is.False);
        }

        [Test]
        public void EffectiveAuthorityRespectsSemanticMinimum()
        {
            Assert.That(
                RagdollMuscleRecoveryMath.ResolveEffectivePositionAuthority(
                    1f,
                    0.9f,
                    0.35f),
                Is.EqualTo(0.35f).Within(0.00001f));
        }

        [Test]
        public void PersistentAuthorityRemainsAuthoritative()
        {
            Assert.That(
                RagdollMuscleRecoveryMath.ResolveEffectivePositionAuthority(
                    0.5f,
                    0.25f,
                    0f),
                Is.EqualTo(0.375f).Within(0.00001f));
            Assert.That(
                RagdollMuscleRecoveryMath.ResolveEffectivePositionAuthority(
                    0f,
                    0f,
                    1f),
                Is.EqualTo(0f));
        }

        [Test]
        public void ZeroRelativeWeightPreservesAuthoredMuscleStrength()
        {
            Assert.That(
                RagdollMuscleRecoveryMath.ResolveRelativeMuscleWeight(
                    0.2f,
                    0f),
                Is.EqualTo(1f));
        }

        [Test]
        public void FullRelativeWeightFollowsEffectivePinAuthority()
        {
            Assert.That(
                RagdollMuscleRecoveryMath.ResolveRelativeMuscleWeight(
                    0.2f,
                    1f),
                Is.EqualTo(0.2f).Within(0.00001f));
        }

        [Test]
        public void PartialRelativeWeightBlendsFromAuthoredToPinAuthority()
        {
            Assert.That(
                RagdollMuscleRecoveryMath.ResolveRelativeMuscleWeight(
                    0.4f,
                    0.5f),
                Is.EqualTo(0.7f).Within(0.00001f));
        }

        [Test]
        public void InvalidRelativeInputsPreserveNeutralMuscleStrength()
        {
            Assert.That(
                RagdollMuscleRecoveryMath.ResolveRelativeMuscleWeight(
                    float.NaN,
                    float.NaN),
                Is.EqualTo(1f));
        }
    }
}
