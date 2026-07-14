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
        [SerializeField, Min(0f)] float positionSuppressionRecoveryRate = 2f;
        [SerializeField, Min(0f)] float rotationSuppressionRecoveryRate = 2f;

        RagdollDefinitionBindings bindings;
        MuscleRuntimeState[] states;
        float[] lastRecoveryTimes;

        public bool IsInitialized => states != null;
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

        public void Initialize(IEnumerable<RagdollAnimator.AnimatedPair> pairs)
        {
            RagdollAnimator animator = GetComponent<RagdollAnimator>();
            bindings = animator.Bindings;

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
            states[index].ApplyTo(ref boneProfile);
        }

        public void ModifyMapping(
            ref RagdollMappingWeights mappingWeights,
            RagdollAnimator.AnimatedPair pair)
        {
            if (!enabled || states == null) return;

            states[pair.Handle.Index].ApplyTo(ref mappingWeights);
        }

        public MuscleRuntimeState GetState(RagdollBoneHandle bone)
        {
            int index = ValidateHandle(bone);
            AdvanceRecovery(index, CurrentTime);
            return states[index];
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
            EnsureInitialized();

            RagdollBoneTopology topology = bindings.Topology;
            if (!topology.Contains(source))
            {
                throw new ArgumentException(
                    "The supplied impact source does not belong to this muscle controller.",
                    nameof(source));
            }

            float now = CurrentTime;
            for (int index = 0; index < states.Length; index++)
            {
                RagdollBoneHandle affected = bindings.GetHandleAt(index);
                int distance = topology.GetKinshipDistance(source, affected);
                float weight = settings.GetPropagationWeight(distance);
                if (weight <= 0f) continue;

                AdvanceRecovery(index, now);
                states[index].AccumulateSuppression(
                    settings.positionSuppression * weight,
                    settings.rotationSuppression * weight);
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

        void AdvanceRecovery(int index, float now)
        {
            float elapsed = Mathf.Max(0f, now - lastRecoveryTimes[index]);
            if (elapsed > 0f)
            {
                states[index].Recover(
                    positionSuppressionRecoveryRate,
                    rotationSuppressionRecoveryRate,
                    elapsed);
            }

            lastRecoveryTimes[index] = now;
        }

        static float CurrentTime => Application.isPlaying ? Time.time : 0f;
    }
}
