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

        internal void FixedUpdateInternal(float deltaTime)
        {
            OnBehaviourFixedUpdate(deltaTime);
        }

        internal void CollisionInternal(RagdollCollisionEvent collisionEvent)
        {
            OnBehaviourCollision(collisionEvent);
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

        /// <summary>Called when this becomes the controller's active behaviour.</summary>
        protected virtual void OnBehaviourActivated()
        {
        }

        /// <summary>Called before this stops being the active behaviour.</summary>
        protected virtual void OnBehaviourDeactivated()
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
