using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>One collider plus the exact baseline captured before BehaviourPuppet owns it.</summary>
    internal sealed class RagdollPuppetColliderSurfaceBinding
    {
        readonly Collider collider;
        readonly RagdollBoneHandle bone;
        bool baselineEnabled;
        PhysicMaterial baselineMaterial;
        bool hasBaseline;

        internal Collider Collider => collider;
        internal RagdollBoneHandle Bone => bone;
        internal bool HasBaseline => hasBaseline;
        internal bool BaselineEnabled => baselineEnabled;
        internal PhysicMaterial BaselineMaterial => baselineMaterial;

        internal RagdollPuppetColliderSurfaceBinding(
            Collider collider,
            RagdollBoneHandle bone)
        {
            if (!collider) throw new ArgumentNullException(nameof(collider));

            this.collider = collider;
            this.bone = bone;
        }

        internal void CaptureBaseline()
        {
            if (!collider)
            {
                hasBaseline = false;
                return;
            }

            baselineEnabled = collider.enabled;
            baselineMaterial = collider.sharedMaterial;
            hasBaseline = true;
        }

        internal void Apply(RagdollPuppetColliderSurfacePlan plan)
        {
            if (!hasBaseline || !collider) return;

            if (collider.enabled != plan.Enabled)
            {
                collider.enabled = plan.Enabled;
            }

            if (collider.sharedMaterial != plan.Material)
            {
                collider.sharedMaterial = plan.Material;
            }
        }

        internal void RestoreBaseline()
        {
            if (!hasBaseline) return;

            if (collider)
            {
                collider.enabled = baselineEnabled;
                collider.sharedMaterial = baselineMaterial;
            }

            hasBaseline = false;
        }
    }
}
