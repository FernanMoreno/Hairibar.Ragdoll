using NUnit.Framework;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollPuppetUnmappedContactTrackerTests
    {
        [Test]
        public void ContactRemainsRecentAcrossOnePhysicsStep()
        {
            RagdollPuppetUnmappedContactTracker tracker =
                new RagdollPuppetUnmappedContactTracker();
            tracker.Register(2f);

            Assert.That(tracker.IsRecent(2f, 0.02f), Is.True);
            Assert.That(tracker.IsRecent(2.02f, 0.02f), Is.True);
            Assert.That(tracker.IsRecent(2.041f, 0.02f), Is.False);
        }

        [Test]
        public void StayCallbacksRefreshRecentContact()
        {
            RagdollPuppetUnmappedContactTracker tracker =
                new RagdollPuppetUnmappedContactTracker();
            tracker.Register(1f);
            tracker.Register(1.02f);

            Assert.That(tracker.IsRecent(1.04f, 0.02f), Is.True);
        }

        [Test]
        public void ResetAndInvalidTimestampsDoNotActivateContact()
        {
            RagdollPuppetUnmappedContactTracker tracker =
                new RagdollPuppetUnmappedContactTracker();
            tracker.Register(float.NaN);
            Assert.That(tracker.IsRecent(0f, 0.02f), Is.False);

            tracker.Register(0f);
            tracker.Reset();
            Assert.That(tracker.IsRecent(0f, 0.02f), Is.False);
        }
    }
}
