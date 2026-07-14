namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Coarse execution stages for systems that modify animation drives or mapping.
    /// Unordered legacy modifiers remain first and preserve their component order.
    /// </summary>
    public enum RagdollModifierStage
    {
        Legacy = 0,
        RuntimeState = 200,
        Impact = 300,
        Behaviour = 400,
        GameplayOverride = 500,
        Final = 600
    }
}
