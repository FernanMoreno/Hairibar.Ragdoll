using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Pure collision-resistance and layer-policy calculations.</summary>
    internal static class RagdollPuppetCollisionResponseMath
    {
        internal struct LayerResolution
        {
            internal float ResistanceMultiplier;
            internal float CollisionThreshold;
            internal int RuleIndex;
        }

        internal static LayerResolution ResolveLayer(
            IReadOnlyList<RagdollPuppetCollisionLayerRule> rules,
            int layer,
            float defaultCollisionThreshold)
        {
            LayerResolution resolution = new LayerResolution
            {
                ResistanceMultiplier = 1f,
                CollisionThreshold = SanitizeNonNegative(defaultCollisionThreshold),
                RuleIndex = -1
            };

            if (layer < 0 || layer >= 32 || rules == null)
            {
                return resolution;
            }

            for (int index = 0; index < rules.Count; index++)
            {
                RagdollPuppetCollisionLayerRule rule = rules[index];
                if (rule == null || !rule.Matches(layer)) continue;

                resolution.ResistanceMultiplier = SanitizePositive(
                    rule.resistanceMultiplier,
                    1f);
                resolution.CollisionThreshold = rule.overrideCollisionThreshold
                    ? SanitizeNonNegative(rule.collisionThreshold)
                    : resolution.CollisionThreshold;
                resolution.RuleIndex = index;
                return resolution;
            }

            return resolution;
        }

        internal static float EvaluateGlobalResistance(
            float constantResistance,
            bool useTargetSpeedCurve,
            AnimationCurve targetSpeedResistance,
            float targetSpeed)
        {
            float fallback = SanitizePositive(constantResistance, 3f);
            if (!useTargetSpeedCurve
                || targetSpeedResistance == null
                || targetSpeedResistance.length == 0)
            {
                return fallback;
            }

            float speed = SanitizeNonNegative(targetSpeed);
            float evaluated = targetSpeedResistance.Evaluate(speed);
            return SanitizePositive(evaluated, fallback);
        }

        internal static float EvaluateEffectiveResistance(
            float globalResistance,
            float layerResistanceMultiplier,
            float muscleResistanceMultiplier)
        {
            return EvaluateEffectiveResistance(
                globalResistance,
                layerResistanceMultiplier,
                muscleResistanceMultiplier,
                1f);
        }

        internal static float EvaluateEffectiveResistance(
            float globalResistance,
            float layerResistanceMultiplier,
            float muscleResistanceMultiplier,
            float stateResistanceMultiplier)
        {
            double effective =
                SanitizePositive(globalResistance, 3f)
                * (double) SanitizePositive(layerResistanceMultiplier, 1f)
                * SanitizePositive(muscleResistanceMultiplier, 1f)
                * SanitizeNonNegative(stateResistanceMultiplier);

            if (double.IsNaN(effective) || effective <= 0d)
            {
                return 0f;
            }

            return effective >= float.MaxValue
                ? float.MaxValue
                : (float) effective;
        }

        internal static float EvaluatePositionSuppression(
            float impulseMagnitude,
            float globalResistance,
            float layerResistanceMultiplier,
            float muscleResistanceMultiplier)
        {
            return EvaluatePositionSuppression(
                impulseMagnitude,
                globalResistance,
                layerResistanceMultiplier,
                muscleResistanceMultiplier,
                1f);
        }

        internal static float EvaluatePositionSuppression(
            float impulseMagnitude,
            float globalResistance,
            float layerResistanceMultiplier,
            float muscleResistanceMultiplier,
            float stateResistanceMultiplier)
        {
            float impulse = SanitizeNonNegative(impulseMagnitude);
            if (impulse <= 0f) return 0f;

            float effectiveResistance = EvaluateEffectiveResistance(
                globalResistance,
                layerResistanceMultiplier,
                muscleResistanceMultiplier,
                stateResistanceMultiplier);
            return effectiveResistance <= 0f
                ? 1f
                : Mathf.Clamp01(impulse / effectiveResistance);
        }

        internal static float SanitizeNonNegative(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : Mathf.Max(0f, value);
        }

        internal static float SanitizePositive(float value, float fallback)
        {
            float safeFallback = float.IsNaN(fallback)
                || float.IsInfinity(fallback)
                || fallback <= 0f
                ? 0.001f
                : Mathf.Max(0.001f, fallback);

            return float.IsNaN(value)
                || float.IsInfinity(value)
                || value <= 0f
                ? safeFallback
                : Mathf.Max(0.001f, value);
        }
    }
}
