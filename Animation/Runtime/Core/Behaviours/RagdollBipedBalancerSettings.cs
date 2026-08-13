using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Mirrors RootMotion's SubBehaviourBalancer.Settings field-for-field (same
    /// names/defaults, verified against the official Doxygen doc) so a reader
    /// familiar with PuppetMaster can map settings directly. The internal
    /// correction math (RagdollBipedBalancerMath) is Hairibar-owned: RootMotion's
    /// own implementation is closed-source, only the public settings surface and
    /// their documented meaning are public.
    ///
    /// Partial: DamperForSpring/MaxForceMlp are exposed for field-for-field
    /// parity but have no wired effect yet (RagdollBipedBalancerMath does not
    /// read them) -- only IMlp/VelocityF/CopOffset/TorqueMlp/MaxTorqueMag are
    /// applied by the current reactive-torque implementation.
    /// </summary>
    [Serializable]
    public struct RagdollBipedBalancerSettings
    {
        [Tooltip("NOT YET WIRED to any joint drive -- settings-surface parity only. Ankle joint damper/spring; increase to make the balancing effect softer.")]
        [SerializeField] float damperForSpring;
        [Tooltip("NOT YET WIRED to any joint drive -- settings-surface parity only. Multiplier for joint max force.")]
        [SerializeField] float maxForceMlp;
        [Tooltip("Multiplier for the inertia tensor. Increasing this will increase the balancing forces.")]
        [SerializeField] float iMlp;
        [Tooltip("Velocity-based prediction.")]
        [SerializeField] float velocityF;
        [Tooltip("World space offset for the center of pressure. Can be used to make the character lean in a certain direction.")]
        [SerializeField] Vector3 copOffset;
        [Tooltip("The amount of torque applied to the lower legs to help keep the puppet balanced. Disabled (0) by default, matching the official product.")]
        [SerializeField] float torqueMlp;
        [Tooltip("Maximum magnitude of the torque applied to the lower legs if Torque Mlp > 0.")]
        [SerializeField] float maxTorqueMag;

        public float DamperForSpring
        {
            get => damperForSpring;
            set => damperForSpring = value;
        }
        public float MaxForceMlp
        {
            get => maxForceMlp;
            set => maxForceMlp = value;
        }
        public float IMlp
        {
            get => iMlp;
            set => iMlp = value;
        }
        public float VelocityF
        {
            get => velocityF;
            set => velocityF = value;
        }
        public Vector3 CopOffset
        {
            get => copOffset;
            set => copOffset = value;
        }
        public float TorqueMlp
        {
            get => torqueMlp;
            set => torqueMlp = value;
        }
        public float MaxTorqueMag
        {
            get => maxTorqueMag;
            set => maxTorqueMag = value;
        }

        public static RagdollBipedBalancerSettings Default => new RagdollBipedBalancerSettings
        {
            damperForSpring = 1f,
            maxForceMlp = 1f,
            iMlp = 1f,
            velocityF = 0.5f,
            copOffset = Vector3.zero,
            torqueMlp = 0f,
            maxTorqueMag = 45f
        };
    }
}
