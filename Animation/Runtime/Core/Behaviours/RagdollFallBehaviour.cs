using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Falling behaviour that drives an Animator blend tree from pelvis height and
    /// vertical velocity. It follows PuppetMaster's documented BehaviourFall surface
    /// while using Hairibar's behaviour, mapping and binding infrastructure.
    /// </summary>
    [AddComponentMenu("Ragdoll/Behaviours/Ragdoll Fall Behaviour")]
    public sealed class RagdollFallBehaviour : RagdollBehaviourBase
    {
        const int RaycastCapacity = 32;

        [Header("Animation")]
        [SerializeField] string stateName = "Falling";
        [SerializeField, Min(0f)] float transitionDuration = 0.4f;
        [SerializeField] int layer = -1;
        [SerializeField] float fixedTime = float.NegativeInfinity;
        [SerializeField] string blendParameter = "FallBlend";

        [Header("Fall Sampling")]
        [SerializeField] LayerMask raycastLayers = ~0;
        [SerializeField, Min(0f)] float writheHeight = 4f;
        [SerializeField, Min(0f)] float writheVerticalVelocity = 1f;
        [SerializeField, Min(0f)] float blendSpeed = 3f;
        [SerializeField, Min(0f)] float blendMappingSpeed = 1f;

        [Header("Completion")]
        [SerializeField] bool canEnd;
        [SerializeField, Min(0f)] float minimumTime = 1.5f;
        [SerializeField, Min(0f)] float maximumEndVelocity = 0.5f;
        [SerializeField] RagdollPuppetEvent onEnd;

        RaycastHit[] hits = new RaycastHit[RaycastCapacity];

        Animator targetAnimator;
        RagdollAnimator.AnimatedPair pelvis;
        int blendParameterHash;
        float blend;
        float mappingBlend;
        float elapsedTime;
        float sampledHeight;
        float sampledVerticalVelocity;
        bool hasGround;
        bool ended;

        public string StateName
        {
            get => stateName;
            set => stateName = value ?? string.Empty;
        }
        public float TransitionDuration
        {
            get => transitionDuration;
            set => transitionDuration = SanitizeNonNegative(value);
        }
        public int Layer
        {
            get => layer;
            set => layer = Mathf.Max(-1, value);
        }
        public float FixedTime
        {
            get => fixedTime;
            set => fixedTime = SanitizeFixedTime(value);
        }
        public string BlendParameter
        {
            get => blendParameter;
            set
            {
                blendParameter = value ?? string.Empty;
                CacheBlendParameter();
            }
        }
        public LayerMask RaycastLayers
        {
            get => raycastLayers;
            set => raycastLayers = value;
        }
        public float WritheHeight
        {
            get => writheHeight;
            set => writheHeight = SanitizeNonNegative(value);
        }
        public float WritheVerticalVelocity
        {
            get => writheVerticalVelocity;
            set => writheVerticalVelocity = SanitizeNonNegative(value);
        }
        public float WritheYVelocity
        {
            get => WritheVerticalVelocity;
            set => WritheVerticalVelocity = value;
        }
        public float BlendSpeed
        {
            get => blendSpeed;
            set => blendSpeed = SanitizeNonNegative(value);
        }
        public float BlendMappingSpeed
        {
            get => blendMappingSpeed;
            set => blendMappingSpeed = SanitizeNonNegative(value);
        }
        public bool CanEnd
        {
            get => canEnd;
            set => canEnd = value;
        }
        public float MinimumTime
        {
            get => minimumTime;
            set => minimumTime = SanitizeNonNegative(value);
        }
        public float MaximumEndVelocity
        {
            get => maximumEndVelocity;
            set => maximumEndVelocity = SanitizeNonNegative(value);
        }
        public float CurrentBlend => blend;
        public float MappingBlend => mappingBlend;
        public float ElapsedTime => elapsedTime;
        public float SampledHeight => sampledHeight;
        public float SampledVerticalVelocity => sampledVerticalVelocity;
        public bool HasGround => hasGround;
        public bool HasEnded => ended;
        public RagdollPuppetEvent OnEnd
        {
            get => onEnd;
            set => onEnd = value;
        }
        public UnityEvent OnEndUnityEvent => onEnd.UnityEvent;

        /// <summary>Runtime-only completion callback, invoked with the serialized event.</summary>
        public event Action Ended;

        protected override void OnBehaviourInitialize()
        {
            RefreshTargetAnimator();
            RefreshPelvis();
            CacheBlendParameter();
        }

        protected override void OnBehaviourHierarchyChanged(
            IReadOnlyList<RagdollMuscleChange> added,
            IReadOnlyList<RagdollMuscleChange> removed)
        {
            RefreshPelvis();
        }

        protected override void OnBehaviourActivated()
        {
            RefreshTargetAnimator();
            elapsedTime = 0f;
            mappingBlend = 0f;
            ended = false;
            SamplePelvis();
            blend = ResolveTargetBlend();

            if (!targetAnimator || string.IsNullOrEmpty(stateName)) return;

            targetAnimator.CrossFadeInFixedTime(
                stateName,
                transitionDuration,
                layer,
                fixedTime);
            WriteAnimatorBlend();
        }

        protected override void OnBehaviourReactivated()
        {
            RefreshTargetAnimator();
            RefreshPelvis();
        }

        protected override void OnBehaviourFixedUpdate(float deltaTime)
        {
            RefreshTargetAnimator();
            if (pelvis == null) RefreshPelvis();
            if (pelvis == null) return;

            deltaTime = SanitizeDeltaTime(deltaTime);
            elapsedTime += deltaTime;
            mappingBlend = RagdollFallMath.MoveBlend(
                mappingBlend,
                1f,
                blendMappingSpeed,
                deltaTime);

            SamplePelvis();
            blend = RagdollFallMath.MoveBlend(
                blend,
                ResolveTargetBlend(),
                blendSpeed,
                deltaTime);
            WriteAnimatorBlend();

            Rigidbody body = pelvis.RagdollBone.Rigidbody;
            float speed = body ? body.linearVelocity.magnitude : 0f;
            if (!RagdollFallMath.CanEnd(
                canEnd,
                ended,
                elapsedTime,
                minimumTime,
                speed,
                maximumEndVelocity))
            {
                return;
            }

            CompleteFall();
        }

        void CompleteFall()
        {
            if (ended) return;
            ended = true;
            try { onEnd.Invoke(this); }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception, this);
            }
            Action callback = Ended;
            if (callback == null) return;
            Delegate[] subscribers = callback.GetInvocationList();
            for (int index = 0; index < subscribers.Length; index++)
            {
                try { ((Action)subscribers[index])(); }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception, this);
                }
            }
        }

        protected override void OnModifyMapping(
            ref RagdollMappingWeights mappingWeights,
            RagdollAnimator.AnimatedPair pair)
        {
            mappingWeights.positionWeight *= mappingBlend;
            mappingWeights.rotationWeight *= mappingBlend;
        }

        void RefreshPelvis()
        {
            pelvis = null;
            IReadOnlyList<RagdollAnimator.AnimatedPair> pairs = Context.Pairs;
            for (int index = 0; index < pairs.Count; index++)
            {
                if (pairs[index].RagdollBone.IsRoot)
                {
                    pelvis = pairs[index];
                    return;
                }
            }
        }

        void SamplePelvis()
        {
            hasGround = false;
            sampledHeight = 0f;
            sampledVerticalVelocity = 0f;
            if (pelvis == null) return;

            Rigidbody body = pelvis.RagdollBone.Rigidbody;
            if (!body) return;

            Vector3 up = RagdollFallMath.ResolveUp(Physics.gravity);
            sampledVerticalVelocity = Vector3.Dot(body.linearVelocity, up);
            Vector3 origin = body.worldCenterOfMass;

            int hitCount;
            bool complete = RagdollRaycastBuffer.TryRaycastComplete(
                origin,
                -up,
                Mathf.Infinity,
                raycastLayers.value,
                ref hits,
                out hitCount);
            if (!complete) return;

            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < hitCount; index++)
            {
                Collider collider = hits[index].collider;
                if (!collider || IsPuppetCollider(collider)) continue;
                if (hits[index].distance >= nearestDistance) continue;

                nearestDistance = hits[index].distance;
                hasGround = true;
            }

            if (hasGround) sampledHeight = nearestDistance;
        }

        bool IsPuppetCollider(Collider collider)
        {
            RagdollBone ignored;
            return Context.Bindings.TryGetBone(collider, out ignored)
                || Context.Bindings.TryGetBoneFromAttachedRigidbody(
                    collider,
                    out ignored);
        }

        float ResolveTargetBlend()
        {
            // No ground in the queried layers means no protective surface is near.
            if (!hasGround) return 1f;

            return RagdollFallMath.ResolveWritheBlend(
                sampledHeight,
                sampledVerticalVelocity,
                writheHeight,
                writheVerticalVelocity);
        }

        void CacheBlendParameter()
        {
            blendParameterHash = string.IsNullOrEmpty(blendParameter)
                ? 0
                : Animator.StringToHash(blendParameter);
        }

        void RefreshTargetAnimator()
        {
            Animator resolved = Context != null && Context.Animator
                ? Context.Animator.TargetAnimator
                : null;
            if (resolved == targetAnimator) return;
            targetAnimator = resolved;
            CacheBlendParameter();
        }

        void WriteAnimatorBlend()
        {
            if (targetAnimator && blendParameterHash != 0)
            {
                targetAnimator.SetFloat(blendParameterHash, blend);
            }
        }

        static float SanitizeDeltaTime(float deltaTime)
        {
            return float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)
                ? 0f
                : Mathf.Max(0f, deltaTime);
        }

        static float SanitizeNonNegative(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : Mathf.Max(0f, value);
        }

        static float SanitizeFixedTime(float value)
        {
            if (float.IsNegativeInfinity(value)) return value;
            return float.IsNaN(value) || float.IsPositiveInfinity(value)
                ? float.NegativeInfinity
                : value;
        }

        void OnValidate()
        {
            stateName = stateName ?? string.Empty;
            transitionDuration = SanitizeNonNegative(transitionDuration);
            layer = Mathf.Max(-1, layer);
            fixedTime = SanitizeFixedTime(fixedTime);
            blendParameter = blendParameter ?? string.Empty;
            writheHeight = SanitizeNonNegative(writheHeight);
            writheVerticalVelocity = SanitizeNonNegative(writheVerticalVelocity);
            blendSpeed = SanitizeNonNegative(blendSpeed);
            blendMappingSpeed = SanitizeNonNegative(blendMappingSpeed);
            minimumTime = SanitizeNonNegative(minimumTime);
            maximumEndVelocity = SanitizeNonNegative(maximumEndVelocity);
            CacheBlendParameter();
        }
    }
}
