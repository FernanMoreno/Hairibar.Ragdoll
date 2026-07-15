using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class RagdollCenterOfMassMathTests
    {
        [Test]
        public void Resolve_UsesMassWeightedPositionAndVelocity()
        {
            float totalMass = 0f;
            Vector3 weightedPosition = Vector3.zero;
            Vector3 weightedVelocity = Vector3.zero;

            RagdollCenterOfMassMath.Accumulate(
                1f,
                Vector3.zero,
                Vector3.right,
                ref totalMass,
                ref weightedPosition,
                ref weightedVelocity);
            RagdollCenterOfMassMath.Accumulate(
                3f,
                new Vector3(4f, 0f, 0f),
                new Vector3(5f, 0f, 0f),
                ref totalMass,
                ref weightedPosition,
                ref weightedVelocity);

            Vector3 center;
            Vector3 velocity;
            RagdollCenterOfMassMath.Resolve(
                totalMass,
                weightedPosition,
                weightedVelocity,
                out center,
                out velocity);

            Assert.That(totalMass, Is.EqualTo(4f));
            Assert.That(center.x, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(velocity.x, Is.EqualTo(4f).Within(0.0001f));
        }

        [Test]
        public void Accumulate_IgnoresNonPositiveMass()
        {
            float totalMass = 0f;
            Vector3 weightedPosition = Vector3.zero;
            Vector3 weightedVelocity = Vector3.zero;

            RagdollCenterOfMassMath.Accumulate(
                -2f,
                Vector3.one,
                Vector3.one,
                ref totalMass,
                ref weightedPosition,
                ref weightedVelocity);

            Assert.That(totalMass, Is.EqualTo(0f));
            Assert.That(weightedPosition, Is.EqualTo(Vector3.zero));
            Assert.That(weightedVelocity, Is.EqualTo(Vector3.zero));
        }
    }
}
