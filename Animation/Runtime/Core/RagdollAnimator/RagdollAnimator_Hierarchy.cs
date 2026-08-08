using System;
using System.Collections.Generic;
using UnityEngine;


namespace Hairibar.Ragdoll.Animation
{
    public partial class RagdollAnimator
    {
        sealed class RuntimeMuscleData
        {
            internal readonly RagdollRuntimeMuscleRegistration Registration;
            internal readonly RagdollTargetBinding TargetBinding;

            internal RuntimeMuscleData(RagdollRuntimeMuscleRegistration registration)
            {
                Registration = registration;
                TargetBinding = new RagdollTargetBinding(
                    registration.Bone,
                    registration.Target,
                    registration.Joint.transform);
            }
        }

        struct PhysicalAddSnapshot
        {
            internal Transform JointParent;
            internal int JointSiblingIndex;
            internal Vector3 JointLocalPosition;
            internal Quaternion JointLocalRotation;
            internal Vector3 JointLocalScale;
            internal Transform TargetParent;
            internal int TargetSiblingIndex;
            internal Vector3 TargetLocalPosition;
            internal Quaternion TargetLocalRotation;
            internal Vector3 TargetLocalScale;
            internal int JointLayer;
            internal int TargetLayer;
            internal Rigidbody ConnectedBody;
            internal ConfigurableJointMotion XMotion;
            internal ConfigurableJointMotion YMotion;
            internal ConfigurableJointMotion ZMotion;
            internal ConfigurableJointMotion AngularXMotion;
            internal ConfigurableJointMotion AngularYMotion;
            internal ConfigurableJointMotion AngularZMotion;
            internal Vector3 ConnectedAnchor;
            internal bool AutoConfigureConnectedAnchor;
            internal JointDrive SlerpDrive;
            internal Vector3 TargetAngularVelocity;
            internal Vector3 Velocity;
            internal Vector3 AngularVelocity;
            internal bool WasSleeping;
        }

        struct RemovedPhysicalSnapshot
        {
            internal BoneName Bone;
            internal Transform MuscleTransform;
            internal Transform MuscleParent;
            internal int MuscleSiblingIndex;
            internal Vector3 MuscleLocalPosition;
            internal Quaternion MuscleLocalRotation;
            internal Vector3 MuscleLocalScale;
            internal bool MuscleActiveSelf;
            internal ConfigurableJoint Joint;
            internal Rigidbody Rigidbody;
            internal bool IsKinematic;
            internal bool DetectCollisions;
            internal Vector3 Velocity;
            internal Vector3 AngularVelocity;
            internal bool WasSleeping;
            internal Rigidbody ConnectedBody;
            internal ConfigurableJointMotion XMotion;
            internal ConfigurableJointMotion YMotion;
            internal ConfigurableJointMotion ZMotion;
            internal ConfigurableJointMotion AngularXMotion;
            internal ConfigurableJointMotion AngularYMotion;
            internal ConfigurableJointMotion AngularZMotion;
            internal Vector3 ConnectedAnchor;
            internal bool AutoConfigureConnectedAnchor;
            internal JointDrive SlerpDrive;
            internal Vector3 TargetAngularVelocity;
            internal Transform Target;
            internal Transform TargetParent;
            internal int TargetSiblingIndex;
            internal Vector3 TargetLocalPosition;
            internal Quaternion TargetLocalRotation;
            internal Vector3 TargetLocalScale;
            internal Vector3 TargetWorldPosition;
            internal Quaternion TargetWorldRotation;
        }

        readonly Dictionary<BoneName, RuntimeMuscleData> runtimeMuscles =
            new Dictionary<BoneName, RuntimeMuscleData>();

        bool hierarchyTransactionInProgress;

        public bool IsHierarchyTransactionInProgress =>
            hierarchyTransactionInProgress;
        public int RuntimeAddedMuscleCount => runtimeMuscles.Count;

        public event Action<RagdollMuscleChange> MuscleAdded;
        public event Action<RagdollMuscleChange> MuscleRemoved;
        public event Action HierarchyChanged;

        /// <summary>
        /// Adds a muscle to the initialized runtime registry. Call from FixedUpdate.
        /// The joint and target must already represent the desired bind pose.
        /// </summary>
        public RagdollBoneHandle AddMuscle(
            BoneName bone,
            ConfigurableJoint joint,
            Transform target,
            RagdollMuscleGroup group,
            Transform targetParent = null,
            bool forceTreeHierarchy = false,
            bool forceLayers = true)
        {
            return AddMuscle(
                new RagdollRuntimeMuscleRegistration(
                    bone,
                    joint,
                    target,
                    group,
                    targetParent,
                    forceTreeHierarchy,
                    forceLayers));
        }

        public RagdollBoneHandle AddMuscle(
            RagdollRuntimeMuscleRegistration registration)
        {
            RagdollBoneHandle handle;
            string error;
            if (!TryAddMuscle(registration, out handle, out error))
            {
                throw new InvalidOperationException(error);
            }
            return handle;
        }

        /// <summary>
        /// Non-throwing variant of AddMuscle.
        /// </summary>
        public bool TryAddMuscle(
            RagdollRuntimeMuscleRegistration registration,
            out RagdollBoneHandle handle,
            out string error)
        {
            handle = RagdollBoneHandle.Invalid;
            error = null;

            if (!ValidateHierarchyMutation(out error)) return false;
            if (string.IsNullOrWhiteSpace(registration.Bone.ToString()))
            {
                error = "A runtime muscle requires a non-empty BoneName.";
                return false;
            }
            if (!Enum.IsDefined(
                typeof(RagdollMuscleGroup),
                registration.Group))
            {
                error = "The runtime muscle has an unsupported semantic group.";
                return false;
            }
            if (!registration.Joint)
            {
                error = "A runtime muscle requires a ConfigurableJoint.";
                return false;
            }
            if (!registration.Joint.gameObject.activeInHierarchy)
            {
                error = "A runtime muscle must be active in the hierarchy when it is added.";
                return false;
            }
            if (!registration.Target)
            {
                error = "A runtime muscle requires a Target Transform.";
                return false;
            }
            if (registration.Target == Bindings.transform
                || registration.Target.IsChildOf(Bindings.transform))
            {
                error = "The runtime Target must live outside the Puppet hierarchy.";
                return false;
            }
            if (registration.TargetParent
                && (registration.TargetParent == Bindings.transform
                    || registration.TargetParent.IsChildOf(
                        Bindings.transform)))
            {
                error = "The runtime Target parent must live outside the Puppet hierarchy.";
                return false;
            }
            RagdollBone existingBone;
            if (Bindings.TryGetBone(registration.Bone, out existingBone))
            {
                error = "A registered ragdoll bone named '"
                    + registration.Bone + "' already exists.";
                return false;
            }
            if (Bindings.TryGetBone(registration.Joint, out existingBone))
            {
                error = "The supplied ConfigurableJoint is already registered.";
                return false;
            }
            for (int pairIndex = 0;
                pairIndex < animatedPairs.Length;
                pairIndex++)
            {
                if (animatedPairs[pairIndex].TargetBone == registration.Target)
                {
                    error = "The supplied Target Transform is already bound to '"
                        + animatedPairs[pairIndex].Name + "'.";
                    return false;
                }
            }

            Rigidbody body = registration.Joint.GetComponent<Rigidbody>();
            if (!body)
            {
                error = "The runtime muscle joint requires a Rigidbody on the same GameObject.";
                return false;
            }
            if (Bindings.TryGetBone(body, out existingBone))
            {
                error = "The runtime muscle Rigidbody is already registered.";
                return false;
            }
            if (registration.Joint.connectedBody == null)
            {
                error = "The initialized ragdoll already has a root. A runtime muscle must connect to a registered Rigidbody.";
                return false;
            }
            RagdollBone parentBone;
            if (!Bindings.TryGetBone(registration.Joint.connectedBody, out parentBone))
            {
                error = "The runtime muscle must connect to a Rigidbody in the current registry.";
                return false;
            }
            if (runtimeMuscles.ContainsKey(registration.Bone))
            {
                error = "A runtime muscle named '" + registration.Bone + "' is already registered.";
                return false;
            }

            RagdollDefinitionBindings.RuntimeRegistrySnapshot bindingSnapshot =
                Bindings.CaptureRuntimeRegistry();
            AnimatedPair[] oldPairs = animatedPairs;
            RagdollHierarchySubsystemSnapshot subsystemSnapshot =
                CaptureHierarchySubsystemSnapshot(oldPairs);
            PhysicalAddSnapshot physicalSnapshot = CaptureAddSnapshot(
                registration,
                body);

            hierarchyTransactionInProgress = true;
            try
            {
                ConfigureAddedMuscle(registration, parentBone, body);
                runtimeMuscles.Add(
                    registration.Bone,
                    new RuntimeMuscleData(registration));

                if (!Bindings.TryAddRuntimeBinding(
                    registration.Bone,
                    registration.Joint,
                    out handle,
                    out error))
                {
                    runtimeMuscles.Remove(registration.Bone);
                    RestoreAddSnapshot(registration, body, physicalSnapshot);
                    return false;
                }

                RebuildRuntimeHierarchy(oldPairs, subsystemSnapshot);
                if (!Bindings.TryGetBoneHandle(registration.Bone, out handle))
                {
                    throw new InvalidOperationException(
                        "The added muscle was not present after the hierarchy rebuild.");
                }

                RagdollMuscleChange change = new RagdollMuscleChange(
                    registration.Bone,
                    registration.Joint,
                    registration.Target,
                    handle,
                    true);
                NotifyHierarchyCommitted(
                    new[] { change },
                    new RagdollMuscleChange[0]);
                return true;
            }
            catch (Exception exception)
            {
                error = "The runtime muscle transaction was rolled back: "
                    + exception.Message;
                try
                {
                    ShutdownInternalCollisions();
                    ShutdownJointRuntime();
                    runtimeMuscles.Remove(registration.Bone);
                    Bindings.RestoreRuntimeRegistry(bindingSnapshot);
                    RestoreAddSnapshot(registration, body, physicalSnapshot);
                    RebuildRuntimeHierarchy(oldPairs, subsystemSnapshot);
                }
                catch (Exception rollbackException)
                {
                    UnityEngine.Debug.LogException(rollbackException, this);
                    error += " Rollback also failed: " + rollbackException.Message;
                }
                handle = RagdollBoneHandle.Invalid;
                return false;
            }
            finally
            {
                hierarchyTransactionInProgress = false;
            }
        }

        sealed class MuscleRemovalTransaction
        {
            internal BoneName RootName;
            internal bool AttachTargets;
            internal bool BlockTargetAnimation;
            internal RagdollMuscleRemoveMode RemoveMode;
            internal RagdollDefinitionBindings.RuntimeRegistrySnapshot BindingSnapshot;
            internal AnimatedPair[] OldPairs;
            internal RagdollHierarchySubsystemSnapshot SubsystemSnapshot;
            internal Dictionary<BoneName, RuntimeMuscleData> RuntimeSnapshot;
            internal RemovedPhysicalSnapshot[] PhysicalSnapshots =
                new RemovedPhysicalSnapshot[0];
            internal RagdollMuscleChange[] RemovedChanges =
                new RagdollMuscleChange[0];
            internal bool RegistryMutated;
        }

        /// <summary>
        /// Atomically replaces one muscle while preserving its BoneName slot. Direct
        /// children are reconnected when the replacement is a branch root. The
        /// old generation handle becomes invalid after commit. Call from FixedUpdate.
        /// </summary>
        public RagdollBoneHandle ReplaceMuscle(
            RagdollBoneHandle existing,
            RagdollRuntimeMuscleRegistration replacement)
        {
            RagdollBoneHandle handle;
            string error;
            if (!TryReplaceMuscle(existing, replacement, out handle, out error))
            {
                throw new InvalidOperationException(error);
            }
            return handle;
        }

        public bool TryReplaceMuscle(
            RagdollBoneHandle existing,
            RagdollRuntimeMuscleRegistration replacement,
            out RagdollBoneHandle replacementHandle,
            out string error)
        {
            replacementHandle = RagdollBoneHandle.Invalid;
            error = null;
            // Replacement preserves the current connected/disconnected state.
            // This is required for atomic root/branch replacement while props or
            // severed descendants remain owned by the same puppet.
            if (!ValidateHierarchyMutation(true, out error)) return false;
            if (!Bindings.Topology.Contains(existing))
            {
                error = "The replacement handle is stale or belongs to another ragdoll.";
                return false;
            }

            RagdollBone oldBone = Bindings.GetBone(existing);
            bool hasDescendants = false;
            for (int index = 0; index < Bindings.BoneCount; index++)
            {
                RagdollBoneHandle candidate = Bindings.GetHandleAt(index);
                if (candidate != existing
                    && Bindings.Topology.IsAncestorOf(existing, candidate))
                {
                    hasDescendants = true;
                    break;
                }
            }
            if (replacement.Bone != oldBone.Name)
            {
                error = "A replacement must preserve the existing BoneName.";
                return false;
            }
            if (!replacement.Joint || !replacement.Target)
            {
                error = "A replacement requires a live ConfigurableJoint and Target.";
                return false;
            }
            if (!Enum.IsDefined(typeof(RagdollMuscleGroup), replacement.Group))
            {
                error = "The replacement has an unsupported semantic group.";
                return false;
            }
            RagdollBone registered;
            if (Bindings.TryGetBone(replacement.Joint, out registered))
            {
                error = "The replacement joint is already registered.";
                return false;
            }
            Rigidbody newBody = replacement.Joint.GetComponent<Rigidbody>();
            if (!newBody || Bindings.TryGetBone(newBody, out registered))
            {
                error = "The replacement requires an unregistered Rigidbody on its joint.";
                return false;
            }

            for (int index = 0; index < animatedPairs.Length; index++)
            {
                if (animatedPairs[index].TargetBone == replacement.Target
                    && animatedPairs[index].Handle != existing)
                {
                    error = "The replacement Target is already assigned to another muscle.";
                    return false;
                }
            }

            if (oldBone.IsRoot)
            {
                return TryReplaceRootMuscle(
                    existing,
                    oldBone,
                    replacement,
                    newBody,
                    out replacementHandle,
                    out error);
            }

            if (hasDescendants)
            {
                return TryReplaceBranchRoot(
                    existing,
                    oldBone,
                    replacement,
                    newBody,
                    out replacementHandle,
                    out error);
            }
            RagdollBoneHandle parentHandle;
            if (!Bindings.Topology.TryGetParent(existing, out parentHandle))
            {
                error = "The replacement muscle has no registered parent.";
                return false;
            }
            RagdollBone parentBone = Bindings.GetBone(parentHandle);
            RagdollDefinitionBindings.RuntimeRegistrySnapshot bindingSnapshot =
                Bindings.CaptureRuntimeRegistry();
            Dictionary<BoneName, RuntimeMuscleData> runtimeSnapshot =
                new Dictionary<BoneName, RuntimeMuscleData>(runtimeMuscles);
            AnimatedPair[] oldPairs = animatedPairs;
            RagdollHierarchySubsystemSnapshot subsystemSnapshot =
                CaptureHierarchySubsystemSnapshot(oldPairs);
            PhysicalAddSnapshot addSnapshot = CaptureAddSnapshot(replacement, newBody);
            RagdollMuscleChange[] removed = CreateRemovedChanges(
                new[] { oldBone },
                oldPairs);
            RemovedPhysicalSnapshot[] removedPhysical = CaptureRemovedSnapshots(
                new[] { oldBone },
                removed,
                oldPairs);

            hierarchyTransactionInProgress = true;
            try
            {
                ConfigureAddedMuscle(replacement, parentBone, newBody);
                RagdollBone[] ignored;
                if (!Bindings.TryRemoveRuntimeSubtree(
                    oldBone.Name,
                    out ignored,
                    out error))
                {
                    throw new InvalidOperationException(error);
                }
                runtimeMuscles.Remove(oldBone.Name);
                runtimeMuscles.Add(
                    replacement.Bone,
                    new RuntimeMuscleData(replacement));
                if (!Bindings.TryAddRuntimeBinding(
                    replacement.Bone,
                    replacement.Joint,
                    out replacementHandle,
                    out error))
                {
                    throw new InvalidOperationException(error);
                }

                RebuildRuntimeHierarchy(oldPairs, subsystemSnapshot);
                ReleaseRemovedMuscles(
                    oldBone.Name,
                    removedPhysical,
                    false,
                    false,
                    RagdollMuscleRemoveMode.Sever);
                RagdollMuscleChange added = new RagdollMuscleChange(
                    replacement.Bone,
                    replacement.Joint,
                    replacement.Target,
                    replacementHandle,
                    true);
                NotifyHierarchyCommitted(new[] { added }, removed);
                return true;
            }
            catch (Exception exception)
            {
                error = "The muscle replacement was rolled back: " + exception.Message;
                replacementHandle = RagdollBoneHandle.Invalid;
                try
                {
                    ShutdownMuscleConnections();
                    ShutdownInternalCollisions();
                    ShutdownJointRuntime();
                    runtimeMuscles.Clear();
                    foreach (KeyValuePair<BoneName, RuntimeMuscleData> pair
                        in runtimeSnapshot)
                    {
                        runtimeMuscles.Add(pair.Key, pair.Value);
                    }
                    Bindings.RestoreRuntimeRegistry(bindingSnapshot);
                    RestoreRemovedSnapshots(removedPhysical);
                    RestoreAddSnapshot(replacement, newBody, addSnapshot);
                    RebuildRuntimeHierarchy(oldPairs, subsystemSnapshot);
                }
                catch (Exception rollbackException)
                {
                    UnityEngine.Debug.LogException(rollbackException, this);
                    error += " Rollback also failed: " + rollbackException.Message;
                }
                return false;
            }
            finally
            {
                hierarchyTransactionInProgress = false;
            }
        }

        bool TryReplaceRootMuscle(
            RagdollBoneHandle existing,
            RagdollBone oldBone,
            RagdollRuntimeMuscleRegistration replacement,
            Rigidbody newBody,
            out RagdollBoneHandle replacementHandle,
            out string error)
        {
            replacementHandle = RagdollBoneHandle.Invalid;
            error = null;
            AnimatedPair oldRootPair = GetAnimatedPair(existing);
            if (replacement.Target != oldRootPair.TargetBone)
            {
                error = "Root replacement must retain the existing Target Transform.";
                return false;
            }
            if (replacement.Joint.connectedBody)
            {
                error = "A replacement root joint cannot have a connectedBody.";
                return false;
            }

            int childCount = Bindings.Topology.GetChildCount(existing);
            ConfigurableJoint[] children = new ConfigurableJoint[childCount];
            Rigidbody[] previousConnections = new Rigidbody[childCount];
            bool[] restoreChildConnection = new bool[childCount];
            for (int index = 0; index < childCount; index++)
            {
                RagdollBoneHandle childHandle =
                    Bindings.Topology.GetChild(existing, index);
                ConfigurableJoint child = Bindings.GetBone(childHandle).Joint;
                children[index] = child;
                previousConnections[index] = child ? child.connectedBody : null;
                restoreChildConnection[index] = child
                    && (child.connectedBody != oldBone.Rigidbody
                        || (connectionRecords != null
                            && GetMuscleConnectionState(childHandle)
                                != RagdollMuscleConnectionState.Connected));
            }

            RagdollDefinitionBindings.RuntimeRegistrySnapshot bindingSnapshot =
                Bindings.CaptureRuntimeRegistry();
            Dictionary<BoneName, RuntimeMuscleData> runtimeSnapshot =
                new Dictionary<BoneName, RuntimeMuscleData>(runtimeMuscles);
            AnimatedPair[] oldPairs = animatedPairs;
            RagdollHierarchySubsystemSnapshot subsystemSnapshot =
                CaptureHierarchySubsystemSnapshot(oldPairs);
            PhysicalAddSnapshot addSnapshot =
                CaptureAddSnapshot(replacement, newBody);
            RagdollMuscleChange[] removed = CreateRemovedChanges(
                new[] { oldBone }, oldPairs);
            RemovedPhysicalSnapshot[] removedPhysical =
                CaptureRemovedSnapshots(new[] { oldBone }, removed, oldPairs);

            hierarchyTransactionInProgress = true;
            try
            {
                for (int index = 0; index < children.Length; index++)
                {
                    if (children[index]) children[index].connectedBody = newBody;
                }
                if (replacement.ForceLayers)
                {
                    replacement.Joint.gameObject.layer = Bindings.gameObject.layer;
                    replacement.Target.gameObject.layer = gameObject.layer;
                }
                if (!newBody.isKinematic)
                {
                    newBody.linearVelocity = oldBone.Rigidbody.linearVelocity;
                    newBody.angularVelocity = oldBone.Rigidbody.angularVelocity;
                }

                runtimeMuscles.Remove(oldBone.Name);
                runtimeMuscles.Add(
                    replacement.Bone,
                    new RuntimeMuscleData(replacement));
                if (!Bindings.TryReplaceRootBinding(
                    replacement.Bone,
                    replacement.Joint,
                    out replacementHandle,
                    out error))
                {
                    throw new InvalidOperationException(error);
                }

                RebuildRuntimeHierarchy(oldPairs, subsystemSnapshot);
                for (int index = 0; index < children.Length; index++)
                {
                    if (restoreChildConnection[index]
                        && children[index])
                    {
                        children[index].connectedBody =
                            previousConnections[index];
                    }
                }
                ReleaseRemovedMuscles(
                    oldBone.Name,
                    removedPhysical,
                    false,
                    false,
                    RagdollMuscleRemoveMode.Sever);
                RagdollMuscleChange added = new RagdollMuscleChange(
                    replacement.Bone,
                    replacement.Joint,
                    replacement.Target,
                    replacementHandle,
                    true);
                NotifyHierarchyCommitted(new[] { added }, removed);
                return true;
            }
            catch (Exception exception)
            {
                error = "The root replacement was rolled back: " + exception.Message;
                replacementHandle = RagdollBoneHandle.Invalid;
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
                    RestoreAddSnapshot(replacement, newBody, addSnapshot);
                    for (int index = 0; index < children.Length; index++)
                    {
                        if (children[index])
                            children[index].connectedBody = previousConnections[index];
                    }
                    RebuildRuntimeHierarchy(oldPairs, subsystemSnapshot);
                }
                catch (Exception rollbackException)
                {
                    UnityEngine.Debug.LogException(rollbackException, this);
                    error += " Rollback also failed: " + rollbackException.Message;
                }
                return false;
            }
            finally
            {
                hierarchyTransactionInProgress = false;
            }
        }

        bool TryReplaceBranchRoot(
            RagdollBoneHandle existing,
            RagdollBone oldBone,
            RagdollRuntimeMuscleRegistration replacement,
            Rigidbody newBody,
            out RagdollBoneHandle replacementHandle,
            out string error)
        {
            replacementHandle = RagdollBoneHandle.Invalid;
            error = null;
            Rigidbody oldBody = oldBone.Rigidbody;
            int childCount = Bindings.Topology.GetChildCount(existing);
            ConfigurableJoint[] children = new ConfigurableJoint[childCount];
            Rigidbody[] previousConnections = new Rigidbody[childCount];
            int reconnectCount = 0;
            bool committed = false;
            for (int index = 0; index < childCount; index++)
            {
                RagdollBoneHandle childHandle =
                    Bindings.Topology.GetChild(existing, index);
                ConfigurableJoint child = Bindings.GetBone(childHandle).Joint;
                if (!child || child.connectedBody != oldBody) continue;
                children[reconnectCount] = child;
                previousConnections[reconnectCount] = child.connectedBody;
                reconnectCount++;
            }

            try
            {
                for (int index = 0; index < reconnectCount; index++)
                    children[index].connectedBody = newBody;

                RagdollHierarchyTransactionResult transaction;
                if (!TryReplaceMuscles(
                    new[] { new RagdollMuscleReplacement(existing, replacement) },
                    out transaction))
                {
                    error = transaction.Error;
                    return false;
                }
                committed = true;
                if (transaction.Added.Count == 1)
                {
                    replacementHandle = transaction.Added[0].Handle;
                }
                else if (!Bindings.TryGetBoneHandle(
                    replacement.Bone,
                    out replacementHandle))
                    throw new InvalidOperationException(
                        "A committed branch replacement produced no live handle.");
                return true;
            }
            finally
            {
                if (!committed)
                {
                    for (int index = 0; index < reconnectCount; index++)
                    {
                        if (children[index])
                            children[index].connectedBody = previousConnections[index];
                    }
                }
            }
        }

        /// <summary>
        /// Removes a registered muscle and every descendant from runtime management.
        /// Sever releases the branch root, Explode releases every joint and Numb keeps
        /// the physical connections while disabling their drives.
        /// </summary>
        public RagdollMuscleChange[] RemoveMuscleRecursive(
            ConfigurableJoint joint,
            bool attachTargets = false,
            bool blockTargetAnimation = false,
            RagdollMuscleRemoveMode removeMode = RagdollMuscleRemoveMode.Sever)
        {
            RagdollMuscleChange[] removed;
            string error;
            if (!TryRemoveMuscleRecursive(
                joint,
                attachTargets,
                blockTargetAnimation,
                removeMode,
                out removed,
                out error))
            {
                throw new InvalidOperationException(error);
            }
            return removed;
        }

        public bool TryRemoveMuscleRecursive(
            ConfigurableJoint joint,
            bool attachTargets,
            out RagdollMuscleChange[] removedChanges,
            out string error)
        {
            return TryRemoveMuscleRecursive(
                joint,
                attachTargets,
                false,
                RagdollMuscleRemoveMode.Sever,
                out removedChanges,
                out error);
        }

        public bool TryRemoveMuscleRecursive(
            ConfigurableJoint joint,
            bool attachTargets,
            bool blockTargetAnimation,
            RagdollMuscleRemoveMode removeMode,
            out RagdollMuscleChange[] removedChanges,
            out string error)
        {
            removedChanges = new RagdollMuscleChange[0];
            error = null;
            if (!joint)
            {
                error = "RemoveMuscleRecursive requires a live ConfigurableJoint.";
                return false;
            }
            RagdollBone requested;
            if (!Bindings.TryGetBone(joint, out requested))
            {
                error = "No registered muscle uses the supplied ConfigurableJoint.";
                return false;
            }
            return TryRemoveMuscleRecursiveByBone(
                requested.Name,
                attachTargets,
                blockTargetAnimation,
                removeMode,
                false,
                out removedChanges,
                out error);
        }

        internal bool TryRemoveMuscleRecursiveByBone(
            BoneName rootName,
            bool attachTargets,
            bool blockTargetAnimation,
            RagdollMuscleRemoveMode removeMode,
            bool irreversibleJointBreak,
            out RagdollMuscleChange[] removedChanges,
            out string error)
        {
            removedChanges = new RagdollMuscleChange[0];
            error = null;
            if (!ValidateHierarchyMutation(
                irreversibleJointBreak,
                out error))
            {
                return false;
            }
            if (!Enum.IsDefined(typeof(RagdollMuscleRemoveMode), removeMode))
            {
                error = "The muscle removal mode is not supported.";
                return false;
            }

            RagdollBone requested;
            if (!Bindings.TryGetBone(rootName, out requested))
            {
                error = "No registered muscle is named '" + rootName + "'.";
                return false;
            }
            if (requested.IsRoot)
            {
                error = "The root muscle cannot be removed at runtime.";
                return false;
            }
            if (!irreversibleJointBreak && !requested.Joint)
            {
                error = "The requested muscle no longer has a ConfigurableJoint.";
                return false;
            }

            MuscleRemovalTransaction transaction =
                CreateMuscleRemovalTransaction(
                    rootName,
                    attachTargets,
                    blockTargetAnimation,
                    removeMode);

            hierarchyTransactionInProgress = true;
            try
            {
                bool committed = CommitMuscleRemoval(transaction, out error);
                removedChanges = transaction.RemovedChanges;
                return committed;
            }
            catch (Exception exception)
            {
                error = "The runtime muscle removal failed: "
                    + exception.Message;

                if (irreversibleJointBreak && transaction.RegistryMutated)
                {
                    removedChanges = transaction.RemovedChanges;
                    return TryCommitBrokenMuscleRemoval(transaction, ref error);
                }

                RollbackMuscleRemoval(transaction, ref error);
                removedChanges = new RagdollMuscleChange[0];
                return false;
            }
            finally
            {
                hierarchyTransactionInProgress = false;
            }
        }

        MuscleRemovalTransaction CreateMuscleRemovalTransaction(
            BoneName rootName,
            bool attachTargets,
            bool blockTargetAnimation,
            RagdollMuscleRemoveMode removeMode)
        {
            AnimatedPair[] oldPairs = animatedPairs;
            return new MuscleRemovalTransaction
            {
                RootName = rootName,
                AttachTargets = attachTargets,
                BlockTargetAnimation = blockTargetAnimation,
                RemoveMode = removeMode,
                BindingSnapshot = Bindings.CaptureRuntimeRegistry(),
                OldPairs = oldPairs,
                SubsystemSnapshot = CaptureHierarchySubsystemSnapshot(oldPairs),
                RuntimeSnapshot =
                    new Dictionary<BoneName, RuntimeMuscleData>(runtimeMuscles)
            };
        }

        bool CommitMuscleRemoval(
            MuscleRemovalTransaction transaction,
            out string error)
        {
            RagdollBone[] removedBones;
            if (!Bindings.TryRemoveRuntimeSubtree(
                transaction.RootName,
                out removedBones,
                out error))
            {
                return false;
            }
            transaction.RegistryMutated = true;
            transaction.RemovedChanges = CreateRemovedChanges(
                removedBones,
                transaction.OldPairs);
            transaction.PhysicalSnapshots = CaptureRemovedSnapshots(
                removedBones,
                transaction.RemovedChanges,
                transaction.OldPairs);
            for (int index = 0; index < removedBones.Length; index++)
            {
                runtimeMuscles.Remove(removedBones[index].Name);
            }

            RebuildRuntimeHierarchy(
                transaction.OldPairs,
                transaction.SubsystemSnapshot);
            ReleaseRemovedMuscles(
                transaction.RootName,
                transaction.PhysicalSnapshots,
                transaction.AttachTargets,
                transaction.BlockTargetAnimation,
                transaction.RemoveMode);
            NotifyHierarchyCommitted(
                new RagdollMuscleChange[0],
                transaction.RemovedChanges);
            return true;
        }

        bool TryCommitBrokenMuscleRemoval(
            MuscleRemovalTransaction transaction,
            ref string error)
        {
            // A Unity joint break has already destroyed the connection. Restoring
            // the old registry would reintroduce a bone with a missing joint.
            try
            {
                RebuildRuntimeHierarchy(
                    transaction.OldPairs,
                    transaction.SubsystemSnapshot);
                ReleaseRemovedMuscles(
                    transaction.RootName,
                    transaction.PhysicalSnapshots,
                    transaction.AttachTargets,
                    transaction.BlockTargetAnimation,
                    transaction.RemoveMode);
                NotifyHierarchyCommitted(
                    new RagdollMuscleChange[0],
                    transaction.RemovedChanges);
                error = null;
                return true;
            }
            catch (Exception degradedException)
            {
                UnityEngine.Debug.LogException(degradedException, this);
                enabled = false;
                error += " The broken branch could not be rebuilt and RagdollAnimator was disabled: "
                    + degradedException.Message;
                return false;
            }
        }

        void RollbackMuscleRemoval(
            MuscleRemovalTransaction transaction,
            ref string error)
        {
            try
            {
                ShutdownMuscleConnections();
                ShutdownInternalCollisions();
                ShutdownJointRuntime();
                runtimeMuscles.Clear();
                foreach (KeyValuePair<BoneName, RuntimeMuscleData> pair
                    in transaction.RuntimeSnapshot)
                {
                    runtimeMuscles.Add(pair.Key, pair.Value);
                }
                Bindings.RestoreRuntimeRegistry(transaction.BindingSnapshot);
                RestoreRemovedSnapshots(transaction.PhysicalSnapshots);
                RebuildRuntimeHierarchy(
                    transaction.OldPairs,
                    transaction.SubsystemSnapshot);
            }
            catch (Exception rollbackException)
            {
                UnityEngine.Debug.LogException(rollbackException, this);
                error += " Rollback also failed: " + rollbackException.Message;
            }
        }

        bool ValidateHierarchyMutation(out string error)
        {
            return ValidateHierarchyMutation(false, out error);
        }

        bool ValidateHierarchyMutation(
            bool allowExistingConnectionState,
            out string error)
        {
            error = null;
            if (hierarchyTransactionInProgress)
            {
                error = "Another ragdoll hierarchy transaction is already in progress.";
                return false;
            }
            if (animatedPairs == null || !Bindings.IsInitialized)
            {
                error = "RagdollAnimator has not completed initialization.";
                return false;
            }
            if (PendingMuscleConnectionOperationCount > 0
                || (!allowExistingConnectionState && HasDisconnectedMuscles))
            {
                error = "Runtime hierarchy mutations require all muscles connected and no pending connection operations.";
                return false;
            }
            if (Application.isPlaying && !Time.inFixedTimeStep)
            {
                error = "Runtime muscle mutations must be requested from FixedUpdate.";
                return false;
            }
            if (!IsAlive || IsKilling || IsSwitchingState)
            {
                error = "Runtime muscle mutations require a stable Alive lifecycle state.";
                return false;
            }

            RagdollSimulationModeController mode =
                GetComponent<RagdollSimulationModeController>();
            if (mode && (mode.IsTransitioning
                || mode.CurrentMode == RagdollSimulationMode.Disabled))
            {
                error = "Runtime muscle mutations are not supported while simulation mode is Disabled or transitioning.";
                return false;
            }
            return true;
        }

        void ConfigureAddedMuscle(
            RagdollRuntimeMuscleRegistration registration,
            RagdollBone parentBone,
            Rigidbody body)
        {
            ConfigurableJoint joint = registration.Joint;
            Transform targetParent = registration.TargetParent
                ? registration.TargetParent
                : GetAnimatedPairForName(parentBone.Name).TargetBone;

            joint.transform.SetParent(
                registration.ForceTreeHierarchy
                    ? parentBone.Transform
                    : Bindings.transform,
                true);
            registration.Target.SetParent(targetParent, true);
            joint.connectedBody = parentBone.Rigidbody;
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;

            if (registration.ForceLayers)
            {
                joint.gameObject.layer = Bindings.gameObject.layer;
                registration.Target.gameObject.layer = gameObject.layer;
            }

            if (!body.isKinematic)
            {
                body.linearVelocity = parentBone.Rigidbody.linearVelocity;
                body.angularVelocity = parentBone.Rigidbody.angularVelocity;
            }
        }

        PhysicalAddSnapshot CaptureAddSnapshot(
            RagdollRuntimeMuscleRegistration registration,
            Rigidbody body)
        {
            ConfigurableJoint joint = registration.Joint;
            return new PhysicalAddSnapshot
            {
                JointParent = joint.transform.parent,
                JointSiblingIndex = joint.transform.GetSiblingIndex(),
                JointLocalPosition = joint.transform.localPosition,
                JointLocalRotation = joint.transform.localRotation,
                JointLocalScale = joint.transform.localScale,
                TargetParent = registration.Target.parent,
                TargetSiblingIndex = registration.Target.GetSiblingIndex(),
                TargetLocalPosition = registration.Target.localPosition,
                TargetLocalRotation = registration.Target.localRotation,
                TargetLocalScale = registration.Target.localScale,
                JointLayer = joint.gameObject.layer,
                TargetLayer = registration.Target.gameObject.layer,
                ConnectedBody = joint.connectedBody,
                XMotion = joint.xMotion,
                YMotion = joint.yMotion,
                ZMotion = joint.zMotion,
                AngularXMotion = joint.angularXMotion,
                AngularYMotion = joint.angularYMotion,
                AngularZMotion = joint.angularZMotion,
                ConnectedAnchor = joint.connectedAnchor,
                AutoConfigureConnectedAnchor = joint.autoConfigureConnectedAnchor,
                SlerpDrive = joint.slerpDrive,
                TargetAngularVelocity = joint.targetAngularVelocity,
                Velocity = body.linearVelocity,
                AngularVelocity = body.angularVelocity,
                WasSleeping = body.IsSleeping()
            };
        }

        static void RestoreAddSnapshot(
            RagdollRuntimeMuscleRegistration registration,
            Rigidbody body,
            PhysicalAddSnapshot snapshot)
        {
            ConfigurableJoint joint = registration.Joint;
            joint.transform.SetParent(snapshot.JointParent, false);
            joint.transform.localPosition = snapshot.JointLocalPosition;
            joint.transform.localRotation = snapshot.JointLocalRotation;
            joint.transform.localScale = snapshot.JointLocalScale;
            joint.transform.SetSiblingIndex(snapshot.JointSiblingIndex);
            registration.Target.SetParent(snapshot.TargetParent, false);
            registration.Target.localPosition = snapshot.TargetLocalPosition;
            registration.Target.localRotation = snapshot.TargetLocalRotation;
            registration.Target.localScale = snapshot.TargetLocalScale;
            registration.Target.SetSiblingIndex(snapshot.TargetSiblingIndex);
            joint.gameObject.layer = snapshot.JointLayer;
            registration.Target.gameObject.layer = snapshot.TargetLayer;
            joint.connectedBody = snapshot.ConnectedBody;
            joint.xMotion = snapshot.XMotion;
            joint.yMotion = snapshot.YMotion;
            joint.zMotion = snapshot.ZMotion;
            joint.angularXMotion = snapshot.AngularXMotion;
            joint.angularYMotion = snapshot.AngularYMotion;
            joint.angularZMotion = snapshot.AngularZMotion;
            joint.connectedAnchor = snapshot.ConnectedAnchor;
            joint.autoConfigureConnectedAnchor =
                snapshot.AutoConfigureConnectedAnchor;
            joint.slerpDrive = snapshot.SlerpDrive;
            joint.targetAngularVelocity = snapshot.TargetAngularVelocity;
            if (!body.isKinematic)
            {
                body.linearVelocity = snapshot.Velocity;
                body.angularVelocity = snapshot.AngularVelocity;
                if (snapshot.WasSleeping) body.Sleep();
                else body.WakeUp();
            }
        }

        RagdollMuscleChange[] CreateRemovedChanges(
            RagdollBone[] removedBones,
            AnimatedPair[] oldPairs)
        {
            Dictionary<BoneName, Transform> targets =
                new Dictionary<BoneName, Transform>();
            for (int index = 0; index < oldPairs.Length; index++)
            {
                targets[oldPairs[index].Name] = oldPairs[index].TargetBone;
            }

            RagdollMuscleChange[] changes =
                new RagdollMuscleChange[removedBones.Length];
            for (int index = 0; index < removedBones.Length; index++)
            {
                Transform target;
                targets.TryGetValue(removedBones[index].Name, out target);
                changes[index] = new RagdollMuscleChange(
                    removedBones[index].Name,
                    removedBones[index].Joint,
                    target,
                    RagdollBoneHandle.Invalid,
                    false);
            }
            return changes;
        }

        static RemovedPhysicalSnapshot[] CaptureRemovedSnapshots(
            RagdollBone[] removedBones,
            RagdollMuscleChange[] removed,
            AnimatedPair[] oldPairs)
        {
            Dictionary<BoneName, AnimatedPair> pairByName =
                new Dictionary<BoneName, AnimatedPair>();
            for (int index = 0; index < oldPairs.Length; index++)
            {
                pairByName[oldPairs[index].Name] = oldPairs[index];
            }

            RemovedPhysicalSnapshot[] snapshots =
                new RemovedPhysicalSnapshot[removed.Length];
            for (int index = 0; index < removed.Length; index++)
            {
                RagdollBone bone = removedBones[index];
                ConfigurableJoint joint = bone.Joint;
                Rigidbody body = bone.Rigidbody;
                Transform target = removed[index].Target;
                AnimatedPair pair;
                Vector3 targetWorldPosition = target ? target.position : Vector3.zero;
                Quaternion targetWorldRotation = target ? target.rotation : Quaternion.identity;
                if (pairByName.TryGetValue(bone.Name, out pair))
                {
                    pair.GetMappedTargetWorldPose(
                        out targetWorldPosition,
                        out targetWorldRotation);
                }

                snapshots[index] = new RemovedPhysicalSnapshot
                {
                    Bone = bone.Name,
                    MuscleTransform = bone.Transform,
                    MuscleParent = bone.Transform ? bone.Transform.parent : null,
                    MuscleSiblingIndex = bone.Transform ? bone.Transform.GetSiblingIndex() : 0,
                    MuscleLocalPosition = bone.Transform ? bone.Transform.localPosition : Vector3.zero,
                    MuscleLocalRotation = bone.Transform ? bone.Transform.localRotation : Quaternion.identity,
                    MuscleLocalScale = bone.Transform ? bone.Transform.localScale : Vector3.one,
                    MuscleActiveSelf = bone.Transform && bone.Transform.gameObject.activeSelf,
                    Joint = joint,
                    Rigidbody = body,
                    IsKinematic = body && body.isKinematic,
                    DetectCollisions = body && body.detectCollisions,
                    Velocity = body ? body.linearVelocity : Vector3.zero,
                    AngularVelocity = body ? body.angularVelocity : Vector3.zero,
                    WasSleeping = body && body.IsSleeping(),
                    ConnectedBody = joint ? joint.connectedBody : null,
                    XMotion = joint ? joint.xMotion : ConfigurableJointMotion.Free,
                    YMotion = joint ? joint.yMotion : ConfigurableJointMotion.Free,
                    ZMotion = joint ? joint.zMotion : ConfigurableJointMotion.Free,
                    AngularXMotion = joint ? joint.angularXMotion : ConfigurableJointMotion.Free,
                    AngularYMotion = joint ? joint.angularYMotion : ConfigurableJointMotion.Free,
                    AngularZMotion = joint ? joint.angularZMotion : ConfigurableJointMotion.Free,
                    ConnectedAnchor = joint ? joint.connectedAnchor : Vector3.zero,
                    AutoConfigureConnectedAnchor = joint && joint.autoConfigureConnectedAnchor,
                    SlerpDrive = joint ? joint.slerpDrive : new JointDrive(),
                    TargetAngularVelocity = joint ? joint.targetAngularVelocity : Vector3.zero,
                    Target = target,
                    TargetParent = target ? target.parent : null,
                    TargetSiblingIndex = target ? target.GetSiblingIndex() : 0,
                    TargetLocalPosition = target ? target.localPosition : Vector3.zero,
                    TargetLocalRotation = target ? target.localRotation : Quaternion.identity,
                    TargetLocalScale = target ? target.localScale : Vector3.one,
                    TargetWorldPosition = targetWorldPosition,
                    TargetWorldRotation = targetWorldRotation
                };
            }
            return snapshots;
        }

        static void RestoreRemovedSnapshots(
            RemovedPhysicalSnapshot[] snapshots)
        {
            if (snapshots == null) return;
            for (int index = 0; index < snapshots.Length; index++)
            {
                RemovedPhysicalSnapshot snapshot = snapshots[index];
                if (snapshot.MuscleTransform)
                {
                    snapshot.MuscleTransform.SetParent(snapshot.MuscleParent, false);
                    snapshot.MuscleTransform.localPosition = snapshot.MuscleLocalPosition;
                    snapshot.MuscleTransform.localRotation = snapshot.MuscleLocalRotation;
                    snapshot.MuscleTransform.localScale = snapshot.MuscleLocalScale;
                    snapshot.MuscleTransform.SetSiblingIndex(snapshot.MuscleSiblingIndex);
                    snapshot.MuscleTransform.gameObject.SetActive(snapshot.MuscleActiveSelf);
                }
                if (snapshot.Joint)
                {
                    snapshot.Joint.connectedBody = snapshot.ConnectedBody;
                    snapshot.Joint.xMotion = snapshot.XMotion;
                    snapshot.Joint.yMotion = snapshot.YMotion;
                    snapshot.Joint.zMotion = snapshot.ZMotion;
                    snapshot.Joint.angularXMotion = snapshot.AngularXMotion;
                    snapshot.Joint.angularYMotion = snapshot.AngularYMotion;
                    snapshot.Joint.angularZMotion = snapshot.AngularZMotion;
                    snapshot.Joint.connectedAnchor = snapshot.ConnectedAnchor;
                    snapshot.Joint.autoConfigureConnectedAnchor = snapshot.AutoConfigureConnectedAnchor;
                    snapshot.Joint.slerpDrive = snapshot.SlerpDrive;
                    snapshot.Joint.targetAngularVelocity = snapshot.TargetAngularVelocity;
                }
                if (snapshot.Rigidbody)
                {
                    snapshot.Rigidbody.isKinematic = snapshot.IsKinematic;
                    snapshot.Rigidbody.detectCollisions = snapshot.DetectCollisions;
                    if (!snapshot.Rigidbody.isKinematic)
                    {
                        snapshot.Rigidbody.linearVelocity = snapshot.Velocity;
                        snapshot.Rigidbody.angularVelocity = snapshot.AngularVelocity;
                        if (snapshot.WasSleeping)
                            snapshot.Rigidbody.Sleep();
                        else
                            snapshot.Rigidbody.WakeUp();
                    }
                }
                if (snapshot.Target)
                {
                    snapshot.Target.SetParent(snapshot.TargetParent, false);
                    snapshot.Target.localPosition = snapshot.TargetLocalPosition;
                    snapshot.Target.localRotation = snapshot.TargetLocalRotation;
                    snapshot.Target.localScale = snapshot.TargetLocalScale;
                    snapshot.Target.SetSiblingIndex(snapshot.TargetSiblingIndex);
                }
            }
        }

        static void ReleaseRemovedMuscles(
            BoneName rootName,
            RemovedPhysicalSnapshot[] removed,
            bool attachTargets,
            bool blockTargetAnimation,
            RagdollMuscleRemoveMode removeMode)
        {
            for (int index = 0; index < removed.Length; index++)
            {
                RemovedPhysicalSnapshot snapshot = removed[index];
                ConfigurableJoint joint = snapshot.Joint;
                bool disconnectJoint = removeMode == RagdollMuscleRemoveMode.Explode
                    || (removeMode == RagdollMuscleRemoveMode.Sever
                        && snapshot.Bone == rootName);

                if (joint)
                {
                    joint.slerpDrive = new JointDrive();
                    joint.targetAngularVelocity = Vector3.zero;
                    if (disconnectJoint)
                    {
                        joint.connectedBody = null;
                        joint.xMotion = ConfigurableJointMotion.Free;
                        joint.yMotion = ConfigurableJointMotion.Free;
                        joint.zMotion = ConfigurableJointMotion.Free;
                        joint.angularXMotion = ConfigurableJointMotion.Free;
                        joint.angularYMotion = ConfigurableJointMotion.Free;
                        joint.angularZMotion = ConfigurableJointMotion.Free;
                    }
                }

                if (attachTargets && snapshot.Target && snapshot.MuscleTransform)
                {
                    snapshot.Target.SetPositionAndRotation(
                        snapshot.TargetWorldPosition,
                        snapshot.TargetWorldRotation);
                    snapshot.Target.SetParent(snapshot.MuscleTransform, true);
                }

                if (blockTargetAnimation && snapshot.Target)
                {
                    RagdollTargetAnimationBlocker blocker =
                        snapshot.Target.GetComponent<RagdollTargetAnimationBlocker>();
                    if (!blocker)
                    {
                        blocker = snapshot.Target.gameObject
                            .AddComponent<RagdollTargetAnimationBlocker>();
                    }
                    blocker.Configure(new[] { snapshot.Target });
                }
            }
        }

        internal RagdollMuscleGroup? ResolveRuntimeMuscleGroup(BoneName bone)
        {
            RuntimeMuscleData data;
            return runtimeMuscles.TryGetValue(bone, out data)
                ? (RagdollMuscleGroup?)data.Registration.Group
                : null;
        }

        RagdollTargetBinding[] ResolveCurrentTargetBindings(
            out string error)
        {
            error = null;
            if (_targetBindings
                && _targetBindings.RagdollBindings != Bindings)
            {
                error = "The target bindings reference a different RagdollDefinitionBindings component.";
                return null;
            }

            RagdollTargetBinding[] resolved =
                new RagdollTargetBinding[Bindings.BoneCount];
            Transform[] targetHierarchy = null;

            for (int index = 0; index < Bindings.BoneCount; index++)
            {
                RagdollBone bone = Bindings.GetBoneAt(index);
                RuntimeMuscleData runtime;
                if (runtimeMuscles.TryGetValue(bone.Name, out runtime))
                {
                    resolved[index] = runtime.TargetBinding;
                    continue;
                }

                RagdollTargetBinding explicitBinding;
                if (_targetBindings
                    && _targetBindings.TryGetBinding(
                        bone.Name,
                        out explicitBinding))
                {
                    if (!explicitBinding.Target || !explicitBinding.OffsetsCaptured)
                    {
                        error = "Target binding '" + bone.Name
                            + "' is missing a target or captured offsets.";
                        return null;
                    }
                    resolved[index] = explicitBinding;
                    continue;
                }

                if (_targetBindings)
                {
                    error = "No explicit Target binding exists for active bone '"
                        + bone.Name + "'.";
                    return null;
                }

                if (targetHierarchy == null)
                {
                    targetHierarchy = GetComponentsInChildren<Transform>(true);
                }
                Transform unique = null;
                for (int targetIndex = 0;
                    targetIndex < targetHierarchy.Length;
                    targetIndex++)
                {
                    if (targetHierarchy[targetIndex].name
                        != bone.Transform.name)
                    {
                        continue;
                    }
                    if (unique)
                    {
                        error = "Legacy Target binding found more than one Transform named '"
                            + bone.Transform.name + "'.";
                        return null;
                    }
                    unique = targetHierarchy[targetIndex];
                }
                if (!unique)
                {
                    error = "Legacy Target binding could not find Transform '"
                        + bone.Transform.name + "'.";
                    return null;
                }
                resolved[index] = new RagdollTargetBinding(
                    bone.Name,
                    unique,
                    bone.Transform);
            }

            UsesLegacyTargetBindingFallback = !_targetBindings;
            return resolved;
        }

        AnimatedPair GetAnimatedPairForName(BoneName bone)
        {
            for (int index = 0; index < animatedPairs.Length; index++)
            {
                if (animatedPairs[index].Name == bone) return animatedPairs[index];
            }
            throw new InvalidOperationException(
                "No animated pair exists for ragdoll bone '" + bone + "'.");
        }

        void NotifyHierarchyCommitted(
            RagdollMuscleChange[] added,
            RagdollMuscleChange[] removed)
        {
            // Core registry subscribers update physical settings, collision relays and
            // diagnostics before behaviours or user callbacks observe the new generation.
            try
            {
                Bindings.NotifyRuntimeHierarchyChanged();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception, Bindings);
            }

            RagdollBehaviourController behaviours =
                GetComponent<RagdollBehaviourController>();
            if (behaviours && behaviours.IsInitialized)
            {
                try
                {
                    behaviours.NotifyHierarchyChanged(added, removed);
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception, behaviours);
                }
            }

            for (int index = 0; index < removed.Length; index++)
            {
                InvokeMuscleChangeSafely(MuscleRemoved, removed[index]);
            }
            for (int index = 0; index < added.Length; index++)
            {
                InvokeMuscleChangeSafely(MuscleAdded, added[index]);
            }
            InvokeHierarchyChangedSafely(HierarchyChanged);
        }

        void InvokeMuscleChangeSafely(
            Action<RagdollMuscleChange> handlers,
            RagdollMuscleChange change)
        {
            if (handlers == null) return;
            Delegate[] invocationList = handlers.GetInvocationList();
            for (int index = 0; index < invocationList.Length; index++)
            {
                try
                {
                    ((Action<RagdollMuscleChange>)invocationList[index])(change);
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception, this);
                }
            }
        }

        void InvokeHierarchyChangedSafely(Action handlers)
        {
            if (handlers == null) return;
            Delegate[] invocationList = handlers.GetInvocationList();
            for (int index = 0; index < invocationList.Length; index++)
            {
                try
                {
                    ((Action)invocationList[index])();
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception, this);
                }
            }
        }

        /// <summary>
        /// Parents every physical muscle to the authored Puppet container. Joint
        /// connectedBody topology and world poses are preserved.
        /// </summary>
        public void FlattenHierarchy()
        {
            EnsureHierarchyLayoutReady();
            Transform puppetParent = authoredPuppetContainer;
            for (int index = 0; index < animatedPairs.Length; index++)
            {
                AnimatedPair pair = animatedPairs[index];
                pair.RagdollBone.Transform.SetParent(puppetParent, true);
            }
        }

        /// <summary>
        /// Rebuilds physical Transform parenting from the registered muscle topology.
        /// Joint connectedBody topology and world poses are preserved.
        /// </summary>
        public void TreeHierarchy()
        {
            EnsureHierarchyLayoutReady();
            for (int index = 0; index < animatedPairs.Length; index++)
            {
                AnimatedPair pair = animatedPairs[index];
                if (pair.RagdollBone.IsRoot)
                {
                    pair.RagdollBone.Transform.SetParent(
                        authoredPuppetContainer,
                        true);
                    continue;
                }

                RagdollBoneHandle parentHandle;
                if (!Bindings.Topology.TryGetParent(pair.Handle, out parentHandle))
                {
                    throw new InvalidOperationException(
                        "A non-root muscle has no registered topology parent.");
                }
                Transform parent = Bindings.GetBone(parentHandle).Transform;
                pair.RagdollBone.Transform.SetParent(parent, true);
            }
        }

        public bool HierarchyIsFlat()
        {
            EnsureHierarchyLayoutReady();
            Transform puppetParent = authoredPuppetContainer;
            for (int index = 0; index < animatedPairs.Length; index++)
            {
                RagdollBone bone = animatedPairs[index].RagdollBone;
                if (bone.Transform.parent != puppetParent)
                    return false;
            }
            return true;
        }

        /// <summary>Moves every available muscle to its current Target position.</summary>
        public void FixMusclePositions()
        {
            FixMusclePose(false);
        }

        /// <summary>Moves every available muscle to its current Target pose.</summary>
        public void FixMusclePositionsAndRotations()
        {
            FixMusclePose(true);
        }

        public void SwitchToActiveMode()
        {
            GetSimulationModeController().SetMode(RagdollSimulationMode.Active);
        }

        public void SwitchToKinematicMode()
        {
            GetSimulationModeController().SetMode(RagdollSimulationMode.Kinematic);
        }

        public void SwitchToDisabledMode()
        {
            GetSimulationModeController().SetMode(RagdollSimulationMode.Disabled);
        }

        public void DisableImmediately()
        {
            GetSimulationModeController().SetModeImmediate(
                RagdollSimulationMode.Disabled);
        }

        void FixMusclePose(bool includeRotations)
        {
            EnsureHierarchyLayoutReady();
            for (int index = 0; index < animatedPairs.Length; index++)
            {
                AnimatedPair pair = animatedPairs[index];
                if (IsMuscleUnavailable(pair)) continue;
                AnimatedPose target = AnimatedPose.Read(pair.TargetBone);
                AnimatedPose physical = pair.ConvertTargetPoseToRagdoll(target);
                Rigidbody body = pair.RagdollBone.Rigidbody;
                body.position = physical.worldPosition;
                if (includeRotations) body.rotation = physical.worldRotation;
            }
        }

        RagdollSimulationModeController GetSimulationModeController()
        {
            RagdollSimulationModeController controller =
                GetComponent<RagdollSimulationModeController>();
            if (!controller || !controller.IsInitialized)
            {
                throw new InvalidOperationException(
                    "Ragdoll simulation mode is not initialized.");
            }
            return controller;
        }

        void EnsureHierarchyLayoutReady()
        {
            if (animatedPairs == null || !Bindings || !Bindings.IsInitialized)
            {
                throw new InvalidOperationException(
                    "Ragdoll hierarchy is not initialized.");
            }
            if (hierarchyTransactionInProgress)
            {
                throw new InvalidOperationException(
                    "Hierarchy layout cannot change during a muscle transaction.");
            }
        }
    }
}
