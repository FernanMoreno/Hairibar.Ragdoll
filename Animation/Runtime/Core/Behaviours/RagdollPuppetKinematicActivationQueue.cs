namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Allocation-free one-slot queue. Collision callbacks only enqueue; the simulation
    /// mode changes from the next BehaviourPuppet fixed-step callback.
    /// </summary>
    internal struct RagdollPuppetKinematicActivationQueue
    {
        bool hasRequest;
        RagdollPuppetKinematicActivationSource source;
        float impulse;
        float fixedTime;

        internal bool HasRequest => hasRequest;

        internal void Reset()
        {
            this = default(RagdollPuppetKinematicActivationQueue);
        }

        internal void Request(
            RagdollPuppetKinematicActivationSource requestedSource,
            float requestedImpulse,
            float requestedFixedTime)
        {
            if (requestedSource == RagdollPuppetKinematicActivationSource.None
                || float.IsNaN(requestedImpulse)
                || float.IsInfinity(requestedImpulse)
                || requestedImpulse < 0f
                || float.IsNaN(requestedFixedTime)
                || float.IsInfinity(requestedFixedTime))
            {
                return;
            }

            if (hasRequest && requestedImpulse < impulse)
            {
                return;
            }

            hasRequest = true;
            source = requestedSource;
            impulse = requestedImpulse;
            fixedTime = requestedFixedTime;
        }

        internal bool TryConsume(
            out RagdollPuppetKinematicActivationSource consumedSource,
            out float consumedImpulse,
            out float consumedFixedTime)
        {
            if (!hasRequest)
            {
                consumedSource = RagdollPuppetKinematicActivationSource.None;
                consumedImpulse = 0f;
                consumedFixedTime = 0f;
                return false;
            }

            consumedSource = source;
            consumedImpulse = impulse;
            consumedFixedTime = fixedTime;
            Reset();
            return true;
        }
    }
}
