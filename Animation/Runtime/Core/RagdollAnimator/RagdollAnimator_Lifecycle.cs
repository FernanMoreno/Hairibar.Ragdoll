using System;
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
        bool lifecycleInitialized;
        bool lifecycleKilling;
        float lifecycleKillElapsed;
        float lifecycleKillStartingWeight = 1f;

        public RagdollLifecycleState State
        {
            get => lifecycleState;
            set
            {
                ValidateLifecycleState(value);
                lifecycleState = value;
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
        public bool IsAlive => activeLifecycleState == RagdollLifecycleState.Alive;
        public bool IsDead => activeLifecycleState == RagdollLifecycleState.Dead;
        public bool IsKilling => lifecycleKilling;
        public bool IsSwitchingState => lifecycleKilling
            || activeLifecycleState != lifecycleState;
        public float KillProgress => !lifecycleKilling
            ? (IsDead ? 1f : 0f)
            : lifecycleSettings.KillDuration <= Mathf.Epsilon
                ? 1f
                : Mathf.Clamp01(
                    lifecycleKillElapsed / lifecycleSettings.KillDuration);

        public event Action DeathCompleted;
        public event Action Resurrected;

        public void Kill()
        {
            lifecycleState = RagdollLifecycleState.Dead;
        }

        public void Kill(RagdollLifecycleSettings settings)
        {
            settings.Normalize();
            lifecycleSettings = settings;
            lifecycleState = RagdollLifecycleState.Dead;
        }

        public void Resurrect()
        {
            lifecycleState = RagdollLifecycleState.Alive;
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

            activeLifecycleState = RagdollLifecycleState.Alive;
            lifecycleKilling = false;
            lifecycleKillElapsed = 0f;
            lifecycleKillStartingWeight = 1f;
            lifecycleMuscles.ClearLifecycleDrive();
            lifecycleInitialized = true;
        }

        void UpdateLifecycle(float deltaTime)
        {
            if (!lifecycleInitialized) return;

            if (lifecycleKilling)
            {
                AdvanceKill(deltaTime);
                return;
            }

            if (activeLifecycleState == lifecycleState) return;

            if (activeLifecycleState == RagdollLifecycleState.Alive
                && lifecycleState == RagdollLifecycleState.Dead)
            {
                BeginKill();
                return;
            }

            if (activeLifecycleState == RagdollLifecycleState.Dead
                && lifecycleState == RagdollLifecycleState.Alive)
            {
                CompleteResurrection();
            }
        }

        void BeginKill()
        {
            lifecycleSettings.Normalize();
            ForceActiveSimulationForDeath();

            lifecycleKilling = true;
            lifecycleKillElapsed = 0f;
            lifecycleKillStartingWeight = ResolveStartingMuscleWeight();

            lifecycleMuscles.SetLifecycleDrive(
                0f,
                lifecycleKillStartingWeight,
                lifecycleSettings.DeadMuscleDamper);
            lifecycleMuscles.ClearAllImmunity();
            CopySampledVelocitiesToPuppet();

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

        void CompleteResurrection()
        {
            lifecycleMuscles.ClearLifecycleDrive();
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
            if (!lifecycleSimulationMode
                || !lifecycleSimulationMode.IsInitialized)
            {
                return;
            }

            lifecycleSimulationMode.SetModeImmediate(
                RagdollSimulationMode.Active);
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

        void RestoreLifecycleAfterEnable()
        {
            if (!lifecycleInitialized) return;

            if (activeLifecycleState == RagdollLifecycleState.Dead)
            {
                lifecycleMuscles.SetLifecycleDrive(
                    0f,
                    lifecycleSettings.DeadMuscleWeight,
                    lifecycleSettings.DeadMuscleDamper);
                SetTargetAnimationEnabled(false);
            }
            else
            {
                lifecycleMuscles.ClearLifecycleDrive();
                SetTargetAnimationEnabled(true);
            }
        }

        void SettleLifecycleBeforeDisable()
        {
            if (!lifecycleInitialized) return;

            lifecycleKilling = false;
            activeLifecycleState = lifecycleState;
            if (activeLifecycleState == RagdollLifecycleState.Dead)
            {
                lifecycleMuscles.SetLifecycleDrive(
                    0f,
                    lifecycleSettings.DeadMuscleWeight,
                    lifecycleSettings.DeadMuscleDamper);
                SetTargetAnimationEnabled(false);
            }
            else
            {
                lifecycleMuscles.ClearLifecycleDrive();
                SetTargetAnimationEnabled(true);
            }
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
                && state != RagdollLifecycleState.Dead)
            {
                throw new ArgumentOutOfRangeException(nameof(state));
            }
        }

        void OnValidate()
        {
            lifecycleSettings.Normalize();
        }
    }
}
