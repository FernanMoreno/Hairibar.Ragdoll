using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Hairibar.Ragdoll.Animation
{
    public readonly struct RagdollPuppetEventInvocationResult
    {
        readonly Exception[] failures;

        internal RagdollPuppetEventInvocationResult(List<Exception> failures)
        {
            this.failures = failures == null || failures.Count == 0
                ? Array.Empty<Exception>()
                : failures.ToArray();
        }

        public bool Succeeded => failures == null || failures.Length == 0;
        public IReadOnlyList<Exception> Failures =>
            failures ?? Array.Empty<Exception>();

        public void ThrowIfFailed()
        {
            if (!Succeeded)
                throw new AggregateException(
                    "One or more RagdollPuppetEvent actions failed.",
                    failures);
        }
    }

    /// <summary>
    /// Serializable Animator action used by <see cref="RagdollPuppetEvent"/>.
    /// It mirrors PuppetMaster's documented AnimatorEvent contract without relying
    /// on UnityEvent parameter serialization.
    /// </summary>
    [Serializable]
    public sealed class RagdollAnimatorEvent
    {
        [SerializeField] string animationState = string.Empty;
        [SerializeField, Min(0f)] float crossfadeTime = 0.3f;
        [SerializeField] int layer;
        [SerializeField] bool resetNormalizedTime;

        public string AnimationState
        {
            get => animationState;
            set => animationState = value;
        }
        public float CrossfadeTime
        {
            get => crossfadeTime;
            set => crossfadeTime = SanitizeDuration(value);
        }
        public int Layer
        {
            get => layer;
            set => layer = Mathf.Max(-1, value);
        }
        public bool ResetNormalizedTime
        {
            get => resetNormalizedTime;
            set => resetNormalizedTime = value;
        }

        public void Invoke(Animator animator)
        {
            if (!animator || string.IsNullOrEmpty(animationState)) return;

            float duration = SanitizeDuration(crossfadeTime);
            int targetLayer = Mathf.Max(-1, layer);
            if (resetNormalizedTime)
            {
                animator.CrossFade(animationState, duration, targetLayer, 0f);
            }
            else
            {
                animator.CrossFade(animationState, duration, targetLayer);
            }
        }

        static float SanitizeDuration(float value)
        {
            return float.IsNaN(value) || float.IsNegativeInfinity(value)
                ? 0f
                : Mathf.Max(0f, value);
        }
    }

    /// <summary>
    /// Actions fired by a ragdoll behaviour event: optionally switch behaviour,
    /// cross-fade Animator states, then invoke user callbacks.
    /// </summary>
    [Serializable]
    public struct RagdollPuppetEvent
    {
        [Tooltip("Exact CLR type name of a behaviour owned by the same controller.")]
        [SerializeField] string switchToBehaviour;
        [SerializeField] RagdollAnimatorEvent[] animations;
        [SerializeField] UnityEvent unityEvent;

        public string SwitchToBehaviour
        {
            get => switchToBehaviour;
            set => switchToBehaviour = value;
        }
        public RagdollAnimatorEvent[] Animations
        {
            get => animations;
            set => animations = value;
        }
        public UnityEvent UnityEvent
        {
            get
            {
                if (unityEvent == null) unityEvent = new UnityEvent();
                return unityEvent;
            }
            set => unityEvent = value;
        }

        /// <summary>Executes every configured action in serialized order.</summary>
        public void Invoke(RagdollBehaviourBase source)
        {
            InvokeWithDiagnostics(source).ThrowIfFailed();
        }

        /// <summary>
        /// Executes all independently addressable actions and returns every failure.
        /// Unity exposes a serialized UnityEvent only as one invocation unit, so a
        /// listener that aborts that unit is reported explicitly instead of hidden.
        /// </summary>
        public RagdollPuppetEventInvocationResult InvokeWithDiagnostics(
            RagdollBehaviourBase source)
        {
            List<Exception> failures = null;
            RagdollBehaviourController controller = source
                ? source.Controller
                : null;

            if (controller && !string.IsNullOrEmpty(switchToBehaviour))
            {
                string behaviourType = switchToBehaviour;
                TryInvoke(
                    () => controller.ActivateByExactTypeName(behaviourType),
                    ref failures);
            }

            Animator animator = controller && controller.Context != null
                ? controller.Context.Animator.TargetAnimator
                : null;
            if (animations != null)
            {
                for (int index = 0; index < animations.Length; index++)
                {
                    RagdollAnimatorEvent animation = animations[index];
                    if (animation == null) continue;
                    TryInvoke(() => animation.Invoke(animator), ref failures);
                }
            }

            if (unityEvent != null)
                TryInvoke(unityEvent.Invoke, ref failures);
            return new RagdollPuppetEventInvocationResult(failures);
        }

        static void TryInvoke(Action action, ref List<Exception> failures)
        {
            try { action(); }
            catch (Exception exception)
            {
                if (failures == null) failures = new List<Exception>();
                failures.Add(exception);
            }
        }
    }
}
