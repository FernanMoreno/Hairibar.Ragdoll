namespace Hairibar.Ragdoll.Animation
{
    internal static class RagdollPhysicsQualityReconciliation
    {
        internal static bool RequiresReapply(
            bool lifecycleOwnsSimulation,
            RagdollSimulationMode currentTargetMode,
            RagdollSimulationMode expectedMode,
            bool hasRuntimeSolverOverride,
            bool useAuthoredSolverSettings)
        {
            if (lifecycleOwnsSimulation) return false;

            bool solverOwnershipMismatch = useAuthoredSolverSettings
                ? hasRuntimeSolverOverride
                : !hasRuntimeSolverOverride;
            return currentTargetMode != expectedMode
                || solverOwnershipMismatch;
        }
    }
}
