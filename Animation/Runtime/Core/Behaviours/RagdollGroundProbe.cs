using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Reusable, allocation-free ground probe based on the mass-weighted puppet center.
    /// Self colliders are skipped so groundLayers may safely include the ragdoll layer.
    /// </summary>
    internal sealed class RagdollGroundProbe
    {
        const int RaycastCapacity = 32;

        readonly RagdollBehaviourContext context;
        readonly RagdollGroundingTracker tracker = new RagdollGroundingTracker();
        RaycastHit[] hits = new RaycastHit[RaycastCapacity];
        Vector3 weightedPressure;
        float pressureWeight;
        int pressureContactCount;
        RagdollBoneHandle leftFootHandle = RagdollBoneHandle.Invalid;
        RagdollBoneHandle rightFootHandle = RagdollBoneHandle.Invalid;
        Vector3 leftFootSupportPoint;
        Vector3 rightFootSupportPoint;
        int leftFootSupportCount;
        int rightFootSupportCount;

        internal RagdollGroundingSnapshot Snapshot => tracker.Snapshot;
        internal Vector3 Up { get; private set; }

        internal RagdollGroundProbe(RagdollBehaviourContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            this.context = context;
            Up = Vector3.up;
        }

        internal void Reset()
        {
            tracker.Reset();
            ClearPressureContacts();
        }

        internal void ConfigureFootSupport(
            RagdollBoneHandle leftFoot,
            RagdollBoneHandle rightFoot)
        {
            leftFootHandle = leftFoot;
            rightFootHandle = rightFoot;
            leftFootSupportPoint = Vector3.zero;
            rightFootSupportPoint = Vector3.zero;
            leftFootSupportCount = 0;
            rightFootSupportCount = 0;
        }

        internal void RegisterCollision(
            RagdollCollisionEvent collisionEvent,
            LayerMask groundLayers,
            float maximumGroundAngle)
        {
            if ((collisionEvent.Phase != RagdollCollisionPhase.Enter
                    && collisionEvent.Phase != RagdollCollisionPhase.Stay)
                || !collisionEvent.HasContact
                || collisionEvent.OtherLayer < 0
                || collisionEvent.OtherLayer >= 32
                || (groundLayers.value & (1 << collisionEvent.OtherLayer)) == 0)
            {
                return;
            }

            Vector3 gravity = Physics.gravity;
            bool effectiveUpAvailable = IsFinite(gravity)
                && gravity.sqrMagnitude > Mathf.Epsilon;
            Vector3 up = effectiveUpAvailable
                ? -gravity.normalized
                : Vector3.up;
            float minimumGroundDot = Mathf.Cos(
                Mathf.Clamp(maximumGroundAngle, 0f, 89.9f) * Mathf.Deg2Rad);
            Collision collision = collisionEvent.Collision;
            if (collision != null && collision.contactCount > 0)
            {
                // Unity 6 exposes the impulse of each ContactPoint. Preserve that
                // distribution: averaging the points first and weighting with the
                // aggregate Collision.impulse produces a false pressure center.
                for (int index = 0; index < collision.contactCount; index++)
                {
                    ContactPoint contact = collision.GetContact(index);
                    AccumulateGroundContact(
                        contact.point,
                        contact.normal,
                        contact.impulse.magnitude,
                        up,
                        minimumGroundDot,
                        ref weightedPressure,
                        ref pressureWeight,
                        ref pressureContactCount);
                }
                AccumulateFootSupportForEvent(
                    collisionEvent,
                    groundLayers,
                    up,
                    minimumGroundDot);
                return;
            }

            AccumulateGroundContact(
                collisionEvent.ContactPoint,
                collisionEvent.ContactNormal,
                collisionEvent.ImpulseMagnitude,
                up,
                minimumGroundDot,
                ref weightedPressure,
                ref pressureWeight,
                ref pressureContactCount);

            AccumulateFootSupportForEvent(
                collisionEvent,
                groundLayers,
                up,
                minimumGroundDot);
        }

        void AccumulateFootSupportForEvent(
            RagdollCollisionEvent collisionEvent,
            LayerMask groundLayers,
            Vector3 up,
            float minimumGroundDot)
        {
            bool isRagdollCollider = collisionEvent.OtherCollider
                && IsRagdollCollider(collisionEvent.OtherCollider);
            bool isGroundLayer = collisionEvent.OtherLayer >= 0
                && collisionEvent.OtherLayer < 32
                && (groundLayers.value & (1 << collisionEvent.OtherLayer)) != 0;
            if (!IsValidFootSupportSample(
                collisionEvent.Bone,
                leftFootHandle,
                rightFootHandle,
                collisionEvent.HasContact,
                isGroundLayer,
                isRagdollCollider,
                collisionEvent.ContactPoint,
                collisionEvent.ContactNormal,
                up,
                minimumGroundDot))
            {
                return;
            }

            if (collisionEvent.Bone.Equals(leftFootHandle))
            {
                AccumulateFootSupport(
                    collisionEvent.ContactPoint,
                    collisionEvent.ContactNormal,
                    up,
                    minimumGroundDot,
                    ref leftFootSupportPoint,
                    ref leftFootSupportCount);
            }
            else if (collisionEvent.Bone.Equals(rightFootHandle))
            {
                AccumulateFootSupport(
                    collisionEvent.ContactPoint,
                    collisionEvent.ContactNormal,
                    up,
                    minimumGroundDot,
                    ref rightFootSupportPoint,
                    ref rightFootSupportCount);
            }
        }

        internal static bool IsValidFootSupportSample(
            RagdollBoneHandle bone,
            RagdollBoneHandle leftFoot,
            RagdollBoneHandle rightFoot,
            bool hasContact,
            bool isGroundLayer,
            bool isRagdollCollider,
            Vector3 point,
            Vector3 normal,
            Vector3 up,
            float minimumGroundDot)
        {
            if (!hasContact || !isGroundLayer || isRagdollCollider
                || (!bone.Equals(leftFoot) && !bone.Equals(rightFoot)))
            {
                return false;
            }
            if (!IsFinite(point) || !IsFinite(normal)
                || !IsFinite(up) || up.sqrMagnitude <= Mathf.Epsilon
                || normal.sqrMagnitude <= Mathf.Epsilon)
            {
                return false;
            }

            return Vector3.Dot(normal.normalized, up.normalized)
                >= Mathf.Clamp01(minimumGroundDot);
        }

        static void AccumulateFootSupport(
            Vector3 point,
            Vector3 normal,
            Vector3 up,
            float minimumGroundDot,
            ref Vector3 pointSum,
            ref int pointCount)
        {
            if (!IsFinite(point) || !IsFinite(normal)
                || normal.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            Vector3 resolvedNormal = normal.normalized;
            if (Vector3.Dot(resolvedNormal, up) < minimumGroundDot) return;

            pointSum += point;
            pointCount++;
        }

        internal static bool AccumulateGroundContact(
            Vector3 point,
            Vector3 normal,
            float impulseMagnitude,
            Vector3 up,
            float minimumGroundDot,
            ref Vector3 weightedPressure,
            ref float pressureWeight,
            ref int pressureContactCount)
        {
            Vector3 resolvedNormal = normal.sqrMagnitude > Mathf.Epsilon
                ? normal.normalized
                : up;
            if (Vector3.Dot(resolvedNormal, up) < minimumGroundDot)
                return false;

            RagdollCenterOfPressureMath.Accumulate(
                point,
                impulseMagnitude,
                ref weightedPressure,
                ref pressureWeight,
                ref pressureContactCount);
            return true;
        }

        internal void Update(
            float deltaTime,
            LayerMask groundLayers,
            float probeStartOffset,
            float probeDistance,
            float maximumGroundAngle)
        {
            Vector3 centerOfMass;
            Vector3 centerOfMassVelocity;
            float totalMass;
            CalculateCenterOfMass(
                out centerOfMass,
                out centerOfMassVelocity,
                out totalMass);

            Vector3 gravity = Physics.gravity;
            bool effectiveUpAvailable = IsFinite(gravity)
                && gravity.sqrMagnitude > Mathf.Epsilon;
            Up = effectiveUpAvailable
                ? -gravity.normalized
                : Vector3.up;

            float startOffset = Mathf.Max(0f, probeStartOffset);
            float distance = startOffset + Mathf.Max(0.001f, probeDistance);
            Vector3 origin = centerOfMass + Up * startOffset;

            int hitCount;
            bool complete = RagdollRaycastBuffer.TryRaycastComplete(
                origin,
                -Up,
                distance,
                groundLayers.value,
                ref hits,
                out hitCount);

            float minimumGroundDot = Mathf.Cos(
                Mathf.Clamp(maximumGroundAngle, 0f, 89.9f) * Mathf.Deg2Rad);

            bool grounded = complete;
            bool foundGround = false;
            float nearestDistance = float.PositiveInfinity;
            Vector3 point = Vector3.zero;
            Vector3 normal = Up;
            int supportColliderId = 0;
            int supportRigidbodyId = 0;
            bool hasSupportPlatform = false;
            Vector3 supportVelocity = Vector3.zero;

            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = hits[index];
                if (!hit.collider || IsRagdollCollider(hit.collider)) continue;
                if (Vector3.Dot(hit.normal, Up) < minimumGroundDot) continue;
                if (hit.distance >= nearestDistance) continue;

                foundGround = true;
                nearestDistance = hit.distance;
                point = hit.point;
                normal = hit.normal;
                supportColliderId = RagdollUnityObjectId.Get(hit.collider);
                supportRigidbodyId = hit.rigidbody ? RagdollUnityObjectId.Get(hit.rigidbody) : 0;
                hasSupportPlatform = supportColliderId != 0;
                supportVelocity = hit.rigidbody
                    ? hit.rigidbody.GetPointVelocity(hit.point)
                    : Vector3.zero;
            }

            grounded = grounded && foundGround;

            Vector3 centerOfPressure;
            bool hasCenterOfPressure = RagdollCenterOfPressureMath.Resolve(
                weightedPressure,
                pressureWeight,
                pressureContactCount,
                out centerOfPressure);

            tracker.Update(
                grounded,
                point,
                normal,
                centerOfMass,
                centerOfMassVelocity,
                totalMass,
                deltaTime,
                hasCenterOfPressure,
                centerOfPressure,
                Up,
                effectiveUpAvailable,
                grounded ? supportColliderId : 0,
                grounded ? supportRigidbodyId : 0,
                grounded && hasSupportPlatform,
                grounded ? supportVelocity : Vector3.zero,
                leftFootSupportCount > 0,
                leftFootSupportCount > 0
                    ? leftFootSupportPoint / leftFootSupportCount
                    : Vector3.zero,
                rightFootSupportCount > 0,
                rightFootSupportCount > 0
                    ? rightFootSupportPoint / rightFootSupportCount
                    : Vector3.zero);
            ClearPressureContacts();
        }

        void ClearPressureContacts()
        {
            weightedPressure = Vector3.zero;
            pressureWeight = 0f;
            pressureContactCount = 0;
            leftFootSupportPoint = Vector3.zero;
            rightFootSupportPoint = Vector3.zero;
            leftFootSupportCount = 0;
            rightFootSupportCount = 0;
        }

        void CalculateCenterOfMass(
            out Vector3 centerOfMass,
            out Vector3 centerOfMassVelocity,
            out float totalMass)
        {
            totalMass = 0f;
            Vector3 weightedPosition = Vector3.zero;
            Vector3 weightedVelocity = Vector3.zero;

            for (int index = 0; index < context.Pairs.Count; index++)
            {
                Rigidbody rigidbody = context.Pairs[index].RagdollBone.Rigidbody;
                if (!rigidbody) continue;

                RagdollCenterOfMassMath.Accumulate(
                    rigidbody.mass,
                    rigidbody.worldCenterOfMass,
                    rigidbody.linearVelocity,
                    ref totalMass,
                    ref weightedPosition,
                    ref weightedVelocity);
            }

            RagdollCenterOfMassMath.Resolve(
                totalMass,
                weightedPosition,
                weightedVelocity,
                out centerOfMass,
                out centerOfMassVelocity);

            if (totalMass <= Mathf.Epsilon)
            {
                Rigidbody root = context.Bindings.Root.Rigidbody;
                centerOfMass = root.worldCenterOfMass;
                centerOfMassVelocity = root.linearVelocity;
                totalMass = Mathf.Max(0f, root.mass);
            }
        }

        bool IsRagdollCollider(Collider collider)
        {
            RagdollBone ignored;
            return context.Bindings.TryGetBone(collider, out ignored)
                || context.Bindings.TryGetBoneFromAttachedRigidbody(collider, out ignored);
        }

        static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }

    /// <summary>
    /// Reuses its caller-owned buffer and expands only on saturation. Unity does not
    /// guarantee which hits are returned when RaycastNonAlloc fills the buffer, so a
    /// saturated result must never be treated as complete.
    /// </summary>
    internal static class RagdollRaycastBuffer
    {
        const int MaximumCapacity = 4096;

        internal static bool TryRaycastComplete(
            Vector3 origin,
            Vector3 direction,
            float distance,
            int layerMask,
            ref RaycastHit[] buffer,
            out int hitCount)
        {
            if (buffer == null || buffer.Length == 0)
            {
                buffer = new RaycastHit[32];
            }

            while (true)
            {
                hitCount = Physics.RaycastNonAlloc(
                    origin,
                    direction,
                    buffer,
                    distance,
                    layerMask,
                    QueryTriggerInteraction.Ignore);
                if (hitCount < buffer.Length) return true;
                if (buffer.Length >= MaximumCapacity)
                {
                    hitCount = 0;
                    return false;
                }

                int capacity = Mathf.Min(buffer.Length * 2, MaximumCapacity);
                Array.Resize(ref buffer, capacity);
            }
        }
    }
}
