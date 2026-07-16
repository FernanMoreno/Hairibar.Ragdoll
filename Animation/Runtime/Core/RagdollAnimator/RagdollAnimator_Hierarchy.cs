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
            internal ConfigurableJoint Joint;
            internal Rigidbody Rigidbody;
            internal bool WasSleeping;
            internal JointDrive SlerpDrive;
            internal Vector3 TargetAngularVelocity;
            internal Transform Target;
            internal Transform TargetParent;
            internal int TargetSiblingIndex;
            internal Vector3 TargetLocalPosition;
            internal Quaternion TargetLocalRotation;
            internal Vector3 TargetLocalScale;
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
                    Debug.LogException(rollbackException, this);
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

        /// <summary>
        /// Removes a registered muscle and every descendant connected through the
        /// current Rigidbody topology. The joints are released from animation matching
        /// but are not disconnected or destroyed in this sprint.
        /// </summary>
        public RagdollMuscleChange[] RemoveMuscleRecursive(
            ConfigurableJoint joint,
            bool attachTargets = false)
        {
            RagdollMuscleChange[] removed;
            string error;
            if (!TryRemoveMuscleRecursive(
                joint,
                attachTargets,
                out removed,
                out error))
            {
                throw new InvalidOperationException(error);
            }
            return removed;
        }

        /// <summary>
        /// Non-throwing variant of RemoveMuscleRecursive.
        /// </summary>
        public bool TryRemoveMuscleRecursive(
            ConfigurableJoint joint,
            bool attachTargets,
            out RagdollMuscleChange[] removedChanges,
            out string error)
        {
            removedChanges = new RagdollMuscleChange[0];
            error = null;
            if (!ValidateHierarchyMutation(out error)) return false;
            if (!joint)
            {
                error = "RemoveMuscleRecursive requires a ConfigurableJoint.";
                return false;
            }

            RagdollBone requested;
            if (!Bindings.TryGetBone(joint, out requested))
            {
                error = "No registered muscle uses the supplied ConfigurableJoint.";
                return false;
            }
            if (requested.IsRoot)
            {
                error = "The root muscle cannot be removed at runtime.";
                return false;
            }

            RagdollDefinitionBindings.RuntimeRegistrySnapshot bindingSnapshot =
                Bindings.CaptureRuntimeRegistry();
            AnimatedPair[] oldPairs = animatedPairs;
            RagdollHierarchySubsystemSnapshot subsystemSnapshot =
                CaptureHierarchySubsystemSnapshot(oldPairs);
            Dictionary<BoneName, RuntimeMuscleData> runtimeSnapshot =
                new Dictionary<BoneName, RuntimeMuscleData>(runtimeMuscles);
            RemovedPhysicalSnapshot[] physicalSnapshots =
                new RemovedPhysicalSnapshot[0];

            hierarchyTransactionInProgress = true;
            try
            {
                RagdollBone[] removedBones;
                if (!Bindings.TryRemoveRuntimeSubtree(
                    joint,
                    out removedBones,
                    out error))
                {
                    return false;
                }

                removedChanges = CreateRemovedChanges(removedBones, oldPairs);
                physicalSnapshots = CaptureRemovedSnapshots(removedChanges);
                for (int index = 0; index < removedBones.Length; index++)
                {
                    runtimeMuscles.Remove(removedBones[index].Name);
                }

                RebuildRuntimeHierarchy(oldPairs, subsystemSnapshot);
                ReleaseRemovedMuscles(removedChanges, attachTargets);
                NotifyHierarchyCommitted(
                    new RagdollMuscleChange[0],
                    removedChanges);
                return true;
            }
            catch (Exception exception)
            {
                error = "The runtime muscle removal was rolled back: "
                    + exception.Message;
                try
                {
                    ShutdownInternalCollisions();
                    ShutdownJointRuntime();
                    runtimeMuscles.Clear();
                    foreach (KeyValuePair<BoneName, RuntimeMuscleData> pair
                        in runtimeSnapshot)
                    {
                        runtimeMuscles.Add(pair.Key, pair.Value);
                    }
                    Bindings.RestoreRuntimeRegistry(bindingSnapshot);
                    RestoreRemovedSnapshots(physicalSnapshots);
                    RebuildRuntimeHierarchy(oldPairs, subsystemSnapshot);
                }
                catch (Exception rollbackException)
                {
                    Debug.LogException(rollbackException, this);
                    error += " Rollback also failed: " + rollbackException.Message;
                }
                removedChanges = new RagdollMuscleChange[0];
                return false;
            }
            finally
            {
                hierarchyTransactionInProgress = false;
            }
        }

        bool ValidateHierarchyMutation(out string error)
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

            body.velocity = parentBone.Rigidbody.velocity;
            body.angularVelocity = parentBone.Rigidbody.angularVelocity;
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
                Velocity = body.velocity,
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
            body.velocity = snapshot.Velocity;
            body.angularVelocity = snapshot.AngularVelocity;
            if (snapshot.WasSleeping && !body.isKinematic) body.Sleep();
            else if (!body.isKinematic) body.WakeUp();
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
            RagdollMuscleChange[] removed)
        {
            RemovedPhysicalSnapshot[] snapshots =
                new RemovedPhysicalSnapshot[removed.Length];
            for (int index = 0; index < removed.Length; index++)
            {
                ConfigurableJoint joint = removed[index].Joint;
                Transform target = removed[index].Target;
                snapshots[index] = new RemovedPhysicalSnapshot
                {
                    Joint = joint,
                    Rigidbody = joint ? joint.GetComponent<Rigidbody>() : null,
                    WasSleeping = joint
                        && joint.GetComponent<Rigidbody>()
                        && joint.GetComponent<Rigidbody>().IsSleeping(),
                    SlerpDrive = joint ? joint.slerpDrive : new JointDrive(),
                    TargetAngularVelocity = joint
                        ? joint.targetAngularVelocity
                        : Vector3.zero,
                    Target = target,
                    TargetParent = target ? target.parent : null,
                    TargetSiblingIndex = target
                        ? target.GetSiblingIndex()
                        : 0,
                    TargetLocalPosition = target
                        ? target.localPosition
                        : Vector3.zero,
                    TargetLocalRotation = target
                        ? target.localRotation
                        : Quaternion.identity,
                    TargetLocalScale = target
                        ? target.localScale
                        : Vector3.one
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
                if (snapshot.Joint)
                {
                    snapshot.Joint.slerpDrive = snapshot.SlerpDrive;
                    snapshot.Joint.targetAngularVelocity =
                        snapshot.TargetAngularVelocity;
                }
                if (snapshot.Rigidbody && !snapshot.Rigidbody.isKinematic)
                {
                    if (snapshot.WasSleeping) snapshot.Rigidbody.Sleep();
                    else snapshot.Rigidbody.WakeUp();
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
            RagdollMuscleChange[] removed,
            bool attachTargets)
        {
            for (int index = 0; index < removed.Length; index++)
            {
                ConfigurableJoint joint = removed[index].Joint;
                if (!joint) continue;
                joint.slerpDrive = new JointDrive();
                joint.targetAngularVelocity = Vector3.zero;

                if (attachTargets && removed[index].Target)
                {
                    removed[index].Target.SetParent(joint.transform, true);
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
                Debug.LogException(exception, Bindings);
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
                    Debug.LogException(exception, behaviours);
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
                    Debug.LogException(exception, this);
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
                    Debug.LogException(exception, this);
                }
            }
        }
    }
}
