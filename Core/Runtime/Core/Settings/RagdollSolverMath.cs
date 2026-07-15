using UnityEngine;

namespace Hairibar.Ragdoll
{
    /// <summary>Pure solver and inertia helpers shared by RagdollSettings and tests.</summary>
    internal static class RagdollSolverMath
    {
        internal static float GetSafeFallbackMass(float mass)
        {
            return IsFinitePositive(mass) ? mass : 1f;
        }

        internal static float ResolveAngularDriveMass(
            float rigidbodyMass,
            Vector3 inertiaTensor,
            RagdollAngularDriveInertiaMode mode)
        {
            float fallback = GetSafeFallbackMass(rigidbodyMass);
            if (mode == RagdollAngularDriveInertiaMode.RigidbodyMass)
            {
                return fallback;
            }

            float sum = 0f;
            float maximum = 0f;
            int count = 0;
            AccumulatePositive(inertiaTensor.x, ref sum, ref maximum, ref count);
            AccumulatePositive(inertiaTensor.y, ref sum, ref maximum, ref count);
            AccumulatePositive(inertiaTensor.z, ref sum, ref maximum, ref count);

            if (count == 0) return fallback;

            switch (mode)
            {
                case RagdollAngularDriveInertiaMode.AverageInertia:
                    return Mathf.Max(Mathf.Epsilon, sum / count);
                case RagdollAngularDriveInertiaMode.MaximumInertia:
                    return Mathf.Max(Mathf.Epsilon, maximum);
                default:
                    return fallback;
            }
        }

        internal static Vector3 StabilizeInertiaTensor(
            Vector3 inertiaTensor,
            float maximumPrincipalRatio)
        {
            float largest = 0f;
            IncludeLargestPositive(inertiaTensor.x, ref largest);
            IncludeLargestPositive(inertiaTensor.y, ref largest);
            IncludeLargestPositive(inertiaTensor.z, ref largest);
            if (largest <= Mathf.Epsilon) return inertiaTensor;

            float ratio = IsFinitePositive(maximumPrincipalRatio)
                ? Mathf.Max(1f, maximumPrincipalRatio)
                : 1f;
            float minimumAllowed = largest / ratio;

            return new Vector3(
                RaisePositiveComponent(inertiaTensor.x, minimumAllowed),
                RaisePositiveComponent(inertiaTensor.y, minimumAllowed),
                RaisePositiveComponent(inertiaTensor.z, minimumAllowed));
        }

        static void AccumulatePositive(
            float value,
            ref float sum,
            ref float maximum,
            ref int count)
        {
            if (!IsFinitePositive(value)) return;

            sum += value;
            maximum = Mathf.Max(maximum, value);
            count++;
        }

        static void IncludeLargestPositive(float value, ref float largest)
        {
            if (IsFinitePositive(value))
            {
                largest = Mathf.Max(largest, value);
            }
        }

        static float RaisePositiveComponent(float value, float minimumAllowed)
        {
            // Zero components may represent locked rotational axes. Preserve zero and any
            // invalid authored value rather than silently changing constraints.
            return IsFinitePositive(value)
                ? Mathf.Max(value, minimumAllowed)
                : value;
        }

        static bool IsFinitePositive(float value)
        {
            return value > 0f
                && !float.IsNaN(value)
                && !float.IsInfinity(value);
        }
    }
}
