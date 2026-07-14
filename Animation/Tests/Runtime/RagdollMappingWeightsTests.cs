using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class RagdollMappingWeightsTests
    {
        GameObject parentObject;
        GameObject childObject;

        [TearDown]
        public void TearDown()
        {
            if (childObject) Object.DestroyImmediate(childObject);
            if (parentObject) Object.DestroyImmediate(parentObject);
        }

        [Test]
        public void FullMapping_UsesSimulatedPositionAndRotation()
        {
            parentObject = new GameObject("Target");
            parentObject.transform.SetPositionAndRotation(
                new Vector3(1f, 2f, 3f),
                Quaternion.Euler(10f, 20f, 30f));

            Vector3 simulatedPosition = new Vector3(8f, 9f, 10f);
            Quaternion simulatedRotation = Quaternion.Euler(40f, 50f, 60f);

            RagdollToTargetMapper.MapTransform(
                parentObject.transform,
                simulatedPosition,
                simulatedRotation,
                RagdollMappingWeights.Full);

            Assert.That(parentObject.transform.position, Is.EqualTo(simulatedPosition));
            Assert.That(Quaternion.Angle(parentObject.transform.rotation, simulatedRotation), Is.LessThan(0.001f));
        }

        [Test]
        public void ZeroMapping_PreservesAnimatedTransform()
        {
            parentObject = new GameObject("Target");
            Vector3 animatedPosition = new Vector3(1f, 2f, 3f);
            Quaternion animatedRotation = Quaternion.Euler(10f, 20f, 30f);
            parentObject.transform.SetPositionAndRotation(animatedPosition, animatedRotation);

            RagdollToTargetMapper.MapTransform(
                parentObject.transform,
                new Vector3(8f, 9f, 10f),
                Quaternion.Euler(40f, 50f, 60f),
                RagdollMappingWeights.None);

            Assert.That(parentObject.transform.position, Is.EqualTo(animatedPosition));
            Assert.That(Quaternion.Angle(parentObject.transform.rotation, animatedRotation), Is.LessThan(0.001f));
        }

        [Test]
        public void PositionAndRotationMapping_AreIndependent()
        {
            parentObject = new GameObject("Target");
            Vector3 animatedPosition = new Vector3(1f, 2f, 3f);
            Quaternion animatedRotation = Quaternion.Euler(10f, 20f, 30f);
            Quaternion simulatedRotation = Quaternion.Euler(40f, 50f, 60f);
            parentObject.transform.SetPositionAndRotation(animatedPosition, animatedRotation);

            RagdollToTargetMapper.MapTransform(
                parentObject.transform,
                new Vector3(8f, 9f, 10f),
                simulatedRotation,
                new RagdollMappingWeights(0f, 1f));

            Assert.That(parentObject.transform.position, Is.EqualTo(animatedPosition));
            Assert.That(Quaternion.Angle(parentObject.transform.rotation, simulatedRotation), Is.LessThan(0.001f));
        }

        [Test]
        public void ChildWithZeroPositionWeight_FollowsMappedParentWithoutWorldSpaceCorrection()
        {
            parentObject = new GameObject("Parent");
            childObject = new GameObject("Child");
            childObject.transform.SetParent(parentObject.transform, false);
            childObject.transform.localPosition = Vector3.right;

            RagdollToTargetMapper.MapTransform(
                parentObject.transform,
                new Vector3(10f, 0f, 0f),
                Quaternion.identity,
                RagdollMappingWeights.Full);

            RagdollToTargetMapper.MapTransform(
                childObject.transform,
                new Vector3(20f, 0f, 0f),
                Quaternion.identity,
                RagdollMappingWeights.None);

            Assert.That(childObject.transform.localPosition, Is.EqualTo(Vector3.right));
            Assert.That(childObject.transform.position, Is.EqualTo(new Vector3(11f, 0f, 0f)));
        }

        [Test]
        public void Multipliers_ComposeAndClamp()
        {
            RagdollMappingWeights weights = new RagdollMappingWeights(0.8f, 0.5f);

            weights.Multiply(0.5f, 4f);

            Assert.That(weights.PositionWeight, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(weights.RotationWeight, Is.EqualTo(0.5f).Within(0.0001f));
        }
    }
}
