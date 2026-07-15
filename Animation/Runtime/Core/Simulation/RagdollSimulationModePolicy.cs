namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Pure mode policy used by the controller and runtime tests.</summary>
    internal static class RagdollSimulationModePolicy
    {
        internal static bool KeepsPuppetHierarchyActive(
            RagdollSimulationMode mode)
        {
            return mode != RagdollSimulationMode.Disabled;
        }

        internal static bool OverridesAllBonesToKinematic(
            RagdollSimulationMode mode)
        {
            return mode != RagdollSimulationMode.Active;
        }

        internal static bool KeepsCollisionConfiguration(
            RagdollSimulationMode mode)
        {
            return mode != RagdollSimulationMode.Disabled;
        }

        internal static float StableDriveWeight(RagdollSimulationMode mode)
        {
            return mode == RagdollSimulationMode.Active ? 1f : 0f;
        }
    }
}
