using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    public partial class RagdollAnimator
    {
        sealed class RagdollHierarchySubsystemSnapshot
        {
            internal readonly RagdollMuscleController.HierarchySnapshot Muscles;
            internal readonly RagdollSimulationModeController.HierarchySnapshot Mode;

            internal RagdollHierarchySubsystemSnapshot(
                RagdollMuscleController.HierarchySnapshot muscles,
                RagdollSimulationModeController.HierarchySnapshot mode)
            {
                Muscles = muscles;
                Mode = mode;
            }
        }

        RagdollHierarchySubsystemSnapshot CaptureHierarchySubsystemSnapshot(
            AnimatedPair[] pairs)
        {
            RagdollMuscleController muscles =
                GetComponent<RagdollMuscleController>();
            RagdollSimulationModeController mode =
                GetComponent<RagdollSimulationModeController>();
            if (!muscles || !muscles.IsInitialized)
            {
                throw new InvalidOperationException(
                    "Ragdoll hierarchy mutation requires an initialized muscle controller.");
            }
            if (!mode || !mode.IsInitialized)
            {
                throw new InvalidOperationException(
                    "Ragdoll hierarchy mutation requires an initialized simulation mode controller.");
            }

            return new RagdollHierarchySubsystemSnapshot(
                muscles.CaptureHierarchySnapshot(pairs),
                mode.CaptureHierarchySnapshot());
        }

        void RebuildRuntimeHierarchy(
            AnimatedPair[] stateSourcePairs,
            RagdollHierarchySubsystemSnapshot subsystemSnapshot)
        {
            if (stateSourcePairs == null)
            {
                throw new ArgumentNullException(nameof(stateSourcePairs));
            }
            if (subsystemSnapshot == null)
            {
                throw new ArgumentNullException(nameof(subsystemSnapshot));
            }

            ShutdownInternalCollisions();
            ShutdownJointRuntime();

            RagdollTargetBinding[] resolvedBindings;
            string bindingError;
            resolvedBindings = ResolveCurrentTargetBindings(out bindingError);
            if (resolvedBindings == null)
            {
                throw new InvalidOperationException(bindingError);
            }

            mapper = new RagdollToTargetMapper(Bindings, resolvedBindings);
            CreateAnimatedPairs(mapper.BonePairs);
            RestoreRetainedAnimatedPairState(stateSourcePairs);
            InitializeAddedAnimatedPairs(stateSourcePairs);
            InheritAddedPowerSettings(stateSourcePairs);

            RagdollMuscleController muscles =
                GetComponent<RagdollMuscleController>();
            muscles.RebuildHierarchy(animatedPairs, subsystemSnapshot.Muscles);

            RagdollSimulationModeController mode =
                GetComponent<RagdollSimulationModeController>();
            mode.RebuildHierarchy(animatedPairs, subsystemSnapshot.Mode);

            RagdollBehaviourController behaviours =
                GetComponent<RagdollBehaviourController>();
            if (behaviours && behaviours.IsInitialized)
            {
                behaviours.RebuildHierarchy(animatedPairs);
            }

            ReinitializeExternalModifiers(muscles, mode, behaviours);
            lifecyclePhysicsPolicy =
                RagdollLifecyclePhysicsPolicy.Create(Bindings);
            InitializeJointRuntime();
            InitializeInternalCollisions();
            RefreshJointRuntimeConfiguration();
            ReapplyInternalCollisionPolicy();
        }

        void RestoreRetainedAnimatedPairState(AnimatedPair[] oldPairs)
        {
            Dictionary<BoneName, AnimatedPair> oldByName =
                new Dictionary<BoneName, AnimatedPair>();
            for (int index = 0; index < oldPairs.Length; index++)
            {
                oldByName[oldPairs[index].Name] = oldPairs[index];
            }

            for (int index = 0; index < animatedPairs.Length; index++)
            {
                AnimatedPair previous;
                if (oldByName.TryGetValue(
                    animatedPairs[index].Name,
                    out previous))
                {
                    animatedPairs[index].CopyRuntimeStateFrom(previous);
                }
            }
        }

        void InitializeAddedAnimatedPairs(AnimatedPair[] oldPairs)
        {
            HashSet<BoneName> retained = new HashSet<BoneName>();
            for (int index = 0; index < oldPairs.Length; index++)
            {
                retained.Add(oldPairs[index].Name);
            }

            float sampleTime = GetAnimationSampleTime();
            for (int index = 0; index < animatedPairs.Length; index++)
            {
                AnimatedPair pair = animatedPairs[index];
                if (retained.Contains(pair.Name)) continue;

                AnimatedPose targetPose = AnimatedPose.Read(pair.TargetBone);
                AnimatedPose ragdollPose =
                    pair.ConvertTargetPoseToRagdoll(targetPose);
                ragdollPose.localRotation = CalculateRagdollLocalRotation(
                    pair,
                    ragdollPose.worldRotation);
                pair.SampleAnimatedPose(targetPose, ragdollPose, sampleTime);
            }
        }

        void InheritAddedPowerSettings(AnimatedPair[] oldPairs)
        {
            HashSet<BoneName> retained = new HashSet<BoneName>();
            for (int index = 0; index < oldPairs.Length; index++)
            {
                retained.Add(oldPairs[index].Name);
            }

            for (int index = 0; index < animatedPairs.Length; index++)
            {
                AnimatedPair pair = animatedPairs[index];
                if (retained.Contains(pair.Name)) continue;

                RagdollBoneHandle parentHandle;
                if (Bindings.Topology.TryGetParent(
                    pair.Handle,
                    out parentHandle))
                {
                    pair.RagdollBone.PowerSetting =
                        Bindings.GetBone(parentHandle).PowerSetting;
                }
            }
        }

        void ReinitializeExternalModifiers(
            RagdollMuscleController muscles,
            RagdollSimulationModeController mode,
            RagdollBehaviourController behaviours)
        {
            for (int index = 0; index < boneProfileModifiers.Length; index++)
            {
                IBoneProfileModifier modifier = boneProfileModifiers[index];
                if (ReferenceEquals(modifier, muscles)
                    || ReferenceEquals(modifier, mode)
                    || ReferenceEquals(modifier, behaviours))
                {
                    continue;
                }
                modifier.Initialize(animatedPairs);
            }

            for (int index = 0; index < targetPoseModifiers.Length; index++)
            {
                ITargetPoseModifier modifier = targetPoseModifiers[index];
                if (ReferenceEquals(modifier, behaviours)) continue;
                modifier.Initialize(animatedPairs);
            }
        }
    }
}
