using System;
using Hairibar.NaughtyExtensions;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Converts collision impulse into temporary, topologically propagated loss of
    /// animation authority. Uses RagdollMuscleController as the single runtime state owner.
    /// </summary>
    [AddComponentMenu("Ragdoll/Ragdoll Collision Reaction")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RagdollAnimator), typeof(RagdollMuscleController))]
    public class RagdollCollisionReaction : MonoBehaviour
    {
        public LayerMask collisionMask = -1;
        public bool softenPositionMatching = true;
        public bool softenRotationMatching = false;
        public bool reactOnCollisionStay = true;
        public bool configureControllerRecovery = true;

        public event Action<RagdollBoneHandle, float> ImpactApplied;

        public float SofteningAmount
        {
            get => softeningAmount;
            set => softeningAmount = Mathf.Clamp01(value);
        }
        [SerializeField, Range(0f, 1f)] float softeningAmount = 1f;

        public float RecoveryTime
        {
            get => _recoveryTime;
            set
            {
                _recoveryTime = Mathf.Max(0f, value);
                ApplyRecoverySettings();
            }
        }
        [SerializeField, UsePropertySetter] float _recoveryTime = 0.5f;

        public float MinimumImpulse
        {
            get => minimumImpulse;
            set
            {
                minimumImpulse = Mathf.Max(0f, value);
                fullSuppressionImpulse = Mathf.Max(minimumImpulse, fullSuppressionImpulse);
            }
        }
        [SerializeField, Min(0f)] float minimumImpulse = 0.05f;

        public float FullSuppressionImpulse
        {
            get => fullSuppressionImpulse;
            set => fullSuppressionImpulse = Mathf.Max(minimumImpulse, value);
        }
        [SerializeField, Min(0f)] float fullSuppressionImpulse = 1f;

        public int MaximumPropagationDistance
        {
            get => maximumPropagationDistance;
            set => maximumPropagationDistance = Mathf.Max(0, value);
        }
        [SerializeField, Min(0)] int maximumPropagationDistance = 0;

        public float PropagationFalloff
        {
            get => propagationFalloff;
            set => propagationFalloff = Mathf.Clamp01(value);
        }
        [SerializeField, Range(0f, 1f)] float propagationFalloff = 0.5f;

        RagdollMuscleController muscleController;
        RagdollCollisionHub collisionHub;

        /// <summary>
        /// Applies the same response used by physical collision callbacks to a code-driven hit.
        /// Returns false when the impulse is below threshold or initialization is incomplete.
        /// </summary>
        public bool ApplyImpact(RagdollBoneHandle bone, float impulseMagnitude)
        {
            if (!muscleController || !muscleController.IsInitialized) return false;

            float response = EvaluateImpactResponse(
                impulseMagnitude,
                minimumImpulse,
                fullSuppressionImpulse);
            if (response <= 0f) return false;

            MuscleImpactSettings settings = new MuscleImpactSettings
            {
                positionSuppression = softenPositionMatching
                    ? softeningAmount * response
                    : 0f,
                rotationSuppression = softenRotationMatching
                    ? softeningAmount * response
                    : 0f,
                maximumPropagationDistance = Mathf.Max(0, maximumPropagationDistance),
                propagationFalloff = Mathf.Clamp01(propagationFalloff)
            };

            if (settings.positionSuppression <= 0f && settings.rotationSuppression <= 0f)
            {
                return false;
            }

            muscleController.ApplyImpact(bone, settings);
            ImpactApplied?.Invoke(bone, response);
            return true;
        }

        internal static float EvaluateImpactResponse(
            float impulseMagnitude,
            float minimumImpulse,
            float fullSuppressionImpulse)
        {
            float impulse = Mathf.Max(0f, impulseMagnitude);
            float minimum = Mathf.Max(0f, minimumImpulse);
            float full = Mathf.Max(minimum, fullSuppressionImpulse);

            if (impulse < minimum) return 0f;
            if (full <= minimum + Mathf.Epsilon) return 1f;

            return Mathf.InverseLerp(minimum, full, impulse);
        }

        void HandleCollisionEnter(RagdollCollisionEvent collisionEvent)
        {
            HandleCollision(collisionEvent);
        }

        void HandleCollisionStay(RagdollCollisionEvent collisionEvent)
        {
            if (reactOnCollisionStay)
            {
                HandleCollision(collisionEvent);
            }
        }

        void HandleCollision(RagdollCollisionEvent collisionEvent)
        {
            if (!LayerIsEnabled(collisionEvent.OtherLayer)) return;
            ApplyImpact(collisionEvent.Bone, collisionEvent.ImpulseMagnitude);
        }

        bool LayerIsEnabled(int layer)
        {
            return layer >= 0
                && layer < 32
                && (collisionMask.value & (1 << layer)) != 0;
        }

        void ApplyRecoverySettings()
        {
            if (!configureControllerRecovery || !muscleController) return;

            float recoveryRate = _recoveryTime <= Mathf.Epsilon
                ? float.MaxValue
                : 1f / _recoveryTime;

            if (softenPositionMatching)
            {
                muscleController.PositionSuppressionRecoveryRate = recoveryRate;
            }

            if (softenRotationMatching)
            {
                muscleController.RotationSuppressionRecoveryRate = recoveryRate;
            }
        }

        void OnEnable()
        {
            RagdollAnimator animator = GetComponent<RagdollAnimator>();
            muscleController = GetComponent<RagdollMuscleController>();
            if (!muscleController)
            {
                muscleController = gameObject.AddComponent<RagdollMuscleController>();
            }

            RagdollDefinitionBindings bindings = animator.Bindings;
            collisionHub = bindings.GetComponent<RagdollCollisionHub>();
            if (!collisionHub)
            {
                collisionHub = bindings.gameObject.AddComponent<RagdollCollisionHub>();
            }

            collisionHub.CollisionEntered += HandleCollisionEnter;
            collisionHub.CollisionStayed += HandleCollisionStay;
            ApplyRecoverySettings();
        }

        void OnDisable()
        {
            if (collisionHub)
            {
                collisionHub.CollisionEntered -= HandleCollisionEnter;
                collisionHub.CollisionStayed -= HandleCollisionStay;
            }
        }

        void OnValidate()
        {
            softeningAmount = Mathf.Clamp01(softeningAmount);
            _recoveryTime = Mathf.Max(0f, _recoveryTime);
            minimumImpulse = Mathf.Max(0f, minimumImpulse);
            fullSuppressionImpulse = Mathf.Max(minimumImpulse, fullSuppressionImpulse);
            maximumPropagationDistance = Mathf.Max(0, maximumPropagationDistance);
            propagationFalloff = Mathf.Clamp01(propagationFalloff);

            if (Application.isPlaying)
            {
                ApplyRecoverySettings();
            }
        }
    }
}
