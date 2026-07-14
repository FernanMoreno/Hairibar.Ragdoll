using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class AnimatedPoseSamplerTests
    {
        [Test]
        public void FirstSample_InitializesWithZeroVelocity()
        {
            AnimatedPoseSampler sampler = new AnimatedPoseSampler();

            sampler.Push(Pose(new Vector3(3f, 2f, 1f), Quaternion.identity), 10f);

            Assert.That(sampler.IsInitialized, Is.True);
            Assert.That(sampler.Pose.worldPosition, Is.EqualTo(new Vector3(3f, 2f, 1f)));
            Assert.That(sampler.LinearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(sampler.AngularVelocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Push_UsesAnimationSampleIntervalInsteadOfFixedDeltaTime()
        {
            AnimatedPoseSampler sampler = new AnimatedPoseSampler();
            sampler.Push(Pose(Vector3.zero, Quaternion.identity), 1f);

            sampler.Push(Pose(new Vector3(1f, 0f, 0f), Quaternion.identity), 1.25f);

            Assert.That(sampler.LinearVelocity.x, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(sampler.LinearVelocity.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(sampler.LinearVelocity.z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void CachedVelocity_RemainsStableUntilAnotherAnimationSampleArrives()
        {
            AnimatedPoseSampler sampler = new AnimatedPoseSampler();
            sampler.Push(Pose(Vector3.zero, Quaternion.identity), 0f);
            sampler.Push(Pose(new Vector3(1f, 0f, 0f), Quaternion.identity), 0.5f);

            Vector3 firstPhysicsStep = sampler.LinearVelocity;
            Vector3 secondPhysicsStep = sampler.LinearVelocity;

            Assert.That(firstPhysicsStep, Is.EqualTo(new Vector3(2f, 0f, 0f)));
            Assert.That(secondPhysicsStep, Is.EqualTo(firstPhysicsStep));
        }

        [Test]
        public void Push_CalculatesAngularVelocityFromLocalRotation()
        {
            AnimatedPoseSampler sampler = new AnimatedPoseSampler();
            sampler.Push(Pose(Vector3.zero, Quaternion.identity), 2f);

            sampler.Push(Pose(Vector3.zero, Quaternion.Euler(0f, 90f, 0f)), 2.5f);

            Assert.That(sampler.AngularVelocity.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(sampler.AngularVelocity.y, Is.EqualTo(Mathf.PI).Within(0.0001f));
            Assert.That(sampler.AngularVelocity.z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void TimeRollback_ResetsVelocityInsteadOfProducingInvalidSpike()
        {
            AnimatedPoseSampler sampler = new AnimatedPoseSampler();
            sampler.Push(Pose(Vector3.zero, Quaternion.identity), 10f);
            sampler.Push(Pose(Vector3.one, Quaternion.identity), 11f);

            sampler.Push(Pose(new Vector3(100f, 0f, 0f), Quaternion.identity), 1f);

            Assert.That(sampler.Pose.worldPosition, Is.EqualTo(new Vector3(100f, 0f, 0f)));
            Assert.That(sampler.LinearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(sampler.AngularVelocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void SameTimestamp_UpdatesPoseWithoutErasingLastValidVelocity()
        {
            AnimatedPoseSampler sampler = new AnimatedPoseSampler();
            sampler.Push(Pose(Vector3.zero, Quaternion.identity), 0f);
            sampler.Push(Pose(Vector3.one, Quaternion.identity), 1f);

            sampler.Push(Pose(new Vector3(2f, 0f, 0f), Quaternion.identity), 1f);

            Assert.That(sampler.Pose.worldPosition, Is.EqualTo(new Vector3(2f, 0f, 0f)));
            Assert.That(sampler.LinearVelocity, Is.EqualTo(Vector3.one));
        }

        static RagdollAnimator.AnimatedPose Pose(Vector3 position, Quaternion localRotation)
        {
            return new RagdollAnimator.AnimatedPose
            {
                worldPosition = position,
                worldRotation = localRotation,
                localRotation = localRotation
            };
        }
    }
}
