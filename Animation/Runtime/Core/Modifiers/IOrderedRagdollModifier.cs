namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Optional ordering contract shared by bone-profile and mapping modifiers.
    /// Modifiers with equal stage and priority retain their component order.
    /// </summary>
    public interface IOrderedRagdollModifier
    {
        RagdollModifierStage Stage { get; }
        int Priority { get; }
    }
}
