using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Reusable center-of-mass and center-of-pressure module. Contact pressure is
    /// accumulated from the shared collision stream and resolved once per fixed step.
    /// </summary>
    [Serializable]
    public sealed class RagdollCenterOfMassSubBehaviour : RagdollSubBehaviourBase
    {
        [SerializeField] LayerMask groundLayers = -1;
        [SerializeField, Min(0f)] float probeStartOffset = 0.1f;
        [SerializeField, Min(0.001f)] float probeDistance = 1f;
        [SerializeField, Range(0f, 89.9f)] float maximumGroundAngle = 60f;

        [NonSerialized] RagdollGroundProbe probe;

        public LayerMask GroundLayers
        {
            get => groundLayers;
            set => groundLayers = value;
        }

        public float ProbeStartOffset
        {
            get => probeStartOffset;
            set => probeStartOffset = SanitizeNonNegative(value, 0f);
        }

        public float ProbeDistance
        {
            get => probeDistance;
            set => probeDistance = Mathf.Max(
                0.001f,
                SanitizeNonNegative(value, 1f));
        }

        public float MaximumGroundAngle
        {
            get => maximumGroundAngle;
            set => maximumGroundAngle = Mathf.Clamp(
                SanitizeNonNegative(value, 60f),
                0f,
                89.9f);
        }

        public RagdollGroundingSnapshot Snapshot => probe != null
            ? probe.Snapshot
            : RagdollGroundingSnapshot.Empty;

        public Vector3 Up => probe != null ? probe.Up : ResolveUp();

        public void Configure(
            LayerMask layers,
            float startOffset,
            float distance,
            float maximumAngle)
        {
            GroundLayers = layers;
            ProbeStartOffset = startOffset;
            ProbeDistance = distance;
            MaximumGroundAngle = maximumAngle;
        }

        public void RegisterCollision(RagdollCollisionEvent collisionEvent)
        {
            if (!IsInitialized || !IsActive || probe == null) return;
            probe.RegisterCollision(
                collisionEvent,
                groundLayers,
                maximumGroundAngle);
        }

        public void Reset()
        {
            if (probe != null) probe.Reset();
        }

        protected override void OnInitialize()
        {
            probe = new RagdollGroundProbe(Context);
            Normalize();
        }

        protected override void OnActivate()
        {
            Reset();
        }

        protected override void OnDeactivate()
        {
            Reset();
        }

        protected override void OnFixedUpdate(float deltaTime)
        {
            probe.Update(
                deltaTime,
                groundLayers,
                probeStartOffset,
                probeDistance,
                maximumGroundAngle);
        }

        protected override void OnShutdown()
        {
            probe = null;
        }

        void Normalize()
        {
            ProbeStartOffset = probeStartOffset;
            ProbeDistance = probeDistance;
            MaximumGroundAngle = maximumGroundAngle;
        }

        static Vector3 ResolveUp()
        {
            return Physics.gravity.sqrMagnitude > Mathf.Epsilon
                ? -Physics.gravity.normalized
                : Vector3.up;
        }

        static float SanitizeNonNegative(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) value = fallback;
            return Mathf.Max(0f, value);
        }
    }
}
