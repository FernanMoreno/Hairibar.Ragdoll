using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Small adapter around RagdollAnimator used by the prop state machine. Keeping the
    /// orchestration behind an interface makes the ordering policy deterministic and
    /// testable without duplicating any of the ragdoll core.
    /// </summary>
    internal interface IRagdollPropMuscleRuntime
    {
        bool IsReady { get; }
        bool IsSimulationDisabled { get; }

        bool TryResolveSlot(
            ConfigurableJoint joint,
            out RagdollBoneHandle handle);

        bool TryRegisterSlot(
            RagdollRuntimeMuscleRegistration registration,
            out RagdollBoneHandle handle,
            out string error);

        bool TryValidatePropGroup(
            RagdollBoneHandle handle,
            out string error);

        RagdollMuscleConnectionState GetConnectionState(
            RagdollBoneHandle handle);

        bool IsDisconnecting(RagdollBoneHandle handle);
        bool IsReconnecting(RagdollBoneHandle handle);

        bool TryDisconnect(
            RagdollBoneHandle handle,
            bool deactivate,
            out string error);

        bool TryReconnect(
            RagdollBoneHandle handle,
            out string error);

        bool TryGetAdditionalPinAuthority(
            RagdollBoneHandle handle,
            Rigidbody slotBody,
            out float authority,
            out string error);

        bool TryReapplyInternalCollisionPolicy(out string error);
    }

    internal sealed class RagdollPropMuscleRuntimeAdapter :
        IRagdollPropMuscleRuntime
    {
        readonly RagdollAnimator animator;

        internal RagdollPropMuscleRuntimeAdapter(RagdollAnimator animator)
        {
            this.animator = animator;
        }

        public bool IsReady => animator
            && animator.Bindings
            && animator.Bindings.IsInitialized;

        public bool IsSimulationDisabled
        {
            get
            {
                if (!animator) return false;
                RagdollSimulationModeController mode =
                    animator.GetComponent<RagdollSimulationModeController>();
                return mode
                    && mode.IsInitialized
                    && mode.CurrentMode == RagdollSimulationMode.Disabled;
            }
        }

        public bool TryResolveSlot(
            ConfigurableJoint joint,
            out RagdollBoneHandle handle)
        {
            handle = RagdollBoneHandle.Invalid;
            return IsReady
                && joint
                && animator.Bindings.TryGetBoneHandle(joint, out handle);
        }

        public bool TryRegisterSlot(
            RagdollRuntimeMuscleRegistration registration,
            out RagdollBoneHandle handle,
            out string error)
        {
            handle = RagdollBoneHandle.Invalid;
            error = null;
            if (!IsReady)
            {
                error = "RagdollAnimator has not completed initialization.";
                return false;
            }
            return animator.TryAddMuscle(
                registration,
                out handle,
                out error);
        }

        public bool TryValidatePropGroup(
            RagdollBoneHandle handle,
            out string error)
        {
            error = null;
            if (!animator)
            {
                error = "RagdollAnimator is no longer available.";
                return false;
            }

            RagdollMuscleController muscles =
                animator.GetComponent<RagdollMuscleController>();
            if (!muscles || !muscles.IsInitialized)
            {
                // A muscle profile is optional. Runtime-added slots are still registered
                // with the Prop resolver even when no authored profile is present.
                return true;
            }

            RagdollMuscleGroup group;
            if (!muscles.TryGetMuscleGroup(handle, out group)) return true;
            if (group == RagdollMuscleGroup.Prop) return true;

            error = "The registered prop slot resolves to muscle group '"
                + group + "' instead of Prop.";
            return false;
        }

        public RagdollMuscleConnectionState GetConnectionState(
            RagdollBoneHandle handle)
        {
            return animator.GetMuscleConnectionState(handle);
        }

        public bool IsDisconnecting(RagdollBoneHandle handle)
        {
            return animator.IsDisconnecting(handle);
        }

        public bool IsReconnecting(RagdollBoneHandle handle)
        {
            return animator.IsReconnecting(handle);
        }

        public bool TryDisconnect(
            RagdollBoneHandle handle,
            bool deactivate,
            out string error)
        {
            return animator.TryDisconnectMuscleRecursive(
                handle,
                RagdollMuscleDisconnectMode.Sever,
                deactivate,
                out error);
        }

        public bool TryReconnect(
            RagdollBoneHandle handle,
            out string error)
        {
            return animator.TryReconnectMuscleRecursive(handle, out error);
        }

        public bool TryGetAdditionalPinAuthority(
            RagdollBoneHandle handle,
            Rigidbody slotBody,
            out float authority,
            out string error)
        {
            authority = 0f;
            error = null;
            if (!IsReady)
            {
                error = "RagdollAnimator is not ready for additional prop pinning.";
                return false;
            }
            if (!slotBody || !slotBody.gameObject.activeInHierarchy
                || slotBody.isKinematic)
            {
                return false;
            }
            if (!animator.IsAlive || animator.IsKilling || animator.IsFrozen)
            {
                return false;
            }

            RagdollSimulationModeController mode =
                animator.GetComponent<RagdollSimulationModeController>();
            if (mode && mode.IsInitialized
                && (mode.CurrentMode != RagdollSimulationMode.Active
                    || mode.TargetMode != RagdollSimulationMode.Active))
            {
                return false;
            }
            if (animator.GetMuscleConnectionState(handle)
                != RagdollMuscleConnectionState.Connected)
            {
                return false;
            }

            RagdollMuscleController muscles =
                animator.GetComponent<RagdollMuscleController>();
            if (!muscles || !muscles.IsInitialized)
            {
                error = "Additional prop pinning requires an initialized RagdollMuscleController.";
                return false;
            }

            authority = Mathf.Clamp01(
                muscles.GetEffectivePositionAuthority(handle));
            return true;
        }

        public bool TryReapplyInternalCollisionPolicy(out string error)
        {
            error = null;
            if (!animator) return true;
            try
            {
                animator.ReapplyInternalCollisionPolicy();
                return true;
            }
            catch (Exception exception)
            {
                error = "The core internal-collision policy could not be reapplied: "
                    + exception.Message;
                Debug.LogException(exception, animator);
                return false;
            }
        }
    }
}
