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
    /// Hairibar maps the public fields to observable PhysX behaviour: MaxForceMlp
    /// scales the effective torque limit and DamperForSpring attenuates torque
    /// already travelling around the correction axis. This is Hairibar design,
    /// not a claim of RootMotion implementation parity.
    /// </summary>
    [Serializable]
    public struct RagdollBipedBalancerSettings
    {
        [Tooltip("Hairibar: damping against current ankle angular velocity around the correction axis. Increase to soften balancing response.")]
        [SerializeField] float damperForSpring;
        [Tooltip("Hairibar: multiplier for effective maximum corrective torque.")]
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
            maxForceMlp = 0.05f,
            iMlp = 1f,
            velocityF = 0.5f,
            copOffset = Vector3.zero,
            torqueMlp = 0f,
            maxTorqueMag = 45f
        };
    }
}
