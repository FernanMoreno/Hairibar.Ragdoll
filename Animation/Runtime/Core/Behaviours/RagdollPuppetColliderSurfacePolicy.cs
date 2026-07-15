using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    internal struct RagdollPuppetColliderSurfacePlan
    {
        internal readonly bool Enabled;
        internal readonly PhysicMaterial Material;
        internal readonly bool DisabledByBehaviour;
        internal readonly bool MaterialOverridden;

        internal RagdollPuppetColliderSurfacePlan(
            bool enabled,
            PhysicMaterial material,
            bool disabledByBehaviour,
            bool materialOverridden)
        {
            Enabled = enabled;
            Material = material;
            DisabledByBehaviour = disabledByBehaviour;
            MaterialOverridden = materialOverridden;
        }
    }

    /// <summary>Pure state-to-collider policy used by the transactional surface controller.</summary>
    internal static class RagdollPuppetColliderSurfacePolicy
    {
        internal static RagdollPuppetColliderSurfaceState ResolveState(
            RagdollPuppetState state)
        {
            switch (state)
            {
                case RagdollPuppetState.Unpinned:
                    return RagdollPuppetColliderSurfaceState.Unpinned;
                case RagdollPuppetState.GetUp:
                    return RagdollPuppetColliderSurfaceState.GetUp;
                default:
                    return RagdollPuppetColliderSurfaceState.Puppet;
            }
        }

        internal static RagdollPuppetColliderSurfacePlan Resolve(
            RagdollPuppetColliderSurfaceState state,
            bool baselineEnabled,
            bool disableCollidersInPuppet,
            PhysicMaterial baselineMaterial,
            PhysicMaterial puppetMaterial,
            PhysicMaterial unpinnedMaterial)
        {
            bool disabledByBehaviour = state == RagdollPuppetColliderSurfaceState.Puppet
                && disableCollidersInPuppet
                && baselineEnabled;
            bool enabled = baselineEnabled && !disabledByBehaviour;

            PhysicMaterial requestedMaterial =
                state == RagdollPuppetColliderSurfaceState.Unpinned
                    ? unpinnedMaterial
                    : puppetMaterial;
            PhysicMaterial material = requestedMaterial
                ? requestedMaterial
                : baselineMaterial;

            return new RagdollPuppetColliderSurfacePlan(
                enabled,
                material,
                disabledByBehaviour,
                requestedMaterial && requestedMaterial != baselineMaterial);
        }
    }
}
