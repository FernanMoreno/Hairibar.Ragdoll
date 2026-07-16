using System;
using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class RagdollTeleportMathTests
    {
        [Test]
        public void Delta_ReachesRequestedTargetPoseAroundArbitraryPivot()
        {
            Vector3 currentPosition = new Vector3(2f, 1f, -3f);
            Quaternion currentRotation = Quaternion.Euler(5f, 20f, -10f);
            Vector3 destinationPosition = new Vector3(-4f, 7f, 9f);
            Quaternion destinationRotation = Quaternion.Euler(-15f, 130f, 25f);
            Vector3 pivot = new Vector3(10f, -2f, 3f);

            Quaternion deltaRotation =
                RagdollTeleportMath.CalculateDeltaRotation(
                    currentRotation,
                    destinationRotation);
            Vector3 deltaPosition =
                RagdollTeleportMath.CalculateDeltaPosition(
                    currentPosition,
                    destinationPosition,
                    pivot,
                    deltaRotation);

            Vector3 transformedPosition = RagdollTeleportMath.TransformPoint(
                currentPosition,
                deltaRotation,
                deltaPosition,
                pivot);
            Quaternion transformedRotation =
                RagdollTeleportMath.TransformRotation(
                    currentRotation,
                    deltaRotation);

            Assert.That(
                Vector3.Distance(transformedPosition, destinationPosition),
                Is.LessThan(0.0001f));
            Assert.That(
                Quaternion.Angle(transformedRotation, destinationRotation),
                Is.LessThan(0.001f));
        }

        [Test]
        public void TransformPose_PreservesLocalRotation()
        {
            RagdollAnimator.AnimatedPose pose = new RagdollAnimator.AnimatedPose
            {
                worldPosition = Vector3.right,
                worldRotation = Quaternion.Euler(0f, 15f, 0f),
                localRotation = Quaternion.Euler(12f, 3f, 7f)
            };

            RagdollAnimator.AnimatedPose transformed =
                RagdollTeleportMath.TransformPose(
                    pose,
                    Quaternion.Euler(0f, 90f, 0f),
                    new Vector3(4f, 0f, 0f),
                    Vector3.zero);

            Assert.That(transformed.worldPosition.x, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(transformed.worldPosition.z, Is.EqualTo(-1f).Within(0.0001f));
            Assert.That(
                Quaternion.Angle(
                    transformed.worldRotation,
                    Quaternion.Euler(0f, 105f, 0f)),
                Is.LessThan(0.001f));
            Assert.That(
                Quaternion.Angle(transformed.localRotation, pose.localRotation),
                Is.LessThan(0.001f));
        }

        [Test]
        public void RootApplication_TargetAncestor_DoesNotDoubleTransformPuppet()
        {
            GameObject targetObject = new GameObject("target");
            GameObject puppetObject = new GameObject("puppet");
            try
            {
                Transform target = targetObject.transform;
                Transform puppet = puppetObject.transform;
                puppet.SetParent(target, true);
                target.SetPositionAndRotation(
                    new Vector3(1f, 0f, 2f),
                    Quaternion.Euler(0f, 10f, 0f));
                puppet.SetPositionAndRotation(
                    new Vector3(4f, 0f, -1f),
                    Quaternion.Euler(0f, 25f, 0f));

                Vector3 targetOriginalPosition = target.position;
                Quaternion targetOriginalRotation = target.rotation;
                Vector3 puppetOriginalPosition = puppet.position;
                Quaternion puppetOriginalRotation = puppet.rotation;
                Vector3 destination = new Vector3(-3f, 2f, 8f);
                Quaternion destinationRotation = Quaternion.Euler(0f, 120f, 0f);
                Vector3 pivot = targetOriginalPosition;
                Quaternion deltaRotation = RagdollTeleportMath.CalculateDeltaRotation(
                    targetOriginalRotation,
                    destinationRotation);
                Vector3 deltaPosition = RagdollTeleportMath.CalculateDeltaPosition(
                    targetOriginalPosition,
                    destination,
                    pivot,
                    deltaRotation);
                Vector3 expectedPuppetPosition = RagdollTeleportMath.TransformPoint(
                    puppetOriginalPosition,
                    deltaRotation,
                    deltaPosition,
                    pivot);
                Quaternion expectedPuppetRotation = RagdollTeleportMath.TransformRotation(
                    puppetOriginalRotation,
                    deltaRotation);

                RagdollTeleportHierarchy.ApplyRootTransforms(
                    target,
                    puppet,
                    destination,
                    destinationRotation,
                    puppetOriginalPosition,
                    puppetOriginalRotation,
                    deltaRotation,
                    deltaPosition,
                    pivot);

                Assert.That(Vector3.Distance(target.position, destination), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(puppet.position, expectedPuppetPosition), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(puppet.rotation, expectedPuppetRotation), Is.LessThan(0.001f));

                RagdollTeleportHierarchy.RestoreRootTransforms(
                    target,
                    targetOriginalPosition,
                    targetOriginalRotation,
                    puppet,
                    puppetOriginalPosition,
                    puppetOriginalRotation);
                Assert.That(Vector3.Distance(target.position, targetOriginalPosition), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(puppet.position, puppetOriginalPosition), Is.LessThan(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(puppetObject);
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void RootApplication_PuppetAncestor_ReachesTargetOnceAndRestoresBoth()
        {
            GameObject puppetObject = new GameObject("puppet");
            GameObject targetObject = new GameObject("target");
            try
            {
                Transform puppet = puppetObject.transform;
                Transform target = targetObject.transform;
                target.SetParent(puppet, true);
                puppet.SetPositionAndRotation(
                    new Vector3(-2f, 1f, 5f),
                    Quaternion.Euler(0f, -20f, 0f));
                target.SetPositionAndRotation(
                    new Vector3(3f, 2f, 4f),
                    Quaternion.Euler(0f, 15f, 0f));

                Vector3 targetOriginalPosition = target.position;
                Quaternion targetOriginalRotation = target.rotation;
                Vector3 puppetOriginalPosition = puppet.position;
                Quaternion puppetOriginalRotation = puppet.rotation;
                Vector3 destination = new Vector3(12f, -1f, 7f);
                Quaternion destinationRotation = Quaternion.Euler(5f, 95f, -3f);
                Vector3 pivot = puppetOriginalPosition;
                Quaternion deltaRotation = RagdollTeleportMath.CalculateDeltaRotation(
                    targetOriginalRotation,
                    destinationRotation);
                Vector3 deltaPosition = RagdollTeleportMath.CalculateDeltaPosition(
                    targetOriginalPosition,
                    destination,
                    pivot,
                    deltaRotation);
                Vector3 expectedPuppetPosition = RagdollTeleportMath.TransformPoint(
                    puppetOriginalPosition,
                    deltaRotation,
                    deltaPosition,
                    pivot);

                RagdollTeleportHierarchy.ApplyRootTransforms(
                    target,
                    puppet,
                    destination,
                    destinationRotation,
                    puppetOriginalPosition,
                    puppetOriginalRotation,
                    deltaRotation,
                    deltaPosition,
                    pivot);

                Assert.That(Vector3.Distance(target.position, destination), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(target.rotation, destinationRotation), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(puppet.position, expectedPuppetPosition), Is.LessThan(0.0001f));

                RagdollTeleportHierarchy.RestoreRootTransforms(
                    target,
                    targetOriginalPosition,
                    targetOriginalRotation,
                    puppet,
                    puppetOriginalPosition,
                    puppetOriginalRotation);
                Assert.That(Vector3.Distance(target.position, targetOriginalPosition), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(target.rotation, targetOriginalRotation), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(puppet.position, puppetOriginalPosition), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(puppet.rotation, puppetOriginalRotation), Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(targetObject);
                UnityEngine.Object.DestroyImmediate(puppetObject);
            }
        }

        [Test]
        public void Request_NormalizesRotationAndRejectsInvalidInputs()
        {
            RagdollTeleportRequest request = RagdollTeleportRequest.Create(
                Vector3.one,
                new Quaternion(0f, 2f, 0f, 2f),
                true);

            float magnitude = Mathf.Sqrt(
                request.Rotation.x * request.Rotation.x
                + request.Rotation.y * request.Rotation.y
                + request.Rotation.z * request.Rotation.z
                + request.Rotation.w * request.Rotation.w);
            Assert.That(magnitude, Is.EqualTo(1f).Within(0.0001f));

            Assert.Throws<ArgumentException>(() =>
                RagdollTeleportRequest.Create(
                    new Vector3(float.NaN, 0f, 0f),
                    Quaternion.identity,
                    false));
            Assert.Throws<ArgumentException>(() =>
                RagdollTeleportRequest.Create(
                    Vector3.zero,
                    new Quaternion(0f, 0f, 0f, 0f),
                    false));
        }
    }
}
