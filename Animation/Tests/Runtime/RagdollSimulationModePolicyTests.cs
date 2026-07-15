using NUnit.Framework;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollSimulationModePolicyTests
    {
        [Test]
        public void ActiveKeepsHierarchyAndAuthoredPower()
        {
            Assert.That(
                RagdollSimulationModePolicy.KeepsPuppetHierarchyActive(
                    RagdollSimulationMode.Active),
                Is.True);
            Assert.That(
                RagdollSimulationModePolicy.OverridesAllBonesToKinematic(
                    RagdollSimulationMode.Active),
                Is.False);
            Assert.That(
                RagdollSimulationModePolicy.StableDriveWeight(
                    RagdollSimulationMode.Active),
                Is.EqualTo(1f));
        }

        [Test]
        public void KinematicKeepsHierarchyAndCollisionConfiguration()
        {
            Assert.That(
                RagdollSimulationModePolicy.KeepsPuppetHierarchyActive(
                    RagdollSimulationMode.Kinematic),
                Is.True);
            Assert.That(
                RagdollSimulationModePolicy.OverridesAllBonesToKinematic(
                    RagdollSimulationMode.Kinematic),
                Is.True);
            Assert.That(
                RagdollSimulationModePolicy.KeepsCollisionConfiguration(
                    RagdollSimulationMode.Kinematic),
                Is.True);
            Assert.That(
                RagdollSimulationModePolicy.StableDriveWeight(
                    RagdollSimulationMode.Kinematic),
                Is.EqualTo(0f));
        }

        [Test]
        public void DisabledDeactivatesHierarchyAndHasNoDriveWeight()
        {
            Assert.That(
                RagdollSimulationModePolicy.KeepsPuppetHierarchyActive(
                    RagdollSimulationMode.Disabled),
                Is.False);
            Assert.That(
                RagdollSimulationModePolicy.OverridesAllBonesToKinematic(
                    RagdollSimulationMode.Disabled),
                Is.True);
            Assert.That(
                RagdollSimulationModePolicy.KeepsCollisionConfiguration(
                    RagdollSimulationMode.Disabled),
                Is.False);
            Assert.That(
                RagdollSimulationModePolicy.StableDriveWeight(
                    RagdollSimulationMode.Disabled),
                Is.EqualTo(0f));
        }
    }
}
