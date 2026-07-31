using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Immutable result of the latest ground and center-of-mass probe.
    /// </summary>
    [Serializable]
    public struct RagdollGroundingSnapshot
    {
        public bool IsGrounded { get; private set; }
        public float StableTime { get; private set; }
        public Vector3 GroundPoint { get; private set; }
        public Vector3 GroundNormal { get; private set; }
        public Vector3 CenterOfMass { get; private set; }
        public Vector3 CenterOfMassVelocity { get; private set; }
        public float TotalMass { get; private set; }
        public bool HasCenterOfPressure { get; private set; }
        public Vector3 CenterOfPressure { get; private set; }
        public Vector3 CenterOfMassVector { get; private set; }
        public Vector3 CenterOfMassDirection { get; private set; }
        public float CenterOfMassDistance { get; private set; }
        public float CenterOfMassAngle { get; private set; }

        internal RagdollGroundingSnapshot(
            bool isGrounded,
            float stableTime,
            Vector3 groundPoint,
            Vector3 groundNormal,
            Vector3 centerOfMass,
            Vector3 centerOfMassVelocity,
            float totalMass,
            bool hasCenterOfPressure = false,
            Vector3 centerOfPressure = default(Vector3),
            Vector3 up = default(Vector3))
        {
            IsGrounded = isGrounded;
            StableTime = Mathf.Max(0f, stableTime);
            GroundPoint = groundPoint;
            GroundNormal = groundNormal.sqrMagnitude > Mathf.Epsilon
                ? groundNormal.normalized
                : Vector3.up;
            CenterOfMass = centerOfMass;
            CenterOfMassVelocity = centerOfMassVelocity;
            TotalMass = Mathf.Max(0f, totalMass);
            HasCenterOfPressure = hasCenterOfPressure;
            CenterOfPressure = hasCenterOfPressure
                ? centerOfPressure
                : Vector3.zero;
            CenterOfMassVector = hasCenterOfPressure
                ? centerOfMass - centerOfPressure
                : Vector3.zero;
            CenterOfMassDistance = CenterOfMassVector.magnitude;
            CenterOfMassDirection = CenterOfMassDistance > Mathf.Epsilon
                ? CenterOfMassVector / CenterOfMassDistance
                : Vector3.zero;
            Vector3 resolvedUp = up.sqrMagnitude > Mathf.Epsilon
                ? up.normalized
                : Vector3.up;
            CenterOfMassAngle = CenterOfMassDistance > Mathf.Epsilon
                ? Vector3.Angle(resolvedUp, CenterOfMassDirection)
                : 0f;
        }

        public static RagdollGroundingSnapshot Empty
        {
            get
            {
                return new RagdollGroundingSnapshot(
                    false,
                    0f,
                    Vector3.zero,
                    Vector3.up,
                    Vector3.zero,
                    Vector3.zero,
                    0f);
            }
        }
    }
}
