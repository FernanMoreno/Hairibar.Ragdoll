using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Multipliers produced by one puppet state. They compose with authored profiles and
    /// runtime muscle state instead of replacing either data source.
    /// </summary>
    internal struct RagdollPuppetStateWeights
    {
        internal float PositionAuthority;
        internal float RotationAuthority;
        internal float MaximumMappingBlend;

        internal static RagdollPuppetStateWeights Evaluate(
            RagdollPuppetState state,
            float getUpProgress,
            float unpinnedMuscleWeightMultiplier)
        {
            float muscleWeight = Mathf.Clamp01(unpinnedMuscleWeightMultiplier);
            float progress = Mathf.Clamp01(getUpProgress);

            switch (state)
            {
                case RagdollPuppetState.Puppet:
                    return new RagdollPuppetStateWeights
                    {
                        PositionAuthority = 1f,
                        RotationAuthority = 1f,
                        MaximumMappingBlend = 0f
                    };

                case RagdollPuppetState.Unpinned:
                    return new RagdollPuppetStateWeights
                    {
                        PositionAuthority = 0f,
                        RotationAuthority = muscleWeight,
                        MaximumMappingBlend = 1f
                    };

                case RagdollPuppetState.GetUp:
                    return new RagdollPuppetStateWeights
                    {
                        PositionAuthority = progress,
                        RotationAuthority = Mathf.Lerp(muscleWeight, 1f, progress),
                        MaximumMappingBlend = 1f - progress
                    };

                default:
                    return new RagdollPuppetStateWeights
                    {
                        PositionAuthority = 1f,
                        RotationAuthority = 1f,
                        MaximumMappingBlend = 0f
                    };
            }
        }
    }
}
