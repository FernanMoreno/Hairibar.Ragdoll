using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Pickup-owned melee surface. A hidden Capsule replaces the authored dropped Box
    /// for the complete held transaction, as documented by RootMotion PropMelee.
    /// StartAction temporarily changes radius, additional-pin weight and mass only.
    /// </summary>
    [AddComponentMenu("Ragdoll/Props/Ragdoll Prop Melee")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RagdollProp))]
    public sealed class RagdollPropMelee : MonoBehaviour
    {
        const string ActionObjectName = "__RagdollPropMeleeActionCollider";

        string OwnedObjectName => ActionObjectName + "_"
            + RagdollUnityObjectId.Get(this);

        [SerializeField]
        RagdollPropMeleeSettings settings = new RagdollPropMeleeSettings();
        [SerializeField, HideInInspector] GameObject actionColliderObject;
        [SerializeField, HideInInspector] BoxCollider actionBox;
        [SerializeField, HideInInspector] CapsuleCollider actionCapsule;
        BoxCollider droppedBox;
        bool droppedBoxWasEnabled;

        RagdollProp prop;
        Collider actionCollider;
        RagdollPropMeleeSnapshot held;
        bool heldSession;
        bool actionActive;
        int heldSessionVersion;
        int actionVersion;
        string lastActionError;
        bool timedAction;
        float timedActionRemaining;
        bool endTimedActionAtFixedBoundary;

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
        internal bool UsesAbsoluteActionPinWeight => actionActive
            && held.UseAbsoluteActionPinWeight;
        internal float EffectiveActionAdditionalPinWeight =>
            held.ActionAdditionalPinWeight;
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
            if (!Application.isPlaying || !heldSession)
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
                RestoreDroppedColliderState();
                heldSession = false;
                held = RagdollPropMeleeSnapshot.Disabled;
            }
            RefreshPropOverridesAfterActionChange();
        }

        void OnDestroy()
        {
            CancelAction();
            RestoreDroppedColliderState();
            heldSession = false;
            held = RagdollPropMeleeSnapshot.Disabled;
            RefreshPropOverridesAfterActionChange();
            DestroyOwnedObject();
        }

        [RagdollCompatibilityApi("Props and IK", "http://www.root-motion.com/puppetmasterdox/html/page6.html")]
        public bool BeginAction()
        {
            return BeginActionCore(true);
        }

        /// <summary>
        /// Starts or restarts one timed melee action. Expiration is committed at a
        /// FixedUpdate boundary so physics overrides are never changed mid-step.
        /// </summary>
        [RagdollCompatibilityApi("Props and IK", "http://www.root-motion.com/puppetmasterdox/html/page6.html")]
        public bool StartAction(float duration)
        {
            return StartActionCore(duration, true);
        }

        internal bool StartActionForTesting(float duration)
        {
            return StartActionCore(duration, false);
        }

        bool StartActionCore(float duration, bool requireCommittedPickup)
        {
            if (float.IsNaN(duration) || float.IsInfinity(duration)
                || duration < 0f)
            {
                lastActionError = "Action duration must be finite and non-negative.";
                return false;
            }
            if (!BeginActionCore(requireCommittedPickup)) return false;

            timedAction = true;
            timedActionRemaining = duration;
            endTimedActionAtFixedBoundary = duration <= 0f;
            return true;
        }

        void FixedUpdate()
        {
            AdvanceTimedAction(Time.fixedDeltaTime);
        }

        internal void AdvanceTimedAction(float deltaTime)
        {
            if (!timedAction || !actionActive) return;
            if (endTimedActionAtFixedBoundary)
            {
                EndAction();
                return;
            }

            deltaTime = float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)
                ? 0f
                : Mathf.Max(0f, deltaTime);
            timedActionRemaining = Mathf.Max(
                0f,
                timedActionRemaining - deltaTime);
            if (timedActionRemaining <= 0f) EndAction();
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

        [RagdollCompatibilityApi("Props and IK", "http://www.root-motion.com/puppetmasterdox/html/page6.html")]
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
            CaptureAndDisableDroppedBox();
            actionCollider.enabled = true;
        }

        internal void EndHeldSession()
        {
            CancelAction();
            RestoreDroppedColliderState();
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
            // Official PropMelee contract uses Box when dropped and Capsule for the
            // complete pickup. Shape remains serialized only for source compatibility.
            actionCollider = actionCapsule;
        }

        void CancelAction()
        {
            if (actionActive) actionVersion++;
            actionActive = false;
            timedAction = false;
            timedActionRemaining = 0f;
            endTimedActionAtFixedBoundary = false;
            if (actionCollider)
            {
                ApplyColliderGeometry(false);
            }
            if (heldSession && actionCollider)
            {
                actionCollider.enabled = true;
                if (actionBox) actionBox.enabled = false;
            }
            else DisableOwnedColliders();
        }

        void CaptureAndDisableDroppedBox()
        {
            // PropMelee's dropped collider belongs to the prop object itself. Never
            // disable a BoxCollider from an unrelated child hierarchy.
            droppedBox = GetComponent<BoxCollider>();
            if (!droppedBox || IsOwnedCollider(droppedBox))
            {
                droppedBox = null;
                droppedBoxWasEnabled = false;
                return;
            }
            droppedBoxWasEnabled = droppedBox.enabled;
            droppedBox.enabled = false;
        }

        void RestoreDroppedColliderState()
        {
            if (droppedBox) droppedBox.enabled = droppedBoxWasEnabled;
            droppedBox = null;
            droppedBoxWasEnabled = false;
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
            // Unity documents DestroyImmediate as an Editor-only operation that should
            // not be used in game code. Colliders are disabled before this point, so
            // deferred runtime destruction cannot participate in physics.
            if (Application.isPlaying) Destroy(owned);
            else DestroyImmediate(owned);
        }
    }
}
