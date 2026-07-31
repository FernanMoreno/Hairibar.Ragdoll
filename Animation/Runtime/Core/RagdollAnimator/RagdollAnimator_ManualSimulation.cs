using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    public partial class RagdollAnimator
    {
        bool manualSimulationPrepared;
        float manualSimulationDeltaTime;
        float manualSampleTime;
        bool manualAnimatorWasEnabled;
        bool manualAnimationWasEnabled;

        public bool IsManualSimulationPrepared => manualSimulationPrepared;

        /// <summary>
        /// Executes Target read, modifiers and matching before a caller-owned
        /// Physics.Simulate call. Physics.simulationMode must be Script.
        /// </summary>
        public void PrepareManualSimulation(float deltaTime)
        {
            if (manualSimulationPrepared)
            {
                throw new InvalidOperationException(
                    "Manual simulation is already prepared.");
            }
            if (!isActiveAndEnabled || animatedPairs == null)
            {
                throw new InvalidOperationException(
                    "Manual simulation requires an active initialized RagdollAnimator.");
            }
            if (Physics.simulationMode != SimulationMode.Script)
            {
                throw new InvalidOperationException(
                    "Physics.simulationMode must be SimulationMode.Script.");
            }
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)
                || deltaTime <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deltaTime),
                    "Manual simulation delta time must be finite and positive.");
            }
            if (LifecycleIsFrozenStable())
            {
                throw new InvalidOperationException(
                    "A frozen ragdoll cannot begin a manual simulation step.");
            }

            manualAnimatorWasEnabled = targetAnimator && targetAnimator.enabled;
            manualAnimationWasEnabled = targetAnimation && targetAnimation.enabled;
            if (targetAnimator) targetAnimator.enabled = false;
            if (targetAnimation) targetAnimation.enabled = false;
            manualSimulationDeltaTime = deltaTime;
            manualSampleTime = Mathf.Max(
                manualSampleTime,
                GetAnimationSampleTime()) + deltaTime;
            manualSimulationPrepared = true;

            try
            {
                ReadAnimatedPose();
                ProcessPendingMuscleConnectionOperations();
                RestoreAnimatedPose();
                ModifyTargetPose();
                UpdateJointRuntimeBeforeSimulation();
                ReapplyDisconnectedPhysicalPolicies();
                UpdateInternalCollisionsBeforeSimulation();
                DoAnimationMatching(deltaTime);
            }
            catch
            {
                RestoreManualAnimationComponents();
                manualSimulationPrepared = false;
                throw;
            }
        }

        /// <summary>
        /// Completes mapping and hooks after the caller has simulated physics, then
        /// restores the exact Animator/Animation enabled state.
        /// </summary>
        public void CompleteManualSimulation()
        {
            if (!manualSimulationPrepared)
            {
                throw new InvalidOperationException(
                    "PrepareManualSimulation must be called before completion.");
            }

            try
            {
                if (!forceTargetPose) MapRagdollToTarget();
                UpdateLifecycle(manualSimulationDeltaTime);
                InvokePostLateUpdateHook();
            }
            finally
            {
                RestoreManualAnimationComponents();
                manualSimulationPrepared = false;
                manualSimulationDeltaTime = 0f;
            }
        }

        void RestoreManualAnimationComponents()
        {
            if (targetAnimator) targetAnimator.enabled = manualAnimatorWasEnabled;
            if (targetAnimation) targetAnimation.enabled = manualAnimationWasEnabled;
        }

        void CancelPreparedManualSimulation()
        {
            if (!manualSimulationPrepared) return;
            RestoreManualAnimationComponents();
            manualSimulationPrepared = false;
            manualSimulationDeltaTime = 0f;
        }
    }
}
