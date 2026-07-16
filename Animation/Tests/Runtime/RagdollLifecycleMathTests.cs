using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollLifecycleMathTests
    {
        [Test]
        public void DefaultSettings_MatchPublishedLifecycleDefaults()
        {
            RagdollLifecycleSettings settings =
                RagdollLifecycleSettings.Default;

            Assert.That(settings.KillDuration, Is.EqualTo(1f));
            Assert.That(settings.DeadMuscleWeight, Is.EqualTo(0.01f));
            Assert.That(settings.DeadMuscleDamper, Is.EqualTo(2f));
            Assert.That(settings.MaxFreezeSqrVelocity, Is.EqualTo(0.02f));
            Assert.That(settings.FreezePermanently, Is.False);
            Assert.That(settings.EnableAngularLimitsOnKill, Is.True);
            Assert.That(settings.EnableInternalCollisionsOnKill, Is.True);
        }

        [Test]
        public void KillWeight_BlendsLinearlyToDeadWeight()
        {
            float value = RagdollLifecycleMath.EvaluateKillMuscleWeight(
                1f,
                0.2f,
                0.5f,
                1f);

            Assert.That(value, Is.EqualTo(0.6f).Within(0.0001f));
        }

        [Test]
        public void KillWeight_ZeroDurationCompletesImmediately()
        {
            Assert.That(
                RagdollLifecycleMath.EvaluateKillMuscleWeight(
                    1f,
                    0.2f,
                    0f,
                    0f),
                Is.EqualTo(0.2f));
            Assert.That(
                RagdollLifecycleMath.IsKillComplete(
                    1f,
                    0.2f,
                    0f,
                    0f),
                Is.True);
        }

        [Test]
        public void KillWeight_CompletesToConfiguredWeightWhenStartingLower()
        {
            Assert.That(
                RagdollLifecycleMath.EvaluateKillMuscleWeight(
                    0.1f,
                    0.2f,
                    0f,
                    1f),
                Is.EqualTo(0.2f));
        }

        [Test]
        public void DeadDrive_ReleasesPositionAndScalesRotation()
        {
            BoneProfile profile = new BoneProfile
            {
                positionAlpha = 10f,
                rotationAlpha = 20f,
                rotationDampingRatio = 0.5f
            };

            RagdollLifecycleMath.ApplyDeadDrive(
                ref profile,
                0f,
                0.25f,
                2f);

            Assert.That(profile.positionAlpha, Is.Zero);
            Assert.That(profile.rotationAlpha, Is.EqualTo(5f));
            Assert.That(profile.rotationDampingRatio, Is.EqualTo(2.5f));
        }

        [Test]
        public void Settings_AllowIntentionalInstantFullyLimpDeath()
        {
            RagdollLifecycleSettings settings =
                new RagdollLifecycleSettings(0f, 0f, 0f);

            Assert.That(settings.KillDuration, Is.Zero);
            Assert.That(settings.DeadMuscleWeight, Is.Zero);
            Assert.That(settings.DeadMuscleDamper, Is.Zero);
        }

        [Test]
        public void Settings_SanitizeNonFiniteAndOutOfRangeValues()
        {
            RagdollLifecycleSettings settings =
                new RagdollLifecycleSettings(
                    float.NaN,
                    5f,
                    float.PositiveInfinity);

            Assert.That(settings.KillDuration, Is.EqualTo(1f));
            Assert.That(settings.DeadMuscleWeight, Is.EqualTo(1f));
            Assert.That(settings.DeadMuscleDamper, Is.EqualTo(2f));
        }

        [Test]
        public void Settings_PreserveIntentionalFreezeOverrides()
        {
            RagdollLifecycleSettings settings =
                new RagdollLifecycleSettings(
                    0f,
                    0f,
                    0f,
                    0f,
                    true,
                    false,
                    false);

            Assert.That(settings.MaxFreezeSqrVelocity, Is.Zero);
            Assert.That(settings.FreezePermanently, Is.True);
            Assert.That(settings.EnableAngularLimitsOnKill, Is.False);
            Assert.That(settings.EnableInternalCollisionsOnKill, Is.False);
        }

        [Test]
        public void Settings_MigrateSprint0026DataToFreezeDefaults()
        {
            RagdollLifecycleSettings settings =
                JsonUtility.FromJson<RagdollLifecycleSettings>(
                    "{\"killDuration\":3,"
                    + "\"deadMuscleWeight\":0.25,"
                    + "\"deadMuscleDamper\":4}");

            settings.Normalize();

            Assert.That(settings.KillDuration, Is.EqualTo(3f));
            Assert.That(settings.DeadMuscleWeight, Is.EqualTo(0.25f));
            Assert.That(settings.DeadMuscleDamper, Is.EqualTo(4f));
            Assert.That(settings.MaxFreezeSqrVelocity, Is.EqualTo(0.02f));
            Assert.That(settings.FreezePermanently, Is.False);
            Assert.That(settings.EnableAngularLimitsOnKill, Is.True);
            Assert.That(settings.EnableInternalCollisionsOnKill, Is.True);
        }

        [TestCase(0f, 0f, true)]
        [TestCase(0.02f, 0.02f, true)]
        [TestCase(0.0201f, 0.02f, false)]
        public void FreezeVelocity_UsesInclusiveSquaredThreshold(
            float velocity,
            float threshold,
            bool expected)
        {
            Assert.That(
                RagdollLifecycleMath.IsFreezeVelocityReady(
                    velocity,
                    threshold),
                Is.EqualTo(expected));
        }

        [Test]
        public void FreezeVelocity_NonFiniteValuesNeverFreeze()
        {
            Assert.That(
                RagdollLifecycleMath.IsFreezeVelocityReady(
                    float.NaN,
                    0.02f),
                Is.False);
            Assert.That(
                RagdollLifecycleMath.IsFreezeVelocityReady(
                    float.PositiveInfinity,
                    0.02f),
                Is.False);
        }
    }
}
