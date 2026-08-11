using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    public partial class RagdollAnimator
    {
        bool manualSimulationPrepared;
        float manualSimulationDeltaTime;
        float manualSampleTime;
        float manualSampleTimeBeforePrepare;
        bool manualAnimatorWasEnabled;

        public bool IsManualSimulationPrepared => manualSimulationPrepared;

        /// <summary>
        /// Executes Target read, modifiers and matching before a caller-owned
        /// Physics.Simulate call. Physics.simulationMode must be Script.
        /// </summary>
        [RagdollCompatibilityApi("Lifecycle and animation", "http://www.root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public void PrepareManualSimulation(float deltaTime)
        {
            if (manualSimulationPrepared)
            {
                AbortManualSimulation(true);
                throw new InvalidOperationException(
                    "Manual simulation was already prepared and has been aborted.");
            }
            if (!gameObject.activeInHierarchy || animatedPairs == null)
            {
                throw new InvalidOperationException(
                    "Manual simulation requires an active GameObject and an initialized RagdollAnimator.");
            }
            if (enabled)
            {
                throw new InvalidOperationException(
                    "Manual simulation requires the RagdollAnimator component to be disabled.");
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
            manualSimulationDeltaTime = deltaTime;
            manualSampleTimeBeforePrepare = manualSampleTime;
            manualSampleTime = Mathf.Max(
                manualSampleTime,
                GetAnimationSampleTime()) + deltaTime;
            manualSimulationPrepared = true;

            try
            {
                // PuppetMaster's documented manual-simulation contract updates the
                // Animator in the pre-simulation call and then forces it disabled.
                // Legacy Animation remains externally driven; PuppetMaster does not
                // claim ownership of its update loop.
                if (targetAnimator && !usesLegacyTargetAnimation)
                {
                    targetAnimator.Update(deltaTime);
                    targetAnimator.enabled = false;
                }
                else if (targetAnimation)
                {
                    targetAnimation.Sample();
                }
                // Automatic simulation drains teleports at its animation-read
                // boundary. Manual simulation must provide the same boundary or a
                // request made while the component is disabled can never commit.
                ProcessPendingTeleport();
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
                AbortManualSimulation(true);
                throw;
            }
        }

        /// <summary>
        /// Completes mapping and hooks after the caller has simulated physics, then
        /// restores the exact Animator/Animation enabled state.
        /// </summary>
        [RagdollCompatibilityApi("Lifecycle and animation", "http://www.root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public void CompleteManualSimulation()
        {
            if (!manualSimulationPrepared)
            {
                throw new InvalidOperationException(
                    "PrepareManualSimulation must be called before completion.");
            }
            try
            {
                if (enabled)
                {
                    throw new InvalidOperationException(
                        "Manual simulation completion requires the RagdollAnimator component to remain disabled.");
                }
                if (!forceTargetPose) MapRagdollToTarget();
                UpdateLifecycle(manualSimulationDeltaTime);
                InvokePostLateUpdateHook();
            }
            finally
            {
                AbortManualSimulation(false);
            }
        }

        /// <summary>
        /// PuppetMaster-compatible name for the documented pre-simulation contract.
        /// The component must be disabled and Physics must use scripted simulation.
        /// </summary>
        [RagdollCompatibilityApi("Lifecycle and animation", "http://www.root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public void OnPreSimulate(float deltaTime)
        {
            PrepareManualSimulation(deltaTime);
        }

        /// <summary>PuppetMaster-compatible name for the post-simulation contract.</summary>
        [RagdollCompatibilityApi("Lifecycle and animation", "http://www.root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public void OnPostSimulate()
        {
            CompleteManualSimulation();
        }

        void RestoreManualAnimationComponents()
        {
            if (targetAnimator) targetAnimator.enabled = manualAnimatorWasEnabled;
        }

        void CancelPreparedManualSimulation()
        {
            if (!manualSimulationPrepared) return;
            AbortManualSimulation(true);
        }

        void AbortManualSimulation(bool rollbackSampleTime)
        {
            RestoreManualAnimationComponents();
            if (rollbackSampleTime)
                manualSampleTime = manualSampleTimeBeforePrepare;
            manualSimulationPrepared = false;
            manualSimulationDeltaTime = 0f;
            manualSampleTimeBeforePrepare = manualSampleTime;
        }
    }
}
