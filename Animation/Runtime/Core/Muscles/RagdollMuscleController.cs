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
        float minimumPositionAuthorityMultiplier = 1f;
        float lifecyclePositionAuthorityMultiplier = 1f;
        float lifecycleMuscleWeightMultiplier = 1f;
        float lifecycleMuscleDamperAdd;
        bool hasActiveBoosts;
        bool combatBoostsEnabled;

        static readonly Dictionary<Rigidbody, BoostSourceRegistration>
            BoostSources =
                new Dictionary<Rigidbody, BoostSourceRegistration>();

        readonly List<Rigidbody> registeredBoostBodies =
            new List<Rigidbody>();

        RagdollDefinitionBindings bindings;
        RagdollMuscleProfileRuntime runtimeProfile;
        MuscleRuntimeState[] states;
        float[] lastRecoveryTimes;

        internal sealed class HierarchySnapshot
        {
            internal readonly Dictionary<BoneName, StateEntry> States;

            internal HierarchySnapshot(
                Dictionary<BoneName, StateEntry> states)
            {
                States = states;
            }
        }

        internal struct StateEntry
        {
            internal MuscleRuntimeState State;
            internal float RecoveryTime;
        }

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
        public float MinimumPositionAuthorityMultiplier =>
            minimumPositionAuthorityMultiplier;
        public float LifecyclePositionAuthorityMultiplier =>
            lifecyclePositionAuthorityMultiplier;
        public float LifecycleMuscleWeightMultiplier =>
            lifecycleMuscleWeightMultiplier;
        public float LifecycleMuscleDamperAdd => lifecycleMuscleDamperAdd;
        public bool HasActiveBoosts => hasActiveBoosts;
        internal bool CombatBoostsEnabled => combatBoostsEnabled;
        public float MaximumImmunity => GetMaximumImmunity();
        public float MaximumImpulseMultiplier => GetMaximumImpulseMultiplier();

        public void Initialize(IEnumerable<RagdollAnimator.AnimatedPair> pairs)
        {
            UnregisterBoostSources();

            RagdollAnimator animator = GetComponent<RagdollAnimator>();
            bindings = animator.Bindings;

            if (muscleProfile)
            {
                string profileError;
                if (!muscleProfile.TryCreateRuntime(
                    bindings,
                    animator.ResolveRuntimeMuscleGroup,
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
            lifecyclePositionAuthorityMultiplier = 1f;
            lifecycleMuscleWeightMultiplier = 1f;
            lifecycleMuscleDamperAdd = 0f;
            hasActiveBoosts = false;
            combatBoostsEnabled = false;

            foreach (RagdollAnimator.AnimatedPair pair in pairs)
            {
                if (!bindings.Topology.Contains(pair.Handle))
                {
                    throw new InvalidOperationException(
                        "RagdollMuscleController received an animated pair from another registry generation.");
                }
            }

            RegisterBoostSources();
        }

        internal HierarchySnapshot CaptureHierarchySnapshot(
            IEnumerable<RagdollAnimator.AnimatedPair> pairs)
        {
            EnsureInitialized();
            Dictionary<BoneName, StateEntry> captured =
                new Dictionary<BoneName, StateEntry>();
            foreach (RagdollAnimator.AnimatedPair pair in pairs)
            {
                int index = pair.Handle.Index;
                captured[pair.Name] = new StateEntry
                {
                    State = states[index],
                    RecoveryTime = lastRecoveryTimes[index]
                };
            }
            return new HierarchySnapshot(captured);
        }

        internal void RebuildHierarchy(
            IEnumerable<RagdollAnimator.AnimatedPair> pairs,
            HierarchySnapshot snapshot)
        {
            if (pairs == null) throw new ArgumentNullException(nameof(pairs));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            UnregisterBoostSources();
            RagdollAnimator animator = GetComponent<RagdollAnimator>();
            bindings = animator.Bindings;

            if (muscleProfile)
            {
                string profileError;
                if (!muscleProfile.TryCreateRuntime(
                    bindings,
                    animator.ResolveRuntimeMuscleGroup,
                    out runtimeProfile,
                    out profileError))
                {
                    throw new InvalidOperationException(
                        "The assigned RagdollMuscleProfile is invalid after hierarchy rebuild: "
                        + profileError);
                }
            }
            else
            {
                runtimeProfile = null;
            }

            states = new MuscleRuntimeState[bindings.BoneCount];
            lastRecoveryTimes = new float[bindings.BoneCount];
            float now = CurrentTime;
            foreach (RagdollAnimator.AnimatedPair pair in pairs)
            {
                StateEntry entry;
                if (snapshot.States.TryGetValue(pair.Name, out entry))
                {
                    states[pair.Handle.Index] = entry.State;
                    lastRecoveryTimes[pair.Handle.Index] = entry.RecoveryTime;
                }
                else
                {
                    states[pair.Handle.Index] = MuscleRuntimeState.Default;
                    lastRecoveryTimes[pair.Handle.Index] = now;
                }
            }

            RecalculateHasActiveBoosts();
            RegisterBoostSources();
        }

        public void Modify(ref BoneProfile boneProfile, RagdollAnimator.AnimatedPair pair, float dt)
        {
            if (!enabled || states == null) return;

            int index = pair.Handle.Index;
            AdvanceRecovery(index, CurrentTime);
            states[index].ApplyTo(
                ref boneProfile,
                GetAppliedMinimumPositionAuthority(index));
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

        public void Boost(float immunity, float impulseMlp)
        {
            BoostImmunity(immunity);
            BoostImpulseMlp(impulseMlp);
        }

        public void Boost(
            RagdollBoneHandle bone,
            float immunity,
            float impulseMlp)
        {
            BoostImmunity(bone, immunity);
            BoostImpulseMlp(bone, impulseMlp);
        }

        public void Boost(
            RagdollBoneHandle bone,
            float immunity,
            float impulseMlp,
            float boostParents,
            float boostChildren)
        {
            BoostImmunity(bone, immunity, boostParents, boostChildren);
            BoostImpulseMlp(bone, impulseMlp, boostParents, boostChildren);
        }

        public int Boost(
            RagdollMuscleGroup group,
            float immunity,
            float impulseMlp)
        {
            int immunityCount = BoostImmunity(group, immunity);
            int impulseCount = BoostImpulseMlp(group, impulseMlp);
            return Mathf.Max(immunityCount, impulseCount);
        }

        public void BoostImmunity(float immunity)
        {
            EnsureInitialized();
            for (int index = 0; index < states.Length; index++)
            {
                hasActiveBoosts |= states[index].BoostImmunity(immunity);
            }
        }

        public void BoostImmunity(
            RagdollBoneHandle bone,
            float immunity)
        {
            int index = ValidateHandle(bone);
            hasActiveBoosts |= states[index].BoostImmunity(immunity);
        }

        public void BoostImmunity(
            RagdollBoneHandle bone,
            float immunity,
            float boostParents,
            float boostChildren)
        {
            ApplyDirectionalBoost(
                bone,
                immunity,
                boostParents,
                boostChildren,
                true);
        }

        public int BoostImmunity(
            RagdollMuscleGroup group,
            float immunity)
        {
            return ApplyGroupBoost(group, immunity, true);
        }

        public void BoostImpulseMlp(float impulseMlp)
        {
            EnsureInitialized();
            for (int index = 0; index < states.Length; index++)
            {
                hasActiveBoosts |=
                    states[index].BoostImpulseMultiplier(impulseMlp);
            }
        }

        public void BoostImpulseMlp(
            RagdollBoneHandle bone,
            float impulseMlp)
        {
            int index = ValidateHandle(bone);
            hasActiveBoosts |=
                states[index].BoostImpulseMultiplier(impulseMlp);
        }

        public void BoostImpulseMlp(
            RagdollBoneHandle bone,
            float impulseMlp,
            float boostParents,
            float boostChildren)
        {
            ApplyDirectionalBoost(
                bone,
                impulseMlp,
                boostParents,
                boostChildren,
                false);
        }

        public int BoostImpulseMlp(
            RagdollMuscleGroup group,
            float impulseMlp)
        {
            return ApplyGroupBoost(group, impulseMlp, false);
        }

        public float GetImmunity(RagdollBoneHandle bone)
        {
            return states[ValidateHandle(bone)].Immunity;
        }

        public float GetImpulseMultiplier(RagdollBoneHandle bone)
        {
            return states[ValidateHandle(bone)].ImpulseMultiplier;
        }

        internal void SetCombatBoostsEnabled(bool enabled)
        {
            combatBoostsEnabled = enabled;
        }

        internal void SetLifecycleDrive(
            float positionAuthorityMultiplier,
            float muscleWeightMultiplier,
            float muscleDamperAdd)
        {
            lifecyclePositionAuthorityMultiplier =
                RagdollLifecycleMath.SanitizeWeight(
                    positionAuthorityMultiplier,
                    1f);
            lifecycleMuscleWeightMultiplier =
                RagdollLifecycleMath.SanitizeWeight(
                    muscleWeightMultiplier,
                    1f);
            lifecycleMuscleDamperAdd =
                RagdollLifecycleMath.SanitizeNonNegative(
                    muscleDamperAdd,
                    0f);
        }

        internal void ClearLifecycleDrive()
        {
            SetLifecycleDrive(1f, 1f, 0f);
        }

        internal void ClearAllImmunity()
        {
            if (states == null) return;

            for (int index = 0; index < states.Length; index++)
            {
                states[index].ClearImmunity();
            }
            RecalculateHasActiveBoosts();
        }

        internal void AdvanceBoostFalloff(float falloff, float deltaTime)
        {
            if (!combatBoostsEnabled
                || !hasActiveBoosts
                || states == null
                || falloff <= 0f
                || deltaTime <= 0f)
            {
                return;
            }

            bool anyActive = false;
            for (int index = 0; index < states.Length; index++)
            {
                states[index].AdvanceBoostFalloff(falloff, deltaTime);
                anyActive |= states[index].HasActiveBoost;
            }

            hasActiveBoosts = anyActive;
        }

        internal static float ResolveExternalImpulseMultiplier(
            Rigidbody sourceRigidbody,
            RagdollMuscleController receivingController)
        {
            if (!sourceRigidbody) return 1f;

            BoostSourceRegistration registration;
            if (!BoostSources.TryGetValue(sourceRigidbody, out registration))
            {
                return 1f;
            }

            RagdollMuscleController sourceController = registration.Controller;
            if (!sourceController || !sourceController.IsInitialized)
            {
                BoostSources.Remove(sourceRigidbody);
                return 1f;
            }

            if (!sourceController.isActiveAndEnabled
                || !sourceController.combatBoostsEnabled)
            {
                return 1f;
            }

            if (ReferenceEquals(sourceController, receivingController))
            {
                return 1f;
            }

            return sourceController.GetImpulseMultiplier(registration.Bone);
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
                GetAppliedMinimumPositionAuthority(index));
        }

        internal float GetAppliedMinimumPositionAuthority(
            RagdollBoneHandle bone)
        {
            return GetAppliedMinimumPositionAuthority(ValidateHandle(bone));
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

        internal void SetMinimumPositionAuthorityMultiplier(float multiplier)
        {
            minimumPositionAuthorityMultiplier =
                RagdollMuscleRecoveryMath.SanitizeWeight(multiplier, 1f);
        }

        internal void ClearMinimumPositionAuthorityMultiplier()
        {
            SetMinimumPositionAuthorityMultiplier(1f);
        }

        float GetAppliedMinimumPositionAuthority(int index)
        {
            return RagdollMuscleRecoveryMath.ResolveMinimumPositionAuthority(
                GetBehaviourSettings(index).minimumPositionAuthority,
                minimumPositionAuthorityMultiplier);
        }

        internal void SetAllPositionSuppressions(float suppression)
        {
            EnsureInitialized();

            float now = CurrentTime;
            AdvanceAllRecovery(now);
            float value = RagdollMuscleRecoveryMath.SanitizeWeight(
                suppression,
                0f);
            for (int index = 0; index < states.Length; index++)
            {
                states[index].SetPositionSuppression(value);
                lastRecoveryTimes[index] = now;
            }
        }

        internal void ClearAllSuppressions()
        {
            EnsureInitialized();

            float now = CurrentTime;
            for (int index = 0; index < states.Length; index++)
            {
                states[index].ClearSuppression();
                lastRecoveryTimes[index] = now;
            }
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
        internal float ApplyResolvedImpact(
            RagdollBoneHandle source,
            MuscleImpactSettings settings)
        {
            ValidateImpactSource(source);
            return ApplyImpactCore(
                source,
                settings,
                Mathf.Clamp01(settings.positionSuppression));
        }

        float ApplyImpactCore(
            RagdollBoneHandle source,
            MuscleImpactSettings settings,
            float sourcePositionSuppression)
        {
            RagdollBoneTopology topology = bindings.Topology;
            RagdollMuscleBehaviourSettings sourceSettings =
                GetBehaviourSettings(source.Index);
            float now = CurrentTime;
            float sourceAppliedPositionSuppression = 0f;

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

                float receivingImmunity = combatBoostsEnabled
                    ? states[index].Immunity
                    : 0f;
                float positionSuppression =
                    RagdollMuscleBoostMath.ApplyImmunity(
                        sourcePositionSuppression * positionWeight,
                        receivingImmunity);
                float rotationSuppression =
                    RagdollMuscleBoostMath.ApplyImmunity(
                        settings.rotationSuppression * distanceWeight,
                        receivingImmunity);
                if (source == affected)
                {
                    sourceAppliedPositionSuppression = positionSuppression;
                }
                if (positionSuppression <= 0f && rotationSuppression <= 0f) continue;

                AdvanceRecovery(index, now);
                states[index].AccumulateSuppression(
                    positionSuppression,
                    rotationSuppression);
            }

            return sourceAppliedPositionSuppression;
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
            RecalculateHasActiveBoosts();
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
            hasActiveBoosts = false;
        }

        void ApplyDirectionalBoost(
            RagdollBoneHandle source,
            float value,
            float boostParents,
            float boostChildren,
            bool immunity)
        {
            ValidateHandle(source);
            RagdollBoneTopology topology = bindings.Topology;
            for (int index = 0; index < states.Length; index++)
            {
                RagdollBoneHandle affected = bindings.GetHandleAt(index);
                float falloff =
                    RagdollMuscleBoostMath.EvaluateDirectionalFalloff(
                        topology,
                        source,
                        affected,
                        boostParents,
                        boostChildren);
                if (falloff <= 0f) continue;

                bool changed = immunity
                    ? states[index].BoostImmunity(value * falloff)
                    : states[index].BoostImpulseMultiplier(value * falloff);
                hasActiveBoosts |= changed;
            }
        }

        int ApplyGroupBoost(
            RagdollMuscleGroup group,
            float value,
            bool immunity)
        {
            EnsureInitialized();
            if (runtimeProfile == null) return 0;

            int affectedCount = 0;
            for (int index = 0; index < states.Length; index++)
            {
                if (runtimeProfile.GetGroup(index) != group) continue;

                affectedCount++;
                bool changed = immunity
                    ? states[index].BoostImmunity(value)
                    : states[index].BoostImpulseMultiplier(value);
                hasActiveBoosts |= changed;
            }

            return affectedCount;
        }

        float GetMaximumImmunity()
        {
            if (states == null) return 0f;

            float maximum = 0f;
            for (int index = 0; index < states.Length; index++)
            {
                maximum = Mathf.Max(maximum, states[index].Immunity);
            }
            return maximum;
        }

        float GetMaximumImpulseMultiplier()
        {
            if (states == null) return 1f;

            float maximum = 1f;
            for (int index = 0; index < states.Length; index++)
            {
                maximum = Mathf.Max(
                    maximum,
                    states[index].ImpulseMultiplier);
            }
            return maximum;
        }

        void RecalculateHasActiveBoosts()
        {
            hasActiveBoosts = false;
            if (states == null) return;

            for (int index = 0; index < states.Length; index++)
            {
                if (!states[index].HasActiveBoost) continue;
                hasActiveBoosts = true;
                return;
            }
        }

        void RegisterBoostSources()
        {
            if (bindings == null || !bindings.IsInitialized) return;

            // Validate the complete batch before mutating the static registry. Initialization
            // must not leave a partially registered puppet when one Rigidbody conflicts.
            for (int index = 0; index < bindings.BoneCount; index++)
            {
                Rigidbody rigidbody = bindings.GetBoneAt(index).Rigidbody;
                if (!rigidbody) continue;

                BoostSourceRegistration existing;
                if (!BoostSources.TryGetValue(rigidbody, out existing)) continue;

                if (!existing.Controller)
                {
                    BoostSources.Remove(rigidbody);
                    continue;
                }

                if (!ReferenceEquals(existing.Controller, this))
                {
                    throw new InvalidOperationException(
                        "A Rigidbody cannot be registered with more than one "
                        + "RagdollMuscleController boost source.");
                }
            }

            for (int index = 0; index < bindings.BoneCount; index++)
            {
                Rigidbody rigidbody = bindings.GetBoneAt(index).Rigidbody;
                if (!rigidbody) continue;

                BoostSources[rigidbody] = new BoostSourceRegistration(
                    this,
                    bindings.GetHandleAt(index));
                registeredBoostBodies.Add(rigidbody);
            }
        }

        void UnregisterBoostSources()
        {
            for (int index = 0; index < registeredBoostBodies.Count; index++)
            {
                Rigidbody rigidbody = registeredBoostBodies[index];
                if (ReferenceEquals(rigidbody, null)) continue;

                BoostSourceRegistration registration;
                if (BoostSources.TryGetValue(rigidbody, out registration)
                    && ReferenceEquals(registration.Controller, this))
                {
                    BoostSources.Remove(rigidbody);
                }
            }

            registeredBoostBodies.Clear();
        }

        void OnDestroy()
        {
            UnregisterBoostSources();
        }

        sealed class BoostSourceRegistration
        {
            internal readonly RagdollMuscleController Controller;
            internal readonly RagdollBoneHandle Bone;

            internal BoostSourceRegistration(
                RagdollMuscleController controller,
                RagdollBoneHandle bone)
            {
                Controller = controller;
                Bone = bone;
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
