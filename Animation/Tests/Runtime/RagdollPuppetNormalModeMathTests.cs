using NUnit.Framework;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollPuppetNormalModeMathTests
    {
        [Test]
        public void ActiveModeAlwaysRequestsFullMappingInPuppet()
        {
            Assert.That(
                RagdollPuppetNormalModeMath.ResolveMappingTarget(
                    RagdollPuppetNormalMode.Active,
                    RagdollPuppetState.Puppet,
                    false),
                Is.EqualTo(1f));
        }

        [Test]
        public void UnmappedModeRequiresRecentContactInPuppet()
        {
            Assert.That(
                RagdollPuppetNormalModeMath.ResolveMappingTarget(
                    RagdollPuppetNormalMode.Unmapped,
                    RagdollPuppetState.Puppet,
                    false),
                Is.EqualTo(0f));
            Assert.That(
                RagdollPuppetNormalModeMath.ResolveMappingTarget(
                    RagdollPuppetNormalMode.Unmapped,
                    RagdollPuppetState.Puppet,
                    true),
                Is.EqualTo(1f));
        }

        [Test]
        public void KinematicModeKeepsFullMappingInPuppet()
        {
            Assert.That(
                RagdollPuppetNormalModeMath.ResolveMappingTarget(
                    RagdollPuppetNormalMode.Kinematic,
                    RagdollPuppetState.Puppet,
                    false),
                Is.EqualTo(1f));
        }

        [TestCase(RagdollPuppetState.Unpinned)]
        [TestCase(RagdollPuppetState.GetUp)]
        public void NonPuppetStatesIgnoreNormalMode(
            RagdollPuppetState state)
        {
            Assert.That(
                RagdollPuppetNormalModeMath.ResolveMappingTarget(
                    RagdollPuppetNormalMode.Unmapped,
                    state,
                    false),
                Is.EqualTo(1f));
        }

        [Test]
        public void StepMappingWeightUsesUnitsPerSecondWithoutOvershoot()
        {
            Assert.That(
                RagdollPuppetNormalModeMath.StepMappingWeight(
                    0.2f,
                    1f,
                    2f,
                    0.25f),
                Is.EqualTo(0.7f).Within(0.00001f));
            Assert.That(
                RagdollPuppetNormalModeMath.StepMappingWeight(
                    0.9f,
                    1f,
                    2f,
                    0.25f),
                Is.EqualTo(1f));
        }

        [Test]
        public void ZeroSpeedPausesMappingBlend()
        {
            Assert.That(
                RagdollPuppetNormalModeMath.StepMappingWeight(
                    0.4f,
                    1f,
                    0f,
                    1f),
                Is.EqualTo(0.4f));
        }

        [Test]
        public void InvalidBlendInputsFailClosed()
        {
            Assert.That(
                RagdollPuppetNormalModeMath.StepMappingWeight(
                    float.NaN,
                    float.PositiveInfinity,
                    float.NaN,
                    -1f),
                Is.EqualTo(0f));
        }
    }
}
