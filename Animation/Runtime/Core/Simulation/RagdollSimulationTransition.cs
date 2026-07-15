using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Small deterministic scalar transition shared by runtime and tests.</summary>
    internal struct RagdollSimulationTransition
    {
        readonly float startValue;
        readonly float endValue;
        readonly float duration;
        float elapsed;

        internal float Value { get; private set; }
        internal float Progress => duration <= Mathf.Epsilon
            ? 1f
            : Mathf.Clamp01(elapsed / duration);
        internal bool IsComplete => Progress >= 1f;

        internal RagdollSimulationTransition(
            float startValue,
            float endValue,
            float duration)
        {
            this.startValue = Mathf.Clamp01(startValue);
            this.endValue = Mathf.Clamp01(endValue);
            this.duration = Mathf.Max(0f, duration);
            elapsed = 0f;
            Value = this.duration <= Mathf.Epsilon
                ? this.endValue
                : this.startValue;
        }

        internal bool Advance(float deltaTime)
        {
            if (IsComplete)
            {
                Value = endValue;
                return true;
            }

            elapsed = Mathf.Min(duration, elapsed + Mathf.Max(0f, deltaTime));
            Value = Mathf.Lerp(startValue, endValue, Progress);
            return IsComplete;
        }

        internal static float ScaleRemainingDuration(
            float fullDuration,
            float startValue,
            float endValue)
        {
            return Mathf.Max(0f, fullDuration)
                * Mathf.Abs(Mathf.Clamp01(endValue) - Mathf.Clamp01(startValue));
        }
    }
}
