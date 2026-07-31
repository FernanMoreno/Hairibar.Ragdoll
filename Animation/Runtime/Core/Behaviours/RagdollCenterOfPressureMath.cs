using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    internal static class RagdollCenterOfPressureMath
    {
        internal static void Accumulate(
            Vector3 contactPoint,
            float impulseMagnitude,
            ref Vector3 weightedPressure,
            ref float totalWeight,
            ref int contactCount)
        {
            float weight = impulseMagnitude;
            if (float.IsNaN(weight)
                || float.IsInfinity(weight)
                || weight <= Mathf.Epsilon)
            {
                weight = 1f;
            }

            weightedPressure += contactPoint * weight;
            totalWeight += weight;
            contactCount++;
        }

        internal static bool Resolve(
            Vector3 weightedPressure,
            float totalWeight,
            int contactCount,
            out Vector3 centerOfPressure)
        {
            if (contactCount <= 0
                || float.IsNaN(totalWeight)
                || float.IsInfinity(totalWeight)
                || totalWeight <= Mathf.Epsilon)
            {
                centerOfPressure = Vector3.zero;
                return false;
            }

            centerOfPressure = weightedPressure / totalWeight;
            return IsFinite(centerOfPressure);
        }

        static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
