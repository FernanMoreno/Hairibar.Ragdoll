using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class RagdollTargetDefaultPoseTests
    {
        [Test]
        public void Apply_RestoresCapturedLocalPoseWithoutMovingParent()
        {
            GameObject parentObject = new GameObject("parent");
            GameObject childObject = new GameObject("target");
            try
            {
                Transform parent = parentObject.transform;
                Transform child = childObject.transform;
                child.SetParent(parent, false);
                parent.SetPositionAndRotation(
                    new Vector3(10f, 2f, -3f),
                    Quaternion.Euler(0f, 35f, 0f));
                child.localPosition = new Vector3(1f, 2f, 3f);
                child.localRotation = Quaternion.Euler(4f, 5f, 6f);

                Vector3 parentPosition = parent.position;
                Quaternion parentRotation = parent.rotation;
                RagdollTargetDefaultPose captured =
                    RagdollTargetDefaultPose.Capture(child);

                child.localPosition = new Vector3(-9f, 8f, 7f);
                child.localRotation = Quaternion.Euler(90f, 80f, 70f);
                captured.Apply(child);

                Assert.That(child.localPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(
                    Quaternion.Angle(
                        child.localRotation,
                        Quaternion.Euler(4f, 5f, 6f)),
                    Is.LessThan(0.001f));
                Assert.That(parent.position, Is.EqualTo(parentPosition));
                Assert.That(
                    Quaternion.Angle(parent.rotation, parentRotation),
                    Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(childObject);
                Object.DestroyImmediate(parentObject);
            }
        }
    }
}
