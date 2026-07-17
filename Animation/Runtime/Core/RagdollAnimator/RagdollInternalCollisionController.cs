using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>One reversible collider-pair binding owned by the core runtime.</summary>
    internal sealed class RagdollInternalCollisionPair
    {
        internal readonly Collider First;
        internal readonly Collider Second;
        internal readonly int FirstBoneIndex;
        internal readonly int SecondBoneIndex;
        internal readonly bool BaselineIgnored;

        internal RagdollInternalCollisionPair(
            Collider first,
            Collider second,
            int firstBoneIndex,
            int secondBoneIndex)
        {
            if (!first) throw new ArgumentNullException(nameof(first));
            if (!second) throw new ArgumentNullException(nameof(second));
            if (first == second)
            {
                throw new ArgumentException(
                    "An internal-collision pair requires two different colliders.");
            }
            if (firstBoneIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(firstBoneIndex));
            }
            if (secondBoneIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(secondBoneIndex));
            }
            if (firstBoneIndex == secondBoneIndex)
            {
                throw new ArgumentException(
                    "Internal-collision pairs must connect different bones.");
            }

            First = first;
            Second = second;
            FirstBoneIndex = firstBoneIndex;
            SecondBoneIndex = secondBoneIndex;
            BaselineIgnored = Physics.GetIgnoreCollision(first, second);
        }

        internal bool TryGetIgnored(out bool ignored)
        {
            if (!First || !Second)
            {
                ignored = false;
                return false;
            }

            ignored = Physics.GetIgnoreCollision(First, Second);
            return true;
        }

        internal bool Apply(bool ignored)
        {
            bool current;
            if (!TryGetIgnored(out current) || current == ignored)
            {
                return false;
            }

            Physics.IgnoreCollision(First, Second, ignored);
            return true;
        }

        internal bool RestoreBaseline()
        {
            return Apply(BaselineIgnored);
        }
    }

    /// <summary>
    /// Single owner for automatic, manual and lifecycle internal-collision policies.
    /// Every write is resolved through the same pair table and authored ignore matrix.
    /// </summary>
    internal sealed class RagdollInternalCollisionController
    {
        readonly RagdollInternalCollisionPair[] pairs;
        RagdollInternalCollisionIgnoreRuntime authoredIgnores;
        bool manualControl;
        bool hasManualValue;
        bool manualCollide;
        bool manualUseAuthoredIgnores;
        bool lifecycleOverrideActive;
        bool abandoned;
        bool automaticDirty = true;
        bool hasAutomaticValue;
        bool automaticCollide;
        bool[] disconnectedBones;
        int[] disconnectedCollisionIslands;
        bool[] lifecycleSnapshot;
        bool[] lifecycleSnapshotValid;
        readonly bool[] transactionSnapshot;
        readonly bool[] transactionSnapshotValid;

        internal int PairCount => pairs.Length;
        internal int AuthoredForcedBonePairCount =>
            authoredIgnores.ForcedBonePairCount;
        internal int ForcedColliderPairCount => CountForcedColliderPairs();
        internal bool ManualControl => manualControl;
        internal bool LifecycleOverrideActive => lifecycleOverrideActive;
        internal int LastWriteCount { get; private set; }

        internal RagdollInternalCollisionController(
            RagdollInternalCollisionPair[] pairs,
            RagdollInternalCollisionIgnoreRuntime authoredIgnores)
        {
            if (authoredIgnores == null)
            {
                throw new ArgumentNullException(nameof(authoredIgnores));
            }

            this.pairs = pairs ?? new RagdollInternalCollisionPair[0];
            this.authoredIgnores = authoredIgnores;
            ValidatePairs();
            transactionSnapshot = new bool[this.pairs.Length];
            transactionSnapshotValid = new bool[this.pairs.Length];
        }

        internal static RagdollInternalCollisionController Create(
            RagdollDefinitionBindings bindings,
            RagdollInternalCollisionIgnoreRuntime authoredIgnores)
        {
            if (!bindings) throw new ArgumentNullException(nameof(bindings));
            if (!bindings.IsInitialized)
            {
                throw new InvalidOperationException(
                    "Internal collision runtime requires initialized ragdoll bindings.");
            }
            if (authoredIgnores == null)
            {
                throw new ArgumentNullException(nameof(authoredIgnores));
            }
            if (authoredIgnores.BoneCount != bindings.BoneCount)
            {
                throw new InvalidOperationException(
                    "The authored internal-collision matrix belongs to a different ragdoll registry.");
            }

            Collider[][] collidersByBone = new Collider[bindings.BoneCount][];
            Dictionary<Collider, int> ownerByCollider =
                new Dictionary<Collider, int>();

            for (int boneIndex = 0;
                boneIndex < bindings.BoneCount;
                boneIndex++)
            {
                List<Collider> colliders = new List<Collider>();
                HashSet<Collider> seenOnBone = new HashSet<Collider>();
                foreach (Collider collider in bindings.GetBoneAt(boneIndex).Colliders)
                {
                    // Trigger colliders are event volumes, not physical muscle
                    // collision surfaces. PuppetMaster's compound-collider gathering
                    // excludes triggers from internal-collision ownership as well.
                    if (!collider
                        || collider.isTrigger
                        || !seenOnBone.Add(collider))
                    {
                        continue;
                    }

                    int existingOwner;
                    if (ownerByCollider.TryGetValue(collider, out existingOwner)
                        && existingOwner != boneIndex)
                    {
                        throw new InvalidOperationException(
                            "A collider is registered by more than one ragdoll bone.");
                    }
                    ownerByCollider[collider] = boneIndex;
                    colliders.Add(collider);
                }
                collidersByBone[boneIndex] = colliders.ToArray();
            }

            List<RagdollInternalCollisionPair> resolvedPairs =
                new List<RagdollInternalCollisionPair>();
            for (int firstBone = 0;
                firstBone < collidersByBone.Length;
                firstBone++)
            {
                for (int secondBone = firstBone + 1;
                    secondBone < collidersByBone.Length;
                    secondBone++)
                {
                    Collider[] firstColliders = collidersByBone[firstBone];
                    Collider[] secondColliders = collidersByBone[secondBone];
                    for (int first = 0; first < firstColliders.Length; first++)
                    {
                        for (int second = 0;
                            second < secondColliders.Length;
                            second++)
                        {
                            resolvedPairs.Add(
                                new RagdollInternalCollisionPair(
                                    firstColliders[first],
                                    secondColliders[second],
                                    firstBone,
                                    secondBone));
                        }
                    }
                }
            }

            return new RagdollInternalCollisionController(
                resolvedPairs.ToArray(),
                authoredIgnores);
        }

        internal void SetDisconnectedBones(bool[] disconnected)
        {
            int[] uniqueIslands = null;
            if (disconnected != null)
            {
                uniqueIslands = new int[disconnected.Length];
                for (int index = 0; index < disconnected.Length; index++)
                {
                    uniqueIslands[index] = disconnected[index]
                        ? index + 1
                        : 0;
                }
            }
            SetDisconnectedBones(disconnected, uniqueIslands);
        }

        internal void SetDisconnectedBones(
            bool[] disconnected,
            int[] collisionIslands)
        {
            if (disconnected != null
                && disconnected.Length != authoredIgnores.BoneCount)
            {
                throw new ArgumentException(
                    "The disconnected-bone mask belongs to a different ragdoll registry.",
                    nameof(disconnected));
            }
            if (collisionIslands != null
                && (disconnected == null
                    || collisionIslands.Length != disconnected.Length))
            {
                throw new ArgumentException(
                    "Disconnected collision islands must match the disconnected-bone mask.",
                    nameof(collisionIslands));
            }
            disconnectedBones = disconnected == null
                ? null
                : (bool[])disconnected.Clone();
            disconnectedCollisionIslands = collisionIslands == null
                ? null
                : (int[])collisionIslands.Clone();
            automaticDirty = true;
        }

        internal void SetAuthoredIgnores(
            RagdollInternalCollisionIgnoreRuntime runtime)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (runtime.BoneCount != authoredIgnores.BoneCount)
            {
                throw new InvalidOperationException(
                    "Cannot replace internal-collision ignores with a different bone registry.");
            }

            authoredIgnores = runtime;
            automaticDirty = true;
        }

        internal void SetManualControl(bool manual)
        {
            if (manualControl == manual) return;
            manualControl = manual;
            if (!manualControl) automaticDirty = true;
        }

        internal void FlagForReapply()
        {
            automaticDirty = true;
        }

        internal int ReapplyCurrentPolicy()
        {
            if (abandoned)
            {
                LastWriteCount = 0;
                return 0;
            }

            if (manualControl)
            {
                if (!hasManualValue)
                {
                    LastWriteCount = 0;
                    return 0;
                }

                LastWriteCount = ApplyPolicy(
                    manualCollide,
                    manualUseAuthoredIgnores);
                return LastWriteCount;
            }

            if (lifecycleOverrideActive)
            {
                LastWriteCount = ApplyPolicy(true, true);
                automaticDirty = false;
                return LastWriteCount;
            }

            if (!hasAutomaticValue)
            {
                LastWriteCount = 0;
                return 0;
            }

            LastWriteCount = ApplyPolicy(automaticCollide, true);
            automaticDirty = false;
            return LastWriteCount;
        }

        internal int UpdateAutomatic(bool collide)
        {
            bool valueChanged = !hasAutomaticValue
                || automaticCollide != collide;
            automaticCollide = collide;
            hasAutomaticValue = true;

            if (abandoned || manualControl)
            {
                LastWriteCount = 0;
                return 0;
            }

            if (lifecycleOverrideActive)
            {
                if (!automaticDirty)
                {
                    LastWriteCount = 0;
                    return 0;
                }

                try
                {
                    LastWriteCount = ApplyPolicy(true, true);
                    automaticDirty = false;
                    return LastWriteCount;
                }
                catch
                {
                    automaticDirty = true;
                    throw;
                }
            }

            if (!automaticDirty && !valueChanged)
            {
                LastWriteCount = 0;
                return 0;
            }

            try
            {
                LastWriteCount = ApplyPolicy(collide, true);
                automaticDirty = false;
                return LastWriteCount;
            }
            catch
            {
                automaticDirty = true;
                throw;
            }
        }

        internal int ApplyManual(
            bool collide,
            bool useInternalCollisionIgnores)
        {
            manualCollide = collide;
            manualUseAuthoredIgnores = useInternalCollisionIgnores;
            hasManualValue = true;

            if (abandoned)
            {
                LastWriteCount = 0;
                return 0;
            }

            LastWriteCount = ApplyPolicy(
                collide,
                useInternalCollisionIgnores);
            if (!manualControl) automaticDirty = true;
            return LastWriteCount;
        }

        internal void BeginLifecycleOverride(bool enable)
        {
            if (!enable || manualControl || abandoned) return;
            if (lifecycleOverrideActive)
            {
                throw new InvalidOperationException(
                    "The internal-collision lifecycle override is already active.");
            }

            lifecycleSnapshot = new bool[pairs.Length];
            lifecycleSnapshotValid = new bool[pairs.Length];
            lifecycleOverrideActive = true;

            try
            {
                for (int index = 0; index < pairs.Length; index++)
                {
                    bool ignored;
                    if (!pairs[index].TryGetIgnored(out ignored)) continue;
                    lifecycleSnapshot[index] = ignored;
                    lifecycleSnapshotValid[index] = true;
                }

                LastWriteCount = ApplyPolicy(true, true);
            }
            catch
            {
                // No policy write can escape ApplyPolicy without its own rollback.
                // Snapshot capture itself does not mutate Physics state.
                ClearLifecycleOverride();
                throw;
            }
        }

        internal void EndLifecycleOverride()
        {
            if (!lifecycleOverrideActive) return;

            try
            {
                if (!manualControl && hasAutomaticValue)
                {
                    LastWriteCount = ApplyPolicy(
                        automaticCollide,
                        true);
                    automaticDirty = false;
                }
                else if (!manualControl)
                {
                    LastWriteCount = RestoreLifecycleSnapshot();
                }
                else
                {
                    // Manual ownership has priority. Preserve whatever the manual owner
                    // applied while the lifecycle layer was active.
                    LastWriteCount = 0;
                }
            }
            catch
            {
                automaticDirty = true;
                throw;
            }
            finally
            {
                ClearLifecycleOverride();
            }
        }

        internal void AbandonForPermanentFreeze()
        {
            ClearLifecycleOverride();
            abandoned = true;
            automaticDirty = false;
            LastWriteCount = 0;
        }

        internal void Release()
        {
            if (abandoned) return;

            int writes = 0;
            Exception firstException = null;
            for (int index = 0; index < pairs.Length; index++)
            {
                try
                {
                    if (pairs[index].RestoreBaseline()) writes++;
                }
                catch (Exception exception)
                {
                    if (firstException == null) firstException = exception;
                }
            }

            LastWriteCount = writes;
            ClearLifecycleOverride();
            if (firstException != null) throw firstException;
        }

        int ApplyPolicy(bool collide, bool useAuthoredIgnores)
        {
            CaptureTransactionSnapshot();
            try
            {
                int writes = 0;
                for (int index = 0; index < pairs.Length; index++)
                {
                    RagdollInternalCollisionPair pair = pairs[index];
                    bool firstDisconnected = disconnectedBones != null
                        && disconnectedBones[pair.FirstBoneIndex];
                    bool secondDisconnected = disconnectedBones != null
                        && disconnectedBones[pair.SecondBoneIndex];
                    bool disconnectedBoundary = firstDisconnected
                        != secondDisconnected;
                    bool differentDisconnectedIslands = firstDisconnected
                        && secondDisconnected
                        && disconnectedCollisionIslands != null
                        && disconnectedCollisionIslands[pair.FirstBoneIndex]
                            != disconnectedCollisionIslands[pair.SecondBoneIndex];
                    bool forceDisconnectedCollision = disconnectedBoundary
                        || differentDisconnectedIslands;
                    bool forced = useAuthoredIgnores
                        && authoredIgnores.IsForcedIgnore(
                            pair.FirstBoneIndex,
                            pair.SecondBoneIndex);
                    // Disconnected physical pieces must collide with the managed body
                    // and with one another, regardless of the automatic/manual baseline.
                    bool ignored = forceDisconnectedCollision
                        ? false
                        : !collide || forced;
                    if (pair.Apply(ignored)) writes++;
                }
                return writes;
            }
            catch (Exception applyException)
            {
                try
                {
                    RestoreSnapshot(
                        transactionSnapshot,
                        transactionSnapshotValid);
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException(
                        "Applying and rolling back an internal-collision policy both failed.",
                        applyException,
                        rollbackException);
                }
                throw;
            }
            finally
            {
                Array.Clear(
                    transactionSnapshotValid,
                    0,
                    transactionSnapshotValid.Length);
            }
        }

        void CaptureTransactionSnapshot()
        {
            Array.Clear(
                transactionSnapshotValid,
                0,
                transactionSnapshotValid.Length);
            for (int index = 0; index < pairs.Length; index++)
            {
                bool ignored;
                if (!pairs[index].TryGetIgnored(out ignored)) continue;
                transactionSnapshot[index] = ignored;
                transactionSnapshotValid[index] = true;
            }
        }

        int RestoreLifecycleSnapshot()
        {
            if (lifecycleSnapshot == null
                || lifecycleSnapshotValid == null)
            {
                return 0;
            }

            return RestoreSnapshot(
                lifecycleSnapshot,
                lifecycleSnapshotValid);
        }

        int RestoreSnapshot(bool[] values, bool[] valid)
        {
            int writes = 0;
            Exception firstException = null;
            for (int index = 0; index < pairs.Length; index++)
            {
                if (!valid[index]) continue;
                try
                {
                    if (pairs[index].Apply(values[index])) writes++;
                }
                catch (Exception exception)
                {
                    if (firstException == null) firstException = exception;
                }
            }

            if (firstException != null) throw firstException;
            return writes;
        }

        void ClearLifecycleOverride()
        {
            lifecycleSnapshot = null;
            lifecycleSnapshotValid = null;
            lifecycleOverrideActive = false;
        }

        int CountForcedColliderPairs()
        {
            int count = 0;
            for (int index = 0; index < pairs.Length; index++)
            {
                RagdollInternalCollisionPair pair = pairs[index];
                if (authoredIgnores.IsForcedIgnore(
                    pair.FirstBoneIndex,
                    pair.SecondBoneIndex))
                {
                    count++;
                }
            }
            return count;
        }

        void ValidatePairs()
        {
            for (int index = 0; index < pairs.Length; index++)
            {
                RagdollInternalCollisionPair pair = pairs[index];
                if (pair == null)
                {
                    throw new ArgumentException(
                        "Internal-collision pair tables cannot contain null records.",
                        nameof(pairs));
                }
                if (pair.FirstBoneIndex >= authoredIgnores.BoneCount
                    || pair.SecondBoneIndex >= authoredIgnores.BoneCount)
                {
                    throw new ArgumentException(
                        "An internal-collision pair references a bone outside the authored matrix.",
                        nameof(pairs));
                }
            }
        }
    }
}
