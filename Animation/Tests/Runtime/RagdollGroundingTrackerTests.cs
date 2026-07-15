using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class RagdollGroundingTrackerTests
    {
        [Test]
        public void StableTime_AccumulatesOnlyWhileGrounded()
        {
            RagdollGroundingTracker tracker = new RagdollGroundingTracker();

            tracker.Update(
                true,
                Vector3.zero,
                Vector3.up,
                Vector3.zero,
                Vector3.zero,
                10f,
                0.1f);
            tracker.Update(
                true,
                Vector3.zero,
                Vector3.up,
                Vector3.zero,
                Vector3.zero,
                10f,
                0.2f);

            Assert.That(tracker.Snapshot.IsGrounded, Is.True);
            Assert.That(tracker.Snapshot.StableTime, Is.EqualTo(0.3f).Within(0.0001f));

            tracker.Update(
                false,
                Vector3.zero,
                Vector3.up,
                Vector3.zero,
                Vector3.zero,
                10f,
                0.1f);

            Assert.That(tracker.Snapshot.IsGrounded, Is.False);
            Assert.That(tracker.Snapshot.StableTime, Is.EqualTo(0f));
        }

        [Test]
        public void Snapshot_NormalizesGroundNormal()
        {
            RagdollGroundingTracker tracker = new RagdollGroundingTracker();
            tracker.Update(
                true,
                Vector3.zero,
                new Vector3(0f, 4f, 0f),
                Vector3.zero,
                Vector3.zero,
                1f,
                0.1f);

            Assert.That(tracker.Snapshot.GroundNormal, Is.EqualTo(Vector3.up));
        }
    }
}
