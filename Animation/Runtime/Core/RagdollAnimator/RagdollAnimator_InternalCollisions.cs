using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    public partial class RagdollAnimator
    {
        [SerializeField]
        RagdollInternalCollisionSettings internalCollisionSettings =
            RagdollInternalCollisionSettings.Default;

        [NonSerialized] bool manualInternalCollisionControl;
        [NonSerialized] bool pendingManualInternalCollisionWrite;
        [NonSerialized] bool pendingManualInternalCollisionCollide;
        [NonSerialized] bool pendingManualInternalCollisionUseIgnores;

        RagdollInternalCollisionController internalCollisionController;
        bool internalCollisionIgnoresDirty;

        public RagdollInternalCollisionSettings InternalCollisionSettings
        {
            get
            {
                RagdollInternalCollisionSettings result =
                    internalCollisionSettings;
                result.Normalize();
                return result;
            }
            set
            {
                value.Normalize();
                internalCollisionSettings = value;
                RefreshInternalCollisionAutomaticPolicy();
            }
        }

        /// <summary>
        /// Enables collisions between different registered ragdoll muscles while
        /// preserving forced ignores authored on the active muscle profile.
        /// </summary>
        public bool InternalCollisions
        {
            get => InternalCollisionSettings.InternalCollisions;
            set
            {
                RagdollInternalCollisionSettings settings =
                    internalCollisionSettings;
                settings.Normalize();
                settings.InternalCollisions = value;
                internalCollisionSettings = settings;
                RefreshInternalCollisionAutomaticPolicy();
            }
        }

        /// <summary>
        /// Suspends automatic and lifecycle internal-collision writes. Call
        /// SetInternalCollisionsManual to own the pair table explicitly.
        /// </summary>
        public bool ManualInternalCollisionControl
        {
            get => manualInternalCollisionControl;
            set
            {
                if (manualInternalCollisionControl == value) return;
                manualInternalCollisionControl = value;
                if (internalCollisionController != null)
                {
                    internalCollisionController.SetManualControl(value);
                    if (!value)
                    {
                        RefreshInternalCollisionAutomaticPolicy();
                    }
                }
            }
        }

        public int RuntimeInternalColliderPairCount =>
            internalCollisionController == null
                ? 0
                : internalCollisionController.PairCount;
        public int RuntimeForcedInternalBonePairCount =>
            internalCollisionController == null
                ? 0
                : internalCollisionController.AuthoredForcedBonePairCount;
        public int RuntimeForcedInternalColliderPairCount =>
            internalCollisionController == null
                ? 0
                : internalCollisionController.ForcedColliderPairCount;
        public int LastInternalCollisionWriteCount =>
            internalCollisionController == null
                ? 0
                : internalCollisionController.LastWriteCount;
        public bool InternalCollisionLifecycleOverrideActive =>
            internalCollisionController != null
            && internalCollisionController.LifecycleOverrideActive;

        /// <summary>
        /// Applies an immediate manual collision policy without changing the serialized
        /// global toggle. Set ManualInternalCollisionControl first when the result must
        /// not be replaced by the next automatic update.
        /// </summary>
        public void SetInternalCollisionsManual(
            bool collide,
            bool useInternalCollisionIgnores)
        {
            if (internalCollisionController == null)
            {
                pendingManualInternalCollisionWrite = true;
                pendingManualInternalCollisionCollide = collide;
                pendingManualInternalCollisionUseIgnores =
                    useInternalCollisionIgnores;
                return;
            }

            RefreshInternalCollisionIgnoresIfDirty();
            internalCollisionController.ApplyManual(
                collide,
                useInternalCollisionIgnores);
        }

        /// <summary>
        /// Re-resolves authored muscle ignore rules and reapplies the current policy at
        /// the next fixed simulation boundary.
        /// </summary>
        public void FlagInternalCollisionsForUpdate()
        {
            internalCollisionIgnoresDirty = true;
            if (internalCollisionController != null)
            {
                internalCollisionController.FlagForReapply();
            }
        }

        /// <summary>
        /// Reapplies the currently owned policy after a collider or Rigidbody has been
        /// reactivated. Physics.IgnoreCollision state is not persistent across all
        /// deactivation paths in the supported Unity versions.
        /// </summary>
        internal void ReapplyInternalCollisionPolicy()
        {
            if (internalCollisionController == null) return;
            RefreshInternalCollisionIgnoresIfDirty();
            internalCollisionController.FlagForReapply();
            internalCollisionController.ReapplyCurrentPolicy();
        }

        void InitializeInternalCollisions()
        {
            if (internalCollisionController != null) return;

            internalCollisionSettings.Normalize();
            RagdollInternalCollisionIgnoreRuntime authored =
                ResolveAuthoredInternalCollisionIgnores();
            internalCollisionController =
                RagdollInternalCollisionController.Create(
                    Bindings,
                    authored);

            try
            {
                internalCollisionController.SetManualControl(
                    manualInternalCollisionControl);
                internalCollisionController.UpdateAutomatic(
                    internalCollisionSettings.InternalCollisions);

                if (pendingManualInternalCollisionWrite)
                {
                    internalCollisionController.ApplyManual(
                        pendingManualInternalCollisionCollide,
                        pendingManualInternalCollisionUseIgnores);
                    pendingManualInternalCollisionWrite = false;
                }
            }
            catch
            {
                try
                {
                    internalCollisionController.Release();
                }
                catch (Exception releaseException)
                {
                    Debug.LogException(releaseException, this);
                }
                internalCollisionController = null;
                throw;
            }
        }

        void UpdateInternalCollisionsBeforeSimulation()
        {
            if (internalCollisionController == null) return;

            RefreshInternalCollisionIgnoresIfDirty();

            RagdollInternalCollisionSettings settings =
                internalCollisionSettings;
            settings.Normalize();
            internalCollisionSettings = settings;
            internalCollisionController.UpdateAutomatic(
                settings.InternalCollisions);
        }

        void RefreshInternalCollisionIgnoresIfDirty()
        {
            if (internalCollisionController == null
                || !internalCollisionIgnoresDirty)
            {
                return;
            }

            RagdollInternalCollisionIgnoreRuntime authored =
                ResolveAuthoredInternalCollisionIgnores();
            internalCollisionController.SetAuthoredIgnores(authored);
            internalCollisionIgnoresDirty = false;
        }

        void RefreshInternalCollisionAutomaticPolicy()
        {
            if (internalCollisionController == null) return;

            RagdollInternalCollisionSettings settings =
                internalCollisionSettings;
            settings.Normalize();
            internalCollisionSettings = settings;
            internalCollisionController.UpdateAutomatic(
                settings.InternalCollisions);
        }

        RagdollInternalCollisionIgnoreRuntime
            ResolveAuthoredInternalCollisionIgnores()
        {
            RagdollMuscleProfile profile = lifecycleMuscles
                ? lifecycleMuscles.MuscleProfile
                : null;
            if (!profile)
            {
                return RagdollInternalCollisionIgnoreRuntime.CreateEmpty(
                    Bindings.BoneCount);
            }

            RagdollInternalCollisionIgnoreRuntime runtime;
            string error;
            if (!profile.TryCreateInternalCollisionRuntime(
                Bindings,
                out runtime,
                out error))
            {
                throw new InvalidOperationException(
                    "The active RagdollMuscleProfile contains invalid internal-collision ignores: "
                    + error);
            }

            return runtime;
        }

        void BeginInternalCollisionLifecycleOverride(bool enable)
        {
            if (internalCollisionController == null) return;
            internalCollisionController.BeginLifecycleOverride(
                enable && !manualInternalCollisionControl);
        }

        void EndInternalCollisionLifecycleOverride()
        {
            if (internalCollisionController == null) return;
            RefreshInternalCollisionIgnoresIfDirty();
            internalCollisionController.EndLifecycleOverride();
        }

        void AbandonInternalCollisionsForPermanentFreeze()
        {
            if (internalCollisionController == null) return;
            internalCollisionController.AbandonForPermanentFreeze();
        }

        void ShutdownInternalCollisions()
        {
            if (internalCollisionController == null) return;
            if (lifecyclePermanentDestructionScheduled
                || lifecycleApplicationQuitting
                || !gameObject.scene.isLoaded)
            {
                internalCollisionController = null;
                internalCollisionIgnoresDirty = false;
                return;
            }

            try
            {
                internalCollisionController.Release();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                internalCollisionController = null;
                internalCollisionIgnoresDirty = false;
            }
        }
    }
}
