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
    }
}
