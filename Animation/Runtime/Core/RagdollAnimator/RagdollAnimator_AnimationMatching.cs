using Hairibar.EngineExtensions;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    public partial class RagdollAnimator
    {
        void TransitionTo(RagdollAnimationProfile newProfile)
        {
            if (newProfile != currentProfile)
            {
                previousProfile = currentProfile;
                currentProfile = newProfile;

                profileTransitioner.StartTransition(_profileTransitionLength);
            }
        }

        #region Simulation Update
        void DoAnimationMatching()
        {
            float dt = Time.fixedDeltaTime;

            profileTransitioner.Update(dt);

            foreach (AnimatedPair pair in animatedPairs)
            {
                switch (pair.RagdollBone.PowerSetting)
                {
                    case PowerSetting.Kinematic:
                        DoKinematicAnimationMatching(pair);
                        break;

                    case PowerSetting.Powered:
                        DoPoweredAnimationMatching(pair, dt);
                        break;
                    case PowerSetting.Unpowered:
                        SetUnpoweredJointDrive(pair);
                        break;
                }
            }
        }

        void DoKinematicAnimationMatching(AnimatedPair pair)
        {
            Rigidbody rigidbody = pair.RagdollBone.Rigidbody;
            rigidbody.MovePosition(pair.currentPose.worldPosition);
            rigidbody.MoveRotation(pair.currentPose.worldRotation);
        }

        void DoPoweredAnimationMatching(AnimatedPair pair, float dt)
        {
            BoneProfile boneProfile = GetBoneProfile(pair.Name);

            ModifyBoneProfile(ref boneProfile, pair, dt);
            ApplyMasterParameters(ref boneProfile);
            ApplyLifecycleParameters(ref boneProfile);

            DoPoweredPositionMatching(pair, boneProfile, dt);
            DoPoweredRotationMatching(pair, boneProfile, dt);
        }


        internal BoneProfile GetBoneProfile(BoneName bone)
        {
            bool isRoot = Bindings.Definition.IsRoot(bone);

            BoneProfile previous = previousProfile.GetBoneProfile(bone, isRoot);
            BoneProfile current = currentProfile.GetBoneProfile(bone, isRoot);

            return BoneProfile.Blend(previous, current, profileTransitioner.Value);
        }

        void ModifyBoneProfile(ref BoneProfile boneProfile, AnimatedPair pair, float dt)
        {
            foreach (IBoneProfileModifier modifier in boneProfileModifiers)
            {
                modifier.Modify(ref boneProfile, pair, dt);
            }
        }

        void ApplyMasterParameters(ref BoneProfile boneProfile)
        {
            boneProfile.positionAlpha *= _masterAlpha;
            boneProfile.positionDampingRatio *= _masterDampingRatio;

            boneProfile.rotationAlpha *= _masterAlpha;
            boneProfile.rotationDampingRatio *= _masterDampingRatio;
        }


        void DoPoweredPositionMatching(AnimatedPair pair, BoneProfile boneProfile, float dt)
        {
            float alpha = boneProfile.positionAlpha;
            float dampingRatio = boneProfile.positionDampingRatio;

            Rigidbody rigidbody = pair.RagdollBone.Rigidbody;
            AnimatedPose targetPose = pair.currentPose;

            Vector3 positionOffset = targetPose.worldPosition - rigidbody.position;
            Vector3 acceleration = AnimationMatching.GetAccelerationFromPositionSpring(
                rigidbody.position,
                targetPose.worldPosition,
                rigidbody.velocity,
                pair.poseLinearVelocity,
                alpha,
                dampingRatio,
                rigidbody.mass,
                dt);

            LimitAcceleration(ref acceleration, boneProfile.maxLinearAcceleration);
            acceleration = RagdollPinMath.ResolvePositionAcceleration(
                acceleration,
                positionOffset,
                boneProfile.PositionPinWeight,
                pinSettings.PinPow,
                pinSettings.PinDistanceFalloff);

            rigidbody.AddForce(acceleration, ForceMode.Acceleration);
        }

        static Vector3 LimitAcceleration(ref Vector3 acceleration, float maxAcceleration)
        {
            acceleration = Vector3.ClampMagnitude(acceleration, maxAcceleration);
            return acceleration;
        }


        void DoPoweredRotationMatching(AnimatedPair pair, BoneProfile boneProfile, float dt)
        {
            RagdollBone bone = pair.RagdollBone;

            Rigidbody rigidbody = bone.Rigidbody;
            float alpha = boneProfile.rotationAlpha;
            float dampingRatio = boneProfile.rotationDampingRatio;

            SetTargetRotation(pair);
            SetTargetAngularVelocityLocal(bone.Joint, pair.poseAngularVelocity, pair.RagdollBone.StartingJointRotation);

            float effectiveAngularMass = RagdollSettings
                ? RagdollSettings.GetRotationDriveEffectiveMass(rigidbody)
                : rigidbody.mass;
            bone.Joint.slerpDrive = AnimationMatching.GetRotationMatchingJointDrive(
                alpha,
                dampingRatio,
                effectiveAngularMass,
                dt,
                boneProfile.maxAngularAcceleration);

            if (pinSettings.AngularPinning)
            {
                Vector3 velocityChange =
                    RagdollPinMath.ResolveAngularVelocityChange(
                        rigidbody.rotation,
                        pair.currentPose.worldRotation,
                        rigidbody.angularVelocity,
                        boneProfile.PositionPinWeight,
                        pinSettings.PinPow,
                        dt);
                rigidbody.AddTorque(
                    velocityChange,
                    ForceMode.VelocityChange);
            }
        }

        void SetTargetRotation(AnimatedPair pair)
        {
            ConfigurableJoint joint = pair.RagdollBone.Joint;

            if (joint.configuredInWorldSpace)
            {
                joint.SetTargetRotation(pair.currentPose.worldRotation, pair.RagdollBone.StartingJointRotation);
            }
            else
            {
                joint.SetTargetRotationLocal(pair.currentPose.localRotation, pair.RagdollBone.StartingJointRotation);
            }
        }

        void SetTargetAngularVelocityLocal(ConfigurableJoint joint, Vector3 targetAngularVelocity, Quaternion startingLocalRotation)
        {
            //So. With k = 0, the joint applies forces to match target angular velocity. These forces are limited (?????) by damping AND by max forces. WHAT. 
            //Also, the velocity needs to be INVERTED. So, target = -velocity.
            //With k > 0, the joint uses a spring to get to the target rotation. In this case, target velocity is ignored for applying forces, but it is used for damping (so, relative velocity damping).
            //In this case, the velocity must NOT be inverted. Huh.

            joint.targetAngularVelocity = startingLocalRotation * targetAngularVelocity;
        }

        void SetUnpoweredJointDrive(AnimatedPair pair)
        {
            pair.RagdollBone.Joint.slerpDrive = new JointDrive()
            {
                maximumForce = 0,
                positionDamper = 0,
                positionSpring = 0
            };
        }
        #endregion

        #region Target Update
        void ForceAnimatorUpdate()
        {
            if (targetAnimator)
            {
                targetAnimator.Update(0);
            }
        }

        void ReadAnimatedPose(
            bool notifyHooks = true,
            bool processPendingTeleport = true)
        {
            if (notifyHooks) InvokeReadHooks();
            if (processPendingTeleport) ProcessPendingTeleport();

            float sampleTime = GetAnimationSampleTime();

            // animatedPairs are sorted parent-first. This allows local joint rotations to
            // be reconstructed from the desired ragdoll world poses even when the Target
            // hierarchy uses different local bone axes.
            foreach (AnimatedPair pair in animatedPairs)
            {
                AnimatedPose targetPose = AnimatedPose.Read(pair.TargetBone);
                AnimatedPose ragdollPose = pair.ConvertTargetPoseToRagdoll(targetPose);
                ragdollPose.localRotation = CalculateRagdollLocalRotation(
                    pair,
                    ragdollPose.worldRotation);

                pair.SampleAnimatedPose(
                    targetPose,
                    ragdollPose,
                    sampleTime);
            }
        }

        Quaternion CalculateRagdollLocalRotation(
            AnimatedPair pair,
            Quaternion ragdollWorldRotation)
        {
            RagdollBoneHandle parentHandle;
            if (Bindings.Topology.TryGetParent(pair.Handle, out parentHandle))
            {
                AnimatedPair parentPair = animatedPairsByHandleIndex[parentHandle.Index];
                Quaternion parentWorldRotation = parentPair != null
                    ? parentPair.currentPose.worldRotation
                    : Bindings.GetBone(parentHandle).Transform.rotation;

                return Quaternion.Inverse(parentWorldRotation)
                    * ragdollWorldRotation;
            }

            Transform ragdollParent = pair.RagdollBone.Transform.parent;
            return ragdollParent
                ? Quaternion.Inverse(ragdollParent.rotation) * ragdollWorldRotation
                : ragdollWorldRotation;
        }

        float GetAnimationSampleTime()
        {
            if (UsesFixedAnimatorUpdate())
            {
                return Time.fixedTime;
            }

            if (targetAnimator && targetAnimator.updateMode == AnimatorUpdateMode.UnscaledTime)
            {
                return Time.unscaledTime;
            }

            return Time.time;
        }

        bool UsesFixedAnimatorUpdate()
        {
            return targetAnimator && targetAnimator.updateMode == AnimatorUpdateMode.AnimatePhysics;
        }

        void RestoreAnimatedPose()
        {
            foreach (AnimatedPair pair in animatedPairs)
            {
                pair.RestoreAnimatedPose();
            }
        }

        void ModifyTargetPose()
        {
            foreach (ITargetPoseModifier modifier in targetPoseModifiers)
            {
                modifier.ModifyPose(animatedPairs);
            }
        }
        #endregion

        #region State Change Operations
        public void SnapToTargetPose()
        {
            MovePuppetToTargetPose(true);
        }

        void MovePuppetToTargetPose(bool clearVelocities)
        {
            if (animatedPairs is null) return;

            foreach (AnimatedPair pair in animatedPairs)
            {
                Rigidbody rb = pair.RagdollBone.Rigidbody;
                AnimatedPose targetPose = AnimatedPose.Read(pair.TargetBone);
                AnimatedPose ragdollPose = pair.ConvertTargetPoseToRagdoll(targetPose);

                rb.position = ragdollPose.worldPosition;
                rb.rotation = ragdollPose.worldRotation;

                if (clearVelocities)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }

        void UnpowerAllJoints()
        {
            if (animatedPairs is null) return;

            foreach (AnimatedPair pair in animatedPairs)
            {
                if (pair.RagdollBone.Joint)
                {
                    SetUnpoweredJointDrive(pair);
                }
            }
        }
        #endregion
    }
}