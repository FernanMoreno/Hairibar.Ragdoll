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

        public bool IsInitialized => initialized;
        public RagdollAnimator.AnimatedPose Pose => pose;
        public Vector3 LinearVelocity => linearVelocity;
        public Vector3 AngularVelocity => angularVelocity;

        public void Reset(RagdollAnimator.AnimatedPose newPose, float newSampleTime)
        {
            initialized = true;
            resetVelocityOnNextPush = false;
            sampleTime = newSampleTime;
            pose = newPose;
            linearVelocity = Vector3.zero;
            angularVelocity = Vector3.zero;
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
            resetVelocityOnNextPush = true;
        }

        public void Push(RagdollAnimator.AnimatedPose newPose, float newSampleTime)
        {
            if (!initialized || resetVelocityOnNextPush)
            {
                Reset(newPose, newSampleTime);
                return;
            }

            float dt = newSampleTime - sampleTime;
            if (dt < 0f)
            {
                Reset(newPose, newSampleTime);
                return;
            }

            if (dt > MinimumSampleDeltaTime)
            {
                linearVelocity = CalculateLinearVelocity(pose, newPose, dt);
                angularVelocity = CalculateAngularVelocity(pose, newPose, dt);
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
