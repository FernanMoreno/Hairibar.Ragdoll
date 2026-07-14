using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class RagdollTargetBindingTests
    {
        GameObject ragdollObject;
        GameObject targetObject;

        [TearDown]
        public void TearDown()
        {
            if (targetObject) Object.DestroyImmediate(targetObject);
            if (ragdollObject) Object.DestroyImmediate(ragdollObject);
        }

        [Test]
        public void CapturedBinding_RoundTripsPositionRotationAndScale()
        {
            ragdollObject = new GameObject("PuppetBone");
            targetObject = new GameObject("TargetBone");

            Transform ragdoll = ragdollObject.transform;
            Transform target = targetObject.transform;

            ragdoll.SetPositionAndRotation(
                new Vector3(1f, 2f, 3f),
                Quaternion.Euler(10f, 25f, -15f));
            ragdoll.localScale = new Vector3(1.5f, 0.75f, 2f);

            Vector3 localOffset = new Vector3(0.2f, -0.1f, 0.3f);
            Quaternion rotationOffset = Quaternion.Euler(35f, -20f, 70f);
            target.SetPositionAndRotation(
                ragdoll.TransformPoint(localOffset),
                ragdoll.rotation * rotationOffset);

            RagdollTargetBinding binding = new RagdollTargetBinding(
                default(BoneName),
                target,
                ragdoll);

            Vector3 simulatedPosition = new Vector3(-4f, 3f, 8f);
            Quaternion simulatedRotation = Quaternion.Euler(-30f, 80f, 15f);
            ragdoll.SetPositionAndRotation(simulatedPosition, simulatedRotation);

            Vector3 mappedTargetPosition;
            Quaternion mappedTargetRotation;
            binding.GetTargetWorldPose(
                ragdoll,
                out mappedTargetPosition,
                out mappedTargetRotation);

            target.SetPositionAndRotation(
                mappedTargetPosition,
                mappedTargetRotation);

            RagdollAnimator.AnimatedPose converted =
                binding.ConvertTargetPoseToRagdoll(
                    RagdollAnimator.AnimatedPose.Read(target),
                    ragdoll);

            AssertVector3(simulatedPosition, converted.worldPosition);
            Assert.That(
                Quaternion.Angle(simulatedRotation, converted.worldRotation),
                Is.LessThan(0.001f));
        }

        [Test]
        public void RotationOffset_AllowsDifferentTargetBoneAxes()
        {
            ragdollObject = new GameObject("PuppetBone");
            targetObject = new GameObject("DifferentTargetName");

            Transform ragdoll = ragdollObject.transform;
            Transform target = targetObject.transform;

            ragdoll.rotation = Quaternion.Euler(0f, 20f, 0f);
            target.rotation = ragdoll.rotation * Quaternion.Euler(90f, 0f, 0f);

            RagdollTargetBinding binding = new RagdollTargetBinding(
                default(BoneName),
                target,
                ragdoll);

            Quaternion desiredRagdollRotation = Quaternion.Euler(15f, 70f, -25f);
            target.rotation = desiredRagdollRotation * binding.TargetRotationOffset;

            RagdollAnimator.AnimatedPose converted =
                binding.ConvertTargetPoseToRagdoll(
                    RagdollAnimator.AnimatedPose.Read(target),
                    ragdoll);

            Assert.That(
                Quaternion.Angle(desiredRagdollRotation, converted.worldRotation),
                Is.LessThan(0.001f));
        }

        static void AssertVector3(Vector3 expected, Vector3 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f));
        }
    }
}
