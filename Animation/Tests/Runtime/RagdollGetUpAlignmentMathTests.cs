using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class RagdollGetUpAlignmentMathTests
    {
        [Test]
        public void Classify_ReturnsProneWhenFrontFacesGround()
        {
            Quaternion rootRotation = Quaternion.Euler(90f, 0f, 0f);

            RagdollGetUpOrientation orientation =
                RagdollGetUpAlignmentMath.Classify(
                    rootRotation,
                    Vector3.forward,
                    Vector3.up,
                    0.2f);

            Assert.That(orientation, Is.EqualTo(RagdollGetUpOrientation.Prone));
        }

        [Test]
        public void Classify_ReturnsSupineWhenFrontFacesSky()
        {
            Quaternion rootRotation = Quaternion.Euler(-90f, 0f, 0f);

            RagdollGetUpOrientation orientation =
                RagdollGetUpAlignmentMath.Classify(
                    rootRotation,
                    Vector3.forward,
                    Vector3.up,
                    0.2f);

            Assert.That(orientation, Is.EqualTo(RagdollGetUpOrientation.Supine));
        }

        [Test]
        public void Classify_ReturnsUnknownNearSidewaysThreshold()
        {
            RagdollGetUpOrientation orientation =
                RagdollGetUpAlignmentMath.Classify(
                    Quaternion.identity,
                    Vector3.forward,
                    Vector3.up,
                    0.2f);

            Assert.That(orientation, Is.EqualTo(RagdollGetUpOrientation.Unknown));
        }

        [Test]
        public void TargetRootPose_AlignsCurrentTargetHipToDesiredHip()
        {
            Vector3 currentRoot = new Vector3(2f, 0f, 3f);
            Quaternion currentRotation = Quaternion.Euler(0f, 35f, 0f);
            Vector3 currentHip = currentRoot + currentRotation * new Vector3(0f, 1f, 0f);
            Vector3 puppetHip = new Vector3(10f, 0.5f, -4f);
            Vector3 offset = new Vector3(0.1f, 0f, 0.2f);

            Vector3 desiredRoot;
            Quaternion desiredRotation;
            RagdollGetUpAlignmentMath.CalculateTargetRootPose(
                currentRoot,
                currentRotation,
                currentHip,
                puppetHip,
                Quaternion.Euler(90f, 20f, 0f),
                Vector3.up,
                RagdollGetUpOrientation.Prone,
                Vector3.up,
                Vector3.forward,
                offset,
                out desiredRoot,
                out desiredRotation);

            Quaternion delta = desiredRotation * Quaternion.Inverse(currentRotation);
            Vector3 alignedHip = desiredRoot + delta * (currentHip - currentRoot);
            Vector3 expectedHip = puppetHip + desiredRotation * offset;

            Assert.That(Vector3.Distance(alignedHip, expectedHip), Is.LessThan(0.0001f));
            Assert.That(Vector3.Dot(desiredRotation * Vector3.up, Vector3.up), Is.GreaterThan(0.999f));
        }

        [Test]
        public void SupineHeading_ReversesProjectedBodyUp()
        {
            Quaternion rootRotation = Quaternion.Euler(-90f, 0f, 0f);
            Vector3 heading = RagdollGetUpAlignmentMath.CalculateHeading(
                rootRotation,
                Vector3.up,
                RagdollGetUpOrientation.Supine,
                Vector3.up,
                Vector3.forward);

            Assert.That(Vector3.Dot(heading, Vector3.forward), Is.GreaterThan(0.999f));
        }
    }
}
