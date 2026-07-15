namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Immutable counters for the most recently observed physics timestamp.</summary>
    public struct RagdollPuppetCollisionStepSnapshot
    {
        public bool HasStep { get; private set; }
        public float FixedTime { get; private set; }
        public int ReportedCount { get; private set; }
        public int AcceptedCount { get; private set; }
        public int RejectedPhaseCount { get; private set; }
        public int RejectedLayerCount { get; private set; }
        public int RejectedThresholdCount { get; private set; }
        public int RejectedBudgetCount { get; private set; }

        public int RejectedCount => RejectedPhaseCount
            + RejectedLayerCount
            + RejectedThresholdCount
            + RejectedBudgetCount;

        internal RagdollPuppetCollisionStepSnapshot(
            bool hasStep,
            float fixedTime,
            int reportedCount,
            int acceptedCount,
            int rejectedPhaseCount,
            int rejectedLayerCount,
            int rejectedThresholdCount,
            int rejectedBudgetCount)
        {
            HasStep = hasStep;
            FixedTime = fixedTime;
            ReportedCount = reportedCount;
            AcceptedCount = acceptedCount;
            RejectedPhaseCount = rejectedPhaseCount;
            RejectedLayerCount = rejectedLayerCount;
            RejectedThresholdCount = rejectedThresholdCount;
            RejectedBudgetCount = rejectedBudgetCount;
        }
    }
}
