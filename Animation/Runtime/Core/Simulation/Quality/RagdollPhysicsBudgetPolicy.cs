using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Pure deterministic ordering used by the shared active-ragdoll budget.</summary>
    internal static class RagdollPhysicsBudgetPolicy
    {
        internal static int Compare(
            int priorityA,
            float squaredDistanceA,
            bool retainedA,
            int stableIdA,
            int priorityB,
            float squaredDistanceB,
            bool retainedB,
            int stableIdB,
            float retentionDistance)
        {
            int priorityComparison = priorityB.CompareTo(priorityA);
            if (priorityComparison != 0) return priorityComparison;

            float retention = Mathf.Max(0f, retentionDistance);
            float adjustedA = Mathf.Sqrt(Mathf.Max(0f, squaredDistanceA))
                - (retainedA ? retention : 0f);
            float adjustedB = Mathf.Sqrt(Mathf.Max(0f, squaredDistanceB))
                - (retainedB ? retention : 0f);

            int distanceComparison = adjustedA.CompareTo(adjustedB);
            return distanceComparison != 0
                ? distanceComparison
                : stableIdA.CompareTo(stableIdB);
        }
    }
}
