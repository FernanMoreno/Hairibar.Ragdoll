using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Pure deterministic state and timing core for RagdollPuppetBehaviour.
    /// Unity object access remains in the behaviour component so this class can be tested
    /// without constructing a physical ragdoll.
    /// </summary>
    internal sealed class RagdollPuppetStateMachine
    {
        internal RagdollPuppetState State { get; private set; }
        internal float StateElapsedTime { get; private set; }

        internal RagdollPuppetStateMachine()
        {
            Reset(RagdollPuppetState.Puppet);
        }

        internal void Reset(RagdollPuppetState state)
        {
            State = state;
            StateElapsedTime = 0f;
        }

        internal bool TryTransition(RagdollPuppetState next)
        {
            if (next == State || !IsTransitionAllowed(State, next))
            {
                return false;
            }

            State = next;
            StateElapsedTime = 0f;
            return true;
        }

        /// <summary>
        /// Advances state-local time. Returns true when GetUp completed automatically.
        /// </summary>
        internal bool Advance(float deltaTime, float getUpDuration)
        {
            StateElapsedTime += Mathf.Max(0f, deltaTime);
            if (State != RagdollPuppetState.GetUp)
            {
                return false;
            }

            float duration = Mathf.Max(0f, getUpDuration);
            if (duration > Mathf.Epsilon && StateElapsedTime < duration)
            {
                return false;
            }

            State = RagdollPuppetState.Puppet;
            StateElapsedTime = 0f;
            return true;
        }

        internal float GetUpProgress(float getUpDuration)
        {
            if (State == RagdollPuppetState.Puppet) return 1f;
            if (State != RagdollPuppetState.GetUp) return 0f;

            float duration = Mathf.Max(0f, getUpDuration);
            return duration <= Mathf.Epsilon
                ? 1f
                : Mathf.Clamp01(StateElapsedTime / duration);
        }

        internal static bool IsTransitionAllowed(
            RagdollPuppetState current,
            RagdollPuppetState next)
        {
            switch (current)
            {
                case RagdollPuppetState.Puppet:
                    return next == RagdollPuppetState.Unpinned;

                case RagdollPuppetState.Unpinned:
                    return next == RagdollPuppetState.GetUp;

                case RagdollPuppetState.GetUp:
                    return next == RagdollPuppetState.Puppet
                        || next == RagdollPuppetState.Unpinned;

                default:
                    throw new ArgumentOutOfRangeException(nameof(current));
            }
        }
    }
}
