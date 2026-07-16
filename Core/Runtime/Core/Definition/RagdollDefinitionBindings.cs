using System;
using System.Collections.Generic;
using Hairibar.EngineExtensions.Serialization;
using UnityEngine;

#pragma warning disable 649
namespace Hairibar.Ragdoll
{
    /// <summary>
    /// Links a RagdollDefinition's bone names to the actual GameObjects that represent them.
    /// If using this class at Edit Time, use SubscribeToOnBonesCreated for initialization.
    /// </summary>
    [AddComponentMenu("Ragdoll/Ragdoll Definition Bindings")]
    [DisallowMultipleComponent, ExecuteAlways]
    public class RagdollDefinitionBindings : MonoBehaviour
    {
        #region Public API
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Monotonically identifies the current runtime registry. Any hierarchy mutation
        /// invalidates handles produced by the previous generation.
        /// </summary>
        public int RegistryGeneration => registryGeneration;

        public RagdollDefinition Definition => _definition;

        /// <summary>
        /// Precalculated parent, child and depth relationships for the current registry generation.
        /// </summary>
        public RagdollBoneTopology Topology
        {
            get
            {
                ThrowExceptionIfNotInitialized();
                return topology;
            }
        }

        /// <summary>
        /// Number of runtime bones in this ragdoll.
        /// </summary>
        public int BoneCount
        {
            get
            {
                ThrowExceptionIfNotInitialized();
                return indexedBones.Length;
            }
        }

        public RagdollBone Root
        {
            get
            {
                ThrowExceptionIfNotInitialized();

                if (TryGetBone(_definition.Root, out RagdollBone rootBone))
                {
                    return rootBone;
                }

                throw new InvalidOperationException("There is no root bone.");
            }
        }

        /// <summary>
        /// Runtime bones in the same deterministic order as RagdollDefinition.Bones.
        /// </summary>
        public IEnumerable<RagdollBone> Bones
        {
            get
            {
                ThrowExceptionIfNotInitialized();
                return indexedBonesView;
            }
        }

        /// <summary>
        /// Runtime bones in the same deterministic order as RagdollDefinition.Bones.
        /// </summary>
        public IReadOnlyList<RagdollBone> IndexedBones
        {
            get
            {
                ThrowExceptionIfNotInitialized();
                return indexedBonesView;
            }
        }

        public RagdollBone GetBoneAt(int index)
        {
            ThrowExceptionIfNotInitialized();
            ValidateBoneIndex(index);
            return indexedBones[index];
        }

        public RagdollBoneHandle GetHandleAt(int index)
        {
            ThrowExceptionIfNotInitialized();
            ValidateBoneIndex(index);
            return CreateHandle(index);
        }

        public bool TryGetBone(BoneName boneName, out RagdollBone bone)
        {
            ThrowExceptionIfNotInitialized();
            return bones.TryGetValue(boneName, out bone);
        }

        public bool TryGetBone(Rigidbody rigidbody, out RagdollBone bone)
        {
            ThrowExceptionIfNotInitialized();

            int index;
            if (rigidbody != null && indexByRigidbody.TryGetValue(rigidbody, out index))
            {
                bone = indexedBones[index];
                return true;
            }

            bone = null;
            return false;
        }

        public bool TryGetBone(Collider collider, out RagdollBone bone)
        {
            ThrowExceptionIfNotInitialized();

            int index;
            if (collider != null && indexByCollider.TryGetValue(collider, out index))
            {
                bone = indexedBones[index];
                return true;
            }

            bone = null;
            return false;
        }

        /// <summary>
        /// Resolves a collider through its attached Rigidbody, even when the collider itself was not
        /// registered as part of the ragdoll bone during initialization.
        /// </summary>
        public bool TryGetBoneFromAttachedRigidbody(Collider collider, out RagdollBone bone)
        {
            ThrowExceptionIfNotInitialized();

            if (collider != null)
            {
                return TryGetBone(collider.attachedRigidbody, out bone);
            }

            bone = null;
            return false;
        }

        public bool TryGetBone(ConfigurableJoint joint, out RagdollBone bone)
        {
            ThrowExceptionIfNotInitialized();

            int index;
            if (joint != null && indexByJoint.TryGetValue(joint, out index))
            {
                bone = indexedBones[index];
                return true;
            }

            bone = null;
            return false;
        }

        public bool TryGetBone(RagdollBoneHandle handle, out RagdollBone bone)
        {
            ThrowExceptionIfNotInitialized();

            if (HandleBelongsToThisRagdoll(handle))
            {
                bone = indexedBones[handle.Index];
                return true;
            }

            bone = null;
            return false;
        }

        public RagdollBone GetBone(RagdollBoneHandle handle)
        {
            RagdollBone bone;
            if (TryGetBone(handle, out bone))
            {
                return bone;
            }

            throw new ArgumentException("The supplied RagdollBoneHandle does not belong to this ragdoll.", nameof(handle));
        }

        public bool TryGetBoneHandle(BoneName boneName, out RagdollBoneHandle handle)
        {
            ThrowExceptionIfNotInitialized();

            int index;
            if (indexByName.TryGetValue(boneName, out index))
            {
                handle = CreateHandle(index);
                return true;
            }

            handle = RagdollBoneHandle.Invalid;
            return false;
        }

        public bool TryGetBoneHandle(Rigidbody rigidbody, out RagdollBoneHandle handle)
        {
            ThrowExceptionIfNotInitialized();

            int index;
            if (rigidbody != null && indexByRigidbody.TryGetValue(rigidbody, out index))
            {
                handle = CreateHandle(index);
                return true;
            }

            handle = RagdollBoneHandle.Invalid;
            return false;
        }

        public bool TryGetBoneHandle(Collider collider, out RagdollBoneHandle handle)
        {
            ThrowExceptionIfNotInitialized();

            int index;
            if (collider != null && indexByCollider.TryGetValue(collider, out index))
            {
                handle = CreateHandle(index);
                return true;
            }

            handle = RagdollBoneHandle.Invalid;
            return false;
        }

        /// <summary>
        /// Resolves a collider through its attached Rigidbody, even when the collider itself was not
        /// registered as part of the ragdoll bone during initialization.
        /// </summary>
        public bool TryGetBoneHandleFromAttachedRigidbody(Collider collider, out RagdollBoneHandle handle)
        {
            ThrowExceptionIfNotInitialized();

            if (collider != null)
            {
                return TryGetBoneHandle(collider.attachedRigidbody, out handle);
            }

            handle = RagdollBoneHandle.Invalid;
            return false;
        }

        public bool TryGetBoneHandle(ConfigurableJoint joint, out RagdollBoneHandle handle)
        {
            ThrowExceptionIfNotInitialized();

            int index;
            if (joint != null && indexByJoint.TryGetValue(joint, out index))
            {
                handle = CreateHandle(index);
                return true;
            }

            handle = RagdollBoneHandle.Invalid;
            return false;
        }

        public bool TryGetBoundBoneName(ConfigurableJoint joint, out BoneName boneName)
        {
            RagdollBone bone;
            if (TryGetBone(joint, out bone))
            {
                boneName = bone.Name;
                return true;
            }

            boneName = "JointDoesNotBelongToARagdollBone";
            return false;
        }

        internal bool IsDefinitionBoneName(BoneName name)
        {
            if (!_definition) return false;
            foreach (BoneName candidate in _definition.Bones)
            {
                if (candidate == name) return true;
            }
            return false;
        }

        internal RuntimeRegistrySnapshot CaptureRuntimeRegistry()
        {
            return new RuntimeRegistrySnapshot(
                runtimeBindings,
                removedDefinitionBones,
                registryGeneration);
        }

        internal bool TryAddRuntimeBinding(
            BoneName name,
            ConfigurableJoint joint,
            out RagdollBoneHandle handle,
            out string error)
        {
            handle = RagdollBoneHandle.Invalid;
            error = null;
            ThrowExceptionIfNotInitialized();

            if (string.IsNullOrWhiteSpace(name.ToString()))
            {
                error = "A runtime muscle requires a non-empty BoneName.";
                return false;
            }
            if (!joint)
            {
                error = "A runtime muscle requires a ConfigurableJoint.";
                return false;
            }
            if (!joint.GetComponent<Rigidbody>())
            {
                error = "A runtime muscle joint requires a Rigidbody on the same GameObject.";
                return false;
            }
            if (indexByName.ContainsKey(name))
            {
                error = "A registered ragdoll bone named '" + name + "' already exists.";
                return false;
            }
            if (indexByJoint.ContainsKey(joint))
            {
                error = "The supplied ConfigurableJoint already belongs to a registered ragdoll bone.";
                return false;
            }

            RuntimeRegistrySnapshot snapshot = CaptureRuntimeRegistry();
            if (IsDefinitionBoneName(name))
            {
                // Replacing a previously removed definition slot keeps the serialized
                // binding suppressed and appends the runtime override deterministically.
                removedDefinitionBones.Add(name);
            }
            runtimeBindings.Add(new RuntimeBinding(name, joint));

            if (!TryCreateRagdollBones())
            {
                error = lastInitializationError
                    ?? "The runtime ragdoll registry could not be rebuilt.";
                RestoreRuntimeRegistry(snapshot);
                return false;
            }

            return TryGetBoneHandle(name, out handle);
        }

        internal bool TryRemoveRuntimeSubtree(
            ConfigurableJoint joint,
            out RagdollBone[] removedBones,
            out string error)
        {
            removedBones = new RagdollBone[0];
            error = null;
            ThrowExceptionIfNotInitialized();

            RagdollBone root;
            if (!TryGetBone(joint, out root))
            {
                error = "No registered ragdoll bone uses the supplied ConfigurableJoint.";
                return false;
            }
            if (root.IsRoot)
            {
                error = "The root ragdoll bone cannot be removed at runtime.";
                return false;
            }

            RagdollBoneHandle rootHandle;
            TryGetBoneHandle(joint, out rootHandle);
            List<RagdollBone> removed = new List<RagdollBone>();
            for (int index = 0; index < indexedBones.Length; index++)
            {
                RagdollBoneHandle candidate = CreateHandle(index);
                if (candidate == rootHandle
                    || topology.IsAncestorOf(rootHandle, candidate))
                {
                    removed.Add(indexedBones[index]);
                }
            }

            RuntimeRegistrySnapshot snapshot = CaptureRuntimeRegistry();
            for (int index = 0; index < removed.Count; index++)
            {
                BoneName name = removed[index].Name;
                bool removedRuntime = false;
                for (int runtimeIndex = runtimeBindings.Count - 1;
                    runtimeIndex >= 0;
                    runtimeIndex--)
                {
                    if (runtimeBindings[runtimeIndex].Name != name) continue;
                    runtimeBindings.RemoveAt(runtimeIndex);
                    removedRuntime = true;
                }

                if (!removedRuntime && IsDefinitionBoneName(name))
                {
                    removedDefinitionBones.Add(name);
                }
            }

            if (!TryCreateRagdollBones())
            {
                error = lastInitializationError
                    ?? "The runtime ragdoll registry could not be rebuilt.";
                RestoreRuntimeRegistry(snapshot);
                return false;
            }

            removedBones = removed.ToArray();
            return true;
        }

        internal void RestoreRuntimeRegistry(RuntimeRegistrySnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            runtimeBindings.Clear();
            for (int index = 0; index < snapshot.RuntimeBindings.Length; index++)
            {
                runtimeBindings.Add(snapshot.RuntimeBindings[index].Clone());
            }

            removedDefinitionBones.Clear();
            for (int index = 0; index < snapshot.RemovedDefinitionBones.Length; index++)
            {
                removedDefinitionBones.Add(snapshot.RemovedDefinitionBones[index]);
            }

            registryGeneration = PreviousGeneration(snapshot.Generation);
            if (!TryCreateRagdollBones())
            {
                throw new InvalidOperationException(
                    "Failed to restore the previous ragdoll registry: "
                    + lastInitializationError);
            }
        }

        internal void NotifyRuntimeHierarchyChanged()
        {
            if (OnRuntimeHierarchyChanged == null) return;

            Delegate[] subscribers =
                OnRuntimeHierarchyChanged.GetInvocationList();
            for (int index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action)subscribers[index])();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }
        #endregion

        #region Initialization Event
        event Action OnBonesCreated;
        event Action OnRuntimeHierarchyChanged;

        /// <summary>
        /// If the Definition is initialized, the action will be instantly called.
        /// If it isn't yet, it will be called when initialized.
        /// <para>
        /// Only useful for [ExecuteAlways] behaviours. At runtime, either the bones are created before Start(), or they are never created due to invalid settings.
        /// If the definition is changed in the inspector, the event will be called again.
        /// </para>
        /// </summary>
        public void SubscribeToOnBonesCreated(Action action)
        {
            if (IsInitialized)
            {
                action();
            }

            OnBonesCreated += action;
        }

        public void UnsubscribeFromOnBonesCreated(Action action)
        {
            OnBonesCreated -= action;
        }

        /// <summary>
        /// Subscribes to committed runtime registry mutations. Unlike OnBonesCreated,
        /// this event does not imply that authored definition profiles changed.
        /// </summary>
        public void SubscribeToRuntimeHierarchyChanged(Action action)
        {
            OnRuntimeHierarchyChanged += action;
        }

        public void UnsubscribeFromRuntimeHierarchyChanged(Action action)
        {
            OnRuntimeHierarchyChanged -= action;
        }
        #endregion

        #region Serialized Data
        [SerializeField] BoneJointBindingsDictionary bindings;
        [SerializeField] RagdollDefinition _definition;
        #endregion

        #region Private State
        Dictionary<BoneName, RagdollBone> bones;
        RagdollBone[] indexedBones;
        IReadOnlyList<RagdollBone> indexedBonesView;

        Dictionary<BoneName, int> indexByName;
        Dictionary<Rigidbody, int> indexByRigidbody;
        Dictionary<Collider, int> indexByCollider;
        Dictionary<ConfigurableJoint, int> indexByJoint;

        RagdollBoneTopology topology;
        int registryGeneration;
        string lastInitializationError;

        readonly List<RuntimeBinding> runtimeBindings =
            new List<RuntimeBinding>();
        readonly HashSet<BoneName> removedDefinitionBones =
            new HashSet<BoneName>();
        #endregion

        #region Validation
        bool BindingsAreValid
        {
            get
            {
                if (!_definition || bindings == null) return false;
                if (bindings.Count < _definition.BoneCount) return false;

                foreach (BoneName boneName in _definition.Bones)
                {
                    if (!bindings.ContainsKey(boneName)) return false;
                }

                List<RuntimeBinding> active = BuildActiveBindings();
                HashSet<ConfigurableJoint> activeJoints =
                    new HashSet<ConfigurableJoint>();
                for (int index = 0; index < active.Count; index++)
                {
                    ConfigurableJoint joint = active[index].Joint;
                    if (!joint || !activeJoints.Add(joint)) return false;
                }

                return true;
            }
        }

        bool BindingsMatchExistingBones
        {
            get
            {
                if (bones == null || indexedBones == null
                    || indexedBonesView == null || topology == null)
                {
                    return false;
                }

                List<RuntimeBinding> active = BuildActiveBindings();
                if (indexedBones.Length != active.Count) return false;

                for (int index = 0; index < active.Count; index++)
                {
                    RagdollBone bone = indexedBones[index];
                    if (bone.Name != active[index].Name
                        || bone.Joint != active[index].Joint)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        void ThrowExceptionIfNotInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("Attempted to access a non initialized RagdollDefinitionBindings.");
            }
        }

        void OnValidate()
        {
            if (Application.isPlaying) return;

            if (!IsInitialized || !BindingsMatchExistingBones)
            {
                IsInitialized = TryCreateRagdollBones();
                if (IsInitialized) OnBonesCreated?.Invoke();
            }
        }
        #endregion

        #region Initialization
        void OnEnable()
        {
            // Initialize in OnEnable instead of Awake to support fast enter play mode.
            // When not reloading the scene, ExecuteAlways scripts won't have Awake called.
            if (Application.IsPlaying(this) && !_definition)
            {
                enabled = false;
                throw new UnassignedReferenceException("No RagdollDefinition was assigned.");
            }

            if (!IsInitialized)
            {
                IsInitialized = TryCreateRagdollBones();
                if (IsInitialized) OnBonesCreated?.Invoke();
            }
            else
            {
                foreach (RagdollBone bone in Bones)
                {
                    bone.ResetJointAxisOnEnable();
                }
            }
        }

        bool TryCreateRagdollBones()
        {
            lastInitializationError = null;
            if (!BindingsAreValid)
            {
                return FailInitialization(
                    "Ragdoll Definition Bindings aren't correctly set up.",
                    this);
            }

            List<RuntimeBinding> activeBindings = BuildActiveBindings();
            int boneCount = activeBindings.Count;
            int nextGeneration = GetNextGeneration();

            bones = new Dictionary<BoneName, RagdollBone>(boneCount);
            indexedBones = new RagdollBone[boneCount];

            indexByName = new Dictionary<BoneName, int>(boneCount);
            indexByRigidbody = new Dictionary<Rigidbody, int>(boneCount);
            indexByCollider = new Dictionary<Collider, int>(boneCount * 2);
            indexByJoint = new Dictionary<ConfigurableJoint, int>(boneCount);

            for (int index = 0; index < activeBindings.Count; index++)
            {
                BoneName boneName = activeBindings[index].Name;
                ConfigurableJoint joint = activeBindings[index].Joint;
                if (!joint)
                {
                    return FailInitialization(
                        "A runtime ragdoll binding references a destroyed ConfigurableJoint.",
                        this);
                }

                Rigidbody rigidbody = joint.GetComponent<Rigidbody>();
                if (!rigidbody)
                {
                    return FailInitialization(
                        "Every bound ConfigurableJoint must have a Rigidbody on the same GameObject.",
                        joint);
                }

                if (indexByName.ContainsKey(boneName))
                {
                    return FailInitialization(
                        "A BoneName cannot appear more than once in the runtime ragdoll registry.",
                        this);
                }
                if (indexByRigidbody.ContainsKey(rigidbody))
                {
                    return FailInitialization(
                        "A Rigidbody cannot belong to more than one ragdoll bone.",
                        rigidbody);
                }
                if (indexByJoint.ContainsKey(joint))
                {
                    return FailInitialization(
                        "A ConfigurableJoint cannot belong to more than one ragdoll bone.",
                        joint);
                }

                RagdollBone bone = new RagdollBone(
                    boneName,
                    joint.transform,
                    rigidbody,
                    joint,
                    _definition.IsRoot(boneName));

                bones.Add(boneName, bone);
                indexedBones[index] = bone;
                indexByName.Add(boneName, index);
                indexByRigidbody.Add(rigidbody, index);
                indexByJoint.Add(joint, index);

                foreach (Collider collider in bone.Colliders)
                {
                    if (!collider) continue;
                    int existingIndex;
                    if (indexByCollider.TryGetValue(collider, out existingIndex)
                        && existingIndex != index)
                    {
                        return FailInitialization(
                            "A Collider cannot belong to more than one ragdoll bone.",
                            collider);
                    }
                    indexByCollider[collider] = index;
                }
            }

            int[] parentIndices = new int[boneCount];
            for (int childIndex = 0; childIndex < boneCount; childIndex++)
            {
                Rigidbody connectedBody = indexedBones[childIndex].Joint.connectedBody;
                int parentIndex;
                parentIndices[childIndex] = connectedBody != null
                    && indexByRigidbody.TryGetValue(connectedBody, out parentIndex)
                        ? parentIndex
                        : -1;
            }

            RagdollBoneTopology createdTopology;
            string topologyError;
            if (!RagdollBoneTopology.TryCreate(
                GetInstanceID(),
                nextGeneration,
                parentIndices,
                out createdTopology,
                out topologyError))
            {
                return FailInitialization(topologyError, this);
            }

            indexedBonesView = Array.AsReadOnly(indexedBones);
            topology = createdTopology;
            registryGeneration = nextGeneration;
            IsInitialized = true;
            return true;
        }

        List<RuntimeBinding> BuildActiveBindings()
        {
            List<RuntimeBinding> active =
                new List<RuntimeBinding>(_definition.BoneCount + runtimeBindings.Count);
            foreach (BoneName boneName in _definition.Bones)
            {
                if (removedDefinitionBones.Contains(boneName)) continue;
                active.Add(new RuntimeBinding(boneName, bindings[boneName]));
            }
            for (int index = 0; index < runtimeBindings.Count; index++)
            {
                active.Add(runtimeBindings[index]);
            }
            return active;
        }

        void ClearRuntimeRegistry()
        {
            bones = null;
            indexedBones = null;
            indexedBonesView = null;

            indexByName = null;
            indexByRigidbody = null;
            indexByCollider = null;
            indexByJoint = null;

            topology = null;
        }

        RagdollBoneHandle CreateHandle(int index)
        {
            return new RagdollBoneHandle(GetInstanceID(), registryGeneration, index);
        }

        bool HandleBelongsToThisRagdoll(RagdollBoneHandle handle)
        {
            return handle.RegistryId == GetInstanceID()
                && handle.Generation == registryGeneration
                && (uint)handle.Index < (uint)indexedBones.Length;
        }

        int GetNextGeneration()
        {
            int nextGeneration = unchecked(registryGeneration + 1);
            return nextGeneration == 0 ? 1 : nextGeneration;
        }

        static int PreviousGeneration(int generation)
        {
            if (generation == 1) return 0;
            return unchecked(generation - 1);
        }

        void ValidateBoneIndex(int index)
        {
            if ((uint)index >= (uint)indexedBones.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        bool FailInitialization(string message, UnityEngine.Object context)
        {
            lastInitializationError = message;
            IsInitialized = false;
            ClearRuntimeRegistry();

            if (Application.isPlaying)
            {
                Debug.LogError(message, context != null ? context : this);
            }

            return false;
        }
        #endregion

        internal sealed class RuntimeRegistrySnapshot
        {
            internal RuntimeBinding[] RuntimeBindings { get; }
            internal BoneName[] RemovedDefinitionBones { get; }
            internal int Generation { get; }

            internal RuntimeRegistrySnapshot(
                List<RuntimeBinding> runtimeBindings,
                HashSet<BoneName> removedDefinitionBones,
                int generation)
            {
                RuntimeBindings = new RuntimeBinding[runtimeBindings.Count];
                for (int index = 0; index < runtimeBindings.Count; index++)
                {
                    RuntimeBindings[index] = runtimeBindings[index].Clone();
                }
                RemovedDefinitionBones =
                    new BoneName[removedDefinitionBones.Count];
                removedDefinitionBones.CopyTo(RemovedDefinitionBones);
                Generation = generation;
            }
        }

        internal sealed class RuntimeBinding
        {
            internal BoneName Name { get; }
            internal ConfigurableJoint Joint { get; }

            internal RuntimeBinding(BoneName name, ConfigurableJoint joint)
            {
                Name = name;
                Joint = joint;
            }

            internal RuntimeBinding Clone()
            {
                return new RuntimeBinding(Name, Joint);
            }
        }

        [Serializable]
        class BoneJointBindingsDictionary : SerializableDictionary<BoneName, ConfigurableJoint>
        {
        }
    }
}
