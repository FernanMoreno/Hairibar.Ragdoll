using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    public partial class RagdollAnimator
    {
        internal enum PendingConnectionOperationType
        {
            Disconnect,
            Reconnect,
            JointBreak
        }

        internal struct PendingConnectionOperation
        {
            internal PendingConnectionOperationType Type;
            internal BoneName Bone;
            internal RagdollMuscleDisconnectMode Mode;
            internal bool Deactivate;
            internal float BreakForce;
        }

        internal struct ConnectionPhysicalSnapshot
        {
            internal Transform Parent;
            internal int SiblingIndex;
            internal Vector3 LocalPosition;
            internal Quaternion LocalRotation;
            internal Vector3 LocalScale;
            internal bool ActiveSelf;
            internal bool IsKinematic;
            internal ConfigurableJointMotion XMotion;
            internal ConfigurableJointMotion YMotion;
            internal ConfigurableJointMotion ZMotion;
            internal ConfigurableJointMotion AngularXMotion;
            internal ConfigurableJointMotion AngularYMotion;
            internal ConfigurableJointMotion AngularZMotion;
            internal Rigidbody ConnectedBody;
            internal Vector3 ConnectedAnchor;
            internal bool AutoConfigureConnectedAnchor;
            internal JointDrive SlerpDrive;
            internal Vector3 TargetAngularVelocity;
            internal Vector3 Velocity;
            internal Vector3 AngularVelocity;
            internal bool WasSleeping;
        }


        internal struct ConnectionHierarchyEntry
        {
            internal RagdollMuscleConnectionState State;
            internal RagdollMuscleDisconnectMode Mode;
            internal bool Severed;
            internal int CollisionIslandId;
            internal bool SnapshotCaptured;
            internal ConnectionPhysicalSnapshot Snapshot;
        }

        internal sealed class MuscleConnectionHierarchySnapshot
        {
            internal readonly Dictionary<BoneName, ConnectionHierarchyEntry> Entries;
            internal readonly PendingConnectionOperation[] Pending;
            internal readonly BoneName[] Broken;
            internal readonly int NextCollisionIslandId;

            internal MuscleConnectionHierarchySnapshot(
                Dictionary<BoneName, ConnectionHierarchyEntry> entries,
                PendingConnectionOperation[] pending,
                BoneName[] broken,
                int nextCollisionIslandId)
            {
                Entries = entries;
                Pending = pending;
                Broken = broken;
                NextCollisionIslandId = nextCollisionIslandId;
            }
        }

        internal sealed class ConnectionRecord
        {
            internal AnimatedPair Pair;
            internal RagdollMuscleConnectionState State;
            internal RagdollMuscleDisconnectMode Mode;
            internal bool Severed;
            internal int CollisionIslandId;
            internal bool SnapshotCaptured;
            internal ConnectionPhysicalSnapshot Snapshot;
        }

        [SerializeField, HideInInspector]
        [Tooltip("If enabled, disconnected muscle Targets are mapped fully to their physical bodies after normal mapping and write hooks.")]
        bool mapDisconnectedMuscles = true;

        readonly List<PendingConnectionOperation> pendingConnectionOperations =
            new List<PendingConnectionOperation>();
        readonly HashSet<BoneName> brokenMuscles = new HashSet<BoneName>();
        ConnectionRecord[] connectionRecords;
        bool[] disconnectedBoneMask;
        bool[] severedBoneMask;
        bool[] mappingSuppressedBoneMask;
        int disconnectedMuscleCount;
        int nextDisconnectedCollisionIslandId;
        int[] disconnectedCollisionIslandIds;
        RagdollJointBreakBroadcaster[] jointBreakBroadcasters;

        public bool MapDisconnectedMuscles
        {
            get => mapDisconnectedMuscles;
            set => mapDisconnectedMuscles = value;
        }
        public int DisconnectedMuscleCount => disconnectedMuscleCount;
        public int PendingMuscleConnectionOperationCount =>
            pendingConnectionOperations.Count;
        public bool HasDisconnectedMuscles => disconnectedMuscleCount > 0;

        public event Action<RagdollMuscleConnectionChange> MuscleDisconnected;
        public event Action<RagdollMuscleConnectionChange> MuscleReconnected;
        public event Action<RagdollJointBreakEvent> JointBroken;

        public RagdollMuscleConnectionState GetMuscleConnectionState(
            RagdollBoneHandle muscle)
        {
            ValidateConnectionHandle(muscle);
            return connectionRecords[muscle.Index].State;
        }

        public bool IsDisconnecting(RagdollBoneHandle muscle)
        {
            ValidateConnectionHandle(muscle);
            return HasPendingOperation(
                connectionRecords[muscle.Index].Pair.Name,
                PendingConnectionOperationType.Disconnect);
        }

        public bool IsReconnecting(RagdollBoneHandle muscle)
        {
            ValidateConnectionHandle(muscle);
            return HasPendingOperation(
                connectionRecords[muscle.Index].Pair.Name,
                PendingConnectionOperationType.Reconnect);
        }

        public void DisconnectMuscleRecursive(
            RagdollBoneHandle muscle,
            RagdollMuscleDisconnectMode mode = RagdollMuscleDisconnectMode.Sever,
            bool deactivate = false)
        {
            string error;
            if (!TryDisconnectMuscleRecursive(muscle, mode, deactivate, out error))
            {
                throw new InvalidOperationException(error);
            }
        }

        public bool TryDisconnectMuscleRecursive(
            RagdollBoneHandle muscle,
            RagdollMuscleDisconnectMode mode,
            bool deactivate,
            out string error)
        {
            error = null;
            if (!TryValidateConnectionRequest(muscle, out error)) return false;
            if (!Enum.IsDefined(typeof(RagdollMuscleDisconnectMode), mode))
            {
                error = "The disconnect mode is not supported.";
                return false;
            }

            BoneName bone = connectionRecords[muscle.Index].Pair.Name;
            if (connectionRecords[muscle.Index].State
                != RagdollMuscleConnectionState.Connected)
            {
                error = "The requested muscle is already disconnected.";
                return false;
            }
            ReplacePendingOperation(new PendingConnectionOperation
            {
                Type = PendingConnectionOperationType.Disconnect,
                Bone = bone,
                Mode = mode,
                Deactivate = deactivate
            });
            return true;
        }

        public void DisconnectMuscleRecursive(
            ConfigurableJoint joint,
            RagdollMuscleDisconnectMode mode = RagdollMuscleDisconnectMode.Sever,
            bool deactivate = false)
        {
            RagdollBoneHandle handle;
            if (!joint || !Bindings.TryGetBoneHandle(joint, out handle))
            {
                throw new ArgumentException(
                    "No registered muscle uses the supplied ConfigurableJoint.",
                    nameof(joint));
            }
            DisconnectMuscleRecursive(handle, mode, deactivate);
        }

        public void ReconnectMuscleRecursive(RagdollBoneHandle muscle)
        {
            string error;
            if (!TryReconnectMuscleRecursive(muscle, out error))
            {
                throw new InvalidOperationException(error);
            }
        }

        public bool TryReconnectMuscleRecursive(
            RagdollBoneHandle muscle,
            out string error)
        {
            error = null;
            if (!TryValidateConnectionRequest(muscle, out error)) return false;
            int rootIndex = RagdollMuscleConnectionPolicy
                .FindHighestDisconnectedAncestor(
                    Bindings.Topology,
                    muscle,
                    disconnectedBoneMask);
            if (!disconnectedBoneMask[rootIndex])
            {
                error = "The requested muscle is not disconnected.";
                return false;
            }
            ReplacePendingOperation(new PendingConnectionOperation
            {
                Type = PendingConnectionOperationType.Reconnect,
                Bone = connectionRecords[rootIndex].Pair.Name
            });
            return true;
        }

        public void ReconnectMuscleRecursive(ConfigurableJoint joint)
        {
            RagdollBoneHandle handle;
            if (!joint || !Bindings.TryGetBoneHandle(joint, out handle))
            {
                throw new ArgumentException(
                    "No registered muscle uses the supplied ConfigurableJoint.",
                    nameof(joint));
            }
            ReconnectMuscleRecursive(handle);
        }

        internal void ScheduleJointBreak(BoneName bone, float breakForce)
        {
            if (animatedPairs == null || brokenMuscles.Contains(bone)) return;
            RagdollBone current;
            if (!Bindings.TryGetBone(bone, out current)) return;

            RagdollBoneHandle root;
            if (!Bindings.TryGetBoneHandle(bone, out root)) return;
            for (int index = 0; index < Bindings.BoneCount; index++)
            {
                RagdollBoneHandle candidate = Bindings.GetHandleAt(index);
                if (candidate == root
                    || Bindings.Topology.IsAncestorOf(root, candidate))
                {
                    brokenMuscles.Add(Bindings.GetBone(candidate).Name);
                }
            }
            RefreshMappingSuppressionMask();
            ReplacePendingOperation(new PendingConnectionOperation
            {
                Type = PendingConnectionOperationType.JointBreak,
                Bone = bone,
                BreakForce = IsFinite(breakForce) ? breakForce : 0f
            });
        }

        void InitializeMuscleConnections()
        {
            RestoreMuscleConnectionRegistry(null);
            FinalizeMuscleConnectionRebuild();
        }

        MuscleConnectionHierarchySnapshot CaptureMuscleConnectionHierarchySnapshot()
        {
            if (connectionRecords == null) return null;

            Dictionary<BoneName, ConnectionHierarchyEntry> entries =
                new Dictionary<BoneName, ConnectionHierarchyEntry>();
            for (int index = 0; index < connectionRecords.Length; index++)
            {
                ConnectionRecord record = connectionRecords[index];
                entries[record.Pair.Name] = new ConnectionHierarchyEntry
                {
                    State = record.State,
                    Mode = record.Mode,
                    Severed = record.Severed,
                    CollisionIslandId = record.CollisionIslandId,
                    SnapshotCaptured = record.SnapshotCaptured,
                    Snapshot = record.Snapshot
                };
            }
            return new MuscleConnectionHierarchySnapshot(
                entries,
                pendingConnectionOperations.ToArray(),
                new List<BoneName>(brokenMuscles).ToArray(),
                nextDisconnectedCollisionIslandId);
        }

        void RestoreMuscleConnectionRegistry(
            MuscleConnectionHierarchySnapshot snapshot)
        {
            connectionRecords = new ConnectionRecord[Bindings.BoneCount];
            disconnectedBoneMask = new bool[Bindings.BoneCount];
            severedBoneMask = new bool[Bindings.BoneCount];
            mappingSuppressedBoneMask = new bool[Bindings.BoneCount];
            disconnectedCollisionIslandIds = new int[Bindings.BoneCount];
            disconnectedMuscleCount = 0;
            nextDisconnectedCollisionIslandId = snapshot == null
                ? 0
                : snapshot.NextCollisionIslandId;
            pendingConnectionOperations.Clear();
            brokenMuscles.Clear();

            for (int index = 0; index < animatedPairs.Length; index++)
            {
                AnimatedPair pair = animatedPairsByHandleIndex[index];
                ConnectionHierarchyEntry previous;
                bool retained = snapshot != null
                    && snapshot.Entries.TryGetValue(pair.Name, out previous);
                ConnectionRecord record = new ConnectionRecord
                {
                    Pair = pair,
                    State = retained
                        ? previous.State
                        : RagdollMuscleConnectionState.Connected,
                    Mode = retained
                        ? previous.Mode
                        : RagdollMuscleDisconnectMode.Sever,
                    Severed = retained && previous.Severed,
                    CollisionIslandId = retained
                        ? previous.CollisionIslandId
                        : 0,
                    SnapshotCaptured = retained && previous.SnapshotCaptured,
                    Snapshot = retained
                        ? previous.Snapshot
                        : new ConnectionPhysicalSnapshot()
                };
                connectionRecords[index] = record;
                bool disconnected = record.State
                    != RagdollMuscleConnectionState.Connected;
                disconnectedBoneMask[index] = disconnected;
                severedBoneMask[index] = disconnected && record.Severed;
                disconnectedCollisionIslandIds[index] = disconnected
                    ? record.CollisionIslandId
                    : 0;
                if (disconnected) disconnectedMuscleCount++;
            }

            if (snapshot != null)
            {
                for (int index = 0; index < snapshot.Pending.Length; index++)
                {
                    RagdollBone ignored;
                    if (Bindings.TryGetBone(snapshot.Pending[index].Bone, out ignored))
                    {
                        pendingConnectionOperations.Add(snapshot.Pending[index]);
                    }
                }
                for (int index = 0; index < snapshot.Broken.Length; index++)
                {
                    RagdollBone ignored;
                    if (Bindings.TryGetBone(snapshot.Broken[index], out ignored))
                    {
                        brokenMuscles.Add(snapshot.Broken[index]);
                    }
                }
            }
            RefreshMappingSuppressionMask();
        }

        void FinalizeMuscleConnectionRebuild()
        {
            InitializeJointBreakBroadcasters();
            ApplyDisconnectedMasksToPhysicalOwners();
            ReapplyDisconnectedPhysicalPolicies();
        }

        void ShutdownMuscleConnections()
        {
            ShutdownJointBreakBroadcasters();
            pendingConnectionOperations.Clear();
            brokenMuscles.Clear();
            connectionRecords = null;
            disconnectedBoneMask = null;
            severedBoneMask = null;
            mappingSuppressedBoneMask = null;
            disconnectedCollisionIslandIds = null;
            disconnectedMuscleCount = 0;
            nextDisconnectedCollisionIslandId = 0;
        }

        void ProcessPendingMuscleConnectionOperations()
        {
            if (pendingConnectionOperations.Count == 0) return;
            if (hierarchyTransactionInProgress) return;

            // Unity has already broken the physical joint, so the complete branch is
            // suppressed immediately. Structural removal waits for stable Alive/Active
            // physics because lifecycle and mode owners must not be reconstructed mid-state.
            if (CanCommitJointBreakRemoval())
            {
                for (int index = pendingConnectionOperations.Count - 1;
                    index >= 0;
                    index--)
                {
                    PendingConnectionOperation operation =
                        pendingConnectionOperations[index];
                    if (operation.Type != PendingConnectionOperationType.JointBreak)
                    {
                        continue;
                    }

                    // Structural removal rebuilds the connection registry and therefore
                    // replaces this pending-operation list. Consume one break before the
                    // rebuild and restart queue processing at the next fixed boundary.
                    pendingConnectionOperations.RemoveAt(index);
                    if (!ProcessJointBreak(operation))
                    {
                        ReplacePendingOperation(operation);
                    }
                    return;
                }
            }

            if (!CanCommitConnectionOperation()) return;
            int operationIndex = 0;
            while (operationIndex < pendingConnectionOperations.Count)
            {
                PendingConnectionOperation operation =
                    pendingConnectionOperations[operationIndex];
                if (operation.Type == PendingConnectionOperationType.JointBreak)
                {
                    operationIndex++;
                    continue;
                }
                pendingConnectionOperations.RemoveAt(operationIndex);
                try
                {
                    if (operation.Type == PendingConnectionOperationType.Disconnect)
                    {
                        ApplyDisconnect(operation);
                    }
                    else if (operation.Type == PendingConnectionOperationType.Reconnect)
                    {
                        ApplyReconnect(operation);
                    }
                }
                catch
                {
                    ReplacePendingOperation(operation);
                    throw;
                }
            }
        }

        bool CanCommitConnectionOperation()
        {
            if (connectionRecords == null
                || hierarchyTransactionInProgress
                || lifecyclePermanentDestructionScheduled
                || !IsAlive
                || IsKilling
                || IsSwitchingState)
            {
                return false;
            }
            RagdollSimulationModeController mode =
                GetComponent<RagdollSimulationModeController>();
            return !mode
                || !mode.IsInitialized
                || (!mode.IsTransitioning
                    && mode.CurrentMode != RagdollSimulationMode.Disabled);
        }

        bool CanCommitJointBreakRemoval()
        {
            return CanCommitConnectionOperation();
        }

        void ApplyDisconnect(PendingConnectionOperation operation)
        {
            RagdollBoneHandle root;
            if (!Bindings.TryGetBoneHandle(operation.Bone, out root)) return;
            if (connectionRecords[root.Index].State
                != RagdollMuscleConnectionState.Connected)
            {
                return;
            }

            bool[] branch = new bool[Bindings.BoneCount];
            bool[] severed = new bool[Bindings.BoneCount];
            RagdollMuscleConnectionPolicy.BuildDisconnectMasks(
                Bindings.Topology,
                root,
                operation.Mode,
                branch,
                severed);

            List<ConnectionRecord> applied = new List<ConnectionRecord>();
            int islandCounterBefore = nextDisconnectedCollisionIslandId;
            int severIslandId = operation.Mode == RagdollMuscleDisconnectMode.Sever
                ? AllocateDisconnectedCollisionIslandId()
                : 0;
            try
            {
                for (int index = 0; index < connectionRecords.Length; index++)
                {
                    if (!branch[index] || disconnectedBoneMask[index]) continue;
                    ConnectionRecord record = connectionRecords[index];
                    CaptureConnectionSnapshot(record);
                    applied.Add(record);
                    record.Mode = operation.Mode;
                    record.Severed = severed[index];
                    record.CollisionIslandId = operation.Mode
                        == RagdollMuscleDisconnectMode.Explode
                            ? AllocateDisconnectedCollisionIslandId()
                            : severIslandId;
                    ApplyDisconnectedPhysicalState(record, operation.Deactivate);
                    disconnectedBoneMask[index] = true;
                    severedBoneMask[index] = severed[index];
                    disconnectedCollisionIslandIds[index] =
                        record.CollisionIslandId;
                    record.State = operation.Deactivate
                        ? RagdollMuscleConnectionState.Deactivated
                        : RagdollMuscleConnectionState.Disconnected;
                    disconnectedMuscleCount++;
                }
            }
            catch
            {
                for (int index = applied.Count - 1; index >= 0; index--)
                {
                    ConnectionRecord record = applied[index];
                    RestoreConnectionSnapshot(record, false, true);
                    int handleIndex = record.Pair.Handle.Index;
                    if (disconnectedBoneMask[handleIndex])
                    {
                        disconnectedMuscleCount--;
                    }
                    disconnectedBoneMask[handleIndex] = false;
                    severedBoneMask[handleIndex] = false;
                    disconnectedCollisionIslandIds[handleIndex] = 0;
                    record.State = RagdollMuscleConnectionState.Connected;
                    record.Severed = false;
                    record.CollisionIslandId = 0;
                }
                nextDisconnectedCollisionIslandId = islandCounterBefore;
                ApplyDisconnectedMasksToPhysicalOwners();
                throw;
            }

            RefreshMappingSuppressionMask();
            ApplyDisconnectedMasksToPhysicalOwners();
            RefreshJointRuntimeConfiguration();
            ReapplyDisconnectedPhysicalPolicies();
            ReapplyInternalCollisionPolicy();
            for (int index = 0; index < applied.Count; index++)
            {
                RagdollMuscleConnectionChange change =
                    CreateConnectionChange(applied[index]);
                NotifyBehaviourMuscleDisconnected(change);
                InvokeConnectionChangeSafely(MuscleDisconnected, change);
            }
        }

        void ApplyReconnect(PendingConnectionOperation operation)
        {
            RagdollBoneHandle requested;
            if (!Bindings.TryGetBoneHandle(operation.Bone, out requested)) return;
            int rootIndex = RagdollMuscleConnectionPolicy
                .FindHighestDisconnectedAncestor(
                    Bindings.Topology,
                    requested,
                    disconnectedBoneMask);
            if (!disconnectedBoneMask[rootIndex]) return;
            RagdollBoneHandle root = Bindings.GetHandleAt(rootIndex);

            List<ConnectionRecord> candidates = new List<ConnectionRecord>();
            for (int index = 0; index < connectionRecords.Length; index++)
            {
                RagdollBoneHandle candidate = Bindings.GetHandleAt(index);
                bool inBranch = candidate == root
                    || Bindings.Topology.IsAncestorOf(root, candidate);
                if (inBranch && disconnectedBoneMask[index])
                {
                    candidates.Add(connectionRecords[index]);
                }
            }

            List<ConnectionRecord> restored = new List<ConnectionRecord>();
            try
            {
                for (int index = 0; index < candidates.Count; index++)
                {
                    ConnectionRecord record = candidates[index];
                    RestoreConnectionSnapshot(record, true, false);
                    restored.Add(record);
                }
            }
            catch
            {
                for (int index = restored.Count - 1; index >= 0; index--)
                {
                    ConnectionRecord record = restored[index];
                    ApplyDisconnectedPhysicalState(
                        record,
                        record.State == RagdollMuscleConnectionState.Deactivated);
                }
                ReapplyDisconnectedPhysicalPolicies();
                throw;
            }

            for (int index = 0; index < candidates.Count; index++)
            {
                ConnectionRecord record = candidates[index];
                int handleIndex = record.Pair.Handle.Index;
                disconnectedBoneMask[handleIndex] = false;
                severedBoneMask[handleIndex] = false;
                disconnectedCollisionIslandIds[handleIndex] = 0;
                record.State = RagdollMuscleConnectionState.Connected;
                record.Severed = false;
                record.CollisionIslandId = 0;
                record.SnapshotCaptured = false;
                disconnectedMuscleCount--;
            }

            RefreshMappingSuppressionMask();
            ApplyDisconnectedMasksToPhysicalOwners();
            RefreshJointRuntimeConfiguration();
            ReapplyInternalCollisionPolicy();
            for (int index = 0; index < candidates.Count; index++)
            {
                RagdollMuscleConnectionChange change =
                    CreateConnectionChange(candidates[index]);
                NotifyBehaviourMuscleReconnected(change);
                InvokeConnectionChangeSafely(MuscleReconnected, change);
            }
        }

        bool ProcessJointBreak(PendingConnectionOperation operation)
        {
            RagdollBone broken;
            if (!Bindings.TryGetBone(operation.Bone, out broken)) return true;
            if (broken.IsRoot)
            {
                Debug.LogError(
                    "The root muscle joint broke. Hairibar requires a registered root, so RagdollAnimator has been disabled instead of constructing an invalid zero-root registry.",
                    this);
                enabled = false;
                brokenMuscles.Clear();
                RefreshMappingSuppressionMask();
                InvokeJointBreakSafely(
                    JointBroken,
                    new RagdollJointBreakEvent(
                        operation.Bone,
                        operation.BreakForce,
                        new RagdollMuscleChange[0]));
                return true;
            }

            RagdollMuscleChange[] removed;
            string error;
            if (!TryRemoveMuscleRecursiveByBone(
                operation.Bone,
                true,
                true,
                RagdollMuscleRemoveMode.Numb,
                true,
                out removed,
                out error))
            {
                Debug.LogError(
                    "Joint break for '" + operation.Bone
                    + "' could not be committed yet: " + error,
                    this);
                return false;
            }
            for (int index = 0; index < removed.Length; index++)
            {
                brokenMuscles.Remove(removed[index].Bone);
            }
            RefreshMappingSuppressionMask();
            InvokeJointBreakSafely(
                JointBroken,
                new RagdollJointBreakEvent(
                    operation.Bone,
                    operation.BreakForce,
                    removed));
            return true;
        }

        void CaptureConnectionSnapshot(ConnectionRecord record)
        {
            if (record.SnapshotCaptured) return;
            RagdollBone bone = record.Pair.RagdollBone;
            ConfigurableJoint joint = bone.Joint;
            Rigidbody body = bone.Rigidbody;
            record.Snapshot = new ConnectionPhysicalSnapshot
            {
                Parent = bone.Transform.parent,
                SiblingIndex = bone.Transform.GetSiblingIndex(),
                LocalPosition = bone.Transform.localPosition,
                LocalRotation = bone.Transform.localRotation,
                LocalScale = bone.Transform.localScale,
                ActiveSelf = bone.Transform.gameObject.activeSelf,
                IsKinematic = body.isKinematic,
                XMotion = joint ? joint.xMotion : ConfigurableJointMotion.Free,
                YMotion = joint ? joint.yMotion : ConfigurableJointMotion.Free,
                ZMotion = joint ? joint.zMotion : ConfigurableJointMotion.Free,
                AngularXMotion = joint ? joint.angularXMotion : ConfigurableJointMotion.Free,
                AngularYMotion = joint ? joint.angularYMotion : ConfigurableJointMotion.Free,
                AngularZMotion = joint ? joint.angularZMotion : ConfigurableJointMotion.Free,
                ConnectedBody = joint ? joint.connectedBody : null,
                ConnectedAnchor = joint ? joint.connectedAnchor : Vector3.zero,
                AutoConfigureConnectedAnchor = joint && joint.autoConfigureConnectedAnchor,
                SlerpDrive = joint ? joint.slerpDrive : new JointDrive(),
                TargetAngularVelocity = joint ? joint.targetAngularVelocity : Vector3.zero,
                Velocity = body.velocity,
                AngularVelocity = body.angularVelocity,
                WasSleeping = body.IsSleeping()
            };
            record.SnapshotCaptured = true;
        }

        void ApplyDisconnectedPhysicalState(
            ConnectionRecord record,
            bool deactivate)
        {
            RagdollBone bone = record.Pair.RagdollBone;
            ConfigurableJoint joint = bone.Joint;
            Rigidbody body = bone.Rigidbody;
            // Hairibar disables the complete Puppet root in global Disabled mode.
            // Every disconnected body therefore leaves that root, even when it stays
            // jointed to other members of the same Sever island.
            Transform disconnectedParent = Bindings.transform.parent;
            if (bone.Transform.parent != disconnectedParent)
            {
                bone.Transform.SetParent(disconnectedParent, true);
            }
            if (joint)
            {
                if (record.Severed)
                {
                    joint.xMotion = ConfigurableJointMotion.Free;
                    joint.yMotion = ConfigurableJointMotion.Free;
                    joint.zMotion = ConfigurableJointMotion.Free;
                    joint.angularXMotion = ConfigurableJointMotion.Free;
                    joint.angularYMotion = ConfigurableJointMotion.Free;
                    joint.angularZMotion = ConfigurableJointMotion.Free;
                }
                joint.slerpDrive = new JointDrive();
                joint.targetAngularVelocity = Vector3.zero;
            }

            if (!deactivate)
            {
                bool applyMappedVelocity = record.Snapshot.IsKinematic
                    || !record.Snapshot.ActiveSelf;
                if (!bone.Transform.gameObject.activeSelf)
                {
                    bone.Transform.gameObject.SetActive(true);
                }
                body.isKinematic = false;
                if (applyMappedVelocity
                    && ActiveState != RagdollLifecycleState.Frozen)
                {
                    body.velocity = record.Pair.poseLinearVelocity;
                    body.angularVelocity = record.Pair.poseAngularVelocity;
                }
                else
                {
                    body.velocity = record.Snapshot.Velocity;
                    body.angularVelocity = record.Snapshot.AngularVelocity;
                }
                if (record.Snapshot.WasSleeping) body.Sleep();
                else body.WakeUp();
            }
            else
            {
                bone.Transform.gameObject.SetActive(false);
            }
        }

        void RestoreConnectionSnapshot(
            ConnectionRecord record,
            bool snapToTarget,
            bool releaseSnapshot)
        {
            if (!record.SnapshotCaptured) return;
            RagdollBone bone = record.Pair.RagdollBone;
            Rigidbody body = bone.Rigidbody;
            ConfigurableJoint joint = bone.Joint;
            ConnectionPhysicalSnapshot snapshot = record.Snapshot;

            bone.Transform.SetParent(snapshot.Parent, false);
            bone.Transform.localPosition = snapshot.LocalPosition;
            bone.Transform.localRotation = snapshot.LocalRotation;
            bone.Transform.localScale = snapshot.LocalScale;
            bone.Transform.SetSiblingIndex(snapshot.SiblingIndex);
            bone.Transform.gameObject.SetActive(snapshot.ActiveSelf);

            if (joint)
            {
                joint.xMotion = snapshot.XMotion;
                joint.yMotion = snapshot.YMotion;
                joint.zMotion = snapshot.ZMotion;
                joint.angularXMotion = snapshot.AngularXMotion;
                joint.angularYMotion = snapshot.AngularYMotion;
                joint.angularZMotion = snapshot.AngularZMotion;
                joint.connectedBody = snapshot.ConnectedBody;
                joint.connectedAnchor = snapshot.ConnectedAnchor;
                joint.autoConfigureConnectedAnchor =
                    snapshot.AutoConfigureConnectedAnchor;
                joint.slerpDrive = snapshot.SlerpDrive;
                joint.targetAngularVelocity = snapshot.TargetAngularVelocity;
            }

            if (snapToTarget && snapshot.ActiveSelf)
            {
                body.isKinematic = true;
                body.position = record.Pair.currentPose.worldPosition;
                body.rotation = record.Pair.currentPose.worldRotation;
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = ResolveReconnectedKinematic(record.Pair);
                body.WakeUp();
            }
            else
            {
                body.isKinematic = snapshot.IsKinematic;
                body.velocity = snapshot.Velocity;
                body.angularVelocity = snapshot.AngularVelocity;
                if (snapshot.WasSleeping && !body.isKinematic) body.Sleep();
                else if (!body.isKinematic) body.WakeUp();
            }
            if (releaseSnapshot) record.SnapshotCaptured = false;
        }

        bool ResolveReconnectedKinematic(AnimatedPair pair)
        {
            RagdollSimulationModeController mode =
                GetComponent<RagdollSimulationModeController>();
            if (mode && mode.IsInitialized
                && mode.CurrentMode == RagdollSimulationMode.Kinematic)
            {
                return true;
            }
            return pair.RagdollBone.PowerSetting == PowerSetting.Kinematic;
        }

        internal bool IsMuscleDisconnected(AnimatedPair pair)
        {
            return pair != null && IsMuscleDisconnected(pair.Name);
        }

        internal bool IsMuscleDisconnected(BoneName bone)
        {
            if (disconnectedBoneMask == null || connectionRecords == null)
            {
                return false;
            }
            RagdollBoneHandle handle;
            return Bindings.TryGetBoneHandle(bone, out handle)
                && handle.Index >= 0
                && handle.Index < disconnectedBoneMask.Length
                && disconnectedBoneMask[handle.Index];
        }

        internal bool IsMuscleUnavailable(AnimatedPair pair)
        {
            return pair == null
                || brokenMuscles.Contains(pair.Name)
                || IsMuscleDisconnected(pair);
        }


        void SuspendDisconnectedMusclesForLifecycleFreeze()
        {
            if (connectionRecords == null) return;
            for (int index = 0; index < connectionRecords.Length; index++)
            {
                ConnectionRecord record = connectionRecords[index];
                if (record.State != RagdollMuscleConnectionState.Disconnected)
                {
                    continue;
                }
                RagdollBone bone = record.Pair.RagdollBone;
                if (bone.Transform && bone.Transform.gameObject.activeSelf)
                {
                    bone.Transform.gameObject.SetActive(false);
                }
            }
        }

        void ResumeDisconnectedMusclesAfterLifecycleFreeze()
        {
            if (connectionRecords == null) return;
            for (int index = 0; index < connectionRecords.Length; index++)
            {
                ConnectionRecord record = connectionRecords[index];
                if (record.State != RagdollMuscleConnectionState.Disconnected)
                {
                    continue;
                }
                RagdollBone bone = record.Pair.RagdollBone;
                if (!bone.Transform) continue;
                if (!bone.Transform.gameObject.activeSelf)
                {
                    bone.Transform.gameObject.SetActive(true);
                }
                Rigidbody body = bone.Rigidbody;
                body.isKinematic = false;
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }
            ReapplyDisconnectedPhysicalPolicies();
            ReapplyInternalCollisionPolicy();
        }

        void DestroyDisconnectedMusclesForPermanentFreeze()
        {
            if (connectionRecords == null) return;
            HashSet<GameObject> scheduled = new HashSet<GameObject>();
            for (int index = 0; index < connectionRecords.Length; index++)
            {
                if (!disconnectedBoneMask[index]) continue;
                Transform muscle = connectionRecords[index].Pair.RagdollBone.Transform;
                if (!muscle || !scheduled.Add(muscle.gameObject)) continue;
                Destroy(muscle.gameObject);
            }
        }

        void ReapplyDisconnectedPhysicalPolicies()
        {
            if (!HasDisconnectedMuscles) return;
            for (int index = 0; index < connectionRecords.Length; index++)
            {
                if (!disconnectedBoneMask[index]) continue;
                ConnectionRecord record = connectionRecords[index];
                RagdollBone bone = record.Pair.RagdollBone;
                ConfigurableJoint joint = bone.Joint;
                if (joint)
                {
                    if (severedBoneMask[index])
                    {
                        joint.xMotion = ConfigurableJointMotion.Free;
                        joint.yMotion = ConfigurableJointMotion.Free;
                        joint.zMotion = ConfigurableJointMotion.Free;
                        joint.angularXMotion = ConfigurableJointMotion.Free;
                        joint.angularYMotion = ConfigurableJointMotion.Free;
                        joint.angularZMotion = ConfigurableJointMotion.Free;
                    }
                    joint.slerpDrive = new JointDrive();
                    joint.targetAngularVelocity = Vector3.zero;
                }
                if (record.State == RagdollMuscleConnectionState.Disconnected
                    && bone.Transform.gameObject.activeInHierarchy)
                {
                    bone.Rigidbody.isKinematic = false;
                }
            }
        }

        int AllocateDisconnectedCollisionIslandId()
        {
            if (nextDisconnectedCollisionIslandId == int.MaxValue)
            {
                throw new InvalidOperationException(
                    "Disconnected collision island identifiers were exhausted.");
            }
            nextDisconnectedCollisionIslandId++;
            return nextDisconnectedCollisionIslandId;
        }

        void MapDisconnectedMusclesToTarget()
        {
            if (!mapDisconnectedMuscles
                || (!HasDisconnectedMuscles && brokenMuscles.Count == 0))
            {
                return;
            }
            for (int index = 0; index < connectionRecords.Length; index++)
            {
                ConnectionRecord record = connectionRecords[index];
                bool broken = brokenMuscles.Contains(record.Pair.Name);
                if (!disconnectedBoneMask[index] && !broken) continue;
                if (!broken
                    && record.State == RagdollMuscleConnectionState.Deactivated)
                {
                    continue;
                }
                AnimatedPair pair = record.Pair;
                Vector3 position;
                Quaternion rotation;
                pair.GetMappedTargetWorldPose(out position, out rotation);
                pair.TargetBone.SetPositionAndRotation(position, rotation);
                pair.TargetBinding.MapAnimatedTargetChildren();
            }
        }

        bool[] GetMappingSuppressedBoneMask()
        {
            RefreshMappingSuppressionMask();
            return mappingSuppressedBoneMask;
        }

        void RefreshMappingSuppressionMask()
        {
            if (mappingSuppressedBoneMask == null
                || connectionRecords == null)
            {
                return;
            }
            for (int index = 0; index < mappingSuppressedBoneMask.Length; index++)
            {
                mappingSuppressedBoneMask[index] = disconnectedBoneMask[index]
                    || brokenMuscles.Contains(connectionRecords[index].Pair.Name);
            }
        }

        bool[] GetDisconnectedBoneMask()
        {
            return disconnectedBoneMask;
        }

        void ApplyDisconnectedMasksToPhysicalOwners()
        {
            if (internalCollisionController != null)
            {
                internalCollisionController.SetDisconnectedBones(
                    disconnectedBoneMask,
                    disconnectedCollisionIslandIds);
            }
            if (lifecyclePhysicsPolicy != null)
            {
                lifecyclePhysicsPolicy.SetDisconnectedBones(
                    disconnectedBoneMask);
            }
        }

        void InitializeJointBreakBroadcasters()
        {
            List<RagdollJointBreakBroadcaster> broadcasters =
                new List<RagdollJointBreakBroadcaster>();
            for (int index = 0; index < animatedPairs.Length; index++)
            {
                RagdollBone bone = animatedPairs[index].RagdollBone;
                if (!bone.Transform) continue;
                RagdollJointBreakBroadcaster broadcaster =
                    bone.Transform.GetComponent<RagdollJointBreakBroadcaster>();
                if (!broadcaster)
                {
                    broadcaster = bone.Transform.gameObject
                        .AddComponent<RagdollJointBreakBroadcaster>();
                }
                broadcaster.Initialize(this, bone.Name);
                broadcasters.Add(broadcaster);
            }
            jointBreakBroadcasters = broadcasters.ToArray();
        }

        void ShutdownJointBreakBroadcasters()
        {
            if (jointBreakBroadcasters == null) return;
            for (int index = 0; index < jointBreakBroadcasters.Length; index++)
            {
                if (jointBreakBroadcasters[index])
                {
                    jointBreakBroadcasters[index].Release(this);
                }
            }
            jointBreakBroadcasters = null;
        }

        bool TryValidateConnectionRequest(
            RagdollBoneHandle muscle,
            out string error)
        {
            error = null;
            if (connectionRecords == null)
            {
                error = "Muscle connection runtime is not initialized.";
                return false;
            }
            if (!Bindings.Topology.Contains(muscle))
            {
                error = "The supplied muscle handle belongs to another registry generation.";
                return false;
            }
            if (hierarchyTransactionInProgress)
            {
                error = "A hierarchy transaction is already in progress.";
                return false;
            }
            return true;
        }

        void ValidateConnectionHandle(RagdollBoneHandle muscle)
        {
            string error;
            if (!TryValidateConnectionRequest(muscle, out error))
            {
                throw new ArgumentException(error, nameof(muscle));
            }
        }

        void ReplacePendingOperation(PendingConnectionOperation operation)
        {
            for (int index = pendingConnectionOperations.Count - 1;
                index >= 0;
                index--)
            {
                if (pendingConnectionOperations[index].Bone == operation.Bone)
                {
                    pendingConnectionOperations.RemoveAt(index);
                }
            }
            pendingConnectionOperations.Add(operation);
        }

        bool HasPendingOperation(
            BoneName bone,
            PendingConnectionOperationType type)
        {
            for (int index = 0; index < pendingConnectionOperations.Count; index++)
            {
                PendingConnectionOperation operation =
                    pendingConnectionOperations[index];
                if (operation.Bone == bone && operation.Type == type) return true;
            }
            return false;
        }

        RagdollMuscleConnectionChange CreateConnectionChange(
            ConnectionRecord record)
        {
            RagdollBone bone = record.Pair.RagdollBone;
            return new RagdollMuscleConnectionChange(
                bone.Name,
                record.Pair.Handle,
                bone.Joint,
                bone.Rigidbody,
                record.Pair.TargetBone,
                record.Mode,
                record.State);
        }

        void NotifyBehaviourMuscleDisconnected(
            RagdollMuscleConnectionChange change)
        {
            RagdollBehaviourController controller =
                GetComponent<RagdollBehaviourController>();
            if (controller && controller.IsInitialized)
            {
                controller.NotifyMuscleDisconnected(change);
            }
        }

        void NotifyBehaviourMuscleReconnected(
            RagdollMuscleConnectionChange change)
        {
            RagdollBehaviourController controller =
                GetComponent<RagdollBehaviourController>();
            if (controller && controller.IsInitialized)
            {
                controller.NotifyMuscleReconnected(change);
            }
        }

        void InvokeConnectionChangeSafely(
            Action<RagdollMuscleConnectionChange> handlers,
            RagdollMuscleConnectionChange change)
        {
            if (handlers == null) return;
            Delegate[] invocationList = handlers.GetInvocationList();
            for (int index = 0; index < invocationList.Length; index++)
            {
                try
                {
                    ((Action<RagdollMuscleConnectionChange>)
                        invocationList[index])(change);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        void InvokeJointBreakSafely(
            Action<RagdollJointBreakEvent> handlers,
            RagdollJointBreakEvent value)
        {
            if (handlers == null) return;
            Delegate[] invocationList = handlers.GetInvocationList();
            for (int index = 0; index < invocationList.Length; index++)
            {
                try
                {
                    ((Action<RagdollJointBreakEvent>)invocationList[index])(value);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
