using NUnit.Framework;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class RagdollCollisionEventBudgetTests
    {
        [Test]
        public void Budget_LimitsEventsWithinTheSamePhysicsTimestamp()
        {
            RagdollCollisionEventBudget budget = new RagdollCollisionEventBudget();

            Assert.That(budget.TryConsume(1f, 2), Is.True);
            Assert.That(budget.TryConsume(1f, 2), Is.True);
            Assert.That(budget.TryConsume(1f, 2), Is.False);
            Assert.That(budget.Consumed, Is.EqualTo(2));
        }

        [Test]
        public void Budget_ResetsWhenFixedTimeChanges()
        {
            RagdollCollisionEventBudget budget = new RagdollCollisionEventBudget();

            Assert.That(budget.TryConsume(1f, 1), Is.True);
            Assert.That(budget.TryConsume(1f, 1), Is.False);
            Assert.That(budget.TryConsume(1.02f, 1), Is.True);
        }

        [Test]
        public void ZeroMaximum_DisablesTheLimit()
        {
            RagdollCollisionEventBudget budget = new RagdollCollisionEventBudget();

            for (int i = 0; i < 100; i++)
            {
                Assert.That(budget.TryConsume(1f, 0), Is.True);
            }
        }
    }
}
