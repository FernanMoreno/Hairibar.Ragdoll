using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    public partial class RagdollAnimator
    {
        /// <summary>
        /// Atomically replaces the complete active muscle registry while retaining the
        /// authored root. All validation occurs before the registry is changed. Call
        /// from FixedUpdate while the ragdoll is in a stable Alive state.
        /// </summary>
        [RagdollCompatibilityApi("Runtime hierarchy", "https://root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public bool TrySetMuscles(
            IReadOnlyList<RagdollRuntimeMuscleRegistration> collection,
            out RagdollHierarchyTransactionResult result)
        {
            string error;
            if (!ValidateHierarchyMutation(out error))
            {
                result = RagdollHierarchyTransactionResult.Failure(
                    error,
                    Bindings ? Bindings.RegistryGeneration : 0);
                return false;
            }

            List<RagdollRuntimeMuscleRegistration> ordered;
            RagdollBone root;
            if (!TryValidateCompleteCollection(collection, out ordered, out root, out error))
            {
                result = RagdollHierarchyTransactionResult.Failure(
                    error,
                    Bindings.RegistryGeneration);
                return false;
            }

            RagdollDefinitionBindings.RuntimeRegistrySnapshot bindingSnapshot =
                Bindings.CaptureRuntimeRegistry();
            Dictionary<BoneName, RuntimeMuscleData> runtimeSnapshot =
                new Dictionary<BoneName, RuntimeMuscleData>(runtimeMuscles);
            AnimatedPair[] oldPairs = animatedPairs;
            RagdollHierarchySubsystemSnapshot subsystemSnapshot =
                CaptureHierarchySubsystemSnapshot(oldPairs);

            RagdollBone[] removedBones = GetNonRootBonesByDescendingDepth();
            RagdollMuscleChange[] removedChanges =
                CreateRemovedChanges(removedBones, oldPairs);
            RemovedPhysicalSnapshot[] removedPhysical =
                CaptureRemovedSnapshots(removedBones, removedChanges, oldPairs);
            PhysicalAddSnapshot[] addSnapshots =
                new PhysicalAddSnapshot[ordered.Count];
            Rigidbody[] addedBodies = new Rigidbody[ordered.Count];
            for (int index = 0; index < ordered.Count; index++)
            {
                Rigidbody body = ordered[index].Joint.GetComponent<Rigidbody>();
                addedBodies[index] = body;
                addSnapshots[index] = CaptureAddSnapshot(ordered[index], body);
            }

            hierarchyTransactionInProgress = true;
            try
            {
                RemoveAllNonRootBindings(root);
                runtimeMuscles.Clear();

                for (int index = 0; index < ordered.Count; index++)
                {
                    RagdollRuntimeMuscleRegistration registration = ordered[index];
                    RagdollBone parent;
                    if (!Bindings.TryGetBone(registration.Joint.connectedBody, out parent))
                    {
                        throw new InvalidOperationException(
                            "Muscle '" + registration.Bone
                            + "' lost its validated parent during commit.");
                    }

                    ConfigureAddedMuscle(registration, parent, addedBodies[index]);
                    runtimeMuscles.Add(
                        registration.Bone,
                        new RuntimeMuscleData(registration));

                    RagdollBoneHandle ignored;
                    if (!Bindings.TryAddRuntimeBinding(
                        registration.Bone,
                        registration.Joint,
                        out ignored,
                        out error))
                    {
                        throw new InvalidOperationException(error);
                    }
                }

                RebuildRuntimeHierarchy(oldPairs, subsystemSnapshot);
                ReleaseMusclesMissingFromCollection(removedPhysical, collection);

                RagdollMuscleChange[] addedChanges =
                    CreateAddedChangesInRegistryOrder(ordered);
                NotifyHierarchyCommitted(addedChanges, removedChanges);
                result = new RagdollHierarchyTransactionResult(
                    true,
                    null,
                    addedChanges,
                    removedChanges,
                    Bindings.RegistryGeneration);
                return true;
            }
            catch (Exception exception)
            {
                error = "The muscle collection transaction was rolled back: "
                    + exception.Message;
                try
                {
                    ShutdownMuscleConnections();
                    ShutdownInternalCollisions();
                    ShutdownJointRuntime();
                    runtimeMuscles.Clear();
                    foreach (KeyValuePair<BoneName, RuntimeMuscleData> entry
                        in runtimeSnapshot)
                    {
                        runtimeMuscles.Add(entry.Key, entry.Value);
                    }
                    Bindings.RestoreRuntimeRegistry(bindingSnapshot);
                    RestoreRemovedSnapshots(removedPhysical);
                    for (int index = ordered.Count - 1; index >= 0; index--)
                    {
                        RestoreAddSnapshot(
                            ordered[index],
                            addedBodies[index],
                            addSnapshots[index]);
                    }
                    RebuildRuntimeHierarchy(oldPairs, subsystemSnapshot);
                }
                catch (Exception rollbackException)
                {
                    UnityEngine.Debug.LogException(rollbackException, this);
                    error += " Rollback also failed: " + rollbackException.Message;
                }

                result = RagdollHierarchyTransactionResult.Failure(
                    error,
                    Bindings.RegistryGeneration);
                return false;
            }
            finally
            {
                hierarchyTransactionInProgress = false;
            }
        }

        /// <summary>
        /// Applies multiple replacements as one complete-registry transaction. Handles
        /// are checked against the current generation before any state is changed.
        /// </summary>
        [RagdollCompatibilityApi("Runtime hierarchy", "https://root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public bool TryReplaceMuscles(
            IReadOnlyList<RagdollMuscleReplacement> replacements,
            out RagdollHierarchyTransactionResult result)
        {
            if (replacements == null)
            {
                result = RagdollHierarchyTransactionResult.Failure(
                    "Replacement collection cannot be null.",
                    Bindings ? Bindings.RegistryGeneration : 0);
                return false;
            }

            List<RagdollRuntimeMuscleRegistration> desired =
                CaptureActiveCollection();
            Dictionary<BoneName, int> indices =
                new Dictionary<BoneName, int>();
            for (int index = 0; index < desired.Count; index++)
            {
                indices[desired[index].Bone] = index;
            }

            HashSet<BoneName> replaced = new HashSet<BoneName>();
            for (int index = 0; index < replacements.Count; index++)
            {
                RagdollMuscleReplacement replacement = replacements[index];
                if (!Bindings.Topology.Contains(replacement.Existing))
                {
                    result = RagdollHierarchyTransactionResult.Failure(
                        "A replacement handle is stale or belongs to another ragdoll.",
                        Bindings.RegistryGeneration);
                    return false;
                }

                BoneName bone = Bindings.GetBone(replacement.Existing).Name;
                if (!replaced.Add(bone))
                {
                    result = RagdollHierarchyTransactionResult.Failure(
                        "Muscle '" + bone + "' appears more than once.",
                        Bindings.RegistryGeneration);
                    return false;
                }
                if (replacement.Replacement.Bone != bone)
                {
                    result = RagdollHierarchyTransactionResult.Failure(
                        "A replacement must preserve BoneName '" + bone + "'.",
                        Bindings.RegistryGeneration);
                    return false;
                }

                desired[indices[bone]] = replacement.Replacement;
            }

            return TrySetMuscles(desired, out result);
        }

        List<RagdollRuntimeMuscleRegistration> CaptureActiveCollection()
        {
            RagdollMuscleController muscles =
                GetComponent<RagdollMuscleController>();
            List<RagdollRuntimeMuscleRegistration> result =
                new List<RagdollRuntimeMuscleRegistration>(animatedPairs.Length);
            for (int index = 0; index < animatedPairs.Length; index++)
            {
                AnimatedPair pair = animatedPairs[index];
                RagdollMuscleGroup group;
                if (!muscles.TryGetMuscleGroup(pair.Handle, out group))
                {
                    group = pair.RagdollBone.IsRoot
                        ? RagdollMuscleGroup.Hips
                        : RagdollMuscleGroup.Spine;
                }

                RagdollBoneHandle parentHandle;
                Transform targetParent = null;
                bool tree = false;
                if (Bindings.Topology.TryGetParent(pair.Handle, out parentHandle))
                {
                    RagdollBone parent = Bindings.GetBone(parentHandle);
                    targetParent = GetAnimatedPairForName(parent.Name).TargetBone;
                    tree = pair.RagdollBone.Transform.parent == parent.Transform;
                }

                result.Add(new RagdollRuntimeMuscleRegistration(
                    pair.Name,
                    pair.RagdollBone.Joint,
                    pair.TargetBone,
                    group,
                    targetParent,
                    tree,
                    false));
            }
            return result;
        }

        bool TryValidateCompleteCollection(
            IReadOnlyList<RagdollRuntimeMuscleRegistration> collection,
            out List<RagdollRuntimeMuscleRegistration> ordered,
            out RagdollBone root,
            out string error)
        {
            ordered = new List<RagdollRuntimeMuscleRegistration>();
            root = null;
            error = null;
            if (collection == null || collection.Count == 0)
            {
                error = "A complete muscle collection cannot be null or empty.";
                return false;
            }

            for (int index = 0; index < Bindings.BoneCount; index++)
            {
                RagdollBone candidate = Bindings.GetBoneAt(index);
                if (candidate.IsRoot)
                {
                    root = candidate;
                    break;
                }
            }
            if (root == null)
            {
                error = "The active registry has no root muscle.";
                return false;
            }

            Dictionary<BoneName, RagdollRuntimeMuscleRegistration> byName =
                new Dictionary<BoneName, RagdollRuntimeMuscleRegistration>();
            Dictionary<Rigidbody, BoneName> byBody =
                new Dictionary<Rigidbody, BoneName>();
            HashSet<Transform> targets = new HashSet<Transform>();
            RagdollRuntimeMuscleRegistration desiredRoot = default;
            bool foundRoot = false;

            for (int index = 0; index < collection.Count; index++)
            {
                RagdollRuntimeMuscleRegistration entry = collection[index];
                if (!entry.Joint || !entry.Target)
                {
                    error = "Every collection entry requires a live joint and Target.";
                    return false;
                }
                if (!Enum.IsDefined(typeof(RagdollMuscleGroup), entry.Group))
                {
                    error = "Muscle '" + entry.Bone + "' has an unsupported group.";
                    return false;
                }
                Rigidbody body = entry.Joint.GetComponent<Rigidbody>();
                if (!body)
                {
                    error = "Muscle '" + entry.Bone
                        + "' requires a Rigidbody on its joint GameObject.";
                    return false;
                }
                if (!byName.TryAdd(entry.Bone, entry))
                {
                    error = "Duplicate BoneName '" + entry.Bone + "'.";
                    return false;
                }
                if (!byBody.TryAdd(body, entry.Bone))
                {
                    error = "Two muscles reference the same Rigidbody.";
                    return false;
                }
                if (!targets.Add(entry.Target))
                {
                    error = "Two muscles reference the same Target Transform.";
                    return false;
                }
                if (entry.Target == Bindings.transform
                    || entry.Target.IsChildOf(Bindings.transform))
                {
                    error = "Targets must live outside the Puppet hierarchy.";
                    return false;
                }
                if (entry.Bone == root.Name)
                {
                    desiredRoot = entry;
                    foundRoot = true;
                }
            }

            AnimatedPair rootPair = GetAnimatedPairForName(root.Name);
            if (!foundRoot
                || desiredRoot.Joint != root.Joint
                || desiredRoot.Target != rootPair.TargetBone)
            {
                error = "The authored root joint and Target must be retained exactly.";
                return false;
            }

            RagdollPropMuscle[] propSlots =
                GetComponentsInChildren<RagdollPropMuscle>(true);
            for (int index = 0; index < propSlots.Length; index++)
            {
                RagdollPropMuscle slot = propSlots[index];
                if (!slot
                    || slot.Animator != this
                    || (!slot.IsHoldingProp && !slot.IsTransitioning))
                {
                    continue;
                }
                RagdollBone currentPropBone;
                if (!Bindings.TryGetBone(slot.Joint, out currentPropBone))
                {
                    error = "An active prop slot is not present in the current registry.";
                    return false;
                }
                RagdollRuntimeMuscleRegistration desired;
                if (!byName.TryGetValue(currentPropBone.Name, out desired)
                    || desired.Joint != slot.Joint
                    || desired.Target != slot.TargetSlot)
                {
                    error = "A held prop muscle must remain in the collection with the same joint and Target.";
                    return false;
                }
            }

            HashSet<Rigidbody> resolved = new HashSet<Rigidbody>
            {
                root.Joint.GetComponent<Rigidbody>()
            };
            HashSet<BoneName> pending = new HashSet<BoneName>(byName.Keys);
            pending.Remove(root.Name);
            while (pending.Count > 0)
            {
                bool progressed = false;
                for (int index = 0; index < collection.Count; index++)
                {
                    RagdollRuntimeMuscleRegistration entry = collection[index];
                    if (!pending.Contains(entry.Bone)) continue;
                    if (!entry.Joint.connectedBody
                        || !resolved.Contains(entry.Joint.connectedBody))
                    {
                        continue;
                    }
                    ordered.Add(entry);
                    resolved.Add(entry.Joint.GetComponent<Rigidbody>());
                    pending.Remove(entry.Bone);
                    progressed = true;
                }
                if (!progressed)
                {
                    error = "The collection contains an orphan, cycle or parent outside the collection.";
                    return false;
                }
            }
            return true;
        }

        RagdollBone[] GetNonRootBonesByDescendingDepth()
        {
            List<RagdollBone> bones = new List<RagdollBone>();
            for (int index = 0; index < Bindings.BoneCount; index++)
            {
                RagdollBone bone = Bindings.GetBoneAt(index);
                if (!bone.IsRoot) bones.Add(bone);
            }
            bones.Sort((left, right) =>
                GetRequiredHandleDepth(right.Name)
                    .CompareTo(GetRequiredHandleDepth(left.Name)));
            return bones.ToArray();
        }

        int GetRequiredHandleDepth(BoneName bone)
        {
            RagdollBoneHandle handle;
            if (!Bindings.TryGetBoneHandle(bone, out handle))
            {
                throw new InvalidOperationException(
                    "No active handle exists for muscle '" + bone + "'.");
            }
            return Bindings.Topology.GetDepth(handle);
        }

        void RemoveAllNonRootBindings(RagdollBone root)
        {
            RagdollBoneHandle rootHandle;
            if (!Bindings.TryGetBoneHandle(root.Name, out rootHandle))
            {
                throw new InvalidOperationException(
                    "The active root has no generation-safe handle.");
            }
            List<BoneName> branchRoots = new List<BoneName>();
            int childCount = Bindings.Topology.GetChildCount(rootHandle);
            for (int index = 0; index < childCount; index++)
            {
                RagdollBoneHandle child =
                    Bindings.Topology.GetChild(rootHandle, index);
                branchRoots.Add(Bindings.GetBone(child).Name);
            }

            for (int index = 0; index < branchRoots.Count; index++)
            {
                RagdollBone[] ignored;
                string error;
                if (!Bindings.TryRemoveRuntimeSubtree(
                    branchRoots[index], out ignored, out error))
                {
                    throw new InvalidOperationException(error);
                }
            }
        }

        RagdollMuscleChange[] CreateAddedChangesInRegistryOrder(
            List<RagdollRuntimeMuscleRegistration> ordered)
        {
            RagdollMuscleChange[] changes =
                new RagdollMuscleChange[ordered.Count];
            for (int index = 0; index < ordered.Count; index++)
            {
                RagdollRuntimeMuscleRegistration entry = ordered[index];
                changes[index] = new RagdollMuscleChange(
                    entry.Bone,
                    entry.Joint,
                    entry.Target,
                    GetRequiredHandle(entry.Bone),
                    true);
            }
            return changes;
        }

        RagdollBoneHandle GetRequiredHandle(BoneName bone)
        {
            RagdollBoneHandle handle;
            if (!Bindings.TryGetBoneHandle(bone, out handle))
            {
                throw new InvalidOperationException(
                    "No active handle exists for muscle '" + bone + "'.");
            }
            return handle;
        }

        static void ReleaseMusclesMissingFromCollection(
            RemovedPhysicalSnapshot[] removed,
            IReadOnlyList<RagdollRuntimeMuscleRegistration> collection)
        {
            HashSet<ConfigurableJoint> retained = new HashSet<ConfigurableJoint>();
            for (int index = 0; index < collection.Count; index++)
            {
                retained.Add(collection[index].Joint);
            }
            for (int index = 0; index < removed.Length; index++)
            {
                if (retained.Contains(removed[index].Joint)) continue;
                ReleaseRemovedMuscles(
                    removed[index].Bone,
                    new[] { removed[index] },
                    false,
                    false,
                    RagdollMuscleRemoveMode.Sever);
            }
        }
    }
}
