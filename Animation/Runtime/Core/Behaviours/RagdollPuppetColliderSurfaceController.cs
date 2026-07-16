using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Owns temporary BehaviourPuppet collider enables and shared materials. Baselines are
    /// captured on activation and restored exactly on deactivation.
    /// </summary>
    internal sealed class RagdollPuppetColliderSurfaceController
    {
        readonly RagdollMuscleController muscles;
        readonly RagdollPuppetColliderSurfaceBinding[] bindings;
        readonly RagdollPuppetColliderSurfacePlan[] plans;
        bool baselineCaptured;
        bool hasAppliedState;
        RagdollPuppetColliderSurfaceState currentState;
        int disabledColliderCount;
        int materialOverrideCount;

        internal int ColliderCount => bindings.Length;
        internal bool BaselineCaptured => baselineCaptured;
        internal bool HasAppliedState => hasAppliedState;
        internal RagdollPuppetColliderSurfaceState CurrentState => currentState;
        internal int DisabledColliderCount => disabledColliderCount;
        internal int MaterialOverrideCount => materialOverrideCount;

        internal RagdollPuppetColliderSurfaceController(
            RagdollDefinitionBindings definitionBindings,
            RagdollMuscleController muscles)
        {
            if (!definitionBindings)
            {
                throw new ArgumentNullException(nameof(definitionBindings));
            }
            if (muscles == null)
            {
                throw new ArgumentNullException(nameof(muscles));
            }

            this.muscles = muscles;
            List<RagdollPuppetColliderSurfaceBinding> resolved =
                new List<RagdollPuppetColliderSurfaceBinding>();
            HashSet<Collider> seen = new HashSet<Collider>();

            for (int boneIndex = 0;
                boneIndex < definitionBindings.BoneCount;
                boneIndex++)
            {
                RagdollBone bone = definitionBindings.GetBoneAt(boneIndex);
                RagdollBoneHandle handle =
                    definitionBindings.GetHandleAt(boneIndex);
                foreach (Collider collider in bone.Colliders)
                {
                    if (!collider || !seen.Add(collider)) continue;
                    resolved.Add(
                        new RagdollPuppetColliderSurfaceBinding(
                            collider,
                            handle));
                }
            }

            bindings = resolved.ToArray();
            plans = new RagdollPuppetColliderSurfacePlan[bindings.Length];
        }

        internal void CaptureBaseline()
        {
            if (baselineCaptured)
            {
                Restore();
            }

            for (int index = 0; index < bindings.Length; index++)
            {
                bindings[index].CaptureBaseline();
            }

            baselineCaptured = true;
            hasAppliedState = false;
            disabledColliderCount = 0;
            materialOverrideCount = 0;
        }

        internal bool Apply(RagdollPuppetState state, bool force)
        {
            if (!baselineCaptured) return false;

            RagdollPuppetColliderSurfaceState surfaceState =
                RagdollPuppetColliderSurfacePolicy.ResolveState(state);
            if (!force && hasAppliedState && currentState == surfaceState)
            {
                return false;
            }

            int disabled = 0;
            int overridden = 0;
            for (int index = 0; index < bindings.Length; index++)
            {
                RagdollPuppetColliderSurfaceBinding binding = bindings[index];
                if (!binding.HasBaseline || !binding.Collider) continue;

                RagdollMuscleBehaviourSettings settings =
                    muscles.GetBehaviourSettings(binding.Bone);
                plans[index] = RagdollPuppetColliderSurfacePolicy.Resolve(
                    surfaceState,
                    binding.BaselineEnabled,
                    settings.disableColliders,
                    binding.BaselineMaterial,
                    settings.puppetMaterial,
                    settings.unpinnedMaterial);
                if (plans[index].DisabledByBehaviour) disabled++;
                if (plans[index].MaterialOverridden) overridden++;
            }

            try
            {
                for (int index = 0; index < bindings.Length; index++)
                {
                    bindings[index].Apply(plans[index]);
                }
            }
            catch
            {
                Restore();
                throw;
            }

            currentState = surfaceState;
            hasAppliedState = true;
            disabledColliderCount = disabled;
            materialOverrideCount = overridden;
            return true;
        }

        internal void Restore()
        {
            for (int index = 0; index < bindings.Length; index++)
            {
                bindings[index].RestoreBaseline();
            }

            baselineCaptured = false;
            hasAppliedState = false;
            disabledColliderCount = 0;
            materialOverrideCount = 0;
        }
    }
}
