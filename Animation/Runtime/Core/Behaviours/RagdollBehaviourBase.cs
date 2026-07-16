using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Base class for modular ragdoll behaviours. A behaviour receives all runtime
    /// dependencies through RagdollBehaviourContext and is executed only while active.
    /// </summary>
    public abstract class RagdollBehaviourBase : MonoBehaviour
    {
        RagdollBehaviourContext context;
        bool isInitialized;

        public bool IsInitialized => isInitialized;
        public bool IsActive { get; private set; }
        public RagdollBehaviourController Controller =>
            context != null ? context.Controller : null;

        protected RagdollBehaviourContext Context
        {
            get
            {
                if (context == null)
                {
                    throw new InvalidOperationException(
                        "The ragdoll behaviour has not been initialized by a controller.");
                }

                return context;
            }
        }

        /// <summary>
        /// Switches the owning controller to this behaviour. All sibling behaviours in
        /// that controller are disabled by the controller.
        /// </summary>
        public bool Activate()
        {
            return Controller != null && Controller.Activate(this);
        }

        /// <summary>
        /// Deactivates this behaviour when it is currently selected by its controller.
        /// </summary>
        public bool Deactivate()
        {
            return Controller != null && Controller.Deactivate(this);
        }

        internal void InitializeInternal(RagdollBehaviourContext assignedContext)
        {
            if (assignedContext == null)
            {
                throw new ArgumentNullException(nameof(assignedContext));
            }

            if (context != null)
            {
                if (ReferenceEquals(context, assignedContext)) return;

                throw new InvalidOperationException(
                    "A ragdoll behaviour cannot be owned by more than one controller.");
            }

            context = assignedContext;
            isInitialized = true;
            try
            {
                OnBehaviourInitialize();
            }
            catch
            {
                context = null;
                isInitialized = false;
                throw;
            }
        }


        internal void RebindContextInternal(
            RagdollBehaviourContext assignedContext)
        {
            if (assignedContext == null)
            {
                throw new ArgumentNullException(nameof(assignedContext));
            }
            if (!IsInitialized)
            {
                throw new InvalidOperationException(
                    "A behaviour must be initialized before rebinding its hierarchy context.");
            }
            context = assignedContext;
        }

        internal void HierarchyChangedInternal(
            IReadOnlyList<RagdollMuscleChange> added,
            IReadOnlyList<RagdollMuscleChange> removed)
        {
            if (!IsInitialized) return;
            OnBehaviourHierarchyChanged(added, removed);
        }

        internal void ShutdownInternal()
        {
            if (!IsInitialized) return;

            SetActiveInternal(false);
            try
            {
                OnBehaviourShutdown();
            }
            finally
            {
                context = null;
                isInitialized = false;
            }
        }

        internal void SetActiveInternal(bool active)
        {
            if (IsActive == active) return;

            IsActive = active;
            if (active)
            {
                OnBehaviourActivated();
            }
            else
            {
                OnBehaviourDeactivated();
            }
        }

        internal void ReactivateInternal()
        {
            if (!IsInitialized) return;
            OnBehaviourReactivated();
        }

        internal void KillStartedInternal()
        {
            if (!IsInitialized) return;
            OnBehaviourKillStarted();
        }

        internal void KillEndedInternal()
        {
            if (!IsInitialized) return;
            OnBehaviourKillEnded();
        }

        internal void ResurrectedInternal()
        {
            if (!IsInitialized) return;
            OnBehaviourResurrected();
        }

        internal void FrozenInternal()
        {
            if (!IsInitialized) return;
            OnBehaviourFrozen();
        }

        internal void UnfrozenInternal()
        {
            if (!IsInitialized) return;
            OnBehaviourUnfrozen();
        }

        internal void TeleportInternal(
            Quaternion deltaRotation,
            Vector3 deltaPosition,
            Vector3 pivot,
            bool moveToTarget)
        {
            if (!IsInitialized) return;
            OnBehaviourTeleported(
                deltaRotation,
                deltaPosition,
                pivot,
                moveToTarget);
        }

        internal void FixTransformsInternal()
        {
            if (!IsInitialized) return;
            OnBehaviourFixTransforms();
        }

        internal void ReadInternal()
        {
            if (!IsInitialized) return;
            OnBehaviourRead();
        }

        internal void WriteInternal()
        {
            if (!IsInitialized) return;
            OnBehaviourWrite();
        }

        internal void FixedUpdateInternal(float deltaTime)
        {
            OnBehaviourFixedUpdate(deltaTime);
        }

        internal void CollisionInternal(RagdollCollisionEvent collisionEvent)
        {
            OnBehaviourCollision(collisionEvent);
        }

        internal float GetLifecycleMuscleWeightInternal(
            RagdollAnimator.AnimatedPair pair)
        {
            if (!IsInitialized) return 1f;
            return Mathf.Clamp01(OnGetLifecycleMuscleWeight(pair));
        }

        internal void ModifyBoneProfileInternal(
            ref BoneProfile boneProfile,
            RagdollAnimator.AnimatedPair pair,
            float deltaTime)
        {
            OnModifyBoneProfile(ref boneProfile, pair, deltaTime);
        }

        internal void ModifyMappingInternal(
            ref RagdollMappingWeights mappingWeights,
            RagdollAnimator.AnimatedPair pair)
        {
            OnModifyMapping(ref mappingWeights, pair);
        }

        internal void ModifyTargetPoseInternal(
            IReadOnlyList<RagdollAnimator.AnimatedPair> pairs)
        {
            OnModifyTargetPose(pairs);
        }

        /// <summary>Called once after the controller has injected the runtime context.</summary>
        protected virtual void OnBehaviourInitialize()
        {
        }

        /// <summary>Called when the owning controller is destroyed.</summary>
        protected virtual void OnBehaviourShutdown()
        {
        }

        /// <summary>
        /// Called after the core has committed a new runtime muscle registry and the
        /// behaviour context points at the new generation.
        /// </summary>
        protected virtual void OnBehaviourHierarchyChanged(
            IReadOnlyList<RagdollMuscleChange> added,
            IReadOnlyList<RagdollMuscleChange> removed)
        {
        }

        /// <summary>Called when this becomes the controller's active behaviour.</summary>
        protected virtual void OnBehaviourActivated()
        {
        }

        /// <summary>Called before this stops being the active behaviour.</summary>
        protected virtual void OnBehaviourDeactivated()
        {
        }

        /// <summary>
        /// Called after the initialized RagdollAnimator has been re-enabled and snapped the
        /// physical rig to the current Target pose. Behaviour selection is preserved.
        /// </summary>
        protected virtual void OnBehaviourReactivated()
        {
        }

        /// <summary>Called when the core starts blending Alive to Dead.</summary>
        protected virtual void OnBehaviourKillStarted()
        {
        }

        /// <summary>Called after the core has completed the transition to Dead.</summary>
        protected virtual void OnBehaviourKillEnded()
        {
        }

        /// <summary>Called while restoring the core from Dead to Alive.</summary>
        protected virtual void OnBehaviourResurrected()
        {
        }

        /// <summary>
        /// Called after the physical Puppet has settled and immediately before this
        /// behaviour is suspended for the Frozen state.
        /// </summary>
        protected virtual void OnBehaviourFrozen()
        {
        }

        /// <summary>
        /// Called after the physical Puppet hierarchy has been restored but before the
        /// selected behaviour component resumes dispatch.
        /// </summary>
        protected virtual void OnBehaviourUnfrozen()
        {
        }

        /// <summary>
        /// Called after an external teleport operation has moved the rig. The hook receives
        /// the exact world-space delta so cached behaviour state can be transformed without
        /// performing the core teleport a second time.
        /// </summary>
        protected virtual void OnBehaviourTeleported(
            Quaternion deltaRotation,
            Vector3 deltaPosition,
            Vector3 pivot,
            bool moveToTarget)
        {
        }

        /// <summary>Called immediately before the core restores default Target transforms.</summary>
        protected virtual void OnBehaviourFixTransforms()
        {
        }

        /// <summary>Called immediately before the core samples the animated Target pose.</summary>
        protected virtual void OnBehaviourRead()
        {
        }

        /// <summary>Called immediately after the core maps the Puppet pose to the Target.</summary>
        protected virtual void OnBehaviourWrite()
        {
        }

        /// <summary>Called once from RagdollAnimator.FixedUpdate before animation matching.</summary>
        protected virtual void OnBehaviourFixedUpdate(float deltaTime)
        {
        }

        /// <summary>Called immediately when the shared collision hub reports a collision.</summary>
        protected virtual void OnBehaviourCollision(
            RagdollCollisionEvent collisionEvent)
        {
        }

        /// <summary>
        /// Returns the active behaviour's current root muscle-weight multiplier so a core
        /// lifecycle transition can start without increasing strength first.
        /// </summary>
        protected virtual float OnGetLifecycleMuscleWeight(
            RagdollAnimator.AnimatedPair pair)
        {
            return 1f;
        }

        /// <summary>Adjusts one bone's effective drive profile before forces are applied.</summary>
        protected virtual void OnModifyBoneProfile(
            ref BoneProfile boneProfile,
            RagdollAnimator.AnimatedPair pair,
            float deltaTime)
        {
        }

        /// <summary>Adjusts one bone's mapping weights before writing the simulated pose.</summary>
        protected virtual void OnModifyMapping(
            ref RagdollMappingWeights mappingWeights,
            RagdollAnimator.AnimatedPair pair)
        {
        }

        /// <summary>Applies kinematic target-pose changes before animation matching.</summary>
        protected virtual void OnModifyTargetPose(
            IReadOnlyList<RagdollAnimator.AnimatedPair> pairs)
        {
        }
    }
}
