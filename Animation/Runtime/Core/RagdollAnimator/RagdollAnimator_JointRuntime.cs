using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    public partial class RagdollAnimator
    {
        [SerializeField] RagdollJointRuntimeSettings jointRuntimeSettings =
            RagdollJointRuntimeSettings.Default;

        [NonSerialized] bool manualAngularLimitControl;
        [NonSerialized] bool pendingManualAngularLimitWrite;
        [NonSerialized] bool pendingManualAngularLimitValue;
        [NonSerialized] bool hasManualAngularLimitValue;
        [NonSerialized] bool manualAngularLimitValue;

        JointAnchorRecord[] jointAnchorRecords;
        bool jointRuntimeInitialized;
        int lastJointAnchorUpdateCount;
        int lastJointAnchorSkippedCount;

        public RagdollJointRuntimeSettings JointRuntimeSettings
        {
            get
            {
                RagdollJointRuntimeSettings result = jointRuntimeSettings;
                result.Normalize();
                return result;
            }
            set
            {
                value.Normalize();
                jointRuntimeSettings = value;
                RefreshJointRuntimeConfiguration();
            }
        }

        public bool UpdateJointAnchors
        {
            get => JointRuntimeSettings.UpdateJointAnchors;
            set
            {
                RagdollJointRuntimeSettings settings = jointRuntimeSettings;
                settings.Normalize();
                settings.UpdateJointAnchors = value;
                jointRuntimeSettings = settings;
                RefreshJointRuntimeConfiguration();
            }
        }

        public bool SupportTranslationAnimation
        {
            get => JointRuntimeSettings.SupportTranslationAnimation;
            set
            {
                RagdollJointRuntimeSettings settings = jointRuntimeSettings;
                settings.Normalize();
                settings.SupportTranslationAnimation = value;
                jointRuntimeSettings = settings;
                RefreshJointRuntimeConfiguration();
            }
        }

        public bool AngularLimits
        {
            get => JointRuntimeSettings.AngularLimits;
            set
            {
                RagdollJointRuntimeSettings settings = jointRuntimeSettings;
                settings.Normalize();
                settings.AngularLimits = value;
                jointRuntimeSettings = settings;
                ApplyAutomaticAngularLimits();
            }
        }

        /// <summary>
        /// When true, automatic angular-limit writes are suspended. Use
        /// SetAngularLimitsManual to apply either authored motions or Free explicitly.
        /// Lifecycle death policies also respect this ownership flag.
        /// </summary>
        public bool ManualAngularLimitControl
        {
            get => manualAngularLimitControl;
            set
            {
                if (manualAngularLimitControl == value) return;
                manualAngularLimitControl = value;
                if (!manualAngularLimitControl)
                {
                    ApplyAutomaticAngularLimits();
                }
            }
        }

        public int RuntimeJointAnchorCount =>
            jointAnchorRecords == null ? 0 : jointAnchorRecords.Length;
        public int RuntimeAngularJointCount =>
            lifecyclePhysicsPolicy == null
                ? 0
                : lifecyclePhysicsPolicy.JointCount;
        public int LastJointAnchorUpdateCount =>
            lastJointAnchorUpdateCount;
        public int LastJointAnchorSkippedCount =>
            lastJointAnchorSkippedCount;

        /// <summary>
        /// Applies angular motions immediately without changing the serialized automatic
        /// toggle. Passing true restores each joint's authored motions; false frees all
        /// registered angular axes. Calls made before Start are queued until runtime
        /// joint state has initialized.
        /// </summary>
        public void SetAngularLimitsManual(bool limited)
        {
            hasManualAngularLimitValue = true;
            manualAngularLimitValue = limited;
            if (lifecyclePhysicsPolicy == null)
            {
                pendingManualAngularLimitWrite = true;
                pendingManualAngularLimitValue = limited;
                return;
            }

            lifecyclePhysicsPolicy.SetAngularLimits(limited);
        }

        void InitializeJointRuntime()
        {
            if (jointRuntimeInitialized) return;

            jointRuntimeSettings.Normalize();
            jointAnchorRecords = CreateJointAnchorRecords();
            jointRuntimeInitialized = true;

            try
            {
                ApplyAutomaticAngularLimits();
                if (manualAngularLimitControl
                    && hasManualAngularLimitValue)
                {
                    lifecyclePhysicsPolicy.SetAngularLimits(
                        manualAngularLimitValue);
                    pendingManualAngularLimitWrite = false;
                }
                else if (pendingManualAngularLimitWrite)
                {
                    lifecyclePhysicsPolicy.SetAngularLimits(
                        pendingManualAngularLimitValue);
                    pendingManualAngularLimitWrite = false;
                }

                RefreshJointRuntimeConfiguration();
            }
            catch
            {
                ReleaseJointAnchorRecords();
                if (lifecyclePhysicsPolicy != null
                    && !lifecyclePhysicsPolicy.IsActive)
                {
                    lifecyclePhysicsPolicy.SetAngularLimits(true);
                }
                jointAnchorRecords = null;
                jointRuntimeInitialized = false;
                throw;
            }
        }

        JointAnchorRecord[] CreateJointAnchorRecords()
        {
            if (animatedPairs == null) return new JointAnchorRecord[0];

            List<JointAnchorRecord> records =
                new List<JointAnchorRecord>();
            try
            {
                for (int index = 0; index < animatedPairs.Length; index++)
                {
                    AnimatedPair child = animatedPairs[index];
                    RagdollBoneHandle parentHandle;
                    if (!Bindings.Topology.TryGetParent(
                        child.Handle,
                        out parentHandle))
                    {
                        continue;
                    }

                    AnimatedPair parent =
                        animatedPairsByHandleIndex[parentHandle.Index];
                    ConfigurableJoint joint = child.RagdollBone.Joint;
                    if (parent == null
                        || !joint
                        || !joint.connectedBody)
                    {
                        continue;
                    }

                    if (joint.connectedBody != parent.RagdollBone.Rigidbody)
                    {
                        throw new InvalidOperationException(
                            "A runtime joint anchor record does not match the "
                            + "connectedBody-derived ragdoll topology.");
                    }

                    records.Add(new JointAnchorRecord(
                        child,
                        parent));
                }

                return records.ToArray();
            }
            catch
            {
                for (int index = 0; index < records.Count; index++)
                {
                    records[index].ReleaseRuntimeOwnership();
                }
                throw;
            }
        }

        void UpdateJointRuntimeBeforeSimulation()
        {
            if (!jointRuntimeInitialized) return;

            ApplyAutomaticAngularLimits();
            RefreshJointRuntimeConfiguration();
        }

        void RefreshJointRuntimeConfiguration()
        {
            lastJointAnchorUpdateCount = 0;
            lastJointAnchorSkippedCount = 0;
            if (!jointRuntimeInitialized || jointAnchorRecords == null) return;

            RagdollJointRuntimeSettings settings = jointRuntimeSettings;
            settings.Normalize();
            jointRuntimeSettings = settings;

            for (int index = 0; index < jointAnchorRecords.Length; index++)
            {
                JointAnchorRecord record = jointAnchorRecords[index];
                bool shouldUpdate = RagdollJointAnchorMath.ShouldUpdateAnchor(
                    settings.UpdateJointAnchors,
                    settings.SupportTranslationAnimation,
                    record.DirectTargetParent);

                if (!shouldUpdate)
                {
                    record.RestoreAuthoredAnchor();
                    lastJointAnchorSkippedCount++;
                    continue;
                }

                // PuppetMaster updates anchors only while the active state is Alive.
                // During the kill blend the active state remains Alive, so the last
                // animated anchor is retained when Dead/Frozen becomes stable.
                if (activeLifecycleState != RagdollLifecycleState.Alive)
                {
                    lastJointAnchorSkippedCount++;
                    continue;
                }

                if (record.TryApplyResolvedAnchor())
                {
                    lastJointAnchorUpdateCount++;
                }
                else
                {
                    record.RestoreAuthoredAnchor();
                    lastJointAnchorSkippedCount++;
                }
            }
        }

        void ApplyAutomaticAngularLimits()
        {
            if (!jointRuntimeInitialized
                || manualAngularLimitControl
                || lifecyclePhysicsPolicy == null
                || lifecyclePhysicsPolicy.IsActive)
            {
                return;
            }

            RagdollJointRuntimeSettings settings = jointRuntimeSettings;
            settings.Normalize();
            jointRuntimeSettings = settings;
            if (!lifecyclePhysicsPolicy.AngularLimitsMatch(
                settings.AngularLimits))
            {
                lifecyclePhysicsPolicy.SetAngularLimits(
                    settings.AngularLimits);
            }
        }

        void ShutdownJointRuntime()
        {
            ReleaseJointAnchorRecords();

            if (lifecyclePhysicsPolicy != null
                && !lifecyclePhysicsPolicy.IsActive)
            {
                lifecyclePhysicsPolicy.SetAngularLimits(true);
            }

            jointAnchorRecords = null;
            jointRuntimeInitialized = false;
            lastJointAnchorUpdateCount = 0;
            lastJointAnchorSkippedCount = 0;
        }

        void ReleaseJointAnchorRecords()
        {
            if (jointAnchorRecords == null) return;

            for (int index = 0;
                index < jointAnchorRecords.Length;
                index++)
            {
                jointAnchorRecords[index].ReleaseRuntimeOwnership();
            }
        }

        sealed class JointAnchorRecord
        {
            readonly AnimatedPair child;
            readonly AnimatedPair parent;
            readonly ConfigurableJoint joint;
            readonly RagdollJointAnchorState anchorState;

            internal bool DirectTargetParent { get; }

            internal JointAnchorRecord(
                AnimatedPair child,
                AnimatedPair parent)
            {
                this.child = child;
                this.parent = parent;
                joint = child.RagdollBone.Joint;
                anchorState = new RagdollJointAnchorState(joint);
                DirectTargetParent =
                    child.TargetBone.parent == parent.TargetBone;
            }

            internal bool TryApplyResolvedAnchor()
            {
                if (!joint
                    || !joint.connectedBody
                    || joint.connectedBody != parent.RagdollBone.Rigidbody)
                {
                    return false;
                }

                Vector3 connectedAnchor;
                if (!RagdollJointAnchorMath.TryResolveConnectedAnchor(
                    child.currentPose.worldPosition,
                    child.currentPose.worldRotation,
                    child.RagdollBone.Transform.lossyScale,
                    joint.anchor,
                    parent.currentPose.worldPosition,
                    parent.currentPose.worldRotation,
                    parent.RagdollBone.Transform.lossyScale,
                    out connectedAnchor))
                {
                    return false;
                }

                return anchorState.TryApply(connectedAnchor);
            }

            internal void RestoreAuthoredAnchor()
            {
                anchorState.RestoreAuthoredAnchor();
            }

            internal void ReleaseRuntimeOwnership()
            {
                anchorState.ReleaseRuntimeOwnership();
            }
        }
    }
}
