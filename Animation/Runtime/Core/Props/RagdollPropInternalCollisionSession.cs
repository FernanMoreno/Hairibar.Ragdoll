using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    internal struct RagdollPropCollisionMuscle
    {
        internal readonly RagdollBoneHandle Handle;
        internal readonly BoneName Bone;
        internal readonly RagdollMuscleGroup Group;
        internal readonly bool HasSemanticGroup;
        internal readonly Collider[] Colliders;

        internal RagdollPropCollisionMuscle(
            RagdollBoneHandle handle,
            BoneName bone,
            RagdollMuscleGroup group,
            Collider[] colliders)
            : this(handle, bone, group, true, colliders)
        {
        }

        internal RagdollPropCollisionMuscle(
            RagdollBoneHandle handle,
            BoneName bone,
            RagdollMuscleGroup group,
            bool hasSemanticGroup,
            Collider[] colliders)
        {
            Handle = handle;
            Bone = bone;
            Group = group;
            HasSemanticGroup = hasSemanticGroup;
            Colliders = colliders ?? new Collider[0];
        }
    }

    /// <summary>
    /// Reversible Physics.IgnoreCollision overlay for one held prop. Unity does not
    /// persist IgnoreCollision through every disable/enable route, so the forced state
    /// is rearmed after the core Puppet owners on every fixed step. Baselines are restored
    /// exactly on drop; a true baseline waits for both colliders to become active again.
    /// </summary>
    internal sealed class RagdollPropInternalCollisionSession
    {
        internal sealed class Pair
        {
            internal readonly Collider PropCollider;
            internal readonly Collider MuscleCollider;
            internal readonly bool BaselineIgnored;
            internal bool Restored;

            internal Pair(
                Collider propCollider,
                Collider muscleCollider,
                bool baselineIgnored)
            {
                PropCollider = propCollider;
                MuscleCollider = muscleCollider;
                BaselineIgnored = baselineIgnored;
            }
        }

        readonly Pair[] pairs;
        bool releaseRequested;

        internal int PairCount => pairs.Length;
        internal bool ReleaseRequested => releaseRequested;
        internal bool IsReleased { get; private set; }

        internal RagdollPropInternalCollisionSession(Pair[] pairs)
        {
            this.pairs = pairs ?? new Pair[0];
            IsReleased = this.pairs.Length == 0;
        }

        internal static bool TryCreate(
            RagdollProp prop,
            RagdollAnimator animator,
            RagdollBoneHandle slotHandle,
            RagdollPropInternalCollisionSettings settings,
            out RagdollPropInternalCollisionSession session,
            out string error)
        {
            session = null;
            error = null;
            if (!prop)
            {
                error = "A live RagdollProp is required to create collision ignores.";
                return false;
            }
            if (settings == null || !settings.HasRules)
            {
                session = new RagdollPropInternalCollisionSession(new Pair[0]);
                return true;
            }
            if (!animator || !animator.Bindings || !animator.Bindings.IsInitialized)
            {
                error = "Prop internal-collision ignores require an initialized RagdollAnimator.";
                return false;
            }

            RagdollMuscleController muscles =
                animator.GetComponent<RagdollMuscleController>();
            if (!muscles || !muscles.IsInitialized)
            {
                error = "Prop internal-collision ignores require an initialized RagdollMuscleController.";
                return false;
            }

            try
            {
                RagdollPropCollisionMuscle[] candidates =
                    BuildRuntimeMuscles(animator.Bindings, muscles);
                return TryCreate(
                    prop.GetPhysicalColliders(),
                    candidates,
                    slotHandle,
                    settings,
                    out session,
                    out error);
            }
            catch (Exception exception)
            {
                session = null;
                error = "Prop internal-collision resolution failed: "
                    + exception.Message;
                return false;
            }
        }

        internal static bool TryCreate(
            Collider[] propColliders,
            RagdollPropCollisionMuscle[] muscles,
            RagdollBoneHandle? slotHandle,
            RagdollPropInternalCollisionSettings settings,
            out RagdollPropInternalCollisionSession session,
            out string error)
        {
            session = null;
            error = null;
            settings = settings ?? new RagdollPropInternalCollisionSettings();
            settings.Normalize();

            List<Pair> resolved = new List<Pair>();
            HashSet<ulong> unique = new HashSet<ulong>();
            Collider[] sources = propColliders ?? new Collider[0];
            RagdollPropCollisionMuscle[] targets = muscles
                ?? new RagdollPropCollisionMuscle[0];

            for (int muscleIndex = 0; muscleIndex < targets.Length; muscleIndex++)
            {
                RagdollPropCollisionMuscle target = targets[muscleIndex];
                if ((slotHandle.HasValue && target.Handle == slotHandle.Value)
                    || !settings.Matches(
                        target.Bone,
                        target.Group,
                        target.HasSemanticGroup))
                {
                    continue;
                }

                for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
                {
                    Collider source = sources[sourceIndex];
                    if (!source || source.isTrigger) continue;

                    for (int targetIndex = 0;
                        targetIndex < target.Colliders.Length;
                        targetIndex++)
                    {
                        Collider other = target.Colliders[targetIndex];
                        if (!other || other.isTrigger || source == other) continue;
                        if (source.attachedRigidbody
                            && source.attachedRigidbody == other.attachedRigidbody)
                        {
                            continue;
                        }

                        ulong key = PairKey(source, other);
                        if (!unique.Add(key)) continue;
                        resolved.Add(new Pair(
                            source,
                            other,
                            Physics.GetIgnoreCollision(source, other)));
                    }
                }
            }

            session = new RagdollPropInternalCollisionSession(resolved.ToArray());
            session.ReapplyForcedIgnores();
            return true;
        }

        internal void ReapplyForcedIgnores()
        {
            if (releaseRequested || IsReleased) return;
            for (int index = 0; index < pairs.Length; index++)
            {
                Pair pair = pairs[index];
                if (!CanWrite(pair.PropCollider, pair.MuscleCollider)) continue;
                if (!Physics.GetIgnoreCollision(
                    pair.PropCollider,
                    pair.MuscleCollider))
                {
                    Physics.IgnoreCollision(
                        pair.PropCollider,
                        pair.MuscleCollider,
                        true);
                }
            }
        }

        internal void RequestRelease()
        {
            releaseRequested = true;
            TryRestoreBaselines();
        }

        internal void ResumeForcedIgnores()
        {
            if (pairs.Length == 0) return;
            releaseRequested = false;
            IsReleased = false;
            for (int index = 0; index < pairs.Length; index++)
            {
                pairs[index].Restored = false;
            }
            ReapplyForcedIgnores();
        }

        internal bool TryRestoreBaselines()
        {
            if (IsReleased) return true;
            releaseRequested = true;
            bool complete = true;

            for (int index = 0; index < pairs.Length; index++)
            {
                Pair pair = pairs[index];
                if (pair.Restored) continue;
                if (!pair.PropCollider || !pair.MuscleCollider)
                {
                    pair.Restored = true;
                    continue;
                }

                if (!CanWrite(pair.PropCollider, pair.MuscleCollider))
                {
                    // Unity clears a non-persistent false baseline when either collider is
                    // disabled. A true authored baseline must wait until it can be written.
                    if (!pair.BaselineIgnored)
                    {
                        pair.Restored = true;
                    }
                    else
                    {
                        complete = false;
                    }
                    continue;
                }

                Physics.IgnoreCollision(
                    pair.PropCollider,
                    pair.MuscleCollider,
                    pair.BaselineIgnored);
                pair.Restored = true;
            }

            if (complete)
            {
                for (int index = 0; index < pairs.Length; index++)
                {
                    if (!pairs[index].Restored)
                    {
                        complete = false;
                        break;
                    }
                }
            }
            IsReleased = complete;
            return complete;
        }

        static RagdollPropCollisionMuscle[] BuildRuntimeMuscles(
            RagdollDefinitionBindings bindings,
            RagdollMuscleController muscles)
        {
            List<RagdollPropCollisionMuscle> result =
                new List<RagdollPropCollisionMuscle>(bindings.BoneCount);
            for (int index = 0; index < bindings.BoneCount; index++)
            {
                RagdollBoneHandle handle = bindings.GetHandleAt(index);
                RagdollBone bone = bindings.GetBoneAt(index);
                RagdollMuscleGroup group;
                bool hasSemanticGroup =
                    muscles.TryGetMuscleGroup(handle, out group);

                List<Collider> colliders = new List<Collider>();
                foreach (Collider collider in bone.Colliders)
                {
                    if (collider && !collider.isTrigger) colliders.Add(collider);
                }
                result.Add(new RagdollPropCollisionMuscle(
                    handle,
                    bone.Name,
                    group,
                    hasSemanticGroup,
                    colliders.ToArray()));
            }
            return result.ToArray();
        }

        static bool CanWrite(Collider first, Collider second)
        {
            return first && second
                && first.enabled && second.enabled
                && first.gameObject.activeInHierarchy
                && second.gameObject.activeInHierarchy;
        }

        static ulong PairKey(Collider first, Collider second)
        {
            uint a = unchecked((uint)first.GetInstanceID());
            uint b = unchecked((uint)second.GetInstanceID());
            if (a > b)
            {
                uint temporary = a;
                a = b;
                b = temporary;
            }
            return ((ulong)a << 32) | b;
        }
    }
}
