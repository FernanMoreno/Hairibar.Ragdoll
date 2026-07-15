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
        readonly RaycastHit[] hits = new RaycastHit[RaycastCapacity];

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
            Up = gravity.sqrMagnitude > Mathf.Epsilon
                ? -gravity.normalized
                : Vector3.up;

            float startOffset = Mathf.Max(0f, probeStartOffset);
            float distance = startOffset + Mathf.Max(0.001f, probeDistance);
            Vector3 origin = centerOfMass + Up * startOffset;

            int hitCount = Physics.RaycastNonAlloc(
                origin,
                -Up,
                hits,
                distance,
                groundLayers.value,
                QueryTriggerInteraction.Ignore);

            float minimumGroundDot = Mathf.Cos(
                Mathf.Clamp(maximumGroundAngle, 0f, 89.9f) * Mathf.Deg2Rad);

            // RaycastNonAlloc does not guarantee that a full buffer contains the nearest
            // hits. Fail closed rather than authorizing GetUp from incomplete ground data.
            bool grounded = hitCount < hits.Length;
            bool foundGround = false;
            float nearestDistance = float.PositiveInfinity;
            Vector3 point = Vector3.zero;
            Vector3 normal = Up;

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
            }

            grounded = grounded && foundGround;

            tracker.Update(
                grounded,
                point,
                normal,
                centerOfMass,
                centerOfMassVelocity,
                totalMass,
                deltaTime);
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
                    rigidbody.velocity,
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
                centerOfMassVelocity = root.velocity;
                totalMass = Mathf.Max(0f, root.mass);
            }
        }

        bool IsRagdollCollider(Collider collider)
        {
            RagdollBone ignored;
            return context.Bindings.TryGetBone(collider, out ignored)
                || context.Bindings.TryGetBoneFromAttachedRigidbody(collider, out ignored);
        }
    }
}
