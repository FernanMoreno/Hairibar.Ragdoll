using NUnit.Framework;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollPuppetKinematicActivationPolicyTests
    {
        [Test]
        public void SourceClassificationSeparatesStaticKinematicAndDynamic()
        {
            Assert.That(
                RagdollPuppetKinematicActivationPolicy.ResolveSource(false, false),
                Is.EqualTo(RagdollPuppetKinematicActivationSource.StaticCollider));
            Assert.That(
                RagdollPuppetKinematicActivationPolicy.ResolveSource(true, true),
                Is.EqualTo(RagdollPuppetKinematicActivationSource.KinematicRigidbody));
            Assert.That(
                RagdollPuppetKinematicActivationPolicy.ResolveSource(true, false),
                Is.EqualTo(RagdollPuppetKinematicActivationSource.DynamicRigidbody));
        }

        [Test]
        public void StaticAndKinematicSourcesUseStaticActivationFlag()
        {
            Assert.That(
                RagdollPuppetKinematicActivationPolicy.ShouldQueueActivation(
                    RagdollPuppetNormalMode.Kinematic,
                    RagdollPuppetState.Puppet,
                    RagdollPuppetKinematicActivationSource.StaticCollider,
                    2f,
                    1f,
                    false,
                    true),
                Is.False);
            Assert.That(
                RagdollPuppetKinematicActivationPolicy.ShouldQueueActivation(
                    RagdollPuppetNormalMode.Kinematic,
                    RagdollPuppetState.Puppet,
                    RagdollPuppetKinematicActivationSource.KinematicRigidbody,
                    2f,
                    1f,
                    true,
                    false),
                Is.True);
        }

        [Test]
        public void DynamicSourceUsesDynamicActivationFlag()
        {
            Assert.That(
                RagdollPuppetKinematicActivationPolicy.ShouldQueueActivation(
                    RagdollPuppetNormalMode.Kinematic,
                    RagdollPuppetState.Puppet,
                    RagdollPuppetKinematicActivationSource.DynamicRigidbody,
                    2f,
                    1f,
                    true,
                    false),
                Is.False);
            Assert.That(
                RagdollPuppetKinematicActivationPolicy.ShouldQueueActivation(
                    RagdollPuppetNormalMode.Kinematic,
                    RagdollPuppetState.Puppet,
                    RagdollPuppetKinematicActivationSource.DynamicRigidbody,
                    2f,
                    1f,
                    false,
                    true),
                Is.True);
        }

        [Test]
        public void MinimumImpulseIsInclusive()
        {
            Assert.That(
                RagdollPuppetKinematicActivationPolicy.ShouldQueueActivation(
                    RagdollPuppetNormalMode.Kinematic,
                    RagdollPuppetState.Puppet,
                    RagdollPuppetKinematicActivationSource.DynamicRigidbody,
                    1f,
                    1f,
                    false,
                    true),
                Is.True);
            Assert.That(
                RagdollPuppetKinematicActivationPolicy.ShouldQueueActivation(
                    RagdollPuppetNormalMode.Kinematic,
                    RagdollPuppetState.Puppet,
                    RagdollPuppetKinematicActivationSource.DynamicRigidbody,
                    0.99f,
                    1f,
                    false,
                    true),
                Is.False);
        }


        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-1f)]
        public void InvalidImpulseFailsClosed(float impulse)
        {
            Assert.That(
                RagdollPuppetKinematicActivationPolicy.ShouldQueueActivation(
                    RagdollPuppetNormalMode.Kinematic,
                    RagdollPuppetState.Puppet,
                    RagdollPuppetKinematicActivationSource.DynamicRigidbody,
                    impulse,
                    0f,
                    false,
                    true),
                Is.False);
        }

        [TestCase(RagdollPuppetNormalMode.Active, RagdollPuppetState.Puppet)]
        [TestCase(RagdollPuppetNormalMode.Unmapped, RagdollPuppetState.Puppet)]
        [TestCase(RagdollPuppetNormalMode.Kinematic, RagdollPuppetState.Unpinned)]
        [TestCase(RagdollPuppetNormalMode.Kinematic, RagdollPuppetState.GetUp)]
        public void OnlyKinematicPuppetStateCanQueue(
            RagdollPuppetNormalMode mode,
            RagdollPuppetState state)
        {
            Assert.That(
                RagdollPuppetKinematicActivationPolicy.ShouldQueueActivation(
                    mode,
                    state,
                    RagdollPuppetKinematicActivationSource.DynamicRigidbody,
                    10f,
                    0f,
                    true,
                    true),
                Is.False);
        }

        [Test]
        public void ReturnRequiresStableRecoveredContactFreePuppet()
        {
            Assert.That(
                RagdollPuppetKinematicActivationPolicy.ShouldReturnToKinematic(
                    RagdollPuppetNormalMode.Kinematic,
                    RagdollPuppetState.Puppet,
                    RagdollSimulationMode.Active,
                    false,
                    false,
                    false,
                    true),
                Is.True);

            Assert.That(
                RagdollPuppetKinematicActivationPolicy.ShouldReturnToKinematic(
                    RagdollPuppetNormalMode.Kinematic,
                    RagdollPuppetState.Puppet,
                    RagdollSimulationMode.Active,
                    false,
                    true,
                    false,
                    true),
                Is.False);

            Assert.That(
                RagdollPuppetKinematicActivationPolicy.ShouldReturnToKinematic(
                    RagdollPuppetNormalMode.Kinematic,
                    RagdollPuppetState.Puppet,
                    RagdollSimulationMode.Active,
                    false,
                    false,
                    false,
                    false),
                Is.False);

            Assert.That(
                RagdollPuppetKinematicActivationPolicy.ShouldReturnToKinematic(
                    RagdollPuppetNormalMode.Kinematic,
                    RagdollPuppetState.Puppet,
                    RagdollSimulationMode.Active,
                    true,
                    false,
                    false,
                    true),
                Is.False);

            Assert.That(
                RagdollPuppetKinematicActivationPolicy.ShouldReturnToKinematic(
                    RagdollPuppetNormalMode.Kinematic,
                    RagdollPuppetState.Puppet,
                    RagdollSimulationMode.Active,
                    false,
                    false,
                    true,
                    true),
                Is.False);

            Assert.That(
                RagdollPuppetKinematicActivationPolicy.ShouldReturnToKinematic(
                    RagdollPuppetNormalMode.Kinematic,
                    RagdollPuppetState.Unpinned,
                    RagdollSimulationMode.Active,
                    false,
                    false,
                    false,
                    true),
                Is.False);
        }
    }
}
