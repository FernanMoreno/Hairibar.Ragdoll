using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    internal enum RagdollBipedStaggerState
    {
        Idle,
        LiftOff,
        Swing,
        Replant,
        Settling,
        Failed
    }

    /// <summary>
    /// Pure deterministic state and timing core for the stagger recovery-step
    /// cycle, mirroring RagdollPuppetStateMachine's split between Unity object
    /// access (kept in the owning behaviour component) and testable state logic.
    /// </summary>
    internal sealed class RagdollBipedStaggerStateMachine
    {
        internal RagdollBipedStaggerState State { get; private set; }
        internal float StateElapsedTime { get; private set; }
        internal int StepCount { get; private set; }

        internal RagdollBipedStaggerStateMachine()
        {
            Reset();
        }

        internal void Reset()
        {
            State = RagdollBipedStaggerState.Idle;
            StateElapsedTime = 0f;
            StepCount = 0;
        }

        /// <summary>
        /// Begins a new LiftOff phase from Idle or from a completed Settling.
        /// Fails the cycle instead once maxSteps is reached.
        /// </summary>
        internal bool TryBeginStep(int maxSteps)
        {
            if (State != RagdollBipedStaggerState.Idle
                && State != RagdollBipedStaggerState.Settling)
            {
                return false;
            }
            if (StepCount >= Mathf.Max(0, maxSteps))
            {
                Fail();
                return false;
            }

            StepCount++;
            State = RagdollBipedStaggerState.LiftOff;
            StateElapsedTime = 0f;
            return true;
        }

        /// <summary>Forces Failed regardless of the current phase.</summary>
        internal void RegisterStepFailed()
        {
            Fail();
        }

        /// <summary>
        /// Advances state-local time. Returns true when the current phase's
        /// duration elapsed and the machine moved to the next phase. Settling
        /// completing returns the machine to Idle -- the owning behaviour reads
        /// the latest balance classification there to decide between declaring
        /// success, beginning another step, or giving up.
        /// </summary>
        internal bool Advance(
            float deltaTime,
            float liftOffDuration,
            float swingDuration,
            float replantDuration,
            float settlingDuration)
        {
            StateElapsedTime += Mathf.Max(0f, deltaTime);
            switch (State)
            {
                case RagdollBipedStaggerState.LiftOff:
                    return TryAdvancePhase(liftOffDuration, RagdollBipedStaggerState.Swing);
                case RagdollBipedStaggerState.Swing:
                    return TryAdvancePhase(swingDuration, RagdollBipedStaggerState.Replant);
                case RagdollBipedStaggerState.Replant:
                    return TryAdvancePhase(replantDuration, RagdollBipedStaggerState.Settling);
                case RagdollBipedStaggerState.Settling:
                    return TryAdvancePhase(settlingDuration, RagdollBipedStaggerState.Idle);
                default:
                    return false;
            }
        }

        void Fail()
        {
            State = RagdollBipedStaggerState.Failed;
            StateElapsedTime = 0f;
        }

        bool TryAdvancePhase(float duration, RagdollBipedStaggerState next)
        {
            float safeDuration = Mathf.Max(0f, duration);
            if (safeDuration > Mathf.Epsilon && StateElapsedTime < safeDuration)
            {
                return false;
            }

            State = next;
            StateElapsedTime = 0f;
            return true;
        }
    }
}
