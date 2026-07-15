using System;
using UnityEngine;

namespace Hairibar.Ragdoll
{
    /// <summary>
    /// Runtime-adjustable PhysX quality values. These parameters are safe to swap without
    /// rebuilding joints, changing mass distribution or recalculating inertia tensors.
    /// </summary>
    [Serializable]
    public struct RagdollSolverQualitySettings
    {
        [Min(1)] public int solverIterations;
        [Min(1)] public int solverVelocityIterations;
        [Min(0f)] public float maxAngularVelocity;
        [Min(0f)] public float maxDepenetrationVelocity;
        public RigidbodyInterpolation interpolation;
        public CollisionDetectionMode collisionDetectionMode;

        public static RagdollSolverQualitySettings Create(
            int solverIterations,
            int solverVelocityIterations,
            float maxAngularVelocity,
            float maxDepenetrationVelocity,
            RigidbodyInterpolation interpolation,
            CollisionDetectionMode collisionDetectionMode)
        {
            return new RagdollSolverQualitySettings
            {
                solverIterations = solverIterations,
                solverVelocityIterations = solverVelocityIterations,
                maxAngularVelocity = maxAngularVelocity,
                maxDepenetrationVelocity = maxDepenetrationVelocity,
                interpolation = interpolation,
                collisionDetectionMode = collisionDetectionMode
            }.Sanitized();
        }

        public RagdollSolverQualitySettings Sanitized()
        {
            RagdollSolverQualitySettings result = this;
            result.solverIterations = Mathf.Max(1, solverIterations);
            result.solverVelocityIterations = Mathf.Max(1, solverVelocityIterations);
            result.maxAngularVelocity = SanitizeNonNegative(maxAngularVelocity);
            result.maxDepenetrationVelocity =
                SanitizeNonNegative(maxDepenetrationVelocity);

            if (!Enum.IsDefined(typeof(RigidbodyInterpolation), interpolation))
            {
                result.interpolation = RigidbodyInterpolation.None;
            }

            if (!Enum.IsDefined(
                typeof(CollisionDetectionMode),
                collisionDetectionMode))
            {
                result.collisionDetectionMode = CollisionDetectionMode.Discrete;
            }

            return result;
        }

        public bool TryValidate(out string error)
        {
            if (solverIterations < 1)
            {
                error = "Solver iterations must be positive.";
                return false;
            }

            if (solverVelocityIterations < 1)
            {
                error = "Solver velocity iterations must be positive.";
                return false;
            }

            if (!IsFiniteNonNegative(maxAngularVelocity))
            {
                error = "Maximum angular velocity must be finite and non-negative.";
                return false;
            }

            if (!IsFiniteNonNegative(maxDepenetrationVelocity))
            {
                error = "Maximum depenetration velocity must be finite and non-negative.";
                return false;
            }

            if (!Enum.IsDefined(typeof(RigidbodyInterpolation), interpolation))
            {
                error = "The Rigidbody interpolation value is invalid.";
                return false;
            }

            if (!Enum.IsDefined(
                typeof(CollisionDetectionMode),
                collisionDetectionMode))
            {
                error = "The collision detection mode is invalid.";
                return false;
            }

            error = null;
            return true;
        }

        internal static RagdollSolverQualitySettings FromAuthored(
            RagdollSettings settings)
        {
            if (!settings) throw new ArgumentNullException(nameof(settings));

            return Create(
                settings.solverIterations,
                settings.solverVelocityIterations,
                settings.maxAngularVelocity,
                settings.maxDepenetrationVelocity,
                settings.interpolation,
                settings.collisionDetectionMode);
        }

        static float SanitizeNonNegative(float value)
        {
            return IsFiniteNonNegative(value) ? value : 0f;
        }

        static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value)
                && !float.IsInfinity(value)
                && value >= 0f;
        }
    }
}
