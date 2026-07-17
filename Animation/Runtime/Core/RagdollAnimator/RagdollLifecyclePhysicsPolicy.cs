using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Captures authored angular motions shared by global joint controls and the
    /// Dead/Frozen lifecycle. Internal collisions are owned separately by
    /// RagdollInternalCollisionController so automatic, manual and death policies
    /// cannot restore the same collider pair in conflicting orders.
    /// </summary>
    internal sealed class RagdollLifecyclePhysicsPolicy
    {
        internal struct AngularMotionState
        {
            internal ConfigurableJointMotion X;
            internal ConfigurableJointMotion Y;
            internal ConfigurableJointMotion Z;

            internal AngularMotionState(ConfigurableJoint joint)
            {
                X = joint.angularXMotion;
                Y = joint.angularYMotion;
                Z = joint.angularZMotion;
            }

            internal void Apply(ConfigurableJoint joint)
            {
                joint.angularXMotion = X;
                joint.angularYMotion = Y;
                joint.angularZMotion = Z;
            }

            internal bool Matches(ConfigurableJoint joint)
            {
                return joint.angularXMotion == X
                    && joint.angularYMotion == Y
                    && joint.angularZMotion == Z;
            }
        }

        internal sealed class JointRecord
        {
            internal readonly int BoneIndex;
            internal readonly ConfigurableJoint Joint;
            internal readonly AngularMotionState Authored;
            internal AngularMotionState BeforeKill;
            internal bool CapturedBeforeKill;

            internal JointRecord(int boneIndex, ConfigurableJoint joint)
            {
                if (boneIndex < 0) throw new ArgumentOutOfRangeException(nameof(boneIndex));
                if (!joint) throw new ArgumentNullException(nameof(joint));
                BoneIndex = boneIndex;
                Joint = joint;
                Authored = new AngularMotionState(joint);
                BeforeKill = Authored;
            }
        }

        readonly JointRecord[] joints;
        bool[] disconnectedBones;
        bool active;
        bool angularLimitsApplied;

        internal bool IsActive => active;
        internal bool AngularLimitsApplied => angularLimitsApplied;
        internal int JointCount => joints.Length;

        internal RagdollLifecyclePhysicsPolicy(JointRecord[] joints)
        {
            this.joints = joints ?? new JointRecord[0];
        }

        internal static RagdollLifecyclePhysicsPolicy Create(
            RagdollDefinitionBindings bindings)
        {
            if (!bindings) throw new ArgumentNullException(nameof(bindings));
            if (!bindings.IsInitialized)
            {
                throw new InvalidOperationException(
                    "Lifecycle physical policy requires initialized ragdoll bindings.");
            }

            List<JointRecord> jointRecords = new List<JointRecord>();
            for (int index = 0; index < bindings.BoneCount; index++)
            {
                RagdollBone bone = bindings.GetBoneAt(index);
                if (bone.Joint)
                {
                    jointRecords.Add(new JointRecord(index, bone.Joint));
                }
            }

            return new RagdollLifecyclePhysicsPolicy(
                jointRecords.ToArray());
        }

        internal void SetDisconnectedBones(bool[] disconnected)
        {
            int requiredLength = 0;
            for (int index = 0; index < joints.Length; index++)
            {
                requiredLength = Math.Max(requiredLength, joints[index].BoneIndex + 1);
            }
            if (disconnected != null && disconnected.Length < requiredLength)
            {
                throw new ArgumentException(
                    "The disconnected-bone mask must cover every joint policy bone index.",
                    nameof(disconnected));
            }
            disconnectedBones = disconnected == null
                ? null
                : (bool[])disconnected.Clone();
        }

        internal void SetAngularLimits(bool limited)
        {
            for (int index = 0; index < joints.Length; index++)
            {
                JointRecord record = joints[index];
                if (IsDisconnected(record.BoneIndex)) continue;
                if (!record.Joint) continue;

                if (limited)
                {
                    record.Authored.Apply(record.Joint);
                }
                else
                {
                    record.Joint.angularXMotion =
                        ConfigurableJointMotion.Free;
                    record.Joint.angularYMotion =
                        ConfigurableJointMotion.Free;
                    record.Joint.angularZMotion =
                        ConfigurableJointMotion.Free;
                }
            }
        }

        internal bool AngularLimitsMatch(bool limited)
        {
            for (int index = 0; index < joints.Length; index++)
            {
                JointRecord record = joints[index];
                if (IsDisconnected(record.BoneIndex)) continue;
                if (!record.Joint) continue;

                if (limited)
                {
                    if (!record.Authored.Matches(record.Joint))
                    {
                        return false;
                    }
                }
                else if (record.Joint.angularXMotion
                        != ConfigurableJointMotion.Free
                    || record.Joint.angularYMotion
                        != ConfigurableJointMotion.Free
                    || record.Joint.angularZMotion
                        != ConfigurableJointMotion.Free)
                {
                    return false;
                }
            }

            return true;
        }

        internal void BeginKill(bool enableAngularLimits)
        {
            if (active)
            {
                throw new InvalidOperationException(
                    "Lifecycle kill policies are already active.");
            }

            active = true;
            angularLimitsApplied = enableAngularLimits;

            try
            {
                if (!enableAngularLimits) return;

                for (int index = 0; index < joints.Length; index++)
                {
                    JointRecord record = joints[index];
                    if (IsDisconnected(record.BoneIndex)) continue;
                    if (!record.Joint) continue;

                    record.BeforeKill =
                        new AngularMotionState(record.Joint);
                    record.CapturedBeforeKill = true;
                    record.Authored.Apply(record.Joint);
                }
            }
            catch
            {
                RestoreAfterDeath();
                throw;
            }
        }

        internal void RestoreAfterDeath()
        {
            if (!active) return;

            if (angularLimitsApplied)
            {
                for (int index = 0; index < joints.Length; index++)
                {
                    JointRecord record = joints[index];
                    if (IsDisconnected(record.BoneIndex)) continue;
                    if (record.CapturedBeforeKill && record.Joint)
                    {
                        record.BeforeKill.Apply(record.Joint);
                    }
                }
            }

            ClearState();
        }

        internal void AbandonForPermanentFreeze()
        {
            ClearState();
        }

        bool IsDisconnected(int index)
        {
            return disconnectedBones != null
                && index >= 0
                && index < disconnectedBones.Length
                && disconnectedBones[index];
        }

        void ClearState()
        {
            for (int index = 0; index < joints.Length; index++)
            {
                joints[index].CapturedBeforeKill = false;
            }

            active = false;
            angularLimitsApplied = false;
        }
    }
}
