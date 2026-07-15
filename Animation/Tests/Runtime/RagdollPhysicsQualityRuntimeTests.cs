using Hairibar.Ragdoll;
using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollPhysicsQualityRuntimeTests
    {
        static RagdollPhysicsQualityLevel Level(
            string name,
            float distance,
            RagdollSimulationMode mode)
        {
            return new RagdollPhysicsQualityLevel(
                name,
                distance,
                mode,
                0.2f,
                false,
                RagdollSolverQualitySettings.Create(
                    6,
                    2,
                    7f,
                    5f,
                    RigidbodyInterpolation.None,
                    CollisionDetectionMode.Discrete));
        }

        [Test]
        public void TryCreate_AcceptsMonotonicQualityBands()
        {
            RagdollPhysicsQualityRuntime runtime;
            string error;
            bool result = RagdollPhysicsQualityRuntime.TryCreate(
                new[]
                {
                    Level("Near", 0f, RagdollSimulationMode.Active),
                    Level("Far", 20f, RagdollSimulationMode.Kinematic)
                },
                out runtime,
                out error);

            Assert.That(result, Is.True, error);
            Assert.That(runtime.LevelCount, Is.EqualTo(2));
            Assert.That(runtime.FindFirstNonActiveLevel(), Is.EqualTo(1));
        }

        [Test]
        public void TryCreate_RejectsDistancesThatDoNotIncrease()
        {
            RagdollPhysicsQualityRuntime runtime;
            string error;
            bool result = RagdollPhysicsQualityRuntime.TryCreate(
                new[]
                {
                    Level("Near", 0f, RagdollSimulationMode.Active),
                    Level("Duplicate", 0f, RagdollSimulationMode.Active)
                },
                out runtime,
                out error);

            Assert.That(result, Is.False);
            Assert.That(error, Does.Contain("strictly increasing"));
        }

        [Test]
        public void TryCreate_RejectsMoreExpensiveModeAtFartherDistance()
        {
            RagdollPhysicsQualityRuntime runtime;
            string error;
            bool result = RagdollPhysicsQualityRuntime.TryCreate(
                new[]
                {
                    Level("Kinematic", 0f, RagdollSimulationMode.Kinematic),
                    Level("Active Again", 20f, RagdollSimulationMode.Active)
                },
                out runtime,
                out error);

            Assert.That(result, Is.False);
            Assert.That(error, Does.Contain("more expensive"));
        }
    }
}
