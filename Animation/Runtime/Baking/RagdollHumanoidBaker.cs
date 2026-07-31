using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    [AddComponentMenu("Ragdoll/Baking/Ragdoll Humanoid Baker")]
    [RequireComponent(typeof(Animator))]
    public sealed class RagdollHumanoidBaker : RagdollBaker
    {
        public bool bakeHandIK = true;
        [Min(0f)] public float IKKeyReductionError = 0.005f;
        [Min(1)] public int muscleFrameRateDiv = 1;

        public override Transform RecordingRoot => transform;
        public Animator Animator => GetComponent<Animator>();
    }
}
