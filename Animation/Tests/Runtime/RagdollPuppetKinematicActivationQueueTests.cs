using NUnit.Framework;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollPuppetKinematicActivationQueueTests
    {
        [Test]
        public void QueueKeepsStrongestRequestAndConsumesOnce()
        {
            RagdollPuppetKinematicActivationQueue queue =
                new RagdollPuppetKinematicActivationQueue();

            queue.Request(
                RagdollPuppetKinematicActivationSource.StaticCollider,
                2f,
                1f);
            queue.Request(
                RagdollPuppetKinematicActivationSource.DynamicRigidbody,
                1f,
                2f);
            queue.Request(
                RagdollPuppetKinematicActivationSource.DynamicRigidbody,
                3f,
                3f);

            RagdollPuppetKinematicActivationSource source;
            float impulse;
            float fixedTime;
            Assert.That(queue.TryConsume(out source, out impulse, out fixedTime), Is.True);
            Assert.That(source, Is.EqualTo(
                RagdollPuppetKinematicActivationSource.DynamicRigidbody));
            Assert.That(impulse, Is.EqualTo(3f));
            Assert.That(fixedTime, Is.EqualTo(3f));
            Assert.That(queue.HasRequest, Is.False);
            Assert.That(queue.TryConsume(out source, out impulse, out fixedTime), Is.False);
        }

        [Test]
        public void InvalidRequestsAreIgnored()
        {
            RagdollPuppetKinematicActivationQueue queue =
                new RagdollPuppetKinematicActivationQueue();

            queue.Request(
                RagdollPuppetKinematicActivationSource.None,
                1f,
                1f);
            queue.Request(
                RagdollPuppetKinematicActivationSource.StaticCollider,
                float.NaN,
                1f);
            queue.Request(
                RagdollPuppetKinematicActivationSource.StaticCollider,
                1f,
                float.PositiveInfinity);

            Assert.That(queue.HasRequest, Is.False);
        }
    }
}
