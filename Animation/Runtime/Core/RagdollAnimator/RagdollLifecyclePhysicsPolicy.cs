using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Reversible physical policy applied for the complete Dead/Frozen lifecycle. It
    /// restores authored angular motions while dying and temporarily enables collisions
    /// between every pair of registered ragdoll bones without replacing collider enabled
    /// state or collision materials.
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
            internal readonly ConfigurableJoint Joint;
            internal readonly AngularMotionState Authored;
            internal AngularMotionState BeforeKill;
            internal bool CapturedBeforeKill;

            internal JointRecord(ConfigurableJoint joint)
            {
                Joint = joint;
                Authored = new AngularMotionState(joint);
                BeforeKill = Authored;
            }
        }

        internal sealed class ColliderPair
        {
            internal readonly Collider First;
            internal readonly Collider Second;
            internal bool IgnoredBeforeKill;
            internal bool CapturedBeforeKill;

            internal ColliderPair(Collider first, Collider second)
            {
                First = first;
                Second = second;
            }
        }

        readonly JointRecord[] joints;
        readonly ColliderPair[] colliderPairs;
        bool active;
        bool angularLimitsApplied;
        bool internalCollisionsApplied;

        internal bool IsActive => active;
        internal bool AngularLimitsApplied => angularLimitsApplied;
        internal bool InternalCollisionsApplied => internalCollisionsApplied;
        internal int InternalColliderPairCount => colliderPairs.Length;

        internal RagdollLifecyclePhysicsPolicy(
            JointRecord[] joints,
            ColliderPair[] colliderPairs)
        {
            this.joints = joints ?? new JointRecord[0];
            this.colliderPairs = colliderPairs ?? new ColliderPair[0];
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
            List<Collider[]> colliderGroups = new List<Collider[]>();

            foreach (RagdollBone bone in bindings.Bones)
            {
                if (bone.Joint)
                {
                    jointRecords.Add(new JointRecord(bone.Joint));
                }

                List<Collider> colliders = new List<Collider>();
                foreach (Collider collider in bone.Colliders)
                {
                    if (collider) colliders.Add(collider);
                }
                colliderGroups.Add(colliders.ToArray());
            }

            List<ColliderPair> pairs = new List<ColliderPair>();
            for (int firstBone = 0;
                firstBone < colliderGroups.Count;
                firstBone++)
            {
                Collider[] firstColliders = colliderGroups[firstBone];
                for (int secondBone = firstBone + 1;
                    secondBone < colliderGroups.Count;
                    secondBone++)
                {
                    Collider[] secondColliders = colliderGroups[secondBone];
                    for (int first = 0; first < firstColliders.Length; first++)
                    {
                        for (int second = 0;
                            second < secondColliders.Length;
                            second++)
                        {
                            pairs.Add(new ColliderPair(
                                firstColliders[first],
                                secondColliders[second]));
                        }
                    }
                }
            }

            return new RagdollLifecyclePhysicsPolicy(
                jointRecords.ToArray(),
                pairs.ToArray());
        }

        internal void BeginKill(
            bool enableAngularLimits,
            bool enableInternalCollisions)
        {
            if (active)
            {
                throw new InvalidOperationException(
                    "Lifecycle kill policies are already active.");
            }

            active = true;
            angularLimitsApplied = enableAngularLimits;
            internalCollisionsApplied = enableInternalCollisions;

            try
            {
                if (enableAngularLimits)
                {
                    for (int index = 0; index < joints.Length; index++)
                    {
                        JointRecord record = joints[index];
                        if (!record.Joint) continue;

                        record.BeforeKill =
                            new AngularMotionState(record.Joint);
                        record.CapturedBeforeKill = true;
                        record.Authored.Apply(record.Joint);
                    }
                }

                if (enableInternalCollisions)
                {
                    for (int index = 0;
                        index < colliderPairs.Length;
                        index++)
                    {
                        ColliderPair pair = colliderPairs[index];
                        if (!pair.First || !pair.Second) continue;

                        pair.IgnoredBeforeKill = Physics.GetIgnoreCollision(
                            pair.First,
                            pair.Second);
                        pair.CapturedBeforeKill = true;
                        Physics.IgnoreCollision(
                            pair.First,
                            pair.Second,
                            false);
                    }
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

            if (internalCollisionsApplied)
            {
                for (int index = 0;
                    index < colliderPairs.Length;
                    index++)
                {
                    ColliderPair pair = colliderPairs[index];
                    if (!pair.CapturedBeforeKill
                        || !pair.First
                        || !pair.Second)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(
                        pair.First,
                        pair.Second,
                        pair.IgnoredBeforeKill);
                }
            }

            if (angularLimitsApplied)
            {
                for (int index = 0; index < joints.Length; index++)
                {
                    JointRecord record = joints[index];
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

        void ClearState()
        {
            for (int index = 0; index < joints.Length; index++)
            {
                joints[index].CapturedBeforeKill = false;
            }
            for (int index = 0; index < colliderPairs.Length; index++)
            {
                colliderPairs[index].CapturedBeforeKill = false;
            }

            active = false;
            angularLimitsApplied = false;
            internalCollisionsApplied = false;
        }
    }
}
