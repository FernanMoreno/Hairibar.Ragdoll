using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollFeature011SupportContextStressTests
    {
        [Test]
        public void MovingPlatformContinuityMatrix_AccumulatesTwentySamplesAndResetsOnIdentityChange()
        {
            RagdollGroundingTracker tracker = new RagdollGroundingTracker();
            Vector3 supportVelocity = new Vector3(1f, 0f, 0f);
            Vector3 centerOfMassVelocity = supportVelocity + new Vector3(0.25f, 0f, 0f);
            const float deltaTime = 0.02f;

            for (int i = 0; i < 20; i++)
            {
                tracker.Update(
                    true,
                    new Vector3(i * deltaTime, 0f, 0f),
                    Vector3.up,
                    Vector3.zero,
                    centerOfMassVelocity,
                    3f,
                    deltaTime,
                    up: Vector3.up,
                    effectiveUpAvailable: true,
                    supportColliderId: 11,
                    supportRigidbodyId: 12,
                    hasSupportPlatform: true,
                    supportVelocity: supportVelocity);

                RagdollGroundingSnapshot snapshot = tracker.Snapshot;
                Assert.That(snapshot.IsGrounded, Is.True, $"platform sample {i}");
                Assert.That(snapshot.EffectiveUpAvailable, Is.True, $"up sample {i}");
                Assert.That(IsFinite(snapshot.StableTime), Is.True, $"time sample {i}");
                Assert.That(IsFinite(snapshot.RelativeCenterOfMassVelocity.x), Is.True, $"velocity sample {i}");

                if (i == 0)
                {
                    Assert.That(snapshot.HasRelativeMotion, Is.False);
                }
                else
                {
                    Assert.That(snapshot.HasRelativeMotion, Is.True, $"relative motion sample {i}");
                    Assert.That(snapshot.RelativeCenterOfMassVelocity,
                        Is.EqualTo(new Vector3(0.25f, 0f, 0f)));
                    Assert.That(snapshot.SupportContinuityReset, Is.False, $"continuity sample {i}");
                }
            }

            Assert.That(tracker.Snapshot.StableTime, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(tracker.Snapshot.SupportContinuityReset, Is.False);

            tracker.Update(
                true,
                new Vector3(20f * deltaTime, 0f, 0f),
                Vector3.up,
                Vector3.zero,
                centerOfMassVelocity,
                3f,
                deltaTime,
                up: Vector3.up,
                effectiveUpAvailable: true,
                supportColliderId: 99,
                supportRigidbodyId: 100,
                hasSupportPlatform: true,
                supportVelocity: supportVelocity);

            Assert.That(tracker.Snapshot.SupportContinuityReset, Is.True);
            Assert.That(tracker.Snapshot.HasRelativeMotion, Is.False);
            Assert.That(tracker.Snapshot.StableTime, Is.Zero);
            Assert.That(IsFinite(tracker.Snapshot.StableTime), Is.True);
            TestContext.WriteLine(
                "Support context PlatformContinuity: cells=20, relativeMotion=true, continuityReset=true, finite=true");
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
