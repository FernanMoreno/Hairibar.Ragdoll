using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Pickup-owned melee surface. A hidden child owns both supported Collider types,
    /// exactly one of which participates in the held transaction. Keeping the Collider
    /// alive but disabled outside actions makes surface snapshots, internal ignores and
    /// lifecycle cleanup deterministic without creating a second Rigidbody.
    /// </summary>
    [AddComponentMenu("Ragdoll/Props/Ragdoll Prop Melee")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RagdollProp))]
    public sealed class RagdollPropMelee : MonoBehaviour
    {
        const string ActionObjectName = "__RagdollPropMeleeActionCollider";

        string OwnedObjectName => ActionObjectName + "_" + GetInstanceID();

        [SerializeField]
        RagdollPropMeleeSettings settings = new RagdollPropMeleeSettings();
        [SerializeField, HideInInspector] GameObject actionColliderObject;
        [SerializeField, HideInInspector] BoxCollider actionBox;
        [SerializeField, HideInInspector] CapsuleCollider actionCapsule;

        RagdollProp prop;
        Collider actionCollider;
        RagdollPropMeleeSnapshot held;
        bool heldSession;
        bool actionActive;
        int heldSessionVersion;
        int actionVersion;
        string lastActionError;

        public RagdollPropMeleeSettings Settings
        {
            get
            {
                EnsureSettings();
                return settings;
            }
        }
        public Collider ActionCollider => actionCollider;
        public bool IsHeldSession => heldSession;
        public bool IsActionActive => actionActive;
        public int HeldSessionVersion => heldSessionVersion;
        public int ActionVersion => actionVersion;
        public string LastActionError => lastActionError;
        public float EffectivePinWeightMultiplier => actionActive
            ? held.ActionPinWeightMultiplier
            : 1f;
        public float EffectiveMassMultiplier => actionActive
            ? held.ActionMassMultiplier
            : 1f;
        public Vector3 HeldCenterOfMassOffset => heldSession
            ? held.CenterOfMassOffset
            : Vector3.zero;
        public bool HasHeldCenterOfMassOffset => heldSession
            && held.HasCenterOfMassOffset;

        void Reset()
        {
            EnsureSettings();
            prop = GetComponent<RagdollProp>();
        }

        void OnValidate()
        {
            EnsureSettings();
            if (!prop) prop = GetComponent<RagdollProp>();
            settings.Normalize();
            ResolveOwnedObject();
            // Inspector edits during Play Mode must not tear down the frozen action
            // transaction. The edited settings are intentionally deferred to the next
            // pickup; only idle/edit-time colliders are forced off here.
            if (!Application.isPlaying || !actionActive)
            {
                DisableOwnedColliders();
            }
        }

        void OnDisable()
        {
            CancelAction();
            // Hierarchy deactivation is temporary for simulation/lifecycle modes. Preserve
            // the frozen pickup snapshot in that case, but disabling this component itself
            // relinquishes all melee overrides for the active transaction.
            if (!enabled)
            {
                heldSession = false;
                held = RagdollPropMeleeSnapshot.Disabled;
            }
            RefreshPropOverridesAfterActionChange();
        }

        void OnDestroy()
        {
            CancelAction();
            heldSession = false;
            held = RagdollPropMeleeSnapshot.Disabled;
            RefreshPropOverridesAfterActionChange();
            DestroyOwnedObject();
        }

        public bool BeginAction()
        {
            return BeginActionCore(true);
        }

        internal bool BeginActionForTesting()
        {
            return BeginActionCore(false);
        }

        bool BeginActionCore(bool requireCommittedPickup)
        {
            lastActionError = null;
            if (!prop) prop = GetComponent<RagdollProp>();
            if (!isActiveAndEnabled || !heldSession || !actionCollider)
            {
                lastActionError = "No active held melee session is available.";
                return false;
            }
            if (requireCommittedPickup
                && (!prop || !prop.CanBeginMeleeAction))
            {
                lastActionError =
                    "Melee actions require a committed RagdollProp in Holding state.";
                return false;
            }

            bool wasActive = actionActive;
            try
            {
                actionActive = true;
                if (!wasActive) actionVersion++;
                ApplyColliderGeometry(true);
                actionCollider.enabled = true;

                string collisionError;
                if (prop && prop.IsHeld
                    && !prop.TryArmMeleeActionCollisionPolicy(
                        out collisionError))
                {
                    CancelAction();
                    lastActionError = collisionError;
                    return false;
                }

                string overrideError;
                if (prop && prop.IsHeld
                    && !prop.TryRefreshHeldPhysicalOverridesFromMelee(
                        out overrideError))
                {
                    CancelAction();
                    RefreshPropOverridesAfterActionChange();
                    lastActionError = overrideError;
                    return false;
                }
                if (prop && prop.IsHeld)
                {
                    prop.WakeHeldBodyForMeleeAction();
                }
                return true;
            }
            catch (Exception exception)
            {
                CancelAction();
                lastActionError = "Melee action activation failed: "
                    + exception.Message;
                return false;
            }
        }

        public bool EndAction()
        {
            lastActionError = null;
            if (!heldSession) return false;
            CancelAction();
            return RefreshPropOverridesAfterActionChange();
        }

        internal void BeginHeldSession()
        {
            EnsureSettings();
            if (!prop) prop = GetComponent<RagdollProp>();
            lastActionError = null;
            held = settings.Capture();
            heldSession = enabled && held.Enabled;
            heldSessionVersion++;
            actionActive = false;
            EnsureOwnedColliders();
            // The hidden owner follows the authored prop layer at the beginning of every
            // pickup. It may survive earlier sessions, while users are still free to change
            // the standalone prop layer between pickups.
            if (actionColliderObject)
            {
                actionColliderObject.layer = gameObject.layer;
            }
            SelectActionCollider();
            DisableOwnedColliders();
            if (!heldSession || !actionCollider) return;

            ApplyColliderGeometry(false);
            actionCollider.isTrigger = false;
        }

        internal void EndHeldSession()
        {
            CancelAction();
            heldSession = false;
            held = RagdollPropMeleeSnapshot.Disabled;
            lastActionError = null;
        }

        internal bool TryValidateConfiguration(out string error)
        {
            EnsureSettings();
            return settings.TryValidate(out error);
        }

        internal bool IsOwnedCollider(Collider candidate)
        {
            return candidate
                && (candidate == actionBox || candidate == actionCapsule);
        }

        internal bool IsSelectedCollider(Collider candidate)
        {
            return candidate && candidate == actionCollider;
        }

        bool RefreshPropOverridesAfterActionChange()
        {
            if (!prop || !prop.IsHeld) return true;
            string error;
            if (prop.TryRefreshHeldPhysicalOverridesFromMelee(out error))
            {
                return true;
            }
            lastActionError = error;
            return false;
        }

        void EnsureSettings()
        {
            if (settings == null)
            {
                settings = new RagdollPropMeleeSettings();
            }
        }

        void EnsureOwnedColliders()
        {
            ResolveOwnedObject();
            if (!actionColliderObject)
            {
                actionColliderObject = new GameObject(OwnedObjectName);
            }

            Transform ownedTransform = actionColliderObject.transform;
            ownedTransform.SetParent(transform, false);
            ownedTransform.localPosition = Vector3.zero;
            ownedTransform.localRotation = Quaternion.identity;
            ownedTransform.localScale = Vector3.one;
            actionColliderObject.layer = gameObject.layer;
            actionColliderObject.hideFlags = HideFlags.HideInHierarchy
                | HideFlags.DontSaveInEditor
                | HideFlags.DontSaveInBuild;

            if (!actionBox)
            {
                actionBox = actionColliderObject.GetComponent<BoxCollider>();
                if (!actionBox)
                {
                    actionBox = actionColliderObject.AddComponent<BoxCollider>();
                }
            }
            if (!actionCapsule)
            {
                actionCapsule = actionColliderObject.GetComponent<CapsuleCollider>();
                if (!actionCapsule)
                {
                    actionCapsule = actionColliderObject.AddComponent<CapsuleCollider>();
                }
            }

            actionBox.hideFlags = HideFlags.HideInInspector;
            actionCapsule.hideFlags = HideFlags.HideInInspector;
            actionBox.isTrigger = false;
            actionCapsule.isTrigger = false;
        }

        void ResolveOwnedObject()
        {
            if (actionColliderObject
                && actionColliderObject.transform.parent != transform)
            {
                // Never adopt or destroy an arbitrary externally referenced object. A
                // corrupted serialized reference is discarded and a local owner is resolved.
                actionColliderObject = null;
                actionBox = null;
                actionCapsule = null;
                actionCollider = null;
            }
            if (!actionColliderObject)
            {
                Transform child = transform.Find(OwnedObjectName);
                if (child) actionColliderObject = child.gameObject;
            }
            if (!actionColliderObject) return;

            if (!actionBox)
            {
                actionBox = actionColliderObject.GetComponent<BoxCollider>();
            }
            if (!actionCapsule)
            {
                actionCapsule = actionColliderObject.GetComponent<CapsuleCollider>();
            }
        }

        void SelectActionCollider()
        {
            actionCollider = held.Shape == RagdollPropMeleeShape.Box
                ? (Collider)actionBox
                : actionCapsule;
        }

        void CancelAction()
        {
            if (actionActive) actionVersion++;
            actionActive = false;
            if (actionCollider)
            {
                ApplyColliderGeometry(false);
            }
            DisableOwnedColliders();
        }

        void DisableOwnedColliders()
        {
            if (actionBox) actionBox.enabled = false;
            if (actionCapsule) actionCapsule.enabled = false;
        }

        void ApplyColliderGeometry(bool boosted)
        {
            if (!actionCollider) return;
            float multiplier = boosted
                ? held.ActionColliderRadiusMultiplier
                : 1f;

            BoxCollider box = actionCollider as BoxCollider;
            if (box)
            {
                box.center = held.Center;
                box.size = new Vector3(
                    Mathf.Max(0.0001f, held.BoxSize.x * multiplier),
                    Mathf.Max(0.0001f, held.BoxSize.y * multiplier),
                    Mathf.Max(0.0001f, held.BoxSize.z * multiplier));
                return;
            }

            CapsuleCollider capsule = actionCollider as CapsuleCollider;
            if (!capsule) return;
            capsule.center = held.Center;
            capsule.direction = held.CapsuleDirection;
            capsule.radius = Mathf.Max(0.0001f, held.Radius * multiplier);
            capsule.height = Mathf.Max(
                capsule.radius * 2f,
                held.Height * multiplier);
        }

        void DestroyOwnedObject()
        {
            if (!actionColliderObject) return;
            GameObject owned = actionColliderObject;
            actionColliderObject = null;
            actionCollider = null;
            actionBox = null;
            actionCapsule = null;
            if (Application.isPlaying) Destroy(owned);
            else DestroyImmediate(owned);
        }
    }
}
