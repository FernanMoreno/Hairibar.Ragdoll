namespace Hairibar.Ragdoll.Animation
{
    internal static class RagdollModifierOrdering
    {
        /// <summary>
        /// Stable insertion sort. Modifier arrays are small and this runs only at initialization.
        /// </summary>
        internal static void StableSort<T>(T[] modifiers) where T : class
        {
            if (modifiers == null || modifiers.Length < 2) return;

            for (int i = 1; i < modifiers.Length; i++)
            {
                T current = modifiers[i];
                int destination = i;

                while (destination > 0 && Compare(modifiers[destination - 1], current) > 0)
                {
                    modifiers[destination] = modifiers[destination - 1];
                    destination--;
                }

                modifiers[destination] = current;
            }
        }

        static int Compare(object first, object second)
        {
            IOrderedRagdollModifier firstOrdered = first as IOrderedRagdollModifier;
            IOrderedRagdollModifier secondOrdered = second as IOrderedRagdollModifier;

            RagdollModifierStage firstStage = firstOrdered != null
                ? firstOrdered.Stage
                : RagdollModifierStage.Legacy;
            RagdollModifierStage secondStage = secondOrdered != null
                ? secondOrdered.Stage
                : RagdollModifierStage.Legacy;

            int stageComparison = firstStage.CompareTo(secondStage);
            if (stageComparison != 0) return stageComparison;

            int firstPriority = firstOrdered != null ? firstOrdered.Priority : 0;
            int secondPriority = secondOrdered != null ? secondOrdered.Priority : 0;
            return firstPriority.CompareTo(secondPriority);
        }
    }
}
