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
        public Vector3 EffectiveUp { get; private set; }
        public bool EffectiveUpAvailable { get; private set; }
        public Vector3 CenterOfMass { get; private set; }
        public Vector3 CenterOfMassVelocity { get; private set; }
        public Vector3 RelativeCenterOfMassVelocity { get; private set; }
        public float TotalMass { get; private set; }
        public int SupportColliderId { get; private set; }
        public int SupportRigidbodyId { get; private set; }
        public bool HasSupportPlatform { get; private set; }
        public Vector3 SupportVelocity { get; private set; }
        public bool HasRelativeMotion { get; private set; }
        public bool SupportContinuityReset { get; private set; }
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
            Vector3 up = default(Vector3),
            bool effectiveUpAvailable = true,
            int supportColliderId = 0,
            int supportRigidbodyId = 0,
            bool hasSupportPlatform = false,
            Vector3 supportVelocity = default(Vector3),
            bool hasRelativeMotion = false,
            bool supportContinuityReset = false)
        {
            IsGrounded = isGrounded;
            StableTime = IsFinite(stableTime) ? Mathf.Max(0f, stableTime) : 0f;
            GroundPoint = IsFinite(groundPoint) ? groundPoint : Vector3.zero;
            GroundNormal = IsFinite(groundNormal) && groundNormal.sqrMagnitude > Mathf.Epsilon
                ? groundNormal.normalized
                : Vector3.up;
            EffectiveUp = IsFinite(up) && up.sqrMagnitude > Mathf.Epsilon
                ? up.normalized
                : Vector3.up;
            EffectiveUpAvailable = effectiveUpAvailable
                && IsFinite(up)
                && up.sqrMagnitude > Mathf.Epsilon;
            CenterOfMass = IsFinite(centerOfMass) ? centerOfMass : Vector3.zero;
            CenterOfMassVelocity = IsFinite(centerOfMassVelocity)
                ? centerOfMassVelocity
                : Vector3.zero;
            SupportVelocity = IsFinite(supportVelocity) ? supportVelocity : Vector3.zero;
            HasRelativeMotion = hasRelativeMotion && IsFinite(centerOfMassVelocity - SupportVelocity);
            RelativeCenterOfMassVelocity = HasRelativeMotion
                ? centerOfMassVelocity - SupportVelocity
                : Vector3.zero;
            TotalMass = IsFinite(totalMass) ? Mathf.Max(0f, totalMass) : 0f;
            SupportColliderId = supportColliderId;
            SupportRigidbodyId = supportRigidbodyId;
            HasSupportPlatform = hasSupportPlatform && supportColliderId != 0;
            SupportContinuityReset = supportContinuityReset;
            HasCenterOfPressure = hasCenterOfPressure && IsFinite(centerOfPressure);
            CenterOfPressure = HasCenterOfPressure
                ? centerOfPressure
                : Vector3.zero;
            CenterOfMassVector = HasCenterOfPressure
                ? CenterOfMass - CenterOfPressure
                : Vector3.zero;
            CenterOfMassDistance = CenterOfMassVector.magnitude;
            CenterOfMassDirection = CenterOfMassDistance > Mathf.Epsilon
                ? CenterOfMassVector / CenterOfMassDistance
                : Vector3.zero;
            Vector3 resolvedUp = EffectiveUp;
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
                    0f,
                    false,
                    Vector3.zero,
                    Vector3.up,
                    false);
            }
        }

        static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
