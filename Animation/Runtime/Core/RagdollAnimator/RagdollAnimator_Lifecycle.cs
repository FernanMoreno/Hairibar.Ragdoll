using System;
using System.Collections;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    public partial class RagdollAnimator
    {
        [SerializeField] RagdollLifecycleState lifecycleState =
            RagdollLifecycleState.Alive;
        [SerializeField] RagdollLifecycleSettings lifecycleSettings =
            RagdollLifecycleSettings.Default;

        RagdollLifecycleState activeLifecycleState =
            RagdollLifecycleState.Alive;
        RagdollMuscleController lifecycleMuscles;
        RagdollBehaviourController lifecycleBehaviours;
        RagdollSimulationModeController lifecycleSimulationMode;
        RagdollLifecyclePhysicsPolicy lifecyclePhysicsPolicy;
        bool lifecycleInitialized;
        bool lifecycleKilling;
        bool lifecyclePermanentDestructionScheduled;
        bool lifecycleApplicationQuitting;
        float lifecycleKillElapsed;
        float lifecycleKillStartingWeight = 1f;

        public RagdollLifecycleState State
        {
            get => lifecycleState;
            set
            {
                ValidateLifecycleState(value);
                lifecycleState = value;
                WakeLifecycleFromDisabledSimulation(value);
            }
        }

        public RagdollLifecycleState ActiveState => activeLifecycleState;
        public RagdollLifecycleSettings LifecycleSettings
        {
            get => lifecycleSettings;
            set
            {
                value.Normalize();
                lifecycleSettings = value;
            }
        }
        public bool IsAlive =>
            activeLifecycleState == RagdollLifecycleState.Alive;
        public bool IsDead =>
            activeLifecycleState == RagdollLifecycleState.Dead;
        public bool IsFrozen =>
            activeLifecycleState == RagdollLifecycleState.Frozen;
        public bool IsKilling => lifecycleKilling;
        public bool IsWaitingForFreeze =>
            !lifecycleKilling
            && activeLifecycleState == RagdollLifecycleState.Dead
            && lifecycleState == RagdollLifecycleState.Frozen;
        public bool IsSwitchingState => lifecycleKilling
            || activeLifecycleState != lifecycleState;
        public float KillProgress => !lifecycleKilling
            ? (IsDead || IsFrozen ? 1f : 0f)
            : lifecycleSettings.KillDuration <= Mathf.Epsilon
                ? 1f
                : Mathf.Clamp01(
                    lifecycleKillElapsed / lifecycleSettings.KillDuration);
        public float MaximumPuppetSqrVelocity =>
            ResolveMaximumPuppetSqrVelocity();
        public bool FreezeReady => IsWaitingForFreeze
            && AreAllMusclesReadyToFreeze();

        public event Action DeathCompleted;
        public event Action Frozen;
        public event Action Unfrozen;
        public event Action Resurrected;

        public void Kill()
        {
            State = RagdollLifecycleState.Dead;
        }

        public void Kill(RagdollLifecycleSettings settings)
        {
            settings.Normalize();
            lifecycleSettings = settings;
            State = RagdollLifecycleState.Dead;
        }

        public void Freeze()
        {
            State = RagdollLifecycleState.Frozen;
        }

        public void Freeze(RagdollLifecycleSettings settings)
        {
            settings.Normalize();
            lifecycleSettings = settings;
            State = RagdollLifecycleState.Frozen;
        }

        public void Resurrect()
        {
            State = RagdollLifecycleState.Alive;
        }

        void WakeLifecycleFromDisabledSimulation(
            RagdollLifecycleState requestedState)
        {
            if (!lifecycleInitialized
                || requestedState == RagdollLifecycleState.Alive
                || !lifecycleSimulationMode
                || !lifecycleSimulationMode.IsInitialized)
            {
                return;
            }

            if (lifecycleSimulationMode.CurrentMode
                    == RagdollSimulationMode.Disabled
                || lifecycleSimulationMode.TargetMode
                    == RagdollSimulationMode.Disabled)
            {
                lifecycleSimulationMode.ForceActiveForLifecycle();
            }
        }

        void InitializeLifecycle()
        {
            if (lifecycleInitialized) return;

            lifecycleSettings.Normalize();
            ValidateLifecycleState(lifecycleState);
            lifecycleMuscles = GetComponent<RagdollMuscleController>();
            lifecycleBehaviours = GetComponent<RagdollBehaviourController>();
            lifecycleSimulationMode =
                GetComponent<RagdollSimulationModeController>();

            if (!lifecycleMuscles || !lifecycleMuscles.IsInitialized)
            {
                throw new InvalidOperationException(
                    "RagdollAnimator lifecycle requires an initialized "
                    + "RagdollMuscleController.");
            }
            if (!lifecycleSimulationMode
                || !lifecycleSimulationMode.IsInitialized)
            {
                throw new InvalidOperationException(
                    "RagdollAnimator lifecycle requires an initialized "
                    + "RagdollSimulationModeController.");
            }

            lifecyclePhysicsPolicy =
                RagdollLifecyclePhysicsPolicy.Create(Bindings);
            activeLifecycleState = RagdollLifecycleState.Alive;
            lifecycleKilling = false;
            lifecyclePermanentDestructionScheduled = false;
            lifecycleKillElapsed = 0f;
            lifecycleKillStartingWeight = 1f;
            lifecycleMuscles.ClearLifecycleDrive();
            lifecycleInitialized = true;
        }

        void UpdateLifecycle(float deltaTime)
        {
            if (!lifecycleInitialized
                || lifecyclePermanentDestructionScheduled)
            {
                return;
            }

            if (lifecycleKilling)
            {
                AdvanceKill(deltaTime);
                return;
            }

            if (activeLifecycleState == lifecycleState)
            {
                if (activeLifecycleState == RagdollLifecycleState.Frozen
                    && lifecycleSettings.FreezePermanently)
                {
                    SchedulePermanentFreezeDestruction();
                }
                return;
            }

            switch (activeLifecycleState)
            {
                case RagdollLifecycleState.Alive:
                    BeginKill();
                    break;
                case RagdollLifecycleState.Dead:
                    if (lifecycleState == RagdollLifecycleState.Alive)
                    {
                        CompleteResurrection();
                    }
                    else if (lifecycleState == RagdollLifecycleState.Frozen)
                    {
                        TryCompleteFreeze();
                    }
                    break;
                case RagdollLifecycleState.Frozen:
                    if (lifecycleState == RagdollLifecycleState.Alive)
                    {
                        CompleteUnfreeze(true);
                    }
                    else if (lifecycleState == RagdollLifecycleState.Dead)
                    {
                        CompleteUnfreeze(false);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(activeLifecycleState));
            }
        }

        void BeginKill()
        {
            lifecycleSettings.Normalize();
            ForceActiveSimulationForDeath();

            bool angularPolicyStarted = false;
            bool internalCollisionPolicyStarted = false;
            try
            {
                lifecyclePhysicsPolicy.BeginKill(
                    lifecycleSettings.EnableAngularLimitsOnKill
                        && !manualAngularLimitControl);
                angularPolicyStarted = true;

                BeginInternalCollisionLifecycleOverride(
                    lifecycleSettings.EnableInternalCollisionsOnKill);
                internalCollisionPolicyStarted = true;

                lifecycleKilling = true;
                lifecycleKillElapsed = 0f;
                lifecycleKillStartingWeight = ResolveStartingMuscleWeight();

                lifecycleMuscles.SetLifecycleDrive(
                    0f,
                    lifecycleKillStartingWeight,
                    lifecycleSettings.DeadMuscleDamper);
                lifecycleMuscles.ClearAllImmunity();
                CopySampledVelocitiesToPuppet();
            }
            catch
            {
                lifecycleKilling = false;
                lifecycleMuscles.ClearLifecycleDrive();
                try
                {
                    if (internalCollisionPolicyStarted)
                    {
                        EndInternalCollisionLifecycleOverride();
                    }
                }
                finally
                {
                    if (angularPolicyStarted)
                    {
                        lifecyclePhysicsPolicy.RestoreAfterDeath();
                    }
                }
                throw;
            }

            if (lifecycleBehaviours && lifecycleBehaviours.IsInitialized)
            {
                lifecycleBehaviours.NotifyKillStarted();
            }
            if (RagdollLifecycleMath.IsKillComplete(
                lifecycleKillStartingWeight,
                lifecycleSettings.DeadMuscleWeight,
                0f,
                lifecycleSettings.KillDuration))
            {
                CompleteKill();
            }
        }

        void AdvanceKill(float deltaTime)
        {
            lifecycleKillElapsed += Mathf.Max(0f, deltaTime);
            float muscleWeight =
                RagdollLifecycleMath.EvaluateKillMuscleWeight(
                    lifecycleKillStartingWeight,
                    lifecycleSettings.DeadMuscleWeight,
                    lifecycleKillElapsed,
                    lifecycleSettings.KillDuration);
            lifecycleMuscles.SetLifecycleDrive(
                0f,
                muscleWeight,
                lifecycleSettings.DeadMuscleDamper);

            if (RagdollLifecycleMath.IsKillComplete(
                lifecycleKillStartingWeight,
                lifecycleSettings.DeadMuscleWeight,
                lifecycleKillElapsed,
                lifecycleSettings.KillDuration))
            {
                CompleteKill();
            }
        }

        void CompleteKill()
        {
            lifecycleMuscles.SetLifecycleDrive(
                0f,
                lifecycleSettings.DeadMuscleWeight,
                lifecycleSettings.DeadMuscleDamper);
            SetTargetAnimationEnabled(false);

            lifecycleKilling = false;
            lifecycleKillElapsed = lifecycleSettings.KillDuration;
            activeLifecycleState = RagdollLifecycleState.Dead;

            if (lifecycleBehaviours && lifecycleBehaviours.IsInitialized)
            {
                lifecycleBehaviours.NotifyKillEnded();
            }
            DeathCompleted?.Invoke();
        }

        void TryCompleteFreeze()
        {
            if (!AreAllMusclesReadyToFreeze()) return;

            SetTargetAnimationEnabled(false);
            SuspendDisconnectedMusclesForLifecycleFreeze();
            try
            {
                lifecycleSimulationMode.SuspendForLifecycleFreeze();
            }
            catch
            {
                ResumeDisconnectedMusclesAfterLifecycleFreeze();
                throw;
            }
            if (lifecycleBehaviours && lifecycleBehaviours.IsInitialized)
            {
                lifecycleBehaviours.NotifyFrozen();
            }

            activeLifecycleState = RagdollLifecycleState.Frozen;
            Frozen?.Invoke();

            if (lifecycleSettings.FreezePermanently)
            {
                SchedulePermanentFreezeDestruction();
            }
        }

        void CompleteUnfreeze(bool resurrect)
        {
            if (resurrect)
            {
                lifecycleMuscles.ClearLifecycleDrive();
                try
                {
                    lifecyclePhysicsPolicy.RestoreAfterDeath();
                }
                finally
                {
                    EndInternalCollisionLifecycleOverride();
                }
            }
            else
            {
                lifecycleMuscles.SetLifecycleDrive(
                    0f,
                    lifecycleSettings.DeadMuscleWeight,
                    lifecycleSettings.DeadMuscleDamper);
            }

            lifecycleSimulationMode.ResumeFromLifecycleFreeze();
            ResumeDisconnectedMusclesAfterLifecycleFreeze();
            ReapplyInternalCollisionPolicy();
            if (lifecycleBehaviours && lifecycleBehaviours.IsInitialized)
            {
                lifecycleBehaviours.NotifyUnfrozen();
            }

            if (!resurrect)
            {
                SetTargetAnimationEnabled(false);
                activeLifecycleState = RagdollLifecycleState.Dead;
                Unfrozen?.Invoke();
                return;
            }

            if (lifecycleBehaviours && lifecycleBehaviours.IsInitialized)
            {
                lifecycleBehaviours.NotifyResurrected();
            }
            SetTargetAnimationEnabled(true);

            lifecycleKillElapsed = 0f;
            lifecycleKillStartingWeight = 1f;
            activeLifecycleState = RagdollLifecycleState.Alive;
            Unfrozen?.Invoke();
            Resurrected?.Invoke();
        }

        void CompleteResurrection()
        {
            lifecycleMuscles.ClearLifecycleDrive();
            try
            {
                lifecyclePhysicsPolicy.RestoreAfterDeath();
            }
            finally
            {
                EndInternalCollisionLifecycleOverride();
            }
            if (lifecycleBehaviours && lifecycleBehaviours.IsInitialized)
            {
                lifecycleBehaviours.NotifyResurrected();
            }
            SetTargetAnimationEnabled(true);

            lifecycleKilling = false;
            lifecycleKillElapsed = 0f;
            lifecycleKillStartingWeight = 1f;
            activeLifecycleState = RagdollLifecycleState.Alive;
            Resurrected?.Invoke();
        }

        void ForceActiveSimulationForDeath()
        {
            lifecycleSimulationMode.ForceActiveForLifecycle();
        }

        bool AreAllMusclesReadyToFreeze()
        {
            if (animatedPairs == null) return false;

            for (int index = 0; index < animatedPairs.Length; index++)
            {
                Rigidbody rigidbody =
                    animatedPairs[index].RagdollBone.Rigidbody;
                if (!rigidbody) continue;

                if (!RagdollLifecycleMath.IsFreezeVelocityReady(
                    rigidbody.velocity.sqrMagnitude,
                    lifecycleSettings.MaxFreezeSqrVelocity))
                {
                    return false;
                }
            }

            return true;
        }

        float ResolveMaximumPuppetSqrVelocity()
        {
            if (animatedPairs == null) return 0f;

            float maximum = 0f;
            for (int index = 0; index < animatedPairs.Length; index++)
            {
                Rigidbody rigidbody =
                    animatedPairs[index].RagdollBone.Rigidbody;
                if (!rigidbody) continue;

                maximum =
                    RagdollLifecycleMath.AccumulateMaximumSqrVelocity(
                        maximum,
                        rigidbody.velocity.sqrMagnitude);
            }

            return maximum;
        }

        float ResolveStartingMuscleWeight()
        {
            if (animatedPairs == null || animatedPairs.Length == 0)
            {
                return 1f;
            }

            if (!lifecycleBehaviours || !lifecycleBehaviours.IsInitialized)
            {
                return 1f;
            }

            AnimatedPair referencePair = animatedPairs[0];
            for (int index = 0; index < animatedPairs.Length; index++)
            {
                if (!Bindings.Definition.IsRoot(animatedPairs[index].Name))
                {
                    continue;
                }

                referencePair = animatedPairs[index];
                break;
            }

            return RagdollLifecycleMath.SanitizeWeight(
                lifecycleBehaviours.ResolveLifecycleMuscleWeight(
                    referencePair),
                1f);
        }

        void CopySampledVelocitiesToPuppet()
        {
            if (animatedPairs == null) return;

            for (int index = 0; index < animatedPairs.Length; index++)
            {
                AnimatedPair pair = animatedPairs[index];
                Rigidbody rigidbody = pair.RagdollBone.Rigidbody;
                if (!rigidbody) continue;

                rigidbody.velocity = pair.poseLinearVelocity;
                rigidbody.angularVelocity = pair.poseAngularVelocity;
            }
        }

        void ApplyLifecycleParameters(ref BoneProfile boneProfile)
        {
            if (!lifecycleInitialized || !lifecycleMuscles) return;

            RagdollLifecycleMath.ApplyDeadDrive(
                ref boneProfile,
                lifecycleMuscles.LifecyclePositionAuthorityMultiplier,
                lifecycleMuscles.LifecycleMuscleWeightMultiplier,
                lifecycleMuscles.LifecycleMuscleDamperAdd);
        }

        bool LifecycleAllowsAnimationSampling()
        {
            return !lifecycleInitialized
                || activeLifecycleState == RagdollLifecycleState.Alive;
        }

        bool LifecycleAllowsEnableSnap()
        {
            return !lifecycleInitialized
                || activeLifecycleState == RagdollLifecycleState.Alive;
        }

        bool LifecycleIsFrozenStable()
        {
            return lifecycleInitialized
                && activeLifecycleState == RagdollLifecycleState.Frozen;
        }

        void RestoreLifecycleAfterEnable()
        {
            if (!lifecycleInitialized
                || lifecyclePermanentDestructionScheduled)
            {
                return;
            }

            switch (activeLifecycleState)
            {
                case RagdollLifecycleState.Alive:
                    lifecycleMuscles.ClearLifecycleDrive();
                    SetTargetAnimationEnabled(true);
                    break;
                case RagdollLifecycleState.Dead:
                    lifecycleMuscles.SetLifecycleDrive(
                        0f,
                        lifecycleSettings.DeadMuscleWeight,
                        lifecycleSettings.DeadMuscleDamper);
                    SetTargetAnimationEnabled(false);
                    break;
                case RagdollLifecycleState.Frozen:
                    lifecycleMuscles.SetLifecycleDrive(
                        0f,
                        lifecycleSettings.DeadMuscleWeight,
                        lifecycleSettings.DeadMuscleDamper);
                    SetTargetAnimationEnabled(false);
                    lifecycleSimulationMode.SuspendForLifecycleFreeze();
                    if (lifecycleBehaviours
                        && lifecycleBehaviours.IsInitialized)
                    {
                        lifecycleBehaviours.NotifyFrozen();
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(activeLifecycleState));
            }
        }

        void SettleLifecycleBeforeDisable()
        {
            if (!lifecycleInitialized
                || lifecyclePermanentDestructionScheduled)
            {
                return;
            }

            if (lifecycleKilling)
            {
                CompleteKill();
            }

            if (activeLifecycleState == RagdollLifecycleState.Frozen)
            {
                if (lifecycleState == RagdollLifecycleState.Alive)
                {
                    CompleteUnfreeze(true);
                }
                else if (lifecycleState == RagdollLifecycleState.Dead)
                {
                    CompleteUnfreeze(false);
                }
                return;
            }

            if (activeLifecycleState == RagdollLifecycleState.Dead
                && lifecycleState == RagdollLifecycleState.Alive)
            {
                CompleteResurrection();
                return;
            }

            if (activeLifecycleState == RagdollLifecycleState.Alive
                && lifecycleState != RagdollLifecycleState.Alive)
            {
                BeginKill();
                if (lifecycleKilling)
                {
                    CompleteKill();
                }
            }
        }

        void SchedulePermanentFreezeDestruction()
        {
            if (lifecyclePermanentDestructionScheduled) return;

            lifecyclePermanentDestructionScheduled = true;
            lifecyclePhysicsPolicy.AbandonForPermanentFreeze();
            AbandonInternalCollisionsForPermanentFreeze();
            lifecycleSimulationMode
                .AbandonLifecycleFreezeForPermanentDestruction();
            StartCoroutine(DestroyFrozenSubsystemPermanently());
        }

        IEnumerator DestroyFrozenSubsystemPermanently()
        {
            DestroyDisconnectedMusclesForPermanentFreeze();

            if (lifecycleBehaviours && lifecycleBehaviours.IsInitialized)
            {
                lifecycleBehaviours.DestroyBehavioursForPermanentFreeze();
                Destroy(lifecycleBehaviours);
            }

            // BehaviourController requires both Animator and MuscleController. Wait for
            // its destruction before removing those required components.
            yield return null;

            if (lifecycleMuscles) Destroy(lifecycleMuscles);
            if (lifecycleSimulationMode) Destroy(lifecycleSimulationMode);

            yield return null;

            if (Bindings && Bindings.gameObject != gameObject)
            {
                Destroy(Bindings.gameObject);
            }
            Destroy(this);
        }

        void ShutdownLifecycle()
        {
            if (!lifecycleInitialized
                || lifecyclePermanentDestructionScheduled
                || lifecycleApplicationQuitting
                || !gameObject.scene.isLoaded)
            {
                return;
            }

            if (activeLifecycleState == RagdollLifecycleState.Frozen
                && lifecycleSimulationMode
                && lifecycleSimulationMode.IsInitialized
                && lifecycleSimulationMode.IsLifecycleFreezeSuspended)
            {
                lifecycleSimulationMode.ResumeFromLifecycleFreeze();
                ResumeDisconnectedMusclesAfterLifecycleFreeze();
                ReapplyInternalCollisionPolicy();
                if (lifecycleBehaviours
                    && lifecycleBehaviours.IsInitialized)
                {
                    lifecycleBehaviours.NotifyUnfrozen();
                }
            }

            try
            {
                if (lifecyclePhysicsPolicy != null)
                {
                    lifecyclePhysicsPolicy.RestoreAfterDeath();
                }
            }
            finally
            {
                EndInternalCollisionLifecycleOverride();
            }
            if (lifecycleMuscles)
            {
                lifecycleMuscles.ClearLifecycleDrive();
            }
            SetTargetAnimationEnabled(true);
            lifecycleInitialized = false;
        }

        void SetTargetAnimationEnabled(bool enabled)
        {
            if (targetAnimator)
            {
                targetAnimator.enabled = enabled;
            }
        }

        static void ValidateLifecycleState(RagdollLifecycleState state)
        {
            if (state != RagdollLifecycleState.Alive
                && state != RagdollLifecycleState.Dead
                && state != RagdollLifecycleState.Frozen)
            {
                throw new ArgumentOutOfRangeException(nameof(state));
            }
        }

        void OnValidate()
        {
            lifecycleSettings.Normalize();
            pinSettings.Normalize();
            jointRuntimeSettings.Normalize();
            internalCollisionSettings.Normalize();
        }
    }
}
