using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Standalone physical prop that can be transferred to a permanent
    /// <see cref="RagdollPropMuscle"/> slot.
    ///
    /// The physical root and its colliders follow the Puppet slot. The visual Mesh Root
    /// follows the animated Target slot. Pickup removes the standalone Rigidbody and drop
    /// recreates it from an absolute snapshot, including center of mass and inertia.
    /// </summary>
    [AddComponentMenu("Ragdoll/Props/Ragdoll Prop")]
    [DisallowMultipleComponent]
    public sealed class RagdollProp : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Purely visual hierarchy moved to the animated Target slot while held. It must not contain Colliders, Joints or Rigidbodies.")]
        Transform meshRoot;

        [SerializeField]
        [Tooltip("Standalone Rigidbody on this GameObject. If omitted it is resolved automatically.")]
        Rigidbody standaloneRigidbody;

        [Header("Held Overrides")]
        [SerializeField, Min(0.0001f)]
        [Tooltip("Mass assigned to the permanent PropMuscle Rigidbody while this prop is held. The slot's previous mass is restored exactly on drop or rollback.")]
        float pickedUpMass = 1f;

        [SerializeField]
        [Tooltip("When enabled, the physical hierarchy follows the PropMuscle layer and the visual Mesh Root follows the Target slot layer while held. Original layers are restored on drop.")]
        bool forceLayers = true;

        [SerializeField]
        [Tooltip("Shared PhysicMaterial assigned to non-trigger physical colliders while held. Null preserves the per-pickup baseline.")]
        PhysicMaterial pickedUpMaterial;

        [SerializeField]
        [Tooltip("Shared PhysicMaterial assigned after a committed drop. Null restores the per-pickup baseline exactly.")]
        PhysicMaterial droppedMaterial;

        [SerializeField]
        RagdollPropInternalCollisionSettings internalCollisionIgnores =
            new RagdollPropInternalCollisionSettings();

        RagdollPropMuscle owner;
        Rigidbody bodyPendingDestruction;
        PropHierarchySnapshot hierarchySnapshot;
        RagdollPropRigidbodySnapshot rigidbodySnapshot;
        PropSurfaceSnapshot surfaceSnapshot;
        bool surfaceSnapshotCaptured;
        bool heldForceLayers;
        PhysicMaterial heldPickedUpMaterial;
        Rigidbody heldSlotBody;
        float heldSlotBaselineMass;
        bool heldSlotMassCaptured;
        RagdollPropInternalCollisionSession collisionSession;
        int collisionSessionGeneration = -1;
        bool pickupPrepared;
        bool pickupCommitted;

        bool emergencyRestorePending;
        bool emergencyRestoreOriginalPose;
        RagdollPropReleaseState emergencyReleaseState;
        string emergencyRestoreError;
        IRagdollPropMuscleRuntime emergencyCleanupRuntime;
        ConfigurableJoint emergencyCleanupJoint;
        bool emergencySlotCleanupPending;

        public Transform MeshRoot => meshRoot;
        public float PickedUpMass
        {
            get => pickedUpMass;
            set => pickedUpMass = SanitizeMass(value);
        }
        public bool ForceLayers
        {
            get => forceLayers;
            set => forceLayers = value;
        }
        public PhysicMaterial PickedUpMaterial
        {
            get => pickedUpMaterial;
            set => pickedUpMaterial = value;
        }
        public PhysicMaterial DroppedMaterial
        {
            get => droppedMaterial;
            set => droppedMaterial = value;
        }
        public RagdollPropInternalCollisionSettings InternalCollisionIgnores =>
            internalCollisionIgnores;
        public int ActiveInternalCollisionIgnorePairCount =>
            collisionSession != null ? collisionSession.PairCount : 0;
        public bool IsCollisionRestorePending => collisionSession != null
            && collisionSession.ReleaseRequested
            && !collisionSession.IsReleased;
        public Rigidbody StandaloneRigidbody => standaloneRigidbody
            ? standaloneRigidbody
            : GetComponent<Rigidbody>();
        public RagdollPropMuscle CurrentMuscle => owner;
        public bool IsHeld => pickupPrepared && pickupCommitted && owner;
        public bool IsReserved => pickupPrepared;
        public bool IsPickupPrepared => pickupPrepared;
        public bool IsPickupCommitted => pickupCommitted;
        public bool IsEmergencyRestorePending => emergencyRestorePending;
        public bool IsEmergencySlotCleanupPending => emergencySlotCleanupPending;
        public bool IsStandaloneBodyRemoved => pickupPrepared
            && !bodyPendingDestruction
            && !GetComponent<Rigidbody>();

        void Reset()
        {
            standaloneRigidbody = GetComponent<Rigidbody>();
            if (!meshRoot && transform.childCount > 0)
            {
                meshRoot = transform.GetChild(0);
            }
        }

        void OnValidate()
        {
            pickedUpMass = SanitizeMass(pickedUpMass);
            if (internalCollisionIgnores == null)
            {
                internalCollisionIgnores =
                    new RagdollPropInternalCollisionSettings();
            }
            internalCollisionIgnores.Normalize();
            if (!pickupPrepared && !standaloneRigidbody)
            {
                standaloneRigidbody = GetComponent<Rigidbody>();
            }
        }

        void Update()
        {
            ProcessEmergencyOperations();
        }

        void ProcessEmergencyOperations()
        {
            AdvanceCollisionSessionRestore();
            if (emergencyRestorePending)
            {
                bool pending;
                string error;
                if (TryRestoreStandaloneCore(
                    null,
                    emergencyReleaseState,
                    emergencyRestoreOriginalPose,
                    false,
                    out pending,
                    out error))
                {
                    emergencyRestorePending = false;
                    emergencyRestoreError = null;
                }
                else if (!pending && !string.IsNullOrEmpty(error)
                    && emergencyRestoreError != error)
                {
                    emergencyRestoreError = error;
                    Debug.LogError(
                        "Emergency standalone restoration for prop '" + name
                        + "' failed and will be retried: " + error,
                        this);
                }
            }

            AdvanceEmergencySlotCleanup();
        }

        /// <summary>
        /// Validates the structural contract required by the PuppetMaster-style prop
        /// transfer: one root Rigidbody, a separate visual Mesh Root and no physical
        /// components in that visual hierarchy.
        /// </summary>
        public bool TryValidateStandaloneConfiguration(out string error)
        {
            error = null;
            if (!meshRoot)
            {
                error = "RagdollProp requires a Mesh Root.";
                return false;
            }
            if (meshRoot == transform || !meshRoot.IsChildOf(transform))
            {
                error = "The Mesh Root must be a strict child of the prop root.";
                return false;
            }

            if (!gameObject.activeInHierarchy)
            {
                error = "A standalone prop must be active in the hierarchy before pickup.";
                return false;
            }

            Rigidbody body = StandaloneRigidbody;
            if (!body)
            {
                error = "A standalone Rigidbody is required on the prop root.";
                return false;
            }
            if (body.transform != transform)
            {
                error = "The standalone Rigidbody must be on the RagdollProp GameObject.";
                return false;
            }

            Rigidbody[] bodies = GetComponentsInChildren<Rigidbody>(true);
            if (bodies.Length != 1 || bodies[0] != body)
            {
                error = "A RagdollProp must contain exactly one Rigidbody, on its root.";
                return false;
            }

            if (meshRoot.GetComponentsInChildren<Rigidbody>(true).Length > 0)
            {
                error = "The Mesh Root hierarchy must not contain Rigidbodies.";
                return false;
            }
            if (meshRoot.GetComponentsInChildren<Collider>(true).Length > 0)
            {
                error = "The Mesh Root hierarchy must be visual-only and must not contain Colliders.";
                return false;
            }
            if (meshRoot.GetComponentsInChildren<Joint>(true).Length > 0)
            {
                error = "The Mesh Root hierarchy must not contain Joints.";
                return false;
            }
            if (GetComponentsInChildren<Joint>(true).Length > 0)
            {
                error = "Standalone prop Joint hierarchies are not supported by Props I.";
                return false;
            }
            if (!IsFinite(pickedUpMass) || pickedUpMass <= 0f)
            {
                error = "Picked Up Mass must be finite and greater than zero.";
                return false;
            }
            if (internalCollisionIgnores == null)
            {
                error = "Internal Collision Ignores settings cannot be null.";
                return false;
            }
            internalCollisionIgnores.Normalize();
            if (IsCollisionRestorePending)
            {
                error = "The prop is still restoring internal-collision baselines from its previous owner.";
                return false;
            }
            return true;
        }

        internal bool CanBePickedUpBy(
            RagdollPropMuscle muscle,
            out string error)
        {
            error = null;
            if (!muscle)
            {
                error = "A live RagdollPropMuscle is required.";
                return false;
            }
            if (pickupPrepared)
            {
                if (owner == muscle) return true;
                error = "The prop is already reserved by another RagdollPropMuscle.";
                return false;
            }
            return TryValidateStandaloneConfiguration(out error);
        }

        internal bool TryPreparePickup(
            RagdollPropMuscle muscle,
            Transform physicalSlot,
            Transform targetSlot,
            out string error)
        {
            error = null;
            if (!CanBePickedUpBy(muscle, out error)) return false;
            if (!physicalSlot || !targetSlot)
            {
                error = "Both physical and Target prop slots are required.";
                return false;
            }
            if (pickupPrepared)
            {
                if (owner == muscle) return true;
                error = "The prop already has a pickup transaction in progress.";
                return false;
            }
            if (physicalSlot == transform || physicalSlot.IsChildOf(transform))
            {
                error = "The physical slot cannot be the prop root or its descendant.";
                return false;
            }
            if (targetSlot == meshRoot || targetSlot.IsChildOf(meshRoot))
            {
                error = "The Target slot cannot be the Mesh Root or its descendant.";
                return false;
            }

            Rigidbody body = StandaloneRigidbody;
            hierarchySnapshot = PropHierarchySnapshot.Capture(transform, meshRoot);
            rigidbodySnapshot = RagdollPropRigidbodySnapshot.Capture(body);
            surfaceSnapshot = PropSurfaceSnapshot.Capture(transform);
            surfaceSnapshotCaptured = true;
            heldForceLayers = forceLayers;
            heldPickedUpMaterial = pickedUpMaterial;
            owner = muscle;
            pickupPrepared = true;
            pickupCommitted = false;
            emergencyRestorePending = false;
            emergencyRestoreError = null;

            try
            {
                if (!gameObject.activeSelf) gameObject.SetActive(true);

                MoveToHeldHierarchy(physicalSlot, targetSlot);
                ApplyHeldSurfaceOverrides(physicalSlot, targetSlot);

                body.detectCollisions = false;
                body.isKinematic = true;
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                standaloneRigidbody = null;
                bodyPendingDestruction = body;
                DestroyBody(body);
                return true;
            }
            catch (Exception exception)
            {
                error = "The prop pickup transaction failed: " + exception.Message;
                try
                {
                    RequestEmergencyStandaloneRestore(
                        muscle,
                        rigidbodySnapshot.ToReleaseState(
                            hierarchySnapshot.RootWorldPosition,
                            hierarchySnapshot.RootWorldRotation),
                        true);
                }
                catch (Exception rollbackException)
                {
                    Debug.LogException(rollbackException, this);
                    error += " Rollback also failed: " + rollbackException.Message;
                }
                return false;
            }
        }

        internal void RefreshPendingBodyDestruction()
        {
            if (bodyPendingDestruction) return;
            bodyPendingDestruction = null;
        }

        internal RagdollPropReleaseState CaptureReleaseState(
            Rigidbody slotBody)
        {
            Vector3 velocity = slotBody ? slotBody.velocity : Vector3.zero;
            Vector3 angularVelocity = slotBody
                ? slotBody.angularVelocity
                : Vector3.zero;
            bool sleeping = slotBody && slotBody.IsSleeping();
            return new RagdollPropReleaseState(
                transform.position,
                transform.rotation,
                velocity,
                angularVelocity,
                sleeping);
        }

        internal bool TryCommitPickup(
            RagdollPropMuscle muscle,
            Rigidbody slotBody,
            RagdollAnimator animator,
            RagdollBoneHandle slotHandle,
            out string error)
        {
            error = null;
            if (!pickupPrepared || owner != muscle)
            {
                error = "The prop pickup was committed by a slot that does not own it.";
                return false;
            }
            if (!slotBody)
            {
                error = "The PropMuscle requires a live Rigidbody before committing pickup.";
                return false;
            }

            RagdollPropInternalCollisionSession createdSession;
            if (!RagdollPropInternalCollisionSession.TryCreate(
                this,
                animator,
                slotHandle,
                internalCollisionIgnores,
                out createdSession,
                out error))
            {
                return false;
            }

            try
            {
                collisionSession = createdSession;
                collisionSessionGeneration = animator && animator.Bindings
                    ? animator.Bindings.RegistryGeneration
                    : -1;
                heldSlotBody = slotBody;
                heldSlotBaselineMass = slotBody.mass;
                heldSlotMassCaptured = true;
                slotBody.mass = SanitizeMass(pickedUpMass);
                pickupCommitted = true;
                return true;
            }
            catch (Exception exception)
            {
                RestoreHeldSlotMass();
                if (collisionSession != null) collisionSession.RequestRelease();
                error = "Held prop overrides could not be committed: "
                    + exception.Message;
                return false;
            }
        }

        internal bool TryReapplyHeldCollisionIgnores(
            RagdollAnimator animator,
            RagdollBoneHandle slotHandle,
            out string error)
        {
            error = null;
            if (!pickupCommitted || collisionSession == null) return true;

            int generation = animator && animator.Bindings
                ? animator.Bindings.RegistryGeneration
                : -1;
            if (generation != collisionSessionGeneration
                && internalCollisionIgnores != null
                && internalCollisionIgnores.HasRules)
            {
                collisionSession.RequestRelease();
                if (!collisionSession.TryRestoreBaselines())
                {
                    error = "Waiting to restore the previous prop collision generation.";
                    return false;
                }

                RagdollPropInternalCollisionSession rebuilt;
                if (!RagdollPropInternalCollisionSession.TryCreate(
                    this,
                    animator,
                    slotHandle,
                    internalCollisionIgnores,
                    out rebuilt,
                    out error))
                {
                    collisionSession.ResumeForcedIgnores();
                    return false;
                }
                collisionSession = rebuilt;
                collisionSessionGeneration = generation;
            }

            collisionSession.ReapplyForcedIgnores();
            return true;
        }

        internal bool TryCancelPreparedPickup(
            RagdollPropMuscle muscle,
            out bool pending,
            out string error)
        {
            RagdollPropReleaseState original = rigidbodySnapshot.ToReleaseState(
                hierarchySnapshot.RootWorldPosition,
                hierarchySnapshot.RootWorldRotation);
            return TryRestoreStandaloneCore(
                muscle,
                original,
                true,
                true,
                out pending,
                out error);
        }

        internal bool TryCompleteDrop(
            RagdollPropMuscle muscle,
            RagdollPropReleaseState release,
            out bool pending,
            out string error)
        {
            return TryRestoreStandaloneCore(
                muscle,
                release,
                false,
                true,
                out pending,
                out error);
        }

        internal bool TryRecoverStandalone(
            RagdollPropMuscle muscle,
            RagdollPropReleaseState release,
            out bool pending,
            out string error)
        {
            bool originalPose = !pickupCommitted;
            if (originalPose)
            {
                release = rigidbodySnapshot.ToReleaseState(
                    hierarchySnapshot.RootWorldPosition,
                    hierarchySnapshot.RootWorldRotation);
            }
            return TryRestoreStandaloneCore(
                muscle,
                release,
                originalPose,
                false,
                out pending,
                out error);
        }

        internal void RequestEmergencyStandaloneRestore(
            RagdollPropMuscle muscle,
            RagdollPropReleaseState release,
            bool restoreOriginalPose)
        {
            if (!pickupPrepared || owner != muscle) return;
            emergencyRestorePending = true;
            emergencyRestoreOriginalPose = restoreOriginalPose;
            emergencyCleanupRuntime = muscle.RuntimeForCleanup;
            emergencyCleanupJoint = muscle.Joint;
            emergencySlotCleanupPending = emergencyCleanupRuntime != null
                && emergencyCleanupJoint;
            emergencyReleaseState = restoreOriginalPose
                ? rigidbodySnapshot.ToReleaseState(
                    hierarchySnapshot.RootWorldPosition,
                    hierarchySnapshot.RootWorldRotation)
                : release;
            emergencyRestoreError = null;

            // The physical slot may be inactive (empty/Disabled). Move the prop back to
            // its standalone hierarchy immediately so this component remains active and
            // can finish recreation after Unity's deferred Rigidbody destruction.
            try
            {
                RestoreStandaloneHierarchy(
                    emergencyReleaseState,
                    emergencyRestoreOriginalPose);
                gameObject.SetActive(true);
            }
            catch (Exception exception)
            {
                emergencyRestoreError = exception.Message;
                Debug.LogException(exception, this);
            }

            bool pending;
            string ignored;
            TryRestoreStandaloneCore(
                null,
                emergencyReleaseState,
                emergencyRestoreOriginalPose,
                false,
                out pending,
                out ignored);
            AdvanceEmergencySlotCleanup();
        }

        void AdvanceEmergencySlotCleanup()
        {
            if (!emergencySlotCleanupPending) return;
            if (emergencyCleanupRuntime == null || !emergencyCleanupJoint)
            {
                ClearEmergencySlotCleanup();
                return;
            }

            try
            {
                RagdollBoneHandle cleanupHandle;
                if (!emergencyCleanupRuntime.TryResolveSlot(
                    emergencyCleanupJoint,
                    out cleanupHandle))
                {
                    ClearEmergencySlotCleanup();
                    return;
                }

                RagdollMuscleConnectionState connection =
                    emergencyCleanupRuntime.GetConnectionState(cleanupHandle);
                if (connection == RagdollMuscleConnectionState.Deactivated
                    && !emergencyCleanupRuntime.IsReconnecting(cleanupHandle))
                {
                    ClearEmergencySlotCleanup();
                    return;
                }

                string ignored;
                if (connection == RagdollMuscleConnectionState.Disconnected
                    || (connection == RagdollMuscleConnectionState.Deactivated
                        && emergencyCleanupRuntime.IsReconnecting(cleanupHandle)))
                {
                    if (!emergencyCleanupRuntime.IsReconnecting(cleanupHandle))
                    {
                        emergencyCleanupRuntime.TryReconnect(
                            cleanupHandle,
                            out ignored);
                    }
                    return;
                }

                if (connection == RagdollMuscleConnectionState.Connected
                    && !emergencyCleanupRuntime.IsDisconnecting(cleanupHandle))
                {
                    emergencyCleanupRuntime.TryDisconnect(
                        cleanupHandle,
                        true,
                        out ignored);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        void ClearEmergencySlotCleanup()
        {
            emergencySlotCleanupPending = false;
            emergencyCleanupRuntime = null;
            emergencyCleanupJoint = null;
        }

        bool TryRestoreStandaloneCore(
            RagdollPropMuscle muscle,
            RagdollPropReleaseState release,
            bool restoreOriginalPose,
            bool allowHeldRollback,
            out bool pending,
            out string error)
        {
            pending = false;
            error = null;
            if (!pickupPrepared) return true;
            if (muscle && owner != muscle)
            {
                error = "The prop is not owned by the supplied RagdollPropMuscle.";
                return false;
            }

            RestoreHeldSlotMass();
            if (collisionSession != null) collisionSession.RequestRelease();
            RefreshPendingBodyDestruction();
            if (bodyPendingDestruction)
            {
                pending = true;
                return false;
            }

            Rigidbody createdBody = null;
            bool createdThisAttempt = false;
            try
            {
                RestoreStandaloneHierarchy(release, restoreOriginalPose);

                createdBody = GetComponent<Rigidbody>();
                if (!createdBody)
                {
                    createdBody = gameObject.AddComponent<Rigidbody>();
                    createdThisAttempt = true;
                }
                rigidbodySnapshot.Apply(
                    createdBody,
                    release.Velocity,
                    release.AngularVelocity,
                    release.WasSleeping);
                standaloneRigidbody = createdBody;
                gameObject.SetActive(hierarchySnapshot.RootActiveSelf);
                RestoreStandaloneSurface(restoreOriginalPose);
                AdvanceCollisionSessionRestore();

                pickupPrepared = false;
                pickupCommitted = false;
                owner = null;
                bodyPendingDestruction = null;
                emergencyRestorePending = false;
                emergencyRestoreError = null;
                surfaceSnapshotCaptured = false;
                heldForceLayers = false;
                heldPickedUpMaterial = null;
                return true;
            }
            catch (Exception exception)
            {
                error = "The prop standalone restoration failed: "
                    + exception.Message;
                if (createdThisAttempt && createdBody)
                {
                    createdBody.detectCollisions = false;
                    createdBody.isKinematic = true;
                    standaloneRigidbody = null;
                    bodyPendingDestruction = createdBody;
                    DestroyBody(createdBody);
                }

                if (allowHeldRollback
                    && muscle
                    && muscle.Joint
                    && muscle.TargetSlot)
                {
                    try
                    {
                        MoveToHeldHierarchy(
                            muscle.Joint.transform,
                            muscle.TargetSlot);
                        ApplyHeldSurfaceOverrides(
                            muscle.Joint.transform,
                            muscle.TargetSlot);
                        ReapplyHeldSlotMass(muscle.Joint.GetComponent<Rigidbody>());
                        if (collisionSession != null)
                        {
                            collisionSession.ResumeForcedIgnores();
                        }
                        gameObject.SetActive(true);
                    }
                    catch (Exception rollbackException)
                    {
                        Debug.LogException(rollbackException, this);
                        error += " Held-state rollback also failed: "
                            + rollbackException.Message;
                    }
                }
                else
                {
                    emergencyRestorePending = true;
                    emergencyRestoreOriginalPose = restoreOriginalPose;
                    emergencyReleaseState = release;
                }
                return false;
            }
        }

        internal Collider[] GetPhysicalColliders()
        {
            Collider[] all = GetComponentsInChildren<Collider>(true);
            if (!meshRoot) return all;
            System.Collections.Generic.List<Collider> physical =
                new System.Collections.Generic.List<Collider>(all.Length);
            for (int index = 0; index < all.Length; index++)
            {
                Collider collider = all[index];
                if (!collider || collider.transform == meshRoot
                    || collider.transform.IsChildOf(meshRoot))
                {
                    continue;
                }
                physical.Add(collider);
            }
            return physical.ToArray();
        }

        void ApplyHeldSurfaceOverrides(
            Transform physicalSlot,
            Transform targetSlot)
        {
            if (!surfaceSnapshotCaptured) return;
            if (heldForceLayers)
            {
                surfaceSnapshot.ApplyHeldLayers(
                    meshRoot,
                    physicalSlot.gameObject.layer,
                    targetSlot.gameObject.layer);
            }
            if (heldPickedUpMaterial)
            {
                surfaceSnapshot.ApplyMaterial(heldPickedUpMaterial);
            }
        }

        void RestoreStandaloneSurface(bool restoreOriginalPose)
        {
            if (!surfaceSnapshotCaptured) return;
            if (heldForceLayers) surfaceSnapshot.RestoreLayers();
            if (restoreOriginalPose || !droppedMaterial)
            {
                surfaceSnapshot.RestoreMaterials();
            }
            else
            {
                surfaceSnapshot.ApplyMaterial(droppedMaterial);
            }
        }

        void RestoreHeldSlotMass()
        {
            if (heldSlotMassCaptured && heldSlotBody)
            {
                heldSlotBody.mass = heldSlotBaselineMass;
            }
            heldSlotBody = null;
            heldSlotBaselineMass = 0f;
            heldSlotMassCaptured = false;
        }

        void ReapplyHeldSlotMass(Rigidbody slotBody)
        {
            if (!pickupCommitted || !slotBody) return;
            if (!heldSlotMassCaptured)
            {
                heldSlotBody = slotBody;
                heldSlotBaselineMass = slotBody.mass;
                heldSlotMassCaptured = true;
            }
            slotBody.mass = SanitizeMass(pickedUpMass);
        }

        void AdvanceCollisionSessionRestore()
        {
            if (collisionSession == null || !collisionSession.ReleaseRequested) return;
            if (collisionSession.TryRestoreBaselines())
            {
                collisionSession = null;
                collisionSessionGeneration = -1;
            }
        }

        static float SanitizeMass(float value)
        {
            return IsFinite(value) && value > 0f ? value : 1f;
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        void MoveToHeldHierarchy(
            Transform physicalSlot,
            Transform targetSlot)
        {
            transform.SetParent(physicalSlot, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            meshRoot.SetParent(targetSlot, false);
            meshRoot.localPosition = hierarchySnapshot.MeshLocalPosition;
            meshRoot.localRotation = hierarchySnapshot.MeshLocalRotation;
            meshRoot.localScale = hierarchySnapshot.MeshLocalScale;
        }

        void RestoreStandaloneHierarchy(
            RagdollPropReleaseState release,
            bool restoreOriginalPose)
        {
            RestoreMeshHierarchy();
            transform.SetParent(hierarchySnapshot.RootParent, false);
            transform.localScale = hierarchySnapshot.RootLocalScale;
            if (restoreOriginalPose)
            {
                transform.localPosition = hierarchySnapshot.RootLocalPosition;
                transform.localRotation = hierarchySnapshot.RootLocalRotation;
            }
            else
            {
                transform.SetPositionAndRotation(
                    release.WorldPosition,
                    release.WorldRotation);
            }
            RestoreSiblingIndex(
                transform,
                hierarchySnapshot.RootParent,
                hierarchySnapshot.RootSiblingIndex);
        }

        void RestoreMeshHierarchy()
        {
            if (!meshRoot) return;
            Transform parent = hierarchySnapshot.MeshParent
                ? hierarchySnapshot.MeshParent
                : transform;
            meshRoot.SetParent(parent, false);
            meshRoot.localPosition = hierarchySnapshot.MeshLocalPosition;
            meshRoot.localRotation = hierarchySnapshot.MeshLocalRotation;
            meshRoot.localScale = hierarchySnapshot.MeshLocalScale;
            RestoreSiblingIndex(
                meshRoot,
                parent,
                hierarchySnapshot.MeshSiblingIndex);
        }

        static void RestoreSiblingIndex(
            Transform value,
            Transform expectedParent,
            int siblingIndex)
        {
            if (!value || value.parent != expectedParent) return;
            int maximum = value.parent
                ? value.parent.childCount - 1
                : 0;
            value.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, maximum));
        }

        static void DestroyBody(Rigidbody body)
        {
            if (!body) return;
            if (Application.isPlaying) Destroy(body);
            else DestroyImmediate(body);
        }

        internal void CompletePendingBodyDestructionForTesting()
        {
            Rigidbody live = GetComponent<Rigidbody>();
            if (!ReferenceEquals(live, null))
            {
                UnityEngine.Object.DestroyImmediate(live);
            }
            bodyPendingDestruction = null;
            standaloneRigidbody = null;
        }

        internal void ProcessEmergencyForTesting()
        {
            ProcessEmergencyOperations();
        }

        [Serializable]
        struct PropSurfaceSnapshot
        {
            internal GameObject[] Objects;
            internal int[] Layers;
            internal Collider[] Colliders;
            internal PhysicMaterial[] Materials;

            internal static PropSurfaceSnapshot Capture(Transform root)
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                GameObject[] objects = new GameObject[transforms.Length];
                int[] layers = new int[transforms.Length];
                for (int index = 0; index < transforms.Length; index++)
                {
                    objects[index] = transforms[index].gameObject;
                    layers[index] = transforms[index].gameObject.layer;
                }

                Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
                PhysicMaterial[] materials = new PhysicMaterial[colliders.Length];
                for (int index = 0; index < colliders.Length; index++)
                {
                    materials[index] = colliders[index]
                        ? colliders[index].sharedMaterial
                        : null;
                }
                return new PropSurfaceSnapshot
                {
                    Objects = objects,
                    Layers = layers,
                    Colliders = colliders,
                    Materials = materials
                };
            }

            internal void ApplyHeldLayers(
                Transform mesh,
                int physicalLayer,
                int targetLayer)
            {
                for (int index = 0; index < Objects.Length; index++)
                {
                    GameObject value = Objects[index];
                    if (!value) continue;
                    Transform valueTransform = value.transform;
                    bool visual = mesh && (valueTransform == mesh
                        || valueTransform.IsChildOf(mesh));
                    value.layer = visual ? targetLayer : physicalLayer;
                }
            }

            internal void RestoreLayers()
            {
                for (int index = 0; index < Objects.Length; index++)
                {
                    if (Objects[index]) Objects[index].layer = Layers[index];
                }
            }

            internal void ApplyMaterial(PhysicMaterial material)
            {
                if (!material) return;
                for (int index = 0; index < Colliders.Length; index++)
                {
                    Collider collider = Colliders[index];
                    if (collider && !collider.isTrigger)
                    {
                        collider.sharedMaterial = material;
                    }
                }
            }

            internal void RestoreMaterials()
            {
                for (int index = 0; index < Colliders.Length; index++)
                {
                    if (Colliders[index])
                    {
                        Colliders[index].sharedMaterial = Materials[index];
                    }
                }
            }
        }

        [Serializable]
        struct PropHierarchySnapshot
        {
            internal Transform RootParent;
            internal int RootSiblingIndex;
            internal Vector3 RootLocalPosition;
            internal Quaternion RootLocalRotation;
            internal Vector3 RootLocalScale;
            internal Vector3 RootWorldPosition;
            internal Quaternion RootWorldRotation;
            internal bool RootActiveSelf;
            internal Transform MeshParent;
            internal int MeshSiblingIndex;
            internal Vector3 MeshLocalPosition;
            internal Quaternion MeshLocalRotation;
            internal Vector3 MeshLocalScale;

            internal static PropHierarchySnapshot Capture(
                Transform root,
                Transform mesh)
            {
                return new PropHierarchySnapshot
                {
                    RootParent = root.parent,
                    RootSiblingIndex = root.GetSiblingIndex(),
                    RootLocalPosition = root.localPosition,
                    RootLocalRotation = root.localRotation,
                    RootLocalScale = root.localScale,
                    RootWorldPosition = root.position,
                    RootWorldRotation = root.rotation,
                    RootActiveSelf = root.gameObject.activeSelf,
                    MeshParent = mesh.parent,
                    MeshSiblingIndex = mesh.GetSiblingIndex(),
                    MeshLocalPosition = mesh.localPosition,
                    MeshLocalRotation = mesh.localRotation,
                    MeshLocalScale = mesh.localScale
                };
            }
        }
    }

    /// <summary>World-space pose and kinematics inherited when a held prop is released.</summary>
    public struct RagdollPropReleaseState
    {
        public Vector3 WorldPosition { get; }
        public Quaternion WorldRotation { get; }
        public Vector3 Velocity { get; }
        public Vector3 AngularVelocity { get; }
        public bool WasSleeping { get; }

        public RagdollPropReleaseState(
            Vector3 worldPosition,
            Quaternion worldRotation,
            Vector3 velocity,
            Vector3 angularVelocity,
            bool wasSleeping)
        {
            WorldPosition = worldPosition;
            WorldRotation = worldRotation;
            Velocity = velocity;
            AngularVelocity = angularVelocity;
            WasSleeping = wasSleeping;
        }
    }

    /// <summary>
    /// Absolute standalone Rigidbody configuration. Values are restored, never multiplied,
    /// preventing the cumulative inertia-tensor defect documented for PropMuscle pickups.
    /// </summary>
    internal struct RagdollPropRigidbodySnapshot
    {
        internal float Mass;
        internal float Drag;
        internal float AngularDrag;
        internal bool UseGravity;
        internal bool IsKinematic;
        internal RigidbodyInterpolation Interpolation;
        internal CollisionDetectionMode CollisionDetectionMode;
        internal RigidbodyConstraints Constraints;
        internal bool DetectCollisions;
        internal Vector3 CenterOfMass;
        internal Vector3 InertiaTensor;
        internal Quaternion InertiaTensorRotation;
        internal float MaxAngularVelocity;
        internal float MaxDepenetrationVelocity;
        internal float SleepThreshold;
        internal int SolverIterations;
        internal int SolverVelocityIterations;
        internal Vector3 Velocity;
        internal Vector3 AngularVelocity;
        internal bool WasSleeping;

        internal static RagdollPropRigidbodySnapshot Capture(Rigidbody body)
        {
            if (!body) throw new ArgumentNullException(nameof(body));
            return new RagdollPropRigidbodySnapshot
            {
                Mass = body.mass,
                Drag = body.drag,
                AngularDrag = body.angularDrag,
                UseGravity = body.useGravity,
                IsKinematic = body.isKinematic,
                Interpolation = body.interpolation,
                CollisionDetectionMode = body.collisionDetectionMode,
                Constraints = body.constraints,
                DetectCollisions = body.detectCollisions,
                CenterOfMass = body.centerOfMass,
                InertiaTensor = body.inertiaTensor,
                InertiaTensorRotation = body.inertiaTensorRotation,
                MaxAngularVelocity = body.maxAngularVelocity,
                MaxDepenetrationVelocity = body.maxDepenetrationVelocity,
                SleepThreshold = body.sleepThreshold,
                SolverIterations = body.solverIterations,
                SolverVelocityIterations = body.solverVelocityIterations,
                Velocity = body.velocity,
                AngularVelocity = body.angularVelocity,
                WasSleeping = body.IsSleeping()
            };
        }

        internal RagdollPropReleaseState ToReleaseState(
            Vector3 worldPosition,
            Quaternion worldRotation)
        {
            return new RagdollPropReleaseState(
                worldPosition,
                worldRotation,
                Velocity,
                AngularVelocity,
                WasSleeping);
        }

        internal void Apply(
            Rigidbody body,
            Vector3 velocity,
            Vector3 angularVelocity,
            bool sleeping)
        {
            if (!body) throw new ArgumentNullException(nameof(body));

            // Suppress transient simulation while reconstructing the body.
            body.detectCollisions = false;
            body.isKinematic = true;

            body.mass = Mass;
            body.drag = Drag;
            body.angularDrag = AngularDrag;
            body.useGravity = UseGravity;
            body.interpolation = Interpolation;
            body.collisionDetectionMode = CollisionDetectionMode;
            body.constraints = Constraints;
            body.centerOfMass = CenterOfMass;
            body.inertiaTensor = InertiaTensor;
            body.inertiaTensorRotation = InertiaTensorRotation;
            body.maxAngularVelocity = MaxAngularVelocity;
            body.maxDepenetrationVelocity = MaxDepenetrationVelocity;
            body.sleepThreshold = SleepThreshold;
            body.solverIterations = SolverIterations;
            body.solverVelocityIterations = SolverVelocityIterations;

            body.isKinematic = IsKinematic;
            body.detectCollisions = DetectCollisions;
            if (!body.isKinematic)
            {
                body.velocity = velocity;
                body.angularVelocity = angularVelocity;
                if (sleeping) body.Sleep();
                else body.WakeUp();
            }
        }
    }
}
