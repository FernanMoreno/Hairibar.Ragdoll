using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Applies the independent PuppetMaster-style global pin and muscle channels to a
    /// resolved bone profile. Kept pure so every 0/1 authority combination is testable.
    /// </summary>
    internal static class RagdollMasterAuthority
    {
        internal static void Apply(
            ref BoneProfile profile,
            float pinWeight,
            float muscleWeight,
            float pinDampingMultiplier,
            float muscleDampingMultiplier)
        {
            pinWeight = SanitizeUnit(pinWeight);
            muscleWeight = SanitizeUnit(muscleWeight);
            pinDampingMultiplier = SanitizeNonNegative(
                pinDampingMultiplier,
                1f);
            muscleDampingMultiplier = SanitizeNonNegative(
                muscleDampingMultiplier,
                1f);

            profile.MultiplyPositionPinWeight(pinWeight);
            profile.positionDampingRatio *= pinDampingMultiplier;
            profile.rotationAlpha *= muscleWeight;
            profile.rotationDampingRatio *= muscleDampingMultiplier;
        }

        internal static void ApplyAbsoluteMuscleDamper(
            ref JointDrive drive,
            float muscleDamper)
        {
            drive.positionDamper += SanitizeNonNegative(muscleDamper, 0f);
        }

        static float SanitizeUnit(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 1f;
            return Mathf.Clamp01(value);
        }

        static float SanitizeNonNegative(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return fallback;
            return Mathf.Max(0f, value);
        }
    }
}
