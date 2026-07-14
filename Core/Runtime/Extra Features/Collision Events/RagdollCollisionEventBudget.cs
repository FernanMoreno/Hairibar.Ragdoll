namespace Hairibar.Ragdoll
{
    /// <summary>
    /// Limits collision processing per physics timestamp without depending on script
    /// execution order between FixedUpdate and collision callbacks.
    /// </summary>
    internal struct RagdollCollisionEventBudget
    {
        bool initialized;
        float fixedTime;
        int consumed;

        public int Consumed => consumed;

        public bool TryConsume(float currentFixedTime, int maximumEvents)
        {
            if (!initialized || currentFixedTime != fixedTime)
            {
                initialized = true;
                fixedTime = currentFixedTime;
                consumed = 0;
            }

            if (maximumEvents > 0 && consumed >= maximumEvents)
            {
                return false;
            }

            consumed++;
            return true;
        }
    }
}
