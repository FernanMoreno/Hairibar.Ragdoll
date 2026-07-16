using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollPinMathTests
    {
        [Test]
        public void DefaultSettings_MatchPublishedPinDefaults()
        {
            RagdollPinSettings settings = RagdollPinSettings.Default;

            Assert.That(settings.PinPow, Is.EqualTo(4f));
            Assert.That(settings.PinDistanceFalloff, Is.EqualTo(5f));
            Assert.That(settings.AngularPinning, Is.False);
        }

        [Test]
        public void Settings_MigratePreSprint0029DataToPublishedDefaults()
        {
            RagdollPinSettings settings =
                JsonUtility.FromJson<RagdollPinSettings>("{}");

            settings.Normalize();

            Assert.That(settings.PinPow, Is.EqualTo(4f));
            Assert.That(settings.PinDistanceFalloff, Is.EqualTo(5f));
            Assert.That(settings.AngularPinning, Is.False);
        }

        [Test]
        public void Settings_SanitizeNonFiniteAndOutOfRangeValues()
        {
            RagdollPinSettings settings = new RagdollPinSettings(
                float.NaN,
                float.PositiveInfinity,
                true);

            Assert.That(settings.PinPow, Is.EqualTo(4f));
            Assert.That(settings.PinDistanceFalloff, Is.EqualTo(5f));
            Assert.That(settings.AngularPinning, Is.True);

            settings.PinPow = 100f;
            settings.PinDistanceFalloff = -10f;
            Assert.That(settings.PinPow, Is.EqualTo(8f));
            Assert.That(settings.PinDistanceFalloff, Is.Zero);
        }

        [Test]
        public void Settings_PreserveIntentionalZeroFalloffAndAngularPinning()
        {
            RagdollPinSettings settings =
                new RagdollPinSettings(1f, 0f, true);

            settings.Normalize();

            Assert.That(settings.PinPow, Is.EqualTo(1f));
            Assert.That(settings.PinDistanceFalloff, Is.Zero);
            Assert.That(settings.AngularPinning, Is.True);
        }

        [Test]
        public void BoneProfilePinChannel_DefaultsToNeutralAndComposesMultiplicatively()
        {
            BoneProfile profile = new BoneProfile { positionAlpha = 0.4f };

            Assert.That(profile.PositionPinWeight, Is.EqualTo(1f));
            profile.MultiplyPositionPinWeight(0.5f);
            profile.MultiplyPositionPinWeight(0.5f);

            Assert.That(profile.positionAlpha, Is.EqualTo(0.4f));
            Assert.That(profile.PositionPinWeight, Is.EqualTo(0.25f));
        }

        [Test]
        public void BoneProfileBlend_PreservesRuntimePinChannel()
        {
            BoneProfile first = new BoneProfile();
            BoneProfile second = new BoneProfile();
            first.SetPositionPinWeight(0.25f);
            second.SetPositionPinWeight(0.75f);

            BoneProfile result = BoneProfile.Blend(first, second, 0.5f);

            Assert.That(result.PositionPinWeight, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [TestCase(0f, 4f, 0f)]
        [TestCase(1f, 4f, 1f)]
        [TestCase(0.5f, 1f, 0.5f)]
        [TestCase(0.5f, 4f, 0.0625f)]
        public void PinPow_CurvesOnlyIntermediateWeights(
            float weight,
            float exponent,
            float expected)
        {
            Assert.That(
                RagdollPinMath.ResolveCurvedPinWeight(weight, exponent),
                Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void DistanceFalloff_UsesSquaredPositionError()
        {
            Assert.That(
                RagdollPinMath.ResolveDistanceMultiplier(4f, 5f),
                Is.EqualTo(1f / 21f).Within(0.0001f));
            Assert.That(
                RagdollPinMath.ResolveDistanceMultiplier(4f, 0f),
                Is.EqualTo(1f));
        }

        [Test]
        public void PositionAcceleration_ComposesCurveAndDistanceFalloff()
        {
            Vector3 result = RagdollPinMath.ResolvePositionAcceleration(
                new Vector3(8f, 0f, 0f),
                new Vector3(2f, 0f, 0f),
                0.5f,
                2f,
                1f);

            // 8 * 0.25 / (1 + 4) = 0.4
            Assert.That(result.x, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(result.y, Is.Zero);
            Assert.That(result.z, Is.Zero);
        }

        [Test]
        public void AngularPin_ResolvesShortestWorldSpaceVelocityChange()
        {
            Vector3 result = RagdollPinMath.ResolveAngularVelocityChange(
                Quaternion.identity,
                Quaternion.AngleAxis(90f, Vector3.up),
                Vector3.zero,
                1f,
                4f,
                0.5f);

            Assert.That(result.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(Mathf.PI).Within(0.0001f));
            Assert.That(result.z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void AngularPin_UsesShortestEquivalentQuaternionPath()
        {
            Vector3 result = RagdollPinMath.ResolveAngularVelocityChange(
                Quaternion.identity,
                Quaternion.AngleAxis(270f, Vector3.up),
                Vector3.zero,
                1f,
                4f,
                1f);

            Assert.That(result.y, Is.EqualTo(-Mathf.PI * 0.5f).Within(0.0001f));
        }

        [Test]
        public void AngularPin_CompensatesCurrentAngularVelocityAndCurvesWeight()
        {
            Vector3 result = RagdollPinMath.ResolveAngularVelocityChange(
                Quaternion.identity,
                Quaternion.identity,
                new Vector3(0f, 2f, 0f),
                0.5f,
                2f,
                0.02f);

            Assert.That(result.y, Is.EqualTo(-0.5f).Within(0.0001f));
        }

        [Test]
        public void NonFiniteInputs_DoNotProduceNonFiniteForces()
        {
            Vector3 position = RagdollPinMath.ResolvePositionAcceleration(
                new Vector3(float.NaN, 0f, 0f),
                Vector3.zero,
                1f,
                4f,
                5f);
            Vector3 angular = RagdollPinMath.ResolveAngularVelocityChange(
                new Quaternion(float.NaN, 0f, 0f, 1f),
                Quaternion.identity,
                Vector3.zero,
                1f,
                4f,
                0.02f);

            Assert.That(position, Is.EqualTo(Vector3.zero));
            Assert.That(angular, Is.EqualTo(Vector3.zero));
        }
    }
}
