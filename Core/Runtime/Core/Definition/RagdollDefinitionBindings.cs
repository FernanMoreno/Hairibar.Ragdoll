using System;
using System.Collections.Generic;
using System.Linq;
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

        public RagdollDefinition Definition => _definition;

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
                return indexedBones ?? Array.Empty<RagdollBone>();
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
                return indexedBones;
            }
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
        #endregion

        #region Initialization Event
        event Action OnBonesCreated;

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
        #endregion

        #region Serialized Data
        [SerializeField] BoneJointBindingsDictionary bindings;
        [SerializeField] RagdollDefinition _definition;
        #endregion

        #region Private State
        Dictionary<BoneName, RagdollBone> bones;
        RagdollBone[] indexedBones;

        Dictionary<BoneName, int> indexByName;
        Dictionary<Rigidbody, int> indexByRigidbody;
        Dictionary<Collider, int> indexByCollider;
        Dictionary<ConfigurableJoint, int> indexByJoint;
        #endregion

        #region Validation
        bool BindingsAreValid
        {
            get
            {
                if (!_definition || bindings == null) return false;
                if (bindings.Count < _definition.BoneCount) return false;
                if (bindings.Any(pair => pair.Value == null)) return false;
                if (bindings.Values.Distinct().Count() != bindings.Values.Count) return false;

                foreach (BoneName boneName in _definition.Bones)
                {
                    if (!bindings.ContainsKey(boneName)) return false;
                }

                return true;
            }
        }

        bool BindingsMatchExistingBones
        {
            get
            {
                if (bones == null || indexedBones == null) return false;

                foreach (BoneName boneName in _definition.Bones)
                {
                    RagdollBone bone;
                    if (!bones.TryGetValue(boneName, out bone) || bone.Joint != GetBindingJoint(boneName))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        ConfigurableJoint GetBindingJoint(BoneName name)
        {
            return bindings[name];
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
            if (!BindingsAreValid)
            {
                ClearRuntimeRegistry();

                if (Application.isPlaying)
                {
                    Debug.LogError("Ragdoll Definition Bindings aren't correctly set up.", this);
                }

                return false;
            }

            int boneCount = _definition.BoneCount;

            bones = new Dictionary<BoneName, RagdollBone>(boneCount);
            indexedBones = new RagdollBone[boneCount];

            indexByName = new Dictionary<BoneName, int>(boneCount);
            indexByRigidbody = new Dictionary<Rigidbody, int>(boneCount);
            indexByCollider = new Dictionary<Collider, int>(boneCount * 2);
            indexByJoint = new Dictionary<ConfigurableJoint, int>(boneCount);

            int index = 0;
            foreach (BoneName boneName in _definition.Bones)
            {
                ConfigurableJoint joint = bindings[boneName];
                Rigidbody rigidbody = joint.GetComponent<Rigidbody>();

                if (rigidbody == null)
                {
                    ClearRuntimeRegistry();

                    if (Application.isPlaying)
                    {
                        Debug.LogError("Every bound ConfigurableJoint must have a Rigidbody on the same GameObject.", joint);
                    }

                    return false;
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
                    if (collider == null) continue;

                    int existingIndex;
                    if (indexByCollider.TryGetValue(collider, out existingIndex) && existingIndex != index)
                    {
                        ClearRuntimeRegistry();

                        if (Application.isPlaying)
                        {
                            Debug.LogError("A Collider cannot belong to more than one ragdoll bone.", collider);
                        }

                        return false;
                    }

                    indexByCollider[collider] = index;
                }

                index++;
            }

            return true;
        }

        void ClearRuntimeRegistry()
        {
            bones = null;
            indexedBones = null;

            indexByName = null;
            indexByRigidbody = null;
            indexByCollider = null;
            indexByJoint = null;
        }

        RagdollBoneHandle CreateHandle(int index)
        {
            return new RagdollBoneHandle(GetInstanceID(), index);
        }

        bool HandleBelongsToThisRagdoll(RagdollBoneHandle handle)
        {
            return handle.RegistryId == GetInstanceID()
                && handle.Index >= 0
                && handle.Index < indexedBones.Length;
        }
        #endregion

        [Serializable]
        class BoneJointBindingsDictionary : SerializableDictionary<BoneName, ConfigurableJoint>
        {
        }
    }
}
