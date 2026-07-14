using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    public partial class RagdollAnimator
    {
        public class AnimatedPair
        {
            public BoneName Name => bonePair.RagdollBone.Name;
            public RagdollBone RagdollBone => bonePair.RagdollBone;
            public Transform TargetBone => bonePair.TargetBone;
            public RagdollBoneHandle Handle { get; }
            public RagdollMappingWeights MappingWeights { get; internal set; }

            /// <summary>
            /// The latest unmodified animation sample. Target pose modifiers operate on
            /// currentPose and never overwrite this sampled value.
            /// </summary>
            public AnimatedPose SampledPose => poseSampler.Pose;

            public AnimatedPose currentPose;

            internal Vector3 poseLinearVelocity;
            internal Vector3 poseAngularVelocity;

            readonly RagdollBoneTargetBonePair bonePair;
            AnimatedPoseSampler poseSampler;


            internal void SampleAnimatedPose(AnimatedPose pose, float sampleTime)
            {
                poseSampler.Push(pose, sampleTime);
                RestoreAnimatedPose();

                poseLinearVelocity = poseSampler.LinearVelocity;
                poseAngularVelocity = poseSampler.AngularVelocity;
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

                AnimatedPose sampledPose = poseSampler.Pose;
                TargetBone.SetPositionAndRotation(
                    sampledPose.worldPosition,
                    sampledPose.worldRotation);
            }


            internal AnimatedPair(
                RagdollBoneTargetBonePair bonePair,
                RagdollBoneHandle handle,
                RagdollMappingWeights mappingWeights)
            {
                this.bonePair = bonePair;
                Handle = handle;
                MappingWeights = mappingWeights;
            }
        }
    }
}