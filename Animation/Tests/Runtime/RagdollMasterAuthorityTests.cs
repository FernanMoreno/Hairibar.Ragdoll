using NUnit.Framework;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollMasterAuthorityTests
    {
        [TestCase(0f, 0f, 0f, 0f)]
        [TestCase(0f, 1f, 0f, 8f)]
        [TestCase(1f, 0f, 1f, 0f)]
        [TestCase(1f, 1f, 1f, 8f)]
        public void PinAndMuscleWeightsAreIndependent(
            float pin,
            float muscle,
            float expectedPin,
            float expectedRotationAlpha)
        {
            BoneProfile profile = new BoneProfile
            {
                positionAlpha = 4f,
                positionDampingRatio = 2f,
                rotationAlpha = 8f,
                rotationDampingRatio = 3f
            };

            RagdollMasterAuthority.Apply(
                ref profile,
                pin,
                muscle,
                0.5f,
                2f);

            Assert.That(profile.PositionPinWeight, Is.EqualTo(expectedPin));
            Assert.That(profile.positionAlpha, Is.EqualTo(4f));
            Assert.That(profile.rotationAlpha, Is.EqualTo(expectedRotationAlpha));
            Assert.That(profile.positionDampingRatio, Is.EqualTo(1f));
            Assert.That(profile.rotationDampingRatio, Is.EqualTo(6f));
        }

        [Test]
        public void InvalidMasterValuesUseSafeNeutralFallbacks()
        {
            BoneProfile profile = new BoneProfile
            {
                positionAlpha = 4f,
                positionDampingRatio = 2f,
                rotationAlpha = 8f,
                rotationDampingRatio = 3f
            };

            RagdollMasterAuthority.Apply(
                ref profile,
                float.NaN,
                float.PositiveInfinity,
                float.NaN,
                -2f);

            Assert.That(profile.PositionPinWeight, Is.EqualTo(1f));
            Assert.That(profile.rotationAlpha, Is.EqualTo(8f));
            Assert.That(profile.positionDampingRatio, Is.EqualTo(2f));
            Assert.That(profile.rotationDampingRatio, Is.Zero);
        }
    }
}
