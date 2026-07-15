using Hairibar.Ragdoll;
using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollSolverQualitySettingsTests
    {
        [Test]
        public void Sanitized_ClampsUnsafeNumericValues()
        {
            RagdollSolverQualitySettings settings =
                new RagdollSolverQualitySettings
                {
                    solverIterations = 0,
                    solverVelocityIterations = -4,
                    maxAngularVelocity = float.NaN,
                    maxDepenetrationVelocity = -2f,
                    interpolation = RigidbodyInterpolation.Interpolate,
                    collisionDetectionMode = CollisionDetectionMode.Discrete
                }.Sanitized();

            Assert.That(settings.solverIterations, Is.EqualTo(1));
            Assert.That(settings.solverVelocityIterations, Is.EqualTo(1));
            Assert.That(settings.maxAngularVelocity, Is.Zero);
            Assert.That(settings.maxDepenetrationVelocity, Is.Zero);
        }

        [Test]
        public void TryValidate_RejectsNonFiniteVelocityLimits()
        {
            RagdollSolverQualitySettings settings =
                RagdollSolverQualitySettings.Create(
                    6,
                    2,
                    7f,
                    5f,
                    RigidbodyInterpolation.None,
                    CollisionDetectionMode.Discrete);
            settings.maxAngularVelocity = float.PositiveInfinity;

            string error;
            Assert.That(settings.TryValidate(out error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }
    }
}
