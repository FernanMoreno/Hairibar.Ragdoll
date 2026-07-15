using NUnit.Framework;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollPhysicsBudgetPolicyTests
    {
        [Test]
        public void HigherPriorityWinsBeforeDistance()
        {
            int result = RagdollPhysicsBudgetPolicy.Compare(
                10, 100f, false, 1,
                0, 1f, false, 2,
                0f);

            Assert.That(result, Is.LessThan(0));
        }

        [Test]
        public void CloserCandidateWinsAtEqualPriority()
        {
            int result = RagdollPhysicsBudgetPolicy.Compare(
                0, 4f, false, 1,
                0, 100f, false, 2,
                0f);

            Assert.That(result, Is.LessThan(0));
        }

        [Test]
        public void RetentionBonusPreventsBoundaryThrashing()
        {
            int result = RagdollPhysicsBudgetPolicy.Compare(
                0, 100f, true, 1,
                0, 81f, false, 2,
                2f);

            Assert.That(result, Is.LessThan(0));
        }

        [Test]
        public void StableIdBreaksExactTiesDeterministically()
        {
            int result = RagdollPhysicsBudgetPolicy.Compare(
                0, 25f, false, 10,
                0, 25f, false, 20,
                0f);

            Assert.That(result, Is.LessThan(0));
        }
    }
}
