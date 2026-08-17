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

        [Test]
        public void Snapshot_UsesEffectiveUpAndRelativePlatformVelocityAfterContinuity()
        {
            RagdollGroundingTracker tracker = new RagdollGroundingTracker();
            Vector3 supportVelocity = new Vector3(1f, 2f, 3f);

            tracker.Update(
                true,
                Vector3.zero,
                Vector3.right,
                Vector3.zero,
                supportVelocity,
                10f,
                0.1f,
                up: Vector3.right,
                effectiveUpAvailable: true,
                supportColliderId: 11,
                supportRigidbodyId: 12,
                hasSupportPlatform: true,
                supportVelocity: supportVelocity);
            Assert.That(tracker.Snapshot.EffectiveUp, Is.EqualTo(Vector3.right));
            Assert.That(tracker.Snapshot.EffectiveUpAvailable, Is.True);
            Assert.That(tracker.Snapshot.HasRelativeMotion, Is.False);

            tracker.Update(
                true,
                Vector3.zero,
                Vector3.right,
                Vector3.zero,
                new Vector3(2f, 2f, 4f),
                10f,
                0.1f,
                up: Vector3.right,
                effectiveUpAvailable: true,
                supportColliderId: 11,
                supportRigidbodyId: 12,
                hasSupportPlatform: true,
                supportVelocity: supportVelocity);

            Assert.That(tracker.Snapshot.HasRelativeMotion, Is.True);
            Assert.That(tracker.Snapshot.RelativeCenterOfMassVelocity,
                Is.EqualTo(new Vector3(1f, 0f, 1f)));
            Assert.That(tracker.Snapshot.StableTime, Is.EqualTo(0.2f).Within(0.0001f));
        }

        [Test]
        public void Snapshot_IdentityChangeResetsContinuityAndStableTime()
        {
            RagdollGroundingTracker tracker = new RagdollGroundingTracker();
            tracker.Update(
                true, Vector3.zero, Vector3.up, Vector3.zero, Vector3.zero, 1f, 0.2f,
                up: Vector3.up, effectiveUpAvailable: true,
                supportColliderId: 1, supportRigidbodyId: 2, hasSupportPlatform: true);
            tracker.Update(
                true, Vector3.zero, Vector3.up, Vector3.zero, Vector3.zero, 1f, 0.2f,
                up: Vector3.up, effectiveUpAvailable: true,
                supportColliderId: 3, supportRigidbodyId: 4, hasSupportPlatform: true);

            Assert.That(tracker.Snapshot.SupportContinuityReset, Is.True);
            Assert.That(tracker.Snapshot.StableTime, Is.Zero);
            Assert.That(tracker.Snapshot.HasRelativeMotion, Is.False);
        }

        [Test]
        public void Snapshot_InvalidEffectiveUpIsMarkedUnavailableAndFinite()
        {
            RagdollGroundingTracker tracker = new RagdollGroundingTracker();
            tracker.Update(
                true, Vector3.zero, Vector3.up, Vector3.zero, Vector3.zero, 1f, 0.1f,
                up: new Vector3(float.NaN, 0f, 0f), effectiveUpAvailable: false);

            Assert.That(tracker.Snapshot.EffectiveUpAvailable, Is.False);
            Assert.That(tracker.Snapshot.EffectiveUp, Is.EqualTo(Vector3.up));
            Assert.That(float.IsNaN(tracker.Snapshot.RelativeCenterOfMassVelocity.x), Is.False);
        }
    }
}
