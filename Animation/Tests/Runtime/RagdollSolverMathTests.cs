using Hairibar.Ragdoll;
using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollSolverMathTests
    {
        [Test]
        public void StabilizeInertiaTensor_RaisesOnlySmallPositiveAxes()
        {
            Vector3 result = RagdollSolverMath.StabilizeInertiaTensor(
                new Vector3(1f, 100f, 10f),
                10f);

            Assert.That(result.x, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(100f).Within(0.0001f));
            Assert.That(result.z, Is.EqualTo(10f).Within(0.0001f));
        }

        [Test]
        public void StabilizeInertiaTensor_PreservesZeroLockedAxis()
        {
            Vector3 result = RagdollSolverMath.StabilizeInertiaTensor(
                new Vector3(0f, 100f, 1f),
                10f);

            Assert.That(result.x, Is.Zero);
            Assert.That(result.y, Is.EqualTo(100f).Within(0.0001f));
            Assert.That(result.z, Is.EqualTo(10f).Within(0.0001f));
        }

        [Test]
        public void StabilizeInertiaTensor_LeavesBalancedTensorUntouched()
        {
            Vector3 tensor = new Vector3(4f, 8f, 6f);
            Vector3 result = RagdollSolverMath.StabilizeInertiaTensor(tensor, 10f);

            Assert.That(result, Is.EqualTo(tensor));
        }

        [Test]
        public void ResolveAngularDriveMass_RigidbodyMassPreservesLegacyValue()
        {
            float result = RagdollSolverMath.ResolveAngularDriveMass(
                7f,
                new Vector3(1f, 2f, 9f),
                RagdollAngularDriveInertiaMode.RigidbodyMass);

            Assert.That(result, Is.EqualTo(7f).Within(0.0001f));
        }

        [Test]
        public void ResolveAngularDriveMass_AveragesPositivePrincipalValues()
        {
            float result = RagdollSolverMath.ResolveAngularDriveMass(
                7f,
                new Vector3(0f, 2f, 4f),
                RagdollAngularDriveInertiaMode.AverageInertia);

            Assert.That(result, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void ResolveAngularDriveMass_CanUseMaximumPrincipalValue()
        {
            float result = RagdollSolverMath.ResolveAngularDriveMass(
                7f,
                new Vector3(1f, 2f, 9f),
                RagdollAngularDriveInertiaMode.MaximumInertia);

            Assert.That(result, Is.EqualTo(9f).Within(0.0001f));
        }

        [Test]
        public void ResolveAngularDriveMass_FallsBackWhenTensorHasNoUsableAxis()
        {
            float result = RagdollSolverMath.ResolveAngularDriveMass(
                5f,
                new Vector3(0f, float.NaN, float.PositiveInfinity),
                RagdollAngularDriveInertiaMode.AverageInertia);

            Assert.That(result, Is.EqualTo(5f).Within(0.0001f));
        }

        [Test]
        public void RotationDrive_UsesEffectiveAngularMassForTorqueLimit()
        {
            JointDrive drive = AnimationMatching.GetRotationMatchingJointDrive(
                0.25f,
                1f,
                2f,
                0.02f,
                12f);

            Assert.That(drive.maximumForce, Is.EqualTo(24f).Within(0.0001f));
            Assert.That(drive.positionSpring, Is.GreaterThan(0f));
            Assert.That(drive.positionDamper, Is.GreaterThan(0f));
        }
    }
}
