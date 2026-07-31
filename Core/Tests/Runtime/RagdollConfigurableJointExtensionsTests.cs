using Hairibar.EngineExtensions;
using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Tests
{
    public sealed class RagdollConfigurableJointExtensionsTests
    {
        GameObject owner;

        [TearDown]
        public void TearDown()
        {
            if (owner) Object.DestroyImmediate(owner);
        }

        [Test]
        public void SetTargetRotation_WorldSpace_PreservesQuaternionOrder()
        {
            ConfigurableJoint joint = CreateJoint();
            joint.axis = new Vector3(1f, 0.2f, 0.1f);
            joint.secondaryAxis = new Vector3(0.3f, 1f, 0.4f);
            Quaternion start = Quaternion.Euler(17f, 41f, -23f);
            Quaternion target = Quaternion.Euler(-31f, 12f, 67f);

            joint.SetTargetRotation(target, start);

            Quaternion jointSpace = ResolveJointSpace(joint);
            Quaternion expected = Quaternion.Inverse(jointSpace)
                * start
                * Quaternion.Inverse(target)
                * jointSpace;
            AssertQuaternion(expected, joint.targetRotation);
        }

        [Test]
        public void SetTargetRotationLocal_UsesOrthonormalJointSpace()
        {
            ConfigurableJoint joint = CreateJoint();
            joint.axis = new Vector3(1f, 0.25f, 0f);
            joint.secondaryAxis = new Vector3(0.4f, 1f, 0.3f);
            Quaternion start = Quaternion.Euler(3f, 28f, -9f);
            Quaternion target = Quaternion.Euler(44f, -16f, 21f);

            joint.SetTargetRotationLocal(target, start);

            Quaternion jointSpace = ResolveJointSpace(joint);
            Quaternion expected = Quaternion.Inverse(jointSpace)
                * Quaternion.Inverse(target)
                * start
                * jointSpace;
            AssertQuaternion(expected, joint.targetRotation);
        }

        ConfigurableJoint CreateJoint()
        {
            owner = new GameObject("joint-extension-test");
            owner.AddComponent<Rigidbody>();
            return owner.AddComponent<ConfigurableJoint>();
        }

        static Quaternion ResolveJointSpace(ConfigurableJoint joint)
        {
            Vector3 right = joint.axis.normalized;
            Vector3 forward = Vector3.Cross(
                right,
                joint.secondaryAxis.normalized).normalized;
            Vector3 up = Vector3.Cross(forward, right).normalized;
            return Quaternion.LookRotation(forward, up);
        }

        static void AssertQuaternion(Quaternion expected, Quaternion actual)
        {
            Assert.That(
                Mathf.Abs(Quaternion.Dot(expected, actual)),
                Is.EqualTo(1f).Within(0.00001f));
        }
    }
}
