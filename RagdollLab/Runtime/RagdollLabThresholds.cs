using UnityEngine;

namespace Hairibar.Ragdoll.RagdollLab
{
    [CreateAssetMenu(fileName = "RagdollLabThresholds", menuName = "Hairibar/Ragdoll/RagdollLab Thresholds")]
    public sealed class RagdollLabThresholds : ScriptableObject
    {
        public float anchorErrorWarningMeters = 0.02f;
        public float penetrationWarningMeters = 0.01f;
        public float torqueSpikeWarning = 500f;
        public float oscillationWarningHz = 3f;
        public int oscillationWarningCrossings = 8;
        public float energySpikeRatio = 5f;
        public float trackingWarningDegrees = 20f;
        public float footSlipWarningMetersPerSecond = 0.15f;
        [Min(0f)] public float shortContactDurationSeconds = 0.1f;
        [Min(0f)] public float supportRadiusMeters = 0.15f;
        [Range(0f, 90f)] public float maximumGroundAngle = 45f;
        [Min(0f)] public float fallHeightMeters = 0.35f;
        [Range(0f, 1f)] public float comparisonSafetyToleranceRatio = 0.1f;
        [Min(0f)] public float comparisonSafetyToleranceAbsolute = 0.001f;
        [Range(0f, 1f)] public float comparisonImprovementToleranceRatio = 0.01f;
        [Min(0f)] public float balancerTorqueWarning = 45f;
        [Min(0f)] public float staggerEpisodeTimeoutSeconds = 2f;
        [Min(0f)] public float supportInstabilityMarginMeters = 0.05f;
        [Min(0f)] public float recoveryTooSlowSeconds = 1.5f;
        [Min(0f)] public float recoveryOvershootMeters = 0.05f;
        [Min(0f)] public float requiresStepEarlySeconds = 0.1f;
    }
}
