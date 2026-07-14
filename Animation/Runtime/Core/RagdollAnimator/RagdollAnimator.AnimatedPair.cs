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
