using NUnit.Framework;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class RagdollMuscleBehaviourSettingsTests
    {
        [Test]
        public void DefaultSettings_PreserveLegacyPropagationAndMapping()
        {
            RagdollMuscleBehaviourSettings settings =
                RagdollMuscleBehaviourSettings.Default;

            Assert.That(
                settings.GetPropagationMultiplier(RagdollMuscleRelation.Parent),
                Is.EqualTo(1f));
            Assert.That(
                settings.GetPropagationMultiplier(RagdollMuscleRelation.Child),
                Is.EqualTo(1f));
            Assert.That(settings.EvaluateMappingAuthority(0f), Is.EqualTo(1f));
            Assert.That(settings.EvaluateMappingAuthority(1f), Is.EqualTo(1f));
            Assert.That(settings.ScaleCollisionSuppression(0.6f), Is.EqualTo(0.6f));
        }

        [Test]
        public void CollisionResistance_SmallerValuesIncreaseUnpinning()
        {
            RagdollMuscleBehaviourSettings settings =
                RagdollMuscleBehaviourSettings.Default;
            settings.collisionResistance = 0.5f;

            Assert.That(
                settings.ScaleCollisionSuppression(0.4f),
                Is.EqualTo(0.8f).Within(0.0001f));

            settings.collisionResistance = 2f;
            Assert.That(
                settings.ScaleCollisionSuppression(0.4f),
                Is.EqualTo(0.2f).Within(0.0001f));
        }

        [Test]
        public void MappingAuthority_BlendsBetweenConfiguredLimits()
        {
            RagdollMuscleBehaviourSettings settings =
                RagdollMuscleBehaviourSettings.Default;
            settings.minimumMappingAuthority = 0.2f;
            settings.maximumMappingAuthority = 0.8f;

            Assert.That(settings.EvaluateMappingAuthority(0f), Is.EqualTo(0.2f));
            Assert.That(settings.EvaluateMappingAuthority(0.5f), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(settings.EvaluateMappingAuthority(1f), Is.EqualTo(0.8f));
        }

        [Test]
        public void UnrelatedMuscles_DoNotReceiveSemanticUnpinning()
        {
            RagdollMuscleBehaviourSettings settings =
                RagdollMuscleBehaviourSettings.Default;

            Assert.That(
                settings.GetPropagationMultiplier(RagdollMuscleRelation.Unrelated),
                Is.EqualTo(0f));
        }

        [Test]
        public void MinimumPositionAuthority_OnlyLimitsSuppressionChannel()
        {
            BoneProfile profile = new BoneProfile
            {
                positionAlpha = 1f,
                rotationAlpha = 1f,
                positionDampingRatio = 1f,
                rotationDampingRatio = 1f,
                maxLinearAcceleration = 1f,
                maxAngularAcceleration = 1f
            };

            MuscleRuntimeState state = MuscleRuntimeState.Default;
            state.SetAuthorities(0.5f, 1f);
            state.AccumulateSuppression(1f, 0f);
            state.ApplyTo(ref profile, 0.4f);

            Assert.That(profile.positionAlpha, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(profile.rotationAlpha, Is.EqualTo(1f));
        }
    }
}
