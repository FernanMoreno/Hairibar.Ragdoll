namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Implemented by runtime systems that multiply or otherwise adjust mapping weights
    /// immediately before the ragdoll pose is written back to the target hierarchy.
    /// </summary>
    public interface IRagdollMappingModifier
    {
        void ModifyMapping(
            ref RagdollMappingWeights mappingWeights,
            RagdollAnimator.AnimatedPair pair);
    }
}
