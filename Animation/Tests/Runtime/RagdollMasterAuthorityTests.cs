using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollMasterAuthorityTests
    {
        [TestCase(0f, 0f, 0f, 0f)]
        [TestCase(0f, 1f, 0f, 8f)]
        [TestCase(1f, 0f, 1f, 0f)]
        [TestCase(1f, 1f, 1f, 8f)]
        public void PinAndMuscleWeightsAreIndependent(
            float pin,
            float muscle,
            float expectedPin,
            float expectedRotationAlpha)
        {
            BoneProfile profile = new BoneProfile
            {
                positionAlpha = 4f,
                positionDampingRatio = 2f,
                rotationAlpha = 8f,
                rotationDampingRatio = 3f
            };

            RagdollMasterAuthority.Apply(
                ref profile,
                pin,
                muscle,
                0.5f,
                2f);

            Assert.That(profile.PositionPinWeight, Is.EqualTo(expectedPin));
            Assert.That(profile.positionAlpha, Is.EqualTo(4f));
            Assert.That(profile.rotationAlpha, Is.EqualTo(expectedRotationAlpha));
            Assert.That(profile.positionDampingRatio, Is.EqualTo(1f));
            Assert.That(profile.rotationDampingRatio, Is.EqualTo(6f));
        }

        [Test]
        public void InvalidMasterValuesUseSafeNeutralFallbacks()
        {
            BoneProfile profile = new BoneProfile
            {
                positionAlpha = 4f,
                positionDampingRatio = 2f,
                rotationAlpha = 8f,
                rotationDampingRatio = 3f
            };

            RagdollMasterAuthority.Apply(
                ref profile,
                float.NaN,
                float.PositiveInfinity,
                float.NaN,
                -2f);

            Assert.That(profile.PositionPinWeight, Is.EqualTo(1f));
            Assert.That(profile.rotationAlpha, Is.EqualTo(8f));
            Assert.That(profile.positionDampingRatio, Is.EqualTo(2f));
            Assert.That(profile.rotationDampingRatio, Is.Zero);
        }

        [Test]
        public void RotationDamperComposesBaseMultiplierAndAbsoluteChannels()
        {
            JointDrive baseDrive = AnimationMatching.GetRotationMatchingJointDrive(
                4f, 2f, 3f, 0.02f, 100f);
            JointDrive composed = AnimationMatching.GetRotationMatchingJointDrive(
                4f, 2f * 3f, 3f, 0.02f, 100f);
            RagdollMasterAuthority.ApplyAbsoluteMuscleDamper(
                ref composed, 7f);

            Assert.That(composed.positionDamper,
                Is.EqualTo(baseDrive.positionDamper * 3f + 7f)
                    .Within(0.0001f));
        }

        [TestCase(0, 0.4f, 2.5f)]
        [TestCase(1, 0.7f, 3.5f)]
        public void LegacySerializationMigratesDamperMultiplierWithoutAddingAbsoluteDamper(
            int version,
            float legacyWeight,
            float legacyDamper)
        {
            GameObject owner = new GameObject("authority-migration");
            owner.SetActive(false);
            try
            {
                RagdollAnimator animator = owner.AddComponent<RagdollAnimator>();
                SetField(animator, "masterAuthoritySerializationVersion", version);
                SetField(animator, "_masterAlpha", legacyWeight);
                SetField(animator, "_masterDampingRatio", legacyDamper);
                if (version == 1)
                    SetField(animator, "_masterMuscleDamper", legacyDamper);

                animator.OnAfterDeserialize();

                Assert.That(animator.MasterMuscleDamper, Is.Zero);
                Assert.That(animator.MasterMuscleDamperMultiplier,
                    Is.EqualTo(legacyDamper));
                if (version == 0)
                {
                    Assert.That(animator.MasterPinWeight,
                        Is.EqualTo(legacyWeight));
                    Assert.That(animator.MasterMuscleWeight,
                        Is.EqualTo(legacyWeight));
                }
                Assert.That(GetField<int>(animator,
                    "masterAuthoritySerializationVersion"), Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        static T GetField<T>(object target, string name)
        {
            return (T)target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);
        }
    }
}
