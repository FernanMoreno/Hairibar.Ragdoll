using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollSupportPlaneRobustnessTests
    {
        static readonly RagdollBoneHandle LeftFoot = new RagdollBoneHandle(1, 1, 1);
        static readonly RagdollBoneHandle RightFoot = new RagdollBoneHandle(1, 1, 2);
        static readonly RagdollBoneHandle Hand = new RagdollBoneHandle(1, 1, 3);

        [Test]
        public void FootSupportFilter_RejectsMissingContactNonFootNonGroundAndRagdollCollider()
        {
            Assert.That(IsValidFootSample(
                LeftFoot, hasContact: false, isGroundLayer: true,
                isRagdollCollider: false, normal: Vector3.up), Is.False);
            Assert.That(IsValidFootSample(
                Hand, hasContact: true, isGroundLayer: true,
                isRagdollCollider: false, normal: Vector3.up), Is.False);
            Assert.That(IsValidFootSample(
                LeftFoot, hasContact: true, isGroundLayer: false,
                isRagdollCollider: false, normal: Vector3.up), Is.False);
            Assert.That(IsValidFootSample(
                LeftFoot, hasContact: true, isGroundLayer: true,
                isRagdollCollider: true, normal: Vector3.up), Is.False);
        }

        [Test]
        public void FootSupportFilter_RejectsInvalidNormalAndAcceptsFiniteGroundContact()
        {
            Assert.That(IsValidFootSample(
                LeftFoot, hasContact: true, isGroundLayer: true,
                isRagdollCollider: false, normal: Vector3.zero), Is.False);
            Assert.That(IsValidFootSample(
                LeftFoot, hasContact: true, isGroundLayer: true,
                isRagdollCollider: false, normal: Vector3.up), Is.True);
        }

        [Test]
        public void SupportMask_OnlyAcceptsFiniteDeclaredFootPoints()
        {
            RagdollGroundingTracker tracker = new RagdollGroundingTracker();
            tracker.Update(
                grounded: false,
                point: Vector3.zero,
                normal: Vector3.up,
                centerOfMass: Vector3.zero,
                centerOfMassVelocity: Vector3.zero,
                totalMass: 1f,
                deltaTime: 0.02f,
                hasLeftFootSupport: true,
                leftFootSupportPoint: new Vector3(0.1f, 0f, 0f),
                hasRightFootSupport: true,
                rightFootSupportPoint: new Vector3(float.PositiveInfinity, 0f, 0f));

            Assert.That(tracker.Snapshot.IsGrounded, Is.False);
            Assert.That(tracker.Snapshot.HasLeftFootSupport, Is.True);
            Assert.That(tracker.Snapshot.HasRightFootSupport, Is.False);
            Assert.That(tracker.Snapshot.ContactBackedSupportPointCount, Is.EqualTo(1));
        }

        [Test]
        public void SupportMask_RemainsIndependentFromCentralRayGrounding()
        {
            RagdollGroundingTracker tracker = new RagdollGroundingTracker();
            tracker.Update(
                grounded: false,
                point: Vector3.zero,
                normal: Vector3.up,
                centerOfMass: Vector3.zero,
                centerOfMassVelocity: Vector3.zero,
                totalMass: 1f,
                deltaTime: 0.02f,
                hasLeftFootSupport: true,
                leftFootSupportPoint: new Vector3(-0.2f, 0f, 0f));

            Assert.That(tracker.Snapshot.IsGrounded, Is.False);
            Assert.That(tracker.Snapshot.HasLeftFootSupport, Is.True);
            Assert.That(tracker.Snapshot.LeftFootSupportPoint,
                Is.EqualTo(new Vector3(-0.2f, 0f, 0f)));
        }

        static bool IsValidFootSample(
            RagdollBoneHandle bone,
            bool hasContact,
            bool isGroundLayer,
            bool isRagdollCollider,
            Vector3 normal)
        {
            return RagdollGroundProbe.IsValidFootSupportSample(
                bone,
                LeftFoot,
                RightFoot,
                hasContact,
                isGroundLayer,
                isRagdollCollider,
                Vector3.zero,
                normal,
                Vector3.up,
                0.5f);
        }
    }
}
