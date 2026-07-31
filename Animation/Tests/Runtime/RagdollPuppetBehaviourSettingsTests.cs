using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollPuppetBehaviourSettingsTests
    {
        GameObject owner;
        RagdollPuppetBehaviour behaviour;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("puppet-settings-test");
            behaviour = owner.AddComponent<RagdollPuppetBehaviour>();
        }

        [TearDown]
        public void TearDown()
        {
            if (owner) Object.DestroyImmediate(owner);
        }

        [Test]
        public void MaximumVelocity_RejectsNegativeAndNonFiniteInputs()
        {
            behaviour.MaxRigidbodyVelocity = -10f;
            Assert.That(behaviour.MaxRigidbodyVelocity, Is.Zero);

            behaviour.MaxRigidbodyVelocity = float.NaN;
            Assert.That(behaviour.MaxRigidbodyVelocity, Is.Zero);

            behaviour.MaxRigidbodyVelocity = float.PositiveInfinity;
            Assert.That(behaviour.MaxRigidbodyVelocity, Is.EqualTo(Mathf.Infinity));
        }

        [Test]
        public void GetUpSetters_RejectNegativeAndNaNInputs()
        {
            behaviour.GetUpDelay = -1f;
            behaviour.BlendToAnimationTime = float.NaN;
            behaviour.MaxGetUpVelocity = -2f;
            behaviour.MinGetUpDuration = float.NaN;
            behaviour.GetUpCollisionResistanceMlp = -3f;
            behaviour.GetUpRegainPinSpeedMlp = float.NaN;
            behaviour.GetUpKnockOutDistanceMlp = -4f;

            Assert.That(behaviour.GetUpDelay, Is.Zero);
            Assert.That(behaviour.BlendToAnimationTime, Is.Zero);
            Assert.That(behaviour.MaxGetUpVelocity, Is.Zero);
            Assert.That(behaviour.MinGetUpDuration, Is.Zero);
            Assert.That(behaviour.GetUpCollisionResistanceMlp, Is.Zero);
            Assert.That(behaviour.GetUpRegainPinSpeedMlp, Is.Zero);
            Assert.That(behaviour.GetUpKnockOutDistanceMlp, Is.Zero);
        }

        [Test]
        public void RuntimeCollisionAndGroundingSettings_AreSanitized()
        {
            behaviour.PinWeightThreshold = float.NaN;
            behaviour.UnpinnedMuscleWeightMultiplier = 4f;
            behaviour.RegainPinSpeed = float.PositiveInfinity;
            behaviour.MuscleWeightRelativeToPinWeight = -2f;
            behaviour.CollisionThreshold = float.NaN;
            behaviour.MaximumCollisionsPerFixedStep = 100;
            behaviour.GroundProbeStartOffset = -1f;
            behaviour.GroundProbeDistance = float.NaN;
            behaviour.MaximumGroundAngle = 120f;
            behaviour.BodyFrontAxis = Vector3.zero;

            Assert.That(behaviour.PinWeightThreshold, Is.Zero);
            Assert.That(behaviour.UnpinnedMuscleWeightMultiplier, Is.EqualTo(1f));
            Assert.That(behaviour.RegainPinSpeed, Is.EqualTo(1f));
            Assert.That(behaviour.MuscleWeightRelativeToPinWeight, Is.Zero);
            Assert.That(behaviour.CollisionThreshold, Is.Zero);
            Assert.That(behaviour.MaximumCollisionsPerFixedStep, Is.EqualTo(30));
            Assert.That(behaviour.GroundProbeStartOffset, Is.Zero);
            Assert.That(behaviour.GroundProbeDistance, Is.EqualTo(0.001f));
            Assert.That(behaviour.MaximumGroundAngle, Is.EqualTo(89.9f));
            Assert.That(behaviour.BodyFrontAxis, Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void Respawn_RequiresInitializedBehaviour()
        {
            Assert.Throws<System.InvalidOperationException>(() =>
                behaviour.Respawn(Vector3.zero, Quaternion.identity));
        }
    }
}
