using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Advanced world-space pinning controls. These values shape temporary pin authority
    /// without replacing the authored animation spring and damping profile.
    /// </summary>
    [Serializable]
    public struct RagdollPinSettings
    {
        const int CurrentSerializedVersion = 1;
        const float DefaultPinPow = 4f;
        const float DefaultPinDistanceFalloff = 5f;

        [SerializeField, Range(1f, 8f)]
        [Tooltip("Adjusts the slope of temporary pin authority while it is between zero and one.")]
        float pinPow;

        [SerializeField, Range(0f, 100f)]
        [Tooltip("Reduces world-space position pinning as a ragdoll bone moves farther from its animated Target.")]
        float pinDistanceFalloff;

        [SerializeField]
        [Tooltip("Adds a separate world-space torque channel driven by pin authority. The muscle Slerp Drive remains active independently.")]
        bool angularPinning;
        [SerializeField, HideInInspector] int serializedVersion;

        public float PinPow
        {
            get => pinPow;
            set => pinPow = SanitizePinPow(value);
        }

        public float PinDistanceFalloff
        {
            get => pinDistanceFalloff;
            set => pinDistanceFalloff = SanitizeFalloff(value);
        }

        public bool AngularPinning
        {
            get => angularPinning;
            set => angularPinning = value;
        }

        public RagdollPinSettings(
            float pinPow,
            float pinDistanceFalloff,
            bool angularPinning)
        {
            this.pinPow = SanitizePinPow(pinPow);
            this.pinDistanceFalloff = SanitizeFalloff(pinDistanceFalloff);
            this.angularPinning = angularPinning;
            serializedVersion = CurrentSerializedVersion;
        }

        public static RagdollPinSettings Default =>
            new RagdollPinSettings(
                DefaultPinPow,
                DefaultPinDistanceFalloff,
                false);

        internal void Normalize()
        {
            if (serializedVersion < CurrentSerializedVersion)
            {
                // Missing sprint-0029 data must receive the published defaults. The
                // version marker preserves intentional zero/false values authored later.
                pinPow = DefaultPinPow;
                pinDistanceFalloff = DefaultPinDistanceFalloff;
                angularPinning = false;
                serializedVersion = CurrentSerializedVersion;
                return;
            }

            pinPow = SanitizePinPow(pinPow);
            pinDistanceFalloff = SanitizeFalloff(pinDistanceFalloff);
        }

        static float SanitizePinPow(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                value = DefaultPinPow;
            }
            return Mathf.Clamp(value, 1f, 8f);
        }

        static float SanitizeFalloff(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                value = DefaultPinDistanceFalloff;
            }
            return Mathf.Clamp(value, 0f, 100f);
        }
    }
}
