using System;
using UnityEngine;
using UnityEngine.Events;

namespace Hairibar.Ragdoll.Animation
{
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
            RagdollBehaviourController controller = source
                ? source.Controller
                : null;

            if (controller && !string.IsNullOrEmpty(switchToBehaviour))
            {
                controller.ActivateByExactTypeName(switchToBehaviour);
            }

            Animator animator = controller && controller.Context != null
                ? controller.Context.Animator.GetComponent<Animator>()
                : null;
            if (animations != null)
            {
                for (int index = 0; index < animations.Length; index++)
                {
                    animations[index]?.Invoke(animator);
                }
            }

            unityEvent?.Invoke();
        }
    }
}
