using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    internal readonly struct RagdollBipedStaggerPhaseSignals
    {
        internal readonly bool SelectedFootGrounded;
        internal readonly bool AnimatorStateAvailable;
        internal readonly float AnimatorNormalizedTime;
        internal readonly bool BalanceRecovered;

        internal bool HasAnimatorProgress => AnimatorStateAvailable
            && IsFinite(AnimatorNormalizedTime)
            && AnimatorNormalizedTime >= 0f;

        internal RagdollBipedStaggerPhaseSignals(
            bool selectedFootGrounded,
            bool animatorStateAvailable,
            float animatorNormalizedTime,
            bool balanceRecovered)
        {
            SelectedFootGrounded = selectedFootGrounded;
            AnimatorStateAvailable = animatorStateAvailable;
            AnimatorNormalizedTime = animatorNormalizedTime;
            BalanceRecovered = balanceRecovered;
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

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
        internal bool LiftOffContactObserved { get; private set; }
        internal float StableBalanceElapsedTime { get; private set; }

        internal RagdollBipedStaggerStateMachine()
        {
            Reset();
        }

        internal void Reset()
        {
            State = RagdollBipedStaggerState.Idle;
            StateElapsedTime = 0f;
            StepCount = 0;
            LiftOffContactObserved = false;
            StableBalanceElapsedTime = 0f;
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
            LiftOffContactObserved = false;
            StableBalanceElapsedTime = 0f;
            return true;
        }

        /// <summary>Forces Failed regardless of the current phase.</summary>
        internal void RegisterStepFailed()
        {
            Fail();
        }

        /// <summary>
        /// Advances the legacy timer-only compatibility path. The production
        /// behaviour uses the signal overload below; this overload preserves
        /// callers that intentionally exercise timeout fallback semantics.
        /// </summary>
        internal bool Advance(
            float deltaTime,
            float liftOffDuration,
            float swingDuration,
            float replantDuration,
            float settlingDuration)
        {
            return Advance(
                deltaTime,
                liftOffDuration,
                swingDuration,
                replantDuration,
                settlingDuration,
                new RagdollBipedStaggerPhaseSignals(
                    selectedFootGrounded: true,
                    animatorStateAvailable: false,
                    animatorNormalizedTime: 0f,
                    balanceRecovered: false));
        }

        /// <summary>
        /// Advances one fixed-step phase using physical/authored evidence first
        /// and the existing durations as finite fail-safe timeouts.
        /// </summary>
        internal bool Advance(
            float deltaTime,
            float liftOffDuration,
            float swingDuration,
            float replantDuration,
            float settlingDuration,
            RagdollBipedStaggerPhaseSignals signals,
            float animatorReplantProgress = 0.75f,
            float stableBalanceDuration = 0.04f)
        {
            StateElapsedTime += SanitizeDelta(deltaTime);
            switch (State)
            {
                case RagdollBipedStaggerState.LiftOff:
                    if (!signals.SelectedFootGrounded)
                        LiftOffContactObserved = true;
                    return (LiftOffContactObserved || HasTimedOut(liftOffDuration))
                        && TransitionTo(RagdollBipedStaggerState.Swing);
                case RagdollBipedStaggerState.Swing:
                    bool replantContact = LiftOffContactObserved
                        && signals.SelectedFootGrounded;
                    bool animatorReached = signals.HasAnimatorProgress
                        && IsFinite(animatorReplantProgress)
                        && signals.AnimatorNormalizedTime >= Mathf.Clamp01(
                            animatorReplantProgress);
                    return (replantContact || animatorReached
                        || HasTimedOut(swingDuration))
                        && TransitionTo(RagdollBipedStaggerState.Replant);
                case RagdollBipedStaggerState.Replant:
                    return (signals.SelectedFootGrounded
                        || HasTimedOut(replantDuration))
                        && TransitionTo(RagdollBipedStaggerState.Settling);
                case RagdollBipedStaggerState.Settling:
                    if (signals.BalanceRecovered)
                    {
                        StableBalanceElapsedTime += SanitizeDelta(deltaTime);
                    }
                    else
                    {
                        StableBalanceElapsedTime = 0f;
                    }

                    bool stableWindowComplete = IsFinite(stableBalanceDuration)
                        && StableBalanceElapsedTime >= Mathf.Max(0f, stableBalanceDuration);
                    return (stableWindowComplete || HasTimedOut(settlingDuration))
                        && TransitionTo(RagdollBipedStaggerState.Idle);
                default:
                    return false;
            }
        }

        void Fail()
        {
            State = RagdollBipedStaggerState.Failed;
            StateElapsedTime = 0f;
            StableBalanceElapsedTime = 0f;
        }

        bool HasTimedOut(float duration)
        {
            if (!IsFinite(duration)) return true;
            float safeDuration = Mathf.Max(0f, duration);
            return safeDuration <= Mathf.Epsilon || StateElapsedTime >= safeDuration;
        }

        static float SanitizeDelta(float deltaTime)
        {
            return IsFinite(deltaTime) ? Mathf.Max(0f, deltaTime) : 0f;
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        bool TransitionTo(RagdollBipedStaggerState next)
        {
            State = next;
            StateElapsedTime = 0f;
            if (next == RagdollBipedStaggerState.Settling)
                StableBalanceElapsedTime = 0f;
            return true;
        }
    }
}
