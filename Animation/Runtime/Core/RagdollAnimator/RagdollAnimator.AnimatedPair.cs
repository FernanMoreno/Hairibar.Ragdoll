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


            internal AnimatedPair(RagdollBoneTargetBonePair bonePair, RagdollBoneHandle handle)
            {
                this.bonePair = bonePair;
                Handle = handle;
            }
        }
    }
}