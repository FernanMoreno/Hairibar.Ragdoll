using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Stores persistent per-bone animation and mapping authority plus temporary impact suppression.
    /// The controller composes with authored values instead of replacing them.
    /// </summary>
    [AddComponentMenu("Ragdoll/Ragdoll Muscle Controller")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RagdollAnimator))]
    public sealed class RagdollMuscleController : MonoBehaviour,
        IBoneProfileModifier,
        IRagdollMappingModifier,
        IOrderedRagdollModifier
    {
        [SerializeField] RagdollMuscleProfile muscleProfile;
        [SerializeField, Min(0f)] float positionSuppressionRecoveryRate = 2f;
        [SerializeField, Min(0f)] float rotationSuppressionRecoveryRate = 2f;

        // Runtime owner multiplier. BehaviourPuppet sets this while active and restores 1 on exit.
        float positionSuppressionRecoveryMultiplier = 1f;

        RagdollDefinitionBindings bindings;
        RagdollMuscleProfileRuntime runtimeProfile;
        MuscleRuntimeState[] states;
        float[] lastRecoveryTimes;

        public bool IsInitialized => states != null;
        public RagdollMuscleProfile MuscleProfile => muscleProfile;
        public RagdollModifierStage Stage => RagdollModifierStage.RuntimeState;
        public int Priority => 0;

        public float PositionSuppressionRecoveryRate
        {
            get => positionSuppressionRecoveryRate;
            set => positionSuppressionRecoveryRate = Mathf.Max(0f, value);
        }

        public float RotationSuppressionRecoveryRate
        {
            get => rotationSuppressionRecoveryRate;
            set => rotationSuppressionRecoveryRate = Mathf.Max(0f, value);
        }

        public float PositionSuppressionRecoveryMultiplier =>
            positionSuppressionRecoveryMultiplier;

        public void Initialize(IEnumerable<RagdollAnimator.AnimatedPair> pairs)
        {
            RagdollAnimator animator = GetComponent<RagdollAnimator>();
            bindings = animator.Bindings;

            if (muscleProfile)
            {
                string profileError;
                if (!muscleProfile.TryCreateRuntime(
                    bindings,
                    out runtimeProfile,
                    out profileError))
                {
                    throw new InvalidOperationException(
                        "The assigned RagdollMuscleProfile is invalid: " + profileError);
                }
            }
            else
            {
                runtimeProfile = null;
            }

            int boneCount = bindings.BoneCount;
            states = new MuscleRuntimeState[boneCount];
            lastRecoveryTimes = new float[boneCount];

            float now = CurrentTime;
            for (int i = 0; i < boneCount; i++)
            {
                states[i] = MuscleRuntimeState.Default;
                lastRecoveryTimes[i] = now;
            }

            foreach (RagdollAnimator.AnimatedPair pair in pairs)
            {
                if (!bindings.Topology.Contains(pair.Handle))
                {
                    throw new InvalidOperationException(
                        "RagdollMuscleController received an animated pair from another registry generation.");
                }
            }
        }

        public void Modify(ref BoneProfile boneProfile, RagdollAnimator.AnimatedPair pair, float dt)
        {
            if (!enabled || states == null) return;

            int index = pair.Handle.Index;
            AdvanceRecovery(index, CurrentTime);
            states[index].ApplyTo(
                ref boneProfile,
                GetBehaviourSettings(index).minimumPositionAuthority);
        }

        public void ModifyMapping(
            ref RagdollMappingWeights mappingWeights,
            RagdollAnimator.AnimatedPair pair)
        {
            if (!enabled || states == null) return;

            int index = pair.Handle.Index;
            AdvanceRecovery(index, CurrentTime);
            MuscleRuntimeState state = states[index];
            state.ApplyTo(ref mappingWeights);

            if (runtimeProfile != null)
            {
                float behaviourMappingAuthority = GetBehaviourSettings(index)
                    .EvaluateMappingAuthority(state.PositionSuppression);
                mappingWeights.Multiply(
                    behaviourMappingAuthority,
                    behaviourMappingAuthority);
            }
        }

        public MuscleRuntimeState GetState(RagdollBoneHandle bone)
        {
            int index = ValidateHandle(bone);
            AdvanceRecovery(index, CurrentTime);
            return states[index];
        }

        public bool TryGetMuscleGroup(
            RagdollBoneHandle bone,
            out RagdollMuscleGroup group)
        {
            int index = ValidateHandle(bone);
            if (runtimeProfile == null)
            {
                group = default(RagdollMuscleGroup);
                return false;
            }

            group = runtimeProfile.GetGroup(index);
            return true;
        }

        public RagdollMuscleBehaviourSettings GetBehaviourSettings(
            RagdollBoneHandle bone)
        {
            return GetBehaviourSettings(ValidateHandle(bone));
        }

        /// <summary>
        /// Returns the effective pin/position authority after persistent authority, temporary
        /// suppression and the semantic group's minimum authority have been composed.
        /// </summary>
        public float GetEffectivePositionAuthority(RagdollBoneHandle bone)
        {
            int index = ValidateHandle(bone);
            AdvanceRecovery(index, CurrentTime);

            MuscleRuntimeState state = states[index];
            return RagdollMuscleRecoveryMath.ResolveEffectivePositionAuthority(
                state.PositionAuthority,
                state.PositionSuppression,
                GetBehaviourSettings(index).minimumPositionAuthority);
        }

        /// <summary>
        /// Sets a reversible runtime multiplier for position-suppression recovery. The current
        /// state is first advanced with the previous multiplier so rate changes are deterministic.
        /// </summary>
        internal void SetPositionSuppressionRecoveryMultiplier(float multiplier)
        {
            float sanitized =
                RagdollMuscleRecoveryMath.SanitizeRecoveryMultiplier(
                    multiplier,
                    1f);
            if (Mathf.Approximately(
                positionSuppressionRecoveryMultiplier,
                sanitized))
            {
                return;
            }

            AdvanceAllRecovery(CurrentTime);
            positionSuppressionRecoveryMultiplier = sanitized;
        }

        internal void ClearPositionSuppressionRecoveryMultiplier()
        {
            SetPositionSuppressionRecoveryMultiplier(1f);
        }

        public void SetAuthorities(RagdollBoneHandle bone, float positionAuthority, float rotationAuthority)
        {
            int index = ValidateHandle(bone);
            AdvanceRecovery(index, CurrentTime);
            states[index].SetAuthorities(positionAuthority, rotationAuthority);
        }

        public void SetMappingAuthorities(
            RagdollBoneHandle bone,
            float positionMappingAuthority,
            float rotationMappingAuthority)
        {
            int index = ValidateHandle(bone);
            states[index].SetMappingAuthorities(
                positionMappingAuthority,
                rotationMappingAuthority);
        }

        public void SetDriveMultipliers(
            RagdollBoneHandle bone,
            float positionDampingMultiplier,
            float rotationDampingMultiplier,
            float maxLinearAccelerationMultiplier,
            float maxAngularAccelerationMultiplier)
        {
            int index = ValidateHandle(bone);
            AdvanceRecovery(index, CurrentTime);
            states[index].SetDriveMultipliers(
                positionDampingMultiplier,
                rotationDampingMultiplier,
                maxLinearAccelerationMultiplier,
                maxAngularAccelerationMultiplier);
        }

        public void AccumulateSuppression(
            RagdollBoneHandle bone,
            float positionSuppression,
            float rotationSuppression)
        {
            int index = ValidateHandle(bone);
            AdvanceRecovery(index, CurrentTime);
            states[index].AccumulateSuppression(positionSuppression, rotationSuppression);
        }

        public void ApplyImpact(RagdollBoneHandle source, MuscleImpactSettings settings)
        {
            ValidateImpactSource(source);

            RagdollMuscleBehaviourSettings sourceSettings =
                GetBehaviourSettings(source.Index);
            float sourcePositionSuppression = runtimeProfile == null
                ? Mathf.Clamp01(settings.positionSuppression)
                : sourceSettings.ScaleCollisionSuppression(
                    settings.positionSuppression);

            ApplyImpactCore(source, settings, sourcePositionSuppression);
        }

        /// <summary>
        /// Applies an impact whose source suppression has already been resolved against
        /// global, layer and muscle resistance. Used internally by BehaviourPuppet so the
        /// source muscle resistance is not applied twice.
        /// </summary>
        internal void ApplyResolvedImpact(
            RagdollBoneHandle source,
            MuscleImpactSettings settings)
        {
            ValidateImpactSource(source);
            ApplyImpactCore(
                source,
                settings,
                Mathf.Clamp01(settings.positionSuppression));
        }

        void ApplyImpactCore(
            RagdollBoneHandle source,
            MuscleImpactSettings settings,
            float sourcePositionSuppression)
        {
            RagdollBoneTopology topology = bindings.Topology;
            RagdollMuscleBehaviourSettings sourceSettings =
                GetBehaviourSettings(source.Index);
            float now = CurrentTime;

            for (int index = 0; index < states.Length; index++)
            {
                RagdollBoneHandle affected = bindings.GetHandleAt(index);
                int distance = topology.GetKinshipDistance(source, affected);
                float distanceWeight = settings.GetPropagationWeight(distance);
                if (distanceWeight <= 0f) continue;

                float positionWeight = distanceWeight;
                if (runtimeProfile != null)
                {
                    positionWeight *= GetSemanticPropagationMultiplier(
                        topology,
                        source,
                        affected,
                        sourceSettings);
                }

                float positionSuppression =
                    sourcePositionSuppression * positionWeight;
                float rotationSuppression =
                    settings.rotationSuppression * distanceWeight;
                if (positionSuppression <= 0f && rotationSuppression <= 0f) continue;

                AdvanceRecovery(index, now);
                states[index].AccumulateSuppression(
                    positionSuppression,
                    rotationSuppression);
            }
        }

        void ValidateImpactSource(RagdollBoneHandle source)
        {
            EnsureInitialized();
            if (!bindings.Topology.Contains(source))
            {
                throw new ArgumentException(
                    "The supplied impact source does not belong to this muscle controller.",
                    nameof(source));
            }
        }

        public void ResetBone(RagdollBoneHandle bone)
        {
            int index = ValidateHandle(bone);
            states[index] = MuscleRuntimeState.Default;
            lastRecoveryTimes[index] = CurrentTime;
        }

        public void ResetAll()
        {
            EnsureInitialized();

            float now = CurrentTime;
            for (int i = 0; i < states.Length; i++)
            {
                states[i] = MuscleRuntimeState.Default;
                lastRecoveryTimes[i] = now;
            }
        }

        float GetSemanticPropagationMultiplier(
            RagdollBoneTopology topology,
            RagdollBoneHandle source,
            RagdollBoneHandle affected,
            RagdollMuscleBehaviourSettings sourceSettings)
        {
            if (source == affected) return 1f;

            float multiplier = 0f;
            if (topology.IsAncestorOf(affected, source))
            {
                multiplier = sourceSettings.GetPropagationMultiplier(
                    RagdollMuscleRelation.Parent);
            }
            else if (topology.IsAncestorOf(source, affected))
            {
                multiplier = sourceSettings.GetPropagationMultiplier(
                    RagdollMuscleRelation.Child);
            }

            if (runtimeProfile.GetGroup(source.Index)
                == runtimeProfile.GetGroup(affected.Index))
            {
                multiplier = Mathf.Max(
                    multiplier,
                    sourceSettings.GetPropagationMultiplier(
                        RagdollMuscleRelation.SameGroup));
            }

            return multiplier;
        }

        RagdollMuscleBehaviourSettings GetBehaviourSettings(int index)
        {
            return runtimeProfile != null
                ? runtimeProfile.GetSettings(index)
                : RagdollMuscleBehaviourSettings.Default;
        }

        int ValidateHandle(RagdollBoneHandle bone)
        {
            EnsureInitialized();

            if (!bindings.Topology.Contains(bone))
            {
                throw new ArgumentException(
                    "The supplied bone handle does not belong to this muscle controller.",
                    nameof(bone));
            }

            return bone.Index;
        }

        void EnsureInitialized()
        {
            if (states == null)
            {
                throw new InvalidOperationException(
                    "RagdollMuscleController has not been initialized by a RagdollAnimator.");
            }
        }

        void AdvanceAllRecovery(float now)
        {
            if (states == null) return;

            for (int index = 0; index < states.Length; index++)
            {
                AdvanceRecovery(index, now);
            }
        }

        void AdvanceRecovery(int index, float now)
        {
            float elapsed = Mathf.Max(0f, now - lastRecoveryTimes[index]);
            if (elapsed > 0f)
            {
                RagdollMuscleBehaviourSettings settings =
                    GetBehaviourSettings(index);
                float positionRecoveryRate =
                    RagdollMuscleRecoveryMath.ResolvePositionRecoveryRate(
                        positionSuppressionRecoveryRate,
                        positionSuppressionRecoveryMultiplier,
                        settings.regainPositionAuthorityMultiplier);
                states[index].Recover(
                    positionRecoveryRate,
                    rotationSuppressionRecoveryRate,
                    elapsed);
            }

            lastRecoveryTimes[index] = now;
        }

        static float CurrentTime => Application.isPlaying ? Time.time : 0f;
    }
}
