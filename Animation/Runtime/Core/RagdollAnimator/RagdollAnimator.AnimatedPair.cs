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

            readonly RagdollBoneTargetBonePair bonePair;
            readonly RagdollTargetDefaultPose defaultTargetPose;
            AnimatedPoseSampler poseSampler;


            internal void SampleAnimatedPose(
                AnimatedPose targetPose,
                AnimatedPose ragdollPose,
                float sampleTime)
            {
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

            internal TeleportState CaptureTeleportState()
            {
                return new TeleportState
                {
                    sampledTargetPose = SampledTargetPose,
                    currentPose = currentPose,
                    poseSampler = poseSampler,
                    poseLinearVelocity = poseLinearVelocity,
                    poseAngularVelocity = poseAngularVelocity
                };
            }

            internal void RestoreTeleportState(TeleportState state)
            {
                SampledTargetPose = state.sampledTargetPose;
                currentPose = state.currentPose;
                poseSampler = state.poseSampler;
                poseLinearVelocity = state.poseLinearVelocity;
                poseAngularVelocity = state.poseAngularVelocity;
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
            }

            internal struct TeleportState
            {
                internal AnimatedPose sampledTargetPose;
                internal AnimatedPose currentPose;
                internal AnimatedPoseSampler poseSampler;
                internal Vector3 poseLinearVelocity;
                internal Vector3 poseAngularVelocity;
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
