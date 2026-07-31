using NUnit.Framework;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollPhysicsQualityReconciliationTests
    {
        [Test]
        public void RequiresReapply_AfterLifecycleReleased_WhenModeWasForcedActive()
        {
            Assert.That(
                RagdollPhysicsQualityReconciliation.RequiresReapply(
                    false,
                    RagdollSimulationMode.Active,
                    RagdollSimulationMode.Kinematic,
                    true,
                    false),
                Is.True);
        }

        [Test]
        public void RequiresReapply_DoesNotFightLifecycleOwnership()
        {
            Assert.That(
                RagdollPhysicsQualityReconciliation.RequiresReapply(
                    true,
                    RagdollSimulationMode.Active,
                    RagdollSimulationMode.Disabled,
                    false,
                    false),
                Is.False);
        }

        [TestCase(true, true)]
        [TestCase(false, false)]
        public void RequiresReapply_RepairsSolverOverrideOwnership(
            bool hasOverride,
            bool useAuthored)
        {
            Assert.That(
                RagdollPhysicsQualityReconciliation.RequiresReapply(
                    false,
                    RagdollSimulationMode.Active,
                    RagdollSimulationMode.Active,
                    hasOverride,
                    useAuthored),
                Is.True);
        }
    }
}
