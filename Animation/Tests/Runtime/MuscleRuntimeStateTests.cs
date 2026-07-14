using NUnit.Framework;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class MuscleRuntimeStateTests
    {
        [Test]
        public void DefaultState_PreservesBoneProfile()
        {
            BoneProfile profile = CreateProfile();
            BoneProfile expected = profile;
            MuscleRuntimeState state = MuscleRuntimeState.Default;

            state.ApplyTo(ref profile);

            Assert.That(profile.positionAlpha, Is.EqualTo(expected.positionAlpha));
            Assert.That(profile.positionDampingRatio, Is.EqualTo(expected.positionDampingRatio));
            Assert.That(profile.maxLinearAcceleration, Is.EqualTo(expected.maxLinearAcceleration));
            Assert.That(profile.rotationAlpha, Is.EqualTo(expected.rotationAlpha));
            Assert.That(profile.rotationDampingRatio, Is.EqualTo(expected.rotationDampingRatio));
            Assert.That(profile.maxAngularAcceleration, Is.EqualTo(expected.maxAngularAcceleration));
        }

        [Test]
        public void DefaultState_PreservesMappingWeights()
        {
            RagdollMappingWeights weights = new RagdollMappingWeights(0.4f, 0.7f);
            MuscleRuntimeState state = MuscleRuntimeState.Default;

            state.ApplyTo(ref weights);

            Assert.That(weights.PositionWeight, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(weights.RotationWeight, Is.EqualTo(0.7f).Within(0.0001f));
        }

        [Test]
        public void MappingAuthorities_AreIndependentAndClamped()
        {
            RagdollMappingWeights weights = RagdollMappingWeights.Full;
            MuscleRuntimeState state = MuscleRuntimeState.Default;

            state.SetMappingAuthorities(0.25f, 2f);
            state.ApplyTo(ref weights);

            Assert.That(state.PositionMappingAuthority, Is.EqualTo(0.25f));
            Assert.That(state.RotationMappingAuthority, Is.EqualTo(1f));
            Assert.That(weights.PositionWeight, Is.EqualTo(0.25f));
            Assert.That(weights.RotationWeight, Is.EqualTo(1f));
        }

        [Test]
        public void Suppression_AccumulatesWithoutHealingPreviousImpact()
        {
            MuscleRuntimeState state = MuscleRuntimeState.Default;

            state.AccumulateSuppression(0.5f, 0.5f);
            state.AccumulateSuppression(0.5f, 0.5f);

            Assert.That(state.PositionSuppression, Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(state.RotationSuppression, Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(state.EffectivePositionAuthority, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(state.EffectiveRotationAuthority, Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        public void Recovery_IsIndependentPerAuthorityChannel()
        {
            MuscleRuntimeState state = MuscleRuntimeState.Default;
            state.AccumulateSuppression(0.8f, 0.8f);

            state.Recover(0.5f, 0.25f, 1f);

            Assert.That(state.PositionSuppression, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(state.RotationSuppression, Is.EqualTo(0.55f).Within(0.0001f));
        }

        [Test]
        public void ZeroAccelerationMultiplier_DoesNotProduceNaNForInfiniteLimit()
        {
            BoneProfile profile = CreateProfile();
            profile.maxLinearAcceleration = float.PositiveInfinity;
            profile.maxAngularAcceleration = float.PositiveInfinity;

            MuscleRuntimeState state = MuscleRuntimeState.Default;
            state.SetDriveMultipliers(1f, 1f, 0f, 0f);
            state.ApplyTo(ref profile);

            Assert.That(profile.maxLinearAcceleration, Is.EqualTo(0f));
            Assert.That(profile.maxAngularAcceleration, Is.EqualTo(0f));
            Assert.That(float.IsNaN(profile.maxLinearAcceleration), Is.False);
            Assert.That(float.IsNaN(profile.maxAngularAcceleration), Is.False);
        }

        [Test]
        public void ImpactFalloff_UsesTopologicalDistance()
        {
            MuscleImpactSettings settings = new MuscleImpactSettings
            {
                maximumPropagationDistance = 3,
                propagationFalloff = 0.5f
            };

            Assert.That(settings.GetPropagationWeight(0), Is.EqualTo(1f));
            Assert.That(settings.GetPropagationWeight(1), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(settings.GetPropagationWeight(2), Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(settings.GetPropagationWeight(4), Is.EqualTo(0f));
            Assert.That(settings.GetPropagationWeight(-1), Is.EqualTo(0f));
        }

        static BoneProfile CreateProfile()
        {
            return new BoneProfile
            {
                positionAlpha = 0.8f,
                positionDampingRatio = 0.7f,
                maxLinearAcceleration = 12f,
                rotationAlpha = 0.6f,
                rotationDampingRatio = 0.5f,
                maxAngularAcceleration = 20f
            };
        }
    }
}
