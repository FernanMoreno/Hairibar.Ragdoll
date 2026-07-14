using NUnit.Framework;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class RagdollImpactResponseTests
    {
        [Test]
        public void Response_IsZeroBelowMinimumImpulse()
        {
            float response = RagdollCollisionReaction.EvaluateImpactResponse(0.49f, 0.5f, 2f);
            Assert.That(response, Is.EqualTo(0f));
        }

        [Test]
        public void Response_IsLinearBetweenThresholds()
        {
            float response = RagdollCollisionReaction.EvaluateImpactResponse(1.25f, 0.5f, 2f);
            Assert.That(response, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void Response_ClampsAtFullSuppression()
        {
            float response = RagdollCollisionReaction.EvaluateImpactResponse(20f, 0.5f, 2f);
            Assert.That(response, Is.EqualTo(1f));
        }

        [Test]
        public void EqualThresholds_BehaveAsStepFunction()
        {
            Assert.That(
                RagdollCollisionReaction.EvaluateImpactResponse(0.99f, 1f, 1f),
                Is.EqualTo(0f));
            Assert.That(
                RagdollCollisionReaction.EvaluateImpactResponse(1f, 1f, 1f),
                Is.EqualTo(1f));
        }
    }
}
