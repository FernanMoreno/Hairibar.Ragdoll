using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    public partial class RagdollAnimator
    {
        RagdollTeleportRequest pendingTeleport;
        bool teleportPending;
        bool teleportProcessing;

        /// <summary>
        /// Queues an absolute Target-root teleport. The newest request replaces an older
        /// unprocessed request and is committed at the next animation read boundary. Stable
        /// Disabled and Frozen simulations process immediately because no read will occur.
        /// </summary>
        public void Teleport(
            Vector3 position,
            Quaternion rotation,
            bool moveToTarget)
        {
            if (lifecyclePermanentDestructionScheduled)
            {
                throw new InvalidOperationException(
                    "A permanently frozen ragdoll cannot be teleported.");
            }

            pendingTeleport = RagdollTeleportRequest.Create(
                position,
                rotation,
                moveToTarget);
            teleportPending = true;

            if (CanProcessTeleportImmediately())
            {
                ProcessPendingTeleport();
            }
        }

        void ProcessPendingTeleportAtFixedBoundary()
        {
            if (!teleportPending) return;

            if (UsesFixedAnimatorUpdate()
                || !LifecycleAllowsAnimationSampling())
            {
                ProcessPendingTeleport();
            }
        }

        void ProcessPendingTeleportAtLateBoundary()
        {
            if (!teleportPending) return;

            if (!UsesFixedAnimatorUpdate()
                || !LifecycleAllowsAnimationSampling())
            {
                ProcessPendingTeleport();
            }
        }

        bool CanProcessTeleportImmediately()
        {
            if (teleportProcessing
                || !teleportPending
                || animatedPairs is null)
            {
                return false;
            }
            if (!isActiveAndEnabled || LifecycleIsFrozenStable()) return true;

            return lifecycleSimulationMode
                && lifecycleSimulationMode.IsInitialized
                && lifecycleSimulationMode.CurrentMode
                    == RagdollSimulationMode.Disabled
                && !lifecycleSimulationMode.IsTransitioning;
        }

        bool ProcessPendingTeleport()
        {
            if (teleportProcessing
                || !teleportPending
                || animatedPairs is null)
            {
                return false;
            }

            bool processed = false;
            do
            {
                teleportProcessing = true;
                try
                {
                    processed |= ProcessSinglePendingTeleport();
                }
                finally
                {
                    teleportProcessing = false;
                }

                // A Frozen/Disabled callback can queue another request. Drain the newest
                // request after the current fan-out without recursively entering the core.
                // Active simulations keep it for the next read boundary.
            }
            while (CanProcessTeleportImmediately());

            return processed;
        }

        bool ProcessSinglePendingTeleport()
        {
            RagdollTeleportRequest request = pendingTeleport;
            teleportPending = false;

            Transform targetRoot = transform;
            Transform puppetRoot = Bindings ? Bindings.transform : null;
            if (!puppetRoot)
            {
                throw new InvalidOperationException(
                    "Teleport requires an initialized Puppet root.");
            }

            Quaternion deltaRotation =
                RagdollTeleportMath.CalculateDeltaRotation(
                    targetRoot.rotation,
                    request.Rotation);
            Vector3 pivot = ResolveTeleportPivot(targetRoot, puppetRoot);
            Vector3 deltaPosition =
                RagdollTeleportMath.CalculateDeltaPosition(
                    targetRoot.position,
                    request.Position,
                    pivot,
                    deltaRotation);

            TeleportSnapshot snapshot = TeleportSnapshot.Capture(
                targetRoot,
                puppetRoot,
                animatedPairs);

            try
            {
                ApplyTeleportCore(
                    snapshot,
                    request,
                    deltaRotation,
                    deltaPosition,
                    pivot);
            }
            catch
            {
                snapshot.Restore();
                throw;
            }

            if (lifecycleBehaviours && lifecycleBehaviours.IsInitialized)
            {
                lifecycleBehaviours.NotifyTeleported(
                    deltaRotation,
                    deltaPosition,
                    pivot,
                    request.MoveToTarget);
            }

            return true;
        }

        void ApplyTeleportCore(
            TeleportSnapshot snapshot,
            RagdollTeleportRequest request,
            Quaternion deltaRotation,
            Vector3 deltaPosition,
            Vector3 pivot)
        {
            RagdollTeleportHierarchy.ApplyRootTransforms(
                snapshot.TargetRoot.Transform,
                snapshot.PuppetRoot.Transform,
                request.Position,
                request.Rotation,
                snapshot.PuppetRoot.Position,
                snapshot.PuppetRoot.Rotation,
                deltaRotation,
                deltaPosition,
                pivot);

            for (int index = 0; index < snapshot.Bodies.Length; index++)
            {
                RigidbodySnapshot body = snapshot.Bodies[index];
                body.Body.position = RagdollTeleportMath.TransformPoint(
                    body.Position,
                    deltaRotation,
                    deltaPosition,
                    pivot);
                body.Body.rotation = RagdollTeleportMath.TransformRotation(
                    body.Rotation,
                    deltaRotation);
            }

            for (int index = 0; index < animatedPairs.Length; index++)
            {
                animatedPairs[index].ApplyTeleport(
                    deltaRotation,
                    deltaPosition,
                    pivot);
            }

            if (request.MoveToTarget)
            {
                MovePuppetToTargetPose(false);
            }

            // Teleport clears sampled Target velocities but preserves physical momentum.
            // Restoring sleep state also prevents a dormant Frozen/Disabled Puppet from
            // being woken merely because its world pose was relocated.
            for (int index = 0; index < snapshot.Bodies.Length; index++)
            {
                snapshot.Bodies[index].RestoreMotionState();
            }
        }

        static Vector3 ResolveTeleportPivot(
            Transform targetRoot,
            Transform puppetRoot)
        {
            HashSet<Transform> targetAncestors = new HashSet<Transform>();
            for (Transform current = targetRoot; current; current = current.parent)
            {
                targetAncestors.Add(current);
            }

            for (Transform current = puppetRoot; current; current = current.parent)
            {
                if (targetAncestors.Contains(current))
                {
                    return current.position;
                }
            }

            return targetRoot.position;
        }

        struct TransformSnapshot
        {
            internal Transform Transform;
            internal Vector3 Position;
            internal Quaternion Rotation;

            internal static TransformSnapshot Capture(Transform value)
            {
                return new TransformSnapshot
                {
                    Transform = value,
                    Position = value.position,
                    Rotation = value.rotation
                };
            }
        }

        struct RigidbodySnapshot
        {
            internal Rigidbody Body;
            internal Vector3 Position;
            internal Quaternion Rotation;
            internal Vector3 Velocity;
            internal Vector3 AngularVelocity;
            internal bool WasSleeping;

            internal static RigidbodySnapshot Capture(Rigidbody body)
            {
                if (!body)
                {
                    throw new InvalidOperationException(
                        "Teleport encountered a missing registered Rigidbody.");
                }

                return new RigidbodySnapshot
                {
                    Body = body,
                    Position = body.position,
                    Rotation = body.rotation,
                    Velocity = body.velocity,
                    AngularVelocity = body.angularVelocity,
                    WasSleeping = body.IsSleeping()
                };
            }

            internal void RestoreMotionState()
            {
                if (!Body) return;

                Body.velocity = Velocity;
                Body.angularVelocity = AngularVelocity;
                if (WasSleeping) Body.Sleep();
                else Body.WakeUp();
            }

            internal void Restore()
            {
                if (!Body) return;

                Body.position = Position;
                Body.rotation = Rotation;
                RestoreMotionState();
            }
        }

        sealed class TeleportSnapshot
        {
            internal TransformSnapshot TargetRoot { get; private set; }
            internal TransformSnapshot PuppetRoot { get; private set; }
            internal RigidbodySnapshot[] Bodies { get; private set; }

            RagdollAnimator.AnimatedPair[] pairs;
            RagdollAnimator.AnimatedPair.TeleportState[] pairStates;

            internal static TeleportSnapshot Capture(
                Transform targetRoot,
                Transform puppetRoot,
                RagdollAnimator.AnimatedPair[] pairs)
            {
                if (!targetRoot) throw new ArgumentNullException(nameof(targetRoot));
                if (!puppetRoot) throw new ArgumentNullException(nameof(puppetRoot));
                if (pairs == null) throw new ArgumentNullException(nameof(pairs));

                TeleportSnapshot result = new TeleportSnapshot
                {
                    TargetRoot = TransformSnapshot.Capture(targetRoot),
                    PuppetRoot = TransformSnapshot.Capture(puppetRoot),
                    Bodies = new RigidbodySnapshot[pairs.Length],
                    pairs = pairs,
                    pairStates = new RagdollAnimator.AnimatedPair.TeleportState[pairs.Length]
                };

                for (int index = 0; index < pairs.Length; index++)
                {
                    if (pairs[index] == null)
                    {
                        throw new InvalidOperationException(
                            "Teleport encountered a missing animated pair.");
                    }

                    result.Bodies[index] = RigidbodySnapshot.Capture(
                        pairs[index].RagdollBone.Rigidbody);
                    result.pairStates[index] =
                        pairs[index].CaptureTeleportState();
                }

                return result;
            }

            internal void Restore()
            {
                RagdollTeleportHierarchy.RestoreRootTransforms(
                    TargetRoot.Transform,
                    TargetRoot.Position,
                    TargetRoot.Rotation,
                    PuppetRoot.Transform,
                    PuppetRoot.Position,
                    PuppetRoot.Rotation);

                for (int index = 0; index < Bodies.Length; index++)
                {
                    Bodies[index].Restore();
                    pairs[index].RestoreTeleportState(pairStates[index]);
                }
            }
        }
    }
}
