using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    public partial class RagdollAnimator
    {
        public class AnimatedPair
        {
            public BoneName Name => bonePair.RagdollBone.Name;
            public RagdollBone RagdollBone => bonePair.RagdollBone;
            public RagdollTargetBinding TargetBinding => bonePair.TargetBinding;
            public Transform TargetBone => bonePair.TargetBone;
            public RagdollBoneHandle Handle { get; }
            public RagdollMappingWeights MappingWeights { get; internal set; }

            /// <summary>
            /// Latest unmodified animation sample converted into the ragdoll bone's pose
            /// space. Target pose modifiers operate on currentPose and never overwrite it.
            /// </summary>
            public AnimatedPose SampledPose => poseSampler.Pose;

            /// <summary>
            /// Latest unmodified world pose read directly from the visual Target Transform.
            /// It is restored before mapping so the simulation never accumulates drift.
            /// </summary>
            public AnimatedPose SampledTargetPose { get; private set; }

            public AnimatedPose currentPose;

            internal Vector3 poseLinearVelocity;
            internal Vector3 poseAngularVelocity;
            Vector3 targetLinearVelocity;
            Vector3 unclockedTargetDisplacement;
            float targetSampleTime;
            float lastTargetMovementSampleTime;
            bool targetSampleInitialized;
            bool hasTargetMovementSample;

            readonly RagdollBoneTargetBonePair bonePair;
            RagdollTargetDefaultPose defaultTargetPose;
            AnimatedPoseSampler poseSampler;


            internal void SampleAnimatedPose(
                AnimatedPose targetPose,
                AnimatedPose ragdollPose,
                float sampleTime)
            {
                if (targetSampleInitialized)
                {
                    float targetDeltaTime = sampleTime - targetSampleTime;
                    Vector3 targetDisplacement = targetPose.worldPosition
                        - SampledTargetPose.worldPosition;
                    if (targetDeltaTime > 0.000001f)
                    {
                        if (targetDisplacement.sqrMagnitude > Mathf.Epsilon)
                        {
                            targetLinearVelocity = targetDisplacement
                                / targetDeltaTime;
                            lastTargetMovementSampleTime = sampleTime;
                            hasTargetMovementSample = true;
                        }
                        else if (hasTargetMovementSample
                            && sampleTime - lastTargetMovementSampleTime
                                > Mathf.Max(Time.fixedDeltaTime, 0.000001f))
                        {
                            targetLinearVelocity = Vector3.zero;
                            hasTargetMovementSample = false;
                        }
                        unclockedTargetDisplacement = Vector3.zero;
                    }
                    else if (targetDeltaTime < 0f)
                    {
                        targetLinearVelocity = Vector3.zero;
                        unclockedTargetDisplacement = Vector3.zero;
                        hasTargetMovementSample = false;
                    }
                    else
                    {
                        // Several physics boundaries may occur during one rendered
                        // animation time stamp. Preserve that real Target displacement
                        // so collision resistance can evaluate it using fixedDeltaTime.
                        if (targetDisplacement.sqrMagnitude > Mathf.Epsilon)
                        {
                            unclockedTargetDisplacement = targetDisplacement;
                        }
                    }
                }
                else
                {
                    targetLinearVelocity = Vector3.zero;
                    unclockedTargetDisplacement = Vector3.zero;
                    targetSampleInitialized = true;
                }
                targetSampleTime = sampleTime;
                SampledTargetPose = targetPose;
                poseSampler.Push(ragdollPose, sampleTime);
                RestoreAnimatedPose();

                poseLinearVelocity = poseSampler.LinearVelocity;
                poseAngularVelocity = poseSampler.AngularVelocity;
            }

            internal AnimatedPose ConvertTargetPoseToRagdoll(AnimatedPose targetPose)
            {
                return TargetBinding.ConvertTargetPoseToRagdoll(
                    targetPose,
                    RagdollBone.Transform);
            }

            internal void GetMappedTargetWorldPose(
                out Vector3 targetWorldPosition,
                out Quaternion targetWorldRotation)
            {
                TargetBinding.GetTargetWorldPose(
                    RagdollBone.Transform,
                    out targetWorldPosition,
                    out targetWorldRotation);
            }

            internal void RestoreAnimatedPose()
            {
                if (poseSampler.IsInitialized)
                {
                    currentPose = poseSampler.Pose;
                }
            }

            /// <summary>
            /// Returns the velocity represented by the latest animation sample, also
            /// accounting for Target movement that happened after that sample and
            /// before the current physics collision callback.
            /// </summary>
            internal float GetTargetSpeedAtPhysicsBoundary(float fixedDeltaTime)
            {
                float sampledSpeed = Mathf.Max(
                    poseLinearVelocity.magnitude,
                    targetLinearVelocity.magnitude);
                if (!poseSampler.IsInitialized
                    || float.IsNaN(fixedDeltaTime)
                    || float.IsInfinity(fixedDeltaTime)
                    || fixedDeltaTime <= Mathf.Epsilon)
                {
                    return sampledSpeed;
                }

                float pendingDistance = Vector3.Distance(
                    TargetBone.position,
                    SampledTargetPose.worldPosition);
                float pendingSpeed = Mathf.Max(
                    pendingDistance,
                    unclockedTargetDisplacement.magnitude)
                    / fixedDeltaTime;
                return float.IsNaN(pendingSpeed) || float.IsInfinity(pendingSpeed)
                    ? sampledSpeed
                    : Mathf.Max(sampledSpeed, pendingSpeed);
            }

            internal void RestoreSampledPoseToTarget()
            {
                if (!poseSampler.IsInitialized) return;

                TargetBone.SetPositionAndRotation(
                    SampledTargetPose.worldPosition,
                    SampledTargetPose.worldRotation);
            }

            internal void FixTargetTransform()
            {
                defaultTargetPose.Apply(TargetBone);
            }

            internal void CopyRuntimeStateFrom(AnimatedPair source)
            {
                if (source == null) throw new System.ArgumentNullException(nameof(source));
                SampledTargetPose = source.SampledTargetPose;
                currentPose = source.currentPose;
                poseSampler = source.poseSampler;
                poseLinearVelocity = source.poseLinearVelocity;
                poseAngularVelocity = source.poseAngularVelocity;
                targetLinearVelocity = source.targetLinearVelocity;
                unclockedTargetDisplacement = source.unclockedTargetDisplacement;
                targetSampleTime = source.targetSampleTime;
                lastTargetMovementSampleTime = source.lastTargetMovementSampleTime;
                targetSampleInitialized = source.targetSampleInitialized;
                hasTargetMovementSample = source.hasTargetMovementSample;
                MappingWeights = source.MappingWeights;
                defaultTargetPose = source.defaultTargetPose;
            }

            internal TeleportState CaptureTeleportState()
            {
                return new TeleportState
                {
                    sampledTargetPose = SampledTargetPose,
                    currentPose = currentPose,
                    poseSampler = poseSampler,
                    poseLinearVelocity = poseLinearVelocity,
                    poseAngularVelocity = poseAngularVelocity,
                    targetLinearVelocity = targetLinearVelocity,
                    unclockedTargetDisplacement = unclockedTargetDisplacement,
                    targetSampleTime = targetSampleTime,
                    lastTargetMovementSampleTime = lastTargetMovementSampleTime,
                    targetSampleInitialized = targetSampleInitialized,
                    hasTargetMovementSample = hasTargetMovementSample
                };
            }

            internal void RestoreTeleportState(TeleportState state)
            {
                SampledTargetPose = state.sampledTargetPose;
                currentPose = state.currentPose;
                poseSampler = state.poseSampler;
                poseLinearVelocity = state.poseLinearVelocity;
                poseAngularVelocity = state.poseAngularVelocity;
                targetLinearVelocity = state.targetLinearVelocity;
                unclockedTargetDisplacement = state.unclockedTargetDisplacement;
                targetSampleTime = state.targetSampleTime;
                lastTargetMovementSampleTime = state.lastTargetMovementSampleTime;
                targetSampleInitialized = state.targetSampleInitialized;
                hasTargetMovementSample = state.hasTargetMovementSample;
            }

            internal void ApplyTeleport(
                Quaternion deltaRotation,
                Vector3 deltaPosition,
                Vector3 pivot)
            {
                if (poseSampler.IsInitialized)
                {
                    SampledTargetPose = RagdollTeleportMath.TransformPose(
                        SampledTargetPose,
                        deltaRotation,
                        deltaPosition,
                        pivot);
                    currentPose = RagdollTeleportMath.TransformPose(
                        currentPose,
                        deltaRotation,
                        deltaPosition,
                        pivot);
                    poseSampler.ApplyTeleport(
                        deltaRotation,
                        deltaPosition,
                        pivot);
                }

                poseLinearVelocity = Vector3.zero;
                poseAngularVelocity = Vector3.zero;
                targetLinearVelocity = Vector3.zero;
                unclockedTargetDisplacement = Vector3.zero;
                hasTargetMovementSample = false;
            }

            internal struct TeleportState
            {
                internal AnimatedPose sampledTargetPose;
                internal AnimatedPose currentPose;
                internal AnimatedPoseSampler poseSampler;
                internal Vector3 poseLinearVelocity;
                internal Vector3 poseAngularVelocity;
                internal Vector3 targetLinearVelocity;
                internal Vector3 unclockedTargetDisplacement;
                internal float targetSampleTime;
                internal float lastTargetMovementSampleTime;
                internal bool targetSampleInitialized;
                internal bool hasTargetMovementSample;
            }


            internal AnimatedPair(
                RagdollBoneTargetBonePair bonePair,
                RagdollBoneHandle handle,
                RagdollMappingWeights mappingWeights)
            {
                this.bonePair = bonePair;
                Handle = handle;
                MappingWeights = mappingWeights;
                defaultTargetPose = RagdollTargetDefaultPose.Capture(TargetBone);
            }
        }
    }
}
