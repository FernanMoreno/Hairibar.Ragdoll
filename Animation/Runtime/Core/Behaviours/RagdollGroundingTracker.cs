using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Pure stable-contact timer and snapshot builder.</summary>
    internal sealed class RagdollGroundingTracker
    {
        internal RagdollGroundingSnapshot Snapshot { get; private set; }
        bool hasPreviousFrame;
        bool previousGrounded;
        bool previousEffectiveUpAvailable;
        bool previousHasSupportPlatform;
        int previousSupportColliderId;
        int previousSupportRigidbodyId;
        Vector3 previousEffectiveUp;

        internal RagdollGroundingTracker()
        {
            Reset();
        }

        internal void Reset()
        {
            Snapshot = RagdollGroundingSnapshot.Empty;
            hasPreviousFrame = false;
            previousGrounded = false;
            previousEffectiveUpAvailable = false;
            previousHasSupportPlatform = false;
            previousSupportColliderId = 0;
            previousSupportRigidbodyId = 0;
            previousEffectiveUp = Vector3.up;
        }

        internal void Update(
            bool grounded,
            Vector3 point,
            Vector3 normal,
            Vector3 centerOfMass,
            Vector3 centerOfMassVelocity,
            float totalMass,
            float deltaTime,
            bool hasCenterOfPressure = false,
            Vector3 centerOfPressure = default(Vector3),
            Vector3 up = default(Vector3),
            bool effectiveUpAvailable = true,
            int supportColliderId = 0,
            int supportRigidbodyId = 0,
            bool hasSupportPlatform = false,
            Vector3 supportVelocity = default(Vector3),
            bool hasLeftFootSupport = false,
            Vector3 leftFootSupportPoint = default(Vector3),
            bool hasRightFootSupport = false,
            Vector3 rightFootSupportPoint = default(Vector3))
        {
            Vector3 resolvedUp = IsFinite(up) && up.sqrMagnitude > Mathf.Epsilon
                ? up.normalized
                : Vector3.up;
            bool validUp = effectiveUpAvailable
                && IsFinite(up)
                && up.sqrMagnitude > Mathf.Epsilon;
            bool sourceAvailable = grounded && hasSupportPlatform && supportColliderId != 0;
            bool continuity = hasPreviousFrame
                && previousGrounded
                && grounded
                && previousEffectiveUpAvailable == validUp
                && (!validUp || Vector3.Dot(previousEffectiveUp, resolvedUp) >= 0.9999f)
                && previousHasSupportPlatform == sourceAvailable
                && (!sourceAvailable
                    || (previousSupportColliderId == supportColliderId
                        && previousSupportRigidbodyId == supportRigidbodyId));
            bool continuityReset = hasPreviousFrame && previousGrounded && grounded && !continuity;
            bool hasRelativeMotion = continuity && sourceAvailable && IsFinite(supportVelocity);
            float stableTime = grounded && !continuityReset
                ? Snapshot.StableTime + Mathf.Max(0f, deltaTime)
                : 0f;

            Snapshot = new RagdollGroundingSnapshot(
                grounded,
                stableTime,
                grounded ? point : Vector3.zero,
                grounded ? normal : Vector3.up,
                centerOfMass,
                centerOfMassVelocity,
                totalMass,
                hasCenterOfPressure,
                centerOfPressure,
                resolvedUp,
                validUp,
                supportColliderId,
                supportRigidbodyId,
                sourceAvailable,
                supportVelocity,
                hasRelativeMotion,
                continuityReset,
                hasLeftFootSupport,
                leftFootSupportPoint,
                hasRightFootSupport,
                rightFootSupportPoint);

            hasPreviousFrame = true;
            previousGrounded = grounded;
            previousEffectiveUpAvailable = validUp;
            previousHasSupportPlatform = sourceAvailable;
            previousSupportColliderId = supportColliderId;
            previousSupportRigidbodyId = supportRigidbodyId;
            previousEffectiveUp = resolvedUp;
        }

        static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
