using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Pure calculations for temporary BehaviourPuppet combat boosts.</summary>
    internal static class RagdollMuscleBoostMath
    {
        internal static float ApplyImmunity(float suppression, float immunity)
        {
            return SanitizeUnit(suppression)
                * (1f - SanitizeUnit(immunity));
        }

        internal static float ApplyImpulseMultiplier(
            float impulseMagnitude,
            float impulseMultiplier)
        {
            float impulse = SanitizeNonNegative(impulseMagnitude);
            float multiplier = SanitizeImpulseMultiplier(impulseMultiplier);
            double scaled = impulse * (double) multiplier;
            return scaled >= float.MaxValue ? float.MaxValue : (float) scaled;
        }

        internal static float StepImmunity(
            float immunity,
            float falloff,
            float deltaTime)
        {
            return Mathf.MoveTowards(
                SanitizeUnit(immunity),
                0f,
                SanitizeFalloff(falloff) * SanitizeDeltaTime(deltaTime));
        }

        internal static float StepImpulseMultiplier(
            float impulseMultiplier,
            float falloff,
            float deltaTime)
        {
            float current = SanitizeImpulseMultiplier(impulseMultiplier);
            float t = Mathf.Clamp01(
                SanitizeFalloff(falloff) * SanitizeDeltaTime(deltaTime));
            float stepped = Mathf.Lerp(current, 1f, t);
            return Mathf.Abs(stepped - 1f) <= 0.0001f ? 1f : stepped;
        }

        internal static float EvaluateDirectionalFalloff(
            RagdollBoneTopology topology,
            RagdollBoneHandle source,
            RagdollBoneHandle affected,
            float boostParents,
            float boostChildren)
        {
            if (topology == null
                || !topology.Contains(source)
                || !topology.Contains(affected))
            {
                return 0f;
            }

            if (source == affected) return 1f;

            int distance = topology.GetKinshipDistance(source, affected);
            if (distance <= 0) return 0f;

            if (topology.IsAncestorOf(affected, source))
            {
                return Mathf.Pow(SanitizeUnit(boostParents), distance);
            }

            if (topology.IsAncestorOf(source, affected))
            {
                return Mathf.Pow(SanitizeUnit(boostChildren), distance);
            }

            return 0f;
        }

        internal static float SanitizeImpulseMultiplier(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 1f
                : Mathf.Max(1f, value);
        }

        internal static float SanitizeFalloff(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : Mathf.Max(0f, value);
        }

        static float SanitizeUnit(float value)
        {
            if (float.IsNaN(value) || value <= 0f) return 0f;
            if (float.IsInfinity(value) || value >= 1f) return 1f;
            return value;
        }

        static float SanitizeNonNegative(float value)
        {
            if (float.IsNaN(value) || value <= 0f) return 0f;
            return float.IsInfinity(value) ? float.MaxValue : value;
        }

        static float SanitizeDeltaTime(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : Mathf.Max(0f, value);
        }
    }
}
