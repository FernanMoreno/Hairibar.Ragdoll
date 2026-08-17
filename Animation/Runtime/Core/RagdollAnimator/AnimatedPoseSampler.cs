using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Stores the two most recent rendered animation samples and derives target velocities
    /// from their real sampling interval. The calculated velocity remains stable until a
    /// newer animation sample arrives, even when physics advances multiple times per frame.
    /// </summary>
    internal struct AnimatedPoseSampler
    {
        const float MinimumSampleDeltaTime = 0.000001f;

        bool initialized;
        bool resetVelocityOnNextPush;
        float sampleTime;
        RagdollAnimator.AnimatedPose pose;
        Vector3 linearVelocity;
        Vector3 angularVelocity;
        Vector3 linearAcceleration;
        Vector3 angularAcceleration;
        Vector3 linearJerk;
        Vector3 angularJerk;
        float sampleDeltaTime;
        bool kinematicsAvailable;
        bool accelerationAvailable;
        bool jerkAvailable;
        bool kinematicsReset;

        public bool IsInitialized => initialized;
        public RagdollAnimator.AnimatedPose Pose => pose;
        public Vector3 LinearVelocity => linearVelocity;
        public Vector3 AngularVelocity => angularVelocity;
        public Vector3 LinearAcceleration => linearAcceleration;
        public Vector3 AngularAcceleration => angularAcceleration;
        public Vector3 LinearJerk => linearJerk;
        public Vector3 AngularJerk => angularJerk;
        public float SampleDeltaTime => sampleDeltaTime;
        public bool KinematicsAvailable => kinematicsAvailable;
        public bool AccelerationAvailable => accelerationAvailable;
        public bool JerkAvailable => jerkAvailable;
        public bool KinematicsReset => kinematicsReset;

        public void Reset(RagdollAnimator.AnimatedPose newPose, float newSampleTime)
        {
            initialized = true;
            resetVelocityOnNextPush = false;
            sampleTime = newSampleTime;
            pose = newPose;
            linearVelocity = Vector3.zero;
            angularVelocity = Vector3.zero;
            linearAcceleration = Vector3.zero;
            angularAcceleration = Vector3.zero;
            linearJerk = Vector3.zero;
            angularJerk = Vector3.zero;
            sampleDeltaTime = 0f;
            kinematicsAvailable = false;
            accelerationAvailable = false;
            jerkAvailable = false;
            kinematicsReset = true;
        }

        internal void ApplyTeleport(
            Quaternion deltaRotation,
            Vector3 deltaPosition,
            Vector3 pivot)
        {
            if (!initialized) return;

            pose = RagdollTeleportMath.TransformPose(
                pose,
                deltaRotation,
                deltaPosition,
                pivot);
            linearVelocity = Vector3.zero;
            angularVelocity = Vector3.zero;
            linearAcceleration = Vector3.zero;
            angularAcceleration = Vector3.zero;
            linearJerk = Vector3.zero;
            angularJerk = Vector3.zero;
            sampleDeltaTime = 0f;
            kinematicsAvailable = false;
            accelerationAvailable = false;
            jerkAvailable = false;
            kinematicsReset = true;
            resetVelocityOnNextPush = true;
        }

        public void Push(RagdollAnimator.AnimatedPose newPose, float newSampleTime)
        {
            if (!initialized || resetVelocityOnNextPush)
            {
                Reset(newPose, newSampleTime);
                return;
            }

            if (!IsFinite(newSampleTime)
                || !IsFinite(newPose.worldPosition)
                || !IsFinite(newPose.worldRotation)
                || !IsFinite(newPose.localRotation))
            {
                Reset(newPose, newSampleTime);
                return;
            }

            float dt = newSampleTime - sampleTime;
            if (!IsFinite(dt) || dt < 0f)
            {
                Reset(newPose, newSampleTime);
                return;
            }

            if (dt > MinimumSampleDeltaTime)
            {
                Vector3 nextLinearVelocity = CalculateLinearVelocity(pose, newPose, dt);
                Vector3 nextAngularVelocity = CalculateAngularVelocity(pose, newPose, dt);
                bool hadVelocity = kinematicsAvailable;
                bool hadAcceleration = accelerationAvailable;
                if (hadVelocity)
                {
                    Vector3 previousLinearAcceleration = linearAcceleration;
                    Vector3 previousAngularAcceleration = angularAcceleration;
                    linearAcceleration = (nextLinearVelocity - linearVelocity) / dt;
                    angularAcceleration = (nextAngularVelocity - angularVelocity) / dt;
                    accelerationAvailable = true;
                    if (hadAcceleration)
                    {
                        linearJerk = (linearAcceleration - previousLinearAcceleration) / dt;
                        angularJerk = (angularAcceleration - previousAngularAcceleration) / dt;
                        jerkAvailable = true;
                    }
                    else
                    {
                        linearJerk = Vector3.zero;
                        angularJerk = Vector3.zero;
                        jerkAvailable = false;
                    }
                }
                else
                {
                    linearAcceleration = Vector3.zero;
                    angularAcceleration = Vector3.zero;
                    linearJerk = Vector3.zero;
                    angularJerk = Vector3.zero;
                    accelerationAvailable = false;
                    jerkAvailable = false;
                }
                linearVelocity = nextLinearVelocity;
                angularVelocity = nextAngularVelocity;
                sampleDeltaTime = dt;
                kinematicsAvailable = true;
                kinematicsReset = false;
            }
            else
            {
                // Preserve the cached velocity used by collision-resistance code,
                // but make the repeated/non-positive sample unavailable to telemetry
                // so it cannot masquerade as a valid derivative interval.
                sampleDeltaTime = 0f;
                kinematicsAvailable = false;
                accelerationAvailable = false;
                jerkAvailable = false;
                linearAcceleration = Vector3.zero;
                angularAcceleration = Vector3.zero;
                linearJerk = Vector3.zero;
                angularJerk = Vector3.zero;
                kinematicsReset = true;
            }

            pose = newPose;
            sampleTime = newSampleTime;
        }

        static Vector3 CalculateLinearVelocity(
            RagdollAnimator.AnimatedPose previousPose,
            RagdollAnimator.AnimatedPose newPose,
            float dt)
        {
            return (newPose.worldPosition - previousPose.worldPosition) / dt;
        }

        static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y)
                && IsFinite(value.z) && IsFinite(value.w);
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        static Vector3 CalculateAngularVelocity(
            RagdollAnimator.AnimatedPose previousPose,
            RagdollAnimator.AnimatedPose newPose,
            float dt)
        {
            Quaternion deltaRotation = newPose.localRotation * Quaternion.Inverse(previousPose.localRotation);
            deltaRotation.ToAngleAxis(out float deltaAngle, out Vector3 axis);

            if (deltaAngle > 180f)
            {
                deltaAngle -= 360f;
            }

            if (Mathf.Abs(deltaAngle) <= Mathf.Epsilon || axis.sqrMagnitude <= Mathf.Epsilon)
            {
                return Vector3.zero;
            }

            return Mathf.Deg2Rad * deltaAngle / dt * axis.normalized;
        }
    }
}
