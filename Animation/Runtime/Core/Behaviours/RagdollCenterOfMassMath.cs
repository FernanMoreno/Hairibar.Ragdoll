using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Allocation-free mass-weighted accumulation shared by runtime and tests.</summary>
    internal static class RagdollCenterOfMassMath
    {
        internal static void Accumulate(
            float mass,
            Vector3 worldCenterOfMass,
            Vector3 velocity,
            ref float totalMass,
            ref Vector3 weightedPosition,
            ref Vector3 weightedVelocity)
        {
            float positiveMass = Mathf.Max(0f, mass);
            if (positiveMass <= Mathf.Epsilon) return;

            totalMass += positiveMass;
            weightedPosition += worldCenterOfMass * positiveMass;
            weightedVelocity += velocity * positiveMass;
        }

        internal static void Resolve(
            float totalMass,
            Vector3 weightedPosition,
            Vector3 weightedVelocity,
            out Vector3 centerOfMass,
            out Vector3 centerOfMassVelocity)
        {
            if (totalMass <= Mathf.Epsilon)
            {
                centerOfMass = Vector3.zero;
                centerOfMassVelocity = Vector3.zero;
                return;
            }

            float inverseMass = 1f / totalMass;
            centerOfMass = weightedPosition * inverseMass;
            centerOfMassVelocity = weightedVelocity * inverseMass;
        }
    }
}
