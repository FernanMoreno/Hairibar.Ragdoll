using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Pure stable-contact timer and snapshot builder.</summary>
    internal sealed class RagdollGroundingTracker
    {
        internal RagdollGroundingSnapshot Snapshot { get; private set; }

        internal RagdollGroundingTracker()
        {
            Reset();
        }

        internal void Reset()
        {
            Snapshot = RagdollGroundingSnapshot.Empty;
        }

        internal void Update(
            bool grounded,
            Vector3 point,
            Vector3 normal,
            Vector3 centerOfMass,
            Vector3 centerOfMassVelocity,
            float totalMass,
            float deltaTime)
        {
            float stableTime = grounded
                ? Snapshot.StableTime + Mathf.Max(0f, deltaTime)
                : 0f;

            Snapshot = new RagdollGroundingSnapshot(
                grounded,
                stableTime,
                grounded ? point : Vector3.zero,
                grounded ? normal : Vector3.up,
                centerOfMass,
                centerOfMassVelocity,
                totalMass);
        }
    }
}
